using UnityEngine;

public class BlockHSStove : BlockWorkstation
{
    int watts = 10;

    public override void Init()
    {
        base.Init();
        watts = 10;
        Properties.ParseInt("RequiredPower", ref watts);
        if (watts < 1)
            watts = 10;
    }

    public string WattLabel()
    {
        return watts.ToString() + "W";
    }

    public override void OnBlockAdded(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(world, _chunk, _blockPos, _blockValue, _addedByPlayer);
        if (_blockValue.ischild)
            return;
        HSStovePower.Attach(world, _chunk, _blockPos);
        OwnWorkstation(world, _blockPos);
        HSShare.Claim(world, _blockPos);
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
        if (_blockValue.ischild)
            return;
        Chunk chunk = _world != null ? _world.GetChunkFromWorldPos(_blockPos) as Chunk : null;
        RepairWorkstation(_world, chunk, _blockPos);
        HSStovePower.Attach(_world, chunk, _blockPos);
        OwnWorkstation(_world, _blockPos);
    }

    // A repaired POI coffee maker, toaster or cooker is the player's. Pick up then shows on a land claim.
    static void OwnWorkstation(WorldBase world, Vector3i pos)
    {
        if (!HSWorkingStuffNet.IsAuthority || world == null)
            return;
        TileEntityWorkstation te = world.GetTileEntity(pos) as TileEntityWorkstation;
        if (te != null && !te.IsPlayerPlaced)
            te.IsPlayerPlaced = true;
    }

    // Vanilla GetBlockActivationCommands reads the workstation tile entity without a null check (NRE in the hold-E radial).
    static void RepairWorkstation(WorldBase world, Chunk chunk, Vector3i pos)
    {
        if (world == null || chunk == null || !HSWorkingStuffNet.IsAuthority || world.GetTileEntity(pos) != null)
            return;
        try
        {
            var te = new TileEntityWorkstation(chunk);
            te.localChunkPos = World.toBlock(pos);
            te.IsPlayerPlaced = true;
            chunk.AddTileEntity(te);
            te.SetModified();
            Log.Out("[HSWorkingStuff] restored missing cooker data at " + pos);
        }
        catch (System.Exception e)
        {
            Log.Warning("[HSWorkingStuff] cooker repair at " + pos + ": " + e.Message);
        }
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        if (!(_world.GetTileEntity(_blockPos) is TileEntityWorkstation))
        {
            RepairWorkstation(_world, _world.GetChunkFromWorldPos(_blockPos) as Chunk, _blockPos);
            if (!(_world.GetTileEntity(_blockPos) is TileEntityWorkstation))
            {
                cmds[1].enabled = false;
                cmds[2].enabled = false;
                return cmds;
            }
        }
        BlockActivationCommand[] got = base.GetBlockActivationCommands(_world, _blockValue, _blockPos, _entityFocusing);
        bool land = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        TileEntityWorkstation owned = _world.GetTileEntity(_blockPos) as TileEntityWorkstation;
        if (land && owned != null && TakeDelay > 0f && got != null && got.Length > 1)
        {
            if (!owned.IsPlayerPlaced)
                OwnWorkstation(_world, _blockPos);
            got[1].enabled = true;
        }
        return got;
    }

    public override void OnBlockRemoved(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        if (!_blockValue.ischild)
        {
            HSShare.QueueRestore(_blockPos);
            HSStovePower.Detach(world, _blockPos);
        }
        base.OnBlockRemoved(world, _chunk, _blockPos, _blockValue);
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        if (HSModelHit.IsBundleModel(this))
            HSModelHit.Wire(_ebcd);
        if (_blockValue.ischild)
            return;
        Chunk chunk = _world != null ? _world.GetChunkFromWorldPos(_blockPos) as Chunk : null;
        TileEntityPoweredBlock side = HSStovePower.Ensure(chunk, _blockPos);
        if (side != null && _ebcd != null)
            side.BlockTransform = _ebcd.transform;
        if (side != null)
            side.DrawWires();
        if (HSShare.Small(this) && _ebcd != null)
            HSShare.Attach(_ebcd.transform, _blockPos);
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string power = HSStovePower.IsPowered(_blockPos)
            ? HSLoc.Get("hsStovePowered", "Powered: cooking like a campfire")
            : HSLoc.Get("hsStoveUnpowered", "No power: wire this cooker to cook");
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + power;
    }
}
