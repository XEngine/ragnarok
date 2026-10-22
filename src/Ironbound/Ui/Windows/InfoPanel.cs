using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.UI.Windows;

// Owned character shortcuts over the live native info panel. Each rounded card
// forwards to its original button/toggle, including other mods' shortcuts.
internal sealed class InfoPanel : IDisposable
{
    private const float Pad = 12, Header = 46, Gap = 8;

    // Valheim's button names; anything else (other mods' buttons) is named by its tooltip.
    private static readonly Dictionary<string, string> Names = new()
    {
        ["Texts"] = "Compendium", ["Skills"] = "Skills", ["Trophies"] = "Trophies",
        ["Achievements"] = "Achievements", ["PVP"] = "PvP"
    };

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly Func<bool> wanted;
    private readonly MonochromeIcons icons = new();
    private readonly List<(UnityEngine.Object Owner, Action Restore)> undo = new();
    private readonly List<(Selectable Source, Image Edge, Image Icon, TMP_Text Label, Button Button)> buttons = new();
    private InventoryGui? gui;
    private RectTransform? root;
    private Image? background;
    private TextMeshProUGUI? title;
    private Vector2 builtSize;
    private int builtCount;

    internal InfoPanel(InterfaceSettings settings, Theme theme, Func<bool> wanted)
    {
        this.settings = settings;
        this.theme = theme;
        this.wanted = wanted;
    }

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            Restore();
            gui = current;
        }
        if (!gui) return;
        if (!wanted())
        {
            Restore();
            return;
        }
        if (!InventoryGui.IsVisible() || !Player.m_localPlayer || !gui!.m_infoPanel) return;
        RectTransform panel = (RectTransform)gui.m_infoPanel;
        if (!panel.gameObject.activeInHierarchy) return;
        List<Selectable> sources = Sources(panel);
        if (!root || panel.rect.size != builtSize || sources.Count != builtCount) Build(panel, sources);
        Refresh();
    }

    public void Dispose()
    {
        Restore();
        icons.Dispose();
    }

    private void Restore()
    {
        for (int i = undo.Count - 1; i >= 0; i--)
            if (undo[i].Owner) undo[i].Restore();
        undo.Clear();
        foreach (var entry in buttons)
            if (entry.Button) entry.Button.onClick.RemoveAllListeners();
        if (root)
        {
            root!.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(root.gameObject);
        }
        root = null;
        buttons.Clear();
    }

    // Valheim's buttons and toggles on the board, left to right.
    private static List<Selectable> Sources(RectTransform panel)
    {
        Transform? ours = panel.Find("Ironbound Info");
        return panel.GetComponentsInChildren<Selectable>(false)
            .Where(s => !ours || !s.transform.IsChildOf(ours!))
            .OrderBy(s => s.transform.position.x)
            .ToList();
    }

    private void Build(RectTransform panel, List<Selectable> sources)
    {
        Restore();
        HideVanilla(panel);
        builtSize = panel.rect.size;
        builtCount = sources.Count;
        float width = builtSize.x, height = builtSize.y;

        root = Ugui.Node(panel, "Ironbound Info");
        root.Stretch();
        background = Ugui.Image(root, "Background", theme.PackSurface,
            Palette.Hex(0x0C0F11, settings.InventoryOpacity.Value), raycast: true).Stretch();
        Ugui.Image(root, "Frame", theme.PackEdge, Palette.Hex(0x3D4541)).Stretch();
        title = Ugui.Text(root, "Name", "", theme.Heading, 22, Palette.Bone)
            .At(Pad, 8, width - Pad * 2, 30);
        title.richText = false;
        title.enableAutoSizing = true; title.fontSizeMin = 13; title.fontSizeMax = 22;
        Ugui.Image(root, "Divider", theme.White, Palette.Hex(0x303631))
            .At(Pad, Header, width - Pad * 2, 1);

        int count = sources.Count;
        if (count == 0) return;
        float top = Header + 10;
        float cardHeight = Mathf.Max(1, height - top - Pad);
        float cardWidth = Mathf.Max(1, (width - Pad * 2 - Gap * (count - 1)) / count);
        float iconSize = Mathf.Max(1, Mathf.Min(28, cardWidth - 16, cardHeight * 0.4f));
        for (int i = 0; i < count; i++)
        {
            Selectable source = sources[i];
            Button button = Ugui.Button(root, source.name, theme.PackPill, theme.PackPill, () => Press(source));
            button.At(Pad + (cardWidth + Gap) * i, top, cardWidth, cardHeight);
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Palette.Hex(0x141A1B);
            colors.highlightedColor = colors.selectedColor = Palette.Hex(0x29291F);
            colors.pressedColor = Palette.Hex(0x493D25);
            colors.disabledColor = Palette.Hex(0x101314);
            colors.colorMultiplier = 1; colors.fadeDuration = 0.08f;
            button.colors = colors;
            Image edge = Ugui.Image(button.transform, "Edge", theme.PackPillEdge, Palette.Hex(0x303631)).Stretch();
            float contentTop = Mathf.Max(4, (cardHeight - iconSize - 24) / 2);
            TMP_Text label = Ugui.Text(button.transform, "Label", Label(source), theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Midline)
                .At(4, contentTop + iconSize + 6, cardWidth - 8, 18);
            label.enableAutoSizing = true; label.fontSizeMin = 9; label.fontSizeMax = 12;
            Sprite? glyph = Glyph(source);
            Image icon = Ugui.Image(button.transform, "Icon", icons.Get(glyph) ?? theme.Diamond, Palette.Bone)
                .At((cardWidth - iconSize) / 2, contentTop, iconSize, iconSize);
            icon.preserveAspect = true;
            if (source.GetComponent<UITooltip>() is { } original)
            {
                UITooltip tip = button.gameObject.AddComponent<UITooltip>();
                tip.m_tooltipPrefab = original.m_tooltipPrefab;
                tip.m_text = original.m_text;
                tip.m_topic = original.m_topic;
            }
            buttons.Add((source, edge, icon, label, button));
        }
    }

    private static readonly string[] Decoration = { "glow", "shadow", "highlight", "selected", "bkg", "background", "frame", "circle", "border" };

    // The button's picture. Valheim's buttons also carry glows and backplates, so: an image named like
    // an icon, else the topmost image that is not decoration, else the button's own image.
    private static Sprite? Glyph(Selectable source)
    {
        List<Image> images = source.GetComponentsInChildren<Image>(false).Where(i => i.sprite).ToList();
        bool Decor(Image i) => Decoration.Any(d => i.name.ToLowerInvariant().Contains(d) || i.sprite.name.ToLowerInvariant().Contains(d));
        Image? icon = images.FirstOrDefault(i => i.name.ToLowerInvariant().Contains("icon") && !Decor(i))
                      ?? images.LastOrDefault(i => i != source.targetGraphic && !Decor(i))
                      ?? images.LastOrDefault(i => !Decor(i));
        return icon ? icon!.sprite : null;
    }

    private static string Label(Selectable source)
    {
        if (Names.TryGetValue(source.name, out string name)) return name;
        string tip = source.GetComponent<UITooltip>()?.m_text ?? "";
        return tip.Length > 0 && Localization.instance != null ? Localization.instance.Localize(tip) : ConfigControls.Pretty(source.name);
    }

    private static void Press(Selectable source)
    {
        if (!source || !source.IsInteractable()) return;
        if (source is Toggle toggle) toggle.isOn = !toggle.isOn;
        else if (source is Button button) button.onClick.Invoke();
    }

    private void Refresh()
    {
        background!.color = Palette.Hex(0x0C0F11, settings.InventoryOpacity.Value);
        string name = Game.instance ? Game.instance.GetPlayerProfile().GetName() : "";
        if (title!.text != name) title.text = name;
        foreach (var (source, edge, icon, label, button) in buttons)
        {
            if (!source) continue;
            bool usable = source.IsInteractable();
            bool on = source is Toggle { isOn: true };
            button.interactable = usable;
            edge.color = on ? Palette.Gold : Palette.Hex(0x303631);
            label.color = on ? Palette.Gold : usable ? Palette.Bone : Palette.Dim;
            icon.color = on ? Palette.Gold : usable ? Palette.Bone : Palette.Dim;
        }
    }

    private void HideVanilla(RectTransform panel)
    {
        if (panel.TryGetComponent(out Image image) && image.enabled)
        {
            image.enabled = false;
            undo.Add((image, () => image.enabled = true));
        }
        foreach (Transform child in panel)
        {
            if (child.name == "Ironbound Info") continue;
            bool added = !child.TryGetComponent(out CanvasGroup group);
            if (added) group = child.gameObject.AddComponent<CanvasGroup>();
            float alpha = group.alpha;
            bool blocks = group.blocksRaycasts;
            group.alpha = 0;
            // Keep native Selectable.IsInteractable checks intact.
            group.blocksRaycasts = false;
            undo.Add((group, () =>
            {
                group.alpha = alpha;
                group.blocksRaycasts = blocks;
                if (added) UnityEngine.Object.Destroy(group);
            }));
        }
    }
}
