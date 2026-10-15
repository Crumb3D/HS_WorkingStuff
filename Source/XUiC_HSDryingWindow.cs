using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class XUiC_HSDryingWindowGroup : XUiController
{
    public static string ID = "hsDrying";
    public static TileEntity Current;
    static string title;

    XUiC_HSDryingGrid wetGrid;
    XUiC_HSDryingGrid dryGrid;
    XUiC_WindowNonPagingHeader header;
    float nextRefresh;

    public override void Init()
    {
        base.Init();
        XUiC_HSDryingGrid[] grids = GetChildrenByType<XUiC_HSDryingGrid>();
        if (grids != null)
        {
            for (int i = 0; i < grids.Length; i++)
            {
                if (grids[i] != null && grids[i].ViewComponent != null && grids[i].ViewComponent.ID == "dry")
                    dryGrid = grids[i];
                else if (wetGrid == null)
                    wetGrid = grids[i];
            }
        }
        header = GetChildByType<XUiC_WindowNonPagingHeader>();
    }

    public static void Open(EntityPlayerLocal player, TileEntity te, string windowTitle)
    {
        if (player == null || te == null)
            return;
        Current = te;
        title = windowTitle;
        te.SetUserAccessing(true);
        player.PlayerUI.windowManager.Open(ID, true);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        string shown = string.IsNullOrEmpty(title) ? HSLoc.Get("hsDryingWindow", "Drying") : title;
        if (header != null)
            header.SetHeader(shown);
        XUiController nameCtrl = GetChildById("lootName");
        if (nameCtrl != null)
        {
            var lbl = nameCtrl.ViewComponent as XUiV_Label;
            if (lbl != null)
                lbl.Text = shown;
        }
        Bind();
        xui.playerUI.windowManager.Open("backpack", false);
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        if (Time.time < nextRefresh)
            return;
        nextRefresh = Time.time + 1f;
        Bind();
    }

    void Bind()
    {
        ItemStack[] stacks = HSInv.GetStorageStacks(Current);
        int half = HSDrying.WetSlots(stacks);
        if (wetGrid != null)
            wetGrid.Bind(Current, 0, half);
        if (dryGrid != null)
            dryGrid.Bind(Current, half, half);
    }

    public override void OnClose()
    {
        base.OnClose();
        xui.playerUI.windowManager.Close("backpack");
        if (Current != null)
        {
            Current.SetUserAccessing(false);
            HSInv.SetModified(Current);
            Current = null;
        }
    }
}

[Preserve]
public class XUiC_HSDryingGrid : XUiC_ItemStackGrid
{
    TileEntity te;
    int offset;
    int view;

    public override XUiC_ItemStack.StackLocationTypes StackLocation
    {
        get { return XUiC_ItemStack.StackLocationTypes.LootContainer; }
    }

    public void Bind(TileEntity tile, int from, int count)
    {
        te = tile;
        offset = from;
        view = count;
        ItemStack[] live = GetSlots();
        int cells = live != null && live.Length > 0 ? live.Length : (view > 0 ? view : 0);
        var shown = new ItemStack[cells];
        ItemStack[] stacks = HSInv.GetStorageStacks(te);
        for (int i = 0; i < cells; i++)
        {
            int src = offset + i;
            shown[i] = i < view && stacks != null && src < stacks.Length && stacks[src] != null
                ? stacks[src].Clone()
                : ItemStack.Empty.Clone();
        }
        SetStacks(shown);
    }

    public override void HandleSlotChangedEvent(int _slotNumber, ItemStack _itemStack)
    {
        if (te == null || _slotNumber < 0 || _slotNumber >= view)
            return;
        ItemStack[] stacks = HSInv.GetStorageStacks(te);
        int i = offset + _slotNumber;
        if (stacks == null || i < 0 || i >= stacks.Length)
            return;
        HSInv.SetStorageSlot(te, i, _itemStack != null ? _itemStack.Clone() : ItemStack.Empty.Clone());
    }
}
