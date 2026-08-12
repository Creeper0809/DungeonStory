using System;
using System.Collections.Generic;
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
    TargetCurrentlyUnreachable = 8
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
    public float craftWorkPerAttempt;
    public int rejectedMaterialAmount;
    public int rejectedMaterialSpawned;
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
}

public interface IApparelWorkOrderQuery
{
    int Version { get; }
    IReadOnlyList<ApparelWorkOrderSaveData> Orders { get; }
}

public interface IApparelWorkOrderPersistence
{
    ApparelWorkOrderSaveData[] CaptureOrders();
    IReadOnlyList<ApparelWorkOrderSaveData> PrepareRestoreOrders(
        IEnumerable<ApparelWorkOrderSaveData> source);
    void PublishRestoreOrders(IEnumerable<ApparelWorkOrderSaveData> source);
    void ResetOrders();
}

public sealed class ApparelWorkOrderRuntime :
    IApparelWorkOrderCommand,
    IApparelWorkOrderQuery,
    IApparelWorkOrderPersistence
{
    private const int MaximumBatch = 12;
    private const float GameSecondsPerHour = 7.5f;
    private static readonly float[] RetryIntervals = { .25f, .5f, 1f };

    private readonly IApparelDefinitionCatalog apparel;
    private readonly ITextileMaterialCatalog materials;
    private readonly IWorldItemStackRuntime items;
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
    private readonly List<ApparelWorkOrderSaveData> orders = new();
    private int nextSequence = 1;

    public ApparelWorkOrderRuntime(
        IApparelDefinitionCatalog apparel,
        ITextileMaterialCatalog materials,
        IWorldItemStackRuntime items,
        ILeasedItemReservationService leases,
        IFacilityCapabilityQuery facilities,
        IGameClock clock,
        IBalanceWorkCalculator balanceWorkCalculator = null,
        ICraftQualityResolver qualityResolver = null,
        IRunSeedProvider runSeedProvider = null,
        IWorkerNarrativeQualificationQuery narrativeQualifications = null,
        ICharacterWorldQuery characterWorld = null,
        ExtremeCraftInspirationRuntime inspirationRuntime = null,
        CharacterIdentityEventPublisher identityEvents = null,
        ICharacterPerformanceQuery performance = null)
    {
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        this.materials = materials ?? throw new ArgumentNullException(nameof(materials));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
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
    }

    public int Version { get; private set; }
    public IReadOnlyList<ApparelWorkOrderSaveData> Orders => orders;

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
        BuildableObject facility = FirstFacility(
            ResearchFacilityCommandKind.ApparelTailoring);
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ApparelFacilityUnavailable);
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
                GetApparelQualitySkill(worker));
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
            if (order.state != ApparelWorkOrderState.WaitingForOutputSpace)
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

    public bool Cancel(string orderId)
    {
        ApparelWorkOrderSaveData order = Find(orderId);
        if (order == null)
        {
            return false;
        }
        // Once dismantling has converted the rejected garment into a saved
        // recovery obligation, removing the order would delete that value.
        // The player may cancel after the physical recovery has completed.
        if (order.dismantlingRejectedOutput)
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
                || (value.rejectedOutputConsumed
                    && !value.dismantlingRejectedOutput)
                || (value.dismantlingRejectedOutput
                    && string.IsNullOrWhiteSpace(value.rejectedOutputStackId))
                || (value.kind == ApparelWorkOrderKind.Craft
                    && (value.qualityRoll == null
                        || value.qualityRoll.attemptIndex
                            != value.qualityAttemptIndex)))
            || restored.Select(value => value.orderId)
                .Distinct(StringComparer.Ordinal).Count() != restored.Count)
        {
            throw new InvalidOperationException("V23 apparel work-order payload is invalid.");
        }
        foreach (ApparelWorkOrderSaveData order in restored)
        {
            order.state = ApparelWorkOrderState.NeedsRevalidation;
            order.nextRetryGameHour = 0f;
        }
        return restored;
    }

    public void PublishRestoreOrders(IEnumerable<ApparelWorkOrderSaveData> source)
    {
        foreach (ApparelWorkOrderSaveData current in orders)
        {
            leases.Release(current.orderId);
        }
        orders.Clear();
        orders.AddRange((source ?? Enumerable.Empty<ApparelWorkOrderSaveData>())
            .Select(Clone));
        nextSequence = Math.Max(
            1,
            orders.Select(ParseSequence).DefaultIfEmpty(0).Max() + 1);
        Version++;
    }

    public void ResetOrders()
    {
        foreach (ApparelWorkOrderSaveData order in orders)
        {
            leases.Release(order.orderId);
        }
        orders.Clear();
        nextSequence = 1;
        Version++;
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
        string outputDestination = order.rejectedDisposition
            == RejectedOutputDisposition.MarkForSale
            && completedQuality != CraftsmanshipQualityTier.Mythic
                ? QualityRejectedOutputRules.MarketDestinationId
                : ProductionBillRuntime.OutputDestinationPrefix
                    + facility.RequirePersistentInstanceId().Value;
        if (!items.SpawnUniqueItemAt(
                definition.PhysicalItemId,
                facility.centerPos,
                WorldItemStackState.FacilityOutputBuffer,
                outputDestination,
                out string stackId))
        {
            failure = new DomainFailure(FailureCode.ApparelTransferFailed);
            return false;
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
        if (!items.TrySetInstanceComponent(stackId, ApparelItemStateCodec.Create(state))
            || !ConsumeNonTargetMaterials(order, string.Empty))
        {
            items.DeleteStack(stackId);
            failure = new DomainFailure(FailureCode.ApparelTransferFailed, order.orderId);
            return false;
        }
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
            order.dismantlingRejectedOutput = true;
            order.rejectedOutputConsumed = false;
            order.rejectedOutputStackId = stackId;
            float salvageYield = maker != null
                ? maker.GetDetailedStatMultiplier(
                    GameplayEffectTargetIds.SalvageYield)
                : 1f;
            order.rejectedMaterialAmount = Mathf.FloorToInt(
                order.materialStackAmounts.Sum()
                * 0.50f
                * Mathf.Max(0f, salvageYield));
            order.rejectedMaterialSpawned = 0;
            order.requiredWork = Mathf.Max(
                0.1f,
                order.craftWorkPerAttempt * 0.20f);
            order.completedWork = 0f;
            order.contributions.Clear();
            order.state = ApparelWorkOrderState.Ready;
            leases.Release(order.orderId);
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

        if (!order.rejectedOutputConsumed)
        {
            if (!items.GetAllStacks().Any(stack => stack != null
                    && string.Equals(
                        stack.StackId,
                        order.rejectedOutputStackId,
                        StringComparison.Ordinal))
                || !items.DeleteStack(order.rejectedOutputStackId))
            {
                failure = new DomainFailure(
                    FailureCode.ApparelPhysicalItemMissing,
                    order.rejectedOutputStackId);
                return false;
            }
            // From this point the order is the authority for the owed salvage.
            // Saving before output space becomes available cannot reroll or
            // duplicate the dismantled garment.
            order.rejectedOutputConsumed = true;
        }
        int remainingRecovery = Mathf.Max(
            0,
            order.rejectedMaterialAmount - order.rejectedMaterialSpawned);
        int spawned = 0;
        if (remainingRecovery > 0
            && !items.SpawnItemAt(
                material.PhysicalItemId,
                remainingRecovery,
                facility.centerPos,
                WorldItemStackState.FacilityOutputBuffer,
                ProductionBillRuntime.OutputDestinationPrefix
                    + facility.RequirePersistentInstanceId().Value,
                out spawned))
        {
            order.rejectedMaterialSpawned += Mathf.Max(0, spawned);
            order.state = ApparelWorkOrderState.WaitingForOutputSpace;
            failure = new DomainFailure(FailureCode.ApparelTransferFailed);
            return false;
        }
        if (remainingRecovery > 0)
        {
            order.rejectedMaterialSpawned += Mathf.Max(0, spawned);
        }
        if (order.rejectedMaterialSpawned < order.rejectedMaterialAmount)
        {
            order.state = ApparelWorkOrderState.WaitingForOutputSpace;
            failure = new DomainFailure(FailureCode.ApparelTransferFailed);
            return false;
        }
        order.consumedWork += Mathf.Max(0f, order.requiredWork);
        order.dismantlingRejectedOutput = false;
        order.rejectedOutputConsumed = false;
        order.rejectedOutputStackId = string.Empty;
        order.rejectedMaterialAmount = 0;
        order.rejectedMaterialSpawned = 0;
        return PrepareNextCraftAttempt(order, definition, out failure);
    }

    private bool PrepareNextCraftAttempt(
        ApparelWorkOrderSaveData order,
        ApparelDefinitionSO definition,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
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
        if (!CanConsumeNonTargetMaterials(order, stack.StackId))
        {
            failure = new DomainFailure(FailureCode.ApparelTransferFailed, order.orderId);
            return false;
        }
        if (!items.TrySetInstanceComponent(
                stack.StackId,
                ApparelItemStateCodec.Create(changed)))
        {
            failure = new DomainFailure(FailureCode.ApparelTransferFailed, stack.StackId);
            return false;
        }
        if (!ConsumeNonTargetMaterials(order, stack.StackId))
        {
            items.TrySetInstanceComponent(
                stack.StackId,
                ApparelItemStateCodec.Create(state));
            failure = new DomainFailure(FailureCode.ApparelTransferFailed, order.orderId);
            return false;
        }
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

    private bool ConsumeNonTargetMaterials(
        ApparelWorkOrderSaveData order,
        string excludedStackId)
    {
        if (!CanConsumeNonTargetMaterials(order, excludedStackId))
        {
            return false;
        }
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
            if (!items.TryConsumeStackQuantity(stackId, amount, out _))
            {
                return false;
            }
        }
        return true;
    }

    private bool CanConsumeNonTargetMaterials(
        ApparelWorkOrderSaveData order,
        string excludedStackId)
    {
        Dictionary<string, WorldItemStackSnapshot> current = items.GetAllStacks()
            .Where(value => value != null)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
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
            if (!current.TryGetValue(stackId, out WorldItemStackSnapshot stack)
                || stack.Quantity < amount)
            {
                return false;
            }
        }
        return true;
    }

    private bool FacilityStillOperational(ApparelWorkOrderSaveData order) =>
        TryGetFacility(order, out _);

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
        facility = facilities.FindOperational(command)
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

    private float GetApparelQualitySkill(CharacterActor worker)
    {
        if (worker == null) return 25f;
        if (performance == null)
            throw new InvalidOperationException(
                "Apparel quality requires the character performance query.");
        CharacterPerformanceSnapshot snapshot = performance.Evaluate(
            worker,
            "performance:work:craft:quality",
            new CharacterPerformanceEvaluationContext
            {
                GameplayEffectContext = new GameplayEffectContext(
                    new[] { "work:craft-finished" })
            });
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
                ? GetApparelQualitySkill(currentWorker)
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
                bestSkill = Mathf.Max(bestSkill, GetApparelQualitySkill(actor));
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
        shortWardrobeOperation = value?.shortWardrobeOperation ?? false
    };

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
