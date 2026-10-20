using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ragnarok.Features.NearbyStorage;

namespace Ragnarok.Features.QualityOfLife;

// Native art/fonts and native crafting actions, with the same optional conveniences
// as the custom panel. All inventory counting is read-only.
internal sealed class VanillaCraftExtras : IDisposable
{
    internal static GameObject? BackedTooltip(InventoryGui gui) => gui.m_playerGrid && gui.m_playerGrid.m_elementPrefab
        ? gui.m_playerGrid.m_elementPrefab.GetComponentInChildren<UITooltip>(true)?.m_tooltipPrefab : null;

    private static readonly FieldInfo Selected = AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe");
    private static readonly Type Pair = AccessTools.Inner(typeof(InventoryGui), "RecipeDataPair");
    private static readonly PropertyInfo RecipeProperty = AccessTools.Property(Pair, "Recipe");
    private static readonly PropertyInfo ItemProperty = AccessTools.Property(Pair, "ItemData");
    private readonly InventoryGui gui;
    private readonly RectTransform craft, root;
    private readonly Vector2 position, size;
    private readonly TMP_InputField amount;
    private readonly List<Button> buttons = new();
    private readonly List<RequirementView> requirements = new();
    private Recipe? previousRecipe;
    private bool shifted;
    private float nextCounts;
    private readonly Dictionary<string, (int Bag, int Chests)> counts = new();
    private bool nearbyCounts;

    private sealed class RequirementView
    {
        internal Image Icon = null!;
        internal TMP_Text Amount = null!, Name = null!;
        internal UITooltip? Tooltip;
        internal GameObject? TooltipPrefab;
        internal RectTransform? TooltipAnchor;
        internal Vector2 TooltipPosition;
        internal string TooltipText = "", TooltipTopic = "", AmountText = "";
        internal bool Auto;
        internal float Min, Max, Size;
    }

    internal VanillaCraftExtras(InventoryGui gui)
    {
        this.gui = gui;
        craft = (RectTransform)gui.m_craftButton.transform;
        position = craft.anchoredPosition; size = craft.sizeDelta;
        root = Ugui.Node(craft.parent, "Ragnarok Vanilla Craft Amount");
        root.anchorMin = craft.anchorMin; root.anchorMax = craft.anchorMin;
        root.pivot = new Vector2(0, 0.5f);
        root.sizeDelta = new Vector2(100, craft.rect.height);
        MakeButton("−", 0, -1); MakeButton("+", 74, 1);
        Image native = (Image)gui.m_craftButton.targetGraphic;
        Image box = Ugui.Image(root, "Amount", native.sprite, Color.white, true).At(27, 0, 46, craft.rect.height);
        box.gameObject.SetActive(false);
        RectTransform area = Ugui.Node(box.transform, "Text area").Stretch(2, 0, 2, 0);
        area.gameObject.AddComponent<RectMask2D>();
        TMP_Text label = Ugui.Text(area, "Value", "1", gui.m_recipeDecription.font, 15,
            gui.m_recipeDecription.color, TextAlignmentOptions.Midline).Stretch();
        amount = box.gameObject.AddComponent<TMP_InputField>();
        amount.textViewport = area; amount.textComponent = label; amount.targetGraphic = box;
        amount.contentType = TMP_InputField.ContentType.IntegerNumber; amount.characterLimit = 2;
        amount.SetTextWithoutNotify("1");
        amount.onEndEdit.AddListener(value => SetAmount(int.TryParse(value, out int n) ? n : 1));
        UiState.TypingFields.Add(amount); box.gameObject.SetActive(true);
        root.gameObject.SetActive(false);
        GameObject? backedTooltip = BackedTooltip(gui);
        foreach (GameObject requirement in gui.m_recipeRequirementList)
        {
            if (!requirement) continue;
            TMP_Text nativeAmount = requirement.transform.Find("res_amount").GetComponent<TMP_Text>();
            Image icon = requirement.transform.Find("res_icon").GetComponent<Image>();
            TMP_Text name = requirement.transform.Find("res_name").GetComponent<TMP_Text>();
            UITooltip tip = requirement.GetComponent<UITooltip>();
            requirements.Add(new RequirementView
            {
                Icon = icon, Amount = nativeAmount, Tooltip = tip,
                Name = name,
                TooltipText = tip ? tip.m_text : "", TooltipTopic = tip ? tip.m_topic : "",
                TooltipAnchor = tip ? AccessTools.Field(typeof(UITooltip), "m_anchor").GetValue(tip) as RectTransform : null,
                TooltipPosition = tip ? (Vector2)AccessTools.Field(typeof(UITooltip), "m_fixedPosition").GetValue(tip) : Vector2.zero,
                TooltipPrefab = tip ? tip.m_tooltipPrefab : null, AmountText = nativeAmount.text,
                Auto = nativeAmount.enableAutoSizing, Min = nativeAmount.fontSizeMin, Max = nativeAmount.fontSizeMax, Size = nativeAmount.fontSize
            });
            if (tip && backedTooltip) tip.m_tooltipPrefab = backedTooltip;
            nativeAmount.enableAutoSizing = true; nativeAmount.fontSizeMin = 9;
            nativeAmount.fontSizeMax = nativeAmount.fontSize;
        }
    }

    private void MakeButton(string text, float x, int direction)
    {
        Image native = (Image)gui.m_craftButton.targetGraphic;
        Button button = Ugui.Button(root, text, native.sprite, native.sprite,
            () => SetAmount(CraftAmount.Amount + direction)).At(x, 0, 26, craft.rect.height);
        button.transition = Selectable.Transition.ColorTint; button.colors = gui.m_craftButton.colors;
        Ugui.Text(button.transform, "Label", text, gui.m_recipeDecription.font, 17,
            gui.m_recipeDecription.color, TextAlignmentOptions.Midline).Stretch();
        buttons.Add(button);
    }

    private void SetAmount(int requested)
    {
        Player player = Player.m_localPlayer;
        Recipe? recipe = RecipeProperty.GetValue(Selected.GetValue(gui)) as Recipe;
        if (!player || !recipe || gui.m_craftProgressPanel.gameObject.activeSelf) return;
        if (ItemProperty.GetValue(Selected.GetValue(gui)) != null) requested = 1;
        int low = 1, high = Mathf.Clamp(requested, 1, CraftAmount.Max);
        if (player.NoCostCheat()) low = high;
        else while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (player.HaveRequirements(recipe!, false, 1, middle)) low = middle;
            else high = middle - 1;
        }
        CraftAmount.Amount = low; CraftAmount.Apply(gui);
        amount.SetTextWithoutNotify(low.ToString());
    }

    internal void Tick()
    {
        Player player = Player.m_localPlayer;
        if (!player || !gui) return;
        object selected = Selected.GetValue(gui);
        Recipe? recipe = RecipeProperty.GetValue(selected) as Recipe;
        var upgrade = ItemProperty.GetValue(selected) as ItemDrop.ItemData;
        bool crafting = gui.m_craftProgressPanel.gameObject.activeSelf;
        if (!crafting && (recipe != previousRecipe || upgrade != null))
        { CraftAmount.Amount = 1; CraftAmount.Apply(gui); }
        if (recipe != previousRecipe) nextCounts = 0;
        previousRecipe = recipe;
        bool show = recipe && upgrade == null && !crafting && gui.m_craftButton.gameObject.activeInHierarchy;
        if (show && !shifted)
        {
            float width = craft.rect.width, height = craft.rect.height;
            root.anchoredPosition = position + new Vector2(-width * craft.pivot.x, height * (0.5f - craft.pivot.y));
            // Convert from the native button's anchor reference to the stepper's fixed anchor.
            root.position = craft.TransformPoint(new Vector3(craft.rect.xMin, craft.rect.center.y, 0));
            craft.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(60, width - 106));
            craft.anchoredPosition = position + new Vector2(106 * (1 - craft.pivot.x), 0);
            shifted = true;
        }
        else if (!show && shifted) RestoreButton();
        root.gameObject.SetActive(show);
        if (show && !amount.isFocused)
        {
            if (CraftAmount.Amount > 1 && !player.HaveRequirements(recipe!, false, 1, CraftAmount.Amount) && !player.NoCostCheat())
                SetAmount(CraftAmount.Amount);
            amount.SetTextWithoutNotify(CraftAmount.Amount.ToString());
        }
        int quality = upgrade == null ? 1 : upgrade.m_quality + 1;
        int multiplier = upgrade == null && (CraftAmount.Amount > 1 || ZInput.GetButton("AltPlace")) ? gui.m_multiCraftAmount : 1;
        bool nearby = StorageRuntime.Settings is { } storage && storage.Enabled.Value && storage.Crafting.Value && StorageRuntime.Registry != null;
        if (Time.unscaledTime >= nextCounts || nearby != nearbyCounts)
        {
            nextCounts = Time.unscaledTime + 0.2f; nearbyCounts = nearby;
            counts.Clear();
            using ResourceScope? scope = nearby ? new ResourceScope(player, StorageRuntime.Registry!, false) : null;
            if (recipe)
                foreach (Piece.Requirement requirement in recipe!.m_resources)
                {
                    if (!requirement.m_resItem) continue;
                    string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                    if (counts.ContainsKey(name)) continue;
                    int total = player.GetInventory().CountItems(name, -1, true);
                    int chests = scope?.Count(name, -1, true) ?? 0;
                    counts.Add(name, (total - chests, chests));
                }
        }
        CraftingStation station = player.GetCurrentCraftingStation();
        foreach (RequirementView view in requirements)
        {
            if (!recipe || !view.Icon.gameObject.activeInHierarchy || !view.Amount.enabled) continue;
            foreach (Piece.Requirement requirement in recipe!.m_resources)
            {
                if (!requirement.m_resItem || requirement.m_resItem.m_itemData.GetIcon() != view.Icon.sprite || requirement.GetAmount(quality) <= 0 ||
                    (station ? station.m_upgrader != requirement.m_upgraderResource : requirement.m_upgraderResource)) continue;
                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                if (Localization.instance.Localize(name) != view.Name.text || !counts.TryGetValue(name, out var stock)) continue;
                int bag = stock.Bag, chests = stock.Chests, total = bag + chests, need = requirement.GetAmount(quality) * multiplier;
                view.Amount.text = total + " / " + need;
                if (view.Tooltip) view.Tooltip!.Set(Localization.instance.Localize(name),
                    "In your bag: " + bag + "\nNearby chests: " + chests +
                    "\nTotal available: " + total + "\nRequired: " + need,
                    view.TooltipAnchor, view.TooltipPosition);
                break;
            }
        }
    }

    private void RestoreButton()
    { if (craft) { craft.anchoredPosition = position; craft.sizeDelta = size; } shifted = false; }

    public void Dispose()
    {
        RestoreButton();
        if (amount) { amount.onEndEdit.RemoveAllListeners(); UiState.TypingFields.Remove(amount); }
        foreach (Button button in buttons) if (button) button.onClick.RemoveAllListeners();
        if (root) { root.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        foreach (RequirementView view in requirements)
        {
            if (view.Amount)
            {
                view.Amount.enableAutoSizing = view.Auto; view.Amount.fontSizeMin = view.Min; view.Amount.fontSizeMax = view.Max;
                view.Amount.fontSize = view.Size;
                view.Amount.text = view.AmountText;
            }
            if (view.Tooltip)
            {
                if (AccessTools.Field(typeof(UITooltip), "m_current").GetValue(null) as UITooltip == view.Tooltip)
                    UITooltip.HideTooltip();
                view.Tooltip!.m_text = view.TooltipText;
                view.Tooltip.m_topic = view.TooltipTopic;
                view.Tooltip.m_tooltipPrefab = view.TooltipPrefab;
            }
        }
        requirements.Clear();
        CraftAmount.Reset();
    }
}
