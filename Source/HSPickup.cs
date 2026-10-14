using System.Text;

public static class HSPickup
{
    static string pendingInv;
    static string pendingWater;
    static string pendingMods;
    static string pendingFuel;
    static string pendingJars;

    public static void CaptureFridge(IHSFridgeInv te)
    {
        pendingInv = null;
        if (te == null)
            return;
        te.EnsureItems();
        pendingInv = EncodeStacks(te.Items);
    }

    public static void RestoreFridge(IHSFridgeInv te, PlatformUserIdentifierAbs player)
    {
        if (te == null)
            return;
        if (!string.IsNullOrEmpty(pendingInv))
        {
            DecodeStacks(pendingInv, te.Items);
            pendingInv = null;
            te.SetModified();
        }
    }

    public static void CaptureWater(int units, bool clean)
    {
        pendingWater = units + "," + (clean ? "1" : "0");
    }

    public static void CaptureCollector(TileEntityCollector te)
    {
        pendingMods = null;
        pendingFuel = null;
        pendingJars = null;
        if (te == null)
            return;
        pendingMods = EncodeStacks(te.ModSlots);
        pendingFuel = EncodeStacks(te.FuelSlots);
        pendingJars = EncodeStacks(te.Items);
    }

    public static void RestoreCollector(TileEntityCollector te)
    {
        if (te == null)
            return;
        if (!string.IsNullOrEmpty(pendingMods))
        {
            ItemStack[] mods = ItemStack.CreateArray(3);
            DecodeStacks(pendingMods, mods);
            te.SetModSlots(mods);
            pendingMods = null;
        }
        if (!string.IsNullOrEmpty(pendingFuel) && te.FuelSlots != null)
        {
            DecodeStacks(pendingFuel, te.FuelSlots);
            pendingFuel = null;
        }
        if (!string.IsNullOrEmpty(pendingJars) && te.Items != null)
        {
            DecodeStacks(pendingJars, te.Items);
            pendingJars = null;
        }
        te.SetModified();
    }

    public static bool TakePendingWater(out int units, out bool clean)
    {
        units = 0;
        clean = true;
        if (string.IsNullOrEmpty(pendingWater))
            return false;
        DecodeWater(pendingWater, out units, out clean);
        pendingWater = null;
        return true;
    }

    public static string EncodeStacks(ItemStack[] items)
    {
        if (items == null)
            return "";
        var sb = new StringBuilder();
        for (int i = 0; i < items.Length; i++)
        {
            ItemStack s = items[i];
            ItemValue iv = HSItem.Val(s);
            if (s == null || s.IsEmpty() || iv == null || iv.ItemClass == null)
                continue;
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(iv.ItemClass.Name).Append(',').Append(HSItem.Count(s)).Append(',')
                .Append(HSSpoil.GetAcc(iv).ToString("0.##"));
        }
        return sb.ToString();
    }

    public static void DecodeStacks(string data, ItemStack[] dest)
    {
        if (dest == null || string.IsNullOrEmpty(data))
            return;
        string[] parts = data.Split(';');
        int slot = 0;
        for (int i = 0; i < parts.Length && slot < dest.Length; i++)
        {
            string[] f = parts[i].Split(',');
            if (f.Length < 2)
                continue;
            ItemValue iv = ItemClass.GetItem(f[0]);
            if (iv == null || iv.IsEmpty())
                continue;
            int count;
            if (!int.TryParse(f[1], out count))
                count = 1;
            float acc = 0f;
            if (f.Length > 2)
                float.TryParse(f[2], out acc);
            dest[slot] = new ItemStack(iv, count);
            ItemValue stored = HSItem.Val(dest[slot]);
            if (stored != null)
                stored.SetMetadata(HSSpoil.MetaAcc, acc);
            slot++;
        }
    }

    static void DecodeWater(string data, out int units, out bool clean)
    {
        units = 0;
        clean = true;
        if (string.IsNullOrEmpty(data))
            return;
        string[] f = data.Split(',');
        if (f.Length > 0)
            int.TryParse(f[0], out units);
        if (f.Length > 1)
            clean = f[1] != "0";
    }
}
