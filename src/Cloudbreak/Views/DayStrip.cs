using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Cloudbreak.Core;

namespace Cloudbreak.Views;

/// <summary>Today's holds and breaks inside the active hours, with a marker for now.</summary>
public sealed class DayStrip : FrameworkElement
{
    private const double BarH = 26;

    public TimerEngine? Engine { get; set; }
    public Settings? Settings { get; set; }
    public DateTime Now { get; set; } = AppClock.Now;

    private (DateTime start, DateTime end) Range()
    {
        var s = Settings!;
        var today = Now.Date;
        if (!s.ActiveHoursEnabled || s.ActiveStartTime == s.ActiveEndTime) return (today, today.AddDays(1));
        var a = s.ActiveStartTime;
        var b = s.ActiveEndTime;
        if (a < b) return (today + a, today + b);
        // Overnight window
        return Now.TimeOfDay < b ? (today.AddDays(-1) + a, today + b) : (today + a, today.AddDays(1) + b);
    }

    private Brush Res(string key) => (Brush)(TryFindResource(key) ?? Brushes.Gray);

    protected override void OnRender(DrawingContext dc)
    {
        if (Engine == null || Settings == null) return;
        double w = ActualWidth;
        if (w <= 0) return;

        var (r0, r1) = Range();
        double total = (r1 - r0).TotalSeconds;
        double X(DateTime t) => Math.Clamp((t - r0).TotalSeconds / total, 0, 1) * w;

        var holdB = Res("StripHoldBrush");
        var breakB = Res("AccentBrush");

        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, BarH), 5, 5));
        dc.DrawRectangle(Res("StripBgBrush"), null, new Rect(0, 0, w, BarH));

        void Seg(Segment s)
        {
            if (s.Skipped || s.End <= r0 || s.Start >= r1) return;
            double x0 = X(s.Start), x1 = X(s.End);
            if (x1 - x0 < 0.5) return;
            dc.DrawRectangle(s.Phase == Phase.Hold ? holdB : breakB, null, new Rect(x0, 0, x1 - x0, BarH));
        }

        foreach (var s in Engine.Log.Segments) Seg(s);
        if (Engine.CurrentSegment(Now) is Segment cur) Seg(cur);
        foreach (var s in Engine.Projection(Now, r1)) Seg(s);

        double nx = X(Now);
        dc.DrawRectangle(Res("StripFutureBrush"), null, new Rect(nx, 0, Math.Max(0, w - nx), BarH));
        dc.DrawRectangle(Res("InkBrush"), null, new Rect(Math.Clamp(nx - 1, 0, w - 2), 0, 2, BarH));
        dc.Pop();

        // Hour labels
        var muted = Res("MutedBrush");
        double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var face = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        FormattedText Label(string t) => new(t, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 11, muted, dip);
        double y = BarH + 6;

        dc.DrawText(Label(r0.ToString("HH:mm")), new Point(0, y));
        var last = Label(r1 - r0 >= TimeSpan.FromDays(1) && r1.TimeOfDay == TimeSpan.Zero ? "24:00" : r1.ToString("HH:mm"));
        dc.DrawText(last, new Point(w - last.Width, y));

        var step = TimeSpan.FromHours(3);
        var t = new DateTime(r0.Year, r0.Month, r0.Day, r0.Hour, 0, 0);
        while (t.Hour % 3 != 0 || t <= r0) t = t.AddHours(1);
        for (; t < r1; t += step)
        {
            double p = (t - r0).TotalSeconds / total;
            if (p < 0.16 || p > 0.82) continue;
            var ft = Label(t.ToString("HH:mm"));
            dc.DrawText(ft, new Point(p * w - ft.Width / 2, y));
        }
    }
}
