using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

public class RunVariableRuntime :
    MonoBehaviour,
    IRunVariableRuntime,
    IRunVariableRestorePublisher
{
    [FormerlySerializedAs("runSeed")]
    [SerializeField] private int initialRunSeed;
    [SerializeField] private bool raiseAlerts = true;

    private IRandomStream random;
    private IRandomStreamProvider randomStreamProvider;
    private IOwnerRunDataProvider ownerRunDataProvider;
    private InvasionThreatRuntime invasionThreat;
    private IRunStartVariableSelector runStartVariableSelector;
    private IGameEventBus gameEventBus;
    private IRunVariableDefinitionCatalog definitionCatalog;
    private IOwnerDoctrineDefinitionCatalog ownerDoctrineCatalog;
    private DungeonRuntimeAggregateRootStore aggregateRootStore;
    private IDisposable invasionCandidateSubscription;
    private IDisposable invasionResolvedSubscription;
    private IDisposable operatingDayStartedSubscription;
    private IDisposable operatingDayEndedSubscription;

    private RunVariableAggregateState aggregateState =>
        (aggregateRootStore
            ?? throw new InvalidOperationException(
                $"{nameof(RunVariableRuntime)} requires aggregate-root injection."))
        .GetOrCreate(CreateInitialAggregateState);
    private RunVariableState state => aggregateState.Variables;
    private int runSeed
    {
        get => aggregateState.RunSeed;
        set => aggregateState.RunSeed = value;
    }
    private int currentDay
    {
        get => aggregateState.CurrentDay;
        set => aggregateState.CurrentDay = value;
    }

    public IRunVariableStateView State => ResolveState();
    public IRunVariableDefinitionCatalog DefinitionCatalog => ResolveDefinitionCatalog();
    public int RunSeed => runSeed;
    public int CurrentDay => currentDay;

    [Inject]
    public void Construct(
        IOwnerRunDataProvider ownerRunDataProvider,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IRunStartVariableSelector runStartVariableSelector,
        IRandomStreamProvider randomStreamProvider,
        IGameEventBus gameEventBus,
        IRunVariableDefinitionCatalog definitionCatalog,
        IOwnerDoctrineDefinitionCatalog ownerDoctrineCatalog,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.ownerRunDataProvider = ownerRunDataProvider
            ?? throw new ArgumentNullException(nameof(ownerRunDataProvider));
        invasionThreat = (invasionRuntimes
                ?? throw new ArgumentNullException(nameof(invasionRuntimes)))
            .Threat
            ?? throw new InvalidOperationException(
                $"{nameof(RunVariableRuntime)} requires a loaded {nameof(InvasionThreatRuntime)}.");
        this.runStartVariableSelector = runStartVariableSelector
            ?? throw new ArgumentNullException(nameof(runStartVariableSelector));
        this.randomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.definitionCatalog = definitionCatalog
            ?? throw new ArgumentNullException(nameof(definitionCatalog));
        this.ownerDoctrineCatalog = ownerDoctrineCatalog
            ?? throw new ArgumentNullException(nameof(ownerDoctrineCatalog));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        aggregateRootStore.GetOrCreate(CreateInitialAggregateState);
        SubscribeToScopedEvents();
    }

    private void Awake()
    {
        if (initialRunSeed == 0)
        {
            initialRunSeed = Environment.TickCount;
        }

    }

    public void StartRun(
        int seed,
        CharacterSO ownerData = null,
        DungeonDifficulty difficulty = DungeonDifficulty.Normal,
        DungeonSurvivalPressure survivalPressure =
            DungeonSurvivalPressure.Standard)
    {
        runSeed = seed != 0 ? seed : Environment.TickCount;
        IRandomStreamProvider provider = ResolveRandomStreamProvider();
        provider.Reseed(runSeed);
        random = provider.Get("run-variables");
        RunStartVariableSnapshot snapshot = ResolveRunStartVariableSelector()
            .Create(runSeed, ownerData, difficulty, survivalPressure);
        state.SetStartVariables(snapshot);
        gameEventBus.Publish(new RunStartVariablesSelectedEvent(snapshot));

        if (raiseAlerts)
        {
            gameEventBus.RaiseAlert(
                "런 시작 변수",
                snapshot.ToSummaryText(),
                EventAlertImportance.Low,
                "런 변수");
        }
    }

    public void StartRun(int seed, CharacterSO ownerData, InvasionThreatDifficulty difficulty)
    {
        StartRun(
            seed,
            ownerData,
            DungeonDifficultyRules.FromLegacy(difficulty),
            DungeonSurvivalPressure.Standard);
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType)
    {
        currentDay = Mathf.Max(1, eventType.day);
        EnsureRunStarted();

        if (currentDay > 1)
        {
            RollOperationVariable(currentDay);
        }
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        IReadOnlyList<ActiveRunVariable> expired = state.AdvanceOperationVariables();
        foreach (ActiveRunVariable active in expired)
        {
            gameEventBus.Publish(new RunVariableExpiredEvent(active.Definition));
        }
    }

    private void OnInvasionCandidate(InvasionCandidateEvent eventType)
    {
        EnsureRunStarted();
        SelectRandomInvasionVariable();
    }

    public void OnTriggerEvent(InvasionResolvedEvent eventType)
    {
        state.ClearInvasionVariable();
    }

    public ActiveRunVariable ActivateOperationVariable(string id, int day = -1, bool alert = true)
    {
        RunVariableDefinition definition = ResolveDefinitionCatalog().Get(id);
        ActiveRunVariable active = state.ActivateOperationVariable(definition, day > 0 ? day : currentDay);
        if (active == null)
        {
            return null;
        }

        gameEventBus.Publish(new RunVariableActivatedEvent(active));
        if (alert && raiseAlerts)
        {
            gameEventBus.RaiseAlert(
                active.Definition.title,
                active.Definition.ToDetailText(),
                active.Definition.importance,
                "운영 변수");
        }

        return active;
    }

    public RunVariableDefinition SelectInvasionVariable(string id, bool alert = true)
    {
        RunVariableDefinition definition = ResolveDefinitionCatalog().Get(id);
        if (definition == null || definition.category != RunVariableCategory.Invasion)
        {
            return null;
        }

        state.SetInvasionVariable(definition);
        gameEventBus.Publish(new InvasionVariableSelectedEvent(definition));
        if (alert && raiseAlerts)
        {
            gameEventBus.RaiseAlert(
                definition.title,
                definition.ToDetailText(),
                definition.importance,
                "침입 변수");
        }

        return definition;
    }

    public float GetGuestDemandMultiplier(string speciesTag)
    {
        return RunVariableEffects.GetGuestDemandMultiplier(
            state,
            ResolveOwnerDoctrineCatalog(),
            speciesTag);
    }

    public float GetStockCostMultiplier(StockCategory category)
    {
        return RunVariableEffects.GetStockCostMultiplier(
            state,
            ResolveOwnerDoctrineCatalog(),
            category);
    }

    public float GetFacilityShopCostMultiplier(BuildingSO building)
    {
        return RunVariableEffects.GetFacilityShopCostMultiplier(
            state,
            ResolveOwnerDoctrineCatalog(),
            building);
    }

    public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint)
    {
        return RunVariableEffects.GetBlueprintCostMultiplier(
            state,
            ResolveOwnerDoctrineCatalog(),
            blueprint);
    }

    public float GetThreatRiseMultiplier()
    {
        return RunVariableEffects.GetThreatRiseMultiplier(
            state,
            ResolveOwnerDoctrineCatalog());
    }

    public float GetWarningThresholdMultiplier()
    {
        return RunVariableEffects.GetWarningThresholdMultiplier(
            state,
            ResolveOwnerDoctrineCatalog());
    }

    public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source)
    {
        return RunVariableEffects.ApplyInvasionSettings(
            state,
            ResolveOwnerDoctrineCatalog(),
            source);
    }

    private void RollOperationVariable(int day)
    {
        IReadOnlyList<RunVariableDefinition> definitions = ResolveDefinitionCatalog()
            .GetByCategory(RunVariableCategory.Operation);
        if (definitions.Count == 0)
        {
            return;
        }

        RunVariableDefinition selected = definitions[NextRandomIndex(definitions.Count)];
        ActivateOperationVariable(selected.id, day);
    }

    private void SelectRandomInvasionVariable()
    {
        EnsureRandom();

        IReadOnlyList<RunVariableDefinition> definitions = ResolveDefinitionCatalog()
            .GetByCategory(RunVariableCategory.Invasion);
        if (definitions.Count == 0)
        {
            return;
        }

        SelectInvasionVariable(definitions[NextRandomIndex(definitions.Count)].id);
    }

    public void RestoreRun(
        int savedSeed,
        int savedCurrentDay,
        RunStartVariableSnapshot startVariables,
        IEnumerable<ActiveRunVariable> operationVariables,
        RunVariableDefinition invasionVariable)
    {
        RunVariableAggregateState restored = new RunVariableAggregateState(
            savedSeed,
            savedCurrentDay);
        restored.Variables.Restore(
            startVariables,
            operationVariables,
            invasionVariable);
        PublishRestoreState(restored);
    }

    public void PublishRestoreState(RunVariableAggregateState candidate) =>
        aggregateRootStore.Replace(candidate);

    public DungeonRunVariableSaveData CaptureForSave()
    {
        DungeonRunVariableSaveData destination = new()
        {
            runSeed = RunSeed,
            currentDay = CurrentDay
        };
        RunStartVariableSnapshot start = State.StartVariables;
        destination.hasStartVariables = start != null;
        if (start != null)
        {
            destination.startVariables = new DungeonRunStartSaveData
            {
                seed = start.seed,
                ownerSpeciesTag = start.ownerSpeciesTag,
                ownerDoctrineId = start.ownerDoctrineId,
                runDifficulty = start.runDifficulty,
                survivalPressure = start.survivalPressure,
                startingFacilityCandidateIds =
                    start.startingFacilityCandidateIds.ToList(),
                startingGuestSpeciesCandidates =
                    start.startingGuestSpeciesCandidates.ToList(),
                startingBlueprintCandidateIds =
                    start.startingBlueprintCandidateIds.ToList(),
                initialShopSeed = start.initialShopSeed,
                initialDungeonLayoutId = start.initialDungeonLayoutId,
                threatRiseMultiplier = start.threatRiseMultiplier
            };
        }

        destination.activeOperationVariables = State.ActiveOperationVariables
            .Select(active => new DungeonActiveRunVariableSaveData
            {
                definitionId = active.Definition.id,
                startDay = active.StartDay,
                remainingDays = active.RemainingDays
            })
            .ToList();
        destination.invasionVariableId =
            State.CurrentInvasionVariable?.id ?? string.Empty;
        return destination;
    }

    public void RestoreFromSave(DungeonRunVariableSaveData source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        RunStartVariableSnapshot start = null;
        if (source.hasStartVariables)
        {
            DungeonRunStartSaveData savedStart = source.startVariables;
            start = new RunStartVariableSnapshot(
                savedStart.seed,
                savedStart.ownerSpeciesTag,
                savedStart.runDifficulty,
                savedStart.startingFacilityCandidateIds,
                savedStart.startingGuestSpeciesCandidates,
                savedStart.startingBlueprintCandidateIds,
                savedStart.initialShopSeed,
                savedStart.initialDungeonLayoutId,
                savedStart.threatRiseMultiplier,
                savedStart.ownerDoctrineId,
                savedStart.survivalPressure);
        }

        List<ActiveRunVariable> activeVariables = new();
        foreach (DungeonActiveRunVariableSaveData saved in
                 source.activeOperationVariables)
        {
            RunVariableDefinition definition =
                ResolveDefinitionCatalog().Require(saved.definitionId);
            activeVariables.Add(new ActiveRunVariable(
                definition,
                saved.startDay,
                saved.remainingDays));
        }

        RestoreRun(
            source.runSeed,
            source.currentDay,
            start,
            activeVariables,
            source.invasionVariableId.Length == 0
                ? null
                : ResolveDefinitionCatalog().Require(
                    source.invasionVariableId));
    }

    private int NextRandomIndex(int maximum)
    {
        EnsureRandom();
        int safeMaximum = Mathf.Max(1, maximum);
        return random.NextInt(0, safeMaximum);
    }

    private void EnsureRunStarted()
    {
        EnsureRandom();

        if (state.HasStarted)
        {
            return;
        }

        StartRun(
            runSeed,
            ResolveSelectedOwnerData(),
            ResolveDifficulty(),
            DungeonSurvivalPressure.Standard);
    }

    private void EnsureRandom()
    {
        if (random == null)
        {
            random = ResolveRandomStreamProvider().Get("run-variables");
        }
    }

    private IRandomStreamProvider ResolveRandomStreamProvider()
    {
        return randomStreamProvider
            ?? throw new InvalidOperationException(
                $"{nameof(RunVariableRuntime)} requires "
                + $"{nameof(IRandomStreamProvider)} injection.");
    }

    private DungeonDifficulty ResolveDifficulty()
    {
        InvasionThreatRuntime threatRuntime = ResolveInvasionThreatRuntime();
        return threatRuntime != null && threatRuntime.Settings != null
            ? DungeonDifficultyRules.FromLegacy(threatRuntime.Settings.difficulty)
            : DungeonDifficulty.Normal;
    }

    private CharacterSO ResolveSelectedOwnerData()
    {
        return ResolveOwnerRunDataProvider().SelectedOwnerData;
    }

    private IOwnerRunDataProvider ResolveOwnerRunDataProvider()
    {
        return ownerRunDataProvider
            ?? throw new InvalidOperationException($"{nameof(RunVariableRuntime)} requires {nameof(IOwnerRunDataProvider)} injection.");
    }

    private InvasionThreatRuntime ResolveInvasionThreatRuntime()
    {
        return invasionThreat
            ?? throw new InvalidOperationException($"{nameof(RunVariableRuntime)} requires a loaded {nameof(InvasionThreatRuntime)}.");
    }

    private IRunStartVariableSelector ResolveRunStartVariableSelector()
    {
        return runStartVariableSelector
            ?? throw new InvalidOperationException($"{nameof(RunVariableRuntime)} requires {nameof(IRunStartVariableSelector)} injection.");
    }

    private RunVariableState ResolveState()
    {
        return state;
    }

    private RunVariableAggregateState CreateInitialAggregateState()
    {
        return new RunVariableAggregateState(
            initialRunSeed != 0 ? initialRunSeed : 1);
    }

    private IOwnerDoctrineDefinitionCatalog ResolveOwnerDoctrineCatalog()
    {
        return ownerDoctrineCatalog ?? throw new InvalidOperationException(
            $"{nameof(RunVariableRuntime)} requires "
            + $"{nameof(IOwnerDoctrineDefinitionCatalog)} injection.");
    }

    private IRunVariableDefinitionCatalog ResolveDefinitionCatalog()
    {
        return definitionCatalog ?? throw new InvalidOperationException(
            $"{nameof(RunVariableRuntime)} requires {nameof(IRunVariableDefinitionCatalog)} injection.");
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        invasionCandidateSubscription?.Dispose();
        invasionCandidateSubscription = null;
        invasionResolvedSubscription?.Dispose();
        invasionResolvedSubscription = null;
        operatingDayStartedSubscription?.Dispose();
        operatingDayStartedSubscription = null;
        operatingDayEndedSubscription?.Dispose();
        operatingDayEndedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        invasionCandidateSubscription ??=
            gameEventBus.Subscribe<InvasionCandidateEvent>(OnInvasionCandidate);
        invasionResolvedSubscription ??=
            gameEventBus.Subscribe<InvasionResolvedEvent>(OnTriggerEvent);
        operatingDayStartedSubscription ??=
            gameEventBus.Subscribe<OperatingDayStartedEvent>(OnTriggerEvent);
        operatingDayEndedSubscription ??=
            gameEventBus.Subscribe<OperatingDayEndedEvent>(OnTriggerEvent);
    }

}
