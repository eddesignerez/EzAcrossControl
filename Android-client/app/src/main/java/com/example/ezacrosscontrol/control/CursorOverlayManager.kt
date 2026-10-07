package com.example.ezacrosscontrol.control

import android.content.Context
import android.graphics.PixelFormat
import android.view.Gravity
import android.view.LayoutInflater
import android.view.View
import android.view.WindowManager
import android.widget.FrameLayout
import android.widget.ImageView
import android.os.Handler
import android.os.Looper
import com.example.ezacrosscontrol.R

class CursorOverlayManager(private val context: Context) {
    private var windowManager: WindowManager? = null
    private var overlayContainer: FrameLayout? = null
    private var cursorView: ImageView? = null
    private var layoutParams: WindowManager.LayoutParams? = null
    private var isShowing = false
    private val mainHandler = Handler(Looper.getMainLooper())

    var pointerHotspotX: Float = 12f
    var pointerHotspotY: Float = 12f

    fun init() {
        if (windowManager != null) return
        windowManager = context.getSystemService(Context.WINDOW_SERVICE) as WindowManager

        cursorView = ImageView(context).apply {
            setImageResource(R.drawable.ic_cursor)
            layoutParams = FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.WRAP_CONTENT,
                FrameLayout.LayoutParams.WRAP_CONTENT
            )
        }

        overlayContainer = FrameLayout(context).apply {
            // Ensure the overlay doesn't intercept touches
            isClickable = false
            isFocusable = false
            addView(cursorView)
        }

        layoutParams = WindowManager.LayoutParams(
            WindowManager.LayoutParams.MATCH_PARENT,
            WindowManager.LayoutParams.MATCH_PARENT,
            WindowManager.LayoutParams.TYPE_ACCESSIBILITY_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                    WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE or
                    WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN or
                    WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS,
            PixelFormat.TRANSLUCENT
        ).apply {
            gravity = Gravity.TOP or Gravity.START
            x = 0
            y = 0
        }
    }

    fun show() {
        mainHandler.post {
            if (isShowing) return@post
            try {
                init()
                windowManager?.addView(overlayContainer, layoutParams)
                isShowing = true
                android.util.Log.i("EZAcrossOverlay", "[OVERLAY] Created width=${layoutParams?.width} height=${layoutParams?.height} x=${layoutParams?.x} y=${layoutParams?.y}")
            } catch (t: Throwable) {
                android.util.Log.e("EZAcrossOverlay", "[OVERLAY] Exception in show(): ${t.message}", t)
                t.printStackTrace()
            }
        }
    }

    fun hide() {
        mainHandler.post {
            if (!isShowing) return@post
            try {
                windowManager?.removeView(overlayContainer)
                isShowing = false
            } catch (t: Throwable) {
                android.util.Log.e("EZAcrossOverlay", "[OVERLAY] Exception in hide(): ${t.message}", t)
                t.printStackTrace()
            }
        }
    }

    fun updatePosition(x: Int, y: Int) {
        mainHandler.post {
            if (!isShowing) return@post
            try {
                cursorView?.translationX = x.toFloat() - pointerHotspotX
                cursorView?.translationY = y.toFloat() - pointerHotspotY
            } catch (t: Throwable) {
                android.util.Log.e("EZAcrossOverlay", "[OVERLAY] Exception in updatePosition: ${t.message}", t)
            }
        }
    }
    
    fun destroy() {
        hide()
        windowManager = null
        overlayContainer = null
        cursorView = null
        layoutParams = null
    }
}
