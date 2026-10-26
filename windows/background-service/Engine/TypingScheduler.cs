using System.Diagnostics;
using LANType.Protocol;
using LANType.Service.Keyboard;
using Microsoft.Extensions.Logging;

namespace LANType.Service.Engine;

public sealed class TypingScheduler : IDisposable
{
    private readonly ILogger<TypingScheduler> _logger;
    private readonly IHidKeyboardInterface _keyboard;
    private readonly object _stateLock = new();

    private CancellationTokenSource? _activeCts;
    private Task? _typingTask;

    private SessionState _state = SessionState.Ready;
    private string _currentText = string.Empty;
    private int _currentIndex;
    private int _currentCpm = ProtocolConstants.DefaultCpm;
    private int _currentWpm = ProtocolConstants.DefaultWpm;
    private string? _pairedDeviceName;
    private string? _currentSessionToken;
    private readonly Stopwatch _sessionStopwatch = new();

    public event Action<SessionStateMessage>? StateChanged;

    public SessionState CurrentState
    {
        get
        {
            lock (_stateLock) return _state;
        }
    }

    public TypingScheduler(ILogger<TypingScheduler> logger, IHidKeyboardInterface keyboard)
    {
        _logger = logger;
        _keyboard = keyboard;
    }

    public void SetPairedDevice(string? deviceName, string? sessionToken)
    {
        lock (_stateLock)
        {
            _pairedDeviceName = deviceName;
            _currentSessionToken = sessionToken;
            if (_state == SessionState.Disconnected && !string.IsNullOrEmpty(sessionToken))
            {
                _state = SessionState.Ready;
            }
            NotifyStateChanged();
        }
    }

    public static int CalculateDelayMs(int cpm)
    {
        int clampedCpm = Math.Clamp(cpm, ProtocolConstants.MinCpm, ProtocolConstants.MaxCpm);
        return Math.Max(1, (int)Math.Round(60000.0 / clampedCpm));
    }

    public static int CpmFromWpm(int wpm)
    {
        int clampedWpm = Math.Clamp(wpm, ProtocolConstants.MinWpm, ProtocolConstants.MaxWpm);
        return clampedWpm * 5;
    }

    public static int WpmFromCpm(int cpm)
    {
        return Math.Max(1, (int)Math.Round(cpm / 5.0));
    }

    public bool Start(StartTypingMessage message, out string? errorMessage)
    {
        lock (_stateLock)
        {
            if (string.IsNullOrEmpty(message.Text))
            {
                errorMessage = "Text payload is empty.";
                return false;
            }

            if (message.Text.Length > ProtocolConstants.MaxPayloadCharacterLength)
            {
                errorMessage = $"Text payload exceeds maximum allowed length of {ProtocolConstants.MaxPayloadCharacterLength} characters.";
                return false;
            }

            // Cancel any previously running session
            CancelActiveTask();

            _currentText = message.Text;
            _currentIndex = 0;

            if (message.SpeedUnit == SpeedUnit.Wpm)
            {
                _currentWpm = Math.Clamp(message.SpeedValue, ProtocolConstants.MinWpm, ProtocolConstants.MaxWpm);
                _currentCpm = CpmFromWpm(_currentWpm);
            }
            else
            {
                _currentCpm = Math.Clamp(message.SpeedValue, ProtocolConstants.MinCpm, ProtocolConstants.MaxCpm);
                _currentWpm = WpmFromCpm(_currentCpm);
            }

            _state = SessionState.Typing;
            _sessionStopwatch.Restart();
            _activeCts = new CancellationTokenSource();

            var token = _activeCts.Token;
            _typingTask = Task.Run(() => ExecuteTypingLoopAsync(token), token);

            _logger.LogInformation("Typing session started. TextLength: {Length}, CPM: {CPM}, WPM: {WPM}", 
                _currentText.Length, _currentCpm, _currentWpm);

            errorMessage = null;
            NotifyStateChanged();
            return true;
        }
    }

    public bool Pause(out string? errorMessage)
    {
        lock (_stateLock)
        {
            if (_state != SessionState.Typing)
            {
                errorMessage = $"Cannot pause when session is in state '{_state}'.";
                return false;
            }

            _state = SessionState.Paused;
            _sessionStopwatch.Stop();
            CancelActiveTask();

            _logger.LogInformation("Typing session paused at character index {Index}/{Total}", 
                _currentIndex, _currentText.Length);

            // Clean up any held keys
            _ = _keyboard.ReleaseAllKeysAsync();

            errorMessage = null;
            NotifyStateChanged();
            return true;
        }
    }

    public bool Resume(out string? errorMessage)
    {
        lock (_stateLock)
        {
            if (_state != SessionState.Paused)
            {
                errorMessage = $"Cannot resume when session is in state '{_state}'.";
                return false;
            }

            if (_currentIndex >= _currentText.Length)
            {
                _state = SessionState.Completed;
                errorMessage = "Typing has already reached completion.";
                NotifyStateChanged();
                return false;
            }

            _state = SessionState.Typing;
            _sessionStopwatch.Start();
            _activeCts = new CancellationTokenSource();

            var token = _activeCts.Token;
            _typingTask = Task.Run(() => ExecuteTypingLoopAsync(token), token);

            _logger.LogInformation("Typing session resumed from character index {Index}/{Total}", 
                _currentIndex, _currentText.Length);

            errorMessage = null;
            NotifyStateChanged();
            return true;
        }
    }

    public bool Stop(string? reason, out string? errorMessage)
    {
        lock (_stateLock)
        {
            if (_state == SessionState.Stopped || _state == SessionState.Completed)
            {
                errorMessage = null;
                return true;
            }

            _state = SessionState.Stopped;
            _sessionStopwatch.Stop();
            CancelActiveTask();

            _logger.LogInformation("Typing session stopped at index {Index}. Reason: {Reason}", 
                _currentIndex, reason ?? "User requested");

            // Safe key release
            _ = _keyboard.ReleaseAllKeysAsync();

            errorMessage = null;
            NotifyStateChanged();
            return true;
        }
    }

    public async Task EmergencyStopAsync()
    {
        lock (_stateLock)
        {
            _state = SessionState.Stopped;
            _sessionStopwatch.Stop();
            CancelActiveTask();
        }

        _logger.LogWarning("EMERGENCY STOP TRIGGERED: Cancelling all queues and releasing all keys.");
        await _keyboard.ReleaseAllKeysAsync();
        NotifyStateChanged();
    }

    private async Task ExecuteTypingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                char c;
                int index;
                int total;
                int cpm;

                lock (_stateLock)
                {
                    if (_state != SessionState.Typing || _currentIndex >= _currentText.Length)
                    {
                        break;
                    }

                    c = _currentText[_currentIndex];
                    index = _currentIndex;
                    total = _currentText.Length;
                    cpm = _currentCpm;
                }

                int nominalDelayMs = CalculateDelayMs(cpm);
                // Key-down duration is clamped between 15ms and min(50ms, 40% of nominal delay)
                int keyDownDurationMs = Math.Clamp((int)(nominalDelayMs * 0.35), 15, 60);
                int keyUpDurationMs = Math.Max(5, nominalDelayMs - keyDownDurationMs);

                var mapping = USKeyboardLayoutMapper.MapCharacter(c);

                if (mapping.IsSupported)
                {
                    // 1. Send Key-Down Report with Modifiers + KeyCode
                    var keyDownReport = HidKeyboardReport.SingleKey(mapping.Modifiers, mapping.KeyCode);
                    await _keyboard.SendReportAsync(keyDownReport, cancellationToken);

                    // Wait key-down hold duration using high precision delay
                    await DelayPreciseAsync(keyDownDurationMs, cancellationToken);

                    // 2. Send Key-Up Report (Release Key)
                    await _keyboard.ReleaseAllKeysAsync(cancellationToken);

                    // Wait remaining inter-key delay
                    await DelayPreciseAsync(keyUpDurationMs, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Skipping unsupported character at index {Index}: {Reason}", 
                        index, mapping.UnsupportedReason);
                    await DelayPreciseAsync(nominalDelayMs, cancellationToken);
                }

                lock (_stateLock)
                {
                    _currentIndex++;
                    if (_currentIndex >= _currentText.Length)
                    {
                        _state = SessionState.Completed;
                        _sessionStopwatch.Stop();
                        _logger.LogInformation("Typing session completed successfully ({Total} chars)", _currentText.Length);
                        NotifyStateChanged();
                        break;
                    }

                    // Periodically notify progress every 5 characters or at least once per second
                    if (_currentIndex % 5 == 0 || nominalDelayMs >= 200)
                    {
                        NotifyStateChanged();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on pause or stop
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in typing loop");
            lock (_stateLock)
            {
                _state = SessionState.Error;
                NotifyStateChanged();
            }
        }
        finally
        {
            // Safety guarantee: Release any held keys
            await _keyboard.ReleaseAllKeysAsync();
            NotifyStateChanged();
        }
    }

    private static async Task DelayPreciseAsync(int milliseconds, CancellationToken cancellationToken)
    {
        if (milliseconds <= 0) return;

        // For delays over 15ms, use Task.Delay to yield CPU
        if (milliseconds > 15)
        {
            await Task.Delay(milliseconds - 5, cancellationToken);
        }

        // Spin wait for the remaining fine milliseconds with high-resolution Stopwatch
        long targetTicks = Stopwatch.GetTimestamp() + (milliseconds * Stopwatch.Frequency / 1000);
        while (Stopwatch.GetTimestamp() < targetTicks)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            Thread.SpinWait(10);
        }
    }

    public SessionStateMessage GetCurrentStateMessage()
    {
        lock (_stateLock)
        {
            int remaining = Math.Max(0, _currentText.Length - _currentIndex);
            long elapsed = _sessionStopwatch.ElapsedMilliseconds;
            long estimatedRemaining = 0;

            if (_currentCpm > 0 && remaining > 0)
            {
                estimatedRemaining = (long)(remaining * (60000.0 / _currentCpm));
            }

            return new SessionStateMessage
            {
                State = _state,
                PairedDeviceName = _pairedDeviceName,
                TotalCharacters = _currentText.Length,
                CharactersTyped = _currentIndex,
                CharactersRemaining = remaining,
                CurrentCpm = _currentCpm,
                CurrentWpm = _currentWpm,
                ElapsedMilliseconds = elapsed,
                EstimatedRemainingMilliseconds = estimatedRemaining,
                ErrorMessage = _state == SessionState.Error ? "An error occurred during typing." : null
            };
        }
    }

    private void NotifyStateChanged()
    {
        var msg = GetCurrentStateMessage();
        try
        {
            StateChanged?.Invoke(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking StateChanged event handler");
        }
    }

    private void CancelActiveTask()
    {
        if (_activeCts != null)
        {
            if (!_activeCts.IsCancellationRequested)
            {
                _activeCts.Cancel();
            }
            _activeCts.Dispose();
            _activeCts = null;
        }
    }

    public void Dispose()
    {
        CancelActiveTask();
        _sessionStopwatch.Stop();
    }
}
