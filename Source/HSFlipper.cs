using System;
using System.Collections.Generic;
using System.Reflection;

// House-flipper: open/empty/broken POI furniture repairs shut into empty
// player storage, not an unsearched loot ping and not a filled crate.
public static class HSFlipper
{
    struct FillJob
    {
        public Vector3i Pos;
        public int EntityId;
        public int Tries;
    }

    static readonly string[] Prefixes =
    {
        "cntCabinet", "cntCupboard", "cntCounter", "counterHollow",
        "cntNightstand", "cntDesk", "cntArmoire", "cntBookcase",
        "cntFileCabinet", "cntLocker", "cntWoodenChest", "cntFootlocker",
        "cntModularCloset", "cntMedicineCabinet", "cntPillCase",
        "cntGunSafe", "cntWallSafe", "cntDeskSafe", "cntToolBox",
        "cntDresser", "cntWardrobe", "cntCloset", "cntDrawer"
    };

    static readonly List<FillJob> Pending = new List<FillJob>();
    static PropertyInfo touchedProp;
    static PropertyInfo worldTouchedProp;
    static PropertyInfo playerStorageProp;
    static PropertyInfo gridProp;
    static PropertyInfo gridOwnedProp;
    static MethodInfo gridTouch;
    static bool resolved;

    public static void InjectFurniture()
    {
        if (Block.list == null)
            return;
        int n = 0;
        for (int i = 0; i < Block.list.Length; i++)
        {
            Block block = Block.list[i];
            if (block == null)
                continue;
            string name = block.blockName;
            if (!IsHouseSource(name))
                continue;
            if (!block.UpgradeBlock.isair)
                continue;
            string dest = PreferEmpty(Dest(name));
            if (string.IsNullOrEmpty(dest) || dest == name)
                continue;
            bool metal = IsMetal(name);
            string item = metal ? "resourceScrapIron" : "resourceWood";
            string count = metal ? "2" : "4";
            block.Properties.SetValue("UpgradeBlock", "ToBlock", dest);
            block.Properties.SetValue("UpgradeBlock", "Item", item);
            block.Properties.SetValue("UpgradeBlock", "ItemCount", count);
            block.Properties.SetValue("UpgradeBlock", "UpgradeHitCount", "4");
            BlockValue nv = Block.GetBlockValue(dest);
            if (nv.isair || nv.Block == null)
                continue;
            block.UpgradeBlock = nv;
            n++;
        }
        Log.Out("[HSWorkingStuff] House furniture: " + n + " blocks repair shut");
    }

    public static void AfterUpgrade(WorldBase world, Vector3i pos, int entityId)
    {
        if (!HSWorkingStuffNet.IsAuthority || world == null)
            return;
        BlockValue bv = world.GetBlock(pos);
        if (bv.Block == null)
            return;
        if (bv.ischild)
            pos = bv.Block.multiBlockPos.GetParentPos(pos, bv);
        bv = world.GetBlock(pos);
        string name = bv.Block != null ? bv.Block.GetBlockName() : null;
        if (!IsHouseDest(name))
            return;
        FillJob job;
        job.Pos = pos;
        job.EntityId = entityId;
        job.Tries = 0;
        Pending.Add(job);
    }

    public static void Tick()
    {
        if (!HSWorkingStuffNet.IsAuthority || Pending.Count == 0)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            FillJob job = Pending[i];
            job.Tries++;
            if (FillLoot(world, job.Pos, job.EntityId) || job.Tries >= 40)
                Pending.RemoveAt(i);
            else
                Pending[i] = job;
        }
    }

    public static void Clear()
    {
        Pending.Clear();
    }

    public static bool FillLoot(WorldBase world, Vector3i pos, int entityId)
    {
        if (world == null)
            return true;
        BlockValue bv = world.GetBlock(pos);
        if (bv.Block == null)
            return false;
        if (bv.ischild)
            pos = bv.Block.multiBlockPos.GetParentPos(pos, bv);
        bv = world.GetBlock(pos);
        if (bv.Block == null)
            return true;
        string name = bv.Block.GetBlockName();
        if (string.IsNullOrEmpty(name) || name.StartsWith("hs") || HSSalvage.IsWorking(name))
            return true;
        TileEntity te = world.GetTileEntity(pos);
        if (te == null)
            return false;
        TEFeatureStorage storage = HSInv.GetStorage(te);
        if (storage == null)
            return true;
        if (!storage.IsEmpty())
            storage.SetEmpty();
        MarkPlayerEmpty(storage, world);
        HSSalvage.MarkPlaced(world, pos);
        HSInv.SetModified(te);
        return true;
    }

    static void MarkPlayerEmpty(TEFeatureStorage storage, WorldBase world)
    {
        Resolve();
        if (storage == null)
            return;
        ulong time = world != null ? world.GetWorldTime() : 0uL;
        if (touchedProp != null && touchedProp.CanWrite)
            touchedProp.SetValue(storage, true, null);
        if (worldTouchedProp != null && worldTouchedProp.CanWrite)
            worldTouchedProp.SetValue(storage, time, null);
        if (playerStorageProp != null && playerStorageProp.CanWrite)
            playerStorageProp.SetValue(storage, true, null);
        if (gridProp == null)
            return;
        object grid = gridProp.GetValue(storage, null);
        if (grid == null)
            return;
        if (gridTouch != null)
            gridTouch.Invoke(grid, null);
        if (gridOwnedProp == null)
            gridOwnedProp = grid.GetType().GetProperty("PlayerOwned");
        if (gridOwnedProp != null && gridOwnedProp.CanWrite)
            gridOwnedProp.SetValue(grid, true, null);
    }

    static void Resolve()
    {
        if (resolved)
            return;
        resolved = true;
        Type t = typeof(TEFeatureStorage);
        touchedProp = t.GetProperty("bTouched");
        worldTouchedProp = t.GetProperty("worldTimeTouched");
        playerStorageProp = t.GetProperty("bPlayerStorage");
        gridProp = t.GetProperty("ItemGrid");
        if (gridProp != null)
        {
            gridTouch = gridProp.PropertyType.GetMethod("Touch", Type.EmptyTypes);
            gridOwnedProp = gridProp.PropertyType.GetProperty("PlayerOwned");
        }
    }

    static bool IsHouseSource(string name)
    {
        if (string.IsNullOrEmpty(name) || name.IndexOf("_Player", StringComparison.Ordinal) >= 0)
            return false;
        return IsHousePrefix(name);
    }

    static bool IsHouseDest(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        if (name.EndsWith("_Player", StringComparison.Ordinal))
            name = name.Substring(0, name.Length - 7);
        return IsHousePrefix(name);
    }

    static bool IsHousePrefix(string name)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith("hs"))
            return false;
        if (name.IndexOf("Helper", StringComparison.Ordinal) >= 0)
            return false;
        for (int i = 0; i < Prefixes.Length; i++)
        {
            if (name.StartsWith(Prefixes[i]))
                return true;
        }
        return false;
    }

    static string PreferEmpty(string dest)
    {
        if (string.IsNullOrEmpty(dest))
            return dest;
        string player = dest + "_Player";
        if (Exists(player))
            return player;
        return dest;
    }

    static bool IsMetal(string name)
    {
        return name.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Locker", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("FileCabinet", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Medicine", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Safe", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("ToolBox", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string Dest(string name)
    {
        string d;
        if (name.IndexOf("BrokenDoor", StringComparison.Ordinal) >= 0)
        {
            d = name.Replace("BrokenDoor", "");
            if (Exists(d))
                return d;
        }
        if (name.IndexOf("Broken", StringComparison.Ordinal) >= 0)
        {
            d = name.Replace("Broken", "Closed");
            if (Exists(d))
                return d;
            d = name.Replace("Broken", "");
            if (Exists(d))
                return d;
        }
        if (name.IndexOf("Open", StringComparison.Ordinal) >= 0)
        {
            d = name.Replace("Open", "Closed");
            if (Exists(d))
                return d;
            d = name.Replace("Open", "");
            if (Exists(d))
                return d;
        }
        if (name.IndexOf("Empty", StringComparison.Ordinal) >= 0)
        {
            d = name.Replace("Empty", "Full");
            if (Exists(d))
                return d;
            d = name.Replace("Empty", "Closed");
            if (Exists(d))
                return d;
        }
        return null;
    }

    static bool Exists(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        BlockValue bv = Block.GetBlockValue(name);
        return !bv.isair && bv.Block != null;
    }
}
