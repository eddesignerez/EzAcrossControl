package com.example.ezacrosscontrol.control

import org.junit.Assert.*
import org.junit.Test

class ReturnEdgeDwellTest {
    @Test fun uninterruptedHoverReturnsAfterDwell() {
        val dwell = ReturnEdgeDwell()
        dwell.arm(1000)
        assertTrue(dwell.enter(1400, false))
        assertFalse(dwell.shouldReturn(1434))
        assertTrue(dwell.shouldReturn(1435))
    }

    @Test fun pressingDuringDwellCancelsScheduledReturn() {
        val dwell = ReturnEdgeDwell()
        dwell.arm(1000)
        dwell.enter(1400, false)
        dwell.updateButtons(true)
        assertFalse(dwell.shouldReturn(5000))
        dwell.updateButtons(false)
        assertFalse(dwell.shouldReturn(5001))
        assertTrue(dwell.enter(5100, false))
        assertTrue(dwell.shouldReturn(5135))
    }

    @Test fun heldButtonAndImmediateEntryCannotArmReturn() {
        val dwell = ReturnEdgeDwell()
        dwell.arm(1000)
        assertFalse(dwell.enter(1200, false))
        assertFalse(dwell.enter(1500, true))
        assertFalse(dwell.shouldReturn(2000))
    }

    @Test fun leavingOrRearmingCancelsOldCandidate() {
        val dwell = ReturnEdgeDwell()
        dwell.arm(1000)
        dwell.enter(1400, false)
        dwell.cancel()
        assertFalse(dwell.shouldReturn(1500))
        dwell.enter(1600, false)
        dwell.arm(1700)
        assertFalse(dwell.shouldReturn(2000))
    }
}
