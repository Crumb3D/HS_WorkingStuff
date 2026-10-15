using System.Runtime.CompilerServices;
using HarmonyLib;
using Platform;
using UnityEngine;

[HarmonyPatch(typeof(TileEntity), "InstantiateFromRead")]
public static class HSPatchFridgeLoad
{
    static void Postfix(PooledBinaryReader _br, Chunk _chunk, ref TileEntity __result)
    {
        if (__result == null || _br == null)
            return;
        if (HSGameVersion.Is33)
            return;
        Load32(_br, _chunk, ref __result);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Load32(PooledBinaryReader _br, Chunk _chunk, ref TileEntity __result)
    {
        var src = __result as TileEntityPowered;
        if (src == null || src is TileEntityHSFridge)
            return;
        Block block = null;
        try
        {
            block = src.blockValue.Block;
        }
        catch
        {
            return;
        }
        if (block == null || !(block is BlockHSFridge || block is BlockHSLaundry))
            return;
        var fridge = new TileEntityHSFridge(_chunk);
        TileEntityHSFridge.CopyPowered(src, fridge);
        fridge.ReadItems(_br);
        __result = fridge;
    }
}

[HarmonyPatch(typeof(TileEntityPowered), "write")]
public static class HSPatchFridgeWrite
{
    static void Postfix(TileEntityPowered __instance, PooledBinaryWriter _bw)
    {
        if (!HSGameVersion.Is33 || __instance == null || _bw == null)
            return;
        if (!HSFridgeInv.IsFridgeBlock(__instance))
            return;
        HSFridgeInv.WriteItems(__instance, _bw);
    }
}

// Must mirror HSPatchFridgeWrite: subclasses (TileEntityPoweredBlock) write their own fields after
// TileEntityPowered.write, so the items sit mid-record and have to be read back at the same point.
[HarmonyPatch(typeof(TileEntityPowered), "read")]
public static class HSPatchFridgeRead
{
    static void Postfix(TileEntityPowered __instance, PooledBinaryReader _br)
    {
        if (!HSGameVersion.Is33 || __instance == null || _br == null)
            return;
        Load33(__instance, _br);
    }

    static void Load33(TileEntityPowered te, PooledBinaryReader br)
    {
        if (!HSFridgeInv.IsFridgeBlock(te))
            return;
        HSFridgeInv.ReadItems(te, br);
    }
}

[HarmonyPatch(typeof(ItemStack), "AddToItemStackArray")]
public static class HSPatchStackMerge
{
    static void Prefix(ItemStack[] _itemStackArr, ItemStack _itemStack)
    {
        if (!HSWorkingStuffSettings.EnableSpoil)
            return;
        if (_itemStack == null || _itemStack.IsEmpty())
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        ulong now = world != null ? world.worldTime : 0UL;
        if (HSSpoil.IsPerishable(HSItem.Val(_itemStack)))
            HSSpoil.EnsureStamp(HSItem.Val(_itemStack), now);
        if (_itemStackArr == null)
            return;
        for (int i = 0; i < _itemStackArr.Length; i++)
        {
            ItemStack dest = _itemStackArr[i];
            if (dest == null || dest.IsEmpty())
                continue;
            if (!dest.CanStackWith(_itemStack))
                continue;
            HSSpoil.KeepOlder(HSItem.Val(dest), HSItem.Val(_itemStack));
        }
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), "GetBindingValueInternal")]
public static class HSPatchTooltip
{
    static void Postfix(XUiC_ItemStack __instance, ref string _value, string _bindingName)
    {
        if (_bindingName != "tooltip" || __instance == null)
            return;
        string extra;
        try
        {
            extra = HSWorkingStuffSettings.EnableSpoil ? HSSpoil.TooltipLine(__instance.itemStack) : null;
            if (string.IsNullOrEmpty(extra))
                extra = HSLaundry.TooltipLine(__instance.itemStack);
        }
        catch
        {
            return;
        }
        if (string.IsNullOrEmpty(extra))
            return;
        if (string.IsNullOrEmpty(_value))
            _value = extra;
        else
            _value = _value + "\n" + extra;
    }
}

[HarmonyPatch(typeof(Block), "HasBlockActivationCommands")]
public static class HSPatchSinkHasCmds
{
    static void Postfix(Block __instance, ref bool __result)
    {
        if (!HSWorkingStuffSettings.EnablePlumbing)
            return;
        if (__instance is BlockHSSink || __instance is BlockHSLaundry || __instance is BlockHSToilet)
            return;
        if (HSWater.IsSinkBlock(__instance))
            __result = true;
    }
}

[HarmonyPatch(typeof(Block), "GetBlockActivationCommands")]
public static class HSPatchSinkCmds
{
    static void Postfix(Block __instance, WorldBase _world, Vector3i _blockPos, EntityAlive _entityFocusing, ref BlockActivationCommand[] __result)
    {
        if (!HSWorkingStuffSettings.EnablePlumbing)
            return;
        if (__instance is BlockHSSink || __instance is BlockHSLaundry || __instance is BlockHSToilet)
            return;
        if (!HSWater.IsSinkBlock(__instance))
            return;
        __result = HSSinkCmds.Build(_world, _blockPos, _entityFocusing);
    }
}

[HarmonyPatch(typeof(Block), "OnBlockActivated", new[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
public static class HSPatchSinkUse
{
    static bool Prefix(Block __instance, string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player, ref bool __result)
    {
        if (!HSWorkingStuffSettings.EnablePlumbing)
            return true;
        if (__instance is BlockHSSink || __instance is BlockHSLaundry || __instance is BlockHSToilet)
            return true;
        if (!HSWater.IsSinkBlock(__instance))
            return true;
        if (!HSSinkCmds.Run(_commandName, _blockPos, _player))
            return true;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(Block), "GetActivationText")]
public static class HSPatchSinkText
{
    static void Postfix(Block __instance, WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, ref string __result)
    {
        if (!HSWorkingStuffSettings.EnablePlumbing)
            return;
        if (__instance is BlockHSSink || __instance is BlockHSLaundry || __instance is BlockHSToilet)
            return;
        if (!HSWater.IsSinkBlock(__instance))
            return;
        World world = _world as World;
        if (world == null)
            return;
        string extra = HSWater.SinkPrompt(world, _blockPos);
        if (string.IsNullOrEmpty(__result))
            __result = extra;
        else
            __result = __result + "\n" + extra;
    }
}

[HarmonyPatch(typeof(XUiC_MainMenuButtons), "Init")]
public static class HSPatchConnectToIp
{
    static void Postfix(XUiC_MainMenuButtons __instance)
    {
        HideNewsUi(__instance);
        XUiC_Button btn;
        if (!__instance.TryGetChildByIdAndType("btnConnectToIp", out btn) || btn == null)
            return;
        if (!HSWorkingStuffSettings.EnableConnectToIp)
        {
            if (btn.ViewComponent != null)
                btn.ViewComponent.IsVisible = false;
            return;
        }
        btn.OnPress -= OnConnectToIp;
        btn.OnPress += OnConnectToIp;
    }

    static void HideNewsUi(XUiC_MainMenuButtons menu)
    {
        if (menu == null || !HSWorkingStuffSettings.HideNews)
            return;
        XUiC_Button newsBtn;
        if (menu.TryGetChildByIdAndType("btnNews", out newsBtn) && newsBtn != null && newsBtn.ViewComponent != null)
            newsBtn.ViewComponent.IsVisible = false;
        XUiC_NewsWindow[] boxes = menu.GetChildControllers<XUiC_NewsWindow>("");
        if (boxes != null)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] != null && boxes[i].ViewComponent != null)
                    boxes[i].ViewComponent.IsVisible = false;
            }
        }
        XUiC_Button play;
        bool isMainButtons = menu.TryGetChildByIdAndType("btnPlayGame", out play) && play != null;
        if (!isMainButtons && menu.ViewComponent != null && boxes != null && boxes.Length > 0)
            menu.ViewComponent.IsVisible = false;
    }

    static void OnConnectToIp(XUiController sender, int mouseButton)
    {
        if (sender == null || sender.xui == null)
            return;
        XUi xui = sender.xui;
        if (!GameManager.HasAcceptedLatestEula())
        {
            XUiC_EulaWindow.Open(xui);
            return;
        }
        XUiC_MultiplayerPrivilegeNotification.ResolvePrivilegesWithDialog(EUserPerms.Multiplayer, delegate (bool ok)
        {
            if (!ok)
                return;
            XUiC_ServerBrowserDirectConnect.Open(xui, delegate
            {
                if (xui.playerUI != null && sender.windowGroup != null)
                    xui.playerUI.windowManager.Close(sender.windowGroup);
            }, null);
        });
    }
}

[HarmonyPatch(typeof(XUiC_MainMenu), "Open")]
public static class HSPatchHideNewsSplash
{
    static void Prefix()
    {
        if (!HSWorkingStuffSettings.HideNews)
            return;
        var f = AccessTools.Field(typeof(XUiC_MainMenu), "shownNewsScreenOnce");
        if (f != null)
            f.SetValue(null, true);
    }
}

[HarmonyPatch(typeof(XUiC_NewsWindow), "get_NewsCount")]
public static class HSPatchHideNewsCount
{
    static void Postfix(ref int __result)
    {
        if (HSWorkingStuffSettings.HideNews)
            __result = 0;
    }
}

[HarmonyPatch(typeof(XUiC_NewsWindow), "OnOpen")]
public static class HSPatchHideNewsWindow
{
    static void Postfix(XUiC_NewsWindow __instance)
    {
        if (!HSWorkingStuffSettings.HideNews || __instance == null)
            return;
        if (__instance.ViewComponent != null)
            __instance.ViewComponent.IsVisible = false;
        XUiController parent = __instance.Parent;
        if (parent != null && parent.ViewComponent != null)
            parent.ViewComponent.IsVisible = false;
    }
}
