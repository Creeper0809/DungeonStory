using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MetaProgressionDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Meta/Run P2 Meta Progression Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P2 meta progression scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("사장 사망 런 결과 정산", VerifyOwnerDeathCreatesRunResult, errors);
        RunScenario("생존 보상 우선 계승 화폐", VerifySurvivalRewardDominatesDiscoveryOnly, errors);
        RunScenario("운영 지식 강화 효과", VerifyOperationKnowledgeUpgrades, errors);
        RunScenario("설계 보존 강화 효과", VerifyRecipePreservation, errors);
        RunScenario("사장 생존 강화 효과", VerifyOwnerSurvivalUpgrades, errors);
        RunScenario("세 전략 계승 강화 실제 배율", VerifyStrategyUpgradeEffects, errors);
        RunScenario("미등록 메타 효과/안정 ID 확장", VerifyOpenMetaEffectRegistration, errors);

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
            Debug.Log("P2 meta progression scenarios passed.");
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

    private static bool VerifyOwnerDeathCreatesRunResult()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        using CountingEventAlertRequestListener alerts =
            new CountingEventAlertRequestListener(scenario.GameEvents);
        using CountingRunResultReadyListener results =
            new CountingRunResultReadyListener(scenario.GameEvents);

        scenario.Runtime.OnTriggerEvent(new OperatingDayStartedEvent(4));
        scenario.Runtime.OnTriggerEvent(new OperatingDayReportEvent(OperatingDayReport.Create(1)));
        scenario.Runtime.OnTriggerEvent(new OperatingDayReportEvent(OperatingDayReport.Create(2)));
        scenario.Runtime.OnTriggerEvent(new InvasionStartedEvent(new InvasionThreatSnapshot(
            120f,
            InvasionThreatStage.Candidate,
            new InvasionThreatFactors(2f, 2f, 2f, 1f),
            0f,
            0f)));
        scenario.Runtime.OnTriggerEvent(new InvasionResolvedEvent(true, 1f));
        scenario.Runtime.OnTriggerEvent(new FacilityVisitEvent((CharacterActor)null, CreateFacility(9001, "발견 시설")));

        CharacterActor owner = CreateOwner(scenario.Runtime);
        RunResultSnapshot result = scenario.Runtime.EndRun(CharacterActor.From(owner), "테스트 사망");

        bool valid = result != null
            && result.legacyCurrency > 0
            && result.defendedInvasionCount == 1
            && result.maxThreatStage == InvasionThreatStage.Candidate
            && result.firstDiscoveredFacilityCount == 1
            && scenario.Runtime.State.LifetimeEarnedCurrency == result.legacyCurrency
            && results.Count == 1
            && alerts.Requests.Any((request) => request.Title == "런 결과 정산")
            && result.ToDetailText().Contains("계승되지 않음");

        Object.DestroyImmediate(owner.gameObject);
        return valid;
    }

    private static bool VerifySurvivalRewardDominatesDiscoveryOnly()
    {
        RunResultSnapshot shortDiscovery = new RunResultSnapshot(
            survivalSeconds: 30f,
            survivedOperatingDays: 1,
            settlementCount: 0,
            firstDiscoveredFacilityCount: 15,
            firstUnlockedRecipeCount: 3,
            difficultyMultiplier: 1f);
        RunResultSnapshot longSurvival = new RunResultSnapshot(
            survivalSeconds: 180f * 8f,
            survivedOperatingDays: 8,
            settlementCount: 7,
            firstDiscoveredFacilityCount: 1,
            firstUnlockedRecipeCount: 0,
            difficultyMultiplier: 1f);

        return MetaProgressionCalculator.CalculateLegacyCurrency(longSurvival)
            > MetaProgressionCalculator.CalculateLegacyCurrency(shortDiscovery);
    }

    private static bool VerifyOperationKnowledgeUpgrades()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        scenario.Runtime.State.AddCurrency(500);

        bool purchasedFacility = scenario.Runtime.TryPurchaseUpgrade(
            MetaUpgradeIds.StartingFacilityCandidatePlusOne,
            out _);
        bool purchasedBasic = scenario.Runtime.TryPurchaseUpgrade(
            MetaUpgradeIds.BasicPurchaseListExpansion,
            out _);

        BuildingSO first = CreateBuilding(9101, "1성 테스트 시설 A", false);
        BuildingSO second = CreateBuilding(9102, "1성 테스트 시설 B", false);
        IReadOnlyList<FacilityShopOffer> offers = FacilityShopService.CreateBasicPurchaseOffers(
            new[] { second, first },
            new FacilityShopUnlockState(),
            scenario.Runtime.GetExpandedBasicPurchaseBuildingIds(new[] { second, first }),
            DefaultBuildingCostMultiplier);

        bool valid = purchasedFacility
            && purchasedBasic
            && scenario.Runtime.GetStartingFacilityCandidateBonus() == 1
            && offers.OfType<FacilityBuildingOffer>()
                .Any((offer) => offer.Building == first || offer.Building == second);

        Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
        return valid;
    }

    private static float DefaultBuildingCostMultiplier(BuildingSO building)
    {
        return 1f;
    }

    private static bool VerifyRecipePreservation()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        scenario.Runtime.State.AddCurrency(500);
        scenario.Runtime.TryPurchaseUpgrade(MetaUpgradeIds.SpecialRecipeRecordSlot, out _);

        FacilityBlueprintSO blueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        blueprint.id = 9201;
        blueprint.blueprintName = "보존 테스트 설계도";
        BlueprintUnlockRecord recipeUnlock = new BlueprintUnlockRecord(
            BlueprintUnlockTypeIds.Recipe,
            "조합식",
            "recipe_preserve_test",
            "recipe_preserve_test");
        BlueprintResearchUnlockResult unlock = new BlueprintResearchUnlockResult(
            blueprint,
            new[] { recipeUnlock });
        scenario.Runtime.OnTriggerEvent(new BlueprintResearchCompletedEvent(blueprint, unlock));
        CharacterActor owner = CreateOwner(scenario.Runtime);
        scenario.Runtime.EndRun(CharacterActor.From(owner), "테스트 사망");

        bool valid = scenario.Runtime.IsRecipePreserved("recipe_preserve_test");

        Object.DestroyImmediate(owner.gameObject);
        Object.DestroyImmediate(blueprint);
        return valid;
    }

    private static bool VerifyOwnerSurvivalUpgrades()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        scenario.Runtime.State.AddCurrency(500);
        bool healthPurchased = scenario.Runtime.TryPurchaseUpgrade(MetaUpgradeIds.OwnerSurvivalBonus, out _);
        bool traitPurchased = scenario.Runtime.TryPurchaseUpgrade(MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne, out _);
        bool warningPurchased = scenario.Runtime.TryPurchaseUpgrade(MetaUpgradeIds.InvasionWarningAccuracy, out _);

        CharacterActor owner = CreateOwner(scenario.Runtime);
        bool valid = healthPurchased
            && traitPurchased
            && warningPurchased
            && scenario.Runtime.GetOwnerMaxHealthMultiplier() > 1f
            && scenario.Runtime.GetStartingOwnerTraitCandidateBonus() == 1
            && scenario.Runtime.GetInvasionWarningThresholdMultiplier() < 1f
            && owner.MaxHealth > 100f;

        Object.DestroyImmediate(owner.gameObject);
        return valid;
    }

    private static bool VerifyStrategyUpgradeEffects()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        scenario.Runtime.State.AddCurrency(1000);
        bool commercePurchased = scenario.Runtime.TryPurchaseUpgrade(MetaUpgradeIds.CommerceSupplyNetwork, out _);
        bool fortressPurchased = scenario.Runtime.TryPurchaseUpgrade(MetaUpgradeIds.FortressEngineering, out _);
        bool arcanePurchased = scenario.Runtime.TryPurchaseUpgrade(MetaUpgradeIds.ArcaneResearchMethod, out _);

        BuildingSO defense = CreateBuilding(9401, "전략 방어 시설", true);
        BuildingSO general = CreateBuilding(9402, "전략 일반 시설", false);
        RuntimeMetaProgressionReader metaReader = new RuntimeMetaProgressionReader(scenario.Runtime);
        RunVariableRuntimeReader runReader = new RunVariableRuntimeReader(
            new EmptyRunVariableRuntimeProvider(),
            metaReader);

        BuildingSO researchBuilding = CreateBuilding(9403, "전략 연구 시설", false);
        researchBuilding.Facility = new FacilityData
        {
            roles = FacilityRole.Research,
            capacity = 1
        };
        researchBuilding.Facility.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Research });
        GameObject researchFacilityObject = new GameObject("Strategy Research Facility");
        BuildableObject researchFacility = researchFacilityObject.AddComponent<BuildableObject>();
        researchFacility.Initialization(researchBuilding, Vector2Int.zero);
        FacilityBlueprintSO researchBlueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        researchBlueprint.id = 9404;
        researchBlueprint.blueprintName = "전략 연구 배율 검증";
        researchBlueprint.researchWorkRequired = 100f;
        GameObject researchRuntimeObject = new GameObject("Strategy Research Runtime");
        BlueprintResearchRuntime researchRuntime = researchRuntimeObject.AddComponent<BlueprintResearchRuntime>();
        researchRuntime.State.EnqueueBlueprint(researchBlueprint);
        BlueprintResearchWorkService researchService = new BlueprintResearchWorkService(
            new FixedBlueprintResearchRuntimeProvider(researchRuntime),
            metaReader);
        BlueprintResearchWorkResult researchResult = researchService.ApplyResearchWork(
            null,
            researchFacility,
            1f);

        bool valid = commercePurchased
            && fortressPurchased
            && arcanePurchased
            && Mathf.Approximately(scenario.Runtime.GetCommerceStockCostMultiplier(StockCategory.Food), 0.96f)
            && Mathf.Approximately(scenario.Runtime.GetCommerceStockCostMultiplier(StockCategory.General), 0.96f)
            && Mathf.Approximately(scenario.Runtime.GetCommerceStockCostMultiplier(StockCategory.Mana), 1f)
            && Mathf.Approximately(scenario.Runtime.GetFortressFacilityCostMultiplier(defense), 0.95f)
            && Mathf.Approximately(scenario.Runtime.GetFortressFacilityCostMultiplier(general), 1f)
            && Mathf.Approximately(scenario.Runtime.GetArcaneResearchWorkMultiplier(), 1.08f)
            && Mathf.Approximately(runReader.GetStockCostMultiplier(StockCategory.Food), 0.96f)
            && Mathf.Approximately(runReader.GetFacilityShopCostMultiplier(defense), 0.95f)
            && researchResult.Success
            && Mathf.Approximately(
                researchResult.AddedProgress,
                BlueprintResearchService.CalculateResearchWork(null, researchFacility, 1f) * 1.08f)
            && MetaProgressionCatalog.All.Count == 9;

        Object.DestroyImmediate(defense);
        Object.DestroyImmediate(general);
        Object.DestroyImmediate(researchFacilityObject);
        Object.DestroyImmediate(researchRuntimeObject);
        Object.DestroyImmediate(researchBuilding);
        Object.DestroyImmediate(researchBlueprint);
        return valid;
    }

    private static bool VerifyOpenMetaEffectRegistration()
    {
        const string UpgradeId = "meta:test:custom-capacity";
        const string EffectId = "meta:test:custom-capacity-value";
        try
        {
            MetaProgressionCatalog.Register(new MetaUpgradeDefinition(
                UpgradeId,
                MetaProgressionBranch.OperationKnowledge,
                "테스트 수용량",
                "확장 계약 검증",
                1,
                3,
                new IMetaUpgradeEffect[] { new TestMetaIntegerEffect(EffectId) }));

            MetaProgressionState state = new MetaProgressionState();
            state.SetUpgradeLevelForDebug(UpgradeId, 2);
            var idProperty = typeof(MetaUpgradeDefinition).GetProperty("id");
            bool immutableStableId = idProperty != null
                && idProperty.PropertyType == typeof(string)
                && !idProperty.CanWrite;
            return MetaProgressionEffects.GetIntegerBonus(state, EffectId) == 6
                && state.UpgradeLevels.ContainsKey(UpgradeId)
                && immutableStableId;
        }
        finally
        {
            MetaProgressionCatalog.ResetToBuiltIns();
        }
    }

    private static CharacterActor CreateOwner(MetaProgressionRuntime runtime)
    {
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        data.id = 9301;
        data.characterName = "테스트 사장";
        data.characterType = CharacterType.NPC;
        data.role = CharacterRole.Owner;
        data.speciesTag = "Orc";

        GameObject obj = new GameObject("Meta Test Owner");
        obj.AddComponent<SpriteRenderer>();
        CharacterActor character = obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityWork>();
        obj.AddComponent<AIBrain>();
        CharacterAiEditorTestDependencies.Inject(obj);
        character.GetComponent<CharacterStats>()?.ConstructCharacterStats(
            new NoopStaffDiscontentRuntimeService(),
            new NoopOwnerRunLifecycleService(),
            new RuntimeMetaProgressionReader(runtime),
            new DungeonStory.Foundation.UnityGameClock(),
            gameEventBus: CharacterAiEditorTestDependencies.GameEvents);
        character.RefreshAbilityCache();
        character.Initialization(data);
        character.SetLifecycleState(CharacterLifecycleState.Active);
        return character;
    }

    private static BuildableObject CreateFacility(int id, string name)
    {
        BuildingSO building = CreateBuilding(id, name, false);
        GameObject obj = new GameObject(name);
        BuildableObject facility = obj.AddComponent<BuildableObject>();
        facility.Initialization(building, Vector2Int.zero);
        return facility;
    }

    private static BuildingSO CreateBuilding(int id, string name, bool defense)
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = id;
        building.objectName = name;
        building.width = 1;
        building.height = 1;
        building.layer = GridLayer.Building;
        building.category = defense ? BuildingCategory.Special : BuildingCategory.Shop;
        if (defense)
        {
            building.Defense = new DefenseFacilityData
            {
                enabled = true,
                concept = DefenseAttackConcept.Physical,
                star = 1
            };
        }

        return building;
    }

    private sealed class ScenarioRuntime : IDisposable
    {
        private readonly GameObject runtimeObject;

        public MetaProgressionRuntime Runtime { get; }
        public DungeonStory.Foundation.IGameEventBus GameEvents { get; }

        public ScenarioRuntime()
        {
            DungeonStory.Foundation.UnityGameClock gameClock =
                new DungeonStory.Foundation.UnityGameClock();
            GameEvents = new DungeonStory.Foundation.GameEventBus();
            runtimeObject = new GameObject("Meta Progression Scenario Runtime");
            Runtime = runtimeObject.AddComponent<MetaProgressionRuntime>();
            Runtime.Construct(
                new MetaRunResultBuilder(
                    new MissingThreatRuntimeProvider(),
                    gameClock),
                new NoopRunResultPanelService(),
                gameClock,
                GameEvents);
            Runtime.SetShowRunResultPanel(false);
            Runtime.StartNewRun();
        }

        public void Dispose()
        {
            foreach (BuildableObject facility in Object.FindObjectsByType<BuildableObject>(FindObjectsSortMode.None)
                         .Where((building) => building != null && building.name.Contains("발견 시설")))
            {
                BuildingSO buildingData = facility.BuildingData;
                Object.DestroyImmediate(facility.gameObject);
                if (buildingData != null)
                {
                    Object.DestroyImmediate(buildingData);
                }
            }

            Object.DestroyImmediate(runtimeObject);
        }
    }

    private sealed class CountingRunResultReadyListener : IDisposable
    {
        private readonly IDisposable subscription;
        public int Count { get; private set; }

        public CountingRunResultReadyListener(DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription = gameEventBus.Subscribe<RunResultReadyEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(RunResultReadyEvent eventType)
        {
            if (eventType.result != null)
            {
                Count++;
            }
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }

    private sealed class CountingEventAlertRequestListener : IDisposable
    {
        private readonly List<EventAlertRequest> requests = new List<EventAlertRequest>();
        private readonly IDisposable subscription;

        public IReadOnlyList<EventAlertRequest> Requests => requests;

        public CountingEventAlertRequestListener(
            DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription =
                gameEventBus.Subscribe<EventAlertRequestedEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(EventAlertRequestedEvent eventType)
        {
            if (eventType.request != null)
            {
                requests.Add(eventType.request);
            }
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }

    private sealed class TestMetaIntegerEffect : IMetaIntegerBonusEffect
    {
        public TestMetaIntegerEffect(string effectId)
        {
            EffectId = effectId;
        }

        public string EffectId { get; }

        public int GetBonus(int level)
        {
            return level * 3;
        }
    }

    private sealed class MissingThreatRuntimeProvider : IInvasionThreatRuntimeProvider
    {
        public bool TryGetRuntime(out InvasionThreatRuntime runtime)
        {
            runtime = null;
            return false;
        }
    }

    private sealed class NoopRunResultPanelService : IRunResultPanelService
    {
        public RunResultPanel Show(RunResultSnapshot result)
        {
            return null;
        }
    }

    private sealed class NoopStaffDiscontentRuntimeService : IStaffDiscontentRuntimeService
    {
        public float GetWorkEfficiencyMultiplier(CharacterActor staff) => 1f;

        public bool ShouldBlockWork(CharacterActor staff, out string reason)
        {
            reason = string.Empty;
            return false;
        }

        public bool IsRebellionTarget(CharacterActor target) => false;
        public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender) => false;
    }

    private sealed class NoopOwnerRunLifecycleService : IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor owner, string reason)
        {
        }
    }

    private sealed class RuntimeMetaProgressionReader : IMetaProgressionRuntimeReader
    {
        private readonly MetaProgressionRuntime runtime;

        public RuntimeMetaProgressionReader(MetaProgressionRuntime runtime)
        {
            this.runtime = runtime;
        }

        public int GetStartingFacilityCandidateBonus() => runtime.GetStartingFacilityCandidateBonus();
        public int GetStartingOwnerTraitCandidateBonus() => runtime.GetStartingOwnerTraitCandidateBonus();
        public float GetOwnerMaxHealthMultiplier() => runtime.GetOwnerMaxHealthMultiplier();
        public float GetInvasionWarningThresholdMultiplier() => runtime.GetInvasionWarningThresholdMultiplier();
        public float GetCommerceStockCostMultiplier(StockCategory category) => runtime.GetCommerceStockCostMultiplier(category);
        public float GetFortressFacilityCostMultiplier(BuildingSO building) => runtime.GetFortressFacilityCostMultiplier(building);
        public float GetArcaneResearchWorkMultiplier() => runtime.GetArcaneResearchWorkMultiplier();
        public bool IsRecipePreserved(string recipeId) => runtime.IsRecipePreserved(recipeId);

        public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(
            IEnumerable<BuildingSO> buildings)
        {
            return runtime.GetExpandedBasicPurchaseBuildingIds(buildings);
        }
    }

    private sealed class EmptyRunVariableRuntimeProvider : IRunVariableRuntimeProvider
    {
        public bool TryGetRuntime(out RunVariableRuntime runtime)
        {
            runtime = null;
            return false;
        }
    }

    private sealed class FixedBlueprintResearchRuntimeProvider : IBlueprintResearchRuntimeProvider
    {
        private readonly BlueprintResearchRuntime runtime;

        public FixedBlueprintResearchRuntimeProvider(BlueprintResearchRuntime runtime)
        {
            this.runtime = runtime;
        }

        public bool TryGetRuntime(out BlueprintResearchRuntime resolved)
        {
            resolved = runtime;
            return resolved != null;
        }
    }
}
