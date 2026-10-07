package com.example.ezacrosscontrol.control

import android.accessibilityservice.AccessibilityService
import android.accessibilityservice.GestureDescription
import android.graphics.Path
import android.util.Log

class MouseActionExecutor(private val accessibilityService: AccessibilityService) {

    fun executeTap(x: Int, y: Int) {
        Log.d("MouseActionExecutor", "[GESTURE] Dispatching tap X=$x Y=$y")
        try {
            val path = Path().apply {
                moveTo(x.toFloat(), y.toFloat())
            }
            val stroke = GestureDescription.StrokeDescription(path, 0, 50)
            val gesture = GestureDescription.Builder().addStroke(stroke).build()
    
            val result = accessibilityService.dispatchGesture(
                gesture,
                object : AccessibilityService.GestureResultCallback() {
                    override fun onCompleted(gestureDescription: GestureDescription?) {
                        super.onCompleted(gestureDescription)
                        Log.d("MouseActionExecutor", "Tap completed at $x, $y")
                    }
    
                    override fun onCancelled(gestureDescription: GestureDescription?) {
                        super.onCancelled(gestureDescription)
                        Log.w("MouseActionExecutor", "Tap cancelled at $x, $y")
                    }
                },
                null
            )
            if (!result) {
                Log.e("MouseActionExecutor", "Failed to dispatch tap gesture")
            }
        } catch (t: Throwable) {
            Log.e("MouseActionExecutor", "Exception during tap: ${t.message}", t)
        }
    }
    
    fun executeScroll(x: Int, y: Int, deltaY: Int) {
        Log.d("MouseActionExecutor", "[GESTURE] Dispatching scroll X=$x Y=$y deltaY=$deltaY")
        try {
            // Vertical Scroll: A negative deltaY usually means scrolling up (wheel up) 
            // which physically means dragging the screen downwards.
            // A positive deltaY usually means scrolling down (wheel down), dragging screen upwards.
            
            // For standard scroll mapping:
            // Use a distance scaled slightly, but safely clamped
            val sign = if (deltaY > 0) -1f else 1f
            val dragDistance = 300f
            
            var startY = y.toFloat()
            // Ensure startY allows the drag distance safely within a 0-2000px screen
            if (startY < 400f) startY = 400f
            if (startY > 1600f) startY = 1600f
            
            val endY = startY + (sign * dragDistance)
            
            val path = Path().apply {
                moveTo(x.toFloat(), startY)
                lineTo(x.toFloat(), endY)
            }
            
            val stroke = GestureDescription.StrokeDescription(path, 0, 150)
            val gesture = GestureDescription.Builder().addStroke(stroke).build()
            
            val result = accessibilityService.dispatchGesture(
                gesture,
                object : AccessibilityService.GestureResultCallback() {
                    override fun onCompleted(gestureDescription: GestureDescription?) {
                        super.onCompleted(gestureDescription)
                        Log.d("MouseActionExecutor", "Scroll completed")
                    }
    
                    override fun onCancelled(gestureDescription: GestureDescription?) {
                        super.onCancelled(gestureDescription)
                        Log.w("MouseActionExecutor", "Scroll cancelled")
                    }
                },
                null
            )
            if (!result) {
                Log.e("MouseActionExecutor", "Failed to dispatch scroll gesture")
            }
        } catch (t: Throwable) {
            Log.e("MouseActionExecutor", "Exception during scroll: ${t.message}", t)
        }
    }

}
