using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

public sealed class ResearchGraphLayout
{
    public ResearchGraphLayout(
        IReadOnlyDictionary<string, Rect> nodeRects,
        IReadOnlyList<ResearchGraphEdge> edges,
        Rect bounds,
        int structureHash)
    {
        NodeRects = nodeRects;
        Edges = edges;
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
    ResearchGraphLayout Build(IReadOnlyList<ResearchProjectSO> projects);
    void ClearCache();
}

public sealed class ResearchGraphLayoutService : IResearchGraphLayoutService
{
    public static readonly Vector2 NodeSize = new Vector2(224f, 112f);
    private const float LayerGap = 120f;
    private const float RowGap = 44f;
    private const float Margin = 72f;
    private const int SweepCount = 6;

    private ResearchGraphLayout cached;

    public ResearchGraphLayout Build(IReadOnlyList<ResearchProjectSO> projects)
    {
        ResearchProjectSO[] nodes = (projects ?? Array.Empty<ResearchProjectSO>())
            .Where(project => project != null && project.ProjectId.IsValid)
            .OrderBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .ToArray();
        int hash = ComputeStructureHash(nodes);
        if (cached != null && cached.StructureHash == hash)
        {
            return cached;
        }

        if (ResearchGraphAlgorithms.TryFindCycle(nodes, out IReadOnlyList<ResearchProjectSO> cycle))
        {
            throw new InvalidOperationException(
                $"Research graph contains a cycle: {string.Join(" -> ", cycle.Select(item => item.ProjectId.Value))}");
        }

        Dictionary<ResearchProjectSO, int> ranks = CalculateRanks(nodes);
        int longEdgeCount = nodes.Sum(target => target.Prerequisites.Count(source =>
            source != null
            && ranks.TryGetValue(source, out int sourceRank)
            && ranks[target] - sourceRank > 1));
        float topRoutingMargin = longEdgeCount > 0
            ? 28f + longEdgeCount * 10f
            : 0f;
        List<List<ResearchProjectSO>> layers = Enumerable
            .Range(0, ranks.Count == 0 ? 0 : ranks.Values.Max() + 1)
            .Select(rank => nodes.Where(node => ranks[node] == rank)
                .OrderBy(node => node.ProjectId.Value, StringComparer.Ordinal)
                .ToList())
            .ToList();
        ReduceCrossings(layers, ranks);

        Dictionary<string, Rect> rects = new Dictionary<string, Rect>(StringComparer.Ordinal);
        float maxLayerHeight = layers.Count == 0
            ? 0f
            : layers.Max(layer => layer.Count * NodeSize.y + Mathf.Max(0, layer.Count - 1) * RowGap);
        for (int rank = 0; rank < layers.Count; rank++)
        {
            List<ResearchProjectSO> layer = layers[rank];
            float height = layer.Count * NodeSize.y + Mathf.Max(0, layer.Count - 1) * RowGap;
            float y = Margin + topRoutingMargin + (maxLayerHeight - height) * 0.5f;
            float x = Margin + rank * (NodeSize.x + LayerGap);
            foreach (ResearchProjectSO project in layer)
            {
                rects[project.ProjectId.Value] = new Rect(x, y, NodeSize.x, NodeSize.y);
                y += NodeSize.y + RowGap;
            }
        }

        List<ResearchGraphEdge> edges = new List<ResearchGraphEdge>();
        int longEdgeIndex = 0;
        foreach (ResearchProjectSO target in nodes)
        {
            foreach (ResearchProjectSO source in target.Prerequisites
                         .Where(item => item != null)
                         .OrderBy(item => item.ProjectId.Value, StringComparer.Ordinal))
            {
                if (!rects.TryGetValue(source.ProjectId.Value, out Rect sourceRect)
                    || !rects.TryGetValue(target.ProjectId.Value, out Rect targetRect))
                {
                    continue;
                }

                Vector2 start = new Vector2(sourceRect.xMax, sourceRect.center.y);
                Vector2 end = new Vector2(targetRect.xMin, targetRect.center.y);
                bool spansLayers = ranks[target] - ranks[source] > 1;
                IReadOnlyList<Vector2> points;
                if (spansLayers)
                {
                    float sourceGapX = sourceRect.xMax + LayerGap * 0.35f;
                    float targetGapX = targetRect.xMin - LayerGap * 0.35f;
                    float laneY = Margin + 12f + longEdgeIndex * 10f;
                    longEdgeIndex++;
                    points = new[]
                    {
                        start,
                        new Vector2(sourceGapX, start.y),
                        new Vector2(sourceGapX, laneY),
                        new Vector2(targetGapX, laneY),
                        new Vector2(targetGapX, end.y),
                        end
                    };
                }
                else
                {
                    float middleX = Mathf.Lerp(start.x, end.x, 0.5f);
                    points = new[]
                    {
                        start,
                        new Vector2(middleX, start.y),
                        new Vector2(middleX, end.y),
                        end
                    };
                }
                edges.Add(new ResearchGraphEdge(
                    source.ProjectId,
                    target.ProjectId,
                    points,
                    target.BlueprintRule == ResearchBlueprintRule.Shortcut));
            }
        }

        float width = layers.Count == 0
            ? Margin * 2f
            : Margin * 2f + layers.Count * NodeSize.x + Mathf.Max(0, layers.Count - 1) * LayerGap;
        float totalHeight = Margin * 2f + topRoutingMargin + maxLayerHeight;
        cached = new ResearchGraphLayout(
            rects,
            edges,
            new Rect(0f, 0f, width, totalHeight),
            hash);
        return cached;
    }

    public void ClearCache()
    {
        cached = null;
    }

    private static Dictionary<ResearchProjectSO, int> CalculateRanks(
        IReadOnlyList<ResearchProjectSO> nodes)
    {
        HashSet<ResearchProjectSO> nodeSet = nodes.ToHashSet();
        Dictionary<ResearchProjectSO, int> ranks = new Dictionary<ResearchProjectSO, int>();
        int Resolve(ResearchProjectSO node)
        {
            if (ranks.TryGetValue(node, out int known))
            {
                return known;
            }

            int rank = node.Prerequisites
                .Where(prerequisite => prerequisite != null && nodeSet.Contains(prerequisite))
                .Select(prerequisite => Resolve(prerequisite) + 1)
                .DefaultIfEmpty(0)
                .Max();
            ranks[node] = rank;
            return rank;
        }

        foreach (ResearchProjectSO node in nodes)
        {
            Resolve(node);
        }

        return ranks;
    }

    private static void ReduceCrossings(
        IList<List<ResearchProjectSO>> layers,
        IReadOnlyDictionary<ResearchProjectSO, int> ranks)
    {
        if (layers == null || layers.Count <= 1)
        {
            return;
        }

        for (int sweep = 0; sweep < SweepCount; sweep++)
        {
            bool forward = sweep % 2 == 0;
            int start = forward ? 1 : layers.Count - 2;
            int end = forward ? layers.Count : -1;
            int step = forward ? 1 : -1;
            for (int layerIndex = start; layerIndex != end; layerIndex += step)
            {
                int adjacentIndex = layerIndex - step;
                Dictionary<ResearchProjectSO, int> adjacentOrder = layers[adjacentIndex]
                    .Select((project, index) => (project, index))
                    .ToDictionary(item => item.project, item => item.index);
                layers[layerIndex] = layers[layerIndex]
                    .Select((project, originalIndex) => new
                    {
                        Project = project,
                        OriginalIndex = originalIndex,
                        Barycenter = CalculateBarycenter(
                            project,
                            forward,
                            adjacentOrder,
                            layers,
                            ranks)
                    })
                    .OrderBy(item => item.Barycenter)
                    .ThenBy(item => item.OriginalIndex)
                    .ThenBy(item => item.Project.ProjectId.Value, StringComparer.Ordinal)
                    .Select(item => item.Project)
                    .ToList();
            }
        }
    }

    private static float CalculateBarycenter(
        ResearchProjectSO project,
        bool forward,
        IReadOnlyDictionary<ResearchProjectSO, int> adjacentOrder,
        IList<List<ResearchProjectSO>> layers,
        IReadOnlyDictionary<ResearchProjectSO, int> ranks)
    {
        IEnumerable<ResearchProjectSO> neighbors = forward
            ? project.Prerequisites
            : layers.SelectMany(layer => layer)
                .Where(candidate => candidate.Prerequisites.Contains(project)
                    && ranks[candidate] == ranks[project] + 1);
        int[] indices = neighbors
            .Where(adjacentOrder.ContainsKey)
            .Select(neighbor => adjacentOrder[neighbor])
            .ToArray();
        return indices.Length == 0 ? float.MaxValue : (float)indices.Average();
    }

    private static int ComputeStructureHash(IEnumerable<ResearchProjectSO> projects)
    {
        unchecked
        {
            int hash = 17;
            foreach (ResearchProjectSO project in projects)
            {
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(project.ProjectId.Value);
                hash = hash * 31 + (int)project.BlueprintRule;
                foreach (ResearchProjectSO prerequisite in project.Prerequisites
                             .Where(item => item != null)
                             .OrderBy(item => item.ProjectId.Value, StringComparer.Ordinal))
                {
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(prerequisite.ProjectId.Value);
                }
            }
            return hash;
        }
    }
}
