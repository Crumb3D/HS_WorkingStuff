using UnityEngine.Scripting;

[Preserve]
public class XUiC_HSStorageWindowGroup : XUiController
{
    public static string ID = "hsWorkingStorage";
    public static IHSFridgeInv Current;
    static string title;
    static int from;

    XUiC_HSStorageGrid grid;
    XUiC_WindowNonPagingHeader header;

    public override void Init()
    {
        base.Init();
        grid = GetChildByType<XUiC_HSStorageGrid>();
        header = GetChildByType<XUiC_WindowNonPagingHeader>();
    }

    public static void Open(EntityPlayerLocal player, IHSFridgeInv te)
    {
        Open(player, te, null, 0);
    }

    public static void Open(EntityPlayerLocal player, IHSFridgeInv te, string windowTitle)
    {
        Open(player, te, windowTitle, 0);
    }

    // firstSlot picks the compartment: 0 the fridge, ViewSlots the fridge-freezer's freezer.
    public static void Open(EntityPlayerLocal player, IHSFridgeInv te, string windowTitle, int firstSlot)
    {
        if (player == null || te == null)
            return;
        Current = te;
        title = windowTitle;
        from = firstSlot;
        te.SetUserAccessing(true);
        player.PlayerUI.windowManager.Open(ID, true);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        string shown = string.IsNullOrEmpty(title) ? HSLoc.Get("hsFridgeWindow", "Fridge") : title;
        if (header != null)
            header.SetHeader(shown);
        XUiController nameCtrl = GetChildById("lootName");
        if (nameCtrl != null)
        {
            var lbl = nameCtrl.ViewComponent as XUiV_Label;
            if (lbl != null)
                lbl.Text = shown;
        }
        if (grid != null)
            grid.Bind(Current, from);
        xui.playerUI.windowManager.Open("backpack", false);
    }

    public override void OnClose()
    {
        base.OnClose();
        xui.playerUI.windowManager.Close("backpack");
        if (Current != null)
        {
            Current.SetUserAccessing(false);
            Current.SetModified();
            Current = null;
        }
    }
}
