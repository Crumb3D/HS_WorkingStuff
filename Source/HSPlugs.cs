using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Wall sockets take as many plugs as they have outlets (block property HSPlugs): the wire tool refuses one more lead.
public static class HSPlugs
{
    public const string Property = "HSPlugs";
    static string denied;
    static float deniedUntil;

    public static int Limit(Block block)
    {
        string v;
        int n;
        if (block != null && block.Properties != null && block.Properties.Values.TryGetValue(Property, out v) && int.TryParse(v, out n))
            return n;
        return 0;
    }

    public static bool IsSocket(Block block)
    {
        return Limit(block) > 0;
    }

    // The server only updates PowerItem.Parent when a wire is laid, so parentPosition there is whatever was last loaded.
    public static bool Parent(TileEntityPowered te, out Vector3i parent)
    {
        parent = Vector3i.zero;
        if (te == null)
            return false;
        PowerItem item = te.GetPowerItem();
        if (item != null)
        {
            if (item.Parent == null)
                return false;
            parent = item.Parent.Position;
            return true;
        }
        if (!te.HasParent())
            return false;
        parent = te.GetParent();
        return true;
    }

    public static bool IsSocketAt(Vector3i pos)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        return world != null && IsSocket(world.GetBlock(pos).Block);
    }

    // Plug leads out of a socket, in wire order. A wire on to the next socket in a chain is not a plug,
    // and neither is a thermostat: it shares the socket with whatever is plugged in.
    public static List<Vector3i> Leads(WorldBase world, TileEntityPowered te)
    {
        var list = new List<Vector3i>();
        if (world == null || te == null || te.wireDataList == null)
            return list;
        for (int i = 0; i < te.wireDataList.Count; i++)
        {
            Block b = world.GetBlock(te.wireDataList[i]).Block;
            if (!IsSocket(b) && !(b is BlockHSWallThermostat))
                list.Add(te.wireDataList[i]);
        }
        return list;
    }

    public static bool Chained(WorldBase world, TileEntityPowered te)
    {
        if (world == null || te == null || te.wireDataList == null)
            return false;
        for (int i = 0; i < te.wireDataList.Count; i++)
            if (IsSocket(world.GetBlock(te.wireDataList[i]).Block))
                return true;
        return false;
    }

    static TileEntityPowered NextInRun(WorldBase world, TileEntityPowered te)
    {
        if (world == null || te == null || te.wireDataList == null)
            return null;
        for (int i = 0; i < te.wireDataList.Count; i++)
            if (IsSocket(world.GetBlock(te.wireDataList[i]).Block))
                return world.GetTileEntity(te.wireDataList[i]) as TileEntityPowered;
        return null;
    }

    // Sockets from the start of the run up to and including this one.
    public static int RunPosition(WorldBase world, TileEntityPowered te)
    {
        int n = 0;
        for (int guard = 0; te != null && guard < 64 && IsSocket(te.blockValue.Block); guard++)
        {
            n++;
            Vector3i parent;
            if (!Parent(te, out parent))
                break;
            te = world.GetTileEntity(parent) as TileEntityPowered;
        }
        return n;
    }

    // Sockets linked on after this one.
    public static int RunAfter(WorldBase world, TileEntityPowered te)
    {
        int n = 0;
        for (int guard = 0; guard < 64; guard++)
        {
            te = NextInRun(world, te);
            if (te == null)
                break;
            n++;
        }
        return n;
    }

    // Plug count, the onward socket link and the circuit it is on, for socket hover text.
    public static string Usage(WorldBase world, Vector3i pos, int limit)
    {
        var te = world != null ? world.GetTileEntity(pos) as TileEntityPowered : null;
        string text = "";
        if (limit > 0)
        {
            int used = te != null ? Mathf.Min(Leads(world, te).Count, limit) : 0;
            text = HSLoc.Format("hsPlugUsed", "Plugs: {0} of {1} in use", used, limit);
            if (Chained(world, te))
                text += "\n" + HSLoc.Get("hsSocketChained", "Linked on to the next socket");
            int at = RunPosition(world, te);
            int total = at + RunAfter(world, te);
            if (total > 1)
                text += "\n" + HSLoc.Format("hsSocketRun", "Socket {0} of {1} on this run (most {2})", at, total, HSWorkingStuffSettings.MaxSocketsPerRun);
        }
        string circuit = HSCircuits.CircuitLine(world, pos);
        if (!string.IsNullOrEmpty(circuit))
            text = text.Length > 0 ? text + "\n" + circuit : circuit;
        return text;
    }

    public static void Deny(int limit)
    {
        denied = limit == 1
            ? HSLoc.Get("hsPlugFullOne", "This socket takes one plug and it is already in use")
            : HSLoc.Format("hsPlugFullMany", "This socket takes {0} plugs and both are in use", limit);
        deniedUntil = Time.time + 1f;
    }

    public static void DenyReason(string text)
    {
        denied = text;
        deniedUntil = Time.time + 1f;
    }

    // Vanilla shows "ttCantHaveParent" straight after the refusal; this swaps in the reason.
    public static string DeniedText()
    {
        return denied != null && Time.time < deniedUntil ? denied : null;
    }

    public static string Status(WorldBase world, Vector3i pos, Block block)
    {
        int limit = Limit(block);
        var te = world != null ? world.GetTileEntity(pos) as TileEntityPowered : null;
        string power = te != null && te.IsPowered
            ? HSLoc.Get("hsPlugPowered", "Live")
            : HSLoc.Get("hsPlugNoPower", "No power: wire it from a generator, battery bank or solar bank");
        string usage = Usage(world, pos, limit);
        return string.IsNullOrEmpty(usage) ? power : power + "\n" + usage;
    }

    // Models carry hidden "<Model>_Plug0", "_Plug1" children: one shows per lead wired out of the socket.
    public static void ShowPlugs(Transform model, int used)
    {
        if (model == null)
            return;
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            int at = t.name.LastIndexOf("_Plug");
            int i;
            if (at < 0 || !int.TryParse(t.name.Substring(at + 5), out i))
                continue;
            bool on = i < used;
            if (t.gameObject.activeSelf != on)
                t.gameObject.SetActive(on);
        }
    }

    public static void ShowPlugs(WorldBase world, Vector3i pos, Transform model)
    {
        var te = world != null ? world.GetTileEntity(pos) as TileEntityPowered : null;
        ShowPlugs(model, Leads(world, te).Count);
        if (te != null)
            Cables(world, te, model);
    }

    // The lead meets the appliance on the face nearest the plug (its back, or its top when the socket is higher), never inside it.
    public static Vector3 LeadEnd(Transform appliance, Vector3 plug)
    {
        Bounds box = default(Bounds);
        bool any = false;
        foreach (var r in appliance.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is LineRenderer || r.GetType().Name == "ParticleSystemRenderer")
                continue;
            if (any)
                box.Encapsulate(r.bounds);
            else
            {
                box = r.bounds;
                any = true;
            }
        }
        if (!any)
            return appliance.position + Vector3.up * 0.1f;
        Vector3 end = box.ClosestPoint(plug);
        Vector3 away = plug - end;
        if (away.sqrMagnitude < 1e-6f)
            return new Vector3(box.center.x, box.max.y + 0.005f, box.center.z);
        return end + away.normalized * 0.005f;
    }

    // Vanilla wires only show with the wire tool out. A plugged-in appliance gets a black lead from the plug that is always there.
    public static void Cables(WorldBase world, TileEntityPowered te, Transform model)
    {
        if (model == null || te == null || Limit(te.blockValue.Block) <= 0)
            return;
        var plugs = new Transform[4];
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            int at = t.name.LastIndexOf("_Plug");
            int i;
            if (at >= 0 && int.TryParse(t.name.Substring(at + 5), out i) && i >= 0 && i < plugs.Length)
                plugs[i] = t;
        }
        List<Vector3i> leads = Leads(world, te);
        for (int i = 0; i < plugs.Length; i++)
        {
            Transform old = model.Find("HSCable" + i);
            bool want = plugs[i] != null && i < leads.Count;
            if (!want)
            {
                if (old != null)
                    old.gameObject.SetActive(false);
                continue;
            }
            var r = plugs[i].GetComponent<Renderer>();
            if (r == null)
                continue;
            Bounds pb = r.bounds;
            Vector3 a = new Vector3(pb.center.x, pb.min.y, pb.center.z);
            Vector3i childPos = leads[i];
            var child = world.GetTileEntity(childPos) as TileEntityPowered;
            Vector3 b = child != null && child.BlockTransform != null
                ? LeadEnd(child.BlockTransform, a)
                : childPos.ToVector3() + new Vector3(0.5f, 0.1f, 0.5f) - Origin.position;
            GameObject go = old != null ? old.gameObject : new GameObject("HSCable" + i);
            if (old == null)
            {
                go.transform.SetParent(model, false);
                go.layer = model.gameObject.layer;
            }
            go.SetActive(true);
            var line = go.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.startWidth = line.endWidth = 0.012f;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            line.sharedMaterial = r.sharedMaterial;
            const int n = 12;
            float sag = 0.04f + 0.06f * Vector3.Distance(a, b);
            line.positionCount = n + 1;
            for (int k = 0; k <= n; k++)
            {
                float s = k / (float)n;
                Vector3 p = Vector3.Lerp(a, b, s) + Vector3.down * (sag * 4f * s * (1f - s));
                line.SetPosition(k, model.InverseTransformPoint(p));
            }
        }
    }
}

// Double UK sockets switch each outlet on its own: block meta2 bit i = outlet i on, and plug i is child i of the socket.
// The vanilla switch state (meta bit 2) is kept as "either outlet on" so the trigger feeds the socket at all.
public static class HSOutlets
{
    public static bool IsDouble(Block b)
    {
        return b is BlockHSWallSwitch && HSPlugs.Limit(b) == 2;
    }

    public static bool On(BlockValue bv, int i)
    {
        return (bv.meta2 & (1 << i)) != 0;
    }

    public static bool Off(PowerItem item)
    {
        PowerItem parent = item != null ? item.Parent : null;
        if (parent == null || parent.Children == null)
            return false;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return false;
        BlockValue bv = world.GetBlock(parent.Position);
        if (bv.Block is BlockHSPanel)
            return !HSCircuits.Live(parent.Position, item.Position, bv);
        if (!IsDouble(bv.Block))
            return false;
        int i = LeadIndex(world, parent, item);
        return i >= 0 && i < 2 && !On(bv, i);
    }

    // Plug i is the i-th lead out of the socket; the onward socket link has no outlet (-1).
    static int LeadIndex(World world, PowerItem parent, PowerItem item)
    {
        int n = 0;
        for (int k = 0; k < parent.Children.Count; k++)
        {
            PowerItem ch = parent.Children[k];
            bool socket = HSPlugs.IsSocket(world.GetBlock(ch.Position).Block);
            if (ch == item)
                return socket ? -1 : n;
            if (!socket)
                n++;
        }
        return -1;
    }

    // Re-run the power share so a flicked outlet takes effect without the main switch changing.
    public static void Reshare(WorldBase world, Vector3i pos)
    {
        var te = world.GetTileEntity(pos) as TileEntityPowered;
        PowerItem item = te != null ? te.GetPowerItem() : null;
        PowerItem root = item != null ? item.GetRoot() : null;
        if (root != null)
            root.hasChangesLocal = true;
    }
}

[HarmonyPatch(typeof(PowerItem), "HandlePowerReceived")]
public static class HSPatchOutletPower
{
    // A cut outlet or circuit takes everything wired beyond it down too, once, not on every power pass.
    public static bool Prefix(PowerItem __instance)
    {
        try
        {
            if (!HSOutlets.Off(__instance))
                return true;
            if (__instance.isPowered)
                __instance.HandleDisconnect();
            return false;
        }
        catch
        {
            return true;
        }
    }
}

// Vanilla BlockSwitch tints every renderer green/red; our plates keep the materials they were built with.
public class HSOriginalMaterials : MonoBehaviour
{
    Renderer[] renderers;
    Material[][] materials;

    public static void Capture(Transform model)
    {
        if (model.GetComponent<HSOriginalMaterials>() != null)
            return;
        var keep = model.gameObject.AddComponent<HSOriginalMaterials>();
        keep.renderers = model.GetComponentsInChildren<Renderer>(true);
        keep.materials = new Material[keep.renderers.Length][];
        for (int i = 0; i < keep.renderers.Length; i++)
            keep.materials[i] = keep.renderers[i].sharedMaterials;
    }

    public static void Restore(Transform model)
    {
        var keep = model.GetComponent<HSOriginalMaterials>();
        if (keep == null || keep.renderers == null)
            return;
        for (int i = 0; i < keep.renderers.Length; i++)
            if (keep.renderers[i] != null)
                keep.renderers[i].sharedMaterials = keep.materials[i];
    }
}

[HarmonyPatch(typeof(TileEntityPowered), "DrawWires")]
public static class HSPatchSocketPlugShow
{
    static void Postfix(TileEntityPowered __instance)
    {
        try
        {
            if (GameManager.IsDedicatedServer || __instance.BlockTransform == null)
                return;
            if (HSPlugs.Limit(__instance.blockValue.Block) <= 0)
                return;
            HSPlugs.ShowPlugs(GameManager.Instance.World, __instance.ToWorldPos(), __instance.BlockTransform);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(TileEntityPowered), "CanHaveParent")]
public static class HSPatchSocketPlugs
{
    static void Postfix(TileEntityPowered __instance, IPowered powered, ref bool __result)
    {
        if (!__result)
            return;
        try
        {
            var parent = powered as TileEntityPowered;
            if (parent == null)
                return;
            int limit = HSPlugs.Limit(parent.blockValue.Block);
            if (limit <= 0 && __instance.blockValue.Block is BlockHSTV)
            {
                __result = false;
                HSPlugs.DenyReason(HSLoc.Get("hsTVSocketOnly", "A TV plugs into a socket, not straight into the power"));
                return;
            }
            if (limit <= 0 && __instance.blockValue.Block is BlockHSWallThermostat)
            {
                __result = false;
                HSPlugs.DenyReason(HSLoc.Get("hsStatSocketOnly", "A thermostat is wired from a socket, not straight into the power"));
                return;
            }
            if (limit <= 0 && __instance.blockValue.Block is BlockHSHifi)
            {
                __result = false;
                HSPlugs.DenyReason(HSLoc.Get("hsHifiSocketOnly", "A hi-fi plugs into a socket, not straight into the power"));
                return;
            }
            bool fromHifi = parent.blockValue.Block is BlockHSHifi;
            if (__instance.blockValue.Block is BlockHSHifiSpeaker && !fromHifi)
            {
                __result = false;
                HSPlugs.DenyReason(HSLoc.Get("hsSpeakerHifiOnly", "Speakers are wired from a hi-fi"));
                return;
            }
            if (fromHifi && !(__instance.blockValue.Block is BlockHSHifiSpeaker))
            {
                __result = false;
                HSPlugs.DenyReason(HSLoc.Get("hsHifiSpeakersOnly", "Only speakers wire out of a hi-fi"));
                return;
            }
            if (parent.wireDataList.Contains(__instance.ToWorldPos()))
                return;
            if (fromHifi)
            {
                if (HSHifi.Speakers(GameManager.Instance.World, parent.ToWorldPos()).Count >= HSHifi.MaxSpeakers)
                {
                    __result = false;
                    HSPlugs.DenyReason(HSLoc.Format("hsHifiSpeakersFull", "A hi-fi drives at most {0} speakers", HSHifi.MaxSpeakers));
                }
                return;
            }
            var panel = parent.blockValue.Block as BlockHSPanel;
            if (panel != null)
            {
                if (parent.ChildCount >= panel.Ways)
                {
                    __result = false;
                    HSPlugs.DenyReason(HSLoc.Format("hsPanelFull", "This panel has {0} ways and every one has a circuit on it", panel.Ways));
                }
                return;
            }
            if (limit <= 0)
                return;
            World world = GameManager.Instance.World;
            if (HSPlugs.IsSocket(__instance.blockValue.Block))
            {
                if (HSPlugs.Chained(world, parent))
                {
                    __result = false;
                    HSPlugs.DenyReason(HSLoc.Get("hsChainFull", "This socket already links on to another socket: one onward link per socket"));
                    return;
                }
                int max = HSWorkingStuffSettings.MaxSocketsPerRun;
                if (max > 0 && HSPlugs.RunPosition(world, parent) + 1 + HSPlugs.RunAfter(world, __instance) > max)
                {
                    __result = false;
                    HSPlugs.DenyReason(HSLoc.Format("hsChainMax", "A run takes at most {0} sockets: start a new circuit from the consumer unit", max));
                }
                return;
            }
            if (!(__instance.blockValue.Block is BlockHSWallThermostat) && HSPlugs.Leads(world, parent).Count >= limit)
            {
                __result = false;
                HSPlugs.Deny(limit);
            }
        }
        catch { }
    }
}

// UK switched socket, light switch, cooker switch: the vanilla switch with our wall-plate model.
// The rocker flicks: "<Model>_SwOn*" / "_SwOff*" children swap with the switch, "_Lamp*" lights when on and powered.
public class BlockHSWallSwitch : BlockSwitch
{
    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        if (_ebcd != null && _ebcd.transform != null)
            HSOriginalMaterials.Capture(_ebcd.transform);
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
        Refresh(_world, _blockPos, _blockValue, _ebcd);
    }

    public override void OnBlockValueChanged(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _oldBlockValue, BlockValue _newBlockValue)
    {
        base.OnBlockValueChanged(_world, _chunk, _blockPos, _oldBlockValue, _newBlockValue);
        if (_oldBlockValue.meta2 != _newBlockValue.meta2 && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && HSOutlets.IsDouble(this))
            HSOutlets.Reshare(_world, _blockPos);
        Refresh(_world, _blockPos, _newBlockValue, Entity(_world, _blockPos));
    }

    static readonly BlockActivationCommand[] outletCmds = new BlockActivationCommand[3]
    {
        new BlockActivationCommand("hsoutlet0", "electric_switch", true),
        new BlockActivationCommand("hsoutlet1", "electric_switch", true),
        new BlockActivationCommand("take", "hand", false)
    };

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        if (!HSOutlets.IsDouble(this))
            return base.GetBlockActivationCommands(_world, _blockValue, _blockPos, _entityFocusing);
        bool mine = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        outletCmds[2].enabled = mine && TakeDelay > 0f;
        return outletCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_commandName != "hsoutlet0" && _commandName != "hsoutlet1")
            return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
        if (!(_world.GetTileEntity(_blockPos) is TileEntityPoweredTrigger))
            return false;
        int i = _commandName == "hsoutlet0" ? 0 : 1;
        int bits = (_blockValue.meta2 ^ (1 << i)) & 3;
        _blockValue.meta2 = (byte)bits;
        _blockValue.meta = (byte)((_blockValue.meta & -3) | (bits != 0 ? 2 : 0));
        _world.SetBlockRPC(_blockPos, _blockValue);
        Audio.Manager.BroadcastPlay(_blockPos.ToVector3(), (bits & (1 << i)) != 0 ? "switch_up" : "switch_down");
        return true;
    }

    public override bool ActivateBlock(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, bool isOn, bool isPowered)
    {
        bool r = base.ActivateBlock(_world, _blockPos, _blockValue, isOn, isPowered);
        _blockValue.meta = (byte)((_blockValue.meta & -3) | (isOn ? 2 : 0));
        _blockValue.meta = (byte)((_blockValue.meta & -2) | (isPowered ? 1 : 0));
        Refresh(_world, _blockPos, _blockValue, Entity(_world, _blockPos));
        return r;
    }

    static BlockEntityData Entity(WorldBase world, Vector3i pos)
    {
        var w = world as World;
        return w != null && w.ChunkCache != null ? w.ChunkCache.GetBlockEntity(pos) : null;
    }

    static void Refresh(WorldBase world, Vector3i pos, BlockValue bv, BlockEntityData ebcd)
    {
        try
        {
            if (GameManager.IsDedicatedServer || ebcd == null || !ebcd.bHasTransform || ebcd.transform == null)
                return;
            Transform model = ebcd.transform;
            HSOriginalMaterials.Restore(model);
            bool on = IsSwitchOn(bv.meta);
            bool powered = (bv.meta & 1) != 0;
            bool perOutlet = HSOutlets.IsDouble(bv.Block);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                bool sw = n.Contains("_SwOn") || n.Contains("_SwOff");
                bool lit = on;
                if (sw && perOutlet)
                {
                    int i;
                    if (int.TryParse(n.Substring(n.Length - 1), out i))
                        lit = HSOutlets.On(bv, i);
                }
                if (n.Contains("_SwOn"))
                    t.gameObject.SetActive(lit);
                else if (n.Contains("_SwOff"))
                    t.gameObject.SetActive(!lit);
                else if (n.Contains("_Lamp"))
                    t.gameObject.SetActive(on && powered);
            }
            HSPlugs.ShowPlugs(world, pos, model);
        }
        catch { }
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        if (HSOutlets.IsDouble(this))
        {
            string onText = HSLoc.Get("hsOutletOn", "On");
            string offText = HSLoc.Get("hsOutletOff", "Off");
            return GetLocalizedBlockName() + "\n"
                + HSLoc.Format("hsOutletState", "Left socket: {0}    Right socket: {1}",
                    HSOutlets.On(_blockValue, 0) ? onText : offText, HSOutlets.On(_blockValue, 1) ? onText : offText) + "\n"
                + HSPlugs.Usage(_world, _blockPos, 2);
        }
        string text = base.GetActivationText(_world, _blockValue, _blockPos, _entityFocusing);
        string usage = HSPlugs.Usage(_world, _blockPos, HSPlugs.Limit(this));
        return string.IsNullOrEmpty(usage) ? text : text + "\n" + usage;
    }
}

// US outlet: always live, no switch, like the vanilla relay.
public class BlockHSWallRelay : BlockPowered
{
    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
        if (_ebcd != null)
            HSPlugs.ShowPlugs(_world, _blockPos, _ebcd.transform);
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSPlugs.Status(_world, _blockPos, this);
    }
}

// Keeps parentPosition true on the server so clients (and the save) know which socket a device hangs off.
public static class HSParentSync
{
    static readonly System.Reflection.FieldInfo field = AccessTools.Field(typeof(TileEntityPowered), "parentPosition");

    public static void Set(PowerItem child)
    {
        if (field == null || child == null || child.TileEntity == null)
            return;
        Vector3i want = child.Parent != null ? child.Parent.Position : new Vector3i(-9999, -9999, -9999);
        if ((Vector3i)field.GetValue(child.TileEntity) == want)
            return;
        field.SetValue(child.TileEntity, want);
        child.TileEntity.SetModified();
    }
}

[HarmonyPatch(typeof(PowerManager), "SetParent", new System.Type[] { typeof(PowerItem), typeof(PowerItem) })]
public static class HSPatchParentSyncSet
{
    static void Postfix(PowerItem child)
    {
        try { HSParentSync.Set(child); } catch { }
    }
}

[HarmonyPatch(typeof(PowerManager), "RemoveParent", new System.Type[] { typeof(PowerItem) })]
public static class HSPatchParentSyncRemove
{
    static void Postfix(PowerItem node)
    {
        try { HSParentSync.Set(node); } catch { }
    }
}
