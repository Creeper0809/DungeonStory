using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum ApparelWorkOrderKind
{
    Craft = 0,
    Laundry = 1,
    Drying = 2,
    Repair = 3,
    Alteration = 4
}

public enum ApparelWorkOrderState
{
    NeedsRevalidation = 0,
    WaitingForMaterials = 1,
    Ready = 2,
    InProgress = 3,
    Completed = 4,
    Failed = 5,
    WaitingForOutputSpace = 6,
    WaitingForEligibleWorker = 7,
    TargetCurrentlyUnreachable = 8,
    WaitingForDispositionFinalization = 9
}

public enum ApparelRepairCommitPhase
{
    None = 0,
    MaterialCommitted = 1,
    RepairApplied = 2
}

[Serializable]
public sealed class ApparelWorkOrderSaveData
{
    public string orderId = string.Empty;
    public ApparelWorkOrderKind kind;
    public ApparelWorkOrderState state;
    public string apparelDefinitionId = string.Empty;
    public string materialDefinitionId = string.Empty;
    public ApparelMaterialSelectionPolicy materialPolicy;
    public CraftsmanshipQualityTier minimumCraftsmanshipQuality;
    public WorkerSelectionPolicySaveData workerPolicy =
        WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.BestExpectedQuality);
    public List<CraftContributionSaveData> contributions = new();
    public string lastWorkerCharacterId = string.Empty;
    public CraftQualityRollSaveData qualityRoll;
    public int qualityAttemptIndex;
    public RejectedOutputDisposition rejectedDisposition =
        RejectedOutputDisposition.AutoDismantle;
    public QualityRepeatLimitMode repeatLimitMode =
        QualityRepeatLimitMode.SafeLimits;
    public int maximumAttempts = 10;
    public float workBudget;
    public float consumedWork;
    public int requiredAcceptedCount = 1;
    public int acceptedCount;
    public bool dismantlingRejectedOutput;
    public bool rejectedOutputConsumed;
    public string rejectedOutputStackId = string.Empty;
    public string rejectedOutputInstanceId = string.Empty;
    public float craftWorkPerAttempt;
    public int rejectedMaterialAmount;
    public int rejectedMaterialSpawned;
    public string rejectedRecoveryItemId = string.Empty;
    public string rejectedDismantleOperationId = string.Empty;
    public string rejectedDismantleCommitId = string.Empty;
    public string rejectedDismantleRequestFingerprint = string.Empty;
    public long rejectedDismantleInputMassGrams;
    public string rejectedRecoveryOperationId = string.Empty;
    public string rejectedRecoveryCommitId = string.Empty;
    public long rejectedRecoveryOutputMassGrams;
    public int rejectedRecoveryPublicationAttempt;
    public string rejectedRecoveryOutcomeFingerprint = string.Empty;
    public string rejectedRecoveryAdmissionTokenId = string.Empty;
    public ProductionOutputCapabilitySaveData rejectedRecoveryOutputCapability = new();
    public string rejectedRecoveryMaximumMassProofDigest = string.Empty;
    public long rejectedRecoveryMaximumBatchMassGrams;
    public string rejectedRecoveryCapacitySourceDigest = string.Empty;
    public long rejectedRecoveryRequiredMinimumCapacityGrams;
    public string rejectedRecoveryPlannedOutputFingerprint = string.Empty;
    public List<string> rejectedRecoveryStackIds = new();
    public bool rejectedRecoveryPublished;
    public bool rejectedRecoveryAdmissionCommitted;
    public bool rejectedRecoveryOutputAcknowledged;
    public bool rejectedDismantleAcknowledged;
    public int craftPublicationAttempt;
    public string craftPublicationOperationId = string.Empty;
    public string craftOutputBatchCommitId = string.Empty;
    public string craftOutcomeFingerprint = string.Empty;
    public string craftOutputComponentFingerprint = string.Empty;
    public ProductionOutputCapabilitySaveData craftOutputCapability = new();
    public string craftAdmissionTokenId = string.Empty;
    public string craftMaximumMassProofDigest = string.Empty;
    public long craftMaximumBatchMassGrams;
    public string craftCapacitySourceDigest = string.Empty;
    public long craftRequiredMinimumCapacityGrams;
    public string craftPlannedOutputFingerprint = string.Empty;
    public string craftOutputStackId = string.Empty;
    public string craftOutputInstanceId = string.Empty;
    public string craftInputCommitId = string.Empty;
    public string craftInputRequestFingerprint = string.Empty;
    public long craftInputMassGrams;
    public long craftOutputMassGrams;
    public bool craftInputPending;
    public bool craftOutputPublished;
    public bool craftAdmissionCommitted;
    public bool craftInputAcknowledged;
    public bool craftOutputAcknowledged;
    public bool craftMarketRouted;
    public string rejectedOutputLeaseId = string.Empty;
    public ApparelSizeClass targetSize;
    public ApparelModificationKind targetModifications;
    public string facilityInstanceId = string.Empty;
    public string targetItemInstanceId = string.Empty;
    public List<string> targetItemInstanceIds = new();
    public List<string> materialStackIds = new();
    public List<int> materialStackAmounts = new();
    public float requiredWork;
    public float completedWork;
    public int retryCount;
    public float nextRetryGameHour;
    public bool powered;
    public bool shortWardrobeOperation;
    public ApparelRepairCommitPhase repairCommitPhase;
    public string repairOperationId = string.Empty;
    public string repairReasonCode = string.Empty;
    public string repairCommitId = string.Empty;
    public List<string> repairSourceStackIds = new();
    public int repairInputQuantity;
    public long repairInputMassGrams;
    public string repairTargetStackId = string.Empty;
    public string repairOriginalStatePayload = string.Empty;
    public string repairResolvedStatePayload = string.Empty;
}

[Serializable]
public sealed class ApparelWorkOrderTerminalStateSaveData
{
    public const int CurrentSchemaVersion = 3;

    public int schemaVersion = CurrentSchemaVersion;
    public ApparelWorkOrderSaveData sourceOrder;
    public string sourceOrderFingerprint = string.Empty;
    public ProductionApparelOrderPendingEffectIdentity pendingEffect;
    public ProductionApparelOrderTerminalEffectReceipt terminalEffectReceipt;
    public ProductionApparelOrderSourceTerminalReceipt sourceTerminalReceipt;

    public ApparelWorkOrderTerminalStateSaveData Clone() => new()
    {
        schemaVersion = schemaVersion,
        sourceOrder = ProductionApparelOrderTerminalDrainCanonical.CloneOrder(
            sourceOrder),
        sourceOrderFingerprint = sourceOrderFingerprint ?? string.Empty,
        pendingEffect = ProductionApparelOrderTerminalDrainCanonical
            .CloneOptionalPendingEffect(pendingEffect),
        terminalEffectReceipt = ProductionApparelOrderTerminalDrainCanonical
            .CloneOptionalTerminalEffectReceipt(terminalEffectReceipt),
        sourceTerminalReceipt = ProductionApparelOrderTerminalDrainCanonical
            .CloneOptionalSourceTerminalReceipt(sourceTerminalReceipt)
    };
}

public sealed class ApparelWorkOrderRestoreCandidate
{
    internal ApparelWorkOrderRestoreCandidate(
        IReadOnlyList<ApparelWorkOrderSaveData> orders,
        IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> terminalStates)
    {
        Orders = orders ?? throw new ArgumentNullException(nameof(orders));
        TerminalStates = terminalStates
            ?? throw new ArgumentNullException(nameof(terminalStates));
    }

    internal IReadOnlyList<ApparelWorkOrderSaveData> Orders { get; }
    internal IReadOnlyList<ApparelWorkOrderTerminalStateSaveData>
        TerminalStates { get; }
}

public readonly struct ApparelCraftOrderRequest
{
    public ApparelCraftOrderRequest(
        string apparelDefinitionId,
        ApparelSizeClass size,
        ApparelModificationKind modifications,
        ApparelMaterialSelectionPolicy materialPolicy,
        string exactMaterialDefinitionId = "",
        CraftsmanshipQualityTier minimumCraftsmanshipQuality =
            CraftsmanshipQualityTier.Normal,
        WorkerSelectionPolicySaveData workerPolicy = null,
        RejectedOutputDisposition rejectedDisposition =
            RejectedOutputDisposition.AutoDismantle,
        QualityRepeatLimitMode repeatLimitMode =
            QualityRepeatLimitMode.SafeLimits,
        int maximumAttempts = 10,
        float workBudget = 0f,
        int requiredAcceptedCount = 1)
    {
        ApparelDefinitionId = apparelDefinitionId?.Trim() ?? string.Empty;
        Size = size;
        Modifications = modifications;
        MaterialPolicy = materialPolicy;
        ExactMaterialDefinitionId = exactMaterialDefinitionId?.Trim() ?? string.Empty;
        MinimumCraftsmanshipQuality = minimumCraftsmanshipQuality;
        WorkerPolicy = workerPolicy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality);
        RejectedDisposition = rejectedDisposition;
        RepeatLimitMode = repeatLimitMode;
        MaximumAttempts = Mathf.Max(1, maximumAttempts);
        WorkBudget = Mathf.Max(0f, workBudget);
        RequiredAcceptedCount = Mathf.Max(1, requiredAcceptedCount);
    }

    public string ApparelDefinitionId { get; }
    public ApparelSizeClass Size { get; }
    public ApparelModificationKind Modifications { get; }
    public ApparelMaterialSelectionPolicy MaterialPolicy { get; }
    public string ExactMaterialDefinitionId { get; }
    public CraftsmanshipQualityTier MinimumCraftsmanshipQuality { get; }
    public WorkerSelectionPolicySaveData WorkerPolicy { get; }
    public RejectedOutputDisposition RejectedDisposition { get; }
    public QualityRepeatLimitMode RepeatLimitMode { get; }
    public int MaximumAttempts { get; }
    public float WorkBudget { get; }
    public int RequiredAcceptedCount { get; }
}

public interface IApparelWorkOrderCommand
{
    bool CreateCraft(
        ApparelCraftOrderRequest request,
        out string orderId,
        out DomainFailure failure);
    bool CreateLaundry(
        IReadOnlyList<ItemInstanceId> items,
        bool powered,
        out string orderId,
        out DomainFailure failure);
    bool CreateDrying(
        IReadOnlyList<ItemInstanceId> items,
        out string orderId,
        out DomainFailure failure);
    bool CreateRepair(
        ItemInstanceId item,
        out string orderId,
        out DomainFailure failure);
    bool CreateAlteration(
        ItemInstanceId item,
        ApparelSizeClass size,
        ApparelModificationKind modifications,
        bool shortWardrobeOperation,
        out string orderId,
        out DomainFailure failure);
    bool ApplyWork(string orderId, float amount, out DomainFailure failure);
    bool ApplyWork(
        string orderId,
        CharacterActor worker,
        float amount,
        out DomainFailure failure);
    bool Cancel(string orderId);
    bool Cancel(string orderId, out DomainFailure failure);
}

public interface IApparelWorkOrderQuery
{
    int Version { get; }
    IReadOnlyList<ApparelWorkOrderSaveData> Orders { get; }
}

public interface IApparelWorkOrderPersistence
{
    ApparelWorkOrderSaveData[] CaptureOrders();
    ApparelWorkOrderTerminalStateSaveData[] CaptureTerminalStates();
    ApparelWorkOrderRestoreCandidate PrepareRestoreState(
        IEnumerable<ApparelWorkOrderSaveData> orders,
        IEnumerable<ApparelWorkOrderTerminalStateSaveData> terminalStates);
    void PublishRestoreState(ApparelWorkOrderRestoreCandidate candidate);
    IReadOnlyList<ApparelWorkOrderSaveData> PrepareRestoreOrders(
        IEnumerable<ApparelWorkOrderSaveData> source);
    void PublishRestoreOrders(IEnumerable<ApparelWorkOrderSaveData> source);
    void ResetOrders();
}

public sealed class ApparelWorkOrderRuntime :
    IApparelWorkOrderCommand,
    IApparelWorkOrderQuery,
    IApparelWorkOrderPersistence,
    IProductionApparelOrderTerminalEffectPort,
    IProductionApparelOrderSourceTerminalPort,
    IProductionApparelTerminalStateCheckpointGcPort,
    IDungeonRestoreTransactionParticipant
{
    private sealed class AuthorityState
    {
        internal List<ApparelWorkOrderSaveData> Orders { get; } = new();
        internal Dictionary<string, ApparelWorkOrderTerminalStateSaveData>
            TerminalStates { get; } = new(StringComparer.Ordinal);
        internal int NextSequence = 1;
        internal int Version;

        internal AuthorityState Clone()
        {
            AuthorityState clone = new()
            {
                NextSequence = NextSequence,
                Version = Version
            };
            clone.Orders.AddRange(Orders.Select(ApparelWorkOrderRuntime.Clone));
            foreach (KeyValuePair<string,
                         ApparelWorkOrderTerminalStateSaveData> pair in
                     TerminalStates)
            {
                clone.TerminalStates.Add(pair.Key, pair.Value?.Clone());
            }
            return clone;
        }
    }

    private const int MaximumBatch = 12;
    private const float GameSecondsPerHour = 7.5f;
    private const string RepairReasonCode =
        "apparel-repair-input-incorporated";
    private const string RestoreParticipantId =
        "226.world.apparel-work-orders";
    private static readonly float[] RetryIntervals = { .25f, .5f, 1f };

    private readonly IApparelDefinitionCatalog apparel;
    private readonly ITextileMaterialCatalog materials;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly IApparelPhysicalTransaction physicalTransactions;
    private readonly IProductionOutputMaximumMassRegistry outputMaximumMass;
    private readonly IProductionFacilityMutationEpochQuery facilityMutations;
    private readonly ILeasedItemReservationService leases;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IGameClock clock;
    private readonly IBalanceWorkCalculator balanceWorkCalculator;
    private readonly ICraftQualityResolver qualityResolver;
    private readonly IRunSeedProvider runSeedProvider;
    private readonly IWorkerNarrativeQualificationQuery narrativeQualifications;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly ExtremeCraftInspirationRuntime inspirationRuntime;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly ICharacterPerformanceQuery performance;
    private readonly CharacterWorkPerformanceContextResolver performanceContext;
    private AuthorityState authority = new();
    private ApparelWorkOrderRestoreCandidate stagedRestoreState;
    private AuthorityState previousRestoreState;
    private bool restoreActive;
    private bool restorePublished;
    private TerminalStateCheckpointGcCandidate activeTerminalStateGcCandidate;

    public ApparelWorkOrderRuntime(
        IApparelDefinitionCatalog apparel,
        ITextileMaterialCatalog materials,
        IWorldItemStackRuntime items,
        ILeasedItemReservationService leases,
        IFacilityCapabilityQuery facilities,
        IGameClock clock,
        IPhysicalItemBatchDispositionService batchDispositions,
        IApparelPhysicalTransaction physicalTransactions,
        IProductionOutputMaximumMassRegistry outputMaximumMass,
        IProductionFacilityMutationEpochQuery facilityMutations,
        IBalanceWorkCalculator balanceWorkCalculator = null,
        ICraftQualityResolver qualityResolver = null,
        IRunSeedProvider runSeedProvider = null,
        IWorkerNarrativeQualificationQuery narrativeQualifications = null,
        ICharacterWorldQuery characterWorld = null,
        ExtremeCraftInspirationRuntime inspirationRuntime = null,
        CharacterIdentityEventPublisher identityEvents = null,
        ICharacterPerformanceQuery performance = null,
        CharacterWorkPerformanceContextResolver performanceContext = null)
    {
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        this.materials = materials ?? throw new ArgumentNullException(nameof(materials));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
        this.physicalTransactions = physicalTransactions
            ?? throw new ArgumentNullException(nameof(physicalTransactions));
        this.outputMaximumMass = outputMaximumMass
            ?? throw new ArgumentNullException(nameof(outputMaximumMass));
        this.facilityMutations = facilityMutations
            ?? throw new ArgumentNullException(nameof(facilityMutations));
        this.leases = leases ?? throw new ArgumentNullException(nameof(leases));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.balanceWorkCalculator = balanceWorkCalculator;
        this.qualityResolver = qualityResolver
            ?? new DeterministicCraftQualityResolver();
        this.runSeedProvider = runSeedProvider;
        this.narrativeQualifications = narrativeQualifications;
        this.characterWorld = characterWorld;
        this.inspirationRuntime = inspirationRuntime;
        this.identityEvents = identityEvents;
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.performanceContext = performanceContext;
    }

    private List<ApparelWorkOrderSaveData> orders => authority.Orders;
    private Dictionary<string, ApparelWorkOrderTerminalStateSaveData>
        terminalStates => authority.TerminalStates;
    private int nextSequence
    {
        get => authority.NextSequence;
        set => authority.NextSequence = value;
    }

    public int Version
    {
        get => authority.Version;
        private set => authority.Version = value;
    }
    public IReadOnlyList<ApparelWorkOrderSaveData> Orders => orders;
    public string ParticipantId => RestoreParticipantId;

    public bool CreateCraft(
        ApparelCraftOrderRequest request,
        out string orderId,
        out DomainFailure failure)
    {
        orderId = string.Empty;
        failure = DomainFailure.None;
        if (!apparel.TryGet(request.ApparelDefinitionId, out ApparelDefinitionSO definition)
            || !Enum.IsDefined(typeof(ApparelSizeClass), request.Size)
            || (request.Modifications & ~definition.SupportedModifications) != 0)
        {
            failure = new DomainFailure(
                FailureCode.ApparelWorkOrderInvalid,
                request.ApparelDefinitionId);
            return false;
        }
        BuildableObject facility = ApparelTailoringFacilityEligibility
            .FindOperational(facilities)
            .FirstOrDefault();
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ApparelFacilityUnavailable);
            return false;
        }
        if (!TryRequireMutable(facility, out failure))
        {
            return false;
        }
        int requiredAmount = Mathf.Max(
            1,
            Mathf.CeilToInt(2f * definition.TailoringCoefficient));
        if (!TrySelectMaterial(
                definition,
                request,
                requiredAmount,
                out TextileMaterialDefinitionSO material,
                out List<WorldItemReservedStackQuantity> selected,
                out failure))
        {
            return false;
        }

        ApparelWorkOrderSaveData order = NewOrder(ApparelWorkOrderKind.Craft);
        order.apparelDefinitionId = definition.ApparelId;
        order.materialDefinitionId = material.MaterialId;
        order.materialPolicy = request.MaterialPolicy;
        order.minimumCraftsmanshipQuality = request.MinimumCraftsmanshipQuality;
        order.workerPolicy = request.WorkerPolicy.CloneNormalized();
        order.rejectedDisposition = request.RejectedDisposition;
        order.repeatLimitMode = request.RepeatLimitMode;
        order.maximumAttempts = request.MaximumAttempts;
        order.workBudget = request.WorkBudget;
        order.requiredAcceptedCount = request.RequiredAcceptedCount;
        order.targetSize = request.Size;
        order.targetModifications = request.Modifications;
        order.facilityInstanceId = facility.RequirePersistentInstanceId().Value;
        order.requiredWork = balanceWorkCalculator?.CalculateApparel(
                definition,
                material,
                request.Size,
                request.Modifications)
            ?? 22f * Mathf.Max(.5f, definition.TailoringCoefficient);
        order.craftWorkPerAttempt = order.requiredWork;
        order.qualityRoll = qualityResolver.Roll(
            unchecked((ulong)(uint)(runSeedProvider?.RunSeed ?? 1)),
            order.orderId,
            definition.ApparelId,
            0);
        order.qualityAttemptIndex = 0;
        if (!CanPotentiallyReachCraftQuality(order, definition, facility))
        {
            order.state = ApparelWorkOrderState.TargetCurrentlyUnreachable;
        }
        else if (!HasEligibleWorker(order.workerPolicy))
        {
            order.state = ApparelWorkOrderState.WaitingForEligibleWorker;
        }
        else if (!Reserve(order, selected, out failure))
        {
            orders.Remove(order);
            return false;
        }
        orderId = order.orderId;
        Version++;
        return true;
    }

    public bool CreateLaundry(
        IReadOnlyList<ItemInstanceId> targetItems,
        bool powered,
        out string orderId,
        out DomainFailure failure) =>
        CreateItemBatchOrder(
            ApparelWorkOrderKind.Laundry,
            targetItems,
            powered
                ? ResearchFacilityCommandKind.PoweredLaundry
                : ResearchFacilityCommandKind.HandLaundry,
            powered ? 4f : 12f,
            powered,
            out orderId,
            out failure);

    public bool CreateDrying(
        IReadOnlyList<ItemInstanceId> targetItems,
        out string orderId,
        out DomainFailure failure) =>
        CreateItemBatchOrder(
            ApparelWorkOrderKind.Drying,
            targetItems,
            ResearchFacilityCommandKind.IndoorDrying,
            24f,
            false,
            out orderId,
            out failure);

    public bool CreateRepair(
        ItemInstanceId target,
        out string orderId,
        out DomainFailure failure)
    {
        orderId = string.Empty;
        failure = DomainFailure.None;
        if (!TryFindApparel(target, out WorldItemStackSnapshot stack, out ApparelInstanceState state)
            || state.durability < 20f)
        {
            failure = new DomainFailure(FailureCode.ApparelWorkOrderInvalid, target.Value);
            return false;
        }
        BuildableObject facility = FirstFacility(ResearchFacilityCommandKind.ApparelRepair);
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ApparelFacilityUnavailable);
            return false;
        }
        if (!TryRequireMutable(facility, out failure))
        {
            return false;
        }
        List<WorldItemReservedStackQuantity> selected = new()
        {
            Reservation(stack, 1)
        };
        if (state.durability < 60f
            && (!TryAddMaterial("material:sewing-thread", 1, selected)
                || !TryAddMaterial("material:mending-scrap", 1, selected)))
        {
            failure = new DomainFailure(FailureCode.ApparelMaterialUnavailable);
            return false;
        }
        ApparelWorkOrderSaveData order = NewOrder(ApparelWorkOrderKind.Repair);
        order.targetItemInstanceId = target.Value;
        order.facilityInstanceId = facility.RequirePersistentInstanceId().Value;
        order.requiredWork = state.durability >= 60f ? 8f : 18f;
        if (!Reserve(order, selected, out failure))
        {
            orders.Remove(order);
            return false;
        }
        orderId = order.orderId;
        Version++;
        return true;
    }

    public bool CreateAlteration(
        ItemInstanceId target,
        ApparelSizeClass size,
        ApparelModificationKind modifications,
        bool shortWardrobeOperation,
        out string orderId,
        out DomainFailure failure)
    {
        orderId = string.Empty;
        failure = DomainFailure.None;
        if (!TryFindApparel(target, out WorldItemStackSnapshot stack, out ApparelInstanceState state)
            || !apparel.TryGet(state.apparelDefinitionId, out ApparelDefinitionSO definition)
            || (modifications & ~definition.SupportedModifications) != 0
            || (shortWardrobeOperation && (modifications & ~state.modifications) != 0))
        {
            failure = new DomainFailure(FailureCode.ApparelWorkOrderInvalid, target.Value);
            return false;
        }
        ResearchFacilityCommandKind command = shortWardrobeOperation
            ? ResearchFacilityCommandKind.DressingChange
            : ResearchFacilityCommandKind.ApparelTailoring;
        BuildableObject facility = FirstFacility(command);
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ApparelFacilityUnavailable);
            return false;
        }
        if (!TryRequireMutable(facility, out failure))
        {
            return false;
        }
        ApparelWorkOrderSaveData order = NewOrder(ApparelWorkOrderKind.Alteration);
        order.targetItemInstanceId = target.Value;
        order.targetSize = size;
        order.targetModifications = modifications;
        order.shortWardrobeOperation = shortWardrobeOperation;
        order.facilityInstanceId = facility.RequirePersistentInstanceId().Value;
        order.requiredWork = shortWardrobeOperation ? 3f : 14f;
        if (!Reserve(order, new[] { Reservation(stack, 1) }, out failure))
        {
            orders.Remove(order);
            return false;
        }
        orderId = order.orderId;
        Version++;
        return true;
    }

    public bool ApplyWork(string orderId, float amount, out DomainFailure failure) =>
        ApplyWork(orderId, null, amount, out failure);

    public bool ApplyWork(
        string orderId,
        CharacterActor worker,
        float amount,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        ApparelWorkOrderSaveData order = Find(orderId);
        if (order == null || amount <= 0f
            || order.state is ApparelWorkOrderState.Completed
                or ApparelWorkOrderState.Failed)
        {
            failure = new DomainFailure(FailureCode.ApparelWorkOrderInvalid, orderId);
            return false;
        }
        if (GameHour < order.nextRetryGameHour)
        {
            failure = new DomainFailure(FailureCode.ApparelMaterialUnavailable, order.orderId);
            return false;
        }
        if (order.state == ApparelWorkOrderState.WaitingForDispositionFinalization)
        {
            if (!Resolve(order, out failure))
            {
                return false;
            }
            if (order.state == ApparelWorkOrderState.WaitingForDispositionFinalization)
            {
                order.state = ApparelWorkOrderState.Completed;
                leases.Release(order.orderId);
            }
            Version++;
            return true;
        }
        bool terminalOutputRetry =
            order.state == ApparelWorkOrderState.WaitingForOutputSpace
            && order.completedWork + 0.0001f
                >= Mathf.Max(0f, order.requiredWork);
        if (!terminalOutputRetry && !TryRequireMutable(order, out failure))
        {
            return false;
        }
        if (order.kind == ApparelWorkOrderKind.Craft
            && apparel.TryGet(
                order.apparelDefinitionId,
                out ApparelDefinitionSO craftDefinition)
            && TryGetFacility(order, out BuildableObject craftFacility))
        {
            if (!CanPotentiallyReachCraftQuality(
                    order,
                    craftDefinition,
                    craftFacility,
                    worker))
            {
                leases.Release(order.orderId);
                order.state = ApparelWorkOrderState.TargetCurrentlyUnreachable;
                failure = new DomainFailure(
                    FailureCode.QualityTargetUnreachable,
                    order.orderId);
                return false;
            }
            if (!HasEligibleWorker(order.workerPolicy, worker))
            {
                leases.Release(order.orderId);
                order.state = ApparelWorkOrderState.WaitingForEligibleWorker;
                failure = new DomainFailure(FailureCode.WorkOrderWorkerIneligible);
                return false;
            }
        }
        if (worker != null
            && !WorkerSelectionPolicyRules.IsEligible(
                order.workerPolicy,
                worker,
                narrativeQualifications,
                out string workerFailure))
        {
            leases.Release(order.orderId);
            order.state = ApparelWorkOrderState.WaitingForEligibleWorker;
            failure = new DomainFailure(
                FailureCode.WorkOrderWorkerIneligible,
                workerFailure);
            return false;
        }
        if (worker == null
            && order.workerPolicy?.mode != WorkerSelectionMode.Anyone)
        {
            leases.Release(order.orderId);
            order.state = ApparelWorkOrderState.WaitingForEligibleWorker;
            failure = new DomainFailure(FailureCode.WorkOrderWorkerIneligible);
            return false;
        }
        if (!FacilityStillOperational(order))
        {
            failure = new DomainFailure(FailureCode.ApparelFacilityUnavailable);
            ReturnToWaiting(order, failure);
            return false;
        }
        if (!order.dismantlingRejectedOutput
            && !TryEnsureReservation(order, out failure))
        {
            ReturnToWaiting(order, failure);
            return false;
        }
        order.state = ApparelWorkOrderState.InProgress;
        float acceptedWork = Mathf.Min(
            amount,
            Mathf.Max(0f, order.requiredWork - order.completedWork));
        order.completedWork = Mathf.Min(
            order.requiredWork,
            order.completedWork + acceptedWork);
        if (worker != null && acceptedWork > 0f)
        {
            CraftContributionAccumulator contributions = new(order.contributions);
            contributions.Add(
                worker.Identity.PersistentId,
                acceptedWork,
                GetApparelQualitySkill(worker, order));
            order.contributions = contributions.Capture();
            order.lastWorkerCharacterId = worker.Identity.PersistentId?.Trim() ?? string.Empty;
        }
        if (order.completedWork < order.requiredWork)
        {
            Version++;
            return true;
        }
        if (!Resolve(order, out failure))
        {
            if (order.state is not (ApparelWorkOrderState.WaitingForOutputSpace
                    or ApparelWorkOrderState.WaitingForDispositionFinalization
                    or ApparelWorkOrderState.Failed))
            {
                ReturnToWaiting(order, failure);
            }
            return false;
        }
        if (order.state == ApparelWorkOrderState.InProgress)
        {
            order.state = ApparelWorkOrderState.Completed;
            leases.Release(order.orderId);
        }
        Version++;
        return true;
    }

    public bool Cancel(string orderId) => Cancel(orderId, out _);

    public bool Cancel(string orderId, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        ApparelWorkOrderSaveData order = Find(orderId);
        if (order == null)
        {
            failure = new DomainFailure(
                FailureCode.ApparelWorkOrderInvalid,
                orderId ?? string.Empty);
            return false;
        }
        // Once a durable physical receipt owns either rejected-output recovery
        // or repair material disposition, removing the order would orphan that
        // receipt and its exact mass. Cancellation is legal only after the
        // physical obligation has reached its terminal acknowledgement.
        if (order.dismantlingRejectedOutput
            || order.repairCommitPhase != ApparelRepairCommitPhase.None)
        {
            string operationId = order.dismantlingRejectedOutput
                ? order.rejectedDismantleOperationId
                : order.repairOperationId;
            string commitId = order.dismantlingRejectedOutput
                ? order.rejectedDismantleCommitId
                : order.repairCommitId;
            failure = new DomainFailure(
                FailureCode.ApparelRecoveryDeferred,
                order.orderId ?? string.Empty,
                operationId ?? string.Empty,
                commitId ?? string.Empty);
            return false;
        }
        if (!TryRequireMutable(order, out failure))
        {
            return false;
        }
        leases.Release(order.orderId);
        orders.Remove(order);
        Version++;
        return true;
    }

    public ApparelWorkOrderSaveData[] CaptureOrders() => orders
        .Where(value => value.state != ApparelWorkOrderState.Completed)
        .OrderBy(value => value.orderId, StringComparer.Ordinal)
        .Select(Clone)
        .ToArray();

    public ApparelWorkOrderTerminalStateSaveData[] CaptureTerminalStates() =>
        terminalStates.Values
            .OrderBy(value => value.sourceOrder.orderId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();

    public ApparelWorkOrderRestoreCandidate PrepareRestoreState(
        IEnumerable<ApparelWorkOrderSaveData> sourceOrders,
        IEnumerable<ApparelWorkOrderTerminalStateSaveData> sourceTerminalStates)
    {
        ApparelWorkOrderSaveData[] rawOrders = (sourceOrders
                ?? Enumerable.Empty<ApparelWorkOrderSaveData>())
            .Select(Clone)
            .ToArray();
        IReadOnlyList<ApparelWorkOrderSaveData> restoredOrders =
            PrepareRestoreOrders(rawOrders);
        List<ApparelWorkOrderTerminalStateSaveData> restoredTerminalStates =
            (sourceTerminalStates
                ?? Enumerable.Empty<ApparelWorkOrderTerminalStateSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.sourceOrder?.orderId, StringComparer.Ordinal)
            .ToList();
        ValidateTerminalStateRows(rawOrders, restoredTerminalStates);
        Dictionary<string, ApparelWorkOrderSaveData> frozenLive =
            restoredTerminalStates
                .Where(value => value.sourceTerminalReceipt == null)
                .ToDictionary(
                    value => value.sourceOrder.orderId,
                    value => Clone(value.sourceOrder),
                    StringComparer.Ordinal);
        restoredOrders = restoredOrders.Select(value =>
                frozenLive.TryGetValue(value.orderId, out var frozen)
                    ? Clone(frozen)
                    : Clone(value))
            .ToArray();
        ValidateTerminalStateRows(restoredOrders, restoredTerminalStates);
        return new ApparelWorkOrderRestoreCandidate(
            restoredOrders,
            restoredTerminalStates);
    }

    public IReadOnlyList<ApparelWorkOrderSaveData> PrepareRestoreOrders(
        IEnumerable<ApparelWorkOrderSaveData> source)
    {
        List<ApparelWorkOrderSaveData> restored = (source
                ?? Enumerable.Empty<ApparelWorkOrderSaveData>())
            .Select(Clone)
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToList();
        if (restored.Any(value => string.IsNullOrWhiteSpace(value.orderId)
                || value.requiredWork <= 0f
                || value.completedWork < 0f
                || value.completedWork > value.requiredWork
                || !Enum.IsDefined(typeof(ApparelWorkOrderKind), value.kind)
                || !Enum.IsDefined(typeof(ApparelWorkOrderState), value.state)
                || !Enum.IsDefined(
                    typeof(ApparelRepairCommitPhase),
                    value.repairCommitPhase)
                || !Enum.IsDefined(
                    typeof(CraftsmanshipQualityTier),
                    value.minimumCraftsmanshipQuality)
                || !Enum.IsDefined(
                    typeof(RejectedOutputDisposition),
                    value.rejectedDisposition)
                || !Enum.IsDefined(
                    typeof(QualityRepeatLimitMode),
                    value.repeatLimitMode)
                || value.maximumAttempts <= 0
                || value.requiredAcceptedCount <= 0
                || value.acceptedCount < 0
                || value.acceptedCount > value.requiredAcceptedCount
                || value.consumedWork < 0f
                || value.rejectedMaterialSpawned < 0
                || value.rejectedMaterialSpawned > value.rejectedMaterialAmount
                || !ApparelRejectedDismantleOutbox.ValidateOwnerShape(
                    value,
                    out _)
                || !ApparelPhysicalTransaction.ValidateCraftOwnerShape(
                    value,
                    out _)
                || !TryValidateMaximumMassProof(value, out _)
                || (value.kind == ApparelWorkOrderKind.Craft
                    && (value.qualityRoll == null
                        || value.qualityRoll.attemptIndex
                            != value.qualityAttemptIndex))
                || !ValidateRepairPendingShape(value))
            || restored.Select(value => value.orderId)
                .Distinct(StringComparer.Ordinal).Count() != restored.Count)
        {
            throw new InvalidOperationException("V23 apparel work-order payload is invalid.");
        }
        foreach (ApparelWorkOrderSaveData order in restored)
        {
            if (order.kind == ApparelWorkOrderKind.Craft
                && order.craftOutputCapability is { IsEmpty: false })
            {
                DomainFailure capabilityFailure = new(
                    FailureCode.ProductionOutputUnavailable,
                    order.apparelDefinitionId,
                    "apparel-definition-missing");
                bool capabilityValid = apparel.TryGet(
                        order.apparelDefinitionId,
                        out ApparelDefinitionSO definition)
                    && physicalTransactions.TryValidateCraftOutputCapability(
                        order,
                        definition.PhysicalItemId,
                        out capabilityFailure);
                if (!capabilityValid)
                {
                    throw new InvalidOperationException(
                        "V27 apparel output capability is invalid: "
                        + capabilityFailure.ToString());
                }
            }
            order.state = order.repairCommitPhase == ApparelRepairCommitPhase.None
                ? ApparelWorkOrderState.NeedsRevalidation
                : ApparelWorkOrderState.WaitingForDispositionFinalization;
            order.nextRetryGameHour = 0f;
        }
        return restored;
    }

    public void PublishRestoreOrders(IEnumerable<ApparelWorkOrderSaveData> source)
    {
        if (terminalStates.Count != 0)
        {
            throw new InvalidOperationException(
                "Order-only apparel restore cannot overwrite terminal authority.");
        }
        PublishRestoreState(new ApparelWorkOrderRestoreCandidate(
            (source ?? Enumerable.Empty<ApparelWorkOrderSaveData>())
                .Select(Clone)
                .ToArray(),
            Array.Empty<ApparelWorkOrderTerminalStateSaveData>()));
    }

    public void PublishRestoreState(ApparelWorkOrderRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        ApparelWorkOrderRestoreCandidate replacement =
            new ApparelWorkOrderRestoreCandidate(
                candidate.Orders.Select(Clone).ToArray(),
                candidate.TerminalStates.Select(value => value.Clone()).ToArray());
        ValidateTerminalStateRows(
            replacement.Orders,
            replacement.TerminalStates);
        if (restoreActive)
        {
            if (stagedRestoreState != null)
            {
                throw new InvalidOperationException(
                    "Apparel work-order restore candidate was staged more than once.");
            }
            stagedRestoreState = replacement;
            return;
        }
        ReplaceAuthority(replacement, true);
    }

    public IReadOnlyList<ProductionApparelOrderTerminalEffectReceipt>
        CaptureTerminalEffectReceipts() => terminalStates.Values
        .Select(value => value.terminalEffectReceipt)
        .Where(value => value != null)
        .OrderBy(value => value.commitId, StringComparer.Ordinal)
        .Select(value => value.Clone())
        .ToArray();

    public bool TryCaptureTerminalEffectReceipt(
        string commitId,
        out ProductionApparelOrderTerminalEffectReceipt receipt)
    {
        receipt = null;
        ProductionApparelOrderTerminalEffectReceipt[] matches = terminalStates
            .Values.Select(value => value.terminalEffectReceipt)
            .Where(value => value != null && string.Equals(
                value.commitId,
                commitId,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            return false;
        receipt = matches[0].Clone();
        return true;
    }

    public bool TryCaptureLiveOrder(
        string orderId,
        out ApparelWorkOrderSaveData sourceOrder,
        out string failureReason)
    {
        sourceOrder = null;
        failureReason = string.Empty;
        ApparelWorkOrderSaveData[] matches = orders.Where(value => value != null
                && value.state != ApparelWorkOrderState.Completed
                && string.Equals(value.orderId, orderId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            failureReason = matches.Length == 0
                ? "production-apparel-terminal-source-order-missing"
                : "production-apparel-terminal-source-order-duplicate";
            return false;
        }
        sourceOrder = Clone(matches[0]);
        return true;
    }

    public IReadOnlyList<ProductionApparelOrderSourceTerminalReceipt>
        CaptureSourceTerminalReceipts() => terminalStates.Values
        .Select(value => value.sourceTerminalReceipt)
        .Where(value => value != null)
        .OrderBy(value => value.commitId, StringComparer.Ordinal)
        .Select(value => value.Clone())
        .ToArray();

    public bool TryCaptureSourceTerminalReceipt(
        string commitId,
        out ProductionApparelOrderSourceTerminalReceipt receipt)
    {
        receipt = null;
        ProductionApparelOrderSourceTerminalReceipt[] matches = terminalStates
            .Values.Select(value => value.sourceTerminalReceipt)
            .Where(value => value != null && string.Equals(
                value.commitId,
                commitId,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            return false;
        receipt = matches[0].Clone();
        return true;
    }

    public bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData> producers,
        out IProductionApparelTerminalStateCheckpointGcCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (activeTerminalStateGcCandidate != null)
        {
            failureReason =
                "production-apparel-terminal-state-gc-already-active";
            return false;
        }

        ProductionApparelOrderTerminalDrainSaveData[] ordered = (producers
                ?? Array.Empty<ProductionApparelOrderTerminalDrainSaveData>())
            .OrderBy(value => value?.orderId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.orderId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            failureReason =
                "production-apparel-terminal-state-gc-producer-invalid";
            return false;
        }

        List<ApparelWorkOrderTerminalStateSaveData> rows = new(ordered.Length);
        foreach (ProductionApparelOrderTerminalDrainSaveData producer in ordered)
        {
            if (producer.phase != ProductionApparelOrderTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc
                || producer.terminalEffectReceipt == null
                || producer.sourceTerminalReceipt == null
                || !terminalStates.TryGetValue(
                    producer.orderId,
                    out ApparelWorkOrderTerminalStateSaveData row)
                || orders.Any(order => order != null
                    && string.Equals(
                        order.orderId,
                        producer.orderId,
                        StringComparison.Ordinal))
                || !TerminalStateMatchesProducer(row, producer))
            {
                failureReason =
                    "production-apparel-terminal-state-gc-row-missing-or-conflicting:"
                    + (producer.orderId ?? string.Empty);
                return false;
            }
            rows.Add(row.Clone());
        }

        activeTerminalStateGcCandidate = new TerminalStateCheckpointGcCandidate(
            Version,
            rows);
        candidate = activeTerminalStateGcCandidate;
        return true;
    }

    public bool TryPublishCheckpointGarbageCollection(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        TerminalStateCheckpointGcCandidate exact = RequireTerminalStateGcCandidate(
            candidate);
        if (exact.Published)
            return true;
        if (Version != exact.ExpectedVersion
            || exact.Rows.Any(row =>
                !terminalStates.TryGetValue(
                    row.sourceOrder.orderId,
                    out ApparelWorkOrderTerminalStateSaveData current)
                || !TerminalStateEquals(current, row)))
        {
            failureReason =
                "production-apparel-terminal-state-gc-live-authority-changed";
            return false;
        }

        if (exact.Rows.Count > 0)
        {
            AuthorityState next = authority.Clone();
            foreach (ApparelWorkOrderTerminalStateSaveData row in exact.Rows)
                next.TerminalStates.Remove(row.sourceOrder.orderId);
            next.Version = checked(next.Version + 1);
            authority = next;
        }
        exact.Published = true;
        exact.PublishedVersion = Version;
        return true;
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate)
    {
        TerminalStateCheckpointGcCandidate exact = RequireTerminalStateGcCandidate(
            candidate);
        if (!exact.Published)
            return;
        if (Version != exact.PublishedVersion
            || exact.Rows.Any(row => terminalStates.ContainsKey(
                row.sourceOrder.orderId)))
        {
            throw new InvalidOperationException(
                "Production apparel terminal-state GC rollback observed authority drift.");
        }

        if (exact.Rows.Count > 0)
        {
            AuthorityState restored = authority.Clone();
            foreach (ApparelWorkOrderTerminalStateSaveData row in exact.Rows)
            {
                restored.TerminalStates.Add(
                    row.sourceOrder.orderId,
                    row.Clone());
            }
            restored.Version = exact.ExpectedVersion;
            authority = restored;
        }
        exact.Published = false;
        exact.PublishedVersion = exact.ExpectedVersion;
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate)
    {
        RequireTerminalStateGcCandidate(candidate);
        activeTerminalStateGcCandidate = null;
    }

    [GameplayInternalOnly(
        "Publishes one apparel terminal-effect receipt in the order aggregate before source removal.",
        "Apparel destructive terminal drain producer only")]
    public ProductionApparelOrderTerminalEffectApplyResult TryCommitTerminalEffect(
        ProductionApparelOrderTerminalEffectReceipt expectedReceipt,
        ProductionApparelOrderPendingEffectIdentity pendingEffect)
    {
        string sourceFailure = string.Empty;
        if (expectedReceipt == null
            || !TryCaptureLiveOrder(
                expectedReceipt.orderId,
                out ApparelWorkOrderSaveData source,
                out sourceFailure))
        {
            return EffectConflict(
                "production-apparel-terminal-effect-source-conflict:"
                + sourceFailure);
        }
        string sourceFingerprint =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceOrderFingerprint(source);
        string effectFailure = string.Empty;
        if (!ProductionApparelOrderTerminalDrainCanonical
                .TryCreatePendingEffectIdentity(
                    source,
                    out ProductionApparelOrderPendingEffectIdentity actualEffect,
                    out effectFailure)
            || !PendingEffectEquals(actualEffect, pendingEffect)
            || !ProductionApparelOrderTerminalDrainCanonical.EffectReceiptEquals(
                expectedReceipt,
                ProductionApparelOrderTerminalDrainCanonical
                    .CreateTerminalEffectReceipt(
                        expectedReceipt.stepOperationId,
                        source,
                        sourceFingerprint,
                        actualEffect)))
        {
            return EffectConflict(
                "production-apparel-terminal-effect-request-conflict:"
                + effectFailure);
        }

        if (terminalStates.TryGetValue(
                source.orderId,
                out ApparelWorkOrderTerminalStateSaveData existing))
        {
            return ProductionApparelOrderTerminalDrainCanonical
                .EffectReceiptEquals(
                    existing.terminalEffectReceipt,
                    expectedReceipt)
                && PendingEffectEquals(existing.pendingEffect, actualEffect)
                ? new ProductionApparelOrderTerminalEffectApplyResult(
                    ProductionApparelOrderTerminalDrainStatus.Replay,
                    existing.terminalEffectReceipt,
                    string.Empty)
                : EffectConflict(
                    "production-apparel-terminal-effect-row-conflict");
        }
        if (terminalStates.Values.Any(value => string.Equals(
                value.terminalEffectReceipt?.commitId,
                expectedReceipt.commitId,
                StringComparison.Ordinal)))
        {
            return EffectConflict(
                "production-apparel-terminal-effect-commit-duplicate");
        }

        AuthorityState next = authority.Clone();
        next.TerminalStates.Add(source.orderId, new()
        {
            sourceOrder = Clone(source),
            sourceOrderFingerprint = sourceFingerprint,
            pendingEffect = actualEffect?.Clone(),
            terminalEffectReceipt = expectedReceipt.Clone()
        });
        next.Version = checked(next.Version + 1);
        authority = next;
        return new ProductionApparelOrderTerminalEffectApplyResult(
            ProductionApparelOrderTerminalDrainStatus.Applied,
            expectedReceipt,
            string.Empty);
    }

    [GameplayInternalOnly(
        "Removes one exact frozen apparel order and publishes its source-terminal receipt in one authority swap.",
        "Apparel destructive terminal drain producer only")]
    public ProductionApparelOrderSourceTerminalApplyResult TryCommitSourceTerminal(
        ProductionApparelOrderSourceTerminalReceipt expectedReceipt)
    {
        if (expectedReceipt == null
            || !terminalStates.TryGetValue(
                expectedReceipt.orderId,
                out ApparelWorkOrderTerminalStateSaveData terminal)
            || terminal.terminalEffectReceipt == null)
        {
            return SourceConflict(
                "production-apparel-source-terminal-effect-missing");
        }
        ProductionApparelOrderSourceTerminalReceipt canonical =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceTerminalReceipt(
                    expectedReceipt.stepOperationId,
                    terminal.sourceOrder,
                    terminal.sourceOrderFingerprint,
                    terminal.terminalEffectReceipt.receiptFingerprint);
        if (!ProductionApparelOrderTerminalDrainCanonical.SourceReceiptEquals(
                expectedReceipt,
                canonical))
        {
            return SourceConflict(
                "production-apparel-source-terminal-request-conflict");
        }
        ApparelWorkOrderSaveData[] live = orders.Where(value => value != null
                && string.Equals(
                    value.orderId,
                    expectedReceipt.orderId,
                    StringComparison.Ordinal))
            .ToArray();
        if (terminal.sourceTerminalReceipt != null)
        {
            return live.Length == 0
                && ProductionApparelOrderTerminalDrainCanonical
                    .SourceReceiptEquals(
                        terminal.sourceTerminalReceipt,
                        expectedReceipt)
                ? new ProductionApparelOrderSourceTerminalApplyResult(
                    ProductionApparelOrderTerminalDrainStatus.Replay,
                    terminal.sourceTerminalReceipt,
                    string.Empty)
                : SourceConflict(
                    "production-apparel-source-terminal-replay-conflict");
        }
        if (live.Length != 1
            || !string.Equals(
                ProductionApparelOrderTerminalDrainCanonical
                    .CreateSourceOrderFingerprint(live[0]),
                terminal.sourceOrderFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                JsonUtility.ToJson(live[0]),
                JsonUtility.ToJson(terminal.sourceOrder),
                StringComparison.Ordinal)
            || terminalStates.Values.Any(value => string.Equals(
                value.sourceTerminalReceipt?.commitId,
                expectedReceipt.commitId,
                StringComparison.Ordinal)))
        {
            return SourceConflict(
                "production-apparel-source-terminal-live-source-conflict");
        }

        AuthorityState next = authority.Clone();
        next.Orders.RemoveAll(value => string.Equals(
            value.orderId,
            expectedReceipt.orderId,
            StringComparison.Ordinal));
        next.TerminalStates[expectedReceipt.orderId].sourceTerminalReceipt =
            expectedReceipt.Clone();
        next.Version = checked(next.Version + 1);
        authority = next;
        return new ProductionApparelOrderSourceTerminalApplyResult(
            ProductionApparelOrderTerminalDrainStatus.Applied,
            expectedReceipt,
            string.Empty);
    }

    public void ResetOrders()
    {
        foreach (ApparelWorkOrderSaveData order in orders)
        {
            leases.Release(order.orderId);
        }
        authority = new AuthorityState
        {
            Version = checked(Version + 1)
        };
    }

    public void BeginRestoreCandidate()
    {
        if (restoreActive)
        {
            throw new InvalidOperationException(
                "Apparel work-order restore transaction is already active.");
        }
        restoreActive = true;
        restorePublished = false;
        stagedRestoreState = null;
        previousRestoreState = authority;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreActive || stagedRestoreState == null)
        {
            throw new InvalidOperationException(
                "Apparel work-order restore candidate was not staged.");
        }

        ValidatePendingRepairJoins(stagedRestoreState.Orders);
        HashSet<string> terminalOrderIds = new(
            stagedRestoreState.TerminalStates.Select(value =>
                value.sourceOrder.orderId),
            StringComparer.Ordinal);
        foreach (ApparelWorkOrderSaveData order in stagedRestoreState.Orders
                     .Where(value => value.repairCommitPhase
                          != ApparelRepairCommitPhase.None
                          && !terminalOrderIds.Contains(value.orderId))
                     .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            if (!ResumePendingRepair(order, out DomainFailure failure))
            {
                throw new InvalidOperationException(
                    $"Apparel repair '{order.orderId}' could not be reconciled: {failure}");
            }
            order.state = ApparelWorkOrderState.Completed;
        }

        ReplaceAuthority(stagedRestoreState, false);
        foreach (ApparelWorkOrderSaveData completed in orders.Where(value =>
                     value.state == ApparelWorkOrderState.Completed))
        {
            leases.Release(completed.orderId);
        }
        restorePublished = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (!restoreActive)
        {
            return;
        }
        if (restorePublished && previousRestoreState != null)
        {
            authority = previousRestoreState;
        }
        ClearRestoreTransaction();
    }

    public void CompleteRestoreCandidate() => ClearRestoreTransaction();

    public void DiscardRestoreCandidate()
    {
        if (restorePublished)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        ClearRestoreTransaction();
    }

    private bool CreateItemBatchOrder(
        ApparelWorkOrderKind kind,
        IReadOnlyList<ItemInstanceId> targetItems,
        ResearchFacilityCommandKind command,
        float work,
        bool powered,
        out string orderId,
        out DomainFailure failure)
    {
        orderId = string.Empty;
        failure = DomainFailure.None;
        ItemInstanceId[] target = (targetItems ?? Array.Empty<ItemInstanceId>())
            .Where(value => value.IsValid)
            .Distinct()
            .Take(MaximumBatch + 1)
            .ToArray();
        if (target.Length == 0 || target.Length > MaximumBatch)
        {
            failure = new DomainFailure(FailureCode.ApparelWorkOrderInvalid);
            return false;
        }
        BuildableObject facility = FirstFacility(command);
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ApparelFacilityUnavailable);
            return false;
        }
        if (!TryRequireMutable(facility, out failure))
        {
            return false;
        }
        List<WorldItemReservedStackQuantity> selected = new(target.Length);
        foreach (ItemInstanceId item in target)
        {
            if (!TryFindApparel(item, out WorldItemStackSnapshot stack, out _))
            {
                failure = new DomainFailure(FailureCode.ApparelPhysicalItemMissing, item.Value);
                return false;
            }
            selected.Add(Reservation(stack, 1));
        }
        ApparelWorkOrderSaveData order = NewOrder(kind);
        order.targetItemInstanceIds = target.Select(value => value.Value).ToList();
        order.facilityInstanceId = facility.RequirePersistentInstanceId().Value;
        order.requiredWork = work;
        order.powered = powered;
        if (!Reserve(order, selected, out failure))
        {
            orders.Remove(order);
            return false;
        }
        orderId = order.orderId;
        Version++;
        return true;
    }

    private bool Resolve(ApparelWorkOrderSaveData order, out DomainFailure failure)
    {
        if (order.dismantlingRejectedOutput)
        {
            return ResolveRejectedApparelDismantle(order, out failure);
        }
        return order.kind switch
        {
            ApparelWorkOrderKind.Craft => ResolveCraft(order, out failure),
            ApparelWorkOrderKind.Laundry => ResolveBatchState(order, false, out failure),
            ApparelWorkOrderKind.Drying => ResolveBatchState(order, true, out failure),
            ApparelWorkOrderKind.Repair => ResolveRepair(order, out failure),
            ApparelWorkOrderKind.Alteration => ResolveAlteration(order, out failure),
            _ => Fail(out failure)
        };
    }

    private bool ResolveCraft(ApparelWorkOrderSaveData order, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!apparel.TryGet(order.apparelDefinitionId, out ApparelDefinitionSO definition)
            || !materials.TryGet(order.materialDefinitionId, out TextileMaterialDefinitionSO material)
            || !TryGetFacility(order, out BuildableObject facility))
        {
            failure = new DomainFailure(FailureCode.ApparelWorkOrderInvalid, order.orderId);
            return false;
        }
        CraftContributionAccumulator contribution = new(order.contributions);
        CraftQualityResolution quality = qualityResolver.Resolve(
            order.qualityRoll,
            contribution.WeightedRelevantSkill > 0f
                ? contribution.WeightedRelevantSkill
                : 50f,
            (facility.Craftsmanship.Score - 50f) * 0.08f,
            toolBonus: 0f,
            complexityPenalty: Mathf.Max(0f,
                definition.TailoringCoefficient - 1f) * 4f);
        CraftsmanshipQualityTier completedQuality = quality.Tier;
        MythicProvenanceSaveData mythicProvenance = null;
        string makerCharacterId = order.lastWorkerCharacterId?.Trim() ?? string.Empty;
        float totalContribution = order.contributions
            .Where(value => value != null)
            .Sum(value => Mathf.Max(0f, value.contributedWork));
        float makerContribution = order.contributions
            .Where(value => value != null && string.Equals(
                value.characterId,
                makerCharacterId,
                StringComparison.Ordinal))
            .Sum(value => Mathf.Max(0f, value.contributedWork));
        CharacterActor maker = characterWorld?.Characters.FirstOrDefault(actor =>
            actor != null && string.Equals(
                actor.Identity?.PersistentId,
                makerCharacterId,
                StringComparison.Ordinal));
        bool hasInspiration = ExtremeCraftInspirationRuntime.TryResolveRule(
            maker,
            out ExtremeCraftInspirationRule inspirationRule);
        if (hasInspiration
            && totalContribution > 0f
            && makerContribution / totalContribution + 0.0001f
                >= inspirationRule.minimumContributionShare
            && definition.AllowMythicInspiration)
        {
            ulong fixedRollHash = MythicCraftInspirationRules.ResolveFixedRollHash(
                unchecked((ulong)(uint)(runSeedProvider?.RunSeed ?? 1)),
                order.orderId,
                definition.ApparelId,
                order.qualityRoll?.attemptIndex ?? order.qualityAttemptIndex,
                makerCharacterId);
            if (MythicCraftInspirationRules.IsMythic(
                    fixedRollHash,
                    inspirationRule.mythicChance))
            {
                completedQuality = CraftsmanshipQualityTier.Mythic;
                mythicProvenance = new MythicProvenanceSaveData
                {
                    makerCharacterId = makerCharacterId,
                    sourceTraitId = MythicCraftInspirationRules.SourceTraitId,
                    originalQuality = quality.Tier,
                    fixedRollHash = fixedRollHash,
                    createdDay = Mathf.FloorToInt(clock.Time / GameCalendarRules.SecondsPerDay),
                    createdFacilityId = facility.RequirePersistentInstanceId().Value
                };
            }
        }
        ApparelInstanceState state = new()
        {
            apparelDefinitionId = definition.ApparelId,
            primaryMaterialId = material.MaterialId,
            craftsmanshipQuality = completedQuality,
            sourceKind = SourceKind(material.Tags),
            sourceDefinitionId = material.MaterialId,
            size = order.targetSize,
            modifications = order.targetModifications,
            durability = 100f,
            craftedAbsoluteDay = Mathf.FloorToInt(
                clock.Time / GameCalendarRules.SecondsPerDay),
            deterministicBatchHash = Hash(order.orderId),
            mythicProvenance = mythicProvenance
        };
        bool rejectedBelowMinimum = (int)completedQuality
            < (int)order.minimumCraftsmanshipQuality;
        bool markForSale = rejectedBelowMinimum
            && order.rejectedDisposition == RejectedOutputDisposition.MarkForSale
            && completedQuality != CraftsmanshipQualityTier.Mythic;
        ApparelPhysicalTransactionResult physical =
            physicalTransactions.ExecuteCraftOrResume(
                order,
                facility,
                definition.PhysicalItemId,
                ApparelItemStateCodec.Create(state),
                markForSale);
        if (!physical.IsCompleted)
        {
            order.state = physical.Status switch
            {
                ApparelPhysicalTransactionStatus.WaitingForOutputSpace =>
                    ApparelWorkOrderState.WaitingForOutputSpace,
                ApparelPhysicalTransactionStatus.PendingFinalization =>
                    ApparelWorkOrderState.WaitingForDispositionFinalization,
                ApparelPhysicalTransactionStatus.Conflict =>
                    ApparelWorkOrderState.Failed,
                _ => throw new ArgumentOutOfRangeException()
            };
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                order.orderId,
                physical.FailureReason);
            return false;
        }
        string stackId = physical.OutputStackId;
        if (hasInspiration)
        {
            inspirationRuntime?.RecordEligibleCompletion(
                maker,
                definition.ApparelId,
                completedQuality == CraftsmanshipQualityTier.Mythic,
                clock.Time);
        }
        if (identityEvents != null
            && CharacterPersistentIdentity.TryGet(
                maker,
                out CharacterId qualityMakerId))
        {
            identityEvents.Publish(new ProductQualityResolvedEvent(
                qualityMakerId,
                definition.ApparelId,
                completedQuality,
                order.qualityRoll?.attemptIndex
                    ?? order.qualityAttemptIndex,
                Mathf.FloorToInt(
                    clock.Time / GameCalendarRules.SecondsPerDay),
                rejectedBelowMinimum: (int)completedQuality
                    < (int)order.minimumCraftsmanshipQuality));
        }
        order.consumedWork += Mathf.Max(0f, order.craftWorkPerAttempt);
        if ((int)completedQuality >= (int)order.minimumCraftsmanshipQuality)
        {
            order.acceptedCount++;
            if (order.acceptedCount >= Mathf.Max(1, order.requiredAcceptedCount))
            {
                return true;
            }
            return PrepareNextCraftAttempt(order, definition, out failure);
        }

        if (HasReachedApparelRepeatLimit(order))
        {
            order.state = ApparelWorkOrderState.Failed;
            return true;
        }
        if (order.rejectedDisposition == RejectedOutputDisposition.AutoDismantle)
        {
            WorldItemStackSnapshot rejected = items.GetAllStacks()
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.StackId,
                        stackId,
                        StringComparison.Ordinal));
            if (rejected == null
                || string.IsNullOrWhiteSpace(rejected.ItemInstanceId))
            {
                failure = new DomainFailure(
                    FailureCode.ApparelPhysicalItemMissing,
                    stackId);
                return false;
            }
            order.dismantlingRejectedOutput = true;
            order.rejectedOutputConsumed = false;
            order.rejectedOutputStackId = stackId;
            order.rejectedOutputInstanceId = rejected.ItemInstanceId;
            float salvageYield = maker != null
                ? maker.GetDetailedStatMultiplier(
                    GameplayEffectTargetIds.SalvageYield)
                : 1f;
            order.rejectedMaterialAmount = Mathf.FloorToInt(
                order.materialStackAmounts.Sum()
                * 0.50f
                * Mathf.Max(0f, salvageYield));
            order.rejectedMaterialSpawned = 0;
            order.rejectedRecoveryItemId = material.PhysicalItemId;
            ApparelRejectedDismantleOutbox.Clear(order);
            order.requiredWork =
                ApparelCraftCycleMaximumAuthority.ResolveRejectedRecoveryWork(
                    order.craftWorkPerAttempt);
            order.completedWork = 0f;
            order.contributions.Clear();
            order.state = ApparelWorkOrderState.Ready;
            leases.Release(order.orderId);
            ApparelPhysicalTransaction.ClearCraftAttempt(order);
            return true;
        }
        return PrepareNextCraftAttempt(order, definition, out failure);
    }

    private bool ResolveRejectedApparelDismantle(
        ApparelWorkOrderSaveData order,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!apparel.TryGet(
                order.apparelDefinitionId,
                out ApparelDefinitionSO definition)
            || !materials.TryGet(
                order.materialDefinitionId,
                out TextileMaterialDefinitionSO material)
            || !TryGetFacility(order, out BuildableObject facility))
        {
            failure = new DomainFailure(
                FailureCode.ApparelWorkOrderInvalid,
                order.orderId);
            return false;
        }

        if (!string.Equals(
                order.rejectedRecoveryItemId,
                material.PhysicalItemId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                "apparel-rejected-recovery-item-authority-drift");
            return false;
        }
        ApparelPhysicalTransactionResult physical =
            physicalTransactions.ExecuteRejectedDismantleOrResume(
                order,
                facility,
                material.PhysicalItemId);
        if (!physical.IsCompleted)
        {
            order.state = physical.Status switch
            {
                ApparelPhysicalTransactionStatus.WaitingForOutputSpace =>
                    ApparelWorkOrderState.WaitingForOutputSpace,
                ApparelPhysicalTransactionStatus.PendingFinalization =>
                    ApparelWorkOrderState.WaitingForDispositionFinalization,
                ApparelPhysicalTransactionStatus.Conflict =>
                    ApparelWorkOrderState.Failed,
                _ => throw new ArgumentOutOfRangeException()
            };
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                physical.FailureReason);
            return false;
        }
        order.consumedWork += Mathf.Max(0f, order.requiredWork);
        order.dismantlingRejectedOutput = false;
        order.rejectedOutputStackId = string.Empty;
        order.rejectedOutputInstanceId = string.Empty;
        order.rejectedMaterialAmount = 0;
        order.rejectedMaterialSpawned = 0;
        order.rejectedRecoveryItemId = string.Empty;
        ApparelRejectedDismantleOutbox.Clear(order);
        return PrepareNextCraftAttempt(order, definition, out failure);
    }

    private bool PrepareNextCraftAttempt(
        ApparelWorkOrderSaveData order,
        ApparelDefinitionSO definition,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        ApparelPhysicalTransaction.ClearCraftAttempt(order);
        leases.Release(order.orderId);
        order.qualityAttemptIndex++;
        if (HasReachedApparelRepeatLimit(order))
        {
            order.state = ApparelWorkOrderState.Failed;
            return true;
        }
        order.qualityRoll = qualityResolver.Roll(
            unchecked((ulong)(uint)(runSeedProvider?.RunSeed ?? 1)),
            order.orderId,
            definition.ApparelId,
            order.qualityAttemptIndex);
        order.requiredWork = Mathf.Max(0.1f, order.craftWorkPerAttempt);
        order.completedWork = 0f;
        order.contributions.Clear();
        order.lastWorkerCharacterId = string.Empty;
        order.materialStackIds.Clear();
        order.materialStackAmounts.Clear();
        if (!TryRebuildSelection(
                order,
                out List<WorldItemReservedStackQuantity> selected,
                out failure)
            || !Reserve(order, selected, out failure))
        {
            order.state = ApparelWorkOrderState.WaitingForMaterials;
            return true;
        }
        order.state = ApparelWorkOrderState.Ready;
        return true;
    }

    private static bool HasReachedApparelRepeatLimit(
        ApparelWorkOrderSaveData order)
    {
        return order.repeatLimitMode == QualityRepeatLimitMode.SafeLimits
            && (order.qualityAttemptIndex + 1
                    >= Mathf.Max(1, order.maximumAttempts)
                || (order.workBudget > 0f
                    && order.consumedWork >= order.workBudget));
    }

    private bool ResolveBatchState(
        ApparelWorkOrderSaveData order,
        bool drying,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        List<(WorldItemStackSnapshot Stack, ApparelInstanceState Original)> targets = new();
        foreach (string id in order.targetItemInstanceIds)
        {
            if (!TryFindApparel((ItemInstanceId)id, out WorldItemStackSnapshot stack, out ApparelInstanceState state))
            {
                failure = new DomainFailure(FailureCode.ApparelPhysicalItemMissing, id);
                return false;
            }
            targets.Add((stack, state));
        }
        foreach ((WorldItemStackSnapshot stack, ApparelInstanceState original) in targets)
        {
            ApparelInstanceState changed = CloneState(original);
            if (drying || order.powered)
            {
                changed.moisture = 0f;
            }
            else
            {
                changed.moisture = 100f;
            }
            if (!drying)
            {
                changed.contamination = 0f;
            }
            if (!items.TrySetInstanceComponent(
                    stack.StackId,
                    ApparelItemStateCodec.Create(changed)))
            {
                foreach ((WorldItemStackSnapshot applied, ApparelInstanceState rollback) in targets)
                {
                    items.TrySetInstanceComponent(
                        applied.StackId,
                        ApparelItemStateCodec.Create(rollback));
                }
                failure = new DomainFailure(FailureCode.ApparelTransferFailed, stack.StackId);
                return false;
            }
        }
        return true;
    }

    private bool ResolveRepair(ApparelWorkOrderSaveData order, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order.repairCommitPhase != ApparelRepairCommitPhase.None)
        {
            return ResumePendingRepair(order, out failure);
        }
        if (!TryFindApparel(
                (ItemInstanceId)order.targetItemInstanceId,
                out WorldItemStackSnapshot stack,
                out ApparelInstanceState state))
        {
            failure = new DomainFailure(
                FailureCode.ApparelPhysicalItemMissing,
                order.targetItemInstanceId);
            return false;
        }
        ApparelInstanceState changed = CloneState(state);
        changed.durability = state.durability >= 60f
            ? Mathf.Min(100f, state.durability + 25f)
            : 70f;
        List<PhysicalItemTransformInput> inputs = BuildNonTargetInputs(
            order,
            stack.StackId);
        if (inputs.Count == 0)
        {
            if (!items.TrySetInstanceComponent(
                    stack.StackId,
                    ApparelItemStateCodec.Create(changed)))
            {
                failure = new DomainFailure(
                    FailureCode.ApparelTransferFailed,
                    stack.StackId);
                return false;
            }
            return true;
        }
        string operationId = RepairOperationId(order.orderId);
        // The order lease protects selection while work is in progress. The
        // pending disposition becomes the sole exact custody authority at the
        // terminal boundary, so release the lease before its atomic preflight.
        leases.Release(order.orderId);
        if (!batchDispositions.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                RepairReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure))
        {
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                commitFailure);
            return false;
        }
        order.repairCommitPhase = ApparelRepairCommitPhase.MaterialCommitted;
        order.repairOperationId = operationId;
        order.repairReasonCode = RepairReasonCode;
        order.repairCommitId = receipt.CommitId;
        order.repairSourceStackIds = receipt.SourceStackIds.ToList();
        order.repairInputQuantity = receipt.Quantity;
        order.repairInputMassGrams = receipt.InputMassGrams;
        order.repairTargetStackId = stack.StackId;
        order.repairOriginalStatePayload = CaptureApparelState(state);
        order.repairResolvedStatePayload = CaptureApparelState(changed);
        order.state = ApparelWorkOrderState.WaitingForDispositionFinalization;
        return ResumePendingRepair(order, out failure);
    }

    private bool ResumePendingRepair(
        ApparelWorkOrderSaveData order,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryValidatePendingRepairJoin(
                order,
                out WorldItemStackSnapshot stack,
                out ApparelInstanceState current,
                out ApparelInstanceState resolved,
                out string validationFailure))
        {
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                validationFailure);
            return false;
        }

        if (order.repairCommitPhase == ApparelRepairCommitPhase.MaterialCommitted)
        {
            if (!items.TrySetInstanceComponent(
                    stack.StackId,
                    ApparelItemStateCodec.Create(resolved)))
            {
                failure = new DomainFailure(
                    FailureCode.ApparelTransferFailed,
                    stack.StackId);
                return false;
            }
            order.repairCommitPhase = ApparelRepairCommitPhase.RepairApplied;
            current = resolved;
        }

        if (!string.Equals(
                CaptureApparelState(current),
                order.repairResolvedStatePayload,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                "apparel-repair-resolved-state-mismatch");
            return false;
        }
        if (!batchDispositions.Acknowledge(
                order.repairCommitId,
                out string acknowledgementFailure))
        {
            failure = new DomainFailure(
                FailureCode.ApparelTransferFailed,
                acknowledgementFailure);
            return false;
        }

        ClearRepairPending(order);
        return true;
    }

    private bool ResolveAlteration(ApparelWorkOrderSaveData order, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryFindApparel(
                (ItemInstanceId)order.targetItemInstanceId,
                out WorldItemStackSnapshot stack,
                out ApparelInstanceState state))
        {
            failure = new DomainFailure(
                FailureCode.ApparelPhysicalItemMissing,
                order.targetItemInstanceId);
            return false;
        }
        ApparelInstanceState changed = CloneState(state);
        if (order.shortWardrobeOperation)
        {
            // At a wardrobe, the target mask means which already-authored
            // openings are temporarily closed. It never cuts a new opening.
            changed.closedOpenings = order.targetModifications
                & changed.modifications;
        }
        else
        {
            changed.size = order.targetSize;
            changed.modifications = order.targetModifications;
            changed.closedOpenings &= changed.modifications;
        }
        if (!items.TrySetInstanceComponent(
                stack.StackId,
                ApparelItemStateCodec.Create(changed)))
        {
            failure = new DomainFailure(FailureCode.ApparelTransferFailed, stack.StackId);
            return false;
        }
        return true;
    }

    private bool TrySelectMaterial(
        ApparelDefinitionSO definition,
        ApparelCraftOrderRequest request,
        int amount,
        out TextileMaterialDefinitionSO material,
        out List<WorldItemReservedStackQuantity> selected,
        out DomainFailure failure)
    {
        material = null;
        selected = new List<WorldItemReservedStackQuantity>();
        failure = DomainFailure.None;
        IEnumerable<TextileMaterialDefinitionSO> allowed = materials.Definitions
            .Where(value => value != null
                && (value.Tags & definition.AllowedMaterialTags) != 0);
        if (request.MaterialPolicy == ApparelMaterialSelectionPolicy.ExactMaterial)
        {
            allowed = allowed.Where(value => string.Equals(
                value.MaterialId,
                request.ExactMaterialDefinitionId,
                StringComparison.Ordinal));
        }
        List<(TextileMaterialDefinitionSO Material, WorldItemStackSnapshot Stack)>
            candidates = new();
        foreach (TextileMaterialDefinitionSO candidateMaterial in allowed)
        {
            foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
            {
                if (stack == null || stack.AvailableQuantity <= 0 || stack.Forbidden
                    || !string.Equals(
                        stack.ItemId,
                        candidateMaterial.PhysicalItemId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                TextileBatchItemState.TryRead(
                    stack.Components,
                    out TextileConditionBand condition);
                if (condition == TextileConditionBand.Ready)
                {
                    candidates.Add((candidateMaterial, stack));
                }
            }
        }
        IOrderedEnumerable<(TextileMaterialDefinitionSO Material,
            WorldItemStackSnapshot Stack)> ordered =
            request.MaterialPolicy switch
            {
                ApparelMaterialSelectionPolicy.LowestHandlingDifficulty =>
                    candidates.OrderBy(value => value.Material.MaterialId,
                        StringComparer.Ordinal),
                ApparelMaterialSelectionPolicy.LowestCost => candidates
                    .OrderBy(value => value.Stack.UnitPrice),
                ApparelMaterialSelectionPolicy.HighestWarmth => candidates
                    .OrderByDescending(value => value.Material.Warmth),
                ApparelMaterialSelectionPolicy.LowestWeight => candidates
                    .OrderBy(value => value.Material.WeightMultiplier),
                ApparelMaterialSelectionPolicy.HighestDurability => candidates
                    .OrderByDescending(value => value.Material.Durability),
                _ => candidates.OrderBy(value => value.Material.MaterialId, StringComparer.Ordinal)
            };
        List<(TextileMaterialDefinitionSO Material, WorldItemStackSnapshot Stack)>
            sorted = ordered
            .ThenBy(value => value.Stack.StackId, StringComparer.Ordinal)
            .ToList();
        foreach (IGrouping<string, (TextileMaterialDefinitionSO Material,
                     WorldItemStackSnapshot Stack)> group in
                 sorted.GroupBy(value => value.Material.MaterialId, StringComparer.Ordinal))
        {
            int remaining = amount;
            List<WorldItemReservedStackQuantity> proposed = new();
            foreach (var candidate in group)
            {
                int take = Math.Min(remaining, candidate.Stack.Quantity);
                if (take > 0)
                {
                    proposed.Add(Reservation(candidate.Stack, take));
                    remaining -= take;
                }
                if (remaining == 0) break;
            }
            if (remaining == 0)
            {
                material = group.First().Material;
                selected = proposed;
                return true;
            }
        }
        failure = new DomainFailure(
            FailureCode.ApparelMaterialUnavailable,
            definition.ApparelId);
        return false;
    }

    private bool TryAddMaterial(
        string itemId,
        int amount,
        ICollection<WorldItemReservedStackQuantity> destination)
    {
        int remaining = amount;
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                     .Where(value => value != null
                         && value.AvailableQuantity > 0
                         && !value.Forbidden
                         && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            int take = Math.Min(remaining, stack.Quantity);
            if (take > 0)
            {
                destination.Add(Reservation(stack, take));
                remaining -= take;
            }
            if (remaining == 0) return true;
        }
        return false;
    }

    private bool Reserve(
        ApparelWorkOrderSaveData order,
        IReadOnlyList<WorldItemReservedStackQuantity> selected,
        out DomainFailure failure)
    {
        if (!leases.TryReserveBatch(order.orderId, selected, out _, out failure))
        {
            order.state = ApparelWorkOrderState.WaitingForMaterials;
            return false;
        }
        order.materialStackIds = selected.Select(value => value.StackId).ToList();
        order.materialStackAmounts = selected.Select(value => value.Quantity).ToList();
        order.state = ApparelWorkOrderState.Ready;
        return true;
    }

    private void ReturnToWaiting(
        ApparelWorkOrderSaveData order,
        DomainFailure failure)
    {
        leases.Release(order.orderId);
        order.state = ApparelWorkOrderState.WaitingForMaterials;
        order.completedWork = 0f;
        int retryIndex = Mathf.Clamp(
            order.retryCount,
            0,
            RetryIntervals.Length - 1);
        order.nextRetryGameHour = GameHour + RetryIntervals[retryIndex];
        order.retryCount = Mathf.Min(
            RetryIntervals.Length,
            order.retryCount + 1);
        Version++;
    }

    private bool TryEnsureReservation(
        ApparelWorkOrderSaveData order,
        out DomainFailure failure)
    {
        if (order.state != ApparelWorkOrderState.NeedsRevalidation
            && order.state != ApparelWorkOrderState.WaitingForMaterials
            && leases.Revalidate(order.orderId, true, out failure))
        {
            return true;
        }

        leases.Release(order.orderId);
        if (TryRestoreSavedSelection(order, out List<WorldItemReservedStackQuantity> selected)
            && Reserve(order, selected, out failure))
        {
            return true;
        }

        leases.Release(order.orderId);
        if (!TryRebuildSelection(order, out selected, out failure))
        {
            return false;
        }
        return Reserve(order, selected, out failure);
    }

    private bool TryRestoreSavedSelection(
        ApparelWorkOrderSaveData order,
        out List<WorldItemReservedStackQuantity> selected)
    {
        selected = new List<WorldItemReservedStackQuantity>(order.materialStackIds.Count);
        Dictionary<string, WorldItemStackSnapshot> current = items.GetAllStacks()
            .Where(value => value != null)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        List<PhysicalItemTransformInput> inputs = new();
        for (int index = 0; index < order.materialStackIds.Count; index++)
        {
            string stackId = order.materialStackIds[index];
            int amount = index < order.materialStackAmounts.Count
                ? order.materialStackAmounts[index]
                : 1;
            if (!current.TryGetValue(stackId, out WorldItemStackSnapshot stack)
                || stack.Quantity < amount
                || stack.Forbidden
                || !IsStillValidForOrder(order, stack))
            {
                return false;
            }
            selected.Add(Reservation(stack, amount));
        }
        return selected.Count > 0;
    }

    private bool IsStillValidForOrder(
        ApparelWorkOrderSaveData order,
        WorldItemStackSnapshot stack)
    {
        if (order.kind != ApparelWorkOrderKind.Craft)
        {
            return true;
        }
        if (!materials.TryGet(
                order.materialDefinitionId,
                out TextileMaterialDefinitionSO material)
            || !string.Equals(
                stack.ItemId,
                material.PhysicalItemId,
                StringComparison.Ordinal))
        {
            return false;
        }
        TextileBatchItemState.TryRead(
            stack.Components,
            out TextileConditionBand condition);
        return condition == TextileConditionBand.Ready;
    }

    private bool TryRebuildSelection(
        ApparelWorkOrderSaveData order,
        out List<WorldItemReservedStackQuantity> selected,
        out DomainFailure failure)
    {
        selected = new List<WorldItemReservedStackQuantity>();
        failure = DomainFailure.None;
        switch (order.kind)
        {
            case ApparelWorkOrderKind.Craft:
                if (!apparel.TryGet(
                        order.apparelDefinitionId,
                        out ApparelDefinitionSO definition))
                {
                    failure = new DomainFailure(
                        FailureCode.ApparelWorkOrderInvalid,
                        order.apparelDefinitionId);
                    return false;
                }
                int requiredAmount = Mathf.Max(
                    1,
                    Mathf.CeilToInt(2f * definition.TailoringCoefficient));
                ApparelCraftOrderRequest request = new(
                    order.apparelDefinitionId,
                    order.targetSize,
                    order.targetModifications,
                    order.materialPolicy,
                    order.materialPolicy == ApparelMaterialSelectionPolicy.ExactMaterial
                        ? order.materialDefinitionId
                        : string.Empty,
                    order.minimumCraftsmanshipQuality);
                if (!TrySelectMaterial(
                        definition,
                        request,
                        requiredAmount,
                        out TextileMaterialDefinitionSO material,
                        out selected,
                        out failure))
                {
                    return false;
                }
                order.materialDefinitionId = material.MaterialId;
                return true;

            case ApparelWorkOrderKind.Repair:
                if (!TryFindApparel(
                        (ItemInstanceId)order.targetItemInstanceId,
                        out WorldItemStackSnapshot repairStack,
                        out ApparelInstanceState repairState)
                    || repairState.durability < 20f)
                {
                    failure = new DomainFailure(
                        FailureCode.ApparelPhysicalItemMissing,
                        order.targetItemInstanceId);
                    return false;
                }
                selected.Add(Reservation(repairStack, 1));
                if (repairState.durability < 60f
                    && (!TryAddMaterial("material:sewing-thread", 1, selected)
                        || !TryAddMaterial("material:mending-scrap", 1, selected)))
                {
                    failure = new DomainFailure(
                        FailureCode.ApparelMaterialUnavailable,
                        order.orderId);
                    return false;
                }
                return true;

            case ApparelWorkOrderKind.Laundry:
            case ApparelWorkOrderKind.Drying:
                foreach (string itemId in order.targetItemInstanceIds)
                {
                    if (!TryFindApparel(
                            (ItemInstanceId)itemId,
                            out WorldItemStackSnapshot batchStack,
                            out _))
                    {
                        failure = new DomainFailure(
                            FailureCode.ApparelPhysicalItemMissing,
                            itemId);
                        return false;
                    }
                    selected.Add(Reservation(batchStack, 1));
                }
                return selected.Count > 0;

            case ApparelWorkOrderKind.Alteration:
                if (!TryFindApparel(
                        (ItemInstanceId)order.targetItemInstanceId,
                        out WorldItemStackSnapshot alterationStack,
                        out _))
                {
                    failure = new DomainFailure(
                        FailureCode.ApparelPhysicalItemMissing,
                        order.targetItemInstanceId);
                    return false;
                }
                selected.Add(Reservation(alterationStack, 1));
                return true;

            default:
                failure = new DomainFailure(
                    FailureCode.ApparelWorkOrderInvalid,
                    order.orderId);
                return false;
        }
    }

    internal static string BuildCraftMaterialOperationId(
        ApparelWorkOrderSaveData order)
    {
        if (order == null
            || order.kind != ApparelWorkOrderKind.Craft
            || string.IsNullOrEmpty(order.orderId)
            || !string.Equals(
                order.orderId,
                order.orderId.Trim(),
                StringComparison.Ordinal)
            || order.qualityAttemptIndex < 0)
        {
            throw new InvalidOperationException(
                "Apparel craft material operation authority is invalid.");
        }
        return "apparel-craft-material:"
            + order.orderId
            + ":"
            + order.qualityAttemptIndex.ToString(
                "D4",
                CultureInfo.InvariantCulture);
    }

    private List<PhysicalItemTransformInput> BuildNonTargetInputs(
        ApparelWorkOrderSaveData order,
        string excludedStackId)
    {
        List<PhysicalItemTransformInput> inputs = new();
        for (int index = 0; index < order.materialStackIds.Count; index++)
        {
            string stackId = order.materialStackIds[index];
            if (string.Equals(stackId, excludedStackId, StringComparison.Ordinal))
            {
                continue;
            }
            int amount = index < order.materialStackAmounts.Count
                ? order.materialStackAmounts[index]
                : 1;
            inputs.Add(new PhysicalItemTransformInput(stackId, amount));
        }
        return inputs;
    }

    private void ValidatePendingRepairJoins(
        IReadOnlyList<ApparelWorkOrderSaveData> candidate)
    {
        HashSet<string> operations = new(StringComparer.Ordinal);
        foreach (ApparelWorkOrderSaveData order in candidate.Where(value =>
                     value.repairCommitPhase != ApparelRepairCommitPhase.None))
        {
            if (!operations.Add(order.repairOperationId))
            {
                throw new InvalidOperationException(
                    $"Apparel repair restore join '{order.orderId}' has a duplicate operation.");
            }
            if (!TryValidatePendingRepairJoin(
                    order,
                    out _,
                    out _,
                    out _,
                    out string failure))
            {
                throw new InvalidOperationException(
                    $"Apparel repair restore join '{order.orderId}' is invalid: {failure}");
            }
        }
    }

    private bool TryValidatePendingRepairJoin(
        ApparelWorkOrderSaveData order,
        out WorldItemStackSnapshot stack,
        out ApparelInstanceState current,
        out ApparelInstanceState resolved,
        out string failure)
    {
        stack = null;
        current = null;
        resolved = null;
        failure = string.Empty;
        if (!ValidateRepairPendingShape(order)
            || !string.Equals(
                order.repairOperationId,
                RepairOperationId(order.orderId),
                StringComparison.Ordinal)
            || !batchDispositions.TryGetPending(
                order.repairOperationId,
                out PhysicalItemBatchDispositionReceipt receipt)
            || receipt.Kind != PhysicalItemDispositionKind.Transfer
            || !string.Equals(
                receipt.OperationId,
                order.repairOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                order.repairReasonCode,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.CommitId,
                order.repairCommitId,
                StringComparison.Ordinal)
            || receipt.Quantity != order.repairInputQuantity
            || receipt.InputMassGrams != order.repairInputMassGrams
            || !receipt.SourceStackIds.SequenceEqual(
                order.repairSourceStackIds,
                StringComparer.Ordinal)
            || !TryFindApparel(
                (ItemInstanceId)order.targetItemInstanceId,
                out stack,
                out current)
            || !string.Equals(
                stack.StackId,
                order.repairTargetStackId,
                StringComparison.Ordinal)
            || !TryReadCanonicalApparelState(
                order.repairOriginalStatePayload,
                out ApparelInstanceState original)
            || !TryReadCanonicalApparelState(
                order.repairResolvedStatePayload,
                out resolved))
        {
            failure = "apparel-repair-pending-authority-mismatch";
            return false;
        }

        string expected = order.repairCommitPhase switch
        {
            ApparelRepairCommitPhase.MaterialCommitted =>
                CaptureApparelState(original),
            ApparelRepairCommitPhase.RepairApplied =>
                CaptureApparelState(resolved),
            _ => string.Empty
        };
        if (!string.Equals(
                CaptureApparelState(current),
                expected,
                StringComparison.Ordinal))
        {
            failure = "apparel-repair-target-state-mismatch";
            return false;
        }
        return true;
    }

    private static bool ValidateRepairPendingShape(ApparelWorkOrderSaveData order)
    {
        bool any = order.repairCommitPhase != ApparelRepairCommitPhase.None
            || !string.IsNullOrEmpty(order.repairOperationId)
            || !string.IsNullOrEmpty(order.repairReasonCode)
            || !string.IsNullOrEmpty(order.repairCommitId)
            || order.repairSourceStackIds.Count > 0
            || order.repairInputQuantity != 0
            || order.repairInputMassGrams != 0L
            || !string.IsNullOrEmpty(order.repairTargetStackId)
            || !string.IsNullOrEmpty(order.repairOriginalStatePayload)
            || !string.IsNullOrEmpty(order.repairResolvedStatePayload);
        if (!any)
        {
            return true;
        }
        return order.kind == ApparelWorkOrderKind.Repair
            && order.state == ApparelWorkOrderState.WaitingForDispositionFinalization
            && order.repairCommitPhase is ApparelRepairCommitPhase.MaterialCommitted
                or ApparelRepairCommitPhase.RepairApplied
            && string.Equals(
                order.repairReasonCode,
                RepairReasonCode,
                StringComparison.Ordinal)
            && order.repairOperationId.Length > 0
            && order.repairCommitId.Length > 0
            && order.repairSourceStackIds.Count > 0
            && order.repairSourceStackIds.All(id => !string.IsNullOrWhiteSpace(id))
            && order.repairSourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == order.repairSourceStackIds.Count
            && order.repairInputQuantity > 0
            && order.repairInputMassGrams > 0L
            && order.repairTargetStackId.Length > 0
            && order.repairOriginalStatePayload.Length > 0
            && order.repairResolvedStatePayload.Length > 0;
    }

    private static string CaptureApparelState(ApparelInstanceState state) =>
        JsonUtility.ToJson(CloneState(state));

    private static bool TryReadCanonicalApparelState(
        string payload,
        out ApparelInstanceState state)
    {
        state = null;
        if (string.IsNullOrEmpty(payload))
        {
            return false;
        }
        try
        {
            state = JsonUtility.FromJson<ApparelInstanceState>(payload);
            return state != null
                && string.Equals(
                    CaptureApparelState(state),
                    payload,
                    StringComparison.Ordinal);
        }
        catch
        {
            state = null;
            return false;
        }
    }

    private static string RepairOperationId(string orderId) =>
        $"apparel-repair:{orderId}";

    private static void ClearRepairPending(ApparelWorkOrderSaveData order)
    {
        order.repairCommitPhase = ApparelRepairCommitPhase.None;
        order.repairOperationId = string.Empty;
        order.repairReasonCode = string.Empty;
        order.repairCommitId = string.Empty;
        order.repairSourceStackIds.Clear();
        order.repairInputQuantity = 0;
        order.repairInputMassGrams = 0L;
        order.repairTargetStackId = string.Empty;
        order.repairOriginalStatePayload = string.Empty;
        order.repairResolvedStatePayload = string.Empty;
    }

    private void ReplaceAuthority(
        ApparelWorkOrderRestoreCandidate candidate,
        bool releaseCurrentLeases)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        if (releaseCurrentLeases)
        {
            foreach (ApparelWorkOrderSaveData current in orders)
            {
                leases.Release(current.orderId);
            }
        }
        AuthorityState next = new()
        {
            NextSequence = Math.Max(
                1,
                candidate.Orders.Select(ParseSequence).DefaultIfEmpty(0).Max()
                    + 1),
            Version = checked(Version + 1)
        };
        next.Orders.AddRange(candidate.Orders.Select(Clone));
        foreach (ApparelWorkOrderTerminalStateSaveData terminal in
                 candidate.TerminalStates)
        {
            next.TerminalStates.Add(
                terminal.sourceOrder.orderId,
                terminal.Clone());
        }
        authority = next;
    }

    private void ValidateTerminalStateRows(
        IReadOnlyList<ApparelWorkOrderSaveData> candidateOrders,
        IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> candidateTerminals)
    {
        IReadOnlyList<ApparelWorkOrderSaveData> ordersToValidate =
            candidateOrders ?? Array.Empty<ApparelWorkOrderSaveData>();
        IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> terminalsToValidate =
            candidateTerminals
            ?? Array.Empty<ApparelWorkOrderTerminalStateSaveData>();
        if (terminalsToValidate.Any(value => value == null
                || value.schemaVersion !=
                    ApparelWorkOrderTerminalStateSaveData.CurrentSchemaVersion
                || value.sourceOrder == null
                || value.terminalEffectReceipt == null)
            || terminalsToValidate.Select(value => value.sourceOrder.orderId)
                .Distinct(StringComparer.Ordinal).Count()
                != terminalsToValidate.Count)
        {
            throw new InvalidOperationException(
                "Apparel terminal-state collection is invalid or duplicated.");
        }

        HashSet<string> effectCommits = new(StringComparer.Ordinal);
        HashSet<string> sourceCommits = new(StringComparer.Ordinal);
        foreach (ApparelWorkOrderTerminalStateSaveData terminal in
                 terminalsToValidate)
        {
            ApparelWorkOrderSaveData frozen = terminal.sourceOrder;
            bool frozenCapabilityValid = frozen.kind != ApparelWorkOrderKind.Craft;
            if (!frozenCapabilityValid
                && frozen.craftOutputCapability is { IsEmpty: false }
                && apparel.TryGet(
                    frozen.apparelDefinitionId,
                    out ApparelDefinitionSO frozenDefinition))
            {
                frozenCapabilityValid = physicalTransactions
                    .TryValidateCraftOutputCapability(
                        frozen,
                        frozenDefinition.PhysicalItemId,
                        out _);
            }
            string fingerprint = ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceOrderFingerprint(frozen);
            bool sourceValid = ProductionApparelOrderTerminalDrainCanonical
                .IsValidSourceOrder(frozen);
            bool fingerprintValid = string.Equals(
                terminal.sourceOrderFingerprint,
                fingerprint,
                StringComparison.Ordinal);
            bool effectValid = ProductionApparelOrderTerminalDrainCanonical
                .TryCreatePendingEffectIdentity(
                    frozen,
                    out ProductionApparelOrderPendingEffectIdentity effect,
                    out _);
            bool pendingValid = effectValid
                && PendingEffectEquals(effect, terminal.pendingEffect);
            bool receiptValid = pendingValid
                && ProductionApparelOrderTerminalDrainCanonical
                    .EffectReceiptEquals(
                        terminal.terminalEffectReceipt,
                        ProductionApparelOrderTerminalDrainCanonical
                            .CreateTerminalEffectReceipt(
                                terminal.terminalEffectReceipt.stepOperationId,
                                frozen,
                                fingerprint,
                                effect));
            bool commitUnique = receiptValid && effectCommits.Add(
                terminal.terminalEffectReceipt.commitId);
            bool maximumMassProofValid = TryValidateMaximumMassProof(
                frozen,
                out _);
            if (!sourceValid
                || !frozenCapabilityValid
                || !maximumMassProofValid
                || !fingerprintValid
                || !effectValid
                || !pendingValid
                || !receiptValid
                || !commitUnique)
            {
                throw new InvalidOperationException(
                    "Apparel terminal-effect restore row is invalid: source="
                    + sourceValid
                    + "; capability=" + frozenCapabilityValid
                    + "; maximumMassProof=" + maximumMassProofValid
                    + "; fingerprint=" + fingerprintValid
                    + "; effect=" + effectValid
                    + "; pending=" + pendingValid
                    + "; receipt=" + receiptValid
                    + "; unique=" + commitUnique + ".");
            }

            ApparelWorkOrderSaveData[] live = ordersToValidate
                .Where(value => value != null && string.Equals(
                    value.orderId,
                    frozen.orderId,
                    StringComparison.Ordinal))
                .ToArray();
            if (terminal.sourceTerminalReceipt == null)
            {
                if (live.Length != 1
                    || !string.Equals(
                        JsonUtility.ToJson(live[0]),
                        JsonUtility.ToJson(frozen),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Apparel terminal/live source restore join is invalid.");
                }
                continue;
            }

            if (live.Length != 0
                || !ProductionApparelOrderTerminalDrainCanonical
                    .SourceReceiptEquals(
                        terminal.sourceTerminalReceipt,
                        ProductionApparelOrderTerminalDrainCanonical
                            .CreateSourceTerminalReceipt(
                                terminal.sourceTerminalReceipt.stepOperationId,
                                frozen,
                                fingerprint,
                                terminal.terminalEffectReceipt
                                    .receiptFingerprint))
                || !sourceCommits.Add(terminal.sourceTerminalReceipt.commitId))
            {
                throw new InvalidOperationException(
                    "Apparel source-terminal restore row is invalid.");
            }
        }
    }

    private static bool PendingEffectEquals(
        ProductionApparelOrderPendingEffectIdentity left,
        ProductionApparelOrderPendingEffectIdentity right)
    {
        if (left == null || right == null)
            return left == null && right == null;
        return string.Equals(
                left.identityFingerprint,
                right.identityFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                JsonUtility.ToJson(left),
                JsonUtility.ToJson(right),
                StringComparison.Ordinal);
    }

    private static ProductionApparelOrderTerminalEffectApplyResult
        EffectConflict(string failureReason) => new(
            ProductionApparelOrderTerminalDrainStatus.Conflict,
            null,
            failureReason);

    private static ProductionApparelOrderSourceTerminalApplyResult
        SourceConflict(string failureReason) => new(
            ProductionApparelOrderTerminalDrainStatus.Conflict,
            null,
            failureReason);

    private void ClearRestoreTransaction()
    {
        restoreActive = false;
        restorePublished = false;
        stagedRestoreState = null;
        previousRestoreState = null;
    }

    private bool FacilityStillOperational(ApparelWorkOrderSaveData order) =>
        TryGetFacility(order, out _);

    private bool TryRequireMutable(
        BuildableObject facility,
        out DomainFailure failure)
    {
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ApparelFacilityUnavailable);
            return false;
        }
        return ProductionFacilityMutationWorkPolicy.TryRequireMutable(
            facilityMutations,
            facility.RequirePersistentInstanceId(),
            out failure);
    }

    private bool TryRequireMutable(
        ApparelWorkOrderSaveData order,
        out DomainFailure failure)
    {
        if (order == null)
        {
            failure = new DomainFailure(FailureCode.ApparelWorkOrderInvalid);
            return false;
        }
        return ProductionFacilityMutationWorkPolicy.TryRequireMutable(
            facilityMutations,
            new BuildingInstanceId(order.facilityInstanceId),
            out failure);
    }

    private bool TryGetFacility(
        ApparelWorkOrderSaveData order,
        out BuildableObject facility)
    {
        ResearchFacilityCommandKind command = order.kind switch
        {
            ApparelWorkOrderKind.Craft => ResearchFacilityCommandKind.ApparelTailoring,
            ApparelWorkOrderKind.Laundry when order.powered => ResearchFacilityCommandKind.PoweredLaundry,
            ApparelWorkOrderKind.Laundry => ResearchFacilityCommandKind.HandLaundry,
            ApparelWorkOrderKind.Drying => ResearchFacilityCommandKind.IndoorDrying,
            ApparelWorkOrderKind.Repair => ResearchFacilityCommandKind.ApparelRepair,
            ApparelWorkOrderKind.Alteration when order.shortWardrobeOperation =>
                ResearchFacilityCommandKind.DressingChange,
            ApparelWorkOrderKind.Alteration => ResearchFacilityCommandKind.ApparelTailoring,
            _ => ResearchFacilityCommandKind.None
        };
        IEnumerable<BuildableObject> candidates = command
                == ResearchFacilityCommandKind.ApparelTailoring
            ? ApparelTailoringFacilityEligibility.FindOperational(facilities)
            : facilities.FindOperational(command);
        facility = candidates
            .FirstOrDefault(value => value != null
                && string.Equals(
                    value.RequirePersistentInstanceId().Value,
                    order.facilityInstanceId,
                    StringComparison.Ordinal));
        return facility != null;
    }

    private BuildableObject FirstFacility(ResearchFacilityCommandKind command) =>
        facilities.FindOperational(command)
            .Where(value => value != null)
            .OrderBy(value => value.RequirePersistentInstanceId().Value, StringComparer.Ordinal)
            .FirstOrDefault();

    private bool TryFindApparel(
        ItemInstanceId id,
        out WorldItemStackSnapshot stack,
        out ApparelInstanceState state)
    {
        stack = items.GetAllStacks().FirstOrDefault(value => value != null
            && string.Equals(value.ItemInstanceId, id.Value, StringComparison.Ordinal));
        state = null;
        return stack != null
            && apparel.TryGetByItemId(stack.ItemId, out _)
            && ApparelItemStateCodec.TryRead(stack.Components, out state);
    }

    private ApparelWorkOrderSaveData NewOrder(ApparelWorkOrderKind kind)
    {
        ApparelWorkOrderSaveData order = new()
        {
            orderId = $"apparel-order:{nextSequence++:D8}",
            kind = kind,
            state = ApparelWorkOrderState.NeedsRevalidation,
            requiredWork = 1f
        };
        orders.Add(order);
        return order;
    }

    private ApparelWorkOrderSaveData Find(string orderId) => orders.FirstOrDefault(value =>
        string.Equals(value.orderId, orderId?.Trim(), StringComparison.Ordinal));

    private static WorldItemReservedStackQuantity Reservation(
        WorldItemStackSnapshot stack,
        int quantity) => new(
            stack.StackId,
            stack.ItemId,
            quantity,
            stack.Position,
            WorldItemHaulDestinationKind.FacilityBuffer,
            string.Empty);

    private static bool Fail(out DomainFailure failure)
    {
        failure = new DomainFailure(FailureCode.ApparelWorkOrderInvalid);
        return false;
    }

    private static ApparelInstanceState CloneState(ApparelInstanceState state) => new()
    {
        apparelDefinitionId = state.apparelDefinitionId,
        primaryMaterialId = state.primaryMaterialId,
        craftsmanshipQuality = state.craftsmanshipQuality,
        sourceKind = state.sourceKind,
        sourceDefinitionId = state.sourceDefinitionId,
        size = state.size,
        modifications = state.modifications,
        closedOpenings = state.closedOpenings,
        durability = state.durability,
        moisture = state.moisture,
        contamination = state.contamination,
        designatedWearerCharacterId = state.designatedWearerCharacterId,
        craftedAbsoluteDay = state.craftedAbsoluteDay,
        deterministicBatchHash = state.deterministicBatchHash,
        mythicProvenance = state.mythicProvenance?.Clone()
    };

    private static TextileSourceKind SourceKind(TextileMaterialTag tags)
    {
        if ((tags & TextileMaterialTag.Animal) != 0) return TextileSourceKind.Animal;
        if ((tags & TextileMaterialTag.Arcane) != 0) return TextileSourceKind.Arcane;
        if ((tags & TextileMaterialTag.Plant) != 0) return TextileSourceKind.Crop;
        return TextileSourceKind.Unknown;
    }

    private float GetApparelQualitySkill(
        CharacterActor worker,
        ApparelWorkOrderSaveData order)
    {
        if (worker == null) return 25f;
        if (performance == null)
            throw new InvalidOperationException(
                "Apparel quality requires the character performance query.");
        if (performanceContext == null)
            throw new InvalidOperationException(
                "Apparel quality requires the work performance context resolver.");
        if (!TryGetFacility(order, out BuildableObject facility))
            throw new InvalidOperationException(
                "Apparel quality requires the exact operational facility.");
        if (!performanceContext.TryResolve(
                worker,
                facility,
                BuiltInWorkTypeIds.Craft,
                out ProficiencyWorkProfile profile,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
        CharacterPerformanceSnapshot snapshot = performance.Evaluate(
            worker,
            "performance:work:craft:quality",
            performanceContext.BuildEvaluationContext(
                profile,
                new GameplayEffectContext(new[] { "work:craft-finished" })));
        if (!snapshot.IsApplicable)
            throw new InvalidOperationException(
                snapshot.Failure?.Message ?? "Apparel quality is unavailable.");
        return Mathf.Clamp(snapshot.Value * 58f, 0f, 100f);
    }

    private bool HasEligibleWorker(
        WorkerSelectionPolicySaveData policy,
        CharacterActor currentWorker = null)
    {
        if (currentWorker != null
            && WorkerSelectionPolicyRules.IsEligible(
                policy,
                currentWorker,
                narrativeQualifications,
                out _))
        {
            return true;
        }
        if (characterWorld == null)
        {
            return true;
        }
        return characterWorld.Characters.Any(actor => actor != null
            && WorkerSelectionPolicyRules.IsEligible(
                policy,
                actor,
                narrativeQualifications,
                out _));
    }

    private bool CanPotentiallyReachCraftQuality(
        ApparelWorkOrderSaveData order,
        ApparelDefinitionSO definition,
        BuildableObject facility,
        CharacterActor currentWorker = null)
    {
        if (characterWorld == null
            || order.minimumCraftsmanshipQuality
                <= CraftsmanshipQualityTier.Awful)
        {
            return true;
        }
        float bestSkill = currentWorker != null
            && WorkerSelectionPolicyRules.IsEligible(
                order.workerPolicy,
                currentWorker,
                narrativeQualifications,
                out _)
                ? GetApparelQualitySkill(currentWorker, order)
                : -1f;
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (actor != null
                && WorkerSelectionPolicyRules.IsEligible(
                    order.workerPolicy,
                    actor,
                    narrativeQualifications,
                    out _))
            {
                bestSkill = Mathf.Max(
                    bestSkill,
                    GetApparelQualitySkill(actor, order));
            }
        }
        if (bestSkill < 0f)
        {
            return true;
        }
        CraftQualityResolution theoreticalBest = qualityResolver.Resolve(
            new CraftQualityRollSaveData
            {
                attemptIndex = order.qualityAttemptIndex,
                randomA = 10,
                randomB = 10,
                randomC = 10
            },
            bestSkill,
            (facility.Craftsmanship.Score - 50f) * 0.08f,
            toolBonus: 0f,
            complexityPenalty: Mathf.Max(
                0f,
                definition.TailoringCoefficient - 1f) * 4f);
        return (int)theoreticalBest.Tier
            >= (int)order.minimumCraftsmanshipQuality;
    }

    private bool TryValidateMaximumMassProof(
        ApparelWorkOrderSaveData order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null)
        {
            failureReason = "apparel-maximum-mass-owner-null";
            return false;
        }

        try
        {
            bool craftProofPresent = order.craftOutputCapability is { IsEmpty: false }
                || !string.IsNullOrEmpty(order.craftMaximumMassProofDigest)
                || order.craftMaximumBatchMassGrams != 0L;
            if (craftProofPresent)
            {
                if (order.kind != ApparelWorkOrderKind.Craft
                    || order.craftOutputCapability == null
                    || order.craftOutputCapability.IsEmpty)
                {
                    failureReason = "apparel-craft-maximum-mass-owner-invalid";
                    return false;
                }
                ProductionOutputMaximumMassProjection projection =
                    outputMaximumMass.CaptureDeclared(
                        order.craftOutputCapability.ToDescriptor(),
                        1);
                ProductionOutputBatchMaximumMassProof proof = new(
                    new[] { projection });
                if (!string.Equals(
                        order.craftMaximumMassProofDigest,
                        proof.SourceDigest,
                        StringComparison.Ordinal)
                    || order.craftMaximumBatchMassGrams
                        != proof.MaximumBatchMassGrams
                    || order.craftOutputMassGrams < 0L
                    || order.craftOutputMassGrams
                        > proof.MaximumBatchMassGrams)
                {
                    failureReason = "apparel-craft-maximum-mass-proof-drift";
                    return false;
                }
            }

            bool rejectedProofPresent =
                order.rejectedRecoveryOutputCapability is { IsEmpty: false }
                || !string.IsNullOrEmpty(
                    order.rejectedRecoveryMaximumMassProofDigest)
                || order.rejectedRecoveryMaximumBatchMassGrams != 0L;
            if (!rejectedProofPresent)
                return true;
            if (order.rejectedMaterialAmount <= 0
                || order.rejectedRecoveryOutputCapability == null
                || order.rejectedRecoveryOutputCapability.IsEmpty)
            {
                failureReason = "apparel-rejected-maximum-mass-owner-invalid";
                return false;
            }

            ProductionOutputCapabilityDescriptor descriptor =
                order.rejectedRecoveryOutputCapability.ToDescriptor();
            if (!string.Equals(
                    descriptor.OutputLineId,
                    ApparelPhysicalTransaction.RejectedRecoveryOutputLineId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    descriptor.ItemId,
                    order.rejectedRecoveryItemId,
                    StringComparison.Ordinal))
            {
                failureReason = "apparel-rejected-maximum-mass-owner-mismatch";
                return false;
            }
            ProductionOutputMaximumMassProjection rejectedProjection =
                outputMaximumMass.CaptureDeclared(
                    descriptor,
                    order.rejectedMaterialAmount);
            ProductionOutputBatchMaximumMassProof rejectedProof = new(
                new[] { rejectedProjection });
            if (!string.Equals(
                    order.rejectedRecoveryMaximumMassProofDigest,
                    rejectedProof.SourceDigest,
                    StringComparison.Ordinal)
                || order.rejectedRecoveryMaximumBatchMassGrams
                    != rejectedProof.MaximumBatchMassGrams
                || order.rejectedRecoveryOutputMassGrams < 0L
                || order.rejectedRecoveryOutputMassGrams
                    > rejectedProof.MaximumBatchMassGrams)
            {
                failureReason = "apparel-rejected-maximum-mass-proof-drift";
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "apparel-maximum-mass-proof-invalid:"
                + exception.Message;
            return false;
        }
    }

    private static ulong Hash(string source)
    {
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (char value in source ?? string.Empty)
        {
            hash ^= value;
            hash *= prime;
        }
        return hash;
    }

    private static ApparelWorkOrderSaveData Clone(ApparelWorkOrderSaveData value) => new()
    {
        orderId = value?.orderId?.Trim() ?? string.Empty,
        kind = value?.kind ?? ApparelWorkOrderKind.Craft,
        state = value?.state ?? ApparelWorkOrderState.NeedsRevalidation,
        apparelDefinitionId = value?.apparelDefinitionId?.Trim() ?? string.Empty,
        materialDefinitionId = value?.materialDefinitionId?.Trim() ?? string.Empty,
        materialPolicy = value?.materialPolicy ?? ApparelMaterialSelectionPolicy.ExactMaterial,
        minimumCraftsmanshipQuality = value?.minimumCraftsmanshipQuality
            ?? CraftsmanshipQualityTier.Normal,
        workerPolicy = value?.workerPolicy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality),
        contributions = value?.contributions?
            .Where(contribution => contribution != null)
            .Select(contribution => contribution.Clone())
            .ToList() ?? new List<CraftContributionSaveData>(),
        lastWorkerCharacterId = value?.lastWorkerCharacterId?.Trim() ?? string.Empty,
        qualityRoll = value?.qualityRoll == null ? null : new CraftQualityRollSaveData
        {
            attemptIndex = value.qualityRoll.attemptIndex,
            randomA = value.qualityRoll.randomA,
            randomB = value.qualityRoll.randomB,
            randomC = value.qualityRoll.randomC
        },
        qualityAttemptIndex = Mathf.Max(0, value?.qualityAttemptIndex ?? 0),
        rejectedDisposition = value?.rejectedDisposition
            ?? RejectedOutputDisposition.AutoDismantle,
        repeatLimitMode = value?.repeatLimitMode
            ?? QualityRepeatLimitMode.SafeLimits,
        maximumAttempts = Mathf.Max(1, value?.maximumAttempts ?? 10),
        workBudget = Mathf.Max(0f, value?.workBudget ?? 0f),
        consumedWork = Mathf.Max(0f, value?.consumedWork ?? 0f),
        requiredAcceptedCount = Mathf.Max(
            1,
            value?.requiredAcceptedCount ?? 1),
        acceptedCount = Mathf.Max(0, value?.acceptedCount ?? 0),
        dismantlingRejectedOutput =
            value?.dismantlingRejectedOutput ?? false,
        rejectedOutputConsumed =
            value?.rejectedOutputConsumed ?? false,
        rejectedOutputStackId =
            value?.rejectedOutputStackId?.Trim() ?? string.Empty,
        rejectedOutputInstanceId =
            value?.rejectedOutputInstanceId?.Trim() ?? string.Empty,
        craftWorkPerAttempt = Mathf.Max(
            0f,
            value?.craftWorkPerAttempt ?? 0f),
        rejectedMaterialAmount = Mathf.Max(
            0,
            value?.rejectedMaterialAmount ?? 0),
        rejectedMaterialSpawned = Mathf.Clamp(
            value?.rejectedMaterialSpawned ?? 0,
            0,
            Mathf.Max(0, value?.rejectedMaterialAmount ?? 0)),
        rejectedRecoveryItemId =
            value?.rejectedRecoveryItemId?.Trim() ?? string.Empty,
        rejectedDismantleOperationId =
            value?.rejectedDismantleOperationId?.Trim() ?? string.Empty,
        rejectedDismantleCommitId =
            value?.rejectedDismantleCommitId?.Trim() ?? string.Empty,
        rejectedDismantleRequestFingerprint =
            value?.rejectedDismantleRequestFingerprint?.Trim()
            ?? string.Empty,
        rejectedDismantleInputMassGrams = Math.Max(
            0L,
            value?.rejectedDismantleInputMassGrams ?? 0L),
        rejectedRecoveryOperationId =
            value?.rejectedRecoveryOperationId?.Trim() ?? string.Empty,
        rejectedRecoveryCommitId =
            value?.rejectedRecoveryCommitId?.Trim() ?? string.Empty,
        rejectedRecoveryOutputMassGrams = Math.Max(
            0L,
            value?.rejectedRecoveryOutputMassGrams ?? 0L),
        rejectedRecoveryPublicationAttempt = Mathf.Max(
            0,
            value?.rejectedRecoveryPublicationAttempt ?? 0),
        rejectedRecoveryOutcomeFingerprint =
            value?.rejectedRecoveryOutcomeFingerprint?.Trim() ?? string.Empty,
        rejectedRecoveryAdmissionTokenId =
            value?.rejectedRecoveryAdmissionTokenId?.Trim() ?? string.Empty,
        rejectedRecoveryOutputCapability =
            value?.rejectedRecoveryOutputCapability?.Clone()
            ?? new ProductionOutputCapabilitySaveData(),
        rejectedRecoveryMaximumMassProofDigest =
            value?.rejectedRecoveryMaximumMassProofDigest ?? string.Empty,
        rejectedRecoveryMaximumBatchMassGrams =
            value?.rejectedRecoveryMaximumBatchMassGrams ?? 0L,
        rejectedRecoveryCapacitySourceDigest =
            value?.rejectedRecoveryCapacitySourceDigest?.Trim() ?? string.Empty,
        rejectedRecoveryRequiredMinimumCapacityGrams = Math.Max(
            0L,
            value?.rejectedRecoveryRequiredMinimumCapacityGrams ?? 0L),
        rejectedRecoveryPlannedOutputFingerprint =
            value?.rejectedRecoveryPlannedOutputFingerprint?.Trim()
            ?? string.Empty,
        rejectedRecoveryStackIds = value?.rejectedRecoveryStackIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList()
            ?? new List<string>(),
        rejectedRecoveryPublished =
            value?.rejectedRecoveryPublished ?? false,
        rejectedRecoveryAdmissionCommitted =
            value?.rejectedRecoveryAdmissionCommitted ?? false,
        rejectedRecoveryOutputAcknowledged =
            value?.rejectedRecoveryOutputAcknowledged ?? false,
        rejectedDismantleAcknowledged =
            value?.rejectedDismantleAcknowledged ?? false,
        craftPublicationAttempt = Mathf.Max(
            0,
            value?.craftPublicationAttempt ?? 0),
        craftPublicationOperationId =
            value?.craftPublicationOperationId?.Trim() ?? string.Empty,
        craftOutputBatchCommitId =
            value?.craftOutputBatchCommitId?.Trim() ?? string.Empty,
        craftOutcomeFingerprint =
            value?.craftOutcomeFingerprint?.Trim() ?? string.Empty,
        craftOutputComponentFingerprint =
            value?.craftOutputComponentFingerprint?.Trim() ?? string.Empty,
        craftOutputCapability = value?.craftOutputCapability?.Clone()
            ?? new ProductionOutputCapabilitySaveData(),
        craftAdmissionTokenId =
            value?.craftAdmissionTokenId?.Trim() ?? string.Empty,
        craftMaximumMassProofDigest =
            value?.craftMaximumMassProofDigest ?? string.Empty,
        craftMaximumBatchMassGrams =
            value?.craftMaximumBatchMassGrams ?? 0L,
        craftCapacitySourceDigest =
            value?.craftCapacitySourceDigest?.Trim() ?? string.Empty,
        craftRequiredMinimumCapacityGrams = Math.Max(
            0L,
            value?.craftRequiredMinimumCapacityGrams ?? 0L),
        craftPlannedOutputFingerprint =
            value?.craftPlannedOutputFingerprint?.Trim() ?? string.Empty,
        craftOutputStackId =
            value?.craftOutputStackId?.Trim() ?? string.Empty,
        craftOutputInstanceId =
            value?.craftOutputInstanceId?.Trim() ?? string.Empty,
        craftInputCommitId =
            value?.craftInputCommitId?.Trim() ?? string.Empty,
        craftInputRequestFingerprint =
            value?.craftInputRequestFingerprint?.Trim() ?? string.Empty,
        craftInputMassGrams = Math.Max(
            0L,
            value?.craftInputMassGrams ?? 0L),
        craftOutputMassGrams = Math.Max(
            0L,
            value?.craftOutputMassGrams ?? 0L),
        craftInputPending = value?.craftInputPending ?? false,
        craftOutputPublished = value?.craftOutputPublished ?? false,
        craftAdmissionCommitted = value?.craftAdmissionCommitted ?? false,
        craftInputAcknowledged = value?.craftInputAcknowledged ?? false,
        craftOutputAcknowledged = value?.craftOutputAcknowledged ?? false,
        craftMarketRouted = value?.craftMarketRouted ?? false,
        rejectedOutputLeaseId =
            value?.rejectedOutputLeaseId?.Trim() ?? string.Empty,
        targetSize = value?.targetSize ?? ApparelSizeClass.Medium,
        targetModifications = value?.targetModifications ?? ApparelModificationKind.None,
        facilityInstanceId = value?.facilityInstanceId?.Trim() ?? string.Empty,
        targetItemInstanceId = value?.targetItemInstanceId?.Trim() ?? string.Empty,
        targetItemInstanceIds = value?.targetItemInstanceIds?
            .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList()
            ?? new List<string>(),
        materialStackIds = value?.materialStackIds?
            .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList()
            ?? new List<string>(),
        materialStackAmounts = value?.materialStackAmounts?.ToList() ?? new List<int>(),
        requiredWork = value?.requiredWork ?? 0f,
        completedWork = value?.completedWork ?? 0f,
        retryCount = value?.retryCount ?? 0,
        nextRetryGameHour = value?.nextRetryGameHour ?? 0f,
        powered = value?.powered ?? false,
        shortWardrobeOperation = value?.shortWardrobeOperation ?? false,
        repairCommitPhase = value?.repairCommitPhase
            ?? ApparelRepairCommitPhase.None,
        repairOperationId = value?.repairOperationId?.Trim() ?? string.Empty,
        repairReasonCode = value?.repairReasonCode?.Trim() ?? string.Empty,
        repairCommitId = value?.repairCommitId?.Trim() ?? string.Empty,
        repairSourceStackIds = value?.repairSourceStackIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList() ?? new List<string>(),
        repairInputQuantity = value?.repairInputQuantity ?? 0,
        repairInputMassGrams = value?.repairInputMassGrams ?? 0L,
        repairTargetStackId = value?.repairTargetStackId?.Trim() ?? string.Empty,
        repairOriginalStatePayload = value?.repairOriginalStatePayload
            ?? string.Empty,
        repairResolvedStatePayload = value?.repairResolvedStatePayload
            ?? string.Empty
    };

    private static bool TerminalStateMatchesProducer(
        ApparelWorkOrderTerminalStateSaveData row,
        ProductionApparelOrderTerminalDrainSaveData producer) => row != null
        && producer != null
        && row.schemaVersion ==
            ApparelWorkOrderTerminalStateSaveData.CurrentSchemaVersion
        && row.sourceOrder != null
        && string.Equals(
            row.sourceOrderFingerprint,
            producer.sourceOrderFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            JsonUtility.ToJson(row.sourceOrder),
            JsonUtility.ToJson(producer.sourceOrder),
            StringComparison.Ordinal)
        && PendingEffectEquals(row.pendingEffect, producer.pendingEffect)
        && ProductionApparelOrderTerminalDrainCanonical.EffectReceiptEquals(
            row.terminalEffectReceipt,
            producer.terminalEffectReceipt)
        && ProductionApparelOrderTerminalDrainCanonical.SourceReceiptEquals(
            row.sourceTerminalReceipt,
            producer.sourceTerminalReceipt);

    private static bool TerminalStateEquals(
        ApparelWorkOrderTerminalStateSaveData left,
        ApparelWorkOrderTerminalStateSaveData right) => left != null
        && right != null
        && string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private TerminalStateCheckpointGcCandidate RequireTerminalStateGcCandidate(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate)
    {
        if (candidate is not TerminalStateCheckpointGcCandidate exact
            || !ReferenceEquals(activeTerminalStateGcCandidate, exact))
        {
            throw new InvalidOperationException(
                "Apparel terminal-state checkpoint GC candidate is stale or foreign.");
        }
        return exact;
    }

    private sealed class TerminalStateCheckpointGcCandidate :
        IProductionApparelTerminalStateCheckpointGcCandidate
    {
        internal TerminalStateCheckpointGcCandidate(
            int expectedVersion,
            IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> rows)
        {
            ExpectedVersion = expectedVersion;
            Rows = (rows
                    ?? Array.Empty<ApparelWorkOrderTerminalStateSaveData>())
                .Select(value => value.Clone())
                .OrderBy(
                    value => value.sourceOrder.orderId,
                    StringComparer.Ordinal)
                .ToArray();
            PublishedVersion = expectedVersion;
        }

        internal int ExpectedVersion { get; }
        internal IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> Rows
            { get; }
        internal bool Published { get; set; }
        internal int PublishedVersion { get; set; }
    }

    private static int ParseSequence(ApparelWorkOrderSaveData order)
    {
        string id = order?.orderId ?? string.Empty;
        int colon = id.LastIndexOf(':');
        return colon >= 0 && int.TryParse(id.Substring(colon + 1), out int value)
            ? value
            : 0;
    }

    private float GameHour => Mathf.Max(0f, clock.Time / GameSecondsPerHour);
}
