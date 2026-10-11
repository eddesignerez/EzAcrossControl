package com.example.ezacrosscontrol.protocol

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.nio.charset.StandardCharsets
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.security.MessageDigest
import java.security.PrivateKey
import java.security.Signature
import java.util.UUID

/** Per-installation identity. The private key is non-exportable Android Keystore material. */
class PairingIdentity(context: Context) {
    private val preferences = context.applicationContext.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)

    val installationId: String by lazy {
        preferences.getString(INSTALLATION_ID, null) ?: UUID.randomUUID().toString().also {
            preferences.edit().putString(INSTALLATION_ID, it).apply()
        }
    }

    val publicKey: String
        get() = Base64.encodeToString(keyStore.getCertificate(ALIAS).publicKey.encoded, Base64.NO_WRAP)

    val pairingCode: String
        get() {
            val digest = MessageDigest.getInstance("SHA-256").digest(Base64.decode(publicKey, Base64.NO_WRAP))
            val number = ((digest[0].toInt() and 0xff) or
                ((digest[1].toInt() and 0xff) shl 8) or
                ((digest[2].toInt() and 0xff) shl 16) or
                ((digest[3].toInt() and 0xff) shl 24)).toUInt() % 1_000_000u
            return number.toString().padStart(6, '0')
        }

    init { ensureKey() }

    fun sign(challenge: String): String {
        val signature = Signature.getInstance("SHA256withECDSA")
        signature.initSign(keyStore.getKey(ALIAS, null) as PrivateKey)
        signature.update("$challenge|$installationId".toByteArray(StandardCharsets.UTF_8))
        return Base64.encodeToString(signature.sign(), Base64.NO_WRAP)
    }

    private val keyStore: KeyStore
        get() = KeyStore.getInstance(ANDROID_KEY_STORE).apply { load(null) }

    private fun ensureKey() {
        if (keyStore.containsAlias(ALIAS)) return
        val generator = KeyPairGenerator.getInstance(KeyProperties.KEY_ALGORITHM_EC, ANDROID_KEY_STORE)
        generator.initialize(KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_SIGN or KeyProperties.PURPOSE_VERIFY)
            .setDigests(KeyProperties.DIGEST_SHA256)
            .setAlgorithmParameterSpec(java.security.spec.ECGenParameterSpec("secp256r1"))
            .build())
        generator.generateKeyPair()
    }

    private companion object {
        const val ANDROID_KEY_STORE = "AndroidKeyStore"
        const val ALIAS = "ezacross.pairing.identity.v1"
        const val PREFERENCES = "pairing_identity"
        const val INSTALLATION_ID = "installation_id"
    }
}
