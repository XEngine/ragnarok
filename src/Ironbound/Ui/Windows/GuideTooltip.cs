using UnityEngine;
using UnityEngine.EventSystems;

namespace Ironbound.UI.Windows;

// Lazy, local hover card: no shared native prefab or global tooltip state.
internal sealed class GuideTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    private Theme? theme;
    private ItemCard? card;
    private string heading = "", caption = "", body = "";
    private bool hovering;

    internal static GuideTooltip Attach(Theme theme, RectTransform target)
    {
        GuideTooltip tooltip = target.gameObject.AddComponent<GuideTooltip>();
        tooltip.theme = theme;
        return tooltip;
    }

    internal void Set(string heading, string caption, string body)
    {
        this.heading = heading;
        this.caption = caption;
        this.body = body;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovering = true;
    public void OnPointerExit(PointerEventData eventData) => Hide();
    public void OnPointerDown(PointerEventData eventData) { if (ZInput.IsTouchActive()) hovering = true; }
    private void LateUpdate()
    {
        if (!hovering || theme == null || string.IsNullOrEmpty(body)) return;
        card ??= new ItemCard(theme, (RectTransform)transform);
        Rect rect = ((RectTransform)transform).rect;
        card.ShowDetails(body, new Rect(0, 0, rect.width, rect.height), heading, caption);
    }
    private void Hide() { hovering = false; card?.ShowDetails(null, default); }
    private void OnDisable() => Hide();
    private void OnDestroy() { card?.Destroy(); card = null; theme = null; }
}
