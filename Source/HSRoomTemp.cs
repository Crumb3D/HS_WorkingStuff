using HarmonyLib;
using UnityEngine;

// Indoor temperature. A house follows the weather slowly (hours, not seconds) and only takes the edge off it,
// so a cold snap or a wet week chills the rooms. A warm radiator heats a room up to the thermostat, air con cools it down.
public static class HSRoomTemp
{
    const float Comfort = 70f;
    static float house = float.NaN;
    static ulong lastTime;

    public static void Clear()
    {
        house = float.NaN;
        lastTime = 0UL;
    }

    // Outdoor air before shade, wind or shelter: the weather plus the height drop, as vanilla works it out.
    public static float Weather(Vector3 at)
    {
        return WeatherManager.GetTemperature() + Utils.FastLerp(0f, -20f, (at.y - 130f) / 100f);
    }

    static float Insulate(float t)
    {
        float k = HSWorkingStuffSettings.IndoorInsulation;
        return t < Comfort ? Mathf.Min(Comfort, t + k) : Mathf.Max(Comfort, t - k);
    }

    // The unheated house, lagging the weather by IndoorLagHours.
    public static float House(Vector3 at)
    {
        float target = Insulate(Weather(at));
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || WeatherManager.currentWeather == null)
            return float.IsNaN(house) ? target : house;
        ulong now = world.worldTime;
        if (float.IsNaN(house) || now < lastTime || now - lastTime > 24000UL)
            house = target;
        else if (now > lastTime)
        {
            float hours = (now - lastTime) / 1000f;
            float lag = Mathf.Max(0.1f, HSWorkingStuffSettings.IndoorLagHours);
            house += (target - house) * (1f - Mathf.Exp(-hours / lag));
        }
        lastTime = now;
        return house;
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
        float t = House(at);
        float heatTo = HSWorkingStuffSettings.ThermostatHeat;
        float coolTo = HSWorkingStuffSettings.ThermostatCool;
        if (t < heatTo && Heating(world, at))
            t = Mathf.Min(heatTo, t + HSWorkingStuffSettings.RadiatorHeat);
        if (t > coolTo && Cooling(world, at))
            t = Mathf.Max(coolTo, t - HSWorkingStuffSettings.AirConCool);
        return t;
    }

    // Follows the player's Fahrenheit / Celsius option.
    public static string Format(float fahrenheit)
    {
        return ValueDisplayFormatters.Temperature(fahrenheit);
    }
}

// Vanilla pulls a sheltered player's temperature 30F towards 70F at once. This swaps that for the room temperature above.
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
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (HSWorkingStuffSettings.EnableRoomTemperature && world != null)
            {
                float s = lp.shelterPercent;
                Vector3 at = lp.GetPosition();
                float room = HSRoomTemp.Room(world, at + Vector3.up);
                if (s > 0f)
                {
                    float outdoor = HSRoomTemp.Weather(at);
                    float sun = SkyManager.GetSunPercent();
                    if (sun > 0.25f)
                        outdoor += __instance.shadePer * sun * -8f;
                    if (s < 0.5f)
                        outdoor += WeatherManager.GetWindSpeed() * 0.01f * -24f;
                    __result = Utils.FastLerpUnclamped(outdoor, room, s);
                }
            }
            __result -= HSLaundry.WetCold(lp);
        }
        catch { }
    }
}

// Wall thermometer: room temperature where it hangs, the weather outside, and what the heating is doing.
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
        string text = name + "\n" + HSLoc.Format("hsThermoRoom", "Room: {0}", HSRoomTemp.Format(room))
            + "   " + HSLoc.Format("hsThermoOutside", "Outside: {0}", HSRoomTemp.Format(HSRoomTemp.Weather(at)));
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
