using HarmonyLib;

// Storage only. Fills from a piped rain catch, a lake intake (murky), or by pouring jars and buckets in.
public class BlockHSTank : Block
{
    public float TakeDelay = 5f;

    BlockActivationCommand[] tankCmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("pour", "water", true),
        new BlockActivationCommand("pourall", "water", true),
        new BlockActivationCommand("filljar", "water", true),
        new BlockActivationCommand("fillall", "water", true),
        new BlockActivationCommand("take", "hand", false)
    };

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
        RestoreTank(_blockPos);
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

    static void RestoreTank(Vector3i pos)
    {
        int units;
        bool clean;
        if (HSPickup.TakePendingWater(out units, out clean))
            HSWater.SetTank(pos, new HSWater.Tank { Units = units, Quality = clean ? HSWater.QualityBoiled : HSWater.QualityMurky });
        else
            HSWater.SetTank(pos, HSWater.GetTank(pos));
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSWater.TankPrompt(_world as World, _blockPos);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        bool land = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        bool plumbing = HSWorkingStuffSettings.EnablePlumbing;
        tankCmds[0].enabled = plumbing;
        tankCmds[1].enabled = plumbing;
        tankCmds[2].enabled = plumbing;
        tankCmds[3].enabled = plumbing;
        tankCmds[4].enabled = land && TakeDelay > 0f;
        return tankCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_commandName == "take")
        {
            var t = HSWater.GetTank(_blockPos);
            HSPickup.CaptureWater(t.Units, t.Quality >= HSWater.QualityClean);
            takeItemWithTimer(_blockPos, _blockValue, _player, TakeDelay > 0f ? TakeDelay : 2f);
            return true;
        }
        byte kind;
        switch (_commandName)
        {
            case "pour": kind = HSWorkingStuffNet.TankPour; break;
            case "pourall": kind = HSWorkingStuffNet.TankPourAll; break;
            case "filljar": kind = HSWorkingStuffNet.TankFill; break;
            case "fillall": kind = HSWorkingStuffNet.TankFillAll; break;
            default: return false;
        }
        HSWorkingStuffNet.SendWater(kind, _blockPos, _player);
        return true;
    }
}

// Tanks and rain catches used to be vanilla collectors. A save from then still carries that tile entity.
public static class HSRainCatchLegacy
{
    public static void Check(WorldBase world, Vector3i pos)
    {
        if (!HSWorkingStuffNet.IsAuthority || world == null)
            return;
        if (world.GetTileEntity(pos) is TileEntityCollector)
            HSWater.QueueMigrate(pos);
    }
}

[HarmonyPatch(typeof(TileEntityCollector), "UpdateTick")]
public static class HSPatchLegacyCollectorTick
{
    static bool Prefix(TileEntityCollector __instance)
    {
        try
        {
            if (__instance.blockValue.Block is BlockCollector)
                return true;
            HSWater.QueueMigrate(__instance.ToWorldPos());
            return false;
        }
        catch
        {
            return true;
        }
    }
}
