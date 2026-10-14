using HarmonyLib;

// Gas grill burns a gas can for much longer than a campfire does (vanilla: 2 seconds per can).
[HarmonyPatch(typeof(TileEntityWorkstation), "GetFuelTime")]
public static class HSPatchGasGrillFuel
{
    public const string GasGrillName = "hsGasGrill";

    static void Postfix(TileEntityWorkstation __instance, ItemStack _fuel, ref float __result)
    {
        if (__result <= 0f || _fuel == null)
            return;
        ItemValue iv = HSItem.Val(_fuel);
        ItemClass item = iv != null ? iv.ItemClass : null;
        if (item == null || item.GetItemName() != "ammoGasCan")
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        if (world.GetBlock(__instance.ToWorldPos()).Block.GetBlockName() != GasGrillName)
            return;
        __result = HSWorkingStuffSettings.GasCanGrillSeconds;
    }
}
