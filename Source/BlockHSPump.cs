using HarmonyLib;
using UnityEngine;

// Self-powered water pump on the bank. Put small engines and petrol in like a generator bank.
// While it is turned on and next to water it feeds piped rain catches and tanks.
public class BlockHSPump : BlockGenerator
{
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
}

// While the generator window is open for a water pump, the header shows water and pipe state.
[HarmonyPatch(typeof(XUiC_PowerSourceWindowGroup), "OnOpen")]
static class HSPatchPumpPowerSourceOpen
{
    static void Postfix(XUiC_PowerSourceWindowGroup __instance)
    {
        TileEntityPowerSource te = __instance.TileEntity;
        if (te == null)
            return;
        Vector3i pos = te.ToWorldPos();
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        Block b = world.GetBlock(pos).Block;
        if (b == null || b.GetBlockName() != HSWater.PumpName)
            return;
        XUiC_WindowNonPagingHeader header = __instance.GetChildByType<XUiC_WindowNonPagingHeader>();
        if (header == null)
            return;
        string title = HSLoc.Get("hsWaterPump", "Water Pump");
        string status = HSWater.PumpPrompt(world, pos).Replace("\n", " · ");
        header.SetHeader(title + " — " + status);
    }
}

[HarmonyPatch(typeof(XUiC_PowerSourceWindowGroup), "Update")]
static class HSPatchPumpPowerSourceUpdate
{
    static float next;

    static void Postfix(XUiC_PowerSourceWindowGroup __instance, float _dt)
    {
        if (__instance == null || __instance.TileEntity == null || !__instance.IsOpen)
            return;
        if (Time.time < next)
            return;
        next = Time.time + 1f;
        TileEntityPowerSource te = __instance.TileEntity;
        Vector3i pos = te.ToWorldPos();
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        Block b = world.GetBlock(pos).Block;
        if (b == null || b.GetBlockName() != HSWater.PumpName)
            return;
        XUiC_WindowNonPagingHeader header = __instance.GetChildByType<XUiC_WindowNonPagingHeader>();
        if (header == null)
            return;
        string title = HSLoc.Get("hsWaterPump", "Water Pump");
        string status = HSWater.PumpPrompt(world, pos).Replace("\n", " · ");
        header.SetHeader(title + " — " + status);
    }
}
