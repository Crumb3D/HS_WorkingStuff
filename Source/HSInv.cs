using System;
using System.Reflection;

// 3.2 bag/toolbelt/storage use ItemStack[]. 3.3 uses ItemStackGrid. One DLL, pick at runtime.
public static class HSInv
{
    static PropertyInfo bagGrid;
    static MethodInfo bagGetSlots;
    static MethodInfo bagAdd;
    static PropertyInfo invGrid;
    static MethodInfo invGetSlots;
    static MethodInfo invAdd;
    static PropertyInfo storageGrid;
    static PropertyInfo storageItems;
    static bool resolved;

    static void Resolve()
    {
        if (resolved)
            return;
        resolved = true;
        bagGrid = typeof(Bag).GetProperty("ItemGrid");
        bagGetSlots = typeof(Bag).GetMethod("GetSlots", Type.EmptyTypes);
        bagAdd = typeof(Bag).GetMethod("AddItem", new[] { typeof(ItemStack) });
        invGrid = typeof(Inventory).GetProperty("ItemGrid");
        invGetSlots = typeof(Inventory).GetMethod("GetSlots", Type.EmptyTypes);
        invAdd = typeof(Inventory).GetMethod("AddItem", new[] { typeof(ItemStack) });
        storageGrid = typeof(TEFeatureStorage).GetProperty("ItemGrid");
        storageItems = typeof(TEFeatureStorage).GetProperty("items");
    }

    public static void ForEachPlayerStack(EntityPlayer player, Action<ItemStack> fn)
    {
        if (player == null || fn == null)
            return;
        ForEach(GetBagStacks(player), fn);
        ForEach(GetToolStacks(player), fn);
    }

    static void ForEach(ItemStack[] arr, Action<ItemStack> fn)
    {
        if (arr == null)
            return;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] != null && !arr[i].IsEmpty())
                fn(arr[i]);
        }
    }

    public static ItemStack[] GetBagStacks(EntityPlayer player)
    {
        Resolve();
        if (player == null || player.bag == null)
            return null;
        if (bagGrid != null)
            return FromGrid(bagGrid.GetValue(player.bag));
        if (bagGetSlots != null)
            return bagGetSlots.Invoke(player.bag, null) as ItemStack[];
        return null;
    }

    public static ItemStack[] GetToolStacks(EntityPlayer player)
    {
        Resolve();
        if (player == null || player.inventory == null)
            return null;
        if (invGrid != null)
            return FromGrid(invGrid.GetValue(player.inventory));
        if (invGetSlots != null)
            return invGetSlots.Invoke(player.inventory, null) as ItemStack[];
        return null;
    }

    public static ItemStack GetHeld(EntityPlayer player)
    {
        if (player == null || player.inventory == null)
            return null;
        Inventory inv = player.inventory;
        MethodInfo getItem = typeof(Inventory).GetMethod("GetItem", new[] { typeof(int) });
        if (getItem == null)
            getItem = typeof(Inventory).GetMethod("GetItemStack", new[] { typeof(int) });
        int idx = HeldIndex(player);
        if (getItem == null)
            return null;
        return getItem.Invoke(inv, new object[] { idx }) as ItemStack;
    }

    public static int HeldIndex(EntityPlayer player)
    {
        if (player == null || player.inventory == null)
            return 0;
        PropertyInfo idxProp = typeof(Inventory).GetProperty("holdingItemIdx");
        if (idxProp != null)
            return (int)idxProp.GetValue(player.inventory, null);
        FieldInfo idxField = typeof(Inventory).GetField("holdingItemIdx");
        return idxField != null ? (int)idxField.GetValue(player.inventory) : 0;
    }

    public static bool AddToPlayer(EntityPlayer player, ItemStack stack)
    {
        Resolve();
        if (player == null || stack == null || stack.IsEmpty())
            return false;
        if (player.bag != null && bagAdd != null)
        {
            object r = bagAdd.Invoke(player.bag, new object[] { stack });
            if (r is bool && (bool)r && HSItem.Count(stack) == 0)
                return true;
        }
        if (player.inventory != null && invAdd != null)
        {
            object r2 = invAdd.Invoke(player.inventory, new object[] { stack });
            if (r2 is bool)
                return (bool)r2;
        }
        return false;
    }

    public static TEFeatureStorage GetStorage(TileEntity te)
    {
        if (te == null)
            return null;
        TEFeatureStorage storage;
        if (te.TryGetSelfOrFeature(out storage))
            return storage;
        return null;
    }

    public static ItemStack[] GetStorageStacks(TileEntity te)
    {
        Resolve();
        TEFeatureStorage storage = GetStorage(te);
        if (storage == null)
            return null;
        if (storageGrid != null)
            return FromGrid(storageGrid.GetValue(storage));
        if (storageItems != null)
            return storageItems.GetValue(storage) as ItemStack[];
        return null;
    }

    public static void SetModified(TileEntity te)
    {
        if (te == null)
            return;
        te.SetModified();
        TEFeatureStorage storage = GetStorage(te);
        if (storage != null)
            storage.SetModified();
    }

    public static void NotifyPlayer(EntityPlayer player)
    {
        if (player == null)
            return;
        try
        {
            if (player.bag != null)
            {
                MethodInfo m = typeof(Bag).GetMethod("onBackpackChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null)
                    m.Invoke(player.bag, null);
            }
        }
        catch { }
    }

    static ItemStack[] FromGrid(object gridObj)
    {
        if (gridObj == null)
            return null;
        PropertyInfo len = gridObj.GetType().GetProperty("Length");
        if (len == null)
            return null;
        int n = (int)len.GetValue(gridObj, null);
        var arr = new ItemStack[n];
        PropertyInfo item = gridObj.GetType().GetProperty("Item", new[] { typeof(int) });
        MethodInfo get = null;
        if (item != null)
            get = item.GetGetMethod();
        if (get == null)
        {
            var idx = gridObj.GetType().GetMethod("get_Item", new[] { typeof(int) });
            get = idx;
        }
        if (get == null)
            return null;
        for (int i = 0; i < n; i++)
            arr[i] = get.Invoke(gridObj, new object[] { i }) as ItemStack;
        return arr;
    }
}
