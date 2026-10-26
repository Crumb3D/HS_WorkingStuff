using System.Runtime.CompilerServices;
using UnityEngine;

public class BlockHSFridge : BlockPowered
{
    BlockActivationCommand[] cmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("open", "hand", true),
        new BlockActivationCommand("hsopenfreezer", "cold", false),
        new BlockActivationCommand("storefood", "store_all_down", true),
        new BlockActivationCommand("takespoiled", "store_all_up", true),
        new BlockActivationCommand("take", "hand", false)
    };

    public BlockHSFridge()
    {
        HasTileEntity = true;
    }

    public override TileEntityPowered CreateTileEntity(Chunk chunk)
    {
        if (HSGameVersion.Is33)
            return CreateTe33(chunk);
        return CreateTe32(chunk);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static TileEntityPowered CreateTe32(Chunk chunk)
    {
        var te = new TileEntityHSFridge(chunk);
        te.PowerItemType = PowerItem.PowerItemTypes.Consumer;
        return te;
    }

    static TileEntityPowered CreateTe33(Chunk chunk)
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
        if (HSGameVersion.Is33)
        {
            HSPickup.RestoreFridge(HSFridgeInv.Of(EnsureTe33(world, _blockPos, _chunk, _addedByPlayer != null)), _addedByPlayer);
            return;
        }
        OnBlockAdded32(world, _chunk, _blockPos, _addedByPlayer);
    }

    // BlockPowered only gets a TE when the wire tool touches it; an unwired fridge needs one to hold food.
    static TileEntity EnsureTe33(WorldBase world, Vector3i pos, Chunk chunk, bool playerPlaced)
    {
        TileEntity existing = world.GetTileEntity(pos);
        if (existing != null)
            return existing;
        if (chunk == null)
            chunk = world.GetChunkFromWorldPos(pos) as Chunk;
        if (chunk == null)
            return null;
        TileEntityPowered te = CreateTe33(chunk);
        te.localChunkPos = World.toBlock(pos);
        BlockEntityData ebd = chunk.GetBlockEntity(pos);
        if (ebd != null)
            te.BlockTransform = ebd.transform;
        te.InitializePowerData();
        if (playerPlaced)
            te.IsPlayerPlaced = true;
        chunk.AddTileEntity(te);
        return te;
    }

    protected static IHSFridgeInv InvAt(WorldBase world, Vector3i pos)
    {
        if (HSGameVersion.Is33)
            return HSFridgeInv.Of(EnsureTe33(world, pos, null, false));
        return HSFridgeInv.Of(world.GetTileEntity(pos));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void OnBlockAdded32(WorldBase world, Chunk _chunk, Vector3i _blockPos, PlatformUserIdentifierAbs _addedByPlayer)
    {
        if (world.GetTileEntity(_blockPos) is TileEntityHSFridge)
            return;
        TileEntityPowered te = CreateTe32(_chunk);
        te.localChunkPos = World.toBlock(_blockPos);
        te.InitializePowerData();
        if (_addedByPlayer != null)
            te.IsPlayerPlaced = true;
        _chunk.AddTileEntity(te);
        HSPickup.RestoreFridge(te as TileEntityHSFridge, _addedByPlayer);
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        _blockPos = HSWater.Parent(_blockPos, _blockValue);
        IHSFridgeInv te = HSFridgeInv.Of(_world.GetTileEntity(_blockPos));
        bool powered = te != null && te.IsPowered;
        string power;
        if (IsFreezer(_blockValue))
            power = powered
                ? HSLoc.Get("hsFreezerPowered", "Powered: food lasts about 90 days and freezes")
                : HSLoc.Get("hsFridgeUnpowered", "Unpowered: spoils at normal speed");
        else if (HSFrozen.HasCompartment(_blockValue.Block))
            power = powered
                ? HSLoc.Get("hsFridgeFreezerPowered", "Powered: fridge about 7 days, freezer about 30 days and it freezes")
                : HSLoc.Get("hsFridgeUnpowered", "Unpowered: spoils at normal speed");
        else
            power = powered
                ? HSLoc.Get("hsFridgePowered", "Powered: food lasts about 7 days")
                : HSLoc.Get("hsFridgeUnpowered", "Unpowered: spoils at normal speed");
        string oldest = te != null ? te.OldestLabel() : "";
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + Temps(_world, _blockPos, _blockValue, powered) + "\n" + power + "\n" + oldest;
    }

    // Unpowered, every compartment sits at room temperature.
    static string Temps(WorldBase world, Vector3i pos, BlockValue bv, bool powered)
    {
        float room = 70f;
        World w = world as World;
        if (!powered && w != null)
        {
            try { room = HSRoomTemp.Room(w, pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f)); }
            catch { }
        }
        if (IsFreezer(bv))
            return HSLoc.Format("hsFreezerTemp", "Freezer: {0}", HSRoomTemp.Format(powered ? HSWorkingStuffSettings.FreezerTemp : room));
        string fridge = HSLoc.Format("hsFridgeTemp", "Fridge: {0}", HSRoomTemp.Format(powered ? HSWorkingStuffSettings.FridgeTemp : room));
        if (!HSFrozen.HasCompartment(bv.Block))
            return fridge;
        return fridge + "    " + HSLoc.Format("hsFreezerTemp", "Freezer: {0}", HSRoomTemp.Format(powered ? HSWorkingStuffSettings.FridgeFreezerTemp : room));
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        if (HSModelHit.IsBundleModel(this))
            HSModelHit.Wire(_ebcd);
    }

    static bool IsFreezer(BlockValue bv)
    {
        return bv.Block != null && bv.Block.GetBlockName() == "hsFreezer";
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        _blockPos = HSWater.Parent(_blockPos, _blockValue);
        bool land = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        var te = _world.GetTileEntity(_blockPos) as TileEntityPowered;
        cmds[1].enabled = HSFrozen.HasCompartment(this);
        cmds[4].enabled = land && te != null && te.IsPlayerPlaced && TakeDelay > 0f;
        return cmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_blockValue.ischild)
        {
            Vector3i parent = _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue);
            return OnBlockActivated(_commandName, _world, parent, _world.GetBlock(parent), _player);
        }
        IHSFridgeInv te = InvAt(_world, _blockPos);
        if (te == null)
            return false;
        if (_commandName == "open")
        {
            string title = IsFreezer(_blockValue) ? HSLoc.Get("hsFreezerWindow", "Freezer") : null;
            XUiC_HSStorageWindowGroup.Open(_player, te, title, 0);
            return true;
        }
        if (_commandName == "hsopenfreezer")
        {
            XUiC_HSStorageWindowGroup.Open(_player, te, HSLoc.Get("hsFreezerWindow", "Freezer"), HSFrozen.CompartmentFrom);
            return true;
        }
        if (_commandName == "storefood")
        {
            te.EnsureItems();
            ItemStack[] fridge = HSFridgeInv.Slice(te.Items, 0, TileEntityHSFridge.ViewSlots);
            bool stored = HSSpoil.StorePerishables(_player, fridge);
            HSFridgeInv.Unslice(te.Items, fridge, 0);
            if (stored)
                te.SetModified();
            return true;
        }
        if (_commandName == "takespoiled")
        {
            te.EnsureItems();
            if (HSSpoil.TakeSpoiled(_player, te.Items))
                te.SetModified();
            return true;
        }
        if (_commandName == "take")
        {
            HSPickup.CaptureFridge(te);
            takeItemWithTimer(_blockPos, _blockValue, _player, TakeDelay);
            return true;
        }
        return false;
    }
}
