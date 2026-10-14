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
            case "drink": kind = HSWorkingStuffNet.SinkDrink; break;
            case "filljarhot":
            case "fillbuckethot": kind = HSWorkingStuffNet.SinkFillHot; break;
            case "fillallhot": kind = HSWorkingStuffNet.SinkFillAllHot; break;
            case "drinkhot": kind = HSWorkingStuffNet.SinkDrinkHot; break;
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

    // Same arm swing and sounds as filling a jar or bucket at a lake, or drinking a jar.
    static void Animate(EntityPlayerLocal player, byte kind)
    {
        if (player == null)
            return;
        try
        {
            if (kind == HSWorkingStuffNet.SinkDrink || kind == HSWorkingStuffNet.SinkDrinkHot)
            {
                player.PlayOneShot("player_drinking");
                return;
            }
            if (kind == HSWorkingStuffNet.SinkFill || kind == HSWorkingStuffNet.SinkFillHot || kind == HSWorkingStuffNet.SinkWash)
                player.RightArmAnimationUse = true;
            player.PlayOneShot("bucketfill_water");
        }
        catch { }
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
            if (b == null || !(b is BlockHSSink || b is BlockHSRainCatch || HSWater.IsSinkBlock(b)))
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
                if (idx < 0 || idx >= cmds.Length || !HSSinkCmds.IsOff(cmds[idx].text))
                    continue;
                items[i].SetSprite("ui_game_symbol_" + cmds[idx].icon, grey);
            }
        }
        catch { }
    }
}
