using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// Pipe laid by the Pipe Tool is not a block. Each cell is stored here with the faces its pipe runs to,
// and drawn as a mesh with no collider, so walls, floors and furniture can be built in the same cell.
// A cell holding a solid block or terrain hides its pipe; the water still flows through it.
public static class HSPipeNet
{
    static readonly Dictionary<long, byte> cells = new Dictionary<long, byte>();
    static bool dirty;
    static float nextSave;

    public static bool Has(Vector3i p)
    {
        return cells.ContainsKey(HSWater.Key(p));
    }

    public static int MaskAt(Vector3i p)
    {
        byte m;
        return cells.TryGetValue(HSWater.Key(p), out m) ? m : 0;
    }

    // 0 straight or a stub, 1 corner, 2 joint.
    public static int Shape(int mask)
    {
        int m = mask & 63;
        int arms = 0;
        for (int i = 0; i < 6; i++)
        {
            if ((m & (1 << i)) != 0)
                arms++;
        }
        if (arms >= 3)
            return 2;
        if (arms == 2 && m != 3 && m != 12 && m != 48)
            return 1;
        return 0;
    }

    public static int DirBit(Vector3i d)
    {
        Vector3i[] dirs = HSWater.Dirs;
        for (int i = 0; i < dirs.Length; i++)
        {
            if (dirs[i] == d)
                return 1 << i;
        }
        return 0;
    }

    // Laid pipe only joins along the faces it runs to, so a run passing under a sink does not feed it.
    // Plain blocks still join every neighbour.
    public static bool Joins(Vector3i from, Vector3i to)
    {
        byte m;
        if (cells.TryGetValue(HSWater.Key(from), out m))
            return (m & DirBit(to - from)) != 0;
        if (cells.TryGetValue(HSWater.Key(to), out m))
            return (m & DirBit(from - to)) != 0;
        return true;
    }

    // Every peer: the authority's copy, or what it sent us.
    public static void SetCell(Vector3i p, int mask)
    {
        long k = HSWater.Key(p);
        mask &= 63;
        if (mask == 0)
            cells.Remove(k);
        else
            cells[k] = (byte)mask;
        nextVisible = 0f;
        layoutDirty = true;
    }

    public static void Lay(Vector3i p, int mask)
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        byte old;
        cells.TryGetValue(HSWater.Key(p), out old);
        int merged = (old | mask) & 63;
        if (merged == 0)
            merged = old != 0 ? old : 63;
        SetCell(p, merged);
        dirty = true;
        HSWorkingStuffNet.BroadcastPipe(p, merged);
    }

    // Takes up every ghost pipe joined to this cell, face to face.
    public static int RemoveRun(Vector3i p, int[] shapes)
    {
        if (!HSWorkingStuffNet.IsAuthority || !Has(p))
            return 0;
        var q = new Queue<Vector3i>();
        var seen = new HashSet<long> { HSWater.Key(p) };
        q.Enqueue(p);
        int removed = 0;
        while (q.Count > 0 && removed < 4096)
        {
            Vector3i c = q.Dequeue();
            for (int i = 0; i < HSWater.Dirs.Length; i++)
            {
                Vector3i n = c + HSWater.Dirs[i];
                long nk = HSWater.Key(n);
                if (seen.Contains(nk) || !cells.ContainsKey(nk) || !Joins(c, n))
                    continue;
                seen.Add(nk);
                q.Enqueue(n);
            }
            if (shapes != null)
            {
                int shape = Shape(MaskAt(c));
                if (shape >= 0 && shape < shapes.Length)
                    shapes[shape]++;
            }
            SetCell(c, 0);
            HSWorkingStuffNet.BroadcastPipe(c, 0);
            removed++;
        }
        dirty = true;
        return removed;
    }

    // Shortest laid-pipe path between two seed sets (pipe cells next to each end). Null if none.
    public static List<Vector3i> FindStretch(IEnumerable<Vector3i> fromSeeds, IEnumerable<Vector3i> toSeeds)
    {
        var goals = new HashSet<long>();
        foreach (Vector3i g in toSeeds)
        {
            if (Has(g))
                goals.Add(HSWater.Key(g));
        }
        if (goals.Count == 0)
            return null;
        var q = new Queue<Vector3i>();
        var came = new Dictionary<long, long>();
        var seen = new HashSet<long>();
        foreach (Vector3i s in fromSeeds)
        {
            if (!Has(s))
                continue;
            long sk = HSWater.Key(s);
            if (!seen.Add(sk))
                continue;
            came[sk] = sk;
            q.Enqueue(s);
        }
        Vector3i hit = Vector3i.zero;
        bool found = false;
        int walked = 0;
        while (q.Count > 0 && walked < 8192)
        {
            Vector3i c = q.Dequeue();
            walked++;
            long ck = HSWater.Key(c);
            if (goals.Contains(ck))
            {
                hit = c;
                found = true;
                break;
            }
            for (int i = 0; i < HSWater.Dirs.Length; i++)
            {
                Vector3i n = c + HSWater.Dirs[i];
                long nk = HSWater.Key(n);
                if (seen.Contains(nk) || !cells.ContainsKey(nk) || !Joins(c, n))
                    continue;
                seen.Add(nk);
                came[nk] = ck;
                q.Enqueue(n);
            }
        }
        if (!found)
            return null;
        var path = new List<Vector3i>();
        long cur = HSWater.Key(hit);
        while (true)
        {
            path.Add(HSWater.FromKey(cur));
            long prev = came[cur];
            if (prev == cur)
                break;
            cur = prev;
            if (path.Count > 8192)
                break;
        }
        path.Reverse();
        return path;
    }

    // Remove only this stretch. Tees keep arms that leave the path (other branches stay).
    public static int RemoveStretch(List<Vector3i> path, int[] shapes)
    {
        if (!HSWorkingStuffNet.IsAuthority || path == null || path.Count == 0)
            return 0;
        var onPath = new HashSet<long>();
        for (int i = 0; i < path.Count; i++)
            onPath.Add(HSWater.Key(path[i]));
        int removed = 0;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3i c = path[i];
            if (!Has(c))
                continue;
            int oldMask = MaskAt(c);
            int keep = 0;
            for (int d = 0; d < HSWater.Dirs.Length; d++)
            {
                int bit = 1 << d;
                if ((oldMask & bit) == 0)
                    continue;
                Vector3i n = c + HSWater.Dirs[d];
                if (!Has(n) || !Joins(c, n))
                    continue;
                if (!onPath.Contains(HSWater.Key(n)))
                    keep |= bit;
            }
            if (keep == 0)
            {
                if (shapes != null)
                {
                    int shape = Shape(oldMask);
                    if (shape >= 0 && shape < shapes.Length)
                        shapes[shape]++;
                }
                // Drop reciprocal faces on neighbours so dead stubs do not linger.
                for (int d = 0; d < HSWater.Dirs.Length; d++)
                {
                    if ((oldMask & (1 << d)) == 0)
                        continue;
                    Vector3i n = c + HSWater.Dirs[d];
                    if (!Has(n))
                        continue;
                    int nb = MaskAt(n) & ~DirBit(c - n);
                    SetCell(n, nb);
                    HSWorkingStuffNet.BroadcastPipe(n, nb);
                }
                SetCell(c, 0);
                HSWorkingStuffNet.BroadcastPipe(c, 0);
                removed++;
            }
            else if (keep != oldMask)
            {
                SetCell(c, keep);
                HSWorkingStuffNet.BroadcastPipe(c, keep);
                // Cleared arms: clear the neighbour face that pointed along the stretch.
                int cleared = oldMask & ~keep;
                for (int d = 0; d < HSWater.Dirs.Length; d++)
                {
                    if ((cleared & (1 << d)) == 0)
                        continue;
                    Vector3i n = c + HSWater.Dirs[d];
                    if (!Has(n))
                        continue;
                    int nb = MaskAt(n) & ~DirBit(c - n);
                    SetCell(n, nb);
                    HSWorkingStuffNet.BroadcastPipe(n, nb);
                }
            }
        }
        if (removed > 0 || path.Count > 0)
            dirty = true;
        return removed;
    }

    public static void SendAll(int entityId)
    {
        foreach (var kv in cells)
            HSWorkingStuffNet.SendPipeTo(entityId, HSWater.FromKey(kv.Key), kv.Value);
    }

    public static void Tick()
    {
        if (dirty && Time.time >= nextSave)
        {
            nextSave = Time.time + 10f;
            Save();
        }
    }

    public static void Load()
    {
        cells.Clear();
        dirty = false;
        try
        {
            string dir = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(dir))
                return;
            string path = Path.Combine(dir, "HSWorkingStuff.pipes");
            if (!File.Exists(path))
                return;
            foreach (string line in File.ReadAllLines(path))
            {
                string[] f = line.Split(',');
                int x, y, z, m;
                if (f.Length < 4 || !int.TryParse(f[0], out x) || !int.TryParse(f[1], out y)
                    || !int.TryParse(f[2], out z) || !int.TryParse(f[3], out m))
                    continue;
                SetCell(new Vector3i(x, y, z), m);
            }
            Log.Out("[HSWorkingStuff] Loaded " + cells.Count + " laid pipes");
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] pipe load: " + e.Message);
        }
    }

    public static void Save()
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        dirty = false;
        try
        {
            string dir = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return;
            var lines = new List<string>(cells.Count);
            foreach (var kv in cells)
            {
                Vector3i p = HSWater.FromKey(kv.Key);
                lines.Add(p.x + "," + p.y + "," + p.z + "," + kv.Value);
            }
            File.WriteAllLines(Path.Combine(dir, "HSWorkingStuff.pipes"), lines.ToArray());
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] pipe save: " + e.Message);
        }
    }

    public static void Clear()
    {
        cells.Clear();
        preview.Clear();
        dirty = false;
        askedForPipes = false;
        layoutDirty = true;
        foreach (var go in shown.Values)
        {
            if (go != null)
                UnityEngine.Object.Destroy(go);
        }
        shown.Clear();
        shownCode.Clear();
    }

    // ------------------------------------------------------------ drawing (any peer with a screen)

    const float DrawRange = 96f;
    const float PipeRadius = 0.055f;
    const float CollarRadius = 0.07f;
    // How far off the cell centre a pipe sits when it runs along a wall or floor.
    const float Hug = 0.41f;
    // How far a pipe end reaches into the fixture it feeds.
    const float IntoDepth = 0.35f;
    static readonly Color PipeColor = new Color(0.62f, 0.64f, 0.66f, 1f);
    static readonly Color PreviewGood = new Color(0.25f, 0.95f, 0.35f, 1f);
    static readonly Color PreviewShort = new Color(1f, 0.3f, 0.2f, 1f);

    static GameObject root;
    static Material material;
    static Material previewGood;
    static Material previewShort;
    static bool materialLit;
    static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
    static readonly Dictionary<long, GameObject> shown = new Dictionary<long, GameObject>();
    static readonly Dictionary<long, int> shownCode = new Dictionary<long, int>();
    static float nextVisible;
    static bool askedForPipes;

    // The Pipe Tool's planned route, drawn as green (or red when short of pipe) pipe.
    static readonly Dictionary<long, byte> preview = new Dictionary<long, byte>();
    static bool previewEnough;

    static Dictionary<long, byte> combined = new Dictionary<long, byte>();
    static readonly Dictionary<long, int> offsets = new Dictionary<long, int>();
    static bool layoutDirty = true;
    static float nextLayout;

    public static void SetPreview(List<Vector3i> path, List<int> masks, bool enough)
    {
        preview.Clear();
        if (path != null && masks != null)
        {
            for (int i = 0; i < path.Count && i < masks.Count; i++)
            {
                long k = HSWater.Key(path[i]);
                byte old;
                preview.TryGetValue(k, out old);
                preview[k] = (byte)((old | masks[i]) & 63);
            }
        }
        previewEnough = enough;
        layoutDirty = true;
        nextVisible = 0f;
    }

    public static void ClientTick()
    {
        if (GameManager.IsDedicatedServer)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        EntityPlayerLocal player = world.GetPrimaryPlayer();
        if (player == null)
            return;
        if (!askedForPipes)
        {
            askedForPipes = true;
            if (!HSWorkingStuffNet.IsAuthority)
                HSWorkingStuffNet.SendWater(HSWorkingStuffNet.PipeHello, Vector3i.zero, player, 0);
        }
        if (root == null)
        {
            root = new GameObject("HSPipeNet");
            UnityEngine.Object.DontDestroyOnLoad(root);
        }
        root.transform.position = -Origin.position;
        float now = Time.time;
        if (now < nextVisible)
            return;
        nextVisible = now + 0.5f;
        if (layoutDirty || now >= nextLayout)
            Relayout(world);
        Vector3 at = player.GetPosition();
        float r2 = DrawRange * DrawRange;
        List<long> gone = null;
        foreach (var kv in shown)
        {
            if (!combined.ContainsKey(kv.Key))
            {
                if (gone == null)
                    gone = new List<long>();
                gone.Add(kv.Key);
            }
        }
        if (gone != null)
        {
            for (int i = 0; i < gone.Count; i++)
            {
                if (shown[gone[i]] != null)
                    UnityEngine.Object.Destroy(shown[gone[i]]);
                shown.Remove(gone[i]);
                shownCode.Remove(gone[i]);
            }
        }
        foreach (var kv in combined)
        {
            Vector3i p = HSWater.FromKey(kv.Key);
            Vector3 c = new Vector3(p.x + 0.5f, p.y + 0.5f, p.z + 0.5f);
            bool show = (c - at).sqrMagnitude <= r2 && world.GetChunkFromWorldPos(p) != null && Open(world.GetBlock(p));
            GameObject go;
            shown.TryGetValue(kv.Key, out go);
            if (!show)
            {
                if (go != null && go.activeSelf)
                    go.SetActive(false);
                continue;
            }
            if (go == null)
            {
                go = MakeCell(c);
                if (go == null)
                    continue;
                shown[kv.Key] = go;
            }
            int off;
            offsets.TryGetValue(kv.Key, out off);
            int look = cells.ContainsKey(kv.Key) ? 0 : (previewEnough ? 1 : 2);
            int into = IntoFixtures(world, p, kv.Value);
            int code = kv.Value | (off << 6) | (look << 11) | (into << 13);
            int drawn;
            if (!shownCode.TryGetValue(kv.Key, out drawn) || drawn != code)
            {
                go.GetComponent<MeshFilter>().sharedMesh = MeshFor(kv.Value, off, into);
                Material m = look == 0 ? material : look == 1 ? previewGood : previewShort;
                if (m != null)
                    go.GetComponent<MeshRenderer>().sharedMaterial = m;
                shownCode[kv.Key] = code;
            }
            if (!go.activeSelf)
                go.SetActive(true);
        }
    }

    static bool Open(BlockValue bv)
    {
        if (bv.isair)
            return true;
        Block b = bv.Block;
        if (b == null || b.shape == null)
            return true;
        if (b.shape.IsSolidCube || b.shape.IsTerrain())
            return false;
        // Building blocks (concrete, wood...) are shaped blocks, only flagged solid if their model says so.
        // All six sides full is a full cube whatever the flag says.
        if (bv.ischild || !(b.shape is BlockShapeNew))
            return true;
        return (b.shape.getFacesDrawnFullBitfield(bv) & 63) != 63;
    }

    // Something a pipe can be clipped to: a full block or terrain.
    public static bool Surface(BlockValue bv)
    {
        return !Open(bv);
    }

    // Each run sits against the wall or floor beside it. Cells joined along an axis must share their offset
    // across that axis, so offsets are pooled per joined line and the pool votes on which side to hug.
    static void Relayout(World world)
    {
        layoutDirty = false;
        nextLayout = Time.time + 3f;
        var all = new Dictionary<long, byte>(cells);
        foreach (var kv in preview)
        {
            byte old;
            all.TryGetValue(kv.Key, out old);
            all[kv.Key] = (byte)(old | kv.Value);
        }
        combined = all;
        offsets.Clear();
        int n = all.Count;
        if (n == 0)
            return;
        var keys = new long[n];
        var masks = new byte[n];
        var index = new Dictionary<long, int>(n);
        int i = 0;
        foreach (var kv in all)
        {
            keys[i] = kv.Key;
            masks[i] = kv.Value;
            index[kv.Key] = i;
            i++;
        }
        var parent = new int[n * 3];
        for (i = 0; i < parent.Length; i++)
            parent[i] = i;
        Vector3i[] dirs = HSWater.Dirs;
        for (i = 0; i < n; i++)
        {
            Vector3i p = HSWater.FromKey(keys[i]);
            for (int d = 0; d < 6; d += 2)
            {
                if ((masks[i] & (1 << d)) == 0)
                    continue;
                int j;
                if (!index.TryGetValue(HSWater.Key(p + dirs[d]), out j) || (masks[j] & (1 << (d + 1))) == 0)
                    continue;
                int axis = d / 2;
                for (int other = 0; other < 3; other++)
                {
                    if (other != axis)
                        Union(parent, i * 3 + other, j * 3 + other);
                }
            }
        }
        var neg = new int[n * 3];
        var pos = new int[n * 3];
        for (i = 0; i < n; i++)
        {
            Vector3i p = HSWater.FromKey(keys[i]);
            if (!Open(world.GetBlock(p)))
                continue;
            for (int axis = 0; axis < 3; axis++)
            {
                if ((masks[i] & ~(3 << (axis * 2)) & 63) == 0)
                    continue;
                int r = Find(parent, i * 3 + axis);
                if (Surface(world.GetBlock(p + dirs[axis * 2 + 1])))
                    neg[r] += axis == 1 ? 2 : 1;
                if (Surface(world.GetBlock(p + dirs[axis * 2])))
                    pos[r] += 1;
            }
        }
        for (i = 0; i < n; i++)
        {
            int code = 0;
            int scale = 1;
            for (int axis = 0; axis < 3; axis++)
            {
                int r = Find(parent, i * 3 + axis);
                int side = neg[r] == 0 && pos[r] == 0 ? 0 : (neg[r] >= pos[r] ? -1 : 1);
                code += (side + 1) * scale;
                scale *= 3;
            }
            offsets[keys[i]] = code;
        }
    }

    static int Find(int[] parent, int a)
    {
        while (parent[a] != a)
        {
            parent[a] = parent[parent[a]];
            a = parent[a];
        }
        return a;
    }

    static void Union(int[] parent, int a, int b)
    {
        a = Find(parent, a);
        b = Find(parent, b);
        if (a != b)
            parent[b] = a;
    }

    static GameObject MakeCell(Vector3 worldCenter)
    {
        Material m = PipeMaterial();
        if (m == null)
            return null;
        var go = new GameObject("HSPipe");
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = worldCenter;
        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;
        return go;
    }

    static Material PipeMaterial()
    {
        if (material != null)
            return material;
        string[] lit = { "Standard", "Legacy Shaders/Diffuse" };
        for (int i = 0; i < lit.Length && material == null; i++)
        {
            Shader s = Shader.Find(lit[i]);
            if (s != null)
            {
                material = new Material(s);
                materialLit = true;
            }
        }
        if (material == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s != null)
                material = new Material(s);
        }
        if (material == null)
            return null;
        material.color = materialLit ? PipeColor : Color.white;
        if (materialLit && material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.6f);
            material.SetFloat("_Glossiness", 0.45f);
        }
        previewGood = Tinted(PreviewGood);
        previewShort = Tinted(PreviewShort);
        Log.Out("[HSWorkingStuff] Pipe shader: " + material.shader.name);
        return material;
    }

    static Material Tinted(Color c)
    {
        var m = new Material(material);
        m.color = c;
        if (materialLit && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.35f);
        }
        return m;
    }

    // Arms that end at a tank, sink, boiler or radiator rather than more pipe. Those models sit back from the
    // block edge, so the pipe carries on into them instead of stopping short.
    // A sink standing on a cabinet is fed up through the cabinet; the pipe stops under the worktop.
    static int IntoFixtures(World world, Vector3i p, byte mask)
    {
        int into = 0;
        bool inBlock = !world.GetBlock(p).isair;
        for (int d = 0; d < 6; d++)
        {
            if ((mask & (1 << d)) == 0)
                continue;
            Vector3i n = p + HSWater.Dirs[d];
            if (combined.ContainsKey(HSWater.Key(n)))
                continue;
            BlockValue bv = world.GetBlock(n);
            if (bv.isair || !Open(bv))
                continue;
            if (d == 2 && inBlock && HSWater.IsSinkBlock(bv.Block))
                continue;
            into |= 1 << d;
        }
        return into;
    }

    static Mesh MeshFor(byte mask, int off, int into)
    {
        int m = mask & 63;
        int key = m | (off << 6) | ((into & 63) << 11);
        Mesh cached;
        if (meshes.TryGetValue(key, out cached) && cached != null)
            return cached;
        Vector3 o = new Vector3(((off % 3) - 1) * Hug, (((off / 3) % 3) - 1) * Hug, ((off / 9) - 1) * Hug);
        var b = new MeshBits();
        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        int arms = 0;
        for (int i = 0; i < 6; i++)
        {
            if ((m & (1 << i)) == 0)
                continue;
            arms++;
            Vector3 face = o;
            face[i / 2] = dirs[i][i / 2] * 0.5f;
            Vector3 tip = (into & (1 << i)) != 0 ? face + dirs[i] * IntoDepth : face;
            b.Cylinder(o, tip, PipeRadius, 12);
            b.Cylinder(face - dirs[i] * 0.05f, face, CollarRadius, 12);
        }
        bool straight = arms == 2 && (m == 3 || m == 12 || m == 48);
        if (!straight)
            b.Cube(o, PipeRadius * 2.5f);
        var mesh = new Mesh { name = "HSPipe" + key };
        mesh.vertices = b.Verts.ToArray();
        mesh.normals = b.Normals.ToArray();
        mesh.colors = b.Shade(PipeColor);
        mesh.triangles = b.Tris.ToArray();
        mesh.RecalculateBounds();
        meshes[key] = mesh;
        return mesh;
    }

    class MeshBits
    {
        public readonly List<Vector3> Verts = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<int> Tris = new List<int>();

        public void Cylinder(Vector3 a, Vector3 b, float r, int segs)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 side = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 u = Vector3.Cross(axis, side).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            int baseIdx = Verts.Count;
            for (int i = 0; i <= segs; i++)
            {
                float t = i * Mathf.PI * 2f / segs;
                Vector3 n = u * Mathf.Cos(t) + v * Mathf.Sin(t);
                Verts.Add(a + n * r);
                Normals.Add(n);
                Verts.Add(b + n * r);
                Normals.Add(n);
            }
            for (int i = 0; i < segs; i++)
            {
                int s0 = baseIdx + i * 2;
                int s1 = s0 + 2;
                Tris.Add(s0); Tris.Add(s1); Tris.Add(s0 + 1);
                Tris.Add(s1); Tris.Add(s1 + 1); Tris.Add(s0 + 1);
            }
            Cap(b, axis, u, v, r, segs);
        }

        void Cap(Vector3 c, Vector3 n, Vector3 u, Vector3 v, float r, int segs)
        {
            int center = Verts.Count;
            Verts.Add(c);
            Normals.Add(n);
            for (int i = 0; i <= segs; i++)
            {
                float t = i * Mathf.PI * 2f / segs;
                Verts.Add(c + (u * Mathf.Cos(t) + v * Mathf.Sin(t)) * r);
                Normals.Add(n);
            }
            for (int i = 0; i < segs; i++)
            {
                Tris.Add(center); Tris.Add(center + 1 + i); Tris.Add(center + 2 + i);
            }
        }

        public void Cube(Vector3 c, float size)
        {
            float h = size * 0.5f;
            Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int f = 0; f < 6; f++)
            {
                Vector3 normal = n[f];
                Vector3 side = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 u = Vector3.Cross(normal, side);
                Vector3 v = Vector3.Cross(normal, u);
                int i0 = Verts.Count;
                Vector3 fc = c + normal * h;
                Verts.Add(fc + (-u - v) * h);
                Verts.Add(fc + (u - v) * h);
                Verts.Add(fc + (u + v) * h);
                Verts.Add(fc + (-u + v) * h);
                for (int k = 0; k < 4; k++)
                    Normals.Add(normal);
                Tris.Add(i0); Tris.Add(i0 + 1); Tris.Add(i0 + 2);
                Tris.Add(i0); Tris.Add(i0 + 2); Tris.Add(i0 + 3);
            }
        }

        // Baked light for the unlit fallback shader; a lit shader ignores vertex colours.
        public Color[] Shade(Color baseColor)
        {
            Vector3 light = new Vector3(0.4f, 1f, 0.3f).normalized;
            var cols = new Color[Normals.Count];
            for (int i = 0; i < cols.Length; i++)
            {
                float k = 0.55f + 0.45f * Mathf.Max(0f, Vector3.Dot(Normals[i], light));
                cols[i] = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, 1f);
            }
            return cols;
        }
    }
}
