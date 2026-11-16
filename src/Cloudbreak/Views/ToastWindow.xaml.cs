using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Cloudbreak.Core;

namespace Cloudbreak.Views;

/// <summary>Cloudbreak's own notification, shown in the corner picked in Settings. Never steals focus.</summary>
public partial class ToastWindow : Window
{
    private readonly AppController _c;
    private readonly DispatcherTimer _life = new() { Interval = TimeSpan.FromSeconds(9) };
    private bool _closing;

    public ToastWindow(AppController c, string title, string body, bool isBreak)
    {
        _c = c;
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;
        Actions.Visibility = isBreak ? Visibility.Visible : Visibility.Collapsed;
        if (isBreak) Card.Background = new SolidColorBrush(Theme.BreakBg);

        SourceInitialized += (_, _) => Native.AddExStyle(this, Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        Loaded += (_, _) => { Place(); AnimateIn(); };
        MouseEnter += (_, _) => _life.Stop();
        MouseLeave += (_, _) => { if (!_closing) _life.Start(); };
        _life.Tick += (_, _) => Dismiss();
    }

    private string Corner => _c.S.NotificationCorner;

    private void Place()
    {
        var wa = SystemParameters.WorkArea;
        const double edge = 4; // the card already has a 16px margin for its shadow
        Left = Corner.EndsWith('l') ? wa.Left + edge : wa.Right - ActualWidth - edge;
        Top = Corner.StartsWith('t') ? wa.Top + edge : wa.Bottom - ActualHeight - edge;
    }

    private void AnimateIn()
    {
        bool motion = SystemParameters.ClientAreaAnimation;
        double from = Corner.StartsWith('t') ? -10 : 10;
        var d = TimeSpan.FromMilliseconds(motion ? 220 : 0);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(from, 0, d) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, d));
        _life.Start();
    }

    public void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        _life.Stop();
        var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 180 : 0));
        anim.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, anim);
    }

    protected override void OnClosed(EventArgs e)
    {
        _life.Stop(); // a toast replaced by a newer one must not fire later
        base.OnClosed(e);
    }

    private void Dismiss_Click(object s, RoutedEventArgs e) => Dismiss();

    private void Skip_Click(object s, RoutedEventArgs e)
    {
        _c.SkipBreak();
        Dismiss();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (e.OriginalSource is not System.Windows.Controls.Button) _c.ShowMain();
    }
}
