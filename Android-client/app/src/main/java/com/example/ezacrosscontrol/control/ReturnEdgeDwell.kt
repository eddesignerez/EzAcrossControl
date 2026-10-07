package com.example.ezacrosscontrol.control

/** A return requires uninterrupted hover with no mouse button held. */
internal class ReturnEdgeDwell {
    private var armedAt = 0L
    private var enteredAt: Long? = null
    private var buttonsPressed = false

    fun arm(now: Long) {
        armedAt = now
        buttonsPressed = false
        cancel()
    }

    fun enter(now: Long, pressed: Boolean): Boolean {
        updateButtons(pressed)
        enteredAt = if (!pressed && now - armedAt >= 350L) now else null
        return enteredAt != null
    }

    fun updateButtons(pressed: Boolean) {
        buttonsPressed = pressed
        if (pressed) cancel()
    }

    fun cancel() { enteredAt = null }

    fun shouldReturn(now: Long): Boolean {
        val entered = enteredAt ?: return false
        return !buttonsPressed && now - entered >= 35L
    }
}
