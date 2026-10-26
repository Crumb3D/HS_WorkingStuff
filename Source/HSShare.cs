using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

// A kettle, toaster or hi-fi can be placed into a socket's block. The socket stays as a plate on the wall
// behind it, and the power that fed the socket feeds the appliance. Picking the appliance up puts the socket back.
public static class HSShare
{
    public class Rec
    {
        public string Name;
        public byte Rotation;
        public byte Meta;
        public Vector3i Parent;
        public bool HasParent;
        public readonly List<Vector3i> Children = new List<Vector3i>();
    }

    class Join
    {
        public Vector3i Child;
        public Vector3i Parent;
        public float Until;
    }

    class Restore
    {
        public Vector3i Pos;
        public Rec Rec;
        public bool Placed;
        public float Until;
    }

    static readonly Dictionary<long, Rec> shares = new Dictionary<long, Rec>();
    static readonly List<Join> joins = new List<Join>();
    static readonly List<Restore> restores = new List<Restore>();
    static bool dirty;

    public static bool Small(Block block)
    {
        if (block == null)
            return false;
        string n = block.GetBlockName();
        return n == "hsKettle" || n == "hsToaster" || n == "hsToasterUS" || n == "hsHifi";
    }

    public static bool OnSocket(WorldBase world, Vector3i pos)
    {
        return world != null && HSPlugs.IsSocket(world.GetBlock(pos).Block);
    }

    public static void Reset()
    {
        shares.Clear();
        joins.Clear();
        restores.Clear();
        dirty = false;
    }

    public static void Load()
    {
        shares.Clear();
        dirty = false;
        try
        {
            string path = FilePath();
            if (path == null || !File.Exists(path))
                return;
            foreach (string line in File.ReadAllLines(path))
            {
                Vector3i pos;
                Rec rec;
                if (Read(line, out pos, out rec))
                    shares[HSWater.Key(pos)] = rec;
            }
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] Could not read shared sockets: " + e.Message);
        }
    }

    public static void Save()
    {
        if (!HSWorkingStuffNet.IsAuthority || !dirty)
            return;
        try
        {
            string path = FilePath();
            if (path == null || !Directory.Exists(Path.GetDirectoryName(path)))
                return;
            var lines = new List<string>();
            foreach (var kv in shares)
                lines.Add(Write(HSWater.FromKey(kv.Key), kv.Value));
            File.WriteAllLines(path, lines.ToArray());
            dirty = false;
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] Could not save shared sockets: " + e.Message);
        }
    }

    public static void Note(World world, Vector3i pos, BlockValue socket)
    {
        if (socket.Block == null)
            return;
        var rec = new Rec();
        rec.Name = socket.Block.GetBlockName();
        rec.Rotation = socket.rotation;
        rec.Meta = socket.meta;
        var te = world.GetTileEntity(pos) as TileEntityPowered;
        if (te != null)
        {
            rec.HasParent = te.HasParent();
            rec.Parent = te.GetParent();
            if (te.wireDataList != null)
            {
                for (int i = 0; i < te.wireDataList.Count; i++)
                    rec.Children.Add(te.wireDataList[i]);
            }
        }
        shares[HSWater.Key(pos)] = rec;
        dirty = true;
        Save();
        HSWorkingStuffNet.BroadcastShare(pos, Write(pos, rec));
    }

    public static void Drop(Vector3i pos)
    {
        if (!shares.Remove(HSWater.Key(pos)))
            return;
        for (int i = restores.Count - 1; i >= 0; i--)
            if (restores[i].Pos == pos)
                restores.RemoveAt(i);
        dirty = true;
        Save();
        HSWorkingStuffNet.BroadcastShare(pos, "");
    }

    public static void Claim(WorldBase world, Vector3i pos)
    {
        Rec rec;
        if (!shares.TryGetValue(HSWater.Key(pos), out rec))
            return;
        for (int i = restores.Count - 1; i >= 0; i--)
            if (restores[i].Pos == pos)
                restores.RemoveAt(i);
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        if (rec.HasParent)
            Want(pos, rec.Parent);
        for (int i = 0; i < rec.Children.Count; i++)
            if (rec.Children[i] != pos)
                Want(rec.Children[i], pos);
    }

    public static void QueueRestore(Vector3i pos)
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        Rec rec;
        if (!shares.TryGetValue(HSWater.Key(pos), out rec))
            return;
        for (int i = 0; i < restores.Count; i++)
            if (restores[i].Pos == pos)
                return;
        restores.Add(new Restore { Pos = pos, Rec = rec, Until = Time.time + 2f });
    }

    public static void Tick()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        if (HSWorkingStuffNet.IsAuthority)
        {
            for (int i = joins.Count - 1; i >= 0; i--)
            {
                Join j = joins[i];
                if (Time.time > j.Until || TryJoin(world, j.Child, j.Parent))
                    joins.RemoveAt(i);
            }
            for (int i = restores.Count - 1; i >= 0; i--)
            {
                Restore r = restores[i];
                BlockValue now = world.GetBlock(r.Pos);
                if (!now.isair && Small(now.Block))
                {
                    restores.RemoveAt(i);
                    continue;
                }
                if (!now.isair)
                {
                    shares.Remove(HSWater.Key(r.Pos));
                    dirty = true;
                    HSWorkingStuffNet.BroadcastShare(r.Pos, "");
                    restores.RemoveAt(i);
                    continue;
                }
                if (!r.Placed)
                {
                    Block b = Block.GetBlockByName(r.Rec.Name, false);
                    if (b == null)
                    {
                        restores.RemoveAt(i);
                        continue;
                    }
                    BlockValue bv = BlockValue.Air;
                    bv.type = b.blockID;
                    bv.rotation = r.Rec.Rotation;
                    bv.meta = r.Rec.Meta;
                    world.SetBlockRPC(r.Pos, bv);
                    r.Placed = true;
                    r.Until = Time.time + 3f;
                    continue;
                }
                if (HSPlugs.IsSocket(world.GetBlock(r.Pos).Block))
                {
                    if (r.Rec.HasParent)
                        Want(r.Pos, r.Rec.Parent);
                    for (int c = 0; c < r.Rec.Children.Count; c++)
                        Want(r.Rec.Children[c], r.Pos);
                    shares.Remove(HSWater.Key(r.Pos));
                    dirty = true;
                    HSWorkingStuffNet.BroadcastShare(r.Pos, "");
                    restores.RemoveAt(i);
                }
                else if (Time.time > r.Until)
                    restores.RemoveAt(i);
            }
            if (dirty)
                Save();
        }
    }

    public static void SendAll(int entityId)
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        foreach (var kv in shares)
            HSWorkingStuffNet.SendShareTo(entityId, HSWater.FromKey(kv.Key), Write(HSWater.FromKey(kv.Key), kv.Value));
    }

    public static void ApplySync(Vector3i pos, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            shares.Remove(HSWater.Key(pos));
            return;
        }
        Vector3i parsed;
        Rec rec;
        if (Read(text, out parsed, out rec))
            shares[HSWater.Key(pos)] = rec;
    }

    public static void Attach(Transform host, Vector3i pos)
    {
        if (host == null || GameManager.IsDedicatedServer)
            return;
        HSShareView view = host.GetComponent<HSShareView>();
        if (view == null)
            view = host.gameObject.AddComponent<HSShareView>();
        view.Pos = pos;
        view.Show();
    }

    public static bool Show(Transform host, Vector3i pos)
    {
        if (host == null)
            return false;
        Transform old = host.Find("HSSharedSocket");
        Rec rec;
        if (!shares.TryGetValue(HSWater.Key(pos), out rec) || string.IsNullOrEmpty(rec.Name))
        {
            if (old != null)
                UnityEngine.Object.Destroy(old.gameObject);
            return false;
        }
        if (old != null)
            return true;
        Block block = Block.GetBlockByName(rec.Name, false);
        if (block == null || block.Properties == null)
            return false;
        string model = block.Properties.GetString("Model");
        if (string.IsNullOrEmpty(model))
            return false;
        GameObject proto = LoadModel(model);
        if (proto == null)
            return false;
        GameObject go = UnityEngine.Object.Instantiate(proto);
        go.name = "HSSharedSocket";
        foreach (Component c in go.GetComponentsInChildren<Component>(true))
            NoHit(c);
        BlockValue bv = BlockValue.Air;
        bv.type = block.blockID;
        bv.rotation = rec.Rotation;
        Quaternion rot = block.shape != null ? block.shape.GetRotation(bv) : Quaternion.identity;
        go.transform.SetParent(host, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Inverse(host.rotation) * rot;
        return true;
    }

    static GameObject LoadModel(string model)
    {
        try
        {
            System.Reflection.MethodInfo open = null;
            System.Reflection.MethodInfo[] methods = typeof(DataLoader).GetMethods();
            for (int i = 0; i < methods.Length; i++)
            {
                System.Reflection.MethodInfo m = methods[i];
                if (m.Name != "LoadAsset" || !m.IsGenericMethodDefinition)
                    continue;
                System.Reflection.ParameterInfo[] ps = m.GetParameters();
                if (ps.Length >= 1 && ps[0].ParameterType == typeof(string))
                {
                    open = m;
                    break;
                }
            }
            if (open == null)
                return null;
            object[] args = open.GetParameters().Length == 1 ? new object[] { model } : new object[] { model, false };
            return open.MakeGenericMethod(typeof(GameObject)).Invoke(null, args) as GameObject;
        }
        catch
        {
            return null;
        }
    }

    static void NoHit(Component c)
    {
        if (c == null)
            return;
        for (Type t = c.GetType(); t != null; t = t.BaseType)
        {
            if (t.Name != "Collider")
                continue;
            System.Reflection.PropertyInfo p = t.GetProperty("enabled");
            if (p != null)
                p.SetValue(c, false, null);
            return;
        }
    }

    static void Want(Vector3i child, Vector3i parent)
    {
        if (child == parent || parent.y < 0)
            return;
        joins.Add(new Join { Child = child, Parent = parent, Until = Time.time + 8f });
    }

    static bool TryJoin(World world, Vector3i childPos, Vector3i parentPos)
    {
        var child = world.GetTileEntity(childPos) as TileEntityPowered;
        var parent = world.GetTileEntity(parentPos) as TileEntityPowered;
        if (child == null || parent == null || child.PowerItem == null || parent.PowerItem == null)
            return false;
        try
        {
            if (child.PowerItem.Parent != parent.PowerItem)
                PowerManager.Instance.SetParent(child.PowerItem, parent.PowerItem);
            parent.CreateWireDataFromPowerItem();
            parent.SendWireData();
            child.SetModified();
            return child.PowerItem.Parent == parent.PowerItem;
        }
        catch
        {
            return false;
        }
    }

    static string Write(Vector3i pos, Rec rec)
    {
        string kids = "";
        for (int i = 0; i < rec.Children.Count; i++)
        {
            if (i > 0)
                kids += ";";
            kids += HSLights.Text(rec.Children[i]);
        }
        Vector3i parent = rec.HasParent ? rec.Parent : new Vector3i(0, -9999, 0);
        return HSLights.Text(pos) + "|" + rec.Name + "|" + rec.Rotation + "|" + rec.Meta + "|" + HSLights.Text(parent) + "|" + kids;
    }

    static bool Read(string line, out Vector3i pos, out Rec rec)
    {
        pos = Vector3i.zero;
        rec = null;
        string[] f = line.Split('|');
        if (f.Length < 5)
            return false;
        int rot, meta;
        Vector3i parent;
        if (!HSLights.Pos(f[0], out pos) || string.IsNullOrEmpty(f[1]) || !int.TryParse(f[2], out rot) || !int.TryParse(f[3], out meta) || !HSLights.Pos(f[4], out parent))
            return false;
        rec = new Rec();
        rec.Name = f[1];
        rec.Rotation = (byte)rot;
        rec.Meta = (byte)meta;
        rec.HasParent = parent.y != -9999;
        rec.Parent = parent;
        if (f.Length > 5 && !string.IsNullOrEmpty(f[5]))
        {
            string[] kids = f[5].Split(';');
            for (int i = 0; i < kids.Length; i++)
            {
                Vector3i c;
                if (HSLights.Pos(kids[i], out c))
                    rec.Children.Add(c);
            }
        }
        return true;
    }

    static string FilePath()
    {
        string dir = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "HSWorkingStuff.shares");
    }
}

public class HSShareView : MonoBehaviour
{
    public Vector3i Pos;
    float until;

    void OnEnable()
    {
        until = Time.time + 15f;
    }

    void Update()
    {
        if (Show() || Time.time > until)
            enabled = false;
    }

    public bool Show()
    {
        return HSShare.Show(transform, Pos);
    }
}

[HarmonyPatch(typeof(GameManager), "SetBlocksRPC")]
public static class HSPatchSharePlace
{
    static void Prefix(List<BlockChangeInfo> _changes)
    {
        if (_changes == null)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        for (int i = 0; i < _changes.Count; i++)
        {
            BlockChangeInfo ch = _changes[i];
            if (ch == null || !ch.bChangeBlockValue)
                continue;
            Vector3i pos;
            if (!ch.blockValueRef.TryGetBlockPos(out pos))
                continue;
            BlockValue old = world.GetBlock(pos);
            if (!HSPlugs.IsSocket(old.Block))
                continue;
            if (HSShare.Small(ch.blockValue.Block))
                HSShare.Note(world, pos, old);
            else
                HSShare.Drop(pos);
        }
    }
}
