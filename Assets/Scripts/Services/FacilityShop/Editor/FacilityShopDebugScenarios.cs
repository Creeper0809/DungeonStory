using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FacilityShopDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Facility Shop/Run P1 Facility Shop Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 facility shop scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        P1FacilityShopAssetBuilder.EnsureP1FacilityShopAssets();

        List<string> errors = new List<string>();
        RunScenario("일일 상품 시설/설계도 포함", VerifyDailyOffersContainBuildingAndBlueprint, errors);
        RunScenario("희귀 상품 랜덤 등장", VerifyRareOffersAppearRandomly, errors);
        RunScenario("기본 구매 해금", VerifyBasicPurchaseUnlocksLowStarsOnly, errors);
        RunScenario("시설 구매", VerifyBuildingPurchaseUsesMoneyAndUnlocksBuilding, errors);
        RunScenario("설계도 구매", VerifyBlueprintPurchaseUsesMoneyAndRecordsBlueprint, errors);
        RunScenario("표시 이름과 시설 등급 분리", VerifyBuildingStarUsesQualityAbility, errors);
        RunScenario("새 상품 타입 다형 구매", VerifyCustomOfferPurchasesWithoutServiceBranch, errors);
        RunScenario("운영일 후 상점 갱신", VerifyRuntimeRefreshesAfterOperatingDay, errors);
        RunScenario("정산 보고서 시설 상점 항목", VerifySettlementReportIncludesFacilityShop, errors);

        RunScenario("Day 1 strategy blueprint candidate", VerifyStartingBlueprintCandidateIsGuaranteed, errors);
        RunScenario("Run start refreshes Day 1 strategy offer", VerifyRunStartRefreshesStrategyOffer, errors);

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
            Debug.Log("P1 facility shop scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyDailyOffersContainBuildingAndBlueprint()
    {
        IReadOnlyList<FacilityShopOffer> offers = CreateDailyOffersForScenario(1);

        return offers.Count >= 4
            && offers.OfType<FacilityBuildingOffer>().Any((offer) => offer.Building != null)
            && offers.OfType<FacilityBlueprintOffer>().Any((offer) => offer.Blueprint != null)
            && offers.All((offer) => offer.IsValid && offer.Cost > 0)
            && offers.All((offer) => offer is not FacilityBuildingOffer || offer.Star <= 2);
    }

    private static bool VerifyStartingBlueprintCandidateIsGuaranteed()
    {
        int[] strategyBlueprintIds =
        {
            RunStrategyBlueprintIds.CommerceBasics,
            RunStrategyBlueprintIds.FortressBasics,
            RunStrategyBlueprintIds.ArcaneBasics
        };

        foreach (int strategyBlueprintId in strategyBlueprintIds)
        {
            IReadOnlyList<FacilityShopOffer> offers = FacilityShopService.CreateDailyOffers(
                1,
                LoadAllBuildings(),
                LoadAllBlueprints(),
                0,
                DefaultBuildingCostMultiplier,
                DefaultBlueprintCostMultiplier,
                new[] { strategyBlueprintId });
            if (!offers.OfType<FacilityBlueprintOffer>()
                .Any((offer) => offer.Blueprint != null && offer.Blueprint.id == strategyBlueprintId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool VerifyRareOffersAppearRandomly()
    {
        bool sawRare = false;
        bool sawNoRare = false;
        for (int day = 1; day <= 40; day++)
        {
            IReadOnlyList<FacilityShopOffer> offers = CreateDailyOffersForScenario(day);
            bool hasRare = offers.Any((offer) => offer.Rarity != FacilityShopRarity.Common);
            sawRare |= hasRare;
            sawNoRare |= !hasRare;
        }

        return sawRare && sawNoRare;
    }

    private static bool VerifyBasicPurchaseUnlocksLowStarsOnly()
    {
        BuildingSO oneStar = LoadBuilding("P1_SpikeTrap");
        BuildingSO twoStar = CreateSyntheticDefenseBuilding(9202, "2성 테스트 방어 시설", 2);
        BuildingSO threeStar = CreateSyntheticDefenseBuilding(9203, "3성 테스트 방어 시설", 3);
        FacilityShopUnlockState state = new FacilityShopUnlockState();

        bool oneUnlocked = state.UnlockBasicPurchase(oneStar);
        bool twoUnlocked = state.UnlockBasicPurchase(twoStar);
        bool threeRejected = !state.UnlockBasicPurchase(threeStar);
        state.UnlockBasicPurchaseById(threeStar.id);

        IReadOnlyList<FacilityShopOffer> offers = FacilityShopService.CreateBasicPurchaseOffers(
            new[] { oneStar, twoStar, threeStar },
            state,
            Array.Empty<int>(),
            DefaultBuildingCostMultiplier);

        bool valid = oneUnlocked
            && twoUnlocked
            && threeRejected
            && offers.OfType<FacilityBuildingOffer>().Any((offer) => offer.Building == oneStar && offer.IsBasicPurchase)
            && offers.OfType<FacilityBuildingOffer>().Any((offer) => offer.Building.id == 9202 && offer.IsBasicPurchase)
            && offers.All((offer) => offer.Star <= 2)
            && offers.OfType<FacilityBuildingOffer>().All((offer) => offer.Building.id != 9203);

        Object.DestroyImmediate(twoStar);
        Object.DestroyImmediate(threeStar);
        return valid;
    }

    private static bool VerifyBuildingPurchaseUsesMoneyAndUnlocksBuilding()
    {
        BuildingSO source = LoadBuilding("P1_GuardRoom");
        BuildingSO building = Object.Instantiate(source);
        building.id = 9301;
        building.unlocked = false;
        GameData gameData = CreateGameData(500);
        FacilityShopUnlockState state = new FacilityShopUnlockState();
        FacilityShopOffer offer = new FacilityBuildingOffer(
            building,
            120,
            FacilityShopRarity.Common,
            false,
            true);

        bool success = FacilityShopService.TryPurchaseOffer(gameData, offer, state, out FacilityShopPurchaseResult result);

        bool valid = success
            && result.success
            && result.TryGetBuilding(out BuildingSO purchasedBuilding)
            && purchasedBuilding == building
            && building.unlocked
            && gameData.holdingMoney.Value == 380
            && result.message.Contains("구매 완료");

        Object.DestroyImmediate(building);
        Object.DestroyImmediate(gameData);
        return valid;
    }

    private static bool VerifyBlueprintPurchaseUsesMoneyAndRecordsBlueprint()
    {
        FacilityBlueprintSO blueprint = LoadBlueprint("BP_CommercialBasics");
        GameData gameData = CreateGameData(500);
        FacilityShopUnlockState state = new FacilityShopUnlockState();
        FacilityShopOffer offer = new FacilityBlueprintOffer(
            blueprint,
            100,
            blueprint.rarity,
            true);

        bool success = FacilityShopService.TryPurchaseOffer(gameData, offer, state, out FacilityShopPurchaseResult result);
        bool valid = success
            && result.success
            && result.TryGetBlueprint(out FacilityBlueprintSO purchasedBlueprint)
            && purchasedBlueprint == blueprint
            && gameData.holdingMoney.Value == 400
            && state.IsBlueprintAcquired(blueprint);

        Object.DestroyImmediate(gameData);
        return valid;
    }

    private static bool VerifyBuildingStarUsesQualityAbility()
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.objectName = "5성처럼 보이는 일반 시설";
        building.ReplaceAbilities(new BuildingAbilityCollection());

        int defaultStar = FacilityShopService.GetBuildingStar(building);
        building.AbilityModules.Add(new BuildingQualityAbility { star = 3 });
        int configuredStar = FacilityShopService.GetBuildingStar(building);

        Object.DestroyImmediate(building);
        return defaultStar == 1 && configuredStar == 3;
    }

    private static bool VerifyCustomOfferPurchasesWithoutServiceBranch()
    {
        GameData gameData = CreateGameData(100);
        DebugFacilityShopOffer offer = new DebugFacilityShopOffer(35);

        bool success = FacilityShopService.TryPurchaseOffer(
            gameData,
            offer,
            new FacilityShopUnlockState(),
            out FacilityShopPurchaseResult result);

        bool valid = success
            && result.success
            && offer.ApplyCount == 1
            && result.offerTypeId == DebugFacilityShopOffer.TypeId
            && gameData.holdingMoney.Value == 65;

        Object.DestroyImmediate(gameData);
        return valid;
    }

    private static bool VerifyRunStartRefreshesStrategyOffer()
    {
        GameObject runtimeObject = new GameObject("DailyFacilityShopRuntime_StrategyStart_Test");
        DailyFacilityShopRuntime runtime = runtimeObject.AddComponent<DailyFacilityShopRuntime>();
        runtime.ConstructDailyFacilityShopRuntime(
            new EditorFacilityShopCatalog(),
            new FixedBlueprintCandidateRunVariableReader(RunStrategyBlueprintIds.CommerceBasics),
            new NeutralMetaProgressionReader(),
            new DungeonStory.Foundation.GameEventBus());

        runtime.OnTriggerEvent(new RunStartVariablesSelectedEvent(null));
        bool valid = runtime.CurrentOfferDay == 1
            && runtime.CurrentDailyOffers.OfType<FacilityBlueprintOffer>()
                .Any((offer) => offer.Blueprint != null
                    && offer.Blueprint.id == RunStrategyBlueprintIds.CommerceBasics);

        Object.DestroyImmediate(runtimeObject);
        return valid;
    }

    private static bool VerifyRuntimeRefreshesAfterOperatingDay()
    {
        GameObject runtimeObject = new GameObject("DailyFacilityShopRuntime_Test");
        DailyFacilityShopRuntime runtime = runtimeObject.AddComponent<DailyFacilityShopRuntime>();
        runtime.ConstructDailyFacilityShopRuntime(
            new EditorFacilityShopCatalog(),
            new NeutralRunVariableReader(),
            new NeutralMetaProgressionReader(),
            new DungeonStory.Foundation.GameEventBus());
        int refreshCount = 0;
        int lastRefreshDay = 0;
        void RecordRefresh(
            int day,
            IReadOnlyList<FacilityShopOffer> offers,
            IReadOnlyList<FacilityShopOffer> basicPurchaseOffers)
        {
            refreshCount++;
            lastRefreshDay = day;
        }

        runtime.Refreshed += RecordRefresh;

        runtime.OnTriggerEvent(new OperatingDayEndedEvent(2));
        bool valid = runtime.CurrentOfferDay == 3
            && runtime.CurrentDailyOffers.Count > 0
            && refreshCount == 1
            && lastRefreshDay == 3;

        runtime.Refreshed -= RecordRefresh;
        Object.DestroyImmediate(runtimeObject);
        return valid;
    }

    private static bool VerifySettlementReportIncludesFacilityShop()
    {
        GameObject settlementObject = new GameObject("Settlement_FacilityShop_Test");
        OperatingDaySettlementRuntime settlement = settlementObject.AddComponent<OperatingDaySettlementRuntime>();
        GameData gameData = CreateGameData(0);
        EmptyWorldQuery worldQuery = new EmptyWorldQuery();
        settlement.Construct(
            worldQuery,
            worldQuery,
            new EditorFacilityShopCatalog(),
            new NeutralRunVariableReader(),
            new FixedGameDataProvider(gameData),
            new DungeonStory.Foundation.GameEventBus());

        settlement.OnTriggerEvent(new OperatingDayEndedEvent(1));
        OperatingDayReport report = settlement.LatestReport;
        bool valid = report != null
            && report.refreshedFacilityShopOffers.Count > 0
            && report.refreshedFacilityShopOffers.Any((offer) => offer.offerTypeId == FacilityShopOfferTypeIds.Building)
            && report.refreshedFacilityShopOffers.Any((offer) => offer.offerTypeId == FacilityShopOfferTypeIds.Blueprint)
            && report.ToDetailText().Contains("시설 상점 갱신");

        Object.DestroyImmediate(gameData);
        Object.DestroyImmediate(settlementObject);
        return valid;
    }

    private static BuildingSO LoadBuilding(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<BuildingSO>($"Assets/Resources/SO/Building/P1/{assetName}.asset");
    }

    private static FacilityBlueprintSO LoadBlueprint(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>($"Assets/Resources/SO/Blueprint/P1/{assetName}.asset");
    }

    private static IReadOnlyList<FacilityShopOffer> CreateDailyOffersForScenario(int day)
    {
        return FacilityShopService.CreateDailyOffers(
            day,
            LoadAllBuildings(),
            LoadAllBlueprints(),
            0,
            DefaultBuildingCostMultiplier,
            DefaultBlueprintCostMultiplier);
    }

    private static IReadOnlyList<BuildingSO> LoadAllBuildings()
    {
        return AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where((building) => building != null)
            .ToArray();
    }

    private static IReadOnlyList<FacilityBlueprintSO> LoadAllBlueprints()
    {
        return AssetDatabase.FindAssets("t:FacilityBlueprintSO", new[] { "Assets/Resources/SO/Blueprint" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>)
            .Where((blueprint) => blueprint != null)
            .ToArray();
    }

    private static float DefaultBuildingCostMultiplier(BuildingSO building)
    {
        return 1f;
    }

    private static float DefaultBlueprintCostMultiplier(FacilityBlueprintSO blueprint)
    {
        return 1f;
    }

    private static BuildingSO CreateSyntheticDefenseBuilding(int id, string objectName, int star)
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = id;
        building.objectName = objectName;
        building.width = 1;
        building.height = 1;
        building.layer = GridLayer.Building;
        building.category = BuildingCategory.Special;
        building.type = typeof(DefenseFacility);
        building.Defense = new DefenseFacilityData
        {
            enabled = true,
            concept = DefenseAttackConcept.Physical,
            triggerTimings = DefenseTriggerTiming.OnEnter,
            targetRule = DefenseTargetRule.EnteringIntruder,
            star = star
        };
        return building;
    }

    private static GameData CreateGameData(int holdingMoney)
    {
        GameData gameData = ScriptableObject.CreateInstance<GameData>();
        gameData.holdingMoney = new Data<int>();
        gameData.holdingMoney.Initialize(holdingMoney);
        return gameData;
    }

    private sealed class DebugFacilityShopOffer : FacilityShopOffer
    {
        public const string TypeId = "facility-shop.offer.debug";

        public DebugFacilityShopOffer(int cost)
            : base(cost, FacilityShopRarity.Special, false, true)
        {
        }

        public int ApplyCount { get; private set; }
        public override string OfferTypeId => TypeId;
        public override string TypeDisplayName => "테스트";
        public override bool IsValid => true;
        public override int Star => 0;
        public override int DataId => 1;
        public override string DisplayName => "확장 상품";

        protected override string ApplyPurchase(FacilityShopUnlockState unlockState)
        {
            ApplyCount++;
            return "확장 상품 구매 완료";
        }
    }

    private sealed class EditorFacilityShopCatalog : IFacilityShopCatalog
    {
        public IReadOnlyCollection<BuildingSO> Buildings => LoadAllBuildings();
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => LoadAllBlueprints();

        public BuildingSO FindBuildingById(int buildingId)
        {
            return Buildings.FirstOrDefault(building => building != null && building.id == buildingId);
        }
    }

    private sealed class NeutralRunVariableReader : IRunVariableRuntimeReader
    {
        public int GetInitialShopSeed() => 0;
        public IReadOnlyList<int> GetStartingBlueprintCandidateIds() => System.Array.Empty<int>();
        public float GetGuestDemandMultiplier(string speciesTag) => 1f;
        public float GetStockCostMultiplier(StockCategory category) => 1f;
        public float GetFacilityShopCostMultiplier(BuildingSO building) => 1f;
        public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint) => 1f;
        public float GetThreatRiseMultiplier() => 1f;
        public float GetWarningThresholdMultiplier() => 1f;
        public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source) => source;
    }

    private sealed class NeutralMetaProgressionReader : IMetaProgressionRuntimeReader
    {
        public int GetStartingFacilityCandidateBonus() => 0;
        public int GetStartingOwnerTraitCandidateBonus() => 0;
        public float GetOwnerMaxHealthMultiplier() => 1f;
        public float GetInvasionWarningThresholdMultiplier() => 1f;
        public float GetCommerceStockCostMultiplier(StockCategory category) => 1f;
        public float GetFortressFacilityCostMultiplier(BuildingSO building) => 1f;
        public float GetArcaneResearchWorkMultiplier() => 1f;
        public bool IsRecipePreserved(string recipeId) => false;

        public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)
        {
            return Array.Empty<int>();
        }
    }

    private sealed class FixedBlueprintCandidateRunVariableReader : IRunVariableRuntimeReader
    {
        private readonly int blueprintId;

        public FixedBlueprintCandidateRunVariableReader(int blueprintId)
        {
            this.blueprintId = blueprintId;
        }

        public int GetInitialShopSeed() => 0;
        public IReadOnlyList<int> GetStartingBlueprintCandidateIds() => new[] { blueprintId };
        public float GetGuestDemandMultiplier(string speciesTag) => 1f;
        public float GetStockCostMultiplier(StockCategory category) => 1f;
        public float GetFacilityShopCostMultiplier(BuildingSO building) => 1f;
        public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint) => 1f;
        public float GetThreatRiseMultiplier() => 1f;
        public float GetWarningThresholdMultiplier() => 1f;
        public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source) => source;
    }

    private sealed class EmptyWorldQuery :
        ICharacterWorldQuery,
        IBuildingWorldQuery
    {
        public int CharacterVersion => 0;
        public IReadOnlyList<CharacterActor> Characters => Array.Empty<CharacterActor>();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings => Array.Empty<BuildableObject>();
    }

    private sealed class FixedGameDataProvider : IGameDataProvider
    {
        private readonly GameData gameData;

        public FixedGameDataProvider(GameData gameData)
        {
            this.gameData = gameData;
        }

        public bool TryGetGameData(out GameData resolvedGameData)
        {
            resolvedGameData = gameData;
            return resolvedGameData != null;
        }
    }

}
