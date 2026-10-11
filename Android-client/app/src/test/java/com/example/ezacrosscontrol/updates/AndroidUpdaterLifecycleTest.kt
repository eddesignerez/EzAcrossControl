package com.example.ezacrosscontrol.updates

import android.content.Context
import android.content.pm.PackageInfo
import android.provider.Settings
import androidx.activity.ComponentActivity
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.*
import org.junit.Assert.*
import org.junit.After
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.Robolectric
import org.robolectric.RobolectricTestRunner
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config

@OptIn(ExperimentalCoroutinesApi::class)
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [28])
class AndroidUpdaterLifecycleTest {
    private lateinit var activity: ComponentActivity
    private lateinit var updater: AndroidUpdater
    private val approved = UpdateCandidate("1.1.20", "https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.20/EZAcrossControl-1.1.20-android.apk", 1234, "a".repeat(64))

    @Before fun setup() {
        Dispatchers.setMain(StandardTestDispatcher())
        activity = Robolectric.buildActivity(ComponentActivity::class.java).setup().get()
        shadowOf(activity.packageManager).installPackage(PackageInfo().apply {
            packageName = activity.packageName; versionName = "1.1.19"
        })
        activity.getSharedPreferences("approved-updates", Context.MODE_PRIVATE).edit().clear().commit()
        updater = AndroidUpdater(activity)
        // Supply metadata without network access; exercise real consent/lifecycle code.
        AndroidUpdater::class.java.getDeclaredField("available").apply { isAccessible = true }.set(updater, approved)
    }
    @After fun teardown() { updater.close(); Dispatchers.resetMain() }

    @Test fun startupOfferDoesNotDownloadWithoutConsent() {
        updater.resume()
        assertEquals("offer", updater.state.value.phase)
        val prefs = activity.getSharedPreferences("approved-updates", Context.MODE_PRIVATE)
        assertFalse(prefs.contains("candidate"))
        assertFalse(prefs.contains("downloadId"))
        assertNull(shadowOf(activity).nextStartedActivity)
    }
    @Test fun acceptRequestsSystemPermissionAndCancelRevokesApproval() {
        updater.resume(); updater.accept()
        assertEquals("permission", updater.state.value.phase)
        assertEquals(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, shadowOf(activity).nextStartedActivity.action)
        val prefs = activity.getSharedPreferences("approved-updates", Context.MODE_PRIVATE)
        assertTrue(prefs.getBoolean("permissionPending", false))
        updater.cancel(); updater.resume()
        assertEquals("idle", updater.state.value.phase)
        assertTrue(prefs.all.isEmpty())
        assertNull(shadowOf(activity).nextStartedActivity)
    }
    @Test fun permissionDenialShowsRetryAndNeverLaunchesInstaller() {
        updater.resume(); updater.accept()
        shadowOf(activity).nextStartedActivity
        updater.pause(); updater.resume()
        assertEquals("offer", updater.state.value.phase)
        assertEquals("Update Permission Denied", updater.state.value.message)
        assertNull(shadowOf(activity).nextStartedActivity)
        assertFalse(activity.getSharedPreferences("approved-updates", Context.MODE_PRIVATE).contains("downloadId"))
    }
    @Test fun returningAfterCancelDoesNotAutomaticallyAskAgain() {
        updater.resume(); updater.cancel(); updater.pause(); updater.resume()
        assertEquals("idle", updater.state.value.phase)
        assertNull(shadowOf(activity).nextStartedActivity)
    }
}
