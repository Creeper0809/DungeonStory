#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PhysicalWorldGridDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Grid Foundation/Run Physical World Grid Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Physical world grid scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        Run("area flags gate movement, item, and building layers", VerifyAreaFlags, errors);
        Run("default manager opens only the exterior ground lane", VerifyDefaultManagerAreas, errors);
        Run("centered manager balances exterior around the dungeon", VerifyCenteredManagerAreas, errors);
        Run("drop zone query prefers tagged drop zone", VerifyDropZoneQuery, errors);
        Run("room detector excludes exterior cells", VerifyRoomDetectorExcludesExterior, errors);
        Run("search path crosses exterior into entrance", VerifyExteriorPathToEntrance, errors);
        Run("construction overlay excludes exterior facilities", VerifyConstructionOverlayExcludesExterior, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Physical world grid scenarios passed.");
        }

        return true;
    }

    private static bool VerifyAreaFlags()
    {
        Grid grid = CreateTaggedGrid();
        AddHallway(grid, new Vector2Int(3, 0));
        GridCell exterior = grid.GetGridCell(new Vector2Int(0, 0));
        GridCell dropZone = grid.GetGridCell(new Vector2Int(1, 0));
        GridCell entrance = grid.GetGridCell(new Vector2Int(2, 0));
        GridCell upperExterior = grid.GetGridCell(new Vector2Int(0, 1));
        GridCell interior = grid.GetGridCell(new Vector2Int(3, 0));

        return grid.IsWalkable(exterior.Position)
            && grid.IsWalkable(dropZone.Position)
            && grid.IsWalkable(entrance.Position)
            && grid.IsWalkable(interior.Position)
            && grid.IsWalkable(upperExterior.Position)
            && exterior.CanOccupy(GridLayer.Character)
            && exterior.CanOccupy(GridLayer.Item)
            && !exterior.IsBuildableArea
            && dropZone.AllowsItemDrop
            && upperExterior.AllowsItemDrop
            && entrance.CanOccupy(GridLayer.Building)
            && interior.IsBuildableArea;
    }

    private static bool VerifyDefaultManagerAreas()
    {
        Grid grid = new Grid(36, 3);
        GridSystemManager.ApplyPhysicalWorldAreas(
            grid,
            dungeonInteriorStartX: 4,
            dungeonInteriorColumnCount: 27,
            dropZoneWidth: 3,
            entranceGridPosition: new Vector2Int(4, 0));

        int total = grid.GetCells().Count(cell => cell != null);
        int interior = grid.GetCells().Count(cell =>
            cell != null && cell.AreaType == GridCellAreaType.DungeonInterior);
        int exterior = grid.GetCells().Count(cell =>
            cell != null && cell.AreaType == GridCellAreaType.ExteriorPath);
        int dropZone = grid.GetCells().Count(cell =>
            cell != null && cell.AreaType == GridCellAreaType.DropZone);
        int entrance = grid.GetCells().Count(cell =>
            cell != null && cell.AreaType == GridCellAreaType.Entrance);
        int blocked = grid.GetCells().Count(cell =>
            cell != null && cell.AreaType == GridCellAreaType.BlockedExterior);

        return total == 108
            && interior == 80
            && exterior == 6
            && dropZone == 3
            && entrance == 1
            && blocked == 18;
    }

    private static bool VerifyCenteredManagerAreas()
    {
        Grid grid = new Grid(60, 3);
        int interiorStart = GridSystemManager.CalculateCenteredDungeonInteriorStartX(
            grid.width,
            27);
        Vector2Int entrance = new Vector2Int(interiorStart, 0);
        GridSystemManager.ApplyPhysicalWorldAreas(
            grid,
            interiorStart,
            dungeonInteriorColumnCount: 27,
            dropZoneWidth: 3,
            entranceGridPosition: entrance);

        bool rightExterior = Enumerable.Range(0, 14).All(x =>
            grid.GetGridCell(new Vector2Int(x, 0))?.AreaType == GridCellAreaType.ExteriorPath);
        bool dropZone = Enumerable.Range(14, 3).All(x =>
            grid.GetGridCell(new Vector2Int(x, 0))?.AreaType == GridCellAreaType.DropZone);
        bool interior = Enumerable.Range(18, 26).All(x =>
            grid.GetGridCell(new Vector2Int(x, 0))?.AreaType == GridCellAreaType.DungeonInterior);
        bool leftExterior = Enumerable.Range(44, 16).All(x =>
            grid.GetGridCell(new Vector2Int(x, 0))?.AreaType == GridCellAreaType.ExteriorPath);

        return interiorStart == 17
            && grid.GetGridCell(entrance)?.AreaType == GridCellAreaType.Entrance
            && rightExterior
            && dropZone
            && interior
            && leftExterior;
    }

    private static bool VerifyDropZoneQuery()
    {
        Grid grid = CreateTaggedGrid();
        WorldDropZoneQuery query = new WorldDropZoneQuery(
            new StaticGridSystemProvider(grid),
            new NoSpawnerProvider());

        bool deliveryResolved = query.TryGetDeliveryDropoff(out Vector2Int delivery);
        bool lootResolved = query.TryGetExpeditionLootDropoff(out Vector2Int loot);
        bool entryResolved = query.TryGetVisitorEntryPoint(out WorldGridEntryPoint entry);

        return deliveryResolved
            && lootResolved
            && entryResolved
            && delivery == new Vector2Int(1, 0)
            && loot == new Vector2Int(1, 0)
            && entry.GridPosition == new Vector2Int(2, 0);
    }

    private static bool VerifyRoomDetectorExcludesExterior()
    {
        Grid grid = CreateTaggedGrid();
        AddHallway(grid, new Vector2Int(3, 0));
        AddHallway(grid, new Vector2Int(4, 0));
        RoomLayout layout = RoomDetector.Build(grid);

        return layout.Rooms.Count > 0
            && layout.Rooms
                .Where(room => room != null && !room.IsSelfContained)
                .SelectMany(room => room.Cells)
                .All(position => grid.GetGridCell(position)?.AreaType == GridCellAreaType.DungeonInterior);
    }

    private static bool VerifyExteriorPathToEntrance()
    {
        Grid grid = CreateTaggedGrid();
        AddHallway(grid, new Vector2Int(3, 0));
        AddHallway(grid, new Vector2Int(4, 0));
        Queue<GridMoveStep> path = grid.GetMovePath(
            new Vector2Int(0, 0),
            position => position == new Vector2Int(4, 0));

        return path.Count == 4
            && path.Select(step => step.To).SequenceEqual(new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(3, 0),
                new Vector2Int(4, 0)
            });
    }

    private static bool VerifyConstructionOverlayExcludesExterior()
    {
        BuildingSO facility = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/Q01_연구책상.asset");
        BuildingSO dungeonDoor = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Door.asset");
        if (facility == null || dungeonDoor == null || !dungeonDoor.IsDoor || dungeonDoor.IsInteriorDoor)
        {
            return false;
        }

        Grid grid = CreateTaggedGrid();
        AddHallway(grid, new Vector2Int(3, 0));
        AddHallway(grid, new Vector2Int(4, 0));
        HashSet<Vector2Int> facilityCells = GridPlacementCellAvailability.CollectInstallableCells(
            grid,
            facility,
            sidePadding: 0);

        GridCell exterior = grid.GetGridCell(new Vector2Int(0, 0));
        GridCell dropZone = grid.GetGridCell(new Vector2Int(1, 0));
        GridCell entrance = grid.GetGridCell(new Vector2Int(2, 0));
        return facilityCells.Count > 0
            && facilityCells.All(position =>
                grid.GetGridCell(position)?.AreaType == GridCellAreaType.DungeonInterior)
            && !exterior.CanBuildInArea(facility)
            && !dropZone.CanBuildInArea(facility)
            && !entrance.CanBuildInArea(facility)
            && dropZone.CanBuildInArea(dungeonDoor)
            && entrance.CanBuildInArea(dungeonDoor);
    }

    private static Grid CreateTaggedGrid()
    {
        Grid grid = new Grid(5, 2);
        grid.SetAreaType(new Vector2Int(0, 0), GridCellAreaType.ExteriorPath);
        grid.SetAreaType(new Vector2Int(1, 0), GridCellAreaType.DropZone);
        grid.SetAreaType(new Vector2Int(2, 0), GridCellAreaType.Entrance);
        grid.SetAreaType(new Vector2Int(0, 1), GridCellAreaType.ExteriorPath);
        grid.SetAreaType(new Vector2Int(1, 1), GridCellAreaType.ExteriorPath);
        grid.SetAreaType(new Vector2Int(2, 1), GridCellAreaType.ExteriorPath);
        return grid;
    }

    private static void AddHallway(Grid grid, Vector2Int position)
    {
        grid.RegisterOccupant(
            new TestOccupant(1, true, GridMoveType.Walk),
            GridLayer.Hallway,
            new List<Vector2Int> { position },
            false);
    }

    private static void Run(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (!scenario())
            {
                errors.Add($"{name} failed.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{name} failed: {ex.Message}");
        }
    }

    private sealed class StaticGridSystemProvider : IGridSystemProvider
    {
        private readonly Grid grid;

        public StaticGridSystemProvider(Grid grid)
        {
            this.grid = grid;
        }

        public GridSystemManager Manager => null;
        public Grid Grid => grid;

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid resolvedGrid)
        {
            resolvedGrid = grid;
            return resolvedGrid != null;
        }
    }

    private sealed class NoSpawnerProvider : ICharacterSpawnerProvider
    {
        public bool TryGetSpawner(out CharacterSpawner spawner)
        {
            spawner = null;
            return false;
        }
    }

    private sealed class TestOccupant : IGridOccupant, IGridMovementOccupant
    {
        public TestOccupant(int id, bool isMovement, GridMoveType moveType)
        {
            GridId = id;
            IsGridMovement = isMovement;
            GridMoveType = moveType;
        }

        public int GridId { get; }
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement { get; }
        public GridMoveType GridMoveType { get; }
    }
}
#endif
