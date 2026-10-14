// Plumbed water cooler: cold tap only, with its own filter so tank water comes out clean.
public class BlockHSWaterCooler : BlockHSSink
{
    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string name = _blockValue.Block.GetLocalizedBlockName();
        World world = _world as World;
        if (world == null)
            return name;
        HSWater.Taps taps = HSWater.FindTaps(world, _blockPos);
        if (!taps.Cold.Ok)
            return name + "\n" + HSWater.ColdReason(taps);
        return name + "\n" + HSWater.TapLine(taps.Cold) + "\n" + HSLoc.Get("hsCoolerFiltered", "Built-in filter: comes out clean");
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return HSSinkCmds.BuildCold(_world, _blockPos, _entityFocusing);
    }
}
