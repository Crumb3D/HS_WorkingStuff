using System.Globalization;
using System.IO;

public static class HSWorkingStuffSettings
{
    public static bool EnableSpoil = true;
    public static bool EnableFridge = true;
    public static bool EnableStove = true;
    public static bool EnablePlumbing = true;
    public static bool EnableConnectToIp = true;
    public static bool HideNews = true;
    public static bool HoldJoinUntilReady = true;

    public static float SpoilDays = 7f;
    public static float FridgeMultiplier = 0.25f;
    public static float CoolerMultiplier = 0.5f;
    public static float WarningDays = 1f;
    public static int TankCapacity = 40;
    public static int RainCatchCapacity = 10;
    public static int BucketUnits = 4;
    public static float RainPerHour = 0.25f;
    public static float DewPerHour = 0.125f;
    public static int PipeSearchLimit = 48;
    public static float DryDays = 1f;
    public static float CompostDays = 1f;
    public static float HotWaterSeconds = 30f;
    public static int RadiatorWatts = 5;
    public static float RadiatorRadius = 5f;
    public static float FreezerMultiplier = 0f;
    public static float AirConRadius = 6f;
    public static int ShowerJars = 2;
    public static int BathJars = 4;
    public static float GasCanGrillSeconds = 60f;
    public static bool EnableRoomTemperature = true;
    public static float IndoorInsulation = 15f;
    public static float IndoorLagHours = 3f;
    public static float RadiatorHeat = 40f;
    public static float ThermostatHeat = 68f;
    public static float AirConCool = 30f;
    public static float ThermostatCool = 72f;
    public static bool EnableBodySmell = true;
    public static float BodySmellStartHours = 24f;
    public static float BodySmellFullHours = 72f;
    public static float BodySmellRadius = 50f;
    public static bool EnableLaundry = true;
    public static float ClothesDirtyHours = 72f;
    public static float DirtyClothesSmell = 6f;
    public static int WasherJars = 4;
    public static float AirDryHours = 6f;
    public static float WaterWashHours = 0.5f;
    public static float WetClothesCold = 6f;
    public static bool EnableFreezing = true;
    public static float FreezeHours = 2f;
    public static float ThawHours = 2f;
    public static float FridgeThawHours = 8f;
    public static float TVVolume = 0.8f;
    public static float TVHearRange = 15f;
    public static float TVViewRange = 40f;

    public static void Load(string modPath)
    {
        EnableSpoil = true;
        EnableFridge = true;
        EnableStove = true;
        EnablePlumbing = true;
        EnableConnectToIp = true;
        HideNews = true;
        HoldJoinUntilReady = true;
        SpoilDays = 7f;
        FridgeMultiplier = 0.25f;
        CoolerMultiplier = 0.5f;
        WarningDays = 1f;
        TankCapacity = 40;
        RainCatchCapacity = 10;
        BucketUnits = 4;
        RainPerHour = 0.25f;
        DewPerHour = 0.125f;
        PipeSearchLimit = 48;
        DryDays = 1f;
        CompostDays = 1f;
        HotWaterSeconds = 30f;
        RadiatorWatts = 5;
        RadiatorRadius = 5f;
        FreezerMultiplier = 0f;
        AirConRadius = 6f;
        ShowerJars = 2;
        BathJars = 4;
        GasCanGrillSeconds = 60f;
        EnableRoomTemperature = true;
        IndoorInsulation = 15f;
        IndoorLagHours = 3f;
        RadiatorHeat = 40f;
        ThermostatHeat = 68f;
        AirConCool = 30f;
        ThermostatCool = 72f;
        EnableBodySmell = true;
        BodySmellStartHours = 24f;
        BodySmellFullHours = 72f;
        BodySmellRadius = 50f;
        EnableLaundry = true;
        ClothesDirtyHours = 72f;
        DirtyClothesSmell = 6f;
        WasherJars = 4;
        AirDryHours = 6f;
        WaterWashHours = 0.5f;
        WetClothesCold = 6f;
        EnableFreezing = true;
        FreezeHours = 2f;
        ThawHours = 2f;
        FridgeThawHours = 8f;
        TVVolume = 0.8f;
        TVHearRange = 15f;
        TVViewRange = 40f;
        if (!string.IsNullOrEmpty(modPath))
            TryRead(Path.Combine(modPath, "HSWorkingStuffSettings.json"));
        try
        {
            string user = GameIO.GetUserGameDataDir();
            if (!string.IsNullOrEmpty(user))
                TryRead(Path.Combine(user, "HSWorkingStuff", "HSWorkingStuffSettings.json"));
        }
        catch { }
        Log.Out("[HSWorkingStuff] Spoil=" + EnableSpoil
            + " Fridge=" + EnableFridge
            + " Stove=" + EnableStove
            + " Plumbing=" + EnablePlumbing
            + " ConnectToIp=" + EnableConnectToIp
            + " HideNews=" + HideNews
            + " HoldJoinUntilReady=" + HoldJoinUntilReady
            + " SpoilDays=" + SpoilDays);
    }

    static void TryRead(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;
        string text = File.ReadAllText(path);
        ReadBool(text, "EnableSpoil", ref EnableSpoil);
        ReadBool(text, "EnableFridge", ref EnableFridge);
        ReadBool(text, "EnableStove", ref EnableStove);
        ReadBool(text, "EnablePlumbing", ref EnablePlumbing);
        ReadBool(text, "EnableConnectToIp", ref EnableConnectToIp);
        ReadBool(text, "HideNews", ref HideNews);
        ReadBool(text, "HoldJoinUntilReady", ref HoldJoinUntilReady);
        ReadFloat(text, "SpoilDays", ref SpoilDays);
        ReadFloat(text, "FridgeMultiplier", ref FridgeMultiplier);
        ReadFloat(text, "CoolerMultiplier", ref CoolerMultiplier);
        ReadFloat(text, "WarningDays", ref WarningDays);
        ReadInt(text, "TankCapacity", ref TankCapacity);
        ReadInt(text, "RainCatchCapacity", ref RainCatchCapacity);
        ReadInt(text, "BucketUnits", ref BucketUnits);
        ReadFloat(text, "RainPerHour", ref RainPerHour);
        ReadFloat(text, "DewPerHour", ref DewPerHour);
        ReadInt(text, "PipeSearchLimit", ref PipeSearchLimit);
        ReadFloat(text, "DryDays", ref DryDays);
        ReadFloat(text, "CompostDays", ref CompostDays);
        ReadFloat(text, "HotWaterSeconds", ref HotWaterSeconds);
        ReadInt(text, "RadiatorWatts", ref RadiatorWatts);
        ReadFloat(text, "RadiatorRadius", ref RadiatorRadius);
        ReadFloat(text, "FreezerMultiplier", ref FreezerMultiplier);
        ReadFloat(text, "AirConRadius", ref AirConRadius);
        ReadInt(text, "ShowerJars", ref ShowerJars);
        ReadInt(text, "BathJars", ref BathJars);
        ReadFloat(text, "GasCanGrillSeconds", ref GasCanGrillSeconds);
        ReadBool(text, "EnableRoomTemperature", ref EnableRoomTemperature);
        ReadFloat(text, "IndoorInsulation", ref IndoorInsulation);
        ReadFloat(text, "IndoorLagHours", ref IndoorLagHours);
        ReadFloat(text, "RadiatorHeat", ref RadiatorHeat);
        ReadFloat(text, "ThermostatHeat", ref ThermostatHeat);
        ReadFloat(text, "AirConCool", ref AirConCool);
        ReadFloat(text, "ThermostatCool", ref ThermostatCool);
        ReadBool(text, "EnableBodySmell", ref EnableBodySmell);
        ReadFloat(text, "BodySmellStartHours", ref BodySmellStartHours);
        ReadFloat(text, "BodySmellFullHours", ref BodySmellFullHours);
        ReadFloat(text, "BodySmellRadius", ref BodySmellRadius);
        ReadBool(text, "EnableLaundry", ref EnableLaundry);
        ReadFloat(text, "ClothesDirtyHours", ref ClothesDirtyHours);
        ReadFloat(text, "DirtyClothesSmell", ref DirtyClothesSmell);
        ReadInt(text, "WasherJars", ref WasherJars);
        ReadFloat(text, "AirDryHours", ref AirDryHours);
        ReadFloat(text, "WaterWashHours", ref WaterWashHours);
        ReadFloat(text, "WetClothesCold", ref WetClothesCold);
        ReadBool(text, "EnableFreezing", ref EnableFreezing);
        ReadFloat(text, "FreezeHours", ref FreezeHours);
        ReadFloat(text, "ThawHours", ref ThawHours);
        ReadFloat(text, "FridgeThawHours", ref FridgeThawHours);
        ReadFloat(text, "TVVolume", ref TVVolume);
        ReadFloat(text, "TVHearRange", ref TVHearRange);
        ReadFloat(text, "TVViewRange", ref TVViewRange);
        Log.Out("[HSWorkingStuff] Read " + path);
    }

    static void ReadBool(string text, string key, ref bool dest)
    {
        string rest = ValueAfter(text, key);
        if (rest == null)
            return;
        if (rest.StartsWith("true"))
            dest = true;
        else if (rest.StartsWith("false"))
            dest = false;
    }

    static void ReadFloat(string text, string key, ref float dest)
    {
        float v;
        if (TryParseNumber(text, key, out v) && v > 0f)
            dest = v;
    }

    static void ReadInt(string text, string key, ref int dest)
    {
        float v;
        if (TryParseNumber(text, key, out v) && v >= 1f)
            dest = (int)v;
    }

    static string ValueAfter(string text, string key)
    {
        int i = text.IndexOf("\"" + key + "\"");
        if (i < 0)
            i = text.IndexOf(key);
        if (i < 0)
            return null;
        int colon = text.IndexOf(':', i);
        if (colon < 0)
            return null;
        return text.Substring(colon + 1).TrimStart();
    }

    static bool TryParseNumber(string text, string key, out float value)
    {
        value = 0f;
        string rest = ValueAfter(text, key);
        if (rest == null)
            return false;
        int end = 0;
        while (end < rest.Length && (char.IsDigit(rest[end]) || rest[end] == '.' || rest[end] == '-'))
            end++;
        if (end <= 0)
            return false;
        return float.TryParse(rest.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
