using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ragnarok.Ui;

// Small builders for code-constructed Unity UI. Layout uses absolute positions from the
// parent's top-left corner, so screens read like a fixed design grid.
internal static class Ugui
{
    private const int UiLayer = 5;

    internal static RectTransform Node(Transform parent, string name)
    {
        GameObject go = new(name, typeof(RectTransform)) { layer = UiLayer };
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    internal static T At<T>(this T component, float x, float y, float width, float height) where T : Component
    {
        RectTransform rect = (RectTransform)component.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return component;
    }

    internal static T At<T>(this T component, Rect r) where T : Component => component.At(r.x, r.y, r.width, r.height);

    internal static T Stretch<T>(this T component, float left = 0, float top = 0, float right = 0, float bottom = 0) where T : Component
    {
        RectTransform rect = (RectTransform)component.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return component;
    }

    internal static Image Image(Transform parent, string name, Sprite? sprite, Color color, bool raycast = false)
    {
        Image image = Node(parent, name).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite && sprite!.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        if (Theme.Current != null && Theme.Current.IsChamfer(sprite)) image.gameObject.AddComponent<ChipSkin>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    // Mouse-wheel step for every list: rows of 40 pixels times [Interface] ScrollSpeed (set by the plugin).
    internal static Func<float> ScrollRows = () => 3f;

    // Keeps a scroll view's wheel step on the setting, live.
    internal static ScrollRect WheelSpeed(ScrollRect scroll)
    {
        if (!scroll.GetComponent<WheelStep>()) scroll.gameObject.AddComponent<WheelStep>();
        return scroll;
    }

    // Item icons drawn as Valheim draws them: a plain quad, no aspect fitting (the boxes are square) and
    // never nine-sliced. Aspect fitting cut into icons whose art reaches the edge of their atlas sprite.
    internal static Image ItemIcon(Image image)
    {
        image.preserveAspect = false;
        image.type = UnityEngine.UI.Image.Type.Simple;
        return image;
    }

    internal static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset? font, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, float spacing = 0, bool wrap = false)
    {
        // Added while inactive so TMP's Awake sees our font; on an active parent it would first look for
        // TMP's default LiberationSans, which Valheim does not ship, and log a warning per text.
        GameObject go = Node(parent, name).gameObject;
        go.SetActive(false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        if (font) label.font = font;
        go.SetActive(true);
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.characterSpacing = spacing;
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.richText = true;
        return label;
    }

    internal static Button Button(Transform parent, string name, Sprite normal, Sprite hover, Action onClick)
    {
        Image image = Image(parent, name, normal, Color.white, raycast: true);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        SpriteSwap(button, normal, hover);
        button.onClick.AddListener(() => onClick());
        return button;
    }

    internal static void SpriteSwap(Selectable selectable, Sprite normal, Sprite hover)
    {
        ((Image)selectable.targetGraphic).sprite = normal;
        selectable.transition = Selectable.Transition.SpriteSwap;
        selectable.spriteState = new SpriteState { highlightedSprite = hover, pressedSprite = hover, selectedSprite = normal };
    }

    // Invisible full-area click target; visuals come from sibling images.
    internal static Button HitArea(Transform parent, string name, Action onClick)
    {
        Image image = Image(parent, name, null, Color.clear, raycast: true);
        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());
        return button;
    }

    internal static Image[] Outline(Transform parent, Rect r)
    {
        Image[] lines =
        {
            Image(parent, "Top", null, Color.white).At(r.x, r.y, r.width, 1),
            Image(parent, "Bottom", null, Color.white).At(r.x, r.yMax - 1, r.width, 1),
            Image(parent, "Left", null, Color.white).At(r.x, r.y, 1, r.height),
            Image(parent, "Right", null, Color.white).At(r.xMax - 1, r.y, 1, r.height)
        };
        return lines;
    }

    internal static Hover OnHover(this Component component, Action<bool> changed)
    {
        if (!component.TryGetComponent(out Hover hover)) hover = component.gameObject.AddComponent<Hover>();
        hover.Changed += changed;
        return hover;
    }

    internal static void Clear(Transform parent, string? keep = null)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (keep != null && child.name == keep) continue;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }
}

internal sealed class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    internal event Action<bool>? Changed;

    public void OnPointerEnter(PointerEventData eventData) => Changed?.Invoke(true);

    public void OnPointerExit(PointerEventData eventData) => Changed?.Invoke(false);

    private void OnDisable() => Changed?.Invoke(false);
}

// Applies the shared material finish whenever size or state (hover, pressed, selected) changes.
// Both solid and chipped silhouettes receive the finish at their actual size. Runs before the canvas
// rebuilds, so the plain stand-in never shows.
internal sealed class ChipSkin : MonoBehaviour
{
    private Image? image;

    private void Awake() => image = GetComponent<Image>();

    private void LateUpdate()
    {
        Theme? theme = Theme.Current;
        if (theme == null || !image) return;
        Rect r = ((RectTransform)transform).rect;
        int width = Mathf.RoundToInt(r.width), height = Mathf.RoundToInt(r.height);
        if (width < 4 || height < 4) return;

        Sprite current = image!.sprite;
        if (theme.IsChamfer(current) && theme.Chipped(current, width, height) is { } chipped && chipped != current)
        {
            image.sprite = chipped;
            image.type = chipped.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        // A button's sprite swap sets an override; chip that too (the getter falls back to sprite).
        Sprite over = image.overrideSprite;
        if (over != image.sprite && theme.IsChamfer(over) && theme.Chipped(over, width, height) is { } chippedOver && chippedOver != over)
            image.overrideSprite = chippedOver;
    }
}

// Sets its ScrollRect's wheel step from [Interface] ScrollSpeed each frame, so the setting applies live.
internal sealed class WheelStep : MonoBehaviour
{
    private ScrollRect? scroll;

    private void Awake() => scroll = GetComponent<ScrollRect>();

    private void LateUpdate()
    {
        if (!scroll) return;
        float step = 40f * Ugui.ScrollRows();
        if (!Mathf.Approximately(scroll!.scrollSensitivity, step)) scroll.scrollSensitivity = step;
    }
}
