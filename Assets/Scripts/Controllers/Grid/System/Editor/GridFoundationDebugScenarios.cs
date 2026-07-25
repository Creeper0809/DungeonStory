using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GridFoundationDebugScenarios
{
    private static readonly IBlueprintResearchWorkService BlueprintResearchWorkService =
        new NoopBlueprintResearchWorkService();
    private static readonly IWorldInfoClickSelector WorldInfoClickSelector =
        new NoopWorldInfoClickSelector();
    private static readonly IFacilityCandidateCache FacilityCandidateCacheService =
        new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry);
    private static readonly IRoomFacilityPolicy RoomFacilityPolicyService =
        new RoomFacilityPolicyService(RoomRegistry.EditorCache);

    [MenuItem("DungeonStory/Debug/Grid Foundation/Run 0 Foundation Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Grid foundation scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("walk path", VerifyWalkPath, errors);
        RunScenario("stale walk path is blocked by a newly placed wall", VerifyStaleWalkPathBlockedByWall, errors);
        RunScenario("nearby walkable search does not scan entire floor", VerifyNearbyWalkableSearchDoesNotScanEntireFloor, errors);
        RunScenario("stair path", VerifyStairPath, errors);
        RunScenario("entry exit path", VerifyEntryExitPath, errors);
        RunScenario("unreachable movement excluded", VerifyUnreachableMovementIsExcluded, errors);
        RunScenario("tileless legacy building does not create sprite renderer", VerifyTilelessBuildingDoesNotCreateGeneratedSprite, errors);
        RunScenario("dragged ghost uses repeated sprites", VerifyDraggedGhostUsesRepeatedSprites, errors);
        RunScenario("initial room footprint keeps hallway layer", VerifyInitialRoomFootprintKeepsHallwayLayer, errors);
        RunScenario("placement overlay uses cell installability", VerifyPlacementOverlayCellAvailability, errors);
        RunScenario("door replaces one structural wall cell", VerifyDoorReplacesOneStructuralWallCell, errors);
        RunScenario("spawner entry crosses dungeon door center", VerifySpawnerEntryCrossesDungeonDoorCenter, errors);
        RunScenario("world click grid fallback only accepts exact wall structures", VerifyWorldClickGridFallbackOnlyAcceptsExactWallStructures, errors);

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
            Debug.Log("Grid foundation scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)
    {
        for (int i = 0; i < 10; i++)
        {
            if (scenario()) continue;

            errors.Add($"{name} failed on pass {i + 1}.");
            return;
        }
    }

    private static bool VerifyWalkPath()
    {
        Grid grid = new Grid(4, 1);
        AddHallway(grid, new Vector2Int(0, 0));
        AddHallway(grid, new Vector2Int(1, 0));
        AddHallway(grid, new Vector2Int(2, 0));

        Queue<GridMoveStep> path = grid.GetMovePath(new Vector2Int(0, 0), (pos) => pos == new Vector2Int(2, 0));
        return path.Count == 2 && path.All((step) => step.MoveType == GridMoveType.Walk);
    }

    private static bool VerifyStaleWalkPathBlockedByWall()
    {
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        if (wall == null || !wall.IsStructuralWall)
        {
            return false;
        }

        Grid grid = new Grid(3, 1);
        AddHallway(grid, new Vector2Int(0, 0));
        AddHallway(grid, new Vector2Int(1, 0));
        AddHallway(grid, new Vector2Int(2, 0));

        Vector2Int wallPosition = new Vector2Int(1, 0);
        Queue<GridMoveStep> stalePath = grid.GetMovePath(
            new Vector2Int(0, 0),
            pos => pos == new Vector2Int(2, 0));
        BuildableObject placedWall = new GridBuildingFactory().Create(grid, wall, wallPosition);
        placedWall.SetGrid(grid);
        placedWall.Initialization(wall, wallPosition);
        bool registered = grid.RegisterOccupant(
            placedWall,
            GridLayer.Building,
            wall.GetGridPosList(wallPosition),
            false);

        Queue<GridMoveStep> freshPath = grid.GetMovePath(
            new Vector2Int(0, 0),
            pos => pos == new Vector2Int(2, 0));
        bool valid = registered
            && stalePath.Count == 2
            && stalePath.Peek().To == wallPosition
            && grid.IsMovementBlockedByWall(wallPosition)
            && !grid.IsWalkable(wallPosition)
            && freshPath.Count == 0;

        Object.DestroyImmediate(placedWall.gameObject);
        return valid;
    }

    private static bool VerifyNearbyWalkableSearchDoesNotScanEntireFloor()
    {
        Grid grid = new Grid(6, 1);
        AddHallway(grid, new Vector2Int(5, 0));

        bool ignoredFarCell = !grid.TryFindNearbyWalkablePositionOnSameFloor(
            new Vector2Int(2, 0),
            out _);

        AddHallway(grid, new Vector2Int(3, 0));
        bool foundAdjacentCell = grid.TryFindNearbyWalkablePositionOnSameFloor(
            new Vector2Int(2, 0),
            out Vector2Int nearby)
            && nearby == new Vector2Int(3, 0);

        return ignoredFarCell && foundAdjacentCell;
    }

    private static bool VerifyStairPath()
    {
        Grid grid = new Grid(3, 2);
        AddHallway(grid, new Vector2Int(0, 0));
        AddHallway(grid, new Vector2Int(1, 0));
        AddHallway(grid, new Vector2Int(1, 1));
        AddHallway(grid, new Vector2Int(2, 1));
        AddMovement(grid, new Vector2Int(1, 0), new Vector2Int(1, 1), GridMoveType.Stair);

        Queue<GridMoveStep> path = grid.GetMovePath(new Vector2Int(0, 0), (pos) => pos == new Vector2Int(2, 1));
        return path.Any((step) => step.MoveType == GridMoveType.Stair)
            && path.Last().To == new Vector2Int(2, 1);
    }

    private static bool VerifyEntryExitPath()
    {
        Grid grid = new Grid(4, 1);
        AddHallway(grid, new Vector2Int(0, 0));
        AddHallway(grid, new Vector2Int(1, 0));
        AddHallway(grid, new Vector2Int(2, 0));
        AddHallway(grid, new Vector2Int(3, 0));

        Queue<GridMoveStep> entryPath = grid.GetMovePath(new Vector2Int(0, 0), (pos) => pos == new Vector2Int(3, 0));
        Queue<GridMoveStep> exitPath = grid.GetMovePath(new Vector2Int(3, 0), (pos) => pos == new Vector2Int(0, 0));
        return entryPath.Count == 3
            && exitPath.Count == 3
            && entryPath.All((step) => step.MoveType == GridMoveType.Walk)
            && exitPath.All((step) => step.MoveType == GridMoveType.Walk);
    }

    private static bool VerifyUnreachableMovementIsExcluded()
    {
        Grid grid = new Grid(6, 1);
        TestOccupant reachable = AddHallway(grid, new Vector2Int(0, 0));
        AddHallway(grid, new Vector2Int(1, 0));
        TestOccupant unreachable = AddHallway(grid, new Vector2Int(5, 0));

        List<IGridOccupant> occupants = grid.SearchPath(new Vector2Int(0, 0)).GetAllReachableOccupants();
        return occupants.Contains(reachable) && !occupants.Contains(unreachable);
    }

    private static bool VerifyTilelessBuildingDoesNotCreateGeneratedSprite()
    {
        BuildingSO lab = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_ResearchLab.asset");
        if (lab == null
            || lab.sprite == null
            || (lab.tiles != null && lab.tiles.Count > 0))
        {
            return false;
        }

        Grid grid = new Grid(10, 1);
        for (int x = 0; x < grid.width; x++)
        {
            AddHallway(grid, new Vector2Int(x, 0));
        }

        Vector2Int center = new Vector2Int(4, 0);
        GridBuildingFactory factory = new GridBuildingFactory(InjectBuildingDependencies);
        BuildableObject building = factory.Create(grid, lab, center);
        if (building != null)
        {
            building.SetGrid(grid);
            building.Initialization(lab, center);
        }

        bool placed = building != null && grid.RegisterOccupant(
            building,
            lab.layer,
            lab.GetGridPosList(center),
            false);
        SpriteRenderer renderer = building != null
            ? building.GetComponentInChildren<SpriteRenderer>()
            : null;
        BuildableObject replacement = factory.Create(grid, lab, center);
        if (replacement != null)
        {
            replacement.SetGrid(grid);
            replacement.Initialization(lab, center);
        }

        bool blockedReplacement = replacement != null
            && !grid.RegisterOccupant(replacement, lab.layer, replacement.buildPoses, false);
        bool originalFootprintUnchanged = building != null
            && lab.GetGridPosList(center)
                .All((pos) => ReferenceEquals(grid.GetGridCell(pos).GetBuilding(), building));

        bool valid = placed
            && building != null
            && renderer == null
            && blockedReplacement
            && originalFootprintUnchanged;

        if (replacement != null)
        {
            Object.DestroyImmediate(replacement.gameObject);
        }

        DestroyBuildables(grid);
        return valid;
    }

    private static bool VerifyDraggedGhostUsesRepeatedSprites()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Images/Placeholders/Facilities/facility_research_lab.png");
        if (sprite == null)
        {
            return false;
        }

        GameObject root = new GameObject("GhostRoot");
        GameObject mainObject = new GameObject("Ghost");
        mainObject.transform.SetParent(root.transform, false);

        SpriteRenderer mainRenderer = mainObject.AddComponent<SpriteRenderer>();
        mainRenderer.drawMode = SpriteDrawMode.Tiled;
        mainRenderer.size = new Vector2(3f, 3f);
        mainRenderer.sortingLayerName = "UI";
        mainRenderer.sortingOrder = 100;

        GridGhostObject ghost = root.AddComponent<GridGhostObject>();
        ghost.Initialize(mainObject);

        List<Vector3> positions = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(2f, 0f, 0f)
        };
        List<bool> buildableStates = new List<bool> { true, false, true };

        ghost.ShowRepeated(sprite, positions, new Vector2(1f, 3f), buildableStates);
        const float expectedPreviewHeight = 3f;

        List<SpriteRenderer> activeRenderers = root
            .GetComponentsInChildren<SpriteRenderer>(false)
            .OrderBy((renderer) => renderer.bounds.center.x)
            .ToList();

        bool repeatedValid = activeRenderers.Count == 3
            && activeRenderers.All((renderer) => renderer.sprite == sprite)
            && activeRenderers.All((renderer) => Mathf.Abs(renderer.bounds.size.x - 1f) <= 0.05f)
            && activeRenderers.All((renderer) => Mathf.Abs(renderer.bounds.size.y - expectedPreviewHeight) <= 0.05f)
            && activeRenderers.All((renderer) => Mathf.Abs(renderer.bounds.min.y) <= 0.05f)
            && activeRenderers.All((renderer) => Mathf.Abs(renderer.bounds.max.y - GridBuildingTileTransformCalculator.DefaultCellTileHeight) <= 0.05f)
            && activeRenderers.All((renderer) => renderer.sortingLayerName == "Default")
            && activeRenderers.All((renderer) => renderer.sortingOrder == 200)
            && activeRenderers.All((renderer) => renderer.drawMode == SpriteDrawMode.Simple)
            && Mathf.Abs(activeRenderers[0].bounds.center.x - 0f) <= 0.05f
            && Mathf.Abs(activeRenderers[1].bounds.center.x - 1f) <= 0.05f
            && Mathf.Abs(activeRenderers[2].bounds.center.x - 2f) <= 0.05f
            && activeRenderers.All((renderer) => Mathf.Abs(renderer.bounds.center.y - 1.5f) <= 0.05f)
            && IsPreviewColor(activeRenderers[0].color, true)
            && IsPreviewColor(activeRenderers[1].color, false)
            && IsPreviewColor(activeRenderers[2].color, true);

        ghost.SetSize(new Vector2(4f, 2f));
        ghost.Show(sprite);
        ghost.SetWorldPosition(Vector3.zero);
        SpriteRenderer[] singleRenderers = root.GetComponentsInChildren<SpriteRenderer>(false);
        bool singleValid = singleRenderers.Length == 1
            && singleRenderers[0].drawMode == SpriteDrawMode.Simple
            && Mathf.Abs(singleRenderers[0].bounds.size.x - 4f) <= 0.05f
            && Mathf.Abs(singleRenderers[0].bounds.size.y - 2f) <= 0.05f
            && Mathf.Abs(singleRenderers[0].bounds.min.y) <= 0.05f
            && IsPreviewColor(singleRenderers[0].color, true);

        Object.DestroyImmediate(root);
        return repeatedValid && singleValid;
    }

    private static bool VerifyInitialRoomFootprintKeepsHallwayLayer()
    {
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO restRoom = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_RestRoom.asset");
        if (hallway == null || restRoom == null)
        {
            return false;
        }

        Grid grid = new Grid(8, 1);
        GridBuildingPlacementService service = new GridBuildingPlacementService(
            grid,
            hallway,
            null,
            new GridBuildingFactory(),
            new BuildingPlacementValidator());

        List<InitialBuildInfo> placements = new List<InitialBuildInfo>
        {
            new InitialBuildInfo { Position = new Vector2Int(3, 0), Building = restRoom },
            new InitialBuildInfo { Position = new Vector2Int(2, 0), Building = hallway },
            new InitialBuildInfo { Position = new Vector2Int(3, 0), Building = hallway },
            new InitialBuildInfo { Position = new Vector2Int(4, 0), Building = hallway }
        };

        service.PlaceInitialBuildings(placements);

        List<Vector2Int> footprint = restRoom.GetGridPosList(new Vector2Int(3, 0));
        bool valid = footprint.All((position) =>
        {
            GridCell cell = grid.GetGridCell(position);
            return cell != null
                && cell.HasOccupantInLayer(GridLayer.Hallway)
                && cell.GetOccupant(GridLayer.Building)?.GridId == restRoom.id;
        });

        DestroyBuildables(grid);
        return valid;
    }

    private static bool VerifyPlacementOverlayCellAvailability()
    {
        Grid grid = new Grid(6, 3);
        AddHallway(grid, new Vector2Int(2, 0));
        TestOccupant occupied = new TestOccupant(99, false, GridMoveType.Walk);
        grid.RegisterOccupant(
            occupied,
            GridLayer.Building,
            new List<Vector2Int> { new Vector2Int(3, 0) },
            false);

        HashSet<Vector2Int> actual = GridPlacementCellAvailability.CollectInstallableCells(
            grid,
            GridLayer.Building,
            sidePadding: 1);
        HashSet<Vector2Int> expected = new HashSet<Vector2Int>
        {
            new Vector2Int(1, 0),
            new Vector2Int(2, 0),
            new Vector2Int(4, 0),
            new Vector2Int(2, 1),
            new Vector2Int(3, 1)
        };

        return actual.SetEquals(expected)
            && actual.Contains(new Vector2Int(2, 0))
            && !actual.Contains(new Vector2Int(3, 0))
            && actual.Contains(new Vector2Int(2, 1))
            && !actual.Contains(new Vector2Int(1, 1))
            && !actual.Any((position) => position.x == 0 || position.x == grid.width - 1)
            && !actual.Any((position) => position.y == 2);
    }

    private static bool VerifyDoorReplacesOneStructuralWallCell()
    {
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        if (wall == null
            || door == null
            || !wall.IsStructuralWall
            || !door.IsDoor
            || !door.IsInteriorDoor
            || door.width != 1
            || door.height != 1)
        {
            return false;
        }

        Grid grid = new Grid(7, 1);
        for (int x = 0; x < grid.width; x++)
        {
            AddHallway(grid, new Vector2Int(x, 0));
        }

        GridBuildingPlacementService service = new GridBuildingPlacementService(
            grid,
            null,
            null,
            new GridBuildingFactory(InjectBuildingDependencies),
            new BuildingPlacementValidator());
        Vector2Int target = new Vector2Int(3, 0);
        bool emptyCellRejected = !service.CanPlaceBuilding(door, new Vector2Int(2, 0));

        TestOccupant furniture = new TestOccupant(99, false, GridMoveType.Walk);
        grid.RegisterOccupant(
            furniture,
            GridLayer.Building,
            new List<Vector2Int> { new Vector2Int(4, 0) },
            false);
        bool nonWallRejected = !service.CanPlaceBuilding(door, new Vector2Int(4, 0));
        grid.RemoveOccupant(
            GridLayer.Building,
            new List<Vector2Int> { new Vector2Int(4, 0) },
            false);

        bool wallPlaced = service.TryPlaceBuilding(wall, target, out _);
        BuildableObject replacedWall = grid.GetGridCell(target)?.GetBuildingInlayer(GridLayer.Building);
        bool wallFoundBeforeReplacement = replacedWall != null;
        HashSet<Vector2Int> doorTargets = GridPlacementCellAvailability.CollectDoorInstallableCells(
            grid,
            sidePadding: 1);
        bool doorValidOnWall = service.CanPlaceBuilding(door, target);
        bool doorPlaced = service.TryPlaceBuilding(door, target, out _);
        InteriorDoor placedDoor = grid.GetGridCell(target)?.GetBuildingInlayer(GridLayer.Building) as InteriorDoor;
        BoxCollider2D doorCollider = placedDoor != null ? placedDoor.GetComponent<BoxCollider2D>() : null;

        bool valid = emptyCellRejected
            && nonWallRejected
            && wallPlaced
            && wallFoundBeforeReplacement
            && doorTargets.SetEquals(new[] { target })
            && doorValidOnWall
            && doorPlaced
            && replacedWall == null
            && placedDoor != null
            && placedDoor.buildPoses.Count == 1
            && placedDoor.buildPoses[0] == target
            && doorCollider != null
            && doorCollider.isTrigger
            && Mathf.Abs(doorCollider.size.x - 1f) <= 0.01f
            && grid.GetGridCell(target).HasOccupantInLayer(GridLayer.Hallway)
            && grid.IsWalkable(target)
            && !grid.IsMovementBlockedByWall(target)
            && !service.CanPlaceBuilding(door, target);

        DestroyBuildables(grid);
        return valid;
    }

    private static bool VerifySpawnerEntryCrossesDungeonDoorCenter()
    {
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Door.asset");
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        if (door == null || hallway == null || door.type != typeof(Door) || door.width != 3)
        {
            return false;
        }

        Grid grid = new Grid(7, 1);
        GridBuildingPlacementService service = new GridBuildingPlacementService(
            grid,
            hallway,
            null,
            new GridBuildingFactory(InjectBuildingDependencies),
            new BuildingPlacementValidator());
        Vector2Int doorCenter = new Vector2Int(2, 0);
        Vector2Int configuredInsidePosition = new Vector2Int(4, 0);
        service.PlaceInitialBuildings(new[]
        {
            new InitialBuildInfo
            {
                Position = doorCenter,
                Building = door
            }
        });
        bool placed = grid.GetGridCell(doorCenter)?.GetBuildingInlayer(GridLayer.Building) is Door;
        bool resolved = DungeonEntranceGridResolver.TryResolve(
            grid,
            configuredInsidePosition,
            out Door entrance);
        Vector3 doorWorldPosition = resolved
            ? grid.GetWorldPos(entrance.centerPos)
            : default;
        Vector3 insideWorldPosition = grid.GetWorldPos(configuredInsidePosition);
        bool valid = placed
            && resolved
            && entrance != null
            && entrance.centerPos == doorCenter
            && Mathf.Abs(doorWorldPosition.y - insideWorldPosition.y) <= 0.01f
            && Mathf.Abs(doorWorldPosition.x - insideWorldPosition.x) >= 1.99f;

        DestroyBuildables(grid);
        return valid;
    }

    private static bool VerifyWorldClickGridFallbackOnlyAcceptsExactWallStructures()
    {
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO interiorDoor = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        BuildingSO dungeonDoor = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Door.asset");
        BuildingSO facility = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_ResearchLab.asset");

        return hallway != null
            && wall != null
            && interiorDoor != null
            && dungeonDoor != null
            && facility != null
            && !WorldInfoClickSelectionService.IsExactGridStructureDefinition(hallway)
            && WorldInfoClickSelectionService.IsExactGridStructureDefinition(wall)
            && WorldInfoClickSelectionService.IsExactGridStructureDefinition(interiorDoor)
            && !WorldInfoClickSelectionService.IsExactGridStructureDefinition(dungeonDoor)
            && !WorldInfoClickSelectionService.IsExactGridStructureDefinition(facility);
    }

    private static void InjectBuildingDependencies(BuildableObject building)
    {
        building.ConstructBuildableObject(
            BlueprintResearchWorkService,
            WorldInfoClickSelector,
            FacilityCandidateCacheService,
            RoomFacilityPolicyService);
    }

    private static TestOccupant AddHallway(Grid grid, Vector2Int position)
    {
        TestOccupant occupant = new TestOccupant(1, true, GridMoveType.Instant);
        grid.RegisterOccupant(occupant, GridLayer.Hallway, new List<Vector2Int> { position }, false);
        return occupant;
    }

    private static void AddMovement(Grid grid, Vector2Int from, Vector2Int to, GridMoveType moveType)
    {
        TestOccupant occupant = new TestOccupant(2, true, moveType);
        grid.RegisterOccupant(occupant, GridLayer.Building, new List<Vector2Int> { from, to }, true);
    }

    private static bool IsPreviewColor(Color actual, bool buildable)
    {
        Color expectedTint = buildable
            ? Color.white
            : Color.Lerp(Color.white, Color.red, 0.45f);
        return Mathf.Abs(actual.r - expectedTint.r) <= 0.01f
            && Mathf.Abs(actual.g - expectedTint.g) <= 0.01f
            && Mathf.Abs(actual.b - expectedTint.b) <= 0.01f
            && Mathf.Abs(actual.a - 1f) <= 0.01f;
    }

    private static void DestroyBuildables(Grid grid)
    {
        foreach (IGridOccupant occupant in grid.FindAllOccupants(null))
        {
            if (occupant is BuildableObject buildableObject)
            {
                Object.DestroyImmediate(buildableObject.gameObject);
            }
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

    private sealed class NoopBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility)
        {
            return false;
        }

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds)
        {
            return new BlueprintResearchWorkResult(
                false,
                null,
                0f,
                0f,
                1f,
                false,
                "Grid foundation fixture has no blueprint research runtime.");
        }
    }

    private sealed class NoopWorldInfoClickSelector : IWorldInfoClickSelector
    {
        public bool TryHandleWorldInfoClick()
        {
            return false;
        }

        public bool TryTriggerCharacterUnderPointer()
        {
            return false;
        }

        public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacterAtScreenPosition(
            Vector3 screenPosition,
            Camera camera,
            out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor)
        {
            actor = null;
            return false;
        }
    }
}
