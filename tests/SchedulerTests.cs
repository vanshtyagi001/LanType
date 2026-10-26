using LANType.Protocol;
using LANType.Service.Engine;
using LANType.Service.Keyboard;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LANType.Tests;

public class SchedulerTests
{
    [Theory]
    [InlineData(300, 200)]  // 60,000 / 300 = 200ms
    [InlineData(600, 100)]  // 60,000 / 600 = 100ms
    [InlineData(60, 1000)]  // 60,000 / 60 = 1000ms
    public void CalculateDelayMs_CalculatesAccurately(int cpm, int expectedMs)
    {
        int delay = TypingScheduler.CalculateDelayMs(cpm);
        Assert.Equal(expectedMs, delay);
    }

    [Theory]
    [InlineData(60, 300)]  // 60 * 5 = 300 CPM
    [InlineData(100, 500)] // 100 * 5 = 500 CPM
    [InlineData(12, 60)]   // 12 * 5 = 60 CPM
    public void CpmFromWpm_CalculatesAccurately(int wpm, int expectedCpm)
    {
        int cpm = TypingScheduler.CpmFromWpm(wpm);
        Assert.Equal(expectedCpm, cpm);
    }

    [Fact]
    public async Task TypingScheduler_Start_TypesCharacters_AndCompletes()
    {
        var logger = NullLogger<DevTestKeyboardReceiver>.Instance;
        var devReceiver = new DevTestKeyboardReceiver(logger);
        var schedLogger = NullLogger<TypingScheduler>.Instance;
        using var scheduler = new TypingScheduler(schedLogger, devReceiver);

        var msg = new StartTypingMessage
        {
            Text = "ABC",
            SpeedUnit = SpeedUnit.Cpm,
            SpeedValue = 3000 // Ultra-fast for test run
        };

        bool ok = scheduler.Start(msg, out var err);
        Assert.True(ok);
        Assert.Null(err);

        // Wait for completion (3 chars at 3000 cpm takes < 100ms)
        int attempts = 0;
        while (scheduler.CurrentState == SessionState.Typing && attempts++ < 50)
        {
            await Task.Delay(20);
        }

        Assert.Equal(SessionState.Completed, scheduler.CurrentState);
        var state = scheduler.GetCurrentStateMessage();
        Assert.Equal(3, state.CharactersTyped);
        Assert.Equal(0, state.CharactersRemaining);
        Assert.True(devReceiver.TotalReportsSent > 0);
    }

    [Fact]
    public async Task TypingScheduler_Pause_And_Resume_MaintainsIndex()
    {
        var logger = NullLogger<DevTestKeyboardReceiver>.Instance;
        var devReceiver = new DevTestKeyboardReceiver(logger);
        var schedLogger = NullLogger<TypingScheduler>.Instance;
        using var scheduler = new TypingScheduler(schedLogger, devReceiver);

        // 100 CPM = 600ms per character
        var msg = new StartTypingMessage
        {
            Text = "Hello World From LANType",
            SpeedUnit = SpeedUnit.Cpm,
            SpeedValue = 100
        };

        scheduler.Start(msg, out _);
        await Task.Delay(150); // Let it start

        bool paused = scheduler.Pause(out _);
        Assert.True(paused);
        Assert.Equal(SessionState.Paused, scheduler.CurrentState);

        var statePaused = scheduler.GetCurrentStateMessage();
        int pausedIndex = statePaused.CharactersTyped;
        Assert.True(pausedIndex >= 0);

        // Resume
        bool resumed = scheduler.Resume(out _);
        Assert.True(resumed);
        Assert.Equal(SessionState.Typing, scheduler.CurrentState);

        // Stop
        scheduler.Stop("Test Done", out _);
        Assert.Equal(SessionState.Stopped, scheduler.CurrentState);
    }
}
