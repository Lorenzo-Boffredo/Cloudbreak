using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cloudbreak.Core;

public sealed class Settings
{
    public int HoldMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;

    public bool ActiveHoursEnabled { get; set; } = true;
    public string ActiveStart { get; set; } = "09:00";
    public string ActiveEnd { get; set; } = "23:00";

    public bool Notifications { get; set; } = true;
    /// <summary>tl, tr, bl, br</summary>
    public string NotificationCorner { get; set; } = "tr";
    /// <summary>Violet mist in the corners of every screen for a few seconds when a break starts.</summary>
    public bool Fog { get; set; } = true;
    public bool Sound { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public bool MiniTimer { get; set; } = true;
    public double? MiniLeft { get; set; }
    public double? MiniTop { get; set; }
    public bool MainTopmost { get; set; }

    /// <summary>auto, en, it</summary>
    public string Language { get; set; } = "auto";
    /// <summary>violet, windows</summary>
    public string Accent { get; set; } = "violet";

    [JsonIgnore] public TimeSpan ActiveStartTime => ParseTime(ActiveStart, new TimeSpan(9, 0, 0));
    [JsonIgnore] public TimeSpan ActiveEndTime => ParseTime(ActiveEnd, new TimeSpan(23, 0, 0));

    public static string FormatTime(TimeSpan t) => $"{(int)t.TotalHours % 24:00}:{t.Minutes:00}";

    private static TimeSpan ParseTime(string s, TimeSpan fallback) =>
        TimeSpan.TryParseExact(s, @"hh\:mm", CultureInfo.InvariantCulture, out var t) ? t : fallback;

    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudbreak");

    private static string FilePath => Path.Combine(DataDir, "settings.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    /// <summary>False on the very first run (no settings file yet).</summary>
    [JsonIgnore] public static bool LoadedFromFile { get; private set; }

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Opts);
                if (s != null) { s.Clamp(); LoadedFromFile = true; return s; }
            }
        }
        catch { /* corrupt file: fall back to defaults */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opts));
        }
        catch { /* not fatal */ }
    }

    public void Clamp()
    {
        HoldMinutes = Math.Clamp(HoldMinutes, 5, 120);
        BreakMinutes = Math.Clamp(BreakMinutes, 1, 30);
        if (NotificationCorner is not ("tl" or "tr" or "bl" or "br")) NotificationCorner = "tr";
        if (Language is not ("auto" or "en" or "it")) Language = "auto";
        if (Accent is not ("violet" or "windows")) Accent = "violet";
        ActiveStart = FormatTime(ActiveStartTime);
        ActiveEnd = FormatTime(ActiveEndTime);
    }
}
