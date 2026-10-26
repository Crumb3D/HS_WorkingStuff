using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Rain catch collects dew/rain and pushes down its pipes into tanks.
// A lake intake or powered water pump can fill a rain catch (cleaned only if the catch has a dew filter) or a tank (always murky).
// A tank only feeds what is in it; it does not join the pipes on one side to the pipes on the other.
// Sinks have a cold tap (straight from a tank or intake) and a hot tap (through a powered heater).
public static class HSWater
{
    public const int QualityMurky = 0;
    public const int QualityClean = 1;
    public const int QualityBoiled = 2;

    public const int ModGatherer = 1;
    public const int ModTarp = 2;
    public const int ModFilter = 4;

    public const string RainCatchName = "hsRainCatch";
    public const string TankName = "hsWaterTank";
    public const string HeaterName = "hsWaterHeater";
    public const string VanillaHeaterName = "hsWaterHeaterVanilla";
    public const string IntakeName = "hsWaterIntake";
    public const string PumpName = "hsWaterPump";
    public const string WaterCoolerName = "hsWaterCooler";
    public const string ShowerName = "hsShower";
    public const string BathName = "hsBath";
    public const string WasherName = "hsWasher";
    public const string WasherVanillaName = "hsWasherVanilla";

    public static readonly string[] ModItems = { "toolDewGatherer", "toolDewTarp", "toolDewFilter" };
    public static readonly int[] ModFlags = { ModGatherer, ModTarp, ModFilter };

    public struct Tank
    {
        public int Units;
        public int Quality;
        public int Mods;
        public float Frac;
    }

    public struct Source
    {
        public bool Ok;
        public bool Infinite;
        public Vector3i TankPos;
        public int Quality;
        public bool Heated;
    }

    public struct Taps
    {
        public Source Cold;
        public Source Hot;
        public bool HeaterSeen;
        public bool HeaterPowered;
        public Vector3i HeaterPos;
        public bool ColdEmpty;
        public bool HotEmpty;
    }

    static readonly Dictionary<long, Tank> tanks = new Dictionary<long, Tank>();
    static readonly Dictionary<long, float> intakeFrac = new Dictionary<long, float>();
    static readonly List<Vector3i> migrate = new List<Vector3i>();
    static readonly Vector3i[] dirs = new Vector3i[]
    {
        new Vector3i(1, 0, 0), new Vector3i(-1, 0, 0),
        new Vector3i(0, 1, 0), new Vector3i(0, -1, 0),
        new Vector3i(0, 0, 1), new Vector3i(0, 0, -1)
    };

    public static Vector3i[] Dirs
    {
        get { return dirs; }
    }

    public static long Key(Vector3i p)
    {
        return ((long)(p.x & 0x1FFFFF) << 42) | ((long)(p.y & 0x1FFFFF) << 21) | (long)(p.z & 0x1FFFFF);
    }

    public static Vector3i FromKey(long k)
    {
        int x = (int)((k >> 42) & 0x1FFFFF);
        int y = (int)((k >> 21) & 0x1FFFFF);
        int z = (int)(k & 0x1FFFFF);
        if (x >= 0x100000) x -= 0x200000;
        if (y >= 0x100000) y -= 0x200000;
        if (z >= 0x100000) z -= 0x200000;
        return new Vector3i(x, y, z);
    }

    public static Tank GetTank(Vector3i pos)
    {
        Tank t;
        if (tanks.TryGetValue(Key(pos), out t))
            return t;
        return new Tank { Units = 0, Quality = QualityMurky };
    }

    public static bool HasTank(Vector3i pos)
    {
        return tanks.ContainsKey(Key(pos));
    }

    public static void SetTank(Vector3i pos, Tank t)
    {
        if (t.Units < 0)
            t.Units = 0;
        int cap = HSWorkingStuffSettings.TankCapacity;
        if (t.Units > cap)
            t.Units = cap;
        tanks[Key(pos)] = t;
    }

    public static void RemoveTank(Vector3i pos)
    {
        tanks.Remove(Key(pos));
    }

    public static int Capacity(string blockName)
    {
        return blockName == RainCatchName ? HSWorkingStuffSettings.RainCatchCapacity : HSWorkingStuffSettings.TankCapacity;
    }

    public static Vector3i Parent(Vector3i pos, BlockValue bv)
    {
        Block b = bv.Block;
        if (bv.ischild && b != null && b.isMultiBlock && b.multiBlockPos != null)
            return b.multiBlockPos.GetParentPos(pos, bv);
        return pos;
    }

    public static bool IsPipe(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        return name == "hsPipeStraight" || name == "hsPipeCorner" || name == "hsPipeJoint"
            || name == HeaterName || name == VanillaHeaterName || name == TankName || name == IntakeName || name == PumpName
            || name == "hsSink" || name == RainCatchName
            || IsBarePipe(name);
    }

    public static bool IsBarePipe(string name)
    {
        return name == "hsPipeStraight" || name == "hsPipeCorner" || name == "hsPipeJoint"
            || name == "metalPipeStraight" || name == "metalPipeCorner" || name == "metalPipeJoint"
            || name == "metalPipeFlange" || name == "metalPipeCap" || name == "metalPipeValve";
    }

    public static bool IsSinkBlock(Block block)
    {
        if (block == null)
            return false;
        string n = block.GetBlockName();
        if (n == "hsSink" || n == "utilitySink" || n == "sinkKitchen" || n == "pedestalSink")
            return true;
        if (n == WaterCoolerName || n == ShowerName || n == BathName || n == WasherName || n == WasherVanillaName)
            return true;
        if (block is BlockHSShower)
            return true;
        if (n == "hsToilet" || n == "hsUrinal" || block is BlockHSToilet)
            return true;
        if (n.IndexOf("Sink", StringComparison.Ordinal) >= 0 || n.IndexOf("sink", StringComparison.Ordinal) >= 0)
            return n.IndexOf("Washer", StringComparison.OrdinalIgnoreCase) < 0;
        return false;
    }

    public static bool CellHasWater(World world, Vector3i p)
    {
        if (world == null)
            return false;
        if (world.GetWater(p).HasMass())
            return true;
        return world.IsWater(p);
    }

    // Bank next to water: sides, diagonals, and one step down the bank. Not "in" the water.
    public static bool TouchesWater(World world, Vector3i pos)
    {
        if (world == null)
            return false;
        if (CellHasWater(world, pos))
            return true;
        for (int i = 0; i < dirs.Length; i++)
        {
            if (CellHasWater(world, pos + dirs[i]))
                return true;
        }
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0)
                    continue;
                Vector3i side = new Vector3i(pos.x + dx, pos.y, pos.z + dz);
                if (CellHasWater(world, side))
                    return true;
                if (CellHasWater(world, new Vector3i(side.x, side.y - 1, side.z)))
                    return true;
            }
        }
        return false;
    }

    public static bool IsWaterSourceName(string n)
    {
        return n == IntakeName || n == PumpName;
    }

    // Pump runs on its own engines and fuel (generator bank). On = pumping.
    public static bool PumpPowered(World world, Vector3i pos)
    {
        var te = world != null ? world.GetTileEntity(pos) as TileEntityPowerSource : null;
        return te != null && te.IsOn;
    }

    // 1.0 per small engine in the bank (slot count, not quality-weighted MaxOutput).
    // Genny MaxOutput is ~0.5–1.0 per engine by quality, so MaxOutput/OutputPerStack made 6 junk engines look like 3x.
    // Passive flange intake always counts as 1.
    public static float PumpEngineScale(World world, Vector3i pos)
    {
        BlockValue bv = world != null ? world.GetBlock(pos) : BlockValue.Air;
        Block b = bv.Block;
        if (b == null || b.GetBlockName() != PumpName)
            return 1f;
        var te = world.GetTileEntity(pos) as TileEntityPowerSource;
        if (te == null)
            return 1f;
        ItemStack[] stacks = te.ItemSlots;
        if (stacks == null || stacks.Length == 0)
            return 1f;
        int n = 0;
        for (int i = 0; i < stacks.Length; i++)
        {
            if (stacks[i] != null && !stacks[i].IsEmpty())
                n++;
        }
        return Mathf.Max(1f, n);
    }

    public static string PumpPrompt(World world, Vector3i pos)
    {
        bool water = world != null && TouchesWater(world, pos);
        string waterLine = water
            ? HSLoc.Get("hsPumpHasWater", "Water: yes — lake / river on the bank")
            : HSLoc.Get("hsPumpNoWater", "Water: no — stand it on dry land next to the water (not in it)");

        var catches = new List<Vector3i>();
        var tanks = new List<Vector3i>();
        if (world != null)
            ReachFromIntake(world, pos, catches, tanks);
        string pipeLine;
        if (catches.Count > 0 && tanks.Count > 0)
            pipeLine = HSLoc.Get("hsPumpPipedBoth", "Piped to a rain catch and a tank");
        else if (catches.Count > 0)
            pipeLine = HSLoc.Get("hsPumpPipedCatch", "Piped to a rain catch");
        else if (tanks.Count > 0)
            pipeLine = HSLoc.Get("hsPumpPipedTank", "Piped to a tank");
        else
            pipeLine = HSLoc.Get("hsPumpNotPiped", "Not piped to a rain catch or tank");

        float engines = world != null ? PumpEngineScale(world, pos) : 1f;
        string runLine;
        if (!water)
            runLine = HSLoc.Get("hsPumpIdleNoWater", "Idle: no water to pump");
        else if (!PumpPowered(world, pos))
            runLine = HSLoc.Get("hsPumpIdleOff", "Idle: put in engines and fuel, then turn it on");
        else if (catches.Count == 0 && tanks.Count == 0)
            runLine = HSLoc.Format("hsPumpRunningNoPipe", "Running ({0}x engines): needs a pipe to a rain catch or tank", engines.ToString("0.#"));
        else
            runLine = HSLoc.Format("hsPumpRunningRate", "Pumping at {0}x (engines)", engines.ToString("0.#"));

        return waterLine + "\n" + pipeLine + "\n" + runLine;
    }

    // Passive flange intake, or a powered pump, both must sit next to water.
    public static bool LiveWaterSource(World world, Vector3i pos)
    {
        if (world == null || !TouchesWater(world, pos))
            return false;
        BlockValue bv = world.GetBlock(pos);
        Block b = bv.Block;
        if (b == null)
            return false;
        string n = b.GetBlockName();
        if (n == IntakeName)
            return true;
        if (n == PumpName)
            return PumpPowered(world, Parent(pos, bv));
        return false;
    }

    // The rain catch is 3x3x3, so its own upper cells must not count as a roof.
    public static bool OpenToSky(World world, Vector3i pos, Block self)
    {
        for (int y = pos.y + 1; y < 256; y++)
        {
            BlockValue bv = world.GetBlock(pos.x, y, pos.z);
            if (bv.isair)
                continue;
            if (self != null && bv.Block == self)
                continue;
            return false;
        }
        return true;
    }

    static bool HeaterPowered(World world, Vector3i p, BlockValue bv)
    {
        var te = world.GetTileEntity(Parent(p, bv)) as TileEntityPowered;
        return te != null && te.IsPowered;
    }

    static void Offer(ref Source slot, Source found)
    {
        if (!slot.Ok || (slot.Infinite && !found.Infinite))
            slot = found;
    }

    // Every water tank joined by pipe (or sitting face-to-face) with this one.
    public static List<Vector3i> ConnectedTanks(World world, Vector3i start)
    {
        var found = new List<Vector3i>();
        if (world == null)
            return found;
        int limit = HSWorkingStuffSettings.PipeSearchLimit * 2;
        var q = new Queue<Vector3i>();
        var seen = new HashSet<long>();
        var tankSeen = new HashSet<long>();
        q.Enqueue(start);
        seen.Add(Key(start));
        int walked = 0;
        while (q.Count > 0 && walked < limit)
        {
            Vector3i p = q.Dequeue();
            walked++;
            BlockValue bv = world.GetBlock(p);
            Block b = bv.Block;
            if (b != null && b.GetBlockName() == TankName)
            {
                Vector3i tp = Parent(p, bv);
                if (tankSeen.Add(Key(tp)))
                    found.Add(tp);
            }
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3i np = p + dirs[i];
                long k = Key(np);
                if (seen.Contains(k) || !HSPipeNet.Joins(p, np))
                    continue;
                Block nb = world.GetBlock(np).Block;
                if (nb == null)
                    continue;
                string nn = nb.GetBlockName();
                if (nn != TankName && !IsBarePipe(nn) && !HSPipeNet.Has(np))
                    continue;
                seen.Add(k);
                q.Enqueue(np);
            }
        }
        return found;
    }

    public static int PoolUnits(World world, Vector3i tankPos)
    {
        int n = 0;
        List<Vector3i> group = ConnectedTanks(world, tankPos);
        for (int i = 0; i < group.Count; i++)
            n += GetTank(group[i]).Units;
        return n;
    }

    // Farthest from rain catch / intake / pump fills first; no supply → farthest from a stable near-end leaf.
    static List<Vector3i> CascadeOrder(World world, List<Vector3i> group)
    {
        var ordered = new List<Vector3i>(group);
        if (world == null || ordered.Count <= 1)
            return ordered;
        var dist = new Dictionary<long, int>();
        var q = new Queue<Vector3i>();
        var seen = new HashSet<long>();
        int limit = HSWorkingStuffSettings.PipeSearchLimit * 2;
        // Seed from supplies that touch this bank's plumbing.
        for (int g = 0; g < group.Count; g++)
        {
            Vector3i seed = group[g];
            var walk = new Queue<Vector3i>();
            var local = new HashSet<long> { Key(seed) };
            walk.Enqueue(seed);
            int steps = 0;
            while (walk.Count > 0 && steps < limit)
            {
                Vector3i p = walk.Dequeue();
                steps++;
                BlockValue bv = world.GetBlock(p);
                Block b = bv.Block;
                string n = b != null ? b.GetBlockName() : "";
                if (n == RainCatchName || IsWaterSourceName(n))
                {
                    long sk = Key(p);
                    if (seen.Add(sk))
                    {
                        dist[sk] = 0;
                        q.Enqueue(p);
                    }
                }
                for (int i = 0; i < dirs.Length; i++)
                {
                    Vector3i np = p + dirs[i];
                    long k = Key(np);
                    if (local.Contains(k) || !HSPipeNet.Joins(p, np))
                        continue;
                    Block nb = world.GetBlock(np).Block;
                    if (nb == null)
                        continue;
                    string nn = nb.GetBlockName();
                    if (nn != TankName && nn != RainCatchName && !IsWaterSourceName(nn)
                        && !IsBarePipe(nn) && !HSPipeNet.Has(np))
                        continue;
                    local.Add(k);
                    walk.Enqueue(np);
                }
            }
        }
        if (q.Count == 0)
        {
            // No catch/pump on the run: treat the lowest Key tank as the near end.
            Vector3i near = ordered[0];
            long nearKey = Key(near);
            for (int i = 1; i < ordered.Count; i++)
            {
                long k = Key(ordered[i]);
                if (k < nearKey)
                {
                    nearKey = k;
                    near = ordered[i];
                }
            }
            seen.Add(nearKey);
            dist[nearKey] = 0;
            q.Enqueue(near);
        }
        int walked = 0;
        while (q.Count > 0 && walked < limit)
        {
            Vector3i p = q.Dequeue();
            walked++;
            int pd = dist[Key(p)];
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3i np = p + dirs[i];
                long k = Key(np);
                if (seen.Contains(k) || !HSPipeNet.Joins(p, np))
                    continue;
                Block nb = world.GetBlock(np).Block;
                if (nb == null)
                    continue;
                string nn = nb.GetBlockName();
                if (nn != TankName && nn != RainCatchName && !IsWaterSourceName(nn)
                    && !IsBarePipe(nn) && !HSPipeNet.Has(np))
                    continue;
                seen.Add(k);
                dist[k] = pd + 1;
                q.Enqueue(np);
            }
        }
        ordered.Sort((a, b) =>
        {
            int da, db;
            if (!dist.TryGetValue(Key(a), out da))
                da = -1;
            if (!dist.TryGetValue(Key(b), out db))
                db = -1;
            int c = db.CompareTo(da);
            return c != 0 ? c : Key(b).CompareTo(Key(a));
        });
        return ordered;
    }

    // Pack water to the far end of the bank (last tank full, then the one before, …).
    public static void CascadeTanks(World world, Vector3i any, List<Vector3i> changed)
    {
        if (world == null)
            return;
        List<Vector3i> group = ConnectedTanks(world, any);
        if (group.Count <= 1)
            return;
        CascadeGroup(world, group, changed);
    }

    static void CascadeGroup(World world, List<Vector3i> group, List<Vector3i> changed)
    {
        int cap = HSWorkingStuffSettings.TankCapacity;
        int total = 0;
        int quality = QualityMurky;
        bool any = false;
        for (int i = 0; i < group.Count; i++)
        {
            Tank t = GetTank(group[i]);
            if (t.Units <= 0)
                continue;
            quality = !any ? t.Quality : MixQuality(quality, t.Quality);
            total += t.Units;
            any = true;
        }
        if (total <= 0)
            return;
        List<Vector3i> order = CascadeOrder(world, group);
        int left = total;
        for (int i = 0; i < order.Count; i++)
        {
            Vector3i pos = order[i];
            Tank before = GetTank(pos);
            Tank t = before;
            int put = left < cap ? left : cap;
            left -= put;
            t.Units = put;
            t.Quality = put > 0 ? quality : before.Quality;
            if (t.Units != before.Units || t.Quality != before.Quality)
            {
                SetTank(pos, t);
                if (changed != null)
                    changed.Add(pos);
            }
            else
                SetTank(pos, t);
        }
    }

    static void CascadeAllTanks(World world, List<Vector3i> changed)
    {
        var done = new HashSet<long>();
        var keys = new List<long>(tanks.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            Vector3i pos = FromKey(keys[i]);
            if (done.Contains(Key(pos)))
                continue;
            if (world.GetChunkFromWorldPos(pos) == null)
                continue;
            BlockValue bv = world.GetBlock(pos);
            if (bv.Block == null || bv.ischild || bv.Block.GetBlockName() != TankName)
                continue;
            List<Vector3i> group = ConnectedTanks(world, pos);
            for (int g = 0; g < group.Count; g++)
                done.Add(Key(group[g]));
            if (group.Count > 1)
                CascadeGroup(world, group, changed);
        }
    }

    // Two walks share one queue: water that has not passed a heater (cold) and water that has (hot).
    // An unpowered heater stops the cold walk, so the hot tap only works through a running heater.
    public static Taps FindTaps(World world, Vector3i start)
    {
        var taps = new Taps();
        if (world == null)
            return taps;
        int limit = HSWorkingStuffSettings.PipeSearchLimit * 2;
        var q = new Queue<Vector3i>();
        var qHot = new Queue<bool>();
        var seenCold = new HashSet<long>();
        var seenHot = new HashSet<long>();
        q.Enqueue(start);
        qHot.Enqueue(false);
        seenCold.Add(Key(start));
        int walked = 0;
        while (q.Count > 0 && walked < limit)
        {
            Vector3i p = q.Dequeue();
            bool hot = qHot.Dequeue();
            walked++;
            BlockValue bv = world.GetBlock(p);
            Block b = bv.Block;
            if (b == null)
                continue;
            string n = b.GetBlockName();
            if (n == HeaterName || n == VanillaHeaterName || b is BlockHSHeater)
            {
                taps.HeaterSeen = true;
                if (HeaterPowered(world, p, bv))
                {
                    if (!taps.HeaterPowered)
                        taps.HeaterPos = Parent(p, bv);
                    taps.HeaterPowered = true;
                    hot = true;
                }
                else if (!hot)
                    continue;
            }
            if (IsWaterSourceName(n) && LiveWaterSource(world, Parent(p, bv)))
            {
                var lake = new Source { Ok = true, Infinite = true, Quality = QualityMurky };
                if (hot)
                    Offer(ref taps.Hot, lake);
                else
                    Offer(ref taps.Cold, lake);
            }
            if (n == TankName)
            {
                Vector3i tp = Parent(p, bv);
                // Any tank on the bank counts; draw uses the whole pool (Cascade packs far end first).
                if (PoolUnits(world, tp) > 0)
                {
                    var stored = new Source { Ok = true, Infinite = false, TankPos = tp, Quality = GetTank(tp).Units > 0 ? GetTank(tp).Quality : QualityMurky };
                    // Prefer a tank that actually holds water for quality readout.
                    if (GetTank(tp).Units <= 0)
                    {
                        List<Vector3i> bank = ConnectedTanks(world, tp);
                        for (int bi = 0; bi < bank.Count; bi++)
                        {
                            Tank bt = GetTank(bank[bi]);
                            if (bt.Units > 0)
                            {
                                stored.TankPos = bank[bi];
                                stored.Quality = bt.Quality;
                                break;
                            }
                        }
                    }
                    if (hot)
                        Offer(ref taps.Hot, stored);
                    else
                        Offer(ref taps.Cold, stored);
                }
                else if (hot)
                    taps.HotEmpty = true;
                else
                    taps.ColdEmpty = true;
                // Walk through tanks so a bank of adjacent / chained tanks is one supply.
            }
            HashSet<long> seen = hot ? seenHot : seenCold;
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3i np = p + dirs[i];
                long k = Key(np);
                if (seen.Contains(k) || !HSPipeNet.Joins(p, np))
                    continue;
                Block nb = world.GetBlock(np).Block;
                if (nb == null)
                    continue;
                string nn = nb.GetBlockName();
                if (nn == RainCatchName)
                    continue;
                if (!IsPipe(nn) && !IsSinkBlock(nb) && !HSPipeNet.Has(np))
                    continue;
                seen.Add(k);
                q.Enqueue(np);
                qHot.Enqueue(hot);
            }
        }
        if (taps.Hot.Ok)
        {
            taps.Hot.Heated = true;
            taps.Hot.Quality = QualityBoiled;
        }
        return taps;
    }

    public static Source FindTap(World world, Vector3i sink, bool hot)
    {
        Taps t = FindTaps(world, sink);
        return hot ? t.Hot : t.Cold;
    }

    // Tanks reachable from a rain catch through bare pipe only. Heaters and sinks are not a way through.
    public static List<Vector3i> FeedTanks(World world, Vector3i catchPos)
    {
        var found = new List<Vector3i>();
        if (world == null)
            return found;
        int limit = HSWorkingStuffSettings.PipeSearchLimit + 27;
        var q = new Queue<Vector3i>();
        var seen = new HashSet<long>();
        var tankSeen = new HashSet<long>();
        q.Enqueue(catchPos);
        seen.Add(Key(catchPos));
        int walked = 0;
        while (q.Count > 0 && walked < limit)
        {
            Vector3i p = q.Dequeue();
            walked++;
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3i np = p + dirs[i];
                long k = Key(np);
                if (seen.Contains(k) || !HSPipeNet.Joins(p, np))
                    continue;
                BlockValue nbv = world.GetBlock(np);
                Block nb = nbv.Block;
                if (nb == null)
                    continue;
                string nn = nb.GetBlockName();
                if (nn == TankName)
                {
                    // Chain: walk through tanks so Catch → Tank → Tank fills every tank on the run.
                    seen.Add(k);
                    Vector3i tp = Parent(np, nbv);
                    if (tankSeen.Add(Key(tp)))
                        found.Add(tp);
                    q.Enqueue(np);
                    continue;
                }
                if (nn != RainCatchName && !IsBarePipe(nn) && !HSPipeNet.Has(np))
                    continue;
                seen.Add(k);
                q.Enqueue(np);
            }
        }
        return found;
    }

    // From an intake: rain catches and tanks on bare pipe. A rain catch stops the walk (it takes the feed).
    // Tanks chain: walk through so Pump → Tank → Tank fills every tank. Direct murky fill (no catch).
    public static void ReachFromIntake(World world, Vector3i intake, List<Vector3i> catches, List<Vector3i> directTanks)
    {
        if (catches != null)
            catches.Clear();
        if (directTanks != null)
            directTanks.Clear();
        if (world == null)
            return;
        int limit = HSWorkingStuffSettings.PipeSearchLimit + 27;
        var q = new Queue<Vector3i>();
        var seen = new HashSet<long>();
        var catchSeen = new HashSet<long>();
        var tankSeen = new HashSet<long>();
        q.Enqueue(intake);
        seen.Add(Key(intake));
        int walked = 0;
        while (q.Count > 0 && walked < limit)
        {
            Vector3i p = q.Dequeue();
            walked++;
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3i np = p + dirs[i];
                long k = Key(np);
                if (seen.Contains(k) || !HSPipeNet.Joins(p, np))
                    continue;
                BlockValue nbv = world.GetBlock(np);
                Block nb = nbv.Block;
                if (nb == null)
                    continue;
                string nn = nb.GetBlockName();
                if (nn == RainCatchName)
                {
                    seen.Add(k);
                    Vector3i cp = Parent(np, nbv);
                    if (catches != null && catchSeen.Add(Key(cp)))
                        catches.Add(cp);
                    continue;
                }
                if (nn == TankName)
                {
                    seen.Add(k);
                    Vector3i tp = Parent(np, nbv);
                    if (directTanks != null && tankSeen.Add(Key(tp)))
                        directTanks.Add(tp);
                    q.Enqueue(np);
                    continue;
                }
                if (IsWaterSourceName(nn))
                {
                    seen.Add(k);
                    continue;
                }
                if (!IsBarePipe(nn) && !HSPipeNet.Has(np))
                    continue;
                seen.Add(k);
                q.Enqueue(np);
            }
        }
    }

    // Live intakes / pumps on the same bare-pipe run as this fixture (rain catch or tank).
    // Walk through tanks so a tank further along a chain still sees the pump.
    public static List<Vector3i> LiveIntakesFrom(World world, Vector3i start)
    {
        var found = new List<Vector3i>();
        if (world == null)
            return found;
        int limit = HSWorkingStuffSettings.PipeSearchLimit + 27;
        var q = new Queue<Vector3i>();
        var seen = new HashSet<long>();
        var intakeSeen = new HashSet<long>();
        q.Enqueue(start);
        seen.Add(Key(start));
        int walked = 0;
        while (q.Count > 0 && walked < limit)
        {
            Vector3i p = q.Dequeue();
            walked++;
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3i np = p + dirs[i];
                long k = Key(np);
                if (seen.Contains(k) || !HSPipeNet.Joins(p, np))
                    continue;
                BlockValue nbv = world.GetBlock(np);
                Block nb = nbv.Block;
                if (nb == null)
                    continue;
                string nn = nb.GetBlockName();
                if (IsWaterSourceName(nn))
                {
                    seen.Add(k);
                    Vector3i ip = Parent(np, nbv);
                    if (LiveWaterSource(world, ip) && intakeSeen.Add(Key(ip)))
                        found.Add(ip);
                    continue;
                }
                if (nn == RainCatchName)
                {
                    seen.Add(k);
                    continue;
                }
                if (nn == TankName)
                {
                    seen.Add(k);
                    q.Enqueue(np);
                    continue;
                }
                if (!IsBarePipe(nn) && !HSPipeNet.Has(np))
                    continue;
                seen.Add(k);
                q.Enqueue(np);
            }
        }
        return found;
    }

    // Draws up to need jars for a boiler's heating loop from tanks (or a lake intake) on its own pipes.
    // Sinks, radiators, rain catches and other boilers are not a way through. Authority only.
    public static int FillFromSupply(World world, Vector3i boiler, int need)
    {
        if (world == null || need <= 0)
            return 0;
        int got = 0;
        int limit = HSWorkingStuffSettings.PipeSearchLimit * 2;
        var q = new Queue<Vector3i>();
        var seen = new HashSet<long>();
        var tankSeen = new HashSet<long>();
        q.Enqueue(boiler);
        seen.Add(Key(boiler));
        int walked = 0;
        while (q.Count > 0 && walked < limit)
        {
            Vector3i p = q.Dequeue();
            walked++;
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3i np = p + dirs[i];
                long k = Key(np);
                if (seen.Contains(k) || !HSPipeNet.Joins(p, np))
                    continue;
                BlockValue nbv = world.GetBlock(np);
                Block nb = nbv.Block;
                if (nb == null)
                    continue;
                string nn = nb.GetBlockName();
                if (IsWaterSourceName(nn))
                {
                    if (LiveWaterSource(world, Parent(np, nbv)))
                        return need;
                    continue;
                }
                if (nn == TankName)
                {
                    seen.Add(k);
                    Vector3i tp = Parent(np, nbv);
                    if (tankSeen.Add(Key(tp)))
                    {
                        Tank t = GetTank(tp);
                        int take = Math.Min(t.Units, need - got);
                        if (take > 0)
                        {
                            t.Units -= take;
                            SetTank(tp, t);
                            HSWorkingStuffNet.BroadcastTank(tp);
                            got += take;
                            if (got >= need)
                                return got;
                        }
                    }
                    continue;
                }
                if (!IsBarePipe(nn) && !HSPipeNet.Has(np))
                    continue;
                seen.Add(k);
                q.Enqueue(np);
            }
        }
        return got;
    }

    public static string WaterItem(int quality)
    {
        if (quality >= QualityClean)
            return "drinkJarBoiledWater";
        return "drinkJarRiverWater";
    }

    public static int HoldingKind(EntityPlayer player)
    {
        if (player == null || player.inventory == null)
            return 0;
        ItemClass ic = player.inventory.holdingItem;
        if (ic == null)
            return 0;
        string n = ic.Name;
        if (n == "drinkJarEmpty")
            return 1;
        if (n == "bucketEmpty")
            return 2;
        if (n == "drinkJarRiverWater" || n == "drinkJarBoiledWater")
            return 3;
        if (n == "bucketRiverWater")
            return 4;
        return 0;
    }

    public static int ItemQuality(string name)
    {
        if (name == "drinkJarBoiledWater")
            return QualityBoiled;
        return QualityMurky;
    }

    public static int MixQuality(int a, int b)
    {
        return a < b ? a : b;
    }

    // all=false fills only the empty jars or bucket in your hand; all=true every empty jar and bucket on you.
    public static bool FillPlayerContainers(EntityPlayer player, Source src, bool all)
    {
        if (player == null || !src.Ok)
            return false;
        if (!all)
        {
            int kind = HoldingKind(player);
            if (kind != 1 && kind != 2)
                return false;
            return FillStack(player, HSInv.GetHeld(player), ref src) > 0;
        }
        int got = 0;
        foreach (ItemStack[] stacks in new[] { HSInv.GetBagStacks(player), HSInv.GetToolStacks(player) })
        {
            if (stacks == null)
                continue;
            for (int i = 0; i < stacks.Length; i++)
                got += FillStack(player, stacks[i], ref src);
        }
        return got > 0;
    }

    // Each jar takes 1 unit and each bucket BucketUnits. A stack the water can't all fill is split.
    static int FillStack(EntityPlayer player, ItemStack stack, ref Source src)
    {
        ItemValue iv = HSItem.Val(stack);
        if (stack == null || stack.IsEmpty() || iv == null || iv.ItemClass == null)
            return 0;
        string name = iv.ItemClass.Name;
        int per;
        string dest;
        if (name == "drinkJarEmpty")
        {
            per = 1;
            dest = WaterItem(src.Quality);
        }
        else if (name == "bucketEmpty")
        {
            per = Math.Max(1, HSWorkingStuffSettings.BucketUnits);
            dest = "bucketRiverWater";
        }
        else
            return 0;
        int count = HSItem.Count(stack);
        int n = count;
        if (!src.Infinite)
        {
            World w = GameManager.Instance != null ? GameManager.Instance.World : null;
            n = Math.Min(count, PoolUnits(w, src.TankPos) / per);
        }
        if (n <= 0 || !TakeFromSource(ref src, n * per))
            return 0;
        if (n == count)
        {
            HSItem.SetVal(stack, ItemClass.GetItem(dest));
            HSItem.SetCount(stack, count);
            return n;
        }
        HSItem.SetCount(stack, count - n);
        if (!HSInv.AddToPlayer(player, new ItemStack(ItemClass.GetItem(dest), n)))
        {
            HSItem.SetCount(stack, count);
            if (!src.Infinite)
            {
                // Put the failed fill back into the bank and re-pack farthest-first.
                World w = GameManager.Instance != null ? GameManager.Instance.World : null;
                Tank t = GetTank(src.TankPos);
                t.Units += n * per;
                SetTank(src.TankPos, t);
                if (w != null)
                    CascadeTanks(w, src.TankPos, null);
            }
            return 0;
        }
        return n;
    }

    // Units added. A stack of 10 jars adds 10, and only those jars become empty.
    public static int PourIntoTank(EntityPlayer player, Vector3i pos, bool all)
    {
        Tank t = GetTank(pos);
        int before = t.Units;
        int cap = HSWorkingStuffSettings.TankCapacity;
        int bucket = HSWorkingStuffSettings.BucketUnits;
        PourArray(player, HSInv.GetBagStacks(player), ref t, cap, bucket, all);
        PourArray(player, HSInv.GetToolStacks(player), ref t, cap, bucket, all);
        if (t.Units == before && !all)
            PourHolding(player, ref t, cap, bucket, false);
        if (t.Units <= before)
            return 0;
        int added = t.Units - before;
        SetTank(pos, t);
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world != null)
            CascadeTanks(world, pos, null);
        return added;
    }

    static void PourArray(EntityPlayer player, ItemStack[] stacks, ref Tank t, int cap, int bucket, bool all)
    {
        if (stacks == null)
            return;
        for (int i = 0; i < stacks.Length; i++)
        {
            if (t.Units >= cap)
                break;
            if (!PourStack(player, stacks[i], ref t, cap, bucket, all))
                continue;
            if (!all)
                break;
        }
    }

    static bool PourStack(EntityPlayer player, ItemStack s, ref Tank t, int cap, int bucket, bool all)
    {
        ItemValue ivPour = HSItem.Val(s);
        if (s == null || s.IsEmpty() || ivPour == null || ivPour.ItemClass == null)
            return false;
        string name = ivPour.ItemClass.Name;
        int each;
        string empty;
        if (name == "drinkJarRiverWater" || name == "drinkJarBoiledWater")
        {
            each = 1;
            empty = "drinkJarEmpty";
        }
        else if (name == "bucketRiverWater")
        {
            each = bucket;
            empty = "bucketEmpty";
        }
        else
            return false;
        int room = (cap - t.Units) / each;
        if (room <= 0)
            return false;
        int have = HSItem.Count(s);
        int take = all ? Math.Min(have, room) : Math.Min(have, 1);
        if (take <= 0)
            return false;
        int q = ItemQuality(name);
        t.Quality = t.Units <= 0 ? q : MixQuality(t.Quality, q);
        t.Units += take * each;
        if (take == have)
        {
            HSItem.SetVal(s, ItemClass.GetItem(empty));
            HSItem.SetCount(s, have);
        }
        else
        {
            HSItem.SetCount(s, have - take);
            var empties = new ItemStack(ItemClass.GetItem(empty), take);
            if (!HSInv.AddToPlayer(player, empties))
                GiveOrDrop(player, new ItemStack(ItemClass.GetItem(empty), take));
        }
        return true;
    }

    static void PourHolding(EntityPlayer player, ref Tank t, int cap, int bucket, bool all)
    {
        int kind = HoldingKind(player);
        if (kind != 3 && kind != 4)
            return;
        ItemStack data = HSInv.GetHeld(player);
        PourStack(player, data, ref t, cap, bucket, all);
    }

    public static bool TakeFromSource(ref Source src, int need)
    {
        if (!src.Ok || need <= 0)
            return false;
        if (src.Infinite)
            return true;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        List<Vector3i> group = ConnectedTanks(world, src.TankPos);
        int have = 0;
        for (int i = 0; i < group.Count; i++)
            have += GetTank(group[i]).Units;
        if (have < need)
            return false;
        // Draw from the far end first (same order the bank packs into).
        List<Vector3i> order = CascadeOrder(world, group);
        int left = need;
        for (int i = 0; i < order.Count && left > 0; i++)
        {
            Tank t = GetTank(order[i]);
            int take = t.Units < left ? t.Units : left;
            if (take <= 0)
                continue;
            t.Units -= take;
            SetTank(order[i], t);
            left -= take;
        }
        return left == 0;
    }

    // Every tank in the bank that changed after a draw / cascade.
    public static List<Vector3i> TankBank(World world, Vector3i tankPos)
    {
        return ConnectedTanks(world, tankPos);
    }

    public static void Drink(EntityPlayer player, Source src)
    {
        if (player == null || !src.Ok)
            return;
        if (!TakeFromSource(ref src, 1))
            return;
        if (src.Quality >= QualityClean)
        {
            player.SetCVar("$waterAmountAdd", player.GetCVar("$waterAmountAdd") + 20f);
            player.Buffs.AddBuff("buffProcessConsumables");
        }
        else
        {
            player.SetCVar("$waterAmountAdd", player.GetCVar("$waterAmountAdd") + 10f);
            player.Buffs.AddBuff("buffProcessConsumables");
            if (!player.Buffs.HasBuff("buffWaterPurifier"))
            {
                player.AddHealth(-5);
                if (player.rand.RandomRange(1, 101) <= 12)
                    player.Buffs.AddBuff("buffDysenteryCatchDrink");
            }
        }
    }

    public static int ModFlagFor(string itemName)
    {
        for (int i = 0; i < ModItems.Length; i++)
        {
            if (ModItems[i] == itemName)
                return ModFlags[i];
        }
        return 0;
    }

    // Mixing keeps the worse quality, so murky water left in a newly filtered catch, or in the tanks it
    // fills, would spoil everything that comes after. Authority only; returns the jars poured away.
    public static int DrainMurky(World world, Vector3i catchPos, List<Vector3i> changed)
    {
        var targets = FeedTanks(world, catchPos);
        targets.Insert(0, catchPos);
        int drained = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            Tank t = GetTank(targets[i]);
            if (t.Units <= 0 || t.Quality >= QualityClean)
                continue;
            drained += t.Units;
            t.Units = 0;
            SetTank(targets[i], t);
            changed.Add(targets[i]);
        }
        return drained;
    }

    public static void SetMods(Vector3i pos, int mods)
    {
        Tank t = GetTank(pos);
        t.Mods = mods;
        SetTank(pos, t);
    }

    public static void QueueMigrate(Vector3i pos)
    {
        for (int i = 0; i < migrate.Count; i++)
        {
            if (migrate[i] == pos)
                return;
        }
        migrate.Add(pos);
    }

    // Older saves placed both blocks as vanilla collectors. Their leftover tile entity would tick with no
    // collector block behind it, so the server takes it out: rain catch keeps its dew tools as installed
    // tools, anything else in it is dropped on the ground.
    static void RunMigrations(World world)
    {
        if (migrate.Count == 0 || world == null)
            return;
        var list = new List<Vector3i>(migrate);
        migrate.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            Vector3i pos = list[i];
            try
            {
                var te = world.GetTileEntity(pos) as TileEntityCollector;
                if (te == null)
                    continue;
                bool isCatch = world.GetBlock(pos).Block != null && world.GetBlock(pos).Block.GetBlockName() == RainCatchName;
                Tank t = GetTank(pos);
                Vector3 drop = new Vector3(pos.x + 0.5f, pos.y + 1.5f, pos.z + 0.5f);
                ItemStack[] mods = te.ModSlots;
                if (mods != null)
                {
                    for (int s = 0; s < mods.Length; s++)
                    {
                        ItemClass ic = HSItem.ClassOf(mods[s]);
                        if (ic == null)
                            continue;
                        int flag = isCatch ? ModFlagFor(ic.Name) : 0;
                        if (flag != 0 && (t.Mods & flag) == 0)
                            t.Mods |= flag;
                        else
                            Drop(mods[s], drop);
                    }
                }
                DropAll(te.FuelSlots, drop);
                DropAll(te.Items, drop);
                SetTank(pos, t);
                Chunk chunk = world.GetChunkFromWorldPos(pos) as Chunk;
                if (chunk != null)
                    chunk.RemoveTileEntity(world, te);
                HSWorkingStuffNet.BroadcastTank(pos);
            }
            catch (Exception e)
            {
                Log.Warning("[HSWorkingStuff] collector cleanup at " + pos + ": " + e.Message);
            }
        }
    }

    static void DropAll(ItemStack[] stacks, Vector3 at)
    {
        if (stacks == null)
            return;
        for (int i = 0; i < stacks.Length; i++)
            Drop(stacks[i], at);
    }

    static void Drop(ItemStack stack, Vector3 at)
    {
        if (stack == null || stack.IsEmpty() || GameManager.Instance == null)
            return;
        GameManager.Instance.ItemDropServer(stack.Clone(), at, new Vector3(0.5f, 0f, 0.5f), -1, 600f);
    }

    public static void GiveOrDrop(EntityPlayer player, ItemStack stack)
    {
        if (player == null || stack == null || stack.IsEmpty())
            return;
        if (HSInv.AddToPlayer(player, stack))
            return;
        if (GameManager.Instance != null)
            GameManager.Instance.ItemDropServer(stack, player.GetPosition() + new Vector3(0f, 0.5f, 0f), Vector3.zero, -1, 600f);
    }

    public static void TickTanks(World world, ulong now, ulong last)
    {
        RunMigrations(world);
        if (world == null || now <= last)
            return;
        float hours = (now - last) / 1000f;
        if (hours <= 0f)
            return;
        float rain = 0f;
        try
        {
            if (WeatherManager.Instance != null)
                rain = WeatherManager.Instance.GetCurrentRainfallPercent();
        }
        catch { }
        float dew = hours * HSWorkingStuffSettings.DewPerHour;
        float rainAdd = rain >= 0.15f ? hours * HSWorkingStuffSettings.RainPerHour * rain : 0f;
        var changed = new List<Vector3i>();
        var keys = new List<long>(tanks.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            Vector3i pos = FromKey(keys[i]);
            if (world.GetChunkFromWorldPos(pos) == null)
                continue;
            BlockValue bv = world.GetBlock(pos);
            Block b = bv.Block;
            if (b == null || bv.ischild || b.GetBlockName() != RainCatchName)
                continue;
            Tank t = GetTank(pos);
            int beforeUnits = t.Units;
            int beforeQuality = t.Quality;
            int cap = HSWorkingStuffSettings.RainCatchCapacity;
            if ((t.Mods & ModFilter) != 0 && t.Units > 0 && t.Quality < QualityClean)
                t.Units = 0;
            if (t.Units < cap && OpenToSky(world, pos, b))
            {
                float add = dew + rainAdd;
                if ((t.Mods & ModGatherer) != 0)
                    add *= 2f;
                if ((t.Mods & ModTarp) != 0)
                    add *= 2f;
                t.Frac += add;
                int units = (int)t.Frac;
                t.Frac -= units;
                if (units > cap - t.Units)
                    units = cap - t.Units;
                if (units > 0)
                {
                    int incoming = (t.Mods & ModFilter) != 0 ? QualityClean : QualityMurky;
                    t.Quality = t.Units <= 0 ? incoming : MixQuality(t.Quality, incoming);
                    t.Units += units;
                }
            }
            if (t.Units > 0)
                PushToTanks(world, pos, ref t, changed);
            SetTank(pos, t);
            if (t.Units != beforeUnits || t.Quality != beforeQuality)
                changed.Add(pos);
        }
        TickIntakeFeeds(world, hours, changed);
        CascadeAllTanks(world, changed);
        for (int i = 0; i < changed.Count; i++)
            HSWorkingStuffNet.BroadcastTank(changed[i]);
    }

    // Lake / river intake fills a piped rain catch (clean only with a dew filter) or a tank (always murky).
    static void TickIntakeFeeds(World world, float hours, List<Vector3i> changed)
    {
        if (world == null || hours <= 0f)
            return;
        float rate = hours * HSWorkingStuffSettings.IntakePerHour;
        if (rate <= 0f)
            return;
        var intakes = new HashSet<long>();
        var keys = new List<long>(tanks.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            Vector3i pos = FromKey(keys[i]);
            if (world.GetChunkFromWorldPos(pos) == null)
                continue;
            BlockValue bv = world.GetBlock(pos);
            Block b = bv.Block;
            if (b == null || bv.ischild)
                continue;
            string n = b.GetBlockName();
            if (n != RainCatchName && n != TankName)
                continue;
            List<Vector3i> live = LiveIntakesFrom(world, pos);
            for (int j = 0; j < live.Count; j++)
                intakes.Add(Key(live[j]));
        }
        var catches = new List<Vector3i>();
        var directTanks = new List<Vector3i>();
        foreach (long ik in intakes)
        {
            Vector3i intake = FromKey(ik);
            if (!LiveWaterSource(world, intake))
                continue;
            float frac;
            if (!intakeFrac.TryGetValue(ik, out frac))
                frac = 0f;
            float feed = rate * PumpEngineScale(world, intake);
            frac += feed;
            int units = (int)frac;
            frac -= units;
            intakeFrac[ik] = frac;
            if (units <= 0)
                continue;
            ReachFromIntake(world, intake, catches, directTanks);
            for (int c = 0; c < catches.Count; c++)
                AddIntakeToCatch(world, catches[c], units, changed);
            for (int t = 0; t < directTanks.Count; t++)
                AddIntakeToTank(world, directTanks[t], units, changed);
        }
    }

    static void AddIntakeToCatch(World world, Vector3i catchPos, int units, List<Vector3i> changed)
    {
        Tank t = GetTank(catchPos);
        int beforeUnits = t.Units;
        int beforeQuality = t.Quality;
        int cap = HSWorkingStuffSettings.RainCatchCapacity;
        if ((t.Mods & ModFilter) != 0 && t.Units > 0 && t.Quality < QualityClean)
            t.Units = 0;
        int space = cap - t.Units;
        if (space <= 0)
        {
            if (t.Units > 0)
                PushToTanks(world, catchPos, ref t, changed);
            SetTank(catchPos, t);
            if (t.Units != beforeUnits || t.Quality != beforeQuality)
                changed.Add(catchPos);
            return;
        }
        int add = units < space ? units : space;
        int incoming = (t.Mods & ModFilter) != 0 ? QualityClean : QualityMurky;
        t.Quality = t.Units <= 0 ? incoming : MixQuality(t.Quality, incoming);
        t.Units += add;
        if (t.Units > 0)
            PushToTanks(world, catchPos, ref t, changed);
        SetTank(catchPos, t);
        if (t.Units != beforeUnits || t.Quality != beforeQuality)
            changed.Add(catchPos);
    }

    static void AddIntakeToTank(World world, Vector3i tankPos, int units, List<Vector3i> changed)
    {
        Tank t = GetTank(tankPos);
        int beforeUnits = t.Units;
        int beforeQuality = t.Quality;
        int cap = HSWorkingStuffSettings.TankCapacity;
        int space = cap - t.Units;
        if (space <= 0)
            return;
        int add = units < space ? units : space;
        t.Quality = t.Units <= 0 ? QualityMurky : MixQuality(t.Quality, QualityMurky);
        t.Units += add;
        SetTank(tankPos, t);
        if (t.Units != beforeUnits || t.Quality != beforeQuality)
            changed.Add(tankPos);
        CascadeTanks(world, tankPos, changed);
    }

    static void PushToTanks(World world, Vector3i catchPos, ref Tank c, List<Vector3i> changed)
    {
        List<Vector3i> targets = FeedTanks(world, catchPos);
        int cap = HSWorkingStuffSettings.TankCapacity;
        for (int i = 0; i < targets.Count && c.Units > 0; i++)
        {
            Tank t = GetTank(targets[i]);
            // A filtered catch flushes murky water out rather than letting it spoil the clean feed.
            bool flush = (c.Mods & ModFilter) != 0 && c.Quality >= QualityClean && t.Units > 0 && t.Quality < QualityClean;
            if (flush)
                t.Units = 0;
            int space = cap - t.Units;
            if (space <= 0)
                continue;
            int move = space < c.Units ? space : c.Units;
            t.Quality = t.Units <= 0 ? c.Quality : MixQuality(t.Quality, c.Quality);
            t.Units += move;
            c.Units -= move;
            SetTank(targets[i], t);
            changed.Add(targets[i]);
        }
        if (targets.Count > 0)
            CascadeTanks(world, targets[0], changed);
    }

    public static void Load()
    {
        tanks.Clear();
        migrate.Clear();
        try
        {
            string dir = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(dir))
                return;
            string path = Path.Combine(dir, "HSWorkingStuff.tanks");
            if (!File.Exists(path))
                return;
            foreach (string line in File.ReadAllLines(path))
            {
                string[] f = line.Split(',');
                if (f.Length < 5)
                    continue;
                int x, y, z, u, q;
                if (!int.TryParse(f[0], out x) || !int.TryParse(f[1], out y) || !int.TryParse(f[2], out z))
                    continue;
                int.TryParse(f[3], out u);
                int.TryParse(f[4], out q);
                int mods = 0;
                float frac = 0f;
                if (f.Length > 5)
                    int.TryParse(f[5], out mods);
                if (f.Length > 6)
                    float.TryParse(f[6], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out frac);
                SetTank(new Vector3i(x, y, z), new Tank { Units = u, Quality = q, Mods = mods, Frac = frac });
            }
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] tank load: " + e.Message);
        }
    }

    public static void Save()
    {
        try
        {
            string dir = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(dir))
                return;
            string path = Path.Combine(dir, "HSWorkingStuff.tanks");
            var lines = new List<string>();
            foreach (var kv in tanks)
            {
                Vector3i p = FromKey(kv.Key);
                Tank t = kv.Value;
                lines.Add(p.x + "," + p.y + "," + p.z + "," + t.Units + "," + t.Quality + "," + t.Mods + ","
                    + t.Frac.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            }
            File.WriteAllLines(path, lines.ToArray());
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] tank save: " + e.Message);
        }
    }

    public static string QualityText(int quality)
    {
        return quality >= QualityClean ? HSLoc.Get("hsWaterClean", "clean") : HSLoc.Get("hsWaterMurky", "murky");
    }

    static string LevelText(Tank t, int cap)
    {
        string q = t.Units <= 0 ? HSLoc.Get("hsWaterEmpty", "empty") : QualityText(t.Quality);
        return HSLoc.Format("hsTankPrompt", "{0}/{1} {2}", t.Units, cap, q);
    }

    public static string TankPrompt(World world, Vector3i pos)
    {
        int per = HSWorkingStuffSettings.TankCapacity;
        Tank show = GetTank(pos);
        int cap = per;
        if (world != null)
        {
            List<Vector3i> bank = ConnectedTanks(world, pos);
            int n = bank.Count > 0 ? bank.Count : 1;
            cap = n * per;
            show.Units = PoolUnits(world, pos);
            for (int i = 0; i < bank.Count; i++)
            {
                Tank bt = GetTank(bank[i]);
                if (bt.Units <= 0)
                    continue;
                show.Quality = bt.Quality;
                break;
            }
        }
        string level = LevelText(show, cap);
        if (world != null && LiveIntakesFrom(world, pos).Count > 0)
            level += "\n" + HSLoc.Get("hsTankLakeMurky", "Lake intake: filling with murky water");
        return level;
    }

    public static string ModsPrompt(int mods)
    {
        if (mods == 0)
            return HSLoc.Get("hsRainNoTools", "No dew tools: murky water");
        var names = new List<string>();
        for (int i = 0; i < ModItems.Length; i++)
        {
            if ((mods & ModFlags[i]) == 0)
                continue;
            ItemClass ic = ItemClass.GetItemClass(ModItems[i]);
            names.Add(ic != null ? ic.GetLocalizedItemName() : ModItems[i]);
        }
        string list = string.Join(", ", names.ToArray());
        if ((mods & ModFilter) != 0)
            return HSLoc.Format("hsRainToolsClean", "Tools: {0} (clean water)", list);
        return HSLoc.Format("hsRainTools", "Tools: {0} (murky water)", list);
    }

    public static string RainCatchPrompt(World world, Vector3i pos)
    {
        Tank t = GetTank(pos);
        string level = LevelText(t, HSWorkingStuffSettings.RainCatchCapacity);
        string feed = FeedTanks(world, pos).Count > 0
            ? HSLoc.Get("hsRainFeeds", "Piped to a tank")
            : HSLoc.Get("hsRainNoFeed", "Not piped to a tank");
        string lake = world != null && LiveIntakesFrom(world, pos).Count > 0
            ? "\n" + ((t.Mods & ModFilter) != 0
                ? HSLoc.Get("hsRainLakeClean", "Lake intake: cleaning to clean water")
                : HSLoc.Get("hsRainLakeMurky", "Lake intake: murky until a dew filter is fitted"))
            : "";
        string sky = world != null && !OpenToSky(world, pos, world.GetBlock(pos).Block)
            ? "\n" + HSLoc.Get("hsRainCovered", "Covered: needs open sky")
            : "";
        return level + "\n" + ModsPrompt(t.Mods) + "\n" + feed + lake + sky;
    }

    public static string ColdReasonKey(Taps taps)
    {
        return taps.ColdEmpty ? "coldempty" : "colddry";
    }

    public static string HotReasonKey(Taps taps)
    {
        if (!taps.HeaterSeen)
            return "hotnoheater";
        if (!taps.HeaterPowered)
            return "hotnopower";
        return taps.HotEmpty ? "hotempty" : "hotdry";
    }

    public static string ColdReason(Taps taps)
    {
        if (taps.ColdEmpty)
            return HSLoc.Get("hsSinkColdEmpty", "Cold: the tank is empty. Pipe a rain catch or lake intake to it, or pour water in");
        return HSLoc.Get("hsSinkColdNone", "Cold: no tank or intake piped in");
    }

    public static string HotReason(Taps taps)
    {
        if (!taps.HeaterSeen)
            return HSLoc.Get("hsSinkHotNoHeater", "Hot: needs pipes to a combi boiler");
        if (!taps.HeaterPowered)
            return HSLoc.Get("hsSinkHotNoPower", "Hot: the boiler has no power");
        if (taps.HotEmpty)
            return HSLoc.Get("hsSinkHotEmpty", "Hot: the tank behind the boiler is empty");
        return HSLoc.Get("hsSinkHotDry", "Hot: the boiler has no tank or intake behind it");
    }

    public static string TapLine(Source s)
    {
        if (s.Infinite)
            return HSLoc.Format("hsSinkLake", "Lake feed, {0}", QualityText(s.Quality));
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        int pool = PoolUnits(world, s.TankPos);
        return HSLoc.Format("hsSinkTank", "Tank {0} {1}", pool, QualityText(s.Quality));
    }

    public static string SinkPrompt(World world, Vector3i pos)
    {
        Taps taps = FindTaps(world, pos);
        string cold = taps.Cold.Ok
            ? HSLoc.Format("hsSinkCold", "Cold: {0}", TapLine(taps.Cold))
            : ColdReason(taps);
        string hot = taps.Hot.Ok
            ? HSLoc.Format("hsSinkHot", "Hot: {0}", TapLine(taps.Hot))
            : HotReason(taps);
        return cold + "\n" + hot;
    }
}
