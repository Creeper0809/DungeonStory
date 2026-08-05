using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ResearchGraphEdge
{
    public ResearchGraphEdge(
        ResearchProjectId from,
        ResearchProjectId to,
        IReadOnlyList<Vector2> points,
        bool shortcut)
    {
        From = from;
        To = to;
        Points = points ?? Array.Empty<Vector2>();
        IsShortcut = shortcut;
    }

    public ResearchProjectId From { get; }
    public ResearchProjectId To { get; }
    public IReadOnlyList<Vector2> Points { get; }
    public bool IsShortcut { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchGraphLayout
{
    public ResearchGraphLayout(
        IReadOnlyDictionary<string, Rect> nodeRects,
        IReadOnlyList<ResearchGraphEdge> edges,
        Rect bounds,
        int structureHash)
    {
        NodeRects = nodeRects
            ?? throw new ArgumentNullException(nameof(nodeRects));
        Edges = edges ?? Array.Empty<ResearchGraphEdge>();
        Bounds = bounds;
        StructureHash = structureHash;
    }

    public IReadOnlyDictionary<string, Rect> NodeRects { get; }
    public IReadOnlyList<ResearchGraphEdge> Edges { get; }
    public Rect Bounds { get; }
    public int StructureHash { get; }
}

public interface IResearchGraphLayoutService
{
    ResearchGraphLayout Build(IReadOnlyList<IResearchProjectDefinition> projects);
    void ClearCache();
}
