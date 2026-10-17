using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RoomProfileConstructionGuardDebugScenarios
{
    private const string SecureDisplayRecipePath =
        "Assets/Resources/SO/FacilityEvolution/P1/EV_SecureDisplay.asset";
    private const string ShopDisplayPath =
        "Assets/Resources/SO/Building/Modular/S02_잡화진열선반.asset";
    private const string ShopCounterPath =
        "Assets/Resources/SO/Building/Modular/S01_판매카운터.asset";
    private const string CandlePath =
        "Assets/Resources/SO/Building/Modular/E07_촛대.asset";

    public static bool RunAll()
    {
        try
        {
            VerifyRoomlessFallbackAndManualFixtureBoundary();
            VerifyConstructionSiteCannotApproveSecureDisplay();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"{nameof(RoomProfileConstructionGuardDebugScenarios)} failed: {exception}");
            return false;
        }
    }

    private static void VerifyRoomlessFallbackAndManualFixtureBoundary()
    {
        BuildingSO completedData = LoadBuilding(ShopDisplayPath);
        BuildingSO constructionTarget = LoadBuilding(CandlePath);
        BuildingWorkAmountAbility constructionWork =
            constructionTarget.GetAbility<BuildingWorkAmountAbility>();
        Require(constructionTarget.id == 1070
                && constructionWork != null
                && Mathf.Approximately(constructionWork.constructionWorkRequired, 530f),
            "The construction fixture must keep the authored candle target (1070, 530 work).");

        using FixtureWorld world = new();
        BuildableObject completed = world.CreateUnregisteredBuilding(
            completedData,
            new Vector2Int(0, 0));
        ConstructionSite site = world.CreateUnregisteredConstructionSite(
            constructionTarget,
            new Vector2Int(1, 0));
        Require(ReferenceEquals(site.TargetBuilding, constructionTarget),
            "ConstructionSite must retain its target building authority.");

        RoomProfile completedProfile = BuildProfile(completed, NoRoomLayoutCache.Instance);
        RoomProfile constructionProfile = BuildProfile(site, NoRoomLayoutCache.Instance);
        constructionProfile.AddFixture(site);

        Require(completedProfile.Fixtures.Count == 1
                && ReferenceEquals(completedProfile.Fixtures[0], completed)
                && completedProfile.HasTag("Shop")
                && completedProfile.GetScore("Shop") > 0f
                && completedProfile.GetMetric(FacilityEvolutionTerms.FurnitureCount) == 1f,
            "A completed BuildableObject must retain its room-profile contribution in roomless fallback.");
        Require(constructionProfile.Fixtures.Count == 0
                && !constructionProfile.HasTag(FacilityEvolutionTerms.Luxury)
                && Mathf.Approximately(
                    constructionProfile.GetScore(FacilityEvolutionTerms.Luxury), 0f)
                && Mathf.Approximately(
                    constructionProfile.GetMetric(FacilityEvolutionTerms.FurnitureCount), 0f),
            "An unfinished ConstructionSite must not contribute through roomless fallback or AddFixture.");
    }

    private static void VerifyConstructionSiteCannotApproveSecureDisplay()
    {
        FacilityEvolutionRecipeSO recipe = AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>(
            SecureDisplayRecipePath);
        BuildingSO display = LoadBuilding(ShopDisplayPath);
        BuildingSO counter = LoadBuilding(ShopCounterPath);
        BuildingSO candle = LoadBuilding(CandlePath);
        Require(recipe != null && recipe.EffectiveId == "evolve_shop_display_to_secure_display",
            "Secure-display evolution fixture asset is missing or changed.");

        using FixtureWorld unfinishedWorld = new();
        BuildableObject unfinishedDisplay = unfinishedWorld.PlaceCompleted(display, new Vector2Int(1, 0));
        BuildableObject unfinishedCounter = unfinishedWorld.PlaceCompleted(
            counter,
            new Vector2Int(3, 0));
        ConstructionSite unfinishedCandle = unfinishedWorld.PlaceConstructionSite(
            candle,
            new Vector2Int(5, 0));
        Require(ReferenceEquals(unfinishedCandle.TargetBuilding, candle),
            "ConstructionSite must expose the authored candle as its target building.");
        SeedComparableServiceHistory(unfinishedDisplay);
        RoomProfile unfinishedProfile = BuildProfile(
            unfinishedDisplay,
            unfinishedWorld.CreateUsableRoomCache(
                unfinishedDisplay,
                unfinishedCounter,
                unfinishedCandle));
        FacilityEvolutionValidationResult unfinishedValidation = Validate(
            unfinishedDisplay,
            recipe,
            unfinishedProfile);

        Require(!unfinishedProfile.Fixtures.Contains(unfinishedCandle)
                && !unfinishedProfile.HasTag(FacilityEvolutionTerms.Luxury)
                && Mathf.Approximately(
                    unfinishedProfile.GetScore(FacilityEvolutionTerms.Luxury), 0f)
                && !unfinishedValidation.Approved,
            "The unfinished candle construction site must not approve secure-display evolution.");

        using FixtureWorld completedWorld = new();
        BuildableObject completedDisplay = completedWorld.PlaceCompleted(display, new Vector2Int(1, 0));
        BuildableObject completedCounter = completedWorld.PlaceCompleted(
            counter,
            new Vector2Int(3, 0));
        BuildableObject completedCandle = completedWorld.PlaceCompleted(candle, new Vector2Int(5, 0));
        SeedComparableServiceHistory(completedDisplay);
        RoomProfile completedProfile = BuildProfile(
            completedDisplay,
            completedWorld.CreateUsableRoomCache(
                completedDisplay,
                completedCounter,
                completedCandle));
        FacilityEvolutionValidationResult completedValidation = Validate(
            completedDisplay,
            recipe,
            completedProfile);

        Require(completedProfile.Fixtures.Contains(completedCandle)
                && completedProfile.HasTag(FacilityEvolutionTerms.Luxury)
                && completedProfile.GetScore(FacilityEvolutionTerms.Luxury) >= 3f
                && Mathf.Approximately(
                    completedProfile.GetIdentityPressure(FacilityEvolutionTerms.Service),
                    unfinishedProfile.GetIdentityPressure(FacilityEvolutionTerms.Service))
                && completedValidation.Approved,
            "The completed candle BuildableObject must retain the authored secure-display eligibility: "
                + completedValidation.ToMessage());
    }

    private static void SeedComparableServiceHistory(BuildableObject display)
    {
        // Explicit synthetic fixture prerequisite, identical on both sides.
        // A real gameplay visit is checked by the separate campaign, not here.
        // Without service history even a completed fixture is rightly rejected.
        var records = new FacilityEvolutionRecordComponentService(
            new FacilityEvolutionRecordComponentFactory());
        records.GetOrAdd(display).SetMetric(FacilityEvolutionTerms.ServiceQuality, 70f);
    }

    private static RoomProfile BuildProfile(
        BuildableObject facility,
        IRoomLayoutCache roomCache)
    {
        IFacilityEvolutionRecordProvider records =
            new FacilityEvolutionRecordComponentService(
                new FacilityEvolutionRecordComponentFactory());
        return new RoomProfileBuilder(records, roomCache).Build(facility);
    }

    private static FacilityEvolutionValidationResult Validate(
        BuildableObject facility,
        FacilityEvolutionRecipeSO recipe,
        RoomProfile profile)
    {
        IFacilityEvolutionStateComponentFactory states =
            new FacilityEvolutionStateComponentFactory();
        return FacilityEvolutionService.Validate(
            facility,
            recipe,
            profile,
            new BlueprintResearchState(),
            // This authored recipe has no material cost. Supply the explicit
            // empty authority, not a null provider or fabricated materials.
            resources: new EmptyFacilityEvolutionResourceProvider(),
            recipeQuery: new FixtureRecipeQuery(recipe),
            stateComponentFactory: states);
    }

    private static BuildingSO LoadBuilding(string path)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        Require(building != null, "Fixture building asset is missing: " + path);
        return building;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixtureRecipeQuery : IFacilityEvolutionRecipeQuery
    {
        private readonly FacilityEvolutionRecipeSO recipe;

        internal FixtureRecipeQuery(FacilityEvolutionRecipeSO recipe)
        {
            this.recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        }

        public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes() => new[] { recipe };

        public bool IsVisible(
            FacilityEvolutionRecipeSO value,
            BlueprintResearchState researchState) => ReferenceEquals(value, recipe);

        public IReadOnlyList<FacilityEvolutionRecipeSO> GetVisibleRecipes(
            BlueprintResearchState researchState) => new[] { recipe };

        public IReadOnlyList<FacilityEvolutionRecipeSO> GetSourceCandidates(
            BuildableObject facility,
            BlueprintResearchState researchState) => Array.Empty<FacilityEvolutionRecipeSO>();
    }

    private sealed class NoRoomLayoutCache : IRoomLayoutCache
    {
        internal static readonly NoRoomLayoutCache Instance = new();

        public RoomLayout GetLayout(Grid grid) =>
            new RoomLayout(Array.Empty<RoomInstance>());

        public bool TryGetRoom(
            Grid grid,
            Vector2Int cell,
            out RoomInstance room)
        {
            room = null;
            return false;
        }

        public bool TryGetRoom(BuildableObject part, out RoomInstance room)
        {
            room = null;
            return false;
        }

        public void Clear()
        {
        }
    }

    private sealed class FixedRoomLayoutCache : IRoomLayoutCache
    {
        private readonly Grid grid;
        private readonly RoomInstance room;

        internal FixedRoomLayoutCache(Grid grid, RoomInstance room)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public RoomLayout GetLayout(Grid candidate) =>
            ReferenceEquals(candidate, grid)
                ? new RoomLayout(new[] { room })
                : new RoomLayout(Array.Empty<RoomInstance>());

        public bool TryGetRoom(
            Grid candidate,
            Vector2Int cell,
            out RoomInstance result)
        {
            result = ReferenceEquals(candidate, grid) && room.ContainsCell(cell)
                ? room
                : null;
            return result != null;
        }

        public bool TryGetRoom(BuildableObject part, out RoomInstance result)
        {
            result = part != null
                     && ReferenceEquals(part.Grid, grid)
                     && room.ContainsPart(part)
                ? room
                : null;
            return result != null;
        }

        public void Clear()
        {
        }
    }

    private sealed class FixtureWorld : IDisposable
    {
        private readonly List<GameObject> objects = new();

        internal FixtureWorld()
        {
            Grid = new Grid(6, 1);
        }

        internal Grid Grid { get; }

        internal BuildableObject CreateUnregisteredBuilding(
            BuildingSO data,
            Vector2Int position) => CreateBuilding<BuildableObject>(data, position, register: false);

        internal ConstructionSite CreateUnregisteredConstructionSite(
            BuildingSO data,
            Vector2Int position) => CreateBuilding<ConstructionSite>(data, position, register: false);

        internal BuildableObject PlaceCompleted(BuildingSO data, Vector2Int position) =>
            CreateBuilding<BuildableObject>(data, position, register: true);

        internal ConstructionSite PlaceConstructionSite(BuildingSO data, Vector2Int position) =>
            CreateBuilding<ConstructionSite>(data, position, register: true);

        internal IRoomLayoutCache CreateUsableRoomCache(params BuildableObject[] furniture)
        {
            Require(furniture != null
                    && furniture.Length == 3
                    && furniture.All(value => value != null && ReferenceEquals(value.Grid, Grid)),
                "Fixture room must contain the registered profile fixtures.");
            RoomInstance room = new(
                1,
                new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(2, 0),
                    new Vector2Int(3, 0),
                    new Vector2Int(4, 0),
                    new Vector2Int(5, 0)
                },
                furniture,
                Array.Empty<BuildableObject>(),
                Array.Empty<BuildableObject>(),
                4,
                0,
                selfContained: true);
            Require(room.IsUsable
                    && room.IsSelfContained
                    && room.Doors.Count == 0
                    && room.Furniture.Count == furniture.Length
                    && furniture.All(room.ContainsPart)
                    && room.Cells.Count == 6
                    && room.ContainsCell(new Vector2Int(0, 0))
                    && room.ContainsCell(new Vector2Int(1, 0))
                    && room.ContainsCell(new Vector2Int(2, 0))
                    && room.ContainsCell(new Vector2Int(3, 0))
                    && room.ContainsCell(new Vector2Int(4, 0))
                    && room.ContainsCell(new Vector2Int(5, 0)),
                "Fixture room must be usable and contain every profile fixture cell.");
            return new FixedRoomLayoutCache(Grid, room);
        }

        public void Dispose()
        {
            foreach (GameObject value in objects)
            {
                if (value != null)
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }
        }

        private T CreateBuilding<T>(
            BuildingSO data,
            Vector2Int position,
            bool register)
            where T : BuildableObject
        {
            Require(data != null, "Fixture building data is required.");
            GameObject obj = new("RoomProfileConstructionGuard_" + data.objectName);
            objects.Add(obj);
            T building = obj.AddComponent<T>();
            CharacterAiEditorTestDependencies.Inject(building);
            if (register)
            {
                building.SetGrid(Grid);
            }

            building.Initialization(data, position);
            if (register)
            {
                GridLayer layer = building is ConstructionSite
                    ? GridLayer.Construction
                    : data.Placement.Layer;
                Require(Grid.RegisterOccupant(
                            building,
                            layer,
                            data.GetGridPosList(position),
                            data.Placement.IsMovement),
                    "Fixture grid registration failed for " + data.objectName);
            }

            return building;
        }
    }
}
