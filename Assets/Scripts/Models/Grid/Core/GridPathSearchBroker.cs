using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public enum GridPathSearchPriority
{
    Normal,
    Urgent
}

public interface IGridPathSearchBroker
{
    int SearchesThisFrame { get; }
    int CacheHitsThisFrame { get; }
    int BudgetDeferralsThisFrame { get; }

    void BeginFrame(int searchBudget, bool enforceBudget);
    bool TryGetSearch(
        Grid grid,
        Vector2Int start,
        out GridPathSearchResult result,
        GridPathSearchPriority priority = GridPathSearchPriority.Normal,
        GridTraversalContext traversalContext = default);
    Queue<GridMoveStep> GetMovePath(
        Grid grid,
        Vector2Int start,
        Func<Vector2Int, bool> terminateEndCondition,
        GridPathSearchPriority priority = GridPathSearchPriority.Normal,
        GridTraversalContext traversalContext = default);
    void Clear();
}

public sealed class GridPathSearchBroker : IGridPathSearchBroker
{
    private readonly struct PathKey : IEquatable<PathKey>
    {
        private readonly int gridHash;
        private readonly int gridVersion;
        private readonly Vector2Int start;
        private readonly int traversalContextHash;
        private readonly int doorAccessVersion;

        public PathKey(
            Grid grid,
            Vector2Int start,
            GridTraversalContext traversalContext,
            int doorAccessVersion)
        {
            gridHash = grid != null ? grid.GetHashCode() : 0;
            gridVersion = grid != null ? grid.version : -1;
            this.start = start;
            traversalContextHash = traversalContext.HasSubject
                ? traversalContext.GetHashCode()
                : 0;
            this.doorAccessVersion = traversalContext.HasSubject
                ? doorAccessVersion
                : 0;
        }

        public bool Equals(PathKey other)
        {
            return gridHash == other.gridHash
                && gridVersion == other.gridVersion
                && start == other.start
                && traversalContextHash == other.traversalContextHash
                && doorAccessVersion == other.doorAccessVersion;
        }

        public override bool Equals(object obj)
        {
            return obj is PathKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = gridHash;
                hash = (hash * 397) ^ gridVersion;
                hash = (hash * 397) ^ start.GetHashCode();
                hash = (hash * 397) ^ traversalContextHash;
                hash = (hash * 397) ^ doorAccessVersion;
                return hash;
            }
        }
    }

    private const int DefaultSearchBudget = 8;
    private readonly IGameClock gameClock;
    private readonly IDoorAccessQuery doorAccessQuery;
    private readonly Dictionary<PathKey, GridPathSearchResult> frameCache =
        new Dictionary<PathKey, GridPathSearchResult>();

    private int cacheFrame = -1;
    private int searchBudget = DefaultSearchBudget;
    private bool enforceBudget = true;

    public GridPathSearchBroker(
        IGameClock gameClock,
        IDoorAccessQuery doorAccessQuery = null)
    {
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.doorAccessQuery = doorAccessQuery;
    }

    public int SearchesThisFrame { get; private set; }
    public int CacheHitsThisFrame { get; private set; }
    public int BudgetDeferralsThisFrame { get; private set; }

    public void BeginFrame(int searchBudget, bool enforceBudget)
    {
        BeginFrameIfNeeded();
        this.searchBudget = Mathf.Max(1, searchBudget);
        this.enforceBudget = enforceBudget;
    }

    public bool TryGetSearch(
        Grid grid,
        Vector2Int start,
        out GridPathSearchResult result,
        GridPathSearchPriority priority = GridPathSearchPriority.Normal,
        GridTraversalContext traversalContext = default)
    {
        BeginFrameIfNeeded();
        result = null;
        if (grid == null)
        {
            return false;
        }

        int doorAccessVersion = traversalContext.HasSubject
            ? doorAccessQuery?.DoorAccessVersion ?? 0
            : 0;
        PathKey key = new PathKey(
            grid,
            start,
            traversalContext,
            doorAccessVersion);
        if (frameCache.TryGetValue(key, out result)
            && result != null
            && result.sourceGrid == grid
            && result.start == start
            && result.gridVersion == grid.version)
        {
            CacheHitsThisFrame++;
            return true;
        }

        if (priority != GridPathSearchPriority.Urgent
            && enforceBudget
            && SearchesThisFrame >= searchBudget)
        {
            BudgetDeferralsThisFrame++;
            result = null;
            return false;
        }

        result = traversalContext.HasSubject && doorAccessQuery != null
            ? grid.SearchPathWithTraversalFilter(
                start,
                position => doorAccessQuery.CanTraverse(
                    grid,
                    position,
                    traversalContext,
                    out _))
            : grid.SearchPath(start);
        frameCache[key] = result;
        SearchesThisFrame++;
        return true;
    }

    public Queue<GridMoveStep> GetMovePath(
        Grid grid,
        Vector2Int start,
        Func<Vector2Int, bool> terminateEndCondition,
        GridPathSearchPriority priority = GridPathSearchPriority.Normal,
        GridTraversalContext traversalContext = default)
    {
        if (terminateEndCondition == null)
        {
            throw new ArgumentNullException(nameof(terminateEndCondition));
        }

        return TryGetSearch(
                grid,
                start,
                out GridPathSearchResult search,
                priority,
                traversalContext)
            ? search.GetMovePath(terminateEndCondition)
            : null;
    }

    public void Clear()
    {
        frameCache.Clear();
        cacheFrame = -1;
        SearchesThisFrame = 0;
        CacheHitsThisFrame = 0;
        BudgetDeferralsThisFrame = 0;
    }

    private void BeginFrameIfNeeded()
    {
        int frame = gameClock.FrameCount;
        if (cacheFrame == frame)
        {
            return;
        }

        cacheFrame = frame;
        frameCache.Clear();
        SearchesThisFrame = 0;
        CacheHitsThisFrame = 0;
        BudgetDeferralsThisFrame = 0;
    }
}
