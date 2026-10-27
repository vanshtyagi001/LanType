using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LANType.Desktop.Services;
using LANType.Protocol;

namespace LANType.Desktop.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ServiceApiClient _apiClient;
    private readonly DispatcherTimer _pollTimer;
    private EmbeddedEngineHost? _embeddedHost;

    // Service & Driver Status
    private bool _isServiceRunning;
    private string _serviceStatusText = "Initializing...";
    private Brush _serviceStatusColor = Brushes.Gray;
    private bool _isDriverConnected;
    private bool _isDevFallback;
    private string _driverStatusText = "Checking driver...";
    private Brush _driverStatusColor = Brushes.Gray;

    // Network & Pairing
    private string _hostIpAddress = "127.0.0.1";
    private int _webSocketPort = ProtocolConstants.DefaultWebSocketPort;
    private string _pairingPin = "------";
    private string _pairingUri = string.Empty;
    private BitmapImage? _qrCodeImage;

    // Paired Client
    private bool _isClientConnected;
    private string _pairedDeviceName = "No phone connected";

    // Session State & Progress
    private SessionState _sessionState = SessionState.Disconnected;
    private string _stateBadgeText = "Disconnected";
    private Brush _stateBadgeColor = Brushes.Gray;
    private int _charactersTyped;
    private int _totalCharacters;
    private int _charactersRemaining;
    private double _progressPercentage;
    private int _currentCpm = 300;
    private int _currentWpm = 60;
    private string _estimatedRemainingText = "00:00";
    private string _elapsedTimeText = "00:00";

    // Manual Typing Test
    private string _manualTestText = "The quick brown fox jumps over the lazy dog 123!";
    private int _manualSpeedCpm = 300;
    private bool _isTypingActive;

    // Troubleshooting
    private string _troubleshootingLogs = "LANType application starting in active user session...\n";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        _apiClient = new ServiceApiClient();

        // 1. Start the embedded typing engine inside this application!
        TryStartEmbeddedEngine();

        // 2. Initialize Commands
        RegeneratePinCommand = new RelayCommand(async () => await RegeneratePinAsync());
        RegisterDriverCommand = new RelayCommand(() => RegisterKernelDriver());
        StartManualTypingCommand = new RelayCommand(async () => await StartManualTypingAsync(), () => IsServiceRunning && !_isTypingActive);
        PauseTypingCommand = new RelayCommand(() => PauseTyping(), () => IsServiceRunning && _isTypingActive);
        ResumeTypingCommand = new RelayCommand(() => ResumeTyping(), () => IsServiceRunning && _sessionState == SessionState.Paused);
        StopTypingCommand = new RelayCommand(() => StopTyping(), () => IsServiceRunning && _isTypingActive);
        EmergencyStopCommand = new RelayCommand(async () => await EmergencyStopAsync(), () => IsServiceRunning);
        RefreshStatusCommand = new RelayCommand(async () => await RefreshStatusAsync());

        // Polling / Heartbeat Timer (1 second interval)
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _pollTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _pollTimer.Start();

        _ = RefreshStatusAsync();
    }

    private void TryStartEmbeddedEngine()
    {
        try
        {
            _embeddedHost = new EmbeddedEngineHost();
            _embeddedHost.Start();
            _embeddedHost.StateChanged += msg => App.Current.Dispatcher.Invoke(() => ApplySessionState(msg));
            _embeddedHost.PinRegenerated += pin => App.Current.Dispatcher.Invoke(() =>
            {
                PairingPin = pin;
                PairingUri = _embeddedHost.GetPairingUri();
                QrCodeImage = QrCodeService.GenerateQrCodeImage(PairingUri);
            });

            TroubleshootingLogs = $"[{DateTime.Now:T}] Embedded typing engine & WebSocket server active on port {ProtocolConstants.DefaultWebSocketPort}.\n" + TroubleshootingLogs;
        }
        catch (Exception ex)
        {
            TroubleshootingLogs = $"[{DateTime.Now:T}] Embedded engine notice: {ex.Message}. Falling back to external service IPC.\n" + TroubleshootingLogs;
            _embeddedHost = null;
        }
    }

    #region Properties

    public bool IsServiceRunning
    {
        get => _isServiceRunning;
        set => SetField(ref _isServiceRunning, value);
    }

    public string ServiceStatusText
    {
        get => _serviceStatusText;
        set => SetField(ref _serviceStatusText, value);
    }

    public Brush ServiceStatusColor
    {
        get => _serviceStatusColor;
        set => SetField(ref _serviceStatusColor, value);
    }

    public bool IsDriverConnected
    {
        get => _isDriverConnected;
        set => SetField(ref _isDriverConnected, value);
    }

    public bool IsDevFallback
    {
        get => _isDevFallback;
        set => SetField(ref _isDevFallback, value);
    }

    public string DriverStatusText
    {
        get => _driverStatusText;
        set => SetField(ref _driverStatusText, value);
    }

    public Brush DriverStatusColor
    {
        get => _driverStatusColor;
        set => SetField(ref _driverStatusColor, value);
    }

    public string HostIpAddress
    {
        get => _hostIpAddress;
        set => SetField(ref _hostIpAddress, value);
    }

    public int WebSocketPort
    {
        get => _webSocketPort;
        set => SetField(ref _webSocketPort, value);
    }

    public string PairingPin
    {
        get => _pairingPin;
        set => SetField(ref _pairingPin, value);
    }

    public string PairingUri
    {
        get => _pairingUri;
        set => SetField(ref _pairingUri, value);
    }

    public BitmapImage? QrCodeImage
    {
        get => _qrCodeImage;
        set => SetField(ref _qrCodeImage, value);
    }

    public bool IsClientConnected
    {
        get => _isClientConnected;
        set => SetField(ref _isClientConnected, value);
    }

    public string PairedDeviceName
    {
        get => _pairedDeviceName;
        set => SetField(ref _pairedDeviceName, value);
    }

    public SessionState SessionState
    {
        get => _sessionState;
        set => SetField(ref _sessionState, value);
    }

    public string StateBadgeText
    {
        get => _stateBadgeText;
        set => SetField(ref _stateBadgeText, value);
    }

    public Brush StateBadgeColor
    {
        get => _stateBadgeColor;
        set => SetField(ref _stateBadgeColor, value);
    }

    public int CharactersTyped
    {
        get => _charactersTyped;
        set => SetField(ref _charactersTyped, value);
    }

    public int TotalCharacters
    {
        get => _totalCharacters;
        set => SetField(ref _totalCharacters, value);
    }

    public int CharactersRemaining
    {
        get => _charactersRemaining;
        set => SetField(ref _charactersRemaining, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetField(ref _progressPercentage, value);
    }

    public int CurrentCpm
    {
        get => _currentCpm;
        set => SetField(ref _currentCpm, value);
    }

    public int CurrentWpm
    {
        get => _currentWpm;
        set => SetField(ref _currentWpm, value);
    }

    public string EstimatedRemainingText
    {
        get => _estimatedRemainingText;
        set => SetField(ref _estimatedRemainingText, value);
    }

    public string ElapsedTimeText
    {
        get => _elapsedTimeText;
        set => SetField(ref _elapsedTimeText, value);
    }

    public string ManualTestText
    {
        get => _manualTestText;
        set => SetField(ref _manualTestText, value);
    }

    public int ManualSpeedCpm
    {
        get => _manualSpeedCpm;
        set => SetField(ref _manualSpeedCpm, value);
    }

    public bool IsTypingActive
    {
        get => _isTypingActive;
        set => SetField(ref _isTypingActive, value);
    }

    public string TroubleshootingLogs
    {
        get => _troubleshootingLogs;
        set => SetField(ref _troubleshootingLogs, value);
    }

    #endregion

    #region Commands

    public RelayCommand RegeneratePinCommand { get; }
    public RelayCommand RegisterDriverCommand { get; }
    public RelayCommand StartManualTypingCommand { get; }
    public RelayCommand PauseTypingCommand { get; }
    public RelayCommand ResumeTypingCommand { get; }
    public RelayCommand StopTypingCommand { get; }
    public RelayCommand EmergencyStopCommand { get; }
    public RelayCommand RefreshStatusCommand { get; }

    #endregion

    public async Task RefreshStatusAsync()
    {
        // 1. If embedded engine is active, pull state directly from in-process engine
        if (_embeddedHost != null && _embeddedHost.IsRunning)
        {
            IsServiceRunning = true;
            ServiceStatusText = "Integrated Engine Active (Port " + ProtocolConstants.DefaultWebSocketPort + ")";
            ServiceStatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green #10B981

            HostIpAddress = _embeddedHost.GetHostIp().ToString();
            WebSocketPort = ProtocolConstants.DefaultWebSocketPort;

            var pin = _embeddedHost.PairingManager.CurrentPin;
            if (PairingPin != pin)
            {
                PairingPin = pin;
                PairingUri = _embeddedHost.GetPairingUri();
                QrCodeImage = QrCodeService.GenerateQrCodeImage(PairingUri);
            }

            var driverStatus = _embeddedHost.GetDriverStatus();
            IsDriverConnected = driverStatus.IsDeviceEnumerated;
            IsDevFallback = driverStatus.IsDevReceiverFallback;

            if (driverStatus.IsDeviceEnumerated)
            {
                DriverStatusText = "Genuine KMDF Virtual HID Keyboard Active";
                DriverStatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            else
            {
                DriverStatusText = "User-Mode Input Active (SendInput Desktop Engine)";
                DriverStatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
            }

            var sessionMsg = _embeddedHost.TypingScheduler.GetCurrentStateMessage();
            ApplySessionState(sessionMsg);
            return;
        }

        // 2. Fallback to external background service IPC if embedded engine is not running
        var status = await _apiClient.GetStatusAsync();

        if (status == null)
        {
            IsServiceRunning = false;
            ServiceStatusText = "Engine Initializing...";
            ServiceStatusColor = Brushes.Gray;
            return;
        }

        IsServiceRunning = true;
        ServiceStatusText = "Service Active (Port " + status.WebSocketPort + ")";
        ServiceStatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129));

        HostIpAddress = status.HostIp;
        WebSocketPort = status.WebSocketPort;

        if (PairingPin != status.PairingPin)
        {
            PairingPin = status.PairingPin;
            PairingUri = status.PairingUri;
            QrCodeImage = QrCodeService.GenerateQrCodeImage(status.PairingUri);
        }

        IsDriverConnected = status.DriverStatus.IsDeviceEnumerated;
        IsDevFallback = status.DriverStatus.IsDevReceiverFallback;

        if (status.DriverStatus.IsDeviceEnumerated)
        {
            DriverStatusText = "Genuine KMDF Virtual HID Keyboard Active";
            DriverStatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        else
        {
            DriverStatusText = "User-Mode Input Active (SendInput Desktop Engine)";
            DriverStatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }

        IsClientConnected = status.IsClientConnected;
        PairedDeviceName = string.IsNullOrEmpty(status.SessionState.PairedDeviceName)
            ? "No phone paired"
            : status.SessionState.PairedDeviceName;

        ApplySessionState(status.SessionState);
    }

    private void ApplySessionState(SessionStateMessage state)
    {
        SessionState = state.State;
        IsTypingActive = (SessionState == SessionState.Typing);

        switch (SessionState)
        {
            case SessionState.Typing:
                StateBadgeText = "Typing In Progress";
                StateBadgeColor = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Blue #3B82F6
                break;
            case SessionState.Paused:
                StateBadgeText = "Typing Paused";
                StateBadgeColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                break;
            case SessionState.Completed:
                StateBadgeText = "Completed";
                StateBadgeColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                break;
            case SessionState.Stopped:
                StateBadgeText = "Stopped";
                StateBadgeColor = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray
                break;
            case SessionState.Ready:
                StateBadgeText = "Ready";
                StateBadgeColor = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                break;
            default:
                StateBadgeText = SessionState.ToString();
                StateBadgeColor = Brushes.Gray;
                break;
        }

        CharactersTyped = state.CharactersTyped;
        TotalCharacters = state.TotalCharacters;
        CharactersRemaining = state.CharactersRemaining;

        ProgressPercentage = TotalCharacters > 0
            ? Math.Round((double)CharactersTyped / TotalCharacters * 100.0, 1)
            : 0;

        CurrentCpm = state.CurrentCpm;
        CurrentWpm = state.CurrentWpm;

        var remainingSpan = TimeSpan.FromMilliseconds(state.EstimatedRemainingMilliseconds);
        EstimatedRemainingText = $"{remainingSpan.Minutes:D2}:{remainingSpan.Seconds:D2}";

        var elapsedSpan = TimeSpan.FromMilliseconds(state.ElapsedMilliseconds);
        ElapsedTimeText = $"{elapsedSpan.Minutes:D2}:{elapsedSpan.Seconds:D2}";
    }

    private async Task RegeneratePinAsync()
    {
        if (_embeddedHost != null)
        {
            _embeddedHost.RegeneratePin();
            await RefreshStatusAsync();
            return;
        }

        bool ok = await _apiClient.RegeneratePinAsync();
        if (ok)
        {
            await RefreshStatusAsync();
        }
    }

    private void RegisterKernelDriver()
    {
        TroubleshootingLogs = $"[{DateTime.Now:T}] Attempting to register kernel driver package...\n" + TroubleshootingLogs;
        var (success, msg) = EmbeddedEngineHost.RegisterKernelDriver();
        TroubleshootingLogs = $"[{DateTime.Now:T}] Driver Registration Result: {msg}\n" + TroubleshootingLogs;
    }

    private async Task StartManualTypingAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualTestText)) return;

        string normalized = ManualTestText.Replace("\r\n", "\n").Replace('\r', '\n');

        for (int c = 5; c >= 1; c--)
        {
            TroubleshootingLogs = $"[{DateTime.Now:T}] Switch to CodeTantra / Kiosk editor! Starting in {c}s...\n" + TroubleshootingLogs;
            await Task.Delay(1000);
        }

        TroubleshootingLogs = $"[{DateTime.Now:T}] Typing started with Hardware OEM Scancodes!\n" + TroubleshootingLogs;

        if (_embeddedHost != null)
        {
            var msg = new StartTypingMessage
            {
                Text = normalized,
                SpeedUnit = SpeedUnit.Cpm,
                SpeedValue = ManualSpeedCpm
            };
            _embeddedHost.TypingScheduler.Start(msg, out var err);
            if (!string.IsNullOrEmpty(err))
            {
                TroubleshootingLogs = $"[{DateTime.Now:T}] Manual typing error: {err}\n" + TroubleshootingLogs;
            }
            await RefreshStatusAsync();
            return;
        }

        await _apiClient.StartTypingAsync(ManualTestText, SpeedUnit.Cpm, ManualSpeedCpm);
        await RefreshStatusAsync();
    }

    private void PauseTyping()
    {
        if (_embeddedHost != null)
        {
            _embeddedHost.TypingScheduler.Pause(out _);
            return;
        }
        _ = _apiClient.PauseTypingAsync();
    }

    private void ResumeTyping()
    {
        if (_embeddedHost != null)
        {
            _embeddedHost.TypingScheduler.Resume(out _);
            return;
        }
        _ = _apiClient.ResumeTypingAsync();
    }

    private void StopTyping()
    {
        if (_embeddedHost != null)
        {
            _embeddedHost.TypingScheduler.Stop("User stopped from control panel", out _);
            return;
        }
        _ = _apiClient.StopTypingAsync();
    }

    private async Task EmergencyStopAsync()
    {
        if (_embeddedHost != null)
        {
            await _embeddedHost.TypingScheduler.EmergencyStopAsync();
            return;
        }
        await _apiClient.EmergencyStopAsync();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
