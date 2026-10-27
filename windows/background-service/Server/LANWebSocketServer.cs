using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LANType.Protocol;
using LANType.Service.Auth;
using LANType.Service.Engine;
using Microsoft.Extensions.Logging;

namespace LANType.Service.Server;

public class LANWebSocketServer : IDisposable
{
    private readonly ILogger<LANWebSocketServer> _logger;
    private readonly PairingManager _pairingManager;
    private readonly TypingScheduler _scheduler;
    private readonly int _port;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private WebSocket? _activeClient;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    public bool IsClientConnected => _activeClient != null && _activeClient.State == WebSocketState.Open;

    public LANWebSocketServer(
        ILogger<LANWebSocketServer> logger,
        PairingManager pairingManager,
        TypingScheduler scheduler,
        int port = ProtocolConstants.DefaultWebSocketPort)
    {
        _logger = logger;
        _pairingManager = pairingManager;
        _scheduler = scheduler;
        _port = port;

        _scheduler.StateChanged += OnSchedulerStateChanged;
    }

    public void Start()
    {
        try
        {
            _cts = new CancellationTokenSource();

            // Create dual-stack socket listener on 0.0.0.0 and [::]
            _tcpListener = new TcpListener(IPAddress.Any, _port);
            _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _tcpListener.Start();

            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
            _logger.LogInformation("LANType Dual-Stack WebSocket Server listening on all interfaces at 0.0.0.0:{Port}", _port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start TCP WebSocket listener on port {Port}", _port);
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        if (_tcpListener == null) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleIncomingClientAsync(tcpClient, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error accepting client TCP connection");
                }
            }
        }
    }

    private async Task HandleIncomingClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var remoteEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
        var stream = tcpClient.GetStream();

        try
        {
            // Read HTTP request header
            byte[] headerBuffer = new byte[4096];
            int bytesRead = await stream.ReadAsync(headerBuffer, 0, headerBuffer.Length, cancellationToken);
            if (bytesRead <= 0)
            {
                tcpClient.Dispose();
                return;
            }

            string requestText = Encoding.UTF8.GetString(headerBuffer, 0, bytesRead);

            // Handle Health / Discovery check probe
            if (requestText.StartsWith("GET /health") || requestText.StartsWith("GET /discovery") || requestText.StartsWith("GET / "))
            {
                var info = new
                {
                    service = "LANType",
                    hostName = Environment.MachineName,
                    port = _port,
                    version = ProtocolConstants.CurrentProtocolVersion
                };
                string json = JsonSerializer.Serialize(info);
                byte[] body = Encoding.UTF8.GetBytes(json);
                string response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\nAccess-Control-Allow-Origin: *\r\n\r\n";
                byte[] responseHeaders = Encoding.ASCII.GetBytes(response);
                await stream.WriteAsync(responseHeaders, cancellationToken);
                await stream.WriteAsync(body, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                tcpClient.Dispose();
                return;
            }

            // Handle WebSocket Upgrade
            if (requestText.Contains("Upgrade: websocket", StringComparison.OrdinalIgnoreCase) ||
                requestText.Contains("upgrade: websocket", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(requestText, @"Sec-WebSocket-Key:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    byte[] badReq = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\nMissing Sec-WebSocket-Key");
                    await stream.WriteAsync(badReq, cancellationToken);
                    tcpClient.Dispose();
                    return;
                }

                string secWebSocketKey = match.Groups[1].Value.Trim();
                string acceptHash = Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(secWebSocketKey + WebSocketGuid)));

                string handshakeResponse =
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Accept: {acceptHash}\r\n\r\n";

                byte[] handshakeBytes = Encoding.ASCII.GetBytes(handshakeResponse);
                await stream.WriteAsync(handshakeBytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                // Create full WebSocket from stream
                var webSocket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds(10));
                await HandleWebSocketSessionAsync(webSocket, remoteEndpoint, cancellationToken);
            }
            else
            {
                byte[] notFound = Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(notFound, cancellationToken);
                tcpClient.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Client handshake closed or aborted from {Endpoint}", remoteEndpoint);
            try { tcpClient.Dispose(); } catch { }
        }
    }

    private async Task HandleWebSocketSessionAsync(WebSocket webSocket, string remoteEndpoint, CancellationToken cancellationToken)
    {
        _logger.LogInformation("New incoming authenticated WebSocket connection from {RemoteEndpoint}", remoteEndpoint);

        // Replace any prior active client
        if (_activeClient != null && _activeClient.State == WebSocketState.Open)
        {
            try
            {
                await _activeClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "New client connected", CancellationToken.None);
            }
            catch { }
        }

        _activeClient = webSocket;
        var buffer = new byte[8192];
        var memoryStream = new MemoryStream();

        try
        {
            // Send current state immediately on connect
            await SendMessageAsync(webSocket, _scheduler.GetCurrentStateMessage(), cancellationToken);

            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                memoryStream.SetLength(0);
                WebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                        break;
                    }
                    memoryStream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string json = Encoding.UTF8.GetString(memoryStream.ToArray());
                    await ProcessIncomingMessageAsync(webSocket, json, cancellationToken);
                }
            }
        }
        catch (WebSocketException wsex)
        {
            _logger.LogWarning("WebSocket connection lost: {Message}", wsex.Message);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebSocket stream from {Endpoint}", remoteEndpoint);
        }
        finally
        {
            _logger.LogInformation("WebSocket connection closed with {Endpoint}.", remoteEndpoint);
            if (_activeClient == webSocket)
            {
                _activeClient = null;
            }

            if (_scheduler.CurrentState == SessionState.Typing)
            {
                _logger.LogWarning("Connection lost during typing. Pausing session automatically for safe recovery.");
                _scheduler.Pause(out _);
            }

            try { webSocket.Dispose(); } catch { }
        }
    }

    private async Task ProcessIncomingMessageAsync(WebSocket webSocket, string json, CancellationToken cancellationToken)
    {
        LANTypeMessage? baseMsg;
        try
        {
            baseMsg = ProtocolSerializer.Deserialize(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize incoming WebSocket message: {Json}", json);
            await SendMessageAsync(webSocket, new ErrorMessage { ErrorCode = "ERR_INVALID_MSG", Message = "Invalid message format: " + ex.Message }, cancellationToken);
            return;
        }

        if (baseMsg == null) return;

        // Dispatch message types
        switch (baseMsg)
        {
            case PairRequestMessage pairReq:
                await HandlePairRequestAsync(webSocket, pairReq, cancellationToken);
                break;

            case StartTypingMessage startMsg:
                if (!ValidateSession(startMsg.SessionToken, out var errStart))
                {
                    await SendMessageAsync(webSocket, errStart, cancellationToken);
                    return;
                }
                HandleStartTyping(startMsg);
                break;

            case PauseTypingMessage pauseMsg:
                if (!ValidateSession(pauseMsg.SessionToken, out var errPause))
                {
                    await SendMessageAsync(webSocket, errPause, cancellationToken);
                    return;
                }
                _scheduler.Pause(out _);
                break;

            case ResumeTypingMessage resumeMsg:
                if (!ValidateSession(resumeMsg.SessionToken, out var errResume))
                {
                    await SendMessageAsync(webSocket, errResume, cancellationToken);
                    return;
                }
                _scheduler.Resume(out _);
                break;

            case StopTypingMessage stopMsg:
                if (!ValidateSession(stopMsg.SessionToken, out var errStop))
                {
                    await SendMessageAsync(webSocket, errStop, cancellationToken);
                    return;
                }
                _scheduler.Stop(stopMsg.Reason, out _);
                break;

            case EmergencyStopMessage emergencyMsg:
                if (!ValidateSession(emergencyMsg.SessionToken, out var errEm))
                {
                    await SendMessageAsync(webSocket, errEm, cancellationToken);
                    return;
                }
                await _scheduler.EmergencyStopAsync();
                break;

            case HeartbeatMessage:
                await SendMessageAsync(webSocket, new HeartbeatMessage { Sequence = Environment.TickCount64, IsAck = true }, cancellationToken);
                break;

            default:
                _logger.LogWarning("Received unhandled message type: {Type}", baseMsg.GetType().Name);
                break;
        }
    }

    private async Task HandlePairRequestAsync(WebSocket webSocket, PairRequestMessage pairReq, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing pairing request from device: '{DeviceName}'", pairReq.DeviceName);

        var pairResult = _pairingManager.ValidateAndPair(pairReq);
        if (pairResult.Success)
        {
            _scheduler.SetPairedDevice(pairReq.DeviceName, pairResult.Token);
            _logger.LogInformation("Device '{DeviceName}' successfully authenticated and paired!", pairReq.DeviceName);
            var resp = new PairResponseMessage
            {
                Success = true,
                SessionToken = pairResult.Token,
                ServerVersion = ProtocolConstants.CurrentProtocolVersion,
                ErrorMessage = null
            };
            await SendMessageAsync(webSocket, resp, cancellationToken);
            await SendMessageAsync(webSocket, _scheduler.GetCurrentStateMessage(), cancellationToken);
        }
        else
        {
            _logger.LogWarning("Pairing failed for device '{DeviceName}': {Reason}", pairReq.DeviceName, pairResult.Error);
            var resp = new PairResponseMessage
            {
                Success = false,
                SessionToken = null,
                ServerVersion = ProtocolConstants.CurrentProtocolVersion,
                ErrorMessage = pairResult.Error
            };
            await SendMessageAsync(webSocket, resp, cancellationToken);
        }
    }

    private void HandleStartTyping(StartTypingMessage startMsg)
    {
        _logger.LogInformation("Starting typing session: {CharCount} chars, speed {Speed} {Unit}", 
            startMsg.Text.Length, startMsg.SpeedValue, startMsg.SpeedUnit);

        _scheduler.Start(startMsg, out _);
    }

    private bool ValidateSession(string? sessionToken, out ErrorMessage error)
    {
        if (string.IsNullOrEmpty(sessionToken) || !_pairingManager.IsSessionValid(sessionToken))
        {
            error = new ErrorMessage { ErrorCode = "ERR_UNAUTHORIZED", Message = "Unauthorized: Session token is invalid or expired. Please re-pair." };
            return false;
        }

        error = new ErrorMessage();
        return true;
    }

    private async void OnSchedulerStateChanged(SessionStateMessage state)
    {
        if (_activeClient != null && _activeClient.State == WebSocketState.Open)
        {
            try
            {
                await SendMessageAsync(_activeClient, state, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to broadcast scheduler state to active client");
            }
        }
    }

    private async Task SendMessageAsync(WebSocket webSocket, LANTypeMessage message, CancellationToken cancellationToken)
    {
        if (webSocket.State != WebSocketState.Open) return;

        string json = ProtocolSerializer.Serialize(message);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _tcpListener?.Stop();
    }

    public void Dispose()
    {
        _scheduler.StateChanged -= OnSchedulerStateChanged;
        Stop();
        _cts?.Dispose();
        _sendLock.Dispose();
    }
}
