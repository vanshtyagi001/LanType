using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace LANType.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static System.Threading.Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new System.Threading.Mutex(true, "LANType.Desktop.SingleInstanceMutex", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogCrash(args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            LogCrash(args.Exception);
            args.Handled = false;
        };

        TerminateCollidingServices();
    }

    private static void TerminateCollidingServices()
    {
        try
        {
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("LANType.Service"))
            {
                try { proc.Kill(); } catch { }
            }
        }
        catch { }
    }

    private static void LogCrash(Exception? ex)
    {
        if (ex == null) return;
        try
        {
            string logDir = @"C:\ProgramData\LANType\logs";
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            string logFile = Path.Combine(logDir, "desktop_crash.log");
            File.AppendAllText(logFile, $"[{DateTime.Now:O}] Unhandled Exception: {ex}\n\n");
        }
        catch
        {
            // Ignore logging failures
        }
    }
}
