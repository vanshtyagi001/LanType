using System.ComponentModel;
using System.Runtime.CompilerServices;
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

    // Service & Driver Status
    private bool _isServiceRunning;
    private string _serviceStatusText = "Checking service...";
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
    private string _troubleshootingLogs = "Connecting to LANType background service...";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        _apiClient = new ServiceApiClient();

        // Commands
        RegeneratePinCommand = new RelayCommand(async () => await RegeneratePinAsync());
        StartManualTypingCommand = new RelayCommand(async () => await StartManualTypingAsync(), () => IsServiceRunning && !_isTypingActive);
        PauseTypingCommand = new RelayCommand(async () => await _apiClient.PauseTypingAsync(), () => IsServiceRunning && _isTypingActive);
        ResumeTypingCommand = new RelayCommand(async () => await _apiClient.ResumeTypingAsync(), () => IsServiceRunning && _sessionState == SessionState.Paused);
        StopTypingCommand = new RelayCommand(async () => await _apiClient.StopTypingAsync(), () => IsServiceRunning && _isTypingActive);
        EmergencyStopCommand = new RelayCommand(async () => await _apiClient.EmergencyStopAsync(), () => IsServiceRunning);
        RefreshStatusCommand = new RelayCommand(async () => await RefreshStatusAsync());

        // Polling Timer (1 second interval)
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _pollTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _pollTimer.Start();

        _ = RefreshStatusAsync();
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
        set
        {
            if (SetField(ref _currentCpm, value))
            {
                CurrentWpm = (int)Math.Round(value / 5.0);
            }
        }
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
    public RelayCommand StartManualTypingCommand { get; }
    public RelayCommand PauseTypingCommand { get; }
    public RelayCommand ResumeTypingCommand { get; }
    public RelayCommand StopTypingCommand { get; }
    public RelayCommand EmergencyStopCommand { get; }
    public RelayCommand RefreshStatusCommand { get; }

    #endregion

    public async Task RefreshStatusAsync()
    {
        var status = await _apiClient.GetStatusAsync();

        if (status == null)
        {
            IsServiceRunning = false;
            ServiceStatusText = "Service Stopped or Unreachable";
            ServiceStatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red #EF4444

            DriverStatusText = "Service Offline";
            DriverStatusColor = Brushes.Gray;
            StateBadgeText = "Offline";
            StateBadgeColor = Brushes.Gray;
            IsTypingActive = false;

            TroubleshootingLogs = $"[{DateTime.Now:T}] Could not reach background service at http://127.0.0.1:52444. Ensure LANType service is installed and running.\n" + TroubleshootingLogs;
            return;
        }

        // Service Online
        IsServiceRunning = true;
        ServiceStatusText = "Service Active (Port " + status.WebSocketPort + ")";
        ServiceStatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green #10B981

        HostIpAddress = status.HostIp;
        WebSocketPort = status.WebSocketPort;

        if (PairingPin != status.PairingPin)
        {
            PairingPin = status.PairingPin;
            PairingUri = status.PairingUri;
            QrCodeImage = QrCodeService.GenerateQrCodeImage(status.PairingUri);
        }

        // Driver Status
        IsDriverConnected = status.DriverStatus.IsDeviceEnumerated;
        IsDevFallback = status.DriverStatus.IsDevReceiverFallback;

        if (status.DriverStatus.IsDeviceEnumerated)
        {
            DriverStatusText = "Genuine KMDF Virtual HID Keyboard Active";
            DriverStatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
        }
        else if (status.DriverStatus.IsDevReceiverFallback)
        {
            DriverStatusText = "Dev Receiver Fallback (Driver Not Attached)";
            DriverStatusColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber #F59E0B
        }
        else
        {
            DriverStatusText = "Driver Not Installed";
            DriverStatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
        }

        // Client & Session
        IsClientConnected = status.IsClientConnected;
        PairedDeviceName = string.IsNullOrEmpty(status.SessionState.PairedDeviceName) 
            ? "No phone paired" 
            : status.SessionState.PairedDeviceName;

        SessionState = status.SessionState.State;
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

        CharactersTyped = status.SessionState.CharactersTyped;
        TotalCharacters = status.SessionState.TotalCharacters;
        CharactersRemaining = status.SessionState.CharactersRemaining;

        ProgressPercentage = TotalCharacters > 0 
            ? Math.Round((double)CharactersTyped / TotalCharacters * 100.0, 1) 
            : 0;

        CurrentCpm = status.SessionState.CurrentCpm;
        CurrentWpm = status.SessionState.CurrentWpm;

        var remainingSpan = TimeSpan.FromMilliseconds(status.SessionState.EstimatedRemainingMilliseconds);
        EstimatedRemainingText = $"{remainingSpan.Minutes:D2}:{remainingSpan.Seconds:D2}";

        var elapsedSpan = TimeSpan.FromMilliseconds(status.SessionState.ElapsedMilliseconds);
        ElapsedTimeText = $"{elapsedSpan.Minutes:D2}:{elapsedSpan.Seconds:D2}";
    }

    private async Task RegeneratePinAsync()
    {
        bool ok = await _apiClient.RegeneratePinAsync();
        if (ok)
        {
            await RefreshStatusAsync();
        }
    }

    private async Task StartManualTypingAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualTestText)) return;

        // Give the user 3 seconds to focus the target application (e.g. Notepad)
        TroubleshootingLogs = $"[{DateTime.Now:T}] Manual typing starting in 3 seconds... Please focus your target application!\n" + TroubleshootingLogs;
        await Task.Delay(3000);

        await _apiClient.StartTypingAsync(ManualTestText, SpeedUnit.Cpm, ManualSpeedCpm);
        await RefreshStatusAsync();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
