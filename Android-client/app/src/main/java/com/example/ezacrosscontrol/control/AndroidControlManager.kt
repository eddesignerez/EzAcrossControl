package com.example.ezacrosscontrol.control

import android.accessibilityservice.AccessibilityService
import android.os.Handler
import android.os.Looper
import android.util.Log
import android.view.Choreographer
import java.util.concurrent.atomic.AtomicInteger

enum class ControlState {
    Disabled,
    Ready,
    Controlling,
    Disconnected
}

object AndroidControlManager {
    private var accessibilityService: AccessibilityService? = null
    private var returnEdgeSensor: ReturnEdgeSensor? = null
    private var coordinateMapper: CoordinateMapper? = null
    private var actionExecutor: MouseActionExecutor? = null

    var currentState: ControlState = ControlState.Disabled
        private set

    val isAccessibilityEnabled: Boolean
        get() = accessibilityService != null

    // State listener for UI
    var onStateChanged: ((ControlState) -> Unit)? = null

    // Track if user pressed "Stop Remote Control"
    var isManuallyStopped: Boolean = false
        private set

    var onRequestHandoffEnd: (() -> Unit)? = null

    private var pointerX: Float = 0f
    private var pointerY: Float = 0f
    private val pointerSensitivity = 1.5f

    private var currentSessionId: Int = -1
    private val remoteEntryInsetPx = 12f
    private val returnArmDistancePx = 40f
    private val returnPushThreshold = 16f

    private var returnDetectionArmed = false
    private var accumulatedReturnDelta = 0f
    private var entryEdge = ""

    var disableAutomaticReturn = false

    private var rxMouseEventsCount = 0L
    private var lastRxLogTimeMs = 0L
    private var lastRxTimeMs = 0L

    private val pendingDeltaX = AtomicInteger(0)
    private val pendingDeltaY = AtomicInteger(0)

    private var activeFrameLoops = 0
    private var previousFrameTimeNanos = 0L

    private val mainHandler = Handler(Looper.getMainLooper())
    private var isFrameScheduled = false
    private val frameCallback = object : Choreographer.FrameCallback {
        override fun doFrame(frameTimeNanos: Long) {
            if (currentState == ControlState.Controlling && !isManuallyStopped) {
                if (previousFrameTimeNanos != 0L) {
                    val intervalMs = (frameTimeNanos - previousFrameTimeNanos) / 1_000_000
                    if (intervalMs > 20) {
                        Log.d("AndroidControlManager", "[FRAME-JANK] $intervalMs ms")
                    }
                }
                previousFrameTimeNanos = frameTimeNanos

                val dx = pendingDeltaX.getAndSet(0)
                val dy = pendingDeltaY.getAndSet(0)

                if (dx != 0 || dy != 0) {
                    processCursorMovement(dx, dy)
                }

                Choreographer.getInstance().postFrameCallback(this)
            } else {
                activeFrameLoops--
                isFrameScheduled = false
            }
        }
    }

    // Mouse button state tracking to resolve Left Down + Up into Tap
    private var leftButtonDown = false

    fun onAccessibilityServiceConnected(service: AccessibilityService) {
        Log.i("EZAcrossAccessibility", "[ACCESSIBILITY] Service available = true")
        accessibilityService = service
        coordinateMapper = CoordinateMapper(service)
        returnEdgeSensor = ReturnEdgeSensor(service)
        actionExecutor = MouseActionExecutor(service)

        if (currentState == ControlState.Disabled) {
            setState(ControlState.Ready)
        }
    }

    fun onAccessibilityServiceDisconnected() {
        returnEdgeSensor?.disarm()
        accessibilityService = null
        coordinateMapper = null
        returnEdgeSensor = null
        actionExecutor = null

        setState(ControlState.Disabled)
    }

    fun stopRemoteControl() {
        isManuallyStopped = true
        returnEdgeSensor?.disarm()
        setState(ControlState.Ready) // Or stay in Ready/Stopped
        onRequestHandoffEnd?.invoke()
    }

    fun resetManualStop() {
        isManuallyStopped = false
    }

    fun onConnectionStateChanged(isConnected: Boolean) {
        if (!isConnected) {
            returnEdgeSensor?.disarm()
            if (currentState != ControlState.Disabled) {
                setState(ControlState.Disconnected)
            }
        } else {
            resetManualStop()
            if (accessibilityService != null) {
                setState(ControlState.Ready)
            }
        }
    }

    fun handleHandoffBegin(edge: String, sessionId: Int, clientTxTimestamp: Long) {
        if (isManuallyStopped) return
        if (accessibilityService != null) {
            coordinateMapper?.updateMetrics()

            currentSessionId = sessionId
            returnDetectionArmed = false
            accumulatedReturnDelta = 0f
            entryEdge = edge

            // Initialize remote cursor position based on entry edge
            val width = coordinateMapper?.getScreenWidth() ?: 1080
            val height = coordinateMapper?.getScreenHeight() ?: 1920
            when (edge) {
                "Right" -> {
                    pointerX = remoteEntryInsetPx
                    pointerY = height / 2f
                }
                "Left" -> {
                    pointerX = width.toFloat() - remoteEntryInsetPx
                    pointerY = height / 2f
                }
                "Top" -> {
                    pointerX = width / 2f
                    pointerY = height.toFloat() - remoteEntryInsetPx
                }
                "Bottom" -> {
                    pointerX = width / 2f
                    pointerY = remoteEntryInsetPx
                }
                else -> {
                    pointerX = width / 2f
                    pointerY = height / 2f
                }
            }

            returnEdgeSensor?.onReturnDetected = {
                handleReturnToWindows()
            }
            returnEdgeSensor?.arm(edge)

            Log.d("AndroidControlManager", "[LATENCY] HandoffBegin received. Windows Tx: $clientTxTimestamp. Armed return sensor.")
            setState(ControlState.Controlling)

            mainHandler.post {
                if (!isFrameScheduled) {
                    isFrameScheduled = true
                    activeFrameLoops++
                    if (activeFrameLoops > 1) {
                        Log.w("AndroidControlManager", "[WARN] activeFrameLoops is $activeFrameLoops! Possible duplicate callback.")
                    }
                    previousFrameTimeNanos = 0L
                    Choreographer.getInstance().postFrameCallback(frameCallback)
                }
            }
        }
    }

    var onRequestReturnToWindows: ((Int, String) -> Unit)? = null

    private fun handleReturnToWindows() {
        val edgeStr = entryEdge
        val sessionId = currentSessionId

        // Keep the sensor/session active until Windows confirms INPUT_HANDOFF_END.
        // Windows may reject an edge request while a native mouse button is held.
        onRequestReturnToWindows?.invoke(sessionId, edgeStr)
    }

    fun handleHandoffEnd(sessionId: Int) {
        if (sessionId == -1 || sessionId == currentSessionId) {
            currentSessionId = -1
            returnEdgeSensor?.disarm()
            leftButtonDown = false
            if (accessibilityService != null) {
                setState(ControlState.Ready)
            }
            mainHandler.post {
                if (isFrameScheduled) {
                    Choreographer.getInstance().removeFrameCallback(frameCallback)
                    isFrameScheduled = false
                    activeFrameLoops--
                }
            }
        }
    }

    fun handleMouseMove(normalizedX: Double, normalizedY: Double, deltaX: Int, deltaY: Int) {
        if (currentState != ControlState.Controlling || isManuallyStopped) return

        rxMouseEventsCount++
        val now = System.currentTimeMillis()
        if (now - lastRxLogTimeMs >= 1000) {
            Log.d("AndroidControlManager", "[METRICS] rxMouseEventsPerSecond: $rxMouseEventsCount activeFrameLoops: $activeFrameLoops")
            rxMouseEventsCount = 0
            lastRxLogTimeMs = now
        }

        if (lastRxTimeMs != 0L) {
            val rxInterval = now - lastRxTimeMs
            if (rxInterval > 30) {
                Log.d("AndroidControlManager", "[RX-GAP] $rxInterval ms")
            }
        }
        lastRxTimeMs = now

        pendingDeltaX.addAndGet(deltaX)
        pendingDeltaY.addAndGet(deltaY)
    }

    private fun processCursorMovement(deltaX: Int, deltaY: Int) {
        val width = coordinateMapper?.getScreenWidth() ?: return
        val height = coordinateMapper?.getScreenHeight() ?: return

        pointerX += deltaX * pointerSensitivity
        pointerY += deltaY * pointerSensitivity

        if (!returnDetectionArmed) {
            when (entryEdge) {
                "Right" -> if (pointerX > returnArmDistancePx) returnDetectionArmed = true
                "Left" -> if (width - pointerX > returnArmDistancePx) returnDetectionArmed = true
                "Top" -> if (height - pointerY > returnArmDistancePx) returnDetectionArmed = true
                "Bottom" -> if (pointerY > returnArmDistancePx) returnDetectionArmed = true
            }
        }

        var isReturning = false
        var outDeltaX = 0f
        var outDeltaY = 0f

        // Clamp and calculate outward push
        if (pointerX < 0) {
            outDeltaX = -(deltaX * pointerSensitivity)
            pointerX = 0f
        } else if (pointerX >= width) {
            outDeltaX = deltaX * pointerSensitivity
            pointerX = width.toFloat() - 1f
        }

        if (pointerY < 0) {
            outDeltaY = -(deltaY * pointerSensitivity)
            pointerY = 0f
        } else if (pointerY >= height) {
            outDeltaY = deltaY * pointerSensitivity
            pointerY = height.toFloat() - 1f
        }

        // Check for return against the entered edge
        if (returnDetectionArmed) {
            var edgePush = 0f
            when (entryEdge) {
                "Right" -> if (outDeltaX < 0) edgePush = -outDeltaX // Pushing against Left edge
                "Left" -> if (outDeltaX > 0) edgePush = outDeltaX // Pushing against Right edge
                "Top" -> if (outDeltaY > 0) edgePush = outDeltaY // Pushing against Bottom edge
                "Bottom" -> if (outDeltaY < 0) edgePush = -outDeltaY // Pushing against Top edge
            }
            if (edgePush > 0) {
                accumulatedReturnDelta += edgePush
            } else {
                accumulatedReturnDelta = 0f
            }
        }

        if (returnDetectionArmed && accumulatedReturnDelta >= returnPushThreshold) {
            isReturning = true
        }

        if (isReturning && !disableAutomaticReturn) {
            handleHandoffEnd(currentSessionId)
            onRequestHandoffEnd?.invoke()
            return
        }
    }

    fun handleMouseButton(button: String, action: String) {
        if (currentState != ControlState.Controlling || isManuallyStopped) return

        if (button == "Left") {
            if (action == "Down") {
                leftButtonDown = true
            } else if (action == "Up" && leftButtonDown) {
                leftButtonDown = false
                // Execute Tap
                val mapper = coordinateMapper ?: return
                // Retrieve current cursor position implicitly by maintaining state,
                // or just relying on the last known MouseMove.
                // For a robust implementation, we should store currentPixelX/Y in ControlManager.
            }
        } else if (button == "Right" || button == "Middle") {
            Log.d("AndroidControlManager", "Received unsupported button: $button $action (Reserved)")
        }
    }

    private var currentPixelX: Int = 0
    private var currentPixelY: Int = 0
    private var lastLogTimeMs = 0L



    fun handleMouseMoveWithState(normalizedX: Double, normalizedY: Double, deltaX: Int, deltaY: Int) {
        if (currentState != ControlState.Controlling || isManuallyStopped) return

        handleMouseMove(normalizedX, normalizedY, deltaX, deltaY)

        currentPixelX = pointerX.toInt()
        currentPixelY = pointerY.toInt()

        val now = System.currentTimeMillis()
        if (now - lastLogTimeMs > 1000) {
            Log.d("AndroidControlManager", "[INPUT] MouseMove dx=$deltaX dy=$deltaY rx=$pointerX ry=$pointerY")
            Log.d("AndroidControlManager", "[CONTROL] State = ${currentState.name}")
            lastLogTimeMs = now
        }
    }

    fun handleMouseButtonWithState(button: String, action: String) {
        if (currentState != ControlState.Controlling || isManuallyStopped) return
        Log.d("AndroidControlManager", "[INPUT] ${button}Button $action")
        if (button == "Left") {
            if (action == "Down") {
                leftButtonDown = true
            } else if (action == "Up" && leftButtonDown) {
                leftButtonDown = false
                actionExecutor?.executeTap(currentPixelX, currentPixelY)
            }
        } else {
            Log.d("AndroidControlManager", "Reserved button action: $button $action")
        }
    }

    fun handleMouseWheel(deltaX: Int, deltaY: Int) {
        if (currentState != ControlState.Controlling || isManuallyStopped) return
        Log.d("AndroidControlManager", "[INPUT] Wheel deltaY=$deltaY")
        if (deltaY != 0) {
            actionExecutor?.executeScroll(currentPixelX, currentPixelY, deltaY)
        }
    }

    fun handleTextCommit(text: String) {
        if (currentState != ControlState.Controlling || isManuallyStopped) return

        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU) {
            val ic = accessibilityService?.inputMethod?.currentInputConnection
            if (ic != null) {
                ic.commitText(text, 1, null)
                return
            }
        }

        if (com.example.ezacrosscontrol.control.keyboard.EZAcrossInputMethodService.isReady()) {
            com.example.ezacrosscontrol.control.keyboard.EZAcrossInputMethodService.commitText(text)
        }
    }

    private var ctrlPressed = false
    private var shiftPressed = false
    private var altPressed = false
    private var winPressed = false

    // Update modifiers when a key down or up comes in, but currently the protocol doesn't send
    // modifier states directly unless we track them, or wait, the protocol sends INPUT_KEY_DOWN.
    // Wait, the new payload has Modifiers, but in handleKeyDown we only get vkCode currently?
    // Let's modify InputSessionManager.kt to send modifiers as well if needed.
    // Actually, we can just intercept the vkCode for Ctrl (0x11).

    fun handleKeyDown(vkCode: Int) {
        if (currentState != ControlState.Controlling || isManuallyStopped) return

        if (vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3) { // VK_CONTROL, LCONTROL, RCONTROL
            ctrlPressed = true
            return
        }

        // Handle Shortcuts Ctrl+A, C, X, V
        if (ctrlPressed) {
            var actionId = -1
            when (vkCode) {
                0x41 -> actionId = android.R.id.selectAll // Ctrl+A
                0x43 -> actionId = android.R.id.copy      // Ctrl+C
                0x56 -> actionId = android.R.id.paste     // Ctrl+V
                0x58 -> actionId = android.R.id.cut       // Ctrl+X
            }
            if (actionId != -1) {
                if (actionId == android.R.id.copy || actionId == android.R.id.paste) {
                    Log.d("AndroidControlManager", "[KEYBOARD] ${if (actionId == android.R.id.copy) "COPY" else "PASTE"} shortcut triggered.")
                }

                if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU) {
                    val ic = accessibilityService?.inputMethod?.currentInputConnection
                    if (ic != null) {
                        ic.performContextMenuAction(actionId)
                        return
                    }
                }

                com.example.ezacrosscontrol.control.keyboard.EZAcrossInputMethodService.performContextMenuAction(actionId)
                return
            }
        }

        // Special handling for Backspace directly (Delete Surrounding Text) to avoid IME jumping
        if (vkCode == 0x08) {
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU) {
                val ic = accessibilityService?.inputMethod?.currentInputConnection
                if (ic != null) {
                    ic.deleteSurroundingText(1, 0)
                    return
                }
            }
            com.example.ezacrosscontrol.control.keyboard.EZAcrossInputMethodService.deleteSurroundingText(1, 0)
            return
        }

        val androidKeyCode = mapVirtualKeyToAndroid(vkCode)
        if (androidKeyCode != -1) {
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU) {
                val ic = accessibilityService?.inputMethod?.currentInputConnection
                if (ic != null) {
                    ic.sendKeyEvent(android.view.KeyEvent(android.view.KeyEvent.ACTION_DOWN, androidKeyCode))
                    ic.sendKeyEvent(android.view.KeyEvent(android.view.KeyEvent.ACTION_UP, androidKeyCode))
                    return
                }
            }

            com.example.ezacrosscontrol.control.keyboard.EZAcrossInputMethodService.sendSpecialKey(androidKeyCode)
        }
    }

    fun handleKeyUp(vkCode: Int) {
        if (vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3) { // VK_CONTROL, LCONTROL, RCONTROL
            ctrlPressed = false
        }
    }

    private fun mapVirtualKeyToAndroid(vkCode: Int): Int {
        return when (vkCode) {
            0x09 -> android.view.KeyEvent.KEYCODE_TAB // Tab
            0x0D -> android.view.KeyEvent.KEYCODE_ENTER // Enter
            0x1B -> android.view.KeyEvent.KEYCODE_ESCAPE // Esc
            0x21 -> android.view.KeyEvent.KEYCODE_PAGE_UP
            0x22 -> android.view.KeyEvent.KEYCODE_PAGE_DOWN
            0x23 -> android.view.KeyEvent.KEYCODE_MOVE_END
            0x24 -> android.view.KeyEvent.KEYCODE_MOVE_HOME
            0x25 -> android.view.KeyEvent.KEYCODE_DPAD_LEFT
            0x26 -> android.view.KeyEvent.KEYCODE_DPAD_UP
            0x27 -> android.view.KeyEvent.KEYCODE_DPAD_RIGHT
            0x28 -> android.view.KeyEvent.KEYCODE_DPAD_DOWN
            0x2E -> android.view.KeyEvent.KEYCODE_FORWARD_DEL // Delete
            0x15 -> android.view.KeyEvent.KEYCODE_KATAKANA_HIRAGANA // VK_KANA
            0xF2 -> android.view.KeyEvent.KEYCODE_KATAKANA_HIRAGANA // VK_OEM_COPY
            0x19 -> android.view.KeyEvent.KEYCODE_ZENKAKU_HANKAKU // VK_KANJI
            0x1C -> android.view.KeyEvent.KEYCODE_HENKAN // VK_CONVERT
            0x1D -> android.view.KeyEvent.KEYCODE_MUHENKAN // VK_NONCONVERT
            else -> -1
        }
    }

    private fun setState(newState: ControlState) {
        currentState = newState
        onStateChanged?.invoke(newState)
    }
}
