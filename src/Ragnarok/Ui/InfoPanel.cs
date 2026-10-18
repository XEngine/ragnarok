using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Ui;

// The Ragnarök info panel: the wooden board above the inventory (character name, and the buttons for the
// Compendium, skills, trophies, achievements and PvP) redrawn as a Ragnarök plate. Valheim's board keeps
// running unseen; each of its buttons gets a chamfered twin with the same glyph and tooltip that presses
// the original (toggles flip it), so whatever buttons Valheim or other mods put there keep working.
internal sealed class InfoPanel : IDisposable
{
    private const float Pad = 16, Header = 46, ButtonMax = 60, LabelHeight = 20;

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
    private readonly List<(Selectable Source, Image Chip, Image Icon, Button Button)> buttons = new();
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
            undo.Clear();
            root = null;
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
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        buttons.Clear();
    }

    // Valheim's buttons and toggles on the board, left to right.
    private static List<Selectable> Sources(RectTransform panel)
    {
        Transform? ours = panel.Find("Ragnarok Info");
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

        root = Ugui.Node(panel, "Ragnarok Info");
        root.Stretch();
        background = Ugui.Image(root, "Frame", theme.Window, Color.white, raycast: true).Stretch();

        // The character's name as the window title (Alegreya, 24) over the guide's divider.
        title = Ugui.Text(root, "Name", "", theme.Heading, 24, Palette.Gold, TextAlignmentOptions.Center).At(Pad, 6, width - Pad * 2, 34);
        Ornaments.Rule(root, theme, Pad, Header, width - Pad * 2);

        // The buttons, evenly spread in the space under the rule, each named beneath.
        int count = sources.Count;
        if (count == 0) return;
        float top = Header + 12, area = height - top - Pad - LabelHeight;
        float size = Mathf.Floor(Mathf.Min(ButtonMax, area, (width - Pad * 2) / count - 10));
        float step = (width - Pad * 2) / count;
        for (int i = 0; i < count; i++)
        {
            Selectable source = sources[i];
            Button button = Ugui.Button(root, source.name, theme.Button, theme.ButtonHover, () => Press(source));
            Image chip = (Image)button.targetGraphic;
            float y = top + (area - size) / 2;
            chip.At(Pad + step * i + (step - size) / 2, y, size, size);
            Ugui.Text(root, "Label", Label(source), theme.Strong, 12, Palette.Muted, TextAlignmentOptions.Center)
                .At(Pad + step * i, y + size + 3, step, LabelHeight - 2);
            Sprite? glyph = Glyph(source);
            Image icon = Ugui.Image(chip.transform, "Icon", icons.Get(glyph) ?? theme.Diamond, Palette.Gold)
                .Stretch(size * 0.18f, size * 0.18f, size * 0.18f, size * 0.18f);
            icon.preserveAspect = true;
            if (source.GetComponent<UITooltip>() is { } original)
            {
                UITooltip tip = chip.gameObject.AddComponent<UITooltip>();
                tip.m_tooltipPrefab = original.m_tooltipPrefab;
                tip.m_text = original.m_text;
                tip.m_topic = original.m_topic;
            }
            buttons.Add((source, chip, icon, button));
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
        background!.color = Color.white.WithAlpha(settings.InventoryOpacity.Value);
        string name = Game.instance ? Game.instance.GetPlayerProfile().GetName() : "";
        if (title!.text != name) title.text = name;
        foreach (var (source, chip, icon, button) in buttons)
        {
            if (!source) continue;
            bool usable = source.IsInteractable();
            bool on = source is Toggle { isOn: true };
            Sprite normal = on ? theme.SegmentOn : theme.Button, hover = on ? theme.SegmentOnHover : theme.ButtonHover;
            // Swap only on a change: ChipSkin may have replaced the sprite with its sized version.
            if (button.spriteState.highlightedSprite != hover) Ugui.SpriteSwap(button, normal, hover);
            button.interactable = usable;
            icon.color = on ? Palette.GoldBright : usable ? Palette.Gold : Palette.Gold.WithAlpha(0.35f);
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
            if (child.name == "Ragnarok Info") continue;
            bool added = !child.TryGetComponent(out CanvasGroup group);
            if (added) group = child.gameObject.AddComponent<CanvasGroup>();
            float alpha = group.alpha;
            bool blocks = group.blocksRaycasts, interactable = group.interactable;
            group.alpha = 0;
            group.blocksRaycasts = false;
            CanvasGroup g = group;
            undo.Add((g, () =>
            {
                if (added) UnityEngine.Object.Destroy(g);
                else { g.alpha = alpha; g.blocksRaycasts = blocks; g.interactable = interactable; }
            }));
        }
    }
}
