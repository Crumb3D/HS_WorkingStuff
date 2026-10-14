public class BlockHSStorage : BlockCompositeTileEntity
{
    BlockActivationCommand[] extra = new BlockActivationCommand[]
    {
        new BlockActivationCommand("storefood", "store_all_down", true),
        new BlockActivationCommand("takespoiled", "store_all_up", true)
    };

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        BlockActivationCommand[] baseCmds = base.GetBlockActivationCommands(_world, _blockValue, _blockPos, _entityFocusing);
        string n = _blockValue.Block.GetBlockName();
        if (n != "hsCooler")
            return baseCmds;
        var all = new BlockActivationCommand[baseCmds.Length + extra.Length];
        for (int i = 0; i < extra.Length; i++)
            all[i] = extra[i];
        for (int i = 0; i < baseCmds.Length; i++)
            all[extra.Length + i] = baseCmds[i];
        return all;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_commandName == "storefood" || _commandName == "takespoiled")
        {
            ItemStack[] stacks = HSInv.GetStorageStacks(_world.GetTileEntity(_blockPos));
            if (stacks == null)
                return false;
            bool any = _commandName == "storefood"
                ? HSSpoil.StorePerishables(_player, stacks)
                : HSSpoil.TakeSpoiled(_player, stacks);
            if (any)
                HSInv.SetModified(_world.GetTileEntity(_blockPos));
            return true;
        }
        return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
    }
}
