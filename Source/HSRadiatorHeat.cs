using System.Collections.Generic;
using UnityEngine;

// A warm radiator heats the masonry it touches first. That warmth walks outward through
// solid blocks. The room air only climbs once the blocks around you are warm. Once the
// warmth reaches a block with open sky beside it, a little heat-map leak calls zombies.
// A campfire is strength 5. One radiator tops out around 1.2, and only on that outside face.
public static class HSRadiatorHeat
{
    const int MaxDist = 6;
    const int MaxBlocks = 700;
    const float LeakStrength = 1.2f;
    const float LeakSeconds = 50f;

    static readonly int[] OffX = { 1, -1, 0, 0, 0, 0 };
    static readonly int[] OffY = { 0, 0, 1, -1, 0, 0 };
    static readonly int[] OffZ = { 0, 0, 0, 0, 1, -1 };

    sealed class Brick
    {
        public float Heat;
        public int Seen;
        public int Dist;
    }

    static readonly Dictionary<long, Brick> masonry = new Dictionary<long, Brick>();
    static readonly List<long> drop = new List<long>();
    static readonly Queue<long> walk = new Queue<long>();
    static ulong spreadTime;
    static int generation;
    static float nextLeak;

    public static void Clear()
    {
        masonry.Clear();
        spreadTime = 0UL;
        generation = 0;
        nextLeak = 0f;
    }

    public static void ServerTick(World world)
    {
        Advance(world);
        if (world == null || world.aiDirector == null || !HSWorkingStuffNet.IsAuthority)
            return;
        if (Time.time < nextLeak)
            return;
        nextLeak = Time.time + LeakSeconds;
        Leak(world);
    }

    // How warm the blocks around this spot are, 0 to 1. The air target uses it.
    public static float Near(World world, Vector3 at)
    {
        if (world == null || masonry.Count == 0)
            return 0f;
        int x0 = Mathf.FloorToInt(at.x);
        int y0 = Mathf.FloorToInt(at.y);
        int z0 = Mathf.FloorToInt(at.z);
        float sum = 0f;
        int n = 0;
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -2; x <= 2; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    Brick brick;
                    if (!masonry.TryGetValue(HSWater.Key(new Vector3i(x0 + x, y0 + y, z0 + z)), out brick))
                        continue;
                    sum += brick.Heat;
                    n++;
                }
            }
        }
        return n == 0 ? 0f : sum / n;
    }

    public static void Advance(World world)
    {
        if (world == null)
            return;
        ulong now = world.worldTime;
        if (now == spreadTime)
            return;
        float hours = 0f;
        if (spreadTime != 0UL && now > spreadTime && now - spreadTime <= 24000UL)
            hours = (now - spreadTime) / 1000f;
        spreadTime = now;
        generation++;
        if (generation > 1000000)
            generation = 1;
        Spread(world, hours);
    }

    static void Spread(World world, float hours)
    {
        List<Vector3i> rads = WarmList();
        for (int i = 0; i < rads.Count; i++)
            Walk(world, rads[i]);
        drop.Clear();
        foreach (KeyValuePair<long, Brick> kv in masonry)
        {
            Brick brick = kv.Value;
            float target = brick.Seen == generation ? 1f : 0f;
            float lag = brick.Seen == generation ? 0.45f + brick.Dist * 0.85f : 4f;
            if (hours > 0f)
                brick.Heat += (target - brick.Heat) * (1f - Mathf.Exp(-hours / lag));
            if (brick.Seen != generation && brick.Heat < 0.02f)
                drop.Add(kv.Key);
        }
        for (int i = 0; i < drop.Count; i++)
            masonry.Remove(drop[i]);
        if (masonry.Count > MaxBlocks)
            masonry.Clear();
    }

    static List<Vector3i> WarmList()
    {
        var list = new List<Vector3i>();
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return list;
        // HeatScale walks the same set. Asking WarmAt on the radiator cell is enough to know it is on.
        // Visit via a probe: any warm radiator reports itself through HeatScale's range, so ask the boiler list.
        HSBoiler.VisitWarm(delegate (Vector3i pos) { list.Add(pos); });
        return list;
    }

    static void Walk(World world, Vector3i origin)
    {
        walk.Clear();
        Touch(world, origin, 0);
        for (int n = 0; n < 6; n++)
            Touch(world, new Vector3i(origin.x + OffX[n], origin.y + OffY[n], origin.z + OffZ[n]), 0);
        int guard = 0;
        while (walk.Count > 0 && guard < MaxBlocks)
        {
            guard++;
            long key = walk.Dequeue();
            Brick brick;
            if (!masonry.TryGetValue(key, out brick))
                continue;
            if (brick.Dist >= MaxDist)
                continue;
            Vector3i pos = HSWater.FromKey(key);
            for (int n = 0; n < 6; n++)
                Touch(world, new Vector3i(pos.x + OffX[n], pos.y + OffY[n], pos.z + OffZ[n]), brick.Dist + 1);
        }
    }

    static void Touch(World world, Vector3i pos, int dist)
    {
        if (dist > MaxDist || !Solid(world, pos))
            return;
        long key = HSWater.Key(pos);
        Brick brick;
        if (!masonry.TryGetValue(key, out brick))
        {
            if (masonry.Count >= MaxBlocks)
                return;
            brick = new Brick();
            masonry[key] = brick;
        }
        if (brick.Seen == generation && brick.Dist <= dist)
            return;
        brick.Seen = generation;
        brick.Dist = dist;
        walk.Enqueue(key);
    }

    static bool Solid(World world, Vector3i pos)
    {
        if (pos.y < 1 || pos.y > 255)
            return false;
        BlockValue bv = world.GetBlock(pos);
        Block block = bv.Block;
        if (block == null || block.shape == null || bv.isair)
            return false;
        if (block.shape.IsSolidCube || block.shape.IsTerrain())
            return true;
        return (block.shape.getFacesDrawnFullBitfield(bv) & 63) == 63;
    }

    static void Leak(World world)
    {
        List<Vector3i> rads = WarmList();
        for (int r = 0; r < rads.Count; r++)
        {
            Vector3i rad = rads[r];
            float best = 0.5f;
            Vector3i at = rad;
            bool found = false;
            foreach (KeyValuePair<long, Brick> kv in masonry)
            {
                if (kv.Value.Heat <= best)
                    continue;
                Vector3i pos = HSWater.FromKey(kv.Key);
                int dx = pos.x - rad.x;
                int dy = pos.y - rad.y;
                int dz = pos.z - rad.z;
                if (dx < 0) dx = -dx;
                if (dy < 0) dy = -dy;
                if (dz < 0) dz = -dz;
                if (dx + dy + dz > MaxDist + 1)
                    continue;
                if (!OutsideFace(world, pos))
                    continue;
                best = kv.Value.Heat;
                at = pos;
                found = true;
            }
            if (!found)
                continue;
            try
            {
                world.aiDirector.NotifyActivity(EnumAIDirectorChunkEvent.Campfire, at, LeakStrength * best, 180f);
            }
            catch { }
        }
    }

    static bool OutsideFace(World world, Vector3i pos)
    {
        for (int n = 0; n < 6; n++)
        {
            int x = pos.x + OffX[n];
            int y = pos.y + OffY[n];
            int z = pos.z + OffZ[n];
            if (y < 1 || y > 255)
                continue;
            BlockValue bv = world.GetBlock(x, y, z);
            if (!bv.isair)
                continue;
            try
            {
                if (world.IsOpenSkyAbove(x, y, z))
                    return true;
            }
            catch { }
        }
        return false;
    }
}
