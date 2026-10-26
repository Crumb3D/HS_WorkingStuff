using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class HSStovePower
{
    [ThreadStatic]
    static int lookingForPower;

    static readonly Dictionary<long, TileEntityPoweredBlock> sidecars = new Dictionary<long, TileEntityPoweredBlock>();
    static readonly Dictionary<long, bool> knownPower = new Dictionary<long, bool>();
    // Late joiners never saw the last change, so the state is re-sent now and then.
    static readonly Dictionary<long, float> nextResend = new Dictionary<long, float>();

    static long Key(Vector3i p)
    {
        return ((long)p.x << 42) ^ ((long)p.y << 21) ^ (uint)p.z;
    }

    public static bool IsStoveBlock(Block block)
    {
        return block is BlockHSStove;
    }

    public static bool IsStove(TileEntityWorkstation te)
    {
        if (te == null)
            return false;
        try
        {
            return te.blockValue.Block is BlockHSStove;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsPowered(TileEntityWorkstation te)
    {
        return te != null && IsPowered(te.ToWorldPos());
    }

    public static bool PluggedIn(WorldBase world, Vector3i pos)
    {
        TileEntityPowered te = GetSidecar(pos);
        Vector3i parent;
        return HSPlugs.Parent(te, out parent) && HSPlugs.IsSocket(world != null ? world.GetBlock(parent).Block : null);
    }

    public static bool IsPowered(Vector3i pos)
    {
        if (!HSWorkingStuffSettings.EnableStove)
            return false;
        if (HSWorkingStuffNet.IsAuthority)
        {
            try
            {
                PowerItem item = PowerManager.Instance != null
                    ? PowerManager.Instance.GetPowerItemByWorldPos(pos)
                    : null;
                if (item != null)
                    return item.IsPowered;
            }
            catch { }
            TileEntityPoweredBlock side = GetSidecar(pos);
            return side != null && side.IsPowered;
        }
        bool cached;
        if (knownPower.TryGetValue(Key(pos), out cached))
            return cached;
        TileEntityPoweredBlock client = GetSidecar(pos);
        return client != null && client.IsPowered;
    }

    public static void RememberPower(Vector3i pos, bool on)
    {
        knownPower[Key(pos)] = on;
    }

    public static void SetLooking(bool on)
    {
        if (on)
            lookingForPower++;
        else if (lookingForPower > 0)
            lookingForPower--;
    }

    public static TileEntity TryPowerTe(Vector3i worldPos, TileEntity current)
    {
        if (lookingForPower <= 0)
            return current;
        if (current is TileEntityPowered)
            return current;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return current;
        if (!(world.GetBlock(worldPos).Block is BlockHSStove))
            return current;
        Chunk chunk = world.GetChunkFromWorldPos(worldPos) as Chunk;
        if (chunk == null)
            return current;
        return Ensure(chunk, worldPos);
    }

    public static TileEntityPoweredBlock GetSidecar(Vector3i worldPos)
    {
        TileEntityPoweredBlock te;
        sidecars.TryGetValue(Key(worldPos), out te);
        return te;
    }

    public static TileEntityPoweredBlock Ensure(Chunk chunk, Vector3i worldPos)
    {
        long k = Key(worldPos);
        TileEntityPoweredBlock te;
        if (sidecars.TryGetValue(k, out te) && te != null)
        {
            BindTransform(chunk, worldPos, te);
            return te;
        }
        te = new TileEntityPoweredBlock(chunk);
        te.localChunkPos = World.toBlock(worldPos);
        te.PowerItemType = PowerItem.PowerItemTypes.Consumer;
        BindTransform(chunk, worldPos, te);
        try
        {
            te.InitializePowerData();
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] stove power init: " + e.Message);
        }
        sidecars[k] = te;
        return te;
    }

    static void BindTransform(Chunk chunk, Vector3i worldPos, TileEntityPoweredBlock te)
    {
        if (chunk == null || te == null)
            return;
        try
        {
            BlockEntityData bed = chunk.GetBlockEntity(worldPos);
            if (bed != null && bed.transform != null)
                te.BlockTransform = bed.transform;
        }
        catch { }
    }

    public static void Attach(WorldBase world, Chunk chunk, Vector3i worldPos)
    {
        if (chunk == null)
            return;
        Ensure(chunk, worldPos);
    }

    public static void Detach(WorldBase world, Vector3i worldPos)
    {
        long k = Key(worldPos);
        TileEntityPoweredBlock te;
        if (sidecars.TryGetValue(k, out te) && te != null)
        {
            try
            {
                if (HSWorkingStuffNet.IsAuthority && PowerManager.Instance != null)
                    PowerManager.Instance.RemovePowerNode(te.GetPowerItem());
            }
            catch { }
            try
            {
                te.RemoveWires();
            }
            catch { }
        }
        sidecars.Remove(k);
        knownPower.Remove(k);
        nextResend.Remove(k);
    }

    public static void TickStove(TileEntityWorkstation te)
    {
        if (te == null || !IsStove(te))
            return;
        bool wants = WantsPower(te);
        HSPowerDemand.Set(te.ToWorldPos(), wants ? HSPowerDemand.Watts(te.blockValue.Block, 10) : (ushort)0);
        bool on = IsPowered(te);
        bool burning = on && wants;
        if (te.IsBurning != burning)
            te.IsBurning = burning;
        long k = Key(te.ToWorldPos());
        bool last;
        float now = Time.time;
        float due;
        bool changed = !knownPower.TryGetValue(k, out last) || last != on;
        if (changed || !nextResend.TryGetValue(k, out due) || now >= due)
        {
            knownPower[k] = on;
            nextResend[k] = now + 5f;
            HSWorkingStuffNet.BroadcastStovePower(te.ToWorldPos(), on);
        }
    }

    // A cooker with its window open may be crafting in the player's UI, so it keeps the hob on.
    // A microwave only runs with the door shut.
    static bool WantsPower(TileEntityWorkstation te)
    {
        if (te.blockValue.Block is BlockHSKettle)
            return HSKettle.Boiling(te.blockValue);
        bool queued = HSMicrowaveSound.HasQueued(te.Queue);
        bool open = te.IsUserAccessing();
        if (HSMicrowaveSound.IsMicrowave(te))
            return queued && !open;
        return queued || open;
    }

    public static bool AllowCook(TileEntityWorkstation te)
    {
        if (!IsStove(te))
            return true;
        if (!HSWorkingStuffSettings.EnableStove || !IsPowered(te))
            return false;
        Block b = te.blockValue.Block;
        if (b is BlockHSKettle || b is BlockHSToaster)
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            return PluggedIn(world, te.ToWorldPos());
        }
        return true;
    }
}

[HarmonyPatch(typeof(TileEntityWorkstation), "HandleRecipeQueue")]
public static class HSPatchStoveCook
{
    static bool Prefix(TileEntityWorkstation __instance)
    {
        return HSStovePower.AllowCook(__instance);
    }
}

[HarmonyPatch(typeof(TileEntityWorkstation), "UpdateTick")]
public static class HSPatchStoveTick
{
    // Chunk.UpdateTick has no guard, so a throw here would stop every later tile entity in the chunk.
    static void Prefix(TileEntityWorkstation __instance)
    {
        try
        {
            HSStovePower.TickStove(__instance);
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] stove tick: " + e.Message);
        }
    }

    static void Postfix(TileEntityWorkstation __instance)
    {
        try
        {
            HSMicrowaveSound.ServerTick(__instance);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(TileEntityWorkstation), "get_IsCrafting")]
public static class HSPatchStoveCrafting
{
    static void Postfix(TileEntityWorkstation __instance, ref bool __result)
    {
        if (__result && HSStovePower.IsStove(__instance) && !HSStovePower.IsPowered(__instance))
            __result = false;
    }
}

// With the window open the recipe stacks craft client-side; vanilla only halts them via a fuel window.
// A microwave never runs with its door open, powered or not.
[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup), "Update")]
public static class HSPatchStoveWindowHalt
{
    static void Prefix(XUiC_WorkstationWindowGroup __instance)
    {
        try
        {
            Halt(__instance);
        }
        catch { }
    }

    static void Postfix(XUiC_WorkstationWindowGroup __instance)
    {
        try
        {
            TileEntityWorkstation te = Halt(__instance);
            if (te != null)
                HSMicrowaveSound.DoorOpen(te);
        }
        catch { }
    }

    static TileEntityWorkstation Halt(XUiC_WorkstationWindowGroup group)
    {
        XUiM_Workstation data = group.WorkstationData;
        XUiC_CraftingQueue queue = group.craftingQueue;
        if (data == null || queue == null)
            return null;
        TileEntityWorkstation te = data.TileEntity;
        if (!HSStovePower.IsStove(te))
            return null;
        bool doorOpen = HSMicrowaveSound.IsMicrowave(te);
        if ((doorOpen || !HSStovePower.AllowCook(te)) && queue.IsCrafting())
            queue.HaltCrafting();
        return te;
    }
}

[HarmonyPatch(typeof(ItemActionConnectPower), "GetPoweredBlock", new Type[] { typeof(ItemInventoryData) })]
public static class HSPatchStoveWireInv
{
    static void Postfix(ItemInventoryData data, ref TileEntityPowered __result)
    {
        World world = HSInvData.WorldOf(data);
        if (__result != null || data == null || world == null || HSInvData.HoldingEntity(data) == null)
            return;
        var player = HSInvData.HoldingEntity(data) as EntityPlayerLocal;
        if (player == null || player.HitInfo == null)
            return;
        Vector3i pos = player.HitInfo.hit.blockPos;
        if (!(world.GetBlock(pos).Block is BlockHSStove))
            return;
        Chunk chunk = world.GetChunkFromWorldPos(pos) as Chunk;
        if (chunk == null)
            return;
        __result = HSStovePower.Ensure(chunk, pos);
    }
}

[HarmonyPatch(typeof(ItemActionConnectPower), "GetPoweredBlock", new Type[] { typeof(Vector3i) })]
public static class HSPatchStoveWirePos
{
    static void Postfix(Vector3i tileEntityPos, ref TileEntityPowered __result)
    {
        if (__result != null)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        if (!(world.GetBlock(tileEntityPos).Block is BlockHSStove))
            return;
        Chunk chunk = world.GetChunkFromWorldPos(tileEntityPos) as Chunk;
        if (chunk == null)
            return;
        __result = HSStovePower.Ensure(chunk, tileEntityPos);
    }
}

[HarmonyPatch(typeof(ItemActionConnectPower), "GetCrosshairType")]
public static class HSPatchStoveCrosshair
{
    static void Postfix(ItemActionData _actionData, ref ItemClass.EnumCrosshairType __result)
    {
        if (_actionData == null || _actionData.invData == null)
            return;
        var player = HSInvData.HoldingEntity(_actionData.invData) as EntityPlayerLocal;
        if (player == null || player.HitInfo == null || !player.HitInfo.bHitValid)
            return;
        if (player.HitInfo.hit.blockValue.Block is BlockHSStove)
            __result = ItemClass.EnumCrosshairType.PowerItem;
    }
}

[HarmonyPatch(typeof(ItemActionConnectPower), "OnHoldingUpdate")]
public static class HSPatchStoveWireHold
{
    static void Prefix()
    {
        HSStovePower.SetLooking(true);
    }

    static void Postfix(ItemActionData _actionData)
    {
        try
        {
            if (_actionData == null || _actionData.invData == null)
                return;
            var data = _actionData as ItemActionConnectPower.ConnectPowerData;
            if (data == null || data.playerUI == null)
                return;
            var player = HSInvData.HoldingEntity(_actionData.invData) as EntityPlayerLocal;
            if (player == null || player.HitInfo == null || !player.HitInfo.bHitValid)
                return;
            Vector3i pos = player.HitInfo.hit.blockPos;
            World world = HSInvData.WorldOf(_actionData.invData);
            if (world == null)
                return;
            var stove = world.GetBlock(pos).Block as BlockHSStove;
            if (stove == null)
                return;
            bool on = HSStovePower.IsPowered(pos);
            data.playerUI.nguiWindowManager.SetLabel(EnumNGUIWindow.PowerInfo, stove.WattLabel(), on ? Color.yellow : Color.grey);
        }
        finally
        {
            HSStovePower.SetLooking(false);
        }
    }
}

[HarmonyPatch(typeof(ItemActionConnectPower), "StopHolding")]
public static class HSPatchStoveWireStop
{
    static void Prefix()
    {
        HSStovePower.SetLooking(true);
    }

    static void Postfix()
    {
        HSStovePower.SetLooking(false);
    }
}

[HarmonyPatch(typeof(ItemActionConnectPower), "DisconnectWire")]
public static class HSPatchStoveWireDisc
{
    static void Prefix()
    {
        HSStovePower.SetLooking(true);
    }

    static void Postfix()
    {
        HSStovePower.SetLooking(false);
    }
}

[HarmonyPatch(typeof(TileEntityPowered), "DrawWires")]
public static class HSPatchStoveDrawWires
{
    static void Prefix()
    {
        HSStovePower.SetLooking(true);
    }

    static void Postfix()
    {
        HSStovePower.SetLooking(false);
    }
}

[HarmonyPatch(typeof(World), "GetTileEntity", new Type[] { typeof(Vector3i) })]
public static class HSPatchStoveWorldTe
{
    static void Postfix(Vector3i _pos, ref TileEntity __result)
    {
        __result = HSStovePower.TryPowerTe(_pos, __result);
    }
}

[HarmonyPatch(typeof(Chunk), "GetTileEntity", new Type[] { typeof(Vector3i) })]
public static class HSPatchStoveChunkTe
{
    static void Postfix(ref TileEntity __result)
    {
        if (__result == null)
            return;
        __result = HSStovePower.TryPowerTe(__result.ToWorldPos(), __result);
    }
}

// The cooker is the campfire without the fuel, and a fifth faster. The microwave is another 30% off that.
[HarmonyPatch(typeof(XUiM_Recipes), "GetRecipeCraftTime")]
[HarmonyPriority(Priority.Last)]
public static class HSPatchCookSpeed
{
    public const float Stove = 0.8f;
    public const float Microwave = 0.56f;

    static void Postfix(XUi xui, Recipe _recipe, ref float __result)
    {
        if (__result <= 0f || _recipe == null || HSLaundryCraft.IsLaundry(_recipe))
            return;
        float scale = Scale(xui);
        if (scale < 0.99f)
            __result *= scale;
    }

    static float Scale(XUi xui)
    {
        if (xui == null)
            return 1f;
        List<XUiC_WorkstationWindowGroup> groups = xui.GetChildrenByType<XUiC_WorkstationWindowGroup>();
        if (groups == null)
            return 1f;
        for (int i = 0; i < groups.Count; i++)
        {
            XUiC_WorkstationWindowGroup group = groups[i];
            if (group == null || group.WindowGroup == null || !group.WindowGroup.isShowing)
                continue;
            if (group.WorkstationData == null || group.WorkstationData.TileEntity == null)
                continue;
            Block block = group.WorkstationData.TileEntity.blockValue.Block;
            if (block is BlockHSMicrowave)
                return Microwave;
            if (block != null && block.GetType() == typeof(BlockHSStove))
                return Stove;
        }
        return 1f;
    }
}
