using System;
using DungeonStory.Operation;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RunVariableDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Run/Run P2 Run Variable Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P2 run variable scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("주인 교리 3종 운영 효과", VerifyOwnerDoctrines, errors);

        RunScenario("시작 변수 선택", VerifyStartVariables, errors);
        RunScenario("전투·생존 난이도 독립", VerifySurvivalPressure, errors);
        RunScenario("운영 이벤트 9종 배율", VerifyOperationVariables, errors);
        RunScenario("운영 이벤트 만료", VerifyOperationVariableExpiration, errors);
        RunScenario("상점/재고 비용 연결", VerifyCostIntegrations, errors);
        RunScenario("침입 변수 5종 설정 연결", VerifyInvasionVariables, errors);
        RunScenario("이벤트 알림 발행", VerifyEventAlerts, errors);
        RunScenario("미등록 효과/안정 ID 확장", VerifyOpenEffectRegistration, errors);
        RunScenario("런 변수 스냅샷 격리", VerifySnapshotIsolation, errors);

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
            Debug.Log("P2 run variable scenarios passed.");
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
        finally
        {
            CleanupLeakedScenarioObjects();
        }

        errors.Add(name);
    }

    private static void CleanupLeakedScenarioObjects()
    {
        foreach (RunVariableRuntime runtime in Object.FindObjectsByType<RunVariableRuntime>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (runtime != null && runtime.gameObject.name == "Run Variable Scenario Runtime")
            {
                Object.DestroyImmediate(runtime.gameObject);
            }
        }
    }

    private static bool VerifyStartVariables()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        CharacterSO owner = CreateCharacter(8101, CharacterType.NPC, "Slime");

        scenario.Runtime.StartRun(12345, owner, InvasionThreatDifficulty.Hard);
        RunStartVariableSnapshot snapshot = scenario.Runtime.State.StartVariables;
        bool valid = snapshot != null
            && snapshot.seed == 12345
            && snapshot.ownerSpeciesTag == "Slime"
            && snapshot.runDifficulty == DungeonDifficulty.Hard
            && snapshot.survivalPressure
                == DungeonSurvivalPressure.Standard
            && snapshot.threatRiseMultiplier > 1f
            && snapshot.ownerDoctrineId == OwnerDoctrineIds.SlimeStewardship
            && snapshot.initialShopSeed != 0
            && snapshot.initialDungeonLayoutId == "wet-front"
            && snapshot.startingBlueprintCandidateIds.Count == 2
            && snapshot.startingBlueprintCandidateIds[0] == RunStrategyBlueprintIds.CommerceBasics;

        Object.DestroyImmediate(owner);
        return valid;
    }

    private static bool VerifySurvivalPressure()
    {
        CharacterSO owner = CreateCharacter(
            8102,
            CharacterType.NPC,
            "Slime");
        try
        {
            using ScenarioRuntime relaxed = new ScenarioRuntime();
            relaxed.Runtime.StartRun(
                54321,
                owner,
                DungeonDifficulty.Hard,
                DungeonSurvivalPressure.Relaxed);
            RunStartVariableSnapshot relaxedStart =
                relaxed.Runtime.State.StartVariables;

            using ScenarioRuntime harsh = new ScenarioRuntime();
            harsh.Runtime.StartRun(
                54321,
                owner,
                DungeonDifficulty.Easy,
                DungeonSurvivalPressure.Harsh);
            RunStartVariableSnapshot harshStart =
                harsh.Runtime.State.StartVariables;

            return relaxedStart.runDifficulty == DungeonDifficulty.Hard
                && relaxedStart.survivalPressure
                    == DungeonSurvivalPressure.Relaxed
                && harshStart.runDifficulty == DungeonDifficulty.Easy
                && harshStart.survivalPressure
                    == DungeonSurvivalPressure.Harsh;
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    private static bool VerifyOwnerDoctrines()
    {
        CharacterSO slime = CreateCharacter(8111, CharacterType.NPC, "Slime");
        CharacterSO orc = CreateCharacter(8112, CharacterType.NPC, "Orc");
        CharacterSO vampire = CreateCharacter(8113, CharacterType.NPC, "Vampire");
        BuildingSO generalBuilding = CreateBuilding(8114, "일반 시설", false);
        BuildingSO defenseBuilding = CreateBuilding(8115, "방어 시설", true);
        FacilityBlueprintSO blueprint = CreateBlueprint(8116, "교리 검증 설계도", 100);

        try
        {
            using ScenarioRuntime slimeScenario = new ScenarioRuntime();
            slimeScenario.Runtime.StartRun(111, slime, InvasionThreatDifficulty.Normal);
            bool slimeValid = slimeScenario.Runtime.State.StartVariables.ownerDoctrineId
                    == OwnerDoctrineIds.SlimeStewardship
                && Mathf.Approximately(
                    slimeScenario.Runtime.GetStockCostMultiplier(StockCategory.Food),
                    0.85f)
                && Mathf.Approximately(
                    slimeScenario.Runtime.GetStockCostMultiplier(StockCategory.General),
                    0.85f)
                && Mathf.Approximately(
                    slimeScenario.Runtime.GetFacilityShopCostMultiplier(generalBuilding),
                    1.1f);

            using ScenarioRuntime orcScenario = new ScenarioRuntime();
            orcScenario.Runtime.StartRun(112, orc, InvasionThreatDifficulty.Normal);
            bool orcValid = orcScenario.Runtime.State.StartVariables.ownerDoctrineId
                    == OwnerDoctrineIds.OrcWarCamp
                && Mathf.Approximately(
                    orcScenario.Runtime.GetFacilityShopCostMultiplier(generalBuilding),
                    1f)
                && Mathf.Approximately(
                    orcScenario.Runtime.GetFacilityShopCostMultiplier(defenseBuilding),
                    0.75f)
                && Mathf.Approximately(orcScenario.Runtime.GetThreatRiseMultiplier(), 1.15f);

            using ScenarioRuntime vampireScenario = new ScenarioRuntime();
            vampireScenario.Runtime.StartRun(113, vampire, InvasionThreatDifficulty.Normal);
            bool vampireValid = vampireScenario.Runtime.State.StartVariables.ownerDoctrineId
                    == OwnerDoctrineIds.VampireForbiddenStudy
                && Mathf.Approximately(
                    vampireScenario.Runtime.GetBlueprintCostMultiplier(blueprint),
                    0.75f)
                && Mathf.Approximately(
                    vampireScenario.Runtime.GetFacilityShopCostMultiplier(generalBuilding),
                    1.1f)
                && Mathf.Approximately(
                    vampireScenario.Runtime.GetStockCostMultiplier(StockCategory.Food),
                    1f);

            return slimeValid && orcValid && vampireValid;
        }
        finally
        {
            Object.DestroyImmediate(slime);
            Object.DestroyImmediate(orc);
            Object.DestroyImmediate(vampire);
            Object.DestroyImmediate(generalBuilding);
            Object.DestroyImmediate(defenseBuilding);
            Object.DestroyImmediate(blueprint);
        }
    }

    private static bool VerifySnapshotIsolation()
    {
        int[] sourceFacilities = { 10, 20 };
        IRunVariableEffect[] sourceEffects =
        {
            new RunGuestDemandEffect("TestSpecies", 2.25f)
        };
        RunStartVariableSnapshot start = new RunStartVariableSnapshot(
            11,
            "Slime",
            DungeonDifficulty.Normal,
            sourceFacilities,
            new[] { "Slime" },
            new[] { 30 },
            12,
            "compact-shop",
            1f);
        RunVariableDefinition definition = new RunVariableDefinition(
            "run:test:snapshot",
            RunVariableCategory.Operation,
            "Snapshot",
            "Snapshot",
            EventAlertImportance.Low,
            1,
            sourceEffects);

        sourceFacilities[0] = 99;
        sourceEffects[0] = null;

        bool mutationRejected = false;
        if (start.startingFacilityCandidateIds is IList<int> facilityIds)
        {
            try
            {
                facilityIds[0] = 77;
            }
            catch (NotSupportedException)
            {
                mutationRejected = true;
            }
        }

        using ScenarioRuntime scenario = new ScenarioRuntime();
        ActiveRunVariable active = scenario.Runtime.ActivateOperationVariable(
            RunVariableIds.SlimeCrowdVisit,
            1,
            false);
        RunVariableActivatedEvent activatedEvent = new RunVariableActivatedEvent(active);
        scenario.Runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));

        return mutationRejected
            && start.startingFacilityCandidateIds[0] == 10
            && definition.effects[0] != null
            && active.RemainingDays == 0
            && activatedEvent.activeVariable.RemainingDays == 1;
    }

    private static bool VerifyOperationVariables()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        foreach (RunVariableDefinition definition in scenario.Runtime.DefinitionCatalog
                     .GetByCategory(RunVariableCategory.Operation))
        {
            scenario.Runtime.ActivateOperationVariable(definition.id, 1, false);
        }

        BuildingSO generalBuilding = CreateBuilding(8201, "일반 시설", false);
        BuildingSO defenseBuilding = CreateBuilding(8202, "방어 시설", true);
        FacilityBlueprintSO blueprint = CreateBlueprint(8301, "테스트 설계도", 100);

        bool guestDemand = scenario.Runtime.GetGuestDemandMultiplier("Slime") > 1f
            && scenario.Runtime.GetGuestDemandMultiplier("Orc") > 1f
            && scenario.Runtime.GetGuestDemandMultiplier("Vampire") > 1f;
        bool stockCosts = scenario.Runtime.GetStockCostMultiplier(StockCategory.Food) > 1f
            && scenario.Runtime.GetStockCostMultiplier(StockCategory.General) < 1f
            && scenario.Runtime.GetStockCostMultiplier(StockCategory.Mana) < 1f;
        bool shopCosts = scenario.Runtime.GetFacilityShopCostMultiplier(generalBuilding) < 1f
            && scenario.Runtime.GetFacilityShopCostMultiplier(defenseBuilding) < 0.75f;
        bool blueprintCosts = scenario.Runtime.GetBlueprintCostMultiplier(blueprint) < 1f;

        Object.DestroyImmediate(generalBuilding);
        Object.DestroyImmediate(defenseBuilding);
        Object.DestroyImmediate(blueprint);
        return guestDemand && stockCosts && shopCosts && blueprintCosts;
    }

    private static bool VerifyOperationVariableExpiration()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        scenario.Runtime.ActivateOperationVariable(RunVariableIds.SlimeCrowdVisit, 1, false);
        bool active = scenario.Runtime.GetGuestDemandMultiplier("Slime") > 1f;

        scenario.Runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));
        bool expired = Math.Abs(scenario.Runtime.GetGuestDemandMultiplier("Slime") - 1f) < 0.001f
            && scenario.Runtime.State.ActiveOperationVariables.Count == 0;

        return active && expired;
    }

    private static bool VerifyCostIntegrations()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        BuildingSO generalBuilding = CreateBuilding(8401, "일반 시설", false);
        FacilityBlueprintSO blueprint = CreateBlueprint(8402, "가격 테스트 설계도", 200);

        int baseFoodCost = StockSupplyService.CreateDailyDeliveryOffers(
                1,
                scenario.Runtime.GetStockCostMultiplier,
                CharacterAiEditorTestDependencies.AuthoredGameplay)
            .First((offer) => offer.category == StockCategory.Food)
            .cost;
        int baseBuildingCost = FacilityShopService.CreateDailyOffers(
                1,
                new[] { generalBuilding },
                Array.Empty<FacilityBlueprintSO>(),
                0,
                scenario.Runtime.GetFacilityShopCostMultiplier,
                scenario.Runtime.GetBlueprintCostMultiplier,
                CharacterAiEditorTestDependencies.AuthoredGameplay)
            .First((offer) => offer is FacilityBuildingOffer)
            .Cost;
        int baseBlueprintCost = FacilityShopService.CreateDailyOffers(
                1,
                Array.Empty<BuildingSO>(),
                new[] { blueprint },
                0,
                scenario.Runtime.GetFacilityShopCostMultiplier,
                scenario.Runtime.GetBlueprintCostMultiplier,
                CharacterAiEditorTestDependencies.AuthoredGameplay)
            .First((offer) => offer is FacilityBlueprintOffer)
            .Cost;

        scenario.Runtime.ActivateOperationVariable(RunVariableIds.FoodDeliveryDelay, 1, false);
        scenario.Runtime.ActivateOperationVariable(RunVariableIds.VisitingMerchant, 1, false);
        scenario.Runtime.ActivateOperationVariable(RunVariableIds.BlueprintRumor, 1, false);

        int eventFoodCost = StockSupplyService.CreateDailyDeliveryOffers(
                1,
                scenario.Runtime.GetStockCostMultiplier,
                CharacterAiEditorTestDependencies.AuthoredGameplay)
            .First((offer) => offer.category == StockCategory.Food)
            .cost;
        int eventBuildingCost = FacilityShopService.CreateDailyOffers(
                1,
                new[] { generalBuilding },
                Array.Empty<FacilityBlueprintSO>(),
                0,
                scenario.Runtime.GetFacilityShopCostMultiplier,
                scenario.Runtime.GetBlueprintCostMultiplier,
                CharacterAiEditorTestDependencies.AuthoredGameplay)
            .First((offer) => offer is FacilityBuildingOffer)
            .Cost;
        int eventBlueprintCost = FacilityShopService.CreateDailyOffers(
                1,
                Array.Empty<BuildingSO>(),
                new[] { blueprint },
                0,
                scenario.Runtime.GetFacilityShopCostMultiplier,
                scenario.Runtime.GetBlueprintCostMultiplier,
                CharacterAiEditorTestDependencies.AuthoredGameplay)
            .First((offer) => offer is FacilityBlueprintOffer)
            .Cost;

        Object.DestroyImmediate(generalBuilding);
        Object.DestroyImmediate(blueprint);
        return eventFoodCost > baseFoodCost
            && eventBuildingCost < baseBuildingCost
            && eventBlueprintCost < baseBlueprintCost;
    }

    private static bool VerifyInvasionVariables()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        InvasionIntruderSettings source = new InvasionIntruderSettings
        {
            secondsToFullFocus = 30f,
            repathIntervalSeconds = 2f,
            facilityDamageIntervalSeconds = 10f,
            finalCombatDamage = 100f,
            finalCombatWindupSeconds = 0.7f
        };

        bool scout = scenario.Runtime.SelectInvasionVariable(RunVariableIds.ScoutTraces, false) != null
            && scenario.Runtime.ApplyInvasionSettings(source) is InvasionIntruderSettings scoutSettings
            && scoutSettings.patternId == InvasionIntruderPatternIds.Hunter
            && scoutSettings.secondsToFullFocus < source.secondsToFullFocus;
        bool ambush = scenario.Runtime.SelectInvasionVariable(RunVariableIds.Ambush, false) != null
            && scenario.Runtime.ApplyInvasionSettings(source) is InvasionIntruderSettings ambushSettings
            && ambushSettings.patternId == InvasionIntruderPatternIds.Ambusher
            && ambushSettings.repathIntervalSeconds < source.repathIntervalSeconds;
        bool armed = scenario.Runtime.SelectInvasionVariable(RunVariableIds.ArmedIntruder, false) != null
            && scenario.Runtime.ApplyInvasionSettings(source) is InvasionIntruderSettings armedSettings
            && armedSettings.patternId == InvasionIntruderPatternIds.Breaker
            && armedSettings.finalCombatDamage > source.finalCombatDamage;
        bool loot = scenario.Runtime.SelectInvasionVariable(RunVariableIds.LootPriority, false) != null
            && scenario.Runtime.ApplyInvasionSettings(source) is InvasionIntruderSettings lootSettings
            && lootSettings.patternId == InvasionIntruderPatternIds.Plunderer
            && lootSettings.facilityDamageIntervalSeconds < source.facilityDamageIntervalSeconds;
        bool tired = scenario.Runtime.SelectInvasionVariable(RunVariableIds.TiredIntruder, false) != null
            && scenario.Runtime.ApplyInvasionSettings(source) is InvasionIntruderSettings tiredSettings
            && tiredSettings.patternId == InvasionIntruderPatternIds.Straggler
            && tiredSettings.finalCombatDamage < source.finalCombatDamage;

        scenario.Runtime.OnTriggerEvent(new InvasionResolvedEvent(true, 0f));
        bool cleared = scenario.Runtime.State.CurrentInvasionVariable == null;

        return scout && ambush && armed && loot && tired && cleared;
    }

    private static bool VerifyEventAlerts()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        using CountingEventAlertRequestListener alerts =
            new CountingEventAlertRequestListener(scenario.GameEvents);

        scenario.Runtime.ActivateOperationVariable(RunVariableIds.OrcFeast, 1);
        scenario.Runtime.SelectInvasionVariable(RunVariableIds.ArmedIntruder);

        return alerts.Requests.Any((request) => request.Title == "오크 회식" && request.Category == "운영 변수")
            && alerts.Requests.Any((request) => request.Title == "무장한 침입자" && request.Category == "침입 변수");
    }

    private static bool VerifyOpenEffectRegistration()
    {
        const string CustomId = "run:test:festival-demand";
        RunVariableDefinition definition = new RunVariableDefinition(
            CustomId,
            RunVariableCategory.Operation,
            "테스트 축제",
            "테스트 종족 수요 증가",
            EventAlertImportance.Low,
            1,
            new IRunVariableEffect[]
            {
                new RunGuestDemandEffect("TestSpecies", 2.25f)
            });
        AuthoredGameplayCatalog authored = CreateAuthoredCatalog();
        using ScenarioRuntime scenario = new ScenarioRuntime(
            new SingleRunVariableCatalog(definition),
            authored);
        ActiveRunVariable active = scenario.Runtime.ActivateOperationVariable(CustomId, 1, false);
        bool noLegacyEffectBag = typeof(RunVariableDefinition)
            .GetField("guestDemandMultiplier") == null
            && typeof(RunVariableDefinition).GetField("finalCombatDamageMultiplier") == null;
        return active != null
            && active.Definition.id == CustomId
            && scenario.Runtime.GetGuestDemandMultiplier("TestSpecies") > 2f
            && noLegacyEffectBag;
    }

    private static BuildingSO CreateBuilding(int id, string name, bool defense)
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = id;
        building.objectName = name;
        building.category = defense ? BuildingCategory.Special : BuildingCategory.Shop;
        building.width = 1;
        building.height = 1;
        building.layer = GridLayer.Building;
        if (defense)
        {
            building.Defense = new DefenseFacilityData
            {
                enabled = true,
                star = 1,
                concept = DefenseAttackConcept.Physical
            };
        }

        return building;
    }

    private static FacilityBlueprintSO CreateBlueprint(int id, string displayName, int defaultCost)
    {
        FacilityBlueprintSO blueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        blueprint.id = id;
        blueprint.blueprintName = displayName;
        blueprint.defaultCost = defaultCost;
        blueprint.rarity = FacilityShopRarity.Common;
        return blueprint;
    }

    private static CharacterSO CreateCharacter(int id, CharacterType type, string speciesTag)
    {
        CharacterSO character = ScriptableObject.CreateInstance<CharacterSO>();
        character.id = id;
        character.characterType = type;
        character.speciesTag = speciesTag;
        return character;
    }

    private sealed class ScenarioRuntime : IDisposable
    {
        private readonly GameObject runtimeObject;

        public RunVariableRuntime Runtime { get; }
        public DungeonStory.Foundation.IGameEventBus GameEvents { get; }

        public ScenarioRuntime()
            : this(CreateAuthoredCatalog(), CreateAuthoredCatalog())
        {
        }

        public ScenarioRuntime(
            IRunVariableDefinitionCatalog variableCatalog,
            IOwnerDoctrineDefinitionCatalog doctrineCatalog)
        {
            runtimeObject = new GameObject("Run Variable Scenario Runtime");
            try
            {
                Runtime = runtimeObject.AddComponent<RunVariableRuntime>();
                GameEvents = new DungeonStory.Foundation.GameEventBus();
                Runtime.Construct(
                    new TestOwnerRunDataProvider(),
                    EditorRuntimeReferenceFixtures.Invasion,
                    new TestRunStartVariableSelector(doctrineCatalog),
                    new DungeonStory.Foundation.RandomStreamProvider(999),
                    GameEvents,
                    variableCatalog,
                    doctrineCatalog,
                    new DungeonRuntimeAggregateRootStore());
                Runtime.StartRun(999, null, InvasionThreatDifficulty.Normal);
            }
            catch
            {
                Object.DestroyImmediate(runtimeObject);
                throw;
            }
        }

        public void Dispose()
        {
            Object.DestroyImmediate(runtimeObject);
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

    private sealed class TestOwnerRunDataProvider : IOwnerRunDataProvider
    {
        public CharacterSO SelectedOwnerData => null;
    }

    private sealed class TestRunStartVariableSelector : IRunStartVariableSelector
    {
        private readonly IOwnerDoctrineDefinitionCatalog doctrines;

        public TestRunStartVariableSelector(IOwnerDoctrineDefinitionCatalog doctrines)
        {
            this.doctrines = doctrines ?? throw new ArgumentNullException(nameof(doctrines));
        }

        public RunStartVariableSnapshot Create(
            int seed,
            CharacterSO ownerData,
            DungeonDifficulty difficulty,
            DungeonSurvivalPressure survivalPressure =
                DungeonSurvivalPressure.Standard)
        {
            string species = !string.IsNullOrWhiteSpace(ownerData?.SpeciesTag)
                ? ownerData.SpeciesTag
                : "Unknown";
            int strategyBlueprintId = RunStrategyBlueprintIds.ResolveForSpecies(species);
            int[] startingBlueprintIds = strategyBlueprintId >= 0
                ? new[] { strategyBlueprintId, 6999 }
                : Array.Empty<int>();
            return new RunStartVariableSnapshot(
                seed,
                species,
                difficulty,
                Array.Empty<int>(),
                Array.Empty<string>(),
                startingBlueprintIds,
                seed ^ 0x5F3759DF,
                string.Equals(species, "Slime", StringComparison.OrdinalIgnoreCase)
                    ? "wet-front"
                    : "compact-shop",
                difficulty == DungeonDifficulty.Hard ? 1.2f : 1f,
                doctrines.ResolveForSpecies(species)?.id,
                survivalPressure);
        }
    }

    private static AuthoredGameplayCatalog CreateAuthoredCatalog()
    {
        return new AuthoredGameplayCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
    }

    private sealed class SingleRunVariableCatalog : IRunVariableDefinitionCatalog
    {
        private readonly RunVariableDefinition definition;

        public SingleRunVariableCatalog(RunVariableDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public IReadOnlyCollection<RunVariableDefinition> All => new[] { definition };

        public RunVariableDefinition Get(string id)
        {
            return string.Equals(id, definition.id, StringComparison.Ordinal)
                ? definition
                : null;
        }

        public RunVariableDefinition Require(string id)
        {
            return Get(id) ?? throw new KeyNotFoundException(id);
        }

        public IReadOnlyList<RunVariableDefinition> GetByCategory(RunVariableCategory category)
        {
            return definition.category == category
                ? new[] { definition }
                : Array.Empty<RunVariableDefinition>();
        }
    }
}
