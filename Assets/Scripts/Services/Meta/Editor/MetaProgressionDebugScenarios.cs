using System;
using DungeonStory.Operation;
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

        RunScenario("Strict meta save restore", VerifyStrictMetaSaveRestore, errors);

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

    public static bool RunStrictSaveInvalidNoMutationOnly() =>
        VerifyStrictMetaSaveRestore();

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

        scenario.GameEvents.Publish(new OperatingDayStartedEvent(4));
        scenario.GameEvents.Publish(new OperatingDayReportEvent(OperatingDayReport.Create(1)));
        scenario.GameEvents.Publish(new OperatingDayReportEvent(OperatingDayReport.Create(2)));
        scenario.GameEvents.Publish(new InvasionStartedEvent(new InvasionThreatSnapshot(
            120f,
            InvasionThreatStage.Candidate,
            new InvasionThreatFactors(2f, 2f, 2f, 1f),
            0f,
            0f)));
        scenario.GameEvents.Publish(new InvasionResolvedEvent(true, 1f));
        scenario.GameEvents.Publish(new FacilityVisitEvent((CharacterActor)null, CreateFacility(9001, "발견 시설")));

        CharacterActor owner = CreateOwner(scenario.Runtime);
        RunResultSnapshot result = scenario.Runtime.EndRun(MetaRuntimeApplicationAdapter.GetOwnerName(CharacterActor.From(owner)), "테스트 사망");

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
            scenario.Runtime.GetExpandedBasicPurchaseBuildingIds(new[]
            {
                new MetaFacilityCandidateSnapshot(second.id, true),
                new MetaFacilityCandidateSnapshot(first.id, true)
            }),
            DefaultBuildingCostMultiplier,
            CharacterAiEditorTestDependencies.AuthoredGameplay);

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
        scenario.GameEvents.Publish(new BlueprintResearchCompletedEvent(blueprint, unlock));
        CharacterActor owner = CreateOwner(scenario.Runtime);
        scenario.Runtime.EndRun(MetaRuntimeApplicationAdapter.GetOwnerName(CharacterActor.From(owner)), "테스트 사망");

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
        AuthoredGameplayCatalog authored = new AuthoredGameplayCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        DungeonSceneRuntimeReferences runReferences =
            EditorRuntimeReferenceFixtures.DungeonWithRunVariables;
        RunVariableRuntime runVariables = runReferences.RunVariables;
        runVariables.Construct(
            new EmptyOwnerRunDataProvider(),
            EditorRuntimeReferenceFixtures.Invasion,
            new RunStartVariableSelector(
                EmptyRunStartVariableCatalog.Instance,
                metaReader,
                authored),
            new DungeonStory.Foundation.RandomStreamProvider(9405),
            new DungeonStory.Foundation.GameEventBus(),
            authored,
            authored,
            new DungeonRuntimeAggregateRootStore());
        runVariables.StartRun(9405);
        RunVariableRuntimeReader runReader = new RunVariableRuntimeReader(
            runReferences,
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
        CharacterAiEditorTestDependencies.Inject(researchFacility);
        researchFacility.Initialization(researchBuilding, Vector2Int.zero);
        FacilityBlueprintSO researchBlueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        researchBlueprint.id = 9404;
        researchBlueprint.blueprintName = "전략 연구 배율 검증";
        researchBlueprint.researchWorkRequired = 100f;
        GameObject researchRuntimeObject = new GameObject("Strategy Research Runtime");
        BlueprintResearchRuntime researchRuntime = researchRuntimeObject.AddComponent<BlueprintResearchRuntime>();
        CharacterAiEditorTestDependencies.Inject(researchRuntime);
        researchRuntime.State.EnqueueBlueprint(researchBlueprint);
        BlueprintResearchWorkService researchService = new BlueprintResearchWorkService(
            new ProgressionSceneRuntimeReferences(null, researchRuntime, null),
            metaReader,
            new EmptyKnowledgeResidueProcessingRuntime());
        BlueprintResearchWorkResult researchResult = researchService.ApplyResearchWork(
            null,
            researchFacility,
            1f);

        bool valid = commercePurchased
            && fortressPurchased
            && arcanePurchased
            && Mathf.Approximately(scenario.Runtime.GetCommerceStockCostMultiplier(true), 0.96f)
            && Mathf.Approximately(scenario.Runtime.GetCommerceStockCostMultiplier(true), 0.96f)
            && Mathf.Approximately(scenario.Runtime.GetCommerceStockCostMultiplier(false), 1f)
            && Mathf.Approximately(scenario.Runtime.GetFortressFacilityCostMultiplier(true), 0.95f)
            && Mathf.Approximately(scenario.Runtime.GetFortressFacilityCostMultiplier(false), 1f)
            && Mathf.Approximately(scenario.Runtime.GetArcaneResearchWorkMultiplier(), 1.08f)
            && Mathf.Approximately(runReader.GetStockCostMultiplier(StockCategory.Food), 0.96f)
            && Mathf.Approximately(runReader.GetFacilityShopCostMultiplier(defense), 0.95f)
            && researchResult.Success
            && Mathf.Approximately(
                researchResult.AddedProgress,
                BlueprintResearchService.CalculateResearchWork(null, researchFacility, 1f) * 1.08f)
            && scenario.Runtime.State.Catalog.All.Count == 9;

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
        MetaUpgradeDefinition definition = new MetaUpgradeDefinition(
            UpgradeId,
            MetaProgressionBranch.OperationKnowledge,
            "테스트 수용량",
            "확장 계약 검증",
            1,
            3,
            new IMetaUpgradeEffect[] { new TestMetaIntegerEffect(EffectId) });
        MetaProgressionState state = new MetaProgressionState(
            new SingleMetaUpgradeCatalog(definition),
            new DungeonRuntimeAggregateRootStore());
        state.SetUpgradeLevelForDebug(UpgradeId, 2);
        var idProperty = typeof(MetaUpgradeDefinition).GetProperty("id");
        bool immutableStableId = idProperty != null
            && idProperty.PropertyType == typeof(string)
            && !idProperty.CanWrite;
        return MetaProgressionEffects.GetIntegerBonus(state, EffectId) == 6
            && state.UpgradeLevels.ContainsKey(UpgradeId)
            && immutableStableId;
    }

    private static CharacterActor CreateOwner(MetaProgressionRuntime runtime)
    {
        CharacterSO data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
            CharacterType.NPC,
            "테스트 사장",
            "Orc",
            CharacterRole.Owner);
        data.id = 9301;
        data.characterName = "테스트 사장";
        data.characterType = CharacterType.NPC;
        data.role = CharacterRole.Owner;
        data.speciesTag = "Orc";

        GameObject obj = new GameObject("Meta Test Owner");
        obj.AddComponent<SpriteRenderer>();
        CharacterActor character = obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        CharacterAiEditorTestDependencies.Inject(obj);
        CharacterAiEditorTestDependencies.InjectCharacterStats(
            character.GetComponent<CharacterStats>(),
            new NoopStaffDiscontentRuntimeService(),
            new RuntimeMetaProgressionReader(runtime),
            new DungeonStory.Foundation.UnityGameClock(),
            CharacterAiEditorTestDependencies.AuthoredGameplay,
            DisabledDungeonDebugRuleQuery.Instance);
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
        CharacterAiEditorTestDependencies.Inject(facility);
        facility.Initialization(building, Vector2Int.zero);
        return facility;
    }

    private static bool VerifyStrictMetaSaveRestore()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        scenario.Runtime.State.AddCurrency(17);
        MetaProgressionSaveSection section = new MetaProgressionSaveSection(
            scenario.Runtime);
        string captured = section.Capture();

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        IDungeonSaveRestoreStage staged = section.StageRestore(
            captured,
            1,
            report);
        int earnedBeforeCommit = scenario.Runtime.State.LifetimeEarnedCurrency;
        staged.Commit(report);
        bool currentVersionRoundTrip =
            scenario.Runtime.State.LifetimeEarnedCurrency == earnedBeforeCommit;

        DungeonMetaProgressionSaveData invalid =
            JsonUtility.FromJson<DungeonMetaProgressionSaveData>(captured);
        invalid.spentCurrency = invalid.lifetimeEarnedCurrency + 1;
        int earnedBeforeInvalid = scenario.Runtime.State.LifetimeEarnedCurrency;
        int spentBeforeInvalid = scenario.Runtime.State.SpentCurrency;
        int runsBeforeInvalid = scenario.Runtime.State.CompletedRunCount;
        bool invalidRejected = false;
        try
        {
            section.StageRestore(
                JsonUtility.ToJson(invalid),
                1,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            invalidRejected = true;
        }

        bool invalidLeftStateUntouched =
            scenario.Runtime.State.LifetimeEarnedCurrency == earnedBeforeInvalid
            && scenario.Runtime.State.SpentCurrency == spentBeforeInvalid
            && scenario.Runtime.State.CompletedRunCount == runsBeforeInvalid;

        bool legacyRejected = false;
        try
        {
            section.StageRestore(captured, 0, new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            legacyRejected = true;
        }

        return currentVersionRoundTrip
            && invalidRejected
            && invalidLeftStateUntouched
            && legacyRejected
            && section is IDungeonRollbackFreeSaveSection
            && section is IDungeonSaveSectionPreflight
            && section is IDungeonStagedSaveSection;
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
                new MetaRunResultBuilder(),
                new MetaRuntimeApplicationAdapter(
                    GameEvents,
                    EditorRuntimeReferenceFixtures.Invasion,
                    runVariables: null,
                    new NoopRunResultPanelService()),
                gameClock,
                CreateAuthoredMetaCatalog(),
                new DungeonRuntimeAggregateRootStore());
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

    private static IMetaUpgradeDefinitionCatalog CreateAuthoredMetaCatalog()
    {
        return new AuthoredGameplayCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
    }

    private sealed class SingleMetaUpgradeCatalog : IMetaUpgradeDefinitionCatalog
    {
        private readonly MetaUpgradeDefinition definition;

        public SingleMetaUpgradeCatalog(MetaUpgradeDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public IReadOnlyCollection<MetaUpgradeDefinition> All =>
            new[] { definition };

        public MetaUpgradeDefinition Get(string id)
        {
            return string.Equals(definition.id, id, StringComparison.Ordinal)
                ? definition
                : null;
        }

        public MetaUpgradeDefinition Require(string id)
        {
            return Get(id) ?? throw new KeyNotFoundException(id);
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

    private sealed class EmptyOwnerRunDataProvider : IOwnerRunDataProvider
    {
        public CharacterSO SelectedOwnerData => null;
    }

    private sealed class EmptyRunStartVariableCatalog : IRunStartVariableCatalog
    {
        public static readonly EmptyRunStartVariableCatalog Instance = new();

        public IReadOnlyCollection<BuildingSO> Buildings =>
            Array.Empty<BuildingSO>();
        public IReadOnlyCollection<CharacterSO> Characters =>
            Array.Empty<CharacterSO>();
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints =>
            Array.Empty<FacilityBlueprintSO>();
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
        public float GetCommerceStockCostMultiplier(StockCategory category) => runtime.GetCommerceStockCostMultiplier(category == StockCategory.Food || category == StockCategory.General);
        public float GetFortressFacilityCostMultiplier(BuildingSO building) => runtime.GetFortressFacilityCostMultiplier(building?.Defense != null && building.Defense.IsDefenseFacility);
        public float GetArcaneResearchWorkMultiplier() => runtime.GetArcaneResearchWorkMultiplier();
        public bool IsRecipePreserved(string recipeId) => runtime.IsRecipePreserved(recipeId);

        public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(
            IEnumerable<BuildingSO> buildings)
        {
            return runtime.GetExpandedBasicPurchaseBuildingIds((buildings ?? Array.Empty<BuildingSO>())
                .Where(building => building != null)
                .Select(building => new MetaFacilityCandidateSnapshot(building.id, !building.IsGridMovement && !building.IsWall && FacilityShopService.GetBuildingStar(building) <= 1)));
        }
    }

    private sealed class EmptyKnowledgeResidueProcessingRuntime :
        IKnowledgeResidueProcessingRuntime
    {
        public IReadOnlyList<KnowledgeResidueTaskSnapshot> Tasks =>
            Array.Empty<KnowledgeResidueTaskSnapshot>();
        public bool TryQueueCodexAnalysis(out string message) { message = string.Empty; return false; }
        public bool TryQueueRegionReconnaissance(string regionId, out string message) { message = string.Empty; return false; }
        public bool HasProcessingWorkFor(BuildableObject facility) => false;
        public BlueprintResearchWorkResult ApplyWork(CharacterActor researcher, BuildableObject facility, float seconds) => default;
        public IReadOnlyList<KnowledgeResidueTaskSaveData> Capture() => Array.Empty<KnowledgeResidueTaskSaveData>();
        public KnowledgeResidueRestoreCandidate PrepareRestore(IEnumerable<KnowledgeResidueTaskSaveData> tasks) =>
            new KnowledgeResidueRestoreCandidate(new KnowledgeResidueAggregateState());
        public void Restore(KnowledgeResidueRestoreCandidate candidate) { }
    }
}
