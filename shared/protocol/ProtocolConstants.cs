namespace LANType.Protocol;

public static class ProtocolConstants
{
    public const string CurrentProtocolVersion = "1.0";
    public const int DefaultWebSocketPort = 52443;
    public const int DefaultIpcPort = 52444;
    public const string NamedPipeName = "LANTypeServicePipe";

    // Pairing configuration
    public const int PairingPinLength = 6;
    public const int SessionTokenExpiryMinutes = 120;
    public const int MaxFailedPairingAttempts = 5;

    // Typing limits
    public const int MinCpm = 30;
    public const int MaxCpm = 3000;
    public const int DefaultCpm = 300;

    public const int MinWpm = 6;
    public const int MaxWpm = 600;
    public const int DefaultWpm = 60;

    public const int MaxPayloadCharacterLength = 500_000;

    // HID Report Constants
    public const int StandardKeyboardReportLength = 8;
    public const string HidDeviceInterfaceGuid = "{7A5B536E-6F31-4F98-84CE-D790BC914C7A}";
    public const string DeviceSymlinkName = @"\\.\LANTypeVhidDevice";
}
