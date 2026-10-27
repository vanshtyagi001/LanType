using System.Net;
using System.Text;
using System.Text.Json;
using LANType.Protocol;
using LANType.Service.Auth;
using LANType.Service.Engine;
using LANType.Service.Keyboard;
using LANType.Service.Network;
using Microsoft.Extensions.Logging;

namespace LANType.Service.Ipc;

public class ServiceStatusDto
{
    public bool IsServiceRunning { get; set; } = true;
    public string ServiceVersion { get; set; } = ProtocolConstants.CurrentProtocolVersion;
    public string HostIp { get; set; } = string.Empty;
    public int WebSocketPort { get; set; }
    public string PairingPin { get; set; } = string.Empty;
    public string PairingUri { get; set; } = string.Empty;
    public DriverStatusMessage DriverStatus { get; set; } = new();
    public SessionStateMessage SessionState { get; set; } = new();
    public bool IsClientConnected { get; set; }
}

public class DesktopIpcServer : IDisposable
{
    private readonly ILogger<DesktopIpcServer> _logger;
    private readonly PairingManager _pairingManager;
    private readonly TypingScheduler _scheduler;
    private readonly IHidKeyboardInterface _keyboard;
    private readonly NetworkInterfaceManager _networkManager;
    private readonly int _ipcPort;
    private readonly int _wsPort;

    private readonly HttpListener _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public DesktopIpcServer(
        ILogger<DesktopIpcServer> logger,
        PairingManager pairingManager,
        TypingScheduler scheduler,
        IHidKeyboardInterface keyboard,
        NetworkInterfaceManager networkManager,
        int ipcPort = ProtocolConstants.DefaultIpcPort,
        int wsPort = ProtocolConstants.DefaultWebSocketPort)
    {
        _logger = logger;
        _pairingManager = pairingManager;
        _scheduler = scheduler;
        _keyboard = keyboard;
        _networkManager = networkManager;
        _ipcPort = ipcPort;
        _wsPort = wsPort;

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_ipcPort}/");
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
            _logger.LogInformation("Desktop IPC Server listening on http://127.0.0.1:{Port}/", _ipcPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Desktop IPC Server on port {Port}", _ipcPort);
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestAsync(context, cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error handling IPC request");
                }
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        // Allow CORS for local desktop calls
        response.AddHeader("Access-Control-Allow-Origin", "*");
        response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 200;
            response.Close();
            return;
        }

        string path = request.Url?.AbsolutePath.ToLowerInvariant() ?? "";

        try
        {
            if (request.HttpMethod == "GET" && path == "/api/status")
            {
                var ip = _networkManager.GetPreferredEndpointAddress();
                var status = new ServiceStatusDto
                {
                    IsServiceRunning = true,
                    ServiceVersion = ProtocolConstants.CurrentProtocolVersion,
                    HostIp = ip.ToString(),
                    WebSocketPort = _wsPort,
                    PairingPin = _pairingManager.CurrentPin,
                    PairingUri = _pairingManager.GetPairingUri(ip.ToString(), _wsPort),
                    DriverStatus = _keyboard.GetStatus(),
                    SessionState = _scheduler.GetCurrentStateMessage(),
                    IsClientConnected = !string.IsNullOrEmpty(_pairingManager.PairedDeviceName)
                };

                await WriteJsonResponseAsync(response, status, cancellationToken);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/pin/regenerate")
            {
                _pairingManager.GenerateNewPin();
                var ip = _networkManager.GetPreferredEndpointAddress();
                var result = new
                {
                    success = true,
                    newPin = _pairingManager.CurrentPin,
                    newUri = _pairingManager.GetPairingUri(ip.ToString(), _wsPort)
                };
                await WriteJsonResponseAsync(response, result, cancellationToken);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/typing/start")
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                string body = await reader.ReadToEndAsync(cancellationToken);
                var startReq = JsonSerializer.Deserialize<StartTypingMessage>(body);

                if (startReq == null || string.IsNullOrEmpty(startReq.Text))
                {
                    response.StatusCode = 400;
                    await WriteJsonResponseAsync(response, new { success = false, error = "Invalid payload" }, cancellationToken);
                    return;
                }

                bool ok = _scheduler.Start(startReq, out var err);
                await WriteJsonResponseAsync(response, new { success = ok, error = err }, cancellationToken);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/typing/pause")
            {
                bool ok = _scheduler.Pause(out var err);
                await WriteJsonResponseAsync(response, new { success = ok, error = err }, cancellationToken);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/typing/resume")
            {
                bool ok = _scheduler.Resume(out var err);
                await WriteJsonResponseAsync(response, new { success = ok, error = err }, cancellationToken);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/typing/stop")
            {
                bool ok = _scheduler.Stop("Desktop control panel requested stop", out var err);
                await WriteJsonResponseAsync(response, new { success = ok, error = err }, cancellationToken);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/typing/emergency-stop")
            {
                await _scheduler.EmergencyStopAsync();
                await WriteJsonResponseAsync(response, new { success = true }, cancellationToken);
                return;
            }

            response.StatusCode = 404;
            response.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling IPC path {Path}", path);
            response.StatusCode = 500;
            response.Close();
        }
    }

    private static async Task WriteJsonResponseAsync(HttpListenerResponse response, object data, CancellationToken cancellationToken)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        response.ContentType = "application/json";
        response.StatusCode = 200;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
        response.OutputStream.Close();
    }

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            _listener.Stop();
            _listener.Close();
            _cts?.Dispose();
        }
        catch { }
    }
}
