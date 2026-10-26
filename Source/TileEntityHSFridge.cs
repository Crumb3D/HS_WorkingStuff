using System.Reflection;
using HarmonyLib;

public class TileEntityHSFridge : TileEntityPoweredBlock, IHSFridgeInv
{
    public ItemStack[] Items
    {
        get { return items; }
    }
    public const int Magic = 0x48534631;
    // Two compartments of ViewSlots: the fridge, then the fridge-freezer's freezer (unused by other fridges).
    public const int ViewSlots = 24;
    public const int SlotCount = ViewSlots * 2;

    public ItemStack[] items;

    public TileEntityHSFridge(Chunk _chunk)
        : base(_chunk)
    {
        items = ItemStack.CreateArray(SlotCount);
        PowerItemType = PowerItem.PowerItemTypes.Consumer;
    }

    public void EnsureItems()
    {
        if (items == null || items.Length != SlotCount)
            items = ItemStack.CreateArray(SlotCount);
    }

    public void TickSpoil(float rate, ulong now)
    {
        EnsureItems();
        HSSpoil.TickArray(items, rate, now);
        SetModified();
    }

    public override void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)
    {
        base.read(_br, _eStreamMode);
        ReadItems(_br);
    }

    public override void write(PooledBinaryWriter _bw, StreamModeWrite _eStreamMode)
    {
        base.write(_bw, _eStreamMode);
        WriteItems(_bw);
    }

    public void ReadItems(PooledBinaryReader _br)
    {
        EnsureItems();
        if (_br == null || _br.BaseStream == null)
            return;
        if (_br.BaseStream.Position + 8 > _br.BaseStream.Length)
            return;
        int magic = _br.ReadInt32();
        if (magic != Magic)
            return;
        int n = _br.ReadInt32();
        if (n < 0 || n > 64)
            return;
        for (int i = 0; i < n && i < items.Length; i++)
            HSItem.Read(items[i], _br);
    }

    public void WriteItems(PooledBinaryWriter _bw)
    {
        EnsureItems();
        _bw.Write(Magic);
        _bw.Write(items.Length);
        for (int i = 0; i < items.Length; i++)
            HSItem.Write(items[i], _bw);
    }

    public static void CopyPowered(TileEntityPowered src, TileEntityHSFridge dest)
    {
        if (src == null || dest == null)
            return;
        dest.localChunkPos = src.localChunkPos;
        dest.SetChunk(src.GetChunk());
        dest.IsPlayerPlaced = src.IsPlayerPlaced;
        dest.PowerItemType = src.PowerItemType;
        CopyField(src, dest, "wireDataList");
        CopyField(src, dest, "parentPosition");
        CopyField(src, dest, "isPowered");
        CopyField(src, dest, "isToggled");
        CopyField(src, dest, "PowerItem");
    }

    static void CopyField(object src, object dest, string name)
    {
        FieldInfo f = AccessTools.Field(src.GetType(), name);
        if (f == null)
            f = AccessTools.Field(typeof(TileEntityPowered), name);
        if (f == null)
            f = AccessTools.Field(typeof(TileEntityPoweredBlock), name);
        if (f == null)
            return;
        FieldInfo d = AccessTools.Field(dest.GetType(), name);
        if (d == null)
            d = AccessTools.Field(typeof(TileEntityPowered), name);
        if (d == null)
            d = AccessTools.Field(typeof(TileEntityPoweredBlock), name);
        if (d == null || d.IsLiteral)
            return;
        d.SetValue(dest, f.GetValue(src));
    }

    public string OldestLabel()
    {
        EnsureItems();
        return HSFridgeInv.OldestLabel(this, items);
    }
}
