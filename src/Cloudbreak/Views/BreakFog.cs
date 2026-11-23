using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cloudbreak.Core;

namespace Cloudbreak.Views;

/// <summary>
/// The break fog: a little violet mist hugging the corners of every screen for about ten seconds when a break starts.
/// One small click-through window per corner per screen. The mist is drawn into tiny bitmaps (about 90 px wide)
/// that are stretched smoothly to the corner, so each frame costs next to nothing. Everything closes when it fades out.
/// </summary>
public sealed class BreakFog
{
    private const double FadeIn = 1.2, Hold = 10, FadeOut = 2.4;
    private readonly List<FogCorner> _corners = new();
    private readonly DispatcherTimer _frame;
    private readonly Stopwatch _clock = new();
    private double _outAt = FadeIn + Hold;
    private bool _closed;

    /// <summary>Raised once, after the last window has closed.</summary>
    public event Action? Finished;

    public BreakFog()
    {
        _frame = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(66) }; // 15 fps is plenty for slow drifting mist
        _frame.Tick += (_, _) => Step();
    }

    public void Show()
    {
        var tint = Theme.WithAlpha(Theme.Accent, 224);
        var milk = Theme.WithAlpha(Mix(Theme.Accent, Colors.White, 0.55), 217);
        int seed = 1;
        foreach (var m in Native.Monitors())
            for (int corner = 0; corner < 4; corner++)
                _corners.Add(new FogCorner(m, corner, seed++, tint, milk));
        _clock.Restart();
        foreach (var c in _corners)
        {
            c.Render(0, 0);
            c.Show();
        }
        _frame.Start();
    }

    /// <summary>Fades out from wherever it is (the break was skipped, stopped, or the fog was turned off).</summary>
    public void FadeAway()
    {
        double t = _clock.Elapsed.TotalSeconds;
        if (t >= _outAt) return;
        _outAt = t - FadeOut * (1 - Level(t));
    }

    /// <summary>Closes at once, without fading.</summary>
    public void Close()
    {
        if (_closed) return;
        _closed = true;
        _frame.Stop();
        foreach (var c in _corners) c.Close();
        _corners.Clear();
        Finished?.Invoke();
    }

    private double Level(double t)
    {
        // Fading out wins over fading in: a break skipped in its first second fades from where it was.
        if (t >= _outAt) return Math.Max(0, 1 - (t - _outAt) / FadeOut);
        if (t < FadeIn) { double s = t / FadeIn; return s * s * (3 - 2 * s); }
        return 1;
    }

    private void Step()
    {
        double t = _clock.Elapsed.TotalSeconds;
        if (t >= _outAt + FadeOut) { Close(); return; }
        double level = Level(t);
        foreach (var c in _corners) c.Render(t, level);
    }

    private static Color Mix(Color a, Color b, double k) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * k), (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k));
}

/// <summary>One corner of one screen: a borderless, click-through, topmost window showing a tiny mist bitmap.</summary>
internal sealed class FogCorner : Window
{
    // Size of the corner area, as a share of the screen width and height.
    private const double AreaW = 0.28, AreaH = 0.26;
    private const int Bw = 90; // bitmap width: one bitmap pixel is about 0.3% of the screen width, which keeps the mist soft

    private readonly Native.RECT _rect;
    private readonly int _bh;
    private readonly WriteableBitmap _bmp;
    private readonly int[] _px;
    private readonly float[] _shape;
    private readonly Grid _a, _b; // two mist textures at different scales, drifting in opposite directions
    private readonly Color _tint, _milk;

    // Drift, in bitmap pixels per second.
    private const double ADx = 6.5, ADy = 0.9, BDx = -4.5, BDy = -0.7;
    private const int DriftRoom = 100; // room for the drift over the whole life of the fog

    public FogCorner(Native.RECT monitor, int corner, int seed, Color tint, Color milk)
    {
        _tint = tint;
        _milk = milk;
        bool right = corner is 1 or 3, bottom = corner is 2 or 3;
        int mw = monitor.Right - monitor.Left, mh = monitor.Bottom - monitor.Top;
        int w = (int)Math.Ceiling(mw * AreaW), h = (int)Math.Ceiling(mh * AreaH);
        int x = right ? monitor.Right - w : monitor.Left;
        int y = bottom ? monitor.Bottom - h : monitor.Top;
        _rect = new Native.RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };

        _bh = Math.Max(8, (int)Math.Round(Bw * (double)h / w));
        _bmp = new WriteableBitmap(Bw, _bh, 96, 96, PixelFormats.Pbgra32, null);
        _px = new int[Bw * _bh];
        _shape = BuildShape(corner, Bw, _bh);

        // Noise frequencies, in cycles per screen width / height, turned into cycles per bitmap pixel.
        double pu = AreaW / Bw, pv = AreaH / _bh;
        _a = new Grid(Bw + DriftRoom, _bh + 16, 5 * pu, 11 * pv, 4, seed * 101, 1.9, -0.4);
        _b = new Grid(Bw + DriftRoom, _bh + 16, 12 * pu, 19 * pv, 3, seed * 211 + 17, 1.8, -0.55);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        IsHitTestVisible = false;
        Width = 1;
        Height = 1;
        var img = new Image { Source = _bmp, Stretch = Stretch.Fill, IsHitTestVisible = false };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.Linear);
        Content = img;

        SourceInitialized += (_, _) =>
        {
            Native.AddExStyle(this, Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT);
            // The window is placed in physical pixels; keep WPF from resizing it when it lands on a screen with another scale.
            if (PresentationSource.FromVisual(this) is HwndSource src)
                src.AddHook((IntPtr _, int msg, IntPtr _, IntPtr _, ref bool handled) =>
                {
                    if (msg == Native.WM_DPICHANGED) handled = true;
                    return IntPtr.Zero;
                });
            Native.PlaceTopmost(this, _rect);
        };
    }

    public void Render(double t, double level)
    {
        double ax = DriftRoom - 1 - ADx * t, ay = 8 + ADy * t * 0.5;
        double bx = BDx * -t, by = 8 + BDy * t * 0.5;
        if (ax < 0) ax = 0;
        if (bx > DriftRoom - 1) bx = DriftRoom - 1;
        float lv = (float)level;
        int i = 0;
        for (int y = 0; y < _bh; y++)
        {
            for (int x = 0; x < Bw; x++, i++)
            {
                float s = _shape[i] * lv;
                if (s <= 0.002f) { _px[i] = 0; continue; }
                float ka = _a.Sample(x + ax, y + ay) * s * (_tint.A / 255f);
                float kb = _b.Sample(x + bx, y + by) * s * (_milk.A / 255f);
                float a = kb + ka * (1 - kb);
                if (a <= 0.002f) { _px[i] = 0; continue; }
                float wa = ka * (1 - kb);
                int r = (int)(_milk.R * kb + _tint.R * wa);
                int g = (int)(_milk.G * kb + _tint.G * wa);
                int b = (int)(_milk.B * kb + _tint.B * wa);
                _px[i] = ((int)(a * 255) << 24) | (r << 16) | (g << 8) | b;
            }
        }
        _bmp.WritePixels(new Int32Rect(0, 0, Bw, _bh), _px, Bw * 4, 0);
    }

    /// <summary>
    /// Where the mist may be, in this corner: a streak along the top or bottom edge, a narrower one along the side,
    /// and a small core in the corner. Their centers sit just off the screen, so there is no round outline.
    /// </summary>
    private static float[] BuildShape(int corner, int bw, int bh)
    {
        // (center u, center v, radius u, radius v), with u/v measured from the corner as a share of the screen.
        var p = corner switch
        {
            0 => new[] { (.05, -.01, .19, .06), (-.01, .07, .04, .16), (-.01, -.01, .07, .10) },
            1 => new[] { (.04, -.01, .16, .055), (-.01, .06, .035, .14), (-.01, -.01, .06, .09) },
            2 => new[] { (.06, -.01, .17, .06), (-.01, .08, .035, .15), (-.01, -.01, .065, .09) },
            _ => new[] { (.06, -.01, .21, .065), (-.01, .07, .04, .17), (-.01, -.01, .075, .10) },
        };
        bool right = corner is 1 or 3, bottom = corner is 2 or 3;
        var shape = new float[bw * bh];
        for (int y = 0; y < bh; y++)
        {
            int ly = bottom ? bh - 1 - y : y;
            double v = (ly + 0.5) / bh * AreaH;
            for (int x = 0; x < bw; x++)
            {
                int lx = right ? bw - 1 - x : x;
                double u = (lx + 0.5) / bw * AreaW;
                double keep = 1;
                for (int k = 0; k < 3; k++)
                {
                    var (cu, cv, ru, rv) = p[k];
                    double d = Math.Sqrt(Math.Pow((u - cu) / ru, 2) + Math.Pow((v - cv) / rv, 2));
                    double val = k == 2
                        ? Math.Max(0, 1 - d)                                   // core: plain fade
                        : d < 0.45 ? 1 - (d / 0.45) * 0.4 : Math.Max(0, 0.6 * (1 - (d - 0.45) / 0.55)); // streaks: 1 → 0.6 → 0
                    keep *= 1 - val;
                }
                shape[y * bw + x] = (float)(1 - keep);
            }
        }
        return shape;
    }

    /// <summary>A precomputed patch of stretched fractal value noise, turned into mist density (0..1).</summary>
    private sealed class Grid
    {
        private readonly int _w, _h;
        private readonly float[] _v;

        public Grid(int w, int h, double fx, double fy, int octaves, int seed, double gain, double bias)
        {
            _w = w;
            _h = h;
            _v = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double sum = 0, amp = 1, norm = 0, f = 1;
                    for (int o = 0; o < octaves; o++)
                    {
                        sum += amp * ValueNoise(x * fx * f, y * fy * f, seed + o * 1013);
                        norm += amp;
                        amp *= 0.5;
                        f *= 2;
                    }
                    double n = sum / norm;
                    _v[y * w + x] = (float)Math.Clamp(gain * n + bias, 0, 1);
                }
        }

        public float Sample(double x, double y)
        {
            int x0 = (int)x, y0 = (int)y;
            if (x0 < 0) x0 = 0; else if (x0 > _w - 2) x0 = _w - 2;
            if (y0 < 0) y0 = 0; else if (y0 > _h - 2) y0 = _h - 2;
            float fx = (float)Math.Clamp(x - x0, 0, 1), fy = (float)Math.Clamp(y - y0, 0, 1);
            int i = y0 * _w + x0;
            float top = _v[i] + (_v[i + 1] - _v[i]) * fx;
            float bot = _v[i + _w] + (_v[i + _w + 1] - _v[i + _w]) * fx;
            return top + (bot - top) * fy;
        }

        private static double ValueNoise(double x, double y, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            double tx = x - xi, ty = y - yi;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            double a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            double c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            double top = a + (b - a) * tx, bot = c + (d - c) * tx;
            return top + (bot - top) * ty;
        }

        private static double Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (double)0xFFFFFF;
            }
        }
    }
}
