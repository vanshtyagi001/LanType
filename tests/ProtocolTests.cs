using LANType.Protocol;
using Xunit;

namespace LANType.Tests;

public class ProtocolTests
{
    [Fact]
    public void Serialize_And_Deserialize_PairRequestMessage()
    {
        var msg = new PairRequestMessage
        {
            DeviceName = "Pixel 8 Pro",
            PairingPin = "123456",
            ClientVersion = "1.0.0"
        };

        var json = ProtocolSerializer.Serialize(msg);
        Assert.Contains("\"type\":\"PairRequest\"", json);
        Assert.Contains("\"deviceName\":\"Pixel 8 Pro\"", json);
        Assert.Contains("\"pairingPin\":\"123456\"", json);

        var deserialized = ProtocolSerializer.Deserialize(json);
        Assert.NotNull(deserialized);
        Assert.IsType<PairRequestMessage>(deserialized);

        var pairReq = (PairRequestMessage)deserialized;
        Assert.Equal("Pixel 8 Pro", pairReq.DeviceName);
        Assert.Equal("123456", pairReq.PairingPin);
    }

    [Fact]
    public void Serialize_And_Deserialize_StartTypingMessage()
    {
        var msg = new StartTypingMessage
        {
            SessionToken = "test-token-xyz",
            Text = "Hello World 123",
            SpeedUnit = SpeedUnit.Wpm,
            SpeedValue = 75,
            InterKeyDelayMs = 25
        };

        var json = ProtocolSerializer.Serialize(msg);
        Assert.Contains("\"type\":\"StartTyping\"", json);
        Assert.Contains("\"speedUnit\":\"Wpm\"", json);

        var deserialized = ProtocolSerializer.Deserialize(json);
        Assert.NotNull(deserialized);
        Assert.IsType<StartTypingMessage>(deserialized);

        var startMsg = (StartTypingMessage)deserialized;
        Assert.Equal("Hello World 123", startMsg.Text);
        Assert.Equal(SpeedUnit.Wpm, startMsg.SpeedUnit);
        Assert.Equal(75, startMsg.SpeedValue);
    }

    [Fact]
    public void HidKeyboardReport_SingleKey_GeneratesCorrectBytes()
    {
        var report = HidKeyboardReport.SingleKey(HidModifier.LeftShift, HidUsageCodes.KeyA);
        var bytes = report.ToByteArray();

        Assert.Equal(8, bytes.Length);
        Assert.Equal((byte)HidModifier.LeftShift, bytes[0]);
        Assert.Equal(0, bytes[1]); // Reserved
        Assert.Equal(HidUsageCodes.KeyA, bytes[2]);
        Assert.Equal(0, bytes[3]);
        Assert.Equal(0, bytes[4]);
        Assert.Equal(0, bytes[5]);
        Assert.Equal(0, bytes[6]);
        Assert.Equal(0, bytes[7]);
        Assert.False(report.IsEmpty);
    }

    [Fact]
    public void HidKeyboardReport_Empty_ReportsIsEmpty()
    {
        var report = HidKeyboardReport.Empty;
        Assert.True(report.IsEmpty);
        var bytes = report.ToByteArray();
        Assert.All(bytes, b => Assert.Equal(0, b));
    }
}
