using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

public interface IResearchTreeInteractionSink
{
    void Pan(Vector2 delta);
    void Zoom(PointerEventData eventData);
    void BeginQueueDrag();
    void MoveQueueEntry(int fromIndex, Vector2 pointerScreenPosition);
    void EndQueueDrag();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchTreePanSurface :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IScrollHandler
{
    private IResearchTreeInteractionSink owner;
    private Vector2 previous;

    public void Bind(IResearchTreeInteractionSink interactionSink)
    {
        owner = interactionSink;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        previous = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - previous;
        previous = eventData.position;
        owner?.Pan(delta);
    }

    public void OnScroll(PointerEventData eventData)
    {
        owner?.Zoom(eventData);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchQueueRowDrag :
    MonoBehaviour,
    IBeginDragHandler,
    IEndDragHandler
{
    private IResearchTreeInteractionSink owner;
    private int index;

    public void Bind(IResearchTreeInteractionSink interactionSink, int queueIndex)
    {
        owner = interactionSink;
        index = queueIndex;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.BeginQueueDrag();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.MoveQueueEntry(index, eventData.position);
        owner?.EndQueueDrag();
    }
}

public readonly struct ResearchConnectorLine
{
    public ResearchConnectorLine(
        IReadOnlyList<Vector2> points,
        Color color,
        bool dotted)
    {
        Points = points ?? Array.Empty<Vector2>();
        Color = color;
        Dotted = dotted;
    }

    public IReadOnlyList<Vector2> Points { get; }
    public Color Color { get; }
    public bool Dotted { get; }
}

[RequireComponent(typeof(CanvasRenderer))]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchConnectorGraphic : MaskableGraphic
{
    private readonly List<ResearchConnectorLine> lines = new();

    public void SetLines(IEnumerable<ResearchConnectorLine> source)
    {
        lines.Clear();
        lines.AddRange(source ?? Array.Empty<ResearchConnectorLine>());
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (ResearchConnectorLine line in lines)
        {
            for (int index = 0; index + 1 < line.Points.Count; index++)
            {
                Vector2 from = ToCanvasPoint(line.Points[index]);
                Vector2 to = ToCanvasPoint(line.Points[index + 1]);
                if (line.Dotted)
                {
                    AddDottedSegment(vh, from, to, 3f, 10f, 7f, line.Color);
                }
                else
                {
                    AddSegment(vh, from, to, 4f, line.Color);
                }
            }
        }
    }

    private static Vector2 ToCanvasPoint(Vector2 layoutPoint)
    {
        return new Vector2(layoutPoint.x, -layoutPoint.y);
    }

    private static void AddDottedSegment(
        VertexHelper vh,
        Vector2 from,
        Vector2 to,
        float width,
        float dash,
        float gap,
        Color color)
    {
        float length = Vector2.Distance(from, to);
        if (length <= 0.01f)
        {
            return;
        }
        Vector2 direction = (to - from) / length;
        for (float cursor = 0f; cursor < length; cursor += dash + gap)
        {
            Vector2 dashStart = from + direction * cursor;
            Vector2 dashEnd = from + direction * Mathf.Min(length, cursor + dash);
            AddSegment(vh, dashStart, dashEnd, width, color);
        }
    }

    private static void AddSegment(
        VertexHelper vh,
        Vector2 from,
        Vector2 to,
        float width,
        Color color)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }
        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
        int start = vh.currentVertCount;
        vh.AddVert(from - normal, color, Vector2.zero);
        vh.AddVert(from + normal, color, Vector2.zero);
        vh.AddVert(to + normal, color, Vector2.zero);
        vh.AddVert(to - normal, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
