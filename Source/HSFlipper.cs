using System;
using System.Collections.Generic;
using System.Reflection;

// House-flipper: open/empty/broken POI furniture repairs shut, and the closed
// cupboard already has loot in it (searched), not a fresh unsearched ping.
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
    static PropertyInfo gridProp;
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
            if (!IsHouse(name))
                continue;
            if (!block.UpgradeBlock.isair)
                continue;
            string dest = Dest(name);
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
        if (!IsHouse(name))
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
        {
            Touch(storage, world);
            HSInv.SetModified(te);
            return true;
        }
        string listName = storage.lootListName;
        if (string.IsNullOrEmpty(listName))
        {
            Touch(storage, world);
            HSInv.SetModified(te);
            return true;
        }
        if (listName.EndsWith("NOPOP", StringComparison.OrdinalIgnoreCase))
            listName = listName.Substring(0, listName.Length - 5);
        LootContainer loot = LootContainer.GetLootContainer(listName, false);
        if (loot == null)
        {
            Touch(storage, world);
            HSInv.SetModified(te);
            return true;
        }
        EntityPlayer player = world.GetEntity(entityId) as EntityPlayer;
        if (player == null)
            return false;
        int slots = loot.size.x * loot.size.y;
        ItemStack[] stacks = HSInv.GetStorageStacks(te);
        if (stacks != null && stacks.Length > 0)
            slots = stacks.Length;
        if (slots <= 0)
            slots = 1;
        int stage = loot.useUnmodifiedLootstage
            ? player.unModifiedGameStage
            : player.GetHighestPartyLootStage(storage.LootStageMod, storage.LootStageBonus);
        IList<ItemStack> spawned = loot.Spawn(
            world.GetGameRandom(),
            slots,
            stage,
            0f,
            player,
            FastTags<TagGroup.Global>.none,
            loot.UniqueItems,
            loot.IgnoreLootProb,
            false);
        if (spawned != null)
        {
            int n = spawned.Count;
            if (n > slots)
                n = slots;
            for (int i = 0; i < n; i++)
            {
                ItemStack stack = spawned[i];
                if (stack == null)
                    continue;
                storage.UpdateSlot(i, stack.Clone());
            }
        }
        Touch(storage, world);
        HSInv.SetModified(te);
        return true;
    }

    static void Touch(TEFeatureStorage storage, WorldBase world)
    {
        Resolve();
        if (storage == null)
            return;
        ulong time = world != null ? world.GetWorldTime() : 0uL;
        if (touchedProp != null && touchedProp.CanWrite)
            touchedProp.SetValue(storage, true, null);
        if (worldTouchedProp != null && worldTouchedProp.CanWrite)
            worldTouchedProp.SetValue(storage, time, null);
        if (gridProp == null)
            return;
        object grid = gridProp.GetValue(storage, null);
        if (grid == null)
            return;
        if (gridTouch != null)
            gridTouch.Invoke(grid, null);
        else
        {
            PropertyInfo wt = grid.GetType().GetProperty("WorldTimeTouched");
            if (wt != null && wt.CanWrite)
                wt.SetValue(grid, time, null);
        }
    }

    static void Resolve()
    {
        if (resolved)
            return;
        resolved = true;
        Type t = typeof(TEFeatureStorage);
        touchedProp = t.GetProperty("bTouched");
        worldTouchedProp = t.GetProperty("worldTimeTouched");
        gridProp = t.GetProperty("ItemGrid");
        if (gridProp != null)
            gridTouch = gridProp.PropertyType.GetMethod("Touch", Type.EmptyTypes);
    }

    static bool IsHouse(string name)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith("hs"))
            return false;
        if (name.IndexOf("Helper", StringComparison.Ordinal) >= 0)
            return false;
        if (name.IndexOf("_Player", StringComparison.Ordinal) >= 0)
            return false;
        for (int i = 0; i < Prefixes.Length; i++)
        {
            if (name.StartsWith(Prefixes[i]))
                return true;
        }
        return false;
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
