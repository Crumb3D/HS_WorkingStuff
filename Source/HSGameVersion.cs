using System;

// One DLL for every supported game. Detect the running 7DTD and pick the right API path.
// Minor 20 = 3.2, 30 = 3.3, 40 = 3.4. Never drop an older release when adding a newer one.
public static class HSGameVersion
{
    public static readonly string[] Supported = { "3.0", "3.1", "3.2", "3.3", "3.4" };

    public static string Current { get; private set; }

    // 3.2 (and older 3.x) use the compiled GetLength / field-shaped ItemStack path.
    public static bool Is32
    {
        get { return Current == "3.2" || Current == "3.1" || Current == "3.0"; }
    }

    // 3.3 and 3.4 share the newer ItemStack / fridge / emitted net-package shapes.
    // Keep the name Is33: existing call sites mean "modern API", not "exactly 3.3 only".
    public static bool Is33
    {
        get { return Current == "3.3" || Current == "3.4"; }
    }

    public static bool AllowLoad(string logPrefix)
    {
        string game;
        string detail;
        try
        {
            var v = Constants.cVersionInformation;
            game = ReleaseKey(v);
            Current = game;
            detail = v != null ? v.LongString : "unknown";
        }
        catch (Exception e)
        {
            Current = "";
            Log.Error(logPrefix + " REFUSING TO LOAD: could not read 7DTD version. " + e.Message);
            return false;
        }
        if (string.IsNullOrEmpty(game) || Array.IndexOf(Supported, game) < 0)
        {
            Log.Error(logPrefix + " REFUSING TO LOAD: this mod supports 7DTD "
                + string.Join(", ", Supported) + ". Game is "
                + (string.IsNullOrEmpty(game) ? "unknown" : game)
                + " (" + detail + "). Remove this folder from Mods or use a supported game version.");
            return false;
        }
        string path = Is33
            ? "Using 3.3/3.4 APIs (emitted net packages)."
            : "Using 3.2 APIs (compiled GetLength packages).";
        Log.Out(logPrefix + " 7DTD " + game + " (" + detail + ") is on the supported list: "
            + string.Join(", ", Supported) + ". " + path);
        return true;
    }

    public static string ReleaseKey(VersionInformation v)
    {
        if (v == null || !v.IsValid) return "";
        if (v.ReleaseType == VersionInformation.EGameReleaseType.V && v.Major >= 3)
            return v.Major + "." + (v.Minor / 10);
        return v.Major + "." + v.Minor;
    }
}
