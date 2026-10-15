using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public class BlockHSSink : Block
{
    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return HSWorkingStuffSettings.EnablePlumbing;
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        World world = _world as World;
        if (world == null)
            return _blockValue.Block.GetLocalizedBlockName();
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSWater.SinkPrompt(world, _blockPos);
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return HSSinkCmds.Build(_world, _blockPos, _entityFocusing);
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return HSSinkCmds.Run(_commandName, _blockPos, _player);
    }
}

// Cold and hot taps. A tap with no supply stays in the menu greyed out, named for what it is missing.
public static class HSSinkCmds
{
    static readonly HashSet<string> off = new HashSet<string> { "colddry", "coldempty", "hotnoheater", "hotnopower", "hotdry", "hotempty", "notools" };

    public static bool IsOff(string command)
    {
        return command != null && off.Contains(command);
    }

    // What the tap offers depends on your hand: empty jars fill jars, an empty bucket fills the bucket,
    // anything else (or nothing) drinks. Fill all is always there for the containers in your bag.
    static string HandCommand(EntityAlive who)
    {
        int kind = HSWater.HoldingKind(who as EntityPlayer);
        if (kind == 1)
            return "filljar";
        if (kind == 2)
            return "fillbucket";
        return "drink";
    }

    public static BlockActivationCommand[] BuildCold(WorldBase world, Vector3i pos, EntityAlive who)
    {
        var list = new List<BlockActivationCommand>();
        World w = world as World;
        HSWater.Taps taps = w != null ? HSWater.FindTaps(w, pos) : new HSWater.Taps();
        AddTap(list, taps.Cold.Ok, HandCommand(who), "", "water", HSWater.ColdReasonKey(taps));
        return list.ToArray();
    }

    public static BlockActivationCommand[] Build(WorldBase world, Vector3i pos, EntityAlive who)
    {
        var list = new List<BlockActivationCommand>();
        World w = world as World;
        HSWater.Taps taps = w != null ? HSWater.FindTaps(w, pos) : new HSWater.Taps();
        string hand = HandCommand(who);
        AddTap(list, taps.Cold.Ok, hand, "", "water", HSWater.ColdReasonKey(taps));
        AddTap(list, taps.Hot.Ok, hand, "hot", "fire", HSWater.HotReasonKey(taps));
        if (HSWorkingStuffSettings.EnableBodySmell && (taps.Cold.Ok || taps.Hot.Ok))
            list.Add(new BlockActivationCommand("washup", "water", true));
        return list.ToArray();
    }

    static void AddTap(List<BlockActivationCommand> list, bool ok, string hand, string suffix, string icon, string reason)
    {
        if (!ok)
        {
            list.Add(new BlockActivationCommand(reason, icon, true));
            return;
        }
        list.Add(new BlockActivationCommand(hand + suffix, icon, true));
        list.Add(new BlockActivationCommand("fillall" + suffix, icon, true));
    }

    public static bool Run(string command, Vector3i pos, EntityPlayerLocal player)
    {
        if (IsOff(command))
        {
            if (player != null)
                GameManager.ShowTooltip(player, Localization.Get("blockcommand_" + command));
            return true;
        }
        byte kind;
        switch (command)
        {
            case "filljar":
            case "fillbucket": kind = HSWorkingStuffNet.SinkFill; break;
            case "fillall": kind = HSWorkingStuffNet.SinkFillAll; break;
            case "drink":
                kind = HSWorkingStuffNet.SinkDrink;
                HSNeeds.FromDrink(player);
                break;
            case "filljarhot":
            case "fillbuckethot": kind = HSWorkingStuffNet.SinkFillHot; break;
            case "fillallhot": kind = HSWorkingStuffNet.SinkFillAllHot; break;
            case "drinkhot":
                kind = HSWorkingStuffNet.SinkDrinkHot;
                HSNeeds.FromDrink(player);
                break;
            case "wash": kind = HSWorkingStuffNet.Wash; break;
            case "washup":
                if (!HSLaundry.QuickWashReady(player))
                    return true;
                kind = HSWorkingStuffNet.SinkWash;
                break;
            default: return false;
        }
        HSWorkingStuffNet.SendWater(kind, pos, player);
        Animate(player, kind);
        return true;
    }

    // Same as empty-hand drink from a lake (meleeHandPlayer Action2 Eat): UseItem + player_drinking.
    public static void DrinkAnim(EntityPlayerLocal player)
    {
        DrinkAnim(player, "player_drinking");
    }

    public static void DrinkAnim(EntityPlayerLocal player, string sound)
    {
        if (player == null)
            return;
        try
        {
            if (player.emodel != null && player.emodel.avatarController != null)
            {
                AvatarController av = player.emodel.avatarController;
                av.SetMeleeAttackSpeed(1f);
                av._resetTrigger(AvatarController.useItemHash);
                av.StartAnimationUse();
            }
            player.PlayOneShot(string.IsNullOrEmpty(sound) ? "player_drinking" : sound);
        }
        catch { }
    }

    static void Animate(EntityPlayerLocal player, byte kind)
    {
        if (player == null)
            return;
        if (kind == HSWorkingStuffNet.SinkDrink || kind == HSWorkingStuffNet.SinkDrinkHot)
        {
            DrinkAnim(player);
            return;
        }
        if (kind == HSWorkingStuffNet.SinkWash || kind == HSWorkingStuffNet.Wash)
        {
            DrinkAnim(player, "hsStream");
            return;
        }
        try
        {
            if (kind == HSWorkingStuffNet.SinkFill || kind == HSWorkingStuffNet.SinkFillHot)
            {
                if (player.emodel != null && player.emodel.avatarController != null)
                {
                    AvatarController av = player.emodel.avatarController;
                    av._resetTrigger(AvatarController.useItemHash);
                    av.StartAnimationUse();
                }
            }
            player.PlayOneShot("bucketfill_water");
        }
        catch { }
    }
}

// Vanilla handleBlockCommand uses `_commandIndex < Commands.Length` so -1 still indexes Commands[-1].
[HarmonyPatch(typeof(XUiC_Radial), "handleBlockCommand")]
public static class HSPatchRadialBlockCommand
{
    static bool Prefix(int _commandIndex, XUiC_Radial.RadialContextAbs _context)
    {
        try
        {
            var ctx = _context as XUiC_Radial.RadialContextBlock;
            if (ctx == null)
                return true;
            int n = ctx.Commands != null ? ctx.Commands.Length : 0;
            if (_commandIndex >= 0 && _commandIndex < n)
                return true;
            int custom = ctx.CustomCommands != null ? ctx.CustomCommands.Length : 0;
            int at = _commandIndex - n;
            return at >= 0 && at < custom;
        }
        catch
        {
            return true;
        }
    }
}

// Vanilla only hides disabled commands, so the unavailable ones are kept and drawn grey here.
[HarmonyPatch(typeof(XUiC_Radial), "SetCurrentBlockData")]
public static class HSPatchRadialGrey
{
    static readonly Color grey = new Color(0.4f, 0.4f, 0.4f, 1f);

    static void Postfix(XUiC_Radial __instance, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _entityFocusing)
    {
        try
        {
            Block b = _blockValue.Block;
            if (b == null || !(b is BlockHSSink || b is BlockHSRainCatch || b is BlockHSToilet || HSWater.IsSinkBlock(b)))
                return;
            BlockActivationCommand[] cmds = b.GetBlockActivationCommands(_world, _blockValue, _blockPos, _entityFocusing);
            XUiC_RadialEntry[] items = __instance.menuItem;
            bool[] state = __instance.menuItemState;
            if (cmds == null || items == null || state == null)
                return;
            for (int i = 0; i < items.Length && i < state.Length; i++)
            {
                if (!state[i] || items[i] == null)
                    continue;
                int idx = items[i].CommandIndex;
                if (idx < 0 || idx >= cmds.Length || (!HSSinkCmds.IsOff(cmds[idx].text) && !HSNeeds.IsOff(cmds[idx].text)))
                    continue;
                items[i].SetSprite("ui_game_symbol_" + cmds[idx].icon, grey);
            }
        }
        catch { }
    }
}

// Emptying a full bucket at a sink, shower or bath pours it down the drain instead of onto the floor.
[HarmonyPatch(typeof(ItemActionDumpWater), "ExecuteAction")]
public static class HSPatchSinkDrain
{
    internal static readonly HashSet<ItemActionData> Draining = new HashSet<ItemActionData>();

    static bool IsDrain(Block b)
    {
        if (b == null || b is BlockHSWaterCooler || b is BlockHSLaundry || b is BlockHSToilet)
            return false;
        string n = b.GetBlockName();
        if (n == HSWater.WaterCoolerName || n == HSWater.WasherName || n == HSWater.WasherVanillaName)
            return false;
        return b is BlockHSSink || HSWater.IsSinkBlock(b);
    }

    static bool AimingAtDrain(EntityPlayerLocal p)
    {
        World world = GameManager.Instance.World;
        if (world == null || p.HitInfo == null)
            return false;
        if (p.HitInfo.bHitValid && IsDrain(p.HitInfo.hit.blockValue.Block))
            return true;
        return Voxel.Raycast(world, p.HitInfo.ray, Constants.cDigAndBuildDistance, -555266061, 4095, 0f)
            && IsDrain(Voxel.voxelRayHitInfo.hit.blockValue.Block);
    }

    static bool Prefix(ItemActionDumpWater __instance, ItemActionData _actionData, bool _bReleased)
    {
        try
        {
            if (!_bReleased || _actionData == null || _actionData.lastUseTime > 0f)
                return true;
            EntityPlayerLocal p = HSInvData.HoldingEntity(_actionData.invData) as EntityPlayerLocal;
            if (p == null || p.inventory == null)
                return true;
            ItemValue iv = p.inventory.holdingItemItemValue;
            if (iv == null || HSItem.Meta(iv) < 195 || !AimingAtDrain(p))
                return true;
            _actionData.lastUseTime = Time.time;
            p.RightArmAnimationUse = true;
            p.PlayOneShot(string.IsNullOrEmpty(__instance.soundStart) ? "bucketfill_water" : __instance.soundStart);
            Draining.Add(_actionData);
            return false;
        }
        catch
        {
            return true;
        }
    }
}

[HarmonyPatch(typeof(ItemActionDumpWater), "OnHoldingUpdate")]
public static class HSPatchSinkDrainDone
{
    static bool Prefix(ItemActionDumpWater __instance, ItemActionData _actionData)
    {
        if (_actionData == null || !HSPatchSinkDrain.Draining.Contains(_actionData))
            return true;
        if (_actionData.lastUseTime == 0f)
        {
            HSPatchSinkDrain.Draining.Remove(_actionData);
            return true;
        }
        if (__instance.IsActionRunning(_actionData))
            return false;
        _actionData.lastUseTime = 0f;
        HSPatchSinkDrain.Draining.Remove(_actionData);
        try
        {
            EntityPlayerLocal p = HSInvData.HoldingEntity(_actionData.invData) as EntityPlayerLocal;
            if (p == null || p.inventory == null)
                return false;
            string to = string.IsNullOrEmpty(__instance.changeItemToItem) ? "bucketEmpty" : __instance.changeItemToItem;
            p.inventory.SetItem(HSInv.HeldIndex(p), new ItemStack(ItemClass.GetItem(to), p.inventory.holdingCount));
            GameManager.ShowTooltip(p, HSLoc.Get("hsSinkDrained", "You pour the water down the drain"));
        }
        catch { }
        return false;
    }
}
