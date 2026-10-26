using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Self-powered water pump on the bank. Put small engines and petrol in like a generator bank.
// Sit on dry land next to water (not in it). More engines pump faster.
public class BlockHSPump : BlockGenerator
{
    readonly BlockActivationCommand[] pumpCmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("open", "hand", true),
        new BlockActivationCommand("light", "electric_switch", true),
        new BlockActivationCommand("take", "hand", true)
    };

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
    }

    public override string GetPowerSourceIcon()
    {
        return "electric_generator";
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        if (!_world.CanPlaceBlockAt(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer()))
            return "";
        Vector3i pos = _blockValue.ischild ? HSWater.Parent(_blockPos, _blockValue) : _blockPos;
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSWater.PumpPrompt(_world as World, pos);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return _world.GetTileEntity(_blockPos) is TileEntityPowerSource;
    }

    // Take works without a land claim (POI bank / job site).
    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        if (!(_world.GetTileEntity(_blockPos) is TileEntityPowerSource))
            return BlockActivationCommand.Empty;
        bool can = _world.CanPlaceBlockAt(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        float take = 8f;
        Properties.ParseFloat("TakeDelay", ref take);
        pumpCmds[0].icon = GetPowerSourceIcon();
        pumpCmds[0].enabled = can;
        pumpCmds[1].enabled = can;
        pumpCmds[2].enabled = take > 0f;
        return pumpCmds;
    }

    // Same as a generator bank, but adjacent lake/river water must not count as "underwater".
    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_blockValue.ischild)
        {
            Vector3i parent = _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue);
            return OnBlockActivated(_commandName, _world, parent, _world.GetBlock(parent), _player);
        }
        if (!(_world.GetTileEntity(_blockPos) is TileEntityPowerSource te))
            return false;
        if (_commandName == "light")
        {
            if (te.MaxOutput == 0)
            {
                Audio.Manager.PlayInsidePlayerHead("ui_denied");
                GameManager.ShowTooltip(_player, Localization.Get("ttRequiresOneComponent"));
                return false;
            }
            if (te.PowerItemType == PowerItem.PowerItemTypes.Generator && te.CurrentFuel == 0)
            {
                Audio.Manager.PlayInsidePlayerHead("ui_denied");
                GameManager.ShowTooltip(_player, Localization.Get("ttGeneratorRequiresFuel"));
                return false;
            }
            bool on = (_blockValue.meta & 2) != 0;
            // Do NOT refuse turn-on for side water — that is the intake. Only block if flooded on top.
            if (!on && _world.IsWater(_blockPos.x, _blockPos.y + 1, _blockPos.z))
            {
                Audio.Manager.PlayInsidePlayerHead("ui_denied");
                GameManager.ShowTooltip(_player, Localization.Get("ttPowerSourceUnderwater"));
                return false;
            }
            _blockValue.meta = (byte)((_blockValue.meta & ~2) | (on ? 0 : 2));
            _world.SetBlockRPC(_blockPos, _blockValue);
            return true;
        }
        return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
    }
}

static class HSPumpUi
{
    public static bool IsPump(TileEntityPowerSource te)
    {
        if (te == null)
            return false;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return false;
        Block b = world.GetBlock(te.ToWorldPos()).Block;
        return b != null && b.GetBlockName() == HSWater.PumpName;
    }

    public static void RefreshHeader(XUiC_PowerSourceWindowGroup group)
    {
        if (group == null || group.TileEntity == null || !IsPump(group.TileEntity))
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        Vector3i pos = group.TileEntity.ToWorldPos();
        XUiC_WindowNonPagingHeader header = group.GetChildByType<XUiC_WindowNonPagingHeader>();
        if (header == null)
            return;
        header.SetHeader(HSLoc.Get("hsWaterPump", "Water Pump") + " — " + HSWater.PumpPrompt(world, pos).Replace("\n", " · "));
    }
}

[HarmonyPatch(typeof(XUiC_PowerSourceWindowGroup), "OnOpen")]
static class HSPatchPumpPowerSourceOpen
{
    static void Postfix(XUiC_PowerSourceWindowGroup __instance)
    {
        HSPumpUi.RefreshHeader(__instance);
        if (__instance == null || __instance.TileEntity == null || !HSPumpUi.IsPump(__instance.TileEntity))
            return;
        EntityPlayerLocal player = __instance.xui != null && __instance.xui.playerUI != null
            ? __instance.xui.playerUI.entityPlayer : null;
        if (player == null)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        GameManager.ShowTooltip(player, HSWater.PumpPrompt(world, __instance.TileEntity.ToWorldPos()));
    }
}

[HarmonyPatch(typeof(XUiC_PowerSourceWindowGroup), "Update")]
static class HSPatchPumpPowerSourceUpdate
{
    static float next;

    static void Postfix(XUiC_PowerSourceWindowGroup __instance)
    {
        if (__instance == null || !__instance.IsOpen || __instance.TileEntity == null || !HSPumpUi.IsPump(__instance.TileEntity))
            return;
        if (Time.time < next)
            return;
        next = Time.time + 1f;
        HSPumpUi.RefreshHeader(__instance);
    }
}

// Stats panel: Water / Pipes rows instead of Max Output / Power titles while this is a pump.
[HarmonyPatch(typeof(XUiC_PowerSourceStats), "GetBindingValueInternal")]
static class HSPatchPumpStatsBindings
{
    static void Postfix(XUiC_PowerSourceStats __instance, ref bool __result, ref string value, string bindingName)
    {
        if (!__result || __instance == null || __instance.TileEntity == null || !HSPumpUi.IsPump(__instance.TileEntity))
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        Vector3i pos = __instance.TileEntity.ToWorldPos();
        bool water = world != null && HSWater.TouchesWater(world, pos);
        var catches = new List<Vector3i>();
        var tanks = new List<Vector3i>();
        if (world != null)
            HSWater.ReachFromIntake(world, pos, catches, tanks);
        switch (bindingName)
        {
            case "maxoutputtitle":
                value = HSLoc.Get("hsPumpStatWater", "Water");
                break;
            case "maxoutput":
                value = water
                    ? HSLoc.Get("hsPumpStatWaterYes", "Yes")
                    : HSLoc.Get("hsPumpStatWaterNo", "No — bank next to water");
                break;
            case "powertitle":
                value = HSLoc.Get("hsPumpStatPipes", "Pipes");
                break;
            case "power":
                if (catches.Count > 0 && tanks.Count > 0)
                    value = HSLoc.Get("hsPumpStatPipedBoth", "Catch + tank");
                else if (catches.Count > 0)
                    value = HSLoc.Get("hsPumpStatPipedCatch", "Rain catch");
                else if (tanks.Count > 0)
                    value = HSLoc.Get("hsPumpStatPipedTank", "Tank");
                else
                    value = HSLoc.Get("hsPumpStatPipedNone", "None");
                break;
            case "powerfill":
                value = water ? "1" : "0";
                break;
        }
    }
}

// While the pump is on it loads itself by MaxOutput (engines), so fuel burns like a genny under load.
[HarmonyPatch(typeof(PowerSource), "HandleSendPower")]
static class HSPatchPumpFuelDraw
{
    static void Postfix(PowerSource __instance)
    {
        if (__instance == null || !__instance.IsOn || __instance.MaxOutput == 0)
            return;
        if (!(__instance is PowerGenerator))
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || __instance.TileEntity == null)
            return;
        Block b = world.GetBlock(__instance.TileEntity.ToWorldPos()).Block;
        if (b == null || b.GetBlockName() != HSWater.PumpName)
            return;
        ushort draw = __instance.MaxOutput;
        if (__instance.CurrentPower > draw)
            __instance.CurrentPower -= draw;
        else
            __instance.CurrentPower = 0;
    }
}

// UI Turn On button also uses the underwater check — skip side water for the pump.
[HarmonyPatch(typeof(XUiC_PowerSourceStats), "btnOn_OnPress")]
static class HSPatchPumpTurnOn
{
    static bool Prefix(XUiC_PowerSourceStats __instance, XUiController _sender)
    {
        if (__instance == null || __instance.TileEntity == null || !HSPumpUi.IsPump(__instance.TileEntity))
            return true;
        TileEntityPowerSource te = __instance.TileEntity;
        BlockValue block = te.GetChunk().GetBlock(te.localChunkPos);
        if (te.MaxOutput == 0)
        {
            Audio.Manager.PlayInsidePlayerHead("ui_denied");
            GameManager.ShowTooltip(__instance.xui.playerUI.localPlayer.entityPlayerLocal, Localization.Get("ttRequiresOneComponent"));
            return false;
        }
        bool on = (block.meta & 2) != 0;
        if (te.PowerItemType == PowerItem.PowerItemTypes.Generator && !on && te.CurrentFuel == 0)
        {
            Audio.Manager.PlayInsidePlayerHead("ui_denied");
            GameManager.ShowTooltip(__instance.xui.playerUI.localPlayer.entityPlayerLocal, Localization.Get("ttGeneratorRequiresFuel"));
            return false;
        }
        Vector3i pos = te.ToWorldPos();
        World world = __instance.xui.playerUI.entityPlayer.world as World;
        if (!on && world != null && world.IsWater(pos.x, pos.y + 1, pos.z))
        {
            Audio.Manager.PlayInsidePlayerHead("ui_denied");
            GameManager.ShowTooltip(__instance.xui.playerUI.entityPlayer, Localization.Get("ttPowerSourceUnderwater"));
            return false;
        }
        on = !on;
        block.meta = (byte)((block.meta & ~2) | (on ? 2 : 0));
        GameManager.Instance.World.SetBlockRPC(pos, block);
        AccessTools.Method(typeof(XUiC_PowerSourceStats), "RefreshIsOn").Invoke(__instance, new object[] { on });
        if (__instance.Owner != null)
            __instance.Owner.SetOn(on);
        return false;
    }
}
