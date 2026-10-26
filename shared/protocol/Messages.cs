using System.Text.Json.Serialization;

namespace LANType.Protocol;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageType
{
    PairRequest,
    PairResponse,
    Heartbeat,
    StartTyping,
    PauseTyping,
    ResumeTyping,
    StopTyping,
    EmergencyStop,
    SessionState,
    DriverStatus,
    Error
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionState
{
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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpeedUnit
{
    Cpm,
    Wpm
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DriverState
{
    NotInstalled,
    InstalledStopped,
    ActiveEnumerated,
    DevReceiverFallback,
    Error
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PairRequestMessage), "PairRequest")]
[JsonDerivedType(typeof(PairResponseMessage), "PairResponse")]
[JsonDerivedType(typeof(HeartbeatMessage), "Heartbeat")]
[JsonDerivedType(typeof(StartTypingMessage), "StartTyping")]
[JsonDerivedType(typeof(PauseTypingMessage), "PauseTyping")]
[JsonDerivedType(typeof(ResumeTypingMessage), "ResumeTyping")]
[JsonDerivedType(typeof(StopTypingMessage), "StopTyping")]
[JsonDerivedType(typeof(EmergencyStopMessage), "EmergencyStop")]
[JsonDerivedType(typeof(SessionStateMessage), "SessionState")]
[JsonDerivedType(typeof(DriverStatusMessage), "DriverStatus")]
[JsonDerivedType(typeof(ErrorMessage), "Error")]
public abstract class LANTypeMessage
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = ProtocolConstants.CurrentProtocolVersion;
}

public class PairRequestMessage : LANTypeMessage
{
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonPropertyName("pairingPin")]
    public string PairingPin { get; set; } = string.Empty;

    [JsonPropertyName("clientVersion")]
    public string ClientVersion { get; set; } = string.Empty;
}

public class PairResponseMessage : LANTypeMessage
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("sessionToken")]
    public string? SessionToken { get; set; }

    [JsonPropertyName("serverVersion")]
    public string ServerVersion { get; set; } = ProtocolConstants.CurrentProtocolVersion;

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class HeartbeatMessage : LANTypeMessage
{
    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }

    [JsonPropertyName("isAck")]
    public bool IsAck { get; set; }
}

public class StartTypingMessage : LANTypeMessage
{
    [JsonPropertyName("sessionToken")]
    public string SessionToken { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("speedUnit")]
    public SpeedUnit SpeedUnit { get; set; } = SpeedUnit.Cpm;

    [JsonPropertyName("speedValue")]
    public int SpeedValue { get; set; } = ProtocolConstants.DefaultCpm;

    [JsonPropertyName("interKeyDelayMs")]
    public int? InterKeyDelayMs { get; set; }
}

public class PauseTypingMessage : LANTypeMessage
{
    [JsonPropertyName("sessionToken")]
    public string SessionToken { get; set; } = string.Empty;
}

public class ResumeTypingMessage : LANTypeMessage
{
    [JsonPropertyName("sessionToken")]
    public string SessionToken { get; set; } = string.Empty;
}

public class StopTypingMessage : LANTypeMessage
{
    [JsonPropertyName("sessionToken")]
    public string SessionToken { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public class EmergencyStopMessage : LANTypeMessage
{
    [JsonPropertyName("sessionToken")]
    public string? SessionToken { get; set; }
}

public class SessionStateMessage : LANTypeMessage
{
    [JsonPropertyName("state")]
    public SessionState State { get; set; } = SessionState.Disconnected;

    [JsonPropertyName("pairedDeviceName")]
    public string? PairedDeviceName { get; set; }

    [JsonPropertyName("totalCharacters")]
    public int TotalCharacters { get; set; }

    [JsonPropertyName("charactersTyped")]
    public int CharactersTyped { get; set; }

    [JsonPropertyName("charactersRemaining")]
    public int CharactersRemaining { get; set; }

    [JsonPropertyName("currentCpm")]
    public int CurrentCpm { get; set; }

    [JsonPropertyName("currentWpm")]
    public int CurrentWpm { get; set; }

    [JsonPropertyName("elapsedMilliseconds")]
    public long ElapsedMilliseconds { get; set; }

    [JsonPropertyName("estimatedRemainingMilliseconds")]
    public long EstimatedRemainingMilliseconds { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class DriverStatusMessage : LANTypeMessage
{
    [JsonPropertyName("state")]
    public DriverState State { get; set; } = DriverState.NotInstalled;

    [JsonPropertyName("isDeviceEnumerated")]
    public bool IsDeviceEnumerated { get; set; }

    [JsonPropertyName("devicePath")]
    public string? DevicePath { get; set; }

    [JsonPropertyName("isDevReceiverFallback")]
    public bool IsDevReceiverFallback { get; set; }

    [JsonPropertyName("statusDescription")]
    public string StatusDescription { get; set; } = string.Empty;
}

public class ErrorMessage : LANTypeMessage
{
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("errorMessage")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}
