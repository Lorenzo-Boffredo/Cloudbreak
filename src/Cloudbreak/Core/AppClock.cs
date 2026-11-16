using System;

namespace Cloudbreak.Core;

/// <summary>The app's notion of "now". Release builds use the system clock as is.</summary>
public static class AppClock
{
#if DEBUG
    // Testing aid (debug builds only): CLOUDBREAK_CLOCK=14:37 pretends it is that time today.
    private static readonly TimeSpan Offset =
        TimeSpan.TryParse(Environment.GetEnvironmentVariable("CLOUDBREAK_CLOCK"), out var t)
            ? DateTime.Today + t - DateTime.Now
            : TimeSpan.Zero;

    public static DateTime Now => DateTime.Now + Offset;
#else
    public static DateTime Now => DateTime.Now;
#endif
}
