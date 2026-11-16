using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Cloudbreak;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) =>
        {
            // Keep the timer alive; write the error next to the settings for diagnosis.
            try
            {
                System.IO.Directory.CreateDirectory(Core.Settings.DataDir);
                var log = System.IO.Path.Combine(Core.Settings.DataDir, "error.log");
                // Keep the log small even if an error repeats every second.
                if (System.IO.File.Exists(log) && new System.IO.FileInfo(log).Length > 256 * 1024)
                    System.IO.File.Delete(log);
                System.IO.File.AppendAllText(log, $"{DateTime.Now:u} {ex.Exception}{Environment.NewLine}");
            }
            catch { }
            ex.Handled = true;
        };

        bool Has(string arg) => e.Args.Any(a => a.Equals(arg, StringComparison.OrdinalIgnoreCase));

        _mutex = new Mutex(true, @"Local\Cloudbreak.SingleInstance", out _ownsMutex);
        var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Cloudbreak.Show");
        var quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Cloudbreak.Quit");

        if (Has("--quit"))
        {
            // Used by the installer: close the running copy cleanly (tray icon removed, today's log saved).
            if (!_ownsMutex)
            {
                quitSignal.Set();
                try { _ownsMutex = _mutex.WaitOne(TimeSpan.FromSeconds(5)); }
                catch (AbandonedMutexException) { _ownsMutex = true; }
            }
            Shutdown();
            return;
        }

        if (!_ownsMutex)
        {
            // Already running: ask the first instance to show its window.
            showSignal.Set();
            Shutdown();
            return;
        }

        bool fromStartup = Has("--startup");
        _controller = new AppController(showSignal, quitSignal);
        _controller.Start(showMain: !fromStartup);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
