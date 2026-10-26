using System.Collections.Generic;
using Audio;
using UnityEngine;

// Clothes and armour you wear get dirty, and you get smelly, as the hours go by. An unwashed body and dirty clothes
// both add to how far zombies can smell you. A plumbed washer cleans clothes but they come out wet, and wet clothes
// are cold to wear until they air dry or go through a dryer.
public static class HSLaundry
{
    public const string MetaDirt = "hsDirt";
    // World hour the clothes came out of the washer; below zero means dry.
    public const string MetaWet = "hsWetH";
    // Hours since the player last washed.
    public const string BodyCVar = "hsBodySmell";
    public const string BodyMetresCVar = ".hsSmellBodyM";
    public const string ClothesMetresCVar = ".hsSmellClothesM";
    public const string DirtPctCVar = ".hsDirtPct";
    public const string WetHoursCVar = ".hsWetHours";
    public const string SmellyBuff = "buffHSSmelly";
    public const string DirtyBuff = "buffHSDirtyClothes";
    public const string WetBuff = "buffHSWetClothes";
    // Short marker the server puts on a player who washed up at a sink.
    public const string SinkWashBuff = "buffHSWashedSink";
    public const string SinkWashColdBuff = "buffHSWashedSinkCold";

    static ulong lastTick;
    static float nextTick;
    static Vector3 lastPos;
    static bool hasPos;
    static bool sinkSeen, sinkColdSeen, showerSeen, bathSeen, coldSeen, inWater, soaking;
    // World hour before which another quick (sink / lakeside) wash, or a new soak in water, does nothing.
    static float quickUntil, soakUntil;
    static float soakStart, soakTarget;

    struct Run
    {
        public float Until;
        public bool Dryer;
        public int PlayerId;
    }

    static readonly Dictionary<long, Run> runs = new Dictionary<long, Run>();

    public const string WasherHum = "hsWasherHum";

    public static void VisitRuns(System.Action<Vector3i> visit)
    {
        if (visit == null || runs.Count == 0)
            return;
        foreach (KeyValuePair<long, Run> kv in runs)
            visit(HSWater.FromKey(kv.Key));
    }

    public static void Clear()
    {
        lastTick = 0UL;
        nextTick = 0f;
        hasPos = false;
        sinkSeen = sinkColdSeen = showerSeen = bathSeen = coldSeen = inWater = soaking = false;
        quickUntil = soakUntil = soakStart = soakTarget = 0f;
        runs.Clear();
    }

    public static bool Busy(Vector3i pos)
    {
        return runs.ContainsKey(HSWater.Key(pos));
    }

    public static int SecondsLeft(Vector3i pos)
    {
        Run run;
        if (!runs.TryGetValue(HSWater.Key(pos), out run))
            return 0;
        return Mathf.Max(0, Mathf.CeilToInt(run.Until - Time.time));
    }

    // Hours of body smell worth 5 m of smell radius on the BodySmellStartHours..FullHours ramp.
    public static float QuickWashHours()
    {
        float start = HSWorkingStuffSettings.BodySmellStartHours;
        float full = Mathf.Max(start + 1f, HSWorkingStuffSettings.BodySmellFullHours);
        float span = Mathf.Max(1f, HSWorkingStuffSettings.BodySmellRadius - 10f);
        return 5f * (full - start) / span;
    }

    public static bool QuickWashReady(EntityPlayerLocal p)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (NowHours(world) >= quickUntil)
            return true;
        if (p != null)
            GameManager.ShowTooltip(p, HSLoc.Get("hsWashTooSoon", "You have just had a quick wash. Another one won't help for an hour; a shower or bath will"));
        return false;
    }

    static float Body(EntityPlayerLocal p)
    {
        return p.Buffs.GetCustomVar(BodyCVar);
    }

    static void QuickWash(EntityPlayerLocal p, string key, string text, float portion = 1f)
    {
        World world = GameManager.Instance.World;
        float body = Mathf.Max(0f, Body(p) - QuickWashHours() * portion);
        p.Buffs.SetCustomVar(BodyCVar, body);
        quickUntil = NowHours(world) + 1f;
        int rinsed = RinseWorn(p, HSWorkingStuffSettings.WaterWashClothes * portion);
        if (rinsed > 0)
            GameManager.ShowTooltip(p, HSLoc.Get(key + "Clothes", text + ", and a bit of dirt off your clothes"));
        else
            GameManager.ShowTooltip(p, HSLoc.Get(key, text));
    }

    // Lakeside / sink splash: a little dirt comes off worn clothes. A washer still does the proper job.
    public static int RinseWorn(EntityPlayerLocal p, float amount)
    {
        if (p == null || p.equipment == null || amount <= 0f || !HSWorkingStuffSettings.EnableLaundry)
            return 0;
        int n = 0;
        int slots = p.equipment.GetSlotCount();
        for (int i = 0; i < slots; i++)
        {
            ItemValue iv = p.equipment.GetSlotItem(i);
            if (!IsClothing(iv))
                continue;
            float d = Dirt(iv);
            if (d <= 0f)
                continue;
            iv.SetMetadata(MetaDirt, Mathf.Max(0f, d - amount));
            n++;
        }
        return n;
    }

    public static bool Immersed(EntityPlayerLocal p)
    {
        return p.inWaterPercent >= 0.45f || p.isHeadUnderwater;
    }

    static int SoakedPercent(float body)
    {
        return soakStart > 0f ? Mathf.RoundToInt(100f * (soakStart - body) / soakStart) : 0;
    }

    // Lakes, rivers, pools: crouch at the edge to wash like a sink. In the water itself the smell comes off slowly (Soak).
    public static bool NaturalWash(EntityPlayerLocal p)
    {
        if (p == null || p.Buffs == null || !HSWorkingStuffSettings.EnableBodySmell)
            return false;
        if (Immersed(p))
        {
            GameManager.ShowTooltip(p, soaking
                ? HSLoc.Format("hsWashSoaking", "Washing in the water: {0}% of your body smell off so far. Stay in to keep washing", SoakedPercent(Body(p)))
                : HSLoc.Get("hsWashSoakIdle", "The water has done what it can for now. A shower or bath gets you properly clean"));
            return false;
        }
        if (!QuickWashReady(p))
            return false;
        QuickWash(p, "hsWashLakeside", "Splashed yourself clean: about 5 m less smell");
        return true;
    }

    // Up to the waist in water: picks 50-70% to come off and takes it off over WaterWashHours. Once an hour.
    static float Soak(EntityPlayerLocal p, float body, float hours, float now)
    {
        bool wet = Immersed(p);
        if (wet && !inWater && now >= soakUntil && body >= 1f)
        {
            soakStart = body;
            soakTarget = body * (1f - Random.Range(0.5f, 0.7f));
            soaking = true;
            soakUntil = now + 1f;
            GameManager.ShowTooltip(p, HSLoc.Get("hsWashSoakStart", "In the water: your smell is slowly washing off"));
        }
        else if (!wet && inWater && soaking)
        {
            soaking = false;
            GameManager.ShowTooltip(p, HSLoc.Format("hsWashSoakLeft", "Out of the water: {0}% of your body smell washed off", SoakedPercent(body)));
        }
        inWater = wet;
        if (soaking && wet && hours > 0f)
        {
            float rate = (soakStart - soakTarget) / Mathf.Max(0.05f, HSWorkingStuffSettings.WaterWashHours);
            body = Mathf.Max(soakTarget, body - rate * hours);
            float clothes = HSWorkingStuffSettings.WaterWashClothes * hours / Mathf.Max(0.05f, HSWorkingStuffSettings.WaterWashHours);
            RinseWorn(p, clothes);
            if (body <= soakTarget + 0.001f)
            {
                soaking = false;
                GameManager.ShowTooltip(p, HSLoc.Format("hsWashSoakDone", "Washed in the water: {0}% of your body smell gone", SoakedPercent(body)));
            }
        }
        return body;
    }

    public static float NowHours(World world)
    {
        return world != null ? world.worldTime / 1000f : 0f;
    }

    public static bool IsClothing(ItemValue iv)
    {
        return iv != null && !iv.IsEmpty() && iv.ItemClass is ItemClassArmor;
    }

    public static float Dirt(ItemValue iv)
    {
        float d;
        if (iv != null && iv.TryGetMetadata(MetaDirt, out d))
            return Mathf.Clamp01(d);
        return 0f;
    }

    public static float Wetness(ItemValue iv, float nowH)
    {
        float w;
        if (iv == null || !iv.TryGetMetadata(MetaWet, out w) || w < 0f)
            return 0f;
        float dry = Mathf.Max(0.1f, HSWorkingStuffSettings.AirDryHours);
        return Mathf.Clamp01(1f - (nowH - w) / dry);
    }

    // Any peer with a local player: the clock for body smell and dirt runs on the player's own machine.
    public static void ClientTick()
    {
        float t = Time.time;
        if (t < nextTick)
            return;
        nextTick = t + 2f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal p = world != null ? world.GetPrimaryPlayer() : null;
        if (p == null || p.Buffs == null || p.IsDead())
        {
            lastTick = 0UL;
            return;
        }
        ulong now = world.worldTime;
        if (HSGod.Active(p))
        {
            lastTick = now;
            HSGod.Clear(p);
            return;
        }
        float hours = 0f;
        if (lastTick != 0UL && now > lastTick && now - lastTick < 2000UL)
            hours = (now - lastTick) / 1000f;
        lastTick = now;
        try
        {
            float body = 0f;
            if (HSWorkingStuffSettings.EnableBodySmell)
            {
                bool sink = p.Buffs.HasBuff(SinkWashBuff);
                bool sinkCold = p.Buffs.HasBuff(SinkWashColdBuff);
                bool shower = p.Buffs.HasBuff(HSWash.ShowerBuff);
                bool bath = p.Buffs.HasBuff(HSWash.BathBuff);
                bool coldWash = p.Buffs.HasBuff(HSWash.ColdShowerBuff) || p.Buffs.HasBuff(HSWash.ColdBathBuff);
                if (sink && !sinkSeen)
                    QuickWash(p, "hsSinkWashed", "Washed up at the sink: about 5 m less smell");
                else if (sinkCold && !sinkColdSeen)
                    QuickWash(p, "hsSinkWashedCold", "Washed up in cold water: about half as much smell off", 0.5f);
                body = Body(p);
                if (bath && !bathSeen)
                    body = 0f;
                else if (shower && !showerSeen)
                    body *= Random.Range(0f, 0.1f);
                else if (coldWash && !coldSeen)
                    body *= 0.5f;
                sinkSeen = sink;
                sinkColdSeen = sinkCold;
                showerSeen = shower;
                bathSeen = bath;
                coldSeen = coldWash;
                body = Soak(p, body, hours, NowHours(world));
                if (!HSWash.IsWashed(p) && !(soaking && inWater))
                    body += hours * (Sweating(p) ? 2f : 1f);
                p.Buffs.SetCustomVar(BodyCVar, body);
                p.Buffs.SetCustomVar(BodyMetresCVar, BodyMetresFromHours(body));
                Toggle(p, SmellyBuff, body >= HSWorkingStuffSettings.BodySmellStartHours);
            }
            else
                Toggle(p, SmellyBuff, false);
            bool dirty = false, wet = false;
            float clothesM = 0f;
            float wetHours = 0f;
            float worst = 0f;
            if (HSWorkingStuffSettings.EnableLaundry && p.equipment != null)
            {
                float nowH = NowHours(world);
                float rate = 1f / Mathf.Max(1f, HSWorkingStuffSettings.ClothesDirtyHours);
                float dirtHours = hours;
                if (hours > 0f)
                {
                    dirtHours += hours * Mathf.Clamp(body / Mathf.Max(1f, HSWorkingStuffSettings.BodySmellStartHours), 0f, 2f);
                    if (Moved(p) && FieldUnder(world, p))
                        dirtHours += hours * 2f;
                }
                int n = p.equipment.GetSlotCount();
                for (int i = 0; i < n; i++)
                {
                    ItemValue iv = p.equipment.GetSlotItem(i);
                    if (!IsClothing(iv))
                        continue;
                    if (dirtHours > 0f)
                        iv.SetMetadata(MetaDirt, Mathf.Min(1f, Dirt(iv) + dirtHours * rate));
                    float d = Dirt(iv);
                    if (d > worst)
                        worst = d;
                    if (d >= 0.2f)
                        dirty = true;
                    if (d >= 0.5f)
                        clothesM += d * HSWorkingStuffSettings.DirtyClothesSmell;
                    float w = Wetness(iv, nowH);
                    if (w > 0f)
                    {
                        wet = true;
                        wetHours = Mathf.Max(wetHours, w * Mathf.Max(0.1f, HSWorkingStuffSettings.AirDryHours));
                    }
                }
            }
            p.Buffs.SetCustomVar(DirtPctCVar, Mathf.Round(worst * 100f));
            p.Buffs.SetCustomVar(ClothesMetresCVar, clothesM);
            p.Buffs.SetCustomVar(WetHoursCVar, wetHours);
            Toggle(p, DirtyBuff, dirty);
            Toggle(p, WetBuff, wet);
        }
        catch { }
    }

    static void Toggle(EntityPlayerLocal p, string buff, bool on)
    {
        bool has = p.Buffs.HasBuff(buff);
        if (on && !has)
            p.Buffs.AddBuff(buff);
        else if (!on && has)
            p.Buffs.RemoveBuff(buff);
    }

    // Running, or a body already hot, builds smell twice as fast.
    static bool Sweating(EntityPlayerLocal p)
    {
        if (p.IsRunning)
            return true;
        return p.Buffs != null && p.Buffs.GetCustomVar("_coretemp") >= 80f;
    }

    static bool Moved(EntityPlayerLocal p)
    {
        Vector3 now = p.position;
        bool moved = hasPos && (now - lastPos).sqrMagnitude > 0.35f;
        lastPos = now;
        hasPos = true;
        return moved;
    }

    static bool FieldUnder(World world, EntityPlayerLocal p)
    {
        if (world == null || p.shelterPercent > 0.5f)
            return false;
        int x = Mathf.FloorToInt(p.position.x);
        int y = Mathf.FloorToInt(p.position.y);
        int z = Mathf.FloorToInt(p.position.z);
        if (IsField(world.GetBlock(x, y, z)))
            return true;
        return y > 1 && IsField(world.GetBlock(x, y - 1, z));
    }

    static bool IsField(BlockValue bv)
    {
        Block block = bv.Block;
        if (block == null || bv.isair || block.blockMaterial == null)
            return false;
        string id = block.blockMaterial.id;
        string name = block.GetBlockName();
        if (Has(id, "water") || Has(name, "water") || Has(id, "concrete") || Has(id, "asphalt") || Has(name, "asphalt"))
            return false;
        return Has(id, "dirt") || Has(id, "grass") || Has(id, "sand") || Has(id, "gravel") || Has(id, "snow")
            || Has(id, "mud") || Has(id, "plant") || Has(id, "crop") || Has(id, "leaves") || Has(id, "clay")
            || Has(name, "terrDirt") || Has(name, "terrGrass") || Has(name, "terrForest") || Has(name, "terrSand")
            || Has(name, "terrGravel") || Has(name, "terrSnow") || Has(name, "planted") || Has(name, "crop")
            || Has(name, "corn") || Has(name, "wheat") || Has(name, "mushroom") || Has(name, "hay");
    }

    static bool Has(string s, string part)
    {
        return !string.IsNullOrEmpty(s) && s.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static float BodyMetresFromHours(float hours)
    {
        float start = HSWorkingStuffSettings.BodySmellStartHours;
        float full = Mathf.Max(start + 1f, HSWorkingStuffSettings.BodySmellFullHours);
        if (hours < start)
            return 0f;
        return Mathf.Lerp(10f, HSWorkingStuffSettings.BodySmellRadius, (hours - start) / (full - start));
    }

    // Extra smell radius in metres: the body after a day without a wash, plus clothes that are at least half dirty.
    public static float SmellRadius(EntityPlayer p)
    {
        if (p == null || p.Buffs == null)
            return 0f;
        float r = 0f;
        if (HSWorkingStuffSettings.EnableBodySmell)
            r += BodyMetresFromHours(p.Buffs.GetCustomVar(BodyCVar));
        if (HSWorkingStuffSettings.EnableLaundry && p.equipment != null)
        {
            int n = p.equipment.GetSlotCount();
            for (int i = 0; i < n; i++)
            {
                ItemValue iv = p.equipment.GetSlotItem(i);
                if (!IsClothing(iv))
                    continue;
                float d = Dirt(iv);
                if (d >= 0.5f)
                    r += d * HSWorkingStuffSettings.DirtyClothesSmell;
            }
        }
        r += HSNeeds.DirtyNearby(p);
        if (r < 5f)
            return 0f;
        return Mathf.Min(r, 100f);
    }

    // An accident stinks now, the way carried meat does. Hours already worse than this are left alone.
    public static void StinkAtLeast(EntityPlayerLocal p, float hours)
    {
        if (!HSWorkingStuffSettings.EnableBodySmell || p == null || p.Buffs == null || hours <= 0f)
            return;
        float cur = p.Buffs.GetCustomVar(BodyCVar);
        if (cur < hours)
            p.Buffs.SetCustomVar(BodyCVar, hours);
    }

    // An accident fills every piece you are wearing. A pee also soaks them.
    public static void SoilWorn(EntityPlayerLocal p, bool wet)
    {
        if (p == null || p.equipment == null)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        float nowH = NowHours(world);
        int n = p.equipment.GetSlotCount();
        for (int i = 0; i < n; i++)
        {
            ItemValue iv = p.equipment.GetSlotItem(i);
            if (!IsClothing(iv))
                continue;
            iv.SetMetadata(MetaDirt, 1f);
            if (wet)
                iv.SetMetadata(MetaWet, nowH);
        }
    }

    // Degrees F wet clothes take off the air temperature the player feels.
    public static float WetCold(EntityPlayer p)
    {
        if (!HSWorkingStuffSettings.EnableLaundry || p == null || p.equipment == null)
            return 0f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        float nowH = NowHours(world);
        float cold = 0f;
        int n = p.equipment.GetSlotCount();
        for (int i = 0; i < n; i++)
        {
            ItemValue iv = p.equipment.GetSlotItem(i);
            if (IsClothing(iv))
                cold += Wetness(iv, nowH) * HSWorkingStuffSettings.WetClothesCold;
        }
        return cold;
    }

    public static string TooltipLine(ItemStack stack)
    {
        if (!HSWorkingStuffSettings.EnableLaundry || stack == null || stack.IsEmpty())
            return null;
        ItemValue iv = HSItem.Val(stack);
        if (!IsClothing(iv) || (!iv.HasMetadata(MetaDirt) && !iv.HasMetadata(MetaWet)))
            return null;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        float nowH = NowHours(world);
        float dirt = Dirt(iv);
        string state;
        if (dirt >= 0.75f)
            state = HSLoc.Get("hsClothesFilthy", "Filthy");
        else if (dirt >= 0.5f)
            state = HSLoc.Get("hsClothesDirty", "Dirty");
        else if (dirt >= 0.2f)
            state = HSLoc.Get("hsClothesWorn", "Worn");
        else
            state = HSLoc.Get("hsClothesClean", "Clean");
        string line = HSLoc.Format("hsClothesDirt", "{0}: {1}% dirty", state, Mathf.RoundToInt(dirt * 100f));
        if (dirt >= 0.2f)
            line += "\n" + HSLoc.Get("hsClothesNeedsWash", "Needs a wash. The Dirty Clothes icon is on");
        if (dirt >= 0.5f)
            line += "\n" + HSLoc.Get("hsClothesSmell", "Adds to your smell: wash it in a washing machine");
        float wet = Wetness(iv, nowH);
        if (wet > 0f)
        {
            float left = wet * Mathf.Max(0.1f, HSWorkingStuffSettings.AirDryHours);
            line += "\n" + HSLoc.Format("hsClothesWet", "Wet: cold to wear, dries in {0} h. A washing line, airer or dryer is quicker", left.ToString("0.0"));
        }
        return line;
    }

    // Clothes in a washer, or wet clothes in a dryer.
    public static int Count(ItemStack[] items, bool dryer, float nowH)
    {
        int n = 0;
        if (items == null)
            return 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null || items[i].IsEmpty())
                continue;
            ItemValue iv = HSItem.Val(items[i]);
            if (!IsClothing(iv))
                continue;
            if (!dryer || Wetness(iv, nowH) > 0f)
                n++;
        }
        return n;
    }

    public static bool HasWashWater(World world, Vector3i pos)
    {
        if (!HSWorkingStuffSettings.EnablePlumbing)
            return true;
        HSWater.Taps taps = HSWater.FindTaps(world, pos);
        if (!taps.Cold.Ok)
            return false;
        if (taps.Cold.Infinite)
            return true;
        return HSWater.GetTank(taps.Cold.TankPos).Units >= Mathf.Max(1, HSWorkingStuffSettings.WasherJars);
    }

    public static bool TakeWashWater(World world, Vector3i pos, EntityPlayer player)
    {
        if (!HSWorkingStuffSettings.EnablePlumbing)
            return true;
        HSWater.Taps taps = HSWater.FindTaps(world, pos);
        HSWater.Source src = taps.Cold;
        if (!src.Ok)
        {
            HSWorkingStuffNet.Tell(player, HSWater.ColdReason(taps));
            return false;
        }
        int jars = Mathf.Max(1, HSWorkingStuffSettings.WasherJars);
        if (!HSWater.TakeFromSource(ref src, jars))
        {
            HSWorkingStuffNet.Tell(player, HSLoc.Format("hsWasherShort", "Not enough water: a wash needs {0} jars", jars));
            return false;
        }
        if (!src.Infinite)
            HSWorkingStuffNet.BroadcastTank(src.TankPos);
        return true;
    }

    public static int Process(ItemStack[] items, bool dryer, float nowH)
    {
        int n = 0;
        if (items == null)
            return 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null || items[i].IsEmpty())
                continue;
            ItemValue iv = HSItem.Val(items[i]);
            if (!IsClothing(iv))
                continue;
            if (dryer)
            {
                if (Wetness(iv, nowH) <= 0f)
                    continue;
                iv.SetMetadata(MetaWet, -1f);
            }
            else
            {
                iv.SetMetadata(MetaDirt, 0f);
                iv.SetMetadata(MetaWet, nowH);
            }
            HSItem.SetVal(items[i], iv);
            n++;
        }
        return n;
    }

    public static void ServerTick()
    {
        if (!HSWorkingStuffNet.IsAuthority || runs.Count == 0)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        float now = Time.time;
        var done = new List<long>();
        foreach (var kv in runs)
        {
            if (now >= kv.Value.Until)
                done.Add(kv.Key);
        }
        for (int i = 0; i < done.Count; i++)
            Finish(world, done[i]);
    }

    public static bool TryStart(World world, Vector3i pos, EntityPlayer player, bool dryer)
    {
        long k = HSWater.Key(pos);
        if (runs.ContainsKey(k))
        {
            HSWorkingStuffNet.Tell(player, HSLoc.Get("hsWasherBusy", "Already running"));
            return false;
        }
        IHSFridgeInv inv = HSFridgeInv.Of(world.GetTileEntity(pos));
        if (inv == null || !inv.IsPowered)
        {
            HSWorkingStuffNet.Tell(player, HSLoc.Get("hsLaundryNoPower", "No power: wire it up first"));
            return false;
        }
        inv.EnsureItems();
        float nowH = NowHours(world);
        if (Count(inv.Items, dryer, nowH) == 0)
        {
            HSWorkingStuffNet.Tell(player, dryer
                ? HSLoc.Get("hsDryerEmpty", "Nothing you're wearing or carrying is wet")
                : HSLoc.Get("hsWasherEmpty", "Nothing you're wearing or carrying needs a wash"));
            return false;
        }
        if (!dryer && HSWorkingStuffSettings.EnablePlumbing)
        {
            HSWater.Taps taps = HSWater.FindTaps(world, pos);
            HSWater.Source src = taps.Cold;
            if (!src.Ok)
            {
                HSWorkingStuffNet.Tell(player, HSWater.ColdReason(taps));
                return false;
            }
            int jars = HSWorkingStuffSettings.WasherJars;
            if (!HSWater.TakeFromSource(ref src, jars))
            {
                HSWorkingStuffNet.Tell(player, HSLoc.Format("hsWasherShort", "Not enough water: a wash needs {0} jars", jars));
                return false;
            }
            if (!src.Infinite)
                HSWorkingStuffNet.BroadcastTank(src.TankPos);
        }
        float secs = Mathf.Max(1f, HSWorkingStuffSettings.WasherSeconds);
        runs[k] = new Run { Until = Time.time + secs, Dryer = dryer, PlayerId = player != null ? player.entityId : -1 };
        Vector3 at = pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
        Audio.Manager.BroadcastPlay(at, WasherHum);
        HSWorkingStuffNet.Tell(player, HSLoc.Format("hsWasherStarted", "Running: about {0} seconds", Mathf.RoundToInt(secs)));
        return true;
    }

    static void Finish(World world, long k)
    {
        Run run;
        runs.TryGetValue(k, out run);
        runs.Remove(k);
        Vector3i pos = HSWater.FromKey(k);
        Vector3 at = pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
        Audio.Manager.BroadcastStop(at, WasherHum);
        IHSFridgeInv inv = HSFridgeInv.Of(world.GetTileEntity(pos));
        EntityPlayer player = world.GetEntity(run.PlayerId) as EntityPlayer;
        if (inv == null)
            return;
        inv.EnsureItems();
        int n = Process(inv.Items, run.Dryer, NowHours(world));
        inv.SetModified();
        Audio.Manager.BroadcastPlay(at, run.Dryer ? "hsDryerDone" : "hsWasherDone");
        if (player == null)
            return;
        HSWorkingStuffNet.Tell(player, run.Dryer
            ? HSLoc.Format("hsDried", "Dried {0} items", n)
            : HSLoc.Format("hsWashed", "Washed {0} items. They are wet: dry them in a dryer, or they dry on you in {1} hours", n, HSWorkingStuffSettings.AirDryHours.ToString("0")));
    }
}

public static class HSGod
{
    public static bool Active(EntityAlive e)
    {
        try
        {
            return e != null && e.IsGodMode != null && e.IsGodMode.Value;
        }
        catch
        {
            return false;
        }
    }

    public static void Clear(EntityPlayerLocal p)
    {
        if (p == null || p.Buffs == null)
            return;
        p.Buffs.SetCustomVar(HSLaundry.BodyCVar, 0f);
        p.Buffs.SetCustomVar(HSLaundry.BodyMetresCVar, 0f);
        p.Buffs.SetCustomVar(HSLaundry.ClothesMetresCVar, 0f);
        p.Buffs.SetCustomVar(HSLaundry.WetHoursCVar, 0f);
        p.Buffs.SetCustomVar(HSNeeds.BladderCVar, 0f);
        p.Buffs.SetCustomVar(HSNeeds.BowelCVar, 0f);
        Remove(p, HSLaundry.SmellyBuff);
        Remove(p, HSLaundry.DirtyBuff);
        Remove(p, HSLaundry.WetBuff);
        Remove(p, HSNeeds.PeeBuff);
        Remove(p, HSNeeds.PooBuff);
        CleanWorn(p);
    }

    static void Remove(EntityPlayerLocal p, string buff)
    {
        if (p.Buffs.HasBuff(buff))
            p.Buffs.RemoveBuff(buff);
    }

    static void CleanWorn(EntityPlayerLocal p)
    {
        if (p.equipment == null)
            return;
        int n = p.equipment.GetSlotCount();
        for (int i = 0; i < n; i++)
        {
            ItemValue iv = p.equipment.GetSlotItem(i);
            if (!HSLaundry.IsClothing(iv))
                continue;
            iv.SetMetadata(HSLaundry.MetaDirt, 0f);
            iv.SetMetadata(HSLaundry.MetaWet, -1f);
        }
    }
}
