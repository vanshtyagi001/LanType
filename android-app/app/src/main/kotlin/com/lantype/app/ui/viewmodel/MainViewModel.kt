package com.lantype.app.ui.viewmodel

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.lantype.app.data.model.SpeedUnit
import com.lantype.app.data.repository.LANTypeRepository
import com.lantype.app.data.repository.SettingsRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

class MainViewModel @JvmOverloads constructor(
    application: Application,
    private val repository: LANTypeRepository = LANTypeRepository(),
    private val settingsRepository: SettingsRepository = SettingsRepository(application)
) : AndroidViewModel(application) {

    private val _uiState = MutableStateFlow(MainUiState())
    val uiState: StateFlow<MainUiState> = _uiState.asStateFlow()

    init {
        // Load initial settings
        viewModelScope.launch {
            val savedHost = settingsRepository.lastHost.first()
            val savedPort = settingsRepository.lastPort.first()
            val savedPin = settingsRepository.lastPin.first()
            val savedUnit = settingsRepository.speedUnit.first()
            val savedSpeed = settingsRepository.typingSpeed.first()

            _uiState.update {
                it.copy(
                    hostInput = savedHost,
                    portInput = savedPort.toString(),
                    pinInput = savedPin,
                    speedUnit = savedUnit,
                    speedValue = savedSpeed
                )
            }
        }

        // Collect repository states
        viewModelScope.launch {
            repository.connectionStatus.collect { status ->
                _uiState.update { it.copy(connectionStatus = status) }
            }
        }

        viewModelScope.launch {
            repository.sessionState.collect { state ->
                _uiState.update { it.copy(sessionState = state) }
            }
        }

        viewModelScope.launch {
            repository.driverStatus.collect { driver ->
                _uiState.update { it.copy(driverStatus = driver) }
            }
        }

        viewModelScope.launch {
            repository.lastError.collect { err ->
                if (err != null) {
                    _uiState.update { it.copy(errorMessage = err) }
                }
            }
        }
    }

    fun onTextChanged(newText: String) {
        _uiState.update { it.copy(text = newText) }
    }

    fun onClearText() {
        _uiState.update { it.copy(text = "") }
    }

    fun onPasteText(pasted: String) {
        _uiState.update { it.copy(text = it.text + pasted) }
    }

    fun onSpeedUnitChanged(unit: SpeedUnit) {
        val currentSpeed = _uiState.value.speedValue
        val convertedSpeed = when (unit) {
            SpeedUnit.Cpm -> if (_uiState.value.speedUnit == SpeedUnit.Wpm) currentSpeed * 5 else currentSpeed
            SpeedUnit.Wpm -> if (_uiState.value.speedUnit == SpeedUnit.Cpm) (currentSpeed / 5).coerceAtLeast(6) else currentSpeed
        }
        _uiState.update { it.copy(speedUnit = unit, speedValue = convertedSpeed) }

        viewModelScope.launch {
            settingsRepository.saveSpeedSettings(unit, convertedSpeed)
        }
    }

    fun onSpeedValueChanged(speed: Int) {
        _uiState.update { it.copy(speedValue = speed) }
        viewModelScope.launch {
            settingsRepository.saveSpeedSettings(_uiState.value.speedUnit, speed)
        }
    }

    fun showPairingDialog(show: Boolean) {
        _uiState.update { it.copy(isPairingDialogVisible = show) }
    }

    fun showHelpDialog(show: Boolean) {
        _uiState.update { it.copy(isHelpDialogVisible = show) }
    }

    fun showQrScanner(show: Boolean) {
        _uiState.update { it.copy(isQrScannerVisible = show) }
    }

    fun onHostInputChanged(host: String) {
        _uiState.update { it.copy(hostInput = host) }
    }

    fun onPortInputChanged(port: String) {
        _uiState.update { it.copy(portInput = port) }
    }

    fun onPinInputChanged(pin: String) {
        _uiState.update { it.copy(pinInput = pin) }
    }

    fun clearError() {
        _uiState.update { it.copy(errorMessage = null) }
    }

    fun handleScannedQrCode(qrData: String): Boolean {
        try {
            if (qrData.startsWith("lantype://pair")) {
                val uri = Uri.parse(qrData)
                val host = uri.getQueryParameter("host")
                val port = uri.getQueryParameter("port")?.toIntOrNull() ?: 52443
                val pin = uri.getQueryParameter("pin")

                if (!host.isNullOrBlank() && !pin.isNullOrBlank()) {
                    _uiState.update {
                        it.copy(
                            hostInput = host,
                            portInput = port.toString(),
                            pinInput = pin,
                            isQrScannerVisible = false,
                            isPairingDialogVisible = false
                        )
                    }
                    connectAndPair(host, port, pin)
                    return true
                }
            }
        } catch (_: Exception) {
        }
        return false
    }

    fun connectAndPair(host: String? = null, port: Int? = null, pin: String? = null) {
        val targetHost = host ?: _uiState.value.hostInput.trim()
        val targetPort = port ?: (_uiState.value.portInput.trim().toIntOrNull() ?: 52443)
        val targetPin = pin ?: _uiState.value.pinInput.trim()

        if (targetHost.isBlank() || targetPin.isBlank()) {
            _uiState.update { it.copy(errorMessage = "Host IP and Pairing PIN cannot be empty.") }
            return
        }

        viewModelScope.launch {
            settingsRepository.saveConnectionDetails(targetHost, targetPort, targetPin)
        }

        repository.connectAndPair(targetHost, targetPort, targetPin)
        _uiState.update { it.copy(isPairingDialogVisible = false) }
    }

    fun disconnect() {
        repository.disconnect()
    }

    fun startTyping() {
        val text = _uiState.value.text
        if (text.isEmpty()) {
            _uiState.update { it.copy(errorMessage = "Text payload is empty.") }
            return
        }
        repository.startTyping(
            text = text,
            unit = _uiState.value.speedUnit,
            speed = _uiState.value.speedValue
        )
    }

    fun pauseTyping() {
        repository.pauseTyping()
    }

    fun resumeTyping() {
        repository.resumeTyping()
    }

    fun stopTyping() {
        repository.stopTyping()
    }

    fun emergencyStop() {
        repository.emergencyStop()
    }
}
