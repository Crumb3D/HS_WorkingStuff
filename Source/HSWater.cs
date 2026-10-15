using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Rain catch collects and pushes down its pipes into tanks. A tank only feeds what is in it;
// it does not join the pipes on one side to the pipes on the other.
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
            || name == HeaterName || name == VanillaHeaterName || name == TankName || name == IntakeName
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

    public static bool TouchesWater(World world, Vector3i pos)
    {
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3i p = pos + dirs[i];
            if (world.GetWater(p).HasMass())
                return true;
        }
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
            if (n == IntakeName && TouchesWater(world, p))
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
                Tank t = GetTank(tp);
                if (t.Units > 0)
                {
                    var stored = new Source { Ok = true, Infinite = false, TankPos = tp, Quality = t.Quality };
                    if (hot)
                        Offer(ref taps.Hot, stored);
                    else
                        Offer(ref taps.Cold, stored);
                }
                else if (hot)
                    taps.HotEmpty = true;
                else
                    taps.ColdEmpty = true;
                continue;
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
                    seen.Add(k);
                    Vector3i tp = Parent(np, nbv);
                    if (tankSeen.Add(Key(tp)))
                        found.Add(tp);
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
                if (nn == IntakeName)
                {
                    if (TouchesWater(world, np))
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
            n = Math.Min(count, GetTank(src.TankPos).Units / per);
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
                Tank t = GetTank(src.TankPos);
                t.Units += n * per;
                SetTank(src.TankPos, t);
            }
            return 0;
        }
        return n;
    }

    public static bool PourIntoTank(EntityPlayer player, Vector3i pos, bool all)
    {
        Tank t = GetTank(pos);
        int cap = HSWorkingStuffSettings.TankCapacity;
        int bucket = HSWorkingStuffSettings.BucketUnits;
        int added = PourArray(HSInv.GetBagStacks(player), ref t, cap, bucket, all);
        added += PourArray(HSInv.GetToolStacks(player), ref t, cap, bucket, all);
        if (added <= 0 && !all)
            added += PourHolding(player, ref t, cap, bucket);
        if (added <= 0)
            return false;
        SetTank(pos, t);
        return true;
    }

    static int PourArray(ItemStack[] stacks, ref Tank t, int cap, int bucket, bool all)
    {
        if (stacks == null)
            return 0;
        int n = 0;
        for (int i = 0; i < stacks.Length; i++)
        {
            if (t.Units >= cap)
                break;
            ItemStack s = stacks[i];
            ItemValue ivPour = HSItem.Val(s);
            if (s == null || s.IsEmpty() || ivPour == null || ivPour.ItemClass == null)
                continue;
            string name = ivPour.ItemClass.Name;
            int give = 0;
            string empty = null;
            if (name == "drinkJarRiverWater" || name == "drinkJarBoiledWater")
            {
                give = 1;
                empty = "drinkJarEmpty";
            }
            else if (name == "bucketRiverWater")
            {
                give = bucket;
                empty = "bucketEmpty";
            }
            else
                continue;
            int space = cap - t.Units;
            if (space <= 0)
                break;
            int q = ItemQuality(name);
            t.Quality = t.Units <= 0 ? q : MixQuality(t.Quality, q);
            t.Units += give;
            if (t.Units > cap)
                t.Units = cap;
            int c = HSItem.Count(s);
            HSItem.SetVal(s, ItemClass.GetItem(empty));
            HSItem.SetCount(s, c);
            n++;
            if (!all)
                break;
        }
        return n;
    }

    static int PourHolding(EntityPlayer player, ref Tank t, int cap, int bucket)
    {
        int kind = HoldingKind(player);
        if (kind != 3 && kind != 4)
            return 0;
        if (t.Units >= cap)
            return 0;
        ItemStack data = HSInv.GetHeld(player);
        ItemValue ivHold = HSItem.Val(data);
        if (data == null || ivHold == null || ivHold.ItemClass == null)
            return 0;
        int give = kind == 4 ? bucket : 1;
        int q = ItemQuality(ivHold.ItemClass.Name);
        t.Quality = t.Units <= 0 ? q : MixQuality(t.Quality, q);
        t.Units += give;
        if (t.Units > cap)
            t.Units = cap;
        HSItem.SetVal(data, ItemClass.GetItem(kind == 4 ? "bucketEmpty" : "drinkJarEmpty"));
        return 1;
    }

    public static bool TakeFromSource(ref Source src, int need)
    {
        if (!src.Ok || need <= 0)
            return false;
        if (src.Infinite)
            return true;
        Tank t = GetTank(src.TankPos);
        if (t.Units < need)
            return false;
        t.Units -= need;
        SetTank(src.TankPos, t);
        return true;
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
        for (int i = 0; i < changed.Count; i++)
            HSWorkingStuffNet.BroadcastTank(changed[i]);
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

    public static string TankPrompt(Vector3i pos)
    {
        return LevelText(GetTank(pos), HSWorkingStuffSettings.TankCapacity);
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
        string sky = world != null && !OpenToSky(world, pos, world.GetBlock(pos).Block)
            ? "\n" + HSLoc.Get("hsRainCovered", "Covered: needs open sky")
            : "";
        return level + "\n" + ModsPrompt(t.Mods) + "\n" + feed + sky;
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
            return HSLoc.Get("hsSinkColdEmpty", "Cold: the tank is empty. Pipe a rain catch to it or pour water in");
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
        Tank t = GetTank(s.TankPos);
        return HSLoc.Format("hsSinkTank", "Tank {0} {1}", t.Units, QualityText(s.Quality));
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
