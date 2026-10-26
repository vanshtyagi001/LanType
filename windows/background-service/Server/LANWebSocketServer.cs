using System.Net;
using System.Net.WebSockets;
using System.Text;
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
    private readonly HttpListener _listener;
    private readonly int _port;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private WebSocket? _activeClient;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

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

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{_port}/");

        // Subscribe to scheduler state changes to broadcast telemetry to the connected client
        _scheduler.StateChanged += OnSchedulerStateChanged;
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
            _logger.LogInformation("LANType WebSocket server listening on port {Port}", _port);
        }
        catch (HttpListenerException hlex) when (hlex.ErrorCode == 5) // Access denied
        {
            _logger.LogWarning("Access denied binding to wildcard http://*:{Port}/. Retrying with localhost.", _port);
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
            _logger.LogInformation("LANType WebSocket server listening on localhost port {Port}", _port);
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();

                // Health check endpoint
                if (context.Request.Url?.AbsolutePath == "/health" || 
                    context.Request.Url?.AbsolutePath == "/")
                {
                    byte[] responseBytes = Encoding.UTF8.GetBytes("LANType Background Service Running");
                    context.Response.ContentType = "text/plain";
                    context.Response.StatusCode = 200;
                    context.Response.ContentLength64 = responseBytes.Length;
                    await context.Response.OutputStream.WriteAsync(responseBytes, cancellationToken);
                    context.Response.OutputStream.Close();
                    continue;
                }

                if (context.Request.IsWebSocketRequest && 
                    (context.Request.Url?.AbsolutePath == "/ws" || context.Request.Url?.AbsolutePath == "/ws/"))
                {
                    _ = HandleWebSocketConnectionAsync(context, cancellationToken);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Exception in listener loop");
                }
            }
        }
    }

    private async Task HandleWebSocketConnectionAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        WebSocketContext wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to accept WebSocket upgrade request");
            context.Response.StatusCode = 500;
            context.Response.Close();
            return;
        }

        var webSocket = wsContext.WebSocket;
        _logger.LogInformation("New incoming WebSocket connection from {RemoteEndpoint}", 
            context.Request.RemoteEndPoint);

        // Replace any previous client
        if (_activeClient != null && _activeClient.State == WebSocketState.Open)
        {
            try
            {
                await _activeClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "New client connected", CancellationToken.None);
            }
            catch { /* Ignore close errors on old client */ }
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
            // Expected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebSocket stream");
        }
        finally
        {
            _logger.LogInformation("WebSocket connection closed.");
            if (_activeClient == webSocket)
            {
                _activeClient = null;
            }

            // Auto-pause if client disconnected while typing
            if (_scheduler.CurrentState == SessionState.Typing)
            {
                _logger.LogWarning("Connection lost during typing. Pausing session automatically for safe recovery.");
                _scheduler.Pause(out _);
            }

            try
            {
                webSocket.Dispose();
            }
            catch { }
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
            _logger.LogWarning(ex, "Failed to parse incoming JSON message");
            await SendMessageAsync(webSocket, new ErrorMessage
            {
                ErrorCode = "INVALID_JSON",
                Message = "Message could not be deserialized."
            }, cancellationToken);
            return;
        }

        if (baseMsg == null) return;

        switch (baseMsg)
        {
            case PairRequestMessage pairReq:
                var (success, token, err) = _pairingManager.ValidateAndPair(pairReq);
                if (success)
                {
                    _scheduler.SetPairedDevice(pairReq.DeviceName, token);
                }
                var pairResp = new PairResponseMessage
                {
                    Success = success,
                    SessionToken = token,
                    ErrorMessage = err
                };
                await SendMessageAsync(webSocket, pairResp, cancellationToken);
                break;

            case HeartbeatMessage hb:
                var ack = new HeartbeatMessage
                {
                    Sequence = hb.Sequence,
                    IsAck = true
                };
                await SendMessageAsync(webSocket, ack, cancellationToken);
                break;

            case StartTypingMessage startMsg:
                if (!_pairingManager.IsSessionValid(startMsg.SessionToken))
                {
                    await SendMessageAsync(webSocket, new ErrorMessage
                    {
                        ErrorCode = "UNAUTHORIZED",
                        Message = "Invalid or expired session token. Please re-pair."
                    }, cancellationToken);
                    return;
                }

                if (!_scheduler.Start(startMsg, out var startErr))
                {
                    await SendMessageAsync(webSocket, new ErrorMessage
                    {
                        ErrorCode = "START_FAILED",
                        Message = startErr ?? "Could not start typing."
                    }, cancellationToken);
                }
                break;

            case PauseTypingMessage pauseMsg:
                if (!_pairingManager.IsSessionValid(pauseMsg.SessionToken))
                {
                    await SendMessageAsync(webSocket, new ErrorMessage { ErrorCode = "UNAUTHORIZED", Message = "Invalid session token." }, cancellationToken);
                    return;
                }
                _scheduler.Pause(out _);
                break;

            case ResumeTypingMessage resumeMsg:
                if (!_pairingManager.IsSessionValid(resumeMsg.SessionToken))
                {
                    await SendMessageAsync(webSocket, new ErrorMessage { ErrorCode = "UNAUTHORIZED", Message = "Invalid session token." }, cancellationToken);
                    return;
                }
                _scheduler.Resume(out _);
                break;

            case StopTypingMessage stopMsg:
                if (!_pairingManager.IsSessionValid(stopMsg.SessionToken))
                {
                    await SendMessageAsync(webSocket, new ErrorMessage { ErrorCode = "UNAUTHORIZED", Message = "Invalid session token." }, cancellationToken);
                    return;
                }
                _scheduler.Stop(stopMsg.Reason, out _);
                break;

            case EmergencyStopMessage:
                await _scheduler.EmergencyStopAsync();
                break;
        }
    }

    private void OnSchedulerStateChanged(SessionStateMessage stateMsg)
    {
        if (_activeClient != null && _activeClient.State == WebSocketState.Open)
        {
            _ = SendMessageAsync(_activeClient, stateMsg, CancellationToken.None);
        }
    }

    private async Task SendMessageAsync(WebSocket webSocket, LANTypeMessage message, CancellationToken cancellationToken)
    {
        if (webSocket.State != WebSocketState.Open) return;

        byte[] payload = ProtocolSerializer.SerializeToUtf8Bytes(message);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send WebSocket message type {Type}", message.GetType().Name);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener.Stop();
        }
        catch { }
    }

    public void Dispose()
    {
        _scheduler.StateChanged -= OnSchedulerStateChanged;
        Stop();
        _listener.Close();
        _sendLock.Dispose();
        _cts?.Dispose();
    }
}
