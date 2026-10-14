using HarmonyLib;
using UnityEngine;

// A consumer's RequiredPower is what the generator pays for while it is powered.
// Idle appliances ask for 0 W; HandlePowerReceived still marks them powered when the
// circuit reaches them, so "is there power here?" keeps working with no draw.
public static class HSPowerDemand
{
    public static ushort Watts(Block block, ushort fallback)
    {
        int w = fallback;
        if (block != null && block.Properties != null)
            block.Properties.ParseInt("RequiredPower", ref w);
        if (w < 0)
            w = 0;
        if (w > ushort.MaxValue)
            w = ushort.MaxValue;
        return (ushort)w;
    }

    public static void Set(Vector3i pos, ushort watts)
    {
        if (!HSWorkingStuffNet.IsAuthority || PowerManager.Instance == null)
            return;
        PowerItem item;
        try
        {
            item = PowerManager.Instance.GetPowerItemByWorldPos(pos);
        }
        catch
        {
            return;
        }
        if (item == null || item.RequiredPower == watts)
            return;
        item.RequiredPower = watts;
        item.SendHasLocalChangesToRoot();
    }

    public static bool OnDemand(ushort blockId)
    {
        Block[] list = Block.list;
        if (list == null || blockId >= list.Length)
            return false;
        Block b = list[blockId];
        return b is BlockHSHeater || b is BlockHSStove || b is BlockHSTV;
    }

    public static bool CircuitLive(Vector3i pos)
    {
        if (PowerManager.Instance == null)
            return false;
        try
        {
            PowerItem item = PowerManager.Instance.GetPowerItemByWorldPos(pos);
            return item != null && item.IsPowered;
        }
        catch
        {
            return false;
        }
    }
}

// Power items are rebuilt from the block's RequiredPower on placement and on every load,
// so on-demand appliances start idle until their tick asks for power.
[HarmonyPatch(typeof(PowerConsumer), "SetValuesFromBlock")]
public static class HSPatchIdleDemand
{
    static void Postfix(PowerConsumer __instance)
    {
        if (__instance != null && HSPowerDemand.OnDemand(__instance.BlockID))
            __instance.RequiredPower = 0;
    }
}
