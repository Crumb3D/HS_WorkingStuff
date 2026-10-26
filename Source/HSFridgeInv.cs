using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

public interface IHSFridgeInv
{
    ItemStack[] Items { get; }
    void EnsureItems();
    void SetModified();
    void SetUserAccessing(bool accessing);
    bool IsPowered { get; }
    string OldestLabel();
}

// 3.3 cannot construct TileEntityHSFridge (vtable vs IHasWorldAddress). Items live
// next to a vanilla powered TE. 3.2 still uses TileEntityHSFridge via Of32.
public static class HSFridgeInv
{
    static readonly Dictionary<string, ItemStack[]> boxes = new Dictionary<string, ItemStack[]>();

    public static IHSFridgeInv Of(TileEntity te)
    {
        if (te == null)
            return null;
        if (HSGameVersion.Is33)
            return Of33(te);
        return Of32(te);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static IHSFridgeInv Of32(TileEntity te)
    {
        return te as TileEntityHSFridge;
    }

    static IHSFridgeInv Of33(TileEntity te)
    {
        var powered = te as TileEntityPowered;
        if (powered == null)
            return null;
        Block block = null;
        try
        {
            block = te.blockValue.Block;
        }
        catch
        {
            return null;
        }
        if (!(block is BlockHSFridge))
            return null;
        return new HSFridgeBox(powered);
    }

    public static ItemStack[] ItemsFor(TileEntityPowered te)
    {
        if (te == null)
            return ItemStack.CreateArray(TileEntityHSFridge.SlotCount);
        string key = KeyOf(te);
        ItemStack[] items;
        if (!boxes.TryGetValue(key, out items) || items == null || items.Length != TileEntityHSFridge.SlotCount)
        {
            items = ItemStack.CreateArray(TileEntityHSFridge.SlotCount);
            boxes[key] = items;
        }
        return items;
    }

    public static void ReadItems(TileEntityPowered te, PooledBinaryReader br)
    {
        if (te == null)
            return;
        ItemStack[] items = ItemsFor(te);
        if (br == null || br.BaseStream == null)
            return;
        Stream s = br.BaseStream;
        bool seek = s.CanSeek;
        if (seek && s.Position + 8 > s.Length)
            return;
        long start = seek ? s.Position : 0;
        int magic = br.ReadInt32();
        int n = magic == TileEntityHSFridge.Magic ? br.ReadInt32() : -1;
        if (n < 0 || n > 64)
        {
            if (seek)
                s.Position = start;
            return;
        }
        for (int i = 0; i < n && i < items.Length; i++)
            HSItem.Read(items[i], br);
    }

    public static void WriteItems(TileEntityPowered te, PooledBinaryWriter bw)
    {
        if (te == null || bw == null)
            return;
        ItemStack[] items = ItemsFor(te);
        bw.Write(TileEntityHSFridge.Magic);
        bw.Write(items.Length);
        for (int i = 0; i < items.Length; i++)
            HSItem.Write(items[i], bw);
    }

    // The same ItemStack objects, so in-place changes show in both. Unslice writes back slots that were replaced.
    public static ItemStack[] Slice(ItemStack[] items, int from, int count)
    {
        var part = new ItemStack[count];
        for (int i = 0; i < count; i++)
            part[i] = items != null && from + i < items.Length ? items[from + i] : ItemStack.Empty.Clone();
        return part;
    }

    public static void Unslice(ItemStack[] items, ItemStack[] part, int from)
    {
        if (items == null || part == null)
            return;
        for (int i = 0; i < part.Length && from + i < items.Length; i++)
            items[from + i] = part[i];
    }

    public static bool IsFridgeBlock(TileEntity te)
    {
        if (te == null)
            return false;
        try
        {
            Block block = te.blockValue.Block;
            if (block is BlockHSFridge)
                return true;
            return block is BlockHSLaundry && !(te is TileEntityWorkstation);
        }
        catch
        {
            return false;
        }
    }

    public static string OldestLabel(TileEntity te, ItemStack[] items)
    {
        if (items == null)
            return HSLoc.Get("hsFridgeEmptyFood", "No perishable food stored");
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        ulong now = world != null ? world.worldTime : 0UL;
        Block block = te != null ? te.blockValue.Block : null;
        float rate = HSSpoil.RateForBlock(block, te);
        bool split = HSFrozen.HasCompartment(block);
        float coldSide = split ? HSSpoil.CompartmentRate(te) : rate;
        float best = 999f;
        for (int i = 0; i < items.Length; i++)
        {
            ItemValue iv = HSItem.Val(items[i]);
            if (items[i] == null || items[i].IsEmpty() || !HSSpoil.IsPerishable(iv))
                continue;
            float r = split && i >= HSFrozen.CompartmentFrom ? coldSide : rate;
            float left = HSSpoil.CalendarDays(iv, now, r);
            if (left < best)
                best = left;
        }
        if (best >= 900f)
            return HSLoc.Get("hsFridgeEmptyFood", "No perishable food stored");
        return HSLoc.Format("hsFridgeOldest", "Oldest food: {0} days left", best.ToString("0.0"));
    }

    static string KeyOf(TileEntity te)
    {
        Vector3i p = te.ToWorldPos();
        return p.x + "," + p.y + "," + p.z;
    }

    class HSFridgeBox : IHSFridgeInv
    {
        readonly TileEntityPowered te;

        public HSFridgeBox(TileEntityPowered _te)
        {
            te = _te;
        }

        public ItemStack[] Items
        {
            get { return ItemsFor(te); }
        }

        public void EnsureItems()
        {
            ItemsFor(te);
        }

        public void SetModified()
        {
            if (te != null)
                te.SetModified();
        }

        public void SetUserAccessing(bool accessing)
        {
            if (te != null)
                te.SetUserAccessing(accessing);
        }

        public bool IsPowered
        {
            get { return te != null && te.IsPowered; }
        }

        public string OldestLabel()
        {
            return HSFridgeInv.OldestLabel(te, Items);
        }
    }
}
