using UnityEngine;
using UnityEngine.EventSystems;

public sealed class OffenseV17MapInput :
    MonoBehaviour,
    IScrollHandler
{
    private const float MinimumZoom = 0.55f;
    private const float MaximumZoom = 1.45f;
    private RectTransform content;
    private float zoom = 1f;

    public void Bind(RectTransform mapContent)
    {
        content = mapContent;
        ApplyZoom();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (content == null || eventData == null)
        {
            return;
        }

        zoom = Mathf.Clamp(
            zoom + Mathf.Sign(eventData.scrollDelta.y) * 0.1f,
            MinimumZoom,
            MaximumZoom);
        ApplyZoom();
        eventData.Use();
    }

    public void ResetView()
    {
        zoom = 1f;
        if (content != null)
        {
            content.anchoredPosition = Vector2.zero;
        }

        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (content != null)
        {
            content.localScale = Vector3.one * zoom;
        }
    }
}
