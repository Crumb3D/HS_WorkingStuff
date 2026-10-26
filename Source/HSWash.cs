using HarmonyLib;
using UnityEngine;

// Washed buffs: all smell (carried meat, smelly food, dysentery) is cleared while one is running.
// Body smell itself drops when the buff starts: a shower takes 90-100% of it, a bath all of it (HSLaundry.ClientTick).
public static class HSWash
{
    public const string ShowerBuff = "buffHSWashed";
    public const string BathBuff = "buffHSWashedBath";
    public const string ColdShowerBuff = "buffHSWashedCold";
    public const string ColdBathBuff = "buffHSWashedColdBath";

    public static void Apply(EntityPlayer player, bool bath)
    {
        Add(player, bath ? BathBuff : ShowerBuff);
    }

    public static void ApplyCold(EntityPlayer player, bool bath)
    {
        Add(player, bath ? ColdBathBuff : ColdShowerBuff);
    }

    static void Add(EntityPlayer player, string buff)
    {
        if (player == null || player.Buffs == null)
            return;
        player.Buffs.AddBuff(buff);
        player.Stealth.SetSmellRadiusTarget(-1, false, false);
    }

    public static bool IsWashed(EntityPlayer player)
    {
        return player != null && player.Buffs != null
            && (player.Buffs.HasBuff(ShowerBuff) || player.Buffs.HasBuff(BathBuff)
                || player.Buffs.HasBuff(ColdShowerBuff) || player.Buffs.HasBuff(ColdBathBuff));
    }
}

// Vanilla drinks from water with an empty hand (ItemActionEat, BlocksAllowed water). Crouching turns that into a wash.
[HarmonyPatch(typeof(ItemActionEat), "CanInteract")]
public static class HSPatchWaterWashPrompt
{
    public const string Label = "hsLblWash";

    public static bool Applies(ItemActionEat action, ItemActionData data)
    {
        if (!HSWorkingStuffSettings.EnableBodySmell || action == null || data == null || data.invData == null)
            return false;
        if (action.ConditionBlockTypes == null || !action.ConditionBlockTypes.Contains(240))
            return false;
        var p = HSInvData.HoldingEntity(data.invData) as EntityPlayerLocal;
        return p != null && p.IsCrouching;
    }

    static void Postfix(ItemActionEat __instance, ItemActionData _actionData, ref string __result)
    {
        try
        {
            if (__result == "lblContextActionDrink" && Applies(__instance, _actionData))
                __result = Label;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ItemActionEat), "ExecuteAction")]
public static class HSPatchWaterWash
{
    static float nextUse;

    static bool Prefix(ItemActionEat __instance, ItemActionData _actionData, bool _bReleased)
    {
        try
        {
            if (!HSPatchWaterWashPrompt.Applies(__instance, _actionData))
                return true;
            if (!_bReleased || Time.time < nextUse)
                return false;
            if (__instance.CanInteract(_actionData) != HSPatchWaterWashPrompt.Label)
                return false;
            nextUse = Time.time + 1.5f;
            var p = HSInvData.HoldingEntity(_actionData.invData) as EntityPlayerLocal;
            if (p == null)
                return true;
            if (HSLaundry.NaturalWash(p))
                HSSinkCmds.DrinkAnim(p, "hsStream");
            return false;
        }
        catch
        {
            return true;
        }
    }
}

// Only ever runs for the player owned by this peer (host's own player, or a client's), so the primary player is the one smelling.
[HarmonyPatch(typeof(PlayerStealth), "SmellUpdateItemsAndBlood")]
public static class HSPatchWashedSmell
{
    static bool Prefix(ref PlayerStealth __instance)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal local = world != null ? world.GetPrimaryPlayer() : null;
        if (!HSWash.IsWashed(local))
            return true;
        __instance.SetSmellRadiusTarget(-1, false, false);
        return false;
    }

    // Body odour and dirty clothes sit on the vanilla smell radius (the same one meat uses).
    // Rain or water still hides it. Walls, windows and shut doors dampen it per zombie in Better Ears;
    // an open door on that side does not.
    static void Postfix(PlayerStealth __instance)
    {
        try
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityPlayerLocal local = world != null ? world.GetPrimaryPlayer() : null;
            if (local == null || local.IsDead() || HSWash.IsWashed(local))
                return;
            if (__instance.smellWet >= 3f || __instance.smellWetRate >= 0.01f)
                return;
            float extra = HSLaundry.SmellRadius(local);
            if (extra < 1f)
                return;
            if (extra > __instance.smellRadiusTarget)
                __instance.smellRadiusTarget = extra;
        }
        catch { }
    }
}
