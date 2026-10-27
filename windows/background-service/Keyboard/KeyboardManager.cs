using LANType.Protocol;
using Microsoft.Extensions.Logging;

namespace LANType.Service.Keyboard;

public class KeyboardManager : IHidKeyboardInterface
{
    private readonly ILogger<KeyboardManager> _logger;
    private readonly VhfDriverKeyboardInterface _vhfDriver;
    private readonly DevTestKeyboardReceiver _devReceiver;

    public bool IsConnected => _vhfDriver.IsConnected || _devReceiver.IsConnected;
    public bool IsDevFallback => !_vhfDriver.IsConnected;
    public string DeviceDescription => IsDevFallback 
        ? _devReceiver.DeviceDescription 
        : _vhfDriver.DeviceDescription;

    public KeyboardManager(
        ILogger<KeyboardManager> logger,
        VhfDriverKeyboardInterface vhfDriver,
        DevTestKeyboardReceiver devReceiver)
    {
        _logger = logger;
        _vhfDriver = vhfDriver;
        _devReceiver = devReceiver;

        if (_vhfDriver.IsConnected)
        {
            _logger.LogInformation("LANType initialized with genuine VHF Virtual HID driver.");
        }
        else
        {
            _logger.LogInformation("VHF driver not attached. Defaulting to LANType Dev Test Receiver fallback.");
        }
    }

    public async Task<bool> SendReportAsync(HidKeyboardReport report, CancellationToken cancellationToken = default)
    {
        if (_vhfDriver.IsConnected)
        {
            return await _vhfDriver.SendReportAsync(report, cancellationToken);
        }

        return await _devReceiver.SendReportAsync(report, cancellationToken);
    }

    public async Task<bool> SendUnicodeCharAsync(char c, CancellationToken cancellationToken = default)
    {
        return await _devReceiver.SendUnicodeCharAsync(c, cancellationToken);
    }

    public async Task<bool> ReleaseAllKeysAsync(CancellationToken cancellationToken = default)
    {
        bool driverOk = true;
        if (_vhfDriver.IsConnected)
        {
            driverOk = await _vhfDriver.ReleaseAllKeysAsync(cancellationToken);
        }

        bool devOk = await _devReceiver.ReleaseAllKeysAsync(cancellationToken);
        return driverOk && devOk;
    }

    public DriverStatusMessage GetStatus()
    {
        if (_vhfDriver.IsConnected)
        {
            return _vhfDriver.GetStatus();
        }

        // Check if driver device just became available
        if (_vhfDriver.TryOpenDevice())
        {
            return _vhfDriver.GetStatus();
        }

        return _devReceiver.GetStatus();
    }

    public void Dispose()
    {
        _vhfDriver.Dispose();
        _devReceiver.Dispose();
    }
}
