package com.lantype.app.data.repository

import android.os.Build
import android.util.Log
import com.lantype.app.data.model.DriverStatusMessage
import com.lantype.app.data.model.EmergencyStopMessage
import com.lantype.app.data.model.ErrorMessage
import com.lantype.app.data.model.PairRequestMessage
import com.lantype.app.data.model.PairResponseMessage
import com.lantype.app.data.model.PauseTypingMessage
import com.lantype.app.data.model.ProtocolJson
import com.lantype.app.data.model.ResumeTypingMessage
import com.lantype.app.data.model.SessionState
import com.lantype.app.data.model.SessionStateMessage
import com.lantype.app.data.model.SpeedUnit
import com.lantype.app.data.model.StartTypingMessage
import com.lantype.app.data.model.StopTypingMessage
import com.lantype.app.data.network.ConnectionStatus
import com.lantype.app.data.network.LANWebSocketClient
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class LANTypeRepository(
    private val webSocketClient: LANWebSocketClient = LANWebSocketClient()
) {
    private val tag = "LANTypeRepository"
    private val scope = CoroutineScope(Dispatchers.IO + Job())

    val connectionStatus: StateFlow<ConnectionStatus> = webSocketClient.connectionStatus

    private val _sessionState = MutableStateFlow(SessionStateMessage())
    val sessionState: StateFlow<SessionStateMessage> = _sessionState.asStateFlow()

    private val _driverStatus = MutableStateFlow(DriverStatusMessage())
    val driverStatus: StateFlow<DriverStatusMessage> = _driverStatus.asStateFlow()

    private val _pairResponse = MutableStateFlow<PairResponseMessage?>(null)
    val pairResponse: StateFlow<PairResponseMessage?> = _pairResponse.asStateFlow()

    private val _isPaired = MutableStateFlow(false)
    val isPaired: StateFlow<Boolean> = _isPaired.asStateFlow()

    private val _lastError = MutableStateFlow<String?>(null)
    val lastError: StateFlow<String?> = _lastError.asStateFlow()

    private var sessionToken: String? = null
    private var pendingPin: String? = null

    init {
        scope.launch {
            webSocketClient.incomingMessages.collect { json ->
                handleIncomingMessage(json)
            }
        }

        scope.launch {
            webSocketClient.connectionStatus.collect { status ->
                if (status == ConnectionStatus.Connected) {
                    val pin = pendingPin
                    if (!pin.isNullOrBlank() && pin.length >= 4 && !_isPaired.value) {
                        sendPairRequest(pin)
                    }
                } else if (status == ConnectionStatus.Disconnected) {
                    _isPaired.value = false
                    _sessionState.value = _sessionState.value.copy(state = SessionState.Disconnected)
                }
            }
        }
    }

    fun setPendingPin(pin: String) {
        pendingPin = pin
        if (webSocketClient.connectionStatus.value == ConnectionStatus.Connected && !_isPaired.value && pin.length >= 4) {
            sendPairRequest(pin)
        }
    }

    fun connectAndPair(host: String, port: Int, pin: String) {
        pendingPin = pin
        _lastError.value = null
        if (webSocketClient.connectionStatus.value == ConnectionStatus.Connected) {
            sendPairRequest(pin)
        } else {
            webSocketClient.connect(host, port)
        }
    }

    fun reconnect() {
        webSocketClient.reconnect()
    }

    fun disconnect() {
        pendingPin = null
        sessionToken = null
        _isPaired.value = false
        webSocketClient.disconnect()
        _sessionState.value = SessionStateMessage()
    }

    private fun sendPairRequest(pin: String) {
        val deviceName = "${Build.MANUFACTURER} ${Build.MODEL}".trim()
        val pairReq = PairRequestMessage(
            deviceName = deviceName,
            pairingPin = pin
        )
        val json = ProtocolJson.json.encodeToString(PairRequestMessage.serializer(), pairReq)
        webSocketClient.sendMessage(json)
    }

    fun startTyping(text: String, unit: SpeedUnit, speed: Int) {
        val token = sessionToken ?: run {
            _lastError.value = "Not paired. Please pair first."
            return
        }

        val startMsg = StartTypingMessage(
            sessionToken = token,
            text = text,
            speedUnit = unit,
            speedValue = speed
        )
        val json = ProtocolJson.json.encodeToString(StartTypingMessage.serializer(), startMsg)
        webSocketClient.sendMessage(json)
    }

    fun pauseTyping() {
        val token = sessionToken ?: return
        val pauseMsg = PauseTypingMessage(sessionToken = token)
        val json = ProtocolJson.json.encodeToString(PauseTypingMessage.serializer(), pauseMsg)
        webSocketClient.sendMessage(json)
    }

    fun resumeTyping() {
        val token = sessionToken ?: return
        val resumeMsg = ResumeTypingMessage(sessionToken = token)
        val json = ProtocolJson.json.encodeToString(ResumeTypingMessage.serializer(), resumeMsg)
        webSocketClient.sendMessage(json)
    }

    fun stopTyping() {
        val token = sessionToken ?: return
        val stopMsg = StopTypingMessage(sessionToken = token, reason = "User stopped from Android app")
        val json = ProtocolJson.json.encodeToString(StopTypingMessage.serializer(), stopMsg)
        webSocketClient.sendMessage(json)
    }

    fun emergencyStop() {
        val stopMsg = EmergencyStopMessage(sessionToken = sessionToken)
        val json = ProtocolJson.json.encodeToString(EmergencyStopMessage.serializer(), stopMsg)
        webSocketClient.sendMessage(json)
    }

    private fun handleIncomingMessage(json: String) {
        try {
            when (ProtocolJson.parseMessageType(json)) {
                "PairResponse" -> {
                    val resp = ProtocolJson.json.decodeFromString<PairResponseMessage>(json)
                    _pairResponse.value = resp
                    if (resp.success && resp.sessionToken != null) {
                        sessionToken = resp.sessionToken
                        _isPaired.value = true
                        _sessionState.value = _sessionState.value.copy(state = SessionState.Ready)
                        Log.i(tag, "Pairing successful. Session token received.")
                    } else {
                        _isPaired.value = false
                        _lastError.value = resp.errorMessage ?: "Pairing failed."
                        Log.w(tag, "Pairing failed: ${resp.errorMessage}")
                    }
                }
                "SessionState" -> {
                    val stateMsg = ProtocolJson.json.decodeFromString<SessionStateMessage>(json)
                    _sessionState.value = stateMsg
                }
                "DriverStatus" -> {
                    val driverMsg = ProtocolJson.json.decodeFromString<DriverStatusMessage>(json)
                    _driverStatus.value = driverMsg
                }
                "Error" -> {
                    val errorMsg = ProtocolJson.json.decodeFromString<ErrorMessage>(json)
                    _lastError.value = "[${errorMsg.errorCode}] ${errorMsg.errorMessage}"
                    Log.e(tag, "Server Error: ${errorMsg.errorMessage}")
                }
            }
        } catch (ex: Exception) {
            Log.e(tag, "Failed to decode incoming message: $json", ex)
        }
    }
}
