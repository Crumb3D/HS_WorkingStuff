using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// Like the lift and escalator tools: pick a water system (New Water System / Use Water System Here), then
// New Connection on each thing to pipe to it. Left click uses the system here, right click is New Connection.
[Preserve]
public class ItemActionHSPipeTool : ItemAction
{
    public static bool IsHolding(EntityPlayerLocal player)
    {
        if (player == null || player.inventory == null)
            return false;
        ItemClass holding = player.inventory.holdingItem;
        if (holding == null || holding.Actions == null)
            return false;
        foreach (ItemAction a in holding.Actions)
        {
            if (a is ItemActionHSPipeTool)
                return true;
        }
        return false;
    }

    public override bool HasRadial()
    {
        return true;
    }

    public override string CanInteract(ItemActionData _actionData)
    {
        return Localization.Get("hsPipeToolHint");
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (!_bReleased)
            return;
        try
        {
            var player = _actionData != null ? HSInvData.HoldingEntity(_actionData.invData) as EntityPlayerLocal : null;
            if (player == null)
                return;
            HSPipeTool.Tell(player, _actionData.indexInEntityOfAction == 0 ? HSPipeTool.UseSystem(player) : HSPipeTool.NewConnection(player));
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] pipe tool click: " + e.Message);
        }
    }

    public static void OpenRadial(EntityPlayerLocal player)
    {
        if (player == null || player.playerUI == null || player.playerUI.xui == null)
            return;
        XUiC_Radial radial = player.playerUI.xui.RadialWindow;
        if (radial == null || radial.IsOpen)
            return;
        radial.Open();
        ItemClass holding = player.inventory.holdingItem;
        if (holding == null || holding.Actions == null)
            return;
        foreach (ItemAction a in holding.Actions)
        {
            if (!(a is ItemActionHSPipeTool))
                continue;
            a.SetupRadial(radial, player);
            return;
        }
    }

    public override void SetupRadial(XUiC_Radial radial, EntityPlayerLocal player)
    {
        try
        {
            radial.ResetRadialEntries();
            Color white = Color.white;
            Color grey = new Color(0.4f, 0.4f, 0.4f, 1f);
            bool canLay = HSPipeTool.CanLay(player);
            bool hasSystem = HSPipeTool.HasSystem;
            radial.CreateRadialEntry(0, "ui_game_symbol_add", white, "UIAtlas", "", Localization.Get("hsPipeRadialNewSystem"));
            radial.CreateRadialEntry(1, "ui_game_symbol_map", white, "UIAtlas", "", Localization.Get("hsPipeRadialUseSystem"));
            radial.CreateRadialEntry(2, "ui_game_symbol_map_cursor", hasSystem ? white : grey, "UIAtlas", "", Localization.Get("hsPipeRadialNew"));
            radial.CreateRadialEntry(3, "ui_game_symbol_assemble", canLay ? white : grey, "UIAtlas", "", Localization.Get("hsPipeRadialLay"));
            radial.CreateRadialEntry(4, "ui_game_symbol_wrench", white, "UIAtlas", "", Localization.Get("hsPipeRadialTakeUp"));
            radial.CreateRadialEntry(5, "ui_game_symbol_trash", white, "UIAtlas", "", Localization.Get("hsPipeRadialTakeStretch"));
            radial.SetCommonData(default(GUI_2.UIUtils.ButtonIcon), HandleCommand,
                new XUiC_Radial.RadialContextHoldingSlotIndex(player.inventory.holdingItemIdx), -1, false, StillHolding);
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] pipe tool radial: " + e.Message);
        }
    }

    static bool StillHolding(XUiC_Radial radial, XUiC_Radial.RadialContextAbs context)
    {
        var ctx = context as XUiC_Radial.RadialContextHoldingSlotIndex;
        var player = radial != null && radial.xui != null && radial.xui.playerUI != null ? radial.xui.playerUI.entityPlayer : null;
        if (player == null || ctx == null || player.inventory.holdingItemIdx != ctx.ItemSlotIndex)
            return false;
        return IsHolding(player);
    }

    static void HandleCommand(XUiC_Radial sender, int commandIndex, XUiC_Radial.RadialContextAbs context)
    {
        try
        {
            EntityPlayerLocal player = sender.xui.playerUI.entityPlayer;
            string msg;
            switch (commandIndex)
            {
                case 0: msg = HSPipeTool.NewSystem(player); break;
                case 1: msg = HSPipeTool.UseSystem(player); break;
                case 2: msg = HSPipeTool.NewConnection(player); break;
                case 3: msg = HSPipeTool.Lay(player); break;
                case 4: msg = HSPipeTool.TakeUp(player); break;
                case 5: msg = HSPipeTool.TakeUpStretch(player); break;
                default: return;
            }
            HSPipeTool.Tell(player, msg);
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] pipe tool command: " + e.Message);
        }
    }
}

public static class HSPipeTool
{
    const int StepCost = 10;
    const int TurnCost = 6;
    // Pipe never crosses open air: it runs on a floor, wall or ceiling, or hidden inside full blocks.
    // Hidden costs more, so it shows along the surface and only cuts through a wall or floor to save a detour.
    const int InBlockCost = 12;
    const int WallOnlyCost = 3;
    const int SidePortCost = 8;
    // Radiator runs go under the floor and stay in the floor layer instead of climbing walls.
    const int UnderVertCost = 30;
    const int MaxDrop = 16;
    const int OffWallCost = 4;

    public static readonly string[] PipeItems = { "hsPipeStraight", "hsPipeCorner", "hsPipeJoint" };

    struct Seed
    {
        public Vector3i Cell;
        public int Dir;
    }

    // start is the picked water system (where new connections come from), end is the thing being connected to it.
    static Vector3i? start;
    static Vector3i? end;
    static int parts;
    static float nextParts;
    static bool startPipe;
    static bool endPipe;
    static string startName = "";
    static string endName = "";
    static List<Vector3i> path;
    static List<Vector3i> pathIn;
    static List<Vector3i> pathOut;
    // Face added to a laid pipe the new run branches off.
    static int startTee;
    static int endTee;
    static Vector3i startTeeAt;
    static Vector3i endTeeAt;
    static int needStraight, needCorner, needJoint;
    static int backStraight, backCorner, backJoint;
    static int hidden;
    static string routeError = "";
    static string routeDebug = "";
    static Dictionary<Vector3i, Vector3i> fromPorts;
    static Dictionary<Vector3i, Vector3i> toPorts;
    static float nextRoute;
    static int routeVersion;
    static int previewKey = -1;
    // Take up stretch: first end, then the other (rain catch → tank, etc.).
    static Vector3i? stretchA;
    static bool stretchAPipe;
    static string stretchAName = "";
    static List<Vector3i> stretchPath;
    static long stretchPreviewKey = -1;

    public static void Tell(EntityPlayerLocal player, string msg)
    {
        if (player != null && !string.IsNullOrEmpty(msg))
            GameManager.ShowTooltip(player, msg);
    }

    static World WorldNow()
    {
        return GameManager.Instance != null ? GameManager.Instance.World : null;
    }

    public static bool IsEndpoint(Block b)
    {
        if (b == null)
            return false;
        string n = b.GetBlockName();
        return n == HSWater.RainCatchName || n == HSWater.TankName || n == HSWater.HeaterName
            || n == HSWater.VanillaHeaterName || n == HSWater.IntakeName || n == HSWater.PumpName
            || b is BlockHSHeater || b is BlockHSPump || HSWater.IsSinkBlock(b) || HSWater.IsBarePipe(n)
            || HSBoiler.IsRadiator(n);
    }

    static bool IsRadiatorEnd(World world, Vector3i pos, bool pipe)
    {
        if (pipe)
            return false;
        Block b = world.GetBlock(pos).Block;
        return b != null && HSBoiler.IsRadiator(b.GetBlockName());
    }

    // Laid pipe has no collider, so walk the crosshair cell by cell, stopping at whatever it hits.
    static bool LookedAtPipe(EntityPlayerLocal player, float reach, out Vector3i cell)
    {
        cell = Vector3i.zero;
        Ray ray = player.GetLookRay();
        for (float t = 0f; t <= reach; t += 0.05f)
        {
            Vector3 p = ray.origin + ray.direction * t;
            var c = new Vector3i(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
            if (HSPipeNet.Has(c))
            {
                cell = c;
                return true;
            }
        }
        return false;
    }

    // Up to whatever the crosshair hits (plus a little, so pipe just inside a floor still counts), never through walls.
    static float Reach(EntityPlayerLocal player)
    {
        float reach = 6f;
        if (player.HitInfo != null && player.HitInfo.bHitValid)
            reach = Mathf.Min(reach, Vector3.Distance(player.GetLookRay().origin, player.HitInfo.hit.pos) + 0.3f);
        return reach;
    }

    static bool CellHasWater(World world, Vector3i p)
    {
        return HSWater.CellHasWater(world, p);
    }

    static bool WaterNear(World world, Vector3i p)
    {
        if (CellHasWater(world, p))
            return true;
        for (int i = 0; i < HSWater.Dirs.Length; i++)
        {
            if (CellHasWater(world, p + HSWater.Dirs[i]))
                return true;
        }
        return false;
    }

    // Existing intake on this stretch of water, or null.
    static bool FindIntakeNear(World world, Vector3i near, out Vector3i intake)
    {
        intake = Vector3i.zero;
        if (world == null)
            return false;
        int best = int.MaxValue;
        bool found = false;
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    Vector3i p = new Vector3i(near.x + dx, near.y + dy, near.z + dz);
                    BlockValue bv = world.GetBlock(p);
                    if (bv.isair || bv.Block == null || !HSWater.IsWaterSourceName(bv.Block.GetBlockName()))
                        continue;
                    Vector3i parent = HSWater.Parent(p, bv);
                    if (!HSWater.TouchesWater(world, parent))
                        continue;
                    int d = Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz);
                    if (d >= best)
                        continue;
                    best = d;
                    intake = parent;
                    found = true;
                }
            }
        }
        return found;
    }

    // Bank cell next to the water for a new intake.
    static bool SuggestIntakeSpot(World world, EntityPlayerLocal player, Vector3i near, out Vector3i spot)
    {
        spot = Vector3i.zero;
        if (world == null || player == null)
            return false;
        var me = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        // Prefer a Water Pump; fall back to the old flange intake.
        string placeName = CountItem(player, HSWater.PumpName) > 0 ? HSWater.PumpName
            : (CountItem(player, HSWater.IntakeName) > 0 ? HSWater.IntakeName : HSWater.PumpName);
        BlockValue intakeBv = Block.GetBlockValue(placeName);
        if (intakeBv.isair)
            return false;
        int best = int.MaxValue;
        bool found = false;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    Vector3i p = new Vector3i(near.x + dx, near.y + dy, near.z + dz);
                    BlockValue bv = world.GetBlock(p);
                    if (!bv.isair)
                        continue;
                    if (!WaterNear(world, p))
                        continue;
                    if (me != null && !world.CanPlaceBlockAt(p, me))
                        continue;
                    if (!intakeBv.Block.CanPlaceBlockAt(world, p, intakeBv))
                        continue;
                    int d = Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz);
                    if (d >= best)
                        continue;
                    best = d;
                    spot = p;
                    found = true;
                }
            }
        }
        return found;
    }

    static int CountItem(EntityPlayer player, string itemName)
    {
        ItemValue iv = PipeItem(itemName);
        if (iv == null || iv.IsEmpty() || player == null)
            return 0;
        int n = 0;
        if (player.bag != null)
            n += player.bag.GetItemCount(iv);
        if (player.inventory != null)
            n += player.inventory.GetItemCount(iv);
        return n;
    }

    static void TakeItem(EntityPlayer player, string itemName)
    {
        ItemValue iv = PipeItem(itemName);
        if (iv == null || iv.IsEmpty() || player == null)
            return;
        int n = 1;
        if (player.bag != null)
            n -= player.bag.DecItem(iv, n);
        if (n > 0 && player.inventory != null)
            player.inventory.DecItem(iv, n);
    }

    // Places a Water Pump (or old intake) on the bank from inventory when aiming at the stream.
    static string EnsureIntake(EntityPlayerLocal player, Vector3i near, out Vector3i intake)
    {
        intake = Vector3i.zero;
        World world = WorldNow();
        if (world == null || player == null)
            return Localization.Get("hsPipeAimNewSystem");
        if (FindIntakeNear(world, near, out intake))
            return null;
        string placeName = CountItem(player, HSWater.PumpName) > 0 ? HSWater.PumpName
            : (CountItem(player, HSWater.IntakeName) > 0 ? HSWater.IntakeName : null);
        if (placeName == null)
            return Localization.Get("hsPipeNeedIntake");
        Vector3i spot;
        if (!SuggestIntakeSpot(world, player, near, out spot))
            return Localization.Get("hsPipeNoIntakeSpot");
        BlockValue bv = Block.GetBlockValue(placeName);
        if (bv.isair)
            return Localization.Get("hsPipeNeedIntake");
        TakeItem(player, placeName);
        world.SetBlockRPC(spot, bv);
        intake = spot;
        return null;
    }

    // Aim at a fixture, laid pipe, or a lake / stream (uses or places a Water Intake on the bank).
    static bool Aimed(EntityPlayerLocal player, out Vector3i pos, out string name, out bool pipe, out bool water)
    {
        pos = Vector3i.zero;
        name = "";
        pipe = false;
        water = false;
        World world = WorldNow();
        if (player == null || world == null)
            return false;
        if (player.HitInfo != null && player.HitInfo.bHitValid)
        {
            Vector3i hit = player.HitInfo.hit.blockPos;
            BlockValue bv = world.GetBlock(hit);
            if (!bv.isair && IsEndpoint(bv.Block))
            {
                pos = HSWater.Parent(hit, bv);
                name = bv.Block.GetLocalizedBlockName();
                return true;
            }
            if (WaterNear(world, hit))
            {
                Vector3i intake;
                if (FindIntakeNear(world, hit, out intake))
                {
                    pos = intake;
                    BlockValue ib = world.GetBlock(intake);
                    name = ib.Block != null ? ib.Block.GetLocalizedBlockName() : Localization.Get("hsWaterIntake");
                    return true;
                }
                pos = hit;
                name = Localization.Get("hsPipeStream");
                water = true;
                return true;
            }
        }
        Vector3i cell;
        if (!LookedAtPipe(player, Reach(player), out cell))
            return false;
        pos = cell;
        pipe = true;
        name = Localization.Get("hsPipeExisting");
        return true;
    }

    static bool Aimed(EntityPlayerLocal player, out Vector3i pos, out string name, out bool pipe)
    {
        bool water;
        return Aimed(player, out pos, out name, out pipe, out water);
    }

    public static bool HasSystem
    {
        get { return start != null; }
    }

    static void PickSystem(Vector3i pos, bool pipe, string name)
    {
        start = pos;
        startPipe = pipe;
        startName = name;
        end = null;
        endPipe = false;
        endName = "";
        ClearRoute();
        World world = WorldNow();
        parts = world != null ? Parts(world, pos, pipe) : 0;
        nextParts = Time.time + 2f;
    }

    // Starts a system at a block that is not piped to anything yet. Already piped: that system is used instead.
    // Aiming at a lake / stream uses the Water Intake on the bank, or places one from your inventory.
    public static string NewSystem(EntityPlayerLocal player)
    {
        Vector3i pos;
        string name;
        bool pipe;
        bool water;
        if (!Aimed(player, out pos, out name, out pipe, out water) || pipe)
            return Localization.Get("hsPipeAimNewSystem");
        if (water)
        {
            string err = EnsureIntake(player, pos, out pos);
            if (err != null)
                return err;
            BlockValue ib = WorldNow().GetBlock(pos);
            name = ib.Block != null ? ib.Block.GetLocalizedBlockName() : Localization.Get("hsWaterIntake");
        }
        PickSystem(pos, false, name);
        if (parts > 1)
            return string.Format(Localization.Get("hsPipeSystemExisting"), name, parts);
        return string.Format(Localization.Get("hsPipeSystemNew"), name);
    }

    // New connections come from here: from this block, or branch off the pipe already coming out of it.
    public static string UseSystem(EntityPlayerLocal player)
    {
        Vector3i pos;
        string name;
        bool pipe;
        bool water;
        if (!Aimed(player, out pos, out name, out pipe, out water))
            return Localization.Get("hsPipeAimSystem");
        if (water)
        {
            string err = EnsureIntake(player, pos, out pos);
            if (err != null)
                return err;
            pipe = false;
            BlockValue ib = WorldNow().GetBlock(pos);
            name = ib.Block != null ? ib.Block.GetLocalizedBlockName() : Localization.Get("hsWaterIntake");
        }
        PickSystem(pos, pipe, name);
        return string.Format(Localization.Get("hsPipeSystemUse"), name, parts);
    }

    public static string NewConnection(EntityPlayerLocal player)
    {
        if (start == null)
            return Localization.Get("hsPipeNeedSystem");
        Vector3i pos;
        string name;
        bool pipe;
        bool water;
        if (!Aimed(player, out pos, out name, out pipe, out water))
            return Localization.Get("hsPipeAimPlumbing");
        if (water)
        {
            string err = EnsureIntake(player, pos, out pos);
            if (err != null)
                return err;
            pipe = false;
            BlockValue ib = WorldNow().GetBlock(pos);
            name = ib.Block != null ? ib.Block.GetLocalizedBlockName() : Localization.Get("hsWaterIntake");
        }
        end = pos;
        endPipe = pipe;
        endName = name;
        Reroute();
        if (path == null)
        {
            Log.Out("[HSWorkingStuff] Pipe tool: no route from " + startName + " " + start.Value + " to " + endName + " " + end.Value
                + ": " + routeError + " | " + routeDebug);
            return routeError;
        }
        return string.Format(Localization.Get("hsPipeConnectTo"), endName, startName);
    }

    // What the router saw next to an end, for the log when there is no route.
    static string Around(World world, List<Vector3i> cells)
    {
        if (cells == null || cells.Count == 0)
            return "-";
        var sb = new System.Text.StringBuilder();
        Vector3i c = cells[0];
        string[] names = { "+x", "-x", "up", "down", "+z", "-z" };
        for (int i = 0; i < HSWater.Dirs.Length; i++)
        {
            Vector3i n = c + HSWater.Dirs[i];
            BlockValue bv = world.GetBlock(n);
            Block b = bv.Block;
            sb.Append(names[i]).Append('=');
            if (bv.isair)
                sb.Append("air");
            else
            {
                sb.Append(b != null ? b.GetBlockName() : "?");
                if (b != null && b.shape != null)
                    sb.Append("(").Append(b.shape.GetType().Name).Append(" solid=").Append(b.shape.IsSolidCube)
                        .Append(" full=").Append(b.shape.getFacesDrawnFullBitfield(bv)).Append(" surface=").Append(HSPipeNet.Surface(bv)).Append(")");
            }
            if (HSPipeNet.Has(n))
                sb.Append("+pipe");
            sb.Append(i < HSWater.Dirs.Length - 1 ? ", " : "");
        }
        return sb.ToString();
    }

    static void ClearRoute()
    {
        path = null;
        pathIn = null;
        pathOut = null;
        startTee = 0;
        endTee = 0;
        needStraight = needCorner = needJoint = 0;
        backStraight = backCorner = backJoint = 0;
        hidden = 0;
        routeError = "";
        routeVersion++;
    }

    public static int CountPipes(EntityPlayer player)
    {
        if (player == null)
            return 0;
        int n = 0;
        for (int i = 0; i < PipeItems.Length; i++)
        {
            ItemValue iv = PipeItem(PipeItems[i]);
            if (iv == null || iv.IsEmpty())
                continue;
            if (player.bag != null)
                n += player.bag.GetItemCount(iv);
            if (player.inventory != null)
                n += player.inventory.GetItemCount(iv);
        }
        return n;
    }

    static ItemValue PipeItem(string name)
    {
        ItemValue iv = ItemClass.GetItem(name);
        if (iv != null && !iv.IsEmpty())
            return iv;
        BlockValue bv = Block.GetBlockValue(name);
        return bv.isair ? null : bv.ToItemValue();
    }

    static void HaveShapes(EntityPlayer player, int[] have)
    {
        for (int i = 0; i < have.Length && i < PipeItems.Length; i++)
        {
            have[i] = 0;
            ItemValue iv = PipeItem(PipeItems[i]);
            if (iv == null || iv.IsEmpty() || player == null)
                continue;
            if (player.bag != null)
                have[i] += player.bag.GetItemCount(iv);
            if (player.inventory != null)
                have[i] += player.inventory.GetItemCount(iv);
        }
    }

    static void TakeShape(EntityPlayer player, int shape, int n)
    {
        if (player == null || n <= 0 || shape < 0 || shape >= PipeItems.Length)
            return;
        ItemValue iv = PipeItem(PipeItems[shape]);
        if (iv == null || iv.IsEmpty())
            return;
        if (player.bag != null)
            n -= player.bag.DecItem(iv, n);
        if (n > 0 && player.inventory != null)
            player.inventory.DecItem(iv, n);
    }

    static void RefundShape(EntityPlayer player, int shape, int n)
    {
        if (player == null || n <= 0 || shape < 0 || shape >= PipeItems.Length)
            return;
        ItemValue iv = PipeItem(PipeItems[shape]);
        if (iv == null || iv.IsEmpty())
            return;
        var stack = new ItemStack(iv, n);
        if (!HSInv.AddToPlayer(player, stack) && HSItem.Count(stack) > 0)
            HSWater.GiveOrDrop(player, stack);
    }

    static void NotePiece(int oldMask, int addMask)
    {
        int merged = (oldMask | addMask) & 63;
        int next = HSPipeNet.Shape(merged);
        if (oldMask == 0)
        {
            AddNeed(next);
            return;
        }
        int old = HSPipeNet.Shape(oldMask);
        if (old == next)
            return;
        AddNeed(next);
        AddBack(old);
    }

    static void AddNeed(int shape)
    {
        if (shape == 1) needCorner++;
        else if (shape == 2) needJoint++;
        else needStraight++;
    }

    static void AddBack(int shape)
    {
        if (shape == 1) backCorner++;
        else if (shape == 2) backJoint++;
        else backStraight++;
    }

    static void Tally()
    {
        needStraight = needCorner = needJoint = 0;
        backStraight = backCorner = backJoint = 0;
        if (path == null)
            return;
        for (int i = 0; i < path.Count; i++)
            NotePiece(0, HSPipeNet.DirBit(pathIn[i]) | HSPipeNet.DirBit(pathOut[i]));
        if (startTee != 0)
            NotePiece(HSPipeNet.MaskAt(startTeeAt), startTee);
        if (endTee != 0)
            NotePiece(HSPipeNet.MaskAt(endTeeAt), endTee);
    }

    public static bool CanLay(EntityPlayerLocal player)
    {
        if (path == null || (path.Count == 0 && endTee == 0))
            return false;
        int[] have = new int[3];
        HaveShapes(player, have);
        return have[0] >= needStraight && have[1] >= needCorner && have[2] >= needJoint;
    }

    public static string Lay(EntityPlayerLocal player)
    {
        World world = WorldNow();
        if (world == null || player == null)
            return "";
        if (start == null)
            return Localization.Get("hsPipeNeedSystem");
        if (end == null)
            return Localization.Get("hsPipeNeedConnection");
        Reroute();
        if (path == null)
            return routeError;
        int need = path.Count;
        int[] haveN = new int[3];
        HaveShapes(player, haveN);
        if (haveN[0] < needStraight || haveN[1] < needCorner || haveN[2] < needJoint)
            return string.Format(Localization.Get("hsPipeNeedShapes"), needStraight, needCorner, needJoint, haveN[0], haveN[1], haveN[2]);
        PersistentPlayerData me = GameManager.Instance.GetPersistentLocalPlayer();
        for (int i = 0; i < path.Count; i++)
        {
            if (!world.CanPlaceBlockAt(path[i], me))
                return Localization.Get("hsPipeClaimed");
        }
        TakeShape(player, 0, needStraight);
        TakeShape(player, 1, needCorner);
        TakeShape(player, 2, needJoint);
        RefundShape(player, 0, backStraight);
        RefundShape(player, 1, backCorner);
        RefundShape(player, 2, backJoint);
        for (int i = 0; i < path.Count; i++)
        {
            int mask = HSPipeNet.DirBit(pathIn[i]) | HSPipeNet.DirBit(pathOut[i]);
            HSWorkingStuffNet.SendWater(HSWorkingStuffNet.PipeLay, path[i], player, mask);
        }
        if (startTee != 0)
            HSWorkingStuffNet.SendWater(HSWorkingStuffNet.PipeLay, startTeeAt, player, startTee);
        if (endTee != 0)
            HSWorkingStuffNet.SendWater(HSWorkingStuffNet.PipeLay, endTeeAt, player, endTee);
        Log.Out("[HSWorkingStuff] Pipe tool laid " + need + " pipes from " + startName + " " + start.Value
            + " to " + endName + " " + end.Value + (hidden > 0 ? ", " + hidden + " under the floor" : ""));
        string done = need > 0
            ? string.Format(Localization.Get("hsPipeLaid"), need, startName, endName)
            : string.Format(Localization.Get("hsPipeTeed"), endName);
        end = null;
        endPipe = false;
        endName = "";
        ClearRoute();
        nextParts = 0f;
        return done;
    }

    public static string TakeUp(EntityPlayerLocal player)
    {
        World world = WorldNow();
        if (world == null || player == null)
            return "";
        ClearStretchPick();
        Vector3i found;
        Vector3i? cell = null;
        if (LookedAtPipe(player, Reach(player), out found))
            cell = found;
        if (cell == null && player.HitInfo != null && player.HitInfo.bHitValid)
        {
            Vector3i hit = player.HitInfo.hit.blockPos;
            BlockValue bv = world.GetBlock(hit);
            if (!bv.isair && IsEndpoint(bv.Block))
            {
                foreach (Vector3i c in Cells(world, HSWater.Parent(hit, bv)))
                {
                    foreach (Vector3i d in HSWater.Dirs)
                    {
                        if (cell == null && HSPipeNet.Has(c + d))
                            cell = c + d;
                    }
                }
            }
        }
        if (cell == null)
            return Localization.Get("hsPipeNoneHere");
        if (!world.CanPlaceBlockAt(cell.Value, GameManager.Instance.GetPersistentLocalPlayer()))
            return Localization.Get("hsPipeClaimed");
        HSWorkingStuffNet.SendWater(HSWorkingStuffNet.PipeRemove, cell.Value, player, 0);
        return "";
    }

    static void ClearStretchPick()
    {
        stretchA = null;
        stretchAPipe = false;
        stretchAName = "";
        stretchPath = null;
        stretchPreviewKey = -1;
    }

    // Pipe cells that touch this fixture (or the cell itself if it is laid pipe).
    static List<Vector3i> PipePorts(World world, Vector3i pos, bool pipe)
    {
        var ports = new List<Vector3i>();
        if (world == null)
            return ports;
        if (pipe)
        {
            if (HSPipeNet.Has(pos))
                ports.Add(pos);
            return ports;
        }
        var seen = new HashSet<long>();
        foreach (Vector3i c in EndCells(world, pos, false))
        {
            foreach (Vector3i d in HSWater.Dirs)
            {
                Vector3i n = c + d;
                if (!HSPipeNet.Has(n) || !HSPipeNet.Joins(c, n))
                    continue;
                if (seen.Add(HSWater.Key(n)))
                    ports.Add(n);
            }
        }
        return ports;
    }

    // Aim at first end, then the other: only that stretch comes up (tees keep other branches).
    public static string TakeUpStretch(EntityPlayerLocal player)
    {
        World world = WorldNow();
        if (world == null || player == null)
            return "";
        Vector3i pos;
        string name;
        bool pipe;
        bool water;
        if (!Aimed(player, out pos, out name, out pipe, out water) || water)
            return Localization.Get(stretchA == null ? "hsPipeAimStretchFirst" : "hsPipeAimStretchSecond");
        if (stretchA == null)
        {
            if (PipePorts(world, pos, pipe).Count == 0)
                return Localization.Get("hsPipeNoneHere");
            stretchA = pos;
            stretchAPipe = pipe;
            stretchAName = name;
            stretchPath = null;
            stretchPreviewKey = -1;
            return string.Format(Localization.Get("hsPipeStretchPicked"), name);
        }
        if (stretchA.Value == pos && stretchAPipe == pipe)
        {
            ClearStretchPick();
            return Localization.Get("hsPipeStretchCancel");
        }
        List<Vector3i> from = PipePorts(world, stretchA.Value, stretchAPipe);
        List<Vector3i> to = PipePorts(world, pos, pipe);
        if (from.Count == 0 || to.Count == 0)
        {
            ClearStretchPick();
            return Localization.Get("hsPipeNoneHere");
        }
        List<Vector3i> path = HSPipeNet.FindStretch(from, to);
        if (path == null || path.Count == 0)
        {
            ClearStretchPick();
            return string.Format(Localization.Get("hsPipeStretchNoPath"), stretchAName, name);
        }
        PersistentPlayerData me = GameManager.Instance.GetPersistentLocalPlayer();
        for (int i = 0; i < path.Count; i++)
        {
            if (!world.CanPlaceBlockAt(path[i], me))
            {
                ClearStretchPick();
                return Localization.Get("hsPipeClaimed");
            }
        }
        bool aPipe = stretchAPipe;
        Vector3i aPos = stretchA.Value;
        ClearStretchPick();
        HSWorkingStuffNet.SendPipeStretch(aPos, aPipe, pos, pipe, player);
        return "";
    }

    // Authority: find the laid path again and take it up, refunding pieces.
    public static void HandleStretchRemove(Vector3i a, bool aPipe, Vector3i b, bool bPipe, EntityPlayer player)
    {
        World world = WorldNow();
        if (world == null || player == null)
            return;
        List<Vector3i> from = PipePorts(world, a, aPipe);
        List<Vector3i> to = PipePorts(world, b, bPipe);
        if (from.Count == 0 || to.Count == 0)
            return;
        List<Vector3i> path = HSPipeNet.FindStretch(from, to);
        if (path == null || path.Count == 0)
            return;
        int[] shapes = new int[3];
        int n = HSPipeNet.RemoveStretch(path, shapes);
        if (n <= 0 && shapes[0] == 0 && shapes[1] == 0 && shapes[2] == 0)
        {
            // Path was only tee faces trimmed — still report something useful.
            HSWorkingStuffNet.Tell(player, Localization.Get("hsPipeStretchTrimmed"));
            return;
        }
        for (int s = 0; s < shapes.Length && s < PipeItems.Length; s++)
        {
            if (shapes[s] <= 0)
                continue;
            ItemValue iv = ItemClass.GetItem(PipeItems[s]);
            if (iv != null && !iv.IsEmpty())
                HSWater.GiveOrDrop(player, new ItemStack(iv, shapes[s]));
        }
        HSWorkingStuffNet.Tell(player, HSLoc.Format("hsPipeTook", "Took up {0} straight, {1} corners and {2} joints", shapes[0], shapes[1], shapes[2]));
        HSInv.NotifyPlayer(player);
    }

    // Live preview of the stretch while the first end is picked and you look at the other.
    public static void UpdateStretchPreview(EntityPlayerLocal player, bool holding)
    {
        if (!holding || stretchA == null || player == null)
        {
            if (stretchPreviewKey != -2)
            {
                stretchPreviewKey = -2;
                if (stretchPath != null)
                {
                    stretchPath = null;
                    // Lay preview owns SetPreview when a route is active; only clear when idle.
                    if (path == null)
                        HSPipeNet.SetPreview(null, null, false);
                }
            }
            return;
        }
        World world = WorldNow();
        if (world == null)
            return;
        Vector3i pos;
        string name;
        bool pipe;
        bool water;
        long key = -1;
        List<Vector3i> show = null;
        if (Aimed(player, out pos, out name, out pipe, out water) && !water
            && !(pos == stretchA.Value && pipe == stretchAPipe))
        {
            List<Vector3i> from = PipePorts(world, stretchA.Value, stretchAPipe);
            List<Vector3i> to = PipePorts(world, pos, pipe);
            if (from.Count > 0 && to.Count > 0)
                show = HSPipeNet.FindStretch(from, to);
            key = show != null ? HSWater.Key(pos) ^ (show.Count * 397L) ^ routeVersion : HSWater.Key(pos);
        }
        if (key == stretchPreviewKey)
            return;
        stretchPreviewKey = key;
        stretchPath = show;
        if (show == null || show.Count == 0 || path != null)
        {
            if (path == null)
                HSPipeNet.SetPreview(null, null, false);
            return;
        }
        var masks = new List<int>(show.Count);
        for (int i = 0; i < show.Count; i++)
            masks.Add(HSPipeNet.MaskAt(show[i]));
        HSPipeNet.SetPreview(show, masks, false);
    }

    static List<Vector3i> Cells(World world, Vector3i parent)
    {
        var list = new List<Vector3i> { parent };
        BlockValue bv = world.GetBlock(parent);
        Block b = bv.Block;
        if (b == null || !b.isMultiBlock)
            return list;
        var seen = new HashSet<Vector3i> { parent };
        for (int i = 0; i < list.Count && list.Count < 64; i++)
        {
            foreach (Vector3i d in HSWater.Dirs)
            {
                Vector3i n = list[i] + d;
                if (seen.Contains(n))
                    continue;
                seen.Add(n);
                BlockValue nbv = world.GetBlock(n);
                if (nbv.Block != b || HSWater.Parent(n, nbv) != parent)
                    continue;
                list.Add(n);
            }
        }
        return list;
    }

    static List<Vector3i> EndCells(World world, Vector3i pos, bool pipe)
    {
        return pipe ? new List<Vector3i> { pos } : Cells(world, pos);
    }

    static bool StillThere(World world, Vector3i pos, bool pipe)
    {
        return pipe ? HSPipeNet.Has(pos) : IsEndpoint(world.GetBlock(pos).Block);
    }

    static void Reroute()
    {
        nextRoute = Time.time + 2f;
        routeVersion++;
        path = null;
        pathIn = null;
        pathOut = null;
        startTee = 0;
        endTee = 0;
        needStraight = needCorner = needJoint = 0;
        backStraight = backCorner = backJoint = 0;
        hidden = 0;
        routeError = "";
        routeDebug = "";
        fromPorts = null;
        toPorts = null;
        World world = WorldNow();
        if (world == null || start == null || end == null)
            return;
        if (start.Value == end.Value)
        {
            routeError = Localization.Get("hsPipeSameBlock");
            return;
        }
        if (!StillThere(world, start.Value, startPipe) || !StillThere(world, end.Value, endPipe))
        {
            routeError = Localization.Get("hsPipeEndGone");
            return;
        }
        List<Vector3i> run = Run(world, start.Value, startPipe);
        if (Joined(run, EndCells(world, end.Value, endPipe)))
        {
            routeError = string.Format(Localization.Get("hsPipeAlreadyJoined"), endName);
            return;
        }
        bool under = IsRadiatorEnd(world, start.Value, startPipe) || IsRadiatorEnd(world, end.Value, endPipe);
        Vector3i sPos = start.Value;
        Vector3i ePos = end.Value;
        bool sPipe = startPipe;
        bool ePipe = endPipe;
        if (under)
        {
            UseFloorPipe(world, ref sPos, ref sPipe);
            UseFloorPipe(world, ref ePos, ref ePipe);
            if (sPos == ePos)
            {
                routeError = Localization.Get("hsPipeTouching");
                return;
            }
        }
        List<Vector3i> to = EndCells(world, ePos, ePipe);
        List<Vector3i> from = under ? EndCells(world, sPos, sPipe) : Nearest(run, to);
        var toSet = new HashSet<Vector3i>(to);
        // The toilet pipe already fills the floor under the sink. That cell touches the sink, so it is
        // not a new port. Tee up from the run before those cells are dropped.
        if (TryTeeUnder(world, run, to))
            return;
        from.RemoveAll(c => Touches(c, toSet));
        if (from.Count == 0)
        {
            routeError = Localization.Get("hsPipeTouching");
            return;
        }
        var fromSet = new HashSet<Vector3i>(from);
        List<Vector3i> route = under
            ? RouteUnder(world, sPos, sPipe, from, ePos, ePipe, to, fromSet, toSet)
            : RouteSurface(world, from, to, fromSet, toSet);
        if (route == null || route.Count == 0)
        {
            if (routeError.Length == 0)
                routeError = Localization.Get("hsPipeNoRoute");
            return;
        }
        path = route;
        pathIn = new List<Vector3i>(route.Count);
        pathOut = new List<Vector3i>(route.Count);
        Vector3i portDir;
        for (int i = 0; i < route.Count; i++)
        {
            if (i > 0)
                pathIn.Add(route[i - 1] - route[i]);
            else
                pathIn.Add(fromPorts != null && fromPorts.TryGetValue(route[0], out portDir) ? portDir : DirTo(route[0], fromSet));
            if (i + 1 < route.Count)
                pathOut.Add(route[i + 1] - route[i]);
            else
                pathOut.Add(toPorts != null && toPorts.TryGetValue(route[i], out portDir) ? portDir : DirTo(route[i], toSet));
            if (HSPipeNet.Surface(world.GetBlock(route[i])))
                hidden++;
        }
        int last = route.Count - 1;
        Vector3i sAt = route[0] + pathIn[0];
        if (HSPipeNet.Has(sAt))
        {
            startTee = HSPipeNet.DirBit(route[0] - sAt);
            startTeeAt = sAt;
        }
        Vector3i eAt = route[last] + pathOut[last];
        if (HSPipeNet.Has(eAt))
        {
            endTee = HSPipeNet.DirBit(route[last] - eAt);
            endTeeAt = eAt;
        }
        Tally();
    }

    // The run to the toilet already fills the only solid block under the sink. Sides are a bin, skirting
    // or open air, so there is no new port. Feed the fixture by adding the upward joint on that pipe.
    static bool TryTeeUnder(World world, List<Vector3i> from, List<Vector3i> to)
    {
        var set = new HashSet<Vector3i>(from);
        foreach (Vector3i c in to)
        {
            Block b = world.GetBlock(c).Block;
            if (b == null || BarePipe(world, c) || !HSWater.IsSinkBlock(b))
                continue;
            Vector3i under = c + Vector3i.down;
            if (!set.Contains(under) || !HSPipeNet.Has(under) || HSPipeNet.Joins(under, c))
                continue;
            path = new List<Vector3i>();
            pathIn = new List<Vector3i>();
            pathOut = new List<Vector3i>();
            endTee = HSPipeNet.DirBit(Vector3i.up);
            endTeeAt = under;
            hidden = 0;
            routeError = "";
            Tally();
            return true;
        }
        return false;
    }

    static bool Touches(Vector3i c, HashSet<Vector3i> set)
    {
        foreach (Vector3i d in HSWater.Dirs)
        {
            if (set.Contains(c + d))
                return true;
        }
        return false;
    }

    static bool Plumbing(World world, Vector3i p)
    {
        return HSPipeNet.Has(p) || IsEndpoint(world.GetBlock(p).Block);
    }

    static bool BarePipe(World world, Vector3i p)
    {
        if (HSPipeNet.Has(p))
            return true;
        Block b = world.GetBlock(p).Block;
        return b != null && HSWater.IsBarePipe(b.GetBlockName());
    }

    // The system's own block plus the pipe coming out of it, up to the next thing it feeds. A new connection
    // can start anywhere on it, so a second sink tees into the pipe instead of a whole new run.
    static List<Vector3i> Run(World world, Vector3i pos, bool pipe)
    {
        List<Vector3i> list = EndCells(world, pos, pipe);
        var seen = new HashSet<Vector3i>(list);
        for (int i = 0; i < list.Count && list.Count < 1024; i++)
        {
            foreach (Vector3i d in HSWater.Dirs)
            {
                Vector3i n = list[i] + d;
                if (seen.Contains(n) || !BarePipe(world, n) || !HSPipeNet.Joins(list[i], n))
                    continue;
                seen.Add(n);
                list.Add(n);
            }
        }
        return list;
    }

    // Things (tanks, sinks, boilers...) joined together with this one, itself included.
    static int Parts(World world, Vector3i pos, bool pipe)
    {
        List<Vector3i> list = EndCells(world, pos, pipe);
        var seen = new HashSet<Vector3i>(list);
        var things = new HashSet<Vector3i>();
        for (int i = 0; i < list.Count && list.Count < 4096; i++)
        {
            Vector3i p = list[i];
            BlockValue bv = world.GetBlock(p);
            if (!HSPipeNet.Has(p) && IsEndpoint(bv.Block) && !HSWater.IsBarePipe(bv.Block.GetBlockName()))
                things.Add(HSWater.Parent(p, bv));
            foreach (Vector3i d in HSWater.Dirs)
            {
                Vector3i n = p + d;
                if (seen.Contains(n) || !Plumbing(world, n) || !HSPipeNet.Joins(p, n))
                    continue;
                seen.Add(n);
                list.Add(n);
            }
        }
        return things.Count;
    }

    static bool Joined(List<Vector3i> run, List<Vector3i> cells)
    {
        var set = new HashSet<Vector3i>(run);
        foreach (Vector3i c in cells)
        {
            if (set.Contains(c))
                return true;
            foreach (Vector3i d in HSWater.Dirs)
            {
                if (set.Contains(c + d) && HSPipeNet.Joins(c + d, c))
                    return true;
            }
        }
        return false;
    }

    // Only the part of the system near the new thing, so the search area stays small.
    static List<Vector3i> Nearest(List<Vector3i> run, List<Vector3i> to)
    {
        var dist = new Dictionary<Vector3i, int>();
        int best = int.MaxValue;
        foreach (Vector3i c in run)
        {
            int m = int.MaxValue;
            foreach (Vector3i t in to)
                m = Math.Min(m, Math.Abs(c.x - t.x) + Math.Abs(c.y - t.y) + Math.Abs(c.z - t.z));
            dist[c] = m;
            best = Math.Min(best, m);
        }
        var near = new List<Vector3i>();
        foreach (Vector3i c in run)
        {
            if (dist[c] <= best + 6)
                near.Add(c);
        }
        near.Sort((a, b) => dist[a].CompareTo(dist[b]));
        if (near.Count > 64)
            near.RemoveRange(64, near.Count - 64);
        return near;
    }

    // A second radiator (or boiler) run branches off pipe already under the floor rather than starting a new drop.
    static void UseFloorPipe(World world, ref Vector3i pos, ref bool pipe)
    {
        var q = new Queue<Vector3i>();
        var seen = new HashSet<Vector3i>();
        if (pipe)
        {
            q.Enqueue(pos);
            seen.Add(pos);
        }
        else
        {
            foreach (Vector3i c in Cells(world, pos))
            {
                seen.Add(c);
                foreach (Vector3i d in HSWater.Dirs)
                {
                    Vector3i n = c + d;
                    if (!seen.Contains(n) && HSPipeNet.Has(n) && HSPipeNet.Joins(c, n))
                    {
                        seen.Add(n);
                        q.Enqueue(n);
                    }
                }
            }
        }
        int walked = 0;
        while (q.Count > 0 && walked++ < 256)
        {
            Vector3i p = q.Dequeue();
            if (FloorCell(world, p))
            {
                pos = p;
                pipe = true;
                return;
            }
            foreach (Vector3i d in HSWater.Dirs)
            {
                Vector3i n = p + d;
                if (!seen.Contains(n) && HSPipeNet.Has(n) && HSPipeNet.Joins(p, n))
                {
                    seen.Add(n);
                    q.Enqueue(n);
                }
            }
        }
    }

    static Vector3i DirTo(Vector3i c, HashSet<Vector3i> set)
    {
        foreach (Vector3i d in HSWater.Dirs)
        {
            if (set.Contains(c + d))
                return d;
        }
        return Vector3i.zero;
    }

    // Visible pipe only runs up or along a wall, or straight up into the thing being piped. Never out across
    // a floor or ceiling in the middle of a room: there it goes inside the floor instead.
    static bool Hugs(World world, Vector3i p, HashSet<Vector3i> fromSet, HashSet<Vector3i> toSet)
    {
        Vector3i up = p + Vector3i.up;
        return OnWall(world, p) || fromSet.Contains(up) || toSet.Contains(up);
    }

    // Does the block at n have a flat, full side facing its neighbour in direction toward? Full cubes and
    // terrain always do. Half blocks and plates only on their full sides, so a half block counts as a floor
    // when it fills the top of its cell and as a wall when its flat side faces the pipe.
    static bool Covers(World world, Vector3i n, Vector3i toward)
    {
        BlockValue bv = world.GetBlock(n);
        if (HSPipeNet.Surface(bv))
            return true;
        if (bv.isair || bv.ischild)
            return false;
        Block b = bv.Block;
        if (b == null || !(b.shape is BlockShapeNew))
            return false;
        int face = FaceBit(toward);
        return face != 0 && (b.shape.getFacesDrawnFullBitfield(bv) & face) != 0;
    }

    // Bit order of getFacesDrawnFullBitfield is the BlockFace order: Top, Bottom, North (+z), West, South, East.
    static int FaceBit(Vector3i d)
    {
        if (d.y > 0) return 1;
        if (d.y < 0) return 2;
        if (d.z > 0) return 4;
        if (d.x < 0) return 8;
        if (d.z < 0) return 16;
        if (d.x > 0) return 32;
        return 0;
    }

    // Pipe can run inside a floor block: a full block, a half block / plate whose top is full,
    // or a POI multi-block floor (child cells still count as the floor the radiator stands on).
    static bool FloorCell(World world, Vector3i p)
    {
        BlockValue bv = world.GetBlock(p);
        if (bv.isair || IsEndpoint(bv.Block))
            return false;
        if (Covers(world, p, Vector3i.up))
            return true;
        if (bv.ischild)
        {
            Vector3i parent = HSWater.Parent(p, bv);
            if (parent == p)
                return true;
            BlockValue pv = world.GetBlock(parent);
            return !pv.isair && (Covers(world, parent, Vector3i.up) || pv.Block != null);
        }
        return false;
    }

    // Where pipe may join each cell, and the direction from there into it. Pipe joins pipe on any side.
    // A tank, sink, boiler or rain catch is plumbed at its bottom: from below (preferred) or the side of its
    // lowest layer, never on top or higher up.
    static Dictionary<Vector3i, Vector3i> Ports(World world, List<Vector3i> cells, Func<Vector3i, bool> free, Dictionary<Vector3i, int> extra)
    {
        var ports = new Dictionary<Vector3i, Vector3i>();
        int low = int.MaxValue;
        foreach (Vector3i c in cells)
        {
            if (!BarePipe(world, c))
                low = Math.Min(low, c.y);
        }
        foreach (Vector3i c in cells)
        {
            bool pipe = BarePipe(world, c);
            if (!pipe && c.y != low)
                continue;
            bool belowOnly = !pipe && HSWater.IsSinkBlock(world.GetBlock(c).Block) && free(c + Vector3i.down);
            foreach (Vector3i d in HSWater.Dirs)
            {
                if (!pipe && d.y > 0)
                    continue;
                if (belowOnly && d.y == 0)
                    continue;
                Vector3i p = c + d;
                if (ports.ContainsKey(p) || !free(p))
                    continue;
                ports[p] = c - p;
                if (!pipe && d.y == 0)
                    extra[p] = SidePortCost;
            }
        }
        return ports;
    }

    // The cabinet a sink stands on: pipe may run inside it and come up into the sink from below.
    static bool UnderSink(World world, Vector3i p, HashSet<Vector3i> fromSet, HashSet<Vector3i> toSet)
    {
        Vector3i up = p + Vector3i.up;
        if (!fromSet.Contains(up) && !toSet.Contains(up))
            return false;
        return HSWater.IsSinkBlock(world.GetBlock(up).Block);
    }

    static int DirIndex(Vector3i d)
    {
        for (int i = 0; i < HSWater.Dirs.Length; i++)
        {
            if (HSWater.Dirs[i] == d)
                return i;
        }
        return 0;
    }

    // Along floors and walls, or through them; never across a room.
    static List<Vector3i> RouteSurface(World world, List<Vector3i> from, List<Vector3i> to, HashSet<Vector3i> fromSet, HashSet<Vector3i> toSet)
    {
        Vector3i[] dirs = HSWater.Dirs;
        Func<Vector3i, bool> free = p =>
        {
            if (fromSet.Contains(p) || toSet.Contains(p) || HSPipeNet.Has(p))
                return false;
            BlockValue bv = world.GetBlock(p);
            if (bv.isair)
                return Hugs(world, p, fromSet, toSet);
            if (UnderSink(world, p, fromSet, toSet))
                return !IsEndpoint(bv.Block);
            return HSPipeNet.Surface(bv) && !IsEndpoint(bv.Block);
        };
        var extra = new Dictionary<Vector3i, int>();
        fromPorts = Ports(world, from, free, extra);
        toPorts = Ports(world, to, free, extra);
        Func<Vector3i, int> enter = p =>
        {
            if (!free(p))
                return -1;
            int e;
            extra.TryGetValue(p, out e);
            if (!world.GetBlock(p).isair)
                return StepCost + InBlockCost + e;
            if (Covers(world, p + Vector3i.down, Vector3i.up))
                return StepCost + e;
            return StepCost + WallOnlyCost + e;
        };
        var seeds = new List<Seed>();
        foreach (var kv in fromPorts)
            seeds.Add(new Seed { Cell = kv.Key, Dir = DirIndex(Vector3i.zero - kv.Value) });
        var goals = new HashSet<Vector3i>(toPorts.Keys);
        routeDebug = "surface: " + from.Count + " start cells, " + seeds.Count + " ways out, " + goals.Count + " ways in; start "
            + Around(world, from) + " | end " + Around(world, to);
        if (seeds.Count == 0)
        {
            routeError = string.Format(Localization.Get("hsPipeNoExit"), startName);
            return null;
        }
        if (goals.Count == 0)
        {
            routeError = string.Format(Localization.Get("hsPipeNoEntry"), endName);
            return null;
        }
        List<Vector3i> found = Dijkstra(seeds, goals, enter, 0);
        if (found == null && routeError.Length == 0)
            routeError = BlockedAt(world, seeds, goals, enter);
        return found;
    }

    // Radiators are fed under the floor: a drop from the boiler (or other end) to the floor, a hidden run
    // through the floor, and straight up into the radiator.
    static List<Vector3i> RouteUnder(World world, Vector3i sPos, bool sPipe, List<Vector3i> from,
        Vector3i ePos, bool ePipe, List<Vector3i> to, HashSet<Vector3i> fromSet, HashSet<Vector3i> toSet)
    {
        Func<Vector3i, bool> floor = p =>
        {
            if (fromSet.Contains(p) || toSet.Contains(p))
                return false;
            if (HSPipeNet.Has(p))
                return !IsEndpoint(world.GetBlock(p).Block);
            return FloorCell(world, p);
        };
        // The drop to the floor runs down the wall, not through the air.
        Func<Vector3i, bool> air = p => !fromSet.Contains(p) && !toSet.Contains(p) && !HSPipeNet.Has(p) && world.GetBlock(p).isair
            && Hugs(world, p, fromSet, toSet);
        routeDebug = "under: start " + Around(world, from) + " | end " + Around(world, to);
        List<Vector3i> riserA, riserB;
        Vector3i entryA, entryB;
        if (!Drop(world, sPos, sPipe, from, floor, air, out riserA, out entryA)
            || !Drop(world, ePos, ePipe, to, floor, air, out riserB, out entryB))
        {
            routeError = Localization.Get("hsPipeNoFloor");
            return null;
        }
        List<Vector3i> under;
        if (entryA == entryB)
            under = new List<Vector3i> { entryA };
        else
        {
            Func<Vector3i, int> enter = p =>
            {
                if (!floor(p))
                    return -1;
                return StepCost;
            };
            under = Dijkstra(new List<Seed> { new Seed { Cell = entryA, Dir = 3 } }, new HashSet<Vector3i> { entryB }, enter, UnderVertCost);
            if (under == null)
            {
                if (routeError.Length == 0)
                    routeError = BlockedAt(world, new List<Seed> { new Seed { Cell = entryA, Dir = 3 } }, new HashSet<Vector3i> { entryB }, enter);
                return null;
            }
        }
        var route = new List<Vector3i>(riserA);
        route.AddRange(under);
        for (int i = riserB.Count - 1; i >= 0; i--)
            route.Add(riserB[i]);
        if (new HashSet<Vector3i>(route).Count != route.Count)
            return null;
        return route;
    }

    // From an end down to the floor. A radiator stands on the floor, so its pipe goes straight into the block below.
    static bool Drop(World world, Vector3i pos, bool pipe, List<Vector3i> cells, Func<Vector3i, bool> floor,
        Func<Vector3i, bool> air, out List<Vector3i> riser, out Vector3i entry)
    {
        riser = null;
        entry = pos;
        if (IsRadiatorEnd(world, pos, pipe))
        {
            int low = int.MaxValue;
            foreach (Vector3i c in cells)
                low = Math.Min(low, c.y);
            foreach (Vector3i c in cells)
            {
                if (c.y == low && floor(c + Vector3i.down))
                {
                    riser = new List<Vector3i>();
                    entry = c + Vector3i.down;
                    return true;
                }
            }
            return false;
        }
        bool inFloor = pipe && !world.GetBlock(pos).isair;
        int best = int.MaxValue;
        var starts = new List<Vector3i>();
        var sideways = new HashSet<Vector3i>();
        foreach (Vector3i c in cells)
        {
            foreach (Vector3i d in HSWater.Dirs)
            {
                if (d.y != 0)
                    continue;
                starts.Add(c + d);
                sideways.Add(c + d);
            }
        }
        // Pipe already in the floor carries on sideways; anything above the floor drops straight down first.
        foreach (Vector3i c in cells)
        {
            if (inFloor)
                starts.Add(c + Vector3i.down);
            else
                starts.Insert(0, c + Vector3i.down);
        }
        foreach (Vector3i s in starts)
        {
            if (cells.Contains(s))
                continue;
            if (floor(s))
            {
                if (sideways.Contains(s) && !inFloor)
                    continue;
                if (riser == null || riser.Count > 0)
                {
                    riser = new List<Vector3i>();
                    entry = s;
                    best = 0;
                }
                continue;
            }
            var r = new List<Vector3i>();
            Vector3i q = s;
            bool landed = false;
            for (int k = 0; k < MaxDrop && air(q); k++)
            {
                r.Add(q);
                q = q + Vector3i.down;
                if (floor(q))
                {
                    landed = true;
                    break;
                }
            }
            if (!landed)
                continue;
            int score = DropScore(world, r);
            if (riser == null || score < best)
            {
                riser = r;
                entry = q;
                best = score;
            }
        }
        return riser != null;
    }

    // A drop standing out in the room costs far more than one running down a wall, so a blocked straight drop
    // moves along the wall instead of away from it.
    static int DropScore(World world, List<Vector3i> r)
    {
        int score = r.Count;
        foreach (Vector3i c in r)
        {
            if (!OnWall(world, c))
                score += OffWallCost;
        }
        return score;
    }

    static bool OnWall(World world, Vector3i c)
    {
        foreach (Vector3i d in HSWater.Dirs)
        {
            if (d.y != 0)
                continue;
            if (Covers(world, c + d, Vector3i.zero - d))
                return true;
        }
        return false;
    }

    // Cheapest run from any seed to any goal. A* toward the nearest goal — no mid-tank required for long runs.
    static List<Vector3i> Dijkstra(List<Seed> seeds, HashSet<Vector3i> goals, Func<Vector3i, int> enter, int vertExtra)
    {
        if (seeds.Count == 0 || goals.Count == 0)
            return null;
        Vector3i[] dirs = HSWater.Dirs;
        var dist = new Dictionary<long, int>();
        var prev = new Dictionary<long, long>();
        var cellCost = new Dictionary<long, int>();
        var atCell = new Dictionary<long, Vector3i>();
        var heap = new List<long>();
        var heapKey = new List<long>();
        Func<Vector3i, int> price = p =>
        {
            long ck = CellKey(p);
            int c;
            if (!cellCost.TryGetValue(ck, out c))
            {
                c = enter(p);
                cellCost[ck] = c;
            }
            return c;
        };
        Func<Vector3i, int> heur = p =>
        {
            int best = int.MaxValue;
            foreach (Vector3i g in goals)
            {
                int md = Math.Abs(p.x - g.x) + Math.Abs(p.y - g.y) + Math.Abs(p.z - g.z);
                if (md < best)
                    best = md;
            }
            return best == int.MaxValue ? 0 : best * StepCost;
        };
        foreach (Seed sd in seeds)
        {
            int c = price(sd.Cell);
            if (c < 0)
                continue;
            long s = (CellKey(sd.Cell) << 3) | (uint)(sd.Dir & 7);
            int old;
            if (dist.TryGetValue(s, out old) && c >= old)
                continue;
            dist[s] = c;
            atCell[s] = sd.Cell;
            PushKey(heap, heapKey, c + heur(sd.Cell), s);
        }
        int expanded = 0;
        int expandCap = HSWorkingStuffSettings.PipeRouteExpandLimit;
        if (expandCap < 80000)
            expandCap = 80000;
        while (heap.Count > 0)
        {
            long top = PopKey(heap, heapKey, out long s);
            int f = (int)(top >> 32);
            int known;
            if (!dist.TryGetValue(s, out known))
                continue;
            Vector3i p;
            if (!atCell.TryGetValue(s, out p))
                continue;
            if (f > known + heur(p))
                continue;
            if (++expanded > expandCap)
            {
                routeError = HSLoc.Get("hsPipeTooBig", "That run is too long for the pipe tool to finish the search. Raise PipeRouteExpandLimit in HSWorkingStuffSettings.json, or check the path is not blocked.");
                return null;
            }
            if (goals.Contains(p))
            {
                var run = new List<Vector3i>();
                long t = s;
                int guard = 0;
                while (guard++ < 100000)
                {
                    Vector3i cell;
                    if (!atCell.TryGetValue(t, out cell))
                        break;
                    run.Add(cell);
                    long back;
                    if (!prev.TryGetValue(t, out back))
                        break;
                    t = back;
                }
                run.Reverse();
                return run;
            }
            int d = (int)(s & 7);
            int at = known;
            for (int e = 0; e < 6; e++)
            {
                if ((e ^ 1) == d)
                    continue;
                Vector3i n = p + dirs[e];
                if (n.y < 1 || n.y > 253)
                    continue;
                int ec = price(n);
                if (ec < 0)
                    continue;
                long ns = (CellKey(n) << 3) | (uint)e;
                int nc = at + ec + (e != d ? TurnCost : 0) + (dirs[e].y != 0 ? vertExtra : 0);
                int nd;
                if (dist.TryGetValue(ns, out nd) && nc >= nd)
                    continue;
                dist[ns] = nc;
                prev[ns] = s;
                atCell[ns] = n;
                PushKey(heap, heapKey, nc + heur(n), ns);
            }
        }
        return null;
    }

    static long CellKey(Vector3i p)
    {
        return ((long)(p.x + 500000) << 42) | ((long)(p.y & 511) << 21) | (long)(p.z + 500000);
    }

    // The reachable pipe stops somewhere. Name that cell, and what is sitting in it.
    static string BlockedAt(World world, List<Seed> seeds, HashSet<Vector3i> goals, Func<Vector3i, int> enter)
    {
        var seen = new HashSet<long>();
        var q = new Queue<Vector3i>();
        foreach (Seed s in seeds)
        {
            if (seen.Add(CellKey(s.Cell)))
                q.Enqueue(s.Cell);
        }
        Vector3i best = Vector3i.zero;
        int bestD = int.MaxValue;
        bool found = false;
        int guard = 0;
        while (q.Count > 0 && guard++ < 8000)
        {
            Vector3i p = q.Dequeue();
            foreach (Vector3i d in HSWater.Dirs)
            {
                Vector3i n = p + d;
                if (n.y < 1 || n.y > 253 || !seen.Add(CellKey(n)))
                    continue;
                if (enter(n) >= 0)
                {
                    q.Enqueue(n);
                    continue;
                }
                int md = int.MaxValue;
                foreach (Vector3i g in goals)
                    md = Math.Min(md, Math.Abs(n.x - g.x) + Math.Abs(n.y - g.y) + Math.Abs(n.z - g.z));
                if (md < bestD)
                {
                    bestD = md;
                    best = n;
                    found = true;
                }
            }
        }
        if (!found)
            return Localization.Get("hsPipeNoRoute");
        BlockValue bv = world.GetBlock(best);
        if (bv.isair || bv.Block == null)
            return string.Format(Localization.Get("hsPipeBlockedAir"), best.x, best.y, best.z);
        string what = bv.Block.GetLocalizedBlockName();
        return string.Format(Localization.Get("hsPipeBlockedBlock"), best.x, best.y, best.z, what);
    }

    static void PushKey(List<long> heap, List<long> keys, int cost, long state)
    {
        heap.Add((long)cost << 32);
        keys.Add(state);
        int i = heap.Count - 1;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (heap[parent] <= heap[i])
                break;
            long t = heap[parent];
            heap[parent] = heap[i];
            heap[i] = t;
            long k = keys[parent];
            keys[parent] = keys[i];
            keys[i] = k;
            i = parent;
        }
    }

    static long PopKey(List<long> heap, List<long> keys, out long state)
    {
        long top = heap[0];
        state = keys[0];
        int last = heap.Count - 1;
        heap[0] = heap[last];
        keys[0] = keys[last];
        heap.RemoveAt(last);
        keys.RemoveAt(last);
        int i = 0;
        while (true)
        {
            int l = i * 2 + 1;
            int r = l + 1;
            int m = i;
            if (l < heap.Count && heap[l] < heap[m]) m = l;
            if (r < heap.Count && heap[r] < heap[m]) m = r;
            if (m == i)
                break;
            long t = heap[m];
            heap[m] = heap[i];
            heap[i] = t;
            long k = keys[m];
            keys[m] = keys[i];
            keys[i] = k;
            i = m;
        }
        return top;
    }

    static int Bends()
    {
        if (path == null)
            return 0;
        int n = 0;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3i a = pathIn[i];
            Vector3i b = pathOut[i];
            if (!(a.x == -b.x && a.y == -b.y && a.z == -b.z))
                n++;
        }
        return n;
    }

    static string PieceLine()
    {
        return string.Format(Localization.Get("hsPipePieces"), needStraight, needCorner, needJoint);
    }

    static string Where(Vector3i? p, string name)
    {
        if (p == null)
            return Localization.Get("hsPipeNotSet");
        return name + "  (" + p.Value.x + " " + p.Value.y + " " + p.Value.z + ")";
    }

    public static string HudText(EntityPlayerLocal player)
    {
        if (start != null && end != null && Time.time >= nextRoute)
            Reroute();
        World world = WorldNow();
        if (start != null && world != null && Time.time >= nextParts)
        {
            nextParts = Time.time + 2f;
            parts = StillThere(world, start.Value, startPipe) ? Parts(world, start.Value, startPipe) : 0;
        }
        int have = CountPipes(player);
        string route;
        if (start == null)
            route = Localization.Get("hsPipeRouteNoSystem");
        else if (end == null)
            route = Localization.Get("hsPipeRoutePending");
        else if (path == null)
            route = routeError;
        else if (path.Count == 0 && endTee != 0)
            route = string.Format(Localization.Get("hsPipeTeeUnder"), endName) + "\n" + PieceLine();
        else
        {
            bool fit = CanLay(player);
            route = string.Format(Localization.Get("hsPipeRoute"), path.Count, Bends(),
                fit ? Localization.Get("hsPipeEnough") : Localization.Get("hsPipeShortShapes"))
                + "\n" + PieceLine();
            if (hidden > 0)
                route += "\n" + string.Format(Localization.Get("hsPipeUnderFloor"), hidden);
        }
        string stretch = stretchA == null
            ? ""
            : Localization.Get("hsPipeHudStretch") + " " + Where(stretchA, stretchAName) + "\n"
                + Localization.Get("hsPipeHudStretchHint") + "\n";
        return Localization.Get("hsPipeHudTitle") + "\n"
            + Localization.Get("hsPipeHudSystem") + " " + Where(start, startName)
            + (start != null ? "  " + string.Format(Localization.Get("hsPipeHudParts"), parts) : "") + "\n"
            + Localization.Get("hsPipeHudConnect") + " " + Where(end, endName) + "\n"
            + stretch
            + route + "\n"
            + string.Format(Localization.Get("hsPipeHudHave"), have) + "\n"
            + Localization.Get("hsPipeHudHelp");
    }

    // The planned route is drawn as real pipe, green when you carry enough, red when short.
    public static void UpdatePreview(EntityPlayerLocal player, bool holding)
    {
        if (holding && stretchA != null && path == null)
        {
            UpdateStretchPreview(player, true);
            return;
        }
        UpdateStretchPreview(player, false);
        bool want = holding && path != null && start != null && end != null;
        bool enough = want && CanLay(player);
        int key = want ? routeVersion * 2 + (enough ? 1 : 0) : -1;
        if (key == previewKey)
            return;
        previewKey = key;
        if (!want)
        {
            HSPipeNet.SetPreview(null, null, false);
            return;
        }
        var cellsOut = new List<Vector3i>(path);
        var masks = new List<int>(path.Count + 2);
        for (int i = 0; i < path.Count; i++)
            masks.Add(HSPipeNet.DirBit(pathIn[i]) | HSPipeNet.DirBit(pathOut[i]));
        if (startTee != 0)
        {
            cellsOut.Add(startTeeAt);
            masks.Add(startTee);
        }
        if (endTee != 0)
        {
            cellsOut.Add(endTeeAt);
            masks.Add(endTee);
        }
        HSPipeNet.SetPreview(cellsOut, masks, enough);
    }
}

[Preserve]
public class HSPipeHud : XUiController
{
    string info = "";
    bool show;
    float nextText;

    public override void Update(float _dt)
    {
        base.Update(_dt);
        var player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        bool holding = ItemActionHSPipeTool.IsHolding(player);
        string next = info;
        if (!holding)
            next = "";
        else if (!show || Time.unscaledTime >= nextText)
        {
            nextText = Time.unscaledTime + 0.25f;
            next = HSPipeTool.HudText(player);
        }
        HSPipeTool.UpdatePreview(player, holding);
        if (holding != show || next != info)
        {
            show = holding;
            info = next;
            IsDirty = true;
        }
        if (!IsDirty)
            return;
        RefreshBindings();
        IsDirty = false;
    }

    public override bool GetBindingValueInternal(ref string _value, string _bindingName)
    {
        if (_bindingName == "info")
        {
            _value = info ?? "";
            return true;
        }
        if (_bindingName == "show")
        {
            _value = show ? "true" : "false";
            return true;
        }
        return base.GetBindingValueInternal(ref _value, _bindingName);
    }
}

[HarmonyPatch(typeof(PlayerMoveController), "HandleInteraction")]
public static class HSPipeToolInteract
{
    static readonly System.Reflection.FieldInfo playerField = AccessTools.Field(typeof(PlayerMoveController), "entityPlayerLocal");
    static readonly System.Reflection.FieldInfo uiField = AccessTools.Field(typeof(PlayerMoveController), "playerUI");

    static bool Prefix(PlayerMoveController __instance, WorldRayHitInfo _hitInfo, bool _activate)
    {
        try
        {
            var player = playerField != null ? playerField.GetValue(__instance) as EntityPlayerLocal : null;
            if (!ItemActionHSPipeTool.IsHolding(player))
                return true;
            if (_hitInfo == null || !_hitInfo.bHitValid)
                return true;
            var ui = uiField != null ? uiField.GetValue(__instance) as LocalPlayerUI : null;
            if (ui != null)
                XUiC_InteractionPrompt.SetText(ui, Localization.Get("hsPipeToolHint"));
            if (_activate)
                ItemActionHSPipeTool.OpenRadial(player);
            return false;
        }
        catch
        {
            return true;
        }
    }
}

[HarmonyPatch(typeof(XUiC_InteractionPrompt), "Update")]
public static class HSPipeToolPromptClear
{
    static void Postfix(XUiC_InteractionPrompt __instance)
    {
        try
        {
            if (__instance == null || __instance.xui == null || string.IsNullOrEmpty(__instance.Text))
                return;
            string hint = Localization.Get("hsPipeToolHint");
            if (__instance.Text.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0)
                return;
            var player = __instance.xui.playerUI != null ? __instance.xui.playerUI.entityPlayer : null;
            if (ItemActionHSPipeTool.IsHolding(player) && player.HitInfo != null && player.HitInfo.bHitValid)
                return;
            XUiC_InteractionPrompt.SetText(__instance.xui.playerUI, null);
        }
        catch { }
    }
}
