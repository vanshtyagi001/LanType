using LANType.Service;
using LANType.Service.Auth;
using LANType.Service.Engine;
using LANType.Service.Ipc;
using LANType.Service.Keyboard;
using LANType.Service.Network;
using LANType.Service.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Enable running as a Windows Service with SCM integration
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "LANTypeService";
});

// Configure structured logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
#pragma warning disable CA1416 // Validate platform compatibility
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(settings =>
    {
        settings.SourceName = "LANTypeService";
    });
}
#pragma warning restore CA1416

// Register core services
builder.Services.AddSingleton<NetworkInterfaceManager>();
builder.Services.AddSingleton<PairingManager>();
builder.Services.AddSingleton<VhfDriverKeyboardInterface>();
builder.Services.AddSingleton<DevTestKeyboardReceiver>();
builder.Services.AddSingleton<KeyboardManager>();
builder.Services.AddSingleton<IHidKeyboardInterface>(sp => sp.GetRequiredService<KeyboardManager>());
builder.Services.AddSingleton<TypingScheduler>();
builder.Services.AddSingleton<LANWebSocketServer>();
builder.Services.AddSingleton<DesktopIpcServer>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
