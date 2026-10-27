using System.Net.WebSockets;
using System.Text;
using LANType.Protocol;
using LANType.Service.Auth;
using LANType.Service.Engine;
using LANType.Service.Keyboard;
using LANType.Service.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LANType.Tests;

public class CrossDeviceE2ETests
{
    private class DummyKeyboard : IHidKeyboardInterface
    {
        public List<HidKeyboardReport> SentReports { get; } = new();
        public List<char> SentUnicodeChars { get; } = new();
        public int ReleaseCount { get; private set; }

        public bool IsConnected => true;
        public bool IsDevFallback => true;
        public string DeviceDescription => "Mock Keyboard";

        public Task<bool> SendReportAsync(HidKeyboardReport report, CancellationToken cancellationToken = default)
        {
            SentReports.Add(report);
            return Task.FromResult(true);
        }

        public Task<bool> SendUnicodeCharAsync(char c, CancellationToken cancellationToken = default)
        {
            SentUnicodeChars.Add(c);
            return Task.FromResult(true);
        }

        public Task<bool> ReleaseAllKeysAsync(CancellationToken cancellationToken = default)
        {
            ReleaseCount++;
            return Task.FromResult(true);
        }

        public DriverStatusMessage GetStatus()
        {
            return new DriverStatusMessage
            {
                State = DriverState.ActiveEnumerated,
                IsDeviceEnumerated = true,
                StatusDescription = "Mock Driver"
            };
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task FullCrossDevice_Pairing_And_Typing_Flow()
    {
        int testPort = 52491;
        var dummyKb = new DummyKeyboard();
        var pairManager = new PairingManager(NullLogger<PairingManager>.Instance);
        var scheduler = new TypingScheduler(NullLogger<TypingScheduler>.Instance, dummyKb);
        using var server = new LANWebSocketServer(
            NullLogger<LANWebSocketServer>.Instance,
            pairManager,
            scheduler,
            testPort);

        server.Start();
        await Task.Delay(100);

        // 1. Mobile Client Connects
        using var clientWs = new ClientWebSocket();
        var serverUri = new Uri($"ws://127.0.0.1:{testPort}/ws");
        await clientWs.ConnectAsync(serverUri, CancellationToken.None);
        Assert.Equal(WebSocketState.Open, clientWs.State);

        // 2. Mobile Sends PairRequest with correct PIN
        string pin = pairManager.CurrentPin;
        var pairReq = new PairRequestMessage
        {
            DeviceName = "OnePlus 12 Test Device",
            PairingPin = pin
        };
        await SendMessageAsync(clientWs, pairReq);

        // Receive PairResponse
        var pairResp = await ReceiveSpecificMessageAsync<PairResponseMessage>(clientWs);
        Assert.NotNull(pairResp);
        Assert.True(pairResp.Success);
        Assert.NotNull(pairResp.SessionToken);

        // Receive SessionState (Ready)
        var readyState = await ReceiveSpecificMessageAsync<SessionStateMessage>(clientWs);
        Assert.NotNull(readyState);
        Assert.Equal(SessionState.Ready, readyState.State);

        // 3. Mobile Sends StartTyping with C/Java code containing CRLF & braces
        string code = "int main() {\r\n    return 0;\r\n}";
        var startMsg = new StartTypingMessage
        {
            SessionToken = pairResp.SessionToken,
            Text = code,
            SpeedUnit = SpeedUnit.Cpm,
            SpeedValue = 2400
        };
        await SendMessageAsync(clientWs, startMsg);

        // Allow scheduler to execute typing loop
        await Task.Delay(300);

        // 4. Verify typing occurred and keys were pressed
        Assert.NotEmpty(dummyKb.SentReports);
        Assert.True(dummyKb.ReleaseCount > 0);

        // Close connection
        await clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test Complete", CancellationToken.None);
    }

    [Fact]
    public async Task CrossDevice_Rejects_Invalid_PIN()
    {
        int testPort = 52492;
        var dummyKb = new DummyKeyboard();
        var pairManager = new PairingManager(NullLogger<PairingManager>.Instance);
        var scheduler = new TypingScheduler(NullLogger<TypingScheduler>.Instance, dummyKb);
        using var server = new LANWebSocketServer(
            NullLogger<LANWebSocketServer>.Instance,
            pairManager,
            scheduler,
            testPort);

        server.Start();
        await Task.Delay(100);

        using var clientWs = new ClientWebSocket();
        await clientWs.ConnectAsync(new Uri($"ws://127.0.0.1:{testPort}/ws"), CancellationToken.None);

        // Send incorrect PIN
        var pairReq = new PairRequestMessage
        {
            DeviceName = "Unauthorized Phone",
            PairingPin = "000000"
        };
        await SendMessageAsync(clientWs, pairReq);

        var pairResp = await ReceiveSpecificMessageAsync<PairResponseMessage>(clientWs);
        Assert.NotNull(pairResp);
        Assert.False(pairResp.Success);
        Assert.Null(pairResp.SessionToken);
    }

    [Fact]
    public async Task CrossDevice_Emergency_Stop_Releases_All_Keys()
    {
        int testPort = 52493;
        var dummyKb = new DummyKeyboard();
        var pairManager = new PairingManager(NullLogger<PairingManager>.Instance);
        var scheduler = new TypingScheduler(NullLogger<TypingScheduler>.Instance, dummyKb);
        using var server = new LANWebSocketServer(
            NullLogger<LANWebSocketServer>.Instance,
            pairManager,
            scheduler,
            testPort);

        server.Start();
        await Task.Delay(100);

        using var clientWs = new ClientWebSocket();
        await clientWs.ConnectAsync(new Uri($"ws://127.0.0.1:{testPort}/ws"), CancellationToken.None);

        // Pair
        await SendMessageAsync(clientWs, new PairRequestMessage
        {
            DeviceName = "Phone",
            PairingPin = pairManager.CurrentPin
        });
        var pairResp = await ReceiveSpecificMessageAsync<PairResponseMessage>(clientWs);
        Assert.NotNull(pairResp);

        // Start long typing
        await SendMessageAsync(clientWs, new StartTypingMessage
        {
            SessionToken = pairResp.SessionToken!,
            Text = new string('A', 5000),
            SpeedUnit = SpeedUnit.Cpm,
            SpeedValue = 100
        });

        await Task.Delay(150);

        // Mobile hits Emergency Stop
        await SendMessageAsync(clientWs, new EmergencyStopMessage
        {
            SessionToken = pairResp.SessionToken
        });

        await Task.Delay(150);

        Assert.Equal(SessionState.Stopped, scheduler.CurrentState);
        Assert.True(dummyKb.ReleaseCount > 0);
    }

    private static async Task SendMessageAsync(ClientWebSocket ws, LANTypeMessage msg)
    {
        string json = ProtocolSerializer.Serialize(msg);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<T?> ReceiveSpecificMessageAsync<T>(ClientWebSocket ws, int timeoutMs = 2500) where T : LANTypeMessage
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var buffer = new byte[8192];
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                string json = Encoding.UTF8.GetString(buffer, 0, res.Count);
                var msg = ProtocolSerializer.Deserialize(json);
                if (msg is T target) return target;
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }
}
