using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Cold is stored on the item in ticks (0 to FreezeHours worth). A dedicated server cannot tick a
// client's own bag, so anything outside a freezer or fridge is read lazily as thawing at room rate
// from the hsLast stamp the spoil tick keeps.
public static class HSFrozen
{
    public const string MetaCold = "hsCold";
    public const string MetaFrozen = "hsFrozen";
    const float TicksPerHour = 1000f;
    const float MaxFreezeStep = 100f;

    static float FullTicks
    {
        get { return Mathf.Max(0.1f, HSWorkingStuffSettings.FreezeHours) * TicksPerHour; }
    }

    public static float RoomRate
    {
        get { return HSWorkingStuffSettings.FreezeHours / Mathf.Max(0.05f, HSWorkingStuffSettings.ThawHours); }
    }

    static float FridgeRate
    {
        get { return HSWorkingStuffSettings.FreezeHours / Mathf.Max(0.05f, HSWorkingStuffSettings.FridgeThawHours); }
    }

    public static float ColdRateForBlock(Block block, TileEntity te)
    {
        if (block == null || !HSWorkingStuffSettings.EnableFridge)
            return -RoomRate;
        string n = block.GetBlockName();
        var powered = te as TileEntityPowered;
        bool on = powered != null && powered.IsPowered;
        if (n == "hsFreezer" && on)
            return 1f;
        if ((n == "hsFridge" || n == "hsFridgeWhite") && on)
            return -FridgeRate;
        return -RoomRate;
    }

    // The fridge-freezer's bottom two rows are its freezer compartment: it freezes slower than a chest freezer.
    public const int CompartmentFrom = 12;

    public static bool HasCompartment(Block block)
    {
        return block != null && block.GetBlockName() == "hsFridgeWhite";
    }

    public static float CompartmentColdRate(TileEntity te)
    {
        var powered = te as TileEntityPowered;
        if (!HSWorkingStuffSettings.EnableFridge || powered == null || !powered.IsPowered)
            return -RoomRate;
        return HSWorkingStuffSettings.FreezeHours / Mathf.Max(0.1f, HSWorkingStuffSettings.FridgeFreezerFreezeHours);
    }

    public static float GetCold(ItemValue iv)
    {
        float c;
        if (iv != null && iv.TryGetMetadata(MetaCold, out c))
            return c;
        return 0f;
    }

    static bool Flag(ItemValue iv)
    {
        int f;
        return iv != null && iv.TryGetMetadata(MetaFrozen, out f) && f != 0;
    }

    // Returns true while the item is frozen after this step.
    public static bool Tick(ItemValue iv, float rate, ulong dt)
    {
        if (!HSWorkingStuffSettings.EnableFreezing || iv == null)
            return false;
        float c = GetCold(iv);
        if (rate <= 0f && c <= 0f)
            return false;
        bool frozen = Flag(iv);
        if (rate > 0f)
            c += Mathf.Min(dt, MaxFreezeStep) * rate;
        else
            c += dt * rate;
        float full = FullTicks;
        if (c >= full)
        {
            c = full;
            frozen = true;
        }
        if (c <= 0f)
        {
            c = 0f;
            frozen = false;
        }
        iv.SetMetadata(MetaCold, c);
        iv.SetMetadata(MetaFrozen, frozen ? 1 : 0);
        return frozen;
    }

    public static float ColdNow(ItemValue iv, ulong now, ulong last)
    {
        float c = GetCold(iv);
        if (now > last)
            c -= (now - last) * RoomRate;
        return Mathf.Max(0f, c);
    }

    public static bool IsFrozen(ItemValue iv)
    {
        if (!HSWorkingStuffSettings.EnableFreezing || !Flag(iv) || !HSSpoil.IsPerishable(iv))
            return false;
        ulong now = Now();
        return ColdNow(iv, now, HSSpoil.LastTick(iv, now)) > 0f;
    }

    public static float HoursToThaw(ItemValue iv)
    {
        ulong now = Now();
        return ColdNow(iv, now, HSSpoil.LastTick(iv, now)) / Mathf.Max(0.01f, RoomRate) / TicksPerHour;
    }

    public static void Thaw(ItemValue iv)
    {
        if (iv == null || !iv.HasMetadata(MetaCold))
            return;
        iv.SetMetadata(MetaCold, 0f);
        iv.SetMetadata(MetaFrozen, 0);
    }

    public static void KeepColder(ItemValue keep, ItemValue incoming)
    {
        if (keep == null || incoming == null)
            return;
        if (GetCold(incoming) > GetCold(keep))
            keep.SetMetadata(MetaCold, GetCold(incoming));
        if (Flag(incoming))
            keep.SetMetadata(MetaFrozen, 1);
    }

    public static int ThawPlayer(EntityPlayer player)
    {
        int n = 0;
        HSInv.ForEachPlayerStack(player, s =>
        {
            ItemValue iv = HSItem.Val(s);
            if (IsFrozen(iv))
            {
                Thaw(iv);
                n += HSItem.Count(s);
            }
        });
        if (n > 0)
            HSInv.NotifyPlayer(player);
        return n;
    }

    public static bool AnyFrozenOn(EntityPlayer player)
    {
        bool any = false;
        HSInv.ForEachPlayerStack(player, s => { if (!any && IsFrozen(HSItem.Val(s))) any = true; });
        return any;
    }

    public static int UnfrozenCount(EntityPlayer player, ItemValue type)
    {
        int n = 0;
        HSInv.ForEachPlayerStack(player, s =>
        {
            ItemValue iv = HSItem.Val(s);
            if (iv != null && iv.ItemClass == type.ItemClass && !IsFrozen(iv))
                n += HSItem.Count(s);
        });
        return n;
    }

    public static string TooltipLine(ItemStack stack)
    {
        ItemValue iv = HSItem.Val(stack);
        if (!IsFrozen(iv))
            return null;
        float h = HoursToThaw(iv);
        if (h < 1f)
            return HSLoc.Format("hsFrozenMins", "Frozen: thaws in {0} min at room temperature", Mathf.Max(1, Mathf.CeilToInt(h * 60f)));
        return HSLoc.Format("hsFrozenHours", "Frozen: thaws in {0}h at room temperature", h.ToString("0.0"));
    }

    public static void TellFrozen(EntityAlive who)
    {
        var p = who as EntityPlayerLocal;
        if (p != null)
            GameManager.ShowTooltip(p, HSLoc.Get("hsFrozenNoEat", "It's frozen solid. Let it thaw, or defrost it in a microwave."));
    }

    static System.Reflection.FieldInfo invStackF, invEntityF;
    static System.Reflection.PropertyInfo invStackP, invEntityP;
    static bool invResolved;

    // ItemInventoryData.itemStack / holdingEntity are fields on 3.2 and properties on 3.3.
    public static void ReadInvData(ItemInventoryData d, out ItemStack stack, out EntityAlive who)
    {
        stack = null;
        who = null;
        if (d == null)
            return;
        if (!invResolved)
        {
            invResolved = true;
            invStackF = AccessTools.Field(typeof(ItemInventoryData), "itemStack");
            invEntityF = AccessTools.Field(typeof(ItemInventoryData), "holdingEntity");
            invStackP = invStackF == null ? AccessTools.Property(typeof(ItemInventoryData), "itemStack") : null;
            invEntityP = invEntityF == null ? AccessTools.Property(typeof(ItemInventoryData), "holdingEntity") : null;
        }
        try
        {
            stack = (invStackF != null ? invStackF.GetValue(d) : invStackP != null ? invStackP.GetValue(d, null) : null) as ItemStack;
            who = (invEntityF != null ? invEntityF.GetValue(d) : invEntityP != null ? invEntityP.GetValue(d, null) : null) as EntityAlive;
        }
        catch { }
    }

    static ulong Now()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        return world != null ? world.worldTime : 0UL;
    }
}

[HarmonyPatch(typeof(ItemActionEat), "ExecuteAction")]
public static class HSPatchFrozenEatHeld
{
    static bool Prefix(ItemActionData _actionData, bool _bReleased)
    {
        if (!_bReleased || _actionData == null || _actionData.invData == null)
            return true;
        ItemStack stack;
        EntityAlive who;
        HSFrozen.ReadInvData(_actionData.invData, out stack, out who);
        if (!HSFrozen.IsFrozen(HSItem.Val(stack)))
            return true;
        if (Time.time - _actionData.lastUseTime >= 1f)
            HSFrozen.TellFrozen(who);
        _actionData.lastUseTime = Time.time;
        return false;
    }
}

[HarmonyPatch(typeof(ItemActionEat), "ExecuteInstantAction")]
public static class HSPatchFrozenEatBag
{
    static bool Prefix(EntityAlive ent, ItemStack stack, ref bool __result)
    {
        if (!HSFrozen.IsFrozen(HSItem.Val(stack)))
            return true;
        HSFrozen.TellFrozen(ent);
        __result = false;
        return false;
    }
}

// Recipes only count food that is not frozen.
[HarmonyPatch(typeof(XUiM_PlayerInventory), "HasItems")]
public static class HSPatchFrozenCraft
{
    static void Postfix(IList<ItemStack> _itemStacks, int _multiplier, ref bool __result)
    {
        if (!__result || _itemStacks == null || !HSWorkingStuffSettings.EnableFreezing)
            return;
        EntityPlayer player = GameManager.Instance != null && GameManager.Instance.World != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player == null)
            return;
        for (int i = 0; i < _itemStacks.Count; i++)
        {
            ItemStack need = _itemStacks[i];
            ItemValue iv = HSItem.Val(need);
            if (iv == null || !HSSpoil.IsPerishable(iv))
                continue;
            if (HSFrozen.UnfrozenCount(player, iv) < HSItem.Count(need) * _multiplier)
            {
                __result = false;
                return;
            }
        }
    }
}
