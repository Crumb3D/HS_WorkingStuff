using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

// A POI light is a BlockLight, so the wire tool cannot power it. The light switch remembers the ones you
// point at, and turns them on only while the switch is on and the switch itself has power.
public static class HSLights
{
    const int MaxPerSwitch = 16;
    const int MaxReach = 30;

    static readonly Dictionary<long, List<Vector3i>> links = new Dictionary<long, List<Vector3i>>();
    static bool dirty;
    static float next;

    public static void Load()
    {
        links.Clear();
        dirty = false;
        try
        {
            string path = FilePath();
            if (path == null || !File.Exists(path))
                return;
            foreach (string line in File.ReadAllLines(path))
            {
                string[] f = line.Split('|');
                if (f.Length < 2)
                    continue;
                Vector3i sw, light;
                if (!Pos(f[0], out sw) || !Pos(f[1], out light))
                    continue;
                Add(sw, light);
            }
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] Could not read lights: " + e.Message);
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
            foreach (var kv in links)
            {
                Vector3i sw = HSWater.FromKey(kv.Key);
                foreach (Vector3i light in kv.Value)
                    lines.Add(Text(sw) + "|" + Text(light));
            }
            File.WriteAllLines(path, lines.ToArray());
            dirty = false;
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] Could not save lights: " + e.Message);
        }
    }

    public static void Link(Vector3i sw, string text)
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        Vector3i light;
        if (!Pos(text, out light))
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        if (!HSPlugs.IsLightSwitch(world.GetBlock(sw).Block))
            return;
        if (!(world.GetBlock(light).Block is BlockLight))
            return;
        if (Reach(sw, light) > MaxReach * MaxReach)
            return;
        Forget(light);
        if (!Add(sw, light))
            return;
        dirty = true;
        Save();
        ApplyOne(world, sw, light);
    }

    public static void Tick()
    {
        if (!HSWorkingStuffNet.IsAuthority || Time.time < next)
            return;
        next = Time.time + 0.5f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || links.Count == 0)
            return;
        var drop = new List<long>();
        foreach (var kv in links)
        {
            Vector3i sw = HSWater.FromKey(kv.Key);
            if (!HSPlugs.IsLightSwitch(world.GetBlock(sw).Block))
            {
                drop.Add(kv.Key);
                continue;
            }
            bool want = Live(world, sw);
            for (int i = kv.Value.Count - 1; i >= 0; i--)
            {
                Vector3i light = kv.Value[i];
                if (!(world.GetBlock(light).Block is BlockLight))
                {
                    kv.Value.RemoveAt(i);
                    dirty = true;
                    continue;
                }
                Set(world, light, want);
            }
            if (kv.Value.Count == 0)
                drop.Add(kv.Key);
        }
        for (int i = 0; i < drop.Count; i++)
            links.Remove(drop[i]);
        if (dirty)
            Save();
    }

    static void ApplyOne(World world, Vector3i sw, Vector3i light)
    {
        if (world.GetBlock(light).Block is BlockLight)
            Set(world, light, Live(world, sw));
    }

    static bool Live(World world, Vector3i sw)
    {
        if ((world.GetBlock(sw).meta & 2) == 0)
            return false;
        var te = world.GetTileEntity(sw) as TileEntityPowered;
        return te != null && te.IsPowered;
    }

    static void Set(World world, Vector3i light, bool on)
    {
        BlockValue bv = world.GetBlock(light);
        bool lit = (bv.meta & 2) != 0;
        if (lit == on)
            return;
        bv.meta = (byte)((bv.meta & ~2) | (on ? 2 : 0));
        world.SetBlockRPC(light, bv);
    }

    static bool Add(Vector3i sw, Vector3i light)
    {
        long key = HSWater.Key(sw);
        List<Vector3i> list;
        if (!links.TryGetValue(key, out list))
        {
            list = new List<Vector3i>();
            links[key] = list;
        }
        for (int i = 0; i < list.Count; i++)
            if (list[i] == light)
                return true;
        if (list.Count >= MaxPerSwitch)
            return false;
        list.Add(light);
        return true;
    }

    static void Forget(Vector3i light)
    {
        var drop = new List<long>();
        foreach (var kv in links)
        {
            kv.Value.RemoveAll(p => p == light);
            if (kv.Value.Count == 0)
                drop.Add(kv.Key);
        }
        for (int i = 0; i < drop.Count; i++)
            links.Remove(drop[i]);
    }

    static int Reach(Vector3i a, Vector3i b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        int dz = a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }

    static string FilePath()
    {
        string dir = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "HSWorkingStuff.lights");
    }

    public static string Text(Vector3i p)
    {
        return p.x + "," + p.y + "," + p.z;
    }

    public static bool Pos(string text, out Vector3i p)
    {
        p = Vector3i.zero;
        if (string.IsNullOrEmpty(text))
            return false;
        string[] f = text.Split(',');
        int x, y, z;
        if (f.Length < 3 || !int.TryParse(f[0], out x) || !int.TryParse(f[1], out y) || !int.TryParse(f[2], out z))
            return false;
        p = new Vector3i(x, y, z);
        return true;
    }
}

[HarmonyPatch(typeof(ItemActionConnectPower), "OnHoldingUpdate")]
public static class HSPatchWireLight
{
    static void Prefix(ItemActionData _actionData)
    {
        var data = _actionData as ItemActionConnectPower.ConnectPowerData;
        if (data == null || !data.StartLink || !data.HasStartPoint)
            return;
        if (Time.time - data.lastUseTime < 0.2f)
            return;
        ItemStack held;
        EntityAlive who;
        HSFrozen.ReadInvData(_actionData.invData, out held, out who);
        var player = who as EntityPlayerLocal;
        if (player == null)
            return;
        WorldRayHitInfo hit = player.HitInfo;
        if (hit == null || !hit.bHitValid)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        Vector3i light = hit.hit.blockPos;
        if (!(world.GetBlock(light).Block is BlockLight))
            return;
        if (!HSPlugs.IsLightSwitch(world.GetBlock(data.startPoint).Block))
            return;
        data.StartLink = false;
        int reach = light.x - data.startPoint.x;
        int ry = light.y - data.startPoint.y;
        int rz = light.z - data.startPoint.z;
        if (reach * reach + ry * ry + rz * rz > 30 * 30)
        {
            GameManager.ShowTooltip(player, HSLoc.Get("hsLightTooFar", "That light is too far from the switch"));
            return;
        }
        GameManager.ShowTooltip(player, HSLoc.Get("hsLightWired", "Wired to the light switch. It comes on when the switch is on and has power"));
        HSWorkingStuffNet.SendLight(data.startPoint, light, player);
    }
}
