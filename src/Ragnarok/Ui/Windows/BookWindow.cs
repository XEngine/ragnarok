using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI.Windows;

// Shared rounded frame for the four native-backed character books. Native data,
// close handling and screen-specific content remain owned by their dialogs.
internal abstract class BookWindow : IDisposable
{
    protected const float Width = 1560, Height = 880, Left = 64, Top = 140;

    protected enum Tab
    {
        Skills,
        Compendium,
        Trophies,
        Achievements
    }

    protected readonly InterfaceSettings settings;
    protected readonly Theme theme;
    private GameObject? canvas;
    private CanvasGroup? hidden;
    private bool ownsHidden;
    private float hiddenAlpha;
    private bool hiddenBlocks;
    private Image? background;
    private Component? dialog;
    protected RectTransform? window;
    protected Image? glyph;

    protected BookWindow(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    protected abstract string Name { get; }
    protected abstract Tab Selected { get; }
    protected abstract bool Wanted { get; }

    // Valheim's dialog this screen stands in for, and how to close it.
    protected abstract Component? Dialog(InventoryGui gui);
    protected abstract void CloseDialog();

    // The screen's own contents, inside the frame.
    protected abstract void Build(InventoryGui gui);

    protected virtual void Refresh() { }

    protected virtual void Closed() { }

    internal void Tick()
    {
        InventoryGui gui = InventoryGui.instance;
        Component? current = gui ? Dialog(gui) : null;
        bool open = Wanted && current && current!.gameObject.activeInHierarchy && Player.m_localPlayer;
        if (!open)
        {
            Close();
            return;
        }
        if (dialog != current || !canvas)
        {
            Close();
            dialog = current;
            Open(gui!, current!);
        }
        background!.color = Palette.Hex(0x0C0F11, settings.InventoryOpacity.Value);
        Rect host = ((RectTransform)canvas!.transform).rect;
        if (host.width > 48 && host.height > 48)
            window!.localScale = Vector3.one * Mathf.Min(1, Mathf.Min((host.width - 48) / Width, (host.height - 48) / Height));
        Refresh();
    }

    public virtual void Dispose() => Close();

    private void Close()
    {
        if (hidden)
        {
            hidden!.alpha = hiddenAlpha;
            hidden.blocksRaycasts = hiddenBlocks;
            if (ownsHidden) UnityEngine.Object.Destroy(hidden);
        }
        hidden = null; ownsHidden = false;
        bool wasOpen = canvas;
        if (canvas)
        {
            foreach (Button button in canvas!.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            canvas.SetActive(false);
            UnityEngine.Object.Destroy(canvas);
        }
        canvas = null;
        window = null;
        background = null; glyph = null;
        dialog = null;
        if (wasOpen) Closed();
    }

    private void Open(InventoryGui gui, Component vanilla)
    {
        // Valheim's dialog keeps running underneath, unseen and unclickable.
        // CanvasGroup allows only one component per GameObject. Preserve native
        // groups instead of attempting to add another and receiving null.
        ownsHidden = !vanilla.TryGetComponent(out CanvasGroup group);
        if (ownsHidden) group = vanilla.gameObject.AddComponent<CanvasGroup>();
        hidden = group;
        hiddenAlpha = group.alpha;
        hiddenBlocks = group.blocksRaycasts;
        group.alpha = 0;
        group.blocksRaycasts = false;

        canvas = new GameObject("Ragnarok " + Name, typeof(RectTransform)) { layer = 5 };
        Canvas c = canvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 29000;
        CanvasScaler scaler = canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        canvas.AddComponent<GraphicRaycaster>();

        Ugui.Image(canvas.transform, "Backdrop", theme.White, Palette.Backdrop, raycast: true).Stretch();
        background = Ugui.Image(canvas.transform, "Window", theme.PackSurface,
            Palette.Hex(0x0C0F11, settings.InventoryOpacity.Value), raycast: true);
        window = background.rectTransform;
        Ugui.Image(window, "Frame", theme.PackEdge, Palette.Hex(0x3D4541)).Stretch();
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(Width, Height);

        Surface(window, "Library", new Rect(Left - 32, Top - 12, 382, Height - Top - 70));
        Surface(window, "Details", new Rect(454, Top - 16, 510, Height - Top - 66));
        Art();
        Tabs(gui);
        CloseBar();
        Build(gui);
    }

    private void Tabs(InventoryGui gui)
    {
        (string Label, Tab Tab, Action Open)[] tabs =
        {
            ("Skills", Tab.Skills, gui.OnOpenSkills),
            ("Compendium", Tab.Compendium, gui.OnOpenTexts),
            ("Trophies", Tab.Trophies, gui.OnOpenTrophies),
            ("Achievements", Tab.Achievements, gui.OnOpenAchievements)
        };
        float width = 176, x = Left;
        foreach (var (label, tab, open) in tabs)
        {
            bool selected = tab == Selected;
            Button button = ActionButton(window!, label, () => { if (!selected) { CloseDialog(); open(); } });
            button.At(x, 30, width, 38);
            Ugui.Image(button.transform, "Edge", theme.PackPillEdge, selected ? Palette.Gold : Palette.Hex(0x303631)).Stretch();
            Ugui.Text(button.transform, "Label", label, theme.Strong, 15, selected ? Palette.Gold : Palette.Bone,
                TextAlignmentOptions.Midline).Stretch(8, 0, 8, 0);
            x += width + 8;
        }
        Ugui.Image(window!, "Header Rule", theme.White, Palette.Hex(0x303631)).At(Left, 86, Width - Left * 2, 1);
    }

    protected Button ActionButton(Transform parent, string name, Action click)
    {
        Button button = Ugui.Button(parent, name, theme.PackPill, theme.PackPill, click);
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Palette.Hex(0x141A1B);
        colors.highlightedColor = colors.selectedColor = Palette.Hex(0x29291F);
        colors.pressedColor = Palette.Hex(0x493D25);
        colors.colorMultiplier = 1; colors.fadeDuration = 0.08f;
        button.colors = colors;
        return button;
    }

    protected Image Surface(Transform parent, string name, Rect area)
    {
        Image face = Ugui.Image(parent, name, theme.PackSurface, Palette.Hex(0x101516)).At(area);
        Ugui.Image(face.transform, "Edge", theme.PackEdge, Palette.Hex(0x303631)).Stretch();
        return face;
    }

    protected Image Choice(Transform parent, Rect area, bool selected)
    {
        Image face = Ugui.Image(parent, "Choice", theme.PackPill,
            selected ? Palette.Hex(0x302B1E) : Palette.Hex(0x141A1B)).At(area);
        return Ugui.Image(face.transform, "Edge", theme.PackPillEdge,
            selected ? Palette.Gold : Palette.Hex(0x303631)).Stretch();
    }

    private void CloseBar()
    {
        Button button = ActionButton(window!, "Close", CloseDialog);
        button.At(Width - Left - 118, 30, 118, 38);
        Ugui.Image(button.transform, "Edge", theme.PackPillEdge, Palette.Hex(0x303631)).Stretch();
        Image key = Ugui.Image(button.transform, "Esc", theme.HudKeySurface, Palette.Hex(0x080B0C)).At(10, 9, 34, 20);
        Ugui.Image(key.transform, "Edge", theme.HudKeyEdge, Palette.Faint).Stretch();
        Ugui.Text(key.transform, "Key", "Esc", theme.Strong, 11, Color.white, TextAlignmentOptions.Midline).Stretch();
        Ugui.Text(button.transform, "Label", "Close", theme.Strong, 13, Palette.Bone).At(54, 0, 56, 38);
    }

    private void Art()
    {
        Image art = Surface(window!, "Preview", new Rect(1000, Top - 16, Width - 1000 - Left, Height - Top - 66));
        glyph = Ugui.Image(art.transform, "Glyph", null, Palette.Gold.WithAlpha(0.5f));
        glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = glyph.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        glyph.rectTransform.sizeDelta = new Vector2(240, 240);
        glyph.preserveAspect = true;
    }

    protected RectTransform Scroller(Rect area, out RectTransform content)
    {
        RectTransform view = Ugui.Node(window!, "Scroll").At(area);
        RectTransform viewport = Ugui.Image(view, "Viewport", null, Color.clear, raycast: true).Stretch().rectTransform;
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Ugui.Node(viewport, "Content");
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0, 1);
        content.sizeDelta = Vector2.zero;
        ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        Ugui.WheelSpeed(scroll);
        scroll.inertia = false;
        RectTransform rail = Ugui.Node(view, "Scrollbar").Stretch();
        rail.anchorMin = new Vector2(1, 0); rail.anchorMax = Vector2.one;
        rail.offsetMin = new Vector2(2, 4); rail.offsetMax = new Vector2(12, -4);
        CapsuleGraphic.Create(rail, "Track", Palette.Bone.WithAlpha(0.08f)).Stretch(4, 0, 3, 0);
        RectTransform handle = Ugui.Node(rail, "Handle").Stretch();
        CapsuleGraphic thumb = CapsuleGraphic.Create(handle, "Thumb", Palette.Gold.WithAlpha(0.65f)).Stretch(3, 0, 2, 0);
        thumb.raycastTarget = true;
        Scrollbar bar = rail.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle; bar.targetGraphic = thumb; bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return view;
    }

}
