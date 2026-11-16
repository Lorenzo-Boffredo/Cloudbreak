using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Cloudbreak.Core;

namespace Cloudbreak.Views;

public partial class MainWindow : Window
{
    private readonly AppController _c;
    private readonly SolidColorBrush _bg = new();
    private SolidColorBrush _phase = new();
    private Storyboard? _drift;
    private Brush _timerBreak = Brushes.Black;
    private bool? _shownBreak;
    private string _hintKey = "";

    public MainWindow(AppController c)
    {
        _c = c;
        InitializeComponent();
        Root.Background = _bg;
        Strip.Engine = c.Engine;
        Strip.Settings = c.S;
        Topmost = c.S.MainTopmost;
        UpdatePin();
        ApplyColors(animate: false);
        DotAlign.Apply(PillDot, PhaseText, "H");

        SourceInitialized += (_, _) => Native.StyleWindow(this, Theme.IsDark);
        IsVisibleChanged += (_, _) => UpdateDrift();
        StateChanged += (_, _) => { UpdateDrift(); _c.Reschedule(); };
        Loc.I.PropertyChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? s, PropertyChangedEventArgs e) => Refresh(AppClock.Now);

    protected override void OnClosed(EventArgs e)
    {
        Loc.I.PropertyChanged -= OnLanguageChanged;
        _drift?.Stop(this);
        base.OnClosed(e);
    }

    public bool IsOnScreen => IsVisible && WindowState != WindowState.Minimized;

    private bool _away;

    /// <summary>Screen off or PC locked: stop the break glow animation until someone is back.</summary>
    public void SetAway(bool away)
    {
        _away = away;
        UpdateDrift();
    }

    public void ApplyTheme()
    {
        Native.StyleWindow(this, Theme.IsDark);
        _shownBreak = null;
        ApplyColors(animate: false);
        Strip.InvalidateVisual();
    }

    public void Refresh(DateTime now)
    {
        var e = _c.Engine;
        bool isBreak = e.IsActive && e.Phase == Phase.Break;
        if (_shownBreak != isBreak) ApplyColors(animate: _shownBreak != null);

        TimerText.Text = Fmt.Duration(e.Remaining(now));
        TimerText.Foreground = e.State == RunState.Paused
            ? (Brush)FindResource("MutedBrush")
            : isBreak ? _timerBreak : (Brush)FindResource("InkBrush");

        var L = Loc.I;
        PhaseText.Text = e.State switch
        {
            RunState.Running => L[e.Phase == Phase.Hold ? "hold" : "break"],
            RunState.Paused => L["paused"],
            RunState.OffHours => L["offHours"],
            _ => L["ready"],
        };
        PillDot.Fill = e.State == RunState.Running ? _phase : Brushes.Transparent;
        RoundText.Text = e.IsActive ? L.F("round", e.Round) : "";

        // Hint with the time in bold
        string key; string? time = null; string? arg = null;
        switch (e.State)
        {
            case RunState.Running:
                key = e.Phase == Phase.Hold ? "hintHold" : "hintBreak";
                time = Fmt.Clock(e.PhaseEnd);
                break;
            case RunState.Paused: key = "hintPaused"; break;
            case RunState.OffHours: key = "hintOutside"; time = Settings.FormatTime(_c.S.ActiveStartTime); break;
            default: key = "hintIdle"; arg = _c.S.HoldMinutes.ToString(); break;
        }
        string hintId = key + time + arg + L.Code;
        if (hintId != _hintKey)
        {
            _hintKey = hintId;
            SetHint(L[key], time, arg);
        }

        BarScale.ScaleX = e.Progress(now);

        PrimaryBtn.Content = e.State switch
        {
            RunState.Running => L["pause"],
            RunState.Paused => L["resume"],
            _ => L["start"],
        };
        SkipBtn.Visibility = isBreak ? Visibility.Visible : Visibility.Collapsed;
        StopBtn.Visibility = e.IsActive ? Visibility.Visible : Visibility.Collapsed;

        RangeText.Text = _c.S.ActiveHoursEnabled
            ? $"{Settings.FormatTime(_c.S.ActiveStartTime)}–{Settings.FormatTime(_c.S.ActiveEndTime)}"
            : L["allDay"];

        HoldsText.Text = e.Log.HoldsDone.ToString();
        FreeText.Text = Fmt.Span(TimeSpan.FromSeconds(e.VapeFreeSeconds(now)));
        SkipText.Text = e.Log.BreaksSkipped.ToString();

        // The day strip only changes visibly about once a minute: skip the other 59 redraws.
        string stripKey = $"{now:HHmm}{e.State}{e.Phase}{e.PhaseStart.Ticks}{_c.S.HoldMinutes}/{_c.S.BreakMinutes}{_c.S.ActiveStart}{_c.S.ActiveEnd}{_c.S.ActiveHoursEnabled}{e.Log.Segments.Count}";
        if (stripKey != _stripKey)
        {
            _stripKey = stripKey;
            Strip.Now = now;
            Strip.InvalidateVisual();
        }
    }

    private string _stripKey = "";

    private void SetHint(string template, string? time, string? arg)
    {
        HintText.Inlines.Clear();
        if (arg != null) template = template.Replace("{0}", arg);
        if (time == null) { HintText.Inlines.Add(new Run(template)); return; }
        int i = template.IndexOf("{0}", StringComparison.Ordinal);
        if (i < 0) { HintText.Inlines.Add(new Run(template)); return; }
        HintText.Inlines.Add(new Run(template[..i]));
        HintText.Inlines.Add(new Run(time) { FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("InkBrush") });
        HintText.Inlines.Add(new Run(template[(i + 3)..]));
    }

    /// <summary>Tints the window for the phase: monochrome for holds, accent for breaks.</summary>
    private void ApplyColors(bool animate)
    {
        var e = _c.Engine;
        bool isBreak = e.IsActive && e.Phase == Phase.Break;
        _shownBreak = isBreak;

        Color bg = isBreak ? Theme.BreakBg : Theme.Paper;
        Color ph = isBreak ? Theme.Accent : Theme.Hold;
        Color soft = isBreak ? (Color)FindResource("AccentPillColor") : Theme.HoldSoft;

        _timerBreak = new SolidColorBrush(Theme.Mix(Theme.Accent, (Color)FindResource("InkColor"), 0.6));
        _timerBreak.Freeze();

        var a = Theme.Accent;
        VaporIn.Color = Color.FromArgb(Theme.IsDark ? (byte)80 : (byte)92, a.R, a.G, a.B);
        VaporMid.Color = Color.FromArgb(Theme.IsDark ? (byte)30 : (byte)38, a.R, a.G, a.B);
        VaporOut.Color = Color.FromArgb(0, a.R, a.G, a.B);

        bool motion = animate && SystemParameters.ClientAreaAnimation;
        var d = TimeSpan.FromMilliseconds(motion ? 900 : 0);
        Animate(_bg, bg, d);
        // Brushes stored in a ResourceDictionary get sealed, so phase colors are swapped, not animated.
        Resources["PhaseBrush"] = Frozen(ph);
        Resources["PhaseSoftBrush"] = Frozen(soft);
        _phase = (SolidColorBrush)Resources["PhaseBrush"];
        Vapor.BeginAnimation(OpacityProperty, new DoubleAnimation(isBreak ? 1 : 0, TimeSpan.FromMilliseconds(motion ? 1200 : 0)));
        UpdateDrift();
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static void Animate(SolidColorBrush b, Color to, TimeSpan d)
    {
        if (d == TimeSpan.Zero) { b.BeginAnimation(SolidColorBrush.ColorProperty, null); b.Color = to; return; }
        b.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, d) { EasingFunction = new QuadraticEase() });
    }

    /// <summary>The glow drifts slowly, only while a break is on screen. Capped at 20 fps.</summary>
    private void UpdateDrift()
    {
        bool run = _shownBreak == true && IsOnScreen && !_away && SystemParameters.ClientAreaAnimation;
        if (run && _drift == null)
        {
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            var dur = TimeSpan.FromSeconds(14);
            _drift = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
            Timeline.SetDesiredFrameRate(_drift, 20);
            Add(_drift, new DoubleAnimation(-22, 24, dur) { EasingFunction = ease }, "VaporMove", TranslateTransform.XProperty);
            Add(_drift, new DoubleAnimation(6, -10, dur) { EasingFunction = ease }, "VaporMove", TranslateTransform.YProperty);
            Add(_drift, new DoubleAnimation(1, 1.12, dur) { EasingFunction = ease }, "VaporScale", ScaleTransform.ScaleXProperty);
            Add(_drift, new DoubleAnimation(1, 1.12, dur) { EasingFunction = ease }, "VaporScale", ScaleTransform.ScaleYProperty);
            _drift.Begin(this, true);
        }
        else if (!run && _drift != null)
        {
            _drift.Stop(this);
            _drift = null;
        }
    }

    private static void Add(Storyboard sb, Timeline t, string target, DependencyProperty p)
    {
        Storyboard.SetTargetName(t, target);
        Storyboard.SetTargetProperty(t, new PropertyPath(p));
        sb.Children.Add(t);
    }

    private void UpdatePin()
    {
        PinBtn.Content = Topmost ? "" : "";
        PinBtn.Foreground = Topmost ? (Brush)FindResource("InkBrush") : (Brush)FindResource("MutedBrush");
    }

    // ---------- Events ----------
    private void Primary_Click(object s, RoutedEventArgs e) => _c.PrimaryAction();
    private void Skip_Click(object s, RoutedEventArgs e) => _c.SkipBreak();
    private void Stop_Click(object s, RoutedEventArgs e) => _c.StopLoop();
    private void Settings_Click(object s, RoutedEventArgs e) => _c.ShowSettings();
    private void Min_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object s, RoutedEventArgs e) => Close();

    private void Pin_Click(object s, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _c.S.MainTopmost = Topmost;
        _c.S.Save();
        UpdatePin();
    }
}
