package com.lantype.app.data.model

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive

enum class SpeedUnit {
    @SerialName("Cpm") Cpm,
    @SerialName("Wpm") Wpm
}

enum class SessionState {
    Disconnected,
    Connecting,
    Connected,
    Paired,
    Ready,
    Typing,
    Paused,
    Stopped,
    Completed,
    Error,
    Reconnecting
}

enum class DriverState {
    NotInstalled,
    InstalledStopped,
    ActiveEnumerated,
    DevReceiverFallback,
    Error
}

@Serializable
data class PairRequestMessage(
    val type: String = "PairRequest",
    val messageId: String = java.util.UUID.randomUUID().toString(),
    val timestamp: String = java.time.Instant.now().toString(),
    val protocolVersion: String = "1.0",
    val deviceName: String,
    val pairingPin: String,
    val clientVersion: String = "1.0.0"
)

@Serializable
data class PairResponseMessage(
    val type: String = "PairResponse",
    val messageId: String? = null,
    val success: Boolean = false,
    val sessionToken: String? = null,
    val serverVersion: String = "1.0",
    val errorMessage: String? = null
)

@Serializable
data class HeartbeatMessage(
    val type: String = "Heartbeat",
    val messageId: String = java.util.UUID.randomUUID().toString(),
    val sequence: Long = 0,
    val isAck: Boolean = false
)

@Serializable
data class StartTypingMessage(
    val type: String = "StartTyping",
    val messageId: String = java.util.UUID.randomUUID().toString(),
    val sessionToken: String,
    val text: String,
    val speedUnit: SpeedUnit = SpeedUnit.Cpm,
    val speedValue: Int = 300,
    val interKeyDelayMs: Int? = null
)

@Serializable
data class PauseTypingMessage(
    val type: String = "PauseTyping",
    val messageId: String = java.util.UUID.randomUUID().toString(),
    val sessionToken: String
)

@Serializable
data class ResumeTypingMessage(
    val type: String = "ResumeTyping",
    val messageId: String = java.util.UUID.randomUUID().toString(),
    val sessionToken: String
)

@Serializable
data class StopTypingMessage(
    val type: String = "StopTyping",
    val messageId: String = java.util.UUID.randomUUID().toString(),
    val sessionToken: String,
    val reason: String? = null
)

@Serializable
data class EmergencyStopMessage(
    val type: String = "EmergencyStop",
    val messageId: String = java.util.UUID.randomUUID().toString(),
    val sessionToken: String? = null
)

@Serializable
data class SessionStateMessage(
    val type: String = "SessionState",
    val state: SessionState = SessionState.Disconnected,
    val pairedDeviceName: String? = null,
    val totalCharacters: Int = 0,
    val charactersTyped: Int = 0,
    val charactersRemaining: Int = 0,
    val currentCpm: Int = 300,
    val currentWpm: Int = 60,
    val elapsedMilliseconds: Long = 0,
    val estimatedRemainingMilliseconds: Long = 0,
    val errorMessage: String? = null
)

@Serializable
data class DriverStatusMessage(
    val type: String = "DriverStatus",
    val state: DriverState = DriverState.NotInstalled,
    val isDeviceEnumerated: Boolean = false,
    val devicePath: String? = null,
    val isDevReceiverFallback: Boolean = false,
    val statusDescription: String = ""
)

@Serializable
data class ErrorMessage(
    val type: String = "Error",
    val errorCode: String = "",
    val errorMessage: String = "",
    val detail: String? = null
)

object ProtocolJson {
    val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        encodeDefaults = true
    }

    fun parseMessageType(jsonStr: String): String? {
        return try {
            val element = json.parseToJsonElement(jsonStr) as? JsonObject
            element?.get("type")?.jsonPrimitive?.content
        } catch (_: Exception) {
            null
        }
    }
}
