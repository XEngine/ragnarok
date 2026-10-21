using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// A compact, vertical version of Valheim's context key guide (building, combat, inventory,
// fishing...). It mirrors whichever vanilla hint group is active, so rebinds, gamepad layouts
// and translations stay correct without hard-coding any keys.
internal sealed class KeyHintsPanel : IDisposable
{
    private struct Part
    {
        internal string Text;
        internal Sprite? Icon;
        internal bool Joiner; // "+" or "/" between keys
    }

    private sealed class Row
    {
        internal string Label = "";
        internal readonly List<Part> Keys = new();
    }

    private struct Element
    {
        internal float X, Y;
        internal bool IsKey;
        internal Part Part;
    }

    private readonly Theme theme;
    private readonly MonochromeIcons icons;
    private readonly RectTransform root;
    private readonly VerticalLayoutGroup layout;
    private readonly List<Row> rows = new();
    private readonly List<Element> elements = new();
    private readonly StringBuilder signature = new();
    private string shown = "";
    private float nextScan;
    private TextAnchor rowAlignment = TextAnchor.MiddleRight, placedAlignment = TextAnchor.MiddleRight;
    // Width of the panel the guide sits under (0 = in its own place on screen).
    private float dockWidth;
    private bool compact;
    private readonly Vector3[] corners = new Vector3[4];
    private const float DockGap = 10, LineGap = 26;

    internal KeyHintsPanel(Theme theme, MonochromeIcons icons, Transform parent)
    {
        this.theme = theme;
        this.icons = icons;
        root = Ugui.Node(parent, "Ragnarok Key Hints");
        layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 3;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    internal void SetVisible(bool visible)
    {
        if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
    }

    internal void SetScale(float scale) => root.localScale = Vector3.one * scale;

    internal static bool CombatActive => KeyHints.instance && KeyHints.instance.m_combatHints &&
        KeyHints.instance.m_combatHints.activeInHierarchy &&
        !(KeyHints.instance.m_buildHints && KeyHints.instance.m_buildHints.activeInHierarchy);

    internal static bool ToolActive => KeyHints.instance &&
        ((KeyHints.instance.m_buildHints && KeyHints.instance.m_buildHints.activeInHierarchy) ||
         (KeyHints.instance.m_fishingHints && KeyHints.instance.m_fishingHints.activeInHierarchy));

    internal static bool HeldWeapon
    {
        get
        {
            Player player = Player.m_localPlayer;
            ItemDrop.ItemData? held = player ? player.GetCurrentWeapon() : null;
            return held != null && held.m_equipped && player!.GetInventory().ContainsItem(held);
        }
    }

    internal float DockSpace => root.rect.height * root.localScale.y + DockGap * root.localScale.y + 6;

    internal void Place(HudPlacement placement)
    {
        placement.Apply(root);
        TextAnchor alignment = (TextAnchor)(int)placement.Position.Value;
        if (placedAlignment == alignment) return;
        placedAlignment = alignment;
        rowAlignment = (int)alignment % 3 == 0 ? TextAnchor.MiddleLeft : (int)alignment % 3 == 1 ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight;
        shown = ""; // Rebuild rows with the new alignment.
    }

    // Sits the guide just under `target` (the open crafting panel), as wide as it and at its scale, the
    // hints flowing in lines; null puts it back in its own place. Where `avoid` (the status list) reaches
    // up beside it, the guide narrows to the room left of it.
    internal bool Dock(RectTransform? target, RectTransform? avoid = null, bool compact = false, float bottomInset = 0)
    {
        bool nextCompact = target && compact;
        if (this.compact != nextCompact)
        {
            this.compact = nextCompact;
            shown = "";
        }
        if (!target)
        {
            if (dockWidth > 0)
            {
                dockWidth = 0;
                // Apply() only re-anchors when the pivot differs from its point; the centre is a valid point too.
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
                shown = "";
            }
            return false;
        }
        RectTransform parent = (RectTransform)root.parent;
        target!.GetWorldCorners(corners);
        Vector2 left = parent.InverseTransformPoint(corners[0]), right = parent.InverseTransformPoint(corners[3]);
        float scale = parent.lossyScale.x > 0 ? target.lossyScale.x / parent.lossyScale.x : 1f;
        // The hotbar's visible slots end above its transparent bottom padding.
        left.y += bottomInset * scale;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 1f);
        root.localScale = Vector3.one * scale;
        // Kept on screen: with no room left below the panel it rises over the panel's foot.
        float floor = parent.rect.yMin + 6 + root.rect.height * scale;
        float top = left.y - DockGap * scale;
        // Follow the moving hotbar instead of clamping up over its slots during the lift.
        if (bottomInset <= 0) top = Mathf.Max(top, floor);
        if (avoid && avoid!.gameObject.activeInHierarchy)
        {
            avoid.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]), max = parent.InverseTransformPoint(corners[2]);
            if (min.x < right.x && max.x > left.x && max.y > top - root.rect.height * scale)
                right.x = Mathf.Max(left.x + 120 * scale, min.x - DockGap * 2 * scale);
        }
        root.anchoredPosition = new Vector2((left.x + right.x) / 2 - parent.rect.center.x, top - parent.rect.center.y);
        float width = (right.x - left.x) / scale;
        if (Mathf.Abs(width - dockWidth) > 1)
        {
            dockWidth = width;
            shown = "";
        }
        return true;
    }

    internal void Tick(bool preview)
    {
        float now = Time.unscaledTime;
        if (now < nextScan && shown.Length > 0 && (preview || !CombatActive || HeldWeapon)) return;
        nextScan = now + 0.15f;
        if (preview) Demo();
        else Scan();

        signature.Clear().Append(rowAlignment).Append('|');
        foreach (Row row in rows)
        {
            signature.Append(row.Label).Append(':');
            foreach (Part part in row.Keys) signature.Append(part.Text).Append(part.Icon ? part.Icon!.name : "").Append(',');
            signature.Append(';');
        }
        string current = signature.ToString();
        if (current == shown) return;
        shown = current;
        Rebuild();
    }

    public void Dispose()
    {
        if (root) UnityEngine.Object.Destroy(root.gameObject);
    }

    // Reading vanilla ------------------------------------------------------------

    private void Scan()
    {
        rows.Clear();
        KeyHints hints = KeyHints.instance;
        if (!hints) return;
        GameObject? group = FirstActive(hints.m_buildHints, hints.m_combatHints, hints.m_inventoryHints,
            hints.m_inventoryWithContainerHints, hints.m_fishingHints, hints.m_barberHints, hints.m_radialHints);
        if (!group) return;
        // Native targeting starts active on entry and can enable fist hints.
        // Our combat guide belongs to an actually equipped inventory weapon.
        if (group == hints.m_combatHints && !HeldWeapon) return;

        elements.Clear();
        foreach (TMP_Text text in group!.GetComponentsInChildren<TMP_Text>(false))
        {
            if (!text.enabled || text.color.a < 0.05f) continue;
            string value = Clean(text.text);
            if (value.Length == 0) continue;
            bool joiner = value is "+" or "/";
            Vector3 p = text.rectTransform.position;
            elements.Add(new Element { X = p.x, Y = p.y, IsKey = joiner || IsKeycap(text), Part = new Part { Text = value, Joiner = joiner } });
        }
        foreach (Image image in group.GetComponentsInChildren<Image>(false))
        {
            // Key icons (mouse wheel, gamepad glyphs) are small images with no text inside.
            if (!image.enabled || !image.sprite || image.color.a < 0.1f) continue;
            if (image.GetComponentInChildren<TMP_Text>(false)) continue;
            Rect r = image.rectTransform.rect;
            if (r.width < 8 || r.width > 80 || r.height > 80) continue;
            Vector3 p = image.rectTransform.position;
            elements.Add(new Element { X = p.x, Y = p.y, IsKey = true, Part = new Part { Icon = image.sprite } });
        }

        // Vanilla lays hints out left to right; read them in that order, line by line.
        elements.Sort((a, b) =>
        {
            int line = Mathf.RoundToInt(b.Y / 24f).CompareTo(Mathf.RoundToInt(a.Y / 24f));
            return line != 0 ? line : a.X.CompareTo(b.X);
        });
        Row? row = null;
        foreach (Element element in elements)
        {
            if (!element.IsKey || row == null)
            {
                row = new Row();
                rows.Add(row);
                if (!element.IsKey)
                {
                    row.Label = element.Part.Text;
                    continue;
                }
            }
            row.Keys.Add(element.Part);
        }
        rows.RemoveAll(r => r.Label.Length == 0 && r.Keys.Count == 0);
    }

    private static GameObject? FirstActive(params GameObject[] groups)
    {
        foreach (GameObject group in groups)
            if (group && group.activeInHierarchy) return group;
        return null;
    }

    // A keycap is text sitting alone on its own small background image.
    private static bool IsKeycap(TMP_Text text)
    {
        Transform parent = text.transform.parent;
        if (!parent || !parent.TryGetComponent(out Image background) || !background.enabled || !background.sprite) return false;
        if (((RectTransform)parent).rect.width > 220) return false;
        return parent.GetComponentsInChildren<TMP_Text>(false).Length == 1;
    }

    private static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string value = text!.Trim();
        if (value.IndexOf('$') >= 0 && Localization.instance != null) value = Localization.instance.Localize(value).Trim();
        return value;
    }

    private void Demo()
    {
        rows.Clear();
        Add("Place", "Mouse-1");
        Add("Remove", "Mouse-3");
        Add("Build menu", "Mouse-2");
        Add("Toggle snapping", "Shift");
        Add("Cycle snapping", "Q", "E");
        Add("Copy", "Shift", "+", "Mouse-3");
        Add("Rotate", "Wheel");

        void Add(string label, params string[] keys)
        {
            Row row = new() { Label = label };
            foreach (string key in keys) row.Keys.Add(new Part { Text = key, Joiner = key == "+" });
            rows.Add(row);
        }
    }

    // Drawing ------------------------------------------------------------------------

    private void Rebuild()
    {
        Ugui.Clear(root);
        bool docked = dockWidth > 0;
        layout.childAlignment = docked ? TextAnchor.UpperCenter : placedAlignment;
        List<RectTransform> built = new();
        foreach (Row data in rows)
        {
            RectTransform row = Ugui.Node(root, "Hint");
            built.Add(row);
            HorizontalLayoutGroup line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 6;
            line.childAlignment = docked ? TextAnchor.MiddleLeft : rowAlignment;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = compact;
            row.gameObject.AddComponent<LayoutElement>().minHeight = compact ? 18 : 24;

            // As in the style guide: the keys first, then what they do.
            foreach (Part part in data.Keys)
            {
                if (part.Joiner) Ugui.Text(row, "Joiner", part.Text, theme.Strong, compact ? 10.5f : 12, Palette.Muted,
                    compact ? TextAlignmentOptions.Midline : TextAlignmentOptions.MidlineLeft);
                else if (part.Icon) Icon(row, part.Icon!);
                else Keycap(row, part.Text);
            }
            if (data.Label.Length > 0)
            {
                TextMeshProUGUI label = Ugui.Text(row, "Label", data.Label, compact ? theme.Body : theme.Strong,
                    compact ? 10.5f : 13, compact ? InventoryCategories.Material : Palette.Bone,
                    TextAlignmentOptions.MidlineLeft);
                label.margin = new Vector4(3, 0, 0, 0);
            }
        }
        if (docked) Flow(built);
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    // Under the crafting panel: the hints side by side, wrapping onto centred lines no wider than the panel.
    private void Flow(List<RectTransform> hints)
    {
        RectTransform? line = null;
        float used = 0;
        float spacing = compact ? 18 : LineGap;
        foreach (RectTransform hint in hints)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(hint);
            float width = LayoutUtility.GetPreferredWidth(hint);
            if (!line || used + spacing + width > dockWidth)
            {
                line = Ugui.Node(root, "Line");
                HorizontalLayoutGroup group = line.gameObject.AddComponent<HorizontalLayoutGroup>();
                group.spacing = spacing;
                group.childAlignment = TextAnchor.MiddleCenter;
                group.childControlWidth = group.childControlHeight = true;
                group.childForceExpandWidth = group.childForceExpandHeight = false;
                used = -spacing;
            }
            hint.SetParent(line, false);
            used += spacing + width;
        }
    }

    private void Keycap(Transform row, string text)
    {
        if (compact)
        {
            Ugui.Text(row, "Key", text, theme.Strong, 10.5f, Color.white, TextAlignmentOptions.Midline);
            return;
        }
        Image cap = Ugui.Image(row, "Key", theme.Button, Color.white);
        HorizontalLayoutGroup pad = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
        pad.padding = new RectOffset(8, 8, 3, 3);
        pad.childAlignment = TextAnchor.MiddleCenter;
        pad.childControlWidth = pad.childControlHeight = true;
        pad.childForceExpandWidth = pad.childForceExpandHeight = false;
        LayoutElement size = cap.gameObject.AddComponent<LayoutElement>();
        size.minWidth = 24;
        size.minHeight = 22;
        Ugui.Text(cap.transform, "Label", text, theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center);
    }

    private void Icon(Transform row, Sprite sprite)
    {
        Image image = Ugui.Image(row, "Icon", icons.Get(sprite) ?? sprite, compact ? Color.white : Palette.Bone);
        image.preserveAspect = true;
        LayoutElement size = image.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = size.preferredHeight = compact ? 14 : 18;
    }
}
