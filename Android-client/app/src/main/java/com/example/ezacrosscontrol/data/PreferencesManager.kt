package com.example.ezacrosscontrol.data

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

val Context.dataStore by preferencesDataStore(name = "settings")

enum class AppTheme {
    System, Light, Dark
}

class PreferencesManager(private val context: Context) {
    private val THEME_KEY = stringPreferencesKey("theme")
    private val IP_KEY = stringPreferencesKey("ip")
    private val MODE_KEY = stringPreferencesKey("connection_mode")
    val connectionModeFlow = context.dataStore.data.map { it[MODE_KEY] ?: "Auto" }
    suspend fun setConnectionMode(mode: String) {
        context.dataStore.edit { it[MODE_KEY] = mode }
    }
    private val PORT_KEY = stringPreferencesKey("port")

    val themeFlow: Flow<AppTheme> = context.dataStore.data.map { preferences ->
        val themeString = preferences[THEME_KEY] ?: AppTheme.System.name
        try {
            AppTheme.valueOf(themeString)
        } catch (e: Exception) {
            AppTheme.System
        }
    }

    suspend fun setTheme(theme: AppTheme) {
        context.dataStore.edit { preferences ->
            preferences[THEME_KEY] = theme.name
        }
    }

    val ipFlow: Flow<String> = context.dataStore.data.map { preferences ->
        preferences[IP_KEY] ?: ""
    }

    suspend fun setIp(ip: String) {
        context.dataStore.edit { preferences ->
            preferences[IP_KEY] = ip
        }
    }

    val portFlow: Flow<String> = context.dataStore.data.map { preferences ->
        val savedPort = preferences[PORT_KEY]
        if (savedPort == "8765") {
            com.example.ezacrosscontrol.Config.DEFAULT_PORT
        } else {
            savedPort ?: com.example.ezacrosscontrol.Config.DEFAULT_PORT
        }
    }

    suspend fun setPort(port: String) {
        context.dataStore.edit { preferences ->
            preferences[PORT_KEY] = port
        }
    }
}
