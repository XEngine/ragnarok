using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ragnarok.Features.QualityOfLife;

// Quick Stack and Sort for Valheim's own inventory: one line of Valheim-style buttons set into the top edge
// of the inventory board, ending where its grid ends. Quick stack (with the number of chests in range), the
// three "keep" toggles, then Sort, which shows its mode: left click sorts, right click picks the next mode.
// The last result shows just before the buttons. Only built while the Ragnarök inventory is off.
internal sealed class VanillaQuickStack : IDisposable
{
    internal const string BarName = "Ragnarok Stack Bar";
    private const float Height = 30, Gap = 6, Overhang = 10, StackWidth = 150, SortWidth = 96;

    private static readonly (Equipment.SortMode Mode, string Label, string Hint)[] Modes =
    {
        (Equipment.SortMode.Name, "A-Z", "name"), (Equipment.SortMode.Category, "Type", "type"), (Equipment.SortMode.Weight, "Weight", "weight")
    };

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly InventoryPanel panel;
    private readonly List<(BepInEx.Configuration.ConfigEntry<bool> Entry, Image Chip, Image Icon)> toggles = new();
    private readonly Vector3[] corners = new Vector3[4];
    private InventoryGui? gui;
    private RectTransform? root;
    private TMP_Text? stackLabel, sortLabel, status;
    private int sortMode;
    private string message = "";
    private float messageTime = -10;
    private bool failed;

    internal VanillaQuickStack(InterfaceSettings settings, Theme theme, InventoryPanel panel)
    {
        this.settings = settings;
        this.theme = theme;
        this.panel = panel;
    }

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            root = null;
            failed = false;
            gui = current;
        }
        if (!gui) return;
        if (panel.Wanted)
        {
            Dispose();
            return;
        }
        if (!InventoryGui.IsVisible() || !Player.m_localPlayer || failed) return;
        if (!root)
        {
            try
            {
                Build(gui!);
            }
            catch
            {
                // A half-built bar would fail every frame; drop it, report once, and stay off for this scene.
                Dispose();
                failed = true;
                throw;
            }
        }
        Refresh(Player.m_localPlayer);
    }

    public void Dispose()
    {
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        toggles.Clear();
    }

    private void Build(InventoryGui inventoryGui)
    {
        RectTransform player = inventoryGui.m_player;
        Rect pr = player.rect;
        Sprite sprite = ((Image)inventoryGui.m_stackAllButton.targetGraphic).sprite;
        TMP_Text font = inventoryGui.m_stackAllButton.GetComponentInChildren<TMP_Text>(true);

        // Set into the board's top edge (half on the frame, half above it), ending where the grid ends.
        root = Ugui.Node(player, BarName);
        root.anchorMin = root.anchorMax = new Vector2(0, 1);
        root.pivot = new Vector2(0, 0);
        root.anchoredPosition = new Vector2(0, Overhang - Height / 2);
        root.sizeDelta = new Vector2(pr.width, Height);
        (float gridLeft, float gridRight) = GridEdges(inventoryGui.m_playerGrid, player);
        gridLeft -= pr.xMin;
        gridRight -= pr.xMin;
        float toggles3 = (Height + 4) * 3 - 4;
        float x = gridRight - (StackWidth + Gap + toggles3 + Gap * 2 + SortWidth);

        // The last result or hint, right-aligned before the buttons.
        status = UnityEngine.Object.Instantiate(font, root);
        status.name = "Status";
        status.fontSize = 14;
        status.alignment = TextAlignmentOptions.MidlineRight;
        status.textWrappingMode = TextWrappingModes.NoWrap;
        status.overflowMode = TextOverflowModes.Ellipsis;
        status.raycastTarget = false;
        status.At(gridLeft, 0, Mathf.Max(40, x - Gap * 2 - gridLeft), Height);

        Image stack = Chip(sprite, "Quick Stack", x, StackWidth);
        Ugui.Image(stack.transform, "Icon", theme.IconStack, Color.white).At(8, 4, Height - 8, Height - 8).preserveAspect = true;
        stackLabel = Label(font, stack.transform, 16, Height + 4);
        SlotInput stackInput = stack.gameObject.AddComponent<SlotInput>();
        stackInput.Down = button =>
        {
            if (button == PointerEventData.InputButton.Left) Press();
        };
        stackInput.Hover = on =>
        {
            if (on) Show("Put items into nearby chests that already hold them");
        };
        x += StackWidth + Gap;

        toggles.Clear();
        AddToggle(settings.Features.KeepWeapons, sprite, theme.IconSword, "weapons", x);
        AddToggle(settings.Features.KeepConsumables, sprite, theme.IconFood, "food", x + Height + 4);
        AddToggle(settings.Features.KeepTools, sprite, theme.IconHammer, "tools", x + (Height + 4) * 2);
        x += toggles3 + Gap * 2;

        Image sort = Chip(sprite, "Sort", x, SortWidth);
        Ugui.Image(sort.transform, "Icon", theme.IconSort, Color.white).At(6, 4, Height - 8, Height - 8).preserveAspect = true;
        sortLabel = Label(font, sort.transform, 16, Height);
        SlotInput sortInput = sort.gameObject.AddComponent<SlotInput>();
        sortInput.Down = button =>
        {
            if (button == PointerEventData.InputButton.Right)
            {
                sortMode = (sortMode + 1) % Modes.Length;
                Show("Sort by " + Modes[sortMode].Hint);
            }
            else if (button == PointerEventData.InputButton.Left && QuickStackCommand.Sort(settings, gui!, Modes[sortMode].Mode) is { } done)
                Show(Sentence(done));
        };
        sortInput.Hover = on =>
        {
            if (on) Show("Sort by " + Modes[sortMode].Hint + " · right click to change");
        };
    }

    private Image Chip(Sprite sprite, string name, float x, float width)
    {
        Image chip = Ugui.Image(root!, name, sprite, Color.white, raycast: true).At(x, 0, width, Height);
        Button button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = chip;
        return chip;
    }

    private static TMP_Text Label(TMP_Text font, Transform parent, float size, float left)
    {
        TMP_Text label = UnityEngine.Object.Instantiate(font, parent);
        label.name = "Label";
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.Stretch(left, 0, 6, 0);
        return label;
    }

    // Where the grid's slots start and end, in the panel's space (the whole grid area if it has no slots yet).
    private (float Left, float Right) GridEdges(InventoryGrid grid, RectTransform player)
    {
        RectTransform gridRoot = grid.m_gridRoot;
        float min = float.MaxValue, max = float.MinValue;
        int count = gridRoot.childCount;
        IEnumerable<Transform> parts = count > 0 ? new[] { gridRoot.GetChild(0), gridRoot.GetChild(count - 1) } : new Transform[] { gridRoot };
        foreach (Transform part in parts)
        {
            if (part is not RectTransform rect) continue;
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                float x = player.InverseTransformPoint(corner).x;
                min = Mathf.Min(min, x);
                max = Mathf.Max(max, x);
            }
        }
        return (min, max);
    }

    // A lit chip keeps that kind in the inventory; a dark one lets Quick Stack take it.
    private void AddToggle(BepInEx.Configuration.ConfigEntry<bool> entry, Sprite sprite, Sprite glyph, string label, float x)
    {
        Image chip = Ugui.Image(root!, label, sprite, Color.white, raycast: true).At(x, 0, Height, Height);
        Image icon = Ugui.Image(chip.transform, "Icon", glyph, Color.white).At(5, 5, Height - 10, Height - 10);
        Button button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = chip;
        button.onClick.AddListener(() =>
        {
            entry.Value = !entry.Value;
            Show(Hint(label, entry.Value));
        });
        chip.OnHover(on =>
        {
            if (on) Show(Hint(label, entry.Value));
        });
        toggles.Add((entry, chip, icon));
    }

    private static string Hint(string label, bool keep) => (keep ? "Keep " : "Stack ") + label;

    private static string Sentence(string text) => char.ToUpper(text[0]) + text.Substring(1).ToLowerInvariant();

    private void Show(string text)
    {
        message = text;
        messageTime = Time.unscaledTime;
    }

    private void Press()
    {
        if (QuickStackCommand.Run(settings, gui!) is { } text) Show(Sentence(text));
    }

    private void Refresh(Player player)
    {
        foreach (var t in toggles)
        {
            bool on = t.Entry.Value;
            t.Chip.color = on ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            t.Icon.color = on ? Color.white : new Color(1, 1, 1, 0.3f);
        }

        int nearby = NearbyStorage.StorageRuntime.Registry?.Nearby(player).Count ?? 0;
        string stack = $"Quick stack <color=#FFD27F>{nearby}</color>";
        if (stackLabel!.text != stack) stackLabel.text = stack;
        if (sortLabel!.text != Modes[sortMode].Label) sortLabel.text = Modes[sortMode].Label;
        string line = Time.unscaledTime - messageTime < 3f ? message : "";
        if (status!.text != line) status.text = line;
    }
}
