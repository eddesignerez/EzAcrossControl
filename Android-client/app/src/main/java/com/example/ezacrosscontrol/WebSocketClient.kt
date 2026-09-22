package com.example.ezacrosscontrol

import android.os.Build
import android.util.Log
import okhttp3.*
import org.json.JSONObject
import java.util.concurrent.TimeUnit

class WebSocketClient {
    private var client: OkHttpClient? = null
    private var webSocket: WebSocket? = null
    var onConnectionStateChanged: ((Boolean) -> Unit)? = null
    var onLatencyUpdated: ((Long) -> Unit)? = null
    var onLogMessage: ((String) -> Unit)? = null
    var onEventReceived: ((com.example.ezacrosscontrol.protocol.MessageEnvelope) -> Unit)? = null

    fun connect(ip: String, port: String) {
        if (webSocket != null) return

        val url = "ws://$ip:$port/"
        onLogMessage?.invoke("Connecting to $url...")

        client = OkHttpClient.Builder()
            .readTimeout(0, TimeUnit.MILLISECONDS)
            .build()

        val request = Request.Builder()
            .url(url)
            .build()

        val listener = object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                onLogMessage?.invoke("Connected")
                onConnectionStateChanged?.invoke(true)
                sendHello()
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
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
                onLogMessage?.invoke("Closed")
                disconnectInternal()
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                onLogMessage?.invoke("Error: ${t.message}")
                disconnectInternal()
            }
        }

        webSocket = client?.newWebSocket(request, listener)
    }

    private fun disconnectInternal() {
        webSocket = null
        onConnectionStateChanged?.invoke(false)
        client?.dispatcher?.executorService?.shutdown()
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
