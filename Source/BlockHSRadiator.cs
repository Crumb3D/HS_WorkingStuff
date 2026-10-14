// A radiator warms players nearby while a combi boiler piped to it has central heating on and power.
public class BlockHSRadiator : Block
{
    public float TakeDelay = 3f;

    BlockActivationCommand[] radiatorCmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("valveup", "hot", true),
        new BlockActivationCommand("valvedown", "cold", true),
        new BlockActivationCommand("take", "hand", false)
    };

    public override void Init()
    {
        base.Init();
        if (Properties.Values.ContainsKey("TakeDelay"))
            TakeDelay = StringParsers.ParseFloat(Properties.Values["TakeDelay"]);
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSBoiler.RadiatorPrompt(_world, _blockPos);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        bool land = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        radiatorCmds[2].enabled = land && TakeDelay > 0f;
        return radiatorCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_commandName == "valveup" || _commandName == "valvedown")
        {
            BlockValue bv = _world.GetBlock(_blockPos);
            int level = HSRadiatorValve.Level(bv);
            int next = level + (_commandName == "valveup" ? 1 : -1);
            if (next > HSRadiatorValve.Max)
            {
                GameManager.ShowTooltip(_player, HSLoc.Get("hsRadiatorValveMax", "The valve is fully open"));
                return true;
            }
            if (next < 0)
            {
                GameManager.ShowTooltip(_player, HSLoc.Get("hsRadiatorValveMin", "The valve is already off"));
                return true;
            }
            _world.SetBlockRPC(_blockPos, HSRadiatorValve.WithLevel(bv, next));
            GameManager.ShowTooltip(_player, HSRadiatorValve.ValveText(next));
            return true;
        }
        if (_commandName == "take")
        {
            takeItemWithTimer(_blockPos, _blockValue, _player, TakeDelay);
            return true;
        }
        return false;
    }
}
