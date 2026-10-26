using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Indoor temperature follows the biome the room stands in, and the blocks hold it.
// Brick, concrete and cobble keep desert heat and snow cold for hours. Solid steel holds it too, and lets it go sooner than masonry. Timber and corrugated sheet let it go soonest.
// Radiators and air con warm or cool that stored heat. A heavy cold room wants more than one radiator.
public static class HSRoomTemp
{
    const float Comfort = 70f;
    const float MassSeconds = 12f;

    static readonly int[] RayX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] RayZ = { 0, 0, 1, -1, 1, -1, 1, -1 };
    static readonly Dictionary<long, Pocket> pockets = new Dictionary<long, Pocket>();

    sealed class Pocket
    {
        public float Temp;
        public ulong Time;
        public ulong Born;
        public float Mass;
        public float MassUntil;
    }

    public static void Clear()
    {
        pockets.Clear();
        HSRadiatorHeat.Clear();
    }

    // True outdoor air: same base the compass HUD uses before shelter (WeatherManager + height).
    public static float Weather(Vector3 at)
    {
        float air = WeatherManager.GetTemperature();
        return air + Utils.FastLerp(0f, -20f, (at.y - 130f) / 100f);
    }

    public static bool Heating(World world, Vector3 at)
    {
        return HSBoiler.WarmAt(world, at);
    }

    public static bool Cooling(World world, Vector3 at)
    {
        return HSAirCon.CoolAt(world, at);
    }

    public static float Room(World world, Vector3 at)
    {
        float outdoor = Weather(at);
        float mass = Mass(world, at);
        float shell = Shell(outdoor, mass);
        float target = shell;
        bool hvac = false;
        if (world != null)
        {
            HSRadiatorHeat.Advance(world);
            float heat = HSBoiler.HeatScale(world, at);
            float soak = HSRadiatorHeat.Near(world, at);
            if (heat > 0f && soak > 0.05f && target < HSWorkingStuffSettings.ThermostatHeat)
            {
                float lift = HSWorkingStuffSettings.RadiatorHeat * Mathf.Min(heat, 3f) * soak;
                target = Mathf.Min(HSWorkingStuffSettings.ThermostatHeat, target + lift);
                hvac = soak > 0.35f;
            }
            float cool = HSAirCon.CoolScale(world, at);
            if (cool > 0f && target > HSWorkingStuffSettings.ThermostatCool)
            {
                float drop = HSWorkingStuffSettings.AirConCool * Mathf.Min(cool, 3f);
                target = Mathf.Max(HSWorkingStuffSettings.ThermostatCool, target - drop);
                hvac = true;
            }
        }
        return Stored(world, at, target, shell, mass, hvac);
    }

    // Without heating / air con the room follows outdoor air. Mass only slows that change (Stored).
    // Do not snap toward 70F comfort — that made "Room" look like the sheltered HUD reading.
    static float Shell(float outdoor, float mass)
    {
        if (mass < 0.5f)
            return outdoor;
        // Heavy shell mutes outdoor extremes a little (insulation degrees), still centred on outdoor.
        float mute = Mathf.Clamp(HSWorkingStuffSettings.IndoorInsulation * 0.15f * Mathf.Min(mass, 2.5f), 0f, 8f);
        float delta = outdoor - Comfort;
        if (Mathf.Abs(delta) <= mute)
            return outdoor;
        return outdoor - Mathf.Sign(delta) * mute;
    }

    static float Stored(World world, Vector3 at, float target, float shell, float mass, bool hvac)
    {
        if (world == null)
            return target;
        ulong now = world.worldTime;
        long key = CellKey(at);
        Pocket p;
        if (!pockets.TryGetValue(key, out p))
        {
            p = new Pocket();
            p.Temp = shell;
            p.Time = now;
            p.Born = now;
            p.Mass = mass;
            p.MassUntil = Time.time + MassSeconds;
            pockets[key] = p;
            if (pockets.Count > 40)
                Prune(now);
            return p.Temp;
        }
        if (now < p.Time || now - p.Time > 24000UL)
            p.Temp = shell;
        else if (Mathf.Abs(p.Mass - mass) > 0.75f && now - p.Born < 2000UL)
            p.Temp = shell;
        else if (now > p.Time)
        {
            float hours = (now - p.Time) / 1000f;
            float lag = Mathf.Max(0.15f, HSWorkingStuffSettings.IndoorLagHours * Mathf.Max(0.35f, mass));
            if (hvac)
                lag *= 0.5f;
            p.Temp += (target - p.Temp) * (1f - Mathf.Exp(-hours / lag));
        }
        p.Time = now;
        p.Mass = mass;
        return p.Temp;
    }

    static void Prune(ulong now)
    {
        List<long> drop = null;
        foreach (var kv in pockets)
        {
            if (now > kv.Value.Time && now - kv.Value.Time > 48000UL)
            {
                if (drop == null)
                    drop = new List<long>();
                drop.Add(kv.Key);
            }
        }
        if (drop == null)
            return;
        for (int i = 0; i < drop.Count; i++)
            pockets.Remove(drop[i]);
    }

    static long CellKey(Vector3 at)
    {
        uint x = (uint)(Mathf.FloorToInt(at.x) >> 3);
        uint y = (uint)(Mathf.FloorToInt(at.y) >> 2);
        uint z = (uint)(Mathf.FloorToInt(at.z) >> 3);
        return ((long)x << 42) | ((long)(y & 0x1FFFFF) << 21) | (z & 0x1FFFFF);
    }

    static float Mass(World world, Vector3 at)
    {
        long key = CellKey(at);
        Pocket p;
        if (pockets.TryGetValue(key, out p) && Time.time < p.MassUntil && p.Mass > 0f)
            return p.Mass;
        float mass = world != null ? SampleMass(world, at) : 1f;
        if (pockets.TryGetValue(key, out p))
        {
            p.Mass = mass;
            p.MassUntil = Time.time + MassSeconds;
        }
        return mass;
    }

    // Floor, ceiling, and the first solid block along each wall. Furniture in the middle does not count.
    static float SampleMass(World world, Vector3 at)
    {
        int x0 = Mathf.FloorToInt(at.x);
        int y0 = Mathf.FloorToInt(at.y);
        int z0 = Mathf.FloorToInt(at.z);
        float sum = 0f;
        int n = 0;
        float hold;
        if (HoldAt(world, x0, y0 - 1, z0, out hold) || HoldAt(world, x0, y0 - 2, z0, out hold))
        {
            sum += hold;
            n++;
        }
        for (int up = 1; up <= 4; up++)
        {
            if (HoldAt(world, x0, y0 + up, z0, out hold))
            {
                sum += hold;
                n++;
                break;
            }
        }
        for (int d = 0; d < 8; d++)
        {
            for (int s = 1; s <= 6; s++)
            {
                int x = x0 + RayX[d] * s;
                int z = z0 + RayZ[d] * s;
                if (HoldAt(world, x, y0, z, out hold) || HoldAt(world, x, y0 + 1, z, out hold))
                {
                    sum += hold;
                    n++;
                    break;
                }
            }
        }
        if (n < 3)
            return 0.4f;
        return sum / n;
    }

    static bool HoldAt(World world, int x, int y, int z, out float hold)
    {
        hold = 1f;
        if (world == null || y < 0 || y > 255)
            return false;
        BlockValue bv = world.GetBlock(x, y, z);
        Block block = bv.Block;
        if (block == null || block.shape == null || block.blockMaterial == null)
            return false;
        string name = block.GetBlockName();
        string id = block.blockMaterial.id;
        if (string.IsNullOrEmpty(name) || string.Equals(name, "air", StringComparison.OrdinalIgnoreCase))
            return false;
        if (Has(id, "water") || Has(name, "water"))
            return false;
        bool glass = Has(id, "glass") || Has(name, "glass") || Has(name, "window");
        bool solid = block.shape.IsSolidCube || block.shape.IsTerrain() || (block.shape.getFacesDrawnFullBitfield(bv) & 63) == 63;
        if (!solid && !glass)
            return false;
        hold = Classify(id, name);
        return true;
    }

    static float Classify(string id, string name)
    {
        if (Has(id, "glass") || Has(name, "glass") || Has(name, "window"))
            return 0.45f;
        if (Has(id, "cloth") || Has(id, "fabric") || Has(name, "cloth") || Has(name, "hay") || Has(name, "thatch") || Has(name, "tent"))
            return 0.35f;
        if (Has(id, "concrete") || Has(id, "cement") || Has(id, "cobble") || Has(id, "brick") || Has(id, "flagstone")
            || Has(id, "stone") || Has(id, "asphalt") || Has(id, "marble") || Has(id, "granite") || Has(id, "terrazzo")
            || Has(name, "concrete") || Has(name, "cement") || Has(name, "cobble") || Has(name, "brick")
            || Has(name, "flagstone") || Has(name, "stone") || Has(name, "asphalt") || Has(name, "marble")
            || Has(name, "granite") || Has(name, "boulder"))
            return Mathf.Max(0.35f, HSWorkingStuffSettings.StoneHeatHold);
        if (Has(id, "metal_thin") || Has(id, "scrap") || Has(name, "corrugated") || Has(name, "scrap"))
            return 0.5f;
        if (Has(id, "steel") || Has(name, "steel"))
            return Mathf.Max(0.35f, HSWorkingStuffSettings.SteelHeatHold);
        if (Has(id, "metal") || Has(id, "iron") || Has(name, "metal") || Has(name, "iron"))
            return 1.2f;
        if (Has(id, "dirt") || Has(id, "sand") || Has(id, "gravel") || Has(id, "clay") || Has(id, "snow")
            || Has(name, "dirt") || Has(name, "sand") || Has(name, "gravel") || Has(name, "snow"))
            return 1.35f;
        if (Has(id, "wood") || Has(id, "plank") || Has(id, "log") || Has(name, "wood") || Has(name, "plank") || Has(name, "log") || Has(name, "timber"))
            return Mathf.Max(0.35f, HSWorkingStuffSettings.WoodHeatHold);
        return 1f;
    }

    static bool Has(string s, string part)
    {
        return !string.IsNullOrEmpty(s) && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string Format(float fahrenheit)
    {
        return ValueDisplayFormatters.Temperature(fahrenheit);
    }
}

// Compass HUD "_outsidetemp" must stay true outdoor weather (Weather + height/shade/wind).
// Vanilla pulls that 30F toward 70F when sheltered — strip that so the top number matches the thermometer Outside.
[HarmonyPatch(typeof(PlayerEntityStats), "GetOutsideTemperature")]
public static class HSPatchRoomTemp
{
    static void Postfix(PlayerEntityStats __instance, ref float __result)
    {
        try
        {
            EntityPlayerLocal lp = __instance.localPlayer;
            if (lp == null)
                return;
            Vector3 at = lp.GetPosition();
            float outdoor = HSRoomTemp.Weather(at);
            float sun = SkyManager.GetSunPercent();
            if (sun > 0.25f)
                outdoor += __instance.shadePer * sun * -8f;
            if (lp.shelterPercent < 0.5f)
                outdoor += WeatherManager.GetWindSpeed() * 0.01f * -24f;
            __result = outdoor - HSLaundry.WetCold(lp);
        }
        catch { }
    }
}

// Core body temp still follows the room when you are indoors, so the house protects you.
// HUD stays on outdoor weather from GetOutsideTemperature above.
[HarmonyPatch(typeof(PlayerEntityStats), "UpdateWeatherStats")]
public static class HSPatchRoomCoreTemp
{
    static float savedCore;

    static void Prefix(PlayerEntityStats __instance)
    {
        savedCore = __instance.CoreTemp;
    }

    static void Postfix(PlayerEntityStats __instance)
    {
        try
        {
            if (!HSWorkingStuffSettings.EnableRoomTemperature)
                return;
            EntityPlayerLocal lp = __instance.localPlayer;
            if (lp == null || lp.shelterPercent <= 0f)
                return;
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null || !World.TemperatureSurvival || !EntityStats.WeatherSurvivalEnabled)
                return;
            if (WeatherManager.inWeatherGracePeriod || lp.biomeStandingOn == null || lp.IsGodMode.Value)
                return;
            if (lp.Buffs != null && lp.Buffs.HasBuff("god"))
                return;
            Vector3 at = lp.GetPosition() + Vector3.up;
            float room = HSRoomTemp.Room(world, at);
            float outdoor = HSRoomTemp.Weather(lp.GetPosition());
            float target = Utils.FastLerpUnclamped(outdoor, room, lp.shelterPercent);
            if (target < 70f)
                target = Utils.FastMin(70f, target + EffectManager.GetValue(PassiveEffects.HypothermalResist, null, 0f, lp));
            else
                target = Utils.FastMax(70f, target - EffectManager.GetValue(PassiveEffects.HyperthermalResist, null, 0f, lp));
            __instance.CoreTemp = Utils.FastMoveTowards(savedCore, target, 1f);
            if (lp.Buffs != null)
                lp.Buffs.SetCustomVar("_coretemp", __instance.CoreTemp);
        }
        catch { }
    }
}

// Wall thermometer: room air where it hangs (from outdoor weather via Room), and true Outside weather.
public class BlockHSThermometer : Block
{
    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        World world = _world as World;
        Vector3 at = new Vector3(_blockPos.x + 0.5f, _blockPos.y + 0.5f, _blockPos.z + 0.5f);
        string name = _blockValue.Block.GetLocalizedBlockName();
        if (world == null)
            return name;
        float room = HSRoomTemp.Room(world, at);
        float outside = HSRoomTemp.Weather(at);
        string text = name + "\n" + HSLoc.Format("hsThermoRoom", "Room: {0}", HSRoomTemp.Format(room))
            + "   " + HSLoc.Format("hsThermoOutside", "Outside: {0}", HSRoomTemp.Format(outside));
        string state;
        if (HSRoomTemp.Heating(world, at))
            state = HSLoc.Get("hsThermoHeating", "Radiator heating the room");
        else if (HSRoomTemp.Cooling(world, at))
            state = HSLoc.Get("hsThermoCooling", "Air con cooling the room");
        else if (room < 50f)
            state = HSLoc.Get("hsThermoCold", "Cold: put the heating on");
        else if (room > 85f)
            state = HSLoc.Get("hsThermoHot", "Hot: an air conditioner would help");
        else
            state = HSLoc.Get("hsThermoComfy", "Comfortable");
        return text + "\n" + state;
    }
}
