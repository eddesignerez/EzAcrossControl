package com.example.ezacrosscontrol

import org.junit.Assert.assertEquals
import org.junit.Test

class LanguageSelectionTest {
    @Test fun followsSupportedSystemLocales() {
        assertEquals("pt-BR", AppStrings.resolve("system", "pt-PT"))
        assertEquals("zh-CN", AppStrings.resolve("system", "zh-TW"))
        assertEquals("ja", AppStrings.resolve("system", "ja-JP"))
        assertEquals("en", AppStrings.resolve("system", "nl-NL"))
    }
    @Test fun manualSelectionOverridesSystem() {
        assertEquals("ar", AppStrings.resolve("ar", "en-US"))
        assertEquals("de", AppStrings.resolve("de", "pt-BR"))
    }
}
