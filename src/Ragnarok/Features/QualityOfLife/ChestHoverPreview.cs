using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Features.QualityOfLife;

// Read the container's normally replicated inventory. Never open it, request ownership,
// transfer items, or register an interaction handler for this display.
internal sealed class ChestHoverPreview : IDisposable
{
    private static readonly MethodInfo Access = AccessTools.Method(typeof(Container), "CheckAccess");
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private RectTransform? root;
    private TMP_Text? title, summary;
    private readonly List<(RectTransform Root, Image Icon, TMP_Text Count)> cells = new();
    private bool vanilla;
    private InventoryGui? skinSource;
    private Image? slotSkin;
    private Color slotTint = Color.white;
    private TMP_Text? countSkin;
    private Container? shown;
    private float nextRefresh;
    // Keep content inside the frame's carved rails, including the footer baseline.
    private const float Cell = 36, Gap = 4;
    // The Ragnarök peek uses the minimal frame, so its content sits closer to the edge.
    private float Pad => vanilla ? 18 : 10;
    private float Header => vanilla ? 42 : 34;
    private float TitleY => vanilla ? 12 : 6;

    internal ChestHoverPreview(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    internal void Tick()
    {
        var player = Player.m_localPlayer;
        var hud = global::Hud.instance;
        if (!settings.ChestPreview.Value || !player || player.IsDead() || !hud ||
            !hud.m_rootObject.activeInHierarchy || InventoryGui.IsVisible() || UiState.WindowOpen || Menu.IsVisible())
        {
            Hide();
            return;
        }
        var hover = player.GetHoverObject();
        var container = hover ? hover.GetComponentInParent<Container>() : null;
        if (!container || container.GetComponent<TombStone>() || !container.isActiveAndEnabled)
        {
            Hide();
            return;
        }
        var view = container.GetComponent<ZNetView>();
        if (!view || !view.IsValid() || container.GetInventory() == null ||
            (container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, flash: false)) ||
            !(bool)Access.Invoke(container, new object[] { player.GetPlayerID() }))
        {
            Hide();
            return;
        }
        bool wantVanilla = settings.Hud.Value == HudStyle.Vanilla;
        if (wantVanilla && !InventoryGui.instance) { Hide(); return; }
        if (root && (root!.parent != hud.m_rootObject.transform || vanilla != wantVanilla ||
            (wantVanilla && skinSource != InventoryGui.instance))) Dispose();
        if (!root) Build(hud.m_rootObject.transform);
        root!.gameObject.SetActive(true);
        if (shown != container || Time.unscaledTime >= nextRefresh)
        {
            shown = container;
            nextRefresh = Time.unscaledTime + 0.15f;
            Refresh(container);
        }
        Place();
    }

    private void Build(Transform parent)
    {
        Dispose();
        vanilla = settings.Hud.Value == HudStyle.Vanilla;
        skinSource = InventoryGui.instance;
        root = Ugui.Node(parent, "Ragnarok Chest Hover Preview");
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        if (vanilla)
        {
            var background = skinSource!.m_player.Find("Bkg")?.GetComponent<Image>();
            NativeImage(root, "Frame", background).Stretch();
            var element = skinSource.m_playerGrid.m_elementPrefab.GetComponent<InventoryElement>();
            // The button target's raw Image is often white: Selectable supplies the idle
            // tint through its CanvasRenderer, which copying the Image alone misses.
            slotSkin = element.transform.Find("bkg")?.GetComponent<Image>() ??
                element.transform.Find("Bkg")?.GetComponent<Image>() ?? element.m_button.targetGraphic as Image;
            slotTint = slotSkin == element.m_button.targetGraphic && element.m_button.transition == Selectable.Transition.ColorTint
                ? element.m_button.colors.normalColor : Color.white;
            // Some prefab variants use an untinted highlight rather than a background.
            // A passive preview must not render that as an opaque white tile.
            Color idle = (slotSkin ? slotSkin!.color : Color.white) * slotTint;
            if (idle.grayscale > 0.65f && idle.a > 0.5f)
                slotTint *= new Color(0.22f, 0.20f, 0.17f, 0.65f);
            countSkin = element.m_amount;
            title = NativeText(root, "Chest", skinSource.m_containerName, 15);
            summary = NativeText(root, "Summary", countSkin, 11);
        }
        else
        {
            Ugui.Image(root, "Frame", theme.Minimal, Color.white).Stretch();
            title = Ugui.Text(root, "Chest", "", theme.Heading, 16, Palette.Bone);
            summary = Ugui.Text(root, "Summary", "", theme.Subtle, 12, Palette.Muted);
        }
        title.richText = false;
        shown = null;
    }

    private static Image NativeImage(Transform parent, string name, Image? source)
    {
        var image = Ugui.Image(parent, name, source ? source!.sprite : null,
            source ? source!.color : new Color(0, 0, 0, 0.7f));
        if (source)
        {
            image.type = source!.type;
            image.material = source.material;
            image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        }
        return image;
    }

    private static TMP_Text NativeText(Transform parent, string name, TMP_Text source, float size)
    {
        var text = UnityEngine.Object.Instantiate(source, parent);
        text.name = name;
        text.gameObject.SetActive(true);
        text.enabled = true;
        text.text = "";
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private void Refresh(Container container)
    {
        var inventory = container.GetInventory();
        var items = inventory.GetAllItems().OrderBy(item => item.m_gridPos.y).ThenBy(item => item.m_gridPos.x).ToList();
        int columns = Math.Min(settings.ChestPreviewColumns.Value, Math.Max(1, items.Count));
        int rows = (items.Count + columns - 1) / columns;
        float width = Math.Max(150, Pad * 2 + columns * (Cell + Gap) - Gap);
        float footer = Header + rows * (Cell + Gap) + 4;
        root!.sizeDelta = new Vector2(width, footer + 18 + (vanilla ? Pad : 4));
        title!.text = Localization.instance.Localize(container.GetHoverName());
        // Ragnarök: the name steps in past the frame's corner flourish.
        float titleX = vanilla ? Pad + 8 : Pad + 12;
        title.At(titleX, TitleY, width - titleX - Pad, 26);
        summary!.text = items.Count == 0 ? "Empty" : items.Count + " / " + (inventory.GetWidth() * inventory.GetHeight()) + " slots";
        summary.At(Pad, footer, width - Pad * 2, 18);
        while (cells.Count < items.Count)
        {
            var cell = Ugui.Node(root, "Item");
            if (vanilla)
            {
                var background = NativeImage(cell, "Slot", slotSkin).Stretch();
                background.color *= slotTint;
            }
            else Ugui.Image(cell, "Slot", theme.Field, Color.white).Stretch();
            var icon = Ugui.ItemIcon(Ugui.Image(cell, "Icon", null, Color.white)).At(2, 2, Cell - 4, Cell - 4);
            TMP_Text count = vanilla ? NativeText(cell, "Count", countSkin!, 12) :
                Ugui.Text(cell, "Count", "", theme.Strong, 12, Palette.Tan);
            count.alignment = TextAlignmentOptions.BottomRight;
            count.At(1, 16, Cell - 3, 18);
            cells.Add((cell, icon, count));
        }
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            cell.Root.gameObject.SetActive(i < items.Count);
            if (i >= items.Count) continue;
            cell.Root.At(Pad + (i % columns) * (Cell + Gap), Header + (i / columns) * (Cell + Gap), Cell, Cell);
            cell.Icon.sprite = items[i].GetIcon();
            cell.Count.text = items[i].m_stack > 1 ? items[i].m_stack.ToString() : "";
        }
    }

    private void Place()
    {
        var parent = (RectTransform)root!.parent;
        float scale = Math.Min(settings.ChestPreviewScale.Value,
            Math.Min((parent.rect.width - 16) / root.sizeDelta.x, (parent.rect.height - 16) / root.sizeDelta.y));
        scale = Math.Max(0.1f, scale);
        root.localScale = Vector3.one * scale;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0, 1);
        float x = Mathf.Clamp(settings.ChestPreviewX.Value, -parent.rect.width / 2 + 8, parent.rect.width / 2 - root.sizeDelta.x * scale - 8);
        float y = -settings.ChestPreviewY.Value;
        // Never over the Ragnarök action prompt: at most just under it (both share the left edge).
        if (HoverPrompt.Shown is { } prompt && prompt.parent == parent)
            y = Mathf.Min(y, prompt.anchoredPosition.y - prompt.sizeDelta.y * prompt.pivot.y - 6);
        y = Mathf.Clamp(y, -parent.rect.height / 2 + root.sizeDelta.y * scale + 8, parent.rect.height / 2 - 8);
        root.anchoredPosition = new Vector2(x, y);
    }

    private void Hide()
    {
        if (root) root!.gameObject.SetActive(false);
        shown = null;
    }

    public void Dispose()
    {
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        shown = null;
        cells.Clear();
        skinSource = null;
        slotSkin = null;
        countSkin = null;
    }
}
