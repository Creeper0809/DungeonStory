using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class ProductionPreparedOutputRoutingLineSaveData
{
    public string batchCommitId = string.Empty;
    public string lineCommitId = string.Empty;
    public string outputLineId = string.Empty;
    public ProductionOutputRole role;
    public string itemId = string.Empty;
    public string destinationId = string.Empty;
    public string componentFingerprint = string.Empty;
    public int originalQuantity;
    public int remainingQuantity;
    public long originalMassGrams;
    public long remainingMassGrams;
    public int routedQuantity;
    public long routedMassGrams;
    public List<ProductionPreparedOutputRouteOperationSaveData> routeOperations =
        new();

    public ProductionPreparedOutputRoutingLineSaveData Clone() => new()
    {
        batchCommitId = batchCommitId,
        lineCommitId = lineCommitId,
        outputLineId = outputLineId,
        role = role,
        itemId = itemId,
        destinationId = destinationId,
        componentFingerprint = componentFingerprint,
        originalQuantity = originalQuantity,
        remainingQuantity = remainingQuantity,
        originalMassGrams = originalMassGrams,
        remainingMassGrams = remainingMassGrams,
        routedQuantity = routedQuantity,
        routedMassGrams = routedMassGrams,
        routeOperations = (routeOperations
                ?? new List<ProductionPreparedOutputRouteOperationSaveData>())
            .ConvertAll(value => value?.Clone())
    };
}

public enum ProductionPreparedOutputRoutePhase
{
    PhysicalPending = 1,
    PhysicalAppliedAwaitingItemsAck = 2,
    ItemsAcknowledgedAwaitingCheckpointGc = 3
}

public enum ProductionPreparedOutputDeliveryTargetKind
{
    InitialExactTarget = 1,
    WarehouseSelectionPending = 2,
    ExactRerouteTarget = 3
}

public enum ProductionPreparedOutputDeliveryRerouteReason
{
    InitialRoute = 1,
    DestinationInvalidated = 2,
    ConsumerCancelled = 3,
    CarrierRecovery = 4,
    WarehouseRetarget = 5,
    InitialTargetAuthorityConfirmed = 6
}

[Serializable]
public sealed class ProductionPreparedOutputDeliveryRevisionSaveData
{
    public long revision;
    public ProductionPreparedOutputDeliveryTargetKind targetKind;
    public ProductionPreparedOutputDeliveryRerouteReason reason;
    public string rerouteOperationId = string.Empty;
    public string previousRevisionFingerprint = string.Empty;
    public string originalPhysicalReceiptFingerprint = string.Empty;
    public string targetDestinationId = string.Empty;
    public int targetPositionX;
    public int targetPositionY;
    public string targetAuthorityFingerprint = string.Empty;
    public string revisionFingerprint = string.Empty;

    public ProductionPreparedOutputDeliveryRevisionSaveData Clone() => new()
    {
        revision = revision,
        targetKind = targetKind,
        reason = reason,
        rerouteOperationId = rerouteOperationId,
        previousRevisionFingerprint = previousRevisionFingerprint,
        originalPhysicalReceiptFingerprint = originalPhysicalReceiptFingerprint,
        targetDestinationId = targetDestinationId,
        targetPositionX = targetPositionX,
        targetPositionY = targetPositionY,
        targetAuthorityFingerprint = targetAuthorityFingerprint,
        revisionFingerprint = revisionFingerprint
    };
}

[Serializable]
public sealed class ProductionPreparedOutputRouteOperationSaveData
{
    public string routeOperationId = string.Empty;
    public string requestFingerprint = string.Empty;
    public string physicalReceiptFingerprint = string.Empty;
    public ProductionPreparedOutputRoutePhase phase;
    public int sourceOffsetQuantity;
    public long sourceOffsetMassGrams;
    public int routedQuantity;
    public long routedMassGrams;
    public int targetPositionX;
    public int targetPositionY;
    public string targetDestinationId = string.Empty;
    public List<ProductionPreparedOutputPhysicalRouteSliceSaveData> physicalSlices =
        new();
    public List<ProductionPreparedOutputDeliveryRevisionSaveData> deliveryRevisions =
        new();

    public ProductionPreparedOutputRouteOperationSaveData Clone() => new()
    {
        routeOperationId = routeOperationId,
        requestFingerprint = requestFingerprint,
        physicalReceiptFingerprint = physicalReceiptFingerprint,
        phase = phase,
        sourceOffsetQuantity = sourceOffsetQuantity,
        sourceOffsetMassGrams = sourceOffsetMassGrams,
        routedQuantity = routedQuantity,
        routedMassGrams = routedMassGrams,
        targetPositionX = targetPositionX,
        targetPositionY = targetPositionY,
        targetDestinationId = targetDestinationId,
        physicalSlices = (physicalSlices
                ?? new List<ProductionPreparedOutputPhysicalRouteSliceSaveData>())
            .ConvertAll(value => value?.Clone()),
        deliveryRevisions = (deliveryRevisions
                ?? new List<ProductionPreparedOutputDeliveryRevisionSaveData>())
            .ConvertAll(value => value?.Clone())
    };
}

[Serializable]
public sealed class ProductionPreparedOutputPhysicalRouteSliceSaveData
{
    public string sourceStackId = string.Empty;
    public string routedStackId = string.Empty;
    public int sourceOffsetQuantity;
    public int routedOffsetQuantity;
    public int routedQuantity;
    public long routedMassGrams;

    public ProductionPreparedOutputPhysicalRouteSliceSaveData Clone() => new()
    {
        sourceStackId = sourceStackId,
        routedStackId = routedStackId,
        sourceOffsetQuantity = sourceOffsetQuantity,
        routedOffsetQuantity = routedOffsetQuantity,
        routedQuantity = routedQuantity,
        routedMassGrams = routedMassGrams
    };
}

[Serializable]
public sealed class ProductionPreparedOutputRoutingBatchSaveData
{
    public string batchCommitId = string.Empty;
    public string ownerBillId = string.Empty;
    public string ownerRecipeId = string.Empty;
    public string ownerFacilityId = string.Empty;
    public int cycleSequence;
    public string outcomeFingerprint = string.Empty;
    public string routingFingerprint = string.Empty;
    public string destinationId = string.Empty;
    public List<ProductionPreparedOutputRoutingLineSaveData> lines = new();

    public ProductionPreparedOutputRoutingBatchSaveData Clone() => new()
    {
        batchCommitId = batchCommitId,
        ownerBillId = ownerBillId,
        ownerRecipeId = ownerRecipeId,
        ownerFacilityId = ownerFacilityId,
        cycleSequence = cycleSequence,
        outcomeFingerprint = outcomeFingerprint,
        routingFingerprint = routingFingerprint,
        destinationId = destinationId,
        lines = (lines ?? new List<ProductionPreparedOutputRoutingLineSaveData>())
            .ConvertAll(value => value?.Clone())
    };
}

[Serializable]
public sealed class ProductionPreparedOutputRoutingSaveData
{
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public long lastConfirmedCheckpointSequence;
    public string lastConfirmedCheckpointDigest = string.Empty;
    public List<ProductionPreparedOutputRoutingBatchSaveData> batches = new();
}

public readonly struct ProductionPreparedOutputRoutingLineSnapshot
{
    public ProductionPreparedOutputRoutingLineSnapshot(
        string batchCommitId,
        string ownerBillId,
        string ownerRecipeId,
        string ownerFacilityId,
        int cycleSequence,
        string lineCommitId,
        string outputLineId,
        ProductionOutputRole role,
        string itemId,
        string destinationId,
        string componentFingerprint,
        int originalQuantity,
        long originalMassGrams,
        int remainingQuantity,
        long remainingMassGrams,
        int routedQuantity,
        long routedMassGrams)
    {
        BatchCommitId = batchCommitId;
        OwnerBillId = ownerBillId;
        OwnerRecipeId = ownerRecipeId;
        OwnerFacilityId = ownerFacilityId;
        CycleSequence = cycleSequence;
        LineCommitId = lineCommitId;
        OutputLineId = outputLineId;
        Role = role;
        ItemId = itemId;
        DestinationId = destinationId;
        ComponentFingerprint = componentFingerprint;
        OriginalQuantity = originalQuantity;
        OriginalMassGrams = originalMassGrams;
        RemainingQuantity = remainingQuantity;
        RemainingMassGrams = remainingMassGrams;
        RoutedQuantity = routedQuantity;
        RoutedMassGrams = routedMassGrams;
    }

    public string BatchCommitId { get; }
    public string OwnerBillId { get; }
    public string OwnerRecipeId { get; }
    public string OwnerFacilityId { get; }
    public int CycleSequence { get; }
    public string LineCommitId { get; }
    public string OutputLineId { get; }
    public ProductionOutputRole Role { get; }
    public string ItemId { get; }
    public string DestinationId { get; }
    public string ComponentFingerprint { get; }
    public int OriginalQuantity { get; }
    public long OriginalMassGrams { get; }
    public int RemainingQuantity { get; }
    public long RemainingMassGrams { get; }
    public int RoutedQuantity { get; }
    public long RoutedMassGrams { get; }
}

/// <summary>
/// Immutable gameplay projection of one prepared-output routing batch.
/// Save DTOs remain persistence-only and must not be consumed by destructive
/// facility orchestration.
/// </summary>
public sealed class ProductionPreparedOutputRoutingBatchSnapshot
{
    private readonly IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
        lines;
    private readonly IReadOnlyList<ProductionPreparedOutputRouteRequestSnapshot>
        routeOperations;
    private readonly IReadOnlyList<ProductionPreparedOutputPhysicalRouteReceipt>
        physicalReceipts;

    public ProductionPreparedOutputRoutingBatchSnapshot(
        string batchCommitId,
        string ownerBillId,
        string ownerRecipeId,
        string ownerFacilityId,
        int cycleSequence,
        string outcomeFingerprint,
        string routingFingerprint,
        string sourceDestinationId,
        IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot> lines,
        IReadOnlyList<ProductionPreparedOutputRouteRequestSnapshot>
            routeOperations,
        IReadOnlyList<ProductionPreparedOutputPhysicalRouteReceipt>
            physicalReceipts,
        bool isDrainAcknowledged)
    {
        BatchCommitId = batchCommitId ?? string.Empty;
        OwnerBillId = ownerBillId ?? string.Empty;
        OwnerRecipeId = ownerRecipeId ?? string.Empty;
        OwnerFacilityId = ownerFacilityId ?? string.Empty;
        CycleSequence = cycleSequence;
        OutcomeFingerprint = outcomeFingerprint ?? string.Empty;
        RoutingFingerprint = routingFingerprint ?? string.Empty;
        SourceDestinationId = sourceDestinationId ?? string.Empty;
        this.lines = Array.AsReadOnly((lines
                ?? Array.Empty<ProductionPreparedOutputRoutingLineSnapshot>())
            .ToArray());
        this.routeOperations = Array.AsReadOnly((routeOperations
                ?? Array.Empty<ProductionPreparedOutputRouteRequestSnapshot>())
            .ToArray());
        this.physicalReceipts = Array.AsReadOnly((physicalReceipts
                ?? Array.Empty<ProductionPreparedOutputPhysicalRouteReceipt>())
            .ToArray());
        IsDrainAcknowledged = isDrainAcknowledged;
    }

    public string BatchCommitId { get; }
    public string OwnerBillId { get; }
    public string OwnerRecipeId { get; }
    public string OwnerFacilityId { get; }
    public int CycleSequence { get; }
    public string OutcomeFingerprint { get; }
    public string RoutingFingerprint { get; }
    public string SourceDestinationId { get; }
    public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot> Lines =>
        lines ?? Array.Empty<ProductionPreparedOutputRoutingLineSnapshot>();
    public IReadOnlyList<ProductionPreparedOutputRouteRequestSnapshot>
        RouteOperations => routeOperations
            ?? Array.Empty<ProductionPreparedOutputRouteRequestSnapshot>();
    public IReadOnlyList<ProductionPreparedOutputPhysicalRouteReceipt>
        PhysicalReceipts => physicalReceipts
            ?? Array.Empty<ProductionPreparedOutputPhysicalRouteReceipt>();
    public bool IsDrainAcknowledged { get; }
    public int RemainingQuantity => Lines.Sum(value => value.RemainingQuantity);
    public long RemainingMassGrams => Lines.Sum(
        value => value.RemainingMassGrams);
}

public readonly struct ProductionPreparedOutputRouteRequestSnapshot
{
    public ProductionPreparedOutputRouteRequestSnapshot(
        string routeOperationId,
        string requestFingerprint,
        string batchCommitId,
        string lineCommitId,
        string outputLineId,
        string itemId,
        string componentFingerprint,
        string sourceDestinationId,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        int sourceOffsetQuantity,
        long sourceOffsetMassGrams,
        int routedQuantity,
        long routedMassGrams,
        ProductionPreparedOutputRoutePhase phase,
        string physicalReceiptFingerprint,
        long currentDeliveryRevision,
        ProductionPreparedOutputDeliveryTargetKind currentDeliveryTargetKind,
        string currentDeliveryRevisionFingerprint,
        string currentTargetDestinationId,
        int currentTargetPositionX,
        int currentTargetPositionY,
        string currentTargetAuthorityFingerprint)
    {
        RouteOperationId = routeOperationId;
        RequestFingerprint = requestFingerprint;
        BatchCommitId = batchCommitId;
        LineCommitId = lineCommitId;
        OutputLineId = outputLineId;
        ItemId = itemId;
        ComponentFingerprint = componentFingerprint;
        SourceDestinationId = sourceDestinationId;
        TargetDestinationId = targetDestinationId;
        TargetPositionX = targetPositionX;
        TargetPositionY = targetPositionY;
        SourceOffsetQuantity = sourceOffsetQuantity;
        SourceOffsetMassGrams = sourceOffsetMassGrams;
        RoutedQuantity = routedQuantity;
        RoutedMassGrams = routedMassGrams;
        Phase = phase;
        PhysicalReceiptFingerprint = physicalReceiptFingerprint;
        CurrentDeliveryRevision = currentDeliveryRevision;
        CurrentDeliveryTargetKind = currentDeliveryTargetKind;
        CurrentDeliveryRevisionFingerprint =
            currentDeliveryRevisionFingerprint;
        CurrentTargetDestinationId = currentTargetDestinationId;
        CurrentTargetPositionX = currentTargetPositionX;
        CurrentTargetPositionY = currentTargetPositionY;
        CurrentTargetAuthorityFingerprint = currentTargetAuthorityFingerprint;
    }

    public string RouteOperationId { get; }
    public string RequestFingerprint { get; }
    public string BatchCommitId { get; }
    public string LineCommitId { get; }
    public string OutputLineId { get; }
    public string ItemId { get; }
    public string ComponentFingerprint { get; }
    public string SourceDestinationId { get; }
    public string TargetDestinationId { get; }
    public int TargetPositionX { get; }
    public int TargetPositionY { get; }
    public int SourceOffsetQuantity { get; }
    public long SourceOffsetMassGrams { get; }
    public int RoutedQuantity { get; }
    public long RoutedMassGrams { get; }
    public ProductionPreparedOutputRoutePhase Phase { get; }
    public string PhysicalReceiptFingerprint { get; }
    public long CurrentDeliveryRevision { get; }
    public ProductionPreparedOutputDeliveryTargetKind CurrentDeliveryTargetKind
        { get; }
    public string CurrentDeliveryRevisionFingerprint { get; }
    public string CurrentTargetDestinationId { get; }
    public int CurrentTargetPositionX { get; }
    public int CurrentTargetPositionY { get; }
    public string CurrentTargetAuthorityFingerprint { get; }
}

public readonly struct ProductionPreparedOutputPhysicalRouteSliceReceipt
{
    public ProductionPreparedOutputPhysicalRouteSliceReceipt(
        string sourceStackId,
        string routedStackId,
        string outputLineId,
        string lineCommitId,
        string itemId,
        int sourceOffsetQuantity,
        int routedOffsetQuantity,
        int routedQuantity,
        long routedMassGrams,
        string componentFingerprint)
    {
        SourceStackId = sourceStackId;
        RoutedStackId = routedStackId;
        OutputLineId = outputLineId;
        LineCommitId = lineCommitId;
        ItemId = itemId;
        SourceOffsetQuantity = sourceOffsetQuantity;
        RoutedOffsetQuantity = routedOffsetQuantity;
        RoutedQuantity = routedQuantity;
        RoutedMassGrams = routedMassGrams;
        ComponentFingerprint = componentFingerprint;
    }

    public string SourceStackId { get; }
    public string RoutedStackId { get; }
    public string OutputLineId { get; }
    public string LineCommitId { get; }
    public string ItemId { get; }
    public int SourceOffsetQuantity { get; }
    public int RoutedOffsetQuantity { get; }
    public int RoutedQuantity { get; }
    public long RoutedMassGrams { get; }
    public string ComponentFingerprint { get; }
}

public readonly struct ProductionPreparedOutputPhysicalRouteReceipt
{
    public ProductionPreparedOutputPhysicalRouteReceipt(
        string routeOperationId,
        string requestFingerprint,
        string physicalReceiptFingerprint,
        string batchCommitId,
        string sourceDestinationId,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        int totalQuantity,
        long totalMassGrams,
        IReadOnlyList<ProductionPreparedOutputPhysicalRouteSliceReceipt> slices)
    {
        RouteOperationId = routeOperationId;
        RequestFingerprint = requestFingerprint;
        PhysicalReceiptFingerprint = physicalReceiptFingerprint;
        BatchCommitId = batchCommitId;
        SourceDestinationId = sourceDestinationId;
        TargetDestinationId = targetDestinationId;
        TargetPositionX = targetPositionX;
        TargetPositionY = targetPositionY;
        TotalQuantity = totalQuantity;
        TotalMassGrams = totalMassGrams;
        Slices = slices ?? Array.Empty<
            ProductionPreparedOutputPhysicalRouteSliceReceipt>();
    }

    public string RouteOperationId { get; }
    public string RequestFingerprint { get; }
    public string PhysicalReceiptFingerprint { get; }
    public string BatchCommitId { get; }
    public string SourceDestinationId { get; }
    public string TargetDestinationId { get; }
    public int TargetPositionX { get; }
    public int TargetPositionY { get; }
    public int TotalQuantity { get; }
    public long TotalMassGrams { get; }
    public IReadOnlyList<ProductionPreparedOutputPhysicalRouteSliceReceipt> Slices
        { get; }
}

public readonly struct ProductionPreparedOutputDeliveryRevisionSnapshot
{
    public ProductionPreparedOutputDeliveryRevisionSnapshot(
        string routeOperationId,
        long revision,
        ProductionPreparedOutputDeliveryTargetKind targetKind,
        ProductionPreparedOutputDeliveryRerouteReason reason,
        string rerouteOperationId,
        string previousRevisionFingerprint,
        string originalPhysicalReceiptFingerprint,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        string targetAuthorityFingerprint,
        string revisionFingerprint)
    {
        RouteOperationId = routeOperationId;
        Revision = revision;
        TargetKind = targetKind;
        Reason = reason;
        RerouteOperationId = rerouteOperationId;
        PreviousRevisionFingerprint = previousRevisionFingerprint;
        OriginalPhysicalReceiptFingerprint = originalPhysicalReceiptFingerprint;
        TargetDestinationId = targetDestinationId;
        TargetPositionX = targetPositionX;
        TargetPositionY = targetPositionY;
        TargetAuthorityFingerprint = targetAuthorityFingerprint;
        RevisionFingerprint = revisionFingerprint;
    }

    public string RouteOperationId { get; }
    public long Revision { get; }
    public ProductionPreparedOutputDeliveryTargetKind TargetKind { get; }
    public ProductionPreparedOutputDeliveryRerouteReason Reason { get; }
    public string RerouteOperationId { get; }
    public string PreviousRevisionFingerprint { get; }
    public string OriginalPhysicalReceiptFingerprint { get; }
    public string TargetDestinationId { get; }
    public int TargetPositionX { get; }
    public int TargetPositionY { get; }
    public string TargetAuthorityFingerprint { get; }
    public string RevisionFingerprint { get; }
}

public interface IProductionPreparedOutputDeliveryRerouteCandidate
{
    string RouteOperationId { get; }
    string RerouteOperationId { get; }
    long ExpectedCurrentRevision { get; }
    string ExpectedCurrentRevisionFingerprint { get; }
    string PreviousRevisionFingerprint { get; }
    long NextRevision { get; }
    string NextRevisionFingerprint { get; }
    string OriginalPhysicalReceiptFingerprint { get; }
    ProductionPreparedOutputDeliveryRerouteReason Reason { get; }
    string TargetDestinationId { get; }
    int TargetPositionX { get; }
    int TargetPositionY { get; }
    string TargetAuthorityFingerprint { get; }
}

public interface IProductionPreparedOutputDeliveryRerouteParticipant
{
    ProductionPreparedOutputDeliveryRevisionSnapshot CaptureCurrentDelivery(
        string routeOperationId);

    [GameplayInternalOnly(
        "Delivery reroutes must atomically coordinate Economy ownership with Items custody and haul intent.",
        "Prepared-output delivery reroute coordinator only")]
    IProductionPreparedOutputDeliveryRerouteCandidate PrepareDeliveryReroute(
        string routeOperationId,
        long expectedCurrentRevision,
        string expectedCurrentRevisionFingerprint,
        string originalPhysicalReceiptFingerprint,
        ProductionPreparedOutputDeliveryRerouteReason reason,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        string targetAuthorityFingerprint);

    [GameplayInternalOnly(
        "Publishes only a fully detached, prevalidated delivery revision image.",
        "Prepared-output delivery reroute coordinator only")]
    void PublishDeliveryReroute(
        IProductionPreparedOutputDeliveryRerouteCandidate candidate);

    [GameplayInternalOnly(
        "Rolls back an Economy delivery revision when the cross-domain reroute fails.",
        "Prepared-output delivery reroute coordinator only")]
    void RollbackDeliveryReroute(
        IProductionPreparedOutputDeliveryRerouteCandidate candidate);

    [GameplayInternalOnly(
        "Completes the Economy half only after the upper coordinator commits every participant.",
        "Prepared-output delivery reroute coordinator only")]
    void CompleteDeliveryReroute(
        IProductionPreparedOutputDeliveryRerouteCandidate candidate);
}

public enum ProductionPreparedOutputDeliveryCoordinationStatus
{
    Applied = 1,
    Replay = 2,
    Deferred = 3,
    Rejected = 4
}

public enum ProductionPreparedOutputDeliveryCoordinationReason
{
    None = 0,
    AuthorityBusy = 1,
    PhysicalStateNotStable = 2,
    TargetAuthorityUnavailable = 3,
    TargetCapacityUnavailable = 4,
    NoCompatibleWarehouse = 5,
    AdmissionUnavailable = 6,
    AuthorityConflict = 7
}

public readonly struct ProductionPreparedOutputDeliveryCoordinationResult
{
    public ProductionPreparedOutputDeliveryCoordinationResult(
        ProductionPreparedOutputDeliveryCoordinationStatus status,
        ProductionPreparedOutputDeliveryCoordinationReason reason,
        string routeOperationId,
        string rerouteOperationId,
        long revision,
        string revisionFingerprint,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        string message)
    {
        Status = status;
        Reason = reason;
        RouteOperationId = routeOperationId ?? string.Empty;
        RerouteOperationId = rerouteOperationId ?? string.Empty;
        Revision = revision;
        RevisionFingerprint = revisionFingerprint ?? string.Empty;
        TargetDestinationId = targetDestinationId ?? string.Empty;
        TargetPositionX = targetPositionX;
        TargetPositionY = targetPositionY;
        Message = message ?? string.Empty;
    }

    public ProductionPreparedOutputDeliveryCoordinationStatus Status { get; }
    public ProductionPreparedOutputDeliveryCoordinationReason Reason { get; }
    public string RouteOperationId { get; }
    public string RerouteOperationId { get; }
    public long Revision { get; }
    public string RevisionFingerprint { get; }
    public string TargetDestinationId { get; }
    public int TargetPositionX { get; }
    public int TargetPositionY { get; }
    public string Message { get; }
    public bool Succeeded => Status is
        ProductionPreparedOutputDeliveryCoordinationStatus.Applied or
        ProductionPreparedOutputDeliveryCoordinationStatus.Replay;
}

/// <summary>
/// Upper transaction boundary for one prepared-output delivery revision.  The
/// implementation coordinates the Economy revision, the Items custody/outbox
/// overlay, and destination gram admission.  Callers never publish an
/// individual participant directly.
/// </summary>
public interface IProductionPreparedOutputDeliveryCoordinator
{
    ProductionPreparedOutputDeliveryCoordinationResult TryApplyExactTarget(
        string routeOperationId,
        ProductionPreparedOutputDeliveryRerouteReason reason,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY);

    ProductionPreparedOutputDeliveryCoordinationResult
        TryApplyCompatibleWarehouse(
            string routeOperationId,
            string itemId,
            int originPositionX,
            int originPositionY);
}

public interface IProductionPreparedOutputRoutingAuthority
{
    void PublishCommittedBatch(
        ProductionPreparedOutputBatchSaveData completedBatch,
        BuildingInstanceId ownerFacilityId);

    IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot> CaptureAll();

    IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot> CaptureBill(
        ProductionBillId ownerBillId);

    IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot> CaptureDestination(
        string destinationId);

    bool HasOutstandingForBill(ProductionBillId ownerBillId);

    bool CanRetireBill(ProductionBillId ownerBillId);

    ProductionPreparedOutputRouteRequestSnapshot PrepareRoute(
        string batchCommitId,
        string lineCommitId,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        int routedQuantity);

    IReadOnlyList<ProductionPreparedOutputRouteRequestSnapshot>
        CaptureRouteOperations();

    void CommitPhysicalRoute(
        ProductionPreparedOutputPhysicalRouteReceipt receipt);

    void AcknowledgePhysicalRoute(
        string routeOperationId,
        string physicalReceiptFingerprint);

}

public interface IProductionPreparedOutputRoutingBatchQuery
{
    bool TryCaptureBatch(
        string batchCommitId,
        out ProductionPreparedOutputRoutingBatchSnapshot snapshot);
}

public enum ProductionPreparedOutputExactRouteLifecycleStatus
{
    Applied = 1,
    Replay = 2,
    Deferred = 3,
    Conflict = 4
}

public enum ProductionPreparedOutputExactRouteLifecycleReason
{
    None = 0,
    RouteUnavailable = 1,
    PhysicalAcknowledgeUnavailable = 2,
    DeliveryAuthorityUnavailable = 3,
    RouteSnapshotMissing = 4,
    RouteSnapshotConflict = 5
}

/// <summary>
/// Typed result from the one shared prepared-output exact-route lifecycle.
/// Applied and Replay mean the route has reached a durable delivery authority;
/// partial physical progress is reported as Deferred so callers cannot retire
/// an owner whose destination admission is still incomplete.
/// </summary>
public readonly struct ProductionPreparedOutputExactRouteLifecycleResult
{
    public ProductionPreparedOutputExactRouteLifecycleResult(
        ProductionPreparedOutputExactRouteLifecycleStatus status,
        ProductionPreparedOutputExactRouteLifecycleReason reason,
        string routeOperationId,
        string physicalReceiptFingerprint,
        string targetAuthorityFingerprint,
        string message)
    {
        if (!Enum.IsDefined(
                typeof(ProductionPreparedOutputExactRouteLifecycleStatus),
                status)
            || !Enum.IsDefined(
                typeof(ProductionPreparedOutputExactRouteLifecycleReason),
                reason))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Status = status;
        Reason = reason;
        RouteOperationId = routeOperationId ?? string.Empty;
        PhysicalReceiptFingerprint = physicalReceiptFingerprint
            ?? string.Empty;
        TargetAuthorityFingerprint = targetAuthorityFingerprint
            ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public ProductionPreparedOutputExactRouteLifecycleStatus Status { get; }
    public ProductionPreparedOutputExactRouteLifecycleReason Reason { get; }
    public string RouteOperationId { get; }
    public string PhysicalReceiptFingerprint { get; }
    public string TargetAuthorityFingerprint { get; }
    public string Message { get; }
    public bool Completed => Status is
        ProductionPreparedOutputExactRouteLifecycleStatus.Applied or
        ProductionPreparedOutputExactRouteLifecycleStatus.Replay;
}

/// <summary>
/// Shared transaction boundary used by ordinary production and destructive
/// capacity drains. It owns Items route publication, Economy acknowledgement,
/// and final destination gram-admission authority as one replayable lifecycle.
/// </summary>
public interface IProductionPreparedOutputExactRouteLifecycle
{
    [GameplayInternalOnly(
        "Prepared-output routing must commit Items, Economy and destination gram authority through one lifecycle.",
        "Production distribution and destructive capacity-drain coordinators only")]
    ProductionPreparedOutputExactRouteLifecycleResult TryProgress(
        ProductionPreparedOutputRouteRequestSnapshot operation);

    int ResolveExactQuantity(
        ProductionPreparedOutputRoutingLineSnapshot line,
        int requestedQuantity);
}
