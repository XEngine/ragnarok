using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.HUD;

// Presentation only. Valheim keeps updating the selected piece, resource and station checks.
// Only the individual native readouts are hidden; the build picker and its input remain native.
internal sealed class BuildTaskPanel : IDisposable
{
    internal static RectTransform? Shown { get; private set; }
    internal static Vector3? MenuClearanceTop { get; private set; }
    private const float MenuTaskHeight = 200;
    private const float MinWidth = 200, MaxWidth = 448, Pad = 12, RequirementHeight = 38, Gap = 6;
    private static readonly AccessTools.FieldRef<global::Hud, Image> SnappingIcon =
        AccessTools.FieldRefAccess<global::Hud, Image>("m_snappingIcon");
    private static readonly System.Reflection.FieldInfo BuildMenu = AccessTools.Field(typeof(global::Hud), "m_buildUi");
    private static readonly System.Reflection.FieldInfo PiecePicker = AccessTools.Field(typeof(global::Hud), "m_pieceSelectionWindow");
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly Vector3[] corners = new Vector3[4];
    private readonly List<(CanvasGroup Group, bool Owned, float Alpha, bool Blocks, bool Interactable)> masks = new();
    private readonly List<Requirement> requirements = new();
    private readonly List<(Image Image, bool Enabled)> backdrops = new();
    private global::Hud? hud;
    private RectTransform? root;
    private Image? background, icon, snapping;
    private TextMeshProUGUI? context, title, description;
    private bool layoutDirty = true;

    private sealed class Requirement
    {
        internal GameObject Source = null!;
        internal Image? SourceIcon;
        internal TMP_Text? SourceName, SourceAmount;
        internal RectTransform Root = null!;
        internal Image Icon = null!;
        internal TextMeshProUGUI Name = null!, Amount = null!;
        internal float Width;
    }

    internal BuildTaskPanel(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    internal void Tick()
    {
        global::Hud current = global::Hud.instance;
        if (settings.Hud.Value != HudStyle.Ironbound || !current || !current.m_buildHud)
        {
            Dispose();
            return;
        }
        if (current != hud || !root || root!.parent != current.m_rootObject.transform)
        {
            Dispose();
            hud = current;
            Build(current);
        }
        foreach (var mask in masks) if (mask.Group) mask.Group.alpha = 0;
        foreach (var backdrop in backdrops) if (backdrop.Image) backdrop.Image.enabled = false;
        Player player = Player.m_localPlayer;
        bool show = player && !player.IsDead() && current.m_buildHud.activeInHierarchy &&
            !InventoryGui.IsVisible() && !UiState.ModalOpen && !WeaponDial.Open;
        root!.gameObject.SetActive(show);
        Shown = show ? root : null;
        if (!show) MenuClearanceTop = null;
        if (!show) return;

        layoutDirty |= title!.text != current.m_buildSelection.text;
        title.text = current.m_buildSelection.text;
        title.spriteAsset = current.m_buildSelection.spriteAsset;
        layoutDirty |= description!.text != current.m_pieceDescription.text;
        description.text = current.m_pieceDescription.text;
        description.spriteAsset = current.m_pieceDescription.spriteAsset;
        icon!.sprite = current.m_buildIcon.sprite;
        bool hasIcon = current.m_buildIcon.enabled && icon.sprite;
        layoutDirty |= icon.enabled != hasIcon;
        icon.enabled = hasIcon;
        Image nativeSnapping = SnappingIcon(current);
        snapping!.sprite = nativeSnapping.sprite;
        bool hasSnapping = nativeSnapping.enabled && snapping.sprite;
        layoutDirty |= snapping.enabled != hasSnapping;
        snapping.enabled = hasSnapping;
        string tool = ToolName(player!);
        layoutDirty |= context!.text != tool;
        context.text = tool;
        background!.color = Palette.Hex(0x0C0F11, settings.InventoryOpacity.Value);

        foreach (Requirement item in requirements)
        {
            bool active = item.Source && item.Source.activeInHierarchy;
            layoutDirty |= item.Root.gameObject.activeSelf != active;
            item.Root.gameObject.SetActive(active);
            if (!active) continue;
            item.Icon.sprite = item.SourceIcon ? item.SourceIcon!.sprite : null;
            item.Icon.enabled = item.SourceIcon && item.SourceIcon!.enabled && item.Icon.sprite;
            item.Icon.color = item.SourceIcon ? item.SourceIcon!.color : Color.white;
            string name = item.SourceName ? item.SourceName!.text : "";
            string amount = item.SourceAmount ? item.SourceAmount!.text : "";
            layoutDirty |= item.Name.text != name || item.Amount.text != amount;
            item.Name.text = name;
            item.Amount.text = amount;
            item.Amount.color = item.SourceAmount ? RequirementColor(item.SourceAmount!.color) : Palette.Bone;
            if (item.SourceName) item.Name.spriteAsset = item.SourceName!.spriteAsset;
            if (item.SourceAmount) item.Amount.spriteAsset = item.SourceAmount!.spriteAsset;
        }
        if (layoutDirty) FitContent();
        Place(root.sizeDelta.x, root.sizeDelta.y);
    }

    private static float Measure(TMP_Text text) => text.GetPreferredValues(text.text, Mathf.Infinity, Mathf.Infinity).x;

    private void FitContent()
    {
        layoutDirty = false;
        float textX = icon!.enabled ? 64 : Pad;
        float snapSpace = snapping!.enabled ? 26 : 0;
        float headerWidth = textX + Mathf.Max(Measure(title!), Measure(context!)) + Pad + snapSpace;
        bool hasDescription = !string.IsNullOrWhiteSpace(description!.text);
        description.gameObject.SetActive(hasDescription);
        if (hasDescription) headerWidth = Mathf.Max(headerWidth, textX + Mathf.Min(280, Measure(description)) + Pad);
        float used = 0, widest = 0;
        foreach (Requirement item in requirements)
        {
            if (!item.Root.gameObject.activeSelf) continue;
            item.Width = Mathf.Clamp(Mathf.Max(Measure(item.Name), Measure(item.Amount)) + 42, 84, 160);
            if (used > 0 && used + Gap + item.Width > MaxWidth - Pad * 2) used = 0;
            used += (used > 0 ? Gap : 0) + item.Width;
            widest = Mathf.Max(widest, used);
        }
        float width = Mathf.Clamp(Mathf.Max(headerWidth, widest + Pad * 2), MinWidth, MaxWidth);
        context!.At(textX, 8, width - textX - Pad - snapSpace, 13);
        title!.At(textX, 21, width - textX - Pad - snapSpace, 25);
        snapping.At(width - Pad - 18, 24, 18, 18);
        float descriptionHeight = hasDescription
            ? Mathf.Clamp(description.GetPreferredValues(description.text, width - textX - Pad, Mathf.Infinity).y, 14, 42) : 0;
        description.At(textX, 48, width - textX - Pad, descriptionHeight);
        float headerBottom = Mathf.Max(icon.enabled ? 63 : 46, hasDescription ? 48 + descriptionHeight : 46);
        float x = Pad, y = headerBottom + 8, bottom = headerBottom;
        foreach (Requirement item in requirements)
        {
            if (!item.Root.gameObject.activeSelf) continue;
            if (x > Pad && x + item.Width > width - Pad + 0.1f)
            {
                x = Pad;
                y += RequirementHeight + Gap;
            }
            item.Root.At(x, y, item.Width, RequirementHeight);
            item.Name.At(36, 3, item.Width - 42, 15);
            item.Amount.At(36, 18, item.Width - 42, 16);
            x += item.Width + Gap;
            bottom = y + RequirementHeight;
        }
        root!.sizeDelta = new Vector2(Mathf.Ceil(width), Mathf.Ceil(bottom + Pad));
    }

    private static Color RequirementColor(Color native) =>
        native.r > native.g * 1.5f && native.r > native.b * 1.5f
            ? Palette.Hex(0xF08B80, native.a) : Palette.Bone.WithAlpha(native.a);

    private static string ToolName(Player player)
    {
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            if (item.m_equipped && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool)
                return Localization.instance.Localize(item.m_shared.m_name);
        ItemDrop.ItemData? weapon = player.GetCurrentWeapon();
        return weapon != null && weapon.m_equipped ? Localization.instance.Localize(weapon.m_shared.m_name) : "";
    }

    private void Build(global::Hud owner)
    {
        root = Ugui.Node(owner.m_rootObject.transform, "Ironbound Building Task");
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0);
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        background = Ugui.Image(root, "Frame", theme.PackSurface, Palette.Hex(0x0C0F11, settings.InventoryOpacity.Value)).Stretch();
        Ugui.Image(root, "Frame edge", theme.PackEdge, Palette.Hex(0x626B64, 0.65f)).Stretch();
        icon = Ugui.Image(root, "Task icon", null, Color.white).At(Pad, 21, 42, 42);
        Ugui.ItemIcon(icon);
        context = Ugui.Text(root, "Tool", "", theme.Body, 10, Palette.Muted).At(64, 8, MaxWidth - 108, 13);
        title = Ugui.Text(root, "Task", "", theme.Heading, 20, Palette.Hex(0xF2D597)).At(64, 21, MaxWidth - 108, 25);
        title.enableAutoSizing = true;
        title.fontSizeMin = 13;
        title.fontSizeMax = 20;
        description = Ugui.Text(root, "Description", "", theme.Body, 11.5f, Palette.Bone,
            TextAlignmentOptions.TopLeft, wrap: true).At(64, 48, MaxWidth - 64 - Pad, 29);
        description.enableAutoSizing = true;
        description.fontSizeMin = 10;
        description.fontSizeMax = 11.5f;
        snapping = Ugui.Image(root, "Snapping", null, Color.white).At(MaxWidth - Pad - 18, 24, 18, 18);
        snapping.preserveAspect = true;
        Mask(owner.m_buildSelection.gameObject);
        Mask(owner.m_pieceDescription.gameObject);
        Mask(owner.m_buildIcon.gameObject);
        Mask(SnappingIcon(owner).gameObject);
        foreach (GameObject source in owner.m_requirementItems)
        {
            if (!source) continue;
            Mask(source);
            RectTransform tile = Ugui.Node(root, "Requirement");
            Ugui.Image(tile, "Fill", theme.PackPill, Palette.Hex(0x111716, 0.3f)).Stretch();
            Ugui.Image(tile, "Edge", theme.PackPillEdge, Palette.Hex(0x626B64, 0.35f)).Stretch();
            Image resourceIcon = Ugui.Image(tile, "Icon", null, Color.white).At(6, 7, 24, 24);
            Ugui.ItemIcon(resourceIcon);
            TextMeshProUGUI resourceName = Ugui.Text(tile, "Name", "", theme.Body, 10.5f, Palette.Bone).At(36, 3, 92, 15);
            resourceName.enableAutoSizing = true;
            resourceName.fontSizeMin = 8;
            resourceName.fontSizeMax = 10.5f;
            TextMeshProUGUI amount = Ugui.Text(tile, "Amount", "", theme.Strong, 12, Palette.Bone).At(36, 18, 92, 16);
            requirements.Add(new Requirement
            {
                Source = source,
                SourceIcon = source.transform.Find("res_icon")?.GetComponent<Image>(),
                SourceName = source.transform.Find("res_name")?.GetComponent<TMP_Text>(),
                SourceAmount = source.transform.Find("res_amount")?.GetComponent<TMP_Text>(),
                Root = tile, Icon = resourceIcon, Name = resourceName, Amount = amount
            });
        }
        HideNativeBackdrops(owner);
    }

    private void HideNativeBackdrops(global::Hud owner)
    {
        Transform? picker = (BuildMenu.GetValue(owner) as Component)?.transform;
        Transform? legacyPicker = (PiecePicker.GetValue(owner) as GameObject)?.transform;
        Image nativeSnapping = SnappingIcon(owner);
        foreach (Image image in owner.m_buildHud.GetComponentsInChildren<Image>(true))
        {
            // Preserve the native build picker and the source icons/colors mirrored by our card.
            if ((picker && image.transform.IsChildOf(picker!)) ||
                (legacyPicker && image.transform.IsChildOf(legacyPicker!)) ||
                image == owner.m_buildIcon || image == nativeSnapping) continue;
            bool requirement = false;
            foreach (GameObject source in owner.m_requirementItems)
                if (source && image.transform.IsChildOf(source.transform)) { requirement = true; break; }
            if (requirement) continue;
            // Disable only the decorative Image, never its object/children or any input component.
            backdrops.Add((image, image.enabled));
            image.enabled = false;
        }
    }

    private void Mask(GameObject source)
    {
        CanvasGroup fade = source.GetComponent<CanvasGroup>();
        foreach (var mask in masks) if (mask.Group == fade && fade) return;
        bool owned = !fade;
        if (owned) fade = source.AddComponent<CanvasGroup>();
        CanvasGroup group = fade!;
        masks.Add((group, owned, group.alpha, group.blocksRaycasts, group.interactable));
        group.alpha = 0;
        group.blocksRaycasts = group.interactable = false;
    }

    private void Place(float width, float height)
    {
        RectTransform parent = (RectTransform)root!.parent;
        Component? picker = BuildMenu.GetValue(hud) as Component;
        bool menuOpen = picker && picker!.gameObject.activeInHierarchy;
        float scale = Mathf.Min(settings.HudScale.Value * 0.85f, (parent.rect.width - 24) / width);
        // A fixed slot prevents hovered descriptions/cost rows from moving the picker.
        // Unusually tall cards fit inside it instead of enlarging its reserved area.
        float slotScale = Mathf.Min(settings.HudScale.Value * 0.85f, (parent.rect.width - 24) / MaxWidth);
        if (menuOpen) scale = slotScale * Mathf.Min(1, MenuTaskHeight / height);
        root.localScale = Vector3.one * scale;
        float bottom = parent.rect.yMin + 150;
        bottom = Mathf.Max(bottom, TopOf(IronboundHud.ShownVitals, parent), TopOf(IronboundActionBar.Shown, parent)) + 12;
        float x = 0;
        // Normally this card fits below the reticle. At unusual HUD positions/scales,
        // move it to the left of the center rather than covering the aiming area.
        if (!menuOpen && bottom + height * scale > parent.rect.center.y - 80)
            x = -width * scale / 2 - 96;
        x = Mathf.Clamp(x, parent.rect.xMin + width * scale / 2 + 12 - parent.rect.center.x,
            parent.rect.xMax - width * scale / 2 - 12 - parent.rect.center.x);
        bottom = Mathf.Min(bottom, parent.rect.yMax - (menuOpen ? MenuTaskHeight * slotScale : height * scale) - 12);
        root.anchoredPosition = new Vector2(x, bottom - parent.rect.yMin);
        MenuClearanceTop = menuOpen
            ? parent.TransformPoint(new Vector3(0, bottom + MenuTaskHeight * slotScale, 0))
            : (Vector3?)null;
    }

    private float TopOf(RectTransform? target, RectTransform parent)
    {
        if (!target || !target!.gameObject.activeInHierarchy) return parent.rect.yMin;
        target.GetWorldCorners(corners);
        return parent.InverseTransformPoint(corners[1]).y;
    }

    public void Dispose()
    {
        if (Shown == root) { Shown = null; MenuClearanceTop = null; }
        foreach (var backdrop in backdrops) if (backdrop.Image) backdrop.Image.enabled = backdrop.Enabled;
        backdrops.Clear();
        foreach (var mask in masks)
        {
            if (!mask.Group) continue;
            mask.Group.alpha = mask.Alpha;
            mask.Group.blocksRaycasts = mask.Blocks;
            mask.Group.interactable = mask.Interactable;
            if (mask.Owned) UnityEngine.Object.Destroy(mask.Group);
        }
        masks.Clear();
        requirements.Clear();
        if (root)
        {
            root!.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(root.gameObject);
        }
        root = null;
        hud = null;
        layoutDirty = true;
    }
}
