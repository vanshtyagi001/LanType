using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LANType.Service.Network;

public class UdpDiscoveryService : IDisposable
{
    private readonly ILogger<UdpDiscoveryService> _logger;
    private readonly NetworkInterfaceManager _networkManager;
    private readonly int _webSocketPort;
    private const int DiscoveryPort = 52442;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _broadcastTask;

    public UdpDiscoveryService(
        ILogger<UdpDiscoveryService> logger,
        NetworkInterfaceManager networkManager,
        int webSocketPort = 52443)
    {
        _logger = logger;
        _networkManager = networkManager;
        _webSocketPort = webSocketPort;
    }

    public void Start()
    {
        try
        {
            _cts = new CancellationTokenSource();
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
            _udpClient.EnableBroadcast = true;

            _listenTask = Task.Run(() => ListenForProbesAsync(_cts.Token), _cts.Token);
            _broadcastTask = Task.Run(() => PeriodicBroadcastAsync(_cts.Token), _cts.Token);

            _logger.LogInformation("UDP Discovery service active on port {Port}", DiscoveryPort);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize UDP discovery service on port {Port}", DiscoveryPort);
        }
    }

    private async Task ListenForProbesAsync(CancellationToken token)
    {
        if (_udpClient == null) return;

        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(token);
                string message = Encoding.UTF8.GetString(result.Buffer).Trim();

                // Ignore own beacons and loopback packets to prevent feedback loops
                if (message.Contains("lantype_beacon", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (message.Contains("LANTYPE_PROBE", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Received discovery probe from {Endpoint}", result.RemoteEndPoint);
                    byte[] responseBytes = CreateBeaconPayloadBytes();
                    await _udpClient.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error processing UDP discovery probe");
            }
        }
    }

    private async Task PeriodicBroadcastAsync(CancellationToken token)
    {
        if (_udpClient == null) return;

        var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

        while (!token.IsCancellationRequested)
        {
            try
            {
                byte[] beacon = CreateBeaconPayloadBytes();
                await _udpClient.SendAsync(beacon, beacon.Length, broadcastEndpoint);

                // Also send specifically to standard Hotspot subnet broadcast (192.168.137.255)
                try
                {
                    var hotspotBroadcast = new IPEndPoint(IPAddress.Parse("192.168.137.255"), DiscoveryPort);
                    await _udpClient.SendAsync(beacon, beacon.Length, hotspotBroadcast);
                }
                catch { }

                await Task.Delay(2000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error in periodic UDP discovery broadcast");
                await Task.Delay(3000, token);
            }
        }
    }

    private byte[] CreateBeaconPayloadBytes()
    {
        var preferredIp = _networkManager.GetPreferredEndpointAddress();
        var beacon = new
        {
            type = "lantype_beacon",
            hostName = Environment.MachineName,
            ip = preferredIp.ToString(),
            port = _webSocketPort
        };

        string json = JsonSerializer.Serialize(beacon);
        return Encoding.UTF8.GetBytes(json);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _udpClient?.Dispose();
        _cts?.Dispose();
    }
}
