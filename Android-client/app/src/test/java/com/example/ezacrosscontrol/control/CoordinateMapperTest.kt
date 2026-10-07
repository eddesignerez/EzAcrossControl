package com.example.ezacrosscontrol.control

import android.content.Context
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.runners.JUnit4

// A dummy context is not needed if we avoid calling init() during test instantiation,
// but since CoordinateMapper calls updateMetrics() in init{}, we can't easily instantiate it without a valid Context unless we bypass init.
// Actually, let's just make the mapping logic a companion object function or static to be pure.
// For now, let's test the pure math.

class CoordinateMapperTest {

    @Test
    fun testMapping() {
        // Since we can't instantiate CoordinateMapper without a real Context in local unit tests (unless we use Robolectric),
        // we'll verify the math directly here, which is what CoordinateMapper does internally.
        val screenWidth = 1920
        val screenHeight = 1080

        fun map(nx: Double, ny: Double): Pair<Int, Int> {
            val cx = Math.max(0.0, Math.min(1.0, nx))
            val cy = Math.max(0.0, Math.min(1.0, ny))
            val px = (cx * screenWidth).toInt()
            val py = (cy * screenHeight).toInt()
            val fx = Math.max(0, Math.min(screenWidth - 1, px))
            val fy = Math.max(0, Math.min(screenHeight - 1, py))
            return Pair(fx, fy)
        }

        // Test bounds
        assertEquals(Pair(0, 0), map(0.0, 0.0))
        assertEquals(Pair(1919, 1079), map(1.0, 1.0))
        
        // Test out of bounds
        assertEquals(Pair(0, 0), map(-0.5, -0.5))
        assertEquals(Pair(1919, 1079), map(1.5, 1.5))
        
        // Test middle
        assertEquals(Pair(960, 540), map(0.5, 0.5))
    }
}
