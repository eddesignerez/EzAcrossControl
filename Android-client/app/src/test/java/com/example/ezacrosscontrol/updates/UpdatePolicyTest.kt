package com.example.ezacrosscontrol.updates

import org.json.JSONObject
import org.json.JSONArray
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [28])
class UpdatePolicyTest {
    private fun release(remote: String = "1.1.20", draft: Boolean = false, prerelease: Boolean = false): JSONObject {
        val asset = JSONObject().put("name", "EZAcrossControl-$remote-android.apk").put("size", 1234)
            .put("state", "uploaded").put("digest", "sha256:" + "a".repeat(64))
            .put("browser_download_url", "https://github.com/eddesignerez/EzAcrossControl/releases/download/v$remote/EZAcrossControl-$remote-android.apk")
        return JSONObject().put("tag_name", "v$remote").put("draft", draft).put("prerelease", prerelease)
            .put("assets", JSONArray().put(asset))
    }
    @Test fun onlyNewStableVersionIsOffered() {
        assertNotNull(UpdatePolicy.release(release().toString(), "1.1.19"))
        assertNull(UpdatePolicy.release(release().toString(), "1.1.20"))
        assertNull(UpdatePolicy.release(release().toString(), "1.1.21"))
        assertNull(UpdatePolicy.release(release(draft = true).toString(), "1.1.19"))
        assertNull(UpdatePolicy.release(release(prerelease = true).toString(), "1.1.19"))
    }
    @Test fun exactAssetAndHashAreRequired() {
        val invalid = release()
        invalid.getJSONArray("assets").getJSONObject(0).put("browser_download_url", "https://evil.example/update.apk")
        assertNull(UpdatePolicy.release(invalid.toString(), "1.1.19"))
        val noDigest = release()
        noDigest.getJSONArray("assets").getJSONObject(0).remove("digest")
        assertNull(UpdatePolicy.release(noDigest.toString(), "1.1.19"))
        val huge = release()
        huge.getJSONArray("assets").getJSONObject(0).put("size", UpdatePolicy.MaxApkBytes + 1)
        assertNull(UpdatePolicy.release(huge.toString(), "1.1.19"))
    }
    @Test fun persistedCandidateCannotChangeRepositoryOrDowngrade() {
        val good = UpdatePolicy.release(release().toString(), "1.1.19")!!
        assertTrue(UpdatePolicy.valid(good))
        assertFalse(UpdatePolicy.valid(good.copy(url = good.url.replace("EzAcrossControl", "AnotherApp"))))
        assertFalse(UpdatePolicy.valid(good.copy(sha256 = "invalid")))
        assertFalse(UpdatePolicy.valid(good.copy(version = "latest")))
        assertFalse(UpdatePolicy.newer("1.1.18", "1.1.19"))
    }
    @Test fun invalidAndOverflowVersionsFailClosed() {
        for (value in listOf(null, "latest", "1.1.20-beta", "1.1.2147483648", "1.1")) assertNull(UpdatePolicy.version(value))
        assertTrue(UpdatePolicy.newer("1.1.20", "1.1.19"))
        assertFalse(UpdatePolicy.newer("1.1.19", "1.1.19"))
    }
    @Test fun apkIdentityRequiresSameSignerAndHigherCode() {
        val signer = setOf("fixture certificate")
        assertTrue(UpdatePolicy.sameIdentity("our.package", "our.package", "1.1.20", "1.1.20", 17, 18, signer, signer))
        assertFalse(UpdatePolicy.sameIdentity("our.package", "other.package", "1.1.20", "1.1.20", 17, 18, signer, signer))
        assertFalse(UpdatePolicy.sameIdentity("our.package", "our.package", "1.1.20", "1.1.19", 17, 18, signer, signer))
        assertFalse(UpdatePolicy.sameIdentity("our.package", "our.package", "1.1.20", "1.1.20", 17, 17, signer, signer))
        assertFalse(UpdatePolicy.sameIdentity("our.package", "our.package", "1.1.20", "1.1.20", 17, 18, signer, setOf("other certificate")))
        assertFalse(UpdatePolicy.sameIdentity("our.package", "our.package", "1.1.20", "1.1.20", 17, 18, emptySet(), emptySet()))
    }
}
