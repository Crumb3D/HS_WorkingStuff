using System.Collections.Generic;
using UnityEngine;

// Wall thermostat: wired from a socket (it doesn't take a plug), and it runs the heating loop of the nearest
// radiator. Turning it up or down sets every radiator valve on the loop at once; a radiator's own valve
// still overrides it until the thermostat is turned again. Block meta = level + 1 (0 = the radiator default).
public class BlockHSWallThermostat : BlockPowered
{
    BlockActivationCommand[] statCmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("hsstatup", "hot", true),
        new BlockActivationCommand("hsstatdown", "cold", true),
        new BlockActivationCommand("heatingon", "electric_switch", false),
        new BlockActivationCommand("heatingoff", "electric_switch", false),
        new BlockActivationCommand("take", "hand", false)
    };

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        World world = _world as World;
        HSThermostat.Loop loop = world != null ? HSThermostat.LoopFor(world, _blockPos) : null;
        bool heating = loop != null && loop.Boiler.HasValue && HSBoiler.Known(loop.Boiler.Value).Heating;
        bool boiler = loop != null && loop.Boiler.HasValue;
        statCmds[2].enabled = boiler && !heating;
        statCmds[3].enabled = boiler && heating;
        bool land = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        statCmds[4].enabled = land && TakeDelay > 0f;
        return statCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_commandName == "take")
        {
            takeItemWithTimer(_blockPos, _blockValue, _player, TakeDelay);
            return true;
        }
        if (_commandName != "hsstatup" && _commandName != "hsstatdown" && _commandName != "heatingon" && _commandName != "heatingoff")
            return false;
        World world = _world as World;
        if (world == null)
            return false;
        string problem = HSThermostat.Problem(world, _blockPos);
        if (problem != null)
        {
            GameManager.ShowTooltip(_player, problem);
            return true;
        }
        HSThermostat.Loop loop = HSThermostat.LoopFor(world, _blockPos);
        if (_commandName == "heatingon" || _commandName == "heatingoff")
        {
            if (!loop.Boiler.HasValue)
                return true;
            bool on = _commandName == "heatingon";
            HSBoiler.Info info = HSBoiler.Known(loop.Boiler.Value);
            info.Heating = on;
            HSBoiler.Remember(loop.Boiler.Value, info);
            HSWorkingStuffNet.SendWater(HSWorkingStuffNet.BoilerHeating, loop.Boiler.Value, _player, on ? 1 : 0);
            Audio.Manager.BroadcastPlay(_blockPos.ToVector3(), on ? "switch_up" : "switch_down");
            return true;
        }
        BlockValue bv = world.GetBlock(_blockPos);
        int level = HSThermostat.Level(bv) + (_commandName == "hsstatup" ? 1 : -1);
        if (level > HSRadiatorValve.Max || level < 0)
        {
            GameManager.ShowTooltip(_player, level < 0
                ? HSLoc.Get("hsStatMin", "The thermostat is already at its lowest")
                : HSLoc.Get("hsStatMax", "The thermostat is already at its highest"));
            return true;
        }
        bv.meta = (byte)(level + 1);
        world.SetBlockRPC(_blockPos, bv);
        int set = 0;
        foreach (Vector3i r in loop.Radiators)
        {
            BlockValue rv = world.GetBlock(r);
            if (!(rv.Block is BlockHSRadiator))
                continue;
            if (HSRadiatorValve.Level(rv) != level)
                world.SetBlockRPC(r, HSRadiatorValve.WithLevel(rv, level));
            set++;
        }
        Audio.Manager.BroadcastPlay(_blockPos.ToVector3(), "switch_up");
        GameManager.ShowTooltip(_player, HSLoc.Format("hsStatSet", "All {0} radiators set to {1} of {2}", set, level, HSRadiatorValve.Max));
        return true;
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSThermostat.Status(_world as World, _blockPos, _blockValue);
    }
}

public static class HSThermostat
{
    public class Loop
    {
        public Vector3i? Boiler;
        public List<Vector3i> Radiators = new List<Vector3i>();
    }

    static Vector3i cachedAt;
    static Loop cached;
    static float cachedUntil;

    public static int Level(BlockValue bv)
    {
        return bv.meta == 0 ? HSRadiatorValve.Default : Mathf.Clamp(bv.meta - 1, 0, HSRadiatorValve.Max);
    }

    public static bool Powered(WorldBase world, Vector3i pos)
    {
        var te = world != null ? world.GetTileEntity(pos) as TileEntityPowered : null;
        Vector3i parent;
        return te != null && te.IsPowered && HSPlugs.Parent(te, out parent) && HSPlugs.IsSocket(world.GetBlock(parent).Block);
    }

    // Why the thermostat can't do anything, or null when it can.
    public static string Problem(World world, Vector3i pos)
    {
        if (!Powered(world, pos))
            return HSLoc.Get("hsStatNoPower", "No power: wire it from a live socket");
        Loop loop = LoopFor(world, pos);
        if (loop == null || loop.Radiators.Count == 0)
            return HSLoc.Format("hsStatNoRadiator", "No radiator within {0} blocks: put it in a room with a radiator", HSWorkingStuffSettings.ThermostatRange);
        return null;
    }

    public static string Status(World world, Vector3i pos, BlockValue bv)
    {
        if (world == null)
            return "";
        Vector3 at = pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
        string text = HSLoc.Format("hsThermoRoom", "Room: {0}", HSRoomTemp.Format(HSRoomTemp.Room(world, at)));
        string problem = Problem(world, pos);
        if (problem != null)
            return text + "\n" + problem;
        Loop loop = LoopFor(world, pos);
        int level = Level(bv);
        text += "\n" + HSLoc.Format("hsStatLevel", "Set to {0} of {1} on {2} radiators (a radiator's own valve overrides it)",
            level, HSRadiatorValve.Max, loop.Radiators.Count);
        if (!loop.Boiler.HasValue)
            return text + "\n" + HSLoc.Get("hsRadiatorNoPipe", "Cold: not piped to a combi boiler");
        HSBoiler.Info info = HSBoiler.Known(loop.Boiler.Value);
        return text + "\n" + (info.Heating
            ? HSLoc.Get("hsStatHeatingOn", "Central heating: on")
            : HSLoc.Get("hsStatHeatingOff", "Central heating: off. Hold E to turn it on"));
    }

    // The prompt and the radial ask every frame, so the scan is cached briefly.
    public static Loop LoopFor(World world, Vector3i pos)
    {
        if (cached != null && pos == cachedAt && Time.time < cachedUntil)
            return cached;
        cachedAt = pos;
        cachedUntil = Time.time + 1f;
        cached = Find(world, pos);
        return cached;
    }

    static Loop Find(World world, Vector3i pos)
    {
        Vector3i? near = NearestRadiator(world, pos);
        if (!near.HasValue)
            return null;
        return Walk(world, near.Value);
    }

    static Vector3i? NearestRadiator(World world, Vector3i pos)
    {
        int r = Mathf.Clamp(HSWorkingStuffSettings.ThermostatRange, 1, 16);
        Vector3i? best = null;
        int bestD = int.MaxValue;
        for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
                for (int z = -r; z <= r; z++)
                {
                    int d = x * x + y * y + z * z;
                    if (d >= bestD || d > r * r)
                        continue;
                    Vector3i p = new Vector3i(pos.x + x, pos.y + y, pos.z + z);
                    BlockValue bv = world.GetBlock(p);
                    if (bv.isair || bv.Block == null || !HSBoiler.IsRadiator(bv.Block.GetBlockName()))
                        continue;
                    best = HSWater.Parent(p, bv);
                    bestD = d;
                }
        return best;
    }

    // Every radiator joined to this one by bare or laid pipe, and the boiler feeding them.
    static Loop Walk(World world, Vector3i radiator)
    {
        var loop = new Loop();
        loop.Radiators.Add(radiator);
        int limit = HSWorkingStuffSettings.PipeSearchLimit * 2;
        var q = new Queue<Vector3i>();
        var seen = new HashSet<long>();
        q.Enqueue(radiator);
        seen.Add(HSWater.Key(radiator));
        int walked = 0;
        while (q.Count > 0 && walked < limit)
        {
            Vector3i p = q.Dequeue();
            walked++;
            for (int i = 0; i < HSWater.Dirs.Length; i++)
            {
                Vector3i np = p + HSWater.Dirs[i];
                long nk = HSWater.Key(np);
                if (seen.Contains(nk) || !HSPipeNet.Joins(p, np))
                    continue;
                BlockValue nbv = world.GetBlock(np);
                Block nb = nbv.Block;
                if (nb == null)
                    continue;
                string nn = nb.GetBlockName();
                seen.Add(nk);
                if (HSBoiler.IsBoiler(nn))
                {
                    if (!loop.Boiler.HasValue)
                        loop.Boiler = HSWater.Parent(np, nbv);
                    continue;
                }
                if (HSBoiler.IsRadiator(nn))
                {
                    Vector3i rp = HSWater.Parent(np, nbv);
                    seen.Add(HSWater.Key(rp));
                    if (!loop.Radiators.Contains(rp))
                        loop.Radiators.Add(rp);
                }
                else if (!HSWater.IsBarePipe(nn) && !HSPipeNet.Has(np))
                    continue;
                q.Enqueue(np);
            }
        }
        return loop;
    }
}
