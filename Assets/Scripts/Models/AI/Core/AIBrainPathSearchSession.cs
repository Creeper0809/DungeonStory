using System;
using UnityEngine;

public sealed class AIBrainPathSearchSession
{
    private readonly IGridPathSearchBroker broker;
    private GridPathSearchResult cache;
    private bool urgentRequested;

    public AIBrainPathSearchSession(IGridPathSearchBroker broker)
    {
        this.broker = broker ?? throw new ArgumentNullException(nameof(broker));
    }

    public bool IsDeferred { get; private set; }

    public GridPathSearchResult Get(
        Grid grid,
        Vector2Int start,
        GridTraversalContext traversalContext)
    {
        IsDeferred = false;
        if (grid == null)
        {
            return null;
        }

        if (cache == null
            || cache.sourceGrid != grid
            || cache.start != start
            || cache.traversalVersion != grid.TraversalVersion)
        {
            GridPathSearchPriority priority = urgentRequested
                ? GridPathSearchPriority.Urgent
                : GridPathSearchPriority.Normal;
            if (!broker.TryGetSearch(
                grid,
                start,
                out cache,
                priority,
                traversalContext))
            {
                IsDeferred = true;
                return null;
            }
        }

        urgentRequested = false;
        return cache;
    }

    public void Clear()
    {
        cache = null;
        IsDeferred = false;
    }

    public void RequestUrgent()
    {
        urgentRequested = true;
        IsDeferred = false;
    }
}
