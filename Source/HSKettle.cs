using System.Collections.Generic;
using Audio;
using UnityEngine;

// Kettle: fill it with jars of water (one jar is one cup, up to 3), switch it on to boil, then pour.
// Pouring onto a mug with a tea bag in it makes the brew; an empty jar takes a jar of boiled water.
// Block meta2: bits 0-1 = cups of water, bit 2 = boiling, bit 3 = hot. meta itself belongs to the
// workstation (its particles), so it is left alone.
public class BlockHSKettle : BlockHSStove
{
    readonly BlockActivationCommand[] kettleCmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("hskettlefill", "water", false),
        new BlockActivationCommand("hskettleboil", "electric_switch", false),
        new BlockActivationCommand("hskettlepour", "coffee", false),
        new BlockActivationCommand("hskettlejar", "water", false),
        new BlockActivationCommand("open", "campfire", false),
        new BlockActivationCommand("take", "hand", false)
    };

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
        if (!_blockValue.ischild)
            HSKettle.Watch(_blockPos, _blockValue);
    }

    public override void OnBlockValueChanged(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _oldBlockValue, BlockValue _newBlockValue)
    {
        base.OnBlockValueChanged(_world, _chunk, _blockPos, _oldBlockValue, _newBlockValue);
        if (!_newBlockValue.ischild)
            HSKettle.Watch(_blockPos, _newBlockValue);
    }

    public override void OnBlockRemoved(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        HSKettle.Forget(_blockPos);
        base.OnBlockRemoved(world, _chunk, _blockPos, _blockValue);
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string held = HSKettle.Held(_entityFocusing as EntityPlayer);
        int cups = HSKettle.Cups(_blockValue);
        bool hot = HSKettle.Hot(_blockValue);
        bool boiling = HSKettle.Boiling(_blockValue);
        kettleCmds[0].enabled = HSKettle.IsWaterJar(held) && cups < HSKettle.MaxCups && !boiling;
        kettleCmds[1].enabled = cups > 0 && !boiling && !hot;
        kettleCmds[2].enabled = hot && cups > 0 && HSKettle.BrewOf(held) != null;
        kettleCmds[3].enabled = hot && cups > 0 && held == "drinkJarEmpty";
        var te = _world.GetTileEntity(_blockPos) as TileEntityWorkstation;
        kettleCmds[4].enabled = te != null && !te.OutputEmpty();
        bool mine = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        kettleCmds[5].enabled = mine && TakeDelay > 0f;
        return kettleCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (!_commandName.StartsWith("hskettle"))
            return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
        BlockValue bv = _world.GetBlock(_blockPos);
        int cups = HSKettle.Cups(bv);
        bool hot = HSKettle.Hot(bv);
        bool boiling = HSKettle.Boiling(bv);
        string held = HSKettle.Held(_player);
        Vector3 at = _blockPos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
        if (_commandName == "hskettlefill")
        {
            if (!HSKettle.IsWaterJar(held) || cups >= HSKettle.MaxCups || boiling)
                return true;
            _player.inventory.DecHoldingItem(1);
            HSWater.GiveOrDrop(_player, new ItemStack(ItemClass.GetItem("drinkJarEmpty"), 1));
            _world.SetBlockRPC(_blockPos, HSKettle.With(bv, cups + 1, false, false));
            Manager.BroadcastPlay(at, "bucketfill_water");
            GameManager.ShowTooltip(_player, HSLoc.Format("hsKettleFilled", "Kettle: {0} of {1} cups of water", cups + 1, HSKettle.MaxCups));
            return true;
        }
        if (_commandName == "hskettleboil")
        {
            if (cups == 0 || boiling || hot)
                return true;
            if (!HSKettle.Live(_world, _blockPos))
            {
                GameManager.ShowTooltip(_player, HSKettle.PluggedIn(_world, _blockPos)
                    ? HSLoc.Get("hsKettleUnpowered", "No power: plug this kettle in to boil")
                    : HSLoc.Get("hsKettleNoSocket", "Not plugged in: wire it from a socket"));
                return true;
            }
            _world.SetBlockRPC(_blockPos, HSKettle.With(bv, cups, true, false));
            Manager.BroadcastPlay(at, "switch_up");
            return true;
        }
        if (!hot || cups == 0)
            return true;
        string give = _commandName == "hskettlepour" ? HSKettle.BrewOf(held) : held == "drinkJarEmpty" ? "drinkJarBoiledWater" : null;
        if (give == null)
            return true;
        _player.inventory.DecHoldingItem(1);
        HSWater.GiveOrDrop(_player, new ItemStack(ItemClass.GetItem(give), 1));
        _world.SetBlockRPC(_blockPos, HSKettle.With(bv, cups - 1, false, cups - 1 > 0));
        Manager.BroadcastPlay(at, "hsKettlePour");
        GameManager.ShowTooltip(_player, _commandName == "hskettlepour"
            ? HSLoc.Get("hsKettleBrewed", "Poured: your brew is ready")
            : HSLoc.Get("hsKettleJarred", "Poured a jar of boiled water"));
        return true;
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSKettle.Status(_blockPos, _blockValue);
    }
}

// A mug put down on the side: drop a tea bag in, then pick it up and pour the kettle onto it.
// Block meta2: 0 = empty, 1 = PJ Tops bag, 2 = Yorkie Brew bag. The model shows "<Model>_BagPJ" / "_BagYorkie".
public class BlockHSMug : Block
{
    readonly BlockActivationCommand[] mugCmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("hsmugbag", "add", false),
        new BlockActivationCommand("hsmugpickup", "hand", true)
    };

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        mugCmds[0].enabled = _blockValue.meta2 == 0 && HSKettle.BagOf(HSKettle.Held(_entityFocusing as EntityPlayer)) > 0;
        return mugCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        BlockValue bv = _world.GetBlock(_blockPos);
        if (_commandName == "hsmugbag")
        {
            int bag = HSKettle.BagOf(HSKettle.Held(_player));
            if (bv.meta2 != 0 || bag == 0)
                return true;
            _player.inventory.DecHoldingItem(1);
            bv.meta2 = (byte)bag;
            _world.SetBlockRPC(_blockPos, bv);
            Manager.BroadcastPlay(_blockPos.ToVector3() + new Vector3(0.5f, 0.2f, 0.5f), "paper_place");
            return true;
        }
        if (_commandName == "hsmugpickup")
        {
            string item = bv.meta2 == 1 ? "hsMugBagPJ" : bv.meta2 == 2 ? "hsMugBagYorkie" : "hsMug";
            _world.SetBlockRPC(_blockPos, BlockValue.Air);
            HSWater.GiveOrDrop(_player, new ItemStack(ItemClass.GetItem(item), 1));
            Manager.BroadcastPlay(_blockPos.ToVector3() + new Vector3(0.5f, 0.2f, 0.5f), "jars_grab");
            return true;
        }
        return false;
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
        ShowBag(_ebcd, _blockValue);
    }

    public override void OnBlockValueChanged(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _oldBlockValue, BlockValue _newBlockValue)
    {
        base.OnBlockValueChanged(_world, _chunk, _blockPos, _oldBlockValue, _newBlockValue);
        var w = _world as World;
        ShowBag(w != null && w.ChunkCache != null ? w.ChunkCache.GetBlockEntity(_blockPos) : null, _newBlockValue);
    }

    static void ShowBag(BlockEntityData ebcd, BlockValue bv)
    {
        if (GameManager.IsDedicatedServer || ebcd == null || !ebcd.bHasTransform || ebcd.transform == null)
            return;
        foreach (var t in ebcd.transform.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith("_BagPJ"))
                t.gameObject.SetActive(bv.meta2 == 1);
            else if (t.name.EndsWith("_BagYorkie"))
                t.gameObject.SetActive(bv.meta2 == 2);
        }
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string state = _blockValue.meta2 == 1 ? HSLoc.Get("hsMugHasPJ", "A PJ Tops bag is in: pick it up and pour the boiled kettle onto it")
            : _blockValue.meta2 == 2 ? HSLoc.Get("hsMugHasYorkie", "A Yorkie Brew bag is in: pick it up and pour the boiled kettle onto it")
            : HSLoc.Get("hsMugEmpty", "Empty: hold a tea bag to put one in");
        return HSLoc.Get("hsMug", "Mug") + "\n" + state;
    }
}

public static class HSKettle
{
    public const int MaxCups = 3;
    static readonly Dictionary<Vector3i, float> boilLeft = new Dictionary<Vector3i, float>();
    static readonly Dictionary<Vector3i, float> hotUntil = new Dictionary<Vector3i, float>();
    static readonly HashSet<Vector3i> humming = new HashSet<Vector3i>();
    static float lastTick;

    public static int Cups(BlockValue bv)
    {
        return bv.meta2 & 3;
    }

    public static bool Boiling(BlockValue bv)
    {
        return (bv.meta2 & 4) != 0;
    }

    public static bool Hot(BlockValue bv)
    {
        return (bv.meta2 & 8) != 0;
    }

    public static BlockValue With(BlockValue bv, int cups, bool boiling, bool hot)
    {
        bv.meta2 = (byte)((Mathf.Clamp(cups, 0, MaxCups)) | (boiling ? 4 : 0) | (hot ? 8 : 0));
        return bv;
    }

    public static string Held(EntityPlayer player)
    {
        ItemValue iv = player != null && player.inventory != null ? player.inventory.holdingItemItemValue : null;
        ItemClass ic = iv != null ? iv.ItemClass : null;
        return ic != null ? ic.GetItemName() : "";
    }

    public static bool IsWaterJar(string item)
    {
        return item == "drinkJarRiverWater" || item == "drinkJarBoiledWater" || item == "drinkJarPureMineralWater";
    }

    public static int BagOf(string item)
    {
        return item == "hsTeaBagsPJ" ? 1 : item == "hsTeaBagsYorkie" ? 2 : 0;
    }

    public static string BrewOf(string item)
    {
        return item == "hsMugBagPJ" ? "hsMugTeaPJ" : item == "hsMugBagYorkie" ? "hsMugTeaYorkie" : null;
    }

    public static bool PluggedIn(WorldBase world, Vector3i pos)
    {
        return HSStovePower.PluggedIn(world, pos);
    }

    public static bool Live(WorldBase world, Vector3i pos)
    {
        return HSStovePower.IsPowered(pos) && PluggedIn(world, pos);
    }

    public static float BoilSeconds(int cups)
    {
        return Mathf.Max(5f, HSWorkingStuffSettings.KettleBoilSeconds) * (0.5f + 0.25f * cups);
    }

    public static void Watch(Vector3i pos, BlockValue bv)
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        if (Boiling(bv))
        {
            if (!boilLeft.ContainsKey(pos))
                boilLeft[pos] = BoilSeconds(Cups(bv));
            hotUntil.Remove(pos);
            return;
        }
        boilLeft.Remove(pos);
        Hum(pos, false);
        if (Hot(bv))
        {
            if (!hotUntil.ContainsKey(pos))
                hotUntil[pos] = Time.time + Mathf.Max(1f, HSWorkingStuffSettings.KettleHotMinutes) * 60f;
        }
        else
            hotUntil.Remove(pos);
    }

    public static void Forget(Vector3i pos)
    {
        boilLeft.Remove(pos);
        hotUntil.Remove(pos);
        Hum(pos, false);
    }

    public static void Clear()
    {
        boilLeft.Clear();
        hotUntil.Clear();
        humming.Clear();
    }

    static void Hum(Vector3i pos, bool on)
    {
        if (on == humming.Contains(pos))
            return;
        Vector3 at = pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
        if (on)
        {
            humming.Add(pos);
            Manager.BroadcastPlay(at, "hsKettleBoil");
        }
        else
        {
            humming.Remove(pos);
            Manager.BroadcastStop(at, "hsKettleBoil");
        }
    }

    // The server boils: only while the kettle has power, and it clicks off when it's done. Then it cools.
    public static void ServerTick()
    {
        if (!HSWorkingStuffNet.IsAuthority || (boilLeft.Count == 0 && hotUntil.Count == 0) || Time.time - lastTick < 1f)
            return;
        float dt = Mathf.Min(Time.time - lastTick, 5f);
        lastTick = Time.time;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        foreach (Vector3i pos in new List<Vector3i>(boilLeft.Keys))
        {
            BlockValue bv = world.GetBlock(pos);
            if (!(bv.Block is BlockHSKettle) || !Boiling(bv))
            {
                Forget(pos);
                continue;
            }
            bool powered = Live(world, pos);
            Hum(pos, powered);
            if (!powered)
                continue;
            float left = boilLeft[pos] - dt;
            boilLeft[pos] = left;
            if (left > 0f)
                continue;
            boilLeft.Remove(pos);
            Hum(pos, false);
            world.SetBlockRPC(pos, With(bv, Cups(bv), false, true));
            Manager.BroadcastPlay(pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f), "switch_down");
        }
        foreach (Vector3i pos in new List<Vector3i>(hotUntil.Keys))
        {
            if (Time.time < hotUntil[pos])
                continue;
            hotUntil.Remove(pos);
            BlockValue bv = world.GetBlock(pos);
            if (bv.Block is BlockHSKettle && Hot(bv))
                world.SetBlockRPC(pos, With(bv, Cups(bv), false, false));
        }
    }

    public static string Status(Vector3i pos, BlockValue bv)
    {
        int cups = Cups(bv);
        string water = cups == 0
            ? HSLoc.Get("hsKettleEmpty", "Empty: hold a jar of water to fill it (one jar is one cup)")
            : HSLoc.Format("hsKettleWater", "Water: {0} of {1} cups", cups, MaxCups);
        string state;
        if (!PluggedIn(GameManager.Instance != null ? GameManager.Instance.World : null, pos))
            state = HSLoc.Get("hsKettleNoSocket", "Not plugged in: wire it from a socket");
        else if (!HSStovePower.IsPowered(pos))
            state = HSLoc.Get("hsKettleUnpowered", "No power: the socket is off");
        else if (Boiling(bv))
            state = HSLoc.Get("hsKettleBoilingNow", "Boiling...");
        else if (Hot(bv) && cups > 0)
            state = HSLoc.Get("hsKettleHot", "Boiled: hold a mug with a tea bag in it and pour");
        else if (cups > 0)
            state = HSLoc.Get("hsKettleCold", "Cold: switch it on to boil");
        else
            state = HSLoc.Get("hsKettleReady", "Powered");
        return water + "\n" + state;
    }
}
