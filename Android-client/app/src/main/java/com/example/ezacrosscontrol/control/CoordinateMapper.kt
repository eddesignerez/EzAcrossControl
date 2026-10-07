package com.example.ezacrosscontrol.control

import android.content.Context
import android.util.DisplayMetrics
import android.view.WindowManager
import kotlin.math.max
import kotlin.math.min

class CoordinateMapper(private val context: Context) {
    private var screenWidth: Int = 0
    private var screenHeight: Int = 0

    init {
        updateMetrics()
    }

    fun updateMetrics() {
        try {
            val windowManager = context.getSystemService(Context.WINDOW_SERVICE) as WindowManager
            val metrics = DisplayMetrics()
            // Get full display metrics, including status bar and navigation bar areas if any
            windowManager.defaultDisplay.getRealMetrics(metrics)
            screenWidth = metrics.widthPixels
            screenHeight = metrics.heightPixels
        } catch (t: Throwable) {
            android.util.Log.e("EZAcrossControl", "[METRICS] Exception in updateMetrics: ${t.message}", t)
            screenWidth = 1080
            screenHeight = 1920
        }
    }

    fun setScreenMetricsForTest(width: Int, height: Int) {
        screenWidth = width
        screenHeight = height
    }

    fun mapNormalizedToPixel(normalizedX: Double, normalizedY: Double): Pair<Int, Int> {
        val clampedX = max(0.0, min(1.0, normalizedX))
        val clampedY = max(0.0, min(1.0, normalizedY))

        val pixelX = (clampedX * screenWidth).toInt()
        val pixelY = (clampedY * screenHeight).toInt()

        // Ensure we don't go strictly out of bounds (0 to width-1)
        val finalX = max(0, min(screenWidth - 1, pixelX))
        val finalY = max(0, min(screenHeight - 1, pixelY))

        return Pair(finalX, finalY)
    }

    fun getScreenWidth(): Int = screenWidth
    fun getScreenHeight(): Int = screenHeight
}
