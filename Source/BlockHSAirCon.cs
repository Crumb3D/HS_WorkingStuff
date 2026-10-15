using System.Collections.Generic;
using UnityEngine;
using Audio;

// Powered air conditioner: standing near a running unit keeps the heat off, the opposite of a radiator.
public class BlockHSAirCon : BlockPowered
{
    public override void OnBlockAdded(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(world, _chunk, _blockPos, _blockValue, _addedByPlayer);
        if (!_blockValue.ischild)
            HSAirCon.Track(_blockPos);
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
        if (!_blockValue.ischild)
            HSAirCon.Track(_blockPos);
    }

    public override void OnBlockRemoved(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        HSAirCon.Untrack(_blockPos);
        base.OnBlockRemoved(world, _chunk, _blockPos, _blockValue);
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string status = HSAirCon.IsRunning(_world, _blockPos)
            ? HSLoc.Format("hsAirConOn", "Powered: cooling within {0} blocks", Mathf.RoundToInt(HSWorkingStuffSettings.AirConRadius))
            : HSLoc.Get("hsAirConOff", "No power: wire it up to cool the room");
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + status;
    }
}

public static class HSAirCon
{
    public const string CoolBuff = "buffHSAirConCool";

    static readonly HashSet<long> units = new HashSet<long>();
    static readonly HashSet<long> humming = new HashSet<long>();
    static float nextCheck;
    static float nextSound;

    public static void VisitHumming(System.Action<Vector3i> visit)
    {
        if (visit == null || humming.Count == 0)
            return;
        foreach (long k in humming)
            visit(HSWater.FromKey(k));
    }

    public static void Track(Vector3i pos)
    {
        units.Add(HSWater.Key(pos));
    }

    public static void Untrack(Vector3i pos)
    {
        units.Remove(HSWater.Key(pos));
    }

    public static bool IsRunning(WorldBase world, Vector3i pos)
    {
        var te = world != null ? world.GetTileEntity(pos) as TileEntityPowered : null;
        return te != null && te.IsPowered;
    }

    public static void SoundTick()
    {
        if (!HSWorkingStuffNet.IsAuthority || units.Count == 0)
            return;
        float now = Time.time;
        if (now < nextSound)
            return;
        nextSound = now + 1f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        foreach (long k in new List<long>(units))
        {
            Vector3i p = HSWater.FromKey(k);
            bool run = world.GetChunkFromWorldPos(p) != null && IsRunning(world, p);
            bool was = humming.Contains(k);
            if (run == was)
                continue;
            Vector3 at = new Vector3(p.x + 0.5f, p.y + 0.5f, p.z + 0.5f);
            if (run)
            {
                humming.Add(k);
                Audio.Manager.BroadcastPlay(at, "hsAirConHum");
            }
            else
            {
                humming.Remove(k);
                Audio.Manager.BroadcastStop(at, "hsAirConHum");
            }
        }
    }

    // Any peer with a local player, like the radiator warmth.
    public static void ClientTick()
    {
        if (units.Count == 0)
            return;
        float now = Time.time;
        if (now < nextCheck)
            return;
        nextCheck = now + 0.5f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (player == null || player.Buffs == null || player.IsDead())
            return;
        float r = HSWorkingStuffSettings.AirConRadius;
        Vector3 at = player.GetPosition();
        List<long> stale = null;
        bool near = false;
        foreach (long k in units)
        {
            Vector3i p = HSWater.FromKey(k);
            if (world.GetChunkFromWorldPos(p) == null)
                continue;
            if (!(world.GetBlock(p).Block is BlockHSAirCon))
            {
                if (stale == null)
                    stale = new List<long>();
                stale.Add(k);
                continue;
            }
            if (near)
                continue;
            Vector3 c = new Vector3(p.x + 0.5f, p.y + 0.5f, p.z + 0.5f);
            if ((c - at).sqrMagnitude <= r * r && IsRunning(world, p))
                near = true;
        }
        if (stale != null)
        {
            for (int i = 0; i < stale.Count; i++)
                units.Remove(stale[i]);
        }
        if (!near)
            return;
        try
        {
            player.Buffs.AddBuff(CoolBuff);
        }
        catch { }
    }

    public static bool CoolAt(World world, Vector3 at)
    {
        if (world == null || units.Count == 0)
            return false;
        float r = HSWorkingStuffSettings.AirConRadius;
        foreach (long k in units)
        {
            Vector3i p = HSWater.FromKey(k);
            Vector3 c = new Vector3(p.x + 0.5f, p.y + 0.5f, p.z + 0.5f);
            if ((c - at).sqrMagnitude <= r * r && world.GetChunkFromWorldPos(p) != null && IsRunning(world, p))
                return true;
        }
        return false;
    }

    public static void Clear()
    {
        units.Clear();
        humming.Clear();
    }
}
