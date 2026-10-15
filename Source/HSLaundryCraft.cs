using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Runtime wash/dry recipes (one per clothing item) and the cooker-style queue:
// highlight worn or carried pieces, time scales with dirt or wetness, same item comes out.
public static class HSLaundryCraft
{
    public const string WashArea = "hsWasher";
    public const string DryArea = "hsDryer";

    static bool registered;
    static int alreadyTaken;
    static ItemValue pendingOut;
    static readonly FieldInfo infoRecipe = AccessTools.Field(typeof(XUiC_CraftingInfoWindow), "recipe");
    static readonly MethodInfo cacheVisible = AccessTools.Method(typeof(CraftingManager), "cacheNonScrapableRecipes");

    public static void Register()
    {
        if (!HSWorkingStuffSettings.EnableLaundry)
            return;
        if (registered)
        {
            ShowInUi();
            return;
        }
        ItemClass[] list = ItemClass.list;
        if (list == null || list.Length == 0)
            return;
        int n = 0;
        for (int i = 0; i < list.Length; i++)
        {
            ItemClass ic = list[i];
            if (!(ic is ItemClassArmor))
                continue;
            Add(ic, false);
            Add(ic, true);
            n++;
        }
        registered = true;
        ShowInUi();
        if (n > 0)
            Log.Out("[HSWorkingStuff] laundry recipes for " + n + " clothes and armour");
    }

    public static void ResetForNewGame()
    {
        registered = false;
    }

    public static void Clear()
    {
        alreadyTaken = 0;
        pendingOut = null;
    }

    // The cooker list reads CraftingManager.NonScrapableRecipes, which AddRecipe does not update.
    static void ShowInUi()
    {
        if (cacheVisible != null)
            cacheVisible.Invoke(null, null);
    }

    static void Add(ItemClass ic, bool dryer)
    {
        var recipe = new Recipe();
        recipe.itemValueType = ic.Id;
        recipe.count = 1;
        recipe.craftingTime = BaseSeconds(ic, dryer);
        recipe.craftingArea = dryer ? DryArea : WashArea;
        recipe.craftExpGain = 0;
        recipe.unlockExpGain = 0;
        recipe.UseIngredientModifier = false;
        recipe.AddIngredient(new ItemValue(ic.Id), 1);
        recipe.Init();
        CraftingManager.AddRecipe(recipe);
    }

    public static bool IsArea(string area)
    {
        return area == WashArea || area == DryArea;
    }

    public static bool IsLaundry(Recipe recipe)
    {
        return recipe != null && IsArea(recipe.craftingArea);
    }

    public static bool IsDryer(Recipe recipe)
    {
        return recipe != null && recipe.craftingArea == DryArea;
    }

    public static bool IsLaundry(TileEntityWorkstation te)
    {
        if (te == null)
            return false;
        try
        {
            return te.blockValue.Block is BlockHSLaundry;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsPrepared(Recipe recipe, float craftTime)
    {
        if (!IsLaundry(recipe) || craftTime <= 0f || recipe.ingredients == null || recipe.ingredients.Count != 1)
            return false;
        ItemValue iv = HSItem.Val(recipe.ingredients[0]);
        return iv != null && !iv.IsEmpty() && HSLaundry.IsClothing(iv);
    }

    public static float BaseSeconds(ItemClass ic, bool dryer)
    {
        float full = dryer
            ? Mathf.Max(1f, HSWorkingStuffSettings.DryerSeconds)
            : Mathf.Max(1f, HSWorkingStuffSettings.WasherSeconds);
        return Mathf.Max(2f, full * SlotScale(ic));
    }

    static float SlotScale(ItemClass ic)
    {
        var armor = ic as ItemClassArmor;
        if (armor == null)
            return 1f;
        switch (armor.EquipSlot)
        {
            case EquipmentSlots.ClothingHead:
            case EquipmentSlots.ClothingHands:
                return 0.4f;
            case EquipmentSlots.Hands:
            case EquipmentSlots.ClothingFeet:
            case EquipmentSlots.Feet:
                return 0.5f;
            case EquipmentSlots.Head:
                return 0.6f;
            case EquipmentSlots.Chest:
                return 1.2f;
            default:
                return 1f;
        }
    }

    public static float Amount(ItemValue iv, bool dryer, float nowH)
    {
        if (iv == null || iv.IsEmpty())
            return 0f;
        return dryer ? HSLaundry.Wetness(iv, nowH) : HSLaundry.Dirt(iv);
    }

    public static float SecondsFor(ItemClass ic, ItemValue iv, bool dryer, float nowH)
    {
        float amount = Amount(iv, dryer, nowH);
        if (amount <= 0f)
            return 0f;
        return Mathf.Max(2f, BaseSeconds(ic, dryer) * amount);
    }

    public static ItemValue Find(EntityPlayer player, int itemType, bool dryer, float nowH)
    {
        if (player == null)
            return null;
        ItemValue best = null;
        float bestAmt = 0f;
        Consider(HSInv.GetBagStacks(player), itemType, dryer, nowH, ref best, ref bestAmt);
        Consider(HSInv.GetToolStacks(player), itemType, dryer, nowH, ref best, ref bestAmt);
        if (player.equipment != null)
        {
            int n = player.equipment.GetSlotCount();
            for (int i = 0; i < n; i++)
                Score(player.equipment.GetSlotItem(i), itemType, dryer, nowH, ref best, ref bestAmt);
        }
        return best;
    }

    static void Consider(ItemStack[] stacks, int itemType, bool dryer, float nowH, ref ItemValue best, ref float bestAmt)
    {
        if (stacks == null)
            return;
        for (int i = 0; i < stacks.Length; i++)
        {
            if (stacks[i] == null || stacks[i].IsEmpty())
                continue;
            Score(HSItem.Val(stacks[i]), itemType, dryer, nowH, ref best, ref bestAmt);
        }
    }

    static void Score(ItemValue iv, int itemType, bool dryer, float nowH, ref ItemValue best, ref float bestAmt)
    {
        if (iv == null || iv.IsEmpty() || iv.GetItemId() != itemType || !HSLaundry.IsClothing(iv))
            return;
        float amt = Amount(iv, dryer, nowH);
        if (amt <= 0f)
            return;
        if (best == null || amt > bestAmt)
        {
            best = iv;
            bestAmt = amt;
        }
    }

    public static bool HasUsable(EntityPlayer player, Recipe recipe)
    {
        if (player == null || !IsLaundry(recipe))
            return false;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        return Find(player, recipe.itemValueType, IsDryer(recipe), HSLaundry.NowHours(world)) != null;
    }

    public static ItemValue Take(EntityPlayer player, int itemType, bool dryer, float nowH)
    {
        if (player == null)
            return null;
        ItemValue found = Find(player, itemType, dryer, nowH);
        if (found == null)
            return null;
        if (ClearMatching(HSInv.GetBagStacks(player), found))
        {
            HSInv.NotifyPlayer(player);
            return found.Clone();
        }
        if (ClearMatching(HSInv.GetToolStacks(player), found))
        {
            HSInv.NotifyPlayer(player);
            return found.Clone();
        }
        if (player.equipment != null)
        {
            int n = player.equipment.GetSlotCount();
            for (int i = 0; i < n; i++)
            {
                if (player.equipment.GetSlotItem(i) != found)
                    continue;
                HSInv.SetEquipSlot(player, i, ItemValue.None);
                return found.Clone();
            }
        }
        return found.Clone();
    }

    static bool ClearMatching(ItemStack[] stacks, ItemValue found)
    {
        if (stacks == null)
            return false;
        for (int i = 0; i < stacks.Length; i++)
        {
            if (stacks[i] == null || stacks[i].IsEmpty())
                continue;
            if (HSItem.Val(stacks[i]) != found)
                continue;
            HSItem.SetVal(stacks[i], ItemValue.None);
            HSItem.SetCount(stacks[i], 0);
            return true;
        }
        return false;
    }

    public static ItemValue Finish(ItemValue iv, bool dryer, float nowH)
    {
        if (iv == null || iv.IsEmpty())
            return iv;
        if (dryer)
            iv.SetMetadata(HSLaundry.MetaWet, -1f);
        else
        {
            iv.SetMetadata(HSLaundry.MetaDirt, 0f);
            iv.SetMetadata(HSLaundry.MetaWet, nowH);
        }
        return iv;
    }

    public static string InfoLine(EntityPlayer player, Recipe recipe)
    {
        if (!IsLaundry(recipe))
            return null;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        float nowH = HSLaundry.NowHours(world);
        bool dryer = IsDryer(recipe);
        ItemValue iv = Find(player, recipe.itemValueType, dryer, nowH);
        if (iv == null)
        {
            return dryer
                ? HSLoc.Get("hsLaundryNeedWet", "You are not wearing or carrying a wet one of these")
                : HSLoc.Get("hsLaundryNeedDirty", "You are not wearing or carrying a dirty one of these");
        }
        int pct = Mathf.RoundToInt(100f * Amount(iv, dryer, nowH));
        float secs = SecondsFor(iv.ItemClass, iv, dryer, nowH);
        if (dryer)
            return HSLoc.Format("hsLaundryDryInfo", "This one is {0}% wet. Drying takes {1}s", pct, Mathf.CeilToInt(secs));
        return HSLoc.Format("hsLaundryWashInfo", "This one is {0}% dirty. Washing takes {1}s", pct, Mathf.CeilToInt(secs));
    }

    public static Recipe InfoRecipe(XUiC_CraftingInfoWindow window)
    {
        return window != null && infoRecipe != null ? infoRecipe.GetValue(window) as Recipe : null;
    }

    public static float CraftTime(XUi xui, Recipe recipe)
    {
        if (xui == null || xui.playerUI == null)
            return recipe.craftingTime;
        EntityPlayer player = xui.playerUI.entityPlayer;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        float nowH = HSLaundry.NowHours(world);
        bool dryer = IsDryer(recipe);
        ItemValue iv = Find(player, recipe.itemValueType, dryer, nowH);
        if (iv == null)
            return recipe.craftingTime;
        return SecondsFor(iv.ItemClass, iv, dryer, nowH);
    }

    public static bool Queue(XUiC_CraftingWindowGroup group, Recipe recipe, int count)
    {
        if (group == null || !IsLaundry(recipe))
            return false;
        EntityPlayerLocal player = group.xui != null && group.xui.playerUI != null ? group.xui.playerUI.entityPlayer : null;
        if (player == null)
            return false;
        var station = group as XUiC_WorkstationWindowGroup;
        TileEntityWorkstation te = station != null && station.WorkstationData != null ? station.WorkstationData.TileEntity : null;
        Vector3i pos = te != null ? te.ToWorldPos() : Vector3i.zero;
        bool dryer = IsDryer(recipe);
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        float nowH = HSLaundry.NowHours(world);
        if (count < 1)
            count = 1;
        string reason;
        if (!MachineReady(station, recipe, out reason))
        {
            GameManager.ShowTooltip(player, reason);
            return false;
        }
        XUiC_CraftingQueue q = group.GetChildByType<XUiC_CraftingQueue>();
        BindQueue(q);
        bool any = false;
        for (int i = 0; i < count; i++)
        {
            ItemValue taken = Take(player, recipe.itemValueType, dryer, nowH);
            if (taken == null)
            {
                if (!any)
                    GameManager.ShowTooltip(player, dryer
                        ? HSLoc.Get("hsDryerEmpty", "Nothing wet to dry")
                        : HSLoc.Get("hsWasherEmpty", "Nothing to wash: put clothes or armour in first"));
                break;
            }
            if (!dryer)
                HSWorkingStuffNet.SendWater(HSWorkingStuffNet.Laundry, pos, player);
            float time = SecondsFor(taken.ItemClass, taken, dryer, nowH);
            var one = new Recipe();
            one.itemValueType = recipe.itemValueType;
            one.count = 1;
            one.craftingArea = recipe.craftingArea;
            one.craftingTime = time;
            one.craftExpGain = 0;
            one.UseIngredientModifier = false;
            one.AddIngredient(taken, 1);
            one.Init();
            if (q == null || !q.AddRecipeToCraft(one, 1, time))
            {
                GiveBack(player, taken);
                GameManager.ShowTooltip(player, HSLoc.Get("hsLaundryQueueFull", "The queue is full"));
                break;
            }
            alreadyTaken++;
            any = true;
        }
        return any;
    }

    static readonly FieldInfo queueItems = AccessTools.Field(typeof(XUiC_CraftingQueue), "queueItems");

    static void BindQueue(XUiC_CraftingQueue q)
    {
        if (q == null || queueItems == null)
            return;
        XUiController[] items = queueItems.GetValue(q) as XUiController[];
        if (items != null && items.Length > 0)
            return;
        items = q.GetChildrenByType<XUiC_RecipeStack>();
        if (items == null || items.Length == 0)
            return;
        queueItems.SetValue(q, items);
        for (int i = 0; i < items.Length; i++)
            ((XUiC_RecipeStack)items[i]).Owner = q;
    }

    static void GiveBack(EntityPlayer player, ItemValue iv)
    {
        if (player == null || iv == null || iv.IsEmpty())
            return;
        HSInv.AddToPlayer(player, new ItemStack(iv, 1));
    }

    public static bool SkipRemove(IList<ItemStack> stacks)
    {
        if (alreadyTaken <= 0 || stacks == null)
            return false;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i] == null || !HSLaundry.IsClothing(HSItem.Val(stacks[i])))
                continue;
            alreadyTaken = 0;
            return true;
        }
        return false;
    }

    public static void BeginOutput(Recipe recipe)
    {
        pendingOut = null;
        if (!IsLaundry(recipe) || recipe.ingredients == null || recipe.ingredients.Count == 0)
            return;
        ItemValue iv = HSItem.Val(recipe.ingredients[0]);
        if (iv == null || iv.IsEmpty())
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        pendingOut = Finish(iv.Clone(), IsDryer(recipe), HSLaundry.NowHours(world));
    }

    public static ItemValue TakeOutput(int itemType)
    {
        if (pendingOut == null || pendingOut.GetItemId() != itemType)
            return null;
        ItemValue iv = pendingOut;
        pendingOut = null;
        return iv;
    }

    public static ItemValue TakeOutput()
    {
        ItemValue iv = pendingOut;
        pendingOut = null;
        return iv;
    }

    public static bool MachineReady(XUiC_WorkstationWindowGroup group, Recipe recipe, out string reason)
    {
        reason = null;
        if (group == null || group.WorkstationData == null || group.WorkstationData.TileEntity == null)
            return true;
        Vector3i pos = group.WorkstationData.TileEntity.ToWorldPos();
        if (!HSStovePower.IsPowered(pos))
        {
            reason = HSLoc.Get("hsLaundryNoPower", "No power: wire it up first");
            return false;
        }
        if (IsDryer(recipe) || !HSWorkingStuffSettings.EnablePlumbing)
            return true;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return true;
        if (HSLaundry.HasWashWater(world, pos))
            return true;
        HSWater.Taps taps = HSWater.FindTaps(world, pos);
        reason = taps.Cold.Ok
            ? HSLoc.Format("hsWasherShort", "Not enough water: a wash needs {0} jars", HSWorkingStuffSettings.WasherJars)
            : HSWater.ColdReason(taps);
        return false;
    }
}

[HarmonyPatch(typeof(Recipe), "CanCraft")]
public static class HSPatchLaundryCanCraft
{
    static void Postfix(Recipe __instance, ref bool __result, EntityAlive _ea)
    {
        if (__result || !HSLaundryCraft.IsLaundry(__instance))
            return;
        __result = HSLaundryCraft.HasUsable(_ea as EntityPlayer, __instance);
    }
}

[HarmonyPatch(typeof(Recipe), "CanCraftAny")]
public static class HSPatchLaundryCanCraftAny
{
    static void Postfix(Recipe __instance, ref bool __result, EntityAlive _ea)
    {
        if (__result || !HSLaundryCraft.IsLaundry(__instance))
            return;
        __result = HSLaundryCraft.HasUsable(_ea as EntityPlayer, __instance);
    }
}

[HarmonyPatch(typeof(XUiM_Recipes), "GetRecipeCraftTime")]
public static class HSPatchLaundryCraftTime
{
    static void Postfix(XUi xui, Recipe _recipe, ref float __result)
    {
        if (!HSLaundryCraft.IsLaundry(_recipe))
            return;
        __result = HSLaundryCraft.CraftTime(xui, _recipe);
    }
}

[HarmonyPatch(typeof(XUiC_CraftingInfoWindow), "GetBindingValueInternal")]
public static class HSPatchLaundryInfo
{
    static void Postfix(XUiC_CraftingInfoWindow __instance, ref string value, string bindingName)
    {
        if (bindingName != "itemdescription")
            return;
        Recipe recipe = HSLaundryCraft.InfoRecipe(__instance);
        if (!HSLaundryCraft.IsLaundry(recipe))
            return;
        EntityPlayer player = __instance.xui != null && __instance.xui.playerUI != null ? __instance.xui.playerUI.entityPlayer : null;
        string extra = HSLaundryCraft.InfoLine(player, recipe);
        if (string.IsNullOrEmpty(extra))
            return;
        value = string.IsNullOrEmpty(value) ? extra : value + "\n\n" + extra;
    }
}

[HarmonyPatch(typeof(XUiC_CraftingWindowGroup), "AddItemToQueue", new[] { typeof(Recipe) })]
public static class HSPatchLaundryQueue1
{
    static bool Prefix(XUiC_CraftingWindowGroup __instance, Recipe _recipe, ref bool __result)
    {
        if (!HSLaundryCraft.IsLaundry(_recipe))
            return true;
        int count = 1;
        XUiC_RecipeCraftCount craft = __instance.GetChildByType<XUiC_RecipeCraftCount>();
        if (craft != null)
            count = craft.Count;
        __result = HSLaundryCraft.Queue(__instance, _recipe, count);
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_CraftingWindowGroup), "AddItemToQueue", new[] { typeof(Recipe), typeof(int) })]
public static class HSPatchLaundryQueueN
{
    static bool Prefix(XUiC_CraftingWindowGroup __instance, Recipe _recipe, int _count, ref bool __result)
    {
        if (!HSLaundryCraft.IsLaundry(_recipe))
            return true;
        __result = HSLaundryCraft.Queue(__instance, _recipe, _count);
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_CraftingWindowGroup), "AddItemToQueue", new[] { typeof(Recipe), typeof(int), typeof(float) })]
public static class HSPatchLaundryQueueTime
{
    static bool Prefix(XUiC_CraftingWindowGroup __instance, Recipe _recipe, int _count, float _craftTime, ref bool __result)
    {
        if (HSLaundryCraft.IsPrepared(_recipe, _craftTime) || !HSLaundryCraft.IsLaundry(_recipe))
            return true;
        __result = HSLaundryCraft.Queue(__instance, _recipe, _count);
        return false;
    }
}

[HarmonyPatch(typeof(XUiM_PlayerInventory), "RemoveItems")]
public static class HSPatchLaundryRemove
{
    static bool Prefix(IList<ItemStack> _itemStacks)
    {
        return !HSLaundryCraft.SkipRemove(_itemStacks);
    }
}

[HarmonyPatch(typeof(ItemActionEntryCraft), "hasItems")]
public static class HSPatchLaundryHasItems
{
    static void Postfix(XUi _xui, Recipe _recipe, ref bool __result)
    {
        if (__result || !HSLaundryCraft.IsLaundry(_recipe) || _xui == null || _xui.playerUI == null)
            return;
        __result = HSLaundryCraft.HasUsable(_xui.playerUI.entityPlayer, _recipe);
    }
}

[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup), "CraftingRequirementsValid")]
public static class HSPatchLaundryReq
{
    static void Postfix(XUiC_WorkstationWindowGroup __instance, Recipe _recipe, ref bool __result)
    {
        if (!__result || !HSLaundryCraft.IsLaundry(_recipe))
            return;
        string reason;
        if (!HSLaundryCraft.MachineReady(__instance, _recipe, out reason))
            __result = false;
    }
}

[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup), "CraftingRequirementsInvalidMessage")]
public static class HSPatchLaundryReqMsg
{
    static void Postfix(XUiC_WorkstationWindowGroup __instance, Recipe _recipe, ref string __result)
    {
        if (!HSLaundryCraft.IsLaundry(_recipe))
            return;
        string reason;
        if (HSLaundryCraft.MachineReady(__instance, _recipe, out reason))
            return;
        __result = reason;
    }
}

[HarmonyPatch(typeof(TileEntityWorkstation), "HandleRecipeQueue")]
public static class HSPatchLaundryOutput
{
    static void Prefix(TileEntityWorkstation __instance)
    {
        if (!HSLaundryCraft.IsLaundry(__instance))
            return;
        RecipeQueueItem[] queue = __instance.Queue;
        if (queue == null || queue.Length == 0)
            return;
        RecipeQueueItem item = queue[queue.Length - 1];
        if (item == null || item.Recipe == null || item.Multiplier <= 0 || item.CraftingTimeLeft > 0f)
            return;
        HSLaundryCraft.BeginOutput(item.Recipe);
    }
}

[HarmonyPatch(typeof(ItemStack), "AddToItemStackArray")]
public static class HSPatchLaundryStackOut
{
    static void Prefix(ref ItemStack _itemStack)
    {
        ItemValue have = HSItem.Val(_itemStack);
        if (_itemStack == null || _itemStack.IsEmpty() || have == null)
            return;
        ItemValue iv = HSLaundryCraft.TakeOutput(have.GetItemId());
        if (iv != null)
            _itemStack = new ItemStack(iv, 1);
    }
}

[HarmonyPatch(typeof(XUiC_RecipeStack), "outputStack")]
public static class HSPatchLaundryClientOut
{
    static void Prefix(XUiC_RecipeStack __instance)
    {
        Recipe recipe = __instance.GetRecipe();
        if (HSLaundryCraft.IsLaundry(recipe))
            HSLaundryCraft.BeginOutput(recipe);
    }

    static void Postfix(XUiC_RecipeStack __instance, bool __result)
    {
        ItemValue leftover = HSLaundryCraft.TakeOutput();
        if (leftover == null || !__result || __instance.WindowGroup == null)
            return;
        XUiC_WorkstationOutputGrid grid = __instance.WindowGroup.Controller.GetChildByType<XUiC_WorkstationOutputGrid>();
        if (grid == null)
            return;
        ItemStack[] slots = grid.GetSlots();
        if (slots == null)
            return;
        for (int i = slots.Length - 1; i >= 0; i--)
        {
            ItemValue have = HSItem.Val(slots[i]);
            if (slots[i] == null || slots[i].IsEmpty() || have == null)
                continue;
            if (have.GetItemId() != leftover.GetItemId())
                continue;
            slots[i] = new ItemStack(leftover, 1);
            grid.SetSlots(slots);
            grid.UpdateData(slots);
            return;
        }
    }
}

[HarmonyPatch(typeof(CraftingManager), "PostInit")]
public static class HSPatchLaundryPostInit
{
    static void Postfix()
    {
        HSLaundryCraft.Register();
    }
}

[HarmonyPatch(typeof(CraftingManager), "InitForNewGame")]
public static class HSPatchLaundryNewGame
{
    static void Postfix()
    {
        HSLaundryCraft.ResetForNewGame();
    }
}

// Leftover Basics/Food category from the last workstation would hide every clothing recipe.
[HarmonyPatch(typeof(XUiM_Recipes), "FilterRecipesByCategory")]
public static class HSPatchLaundryCategory
{
    static bool Prefix(ref List<Recipe> recipeList)
    {
        if (recipeList == null || recipeList.Count == 0)
            return true;
        for (int i = 0; i < recipeList.Count; i++)
        {
            if (HSLaundryCraft.IsLaundry(recipeList[i]))
                return false;
        }
        return true;
    }
}
