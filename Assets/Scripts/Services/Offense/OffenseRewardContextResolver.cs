using System.Collections.Generic;
using System;
using System.Linq;

public sealed class OffenseRewardDebugContext
{
    public GameSessionState gameData;
    public IEnumerable<IWarehouseFacility> warehouses;
    public FacilityShopUnlockState shopUnlockState;
    public BlueprintResearchState researchState;

    public void Clear()
    {
        gameData = null;
        warehouses = null;
        shopUnlockState = null;
        researchState = null;
    }
}

public interface IOffenseRewardContextBuilder
{
    OffenseRewardContext Create(
        OffenseTargetDefinition target,
        OffenseRewardState state,
        OffenseRewardDebugContext debugContext,
        string expeditionId = "");
}

public sealed class OffenseRewardContextBuilder : IOffenseRewardContextBuilder
{
    private readonly BlueprintResearchRuntime researchRuntime;
    private readonly DailyFacilityShopRuntime shopRuntime;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IWarehouseWorldQuery warehouseWorld;
    private readonly IOffenseRegionRuntime regionRuntime;
    private readonly IOffenseReturnArrivalRuntime returnArrivalRuntime;

    public OffenseRewardContextBuilder(
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IGameSessionStateProvider gameDataProvider,
        IWarehouseWorldQuery warehouseWorld,
        IOffenseRegionRuntime regionRuntime,
        IOffenseReturnArrivalRuntime returnArrivalRuntime)
    {
        progressionRuntimes = progressionRuntimes
            ?? throw new ArgumentNullException(nameof(progressionRuntimes));
        researchRuntime = progressionRuntimes.BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseRewardContextBuilder)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        shopRuntime = progressionRuntimes.FacilityShop
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseRewardContextBuilder)} requires a loaded {nameof(DailyFacilityShopRuntime)}.");
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.warehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
        this.regionRuntime = regionRuntime ?? new OffenseRegionRuntime();
        this.returnArrivalRuntime = returnArrivalRuntime;
    }

    public OffenseRewardContext Create(
        OffenseTargetDefinition target,
        OffenseRewardState state,
        OffenseRewardDebugContext debugContext,
        string expeditionId = "")
    {
        gameDataProvider.TryGetSessionState(out GameSessionState runtimeGameData);
        OffenseRewardContextSelection selection = OffenseRewardContextSelectionRules.Select(
            new OffenseRewardContextAvailabilitySnapshot(
                hasDebugGameData: debugContext?.gameData != null,
                hasDebugWarehouses: debugContext?.warehouses != null,
                hasDebugShopUnlockState: debugContext?.shopUnlockState != null,
                hasDebugResearchState: debugContext?.researchState != null,
                hasShopRuntime: shopRuntime != null,
                hasResearchRuntime: researchRuntime != null),
            expeditionId);

        return new OffenseRewardContext
        {
            gameData = selection.GameDataSource == OffenseRewardContextSource.DebugOverride
                ? debugContext.gameData
                : runtimeGameData,
            warehouses = selection.WarehouseSource == OffenseRewardContextSource.DebugOverride
                ? debugContext.warehouses
                : warehouseWorld.Warehouses,
            shopUnlockState = ResolveShopUnlockState(selection, debugContext),
            researchState = ResolveResearchState(selection, debugContext),
            researchRuntime = selection.IncludeResearchRuntime ? researchRuntime : null,
            rewardState = state,
            regionRuntime = regionRuntime,
            returnArrivalRuntime = returnArrivalRuntime,
            expeditionId = selection.ExpeditionId,
            target = target
        };
    }

    private FacilityShopUnlockState ResolveShopUnlockState(
        OffenseRewardContextSelection selection,
        OffenseRewardDebugContext debugContext)
    {
        return selection.ShopUnlockStateSource switch
        {
            OffenseRewardContextSource.DebugOverride => debugContext.shopUnlockState,
            OffenseRewardContextSource.ShopRuntime => shopRuntime.UnlockState,
            OffenseRewardContextSource.ResearchRuntime => researchRuntime.ShopUnlockState,
            _ => null
        };
    }

    private BlueprintResearchState ResolveResearchState(
        OffenseRewardContextSelection selection,
        OffenseRewardDebugContext debugContext)
    {
        return selection.ResearchStateSource switch
        {
            OffenseRewardContextSource.DebugOverride => debugContext.researchState,
            OffenseRewardContextSource.ResearchRuntime => researchRuntime.State,
            _ => null
        };
    }
}

public sealed class OffenseRewardSelector : IOffenseRewardSelector
{
    private readonly IOffenseRewardCatalog catalog;

    public OffenseRewardSelector(IOffenseRewardCatalog catalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public BuildingSO SelectRareFacility(
        OffenseRewardContext context,
        IReadOnlyCollection<int> additionallyExcludedBuildingIds)
    {
        HashSet<int> alreadyGranted = context.rewardState != null
            ? context.rewardState.RareFacilityBuildingIds.ToHashSet()
            : new HashSet<int>();
        if (additionallyExcludedBuildingIds != null)
        {
            foreach (int buildingId in additionallyExcludedBuildingIds)
            {
                alreadyGranted.Add(buildingId);
            }
        }

        List<BuildingSO> buildings = new List<BuildingSO>();
        List<OffenseRareFacilityCandidateSnapshot> candidates =
            new List<OffenseRareFacilityCandidateSnapshot>();
        foreach (BuildingSO building in catalog.Buildings)
        {
            if (building == null)
            {
                continue;
            }

            int sourceIndex = buildings.Count;
            bool isGridMovement = building.IsGridMovement;
            bool isWall = !isGridMovement && building.IsWall;
            int star = !isGridMovement && !isWall
                ? FacilityShopService.GetBuildingStar(building)
                : 0;
            buildings.Add(building);
            candidates.Add(new OffenseRareFacilityCandidateSnapshot(
                sourceIndex,
                building.id,
                star,
                isGridMovement,
                isWall));
        }

        int selectedSourceIndex =
            OffenseRewardSelectionPolicy.SelectRareFacilitySourceIndex(
                candidates,
                alreadyGranted);
        return selectedSourceIndex >= 0
            ? buildings[selectedSourceIndex]
            : null;
    }

    public FacilityBlueprintSO SelectBlueprint(
        OffenseBlueprintRewardSpec rewardSpec,
        OffenseRewardContext context)
    {
        IReadOnlyCollection<FacilityBlueprintSO> catalogBlueprints =
            catalog.Blueprints;
        HashSet<int> acquired = context.rewardState != null
            ? context.rewardState.AcquiredBlueprintIds.ToHashSet()
            : new HashSet<int>();
        List<FacilityBlueprintSO> blueprints = new List<FacilityBlueprintSO>();
        List<OffenseBlueprintCandidateSnapshot> candidates =
            new List<OffenseBlueprintCandidateSnapshot>();

        foreach (FacilityBlueprintSO blueprint in catalogBlueprints)
        {
            if (blueprint == null)
            {
                continue;
            }

            bool eligible = rewardSpec != null
                && rewardSpec.IsEligible(blueprint, catalog.Buildings);
            bool rewardAcquired = eligible && acquired.Contains(blueprint.id);
            bool shopAcquired = false;
            bool researchCompleted = false;
            if (eligible && !rewardAcquired)
            {
                shopAcquired = context.shopUnlockState != null
                    && context.shopUnlockState.IsBlueprintAcquired(blueprint);
                if (!shopAcquired)
                {
                    researchCompleted = context.researchState != null
                        && context.researchState.IsCompleted(blueprint);
                }
            }

            int sourceIndex = blueprints.Count;
            blueprints.Add(blueprint);
            candidates.Add(new OffenseBlueprintCandidateSnapshot(
                sourceIndex,
                blueprint.id,
                (int)blueprint.rarity,
                eligible,
                rewardAcquired,
                shopAcquired,
                researchCompleted));
        }

        int selectedSourceIndex =
            OffenseRewardSelectionPolicy.SelectBlueprintSourceIndex(candidates);
        return selectedSourceIndex >= 0
            ? blueprints[selectedSourceIndex]
            : null;
    }
}
