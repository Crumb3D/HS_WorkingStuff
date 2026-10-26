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

    // The fridge-freezer's freezer compartment (its second window): it freezes slower than a chest freezer.
    public const int CompartmentFrom = TileEntityHSFridge.ViewSlots;

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
        return ColdNow(iv, now, last, RoomRate);
    }

    public static float ColdNow(ItemValue iv, ulong now, ulong last, float lossPerTick)
    {
        float c = GetCold(iv);
        if (now > last && lossPerTick > 0f)
            c -= (now - last) * lossPerTick;
        return Mathf.Max(0f, c);
    }

    public const string MetaBin = "hsBin";

    // 0 inventory or chopping board, 1 fridge, 2 chest freezer, 3 fridge-freezer compartment.
    public static void Mark(ItemValue iv, float rate, float cold)
    {
        if (iv == null)
            return;
        int bin = 0;
        if (cold > 0.7f)
            bin = 2;
        else if (cold > 0f)
            bin = 3;
        else if (rate < 0.7f && rate > 0.25f)
            bin = 1;
        else if (rate < 0.2f && rate > 0.05f)
            bin = 3;
        else if (rate <= 0.05f && rate > 0f)
            bin = 2;
        iv.SetMetadata(MetaBin, bin);
    }

    public static int Bin(ItemValue iv)
    {
        int b;
        if (iv != null && iv.TryGetMetadata(MetaBin, out b))
            return b;
        return 0;
    }

    public static float SpoilRate(ItemValue iv)
    {
        switch (Bin(iv))
        {
            case 1: return HSWorkingStuffSettings.FridgeMultiplier;
            case 2: return HSWorkingStuffSettings.FreezerMultiplier;
            case 3: return HSWorkingStuffSettings.FridgeFreezerMultiplier;
            default: return 1f;
        }
    }

    static float LossFor(int bin)
    {
        if (bin == 1)
            return FridgeRate;
        return RoomRate;
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

    public static string TooltipLine(ItemStack stack, ulong now)
    {
        ItemValue iv = HSItem.Val(stack);
        if (!IsFrozen(iv))
            return null;
        int bin = Bin(iv);
        if (bin == 2 || bin == 3)
        {
            float days = HSSpoil.CalendarDays(iv, now, SpoilRate(iv));
            return HSLoc.Format("hsFrozenKeeps", "Frozen. Spoils in {0} days", days.ToString("0.0"));
        }
        float loss = LossFor(bin);
        float h = ColdNow(iv, now, HSSpoil.LastTick(iv, now), loss) / Mathf.Max(0.01f, loss) / TicksPerHour;
        float then = HSSpoil.CalendarDays(iv, now, bin == 1 ? HSWorkingStuffSettings.FridgeMultiplier : 1f);
        string thenDays = then.ToString("0.0");
        if (h < 1f)
            return HSLoc.Format("hsFrozenThenMins", "Frozen. Defrosts in {0} min, then spoils in {1} days", Mathf.Max(1, Mathf.CeilToInt(h * 60f)), thenDays);
        return HSLoc.Format("hsFrozenThenHours", "Frozen. Defrosts in {0}h, then spoils in {1} days", h.ToString("0.0"), thenDays);
    }

    public static void TellFrozen(EntityAlive who)
    {
        var p = who as EntityPlayerLocal;
        if (p != null)
            GameManager.ShowTooltip(p, HSLoc.Get("hsFrozenNoEat", "It's frozen solid. Let it thaw, or defrost it at a microwave, cooker, campfire or grill."));
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

    // Campfire, cooker and grill stand in for a chopping board: the game has no chopping board.
    public static bool IsPrepStation(Block block)
    {
        if (block == null || block is BlockHSMicrowave)
            return false;
        string n = block.GetBlockName();
        if (string.IsNullOrEmpty(n))
            return false;
        if (n == "campfire" || n.StartsWith("hsStove"))
            return true;
        return n.IndexOf("Grill") >= 0;
    }

    public static bool PrepReady(Block block, Vector3i pos)
    {
        if (block == null)
            return false;
        string n = block.GetBlockName();
        if (n != null && n.StartsWith("hsStove"))
            return HSStovePower.IsPowered(pos);
        return true;
    }

    public static BlockActivationCommand[] WithDefrost(BlockActivationCommand[] cmds, bool enabled)
    {
        if (cmds == null)
            cmds = new BlockActivationCommand[0];
        for (int i = 0; i < cmds.Length; i++)
        {
            if (cmds[i].text == "hsdefrost")
            {
                cmds[i].enabled = enabled;
                return cmds;
            }
        }
        var all = new BlockActivationCommand[cmds.Length + 1];
        System.Array.Copy(cmds, all, cmds.Length);
        all[cmds.Length] = new BlockActivationCommand("hsdefrost", "campfire", enabled);
        return all;
    }

    public static void DoDefrost(EntityPlayerLocal player, bool ready)
    {
        if (player == null)
            return;
        if (!ready)
        {
            GameManager.ShowTooltip(player, HSLoc.Get("hsDefrostNoPower", "No power: wire this cooker to defrost"));
            return;
        }
        int n = ThawPlayer(player);
        if (n == 0)
        {
            GameManager.ShowTooltip(player, HSLoc.Get("hsDefrostNone", "Nothing frozen on you"));
            return;
        }
        player.PlayOneShot("campfire_complete_item");
        GameManager.ShowTooltip(player, HSLoc.Format("hsDefrosted", "Defrosted {0} frozen food", n));
    }
}

[HarmonyPatch(typeof(BlockWorkstation), "GetBlockActivationCommands")]
public static class HSPatchPrepDefrost
{
    static void Postfix(BlockWorkstation __instance, Vector3i _blockPos, ref BlockActivationCommand[] __result)
    {
        if (!HSWorkingStuffSettings.EnableFreezing || !HSFrozen.IsPrepStation(__instance))
            return;
        __result = HSFrozen.WithDefrost(__result, HSFrozen.PrepReady(__instance, _blockPos));
    }
}

[HarmonyPatch(typeof(BlockWorkstation), "OnBlockActivated", new System.Type[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
public static class HSPatchPrepDefrostUse
{
    static bool Prefix(BlockWorkstation __instance, string _commandName, Vector3i _blockPos, EntityPlayerLocal _player)
    {
        if (_commandName != "hsdefrost" || !HSFrozen.IsPrepStation(__instance))
            return true;
        HSFrozen.DoDefrost(_player, HSFrozen.PrepReady(__instance, _blockPos));
        return false;
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

// Frozen and fresh of the same food stay in separate stacks.
[HarmonyPatch(typeof(ItemStack), "CanStackWith", new System.Type[] { typeof(ItemStack), typeof(bool) })]
public static class HSPatchFrozenStack
{
    static void Postfix(ItemStack __instance, ItemStack _other, ref bool __result)
    {
        if (!__result || __instance == null || _other == null)
            return;
        if (HSFrozen.IsFrozen(HSItem.Val(__instance)) != HSFrozen.IsFrozen(HSItem.Val(_other)))
            __result = false;
    }
}

[HarmonyPatch(typeof(ItemStack), "StackTransferCount")]
public static class HSPatchFrozenMove
{
    static bool Prefix(ItemStack __instance, ItemStack _other, ref int __result)
    {
        if (__instance == null || _other == null)
            return true;
        if (HSFrozen.IsFrozen(HSItem.Val(__instance)) != HSFrozen.IsFrozen(HSItem.Val(_other)))
        {
            __result = 0;
            return false;
        }
        return true;
    }
}
