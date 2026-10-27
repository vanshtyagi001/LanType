package com.lantype.app.ui.viewmodel

import com.lantype.app.data.model.DriverStatusMessage
import com.lantype.app.data.model.SessionState
import com.lantype.app.data.model.SessionStateMessage
import com.lantype.app.data.model.SpeedUnit
import com.lantype.app.data.network.ConnectionStatus

data class MainUiState(
    val connectionStatus: ConnectionStatus = ConnectionStatus.Disconnected,
    val isPaired: Boolean = false,
    val sessionState: SessionStateMessage = SessionStateMessage(),
    val driverStatus: DriverStatusMessage = DriverStatusMessage(),
    val text: String = "",
    val speedUnit: SpeedUnit = SpeedUnit.Cpm,
    val speedValue: Int = 300,
    val isPairingDialogVisible: Boolean = false,
    val isHelpDialogVisible: Boolean = false,
    val isQrScannerVisible: Boolean = false,
    val hostInput: String = "192.168.137.1",
    val portInput: String = "52443",
    val pinInput: String = "",
    val errorMessage: String? = null,
    val discoveredDevice: com.lantype.app.data.model.DiscoveredDevice? = null,
    val isScanningDevices: Boolean = false
) {
    val characterCount: Int
        get() = text.length

    val lineCount: Int
        get() = if (text.isEmpty()) 0 else text.count { it == '\n' } + 1

    val isConnectedAndReady: Boolean
        get() = connectionStatus == ConnectionStatus.Connected && 
                isPaired && 
                sessionState.state != SessionState.Typing

    val isTyping: Boolean
        get() = sessionState.state == SessionState.Typing

    val isPaused: Boolean
        get() = sessionState.state == SessionState.Paused

    val progressPercent: Float
        get() = if (sessionState.totalCharacters > 0) {
            sessionState.charactersTyped.toFloat() / sessionState.totalCharacters.toFloat()
        } else 0f

    val estimatedTotalDurationSeconds: Long
        get() {
            if (characterCount == 0) return 0
            val effectiveCpm = when (speedUnit) {
                SpeedUnit.Cpm -> speedValue.coerceAtLeast(30)
                SpeedUnit.Wpm -> (speedValue * 5).coerceAtLeast(30)
            }
            return (characterCount * 60L) / effectiveCpm
        }
}
