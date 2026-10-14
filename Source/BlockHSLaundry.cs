using System;
using UnityEngine;

// Washing machine (plumbed cold water + power) and tumble dryer (power). Same storage as the fridge.
public class BlockHSLaundry : BlockHSFridge
{
    readonly BlockActivationCommand[] lcmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("open", "hand", true),
        new BlockActivationCommand("hswash", "water", true),
        new BlockActivationCommand("take", "hand", false)
    };

    public bool IsDryer
    {
        get { return GetBlockName().IndexOf("Dryer", StringComparison.OrdinalIgnoreCase) >= 0; }
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        _blockPos = HSWater.Parent(_blockPos, _blockValue);
        World world = _world as World;
        IHSFridgeInv te = HSFridgeInv.Of(_world.GetTileEntity(_blockPos));
        bool powered = te != null && te.IsPowered;
        string text = _blockValue.Block.GetLocalizedBlockName() + "\n"
            + (powered ? HSLoc.Get("hsLaundryPowered", "Powered") : HSLoc.Get("hsLaundryNoPower", "No power: wire it up first"));
        if (!IsDryer && world != null && HSWorkingStuffSettings.EnablePlumbing)
        {
            HSWater.Taps taps = HSWater.FindTaps(world, _blockPos);
            if (!taps.Cold.Ok)
                text += "\n" + HSWater.ColdReason(taps);
        }
        if (te != null)
        {
            int n = HSLaundry.Count(te.Items, IsDryer, HSLaundry.NowHours(world));
            text += "\n" + (IsDryer
                ? HSLoc.Format("hsDryerHolds", "{0} wet items inside", n)
                : HSLoc.Format("hsWasherHolds", "{0} clothes or armour inside", n));
        }
        return text;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        _blockPos = HSWater.Parent(_blockPos, _blockValue);
        bool land = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        var te = _world.GetTileEntity(_blockPos) as TileEntityPowered;
        string run = IsDryer ? "hsdry" : "hswash";
        if (!IsDryer && HSWorkingStuffSettings.EnablePlumbing)
        {
            World world = _world as World;
            HSWater.Taps taps = world != null ? HSWater.FindTaps(world, _blockPos) : new HSWater.Taps();
            if (!taps.Cold.Ok)
                run = HSWater.ColdReasonKey(taps);
        }
        lcmds[1] = new BlockActivationCommand(run, IsDryer ? "fire" : "water", true);
        lcmds[2].enabled = land && te != null && te.IsPlayerPlaced && TakeDelay > 0f;
        return lcmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_blockValue.ischild)
        {
            Vector3i parent = _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue);
            return OnBlockActivated(_commandName, _world, parent, _world.GetBlock(parent), _player);
        }
        if (_commandName == "hswash" || _commandName == "hsdry")
        {
            HSWorkingStuffNet.SendWater(HSWorkingStuffNet.Laundry, _blockPos, _player);
            try
            {
                if (_player != null)
                    _player.PlayOneShot(IsDryer ? HSMicrowaveSound.Hum : "bucketfill_water");
            }
            catch { }
            return true;
        }
        if (HSSinkCmds.IsOff(_commandName))
        {
            if (_player != null)
                GameManager.ShowTooltip(_player, Localization.Get("blockcommand_" + _commandName));
            return true;
        }
        if (_commandName == "open")
        {
            IHSFridgeInv te = InvAt(_world, _blockPos);
            if (te == null)
                return false;
            XUiC_HSStorageWindowGroup.Open(_player, te, _blockValue.Block.GetLocalizedBlockName());
            return true;
        }
        return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
    }
}
