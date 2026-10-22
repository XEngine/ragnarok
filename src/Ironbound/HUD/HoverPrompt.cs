using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Ironbound.HUD;

// Native hover content with themed keycaps and quiet, regular action labels.
// The native hover source remains active for mod hooks, bindings and glyphs.
internal sealed class HoverPrompt : IDisposable
{
    // The prompt while it is on screen, for the chest peek to sit under; else null.
    internal static RectTransform? Shown;

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private RectTransform? root;
    private readonly List<LineView> rows = new();
    private TextMeshProUGUI? source;
    private CanvasGroup? fade;
    private bool addedFade;
    private float vanillaAlpha;
    private UnityEngine.UI.Image? crosshair;
    private Sprite? vanillaCrosshair;
    private string last = "";
    private TMP_SpriteAsset? lastSprites;
    private readonly string key = "<color=#" + ColorUtility.ToHtmlStringRGB(Palette.Key) + ">";
    private readonly string muted = "<color=#" + ColorUtility.ToHtmlStringRGBA(Palette.Muted) + ">";

    private sealed class LineView
    {
        internal RectTransform Root = null!;
        internal TextMeshProUGUI Label = null!;
        internal string Signature = "";
        internal float LabelX, Height;
        internal readonly List<TextOutline> Outlines = new();
        internal readonly List<TextMeshProUGUI> Glyphs = new();
    }

    internal HoverPrompt(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    internal void Tick()
    {
        global::Hud hud = global::Hud.instance;
        if (settings.Hud.Value != HudStyle.Ironbound || !hud || !hud.m_hoverName)
        {
            Dispose();
            return;
        }
        if (source != hud.m_hoverName)
        {
            Dispose();
            source = hud.m_hoverName;
        }
        HideVanilla();
        Reticle(hud);
        if (!root || root!.parent != hud.m_rootObject.transform) Build(hud.m_rootObject.transform);

        string value = source!.text ?? "";
        bool show = value.Length > 0 && source.gameObject.activeInHierarchy && !InventoryGui.IsVisible() && !WeaponDial.Open;
        if (root!.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        Shown = show ? root : null;
        if (!show) return;
        if (value != last || lastSprites != source.spriteAsset)
        {
            last = value;
            lastSprites = source.spriteAsset;
            Render(value);
        }
        Place();
    }

    private void Render(string value)
    {
        List<HoverPromptLines.Line> lines = HoverPromptLines.Parse(value.Replace("<color=yellow>", key).Replace("<color=\"yellow\">", key));
        float y = 0, width = 0;
        float maximum = Mathf.Min(420, Mathf.Max(80, ((RectTransform)root!.parent).rect.width - 24));
        for (int i = 0; i < lines.Count; i++)
        {
            HoverPromptLines.Line data = lines[i];
            if (rows.Count == i) rows.Add(BuildLine(data));
            else if (rows[i].Signature != data.Signature)
            {
                rows[i].Root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(rows[i].Root.gameObject);
                rows[i] = BuildLine(data);
            }
            LineView row = rows[i];
            row.Label.text = data.Quiet ? muted + data.Text + "</color>" : data.Text;
            row.Label.spriteAsset = source!.spriteAsset;
            foreach (TextMeshProUGUI glyph in row.Glyphs) glyph.spriteAsset = source.spriteAsset;
            float available = Mathf.Max(1, maximum - row.LabelX);
            row.Label.textWrappingMode = TextWrappingModes.NoWrap;
            float labelWidth = Mathf.Min(available, Mathf.Ceil(row.Label.GetPreferredValues().x) + 4);
            float height = row.Height;
            if (data.Keys.Length == 0 && !data.Heading)
            {
                row.Label.textWrappingMode = TextWrappingModes.Normal;
                height = Mathf.Max(height, Mathf.Ceil(row.Label.GetPreferredValues(row.Label.text, available, Mathf.Infinity).y) + 2);
            }
            row.Label.rectTransform.sizeDelta = new Vector2(Mathf.Max(1, labelWidth), height);
            row.Root.At(0, y, row.LabelX + labelWidth, height);
            foreach (TextOutline outline in row.Outlines) outline.Sync();
            width = Mathf.Max(width, row.LabelX + labelWidth);
            y += height + 2;
        }
        while (rows.Count > lines.Count)
        {
            LineView row = rows[rows.Count - 1];
            row.Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(row.Root.gameObject); rows.RemoveAt(rows.Count - 1);
        }
        root.sizeDelta = new Vector2(width + 4, y + 2);
    }

    private LineView BuildLine(HoverPromptLines.Line data)
    {
        var row = new LineView { Root = Ugui.Node(root!, "Hover line"), Signature = data.Signature,
            Height = data.Keys.Length > 0 ? 22 : data.Heading ? 22 : 18 };
        float x = 0;
        if (data.Hold)
        {
            var hold = Ugui.Text(row.Root, "Hold", "Hold", theme.Body, 11, Palette.Muted);
            float width = Mathf.Ceil(hold.GetPreferredValues().x) + 2;
            hold.At(x, 0, width, row.Height); row.Outlines.Add(new TextOutline(hold)); x += width + 4;
        }
        foreach (string keyText in data.Keys)
        {
            bool joiner = keyText is "+" or "/";
            RectTransform keyRoot = joiner ? row.Root : Ugui.Node(row.Root, "Keycap");
            if (!joiner)
            {
                // Match the Forsaken Power chip's translucent face and soft edge.
                Ugui.Image(keyRoot, "Face", theme.HudKeySurface, Palette.HudGlass).Stretch();
                Ugui.Image(keyRoot, "Edge", theme.HudKeyEdge, Palette.Bone.WithAlpha(0.45f)).Stretch();
            }
            var keyLabel = Ugui.Text(keyRoot, "Key", keyText, theme.Strong, 11, joiner ? Palette.Muted : Palette.Bone.WithAlpha(0.9f),
                TextAlignmentOptions.Center);
            keyLabel.spriteAsset = source!.spriteAsset;
            row.Glyphs.Add(keyLabel);
            float width = joiner ? 8 : Mathf.Clamp(Mathf.Ceil(keyLabel.GetPreferredValues().x) + 12, 22, 160);
            if (joiner) keyLabel.At(x, 0, width, row.Height);
            else { keyRoot.At(x, 1, width, 20); keyLabel.Stretch(5, 0, 5, 0); }
            row.Outlines.Add(new TextOutline(keyLabel, opacity: 0.65f));
            x += width + 4;
        }
        row.LabelX = x + (data.Keys.Length > 0 ? 3 : 0);
        row.Label = Ugui.Text(row.Root, "Label", "", data.Heading ? theme.Strong : theme.Body,
            data.Heading ? 15 : 12, Palette.Bone).At(row.LabelX, 0, 500, row.Height);
        row.Outlines.Add(new TextOutline(row.Label, opacity: 0.9f));
        return row;
    }

    private void Build(Transform parent)
    {
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        rows.Clear();
        root = Ugui.Node(parent, "Ironbound Hover Prompt");
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        last = ""; lastSprites = null;
    }

    // Left edge on the chest peek's, its first line level with the crosshair like Valheim's own prompt.
    private void Place()
    {
        RectTransform parent = (RectTransform)root!.parent;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0, 1);
        root.localScale = Vector3.one;
        float x = Mathf.Clamp(settings.ChestPreviewX.Value, -parent.rect.width / 2 + 8, parent.rect.width / 2 - root.sizeDelta.x - 8);
        root.anchoredPosition = new Vector2(x, 12);
    }

    // The guide's reticle in place of Valheim's dot; Valheim still colours it (yellow over something usable).
    private void Reticle(global::Hud hud)
    {
        if (crosshair == hud.m_crosshair) return;
        RestoreReticle();
        crosshair = hud.m_crosshair;
        if (!crosshair) return;
        vanillaCrosshair = crosshair!.sprite;
        crosshair.sprite = theme.Reticle;
    }

    private void RestoreReticle()
    {
        if (crosshair && crosshair!.sprite == theme.Reticle) crosshair.sprite = vanillaCrosshair;
        crosshair = null;
        vanillaCrosshair = null;
    }

    private void HideVanilla()
    {
        if (fade)
        {
            fade!.alpha = 0;
            return;
        }
        addedFade = !source!.TryGetComponent(out CanvasGroup existing);
        fade = addedFade ? source.gameObject.AddComponent<CanvasGroup>() : existing;
        vanillaAlpha = fade!.alpha;
        fade.alpha = 0;
    }

    public void Dispose()
    {
        if (fade)
        {
            fade!.alpha = vanillaAlpha;
            if (addedFade) UnityEngine.Object.Destroy(fade);
        }
        fade = null;
        source = null;
        RestoreReticle();
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        rows.Clear();
        lastSprites = null;
        Shown = null;
    }
}
