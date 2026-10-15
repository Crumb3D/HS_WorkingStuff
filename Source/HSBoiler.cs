using System;
using System.Collections.Generic;
using System.IO;
using Audio;
using UnityEngine;

// The combi boiler (block hsWaterHeater) fires on demand. It asks its circuit for power only
// while a hot tap has just run or while central heating is on with radiators piped to it.
// Idle it asks for 0 W, so a live circuit still reads as powered and the hot taps stay usable.
public static class HSBoiler
{
    public const string PanelRadiatorName = "hsRadiator";
    public const string CastIronRadiatorName = "hsRadiatorCastIron";
    public const string WarmBuff = "buffHSRadiatorWarm";

    public struct Info
    {
        public bool Heating;
        public bool Firing;
        public int Radiators;
        public int Filled;
    }

    // Authority only.
    static readonly Dictionary<long, bool> heating = new Dictionary<long, bool>();
    // Radiators' worth of water (1 jar each) sealed in each boiler's heating loop.
    static readonly Dictionary<long, int> filled = new Dictionary<long, int>();
    static readonly Dictionary<long, float> nextFill = new Dictionary<long, float>();
    static readonly HashSet<long> boilers = new HashSet<long>();
    static readonly Dictionary<long, float> hotUntil = new Dictionary<long, float>();
    static readonly Dictionary<long, List<Vector3i>> radiatorCache = new Dictionary<long, List<Vector3i>>();
    static readonly Dictionary<long, float> nextScan = new Dictionary<long, float>();
    static readonly Dictionary<long, Info> published = new Dictionary<long, Info>();
    static HashSet<long> hotRadiators = new HashSet<long>();
    static float nextResend;

    // Every peer, filled from the authority's broadcasts.
    static readonly Dictionary<long, Info> known = new Dictionary<long, Info>();
    static readonly Dictionary<long, float> warmRadiators = new Dictionary<long, float>();
    static float nextWarmCheck;

    const float ResendSeconds = 5f;
    const float RadiatorStaleSeconds = 12f;
    const float ScanSeconds = 5f;
    const float FillSeconds = 5f;

    public static bool IsBoiler(string name)
    {
        return name == HSWater.HeaterName;
    }

    public static bool IsRadiator(string name)
    {
        return name == PanelRadiatorName || name == CastIronRadiatorName
            || name == "radiatorHouse01" || name == "radiatorHouse01Player";
    }

    // POI house radiators only join the heating loop after the pipe tool has laid into them.
    public static bool PipedRadiator(World world, Vector3i pos)
    {
        if (HSPipeNet.Has(pos))
            return true;
        if (world == null)
            return false;
        BlockValue bv = world.GetBlock(pos);
        Vector3i parent = HSWater.Parent(pos, bv);
        return parent != pos && HSPipeNet.Has(parent);
    }

    // Laid pipe sits in the tank, sinks and the other boiler too. Those are cold water, not this heating loop.
    public static bool BlocksHeat(Block block)
    {
        if (block == null)
            return false;
        string n = block.GetBlockName();
        if (IsBoiler(n))
            return true;
        if (n == HSWater.TankName || n == HSWater.RainCatchName || n == HSWater.IntakeName)
            return true;
        return HSWater.IsSinkBlock(block);
    }

    public static Info Known(Vector3i pos)
    {
        Info info;
        known.TryGetValue(HSWater.Key(pos), out info);
        return info;
    }

    public static bool RadiatorWarm(Vector3i pos)
    {
        float until;
        return warmRadiators.TryGetValue(HSWater.Key(pos), out until) && until > Time.time;
    }

    public static void Register(Vector3i pos)
    {
        if (HSWorkingStuffNet.IsAuthority)
            boilers.Add(HSWater.Key(pos));
    }

    public static void Removed(Vector3i pos)
    {
        long k = HSWater.Key(pos);
        known.Remove(k);
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        boilers.Remove(k);
        hotUntil.Remove(k);
        radiatorCache.Remove(k);
        nextScan.Remove(k);
        published.Remove(k);
        nextFill.Remove(k);
        bool hadHeating = heating.Remove(k);
        if (filled.Remove(k) || hadHeating)
            Save();
    }

    public static void HotDraw(Vector3i pos)
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        long k = HSWater.Key(pos);
        boilers.Add(k);
        hotUntil[k] = Time.time + HSWorkingStuffSettings.HotWaterSeconds;
    }

    public static void SetHeating(Vector3i pos, bool on)
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        long k = HSWater.Key(pos);
        boilers.Add(k);
        if (on)
            heating[k] = true;
        else
            heating.Remove(k);
        nextScan.Remove(k);
        Save();
    }

    public static void Tick(World world)
    {
        if (world == null || !HSWorkingStuffNet.IsAuthority)
            return;
        float now = Time.time;
        bool resend = now >= nextResend;
        if (resend)
            nextResend = now + ResendSeconds;
        var hotNow = new HashSet<long>();
        var keys = new List<long>(boilers);
        for (int i = 0; i < keys.Count; i++)
        {
            long k = keys[i];
            Vector3i pos = HSWater.FromKey(k);
            if (world.GetChunkFromWorldPos(pos) == null)
                continue;
            BlockValue bv = world.GetBlock(pos);
            if (bv.Block == null || bv.ischild || !IsBoiler(bv.Block.GetBlockName()))
            {
                Removed(pos);
                continue;
            }
            List<Vector3i> rads = Radiators(world, pos, k, now);
            bool heatOn = heating.ContainsKey(k);
            int wet = Fill(world, pos, k, rads.Count, heatOn, now);
            var open = new List<Vector3i>();
            int valves = 0;
            for (int r = 0; r < wet; r++)
            {
                int level = HSRadiatorValve.Level(world.GetBlock(rads[r]));
                if (level <= 0)
                    continue;
                open.Add(rads[r]);
                valves += level;
            }
            bool heat = heatOn && open.Count > 0;
            float until;
            bool hot = hotUntil.TryGetValue(k, out until) && until > now;
            if (!hot)
                hotUntil.Remove(k);
            int watts = 0;
            if (hot || heat)
                watts = HSPowerDemand.Watts(bv.Block, 15);
            if (heat)
                watts += HSRadiatorValve.Watts(valves);
            if (watts > ushort.MaxValue)
                watts = ushort.MaxValue;
            HSPowerDemand.Set(pos, (ushort)watts);
            bool firing = watts > 0 && HSPowerDemand.CircuitLive(pos);
            if (heat && firing)
            {
                for (int r = 0; r < open.Count; r++)
                    hotNow.Add(HSWater.Key(open[r]));
            }
            Publish(k, pos, new Info { Heating = heatOn, Firing = firing, Radiators = rads.Count, Filled = wet }, resend);
        }
        foreach (long r in hotNow)
        {
            if (resend || !hotRadiators.Contains(r))
                SendRadiator(HSWater.FromKey(r), true);
        }
        foreach (long r in hotRadiators)
        {
            if (!hotNow.Contains(r))
                SendRadiator(HSWater.FromKey(r), false);
        }
        hotRadiators = hotNow;
    }

    // Radiators are filled nearest first, so the first wet ones in the list are the ones that heat.
    // The stored fill never shrinks: a radiator in a chunk that has not loaded yet must not lose its water.
    static int Fill(World world, Vector3i pos, long k, int count, bool heatOn, float now)
    {
        int had;
        filled.TryGetValue(k, out had);
        if (had >= count)
            return count;
        float due;
        if (!heatOn || (nextFill.TryGetValue(k, out due) && now < due))
            return had;
        nextFill[k] = now + FillSeconds;
        int got = HSWater.FillFromSupply(world, pos, count - had);
        if (got <= 0)
            return had;
        filled[k] = had + got;
        Save();
        return had + got;
    }

    static void Publish(long k, Vector3i pos, Info info, bool resend)
    {
        Info last;
        bool same = published.TryGetValue(k, out last)
            && last.Heating == info.Heating && last.Firing == info.Firing && last.Radiators == info.Radiators
            && last.Filled == info.Filled;
        if (same && !resend)
            return;
        bool fireChanged = last.Firing != info.Firing;
        published[k] = info;
        Remember(pos, info);
        HSWorkingStuffNet.BroadcastBoiler(pos, info);
        if (fireChanged)
        {
            Vector3 at = pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
            if (info.Firing)
                Audio.Manager.BroadcastPlay(at, "hsBoilerFire");
            else
                Audio.Manager.BroadcastStop(at, "hsBoilerFire");
        }
    }

    static void SendRadiator(Vector3i pos, bool warm)
    {
        RememberRadiator(pos, warm);
        HSWorkingStuffNet.BroadcastRadiator(pos, warm);
    }

    public static void Remember(Vector3i pos, Info info)
    {
        known[HSWater.Key(pos)] = info;
    }

    public static void RememberRadiator(Vector3i pos, bool warm)
    {
        long k = HSWater.Key(pos);
        if (warm)
            warmRadiators[k] = Time.time + RadiatorStaleSeconds;
        else
            warmRadiators.Remove(k);
        HSRadiatorValve.StateChanged(pos, warm);
    }

    // Heat travels through bare pipe and on through radiators, never through tanks, sinks or another boiler.
    static List<Vector3i> Radiators(World world, Vector3i boiler, long k, float now)
    {
        List<Vector3i> cached;
        float due;
        if (radiatorCache.TryGetValue(k, out cached) && nextScan.TryGetValue(k, out due) && now < due)
            return cached;
        var found = new List<Vector3i>();
        int limit = HSWorkingStuffSettings.PipeSearchLimit * 2;
        var q = new Queue<Vector3i>();
        var seen = new HashSet<long>();
        q.Enqueue(boiler);
        seen.Add(k);
        int walked = 0;
        while (q.Count > 0 && walked < limit)
        {
            Vector3i p = q.Dequeue();
            walked++;
            for (int i = 0; i < HSWater.Dirs.Length; i++)
            {
                Vector3i np = p + HSWater.Dirs[i];
                long nk = HSWater.Key(np);
                if (seen.Contains(nk) || !HSPipeNet.Joins(p, np))
                    continue;
                BlockValue nbv = world.GetBlock(np);
                Block nb = nbv.Block;
                if (nb == null)
                    continue;
                string nn = nb.GetBlockName();
                if (IsRadiator(nn))
                {
                    Vector3i rp = HSWater.Parent(np, nbv);
                    if (!PipedRadiator(world, np) && !PipedRadiator(world, rp))
                        continue;
                    seen.Add(nk);
                    if (rp != np)
                        seen.Add(HSWater.Key(rp));
                    if (!found.Contains(rp))
                        found.Add(rp);
                    q.Enqueue(np);
                    continue;
                }
                if (BlocksHeat(nb) || (!HSWater.IsBarePipe(nn) && !HSPipeNet.Has(np)))
                    continue;
                seen.Add(nk);
                q.Enqueue(np);
            }
        }
        for (int i = found.Count - 1; i >= 0; i--)
        {
            Vector3i? owner = FindBoiler(world, found[i]);
            if (!owner.HasValue || owner.Value != boiler)
                found.RemoveAt(i);
        }
        radiatorCache[k] = found;
        nextScan[k] = now + ScanSeconds;
        return found;
    }

    // Any peer with a local player: standing near a warm radiator works like standing by a campfire.
    public static void ClientTick()
    {
        if (warmRadiators.Count == 0)
            return;
        float now = Time.time;
        if (now < nextWarmCheck)
            return;
        nextWarmCheck = now + 0.5f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        EntityPlayerLocal player = world.GetPrimaryPlayer();
        float radius = HSWorkingStuffSettings.RadiatorRadius;
        bool near = false;
        List<long> stale = null;
        Vector3 at = player != null ? player.GetPosition() : Vector3.zero;
        foreach (var kv in warmRadiators)
        {
            if (kv.Value < now)
            {
                if (stale == null)
                    stale = new List<long>();
                stale.Add(kv.Key);
                continue;
            }
            if (player == null || near)
                continue;
            Vector3i rp = HSWater.FromKey(kv.Key);
            Vector3 c = new Vector3(rp.x + 0.5f, rp.y + 0.5f, rp.z + 0.5f);
            float rr = radius * HSRadiatorValve.RadiusScale(HSRadiatorValve.Level(world.GetBlock(rp)));
            if ((c - at).sqrMagnitude <= rr * rr)
                near = true;
        }
        if (stale != null)
        {
            for (int i = 0; i < stale.Count; i++)
                warmRadiators.Remove(stale[i]);
        }
        if (!near || player == null || player.Buffs == null || player.IsDead())
            return;
        try
        {
            player.Buffs.AddBuff(WarmBuff);
        }
        catch { }
    }

    public static bool WarmAt(World world, Vector3 at)
    {
        if (world == null || warmRadiators.Count == 0)
            return false;
        float now = Time.time;
        float radius = HSWorkingStuffSettings.RadiatorRadius;
        foreach (var kv in warmRadiators)
        {
            if (kv.Value < now)
                continue;
            Vector3i rp = HSWater.FromKey(kv.Key);
            Vector3 c = new Vector3(rp.x + 0.5f, rp.y + 0.5f, rp.z + 0.5f);
            float rr = radius * HSRadiatorValve.RadiusScale(HSRadiatorValve.Level(world.GetBlock(rp)));
            if ((c - at).sqrMagnitude <= rr * rr)
                return true;
        }
        return false;
    }

    public static void Clear()
    {
        heating.Clear();
        filled.Clear();
        nextFill.Clear();
        boilers.Clear();
        hotUntil.Clear();
        radiatorCache.Clear();
        nextScan.Clear();
        published.Clear();
        hotRadiators.Clear();
        known.Clear();
        warmRadiators.Clear();
        HSRadiatorValve.Clear();
    }

    // Boilers in chunks that loaded before this stay registered.
    // Lines are x,y,z (heating on, from before the loop held water) or x,y,z,heating,filled.
    public static void Load()
    {
        heating.Clear();
        filled.Clear();
        try
        {
            string dir = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(dir))
                return;
            string path = Path.Combine(dir, "HSWorkingStuff.heating");
            if (!File.Exists(path))
                return;
            foreach (string line in File.ReadAllLines(path))
            {
                string[] f = line.Split(',');
                int x, y, z;
                if (f.Length < 3 || !int.TryParse(f[0], out x) || !int.TryParse(f[1], out y) || !int.TryParse(f[2], out z))
                    continue;
                long k = HSWater.Key(new Vector3i(x, y, z));
                boilers.Add(k);
                int fill = 0;
                if (f.Length >= 5)
                    int.TryParse(f[4], out fill);
                if (f.Length < 5 || f[3] == "1")
                    heating[k] = true;
                if (fill > 0)
                    filled[k] = fill;
            }
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] heating load: " + e.Message);
        }
    }

    public static void Save()
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        try
        {
            string dir = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return;
            var lines = new List<string>();
            var keys = new HashSet<long>(heating.Keys);
            keys.UnionWith(filled.Keys);
            foreach (long k in keys)
            {
                Vector3i p = HSWater.FromKey(k);
                int fill;
                filled.TryGetValue(k, out fill);
                lines.Add(p.x + "," + p.y + "," + p.z + "," + (heating.ContainsKey(k) ? 1 : 0) + "," + fill);
            }
            File.WriteAllLines(Path.Combine(dir, "HSWorkingStuff.heating"), lines.ToArray());
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] heating save: " + e.Message);
        }
    }

    public static string BoilerPrompt(WorldBase world, Vector3i pos)
    {
        var te = world != null ? world.GetTileEntity(pos) as TileEntityPowered : null;
        bool live = te != null && te.IsPowered;
        Info info = Known(pos);
        string water;
        if (!live)
            water = HSLoc.Get("hsBoilerNoPower", "No power: no hot water or heating");
        else if (info.Firing)
            water = HSLoc.Get("hsBoilerFiring", "Firing: heating water");
        else
            water = HSLoc.Get("hsBoilerIdle", "Idle: heats only when a hot tap runs (0W)");
        string ch;
        if (!info.Heating)
            ch = HSLoc.Get("hsBoilerHeatingOff", "Central heating: off");
        else if (info.Radiators <= 0)
            ch = HSLoc.Get("hsBoilerNoRadiators", "Central heating: on, no radiators piped in");
        else
            ch = HSLoc.Format("hsBoilerHeatingOn", "Central heating: on, {0} radiators", info.Radiators);
        if (info.Radiators > 0 && info.Filled < info.Radiators)
            ch += "\n" + HSLoc.Format("hsBoilerNeedsWater", "Heating water: {0} of {1} radiators filled. Needs {2} more jars in the tank piped to the boiler",
                info.Filled, info.Radiators, info.Radiators - info.Filled);
        else if (info.Radiators > 0)
            ch += "\n" + HSLoc.Format("hsBoilerFilled", "Heating water: all {0} radiators filled", info.Radiators);
        return water + "\n" + ch;
    }

    public static string RadiatorPrompt(WorldBase world, Vector3i pos)
    {
        int level = HSRadiatorValve.Level(world.GetBlock(pos));
        bool warm = RadiatorWarm(pos);
        return HSRadiatorValve.GaugeLine(pos, level, warm) + "\n" + HSRadiatorValve.ValveText(level)
            + "\n" + RadiatorStatus(world, pos, level, warm);
    }

    static string RadiatorStatus(WorldBase world, Vector3i pos, int level, bool warm)
    {
        if (warm)
            return HSLoc.Get("hsRadiatorWarm", "Warm: keeps you warm nearby");
        if (level <= 0)
            return HSLoc.Get("hsRadiatorOff", "Off at the valve: hold E and turn it up");
        World w = world as World;
        Vector3i? boiler = w != null ? BoilerFor(w, pos) : null;
        if (boiler == null)
            return HSLoc.Get("hsRadiatorNoPipe", "Cold: not piped to a combi boiler");
        Info info = Known(boiler.Value);
        if (!info.Heating)
            return HSLoc.Get("hsRadiatorHeatingOff", "Cold: piped to the boiler, but its central heating is off. Turn it on at the boiler");
        if (info.Filled < info.Radiators)
            return HSLoc.Format("hsRadiatorDry", "Cold: no water in the heating. Needs {0} more jars in the tank piped to the boiler (1 per radiator)",
                info.Radiators - info.Filled);
        var te = world.GetTileEntity(boiler.Value) as TileEntityPowered;
        if (te == null || !te.IsPowered)
            return HSLoc.Get("hsRadiatorNoPower", "Cold: the boiler has no power");
        return HSLoc.Get("hsRadiatorWarming", "Cold: the boiler is on, warming up");
    }

    static Vector3i lookedRadiator;
    static Vector3i? lookedBoiler;
    static float lookedUntil;

    // The prompt is asked for every frame while looked at, so the walk is cached briefly.
    static Vector3i? BoilerFor(World world, Vector3i radiator)
    {
        if (radiator == lookedRadiator && Time.time < lookedUntil)
            return lookedBoiler;
        lookedRadiator = radiator;
        lookedUntil = Time.time + 1f;
        lookedBoiler = FindBoiler(world, radiator);
        return lookedBoiler;
    }

    public static Vector3i? OwnerOf(World world, Vector3i radiator)
    {
        return FindBoiler(world, radiator);
    }

    // Nearest boiler on heating pipe only. Shared tank / cold pipe is a wall, so two boilers on one tank stay apart.
    static Vector3i? FindBoiler(World world, Vector3i radiator)
    {
        if (!PipedRadiator(world, radiator))
            return null;
        int limit = HSWorkingStuffSettings.PipeSearchLimit * 2;
        var q = new Queue<Vector3i>();
        var seen = new HashSet<long>();
        q.Enqueue(radiator);
        seen.Add(HSWater.Key(radiator));
        int walked = 0;
        while (q.Count > 0 && walked < limit)
        {
            Vector3i p = q.Dequeue();
            walked++;
            for (int i = 0; i < HSWater.Dirs.Length; i++)
            {
                Vector3i np = p + HSWater.Dirs[i];
                long nk = HSWater.Key(np);
                if (seen.Contains(nk) || !HSPipeNet.Joins(p, np))
                    continue;
                BlockValue nbv = world.GetBlock(np);
                Block nb = nbv.Block;
                if (nb == null)
                    continue;
                string nn = nb.GetBlockName();
                if (IsBoiler(nn))
                    return HSWater.Parent(np, nbv);
                if (IsRadiator(nn) && !PipedRadiator(world, np) && !PipedRadiator(world, HSWater.Parent(np, nbv)))
                    continue;
                if (BlocksHeat(nb) || (!IsRadiator(nn) && !HSWater.IsBarePipe(nn) && !HSPipeNet.Has(np)))
                    continue;
                seen.Add(nk);
                q.Enqueue(np);
            }
        }
        return null;
    }
}
