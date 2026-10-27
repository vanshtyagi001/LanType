using LANType.Service.Engine;
using LANType.Service.Ipc;
using LANType.Service.Keyboard;
using LANType.Service.Network;
using LANType.Service.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LANType.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly LANWebSocketServer _wsServer;
    private readonly DesktopIpcServer _ipcServer;
    private readonly NetworkInterfaceManager _networkManager;
    private readonly IHidKeyboardInterface _keyboard;
    private readonly TypingScheduler _scheduler;
    private readonly UdpDiscoveryService _discoveryService;

    public Worker(
        ILogger<Worker> logger,
        LANWebSocketServer wsServer,
        DesktopIpcServer ipcServer,
        NetworkInterfaceManager networkManager,
        IHidKeyboardInterface keyboard,
        TypingScheduler scheduler,
        UdpDiscoveryService discoveryService)
    {
        _logger = logger;
        _wsServer = wsServer;
        _ipcServer = ipcServer;
        _networkManager = networkManager;
        _keyboard = keyboard;
        _scheduler = scheduler;
        _discoveryService = discoveryService;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("==================================================");
        _logger.LogInformation("   LANType Windows Background Service Starting    ");
        _logger.LogInformation("==================================================");

        var ip = _networkManager.GetPreferredEndpointAddress();
        _logger.LogInformation("Local Network Host IP: {IP}", ip);

        var driverStatus = _keyboard.GetStatus();
        _logger.LogInformation("Keyboard Interface Status: {Status} (Fallback: {Fallback})", 
            driverStatus.State, driverStatus.IsDevReceiverFallback);

        _wsServer.Start();
        _ipcServer.Start();
        _discoveryService.Start();

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(30000, stoppingToken);
                _logger.LogDebug("LANType Service Health: Running, SessionState: {State}", _scheduler.CurrentState);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("LANType Windows Service is stopping. Releasing all keys...");
        await _keyboard.ReleaseAllKeysAsync(CancellationToken.None);
        _discoveryService.Dispose();
        _wsServer.Stop();
        _ipcServer.Dispose();

        _logger.LogInformation("LANType Windows Service shutdown complete.");
        await base.StopAsync(cancellationToken);
    }
}
