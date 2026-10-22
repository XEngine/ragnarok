using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.HUD;

// Presentation only. Native saddle updates keep running behind this readout.
internal sealed class MountHudLayout : IDisposable
{
    private const float Width = 280, Height = 52, BarWidth = 134;
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly Vector3[] corners = new Vector3[4];
    private readonly List<TextOutline> outlines = new();
    private RectTransform? root;
    private GameObject? native;
    private CanvasGroup? mask;
    private bool ownedMask;
    private float originalAlpha;
    private TextMeshProUGUI? name, healthValue, staminaValue;
    private Image? health, stamina;

    internal MountHudLayout(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
        Canvas.willRenderCanvases += Place;
    }

    internal void Tick() => Place();

    private void Place()
    {
        var hud = global::Hud.instance;
        Player player = Player.m_localPlayer;
        var saddle = player ? player.GetDoodadController() as Sadle : null;
        Character animal = saddle ? saddle.GetCharacter() : null!;
        if (settings.Hud.Value != HudStyle.Ironbound || !hud || !hud.m_mountPanel ||
            !player || player.IsDead() || !saddle || !animal)
        { Restore(); return; }
        if (!root || native != hud.m_mountPanel || root!.parent != hud.m_rootObject.transform)
        { Restore(); Build(hud); }
        if (mask) mask!.alpha = 0;
        bool show = hud.m_mountPanel.activeInHierarchy && !global::Hud.IsUserHidden() &&
            !InventoryGui.IsVisible() && !UiState.ModalOpen;
        root!.gameObject.SetActive(show);
        if (!show) return;
        name!.text = hud.m_mountNameText.text;
        float hp = Mathf.Max(0, animal.GetHealth()), maxHp = Mathf.Max(1, animal.GetMaxHealth());
        float energy = Mathf.Max(0, saddle.GetStamina()), maxEnergy = Mathf.Max(1, saddle.GetMaxStamina());
        healthValue!.text = Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(maxHp);
        staminaValue!.text = Mathf.CeilToInt(energy) + " / " + Mathf.CeilToInt(maxEnergy);
        health!.rectTransform.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(hp / maxHp), 4);
        stamina!.rectTransform.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(energy / maxEnergy), 4);
        foreach (TextOutline outline in outlines) outline.Sync();

        var parent = root.parent as RectTransform;
        if (!parent) return;
        float scale = Mathf.Max(0.1f, settings.HudScale.Value);
        scale *= Mathf.Min(1, Mathf.Max(1, parent!.rect.width - 48) / (Width * scale));
        root.localScale = Vector3.one * scale;
        float width = Width * scale, height = Height * scale;
        RectTransform? vitals = IronboundHud.ShownVitals;
        bool above = vitals && vitals!.gameObject.activeInHierarchy;
        Vector2 bottomLeft = Vector2.zero;
        if (above)
        {
            Rect anchor = Bounds(vitals!, parent);
            // The weapon title extends above the vitals' own rect.
            bottomLeft = new Vector2(anchor.center.x - width / 2, anchor.yMax + 44 * scale);
        }
        if (!above)
            bottomLeft = new Vector2(parent.rect.xMax - width - 24, parent.rect.center.y - height / 2);
        bottomLeft.x = Mathf.Clamp(bottomLeft.x, parent.rect.xMin + 12, Mathf.Max(parent.rect.xMin + 12, parent.rect.xMax - width - 12));
        bottomLeft.y = Mathf.Clamp(bottomLeft.y, parent.rect.yMin + 12, Mathf.Max(parent.rect.yMin + 12, parent.rect.yMax - height - 12));
        root.position = parent.TransformPoint(new Vector3(bottomLeft.x, bottomLeft.y + height, 0));
    }

    private void Build(global::Hud hud)
    {
        root = Ugui.Node(hud.m_rootObject.transform, "Ironbound Mount");
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0, 1);
        root.sizeDelta = new Vector2(Width, Height);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        name = Label("Mount name", "", theme.Strong, 15, Palette.Bone, TextAlignmentOptions.Center,
            0, 0, Width, 22);
        Label("Health label", "HEALTH", theme.Strong, 9, Palette.Muted, TextAlignmentOptions.MidlineLeft,
            0, 25, 42, 16);
        Label("Stamina label", "STAMINA", theme.Strong, 9, Palette.Muted, TextAlignmentOptions.MidlineLeft,
            146, 25, 44, 16);
        healthValue = Label("Mount health", "", theme.Numbers, 13, Palette.Bone, TextAlignmentOptions.MidlineRight,
            43, 25, 91, 16);
        staminaValue = Label("Mount stamina", "", theme.Numbers, 13, Palette.Bone, TextAlignmentOptions.MidlineRight,
            191, 25, 89, 16);
        Ugui.Image(root, "Health track", null, Palette.Health.WithAlpha(0.2f)).At(0, 44, BarWidth, 4);
        Ugui.Image(root, "Stamina track", null, Palette.Stamina.WithAlpha(0.2f)).At(146, 44, BarWidth, 4);
        health = Ugui.Image(root, "Health", null, Palette.Health).At(0, 44, BarWidth, 4);
        stamina = Ugui.Image(root, "Stamina", null, Palette.Stamina).At(146, 44, BarWidth, 4);
        native = hud.m_mountPanel;
        ownedMask = !native.TryGetComponent(out CanvasGroup existing);
        mask = ownedMask ? native.AddComponent<CanvasGroup>() : existing;
        originalAlpha = mask!.alpha;
        mask.alpha = 0;
    }

    private TextMeshProUGUI Label(string id, string text, TMP_FontAsset? font, float size, Color color,
        TextAlignmentOptions alignment, float x, float y, float width, float height)
    {
        var label = Ugui.Text(root!, id, text, font, size, color, alignment).At(x, y, width, height);
        outlines.Add(new TextOutline(label, opacity: 0.8f));
        return label;
    }

    private Rect Bounds(RectTransform target, RectTransform parent)
    {
        target.GetWorldCorners(corners);
        Vector3 min = parent.InverseTransformPoint(corners[0]), max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector3 point = parent.InverseTransformPoint(corners[i]);
            min = Vector3.Min(min, point); max = Vector3.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void Restore()
    {
        if (mask)
        {
            mask!.alpha = originalAlpha;
            if (ownedMask) UnityEngine.Object.Destroy(mask);
        }
        if (root) { root!.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        root = null; native = null; mask = null;
        name = healthValue = staminaValue = null;
        health = stamina = null;
        outlines.Clear();
    }

    public void Dispose()
    {
        Canvas.willRenderCanvases -= Place;
        Restore();
    }
}
