using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RoomEnvironmentDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Run Room Environment Scenarios")]
    public static void RunAllFromMenu()
    {
        RunAll(true);
    }

    public static bool RunAll(bool log = false)
    {
        List<string> errors = new List<string>();
        Run("Luxury, hygiene, damage and clean records affect scores", VerifyEnvironmentInputs, errors);
        Run("Occupied cells reduce spaciousness and cleanliness", VerifyCrowding, errors);
        Run("Mixed room roles keep all contributors", VerifyMixedRoles, errors);
        Run("Incomplete and self-contained rooms disable effects", VerifyInvalidRooms, errors);
        Run("Room cache resolves a room by grid cell", VerifyCellLookup, errors);
        Run("Room environment query drives work multipliers", VerifyEnvironmentQueryWorkMultiplier, errors);
        Run("Mood thresholds match the room grade bands", VerifyMoodThresholds, errors);

        if (errors.Count > 0)
        {
            Debug.LogError($"RoomEnvironmentDebugScenarios failed:\n{string.Join("\n", errors)}");
            return false;
        }

        if (log)
        {
            Debug.Log("RoomEnvironmentDebugScenarios passed.");
        }

        return true;
    }

    private static void Run(string name, Func<bool> scenario, ICollection<string> errors)
    {
        try
        {
            if (!scenario())
            {
                errors.Add("- " + name);
            }
        }
        catch (Exception exception)
        {
            errors.Add($"- {name}: {exception.GetType().Name} {exception.Message}");
        }
    }

    private static bool VerifyEnvironmentInputs()
    {
        using ScenarioWorld world = ScenarioWorld.Create();
        RoomInstance room = world.GetMainRoom();
        RoomEnvironmentEvaluator evaluator = world.CreateEvaluator();

        RoomEnvironmentSnapshot baseline = evaluator.Evaluate(world.Grid, room);
        world.MainFixture.BuildingData.Evolution.scores = new[]
        {
            new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, 20f)
        };
        RoomEnvironmentSnapshot luxurious = evaluator.Evaluate(world.Grid, room);

        world.MainFixture.BuildingData.Facility.roles |= FacilityRole.Hygiene;
        world.Records.GetRecord(world.MainFixture).AddToken(FacilityEvolutionTerms.CleanServiceStreak, 3);
        RoomEnvironmentSnapshot hygienic = evaluator.Evaluate(world.Grid, room);

        SetDamagedWithoutRuntimeServices(world.MainFixture, true);
        RoomEnvironmentSnapshot damaged = evaluator.Evaluate(world.Grid, room);

        return luxurious.Beauty > baseline.Beauty
            && hygienic.Cleanliness > luxurious.Cleanliness
            && damaged.Beauty < hygienic.Beauty
            && damaged.Cleanliness < hygienic.Cleanliness;
    }

    private static bool VerifyCrowding()
    {
        using ScenarioWorld world = ScenarioWorld.Create();
        RoomInstance sparseRoom = world.GetMainRoom();
        RoomEnvironmentEvaluator evaluator = world.CreateEvaluator();
        RoomEnvironmentSnapshot sparse = evaluator.Evaluate(world.Grid, sparseRoom);

        world.PlaceFixture("Crate A", new Vector2Int(2, 0), FacilityRole.None);
        world.PlaceFixture("Crate B", new Vector2Int(4, 0), FacilityRole.None);
        world.PlaceFixture("Crate C", new Vector2Int(5, 0), FacilityRole.None);
        RoomInstance crowdedRoom = world.GetMainRoom();
        RoomEnvironmentSnapshot crowded = evaluator.Evaluate(world.Grid, crowdedRoom);

        return crowded.OccupiedCells > sparse.OccupiedCells
            && crowded.FreeCells < sparse.FreeCells
            && crowded.Spaciousness < sparse.Spaciousness
            && crowded.Cleanliness < sparse.Cleanliness;
    }

    private static bool VerifyMixedRoles()
    {
        using ScenarioWorld world = ScenarioWorld.Create();
        BuildableObject mana = world.PlaceFixture("Mana Focus", new Vector2Int(4, 0), FacilityRole.Mana);
        RoomInstance room = world.GetMainRoom();
        RoomEnvironmentSnapshot snapshot = world.CreateEvaluator().Evaluate(world.Grid, room);

        return mana != null
            && (snapshot.Roles & FacilityRole.Research) != 0
            && (snapshot.Roles & FacilityRole.Mana) != 0
            && snapshot.RoleContributions.Count == 2
            && snapshot.RoleContributions.All((entry) => entry.Count == 1)
            && snapshot.PrimaryRole == FacilityRole.None
            && snapshot.UsesMixedColor;
    }

    private static bool VerifyInvalidRooms()
    {
        using ScenarioWorld world = ScenarioWorld.Create();
        RoomEnvironmentEvaluator evaluator = world.CreateEvaluator();
        RoomInstance missingDoor = new RoomInstance(
            100,
            new[] { new Vector2Int(3, 0) },
            new[] { world.MainFixture },
            Array.Empty<BuildableObject>(),
            new[] { world.RightWall },
            2,
            0);
        RoomInstance openBoundary = new RoomInstance(
            101,
            new[] { new Vector2Int(3, 0) },
            new[] { world.MainFixture },
            Array.Empty<BuildableObject>(),
            Array.Empty<BuildableObject>(),
            1,
            1);
        RoomInstance selfContained = new RoomInstance(
            102,
            new[] { new Vector2Int(3, 0) },
            new[] { world.MainFixture },
            Array.Empty<BuildableObject>(),
            Array.Empty<BuildableObject>(),
            2,
            0,
            selfContained: true);

        RoomEnvironmentSnapshot missing = evaluator.Evaluate(world.Grid, missingDoor);
        RoomEnvironmentSnapshot open = evaluator.Evaluate(world.Grid, openBoundary);
        RoomEnvironmentSnapshot self = evaluator.Evaluate(world.Grid, selfContained);
        return missing.Status == RoomEnvironmentStatus.MissingDoor
            && open.Status == RoomEnvironmentStatus.OpenBoundary
            && self.Status == RoomEnvironmentStatus.SelfContained
            && !missing.IsEnvironmentActive
            && !open.IsEnvironmentActive
            && !self.IsEnvironmentActive;
    }

    private static bool VerifyCellLookup()
    {
        using ScenarioWorld world = ScenarioWorld.Create();
        RoomLayoutCache cache = new RoomLayoutCache();
        return cache.TryGetRoom(world.Grid, new Vector2Int(3, 0), out RoomInstance room)
            && room != null
            && room.ContainsPart(world.MainFixture)
            && room.IsUsable;
    }

    private static bool VerifyEnvironmentQueryWorkMultiplier()
    {
        using ScenarioWorld world = ScenarioWorld.Create();
        FacilityCandidateCacheStore facilityCache = new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry);
        RoomEnvironmentQuery query = new RoomEnvironmentQuery(
            new RoomLayoutCache(),
            world.CreateEvaluator(),
            facilityCache);

        Require(query.TryGetSnapshot(world.MainFixture, out RoomEnvironmentSnapshot baseline),
            "Room environment query did not resolve the main fixture room.");
        float score = Mathf.Clamp(baseline.Impressiveness * 0.6f + baseline.Cleanliness * 0.4f, 0f, 100f);
        float expectedDuration = 1f / Mathf.Clamp(0.85f + score * 0.003f, 0.85f, 1.15f);
        Require(Mathf.Abs(query.GetWorkDurationMultiplier(world.MainFixture, BuiltInWorkTypeIds.Research) - expectedDuration) < 0.001f,
            "Research duration multiplier did not use the shared room environment score.");
        Require(Mathf.Abs(query.GetWorkDurationMultiplier(world.MainFixture, BuiltInWorkTypeIds.Craft) - expectedDuration) < 0.001f,
            "Craft duration multiplier did not use the shared room environment score.");
        Require(Mathf.Approximately(query.GetWorkDurationMultiplier(world.MainFixture, BuiltInWorkTypeIds.Clean), 1f)
                && Mathf.Approximately(query.GetWorkDurationMultiplier(world.MainFixture, BuiltInWorkTypeIds.Repair), 1f)
                && Mathf.Approximately(query.GetWorkDurationMultiplier(world.MainFixture, BuiltInWorkTypeIds.Rest), 1f),
            "Recovery or repair work incorrectly used the room environment multiplier.");

        world.MainFixture.FacilityState.cleanliness = 0f;
        facilityCache.MarkDynamicStateDirty();
        Require(query.TryGetSnapshot(world.MainFixture, out RoomEnvironmentSnapshot dirty),
            "Room environment query failed after dynamic state invalidation.");
        Require(dirty.Cleanliness < baseline.Cleanliness,
            "Room environment query did not refresh after facility state changed.");
        return true;
    }

    private static bool VerifyMoodThresholds()
    {
        RoomEnvironmentSettingsSO settings = ScriptableObject.CreateInstance<RoomEnvironmentSettingsSO>();
        try
        {
            return settings.GetImpressivenessMood(19f) == -6f
                && settings.GetImpressivenessMood(39f) == -3f
                && settings.GetImpressivenessMood(59f) == 0f
                && settings.GetImpressivenessMood(79f) == 3f
                && settings.GetImpressivenessMood(80f) == 6f
                && settings.GetCleanlinessMood(19f) == -4f
                && settings.GetCleanlinessMood(39f) == -2f
                && settings.GetCleanlinessMood(79f) == 0f
                && settings.GetCleanlinessMood(80f) == 2f;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static void SetDamagedWithoutRuntimeServices(BuildableObject fixture, bool value)
    {
        FieldInfo field = typeof(BuildableObject).GetField(
            "isDamaged",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(fixture, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ScenarioWorld : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private readonly RoomEnvironmentSettingsSO settings;

        private ScenarioWorld()
        {
            Grid = new Grid(8, 1);
            Records = new TestRecordProvider();
            settings = ScriptableObject.CreateInstance<RoomEnvironmentSettingsSO>();
            cleanup.Add(settings);
        }

        public Grid Grid { get; }
        public TestRecordProvider Records { get; }
        public BuildableObject MainFixture { get; private set; }
        public BuildableObject RightWall { get; private set; }

        public static ScenarioWorld Create()
        {
            ScenarioWorld world = new ScenarioWorld();
            for (int x = 0; x < world.Grid.width; x++)
            {
                world.Grid.RegisterOccupant(
                    new HallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            world.PlaceBoundary("Door", new Vector2Int(1, 0), BuildingCategory.Movement);
            world.RightWall = world.PlaceBoundary("Wall", new Vector2Int(6, 0), BuildingCategory.Wall);
            world.MainFixture = world.PlaceFixture("Research Desk", new Vector2Int(3, 0), FacilityRole.Research);
            return world;
        }

        public RoomEnvironmentEvaluator CreateEvaluator()
        {
            return new RoomEnvironmentEvaluator(new TestSettingsProvider(settings), Records);
        }

        public RoomInstance GetMainRoom()
        {
            return RoomDetector.Build(Grid).Rooms.First((room) =>
                room != null && room.ContainsPart(MainFixture));
        }

        public BuildableObject PlaceFixture(string name, Vector2Int position, FacilityRole roles)
        {
            return Place(name, position, BuildingCategory.Special, roles);
        }

        public void Dispose()
        {
            foreach (UnityEngine.Object item in cleanup.Where((item) => item != null))
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private BuildableObject PlaceBoundary(string name, Vector2Int position, BuildingCategory category)
        {
            return Place(name, position, category, FacilityRole.None);
        }

        private BuildableObject Place(
            string name,
            Vector2Int position,
            BuildingCategory category,
            FacilityRole roles)
        {
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            cleanup.Add(data);
            data.id = cleanup.Count + 1000;
            data.objectName = name;
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = category;
            data.type = category == BuildingCategory.Movement
                ? typeof(Door)
                : typeof(BuildableObject);
            data.Facility = new FacilityData
            {
                roles = roles,
                capacity = roles == FacilityRole.None ? 0 : 1,
                useDuration = 1f
            };
            data.Evolution = new FacilityEvolutionContributionData();

            GameObject obj = new GameObject(name);
            cleanup.Add(obj);
            BuildableObject fixture = obj.AddComponent<BuildableObject>();
            fixture.SetGrid(Grid);
            fixture.Initialization(data, position);
            bool registered = Grid.RegisterOccupant(
                fixture,
                GridLayer.Building,
                new List<Vector2Int> { position },
                category == BuildingCategory.Movement);
            if (!registered)
            {
                throw new InvalidOperationException($"Could not place test fixture {name} at {position}.");
            }

            return fixture;
        }
    }

    private sealed class TestSettingsProvider : IRoomEnvironmentSettingsProvider
    {
        public TestSettingsProvider(RoomEnvironmentSettingsSO settings)
        {
            Settings = settings;
        }

        public RoomEnvironmentSettingsSO Settings { get; }
    }

    public sealed class TestRecordProvider : IFacilityEvolutionRecordProvider
    {
        private readonly Dictionary<BuildableObject, FacilityEvolutionRecord> records =
            new Dictionary<BuildableObject, FacilityEvolutionRecord>();

        public FacilityEvolutionRecord GetRecord(BuildableObject facility)
        {
            if (facility == null)
            {
                return new FacilityEvolutionRecord();
            }

            if (!records.TryGetValue(facility, out FacilityEvolutionRecord record))
            {
                record = new FacilityEvolutionRecord();
                records[facility] = record;
            }

            return record;
        }
    }

    private sealed class HallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }
}
