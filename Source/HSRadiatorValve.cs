using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Thermostatic valve on each mod radiator, kept in the block's meta so it saves with the chunk and
// syncs like a door. Meta 0 is a radiator placed before valves existed and reads as the default.
public static class HSRadiatorValve
{
    public const int Max = 5;
    public const int Default = 3;
    const float Tau = 25f;
    const int BarSlots = 10;
    const float BarTopC = 80f;
    const float ColdC = 15f;

    class Gauge
    {
        public float FromF;
        public float At;
        public bool Warm;
        public int Level;
    }

    static readonly Dictionary<long, Gauge> gauges = new Dictionary<long, Gauge>();

    public static int Level(BlockValue bv)
    {
        if (!(bv.Block is BlockHSRadiator))
            return Default;
        int m = bv.meta;
        return m == 0 ? Default : Mathf.Clamp(m - 1, 0, Max);
    }

    public static BlockValue WithLevel(BlockValue bv, int level)
    {
        bv.meta = (byte)(Mathf.Clamp(level, 0, Max) + 1);
        return bv;
    }

    public static float RadiusScale(int level)
    {
        return 0.4f + 0.2f * level;
    }

    public static int Watts(int levels)
    {
        return (HSWorkingStuffSettings.RadiatorWatts * levels + Default / 2) / Default;
    }

    static float FlowC(int level)
    {
        return 30f + 10f * level;
    }

    static float TargetF(Gauge g)
    {
        float c = g.Warm && g.Level > 0 ? FlowC(g.Level) : ColdC;
        return c * 9f / 5f + 32f;
    }

    static float Value(Gauge g, float now)
    {
        float target = TargetF(g);
        return target + (g.FromF - target) * Mathf.Exp(-(now - g.At) / Tau);
    }

    static void Rebase(Gauge g, float now, bool warm, int level)
    {
        g.FromF = Value(g, now);
        g.At = now;
        g.Warm = warm;
        g.Level = level;
    }

    public static void StateChanged(Vector3i pos, bool warm)
    {
        Gauge g;
        if (gauges.TryGetValue(HSWater.Key(pos), out g) && g.Warm != warm)
            Rebase(g, Time.time, warm, g.Level);
    }

    public static float ReadingF(Vector3i pos, int level, bool warm)
    {
        long k = HSWater.Key(pos);
        float now = Time.time;
        Gauge g;
        if (!gauges.TryGetValue(k, out g))
        {
            g = new Gauge { At = now, Warm = warm, Level = level };
            g.FromF = TargetF(g);
            gauges[k] = g;
        }
        else if (g.Warm != warm || g.Level != level)
            Rebase(g, now, warm, level);
        return Value(g, now);
    }

    public static string GaugeLine(Vector3i pos, int level, bool warm)
    {
        float f = ReadingF(pos, level, warm);
        float c = (f - 32f) * 5f / 9f;
        int filled = Mathf.Clamp(Mathf.RoundToInt(c / BarTopC * BarSlots), 0, BarSlots);
        var bar = new StringBuilder(BarSlots + 2);
        bar.Append('[');
        for (int i = 0; i < BarSlots; i++)
            bar.Append(i < filled ? '|' : '.');
        bar.Append(']');
        return HSLoc.Format("hsRadiatorGauge", "Temp: {0} {1}", ValueDisplayFormatters.Temperature(f, 0), bar.ToString());
    }

    public static string ValveText(int level)
    {
        if (level <= 0)
            return HSLoc.Get("hsRadiatorValveOff", "Valve: off");
        return HSLoc.Format("hsRadiatorValve", "Valve: {0} of {1}", level, Max);
    }

    public static void Clear()
    {
        gauges.Clear();
    }
}
