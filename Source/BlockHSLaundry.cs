using System;
using UnityEngine;

// Washing machine and tumble dryer: cooker-style workstation. Clothes you wear or carry
// show as washable recipes; time depends on the piece and how dirty or wet it is.
public class BlockHSLaundry : BlockHSStove
{
    public bool IsDryer
    {
        get { return GetBlockName().IndexOf("Dryer", StringComparison.OrdinalIgnoreCase) >= 0; }
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        if (!_blockValue.ischild)
            MigrateFridge(_world, _blockPos);
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
    }

    // Old washers were fridge storage. Read any leftover clothes, drop them, then the
    // stove load creates the workstation tile entity.
    static void MigrateFridge(WorldBase world, Vector3i pos)
    {
        if (world == null || !HSWorkingStuffNet.IsAuthority)
            return;
        TileEntity te = world.GetTileEntity(pos);
        if (te == null || te is TileEntityWorkstation)
            return;
        IHSFridgeInv fridge = HSFridgeInv.Of(te);
        if (fridge != null)
        {
            fridge.EnsureItems();
            Drop(fridge.Items, pos);
        }
        Chunk chunk = world.GetChunkFromWorldPos(pos) as Chunk;
        World w = world as World;
        if (chunk != null && w != null)
        {
            try
            {
                chunk.RemoveTileEntity(w, te);
            }
            catch (Exception e)
            {
                Log.Warning("[HSWorkingStuff] washer migrate at " + pos + ": " + e.Message);
            }
        }
        Log.Out("[HSWorkingStuff] washer at " + pos + " is now a workstation");
    }

    static void Drop(ItemStack[] items, Vector3i pos)
    {
        if (items == null || GameManager.Instance == null)
            return;
        Vector3 at = pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null || items[i].IsEmpty())
                continue;
            GameManager.Instance.ItemDropServer(items[i].Clone(), at, new Vector3(0.5f, 0f, 0.5f), -1, 600f);
        }
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        _blockPos = HSWater.Parent(_blockPos, _blockValue);
        World world = _world as World;
        bool powered = HSStovePower.IsPowered(_blockPos);
        string name = _blockValue.Block.GetLocalizedBlockName();
        string text = name + "\n" + (powered
            ? HSLoc.Get("hsLaundryPowered", "Powered")
            : HSLoc.Get("hsLaundryNoPower", "No power: wire it up first"));
        if (!IsDryer && world != null && HSWorkingStuffSettings.EnablePlumbing)
        {
            HSWater.Taps taps = HSWater.FindTaps(world, _blockPos);
            if (!taps.Cold.Ok)
                text += "\n" + HSWater.ColdReason(taps);
            else if (!HSLaundry.HasWashWater(world, _blockPos))
                text += "\n" + HSLoc.Format("hsWasherShort", "Not enough water: a wash needs {0} jars", HSWorkingStuffSettings.WasherJars);
        }
        TileEntityWorkstation te = _world.GetTileEntity(_blockPos) as TileEntityWorkstation;
        string job;
        float left = BlockHSMicrowave.SecondsLeft(te, out job);
        if (left > 0f)
        {
            text += "\n" + (powered
                ? HSLoc.Format("hsLaundryQueued", "{0}: {1} left", job, BlockHSMicrowave.Clock(left))
                : HSLoc.Format("hsLaundryPaused", "No power: paused, {0} left", BlockHSMicrowave.Clock(left)));
        }
        else if (te != null && !te.OutputEmpty())
            text += "\n" + HSLoc.Get("hsLaundryDone", "Done: take it from the output");
        return text;
    }
}
