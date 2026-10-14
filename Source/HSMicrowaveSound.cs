using System.Collections.Generic;
using Audio;
using UnityEngine;

// Hum while a powered microwave has something cooking, ding when the queue runs out.
// Only the server cooks a microwave (the door-open window pauses it), so the server drives
// the sound; the client with the door open just makes sure the hum is off.
public static class HSMicrowaveSound
{
    public const string Hum = "hsMicrowaveHum";
    public const string Ding = "hsMicrowaveDing";

    const float UiStaleSeconds = 1f;

    static readonly Dictionary<Vector3i, bool> serverState = new Dictionary<Vector3i, bool>();
    static readonly Dictionary<Vector3i, bool> uiState = new Dictionary<Vector3i, bool>();
    static readonly Dictionary<Vector3i, float> uiSeen = new Dictionary<Vector3i, float>();

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

    public static void ServerTick(TileEntityWorkstation te)
    {
        if (!HSWorkingStuffNet.IsAuthority || !IsMicrowave(te))
            return;
        Vector3i pos = te.ToWorldPos();
        if (te.IsUserAccessing())
        {
            serverState.Remove(pos);
            return;
        }
        bool queued = HasQueued(te.Queue);
        Apply(serverState, pos, queued && HSStovePower.AllowCook(te), queued);
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
        Apply(uiState, pos, false, true);
    }

    public static void Removed(Vector3i pos)
    {
        serverState.Remove(pos);
        uiState.Remove(pos);
        uiSeen.Remove(pos);
        if (HSWorkingStuffNet.IsAuthority)
            Manager.BroadcastStop(Center(pos), Hum);
    }

    static void Apply(Dictionary<Vector3i, bool> states, Vector3i pos, bool cooking, bool queued)
    {
        bool was;
        bool known = states.TryGetValue(pos, out was);
        states[pos] = cooking;
        if (known && was == cooking)
            return;
        Vector3 at = Center(pos);
        if (cooking)
        {
            Manager.BroadcastPlay(at, Hum);
            return;
        }
        Manager.BroadcastStop(at, Hum);
        if (known && was && !queued)
            Manager.BroadcastPlay(at, Ding);
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
