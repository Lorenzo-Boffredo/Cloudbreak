using System;
using System.Collections.Generic;

namespace Cloudbreak.Core;

public enum RunState { Idle, Running, Paused, OffHours }

public enum Transition
{
    /// <summary>A phase ended on time while the app was watching.</summary>
    Natural,
    /// <summary>The loop started by itself at the beginning of the active hours.</summary>
    AutoStart,
    /// <summary>Caught up after sleep, or a user action: no notification.</summary>
    Silent,
}

/// <summary>
/// The hold/break loop, driven by wall-clock time. It has no timer of its own:
/// the caller calls <see cref="Update"/> when it wakes up, and asks <see cref="NextWakeup"/> when to wake up next.
/// Time spent paused never counts as hold or break time.
/// </summary>
public sealed class TimerEngine
{
    private readonly Settings _s;
    private TimeSpan _pausedRemaining;
    private DateTime _pausedAt;
    private TimeSpan _pausedSpan;   // total paused time in the current phase (finished pauses)
    private bool _manualOverride;   // started by hand outside the active hours

    public TimerEngine(Settings settings, DayLog log)
    {
        _s = settings;
        Log = log;
    }

    public DayLog Log { get; }
    public RunState State { get; private set; } = RunState.Idle;
    public Phase Phase { get; private set; } = Phase.Hold;
    public DateTime PhaseStart { get; private set; }
    public DateTime PhaseEnd { get; private set; }

    public event Action<Phase, Transition>? PhaseChanged;
    public event Action? StateChanged;

    public bool IsActive => State is RunState.Running or RunState.Paused;

#if DEBUG
    // Testing aid (debug builds only): CLOUDBREAK_SPEED=60 turns minutes into seconds.
    private static readonly double Speed =
        double.TryParse(Environment.GetEnvironmentVariable("CLOUDBREAK_SPEED"), out var v) && v > 0 ? v : 1;
#else
    private const double Speed = 1;
#endif

    public TimeSpan Length(Phase p) => TimeSpan.FromMinutes((p == Phase.Hold ? _s.HoldMinutes : _s.BreakMinutes) / Speed);

    public TimeSpan Remaining(DateTime now) => State switch
    {
        RunState.Running => PhaseEnd > now ? PhaseEnd - now : TimeSpan.Zero,
        RunState.Paused => _pausedRemaining,
        _ => Length(Phase.Hold),
    };

    public double Progress(DateTime now)
    {
        if (!IsActive) return 0;
        var len = Length(Phase).TotalSeconds;
        return len <= 0 ? 1 : Math.Clamp(1 - Remaining(now).TotalSeconds / len, 0, 1);
    }

    /// <summary>Time the current phase has really been running, without pauses.</summary>
    public TimeSpan ActiveTime(DateTime at)
    {
        if (!IsActive) return TimeSpan.Zero;
        var t = at - PhaseStart - _pausedSpan;
        if (State == RunState.Paused && at > _pausedAt) t -= at - _pausedAt;
        return t < TimeSpan.Zero ? TimeSpan.Zero : t;
    }

    /// <summary>When the current phase effectively started, as if it had never been paused.</summary>
    public DateTime EffectiveStart(DateTime now) => now - ActiveTime(now);

    /// <summary>End time of the current phase as it would be if resumed now.</summary>
    public DateTime CurrentEnd(DateTime now) => State == RunState.Paused ? now + _pausedRemaining : PhaseEnd;

    public double VapeFreeSeconds(DateTime now)
    {
        double s = Log.VapeFreeSeconds;
        if (IsActive && Phase == Phase.Hold) s += TodayPart(now, ActiveTime(now)).TotalSeconds;
        return s;
    }

    public int Round => Log.HoldSegmentCount() + (IsActive && Phase == Phase.Hold ? 1 : 0);

    // ---------- Active hours ----------

    public bool InsideActiveHours(DateTime now)
    {
        if (!_s.ActiveHoursEnabled) return true;
        var t = now.TimeOfDay;
        var a = _s.ActiveStartTime;
        var b = _s.ActiveEndTime;
        if (a == b) return true;
        return a < b ? t >= a && t < b : t >= a || t < b;
    }

    private DateTime NextBoundary(DateTime now)
    {
        var next = now.Date.AddDays(1); // midnight: day log rolls over
        if (_s.ActiveHoursEnabled)
        {
            foreach (var t in new[] { _s.ActiveStartTime, _s.ActiveEndTime })
            {
                var c = now.Date + t;
                if (c <= now) c = c.AddDays(1);
                if (c < next) next = c;
            }
        }
        return next;
    }

    /// <summary>How long the caller can sleep before something needs to happen, when nothing is on screen.</summary>
    public TimeSpan NextWakeup(DateTime now)
    {
        var next = NextBoundary(now);
        if (State == RunState.Running && PhaseEnd < next) next = PhaseEnd;
        var d = next - now;
        return d < TimeSpan.FromMilliseconds(20) ? TimeSpan.FromMilliseconds(20) : d;
    }

    // ---------- Actions ----------

    /// <summary>Called once at app launch: starts the loop when inside the active hours.</summary>
    public void Boot(DateTime now)
    {
        Log.RollIfNewDay(now);
        if (InsideActiveHours(now)) BeginLoop(now, Transition.Silent);
        else SetState(RunState.OffHours);
    }

    public void Start(DateTime now)
    {
        _manualOverride = !InsideActiveHours(now);
        BeginLoop(now, Transition.Silent);
    }

    public void Pause(DateTime now)
    {
        if (State != RunState.Running) return;
        _pausedRemaining = Remaining(now);
        _pausedAt = now;
        SetState(RunState.Paused);
    }

    public void Resume(DateTime now)
    {
        if (State != RunState.Paused) return;
        if (now > _pausedAt) _pausedSpan += now - _pausedAt;
        PhaseEnd = now + _pausedRemaining;
        SetState(RunState.Running);
    }

    public void Stop(DateTime now)
    {
        if (IsActive) CloseSegment(now, skipped: false);
        _manualOverride = false;
        SetState(InsideActiveHours(now) ? RunState.Idle : RunState.OffHours);
    }

    /// <summary>App is quitting: keep what was done in the current phase.</summary>
    public void Shutdown(DateTime now)
    {
        if (IsActive) CloseSegment(now, skipped: false);
    }

    public void SkipBreak(DateTime now)
    {
        if (!IsActive || Phase != Phase.Break) return;
        bool skipped = ActiveTime(now) < Length(Phase.Break) / 2;
        CloseSegment(now, skipped);
        BeginPhase(Phase.Hold, now);
        State = RunState.Running;
        PhaseChanged?.Invoke(Phase.Hold, Transition.Silent);
        StateChanged?.Invoke();
    }

    /// <summary>Re-applies phase lengths after the settings changed.</summary>
    public void SettingsChanged(DateTime now)
    {
        if (State == RunState.Running)
        {
            PhaseEnd = PhaseStart + _pausedSpan + Length(Phase);
            if (PhaseEnd <= now)
            {
                // The new length is already used up: end this phase now, without replaying phantom phases.
                CloseSegment(now, skipped: false);
                BeginPhase(Phase == Phase.Hold ? Phase.Break : Phase.Hold, now);
                PhaseChanged?.Invoke(Phase, Transition.Silent);
            }
        }
        else if (State == RunState.Paused)
        {
            var left = Length(Phase) - ActiveTime(now);
            _pausedRemaining = left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }
        Update(now);
        StateChanged?.Invoke();
    }

    public void Update(DateTime now)
    {
        if (Log.RollIfNewDay(now)) StateChanged?.Invoke();

        bool inside = InsideActiveHours(now);
        if (inside) _manualOverride = false;

        if (!inside && !_manualOverride)
        {
            if (IsActive) CloseSegment(Min(now, State == RunState.Running ? PhaseEnd : now), skipped: false);
            if (State != RunState.OffHours) SetState(RunState.OffHours);
            return;
        }

        if (State == RunState.OffHours)
        {
            BeginLoop(now, Transition.AutoStart);
            return;
        }

        if (State != RunState.Running || now < PhaseEnd) return;

        // Away for a long time (sleep, hibernation): start a fresh hold instead of replaying the gap.
        if (now - PhaseEnd > TimeSpan.FromHours(3))
        {
            CloseSegment(PhaseEnd, skipped: false);
            BeginPhase(Phase.Hold, now);
            PhaseChanged?.Invoke(Phase.Hold, Transition.Silent);
            StateChanged?.Invoke();
            return;
        }

        int guard = 0;
        while (now >= PhaseEnd && guard++ < 1000)
        {
            var end = PhaseEnd;
            CloseSegment(end, skipped: false);
            BeginPhase(Phase == Phase.Hold ? Phase.Break : Phase.Hold, end);
        }
        var kind = now - PhaseStart < TimeSpan.FromSeconds(90) ? Transition.Natural : Transition.Silent;
        PhaseChanged?.Invoke(Phase, kind);
        StateChanged?.Invoke();
    }

    // ---------- Day strip data ----------

    /// <summary>The current phase as a segment, ending where it is scheduled to end.</summary>
    public Segment? CurrentSegment(DateTime now) =>
        IsActive ? new Segment { Phase = Phase, Start = PhaseStart, End = CurrentEnd(now) } : null;

    /// <summary>Planned phases after the current one, up to <paramref name="until"/>.</summary>
    public IEnumerable<Segment> Projection(DateTime now, DateTime until)
    {
        if (!IsActive) yield break;
        var t = CurrentEnd(now);
        var p = Phase == Phase.Hold ? Phase.Break : Phase.Hold;
        int n = 0;
        while (t < until && n++ < 500)
        {
            var e = t + Length(p);
            yield return new Segment { Phase = p, Start = t, End = e };
            t = e;
            p = p == Phase.Hold ? Phase.Break : Phase.Hold;
        }
    }

#if DEBUG
    /// <summary>
    /// Testing aid (debug builds only): fills today with a realistic day that started at 09:04,
    /// with two skipped breaks, and leaves the requested phase running. Used for README screenshots.
    /// </summary>
    public void DemoSeed(DateTime now, bool inBreak)
    {
        Log.RollIfNewDay(now);
        Log.Segments.Clear();
        Log.HoldsDone = 0;
        Log.BreaksSkipped = 0;
        Log.VapeFreeSeconds = 0;

        // Where the current phase should start so the countdown shows a nice value.
        var currentStart = inBreak ? now - TimeSpan.FromSeconds(108) : now - TimeSpan.FromSeconds(378);
        var segs = new List<Segment>();
        var t = now.Date + new TimeSpan(9, 4, 0);
        var p = Phase.Hold;
        int breaks = 0;
        while (t < currentStart)
        {
            var len = Length(p);
            bool skipped = false;
            if (p == Phase.Break && (++breaks == 3 || breaks == 7)) { len = TimeSpan.Zero; skipped = true; }
            var end = t + len > currentStart ? currentStart : t + len;
            segs.Add(new Segment { Phase = p, Start = t, End = end, Skipped = skipped });
            t = end;
            p = p == Phase.Hold ? Phase.Break : Phase.Hold;
        }
        // The segment before the running phase must be of the other kind: merge if needed.
        var current = inBreak ? Phase.Break : Phase.Hold;
        if (segs.Count > 1 && segs[^1].Phase == current)
        {
            segs.RemoveAt(segs.Count - 1);
            segs[^1].End = currentStart;
            segs[^1].Skipped = false;
        }
        foreach (var s in segs)
            Log.Add(s, s.Phase == Phase.Hold && s.End - s.Start >= Length(Phase.Hold) - TimeSpan.FromSeconds(1),
                    s.Phase == Phase.Hold ? (s.End - s.Start).TotalSeconds : 0);

        Phase = current;
        PhaseStart = currentStart;
        PhaseEnd = currentStart + Length(Phase);
        _pausedSpan = TimeSpan.Zero;
        State = RunState.Running;
        StateChanged?.Invoke();
    }
#endif

    // ---------- Internals ----------

    private void BeginLoop(DateTime now, Transition kind)
    {
        BeginPhase(Phase.Hold, now);
        State = RunState.Running;
        PhaseChanged?.Invoke(Phase.Hold, kind);
        StateChanged?.Invoke();
    }

    private void BeginPhase(Phase p, DateTime start)
    {
        Phase = p;
        PhaseStart = start;
        PhaseEnd = start + Length(p);
        _pausedSpan = TimeSpan.Zero;
    }

    /// <summary>Only the part of an active span that falls on the log's day counts towards today.</summary>
    private TimeSpan TodayPart(DateTime end, TimeSpan active)
    {
        var sinceMidnight = end - Log.Date;
        if (sinceMidnight <= TimeSpan.Zero) return TimeSpan.Zero;
        return active < sinceMidnight ? active : sinceMidnight;
    }

    private void CloseSegment(DateTime end, bool skipped)
    {
        var active = ActiveTime(end);
        bool startedToday = PhaseStart >= Log.Date;
        bool completedHold = Phase == Phase.Hold && startedToday && active >= Length(Phase.Hold) - TimeSpan.FromSeconds(1);
        var start = startedToday ? PhaseStart : Log.Date;   // a phase from yesterday is drawn from midnight
        Log.Add(new Segment { Phase = Phase, Start = start, End = end, Skipped = skipped },
                completedHold, Phase == Phase.Hold ? TodayPart(end, active).TotalSeconds : 0);
    }

    private void SetState(RunState s)
    {
        State = s;
        StateChanged?.Invoke();
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
