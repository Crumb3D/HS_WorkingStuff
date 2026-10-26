using System;
using System.Reflection;
using HarmonyLib;

public class HSWorkingStuffMod : IModApi
{
    public static string ModPath;

    public void InitMod(Mod _modInstance)
    {
        if (!HSGameVersion.AllowLoad("[HSWorkingStuff]"))
            return;
        ModPath = _modInstance.Path;
        HSWorkingStuffSettings.Load(ModPath);
        HSWorkingStuffNet.RegisterPackage();
        ModEvents.GameUpdate.RegisterHandler(OnGameUpdate);
        ModEvents.GameStartDone.RegisterHandler(OnStart);
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(OnSpawn);
        ModEvents.WorldShuttingDown.RegisterHandler(OnShutdown);
        ApplyHarmony();
        Log.Out("[HSWorkingStuff] v1.0.83 thermometer room/outside temps fixed; water pump (own engines and fuel) at the bank; pipe tool aims at lakes and streams; toilets and urinals, bladder and bowels, washing lines and clothes airers, consumer units and breaker panels, chained sockets, TVs, hi-fi with speakers, fridge, fridge-freezer, chest freezer with frozen food, cooler, stove, microwave, coffee maker, kettle, toaster, wheat and bread, tea, grills, plumbing, water cooler, shower, bath, combi boiler, radiators, air con, washer, dryer, thermometer, sockets, room temperature, smell, laundry, pipe tool, and 3-day food. Install on the host and on every client.");
    }

    static void ApplyHarmony()
    {
        Harmony harmony = new Harmony("HSWorkingStuff");
        Type[] types = AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly());
        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            if (type == null || type.Name == "TileEntityHSFridge")
                continue;
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception e)
            {
                Log.Error("[HSWorkingStuff] Harmony patch failed on " + type.Name + ". " + e);
            }
        }
    }

    static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        try
        {
            HSSpoil.OnGameUpdate();
        }
        catch (Exception e)
        {
            Log.Error("[HSWorkingStuff] Spoil tick failed. " + e);
        }
        try
        {
            HSBoiler.ClientTick();
            HSPipeNet.ClientTick();
            HSAirCon.ClientTick();
            HSAirCon.SoundTick();
            HSLaundry.ClientTick();
            HSUnlock.ClientTick();
            HSLaundry.ServerTick();
            HSHouseNoise.ServerTick();
            HSTV.ServerTick();
            HSKettle.ServerTick();
            HSCircuits.ServerTick();
            HSDrying.ServerTick();
            HSNeeds.ClientTick();
            HSFlipper.Tick();
            HSLights.Tick();
            HSShare.Tick();
        }
        catch (Exception e)
        {
            Log.Error("[HSWorkingStuff] Radiator/pipe tick failed. " + e);
        }
        HSJoinHold.Tick();
    }

    static void OnStart(ref ModEvents.SGameStartDoneData data)
    {
        HSWater.Load();
        HSShare.Reset();
        if (HSWorkingStuffNet.IsAuthority)
        {
            HSBoiler.Load();
            HSPipeNet.Load();
            HSCircuits.Load();
            HSLights.Load();
            HSShare.Load();
        }
        HSWorkingStuffNet.RegisterPackage();
        HSLaundryCraft.Register();
        HSFlipper.InjectFurniture();
        HSJoinHold.Tick();
    }

    static void OnSpawn(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!HSWorkingStuffNet.IsAuthority || data.IsLocalPlayer)
            return;
        HSShare.SendAll(data.EntityId);
    }

    static void OnShutdown(ref ModEvents.SWorldShuttingDownData data)
    {
        HSWater.Save();
        HSBoiler.Save();
        HSBoiler.Clear();
        HSPipeNet.Save();
        HSPipeNet.Clear();
        HSCircuits.Save();
        HSCircuits.Clear();
        HSLights.Save();
        HSShare.Save();
        HSJoinHold.Clear();
        HSAirCon.Clear();
        HSTV.Clear();
        HSKettle.Clear();
        HSLaundry.Clear();
        HSUnlock.Clear();
        HSLaundryCraft.Clear();
        HSDrying.Clear();
        HSNeeds.Clear();
        HSRoomTemp.Clear();
        HSFlipper.Clear();
    }
}
