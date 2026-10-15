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
        HSLaundry.VisitRuns(delegate (Vector3i p) { Ring(world, p, HSLaundry.WasherHum); });
        HSMicrowaveSound.VisitHums(delegate (Vector3i p, string clip) { Ring(world, p, clip); });
        HSKettle.VisitHumming(delegate (Vector3i p) { Ring(world, p, "hsKettleBoil"); });
        HSAirCon.VisitHumming(delegate (Vector3i p) { Ring(world, p, "hsAirConHum"); });
        HSBoiler.VisitFiring(delegate (Vector3i p) { Ring(world, p, "hsBoilerFire"); });
        HSTV.VisitOn(world, delegate (Vector3i p) { Ring(world, p, TV); });
        HSHifi.VisitPlaying(world, delegate (Vector3i p) { Ring(world, p, Hifi); });
    }

    static void Ring(World world, Vector3i pos, string clip)
    {
        if (string.IsNullOrEmpty(clip) || world.GetChunkFromWorldPos(pos) == null)
            return;
        Vector3 at = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        world.aiDirector.NotifyNoise(null, at, clip, 1f);
    }
}
