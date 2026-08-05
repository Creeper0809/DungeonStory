using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ResearchTreeViewportController
{
    private const float MinZoom = 0.55f;
    private const float MaxZoom = 1.45f;

    private readonly RectTransform viewport;
    private readonly RectTransform graphRoot;
    private readonly RectTransform nodeRoot;
    private readonly ResearchConnectorGraphic connectorGraphic;
    private ResearchGraphLayout layout;
    private float zoom = 1f;

    public ResearchTreeViewportController(
        RectTransform viewport,
        RectTransform graphRoot,
        RectTransform nodeRoot,
        ResearchConnectorGraphic connectorGraphic)
    {
        this.viewport = viewport;
        this.graphRoot = graphRoot;
        this.nodeRoot = nodeRoot;
        this.connectorGraphic = connectorGraphic;
    }

    public void SetLayout(ResearchGraphLayout layout)
    {
        this.layout = layout;
        if (layout == null)
        {
            return;
        }

        graphRoot.sizeDelta = layout.Bounds.size;
        nodeRoot.sizeDelta = layout.Bounds.size;
        connectorGraphic.rectTransform.sizeDelta = layout.Bounds.size;
    }

    public void Pan(Vector2 delta)
    {
        graphRoot.anchoredPosition += delta;
    }

    public void Zoom(PointerEventData eventData)
    {
        float next = Mathf.Clamp(
            zoom * (eventData.scrollDelta.y > 0f ? 1.1f : 0.9f),
            MinZoom,
            MaxZoom);
        if (Mathf.Approximately(next, zoom))
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointer);
        Vector2 graphPoint = (pointer - graphRoot.anchoredPosition) / zoom;
        zoom = next;
        graphRoot.localScale = Vector3.one * zoom;
        graphRoot.anchoredPosition = pointer - graphPoint * zoom;
    }

    public void Fit()
    {
        if (layout == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        Vector2 viewportSize = viewport.rect.size;
        if (viewportSize.x <= 1f || viewportSize.y <= 1f)
        {
            return;
        }

        zoom = Mathf.Clamp(
            Mathf.Min(
                (viewportSize.x - 36f) / layout.Bounds.width,
                (viewportSize.y - 36f) / layout.Bounds.height),
            MinZoom,
            1f);
        graphRoot.localScale = Vector3.one * zoom;
        graphRoot.anchoredPosition = new Vector2(
            Mathf.Max(
                18f,
                (viewportSize.x - layout.Bounds.width * zoom) * 0.5f),
            -Mathf.Max(
                18f,
                (viewportSize.y - layout.Bounds.height * zoom) * 0.5f));
    }

    public bool Center(ResearchProjectId projectId)
    {
        if (!projectId.IsValid
            || layout == null
            || !layout.NodeRects.TryGetValue(
                projectId.Value,
                out Rect rect))
        {
            return false;
        }

        Canvas.ForceUpdateCanvases();
        Vector2 nodeCenter = new Vector2(rect.center.x, -rect.center.y);
        Vector3 nodeWorldPosition = graphRoot.TransformPoint(nodeCenter);
        Vector2 positionInViewport = viewport.InverseTransformPoint(
            nodeWorldPosition);
        graphRoot.anchoredPosition += viewport.rect.center - positionInViewport;
        return true;
    }
}
