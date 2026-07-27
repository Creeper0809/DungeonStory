using System;
using System.Collections.Generic;
using System.Diagnostics;
using DungeonStory.Foundation;
using UnityEngine;

public enum GridPathSearchPriority
{
    Normal,
    Urgent
}

public enum GridPathRequestStatus
{
    Pending,
    Reachable,
    Unreachable
}

public interface IGridPathSearchBroker
{
    int SearchesThisFrame { get; }
    int UnboundedSearchesThisFrame { get; }
    int CacheHitsThisFrame { get; }
    int BudgetDeferralsThisFrame { get; }
    double SearchMillisecondsThisFrame { get; }

    void BeginFrame(
        int searchBudget,
        bool enforceBudget,
        double searchTimeBudgetMilliseconds = double.PositiveInfinity);
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
    Queue<GridMoveStep> GetMovePathTo(
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        GridPathSearchPriority priority = GridPathSearchPriority.Normal,
        GridTraversalContext traversalContext = default);
    GridPathRequestStatus RequestMovePathTo(
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        out Queue<GridMoveStep> path,
        GridPathSearchPriority priority = GridPathSearchPriority.Normal,
        GridTraversalContext traversalContext = default);
    void Clear();
}

public sealed class GridPathSearchBroker : IGridPathSearchBroker
{
    private sealed class CacheEntry
    {
        public GridPathSearchResult Result;
        public int LastAccessFrame;
    }

    private sealed class IncrementalExactSearch
    {
        private sealed class NodeRecord
        {
            public int Cost;
            public int ParentIndex = -1;
            public IGridOccupant MovementOccupant;
            public GridMoveType MoveType;
        }

        private readonly Grid grid;
        private readonly Vector2Int start;
        private readonly Vector2Int destination;
        private readonly GridTraversalContext traversalContext;
        private readonly Func<Vector2Int, bool> traversalFilter;
        private readonly IGridTraversalCostPolicy costPolicy;
        private readonly GridSearchPriorityQueue queue =
            new GridSearchPriorityQueue();
        private readonly Dictionary<int, NodeRecord> records =
            new Dictionary<int, NodeRecord>(256);
        private readonly List<GridTraversalStepData> nextSteps =
            new List<GridTraversalStepData>(8);
        private readonly int startIndex;
        private int sequence;
        private int expandedNodeCount;
        private bool completed;
        private int destinationCost = int.MaxValue;

        public IncrementalExactSearch(
            Grid grid,
            Vector2Int start,
            Vector2Int destination,
            GridTraversalContext traversalContext,
            Func<Vector2Int, bool> traversalFilter,
            IGridTraversalCostPolicy costPolicy,
            int accessFrame)
        {
            this.grid = grid;
            this.start = start;
            this.destination = destination;
            this.traversalContext = traversalContext;
            this.traversalFilter = traversalFilter;
            this.costPolicy =
                costPolicy ?? DefaultGridTraversalCostPolicy.Instance;
            TraversalVersion = grid != null ? grid.TraversalVersion : -1;
            LastAccessFrame = accessFrame;

            if (grid == null
                || !grid.TryGetCellIndex(start, out int resolvedStartIndex)
                || !grid.IsValidGridPos(destination))
            {
                startIndex = -1;
                completed = true;
                return;
            }

            startIndex = resolvedStartIndex;
            records[startIndex] = new NodeRecord { Cost = 0 };
            queue.Enqueue(new GridSearchQueueNode(
                startIndex,
                0,
                grid.EstimatePathHeuristicCost(
                    start,
                    destination,
                    this.costPolicy),
                sequence++));
        }

        public int TraversalVersion { get; }
        public int LastAccessFrame { get; set; }
        public bool IsComplete => completed;

        public int Advance(double budgetMilliseconds)
        {
            if (completed)
            {
                return 0;
            }

            long started = Stopwatch.GetTimestamp();
            int expandedThisSlice = 0;
            double budget = Math.Max(0.02, budgetMilliseconds);
            while (queue.Count > 0)
            {
                GridSearchQueueNode current = queue.Dequeue();
                if (!records.TryGetValue(
                        current.CellIndex,
                        out NodeRecord currentRecord)
                    || current.Cost != currentRecord.Cost)
                {
                    continue;
                }

                Vector2Int position =
                    grid.GetPositionFromCellIndex(current.CellIndex);
                expandedNodeCount++;
                expandedThisSlice++;
                if (position == destination)
                {
                    destinationCost = current.Cost;
                    completed = true;
                    break;
                }

                GridCell cell = grid.GetGridCell(position);
                if (cell != null)
                {
                    nextSteps.Clear();
                    IReadOnlyList<GridTraversalLink> links =
                        cell.TraversalLinks;
                    for (int index = 0; index < links.Count; index++)
                    {
                        GridTraversalLink link = links[index];
                        nextSteps.Add(new GridTraversalStepData(
                            position,
                            link.To,
                            link.Through,
                            link.MoveType));
                    }

                    nextSteps.Add(new GridTraversalStepData(
                        position,
                        position + Vector2Int.left,
                        null,
                        GridMoveType.Walk));
                    nextSteps.Add(new GridTraversalStepData(
                        position,
                        position + Vector2Int.right,
                        null,
                        GridMoveType.Walk));
                    for (int index = 0; index < nextSteps.Count; index++)
                    {
                        GridTraversalStepData step = nextSteps[index];
                        ExpandStep(
                            current.CellIndex,
                            current.Cost,
                            in step);
                    }
                }

                if (expandedThisSlice >= 16
                    && ElapsedMilliseconds(started) >= budget)
                {
                    break;
                }
            }

            if (queue.Count == 0)
            {
                completed = true;
            }

            return expandedThisSlice;
        }

        public GridPathSearchResult CreateResult()
        {
            if (!completed)
            {
                return null;
            }

            GridMoveStep[] path = BuildPath();
            return new GridPathSearchResult(
                grid,
                start,
                TraversalVersion,
                destination,
                path,
                destinationCost,
                expandedNodeCount);
        }

        private void ExpandStep(
            int currentIndex,
            int currentCost,
            in GridTraversalStepData step)
        {
            Vector2Int nextPosition = step.To;
            GridCell nextCell = grid.GetGridCell(nextPosition);
            bool passesFilter =
                traversalFilter == null || traversalFilter(nextPosition);
            bool allowedTerminal = nextPosition == destination
                && !grid.IsMovementBlockedByWall(nextPosition)
                && passesFilter;
            if (nextCell == null
                || !passesFilter
                || (!grid.IsWalkable(nextPosition) && !allowedTerminal)
                || !grid.TryGetCellIndex(nextPosition, out int nextIndex))
            {
                return;
            }

            int stepCost = costPolicy.GetTraversalCost(
                grid,
                in step,
                traversalContext);
            if (stepCost == int.MaxValue
                || currentCost > int.MaxValue - stepCost)
            {
                return;
            }

            int candidateCost = currentCost + stepCost;
            if (records.TryGetValue(nextIndex, out NodeRecord known)
                && candidateCost >= known.Cost)
            {
                return;
            }

            NodeRecord record = known ?? new NodeRecord();
            record.Cost = candidateCost;
            record.ParentIndex = currentIndex;
            record.MovementOccupant = step.MovementOccupant;
            record.MoveType = step.MoveType;
            records[nextIndex] = record;
            int priority = candidateCost
                + grid.EstimatePathHeuristicCost(
                    nextPosition,
                    destination,
                    costPolicy);
            queue.Enqueue(new GridSearchQueueNode(
                nextIndex,
                candidateCost,
                priority,
                sequence++));
        }

        private GridMoveStep[] BuildPath()
        {
            if (destinationCost == int.MaxValue
                || !grid.TryGetCellIndex(
                    destination,
                    out int destinationIndex))
            {
                return Array.Empty<GridMoveStep>();
            }

            if (destinationIndex == startIndex)
            {
                return Array.Empty<GridMoveStep>();
            }

            List<int> reversed = new List<int>();
            int currentIndex = destinationIndex;
            int guard = records.Count + 1;
            while (currentIndex != startIndex && guard-- > 0)
            {
                reversed.Add(currentIndex);
                if (!records.TryGetValue(
                        currentIndex,
                        out NodeRecord record)
                    || record.ParentIndex < 0)
                {
                    destinationCost = int.MaxValue;
                    return Array.Empty<GridMoveStep>();
                }

                currentIndex = record.ParentIndex;
            }

            if (currentIndex != startIndex)
            {
                destinationCost = int.MaxValue;
                return Array.Empty<GridMoveStep>();
            }

            GridMoveStep[] path = new GridMoveStep[reversed.Count];
            for (int index = reversed.Count - 1, pathIndex = 0;
                index >= 0;
                index--, pathIndex++)
            {
                int toIndex = reversed[index];
                NodeRecord record = records[toIndex];
                Vector2Int from =
                    grid.GetPositionFromCellIndex(record.ParentIndex);
                Vector2Int to = grid.GetPositionFromCellIndex(toIndex);
                path[pathIndex] = new GridMoveStep(
                    from,
                    to,
                    grid.GetGridCell(to)?.GetTopOccupant(),
                    record.MovementOccupant,
                    record.MoveType);
            }

            return path;
        }

        private static double ElapsedMilliseconds(long started)
        {
            return (Stopwatch.GetTimestamp() - started)
                * 1000.0
                / Stopwatch.Frequency;
        }
    }

    private readonly struct PathKey : IEquatable<PathKey>
    {
        private readonly Grid grid;
        private readonly int gridVersion;
        private readonly Vector2Int start;
        private readonly GridTraversalContext traversalContext;
        private readonly int doorAccessVersion;
        private readonly int costPolicyVersion;
        private readonly Vector2Int destination;
        private readonly bool hasDestination;

        public PathKey(
            Grid grid,
            Vector2Int start,
            GridTraversalContext traversalContext,
            int doorAccessVersion,
            int costPolicyVersion,
            Vector2Int? destination)
        {
            this.grid = grid;
            gridVersion = grid != null ? grid.TraversalVersion : -1;
            this.start = start;
            this.traversalContext = traversalContext.HasSubject
                ? traversalContext
                : default;
            this.doorAccessVersion = traversalContext.HasSubject
                ? doorAccessVersion
                : 0;
            this.costPolicyVersion = costPolicyVersion;
            this.destination = destination ?? default;
            hasDestination = destination.HasValue;
        }

        public bool Equals(PathKey other)
        {
            return ReferenceEquals(grid, other.grid)
                && gridVersion == other.gridVersion
                && start == other.start
                && traversalContext.Equals(other.traversalContext)
                && doorAccessVersion == other.doorAccessVersion
                && costPolicyVersion == other.costPolicyVersion
                && destination == other.destination
                && hasDestination == other.hasDestination;
        }

        public override bool Equals(object obj)
        {
            return obj is PathKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = grid != null ? grid.GetHashCode() : 0;
                hash = (hash * 397) ^ gridVersion;
                hash = (hash * 397) ^ start.GetHashCode();
                hash = (hash * 397) ^ traversalContext.GetHashCode();
                hash = (hash * 397) ^ doorAccessVersion;
                hash = (hash * 397) ^ costPolicyVersion;
                hash = (hash * 397) ^ destination.GetHashCode();
                hash = (hash * 397) ^ (hasDestination ? 1 : 0);
                return hash;
            }
        }
    }

    private const int DefaultSearchBudget = 8;
    private const int MaxUrgentSearchOverdraft = 4;
    private const int MaxCacheEntries = 512;
    private const int MaxCacheAgeFrames = 120;
    private const int CachePruneIntervalFrames = 30;
    private readonly IGameClock gameClock;
    private readonly IDoorAccessQuery doorAccessQuery;
    private readonly ICharacterAiPerformanceRecorder performanceRecorder;
    private readonly IGridTraversalCostPolicy costPolicy;
    private readonly Dictionary<PathKey, CacheEntry> pathCache =
        new Dictionary<PathKey, CacheEntry>();
    private readonly Dictionary<PathKey, IncrementalExactSearch>
        incrementalExactSearches =
            new Dictionary<PathKey, IncrementalExactSearch>();
    private readonly HashSet<PathKey> deferredKeys = new HashSet<PathKey>();
    private readonly List<PathKey> staleKeys = new List<PathKey>();

    private int cacheFrame = -1;
    private int nextPruneFrame;
    private int searchBudget = DefaultSearchBudget;
    private bool enforceBudget = true;
    private double searchTimeBudgetMilliseconds = double.PositiveInfinity;
    private double searchMillisecondsThisFrame;

    public GridPathSearchBroker(
        IGameClock gameClock,
        IDoorAccessQuery doorAccessQuery = null,
        ICharacterAiPerformanceRecorder performanceRecorder = null,
        IGridTraversalCostPolicy costPolicy = null)
    {
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.doorAccessQuery = doorAccessQuery;
        this.performanceRecorder = performanceRecorder;
        this.costPolicy = costPolicy ?? DefaultGridTraversalCostPolicy.Instance;
    }

    public int SearchesThisFrame { get; private set; }
    public int UnboundedSearchesThisFrame { get; private set; }
    public int CacheHitsThisFrame { get; private set; }
    public int BudgetDeferralsThisFrame { get; private set; }
    public double SearchMillisecondsThisFrame => searchMillisecondsThisFrame;

    public void BeginFrame(
        int searchBudget,
        bool enforceBudget,
        double searchTimeBudgetMilliseconds = double.PositiveInfinity)
    {
        ResetWindow(gameClock.FrameCount);
        this.searchBudget = Mathf.Max(0, searchBudget);
        this.enforceBudget = enforceBudget;
        this.searchTimeBudgetMilliseconds =
            double.IsNaN(searchTimeBudgetMilliseconds)
            || searchTimeBudgetMilliseconds <= 0.0
                ? double.PositiveInfinity
                : searchTimeBudgetMilliseconds;
    }

    public bool TryGetSearch(
        Grid grid,
        Vector2Int start,
        out GridPathSearchResult result,
        GridPathSearchPriority priority = GridPathSearchPriority.Normal,
        GridTraversalContext traversalContext = default)
    {
        return TryGetSearchInternal(
            grid,
            start,
            null,
            out result,
            priority,
            traversalContext);
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

    public Queue<GridMoveStep> GetMovePathTo(
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        GridPathSearchPriority priority = GridPathSearchPriority.Normal,
        GridTraversalContext traversalContext = default)
    {
        GridPathRequestStatus status = RequestMovePathTo(
            grid,
            start,
            destination,
            out Queue<GridMoveStep> path,
            priority,
            traversalContext);
        return status == GridPathRequestStatus.Pending ? null : path;
    }

    public GridPathRequestStatus RequestMovePathTo(
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        out Queue<GridMoveStep> path,
        GridPathSearchPriority priority = GridPathSearchPriority.Normal,
        GridTraversalContext traversalContext = default)
    {
        path = null;
        if (grid == null)
        {
            return GridPathRequestStatus.Unreachable;
        }

        if (!TryGetSearchInternal(
                grid,
                start,
                destination,
                out GridPathSearchResult search,
                priority,
                traversalContext))
        {
            return GridPathRequestStatus.Pending;
        }

        if (search == null
            || search.GetMoveCostTo(destination) == int.MaxValue)
        {
            path = new Queue<GridMoveStep>();
            return GridPathRequestStatus.Unreachable;
        }

        path = search.GetMovePathTo(destination);
        return GridPathRequestStatus.Reachable;
    }

    public void Clear()
    {
        pathCache.Clear();
        incrementalExactSearches.Clear();
        deferredKeys.Clear();
        staleKeys.Clear();
        cacheFrame = -1;
        nextPruneFrame = 0;
        SearchesThisFrame = 0;
        UnboundedSearchesThisFrame = 0;
        CacheHitsThisFrame = 0;
        BudgetDeferralsThisFrame = 0;
        searchTimeBudgetMilliseconds = double.PositiveInfinity;
        searchMillisecondsThisFrame = 0.0;
    }

    private void BeginFrameIfNeeded()
    {
        int frame = gameClock.FrameCount;
        if (cacheFrame == frame)
        {
            return;
        }

        ResetWindow(frame);
    }

    private void ResetWindow(int frame)
    {
        cacheFrame = frame;
        deferredKeys.Clear();
        SearchesThisFrame = 0;
        UnboundedSearchesThisFrame = 0;
        CacheHitsThisFrame = 0;
        BudgetDeferralsThisFrame = 0;
        searchMillisecondsThisFrame = 0.0;
        PruneCache(frame);
    }

    private bool TryGetSearchInternal(
        Grid grid,
        Vector2Int start,
        Vector2Int? destination,
        out GridPathSearchResult result,
        GridPathSearchPriority priority,
        GridTraversalContext traversalContext)
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
            doorAccessVersion,
            costPolicy.Version,
            destination);
        if (pathCache.TryGetValue(key, out CacheEntry entry)
            && IsValidCachedResult(entry, grid, start))
        {
            entry.LastAccessFrame = cacheFrame;
            result = entry.Result;
            CacheHitsThisFrame++;
            return true;
        }

        if (entry != null)
        {
            pathCache.Remove(key);
        }

        if (deferredKeys.Contains(key))
        {
            return false;
        }

        int hardSearchLimit = searchBudget + MaxUrgentSearchOverdraft;
        bool normalBudgetExhausted =
            priority != GridPathSearchPriority.Urgent
            && SearchesThisFrame >= searchBudget;
        bool hardBudgetExhausted = SearchesThisFrame >= hardSearchLimit;
        bool timeBudgetExhausted = SearchesThisFrame > 0
            && searchMillisecondsThisFrame >= searchTimeBudgetMilliseconds;
        if (enforceBudget
            && (normalBudgetExhausted
                || hardBudgetExhausted
                || timeBudgetExhausted))
        {
            BudgetDeferralsThisFrame++;
            deferredKeys.Add(key);
            return false;
        }

        Func<Vector2Int, bool> traversalFilter = traversalContext.HasSubject
            && doorAccessQuery != null
            ? position => doorAccessQuery.CanTraverse(
                grid,
                position,
                traversalContext,
                out _)
            : null;
        bool recordSearch = performanceRecorder?.DetailedCollectionEnabled == true;
        long searchStarted = Stopwatch.GetTimestamp();
        if (destination.HasValue)
        {
            if (!incrementalExactSearches.TryGetValue(
                    key,
                    out IncrementalExactSearch incremental)
                || incremental.TraversalVersion != grid.TraversalVersion)
            {
                incremental = new IncrementalExactSearch(
                    grid,
                    start,
                    destination.Value,
                    traversalContext,
                    traversalFilter,
                    costPolicy,
                    cacheFrame);
                incrementalExactSearches[key] = incremental;
            }

            incremental.LastAccessFrame = cacheFrame;
            incremental.Advance(ResolveExactSearchSliceMilliseconds(priority));
            result = incremental.CreateResult();
        }
        else
        {
            result = grid.SearchPathWeighted(
                start,
                traversalFilter,
                costPolicy,
                traversalContext);
        }

        double elapsedSearchMilliseconds =
            (Stopwatch.GetTimestamp() - searchStarted)
            * 1000.0
            / Stopwatch.Frequency;
        searchMillisecondsThisFrame += elapsedSearchMilliseconds;
        if (recordSearch)
        {
            performanceRecorder.Record(
                AiPerformanceCategory.PathSearch,
                elapsedSearchMilliseconds);
        }

        SearchesThisFrame++;
        if (destination.HasValue && result == null)
        {
            BudgetDeferralsThisFrame++;
            return false;
        }

        if (destination.HasValue)
        {
            incrementalExactSearches.Remove(key);
        }

        pathCache[key] = new CacheEntry
        {
            Result = result,
            LastAccessFrame = cacheFrame
        };
        TrimCacheToLimit();
        if (!destination.HasValue)
        {
            UnboundedSearchesThisFrame++;
        }

        return true;
    }

    private double ResolveExactSearchSliceMilliseconds(
        GridPathSearchPriority priority)
    {
        double remaining = double.IsPositiveInfinity(
                searchTimeBudgetMilliseconds)
            ? 0.45
            : Math.Max(
                0.02,
                searchTimeBudgetMilliseconds
                    - searchMillisecondsThisFrame);
        double maximum = priority == GridPathSearchPriority.Urgent
            ? 0.85
            : 0.55;
        return Math.Clamp(remaining, 0.02, maximum);
    }

    private static bool IsValidCachedResult(
        CacheEntry entry,
        Grid grid,
        Vector2Int start)
    {
        GridPathSearchResult result = entry?.Result;
        return result != null
            && result.sourceGrid == grid
            && result.start == start
            && result.traversalVersion == grid.TraversalVersion;
    }

    private void PruneCache(int frame)
    {
        if (frame < nextPruneFrame && pathCache.Count <= MaxCacheEntries)
        {
            return;
        }

        nextPruneFrame = frame + CachePruneIntervalFrames;
        staleKeys.Clear();
        foreach (KeyValuePair<PathKey, CacheEntry> pair in pathCache)
        {
            CacheEntry entry = pair.Value;
            if (entry?.Result == null
                || frame - entry.LastAccessFrame > MaxCacheAgeFrames)
            {
                staleKeys.Add(pair.Key);
            }
        }

        foreach (PathKey key in staleKeys)
        {
            pathCache.Remove(key);
        }

        staleKeys.Clear();
        foreach (KeyValuePair<PathKey, IncrementalExactSearch> pair
                 in incrementalExactSearches)
        {
            IncrementalExactSearch search = pair.Value;
            if (search == null
                || frame - search.LastAccessFrame > MaxCacheAgeFrames)
            {
                staleKeys.Add(pair.Key);
            }
        }

        foreach (PathKey key in staleKeys)
        {
            incrementalExactSearches.Remove(key);
        }

        staleKeys.Clear();
        TrimCacheToLimit();
    }

    private void TrimCacheToLimit()
    {
        while (pathCache.Count > MaxCacheEntries)
        {
            bool found = false;
            PathKey oldestKey = default;
            int oldestFrame = int.MaxValue;
            foreach (KeyValuePair<PathKey, CacheEntry> pair in pathCache)
            {
                int accessFrame = pair.Value?.LastAccessFrame ?? int.MinValue;
                if (found && accessFrame >= oldestFrame)
                {
                    continue;
                }

                found = true;
                oldestKey = pair.Key;
                oldestFrame = accessFrame;
            }

            if (!found)
            {
                break;
            }

            pathCache.Remove(oldestKey);
        }
    }
}
