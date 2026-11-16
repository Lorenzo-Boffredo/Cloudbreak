using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Cloudbreak.Core;

/// <summary>A soft two-note bell, synthesized on demand (no sound files). Rising for a break, falling for a hold.</summary>
public static class Chime
{
    private static GCHandle _pinned;

    public static void Play(bool toBreak)
    {
        try
        {
            var wav = toBreak ? Make(659.25, 987.77) : Make(987.77, 659.25);
            Native.PlaySound(IntPtr.Zero, IntPtr.Zero, 0); // stop anything still playing before freeing it
            if (_pinned.IsAllocated) _pinned.Free();
            _pinned = GCHandle.Alloc(wav, GCHandleType.Pinned);
            Native.PlaySound(_pinned.AddrOfPinnedObject(), IntPtr.Zero, Native.SND_MEMORY | Native.SND_ASYNC | Native.SND_NODEFAULT);
        }
        catch { /* no audio device */ }
    }

    private static byte[] Make(double f1, double f2)
    {
        const int rate = 32000;
        const double gap = 0.17, tail = 0.95;
        int n = (int)(rate * (gap + tail));
        var samples = new double[n];
        AddNote(samples, rate, f1, 0, tail);
        AddNote(samples, rate, f2, gap, tail);

        using var ms = new MemoryStream(44 + n * 2);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + n * 2); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(n * 2);
        foreach (var s in samples) w.Write((short)Math.Clamp(s * 32767, -32767, 32767));
        w.Flush();
        return ms.ToArray();
    }

    private static void AddNote(double[] buf, int rate, double f, double start, double len)
    {
        int s0 = (int)(start * rate);
        int count = Math.Min(buf.Length - s0, (int)(len * rate));
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / rate;
            double attack = Math.Min(1, t / 0.006);
            double env = attack * Math.Exp(-t * 5.2);
            double v = Math.Sin(2 * Math.PI * f * t)
                     + 0.28 * Math.Sin(2 * Math.PI * f * 2.0 * t) * Math.Exp(-t * 9)
                     + 0.10 * Math.Sin(2 * Math.PI * f * 3.01 * t) * Math.Exp(-t * 14);
            buf[s0 + i] += 0.16 * env * v;
        }
    }
}
