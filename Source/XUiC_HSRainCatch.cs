using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// The rain catch window: three slots for the dew gatherer, tarp and filter, plus what it holds.
// Tools stay as flags on the server; the slots only show and change those flags.
[Preserve]
public class XUiC_HSRainCatchWindowGroup : XUiController
{
    public static string ID = "hsRainCatch";
    public static Vector3i Current;

    XUiC_HSRainToolGrid grid;
    XUiV_Label info;
    float nextInfo;

    public override void Init()
    {
        base.Init();
        grid = GetChildByType<XUiC_HSRainToolGrid>();
        XUiController infoCtrl = GetChildById("rainInfo");
        info = infoCtrl != null ? infoCtrl.ViewComponent as XUiV_Label : null;
    }

    public static void Open(EntityPlayerLocal player, Vector3i pos)
    {
        if (player == null)
            return;
        Current = pos;
        player.PlayerUI.windowManager.Open(ID, true);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        string title = HSLoc.Get("hsRainCatch", "Rain Catch");
        XUiController nameCtrl = GetChildById("rainName");
        var lbl = nameCtrl != null ? nameCtrl.ViewComponent as XUiV_Label : null;
        if (lbl != null)
            lbl.Text = title;
        if (grid != null)
            grid.Bind(Current);
        XUiC_HSRainToolGrid.Active = grid;
        nextInfo = 0f;
        xui.playerUI.windowManager.Open("backpack", false);
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        if (info == null || Time.time < nextInfo)
            return;
        nextInfo = Time.time + 1f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        info.Text = HSWater.RainCatchPrompt(world, Current);
    }

    public override void OnClose()
    {
        base.OnClose();
        if (XUiC_HSRainToolGrid.Active == grid)
            XUiC_HSRainToolGrid.Active = null;
        xui.playerUI.windowManager.Close("backpack");
    }
}

[Preserve]
public class XUiC_HSRainToolGrid : XUiC_ItemStackGrid
{
    public static XUiC_HSRainToolGrid Active;

    Vector3i pos;
    ItemStack[] slots;

    // Shift-click / quick move from the bag: one dew tool into its own empty slot.
    public bool TryQuickAdd(ItemStack stack)
    {
        if (slots == null || stack == null || stack.IsEmpty())
            return false;
        ItemClass ic = HSItem.ClassOf(stack);
        int flag = ic != null ? HSWater.ModFlagFor(ic.GetItemName()) : 0;
        for (int i = 0; i < HSWater.ModFlags.Length && i < slots.Length; i++)
        {
            if (HSWater.ModFlags[i] != flag)
                continue;
            if (slots[i] != null && !slots[i].IsEmpty())
                return false;
            ItemStack one = stack.Clone();
            HSItem.SetCount(one, 1);
            slots[i] = one;
            SetStacks(slots);
            Apply();
            return true;
        }
        return false;
    }

    public override XUiC_ItemStack.StackLocationTypes StackLocation
    {
        get { return XUiC_ItemStack.StackLocationTypes.LootContainer; }
    }

    public void Bind(Vector3i p)
    {
        pos = p;
        int mods = HSWater.GetTank(p).Mods;
        slots = ItemStack.CreateArray(HSWater.ModItems.Length);
        for (int i = 0; i < HSWater.ModItems.Length; i++)
        {
            if ((mods & HSWater.ModFlags[i]) == 0)
                continue;
            ItemValue iv = ItemClass.GetItem(HSWater.ModItems[i]);
            if (iv != null && !iv.IsEmpty())
                slots[i] = new ItemStack(iv, 1);
        }
        SetStacks(slots);
    }

    public override void HandleSlotChangedEvent(int _slotNumber, ItemStack _itemStack)
    {
        if (slots == null || _slotNumber < 0 || _slotNumber >= slots.Length)
            return;
        slots[_slotNumber] = _itemStack != null ? _itemStack.Clone() : ItemStack.Empty.Clone();
        Apply();
    }

    // Anything that is not a dew tool, a second copy of one, or extra in a stack goes back to the player.
    void Apply()
    {
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        int mods = 0;
        bool bounced = false;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack s = slots[i];
            if (s == null || s.IsEmpty())
                continue;
            ItemClass ic = HSItem.ClassOf(s);
            int flag = ic != null ? HSWater.ModFlagFor(ic.GetItemName()) : 0;
            if (flag == 0 || (mods & flag) != 0)
            {
                HSWater.GiveOrDrop(player, s.Clone());
                slots[i] = ItemStack.Empty.Clone();
                bounced = true;
                continue;
            }
            int n = HSItem.Count(s);
            if (n > 1)
            {
                ItemStack extra = s.Clone();
                HSItem.SetCount(extra, n - 1);
                HSWater.GiveOrDrop(player, extra);
                HSItem.SetCount(s, 1);
                bounced = true;
            }
            mods |= flag;
        }
        if (bounced)
            SetStacks(slots);
        int had = HSWater.GetTank(pos).Mods;
        int added = mods & ~had;
        int removed = had & ~mods;
        if (added == 0 && removed == 0)
            return;
        HSWater.SetMods(pos, mods);
        if (added != 0)
            HSWorkingStuffNet.SendWater(HSWorkingStuffNet.RainInstall, pos, player, added);
        if (removed != 0)
            HSWorkingStuffNet.SendWater(HSWorkingStuffNet.RainRemove, pos, player, removed);
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), "HandleMoveToPreferredLocation")]
public static class HSPatchRainQuickMove
{
    static bool Prefix(XUiC_ItemStack __instance)
    {
        XUiC_HSRainToolGrid grid = XUiC_HSRainToolGrid.Active;
        if (grid == null || __instance == null || __instance.StackLock)
            return true;
        var from = __instance.StackLocation;
        if (from != XUiC_ItemStack.StackLocationTypes.Backpack && from != XUiC_ItemStack.StackLocationTypes.ToolBelt)
            return true;
        ItemStack stack = __instance.ItemStack;
        if (stack == null || stack.IsEmpty() || !grid.TryQuickAdd(stack))
            return true;
        int left = HSItem.Count(stack) - 1;
        ItemStack rest = ItemStack.Empty.Clone();
        if (left > 0)
        {
            rest = stack.Clone();
            HSItem.SetCount(rest, left);
        }
        __instance.PlayPlaceSound();
        __instance.ItemStack = rest;
        __instance.HandleSlotChangeEvent();
        return false;
    }
}
