using System;
using System.Reflection;

// 3.2 ItemStack.itemValue/count are fields. 3.3 they are properties over
// itemValueField/countField. Resolve at runtime so 3.2 IL is never required.
public static class HSItem
{
    static bool resolved;
    static PropertyInfo ivProp;
    static FieldInfo ivField;
    static PropertyInfo countProp;
    static FieldInfo countField;
    static MethodInfo readPooled;
    static MethodInfo readBin;
    static MethodInfo writePooled;
    static MethodInfo writeBin;

    static void Ensure()
    {
        if (resolved)
            return;
        resolved = true;
        Type t = typeof(ItemStack);
        ivProp = t.GetProperty("itemValue", BindingFlags.Public | BindingFlags.Instance);
        if (ivProp != null && ivProp.GetGetMethod() == null)
            ivProp = null;
        ivField = t.GetField("itemValue", BindingFlags.Public | BindingFlags.Instance);
        if (ivField == null)
            ivField = t.GetField("itemValueField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        countProp = t.GetProperty("count", BindingFlags.Public | BindingFlags.Instance);
        if (countProp != null && countProp.GetGetMethod() == null)
            countProp = null;
        countField = t.GetField("count", BindingFlags.Public | BindingFlags.Instance);
        if (countField == null)
            countField = t.GetField("countField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        readPooled = t.GetMethod("Read", new[] { typeof(PooledBinaryReader) });
        readBin = t.GetMethod("Read", new[] { typeof(System.IO.BinaryReader) });
        writePooled = t.GetMethod("Write", new[] { typeof(PooledBinaryWriter) });
        writeBin = t.GetMethod("Write", new[] { typeof(System.IO.BinaryWriter) });
    }

    // ItemValue.Meta: 3.2 field, 3.3 property over meta.
    static MemberInfo metaMember;

    public static int Meta(ItemValue iv)
    {
        if (iv == null)
            return 0;
        if (metaMember == null)
            metaMember = (MemberInfo)typeof(ItemValue).GetProperty("Meta", BindingFlags.Public | BindingFlags.Instance)
                ?? typeof(ItemValue).GetField("Meta", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo p = metaMember as PropertyInfo;
        if (p != null)
            return (int)p.GetValue(iv, null);
        FieldInfo f = metaMember as FieldInfo;
        return f != null ? (int)f.GetValue(iv) : 0;
    }

    public static ItemValue Val(ItemStack stack)
    {
        if (stack == null)
            return null;
        Ensure();
        if (ivProp != null)
            return ivProp.GetValue(stack, null) as ItemValue;
        if (ivField != null)
            return ivField.GetValue(stack) as ItemValue;
        return null;
    }

    public static void SetVal(ItemStack stack, ItemValue value)
    {
        if (stack == null)
            return;
        Ensure();
        if (ivProp != null)
        {
            ivProp.SetValue(stack, value, null);
            return;
        }
        if (ivField != null)
            ivField.SetValue(stack, value);
    }

    public static int Count(ItemStack stack)
    {
        if (stack == null)
            return 0;
        Ensure();
        if (countProp != null)
            return (int)countProp.GetValue(stack, null);
        if (countField != null)
            return (int)countField.GetValue(stack);
        return 0;
    }

    public static void SetCount(ItemStack stack, int count)
    {
        if (stack == null)
            return;
        Ensure();
        if (countProp != null)
        {
            countProp.SetValue(stack, count, null);
            return;
        }
        if (countField != null)
            countField.SetValue(stack, count);
    }

    public static ItemClass ClassOf(ItemStack stack)
    {
        ItemValue iv = Val(stack);
        return iv != null ? iv.ItemClass : null;
    }

    public static void Read(ItemStack stack, PooledBinaryReader br)
    {
        if (stack == null || br == null)
            return;
        Ensure();
        if (readPooled != null)
        {
            readPooled.Invoke(stack, new object[] { br });
            return;
        }
        if (readBin != null)
            readBin.Invoke(stack, new object[] { br });
    }

    public static void Write(ItemStack stack, PooledBinaryWriter bw)
    {
        if (stack == null || bw == null)
            return;
        Ensure();
        if (writePooled != null)
        {
            writePooled.Invoke(stack, new object[] { bw });
            return;
        }
        if (writeBin != null)
            writeBin.Invoke(stack, new object[] { bw });
    }
}
