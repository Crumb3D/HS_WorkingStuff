using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Looted POI toilets and baths become the working ones. Looted kitchen appliances
// can then be repaired (wrench + parts) into the working fridge / cooker / microwave /
// coffee maker / washer / dryer, and picked up inside a land claim.
public static class HSSalvage
{
    public static readonly Dictionary<string, string> Immediate = new Dictionary<string, string>
    {
        { "cntToilet01", "hsToilet" },
        { "cntToilet02", "hsToilet" },
        { "cntToilet03", "hsToilet" },
        { "cntBathTubGore", "cntBathTubEmpty" }
    };

    public static readonly Dictionary<string, string> Repair = new Dictionary<string, string>
    {
        { "cntRetroFridgeVer1Closed", "hsFridge" },
        { "cntRetroFridgeVer1Closed_Player", "hsFridge" },
        { "cntRetroFridgeVer2Open", "hsFridge" },
        { "cntFridgeStainlessSteel", "hsFridge" },
        { "cntFridgeStainlessSteel_Player", "hsFridge" },
        { "cntFridgeStainlessSteelVer2Open", "hsFridge" },
        { "cntStoveOldVer1Closed", "hsStoveOld" },
        { "cntStoveOldVer1Closed_Player", "hsStoveOld" },
        { "cntStoveOldVer2Open", "hsStoveOld" },
        { "cntStoveModernVer1Closed", "hsStoveModern" },
        { "cntStoveModernVer1Closed_Player", "hsStoveModern" },
        { "cntStoveModernVer2Open", "hsStoveModern" },
        { "cntwallOvenEmptyWhite", "hsStoveWall" },
        { "cntwallOvenEmptyGrey", "hsStoveWall" },
        { "cntwallOvenEmptyBlack", "hsStoveWall" },
        { "cntwallOvenFullWhite", "hsStoveWall" },
        { "cntwallOvenFullGrey", "hsStoveWall" },
        { "cntwallOvenFullBlack", "hsStoveWall" },
        { "cntwallOvenFullWhite_Player", "hsStoveWall" },
        { "cntwallOvenFullGrey_Player", "hsStoveWall" },
        { "cntwallOvenFullBlack_Player", "hsStoveWall" },
        { "cntMicrowaveOvenVer1Closed", "hsMicrowave" },
        { "cntMicrowaveOvenVer1Closed_Player", "hsMicrowave" },
        { "cntMicrowaveOvenVer1Open", "hsMicrowave" },
        { "cntMicrowaveOvenVer2Closed", "hsMicrowaveRetro" },
        { "cntMicrowaveOvenVer2Closed_Player", "hsMicrowaveRetro" },
        { "cntMicrowaveOvenVer2Open", "hsMicrowaveRetro" },
        { "cntCoffeeMaker", "hsCoffeeMaker" },
        { "cntCoffeeMakerCommercial", "hsCoffeeMakerCommercial" },
        { "cntToaster", "hsToasterUS" },
        { "cntWasherClosed", "hsWasherVanilla" },
        { "cntWasherClosed_Player", "hsWasherVanilla" },
        { "cntWasherOpen", "hsWasherVanilla" },
        { "cntDryerClosed", "hsDryerVanilla" },
        { "cntDryerClosed_Player", "hsDryerVanilla" },
        { "cntDryerOpen", "hsDryerVanilla" },
        { "waterHeaterWhite", "hsWaterHeaterVanilla" },
        { "waterHeaterBrown", "hsWaterHeaterVanilla" },
        { "waterHeaterRed", "hsWaterHeaterVanilla" },
        { "waterHeaterYellow", "hsWaterHeaterVanilla" },
        { "waterHeaterGreen", "hsWaterHeaterVanilla" },
        { "waterHeaterBlue", "hsWaterHeaterVanilla" },
        { "waterHeaterGrey", "hsWaterHeaterVanilla" },
        { "waterHeaterBlack", "hsWaterHeaterVanilla" },
        { "radiatorHouse01", "hsRadiatorCastIron" },
        { "radiatorHouse01Player", "hsRadiatorCastIron" }
    };

    static readonly HashSet<string> Working = new HashSet<string>
    {
        "hsFridge", "hsFridgeWhite", "hsFreezer",
        "hsStoveOld", "hsStoveModern", "hsStoveWall",
        "hsMicrowave", "hsMicrowaveRetro",
        "hsCoffeeMaker", "hsCoffeeMakerCommercial",
        "hsWasher", "hsWasherVanilla", "hsDryer", "hsDryerVanilla",
        "hsToilet", "hsUrinal", "hsBath", "hsShower",
        "cntBathTubEmpty", "decoClawFootBathTub",
        "showerHead", "showerHead02",
        "hsToasterUS", "hsWaterHeaterVanilla", "hsRadiatorCastIron", "hsWaterHeater",
        "hsTVStand", "hsTVWall",
        "hsTVSmallStand1x1", "hsTVSmallWall1x1", "hsTVSmallStand2x1", "hsTVSmallWall2x1",
        "hsTVLargeStand2x2", "hsTVLargeWall2x2", "hsTVLargeStand3x2", "hsTVLargeWall3x2",
        "hsTVExtraSmallStand1x1", "hsTVExtraSmallWall1x1"
    };

    public static bool LootEmpty(WorldBase world, Vector3i pos)
    {
        if (world == null)
            return true;
        TileEntity te = world.GetTileEntity(pos);
        if (te == null)
            return true;
        TEFeatureStorage storage = HSInv.GetStorage(te);
        if (storage == null)
            return true;
        return storage.IsEmpty();
    }

    public static bool Convert(World world, Vector3i pos, EntityPlayer player)
    {
        if (world == null)
            return false;
        BlockValue bv = world.GetBlock(pos);
        if (bv.Block == null)
            return false;
        if (bv.ischild)
            pos = bv.Block.multiBlockPos.GetParentPos(pos, bv);
        bv = world.GetBlock(pos);
        string name = bv.Block != null ? bv.Block.GetBlockName() : null;
        string dest;
        if (string.IsNullOrEmpty(name) || !Immediate.TryGetValue(name, out dest))
            return false;
        if (!LootEmpty(world, pos) && name != "cntBathTubEmpty")
            return false;
        return Place(world, pos, bv, dest, player);
    }

    static bool Place(World world, Vector3i pos, BlockValue old, string dest, EntityPlayer player)
    {
        BlockValue nv = Block.GetBlockValue(dest);
        if (nv.type == 0 || nv.Block == null)
            return false;
        nv.rotation = old.rotation;
        nv.meta = old.meta;
        nv.damage = 0;
        world.SetBlockRPC(pos, nv);
        MarkPlaced(world, pos);
        if (player != null)
        {
            bool bath = dest == "hsBath" || dest == "cntBathTubEmpty";
            string key = bath ? "hsSalvageBath" : "hsSalvageToilet";
            string text = bath
                ? "The bath is usable. Pipe hot water to it when you can"
                : "The toilet is usable. A few pees before it needs a jar to flush; a poo always does";
            HSWorkingStuffNet.Tell(player, HSLoc.Get(key, text));
        }
        return true;
    }

    public static void MarkPlaced(WorldBase world, Vector3i pos)
    {
        if (world == null)
            return;
        TileEntity te = world.GetTileEntity(pos);
        if (te == null)
            return;
        PropertyInfo p = te.GetType().GetProperty("IsPlayerPlaced", BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.CanWrite)
            p.SetValue(te, true, null);
        te.SetModified();
    }

    public static bool IsWorking(string name)
    {
        return !string.IsNullOrEmpty(name) && Working.Contains(name);
    }
}

[HarmonyPatch(typeof(XUiC_LootWindow), "OnClose")]
public static class HSPatchSalvageLootClose
{
    static Vector3i pending;
    static bool ready;

    static void Prefix(XUiC_LootWindow __instance)
    {
        ready = false;
        try
        {
            FieldInfo f = AccessTools.Field(typeof(XUiC_LootWindow), "te");
            object raw = f != null ? f.GetValue(__instance) : null;
            var ite = raw as ITileEntity;
            if (ite == null)
                return;
            Vector3i pos = ite.ToWorldPos();
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null)
                return;
            BlockValue bv = world.GetBlock(pos);
            if (bv.Block == null)
                return;
            if (bv.ischild)
                pos = bv.Block.multiBlockPos.GetParentPos(pos, bv);
            string name = world.GetBlock(pos).Block.GetBlockName();
            if (!HSSalvage.Immediate.ContainsKey(name))
                return;
            TEFeatureStorage storage = raw as TEFeatureStorage;
            if (storage != null && !storage.IsEmpty())
                return;
            pending = pos;
            ready = true;
        }
        catch { }
    }

    static void Postfix(XUiC_LootWindow __instance)
    {
        if (!ready)
            return;
        ready = false;
        try
        {
            EntityPlayerLocal p = __instance.xui != null && __instance.xui.playerUI != null
                ? __instance.xui.playerUI.entityPlayer : null;
            HSWorkingStuffNet.SendWater(HSWorkingStuffNet.Salvage, pending, p);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(EntityBuffs), "AddBuff", new[] { typeof(string), typeof(Vector3i), typeof(int), typeof(bool), typeof(bool), typeof(float) })]
public static class HSPatchGodBuff
{
    static void Postfix(EntityBuffs __instance, string _name)
    {
        if (_name != "god" || __instance == null)
            return;
        EntityPlayerLocal p = __instance.parent as EntityPlayerLocal;
        if (p != null)
            HSGod.Clear(p);
    }
}

[HarmonyPatch(typeof(Block), "OnBlockDamaged")]
public static class HSPatchSalvagePlaced
{
    static void Postfix(WorldBase _world, BlockValueRef _bvRef, int _damagePoints, int _entityIdThatDamaged)
    {
        if (_damagePoints >= 0 || _world == null)
            return;
        Vector3i pos = _bvRef.BlockPosition;
        BlockValue now = _world.GetBlock(pos);
        if (now.Block != null && HSSalvage.IsWorking(now.Block.GetBlockName()))
            HSSalvage.MarkPlaced(_world, pos);
        HSFlipper.AfterUpgrade(_world, pos, _entityIdThatDamaged);
    }
}
