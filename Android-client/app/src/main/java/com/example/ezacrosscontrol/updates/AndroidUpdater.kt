package com.example.ezacrosscontrol.updates

import android.app.DownloadManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.provider.Settings
import androidx.activity.ComponentActivity
import androidx.core.content.FileProvider
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.io.File
import java.security.MessageDigest
import java.util.UUID
import java.util.concurrent.TimeUnit

internal data class UpdateState(val phase: String = "idle", val version: String = "", val progress: Int = 0, val message: String = "")

/** DownloadManager persists the authorized transfer independently of Activity lifetime. */
internal class AndroidUpdater(private val activity: ComponentActivity) {
    private val prefs = activity.getSharedPreferences("approved-updates", Context.MODE_PRIVATE)
    private val downloads = activity.getSystemService(Context.DOWNLOAD_SERVICE) as DownloadManager
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val mutableState = MutableStateFlow(UpdateState())
    val state = mutableState.asStateFlow()
    private var foreground = false
    private var poll: Job? = null
    private var busy = false
    private var available: UpdateCandidate? = null
    private var candidate = restoreCandidate()
    private var downloadId = prefs.getLong("downloadId", -1)
    private var permissionPending = prefs.getBoolean("permissionPending", false)
    private val installed: String get() = activity.packageManager.getPackageInfo(activity.packageName, 0).versionName ?: ""

    fun checkAtStartup() {
        scope.launch {
            try {
                val release = withContext(Dispatchers.IO) {
                    val client = OkHttpClient.Builder().followRedirects(false).callTimeout(10, TimeUnit.SECONDS).build()
                    client.newCall(Request.Builder().url("https://api.github.com/repos/${UpdatePolicy.Repository}/releases/latest")
                        .header("User-Agent", "EZAcrossControl-Updater/1.0").build()).execute().use { response ->
                        if (!response.isSuccessful) return@withContext null
                        val body = response.body ?: return@withContext null
                        val bytes = body.byteStream().use { input ->
                            val output = java.io.ByteArrayOutputStream()
                            val buffer = ByteArray(8192)
                            while (true) {
                                val count = input.read(buffer)
                                if (count < 0) break
                                if (output.size() + count > 1024 * 1024) return@withContext null
                                output.write(buffer, 0, count)
                            }
                            output.toByteArray()
                        }
                        UpdatePolicy.release(bytes.toString(Charsets.UTF_8), installed)
                    }
                }
                available = release
                showOffer()
            } catch (_: Exception) { /* Offline checks never interrupt local control. */ }
        }
    }

    fun resume() {
        foreground = true
        if (candidate == null && downloadId > 0) clear()
        if (candidate != null && !UpdatePolicy.newer(candidate?.version, installed)) clear()
        if (permissionPending && candidate != null) {
            permissionPending = false
            prefs.edit().putBoolean("permissionPending", false).apply()
            if (canInstall()) enqueue(candidate!!) else {
                available = candidate
                clear()
                showOffer("Update Permission Denied")
                return
            }
        }
        if (downloadId > 0 && candidate != null) startPolling() else showOffer()
    }
    fun pause() { foreground = false; poll?.cancel() }
    fun close() { scope.cancel() }

    private fun showOffer(message: String = "") {
        val offer = available ?: return
        if (foreground && downloadId <= 0 && !permissionPending && !busy)
            mutableState.value = UpdateState("offer", offer.version, message = message)
    }

    fun accept() {
        if (busy || downloadId > 0) return
        val offer = available ?: return
        if (!UpdatePolicy.valid(offer) || !UpdatePolicy.newer(offer.version, installed)) return
        candidate = offer
        saveCandidate(offer)
        if (!canInstall()) {
            permissionPending = true
            prefs.edit().putBoolean("permissionPending", true).apply()
            mutableState.value = UpdateState("permission", offer.version)
            try {
                activity.startActivity(Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:${activity.packageName}")))
            } catch (_: Exception) {
                clear(); showOffer("Update Permission Denied")
            }
            return
        }
        enqueue(offer)
    }
    fun cancel() {
        poll?.cancel()
        busy = false
        if (downloadId > 0) downloads.remove(downloadId)
        available = null
        clear()
    }
    private fun canInstall() = Build.VERSION.SDK_INT < 26 || activity.packageManager.canRequestPackageInstalls()

    private fun enqueue(offer: UpdateCandidate) {
        if (downloadId > 0) return
        try {
            candidate = offer
            saveCandidate(offer)
            val request = DownloadManager.Request(Uri.parse(offer.url))
                .setTitle("EZ Across Control ${offer.version}")
                .setMimeType("application/vnd.android.package-archive")
                .setAllowedOverRoaming(false)
                .setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED)
                .setDestinationInExternalFilesDir(activity, Environment.DIRECTORY_DOWNLOADS, "ezac-update-${UUID.randomUUID()}.apk")
            downloadId = downloads.enqueue(request)
            prefs.edit().putLong("downloadId", downloadId).apply()
            startPolling()
        } catch (_: Exception) { clear(); showOffer("Update Failed") }
    }

    private fun startPolling() {
        if (poll?.isActive == true) return
        poll = scope.launch {
            while (foreground && downloadId > 0) {
                val id = downloadId
                val approved = candidate ?: break
                try {
                    val result = withContext(Dispatchers.IO) {
                        downloads.query(DownloadManager.Query().setFilterById(id)).use { cursor ->
                            if (!cursor.moveToFirst()) return@withContext null
                            Triple(cursor.getInt(cursor.getColumnIndexOrThrow(DownloadManager.COLUMN_STATUS)),
                                cursor.getLong(cursor.getColumnIndexOrThrow(DownloadManager.COLUMN_BYTES_DOWNLOADED_SO_FAR)),
                                cursor.getLong(cursor.getColumnIndexOrThrow(DownloadManager.COLUMN_TOTAL_SIZE_BYTES)))
                        }
                    }
                    if (result == null || result.first == DownloadManager.STATUS_FAILED) {
                        fail("Update Failed"); break
                    }
                    if (result.second > approved.size || (result.third > 0 && result.third != approved.size)) {
                        fail("Update Invalid Package"); break
                    }
                    mutableState.value = UpdateState("download", approved.version, ((result.second * 100 / approved.size).coerceIn(0, 100)).toInt())
                    if (result.first == DownloadManager.STATUS_SUCCESSFUL) {
                        if (!canInstall()) { fail("Update Permission Denied"); break }
                        busy = true
                        mutableState.value = UpdateState("validate", approved.version)
                        val file = withContext(Dispatchers.IO) { validate(id, approved) }
                        busy = false
                        if (file == null) { fail("Update Invalid Package"); break }
                        if (!foreground || downloadId != id || candidate != approved) {
                            file.delete(); break
                        }
                        try {
                            val uri = FileProvider.getUriForFile(activity, activity.packageName + ".updates", file)
                            activity.startActivity(Intent(Intent.ACTION_VIEW).setDataAndType(uri, "application/vnd.android.package-archive")
                                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION))
                            // Dispatch exactly once; cancellation of the OS installer does
                            // not cause automatic reinstall loops on every resume.
                            downloads.remove(id)
                            available = null
                            clear()
                        } catch (_: Exception) { file.delete(); fail("Update Failed") }
                        break
                    }
                } catch (error: CancellationException) { busy = false; throw error }
                catch (_: Exception) { busy = false; fail("Update Failed"); break }
                delay(1000)
            }
        }
    }

    private fun validate(id: Long, approved: UpdateCandidate): File? {
        if (!UpdatePolicy.valid(approved) || !UpdatePolicy.newer(approved.version, installed)) return null
        val uri = downloads.getUriForDownloadedFile(id) ?: return null
        val directory = File(activity.filesDir, "updates").apply { mkdirs() }
        val file = File(directory, "${UUID.randomUUID()}.apk")
        return try {
            val digest = MessageDigest.getInstance("SHA-256")
            var total = 0L
            activity.contentResolver.openInputStream(uri)?.use { input ->
                file.outputStream().use { output ->
                    val buffer = ByteArray(65536)
                    while (true) {
                        val count = input.read(buffer)
                        if (count < 0) break
                        total += count
                        if (total > approved.size) error("APK size exceeded")
                        digest.update(buffer, 0, count); output.write(buffer, 0, count)
                    }
                }
            } ?: error("No APK stream")
            val expected = approved.sha256.chunked(2).map { it.toInt(16).toByte() }.toByteArray()
            if (total != approved.size || !MessageDigest.isEqual(digest.digest(), expected)) error("APK digest mismatch")
            val flags = if (Build.VERSION.SDK_INT >= 28) PackageManager.GET_SIGNING_CERTIFICATES else {
                @Suppress("DEPRECATION") PackageManager.GET_SIGNATURES
            }
            val archive = activity.packageManager.getPackageArchiveInfo(file.absolutePath, flags) ?: error("Not an APK")
            val current = activity.packageManager.getPackageInfo(activity.packageName, flags)
            @Suppress("DEPRECATION")
            val code = if (Build.VERSION.SDK_INT >= 28) archive.longVersionCode else archive.versionCode.toLong()
            @Suppress("DEPRECATION")
            val installedCode = if (Build.VERSION.SDK_INT >= 28) current.longVersionCode else current.versionCode.toLong()
            @Suppress("DEPRECATION")
            val currentSigners = if (Build.VERSION.SDK_INT >= 28) current.signingInfo?.apkContentsSigners else current.signatures
            @Suppress("DEPRECATION")
            val newSigners = if (Build.VERSION.SDK_INT >= 28) archive.signingInfo?.apkContentsSigners else archive.signatures
            if (!UpdatePolicy.sameIdentity(activity.packageName, archive.packageName, approved.version, archive.versionName,
                    installedCode, code, currentSigners.orEmpty().map { it.toCharsString() }.toSet(),
                    newSigners.orEmpty().map { it.toCharsString() }.toSet())) error("APK identity/signer/version mismatch")
            file
        } catch (_: Exception) { file.delete(); null }
    }

    private fun fail(message: String) {
        if (downloadId > 0) downloads.remove(downloadId)
        available = candidate
        clear()
        showOffer(message)
    }
    private fun clear() {
        downloadId = -1; candidate = null; permissionPending = false
        prefs.edit().clear().apply()
        mutableState.value = UpdateState()
    }
    private fun saveCandidate(value: UpdateCandidate) {
        prefs.edit().putString("candidate", JSONObject().put("version", value.version).put("url", value.url)
            .put("size", value.size).put("sha256", value.sha256).toString()).apply()
    }
    private fun restoreCandidate(): UpdateCandidate? = try {
        val json = JSONObject(prefs.getString("candidate", null) ?: "{}")
        UpdateCandidate(json.getString("version"), json.getString("url"), json.getLong("size"), json.getString("sha256"))
            .takeIf { UpdatePolicy.valid(it) }
    } catch (_: Exception) { null }
}
