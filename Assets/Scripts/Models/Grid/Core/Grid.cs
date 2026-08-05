using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

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
    private readonly GridTraversalHeuristicIndex traversalHeuristics;
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
        traversalHeuristics = new GridTraversalHeuristicIndex(height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                gridArray[y, x] = new GridCell(pos);
            }
        }

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

    /// <summary>
    /// Creates an occupant-free copy of the authored grid layout. Restore code
    /// can populate this detached grid without exposing partial world state.
    /// </summary>
    public Grid CreateDetachedLayoutCopy()
    {
        Grid copy = new Grid(width, height, originPos, cellWorldHeight);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                GridCell source = gridArray[y, x];
                copy.SetAreaType(position, source.AreaType);
                copy.SetTerrainType(position, source.TerrainType);
            }
        }

        return copy;
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

    public bool TryGetCellIndex(Vector2Int position, out int index)
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

    public Vector2Int GetPositionFromCellIndex(int index)
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
            if (cell == null
                || !cell.CanOccupy(layer)
                || cell.ContainsOccupant(layer, occupant))
            {
                return false;
            }
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
        List<IGridOccupant> removedOccupants =
            layer == GridLayer.Utility
                ? new List<IGridOccupant>(3)
                : null;
        foreach (Vector2Int tempPos in targetPositions)
        {
            GridCell cell = GetGridCell(tempPos);
            removedOccupants?.Clear();
            if (removedOccupants != null)
            {
                cell.FillOccupantsInLayer(layer, removedOccupants);
            }

            IGridOccupant removedOccupant = removedOccupants == null
                ? cell.GetOccupant(layer)
                : null;
            structuralChange |= removedOccupant != null
                && removedOccupant.IsGridMovement;
            if (removedOccupants != null)
            {
                structuralChange |= removedOccupants.Any(
                    occupant => occupant != null && occupant.IsGridMovement);
            }
            changed = changed || cell.HasOccupantInLayer(layer) || (disconnectPositions && cell.TraversalLinks.Any());
            cell.RemoveOccupantByLayer(layer);
            RemoveOccupantReferences(removedOccupant, 1);
            if (removedOccupants != null)
            {
                foreach (IGridOccupant utilityOccupant in removedOccupants)
                {
                    RemoveOccupantReferences(utilityOccupant, 1);
                }
            }
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
            if (!cell.RemoveOccupant(layer, expectedOccupant))
            {
                continue;
            }

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
        Func<Vector2Int, bool> traversalFilter,
        IGridTraversalCostPolicy costPolicy,
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

    public GridPathSearchResult SearchPathTo(
        Vector2Int start,
        Vector2Int destination) =>
        SearchPathTo(
            start,
            destination,
            null,
            DefaultGridTraversalCostPolicy.Instance,
            default);

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
                traversalHeuristics.EstimateRemainingCost(
                    start,
                    exactDestination,
                    costPolicy),
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
                            + traversalHeuristics.EstimateRemainingCost(
                                nextPos,
                                exactDestination,
                                costPolicy);
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

        IGridBuildingOccupantCapability building =
            cell.GetOccupant(GridLayer.Building) as IGridBuildingOccupantCapability;
        if (IsMovementBlockedByWall(pos))
        {
            return false;
        }

        if (cell.AreaType != GridCellAreaType.DungeonInterior)
        {
            return true;
        }

        if (building?.AllowsInteriorWalkability == true)
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
        IGridBuildingOccupantCapability building =
            GetGridCell(pos)?.GetOccupant(GridLayer.Building)
                as IGridBuildingOccupantCapability;
        return building?.BlocksGridMovement == true;
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

    public bool IsConnected(Vector2Int start, int associatedId)
    {
        return GetOccupantPath(start, position =>
        {
            GridCell cell = GetGridCell(position);
            return cell != null
                && cell.GetAllOccupants()
                    .Any(occupant => occupant.GridId == associatedId);
        }).Any();
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

    internal int EstimatePathHeuristicCost(
        Vector2Int position,
        Vector2Int destination,
        IGridTraversalCostPolicy costPolicy)
    {
        return traversalHeuristics.EstimateRemainingCost(
            position,
            destination,
            costPolicy ?? DefaultGridTraversalCostPolicy.Instance);
    }

    public void RefreshTraversalHeuristicMetadata()
    {
        traversalHeuristics.Rebuild(this);
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
            }

            cell.SetTraversalLinks(links);
            foreach (GridTraversalLink link in links)
            {
                traversalHeuristics.ObserveLink(this, from, link);
            }
        }
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
