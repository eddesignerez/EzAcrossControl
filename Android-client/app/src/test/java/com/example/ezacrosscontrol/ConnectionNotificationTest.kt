package com.example.ezacrosscontrol

import android.Manifest
import android.app.Notification
import android.app.NotificationManager
import android.content.Context
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [30])
class ConnectionNotificationTest {
    private val context: Context get() = RuntimeEnvironment.getApplication()
    private val manager get() = context.getSystemService(NotificationManager::class.java)

    @Test fun disconnectRemovesConnectionIndicator() {
        val indicator = ConnectionNotification(context)
        val strings = AppStrings(context, "pt-BR")
        indicator.update(false, null, strings)
        assertTrue(manager.activeNotifications.isEmpty())
        indicator.update(true, "Wi-Fi", strings)
        val notification = manager.activeNotifications.single().notification
        assertEquals("Conectado · Wi-Fi", notification.extras.getCharSequence(Notification.EXTRA_TEXT))
        assertEquals(R.drawable.ic_connection_status, notification.smallIcon.resId)
        assertTrue(notification.flags and Notification.FLAG_ONGOING_EVENT != 0)
        assertNull(manager.getNotificationChannel(ConnectionNotification.CHANNEL_ID).sound)
        assertNotNull(notification.contentIntent)
        indicator.update(false, null, strings)
        assertTrue(manager.activeNotifications.isEmpty())
    }

    @Test fun transportAndLanguageUpdateTheExistingIndicator() {
        val indicator = ConnectionNotification(context)
        indicator.update(true, "Wi-Fi", AppStrings(context, "pt-BR"))
        indicator.update(true, "USB", AppStrings(context, "en"))
        assertEquals("Connected · USB", manager.activeNotifications.single().notification.extras.getCharSequence(Notification.EXTRA_TEXT))
        indicator.clear()
    }

    @Test @Config(sdk = [33]) fun deniedNotificationPermissionDoesNotBreakConnection() {
        shadowOf(RuntimeEnvironment.getApplication()).denyPermissions(Manifest.permission.POST_NOTIFICATIONS)
        ConnectionNotification(context).update(true, "USB", AppStrings(context, "pt-BR"))
        assertTrue(manager.activeNotifications.isEmpty())
    }
}
