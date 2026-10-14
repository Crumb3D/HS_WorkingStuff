using System.Collections.Generic;
using HarmonyLib;

[HarmonyPatch(typeof(BlockSwitch), "GetActivationText")]
public static class HSPatchSwitchText
{
    static void Postfix(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing, ref string __result)
    {
        try
        {
            bool on = (_blockValue.meta & 2) != 0;
            string key = KeyHint(__result, on ? "useSwitchLightOff" : "useSwitchLightOn");
            string target = ConnectedName(_world, _blockPos);
            if (string.IsNullOrEmpty(target))
            {
                __result = on
                    ? HSLoc.Format("hsSwitchOff", "{0} Switch Off", key)
                    : HSLoc.Format("hsSwitchOn", "{0} Switch On", key);
                return;
            }
            __result = on
                ? HSLoc.Format("hsSwitchTurnOff", "{0} Turn {1} Off", key, target)
                : HSLoc.Format("hsSwitchTurnOn", "{0} Turn {1} On", key, target);
        }
        catch { }
    }

    // Vanilla formats the key binding into its own template; lift it back out so we need no InControl reference.
    static string KeyHint(string vanilla, string templateKey)
    {
        if (string.IsNullOrEmpty(vanilla))
            return "";
        string template = Localization.Get(templateKey);
        int at = template != null ? template.IndexOf("{0}") : -1;
        if (at < 0)
            return "";
        string head = template.Substring(0, at);
        string tail = template.Substring(at + 3);
        if (!vanilla.StartsWith(head) || !vanilla.EndsWith(tail) || vanilla.Length < head.Length + tail.Length)
            return "";
        return vanilla.Substring(head.Length, vanilla.Length - head.Length - tail.Length);
    }

    static string ConnectedName(WorldBase world, Vector3i pos)
    {
        TileEntityPowered te = world.GetTileEntity(pos) as TileEntityPowered;
        if (te == null || te.wireDataList == null || te.wireDataList.Count == 0)
            return null;
        var names = new List<string>();
        for (int i = 0; i < te.wireDataList.Count; i++)
        {
            Block block = world.GetBlock(te.wireDataList[i]).Block;
            if (block == null)
                continue;
            string name = block.GetLocalizedBlockName();
            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                names.Add(name);
        }
        if (names.Count == 0)
            return null;
        if (names.Count == 1)
            return names[0];
        if (names.Count == 2)
            return names[0] + " & " + names[1];
        return HSLoc.Format("hsSwitchDevices", "{0} Devices", names.Count);
    }
}
