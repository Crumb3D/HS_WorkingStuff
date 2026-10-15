// Shower and bath: hot water from a combi boiler washes all the smell off for a while.
public class BlockHSShower : BlockHSSink
{
    public float TakeDelay = 5f;

    public bool IsBath
    {
        get { return GetBlockName() == HSWater.BathName; }
    }

    public override void Init()
    {
        base.Init();
        if (Properties.Values.ContainsKey("TakeDelay"))
            TakeDelay = StringParsers.ParseFloat(Properties.Values["TakeDelay"]);
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
        bool land = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        var wash = taps.Hot.Ok
            ? new BlockActivationCommand("wash", "water", true)
            : new BlockActivationCommand(HSWater.HotReasonKey(taps), "fire", true);
        return new[]
        {
            wash,
            new BlockActivationCommand("take", "hand", land && TakeDelay > 0f)
        };
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_commandName == "take")
        {
            takeItemWithTimer(_blockPos, _blockValue, _player, TakeDelay > 0f ? TakeDelay : 5f);
            return true;
        }
        return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
    }
}
