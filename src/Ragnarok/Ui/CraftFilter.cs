using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Ui;

internal enum CraftCategory { Weapons, Armor, Ammo, Tools, Health, Stamina, Eitr, Materials, Other }

// The crafting search and category filters, shared by the Ragnarök crafting panel and the vanilla one.
// Valheim's own recipe list is filtered after it is built (SlotRecipeListPatch), so selection, keys and
// gamepad all work on what is shown.
internal static class CraftFilter
{
    internal static string Query = "";
    internal static readonly HashSet<CraftCategory> Categories = new();
    internal static bool CraftableOnly;

    // The categories of every recipe this station offers (known ones, craftable or not), before the
    // filters: only these get a chip. Set each time Valheim builds its list.
    internal static readonly HashSet<CraftCategory> Present = new();

    // On while either crafting panel shows the filters.
    internal static Func<bool> Enabled = () => false;

    private static readonly MethodInfo UpdateCraftingPanel = AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel");

    internal static bool Active => Enabled() && (Query.Length > 0 || Categories.Count > 0 || CraftableOnly);

    internal static bool Matches(Recipe recipe, bool canCraft)
    {
        ItemDrop.ItemData.SharedData shared = recipe.m_item.m_itemData.m_shared;
        if (CraftableOnly && !canCraft) return false;
        if (Categories.Count > 0 && !Categories.Contains(Of(shared))) return false;
        return Query.Length == 0 || Localization.instance.Localize(shared.m_name).IndexOf(Query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Food and meads go by what they restore: the largest of health, stamina and eitr, or for a mead its
    // effect. What is still to be fermented or baked (mead bases, bread dough) goes by what it becomes.
    // Food or meads that restore none of the three (resistance meads) are Other; the rest go by item type.
    internal static CraftCategory Of(ItemDrop.ItemData.SharedData shared)
    {
        if (Restores(shared) is { } stat) return stat;
        if (Becomes(shared) is { } made) return Restores(made) ?? CraftCategory.Other;
        return Of(shared.m_itemType);
    }

    private static CraftCategory? Restores(ItemDrop.ItemData.SharedData shared)
    {
        if (shared.m_food > 0 || shared.m_foodStamina > 0 || shared.m_foodEitr > 0)
            return shared.m_foodEitr >= shared.m_food && shared.m_foodEitr >= shared.m_foodStamina ? CraftCategory.Eitr
                : shared.m_foodStamina > shared.m_food ? CraftCategory.Stamina : CraftCategory.Health;
        if (shared.m_consumeStatusEffect is SE_Stats se)
        {
            if (se.m_healthOverTime > 0 || se.m_healthUpFront > 0 || se.m_healthRegenMultiplier > 1) return CraftCategory.Health;
            if (se.m_staminaOverTime > 0 || se.m_staminaUpFront > 0 || se.m_staminaRegenMultiplier > 1) return CraftCategory.Stamina;
            if (se.m_eitrOverTime > 0 || se.m_eitrUpFront > 0 || se.m_eitrRegenMultiplier > 1) return CraftCategory.Eitr;
        }
        return null;
    }

    // What each fermenter or oven input turns into, read from the prefabs once per world.
    private static Dictionary<string, ItemDrop.ItemData.SharedData>? products;
    private static ZNetScene? productScene;

    private static ItemDrop.ItemData.SharedData? Becomes(ItemDrop.ItemData.SharedData shared)
    {
        if (shared.m_itemType != ItemDrop.ItemData.ItemType.Material || !ZNetScene.instance) return null;
        if (products == null || productScene != ZNetScene.instance)
        {
            productScene = ZNetScene.instance;
            products = new Dictionary<string, ItemDrop.ItemData.SharedData>();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (!prefab) continue;
                if (prefab.GetComponent<Fermenter>() is { } fermenter)
                    foreach (Fermenter.ItemConversion c in fermenter.m_conversion)
                        if (c.m_from && c.m_to) products[c.m_from.m_itemData.m_shared.m_name] = c.m_to.m_itemData.m_shared;
                if (prefab.GetComponent<CookingStation>() is { } oven)
                    foreach (CookingStation.ItemConversion c in oven.m_conversion)
                        if (c.m_from && c.m_to) products[c.m_from.m_itemData.m_shared.m_name] = c.m_to.m_itemData.m_shared;
            }
        }
        return products.TryGetValue(shared.m_name, out ItemDrop.ItemData.SharedData made) ? made : null;
    }

    internal static CraftCategory Of(ItemDrop.ItemData.ItemType type) => type switch
    {
        ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon or
            ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Bow or
            ItemDrop.ItemData.ItemType.Shield or ItemDrop.ItemData.ItemType.Attach_Atgeir => CraftCategory.Weapons,
        ItemDrop.ItemData.ItemType.Helmet or ItemDrop.ItemData.ItemType.Chest or ItemDrop.ItemData.ItemType.Legs or
            ItemDrop.ItemData.ItemType.Shoulder or ItemDrop.ItemData.ItemType.Hands or ItemDrop.ItemData.ItemType.Utility or
            ItemDrop.ItemData.ItemType.Trinket => CraftCategory.Armor,
        ItemDrop.ItemData.ItemType.Ammo or ItemDrop.ItemData.ItemType.AmmoNonEquipable => CraftCategory.Ammo,
        ItemDrop.ItemData.ItemType.Tool or ItemDrop.ItemData.ItemType.Torch => CraftCategory.Tools,
        ItemDrop.ItemData.ItemType.Material => CraftCategory.Materials,
        _ => CraftCategory.Other
    };

    internal static void Toggle(CraftCategory category)
    {
        if (!Categories.Remove(category)) Categories.Add(category);
        Changed();
    }

    internal static void SetQuery(string query)
    {
        query = query.Trim();
        if (query == Query) return;
        Query = query;
        Changed();
    }

    internal static void ToggleCraftable()
    {
        CraftableOnly = !CraftableOnly;
        Changed();
    }

    // Rebuild Valheim's list (it then runs through the filter).
    internal static void Changed()
    {
        if (InventoryGui.instance && Player.m_localPlayer) UpdateCraftingPanel.Invoke(InventoryGui.instance, new object[] { false });
    }
}

// After Valheim builds the recipe list, drop what the filters hide and close the gaps.
[HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList")]
internal static class SlotRecipeListPatch
{
    private static readonly FieldInfo Available = AccessTools.Field(typeof(InventoryGui), "m_availableRecipes");
    private static readonly FieldInfo BaseSize = AccessTools.Field(typeof(InventoryGui), "m_recipeListBaseSize");
    private static readonly Type Pair = AccessTools.Inner(typeof(InventoryGui), "RecipeDataPair");
    private static readonly PropertyInfo PairRecipe = AccessTools.Property(Pair, "Recipe");
    private static readonly PropertyInfo PairCan = AccessTools.Property(Pair, "CanCraft");
    private static readonly PropertyInfo PairElement = AccessTools.Property(Pair, "InterfaceElement");

    private static void Postfix(InventoryGui __instance)
    {
        IList list = (IList)Available.GetValue(__instance);
        CraftFilter.Present.Clear();
        foreach (object pair in list) CraftFilter.Present.Add(CraftFilter.Of(((Recipe)PairRecipe.GetValue(pair)).m_item.m_itemData.m_shared));
        // A category this station has nothing in (a food filter left on at the forge) stops filtering.
        CraftFilter.Categories.RemoveWhere(c => !CraftFilter.Present.Contains(c));
        if (!CraftFilter.Active) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            object pair = list[i];
            if (CraftFilter.Matches((Recipe)PairRecipe.GetValue(pair), (bool)PairCan.GetValue(pair))) continue;
            if (PairElement.GetValue(pair) is GameObject element) UnityEngine.Object.Destroy(element);
            list.RemoveAt(i);
        }
        for (int i = 0; i < list.Count; i++)
            if (PairElement.GetValue(list[i]) is GameObject element)
                ((RectTransform)element.transform).anchoredPosition = new Vector2(0f, i * -__instance.m_recipeListSpace);
        float size = Mathf.Max((float)BaseSize.GetValue(__instance), list.Count * __instance.m_recipeListSpace);
        __instance.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
    }
}
