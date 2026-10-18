using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Ui;

// Sorting for Valheim's own chest panel: three of Valheim's buttons (A-Z, Type, Weight) hung down the
// panel's right edge at its top. Only built while the Ragnarök inventory is off (the Ragnarök chest has
// its own in its footer).
internal sealed class VanillaChestSort : IDisposable
{
    private const float Width = 84, Height = 30, Gap = 6, Overhang = 10;

    private readonly Func<bool> ragnarok;
    private InventoryGui? gui;
    private RectTransform? root;

    internal VanillaChestSort(Func<bool> ragnarok) => this.ragnarok = ragnarok;

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            root = null;
            gui = current;
        }
        if (!gui) return;
        if (ragnarok())
        {
            Dispose();
            return;
        }
        if (!root && gui!.m_container && gui.m_container.gameObject.activeInHierarchy) Build(gui);
    }

    public void Dispose()
    {
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
    }

    private void Build(InventoryGui inventoryGui)
    {
        Sprite sprite = ((Image)inventoryGui.m_stackAllButton.targetGraphic).sprite;
        TMP_Text font = inventoryGui.m_stackAllButton.GetComponentInChildren<TMP_Text>(true);

        root = Ugui.Node(inventoryGui.m_container, "Ragnarok Chest Sort");
        root.anchorMin = root.anchorMax = new Vector2(1, 1);
        root.pivot = new Vector2(0, 1);
        root.anchoredPosition = new Vector2(Overhang + 8, -Overhang);
        root.sizeDelta = new Vector2(Width, Height * 3 + Gap * 2);

        AddButton(sprite, font, Equipment.SortMode.Name, "A-Z", 0);
        AddButton(sprite, font, Equipment.SortMode.Category, "Type", Height + Gap);
        AddButton(sprite, font, Equipment.SortMode.Weight, "Weight", (Height + Gap) * 2);
    }

    private void AddButton(Sprite sprite, TMP_Text font, Equipment.SortMode mode, string text, float y)
    {
        Image chip = Ugui.Image(root!, "Sort " + text, sprite, Color.white, raycast: true).At(0, y, Width, Height);
        Button button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = chip;
        button.onClick.AddListener(() =>
        {
            if (QuickStackCommand.SortChest(gui!, mode) is { } done)
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, char.ToUpper(done[0]) + done.Substring(1).ToLowerInvariant());
        });
        TMP_Text label = UnityEngine.Object.Instantiate(font, chip.transform);
        label.name = "Label";
        label.text = text;
        label.fontSize = 15;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.Stretch();
    }
}
