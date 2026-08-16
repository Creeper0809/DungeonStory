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
    private static readonly IGameContentCatalog GameContent =
        new ResourceGameContentCatalog(new UnityGameContentRootLoader());
    private static readonly ICharacterSpeciesCatalog CharacterSpecies =
        new ResourceCharacterSpeciesCatalog(GameContent);
    private static readonly EditorNoCharacterMedicalAccess CharacterMedical = new();
    private static readonly ICharacterSkillSystemSettingsProvider SkillSettings =
        new ResourceCharacterSkillSystemSettingsProvider(GameContent);
    internal static readonly AuthoredGameplayCatalog AuthoredGameplay =
        new AuthoredGameplayCatalog(GameContent);
    internal static IGameContentDefinitionSource ContentDefinitions => GameContent;
    internal static ICharacterSpeciesCatalog CharacterSpeciesCatalog => CharacterSpecies;
    internal static ICharacterBehaviorTreeRuntimeConfigurator TestBehaviorTreeConfigurator =>
        BehaviorTreeConfigurator;
    internal static IMainCameraProvider TestMainCameraProvider => MainCamera;
    internal static ICharacterAiPerformanceRecorder TestPerformanceRecorder =>
        PerformanceRecorder;
    private static readonly ICharacterSkillGenerationService SkillGeneration =
        new EditorCharacterSkillGenerationService();
    private static readonly ICharacterAiSchedulingService Scheduling =
        new ImmediateSchedulingService();
    private static readonly EditorCharacterAiPerformanceRecorder PerformanceRecorder =
        new EditorCharacterAiPerformanceRecorder();
    private static readonly IGridPathSearchBroker PathSearchBroker =
        new GridPathSearchBroker(
            new UnityGameClock(),
            performanceRecorder: PerformanceRecorder, doorAccessQuery: null, costPolicy: null);
    internal static readonly IGameClock GameClock = new UnityGameClock();
    internal static readonly IUiClock UiClock = new UnityUiClock();
    private static readonly IDynamicFrameWorkBudget FrameWorkBudget =
        new DynamicFrameWorkBudget(GameClock, UiClock);
    internal static readonly IGameEventBus GameEvents = new GameEventBus();
    private static readonly IRandomStreamProvider RandomStreams =
        new RandomStreamProvider(rootSeed: 9173);
    private static readonly IPersistentIdGenerator PersistentIds =
        new GuidPersistentIdGenerator();
    private static readonly DungeonRuntimeAggregateRootStore ItemRuntimeState =
        new DungeonRuntimeAggregateRootStore();
    private static readonly IDungeonItemCatalogProvider ItemCatalog =
        new ResourceDungeonItemCatalogProvider(
            new ResourceItemDefinitionCatalog(GameContent));
    private static readonly IItemHaulingSettingsProvider HaulingSettings =
        new ResourceItemHaulingSettingsProvider(
            GameContent,
            new EditorDungeonUserSettingsService(),
            ItemRuntimeState);
    private static readonly CharacterCarryInventoryRegistry CarryInventories = new();
    private static readonly WorldItemRepository PhysicalItems =
        new WorldItemRepository(
            PersistentIds,
            ItemRuntimeState);
    private static readonly IStockQuery StockQuery =
        new PhysicalStockQuery(
            PhysicalItems,
            new ResourceDungeonItemCatalogProvider(
                new ResourceItemDefinitionCatalog(GameContent)));
    internal static readonly ICharacterAiWorldRegistry WorldRegistry =
        new CharacterAiWorldRegistry(
            new SceneRuntimeRegistry<CharacterActor>(),
            new SceneRuntimeRegistry<WildlifeActor>(),
            new SceneRuntimeRegistry<BuildableObject>(),
            new SceneRuntimeRegistry<IWarehouseFacility>(),
            new SceneRuntimeRegistry<IRetailFacility>(),
            new FixedGameDataProvider(GetGameData()),
            new RestoreWorldCandidateIndex(),
            new EditorNoCharacterLifePublicationService());
    internal static readonly IGameCalendar GameCalendar =
        new GameCalendarRuntime(
            new FixedGameDataProvider(GetGameData()),
            GameEvents,
            GameClock);
    private static readonly ICharacterAiWorldSignalQuery WorldSignalQuery =
        new DefaultCharacterAiWorldSignalQuery(
            WorldRegistry,
            GameClock,
            performanceRecorder: PerformanceRecorder, survivalFoodRuntime: null, survivalEnvironment: null);
    private static readonly Lazy<CharacterBodyHealthRuntime> BodyHealth =
        new Lazy<CharacterBodyHealthRuntime>(() =>
            new CharacterBodyHealthRuntime(
                WorldRegistry,
                GameClock,
                GameEvents,
                FrameWorkBudget,
                new ResourceAnatomyProfileCatalog(GameContent),
                new DungeonRuntimeAggregateRootStore()));
    private static readonly ICharacterSpeciesCommand SpeciesCommands =
        new EditorNoCharacterSpeciesCommand();
    private static readonly IFacilityCandidateCache FacilityCandidates =
        new FacilityCandidateCacheStore(WorldRegistry, frameWorkBudget: null);
    private static readonly IRoomFacilityPolicy RoomPolicy =
        new RoomFacilityPolicyService(RoomRegistry.EditorCache);
    private static readonly IStaffDiscontentRuntimeService StaffDiscontent =
        new NoopStaffDiscontentService();
    private static readonly IMetaProgressionRuntimeReader MetaProgression =
        new DefaultMetaProgressionReader();
    private static readonly IFloatingIconFeedbackService FloatingIcons =
        new NoopFloatingIconFeedbackService();
    private static readonly IBlueprintResearchWorkService BlueprintResearch =
        new EditorNoBlueprintResearchWorkService();
    private static readonly IWorkOrderRuntime WorkOrders =
        new EditorNoWorkOrderRuntime();
    private static readonly IWorkAmountCalculator WorkAmounts =
        new EditorFixedWorkAmountCalculator();
    private static readonly ICareerService Careers =
        new CareerRuntime(new DungeonRuntimeAggregateRootStore());
    private static readonly IRoomEnvironmentExperienceService RoomExperience =
        new EditorNoRoomEnvironmentExperienceService();
    private static readonly IPaidFacilityContractRuntime PaidFacilities =
        new EditorNoPaidFacilityContractRuntime();
    private static readonly IEmploymentContractRuntime EmploymentContracts =
        new EditorNoEmploymentContractRuntime();
    private static readonly IEnvironmentWorkPolicy EnvironmentWork =
        new EditorSafeEnvironmentWorkPolicy();
    private static readonly IWorldInfoClickSelector WorldInfo =
        new NoopWorldInfoClickSelector();
    private static readonly IBuildingAbilityRuntimeDispatcher BuildingAbilities =
        CreateBuildingAbilityRuntimeDispatcher();
    internal static IBuildingAbilityRuntimeDispatcher BuildingAbilityRuntimeDispatcher =>
        BuildingAbilities;

    private static IBuildingAbilityRuntimeDispatcher CreateBuildingAbilityRuntimeDispatcher()
    {
        ModularBuildingCoreFacilityEffectsAdapter effects = new();
        return new BuildingAbilityRuntimeDispatcher(
            new IBuildingAbilityWorkCompletedHandler[]
            {
                new EditorSurvivalBuildingAbilityHandler(),
                new ProductionBuildingAbilityHandler(
                    new EditorFacilityEvolutionModifierQuery(),
                    new DungeonStory.Buildings.ProductionBuildingAbilityHandler(effects)),
                new CleaningBuildingAbilityHandler(
                    new DungeonStory.Buildings.CleaningBuildingAbilityHandler(effects)),
                new SecurityBuildingAbilityHandler(
                    new DungeonStory.Buildings.SecurityBuildingAbilityHandler(effects)),
                new ReceptionBuildingAbilityHandler(
                    new DungeonStory.Buildings.ReceptionBuildingAbilityHandler()),
                new PatrolPostBuildingAbilityHandler(
                    new DungeonStory.Buildings.PatrolPostBuildingAbilityHandler()),
                new OutdoorRestBuildingAbilityHandler(
                    new DungeonStory.Buildings.OutdoorRestBuildingAbilityHandler()),
                new ExteriorMaintenanceBuildingAbilityHandler(
                    new DungeonStory.Buildings.ExteriorMaintenanceBuildingAbilityHandler())
            },
            Array.Empty<IBuildingWorkCompletionFallbackHandler>());
    }

    private sealed class EditorFacilityEvolutionModifierQuery :
        IFacilityEvolutionModifierQuery
    {
        public float GetMultiplier(BuildableObject facility, string statId) => 1f;
        public float GetAdditive(BuildableObject facility, string statId) => 0f;
        public float GetOutputMultiplier(BuildableObject facility, WorkTypeId workTypeId) => 1f;
        public float GetWorkSpeedMultiplier(BuildableObject facility, WorkTypeId workTypeId) => 1f;
    }

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
    private static GameSessionState gameData;

    public static CharacterSO RequireAuthoredCharacterDefinition(
        string speciesTag,
        CharacterRole role = CharacterRole.Regular)
    {
        CharacterSO definition = GameContent.GetAll<CharacterSO>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.species != null
                && candidate.role == role
                && string.Equals(
                    candidate.SpeciesTag,
                    speciesTag,
                    StringComparison.OrdinalIgnoreCase));
        return definition ?? throw new InvalidOperationException(
            $"No authored {role} character archetype exists for species '{speciesTag}'.");
    }

    public static CharacterSO CreateCharacterFixtureData(
        CharacterType type,
        string characterName,
        string speciesTag,
        CharacterRole role = CharacterRole.Regular)
    {
        CharacterSO fixture = UnityEngine.Object.Instantiate(
            RequireAuthoredCharacterDefinition(speciesTag, role));
        fixture.hideFlags = HideFlags.HideAndDontSave;
        fixture.characterType = type;
        fixture.characterName = characterName;
        fixture.role = role;
        return fixture;
    }

    public static void Inject(GameObject actorObject)
    {
        Inject(actorObject, Scheduling, StaffDiscontent, BlueprintResearch);
    }

    public static void Inject(
        GameObject actorObject,
        ICharacterAiSchedulingService scheduling)
    {
        Inject(actorObject, scheduling, StaffDiscontent, BlueprintResearch);
    }

    public static void Inject(
        GameObject actorObject,
        ICharacterAiSchedulingService scheduling,
        IGameClock gameClock,
        IGridPathSearchBroker pathSearchBroker,
        IDynamicFrameWorkBudget frameWorkBudget,
        IFacilityCandidateCache facilityCandidates = null)
    {
        Inject(
            actorObject,
            scheduling,
            StaffDiscontent,
            BlueprintResearch,
            gameClock,
            pathSearchBroker,
            frameWorkBudget,
            facilityCandidates);
    }

    public static void Inject(
        GameObject actorObject,
        BlueprintResearchRuntime blueprintResearchRuntime)
    {
        Inject(
            actorObject,
            Scheduling,
            StaffDiscontent,
            new EditorBlueprintResearchWorkService(blueprintResearchRuntime));
    }

    public static void Inject(
        GameObject actorObject,
        StaffDiscontentRuntime staffDiscontentRuntime)
    {
        Inject(
            actorObject,
            Scheduling,
            new EditorStaffDiscontentRuntimeService(staffDiscontentRuntime),
            BlueprintResearch);
    }

    private static void Inject(
        GameObject actorObject,
        ICharacterAiSchedulingService scheduling,
        IStaffDiscontentRuntimeService staffDiscontent,
        IBlueprintResearchWorkService blueprintResearch,
        IGameClock gameClock = null,
        IGridPathSearchBroker pathSearchBroker = null,
        IDynamicFrameWorkBudget frameWorkBudget = null,
        IFacilityCandidateCache facilityCandidates = null)
    {
        if (actorObject == null)
        {
            return;
        }

        scheduling ??= Scheduling;
        gameClock ??= GameClock;
        pathSearchBroker ??= PathSearchBroker;
        frameWorkBudget ??= FrameWorkBudget;
        facilityCandidates ??= FacilityCandidates;
        pathSearchBroker.BeginFrame(int.MaxValue, enforceBudget: false);
        WorkExecutionHandlerRegistry workRegistry =
            CreateWorkRegistry(blueprintResearch);
        EnsureCharacterProgression(actorObject);

        foreach (CharacterAbility ability in actorObject.GetComponents<CharacterAbility>())
        {
            ability.ConstructCharacterAbility(GridSystem);
        }

        AbilityMove abilityMove = actorObject.GetComponent<AbilityMove>();
        abilityMove?.ConstructAbilityMove(
            CharacterSpawner,
            scheduling,
            pathSearchBroker,
            RandomStreams,
            gameClock, defenseEngagementRuntime: null);
        abilityMove?.ConstructDoorAccessQuery(OpenDoorAccessQuery.Instance);

        actorObject.GetComponent<CharacterLifecycle>()?.ConstructCharacterLifecycle(GridSystem);

        InjectCharacterStats(
            actorObject.GetComponent<CharacterStats>(),
            staffDiscontent,
            MetaProgression,
            gameClock,
            AuthoredGameplay,
            DisabledDungeonDebugRuleQuery.Instance);

        actorObject.GetComponent<CustomerPersonaRuntime>()?.ConstructCustomerPersonaRuntime(
            LocalLlm);

        actorObject.GetComponent<CharacterDialogueRuntime>()?.ConstructCharacterDialogueRuntime(
            LocalLlm,
            scheduling,
            DialogueBubbles,
            gameClock, frameWorkBudget: frameWorkBudget);
        actorObject.GetComponent<CharacterVisual>()?.ConstructCharacterVisual(gameClock);

        AbilityWork abilityWork = actorObject.GetComponent<AbilityWork>();
        if (abilityWork != null)
        {
            abilityWork.ConstructAbilityWork(
                blueprintResearch,
                staffDiscontent,
                FloatingIcons,
                new ActiveWorkGridResolver(),
                facilityCandidates,
                null,
                workPolicyRegistry: workRegistry,
                gameClock: gameClock, exteriorZoneQuery: null, workExecutionHandlerRegistry: workRegistry, workOrderRuntime: WorkOrders, workAmountCalculator: WorkAmounts, captiveLaborQuery: null, defenseEngagementRuntime: null, roomEnvironmentExperienceService: RoomExperience, paidFacilityContracts: PaidFacilities, environmentWorkPolicy: EnvironmentWork, characterEnvironment: NoCharacterEnvironmentWorkContext.Instance, environmentalWorkwearCommands: NoEnvironmentalWorkwearCommand.Instance,
                needDefinitionCatalog: AuthoredGameplay,
                debugRules: DisabledDungeonDebugRuleQuery.Instance);
            abilityWork.ConstructPerformance(
                NeutralPerformance,
                new CharacterWorkPerformanceContextResolver(
                    NeutralProficiencies,
                    GameCalendar,
                    combatEquipment: null),
                BodyHealth.Value,
                SpeciesCommands);
            abilityWork.ConstructProficiencyProgression(
                NeutralProficiencies,
                NeutralProficiencies,
                GameCalendar,
                combatEquipmentRuntime: null);
        }

        actorObject.GetComponent<AbilityShopping>()?.ConstructAbilityShopping(
            ShopStock,
            FloatingIcons,
            RandomStreams,
            gameClock,
            GameEvents);

        actorObject.GetComponent<AIBrain>()?.ConstructAIBrain(
            new AIBrainDecisionServices(
                new ResourceCharacterAiActionAssetCatalog(GameContent),
                scheduling,
                facilityCandidates,
                new SceneFacilityLookup(),
                new CharacterAiJobGiverCatalog(),
                new CharacterAiDecisionPipeline(
                    NoCharacterDeprivationBoundary.Instance,
                    NoCharacterDeprivationBoundary.Instance),
                PerformanceRecorder),
            new AIBrainExecutionServices(
                pathSearchBroker,
                gameClock,
                RandomStreams,
                new NeutralSocialReputationBiasService(),
                RoomPolicy));

        actorObject.GetComponent<CharacterActor>()?.ConstructCharacterActor(
            GridSystem,
            scheduling,
            WorldInfo,
            SocialMemoryFactory,
            FeedbackBubbles,
            MainCamera,
            pathSearchBroker,
            WorldRegistry,
            WorldSignalQuery,
            frameWorkBudget,
            CarryInventories,
            new CharacterIdRegistryAdapter(
                WorldRegistry,
                new DungeonStory.Characters.CharacterIdRegistry(
                    new GuidPersistentIdGenerator())),
            GameContent,
            new EditorDungeonUserSettingsService(),
            null,
            null,
            CharacterMedical,
            CharacterMedical,
            null,
            null,
            null,
            gameClock,
            WorkAmounts,
            tmpKoreanFontService: null,
            presentationScheduler: null,
            runtimeProfileFactory: new CharacterRuntimeProfileFactory(GameContent),
            moodPolicy: new CharacterMoodPolicyService(
                new CharacterIdentityRuleRouter(),
                new CharacterPersistentNeedRuntime(
                    new CharacterIdentityStateStore(),
                    gameClock)));

        // Editor scenarios construct actors without a world item runtime. Carry
        // inventory is still part of the AI decision snapshot, so the fixture must
        // provide the same typed catalog/settings authority as a live world.
        actorObject.GetComponent<CharacterCarryInventory>()?.Configure(
            ItemCatalog,
            HaulingSettings,
            CarryInventories);

    }

    /// <summary>
    /// Editor AI fixtures have no authored door-policy runtime. Movement still
    /// has to traverse the same guard as the live game, so provide an explicit
    /// open policy instead of leaving the guard unconstructed and crashing on
    /// the first idle-wander step.
    /// </summary>
    private sealed class OpenDoorAccessQuery : IDoorAccessQuery
    {
        public static readonly OpenDoorAccessQuery Instance = new();

        public int DoorAccessVersion => 0;

        public DoorAccessSubjectRef ResolveSubject(GridTraversalContext context) => default;

        public bool CanUse(
            Door door,
            GridTraversalContext context,
            out string denialReason)
        {
            denialReason = string.Empty;
            return true;
        }

        public bool CanTraverse(
            Grid grid,
            Vector2Int position,
            GridTraversalContext context,
            out string denialReason)
        {
            denialReason = string.Empty;
            return true;
        }
    }

    private static WorkExecutionHandlerRegistry CreateWorkRegistry(
        IBlueprintResearchWorkService blueprintResearch)
    {
        return new WorkExecutionHandlerRegistry(
            Array.Empty<IWorkExecutionHandler>(),
            new IWorkCandidateProvider[]
            {
                new ResearchWorkExecutionHandler(
                    blueprintResearch
                    ?? throw new ArgumentNullException(nameof(blueprintResearch))),
                new EditorRepairCandidateProvider()
            },
            Array.Empty<IWorkUrgencyProvider>(),
            Careers,
            GameCalendar);
    }

    private sealed class EditorNoCharacterLifePublicationService :
        ICharacterLifePublicationService
    {
        public void EnsureRegistered(CharacterActor actor)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }
        }

        public void EnsureRegistered(
            CharacterId characterId,
            CharacterSpeciesId phenotypeSpeciesId)
        {
            if (!characterId.IsValid || !phenotypeSpeciesId.IsValid)
            {
                throw new ArgumentException(
                    "Editor character life publication requires valid IDs.");
            }
        }
    }

    internal static void EnsureCharacterProgression(GameObject actorObject)
    {
        if (actorObject == null)
        {
            return;
        }

        CharacterProgression progression =
            actorObject.GetComponent<CharacterProgression>();
        if (progression == null)
        {
            progression = actorObject.AddComponent<CharacterProgression>();
        }

        progression.ConstructCharacterProgression(
            SkillGeneration,
            SkillSettings,
            GameEvents,
            new CharacterProgressionProfileProjector(
                GameContent,
                new CharacterRuntimeProfileFactory(GameContent)));
        progression.GrowthState.traitSelectionAuthorityVersion =
            CharacterGrowthState.CurrentTraitSelectionAuthorityVersion;
        progression.GrowthState.traitSelectionAuthorityOrigin =
            CharacterTraitSelectionAuthorityOrigin.PreparedSelection;
        progression.GrowthState.traitIds ??= new List<int>();
    }

    internal static void InjectCharacterStats(
        CharacterStats stats,
        IStaffDiscontentRuntimeService staffDiscontent,
        IMetaProgressionRuntimeReader metaProgression,
        IGameClock gameClock,
        ICharacterNeedDefinitionCatalog needDefinitions,
        IDungeonDebugRuleQuery debugRules)
    {
        if (stats == null)
        {
            return;
        }

        CharacterStatsProjectionService projection =
            new CharacterStatsProjectionService(
                staffDiscontent,
                metaProgression,
                NoCharacterDeprivationBoundary.Instance,
                NeutralCharacterSubstanceRuntime.Instance,
                NeutralCharacterEnvironmentStatusQuery.Instance,
                NeutralExternalCombatInfluenceQuery.Instance,
                NeutralContentWorkDelayQuery.Instance,
                NeutralDiseaseSymptomEffectQuery.Instance,
                NeutralCharacterCombatSpecialStatusQuery.Instance,
                NeutralCombatEquipmentBurdenQuery.Instance,
                GameCalendar,
                sharedEffects: new CharacterDerivedStatsSnapshotProjector(
                    GameContent,
                    EditorNoEquipmentGameplayEffects.Instance,
                    EditorNoTransientGameplayEffects.Instance,
                    new ExtremeTraitRuntime(new CharacterIdentityStateStore()),
                    gameClock),
                performance: NeutralPerformance);
        stats.ConstructCharacterStats(
            gameClock,
            needDefinitions,
            debugRules,
            projection,
            new CharacterNeedStateService(
                DefaultCharacterNeedBalanceRuntime.Instance,
                debugRules),
            new CharacterMoodStateService(gameClock, needDefinitions),
            new CharacterStatsMaintenanceSchedule(),
            GameEvents,
            NeutralPerformance,
            new CharacterWorkPerformanceContextResolver(
                NeutralProficiencies,
                GameCalendar,
                combatEquipment: null),
            combatEquipment: null);
        stats.ConstructCharacterVitals(
            new CharacterStatsVitalsService(
                BodyHealth.Value,
                BodyHealth.Value,
                GameEvents,
                new CharacterDeathEventFactory(WorldRegistry, GameCalendar),
                new NoopOwnerRunLifecycleService()));
    }

    public static ICharacterPerformanceQuery NeutralPerformance { get; } =
        new EditorNeutralCharacterPerformanceQuery();

    private static EditorNeutralCharacterProficiencyQuery NeutralProficiencies { get; } =
        new EditorNeutralCharacterProficiencyQuery();

    private sealed class EditorNeutralCharacterProficiencyQuery :
        ICharacterProficiencyQuery,
        ICharacterProficiencyCommand
    {
        public bool TryGetProficiency(
            CharacterId characterId,
            CharacterProficiencyId proficiencyId,
            long absoluteHour,
            out CharacterProficiencySnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public IReadOnlyList<CharacterProficiencySnapshot> GetAllProficiencies(
            CharacterId characterId,
            long absoluteHour) => Array.Empty<CharacterProficiencySnapshot>();

        public long AddApprovedWork(
            CharacterId characterId,
            ProficiencyWorkProfile profile,
            float approvedWork,
            float difficultyMultiplier,
            ProficiencyWorkOutcome outcome,
            float learningMultiplier,
            float repetitionMultiplier,
            long absoluteHour) => 0L;

        public long AddDirectExperience(
            CharacterId characterId,
            CharacterProficiencyId proficiencyId,
            float experience,
            long absoluteHour,
            bool applyLearningMultiplier = true) => 0L;

        public long AddCombatExperience(
            CharacterId characterId,
            CharacterProficiencyId proficiencyId,
            float experience,
            bool training,
            string stableAwardKey,
            long absoluteHour) => 0L;

        public void RecordPractice(
            CharacterId characterId,
            CharacterProficiencyId proficiencyId,
            long absoluteHour)
        {
        }
    }

    private sealed class EditorNoCharacterSpeciesCommand :
        ICharacterSpeciesCommand
    {
        public bool RepairIntegrity(
            CharacterId characterId,
            float amount,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return characterId.IsValid && amount >= 0f;
        }

        public bool RecordCompletedWork(
            CharacterId characterId,
            string workTypeId,
            float completedWork,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return characterId.IsValid
                && !string.IsNullOrWhiteSpace(workTypeId)
                && completedWork >= 0f;
        }
    }

    private sealed class EditorNeutralCharacterPerformanceQuery :
        ICharacterPerformanceQuery
    {
        public CharacterFunctionalCapacitySnapshot GetFunctionalCapacities(
            CharacterActor actor) => new(
            Enum.GetValues(typeof(CharacterFunctionalCapacityId))
                .Cast<CharacterFunctionalCapacityId>()
                .Select(id => new CharacterFunctionalCapacityValue(
                    id,
                    true,
                    1f,
                    string.Empty,
                    Array.Empty<CharacterPerformanceContributionTrace>()))
                .ToArray());

        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            float contextFactor = 1f,
            GameplayEffectContext effectContext = null) => Neutral(
            formulaId,
            contextFactor);

        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            CharacterPerformanceEvaluationContext context) => Neutral(
            formulaId,
            context?.ContextFactor ?? 1f);

        public CharacterPerformanceSnapshot EvaluateWork(
            CharacterActor actor,
            WorkTypeId workTypeId,
            CharacterPerformanceResultChannel resultChannel,
            CharacterPerformanceEvaluationContext context) => Neutral(
            $"{workTypeId.Value}:{resultChannel}",
            context?.ContextFactor ?? 1f);

        public IReadOnlyList<CharacterPerformanceSnapshot> EvaluateDomain(
            CharacterActor actor,
            CharacterPerformanceFormulaDomain domain) =>
            Array.Empty<CharacterPerformanceSnapshot>();

        private static CharacterPerformanceSnapshot Neutral(
            string formulaId,
            float contextFactor) => new()
            {
                FormulaId = formulaId?.Trim() ?? string.Empty,
                DisplayName = formulaId?.Trim() ?? string.Empty,
                BaseValue = 1f,
                FunctionalCapacityFactor = 1f,
                ProficiencyFactor = 1f,
                GameplayEffectFactor = 1f,
                ContextFactor = contextFactor,
                WeightedCapacityValue = 1f,
                BottleneckCap = 1f,
                Value = contextFactor,
                IsApplicable = true,
                Contributions = Array.Empty<CharacterPerformanceContributionTrace>()
            };
    }

    private sealed class EditorNoEquipmentGameplayEffects :
        ICharacterEquipmentGameplayEffectSourceQuery
    {
        public static readonly EditorNoEquipmentGameplayEffects Instance = new();
        public IReadOnlyList<IGameplayEffectSource> GetEquipmentSources(
            CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
    }

    private sealed class EditorNoTransientGameplayEffects :
        ICharacterTransientGameplayEffectSourceQuery
    {
        public static readonly EditorNoTransientGameplayEffects Instance = new();
        public IReadOnlyList<IGameplayEffectSource> GetStatusSources(
            CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
        public IReadOnlyList<IGameplayEffectSource> GetCompletedResearchSources(
            CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
    }

    public static void Inject(SocialReputationRuntime runtime)
    {
        runtime?.ConstructSocialReputationRuntime(
            LocalLlm,
            WorldRegistry,
            WorldRegistry,
            SocialMemoryFactory,
            GameClock,
            RandomStreams, uiClock: null);
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
            GameClock, uiClock: null,
            needDefinitionCatalog: AuthoredGameplay);
    }

    public static void Inject(CharacterAiScheduler scheduler)
    {
        Inject(scheduler, UiClock);
    }

    public static void Inject(
        CharacterAiScheduler scheduler,
        IUiClock uiClock)
    {
        Inject(scheduler, GameClock, uiClock);
    }

    public static void Inject(
        CharacterAiScheduler scheduler,
        IGameClock gameClock,
        IUiClock uiClock)
    {
        gameClock ??= GameClock;
        uiClock ??= UiClock;
        IGridPathSearchBroker pathSearchBroker = ReferenceEquals(gameClock, GameClock)
            ? PathSearchBroker
            : new GridPathSearchBroker(
                gameClock,
                doorAccessQuery: null,
                performanceRecorder: PerformanceRecorder,
                costPolicy: null);
        scheduler?.Construct(
            WorldRegistry,
            MainCamera,
            BehaviorTreeConfigurator,
            pathSearchBroker,
            gameClock,
            ReferenceEquals(gameClock, GameClock)
                && ReferenceEquals(uiClock, UiClock)
                ? FrameWorkBudget
                : new DynamicFrameWorkBudget(gameClock, uiClock),
            PerformanceRecorder,
            uiClock,
            FacilityCandidates,
            playerStaffCommands: null,
            debugRules: DisabledDungeonDebugRuleQuery.Instance);
    }

    internal static void ResetPerformanceRecorder(
        bool detailedCollectionEnabled = true,
        bool slowTraceEnabled = false)
    {
        PerformanceRecorder.SetDetailedCollectionEnabled(
            detailedCollectionEnabled);
        PerformanceRecorder.SetSlowTraceEnabled(slowTraceEnabled);
        PerformanceRecorder.Reset();
    }

    internal static CharacterAiPerformanceReport CapturePerformanceReport(int actorCount)
    {
        return PerformanceRecorder.CaptureReport(actorCount);
    }

    internal static IDisposable OverrideGridSystemForScenario(
        GridSystemManager manager)
    {
        return ((EditorGridSystemProvider)GridSystem).PushOverride(manager);
    }

    internal static void FlushSlowPerformanceTrace()
    {
        PerformanceRecorder.FlushSlowTrace();
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
            GameEvents,
            EmploymentContracts,
            new EditorGameMoneyAccount(GetGameData()),
            PaidFacilities,
            stockCategoryCatalog: AuthoredGameplay,
            buildingCategoryCatalog: AuthoredGameplay,
            aggregateRootStore: new DungeonRuntimeAggregateRootStore());
    }

    public static void Inject(StaffDiscontentRuntime runtime)
    {
        runtime?.Construct(
            WorldRegistry,
            GameEvents,
            new DungeonRuntimeAggregateRootStore());
    }

    public static void Inject(BlueprintResearchRuntime runtime)
    {
        runtime?.Construct(
            new FixedFacilityShopUnlockStateService(),
            new EmptyFacilityShopCatalog(),
            FacilityCandidates,
            new NoopWorkforceReplanService(),
            GameEvents,
            itemStackRuntime: null,
            projectCoordinator: new BlueprintResearchProjectCoordinator(
                new ResourceResearchProjectCatalog(
                    Array.Empty<ResearchProjectSO>()),
                UnavailableResearchBlueprintArchiveQuery.Instance,
                UnrestrictedResearchFacilityCapacityQuery.Instance),
            worldDropZoneQuery: null,
            aggregateRootStore: new DungeonRuntimeAggregateRootStore(),
            debugRules: DisabledDungeonDebugRuleQuery.Instance,
            uiClock: UiClock);
    }

    public static void Inject(
        StaffDiscontentRuntime runtime,
        IEnumerable<GameObject> scenarioRoots)
    {
        runtime?.Construct(
            WorldRegistry,
            GameEvents,
            new DungeonRuntimeAggregateRootStore());
    }

    public static void Inject(BuildableObject building)
    {
        Inject(building, BlueprintResearch, RoomPolicy);
    }

    public static void InjectWithRoomPolicy(
        BuildableObject building,
        IBuildingRoomPolicyPort roomPolicy)
    {
        Inject(
            building,
            BlueprintResearch,
            roomPolicy ?? throw new ArgumentNullException(nameof(roomPolicy)));
    }

    public static void Inject(
        BuildableObject building,
        BlueprintResearchRuntime blueprintResearchRuntime)
    {
        Inject(
            building,
            new EditorBlueprintResearchWorkService(blueprintResearchRuntime),
            RoomPolicy);
    }

    private static void Inject(
        BuildableObject building,
        IBlueprintResearchWorkService blueprintResearch,
        IBuildingRoomPolicyPort roomPolicy)
    {
        building?.ConstructPersistentIdentity(PersistentIds);
        building?.ConstructBuildableObject(
            new BuildingResearchWorkPortAdapter(blueprintResearch),
            FacilityCandidates,
            roomPolicy,
            worldRegistry: (IBuildingWorldRegistryPort)WorldRegistry,
            abilityRuntimeDispatcher: BuildingAbilities,
            gameClock: GameClock, combatEquipmentRuntime: null, worldItemStackRuntime: null, paidFacilityContracts: null, evolutionState: new FacilityEvolutionStateComponentFactory());
        building?.ConstructBuildableObjectEventBus(
            GameEvents,
            new BuildingVisitEventPublisher(GameEvents),
            new BuildingInfoPresentationAdapter(GameEvents));
        building?.ConstructDebugRules(DisabledDungeonDebugRuleQuery.Instance);

        if (building is Facility facility)
        {
            facility.ConstructFacility(
                roomEnvironmentExperienceService: null,
                stockQuery: StockQuery,
                mealConsumptionRuntime: null,
                waterFixtureUseRuntime: null,
                wastewaterNetworkRuntime: PermissiveWastewaterTransaction.Instance,
                serviceSessionRuntime: null,
                serviceRoomLinkRuntime: null,
                stockCategoryCatalog: AuthoredGameplay);
        }

        if (building is DefenseFacility defenseFacility)
        {
            defenseFacility.ConstructDefenseFacilityEventBus(GameEvents, worldThreatModifiers: null, defenseRuntime: null);
        }
    }

    public static void InjectShop(Shop shop)
    {
        GameSessionState state = GetGameData();
        shop?.ConstructShop(
            new EditorGameMoneyAccount(state),
            ShopStock,
            new NoopFloatingNumberFeedbackService(),
            new NoopWorkforceReplanService(),
            FacilityCrimeEditorTestDependencies.Evaluator,
            RandomStreams,
            null,
            null,
            null);
    }

    private static GameSessionState GetGameData()
    {
        if (gameData != null)
        {
            return gameData;
        }

        gameData = new GameSessionState();
        gameData.gameSpeed.Initialize(1);
        gameData.holdingMoney.Initialize(100000);
        gameData.day.Initialize(7);
        gameData.curTime.Initialize(0f);
        gameData.hour.Initialize(0);
        gameData.timeOfDay.Initialize(TimeOfDay.Morning);
        return gameData;
    }

    private sealed class PermissiveWastewaterTransaction :
        IFluidWastewaterTransaction
    {
        public static readonly PermissiveWastewaterTransaction Instance = new();

        public bool TryAddWastewater(
            BuildableObject fixture,
            float amount,
            out float accepted,
            out DomainFailure failure)
        {
            accepted = Mathf.Max(0f, amount);
            failure = default;
            return true;
        }

        public bool TryConsumeWastewater(
            BuildableObject processor,
            float amount,
            out float consumed)
        {
            consumed = Mathf.Max(0f, amount);
            return true;
        }

        public bool CanAcceptWastewater(
            BuildableObject fixture,
            float amount,
            out DomainFailure failure)
        {
            failure = default;
            return true;
        }
    }

    private sealed class ImmediateSchedulingService : ICharacterAiSchedulingService
    {
        public bool IsSchedulerAvailable => true;
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
        private sealed class OverrideLease : IDisposable
        {
            private Action release;

            public OverrideLease(Action release)
            {
                this.release = release;
            }

            public void Dispose()
            {
                Action callback = release;
                release = null;
                callback?.Invoke();
            }
        }

        private GridSystemManager cachedManager;
        private GridSystemManager overrideManager;

        public IDisposable PushOverride(GridSystemManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            GridSystemManager previous = overrideManager;
            overrideManager = manager;
            cachedManager = manager;
            return new OverrideLease(() =>
            {
                overrideManager = previous;
                cachedManager = previous;
            });
        }

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
            // Large stress worlds bind their exact manager explicitly. Ordinary
            // editor scenarios keep resolving by priority because many of them
            // create and destroy different grids within the same editor frame.
            if (overrideManager != null && overrideManager.grid != null)
            {
                manager = overrideManager;
                return true;
            }

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
            if (!ownerObject.TryGetComponent(out AbilityShopping _))
            {
                ownerObject.AddComponent<AbilityShopping>();
            }
            if (!ownerObject.TryGetComponent(out AIBrain _))
            {
                ownerObject.AddComponent<AIBrain>();
            }

            ownerObject.transform.position = ownerSpawnPoint != null
                ? ownerSpawnPoint.position
                : Vector3.zero;
            Inject(ownerObject);
            InjectCharacterStats(
                ownerObject.GetComponent<CharacterStats>(),
                StaffDiscontent,
                MetaProgression,
                GameClock,
                AuthoredGameplay,
                DisabledDungeonDebugRuleQuery.Instance);
            owner.EnsureRuntimeState();
            owner.RefreshAbilityCache();
            owner.Initialization(ownerData);
            owner.SetLifecycleState(CharacterLifecycleState.Active);
            owner.Brain?.UseOwnerWorkActions();
            return owner;
        }

        public CharacterActor CreateOwnerDetached(
            CharacterSO ownerData,
            GameObject ownerPrefab)
        {
            throw new NotSupportedException(
                "The shared editor dependency fixture does not restore character worlds.");
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
        public DungeonSurvivalPressure GetSurvivalPressure() =>
            DungeonSurvivalPressure.Standard;
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

    private sealed class EditorNoBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility) => false;

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
                "Editor test fixture has no blueprint research runtime.");
        }

        public BlueprintResearchWorkResult ApplyApprovedResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float approvedWorkUnits) =>
            ApplyResearchWork(researcher, researchFacility, approvedWorkUnits);
    }

    private sealed class EditorBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        private readonly BlueprintResearchRuntime runtime;

        public EditorBlueprintResearchWorkService(BlueprintResearchRuntime runtime)
        {
            this.runtime = runtime
                ?? throw new ArgumentNullException(nameof(runtime));
        }

        public bool HasResearchWorkFor(BuildableObject facility) =>
            runtime.HasActiveResearch
            && facility != null
            && facility.SupportsWork(BuiltInWorkTypeIds.Research);

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds) =>
            runtime.ApplyResearchWork(researcher, researchFacility, seconds);

        public BlueprintResearchWorkResult ApplyApprovedResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float approvedWorkUnits) =>
            runtime.ApplyApprovedResearchWork(
                researcher,
                researchFacility,
                approvedWorkUnits);
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

    private sealed class FixedGameDataProvider : IGameSessionStateProvider
    {
        private readonly GameSessionState value;

        public FixedGameDataProvider(GameSessionState value)
        {
            this.value = value;
        }

        public bool TryGetSessionState(out GameSessionState resolvedGameData)
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

    private sealed class EditorNoCharacterMedicalAccess :
        ICharacterMedicalQuery,
        ICharacterMedicalCommand
    {
        public IReadOnlyList<CharacterMedicalOrder> ActiveOrders =>
            Array.Empty<CharacterMedicalOrder>();

        public bool HasAvailableRescueOrder(CharacterActor rescuer) => false;

        public bool TryGetOrder(
            string orderId,
            out CharacterMedicalOrder order)
        {
            order = null;
            return false;
        }

        public bool TryGetPatient(
            CharacterMedicalOrder order,
            out CharacterActor patient)
        {
            patient = null;
            return false;
        }

        public bool TryGetTreatmentFacility(
            CharacterMedicalOrder order,
            out BuildableObject facility)
        {
            facility = null;
            return false;
        }

        public bool TryReserveBestOrder(
            CharacterActor rescuer,
            out CharacterMedicalOrder order,
            out DomainFailure failure)
        {
            order = null;
            failure = Unavailable();
            return false;
        }

        public bool TryReserveOrderForPatient(
            CharacterActor rescuer,
            CharacterActor patient,
            out CharacterMedicalOrder order,
            out DomainFailure failure)
        {
            order = null;
            failure = Unavailable();
            return false;
        }

        public bool TryRequestTreatment(
            CharacterActor patient,
            out CharacterMedicalOrder order,
            out DomainFailure failure)
        {
            order = null;
            failure = Unavailable();
            return false;
        }

        public bool TryAssignSpecificTreatmentFacility(
            string orderId,
            BuildableObject facility,
            out DomainFailure failure)
        {
            failure = Unavailable();
            return false;
        }

        public float AdvanceStabilization(
            string orderId,
            CharacterActor rescuer,
            float work) => 0f;

        public bool TryBeginCarrying(
            string orderId,
            CharacterActor rescuer,
            out DomainFailure failure)
        {
            failure = Unavailable();
            return false;
        }

        public bool TryPlaceAtTreatmentDestination(
            string orderId,
            CharacterActor rescuer,
            out DomainFailure failure)
        {
            failure = Unavailable();
            return false;
        }

        public float AdvanceTreatment(
            string orderId,
            CharacterActor rescuer,
            float work) => 0f;

        public bool TryReleaseReservation(
            string orderId,
            CharacterActor rescuer,
            CharacterMedicalStatusCode releaseStatus,
            out DomainFailure failure)
        {
            failure = Unavailable();
            return false;
        }

        public void NotifyCharacterDowned(CharacterActor actor) { }
        public void NotifyCharacterRecovered(CharacterActor actor) { }

        private static DomainFailure Unavailable() => new(
            FailureCode.CharacterMedicalRuntimeUnavailable);
    }

    private sealed class EditorNoWorkOrderRuntime : IWorkOrderRuntime
    {
        public int WorkOrderCandidateVersion => 0;

        public DungeonWorkOrderSaveData Capture() => new DungeonWorkOrderSaveData();

        public void ValidateRestorePayload(
            DungeonWorkOrderSaveData snapshot)
        {
        }

        public WorkOrderRestoreCandidate PrepareRestoreCandidate(
            DungeonWorkOrderSaveData snapshot)
        {
            throw new InvalidOperationException(
                "Editor fixture does not restore work orders.");
        }

        public void PublishRestoreCandidate(WorkOrderRestoreCandidate candidate)
        {
        }

        public bool TryCreateConstructionOrder(
            ConstructionSite site,
            BuildingSO building,
            Vector2Int position,
            out string orderId,
            out string failureReason)
        {
            orderId = string.Empty;
            failureReason = "Editor fixture does not create construction orders.";
            return false;
        }

        public bool TryGetOrderFor(
            BuildableObject target,
            WorkTypeId workTypeId,
            out WorkOrderProgressState order)
        {
            order = null;
            return false;
        }

        public bool ApplyWork(
            CharacterActor worker,
            BuildableObject target,
            WorkTypeId workTypeId,
            float amount,
            out bool completed,
            out bool appliedCompletionEffects,
            out string message)
        {
            completed = false;
            appliedCompletionEffects = false;
            message = "Editor fixture has no persistent work order.";
            return false;
        }

        public bool RefreshMaterialsReady(ConstructionSite site) => false;
        public bool CancelOrder(string orderId, bool refundDeliveredMaterials) => false;

        public bool DebugCompleteOrder(string orderId, out string message)
        {
            message = "Editor fixture has no persistent work order.";
            return false;
        }

        public int DebugCompleteAllOrders() => 0;
    }

    private sealed class EditorFixedWorkAmountCalculator : IWorkAmountCalculator
    {
        public float CalculateWorkPerSecond(
            CharacterActor actor,
            BuildableObject target,
            WorkTypeId workTypeId,
            float environmentDurationMultiplier)
        {
            return Mathf.Max(0.01f, environmentDurationMultiplier);
        }
    }

    private sealed class EditorNoRoomEnvironmentExperienceService :
        IRoomEnvironmentExperienceService
    {
        public bool Apply(RoomEnvironmentExperienceEvent eventType) => false;
        public IReadOnlyList<string> GetActiveConditionIds(
            BuildableObject facility) => Array.Empty<string>();
    }

    private sealed class EditorSafeEnvironmentWorkPolicy : IEnvironmentWorkPolicy
    {
        private static WorkEnvironmentAssessment Safe => new WorkEnvironmentAssessment(
            canStart: true,
            needsProtection: false,
            projectedExposure: 0f,
            workSpeedMultiplier: 1f,
            failure: DomainFailure.None);

        public WorkEnvironmentAssessment Assess(
            CharacterActor actor,
            Vector2Int destination,
            float expectedSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;

        public WorkEnvironmentAssessment AssessStart(
            CharacterActor actor,
            Vector2Int destination,
            IReadOnlyList<GridMoveStep> route,
            float expectedSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;

        public WorkEnvironmentAssessment RecheckActive(
            CharacterActor actor,
            Vector2Int currentPosition,
            float remainingSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;

        public bool TryFindEvacuationCell(
            CharacterActor actor,
            Grid grid,
            out Vector2Int destination,
            out bool fullySafe,
            out DomainFailure failure)
        {
            destination = actor != null ? actor.GetNowXY() : Vector2Int.zero;
            fullySafe = true;
            failure = DomainFailure.None;
            return true;
        }
    }

    private sealed class EditorNoPaidFacilityContractRuntime :
        IPaidFacilityContractRuntime
    {
        public IReadOnlyList<PaidFacilityContractState> Contracts =>
            Array.Empty<PaidFacilityContractState>();

        public int ForecastCost(int days) => 0;
        public int SettleDay(int day) => 0;
        public PaidFacilityContractState GetContract(BuildableObject facility) => null;

        public bool CanBeginUse(BuildableObject facility, out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryChargeUse(BuildableObject facility, out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryChargeOrder(
            BuildableObject facility,
            string orderKey,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TrySetDailyContractActive(
            BuildableObject facility,
            bool active,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public void SynchronizeFacility(BuildableObject facility) { }
        public void RemoveFacility(BuildableObject facility) { }
        public string GetLastFailureReason(BuildableObject facility) => string.Empty;
        public PaidFacilityContractSaveData Capture() => new PaidFacilityContractSaveData();

        public bool CanBeginUse(
            IBuildingWorldEntryPort facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryChargeUse(
            IBuildingWorldEntryPort facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public void SynchronizeFacility(IBuildingWorldEntryPort facility) { }
        public void RemoveFacility(IBuildingWorldEntryPort facility) { }
    }

    private sealed class EditorNoEmploymentContractRuntime :
        IEmploymentContractRuntime
    {
        public IReadOnlyList<EmployeeWageState> WageStates =>
            Array.Empty<EmployeeWageState>();

        public IReadOnlyList<MercenaryContract> MercenaryContracts =>
            Array.Empty<MercenaryContract>();

        public int ForecastCost(int days) => 0;
        public int GetDailyCost(string characterId) => 0;

        public int QuoteMercenaryDailyCost(
            string characterId,
            int level,
            int rolePremium) => 0;

        public EmploymentDailySettlement SettleDay(int day) =>
            new EmploymentDailySettlement
            {
                day = Mathf.Max(1, day)
            };

        public bool TryHireMercenary(
            CharacterActor actor,
            int rolePremium,
            int day,
            out string failureReason)
        {
            failureReason = "Employment mutation is unavailable in this fixture.";
            return false;
        }

        public bool SetEmployeeRolePremium(
            string characterId,
            int premium,
            out string failureReason)
        {
            failureReason = "Employment mutation is unavailable in this fixture.";
            return false;
        }

        public EmploymentContractSaveData Capture() =>
            new EmploymentContractSaveData();
    }
}

internal sealed class EditorCharacterAiPerformanceRecorder : ICharacterAiPerformanceRecorder
{
    private const double SlowOperationThresholdMilliseconds = 1d;
    private const int MaximumSlowOperationEntries = 64;
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
    private bool slowTraceEnabled;
    private int slowTraceEntryCount;
    private readonly List<string> slowTraceEntries =
        new List<string>(MaximumSlowOperationEntries);

    public EditorCharacterAiPerformanceRecorder()
    {
        samples = new List<double>[Names.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = new List<double>(512);
        }
    }

    public bool DetailedCollectionEnabled => detailedCollectionEnabled;
    public bool SlowTraceEnabled =>
        slowTraceEnabled
        && slowTraceEntryCount < MaximumSlowOperationEntries;

    public void RecordSlowOperation(
        string stage,
        CharacterActor actor,
        AIActionSet actionSet,
        Consideration consideration,
        double elapsedMilliseconds)
    {
        if (!SlowTraceEnabled
            || elapsedMilliseconds < SlowOperationThresholdMilliseconds)
        {
            return;
        }

        slowTraceEntryCount++;
        string actorId = actor?.Identity?.PersistentId;
        string actorLabel = !string.IsNullOrWhiteSpace(actorId)
            ? actorId
            : actor != null && !string.IsNullOrWhiteSpace(actor.name)
                ? actor.name
                : "none";
        string stageLabel = stage ?? "unknown";
        string actionType = actionSet != null
            ? actionSet.GetType().Name
            : "none";
        string actionLabel = actionSet != null
            ? actionSet.GetDisplayLabel()
            : "none";
        string considerationType = consideration != null
            ? consideration.GetType().Name
            : "none";
        slowTraceEntries.Add(
            $"AI_SLOW_OPERATION #{slowTraceEntryCount} "
            + $"stage={stageLabel} "
            + $"elapsedMs={elapsedMilliseconds:0.000} "
            + $"actor={actorLabel} "
            + $"action={actionType} "
            + $"actionLabel={actionLabel} "
            + $"consideration={considerationType}");
    }

    public void SetDetailedCollectionEnabled(bool enabled)
    {
        detailedCollectionEnabled = enabled;
    }

    public void SetSlowTraceEnabled(bool enabled)
    {
        slowTraceEnabled = enabled;
        slowTraceEntryCount = 0;
        slowTraceEntries.Clear();
    }

    public void FlushSlowTrace()
    {
        for (int index = 0; index < slowTraceEntries.Count; index++)
        {
            UnityEngine.Debug.Log(slowTraceEntries[index]);
        }

        slowTraceEntries.Clear();
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

    public void RecordGridPathSearch(double elapsedMilliseconds)
    {
        Record(AiPerformanceCategory.PathSearch, elapsedMilliseconds);
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
        slowTraceEntryCount = 0;
        slowTraceEntries.Clear();
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
