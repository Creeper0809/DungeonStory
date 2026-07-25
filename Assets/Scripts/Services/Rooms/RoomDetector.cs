using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RoomDetector
{
    private static readonly Vector2Int[] RoomNeighbors =
    {
        Vector2Int.left,
        Vector2Int.right
    };

    private static readonly Vector2Int[] BoundaryDirections =
    {
        Vector2Int.left,
        Vector2Int.right
    };

    public static RoomLayout Build(Grid grid)
    {
        if (grid == null)
        {
            return new RoomLayout(System.Array.Empty<RoomInstance>());
        }

        RoomOccupancySnapshot snapshot = RoomOccupancySnapshot.Build(grid);
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        List<RoomInstance> rooms = new List<RoomInstance>();
        int nextId = 1;

        foreach (GridCell cell in grid.GetCells())
        {
            if (cell == null
                || visited.Contains(cell.Position)
                || !IsInteriorCell(grid, snapshot, cell.Position))
            {
                continue;
            }

            List<Vector2Int> cells = CollectConnectedInteriorCells(grid, snapshot, cell.Position, visited);
            if (cells.Count == 0)
            {
                continue;
            }

            RoomBoundaryInfo boundary = AnalyzeBoundary(grid, snapshot, cells);
            List<BuildableObject> furniture = CollectFurniture(snapshot, cells);
            rooms.Add(new RoomInstance(
                nextId++,
                cells,
                furniture,
                boundary.Doors,
                boundary.Walls,
                boundary.SolidBoundaryCount,
                boundary.OpenBoundaryCount));
        }

        AddSelfContainedFacilityRooms(grid, rooms, ref nextId);

        return new RoomLayout(rooms);
    }

    internal static bool IsDoor(BuildableObject building)
    {
        if (building == null)
        {
            return false;
        }

        if (building is Door)
        {
            return true;
        }

        return building.BuildingData?.IsDoor == true;
    }

    internal static bool IsWall(BuildableObject building)
    {
        if (building == null)
        {
            return false;
        }

        return building.BuildingData != null
            ? building.BuildingData.IsStructuralWall
            : building.category == BuildingCategory.Wall;
    }

    private static bool IsInteriorCell(Grid grid, RoomOccupancySnapshot snapshot, Vector2Int position)
    {
        GridCell cell = grid.GetGridCell(position);
        return cell != null
            && cell.AreaType == GridCellAreaType.DungeonInterior
            && grid.IsWalkable(position)
            && !snapshot.HasDoor(position)
            && !snapshot.HasWall(position);
    }

    private static List<Vector2Int> CollectConnectedInteriorCells(
        Grid grid,
        RoomOccupancySnapshot snapshot,
        Vector2Int start,
        HashSet<Vector2Int> visited)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        visited.Add(start);
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            cells.Add(current);

            foreach (Vector2Int direction in RoomNeighbors)
            {
                Vector2Int next = current + direction;
                if (visited.Contains(next) || !IsInteriorCell(grid, snapshot, next))
                {
                    continue;
                }

                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return cells;
    }

    private static RoomBoundaryInfo AnalyzeBoundary(
        Grid grid,
        RoomOccupancySnapshot snapshot,
        IReadOnlyList<Vector2Int> cells)
    {
        HashSet<Vector2Int> cellSet = new HashSet<Vector2Int>(cells);
        RoomBoundaryInfo result = new RoomBoundaryInfo();

        foreach (Vector2Int cell in cells)
        {
            foreach (Vector2Int direction in BoundaryDirections)
            {
                Vector2Int neighbor = cell + direction;
                if (cellSet.Contains(neighbor))
                {
                    continue;
                }

                if (!grid.IsValidGridPos(neighbor))
                {
                    result.SolidBoundaryCount++;
                    continue;
                }

                if (snapshot.TryGetDoor(neighbor, out BuildableObject door))
                {
                    result.AddDoor(door);
                    continue;
                }

                if (snapshot.TryGetWall(neighbor, out BuildableObject wall))
                {
                    result.AddWall(wall);
                    result.SolidBoundaryCount++;
                    continue;
                }

                if (!grid.IsWalkable(neighbor))
                {
                    result.SolidBoundaryCount++;
                    foreach (BuildableObject solid in snapshot.GetBuildings(neighbor))
                    {
                        if (solid != null)
                        {
                            result.AddWall(solid);
                        }
                    }

                    continue;
                }

                result.OpenBoundaryCount++;
            }
        }

        return result;
    }

    private static List<BuildableObject> CollectFurniture(
        RoomOccupancySnapshot snapshot,
        IReadOnlyList<Vector2Int> cells)
    {
        List<BuildableObject> result = new List<BuildableObject>();
        foreach (Vector2Int cell in cells)
        {
            foreach (BuildableObject building in snapshot.GetBuildings(cell))
            {
                if (building == null
                    || IsDoor(building)
                    || IsWall(building)
                    || building.Facility == null
                    || building.Facility.roles == FacilityRole.None
                    || result.Contains(building))
                {
                    continue;
                }

                result.Add(building);
            }
        }

        return result;
    }

    private static void AddSelfContainedFacilityRooms(
        Grid grid,
        List<RoomInstance> rooms,
        ref int nextId)
    {
        foreach (IGridOccupant occupant in grid.FindAllOccupants(null))
        {
            if (occupant is not BuildableObject building
                || building.isDestroy
                || !IsSelfContainedFacilityRoom(building))
            {
                continue;
            }

            bool belongsToFormalRoom = rooms.Any((room) => room != null
                && !room.IsSelfContained
                && room.ContainsPart(building));
            if (belongsToFormalRoom)
            {
                continue;
            }

            List<Vector2Int> cells = GetValidBuildCells(grid, building);
            if (cells.Count == 0)
            {
                continue;
            }

            rooms.Add(new RoomInstance(
                nextId++,
                cells,
                new[] { building },
                System.Array.Empty<BuildableObject>(),
                System.Array.Empty<BuildableObject>(),
                cells.Count * 2,
                0,
                selfContained: true));
        }
    }

    private static bool IsSelfContainedFacilityRoom(BuildableObject building)
    {
        return building != null
            && !IsDoor(building)
            && !IsWall(building)
            && building.BuildingData != null
            && building.BuildingData.Facility != null
            && building.BuildingData.IsSelfContainedRoom()
            && building.BuildingData.Facility.roles != FacilityRole.None;
    }

    private static List<Vector2Int> GetValidBuildCells(Grid grid, BuildableObject building)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        IReadOnlyList<Vector2Int> positions = building.buildPoses;
        if (positions == null || positions.Count == 0)
        {
            positions = new[] { building.centerPos };
        }

        foreach (Vector2Int position in positions)
        {
            GridCell cell = grid.GetGridCell(position);
            if (cell != null
                && cell.AreaType == GridCellAreaType.DungeonInterior
                && !cells.Contains(position))
            {
                cells.Add(position);
            }
        }

        return cells;
    }

    private sealed class RoomBoundaryInfo
    {
        private readonly HashSet<BuildableObject> doorSet = new HashSet<BuildableObject>();
        private readonly HashSet<BuildableObject> wallSet = new HashSet<BuildableObject>();

        public readonly List<BuildableObject> Doors = new List<BuildableObject>();
        public readonly List<BuildableObject> Walls = new List<BuildableObject>();
        public int SolidBoundaryCount;
        public int OpenBoundaryCount;

        public void AddDoor(BuildableObject door)
        {
            if (door != null && doorSet.Add(door))
            {
                Doors.Add(door);
            }
        }

        public void AddWall(BuildableObject wall)
        {
            if (wall != null && wallSet.Add(wall))
            {
                Walls.Add(wall);
            }
        }
    }

    private sealed class RoomOccupancySnapshot
    {
        private readonly Dictionary<Vector2Int, List<BuildableObject>> buildingsByCell =
            new Dictionary<Vector2Int, List<BuildableObject>>();
        private readonly Dictionary<Vector2Int, BuildableObject> doorsByCell =
            new Dictionary<Vector2Int, BuildableObject>();
        private readonly Dictionary<Vector2Int, BuildableObject> wallsByCell =
            new Dictionary<Vector2Int, BuildableObject>();

        public static RoomOccupancySnapshot Build(Grid grid)
        {
            RoomOccupancySnapshot snapshot = new RoomOccupancySnapshot();
            foreach (IGridOccupant occupant in grid.FindAllOccupants(null))
            {
                if (occupant is not BuildableObject building || building.isDestroy)
                {
                    continue;
                }

                IReadOnlyList<Vector2Int> positions = building.buildPoses;
                if (positions == null || positions.Count == 0)
                {
                    positions = new[] { building.centerPos };
                }

                foreach (Vector2Int position in positions.Where(grid.IsValidGridPos))
                {
                    if (!snapshot.buildingsByCell.TryGetValue(position, out List<BuildableObject> buildings))
                    {
                        buildings = new List<BuildableObject>();
                        snapshot.buildingsByCell[position] = buildings;
                    }

                    if (!buildings.Contains(building))
                    {
                        buildings.Add(building);
                    }

                    if (IsDoor(building))
                    {
                        snapshot.doorsByCell[position] = building;
                    }
                    else if (IsWall(building))
                    {
                        snapshot.wallsByCell[position] = building;
                    }
                }
            }

            return snapshot;
        }

        public bool HasDoor(Vector2Int cell)
        {
            return doorsByCell.ContainsKey(cell);
        }

        public bool HasWall(Vector2Int cell)
        {
            return wallsByCell.ContainsKey(cell);
        }

        public bool TryGetDoor(Vector2Int cell, out BuildableObject door)
        {
            return doorsByCell.TryGetValue(cell, out door);
        }

        public bool TryGetWall(Vector2Int cell, out BuildableObject wall)
        {
            return wallsByCell.TryGetValue(cell, out wall);
        }

        public IReadOnlyList<BuildableObject> GetBuildings(Vector2Int cell)
        {
            return buildingsByCell.TryGetValue(cell, out List<BuildableObject> buildings)
                ? buildings
                : System.Array.Empty<BuildableObject>();
        }
    }
}
