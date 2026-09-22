package com.example.ezacrosscontrol.control

import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.Before

class AndroidControlManagerTest {

    @Before
    fun setup() {
        AndroidControlManager.onAccessibilityServiceDisconnected() // Reset to Disabled
    }

    @Test
    fun testInitialState() {
        assertEquals(ControlState.Disabled, AndroidControlManager.currentState)
    }

    @Test
    fun testConnectionChangesState() {
        AndroidControlManager.onConnectionStateChanged(true)
        // If accessibility service is not connected, it should remain disabled
        assertEquals(ControlState.Disabled, AndroidControlManager.currentState)

        // Mock accessibility service connected (we can't easily mock AccessibilityService in plain JUnit, 
        // but we can call onConnectionStateChanged and observe the logic)
        // We will just test the logic that is accessible.
    }
    
    @Test
    fun testManualStop() {
        AndroidControlManager.stopRemoteControl()
        assertEquals(true, AndroidControlManager.isManuallyStopped)
        
        AndroidControlManager.resetManualStop()
        assertEquals(false, AndroidControlManager.isManuallyStopped)
    }
}
