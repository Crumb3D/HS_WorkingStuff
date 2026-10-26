using UnityEngine;

// Powered water pump on the bank. Wire it like any other electrical consumer.
// While powered and next to water it feeds piped rain catches and tanks.
public class BlockHSPump : BlockPowered
{
    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        Vector3i pos = _blockValue.ischild ? HSWater.Parent(_blockPos, _blockValue) : _blockPos;
        World world = _world as World;
        string line;
        if (world == null || !HSWater.TouchesWater(world, pos))
            line = HSLoc.Get("hsPumpNoWater", "Place next to a lake, river or stream");
        else if (!HSWater.PumpPowered(world, pos))
            line = HSLoc.Get("hsPumpNoPower", "No power: wire it to a generator or battery bank");
        else
            line = HSLoc.Get("hsPumpRunning", "Pumping: pipe to a rain catch or tank");
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + line;
    }
}
