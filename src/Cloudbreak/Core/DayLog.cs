using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Cloudbreak.Core;

public enum Phase { Hold, Break }

public sealed class Segment
{
    public Phase Phase { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Skipped { get; set; }
}

/// <summary>Today's finished segments and counters. Saved on every change (a few times per hour).</summary>
public sealed class DayLog
{
    public DateTime Date { get; set; }
    public List<Segment> Segments { get; set; } = new();
    public int HoldsDone { get; set; }
    public int BreaksSkipped { get; set; }
    public double VapeFreeSeconds { get; set; }

    private static string FilePath => Path.Combine(Settings.DataDir, "today.json");

    public static DayLog Load(DateTime now)
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var log = JsonSerializer.Deserialize<DayLog>(File.ReadAllText(FilePath));
                // A real day has a few hundred segments at most; anything bigger is a damaged file.
                if (log != null && log.Date.Date == now.Date && log.Segments.Count <= 2000) return log;
            }
        }
        catch { /* start fresh */ }
        return new DayLog { Date = now.Date };
    }

    /// <param name="activeSeconds">Hold time really spent today, without pauses.</param>
    public void Add(Segment s, bool completedHold, double activeSeconds)
    {
        if (s.End <= s.Start && !s.Skipped) return;
        Segments.Add(s);
        if (s.Phase == Phase.Hold) VapeFreeSeconds += Math.Max(0, activeSeconds);
        if (completedHold) HoldsDone++;
        if (s.Skipped) BreaksSkipped++;
        Save();
    }

    /// <returns>true when the day changed and the log was reset.</returns>
    public bool RollIfNewDay(DateTime now)
    {
        if (now.Date == Date.Date) return false;
        Date = now.Date;
        Segments.Clear();
        HoldsDone = 0;
        BreaksSkipped = 0;
        VapeFreeSeconds = 0;
        Save();
        return true;
    }

    public int HoldSegmentCount()
    {
        int n = 0;
        foreach (var s in Segments) if (s.Phase == Phase.Hold) n++;
        return n;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Settings.DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch { /* not fatal */ }
    }
}
