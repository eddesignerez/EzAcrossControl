package com.example.ezacrosscontrol.control

import android.content.Context
import android.os.PowerManager
import android.util.Log

/** Wakes the display briefly after a paired Host asks to begin remote control. */
internal object ScreenWakeController {
    private const val Tag = "EZAcross:ScreenWake"
    private const val TimeoutMillis = 2_000L

    @Suppress("DEPRECATION")
    fun wake(context: Context) {
        val powerManager = context.getSystemService(PowerManager::class.java) ?: return
        if (powerManager.isInteractive) return

        val wakeLock = powerManager.newWakeLock(
            PowerManager.SCREEN_BRIGHT_WAKE_LOCK or
                PowerManager.ACQUIRE_CAUSES_WAKEUP or
                PowerManager.ON_AFTER_RELEASE,
            Tag
        )
        wakeLock.setReferenceCounted(false)

        try {
            wakeLock.acquire(TimeoutMillis)
            Log.i(Tag, "Screen wake requested by paired Host")
        } catch (error: SecurityException) {
            Log.w(Tag, "Android denied the screen wake request", error)
        }
    }
}
