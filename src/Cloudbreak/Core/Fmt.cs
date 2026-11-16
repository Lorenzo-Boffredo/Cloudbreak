using System;

namespace Cloudbreak.Core;

public static class Fmt
{
    /// <summary>Countdown text, rounded up so it reaches 00:00 exactly when the phase ends.</summary>
    public static string Duration(TimeSpan t)
    {
        long s = (long)Math.Ceiling(Math.Max(0, t.TotalSeconds) - 0.001);
        if (s < 0) s = 0;
        long h = s / 3600, m = s / 60 % 60, sec = s % 60;
        return h > 0 ? $"{h}:{m:00}:{sec:00}" : $"{m:00}:{sec:00}";
    }

    public static string Clock(DateTime t) => t.ToString("HH:mm");

    public static string Span(TimeSpan t)
    {
        int m = (int)t.TotalMinutes;
        return m >= 60 ? $"{m / 60}h {m % 60}m" : $"{m}m";
    }
}
