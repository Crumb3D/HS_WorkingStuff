// Shower and bath: hot water from a combi boiler washes all the smell off for a while.
public class BlockHSShower : BlockHSSink
{
    public bool IsBath
    {
        get { return GetBlockName() == HSWater.BathName; }
    }

    public int Jars
    {
        get { return IsBath ? HSWorkingStuffSettings.BathJars : HSWorkingStuffSettings.ShowerJars; }
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string name = _blockValue.Block.GetLocalizedBlockName();
        World world = _world as World;
        if (world == null)
            return name;
        HSWater.Taps taps = HSWater.FindTaps(world, _blockPos);
        if (!taps.Hot.Ok)
            return name + "\n" + HSWater.HotReason(taps);
        return name + "\n" + HSWater.TapLine(taps.Hot) + "\n"
            + HSLoc.Format("hsWashUses", "A wash uses {0} jars of hot water", Jars);
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        World w = _world as World;
        HSWater.Taps taps = w != null ? HSWater.FindTaps(w, _blockPos) : new HSWater.Taps();
        if (taps.Hot.Ok)
            return new[] { new BlockActivationCommand("wash", "water", true) };
        return new[] { new BlockActivationCommand(HSWater.HotReasonKey(taps), "fire", true) };
    }
}
