using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Cloudbreak.Core;
using Cloudbreak.Views;

namespace Cloudbreak;

public enum SettingsChange { Timing, Behavior, Corner, Look }

/// <summary>
/// Wires the engine to the tray, the windows and the clock.
/// One DispatcherTimer drives everything: every second while something is on screen,
/// otherwise a single wake-up at the next phase end or active-hours boundary.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly TrayIcon _tray;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _themeDebounce;
    private readonly RegisteredWaitHandle _showWait;
    private MainWindow? _main;
    private SettingsWindow? _settings;
    private MiniWindow? _mini;
    private ToastWindow? _toast;
    private BreakFog? _fog;
    private ContextMenu? _menu;
    private string _trayKey = "";
    private bool _displayOff, _locked;
    private bool? _efficiency;

    /// <summary>Nobody can see the screen: skip per-second redraws until they are back.</summary>
    private bool Away => _displayOff || _locked;

    public Settings S { get; }
    public TimerEngine Engine { get; }

    private bool _taskbarDark;
    private System.Windows.Media.Color _trayAccent;
    private readonly RegisteredWaitHandle _quitWait;

    public AppController(EventWaitHandle showSignal, EventWaitHandle quitSignal)
    {
        S = Settings.Load();
        Loc.I.Set(S.Language);
        Theme.Apply(S);
        CacheTrayTheme();

        Engine = new TimerEngine(S, DayLog.Load(AppClock.Now));
        Engine.PhaseChanged += OnPhaseChanged;
        Engine.StateChanged += UpdateTray;
        // After the engine has finished its own update: closing a window inside it would re-enter the clock.
        Engine.StateChanged += () => Application.Current.Dispatcher.BeginInvoke(SyncMini, DispatcherPriority.Background);

        _timer = new DispatcherTimer(DispatcherPriority.Normal);
        _timer.Tick += (_, _) => Tick();

        _themeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _themeDebounce.Tick += (_, _) => { _themeDebounce.Stop(); RefreshTheme(); };

        _tray = new TrayIcon();
        _tray.LeftClick += ToggleMain;
        _tray.RightClick += ShowMenu;
        _tray.SystemThemeChanged += () => { _themeDebounce.Stop(); _themeDebounce.Start(); };
        _tray.ClockJumped += Tick;
        _tray.DisplayOff += off => { _displayOff = off; AwayChanged(); };
        _tray.SessionLocked += locked => { _locked = locked; AwayChanged(); };

        var dispatcher = Application.Current.Dispatcher;
        _showWait = ThreadPool.RegisterWaitForSingleObject(showSignal,
            (_, _) => dispatcher.BeginInvoke(ShowMain), null, Timeout.Infinite, executeOnlyOnce: false);
        // "Cloudbreak.exe --quit" (used by the installer) asks this instance to close cleanly.
        _quitWait = ThreadPool.RegisterWaitForSingleObject(quitSignal,
            (_, _) => dispatcher.BeginInvoke(() => Application.Current.Shutdown()), null, Timeout.Infinite, executeOnlyOnce: true);
    }

    private void AwayChanged()
    {
        _main?.SetAway(Away);
        Tick();
    }

    private void CacheTrayTheme()
    {
        _taskbarDark = Theme.TaskbarIsDark;
        _trayAccent = Theme.AccentFor(S, _taskbarDark);
    }

    public void Start(bool showMain)
    {
        if (!Settings.LoadedFromFile)
        {
            // First run: respect the "Start with Windows" choice made in the installer.
            S.StartWithWindows = AutoStart.IsRegistered();
            S.Save();
        }
        Engine.Boot(AppClock.Now);
#if DEBUG
        // Testing aid (debug builds only): CLOUDBREAK_DEMO=hold|break seeds a realistic day for screenshots.
        var demo = Environment.GetEnvironmentVariable("CLOUDBREAK_DEMO");
        if (demo is "hold" or "break") Engine.DemoSeed(AppClock.Now, demo == "break");
        // Testing aid (debug builds only): CLOUDBREAK_OPEN=menu opens the tray menu, =alerts the Alerts settings, for screenshots.
        var open = Environment.GetEnvironmentVariable("CLOUDBREAK_OPEN");
        if (open == "alerts") { ShowSettings(); _settings!.NavAlerts.IsChecked = true; }
        if (open == "startstop")
        {
            // =startstop presses Start after 3 s and Stop after 6 s, to test what happens around a session.
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            int n = 0;
            t.Tick += (_, _) => { if (++n == 1) PrimaryAction(); else { t.Stop(); StopLoop(); } };
            t.Start();
        }
        if (open == "menu")
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (_, _) => { t.Stop(); ShowMenu(); };
            t.Start();
        }
#endif
        UpdateTray();
        SyncMini();
        if (showMain) ShowMain();
        Tick();
        TrimLater(TimeSpan.FromSeconds(8));
    }

    // ---------- Clock ----------

    private void Tick()
    {
        var now = AppClock.Now;
        Engine.Update(now);
        UpdateTray(); // cheap: redraws only when the ring gains a step
        if (!Away)
        {
            if (_main?.IsOnScreen == true) _main.Refresh(now);
            if (_mini?.IsOnScreen == true) _mini.Refresh(now);
        }
        UpdateEfficiency();
        Schedule(now);
    }

    /// <summary>Efficiency mode whenever no big window (main, settings) is open and the fog is not drifting.</summary>
    private void UpdateEfficiency()
    {
        bool background = _main == null && _settings == null && _fog == null;
        if (_efficiency == background) return;
        _efficiency = background;
        Native.SetEfficiencyMode(background);
    }

    /// <summary>Call when visibility changes so the tick rate follows.</summary>
    public void Reschedule() => Tick();

    private void Schedule(DateTime now)
    {
        _timer.Stop();
        bool visible = !Away && (_main?.IsOnScreen == true || _mini?.IsOnScreen == true);
        TimeSpan next;
        if (visible && Engine.State == RunState.Running)
        {
            // Wake exactly when the displayed second changes.
            double ms = Engine.Remaining(now).TotalMilliseconds % 1000;
            next = TimeSpan.FromMilliseconds(ms < 15 ? 1000 + ms + 5 : ms + 5);
        }
        else
        {
            next = Engine.NextWakeup(now);
            var ring = NextRingStep(now);
            if (ring < next) next = ring + TimeSpan.FromMilliseconds(20); // tray ring: one wake-up per minute
            if (visible && next > TimeSpan.FromSeconds(30)) next = TimeSpan.FromSeconds(30); // keep the "now" marker moving
        }
        if (next > TimeSpan.FromHours(1)) next = TimeSpan.FromHours(1);
        _timer.Interval = next;
        _timer.Start();
    }

    // ---------- Engine events ----------

    private void OnPhaseChanged(Phase phase, Transition kind)
    {
        UpdateTray();
        if (phase != Phase.Break) _fog?.FadeAway();
        if (kind == Transition.Silent) return;
        if (S.Sound) Chime.Play(phase == Phase.Break);
        if (phase == Phase.Break && S.Fog && !Away)
        {
            ShowFog();
            _mini?.BringToFront(); // the chip stays readable above the fog
        }
        if (S.Notifications) ShowToast(phase, kind); // last, so the toast and its buttons sit above both
    }

    private void ShowFog()
    {
        _fog?.Close();
        var fog = new BreakFog();
        fog.Finished += () =>
        {
            if (!ReferenceEquals(_fog, fog)) return;
            _fog = null;
            UpdateEfficiency();
            TrimLater(TimeSpan.FromSeconds(2));
        };
        _fog = fog;
        UpdateEfficiency(); // smooth drift needs normal timers for these few seconds
        fog.Show();
    }

    private void ShowToast(Phase phase, Transition kind)
    {
        var L = Loc.I;
        string title, body;
        var now = AppClock.Now;
        if (phase == Phase.Break)
        {
            title = L["tBreakTitle"];
            body = S.BreakMinutes == 1
                ? L.F("tBreakOne", Fmt.Clock(Engine.PhaseEnd))
                : L.F("tBreak", S.BreakMinutes, Fmt.Clock(Engine.PhaseEnd));
        }
        else
        {
            title = kind == Transition.AutoStart ? L["tStartTitle"] : L["tHoldTitle"];
            body = L.F("tHold", S.HoldMinutes, Fmt.Clock(Engine.PhaseEnd));
        }
        _toast?.Close();
        _toast = new ToastWindow(this, title, body, phase == Phase.Break);
        _toast.Closed += (s, _) => { if (ReferenceEquals(_toast, s)) _toast = null; };
        _toast.Show();
    }

    // ---------- Actions ----------

    public void PrimaryAction()
    {
        var now = AppClock.Now;
        switch (Engine.State)
        {
            case RunState.Running: Engine.Pause(now); break;
            case RunState.Paused: Engine.Resume(now); break;
            default: Engine.Start(now); break;
        }
        Tick();
    }

    public void SkipBreak()
    {
        Engine.SkipBreak(AppClock.Now);
        _toast?.Dismiss();
        _fog?.FadeAway();
        Tick();
    }

    public void StopLoop()
    {
        Engine.Stop(AppClock.Now);
        _fog?.FadeAway();
        Tick();
    }

    public void ApplySettings(SettingsChange what)
    {
        S.Save();
        var now = AppClock.Now;
        switch (what)
        {
            case SettingsChange.Timing:
                Engine.SettingsChanged(now);
                break;
            case SettingsChange.Behavior:
                AutoStart.Apply(S.StartWithWindows);
                if (!S.Fog) _fog?.FadeAway();
                SyncMini();
                break;
            case SettingsChange.Look:
                Loc.I.Set(S.Language);
                RefreshTheme();
                break;
        }
        _trayKey = "";
        UpdateTray();
        Tick();
    }

    // ---------- Windows ----------

    public void ShowMain()
    {
        if (_main == null)
        {
            _main = new MainWindow(this);
            _main.Closed += (_, _) => { _main = null; Reschedule(); TrimLater(TimeSpan.FromSeconds(2)); };
            _main.Refresh(AppClock.Now);
            _main.Show();
        }
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
        Reschedule();
    }

    private void ToggleMain()
    {
        if (_main != null && _main.IsActive && _main.WindowState != WindowState.Minimized) _main.Close();
        else ShowMain();
    }

    public void ShowSettings()
    {
        if (_settings == null)
        {
            _settings = new SettingsWindow(this);
            _settings.Closed += (_, _) => { _settings = null; UpdateEfficiency(); TrimLater(TimeSpan.FromSeconds(2)); };
            UpdateEfficiency();
            _settings.Show();
        }
        if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
        _settings.Activate();
    }

    /// <summary>
    /// The chip shows when it is on in Settings and a session is going (running or paused).
    /// When the timer is stopped, or waits outside the active hours, there is nothing to count: it hides.
    /// </summary>
    private void SyncMini()
    {
        if (S.MiniTimer && Engine.IsActive) ShowMini();
        else if (_mini != null)
        {
            var m = _mini;
            _mini = null;
            m.Close();
        }
    }

    private void ShowMini()
    {
        if (_mini != null) return;
        _mini = new MiniWindow(this);
        // Alt+F4 can close it: forget it so it can be shown again
        _mini.Closed += (s, _) => { if (ReferenceEquals(_mini, s)) _mini = null; };
        _mini.Refresh(AppClock.Now);
        _mini.Show();
    }

    // ---------- Tray ----------

    /// <summary>
    /// Tray icon = one round. The long arc is the hold, the short arc the break; they fill as time passes.
    /// Progress is rounded to whole phase minutes, so the icon is redrawn at most once a minute.
    /// </summary>
    private void UpdateTray()
    {
        // Registry values are cached in CacheTrayTheme: this runs every tick while something is on screen.
        var neutral = _taskbarDark ? Theme.Rgb(0xF2, 0xF3, 0xF5) : Theme.Rgb(0x1D, 0x20, 0x27);
        var dim = Theme.Rgb(0x8E, 0x93, 0xA0);
        var accent = _trayAccent;
        static System.Windows.Media.Color A(System.Windows.Media.Color c, byte a) => Theme.WithAlpha(c, a);

        var now = AppClock.Now;
        var L = Loc.I;
        double holdDone = 0, breakDone = 0;
        string tip;
        IconFactory.RingColors col;

        if (Engine.IsActive)
        {
            int phaseMin = Engine.Phase == Phase.Hold ? S.HoldMinutes : S.BreakMinutes;
            double q = Math.Floor(Engine.Progress(now) * phaseMin) / Math.Max(1, phaseMin);
            if (Engine.Phase == Phase.Hold) holdDone = q; else { holdDone = 1; breakDone = q; }

            bool paused = Engine.State == RunState.Paused;
            col = new IconFactory.RingColors(
                HoldTrack: A(neutral, 84), BreakTrack: A(accent, 110),
                HoldFill: paused ? dim : neutral, BreakFill: paused ? dim : accent,
                Dot: paused ? dim : accent);
            tip = paused ? L["tipPaused"]
                : L.F(Engine.Phase == Phase.Hold ? "tipHold" : "tipBreak", Fmt.Clock(Engine.PhaseEnd));
        }
        else
        {
            col = new IconFactory.RingColors(A(dim, 150), A(dim, 110), dim, dim, dim);
            tip = Engine.State == RunState.OffHours
                ? L.F("tipOff", Settings.FormatTime(S.ActiveStartTime))
                : L["tipIdle"];
        }

        string key = $"{col}{holdDone:F3}{breakDone:F3}{S.HoldMinutes}/{S.BreakMinutes}{tip}";
        if (key == _trayKey && _tray.IsShown) return;
        _trayKey = key;
        int px = Native.GetSystemMetrics(Native.SM_CXSMICON);
        if (px <= 0) px = 16;
        _tray.Update(IconFactory.CreateRoundHIcon(px, S.HoldMinutes, S.BreakMinutes, holdDone, breakDone, col), tip);
#if DEBUG
        DumpIconForTesting(holdDone, breakDone, col);
#endif
    }

#if DEBUG
    /// <summary>Testing aid (debug builds only): CLOUDBREAK_DUMP_ICON=folder writes the tray ring as PNGs.</summary>
    private void DumpIconForTesting(double holdDone, double breakDone, IconFactory.RingColors col)
    {
        var dir = Environment.GetEnvironmentVariable("CLOUDBREAK_DUMP_ICON");
        if (string.IsNullOrEmpty(dir)) return;
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            foreach (int size in new[] { 128, 16 })
            {
                var dv = new System.Windows.Media.DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRectangle(new System.Windows.Media.SolidColorBrush(Theme.TaskbarIsDark ? Theme.Rgb(0x20, 0x20, 0x20) : Theme.Rgb(0xEE, 0xEE, 0xEE)), null, new Rect(0, 0, size, size));
                    IconFactory.DrawRound(dc, size, S.HoldMinutes, S.BreakMinutes, holdDone, breakDone, col);
                }
                var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bmp.Render(dv);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                using var fs = System.IO.File.Create(System.IO.Path.Combine(dir, $"tray-{Engine.Phase}-{(int)(holdDone * 100):000}-{(int)(breakDone * 100):000}-{size}.png"));
                enc.Save(fs);
            }
        }
        catch { }
    }
#endif

    /// <summary>When the tray ring next gains a step (one phase minute), while running.</summary>
    private TimeSpan NextRingStep(DateTime now)
    {
        if (Engine.State != RunState.Running) return TimeSpan.MaxValue;
        int phaseMin = Engine.Phase == Phase.Hold ? S.HoldMinutes : S.BreakMinutes;
        var step = Engine.Length(Engine.Phase) / Math.Max(1, phaseMin);
        var elapsed = Engine.ActiveTime(now); // pauses excluded, like the ring itself
        return step * (Math.Floor(elapsed / step) + 1) - elapsed;
    }

    public void ShowMenu()
    {
        _menu ??= new ContextMenu();
        var m = _menu;
        m.Items.Clear();
        var L = Loc.I;

        void Item(string text, Action act, bool enabled = true)
        {
            var mi = new MenuItem { Header = text, IsEnabled = enabled };
            mi.Click += (_, _) => act();
            m.Items.Add(mi);
        }

        bool active = Engine.IsActive;
        Item(Engine.State switch
        {
            RunState.Running => L["pause"],
            RunState.Paused => L["resume"],
            _ => L["start"],
        }, PrimaryAction);
        Item(L["skipBreak"], SkipBreak, active && Engine.Phase == Phase.Break);
        if (active) Item(L["stop"], StopLoop);
        m.Items.Add(new Separator());
        Item(L["open"], ShowMain);
        var mini = new MenuItem { Header = L["mini"], IsCheckable = true, IsChecked = S.MiniTimer };
        mini.Click += (_, _) =>
        {
            S.MiniTimer = mini.IsChecked;
            ApplySettings(SettingsChange.Behavior);
            _settings?.Reload();
        };
        m.Items.Add(mini);
        Item(L["settings"], ShowSettings);
        m.Items.Add(new Separator());
        Item(L["quit"], () => Application.Current.Shutdown());

        Native.GetCursorPos(out var p);
        double scale = Native.GetDpiForSystem() / 96.0;
        m.Placement = PlacementMode.AbsolutePoint;
        m.HorizontalOffset = p.X / scale - 10;
        m.VerticalOffset = p.Y / scale - 10;
        m.IsOpen = true;

        // The menu must own the foreground, or it will not close when clicking elsewhere.
        if (PresentationSource.FromVisual(m) is HwndSource src) Native.SetForegroundWindow(src.Handle);
    }

    // ---------- Theme and memory ----------

    private void RefreshTheme()
    {
        Theme.Apply(S);
        CacheTrayTheme();
        _main?.ApplyTheme();
        _settings?.ApplyTheme();
        _mini?.ApplyTheme();
        _trayKey = "";
        UpdateTray();
    }

    private DispatcherTimer? _trim;

    /// <summary>After big windows close, give memory back to Windows.</summary>
    private void TrimLater(TimeSpan delay)
    {
        _trim?.Stop();
        _trim = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = delay };
        _trim.Tick += (_, _) =>
        {
            _trim?.Stop();
            _trim = null;
            if (_main == null && _settings == null && _toast == null && _fog == null) Native.TrimWorkingSet();
        };
        _trim.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _fog?.Close();
        _showWait.Unregister(null);
        _quitWait.Unregister(null);
        _tray.Dispose();
        Engine.Shutdown(AppClock.Now); // keep the minutes of the phase in progress
        Engine.Log.Save();
    }
}
