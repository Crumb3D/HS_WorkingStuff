using System;
using UnityEngine;

public static class HSSpoil
{
    public const string MetaAcc = "hsAcc";
    public const string MetaLast = "hsLast";
    public const string MetaDry = "hsDry";
    public const string MetaInv = "hsInv";
    public const string MetaWater = "hsWater";
    public const ulong TicksPerDay = 24000UL;

    static int lastWarnDay = -1;
    static float nextTick;
    static ulong lastWorldTime;

    public static float Days(ItemValue iv)
    {
        float d = HSWorkingStuffSettings.SpoilDays;
        if (iv != null && iv.ItemClass != null && iv.ItemClass.Properties != null
            && iv.ItemClass.Properties.Values.ContainsKey("SpoilDays"))
        {
            float n = iv.ItemClass.Properties.GetFloat("SpoilDays");
            if (n > 0.05f)
                d = n;
        }
        return d;
    }

    public static ulong LifeTicksFor(ItemValue iv)
    {
        return (ulong)Mathf.Max(1000f, Days(iv) * TicksPerDay);
    }

    public static bool IsPerishable(ItemValue iv)
    {
        if (iv == null || iv.IsEmpty())
            return false;
        var ic = iv.ItemClass;
        if (ic == null || ic.Properties == null)
            return false;
        return ic.Properties.Values.ContainsKey("SpoilDays");
    }

    public static bool CanFridge(ItemValue iv)
    {
        if (iv == null || iv.IsEmpty())
            return true;
        return IsPerishable(iv) || IsSpoiled(iv);
    }

    public static bool CanFridge(ItemStack stack)
    {
        return stack == null || stack.IsEmpty() || CanFridge(HSItem.Val(stack));
    }

    public static bool IsMeatOrEgg(ItemValue iv)
    {
        if (iv == null || iv.ItemClass == null)
            return false;
        string n = iv.ItemClass.Name;
        return n == "foodRawMeat" || n == "foodEgg" || n == "foodEggBoiled"
            || n == "foodRawMeatBundle" || n == "foodCharredMeat" || n == "foodGrilledMeat"
            || n == "foodBoiledMeat" || n == "foodBaconAndEggs";
    }

    public static bool IsSpoiled(ItemValue iv)
    {
        if (iv == null || iv.ItemClass == null)
            return false;
        string n = iv.ItemClass.Name;
        return n == "foodRottingFlesh" || n == "hsSpoiledFood";
    }

    public static float RateForBlock(Block block, TileEntity te)
    {
        if (block == null)
            return 1f;
        string n = block.GetBlockName();
        if (n == "hsFridge" || n == "hsFridgeWhite" || n == "hsFreezer")
        {
            if (!HSWorkingStuffSettings.EnableFridge)
                return 1f;
            var powered = te as TileEntityPowered;
            if (powered != null && powered.IsPowered)
                return n == "hsFreezer" ? HSWorkingStuffSettings.FreezerMultiplier : HSWorkingStuffSettings.FridgeMultiplier;
            return 1f;
        }
        if (n == "hsCooler")
            return HSWorkingStuffSettings.EnableFridge ? HSWorkingStuffSettings.CoolerMultiplier : 1f;
        return 1f;
    }

    public static float CompartmentRate(TileEntity te)
    {
        var powered = te as TileEntityPowered;
        if (!HSWorkingStuffSettings.EnableFridge || powered == null || !powered.IsPowered)
            return 1f;
        return HSWorkingStuffSettings.FridgeFreezerMultiplier;
    }

    public static void StampFresh(ItemValue iv, ulong now)
    {
        if (iv == null || iv.IsEmpty())
            return;
        iv.SetMetadata(MetaAcc, 0f);
        iv.SetMetadata(MetaLast, (int)(now & 0x7FFFFFFF));
        iv.SetMetadata("hsLastHi", (int)(now >> 31));
    }

    public static void EnsureStamp(ItemValue iv, ulong now)
    {
        if (!IsPerishable(iv))
            return;
        if (!iv.HasMetadata(MetaAcc))
            StampFresh(iv, now);
        if (!iv.HasMetadata(MetaLast))
            WriteLast(iv, now);
    }

    static void WriteLast(ItemValue iv, ulong t)
    {
        iv.SetMetadata(MetaLast, (int)(t & 0x7FFFFFFF));
        iv.SetMetadata("hsLastHi", (int)(t >> 31));
    }

    static ulong ReadLast(ItemValue iv, ulong now)
    {
        int lo;
        if (!iv.TryGetMetadata(MetaLast, out lo))
            return now;
        int hi = 0;
        iv.TryGetMetadata("hsLastHi", out hi);
        ulong t = ((ulong)(uint)hi << 31) | (uint)lo;
        if (t > now)
            return now;
        return t;
    }

    public static float GetAcc(ItemValue iv)
    {
        float acc;
        if (iv != null && iv.TryGetMetadata(MetaAcc, out acc))
            return acc;
        return 0f;
    }

    public static float RemainingDays(ItemValue iv, ulong now)
    {
        if (!IsPerishable(iv))
            return -1f;
        EnsureStamp(iv, now);
        float acc = GetAcc(iv);
        float life = Days(iv);
        float left = life - acc / TicksPerDay;
        if (left < 0f)
            left = 0f;
        return left;
    }

    // Player-days left, stretched by the container. A chest freezer's 0.033 rate is about 90 days.
    public static float CalendarDays(ItemValue iv, ulong now, float rate)
    {
        float left = RemainingDays(iv, now);
        if (left < 0f)
            return left;
        if (rate > 0.001f)
            left /= rate;
        return left;
    }

    public static void KeepOlder(ItemValue keep, ItemValue incoming)
    {
        if (keep == null || incoming == null)
            return;
        if (!IsPerishable(keep) || !IsPerishable(incoming))
            return;
        HSFrozen.KeepColder(keep, incoming);
        float a = GetAcc(keep);
        float b = GetAcc(incoming);
        if (b > a)
        {
            keep.SetMetadata(MetaAcc, b);
            int lo, hi;
            if (incoming.TryGetMetadata(MetaLast, out lo))
                keep.SetMetadata(MetaLast, lo);
            if (incoming.TryGetMetadata("hsLastHi", out hi))
                keep.SetMetadata("hsLastHi", hi);
        }
    }

    public static ulong LastTick(ItemValue iv, ulong now)
    {
        return ReadLast(iv, now);
    }

    public static void TickStack(ItemStack stack, float rate, ulong now)
    {
        TickStack(stack, rate, -HSFrozen.RoomRate, now);
    }

    public static void TickStack(ItemStack stack, float rate, float cold, ulong now)
    {
        ItemValue iv = HSItem.Val(stack);
        if (stack == null || stack.IsEmpty() || iv == null)
            return;
        if (iv.ItemClass != null && iv.ItemClass.Name == "foodRawMeat")
            TickDry(stack, now);
        if (!IsPerishable(iv))
            return;
        EnsureStamp(iv, now);
        ulong last = ReadLast(iv, now);
        if (now <= last)
            return;
        bool frozen = HSFrozen.Tick(iv, cold, now - last);
        HSFrozen.Mark(iv, rate, cold);
        // Thawing food waits. The 3-day or 7-day clock starts once it is soft. A freezer keeps it frozen and still spoils, slowly.
        if (rate <= 0f || (frozen && cold < 0f))
        {
            WriteLast(iv, now);
            return;
        }
        float add = (now - last) * rate;
        float acc = GetAcc(iv) + add;
        WriteLast(iv, now);
        float life = LifeTicksFor(iv);
        if (acc >= life)
        {
            SpoilNow(stack);
            return;
        }
        iv.SetMetadata(MetaAcc, acc);
    }

    static void TickDry(ItemStack stack, ulong now)
    {
        float dry;
        ItemValue iv = HSItem.Val(stack);
        if (iv == null || !iv.TryGetMetadata(MetaDry, out dry))
            return;
        ulong last = ReadLast(iv, now);
        if (now > last)
            dry += (now - last);
        float need = HSWorkingStuffSettings.DryDays * TicksPerDay;
        if (dry >= need)
        {
            int c = HSItem.Count(stack);
            HSItem.SetVal(stack, ItemClass.GetItem("hsDriedMeat"));
            HSItem.SetCount(stack, c);
            return;
        }
        iv.SetMetadata(MetaDry, dry);
        WriteLast(iv, now);
    }

    public static void SpoilNow(ItemStack stack)
    {
        int c = HSItem.Count(stack);
        string dest = IsMeatOrEgg(HSItem.Val(stack)) ? "foodRottingFlesh" : "hsSpoiledFood";
        HSItem.SetVal(stack, ItemClass.GetItem(dest));
        HSItem.SetCount(stack, c);
    }

    public static void TickArray(ItemStack[] items, float rate, ulong now)
    {
        TickArray(items, rate, -HSFrozen.RoomRate, now);
    }

    public static void TickArray(ItemStack[] items, float rate, float cold, ulong now)
    {
        if (items == null)
            return;
        for (int i = 0; i < items.Length; i++)
            TickStack(items[i], rate, cold, now);
    }

    public static void OnGameUpdate()
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        float t = Time.time;
        if (t < nextTick)
            return;
        nextTick = t + 1f;
        ulong now = world.worldTime;
        if (lastWorldTime == 0)
            lastWorldTime = now;
        if (HSWorkingStuffSettings.EnableSpoil)
        {
            TickPlayers(world, now);
            TickChunks(world, now);
            MaybeWarn(world, now);
        }
        if (HSWorkingStuffSettings.EnablePlumbing)
        {
            HSWater.TickTanks(world, now, lastWorldTime);
            HSBoiler.Tick(world);
            HSPipeNet.Tick();
        }
        lastWorldTime = now;
    }

    static void TickPlayers(World world, ulong now)
    {
        var list = world.Players.list;
        if (list == null)
            return;
        for (int i = 0; i < list.Count; i++)
        {
            EntityPlayer p = list[i];
            if (p == null)
                continue;
            TickArray(HSInv.GetBagStacks(p), 1f, now);
            TickArray(HSInv.GetToolStacks(p), 1f, now);
        }
    }

    static void TickChunks(World world, ulong now)
    {
        if (world.ChunkCache == null)
            return;
        var chunks = world.ChunkCache.GetChunkArrayCopySync();
        if (chunks == null)
            return;
        for (int c = 0; c < chunks.Count; c++)
        {
            Chunk chunk = chunks[c];
            if (chunk == null || chunk.tileEntities == null)
                continue;
            var tes = chunk.tileEntities.list;
            for (int i = 0; i < tes.Count; i++)
            {
                TileEntity te = tes[i];
                if (te == null)
                    continue;
                Block block = te.blockValue.Block;
                float rate = RateForBlock(block, te);
                float cold = HSFrozen.ColdRateForBlock(block, te);
                IHSFridgeInv fridge = HSFridgeInv.Of(te);
                if (fridge != null)
                {
                    fridge.EnsureItems();
                    ItemStack[] items = fridge.Items;
                    if (HSFrozen.HasCompartment(block) && items != null)
                    {
                        float cRate = CompartmentRate(te);
                        float cCold = HSFrozen.CompartmentColdRate(te);
                        for (int k = 0; k < items.Length; k++)
                        {
                            if (k < HSFrozen.CompartmentFrom)
                                TickStack(items[k], rate, cold, now);
                            else
                                TickStack(items[k], cRate, cCold, now);
                        }
                    }
                    else
                        TickArray(items, rate, cold, now);
                    fridge.SetModified();
                    continue;
                }
                ItemStack[] stacks = HSInv.GetStorageStacks(te);
                if (stacks != null)
                {
                    TickArray(stacks, rate, cold, now);
                    TickDryingAndCompost(block, stacks, now);
                }
            }
        }
    }

    static void TickDryingAndCompost(Block block, ItemStack[] stacks, ulong now)
    {
        if (block == null || stacks == null)
            return;
        string n = block.GetBlockName();
        if (n == "hsDryingRack")
        {
            for (int i = 0; i < stacks.Length; i++)
            {
                ItemStack s = stacks[i];
                ItemValue iv = HSItem.Val(s);
                if (s.IsEmpty() || iv == null || iv.ItemClass == null)
                    continue;
                if (iv.ItemClass.Name != "foodRawMeat")
                    continue;
                if (!iv.HasMetadata(MetaDry))
                    iv.SetMetadata(MetaDry, 0f);
                HSItem.SetVal(s, iv);
                TickDry(s, now);
            }
            return;
        }
        if (n != "hsCompost")
            return;
        float need = HSWorkingStuffSettings.CompostDays * TicksPerDay;
        for (int i = 0; i < stacks.Length; i++)
        {
            ItemStack s = stacks[i];
            ItemValue iv = HSItem.Val(s);
            if (s.IsEmpty() || !IsSpoiled(iv))
                continue;
            float acc = GetAcc(iv);
            if (!iv.HasMetadata(MetaAcc))
                iv.SetMetadata(MetaAcc, 0f);
            ulong last = ReadLast(iv, now);
            if (now > last)
                acc += (now - last);
            WriteLast(iv, now);
            if (acc >= need)
            {
                int c = HSItem.Count(s);
                HSItem.SetVal(s, ItemClass.GetItem("resourcePotassiumNitratePowder"));
                HSItem.SetCount(s, c);
            }
            else
                iv.SetMetadata(MetaAcc, acc);
        }
    }

    static void MaybeWarn(World world, ulong now)
    {
        int day = GameUtils.WorldTimeToDays(now);
        int hour = GameUtils.WorldTimeToHours(now);
        if (hour != 6 || lastWarnDay == day)
            return;
        lastWarnDay = day;
        var list = world.Players.list;
        if (list == null)
            return;
        for (int i = 0; i < list.Count; i++)
        {
            EntityPlayer p = list[i];
            if (p == null)
                continue;
            string name = OldestName(p, now);
            if (name == null)
                continue;
            GameManager.ShowTooltipMP(p, HSLoc.Format("hsSpoilWarn", "{0} will spoil soon. Get it in a fridge.", name));
        }
    }

    static string OldestName(EntityPlayer p, ulong now)
    {
        string best = null;
        float bestLeft = HSWorkingStuffSettings.WarningDays;
        ScanWarn(HSInv.GetBagStacks(p), now, ref best, ref bestLeft);
        ScanWarn(HSInv.GetToolStacks(p), now, ref best, ref bestLeft);
        return best;
    }

    static void ScanWarn(ItemStack[] stacks, ulong now, ref string best, ref float bestLeft)
    {
        if (stacks == null)
            return;
        for (int i = 0; i < stacks.Length; i++)
        {
            ItemStack s = stacks[i];
            ItemValue iv = HSItem.Val(s);
            if (s == null || s.IsEmpty() || !IsPerishable(iv) || iv == null || iv.ItemClass == null)
                continue;
            float left = RemainingDays(iv, now);
            if (left < bestLeft)
            {
                bestLeft = left;
                best = iv.ItemClass.GetLocalizedItemName();
            }
        }
    }

    public static string TooltipLine(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty() || !IsPerishable(HSItem.Val(stack)))
            return null;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        ulong now = world != null ? world.worldTime : 0UL;
        string frozen = HSFrozen.TooltipLine(stack, now);
        if (frozen != null)
            return frozen;
        float left = CalendarDays(HSItem.Val(stack), now, HSFrozen.SpoilRate(HSItem.Val(stack)));
        if (left <= 0f)
            return HSLoc.Get("hsSpoilSoon", "Spoiling now");
        if (left < 1f)
            return HSLoc.Format("hsSpoilHours", "Spoils in {0}h", Mathf.CeilToInt(left * 24f));
        return HSLoc.Format("hsSpoilDays", "Spoils in {0} days", left.ToString("0.0"));
    }

    public static bool StorePerishables(EntityPlayer player, ItemStack[] dest)
    {
        if (player == null || dest == null)
            return false;
        bool any = false;
        any |= PullFromArray(HSInv.GetBagStacks(player), dest);
        any |= PullFromArray(HSInv.GetToolStacks(player), dest);
        return any;
    }

    static bool PullFromArray(ItemStack[] src, ItemStack[] dest)
    {
        if (src == null)
            return false;
        bool any = false;
        for (int pass = 0; pass < src.Length; pass++)
        {
            int best = -1;
            float bestAcc = -1f;
            for (int i = 0; i < src.Length; i++)
            {
                ItemStack s = src[i];
                if (s == null || s.IsEmpty() || !IsPerishable(HSItem.Val(s)) || IsSpoiled(HSItem.Val(s)))
                    continue;
                float acc = GetAcc(HSItem.Val(s));
                if (acc > bestAcc)
                {
                    bestAcc = acc;
                    best = i;
                }
            }
            if (best < 0)
                break;
            ItemStack take = src[best].Clone();
            src[best].Clear();
            if (ItemStack.AddToItemStackArray(dest, take) < 0)
            {
                HSItem.SetVal(src[best], HSItem.Val(take));
                HSItem.SetCount(src[best], HSItem.Count(take));
                break;
            }
            any = true;
        }
        return any;
    }

    public static bool TakeSpoiled(EntityPlayer player, ItemStack[] src)
    {
        if (player == null || src == null)
            return false;
        bool any = false;
        for (int i = 0; i < src.Length; i++)
        {
            if (src[i] == null || src[i].IsEmpty() || !IsSpoiled(HSItem.Val(src[i])))
                continue;
            ItemStack take = src[i].Clone();
            src[i].Clear();
            if (!HSInv.AddToPlayer(player, take))
            {
                HSItem.SetVal(src[i], HSItem.Val(take));
                HSItem.SetCount(src[i], HSItem.Count(take));
                continue;
            }
            any = true;
        }
        return any;
    }
}
