using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Cloudbreak.Core;

namespace Cloudbreak.Views;

/// <summary>Small always-on-top chip with the time left. Drag to move, double-click to open, right-click for the menu.</summary>
public partial class MiniWindow : Window
{
    private readonly AppController _c;
    private readonly SolidColorBrush _dot = new();
    private bool? _shownBreak;

    public MiniWindow(AppController c)
    {
        _c = c;
        InitializeComponent();
        Dot.Fill = _dot;
        Dot.Stroke = _dot;
        SourceInitialized += (_, _) => Native.AddExStyle(this, Native.WS_EX_TOOLWINDOW);
        DotAlign.Apply(Dot, TimeText, "0");
        Loaded += (_, _) => PlaceInitially();
        IsVisibleChanged += (_, _) => _c.Reschedule();
    }

    public bool IsOnScreen => IsVisible;

    private void PlaceInitially()
    {
        var wa = SystemParameters.WorkArea;
        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;
        if (_c.S.MiniLeft is double l && _c.S.MiniTop is double t &&
            l >= vl - 20 && t >= vt - 20 && l + ActualWidth <= vr + 20 && t + ActualHeight <= vb + 20)
        {
            Left = l;
            Top = t;
        }
        else
        {
            // Default: bottom right, just above the taskbar (notifications default to the top right).
            Left = wa.Right - ActualWidth - 8;
            Top = wa.Bottom - ActualHeight - 8;
        }
    }

    public void ApplyTheme()
    {
        _shownBreak = null;
        Refresh(AppClock.Now);
    }

    public void Refresh(DateTime now)
    {
        var e = _c.Engine;
        bool isBreak = e.IsActive && e.Phase == Phase.Break;
        if (_shownBreak != isBreak)
        {
            _shownBreak = isBreak;
            Pill.Background = isBreak ? new SolidColorBrush(Theme.BreakBg) : (Brush)FindResource("PaperBrush");
            _dot.Color = isBreak ? Theme.Accent : Theme.Hold;
        }
        Dot.Fill = e.State == RunState.Running ? _dot : Brushes.Transparent;

        TimeText.Text = Fmt.Duration(e.Remaining(now));
        var L = Loc.I;
        LabelText.Text = e.State switch
        {
            RunState.Running => L[e.Phase == Phase.Hold ? "hold" : "break"],
            RunState.Paused => L["paused"],
            RunState.OffHours => L["offHours"],
            _ => L["ready"],
        };
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ClickCount == 2) { _c.ShowMain(); return; }
        try { DragMove(); } catch (InvalidOperationException) { return; }
        _c.S.MiniLeft = Left;
        _c.S.MiniTop = Top;
        _c.S.Save();
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        _c.ShowMenu();
        e.Handled = true;
    }
}
