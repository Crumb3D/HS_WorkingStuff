public class BlockHSToaster : BlockHSStove
{
    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string name = _blockValue.Block.GetLocalizedBlockName();
        bool powered = HSStovePower.IsPowered(_blockPos) && HSStovePower.PluggedIn(_world, _blockPos);
        TileEntityWorkstation te = _world.GetTileEntity(_blockPos) as TileEntityWorkstation;
        string making;
        float left = BlockHSMicrowave.SecondsLeft(te, out making);
        string status;
        if (left <= 0f)
        {
            if (te != null && !te.OutputEmpty())
                status = HSLoc.Get("hsToasterDone", "Popped up: your toast is ready");
            else if (powered)
                status = HSLoc.Get("hsToasterPowered", "Powered: add slices of bread");
            else
                status = HSStovePower.PluggedIn(_world, _blockPos)
                    ? HSLoc.Get("hsToasterUnpowered", "No power: the socket is off")
                    : HSLoc.Get("hsKettleNoSocket", "Not plugged in: wire it from a socket");
        }
        else if (!powered)
            status = HSLoc.Format("hsMicrowaveNoPowerPaused", "No power: paused, {0} left", BlockHSMicrowave.Clock(left));
        else
            status = HSLoc.Format("hsToasterToasting", "Toasting: {0} left", BlockHSMicrowave.Clock(left));
        return name + "\n" + status;
    }
}
