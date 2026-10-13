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

    @Test fun controlReadyRequiresEveryLayerToBeReady() {
        fun ready(
            appConnected: Boolean = true,
            transport: String? = "USB",
            engineState: String? = "Ready",
            hostControlEnabled: Boolean = true,
            accessibilityEnabled: Boolean = true,
            manuallyStopped: Boolean = false,
            readiness: ConnectionReadiness = both,
        ) = isControlReady(appConnected, transport, engineState, hostControlEnabled,
            accessibilityEnabled, manuallyStopped, readiness)

        assertTrue(ready())
        assertTrue(ready(engineState = "Captured"))
        assertFalse(ready(appConnected = false))
        assertFalse(ready(transport = null))
        assertFalse(ready(engineState = "Starting"))
        assertFalse(ready(engineState = "Error"))
        assertFalse(ready(hostControlEnabled = false))
        assertFalse(ready(accessibilityEnabled = false))
        assertFalse(ready(manuallyStopped = true))
        assertTrue(ready(readiness = both.copy(hostAvailable = false, usbConnected = false)))
        assertFalse(ready(engineState = "Offline", readiness = both.copy(usbConnected = false)))
    }
}
