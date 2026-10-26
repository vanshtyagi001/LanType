using System.Text.Json;

namespace LANType.Protocol;

public static class ProtocolSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static string Serialize<T>(T message) where T : LANTypeMessage
    {
        return JsonSerializer.Serialize<LANTypeMessage>(message, Options);
    }

    public static byte[] SerializeToUtf8Bytes<T>(T message) where T : LANTypeMessage
    {
        return JsonSerializer.SerializeToUtf8Bytes<LANTypeMessage>(message, Options);
    }

    public static LANTypeMessage? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<LANTypeMessage>(json, Options);
    }

    public static LANTypeMessage? Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        return JsonSerializer.Deserialize<LANTypeMessage>(utf8Json, Options);
    }
}
