using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public class GridPathSearchResult
{
    public Grid sourceGrid { get; private set; }
    public Vector2Int start { get; private set; }
    public int gridVersion { get; private set; }
    public int traversalVersion { get; private set; }
    public int ExpandedNodeCount { get; private set; }

    private readonly int[] parentIndex;
    private readonly IGridOccupant[] parentMovementOccupant;
    private readonly GridMoveType[] parentMoveType;
    private readonly int[] searchOrder;
    private readonly int searchOrderCount;
    private readonly int[] moveCost;
    private readonly List<IGridOccupant> visitableOccupants;
    private readonly Vector2Int? exactDestination;
    private readonly GridMoveStep[] exactPath;
    private readonly int exactMoveCost;
    private Queue<GridMoveStep> ownedExactPath;
    private HashSet<IGridOccupant> visitableOccupantSet;
    private Dictionary<IGridOccupant, Vector2Int> visitableOccupantPositions;
    private Dictionary<IGridOccupant, int> visitableOccupantMoveCosts;
    private bool visitableOccupantPositionsBuilt;

    internal GridPathSearchResult(
        Grid sourceGrid,
        Vector2Int start,
        int gridVersion,
        int[] parentIndex,
        IGridOccupant[] parentMovementOccupant,
        GridMoveType[] parentMoveType,
        int[] searchOrder,
        int searchOrderCount,
        int[] moveCost,
        List<IGridOccupant> visitableOccupants)
    {
        this.sourceGrid = sourceGrid;
        this.start = start;
        this.gridVersion = gridVersion;
        traversalVersion = gridVersion;
        this.parentIndex = parentIndex;
        this.parentMovementOccupant = parentMovementOccupant;
        this.parentMoveType = parentMoveType;
        this.searchOrder = searchOrder;
        this.searchOrderCount = searchOrderCount;
        ExpandedNodeCount = searchOrderCount;
        this.moveCost = moveCost;
        this.visitableOccupants = visitableOccupants;
        exactDestination = null;
        exactPath = null;
        exactMoveCost = int.MaxValue;
        ownedExactPath = null;
    }

    internal GridPathSearchResult(
        Grid sourceGrid,
        Vector2Int start,
        int gridVersion,
        Vector2Int destination,
        GridMoveStep[] path,
        int moveCost,
        int expandedNodeCount = 0)
    {
        this.sourceGrid = sourceGrid;
        this.start = start;
        this.gridVersion = gridVersion;
        traversalVersion = gridVersion;
        parentIndex = Array.Empty<int>();
        parentMovementOccupant = Array.Empty<IGridOccupant>();
        parentMoveType = Array.Empty<GridMoveType>();
        searchOrder = Array.Empty<int>();
        searchOrderCount = 0;
        this.moveCost = Array.Empty<int>();
        visitableOccupants = new List<IGridOccupant>(0);
        exactDestination = destination;
        exactPath = path ?? Array.Empty<GridMoveStep>();
        exactMoveCost = moveCost;
        ownedExactPath = null;
        ExpandedNodeCount = Mathf.Max(0, expandedNodeCount);
    }

    internal GridPathSearchResult(
        Grid sourceGrid,
        Vector2Int start,
        int gridVersion,
        Vector2Int destination,
        Queue<GridMoveStep> path,
        int moveCost,
        int expandedNodeCount = 0)
    {
        this.sourceGrid = sourceGrid;
        this.start = start;
        this.gridVersion = gridVersion;
        traversalVersion = gridVersion;
        parentIndex = Array.Empty<int>();
        parentMovementOccupant = Array.Empty<IGridOccupant>();
        parentMoveType = Array.Empty<GridMoveType>();
        searchOrder = Array.Empty<int>();
        searchOrderCount = 0;
        this.moveCost = Array.Empty<int>();
        visitableOccupants = new List<IGridOccupant>(0);
        exactDestination = destination;
        exactPath = Array.Empty<GridMoveStep>();
        exactMoveCost = moveCost;
        ownedExactPath = path;
        ExpandedNodeCount = Mathf.Max(0, expandedNodeCount);
    }

    internal Queue<GridMoveStep> TakeOwnedExactMovePath(
        Vector2Int destination)
    {
        if (ownedExactPath != null
            && exactDestination.HasValue
            && exactDestination.Value == destination
            && exactMoveCost != int.MaxValue)
        {
            Queue<GridMoveStep> path = ownedExactPath;
            ownedExactPath = null;
            return path;
        }

        return GetMovePathTo(destination);
    }

    public List<IGridOccupant> GetAllVisitableOccupants()
    {
        return new List<IGridOccupant>(visitableOccupants);
    }

    public bool ContainsVisitableOccupant(IGridOccupant occupant)
    {
        if (occupant == null)
        {
            return false;
        }

        visitableOccupantSet ??= new HashSet<IGridOccupant>(visitableOccupants);
        return visitableOccupantSet.Contains(occupant);
    }

    public int GetMoveDistanceTo(IGridOccupant destination)
    {
        if (destination == null || destination.IsGridDestroyed)
        {
            return int.MaxValue;
        }

        if (!TryGetVisitableOccupantPosition(destination, out Vector2Int position))
        {
            return int.MaxValue;
        }

        return GetMoveDistance(position);
    }

    public int GetMoveDistanceTo(Vector2Int position)
    {
        return ContainsPosition(position)
            ? GetMoveDistance(position)
            : int.MaxValue;
    }

    public int GetMoveCostTo(IGridOccupant destination)
    {
        if (destination == null || destination.IsGridDestroyed)
        {
            return int.MaxValue;
        }

        EnsureVisitableOccupantPositionCache();
        return visitableOccupantMoveCosts.TryGetValue(destination, out int cost)
            ? cost
            : int.MaxValue;
    }

    public int GetMoveCostTo(Vector2Int position)
    {
        if (exactDestination.HasValue)
        {
            if (position == start)
            {
                return 0;
            }

            return position == exactDestination.Value
                ? exactMoveCost
                : int.MaxValue;
        }

        if (!sourceGrid.TryGetCellIndex(position, out int index))
        {
            return int.MaxValue;
        }

        return moveCost[index];
    }

    public List<IGridOccupant> GetAllReachableOccupants()
    {
        List<IGridOccupant> result = new List<IGridOccupant>();
        if (exactDestination.HasValue)
        {
            foreach (Vector2Int position in GetReachablePositions())
            {
                GridCell exactCell = sourceGrid.GetGridCell(position);
                if (exactCell == null)
                {
                    continue;
                }

                GridSearchScratch.SharedOccupants.Clear();
                exactCell.FillAllOccupants(GridSearchScratch.SharedOccupants);
                foreach (IGridOccupant occupant in GridSearchScratch.SharedOccupants)
                {
                    if (occupant != null
                        && !occupant.IsGridDestroyed
                        && !result.Contains(occupant))
                    {
                        result.Add(occupant);
                    }
                }
            }

            GridSearchScratch.SharedOccupants.Clear();
            return result;
        }

        for (int index = 0; index < searchOrderCount; index++)
        {
            Vector2Int pos = sourceGrid.GetPositionFromCellIndex(searchOrder[index]);
            GridCell cell = sourceGrid.GetGridCell(pos);
            if (cell == null) continue;

            GridSearchScratch.SharedOccupants.Clear();
            cell.FillAllOccupants(GridSearchScratch.SharedOccupants);
            foreach (IGridOccupant occupant in GridSearchScratch.SharedOccupants)
            {
                if (occupant != null && !occupant.IsGridDestroyed && !result.Contains(occupant))
                {
                    result.Add(occupant);
                }
            }
        }

        GridSearchScratch.SharedOccupants.Clear();
        return result;
    }

    public List<Vector2Int> GetReachablePositions()
    {
        if (exactDestination.HasValue)
        {
            List<Vector2Int> exactPositions = new List<Vector2Int>(exactPath.Length + 1)
            {
                start
            };
            for (int index = 0; index < exactPath.Length; index++)
            {
                exactPositions.Add(exactPath[index].To);
            }

            return exactPositions;
        }

        List<Vector2Int> positions = new List<Vector2Int>(searchOrderCount);
        for (int index = 0; index < searchOrderCount; index++)
        {
            positions.Add(sourceGrid.GetPositionFromCellIndex(searchOrder[index]));
        }

        return positions;
    }

    public bool ContainsPosition(Vector2Int position)
    {
        if (exactDestination.HasValue)
        {
            if (position == start)
            {
                return true;
            }

            for (int exactIndex = 0; exactIndex < exactPath.Length; exactIndex++)
            {
                if (exactPath[exactIndex].To == position)
                {
                    return true;
                }
            }

            return false;
        }

        return sourceGrid.TryGetCellIndex(position, out int index)
            && moveCost[index] != int.MaxValue;
    }

    public bool TryGetMovePathToRandomReachablePosition(
        Func<Vector2Int, bool> destinationCondition,
        Func<Queue<GridMoveStep>, bool> pathCondition,
        int minDistance,
        int maxDistance,
        IRandomStream randomStream,
        out Queue<GridMoveStep> path)
    {
        path = null;
        if (destinationCondition == null || randomStream == null)
        {
            return false;
        }

        List<Vector2Int> candidates = GridSearchScratch.RentPositionList();
        try
        {
            for (int index = 0; index < searchOrderCount; index++)
            {
                Vector2Int pos = sourceGrid.GetPositionFromCellIndex(searchOrder[index]);
                if (pos == start
                    || !IsDistanceInRange(start, pos, minDistance, maxDistance)
                    || !destinationCondition(pos))
                {
                    continue;
                }

                candidates.Add(pos);
            }

            while (candidates.Count > 0)
            {
                int index = randomStream.NextInt(0, candidates.Count);
                Vector2Int candidate = candidates[index];
                candidates[index] = candidates[candidates.Count - 1];
                candidates.RemoveAt(candidates.Count - 1);

                Queue<GridMoveStep> candidatePath = BuildMovePath(candidate);
                if (pathCondition != null && !pathCondition(candidatePath))
                {
                    continue;
                }

                path = candidatePath;
                return true;
            }
        }
        finally
        {
            GridSearchScratch.ReturnPositionList(candidates);
        }

        return false;
    }

    public Queue<IGridOccupant> GetOccupantPathTo(IGridOccupant destination)
    {
        if (destination == null || destination.IsGridDestroyed) return new Queue<IGridOccupant>();
        if (exactDestination.HasValue)
        {
            GridCell exactCell = sourceGrid.GetGridCell(exactDestination.Value);
            return exactCell != null && exactCell.ContainsOccupant(destination)
                ? BuildOccupantPath(exactDestination.Value, destination)
                : new Queue<IGridOccupant>();
        }

        for (int index = 0; index < searchOrderCount; index++)
        {
            Vector2Int pos = sourceGrid.GetPositionFromCellIndex(searchOrder[index]);
            GridCell cell = sourceGrid.GetGridCell(pos);
            if (cell != null && cell.ContainsOccupant(destination))
            {
                return BuildOccupantPath(pos, destination);
            }
        }

        return new Queue<IGridOccupant>();
    }

    public Queue<IGridOccupant> GetOccupantPath(Func<Vector2Int, bool> terminateEndCondition)
    {
        if (terminateEndCondition == null) return new Queue<IGridOccupant>();
        if (exactDestination.HasValue)
        {
            for (int index = 0; index < exactPath.Length; index++)
            {
                if (terminateEndCondition(exactPath[index].To))
                {
                    return BuildOccupantPath(exactPath[index].To);
                }
            }

            return new Queue<IGridOccupant>();
        }

        for (int index = 0; index < searchOrderCount; index++)
        {
            Vector2Int pos = sourceGrid.GetPositionFromCellIndex(searchOrder[index]);
            if (terminateEndCondition(pos))
            {
                return BuildOccupantPath(pos);
            }
        }

        return new Queue<IGridOccupant>();
    }

    public Queue<GridMoveStep> GetMovePathTo(IGridOccupant destination)
    {
        if (destination == null || destination.IsGridDestroyed) return new Queue<GridMoveStep>();
        if (exactDestination.HasValue)
        {
            GridCell exactCell = sourceGrid.GetGridCell(exactDestination.Value);
            return exactCell != null && exactCell.ContainsOccupant(destination)
                ? BuildMovePath(exactDestination.Value, destination)
                : new Queue<GridMoveStep>();
        }

        for (int index = 0; index < searchOrderCount; index++)
        {
            Vector2Int pos = sourceGrid.GetPositionFromCellIndex(searchOrder[index]);
            GridCell cell = sourceGrid.GetGridCell(pos);
            if (cell != null && cell.ContainsOccupant(destination))
            {
                return BuildMovePath(pos, destination);
            }
        }

        return new Queue<GridMoveStep>();
    }

    public Queue<GridMoveStep> GetMovePathTo(Vector2Int destination)
    {
        return GetMoveCostTo(destination) != int.MaxValue
            ? BuildMovePath(destination)
            : new Queue<GridMoveStep>();
    }

    public Queue<GridMoveStep> GetMovePath(Func<Vector2Int, bool> terminateEndCondition)
    {
        if (terminateEndCondition == null) return new Queue<GridMoveStep>();
        if (exactDestination.HasValue)
        {
            Queue<GridMoveStep> exactPrefix = new Queue<GridMoveStep>();
            for (int index = 0; index < exactPath.Length; index++)
            {
                GridMoveStep step = exactPath[index];
                exactPrefix.Enqueue(step);
                if (terminateEndCondition(step.To))
                {
                    return exactPrefix;
                }
            }

            return new Queue<GridMoveStep>();
        }

        for (int index = 0; index < searchOrderCount; index++)
        {
            Vector2Int pos = sourceGrid.GetPositionFromCellIndex(searchOrder[index]);
            if (terminateEndCondition(pos))
            {
                return BuildMovePath(pos);
            }
        }

        return new Queue<GridMoveStep>();
    }

    private Queue<IGridOccupant> BuildOccupantPath(Vector2Int end, IGridOccupant destination)
    {
        Queue<IGridOccupant> path = new Queue<IGridOccupant>();
        foreach (GridMoveStep step in BuildMovePath(end, destination))
        {
            IGridOccupant occupant = step.IsSpecialMove
                ? step.MovementOccupant
                : step.DestinationOccupant;
            if (occupant != null && !path.Contains(occupant))
            {
                path.Enqueue(occupant);
            }
        }

        return path;
    }

    private Queue<IGridOccupant> BuildOccupantPath(Vector2Int end) =>
        BuildOccupantPath(end, null);

    private Queue<GridMoveStep> BuildMovePath(Vector2Int end, IGridOccupant destination)
    {
        if (exactDestination.HasValue)
        {
            if (end == start)
            {
                return new Queue<GridMoveStep>();
            }

            if (exactMoveCost == int.MaxValue)
            {
                return new Queue<GridMoveStep>();
            }

            int endPathIndex = -1;
            for (int index = 0; index < exactPath.Length; index++)
            {
                if (exactPath[index].To == end)
                {
                    endPathIndex = index;
                    break;
                }
            }

            if (endPathIndex < 0)
            {
                return new Queue<GridMoveStep>();
            }

            if (destination == null && endPathIndex == exactPath.Length - 1)
            {
                return new Queue<GridMoveStep>(exactPath);
            }

            GridMoveStep[] prefixPath = new GridMoveStep[endPathIndex + 1];
            Array.Copy(exactPath, prefixPath, prefixPath.Length);
            if (destination != null)
            {
                prefixPath[prefixPath.Length - 1] =
                    prefixPath[prefixPath.Length - 1].WithDestination(destination);
            }

            return new Queue<GridMoveStep>(prefixPath);
        }

        List<GridMoveStep> path = new List<GridMoveStep>();
        if (end == start) return new Queue<GridMoveStep>();
        if (!sourceGrid.TryGetCellIndex(end, out int currentIndex)
            || !sourceGrid.TryGetCellIndex(start, out int startIndex))
        {
            return new Queue<GridMoveStep>();
        }

        while (currentIndex != startIndex)
        {
            int fromIndex = parentIndex[currentIndex];
            if (fromIndex < 0)
            {
                return new Queue<GridMoveStep>();
            }

            Vector2Int from = sourceGrid.GetPositionFromCellIndex(fromIndex);
            Vector2Int to = sourceGrid.GetPositionFromCellIndex(currentIndex);
            IGridOccupant destinationOccupant = currentIndex == sourceGrid.GetCellIndexUnchecked(end)
                && destination != null
                    ? destination
                    : sourceGrid.GetGridCell(to)?.GetTopOccupant();
            GridMoveStep step = new GridMoveStep(
                from,
                to,
                destinationOccupant,
                parentMovementOccupant[currentIndex],
                parentMoveType[currentIndex]);
            path.Add(step);
            currentIndex = fromIndex;
        }

        path.Reverse();
        return new Queue<GridMoveStep>(path);
    }

    private Queue<GridMoveStep> BuildMovePath(Vector2Int end) =>
        BuildMovePath(end, null);

    private bool TryGetVisitableOccupantPosition(IGridOccupant occupant, out Vector2Int position)
    {
        EnsureVisitableOccupantPositionCache();
        return visitableOccupantPositions.TryGetValue(occupant, out position);
    }

    private void EnsureVisitableOccupantPositionCache()
    {
        if (visitableOccupantPositionsBuilt)
        {
            return;
        }

        visitableOccupantPositionsBuilt = true;
        visitableOccupantSet ??= new HashSet<IGridOccupant>(visitableOccupants);
        visitableOccupantPositions ??= new Dictionary<IGridOccupant, Vector2Int>();
        visitableOccupantMoveCosts ??= new Dictionary<IGridOccupant, int>();
        for (int index = 0; index < searchOrderCount; index++)
        {
            Vector2Int pos = sourceGrid.GetPositionFromCellIndex(searchOrder[index]);
            GridCell cell = sourceGrid.GetGridCell(pos);
            if (cell == null)
            {
                continue;
            }

            GridSearchScratch.SharedOccupants.Clear();
            cell.FillAllOccupants(GridSearchScratch.SharedOccupants);
            foreach (IGridOccupant occupant in GridSearchScratch.SharedOccupants)
            {
                if (occupant != null
                    && visitableOccupantSet.Contains(occupant)
                    && !visitableOccupantPositions.ContainsKey(occupant))
                {
                    visitableOccupantPositions.Add(occupant, pos);
                    int cellIndex = sourceGrid.TryGetCellIndex(pos, out int resolvedIndex)
                        ? resolvedIndex
                        : -1;
                    visitableOccupantMoveCosts.Add(
                        occupant,
                        cellIndex >= 0 ? moveCost[cellIndex] : int.MaxValue);
                }
            }
        }
    }

    private int GetMoveDistance(Vector2Int end)
    {
        if (exactDestination.HasValue)
        {
            if (end == start)
            {
                return 0;
            }

            return end == exactDestination.Value && exactMoveCost != int.MaxValue
                ? exactPath.Length
                : int.MaxValue;
        }

        if (end == start)
        {
            return 0;
        }

        if (!sourceGrid.TryGetCellIndex(end, out int currentIndex)
            || !sourceGrid.TryGetCellIndex(start, out int startIndex))
        {
            return int.MaxValue;
        }

        int distance = 0;
        while (currentIndex != startIndex)
        {
            currentIndex = parentIndex[currentIndex];
            if (currentIndex < 0)
            {
                return int.MaxValue;
            }

            distance++;
        }

        return distance;
    }

    private static bool IsDistanceInRange(
        Vector2Int from,
        Vector2Int to,
        int minDistance,
        int maxDistance)
    {
        int distance = Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        return distance >= Mathf.Max(0, minDistance)
            && (maxDistance <= 0 || distance <= maxDistance);
    }
}

