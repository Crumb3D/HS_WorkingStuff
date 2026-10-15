using System.Collections.Generic;
using Audio;
using UnityEngine;

// Hum while a powered workstation is cooking, then a done sound when the queue runs out.
// Microwave, toaster, coffee maker and electric cooker. Fridge and freezer stay quiet.
// Only the server cooks a microwave (the door-open window pauses it), so the server drives
// the sound; the client with the door open just makes sure the hum is off.
public static class HSMicrowaveSound
{
    public const string Hum = "hsMicrowaveHum";
    public const string Ding = "hsMicrowaveDing";
    public const string ToasterHum = "hsToasterHum";
    public const string ToasterPop = "hsToasterPop";
    public const string CoffeeHum = "hsCoffeeBrew";
    public const string StoveHum = "hsStoveCook";

    const float UiStaleSeconds = 1f;

    static readonly Dictionary<Vector3i, bool> serverState = new Dictionary<Vector3i, bool>();
    static readonly Dictionary<Vector3i, bool> uiState = new Dictionary<Vector3i, bool>();
    static readonly Dictionary<Vector3i, float> uiSeen = new Dictionary<Vector3i, float>();
    static readonly Dictionary<Vector3i, string> humAt = new Dictionary<Vector3i, string>();

    public static bool IsMicrowave(TileEntityWorkstation te)
    {
        if (te == null)
            return false;
        try
        {
            return te.blockValue.Block is BlockHSMicrowave;
        }
        catch
        {
            return false;
        }
    }

    static bool SoundsOf(Block block, out string hum, out string done)
    {
        hum = null;
        done = Ding;
        if (block is BlockHSKettle)
            return false;
        if (block is BlockHSMicrowave)
        {
            hum = Hum;
            return true;
        }
        if (block is BlockHSToaster)
        {
            hum = ToasterHum;
            done = ToasterPop;
            return true;
        }
        if (block is BlockHSCoffeeMaker)
        {
            hum = CoffeeHum;
            return true;
        }
        if (block is BlockHSStove)
        {
            hum = StoveHum;
            return true;
        }
        return false;
    }

    public static void ServerTick(TileEntityWorkstation te)
    {
        if (!HSWorkingStuffNet.IsAuthority || te == null)
            return;
        Block block;
        try
        {
            block = te.blockValue.Block;
        }
        catch
        {
            return;
        }
        string hum;
        string done;
        if (!SoundsOf(block, out hum, out done))
            return;
        Vector3i pos = te.ToWorldPos();
        bool microwave = block is BlockHSMicrowave;
        if (microwave && te.IsUserAccessing())
        {
            serverState.Remove(pos);
            StopHum(pos);
            return;
        }
        bool queued = HasQueued(te.Queue);
        bool cooking = queued && HSStovePower.AllowCook(te);
        if (microwave)
            cooking = cooking && !te.IsUserAccessing();
        Apply(serverState, pos, cooking, queued, hum, done);
    }

    public static void DoorOpen(TileEntityWorkstation te)
    {
        if (!IsMicrowave(te))
            return;
        Vector3i pos = te.ToWorldPos();
        float last;
        if (uiSeen.TryGetValue(pos, out last) && Time.time - last > UiStaleSeconds)
            uiState.Remove(pos);
        uiSeen[pos] = Time.time;
        Apply(uiState, pos, false, true, Hum, Ding);
    }

    public static void Removed(Vector3i pos)
    {
        serverState.Remove(pos);
        uiState.Remove(pos);
        uiSeen.Remove(pos);
        StopHum(pos);
    }

    public static void PlayAt(Vector3i pos, string sound)
    {
        if (string.IsNullOrEmpty(sound))
            return;
        Manager.BroadcastPlay(Center(pos), sound);
    }

    static void Apply(Dictionary<Vector3i, bool> states, Vector3i pos, bool cooking, bool queued, string hum, string done)
    {
        bool was;
        bool known = states.TryGetValue(pos, out was);
        states[pos] = cooking;
        if (known && was == cooking)
            return;
        Vector3 at = Center(pos);
        if (cooking)
        {
            StopHum(pos);
            Manager.BroadcastPlay(at, hum);
            humAt[pos] = hum;
            return;
        }
        StopHum(pos);
        if (known && was && !queued)
            Manager.BroadcastPlay(at, done);
    }

    static void StopHum(Vector3i pos)
    {
        string hum;
        if (humAt.TryGetValue(pos, out hum) && hum != null)
            Manager.BroadcastStop(Center(pos), hum);
        humAt.Remove(pos);
        Manager.BroadcastStop(Center(pos), Hum);
    }

    public static bool HasQueued(RecipeQueueItem[] items)
    {
        if (items == null)
            return false;
        for (int i = 0; i < items.Length; i++)
        {
            RecipeQueueItem item = items[i];
            if (item != null && item.Multiplier > 0 && item.Recipe != null)
                return true;
        }
        return false;
    }

    static Vector3 Center(Vector3i p)
    {
        return new Vector3(p.x + 0.5f, p.y + 0.5f, p.z + 0.5f);
    }
}
