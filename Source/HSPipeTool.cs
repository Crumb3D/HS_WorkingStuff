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
    public const int MaxRun = 64;
    const int Margin = 4;
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
    static int hidden;
    static string routeError = "";
    static string routeDebug = "";
    static Dictionary<Vector3i, Vector3i> fromPorts;
    static Dictionary<Vector3i, Vector3i> toPorts;
    static float nextRoute;
    static int routeVersion;
    static int previewKey = -1;

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
            || n == HSWater.IntakeName || HSWater.IsSinkBlock(b) || HSWater.IsBarePipe(n)
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

    static bool Aimed(EntityPlayerLocal player, out Vector3i pos, out string name, out bool pipe)
    {
        pos = Vector3i.zero;
        name = "";
        pipe = false;
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
        }
        Vector3i cell;
        if (!LookedAtPipe(player, Reach(player), out cell))
            return false;
        pos = cell;
        pipe = true;
        name = Localization.Get("hsPipeExisting");
        return true;
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
    public static string NewSystem(EntityPlayerLocal player)
    {
        Vector3i pos;
        string name;
        bool pipe;
        if (!Aimed(player, out pos, out name, out pipe) || pipe)
            return Localization.Get("hsPipeAimNewSystem");
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
        if (!Aimed(player, out pos, out name, out pipe))
            return Localization.Get("hsPipeAimSystem");
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
        if (!Aimed(player, out pos, out name, out pipe))
            return Localization.Get("hsPipeAimPlumbing");
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

    static void TakePipes(EntityPlayer player, int need)
    {
        for (int i = 0; i < PipeItems.Length && need > 0; i++)
        {
            ItemValue iv = PipeItem(PipeItems[i]);
            if (iv == null || iv.IsEmpty())
                continue;
            if (player.bag != null)
                need -= player.bag.DecItem(iv, need);
            if (need > 0 && player.inventory != null)
                need -= player.inventory.DecItem(iv, need);
        }
    }

    public static bool CanLay(EntityPlayerLocal player)
    {
        return path != null && path.Count > 0 && CountPipes(player) >= path.Count;
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
        int have = CountPipes(player);
        if (have < need)
            return string.Format(Localization.Get("hsPipeNotEnough"), need, have);
        PersistentPlayerData me = GameManager.Instance.GetPersistentLocalPlayer();
        for (int i = 0; i < path.Count; i++)
        {
            if (!world.CanPlaceBlockAt(path[i], me))
                return Localization.Get("hsPipeClaimed");
        }
        TakePipes(player, need);
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
        string done = string.Format(Localization.Get("hsPipeLaid"), need, startName, endName);
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
        // Pipe that only runs past the new thing can't feed it from there; tee in from a little further along.
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
        if (route == null || route.Count == 0 || route.Count > MaxRun)
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
        return Dijkstra(seeds, goals, enter, 0);
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
                return null;
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

    // Cheapest run from any seed to any goal cell (both included). Bends cost a little so runs stay straight.
    static List<Vector3i> Dijkstra(List<Seed> seeds, HashSet<Vector3i> goals, Func<Vector3i, int> enter, int vertExtra)
    {
        if (seeds.Count == 0 || goals.Count == 0)
            return null;
        Vector3i min = seeds[0].Cell;
        Vector3i max = seeds[0].Cell;
        foreach (Seed s in seeds) { min = Vector3i.Min(min, s.Cell); max = Vector3i.Max(max, s.Cell); }
        foreach (Vector3i g in goals) { min = Vector3i.Min(min, g); max = Vector3i.Max(max, g); }
        min -= new Vector3i(Margin, Margin, Margin);
        max += new Vector3i(Margin, Margin, Margin);
        if (min.y < 1) min.y = 1;
        if (max.y > 253) max.y = 253;
        int sx = max.x - min.x + 1;
        int sy = max.y - min.y + 1;
        int sz = max.z - min.z + 1;
        if (sx <= 0 || sy <= 0 || sz <= 0)
            return null;
        if (sx > MaxRun + 2 * Margin || sy > MaxRun + 2 * Margin || sz > MaxRun + 2 * Margin || sx + sy + sz > MaxRun + 6 * Margin + 6)
        {
            routeDebug += " | search area too big " + sx + "x" + sy + "x" + sz;
            return null;
        }
        Vector3i[] dirs = HSWater.Dirs;
        int cells = sx * sy * sz;
        var dist = new int[cells * 6];
        var prev = new int[cells * 6];
        var cost = new int[cells];
        for (int i = 0; i < dist.Length; i++)
        {
            dist[i] = int.MaxValue;
            prev[i] = -1;
        }
        for (int i = 0; i < cost.Length; i++)
            cost[i] = int.MinValue;
        Func<Vector3i, int> index = p =>
        {
            if (p.x < min.x || p.y < min.y || p.z < min.z || p.x > max.x || p.y > max.y || p.z > max.z)
                return -1;
            return ((p.x - min.x) * sy + (p.y - min.y)) * sz + (p.z - min.z);
        };
        Func<int, Vector3i, int> enterAt = (idx, p) =>
        {
            if (cost[idx] == int.MinValue)
                cost[idx] = enter(p);
            return cost[idx];
        };
        var heap = new List<long>();
        foreach (Seed sd in seeds)
        {
            int idx = index(sd.Cell);
            if (idx < 0)
                continue;
            int c = enterAt(idx, sd.Cell);
            if (c < 0)
                continue;
            int s = idx * 6 + sd.Dir;
            if (c >= dist[s])
                continue;
            dist[s] = c;
            Push(heap, c, s);
        }
        int limit = (MaxRun + 1) * (StepCost + InBlockCost + TurnCost + vertExtra);
        while (heap.Count > 0)
        {
            long top = Pop(heap);
            int at = (int)(top >> 32);
            int s = (int)(top & 0xFFFFFFFF);
            if (at > limit)
                break;
            if (at != dist[s])
                continue;
            int idx = s / 6;
            int d = s % 6;
            var p = new Vector3i(idx / (sy * sz) + min.x, (idx / sz) % sy + min.y, idx % sz + min.z);
            if (goals.Contains(p))
            {
                var run = new List<Vector3i>();
                for (int t = s; t >= 0; t = prev[t])
                {
                    int ti = t / 6;
                    run.Add(new Vector3i(ti / (sy * sz) + min.x, (ti / sz) % sy + min.y, ti % sz + min.z));
                }
                run.Reverse();
                return run;
            }
            for (int e = 0; e < 6; e++)
            {
                if ((e ^ 1) == d)
                    continue;
                Vector3i n = p + dirs[e];
                int nidx = index(n);
                if (nidx < 0)
                    continue;
                int ec = enterAt(nidx, n);
                if (ec < 0)
                    continue;
                int ns = nidx * 6 + e;
                int nc = at + ec + (e != d ? TurnCost : 0) + (dirs[e].y != 0 ? vertExtra : 0);
                if (nc >= dist[ns])
                    continue;
                dist[ns] = nc;
                prev[ns] = s;
                Push(heap, nc, ns);
            }
        }
        return null;
    }

    static void Push(List<long> heap, int cost, int state)
    {
        heap.Add(((long)cost << 32) | (uint)state);
        int i = heap.Count - 1;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (heap[parent] <= heap[i])
                break;
            long t = heap[parent];
            heap[parent] = heap[i];
            heap[i] = t;
            i = parent;
        }
    }

    static long Pop(List<long> heap)
    {
        long top = heap[0];
        int last = heap.Count - 1;
        heap[0] = heap[last];
        heap.RemoveAt(last);
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
        else
        {
            route = string.Format(Localization.Get("hsPipeRoute"), path.Count, Bends(),
                have >= path.Count ? Localization.Get("hsPipeEnough") : string.Format(Localization.Get("hsPipeShort"), path.Count - have));
            if (hidden > 0)
                route += "\n" + string.Format(Localization.Get("hsPipeUnderFloor"), hidden);
        }
        return Localization.Get("hsPipeHudTitle") + "\n"
            + Localization.Get("hsPipeHudSystem") + " " + Where(start, startName)
            + (start != null ? "  " + string.Format(Localization.Get("hsPipeHudParts"), parts) : "") + "\n"
            + Localization.Get("hsPipeHudConnect") + " " + Where(end, endName) + "\n"
            + route + "\n"
            + string.Format(Localization.Get("hsPipeHudHave"), have) + "\n"
            + Localization.Get("hsPipeHudHelp");
    }

    // The planned route is drawn as real pipe, green when you carry enough, red when short.
    public static void UpdatePreview(EntityPlayerLocal player, bool holding)
    {
        bool want = holding && path != null && start != null && end != null;
        bool enough = want && CountPipes(player) >= path.Count;
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
