using System;
using UnityEngine;

namespace Ragnarok.UI.Windows;

// [Inventory] Scale controls the guide layout. The native layout only shrinks
// when necessary to fit its character board/open chest, anchored at the same top-left
// corner. It grows into the free space towards the info board and crafting panel, which keep their size
// and place; growth stops short of those panels and of the screen's edges. An open chest keeps its size and
// only moves down as far as the inventory grew.
internal sealed class InventoryScale : IDisposable
{
    private const float Margin = 8;

    // 100 % (1x) is Valheim's own inventory size.
    private const float Base = 1f;

    private readonly InterfaceSettings settings;
    private readonly Vector3[] corners = new Vector3[4];
    private InventoryGui? gui;
    private RectTransform? player, container;
    // Where Valheim put each panel, relative to its anchor: unlike a local position, this holds while the
    // interface is still sizing itself to the screen (the first frames after loading).
    private Vector2 playerHome, containerHome;

    private readonly Func<bool> ragnarok;
    private bool placed;
    private bool custom;

    internal InventoryScale(InterfaceSettings settings, Func<bool> ragnarok)
    {
        this.settings = settings;
        this.ragnarok = ragnarok;
    }

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            gui = current;
            player = gui ? gui.m_player : null;
            container = gui ? gui.m_container : null;
            if (player) playerHome = player!.anchoredPosition;
            if (container) containerHome = container!.anchoredPosition;
        }
        if (!player || !container) return;
        bool guide = ragnarok();
        if (placed && custom != guide) Dispose();
        custom = guide;
        if (!InventoryGui.IsVisible()) return;
        placed = true;

        float wanted = guide ? settings.InventoryScale.Value * Base : 1f;
        // The reference layout includes its header and footer. Allow it to shrink
        // when needed instead of forcing the taller layout past the screen edge.
        float scale = Mathf.Clamp(Mathf.Min(wanted, Room()), 0.5f, wanted);

        // Each panel keeps its top-left corner where Valheim put it.
        Place(player!, playerHome, scale, Vector2.zero);
        // Native chest is anchored below Player and inherits its scale. It needs
        // no extra growth offset; keep its own native anchor/scale intact.
        if (!guide)
        {
            container!.localScale = Vector3.one;
            container.anchoredPosition = containerHome;
            return;
        }
        // The chest sits below the inventory at its own size: it moves down by as much as the inventory grew.
        Vector3 grown = player!.parent.TransformVector(new Vector3(0, -(scale - 1) * player.rect.height, 0));
        Vector2 down = container!.parent.InverseTransformVector(grown);
        if (container.localScale != Vector3.one) container.localScale = Vector3.one;
        if (container.anchoredPosition != containerHome + down) container.anchoredPosition = containerHome + down;
        // The custom frames are shorter than vanilla's layout rectangles. Dock the chest to
        // the visible inventory edge instead of inheriting vanilla's large empty gap.
        if (player.Find("Ragnarok Inventory") is RectTransform inventoryFrame && inventoryFrame.gameObject.activeInHierarchy &&
            container.Find("Ragnarok Chest") is RectTransform chestFrame && chestFrame.gameObject.activeInHierarchy)
        {
            Rect inventoryBounds = Bounds(inventoryFrame, container.parent);
            Rect chestBounds = Bounds(chestFrame, container.parent);
            // Room for the chest's weight tab under it and the inventory's stats tab over the inventory.
            const float panelGap = 28f;
            container.anchoredPosition += new Vector2(0, inventoryBounds.yMin - panelGap - chestBounds.yMax);
        }
    }

    public void Dispose()
    {
        placed = false;
        if (player)
        {
            player!.localScale = Vector3.one;
            player.anchoredPosition = playerHome;
        }
        if (container)
        {
            container!.localScale = Vector3.one;
            container.anchoredPosition = containerHome;
        }
    }

    private static void Place(RectTransform panel, Vector2 home, float scale, Vector2 shift)
    {
        Rect r = panel.rect;
        Vector2 topLeft = new(r.xMin, r.yMax);
        Vector2 position = home + (1 - scale) * topLeft + shift;
        if (panel.localScale != Vector3.one * scale) panel.localScale = Vector3.one * scale;
        if (panel.anchoredPosition != position) panel.anchoredPosition = position;
    }

    // How large the inventory may grow before it reaches the info board, the crafting panel or the screen's
    // edges. Everything is measured as at Valheim's size, in the space the inventory sits in.
    private float Room()
    {
        RectTransform p = player!;
        Transform space = p.parent;
        Rect pr = p.rect;
        Vector3 home = Home(p, playerHome), chestHome = Home(container!, containerHome);
        Vector2 topLeft = (Vector2)home + new Vector2(pr.xMin, pr.yMax);

        // The inventory's right side and bottom, with what hangs outside its rectangle: the character column
        // or the vanilla character board under it, and the Quick stack bar on top.
        float right = home.x + pr.xMax, lowest = home.y + pr.yMin;
        foreach (string path in new[]
                 {
                     "Ragnarok Inventory", "Ragnarok Inventory/Character Frame", "Ragnarok Character Vanilla",
                     VanillaQuickStack.BarName
                 })
            if (p.Find(path) is RectTransform part && part.gameObject.activeInHierarchy)
            {
                part.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = p.InverseTransformPoint(corner);
                    right = Mathf.Max(right, home.x + local.x);
                    lowest = Mathf.Min(lowest, home.y + local.y);
                }
            }
        // The bottom: the inventory's, or an open chest's below it (the chest keeps its size and moves down
        // as far as the inventory grows, so it is the chest's bottom that must stay on screen).
        float playerBottom = lowest, bottom = playerBottom;
        if (container!.gameObject.activeInHierarchy)
        {
            if (!custom)
            {
                container.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                    bottom = Mathf.Min(bottom, home.y + p.InverseTransformPoint(corner).y);
            }
            else
            {
                float yMin = container.rect.yMin;
                if (container.Find("Ragnarok Chest") is RectTransform chest && chest.gameObject.activeInHierarchy)
                {
                    chest.GetWorldCorners(corners);
                    yMin = float.MaxValue;
                    foreach (Vector3 corner in corners) yMin = Mathf.Min(yMin, container.InverseTransformPoint(corner).y);
                }
                Vector3 chestBottom = space.InverseTransformPoint(container.parent.TransformPoint(chestHome + new Vector3(0, yMin, 0)));
                bottom = Mathf.Min(bottom, chestBottom.y);
            }
        }

        // Limits: the screen, and the panels to the right.
        Canvas? canvas = p.GetComponentInParent<Canvas>();
        Rect screen = Bounds(canvas ? canvas!.rootCanvas.transform as RectTransform : null, space);
        float limitRight = screen.xMax - Margin, limitBottom = screen.yMin + Margin;
        foreach (Transform? other in new Transform?[] { gui!.m_infoPanel, gui.m_crafting })
        {
            if (other is not RectTransform o || !o.gameObject.activeInHierarchy) continue;
            Rect b = Bounds(o, space);
            if (b.xMin > topLeft.x + 10) limitRight = Mathf.Min(limitRight, b.xMin - Margin * 2);
        }
        // The integrated repair extension occupies space left of the native crafting rectangle.
        RectTransform? repair = CraftPanel.RepairExtension;
        if (repair && repair!.gameObject.activeInHierarchy)
        {
            CanvasGroup? group = repair.GetComponentInParent<CanvasGroup>();
            if (!group || group!.alpha > 0.05f)
            {
                Rect b = Bounds(repair, space);
                if (b.xMin > topLeft.x + 10) limitRight = Mathf.Min(limitRight, b.xMin - Margin * 2);
            }
        }

        float width = right - topLeft.x, height = topLeft.y - playerBottom;
        if (width < 1 || height < 1 || screen.width < 1) return 1f;
        if (!custom)
            return Mathf.Min((limitRight - topLeft.x) / (width + 10),
                (topLeft.y - limitBottom) / (topLeft.y - bottom + 10));
        return Mathf.Min((limitRight - topLeft.x) / width, 1 + (bottom - limitBottom) / height);
    }

    // The local position a panel has when it sits at its anchored home.
    private static Vector3 Home(RectTransform panel, Vector2 home) => panel.localPosition + (Vector3)(home - panel.anchoredPosition);

    // A rectangle's extent in another transform's space.
    private Rect Bounds(RectTransform? rect, Transform space)
    {
        if (!rect) return default;
        rect!.GetWorldCorners(corners);
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (Vector3 corner in corners)
        {
            Vector2 local = space.InverseTransformPoint(corner);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
