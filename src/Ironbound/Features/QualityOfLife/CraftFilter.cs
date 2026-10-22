using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Ironbound.Features.QualityOfLife;

// The crafting search and category filters, shared by the Ironbound crafting panel and the vanilla one.
// Valheim's own recipe list is filtered after it is built (SlotRecipeListPatch), so selection, keys and
// gamepad all work on what is shown.
internal static class CraftFilter
{
    internal static string Query = "";
    internal static readonly HashSet<CraftCategory> Categories = new();
    internal static bool CraftableOnly;
    internal static bool CraftableFirst;
    internal static CraftArmorSlot ArmorSlot;
    internal static int ContextVersion;
    internal static InventoryGui? LastListOwner;
    internal static bool LastListSorted;
    internal static int ListVersion;
    internal static readonly HashSet<CraftArmorSlot> PresentSlots = new();
    private static string[] terms = Array.Empty<string>();
    private static readonly List<string> ingredients = new();
    private static string? station;
    private static ZNetScene? scene;

    // The categories of every recipe this station offers (known ones, craftable or not), before the
    // filters: only these get a chip. Set each time Valheim builds its list.
    internal static readonly HashSet<CraftCategory> Present = new();

    // On while either crafting panel shows the filters.
    internal static Func<bool> Enabled = () => false;
    internal static Func<bool> SortEnabled = () => false;

    private static readonly MethodInfo UpdateCraftingPanel = AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel");

    internal static bool Active => Enabled() && (Query.Length > 0 || Categories.Count > 0 || CraftableOnly || ArmorSlot != CraftArmorSlot.All);

    internal static bool Matches(Recipe recipe, bool canCraft, int quality)
    {
        ItemDrop.ItemData.SharedData shared = recipe.m_item.m_itemData.m_shared;
        if (CraftableOnly && !canCraft) return false;
        if (Categories.Count > 0 && !Categories.Contains(Of(shared))) return false;
        if (ArmorSlot != CraftArmorSlot.All && Slot(shared.m_itemType) != ArmorSlot) return false;
        if (terms.Length == 0) return true;
        ingredients.Clear();
        Player player = Player.m_localPlayer;
        CraftingStation station = player ? player.GetCurrentCraftingStation() : null!;
        foreach (Piece.Requirement resource in recipe.m_resources)
            if (resource.m_resItem && resource.GetAmount(quality) > 0 &&
                resource.m_upgraderResource == (station && station.m_upgrader) &&
                (!recipe.m_requireOnlyOneIngredient || !player || player.IsKnownMaterial(resource.m_resItem.m_itemData.m_shared.m_name)))
                ingredients.Add(Localization.instance.Localize(resource.m_resItem.m_itemData.m_shared.m_name));
        return CraftSearch.Matches(terms, Localization.instance.Localize(shared.m_name), ingredients);
    }

    // Foods use their dominant resource, including what dough becomes after baking.
    // Meads/bases get their own category, including resistance meads.
    internal static CraftCategory Of(ItemDrop.ItemData.SharedData shared)
    {
        if (CraftSearch.Mead(shared.m_food, shared.m_foodStamina, shared.m_foodEitr, shared.m_consumeStatusEffect))
            return CraftCategory.Meads;
        if (Restores(shared) is { } stat) return stat;
        if (Becomes(shared) is { } made)
            return CraftSearch.Mead(made.m_food, made.m_foodStamina, made.m_foodEitr, made.m_consumeStatusEffect)
                ? CraftCategory.Meads : Restores(made) ?? CraftCategory.Other;
        return Of(shared.m_itemType);
    }

    private static CraftCategory? Restores(ItemDrop.ItemData.SharedData shared)
    {
        if (shared.m_food > 0 || shared.m_foodStamina > 0 || shared.m_foodEitr > 0)
            return CraftSearch.Food(shared.m_food, shared.m_foodStamina, shared.m_foodEitr);
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
            ItemDrop.ItemData.ItemType.Attach_Atgeir => CraftCategory.Weapons,
        ItemDrop.ItemData.ItemType.Shield => CraftCategory.Shields,
        ItemDrop.ItemData.ItemType.Helmet or ItemDrop.ItemData.ItemType.Chest or ItemDrop.ItemData.ItemType.Legs or
            ItemDrop.ItemData.ItemType.Shoulder or ItemDrop.ItemData.ItemType.Hands or ItemDrop.ItemData.ItemType.Utility or
            ItemDrop.ItemData.ItemType.Trinket => CraftCategory.Armor,
        ItemDrop.ItemData.ItemType.Ammo or ItemDrop.ItemData.ItemType.AmmoNonEquipable => CraftCategory.Ammo,
        ItemDrop.ItemData.ItemType.Tool or ItemDrop.ItemData.ItemType.Torch => CraftCategory.Tools,
        ItemDrop.ItemData.ItemType.Material => CraftCategory.Materials,
        _ => CraftCategory.Other
    };

    internal static CraftArmorSlot Slot(ItemDrop.ItemData.ItemType type) => type switch
    {
        ItemDrop.ItemData.ItemType.Helmet => CraftArmorSlot.Head,
        ItemDrop.ItemData.ItemType.Chest => CraftArmorSlot.Chest,
        ItemDrop.ItemData.ItemType.Legs => CraftArmorSlot.Legs,
        ItemDrop.ItemData.ItemType.Shoulder => CraftArmorSlot.Cape,
        ItemDrop.ItemData.ItemType.Utility or ItemDrop.ItemData.ItemType.Trinket => CraftArmorSlot.Utility,
        _ => CraftArmorSlot.Other
    };

    internal static void Select(CraftCategory? category)
    {
        Categories.Clear();
        if (category is { } selected) Categories.Add(selected);
        ArmorSlot = CraftArmorSlot.All;
        Changed();
    }

    internal static void SelectSlot(CraftArmorSlot slot) { ArmorSlot = slot; Changed(); }

    // Never carry a food query/category into an unrelated station (or another world).
    internal static void PrepareStation(Player player)
    {
        string current = player.GetCurrentCraftingStation() ? player.GetCurrentCraftingStation().m_name : "__hand__";
        if (station == current && scene == ZNetScene.instance) return;
        station = current;
        scene = ZNetScene.instance;
        ContextVersion++;
        Query = "";
        terms = Array.Empty<string>();
        Categories.Clear();
        ArmorSlot = CraftArmorSlot.All;
        CraftableOnly = false;
    }

    internal static void Reset()
    {
        LastListOwner = null;
        LastListSorted = false;
        ContextVersion++;
        station = null;
        scene = productScene = null;
        products = null;
        Query = "";
        terms = Array.Empty<string>();
        ingredients.Clear();
        Categories.Clear();
        Present.Clear();
        PresentSlots.Clear();
        ArmorSlot = CraftArmorSlot.All;
        CraftableOnly = CraftableFirst = false;
    }

    internal static void Toggle(CraftCategory category)
    {
        if (!Categories.Remove(category)) Categories.Add(category);
        if (!Categories.Contains(CraftCategory.Armor)) ArmorSlot = CraftArmorSlot.All;
        Changed();
    }

    internal static void SetQuery(string query)
    {
        query = query.Trim();
        if (query == Query) return;
        Query = query;
        terms = CraftSearch.Terms(query);
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
    private static readonly PropertyInfo PairItem = AccessTools.Property(Pair, "ItemData");
    private static readonly PropertyInfo PairElement = AccessTools.Property(Pair, "InterfaceElement");

    private static void Postfix(InventoryGui __instance)
    {
        unchecked { CraftFilter.ListVersion++; }
        CraftFilter.LastListOwner = __instance;
        CraftFilter.LastListSorted = CraftFilter.Enabled() && CraftFilter.SortEnabled();
        IList list = (IList)Available.GetValue(__instance);
        if (Player.m_localPlayer) CraftFilter.PrepareStation(Player.m_localPlayer);
        CraftFilter.Present.Clear();
        CraftFilter.PresentSlots.Clear();
        foreach (object pair in list)
        {
            var shared = ((Recipe)PairRecipe.GetValue(pair)).m_item.m_itemData.m_shared;
            CraftCategory category = CraftFilter.Of(shared);
            CraftFilter.Present.Add(category);
            if (category == CraftCategory.Armor) CraftFilter.PresentSlots.Add(CraftFilter.Slot(shared.m_itemType));
        }
        // A category this station has nothing in (a food filter left on at the forge) stops filtering.
        CraftFilter.Categories.RemoveWhere(c => !CraftFilter.Present.Contains(c));
        if (!CraftFilter.Categories.Contains(CraftCategory.Armor) ||
            (CraftFilter.ArmorSlot != CraftArmorSlot.All && !CraftFilter.PresentSlots.Contains(CraftFilter.ArmorSlot)))
            CraftFilter.ArmorSlot = CraftArmorSlot.All;
        if (!CraftFilter.Enabled()) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            object pair = list[i];
            var item = PairItem.GetValue(pair) as ItemDrop.ItemData;
            if (!CraftFilter.Active || CraftFilter.Matches((Recipe)PairRecipe.GetValue(pair), (bool)PairCan.GetValue(pair), item == null ? 1 : item.m_quality + 1)) continue;
            if (PairElement.GetValue(pair) is GameObject element) UnityEngine.Object.Destroy(element);
            list.RemoveAt(i);
        }
        // Reorder the actual native pairs, not just our rows, keeping selection/gamepad indices aligned.
        if (CraftFilter.SortEnabled())
        {
            List<(object Pair, int Index, string Name, bool Can)> ordered = new();
            for (int i = 0; i < list.Count; i++)
            {
                object pair = list[i];
                string name = Localization.instance.Localize(((Recipe)PairRecipe.GetValue(pair)).m_item.m_itemData.m_shared.m_name);
                ordered.Add((pair, i, name, (bool)PairCan.GetValue(pair)));
            }
            ordered.Sort((a, b) =>
            {
                int ready = CraftFilter.CraftableFirst ? b.Can.CompareTo(a.Can) : 0;
                if (ready != 0) return ready;
                int name = StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
                return name != 0 ? name : a.Index.CompareTo(b.Index);
            });
            list.Clear();
            foreach (var entry in ordered) list.Add(entry.Pair);
        }
        for (int i = 0; i < list.Count; i++)
            if (PairElement.GetValue(list[i]) is GameObject element)
                ((RectTransform)element.transform).anchoredPosition = new Vector2(0f, i * -__instance.m_recipeListSpace);
        float size = Mathf.Max((float)BaseSize.GetValue(__instance), list.Count * __instance.m_recipeListSpace);
        __instance.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
    }
}
