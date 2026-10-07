package com.example.ezacrosscontrol.control

import android.content.Context
import android.graphics.PixelFormat
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import android.util.Log
import android.view.Gravity
import android.view.InputDevice
import android.view.MotionEvent
import android.view.View
import android.view.WindowManager

enum class ScreenEdge {
    Left, Right, Top, Bottom, None
}

class ReturnEdgeSensor(private val context: Context) {
    private var windowManager: WindowManager? = null
    private var edgeView: View? = null
    private var isShowing = false
    private val mainHandler = Handler(Looper.getMainLooper())

    private var currentEdge = ScreenEdge.None
    private val returnEdgeWidthPx = 3

    var onReturnDetected: (() -> Unit)? = null

    private val dwell = ReturnEdgeDwell()
    private val returnDwellMs = 35L
    private val returnCooldownMs = 500L
    private var lastReturnTime = 0L
    private val returnCheck = Runnable {
        if (isShowing && dwell.shouldReturn(SystemClock.uptimeMillis())) triggerReturn()
    }

    fun getReturnEdge(androidPosition: String): ScreenEdge {
        return when (androidPosition) {
            "Right" -> ScreenEdge.Left
            "Left" -> ScreenEdge.Right
            "Top" -> ScreenEdge.Bottom
            "Bottom" -> ScreenEdge.Top
            else -> ScreenEdge.None
        }
    }

    fun arm(androidPosition: String) {
        val edge = getReturnEdge(androidPosition)
        if (edge == ScreenEdge.None) {
            hide()
            return
        }
        currentEdge = edge
        dwell.arm(SystemClock.uptimeMillis())
        mainHandler.removeCallbacks(returnCheck)
        show()
    }

    fun disarm() {
        hide()
        currentEdge = ScreenEdge.None
    }

    private fun show() {
        mainHandler.post {
            if (isShowing) {
                updateLayoutParams()
                return@post
            }
            if (windowManager == null) {
                windowManager = context.getSystemService(Context.WINDOW_SERVICE) as WindowManager
            }

            edgeView = View(context).apply {
                setBackgroundColor(0x00000000) // Transparent
                setOnHoverListener { _, event ->
                    handleHoverEvent(event)
                }
                setOnTouchListener { _, event ->
                    if (event.isFromSource(InputDevice.SOURCE_MOUSE)) {
                        dwell.updateButtons(event.buttonState != 0 || event.actionMasked == MotionEvent.ACTION_DOWN)
                        dwell.cancel()
                        mainHandler.removeCallbacks(returnCheck)
                    }
                    false
                }
            }

            val params = getLayoutParamsForEdge(currentEdge)
            try {
                windowManager?.addView(edgeView, params)
                isShowing = true
                Log.d("ReturnEdgeSensor", "Armed ReturnEdge on $currentEdge")
            } catch (e: Exception) {
                Log.e("ReturnEdgeSensor", "Failed to add edge view", e)
            }
        }
    }

    private fun hide() {
        mainHandler.post {
            if (!isShowing) return@post
            try {
                windowManager?.removeView(edgeView)
            } catch (e: Exception) {
                Log.e("ReturnEdgeSensor", "Failed to remove edge view", e)
            }
            edgeView = null
            isShowing = false
            dwell.cancel()
            mainHandler.removeCallbacks(returnCheck)
            Log.d("ReturnEdgeSensor", "Disarmed ReturnEdge")
        }
    }

    private fun updateLayoutParams() {
        if (!isShowing || edgeView == null) return
        val params = getLayoutParamsForEdge(currentEdge)
        try {
            windowManager?.updateViewLayout(edgeView, params)
        } catch (e: Exception) {
            Log.e("ReturnEdgeSensor", "Failed to update edge view", e)
        }
    }

    private fun getLayoutParamsForEdge(edge: ScreenEdge): WindowManager.LayoutParams {
        val params = WindowManager.LayoutParams(
            WindowManager.LayoutParams.TYPE_ACCESSIBILITY_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                    WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN or
                    WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS or
                    WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL,
            PixelFormat.TRANSLUCENT
        )

        when (edge) {
            ScreenEdge.Left -> {
                params.width = returnEdgeWidthPx
                params.height = WindowManager.LayoutParams.MATCH_PARENT
                params.gravity = Gravity.START or Gravity.TOP
            }
            ScreenEdge.Right -> {
                params.width = returnEdgeWidthPx
                params.height = WindowManager.LayoutParams.MATCH_PARENT
                params.gravity = Gravity.END or Gravity.TOP
            }
            ScreenEdge.Top -> {
                params.width = WindowManager.LayoutParams.MATCH_PARENT
                params.height = returnEdgeWidthPx
                params.gravity = Gravity.START or Gravity.TOP
            }
            ScreenEdge.Bottom -> {
                params.width = WindowManager.LayoutParams.MATCH_PARENT
                params.height = returnEdgeWidthPx
                params.gravity = Gravity.START or Gravity.BOTTOM
            }
            ScreenEdge.None -> {
                params.width = 0
                params.height = 0
            }
        }
        return params
    }

    private fun handleHoverEvent(event: MotionEvent): Boolean {
        if (!event.isFromSource(InputDevice.SOURCE_MOUSE)) {
            return false // Ignore touch
        }
        dwell.updateButtons(event.buttonState != 0)
        if (event.buttonState != 0) {
            mainHandler.removeCallbacks(returnCheck)
            return false
        }

        when (event.actionMasked) {
            MotionEvent.ACTION_HOVER_ENTER -> {
                // Do not bounce straight back if the native cursor was already
                // on this edge when the handoff armed the overlay.
                mainHandler.removeCallbacks(returnCheck)
                if (dwell.enter(SystemClock.uptimeMillis(), false))
                    mainHandler.postDelayed(returnCheck, returnDwellMs)
            }
            MotionEvent.ACTION_HOVER_MOVE -> {
                if (dwell.shouldReturn(SystemClock.uptimeMillis())) triggerReturn()
            }
            MotionEvent.ACTION_HOVER_EXIT -> {
                dwell.cancel()
                mainHandler.removeCallbacks(returnCheck)
            }
        }
        return false
    }

    private fun triggerReturn() {
        val now = SystemClock.uptimeMillis()
        if (now - lastReturnTime < returnCooldownMs) {
            return // Debounce
        }
        lastReturnTime = now
        dwell.cancel()
        
        Log.i("ReturnEdgeSensor", "RETURN_TO_WINDOWS triggered from edge $currentEdge")
        onReturnDetected?.invoke()
    }
}
