using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Ui;

// The shared frame of Ragnarök's book screens (Skills, Compendium, Trophies, Achievements): a full-screen
// window drawn over one of Valheim's dialogs while it is open. Valheim's dialog stays alive but invisible,
// so its data, Esc and gamepad handling are untouched. The frame carries the tabs between the screens, a
// diagonal art panel on the right with drifting embers and a large glyph, and ESC · CLOSE.
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
    private readonly List<(RectTransform Rect, Image Image, float Speed, float Drift, float Phase)> embers = new();
    private GameObject? canvas;
    private CanvasGroup? hidden;
    private float hiddenAlpha;
    private bool hiddenBlocks;
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
        Animate();
        Refresh();
    }

    public virtual void Dispose() => Close();

    private void Close()
    {
        if (hidden)
        {
            hidden!.alpha = hiddenAlpha;
            hidden.blocksRaycasts = hiddenBlocks;
        }
        hidden = null;
        if (canvas) UnityEngine.Object.Destroy(canvas);
        bool wasOpen = canvas;
        canvas = null;
        window = null;
        embers.Clear();
        dialog = null;
        if (wasOpen) Closed();
    }

    private void Open(InventoryGui gui, Component vanilla)
    {
        // Valheim's dialog keeps running underneath, unseen and unclickable.
        if (!vanilla.TryGetComponent(out CanvasGroup group)) group = vanilla.gameObject.AddComponent<CanvasGroup>();
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
        window = Ugui.Image(canvas.transform, "Window", theme.Window, Color.white, raycast: true).rectTransform;
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(Width, Height);

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
        float width = 176, x = Width / 2 - width * tabs.Length / 2;
        foreach (var (label, tab, open) in tabs)
        {
            bool selected = tab == Selected;
            // Alegreya, the open page in bone over a gold underline and diamond, the others muted.
            TextMeshProUGUI text = Ugui.Text(window!, label, label, theme.Heading, selected ? 24 : 20, selected ? Palette.Bone : Palette.Muted,
                TextAlignmentOptions.Center).At(x, 38, width, 34);
            if (selected)
            {
                Ugui.Image(window!, "Tab Line", theme.White, Palette.GoldBright).At(x + 40, 84, width - 80, 2);
                Ugui.Image(window!, "Tab Notch", theme.Diamond, Palette.GoldBright).At(x + width / 2 - 5, 79, 10, 10);
            }
            else
            {
                Action run = open;
                Ugui.HitArea(window!, "Tab " + label, () => { CloseDialog(); run(); }).At(x, 36, width, 40)
                    .OnHover(on => text.color = on ? Palette.GoldBright : Palette.Muted);
            }
            x += width;
        }
        Ugui.Image(window!, "Tab Rule", theme.White, Palette.Gold.WithAlpha(0.45f)).At(Left, 85, Width * 0.62f - Left, 1);
    }

    // [Esc] Close, bottom left, as in the guide's key hints.
    private void CloseBar()
    {
        Image key = Ugui.Image(window!, "Esc", theme.Button, Color.white).At(Left, Height - 74, 40, 26);
        Ugui.Text(key.transform, "Label", "Esc", theme.Strong, 13, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Ugui.Button(window!, "Close", theme.White, theme.White, CloseDialog).At(Left + 48, Height - 74, 90, 26)
            .GetComponent<Image>().color = Color.clear;
        TextMeshProUGUI close = Ugui.Text(window!, "Close Label", "Close", theme.Strong, 14, Palette.Bone).At(Left + 50, Height - 74, 90, 26);
        key.transform.parent.Find("Close").OnHover(on => close.color = on ? Palette.GoldBright : Palette.Bone);
    }

    // The right-hand scene: cut diagonally, dark and warm, a big glyph and embers rising through it.
    private void Art()
    {
        float x = Width * 0.56f, w = Width - x - 6;
        RectTransform art = Ugui.Image(window!, "Art", theme.ArtCut, Color.white).At(x, 6, w, Height - 12).rectTransform;
        art.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Ugui.Image(art, "Scene", theme.ArtScene, Color.white).Stretch();
        glyph = Ugui.Image(art, "Glyph", null, new Color(0.62f, 0.78f, 1f, 0.5f));
        glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = glyph.rectTransform.pivot = new Vector2(0.62f, 0.55f);
        glyph.rectTransform.sizeDelta = new Vector2(320, 320);
        glyph.preserveAspect = true;
        System.Random random = new(7);
        for (int i = 0; i < 46; i++)
        {
            float size = 2.5f + (float)random.NextDouble() * 3.5f;
            Image ember = Ugui.Image(art, "Ember", theme.Diamond, Palette.Ember);
            ember.rectTransform.anchorMin = ember.rectTransform.anchorMax = new Vector2((float)random.NextDouble(), 0);
            ember.rectTransform.sizeDelta = new Vector2(size, size);
            embers.Add((ember.rectTransform, ember, 30f + (float)random.NextDouble() * 70f, (float)random.NextDouble() * 30f - 15f,
                (float)random.NextDouble() * 100f));
        }
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
        return view;
    }

    // Embers rise and sway, fading near the top; the glyph breathes.
    private void Animate()
    {
        float t = Time.unscaledTime;
        foreach (var (rect, image, speed, drift, phase) in embers)
        {
            float height = Height - 12, rise = (t * speed + phase * 9f) % (height + 40);
            rect.anchoredPosition = new Vector2(Mathf.Sin(t * 0.8f + phase) * drift, rise - 20);
            float life = rise / height;
            image.color = Palette.Ember.WithAlpha(Mathf.Clamp01(1.2f - life) * (0.55f + 0.45f * Mathf.Sin(t * 5f + phase)));
        }
        if (glyph) glyph!.color = new Color(0.62f, 0.78f, 1f, 0.42f + 0.1f * Mathf.Sin(t * 1.2f));
    }
}
