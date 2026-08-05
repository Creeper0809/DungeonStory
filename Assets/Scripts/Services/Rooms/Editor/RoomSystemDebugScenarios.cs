using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RoomSystemDebugScenarios
{
    private static readonly IBlueprintResearchWorkService BlueprintResearchWorkService =
        new NoopBlueprintResearchWorkService();
    private static readonly IWorldInfoClickSelector WorldInfoClickSelector =
        new NoopWorldInfoClickSelector();
    private static readonly IFacilityCandidateCache FacilityCandidateCacheService =
        new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null);
    private static readonly IRoomFacilityPolicy RoomFacilityPolicyService =
        new RoomFacilityPolicyService(RoomRegistry.EditorCache);

    [MenuItem("DungeonStory/Debug/Run Room System Scenarios")]
    public static void RunAllFromMenu()
    {
        RunAll(true);
    }

    public static bool RunAll(bool log = false)
    {
        List<string> errors = new List<string>();
        RunScenario("Closed room gets role from furniture", VerifyClosedRoomGetsRoleFromFurniture, errors);
        RunScenario("Formal room supersedes self-contained fallback", VerifyFormalRoomSupersedesSelfContainedFallback, errors);
        RunScenario("Open hallway is not a usable room", VerifyOpenHallwayIsNotUsableRoom, errors);
        RunScenario("Room scan does not mutate pathing", VerifyRoomScanDoesNotMutatePathing, errors);
        RunScenario("Room requirement gates facility candidate", VerifyRoomRequirementGatesFacilityCandidate, errors);
        RunScenario("Room requirement gates CanVisit and path visitability", VerifyRoomRequirementGatesCanVisitAndPathVisitability, errors);
        RunScenario("Formal wall blocks movement even over hallway", VerifyFormalWallBlocksMovement, errors);
        RunScenario("Room boundary build assets are player-facing", VerifyRoomBoundaryBuildAssets, errors);
        RunScenario("Buildable hallway remains room interior", VerifyBuildableHallwayRemainsInterior, errors);
        RunScenario("Wall-installed door belongs to both rooms", VerifyWallInstalledDoorBelongsToBothRooms, errors);

        if (errors.Count > 0)
        {
            Debug.LogError($"RoomSystemDebugScenarios failed:\n{string.Join("\n", errors)}");
            return false;
        }

        if (log)
        {
            Debug.Log("RoomSystemDebugScenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (!scenario())
            {
                errors.Add($"- {name}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"- {name}: {ex.GetType().Name} {ex.Message}");
        }
    }

    private static bool VerifyClosedRoomGetsRoleFromFurniture()
    {
        using RoomScenarioWorld world = RoomScenarioWorld.CreateClosedToiletRoom();

        RoomLayout layout = RoomDetector.Build(world.Grid);
        RoomInstance room = layout.Rooms.FirstOrDefault((candidate) =>
            candidate.SupportsFacilityRole(FacilityRole.Toilet));

        return room != null
            && room.IsUsable
            && room.HasDoor
            && room.ContainsPart(world.Toilet)
            && (room.Roles & FacilityRole.Toilet) != 0;
    }

    private static bool VerifyOpenHallwayIsNotUsableRoom()
    {
        using RoomScenarioWorld world = RoomScenarioWorld.CreateOpenToiletHallway();

        RoomLayout layout = RoomDetector.Build(world.Grid);
        return layout.Rooms.Count > 0
            && layout.Rooms.All((room) => !room.SupportsFacilityRole(FacilityRole.Toilet));
    }

    private static bool VerifyFormalRoomSupersedesSelfContainedFallback()
    {
        using RoomScenarioWorld world = RoomScenarioWorld.CreateClosedToiletRoom();
        world.Toilet.BuildingData.AbilityModules.Add(new BuildingSelfContainedRoomAbility());

        RoomLayout layout = RoomDetector.Build(world.Grid);
        return layout.TryGetRoom(world.Toilet, out RoomInstance room)
            && room != null
            && !room.IsSelfContained
            && room.IsUsable
            && room.Doors.Count > 0
            && room.Walls.Count > 0;
    }

    private static bool VerifyRoomScanDoesNotMutatePathing()
    {
        using RoomScenarioWorld world = RoomScenarioWorld.CreateClosedToiletRoom();

        GridPathSearchResult before = world.Grid.SearchPath(Vector2Int.zero);
        int beforeVersion = world.Grid.version;
        int beforeReachable = before.GetReachablePositions().Count;
        int beforeVisitable = before.GetAllVisitableOccupants().Count;

        RoomLayout layout = RoomDetector.Build(world.Grid);

        GridPathSearchResult after = world.Grid.SearchPath(Vector2Int.zero);
        return layout.Rooms.Count > 0
            && beforeVersion == world.Grid.version
            && beforeReachable == after.GetReachablePositions().Count
            && beforeVisitable == after.GetAllVisitableOccupants().Count
            && after.ContainsVisitableOccupant(world.Toilet);
    }

    private static bool VerifyRoomRequirementGatesFacilityCandidate()
    {
        using RoomScenarioWorld openWorld = RoomScenarioWorld.CreateOpenToiletHallway();
        using RoomScenarioWorld closedWorld = RoomScenarioWorld.CreateClosedToiletRoom();

        bool openAllowed = RoomFacilityPolicy.IsFacilityRoleAvailable(
            openWorld.Toilet,
            FacilityRole.Toilet,
            out _);
        bool closedAllowed = RoomFacilityPolicy.IsFacilityRoleAvailable(
            closedWorld.Toilet,
            FacilityRole.Toilet,
            out _);

        List<BuildableObject> openCandidates = openWorld.Grid
            .SearchPath(Vector2Int.zero)
            .GetAllVisitableBuilding()
            .Where((building) => building.SupportsFacilityRole(FacilityRole.Toilet))
            .ToList();
        List<BuildableObject> closedCandidates = closedWorld.Grid
            .SearchPath(Vector2Int.zero)
            .GetAllVisitableBuilding()
            .Where((building) => building.SupportsFacilityRole(FacilityRole.Toilet))
            .ToList();

        return !openAllowed
            && closedAllowed
            && openCandidates.Count == 0
            && closedCandidates.Count == 1
            && closedCandidates[0] == closedWorld.Toilet;
    }

    private static bool VerifyRoomRequirementGatesCanVisitAndPathVisitability()
    {
        using RoomScenarioWorld openWorld = RoomScenarioWorld.CreateOpenToiletHallway();
        using RoomScenarioWorld closedWorld = RoomScenarioWorld.CreateClosedToiletRoom();

        bool openVisitRejected = !openWorld.Toilet.CanVisit((CharacterActor)null, out string openReason)
            && !string.IsNullOrWhiteSpace(openReason);
        bool closedVisitAllowed = closedWorld.Toilet.CanVisit((CharacterActor)null, out _);
        bool openPathRejected = !openWorld.Grid
            .SearchPath(Vector2Int.zero)
            .GetAllVisitableBuilding()
            .Contains(openWorld.Toilet);
        bool closedPathAllowed = closedWorld.Grid
            .SearchPath(Vector2Int.zero)
            .GetAllVisitableBuilding()
            .Contains(closedWorld.Toilet);

        return openVisitRejected
            && closedVisitAllowed
            && openPathRejected
            && closedPathAllowed;
    }

    private static bool VerifyFormalWallBlocksMovement()
    {
        using RoomScenarioWorld world = RoomScenarioWorld.CreateWallBlockedHallway();

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        return !search.GetReachablePositions().Contains(new Vector2Int(3, 0))
            && !world.Grid.IsWalkable(new Vector2Int(2, 0));
    }

    private static bool VerifyRoomBoundaryBuildAssets()
    {
        const string WallPath = "Assets/Resources/SO/Building/Wall.asset";
        const string DungeonDoorPath = "Assets/Resources/SO/Building/Door.asset";
        const string InteriorDoorPath = "Assets/Resources/SO/Building/InteriorDoor.asset";
        AssetDatabase.ImportAsset(WallPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(DungeonDoorPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(InteriorDoorPath, ImportAssetOptions.ForceUpdate);

        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            WallPath);
        BuildingSO dungeonDoor = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            DungeonDoorPath);
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            InteriorDoorPath);
        if (dungeonDoor != null && dungeonDoor.unlocked)
        {
            dungeonDoor.unlocked = false;
            EditorUtility.SetDirty(dungeonDoor);
            AssetDatabase.SaveAssetIfDirty(dungeonDoor);
            AssetDatabase.ImportAsset(DungeonDoorPath, ImportAssetOptions.ForceUpdate);
            dungeonDoor = AssetDatabase.LoadAssetAtPath<BuildingSO>(DungeonDoorPath);
        }

        bool valid = wall != null
            && wall.objectName == "내벽"
            && wall.sprite != null
            && wall.icon != null
            && wall.category == BuildingCategory.Wall
            && wall.horizontalDraggable
            && wall.verticalDraggable
            && wall.unlocked
            && dungeonDoor != null
            && dungeonDoor.runtimeArchetype == BuildingRuntimeArchetypeKind.Door
            && dungeonDoor.width == 3
            && dungeonDoor.height == 1
            && !dungeonDoor.unlocked
            && !dungeonDoor.IsInteriorDoor
            && door != null
            && door.runtimeArchetype == BuildingRuntimeArchetypeKind.InteriorDoor
            && door.IsInteriorDoor
            && door.width == 1
            && door.height == 1
            && door.unlocked;
        if (!valid)
        {
            Debug.Log(
                "Room boundary asset diagnostics: "
                + $"wall={wall != null},name={wall?.objectName},sprite={wall?.sprite != null},icon={wall?.icon != null},"
                + $"category={wall?.category},hDrag={wall?.horizontalDraggable},vDrag={wall?.verticalDraggable},unlocked={wall?.unlocked}; "
                + $"dungeonDoor={dungeonDoor != null},archetype={dungeonDoor?.runtimeArchetype},size={dungeonDoor?.width}x{dungeonDoor?.height},"
                + $"unlocked={dungeonDoor?.unlocked},interior={dungeonDoor?.IsInteriorDoor}; "
                + $"interiorDoor={door != null},archetype={door?.runtimeArchetype},size={door?.width}x{door?.height},"
                + $"unlocked={door?.unlocked},interior={door?.IsInteriorDoor}");
        }

        return valid;
    }

    private static bool VerifyWallInstalledDoorBelongsToBothRooms()
    {
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        if (wall == null || door == null)
        {
            return false;
        }

        Grid grid = new Grid(7, 1);
        for (int x = 0; x < grid.width; x++)
        {
            grid.RegisterOccupant(
                new TestHallwayOccupant(),
                GridLayer.Hallway,
                new List<Vector2Int> { new Vector2Int(x, 0) },
                false);
        }

        int nextPlacementBuildingId = 1;
        GridBuildingPlacementService placement = new GridBuildingPlacementService(
            grid,
            null,
            null,
            new GridBuildingFactory((building) =>
            {
                building.RestorePersistentIdentity(
                    new BuildingInstanceId($"building:room-system-placement-{nextPlacementBuildingId++}"));
                building.ConstructBuildableObject(
                    new BuildingResearchWorkPortAdapter(BlueprintResearchWorkService),
                    FacilityCandidateCacheService,
                    RoomFacilityPolicyService, combatEquipmentRuntime: null, worldRegistry: null, worldItemStackRuntime: null, abilityRuntimeDispatcher: null, gameClock: null, paidFacilityContracts: null, evolutionState: new FacilityEvolutionStateComponentFactory());
            }),
            new BuildingPlacementValidator(), workOrderRuntime: null);
        Vector2Int target = new Vector2Int(3, 0);
        bool wallPlaced = placement.TryPlaceBuilding(wall, target, out _);
        bool doorPlaced = placement.TryPlaceBuilding(door, target, out _);
        InteriorDoor placedDoor = grid.GetGridCell(target)?.GetBuildingInlayer(GridLayer.Building) as InteriorDoor;
        RoomLayout layout = RoomDetector.Build(grid);
        List<RoomInstance> adjoiningRooms = layout.Rooms
            .Where((room) => placedDoor != null && room.Doors.Contains(placedDoor))
            .ToList();

        bool valid = wallPlaced
            && doorPlaced
            && placedDoor != null
            && grid.IsWalkable(target)
            && adjoiningRooms.Count == 2
            && adjoiningRooms.All((room) => room.HasDoor)
            && adjoiningRooms.All((room) => !room.Cells.Contains(target));

        foreach (BuildableObject building in grid.FindAllOccupants(null).OfType<BuildableObject>().Distinct())
        {
            if (building != null)
            {
                UnityEngine.Object.DestroyImmediate(building.gameObject);
            }
        }

        return valid;
    }

    private static bool VerifyBuildableHallwayRemainsInterior()
    {
        using RoomScenarioWorld world = RoomScenarioWorld.CreateBuildableHallwayRow();

        RoomLayout layout = RoomDetector.Build(world.Grid);
        return layout.Rooms.Count == 1
            && layout.Rooms[0].Cells.Count == 5
            && layout.Rooms[0].Walls.Count == 0;
    }

    private sealed class RoomScenarioWorld : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private int nextBuildingInstanceId = 1;

        private RoomScenarioWorld(Grid grid)
        {
            Grid = grid;
        }

        public Grid Grid { get; }
        public BuildableObject Toilet { get; private set; }

        public static RoomScenarioWorld CreateClosedToiletRoom()
        {
            RoomScenarioWorld world = new RoomScenarioWorld(new Grid(8, 1));
            for (int x = 0; x <= 6; x++)
            {
                world.AddHallway(new Vector2Int(x, 0));
            }

            world.PlaceDoor(new Vector2Int(1, 0));
            world.Toilet = world.PlaceFacility(
                "Toilet Part",
                new Vector2Int(3, 0),
                FacilityRole.Toilet,
                requiresRoom: true);
            world.PlaceWall(new Vector2Int(6, 0));
            return world;
        }

        public static RoomScenarioWorld CreateOpenToiletHallway()
        {
            RoomScenarioWorld world = new RoomScenarioWorld(new Grid(8, 1));
            for (int x = 0; x <= 6; x++)
            {
                world.AddHallway(new Vector2Int(x, 0));
            }

            world.Toilet = world.PlaceFacility(
                "Open Toilet Part",
                new Vector2Int(3, 0),
                FacilityRole.Toilet,
                requiresRoom: true);
            return world;
        }

        public static RoomScenarioWorld CreateWallBlockedHallway()
        {
            RoomScenarioWorld world = new RoomScenarioWorld(new Grid(5, 1));
            for (int x = 0; x < 5; x++)
            {
                world.AddHallway(new Vector2Int(x, 0));
            }

            world.PlaceWall(new Vector2Int(2, 0));
            return world;
        }

        public static RoomScenarioWorld CreateBuildableHallwayRow()
        {
            RoomScenarioWorld world = new RoomScenarioWorld(new Grid(5, 1));
            for (int x = 0; x < 5; x++)
            {
                world.AddBuildableHallway(new Vector2Int(x, 0));
            }

            return world;
        }

        public void Dispose()
        {
            RoomRegistry.Clear();
            FacilityCandidateCache.Clear();
            foreach (UnityEngine.Object obj in cleanup.Where((obj) => obj != null))
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        private void AddHallway(Vector2Int position)
        {
            Grid.RegisterOccupant(
                new TestHallwayOccupant(),
                GridLayer.Hallway,
                new List<Vector2Int> { position },
                false);
        }

        private void AddBuildableHallway(Vector2Int position)
        {
            BuildingSO data = CreateBuildingData(
                "복도",
                900,
                BuildingCategory.Wall,
                FacilityRole.None,
                false);
            data.layer = GridLayer.Hallway;
            Place(data, position);
        }

        private BuildableObject PlaceDoor(Vector2Int position)
        {
            BuildingSO data = CreateBuildingData("문", 901, BuildingCategory.None, FacilityRole.None, false);
            data.runtimeArchetype = BuildingRuntimeArchetypeKind.Door;
            return Place(data, position);
        }

        private BuildableObject PlaceWall(Vector2Int position)
        {
            BuildingSO data = CreateBuildingData("벽", 902, BuildingCategory.Wall, FacilityRole.None, false);
            return Place(data, position);
        }

        private BuildableObject PlaceFacility(
            string name,
            Vector2Int position,
            FacilityRole roles,
            bool requiresRoom)
        {
            BuildingSO data = CreateBuildingData(name, 903, BuildingCategory.Special, roles, requiresRoom);
            data.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
            return Place(data, position);
        }

        private BuildableObject Place(BuildingSO data, Vector2Int position)
        {
            GameObject obj = new GameObject(data.objectName);
            cleanup.Add(obj);
            BuildableObject building = data.runtimeArchetype == BuildingRuntimeArchetypeKind.Facility
                ? obj.AddComponent<Facility>()
                : obj.AddComponent<BuildableObject>();
            building.RestorePersistentIdentity(
                new BuildingInstanceId($"building:room-system-{nextBuildingInstanceId++}"));
            building.ConstructBuildableObject(
                new BuildingResearchWorkPortAdapter(BlueprintResearchWorkService),
                FacilityCandidateCacheService,
                RoomFacilityPolicyService, combatEquipmentRuntime: null, worldRegistry: null, worldItemStackRuntime: null, abilityRuntimeDispatcher: null, gameClock: null, paidFacilityContracts: null, evolutionState: new FacilityEvolutionStateComponentFactory());
            building.SetGrid(Grid);
            building.Initialization(data, position);
            bool registered = Grid.RegisterOccupant(
                building,
                data.layer,
                data.GetGridPosList(position),
                data.Placement.IsMovement);
            if (!registered)
            {
                throw new InvalidOperationException($"{data.objectName} registration failed.");
            }

            return building;
        }

        private BuildingSO CreateBuildingData(
            string name,
            int id,
            BuildingCategory category,
            FacilityRole roles,
            bool requiresRoom)
        {
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            cleanup.Add(data);
            data.id = id;
            data.objectName = name;
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = category;
            data.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
            data.unlocked = true;
            data.Facility = new FacilityData
            {
                roles = roles,
                capacity = roles == FacilityRole.None ? 0 : 1,
                useDuration = roles == FacilityRole.None ? 0f : 1f,
                disabledWhenDamaged = true
            };
            if (requiresRoom)
            {
                data.AbilityModules.Add(new BuildingRoomRequirementAbility());
            }
            return data;
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
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
                "Room scenario fixture has no blueprint research runtime.");
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
