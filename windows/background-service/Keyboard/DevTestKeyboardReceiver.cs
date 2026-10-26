using System.Collections.Concurrent;
using LANType.Protocol;
using Microsoft.Extensions.Logging;

namespace LANType.Service.Keyboard;

/// <summary>
/// Development-only test receiver for testing the network, scheduling, and protocol pipelines
/// without requiring an active signed KMDF kernel driver.
/// Explicitly labeled as DevReceiverFallback in telemetry.
/// </summary>
public sealed class DevTestKeyboardReceiver : IHidKeyboardInterface
{
    private readonly ILogger<DevTestKeyboardReceiver> _logger;
    private readonly ConcurrentQueue<HidKeyboardReport> _receivedReports = new();
    private long _totalReportsSent;
    private long _totalKeysPressed;

    public bool IsConnected => true;
    public bool IsDevFallback => true;
    public string DeviceDescription => "LANType In-Memory Dev Receiver (Test Fallback)";

    public long TotalReportsSent => Interlocked.Read(ref _totalReportsSent);
    public long TotalKeysPressed => Interlocked.Read(ref _totalKeysPressed);

    public DevTestKeyboardReceiver(ILogger<DevTestKeyboardReceiver> logger)
    {
        _logger = logger;
        _logger.LogInformation("Initialized LANType Development Test Keyboard Receiver.");
    }

    public Task<bool> SendReportAsync(HidKeyboardReport report, CancellationToken cancellationToken = default)
    {
        _receivedReports.Enqueue(report);
        Interlocked.Increment(ref _totalReportsSent);

        if (!report.IsEmpty && report.Key1 != 0)
        {
            Interlocked.Increment(ref _totalKeysPressed);
        }

        // Limit in-memory queue size
        while (_receivedReports.Count > 1000)
        {
            _receivedReports.TryDequeue(out _);
        }

        return Task.FromResult(true);
    }

    public Task<bool> ReleaseAllKeysAsync(CancellationToken cancellationToken = default)
    {
        return SendReportAsync(HidKeyboardReport.Empty, cancellationToken);
    }

    public DriverStatusMessage GetStatus()
    {
        return new DriverStatusMessage
        {
            State = DriverState.DevReceiverFallback,
            IsDeviceEnumerated = false,
            DevicePath = null,
            IsDevReceiverFallback = true,
            StatusDescription = "Using development-only test receiver fallback. Virtual HID driver is not attached."
        };
    }

    public void ClearHistory()
    {
        _receivedReports.Clear();
        Interlocked.Exchange(ref _totalReportsSent, 0);
        Interlocked.Exchange(ref _totalKeysPressed, 0);
    }

    public void Dispose()
    {
        _receivedReports.Clear();
    }
}
