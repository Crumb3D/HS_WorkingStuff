// The combi boiler. Kept as hsWaterHeater / BlockHSHeater so placed heaters in old saves carry over.
public class BlockHSHeater : BlockPowered
{
    BlockActivationCommand[] boilerCmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("heatingon", "electric_switch", false),
        new BlockActivationCommand("heatingoff", "electric_switch", false),
        new BlockActivationCommand("take", "hand", false)
    };

    public BlockHSHeater()
    {
        HasTileEntity = true;
    }

    public override TileEntityPowered CreateTileEntity(Chunk chunk)
    {
        var te = new TileEntityPoweredBlock(chunk);
        te.PowerItemType = PowerItem.PowerItemTypes.Consumer;
        return te;
    }

    public override void OnBlockAdded(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(world, _chunk, _blockPos, _blockValue, _addedByPlayer);
        if (_blockValue.ischild)
            return;
        HSBoiler.Register(_blockPos);
        if (world.GetTileEntity(_blockPos) is TileEntityPowered)
            return;
        TileEntityPowered te = CreateTileEntity(_chunk);
        te.localChunkPos = World.toBlock(_blockPos);
        te.InitializePowerData();
        if (_addedByPlayer != null)
            te.IsPlayerPlaced = true;
        _chunk.AddTileEntity(te);
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
        if (!_blockValue.ischild)
            HSBoiler.Register(_blockPos);
    }

    public override void OnBlockRemoved(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        if (!_blockValue.ischild)
            HSBoiler.Removed(_blockPos);
        base.OnBlockRemoved(world, _chunk, _blockPos, _blockValue);
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSBoiler.BoilerPrompt(_world, _blockPos);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        bool land = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        var te = _world.GetTileEntity(_blockPos) as TileEntityPowered;
        bool heatingOn = HSBoiler.Known(_blockPos).Heating;
        bool plumbing = HSWorkingStuffSettings.EnablePlumbing;
        boilerCmds[0].enabled = plumbing && !heatingOn;
        boilerCmds[1].enabled = plumbing && heatingOn;
        boilerCmds[2].enabled = land && te != null && te.IsPlayerPlaced && TakeDelay > 0f;
        return boilerCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_commandName == "take")
        {
            takeItemWithTimer(_blockPos, _blockValue, _player, TakeDelay);
            return true;
        }
        if (_commandName == "heatingon" || _commandName == "heatingoff")
        {
            bool on = _commandName == "heatingon";
            HSBoiler.Info info = HSBoiler.Known(_blockPos);
            info.Heating = on;
            HSBoiler.Remember(_blockPos, info);
            HSWorkingStuffNet.SendWater(HSWorkingStuffNet.BoilerHeating, _blockPos, _player, on ? 1 : 0);
            return true;
        }
        return false;
    }
}
