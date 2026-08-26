using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class CropPlotState
{
    public BuildingInstanceId PlotId;
    public BuildableObject Building;
    public BuildingCropPlotAbility Ability;
    public Vector2Int LastKnownPosition;
    public string CropId = string.Empty;
    public CropPlotPhase Phase;
    public float SowWork;
    public float GrowthHours;
    public float HarvestWork;
    public string MaterialDestinationId = string.Empty;
    public bool MaterialsConsumed;
    public string BlockedReason = string.Empty;
    public string GoldenHarvestHarvesterId = string.Empty;
    public int GoldenHarvestAttemptSequence;
    public int NextSowOperationSequence;
    public CropPhysicalCommitSaveData PendingSow = new();
    public int NextTreatmentOperationSequence;
    public int PestLureNextAllowedDay;
    public int BotanicalPesticideNextAllowedDay;
    public int FungicideNextAllowedDay;
    public CropTreatmentOrderSaveData Treatment = new();
}

internal sealed class CropPlotAggregateState
{
    internal Dictionary<BuildingInstanceId, CropPlotState> States { get; } =
        new();
    internal Dictionary<BuildableObject, CropPlotState> StatesByBuilding { get; } =
        new();
    internal List<CropPlotSnapshot> Snapshots { get; } = new();
    internal int ObservedBuildingVersion { get; set; } = -1;
    internal float NextMaterialRequestTime { get; set; }
    internal bool SnapshotsDirty { get; set; } = true;
    internal int Version { get; set; }
}

public sealed class CropPlotWorldDependencies
{
    public CropPlotWorldDependencies(
        IBuildingWorldQuery buildingWorld,
        IResourceEconomyContentCatalog catalog,
        IProductionItemGateway items,
        IPhysicalSeedLotGateway seedLots,
        IPhysicalFacilityItemSinkGateway treatmentItems,
        IPackagedLotTareDispositionService packagedTare,
        ICropEcologyService ecology,
        IFacilityCapabilityQuery facilities,
        IFacilityCandidateCache facilityCandidates,
        IWorkforceReplanService workforce)
    {
        BuildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        SeedLots = seedLots ?? throw new ArgumentNullException(nameof(seedLots));
        TreatmentItems = treatmentItems
            ?? throw new ArgumentNullException(nameof(treatmentItems));
        PackagedTare = packagedTare
            ?? throw new ArgumentNullException(nameof(packagedTare));
        Ecology = ecology ?? throw new ArgumentNullException(nameof(ecology));
        Facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        FacilityCandidates = facilityCandidates
            ?? throw new ArgumentNullException(nameof(facilityCandidates));
        Workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
    }

    public IBuildingWorldQuery BuildingWorld { get; }
    public IResourceEconomyContentCatalog Catalog { get; }
    public IProductionItemGateway Items { get; }
    public IPhysicalSeedLotGateway SeedLots { get; }
    public IPhysicalFacilityItemSinkGateway TreatmentItems { get; }
    public IPackagedLotTareDispositionService PackagedTare { get; }
    public ICropEcologyService Ecology { get; }
    public IFacilityCapabilityQuery Facilities { get; }
    public IFacilityCandidateCache FacilityCandidates { get; }
    public IWorkforceReplanService Workforce { get; }
}

public sealed class CropPlotSimulationDependencies
{
    public CropPlotSimulationDependencies(
        IGameClock gameClock,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IGameSessionStateProvider gameDataProvider,
        ISurvivalEnvironmentQuery environmentQuery,
        IGrandProjectBenefitQuery grandProjectBenefits,
        IGameEventBus events,
        ExtremeTraitRuntime extremeTraits = null,
        IRunSeedProvider runSeedProvider = null,
        CharacterIdentityEventPublisher identityEvents = null)
    {
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        ProgressionRuntimes = progressionRuntimes
            ?? throw new ArgumentNullException(nameof(progressionRuntimes));
        GameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        EnvironmentQuery = environmentQuery
            ?? throw new ArgumentNullException(nameof(environmentQuery));
        GrandProjectBenefits = grandProjectBenefits
            ?? throw new ArgumentNullException(nameof(grandProjectBenefits));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        ExtremeTraits = extremeTraits;
        RunSeedProvider = runSeedProvider;
        IdentityEvents = identityEvents;
    }

    public IGameClock GameClock { get; }
    public ProgressionSceneRuntimeReferences ProgressionRuntimes { get; }
    public IGameSessionStateProvider GameDataProvider { get; }
    public ISurvivalEnvironmentQuery EnvironmentQuery { get; }
    public IGrandProjectBenefitQuery GrandProjectBenefits { get; }
    public IGameEventBus Events { get; }
    public ExtremeTraitRuntime ExtremeTraits { get; }
    public IRunSeedProvider RunSeedProvider { get; }
    public CharacterIdentityEventPublisher IdentityEvents { get; }
}

public sealed class CropPlotRuntime :
    ICropPlotRuntime,
    ICropPlotPersistence,
    IInitializable,
    ITickable,
    IDisposable
{
    private const float SecondsPerGameHour = 7.5f;
    private const float MaterialRequestInterval = 0.5f;
    private const string CompostItemId = "material:compost";
    private const string CleanWaterItemId = "resource:clean-water";

    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionItemGateway items;
    private readonly IPhysicalSeedLotGateway seedLots;
    private readonly IPhysicalFacilityItemSinkGateway treatmentItems;
    private readonly IPackagedLotTareDispositionService packagedTare;
    private readonly ICropEcologyService ecology;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IGameClock gameClock;
    private readonly BlueprintResearchRuntime research;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly ISurvivalEnvironmentQuery environmentQuery;
    private readonly IFacilityCandidateCache facilityCandidates;
    private readonly IWorkforceReplanService workforce;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly IGameEventBus events;
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly IRunSeedProvider runSeedProvider;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly ICharacterPerformanceQuery performance;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private IDisposable dayEndedSubscription;

    private CropPlotAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState());
    private Dictionary<BuildingInstanceId, CropPlotState> states =>
        aggregateState.States;
    private Dictionary<BuildableObject, CropPlotState> statesByBuilding =>
        aggregateState.StatesByBuilding;
    private List<CropPlotSnapshot> snapshots => aggregateState.Snapshots;
    private int observedBuildingVersion
    {
        get => aggregateState.ObservedBuildingVersion;
        set => aggregateState.ObservedBuildingVersion = value;
    }
    private float nextMaterialRequestTime
    {
        get => aggregateState.NextMaterialRequestTime;
        set => aggregateState.NextMaterialRequestTime = value;
    }
    private bool snapshotsDirty
    {
        get => aggregateState.SnapshotsDirty;
        set => aggregateState.SnapshotsDirty = value;
    }

    internal IReadOnlyCollection<CropPlotState> PhysicalTransactionStates =>
        states.Values;

    public CropPlotRuntime(
        CropPlotWorldDependencies world,
        CropPlotSimulationDependencies simulation,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IMilestoneGameplayModifierQuery milestoneModifiers = null,
        ICharacterPerformanceQuery performance = null)
    {
        world = world ?? throw new ArgumentNullException(nameof(world));
        simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        buildingWorld = world.BuildingWorld;
        catalog = world.Catalog;
        items = world.Items;
        seedLots = world.SeedLots;
        treatmentItems = world.TreatmentItems;
        packagedTare = world.PackagedTare;
        ecology = world.Ecology;
        facilities = world.Facilities;
        facilityCandidates = world.FacilityCandidates;
        workforce = world.Workforce;
        gameClock = simulation.GameClock;
        research = simulation.ProgressionRuntimes
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(CropPlotRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        gameDataProvider = simulation.GameDataProvider;
        environmentQuery = simulation.EnvironmentQuery;
        grandProjectBenefits = simulation.GrandProjectBenefits;
        events = simulation.Events;
        extremeTraits = simulation.ExtremeTraits;
        runSeedProvider = simulation.RunSeedProvider;
        identityEvents = simulation.IdentityEvents;
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.milestoneModifiers = milestoneModifiers
            ?? NeutralMilestoneGameplayModifierQuery.Instance;
    }

    public int Version
    {
        get => aggregateState.Version;
        private set => aggregateState.Version = value;
    }

    public IReadOnlyList<CropPlotSnapshot> Plots
    {
        get
        {
            RefreshSnapshots();
            return snapshots;
        }
    }

    public void CopyVisualStates(List<CropPlotVisualState> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        foreach (CropPlotState state in states.Values)
        {
            if (state?.Building == null
                || state.Building.isDestroy
                || !catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
            {
                continue;
            }

            destination.Add(new CropPlotVisualState(
                state.PlotId.Value,
                state.Building,
                state.CropId,
                state.Phase,
                crop.GrowthHours <= 0f
                    ? 0f
                    : state.GrowthHours / crop.GrowthHours));
        }
    }

    public void Initialize()
    {
        SynchronizePlots(force: true);
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);
    }

    public void Tick()
    {
        SynchronizePlots(force: false);
        bool requestMaterials = gameClock.Time >= nextMaterialRequestTime;
        if (requestMaterials)
        {
            nextMaterialRequestTime =
                gameClock.Time + MaterialRequestInterval;
        }

        List<BuildingInstanceId> destroyed = new();
        foreach (CropPlotState state in states.Values.ToArray())
        {
            if (state.Building == null || state.Building.isDestroy)
            {
                if (TryFinalizeDestroyedPlot(state))
                    destroyed.Add(state.PlotId);
                continue;
            }
            TickState(state, requestMaterials);
        }
        foreach (BuildingInstanceId plotId in destroyed)
            RemoveFinalizedDestroyedPlot(plotId);
    }

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
        foreach (CropPlotState state in states.Values)
        {
            ReleaseMaterialDestination(state);
            ReleaseTreatmentDestination(state);
        }

        states.Clear();
        statesByBuilding.Clear();
        snapshots.Clear();
    }

    public bool TrySetCrop(
        BuildableObject plot,
        string cropId,
        out string message)
    {
        message = string.Empty;
        if (!TryGetState(plot, out CropPlotState state))
        {
            message = "경작지가 아닙니다.";
            return false;
        }

        if (state.MaterialsConsumed
            || state.PendingSow.phase != CropPhysicalCommitPhase.None
            || state.Treatment.phase != CropTreatmentOrderPhase.None
            || state.Phase is CropPlotPhase.Sowing
                or CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting)
        {
            message = "현재 재배 주기가 끝난 뒤 작물을 바꿀 수 있습니다.";
            return false;
        }

        if (!catalog.TryGetCrop(cropId, out CropDefinitionSO crop))
        {
            message = "알 수 없는 작물입니다.";
            return false;
        }

        if (state.Ability.Indoor && !crop.IndoorAllowed)
        {
            message = "이 작물은 실내에서 재배할 수 없습니다.";
            return false;
        }

        if (!IsResearchUnlocked(crop, out message))
        {
            return false;
        }

        ReleaseMaterialDestination(state);
        state.CropId = crop.CropId;
        state.MaterialDestinationId = BuildDestinationId(state.PlotId);
        state.Phase = CropPlotPhase.Empty;
        state.SowWork = 0f;
        state.GrowthHours = 0f;
        state.HarvestWork = 0f;
        state.MaterialsConsumed = false;
        state.BlockedReason = string.Empty;
        MarkChanged();
        message = $"{crop.DisplayName} 재배를 지정했습니다.";
        return true;
    }

    public bool CanScheduleTreatment(
        BuildableObject plot,
        string treatmentItemId,
        out string reason)
    {
        reason = string.Empty;
        if (!TryGetState(plot, out CropPlotState state)
            || state.Building == null
            || state.Building.isDestroy)
        {
            reason = "경작지가 아닙니다.";
            return false;
        }
        if (state.Treatment.phase != CropTreatmentOrderPhase.None)
        {
            reason = "이미 처리 작업이 예약되어 있습니다.";
            return false;
        }
        if (state.Phase is not (CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest))
        {
            reason = "성장 중이거나 수확 대기 중인 작물만 처리할 수 있습니다.";
            return false;
        }
        if (!TryResolveTreatment(
                treatmentItemId,
                out _,
                out CropTreatmentPolicy policy,
                out reason))
            return false;

        CropEcologyPlotSaveData ecologyPlot = ecology.Plots.SingleOrDefault(value =>
            value != null
            && string.Equals(value.plotId, state.PlotId.Value, StringComparison.Ordinal));
        if (ecologyPlot == null || ecologyPlot.cropDead)
        {
            reason = "처리할 살아 있는 작물 생태 상태가 없습니다.";
            return false;
        }
        float pressure = policy.Kind == CropTreatmentKind.Fungicide
            ? ecologyPlot.diseasePressure
            : ecologyPlot.pestPressure;
        if (pressure <= 0f)
        {
            reason = policy.Kind == CropTreatmentKind.Fungicide
                ? "병압이 없어 살균제가 필요하지 않습니다."
                : "해충 압력이 없어 방제가 필요하지 않습니다.";
            return false;
        }
        int currentDay = CurrentAbsoluteDay;
        int nextAllowed = GetNextTreatmentAllowedDay(state, policy.Kind);
        if (currentDay < nextAllowed)
        {
            reason = $"재처리 가능일까지 {nextAllowed - currentDay}일 남았습니다.";
            return false;
        }
        return true;
    }

    [GameplayEntryPoint(
        "CropPlotBuildingPanelPresenter treatment button; crop Treat work planner")]
    public bool TryScheduleTreatment(
        BuildableObject plot,
        string treatmentItemId,
        out string message)
    {
        if (!CanScheduleTreatment(plot, treatmentItemId, out message)
            || !TryGetState(plot, out CropPlotState state)
            || !TryResolveTreatment(
                treatmentItemId,
                out ResourceItemDefinitionSO item,
                out CropTreatmentPolicy policy,
                out message))
            return false;

        int sequence = state.NextTreatmentOperationSequence;
        state.Treatment = new CropTreatmentOrderSaveData
        {
            phase = CropTreatmentOrderPhase.WaitingForDelivery,
            operationSequence = sequence,
            operationId = CropTreatmentPhysicalOutbox.FormatOperationId(
                state.PlotId.Value,
                sequence),
            reasonCode = CropTreatmentPhysicalOutbox.ReasonCode,
            destinationId = BuildTreatmentDestinationId(state.PlotId),
            itemId = item.ItemId,
            treatmentKind = policy.Kind,
            quantity = policy.QuantityPerApplication,
            requiredWork = policy.RequiredWork,
            completedWork = 0f,
            effectAmount = policy.EffectAmount,
            cooldownDays = policy.CooldownDays,
            scheduledAbsoluteDay = CurrentAbsoluteDay
        };
        state.BlockedReason = string.Empty;
        MarkChanged();
        message = $"{item.DisplayName} 처리를 예약했습니다.";
        return true;
    }

    [GameplayEntryPoint(
        "CropPlotBuildingPanelPresenter treatment cancellation button")]
    public bool TryCancelTreatment(
        BuildableObject plot,
        out string message)
    {
        message = string.Empty;
        if (!TryGetState(plot, out CropPlotState state)
            || state.Treatment.phase == CropTreatmentOrderPhase.None)
        {
            message = "취소할 처리 작업이 없습니다.";
            return false;
        }
        if (state.Treatment.phase is CropTreatmentOrderPhase.InputCommitted
                or CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.PlotDestroyedLossPending)
        {
            message = "이미 물리 소비가 확정된 처리는 취소할 수 없습니다.";
            return false;
        }

        ReleaseTreatmentDestination(state);
        CropTreatmentPhysicalOutbox.Clear(state.Treatment);
        state.BlockedReason = string.Empty;
        MarkChanged();
        message = "처리 작업을 취소하고 배송된 자재를 현장에 반환했습니다.";
        return true;
    }

    public bool TryGetWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        out CropPlotWorkSnapshot snapshot)
    {
        snapshot = default;
        if (!TryGetState(plot, out CropPlotState state)
            || !catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
        {
            return false;
        }

        if (workTypeId == BuiltInWorkTypeIds.Sow)
        {
            bool available = state.Phase is CropPlotPhase.ReadyToSow
                or CropPlotPhase.Sowing;
            snapshot = new CropPlotWorkSnapshot(
                state.PlotId.Value,
                workTypeId,
                $"{crop.DisplayName} 파종",
                crop.SowWork,
                state.SowWork,
                available,
                available ? string.Empty : ResolveUnavailableReason(state));
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Harvest)
        {
            bool phaseAvailable = state.Phase is CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting;
            bool treatmentClear = state.Treatment.phase
                == CropTreatmentOrderPhase.None;
            DomainFailure outputFailure = DomainFailure.None;
            bool outputAvailable = phaseAvailable
                && treatmentClear
                && items.CanSpawnOutput(
                    crop.HarvestItemId,
                    Mathf.Max(1, Mathf.CeilToInt(crop.Yield)),
                    state.Building.centerPos,
                    out outputFailure)
                && seedLots.CanSpawnSeedLot(
                    crop.SeedItemId,
                    1,
                    state.Building.centerPos,
                    out outputFailure);
            bool available = phaseAvailable && treatmentClear && outputAvailable;
            snapshot = new CropPlotWorkSnapshot(
                state.PlotId.Value,
                workTypeId,
                $"{crop.DisplayName} 수확",
                crop.HarvestWork,
                state.HarvestWork,
                available,
                available
                    ? string.Empty
                    : phaseAvailable && !treatmentClear
                        ? "예약된 작물 처리 작업을 먼저 완료하거나 취소해야 합니다."
                    : phaseAvailable
                        ? outputFailure.Code.ToString()
                        : ResolveUnavailableReason(state));
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Treat
            && state.Treatment.phase != CropTreatmentOrderPhase.None)
        {
            bool available = state.Treatment.phase is
                    CropTreatmentOrderPhase.ReadyForWork
                    or CropTreatmentOrderPhase.Working
                && IsTreatmentStillRequired(state);
            snapshot = new CropPlotWorkSnapshot(
                state.PlotId.Value,
                workTypeId,
                ResolveTreatmentDisplayName(state.Treatment.itemId) + " 살포",
                state.Treatment.requiredWork,
                state.Treatment.completedWork,
                available,
                available
                    ? string.Empty
                    : ResolveTreatmentUnavailableReason(state));
            return true;
        }

        return false;
    }

    public bool ApplyWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        float amount,
        out bool cycleCompleted) =>
        ApplyWork(plot, workTypeId, amount, null, out cycleCompleted);

    public bool ApplyWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        float amount,
        CharacterActor worker,
        out bool cycleCompleted)
    {
        cycleCompleted = false;
        if (amount <= 0f
            || !TryGetState(plot, out CropPlotState state)
            || !catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
        {
            return false;
        }

        if (workTypeId == BuiltInWorkTypeIds.Sow
            && state.Phase is CropPlotPhase.ReadyToSow
                or CropPlotPhase.Sowing)
        {
            state.Phase = CropPlotPhase.Sowing;
            state.SowWork = Mathf.Min(
                crop.SowWork,
                state.SowWork + amount);
            if (state.SowWork + 0.001f >= crop.SowWork)
            {
                state.SowWork = crop.SowWork;
                state.Phase = CropPlotPhase.Growing;
                cycleCompleted = true;
            }

            MarkChanged();
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Harvest
            && state.Phase is CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting)
        {
            if (!items.CanSpawnOutput(
                    crop.HarvestItemId,
                    Mathf.Max(1, Mathf.CeilToInt(crop.Yield)),
                    state.Building.centerPos,
                    out _)
                || !seedLots.CanSpawnSeedLot(
                    crop.SeedItemId,
                    1,
                    state.Building.centerPos,
                    out _))
            {
                return false;
            }
            state.Phase = CropPlotPhase.Harvesting;
            state.HarvestWork = Mathf.Min(
                crop.HarvestWork,
                state.HarvestWork + amount);
            if (state.HarvestWork + 0.001f >= crop.HarvestWork)
            {
                float extremeYieldMultiplier = 1f;
                float extremeSeedMultiplier = 1f;
                string[] yieldConditions = Array.Empty<string>();
                ExtremeRiskResolution extremeResolution = default;
                bool extremeResolved = extremeTraits != null
                    && runSeedProvider != null
                    && worker != null
                    && string.Equals(
                        state.GoldenHarvestHarvesterId,
                        worker.Identity?.PersistentId,
                        StringComparison.Ordinal)
                    && extremeTraits.TryResolveGoldenHarvest(
                        worker,
                        state.PlotId.Value,
                        unchecked((ulong)(uint)runSeedProvider.RunSeed),
                        gameClock.Time,
                        out extremeResolution);
                if (extremeResolved)
                {
                    if (extremeResolution.Outcome == ExtremeRiskOutcome.Jackpot)
                    {
                        yieldConditions = new[] { "state:golden-harvest-jackpot" };
                        extremeSeedMultiplier = worker.GetDetailedStatMultiplier(
                            "harvest:seed-yield",
                            yieldConditions);
                    }
                    else
                    {
                        extremeYieldMultiplier = extremeResolution.PrimaryMultiplier;
                        extremeSeedMultiplier = extremeResolution.SecondaryMultiplier;
                    }
                }
                CropHarvestEcologyResult ecologyResult = ecology.Harvest(state.PlotId.Value);
                float workerYieldMultiplier = 1f;
                if (worker != null)
                {
                    if (performance == null)
                        throw new InvalidOperationException(
                            "Harvest yield requires the character performance query.");
                    CharacterPerformanceSnapshot yield = performance.Evaluate(
                        worker,
                        "performance:work:harvest:yield",
                        new CharacterPerformanceEvaluationContext
                        {
                            GameplayEffectContext = new GameplayEffectContext(
                                yieldConditions)
                        });
                    if (!yield.IsApplicable)
                        throw new InvalidOperationException(
                            yield.Failure?.Message ?? "Harvest yield is unavailable.");
                    workerYieldMultiplier = yield.Value;
                }
                float outputMultiplier = state.Ability != null
                    && state.Ability.Indoor
                        ? grandProjectBenefits.GetProductionOutputMultiplier(
                            "crop-indoor")
                        : 1f;
                int harvestAmount = Mathf.Max(
                    1,
                    Mathf.RoundToInt(crop.Yield * outputMultiplier
                        * workerYieldMultiplier
                        * extremeYieldMultiplier
                        * ecologyResult.YieldMultiplier
                        * (IsOperational(
                            ResearchFacilityCommandKind.SoilDiagnostics)
                                ? 1.05f
                                : 1f)));
                if (!items.SpawnOutput(
                        crop.HarvestItemId,
                        harvestAmount,
                        state.Building.centerPos))
                {
                    throw new InvalidOperationException(
                        $"Crop '{crop.CropId}' failed to materialize "
                        + $"{harvestAmount}x '{crop.HarvestItemId}' after output-capacity admission.");
                }
                if (!seedLots.SpawnSeedLot(
                        crop.SeedItemId,
                        Mathf.Max(0, Mathf.RoundToInt(
                            ecologyResult.ReturnedSeedCount * extremeSeedMultiplier))
                            + (IsOperational(
                                ResearchFacilityCommandKind.SeedSelection)
                                    ? 1
                                    : 0),
                        ecologyResult.ReturnedSeedLot,
                        state.Building.centerPos))
                    throw new InvalidOperationException(
                        $"Crop '{crop.CropId}' failed to materialize its harvested seed lot.");
                ResetForNextCycle(state);
                cycleCompleted = true;
                PublishHarvestCompleted(worker, state.PlotId.Value, extremeResolved
                    ? extremeResolution.Outcome.ToString().ToLowerInvariant()
                    : "normal");
            }

            MarkChanged();
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Treat
            && state.Treatment.phase is CropTreatmentOrderPhase.ReadyForWork
                or CropTreatmentOrderPhase.Working)
        {
            state.Treatment.phase = CropTreatmentOrderPhase.Working;
            state.Treatment.completedWork = Mathf.Min(
                state.Treatment.requiredWork,
                state.Treatment.completedWork + amount);
            if (state.Treatment.completedWork + 0.001f
                >= state.Treatment.requiredWork)
            {
                state.Treatment.completedWork = state.Treatment.requiredWork;
                bool outcomePublished = TryFinalizeTreatment(state);
                cycleCompleted = outcomePublished;
                MarkChanged();
                return outcomePublished;
            }

            MarkChanged();
            return true;
        }

        return false;
    }

    [GameplayEntryPoint(
        "CropPlotBuildingPanelPresenter golden-harvest button; V26 extreme-trait focused audit")]
    public bool TryScheduleGoldenHarvest(
        BuildableObject plot,
        CharacterActor harvester,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (harvester == null
            || !TryGetState(plot, out CropPlotState state)
            || state.Phase is not (CropPlotPhase.ReadyToHarvest or CropPlotPhase.Harvesting))
        {
            failureReason = "수확 가능한 경작지와 작업자가 필요합니다.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(state.GoldenHarvestHarvesterId))
        {
            failureReason = "이미 황금 수확 작업자가 지정되어 있습니다.";
            return false;
        }
        string harvesterId = harvester.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        if (harvesterId.Length == 0)
        {
            failureReason = "저장 가능한 작업자 ID가 필요합니다.";
            return false;
        }
        if (extremeTraits == null
            || !extremeTraits.TryScheduleGoldenHarvest(
                harvester,
                state.PlotId.Value,
                state.GoldenHarvestAttemptSequence,
                gameClock.Time))
        {
            failureReason = "황금 수확을 예약할 수 없습니다.";
            return false;
        }
        state.GoldenHarvestHarvesterId = harvesterId;
        state.GoldenHarvestAttemptSequence = checked(
            state.GoldenHarvestAttemptSequence + 1);
        MarkChanged();
        failureReason = $"{harvester.Identity.DisplayName}에게 황금 수확을 예약했습니다.";
        return true;
    }

    public bool IsGoldenHarvestWorkerEligible(
        BuildableObject plot,
        CharacterActor harvester,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryGetState(plot, out CropPlotState state)
            || string.IsNullOrWhiteSpace(state.GoldenHarvestHarvesterId))
            return true;
        string harvesterId = harvester?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        if (string.Equals(
                state.GoldenHarvestHarvesterId,
                harvesterId,
                StringComparison.Ordinal))
            return true;
        failureReason = $"황금 수확 담당자 {state.GoldenHarvestHarvesterId} 전용 경작지";
        return false;
    }

    public bool TryGetGoldenHarvestDelay(
        BuildableObject plot,
        CharacterActor harvester,
        out float remainingSeconds)
    {
        remainingSeconds = 0f;
        return harvester != null
            && TryGetState(plot, out CropPlotState state)
            && extremeTraits != null
            && extremeTraits.TryGetGoldenHarvestDelay(
                harvester,
                state.PlotId.Value,
                gameClock.Time,
                out remainingSeconds);
    }

    private void PublishHarvestCompleted(
        CharacterActor worker,
        string plotId,
        string outcomeId)
    {
        if (identityEvents == null
            || worker == null
            || !CharacterPersistentIdentity.TryGet(worker, out CharacterId id))
            return;
        identityEvents.Publish(new WorkCompletedIdentityEvent(
            id,
            "work:harvest",
            $"{plotId}:{outcomeId}",
            CharacterCommandOrigin.Autonomous,
            Mathf.Max(0, Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay))));
    }

    public DungeonCropPlotSaveData Capture()
    {
        DungeonCropPlotSaveData data = new DungeonCropPlotSaveData();
        foreach (CropPlotState state in states.Values
                     .OrderBy(entry => entry.PlotId.Value, StringComparer.Ordinal))
        {
            data.plots.Add(new CropPlotSaveData
            {
                buildingInstanceId = state.PlotId.Value,
                lastKnownGridX = state.LastKnownPosition.x,
                lastKnownGridY = state.LastKnownPosition.y,
                cropId = state.CropId,
                phase = state.Phase,
                sowWork = state.SowWork,
                growthHours = state.GrowthHours,
                harvestWork = state.HarvestWork,
                materialsConsumed = state.MaterialsConsumed,
                goldenHarvestHarvesterId = state.GoldenHarvestHarvesterId,
                goldenHarvestAttemptSequence = state.GoldenHarvestAttemptSequence,
                nextSowOperationSequence = state.NextSowOperationSequence,
                pendingSow = state.PendingSow.DeepClone(),
                nextTreatmentOperationSequence =
                    state.NextTreatmentOperationSequence,
                pestLureNextAllowedDay = state.PestLureNextAllowedDay,
                botanicalPesticideNextAllowedDay =
                    state.BotanicalPesticideNextAllowedDay,
                fungicideNextAllowedDay = state.FungicideNextAllowedDay,
                treatment = state.Treatment.DeepClone()
            });
        }

        return data;
    }

    public CropPlotRestoreCandidate BuildRestore(
        DungeonCropPlotSaveData snapshot)
    {
        RequireSaveRoot(snapshot);
        CropPlotAggregateState restored = new()
        {
            ObservedBuildingVersion = -1,
            NextMaterialRequestTime = gameClock.Time,
            SnapshotsDirty = true,
            Version = aggregateState.Version + 1
        };
        HashSet<BuildingInstanceId> seen = new();
        foreach (CropPlotSaveData saved in snapshot.plots)
        {
            BuildingInstanceId plotId = RequireRestorePlotId(saved, seen);
            CropDefinitionSO crop = RequireCrop(saved);
            ValidateRestoreProgress(saved, crop, plotId);
            restored.States.Add(plotId, new CropPlotState
            {
                PlotId = plotId,
                LastKnownPosition = new Vector2Int(
                    saved.lastKnownGridX,
                    saved.lastKnownGridY),
                CropId = crop.CropId,
                Phase = saved.phase,
                SowWork = saved.sowWork,
                GrowthHours = saved.growthHours,
                HarvestWork = saved.harvestWork,
                MaterialDestinationId = BuildDestinationId(plotId),
                MaterialsConsumed = saved.materialsConsumed,
                GoldenHarvestHarvesterId = saved.goldenHarvestHarvesterId?.Trim()
                    ?? string.Empty,
                GoldenHarvestAttemptSequence = Math.Max(
                    0,
                    saved.goldenHarvestAttemptSequence),
                NextSowOperationSequence = saved.nextSowOperationSequence,
                PendingSow = saved.pendingSow.DeepClone(),
                NextTreatmentOperationSequence =
                    saved.nextTreatmentOperationSequence,
                PestLureNextAllowedDay = saved.pestLureNextAllowedDay,
                BotanicalPesticideNextAllowedDay =
                    saved.botanicalPesticideNextAllowedDay,
                FungicideNextAllowedDay = saved.fungicideNextAllowedDay,
                Treatment = saved.treatment.DeepClone(),
                BlockedReason = string.Empty
            });
        }

        return new CropPlotRestoreCandidate(restored);
    }

    public void Restore(CropPlotRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
    }

    private void TickState(CropPlotState state, bool requestMaterials)
    {
        if (state == null)
        {
            return;
        }

        if (state.PendingSow.phase == CropPhysicalCommitPhase.OutcomePublished)
        {
            FinalizePublishedSow(state);
            if (state.PendingSow.phase != CropPhysicalCommitPhase.None)
                return;
        }

        if (state.Treatment.phase is CropTreatmentOrderPhase.Working
                or CropTreatmentOrderPhase.InputCommitted
                or CropTreatmentOrderPhase.OutcomePublished
            && (state.Treatment.phase != CropTreatmentOrderPhase.Working
                || state.Treatment.completedWork + 0.001f
                    >= state.Treatment.requiredWork))
        {
            TryFinalizeTreatment(state);
        }

        if (state.Building == null
            || state.Building.isDestroy
            || !catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
        {
            return;
        }

        if (!IsResearchUnlocked(crop, out string researchReason))
        {
            SetBlocked(state, researchReason);
            return;
        }

        if (state.Phase == CropPlotPhase.Blocked
            && state.BlockedReason.StartsWith("연구 필요", StringComparison.Ordinal))
        {
            state.Phase = CropPlotPhase.Empty;
            state.BlockedReason = string.Empty;
            MarkChanged();
        }

        TickTreatmentDelivery(state, requestMaterials);

        if (state.Phase is CropPlotPhase.Empty
            or CropPlotPhase.WaitingForMaterials)
        {
            EnsureSowingMaterials(state, crop, requestMaterials);
            return;
        }

        if (state.Phase != CropPlotPhase.Growing)
        {
            return;
        }

        float multiplier = ResolveGrowthMultiplier(state, crop, out string blockedReason);
        if (multiplier <= 0f)
        {
            if (!string.Equals(state.BlockedReason, blockedReason, StringComparison.Ordinal))
            {
                state.BlockedReason = blockedReason;
                MarkChanged(replan: false);
            }
            return;
        }

        state.BlockedReason = string.Empty;
        float gameHours = gameClock.DeltaTime / SecondsPerGameHour;
        state.GrowthHours = Mathf.Min(
            crop.GrowthHours,
            state.GrowthHours + gameHours * multiplier);
        snapshotsDirty = true;
        if (state.GrowthHours + 0.001f >= crop.GrowthHours)
        {
            state.GrowthHours = crop.GrowthHours;
            state.Phase = CropPlotPhase.ReadyToHarvest;
            MarkChanged();
        }
    }

    private void EnsureSowingMaterials(
        CropPlotState state,
        CropDefinitionSO crop,
        bool requestMaterials)
    {
        Dictionary<string, int> requirements = BuildMaterialRequirements(state, crop);
        if (requirements.Count == 0)
        {
            state.MaterialsConsumed = true;
            state.Phase = CropPlotPhase.ReadyToSow;
            MarkChanged();
            return;
        }

        if (!state.MaterialsConsumed
            && (state.PendingSow.phase == CropPhysicalCommitPhase.InputCommitted
                || HasDelivered(requirements, state.MaterialDestinationId)))
        {
            string operationId = CropPhysicalTransactionOutbox.FormatSowOperationId(
                state.PlotId.Value,
                state.NextSowOperationSequence);
            bool starting = state.PendingSow.phase == CropPhysicalCommitPhase.None;
            if (CropPhysicalTransactionOutbox.TryCommitOrResume(
                    state.PendingSow,
                    operationId,
                    CropPhysicalTransactionOutbox.SowReasonCode,
                    state.NextSowOperationSequence,
                    state.MaterialDestinationId,
                    requirements,
                    crop.SeedItemId,
                    crop.CropId,
                    seedLots,
                    out SeedLotState seedLot,
                    out string failureReason))
            {
                string ecologyBefore = CreateEcologyFingerprint(state.PlotId.Value);
                if (starting)
                {
                    state.PendingSow.ecologyBeforeFingerprint = ecologyBefore;
                }
                else if (!string.Equals(
                             state.PendingSow.ecologyBeforeFingerprint,
                             ecologyBefore,
                             StringComparison.Ordinal))
                {
                    SetBlocked(state, "crop-sow-ecology-before-conflict");
                    return;
                }
                ecology.Sow(state.PlotId.Value, crop.FamilyGroup, seedLot);
                if (state.Ability.CompostPerCycle > 0)
                    ecology.ApplyCompost(state.PlotId.Value);
                state.PendingSow.ecologyAfterFingerprint =
                    CreateEcologyFingerprint(state.PlotId.Value);
                state.PendingSow.phase = CropPhysicalCommitPhase.OutcomePublished;
                state.MaterialsConsumed = true;
                state.Phase = CropPlotPhase.ReadyToSow;
                state.BlockedReason = string.Empty;
                MarkChanged();
                FinalizePublishedSow(state);
                return;
            }

            SetBlocked(state, failureReason);
            return;
        }

        if (!requestMaterials)
        {
            state.Phase = CropPlotPhase.WaitingForMaterials;
            snapshotsDirty = true;
            return;
        }

        bool requestedAny = false;
        foreach (KeyValuePair<string, int> requirement in requirements)
        {
            int pending = items.CountPending(
                requirement.Key,
                state.MaterialDestinationId);
            int missing = Mathf.Max(0, requirement.Value - pending);
            if (missing <= 0)
            {
                continue;
            }

            int requested;
            if (string.Equals(requirement.Key, crop.SeedItemId, StringComparison.Ordinal))
            {
                seedLots.RequestBestSeedLot(
                    crop.SeedItemId,
                    crop.CropId,
                    state.Building.centerPos,
                    state.MaterialDestinationId,
                    out requested,
                    out _);
            }
            else
            {
                items.RequestDelivery(
                    requirement.Key,
                    missing,
                    state.Building.centerPos,
                    state.MaterialDestinationId,
                    out requested,
                    out _);
            }
            requestedAny |= requested > 0;
        }

        state.Phase = CropPlotPhase.WaitingForMaterials;
        state.BlockedReason = BuildMaterialWaitReason(
            requirements,
            state.MaterialDestinationId);
        snapshotsDirty = true;
        if (requestedAny)
        {
            items.PrioritizeDestination(state.MaterialDestinationId);
            workforce.RequestOneHaulerToReplan(forceInterrupt: false);
            MarkChanged(replan: false);
        }
    }

    private void FinalizePublishedSow(CropPlotState state)
    {
        if (state?.PendingSow == null
            || state.PendingSow.phase != CropPhysicalCommitPhase.OutcomePublished)
            return;
        string current = CreateEcologyFingerprint(state.PlotId.Value);
        if (!string.Equals(
                current,
                state.PendingSow.ecologyAfterFingerprint,
                StringComparison.Ordinal))
        {
            SetBlocked(state, "crop-sow-ecology-after-conflict");
            return;
        }
        if (!CropPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                state.PendingSow,
                seedLots,
                out string failureReason))
        {
            state.BlockedReason = failureReason;
            snapshotsDirty = true;
            return;
        }
        CropPhysicalTransactionOutbox.Clear(state.PendingSow);
        state.NextSowOperationSequence = checked(
            state.NextSowOperationSequence + 1);
        state.BlockedReason = string.Empty;
        MarkChanged();
    }

    private void TickTreatmentDelivery(
        CropPlotState state,
        bool requestMaterials)
    {
        CropTreatmentOrderSaveData treatment = state?.Treatment;
        if (treatment == null
            || treatment.phase is CropTreatmentOrderPhase.None
                or CropTreatmentOrderPhase.InputCommitted
                or CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.PlotDestroyedLossPending)
            return;

        int delivered = items.CountDelivered(
            treatment.itemId,
            treatment.destinationId);
        if (delivered >= treatment.quantity)
        {
            if (treatment.phase == CropTreatmentOrderPhase.WaitingForDelivery)
            {
                treatment.phase = CropTreatmentOrderPhase.ReadyForWork;
                treatment.failureReason = string.Empty;
                MarkChanged();
            }
            return;
        }

        treatment.phase = CropTreatmentOrderPhase.WaitingForDelivery;
        treatment.failureReason = $"처리제 운반 대기 {delivered}/{treatment.quantity}";
        snapshotsDirty = true;
        if (!requestMaterials)
            return;

        int pending = items.CountPending(
            treatment.itemId,
            treatment.destinationId);
        int missing = Mathf.Max(0, treatment.quantity - pending);
        if (missing <= 0)
            return;
        if (items.RequestDelivery(
                treatment.itemId,
                missing,
                state.Building.centerPos,
                treatment.destinationId,
                out int requested,
                out string failureReason)
            && requested > 0)
        {
            items.PrioritizeDestination(treatment.destinationId);
            workforce.RequestOneHaulerToReplan(forceInterrupt: false);
            treatment.failureReason = string.Empty;
            MarkChanged(replan: false);
            return;
        }
        treatment.failureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "사용 가능한 처리제가 없습니다."
            : failureReason;
    }

    private bool TryFinalizeTreatment(CropPlotState state)
    {
        CropTreatmentOrderSaveData treatment = state?.Treatment;
        if (treatment == null
            || treatment.phase == CropTreatmentOrderPhase.None)
            return false;

        bool outcomePublishedNow = false;
        if (treatment.phase == CropTreatmentOrderPhase.Working)
        {
            string before = CreateEcologyFingerprint(state.PlotId.Value);
            if (string.IsNullOrEmpty(treatment.ecologyBeforeFingerprint))
                treatment.ecologyBeforeFingerprint = before;
            else if (!string.Equals(
                         treatment.ecologyBeforeFingerprint,
                         before,
                         StringComparison.Ordinal))
            {
                treatment.failureReason = "crop-treatment-ecology-before-conflict";
                snapshotsDirty = true;
                return false;
            }
            if (!CropTreatmentPhysicalOutbox.TryCommitOrResume(
                    treatment,
                    treatmentItems,
                    out string commitFailure))
            {
                treatment.failureReason = commitFailure;
                snapshotsDirty = true;
                return false;
            }
        }

        if (treatment.phase == CropTreatmentOrderPhase.InputCommitted)
        {
            string current = CreateEcologyFingerprint(state.PlotId.Value);
            if (!string.Equals(
                    treatment.ecologyBeforeFingerprint,
                    current,
                    StringComparison.Ordinal))
            {
                treatment.failureReason = "crop-treatment-ecology-before-conflict";
                snapshotsDirty = true;
                return false;
            }
            Vector2Int position = state.Building != null
                ? state.Building.centerPos
                : state.LastKnownPosition;
            if (!CropTreatmentPhysicalOutbox.EnsureTareOutputs(
                    treatment,
                    position,
                    packagedTare,
                    out string tareFailure))
            {
                treatment.failureReason = tareFailure;
                snapshotsDirty = true;
                return false;
            }

            switch (treatment.treatmentKind)
            {
                case CropTreatmentKind.PestLure:
                case CropTreatmentKind.BotanicalPesticide:
                    ecology.ApplyPestControl(
                        state.PlotId.Value,
                        treatment.effectAmount);
                    break;
                case CropTreatmentKind.Fungicide:
                    ecology.ApplyFungicide(
                        state.PlotId.Value,
                        treatment.effectAmount);
                    break;
                default:
                    treatment.failureReason = "crop-treatment-kind-invalid";
                    return false;
            }
            treatment.ecologyAfterFingerprint =
                CreateEcologyFingerprint(state.PlotId.Value);
            SetNextTreatmentAllowedDay(
                state,
                treatment.treatmentKind,
                checked(CurrentAbsoluteDay + treatment.cooldownDays));
            treatment.phase = CropTreatmentOrderPhase.OutcomePublished;
            treatment.failureReason = string.Empty;
            outcomePublishedNow = true;
            MarkChanged();
        }

        if (treatment.phase != CropTreatmentOrderPhase.OutcomePublished)
            return false;
        if (!CropTreatmentPhysicalOutbox.TryAcknowledgeOutcome(
                treatment,
                treatmentItems,
                out string acknowledgeFailure))
        {
            treatment.failureReason = acknowledgeFailure;
            snapshotsDirty = true;
            return outcomePublishedNow;
        }

        ReleaseTreatmentDestination(state);
        state.NextTreatmentOperationSequence = checked(
            state.NextTreatmentOperationSequence + 1);
        CropTreatmentPhysicalOutbox.Clear(treatment);
        MarkChanged();
        return true;
    }

    private string CreateEcologyFingerprint(string plotId)
        => CropPhysicalTransactionOutbox.CreateEcologyFingerprint(
            ecology.Plots,
            plotId);

    private int CurrentAbsoluteDay => Mathf.Max(
        0,
        Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));

    private bool TryResolveTreatment(
        string treatmentItemId,
        out ResourceItemDefinitionSO item,
        out CropTreatmentPolicy policy,
        out string failureReason)
    {
        item = null;
        policy = default;
        failureReason = string.Empty;
        string itemId = treatmentItemId ?? string.Empty;
        if (itemId.Length == 0
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
            || !catalog.TryGetItem(itemId, out item)
            || item == null
            || !item.TryGetCropTreatment(out policy)
            || !policy.IsValid)
        {
            failureReason = "알 수 없거나 잘못 작성된 작물 처리제입니다.";
            return false;
        }
        return true;
    }

    private string ResolveTreatmentDisplayName(string treatmentItemId) =>
        catalog.TryGetItem(
            treatmentItemId,
            out ResourceItemDefinitionSO item)
            ? item.DisplayName
            : treatmentItemId;

    private string ResolveTreatmentUnavailableReason(CropPlotState state)
    {
        CropTreatmentOrderSaveData treatment = state?.Treatment;
        if (treatment == null
            || treatment.phase == CropTreatmentOrderPhase.None)
            return "예약된 작물 처리가 없습니다.";
        if (!string.IsNullOrWhiteSpace(treatment.failureReason))
            return treatment.failureReason;
        CropEcologyPlotSaveData ecologyPlot = ecology.Plots.SingleOrDefault(value =>
            value != null
            && string.Equals(value.plotId, state.PlotId.Value, StringComparison.Ordinal));
        float pressure = treatment.treatmentKind == CropTreatmentKind.Fungicide
            ? ecologyPlot?.diseasePressure ?? 0f
            : ecologyPlot?.pestPressure ?? 0f;
        if (pressure <= 0f
            && treatment.phase is CropTreatmentOrderPhase.WaitingForDelivery
                or CropTreatmentOrderPhase.ReadyForWork
                or CropTreatmentOrderPhase.Working)
            return "처리 필요성이 사라졌습니다. 예약을 취소하십시오.";
        return treatment.phase switch
        {
            CropTreatmentOrderPhase.WaitingForDelivery => "처리제 운반 대기",
            CropTreatmentOrderPhase.InputCommitted => "처리 결과 확정 대기",
            CropTreatmentOrderPhase.OutcomePublished => "물리 소비 승인 대기",
            CropTreatmentOrderPhase.PlotDestroyedLossPending =>
                "파괴된 경작지 처리 손실 승인 대기",
            _ => string.Empty
        };
    }

    private bool IsTreatmentStillRequired(CropPlotState state)
    {
        CropTreatmentOrderSaveData treatment = state?.Treatment;
        CropEcologyPlotSaveData ecologyPlot = ecology.Plots.SingleOrDefault(value =>
            value != null
            && state != null
            && string.Equals(value.plotId, state.PlotId.Value, StringComparison.Ordinal));
        if (treatment == null || ecologyPlot == null || ecologyPlot.cropDead)
            return false;
        return treatment.treatmentKind == CropTreatmentKind.Fungicide
            ? ecologyPlot.diseasePressure > 0f
            : ecologyPlot.pestPressure > 0f;
    }

    private static int GetNextTreatmentAllowedDay(
        CropPlotState state,
        CropTreatmentKind kind) => kind switch
        {
            CropTreatmentKind.PestLure => state.PestLureNextAllowedDay,
            CropTreatmentKind.BotanicalPesticide =>
                state.BotanicalPesticideNextAllowedDay,
            CropTreatmentKind.Fungicide => state.FungicideNextAllowedDay,
            _ => int.MaxValue
        };

    private static void SetNextTreatmentAllowedDay(
        CropPlotState state,
        CropTreatmentKind kind,
        int absoluteDay)
    {
        int day = Math.Max(0, absoluteDay);
        switch (kind)
        {
            case CropTreatmentKind.PestLure:
                state.PestLureNextAllowedDay = day;
                break;
            case CropTreatmentKind.BotanicalPesticide:
                state.BotanicalPesticideNextAllowedDay = day;
                break;
            case CropTreatmentKind.Fungicide:
                state.FungicideNextAllowedDay = day;
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown crop treatment kind cannot own a cooldown.");
        }
    }

    private static string BuildTreatmentDestinationId(BuildingInstanceId plotId) =>
        BuildDestinationId(plotId) + ":treatment";

    private void ReleaseTreatmentDestination(CropPlotState state)
    {
        string destination = state?.Treatment?.destinationId ?? string.Empty;
        if (destination.Length == 0)
            return;
        Vector2Int position = state.Building != null
            ? state.Building.centerPos
            : state.LastKnownPosition;
        items.ReleaseDestination(destination, position);
    }

    private Dictionary<string, int> BuildMaterialRequirements(
        CropPlotState state,
        CropDefinitionSO crop)
    {
        Dictionary<string, int> requirements =
            new Dictionary<string, int>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(crop.SeedItemId)
            || !catalog.TryGetItem(crop.SeedItemId, out _))
            throw new InvalidOperationException(
                $"Crop '{crop.CropId}' requires a missing authored physical seed-lot item.");
        requirements[crop.SeedItemId] = 1;
        float waterRate = state.Ability.WaterMultiplier;
        if (!state.Ability.Indoor
            && environmentQuery.GetEnvironmentSnapshot().Weather
                is SurvivalWeatherType.Rain or SurvivalWeatherType.Storm)
        {
            waterRate *= 0.5f;
        }

        int water = crop.DailyWater <= 0f
            ? 0
            : Mathf.Max(
                1,
                Mathf.CeilToInt(
                    crop.DailyWater
                    * (crop.GrowthHours / 24f)
                    * waterRate
                    * Mathf.Clamp(
                        milestoneModifiers
                            .WaterAndFertilizerConsumptionMultiplier,
                        0.1f,
                        1f)));
        if (water > 0)
        {
            if (!catalog.TryGetItem(CleanWaterItemId, out _))
            {
                throw new InvalidOperationException(
                    $"Crop plot '{state.PlotId}' requires missing authored item '{CleanWaterItemId}'.");
            }

            requirements[CleanWaterItemId] = water;
        }

        if (state.Ability.CompostPerCycle > 0)
        {
            requirements[CompostItemId] = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    state.Ability.CompostPerCycle
                    * Mathf.Clamp(
                        milestoneModifiers
                            .WaterAndFertilizerConsumptionMultiplier,
                        0.1f,
                        1f)));
        }

        if (state.Ability.FuelPerCycle > 0)
        {
            string fuelItemId = ResolveFacilityFuelItemId(
                state.MaterialDestinationId);
            requirements[fuelItemId] = state.Ability.FuelPerCycle;
        }

        foreach (ItemAmountDefinition supply in
                 state.Ability.CycleSupplyInputs)
        {
            requirements.TryGetValue(supply.ItemId, out int current);
            requirements[supply.ItemId] = current + supply.Amount;
        }

        return requirements;
    }

    private string ResolveFacilityFuelItemId(string excludedDestinationId)
    {
        FacilitySupplyProfile fuelProfile = new()
        {
            kind = FacilitySupplyKind.Fuel,
            requiredTags = ResourceIngredientTag.Fuel,
            minimumValue = 0.01f,
        };
        ResourceItemDefinitionSO[] candidates = catalog.Items
            .Where(fuelProfile.Allows)
            .ToArray();
        ResourceItemDefinitionSO[] available = candidates
            .Where(item => items.CountAvailableStock(
                item.ItemId,
                excludedDestinationId) > 0)
            .ToArray();
        ResourceItemDefinitionSO selected = (available.Length > 0
                ? available
                : candidates)
            .OrderBy(item => item.UnitPrice / Mathf.Max(0.01f, item.FuelValue))
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
        return selected?.ItemId
            ?? throw new InvalidOperationException(
                "Crop plot requires an authored fuel-tagged item, but the item catalog has none.");
    }

    private float ResolveGrowthMultiplier(
        CropPlotState state,
        CropDefinitionSO crop,
        out string blockedReason)
    {
        blockedReason = string.Empty;
        if (state.Ability.Indoor)
        {
            return state.Ability.GrowthMultiplier
                * (IsOperational(ResearchFacilityCommandKind.ClimateControl)
                    ? 1.08f
                    : 1f)
                * (IsOperational(ResearchFacilityCommandKind.CropCalendar)
                    ? 1.05f
                    : 1f)
                * ecology.GetPhenotype(state.PlotId.Value).GrowthMultiplier;
        }

        SurvivalEnvironmentSnapshot environment =
            environmentQuery.GetEnvironmentSnapshot();
        CropGenomePhenotype phenotype = ecology.GetPhenotype(state.PlotId.Value);
        Vector2 authoredRange = crop.TemperatureRange;
        Vector2 range = new(
            authoredRange.x - phenotype.ColdToleranceDegrees,
            authoredRange.y + phenotype.HeatToleranceDegrees);
        if (environment.OutdoorTemperature < range.x)
        {
            blockedReason = $"기온이 너무 낮음 ({environment.OutdoorTemperature:0.#}도)";
            return 0f;
        }

        if (environment.OutdoorTemperature > range.y)
        {
            blockedReason = $"기온이 너무 높음 ({environment.OutdoorTemperature:0.#}도)";
            return 0f;
        }

        float weatherMultiplier = environment.Weather switch
        {
            SurvivalWeatherType.Rain => 1.1f,
            SurvivalWeatherType.Fog => 0.85f,
            SurvivalWeatherType.Storm => 0.55f,
            SurvivalWeatherType.HeatWave => 0.9f,
            SurvivalWeatherType.ColdSnap => 0.9f,
            _ => 1f
        };
        float dayMultiplier = 1f;
        if (gameDataProvider.TryGetSessionState(out GameSessionState data)
            && data?.timeOfDay?.Value == TimeOfDay.Night)
        {
            dayMultiplier = 0.55f;
        }

        return state.Ability.GrowthMultiplier
            * weatherMultiplier
            * dayMultiplier
            * (IsOperational(ResearchFacilityCommandKind.CropCalendar)
                ? 1.05f
                : 1f)
            * phenotype.GrowthMultiplier;
    }

    private void SynchronizePlots(bool force)
    {
        if (!force && observedBuildingVersion == buildingWorld.BuildingVersion)
        {
            return;
        }

        observedBuildingVersion = buildingWorld.BuildingVersion;
        HashSet<BuildingInstanceId> seen = new();
        statesByBuilding.Clear();
        foreach (BuildableObject building in buildingWorld.Buildings)
        {
            BuildingCropPlotAbility ability =
                building?.BuildingData?.GetAbility<BuildingCropPlotAbility>();
            if (building == null
                || building.isDestroy
                || ability == null)
            {
                continue;
            }

            BuildingInstanceId plotId = BuildPlotId(building);
            seen.Add(plotId);
            if (!states.TryGetValue(plotId, out CropPlotState state))
            {
                state = CreateState(building, ability, plotId);
                states.Add(plotId, state);
            }
            else
            {
                state.Building = building;
                state.Ability = ability;
                state.LastKnownPosition = building.centerPos;
            }

            statesByBuilding[building] = state;
        }

        foreach (BuildingInstanceId removedId in states.Keys
                     .Where(id => !seen.Contains(id))
                     .ToArray())
        {
            CropPlotState removed = states[removedId];
            ReleaseMaterialDestination(removed);
            if (removed.Treatment.phase is CropTreatmentOrderPhase.WaitingForDelivery
                    or CropTreatmentOrderPhase.ReadyForWork
                    or CropTreatmentOrderPhase.Working)
            {
                ReleaseTreatmentDestination(removed);
                CropTreatmentPhysicalOutbox.Clear(removed.Treatment);
            }
            removed.Building = null;
            removed.Ability = null;
            removed.BlockedReason = removed.PendingSow.phase
                == CropPhysicalCommitPhase.None
                    ? "crop-plot-destroyed"
                    : "crop-plot-destroyed-with-pending-physical-transaction";
        }

        MarkChanged();
    }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        SurvivalEnvironmentSnapshot environment = environmentQuery.GetEnvironmentSnapshot();
        foreach (CropPlotState state in states.Values
                     .Where(value => value != null && value.Phase == CropPlotPhase.Growing)
                     .ToArray())
        {
            if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop)) continue;
            CropGenomePhenotype phenotype = ecology.GetPhenotype(state.PlotId.Value);
            Vector2 authoredRange = crop.TemperatureRange;
            Vector2 range = new(
                authoredRange.x - phenotype.ColdToleranceDegrees,
                authoredRange.y + phenotype.HeatToleranceDegrees);
            bool lethal = !state.Ability.Indoor
                && (environment.OutdoorTemperature < range.x - 5f
                    || environment.OutdoorTemperature > range.y + 5f);
            if (ecology.AdvanceDay(state.PlotId.Value, lethal)) continue;
            state.MaterialsConsumed = false;
            state.Phase = CropPlotPhase.Blocked;
            state.BlockedReason = "작물이 극한 환경 또는 해충으로 고사했습니다.";
            MarkChanged();
        }
    }

    private bool IsOperational(ResearchFacilityCommandKind command) =>
        facilities.FindOperational(command).Count > 0;

    private CropPlotState CreateState(
        BuildableObject building,
        BuildingCropPlotAbility ability,
        BuildingInstanceId plotId)
    {
        CropDefinitionSO crop = ResolveDefaultCrop(ability);
        return new CropPlotState
        {
            PlotId = plotId,
            Building = building,
            Ability = ability,
            LastKnownPosition = building.centerPos,
            CropId = crop?.CropId ?? string.Empty,
            Phase = crop != null
                ? CropPlotPhase.Empty
                : CropPlotPhase.Blocked,
            MaterialDestinationId = BuildDestinationId(plotId),
            BlockedReason = crop != null
                ? string.Empty
                : "연구가 완료된 재배 작물이 없습니다."
        };
    }

    private CropDefinitionSO ResolveDefaultCrop(BuildingCropPlotAbility ability)
    {
        string preferredId = ability.Indoor
            ? "crop:cave-mushroom"
            : "crop:twilight-grain";
        CropDefinitionSO preferred = catalog.Crops
            .FirstOrDefault(crop => crop != null
                && string.Equals(crop.CropId, preferredId, StringComparison.Ordinal)
                && (!ability.Indoor || crop.IndoorAllowed)
                && IsResearchUnlocked(crop, out _));
        return preferred ?? catalog.Crops
            .FirstOrDefault(crop => crop != null
                && (!ability.Indoor || crop.IndoorAllowed)
                && IsResearchUnlocked(crop, out _));
    }

    private bool TryGetState(
        BuildableObject plot,
        out CropPlotState state)
    {
        state = null;
        if (plot == null)
        {
            return false;
        }

        SynchronizePlots(force: false);
        return statesByBuilding.TryGetValue(plot, out state)
            || states.TryGetValue(BuildPlotId(plot), out state);
    }

    private bool IsResearchUnlocked(
        CropDefinitionSO crop,
        out string reason)
    {
        reason = string.Empty;
        if (crop == null || string.IsNullOrWhiteSpace(crop.RequiredResearchId))
        {
            return true;
        }

        if (!research.State.Projects.IsCompleted(
                new ResearchProjectId(crop.RequiredResearchId)))
        {
            reason = $"연구 필요: {crop.RequiredResearchId}";
            return false;
        }

        return true;
    }

    private static void RequireSaveRoot(DungeonCropPlotSaveData snapshot)
    {
        if (snapshot == null)
        {
            throw new InvalidOperationException("Crop-plot payload is null.");
        }
        if (snapshot.version != DungeonCropPlotSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Crop-plot payload version {snapshot.version} is not current V{DungeonCropPlotSaveData.CurrentVersion}.");
        }
        if (snapshot.plots == null || snapshot.plots.Count > 512)
        {
            throw new InvalidOperationException(
                "Crop-plot payload must contain at most 512 non-null plot records.");
        }
    }

    private static BuildingInstanceId RequireRestorePlotId(
        CropPlotSaveData saved,
        ISet<BuildingInstanceId> seen)
    {
        if (saved == null)
        {
            throw new InvalidOperationException(
                "Crop-plot payload contains a null plot record.");
        }

        BuildingInstanceId buildingId =
            (BuildingInstanceId)saved.buildingInstanceId;
        if (!buildingId.IsValid
            || !string.Equals(
                buildingId.Value,
                saved.buildingInstanceId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Crop-plot building instance ID '{saved.buildingInstanceId}' is not canonical.");
        }
        if (!seen.Add(buildingId))
        {
            throw new InvalidOperationException(
                $"Crop-plot building instance ID '{buildingId.Value}' is duplicated.");
        }
        return buildingId;
    }

    private CropDefinitionSO RequireCrop(CropPlotSaveData saved)
    {
        if (string.IsNullOrWhiteSpace(saved.cropId)
            || !string.Equals(saved.cropId, saved.cropId.Trim(), StringComparison.Ordinal)
            || !catalog.TryGetCrop(saved.cropId, out CropDefinitionSO crop))
        {
            throw new InvalidOperationException(
                $"Crop-plot payload references unknown crop '{saved.cropId}'.");
        }
        return crop;
    }

    private void ValidateRestoreProgress(
        CropPlotSaveData saved,
        CropDefinitionSO crop,
        BuildingInstanceId plotId)
    {
        if (!Enum.IsDefined(typeof(CropPlotPhase), saved.phase))
        {
            throw new InvalidOperationException(
                $"Crop-plot payload contains unknown phase {(int)saved.phase}.");
        }
        RequireFiniteRange(saved.sowWork, 0f, crop.SowWork, "sow work");
        RequireFiniteRange(saved.growthHours, 0f, crop.GrowthHours, "growth hours");
        RequireFiniteRange(saved.harvestWork, 0f, crop.HarvestWork, "harvest work");
        if (saved.goldenHarvestAttemptSequence < 0)
            throw new InvalidOperationException(
                "Crop-plot golden-harvest attempt sequence cannot be negative.");
        if (saved.nextSowOperationSequence < 0 || saved.pendingSow == null)
            throw new InvalidOperationException(
                "Crop-plot sow transaction owner is missing or invalid.");
        if (saved.nextTreatmentOperationSequence < 0
            || saved.pestLureNextAllowedDay < 0
            || saved.botanicalPesticideNextAllowedDay < 0
            || saved.fungicideNextAllowedDay < 0
            || saved.treatment == null)
            throw new InvalidOperationException(
                "Crop-plot treatment owner or cooldown is invalid.");
        if (!string.IsNullOrEmpty(saved.goldenHarvestHarvesterId)
            && (!string.Equals(
                    saved.goldenHarvestHarvesterId,
                    saved.goldenHarvestHarvesterId.Trim(),
                    StringComparison.Ordinal)
                || saved.phase is not (CropPlotPhase.ReadyToHarvest
                    or CropPlotPhase.Harvesting)))
            throw new InvalidOperationException(
                "Crop-plot golden-harvest worker requires a canonical ID and harvest phase.");

        bool requiresConsumedMaterials = saved.phase is
            CropPlotPhase.ReadyToSow
            or CropPlotPhase.Sowing
            or CropPlotPhase.Growing
            or CropPlotPhase.ReadyToHarvest
            or CropPlotPhase.Harvesting;
        bool requiresUnconsumedMaterials = saved.phase is
            CropPlotPhase.Empty or CropPlotPhase.WaitingForMaterials;
        if ((requiresConsumedMaterials && !saved.materialsConsumed)
            || (requiresUnconsumedMaterials && saved.materialsConsumed))
        {
            throw new InvalidOperationException(
                $"Crop-plot phase {saved.phase} contradicts materialsConsumed={saved.materialsConsumed}.");
        }

        ValidatePendingSow(saved, crop);
        ValidatePendingTreatment(saved, plotId);

        const float Epsilon = 0.001f;
        if (saved.phase is CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting
            && saved.sowWork + Epsilon < crop.SowWork)
        {
            throw new InvalidOperationException(
                "Crop-plot post-sowing phase has incomplete sow work.");
        }
        if (saved.phase is CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting
            && saved.growthHours + Epsilon < crop.GrowthHours)
        {
            throw new InvalidOperationException(
                "Crop-plot harvest phase has incomplete growth.");
        }
    }

    private void ValidatePendingTreatment(
        CropPlotSaveData saved,
        BuildingInstanceId plotId)
    {
        CropTreatmentOrderSaveData owner = saved.treatment;
        if (owner.phase == CropTreatmentOrderPhase.None)
        {
            bool empty = owner.operationSequence == 0
                && string.IsNullOrEmpty(owner.operationId)
                && string.IsNullOrEmpty(owner.reasonCode)
                && string.IsNullOrEmpty(owner.destinationId)
                && string.IsNullOrEmpty(owner.itemId)
                && owner.quantity == 0
                && owner.requiredWork == 0f
                && owner.completedWork == 0f
                && owner.effectAmount == 0f
                && owner.cooldownDays == 0
                && owner.scheduledAbsoluteDay == 0
                && (owner.sourceStackIds?.Count ?? 0) == 0
                && owner.inputMassGrams == 0L
                && string.IsNullOrEmpty(owner.commitId)
                && string.IsNullOrEmpty(owner.requestFingerprint)
                && owner.tareOutputQuantity == 0
                && owner.tareOutputMassGrams == 0L
                && owner.destroyedTareMassGrams == 0L
                && (owner.tareOutputCommitIds?.Count ?? 0) == 0
                && string.IsNullOrEmpty(owner.ecologyBeforeFingerprint)
                && string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
                && owner.terminalDisposition
                    == CropTreatmentTerminalDisposition.None
                && string.IsNullOrEmpty(owner.terminalReasonCode)
                && owner.terminalLossQuantity == 0
                && owner.terminalLossMassGrams == 0L;
            if (!empty)
                throw new InvalidOperationException(
                    "Empty crop treatment owner contains provenance.");
            return;
        }

        if (!Enum.IsDefined(typeof(CropTreatmentOrderPhase), owner.phase)
            || owner.operationSequence != saved.nextTreatmentOperationSequence
            || !string.Equals(
                owner.operationId,
                CropTreatmentPhysicalOutbox.FormatOperationId(
                    plotId.Value,
                    owner.operationSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                owner.destinationId,
                BuildTreatmentDestinationId(plotId),
                StringComparison.Ordinal)
            || !TryResolveTreatment(
                owner.itemId,
                out _,
                out CropTreatmentPolicy policy,
                out _)
            || owner.treatmentKind != policy.Kind
            || owner.quantity != policy.QuantityPerApplication
            || !owner.requiredWork.Equals(policy.RequiredWork)
            || !owner.effectAmount.Equals(policy.EffectAmount)
            || owner.cooldownDays != policy.CooldownDays
            || !CropTreatmentPhysicalOutbox.ValidateIntent(owner))
            throw new InvalidOperationException(
                "Crop treatment intent contradicts authored policy or plot identity.");

        bool hasReceipt = owner.sourceStackIds != null
            && owner.sourceStackIds.Count > 0
            && owner.sourceStackIds.All(value =>
                !string.IsNullOrWhiteSpace(value)
                && string.Equals(value, value.Trim(), StringComparison.Ordinal))
            && owner.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == owner.sourceStackIds.Count
            && owner.inputMassGrams > 0L
            && !string.IsNullOrWhiteSpace(owner.commitId)
            && !string.IsNullOrWhiteSpace(owner.requestFingerprint)
            && !string.IsNullOrWhiteSpace(owner.ecologyBeforeFingerprint);
        bool emptyReceipt = (owner.sourceStackIds?.Count ?? 0) == 0
            && owner.inputMassGrams == 0L
            && string.IsNullOrEmpty(owner.commitId)
            && string.IsNullOrEmpty(owner.requestFingerprint)
            && owner.tareOutputQuantity == 0
            && owner.tareOutputMassGrams == 0L
            && owner.destroyedTareMassGrams == 0L
            && (owner.tareOutputCommitIds?.Count ?? 0) == 0
            && string.IsNullOrEmpty(owner.ecologyBeforeFingerprint)
            && string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
            && owner.terminalDisposition
                == CropTreatmentTerminalDisposition.None
            && string.IsNullOrEmpty(owner.terminalReasonCode)
            && owner.terminalLossQuantity == 0
            && owner.terminalLossMassGrams == 0L;
        bool preCommit = owner.phase is
            CropTreatmentOrderPhase.WaitingForDelivery
            or CropTreatmentOrderPhase.ReadyForWork
            or CropTreatmentOrderPhase.Working;
        bool valid = preCommit
            ? emptyReceipt
            : owner.phase == CropTreatmentOrderPhase.InputCommitted
                ? hasReceipt
                    && string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
                    && owner.terminalDisposition
                        == CropTreatmentTerminalDisposition.None
                : owner.phase == CropTreatmentOrderPhase.OutcomePublished
                    ? hasReceipt
                        && !string.IsNullOrWhiteSpace(
                            owner.ecologyAfterFingerprint)
                        && owner.terminalDisposition
                            == CropTreatmentTerminalDisposition.None
                    : CropTreatmentPhysicalOutbox.ValidateDestroyedPlotLoss(
                        owner,
                        out _);
        if (!valid)
            throw new InvalidOperationException(
                "Crop treatment physical provenance contradicts its phase.");
    }

    private static void ValidatePendingSow(
        CropPlotSaveData saved,
        CropDefinitionSO crop)
    {
        CropPhysicalCommitSaveData owner = saved.pendingSow;
        if (owner.phase == CropPhysicalCommitPhase.None)
        {
            if (owner.inputs == null
                || owner.inputs.Count != 0
                || owner.hasSeedLot
                || !string.IsNullOrEmpty(owner.operationId)
                || !string.IsNullOrEmpty(owner.commitId)
                || owner.inputQuantity != 0
                || owner.inputMassGrams != 0L
                || owner.terminalDisposition != CropWipTerminalDisposition.None
                || !string.IsNullOrEmpty(owner.terminalOperationId)
                || !string.IsNullOrEmpty(owner.terminalReasonCode)
                || owner.terminalLossQuantity != 0
                || owner.terminalLossMassGrams != 0L)
                throw new InvalidOperationException(
                    "Empty crop sow owner contains transaction provenance: "
                    + DescribeEmptySowOwnerConflict(owner));
            return;
        }
        if (owner.phase is not (CropPhysicalCommitPhase.InputCommitted
                or CropPhysicalCommitPhase.OutcomePublished
                or CropPhysicalCommitPhase.PlotDestroyedLossPending)
            || owner.operationSequence != saved.nextSowOperationSequence
            || owner.inputs == null
            || owner.inputs.Count == 0
            || !owner.hasSeedLot
            || owner.seedLot == null
            || !string.Equals(owner.cropId, crop.CropId, StringComparison.Ordinal)
            || !string.Equals(owner.seedItemId, crop.SeedItemId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(owner.ecologyBeforeFingerprint)
            || owner.phase == CropPhysicalCommitPhase.InputCommitted
                && (saved.materialsConsumed
                    || !HasEmptyCropTerminalDisposition(owner))
            || owner.phase == CropPhysicalCommitPhase.OutcomePublished
                && (!saved.materialsConsumed
                    || string.IsNullOrWhiteSpace(owner.ecologyAfterFingerprint)
                    || !HasEmptyCropTerminalDisposition(owner))
            || owner.phase == CropPhysicalCommitPhase.PlotDestroyedLossPending
                && (saved.materialsConsumed
                    || !string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
                    || !CropPhysicalTransactionOutbox.ValidateDestroyedPlotLoss(
                        owner,
                        out _)))
            throw new InvalidOperationException(
                "Crop sow transaction owner contradicts plot state.");
    }

    private static string DescribeEmptySowOwnerConflict(
        CropPhysicalCommitSaveData owner)
    {
        List<string> fields = new();
        if (owner.inputs == null) fields.Add("inputs=null");
        else if (owner.inputs.Count != 0)
            fields.Add($"inputs={owner.inputs.Count}");
        if (owner.hasSeedLot) fields.Add("hasSeedLot");
        if (!string.IsNullOrEmpty(owner.operationId)) fields.Add("operationId");
        if (!string.IsNullOrEmpty(owner.commitId)) fields.Add("commitId");
        if (owner.inputQuantity != 0) fields.Add($"quantity={owner.inputQuantity}");
        if (owner.inputMassGrams != 0L) fields.Add($"grams={owner.inputMassGrams}");
        if (owner.terminalDisposition != CropWipTerminalDisposition.None)
            fields.Add($"terminal={(int)owner.terminalDisposition}");
        if (!string.IsNullOrEmpty(owner.terminalOperationId))
            fields.Add("terminalOperationId");
        if (!string.IsNullOrEmpty(owner.terminalReasonCode))
            fields.Add("terminalReasonCode");
        if (owner.terminalLossQuantity != 0)
            fields.Add($"terminalQuantity={owner.terminalLossQuantity}");
        if (owner.terminalLossMassGrams != 0L)
            fields.Add($"terminalGrams={owner.terminalLossMassGrams}");
        return fields.Count == 0 ? "unknown" : string.Join(",", fields);
    }

    private static bool HasEmptyCropTerminalDisposition(
        CropPhysicalCommitSaveData owner) =>
        owner != null
        && owner.terminalDisposition == CropWipTerminalDisposition.None
        && string.IsNullOrEmpty(owner.terminalOperationId)
        && string.IsNullOrEmpty(owner.terminalReasonCode)
        && owner.terminalLossQuantity == 0
        && owner.terminalLossMassGrams == 0L;

    private static void RequireFiniteRange(
        float value,
        float minimum,
        float maximum,
        string label)
    {
        if (float.IsNaN(value)
            || float.IsInfinity(value)
            || value < minimum
            || value > maximum)
        {
            throw new InvalidOperationException(
                $"Crop-plot {label} {value} is outside [{minimum}, {maximum}].");
        }
    }

    private void ResetForNextCycle(CropPlotState state)
    {
        items.RemoveDestination(state.MaterialDestinationId);
        state.Phase = CropPlotPhase.Empty;
        state.SowWork = 0f;
        state.GrowthHours = 0f;
        state.HarvestWork = 0f;
        state.MaterialsConsumed = false;
        state.BlockedReason = string.Empty;
        state.GoldenHarvestHarvesterId = string.Empty;
    }

    private void ReleaseMaterialDestination(CropPlotState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.MaterialDestinationId))
        {
            return;
        }

        Vector2Int position = state.Building != null
            ? state.Building.centerPos
            : state.LastKnownPosition;
        items.ReleaseDestination(state.MaterialDestinationId, position);
    }

    private bool TryFinalizeDestroyedPlot(CropPlotState state)
    {
        if (state == null)
            return true;

        if (state.Treatment.phase == CropTreatmentOrderPhase.OutcomePublished)
        {
            TryFinalizeTreatment(state);
            if (state.Treatment.phase != CropTreatmentOrderPhase.None)
                return false;
        }
        else if (state.Treatment.phase is CropTreatmentOrderPhase.InputCommitted
                or CropTreatmentOrderPhase.PlotDestroyedLossPending)
        {
            if (!CropTreatmentPhysicalOutbox.TryAcknowledgeDestroyedPlotLoss(
                    state.Treatment,
                    treatmentItems,
                    out string treatmentFailure))
            {
                state.Treatment.failureReason = treatmentFailure;
                snapshotsDirty = true;
                return false;
            }
            CropTreatmentPhysicalOutbox.Clear(state.Treatment);
            state.NextTreatmentOperationSequence = checked(
                state.NextTreatmentOperationSequence + 1);
        }
        else if (state.Treatment.phase != CropTreatmentOrderPhase.None)
        {
            ReleaseTreatmentDestination(state);
            CropTreatmentPhysicalOutbox.Clear(state.Treatment);
        }

        if (state.PendingSow.phase == CropPhysicalCommitPhase.OutcomePublished)
        {
            FinalizePublishedSow(state);
            return state.PendingSow.phase == CropPhysicalCommitPhase.None;
        }

        if (state.PendingSow.phase is CropPhysicalCommitPhase.InputCommitted
                or CropPhysicalCommitPhase.PlotDestroyedLossPending)
        {
            if (!CropPhysicalTransactionOutbox.TryAcknowledgeDestroyedPlotLoss(
                    state.PendingSow,
                    seedLots,
                    out string failureReason))
            {
                state.BlockedReason = failureReason;
                snapshotsDirty = true;
                return false;
            }
            CropPhysicalTransactionOutbox.Clear(state.PendingSow);
        }

        return state.PendingSow.phase == CropPhysicalCommitPhase.None
            && state.Treatment.phase == CropTreatmentOrderPhase.None;
    }

    private void RemoveFinalizedDestroyedPlot(BuildingInstanceId plotId)
    {
        if (!states.TryGetValue(plotId, out CropPlotState state))
            return;
        ReleaseMaterialDestination(state);
        ReleaseTreatmentDestination(state);
        ecology.AbandonPlot(plotId.Value);
        states.Remove(plotId);
        MarkChanged();
    }

    private void SetBlocked(CropPlotState state, string reason)
    {
        string normalized = string.IsNullOrWhiteSpace(reason)
            ? "작업이 막혔습니다."
            : reason.Trim();
        if (state.Phase == CropPlotPhase.Blocked
            && string.Equals(state.BlockedReason, normalized, StringComparison.Ordinal))
        {
            return;
        }

        state.Phase = CropPlotPhase.Blocked;
        state.BlockedReason = normalized;
        MarkChanged();
    }

    private void MarkChanged(bool replan = true)
    {
        Version++;
        snapshotsDirty = true;
        facilityCandidates.MarkDynamicStateDirty();
        if (replan)
        {
            workforce.RequestIdleWorkersToReplan();
        }
    }

    private void RefreshSnapshots()
    {
        if (!snapshotsDirty)
        {
            return;
        }

        snapshots.Clear();
        foreach (CropPlotState state in states.Values
                     .OrderBy(entry => entry.PlotId.Value, StringComparer.Ordinal))
        {
            if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
            {
                continue;
            }

            Dictionary<string, int> required =
                BuildMaterialRequirements(state, crop);
            Dictionary<string, int> delivered = required.Keys.ToDictionary(
                itemId => itemId,
                itemId => items.CountDelivered(
                    itemId,
                    state.MaterialDestinationId),
                StringComparer.Ordinal);
            CropEcologyPlotSaveData ecologyPlot = ecology.Plots.FirstOrDefault(value =>
                string.Equals(value.plotId, state.PlotId.Value, StringComparison.Ordinal));
            CropTreatmentOrderSaveData treatment = state.Treatment
                ?? new CropTreatmentOrderSaveData();
            int treatmentDelivered = treatment.phase
                    == CropTreatmentOrderPhase.None
                ? 0
                : items.CountDelivered(
                    treatment.itemId,
                    treatment.destinationId);
            snapshots.Add(new CropPlotSnapshot
            {
                PlotId = state.PlotId.Value,
                BuildingId = state.Building != null ? state.Building.id : 0,
                Position = state.Building != null
                    ? state.Building.centerPos
                    : Vector2Int.zero,
                Indoor = state.Ability?.Indoor ?? false,
                CropId = crop.CropId,
                CropName = crop.DisplayName,
                SeedItemId = crop.SeedItemId,
                CultivarGenomeId = ecologyPlot?.cultivarGenomeId ?? string.Empty,
                Fertility = ecologyPlot?.fertility ?? 100f,
                PestPressure = ecologyPlot?.pestPressure ?? 0f,
                DiseasePressure = ecologyPlot?.diseasePressure ?? 0f,
                CropDisease = ecologyPlot?.disease ?? CropDiseaseKind.None,
                Phase = state.Phase,
                SowProgress = crop.SowWork <= 0f
                    ? 0f
                    : Mathf.Clamp01(state.SowWork / crop.SowWork),
                GrowthProgress = crop.GrowthHours <= 0f
                    ? 0f
                    : Mathf.Clamp01(state.GrowthHours / crop.GrowthHours),
                HarvestProgress = crop.HarvestWork <= 0f
                    ? 0f
                    : Mathf.Clamp01(state.HarvestWork / crop.HarvestWork),
                MaterialDestinationId = state.MaterialDestinationId,
                RequiredMaterials = required,
                DeliveredMaterials = delivered,
                BlockedReason = state.BlockedReason,
                GoldenHarvestHarvesterId = state.GoldenHarvestHarvesterId,
                GoldenHarvestAttemptSequence = state.GoldenHarvestAttemptSequence,
                TreatmentScheduled = treatment.phase
                    != CropTreatmentOrderPhase.None,
                TreatmentPhase = treatment.phase,
                TreatmentItemId = treatment.itemId,
                TreatmentItemName = ResolveTreatmentDisplayName(
                    treatment.itemId),
                TreatmentKind = treatment.treatmentKind,
                TreatmentRequiredQuantity = treatment.quantity,
                TreatmentDeliveredQuantity = treatmentDelivered,
                TreatmentRequiredWork = treatment.requiredWork,
                TreatmentCompletedWork = treatment.completedWork,
                TreatmentEffectAmount = treatment.effectAmount,
                TreatmentCooldownDays = treatment.cooldownDays,
                TreatmentDestinationId = treatment.destinationId,
                TreatmentFailureReason = treatment.failureReason,
                CurrentAbsoluteDay = CurrentAbsoluteDay,
                PestLureNextAllowedDay = state.PestLureNextAllowedDay,
                BotanicalPesticideNextAllowedDay =
                    state.BotanicalPesticideNextAllowedDay,
                FungicideNextAllowedDay = state.FungicideNextAllowedDay
            });
        }

        snapshotsDirty = false;
    }

    private bool HasDelivered(
        IReadOnlyDictionary<string, int> requirements,
        string destinationId)
    {
        return requirements.All(requirement =>
            items.CountDelivered(requirement.Key, destinationId)
            >= requirement.Value);
    }

    private string BuildMaterialWaitReason(
        IReadOnlyDictionary<string, int> requirements,
        string destinationId)
    {
        string missing = requirements
            .Where(requirement =>
                items.CountDelivered(requirement.Key, destinationId)
                < requirement.Value)
            .Select(requirement =>
                $"{requirement.Key} "
                + $"{items.CountDelivered(requirement.Key, destinationId)}"
                + $"/{requirement.Value}")
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(missing)
            ? "파종 재료 확인 중"
            : $"파종 재료 운반 대기: {missing}";
    }

    private static string ResolveUnavailableReason(CropPlotState state)
    {
        if (!string.IsNullOrWhiteSpace(state.BlockedReason))
        {
            return state.BlockedReason;
        }

        return state.Phase switch
        {
            CropPlotPhase.Empty => "재배 주기 준비 중",
            CropPlotPhase.WaitingForMaterials => "파종 재료 운반 대기",
            CropPlotPhase.Growing => "작물이 자라는 중",
            CropPlotPhase.ReadyToHarvest => "수확 작업 대기",
            CropPlotPhase.Harvesting => "수확 작업 진행 중",
            _ => "현재 수행할 수 없는 작업"
        };
    }

    private static BuildingInstanceId BuildPlotId(BuildableObject plot)
    {
        return plot == null
            ? default
            : plot.RequirePersistentInstanceId();
    }

    private static string BuildDestinationId(BuildingInstanceId plotId)
    {
        if (!plotId.IsValid)
        {
            throw new InvalidOperationException(
                "Crop material destination requires a BuildingInstanceId.");
        }
        return $"crop-materials:{plotId.Value}";
    }
}
