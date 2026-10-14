using System;
using System.Collections.Generic;
using HarmonyLib;
using Platform;

public static class HSJoinHold
{
    public const string AuthStateKey = "hsAuthServerStarting";

    static readonly List<ClientInfo> waiting = new List<ClientInfo>();
    static ServerStateAuthorizer authorizer;
    static IAuthorizationResponses handler;
    static float nextNudge;
    static float nextHoldLog;

    public static bool WorldReady()
    {
        try
        {
            return GameManager.Instance != null
                && GameManager.Instance.gameStateManager != null
                && GameManager.Instance.gameStateManager.IsGameStarted();
        }
        catch
        {
            return false;
        }
    }

    public static bool StillLoading()
    {
        return !WorldReady();
    }

    public static void Hold(ServerStateAuthorizer inst, ClientInfo client)
    {
        if (inst != null)
            authorizer = inst;
        CaptureHandler(inst);
        if (client == null)
            return;
        for (int i = 0; i < waiting.Count; i++)
        {
            if (waiting[i] == client)
            {
                Nudge(client);
                return;
            }
        }
        waiting.Add(client);
        Log.Out("[HSWorkingStuff] Holding " + ClientLabel(client) + " until the world is ready.");
        Nudge(client);
        if (WorldReady())
            Tick();
    }

    public static void Drop(ClientInfo client)
    {
        if (client == null)
            return;
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            if (waiting[i] == client)
                waiting.RemoveAt(i);
        }
    }

    public static void Clear()
    {
        waiting.Clear();
        authorizer = null;
        handler = null;
        nextNudge = 0f;
        nextHoldLog = 0f;
    }

    public static void Tick()
    {
        if (waiting.Count == 0)
            return;
        Prune();
        if (waiting.Count == 0)
            return;
        if (!WorldReady())
        {
            if (UnityEngine.Time.unscaledTime >= nextHoldLog)
            {
                nextHoldLog = UnityEngine.Time.unscaledTime + 15f;
                Log.Out("[HSWorkingStuff] Still holding " + waiting.Count + " player(s), world not started yet.");
            }
            if (UnityEngine.Time.unscaledTime >= nextNudge)
            {
                nextNudge = UnityEngine.Time.unscaledTime + 5f;
                for (int i = 0; i < waiting.Count; i++)
                    Nudge(waiting[i]);
            }
            return;
        }
        ResumeAll();
    }

    public static void KeepWaitingInAuthSet()
    {
        for (int i = 0; i < waiting.Count; i++)
            EnsureInAuthSet(waiting[i]);
    }

    static void EnsureInAuthSet(ClientInfo client)
    {
        if (client == null)
            return;
        AuthorizationManager mgr = AuthorizationManager.Instance;
        if (mgr == null)
            return;
        var field = AccessTools.Field(typeof(AuthorizationManager), "clientsInAuthorization");
        if (field == null)
            return;
        HashSet<ClientInfo> set = field.GetValue(mgr) as HashSet<ClientInfo>;
        if (set == null)
            return;
        if (set.Add(client))
            Log.Out("[HSWorkingStuff] Re-queued held login for " + ClientLabel(client));
    }

    static void CaptureHandler(ServerStateAuthorizer inst)
    {
        if (handler != null)
            return;
        if (inst != null)
        {
            var field = AccessTools.Field(typeof(AuthorizerAbs), "authResponsesHandler");
            if (field != null)
                handler = field.GetValue(inst) as IAuthorizationResponses;
        }
        if (handler == null)
            handler = AuthorizationManager.Instance;
    }

    static void ResumeAll()
    {
        CaptureHandler(authorizer);
        if (handler == null || authorizer == null)
        {
            Log.Warning("[HSWorkingStuff] World is ready but join-hold cannot continue login (missing authorizer).");
            return;
        }
        List<ClientInfo> ready = new List<ClientInfo>(waiting);
        waiting.Clear();
        for (int i = 0; i < ready.Count; i++)
        {
            ClientInfo client = ready[i];
            if (client == null || client.disconnecting)
                continue;
            try
            {
                if (GameStats.GetInt(EnumGameStats.GameState) == 2)
                {
                    handler.AuthorizationDenied(authorizer, client, new GameUtils.KickPlayerData(GameUtils.EKickReason.GamePaused));
                    continue;
                }
                EnsureInAuthSet(client);
                Log.Out("[HSWorkingStuff] World ready, continuing login for " + ClientLabel(client));
                handler.AuthorizationAccepted(authorizer, client);
            }
            catch (Exception e)
            {
                Log.Error("[HSWorkingStuff] Failed to continue held login for " + ClientLabel(client) + ": " + e);
            }
        }
    }

    static void Prune()
    {
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            ClientInfo client = waiting[i];
            if (client == null || client.disconnecting)
                waiting.RemoveAt(i);
        }
    }

    static void Nudge(ClientInfo client)
    {
        if (client == null || client.disconnecting)
            return;
        try
        {
            client.SendPackage(NetPackageManager.GetPackage<NetPackageAuthState>().Setup(AuthStateKey));
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] Could not send starting-up wait to " + ClientLabel(client) + ": " + e.Message);
        }
    }

    static string ClientLabel(ClientInfo client)
    {
        if (client == null)
            return "?";
        if (!string.IsNullOrEmpty(client.playerName))
            return client.playerName;
        return "client " + client.ClientNumber;
    }

    public static void CancelClientWait()
    {
        ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (cm == null)
            return;
        Log.Out("[HSWorkingStuff] Cancelled waiting for the server.");
        cm.Disconnect();
        if (LocalPlayerUI.primaryUI != null && LocalPlayerUI.primaryUI.windowManager != null)
            LocalPlayerUI.primaryUI.windowManager.Open(XUiC_MainMenu.ID, true);
    }

    public static void ShowWaitOnClient()
    {
        if (LocalPlayerUI.primaryUI == null)
            return;
        string text = HSLoc.Get(AuthStateKey, "Server is Starting up please wait");
        try
        {
            text = text + "\n\n[FFFFFF]" + Utils.GetCancellationMessage();
        }
        catch { }
        XUiC_ProgressWindow.SetText(LocalPlayerUI.primaryUI, text, false);
        XUiC_ProgressWindow.SetEscDelegate(LocalPlayerUI.primaryUI, CancelClientWait);
    }
}

[HarmonyPatch(typeof(ServerStateAuthorizer), "Authorize")]
public static class HSPatchJoinHoldAuthorize
{
    static void Postfix(ServerStateAuthorizer __instance, ClientInfo _clientInfo, ref ValueTuple<EAuthorizerSyncResult, GameUtils.KickPlayerData?> __result)
    {
        if (!HSWorkingStuffSettings.HoldJoinUntilReady)
            return;
        if (__result.Item1 != EAuthorizerSyncResult.SyncDeny)
            return;
        GameUtils.KickPlayerData? kick = __result.Item2;
        if (!kick.HasValue || kick.Value.reason != GameUtils.EKickReason.GameStillLoading)
            return;
        HSJoinHold.Hold(__instance, _clientInfo);
        __result = new ValueTuple<EAuthorizerSyncResult, GameUtils.KickPlayerData?>(EAuthorizerSyncResult.WaitAsync, null);
    }
}

[HarmonyPatch(typeof(ServerStateAuthorizer), "get_StateLocalizationKey")]
public static class HSPatchJoinHoldStateKey
{
    static void Postfix(ref string __result)
    {
        if (!HSWorkingStuffSettings.HoldJoinUntilReady)
            return;
        if (!HSJoinHold.StillLoading())
            return;
        __result = HSJoinHold.AuthStateKey;
    }
}

[HarmonyPatch(typeof(AuthorizerAbs), "Disconnect")]
public static class HSPatchJoinHoldDisconnect
{
    static void Prefix(AuthorizerAbs __instance, ClientInfo _clientInfo)
    {
        if (__instance is ServerStateAuthorizer)
            HSJoinHold.Drop(_clientInfo);
    }
}

[HarmonyPatch(typeof(GameStateManager), "StartGame")]
public static class HSPatchJoinHoldGameStarted
{
    static void Postfix()
    {
        HSJoinHold.KeepWaitingInAuthSet();
        HSJoinHold.Tick();
    }
}

[HarmonyPatch(typeof(AuthorizationManager), "ServerStart")]
public static class HSPatchJoinHoldAuthStart
{
    static void Postfix()
    {
        HSJoinHold.KeepWaitingInAuthSet();
        HSJoinHold.Tick();
    }
}

[HarmonyPatch(typeof(NetPackageAuthState), "ProcessPackage")]
public static class HSPatchJoinHoldAuthState
{
    static void Postfix(NetPackageAuthState __instance)
    {
        if (GameManager.IsDedicatedServer)
            return;
        ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (cm == null || !cm.IsConnected || cm.IsServer)
            return;
        var field = AccessTools.Field(typeof(NetPackageAuthState), "stateKey");
        string key = field != null ? field.GetValue(__instance) as string : null;
        if (key != HSJoinHold.AuthStateKey)
            return;
        HSJoinHold.ShowWaitOnClient();
    }
}
