package com.example.ezacrosscontrol.updates

import org.json.JSONObject

internal data class UpdateCandidate(val version: String, val url: String, val size: Long, val sha256: String)

internal object UpdatePolicy {
    const val Repository = "eddesignerez/EzAcrossControl"
    const val MaxApkBytes = 64L * 1024 * 1024
    fun sameIdentity(expectedPackage: String, actualPackage: String?, expectedVersion: String,
                     actualVersion: String?, installedCode: Long, actualCode: Long,
                     currentSigners: Set<String>, newSigners: Set<String>): Boolean =
        expectedPackage == actualPackage && expectedVersion == actualVersion && actualCode > installedCode
            && currentSigners.isNotEmpty() && currentSigners == newSigners
    fun version(raw: String?): List<Int>? {
        if (raw == null || !Regex("^v?[0-9]+\\.[0-9]+\\.[0-9]+$").matches(raw)) return null
        return raw.removePrefix("v").split('.').map { it.toIntOrNull() ?: return null }
    }
    fun newer(remote: String?, installed: String?): Boolean {
        val a = version(remote) ?: return false
        val b = version(installed) ?: return false
        for (i in a.indices) if (a[i] != b[i]) return a[i] > b[i]
        return false
    }
    fun valid(candidate: UpdateCandidate): Boolean {
        val parts = version(candidate.version) ?: return false
        if (parts.joinToString(".") != candidate.version) return false
        return candidate.url == "https://github.com/$Repository/releases/download/v${candidate.version}/EZAcrossControl-${candidate.version}-android.apk"
            && candidate.size in 1..MaxApkBytes && Regex("^[0-9a-f]{64}$").matches(candidate.sha256)
    }
    fun release(json: String, installed: String): UpdateCandidate? {
        val root = JSONObject(json)
        if (root.getBoolean("draft") || root.getBoolean("prerelease")) return null
        val tag = root.getString("tag_name")
        val parts = version(tag) ?: return null
        val remote = parts.joinToString(".")
        if (tag != "v$remote" || !newer(remote, installed)) return null
        val name = "EZAcrossControl-$remote-android.apk"
        val assets = root.getJSONArray("assets")
        val matching = (0 until assets.length()).map { assets.getJSONObject(it) }.filter { it.optString("name") == name }
        if (matching.size != 1) return null
        val asset = matching.single()
        val digest = asset.optString("digest")
        if (asset.optString("state") != "uploaded" || !digest.startsWith("sha256:")) return null
        return UpdateCandidate(remote, asset.getString("browser_download_url"), asset.getLong("size"), digest.removePrefix("sha256:"))
            .takeIf { valid(it) }
    }
}
