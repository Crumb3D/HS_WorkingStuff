using UnityEngine.Scripting;

[Preserve]
public class XUiC_HSStorageWindowGroup : XUiController
{
    public static string ID = "hsWorkingStorage";
    public static IHSFridgeInv Current;
    static string title;
    static bool split;

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
        Open(player, te, null);
    }

    public static void Open(EntityPlayerLocal player, IHSFridgeInv te, string windowTitle)
    {
        Open(player, te, windowTitle, false);
    }

    public static void Open(EntityPlayerLocal player, IHSFridgeInv te, string windowTitle, bool freezerRows)
    {
        if (player == null || te == null)
            return;
        Current = te;
        title = windowTitle;
        split = freezerRows;
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
        foreach (string id in new[] { "freezerLine", "freezerTint" })
        {
            XUiController c = GetChildById(id);
            if (c != null && c.ViewComponent != null)
                c.ViewComponent.IsVisible = split;
        }
        if (grid != null)
            grid.Bind(Current);
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
