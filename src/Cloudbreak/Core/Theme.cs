using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Cloudbreak.Core;

/// <summary>
/// Builds the palette from the Windows light/dark setting and the chosen accent,
/// and puts it into application resources (consumed with DynamicResource).
/// Holds are monochrome ink; the accent is only used for breaks.
/// </summary>
public static class Theme
{
    public static bool IsDark { get; private set; }

    public static Color Paper { get; private set; }
    public static Color Hold { get; private set; }
    public static Color HoldSoft { get; private set; }
    public static Color Accent { get; private set; }
    public static Color AccentSoft { get; private set; }
    public static Color BreakBg { get; private set; }

    private static readonly Color VioletLight = Rgb(0x7A, 0x64, 0xDA);
    private static readonly Color VioletDark = Rgb(0xB4, 0xA6, 0xF6);

    public static void Apply(Settings s)
    {
        IsDark = !ReadBool(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", true);
#if DEBUG
        // Testing aid: CLOUDBREAK_THEME=light|dark overrides the Windows setting.
        var forced = Environment.GetEnvironmentVariable("CLOUDBREAK_THEME");
        if (forced == "light") IsDark = false;
        else if (forced == "dark") IsDark = true;
#endif
        bool d = IsDark;

        Color paper = d ? Rgb(0x1C, 0x1E, 0x24) : Rgb(0xF9, 0xFA, 0xFB);
        Color ink = d ? Rgb(0xE5, 0xE7, 0xEC) : Rgb(0x1D, 0x20, 0x27);
        Color muted = d ? Rgb(0x8E, 0x93, 0xA0) : Rgb(0x67, 0x6C, 0x78);
        Color line = d ? Rgb(0x2E, 0x31, 0x3A) : Rgb(0xD8, 0xDB, 0xE1);
        Color hold = d ? Rgb(0xDA, 0xDD, 0xE4) : Rgb(0x2A, 0x2E, 0x37);
        Color onAccent = d ? Rgb(0x11, 0x13, 0x17) : Colors.White;

        Color accent = AccentFor(s, d);
        Color accentSoft = Mix(accent, paper, d ? 0.20 : 0.13);

        Paper = paper;
        Hold = hold;
        HoldSoft = Mix(ink, paper, d ? 0.10 : 0.09);
        Accent = accent;
        AccentSoft = accentSoft;
        BreakBg = Mix(accentSoft, paper, 0.75);

        var r = Application.Current.Resources;
        Put(r, "Paper", paper);
        Put(r, "Ink", ink);
        Put(r, "Muted", muted);
        Put(r, "Line", line);
        Put(r, "Hold", hold);
        Put(r, "Accent", accent);
        Put(r, "AccentSoft", accentSoft);
        Put(r, "OnAccent", onAccent);
        // Plain colors read by code or by effects
        r["InkColor"] = ink;
        r["AccentPillColor"] = Mix(accent, paper, d ? 0.22 : 0.16);
        r["ShadowColor"] = d ? Color.FromArgb(255, 0, 0, 0) : Rgb(0x1D, 0x20, 0x27);
        Put(r, "Hover", Mix(ink, paper, 0.06));
        Put(r, "Pressed", Mix(ink, paper, 0.10));
        Put(r, "Track", Mix(ink, paper, 0.09));
        // Off switches need 3:1 against the window to read as a control (WCAG 1.4.11).
        Put(r, "SwitchOff", Mix(ink, paper, d ? 0.34 : 0.36));
        Put(r, "StripBg", Mix(ink, paper, 0.05));
        Put(r, "StripHold", Mix(hold, paper, 0.30));
        Put(r, "StripFuture", Color.FromArgb(170, paper.R, paper.G, paper.B));
        Put(r, "TileBg", Mix(ink, paper, 0.03));
        Put(r, "TileBlock", Mix(ink, paper, 0.17));
        Put(r, "TileBar", Mix(ink, paper, 0.09));
        Put(r, "Knob", d ? Rgb(0x1C, 0x1E, 0x24) : Colors.White);
    }

    /// <summary>Accent for a given light/dark context (the taskbar can differ from apps).</summary>
    public static Color AccentFor(Settings s, bool dark)
    {
        if (s.Accent == "windows" && WindowsAccent(dark) is Color c) return c;
        return dark ? VioletDark : VioletLight;
    }

    public static bool TaskbarIsDark =>
        !ReadBool(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", false);

    private static Color? WindowsAccent(bool dark)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (k?.GetValue("AccentPalette") is byte[] p && p.Length >= 32)
            {
                // Palette: light3, light2, light1, accent, dark1, dark2, dark3, -
                int i = dark ? 1 : 4;
                return Rgb(p[i * 4], p[i * 4 + 1], p[i * 4 + 2]);
            }
            using var dwm = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (dwm?.GetValue("AccentColor") is int abgr)
                return Rgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
        }
        catch { }
        return null;
    }

    private static bool ReadBool(string key, string name, bool fallback)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(key);
            if (k?.GetValue(name) is int v) return v != 0;
        }
        catch { }
        return fallback;
    }

    private static void Put(ResourceDictionary r, string name, Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        r[name + "Brush"] = b;
    }

    public static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    /// <summary>Mix: <paramref name="t"/> of a, the rest of b.</summary>
    public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A * t + b.A * (1 - t)),
        (byte)Math.Round(a.R * t + b.R * (1 - t)),
        (byte)Math.Round(a.G * t + b.G * (1 - t)),
        (byte)Math.Round(a.B * t + b.B * (1 - t)));

    public static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);
}
