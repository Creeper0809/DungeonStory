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
    public int NextSowOperationSequence;
    public CropPhysicalCommitSaveData PendingSow = new();
    public string PendingCycleCorrelationId = string.Empty;
    public CropCycleExecutionReceiptSaveData CycleExecutionReceipt = new();
    public int NextTreatmentOperationSequence;
    public int PestLureNextAllowedDay;
    public int BotanicalPesticideNextAllowedDay;
    public int FungicideNextAllowedDay;
    public CropTreatmentOrderSaveData Treatment = new();
    public int NextHarvestOperationSequence;
    public CropHarvestOutputSaveData PendingHarvest = new();
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
        CharacterIdentityEventPublisher identityEvents = null,
        IWorkCompletionIdentityDeliveryCommand completionDeliveries = null)
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
        CompletionDeliveries = completionDeliveries;
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
    public IWorkCompletionIdentityDeliveryCommand CompletionDeliveries { get; }
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
    private const float MaterialRequestInterval = 0.5f;
    public const string HarvestOutputBatchCommitPrefix =
        ProductionDomainOutputPublicationIdentity.BatchCommitPrefix
        + "crop-harvest:";
    public const string HarvestOutputPublicationOperationPrefix =
        ProductionDomainOutputPublicationIdentity.PublicationOperationPrefix
        + "crop-harvest:";
    public const string HarvestCompletionDeliveryPrefix = "identity-event:";
    public const string HarvestCompletionStreamPrefix = "crop-harvest:";

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
    private readonly IWorkCompletionIdentityDeliveryCommand completionDeliveries;
    private readonly ICharacterPerformanceQuery performance;
    private readonly IProductionFacilityMutationEpochQuery facilityMutations;
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
            || state.MaterialsConsumed
            || state.PendingSow.phase != CropPhysicalCommitPhase.None
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

        owners[0].CycleExecutionReceipt =
            new CropCycleExecutionReceiptSaveData();
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
        completionDeliveries = simulation.CompletionDeliveries;
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
        if (!inputOwners.TryReconcileLive(
                Array.Empty<CropPlotInputOwnerDescriptor>(),
                out string ownerFailure))
            throw new InvalidOperationException(
                "Crop-plot input owner disposal failed: " + ownerFailure);

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
        if (!TryRequireMutable(state.PlotId, out message))
            return false;

        if (state.MaterialsConsumed
            || state.PendingSow.phase != CropPhysicalCommitPhase.None
            || state.Treatment.phase != CropTreatmentOrderPhase.None
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

        if (!TryRetireDestination(
                state,
                state.MaterialDestinationId,
                CropPlotInputOwnerAuthority.CropChangedReleaseReasonCode,
                out string retireFailure))
        {
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
        ClearFrozenSowInputs(state);
        state.PendingCycleCorrelationId = string.Empty;
        state.CycleExecutionReceipt = new CropCycleExecutionReceiptSaveData();
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
                        : ResolveUnavailableReason(state));
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
            if (state.PendingHarvest.phase != CropHarvestOutputPhase.None)
            {
                TryAdvancePendingHarvest(state, crop, out cycleCompleted);
                MarkChanged();
                return true;
            }
            if (!TryRequireMutable(state.PlotId, out _))
                return false;
            state.Phase = CropPlotPhase.Harvesting;
            state.HarvestWork = Mathf.Min(
                crop.HarvestWork,
                state.HarvestWork + amount);
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
        int harvestQuantity = CropHarvestOutputRules.ResolveHarvestQuantity(
            crop.Yield,
            outputMultiplier,
            workerYieldMultiplier,
            extremeYieldMultiplier,
            ecologyPrepared.Result.YieldMultiplier,
            IsOperational(ResearchFacilityCommandKind.SoilDiagnostics));
        int seedQuantity = CropHarvestOutputRules.ResolveReturnedSeedQuantity(
            ecologyPrepared.Result.ReturnedSeedCount,
            extremeSeedMultiplier,
            IsOperational(ResearchFacilityCommandKind.SeedSelection));
        string harvesterId = worker?.Identity?.PersistentId ?? string.Empty;
        int completionAbsoluteDay = Mathf.Max(
            0,
            Mathf.FloorToInt(
                gameClock.Time / GameCalendarRules.SecondsPerDay));
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
            harvestCapability = ProductionOutputCapabilitySaveData.Freeze(
                harvestCapability),
            seedCapability = ProductionOutputCapabilitySaveData.Freeze(
                seedCapability),
            outputPublication = new ProductionDomainOutputPublicationSaveData()
        };
        if (!string.IsNullOrEmpty(harvesterId))
        {
            pending.completionDeliveryId = HarvestCompletionDeliveryPrefix
                + operationId;
            WorkCompletionIdentityDeliveryRequest completion =
                CreateHarvestCompletionDelivery(state.PlotId, pending);
            pending.completionDeliveryFingerprint =
                completion.PayloadFingerprint;
        }
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
        if (!pending.completionEventPublished)
        {
            if (!string.IsNullOrEmpty(pending.harvesterId))
            {
                if (completionDeliveries == null)
                    throw new InvalidOperationException(
                        "Crop harvest has no durable completion delivery command.");
                WorkCompletionIdentityDeliveryResult delivery =
                    completionDeliveries.EnsureApplied(
                        CreateHarvestCompletionDelivery(state.PlotId, pending));
                if (delivery.Status ==
                    WorkCompletionIdentityDeliveryStatus.Deferred)
                {
                    state.BlockedReason = delivery.FailureReason;
                    return;
                }
                if (!delivery.IsApplied)
                    throw new InvalidOperationException(
                        "Crop harvest completion delivery conflicted: "
                        + delivery.FailureReason);
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
        state.CycleExecutionReceipt = CropPlanExecutionReceiptAuthority.Complete(
            state.CycleExecutionReceipt,
            pending);
        ResetForNextCycle(state);
        state.NextHarvestOperationSequence = checked(
            state.NextHarvestOperationSequence + 1);
        state.PendingHarvest = new CropHarvestOutputSaveData();
        cycleCompleted = true;
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
        digest.Append("crop-harvest-frozen-output@1");
        digest.Append(pending.operationId);
        digest.Append(plotId.Value);
        digest.Append(pending.cropId);
        digest.Append(pending.indoor);
        digest.Append(pending.harvesterId);
        digest.Append(pending.outcomeId);
        digest.Append(pending.ecologyOutcomeFingerprint);
        digest.Append(pending.goldenOutcomeFingerprint);
        digest.Append(pending.harvestItemId);
        digest.Append(pending.harvestQuantity);
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

    public static WorkCompletionIdentityDeliveryRequest
        CreateHarvestCompletionDelivery(
            BuildingInstanceId plotId,
            CropHarvestOutputSaveData pending)
    {
        if (!plotId.IsValid
            || pending == null
            || string.IsNullOrWhiteSpace(pending.harvesterId)
            || string.IsNullOrWhiteSpace(pending.operationId)
            || string.IsNullOrWhiteSpace(pending.outcomeId))
            throw new InvalidOperationException(
                "Crop harvest completion provenance is incomplete.");
        CharacterId character = new(pending.harvesterId);
        WorkCompletionIdentityDeliveryRequest request = new(
            HarvestCompletionDeliveryPrefix + pending.operationId,
            HarvestCompletionStreamPrefix + plotId.Value,
            pending.operationSequence,
            character,
            BuiltInWorkTypeIds.Harvest.Value,
            plotId.Value + ":" + pending.outcomeId,
            CharacterCommandOrigin.Autonomous,
            pending.completionAbsoluteDay);
        if (!string.IsNullOrEmpty(pending.completionDeliveryId)
            && !string.Equals(
                pending.completionDeliveryId,
                request.DeliveryId,
                StringComparison.Ordinal)
            || !string.IsNullOrEmpty(pending.completionDeliveryFingerprint)
            && !string.Equals(
                pending.completionDeliveryFingerprint,
                request.PayloadFingerprint,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Crop harvest completion delivery provenance drifted.");
        return request;
    }

    public DungeonCropPlotSaveData Capture()
    {
        if (!inputOwners.TryReconcileLive(
                BuildLiveInputOwnerDescriptors(),
                out string ownerFailure))
            throw new InvalidOperationException(
                "Crop-plot input ownership is not capture-safe: "
                + ownerFailure);
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
                nextSowOperationSequence = state.NextSowOperationSequence,
                pendingSow = state.PendingSow.DeepClone(),
                pendingCycleCorrelationId =
                    state.PendingCycleCorrelationId,
                cycleExecutionReceipt = state.CycleExecutionReceipt.DeepClone(),
                nextTreatmentOperationSequence =
                    state.NextTreatmentOperationSequence,
                pestLureNextAllowedDay = state.PestLureNextAllowedDay,
                botanicalPesticideNextAllowedDay =
                    state.BotanicalPesticideNextAllowedDay,
                fungicideNextAllowedDay = state.FungicideNextAllowedDay,
                treatment = state.Treatment.DeepClone(),
                nextHarvestOperationSequence =
                    state.NextHarvestOperationSequence,
                pendingHarvest = state.PendingHarvest.DeepClone()
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
        HashSet<string> correlations = new(StringComparer.Ordinal);
        foreach (CropPlotSaveData saved in snapshot.plots)
        {
            BuildingInstanceId plotId = RequireRestorePlotId(saved, seen);
            CropDefinitionSO crop = RequireCrop(saved);
            ValidateRestoreProgress(saved, crop, plotId);
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
                LastKnownPosition = new Vector2Int(
                    saved.lastKnownGridX,
                    saved.lastKnownGridY),
                CropId = crop.CropId,
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
                NextSowOperationSequence = saved.nextSowOperationSequence,
                PendingSow = saved.pendingSow.DeepClone(),
                PendingCycleCorrelationId =
                    saved.pendingCycleCorrelationId ?? string.Empty,
                CycleExecutionReceipt =
                    saved.cycleExecutionReceipt.DeepClone(),
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
        float gameHours = gameClock.DeltaTime
            / GameSimulationTimeRules.SecondsPerGameHour;
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
            state.CycleExecutionReceipt =
                CropPlanExecutionReceiptAuthority.Begin(
                    correlationId,
                    explicitCorrelation,
                    state.PlotId.Value,
                    state.Ability.Indoor,
                    state.PendingSow);
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
        if (!TryRetireDestination(
                state,
                treatment.destinationId,
                CropPlotInputOwnerAuthority
                    .TreatmentCompletedReleaseReasonCode,
                out string retireFailure))
        {
            treatment.failureReason = retireFailure;
            snapshotsDirty = true;
            return outcomePublishedNow;
        }
        if (!CropTreatmentPhysicalOutbox.TryAcknowledgeOutcome(
                treatment,
                treatmentItems,
                out string acknowledgeFailure))
        {
            treatment.failureReason = acknowledgeFailure;
            snapshotsDirty = true;
            return outcomePublishedNow;
        }

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

    private static bool RequiresAnyInputAuthority(CropPlotState state) =>
        RequiresSowInputAuthority(state)
        || state?.Treatment?.phase != CropTreatmentOrderPhase.None;

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
        if (state.Ability.Indoor)
        {
            return CropGrowthCycleAuthority.ResolveIndoorRuntimeMultiplier(
                state.Ability,
                IsOperational(ResearchFacilityCommandKind.ClimateControl),
                IsOperational(ResearchFacilityCommandKind.CropCalendar),
                phenotype);
        }

        SurvivalEnvironmentSnapshot environment =
            environmentQuery.GetEnvironmentSnapshot();
        TimeOfDay? timeOfDay = gameDataProvider.TryGetSessionState(
                out GameSessionState data)
            && data?.timeOfDay != null
                ? data.timeOfDay.Value
                : null;
        return CropGrowthCycleAuthority.ResolveOutdoorRuntimeMultiplier(
            state.Ability,
            crop,
            phenotype,
            environment,
            timeOfDay,
            IsOperational(ResearchFacilityCommandKind.CropCalendar),
            out blockedReason);
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
            if (state.CycleExecutionReceipt != null
                && !state.CycleExecutionReceipt.IsEmpty
                && state.CycleExecutionReceipt.status
                    == CropCycleExecutionReceiptStatus.Active)
            {
                state.CycleExecutionReceipt =
                    CropPlanExecutionReceiptAuthority.Fail(
                        state.CycleExecutionReceipt,
                        CropCycleExecutionReceiptStatus.FailedCropDeath,
                        "crop-cycle-failed-crop-death");
            }
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
        CropPlotState state = new()
        {
            PlotId = plotId,
            Building = building,
            Ability = ability,
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
        ValidateFrozenSowInputs(saved, crop, plotId);
        ValidateCycleExecutionReceipt(saved, crop, plotId);
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
                && owner.harvestCapability is { IsEmpty: true }
                && owner.seedCapability is { IsEmpty: true }
                && owner.outputPublication is { IsEmpty: true };
            if (!empty)
                throw new InvalidOperationException(
                    "Empty crop harvest owner contains frozen provenance.");
            return;
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
            || owner.ecologyAcknowledged && !owner.ecologyCommitted
            || owner.goldenCommitted && !owner.goldenPrepared
            || owner.goldenAcknowledged && !owner.goldenCommitted
            || owner.goldenCommitted && !owner.ecologyCommitted
            || owner.ecologyAcknowledged && !owner.completionEventPublished
            || owner.goldenAcknowledged
                && (!owner.ecologyAcknowledged
                    || !owner.completionEventPublished)
            || owner.completionEventPublished
                && (owner.phase != CropHarvestOutputPhase
                        .OutputRestoredAwaitingFinalization
                    || !owner.ecologyCommitted
                    || owner.goldenPrepared && !owner.goldenCommitted)
            || owner.completionAbsoluteDay < 0
            || !string.IsNullOrEmpty(owner.harvesterId)
                && !IsCanonicalToken(owner.harvesterId)
            || !IsCanonicalToken(owner.outcomeId)
            || !Enum.IsDefined(typeof(ExtremeRiskOutcome), owner.goldenOutcome)
            || !IsFinitePositive(owner.goldenPrimaryMultiplier)
            || !IsFinitePositive(owner.goldenSecondaryMultiplier))
        {
            throw new InvalidOperationException(
                "Crop harvest frozen owner contradicts plot state or authored maximums.");
        }

        if (string.IsNullOrEmpty(owner.harvesterId))
        {
            if (!string.IsNullOrEmpty(owner.completionDeliveryId)
                || !string.IsNullOrEmpty(owner.completionDeliveryFingerprint))
                throw new InvalidOperationException(
                    "Workerless crop harvest contains completion delivery provenance.");
        }
        else
        {
            WorkCompletionIdentityDeliveryRequest completion =
                CreateHarvestCompletionDelivery(plotId, owner);
            if (!string.Equals(
                    owner.completionDeliveryId,
                    completion.DeliveryId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.completionDeliveryFingerprint,
                    completion.PayloadFingerprint,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop harvest completion delivery fingerprint is invalid.");
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

    private static bool IsSerializedEmptySeedLot(SeedLotState value) =>
        value == null
        || string.IsNullOrEmpty(value.cropId)
        && string.IsNullOrEmpty(value.cultivarGenomeId)
        && value.generation == 0
        && value.pathogenLoad == 0f;

    private static bool IsCanonicalToken(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

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
                || saved.pendingHarvest.phase != CropHarvestOutputPhase.None)
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

        bool activeCycle = saved.materialsConsumed
            || saved.pendingHarvest.phase != CropHarvestOutputPhase.None;
        if (activeCycle
            && receipt.status != CropCycleExecutionReceiptStatus.Active)
            throw new InvalidOperationException(
                "Active crop cycle must retain its active execution receipt.");
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

    private void ResetForNextCycle(CropPlotState state)
    {
        if (!TryRetireDestination(
                state,
                state.MaterialDestinationId,
                CropPlotInputOwnerAuthority.SowCompletedReleaseReasonCode,
                out string retireFailure))
            throw new InvalidOperationException(
                "Crop-plot next-cycle input retirement failed: "
                + retireFailure);
        state.Phase = CropPlotPhase.Empty;
        state.SowWork = 0f;
        state.GrowthHours = 0f;
        state.HarvestWork = 0f;
        state.MaterialsConsumed = false;
        ClearFrozenSowInputs(state);
        state.BlockedReason = string.Empty;
        state.GoldenHarvestHarvesterId = string.Empty;
    }

    private bool TryFinalizeDestroyedPlot(CropPlotState state)
    {
        if (state == null)
            return true;

        // Destructive mutation must be fenced by the shared facility lifecycle
        // query before the plot disappears. If a caller bypasses that fence,
        // retain the exact frozen/committed owner instead of deleting output or
        // inventing a loose fallback location.
        if (state.PendingHarvest?.phase != CropHarvestOutputPhase.None)
        {
            state.BlockedReason =
                "crop-harvest-output-owner-blocks-plot-destruction";
            snapshotsDirty = true;
            return false;
        }

        if (state.Treatment.phase == CropTreatmentOrderPhase.OutcomePublished)
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
            if (state.CycleExecutionReceipt.status
                == CropCycleExecutionReceiptStatus.Active)
            {
                state.CycleExecutionReceipt =
                    CropPlanExecutionReceiptAuthority.Fail(
                        state.CycleExecutionReceipt,
                        CropCycleExecutionReceiptStatus.FailedPlotDestroyed,
                        "crop-cycle-failed-plot-destroyed");
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
            && state.Treatment.phase == CropTreatmentOrderPhase.None;
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
        ecology.AbandonPlot(plotId.Value);
        if (completionDeliveries != null
            && !completionDeliveries.RetireProducerStream(
                HarvestCompletionStreamPrefix + plotId.Value))
            throw new InvalidOperationException(
                "Crop completion delivery stream retirement failed.");
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

            Dictionary<string, int> required = RequiresSowInputAuthority(state)
                ? BuildMaterialRequirements(state, crop)
                : new Dictionary<string, int>(StringComparer.Ordinal);
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

    private bool TryRequireMutable(
        BuildingInstanceId plotId,
        out string failureReason)
    {
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
