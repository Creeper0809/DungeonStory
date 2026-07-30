using System;
using System.Collections.Generic;
using System.Linq;
using DamageNumbersPro;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEngine;

internal static class CharacterAiEditorTestDependencies
{
    private static readonly ICharacterAiSchedulingService Scheduling =
        new ImmediateSchedulingService();
    private static readonly EditorCharacterAiPerformanceRecorder PerformanceRecorder =
        new EditorCharacterAiPerformanceRecorder();
    private static readonly IGridPathSearchBroker PathSearchBroker =
        new GridPathSearchBroker(
            new UnityGameClock(),
            performanceRecorder: PerformanceRecorder);
    internal static readonly IGameClock GameClock = new UnityGameClock();
    internal static readonly IUiClock UiClock = new UnityUiClock();
    private static readonly IDynamicFrameWorkBudget FrameWorkBudget =
        new DynamicFrameWorkBudget(GameClock, UiClock);
    internal static readonly IGameEventBus GameEvents = new GameEventBus();
    private static readonly IRandomStreamProvider RandomStreams =
        new RandomStreamProvider(rootSeed: 9173);
    internal static readonly ICharacterAiWorldRegistry WorldRegistry =
        new CharacterAiWorldRegistry(
            new SceneRuntimeRegistry<CharacterActor>(),
            new SceneRuntimeRegistry<WildlifeActor>(),
            new SceneRuntimeRegistry<BuildableObject>(),
            new SceneRuntimeRegistry<IWarehouseFacility>(),
            new SceneRuntimeRegistry<IRetailFacility>(),
            new FixedGameDataProvider(GetGameData()));
    private static readonly ICharacterAiWorldSignalQuery WorldSignalQuery =
        new DefaultCharacterAiWorldSignalQuery(
            WorldRegistry,
            GameClock,
            performanceRecorder: PerformanceRecorder);
    private static readonly IFacilityCandidateCache FacilityCandidates =
        new FacilityCandidateCacheStore(WorldRegistry);
    private static readonly IRoomFacilityPolicy RoomPolicy =
        new RoomFacilityPolicyService(RoomRegistry.EditorCache);
    private static readonly IStaffDiscontentRuntimeService StaffDiscontent =
        new NoopStaffDiscontentService();
    private static readonly IMetaProgressionRuntimeReader MetaProgression =
        new DefaultMetaProgressionReader();
    private static readonly IFloatingIconFeedbackService FloatingIcons =
        new NoopFloatingIconFeedbackService();
    private static readonly IBlueprintResearchWorkService BlueprintResearch =
        new SceneBlueprintResearchWorkService();
    private static readonly IWorkPolicyRegistry WorkPolicies =
        new WorkExecutionHandlerRegistry(
            Array.Empty<IWorkExecutionHandler>(),
            new IWorkCandidateProvider[]
            {
                new ResearchWorkExecutionHandler(BlueprintResearch),
                new EditorRepairCandidateProvider()
            },
            Array.Empty<IWorkUrgencyProvider>());
    private static readonly IWorldInfoClickSelector WorldInfo =
        new NoopWorldInfoClickSelector();
    private static readonly IBuildingAbilityRuntimeDispatcher BuildingAbilities =
        new BuildingAbilityRuntimeDispatcher(
            new IBuildingAbilityWorkCompletedHandler[]
            {
                new EditorSurvivalBuildingAbilityHandler(),
                new ProductionBuildingAbilityHandler(),
                new CleaningBuildingAbilityHandler(),
                new SecurityBuildingAbilityHandler(),
                new ReceptionBuildingAbilityHandler(),
                new PatrolPostBuildingAbilityHandler(),
                new OutdoorRestBuildingAbilityHandler(),
                new ExteriorMaintenanceBuildingAbilityHandler()
            },
            Array.Empty<IBuildingWorkCompletionFallbackHandler>());
    internal static IBuildingAbilityRuntimeDispatcher BuildingAbilityRuntimeDispatcher =>
        BuildingAbilities;

    private sealed class EditorSurvivalBuildingAbilityHandler :
        IBuildingAbilityWorkCompletedHandler
    {
        private static readonly Type[] Types =
        {
            typeof(BuildingWaterSourceAbility),
            typeof(BuildingCookingAbility),
            typeof(BuildingMedicalAbility),
            typeof(BuildingFuelConsumerAbility)
        };

        public IReadOnlyCollection<Type> AbilityTypes => Types;

        public int Apply(
            BuildingAbility ability,
            BuildingAbilityWorkContext context)
        {
            if (ability is BuildingWaterSourceAbility water
                && context.WorkTypeId == BuiltInWorkTypeIds.DrawWater)
            {
                return ModularFacilityRuntimeEffects.Produce(
                    context.Building,
                    StockCategory.Water,
                    Mathf.Max(1, water.waterPerWork));
            }

            if (ability is BuildingCookingAbility cooking
                && context.WorkTypeId == BuiltInWorkTypeIds.Cook)
            {
                return ModularFacilityRuntimeEffects.Produce(
                    context.Building,
                    StockCategory.Food,
                    Mathf.Max(1, cooking.cookedMeals));
            }

            return 0;
        }
    }
    private static readonly IShopStockCatalog ShopStock =
        new AssetDatabaseShopStockCatalog();
    private static readonly IGridSystemProvider GridSystem =
        new EditorGridSystemProvider();
    private static readonly DungeonSceneComponentQuery SceneQuery =
        new DungeonSceneComponentQuery();
    private static readonly ILocalLlmRuntimeProvider LocalLlm =
        new EditorLocalLlmRuntimeProvider(SceneQuery);
    private static readonly CharacterSceneRuntimeReferences CharacterRuntimeReferences =
        new CharacterSceneRuntimeReferences(
            null,
            SceneQuery.First<SocialReputationRuntime>(includeInactive: true),
            SceneQuery.First<StaffDiscontentRuntime>(includeInactive: true),
            SceneQuery.First<RegularCustomerRuntime>(includeInactive: true),
            SceneQuery.First<CharacterSpawner>(includeInactive: true),
            SceneQuery.First<CharacterAiScheduler>(includeInactive: true),
            SceneQuery.First<OwnerRunManager>(includeInactive: true),
            SceneQuery.First<AiDirectorRuntime>(includeInactive: true));
    private static readonly ICharacterSpawnerProvider CharacterSpawner =
        new CharacterSpawnerProvider(CharacterRuntimeReferences);
    private static readonly ICharacterSocialMemoryFactory SocialMemoryFactory =
        new EditorCharacterSocialMemoryFactory();
    private static readonly IAiDirectorContextSceneQuery DirectorContext =
        new AiDirectorContextSceneQuery(WorldRegistry, WorldRegistry);
    private static readonly ICharacterDialogueBubbleFactory DialogueBubbles =
        new EditorCharacterDialogueBubbleFactory();
    private static readonly ICharacterBehaviorTreeRuntimeConfigurator BehaviorTreeConfigurator =
        new CharacterBehaviorTreeRuntimeConfigurator();
    private static readonly IMainCameraProvider MainCamera =
        new OptionalMainCameraProvider();
    private static readonly ICharacterFeedbackBubbleFactory FeedbackBubbles =
        new NoopCharacterFeedbackBubbleFactory();
    private static readonly IOwnerCandidateCatalog OwnerCandidates =
        new EditorOwnerCandidateCatalog();
    private static GameData gameData;

    public static void Inject(GameObject actorObject)
    {
        Inject(actorObject, Scheduling);
    }

    public static void Inject(
        GameObject actorObject,
        ICharacterAiSchedulingService scheduling)
    {
        Inject(actorObject, scheduling, StaffDiscontent);
    }

    public static void Inject(
        GameObject actorObject,
        StaffDiscontentRuntime staffDiscontentRuntime)
    {
        Inject(
            actorObject,
            Scheduling,
            new EditorStaffDiscontentRuntimeService(staffDiscontentRuntime));
    }

    private static void Inject(
        GameObject actorObject,
        ICharacterAiSchedulingService scheduling,
        IStaffDiscontentRuntimeService staffDiscontent)
    {
        if (actorObject == null)
        {
            return;
        }

        scheduling ??= Scheduling;
        PathSearchBroker.BeginFrame(int.MaxValue, enforceBudget: false);

        foreach (CharacterAbility ability in actorObject.GetComponents<CharacterAbility>())
        {
            ability.ConstructCharacterAbility(GridSystem);
        }

        actorObject.GetComponent<AbilityMove>()?.ConstructAbilityMove(
            CharacterSpawner,
            scheduling,
            PathSearchBroker,
            RandomStreams,
            GameClock);

        actorObject.GetComponent<CharacterLifecycle>()?.ConstructCharacterLifecycle(GridSystem);

        actorObject.GetComponent<CharacterStats>()?.ConstructCharacterStats(
            staffDiscontent,
            new NoopOwnerRunLifecycleService(),
            MetaProgression,
            GameClock,
            gameEventBus: GameEvents);

        actorObject.GetComponent<CustomerPersonaRuntime>()?.ConstructCustomerPersonaRuntime(
            LocalLlm);

        actorObject.GetComponent<CharacterDialogueRuntime>()?.ConstructCharacterDialogueRuntime(
            LocalLlm,
            scheduling,
            DialogueBubbles,
            GameClock);
        actorObject.GetComponent<CharacterVisual>()?.ConstructCharacterVisual(GameClock);

        actorObject.GetComponent<AbilityWork>()?.ConstructAbilityWork(
            BlueprintResearch,
            staffDiscontent,
            FloatingIcons,
            new ActiveWorkGridResolver(),
            FacilityCandidates,
            null,
            workPolicyRegistry: WorkPolicies,
            gameClock: GameClock);

        actorObject.GetComponent<AbilityShopping>()?.ConstructAbilityShopping(
            ShopStock,
            FloatingIcons,
            RandomStreams,
            GameClock,
            GameEvents);

        actorObject.GetComponent<AIBrain>()?.ConstructAIBrain(
            new ResourceCharacterAiActionAssetCatalog(new UnityResourcesAssetLoader()),
            scheduling,
            new NeutralSocialReputationBiasService(),
            FacilityCandidates,
            new SceneFacilityLookup(),
            new CharacterAiJobGiverCatalog(),
            new CharacterAiDecisionPipeline(),
            RoomPolicy,
            PathSearchBroker,
            new UnityGameClock(),
            RandomStreams,
            PerformanceRecorder);

        actorObject.GetComponent<CharacterActor>()?.ConstructCharacterActor(
            GridSystem,
            scheduling,
            WorldInfo,
            SocialMemoryFactory,
            FeedbackBubbles,
            MainCamera,
            PathSearchBroker,
            WorldRegistry,
            WorldSignalQuery,
            FrameWorkBudget,
            null,
            null,
            null,
            null,
            null,
            GameClock);
    }

    public static void Inject(SocialReputationRuntime runtime)
    {
        runtime?.ConstructSocialReputationRuntime(
            LocalLlm,
            WorldRegistry,
            WorldRegistry,
            SocialMemoryFactory,
            GameClock,
            RandomStreams);
    }

    public static void Inject(LocalLlmRequestQueue queue)
    {
        queue?.Construct(UiClock);
    }

    private sealed class EditorLocalLlmRuntimeProvider : ILocalLlmRuntimeProvider
    {
        private readonly DungeonSceneComponentQuery sceneQuery;

        public EditorLocalLlmRuntimeProvider(DungeonSceneComponentQuery sceneQuery)
        {
            this.sceneQuery = sceneQuery ?? throw new ArgumentNullException(nameof(sceneQuery));
        }

        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            LocalLlmRequestQueue queue =
                sceneQuery.First<LocalLlmRequestQueue>(includeInactive: true);
            CharacterAiEditorTestDependencies.Inject(queue);
            runtime = queue;
            return queue != null;
        }

        public ILocalLlmRuntime GetRequiredRuntime()
        {
            if (TryGetRuntime(out ILocalLlmRuntime runtime))
            {
                return runtime;
            }

            throw new InvalidOperationException(
                $"Editor AI fixture requires a loaded {nameof(LocalLlmRequestQueue)}.");
        }
    }

    public static void Inject(AiDirectorRuntime runtime)
    {
        runtime?.ConstructAiDirectorRuntime(
            LocalLlm,
            DirectorContext,
            Scheduling,
            new SceneFacilityLookup(),
            GameClock);
    }

    public static void Inject(CharacterAiScheduler scheduler)
    {
        scheduler?.Construct(
            WorldRegistry,
            MainCamera,
            BehaviorTreeConfigurator,
            PathSearchBroker,
            GameClock,
            FrameWorkBudget,
            PerformanceRecorder,
            UiClock,
            FacilityCandidates);
    }

    internal static void ResetPerformanceRecorder(
        bool detailedCollectionEnabled = true)
    {
        PerformanceRecorder.SetDetailedCollectionEnabled(
            detailedCollectionEnabled);
        PerformanceRecorder.Reset();
    }

    internal static CharacterAiPerformanceReport CapturePerformanceReport(int actorCount)
    {
        return PerformanceRecorder.CaptureReport(actorCount);
    }

    public static void Inject(OwnerRunManager manager)
    {
        if (manager != null)
        {
            manager.ConstructOwnerRunManager(
                OwnerCandidates,
                new EditorOwnerCharacterFactory(manager),
                GameEvents);
        }
    }

    public static void Inject(OperatingDaySettlementRuntime runtime)
    {
        runtime?.Construct(
            WorldRegistry,
            WorldRegistry,
            new EmptyFacilityShopCatalog(),
            new NeutralRunVariableReader(),
            new FixedGameDataProvider(GetGameData()),
            GameEvents);
    }

    public static void Inject(StaffDiscontentRuntime runtime)
    {
        runtime?.Construct(WorldRegistry, GameEvents);
    }

    public static void Inject(BlueprintResearchRuntime runtime)
    {
        runtime?.Construct(
            new FixedFacilityShopUnlockStateService(),
            new EmptyFacilityShopCatalog(),
            FacilityCandidates,
            new NoopWorkforceReplanService(),
            GameEvents);
    }

    public static void Inject(
        StaffDiscontentRuntime runtime,
        IEnumerable<GameObject> scenarioRoots)
    {
        runtime?.Construct(WorldRegistry, GameEvents);
    }

    public static void Inject(BuildableObject building)
    {
        building?.ConstructBuildableObject(
            BlueprintResearch,
            WorldInfo,
            FacilityCandidates,
            RoomPolicy,
            worldRegistry: WorldRegistry,
            abilityRuntimeDispatcher: BuildingAbilities,
            gameClock: GameClock);
        building?.ConstructBuildableObjectEventBus(GameEvents);

        if (building is DefenseFacility defenseFacility)
        {
            defenseFacility.ConstructDefenseFacilityEventBus(GameEvents);
        }
    }

    public static void InjectShop(Shop shop)
    {
        shop?.ConstructShop(
            new FixedGameDataProvider(GetGameData()),
            ShopStock,
            new NoopFloatingNumberFeedbackService(),
            new NoopWorkforceReplanService(),
            FacilityCrimeEditorTestDependencies.Evaluator,
            RandomStreams);
    }

    private static GameData GetGameData()
    {
        if (gameData != null)
        {
            return gameData;
        }

        gameData = ScriptableObject.CreateInstance<GameData>();
        gameData.hideFlags = HideFlags.HideAndDontSave;
        gameData.gameSpeed = new Data<int>();
        gameData.holdingMoney = new Data<int>();
        gameData.day = new Data<int>();
        gameData.curTime = new Data<float>();
        gameData.hour = new Data<int>();
        gameData.timeOfDay = new Data<TimeOfDay>();
        gameData.gameSpeed.Initialize(1);
        gameData.holdingMoney.Initialize(100000);
        gameData.day.Initialize(7);
        gameData.curTime.Initialize(0f);
        gameData.hour.Initialize(0);
        gameData.timeOfDay.Initialize(TimeOfDay.Morning);
        return gameData;
    }

    private sealed class ImmediateSchedulingService : ICharacterAiSchedulingService
    {
        public bool IsDrivingAi => false;
        public void Register(CharacterActor actor) { }
        public void Unregister(CharacterActor actor) { }
        public void RequestImmediateDecision(CharacterActor actor) { }
        public bool TryConsumePathSearchBudget() => true;
        public bool ShouldShowCharacterFeedback(CharacterActor actor) => false;
        public bool ShouldCollectDetailedDiagnostics(CharacterActor actor) => true;
        public int GetMovementFrameStride(CharacterActor actor) => 1;
        public double GetDecisionWorkSliceMilliseconds(CharacterActor actor) =>
            double.PositiveInfinity;
        public void ResetPathSearchBudgetForDebug() { }
    }

    private sealed class EditorRepairCandidateProvider : IWorkCandidateProvider
    {
        private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Repair };

        public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

        public bool IsAvailable(
            WorkTypeId workTypeId,
            CharacterActor actor,
            BuildableObject target,
            out string reason)
        {
            bool available = target != null && target.IsDamaged;
            reason = available ? string.Empty : "수리할 손상이 없음";
            return available;
        }
    }

    private sealed class EditorGridSystemProvider : IGridSystemProvider
    {
        private GridSystemManager cachedManager;

        public GridSystemManager Manager
        {
            get
            {
                if (!TryGetManager(out GridSystemManager manager))
                {
                    throw new InvalidOperationException($"{nameof(GridSystemManager)} not found for editor AI fixture.");
                }

                return manager;
            }
        }

        public Grid Grid
        {
            get
            {
                if (!TryGetGrid(out Grid grid))
                {
                    throw new InvalidOperationException($"{nameof(GridSystemManager)} has no grid for editor AI fixture.");
                }

                return grid;
            }
        }

        public bool TryGetManager(out GridSystemManager manager)
        {
            GridSystemManager[] managers =
                UnityEngine.Object.FindObjectsByType<GridSystemManager>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            GridSystemManager scenarioManager = null;
            GridSystemManager staffScenarioManager = null;
            for (int index = 0; index < managers.Length; index++)
            {
                GridSystemManager candidate = managers[index];
                if (candidate == null)
                {
                    continue;
                }

                string objectName = candidate.gameObject.name;
                if (objectName.IndexOf(
                        "Scenario GridSystemManager",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    scenarioManager = candidate;
                    break;
                }

                if (objectName.EndsWith(
                        " GridSystem",
                        StringComparison.OrdinalIgnoreCase))
                {
                    staffScenarioManager = candidate;
                }
            }

            cachedManager = scenarioManager != null
                ? scenarioManager
                : staffScenarioManager != null
                    ? staffScenarioManager
                : managers.Length > 0
                    ? managers[0]
                    : null;
            if (cachedManager == null)
            {
                manager = null;
                return false;
            }

            cachedManager.EnsureGridInitialized();
            manager = cachedManager;
            return true;
        }

        public bool TryGetGrid(out Grid grid)
        {
            if (!TryGetManager(out GridSystemManager manager) || manager.grid == null)
            {
                grid = null;
                return false;
            }

            grid = manager.grid;
            return true;
        }
    }

    private sealed class NeutralSocialReputationBiasService : ISocialReputationBiasService
    {
        public float GetFacilityUtilityBias(CharacterActor actor, BuildableObject building) => 0f;
    }

    private sealed class EditorCharacterSocialMemoryFactory : ICharacterSocialMemoryFactory
    {
        public CharacterSocialMemory GetOrAdd(CharacterActor actor)
        {
            if (actor == null)
            {
                return null;
            }

            CharacterSocialMemory memory = actor.GetComponent<CharacterSocialMemory>();
            if (memory == null)
            {
                memory = actor.gameObject.AddComponent<CharacterSocialMemory>();
            }

            memory.Construct(GameClock);
            memory.Bind(actor);
            return memory;
        }
    }

    private sealed class EditorCharacterDialogueBubbleFactory : ICharacterDialogueBubbleFactory
    {
        public TextMeshPro Create(Transform parent)
        {
            GameObject bubbleObject = new GameObject("EditorCharacterDialogueBubble", typeof(TextMeshPro));
            bubbleObject.transform.SetParent(parent, false);
            return bubbleObject.GetComponent<TextMeshPro>();
        }
    }

    private sealed class OptionalMainCameraProvider : IMainCameraProvider
    {
        public Camera Camera => UnityEngine.Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
    }

    private sealed class NoopCharacterFeedbackBubbleFactory : ICharacterFeedbackBubbleFactory
    {
        public CharacterFeedbackBubble GetOrAdd(CharacterActor actor) => null;
    }

    private sealed class EditorOwnerCandidateCatalog : IOwnerCandidateCatalog
    {
        public IReadOnlyCollection<CharacterSO> OwnerCandidates => AssetDatabase
            .FindAssets("t:CharacterSO", new[] { "Assets/Resources/SO/Character/Owners" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterSO>)
            .Where(candidate => candidate != null && candidate.IsOwnerCandidate)
            .OrderBy(candidate => candidate.id)
            .ToArray();
    }

    private sealed class EditorOwnerCharacterFactory : IOwnerCharacterFactory
    {
        private readonly OwnerRunManager manager;

        public EditorOwnerCharacterFactory(OwnerRunManager manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public CharacterActor CreateOwner(
            CharacterSO ownerData,
            GameObject ownerPrefab,
            Transform ownerSpawnPoint,
            Vector2Int ownerSpawnGridPosition)
        {
            GameObject ownerObject = ownerPrefab != null
                ? UnityEngine.Object.Instantiate(ownerPrefab)
                : new GameObject(ownerData.characterName);

            if (!ownerObject.TryGetComponent(out SpriteRenderer _))
            {
                ownerObject.AddComponent<SpriteRenderer>();
            }

            CharacterActor owner = ownerObject.GetComponent<CharacterActor>()
                ?? ownerObject.AddComponent<CharacterActor>();
            if (!ownerObject.TryGetComponent(out AbilityMove _))
            {
                ownerObject.AddComponent<AbilityMove>();
            }
            if (!ownerObject.TryGetComponent(out AbilityWork _))
            {
                ownerObject.AddComponent<AbilityWork>();
            }
            if (!ownerObject.TryGetComponent(out AIBrain _))
            {
                ownerObject.AddComponent<AIBrain>();
            }

            ownerObject.transform.position = ownerSpawnPoint != null
                ? ownerSpawnPoint.position
                : Vector3.zero;
            Inject(ownerObject);
            ownerObject.GetComponent<CharacterStats>()?.ConstructCharacterStats(
                StaffDiscontent,
                new EditorOwnerRunLifecycleService(manager),
                MetaProgression,
                GameClock,
                gameEventBus: GameEvents);
            owner.EnsureRuntimeState();
            owner.RefreshAbilityCache();
            owner.Initialization(ownerData);
            owner.SetLifecycleState(CharacterLifecycleState.Active);
            owner.Brain?.UseOwnerWorkActions();
            return owner;
        }
    }

    private sealed class EditorOwnerRunLifecycleService : IOwnerRunLifecycleService
    {
        private readonly OwnerRunManager manager;

        public EditorOwnerRunLifecycleService(OwnerRunManager manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public void HandleOwnerDeath(CharacterActor owner, string reason)
        {
            manager.HandleOwnerDeath(owner, reason);
        }
    }

    private sealed class EmptyFacilityShopCatalog : IFacilityShopCatalog
    {
        public IReadOnlyCollection<BuildingSO> Buildings => Array.Empty<BuildingSO>();
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => Array.Empty<FacilityBlueprintSO>();
        public BuildingSO FindBuildingById(int buildingId) => null;
    }

    private sealed class FixedFacilityShopUnlockStateService :
        IFacilityShopUnlockStateService
    {
        private readonly FacilityShopUnlockState state =
            new FacilityShopUnlockState();

        public FacilityShopUnlockState GetUnlockState()
        {
            return state;
        }
    }

    private sealed class NeutralRunVariableReader : IRunVariableRuntimeReader
    {
        public int GetInitialShopSeed() => 0;
        public IReadOnlyList<int> GetStartingBlueprintCandidateIds() => Array.Empty<int>();
        public float GetGuestDemandMultiplier(string speciesTag) => 1f;
        public float GetStockCostMultiplier(StockCategory category) => 1f;
        public float GetFacilityShopCostMultiplier(BuildingSO building) => 1f;
        public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint) => 1f;
        public float GetThreatRiseMultiplier() => 1f;
        public float GetWarningThresholdMultiplier() => 1f;
        public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source) => source;
    }

    private sealed class SceneFacilityLookup : ICharacterAiFacilityLookup
    {
        public BuildableObject FindFacility(int id, string tag)
        {
            return UnityEngine.Object.FindObjectsByType<BuildableObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(building => CharacterAiDecisionPipeline.MatchesFacility(building, id, tag));
        }
    }

    private sealed class ActiveWorkGridResolver : IWorkGridResolver
    {
        public Grid ResolveActiveGrid(
            AbilityWork work,
            GridPathSearchResult searchResult,
            Grid priorityGrid = null)
        {
            return priorityGrid
                ?? searchResult?.sourceGrid
                ?? UnityEngine.Object.FindFirstObjectByType<GridSystemManager>()?.grid;
        }

        public Vector2Int GetGridPosition(Grid activeGrid, CharacterActor actor)
        {
            return activeGrid != null && actor != null
                ? activeGrid.GetXY(actor.transform.position)
                : Vector2Int.zero;
        }
    }

    private sealed class NoopStaffDiscontentService : IStaffDiscontentRuntimeService
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

    private sealed class EditorStaffDiscontentRuntimeService : IStaffDiscontentRuntimeService
    {
        private readonly StaffDiscontentRuntime runtime;

        public EditorStaffDiscontentRuntimeService(StaffDiscontentRuntime runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public float GetWorkEfficiencyMultiplier(CharacterActor staff) =>
            runtime.GetWorkEfficiencyMultiplier(staff);

        public bool ShouldBlockWork(CharacterActor staff, out string reason) =>
            runtime.ShouldBlockWork(staff, out reason);

        public bool IsRebellionTarget(CharacterActor target) =>
            runtime.IsRebellionTarget(target);

        public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender) =>
            runtime.ResolveSuppressedRebel(rebel, defender);
    }

    private sealed class NoopOwnerRunLifecycleService : IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor owner, string reason) { }
    }

    private sealed class DefaultMetaProgressionReader : IMetaProgressionRuntimeReader
    {
        public int GetStartingFacilityCandidateBonus() => 0;
        public int GetStartingOwnerTraitCandidateBonus() => 0;
        public float GetOwnerMaxHealthMultiplier() => 1f;
        public float GetInvasionWarningThresholdMultiplier() => 1f;
        public float GetCommerceStockCostMultiplier(StockCategory category) => 1f;
        public float GetFortressFacilityCostMultiplier(BuildingSO building) => 1f;
        public float GetArcaneResearchWorkMultiplier() => 1f;
        public bool IsRecipePreserved(string recipeId) => false;

        public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(
            IEnumerable<BuildingSO> buildings)
        {
            return Array.Empty<int>();
        }
    }

    private sealed class NoopFloatingIconFeedbackService : IFloatingIconFeedbackService
    {
        public bool Show(Component target, Sprite sprite, float maxWorldSize) => false;
    }

    private sealed class SceneBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility)
        {
            return TryGetRuntime(preferActive: true, out BlueprintResearchRuntime runtime)
                && runtime.HasActiveResearch
                && facility != null
                && facility.SupportsWork(BuiltInWorkTypeIds.Research);
        }

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds)
        {
            if (!TryGetRuntime(preferActive: true, out BlueprintResearchRuntime runtime))
            {
                return new BlueprintResearchWorkResult(
                    false,
                    null,
                    0f,
                    0f,
                    1f,
                    false,
                    "Editor test fixture has no blueprint research runtime.");
            }

            return runtime.ApplyResearchWork(researcher, researchFacility, seconds);
        }

        private static bool TryGetRuntime(bool preferActive, out BlueprintResearchRuntime runtime)
        {
            BlueprintResearchRuntime[] runtimes = UnityEngine.Object.FindObjectsByType<BlueprintResearchRuntime>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            runtime = preferActive
                ? runtimes.FirstOrDefault((candidate) => candidate != null && candidate.HasActiveResearch)
                    ?? runtimes.FirstOrDefault((candidate) => candidate != null)
                : runtimes.FirstOrDefault((candidate) => candidate != null);
            return runtime != null;
        }
    }

    private sealed class NoopWorldInfoClickSelector : IWorldInfoClickSelector
    {
        public bool TryHandleWorldInfoClick() => false;
        public bool TryTriggerCharacterUnderPointer() => false;

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

    private sealed class AssetDatabaseShopStockCatalog : IShopStockCatalog
    {
        public bool TryGetStockInfoForShop(int shopId, out StockInfo stockInfo)
        {
            stockInfo = AssetDatabase.FindAssets("t:StockInfo", new[] { "Assets/Resources/SO/Stock" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<StockInfo>)
                .FirstOrDefault(candidate => candidate != null && candidate.shopId == shopId);
            return stockInfo != null;
        }

        public bool TryGetSaleItem(int saleItemId, out SaleItem saleItem)
        {
            saleItem = LoadSaleItems().FirstOrDefault(candidate => candidate.id == saleItemId);
            return saleItem != null;
        }

        public StockCategory GetStockCategory(int saleItemId)
        {
            return TryGetSaleItem(saleItemId, out SaleItem saleItem)
                ? saleItem.category
                : StockCategory.General;
        }

        private static IEnumerable<SaleItem> LoadSaleItems()
        {
            return AssetDatabase.FindAssets("t:SaleItem", new[] { "Assets/Resources/SO/Stock" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SaleItem>)
                .Where(item => item != null);
        }
    }

    private sealed class FixedGameDataProvider : IGameDataProvider
    {
        private readonly GameData value;

        public FixedGameDataProvider(GameData value)
        {
            this.value = value;
        }

        public bool TryGetGameData(out GameData resolvedGameData)
        {
            resolvedGameData = value;
            return resolvedGameData != null;
        }
    }

    private sealed class NoopFloatingNumberFeedbackService : IFloatingNumberFeedbackService
    {
        public bool TryShow(NumberCondition condition, Vector3 worldPosition, float value) => false;
    }

    private sealed class NoopWorkforceReplanService : IWorkforceReplanService
    {
        public void RequestIdleWorkersToReplan(bool clearFailures = true) { }
        public void RequestOneWorkerToReplanFor(
            WorkTypeId workTypeId,
            bool clearFailures = true,
            bool forceInterrupt = false) { }
        public void RequestOneHaulerToReplan(
            bool clearFailures = true,
            bool forceInterrupt = false) { }
    }
}

internal sealed class EditorCharacterAiPerformanceRecorder : ICharacterAiPerformanceRecorder
{
    private static readonly string[] Names =
    {
        "Scheduler",
        "BT",
        "DecisionContext",
        "DomainSelection",
        "ActionScoring",
        "WorldSignal",
        "FacilityScoring",
        "WorkTargetSelector",
        "Haul",
        "Wildlife",
        "Grid.SearchPath",
        "UI Feedback",
        "WorldSignal.SpatialIndex",
        "WorldSignal.Proximity",
        "WorldSignal.Environment",
        "Action.Prepare",
        "Action.Considerations",
        "Action.CanStart",
        "Action.ResolveDestination",
        "Facility.CandidateSource",
        "Facility.CandidateLoop",
        "DecisionContext.Needs",
        "DecisionContext.Abilities",
        "DecisionContext.WorldSignal",
        "Facility.Availability"
    };

    private readonly List<double>[] samples;
    private int searches;
    private int cacheHits;
    private int deferrals;
    private bool detailedCollectionEnabled = true;

    public EditorCharacterAiPerformanceRecorder()
    {
        samples = new List<double>[Names.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = new List<double>(512);
        }
    }

    public bool DetailedCollectionEnabled => detailedCollectionEnabled;

    public void SetDetailedCollectionEnabled(bool enabled)
    {
        detailedCollectionEnabled = enabled;
    }

    public void Record(AiPerformanceCategory category, double elapsedMilliseconds, long gcBytes = 0)
    {
        if (!detailedCollectionEnabled
            && category != AiPerformanceCategory.Scheduler)
        {
            return;
        }

        int index = (int)category;
        if (index >= 0 && index < samples.Length)
        {
            samples[index].Add(Math.Max(0d, elapsedMilliseconds));
        }
    }

    public void RecordPathCounters(int pathSearches, int pathCacheHits, int budgetDeferrals)
    {
        searches += Math.Max(0, pathSearches);
        cacheHits += Math.Max(0, pathCacheHits);
        deferrals += Math.Max(0, budgetDeferrals);
    }

    public CharacterAiPerformanceReport CaptureReport(int actorCount)
    {
        CharacterAiPerformanceReport report = new CharacterAiPerformanceReport
        {
            actorCount = Math.Max(0, actorCount),
            brokerSearches = searches,
            brokerCacheHits = cacheHits,
            brokerBudgetDeferrals = deferrals,
            valid = true
        };

        for (int i = 0; i < samples.Length; i++)
        {
            CharacterAiPerformanceMetric metric = CaptureMetric(Names[i], samples[i]);
            report.metrics.Add(metric);
            report.sampleFrames = Math.Max(report.sampleFrames, samples[i].Count);
        }

        report.scheduler = report.metrics[(int)AiPerformanceCategory.Scheduler];
        report.behaviorTree = report.metrics[(int)AiPerformanceCategory.BehaviorTree];
        report.pathBroker = report.metrics[(int)AiPerformanceCategory.PathSearch];
        return report;
    }

    public void Reset()
    {
        foreach (List<double> sample in samples)
        {
            sample.Clear();
        }

        searches = 0;
        cacheHits = 0;
        deferrals = 0;
    }

    private static CharacterAiPerformanceMetric CaptureMetric(string name, List<double> source)
    {
        CharacterAiPerformanceMetric metric = new CharacterAiPerformanceMetric(name);
        if (source.Count == 0)
        {
            return metric;
        }

        double[] sorted = source.ToArray();
        Array.Sort(sorted);
        metric.average = sorted.Average();
        metric.p95 = sorted[Mathf.Clamp(
            Mathf.CeilToInt(sorted.Length * 0.95f) - 1,
            0,
            sorted.Length - 1)];
        metric.max = sorted[sorted.Length - 1];
        metric.sampleCount = source.Count;
        return metric;
    }
}
