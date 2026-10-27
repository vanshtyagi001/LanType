using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LANType.Protocol;
using LANType.Service.Auth;
using LANType.Service.Engine;
using LANType.Service.Keyboard;
using LANType.Service.Network;
using LANType.Service.Server;
using Microsoft.Extensions.Logging;

namespace LANType.Desktop.Services;

/// <summary>
/// Embeds the entire LANType typing engine, WebSocket server, and UDP discovery
/// directly inside the desktop application. Eliminates the need for any separate
/// command line windows or external background service processes.
/// </summary>
public sealed class EmbeddedEngineHost : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<EmbeddedEngineHost> _logger;

    public PairingManager PairingManager { get; }
    public KeyboardManager KeyboardManager { get; }
    public TypingScheduler TypingScheduler { get; }
    public NetworkInterfaceManager NetworkManager { get; }
    public LANWebSocketServer WebSocketServer { get; }
    public UdpDiscoveryService UdpDiscovery { get; }
    public VhfDriverKeyboardInterface VhfDriver { get; }
    public DevTestKeyboardReceiver DevReceiver { get; }

    public bool IsRunning { get; private set; }

    public event Action<SessionStateMessage>? StateChanged;
    public event Action<string>? PinRegenerated;

    public EmbeddedEngineHost()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        _logger = _loggerFactory.CreateLogger<EmbeddedEngineHost>();

        var devLogger = _loggerFactory.CreateLogger<DevTestKeyboardReceiver>();
        DevReceiver = new DevTestKeyboardReceiver(devLogger);

        var vhfLogger = _loggerFactory.CreateLogger<VhfDriverKeyboardInterface>();
        VhfDriver = new VhfDriverKeyboardInterface(vhfLogger);

        var kbLogger = _loggerFactory.CreateLogger<KeyboardManager>();
        KeyboardManager = new KeyboardManager(kbLogger, VhfDriver, DevReceiver);

        var pairLogger = _loggerFactory.CreateLogger<PairingManager>();
        PairingManager = new PairingManager(pairLogger);

        var schedLogger = _loggerFactory.CreateLogger<TypingScheduler>();
        TypingScheduler = new TypingScheduler(schedLogger, KeyboardManager);

        var netLogger = _loggerFactory.CreateLogger<NetworkInterfaceManager>();
        NetworkManager = new NetworkInterfaceManager(netLogger);

        var wsLogger = _loggerFactory.CreateLogger<LANWebSocketServer>();
        WebSocketServer = new LANWebSocketServer(wsLogger, PairingManager, TypingScheduler, ProtocolConstants.DefaultWebSocketPort);

        var udpLogger = _loggerFactory.CreateLogger<UdpDiscoveryService>();
        UdpDiscovery = new UdpDiscoveryService(udpLogger, NetworkManager, ProtocolConstants.DefaultWebSocketPort);

        // Hook internal events
        TypingScheduler.StateChanged += msg => StateChanged?.Invoke(msg);
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            WebSocketServer.Start();
            UdpDiscovery.Start();
            IsRunning = true;
            _logger.LogInformation("Embedded LANType engine started successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start embedded LANType engine.");
            throw;
        }
    }

    public void RegeneratePin()
    {
        PairingManager.GenerateNewPin();
        PinRegenerated?.Invoke(PairingManager.CurrentPin);
    }

    public string GetPairingUri()
    {
        var ip = NetworkManager.GetPreferredEndpointAddress();
        return PairingManager.GetPairingUri(ip.ToString(), ProtocolConstants.DefaultWebSocketPort);
    }

    public IPAddress GetHostIp()
    {
        return NetworkManager.GetPreferredEndpointAddress();
    }

    public DriverStatusMessage GetDriverStatus()
    {
        return KeyboardManager.GetStatus();
    }

    /// <summary>
    /// Attempts to register the Virtual HID kernel driver with Windows.
    /// </summary>
    public static (bool Success, string Message) RegisterKernelDriver()
    {
        if (!OperatingSystem.IsWindows())
        {
            return (false, "Kernel driver registration is only supported on Windows.");
        }

        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidateInfPaths = new[]
            {
                Path.Combine(baseDir, "Driver", "LANTypeVhid.inf"),
                Path.Combine(baseDir, "LANTypeVhid.inf"),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\hid-driver\LANTypeVhid.inf")),
                @"C:\Program Files\LANType\Driver\LANTypeVhid.inf"
            };

            string? infPath = candidateInfPaths.FirstOrDefault(File.Exists);
            if (infPath == null)
            {
                return (false, "Driver INF package not found in application directory or system driver path.");
            }

            // Attempt to install INF via pnputil
            if (File.Exists(infPath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = $"/add-driver \"{infPath}\" /install",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                try
                {
                    var proc = Process.Start(psi);
                    proc?.WaitForExit(10000);
                    if (proc != null && proc.ExitCode == 0)
                    {
                        return (true, "Kernel driver INF package successfully registered with Windows PnP.");
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"Driver installation request failed: {ex.Message}");
                }
            }

            return (false, "Driver file found, but requires Administrator elevation or Windows Test Signing mode to attach.");
        }
        catch (Exception ex)
        {
            return (false, $"Error registering kernel driver: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            UdpDiscovery.Dispose();
            WebSocketServer.Dispose();
            TypingScheduler.Dispose();
            KeyboardManager.Dispose();
            _loggerFactory.Dispose();
        }
        catch { }

        IsRunning = false;
    }
}
