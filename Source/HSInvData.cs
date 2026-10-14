using System;
using System.Reflection;

// 3.2 ItemInventoryData.holdingEntity/world are fields. 3.3 they are properties
// (holdingEntity => entity, world => GameManager.Instance.World). Resolve at
// runtime so a 3.2-built DLL never emits field IL that breaks on 3.3.
public static class HSInvData
{
    static bool resolved;
    static PropertyInfo holdProp;
    static FieldInfo holdField;
    static FieldInfo entityField;
    static PropertyInfo worldProp;
    static FieldInfo worldField;

    static void Ensure()
    {
        if (resolved)
            return;
        resolved = true;
        Type t = typeof(ItemInventoryData);
        holdProp = t.GetProperty("holdingEntity", BindingFlags.Public | BindingFlags.Instance);
        if (holdProp != null && holdProp.GetGetMethod() == null)
            holdProp = null;
        holdField = t.GetField("holdingEntity", BindingFlags.Public | BindingFlags.Instance);
        entityField = t.GetField("entity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        worldProp = t.GetProperty("world", BindingFlags.Public | BindingFlags.Instance);
        if (worldProp != null && worldProp.GetGetMethod() == null)
            worldProp = null;
        worldField = t.GetField("world", BindingFlags.Public | BindingFlags.Instance);
    }

    public static EntityAlive HoldingEntity(ItemInventoryData data)
    {
        if (data == null)
            return null;
        Ensure();
        if (holdProp != null)
            return holdProp.GetValue(data, null) as EntityAlive;
        if (holdField != null)
            return holdField.GetValue(data) as EntityAlive;
        if (entityField != null)
            return entityField.GetValue(data) as EntityAlive;
        return null;
    }

    public static World WorldOf(ItemInventoryData data)
    {
        if (data == null)
            return null;
        Ensure();
        if (worldProp != null)
            return worldProp.GetValue(data, null) as World;
        if (worldField != null)
            return worldField.GetValue(data) as World;
        return GameManager.Instance != null ? GameManager.Instance.World : null;
    }
}
