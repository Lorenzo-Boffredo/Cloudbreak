using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cloudbreak.Core;

/// <summary>
/// The Cloudbreak mark: one round. A long arc for the hold, a short arc for the break, a dot in the middle.
/// The same drawing is the in-app logo, the tray icon (which fills as time passes) and the app icon.
/// </summary>
public static class IconFactory
{
    /// <summary>The static logo: a complete round, hold arc in <paramref name="ring"/>, break arc and dot in <paramref name="dot"/>.</summary>
    public static void Draw(DrawingContext dc, Brush ring, Brush dot, double size) =>
        DrawRing(dc, size, 25, 5, 1, 1, null, null, ring, dot, dot);

    private static Point At(Point c, double r, double deg)
    {
        double a = deg * Math.PI / 180;
        return new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
    }

    /// <summary>Colors for the round ring in the tray.</summary>
    public readonly record struct RingColors(Color HoldTrack, Color BreakTrack, Color HoldFill, Color BreakFill, Color Dot);

    /// <summary>
    /// The tray ring: one full round. The long arc is the hold, the short arc is the break,
    /// separated by small gaps. <paramref name="holdDone"/> and <paramref name="breakDone"/> (0..1) fill them.
    /// </summary>
    public static void DrawRound(DrawingContext dc, double size, int holdMin, int breakMin,
        double holdDone, double breakDone, RingColors col) =>
        DrawRing(dc, size, holdMin, breakMin, holdDone, breakDone,
            Frozen(col.HoldTrack), Frozen(col.BreakTrack), Frozen(col.HoldFill), Frozen(col.BreakFill), Frozen(col.Dot));

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static void DrawRing(DrawingContext dc, double size, int holdMin, int breakMin, double holdDone, double breakDone,
        Brush? holdTrack, Brush? breakTrack, Brush holdFill, Brush breakFill, Brush dotBrush)
    {
        double k = size / 32.0;
        var c = new Point(16 * k, 16 * k);
        double r = 11.5 * k;
        double stroke = 3.4 * k;
        double circ = 2 * Math.PI * r;
        double gapDeg = 360 * (5.2 * k) / circ;
        double capDeg = 360 * stroke / circ; // round caps stick out half a stroke at each end
        double usable = 360 - 2 * gapDeg;
        double total = Math.Max(1, holdMin + breakMin);
        double hSweep = usable * holdMin / total;
        double bSweep = usable * breakMin / total;
        double hStart = -90;
        double bStart = hStart + hSweep + gapDeg;

        void Arc(Brush? brush, double start, double sweep)
        {
            double s = sweep - capDeg;
            if (brush == null || s <= 0.5) return;
            var pen = new Pen(brush, stroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            double a0 = start + capDeg / 2;
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(At(c, r, a0), false, false);
                g.ArcTo(At(c, r, a0 + s), new Size(r, r), 0, s > 180, SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        Arc(holdTrack, hStart, hSweep);
        Arc(breakTrack, bStart, bSweep);
        if (holdDone > 0) Arc(holdFill, hStart, capDeg + (hSweep - capDeg) * Math.Clamp(holdDone, 0, 1));
        if (breakDone > 0) Arc(breakFill, bStart, capDeg + (bSweep - capDeg) * Math.Clamp(breakDone, 0, 1));
        dc.DrawEllipse(dotBrush, null, c, 3.6 * k, 3.6 * k);
    }

    public static IntPtr CreateRoundHIcon(int px, int holdMin, int breakMin, double holdDone, double breakDone, RingColors col) =>
        Render(px, dc => DrawRound(dc, px, holdMin, breakMin, holdDone, breakDone, col));

    /// <summary>Renders a drawing into a Win32 icon handle for the tray. Caller destroys it.</summary>
    private static IntPtr Render(int px, Action<DrawingContext> draw)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen()) draw(dc);
        var rtb = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var bgra = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[px * px * 4];
        bgra.CopyPixels(pixels, px * 4, 0);

        var bi = new Native.BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<Native.BITMAPINFOHEADER>(),
            biWidth = px,
            biHeight = -px, // top-down
            biPlanes = 1,
            biBitCount = 32,
        };
        IntPtr color = Native.CreateDIBSection(IntPtr.Zero, ref bi, 0, out IntPtr bits, IntPtr.Zero, 0);
        if (color == IntPtr.Zero) return IntPtr.Zero;
        Marshal.Copy(pixels, 0, bits, pixels.Length);
        IntPtr mask = Native.CreateBitmap(px, px, 1, 1, IntPtr.Zero);
        var ii = new Native.ICONINFO { fIcon = true, hbmColor = color, hbmMask = mask };
        IntPtr icon = Native.CreateIconIndirect(ref ii);
        Native.DeleteObject(color);
        Native.DeleteObject(mask);
        return icon;
    }
}

/// <summary>The mark as a WPF element.</summary>
public sealed class LogoIcon : FrameworkElement
{
    public static readonly DependencyProperty RingProperty = DependencyProperty.Register(
        nameof(Ring), typeof(Brush), typeof(LogoIcon),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DotProperty = DependencyProperty.Register(
        nameof(Dot), typeof(Brush), typeof(LogoIcon),
        new FrameworkPropertyMetadata(Brushes.MediumPurple, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Ring { get => (Brush)GetValue(RingProperty); set => SetValue(RingProperty, value); }
    public Brush Dot { get => (Brush)GetValue(DotProperty); set => SetValue(DotProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double s = Math.Min(ActualWidth, ActualHeight);
        if (s > 0) IconFactory.Draw(dc, Ring, Dot, s);
    }
}
