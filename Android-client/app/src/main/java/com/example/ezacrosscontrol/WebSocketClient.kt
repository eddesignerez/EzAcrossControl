package com.example.ezacrosscontrol

import android.os.Build
import android.content.Context
import android.net.wifi.WifiManager
import android.util.Log
import android.os.Handler
import android.os.Looper
import okhttp3.*
import org.json.JSONObject
import java.util.concurrent.TimeUnit

class WebSocketClient(context: Context) {
    private val appContext = context.applicationContext
    private var wifiLock: WifiManager.WifiLock? = null
    private var client: OkHttpClient? = null
    private var webSocket: WebSocket? = null
    private val mainHandler = Handler(Looper.getMainLooper())
    private var welcomeTimeout: Runnable? = null
    var onConnectionStateChanged: ((Boolean) -> Unit)? = null
    var onConnectionProgressChanged: ((Float) -> Unit)? = null
    var onConnectionFailure: ((String) -> Unit)? = null
    var onLatencyUpdated: ((Long) -> Unit)? = null
    var onLogMessage: ((String) -> Unit)? = null
    var onEventReceived: ((com.example.ezacrosscontrol.protocol.MessageEnvelope) -> Unit)? = null

    private var connectionMode = "Auto"

    fun connect(ip: String, port: String, mode: String = "Auto") {
        connectionMode = mode
        if (webSocket != null) return

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
        onConnectionProgressChanged?.invoke(.15f)

        val url = "ws://$address:$portNumber/"
        onLogMessage?.invoke("Connecting to $url...")

        client = OkHttpClient.Builder()
            .connectTimeout(10, TimeUnit.SECONDS)
            .pingInterval(3, TimeUnit.SECONDS)
            .readTimeout(0, TimeUnit.MILLISECONDS)
            .build()

        val request = Request.Builder()
            .url(url)
            .build()

        val listener = object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                if (this@WebSocketClient.webSocket !== webSocket) return
                acquireWifiLatencyLock()
                onConnectionProgressChanged?.invoke(.65f)
                welcomeTimeout = Runnable {
                    if (this@WebSocketClient.webSocket === webSocket) {
                        webSocket.cancel()
                        disconnectInternal(webSocket)
                        onConnectionFailure?.invoke("Host não respondeu")
                    }
                }.also { mainHandler.postDelayed(it, 10000) }
                sendHello()
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                if (this@WebSocketClient.webSocket !== webSocket) return
                try {
                    val envelope = com.example.ezacrosscontrol.protocol.InputEventParser.parse(text)
                    if (envelope == null) {
                        onLogMessage?.invoke("Failed to parse message")
                        return
                    }

                    // Removed high-frequency logging per packet

                    onEventReceived?.invoke(envelope)

                    when (envelope.type) {
                        "WELCOME" -> {
                            if (envelope.protocolVersion != 1) {
                                disconnect()
                                onConnectionFailure?.invoke("Protocolo do Host incompatível")
                                return
                            }
                            clearWelcomeTimeout()
                            onConnectionStateChanged?.invoke(true)
                            onLogMessage?.invoke("Server said welcome! (v${envelope.protocolVersion})")
                        }
                        "PONG" -> {
                            val timestamp = envelope.payload.getLong("Timestamp")
                            val latency = System.currentTimeMillis() - timestamp
                            onLatencyUpdated?.invoke(latency)
                        }
                    }
                } catch (e: Exception) {
                    onLogMessage?.invoke("Process error: ${e.message}")
                }
            }

            override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                webSocket.close(1000, null)
                onLogMessage?.invoke("Closing: $reason")
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                if (this@WebSocketClient.webSocket !== webSocket) return
                onLogMessage?.invoke("Closed")
                disconnectInternal(webSocket)
                onConnectionFailure?.invoke("Conexão com Host encerrada")
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                if (this@WebSocketClient.webSocket !== webSocket) return
                onLogMessage?.invoke("Error: ${t.message}")
                disconnectInternal(webSocket)
                onConnectionFailure?.invoke("Falha na conexão com Host")
            }
        }

        webSocket = client?.newWebSocket(request, listener)
    }

    private fun disconnectInternal(expected: WebSocket? = null) {
        if (expected != null && webSocket !== expected) return
        webSocket = null
        clearWelcomeTimeout()
        releaseWifiLatencyLock()
        onConnectionStateChanged?.invoke(false)
        client?.dispatcher?.executorService?.shutdown()
    }

    private fun clearWelcomeTimeout() {
        welcomeTimeout?.let { mainHandler.removeCallbacks(it) }
        welcomeTimeout = null
    }

    @Suppress("DEPRECATION")
    private fun acquireWifiLatencyLock() {
        try {
            val manager = appContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager ?: return
            val mode = if (Build.VERSION.SDK_INT >= 29) WifiManager.WIFI_MODE_FULL_LOW_LATENCY
                else WifiManager.WIFI_MODE_FULL_HIGH_PERF
            val lock = manager.createWifiLock(mode, "EZAcross:connected-session")
            lock.setReferenceCounted(false)
            lock.acquire()
            wifiLock = lock
            Log.i("EZAcrossWifi", "Wi-Fi latency lock acquired for connected session")
        } catch (error: Exception) {
            Log.w("EZAcrossWifi", "Wi-Fi latency lock unavailable", error)
        }
    }

    private fun releaseWifiLatencyLock() {
        wifiLock?.let { if (it.isHeld) it.release() }
        wifiLock = null
    }

    fun disconnect() {
        webSocket?.close(1000, "User disconnected")
        disconnectInternal()
    }

    private fun sendHello() {
        val deviceName = Build.MODEL
        val message = JSONObject().apply {
            put("Type", "HELLO")
            put("ProtocolVersion", 1)
            put("Payload", JSONObject().apply {
                put("DeviceName", deviceName)
                put("ConnectionMode", connectionMode)
            })
        }
        send(message.toString())
    }

    fun sendPing() {
        val message = JSONObject().apply {
            put("Type", "PING")
            put("ProtocolVersion", 1)
            put("Payload", JSONObject().apply {
                put("Timestamp", System.currentTimeMillis())
            })
        }
        send(message.toString())
    }

    fun sendMessage(message: String) {
        send(message)
    }

    private fun send(message: String) {
        onLogMessage?.invoke("Sending: $message")
        webSocket?.send(message)
    }
}
