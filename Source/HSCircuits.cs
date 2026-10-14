using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// UK consumer unit / US breaker panel. Every wire out of the panel is one circuit (one "way"), and a circuit is
// only live with a good fuse or a closed breaker fitted for it. Too much load blows the fuse or trips the breaker.
// Block meta2 bit 0 = main switch OFF.
public class BlockHSPanel : BlockPowered
{
    public int Ways = 6;

    static readonly BlockActivationCommand[] panelCmds = new BlockActivationCommand[7]
    {
        new BlockActivationCommand("hscircnext", "search", true),
        new BlockActivationCommand("hscircfit", "wrench", true),
        new BlockActivationCommand("hscircout", "hand", true),
        new BlockActivationCommand("hscircswitch", "electric_switch", true),
        new BlockActivationCommand("hscircname", "keypad", true),
        new BlockActivationCommand("hspanelmain", "electric_switch", true),
        new BlockActivationCommand("take", "hand", false)
    };

    public override void Init()
    {
        base.Init();
        Properties.ParseInt("HSWays", ref Ways);
        if (Ways < 1)
            Ways = 1;
        if (Ways > 8)
            Ways = 8;
    }

    public override void OnBlockAdded(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(world, _chunk, _blockPos, _blockValue, _addedByPlayer);
        if (!_blockValue.ischild)
            HSCircuits.Track(_blockPos);
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
        if (!_blockValue.ischild)
            HSCircuits.Track(_blockPos);
    }

    public override void OnBlockRemoved(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        HSCircuits.PanelRemoved(_blockPos);
        base.OnBlockRemoved(world, _chunk, _blockPos, _blockValue);
    }

    public override void OnBlockValueChanged(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _oldBlockValue, BlockValue _newBlockValue)
    {
        base.OnBlockValueChanged(_world, _chunk, _blockPos, _oldBlockValue, _newBlockValue);
        if (_oldBlockValue.meta2 != _newBlockValue.meta2 && HSWorkingStuffNet.IsAuthority)
            HSOutlets.Reshare(_world, _blockPos);
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
        if (GameManager.IsDedicatedServer || _ebcd == null || _ebcd.transform == null)
            return;
        var view = _ebcd.transform.GetComponent<HSPanelView>();
        if (view == null)
            view = _ebcd.transform.gameObject.AddComponent<HSPanelView>();
        view.Pos = _blockPos;
        view.Ways = Ways;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        bool mine = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        List<Vector3i> wires = HSCircuits.Wires(_world, _blockPos);
        int sel = HSCircuits.Selected(_blockPos, wires.Count);
        HSCircuits.Circuit c = sel >= 0 ? HSCircuits.Find(_blockPos, wires[sel]) : null;
        bool fitted = c != null && c.Amps > 0;
        bool breaker;
        panelCmds[0].enabled = wires.Count > 1;
        panelCmds[1].enabled = sel >= 0 && HSCircuits.HeldAmps(_entityFocusing as EntityPlayerLocal, out breaker) > 0;
        panelCmds[2].enabled = fitted;
        panelCmds[3].enabled = fitted && c.Breaker;
        panelCmds[4].enabled = sel >= 0;
        panelCmds[5].enabled = true;
        panelCmds[6].enabled = mine && TakeDelay > 0f;
        return panelCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_commandName == "hspanelmain")
        {
            _blockValue.meta2 = (byte)(_blockValue.meta2 ^ 1);
            _world.SetBlockRPC(_blockPos, _blockValue);
            Audio.Manager.BroadcastPlay(_blockPos.ToVector3(), HSCircuits.MainOn(_blockValue) ? "switch_up" : "switch_down");
            return true;
        }
        if (!_commandName.StartsWith("hscirc"))
            return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
        List<Vector3i> wires = HSCircuits.Wires(_world, _blockPos);
        int sel = HSCircuits.Selected(_blockPos, wires.Count);
        if (sel < 0)
        {
            GameManager.ShowTooltip(_player, HSLoc.Format("hsPanelNoCircuits", "No circuits: wire sockets, switches or lights out of the panel, one wire per circuit ({0} ways)", Ways));
            return true;
        }
        Vector3i child = wires[sel];
        switch (_commandName)
        {
            case "hscircnext":
                HSCircuits.Select(_blockPos, sel + 1, wires.Count);
                return true;
            case "hscircfit":
                HSCircuits.FitHeld(_player, _blockPos, child);
                return true;
            case "hscircout":
                HSCircuits.TakeOut(_player, _blockPos, child);
                return true;
            case "hscircswitch":
                HSCircuits.Flick(_player, _blockPos, child);
                return true;
            case "hscircname":
                HSCircuits.Circuit c = HSCircuits.Find(_blockPos, child);
                XUiC_HSCircuitNameWindowGroup.Open(_player, _blockPos, child, c != null ? c.Name : "");
                return true;
        }
        return false;
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return HSCircuits.PanelText(_world, _blockPos, _blockValue, this);
    }
}

public static class HSCircuits
{
    public const int Empty = 0;
    public const int On = 1;
    public const int Blown = 2;
    public const int Off = 3;

    public class Circuit
    {
        public int Amps;
        public bool Breaker;
        public int State;
        public string Name = "";
        public int Load;
        public float Over;
    }

    static readonly Dictionary<long, Dictionary<long, Circuit>> panels = new Dictionary<long, Dictionary<long, Circuit>>();
    static readonly HashSet<long> tracked = new HashSet<long>();
    static readonly Dictionary<long, int> selected = new Dictionary<long, int>();
    static readonly Dictionary<long, float> asked = new Dictionary<long, float>();
    static readonly List<Vector3i> none = new List<Vector3i>();
    static bool dirty;
    static float nextTick;
    static float nextSave;

    public static bool IsRequest(byte kind)
    {
        return kind >= HSWorkingStuffNet.CircuitFit && kind <= HSWorkingStuffNet.CircuitAsk;
    }

    public static bool MainOn(BlockValue bv)
    {
        return (bv.meta2 & 1) == 0;
    }

    public static void Track(Vector3i pos)
    {
        tracked.Add(HSWater.Key(pos));
    }

    public static void Clear()
    {
        panels.Clear();
        tracked.Clear();
        selected.Clear();
        asked.Clear();
        dirty = false;
    }

    public static Circuit Find(Vector3i panel, Vector3i child)
    {
        Dictionary<long, Circuit> map;
        Circuit c;
        if (panels.TryGetValue(HSWater.Key(panel), out map) && map.TryGetValue(HSWater.Key(child), out c))
            return c;
        return null;
    }

    static Circuit GetOrAdd(Vector3i panel, Vector3i child)
    {
        long pk = HSWater.Key(panel);
        Dictionary<long, Circuit> map;
        if (!panels.TryGetValue(pk, out map))
        {
            map = new Dictionary<long, Circuit>();
            panels[pk] = map;
        }
        long ck = HSWater.Key(child);
        Circuit c;
        if (!map.TryGetValue(ck, out c))
        {
            c = new Circuit();
            map[ck] = c;
        }
        return c;
    }

    public static int Watts(Circuit c)
    {
        return c == null ? 0 : Mathf.RoundToInt(c.Amps * HSWorkingStuffSettings.WattsPerAmp);
    }

    public static bool Live(Vector3i panel, Vector3i child, BlockValue panelBv)
    {
        if (!MainOn(panelBv))
            return false;
        Circuit c = Find(panel, child);
        return c != null && c.Amps > 0 && c.State == On;
    }

    // ---------------------------------------------------------------- fuse and breaker items

    public static string ItemName(int amps, bool breaker)
    {
        return (breaker ? "hsBreaker" : "hsFuse") + amps;
    }

    public static int AmpsOf(ItemClass ic, out bool breaker)
    {
        breaker = false;
        if (ic == null)
            return 0;
        string n = ic.GetItemName();
        string rest = null;
        if (n.StartsWith("hsBreaker"))
        {
            breaker = true;
            rest = n.Substring(9);
        }
        else if (n.StartsWith("hsFuse"))
            rest = n.Substring(6);
        int a;
        return rest != null && int.TryParse(rest, out a) && a > 0 && a < 256 ? a : 0;
    }

    public static int HeldAmps(EntityPlayerLocal player, out bool breaker)
    {
        breaker = false;
        if (player == null || player.inventory == null)
            return 0;
        return AmpsOf(player.inventory.holdingItem, out breaker);
    }

    static string FuseLabel(Circuit c)
    {
        return c.Breaker
            ? HSLoc.Format("hsBreakerRating", "{0}A breaker", c.Amps)
            : HSLoc.Format("hsFuseRating", "{0}A fuse", c.Amps);
    }

    static void GiveBack(EntityPlayer player, Circuit c)
    {
        if (c == null || c.Amps <= 0)
            return;
        string name = c.State == Blown && !c.Breaker ? "hsFuseBlown" : ItemName(c.Amps, c.Breaker);
        ItemValue iv = ItemClass.GetItem(name);
        if (iv != null && !iv.IsEmpty())
            HSWater.GiveOrDrop(player, new ItemStack(iv, 1));
    }

    // ---------------------------------------------------------------- player actions (client)

    public static void FitHeld(EntityPlayerLocal player, Vector3i panel, Vector3i child)
    {
        bool breaker;
        int amps = HeldAmps(player, out breaker);
        if (amps <= 0)
        {
            GameManager.ShowTooltip(player, HSLoc.Get("hsPanelHoldFuse", "Hold a fuse or a breaker to fit it"));
            return;
        }
        Circuit c = GetOrAdd(panel, child);
        GiveBack(player, c);
        player.inventory.DecHoldingItem(1);
        c.Amps = amps;
        c.Breaker = breaker;
        c.State = On;
        HSWorkingStuffNet.SendCircuit(HSWorkingStuffNet.CircuitFit, panel, player, amps | (breaker ? 0x100 : 0), ChildText(child, null));
        player.PlayOneShot("switch_place");
        GameManager.ShowTooltip(player, HSLoc.Format("hsPanelFitted", "Fitted a {0} for {1}", FuseLabel(c), Label(c, Index(panel, child))));
    }

    public static void TakeOut(EntityPlayerLocal player, Vector3i panel, Vector3i child)
    {
        Circuit c = Find(panel, child);
        if (c == null || c.Amps <= 0)
            return;
        GiveBack(player, c);
        c.Amps = 0;
        c.Breaker = false;
        c.State = Empty;
        HSWorkingStuffNet.SendCircuit(HSWorkingStuffNet.CircuitRemove, panel, player, 0, ChildText(child, null));
        player.PlayOneShot("switch_grab");
    }

    public static void Flick(EntityPlayerLocal player, Vector3i panel, Vector3i child)
    {
        Circuit c = Find(panel, child);
        if (c == null || c.Amps <= 0 || !c.Breaker)
            return;
        c.State = c.State == On ? Off : On;
        HSWorkingStuffNet.SendCircuit(HSWorkingStuffNet.CircuitSwitch, panel, player, c.State, ChildText(child, null));
        Audio.Manager.BroadcastPlay(panel.ToVector3(), c.State == On ? "switch_up" : "switch_down");
    }

    public static void Rename(EntityPlayerLocal player, Vector3i panel, Vector3i child, string name)
    {
        name = Clean(name);
        GetOrAdd(panel, child).Name = name;
        HSWorkingStuffNet.SendCircuit(HSWorkingStuffNet.CircuitName, panel, player, 0, ChildText(child, name));
    }

    public static List<Vector3i> Wires(WorldBase world, Vector3i panel)
    {
        var te = world != null ? world.GetTileEntity(panel) as TileEntityPowered : null;
        return te != null && te.wireDataList != null ? te.wireDataList : none;
    }

    static int Index(Vector3i panel, Vector3i child)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        return Wires(world, panel).IndexOf(child);
    }

    public static int Selected(Vector3i panel, int count)
    {
        if (count <= 0)
            return -1;
        int i;
        selected.TryGetValue(HSWater.Key(panel), out i);
        return Mathf.Clamp(i, 0, count - 1);
    }

    public static void Select(Vector3i panel, int i, int count)
    {
        selected[HSWater.Key(panel)] = count > 0 ? i % count : 0;
    }

    // ---------------------------------------------------------------- text

    static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        name = name.Replace("\r", " ").Replace("\n", " ").Replace("|", "/").Replace("[", "(").Replace("]", ")").Trim();
        return name.Length > 32 ? name.Substring(0, 32) : name;
    }

    static string ChildText(Vector3i child, string name)
    {
        string s = child.x + "," + child.y + "," + child.z;
        return name == null ? s : s + "\n" + name;
    }

    static bool ParseChild(string text, out Vector3i child, out string name)
    {
        child = Vector3i.zero;
        name = null;
        if (string.IsNullOrEmpty(text))
            return false;
        int nl = text.IndexOf('\n');
        string head = nl >= 0 ? text.Substring(0, nl) : text;
        if (nl >= 0)
            name = text.Substring(nl + 1);
        string[] p = head.Split(',');
        int x, y, z;
        if (p.Length != 3 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out z))
            return false;
        child = new Vector3i(x, y, z);
        return true;
    }

    public static string Label(Circuit c, int index)
    {
        if (c != null && !string.IsNullOrEmpty(c.Name))
            return c.Name;
        return HSLoc.Format("hsCircuitN", "Circuit {0}", Mathf.Max(index, 0) + 1);
    }

    static string StateText(Circuit c, bool mainOn)
    {
        if (c == null || c.Amps <= 0)
            return "[ff6040]" + HSLoc.Get("hsCircuitEmpty", "no fuse or breaker: dead") + "[-]";
        string fuse = FuseLabel(c);
        if (c.State == Blown)
            return fuse + " [ff4030]" + (c.Breaker
                ? HSLoc.Get("hsCircuitTripped", "TRIPPED: reset it")
                : HSLoc.Get("hsCircuitBlown", "BLOWN: take it out and fit a new fuse")) + "[-]";
        if (c.State == Off)
            return fuse + " [ffb030]" + HSLoc.Get("hsCircuitOff", "switched OFF") + "[-]";
        string load = HSLoc.Format("hsCircuitLoad", "{0} of {1} W", c.Load, Watts(c));
        return mainOn ? fuse + ", " + load : fuse + ", " + load + " [ffb030]" + HSLoc.Get("hsCircuitMainOff", "(main switch off)") + "[-]";
    }

    public static string PanelText(WorldBase world, Vector3i pos, BlockValue bv, BlockHSPanel block)
    {
        Ask(pos);
        var sb = new StringBuilder(block.GetLocalizedBlockName());
        bool main = MainOn(bv);
        sb.Append("\n").Append(main
            ? HSLoc.Get("hsPanelMainOn", "Main switch: on")
            : "[ffb030]" + HSLoc.Get("hsPanelMainOff", "Main switch: OFF, every circuit is dead") + "[-]");
        var te = world.GetTileEntity(pos) as TileEntityPowered;
        if (te != null && !te.IsPowered)
            sb.Append("\n").Append(HSLoc.Get("hsPanelNoPower", "No power in: wire the panel from a generator, battery bank or solar bank"));
        List<Vector3i> wires = Wires(world, pos);
        if (wires.Count == 0)
        {
            sb.Append("\n").Append(HSLoc.Format("hsPanelNoCircuits", "No circuits: wire sockets, switches or lights out of the panel, one wire per circuit ({0} ways)", block.Ways));
            return sb.ToString();
        }
        int sel = Selected(pos, wires.Count);
        for (int i = 0; i < wires.Count; i++)
        {
            Circuit c = Find(pos, wires[i]);
            sb.Append("\n").Append(i == sel ? "> " : "   ").Append(i + 1).Append(". ").Append(Label(c, i)).Append(": ").Append(StateText(c, main));
        }
        sb.Append("\n").Append(HSLoc.Format("hsPanelWays", "{0} of {1} ways in use. Hold a fuse or breaker to fit it in the selected circuit.", wires.Count, block.Ways));
        return sb.ToString();
    }

    // "Circuit: Kitchen sockets" on anything fed from a panel, however many sockets and switches are in between.
    public static string CircuitLine(WorldBase world, Vector3i pos)
    {
        if (world == null)
            return null;
        Vector3i cur = pos;
        for (int n = 0; n < 32; n++)
        {
            var te = world.GetTileEntity(cur) as TileEntityPowered;
            Vector3i parent;
            if (!HSPlugs.Parent(te, out parent))
                return null;
            BlockValue pbv = world.GetBlock(parent);
            if (pbv.Block is BlockHSPanel)
            {
                Ask(parent);
                Circuit c = Find(parent, cur);
                string line = HSLoc.Format("hsCircuitLine", "Circuit: {0}", Label(c, Wires(world, parent).IndexOf(cur)));
                if (c == null || c.Amps <= 0)
                    return line + " [ff6040](" + HSLoc.Get("hsCircuitNoFuse", "no fuse") + ")[-]";
                if (c.State == Blown)
                    return line + " [ff4030](" + (c.Breaker ? HSLoc.Get("hsCircuitTrippedShort", "breaker tripped") : HSLoc.Get("hsCircuitBlownShort", "fuse blown")) + ")[-]";
                if (c.State == Off || !MainOn(pbv))
                    return line + " [ffb030](" + HSLoc.Get("hsCircuitOffShort", "off at the panel") + ")[-]";
                return line;
            }
            cur = parent;
        }
        return null;
    }

    // ---------------------------------------------------------------- sync

    static int Pack(Circuit c)
    {
        return (c.Amps & 0xFF) | (c.Breaker ? 0x100 : 0) | ((c.State & 3) << 9) | (Mathf.Clamp(c.Load, 0, 0xFFFF) << 16);
    }

    static void Broadcast(Vector3i panel, Vector3i child, Circuit c)
    {
        HSWorkingStuffNet.BroadcastCircuit(panel, Pack(c), ChildText(child, c.Name ?? ""));
    }

    public static void ApplySync(Vector3i panel, int packed, string text)
    {
        Vector3i child;
        string name;
        if (!ParseChild(text, out child, out name))
            return;
        Circuit c = GetOrAdd(panel, child);
        c.Amps = packed & 0xFF;
        c.Breaker = (packed & 0x100) != 0;
        c.State = (packed >> 9) & 3;
        c.Load = (packed >> 16) & 0xFFFF;
        c.Name = name ?? "";
    }

    // A client asks the server for a panel's circuits the first time it looks at it (and again every 30 s).
    public static void Ask(Vector3i panel)
    {
        if (HSWorkingStuffNet.IsAuthority)
            return;
        long k = HSWater.Key(panel);
        float t;
        if (asked.TryGetValue(k, out t) && Time.time - t < 30f)
            return;
        asked[k] = Time.time;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayer player = world != null ? world.GetPrimaryPlayer() : null;
        if (player != null)
            HSWorkingStuffNet.SendCircuit(HSWorkingStuffNet.CircuitAsk, panel, player, 0, null);
    }

    // ---------------------------------------------------------------- server

    public static void Handle(byte kind, Vector3i panel, EntityPlayer player, int arg, string text)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        if (kind == HSWorkingStuffNet.CircuitAsk)
        {
            Dictionary<long, Circuit> map;
            if (player != null && panels.TryGetValue(HSWater.Key(panel), out map))
                foreach (var kv in map)
                    HSWorkingStuffNet.SendCircuitTo(player.entityId, panel, Pack(kv.Value), ChildText(HSWater.FromKey(kv.Key), kv.Value.Name ?? ""));
            return;
        }
        if (!(world.GetBlock(panel).Block is BlockHSPanel))
            return;
        Vector3i child;
        string name;
        if (!ParseChild(text, out child, out name))
            return;
        Circuit c = GetOrAdd(panel, child);
        if (kind == HSWorkingStuffNet.CircuitFit)
        {
            c.Amps = arg & 0xFF;
            c.Breaker = (arg & 0x100) != 0;
            c.State = c.Amps > 0 ? On : Empty;
            c.Over = 0f;
        }
        else if (kind == HSWorkingStuffNet.CircuitRemove)
        {
            c.Amps = 0;
            c.Breaker = false;
            c.State = Empty;
        }
        else if (kind == HSWorkingStuffNet.CircuitSwitch)
        {
            if (c.Amps <= 0 || !c.Breaker)
                return;
            c.State = arg == On ? On : Off;
            c.Over = 0f;
        }
        else if (kind == HSWorkingStuffNet.CircuitName)
            c.Name = Clean(name);
        else
            return;
        dirty = true;
        Broadcast(panel, child, c);
        HSOutlets.Reshare(world, panel);
    }

    // What the circuit asks for: everything wired on it that the switches on the way let power through to.
    static int Demand(PowerItem item, int depth)
    {
        if (item == null || depth > 24 || item is PowerSource)
            return 0;
        int sum = item.RequiredPower;
        if (item.Children == null || item.Children.Count == 0)
            return sum;
        var trig = item as PowerTrigger;
        bool all = trig != null ? trig.IsActive : item.PowerChildren();
        bool socketsOnly = !all && trig != null && HSPlugs.IsSocketAt(item.Position);
        if (!all && !socketsOnly)
            return sum;
        for (int i = 0; i < item.Children.Count; i++)
        {
            PowerItem ch = item.Children[i];
            if (HSOutlets.Off(ch))
                continue;
            if (socketsOnly && !HSPlugs.IsSocketAt(ch.Position))
                continue;
            sum += Demand(ch, depth + 1);
        }
        return sum;
    }

    public static void ServerTick()
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        if (dirty && Time.time >= nextSave)
        {
            nextSave = Time.time + 30f;
            Save();
        }
        if (tracked.Count == 0 || Time.time < nextTick)
            return;
        nextTick = Time.time + 1f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        PowerManager pm = PowerManager.Instance;
        if (world == null || pm == null)
            return;
        List<long> stale = null;
        foreach (long k in tracked)
        {
            Vector3i p = HSWater.FromKey(k);
            if (world.GetChunkFromWorldPos(p) == null)
                continue;
            BlockValue bv = world.GetBlock(p);
            if (!(bv.Block is BlockHSPanel))
            {
                if (stale == null)
                    stale = new List<long>();
                stale.Add(k);
                continue;
            }
            PowerItem pi;
            try
            {
                pi = pm.GetPowerItemByWorldPos(p);
            }
            catch
            {
                continue;
            }
            if (pi == null || pi.Children == null)
                continue;
            bool main = MainOn(bv);
            for (int i = 0; i < pi.Children.Count; i++)
                Check(world, p, pi, pi.Children[i], main);
        }
        if (stale != null)
            foreach (long k in stale)
                tracked.Remove(k);
    }

    static void Check(World world, Vector3i panel, PowerItem pi, PowerItem child, bool main)
    {
        Circuit c = GetOrAdd(panel, child.Position);
        bool changed = false;
        int load = Demand(child, 0);
        if (load != c.Load)
        {
            c.Load = load;
            changed = true;
        }
        if (main && c.State == On && c.Amps > 0 && pi.IsPowered && load > Watts(c))
        {
            c.Over += 1f;
            if (c.Over >= HSWorkingStuffSettings.OverloadSeconds)
            {
                c.State = Blown;
                c.Over = 0f;
                changed = true;
                dirty = true;
                Audio.Manager.BroadcastPlay(panel.ToVector3(), c.Breaker ? "switch_down" : "electric_fence_impact");
                HSOutlets.Reshare(world, panel);
            }
        }
        else
            c.Over = 0f;
        if (changed)
            Broadcast(panel, child.Position, c);
    }

    // Taking the panel down drops whatever fuses and breakers were in it.
    public static void PanelRemoved(Vector3i panel)
    {
        long pk = HSWater.Key(panel);
        tracked.Remove(pk);
        Dictionary<long, Circuit> map;
        if (!panels.TryGetValue(pk, out map))
            return;
        panels.Remove(pk);
        if (!HSWorkingStuffNet.IsAuthority || GameManager.Instance == null)
            return;
        dirty = true;
        foreach (Circuit c in map.Values)
        {
            if (c.Amps <= 0)
                continue;
            ItemValue iv = ItemClass.GetItem(c.State == Blown && !c.Breaker ? "hsFuseBlown" : ItemName(c.Amps, c.Breaker));
            if (iv != null && !iv.IsEmpty())
                GameManager.Instance.ItemDropServer(new ItemStack(iv, 1), panel.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f), Vector3.zero);
        }
    }

    // ---------------------------------------------------------------- save

    static string FilePath()
    {
        string dir = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "HSWorkingStuff.circuits");
    }

    // Chunks can load panels (and track them) before this runs, so only the circuit data is reset.
    public static void Load()
    {
        panels.Clear();
        dirty = false;
        try
        {
            string path = FilePath();
            if (path == null || !File.Exists(path))
                return;
            foreach (string line in File.ReadAllLines(path))
            {
                string[] f = line.Split(new[] { '|' }, 6);
                if (f.Length < 6)
                    continue;
                Vector3i panel, child;
                string unused;
                if (!ParseChild(f[0], out panel, out unused) || !ParseChild(f[1], out child, out unused))
                    continue;
                int amps, state;
                if (!int.TryParse(f[2], out amps) || !int.TryParse(f[4], out state))
                    continue;
                Circuit c = GetOrAdd(panel, child);
                c.Amps = amps;
                c.Breaker = f[3] == "1";
                c.State = state & 3;
                c.Name = Clean(f[5]);
                tracked.Add(HSWater.Key(panel));
            }
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] Could not read circuits: " + e.Message);
        }
    }

    public static void Save()
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        try
        {
            string path = FilePath();
            if (path == null || !Directory.Exists(Path.GetDirectoryName(path)))
                return;
            var lines = new List<string>();
            foreach (var p in panels)
            {
                string ps = ChildText(HSWater.FromKey(p.Key), null);
                foreach (var kv in p.Value)
                {
                    Circuit c = kv.Value;
                    if (c.Amps <= 0 && string.IsNullOrEmpty(c.Name))
                        continue;
                    lines.Add(ps + "|" + ChildText(HSWater.FromKey(kv.Key), null) + "|" + c.Amps + "|" + (c.Breaker ? "1" : "0") + "|" + c.State + "|" + (c.Name ?? ""));
                }
            }
            File.WriteAllLines(path, lines.ToArray());
            dirty = false;
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] Could not save circuits: " + e.Message);
        }
    }
}

// The panel model carries "<Model>_W<i>Fuse", "_W<i>Blown", "_W<i>On", "_W<i>Off" per way and "_MainOn" / "_MainOff":
// whichever matches the fitted fuse or breaker shows.
public class HSPanelView : MonoBehaviour
{
    public Vector3i Pos;
    public int Ways = 6;
    readonly Dictionary<string, GameObject> parts = new Dictionary<string, GameObject>();
    float next;

    void Start()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            int at = t.name.LastIndexOf('_');
            if (at >= 0)
                parts[t.name.Substring(at + 1)] = t.gameObject;
        }
        HSCircuits.Ask(Pos);
    }

    void Show(string part, bool on)
    {
        GameObject go;
        if (parts.TryGetValue(part, out go) && go.activeSelf != on)
            go.SetActive(on);
    }

    void Update()
    {
        if (Time.time < next)
            return;
        next = Time.time + 0.5f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        bool main = HSCircuits.MainOn(world.GetBlock(Pos));
        Show("MainOn", main);
        Show("MainOff", !main);
        List<Vector3i> wires = HSCircuits.Wires(world, Pos);
        for (int i = 0; i < Ways; i++)
        {
            HSCircuits.Circuit c = i < wires.Count ? HSCircuits.Find(Pos, wires[i]) : null;
            bool fitted = c != null && c.Amps > 0;
            string w = "W" + i;
            Show(w + "Fuse", fitted && !c.Breaker && c.State != HSCircuits.Blown);
            Show(w + "Blown", fitted && !c.Breaker && c.State == HSCircuits.Blown);
            Show(w + "On", fitted && c.Breaker && c.State == HSCircuits.On);
            Show(w + "Off", fitted && c.Breaker && c.State != HSCircuits.On);
        }
    }
}

// "Name circuit": one text box, Enter saves. The name shows on the panel and on everything fed from that circuit.
[Preserve]
public class XUiC_HSCircuitNameWindowGroup : XUiController
{
    public static string ID = "hsCircuitName";
    static Vector3i panel;
    static Vector3i child;
    static string current = "";

    XUiC_TextInput input;

    public override void Init()
    {
        base.Init();
        input = GetChildById("nameInput") as XUiC_TextInput;
        if (input != null)
            input.OnSubmitHandler += OnSubmit;
    }

    public static void Open(EntityPlayerLocal player, Vector3i p, Vector3i c, string name)
    {
        if (player == null)
            return;
        panel = p;
        child = c;
        current = name ?? "";
        player.PlayerUI.windowManager.Open(ID, true);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (input == null)
            return;
        input.Text = current;
        input.SetSelected();
    }

    void OnSubmit(XUiController sender, string text)
    {
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (player != null)
        {
            HSCircuits.Rename(player, panel, child, text);
            GameManager.ShowTooltip(player, string.IsNullOrEmpty(text != null ? text.Trim() : null)
                ? HSLoc.Get("hsCircuitNameCleared", "Circuit name cleared")
                : HSLoc.Format("hsCircuitNamed", "Circuit named {0}", text.Trim()));
        }
        xui.playerUI.windowManager.Close(ID);
    }
}

// Switches, lights and timer relays have their own HandlePowerReceived, so the outlet / circuit cut is patched on them too.
[HarmonyPatch(typeof(PowerTrigger), "HandlePowerReceived")]
public static class HSPatchCutTrigger
{
    static bool Prefix(PowerItem __instance)
    {
        return HSPatchOutletPower.Prefix(__instance);
    }
}

[HarmonyPatch(typeof(PowerConsumerToggle), "HandlePowerReceived")]
public static class HSPatchCutToggle
{
    static bool Prefix(PowerItem __instance)
    {
        return HSPatchOutletPower.Prefix(__instance);
    }
}

[HarmonyPatch(typeof(PowerTimerRelay), "HandlePowerReceived")]
public static class HSPatchCutTimer
{
    static bool Prefix(PowerItem __instance)
    {
        return HSPatchOutletPower.Prefix(__instance);
    }
}

// Sockets chain: a switched-off UK socket still feeds the next socket along, only its own plugs go dead.
[HarmonyPatch(typeof(PowerTrigger), "HandlePowerReceived")]
public static class HSPatchSocketChain
{
    static void Postfix(PowerTrigger __instance, ref ushort power)
    {
        try
        {
            if (power <= 0 || !__instance.isPowered || __instance.IsActive || __instance.Children == null || __instance.Children.Count == 0)
                return;
            if (!HSPlugs.IsSocketAt(__instance.Position))
                return;
            for (int i = 0; i < __instance.Children.Count && power > 0; i++)
            {
                PowerItem ch = __instance.Children[i];
                if (!HSPlugs.IsSocketAt(ch.Position))
                    continue;
                ch.HandlePowerReceived(ref power);
                // Vanilla only passes the power update on to switch children while this switch is off.
                if (!(ch is PowerTrigger))
                    ch.HandlePowerUpdate(true);
            }
        }
        catch { }
    }
}
