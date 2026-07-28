using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static class GridCellAreaRules
{
    public static bool IsWalkableArea(GridCellAreaType areaType)
    {
        return areaType != GridCellAreaType.BlockedExterior;
    }

    public static bool IsBuildableArea(GridCellAreaType areaType)
    {
        return areaType == GridCellAreaType.DungeonInterior;
    }

    public static bool AllowsItemDrop(GridCellAreaType areaType)
    {
        return areaType != GridCellAreaType.BlockedExterior;
    }

    public static bool AllowsLayer(GridCellAreaType areaType, GridLayer layer)
    {
        if (!IsWalkableArea(areaType))
        {
            return false;
        }

        if (areaType == GridCellAreaType.DungeonInterior)
        {
            return true;
        }

        if (areaType == GridCellAreaType.Entrance)
        {
            return layer == GridLayer.Hallway
                || layer == GridLayer.Character
                || layer == GridLayer.Wildlife
                || layer == GridLayer.Item
                || layer == GridLayer.Construction
                || layer == GridLayer.Building
                || layer == GridLayer.WallFixture
                || layer == GridLayer.CeilingFixture
                || layer == GridLayer.FloorOverlay
                || layer == GridLayer.Filth
                || layer == GridLayer.DownedCharacter;
        }

        return layer == GridLayer.Hallway
            || layer == GridLayer.Building
            || layer == GridLayer.WallFixture
            || layer == GridLayer.CeilingFixture
            || layer == GridLayer.FloorOverlay
            || layer == GridLayer.Character
            || layer == GridLayer.Wildlife
            || layer == GridLayer.Item
            || layer == GridLayer.Construction
            || layer == GridLayer.Filth
            || layer == GridLayer.DownedCharacter;
    }

    public static bool CanBuildInArea(GridCellAreaType areaType, BuildingSO buildingData)
    {
        if (buildingData == null || !IsWalkableArea(areaType))
        {
            return false;
        }

        if (areaType == GridCellAreaType.DungeonInterior)
        {
            return true;
        }

        if (buildingData.IsDoor && !buildingData.IsInteriorDoor)
        {
            return true;
        }

        if (buildingData.Placement.Layer == GridLayer.Hallway)
        {
            return areaType == GridCellAreaType.Entrance
                || areaType == GridCellAreaType.DropZone
                || areaType == GridCellAreaType.ExteriorPath;
        }

        if (areaType == GridCellAreaType.Entrance)
        {
            return buildingData.IsDoor || buildingData.IsStructuralWall;
        }

        return false;
    }
}

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

        return TryGetVisitableOccupantPosition(destination, out Vector2Int position)
            ? GetMoveCostTo(position)
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

    private Queue<IGridOccupant> BuildOccupantPath(Vector2Int end, IGridOccupant destination = null)
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

    private Queue<GridMoveStep> BuildMovePath(Vector2Int end, IGridOccupant destination = null)
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

public class Grid
{
    private const int DefaultCellWorldHeight = 3;

    public int width { get; private set; }
    public int height { get; private set; }
    public int version { get; private set; }
    public int StructuralVersion { get; private set; }
    public int NavigationVersion { get; private set; }
    public int TraversalVersion => NavigationVersion;
    public Vector3 OriginPosition => originPos;
    public int CellWorldHeight => cellWorldHeight;

    private readonly GridCell[,] gridArray;
    private readonly Dictionary<IGridOccupant, int> occupantRegistrationCounts =
        new Dictionary<IGridOccupant, int>();
    private Vector3 originPos;
    private int cellWorldHeight;
    private bool hasArbitraryHorizontalTraversal;
    private bool hasArbitraryVerticalTraversal;
    private int minimumAdjacentVerticalTraversalCost;
    private readonly List<int>[] verticalPortalXsByFloor;
    private int cachedWalkableExitNavigationVersion = -1;
    private bool hasCachedWalkableExit;
    private Vector2Int cachedWalkableExit;

    public Grid(int gridWidth, int gridHeight)
        : this(gridWidth, gridHeight, Vector3.zero, DefaultCellWorldHeight)
    {
    }

    public Grid(int gridWidth, int gridHeight, Vector3 originPos, int cellWorldHeight = DefaultCellWorldHeight)
    {
        width = Mathf.Max(1, gridWidth);
        height = Mathf.Max(1, gridHeight);
        this.originPos = originPos;
        this.cellWorldHeight = cellWorldHeight <= 0 ? DefaultCellWorldHeight : cellWorldHeight;
        version = 0;
        StructuralVersion = 0;
        NavigationVersion = 0;

        gridArray = new GridCell[height, width];
        verticalPortalXsByFloor = new List<int>[height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                gridArray[y, x] = new GridCell(pos);
            }
        }

        hasArbitraryHorizontalTraversal = false;
        hasArbitraryVerticalTraversal = false;
        minimumAdjacentVerticalTraversalCost = int.MaxValue;
    }

    public static void ReleaseRetainedSearchMemoryForDiagnostics()
    {
        GridSearchScratch.ClearRetainedMemory();
    }

    public void SetUnityCoordinates(Vector3 originPos, int cellWorldHeight = DefaultCellWorldHeight)
    {
        this.originPos = originPos;
        this.cellWorldHeight = cellWorldHeight <= 0 ? DefaultCellWorldHeight : cellWorldHeight;
    }

    public Vector2Int GetXY(Vector3 worldPosition)
    {
        int x = -Mathf.FloorToInt((worldPosition - originPos).x);
        int y = Mathf.FloorToInt((worldPosition - originPos).y) / cellWorldHeight;
        return new Vector2Int(x, y);
    }

    public Vector3 GetWorldPos(Vector2Int gridPosition)
    {
        return GetWorldPos((Vector2)gridPosition);
    }

    public Vector3 GetWorldPos(Vector2 gridPosition)
    {
        return new Vector3(
            originPos.x - gridPosition.x,
            originPos.y + (gridPosition.y * cellWorldHeight))
            + new Vector3(0.5f, 0);
    }

    public bool IsValidGridPos(Vector2Int gridPos)
    {
        return gridPos.x >= 0
            && gridPos.y >= 0
            && gridPos.x < width
            && gridPos.y < height;
    }

    public GridCell GetGridCell(Vector2Int pos)
    {
        if (IsValidGridPos(pos)) return gridArray[pos.y, pos.x];

        return null;
    }

    internal bool TryGetCellIndex(Vector2Int position, out int index)
    {
        if (!IsValidGridPos(position))
        {
            index = -1;
            return false;
        }

        index = GetCellIndexUnchecked(position);
        return true;
    }

    internal int GetCellIndexUnchecked(Vector2Int position)
    {
        return position.y * width + position.x;
    }

    internal Vector2Int GetPositionFromCellIndex(int index)
    {
        return new Vector2Int(index % width, index / width);
    }

    public IEnumerable<GridCell> GetCells()
    {
        foreach (GridCell cell in gridArray)
        {
            yield return cell;
        }
    }

    public bool TryGetAnyWalkableExit(out Vector2Int position)
    {
        if (cachedWalkableExitNavigationVersion == NavigationVersion)
        {
            position = cachedWalkableExit;
            return hasCachedWalkableExit;
        }

        hasCachedWalkableExit = false;
        cachedWalkableExit = default;
        for (int y = 0; y < height; y++)
        {
            Vector2Int left = new Vector2Int(0, y);
            if (IsWalkable(left))
            {
                hasCachedWalkableExit = true;
                cachedWalkableExit = left;
                break;
            }

            if (width <= 1)
            {
                continue;
            }

            Vector2Int right = new Vector2Int(width - 1, y);
            if (IsWalkable(right))
            {
                hasCachedWalkableExit = true;
                cachedWalkableExit = right;
                break;
            }
        }

        if (!hasCachedWalkableExit)
        {
            for (int y = 0; y < height && !hasCachedWalkableExit; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GridCell cell = gridArray[y, x];
                    if (cell == null
                        || (cell.AreaType != GridCellAreaType.Entrance
                            && cell.AreaType != GridCellAreaType.DropZone
                            && cell.AreaType != GridCellAreaType.ExteriorPath)
                        || !IsWalkable(cell.Position))
                    {
                        continue;
                    }

                    hasCachedWalkableExit = true;
                    cachedWalkableExit = cell.Position;
                    break;
                }
            }
        }

        cachedWalkableExitNavigationVersion = NavigationVersion;
        position = cachedWalkableExit;
        return hasCachedWalkableExit;
    }

    public bool SetAreaType(Vector2Int pos, GridCellAreaType areaType)
    {
        GridCell cell = GetGridCell(pos);
        if (cell == null)
        {
            return false;
        }

        bool changed = cell.SetAreaType(areaType);
        if (changed)
        {
            MarkChanged(structural: true);
        }

        return changed;
    }

    public bool SetTerrainType(Vector2Int pos, GridCellTerrainType terrainType)
    {
        GridCell cell = GetGridCell(pos);
        if (cell == null || !cell.SetTerrainType(terrainType))
        {
            return false;
        }

        version++;
        NavigationVersion++;
        return true;
    }

    public Grid TryExpandGrid(int x, int y)
    {
        int newWidth = width + x;
        int newHeight = height + y;
        if (newWidth <= 0 || newHeight <= 0) return null;

        Grid newGrid = new Grid(newWidth, newHeight, originPos, cellWorldHeight);
        int copyHeight = Mathf.Min(height, newHeight);
        int copyWidth = Mathf.Min(width, newWidth);
        for (int j = 0; j < copyHeight; j++)
        {
            for (int i = 0; i < copyWidth; i++)
            {
                newGrid.gridArray[j, i] = GetGridCell(new Vector2Int(i, j));
            }
        }

        newGrid.RebuildOccupantIndex();
        newGrid.RefreshTraversalHeuristicMetadata();
        return newGrid;
    }

    public bool RegisterOccupant(IGridOccupant occupant, GridLayer layer, IReadOnlyList<Vector2Int> positions, bool connectPositions)
    {
        if (occupant == null || positions == null) return false;

        List<Vector2Int> targetPositions = positions.Distinct().ToList();
        if (!targetPositions.Any()) return false;

        foreach (Vector2Int tempPos in targetPositions)
        {
            GridCell cell = GetGridCell(tempPos);
            if (cell == null || !cell.CanOccupy(layer)) return false;
        }

        foreach (Vector2Int tempPos in targetPositions)
        {
            GridCell cell = GetGridCell(tempPos);
            if (!cell.TrySetOccupant(layer, occupant)) return false;
        }

        if (connectPositions)
        {
            RegisterTraversalLinks(occupant, targetPositions);
        }

        AddOccupantReferences(occupant, targetPositions.Count);
        MarkChanged(AffectsStructure(layer, occupant, connectPositions));
        return true;
    }

    public bool RemoveOccupant(GridLayer layer, IReadOnlyList<Vector2Int> positions, bool disconnectPositions)
    {
        if (positions == null) return false;

        List<Vector2Int> targetPositions = positions.Distinct().ToList();
        if (!targetPositions.Any()) return false;

        foreach (Vector2Int tempPos in targetPositions)
        {
            GridCell cell = GetGridCell(tempPos);
            if (cell == null) return false;
        }

        bool changed = false;
        bool structuralChange = disconnectPositions || IsStructuralLayer(layer);
        foreach (Vector2Int tempPos in targetPositions)
        {
            GridCell cell = GetGridCell(tempPos);
            IGridOccupant removedOccupant = cell.GetOccupant(layer);
            structuralChange |= removedOccupant != null && removedOccupant.IsGridMovement;
            changed = changed || cell.HasOccupantInLayer(layer) || (disconnectPositions && cell.TraversalLinks.Any());
            cell.RemoveOccupantByLayer(layer);
            RemoveOccupantReferences(removedOccupant, 1);
            if (disconnectPositions)
            {
                cell.SetTraversalLinks(null);
            }
        }

        if (changed)
        {
            if (disconnectPositions)
            {
                RefreshTraversalHeuristicMetadata();
            }

            MarkChanged(structuralChange);
        }

        return changed;
    }

    public bool RemoveOccupant(
        IGridOccupant expectedOccupant,
        GridLayer layer,
        IReadOnlyList<Vector2Int> positions,
        bool disconnectPositions)
    {
        if (expectedOccupant == null || positions == null)
        {
            return false;
        }

        List<Vector2Int> targetPositions = positions.Distinct().ToList();
        if (!targetPositions.Any())
        {
            return false;
        }

        foreach (Vector2Int position in targetPositions)
        {
            if (GetGridCell(position) == null)
            {
                return false;
            }
        }

        bool changed = false;
        bool structuralChange = disconnectPositions
            || IsStructuralLayer(layer)
            || expectedOccupant.IsGridMovement;
        foreach (Vector2Int position in targetPositions)
        {
            GridCell cell = GetGridCell(position);
            if (!ReferenceEquals(cell.GetOccupant(layer), expectedOccupant))
            {
                continue;
            }

            cell.RemoveOccupantByLayer(layer);
            RemoveOccupantReferences(expectedOccupant, 1);
            if (disconnectPositions)
            {
                cell.SetTraversalLinks(null);
            }

            changed = true;
        }

        if (changed)
        {
            if (disconnectPositions)
            {
                RefreshTraversalHeuristicMetadata();
            }

            MarkChanged(structuralChange);
        }

        return changed;
    }

    public GridPathSearchResult SearchPath(Vector2Int start)
    {
        return SearchPath(
            start,
            null,
            null,
            DefaultGridTraversalCostPolicy.Instance,
            default,
            null);
    }

    public GridPathSearchResult SearchPathWithTraversalFilter(
        Vector2Int start,
        Func<Vector2Int, bool> traversalFilter)
    {
        return SearchPath(
            start,
            null,
            traversalFilter,
            DefaultGridTraversalCostPolicy.Instance,
            default,
            null);
    }

    public GridPathSearchResult SearchPathTo(
        Vector2Int start,
        Vector2Int destination,
        Func<Vector2Int, bool> traversalFilter = null,
        IGridTraversalCostPolicy costPolicy = null,
        GridTraversalContext traversalContext = default)
    {
        return SearchPath(
            start,
            null,
            traversalFilter,
            costPolicy ?? DefaultGridTraversalCostPolicy.Instance,
            traversalContext,
            destination);
    }

    internal GridPathSearchResult SearchPathWeighted(
        Vector2Int start,
        Func<Vector2Int, bool> traversalFilter,
        IGridTraversalCostPolicy costPolicy,
        GridTraversalContext traversalContext)
    {
        return SearchPath(
            start,
            null,
            traversalFilter,
            costPolicy ?? DefaultGridTraversalCostPolicy.Instance,
            traversalContext,
            null);
    }

    private GridPathSearchResult SearchPath(
        Vector2Int start,
        Func<Vector2Int, bool> stopCondition,
        Func<Vector2Int, bool> traversalFilter,
        IGridTraversalCostPolicy costPolicy,
        GridTraversalContext traversalContext,
        Vector2Int? exactDestination)
    {
        int cellCount = width * height;
        if (!TryGetCellIndex(start, out int startIndex))
        {
            if (exactDestination.HasValue)
            {
                return new GridPathSearchResult(
                    this,
                    start,
                    TraversalVersion,
                    exactDestination.Value,
                    Array.Empty<GridMoveStep>(),
                    int.MaxValue);
            }

            return new GridPathSearchResult(
                this,
                start,
                TraversalVersion,
                CreateFilledArray(cellCount, -1),
                new IGridOccupant[cellCount],
                new GridMoveType[cellCount],
                new int[cellCount],
                0,
                CreateFilledArray(cellCount, int.MaxValue),
                new List<IGridOccupant>(0));
        }

        GridSearchWorkspace workspace = exactDestination.HasValue
            ? GridSearchScratch.RentWorkspace(cellCount)
            : null;
        int[] parentIndex = workspace?.ParentIndex ?? CreateFilledArray(cellCount, -1);
        IGridOccupant[] parentMovementOccupant =
            workspace?.ParentMovementOccupant ?? new IGridOccupant[cellCount];
        GridMoveType[] parentMoveType = workspace?.ParentMoveType ?? new GridMoveType[cellCount];
        int[] searchOrder = workspace?.SearchOrder ?? new int[cellCount];
        int[] moveCost = workspace?.MoveCost ?? CreateFilledArray(cellCount, int.MaxValue);
        int searchOrderCount = 0;
        costPolicy ??= DefaultGridTraversalCostPolicy.Instance;
        GridSearchPriorityQueue queue = GridSearchScratch.RentPriorityQueue();
        List<GridTraversalStepData> nextSteps = GridSearchScratch.RentTraversalStepList();
        List<IGridOccupant> currentOccupants = GridSearchScratch.RentOccupantList();
        int sequence = 0;
        bool collectVisitableOccupants = !exactDestination.HasValue;
        List<IGridOccupant> visitableOccupants = collectVisitableOccupants
            ? new List<IGridOccupant>()
            : null;
        HashSet<IGridOccupant> visitableOccupantSet = collectVisitableOccupants
            ? GridSearchScratch.RentOccupantSet()
            : null;
        GridMoveStep[] compactExactPath = null;
        int compactExactCost = int.MaxValue;

        try
        {
            if (workspace != null)
            {
                workspace.SetStart(startIndex);
            }
            else
            {
                moveCost[startIndex] = 0;
            }
            queue.Enqueue(new GridSearchQueueNode(
                startIndex,
                0,
                EstimateRemainingCost(start, exactDestination, costPolicy),
                sequence++));

            while (queue.Count > 0)
            {
                GridSearchQueueNode current = queue.Dequeue();
                int currentIndex = current.CellIndex;
                int knownCurrentCost = workspace != null
                    ? workspace.GetMoveCost(currentIndex)
                    : moveCost[currentIndex];
                if (current.Cost != knownCurrentCost)
                {
                    continue;
                }

                Vector2Int pos = GetPositionFromCellIndex(currentIndex);
                searchOrder[searchOrderCount++] = currentIndex;
                nextSteps.Clear();

                GridCell cell = GetGridCell(pos);
                if (cell == null) continue;

                if (collectVisitableOccupants)
                {
                    currentOccupants.Clear();
                    cell.FillAllOccupants(currentOccupants);
                    foreach (IGridOccupant occupant in currentOccupants)
                    {
                        if (occupant != null
                            && occupant.IsGridVisitable
                            && visitableOccupantSet.Add(occupant))
                        {
                            visitableOccupants.Add(occupant);
                        }
                    }
                }

                bool reachedDestination = exactDestination.HasValue
                    ? pos == exactDestination.Value
                    : stopCondition != null && stopCondition(pos);
                if (reachedDestination)
                {
                    break;
                }

                foreach (GridTraversalLink link in cell.TraversalLinks)
                {
                    AddTraversalStep(nextSteps, pos, link.To, link.Through, link.MoveType);
                }

                AddTraversalStep(nextSteps, pos, pos + Vector2Int.left, null, GridMoveType.Walk);
                AddTraversalStep(nextSteps, pos, pos + Vector2Int.right, null, GridMoveType.Walk);

                foreach (GridTraversalStepData step in nextSteps)
                {
                    Vector2Int nextPos = step.To;
                    GridCell nextCell = GetGridCell(nextPos);
                    bool passesTraversalFilter =
                        traversalFilter == null || traversalFilter(nextPos);
                    bool isAllowedTerminal = (exactDestination.HasValue
                            ? nextPos == exactDestination.Value
                            : stopCondition != null && stopCondition(nextPos))
                        && !IsMovementBlockedByWall(nextPos)
                        && passesTraversalFilter;
                    if (nextCell != null
                        && passesTraversalFilter
                        && (IsWalkable(nextPos) || isAllowedTerminal))
                    {
                        int nextIndex = GetCellIndexUnchecked(nextPos);
                        int stepCost = costPolicy.GetTraversalCost(
                            this,
                            in step,
                            traversalContext);
                        if (stepCost == int.MaxValue
                            || current.Cost > int.MaxValue - stepCost)
                        {
                            continue;
                        }

                        int candidateCost = current.Cost + stepCost;
                        int knownNextCost = workspace != null
                            ? workspace.GetMoveCost(nextIndex)
                            : moveCost[nextIndex];
                        if (candidateCost >= knownNextCost)
                        {
                            continue;
                        }

                        if (workspace != null)
                        {
                            workspace.SetNode(
                                nextIndex,
                                candidateCost,
                                currentIndex,
                                step.MovementOccupant,
                                step.MoveType);
                        }
                        else
                        {
                            moveCost[nextIndex] = candidateCost;
                            parentIndex[nextIndex] = currentIndex;
                            parentMovementOccupant[nextIndex] = step.MovementOccupant;
                            parentMoveType[nextIndex] = step.MoveType;
                        }
                        int priority = candidateCost
                            + EstimateRemainingCost(nextPos, exactDestination, costPolicy);
                        queue.Enqueue(new GridSearchQueueNode(
                            nextIndex,
                            candidateCost,
                            priority,
                            sequence++));
                    }
                }
            }

            if (exactDestination.HasValue)
            {
                int destinationCost = TryGetCellIndex(
                        exactDestination.Value,
                        out int destinationIndex)
                    ? workspace.GetMoveCost(destinationIndex)
                    : int.MaxValue;
                compactExactPath = BuildExactPath(
                    startIndex,
                    exactDestination.Value,
                    parentIndex,
                    parentMovementOccupant,
                    parentMoveType,
                    destinationCost,
                    out compactExactCost);
            }
        }
        finally
        {
            GridSearchScratch.Return(queue);
            GridSearchScratch.ReturnTraversalStepList(nextSteps);
            GridSearchScratch.ReturnOccupantList(currentOccupants);
            GridSearchScratch.Return(visitableOccupantSet);
            GridSearchScratch.Return(workspace);
            GridSearchScratch.SharedOccupants.Clear();
        }

        if (exactDestination.HasValue)
        {
            return new GridPathSearchResult(
                this,
                start,
                TraversalVersion,
                exactDestination.Value,
                compactExactPath,
                compactExactCost,
                searchOrderCount);
        }

        return new GridPathSearchResult(
            this,
            start,
            TraversalVersion,
            parentIndex,
            parentMovementOccupant,
            parentMoveType,
            searchOrder,
            searchOrderCount,
            moveCost,
            visitableOccupants);
    }

    public Queue<IGridOccupant> GetOccupantPath(Vector2Int start, Func<Vector2Int, bool> terminateEndCondition)
    {
        return SearchPath(
                start,
                terminateEndCondition,
                null,
                DefaultGridTraversalCostPolicy.Instance,
                default,
                null)
            .GetOccupantPath(terminateEndCondition);
    }

    public Queue<GridMoveStep> GetMovePath(Vector2Int start, Func<Vector2Int, bool> terminateEndCondition)
    {
        return SearchPath(
                start,
                terminateEndCondition,
                null,
                DefaultGridTraversalCostPolicy.Instance,
                default,
                null)
            .GetMovePath(terminateEndCondition);
    }

    public Queue<GridMoveStep> GetMovePathTo(Vector2Int start, Vector2Int destination)
    {
        return SearchPathTo(start, destination).GetMovePathTo(destination);
    }

    public List<IGridOccupant> GetAllVisitableOccupants(Vector2Int start)
    {
        return SearchPath(start).GetAllVisitableOccupants();
    }

    public List<IGridOccupant> GetAllReachableOccupants(Vector2Int start)
    {
        return SearchPath(start).GetAllReachableOccupants();
    }

    public Queue<IGridOccupant> SmoothOccupantPath(Queue<IGridOccupant> gridPath)
    {
        Queue<IGridOccupant> result = new Queue<IGridOccupant>();
        if (gridPath == null || !gridPath.Any()) return result;

        while (gridPath.Count > 1)
        {
            IGridOccupant occupant = gridPath.Dequeue();
            if (occupant.IsGridMovement)
            {
                result.Enqueue(occupant);
            }
        }

        result.Enqueue(gridPath.Dequeue());
        return result;
    }

    public bool IsWalkable(Vector2Int pos)
    {
        GridCell cell = GetGridCell(pos);
        if (cell == null) return false;

        if (!cell.IsWalkableArea)
        {
            return false;
        }

        BuildableObject building = cell.GetOccupant(GridLayer.Building) as BuildableObject;
        if (IsMovementBlockedByWall(pos))
        {
            return false;
        }

        if (cell.AreaType != GridCellAreaType.DungeonInterior)
        {
            return true;
        }

        if (building != null && IsWalkableFacilityCell(building))
        {
            return true;
        }

        if (cell.HasOccupantInLayer(GridLayer.Hallway)) return true;

        GridSearchScratch.SharedOccupants.Clear();
        cell.FillAllOccupants(GridSearchScratch.SharedOccupants);
        foreach (IGridOccupant occupant in GridSearchScratch.SharedOccupants)
        {
            if (occupant != null && occupant.IsGridMovement)
            {
                GridSearchScratch.SharedOccupants.Clear();
                return true;
            }
        }

        GridSearchScratch.SharedOccupants.Clear();
        return false;
    }

    public bool IsMovementBlockedByWall(Vector2Int pos)
    {
        BuildableObject building = GetGridCell(pos)?.GetOccupant(GridLayer.Building) as BuildableObject;
        if (building == null || building.isDestroy)
        {
            return false;
        }

        BuildingSO buildingData = building.BuildingData;
        bool isDoor = building is Door || (buildingData != null && buildingData.IsDoor);
        bool isStructuralWall = buildingData != null
            ? buildingData.IsStructuralWall
            : building.category == BuildingCategory.Wall;
        return isStructuralWall && !isDoor;
    }

    private static bool IsWalkableFacilityCell(BuildableObject building)
    {
        FacilityData facility = building != null ? building.Facility : null;
        return facility != null && facility.IsVisitorFacility;
    }

    public bool TryFindNearestWalkablePosition(Vector2Int start, out Vector2Int walkablePosition)
    {
        if (IsValidGridPos(start) && IsWalkable(start))
        {
            walkablePosition = start;
            return true;
        }

        bool found = false;
        Vector2Int best = default;
        int bestDistance = int.MaxValue;
        foreach (GridCell cell in GetCells())
        {
            if (cell == null || !IsWalkable(cell.Position)) continue;

            int distance = Mathf.Abs(cell.Position.x - start.x) + Mathf.Abs(cell.Position.y - start.y);
            if (found && distance >= bestDistance) continue;

            found = true;
            best = cell.Position;
            bestDistance = distance;
        }

        walkablePosition = best;
        return found;
    }

    public bool TryFindNearestWalkablePositionOnSameFloor(Vector2Int start, out Vector2Int walkablePosition)
    {
        if (IsValidGridPos(start) && IsWalkable(start))
        {
            walkablePosition = start;
            return true;
        }

        bool found = false;
        Vector2Int best = default;
        int bestDistance = int.MaxValue;
        foreach (GridCell cell in GetCells())
        {
            if (cell == null || cell.Position.y != start.y || !IsWalkable(cell.Position)) continue;

            int distance = Mathf.Abs(cell.Position.x - start.x);
            if (found && distance >= bestDistance) continue;

            found = true;
            best = cell.Position;
            bestDistance = distance;
        }

        walkablePosition = best;
        return found;
    }

    public bool TryFindNearbyWalkablePositionOnSameFloor(
        Vector2Int start,
        out Vector2Int walkablePosition,
        int maxDistance = 1)
    {
        if (IsValidGridPos(start) && IsWalkable(start))
        {
            walkablePosition = start;
            return true;
        }

        int clampedDistance = Mathf.Max(1, maxDistance);
        for (int distance = 1; distance <= clampedDistance; distance++)
        {
            Vector2Int left = new Vector2Int(start.x - distance, start.y);
            if (IsValidGridPos(left) && IsWalkable(left))
            {
                walkablePosition = left;
                return true;
            }

            Vector2Int right = new Vector2Int(start.x + distance, start.y);
            if (IsValidGridPos(right) && IsWalkable(right))
            {
                walkablePosition = right;
                return true;
            }
        }

        walkablePosition = default;
        return false;
    }

    public bool IsConnectedWithAny(IReadOnlyCollection<Vector2Int> end)
    {
        if (end == null) return false;

        return GetOccupantPath(Vector2Int.zero, (pos) => end.Contains(pos)).Any();
    }

    public List<IGridOccupant> FindAllOccupants(Func<IGridOccupant, bool> predicate)
    {
        List<IGridOccupant> result =
            new List<IGridOccupant>(occupantRegistrationCounts.Count);
        foreach (IGridOccupant occupant in occupantRegistrationCounts.Keys)
        {
            if (occupant != null && (predicate == null || predicate(occupant)))
            {
                result.Add(occupant);
            }
        }

        return result;
    }

    private void AddOccupantReferences(IGridOccupant occupant, int count)
    {
        if (occupant == null || count <= 0)
        {
            return;
        }

        occupantRegistrationCounts.TryGetValue(occupant, out int existingCount);
        occupantRegistrationCounts[occupant] = existingCount + count;
    }

    private void RemoveOccupantReferences(IGridOccupant occupant, int count)
    {
        if (occupant == null
            || count <= 0
            || !occupantRegistrationCounts.TryGetValue(occupant, out int existingCount))
        {
            return;
        }

        int remainingCount = existingCount - count;
        if (remainingCount > 0)
        {
            occupantRegistrationCounts[occupant] = remainingCount;
        }
        else
        {
            occupantRegistrationCounts.Remove(occupant);
        }
    }

    private void RebuildOccupantIndex()
    {
        occupantRegistrationCounts.Clear();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GridSearchScratch.SharedOccupants.Clear();
                gridArray[y, x].FillAllOccupants(GridSearchScratch.SharedOccupants);
                foreach (IGridOccupant occupant in GridSearchScratch.SharedOccupants)
                {
                    AddOccupantReferences(occupant, 1);
                }
            }
        }

        GridSearchScratch.SharedOccupants.Clear();
    }

    private void MarkChanged(bool structural)
    {
        version++;
        if (structural)
        {
            StructuralVersion++;
            NavigationVersion++;
        }
    }

    private int EstimateRemainingCost(
        Vector2Int position,
        Vector2Int? exactDestination,
        IGridTraversalCostPolicy costPolicy)
    {
        if (!exactDestination.HasValue)
        {
            return 0;
        }

        long estimate = 0L;
        if (!hasArbitraryHorizontalTraversal)
        {
            int horizontalDistance = Mathf.Abs(position.x - exactDestination.Value.x);
            if (position.y != exactDestination.Value.y
                && TryGetNearestVerticalPortalDistance(
                    position.y,
                    position.x,
                    out int distanceToDeparturePortal)
                && TryGetNearestVerticalPortalDistance(
                    exactDestination.Value.y,
                    exactDestination.Value.x,
                    out int distanceFromArrivalPortal))
            {
                horizontalDistance = Mathf.Max(
                    horizontalDistance,
                    distanceToDeparturePortal + distanceFromArrivalPortal);
            }

            estimate += (long)horizontalDistance
                * Mathf.Max(0, costPolicy.MinimumHorizontalCost);
        }

        if (ReferenceEquals(costPolicy, DefaultGridTraversalCostPolicy.Instance)
            && !hasArbitraryVerticalTraversal
            && minimumAdjacentVerticalTraversalCost != int.MaxValue)
        {
            int verticalDistance = Mathf.Abs(position.y - exactDestination.Value.y);
            estimate += (long)verticalDistance * minimumAdjacentVerticalTraversalCost;
        }

        return estimate >= int.MaxValue ? int.MaxValue : (int)estimate;
    }

    internal int EstimatePathHeuristicCost(
        Vector2Int position,
        Vector2Int destination,
        IGridTraversalCostPolicy costPolicy)
    {
        return EstimateRemainingCost(
            position,
            destination,
            costPolicy ?? DefaultGridTraversalCostPolicy.Instance);
    }

    public void RefreshTraversalHeuristicMetadata()
    {
        hasArbitraryHorizontalTraversal = false;
        hasArbitraryVerticalTraversal = false;
        minimumAdjacentVerticalTraversalCost = int.MaxValue;
        Array.Clear(verticalPortalXsByFloor, 0, verticalPortalXsByFloor.Length);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GridCell cell = gridArray[y, x];
                foreach (GridTraversalLink link in cell.TraversalLinks)
                {
                    UpdateTraversalHeuristicMetadata(
                        new Vector2Int(x, y),
                        link);
                }
            }
        }
    }

    private static bool AffectsStructure(
        GridLayer layer,
        IGridOccupant occupant,
        bool connectsPositions)
    {
        return connectsPositions
            || IsStructuralLayer(layer)
            || occupant?.IsGridMovement == true;
    }

    private static bool IsStructuralLayer(GridLayer layer)
    {
        return layer == GridLayer.Building || layer == GridLayer.Hallway;
    }

    private void RegisterTraversalLinks(IGridOccupant occupant, IReadOnlyList<Vector2Int> positions)
    {
        GridMoveType moveType = ResolveMoveType(occupant);
        foreach (Vector2Int from in positions)
        {
            GridCell cell = GetGridCell(from);
            if (cell == null) continue;

            List<GridTraversalLink> links = new List<GridTraversalLink>();
            foreach (Vector2Int to in positions)
            {
                if (from == to || !CanConnectMovementCells(from, to, moveType)) continue;

                links.Add(new GridTraversalLink(to, occupant, moveType));
                if (moveType == GridMoveType.Teleport && from.x != to.x)
                {
                    hasArbitraryHorizontalTraversal = true;
                }
            }

            cell.SetTraversalLinks(links);
            foreach (GridTraversalLink link in links)
            {
                UpdateTraversalHeuristicMetadata(from, link);
            }
        }
    }

    private void UpdateTraversalHeuristicMetadata(
        Vector2Int from,
        GridTraversalLink link)
    {
        if (link == null)
        {
            return;
        }

        if (link.MoveType == GridMoveType.Teleport && link.To.x != from.x)
        {
            hasArbitraryHorizontalTraversal = true;
        }

        int verticalDistance = Mathf.Abs(link.To.y - from.y);
        if (verticalDistance <= 0)
        {
            return;
        }

        RecordVerticalPortal(from);
        RecordVerticalPortal(link.To);
        if (verticalDistance != 1)
        {
            hasArbitraryVerticalTraversal = true;
            return;
        }

        GridTraversalStepData step = new GridTraversalStepData(
            from,
            link.To,
            link.Through,
            link.MoveType);
        int cost = DefaultGridTraversalCostPolicy.Instance.GetTraversalCost(
            this,
            in step,
            default);
        if (cost > 0 && cost < minimumAdjacentVerticalTraversalCost)
        {
            minimumAdjacentVerticalTraversalCost = cost;
        }
    }

    private void RecordVerticalPortal(Vector2Int position)
    {
        if (position.y < 0 || position.y >= verticalPortalXsByFloor.Length)
        {
            return;
        }

        List<int> portals = verticalPortalXsByFloor[position.y];
        if (portals == null)
        {
            portals = new List<int>(2);
            verticalPortalXsByFloor[position.y] = portals;
        }

        int index = portals.BinarySearch(position.x);
        if (index < 0)
        {
            portals.Insert(~index, position.x);
        }
    }

    private bool TryGetNearestVerticalPortalDistance(
        int floor,
        int x,
        out int distance)
    {
        distance = 0;
        if (floor < 0
            || floor >= verticalPortalXsByFloor.Length
            || verticalPortalXsByFloor[floor] == null
            || verticalPortalXsByFloor[floor].Count == 0)
        {
            return false;
        }

        List<int> portals = verticalPortalXsByFloor[floor];
        int index = portals.BinarySearch(x);
        if (index >= 0)
        {
            return true;
        }

        int insertionIndex = ~index;
        int lowerDistance = insertionIndex > 0
            ? x - portals[insertionIndex - 1]
            : int.MaxValue;
        int upperDistance = insertionIndex < portals.Count
            ? portals[insertionIndex] - x
            : int.MaxValue;
        distance = Mathf.Min(lowerDistance, upperDistance);
        return distance != int.MaxValue;
    }

    private bool CanConnectMovementCells(Vector2Int from, Vector2Int to, GridMoveType moveType)
    {
        switch (moveType)
        {
            case GridMoveType.Stair:
                return from.x == to.x && Mathf.Abs(from.y - to.y) == 1;
            case GridMoveType.Elevator:
                return from.x == to.x && from.y != to.y;
            case GridMoveType.Teleport:
                return true;
            case GridMoveType.Instant:
                return from.x == to.x && from.y != to.y;
            default:
                return false;
        }
    }

    private GridMoveType ResolveMoveType(IGridOccupant occupant)
    {
        if (occupant is IGridMovementOccupant movementOccupant)
        {
            return movementOccupant.GridMoveType;
        }

        return GridMoveType.Instant;
    }

    private static void AddTraversalStep(
        List<GridTraversalStepData> steps,
        Vector2Int from,
        Vector2Int to,
        IGridOccupant movementOccupant,
        GridMoveType moveType)
    {
        steps.Add(new GridTraversalStepData(from, to, movementOccupant, moveType));
    }

    private GridMoveStep[] BuildExactPath(
        int startIndex,
        Vector2Int destination,
        int[] parentIndices,
        IGridOccupant[] movementOccupants,
        GridMoveType[] moveTypes,
        int destinationCost,
        out int totalCost)
    {
        totalCost = int.MaxValue;
        if (!TryGetCellIndex(destination, out int destinationIndex))
        {
            return Array.Empty<GridMoveStep>();
        }

        totalCost = destinationCost;
        if (destinationIndex == startIndex)
        {
            totalCost = 0;
            return Array.Empty<GridMoveStep>();
        }

        if (totalCost == int.MaxValue)
        {
            return Array.Empty<GridMoveStep>();
        }

        int stepCount = 0;
        int currentIndex = destinationIndex;
        while (currentIndex != startIndex)
        {
            currentIndex = parentIndices[currentIndex];
            if (currentIndex < 0 || ++stepCount > parentIndices.Length)
            {
                totalCost = int.MaxValue;
                return Array.Empty<GridMoveStep>();
            }
        }

        GridMoveStep[] path = new GridMoveStep[stepCount];
        currentIndex = destinationIndex;
        for (int pathIndex = stepCount - 1; pathIndex >= 0; pathIndex--)
        {
            int fromIndex = parentIndices[currentIndex];
            Vector2Int from = GetPositionFromCellIndex(fromIndex);
            Vector2Int to = GetPositionFromCellIndex(currentIndex);
            path[pathIndex] = new GridMoveStep(
                from,
                to,
                GetGridCell(to)?.GetTopOccupant(),
                movementOccupants[currentIndex],
                moveTypes[currentIndex]);
            currentIndex = fromIndex;
        }

        return path;
    }

    private static int[] CreateFilledArray(int count, int value)
    {
        int[] result = new int[Mathf.Max(0, count)];
        Array.Fill(result, value);
        return result;
    }
}

internal sealed class GridSearchWorkspace
{
    public int[] ParentIndex { get; private set; } = Array.Empty<int>();
    public IGridOccupant[] ParentMovementOccupant { get; private set; } =
        Array.Empty<IGridOccupant>();
    public GridMoveType[] ParentMoveType { get; private set; } = Array.Empty<GridMoveType>();
    public int[] SearchOrder { get; private set; } = Array.Empty<int>();
    public int[] MoveCost { get; private set; } = Array.Empty<int>();
    private int[] nodeGeneration = Array.Empty<int>();
    private int[] touchedIndices = Array.Empty<int>();
    private int currentGeneration;
    private int touchedCount;

    public void Prepare(int cellCount)
    {
        int capacity = Mathf.NextPowerOfTwo(Mathf.Max(1, cellCount));
        if (ParentIndex.Length < cellCount)
        {
            ParentIndex = new int[capacity];
            ParentMovementOccupant = new IGridOccupant[capacity];
            ParentMoveType = new GridMoveType[capacity];
            SearchOrder = new int[capacity];
            MoveCost = new int[capacity];
            nodeGeneration = new int[capacity];
            touchedIndices = new int[capacity];
            currentGeneration = 0;
            touchedCount = 0;
        }

        currentGeneration++;
        if (currentGeneration == int.MaxValue)
        {
            Array.Clear(nodeGeneration, 0, nodeGeneration.Length);
            currentGeneration = 1;
        }

        touchedCount = 0;
    }

    public int GetMoveCost(int index)
    {
        return nodeGeneration[index] == currentGeneration
            ? MoveCost[index]
            : int.MaxValue;
    }

    public void SetStart(int index)
    {
        MarkTouched(index);
        ParentIndex[index] = -1;
        ParentMovementOccupant[index] = null;
        ParentMoveType[index] = default;
        MoveCost[index] = 0;
    }

    public void SetNode(
        int index,
        int cost,
        int parentIndex,
        IGridOccupant movementOccupant,
        GridMoveType moveType)
    {
        MarkTouched(index);
        ParentIndex[index] = parentIndex;
        ParentMovementOccupant[index] = movementOccupant;
        ParentMoveType[index] = moveType;
        MoveCost[index] = cost;
    }

    public void ReleaseReferences()
    {
        for (int index = 0; index < touchedCount; index++)
        {
            ParentMovementOccupant[touchedIndices[index]] = null;
        }
    }

    private void MarkTouched(int index)
    {
        if (nodeGeneration[index] == currentGeneration)
        {
            return;
        }

        nodeGeneration[index] = currentGeneration;
        touchedIndices[touchedCount++] = index;
    }
}

internal sealed class SparseGridSearchWorkspace
{
    private const int InitialCapacity = 512;
    private const int LoadFactorNumerator = 7;
    private const int LoadFactorDenominator = 10;

    private int[] keys = new int[InitialCapacity];
    private int[] generations = new int[InitialCapacity];
    private int[] parentIndices = new int[InitialCapacity];
    private int[] moveCosts = new int[InitialCapacity];
    private IGridOccupant[] parentMovementOccupants =
        new IGridOccupant[InitialCapacity];
    private GridMoveType[] parentMoveTypes =
        new GridMoveType[InitialCapacity];
    private int[] occupiedSlots = new int[InitialCapacity];
    private int currentGeneration;
    private int count;

    public void Prepare()
    {
        currentGeneration++;
        if (currentGeneration == int.MaxValue)
        {
            Array.Clear(generations, 0, generations.Length);
            currentGeneration = 1;
        }

        count = 0;
    }

    public int GetMoveCost(int cellIndex)
    {
        int slot = FindExistingSlot(cellIndex);
        return slot >= 0 ? moveCosts[slot] : int.MaxValue;
    }

    public int GetParentIndex(int cellIndex)
    {
        int slot = FindExistingSlot(cellIndex);
        return slot >= 0 ? parentIndices[slot] : -1;
    }

    public IGridOccupant GetParentMovementOccupant(int cellIndex)
    {
        int slot = FindExistingSlot(cellIndex);
        return slot >= 0 ? parentMovementOccupants[slot] : null;
    }

    public GridMoveType GetParentMoveType(int cellIndex)
    {
        int slot = FindExistingSlot(cellIndex);
        return slot >= 0 ? parentMoveTypes[slot] : default;
    }

    public void SetStart(int cellIndex)
    {
        int slot = FindOrCreateSlot(cellIndex);
        parentIndices[slot] = -1;
        parentMovementOccupants[slot] = null;
        parentMoveTypes[slot] = default;
        moveCosts[slot] = 0;
    }

    public void SetNode(
        int cellIndex,
        int cost,
        int parentIndex,
        IGridOccupant movementOccupant,
        GridMoveType moveType)
    {
        int slot = FindOrCreateSlot(cellIndex);
        parentIndices[slot] = parentIndex;
        parentMovementOccupants[slot] = movementOccupant;
        parentMoveTypes[slot] = moveType;
        moveCosts[slot] = cost;
    }

    public void ReleaseReferences()
    {
        for (int index = 0; index < count; index++)
        {
            parentMovementOccupants[occupiedSlots[index]] = null;
        }
    }

    private int FindExistingSlot(int cellIndex)
    {
        int mask = keys.Length - 1;
        int slot = Hash(cellIndex) & mask;
        while (generations[slot] == currentGeneration)
        {
            if (keys[slot] == cellIndex)
            {
                return slot;
            }

            slot = (slot + 1) & mask;
        }

        return -1;
    }

    private int FindOrCreateSlot(int cellIndex)
    {
        int existing = FindExistingSlot(cellIndex);
        if (existing >= 0)
        {
            return existing;
        }

        EnsureInsertCapacity();
        int mask = keys.Length - 1;
        int slot = Hash(cellIndex) & mask;
        while (generations[slot] == currentGeneration)
        {
            slot = (slot + 1) & mask;
        }

        generations[slot] = currentGeneration;
        keys[slot] = cellIndex;
        parentIndices[slot] = -1;
        moveCosts[slot] = int.MaxValue;
        parentMovementOccupants[slot] = null;
        parentMoveTypes[slot] = default;
        occupiedSlots[count++] = slot;
        return slot;
    }

    private void EnsureInsertCapacity()
    {
        if ((count + 1) * LoadFactorDenominator
            < keys.Length * LoadFactorNumerator)
        {
            return;
        }

        Grow(keys.Length << 1);
    }

    private void Grow(int capacity)
    {
        int[] oldKeys = keys;
        int[] oldGenerations = generations;
        int[] oldParentIndices = parentIndices;
        int[] oldMoveCosts = moveCosts;
        IGridOccupant[] oldParentMovementOccupants =
            parentMovementOccupants;
        GridMoveType[] oldParentMoveTypes = parentMoveTypes;
        int oldGeneration = currentGeneration;

        keys = new int[capacity];
        generations = new int[capacity];
        parentIndices = new int[capacity];
        moveCosts = new int[capacity];
        parentMovementOccupants = new IGridOccupant[capacity];
        parentMoveTypes = new GridMoveType[capacity];
        occupiedSlots = new int[capacity];
        currentGeneration = 1;
        count = 0;

        int mask = capacity - 1;
        for (int oldSlot = 0; oldSlot < oldKeys.Length; oldSlot++)
        {
            if (oldGenerations[oldSlot] != oldGeneration)
            {
                continue;
            }

            int cellIndex = oldKeys[oldSlot];
            int slot = Hash(cellIndex) & mask;
            while (generations[slot] == currentGeneration)
            {
                slot = (slot + 1) & mask;
            }

            generations[slot] = currentGeneration;
            keys[slot] = cellIndex;
            parentIndices[slot] = oldParentIndices[oldSlot];
            moveCosts[slot] = oldMoveCosts[oldSlot];
            parentMovementOccupants[slot] =
                oldParentMovementOccupants[oldSlot];
            parentMoveTypes[slot] = oldParentMoveTypes[oldSlot];
            occupiedSlots[count++] = slot;
        }
    }

    private static int Hash(int value)
    {
        unchecked
        {
            uint hash = (uint)value;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            return (int)hash;
        }
    }
}

internal static class GridSearchScratch
{
    private static readonly Stack<Queue<Vector2Int>> PositionQueues = new Stack<Queue<Vector2Int>>();
    private static readonly Stack<List<Vector2Int>> PositionLists = new Stack<List<Vector2Int>>();
    private static readonly Stack<List<GridTraversalStepData>> TraversalStepLists =
        new Stack<List<GridTraversalStepData>>();
    private static readonly Stack<List<IGridOccupant>> OccupantLists = new Stack<List<IGridOccupant>>();
    private static readonly Stack<HashSet<IGridOccupant>> OccupantSets = new Stack<HashSet<IGridOccupant>>();
    private static readonly Stack<Queue<GridMoveStep>> MovePaths =
        new Stack<Queue<GridMoveStep>>();
    private static readonly Stack<GridSearchPriorityQueue> PriorityQueues =
        new Stack<GridSearchPriorityQueue>();
    private static readonly Stack<GridSearchWorkspace> Workspaces =
        new Stack<GridSearchWorkspace>();
    private static readonly Stack<SparseGridSearchWorkspace> SparseWorkspaces =
        new Stack<SparseGridSearchWorkspace>();

    [ThreadStatic] private static List<IGridOccupant> sharedOccupants;

    public static List<IGridOccupant> SharedOccupants =>
        sharedOccupants ??= new List<IGridOccupant>(8);

    public static Queue<Vector2Int> RentPositionQueue()
    {
        return PositionQueues.Count > 0 ? PositionQueues.Pop() : new Queue<Vector2Int>(128);
    }

    public static Queue<GridMoveStep> RentMovePath(int capacity = 0)
    {
        return MovePaths.Count > 0
            ? MovePaths.Pop()
            : new Queue<GridMoveStep>(Mathf.Max(4, capacity));
    }

    public static List<GridTraversalStepData> RentTraversalStepList()
    {
        return TraversalStepLists.Count > 0
            ? TraversalStepLists.Pop()
            : new List<GridTraversalStepData>(8);
    }

    public static List<Vector2Int> RentPositionList()
    {
        return PositionLists.Count > 0 ? PositionLists.Pop() : new List<Vector2Int>(128);
    }

    public static List<IGridOccupant> RentOccupantList()
    {
        return OccupantLists.Count > 0 ? OccupantLists.Pop() : new List<IGridOccupant>(8);
    }

    public static HashSet<IGridOccupant> RentOccupantSet()
    {
        return OccupantSets.Count > 0 ? OccupantSets.Pop() : new HashSet<IGridOccupant>();
    }

    public static GridSearchPriorityQueue RentPriorityQueue()
    {
        return PriorityQueues.Count > 0
            ? PriorityQueues.Pop()
            : new GridSearchPriorityQueue();
    }

    public static GridSearchWorkspace RentWorkspace(int cellCount)
    {
        GridSearchWorkspace workspace = Workspaces.Count > 0
            ? Workspaces.Pop()
            : new GridSearchWorkspace();
        workspace.Prepare(cellCount);
        return workspace;
    }

    public static SparseGridSearchWorkspace RentSparseWorkspace()
    {
        SparseGridSearchWorkspace workspace =
            SparseWorkspaces.Count > 0
                ? SparseWorkspaces.Pop()
                : new SparseGridSearchWorkspace();
        workspace.Prepare();
        return workspace;
    }

    public static void EnsureIncrementalSearchCapacity(int count)
    {
        int target = Mathf.Max(0, count);
        while (SparseWorkspaces.Count < target)
        {
            SparseWorkspaces.Push(new SparseGridSearchWorkspace());
        }

        while (PriorityQueues.Count < target)
        {
            PriorityQueues.Push(new GridSearchPriorityQueue());
        }
    }

    public static void Return(Queue<Vector2Int> queue)
    {
        if (queue == null) return;

        queue.Clear();
        PositionQueues.Push(queue);
    }

    public static void ReturnMovePath(Queue<GridMoveStep> path)
    {
        if (path == null)
        {
            return;
        }

        path.Clear();
        MovePaths.Push(path);
    }

    public static void ReturnTraversalStepList(List<GridTraversalStepData> list)
    {
        if (list == null) return;

        list.Clear();
        TraversalStepLists.Push(list);
    }

    public static void ReturnPositionList(List<Vector2Int> list)
    {
        if (list == null) return;

        list.Clear();
        PositionLists.Push(list);
    }

    public static void ReturnOccupantList(List<IGridOccupant> list)
    {
        if (list == null) return;

        list.Clear();
        OccupantLists.Push(list);
    }

    public static void Return(HashSet<IGridOccupant> set)
    {
        if (set == null) return;

        set.Clear();
        OccupantSets.Push(set);
    }

    public static void Return(GridSearchPriorityQueue queue)
    {
        if (queue == null) return;

        queue.Clear();
        PriorityQueues.Push(queue);
    }

    public static void Return(GridSearchWorkspace workspace)
    {
        if (workspace == null)
        {
            return;
        }

        workspace.ReleaseReferences();
        Workspaces.Push(workspace);
    }

    public static void Return(SparseGridSearchWorkspace workspace)
    {
        if (workspace == null)
        {
            return;
        }

        workspace.ReleaseReferences();
        SparseWorkspaces.Push(workspace);
    }

    internal static void ClearRetainedMemory()
    {
        PositionQueues.Clear();
        PositionLists.Clear();
        TraversalStepLists.Clear();
        OccupantLists.Clear();
        OccupantSets.Clear();
        MovePaths.Clear();
        PriorityQueues.Clear();
        Workspaces.Clear();
        SparseWorkspaces.Clear();
        sharedOccupants = null;
    }
}
