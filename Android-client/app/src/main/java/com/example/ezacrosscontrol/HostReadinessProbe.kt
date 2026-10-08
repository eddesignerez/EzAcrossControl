package com.example.ezacrosscontrol

import android.os.Build
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.util.concurrent.TimeUnit

class HostReadinessProbe {
    private val client = OkHttpClient.Builder().callTimeout(2, TimeUnit.SECONDS).build()
    suspend fun check(ip: String, port: String): Triple<Boolean, Boolean, Boolean> = withContext(Dispatchers.IO) {
        val octets = ip.trim().split('.')
        val validIp = octets.size == 4 && octets.all { it.toIntOrNull() in 0..255 }
        val validPort = port.toIntOrNull() in 1..65535
        if (!validIp || !validPort) return@withContext Triple(false, false, false)
        val url = "http://${ip.trim()}:${port.trim()}/readiness".toHttpUrlOrNull()
            ?.newBuilder()?.addQueryParameter("device", Build.MODEL)?.build()
            ?: return@withContext Triple(false, false, false)
        try {
            client.newCall(Request.Builder().url(url).build()).execute().use { response ->
                if (!response.isSuccessful) return@withContext Triple(false, false, false)
                val payload = JSONObject(response.body?.string().orEmpty())
                Triple(true, payload.optBoolean("UsbReady"), payload.optBoolean("WifiReady"))
            }
        } catch (_: Exception) { Triple(false, false, false) }
    }
}
