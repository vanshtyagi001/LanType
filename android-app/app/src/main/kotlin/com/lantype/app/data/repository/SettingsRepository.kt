package com.lantype.app.data.repository

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.lantype.app.data.model.SpeedUnit
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "lantype_settings")

class SettingsRepository(private val context: Context) {

    private object PreferencesKeys {
        val LAST_HOST = stringPreferencesKey("last_host")
        val LAST_PORT = intPreferencesKey("last_port")
        val LAST_PIN = stringPreferencesKey("last_pin")
        val SPEED_UNIT = stringPreferencesKey("speed_unit")
        val TYPING_SPEED = intPreferencesKey("typing_speed")
    }

    val lastHost: Flow<String> = context.dataStore.data.map { prefs ->
        prefs[PreferencesKeys.LAST_HOST] ?: "192.168.137.1"
    }

    val lastPort: Flow<Int> = context.dataStore.data.map { prefs ->
        prefs[PreferencesKeys.LAST_PORT] ?: 52443
    }

    val lastPin: Flow<String> = context.dataStore.data.map { prefs ->
        prefs[PreferencesKeys.LAST_PIN] ?: ""
    }

    val speedUnit: Flow<SpeedUnit> = context.dataStore.data.map { prefs ->
        when (prefs[PreferencesKeys.SPEED_UNIT]) {
            "Wpm" -> SpeedUnit.Wpm
            else -> SpeedUnit.Cpm
        }
    }

    val typingSpeed: Flow<Int> = context.dataStore.data.map { prefs ->
        prefs[PreferencesKeys.TYPING_SPEED] ?: 300
    }

    suspend fun saveConnectionDetails(host: String, port: Int, pin: String) {
        context.dataStore.edit { prefs ->
            prefs[PreferencesKeys.LAST_HOST] = host
            prefs[PreferencesKeys.LAST_PORT] = port
            prefs[PreferencesKeys.LAST_PIN] = pin
        }
    }

    suspend fun saveSpeedSettings(unit: SpeedUnit, speed: Int) {
        context.dataStore.edit { prefs ->
            prefs[PreferencesKeys.SPEED_UNIT] = unit.name
            prefs[PreferencesKeys.TYPING_SPEED] = speed
        }
    }
}
