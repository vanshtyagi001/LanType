using LANType.Protocol;

namespace LANType.Service.Keyboard;

public interface IHidKeyboardInterface : IDisposable
{
    bool IsConnected { get; }
    bool IsDevFallback { get; }
    string DeviceDescription { get; }

    Task<bool> SendReportAsync(HidKeyboardReport report, CancellationToken cancellationToken = default);
    Task<bool> SendUnicodeCharAsync(char c, CancellationToken cancellationToken = default);
    Task<bool> ReleaseAllKeysAsync(CancellationToken cancellationToken = default);
    DriverStatusMessage GetStatus();
}
