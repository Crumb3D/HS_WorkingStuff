using System.Collections.Generic;
using Audio;
using UnityEngine;

// Magazines and perk ranks unlock our recipes through RecipeTagUnlocked. Vanilla only names the
// crafting-skill tier, so a new hs recipe never gets the schematic line. Queue the same tooltip.
public static class HSUnlock
{
    static readonly HashSet<string> known = new HashSet<string>();
    static bool primed;
    static float next;

    public static void Clear()
    {
        known.Clear();
        primed = false;
        next = 0f;
    }

    public static void ClientTick()
    {
        if (Time.time < next || GameManager.IsDedicatedServer)
            return;
        next = Time.time + 0.5f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (player == null || player.Progression == null)
            return;
        List<Recipe> all = CraftingManager.GetAllRecipes();
        if (all == null || all.Count == 0)
            return;
        var fresh = new List<string>();
        for (int i = 0; i < all.Count; i++)
        {
            Recipe recipe = all[i];
            if (recipe == null || !recipe.IsLearnable)
                continue;
            string name = recipe.GetName();
            if (name == null || name.Length < 3 || name[0] != 'h' || name[1] != 's')
                continue;
            if (!recipe.IsUnlocked(player))
            {
                known.Remove(name);
                continue;
            }
            if (!known.Add(name) || !primed)
                continue;
            fresh.Add(name);
        }
        if (!primed)
        {
            primed = true;
            return;
        }
        if (fresh.Count == 0)
            return;
        Manager.PlayInsidePlayerHead("recipe_unlocked");
        string format = Localization.Get("ttRecipeUnlocked");
        for (int i = 0; i < fresh.Count; i++)
        {
            ItemClass item = ItemClass.GetItemClass(fresh[i], false);
            string label = item != null ? item.GetLocalizedItemName() : Localization.Get(fresh[i]);
            GameManager.ShowTooltip(player, string.Format(format, label));
        }
    }
}
