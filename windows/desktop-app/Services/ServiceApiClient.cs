using System.Net.Http;
using System.Text;
using System.Text.Json;
using LANType.Protocol;

namespace LANType.Desktop.Services;

public class ServiceStatusDto
{
    public bool IsServiceRunning { get; set; }
    public string ServiceVersion { get; set; } = string.Empty;
    public string HostIp { get; set; } = string.Empty;
    public int WebSocketPort { get; set; }
    public string PairingPin { get; set; } = string.Empty;
    public string PairingUri { get; set; } = string.Empty;
    public DriverStatusMessage DriverStatus { get; set; } = new();
    public SessionStateMessage SessionState { get; set; } = new();
    public bool IsClientConnected { get; set; }
}

public class ServiceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ServiceApiClient(string baseUrl = "http://127.0.0.1:52444")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
    }

    public async Task<ServiceStatusDto?> GetStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/status");
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ServiceStatusDto>(json, JsonOptions);
            }
        }
        catch
        {
            // Service not running or unreachable
        }

        return null;
    }

    public async Task<bool> RegeneratePinAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/pin/regenerate", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StartTypingAsync(string text, SpeedUnit unit, int speed)
    {
        try
        {
            var payload = new StartTypingMessage
            {
                Text = text,
                SpeedUnit = unit,
                SpeedValue = speed
            };

            string json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/typing/start", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PauseTypingAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/typing/pause", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ResumeTypingAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/typing/resume", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StopTypingAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/typing/stop", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EmergencyStopAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/typing/emergency-stop", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
