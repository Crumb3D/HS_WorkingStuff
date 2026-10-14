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
    public const string SmellyBuff = "buffHSSmelly";
    public const string DirtyBuff = "buffHSDirtyClothes";
    public const string WetBuff = "buffHSWetClothes";
    // Short marker the server puts on a player who washed up at a sink.
    public const string SinkWashBuff = "buffHSWashedSink";

    static ulong lastTick;
    static float nextTick;
    static bool sinkSeen, showerSeen, bathSeen, inWater, soaking;
    // World hour before which another quick (sink / lakeside) wash, or a new soak in water, does nothing.
    static float quickUntil, soakUntil;
    static float soakStart, soakTarget;

    public static void Clear()
    {
        lastTick = 0UL;
        nextTick = 0f;
        sinkSeen = showerSeen = bathSeen = inWater = soaking = false;
        quickUntil = soakUntil = soakStart = soakTarget = 0f;
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

    static void QuickWash(EntityPlayerLocal p, string key, string text)
    {
        World world = GameManager.Instance.World;
        float body = Mathf.Max(0f, Body(p) - QuickWashHours());
        p.Buffs.SetCustomVar(BodyCVar, body);
        quickUntil = NowHours(world) + 1f;
        GameManager.ShowTooltip(p, HSLoc.Get(key, text));
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
        float hours = 0f;
        if (lastTick != 0UL && now > lastTick && now - lastTick < 2000UL)
            hours = (now - lastTick) / 1000f;
        lastTick = now;
        try
        {
            if (HSWorkingStuffSettings.EnableBodySmell)
            {
                bool sink = p.Buffs.HasBuff(SinkWashBuff);
                bool shower = p.Buffs.HasBuff(HSWash.ShowerBuff);
                bool bath = p.Buffs.HasBuff(HSWash.BathBuff);
                if (sink && !sinkSeen)
                    QuickWash(p, "hsSinkWashed", "Washed up at the sink: about 5 m less smell");
                float body = Body(p);
                if (bath && !bathSeen)
                    body = 0f;
                else if (shower && !showerSeen)
                    body *= Random.Range(0f, 0.1f);
                sinkSeen = sink;
                showerSeen = shower;
                bathSeen = bath;
                body = Soak(p, body, hours, NowHours(world));
                if (!HSWash.IsWashed(p) && !(soaking && inWater))
                    body += hours;
                p.Buffs.SetCustomVar(BodyCVar, body);
                Toggle(p, SmellyBuff, body >= HSWorkingStuffSettings.BodySmellStartHours);
            }
            else
                Toggle(p, SmellyBuff, false);
            bool dirty = false, wet = false;
            if (HSWorkingStuffSettings.EnableLaundry && p.equipment != null)
            {
                float nowH = NowHours(world);
                float rate = 1f / Mathf.Max(1f, HSWorkingStuffSettings.ClothesDirtyHours);
                int n = p.equipment.GetSlotCount();
                for (int i = 0; i < n; i++)
                {
                    ItemValue iv = p.equipment.GetSlotItem(i);
                    if (!IsClothing(iv))
                        continue;
                    if (hours > 0f)
                        iv.SetMetadata(MetaDirt, Mathf.Min(1f, Dirt(iv) + hours * rate));
                    if (Dirt(iv) >= 0.5f)
                        dirty = true;
                    if (Wetness(iv, nowH) > 0f)
                        wet = true;
                }
            }
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

    // Extra smell radius in metres: the body after a day without a wash, plus clothes that are at least half dirty.
    public static float SmellRadius(EntityPlayer p)
    {
        if (p == null || p.Buffs == null)
            return 0f;
        float r = 0f;
        if (HSWorkingStuffSettings.EnableBodySmell)
        {
            float h = p.Buffs.GetCustomVar(BodyCVar);
            float start = HSWorkingStuffSettings.BodySmellStartHours;
            float full = Mathf.Max(start + 1f, HSWorkingStuffSettings.BodySmellFullHours);
            if (h >= start)
                r += Mathf.Lerp(10f, HSWorkingStuffSettings.BodySmellRadius, (h - start) / (full - start));
        }
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
        if (r < 5f)
            return 0f;
        return Mathf.Min(r, 100f);
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
            n++;
        }
        return n;
    }
}
