public class BlockHSCoffeeMaker : BlockHSStove
{
    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string name = _blockValue.Block.GetLocalizedBlockName();
        bool powered = HSStovePower.IsPowered(_blockPos);
        TileEntityWorkstation te = _world.GetTileEntity(_blockPos) as TileEntityWorkstation;
        string brewing;
        float left = BlockHSMicrowave.SecondsLeft(te, out brewing);
        string status;
        if (left <= 0f)
        {
            if (te != null && !te.OutputEmpty())
                status = HSLoc.Get("hsCoffeeDone", "Done: your coffee is ready");
            else if (powered)
                status = HSLoc.Get("hsCoffeePowered", "Powered: add a jar of water and coffee beans");
            else
                status = HSLoc.Get("hsCoffeeUnpowered", "No power: wire this coffee maker to brew");
        }
        else if (!powered)
            status = HSLoc.Format("hsMicrowaveNoPowerPaused", "No power: paused, {0} left", BlockHSMicrowave.Clock(left));
        else
            status = HSLoc.Format("hsCoffeeBrewing", "Brewing {0}: {1} left", brewing, BlockHSMicrowave.Clock(left));
        return name + "\n" + status;
    }
}
