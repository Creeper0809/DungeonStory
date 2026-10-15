using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

internal sealed class CropPlotState
{
    public BuildingInstanceId PlotId;
    public BuildableObject Building;
    public BuildingCropPlotAbility Ability;
    public string PlotDisplayName = string.Empty;
    public string RetirementPublicationKey = string.Empty;
    public Vector2Int LastKnownPosition;
    public string CropId = string.Empty;
    public int NextCropSelectionOperationSequence;
    public CropSelectionCommandSaveData CropSelectionOutcome = new();
    public CropPlotPhase Phase;
    public float SowWork;
    public float GrowthHours;
    public float HarvestWork;
    public string MaterialDestinationId = string.Empty;
    public bool MaterialsConsumed;
    public int FrozenSowInputOperationSequence = -1;
    public string FrozenSowInputSourceDigest = string.Empty;
    public string FrozenSowInputVectorDigest = string.Empty;
    public SurvivalWeatherType FrozenSowInputWeather;
    public float FrozenSowInputConsumptionMultiplier;
    public string FrozenSowInputSelectedFuelItemId = string.Empty;
    public Dictionary<string, int> FrozenSowInputs = new(StringComparer.Ordinal);
    public string BlockedReason = string.Empty;
    public string GoldenHarvestHarvesterId = string.Empty;
    public int GoldenHarvestAttemptSequence;
    public CropGoldenHarvestScheduleSaveData GoldenHarvestScheduleOutcome = new();
    public int NextSowOperationSequence;
    public CropPhysicalCommitSaveData PendingSow = new();
    public string PendingCycleCorrelationId = string.Empty;
    public CropCycleExecutionReceiptSaveData CycleExecutionReceipt = new();
    public CropFailedOutcomeSaveData FailedOutcome = new();
    public float CurrentWater;
    public float WaterCapacity = CropWaterRules.WaterCapacity;
    public int NextWaterRefillOperationSequence;
    public CropIrrigationPublicationSaveData IrrigationPublication = new();
    public CropWaterRefillSaveData WaterRefill = new();
    public int NextTreatmentOperationSequence;
    public int PestLureNextAllowedDay;
    public int BotanicalPesticideNextAllowedDay;
    public int FungicideNextAllowedDay;
    public CropTreatmentOrderSaveData Treatment = new();
    public int NextHarvestOperationSequence;
    public CropHarvestOutputSaveData PendingHarvest = new();
    public CropSeasonalYieldDamageSaveData SeasonalYieldDamage = new();
}

internal sealed class CropPlotAggregateState
{
    internal Dictionary<BuildingInstanceId, CropPlotState> States { get; } =
        new();
    internal Dictionary<BuildableObject, CropPlotState> StatesByBuilding { get; } =
        new();
    internal List<CropPlotSnapshot> Snapshots { get; } = new();
    internal HashSet<string> HandledSeasonalStartInstanceIds { get; } =
        new(StringComparer.Ordinal);
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
        ICropCycleInputRequirementQuery inputRequirements,
        IProductionItemGateway items,
        IPhysicalSeedLotGateway seedLots,
        IPhysicalFacilityItemSinkGateway treatmentItems,
        IPackagedLotTareDispositionService packagedTare,
        ICropEcologyService ecology,
        ICropEcologyHarvestTransactionService ecologyHarvests,
        IProductionOutputCapabilityRegistry outputCapabilities,
        IProductionDomainOutputPublicationService outputPublication,
        ICropPlotInputOwnerRuntime inputOwners,
        ICharacterPerformanceDefinitionMaximumQuery performanceMaximum,
        IGameplayEffectResultBoundsQuery effectBounds,
        IFacilityCapabilityQuery facilities,
        ICropIrrigationRuntime irrigation,
        IFacilityCandidateCache facilityCandidates,
        IWorkforceReplanService workforce)
    {
        BuildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        InputRequirements = inputRequirements
            ?? throw new ArgumentNullException(nameof(inputRequirements));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        SeedLots = seedLots ?? throw new ArgumentNullException(nameof(seedLots));
        TreatmentItems = treatmentItems
            ?? throw new ArgumentNullException(nameof(treatmentItems));
        PackagedTare = packagedTare
            ?? throw new ArgumentNullException(nameof(packagedTare));
        Ecology = ecology ?? throw new ArgumentNullException(nameof(ecology));
        EcologyHarvests = ecologyHarvests
            ?? throw new ArgumentNullException(nameof(ecologyHarvests));
        OutputCapabilities = outputCapabilities
            ?? throw new ArgumentNullException(nameof(outputCapabilities));
        OutputPublication = outputPublication
            ?? throw new ArgumentNullException(nameof(outputPublication));
        InputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        PerformanceMaximum = performanceMaximum
            ?? throw new ArgumentNullException(nameof(performanceMaximum));
        EffectBounds = effectBounds
            ?? throw new ArgumentNullException(nameof(effectBounds));
        Facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        Irrigation = irrigation ?? throw new ArgumentNullException(nameof(irrigation));
        FacilityCandidates = facilityCandidates
            ?? throw new ArgumentNullException(nameof(facilityCandidates));
        Workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
    }

    public IBuildingWorldQuery BuildingWorld { get; }
    public IResourceEconomyContentCatalog Catalog { get; }
    public ICropCycleInputRequirementQuery InputRequirements { get; }
    public IProductionItemGateway Items { get; }
    public IPhysicalSeedLotGateway SeedLots { get; }
    public IPhysicalFacilityItemSinkGateway TreatmentItems { get; }
    public IPackagedLotTareDispositionService PackagedTare { get; }
    public ICropEcologyService Ecology { get; }
    public ICropEcologyHarvestTransactionService EcologyHarvests { get; }
    public IProductionOutputCapabilityRegistry OutputCapabilities { get; }
    public IProductionDomainOutputPublicationService OutputPublication { get; }
    public ICropPlotInputOwnerRuntime InputOwners { get; }
    public ICharacterPerformanceDefinitionMaximumQuery PerformanceMaximum { get; }
    public IGameplayEffectResultBoundsQuery EffectBounds { get; }
    public IFacilityCapabilityQuery Facilities { get; }
    public ICropIrrigationRuntime Irrigation { get; }
    public IFacilityCandidateCache FacilityCandidates { get; }
    public IWorkforceReplanService Workforce { get; }
}

public sealed class CropPlotSimulationDependencies
{
    public CropPlotSimulationDependencies(
        IGameClock gameClock,
        IGameCalendar calendar,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IGameSessionStateProvider gameDataProvider,
        ISurvivalEnvironmentQuery environmentQuery,
        IEnvironmentalFieldQuery environmentalField,
        IGrandProjectBenefitQuery grandProjectBenefits,
        IGameEventBus events,
        ISeasonalEventQuery seasonalEvents,
        V20StoryContentCatalog storyCatalog,
        ExtremeTraitRuntime extremeTraits = null,
        IRunSeedProvider runSeedProvider = null)
    {
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        ProgressionRuntimes = progressionRuntimes
            ?? throw new ArgumentNullException(nameof(progressionRuntimes));
        GameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        EnvironmentQuery = environmentQuery
            ?? throw new ArgumentNullException(nameof(environmentQuery));
        EnvironmentalField = environmentalField
            ?? throw new ArgumentNullException(nameof(environmentalField));
        GrandProjectBenefits = grandProjectBenefits
            ?? throw new ArgumentNullException(nameof(grandProjectBenefits));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        SeasonalEvents = seasonalEvents
            ?? throw new ArgumentNullException(nameof(seasonalEvents));
        StoryCatalog = storyCatalog
            ?? throw new ArgumentNullException(nameof(storyCatalog));
        ExtremeTraits = extremeTraits;
        RunSeedProvider = runSeedProvider;
    }

    public IGameClock GameClock { get; }
    public IGameCalendar Calendar { get; }
    public ProgressionSceneRuntimeReferences ProgressionRuntimes { get; }
    public IGameSessionStateProvider GameDataProvider { get; }
    public ISurvivalEnvironmentQuery EnvironmentQuery { get; }
    public IEnvironmentalFieldQuery EnvironmentalField { get; }
    public IGrandProjectBenefitQuery GrandProjectBenefits { get; }
    public IGameEventBus Events { get; }
    public ISeasonalEventQuery SeasonalEvents { get; }
    public V20StoryContentCatalog StoryCatalog { get; }
    public ExtremeTraitRuntime ExtremeTraits { get; }
    public IRunSeedProvider RunSeedProvider { get; }
}

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
    public const string WaterRefillOperationPrefix = "crop-water-refill:";
    public const string IrrigationOperationPrefix = "crop-irrigation:";
    public const string CropSelectionOperationPrefix = "crop-selection:";
    public const string GoldenHarvestScheduleOperationPrefix =
        "golden-harvest-schedule:";

    private const float MaterialRequestInterval = 0.5f;
    public const string HarvestOutputBatchCommitPrefix =
        ProductionDomainOutputPublicationIdentity.BatchCommitPrefix
        + "crop-harvest:";
    public const string HarvestOutputPublicationOperationPrefix =
        ProductionDomainOutputPublicationIdentity.PublicationOperationPrefix
        + "crop-harvest:";

    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly ICropCycleInputRequirementQuery inputRequirements;
    private readonly IProductionItemGateway items;
    private readonly IPhysicalSeedLotGateway seedLots;
    private readonly IPhysicalFacilityItemSinkGateway treatmentItems;
    private readonly IPackagedLotTareDispositionService packagedTare;
    private readonly ICropEcologyService ecology;
    private readonly ICropEcologyHarvestTransactionService ecologyHarvests;
    private readonly IProductionOutputCapabilityRegistry outputCapabilities;
    private readonly IProductionDomainOutputPublicationService outputPublication;
    private readonly ICropPlotInputOwnerRuntime inputOwners;
    private readonly ICharacterPerformanceDefinitionMaximumQuery performanceMaximum;
    private readonly IGameplayEffectResultBoundsQuery effectBounds;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly ICropIrrigationRuntime irrigation;
    private readonly ICropIrrigationSupplyTransaction irrigationTransactions;
    private readonly IGameClock gameClock;
    private readonly IGameCalendar calendar;
    private readonly BlueprintResearchRuntime research;
    private readonly ISurvivalEnvironmentQuery environmentQuery;
    private readonly IEnvironmentalFieldQuery environmentalField;
    private readonly IFacilityCandidateCache facilityCandidates;
    private readonly IWorkforceReplanService workforce;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly IGameEventBus events;
    private readonly ISeasonalEventQuery seasonalEvents;
    private readonly V20StoryContentCatalog storyCatalog;
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly IRunSeedProvider runSeedProvider;
    private readonly ICharacterPerformanceQuery performance;
    private readonly IProductionFacilityMutationEpochQuery facilityMutations;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private IDomainFactPublisher domainFacts;

    [VContainer.Inject]
    public void ConstructDomainFacts(IDomainFactPublisher publisher) =>
        domainFacts = publisher ?? throw new ArgumentNullException(nameof(publisher));

    private IDomainFactPublisher RequireDomainFacts() => domainFacts
        ?? throw new InvalidOperationException("Crop domain-fact publisher is not registered.");
    private IDisposable dayEndedSubscription;
    private IDisposable contentEffectsResolvedSubscription;
    private int observedEnvironmentalFieldVersion = -1;
    private int observedIrrigationVersion = -1;

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

    public string OutputOwnerDomainId => "economy.crop-harvest";
    public string OutputBatchCommitPrefix => HarvestOutputBatchCommitPrefix;

    public IReadOnlyList<ProductionDomainOutputRestoreOwnerSnapshot>
        CapturePendingOutputOwners() => states.Values
        .Where(state => state?.PendingHarvest != null
            && (state.PendingHarvest.phase ==
                    CropHarvestOutputPhase.OutputCommitted
                && state.PendingHarvest.outputPublication is
                    { outputAcknowledged: false }
                || state.PendingHarvest.phase ==
                    CropHarvestOutputPhase.OutputRestoredAwaitingFinalization
                && state.PendingHarvest.outputPublication is
                    {
                        outputAcknowledged: true,
                        restoredInCurrentTransaction: true
                    }))
        .OrderBy(state => state.PendingHarvest.operationId, StringComparer.Ordinal)
        .Select(state => new ProductionDomainOutputRestoreOwnerSnapshot(
            state.PendingHarvest.operationId,
            state.PendingHarvest.outputPublication,
            CaptureHarvestMaximumMassClaims(state.PendingHarvest)))
        .ToArray();

    public IReadOnlyList<ProductionDomainOutputFacilityOwnerSnapshot>
        CaptureActiveOutputOwners(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "A valid crop-plot facility ID is required.",
                nameof(facilityId));
        return states.Values
            .Where(state => state != null
                && state.PlotId.Equals(facilityId)
                && state.PendingHarvest != null
                && state.PendingHarvest.phase != CropHarvestOutputPhase.None)
            .OrderBy(
                state => state.PendingHarvest.operationId,
                StringComparer.Ordinal)
            .Select(state =>
            {
                ValidateActiveHarvestOwner(state);
                return new ProductionDomainOutputFacilityOwnerSnapshot(
                    OutputOwnerDomainId,
                    state.PendingHarvest.operationId,
                    facilityId,
                    CaptureHarvestLifecycleFingerprint(state));
            })
            .ToArray();
    }

    public IReadOnlyList<CropPlotInputOwnerDescriptor>
        BuildLiveInputOwnerDescriptors()
    {
        SynchronizePlots(force: false);
        return states.Values
            .Where(RequiresAnyInputAuthority)
            .OrderBy(value => value.PlotId.Value, StringComparer.Ordinal)
            .SelectMany(value => BuildInputOwnerDescriptors(
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
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        BuildableObject[] buildings = (detachedBuildings
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null && !value.IsBuildingDestroyed)
            .OrderBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToArray();
        List<CropPlotInputOwnerDescriptor> descriptors = new();
        foreach (CropPlotState state in candidate.State.States.Values
                     .Where(RequiresAnyInputAuthority)
                     .OrderBy(value => value.PlotId.Value,
                         StringComparer.Ordinal))
        {
            BuildableObject[] matches = buildings.Where(value =>
                    value.PersistentInstanceId.Equals(state.PlotId))
                .ToArray();
            BuildingCropPlotAbility ability = matches.Length == 1
                ? matches[0].BuildingData?
                    .GetAbility<BuildingCropPlotAbility>()
                : null;
            if (matches.Length != 1 || ability == null)
            {
                throw new InvalidOperationException(
                    "Crop-plot input restore requires one exact live facility: "
                    + state.PlotId.Value);
            }
            descriptors.AddRange(BuildInputOwnerDescriptors(
                state,
                matches[0],
                ability));
        }
        return descriptors
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryBindNextCycle(
        string correlationId,
        string plotId,
        string cropId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(correlationId)
            || !string.Equals(
                correlationId,
                correlationId.Trim(),
                StringComparison.Ordinal)
            || correlationId.Any(char.IsWhiteSpace)
            || string.IsNullOrWhiteSpace(plotId)
            || !string.Equals(plotId, plotId.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(cropId)
            || !string.Equals(cropId, cropId.Trim(), StringComparison.Ordinal))
        {
            failureReason = "crop-cycle-correlation-invalid";
            return false;
        }
        if (!states.TryGetValue(
                (BuildingInstanceId)plotId,
                out CropPlotState state)
            || state == null
            || !string.IsNullOrEmpty(state.RetirementPublicationKey)
            || state.MaterialsConsumed
            || state.PendingSow.phase != CropPhysicalCommitPhase.None
            || state.WaterRefill.phase != CropWaterRefillPhase.None
            || state.Phase is CropPlotPhase.Sowing
                or CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting
            || !string.Equals(state.CropId, cropId, StringComparison.Ordinal))
        {
            failureReason = "crop-cycle-correlation-state-conflict";
            return false;
        }
        if (!TryRequireMutable(state.PlotId, out failureReason))
            return false;
        if (state.GoldenHarvestScheduleOutcome is { IsEmpty: false }
            && !TryAdvanceGoldenHarvestScheduleOutcome(state))
        {
            failureReason = "이전 황금 수확 예약 결과를 원장에 확정하는 중입니다: "
                + state.GoldenHarvestScheduleOutcome.failureReason;
            return false;
        }
        CropPlotState[] existingOwners = states.Values
            .Where(candidate => candidate != null
                && (string.Equals(
                        candidate.PendingCycleCorrelationId,
                        correlationId,
                        StringComparison.Ordinal)
                    || candidate.CycleExecutionReceipt != null
                    && !candidate.CycleExecutionReceipt.IsEmpty
                    && string.Equals(
                        candidate.CycleExecutionReceipt.correlationId,
                        correlationId,
                        StringComparison.Ordinal)))
            .ToArray();
        if (existingOwners.Length > 1
            || existingOwners.Length == 1
            && !ReferenceEquals(existingOwners[0], state))
        {
            failureReason = "crop-cycle-correlation-global-conflict";
            return false;
        }
        if (!string.IsNullOrEmpty(state.PendingCycleCorrelationId)
            && !string.Equals(
                state.PendingCycleCorrelationId,
                correlationId,
                StringComparison.Ordinal))
        {
            failureReason = "crop-cycle-correlation-already-bound";
            return false;
        }
        if (state.CycleExecutionReceipt != null
            && !state.CycleExecutionReceipt.IsEmpty)
        {
            CropPlanExecutionReceiptAuthority.Validate(
                state.CycleExecutionReceipt,
                requireCompleted: false);
            if (state.CycleExecutionReceipt.status
                    == CropCycleExecutionReceiptStatus.Active)
            {
                failureReason = "crop-cycle-execution-receipt-active";
                return false;
            }
            if (IsFailedCycleReceipt(state.CycleExecutionReceipt))
            {
                failureReason = "crop-cycle-failed-outcome-awaiting-acknowledgement";
                return false;
            }
            if (state.CycleExecutionReceipt.explicitCorrelation)
            {
                failureReason = "crop-cycle-execution-receipt-unacknowledged";
                return false;
            }
            state.CycleExecutionReceipt =
                new CropCycleExecutionReceiptSaveData();
        }
        state.PendingCycleCorrelationId = correlationId;
        MarkChanged(replan: false);
        return true;
    }

    public bool TryCaptureExecutionReceipt(
        string actionId,
        out CropPlanExecutionReceipt receipt)
    {
        receipt = null;
        if (string.IsNullOrWhiteSpace(actionId)
            || !string.Equals(actionId, actionId.Trim(), StringComparison.Ordinal)
            || actionId.Any(char.IsWhiteSpace))
        {
            return false;
        }

        CropPlotState[] owners = states.Values
            .Where(candidate => candidate?.CycleExecutionReceipt != null
                && !candidate.CycleExecutionReceipt.IsEmpty
                && candidate.CycleExecutionReceipt.status
                    != CropCycleExecutionReceiptStatus.Active
                && string.Equals(
                    candidate.CycleExecutionReceipt.correlationId,
                    actionId,
                    StringComparison.Ordinal))
            .ToArray();
        if (owners.Length > 1)
            throw new InvalidOperationException(
                "Crop execution correlation has multiple durable owners.");
        if (owners.Length == 0)
            return false;

        receipt = new CropPlanExecutionReceipt(
            actionId,
            owners[0].CycleExecutionReceipt);
        return true;
    }

    public bool TryAcknowledgeExecutionReceipt(
        string correlationId,
        string expectedRuntimeReceiptDigest,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(correlationId)
            || !string.Equals(
                correlationId,
                correlationId.Trim(),
                StringComparison.Ordinal)
            || correlationId.Any(char.IsWhiteSpace))
        {
            failureReason = "crop-cycle-correlation-invalid";
            return false;
        }

        CropPlotState[] owners = states.Values
            .Where(candidate => candidate?.CycleExecutionReceipt != null
                && !candidate.CycleExecutionReceipt.IsEmpty
                && string.Equals(
                    candidate.CycleExecutionReceipt.correlationId,
                    correlationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (owners.Length != 1)
        {
            failureReason = owners.Length == 0
                ? "crop-cycle-execution-receipt-not-found"
                : "crop-cycle-correlation-global-conflict";
            return false;
        }
        CropPlanExecutionReceiptAuthority.Validate(
            owners[0].CycleExecutionReceipt,
            requireCompleted: false);
        if (owners[0].IrrigationPublication.pending)
        {
            failureReason = "crop-cycle-irrigation-publication-pending";
            return false;
        }
        if (!ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                expectedRuntimeReceiptDigest)
            || !string.Equals(
                owners[0].CycleExecutionReceipt.sourceDigest,
                expectedRuntimeReceiptDigest,
                StringComparison.Ordinal))
        {
            failureReason = "crop-cycle-execution-receipt-digest-mismatch";
            return false;
        }
        if (owners[0].CycleExecutionReceipt.status
            == CropCycleExecutionReceiptStatus.Active)
        {
            failureReason = "crop-cycle-execution-receipt-not-terminal";
            return false;
        }

        if (IsFailedCycleReceipt(owners[0].CycleExecutionReceipt)
            && (!TryEnsureFailedOutcomeAcknowledged(
                    owners[0], out failureReason)
                || !TryCloseFailedCyclePhysicalOwners(
                    owners[0], out failureReason)))
            return false;

        owners[0].CycleExecutionReceipt =
            new CropCycleExecutionReceiptSaveData();
        owners[0].FailedOutcome = new CropFailedOutcomeSaveData();
        MarkChanged(replan: false);
        return true;
    }

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
        inputRequirements = world.InputRequirements;
        items = world.Items;
        seedLots = world.SeedLots;
        treatmentItems = world.TreatmentItems;
        packagedTare = world.PackagedTare;
        ecology = world.Ecology;
        ecologyHarvests = world.EcologyHarvests;
        outputCapabilities = world.OutputCapabilities;
        outputPublication = world.OutputPublication;
        inputOwners = world.InputOwners;
        performanceMaximum = world.PerformanceMaximum;
        effectBounds = world.EffectBounds;
        facilities = world.Facilities;
        irrigation = world.Irrigation;
        irrigationTransactions = world.Irrigation as
                ICropIrrigationSupplyTransaction
            ?? throw new InvalidOperationException(
                $"{nameof(CropPlotRuntime)} requires the exact irrigation supply transaction.");
        facilityCandidates = world.FacilityCandidates;
        workforce = world.Workforce;
        gameClock = simulation.GameClock;
        calendar = simulation.Calendar;
        research = simulation.ProgressionRuntimes
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(CropPlotRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        environmentQuery = simulation.EnvironmentQuery;
        environmentalField = simulation.EnvironmentalField;
        grandProjectBenefits = simulation.GrandProjectBenefits;
        events = simulation.Events;
        seasonalEvents = simulation.SeasonalEvents;
        storyCatalog = simulation.StoryCatalog;
        extremeTraits = simulation.ExtremeTraits;
        runSeedProvider = simulation.RunSeedProvider;
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.facilityMutations = facilityMutations
            ?? throw new ArgumentNullException(nameof(facilityMutations));
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
            SynchronizePlots(force: false);
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
        if (!TryRequireMutable(state.PlotId, out message))
            return false;
        if (state.CropSelectionOutcome is { IsEmpty: false }
            && !TryAdvanceCropSelectionOutcome(state))
        {
            message = "이전 작물 지정 결과를 원장에 확정하는 중입니다: "
                + state.CropSelectionOutcome.failureReason;
            return false;
        }

        if (state.MaterialsConsumed
            || state.PendingSow.phase != CropPhysicalCommitPhase.None
            || state.WaterRefill.phase != CropWaterRefillPhase.None
            || state.Treatment.phase != CropTreatmentOrderPhase.None
            || state.FailedOutcome != null
                && !state.FailedOutcome.IsEmpty
            || state.CycleExecutionReceipt != null
                && !state.CycleExecutionReceipt.IsEmpty
                && state.CycleExecutionReceipt.explicitCorrelation
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

        if (string.Equals(state.CropId, crop.CropId, StringComparison.Ordinal))
        {
            message = $"{crop.DisplayName} 재배가 이미 지정되어 있습니다.";
            return true;
        }

        int selectionSequence = state.NextCropSelectionOperationSequence;
        string selectionOperationId = CropSelectionOperationPrefix
            + state.PlotId.Value + ":"
            + selectionSequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
        var pendingSelection = new CropSelectionCommandSaveData
        {
            phase = CropCommandOutcomePhase.MutationApplied,
            operationSequence = selectionSequence, operationId = selectionOperationId,
            cropBefore = state.CropId, cropAfter = crop.CropId,
            waterBefore = state.CurrentWater, waterAfter = 0f,
            completedAbsoluteDay = CurrentAbsoluteDay,
            plotDisplayName = state.PlotDisplayName, cropDisplayName = crop.DisplayName,
            locationX = state.LastKnownPosition.x, locationY = state.LastKnownPosition.y
        };
        IDomainFactPublisher publisher = RequireDomainFacts();
        if (!publisher.TryReserve(new CropSelectionChangedFact(state.PlotId, pendingSelection),
                out DomainFactLease selectionReservation, out string selectionReserveFailure))
        {
            message = "작물 지정 결과를 예약할 수 없습니다: " + selectionReserveFailure;
            return false;
        }
        pendingSelection.publicationKey = selectionReservation.PublicationKey;

        if (!TryRetireDestination(
                state,
                state.MaterialDestinationId,
                CropPlotInputOwnerAuthority.CropChangedReleaseReasonCode,
                out string retireFailure))
        {
            publisher.Cancel(selectionReservation);
            message = "기존 경작지 입력 소유권을 종료할 수 없습니다: "
                + retireFailure;
            return false;
        }
        state.CropId = crop.CropId;
        state.NextSowOperationSequence = checked(
            state.NextSowOperationSequence + 1);
        state.MaterialDestinationId = BuildSowDestinationId(
            state.PlotId,
            state.NextSowOperationSequence);
        state.Phase = CropPlotPhase.Empty;
        state.SowWork = 0f;
        state.GrowthHours = 0f;
        state.HarvestWork = 0f;
        state.MaterialsConsumed = false;
        state.CurrentWater = 0f;
        state.WaterCapacity = CropWaterRules.WaterCapacity;
        state.WaterRefill = new CropWaterRefillSaveData();
        ClearFrozenSowInputs(state);
        state.PendingCycleCorrelationId = string.Empty;
        state.CycleExecutionReceipt = new CropCycleExecutionReceiptSaveData();
        state.SeasonalYieldDamage =
            new CropSeasonalYieldDamageSaveData();
        state.BlockedReason = string.Empty;
        state.CropSelectionOutcome = pendingSelection;
        MarkChanged();
        bool recorded = TryAdvanceCropSelectionOutcome(
            state,
            selectionReservation);
        message = recorded
            ? $"{crop.DisplayName} 재배를 지정했습니다."
            : $"{crop.DisplayName} 재배 지정은 적용됐지만 결과 원장 확정이 대기 중입니다: "
                + state.CropSelectionOutcome.failureReason;
        return recorded;
    }

    private bool TryAdvanceCropSelectionOutcome(
        CropPlotState state, DomainFactLease reserved = default)
    {
        CropSelectionCommandSaveData owner = state?.CropSelectionOutcome;
        if (owner == null || owner.IsEmpty) return true;
        DomainFactDelivery delivery = DeliverCropCommandFact(reserved, owner.publicationKey,
            new CropSelectionChangedFact(state.PlotId, owner));
        if (!ConfirmCropCommandPublication(delivery, owner.publicationKey,
                owner.gameplayOutcomeId, owner.gameplayOutcomeDigest, out string failure))
        {
            owner.failureReason = failure;
            snapshotsDirty = true;
            return false;
        }
        owner.phase = CropCommandOutcomePhase.LedgerCommitted;
        owner.gameplayOutcomeId = delivery.PublicationId;
        owner.gameplayOutcomeDigest = delivery.Fingerprint;
        if (delivery.Phase != DomainFactDeliveryPhase.Delivered)
        {
            owner.failureReason = delivery.Detail;
            snapshotsDirty = true;
            return false;
        }
        state.NextCropSelectionOperationSequence = checked(owner.operationSequence + 1);
        state.CropSelectionOutcome = new CropSelectionCommandSaveData();
        MarkChanged(replan: false);
        return true;
    }

    private DomainFactDelivery DeliverCropCommandFact<TFact>(
        in DomainFactLease reservation, string publicationKey, TFact fact)
    {
        IDomainFactPublisher publisher = RequireDomainFacts();
        try
        {
            if (reservation.IsValid) return publisher.Publish(reservation, fact);
            if (publisher is not IDomainFactRecoveryPublisher recovery)
                throw new InvalidOperationException("Crop command facts require exact-key recovery.");
            var results = new DomainFactDelivery[1];
            return recovery.ReplayBatch(new[] { DomainFactReplay.Create(publicationKey, fact) },
                results, out string failure) ? results[0] : DomainFactDelivery.Reject(failure);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            // The authoritative crop command has already changed state. Retain
            // its frozen fact/key; an unknown observer ACK never redoes it.
            return DomainFactDelivery.Uncertain("crop-command-publication-unconfirmed:" + exception.Message);
        }
    }

    private static bool ConfirmCropCommandPublication(DomainFactDelivery delivery,
        string publicationKey, string previousId, string previousFingerprint, out string failure)
    {
        bool known = (delivery.Phase is DomainFactDeliveryPhase.Delivered
                or DomainFactDeliveryPhase.AcceptedPendingDelivery)
            && delivery.OperationKey == publicationKey
            && !string.IsNullOrEmpty(delivery.PublicationId)
            && IsCropCommandFingerprint(delivery.Fingerprint)
            && (string.IsNullOrEmpty(previousId) || delivery.PublicationId == previousId)
            && (string.IsNullOrEmpty(previousFingerprint) || delivery.Fingerprint == previousFingerprint);
        failure = known ? string.Empty : "crop-command-unconfirmed:" + delivery.Detail;
        return known;
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
        if (!TryRequireMutable(state.PlotId, out reason))
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
            CreateTreatmentInputOwnerDescriptor(state, treatment);
        if (!inputOwners.TryEnsure(descriptor, out string ownerFailure))
        {
            message = "처리제 목적지 소유권을 만들 수 없습니다: "
                + ownerFailure;
            return false;
        }
        state.Treatment = treatment;
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
                or CropTreatmentOrderPhase.LedgerCommitted
                or CropTreatmentOrderPhase.PlotDestroyedLossPending)
        {
            message = "이미 물리 소비가 확정된 처리는 취소할 수 없습니다.";
            return false;
        }
        if (!TryRequireMutable(state.PlotId, out message))
            return false;

        if (!TryRetireDestination(
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

        bool mutable = TryRequireMutable(
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
                    : !mutable
                        ? mutationReason
                        : ResolveTreatmentUnavailableReason(state));
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
                        : ResolveWaterRefillUnavailableReason(state));
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
            if (!TryRequireMutable(state.PlotId, out _))
                return false;
            state.Phase = CropPlotPhase.Sowing;
            float sowBefore = state.SowWork;
            state.SowWork = Mathf.Min(
                crop.SowWork,
                state.SowWork + amount);
            RecordCycleContribution(
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

            MarkChanged();
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Harvest
            && state.Phase is CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting)
        {
            if (state.PendingHarvest.phase != CropHarvestOutputPhase.None)
            {
                TryAdvancePendingHarvest(state, crop, out cycleCompleted);
                MarkChanged();
                return true;
            }
            if (!TryRequireMutable(state.PlotId, out _))
                return false;
            state.Phase = CropPlotPhase.Harvesting;
            float harvestBefore = state.HarvestWork;
            state.HarvestWork = Mathf.Min(
                crop.HarvestWork,
                state.HarvestWork + amount);
            RecordCycleContribution(
                state,
                worker,
                Mathf.Max(0f, state.HarvestWork - harvestBefore),
                harvest: true);
            if (state.HarvestWork + 0.001f >= crop.HarvestWork)
            {
                state.HarvestWork = crop.HarvestWork;
                if (FreezeHarvestOutcome(state, crop, worker))
                    TryAdvancePendingHarvest(state, crop, out cycleCompleted);
            }

            MarkChanged();
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
                bool finalized = TryFinalizeTreatment(state);
                cycleCompleted = finalized;
                MarkChanged();
                return finalized;
            }
            if (!TryRequireMutable(state.PlotId, out _))
                return false;
            state.Treatment.phase = CropTreatmentOrderPhase.Working;
            float treatmentBefore = state.Treatment.completedWork;
            state.Treatment.completedWork = Mathf.Min(
                state.Treatment.requiredWork,
                state.Treatment.completedWork + amount);
            RecordTreatmentContribution(
                state.Treatment,
                worker,
                Mathf.Max(
                    0f,
                    state.Treatment.completedWork - treatmentBefore));
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

        if (workTypeId == BuiltInWorkTypeIds.Treat
            && state.Phase == CropPlotPhase.Growing
            && state.WaterRefill.phase is CropWaterRefillPhase.ReadyForWork
                or CropWaterRefillPhase.Working)
        {
            if (state.WaterRefill.phase == CropWaterRefillPhase.Working
                && state.WaterRefill.completedWork + 0.001f
                    >= state.WaterRefill.requiredWork)
            {
                bool finalized = TryFinalizeWaterRefill(state);
                cycleCompleted = finalized;
                MarkChanged();
                return finalized;
            }
            if (!TryRequireMutable(state.PlotId, out _))
                return false;
            state.WaterRefill.phase = CropWaterRefillPhase.Working;
            float refillWorkBefore = state.WaterRefill.completedWork;
            state.WaterRefill.completedWork = Mathf.Min(
                state.WaterRefill.requiredWork,
                state.WaterRefill.completedWork + amount);
            RecordWaterRefillContribution(
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
                bool outcomePublished = TryFinalizeWaterRefill(state);
                cycleCompleted = outcomePublished;
                MarkChanged();
                return outcomePublished;
            }

            MarkChanged();
            return true;
        }

        return false;
    }

    private bool FreezeHarvestOutcome(
        CropPlotState state,
        CropDefinitionSO crop,
        CharacterActor worker)
    {
        if (state.PendingHarvest.phase != CropHarvestOutputPhase.None)
            return true;
        string operationId = FormatHarvestOperationId(
            state.PlotId,
            state.NextHarvestOperationSequence);
        float extremeYieldMultiplier = 1f;
        float extremeSeedMultiplier = 1f;
        string[] yieldConditions = Array.Empty<string>();
        GoldenHarvestPreparedResolution golden = default;
        bool expectsGolden = !string.IsNullOrWhiteSpace(
            state.GoldenHarvestHarvesterId);
        bool goldenPrepared = expectsGolden
            && extremeTraits != null
            && runSeedProvider != null
            && worker != null
            && string.Equals(
                state.GoldenHarvestHarvesterId,
                worker.Identity?.PersistentId,
                StringComparison.Ordinal)
            && extremeTraits.TryPrepareGoldenHarvest(
                worker,
                state.PlotId.Value,
                operationId,
                unchecked((ulong)(uint)runSeedProvider.RunSeed),
                gameClock.Time,
                out golden);
        if (expectsGolden && !goldenPrepared)
            return false;
        if (goldenPrepared)
        {
            if (golden.Resolution.Outcome == ExtremeRiskOutcome.Jackpot)
            {
                yieldConditions = new[] { "state:golden-harvest-jackpot" };
                extremeSeedMultiplier = worker.GetDetailedStatMultiplier(
                    CropHarvestOutputRules.SeedYieldEffectTargetId,
                    yieldConditions);
            }
            else
            {
                extremeYieldMultiplier = golden.Resolution.PrimaryMultiplier;
                extremeSeedMultiplier = golden.Resolution.SecondaryMultiplier;
            }
        }

        bool ecologyPreparedOwned = false;
        try
        {
        CropEcologyPreparedHarvestSnapshot ecologyPrepared = ecologyHarvests
            .PrepareHarvest(operationId, state.PlotId.Value);
        ecologyPreparedOwned = true;
        float workerYieldMultiplier = 1f;
        if (worker != null)
        {
            CharacterPerformanceSnapshot yield = performance.Evaluate(
                worker,
                CropHarvestOutputRules.PerformanceFormulaId,
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
        bool indoor = state.Ability != null && state.Ability.Indoor;
        float outputMultiplier = indoor
            ? grandProjectBenefits.GetProductionOutputMultiplier("crop-indoor")
            : 1f;
        int primaryQuantityBeforeSeasonalLoss =
            CropHarvestOutputRules.ResolveHarvestQuantity(
            crop.Yield,
            outputMultiplier,
            workerYieldMultiplier,
            extremeYieldMultiplier,
            ecologyPrepared.Result.YieldMultiplier,
            IsOperational(ResearchFacilityCommandKind.SoilDiagnostics));
        CropSeasonalYieldDamageSaveData seasonalDamage =
            state.SeasonalYieldDamage
            ?? throw new InvalidOperationException(
                "Crop harvest has no seasonal yield-damage owner.");
        if (!seasonalDamage.IsEmpty)
            ValidateSeasonalYieldDamage(
                seasonalDamage,
                state.CropId,
                state.CycleExecutionReceipt,
                aggregateState.HandledSeasonalStartInstanceIds);
        int seasonalLossPercent = seasonalDamage.IsEmpty
            ? 0
            : seasonalDamage.primaryBatchLossPercent;
        int seasonalLossQuantity =
            CropHarvestOutputRules.ResolvePrimaryBatchLoss(
                primaryQuantityBeforeSeasonalLoss,
                seasonalLossPercent);
        int harvestQuantity = CropHarvestOutputRules.ApplyPrimaryBatchLoss(
            primaryQuantityBeforeSeasonalLoss,
            seasonalLossPercent);
        int seedQuantity = CropHarvestOutputRules.ResolveReturnedSeedQuantity(
            ecologyPrepared.Result.ReturnedSeedCount,
            extremeSeedMultiplier,
            IsOperational(ResearchFacilityCommandKind.SeedSelection));
        string harvesterId = worker?.Identity?.PersistentId ?? string.Empty;
        int completionAbsoluteDay = calendar.Day;
        string harvestLineId = CropHarvestOutputMaximumAuthority
            .HarvestOutputLineId(crop.CropId);
        string seedLineId = CropHarvestOutputMaximumAuthority
            .SeedOutputLineId(crop.CropId);
        ProductionOutputCapabilityDescriptor harvestCapability =
            outputCapabilities.CaptureDeclaredDescriptor(
                harvestLineId,
                crop.HarvestItemId,
                ProductionOutputCapabilityIds.StandardDefinition);
        ProductionOutputCapabilityDescriptor seedCapability =
            outputCapabilities.CaptureDeclaredDescriptor(
                seedLineId,
                crop.SeedItemId,
                ProductionOutputCapabilityIds.CropHarvestSeedLot);
        CropHarvestOutputSaveData pending = new()
        {
            phase = CropHarvestOutputPhase.Frozen,
            operationSequence = state.NextHarvestOperationSequence,
            operationId = operationId,
            cropId = crop.CropId,
            indoor = indoor,
            harvesterId = harvesterId,
            outcomeId = goldenPrepared
                ? golden.Resolution.Outcome.ToString().ToLowerInvariant()
                : "normal",
            ecologyOutcomeFingerprint = ecologyPrepared.OutcomeFingerprint,
            ecologyBeforeFingerprint = ecologyPrepared.PlotBeforeFingerprint,
            ecologyAfterFingerprint = ecologyPrepared.PlotAfterFingerprint,
            goldenPrepared = goldenPrepared,
            goldenTraitDefinitionId = goldenPrepared
                ? golden.TraitDefinitionId
                : string.Empty,
            goldenOutcomeFingerprint = goldenPrepared
                ? golden.Fingerprint
                : string.Empty,
            goldenOutcome = goldenPrepared
                ? golden.Resolution.Outcome
                : ExtremeRiskOutcome.Normal,
            goldenPrimaryMultiplier = goldenPrepared
                ? golden.Resolution.PrimaryMultiplier
                : 1f,
            goldenSecondaryMultiplier = goldenPrepared
                ? golden.Resolution.SecondaryMultiplier
                : 1f,
            goldenRollHash = goldenPrepared
                ? golden.Resolution.FixedRollHash
                : 0UL,
            completionAbsoluteDay = completionAbsoluteDay,
            harvestItemId = crop.HarvestItemId,
            harvestQuantity = harvestQuantity,
            seedItemId = crop.SeedItemId,
            seedQuantity = seedQuantity,
            returnedSeedLot = ecologyPrepared.Result.ReturnedSeedLot.Clone(),
            maximumHarvestQuantity = CropHarvestOutputMaximumAuthority
                .ResolveMaximumHarvestQuantity(
                    crop,
                    indoor,
                    performanceMaximum),
            maximumSeedQuantity = CropHarvestOutputMaximumAuthority
                .ResolveMaximumReturnedSeedQuantity(effectBounds),
            seasonalDamageSourceEventInstanceId = seasonalDamage.IsEmpty
                ? string.Empty
                : seasonalDamage.sourceEventInstanceId,
            seasonalDamageSourceDefinitionId = seasonalDamage.IsEmpty
                ? string.Empty
                : seasonalDamage.sourceDefinitionId,
            seasonalPrimaryBatchLossPercent = seasonalLossPercent,
            primaryQuantityBeforeSeasonalLoss = seasonalDamage.IsEmpty
                ? 0
                : primaryQuantityBeforeSeasonalLoss,
            seasonalPrimaryLossQuantity = seasonalDamage.IsEmpty
                ? 0
                : seasonalLossQuantity,
            harvestCapability = ProductionOutputCapabilitySaveData.Freeze(
                harvestCapability),
            seedCapability = ProductionOutputCapabilitySaveData.Freeze(
                seedCapability),
            outputPublication = new ProductionDomainOutputPublicationSaveData()
        };
        if (pending.harvestQuantity > pending.maximumHarvestQuantity
            || pending.seedQuantity > pending.maximumSeedQuantity)
            throw new InvalidOperationException(
                "Crop harvest actual output exceeds its authored maximum proof.");
        state.PendingHarvest = pending;
        return true;
        }
        catch (Exception error)
        {
            List<Exception> compensationFailures = new();
            if (ecologyPreparedOwned
                && !ecologyHarvests.AbortPreparedHarvest(operationId))
                compensationFailures.Add(new InvalidOperationException(
                    "Crop ecology prepared harvest compensation failed."));
            if (goldenPrepared
                && (extremeTraits == null
                    || !extremeTraits.TryAbortPreparedGoldenHarvest(
                        golden.CharacterId,
                        golden.TraitDefinitionId,
                        operationId)))
                compensationFailures.Add(new InvalidOperationException(
                    "Golden Harvest prepared resolution compensation failed."));
            if (compensationFailures.Count == 0)
                throw;
            compensationFailures.Insert(0, error);
            throw new AggregateException(
                "Crop harvest freeze failed and compensation was incomplete.",
                compensationFailures);
        }
    }

    private void TryAdvancePendingHarvest(
        CropPlotState state,
        CropDefinitionSO crop,
        out bool cycleCompleted)
    {
        cycleCompleted = false;
        CropHarvestOutputSaveData pending = state.PendingHarvest;
        if (pending == null || pending.phase == CropHarvestOutputPhase.None)
            return;
        if (!ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                pending.outputPublication,
                out _))
        {
            ProductionDomainOutputPublicationResult publicationResult =
                outputPublication.EnsureCommitted(
                    pending.outputPublication,
                    CreateHarvestOutputPlan(state, pending));
            if (publicationResult.Status ==
                ProductionDomainOutputPublicationStatus.Conflict)
                throw new InvalidOperationException(
                    "Crop harvest output publication conflicted: "
                    + publicationResult.FailureReason);
            if (!publicationResult.IsCommitted)
            {
                state.BlockedReason = publicationResult.FailureReason;
                return;
            }
            pending.phase = CropHarvestOutputPhase.OutputCommitted;
        }

        if (!pending.ecologyCommitted)
        {
            CropEcologyPreparedHarvestSnapshot committed = ecologyHarvests
                .CommitPreparedHarvest(pending.operationId);
            if (!string.Equals(
                    committed.OutcomeFingerprint,
                    pending.ecologyOutcomeFingerprint,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop harvest ecology receipt drifted before commit.");
            pending.ecologyCommitted = true;
        }
        if (pending.goldenPrepared && !pending.goldenCommitted)
        {
            if (extremeTraits == null
                || !extremeTraits.TryCommitPreparedGoldenHarvest(
                    pending.harvesterId,
                    pending.goldenTraitDefinitionId,
                    pending.operationId,
                    out GoldenHarvestPreparedResolution committed)
                || !string.Equals(
                    committed.Fingerprint,
                    pending.goldenOutcomeFingerprint,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop harvest Golden Harvest receipt drifted before commit.");
            pending.goldenCommitted = true;
        }
        if (!pending.outputPublication.outputAcknowledged
            && !outputPublication.TryAcknowledge(
                pending.outputPublication,
                out string acknowledgeFailure))
        {
            state.BlockedReason = acknowledgeFailure;
            return;
        }
        pending.phase = CropHarvestOutputPhase
            .OutputRestoredAwaitingFinalization;
        // The crop terminal is the authority for the completion reaction. Keep
        // the pending owner until both child and identity are durably applied;
        // a deferred identity retries this same child without publishing output.
        if (pending.terminalReceipt == null)
            throw new InvalidOperationException("Crop terminal snapshot is missing.");
        if (pending.terminalReceipt.IsEmpty)
        {
            pending.terminalReceipt = CropPlanExecutionReceiptAuthority.Complete(
                state.CycleExecutionReceipt, pending, state.CurrentWater);
            pending.terminalPlotDisplayName = RequirePlotDisplayName(state.Building);
            pending.terminalCropDisplayName = crop.DisplayName?.Trim() ?? string.Empty;
        }
        CropCycleExecutionReceiptSaveData terminalReceipt = pending.terminalReceipt;
        CropCycleTerminalFact terminalFact = new(state.PlotId, terminalReceipt,
            pending.terminalPlotDisplayName, pending.terminalCropDisplayName,
            pending.completionAbsoluteDay, checked((long)pending.operationSequence + 1L));
        if (!TryDeliverCropFact(terminalFact,
                ref pending.terminalPublicationKey, ref pending.terminalPublicationId,
                ref pending.terminalPublicationFingerprint, out string outcomeFailure))
        {
            state.BlockedReason = outcomeFailure;
            return;
        }
        pending.cropOutcomeAcknowledged = true;
        if (!pending.completionEventPublished)
        {
            if (!string.IsNullOrEmpty(pending.harvesterId))
            {
                var completionFact = new CropHarvestCompletedFact(state.PlotId, pending, state.CycleExecutionReceipt);
                if (!TryDeliverCropFact(completionFact, ref pending.completionPublicationKey,
                    ref pending.completionDeliveryId, ref pending.completionDeliveryFingerprint, out string failure))
                {
                    state.BlockedReason = failure;
                    return;
                }
            }
            pending.completionEventPublished = true;
        }
        if (!pending.ecologyAcknowledged)
        {
            if (!ecologyHarvests.AcknowledgePreparedHarvest(pending.operationId))
                throw new InvalidOperationException(
                    "Crop harvest ecology receipt acknowledgement failed.");
            pending.ecologyAcknowledged = true;
        }
        if (pending.goldenPrepared && !pending.goldenAcknowledged)
        {
            if (extremeTraits == null
                || !extremeTraits.TryAcknowledgePreparedGoldenHarvest(
                    pending.harvesterId,
                    pending.goldenTraitDefinitionId,
                    pending.operationId))
                throw new InvalidOperationException(
                    "Crop harvest Golden Harvest receipt acknowledgement failed.");
            pending.goldenAcknowledged = true;
        }
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

    private bool TryDeliverCropFact<TFact>(TFact fact,
        ref string publicationKey, ref string publicationId, ref string fingerprint,
        out string failureReason)
    {
        DomainFactLease reservation = default;
        try
        {
            if (string.IsNullOrEmpty(publicationKey))
            {
                if (!RequireDomainFacts().TryReserve(fact, out reservation, out failureReason))
                    return false;
                publicationKey = reservation.PublicationKey;
            }
            DomainFactDelivery delivery = DeliverCropCommandFact(reservation, publicationKey, fact);
            reservation = default;
            if (!ConfirmCropCommandPublication(delivery, publicationKey, publicationId, fingerprint, out failureReason))
                return false;
            publicationId = delivery.PublicationId;
            fingerprint = delivery.Fingerprint;
            if (delivery.Phase == DomainFactDeliveryPhase.Delivered)
                return true;
            failureReason = delivery.Detail;
            return false;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            if (reservation.IsValid) RequireDomainFacts().Cancel(reservation);
            failureReason = "crop-fact-publication-unconfirmed:" + exception.Message;
            return false;
        }
        finally { snapshotsDirty = true; }
    }

    private static void RecordCycleContribution(
        CropPlotState state,
        CharacterActor worker,
        float acceptedWork,
        bool harvest)
    {
        if (acceptedWork <= 0f || state?.CycleExecutionReceipt == null)
            return;
        string characterId = worker?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        if (characterId.Length == 0)
            return;
        if (state.CycleExecutionReceipt.IsEmpty
            || state.CycleExecutionReceipt.status
                != CropCycleExecutionReceiptStatus.Active)
        {
            throw new InvalidOperationException(
                "Crop work contribution has no active cycle receipt.");
        }
        state.CycleExecutionReceipt = CropPlanExecutionReceiptAuthority
            .RecordContribution(
                state.CycleExecutionReceipt,
                characterId,
                acceptedWork,
                harvest);
    }

    private static void RecordTreatmentContribution(
        CropTreatmentOrderSaveData treatment,
        CharacterActor worker,
        float acceptedWork)
    {
        if (acceptedWork <= 0f || treatment == null)
            return;
        string characterId = worker?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        string displayName = worker?.Identity?.DisplayName?.Trim()
            ?? string.Empty;
        if (characterId.Length == 0 || displayName.Length == 0)
            return;
        treatment.workContributions ??= new List<CraftContributionSaveData>();
        CraftContributionSaveData contribution =
            treatment.workContributions.FirstOrDefault(value => value != null
                && string.Equals(
                    value.characterId,
                    characterId,
                    StringComparison.Ordinal));
        if (contribution == null)
        {
            contribution = new CraftContributionSaveData
            {
                characterId = characterId
            };
            treatment.workContributions.Add(contribution);
        }
        contribution.contributedWork += acceptedWork;
        treatment.workContributions.Sort((left, right) =>
            string.CompareOrdinal(left?.characterId, right?.characterId));
        treatment.responsibleWorkerId = characterId;
        treatment.responsibleWorkerDisplayName = displayName;
    }

    private static void RecordWaterRefillContribution(
        CropWaterRefillSaveData refill,
        CharacterActor worker,
        float acceptedWork)
    {
        if (acceptedWork <= 0f || refill == null)
            return;
        string characterId = worker?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        string displayName = worker?.Identity?.DisplayName?.Trim()
            ?? string.Empty;
        if (characterId.Length == 0 || displayName.Length == 0)
            return;
        refill.workContributions ??= new List<CraftContributionSaveData>();
        CraftContributionSaveData contribution = refill.workContributions
            .FirstOrDefault(value => value != null
                && string.Equals(
                    value.characterId,
                    characterId,
                    StringComparison.Ordinal));
        if (contribution == null)
        {
            contribution = new CraftContributionSaveData
            {
                characterId = characterId
            };
            refill.workContributions.Add(contribution);
        }
        contribution.contributedWork += acceptedWork;
        refill.workContributions.Sort((left, right) =>
            string.CompareOrdinal(left?.characterId, right?.characterId));
        refill.responsibleWorkerId = characterId;
        refill.responsibleWorkerDisplayName = displayName;
    }

    private static bool IsFailedCycleReceipt(
        CropCycleExecutionReceiptSaveData receipt) => receipt != null
        && receipt.status is CropCycleExecutionReceiptStatus.FailedCropDeath
            or CropCycleExecutionReceiptStatus.FailedPlotDestroyed;

    private static string RequirePlotDisplayName(BuildableObject building)
    {
        string name = building?.BuildingData?.objectName?.Trim()
            ?? string.Empty;
        if (name.Length == 0)
            throw new InvalidOperationException(
                "Crop terminal outcome requires an immutable plot display snapshot.");
        return name;
    }

    private void FreezeFailedOutcome(CropPlotState state, int absoluteDay)
    {
        CropCycleExecutionReceiptSaveData terminal =
            state?.CycleExecutionReceipt;
        if (!IsFailedCycleReceipt(terminal))
            throw new InvalidOperationException(
                "A crop failure outcome requires a failed execution receipt.");
        if (state.FailedOutcome != null && !state.FailedOutcome.IsEmpty)
        {
            if (!string.Equals(
                    state.FailedOutcome.receiptDigest,
                    terminal.sourceDigest,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop failure outcome snapshot drifted from its receipt.");
            return;
        }
        if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
            throw new InvalidOperationException(
                "Crop failure outcome references unknown crop content.");
        string plotName = state.PlotDisplayName?.Trim() ?? string.Empty;
        string cropName = crop.DisplayName?.Trim() ?? string.Empty;
        if (plotName.Length == 0 || cropName.Length == 0
            || string.Equals(cropName, crop.CropId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Crop failure outcome lacks an authored display snapshot.");
        state.FailedOutcome = new CropFailedOutcomeSaveData
        {
            receiptDigest = terminal.sourceDigest,
            plotDisplayName = plotName,
            cropDisplayName = cropName,
            absoluteDay = Math.Max(0, absoluteDay),
            ownerRevision = checked((long)state.NextHarvestOperationSequence + 1L)
        };
    }

    private bool TryEnsureFailedOutcomeAcknowledged(
        CropPlotState state,
        out string failureReason)
    {
        if (state?.IrrigationPublication.pending == true)
        {
            failureReason = "crop-failed-cycle-irrigation-publication-pending";
            return false;
        }
        CropCycleExecutionReceiptSaveData terminal =
            state?.CycleExecutionReceipt;
        CropFailedOutcomeSaveData frozen = state?.FailedOutcome;
        if (!IsFailedCycleReceipt(terminal)
            || frozen == null
            || frozen.IsEmpty
            || !string.Equals(
                frozen.receiptDigest,
                terminal.sourceDigest,
                StringComparison.Ordinal))
        {
            failureReason = "crop-failed-outcome-snapshot-invalid";
            return false;
        }
        CropCycleTerminalFact fact = new(state.PlotId, terminal,
            frozen.plotDisplayName, frozen.cropDisplayName, frozen.absoluteDay, frozen.ownerRevision);
        return TryDeliverCropFact(fact, ref frozen.publicationKey,
            ref frozen.publicationId, ref frozen.publicationFingerprint, out failureReason);
    }

    private bool TryCloseFailedCyclePhysicalOwners(
        CropPlotState state,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (state.IrrigationPublication.pending)
        {
            failureReason = "crop-failed-cycle-irrigation-publication-pending";
            return false;
        }
        if (state.PendingHarvest.phase != CropHarvestOutputPhase.None
            || state.PendingSow.phase != CropPhysicalCommitPhase.None)
        {
            failureReason = "crop-failed-cycle-physical-child-pending";
            return false;
        }
        if (!state.MaterialsConsumed
            && state.CurrentWater == 0f
            && state.Phase == CropPlotPhase.Blocked
            && state.WaterRefill.phase == CropWaterRefillPhase.None
            && state.Treatment.phase == CropTreatmentOrderPhase.None
            && state.FrozenSowInputOperationSequence < 0)
            return true;
        if (!TryRetireInactiveWaterRefill(state))
        {
            failureReason = "crop-failed-cycle-water-refill-pending";
            return false;
        }
        CropTreatmentOrderSaveData treatment = state.Treatment;
        if (treatment.phase is CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted)
            TryFinalizeTreatment(state);
        else if (treatment.phase is CropTreatmentOrderPhase.WaitingForDelivery
                or CropTreatmentOrderPhase.ReadyForWork
                or CropTreatmentOrderPhase.Working)
        {
            if (!TryRetireDestination(
                    state,
                    treatment.destinationId,
                    CropPlotInputOwnerAuthority.TreatmentCancelledReleaseReasonCode,
                    out failureReason))
                return false;
            CropTreatmentPhysicalOutbox.Clear(treatment);
        }
        if (treatment.phase != CropTreatmentOrderPhase.None)
        {
            failureReason = "crop-failed-cycle-treatment-child-pending";
            return false;
        }
        string releaseReason = state.Building == null
            || state.Building.isDestroy
            ? CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode
            : CropPlotInputOwnerAuthority.SowCompletedReleaseReasonCode;
        if (!TryRetireDestination(
                state,
                state.MaterialDestinationId,
                releaseReason,
                out failureReason))
            return false;
        state.MaterialsConsumed = false;
        state.CurrentWater = 0f;
        state.Phase = CropPlotPhase.Blocked;
        state.SeasonalYieldDamage = new CropSeasonalYieldDamageSaveData();
        ClearFrozenSowInputs(state);
        MarkChanged(replan: false);
        return true;
    }

    private bool TryAdvanceFailedCycle(CropPlotState state)
    {
        if (!TryEnsureFailedOutcomeAcknowledged(
                state, out string outcomeFailure))
        {
            state.BlockedReason = outcomeFailure;
            snapshotsDirty = true;
            return false;
        }
        if (!TryCloseFailedCyclePhysicalOwners(
                state, out string cleanupFailure))
        {
            state.BlockedReason = cleanupFailure;
            snapshotsDirty = true;
            return false;
        }
        if (state.CycleExecutionReceipt.explicitCorrelation)
        {
            state.BlockedReason =
                "crop-cycle-execution-receipt-awaiting-acknowledgement";
            snapshotsDirty = true;
            return false;
        }
        state.CycleExecutionReceipt = new CropCycleExecutionReceiptSaveData();
        state.FailedOutcome = new CropFailedOutcomeSaveData();
        MarkChanged(replan: false);
        return true;
    }

    private static ProductionDomainOutputPublicationPlan CreateHarvestOutputPlan(
        CropPlotState state,
        CropHarvestOutputSaveData pending)
    {
        List<ProductionDomainOutputLine> lines = new()
        {
            new ProductionDomainOutputLine(
                pending.harvestCapability.outputLineId,
                pending.harvestItemId,
                pending.harvestQuantity,
                string.Empty,
                Array.Empty<ItemInstanceComponentSaveData>(),
                pending.harvestCapability.ToDescriptor())
        };
        if (pending.seedQuantity > 0)
        {
            lines.Add(new ProductionDomainOutputLine(
                pending.seedCapability.outputLineId,
                pending.seedItemId,
                pending.seedQuantity,
                string.Empty,
                new[]
                {
                    SeedLotItemStateCodec.Encode(pending.returnedSeedLot)
                },
                pending.seedCapability.ToDescriptor()));
        }
        return new ProductionDomainOutputPublicationPlan(
            HarvestOutputPublicationOperationPrefix,
            pending.operationId,
            HarvestOutputBatchCommitPrefix + pending.operationId,
            CaptureHarvestOutcomeFingerprint(state.PlotId, pending),
            state.Building,
            lines,
            FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned,
            ProductionDomainOutputAcknowledgementDisposition
                .ReleaseLooseOrDestination,
            new[]
            {
                new ProductionDomainOutputMaximumMassClaim(
                    pending.harvestCapability.ToDescriptor(),
                    pending.maximumHarvestQuantity),
                new ProductionDomainOutputMaximumMassClaim(
                    pending.seedCapability.ToDescriptor(),
                    pending.maximumSeedQuantity)
            });
    }

    private static string CaptureHarvestOutcomeFingerprint(
        BuildingInstanceId plotId,
        CropHarvestOutputSaveData pending)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-harvest-frozen-output@3");
        digest.Append(pending.operationId);
        digest.Append(plotId.Value);
        digest.Append(pending.cropId);
        digest.Append(pending.indoor);
        digest.Append(pending.harvesterId);
        digest.Append(pending.outcomeId);
        digest.Append(pending.ecologyOutcomeFingerprint);
        digest.Append(pending.ecologyBeforeFingerprint);
        digest.Append(pending.ecologyAfterFingerprint);
        digest.Append(pending.goldenPrepared);
        digest.Append(pending.goldenTraitDefinitionId);
        digest.Append(pending.goldenOutcomeFingerprint);
        digest.Append((int)pending.goldenOutcome);
        digest.AppendFloat(pending.goldenPrimaryMultiplier);
        digest.AppendFloat(pending.goldenSecondaryMultiplier);
        digest.Append(pending.goldenRollHash.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        digest.Append(pending.harvestItemId);
        digest.Append(pending.harvestQuantity);
        digest.Append(pending.seasonalDamageSourceEventInstanceId);
        digest.Append(pending.seasonalDamageSourceDefinitionId);
        digest.Append(pending.seasonalPrimaryBatchLossPercent);
        digest.Append(pending.primaryQuantityBeforeSeasonalLoss);
        digest.Append(pending.seasonalPrimaryLossQuantity);
        digest.Append(pending.seedItemId);
        digest.Append(pending.seedQuantity);
        digest.Append(SeedLotItemStateCodec.Encode(pending.returnedSeedLot)
            .ToCanonicalString());
        digest.Append(pending.maximumHarvestQuantity);
        digest.Append(pending.maximumSeedQuantity);
        digest.Append(pending.harvestCapability.fingerprint);
        digest.Append(pending.seedCapability.fingerprint);
        return digest.ComputeSha256();
    }

    private static IReadOnlyList<ProductionDomainOutputMaximumMassClaim>
        CaptureHarvestMaximumMassClaims(CropHarvestOutputSaveData pending) =>
        new[]
        {
            new ProductionDomainOutputMaximumMassClaim(
                pending.harvestCapability.ToDescriptor(),
                pending.maximumHarvestQuantity),
            new ProductionDomainOutputMaximumMassClaim(
                pending.seedCapability.ToDescriptor(),
                pending.maximumSeedQuantity)
        };

    public static string FormatHarvestOperationId(
        BuildingInstanceId plotId,
        int sequence)
    {
        if (!plotId.IsValid || sequence < 0)
            throw new ArgumentException(
                "A valid crop plot and non-negative harvest sequence are required.");
        return "crop-harvest:"
            + plotId.Value + ":"
            + sequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string CaptureHarvestLifecycleFingerprint(
        CropPlotState state)
    {
        CropHarvestOutputSaveData pending = state.PendingHarvest;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-harvest-facility-lifecycle@1");
        digest.Append(state.PlotId.Value);
        digest.Append((int)pending.phase);
        digest.Append(CaptureHarvestOutcomeFingerprint(state.PlotId, pending));
        digest.Append(pending.outputPublication?.batchCommitId ?? string.Empty);
        digest.Append(pending.outputPublication?.plannedOutputFingerprint
            ?? string.Empty);
        return digest.ComputeSha256();
    }

    private static void ValidateActiveHarvestOwner(CropPlotState state)
    {
        CropHarvestOutputSaveData pending = state?.PendingHarvest;
        if (pending == null || pending.phase == CropHarvestOutputPhase.None)
            throw new InvalidOperationException(
                "Crop harvest lifecycle owner is empty.");
        if (pending.phase == CropHarvestOutputPhase.Frozen)
        {
            if (pending.outputPublication == null
                || !pending.outputPublication.IsEmpty)
                throw new InvalidOperationException(
                    "Frozen crop harvest already contains publication provenance.");
            return;
        }
        if (!ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                pending.outputPublication,
                out string failureReason)
            || pending.outputPublication.acknowledgementDisposition !=
                ProductionDomainOutputAcknowledgementDisposition
                    .ReleaseLooseOrDestination
            || pending.phase == CropHarvestOutputPhase.OutputCommitted
                && pending.outputPublication.outputAcknowledged
            || pending.phase == CropHarvestOutputPhase
                    .OutputRestoredAwaitingFinalization
                && !pending.outputPublication.outputAcknowledged)
        {
            throw new InvalidOperationException(
                "Crop harvest active output owner is invalid: "
                + (pending.operationId ?? string.Empty) + ":" + failureReason);
        }
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
        if (!TryRequireMutable(state.PlotId, out failureReason))
            return false;
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
        int scheduleSequence = state.GoldenHarvestAttemptSequence;
        string operationId = GoldenHarvestScheduleOperationPrefix
            + state.PlotId.Value + ":"
            + scheduleSequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
        if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
        {
            failureReason = "golden-harvest-crop-definition-missing";
            return false;
        }
        var pendingSchedule = new CropGoldenHarvestScheduleSaveData
        {
            phase = CropCommandOutcomePhase.MutationApplied,
            operationSequence = scheduleSequence, operationId = operationId,
            cropId = state.CropId, harvesterId = harvesterId,
            harvesterDisplayName = harvester.Identity.DisplayName,
            harvesterBefore = string.Empty, harvesterAfter = harvesterId,
            attemptBefore = scheduleSequence, attemptAfter = checked(scheduleSequence + 1),
            completedAbsoluteDay = CurrentAbsoluteDay,
            plotDisplayName = state.PlotDisplayName, cropDisplayName = crop.DisplayName,
            locationX = state.LastKnownPosition.x, locationY = state.LastKnownPosition.y
        };
        IDomainFactPublisher publisher = RequireDomainFacts();
        if (!publisher.TryReserve(new GoldenHarvestScheduledFact(state.PlotId, pendingSchedule),
                out DomainFactLease reserved, out string reserveFailure))
        {
            failureReason = "황금 수확 예약 결과를 예약할 수 없습니다: " + reserveFailure;
            return false;
        }
        pendingSchedule.publicationKey = reserved.PublicationKey;
        if (extremeTraits == null
            || !extremeTraits.TryScheduleGoldenHarvest(
                harvester,
                state.PlotId.Value,
                state.GoldenHarvestAttemptSequence,
                gameClock.Time))
        {
            publisher.Cancel(reserved);
            failureReason = "황금 수확을 예약할 수 없습니다.";
            return false;
        }
        state.GoldenHarvestHarvesterId = harvesterId;
        state.GoldenHarvestAttemptSequence = checked(
            state.GoldenHarvestAttemptSequence + 1);
        state.GoldenHarvestScheduleOutcome = pendingSchedule;
        MarkChanged();
        bool recorded = TryAdvanceGoldenHarvestScheduleOutcome(state, reserved);
        failureReason = recorded
            ? $"{harvester.Identity.DisplayName}에게 황금 수확을 예약했습니다."
            : "황금 수확 예약은 적용됐지만 결과 원장 확정이 대기 중입니다: "
                + state.GoldenHarvestScheduleOutcome.failureReason;
        return recorded;
    }

    private bool TryAdvanceGoldenHarvestScheduleOutcome(
        CropPlotState state, DomainFactLease reserved = default)
    {
        CropGoldenHarvestScheduleSaveData owner = state?.GoldenHarvestScheduleOutcome;
        if (owner == null || owner.IsEmpty) return true;
        DomainFactDelivery delivery = DeliverCropCommandFact(reserved, owner.publicationKey,
            new GoldenHarvestScheduledFact(state.PlotId, owner));
        if (!ConfirmCropCommandPublication(delivery, owner.publicationKey,
                owner.gameplayOutcomeId, owner.gameplayOutcomeDigest, out string failure))
        {
            owner.failureReason = failure;
            snapshotsDirty = true;
            return false;
        }
        owner.phase = CropCommandOutcomePhase.LedgerCommitted;
        owner.gameplayOutcomeId = delivery.PublicationId;
        owner.gameplayOutcomeDigest = delivery.Fingerprint;
        if (delivery.Phase != DomainFactDeliveryPhase.Delivered)
        {
            owner.failureReason = delivery.Detail;
            snapshotsDirty = true;
            return false;
        }
        state.GoldenHarvestScheduleOutcome = new CropGoldenHarvestScheduleSaveData();
        MarkChanged(replan: false);
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


    public DungeonCropPlotSaveData Capture()
    {
        if (!inputOwners.TryReconcileLive(
                BuildLiveInputOwnerDescriptors(),
                out string ownerFailure))
            throw new InvalidOperationException(
                "Crop-plot input ownership is not capture-safe: "
                + ownerFailure);
        DungeonCropPlotSaveData data = new DungeonCropPlotSaveData
        {
            handledSeasonalStartInstanceIds = aggregateState
                .HandledSeasonalStartInstanceIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList()
        };
        foreach (CropPlotState state in states.Values
                     .OrderBy(entry => entry.PlotId.Value, StringComparer.Ordinal))
        {
            data.plots.Add(new CropPlotSaveData
            {
                buildingInstanceId = state.PlotId.Value,
                plotDisplayName = state.PlotDisplayName,
                lastKnownGridX = state.LastKnownPosition.x,
                lastKnownGridY = state.LastKnownPosition.y,
                cropId = state.CropId,
                retirementPublicationKey = state.RetirementPublicationKey,
                nextCropSelectionOperationSequence =
                    state.NextCropSelectionOperationSequence,
                cropSelectionOutcome = state.CropSelectionOutcome.DeepClone(),
                phase = state.Phase,
                sowWork = state.SowWork,
                growthHours = state.GrowthHours,
                harvestWork = state.HarvestWork,
                materialsConsumed = state.MaterialsConsumed,
                frozenSowInputOperationSequence =
                    state.FrozenSowInputOperationSequence,
                frozenSowInputSourceDigest = state.FrozenSowInputSourceDigest,
                frozenSowInputVectorDigest = state.FrozenSowInputVectorDigest,
                frozenSowInputWeather = state.FrozenSowInputWeather,
                frozenSowInputConsumptionMultiplier =
                    state.FrozenSowInputConsumptionMultiplier,
                frozenSowInputSelectedFuelItemId =
                    state.FrozenSowInputSelectedFuelItemId,
                frozenSowInputs = state.FrozenSowInputs
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => new CropCycleInputRequirementSaveData
                    {
                        itemId = value.Key,
                        quantity = value.Value
                    })
                    .ToList(),
                goldenHarvestHarvesterId = state.GoldenHarvestHarvesterId,
                goldenHarvestAttemptSequence = state.GoldenHarvestAttemptSequence,
                goldenHarvestScheduleOutcome =
                    state.GoldenHarvestScheduleOutcome.DeepClone(),
                nextSowOperationSequence = state.NextSowOperationSequence,
                pendingSow = state.PendingSow.DeepClone(),
                pendingCycleCorrelationId =
                    state.PendingCycleCorrelationId,
                cycleExecutionReceipt = state.CycleExecutionReceipt.DeepClone(),
                failedOutcome = state.FailedOutcome.DeepClone(),
                currentWater = state.CurrentWater,
                waterCapacity = state.WaterCapacity,
                nextWaterRefillOperationSequence =
                    state.NextWaterRefillOperationSequence,
                irrigationPublication = state.IrrigationPublication.DeepClone(),
                waterRefill = state.WaterRefill.DeepClone(),
                nextTreatmentOperationSequence =
                    state.NextTreatmentOperationSequence,
                pestLureNextAllowedDay = state.PestLureNextAllowedDay,
                botanicalPesticideNextAllowedDay =
                    state.BotanicalPesticideNextAllowedDay,
                fungicideNextAllowedDay = state.FungicideNextAllowedDay,
                treatment = state.Treatment.DeepClone(),
                nextHarvestOperationSequence =
                    state.NextHarvestOperationSequence,
                pendingHarvest = state.PendingHarvest.DeepClone(),
                seasonalYieldDamage = state.SeasonalYieldDamage.DeepClone()
            });
        }

        return data;
    }

    public CropPlotRestoreCandidate BuildRestore(
        DungeonCropPlotSaveData snapshot)
    {
        HashSet<string> handledSeasonalStarts = RequireSaveRoot(snapshot);
        CropPlotAggregateState restored = new()
        {
            ObservedBuildingVersion = -1,
            NextMaterialRequestTime = gameClock.Time,
            SnapshotsDirty = true,
            Version = aggregateState.Version + 1
        };
        restored.HandledSeasonalStartInstanceIds.UnionWith(
            handledSeasonalStarts);
        HashSet<BuildingInstanceId> seen = new();
        HashSet<string> correlations = new(StringComparer.Ordinal);
        foreach (CropPlotSaveData saved in snapshot.plots)
        {
            BuildingInstanceId plotId = RequireRestorePlotId(saved, seen);
            CropDefinitionSO crop = RequireCrop(saved);
            ValidateRestoreProgress(
                saved,
                crop,
                plotId,
                handledSeasonalStarts);
            string correlationId = !string.IsNullOrEmpty(
                    saved.pendingCycleCorrelationId)
                ? saved.pendingCycleCorrelationId
                : saved.cycleExecutionReceipt.IsEmpty
                    ? string.Empty
                    : saved.cycleExecutionReceipt.correlationId;
            if (correlationId.Length > 0 && !correlations.Add(correlationId))
                throw new InvalidOperationException(
                    "Crop execution correlation is duplicated across plots: "
                    + correlationId);
            restored.States.Add(plotId, new CropPlotState
            {
                PlotId = plotId,
                PlotDisplayName = saved.plotDisplayName ?? string.Empty,
                RetirementPublicationKey = saved.retirementPublicationKey,
                LastKnownPosition = new Vector2Int(
                    saved.lastKnownGridX,
                    saved.lastKnownGridY),
                CropId = crop.CropId,
                NextCropSelectionOperationSequence = Math.Max(
                    0,
                    saved.nextCropSelectionOperationSequence),
                CropSelectionOutcome = saved.cropSelectionOutcome?.DeepClone()
                    ?? new CropSelectionCommandSaveData(),
                Phase = saved.phase,
                SowWork = saved.sowWork,
                GrowthHours = saved.growthHours,
                HarvestWork = saved.harvestWork,
                MaterialDestinationId = BuildSowDestinationId(
                    plotId,
                    saved.nextSowOperationSequence),
                MaterialsConsumed = saved.materialsConsumed,
                FrozenSowInputOperationSequence =
                    saved.frozenSowInputOperationSequence,
                FrozenSowInputSourceDigest =
                    saved.frozenSowInputSourceDigest,
                FrozenSowInputVectorDigest =
                    saved.frozenSowInputVectorDigest,
                FrozenSowInputWeather = saved.frozenSowInputWeather,
                FrozenSowInputConsumptionMultiplier =
                    saved.frozenSowInputConsumptionMultiplier,
                FrozenSowInputSelectedFuelItemId =
                    saved.frozenSowInputSelectedFuelItemId,
                FrozenSowInputs = saved.frozenSowInputs.ToDictionary(
                    value => value.itemId,
                    value => value.quantity,
                    StringComparer.Ordinal),
                GoldenHarvestHarvesterId = saved.goldenHarvestHarvesterId?.Trim()
                    ?? string.Empty,
                GoldenHarvestAttemptSequence = Math.Max(
                    0,
                    saved.goldenHarvestAttemptSequence),
                GoldenHarvestScheduleOutcome = saved
                    .goldenHarvestScheduleOutcome?.DeepClone()
                    ?? new CropGoldenHarvestScheduleSaveData(),
                NextSowOperationSequence = saved.nextSowOperationSequence,
                PendingSow = saved.pendingSow.DeepClone(),
                PendingCycleCorrelationId =
                    saved.pendingCycleCorrelationId ?? string.Empty,
                CycleExecutionReceipt =
                    saved.cycleExecutionReceipt.DeepClone(),
                FailedOutcome = saved.failedOutcome?.DeepClone()
                    ?? new CropFailedOutcomeSaveData(),
                CurrentWater = saved.currentWater,
                WaterCapacity = saved.waterCapacity,
                NextWaterRefillOperationSequence =
                    saved.nextWaterRefillOperationSequence,
                IrrigationPublication = saved.irrigationPublication.DeepClone(),
                WaterRefill = saved.waterRefill.DeepClone(),
                NextTreatmentOperationSequence =
                    saved.nextTreatmentOperationSequence,
                PestLureNextAllowedDay = saved.pestLureNextAllowedDay,
                BotanicalPesticideNextAllowedDay =
                    saved.botanicalPesticideNextAllowedDay,
                FungicideNextAllowedDay = saved.fungicideNextAllowedDay,
                Treatment = saved.treatment.DeepClone(),
                NextHarvestOperationSequence =
                    saved.nextHarvestOperationSequence,
                PendingHarvest = saved.pendingHarvest.phase ==
                        CropHarvestOutputPhase.None
                    ? new CropHarvestOutputSaveData()
                    : saved.pendingHarvest.DeepClone(),
                SeasonalYieldDamage = saved.seasonalYieldDamage.DeepClone(),
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

        if (!TryAdvanceIrrigationPublication(state)
            || !TryAdvanceCropSelectionOutcome(state)
            || !TryAdvanceGoldenHarvestScheduleOutcome(state))
            return;

        if (IsFailedCycleReceipt(state.CycleExecutionReceipt))
        {
            TryAdvanceFailedCycle(state);
            return;
        }

        if (state.PendingSow.phase == CropPhysicalCommitPhase.OutcomePublished)
        {
            FinalizePublishedSow(state);
            if (state.PendingSow.phase != CropPhysicalCommitPhase.None)
                return;
        }

        if (state.WaterRefill.phase is CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted)
        {
            TryFinalizeWaterRefill(state);
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
            TryFinalizeTreatment(state);
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

        if (!TryRequireMutable(state.PlotId, out string mutationReason))
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
                MarkChanged(replan: false);
            }
            EnsureSowingMaterials(state, crop, requestMaterials);
            return;
        }

        if (state.Phase != CropPlotPhase.Growing)
        {
            TryRetireInactiveWaterRefill(state);
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
                ? TryCommitAutomaticIrrigation(state, request)
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
            else if (ShouldBeginManualWaterRefill(
                         supply.Assessment.Status))
            {
                BeginWaterRefill(state);
            }
        }
        if (state.IrrigationPublication.pending)
            return;
        TickWaterRefillDelivery(state, requestMaterials);

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
                MarkChanged(replan: false);
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
            MarkChanged();
        }
    }

    private void EnsureSowingMaterials(
        CropPlotState state,
        CropDefinitionSO crop,
        bool requestMaterials)
    {
        if (!HasFrozenSowInputs(state))
            FreezeSowInputs(state, crop);
        Dictionary<string, int> requirements = BuildMaterialRequirements(state, crop);
        if (requirements.Count == 0)
        {
            state.MaterialsConsumed = true;
            state.Phase = CropPlotPhase.ReadyToSow;
            MarkChanged();
            return;
        }
        CropPlotInputOwnerDescriptor inputOwner =
            CreateSowInputOwnerDescriptor(state, requirements);
        if (!inputOwners.TryEnsure(inputOwner, out string ownerFailure))
        {
            SetBlocked(state, "crop-sow-input-owner-unavailable:" + ownerFailure);
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
        int requiredSeed = requirements.TryGetValue(
            crop.SeedItemId,
            out int authoredSeedRequirement)
                ? authoredSeedRequirement
                : 0;
        int deliveredSeed = requiredSeed > 0
            ? items.CountDelivered(crop.SeedItemId, state.MaterialDestinationId)
            : 0;
        int pendingSeed = requiredSeed > 0
            ? items.CountPending(crop.SeedItemId, state.MaterialDestinationId)
            : 0;
        if (requiredSeed > deliveredSeed && pendingSeed > deliveredSeed)
        {
            if (!seedLots.TryReleaseUnreachableSeedDelivery(
                    crop.SeedItemId,
                    crop.CropId,
                    state.Building.centerPos,
                    state.MaterialDestinationId,
                    out bool releasedUnreachableDelivery,
                    out DomainFailure recoveryFailure))
            {
                SetBlocked(
                    state,
                    "crop-seed-delivery-recovery-failed:"
                        + recoveryFailure.Code);
                return;
            }
            if (releasedUnreachableDelivery)
            {
                // Destination release is intentionally whole-owner atomic. Any
                // companion water/compost input is now counted again by the same
                // loop instead of being silently stranded under an old route.
                requestedAny = true;
            }
        }
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
        if (!TryRetireDestination(
                state,
                state.MaterialDestinationId,
                CropPlotInputOwnerAuthority.SowCompletedReleaseReasonCode,
                out string retireFailure))
        {
            state.BlockedReason = retireFailure;
            snapshotsDirty = true;
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
        if (state.Ability == null)
            throw new InvalidOperationException(
                "Crop sow execution receipt requires its authored plot ability.");
        bool explicitCorrelation = !string.IsNullOrEmpty(
            state.PendingCycleCorrelationId);
        string correlationId = !explicitCorrelation
            ? "crop-cycle:" + state.PendingSow.operationId
            : state.PendingCycleCorrelationId;
        if (state.CycleExecutionReceipt != null
            && !state.CycleExecutionReceipt.IsEmpty)
        {
            CropPlanExecutionReceiptAuthority.Validate(
                state.CycleExecutionReceipt,
                requireCompleted: false);
            if (state.CycleExecutionReceipt.status
                != CropCycleExecutionReceiptStatus.Active)
            {
                if (state.CycleExecutionReceipt.explicitCorrelation)
                    throw new InvalidOperationException(
                        "An unacknowledged explicit crop receipt blocks the next cycle.");
                state.CycleExecutionReceipt =
                    new CropCycleExecutionReceiptSaveData();
            }
        }
        if (state.CycleExecutionReceipt == null
            || state.CycleExecutionReceipt.IsEmpty)
        {
            int committedWater = (state.PendingSow.inputs
                    ?? new List<CropPhysicalInputSaveData>())
                .Where(input => input != null && string.Equals(
                    input.itemId,
                    CropCycleInputRequirementAuthority.CleanWaterItemId,
                    StringComparison.Ordinal))
                .Sum(input => input.quantity);
            if (committedWater is < 0 or > (int)CropWaterRules.InitialWaterQuantity)
                throw new InvalidOperationException(
                    "Crop sow committed an invalid initial clean-water quantity.");
            state.CycleExecutionReceipt =
                CropPlanExecutionReceiptAuthority.Begin(
                    correlationId,
                    explicitCorrelation,
                    state.PlotId.Value,
                    state.Ability.Indoor,
                    state.PendingSow,
                    committedWater);
            state.CurrentWater = committedWater;
            state.WaterCapacity = CropWaterRules.WaterCapacity;
        }
        else
        {
            CropPlanExecutionReceiptAuthority.Validate(
                state.CycleExecutionReceipt,
                requireCompleted: false);
            if (!string.Equals(
                    state.CycleExecutionReceipt.sowOperationId,
                    state.PendingSow.operationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    state.CycleExecutionReceipt.correlationId,
                    correlationId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Crop sow execution receipt conflicts with the active cycle.");
            }
        }
        state.PendingCycleCorrelationId = string.Empty;
        CropPhysicalTransactionOutbox.Clear(state.PendingSow);
        ClearFrozenSowInputs(state);
        state.NextSowOperationSequence = checked(
            state.NextSowOperationSequence + 1);
        state.MaterialDestinationId = BuildSowDestinationId(
            state.PlotId,
            state.NextSowOperationSequence);
        state.BlockedReason = string.Empty;
        MarkChanged();
    }

    private CropIrrigationSupplyResult TryCommitAutomaticIrrigation(
        CropPlotState state, CropIrrigationRequest request)
    {
        if (state.IrrigationPublication.pending)
            throw new InvalidOperationException("A previous irrigation fact is still pending.");
        CropIrrigationAssessment assessment = irrigation.Assess(request);
        if (!assessment.CanSupply)
            return new CropIrrigationSupplyResult(assessment, false, 0f, WorldWaterQuality.Clean);

        int sequence = state.NextWaterRefillOperationSequence;
        int nextSequence = checked(sequence + 1);
        float waterBefore = state.CurrentWater;
        CropCycleExecutionReceiptSaveData cycleBefore = state.CycleExecutionReceipt;
        if (cycleBefore == null || cycleBefore.IsEmpty)
            throw new InvalidOperationException("Crop irrigation requires its active physical cycle.");
        PreparedCropIrrigationSupply prepared = null;
        DomainFactLease lease = default;
        IDomainFactPublisher publisher = RequireDomainFacts();
        bool ownerChanged = false;
        bool publicationMayBeCommitted = false;
        try
        {
            // Fluid preparation is reversible. No observer failure may leave a
            // debit unless an authoritative post-supply fact could be committed.
            if (!irrigationTransactions.TryPrepareSupply(request, out prepared))
                return new CropIrrigationSupplyResult(irrigation.Assess(request), false, 0f, WorldWaterQuality.Clean);
            CropIrrigationSupplyResult supplied = prepared.Result;
            if (supplied.SuppliedWaterUnits != CropWaterRules.RefillQuantity
                || waterBefore + supplied.SuppliedWaterUnits > state.WaterCapacity)
                throw new InvalidOperationException("Prepared crop irrigation drifted from its preflight quantity.");
            var pending = new CropIrrigationPublicationSaveData
            {
                pending = true, operationSequence = sequence,
                operationId = IrrigationOperationPrefix + state.PlotId.Value + ":"
                    + sequence.ToString("D6", System.Globalization.CultureInfo.InvariantCulture),
                plotId = state.PlotId.Value, irrigatorId = supplied.Assessment.IrrigatorId.Value,
                plotDisplayName = prepared.PlotDisplayName, irrigatorDisplayName = prepared.IrrigatorDisplayName,
                absoluteDay = CurrentAbsoluteDay, locationX = state.LastKnownPosition.x, locationY = state.LastKnownPosition.y,
                quantity = supplied.SuppliedWaterUnits, consumedQuality = supplied.ConsumedQuality,
                waterBefore = waterBefore, waterAfter = waterBefore + supplied.SuppliedWaterUnits,
                networkId = prepared.NetworkId, networkWaterBefore = prepared.NetworkWaterBefore,
                networkWaterAfter = prepared.NetworkWaterAfter
            };
            CropIrrigationSuppliedFact fact = new(pending);
            if (!publisher.TryReserve(fact, out lease, out string failure))
                throw new InvalidOperationException("Crop irrigation observation reservation failed: " + failure);
            pending.publicationKey = lease.PublicationKey;
            CropCycleExecutionReceiptSaveData cycleAfter = CropPlanExecutionReceiptAuthority.RecordWaterSupply(
                cycleBefore, pending.waterBefore, pending.waterAfter, pending.publicationKey);

            // Persist the applied state, exact opaque identity and frozen fact
            // before dispatch. An unknown ACK is forward recovery, never a
            // second network debit or an inferred rollback of a committed fact.
            state.CurrentWater = pending.waterAfter;
            state.NextWaterRefillOperationSequence = nextSequence;
            state.CycleExecutionReceipt = cycleAfter;
            state.IrrigationPublication = pending;
            ownerChanged = true;
            DomainFactDelivery delivery = DeliverCropCommandFact(lease, pending.publicationKey, fact);
            publicationMayBeCommitted = delivery.MayBeCommitted;
            if (!publicationMayBeCommitted)
                throw new InvalidOperationException("Crop irrigation observation rejected: " + delivery.Detail);
            irrigationTransactions.CommitPreparedSupply(prepared);
            ApplyIrrigationAcknowledgement(state, delivery);
            MarkChanged();
            return supplied;
        }
        catch
        {
            if (!publicationMayBeCommitted)
            {
                if (ownerChanged)
                {
                    state.CurrentWater = waterBefore;
                    state.NextWaterRefillOperationSequence = sequence;
                    state.CycleExecutionReceipt = cycleBefore;
                    state.IrrigationPublication = new CropIrrigationPublicationSaveData();
                }
                if (prepared?.IsPending == true)
                    irrigationTransactions.RollbackPreparedSupply(prepared);
                if (lease.IsValid) publisher.Cancel(lease);
            }
            else if (prepared?.IsPending == true)
                irrigationTransactions.CommitPreparedSupply(prepared);
            throw;
        }
    }

    private bool TryAdvanceIrrigationPublication(CropPlotState state)
    {
        if (state == null || !state.IrrigationPublication.pending) return true;
        CropIrrigationPublicationSaveData owner = state.IrrigationPublication;
        DomainFactDelivery delivery = DeliverCropCommandFact(default, owner.publicationKey,
            new CropIrrigationSuppliedFact(owner));
        return ApplyIrrigationAcknowledgement(state, delivery);
    }

    private bool ApplyIrrigationAcknowledgement(CropPlotState state, DomainFactDelivery delivery)
    {
        CropIrrigationPublicationSaveData owner = state.IrrigationPublication;
        if (!ConfirmCropCommandPublication(delivery, owner.publicationKey,
                owner.publicationId, owner.fingerprint, out string failure))
        {
            owner.failureReason = failure;
            snapshotsDirty = true;
            return false;
        }
        owner.publicationId = delivery.PublicationId;
        owner.fingerprint = delivery.Fingerprint;
        if (delivery.Phase != DomainFactDeliveryPhase.Delivered)
        {
            owner.failureReason = delivery.Detail;
            snapshotsDirty = true;
            return false;
        }
        state.IrrigationPublication = new CropIrrigationPublicationSaveData();
        MarkChanged(replan: false);
        return true;
    }

    private static void ValidateIrrigationPublication(CropPlotSaveData saved, BuildingInstanceId plotId)
    {
        CropIrrigationPublicationSaveData owner = saved.irrigationPublication
            ?? throw new InvalidOperationException("Crop irrigation publication owner is missing.");
        if (owner.IsEmpty) return;
        _ = new CropIrrigationSuppliedFact(owner);
        ValidateCropCommandPublicationKey(owner.publicationKey);
        bool acknowledged = !string.IsNullOrEmpty(owner.publicationId);
        if (owner.plotId != plotId.Value
            || saved.nextWaterRefillOperationSequence != owner.operationSequence + 1
            || owner.quantity != CropWaterRules.RefillQuantity
            || owner.waterAfter > saved.waterCapacity
            || Math.Abs(saved.currentWater - owner.waterAfter) > 0.0001f
            || (acknowledged
                ? !IsCanonicalToken(owner.publicationId) || !IsCropCommandFingerprint(owner.fingerprint)
                : !string.IsNullOrEmpty(owner.fingerprint))
            || saved.cycleExecutionReceipt == null || saved.cycleExecutionReceipt.IsEmpty
            || Math.Abs(saved.cycleExecutionReceipt.waterAfter - owner.waterAfter) > 0.0001f
            || saved.cycleExecutionReceipt.irrigationChildResultKeys == null
            || !saved.cycleExecutionReceipt.irrigationChildResultKeys.Contains(owner.publicationKey))
            throw new InvalidOperationException("Crop irrigation publication contradicts its applied owner.");
    }

    private static void RecordCycleWaterSupply(
        CropPlotState state,
        float waterBefore,
        float waterAfter,
        string childResultKey)
    {
        if (state?.CycleExecutionReceipt == null
            || state.CycleExecutionReceipt.IsEmpty)
            return;
        if (state.CycleExecutionReceipt.status
            != CropCycleExecutionReceiptStatus.Active)
            throw new InvalidOperationException(
                "Crop water supply has no active cycle receipt.");
        state.CycleExecutionReceipt = CropPlanExecutionReceiptAuthority
            .RecordWaterSupply(
                state.CycleExecutionReceipt,
                waterBefore,
                waterAfter,
                childResultKey);
    }

    public static string FormatWaterRefillOperationId(
        BuildingInstanceId plotId,
        int sequence)
    {
        if (!plotId.IsValid || sequence < 0)
            throw new ArgumentException(
                "A valid crop plot and non-negative water-refill sequence are required.");
        return WaterRefillOperationPrefix
            + plotId.Value + ":"
            + sequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
    }

    private void BeginWaterRefill(CropPlotState state)
    {
        if (state == null
            || state.WaterRefill.phase != CropWaterRefillPhase.None)
            return;
        int sequence = state.NextWaterRefillOperationSequence;
        string operationId = FormatWaterRefillOperationId(
            state.PlotId,
            sequence);
        string destinationId =
            CropPlotInputOwnerAuthority.BuildWaterRefillDestinationId(
                state.PlotId.Value,
                sequence);
        float requiredWork = CropWaterRules.RequireRefillWork(state.Building);
        CropWaterRefillSaveData refill = new()
        {
            phase = CropWaterRefillPhase.WaitingForDelivery,
            outcomeSchemaVersion =
                CropWaterRefillSaveData.CurrentOutcomeSchemaVersion,
            operationSequence = sequence,
            operationId = operationId,
            reasonCode = "crop-water-refill-input",
            destinationId = destinationId,
            itemId = CropCycleInputRequirementAuthority.CleanWaterItemId,
            quantity = CropWaterRules.RefillQuantity,
            requiredWork = requiredWork,
            requestFingerprint = CaptureWaterRefillRequestFingerprint(
                state.PlotId,
                sequence,
                destinationId,
                requiredWork)
        };
        CropPlotInputOwnerDescriptor descriptor =
            CreateWaterRefillInputOwnerDescriptor(state, refill);
        if (!inputOwners.TryEnsure(descriptor, out string ownerFailure))
        {
            state.BlockedReason =
                "crop-water-refill-input-owner-unavailable:" + ownerFailure;
            snapshotsDirty = true;
            return;
        }
        state.WaterRefill = refill;
        MarkChanged();
    }

    private void TickWaterRefillDelivery(
        CropPlotState state,
        bool requestMaterials)
    {
        CropWaterRefillSaveData refill = state?.WaterRefill;
        if (refill == null
            || refill.phase is CropWaterRefillPhase.None
                or CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted)
            return;

        CropPlotInputOwnerDescriptor descriptor =
            CreateWaterRefillInputOwnerDescriptor(state, refill);
        if (!inputOwners.TryEnsure(descriptor, out string ownerFailure))
        {
            refill.failureReason =
                "crop-water-refill-input-owner-unavailable:" + ownerFailure;
            snapshotsDirty = true;
            return;
        }

        int delivered = items.CountDelivered(
            refill.itemId,
            refill.destinationId);
        if (delivered >= refill.quantity)
        {
            if (refill.phase == CropWaterRefillPhase.WaitingForDelivery)
            {
                refill.phase = CropWaterRefillPhase.ReadyForWork;
                refill.failureReason = string.Empty;
                MarkChanged();
            }
            return;
        }

        refill.phase = CropWaterRefillPhase.WaitingForDelivery;
        refill.failureReason = $"급수용 물 운반 대기 {delivered}/{refill.quantity}";
        snapshotsDirty = true;
        if (!requestMaterials)
            return;

        int pending = items.CountPending(refill.itemId, refill.destinationId);
        int missing = Mathf.Max(0, refill.quantity - pending);
        if (missing <= 0)
            return;
        if (items.RequestDelivery(
                refill.itemId,
                missing,
                state.Building.centerPos,
                refill.destinationId,
                out int requested,
                out string failureReason)
            && requested > 0)
        {
            items.PrioritizeDestination(refill.destinationId);
            workforce.RequestOneHaulerToReplan(forceInterrupt: false);
            refill.failureReason = string.Empty;
            MarkChanged(replan: false);
            return;
        }
        refill.failureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "사용 가능한 깨끗한 물이 없습니다."
            : failureReason;
    }

    private bool TryFinalizeWaterRefill(CropPlotState state)
    {
        CropWaterRefillSaveData refill = state?.WaterRefill;
        if (refill == null || refill.phase == CropWaterRefillPhase.None)
            return false;
        if (refill.outcomeSchemaVersion != CropWaterRefillSaveData.CurrentOutcomeSchemaVersion)
        {
            refill.failureReason = "crop-water-refill-outcome-schema-unsupported";
            snapshotsDirty = true;
            return false;
        }
        if (refill.phase is not (CropWaterRefillPhase.Working
                or CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted))
            return false;
        if (string.IsNullOrWhiteSpace(refill.responsibleWorkerId)
            || string.IsNullOrWhiteSpace(refill.responsibleWorkerDisplayName)
            || refill.workContributions == null || refill.workContributions.Count == 0)
        {
            refill.failureReason = "crop-water-refill-worker-required";
            snapshotsDirty = true;
            return false;
        }

        DomainFactLease reservation = default;
        try
        {
            if (refill.phase is CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted)
            {
                if (Math.Abs(state.CurrentWater - refill.waterAfter) > 0.0001f)
                {
                    refill.failureReason = "crop-water-refill-water-after-conflict";
                    snapshotsDirty = true;
                    return false;
                }
                return TryFinishCommittedWaterRefill(state, refill,
                    DeliverCropCommandFact(default, refill.publicationKey,
                        new CropWaterRefillFact(state.PlotId, refill)));
            }

            if (refill.completedWork + 0.001f < refill.requiredWork)
                return false;
            if (state.CurrentWater + refill.quantity > state.WaterCapacity + 0.001f)
            {
                refill.failureReason = "crop-water-refill-capacity-exceeded";
                snapshotsDirty = true;
                return false;
            }

            // Reserve observation capacity for a proposal, before consuming any
            // input. Only the subsequent applied fact can create a completion.
            CropWaterRefillSaveData proposal = refill.DeepClone();
            if (refill.phase == CropWaterRefillPhase.Working)
            {
                proposal.plotDisplayName = state.PlotDisplayName;
                proposal.itemDisplayName = ResolveTreatmentDisplayName(refill.itemId);
                proposal.locationX = state.LastKnownPosition.x;
                proposal.locationY = state.LastKnownPosition.y;
                proposal.waterBefore = state.CurrentWater;
                proposal.completedAbsoluteDay = CurrentAbsoluteDay;
            }
            if (!RequireDomainFacts().TryReserve(
                    new CropWaterRefillFact(state.PlotId, proposal),
                    out reservation, out string reserveFailure))
            {
                refill.failureReason = reserveFailure;
                snapshotsDirty = true;
                return false;
            }
            if (!string.IsNullOrEmpty(refill.publicationKey)
                && refill.publicationKey != reservation.PublicationKey)
                throw new InvalidOperationException("Crop water refill publication identity drifted.");
            proposal.publicationKey = reservation.PublicationKey;
            state.WaterRefill = refill = proposal;

            if (refill.phase == CropWaterRefillPhase.Working)
            {
                Dictionary<string, int> costs = new(StringComparer.Ordinal)
                {
                    [refill.itemId] = refill.quantity
                };
                if (!items.ConsumeDeliveredToWip(
                        refill.destinationId, costs, refill.operationId,
                        out ProductionWipInputReceipt receipt, out string commitFailure))
                {
                    refill.failureReason = commitFailure;
                    RequireDomainFacts().Cancel(reservation);
                    reservation = default;
                    snapshotsDirty = true;
                    return false;
                }
                string[] sourceStackIds = (receipt.SourceStackIds ?? Array.Empty<string>())
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                // Keep the committed physical receipt even if validation fails:
                // retry must never consume the material a second time.
                refill.commitId = receipt.CommitId;
                refill.inputQuantity = receipt.Quantity;
                refill.inputMassGrams = receipt.InputMassGrams;
                refill.physicalRequestFingerprint = receipt.PhysicalRequestFingerprint;
                refill.physicalOutcomeOwnerRevision = receipt.PhysicalOutcomeOwnerRevision;
                refill.sourceStackIds = sourceStackIds.ToList();
                refill.phase = CropWaterRefillPhase.InputCommitted;
                MarkChanged(replan: false);
                long expectedMass = items.GetDefinitionQuantityMassGrams(refill.itemId, refill.quantity);
                if (!receipt.IsCommitted || receipt.PhysicalOutcomeOwnerRevision <= 0L
                    || !IsCanonicalToken(receipt.PhysicalRequestFingerprint)
                    || sourceStackIds.Length == 0 || sourceStackIds.Any(value => !IsCanonicalToken(value))
                    || sourceStackIds.Distinct(StringComparer.Ordinal).Count() != sourceStackIds.Length
                    || receipt.Quantity != refill.quantity || receipt.InputMassGrams != expectedMass)
                    throw new InvalidOperationException(
                        "Crop water refill WIP receipt contradicts its exact physical input.");
            }

            if (Math.Abs(state.CurrentWater - refill.waterBefore) > 0.0001f)
            {
                refill.failureReason = "crop-water-refill-water-before-conflict";
                RequireDomainFacts().Cancel(reservation);
                reservation = default;
                snapshotsDirty = true;
                return false;
            }
            if (refill.inputQuantity != refill.quantity
                || refill.inputMassGrams != items.GetDefinitionQuantityMassGrams(refill.itemId, refill.quantity))
                throw new InvalidOperationException("Crop water refill committed input mass contradicts its intent.");
            state.CurrentWater = Mathf.Min(state.WaterCapacity, state.CurrentWater + refill.quantity);
            refill.waterAfter = state.CurrentWater;
            refill.phase = CropWaterRefillPhase.OutcomePublished;
            refill.failureReason = string.Empty;
            MarkChanged();

            DomainFactDelivery delivered = DeliverCropCommandFact(
                reservation, refill.publicationKey, new CropWaterRefillFact(state.PlotId, refill));
            reservation = default;
            return TryFinishCommittedWaterRefill(state, refill, delivered);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            if (reservation.IsValid) RequireDomainFacts().Cancel(reservation);
            refill.failureReason = "crop-water-refill-finalize-threw:"
                + exception.GetType().Name + ":" + exception.Message;
            snapshotsDirty = true;
            return false;
        }
    }

    private bool TryFinishCommittedWaterRefill(
        CropPlotState state, CropWaterRefillSaveData refill, DomainFactDelivery delivered)
    {
        if (!ConfirmCropCommandPublication(delivered, refill.publicationKey,
                refill.gameplayOutcomeId, refill.gameplayOutcomeDigest, out string failure))
        {
            refill.failureReason = failure;
            snapshotsDirty = true;
            return false;
        }
        refill.phase = CropWaterRefillPhase.LedgerCommitted;
        refill.gameplayOutcomeId = delivered.PublicationId;
        refill.gameplayOutcomeDigest = delivered.Fingerprint;
        if (delivered.Phase != DomainFactDeliveryPhase.Delivered)
        {
            refill.failureReason = delivered.Detail;
            snapshotsDirty = true;
            return false;
        }
        if (!TryRetireDestination(state, refill.destinationId,
                CropPlotInputOwnerAuthority.WaterRefillCompletedReleaseReasonCode,
                out string retireFailure))
        {
            refill.failureReason = retireFailure;
            return false;
        }
        if (!items.AcknowledgeWipInput(refill.commitId, out string acknowledgeFailure))
        {
            refill.failureReason = acknowledgeFailure;
            return false;
        }
        RecordCycleWaterSupply(state, refill.waterBefore, refill.waterAfter, refill.publicationKey);
        state.NextWaterRefillOperationSequence = checked(refill.operationSequence + 1);
        state.WaterRefill = new CropWaterRefillSaveData();
        MarkChanged();
        return true;
    }

    private bool TryRetireInactiveWaterRefill(CropPlotState state)
    {
        CropWaterRefillSaveData refill = state?.WaterRefill;
        if (refill == null || refill.phase == CropWaterRefillPhase.None)
            return true;
        if (refill.phase is CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted)
            return TryFinalizeWaterRefill(state);
        if (!TryRetireDestination(
                state,
                refill.destinationId,
                CropPlotInputOwnerAuthority
                    .WaterRefillNoLongerRequiredReleaseReasonCode,
                out string retireFailure))
        {
            refill.failureReason = retireFailure;
            snapshotsDirty = true;
            return false;
        }
        state.NextWaterRefillOperationSequence = checked(
            state.NextWaterRefillOperationSequence + 1);
        state.WaterRefill = new CropWaterRefillSaveData();
        MarkChanged();
        return true;
    }

    private static string CaptureWaterRefillRequestFingerprint(
        BuildingInstanceId plotId,
        int sequence,
        string destinationId,
        float requiredWork)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-water-refill-request@1");
        digest.Append(plotId.Value);
        digest.Append(sequence);
        digest.Append(FormatWaterRefillOperationId(plotId, sequence));
        digest.Append(destinationId);
        digest.Append(CropCycleInputRequirementAuthority.CleanWaterItemId);
        digest.Append(CropWaterRules.RefillQuantity);
        digest.AppendFloat(requiredWork);
        digest.AppendFloat(CropWaterRules.WaterCapacity);
        return digest.ComputeSha256();
    }

    private static string ResolveWaterRefillUnavailableReason(
        CropPlotState state)
    {
        CropWaterRefillSaveData refill = state?.WaterRefill;
        if (refill == null || refill.phase == CropWaterRefillPhase.None)
            return "예약된 급수 작업이 없습니다.";
        if (!string.IsNullOrWhiteSpace(refill.failureReason))
            return refill.failureReason;
        if (state.Phase != CropPlotPhase.Growing)
            return "성장이 끝난 경작지는 보충 급수가 필요하지 않습니다.";
        return refill.phase switch
        {
            CropWaterRefillPhase.WaitingForDelivery => "급수용 물 운반 대기",
            CropWaterRefillPhase.InputCommitted => "물리 물 투입 확정 대기",
            CropWaterRefillPhase.OutcomePublished => "물 투입 승인 대기",
            CropWaterRefillPhase.LedgerCommitted => "물 투입 원장 승인 대기",
            _ => string.Empty
        };
    }

    private static bool ShouldBeginManualWaterRefill(
        CropIrrigationStatus status) =>
        status is CropIrrigationStatus.NoOperationalIrrigator
            or CropIrrigationStatus.OutOfRange
            or CropIrrigationStatus.RouteBlocked
            or CropIrrigationStatus.NetworkUnavailable
            or CropIrrigationStatus.WaterUnavailable;

    private void TickTreatmentDelivery(
        CropPlotState state,
        bool requestMaterials)
    {
        CropTreatmentOrderSaveData treatment = state?.Treatment;
        if (treatment == null
            || treatment.phase is CropTreatmentOrderPhase.None
                or CropTreatmentOrderPhase.InputCommitted
                or CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted
                or CropTreatmentOrderPhase.PlotDestroyedLossPending)
            return;

        CropPlotInputOwnerDescriptor inputOwner =
            CreateTreatmentInputOwnerDescriptor(state, treatment);
        if (!inputOwners.TryEnsure(inputOwner, out string ownerFailure))
        {
            treatment.failureReason =
                "crop-treatment-input-owner-unavailable:" + ownerFailure;
            snapshotsDirty = true;
            return;
        }

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
        if (treatment == null || treatment.phase == CropTreatmentOrderPhase.None)
            return false;
        if (treatment.outcomeSchemaVersion != CropTreatmentOrderSaveData.CurrentOutcomeSchemaVersion)
        {
            treatment.failureReason = "crop-treatment-outcome-schema-unsupported";
            snapshotsDirty = true;
            return false;
        }
        if (treatment.phase is not (CropTreatmentOrderPhase.Working
            or CropTreatmentOrderPhase.InputCommitted or CropTreatmentOrderPhase.OutcomePublished
            or CropTreatmentOrderPhase.LedgerCommitted))
            return false;
        if (string.IsNullOrWhiteSpace(treatment.responsibleWorkerId)
            || string.IsNullOrWhiteSpace(treatment.responsibleWorkerDisplayName)
            || treatment.workContributions == null || treatment.workContributions.Count == 0)
        {
            treatment.failureReason = "crop-treatment-worker-required";
            snapshotsDirty = true;
            return false;
        }

        DomainFactLease reservation = default;
        try
        {
            if (treatment.phase is CropTreatmentOrderPhase.OutcomePublished or CropTreatmentOrderPhase.LedgerCommitted)
            {
                if (treatment.phase == CropTreatmentOrderPhase.OutcomePublished
                    && treatment.ecologyAfterFingerprint != CreateEcologyFingerprint(state.PlotId.Value))
                {
                    treatment.failureReason = "crop-treatment-ecology-after-conflict";
                    return false;
                }
                return TryFinishCommittedTreatment(state, treatment,
                    DeliverCropCommandFact(default, treatment.publicationKey,
                        new CropTreatmentFact(state.PlotId, treatment)));
            }
            if (treatment.completedWork + 0.001f < treatment.requiredWork)
                return false;

            CropTreatmentOrderSaveData proposal = treatment.DeepClone();
            if (treatment.phase == CropTreatmentOrderPhase.Working)
            {
                CropEcologyPlotSaveData before = RequireTreatmentEcology(state);
                string beforeFingerprint = CreateEcologyFingerprint(state.PlotId.Value);
                if (string.IsNullOrEmpty(proposal.ecologyBeforeFingerprint))
                {
                    proposal.ecologyBeforeFingerprint = beforeFingerprint;
                    proposal.pestPressureBefore = before.pestPressure;
                    proposal.diseasePressureBefore = before.diseasePressure;
                    proposal.cooldownBeforeDay = GetNextTreatmentAllowedDay(state, proposal.treatmentKind);
                    proposal.completedAbsoluteDay = CurrentAbsoluteDay;
                    proposal.plotDisplayName = state.PlotDisplayName;
                    proposal.itemDisplayName = ResolveTreatmentDisplayName(proposal.itemId);
                    proposal.locationX = state.LastKnownPosition.x;
                    proposal.locationY = state.LastKnownPosition.y;
                }
                else if (proposal.ecologyBeforeFingerprint != beforeFingerprint)
                {
                    treatment.failureReason = "crop-treatment-ecology-before-conflict";
                    return false;
                }
            }

            // The observer reserves only a proposal's bounded capacity. No
            // applied receipt exists before physical input and ecology effects.
            if (!RequireDomainFacts().TryReserve(new CropTreatmentFact(state.PlotId, proposal),
                    out reservation, out string reserveFailure))
            {
                treatment.failureReason = reserveFailure;
                return false;
            }
            if (!string.IsNullOrEmpty(treatment.publicationKey)
                && treatment.publicationKey != reservation.PublicationKey)
                throw new InvalidOperationException("Crop treatment publication identity drifted.");
            proposal.publicationKey = reservation.PublicationKey;
            state.Treatment = treatment = proposal;

            // Both a first consumption and a saved InputCommitted continuation
            // must match the actual physical authority's pending receipt.
            if (!CropTreatmentPhysicalOutbox.TryCommitOrResume(treatment, treatmentItems, out string commitFailure))
            {
                treatment.failureReason = commitFailure;
                RequireDomainFacts().Cancel(reservation);
                reservation = default;
                return false;
            }
            if (treatment.ecologyBeforeFingerprint != CreateEcologyFingerprint(state.PlotId.Value))
            {
                treatment.failureReason = "crop-treatment-ecology-before-conflict";
                RequireDomainFacts().Cancel(reservation);
                reservation = default;
                return false;
            }
            Vector2Int position = state.Building != null ? state.Building.centerPos : state.LastKnownPosition;
            if (!CropTreatmentPhysicalOutbox.EnsureTareOutputs(treatment, position, packagedTare, out string tareFailure))
            {
                treatment.failureReason = tareFailure;
                RequireDomainFacts().Cancel(reservation);
                reservation = default;
                return false;
            }

            switch (treatment.treatmentKind)
            {
                case CropTreatmentKind.PestLure:
                case CropTreatmentKind.BotanicalPesticide:
                    ecology.ApplyPestControl(state.PlotId.Value, treatment.effectAmount);
                    break;
                case CropTreatmentKind.Fungicide:
                    ecology.ApplyFungicide(state.PlotId.Value, treatment.effectAmount);
                    break;
                default:
                    throw new InvalidOperationException("crop-treatment-kind-invalid");
            }
            CropEcologyPlotSaveData after = RequireTreatmentEcology(state);
            treatment.pestPressureAfter = after.pestPressure;
            treatment.diseasePressureAfter = after.diseasePressure;
            treatment.ecologyAfterFingerprint = CreateEcologyFingerprint(state.PlotId.Value);
            treatment.cooldownAfterDay = checked(treatment.completedAbsoluteDay + treatment.cooldownDays);
            SetNextTreatmentAllowedDay(state, treatment.treatmentKind, treatment.cooldownAfterDay);
            treatment.phase = CropTreatmentOrderPhase.OutcomePublished;
            treatment.failureReason = string.Empty;
            MarkChanged();

            DomainFactDelivery delivered = DeliverCropCommandFact(reservation, treatment.publicationKey,
                new CropTreatmentFact(state.PlotId, treatment));
            reservation = default;
            return TryFinishCommittedTreatment(state, treatment, delivered);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            if (reservation.IsValid) RequireDomainFacts().Cancel(reservation);
            treatment.failureReason = "crop-treatment-finalize-threw:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        finally { snapshotsDirty = true; }
    }

    private CropEcologyPlotSaveData RequireTreatmentEcology(CropPlotState state)
    {
        CropEcologyPlotSaveData plot = ecology.Plots.SingleOrDefault(value =>
            value != null
            && state != null
            && string.Equals(
                value.plotId,
                state.PlotId.Value,
                StringComparison.Ordinal));
        return plot ?? throw new InvalidOperationException(
            "Crop treatment requires an owned ecology plot.");
    }

    private bool TryFinishCommittedTreatment(
        CropPlotState state, CropTreatmentOrderSaveData treatment, DomainFactDelivery delivered)
    {
        if (!ConfirmCropCommandPublication(delivered, treatment.publicationKey,
                treatment.gameplayOutcomeId, treatment.gameplayOutcomeDigest, out string failure))
        {
            treatment.failureReason = failure;
            snapshotsDirty = true;
            return false;
        }
        treatment.phase = CropTreatmentOrderPhase.LedgerCommitted;
        treatment.gameplayOutcomeId = delivered.PublicationId;
        treatment.gameplayOutcomeDigest = delivered.Fingerprint;
        if (delivered.Phase != DomainFactDeliveryPhase.Delivered)
        {
            treatment.failureReason = delivered.Detail;
            snapshotsDirty = true;
            return false;
        }
        if (!TryRetireDestination(state, treatment.destinationId,
                CropPlotInputOwnerAuthority.TreatmentCompletedReleaseReasonCode, out string retireFailure))
        {
            treatment.failureReason = retireFailure;
            snapshotsDirty = true;
            return false;
        }
        if (!CropTreatmentPhysicalOutbox.TryAcknowledgeOutcome(treatment, treatmentItems, out string acknowledgeFailure))
        {
            treatment.failureReason = acknowledgeFailure;
            snapshotsDirty = true;
            return false;
        }
        state.NextTreatmentOperationSequence = checked(treatment.operationSequence + 1);
        CropTreatmentPhysicalOutbox.Clear(treatment);
        MarkChanged();
        return true;
    }

    private string CreateEcologyFingerprint(string plotId)
        => CropPhysicalTransactionOutbox.CreateEcologyFingerprint(
            ecology.Plots,
            plotId);

    private int CurrentAbsoluteDay => calendar.Day;

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
            CropTreatmentOrderPhase.LedgerCommitted => "원장 승인 대기",
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

    private static string BuildTreatmentDestinationId(
        BuildingInstanceId plotId,
        int operationSequence) =>
        CropPlotInputOwnerAuthority.BuildTreatmentDestinationId(
            plotId.Value,
            operationSequence);

    private static string BuildSowDestinationId(
        BuildingInstanceId plotId,
        int operationSequence) =>
        CropPlotInputOwnerAuthority.BuildSowDestinationId(
            plotId.Value,
            operationSequence);

    private IReadOnlyList<CropPlotInputOwnerDescriptor>
        BuildInputOwnerDescriptors(
            CropPlotState state,
            BuildableObject building,
            BuildingCropPlotAbility ability)
    {
        if (state == null
            || building == null
            || building.IsBuildingDestroyed
            || ability == null
            || !building.PersistentInstanceId.Equals(state.PlotId)
            || building.centerPos != state.LastKnownPosition
                && state.Building == null)
        {
            throw new InvalidOperationException(
                "Crop-plot input owner source is not attached to its exact live facility.");
        }

        List<CropPlotInputOwnerDescriptor> descriptors = new();
        if (RequiresSowInputAuthority(state))
        {
            if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop)
                || crop == null)
                throw new InvalidOperationException(
                    "Crop-plot input owner references an unknown crop: "
                    + state.CropId);
            IReadOnlyDictionary<string, int> requirements =
                BuildMaterialRequirements(state, crop, ability);
            descriptors.Add(CreateSowInputOwnerDescriptor(
                state,
                requirements,
                building.centerPos));
        }
        if (state.Treatment?.phase != CropTreatmentOrderPhase.None)
            descriptors.Add(CreateTreatmentInputOwnerDescriptor(
                state,
                state.Treatment,
                building.centerPos));
        if (state.WaterRefill?.phase != CropWaterRefillPhase.None)
            descriptors.Add(CreateWaterRefillInputOwnerDescriptor(
                state,
                state.WaterRefill,
                building.centerPos));
        return descriptors;
    }

    private CropPlotInputOwnerDescriptor CreateSowInputOwnerDescriptor(
        CropPlotState state,
        IReadOnlyDictionary<string, int> requirements,
        Vector2Int? position = null) => new(
        state.PlotId.Value,
        position ?? state.Building?.centerPos ?? state.LastKnownPosition,
        state.MaterialDestinationId,
        CropPhysicalTransactionOutbox.FormatSowOperationId(
            state.PlotId.Value,
            state.NextSowOperationSequence),
        requirements);

    private static CropPlotInputOwnerDescriptor
        CreateTreatmentInputOwnerDescriptor(
            CropPlotState state,
            CropTreatmentOrderSaveData treatment,
            Vector2Int? position = null)
    {
        if (state == null || treatment == null
            || treatment.phase == CropTreatmentOrderPhase.None)
            throw new ArgumentException(
                "A live crop-treatment input owner is required.");
        return new CropPlotInputOwnerDescriptor(
            state.PlotId.Value,
            position ?? state.Building?.centerPos ?? state.LastKnownPosition,
            treatment.destinationId,
            treatment.operationId,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [treatment.itemId] = treatment.quantity
            });
    }

    private static CropPlotInputOwnerDescriptor
        CreateWaterRefillInputOwnerDescriptor(
            CropPlotState state,
            CropWaterRefillSaveData refill,
            Vector2Int? position = null)
    {
        if (state == null || refill == null
            || refill.phase == CropWaterRefillPhase.None)
            throw new ArgumentException(
                "A live crop water-refill input owner is required.");
        return new CropPlotInputOwnerDescriptor(
            state.PlotId.Value,
            position ?? state.Building?.centerPos ?? state.LastKnownPosition,
            refill.destinationId,
            refill.operationId,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [refill.itemId] = refill.quantity
            });
    }

    private static bool RequiresAnyInputAuthority(CropPlotState state) =>
        RequiresSowInputAuthority(state)
        || state?.Treatment?.phase != CropTreatmentOrderPhase.None
        || state?.WaterRefill?.phase != CropWaterRefillPhase.None;

    private static bool RequiresSowInputAuthority(CropPlotState state) =>
        state != null
        && (state.PendingSow?.phase != CropPhysicalCommitPhase.None
            || HasFrozenSowInputs(state)
            && !state.MaterialsConsumed
            && state.Phase is CropPlotPhase.Empty
                or CropPlotPhase.WaitingForMaterials);

    private bool TryRetireDestination(
        CropPlotState state,
        string destinationId,
        string reasonCode,
        out string failureReason)
    {
        Vector2Int position = state?.Building != null
            ? state.Building.centerPos
            : state?.LastKnownPosition ?? default;
        return inputOwners.TryRetireDestination(
            destinationId,
            position,
            reasonCode,
            out failureReason);
    }

    private Dictionary<string, int> BuildMaterialRequirements(
        CropPlotState state,
        CropDefinitionSO crop,
        BuildingCropPlotAbility ability = null)
    {
        if (state == null || crop == null
            || state.FrozenSowInputOperationSequence
                != state.NextSowOperationSequence
            || state.FrozenSowInputs == null
            || state.FrozenSowInputs.Count == 0
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                state.FrozenSowInputSourceDigest)
            || !string.Equals(
                state.FrozenSowInputVectorDigest,
                CaptureFrozenSowInputVectorDigest(
                    state.PlotId,
                    crop.CropId,
                    state.FrozenSowInputOperationSequence,
                    state.FrozenSowInputs),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop sow input requirements are not frozen for the active operation.");
        }
        ability ??= state.Ability;
        if (ability == null)
            throw new InvalidOperationException(
                "Crop sow input requirements have no facility ability authority.");
        _ = inputRequirements.RehydrateAndValidate(
            crop,
            ability,
            state.MaterialDestinationId,
            state.FrozenSowInputWeather,
            state.FrozenSowInputConsumptionMultiplier,
            state.FrozenSowInputSelectedFuelItemId,
            state.FrozenSowInputs,
            state.FrozenSowInputSourceDigest);
        return new Dictionary<string, int>(
            state.FrozenSowInputs,
            StringComparer.Ordinal);
    }

    private void FreezeSowInputs(CropPlotState state, CropDefinitionSO crop)
    {
        CropCycleInputRequirementSnapshot snapshot = inputRequirements.Capture(
            crop,
            state.Ability,
            state.MaterialDestinationId,
            environmentQuery.GetEnvironmentSnapshot().Weather,
            milestoneModifiers.WaterAndFertilizerConsumptionMultiplier,
            (itemId, excludedDestinationId) => items.CountAvailableStock(
                itemId,
                excludedDestinationId));
        state.FrozenSowInputOperationSequence = state.NextSowOperationSequence;
        state.FrozenSowInputSourceDigest = snapshot.SourceDigest;
        state.FrozenSowInputWeather = snapshot.Weather;
        state.FrozenSowInputConsumptionMultiplier =
            snapshot.MilestoneConsumptionMultiplier;
        state.FrozenSowInputSelectedFuelItemId = snapshot.SelectedFuelItemId;
        state.FrozenSowInputs = new Dictionary<string, int>(
            snapshot.Requirements,
            StringComparer.Ordinal);
        state.FrozenSowInputVectorDigest = CaptureFrozenSowInputVectorDigest(
            state.PlotId,
            crop.CropId,
            state.FrozenSowInputOperationSequence,
            state.FrozenSowInputs);
    }

    private static void ClearFrozenSowInputs(CropPlotState state)
    {
        state.FrozenSowInputOperationSequence = -1;
        state.FrozenSowInputSourceDigest = string.Empty;
        state.FrozenSowInputVectorDigest = string.Empty;
        state.FrozenSowInputWeather = default;
        state.FrozenSowInputConsumptionMultiplier = 0f;
        state.FrozenSowInputSelectedFuelItemId = string.Empty;
        state.FrozenSowInputs.Clear();
    }

    private static bool HasFrozenSowInputs(CropPlotState state) =>
        state != null
        && state.FrozenSowInputOperationSequence >= 0
        && state.FrozenSowInputs != null
        && state.FrozenSowInputs.Count > 0;

    private static string CaptureFrozenSowInputVectorDigest(
        BuildingInstanceId plotId,
        string cropId,
        int operationSequence,
        IReadOnlyDictionary<string, int> requirements)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-sow-frozen-input-vector@1");
        digest.Append(plotId.Value);
        digest.Append(cropId);
        digest.Append(operationSequence);
        digest.Append(requirements.Count);
        foreach (KeyValuePair<string, int> value in requirements.OrderBy(
                     entry => entry.Key,
                     StringComparer.Ordinal))
        {
            digest.Append(value.Key);
            digest.Append(value.Value);
        }
        return digest.ComputeSha256();
    }

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
                    state.PlotDisplayName = RequirePlotDisplayName(building);
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
                removed.PlotDisplayName = RequirePlotDisplayName(removed.Building);
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
                FreezeFailedOutcome(removed, CurrentAbsoluteDay);
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
                FreezeFailedOutcome(removed, CurrentAbsoluteDay);
            }
            if (IsFailedCycleReceipt(removed.CycleExecutionReceipt))
            {
                removed.Building = null;
                removed.Ability = null;
                removed.Phase = CropPlotPhase.Blocked;
                removed.BlockedReason = "crop-plot-destroyed-awaiting-outcome";
                continue;
            }
            if (!TryRetireDestination(
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
                if (!TryRetireDestination(
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
            FreezeFailedOutcome(state, Math.Max(0, ended.day));
            state.SeasonalYieldDamage =
                new CropSeasonalYieldDamageSaveData();
            state.Phase = CropPlotPhase.Blocked;
            state.BlockedReason = "작물이 극한 환경 또는 해충으로 고사했습니다.";
            MarkChanged();
            TryAdvanceFailedCycle(state);
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
            PlotDisplayName = RequirePlotDisplayName(building),
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

    private HashSet<string> RequireSaveRoot(
        DungeonCropPlotSaveData snapshot)
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
        if (snapshot.handledSeasonalStartInstanceIds == null
            || snapshot.handledSeasonalStartInstanceIds.Count > 2048)
            throw new InvalidOperationException(
                "Crop-plot handled seasonal starts are missing or exceed 2048 records.");
        HashSet<string> handled = new(StringComparer.Ordinal);
        foreach (string instanceId in snapshot.handledSeasonalStartInstanceIds)
        {
            if (!TryParseSeasonalEventInstanceId(
                    instanceId,
                    out _,
                    out string definitionId)
                || !storyCatalog.SeasonalEvents.Any(value => value != null
                    && value.cropPrimaryBatchLossPercent > 0
                    && string.Equals(
                        value.StableId,
                        definitionId,
                        StringComparison.Ordinal))
                || !handled.Add(instanceId))
                throw new InvalidOperationException(
                    "Crop-plot handled seasonal start IDs must be canonical and unique.");
        }
        return handled;
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
        BuildingInstanceId plotId,
        ISet<string> handledSeasonalStarts)
    {
        if (saved.retirementPublicationKey == null)
            throw new InvalidOperationException("Crop retirement publication key is missing.");
        if (saved.retirementPublicationKey.Length != 0)
        {
            ValidateCropCommandPublicationKey(saved.retirementPublicationKey);
            if (!string.IsNullOrEmpty(saved.pendingCycleCorrelationId)
                || saved.materialsConsumed || saved.pendingHarvest?.phase != CropHarvestOutputPhase.None
                || saved.pendingSow?.phase != CropPhysicalCommitPhase.None
                || saved.treatment?.phase != CropTreatmentOrderPhase.None
                || saved.waterRefill?.phase != CropWaterRefillPhase.None)
                throw new InvalidOperationException("Retiring crop plot still has active work or physical owners.");
        }
        if (!Enum.IsDefined(typeof(CropPlotPhase), saved.phase))
        {
            throw new InvalidOperationException(
                $"Crop-plot payload contains unknown phase {(int)saved.phase}.");
        }
        RequireFiniteRange(saved.sowWork, 0f, crop.SowWork, "sow work");
        RequireFiniteRange(saved.growthHours, 0f, crop.GrowthHours, "growth hours");
        RequireFiniteRange(saved.harvestWork, 0f, crop.HarvestWork, "harvest work");
        if (!saved.waterCapacity.Equals(CropWaterRules.WaterCapacity))
            throw new InvalidOperationException(
                "Crop-plot water capacity contradicts the current authored rule.");
        RequireFiniteRange(
            saved.currentWater,
            0f,
            saved.waterCapacity,
            "current water");
        if (saved.goldenHarvestAttemptSequence < 0
            || saved.nextCropSelectionOperationSequence < 0
            || saved.cropSelectionOutcome == null
            || saved.goldenHarvestScheduleOutcome == null)
            throw new InvalidOperationException(
                "Crop-plot command outcome owner or sequence is invalid.");
        if (saved.nextSowOperationSequence < 0 || saved.pendingSow == null)
            throw new InvalidOperationException(
                "Crop-plot sow transaction owner is missing or invalid.");
        if (saved.cycleExecutionReceipt == null)
            throw new InvalidOperationException(
                "Crop-plot cycle execution receipt owner is missing.");
        if (!string.IsNullOrEmpty(saved.pendingCycleCorrelationId)
            && (!string.Equals(
                    saved.pendingCycleCorrelationId,
                    saved.pendingCycleCorrelationId.Trim(),
                    StringComparison.Ordinal)
                || saved.pendingCycleCorrelationId.Any(char.IsWhiteSpace)
                || !saved.cycleExecutionReceipt.IsEmpty))
        {
            throw new InvalidOperationException(
                "Crop-plot pending cycle correlation is invalid or already consumed.");
        }
        if (saved.nextTreatmentOperationSequence < 0
            || saved.pestLureNextAllowedDay < 0
            || saved.botanicalPesticideNextAllowedDay < 0
            || saved.fungicideNextAllowedDay < 0
            || saved.treatment == null)
            throw new InvalidOperationException(
                "Crop-plot treatment owner or cooldown is invalid.");
        if (saved.nextWaterRefillOperationSequence < 0
            || saved.waterRefill == null)
            throw new InvalidOperationException(
                "Crop-plot water-refill owner or sequence is invalid.");
        if (saved.seasonalYieldDamage == null)
            throw new InvalidOperationException(
                "Crop-plot seasonal yield-damage owner is missing.");
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
        if (!saved.materialsConsumed && saved.currentWater != 0f)
            throw new InvalidOperationException(
                "An inactive crop cycle retained physical plot water.");
        if (crop.DailyWater <= 0f
            && (saved.currentWater != 0f
                || saved.waterRefill.phase != CropWaterRefillPhase.None))
            throw new InvalidOperationException(
                "A zero-demand crop retained water or refill provenance.");
        if (saved.waterRefill.phase != CropWaterRefillPhase.None
            && !saved.materialsConsumed)
            throw new InvalidOperationException(
                "Crop water-refill provenance requires an active physical cycle.");

        ValidatePendingSow(saved, crop);
        ValidateIrrigationPublication(saved, plotId);
        ValidateCropSelectionOutcome(saved, plotId);
        ValidateGoldenHarvestScheduleOutcome(saved, plotId);
        ValidateFrozenSowInputs(saved, crop, plotId);
        ValidateCycleExecutionReceipt(saved, crop, plotId);
        ValidateSeasonalYieldDamage(
            saved.seasonalYieldDamage,
            saved.cropId,
            saved.cycleExecutionReceipt,
            handledSeasonalStarts);
        if (!saved.seasonalYieldDamage.IsEmpty)
        {
            SeasonalWorldEventDefinitionSO sourceDefinition = storyCatalog
                .SeasonalEvents.SingleOrDefault(value => value != null
                    && string.Equals(
                        value.StableId,
                        saved.seasonalYieldDamage.sourceDefinitionId,
                        StringComparison.Ordinal));
            if (sourceDefinition == null
                || sourceDefinition.cropPrimaryBatchLossPercent
                    != saved.seasonalYieldDamage.primaryBatchLossPercent)
            {
                throw new InvalidOperationException(
                    "Crop seasonal yield damage contradicts its authored source declaration.");
            }
        }
        if (!saved.seasonalYieldDamage.IsEmpty
            && saved.phase is not (CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting))
            throw new InvalidOperationException(
                "Crop seasonal yield damage requires a live post-sow cycle.");
        ValidatePendingWaterRefill(saved, crop, plotId);
        ValidatePendingTreatment(saved, plotId);
        ValidatePendingHarvest(saved, crop, plotId);

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

    private void ValidatePendingHarvest(
        CropPlotSaveData saved,
        CropDefinitionSO crop,
        BuildingInstanceId plotId)
    {
        CropHarvestOutputSaveData owner = saved.pendingHarvest;
        if (saved.nextHarvestOperationSequence < 0 || owner == null)
            throw new InvalidOperationException(
                "Crop harvest output owner or sequence is invalid.");
        if (owner.phase == CropHarvestOutputPhase.None)
        {
            bool empty = owner.operationSequence == 0
                && string.IsNullOrEmpty(owner.operationId)
                && string.IsNullOrEmpty(owner.cropId)
                && !owner.indoor
                && string.IsNullOrEmpty(owner.harvesterId)
                && string.IsNullOrEmpty(owner.outcomeId)
                && string.IsNullOrEmpty(owner.ecologyOutcomeFingerprint)
                && string.IsNullOrEmpty(owner.ecologyBeforeFingerprint)
                && string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
                && !owner.ecologyCommitted
                && !owner.ecologyAcknowledged
                && !owner.goldenPrepared
                && string.IsNullOrEmpty(owner.goldenTraitDefinitionId)
                && string.IsNullOrEmpty(owner.goldenOutcomeFingerprint)
                && owner.goldenOutcome == default
                && owner.goldenPrimaryMultiplier == 0f
                && owner.goldenSecondaryMultiplier == 0f
                && owner.goldenRollHash == 0UL
                && !owner.goldenCommitted
                && !owner.goldenAcknowledged
                && !owner.completionEventPublished
                && string.IsNullOrEmpty(owner.completionPublicationKey)
                && string.IsNullOrEmpty(owner.completionDeliveryId)
                && string.IsNullOrEmpty(owner.completionDeliveryFingerprint)
                && owner.completionAbsoluteDay == 0
                && string.IsNullOrEmpty(owner.harvestItemId)
                && owner.harvestQuantity == 0
                && string.IsNullOrEmpty(owner.seedItemId)
                && owner.seedQuantity == 0
                && IsSerializedEmptySeedLot(owner.returnedSeedLot)
                && owner.maximumHarvestQuantity == 0
                && owner.maximumSeedQuantity == 0
                && string.IsNullOrEmpty(
                    owner.seasonalDamageSourceEventInstanceId)
                && string.IsNullOrEmpty(
                    owner.seasonalDamageSourceDefinitionId)
                && owner.seasonalPrimaryBatchLossPercent == 0
                && owner.primaryQuantityBeforeSeasonalLoss == 0
                && owner.seasonalPrimaryLossQuantity == 0
                && owner.harvestCapability is { IsEmpty: true }
                && owner.seedCapability is { IsEmpty: true }
                && owner.outputPublication is { IsEmpty: true };
            if (!empty)
                throw new InvalidOperationException(
                    "Empty crop harvest owner contains frozen provenance: "
                    + DescribeEmptyHarvestProvenance(owner));
            return;
        }

        CropSeasonalYieldDamageSaveData seasonalDamage =
            saved.seasonalYieldDamage;
        bool seasonalLossValid;
        if (seasonalDamage.IsEmpty)
        {
            seasonalLossValid = string.IsNullOrEmpty(
                    owner.seasonalDamageSourceEventInstanceId)
                && string.IsNullOrEmpty(
                    owner.seasonalDamageSourceDefinitionId)
                && owner.seasonalPrimaryBatchLossPercent == 0
                && owner.primaryQuantityBeforeSeasonalLoss == 0
                && owner.seasonalPrimaryLossQuantity == 0;
        }
        else
        {
            seasonalLossValid = string.Equals(
                    owner.seasonalDamageSourceEventInstanceId,
                    seasonalDamage.sourceEventInstanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    owner.seasonalDamageSourceDefinitionId,
                    seasonalDamage.sourceDefinitionId,
                    StringComparison.Ordinal)
                && owner.seasonalPrimaryBatchLossPercent
                    == seasonalDamage.primaryBatchLossPercent
                && owner.primaryQuantityBeforeSeasonalLoss > 0
                && owner.seasonalPrimaryLossQuantity
                    == CropHarvestOutputRules.ResolvePrimaryBatchLoss(
                        owner.primaryQuantityBeforeSeasonalLoss,
                        owner.seasonalPrimaryBatchLossPercent)
                && owner.harvestQuantity
                    == CropHarvestOutputRules.ApplyPrimaryBatchLoss(
                        owner.primaryQuantityBeforeSeasonalLoss,
                        owner.seasonalPrimaryBatchLossPercent);
        }

        if (!Enum.IsDefined(typeof(CropHarvestOutputPhase), owner.phase)
            || saved.phase != CropPlotPhase.Harvesting
            || saved.harvestWork + 0.001f < crop.HarvestWork
            || owner.operationSequence != saved.nextHarvestOperationSequence
            || !string.Equals(
                owner.operationId,
                FormatHarvestOperationId(plotId, owner.operationSequence),
                StringComparison.Ordinal)
            || !string.Equals(owner.cropId, crop.CropId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(owner.ecologyOutcomeFingerprint)
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                owner.ecologyBeforeFingerprint)
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                owner.ecologyAfterFingerprint)
            || owner.harvestQuantity <= 0
            || owner.seedQuantity <= 0
            || owner.returnedSeedLot == null
            || !string.Equals(
                owner.returnedSeedLot.cropId,
                crop.CropId,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.harvestItemId,
                crop.HarvestItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.seedItemId,
                crop.SeedItemId,
                StringComparison.Ordinal)
            || owner.maximumHarvestQuantity !=
                CropHarvestOutputMaximumAuthority.ResolveMaximumHarvestQuantity(
                    crop,
                    owner.indoor,
                    performanceMaximum)
            || owner.maximumSeedQuantity !=
                CropHarvestOutputMaximumAuthority
                    .ResolveMaximumReturnedSeedQuantity(effectBounds)
            || owner.harvestQuantity > owner.maximumHarvestQuantity
            || owner.seedQuantity > owner.maximumSeedQuantity
            || !seasonalLossValid
            || owner.ecologyAcknowledged && !owner.ecologyCommitted
            || owner.goldenCommitted && !owner.goldenPrepared
            || owner.goldenAcknowledged && !owner.goldenCommitted
            || owner.goldenCommitted && !owner.ecologyCommitted
            || owner.ecologyAcknowledged && !owner.completionEventPublished
            || owner.goldenAcknowledged
                && (!owner.ecologyAcknowledged
                    || !owner.completionEventPublished)
            || owner.completionEventPublished && !owner.cropOutcomeAcknowledged
            || owner.cropOutcomeAcknowledged
                && (owner.phase != CropHarvestOutputPhase
                        .OutputRestoredAwaitingFinalization
                    || !owner.ecologyCommitted
                    || !owner.outputPublication.outputAcknowledged
                    || owner.goldenPrepared && !owner.goldenCommitted)
            || owner.completionAbsoluteDay < 0
            || !IsCanonicalToken(owner.harvesterId)
            || !IsCanonicalToken(owner.outcomeId)
            || !Enum.IsDefined(typeof(ExtremeRiskOutcome), owner.goldenOutcome)
            || !IsFinitePositive(owner.goldenPrimaryMultiplier)
            || !IsFinitePositive(owner.goldenSecondaryMultiplier))
        {
            throw new InvalidOperationException(
                "Crop harvest frozen owner contradicts plot state or authored maximums.");
        }

        ValidateHarvestTerminalPublication(saved, plotId);
        if (string.IsNullOrEmpty(owner.harvesterId))
        {
            if (!string.IsNullOrEmpty(owner.completionPublicationKey)
                || !string.IsNullOrEmpty(owner.completionDeliveryId)
                || !string.IsNullOrEmpty(owner.completionDeliveryFingerprint))
                throw new InvalidOperationException(
                    "Workerless crop harvest contains completion delivery provenance.");
        }
        else
        {
            _ = new CropHarvestCompletedFact(plotId, owner, saved.cycleExecutionReceipt);
            ValidateCropTerminalPublicationFields(owner.completionPublicationKey, owner.completionDeliveryId,
                owner.completionDeliveryFingerprint, owner.completionEventPublished);
            if (!string.IsNullOrEmpty(owner.completionPublicationKey) && !owner.cropOutcomeAcknowledged)
                throw new InvalidOperationException("Crop completion publication precedes its terminal acknowledgement.");
        }

        if (owner.goldenPrepared)
        {
            if (string.IsNullOrWhiteSpace(owner.goldenTraitDefinitionId)
                || string.IsNullOrWhiteSpace(owner.goldenOutcomeFingerprint)
                || !string.Equals(
                    owner.harvesterId,
                    saved.goldenHarvestHarvesterId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.outcomeId,
                    owner.goldenOutcome.ToString().ToLowerInvariant(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Prepared Golden Harvest provenance is incomplete.");
            }
        }
        else if (!string.IsNullOrEmpty(owner.goldenTraitDefinitionId)
            || !string.IsNullOrEmpty(owner.goldenOutcomeFingerprint)
            || owner.goldenOutcome != ExtremeRiskOutcome.Normal
            || owner.goldenPrimaryMultiplier != 1f
            || owner.goldenSecondaryMultiplier != 1f
            || owner.goldenRollHash != 0UL
            || owner.goldenCommitted
            || owner.goldenAcknowledged)
        {
            throw new InvalidOperationException(
                "Normal crop harvest contains Golden Harvest provenance.");
        }

        ProductionOutputCapabilityDescriptor harvest = outputCapabilities
            .CaptureDeclaredDescriptor(
                CropHarvestOutputMaximumAuthority.HarvestOutputLineId(
                    crop.CropId),
                crop.HarvestItemId,
                ProductionOutputCapabilityIds.StandardDefinition);
        ProductionOutputCapabilityDescriptor seed = outputCapabilities
            .CaptureDeclaredDescriptor(
                CropHarvestOutputMaximumAuthority.SeedOutputLineId(crop.CropId),
                crop.SeedItemId,
                ProductionOutputCapabilityIds.CropHarvestSeedLot);
        string expectedOutcomeFingerprint =
            CaptureHarvestOutcomeFingerprint(plotId, owner);
        if (!CapabilityMatches(owner.harvestCapability, harvest)
            || !CapabilityMatches(owner.seedCapability, seed)
            || owner.phase != CropHarvestOutputPhase.Frozen
                && !string.Equals(
                    expectedOutcomeFingerprint,
                    owner.outputPublication.outcomeFingerprint,
                    StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop harvest output capability or outcome fingerprint drifted.");
        }

        if (owner.phase == CropHarvestOutputPhase.Frozen)
        {
            if (owner.ecologyCommitted
                || owner.ecologyAcknowledged
                || owner.goldenCommitted
                || owner.goldenAcknowledged
                || owner.completionEventPublished)
                throw new InvalidOperationException(
                    "Frozen crop harvest contains committed provenance.");
            if (owner.outputPublication.IsEmpty)
                return;
            if (!ProductionDomainOutputPublicationService
                    .TryValidateRestorableOwner(
                        owner.outputPublication,
                        out bool committed,
                        out string restorableFailure)
                || committed
                || !string.Equals(
                    expectedOutcomeFingerprint,
                    owner.outputPublication.outcomeFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.outputPublication.batchCommitId,
                    HarvestOutputBatchCommitPrefix + owner.operationId,
                    StringComparison.Ordinal)
                || !string.IsNullOrEmpty(
                        owner.outputPublication.publicationOperationId)
                    && !string.Equals(
                        owner.outputPublication.publicationOperationId,
                        HarvestOutputPublicationOperationPrefix
                            + owner.operationId + ":"
                            + owner.outputPublication.publicationAttempt.ToString(
                                "D4",
                                System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.Ordinal)
                || !string.Equals(
                    owner.outputPublication.ownerFacilityId,
                    plotId.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.outputPublication.ownerDomain,
                    ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.outputPublication.destinationId,
                    ProductionBillRuntime.OutputDestinationPrefix + plotId.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.outputPublication.ownerOperationId,
                    owner.outputPublication.destinationId,
                    StringComparison.Ordinal)
                || owner.outputPublication.acknowledgementDisposition !=
                    ProductionDomainOutputAcknowledgementDisposition
                        .ReleaseLooseOrDestination)
                throw new InvalidOperationException(
                    "Frozen crop harvest publication owner is invalid: "
                    + restorableFailure);
            return;
        }

        if (!ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                owner.outputPublication,
                out string publicationFailure)
            || !string.Equals(
                owner.outputPublication.batchCommitId,
                HarvestOutputBatchCommitPrefix + owner.operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outputPublication.publicationOperationId,
                HarvestOutputPublicationOperationPrefix
                    + owner.operationId + ":"
                    + owner.outputPublication.publicationAttempt.ToString(
                        "D4",
                        System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outputPublication.ownerFacilityId,
                plotId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outputPublication.ownerDomain,
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outputPublication.destinationId,
                ProductionBillRuntime.OutputDestinationPrefix + plotId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outputPublication.ownerOperationId,
                owner.outputPublication.destinationId,
                StringComparison.Ordinal)
            || owner.outputPublication.acknowledgementDisposition !=
                ProductionDomainOutputAcknowledgementDisposition
                    .ReleaseLooseOrDestination
            || owner.phase == CropHarvestOutputPhase.OutputCommitted
                && owner.outputPublication.outputAcknowledged
            || owner.phase == CropHarvestOutputPhase
                    .OutputRestoredAwaitingFinalization
                && !owner.outputPublication.outputAcknowledged)
        {
            throw new InvalidOperationException(
                "Crop harvest committed publication is invalid: "
                + publicationFailure);
        }
        ValidatePublishedHarvestVector(owner);
    }

    private static void ValidatePublishedHarvestVector(
        CropHarvestOutputSaveData owner)
    {
        ProductionDomainPublishedStackSaveData[] stacks =
            owner.outputPublication.stacks?.ToArray()
            ?? Array.Empty<ProductionDomainPublishedStackSaveData>();
        if (stacks.Length != 2
            || stacks.Select(value => value.outputLineId)
                    .Distinct(StringComparer.Ordinal).Count() != 2)
            throw new InvalidOperationException(
                "Crop harvest publication is not an exact two-line vector.");
        ProductionDomainPublishedStackSaveData harvest = stacks.SingleOrDefault(
            value => string.Equals(
                value.outputLineId,
                owner.harvestCapability.outputLineId,
                StringComparison.Ordinal));
        ProductionDomainPublishedStackSaveData seed = stacks.SingleOrDefault(
            value => string.Equals(
                value.outputLineId,
                owner.seedCapability.outputLineId,
                StringComparison.Ordinal));
        if (harvest == null
            || seed == null
            || !string.Equals(
                harvest.itemId,
                owner.harvestItemId,
                StringComparison.Ordinal)
            || harvest.quantity != owner.harvestQuantity
            || !string.Equals(
                seed.itemId,
                owner.seedItemId,
                StringComparison.Ordinal)
            || seed.quantity != owner.seedQuantity)
            throw new InvalidOperationException(
                "Crop harvest publication vector drifted from its frozen outcome.");
    }

    private static string DescribeEmptyHarvestProvenance(
        CropHarvestOutputSaveData owner)
    {
        if (owner == null)
            return "owner=null";

        List<string> fields = new();
        void Add(bool present, string field)
        {
            if (present)
                fields.Add(field);
        }

        Add(owner.operationSequence != 0, nameof(owner.operationSequence));
        Add(!string.IsNullOrEmpty(owner.operationId), nameof(owner.operationId));
        Add(!string.IsNullOrEmpty(owner.cropId), nameof(owner.cropId));
        Add(owner.indoor, nameof(owner.indoor));
        Add(!string.IsNullOrEmpty(owner.harvesterId), nameof(owner.harvesterId));
        Add(!string.IsNullOrEmpty(owner.outcomeId), nameof(owner.outcomeId));
        Add(!string.IsNullOrEmpty(owner.ecologyOutcomeFingerprint),
            nameof(owner.ecologyOutcomeFingerprint));
        Add(!string.IsNullOrEmpty(owner.ecologyBeforeFingerprint),
            nameof(owner.ecologyBeforeFingerprint));
        Add(!string.IsNullOrEmpty(owner.ecologyAfterFingerprint),
            nameof(owner.ecologyAfterFingerprint));
        Add(owner.ecologyCommitted, nameof(owner.ecologyCommitted));
        Add(owner.ecologyAcknowledged, nameof(owner.ecologyAcknowledged));
        Add(owner.goldenPrepared, nameof(owner.goldenPrepared));
        Add(!string.IsNullOrEmpty(owner.goldenTraitDefinitionId),
            nameof(owner.goldenTraitDefinitionId));
        Add(!string.IsNullOrEmpty(owner.goldenOutcomeFingerprint),
            nameof(owner.goldenOutcomeFingerprint));
        Add(owner.goldenOutcome != default, nameof(owner.goldenOutcome));
        Add(owner.goldenPrimaryMultiplier != 0f,
            nameof(owner.goldenPrimaryMultiplier));
        Add(owner.goldenSecondaryMultiplier != 0f,
            nameof(owner.goldenSecondaryMultiplier));
        Add(owner.goldenRollHash != 0UL, nameof(owner.goldenRollHash));
        Add(owner.goldenCommitted, nameof(owner.goldenCommitted));
        Add(owner.goldenAcknowledged, nameof(owner.goldenAcknowledged));
        Add(owner.cropOutcomeAcknowledged, nameof(owner.cropOutcomeAcknowledged));
        Add(owner.terminalReceipt == null || !owner.terminalReceipt.IsEmpty, nameof(owner.terminalReceipt));
        Add(!string.IsNullOrEmpty(owner.terminalPlotDisplayName), nameof(owner.terminalPlotDisplayName));
        Add(!string.IsNullOrEmpty(owner.terminalCropDisplayName), nameof(owner.terminalCropDisplayName));
        Add(!string.IsNullOrEmpty(owner.terminalPublicationKey), nameof(owner.terminalPublicationKey));
        Add(!string.IsNullOrEmpty(owner.terminalPublicationId), nameof(owner.terminalPublicationId));
        Add(!string.IsNullOrEmpty(owner.terminalPublicationFingerprint), nameof(owner.terminalPublicationFingerprint));
        Add(owner.completionEventPublished,
            nameof(owner.completionEventPublished));
        Add(!string.IsNullOrEmpty(owner.completionDeliveryId),
            nameof(owner.completionDeliveryId));
        Add(!string.IsNullOrEmpty(owner.completionPublicationKey), nameof(owner.completionPublicationKey));
        Add(!string.IsNullOrEmpty(owner.completionDeliveryFingerprint),
            nameof(owner.completionDeliveryFingerprint));
        Add(owner.completionAbsoluteDay != 0,
            nameof(owner.completionAbsoluteDay));
        Add(!string.IsNullOrEmpty(owner.harvestItemId),
            nameof(owner.harvestItemId));
        Add(owner.harvestQuantity != 0, nameof(owner.harvestQuantity));
        Add(!string.IsNullOrEmpty(owner.seedItemId), nameof(owner.seedItemId));
        Add(owner.seedQuantity != 0, nameof(owner.seedQuantity));
        Add(!IsSerializedEmptySeedLot(owner.returnedSeedLot),
            nameof(owner.returnedSeedLot));
        Add(owner.maximumHarvestQuantity != 0,
            nameof(owner.maximumHarvestQuantity));
        Add(owner.maximumSeedQuantity != 0,
            nameof(owner.maximumSeedQuantity));
        Add(!string.IsNullOrEmpty(owner.seasonalDamageSourceEventInstanceId),
            nameof(owner.seasonalDamageSourceEventInstanceId));
        Add(!string.IsNullOrEmpty(owner.seasonalDamageSourceDefinitionId),
            nameof(owner.seasonalDamageSourceDefinitionId));
        Add(owner.seasonalPrimaryBatchLossPercent != 0,
            nameof(owner.seasonalPrimaryBatchLossPercent));
        Add(owner.primaryQuantityBeforeSeasonalLoss != 0,
            nameof(owner.primaryQuantityBeforeSeasonalLoss));
        Add(owner.seasonalPrimaryLossQuantity != 0,
            nameof(owner.seasonalPrimaryLossQuantity));
        Add(owner.harvestCapability is not { IsEmpty: true },
            nameof(owner.harvestCapability));
        Add(owner.seedCapability is not { IsEmpty: true },
            nameof(owner.seedCapability));
        Add(owner.outputPublication == null,
            nameof(owner.outputPublication) + "=null");
        if (owner.outputPublication is { IsEmpty: false })
        {
            fields.Add(
                nameof(owner.outputPublication)
                + "="
                + JsonUtility.ToJson(owner.outputPublication));
        }
        return fields.Count == 0 ? "unknown" : string.Join(",", fields);
    }

    private static bool IsSerializedEmptySeedLot(SeedLotState value) =>
        value == null
        || string.IsNullOrEmpty(value.cropId)
        && string.IsNullOrEmpty(value.cultivarGenomeId)
        && value.generation == 0
        && value.pathogenLoad == 0f;

    private static bool IsCanonicalToken(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static void ValidateSeasonalYieldDamage(
        CropSeasonalYieldDamageSaveData damage,
        string cropId,
        CropCycleExecutionReceiptSaveData cycleReceipt,
        ISet<string> handledSeasonalStarts)
    {
        if (damage == null)
            throw new InvalidOperationException(
                "Crop seasonal yield-damage provenance is missing.");
        if (damage.IsEmpty)
            return;
        bool parsedSource = TryParseSeasonalEventInstanceId(
            damage.sourceEventInstanceId,
            out int parsedDay,
            out string parsedDefinitionId);
        if (!parsedSource
            || !IsCanonicalToken(damage.sourceDefinitionId)
            || damage.sourceStartedAbsoluteDay < 1
            || parsedDay != damage.sourceStartedAbsoluteDay
            || !string.Equals(
                parsedDefinitionId,
                damage.sourceDefinitionId,
                StringComparison.Ordinal)
            || !IsCanonicalToken(damage.cropId)
            || !string.Equals(damage.cropId, cropId, StringComparison.Ordinal)
            || damage.sowOperationSequence < 0
            || damage.primaryBatchLossPercent is < 1
                or > CropHarvestOutputRules
                    .MaximumSeasonalPrimaryBatchLossPercent
            || cycleReceipt == null
            || cycleReceipt.IsEmpty
            || cycleReceipt.status != CropCycleExecutionReceiptStatus.Active
            || cycleReceipt.sowOperationSequence != damage.sowOperationSequence
            || !string.Equals(
                cycleReceipt.cropId,
                damage.cropId,
                StringComparison.Ordinal)
            || handledSeasonalStarts == null
            || !handledSeasonalStarts.Contains(damage.sourceEventInstanceId))
        {
            throw new InvalidOperationException(
                "Crop seasonal yield-damage provenance contradicts its live cycle or handled start.");
        }
    }

    private static bool TryParseSeasonalEventInstanceId(
        string value,
        out int startedAbsoluteDay,
        out string definitionId)
    {
        startedAbsoluteDay = 0;
        definitionId = string.Empty;
        if (!IsCanonicalToken(value)
            || !value.StartsWith("event:", StringComparison.Ordinal))
            return false;
        int dayEnd = value.IndexOf(':', "event:".Length);
        int rollStart = value.LastIndexOf(':');
        if (dayEnd <= "event:".Length
            || rollStart <= dayEnd + 1
            || rollStart + 9 != value.Length
            || !int.TryParse(
                value.Substring("event:".Length, dayEnd - "event:".Length),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out startedAbsoluteDay)
            || startedAbsoluteDay < 1)
        {
            return false;
        }
        definitionId = value.Substring(
            dayEnd + 1,
            rollStart - dayEnd - 1);
        string roll = value.Substring(rollStart + 1);
        return IsCanonicalToken(definitionId)
            && roll.All(character => character is >= '0' and <= '9'
                or >= 'A' and <= 'F');
    }

    private static bool IsFinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    private static bool CapabilityMatches(
        ProductionOutputCapabilitySaveData saved,
        ProductionOutputCapabilityDescriptor expected) =>
        saved != null
        && string.Equals(saved.outputLineId, expected.OutputLineId,
            StringComparison.Ordinal)
        && string.Equals(saved.itemId, expected.ItemId,
            StringComparison.Ordinal)
        && string.Equals(saved.capabilityId, expected.CapabilityId,
            StringComparison.Ordinal)
        && saved.capabilityVersion == expected.CapabilityVersion
        && string.Equals(saved.componentCodecId, expected.ComponentCodecId,
            StringComparison.Ordinal)
        && saved.componentCodecVersion == expected.ComponentCodecVersion
        && string.Equals(saved.fingerprint, expected.Fingerprint,
            StringComparison.Ordinal);

    private static void ValidateCropSelectionOutcome(
        CropPlotSaveData saved,
        BuildingInstanceId plotId)
    {
        CropSelectionCommandSaveData owner = saved.cropSelectionOutcome;
        if (owner.IsEmpty)
            return;
        _ = new CropSelectionChangedFact(plotId, owner);
        ValidateCropCommandPublicationKey(owner.publicationKey);
        string expectedOperationId = CropSelectionOperationPrefix
            + plotId.Value + ":"
            + owner.operationSequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
        bool ledgerExact = owner.phase == CropCommandOutcomePhase.LedgerCommitted
            ? IsCanonicalToken(owner.gameplayOutcomeId)
                && IsCropCommandFingerprint(
                    owner.gameplayOutcomeDigest)
            : string.IsNullOrEmpty(owner.gameplayOutcomeId)
                && string.IsNullOrEmpty(owner.gameplayOutcomeDigest);
        if (!Enum.IsDefined(typeof(CropCommandOutcomePhase), owner.phase)
            || owner.phase == CropCommandOutcomePhase.None
            || owner.operationSequence
                != saved.nextCropSelectionOperationSequence
            || !string.Equals(
                owner.operationId,
                expectedOperationId,
                StringComparison.Ordinal)
            || !IsCanonicalToken(owner.cropAfter)
            || !string.Equals(owner.cropAfter, saved.cropId, StringComparison.Ordinal)
            || !float.IsFinite(owner.waterBefore)
            || !float.IsFinite(owner.waterAfter)
            || owner.waterBefore < 0f
            || owner.waterAfter != 0f
            || owner.completedAbsoluteDay < 0
            || !ledgerExact)
            throw new InvalidOperationException(
                "Crop selection command outcome is invalid.");
    }

    private static void ValidateGoldenHarvestScheduleOutcome(
        CropPlotSaveData saved,
        BuildingInstanceId plotId)
    {
        CropGoldenHarvestScheduleSaveData owner =
            saved.goldenHarvestScheduleOutcome;
        if (owner.IsEmpty)
            return;
        _ = new GoldenHarvestScheduledFact(plotId, owner);
        ValidateCropCommandPublicationKey(owner.publicationKey);
        string expectedOperationId = GoldenHarvestScheduleOperationPrefix
            + plotId.Value + ":"
            + owner.operationSequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
        bool ledgerExact = owner.phase == CropCommandOutcomePhase.LedgerCommitted
            ? IsCanonicalToken(owner.gameplayOutcomeId)
                && IsCropCommandFingerprint(
                    owner.gameplayOutcomeDigest)
            : string.IsNullOrEmpty(owner.gameplayOutcomeId)
                && string.IsNullOrEmpty(owner.gameplayOutcomeDigest);
        if (!Enum.IsDefined(typeof(CropCommandOutcomePhase), owner.phase)
            || owner.phase == CropCommandOutcomePhase.None
            || owner.operationSequence != owner.attemptBefore
            || owner.attemptAfter != checked(owner.attemptBefore + 1)
            || owner.attemptAfter != saved.goldenHarvestAttemptSequence
            || !string.Equals(
                owner.operationId,
                expectedOperationId,
                StringComparison.Ordinal)
            || !string.Equals(owner.cropId, saved.cropId, StringComparison.Ordinal)
            || !IsCanonicalToken(owner.harvesterId)
            || string.IsNullOrWhiteSpace(owner.harvesterDisplayName)
            || !string.IsNullOrEmpty(owner.harvesterBefore)
            || !string.Equals(
                owner.harvesterAfter,
                saved.goldenHarvestHarvesterId,
                StringComparison.Ordinal)
            || owner.completedAbsoluteDay < 0
            || !ledgerExact)
            throw new InvalidOperationException(
                "Golden harvest schedule outcome is invalid.");
    }

    private static void ValidateCropTerminalPublicationFields(string key, string id, string fingerprint, bool acknowledged = false)
    {
        if (key == null || id == null || fingerprint == null)
            throw new InvalidOperationException("Crop terminal publication fields must be explicit.");
        if (key.Length == 0)
        {
            if (id.Length != 0 || fingerprint.Length != 0 || acknowledged)
                throw new InvalidOperationException("Crop terminal response is missing its publication key.");
            return;
        }
        ValidateCropCommandPublicationKey(key);
        bool hasIdentity = id.Length != 0;
        if (hasIdentity != (fingerprint.Length != 0)
            || hasIdentity && (!IsCanonicalToken(id) || !IsCropCommandFingerprint(fingerprint))
            || acknowledged && !hasIdentity)
            throw new InvalidOperationException("Crop terminal publication acknowledgement is incomplete.");
    }

    private static void ValidateHarvestTerminalPublication(CropPlotSaveData saved, BuildingInstanceId plotId)
    {
        CropHarvestOutputSaveData owner = saved.pendingHarvest;
        if (owner.terminalReceipt == null)
            throw new InvalidOperationException("Crop harvest terminal snapshot must be explicit.");
        ValidateCropTerminalPublicationFields(owner.terminalPublicationKey, owner.terminalPublicationId,
            owner.terminalPublicationFingerprint, owner.cropOutcomeAcknowledged);
        if (owner.terminalReceipt.IsEmpty)
        {
            if (!string.IsNullOrEmpty(owner.terminalPlotDisplayName)
                || !string.IsNullOrEmpty(owner.terminalCropDisplayName)
                || !string.IsNullOrEmpty(owner.terminalPublicationKey) || owner.cropOutcomeAcknowledged)
                throw new InvalidOperationException("Empty harvest terminal snapshot contains publication provenance.");
            return;
        }
        if (owner.phase != CropHarvestOutputPhase.OutputRestoredAwaitingFinalization
            || !owner.outputPublication.outputAcknowledged || !owner.ecologyCommitted
            || owner.goldenPrepared && !owner.goldenCommitted)
            throw new InvalidOperationException("Harvest terminal was frozen before its physical effects completed.");
        _ = new CropCycleTerminalFact(plotId, owner.terminalReceipt,
            owner.terminalPlotDisplayName, owner.terminalCropDisplayName,
            owner.completionAbsoluteDay, checked((long)owner.operationSequence + 1L));
        CropCycleExecutionReceiptSaveData expected = CropPlanExecutionReceiptAuthority.Complete(
            saved.cycleExecutionReceipt, owner, saved.currentWater);
        if (owner.terminalReceipt.sourceDigest != expected.sourceDigest)
            throw new InvalidOperationException("Harvest terminal snapshot drifted from its authoritative execution.");
    }

    private static void ValidateCropCommandPublicationKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 512
            || !string.Equals(key, key.Trim(), StringComparison.Ordinal)
            || key.Any(char.IsControl))
            throw new InvalidOperationException("Crop command publication key is invalid.");
    }

    private static bool IsCropCommandFingerprint(string value) =>
        value != null && value.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private void ValidatePendingWaterRefill(
        CropPlotSaveData saved,
        CropDefinitionSO crop,
        BuildingInstanceId plotId)
    {
        CropWaterRefillSaveData owner = saved.waterRefill;
        bool hasPublication = !string.IsNullOrEmpty(owner.publicationKey);
        if (hasPublication)
        {
            ValidateCropCommandPublicationKey(owner.publicationKey);
            _ = new CropWaterRefillFact(plotId, owner);
        }
        else if (!string.IsNullOrEmpty(owner.plotDisplayName)
            || !string.IsNullOrEmpty(owner.itemDisplayName)
            || owner.locationX != 0 || owner.locationY != 0
            || owner.phase is CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished or CropWaterRefillPhase.LedgerCommitted)
            throw new InvalidOperationException("Crop water-refill publication snapshot is missing its key.");
        if (owner.phase == CropWaterRefillPhase.None)
        {
            bool empty = !hasPublication && owner.operationSequence == 0
                && owner.outcomeSchemaVersion == 0
                && string.IsNullOrEmpty(owner.operationId)
                && string.IsNullOrEmpty(owner.reasonCode)
                && string.IsNullOrEmpty(owner.destinationId)
                && string.IsNullOrEmpty(owner.itemId)
                && owner.quantity == 0
                && owner.requiredWork == 0f
                && owner.completedWork == 0f
                && string.IsNullOrEmpty(owner.commitId)
                && owner.inputQuantity == 0
                && owner.inputMassGrams == 0L
                && string.IsNullOrEmpty(owner.requestFingerprint)
                && string.IsNullOrEmpty(owner.physicalRequestFingerprint)
                && owner.physicalOutcomeOwnerRevision == 0L
                && (owner.sourceStackIds?.Count ?? 0) == 0
                && string.IsNullOrEmpty(owner.responsibleWorkerId)
                && string.IsNullOrEmpty(owner.responsibleWorkerDisplayName)
                && (owner.workContributions?.Count ?? 0) == 0
                && owner.waterBefore == 0f
                && owner.waterAfter == 0f
                && owner.completedAbsoluteDay == 0
                && string.IsNullOrEmpty(owner.gameplayOutcomeId)
                && string.IsNullOrEmpty(owner.gameplayOutcomeDigest)
                && string.IsNullOrEmpty(owner.failureReason);
            if (!empty)
                throw new InvalidOperationException(
                    "Empty crop water-refill owner contains provenance.");
            return;
        }

        if (!Enum.IsDefined(typeof(CropWaterRefillPhase), owner.phase)
            || owner.outcomeSchemaVersion
                != CropWaterRefillSaveData.CurrentOutcomeSchemaVersion
            || crop.DailyWater <= 0f
            || owner.operationSequence
                != saved.nextWaterRefillOperationSequence
            || !string.Equals(
                owner.operationId,
                FormatWaterRefillOperationId(
                    plotId,
                    owner.operationSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                owner.destinationId,
                CropPlotInputOwnerAuthority.BuildWaterRefillDestinationId(
                    plotId.Value,
                    owner.operationSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                owner.reasonCode,
                "crop-water-refill-input",
                StringComparison.Ordinal)
            || !string.Equals(
                owner.itemId,
                CropCycleInputRequirementAuthority.CleanWaterItemId,
                StringComparison.Ordinal)
            || owner.quantity != CropWaterRules.RefillQuantity
            || !float.IsFinite(owner.requiredWork)
            || owner.requiredWork < 0.1f
            || !float.IsFinite(owner.completedWork)
            || owner.completedWork < 0f
            || owner.completedWork > owner.requiredWork
            || !string.Equals(
                owner.requestFingerprint,
                CaptureWaterRefillRequestFingerprint(
                    plotId,
                    owner.operationSequence,
                    owner.destinationId,
                    owner.requiredWork),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop water-refill intent contradicts plot identity or current policy.");
        }

        bool committed = owner.phase is CropWaterRefillPhase.InputCommitted
            or CropWaterRefillPhase.OutcomePublished
            or CropWaterRefillPhase.LedgerCommitted;
        long expectedMass = items.GetDefinitionQuantityMassGrams(
            owner.itemId,
            owner.quantity);
        bool receiptExact = !string.IsNullOrWhiteSpace(owner.commitId)
            && string.Equals(
                owner.commitId,
                owner.commitId.Trim(),
                StringComparison.Ordinal)
            && owner.inputQuantity == owner.quantity
            && owner.inputMassGrams == expectedMass
            && IsCanonicalToken(owner.physicalRequestFingerprint)
            && owner.physicalOutcomeOwnerRevision > 0L
            && owner.sourceStackIds != null
            && owner.sourceStackIds.Count > 0
            && owner.sourceStackIds.All(IsCanonicalToken)
            && owner.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == owner.sourceStackIds.Count
            && owner.sourceStackIds.SequenceEqual(
                owner.sourceStackIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal);
        bool receiptEmpty = string.IsNullOrEmpty(owner.commitId)
            && owner.inputQuantity == 0
            && owner.inputMassGrams == 0L
            && string.IsNullOrEmpty(owner.physicalRequestFingerprint)
            && owner.physicalOutcomeOwnerRevision == 0L
            && (owner.sourceStackIds?.Count ?? 0) == 0;
        bool workerExact = owner.phase is CropWaterRefillPhase.Working
                or CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted
            ? IsCanonicalToken(owner.responsibleWorkerId)
                && !string.IsNullOrWhiteSpace(
                    owner.responsibleWorkerDisplayName)
                && owner.workContributions != null
                && owner.workContributions.Count > 0
            : string.IsNullOrEmpty(owner.responsibleWorkerId)
                && string.IsNullOrEmpty(owner.responsibleWorkerDisplayName)
                && (owner.workContributions?.Count ?? 0) == 0;
        bool outcomeExact = owner.phase == CropWaterRefillPhase.LedgerCommitted
            ? IsCanonicalToken(owner.gameplayOutcomeId)
                && IsCropCommandFingerprint(owner.gameplayOutcomeDigest)
            : string.IsNullOrEmpty(owner.gameplayOutcomeId)
                && string.IsNullOrEmpty(owner.gameplayOutcomeDigest);
        if (committed != receiptExact
            || !committed && !receiptEmpty
            || committed
                && owner.completedWork + 0.001f < owner.requiredWork
            || !workerExact
            || !outcomeExact
            || committed && (!float.IsFinite(owner.waterBefore)
                || owner.waterBefore < 0f
                || owner.completedAbsoluteDay < 0)
            || owner.phase == CropWaterRefillPhase.InputCommitted
                && saved.currentWater + owner.quantity
                    > saved.waterCapacity + 0.001f
            || (owner.phase is CropWaterRefillPhase.OutcomePublished
                    or CropWaterRefillPhase.LedgerCommitted)
                && (Math.Abs(saved.currentWater - owner.waterAfter) > 0.0001f
                    || Math.Abs(
                        owner.waterBefore + owner.quantity - owner.waterAfter)
                        > 0.0001f)
            || owner.phase == CropWaterRefillPhase.WaitingForDelivery
                && owner.completedWork != 0f)
        {
            throw new InvalidOperationException(
                "Crop water-refill WIP receipt contradicts its phase.");
        }
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
        MarkChanged(replan: false);
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

    private void ValidatePendingTreatment(
        CropPlotSaveData saved,
        BuildingInstanceId plotId)
    {
        CropTreatmentOrderSaveData owner = saved.treatment;
        bool hasPublication = !string.IsNullOrEmpty(owner.publicationKey);
        if (hasPublication)
        {
            ValidateCropCommandPublicationKey(owner.publicationKey);
            if (owner.phase != CropTreatmentOrderPhase.PlotDestroyedLossPending)
                _ = new CropTreatmentFact(plotId, owner);
            else if (string.IsNullOrWhiteSpace(owner.plotDisplayName)
                || string.IsNullOrWhiteSpace(owner.itemDisplayName))
                throw new InvalidOperationException("Destroyed crop treatment lost its frozen presentation.");
        }
        else if (!string.IsNullOrEmpty(owner.plotDisplayName) || !string.IsNullOrEmpty(owner.itemDisplayName)
            || owner.locationX != 0 || owner.locationY != 0
            || !string.IsNullOrEmpty(owner.ecologyBeforeFingerprint)
            || owner.pestPressureBefore != 0f || owner.diseasePressureBefore != 0f
            || owner.cooldownBeforeDay != 0 || owner.completedAbsoluteDay != 0
            || owner.phase is CropTreatmentOrderPhase.InputCommitted or CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted or CropTreatmentOrderPhase.PlotDestroyedLossPending)
            throw new InvalidOperationException("Crop treatment publication snapshot is missing its key.");
        if (owner.phase == CropTreatmentOrderPhase.None)
        {
            bool empty = !hasPublication && owner.outcomeSchemaVersion == 0
                && owner.operationSequence == 0
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
                && string.IsNullOrEmpty(owner.responsibleWorkerId)
                && string.IsNullOrEmpty(owner.responsibleWorkerDisplayName)
                && (owner.workContributions?.Count ?? 0) == 0
                && owner.pestPressureBefore == 0f
                && owner.pestPressureAfter == 0f
                && owner.diseasePressureBefore == 0f
                && owner.diseasePressureAfter == 0f
                && owner.cooldownBeforeDay == 0
                && owner.cooldownAfterDay == 0
                && owner.completedAbsoluteDay == 0
                && string.IsNullOrEmpty(owner.gameplayOutcomeId)
                && string.IsNullOrEmpty(owner.gameplayOutcomeDigest)
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
            || owner.outcomeSchemaVersion != CropTreatmentOrderSaveData.CurrentOutcomeSchemaVersion
            || owner.operationSequence != saved.nextTreatmentOperationSequence
            || !string.Equals(
                owner.operationId,
                CropTreatmentPhysicalOutbox.FormatOperationId(
                    plotId.Value,
                    owner.operationSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                owner.destinationId,
                BuildTreatmentDestinationId(
                    plotId,
                    owner.operationSequence),
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

        bool hasWorker = !string.IsNullOrWhiteSpace(owner.responsibleWorkerId)
            && string.Equals(
                owner.responsibleWorkerId,
                owner.responsibleWorkerId.Trim(),
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(
                owner.responsibleWorkerDisplayName)
            && owner.workContributions != null
            && owner.workContributions.Count > 0
            && owner.workContributions.All(value => value != null
                && !string.IsNullOrWhiteSpace(value.characterId)
                && value.characterId == value.characterId.Trim()
                && float.IsFinite(value.contributedWork)
                && value.contributedWork > 0f)
            && owner.workContributions.Select(value => value.characterId)
                .Distinct(StringComparer.Ordinal).Count()
                == owner.workContributions.Count;
        bool hasBeforeSnapshot = !string.IsNullOrWhiteSpace(
                owner.ecologyBeforeFingerprint)
            && float.IsFinite(owner.pestPressureBefore)
            && owner.pestPressureBefore is >= 0f and <= 100f
            && float.IsFinite(owner.diseasePressureBefore)
            && owner.diseasePressureBefore is >= 0f and <= 100f
            && owner.cooldownBeforeDay >= 0
            && owner.completedAbsoluteDay >= 0;
        bool hasAfterSnapshot = hasBeforeSnapshot
            && !string.IsNullOrWhiteSpace(owner.ecologyAfterFingerprint)
            && float.IsFinite(owner.pestPressureAfter)
            && owner.pestPressureAfter is >= 0f and <= 100f
            && float.IsFinite(owner.diseasePressureAfter)
            && owner.diseasePressureAfter is >= 0f and <= 100f
            && owner.cooldownAfterDay
                == owner.completedAbsoluteDay + owner.cooldownDays;
        bool hasLedgerIdentity = IsCanonicalToken(owner.gameplayOutcomeId)
            && IsCropCommandFingerprint(owner.gameplayOutcomeDigest);

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
            && string.IsNullOrEmpty(owner.gameplayOutcomeId)
            && string.IsNullOrEmpty(owner.gameplayOutcomeDigest)
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
            ? emptyReceipt && (owner.completedWork <= 0f || hasWorker)
                    || owner.phase == CropTreatmentOrderPhase.Working
                        && hasWorker
                        && hasBeforeSnapshot
                        && (owner.sourceStackIds?.Count ?? 0) == 0
                        && owner.inputMassGrams == 0L
                        && string.IsNullOrEmpty(owner.commitId)
                        && string.IsNullOrEmpty(owner.requestFingerprint)
            : owner.phase == CropTreatmentOrderPhase.InputCommitted
                ? hasReceipt
                    && string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
                    && hasWorker && hasBeforeSnapshot
                    && owner.terminalDisposition
                        == CropTreatmentTerminalDisposition.None
                : owner.phase == CropTreatmentOrderPhase.OutcomePublished
                    ? hasReceipt
                        && !string.IsNullOrWhiteSpace(
                            owner.ecologyAfterFingerprint)
                        && hasWorker && hasAfterSnapshot
                        && string.IsNullOrEmpty(owner.gameplayOutcomeId)
                        && string.IsNullOrEmpty(owner.gameplayOutcomeDigest)
                        && owner.terminalDisposition
                            == CropTreatmentTerminalDisposition.None
                    : owner.phase == CropTreatmentOrderPhase.LedgerCommitted
                        ? hasReceipt
                            && hasWorker
                            && hasAfterSnapshot
                            && hasLedgerIdentity
                            && owner.terminalDisposition
                                == CropTreatmentTerminalDisposition.None
                    : hasReceipt && hasWorker && hasBeforeSnapshot
                        && string.IsNullOrEmpty(owner.gameplayOutcomeId)
                        && string.IsNullOrEmpty(owner.gameplayOutcomeDigest)
                        && CropTreatmentPhysicalOutbox.ValidateDestroyedPlotLoss(
                        owner,
                        out _);
        if (!valid)
            throw new InvalidOperationException(
                "Crop treatment physical provenance contradicts its phase.");
    }

    private static void ValidateCycleExecutionReceipt(
        CropPlotSaveData saved,
        CropDefinitionSO crop,
        BuildingInstanceId plotId)
    {
        CropCycleExecutionReceiptSaveData receipt =
            saved.cycleExecutionReceipt;
        CropPlanExecutionReceiptAuthority.Validate(
            receipt,
            requireCompleted: false);
        if (receipt.IsEmpty)
        {
            if (saved.materialsConsumed
                || saved.pendingHarvest.phase != CropHarvestOutputPhase.None
                || saved.failedOutcome != null
                    && !saved.failedOutcome.IsEmpty)
            {
                throw new InvalidOperationException(
                    "Active crop cycle is missing its durable execution receipt.");
            }
            return;
        }

        if (!string.Equals(receipt.plotId, plotId.Value, StringComparison.Ordinal)
            || !string.Equals(receipt.cropId, crop.CropId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop execution receipt drifted from its plot or crop.");
        }

        CropFailedOutcomeSaveData failedOutcome = saved.failedOutcome
            ?? new CropFailedOutcomeSaveData();
        if (IsFailedCycleReceipt(receipt))
        {
            if (failedOutcome.IsEmpty
                || !ProductionOutputClearanceProfileObservation
                    .IsLowercaseSha256(failedOutcome.receiptDigest)
                || !string.Equals(
                    failedOutcome.receiptDigest,
                    receipt.sourceDigest,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(failedOutcome.plotDisplayName)
                || string.IsNullOrWhiteSpace(failedOutcome.cropDisplayName)
                || !string.Equals(
                    saved.plotDisplayName,
                    failedOutcome.plotDisplayName,
                    StringComparison.Ordinal)
                || failedOutcome.absoluteDay < 0
                || failedOutcome.ownerRevision != checked((long)saved.nextHarvestOperationSequence + 1L))
                throw new InvalidOperationException(
                    "Crop failure outcome snapshot contradicts its terminal receipt.");
            ValidateCropTerminalPublicationFields(failedOutcome.publicationKey,
                failedOutcome.publicationId, failedOutcome.publicationFingerprint);
            _ = new CropCycleTerminalFact(plotId, receipt, failedOutcome.plotDisplayName,
                failedOutcome.cropDisplayName, failedOutcome.absoluteDay, failedOutcome.ownerRevision);
        }
        else if (!failedOutcome.IsEmpty)
            throw new InvalidOperationException(
                "A non-failed crop cycle retained a failure outcome snapshot.");

        bool activeCycle = saved.materialsConsumed
            || saved.pendingHarvest.phase != CropHarvestOutputPhase.None;
        if (activeCycle
            && receipt.status != CropCycleExecutionReceiptStatus.Active
            && !(IsFailedCycleReceipt(receipt)
                && saved.phase == CropPlotPhase.Blocked
                && saved.pendingHarvest.phase == CropHarvestOutputPhase.None))
            throw new InvalidOperationException(
                "Active crop cycle must retain its active execution receipt: "
                + $"status={receipt.status}, phase={saved.phase}, "
                + $"materialsConsumed={saved.materialsConsumed}, "
                + $"pendingHarvest={saved.pendingHarvest.phase}.");
        if (!activeCycle
            && receipt.status == CropCycleExecutionReceiptStatus.Active)
            throw new InvalidOperationException(
                "Inactive crop plot cannot retain an incomplete execution receipt.");
        if (saved.pendingHarvest.phase != CropHarvestOutputPhase.None
            && !string.Equals(
                receipt.cropId,
                saved.pendingHarvest.cropId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop harvest owner drifted from its cycle execution receipt.");
        }
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

    private void ValidateFrozenSowInputs(
        CropPlotSaveData saved,
        CropDefinitionSO crop,
        BuildingInstanceId plotId)
    {
        if (saved.frozenSowInputs == null)
            throw new InvalidOperationException(
                "Crop sow frozen input vector is missing.");

        bool postSow = saved.materialsConsumed
            || saved.phase is CropPlotPhase.ReadyToSow
                or CropPlotPhase.Sowing
                or CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting;
        bool hasFrozen = saved.frozenSowInputs.Count > 0
            || saved.frozenSowInputOperationSequence >= 0
            || !string.IsNullOrEmpty(saved.frozenSowInputSourceDigest)
            || !string.IsNullOrEmpty(saved.frozenSowInputVectorDigest)
            || saved.frozenSowInputConsumptionMultiplier != 0f
            || !string.IsNullOrEmpty(saved.frozenSowInputSelectedFuelItemId);
        bool hasPendingSow = saved.pendingSow.phase
            != CropPhysicalCommitPhase.None;
        if (postSow && !hasPendingSow)
        {
            if (hasFrozen)
                throw new InvalidOperationException(
                    "A post-sow crop plot retained a stale frozen input vector.");
            return;
        }

        bool requiresFrozen = saved.phase == CropPlotPhase.WaitingForMaterials
            || hasPendingSow;
        if (!requiresFrozen && !hasFrozen)
            return;
        if (saved.frozenSowInputOperationSequence
                != saved.nextSowOperationSequence
            || saved.frozenSowInputs.Count == 0
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                saved.frozenSowInputSourceDigest)
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                saved.frozenSowInputVectorDigest))
        {
            throw new InvalidOperationException(
                "Crop sow frozen input owner is incomplete or stale.");
        }
        if (!Enum.IsDefined(
                typeof(SurvivalWeatherType),
                saved.frozenSowInputWeather)
            || !float.IsFinite(saved.frozenSowInputConsumptionMultiplier)
            || saved.frozenSowInputConsumptionMultiplier is < 0.1f or > 1f
            || !string.IsNullOrEmpty(saved.frozenSowInputSelectedFuelItemId)
                && (!string.Equals(
                        saved.frozenSowInputSelectedFuelItemId,
                        saved.frozenSowInputSelectedFuelItemId.Trim(),
                        StringComparison.Ordinal)
                    || !catalog.TryGetItem(
                        saved.frozenSowInputSelectedFuelItemId,
                        out _)))
        {
            throw new InvalidOperationException(
                "Crop sow frozen input selection context is invalid.");
        }

        Dictionary<string, int> requirements = new(StringComparer.Ordinal);
        foreach (CropCycleInputRequirementSaveData input in
                 saved.frozenSowInputs)
        {
            if (input == null
                || string.IsNullOrWhiteSpace(input.itemId)
                || !string.Equals(input.itemId, input.itemId.Trim(),
                    StringComparison.Ordinal)
                || input.quantity <= 0
                || !catalog.TryGetItem(input.itemId, out _)
                || !requirements.TryAdd(input.itemId, input.quantity))
            {
                throw new InvalidOperationException(
                    "Crop sow frozen input vector contains an invalid item.");
            }
        }
        if (!string.IsNullOrEmpty(saved.frozenSowInputSelectedFuelItemId)
            && !requirements.ContainsKey(
                saved.frozenSowInputSelectedFuelItemId))
        {
            throw new InvalidOperationException(
                "Crop sow frozen fuel is absent from its input vector.");
        }
        if (hasPendingSow)
        {
            Dictionary<string, int> committed = saved.pendingSow.inputs
                .GroupBy(value => value.itemId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(value => value.quantity),
                    StringComparer.Ordinal);
            bool committedExact = committed.Count == requirements.Count
                && requirements.All(value =>
                    committed.TryGetValue(value.Key, out int quantity)
                    && quantity == value.Value);
            if (!committedExact)
                throw new InvalidOperationException(
                    "Crop sow frozen inputs drifted from the committed transaction.");
        }
        string expectedDigest = CaptureFrozenSowInputVectorDigest(
            plotId,
            crop.CropId,
            saved.frozenSowInputOperationSequence,
            requirements);
        if (!string.Equals(
                saved.frozenSowInputVectorDigest,
                expectedDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop sow frozen input vector digest drifted.");
        }
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
            if (!TryRetireInactiveWaterRefill(state))
            {
                failureReason =
                    "crop-next-cycle-water-refill-retirement-failed:"
                    + (state.WaterRefill?.failureReason ?? "unknown");
                return false;
            }
            if (!TryRetireDestination(
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
        ClearFrozenSowInputs(state);
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

        if (!TryAdvanceIrrigationPublication(state))
        {
            state.BlockedReason = "crop-irrigation-publication-blocks-plot-destruction";
            snapshotsDirty = true;
            return false;
        }

        if (state.CropSelectionOutcome is { IsEmpty: false }
            && !TryAdvanceCropSelectionOutcome(state))
        {
            state.BlockedReason =
                "crop-selection-outcome-blocks-plot-destruction";
            snapshotsDirty = true;
            return false;
        }
        if (state.GoldenHarvestScheduleOutcome is { IsEmpty: false }
            && !TryAdvanceGoldenHarvestScheduleOutcome(state))
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
            FreezeFailedOutcome(state, CurrentAbsoluteDay);
        }
        if (state.CycleExecutionReceipt?.status
            == CropCycleExecutionReceiptStatus.Active)
        {
            state.CycleExecutionReceipt =
                CropPlanExecutionReceiptAuthority.Fail(
                    state.CycleExecutionReceipt,
                    CropCycleExecutionReceiptStatus.FailedPlotDestroyed,
                    "crop-cycle-failed-plot-destroyed");
            FreezeFailedOutcome(state, CurrentAbsoluteDay);
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
                MarkChanged(replan: false);
            }
            if (!TryEnsureFailedOutcomeAcknowledged(
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

        if (!TryRetireInactiveWaterRefill(state))
            return false;

        if (state.Treatment.phase is CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted)
        {
            TryFinalizeTreatment(state);
            if (state.Treatment.phase != CropTreatmentOrderPhase.None)
                return false;
        }
        else if (state.Treatment.phase is CropTreatmentOrderPhase.InputCommitted
                or CropTreatmentOrderPhase.PlotDestroyedLossPending)
        {
            if (!TryRetireDestination(
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
            if (!TryRetireDestination(
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
            FinalizePublishedSow(state);
            if (state.PendingSow.phase != CropPhysicalCommitPhase.None)
                return false;
        }

        if (state.PendingSow.phase is CropPhysicalCommitPhase.InputCommitted
                or CropPhysicalCommitPhase.PlotDestroyedLossPending)
        {
            if (!TryRetireDestination(
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
                if (!TryAdvanceFailedCycle(state))
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
        if (!TryRetireDestination(
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
            && !TryRetireDestination(
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
            && !TryRetireDestination(
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
        if (!TryDeliverCropFact(new CropPlotRetiredFact(plotId), ref state.RetirementPublicationKey,
            ref publicationId, ref fingerprint, out string failure))
        {
            state.BlockedReason = failure;
            return;
        }
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
        int environmentalFieldVersion = environmentalField.Version;
        int irrigationVersion = irrigation.Version;
        if (!snapshotsDirty
            && observedEnvironmentalFieldVersion == environmentalFieldVersion
            && observedIrrigationVersion == irrigationVersion)
        {
            return;
        }

        snapshots.Clear();
        foreach (CropPlotState state in states.Values
                     .OrderBy(entry => entry.PlotId.Value, StringComparer.Ordinal))
        {
            // A destroyed facility can retain its crop state until terminal
            // physical owners finish releasing their claims and inputs. That
            // detached state is lifecycle-only and must not be projected.
            if (state.Building == null || state.Building.IsBuildingDestroyed)
            {
                continue;
            }
            if (state.Ability == null)
            {
                throw new InvalidOperationException(
                    "A live crop plot has no authored crop-plot ability: "
                    + state.PlotId.Value);
            }
            if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
            {
                continue;
            }

            Dictionary<string, int> required = RequiresSowInputAuthority(state)
                ? BuildMaterialRequirements(state, crop)
                : new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> delivered = required.Keys.ToDictionary(
                itemId => itemId,
                itemId => items.CountDelivered(
                    itemId,
                    state.MaterialDestinationId),
                StringComparer.Ordinal);
            CropCycleWaterSupplyStatus waterSupplyStatus =
                ResolveCycleWaterSupplyStatus(
                    state,
                    crop,
                    required,
                    out int cycleWaterQuantity);
            float dailyWaterDemand = CropWaterRules.ResolveDailyDemand(
                crop,
                state.Ability);
            CropWaterRefillSaveData waterRefill = state.WaterRefill
                ?? new CropWaterRefillSaveData();
            int waterRefillDelivered = waterRefill.phase
                    == CropWaterRefillPhase.None
                ? 0
                : items.CountDelivered(
                    waterRefill.itemId,
                    waterRefill.destinationId);
            CropEcologyPlotSaveData ecologyPlot = ecology.Plots.FirstOrDefault(value =>
                string.Equals(value.plotId, state.PlotId.Value, StringComparison.Ordinal));
            CropTreatmentOrderSaveData treatment = state.Treatment
                ?? new CropTreatmentOrderSaveData();
            EnvironmentalCellSnapshot environmentCell = default;
            bool hasEnvironmentObservation = state.Building != null
                && environmentalField.IsInitialized
                && environmentalField.TryGetCell(
                    state.Building.centerPos,
                    out environmentCell);
            bool requiresEcologyPhenotype = state.Phase is
                CropPlotPhase.Sowing
                or CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting;
            if (ecologyPlot == null && requiresEcologyPhenotype)
                throw new InvalidOperationException(
                    $"Crop plot '{state.PlotId.Value}' in phase {state.Phase} "
                    + "requires an owned ecology phenotype.");
            CropGenomePhenotype temperaturePhenotype = requiresEcologyPhenotype
                ? ecology.GetPhenotype(state.PlotId.Value)
                : new CropGenomePhenotype(
                    coldToleranceDegrees: 0f,
                    heatToleranceDegrees: 0f,
                    growthMultiplier: 1f,
                    yieldMultiplier: 1f,
                    diseaseRiskMultiplier: 1f,
                    seedYieldBonus: 0);
            CropGrowthTemperatureEvaluation temperatureEvaluation =
                CropGrowthCycleAuthority.EvaluateTemperature(
                    crop,
                    temperaturePhenotype,
                    hasEnvironmentObservation,
                    hasEnvironmentObservation
                        ? environmentCell.TemperatureC
                        : 0f);
            CropGrowthLightEvaluation lightEvaluation =
                CropGrowthCycleAuthority.EvaluateLight(
                    crop,
                    hasEnvironmentObservation,
                    hasEnvironmentObservation
                        ? environmentCell.LightLevel
                        : 0f);
            CropIrrigationAssessment irrigationAssessment = irrigation.Assess(
                new CropIrrigationRequest(
                    state.Building,
                    dailyWaterDemand > 0f
                        && state.CurrentWater + 0.001f < dailyWaterDemand,
                    state.CurrentWater,
                    state.WaterCapacity,
                    waterRefill.phase != CropWaterRefillPhase.None));
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
                CycleWaterSupplyStatus = waterSupplyStatus,
                CycleWaterQuantity = cycleWaterQuantity,
                WaterStatus = ResolveWaterStatus(
                    state,
                    dailyWaterDemand),
                CurrentWater = state.CurrentWater,
                WaterCapacity = state.WaterCapacity,
                WaterDemandPerDay = dailyWaterDemand,
                WaterGrowthMultiplier = CropWaterRules.ResolveGrowthMultiplier(
                    state.CurrentWater,
                    dailyWaterDemand),
                TemperatureStatus = temperatureEvaluation.Status,
                CurrentTemperatureC =
                    temperatureEvaluation.ObservedTemperatureC,
                MinimumTemperatureC =
                    temperatureEvaluation.MinimumTemperatureC,
                MaximumTemperatureC =
                    temperatureEvaluation.MaximumTemperatureC,
                LightStatus = lightEvaluation.Status,
                CurrentLight = lightEvaluation.ObservedLight,
                LightStopThreshold = lightEvaluation.StopLight,
                LightSufficientThreshold = lightEvaluation.SufficientLight,
                LightGrowthMultiplier = lightEvaluation.GrowthMultiplier,
                WaterRefillPhase = waterRefill.phase,
                WaterRefillDestinationId = waterRefill.destinationId,
                WaterRefillDeliveredQuantity = waterRefillDelivered,
                WaterRefillRequiredWork = waterRefill.requiredWork,
                WaterRefillCompletedWork = waterRefill.completedWork,
                WaterRefillFailureReason = waterRefill.failureReason,
                IrrigationStatus = irrigationAssessment.Status,
                IrrigationFacilityId = irrigationAssessment.IrrigatorId.Value,
                IrrigationReason = irrigationAssessment.Reason,
                BlockedReason = state.BlockedReason,
                GoldenHarvestHarvesterId = state.GoldenHarvestHarvesterId,
                GoldenHarvestAttemptSequence = state.GoldenHarvestAttemptSequence,
                HasSeasonalPrimaryYieldDamage =
                    state.SeasonalYieldDamage is { IsEmpty: false },
                SeasonalDamageSourceEventInstanceId =
                    state.SeasonalYieldDamage?.sourceEventInstanceId
                    ?? string.Empty,
                SeasonalPrimaryBatchLossPercent =
                    state.SeasonalYieldDamage?.primaryBatchLossPercent ?? 0,
                FrozenPrimaryQuantityBeforeSeasonalLoss =
                    state.PendingHarvest?.primaryQuantityBeforeSeasonalLoss ?? 0,
                FrozenPrimarySeasonalLossQuantity =
                    state.PendingHarvest?.seasonalPrimaryLossQuantity ?? 0,
                FrozenPrimaryQuantityAfterSeasonalLoss =
                    state.PendingHarvest is
                        { seasonalPrimaryBatchLossPercent: > 0 }
                        ? state.PendingHarvest.harvestQuantity
                        : 0,
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

        observedEnvironmentalFieldVersion = environmentalFieldVersion;
        observedIrrigationVersion = irrigationVersion;
        snapshotsDirty = false;
    }

    private static CropCycleWaterSupplyStatus ResolveCycleWaterSupplyStatus(
        CropPlotState state,
        CropDefinitionSO crop,
        IReadOnlyDictionary<string, int> currentRequirements,
        out int cycleWaterQuantity)
    {
        cycleWaterQuantity = 0;
        if (currentRequirements != null
            && currentRequirements.TryGetValue(
                CropCycleInputRequirementAuthority.CleanWaterItemId,
                out int requiredWater))
        {
            cycleWaterQuantity = requiredWater;
            return CropCycleWaterSupplyStatus.AwaitingCycleSupply;
        }

        CropCycleExecutionReceiptSaveData receipt = state?.CycleExecutionReceipt;
        if (state?.MaterialsConsumed == true
            && receipt != null
            && receipt.status == CropCycleExecutionReceiptStatus.Active)
        {
            cycleWaterQuantity = (receipt.inputs
                    ?? new List<CropPhysicalInputSaveData>())
                .Where(input => input != null && string.Equals(
                    input.itemId,
                    CropCycleInputRequirementAuthority.CleanWaterItemId,
                    StringComparison.Ordinal))
                .Sum(input => input.quantity);
            if (cycleWaterQuantity > 0)
                return CropCycleWaterSupplyStatus.SuppliedForCurrentCycle;
            if (crop.DailyWater > 0f)
                throw new InvalidOperationException(
                    "Active crop cycle is missing its committed clean-water input.");
            return CropCycleWaterSupplyStatus.NotRequired;
        }

        return crop.DailyWater <= 0f
            ? CropCycleWaterSupplyStatus.NotRequired
            : CropCycleWaterSupplyStatus.None;
    }

    private static CropPlotWaterStatus ResolveWaterStatus(
        CropPlotState state,
        float dailyWaterDemand)
    {
        if (dailyWaterDemand <= 0f)
            return CropPlotWaterStatus.NotRequired;
        CropWaterRefillPhase phase = state?.WaterRefill?.phase
            ?? CropWaterRefillPhase.None;
        return phase switch
        {
            CropWaterRefillPhase.WaitingForDelivery =>
                CropPlotWaterStatus.WaitingForDelivery,
            CropWaterRefillPhase.ReadyForWork => CropPlotWaterStatus.ReadyForWork,
            CropWaterRefillPhase.Working => CropPlotWaterStatus.Working,
            CropWaterRefillPhase.InputCommitted =>
                CropPlotWaterStatus.InputCommitted,
            CropWaterRefillPhase.OutcomePublished =>
                CropPlotWaterStatus.OutcomePublished,
            CropWaterRefillPhase.LedgerCommitted =>
                CropPlotWaterStatus.OutcomePublished,
            _ => state.CurrentWater <= 0f
                ? CropPlotWaterStatus.Empty
                : state.CurrentWater + 0.001f < dailyWaterDemand
                    ? CropPlotWaterStatus.Low
                    : CropPlotWaterStatus.Sufficient
        };
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

    private bool TryRequireMutable(
        BuildingInstanceId plotId,
        out string failureReason)
    {
        if (states.TryGetValue(plotId, out CropPlotState state)
            && state.IrrigationPublication.pending)
        {
            failureReason = "crop-irrigation-publication-pending:" + state.IrrigationPublication.failureReason;
            return false;
        }
        if (ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                facilityMutations,
                plotId,
                out DomainFailure failure))
        {
            failureReason = string.Empty;
            return true;
        }

        ReadOnlySpan<string> parameters = failure.Parameters;
        failureReason = parameters.Length == 0
            ? "production-facility-mutation-open"
            : parameters[parameters.Length - 1];
        return false;
    }

    private static BuildingInstanceId BuildPlotId(BuildableObject plot)
    {
        return plot == null
            ? default
            : plot.RequirePersistentInstanceId();
    }

}
