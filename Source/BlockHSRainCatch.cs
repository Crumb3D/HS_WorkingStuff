using System.Collections.Generic;

// Collects dew and rain under open sky, no jars needed. Bare it gives murky water, a dew filter makes it clean,
// the gatherer and tarp speed it up. It pushes what it holds down its pipes into water tanks.
public class BlockHSRainCatch : Block
{
    public float TakeDelay = 5f;

    public override void Init()
    {
        base.Init();
        if (Properties.Values.ContainsKey("TakeDelay"))
            TakeDelay = StringParsers.ParseFloat(Properties.Values["TakeDelay"]);
    }

    public override void OnBlockAdded(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(world, _chunk, _blockPos, _blockValue, _addedByPlayer);
        if (_blockValue.ischild)
            return;
        int units;
        bool clean;
        if (HSPickup.TakePendingWater(out units, out clean))
            HSWater.SetTank(_blockPos, new HSWater.Tank { Units = units, Quality = clean ? HSWater.QualityClean : HSWater.QualityMurky });
        else
            HSWater.SetTank(_blockPos, HSWater.GetTank(_blockPos));
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
        if (_blockValue.ischild)
            return;
        HSWater.SetTank(_blockPos, HSWater.GetTank(_blockPos));
        HSRainCatchLegacy.Check(_world, _blockPos);
    }

    public override void OnBlockRemoved(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockRemoved(_world, _chunk, _blockPos, _blockValue);
        if (!_blockValue.ischild)
            HSWater.RemoveTank(_blockPos);
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        Vector3i pos = HSWater.Parent(_blockPos, _blockValue);
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSWater.RainCatchPrompt(_world as World, pos);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        Vector3i pos = HSWater.Parent(_blockPos, _blockValue);
        int mods = HSWater.GetTank(pos).Mods;
        bool plumbing = HSWorkingStuffSettings.EnablePlumbing;
        var list = new List<BlockActivationCommand>();
        list.Add(new BlockActivationCommand("open", "hand", true));
        list.Add(new BlockActivationCommand("filljar", "water", plumbing));
        list.Add(new BlockActivationCommand("fillall", "water", plumbing));
        if (mods != 7)
        {
            bool can = ToolsToAdd(_entityFocusing as EntityPlayer, mods) != 0;
            list.Add(new BlockActivationCommand(can ? "addtools" : "notools", "wrench", true));
        }
        if (mods != 0)
            list.Add(new BlockActivationCommand("removetools", "tool", true));
        bool land = _world.IsMyLandProtectedBlock(pos, _world.GetGameManager().GetPersistentLocalPlayer());
        list.Add(new BlockActivationCommand("take", "hand", land && TakeDelay > 0f));
        return list.ToArray();
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        Vector3i pos = HSWater.Parent(_blockPos, _blockValue);
        int mods = HSWater.GetTank(pos).Mods;
        switch (_commandName)
        {
            case "open":
                XUiC_HSRainCatchWindowGroup.Open(_player, pos);
                return true;
            case "filljar":
                HSWorkingStuffNet.SendWater(HSWorkingStuffNet.TankFill, pos, _player);
                return true;
            case "fillall":
                HSWorkingStuffNet.SendWater(HSWorkingStuffNet.TankFillAll, pos, _player);
                return true;
            case "notools":
                GameManager.ShowTooltip(_player, Localization.Get("blockcommand_notools"));
                return true;
            case "addtools":
                InstallTools(_player, pos, mods);
                return true;
            case "removetools":
                ReturnTools(_player, pos, mods);
                return true;
            case "take":
                ReturnTools(_player, pos, mods);
                var t = HSWater.GetTank(pos);
                HSPickup.CaptureWater(t.Units, t.Quality >= HSWater.QualityClean);
                takeItemWithTimer(pos, _blockValue, _player, TakeDelay > 0f ? TakeDelay : 2f);
                return true;
        }
        return false;
    }

    static int HeldCount(EntityPlayer player, ItemValue iv)
    {
        int n = 0;
        if (player.bag != null)
            n += player.bag.GetItemCount(iv);
        if (player.inventory != null)
            n += player.inventory.GetItemCount(iv);
        return n;
    }

    static int ToolsToAdd(EntityPlayer player, int mods)
    {
        if (player == null)
            return 0;
        int add = 0;
        for (int i = 0; i < HSWater.ModItems.Length; i++)
        {
            if ((mods & HSWater.ModFlags[i]) != 0)
                continue;
            ItemValue iv = ItemClass.GetItem(HSWater.ModItems[i]);
            if (iv == null || iv.IsEmpty())
                continue;
            if (HeldCount(player, iv) > 0)
                add |= HSWater.ModFlags[i];
        }
        return add;
    }

    // Player inventory is the client's, so the tool leaves (or comes back to) the bag here and the server only flips the flag.
    static void InstallTools(EntityPlayerLocal player, Vector3i pos, int mods)
    {
        int added = 0;
        for (int i = 0; i < HSWater.ModItems.Length; i++)
        {
            if ((mods & HSWater.ModFlags[i]) != 0)
                continue;
            ItemValue iv = ItemClass.GetItem(HSWater.ModItems[i]);
            if (iv == null || iv.IsEmpty())
                continue;
            int took = player.bag != null ? player.bag.DecItem(iv, 1) : 0;
            if (took < 1 && player.inventory != null)
                took = player.inventory.DecItem(iv, 1);
            if (took > 0)
                added |= HSWater.ModFlags[i];
        }
        if (added == 0)
        {
            GameManager.ShowTooltip(player, Localization.Get("blockcommand_notools"));
            return;
        }
        HSWater.SetMods(pos, mods | added);
        HSWorkingStuffNet.SendWater(HSWorkingStuffNet.RainInstall, pos, player, added);
    }

    static void ReturnTools(EntityPlayerLocal player, Vector3i pos, int mods)
    {
        if (mods == 0)
            return;
        for (int i = 0; i < HSWater.ModItems.Length; i++)
        {
            if ((mods & HSWater.ModFlags[i]) == 0)
                continue;
            ItemValue iv = ItemClass.GetItem(HSWater.ModItems[i]);
            if (iv != null && !iv.IsEmpty())
                HSWater.GiveOrDrop(player, new ItemStack(iv, 1));
        }
        HSWater.SetMods(pos, 0);
        HSWorkingStuffNet.SendWater(HSWorkingStuffNet.RainRemove, pos, player, mods);
    }
}
