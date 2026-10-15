using Audio;
using HarmonyLib;
using UnityEngine.Scripting;

[Preserve]
public class XUiC_HSStorageGrid : XUiC_ItemStackGrid
{
    IHSFridgeInv fridge;
    int offset;

    public override XUiC_ItemStack.StackLocationTypes StackLocation
    {
        get { return XUiC_ItemStack.StackLocationTypes.LootContainer; }
    }

    public void Bind(IHSFridgeInv te)
    {
        Bind(te, 0);
    }

    // Shows one compartment: ViewSlots stacks starting at from.
    public void Bind(IHSFridgeInv te, int from)
    {
        fridge = te;
        offset = from;
        if (fridge == null)
            return;
        fridge.EnsureItems();
        KickJunk();
        SetStacks(HSFridgeInv.Slice(fridge.Items, offset, TileEntityHSFridge.ViewSlots));
    }

    public override void HandleSlotChangedEvent(int _slotNumber, ItemStack _itemStack)
    {
        if (fridge == null || fridge.Items == null)
            return;
        int i = offset + _slotNumber;
        if (_slotNumber < 0 || _slotNumber >= TileEntityHSFridge.ViewSlots || i >= fridge.Items.Length)
            return;
        if (!HSSpoil.CanFridge(_itemStack))
        {
            Bounce(_itemStack);
            SetStacks(HSFridgeInv.Slice(fridge.Items, offset, TileEntityHSFridge.ViewSlots));
            return;
        }
        fridge.Items[i] = _itemStack != null ? _itemStack.Clone() : ItemStack.Empty.Clone();
        fridge.SetModified();
    }

    void KickJunk()
    {
        EntityPlayerLocal player = Player();
        if (player == null || fridge.Items == null)
            return;
        bool any = false;
        int last = System.Math.Min(offset + TileEntityHSFridge.ViewSlots, fridge.Items.Length);
        for (int i = offset; i < last; i++)
        {
            ItemStack s = fridge.Items[i];
            if (s == null || s.IsEmpty() || HSSpoil.CanFridge(s))
                continue;
            if (!HSInv.AddToPlayer(player, s.Clone()))
                continue;
            fridge.Items[i] = ItemStack.Empty.Clone();
            any = true;
        }
        if (any)
        {
            fridge.SetModified();
            Refuse(player);
        }
    }

    void Bounce(ItemStack stack)
    {
        EntityPlayerLocal player = Player();
        if (player != null && stack != null && !stack.IsEmpty())
            HSInv.AddToPlayer(player, stack.Clone());
        Refuse(player);
    }

    EntityPlayerLocal Player()
    {
        return xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
    }

    public static void Refuse(EntityPlayerLocal player)
    {
        if (player != null)
            GameManager.ShowTooltip(player, HSLoc.Get("hsFridgeFoodOnly", "Only food that spoils goes in here"));
        Manager.PlayInsidePlayerHead("ui_denied");
    }

    public static bool IsFoodSlot(XUiC_ItemStack slot)
    {
        return slot != null && slot.GetParentByType<XUiC_HSStorageGrid>() != null;
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), "CanSwap")]
public static class HSPatchFridgeCanSwap
{
    static void Postfix(XUiC_ItemStack __instance, ItemStack _stack, ref bool __result)
    {
        if (!__result || !XUiC_HSStorageGrid.IsFoodSlot(__instance))
            return;
        if (!HSSpoil.CanFridge(_stack))
            __result = false;
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), "SwapItem")]
public static class HSPatchFridgeSwap
{
    static bool Prefix(XUiC_ItemStack __instance)
    {
        if (!XUiC_HSStorageGrid.IsFoodSlot(__instance) || __instance.xui == null || __instance.xui.DragAndDropWindow == null)
            return true;
        ItemStack cur = __instance.xui.DragAndDropWindow.CurrentStack;
        if (HSSpoil.CanFridge(cur))
            return true;
        XUiC_HSStorageGrid.Refuse(__instance.xui.playerUI != null ? __instance.xui.playerUI.entityPlayer : null);
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), "HandleDropOne")]
public static class HSPatchFridgeDropOne
{
    static bool Prefix(XUiC_ItemStack __instance)
    {
        if (!XUiC_HSStorageGrid.IsFoodSlot(__instance) || __instance.xui == null || __instance.xui.DragAndDropWindow == null)
            return true;
        ItemStack cur = __instance.xui.DragAndDropWindow.CurrentStack;
        if (HSSpoil.CanFridge(cur))
            return true;
        XUiC_HSStorageGrid.Refuse(__instance.xui.playerUI != null ? __instance.xui.playerUI.entityPlayer : null);
        return false;
    }
}

