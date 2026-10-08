package com.example.ezacrosscontrol

import org.junit.Assert.*
import org.junit.Test

class ConnectionReadinessTest {
    private val both = ConnectionReadiness(true, true, true, true, true, true)
    @Test fun autoPrefersWifiAndFallsBackOnlyToConnectedUsb() {
        assertEquals("Wi-Fi", both.transport("Auto"))
        assertEquals("USB", both.copy(wifiConnected = false).transport("Auto"))
        assertNull(both.copy(wifiConnected = false, usbConnected = false).transport("Auto"))
    }
    @Test fun enabledDebuggingAloneDoesNotClaimReady() {
        assertNull(both.copy(hostAvailable = false).transport("Auto"))
        assertNull(both.copy(wifiConnected = false).transport("Wi-Fi"))
        assertNull(both.copy(usbConnected = false).transport("USB"))
    }
    @Test fun explicitModesNeverFallBackToOtherTransport() {
        assertNull(both.copy(wifiEnabled = false).transport("Wi-Fi"))
        assertNull(both.copy(usbEnabled = false).transport("USB"))
    }
    @Test fun disabledDeveloperModeBlocksAllReadiness() {
        val disabled = both.copy(developerEnabled = false)
        assertNull(disabled.transport("Auto"))
        assertEquals("Ative as Opções do Desenvolvedor", disabled.message("USB"))
    }
    @Test fun disabledDebuggingGivesActionableInstructions() {
        assertEquals("Ative a Depuração USB", both.copy(usbEnabled = false).message("USB"))
        assertEquals("Ative a Depuração Wi-Fi", both.copy(wifiEnabled = false).message("Wi-Fi"))
    }
}
