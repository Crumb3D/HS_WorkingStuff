using System.Collections.Generic;
using UnityEngine;

// Running house kit is a dinner bell. Fridge stays quiet. Washer, dryer, cooker, TV, hi-fi,
// microwave, kettle, air con and a firing boiler pulse AIDirector noise so Better Ears
// (and vanilla heatmap) can hear the house.
public static class HSHouseNoise
{
    public const string TV = "hsTVPlay";
    public const string Hifi = "hsHifiPlay";

    static float next;
    static readonly List<Ping> pings = new List<Ping>();

    class Ping
    {
        public Vector3i Pos;
        public float Metres;
        public float Until;
    }

    public static void ServerTick()
    {
        if (!HSWorkingStuffSettings.EnableHouseNoise || !HSWorkingStuffNet.IsAuthority)
            return;
        if (Time.time < next)
            return;
        next = Time.time + 2f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.aiDirector == null)
            return;
        HSRadiatorHeat.ServerTick(world);
        HSLaundry.VisitRuns(delegate (Vector3i p) { Ring(world, p, HSLaundry.WasherHum); });
        HSMicrowaveSound.VisitHums(delegate (Vector3i p, string clip) { Ring(world, p, clip); });
        HSKettle.VisitHumming(delegate (Vector3i p) { Ring(world, p, "hsKettleBoil"); });
        HSAirCon.VisitHumming(delegate (Vector3i p) { Ring(world, p, "hsAirConHum"); });
        HSBoiler.VisitFiring(delegate (Vector3i p) { Ring(world, p, "hsBoilerFire"); });
        HSTV.VisitOn(world, delegate (Vector3i p) { Ring(world, p, TV); });
        HSHifi.VisitSpeakers(world, delegate (Vector3i p) { Ring(world, p, Hifi); });
        WarnPlayers(world);
    }

    static void Ring(World world, Vector3i pos, string clip)
    {
        if (string.IsNullOrEmpty(clip) || world.GetChunkFromWorldPos(pos) == null)
            return;
        Vector3 at = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        world.aiDirector.NotifyNoise(null, at, clip, 1f);
        pings.Add(new Ping { Pos = pos, Metres = Metres(clip), Until = Time.time + 5f });
    }

    // The number is how far the loudest running machine can be heard, after the walls. It shows once that is far enough to start drawing zombies.
    static void WarnPlayers(World world)
    {
        for (int i = pings.Count - 1; i >= 0; i--)
            if (Time.time > pings[i].Until)
                pings.RemoveAt(i);
        var list = world.Players != null ? world.Players.list : null;
        if (list == null)
            return;
        for (int i = 0; i < list.Count; i++)
        {
            EntityPlayer p = list[i];
            if (p == null || p.Buffs == null)
                continue;
            float best = 0f;
            Vector3 at = p.position;
            for (int n = 0; n < pings.Count; n++)
            {
                Vector3 machine = pings[n].Pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
                if ((machine - at).sqrMagnitude > 16f * 16f)
                    continue;
                float heard = pings[n].Metres * HSIndoor.SmellScale(world, machine);
                if (heard > best)
                    best = heard;
            }
            p.Buffs.SetCustomVar(".hsNoiseM", best);
            bool on = best >= 8f;
            bool has = p.Buffs.HasBuff("buffHSLoud");
            if (on && !has)
                p.Buffs.AddBuff("buffHSLoud");
            else if (!on && has)
                p.Buffs.RemoveBuff("buffHSLoud");
        }
    }

    static float Metres(string clip)
    {
        if (clip == "hsWasherHum") return 32f;
        if (clip == Hifi) return 26f;
        if (clip == TV) return 24f;
        if (clip == "hsStoveCook") return 20f;
        if (clip == "hsMicrowaveHum") return 18f;
        if (clip == "hsAirConHum") return 16f;
        if (clip == "hsKettleBoil" || clip == "hsBoilerFire") return 14f;
        if (clip == "hsToasterHum" || clip == "hsCoffeeBrew") return 12f;
        return 12f;
    }
}
