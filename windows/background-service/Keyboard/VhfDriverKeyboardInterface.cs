using System.Runtime.InteropServices;
using LANType.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace LANType.Service.Keyboard;

/// <summary>
/// Communicates with the KMDF Virtual HID Keyboard driver via IOCTL.
/// Presents genuine hardware-level keyboard reports through Microsoft VHF.
/// </summary>
public sealed class VhfDriverKeyboardInterface : IHidKeyboardInterface
{
    private readonly ILogger<VhfDriverKeyboardInterface> _logger;
    private SafeFileHandle? _deviceHandle;
    private readonly object _syncLock = new();

    // CTL_CODE(FILE_DEVICE_KEYBOARD (0x0B), Function, Method (0), Access)
    public const uint IOCTL_LANTYPE_SEND_REPORT = 0x000BA004;
    public const uint IOCTL_LANTYPE_RESET_KEYS  = 0x000BA008;
    public const uint IOCTL_LANTYPE_GET_STATUS  = 0x000B600C;

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        byte[]? lpInBuffer,
        uint nInBufferSize,
        byte[]? lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    public bool IsConnected
    {
        get
        {
            lock (_syncLock)
            {
                return _deviceHandle != null && !_deviceHandle.IsInvalid && !_deviceHandle.IsClosed;
            }
        }
    }

    public bool IsDevFallback => false;
    public string DeviceDescription => "Windows VHF Virtual HID Keyboard Driver";

    public VhfDriverKeyboardInterface(ILogger<VhfDriverKeyboardInterface> logger)
    {
        _logger = logger;
        TryOpenDevice();
    }

    public bool TryOpenDevice()
    {
        lock (_syncLock)
        {
            if (IsConnected)
            {
                return true;
            }

            try
            {
                var handle = CreateFile(
                    ProtocolConstants.DeviceSymlinkName,
                    GENERIC_READ | GENERIC_WRITE,
                    0,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero);

                if (!handle.IsInvalid)
                {
                    _deviceHandle = handle;
                    _logger.LogInformation("Successfully opened connection to LANType Virtual HID Driver ({Symlink})", 
                        ProtocolConstants.DeviceSymlinkName);
                    return true;
                }

                int lastError = Marshal.GetLastWin32Error();
                _logger.LogDebug("Virtual HID Driver device not accessible at {Symlink} (Win32 Error: {Error})", 
                    ProtocolConstants.DeviceSymlinkName, lastError);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while attempting to connect to Virtual HID Driver");
            }

            return false;
        }
    }

    public Task<bool> SendReportAsync(HidKeyboardReport report, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_syncLock)
            {
                if (!IsConnected && !TryOpenDevice())
                {
                    _logger.LogWarning("Cannot send HID report: Virtual HID driver is not connected.");
                    return false;
                }

                byte[] reportBytes = report.ToByteArray();
                bool success = DeviceIoControl(
                    _deviceHandle!,
                    IOCTL_LANTYPE_SEND_REPORT,
                    reportBytes,
                    (uint)reportBytes.Length,
                    null,
                    0,
                    out _,
                    IntPtr.Zero);

                if (!success)
                {
                    int err = Marshal.GetLastWin32Error();
                    _logger.LogError("DeviceIoControl IOCTL_LANTYPE_SEND_REPORT failed with Win32 error {ErrorCode}", err);
                    CloseDeviceInternal();
                    return false;
                }

                return true;
            }
        }, cancellationToken);
    }

    public Task<bool> ReleaseAllKeysAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_syncLock)
            {
                if (!IsConnected && !TryOpenDevice())
                {
                    return false;
                }

                bool success = DeviceIoControl(
                    _deviceHandle!,
                    IOCTL_LANTYPE_RESET_KEYS,
                    null,
                    0,
                    null,
                    0,
                    out _,
                    IntPtr.Zero);

                if (!success)
                {
                    int err = Marshal.GetLastWin32Error();
                    _logger.LogError("DeviceIoControl IOCTL_LANTYPE_RESET_KEYS failed with Win32 error {ErrorCode}", err);
                    CloseDeviceInternal();
                    return false;
                }

                return true;
            }
        }, cancellationToken);
    }

    public DriverStatusMessage GetStatus()
    {
        bool connected = IsConnected || TryOpenDevice();
        return new DriverStatusMessage
        {
            State = connected ? DriverState.ActiveEnumerated : DriverState.NotInstalled,
            IsDeviceEnumerated = connected,
            DevicePath = connected ? ProtocolConstants.DeviceSymlinkName : null,
            IsDevReceiverFallback = false,
            StatusDescription = connected 
                ? "Virtual HID Driver is connected and ready." 
                : "Virtual HID Driver device not found or not loaded in Windows Device Manager."
        };
    }

    private void CloseDeviceInternal()
    {
        if (_deviceHandle != null)
        {
            if (!_deviceHandle.IsClosed)
            {
                _deviceHandle.Close();
            }
            _deviceHandle = null;
        }
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            CloseDeviceInternal();
        }
    }
}
