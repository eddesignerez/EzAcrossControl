package com.example.ezacrosscontrol

import android.content.Context
import androidx.compose.runtime.staticCompositionLocalOf
import org.json.JSONObject
import java.util.Locale

data class LanguageOption(val code: String, val label: String)

class AppStrings(context: Context, preference: String) {
    private val catalog = Catalog.load(context)
    val languages = catalog.getJSONArray("languages").let { array ->
        (0 until array.length()).map { array.getJSONObject(it).let { LanguageOption(it.getString("code"), it.getString("label")) } }
    }
    val locale = resolve(preference, Locale.getDefault().toLanguageTag())
    private val messages = catalog.getJSONObject("translations").getJSONObject(locale)
    operator fun get(source: String): String {
        if (source.endsWith(':')) return get(source.dropLast(1)) + ":"
        return messages.optString(source, source)
    }
    companion object {
        fun resolve(preference: String, system: String): String {
            val tag = if (preference == "system") system else preference
            val base = tag.substringBefore('-').lowercase(Locale.ROOT)
            return when (base) {
                "pt" -> "pt-BR"; "zh" -> "zh-CN"
                "en", "es", "ja", "it", "fr", "de", "vi", "ko", "ar" -> base
                else -> "en"
            }
        }
    }
    private object Catalog {
        private var cached: JSONObject? = null
        fun load(context: Context): JSONObject = cached ?: context.assets.open("catalog.json").bufferedReader()
            .use { JSONObject(it.readText()) }.also { cached = it }
    }
}

val LocalStrings = staticCompositionLocalOf<AppStrings> { error("AppStrings provider missing") }
