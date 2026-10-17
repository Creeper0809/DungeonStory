using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

public class RunVariableRuntime :
    MonoBehaviour,
    IRunVariableRuntime,
    IRunVariableRestorePublisher,
    IDungeonSaveRestoreCompletedHook
{
    [FormerlySerializedAs("runSeed")]
    [SerializeField] private int initialRunSeed;

    private IRandomStream random;
    private IRandomStreamProvider randomStreamProvider;
    private IOwnerRunDataProvider ownerRunDataProvider;
    private InvasionThreatRuntime invasionThreat;
    private IRunStartVariableSelector runStartVariableSelector;
    private IGameEventBus gameEventBus;
    private IRunVariableDefinitionCatalog definitionCatalog;
    private IOwnerDoctrineDefinitionCatalog ownerDoctrineCatalog;
    private DungeonRuntimeAggregateRootStore aggregateRootStore;
    private IDomainFactPublisher facts;
    private IDomainFactRecoveryPublisher recovery;
    private bool publishing;
    private long nextRecoveryTick;
    private string recoveryFailure;
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
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher recovery)
    {
        AssertIdle();
        if (!ReferenceEquals(this.gameEventBus, gameEventBus)) OnDisable();
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
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
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
        RequireObservationReady();
        runSeed = seed != 0 ? seed : Environment.TickCount;
        IRandomStreamProvider provider = ResolveRandomStreamProvider();
        provider.Reseed(runSeed);
        random = provider.Get("run-variables");
        RunStartVariableSnapshot snapshot = ResolveRunStartVariableSelector()
            .Create(runSeed, ownerData, difficulty, survivalPressure);
        state.SetStartVariables(snapshot);
        PublishObserver(() => gameEventBus.Publish(new RunStartVariablesSelectedEvent(snapshot)),
            "run-start-variables-observer");
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
        RequireObservationReady();
        currentDay = Mathf.Max(1, eventType.day);
        EnsureRunStarted();

        if (currentDay > 1)
        {
            RollOperationVariable(currentDay);
        }
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        RequireObservationReady();
        int completedDay = Mathf.Max(1, eventType.day);
        if (completedDay <= aggregateState.LastOperationAdvanceDay) return;

        RunVariableAggregateState preview = aggregateState.DeepClone();
        ActiveRunVariable[] expired = preview.Variables.AdvanceOperationVariables()
            .OrderBy(active => active.InstanceSequence).ToArray();
        if (expired.Length == 0)
        {
            state.AdvanceOperationVariables();
            aggregateState.LastOperationAdvanceDay = completedDay;
            currentDay = Math.Max(currentDay, completedDay);
            return;
        }
        RunVariableFact fact = CreateFact(RunVariableFactKind.Expired, completedDay, expired);
        if (!ApplyObservedChange(fact, () =>
            {
                var actual = state.AdvanceOperationVariables().OrderBy(value => value.InstanceSequence).ToArray();
                if (!actual.Select(value => value.InstanceSequence).SequenceEqual(expired.Select(value => value.InstanceSequence)))
                    throw new InvalidOperationException("run-variable-expiration-preview-diverged");
                aggregateState.LastOperationAdvanceDay = completedDay;
                currentDay = Math.Max(currentDay, completedDay);
            })) return;

        foreach (ActiveRunVariable active in expired)
            PublishObserver(() => gameEventBus.Publish(new RunVariableExpiredEvent(active.Definition)),
                "run-variable-expired-observer");
    }

    private void OnInvasionCandidate(InvasionCandidateEvent eventType)
    {
        EnsureRunStarted();
        SelectRandomInvasionVariable();
    }

    public void OnTriggerEvent(InvasionResolvedEvent eventType)
    {
        RequireObservationReady();
        state.ClearInvasionVariable();
    }

    public ActiveRunVariable ActivateOperationVariable(string id, int day = -1, bool alert = true)
    {
        RequireObservationReady();
        RunVariableDefinition definition = ResolveDefinitionCatalog().Get(id);
        if (definition == null || definition.category != RunVariableCategory.Operation) return null;
        EnsureRunStarted();
        int activationDay = day > 0 ? day : currentDay;
        long sequence = aggregateState.NextOperationSequence;
        long nextSequence = checked(sequence + 1L);
        var proposed = new ActiveRunVariable(definition, activationDay, definition.activeDays, sequence);
        RunVariableFact fact = CreateFact(RunVariableFactKind.Activated, activationDay, new[] { proposed });
        ActiveRunVariable active = null;
        if (!ApplyObservedChange(fact, () =>
            {
                active = state.ActivateOperationVariable(definition, activationDay, sequence)
                    ?? throw new InvalidOperationException("run-variable-activation-rejected");
                aggregateState.NextOperationSequence = nextSequence;
                currentDay = Math.Max(currentDay, activationDay);
            })) return null;

        PublishObserver(() => gameEventBus.Publish(new RunVariableActivatedEvent(active, alert)),
            "run-variable-activated-observer");
        return active;
    }

    public RunVariableDefinition SelectInvasionVariable(string id, bool alert = true)
    {
        RequireObservationReady();
        RunVariableDefinition definition = ResolveDefinitionCatalog().Get(id);
        if (definition == null || definition.category != RunVariableCategory.Invasion)
        {
            return null;
        }

        state.SetInvasionVariable(definition);
        PublishObserver(() => gameEventBus.Publish(new InvasionVariableSelectedEvent(definition, alert)),
            "invasion-variable-selected-observer");

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

    public void PublishRestoreState(RunVariableAggregateState candidate)
    {
        AssertIdle();
        aggregateRootStore.Replace(candidate ?? throw new ArgumentNullException(nameof(candidate)));
        random = null;
        nextRecoveryTick = 0;
        recoveryFailure = null;
    }

    public DungeonRunVariableSaveData CaptureForSave()
    {
        AssertIdle();
        DungeonRunVariableSaveData destination = new()
        {
            runSeed = RunSeed,
            currentDay = CurrentDay,
            nextOperationSequence = aggregateState.NextOperationSequence,
            lastOperationAdvanceDay = aggregateState.LastOperationAdvanceDay,
            pendingObservations = aggregateState.PendingObservations.Select(value => value.Clone()).ToList()
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
                remainingDays = active.RemainingDays,
                instanceSequence = active.InstanceSequence
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
                saved.remainingDays,
                saved.instanceSequence));
        }

        if (source.version != DungeonRunVariableSaveData.CurrentVersion)
            throw new InvalidOperationException("run-variable-save-version-unsupported");
        RunVariableSaveSection.ValidateObservationOwners(source);
        var candidate = new RunVariableAggregateState(source.runSeed, source.currentDay)
        {
            NextOperationSequence = source.nextOperationSequence,
            LastOperationAdvanceDay = source.lastOperationAdvanceDay
        };
        candidate.Variables.Restore(start, activeVariables,
            source.invasionVariableId.Length == 0 ? null : ResolveDefinitionCatalog().Require(source.invasionVariableId));
        candidate.PendingObservations.AddRange(source.pendingObservations.Select(value => value.Clone()));
        PublishRestoreState(candidate);
    }

    private int NextRandomIndex(int maximum)
    {
        EnsureRandom();
        int safeMaximum = Mathf.Max(1, maximum);
        return random.NextInt(0, safeMaximum);
    }

    private RunVariableFact CreateFact(RunVariableFactKind kind, int day, IEnumerable<ActiveRunVariable> entries) =>
        new(new RunVariableFactData
        {
            kind = kind, runSeed = runSeed, absoluteDay = day,
            entries = entries.Select(value => new RunVariableFactEntry
            {
                instanceSequence = value.InstanceSequence, definitionId = value.Definition.id,
                title = value.Definition.title, startDay = value.StartDay, remainingDays = value.RemainingDays
            }).ToList()
        });

    private bool ApplyObservedChange(RunVariableFact fact, Action mutation)
    {
        AssertIdle();
        var rollback = aggregateState.DeepClone();
        DomainFactLease lease = default;
        publishing = true;
        bool accepted = false;
        try
        {
            if (!facts.TryReserve(fact, out lease, out string failure))
            {
                Debug.LogError("run-variable-observation-reservation-rejected:" + failure);
                return false;
            }
            var pending = new RunVariablePendingFactData { publicationKey = lease.PublicationKey, fact = fact.Snapshot() };
            pending.Validate();
            mutation();
            aggregateState.PendingObservations.Add(pending);
            DomainFactDelivery delivery = facts.Publish(lease, fact);
            if (!delivery.MayBeCommitted)
            {
                Debug.LogError("run-variable-observation-rejected:" + delivery.Detail);
                return false;
            }
            accepted = true;
            if (delivery.Phase != DomainFactDeliveryPhase.InDoubt)
                aggregateState.PendingObservations.Clear();
            else
                ReportPending(delivery.Detail);
            return true;
        }
        finally
        {
            // Rejected preparation/commit has no durable fact and restores the complete
            // state, including replaced variables and the operation sequence.
            if (!accepted) aggregateRootStore.Replace(rollback);
            try { if (lease.IsValid) facts.Cancel(lease); }
            finally { publishing = false; }
        }
    }

    public bool ResumePendingObservations()
    {
        AssertIdle();
        if (aggregateState.PendingObservations.Count == 0) return true;
        publishing = true;
        try
        {
            var pending = aggregateState.PendingObservations.Single();
            pending.Validate();
            var results = new DomainFactDelivery[1];
            bool accepted = recovery.ReplayBatch(new[]
            {
                DomainFactReplay.Create(pending.publicationKey, new RunVariableFact(pending.fact))
            }, results, out string failure);
            if (accepted && results[0].MayBeCommitted && results[0].Phase != DomainFactDeliveryPhase.InDoubt)
            {
                aggregateState.PendingObservations.Clear();
                recoveryFailure = null;
                return true;
            }
            ReportPending(failure ?? results[0].Detail);
            return false;
        }
        catch (Exception error) when (error is not OutOfMemoryException
            && error is not StackOverflowException && error is not AccessViolationException)
        {
            ReportPending(error.GetType().Name + ":" + error.Message);
            return false;
        }
        finally { publishing = false; }
    }

    public void OnRestoreCompleted() => ResumePendingObservations();

    private void Update()
    {
        if (aggregateRootStore == null || publishing || aggregateState.PendingObservations.Count == 0) return;
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (now < nextRecoveryTick) return;
        nextRecoveryTick = now + System.Diagnostics.Stopwatch.Frequency;
        ResumePendingObservations();
    }

    private void RequireObservationReady()
    {
        AssertIdle();
        if (!ResumePendingObservations())
            throw new InvalidOperationException("run-variable-observation-pending");
    }

    private void AssertIdle()
    {
        if (publishing) throw new InvalidOperationException("run-variable-observation-in-flight");
    }

    private void ReportPending(string detail)
    {
        if (recoveryFailure == detail) return;
        recoveryFailure = detail;
        Debug.LogWarning("run-variable-observation-pending:" + detail);
    }

    private static void PublishObserver(Action observer, string label)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(label + ":" + exception.GetType().Name);
        }
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

    private void OnDestroy() => OnDisable();

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
