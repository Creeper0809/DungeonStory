using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;

public sealed class CropPlotRuntime :
    ICropPlotRuntime,
    ICropPlotPersistence,
    ICropPlanExecutionReceiptQuery,
    ICropCycleExecutionCorrelationCommand,
    IProductionDomainOutputRestoreOwnerSource,
    IProductionDomainOutputFacilityLifecycleQuery,
    ICropPlotInputOwnerDescriptorSource,
    IInitializable,
    ITickable,
    IDisposable
{
    public const string WaterRefillOperationPrefix = CropPlotOperationIdentity.WaterRefillOperationPrefix;
    public const string IrrigationOperationPrefix = CropPlotOperationIdentity.IrrigationOperationPrefix;
    public const string CropSelectionOperationPrefix = CropPlotOperationIdentity.CropSelectionOperationPrefix;
    public const string GoldenHarvestScheduleOperationPrefix = CropPlotOperationIdentity.GoldenHarvestScheduleOperationPrefix;

    private const float MaterialRequestInterval = 0.5f;
    public const string HarvestOutputBatchCommitPrefix = CropPlotOperationIdentity.HarvestOutputBatchCommitPrefix;
    public const string HarvestOutputPublicationOperationPrefix = CropPlotOperationIdentity.HarvestOutputPublicationOperationPrefix;

    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IPhysicalSeedLotGateway seedLots;
    private readonly IPhysicalFacilityItemSinkGateway treatmentItems;
    private readonly ICropEcologyService ecology;
    private readonly ICropPlotInputOwnerRuntime inputOwners;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly ICropIrrigationRuntime irrigation;
    private readonly IGameClock gameClock;
    private readonly IGameCalendar calendar;
    private readonly ISurvivalEnvironmentQuery environmentQuery;
    private readonly IEnvironmentalFieldQuery environmentalField;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly IGameEventBus events;
    private readonly ISeasonalEventQuery seasonalEvents;
    private readonly V20StoryContentCatalog storyCatalog;
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly CropPlotSaveCodec saveCodec;
    private readonly CropPlotMutationRuntime mutation;
    private readonly CropPlotPhysicalInputRuntime physicalInputs;
    private readonly CropPlotWaterRuntime waterRuntime;
    private readonly CropPlotTreatmentRuntime treatmentRuntime;
    private readonly CropPlotSowRuntime sowRuntime;
    private readonly CropPlotIrrigationRuntime irrigationRuntime;
    private readonly CropPlotCycleSelectionRuntime cycleSelection;
    private readonly CropPlotCycleReceiptRuntime cycleReceipts;
    private readonly CropPlotFactPublication factPublication;
    private readonly CropPlotHarvestRuntime harvestRuntime;
    private readonly CropPlotSnapshotProjector snapshotProjector;
    private readonly CropTreatmentDefinitionQuery treatmentDefinitions;

    [VContainer.Inject]
    public void ConstructDomainFacts(IDomainFactPublisher publisher)  => factPublication.ConstructDomainFacts(publisher);
    private IDisposable dayEndedSubscription;
    private IDisposable contentEffectsResolvedSubscription;

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

    public string OutputOwnerDomainId => CropPlotHarvestRuntime.OutputOwnerDomainId;
    public string OutputBatchCommitPrefix => HarvestOutputBatchCommitPrefix;

    public IReadOnlyList<ProductionDomainOutputRestoreOwnerSnapshot>
        CapturePendingOutputOwners()  => harvestRuntime.CapturePendingOutputOwners();

    public IReadOnlyList<ProductionDomainOutputFacilityOwnerSnapshot>
        CaptureActiveOutputOwners(BuildingInstanceId facilityId)
     => harvestRuntime.CaptureActiveOutputOwners(facilityId);

    public IReadOnlyList<CropPlotInputOwnerDescriptor>
        BuildLiveInputOwnerDescriptors()
    {
        SynchronizePlots(force: false);
        return states.Values
            .Where(CropPlotPhysicalInputRuntime.RequiresAnyInputAuthority)
            .OrderBy(value => value.PlotId.Value, StringComparer.Ordinal)
            .SelectMany(value => physicalInputs.BuildInputOwnerDescriptors(
                value,
                value.Building,
                value.Ability))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<CropPlotInputOwnerDescriptor>
        BuildInputOwnerDescriptors(
            CropPlotRestoreCandidate candidate,
            IReadOnlyList<BuildableObject> detachedBuildings)
     => physicalInputs.BuildInputOwnerDescriptors(candidate, detachedBuildings);

    public bool TryBindNextCycle(
        string correlationId,
        string plotId,
        string cropId,
        out string failureReason)
     => cycleReceipts.TryBindNextCycle(correlationId, plotId, cropId, out failureReason);

    public bool TryCaptureExecutionReceipt(
        string actionId,
        out CropPlanExecutionReceipt receipt)
     => cycleReceipts.TryCaptureExecutionReceipt(actionId, out receipt);

    public bool TryAcknowledgeExecutionReceipt(
        string correlationId,
        string expectedRuntimeReceiptDigest,
        out string failureReason)
     => cycleReceipts.TryAcknowledgeExecutionReceipt(correlationId, expectedRuntimeReceiptDigest, out failureReason);

    public CropPlotRuntime(
        CropPlotWorldDependencies world,
        CropPlotSimulationDependencies simulation,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IProductionFacilityMutationEpochQuery facilityMutations,
        IMilestoneGameplayModifierQuery milestoneModifiers = null,
        ICharacterPerformanceQuery performance = null)
    {
        world = world ?? throw new ArgumentNullException(nameof(world));
        simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        buildingWorld = world.BuildingWorld;
        catalog = world.Catalog;
        seedLots = world.SeedLots;
        treatmentItems = world.TreatmentItems;
        ecology = world.Ecology;
        inputOwners = world.InputOwners;
        facilities = world.Facilities;
        irrigation = world.Irrigation;
        ICropIrrigationSupplyTransaction irrigationTransactions = world.Irrigation as
                ICropIrrigationSupplyTransaction
            ?? throw new InvalidOperationException(
                $"{nameof(CropPlotRuntime)} requires the exact irrigation supply transaction.");
        gameClock = simulation.GameClock;
        calendar = simulation.Calendar;
        BlueprintResearchRuntime research = simulation.ProgressionRuntimes
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(CropPlotRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        environmentQuery = simulation.EnvironmentQuery;
        environmentalField = simulation.EnvironmentalField;
        events = simulation.Events;
        seasonalEvents = simulation.SeasonalEvents;
        storyCatalog = simulation.StoryCatalog;
        extremeTraits = simulation.ExtremeTraits;
        performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        facilityMutations = facilityMutations
            ?? throw new ArgumentNullException(nameof(facilityMutations));
        this.milestoneModifiers = milestoneModifiers
            ?? NeutralMilestoneGameplayModifierQuery.Instance;

        factPublication = new CropPlotFactPublication(aggregateRootStore);
        treatmentDefinitions = new CropTreatmentDefinitionQuery(world.Catalog);
        var inputRequirementsQuery = new CropPlotInputRequirementsQuery(world.InputRequirements);
        mutation = new CropPlotMutationRuntime(world, aggregateRootStore, facilityMutations);
        physicalInputs = new CropPlotPhysicalInputRuntime(world, inputRequirementsQuery);
        waterRuntime = new CropPlotWaterRuntime(world, simulation, aggregateRootStore, mutation, physicalInputs, factPublication, treatmentDefinitions);
        treatmentRuntime = new CropPlotTreatmentRuntime(world, simulation, aggregateRootStore, mutation, physicalInputs, factPublication, treatmentDefinitions);
        sowRuntime = new CropPlotSowRuntime(world, simulation, aggregateRootStore, mutation, physicalInputs, inputRequirementsQuery, this.milestoneModifiers);
        cycleSelection = new CropPlotCycleSelectionRuntime(world, simulation, aggregateRootStore, mutation, physicalInputs, factPublication, research);
        irrigationRuntime = new CropPlotIrrigationRuntime(irrigation, irrigationTransactions, calendar, aggregateRootStore, mutation, factPublication);
        cycleReceipts = new CropPlotCycleReceiptRuntime(catalog, aggregateRootStore, mutation, physicalInputs, waterRuntime, treatmentRuntime, cycleSelection, factPublication);
        harvestRuntime = new CropPlotHarvestRuntime(world, simulation, aggregateRootStore, performance, factPublication);
        snapshotProjector = new CropPlotSnapshotProjector(world, simulation, aggregateRootStore, inputRequirementsQuery, treatmentDefinitions);
        var physicalSaveValidator = new CropPlotPhysicalSaveValidator(world, treatmentDefinitions);
        saveCodec = new CropPlotSaveCodec(world, simulation, aggregateRootStore, physicalSaveValidator);
    }

    public int Version
    {
        get => aggregateState.Version;
    }

    public IReadOnlyList<CropPlotSnapshot> Plots
    {
        get
        {
            SynchronizePlots(force: false);
            snapshotProjector.RefreshSnapshots();
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
        contentEffectsResolvedSubscription ??=
            events.Subscribe<V20ContentEffectsResolvedEvent>(
                OnContentEffectsResolved);
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
        foreach (CropPlotState state in states.Values
                     .OrderBy(value => value.PlotId.Value, StringComparer.Ordinal)
                     .ToArray())
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
        contentEffectsResolvedSubscription?.Dispose();
        contentEffectsResolvedSubscription = null;
        if (!inputOwners.TryReconcileLive(
                Array.Empty<CropPlotInputOwnerDescriptor>(),
                out string ownerFailure))
            throw new InvalidOperationException(
                "Crop-plot input owner disposal failed: " + ownerFailure);

        states.Clear();
        statesByBuilding.Clear();
        snapshots.Clear();
        aggregateState.HandledSeasonalStartInstanceIds.Clear();
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
        return cycleSelection.TrySetCrop(state, cropId, out message);
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
        if (!mutation.TryRequireMutable(state.PlotId, out reason))
            return false;
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
        if (!treatmentDefinitions.TryResolveTreatment(
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
        int nextAllowed = CropPlotTreatmentRuntime.GetNextTreatmentAllowedDay(state, policy.Kind);
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
            || !treatmentDefinitions.TryResolveTreatment(
                treatmentItemId,
                out ResourceItemDefinitionSO item,
                out CropTreatmentPolicy policy,
                out message))
            return false;

        int sequence = state.NextTreatmentOperationSequence;
        CropTreatmentOrderSaveData treatment = new()
        {
            phase = CropTreatmentOrderPhase.WaitingForDelivery,
            outcomeSchemaVersion =
                CropTreatmentOrderSaveData.CurrentOutcomeSchemaVersion,
            operationSequence = sequence,
            operationId = CropTreatmentPhysicalOutbox.FormatOperationId(
                state.PlotId.Value,
                sequence),
            reasonCode = CropTreatmentPhysicalOutbox.ReasonCode,
            destinationId = BuildTreatmentDestinationId(
                state.PlotId,
                sequence),
            itemId = item.ItemId,
            treatmentKind = policy.Kind,
            quantity = policy.QuantityPerApplication,
            requiredWork = policy.RequiredWork,
            completedWork = 0f,
            effectAmount = policy.EffectAmount,
            cooldownDays = policy.CooldownDays,
            scheduledAbsoluteDay = CurrentAbsoluteDay
        };
        CropPlotInputOwnerDescriptor descriptor =
            CropPlotPhysicalInputRuntime.CreateTreatmentInputOwnerDescriptor(state, treatment);
        if (!inputOwners.TryEnsure(descriptor, out string ownerFailure))
        {
            message = "처리제 목적지 소유권을 만들 수 없습니다: "
                + ownerFailure;
            return false;
        }
        state.Treatment = treatment;
        state.BlockedReason = string.Empty;
        mutation.MarkChanged();
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
                or CropTreatmentOrderPhase.LedgerCommitted
                or CropTreatmentOrderPhase.PlotDestroyedLossPending)
        {
            message = "이미 물리 소비가 확정된 처리는 취소할 수 없습니다.";
            return false;
        }
        if (!mutation.TryRequireMutable(state.PlotId, out message))
            return false;

        if (!physicalInputs.TryRetireDestination(
                state,
                state.Treatment.destinationId,
                CropPlotInputOwnerAuthority
                    .TreatmentCancelledReleaseReasonCode,
                out string retireFailure))
        {
            message = "처리제 목적지를 종료할 수 없습니다: "
                + retireFailure;
            return false;
        }
        CropTreatmentPhysicalOutbox.Clear(state.Treatment);
        state.BlockedReason = string.Empty;
        mutation.MarkChanged();
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

        bool mutable = mutation.TryRequireMutable(
            state.PlotId,
            out string mutationReason);

        if (workTypeId == BuiltInWorkTypeIds.Sow)
        {
            bool available = mutable
                && state.Phase is (CropPlotPhase.ReadyToSow
                    or CropPlotPhase.Sowing);
            snapshot = new CropPlotWorkSnapshot(
                state.PlotId.Value,
                workTypeId,
                $"{crop.DisplayName} 파종",
                crop.SowWork,
                state.SowWork,
                available,
                available
                    ? string.Empty
                    : !mutable
                        ? mutationReason
                        : ResolveUnavailableReason(state));
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Harvest)
        {
            bool phaseAvailable = state.Phase is CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting;
            bool treatmentClear = state.Treatment.phase
                == CropTreatmentOrderPhase.None;
            bool outputAvailable = state.PendingHarvest.phase
                == CropHarvestOutputPhase.None;
            bool available = mutable
                && phaseAvailable
                && treatmentClear
                && outputAvailable;
            snapshot = new CropPlotWorkSnapshot(
                state.PlotId.Value,
                workTypeId,
                $"{crop.DisplayName} 수확",
                crop.HarvestWork,
                state.HarvestWork,
                available,
                available
                    ? string.Empty
                    : !mutable
                        ? mutationReason
                    : phaseAvailable && !treatmentClear
                        ? "예약된 작물 처리 작업을 먼저 완료하거나 취소해야 합니다."
                    : phaseAvailable && !outputAvailable
                        ? "동결된 수확 출력이 시설 버퍼 공간을 기다리고 있습니다."
                        : ResolveUnavailableReason(state),
                state.NextHarvestOperationSequence,
                state.PendingHarvest.phase != CropHarvestOutputPhase.None);
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Treat
            && state.Treatment.phase != CropTreatmentOrderPhase.None)
        {
            bool available = mutable
                && state.Treatment.phase is
                    (CropTreatmentOrderPhase.ReadyForWork
                    or CropTreatmentOrderPhase.Working)
                && treatmentRuntime.IsTreatmentStillRequired(state);
            snapshot = new CropPlotWorkSnapshot(
                state.PlotId.Value,
                workTypeId,
                treatmentDefinitions.ResolveTreatmentDisplayName(state.Treatment.itemId) + " 살포",
                state.Treatment.requiredWork,
                state.Treatment.completedWork,
                available,
                available
                    ? string.Empty
                    : !mutable
                        ? mutationReason
                        : treatmentRuntime.ResolveTreatmentUnavailableReason(state));
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Treat
            && state.WaterRefill.phase != CropWaterRefillPhase.None)
        {
            bool available = mutable
                && state.Phase == CropPlotPhase.Growing
                && state.WaterRefill.phase is
                    (CropWaterRefillPhase.ReadyForWork
                    or CropWaterRefillPhase.Working);
            snapshot = new CropPlotWorkSnapshot(
                state.PlotId.Value,
                workTypeId,
                "경작지 급수",
                state.WaterRefill.requiredWork,
                state.WaterRefill.completedWork,
                available,
                available
                    ? string.Empty
                    : !mutable
                        ? mutationReason
                        : CropPlotWaterRuntime.ResolveWaterRefillUnavailableReason(state));
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
            if (!mutation.TryRequireMutable(state.PlotId, out _))
                return false;
            state.Phase = CropPlotPhase.Sowing;
            float sowBefore = state.SowWork;
            state.SowWork = Mathf.Min(
                crop.SowWork,
                state.SowWork + amount);
            CropPlotCycleReceiptRuntime.RecordCycleContribution(
                state,
                worker,
                Mathf.Max(0f, state.SowWork - sowBefore),
                harvest: false);
            if (state.SowWork + 0.001f >= crop.SowWork)
            {
                state.SowWork = crop.SowWork;
                state.Phase = CropPlotPhase.Growing;
                cycleCompleted = true;
            }

            mutation.MarkChanged();
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Harvest
            && state.Phase is CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting)
        {
            if (state.PendingHarvest.phase != CropHarvestOutputPhase.None)
            {
                TryAdvancePendingHarvest(state, crop, out cycleCompleted);
                mutation.MarkChanged();
                return true;
            }
            if (!mutation.TryRequireMutable(state.PlotId, out _))
                return false;
            state.Phase = CropPlotPhase.Harvesting;
            float harvestBefore = state.HarvestWork;
            state.HarvestWork = Mathf.Min(
                crop.HarvestWork,
                state.HarvestWork + amount);
            CropPlotCycleReceiptRuntime.RecordCycleContribution(
                state,
                worker,
                Mathf.Max(0f, state.HarvestWork - harvestBefore),
                harvest: true);
            if (state.HarvestWork + 0.001f >= crop.HarvestWork)
            {
                state.HarvestWork = crop.HarvestWork;
                if (harvestRuntime.FreezeHarvestOutcome(state, crop, worker))
                    TryAdvancePendingHarvest(state, crop, out cycleCompleted);
            }

            mutation.MarkChanged();
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Treat
            && state.Treatment.phase is CropTreatmentOrderPhase.ReadyForWork
                or CropTreatmentOrderPhase.Working)
        {
            if (state.Treatment.phase == CropTreatmentOrderPhase.Working
                && state.Treatment.completedWork + 0.001f
                    >= state.Treatment.requiredWork)
            {
                bool finalized = treatmentRuntime.TryFinalizeTreatment(state);
                cycleCompleted = finalized;
                mutation.MarkChanged();
                return finalized;
            }
            if (!mutation.TryRequireMutable(state.PlotId, out _))
                return false;
            state.Treatment.phase = CropTreatmentOrderPhase.Working;
            float treatmentBefore = state.Treatment.completedWork;
            state.Treatment.completedWork = Mathf.Min(
                state.Treatment.requiredWork,
                state.Treatment.completedWork + amount);
            CropPlotTreatmentRuntime.RecordTreatmentContribution(
                state.Treatment,
                worker,
                Mathf.Max(
                    0f,
                    state.Treatment.completedWork - treatmentBefore));
            if (state.Treatment.completedWork + 0.001f
                >= state.Treatment.requiredWork)
            {
                state.Treatment.completedWork = state.Treatment.requiredWork;
                bool outcomePublished = treatmentRuntime.TryFinalizeTreatment(state);
                cycleCompleted = outcomePublished;
                mutation.MarkChanged();
                return outcomePublished;
            }

            mutation.MarkChanged();
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Treat
            && state.Phase == CropPlotPhase.Growing
            && state.WaterRefill.phase is CropWaterRefillPhase.ReadyForWork
                or CropWaterRefillPhase.Working)
        {
            if (state.WaterRefill.phase == CropWaterRefillPhase.Working
                && state.WaterRefill.completedWork + 0.001f
                    >= state.WaterRefill.requiredWork)
            {
                bool finalized = waterRuntime.TryFinalizeWaterRefill(state);
                cycleCompleted = finalized;
                mutation.MarkChanged();
                return finalized;
            }
            if (!mutation.TryRequireMutable(state.PlotId, out _))
                return false;
            state.WaterRefill.phase = CropWaterRefillPhase.Working;
            float refillWorkBefore = state.WaterRefill.completedWork;
            state.WaterRefill.completedWork = Mathf.Min(
                state.WaterRefill.requiredWork,
                state.WaterRefill.completedWork + amount);
            CropPlotWaterRuntime.RecordWaterRefillContribution(
                state.WaterRefill,
                worker,
                Mathf.Max(
                    0f,
                    state.WaterRefill.completedWork - refillWorkBefore));
            if (state.WaterRefill.completedWork + 0.001f
                >= state.WaterRefill.requiredWork)
            {
                state.WaterRefill.completedWork =
                    state.WaterRefill.requiredWork;
                bool outcomePublished = waterRuntime.TryFinalizeWaterRefill(state);
                cycleCompleted = outcomePublished;
                mutation.MarkChanged();
                return outcomePublished;
            }

            mutation.MarkChanged();
            return true;
        }

        return false;
    }

    private void TryAdvancePendingHarvest(
        CropPlotState state,
        CropDefinitionSO crop,
        out bool cycleCompleted)
    {
        cycleCompleted = false;
        if (!harvestRuntime.TryPublishPendingHarvest(state, crop,
                out CropCycleExecutionReceiptSaveData terminalReceipt))
            return;
        int nextHarvestSequence = checked(state.NextHarvestOperationSequence + 1);
        if (!TryResetForNextCycle(state, out string resetFailure))
        {
            // The ledger result is already canonical and acknowledged.  Keep the
            // authoritative harvest owner so the idempotent replay can resume
            // cleanup; never roll the domain back behind a committed outcome.
            state.BlockedReason = resetFailure;
            return;
        }
        state.CycleExecutionReceipt = terminalReceipt;
        state.NextHarvestOperationSequence = nextHarvestSequence;
        state.PendingHarvest = new CropHarvestOutputSaveData();
        cycleCompleted = true;
    }

    public static string FormatHarvestOperationId(
        BuildingInstanceId plotId,
        int sequence)
    => CropPlotOperationIdentity.FormatHarvestOperationId(plotId, sequence);

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
        return cycleSelection.TryScheduleGoldenHarvest(state, harvester, out failureReason);
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


    public DungeonCropPlotSaveData Capture()
    {
        if (!inputOwners.TryReconcileLive(
                BuildLiveInputOwnerDescriptors(),
                out string ownerFailure))
            throw new InvalidOperationException(
                "Crop-plot input ownership is not capture-safe: "
                + ownerFailure);
        return saveCodec.Capture();
    }

    public CropPlotRestoreCandidate BuildRestore(
        DungeonCropPlotSaveData snapshot)
    => saveCodec.BuildRestore(snapshot);

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

        if (!irrigationRuntime.TryAdvanceIrrigationPublication(state)
            || !cycleSelection.TryAdvanceCropSelectionOutcome(state)
            || !cycleSelection.TryAdvanceGoldenHarvestScheduleOutcome(state))
            return;

        if (IsFailedCycleReceipt(state.CycleExecutionReceipt))
        {
            cycleReceipts.TryAdvanceFailedCycle(state);
            return;
        }

        if (state.PendingSow.phase == CropPhysicalCommitPhase.OutcomePublished)
        {
            sowRuntime.FinalizePublishedSow(state);
            if (state.PendingSow.phase != CropPhysicalCommitPhase.None)
                return;
        }

        if (state.WaterRefill.phase is CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted)
        {
            waterRuntime.TryFinalizeWaterRefill(state);
            if (state.WaterRefill.phase != CropWaterRefillPhase.None)
                return;
        }

        if (state.Treatment.phase is CropTreatmentOrderPhase.Working
                or CropTreatmentOrderPhase.InputCommitted
                or CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted
            && (state.Treatment.phase != CropTreatmentOrderPhase.Working
                || state.Treatment.completedWork + 0.001f
                    >= state.Treatment.requiredWork))
        {
            treatmentRuntime.TryFinalizeTreatment(state);
        }

        if (state.Building == null
            || state.Building.isDestroy
            || !catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
        {
            return;
        }

        if (state.PendingHarvest.phase != CropHarvestOutputPhase.None)
        {
            TryAdvancePendingHarvest(state, crop, out _);
            if (state.PendingHarvest.phase != CropHarvestOutputPhase.None)
                return;
        }

        if (!mutation.TryRequireMutable(state.PlotId, out string mutationReason))
        {
            if (!string.Equals(
                    state.BlockedReason,
                    mutationReason,
                    StringComparison.Ordinal))
            {
                state.BlockedReason = mutationReason;
                snapshotsDirty = true;
            }
            return;
        }
        if (state.BlockedReason.StartsWith(
                "production-facility-mutation-open:",
                StringComparison.Ordinal))
        {
            state.BlockedReason = string.Empty;
            snapshotsDirty = true;
        }

        if (!cycleSelection.IsResearchUnlocked(crop, out string researchReason))
        {
            mutation.SetBlocked(state, researchReason);
            return;
        }

        if (state.Phase == CropPlotPhase.Blocked
            && state.BlockedReason.StartsWith("연구 필요", StringComparison.Ordinal))
        {
            state.Phase = CropPlotPhase.Empty;
            state.BlockedReason = string.Empty;
            mutation.MarkChanged();
        }

        treatmentRuntime.TickTreatmentDelivery(state, requestMaterials);

        if (state.Phase is CropPlotPhase.Empty
            or CropPlotPhase.WaitingForMaterials)
        {
            if (state.CycleExecutionReceipt != null
                && !state.CycleExecutionReceipt.IsEmpty)
            {
                CropPlanExecutionReceiptAuthority.Validate(
                    state.CycleExecutionReceipt,
                    requireCompleted: false);
                if (state.CycleExecutionReceipt.status
                    == CropCycleExecutionReceiptStatus.Active)
                {
                    throw new InvalidOperationException(
                        "An empty crop plot retained an active execution receipt.");
                }
                if (state.CycleExecutionReceipt.explicitCorrelation)
                {
                    state.BlockedReason =
                        "crop-cycle-execution-receipt-awaiting-acknowledgement";
                    snapshotsDirty = true;
                    return;
                }
                state.CycleExecutionReceipt =
                    new CropCycleExecutionReceiptSaveData();
                mutation.MarkChanged(replan: false);
            }
            sowRuntime.EnsureSowingMaterials(state, crop, requestMaterials);
            return;
        }

        if (state.Phase != CropPlotPhase.Growing)
        {
            waterRuntime.TryRetireInactiveWaterRefill(state);
            return;
        }

        float gameHours = gameClock.DeltaTime
            / GameSimulationTimeRules.SecondsPerGameHour;
        float hourlyWaterConsumption = CropWaterRules.ResolveHourlyConsumption(
            crop,
            state.Ability,
            environmentQuery.GetEnvironmentSnapshot().Weather);
        float nextWater = Mathf.Max(
            0f,
            state.CurrentWater - hourlyWaterConsumption * gameHours);
        if (!Mathf.Approximately(nextWater, state.CurrentWater))
        {
            state.CurrentWater = nextWater;
            snapshotsDirty = true;
        }

        float dailyWaterDemand = CropWaterRules.ResolveDailyDemand(
            crop,
            state.Ability);
        bool needsWater = dailyWaterDemand > 0f
            && state.CurrentWater + 0.001f < dailyWaterDemand;
        if (needsWater
            && state.WaterRefill.phase == CropWaterRefillPhase.None)
        {
            CropIrrigationRequest request = new(
                state.Building,
                needsWater: true,
                state.CurrentWater,
                state.WaterCapacity,
                hasManualWaterRefillOwner: false);
            float waterBefore = state.CurrentWater;
            float capacityBefore = state.WaterCapacity;
            CropIrrigationSupplyResult supply = !gameClock.IsPaused
                    && gameClock.DeltaTime > 0f
                ? irrigationRuntime.TryCommitAutomaticIrrigation(state, request)
                : new CropIrrigationSupplyResult(
                    irrigation.Assess(request),
                    succeeded: false,
                    suppliedWaterUnits: 0f,
                    consumedQuality: WorldWaterQuality.Clean);
            if (supply.Succeeded)
            {
                if (supply.SuppliedWaterUnits
                        != CropWaterRules.RefillQuantity
                    || state.WaterCapacity != capacityBefore
                    || state.WaterRefill.phase != CropWaterRefillPhase.None
                    || waterBefore + supply.SuppliedWaterUnits
                        > capacityBefore)
                {
                    throw new InvalidOperationException(
                        "Synchronous crop irrigation violated its preflight publication contract.");
                }

                if (state.CurrentWater != waterBefore + supply.SuppliedWaterUnits)
                    throw new InvalidOperationException(
                        "Committed crop irrigation did not publish its exact plot-water credit.");
            }
            else if (CropPlotWaterRuntime.ShouldBeginManualWaterRefill(
                         supply.Assessment.Status))
            {
                waterRuntime.BeginWaterRefill(state);
            }
        }
        if (state.IrrigationPublication.pending)
            return;
        waterRuntime.TickWaterRefillDelivery(state, requestMaterials);

        float waterGrowthMultiplier = CropWaterRules.ResolveGrowthMultiplier(
            state.CurrentWater,
            dailyWaterDemand);
        float multiplier = ResolveGrowthMultiplier(
            state,
            crop,
            out string blockedReason) * waterGrowthMultiplier;
        if (multiplier <= 0f)
        {
            if (waterGrowthMultiplier <= 0f)
                blockedReason = "급수 부족 · 물이 없어 성장이 멈췄습니다.";
            if (!string.Equals(state.BlockedReason, blockedReason, StringComparison.Ordinal))
            {
                state.BlockedReason = blockedReason;
                mutation.MarkChanged(replan: false);
            }
            return;
        }

        state.BlockedReason = string.Empty;
        state.GrowthHours = Mathf.Min(
            crop.GrowthHours,
            state.GrowthHours + gameHours * multiplier);
        snapshotsDirty = true;
        if (state.GrowthHours + 0.001f >= crop.GrowthHours)
        {
            state.GrowthHours = crop.GrowthHours;
            state.Phase = CropPlotPhase.ReadyToHarvest;
            mutation.MarkChanged();
        }
    }

    public static string FormatWaterRefillOperationId(
        BuildingInstanceId plotId,
        int sequence)
    => CropPlotOperationIdentity.FormatWaterRefillOperationId(plotId, sequence);

    private int CurrentAbsoluteDay => calendar.Day;

    private float ResolveGrowthMultiplier(
        CropPlotState state,
        CropDefinitionSO crop,
        out string blockedReason)
    {
        blockedReason = string.Empty;
        CropGenomePhenotype phenotype =
            ecology.GetPhenotype(state.PlotId.Value);
        EnvironmentalCellSnapshot environmentCell = default;
        bool hasEnvironmentObservation = environmentalField.IsInitialized
            && environmentalField.TryGetCell(
                state.Building.centerPos,
                out environmentCell);
        CropGrowthTemperatureEvaluation temperature =
            CropGrowthCycleAuthority.EvaluateTemperature(
                crop,
                phenotype,
                hasEnvironmentObservation,
                hasEnvironmentObservation ? environmentCell.TemperatureC : 0f);
        if (!temperature.AllowsGrowth)
        {
            blockedReason = temperature.BlockedReason;
            return 0f;
        }
        CropGrowthLightEvaluation light =
            CropGrowthCycleAuthority.EvaluateLight(
                crop,
                hasEnvironmentObservation,
                hasEnvironmentObservation ? environmentCell.LightLevel : 0f);
        if (!light.AllowsGrowth)
        {
            blockedReason = light.BlockedReason;
            return 0f;
        }
        float lightMultiplier = light.GrowthMultiplier;
        if (state.Ability.Indoor)
        {
            return CropGrowthCycleAuthority.ResolveIndoorRuntimeMultiplier(
                state.Ability,
                IsOperational(ResearchFacilityCommandKind.ClimateControl),
                IsOperational(ResearchFacilityCommandKind.CropCalendar),
                phenotype) * lightMultiplier;
        }

        SurvivalEnvironmentSnapshot environment =
            environmentQuery.GetEnvironmentSnapshot();
        return CropGrowthCycleAuthority.ResolveOutdoorRuntimeMultiplier(
            state.Ability,
            phenotype,
            environment,
            IsOperational(ResearchFacilityCommandKind.CropCalendar))
            * lightMultiplier;
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
                if (!string.IsNullOrEmpty(state.RetirementPublicationKey))
                    throw new InvalidOperationException("A retiring crop plot cannot be reattached to a live building.");
                state.Building = building;
                state.Ability = ability;
                state.LastKnownPosition = building.centerPos;
                if (string.IsNullOrWhiteSpace(state.PlotDisplayName))
                    state.PlotDisplayName = CropPlotFactPublication.RequirePlotDisplayName(building);
            }

            statesByBuilding[building] = state;
        }

        foreach (BuildingInstanceId removedId in states.Keys
                     .Where(id => !seen.Contains(id))
                     .ToArray())
        {
            CropPlotState removed = states[removedId];
            if (string.IsNullOrWhiteSpace(removed.PlotDisplayName)
                && removed.Building != null)
                removed.PlotDisplayName = CropPlotFactPublication.RequirePlotDisplayName(removed.Building);
            if (!string.IsNullOrEmpty(removed.PendingCycleCorrelationId)
                && (removed.CycleExecutionReceipt == null
                    || removed.CycleExecutionReceipt.IsEmpty))
            {
                removed.CycleExecutionReceipt =
                    CropPlanExecutionReceiptAuthority.FailBeforeSow(
                        removed.PendingCycleCorrelationId,
                        removed.PlotId.Value,
                        removed.CropId,
                        removed.Ability?.Indoor ?? false,
                        "crop-cycle-failed-plot-destroyed-before-sow");
                removed.PendingCycleCorrelationId = string.Empty;
                cycleReceipts.FreezeFailedOutcome(removed, CurrentAbsoluteDay);
            }
            else if (removed.CycleExecutionReceipt?.status
                    == CropCycleExecutionReceiptStatus.Active
                && removed.PendingHarvest.phase
                    == CropHarvestOutputPhase.None)
            {
                removed.CycleExecutionReceipt =
                    CropPlanExecutionReceiptAuthority.Fail(
                        removed.CycleExecutionReceipt,
                        CropCycleExecutionReceiptStatus.FailedPlotDestroyed,
                        "crop-cycle-failed-plot-destroyed");
                cycleReceipts.FreezeFailedOutcome(removed, CurrentAbsoluteDay);
            }
            if (IsFailedCycleReceipt(removed.CycleExecutionReceipt))
            {
                removed.Building = null;
                removed.Ability = null;
                removed.Phase = CropPlotPhase.Blocked;
                removed.BlockedReason = "crop-plot-destroyed-awaiting-outcome";
                continue;
            }
            if (!physicalInputs.TryRetireDestination(
                    removed,
                    removed.MaterialDestinationId,
                    CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode,
                    out string materialRetireFailure))
            {
                removed.BlockedReason = materialRetireFailure;
                snapshotsDirty = true;
                continue;
            }
            if (removed.Treatment.phase is CropTreatmentOrderPhase.WaitingForDelivery
                    or CropTreatmentOrderPhase.ReadyForWork
                    or CropTreatmentOrderPhase.Working)
            {
                if (!physicalInputs.TryRetireDestination(
                        removed,
                        removed.Treatment.destinationId,
                        CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode,
                        out string treatmentRetireFailure))
                {
                    removed.BlockedReason = treatmentRetireFailure;
                    snapshotsDirty = true;
                    continue;
                }
                CropTreatmentPhysicalOutbox.Clear(removed.Treatment);
            }
            removed.Building = null;
            removed.Ability = null;
            removed.BlockedReason = removed.PendingSow.phase
                == CropPhysicalCommitPhase.None
                    ? "crop-plot-destroyed"
                    : "crop-plot-destroyed-with-pending-physical-transaction";
        }

        mutation.MarkChanged();
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
            if (state.CycleExecutionReceipt == null
                || state.CycleExecutionReceipt.status
                    != CropCycleExecutionReceiptStatus.Active)
                throw new InvalidOperationException(
                    "A dying crop has no active execution receipt.");
            state.CycleExecutionReceipt =
                CropPlanExecutionReceiptAuthority.Fail(
                    state.CycleExecutionReceipt,
                    CropCycleExecutionReceiptStatus.FailedCropDeath,
                    "crop-cycle-failed-crop-death");
            cycleReceipts.FreezeFailedOutcome(state, Math.Max(0, ended.day));
            state.SeasonalYieldDamage =
                new CropSeasonalYieldDamageSaveData();
            state.Phase = CropPlotPhase.Blocked;
            state.BlockedReason = "작물이 극한 환경 또는 해충으로 고사했습니다.";
            mutation.MarkChanged();
            cycleReceipts.TryAdvanceFailedCycle(state);
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
        CropPlotState state = new()
        {
            PlotId = plotId,
            Building = building,
            Ability = ability,
            PlotDisplayName = CropPlotFactPublication.RequirePlotDisplayName(building),
            LastKnownPosition = building.centerPos,
            CropId = crop?.CropId ?? string.Empty,
            Phase = crop != null
                ? CropPlotPhase.Empty
                : CropPlotPhase.Blocked,
            MaterialDestinationId = BuildSowDestinationId(plotId, 0),
            BlockedReason = crop != null
                ? string.Empty
                : "연구가 완료된 재배 작물이 없습니다."
        };
        return state;
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
                && cycleSelection.IsResearchUnlocked(crop, out _));
        return preferred ?? catalog.Crops
            .FirstOrDefault(crop => crop != null
                && (!ability.Indoor || crop.IndoorAllowed)
                && cycleSelection.IsResearchUnlocked(crop, out _));
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

    private void OnContentEffectsResolved(V20ContentEffectsResolvedEvent resolved)
    {
        if (!resolved.PhysicalEffectsApplied
            || !string.Equals(
                resolved.ResolutionId,
                "started",
                StringComparison.Ordinal))
        {
            return;
        }

        SeasonalWorldEventDefinitionSO definition = storyCatalog.SeasonalEvents
            .SingleOrDefault(value => value != null && string.Equals(
                value.StableId,
                resolved.DefinitionId,
                StringComparison.Ordinal));
        // The shared resolved-effects event also carries society starts. Crop
        // authority consumes only declarations owned by seasonal definitions.
        if (definition == null)
            return;
        int lossPercent = definition.cropPrimaryBatchLossPercent;
        if (lossPercent == 0)
            return;
        if (lossPercent is < 0
            or > CropHarvestOutputRules.MaximumSeasonalPrimaryBatchLossPercent)
        {
            throw new InvalidOperationException(
                "A committed seasonal crop-loss declaration is outside the approved range: "
                + definition.StableId);
        }

        V20ActiveEventSaveData[] activeMatches = seasonalEvents
            .ActiveSeasonalEvents
            .Where(value => value != null
                && !value.resolved
                && string.Equals(
                    value.definitionId,
                    definition.StableId,
                    StringComparison.Ordinal))
            .ToArray();
        if (activeMatches.Length != 1)
            throw new InvalidOperationException(
                "A committed seasonal crop-loss start requires one current active occurrence: "
                + definition.StableId);
        V20ActiveEventSaveData occurrence = activeMatches[0];
        if (!TryParseSeasonalEventInstanceId(
                occurrence.instanceId,
                out int parsedStartedDay,
                out string parsedDefinitionId)
            || parsedStartedDay != occurrence.startedAbsoluteDay
            || !string.Equals(
                parsedDefinitionId,
                occurrence.definitionId,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "A committed seasonal crop-loss occurrence has invalid provenance.");
        if (aggregateState.HandledSeasonalStartInstanceIds.Contains(
                occurrence.instanceId))
        {
            return;
        }

        List<CropPlotState> affected = new();
        foreach (CropPlotState state in states.Values
                     .OrderBy(value => value.PlotId.Value, StringComparer.Ordinal))
        {
            if (state?.Building == null
                || state.Building.isDestroy
                || state.Phase is not (CropPlotPhase.Growing
                    or CropPlotPhase.ReadyToHarvest
                    or CropPlotPhase.Harvesting))
            {
                continue;
            }
            if (state.PendingHarvest == null)
                throw new InvalidOperationException(
                    "A seasonal crop-loss target has no harvest output owner.");
            if (state.PendingHarvest.phase != CropHarvestOutputPhase.None)
                continue;
            if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
                throw new InvalidOperationException(
                    "A seasonal crop-loss target references unknown crop content: "
                    + state.CropId);
            RequireCompletedSowCycle(state, crop);
            CropSeasonalYieldDamageSaveData existing =
                state.SeasonalYieldDamage
                ?? throw new InvalidOperationException(
                    "A seasonal crop-loss target has no yield-damage owner.");
            if (!existing.IsEmpty)
            {
                ValidateSeasonalYieldDamage(
                    existing,
                    state.CropId,
                    state.CycleExecutionReceipt,
                    aggregateState.HandledSeasonalStartInstanceIds);
                continue;
            }
            affected.Add(state);
        }

        foreach (CropPlotState state in affected)
        {
            state.SeasonalYieldDamage = new CropSeasonalYieldDamageSaveData
            {
                sourceEventInstanceId = occurrence.instanceId,
                sourceDefinitionId = definition.StableId,
                sourceStartedAbsoluteDay = occurrence.startedAbsoluteDay,
                cropId = state.CropId,
                sowOperationSequence =
                    state.CycleExecutionReceipt.sowOperationSequence,
                primaryBatchLossPercent = lossPercent
            };
        }
        aggregateState.HandledSeasonalStartInstanceIds.Add(
            occurrence.instanceId);
        mutation.MarkChanged(replan: false);
    }

    private static void RequireCompletedSowCycle(
        CropPlotState state,
        CropDefinitionSO crop)
    {
        CropCycleExecutionReceiptSaveData receipt = state?.CycleExecutionReceipt;
        if (state == null
            || crop == null
            || !state.MaterialsConsumed
            || state.SowWork + 0.001f < crop.SowWork
            || receipt == null
            || receipt.IsEmpty
            || receipt.status != CropCycleExecutionReceiptStatus.Active
            || !string.Equals(
                receipt.plotId,
                state.PlotId.Value,
                StringComparison.Ordinal)
            || !string.Equals(receipt.cropId, state.CropId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Seasonal crop damage requires a live cycle with completed sow authority.");
        }
        CropPlanExecutionReceiptAuthority.Validate(receipt, requireCompleted: false);
    }

    private bool TryResetForNextCycle(
        CropPlotState state,
        out string failureReason)
    {
        if (state.IrrigationPublication.pending)
        {
            failureReason = "crop-next-cycle-irrigation-publication-pending";
            return false;
        }
        try
        {
            if (!waterRuntime.TryRetireInactiveWaterRefill(state))
            {
                failureReason =
                    "crop-next-cycle-water-refill-retirement-failed:"
                    + (state.WaterRefill?.failureReason ?? "unknown");
                return false;
            }
            if (!physicalInputs.TryRetireDestination(
                    state,
                    state.MaterialDestinationId,
                    CropPlotInputOwnerAuthority.SowCompletedReleaseReasonCode,
                    out string retireFailure))
            {
                failureReason =
                    "crop-next-cycle-input-retirement-failed:"
                    + retireFailure;
                return false;
            }
        }
        catch (Exception error)
        {
            failureReason =
                "crop-next-cycle-cleanup-deferred:"
                + error.GetType().Name;
            return false;
        }
        state.Phase = CropPlotPhase.Empty;
        state.SowWork = 0f;
        state.GrowthHours = 0f;
        state.HarvestWork = 0f;
        state.MaterialsConsumed = false;
        state.CurrentWater = 0f;
        state.WaterCapacity = CropWaterRules.WaterCapacity;
        CropPlotSowRuntime.ClearFrozenSowInputs(state);
        state.BlockedReason = string.Empty;
        state.GoldenHarvestHarvesterId = string.Empty;
        state.SeasonalYieldDamage =
            new CropSeasonalYieldDamageSaveData();
        failureReason = string.Empty;
        return true;
    }

    private bool TryFinalizeDestroyedPlot(CropPlotState state)
    {
        if (state == null)
            return true;

        if (!irrigationRuntime.TryAdvanceIrrigationPublication(state))
        {
            state.BlockedReason = "crop-irrigation-publication-blocks-plot-destruction";
            snapshotsDirty = true;
            return false;
        }

        if (state.CropSelectionOutcome is { IsEmpty: false }
            && !cycleSelection.TryAdvanceCropSelectionOutcome(state))
        {
            state.BlockedReason =
                "crop-selection-outcome-blocks-plot-destruction";
            snapshotsDirty = true;
            return false;
        }
        if (state.GoldenHarvestScheduleOutcome is { IsEmpty: false }
            && !cycleSelection.TryAdvanceGoldenHarvestScheduleOutcome(state))
        {
            state.BlockedReason =
                "golden-harvest-schedule-outcome-blocks-plot-destruction";
            snapshotsDirty = true;
            return false;
        }

        // A frozen harvest publication still owns its terminal cycle.  Let it
        // settle before choosing a destruction failure for that correlation.
        if (state.PendingHarvest?.phase != CropHarvestOutputPhase.None)
        {
            state.BlockedReason =
                "crop-harvest-output-owner-blocks-plot-destruction";
            snapshotsDirty = true;
            return false;
        }

        if (!string.IsNullOrEmpty(state.PendingCycleCorrelationId)
            && (state.CycleExecutionReceipt == null
                || state.CycleExecutionReceipt.IsEmpty))
        {
            state.CycleExecutionReceipt =
                CropPlanExecutionReceiptAuthority.FailBeforeSow(
                    state.PendingCycleCorrelationId,
                    state.PlotId.Value,
                    state.CropId,
                    state.Ability?.Indoor ?? false,
                    "crop-cycle-failed-plot-destroyed-before-sow");
            state.PendingCycleCorrelationId = string.Empty;
            cycleReceipts.FreezeFailedOutcome(state, CurrentAbsoluteDay);
        }
        if (state.CycleExecutionReceipt?.status
            == CropCycleExecutionReceiptStatus.Active)
        {
            state.CycleExecutionReceipt =
                CropPlanExecutionReceiptAuthority.Fail(
                    state.CycleExecutionReceipt,
                    CropCycleExecutionReceiptStatus.FailedPlotDestroyed,
                    "crop-cycle-failed-plot-destroyed");
            cycleReceipts.FreezeFailedOutcome(state, CurrentAbsoluteDay);
        }
        if (IsFailedCycleReceipt(state.CycleExecutionReceipt))
        {
            if (state.Phase != CropPlotPhase.Blocked)
            {
                state.Phase = CropPlotPhase.Blocked;
                state.SeasonalYieldDamage =
                    new CropSeasonalYieldDamageSaveData();
                state.BlockedReason =
                    "crop-plot-destroyed-awaiting-outcome";
                mutation.MarkChanged(replan: false);
            }
            if (!cycleReceipts.TryEnsureFailedOutcomeAcknowledged(
                    state, out string outcomeFailure))
            {
                state.BlockedReason = outcomeFailure;
                snapshotsDirty = true;
                return false;
            }
        }

        // Destructive mutation must be fenced by the shared facility lifecycle
        // query before the plot disappears. If a caller bypasses that fence,
        // retain the exact frozen/committed owner instead of deleting output or
        // inventing a loose fallback location.

        if (!waterRuntime.TryRetireInactiveWaterRefill(state))
            return false;

        if (state.Treatment.phase is CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted)
        {
            treatmentRuntime.TryFinalizeTreatment(state);
            if (state.Treatment.phase != CropTreatmentOrderPhase.None)
                return false;
        }
        else if (state.Treatment.phase is CropTreatmentOrderPhase.InputCommitted
                or CropTreatmentOrderPhase.PlotDestroyedLossPending)
        {
            if (!physicalInputs.TryRetireDestination(
                    state,
                    state.Treatment.destinationId,
                    CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode,
                    out string treatmentRetireFailure))
            {
                state.Treatment.failureReason = treatmentRetireFailure;
                snapshotsDirty = true;
                return false;
            }
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
            if (!physicalInputs.TryRetireDestination(
                    state,
                    state.Treatment.destinationId,
                    CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode,
                    out string treatmentRetireFailure))
            {
                state.Treatment.failureReason = treatmentRetireFailure;
                snapshotsDirty = true;
                return false;
            }
            CropTreatmentPhysicalOutbox.Clear(state.Treatment);
        }

        if (state.PendingSow.phase == CropPhysicalCommitPhase.OutcomePublished)
        {
            sowRuntime.FinalizePublishedSow(state);
            if (state.PendingSow.phase != CropPhysicalCommitPhase.None)
                return false;
        }

        if (state.PendingSow.phase is CropPhysicalCommitPhase.InputCommitted
                or CropPhysicalCommitPhase.PlotDestroyedLossPending)
        {
            if (!physicalInputs.TryRetireDestination(
                    state,
                    state.MaterialDestinationId,
                    CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode,
                    out string sowRetireFailure))
            {
                state.BlockedReason = sowRetireFailure;
                snapshotsDirty = true;
                return false;
            }
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

        if (state.CycleExecutionReceipt != null
            && !state.CycleExecutionReceipt.IsEmpty)
        {
            CropPlanExecutionReceiptAuthority.Validate(
                state.CycleExecutionReceipt,
                requireCompleted: false);
            if (IsFailedCycleReceipt(state.CycleExecutionReceipt))
            {
                state.Phase = CropPlotPhase.Blocked;
                state.SeasonalYieldDamage =
                    new CropSeasonalYieldDamageSaveData();
                if (!cycleReceipts.TryAdvanceFailedCycle(state))
                    return false;
            }
            if (state.CycleExecutionReceipt.explicitCorrelation)
            {
                state.BlockedReason =
                    "crop-cycle-execution-receipt-awaiting-acknowledgement";
                snapshotsDirty = true;
                return false;
            }
            state.CycleExecutionReceipt =
                new CropCycleExecutionReceiptSaveData();
        }

        return state.PendingSow.phase == CropPhysicalCommitPhase.None
            && state.Treatment.phase == CropTreatmentOrderPhase.None
            && state.WaterRefill.phase == CropWaterRefillPhase.None;
    }

    private void RemoveFinalizedDestroyedPlot(BuildingInstanceId plotId)
    {
        if (!states.TryGetValue(plotId, out CropPlotState state))
            return;
        if (!physicalInputs.TryRetireDestination(
                state,
                state.MaterialDestinationId,
                CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode,
                out string materialRetireFailure))
            throw new InvalidOperationException(
                "Crop-plot destroyed material owner retirement failed: "
                + materialRetireFailure);
        string treatmentDestination = state.Treatment?.destinationId
            ?? string.Empty;
        if (treatmentDestination.Length > 0
            && !physicalInputs.TryRetireDestination(
                state,
                treatmentDestination,
                CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode,
                out string treatmentRetireFailure))
            throw new InvalidOperationException(
                "Crop-plot destroyed treatment owner retirement failed: "
                + treatmentRetireFailure);
        string waterDestination = state.WaterRefill?.destinationId
            ?? string.Empty;
        if (waterDestination.Length > 0
            && !physicalInputs.TryRetireDestination(
                state,
                waterDestination,
                CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode,
                out string waterRetireFailure))
            throw new InvalidOperationException(
                "Crop-plot destroyed water-refill owner retirement failed: "
                + waterRetireFailure);
        ecology.AbandonPlot(plotId.Value);
        string publicationId = string.Empty;
        string fingerprint = string.Empty;
        if (!factPublication.TryDeliverCropFact(new CropPlotRetiredFact(plotId), ref state.RetirementPublicationKey,
            ref publicationId, ref fingerprint, out string failure))
        {
            state.BlockedReason = failure;
            return;
        }
        states.Remove(plotId);
        mutation.MarkChanged();
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

}
