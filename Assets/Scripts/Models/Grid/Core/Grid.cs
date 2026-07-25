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

    private readonly Dictionary<Vector2Int, GridMoveStep> parentStep;
    private readonly List<Vector2Int> searchOrder;
    private readonly HashSet<Vector2Int> reachablePositionSet;
    private readonly List<IGridOccupant> visitableOccupants;
    private readonly HashSet<IGridOccupant> visitableOccupantSet;
    private readonly Dictionary<IGridOccupant, Vector2Int> visitableOccupantPositions =
        new Dictionary<IGridOccupant, Vector2Int>();
    private readonly Dictionary<Vector2Int, int> moveDistanceCache = new Dictionary<Vector2Int, int>();
    private bool visitableOccupantPositionsBuilt;

    public GridPathSearchResult(
        Grid sourceGrid,
        Vector2Int start,
        int gridVersion,
        Dictionary<Vector2Int, GridMoveStep> parentStep,
        List<Vector2Int> searchOrder,
        List<IGridOccupant> visitableOccupants)
    {
        this.sourceGrid = sourceGrid;
        this.start = start;
        this.gridVersion = gridVersion;
        this.parentStep = parentStep;
        this.searchOrder = searchOrder;
        reachablePositionSet = new HashSet<Vector2Int>(searchOrder);
        this.visitableOccupants = visitableOccupants;
        visitableOccupantSet = new HashSet<IGridOccupant>(visitableOccupants);
    }

    public List<IGridOccupant> GetAllVisitableOccupants()
    {
        return new List<IGridOccupant>(visitableOccupants);
    }

    public bool ContainsVisitableOccupant(IGridOccupant occupant)
    {
        return occupant != null && visitableOccupantSet.Contains(occupant);
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

    public List<IGridOccupant> GetAllReachableOccupants()
    {
        List<IGridOccupant> result = new List<IGridOccupant>();
        foreach (Vector2Int pos in searchOrder)
        {
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
        return new List<Vector2Int>(searchOrder);
    }

    public bool ContainsPosition(Vector2Int position)
    {
        return reachablePositionSet.Contains(position);
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
            foreach (Vector2Int pos in searchOrder)
            {
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

        foreach (Vector2Int pos in searchOrder)
        {
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

        foreach (Vector2Int pos in searchOrder)
        {
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

        foreach (Vector2Int pos in searchOrder)
        {
            GridCell cell = sourceGrid.GetGridCell(pos);
            if (cell != null && cell.ContainsOccupant(destination))
            {
                return BuildMovePath(pos, destination);
            }
        }

        return new Queue<GridMoveStep>();
    }

    public Queue<GridMoveStep> GetMovePath(Func<Vector2Int, bool> terminateEndCondition)
    {
        if (terminateEndCondition == null) return new Queue<GridMoveStep>();

        foreach (Vector2Int pos in searchOrder)
        {
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
        List<GridMoveStep> path = new List<GridMoveStep>();
        if (end == start) return new Queue<GridMoveStep>();

        Vector2Int current = end;
        while (current != start && parentStep.TryGetValue(current, out GridMoveStep step))
        {
            if (current == end && destination != null)
            {
                step = step.WithDestination(destination);
            }

            path.Add(step);
            current = step.From;
        }

        if (current != start) return new Queue<GridMoveStep>();

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
        foreach (Vector2Int pos in searchOrder)
        {
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
        if (end == start)
        {
            return 0;
        }

        if (moveDistanceCache.TryGetValue(end, out int cachedDistance))
        {
            return cachedDistance;
        }

        int distance = 0;
        Vector2Int current = end;
        while (current != start)
        {
            if (!parentStep.TryGetValue(current, out GridMoveStep step))
            {
                moveDistanceCache[end] = int.MaxValue;
                return int.MaxValue;
            }

            distance++;
            current = step.From;
        }

        moveDistanceCache[end] = distance;
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
    public Vector3 OriginPosition => originPos;
    public int CellWorldHeight => cellWorldHeight;

    private readonly GridCell[,] gridArray;
    private readonly int[,] searchMarks;
    private Vector3 originPos;
    private int cellWorldHeight;
    private int currentSearchMark;

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

        gridArray = new GridCell[height, width];
        searchMarks = new int[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                gridArray[y, x] = new GridCell(pos);
            }
        }
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

    public IEnumerable<GridCell> GetCells()
    {
        foreach (GridCell cell in gridArray)
        {
            yield return cell;
        }
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
            version++;
        }

        return changed;
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

        version++;
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
        foreach (Vector2Int tempPos in targetPositions)
        {
            GridCell cell = GetGridCell(tempPos);
            changed = changed || cell.HasOccupantInLayer(layer) || (disconnectPositions && cell.TraversalLinks.Any());
            cell.RemoveOccupantByLayer(layer);
            if (disconnectPositions)
            {
                cell.SetTraversalLinks(null);
            }
        }

        if (changed)
        {
            version++;
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
        foreach (Vector2Int position in targetPositions)
        {
            GridCell cell = GetGridCell(position);
            if (!ReferenceEquals(cell.GetOccupant(layer), expectedOccupant))
            {
                continue;
            }

            cell.RemoveOccupantByLayer(layer);
            if (disconnectPositions)
            {
                cell.SetTraversalLinks(null);
            }

            changed = true;
        }

        if (changed)
        {
            version++;
        }

        return changed;
    }

    public GridPathSearchResult SearchPath(Vector2Int start)
    {
        return SearchPath(start, null);
    }

    private GridPathSearchResult SearchPath(Vector2Int start, Func<Vector2Int, bool> stopCondition)
    {
        Dictionary<Vector2Int, GridMoveStep> parentStep = new Dictionary<Vector2Int, GridMoveStep>();
        List<Vector2Int> searchOrder = new List<Vector2Int>();
        List<IGridOccupant> visitableOccupants = new List<IGridOccupant>();
        if (!IsValidGridPos(start))
        {
            return new GridPathSearchResult(this, start, version, parentStep, searchOrder, visitableOccupants);
        }

        Vector2Int[] dir = { Vector2Int.left, Vector2Int.right };
        Queue<Vector2Int> queue = GridSearchScratch.RentPositionQueue();
        List<GridMoveStep> nextSteps = GridSearchScratch.RentMoveStepList();
        List<IGridOccupant> currentOccupants = GridSearchScratch.RentOccupantList();
        HashSet<IGridOccupant> visitableOccupantSet = GridSearchScratch.RentOccupantSet();
        int searchMark = NextSearchMark();

        try
        {
            queue.Enqueue(start);
            searchMarks[start.y, start.x] = searchMark;

            while (queue.Count > 0)
            {
                Vector2Int pos = queue.Dequeue();
                searchOrder.Add(pos);
                nextSteps.Clear();

                GridCell cell = GetGridCell(pos);
                if (cell == null) continue;

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

                if (stopCondition != null && stopCondition(pos))
                {
                    break;
                }

                foreach (GridTraversalLink link in cell.TraversalLinks)
                {
                    AddMoveStep(nextSteps, pos, link.To, link.Through, link.MoveType);
                }

                foreach (Vector2Int nextPos in dir)
                {
                    AddMoveStep(nextSteps, pos, nextPos + pos, null, GridMoveType.Walk);
                }

                foreach (GridMoveStep step in nextSteps)
                {
                    Vector2Int nextPos = step.To;
                    GridCell nextCell = GetGridCell(nextPos);
                    bool isAllowedTerminal = stopCondition != null
                        && stopCondition(nextPos)
                        && !IsMovementBlockedByWall(nextPos);
                    if (nextCell != null
                        && searchMarks[nextPos.y, nextPos.x] != searchMark
                        && (IsWalkable(nextPos) || isAllowedTerminal))
                    {
                        queue.Enqueue(nextPos);
                        searchMarks[nextPos.y, nextPos.x] = searchMark;
                        parentStep[nextPos] = step;
                    }
                }
            }
        }
        finally
        {
            GridSearchScratch.Return(queue);
            GridSearchScratch.Return(nextSteps);
            GridSearchScratch.ReturnOccupantList(currentOccupants);
            GridSearchScratch.Return(visitableOccupantSet);
            GridSearchScratch.SharedOccupants.Clear();
        }

        return new GridPathSearchResult(this, start, version, parentStep, searchOrder, visitableOccupants);
    }

    public Queue<IGridOccupant> GetOccupantPath(Vector2Int start, Func<Vector2Int, bool> terminateEndCondition)
    {
        return SearchPath(start, terminateEndCondition).GetOccupantPath(terminateEndCondition);
    }

    public Queue<GridMoveStep> GetMovePath(Vector2Int start, Func<Vector2Int, bool> terminateEndCondition)
    {
        return SearchPath(start, terminateEndCondition).GetMovePath(terminateEndCondition);
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
        List<IGridOccupant> result = new List<IGridOccupant>();
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                GridSearchScratch.SharedOccupants.Clear();
                gridArray[i, j].FillAllOccupants(GridSearchScratch.SharedOccupants);
                foreach (IGridOccupant occupant in GridSearchScratch.SharedOccupants)
                {
                    if (occupant != null && !result.Contains(occupant) && (predicate == null || predicate(occupant)))
                    {
                        result.Add(occupant);
                    }
                }
            }
        }

        GridSearchScratch.SharedOccupants.Clear();
        return result;
    }

    private int NextSearchMark()
    {
        currentSearchMark++;
        if (currentSearchMark == int.MaxValue)
        {
            Array.Clear(searchMarks, 0, searchMarks.Length);
            currentSearchMark = 1;
        }

        return currentSearchMark;
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

    private void AddMoveStep(
        List<GridMoveStep> steps,
        Vector2Int from,
        Vector2Int to,
        IGridOccupant movementOccupant,
        GridMoveType moveType)
    {
        GridCell nextCell = GetGridCell(to);
        IGridOccupant destinationOccupant = nextCell?.GetTopOccupant();
        steps.Add(new GridMoveStep(from, to, destinationOccupant, movementOccupant, moveType));
    }
}

internal static class GridSearchScratch
{
    private static readonly Stack<Queue<Vector2Int>> PositionQueues = new Stack<Queue<Vector2Int>>();
    private static readonly Stack<List<Vector2Int>> PositionLists = new Stack<List<Vector2Int>>();
    private static readonly Stack<List<GridMoveStep>> MoveStepLists = new Stack<List<GridMoveStep>>();
    private static readonly Stack<List<IGridOccupant>> OccupantLists = new Stack<List<IGridOccupant>>();
    private static readonly Stack<HashSet<IGridOccupant>> OccupantSets = new Stack<HashSet<IGridOccupant>>();

    [ThreadStatic] private static List<IGridOccupant> sharedOccupants;

    public static List<IGridOccupant> SharedOccupants =>
        sharedOccupants ??= new List<IGridOccupant>(8);

    public static Queue<Vector2Int> RentPositionQueue()
    {
        return PositionQueues.Count > 0 ? PositionQueues.Pop() : new Queue<Vector2Int>(128);
    }

    public static List<GridMoveStep> RentMoveStepList()
    {
        return MoveStepLists.Count > 0 ? MoveStepLists.Pop() : new List<GridMoveStep>(8);
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

    public static void Return(Queue<Vector2Int> queue)
    {
        if (queue == null) return;

        queue.Clear();
        PositionQueues.Push(queue);
    }

    public static void Return(List<GridMoveStep> list)
    {
        if (list == null) return;

        list.Clear();
        MoveStepLists.Push(list);
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
}
