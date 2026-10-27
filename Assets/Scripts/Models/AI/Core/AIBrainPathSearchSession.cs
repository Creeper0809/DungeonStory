using System;
using UnityEngine;

public sealed class AIBrainPathSearchSession
{
    private readonly IGridPathSearchBroker broker;
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

        GridPathSearchPriority priority = urgentRequested
            ? GridPathSearchPriority.Urgent
            : GridPathSearchPriority.Normal;
        if (!broker.TryGetSearch(
            grid,
            start,
            out GridPathSearchResult result,
            priority,
            traversalContext))
        {
            IsDeferred = true;
            return null;
        }

        urgentRequested = false;
        return result;
    }

    public void Clear()
    {
        IsDeferred = false;
    }

    public void RequestUrgent()
    {
        urgentRequested = true;
        IsDeferred = false;
    }
}
