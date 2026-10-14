using UnityEngine;

public class BlockHSMicrowave : BlockHSStove
{
    public override void OnBlockRemoved(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        if (!_blockValue.ischild)
            HSMicrowaveSound.Removed(_blockPos);
        base.OnBlockRemoved(world, _chunk, _blockPos, _blockValue);
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        BlockActivationCommand[] baseCmds = base.GetBlockActivationCommands(_world, _blockValue, _blockPos, _entityFocusing);
        if (!HSWorkingStuffSettings.EnableFreezing || baseCmds == null)
            return baseCmds;
        var all = new BlockActivationCommand[baseCmds.Length + 1];
        System.Array.Copy(baseCmds, all, baseCmds.Length);
        all[baseCmds.Length] = new BlockActivationCommand("hsdefrost", "campfire", HSStovePower.IsPowered(_blockPos));
        return all;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_commandName != "hsdefrost")
            return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
        if (!HSStovePower.IsPowered(_blockPos))
        {
            GameManager.ShowTooltip(_player, HSLoc.Get("hsMicrowaveUnpowered", "No power: wire this microwave to cook"));
            return true;
        }
        int n = HSFrozen.ThawPlayer(_player);
        if (n == 0)
        {
            GameManager.ShowTooltip(_player, HSLoc.Get("hsDefrostNone", "Nothing frozen on you"));
            return true;
        }
        _player.PlayOneShot(HSMicrowaveSound.Ding);
        GameManager.ShowTooltip(_player, HSLoc.Format("hsDefrosted", "Defrosted {0} frozen food", n));
        return true;
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string name = _blockValue.Block.GetLocalizedBlockName();
        bool powered = HSStovePower.IsPowered(_blockPos);
        TileEntityWorkstation te = _world.GetTileEntity(_blockPos) as TileEntityWorkstation;
        string cooking;
        float left = SecondsLeft(te, out cooking);
        string status;
        if (left <= 0f)
        {
            if (te != null && !te.OutputEmpty())
                status = HSLoc.Get("hsMicrowaveDone", "Done: your food is ready");
            else if (powered)
                status = HSLoc.Get("hsMicrowavePowered", "Powered: ready to cook");
            else
                status = HSLoc.Get("hsMicrowaveUnpowered", "No power: wire this microwave to cook");
        }
        else if (!powered)
            status = HSLoc.Format("hsMicrowaveNoPowerPaused", "No power: paused, {0} left", Clock(left));
        else if (te.IsUserAccessing())
            status = HSLoc.Format("hsMicrowaveDoorOpen", "Door open: paused, {0} left", Clock(left));
        else
            status = HSLoc.Format("hsMicrowaveCooking", "Cooking {0}: {1} left", cooking, Clock(left));
        return name + "\n" + status;
    }

    internal static float SecondsLeft(TileEntityWorkstation te, out string cooking)
    {
        cooking = "";
        RecipeQueueItem[] queue = te != null ? te.Queue : null;
        if (queue == null)
            return 0f;
        float total = 0f;
        for (int i = queue.Length - 1; i >= 0; i--)
        {
            RecipeQueueItem item = queue[i];
            if (item == null || item.Recipe == null || item.Multiplier <= 0)
                continue;
            float one = item.OneItemCraftTime > 0f ? item.OneItemCraftTime : item.Recipe.craftingTime;
            total += Mathf.Max(0f, item.CraftingTimeLeft) + one * (item.Multiplier - 1);
            if (cooking.Length == 0)
            {
                ItemClass output = item.Recipe.GetOutputItemClass();
                cooking = output != null ? output.GetLocalizedItemName() : item.Recipe.GetName();
            }
        }
        return total;
    }

    internal static string Clock(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return (s / 60) + ":" + (s % 60).ToString("00");
    }
}
