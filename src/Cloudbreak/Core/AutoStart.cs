using System;
using Microsoft.Win32;

namespace Cloudbreak.Core;

/// <summary>Start with Windows through the per-user Run key.</summary>
public static class AutoStart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Cloudbreak";

    /// <summary>Whether Windows will start Cloudbreak at sign-in (set by the installer or by the app).</summary>
    public static bool IsRegistered()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key);
            return k?.GetValue(Name) is string;
        }
        catch { return false; }
    }

    public static void Apply(bool on)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        // Never register a development build.
        if (exe.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
            exe.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key, writable: true) ?? Registry.CurrentUser.CreateSubKey(Key);
            if (on) k.SetValue(Name, $"\"{exe}\" --startup");
            else if (k.GetValue(Name) != null) k.DeleteValue(Name, throwOnMissingValue: false);
        }
        catch { /* policy may block it */ }
    }
}
