package com.example.ezacrosscontrol

import android.content.Context
import android.net.wifi.WifiManager
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.util.Log
import com.example.ezacrosscontrol.protocol.InputEventParser
import com.example.ezacrosscontrol.protocol.MessageEnvelope
import com.example.ezacrosscontrol.protocol.PairingIdentity
import okhttp3.*
import org.json.JSONObject
import java.util.concurrent.TimeUnit
import kotlin.math.min

class WebSocketClient(context: Context) {
    private val appContext = context.applicationContext
    private val pairingIdentity = PairingIdentity(appContext)
    private var wifiLock: WifiManager.WifiLock? = null
    private var client: OkHttpClient? = null
    @Volatile private var webSocket: WebSocket? = null
    private val mainHandler = Handler(Looper.getMainLooper())
    private var welcomeTimeout: Runnable? = null
    private var reconnectRunnable: Runnable? = null
    private var desiredConnection = false
    private var reconnectAttempt = 0
    @Volatile private var connectionAttemptId = 0L
    @Volatile private var authenticatedAttemptId = -1L
    private var lastIp = ""
    private var lastPort = ""
    private var connectionMode = "Auto"

    var onConnectionStateChanged: ((Boolean) -> Unit)? = null
    var onConnectionProgressChanged: ((Float) -> Unit)? = null
    var onConnectionFailure: ((String) -> Unit)? = null
    var onPairingRequested: ((String) -> Unit)? = null
    var onLatencyUpdated: ((Long) -> Unit)? = null
    var onLogMessage: ((String) -> Unit)? = null
    var onEventReceived: ((MessageEnvelope) -> Unit)? = null

    fun connect(ip: String, port: String, mode: String = "Auto") {
        val address = ip.trim()
        val octets = address.split(".")
        if (octets.size != 4 || octets.any { val number = it.toIntOrNull(); number == null || number !in 0..255 }) {
            onConnectionFailure?.invoke("Endereço do IP inválido")
            return
        }
        val portNumber = port.trim().toIntOrNull()
        if (portNumber == null || portNumber !in 1..65535) {
            onConnectionFailure?.invoke("Porta inválida")
            return
        }
        lastIp = address
        lastPort = portNumber.toString()
        connectionMode = mode
        desiredConnection = true
        reconnectAttempt = 0
        cancelReconnect()
        beginConnection()
    }

    private fun beginConnection() {
        if (!desiredConnection || webSocket != null) return
        val attemptId = ++connectionAttemptId
        onConnectionProgressChanged?.invoke(.15f)
        val url = "ws://$lastIp:$lastPort/"
        onLogMessage?.invoke("Connecting to $url")
        client = OkHttpClient.Builder().connectTimeout(10, TimeUnit.SECONDS).pingInterval(3, TimeUnit.SECONDS)
            .readTimeout(0, TimeUnit.MILLISECONDS).build()
        val listener = object : WebSocketListener() {
            override fun onOpen(socket: WebSocket, response: Response) {
                if (!acceptSocket(attemptId, socket)) return
                acquireWifiLatencyLock()
                onConnectionProgressChanged?.invoke(.55f)
                welcomeTimeout = Runnable {
                    if (webSocket === socket && authenticatedAttemptId != attemptId) {
                        socket.cancel()
                        disconnectInternal(socket)
                        scheduleReconnect("Host não respondeu")
                    }
                }.also { mainHandler.postDelayed(it, WELCOME_TIMEOUT_MS) }
            }

            override fun onMessage(socket: WebSocket, text: String) {
                if (!acceptSocket(attemptId, socket)) return
                val envelope = InputEventParser.parse(text) ?: run {
                    rejectIncompatible(socket, "Mensagem inválida do Host")
                    return
                }
                Log.i("EZAcrossSocket", "Received Host message: ${envelope.type}")
                if (envelope.protocolVersion != PROTOCOL_VERSION) {
                    rejectIncompatible(socket, "Protocolo do Host incompatível: atualize Host e Android")
                    return
                }
                when (envelope.type) {
                    "PAIR_CHALLENGE" -> {
                        val nonce = envelope.payload.optString("Nonce")
                        if (nonce.isBlank() || nonce.length > 256) rejectIncompatible(socket, "Desafio de pareamento inválido")
                        else sendHello(nonce)
                    }
                    "WELCOME" -> {
                        authenticatedAttemptId = attemptId
                        clearWelcomeTimeout()
                        reconnectAttempt = 0
                        onConnectionStateChanged?.invoke(true)
                        onLogMessage?.invoke("Authenticated Host session established")
                    }
                    "PONG" -> onLatencyUpdated?.invoke(System.currentTimeMillis() - envelope.payload.optLong("Timestamp"))
                    else -> onEventReceived?.invoke(envelope)
                }
            }

            override fun onClosing(socket: WebSocket, code: Int, reason: String) {
                if (attemptId == connectionAttemptId) socket.close(1000, null)
            }

            override fun onClosed(socket: WebSocket, code: Int, reason: String) {
                if (attemptId != connectionAttemptId || webSocket !== socket) return
                disconnectInternal(socket)
                if (code == 1008) {
                    desiredConnection = false
                    onConnectionFailure?.invoke(reason.ifBlank { "Pareamento recusado pelo Host" })
                } else scheduleReconnect("Conexão com Host encerrada")
            }

            override fun onFailure(socket: WebSocket, t: Throwable, response: Response?) {
                if (attemptId != connectionAttemptId || webSocket !== socket) return
                Log.w("EZAcrossSocket", "Host connection failed: ${t.javaClass.simpleName}")
                disconnectInternal(socket)
                scheduleReconnect("Falha na conexão com Host")
            }
        }
        val createdSocket = client!!.newWebSocket(Request.Builder().url(url).build(), listener)
        if (attemptId == connectionAttemptId && webSocket == null) webSocket = createdSocket
        else if (webSocket !== createdSocket) createdSocket.cancel()
    }

    /**
     * OkHttp can invoke onOpen/onMessage before newWebSocket returns on a fast LAN.
     * Bind that first callback to its attempt instead of discarding the pairing challenge.
     */
    private fun acceptSocket(attemptId: Long, socket: WebSocket): Boolean {
        if (attemptId != connectionAttemptId) return false
        if (webSocket == null) webSocket = socket
        return webSocket === socket
    }

    private fun rejectIncompatible(socket: WebSocket, message: String) {
        desiredConnection = false
        socket.close(1008, message)
        disconnectInternal(socket)
        onConnectionFailure?.invoke(message)
    }

    private fun scheduleReconnect(reason: String) {
        if (!desiredConnection) return
        if (reconnectAttempt >= MAX_RECONNECT_ATTEMPTS) {
            onConnectionFailure?.invoke("$reason. Reconexão pausada após $MAX_RECONNECT_ATTEMPTS tentativas.")
            return
        }
        reconnectAttempt++
        val delay = min(MAX_RECONNECT_DELAY_MS, BASE_RECONNECT_DELAY_MS shl (reconnectAttempt - 1))
        onConnectionFailure?.invoke("$reason. Tentando novamente ($reconnectAttempt/$MAX_RECONNECT_ATTEMPTS).")
        onConnectionProgressChanged?.invoke(.2f)
        reconnectRunnable = Runnable { beginConnection() }.also { mainHandler.postDelayed(it, delay) }
    }

    private fun cancelReconnect() { reconnectRunnable?.let { mainHandler.removeCallbacks(it) }; reconnectRunnable = null }

    private fun disconnectInternal(expected: WebSocket? = null) {
        if (expected != null && webSocket !== expected) return
        connectionAttemptId++
        authenticatedAttemptId = -1L
        webSocket = null
        clearWelcomeTimeout()
        releaseWifiLatencyLock()
        onConnectionStateChanged?.invoke(false)
        client?.dispatcher?.executorService?.shutdown()
        client = null
    }

    private fun clearWelcomeTimeout() { welcomeTimeout?.let { mainHandler.removeCallbacks(it) }; welcomeTimeout = null }

    @Suppress("DEPRECATION")
    private fun acquireWifiLatencyLock() {
        try {
            val manager = appContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager ?: return
            val mode = if (Build.VERSION.SDK_INT >= 29) WifiManager.WIFI_MODE_FULL_LOW_LATENCY else WifiManager.WIFI_MODE_FULL_HIGH_PERF
            wifiLock = manager.createWifiLock(mode, "EZAcross:connected-session").apply { setReferenceCounted(false); acquire() }
        } catch (error: Exception) { Log.w("EZAcrossWifi", "Wi-Fi latency lock unavailable", error) }
    }

    private fun releaseWifiLatencyLock() { wifiLock?.let { if (it.isHeld) it.release() }; wifiLock = null }

    fun disconnect() {
        desiredConnection = false
        cancelReconnect()
        webSocket?.close(1000, "User disconnected")
        disconnectInternal()
    }

    private fun sendHello(challenge: String) {
        try {
            val code = pairingIdentity.pairingCode
            onPairingRequested?.invoke(code)
            val sent = webSocket?.send(JSONObject().apply {
                put("Type", "HELLO"); put("ProtocolVersion", PROTOCOL_VERSION)
                put("Payload", JSONObject().apply {
                    put("DeviceName", Build.MODEL); put("ConnectionMode", connectionMode)
                    put("InstallationId", pairingIdentity.installationId); put("PublicKey", pairingIdentity.publicKey)
                    put("Signature", pairingIdentity.sign(challenge)); put("PairingCode", code)
                })
            }.toString()) == true
            if (!sent) throw IllegalStateException("The WebSocket was no longer open")
            Log.i("EZAcrossSocket", "Authenticated HELLO queued")
        } catch (error: Exception) {
            Log.e("EZAcrossSocket", "Unable to create authenticated HELLO", error)
            desiredConnection = false
            onConnectionFailure?.invoke("Não foi possível preparar o pareamento. Verifique o log do aplicativo.")
            webSocket?.cancel()
        }
    }

    fun sendPing() = send(JSONObject().apply {
        put("Type", "PING"); put("ProtocolVersion", PROTOCOL_VERSION)
        put("Payload", JSONObject().put("Timestamp", System.currentTimeMillis()))
    }.toString())

    fun sendMessage(message: String) = send(message)
    private fun send(message: String) { webSocket?.send(message) }

    companion object {
        const val PROTOCOL_VERSION = 2
        private const val WELCOME_TIMEOUT_MS = 10_000L
        private const val MAX_RECONNECT_ATTEMPTS = 8
        private const val BASE_RECONNECT_DELAY_MS = 1_000L
        private const val MAX_RECONNECT_DELAY_MS = 30_000L
    }
}
