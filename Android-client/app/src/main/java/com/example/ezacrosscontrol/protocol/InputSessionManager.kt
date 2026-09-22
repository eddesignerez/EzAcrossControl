package com.example.ezacrosscontrol.protocol

class InputSessionManager {
    var lastSequence: Long = 0
        private set
    var lastEvent: String = "None"
        private set
    var currentHz: Int = 0
        private set
    
    private var eventCount: Int = 0
    private var lastTimeMs: Long = 0

    var onMouseMove: ((Double, Double, Int, Int) -> Unit)? = null
    var onMouseButton: ((String, String) -> Unit)? = null
    var onMouseWheel: ((Int, Int) -> Unit)? = null
    var onHandoffBegin: ((String, Int, Long) -> Unit)? = null
    var onHandoffEnd: ((Int) -> Unit)? = null
    var onHandoffCancel: (() -> Unit)? = null
    var onTextCommit: ((String) -> Unit)? = null
    var onKeyDown: ((Int) -> Unit)? = null
    var onKeyUp: ((Int) -> Unit)? = null

    fun onEventReceived(envelope: MessageEnvelope) {
        lastSequence = envelope.sequence
        lastEvent = envelope.type
        eventCount++
        
        val now = System.currentTimeMillis()
        if (now - lastTimeMs >= 1000) {
            currentHz = eventCount
            eventCount = 0
            lastTimeMs = now
        }

        try {
            when (envelope.type) {
                "INPUT_MOUSE_MOVE" -> {
                    val map = envelope.payload
                    val nx = map.optDouble("normalizedX", map.optDouble("NormalizedX", 0.0))
                    val ny = map.optDouble("normalizedY", map.optDouble("NormalizedY", 0.0))
                    val dx = map.optInt("deltaX", map.optInt("DeltaX", 0))
                    val dy = map.optInt("deltaY", map.optInt("DeltaY", 0))
                    onMouseMove?.invoke(nx, ny, dx, dy)
                }
                "INPUT_MOUSE_BUTTON" -> {
                    val map = envelope.payload
                    val button = map.optString("button", map.optString("Button", ""))
                    val action = map.optString("action", map.optString("Action", ""))
                    onMouseButton?.invoke(button, action)
                }
                "INPUT_MOUSE_WHEEL" -> {
                    val map = envelope.payload
                    val deltaX = map.optInt("deltaX", map.optInt("DeltaX", 0))
                    val deltaY = map.optInt("deltaY", map.optInt("DeltaY", 0))
                    onMouseWheel?.invoke(deltaX, deltaY)
                }
                "INPUT_HANDOFF_BEGIN" -> {
                    val map = envelope.payload
                    val edge = map.optString("edge", map.optString("Edge", ""))
                    val sessionId = map.optInt("sessionId", map.optInt("SessionId", -1))
                    val clientTxTimestamp = map.optLong("clientTxTimestamp", map.optLong("ClientTxTimestamp", 0L))
                    onHandoffBegin?.invoke(edge, sessionId, clientTxTimestamp)
                }
                "INPUT_HANDOFF_END" -> {
                    val map = envelope.payload
                    val sessionId = map.optInt("sessionId", map.optInt("SessionId", -1))
                    onHandoffEnd?.invoke(sessionId)
                }
                "INPUT_HANDOFF_CANCEL" -> {
                    onHandoffCancel?.invoke()
                }
                "INPUT_TEXT_COMMIT" -> {
                    val map = envelope.payload
                    val text = map.optString("text", map.optString("Text", ""))
                    if (text.isNotEmpty()) {
                        onTextCommit?.invoke(text)
                    }
                }
                "INPUT_KEY_DOWN" -> {
                    val map = envelope.payload
                    val vkCode = map.optInt("virtualKeyCode", map.optInt("VirtualKeyCode", 0))
                    onKeyDown?.invoke(vkCode)
                }
                "INPUT_KEY_UP" -> {
                    val map = envelope.payload
                    val vkCode = map.optInt("virtualKeyCode", map.optInt("VirtualKeyCode", 0))
                    onKeyUp?.invoke(vkCode)
                }
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    fun reset() {
        lastSequence = 0
        lastEvent = "None"
        currentHz = 0
        eventCount = 0
    }
}
