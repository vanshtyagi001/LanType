using System.Diagnostics;
using System.Runtime.InteropServices;
using LANType.Protocol;
using LANType.Service.Keyboard;
using Microsoft.Extensions.Logging;

namespace LANType.Service.Engine;

public sealed class TypingScheduler : IDisposable
{
    private readonly ILogger<TypingScheduler> _logger;
    private readonly IHidKeyboardInterface _keyboard;
    private readonly object _stateLock = new();

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

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

            // Normalize newlines (\r\n -> \n) so code line breaks trigger exactly one Enter keystroke
            _currentText = message.Text.Replace("\r\n", "\n").Replace('\r', '\n');
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
        if (OperatingSystem.IsWindows())
        {
            _ = TimeBeginPeriod(1);
        }

        try
        {
            int startIndex;
            string text;
            int cpm;

            lock (_stateLock)
            {
                startIndex = _currentIndex;
                text = _currentText;
                cpm = _currentCpm;
            }

            // Ideal interval per character in milliseconds
            double nominalDelayPerCharMs = 60000.0 / Math.Clamp(cpm, ProtocolConstants.MinCpm, ProtocolConstants.MaxCpm);

            // High-resolution session base timestamp for real-time jitter pacing
            long loopStartTicks = Stopwatch.GetTimestamp();
            var notifyStopwatch = Stopwatch.StartNew();

            for (int i = startIndex; i < text.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                char c = text[i];
                var mapping = USKeyboardLayoutMapper.MapCharacter(c);

                // Closed-loop target pacing:
                // Target cumulative elapsed time for completing character 'i' relative to loop start
                int charsProcessed = (i - startIndex + 1);
                double targetElapsedMs = charsProcessed * nominalDelayPerCharMs;

                // Current actual elapsed time
                double actualElapsedMs = (double)(Stopwatch.GetTimestamp() - loopStartTicks) * 1000.0 / Stopwatch.Frequency;
                double budgetMs = targetElapsedMs - actualElapsedMs;

                // Jitter Compensation:
                // If system jitter or scheduling delays cause actualElapsedMs to lag behind targetElapsedMs,
                // budgetMs drops. We dynamically compress hold and inter-key delay down to hardware minimums
                // to immediately accelerate and catch up with real-time!
                int keyDownMs;
                double keyUpMs;

                if (budgetMs <= 15.0)
                {
                    // Jitter / lag detected: increase typing speed to catch up!
                    keyDownMs = 12; // Minimum safe physical latch duration for HID and Windows message queues
                    keyUpMs = Math.Max(0.5, budgetMs - keyDownMs);
                }
                else
                {
                    // Ahead or on schedule: natural human-like keystroke pacing
                    keyDownMs = Math.Clamp((int)(budgetMs * 0.35), 15, 50);
                    keyUpMs = Math.Max(3.0, budgetMs - keyDownMs);
                }

                if (mapping.IsSupported)
                {
                    // 1. Send Key-Down Report
                    var keyDownReport = HidKeyboardReport.SingleKey(mapping.Modifiers, mapping.KeyCode);
                    await _keyboard.SendReportAsync(keyDownReport, cancellationToken);

                    // Key hold duration
                    await DelayPreciseAsync(keyDownMs, cancellationToken);

                    // 2. Send Key-Up Report (Release Key)
                    await _keyboard.ReleaseAllKeysAsync(cancellationToken);

                    // Remaining inter-key rest (compressed dynamically when lagging)
                    if (keyUpMs > 0.5)
                    {
                        await DelayPreciseAsync(keyUpMs, cancellationToken);
                    }
                }
                else
                {
                    // Fallback for non-ASCII / Unicode characters (smart quotes, em-dashes, non-Latin, etc.)
                    await _keyboard.SendUnicodeCharAsync(c, cancellationToken);
                    await DelayPreciseAsync(keyDownMs, cancellationToken);
                    await _keyboard.ReleaseAllKeysAsync(cancellationToken);

                    if (keyUpMs > 0.5)
                    {
                        await DelayPreciseAsync(keyUpMs, cancellationToken);
                    }
                }

                lock (_stateLock)
                {
                    _currentIndex = i + 1;
                    if (_currentIndex >= text.Length)
                    {
                        _state = SessionState.Completed;
                        _sessionStopwatch.Stop();
                        _logger.LogInformation("Typing session completed successfully ({Total} chars)", text.Length);
                        NotifyStateChanged();
                        break;
                    }

                    // High-frequency real-time update: at least 20 FPS (every 50ms) or every 3 characters
                    if (notifyStopwatch.ElapsedMilliseconds >= 50 || _currentIndex % 3 == 0)
                    {
                        NotifyStateChanged();
                        notifyStopwatch.Restart();
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
            if (OperatingSystem.IsWindows())
            {
                _ = TimeEndPeriod(1);
            }
            await _keyboard.ReleaseAllKeysAsync();
            NotifyStateChanged();
        }
    }

    private static async Task DelayPreciseAsync(double milliseconds, CancellationToken cancellationToken)
    {
        if (milliseconds <= 0.5) return;

        long startTicks = Stopwatch.GetTimestamp();
        long targetTicks = startTicks + (long)(milliseconds * Stopwatch.Frequency / 1000.0);

        // If delay is significant (> 12ms), yield CPU for the bulk of it using Task.Delay
        if (milliseconds > 12.0)
        {
            int taskDelayMs = (int)(milliseconds - 4.0);
            if (taskDelayMs > 0)
            {
                await Task.Delay(taskDelayMs, cancellationToken);
            }
        }

        // Sub-millisecond spin wait for exact precision
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
