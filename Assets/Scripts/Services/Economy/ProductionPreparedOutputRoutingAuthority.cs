using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public interface IProductionPreparedOutputRoutingPersistence
{
    ProductionPreparedOutputRoutingSaveData Capture();
    ProductionPreparedOutputRoutingSaveData BuildRestoreCandidate(
        ProductionPreparedOutputRoutingSaveData source);
    void Restore(ProductionPreparedOutputRoutingSaveData candidate);
}

public sealed class ProductionPreparedOutputRoutingAuthority :
    IProductionPreparedOutputRoutingAuthority,
    IProductionPreparedOutputRoutingBatchQuery,
    IProductionPreparedOutputDeliveryRerouteParticipant,
    IProductionPreparedOutputRoutingPersistence,
    IProductionPreparedOutputRoutingRestoreReconciler,
    IPreparedOutputCheckpointGcParticipant,
    IDungeonRestoreTransactionParticipant
{
    private const string RequestV1 = "facility-output-exact-route-request-v1";
    private const string ReceiptV1 = "facility-output-exact-route-receipt-v1";
    private const string RoutingV9 = "prepared-output-routing-v9";
    private const string DeliveryRevisionV1 =
        "prepared-output-delivery-revision-v1";
    private const string DeliveryRerouteOperationV1 =
        "prepared-output-delivery-reroute-operation-v1";

    private Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
        batches = new(StringComparer.Ordinal);
    private Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
        stagedBatches;
    private Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
        rollbackBatches;
    private long checkpointSequence;
    private string checkpointDigest = string.Empty;
    private long routingRevision;
    private long stagedCheckpointSequence;
    private string stagedCheckpointDigest = string.Empty;
    private long rollbackCheckpointSequence;
    private string rollbackCheckpointDigest = string.Empty;
    private bool restoreActive;
    private bool restorePublished;

    public string ParticipantId => "540.economy.prepared-output-routing";
    public string CheckpointGcParticipantId => ParticipantId;
    public PreparedOutputCheckpointGcParticipantKind CheckpointGcParticipantKind =>
        PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority;
    public long LastConfirmedCheckpointSequence => checkpointSequence;
    public string LastConfirmedSerializedByteDigest => checkpointDigest;

    public void PublishCommittedBatch(
        ProductionPreparedOutputBatchSaveData completedBatch,
        BuildingInstanceId ownerFacilityId)
    {
        ProductionPreparedOutputRoutingBatchSaveData candidate =
            BuildCommittedBatch(completedBatch, ownerFacilityId);
        if (batches.TryGetValue(
                candidate.batchCommitId,
                out ProductionPreparedOutputRoutingBatchSaveData existing))
        {
            if (!string.Equals(existing.routingFingerprint,
                    candidate.routingFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Prepared-output routing replay conflicts for '{candidate.batchCommitId}'.");
            }
            return;
        }
        batches.Add(candidate.batchCommitId, candidate);
        routingRevision = checked(routingRevision + 1L);
    }

    public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot> CaptureAll() =>
        CaptureWhere(_ => true);

    public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot> CaptureBill(
        ProductionBillId ownerBillId)
    {
        RequireBill(ownerBillId);
        return CaptureWhere(batch => string.Equals(
            batch.ownerBillId, ownerBillId.Value, StringComparison.Ordinal));
    }

    public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
        CaptureDestination(string destinationId)
    {
        RequireCanonical(destinationId, nameof(destinationId));
        return CaptureWhere(batch => string.Equals(
            batch.destinationId, destinationId, StringComparison.Ordinal));
    }

    public bool HasOutstandingForBill(ProductionBillId ownerBillId)
    {
        RequireBill(ownerBillId);
        return batches.Values
            .Where(batch => string.Equals(
                batch.ownerBillId, ownerBillId.Value, StringComparison.Ordinal))
            .Any(batch => !IsDrainAcknowledged(batch));
    }

    public bool CanRetireBill(ProductionBillId ownerBillId)
    {
        RequireBill(ownerBillId);
        return !HasOutstandingForBill(ownerBillId);
    }

    public IReadOnlyList<ProductionPreparedOutputRouteRequestSnapshot>
        CaptureRouteOperations() => batches.Values
        .OrderBy(batch => batch.batchCommitId, StringComparer.Ordinal)
        .SelectMany(batch => batch.lines
            .OrderBy(line => line.lineCommitId, StringComparer.Ordinal)
            .SelectMany(line => line.routeOperations
                .OrderBy(operation => operation.sourceOffsetQuantity)
                .Select(operation => Snapshot(batch, line, operation))))
        .ToArray();

    public bool TryCaptureBatch(
        string batchCommitId,
        out ProductionPreparedOutputRoutingBatchSnapshot snapshot)
    {
        RequireCanonical(batchCommitId, nameof(batchCommitId));
        if (!batches.TryGetValue(
                batchCommitId,
                out ProductionPreparedOutputRoutingBatchSaveData batch))
        {
            snapshot = null;
            return false;
        }

        ProductionPreparedOutputRoutingLineSnapshot[] lines = batch.lines
            .OrderBy(line => line.lineCommitId, StringComparer.Ordinal)
            .Select(line => Snapshot(batch, line))
            .ToArray();
        ProductionPreparedOutputRouteRequestSnapshot[] operations = batch.lines
            .OrderBy(line => line.lineCommitId, StringComparer.Ordinal)
            .SelectMany(line => line.routeOperations
                .OrderBy(operation => operation.sourceOffsetQuantity)
                .ThenBy(operation => operation.routeOperationId,
                    StringComparer.Ordinal)
                .Select(operation => Snapshot(batch, line, operation)))
            .ToArray();
        ProductionPreparedOutputPhysicalRouteReceipt[] receipts = batch.lines
            .OrderBy(line => line.lineCommitId, StringComparer.Ordinal)
            .SelectMany(line => line.routeOperations
                .Where(operation => !string.IsNullOrEmpty(
                    operation.physicalReceiptFingerprint))
                .OrderBy(operation => operation.sourceOffsetQuantity)
                .ThenBy(operation => operation.routeOperationId,
                    StringComparer.Ordinal)
                .Select(operation => SnapshotReceipt(batch, line, operation)))
            .ToArray();
        ProductionPreparedOutputNonPhysicalDispositionSnapshot[] dispositions =
            batch.nonPhysicalDispositions
                .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                .Select(Snapshot)
                .ToArray();
        snapshot = new ProductionPreparedOutputRoutingBatchSnapshot(
            batch.batchCommitId,
            batch.ownerBillId,
            batch.ownerRecipeId,
            batch.ownerFacilityId,
            batch.cycleSequence,
            batch.outcomeFingerprint,
            batch.routingFingerprint,
            batch.destinationId,
            lines,
            operations,
            receipts,
            IsDrainAcknowledged(batch),
            dispositions);
        return true;
    }

    public ProductionPreparedOutputRouteRequestSnapshot PrepareRoute(
        string batchCommitId,
        string lineCommitId,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        int routedQuantity)
    {
        RequireCanonical(batchCommitId, nameof(batchCommitId));
        RequireCanonical(lineCommitId, nameof(lineCommitId));
        RequireCanonicalOptional(
            targetDestinationId,
            nameof(targetDestinationId));
        if (routedQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(routedQuantity));

        Context context = FindLine(batchCommitId, lineCommitId);
        ProductionPreparedOutputRouteOperationSaveData active =
            context.Line.routeOperations.SingleOrDefault(operation =>
                operation.phase != ProductionPreparedOutputRoutePhase
                    .ItemsAcknowledgedAwaitingCheckpointGc);
        if (active != null)
        {
            if (!string.Equals(active.targetDestinationId,
                    targetDestinationId, StringComparison.Ordinal)
                || active.targetPositionX != targetPositionX
                || active.targetPositionY != targetPositionY
                || active.routedQuantity != routedQuantity)
            {
                throw new InvalidOperationException(
                    $"Prepared-output route conflicts with pending operation '{active.routeOperationId}'.");
            }
            return Snapshot(context.Batch, context.Line, active);
        }
        if (routedQuantity > context.Line.remainingQuantity)
        {
            throw new InvalidOperationException(
                $"Prepared-output route exceeds line '{lineCommitId}' remainder.");
        }
        long massNumerator = checked(
            context.Line.originalMassGrams * routedQuantity);
        if (massNumerator % context.Line.originalQuantity != 0L)
        {
            throw new InvalidOperationException(
                $"Prepared-output route cannot represent exact grams for '{lineCommitId}'.");
        }

        ProductionPreparedOutputRouteOperationSaveData operation = new()
        {
            routeOperationId = BuildOperationId(
                context.Batch.batchCommitId,
                context.Line.lineCommitId,
                context.Line.routedQuantity),
            phase = ProductionPreparedOutputRoutePhase.PhysicalPending,
            sourceOffsetQuantity = context.Line.routedQuantity,
            sourceOffsetMassGrams = context.Line.routedMassGrams,
            routedQuantity = routedQuantity,
            routedMassGrams = massNumerator / context.Line.originalQuantity,
            targetDestinationId = targetDestinationId,
            targetPositionX = targetPositionX,
            targetPositionY = targetPositionY,
            physicalReceiptFingerprint = string.Empty,
            physicalSlices = new(),
            deliveryRevisions = new()
        };
        operation.requestFingerprint = RequestFingerprint(
            context.Batch, context.Line, operation);
        operation.deliveryRevisions.Add(BuildInitialDeliveryRevision(operation));
        if (context.Line.routeOperations.Any(value => string.Equals(
                value.routeOperationId, operation.routeOperationId,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Prepared-output route operation '{operation.routeOperationId}' is duplicated.");
        }
        context.Line.routeOperations.Add(operation);
        routingRevision = checked(routingRevision + 1L);
        return Snapshot(context.Batch, context.Line, operation);
    }

    public void CommitPhysicalRoute(
        ProductionPreparedOutputPhysicalRouteReceipt receipt)
    {
        CommitPhysicalRoute(batches, receipt);
        routingRevision = checked(routingRevision + 1L);
    }

    public void CommitRestoredPhysicalRoute(
        ProductionPreparedOutputPhysicalRouteReceipt receipt)
    {
        if (!restoreActive || restorePublished || stagedBatches == null)
        {
            throw new InvalidOperationException(
                "Prepared-output restored physical route can only reconcile a detached candidate.");
        }
        CommitPhysicalRoute(stagedBatches, receipt);
    }

    private static void CommitPhysicalRoute(
        IReadOnlyDictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
            source,
        ProductionPreparedOutputPhysicalRouteReceipt receipt)
    {
        RequireCanonical(receipt.RouteOperationId, nameof(receipt.RouteOperationId));
        RequireDigest(receipt.RequestFingerprint, nameof(receipt.RequestFingerprint));
        RequireDigest(receipt.PhysicalReceiptFingerprint,
            nameof(receipt.PhysicalReceiptFingerprint));
        if (!string.Equals(receipt.PhysicalReceiptFingerprint,
                ComputePhysicalReceiptFingerprint(receipt),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Prepared-output physical receipt fingerprint conflicts for '{receipt.RouteOperationId}'.");
        }

        Context context = FindOperation(source, receipt.RouteOperationId);
        ValidateDeliveryRevisions(context.Operation);
        ValidateReceipt(context, receipt);
        if (context.Operation.phase !=
            ProductionPreparedOutputRoutePhase.PhysicalPending)
        {
            if (!string.Equals(context.Operation.physicalReceiptFingerprint,
                    receipt.PhysicalReceiptFingerprint,
                    StringComparison.Ordinal)
                || !SameSlices(context.Operation.physicalSlices, receipt.Slices))
            {
                throw new InvalidOperationException(
                    $"Prepared-output receipt replay conflicts for '{receipt.RouteOperationId}'.");
            }
            return;
        }

        int nextRoutedQuantity = checked(
            context.Line.routedQuantity + context.Operation.routedQuantity);
        long nextRoutedMass = checked(
            context.Line.routedMassGrams + context.Operation.routedMassGrams);
        int nextRemainingQuantity = checked(
            context.Line.remainingQuantity - context.Operation.routedQuantity);
        long nextRemainingMass = checked(
            context.Line.remainingMassGrams - context.Operation.routedMassGrams);
        if (nextRemainingQuantity < 0 || nextRemainingMass < 0L
            || nextRoutedQuantity + nextRemainingQuantity
                != context.Line.originalQuantity
            || nextRoutedMass + nextRemainingMass
                != context.Line.originalMassGrams)
        {
            throw new InvalidOperationException(
                $"Prepared-output receipt overdraws '{context.Line.lineCommitId}'.");
        }

        context.Operation.physicalReceiptFingerprint =
            receipt.PhysicalReceiptFingerprint;
        context.Operation.physicalSlices = receipt.Slices
            .OrderBy(value => value.SourceOffsetQuantity)
            .ThenBy(value => value.SourceStackId, StringComparer.Ordinal)
            .ThenBy(value => value.RoutedStackId, StringComparer.Ordinal)
            .ThenBy(value => value.RoutedOffsetQuantity)
            .Select(value => new ProductionPreparedOutputPhysicalRouteSliceSaveData
            {
                sourceStackId = value.SourceStackId,
                routedStackId = value.RoutedStackId,
                sourceOffsetQuantity = value.SourceOffsetQuantity,
                routedOffsetQuantity = value.RoutedOffsetQuantity,
                routedQuantity = value.RoutedQuantity,
                routedMassGrams = value.RoutedMassGrams
            })
            .ToList();
        context.Operation.phase = ProductionPreparedOutputRoutePhase
            .PhysicalAppliedAwaitingItemsAck;
        FinalizeInitialDeliveryRevision(context.Operation);
        context.Line.routedQuantity = nextRoutedQuantity;
        context.Line.routedMassGrams = nextRoutedMass;
        context.Line.remainingQuantity = nextRemainingQuantity;
        context.Line.remainingMassGrams = nextRemainingMass;
    }

    public void AcknowledgePhysicalRoute(
        string routeOperationId,
        string physicalReceiptFingerprint)
    {
        RequireCanonical(routeOperationId, nameof(routeOperationId));
        RequireDigest(physicalReceiptFingerprint, nameof(physicalReceiptFingerprint));
        Context context = FindOperation(routeOperationId);
        if (!string.Equals(context.Operation.physicalReceiptFingerprint,
                physicalReceiptFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Prepared-output acknowledgement conflicts for '{routeOperationId}'.");
        }
        if (context.Operation.phase ==
            ProductionPreparedOutputRoutePhase.PhysicalPending)
        {
            throw new InvalidOperationException(
                $"Prepared-output route '{routeOperationId}' is not physically applied.");
        }
        context.Operation.phase = ProductionPreparedOutputRoutePhase
            .ItemsAcknowledgedAwaitingCheckpointGc;
        routingRevision = checked(routingRevision + 1L);
    }

    ProductionPreparedOutputDeliveryRevisionSnapshot
        IProductionPreparedOutputDeliveryRerouteParticipant.CaptureCurrentDelivery(
        string routeOperationId)
    {
        RequireCanonical(routeOperationId, nameof(routeOperationId));
        Context context = FindOperation(routeOperationId);
        ProductionPreparedOutputDeliveryRevisionSaveData current =
            CurrentDeliveryRevision(context.Operation);
        return DeliverySnapshot(routeOperationId, current);
    }

    IProductionPreparedOutputDeliveryRerouteCandidate
        IProductionPreparedOutputDeliveryRerouteParticipant.PrepareDeliveryReroute(
        string routeOperationId,
        long expectedCurrentRevision,
        string expectedCurrentRevisionFingerprint,
        string originalPhysicalReceiptFingerprint,
        ProductionPreparedOutputDeliveryRerouteReason reason,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        string targetAuthorityFingerprint)
    {
        RequireCanonical(routeOperationId, nameof(routeOperationId));
        RequireDigest(expectedCurrentRevisionFingerprint,
            nameof(expectedCurrentRevisionFingerprint));
        RequireDigest(originalPhysicalReceiptFingerprint,
            nameof(originalPhysicalReceiptFingerprint));
        RequireCanonical(targetDestinationId, nameof(targetDestinationId));
        RequireDigest(targetAuthorityFingerprint,
            nameof(targetAuthorityFingerprint));
        if (expectedCurrentRevision < 0L
            || !Enum.IsDefined(typeof(ProductionPreparedOutputDeliveryRerouteReason),
                reason)
            || reason == ProductionPreparedOutputDeliveryRerouteReason.InitialRoute)
        {
            throw new InvalidOperationException(
                "Prepared-output delivery reroute command is invalid.");
        }

        Context live = FindOperation(routeOperationId);
        if (live.Operation.phase != ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc)
        {
            throw new InvalidOperationException(
                $"Delivery reroute requires a routable owner '{routeOperationId}'.");
        }
        if (!string.Equals(live.Operation.physicalReceiptFingerprint,
                originalPhysicalReceiptFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Delivery reroute changed original receipt '{routeOperationId}'.");
        }
        ValidateDeliveryRevisions(live.Operation);

        ProductionPreparedOutputDeliveryRevisionSaveData[] revisions =
            live.Operation.deliveryRevisions
                .OrderBy(value => value.revision)
                .ToArray();
        ProductionPreparedOutputDeliveryRevisionSaveData expected = revisions
            .SingleOrDefault(value => value.revision == expectedCurrentRevision);
        if (expected == null
            || !string.Equals(expected.revisionFingerprint,
                expectedCurrentRevisionFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Delivery reroute expected revision conflicts for '{routeOperationId}'.");
        }

        ProductionPreparedOutputDeliveryRevisionSaveData proposed =
            BuildRerouteDeliveryRevision(
                routeOperationId,
                live.Operation.requestFingerprint,
                expected,
                originalPhysicalReceiptFingerprint,
                reason,
                targetDestinationId,
                targetPositionX,
                targetPositionY,
                targetAuthorityFingerprint);
        ProductionPreparedOutputDeliveryRevisionSaveData existingNext = revisions
            .SingleOrDefault(value => value.revision == proposed.revision);
        if (existingNext != null)
        {
            if (!SameDeliveryRevision(existingNext, proposed))
            {
                throw new InvalidOperationException(
                    $"Delivery reroute revision '{proposed.revision}' conflicts for '{routeOperationId}'.");
            }
            return new DeliveryRerouteCandidate(
                routeOperationId,
                expected,
                existingNext,
                routingRevision,
                batches,
                batches,
                routingRevision,
                alreadyApplied: true);
        }

        ProductionPreparedOutputDeliveryRevisionSaveData current = revisions[^1];
        if (current.revision != expectedCurrentRevision
            || !string.Equals(current.revisionFingerprint,
                expectedCurrentRevisionFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Delivery reroute cannot fork a non-current revision for '{routeOperationId}'.");
        }

        Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData> next =
            CloneBatchMap(batches);
        Context staged = FindOperation(next, routeOperationId);
        staged.Operation.deliveryRevisions.Add(proposed.Clone());
        ValidateDeliveryRevisions(staged.Operation);
        long nextRoutingRevision = checked(routingRevision + 1L);
        return new DeliveryRerouteCandidate(
            routeOperationId,
            expected,
            proposed,
            routingRevision,
            batches,
            next,
            nextRoutingRevision,
            alreadyApplied: false);
    }

    void IProductionPreparedOutputDeliveryRerouteParticipant.PublishDeliveryReroute(
        IProductionPreparedOutputDeliveryRerouteCandidate candidate)
    {
        DeliveryRerouteCandidate exact = RequireDeliveryRerouteCandidate(candidate);
        if (exact.Completed)
            throw new InvalidOperationException("Delivery reroute candidate is completed.");
        if (exact.Published)
            return;
        if (exact.AlreadyApplied)
        {
            Context replay = FindOperation(exact.RouteOperationId);
            ProductionPreparedOutputDeliveryRevisionSaveData revision = replay
                .Operation.deliveryRevisions.SingleOrDefault(value =>
                    value.revision == exact.NextRevision);
            if (revision == null || !SameDeliveryRevision(revision, exact.Next))
            {
                throw new InvalidOperationException(
                    "Delivery reroute replay no longer matches live authority.");
            }
            exact.Published = true;
            return;
        }
        if (routingRevision != exact.SourceRoutingRevision
            || !ReferenceEquals(batches, exact.PreviousBatches))
        {
            throw new InvalidOperationException(
                "Prepared-output routing changed after reroute preparation.");
        }
        batches = exact.NextBatches;
        routingRevision = exact.NextRoutingRevision;
        exact.Published = true;
    }

    void IProductionPreparedOutputDeliveryRerouteParticipant.RollbackDeliveryReroute(
        IProductionPreparedOutputDeliveryRerouteCandidate candidate)
    {
        DeliveryRerouteCandidate exact = RequireDeliveryRerouteCandidate(candidate);
        if (exact.Completed || !exact.Published || exact.AlreadyApplied)
            return;
        if (!ReferenceEquals(batches, exact.NextBatches)
            || routingRevision != exact.NextRoutingRevision)
        {
            throw new InvalidOperationException(
                "Delivery reroute rollback authority changed.");
        }
        batches = exact.PreviousBatches;
        routingRevision = exact.SourceRoutingRevision;
        exact.Published = false;
    }

    void IProductionPreparedOutputDeliveryRerouteParticipant.CompleteDeliveryReroute(
        IProductionPreparedOutputDeliveryRerouteCandidate candidate)
    {
        DeliveryRerouteCandidate exact = RequireDeliveryRerouteCandidate(candidate);
        if (!exact.Published)
            throw new InvalidOperationException(
                "Delivery reroute cannot complete before publication.");
        exact.Completed = true;
    }

    public void AcknowledgeRestoredRoute(
        string routeOperationId,
        string physicalReceiptFingerprint)
    {
        RequireCanonical(routeOperationId, nameof(routeOperationId));
        RequireDigest(physicalReceiptFingerprint, nameof(physicalReceiptFingerprint));
        if (!restoreActive || restorePublished || stagedBatches == null)
        {
            throw new InvalidOperationException(
                "Prepared-output restored route can only reconcile a detached candidate.");
        }
        Context context = FindOperation(stagedBatches, routeOperationId);
        if (!string.Equals(context.Operation.physicalReceiptFingerprint,
                physicalReceiptFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Prepared-output restored acknowledgement conflicts for '{routeOperationId}'.");
        }
        if (context.Operation.phase == ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc)
            return;
        if (context.Operation.phase != ProductionPreparedOutputRoutePhase
                .PhysicalAppliedAwaitingItemsAck)
        {
            throw new InvalidOperationException(
                $"Prepared-output restored route phase conflicts for '{routeOperationId}'.");
        }
        context.Operation.phase = ProductionPreparedOutputRoutePhase
            .ItemsAcknowledgedAwaitingCheckpointGc;
    }

    PreparedOutputCheckpointGcResult IPreparedOutputCheckpointGcParticipant
        .PrepareCheckpointGarbageCollection(
        PreparedOutputCheckpointGcContext context,
        out IPreparedOutputCheckpointGcCandidate candidate)
    {
        candidate = null;
        if (context.CheckpointSequence < checkpointSequence)
        {
            return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                PreparedOutputCheckpointGcReason.StaleCheckpoint,
                context,
                "Prepared-output checkpoint sequence moved backwards.");
        }
        if (context.CheckpointSequence == checkpointSequence)
        {
            if (!string.Equals(checkpointDigest,
                    context.SerializedByteDigest,
                    StringComparison.Ordinal))
            {
                return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                    PreparedOutputCheckpointGcReason.ReplayDigestMismatch,
                    context,
                    "Prepared-output checkpoint replay changed serialized bytes.");
            }
            return GcResult(PreparedOutputCheckpointGcStatus.AlreadyApplied,
                PreparedOutputCheckpointGcReason.None,
                context,
                "Prepared-output checkpoint was already applied.");
        }
        if (context.CheckpointSequence != checked(checkpointSequence + 1L))
        {
            return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                PreparedOutputCheckpointGcReason.StaleCheckpoint,
                context,
                "Prepared-output checkpoint sequence is not contiguous.");
        }

        ProductionPreparedOutputRoutingBatchSaveData[] collected = batches.Values
            .Where(IsDrainAcknowledged)
            .OrderBy(value => value.batchCommitId, StringComparer.Ordinal)
            .ToArray();
        string[] batchIds = collected
            .Select(value => value.batchCommitId)
            .ToArray();
        string[] operationIds = collected
            .SelectMany(batch => batch.lines)
            .SelectMany(line => line.routeOperations)
            .Select(operation => operation.routeOperationId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData> next =
            batches.Values
                .Where(value => !batchIds.Contains(
                    value.batchCommitId,
                    StringComparer.Ordinal))
                .OrderBy(value => value.batchCommitId, StringComparer.Ordinal)
                .ToDictionary(
                    value => value.batchCommitId,
                    value => value.Clone(),
                    StringComparer.Ordinal);
        candidate = new CheckpointGcCandidate(
            context,
            routingRevision,
            batches,
            checkpointSequence,
            checkpointDigest,
            next,
            batchIds,
            operationIds);
        return GcResult(PreparedOutputCheckpointGcStatus.Applied,
            collected.Length == 0
                ? PreparedOutputCheckpointGcReason.NoEligibleWholeBatch
                : PreparedOutputCheckpointGcReason.None,
            context,
            collected.Length == 0
                ? "Checkpoint advances without an eligible whole batch."
                : "Prepared-output whole-batch GC candidate is detached.",
            collected.Length);
    }

    PreparedOutputCheckpointGcResult IPreparedOutputCheckpointGcParticipant
        .PublishCheckpointGarbageCollection(
        IPreparedOutputCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireGcCandidate(candidate);
        if (exact.Completed)
            throw new InvalidOperationException("Checkpoint GC candidate is completed.");
        if (exact.Published)
        {
            return GcResult(PreparedOutputCheckpointGcStatus.AlreadyApplied,
                PreparedOutputCheckpointGcReason.None,
                exact.Context,
                "Prepared-output checkpoint candidate is already published.",
                exact.BatchCommitIds.Count);
        }
        if (routingRevision != exact.SourceRevision
            || !ReferenceEquals(batches, exact.PreviousBatches)
            || checkpointSequence != exact.PreviousSequence
            || !string.Equals(checkpointDigest,
                exact.PreviousDigest,
                StringComparison.Ordinal))
        {
            return GcResult(PreparedOutputCheckpointGcStatus.Deferred,
                PreparedOutputCheckpointGcReason.LiveAuthorityChanged,
                exact.Context,
                "Prepared-output routing changed after checkpoint preparation.");
        }
        long nextRoutingRevision = checked(exact.SourceRevision + 1L);

        batches = exact.NextBatches;
        checkpointSequence = exact.Context.CheckpointSequence;
        checkpointDigest = exact.Context.SerializedByteDigest;
        routingRevision = nextRoutingRevision;
        exact.Published = true;
        return GcResult(PreparedOutputCheckpointGcStatus.Applied,
            PreparedOutputCheckpointGcReason.None,
            exact.Context,
            "Prepared-output checkpoint candidate was published.",
            exact.BatchCommitIds.Count);
    }

    void IPreparedOutputCheckpointGcParticipant
        .RollbackCheckpointGarbageCollection(
        IPreparedOutputCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireGcCandidate(candidate);
        if (exact.Completed || !exact.Published)
            return;
        if (!ReferenceEquals(batches, exact.NextBatches)
            || checkpointSequence != exact.Context.CheckpointSequence
            || !string.Equals(checkpointDigest,
                exact.Context.SerializedByteDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Prepared-output checkpoint rollback authority changed.");
        }
        batches = exact.PreviousBatches;
        checkpointSequence = exact.PreviousSequence;
        checkpointDigest = exact.PreviousDigest;
        routingRevision = exact.SourceRevision;
        exact.Published = false;
    }

    void IPreparedOutputCheckpointGcParticipant
        .CompleteCheckpointGarbageCollection(
        IPreparedOutputCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireGcCandidate(candidate);
        exact.Completed = true;
    }

    public ProductionPreparedOutputRoutingSaveData Capture() => new()
    {
        version = ProductionPreparedOutputRoutingSaveData.CurrentVersion,
        lastConfirmedCheckpointSequence = checkpointSequence,
        lastConfirmedCheckpointDigest = checkpointDigest,
        batches = batches.Values
            .OrderBy(value => value.batchCommitId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToList()
    };

    public ProductionPreparedOutputRoutingSaveData BuildRestoreCandidate(
        ProductionPreparedOutputRoutingSaveData source)
    {
        ValidateSave(source);
        return new ProductionPreparedOutputRoutingSaveData
        {
            version = source.version,
            lastConfirmedCheckpointSequence = source.lastConfirmedCheckpointSequence,
            lastConfirmedCheckpointDigest = source.lastConfirmedCheckpointDigest,
            batches = source.batches.Select(value => value.Clone()).ToList()
        };
    }

    public void Restore(ProductionPreparedOutputRoutingSaveData candidate)
    {
        ProductionPreparedOutputRoutingSaveData exact =
            BuildRestoreCandidate(candidate);
        Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData> next =
            exact.batches.ToDictionary(value => value.batchCommitId,
                value => value, StringComparer.Ordinal);
        if (restoreActive)
        {
            if (restorePublished)
            {
                throw new InvalidOperationException(
                    "Prepared-output routing restore cannot mutate after publication.");
            }
            stagedBatches = next;
            stagedCheckpointSequence = exact.lastConfirmedCheckpointSequence;
            stagedCheckpointDigest = exact.lastConfirmedCheckpointDigest;
            return;
        }
        long nextRoutingRevision = checked(routingRevision + 1L);
        batches = next;
        checkpointSequence = exact.lastConfirmedCheckpointSequence;
        checkpointDigest = exact.lastConfirmedCheckpointDigest;
        routingRevision = nextRoutingRevision;
    }

    public void BeginRestoreCandidate()
    {
        if (restoreActive)
            throw new InvalidOperationException("Routing restore is already active.");
        stagedBatches = null;
        rollbackBatches = null;
        stagedCheckpointSequence = 0L;
        stagedCheckpointDigest = string.Empty;
        rollbackCheckpointSequence = 0L;
        rollbackCheckpointDigest = string.Empty;
        restoreActive = true;
        restorePublished = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreActive || restorePublished || stagedBatches == null)
            throw new InvalidOperationException("Routing restore candidate is incomplete.");
        long nextRoutingRevision = checked(routingRevision + 1L);
        rollbackBatches = batches;
        rollbackCheckpointSequence = checkpointSequence;
        rollbackCheckpointDigest = checkpointDigest;
        batches = stagedBatches;
        checkpointSequence = stagedCheckpointSequence;
        checkpointDigest = stagedCheckpointDigest;
        stagedBatches = null;
        routingRevision = nextRoutingRevision;
        restorePublished = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (!restoreActive)
            return;
        if (restorePublished)
        {
            long nextRoutingRevision = checked(routingRevision + 1L);
            batches = rollbackBatches ?? new(StringComparer.Ordinal);
            checkpointSequence = rollbackCheckpointSequence;
            checkpointDigest = rollbackCheckpointDigest;
            routingRevision = nextRoutingRevision;
        }
        ResetRestore();
    }

    public void CompleteRestoreCandidate() => ResetRestore();

    public void DiscardRestoreCandidate()
    {
        if (restorePublished)
            RollbackPublishedRestoreCandidate();
        else
            ResetRestore();
    }

    private IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot> CaptureWhere(
        Func<ProductionPreparedOutputRoutingBatchSaveData, bool> predicate) =>
        batches.Values.Where(predicate)
            .OrderBy(batch => batch.batchCommitId, StringComparer.Ordinal)
            .SelectMany(batch => batch.lines
                .OrderBy(line => line.lineCommitId, StringComparer.Ordinal)
                .Select(line => Snapshot(batch, line)))
            .ToArray();

    private static ProductionPreparedOutputRoutingLineSnapshot Snapshot(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line) => new(
        batch.batchCommitId,
        batch.ownerBillId,
        batch.ownerRecipeId,
        batch.ownerFacilityId,
        batch.cycleSequence,
        line.lineCommitId,
        line.outputLineId,
        line.role,
        line.itemId,
        line.destinationId,
        line.componentFingerprint,
        line.outputCapabilityId,
        line.outputCapabilityVersion,
        line.outputComponentCodecId,
        line.outputComponentCodecVersion,
        line.outputCapabilityFingerprint,
        line.originalQuantity,
        line.originalMassGrams,
        line.remainingQuantity,
        line.remainingMassGrams,
        line.routedQuantity,
        line.routedMassGrams);

    private static ProductionPreparedOutputNonPhysicalDispositionSnapshot Snapshot(
        ProductionPreparedOutputNonPhysicalDispositionSaveData disposition) =>
        new(
            disposition.batchCommitId,
            disposition.lineCommitId,
            disposition.outputLineId,
            disposition.role,
            disposition.canonicalPayload,
            disposition.dispositionFingerprint,
            disposition.exactMassGrams);

    private static ProductionPreparedOutputPhysicalRouteReceipt SnapshotReceipt(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData operation) => new(
        operation.routeOperationId,
        operation.requestFingerprint,
        operation.physicalReceiptFingerprint,
        batch.batchCommitId,
        line.destinationId,
        operation.targetDestinationId,
        operation.targetPositionX,
        operation.targetPositionY,
        operation.routedQuantity,
        operation.routedMassGrams,
        Array.AsReadOnly((operation.physicalSlices
                ?? new List<ProductionPreparedOutputPhysicalRouteSliceSaveData>())
            .OrderBy(slice => slice.sourceOffsetQuantity)
            .ThenBy(slice => slice.sourceStackId, StringComparer.Ordinal)
            .ThenBy(slice => slice.routedStackId, StringComparer.Ordinal)
            .ThenBy(slice => slice.routedOffsetQuantity)
            .Select(slice => new ProductionPreparedOutputPhysicalRouteSliceReceipt(
                slice.sourceStackId,
                slice.routedStackId,
                line.outputLineId,
                line.lineCommitId,
                line.itemId,
                slice.sourceOffsetQuantity,
                slice.routedOffsetQuantity,
                slice.routedQuantity,
                slice.routedMassGrams,
                line.componentFingerprint))
            .ToArray()));

    private static ProductionPreparedOutputRouteRequestSnapshot Snapshot(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData operation)
    {
        ProductionPreparedOutputDeliveryRevisionSaveData delivery =
            CurrentDeliveryRevision(operation);
        return new ProductionPreparedOutputRouteRequestSnapshot(
            operation.routeOperationId,
            operation.requestFingerprint,
            batch.batchCommitId,
            line.lineCommitId,
            line.outputLineId,
            line.itemId,
            line.componentFingerprint,
            line.destinationId,
            operation.targetDestinationId,
            operation.targetPositionX,
            operation.targetPositionY,
            operation.sourceOffsetQuantity,
            operation.sourceOffsetMassGrams,
            operation.routedQuantity,
            operation.routedMassGrams,
            operation.phase,
            operation.physicalReceiptFingerprint,
            delivery.revision,
            delivery.targetKind,
            delivery.revisionFingerprint,
            delivery.targetDestinationId,
            delivery.targetPositionX,
            delivery.targetPositionY,
            delivery.targetAuthorityFingerprint);
    }

    private Context FindLine(string batchCommitId, string lineCommitId)
    {
        if (!batches.TryGetValue(batchCommitId, out var batch))
            throw new InvalidOperationException($"Routing batch '{batchCommitId}' is missing.");
        var line = batch.lines.SingleOrDefault(value => string.Equals(
            value.lineCommitId, lineCommitId, StringComparison.Ordinal));
        if (line == null)
            throw new InvalidOperationException($"Routing line '{lineCommitId}' is missing.");
        return new Context(batch, line, null);
    }

    private Context FindOperation(string operationId)
        => FindOperation(batches, operationId);

    private static Context FindOperation(
        IReadOnlyDictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
            source,
        string operationId)
    {
        foreach (var batch in source.Values)
        foreach (var line in batch.lines)
        {
            var operation = line.routeOperations.SingleOrDefault(value =>
                string.Equals(value.routeOperationId, operationId,
                    StringComparison.Ordinal));
            if (operation != null)
                return new Context(batch, line, operation);
        }
        throw new InvalidOperationException($"Routing operation '{operationId}' is missing.");
    }

    private static void ValidateReceipt(
        Context context,
        ProductionPreparedOutputPhysicalRouteReceipt receipt)
    {
        if (!string.Equals(receipt.RequestFingerprint,
                context.Operation.requestFingerprint, StringComparison.Ordinal)
            || !string.Equals(receipt.BatchCommitId,
                context.Batch.batchCommitId, StringComparison.Ordinal)
            || !string.Equals(receipt.SourceDestinationId,
                context.Line.destinationId, StringComparison.Ordinal)
            || !string.Equals(receipt.TargetDestinationId,
                context.Operation.targetDestinationId, StringComparison.Ordinal)
            || receipt.TargetPositionX != context.Operation.targetPositionX
            || receipt.TargetPositionY != context.Operation.targetPositionY
            || receipt.TotalQuantity != context.Operation.routedQuantity
            || receipt.TotalMassGrams != context.Operation.routedMassGrams
            || receipt.Slices == null || receipt.Slices.Count == 0)
        {
            throw new InvalidOperationException(
                $"Physical route receipt conflicts for '{receipt.RouteOperationId}'.");
        }

        int expectedSourceOffset = context.Operation.sourceOffsetQuantity;
        int quantity = 0;
        long mass = 0L;
        foreach (var slice in receipt.Slices
                     .OrderBy(value => value.SourceOffsetQuantity)
                     .ThenBy(value => value.SourceStackId, StringComparer.Ordinal)
                     .ThenBy(value => value.RoutedStackId, StringComparer.Ordinal)
                     .ThenBy(value => value.RoutedOffsetQuantity))
        {
            RequireCanonical(slice.SourceStackId, "receipt.sourceStackId");
            RequireCanonical(slice.RoutedStackId, "receipt.routedStackId");
            RequireDigest(slice.ComponentFingerprint, "receipt.componentFingerprint");
            if (slice.SourceOffsetQuantity != expectedSourceOffset
                || slice.RoutedOffsetQuantity < 0
                || slice.RoutedQuantity <= 0 || slice.RoutedMassGrams <= 0L
                || checked(context.Line.originalMassGrams
                    * slice.RoutedQuantity)
                    != checked(slice.RoutedMassGrams
                        * context.Line.originalQuantity)
                || !string.Equals(slice.OutputLineId,
                    context.Line.outputLineId, StringComparison.Ordinal)
                || !string.Equals(slice.LineCommitId,
                    context.Line.lineCommitId, StringComparison.Ordinal)
                || !string.Equals(slice.ItemId,
                    context.Line.itemId, StringComparison.Ordinal)
                || !string.Equals(slice.ComponentFingerprint,
                    context.Line.componentFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Physical route slice conflicts for '{receipt.RouteOperationId}'.");
            }
            expectedSourceOffset = checked(
                expectedSourceOffset + slice.RoutedQuantity);
            quantity = checked(quantity + slice.RoutedQuantity);
            mass = checked(mass + slice.RoutedMassGrams);
        }
        if (quantity != receipt.TotalQuantity || mass != receipt.TotalMassGrams)
            throw new InvalidOperationException("Physical route receipt totals conflict.");
        foreach (IGrouping<string, ProductionPreparedOutputPhysicalRouteSliceReceipt>
                 routed in receipt.Slices.GroupBy(
                     value => value.RoutedStackId,
                     StringComparer.Ordinal))
        {
            int expectedRoutedOffset = 0;
            foreach (ProductionPreparedOutputPhysicalRouteSliceReceipt slice in
                     routed.OrderBy(value => value.RoutedOffsetQuantity)
                         .ThenBy(value => value.SourceStackId,
                             StringComparer.Ordinal))
            {
                if (slice.RoutedOffsetQuantity != expectedRoutedOffset)
                {
                    throw new InvalidOperationException(
                        $"Physical routed-stack ranges overlap or gap for '{receipt.RouteOperationId}'.");
                }
                expectedRoutedOffset = checked(
                    expectedRoutedOffset + slice.RoutedQuantity);
            }
        }
    }

    private static bool SameSlices(
        IReadOnlyList<ProductionPreparedOutputPhysicalRouteSliceSaveData> saved,
        IReadOnlyList<ProductionPreparedOutputPhysicalRouteSliceReceipt> received)
    {
        var left = (saved ?? Array.Empty<
                ProductionPreparedOutputPhysicalRouteSliceSaveData>())
            .OrderBy(value => value.sourceOffsetQuantity)
            .ThenBy(value => value.sourceStackId, StringComparer.Ordinal)
            .ThenBy(value => value.routedStackId, StringComparer.Ordinal)
            .ThenBy(value => value.routedOffsetQuantity).ToArray();
        var right = (received ?? Array.Empty<
                ProductionPreparedOutputPhysicalRouteSliceReceipt>())
            .OrderBy(value => value.SourceOffsetQuantity)
            .ThenBy(value => value.SourceStackId, StringComparer.Ordinal)
            .ThenBy(value => value.RoutedStackId, StringComparer.Ordinal)
            .ThenBy(value => value.RoutedOffsetQuantity).ToArray();
        return left.Length == right.Length && left.Zip(right, (a, b) =>
            string.Equals(a.sourceStackId, b.SourceStackId, StringComparison.Ordinal)
            && string.Equals(a.routedStackId, b.RoutedStackId, StringComparison.Ordinal)
            && a.sourceOffsetQuantity == b.SourceOffsetQuantity
            && a.routedOffsetQuantity == b.RoutedOffsetQuantity
            && a.routedQuantity == b.RoutedQuantity
            && a.routedMassGrams == b.RoutedMassGrams).All(value => value);
    }

    private static bool IsDrainAcknowledged(
        ProductionPreparedOutputRoutingBatchSaveData batch) =>
        batch.lines.All(line => line.remainingQuantity == 0
            && line.remainingMassGrams == 0L
            && line.routeOperations.Count > 0
            && line.routeOperations.All(operation => operation.phase ==
                ProductionPreparedOutputRoutePhase
                    .ItemsAcknowledgedAwaitingCheckpointGc));

    private void ResetRestore()
    {
        stagedBatches = null;
        rollbackBatches = null;
        stagedCheckpointSequence = 0L;
        stagedCheckpointDigest = string.Empty;
        rollbackCheckpointSequence = 0L;
        rollbackCheckpointDigest = string.Empty;
        restoreActive = false;
        restorePublished = false;
    }

    private static ProductionPreparedOutputRoutingBatchSaveData BuildCommittedBatch(
        ProductionPreparedOutputBatchSaveData batch,
        BuildingInstanceId ownerFacilityId)
    {
        if (batch == null || batch.phase != ProductionPreparedOutputPhase.Completed)
            throw new InvalidOperationException("Routing requires a Completed batch.");
        ProductionPreparedOutputContract.ValidateForBill(batch,
            (ProductionBillId)batch.billId, batch.recipeId, batch.cycleSequence,
            batch.destinationId);
        if (!ownerFacilityId.IsValid || !string.Equals(batch.destinationId,
                ProductionBillRuntime.OutputDestinationPrefix + ownerFacilityId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Routing facility ownership conflicts.");
        }
        var lines = batch.lines.Where(line => line != null && line.rollSucceeded
                && line.quantity > 0 && IsPhysicalRole(line.role))
            .OrderBy(line => line.outputLineId, StringComparer.Ordinal)
            .Select(line => new ProductionPreparedOutputRoutingLineSaveData
            {
                batchCommitId = batch.batchCommitId,
                lineCommitId = line.lineCommitId,
                outputLineId = line.outputLineId,
                role = line.role,
                itemId = line.itemId,
                destinationId = batch.destinationId,
                componentFingerprint = line.componentFingerprint,
                outputCapabilityId = line.outputCapabilityId,
                outputCapabilityVersion = line.outputCapabilityVersion,
                outputComponentCodecId = line.outputComponentCodecId,
                outputComponentCodecVersion = line.outputComponentCodecVersion,
                outputCapabilityFingerprint = line.outputCapabilityFingerprint,
                originalQuantity = line.quantity,
                remainingQuantity = line.quantity,
                originalMassGrams = line.exactMassGrams,
                remainingMassGrams = line.exactMassGrams,
                routedQuantity = 0,
                routedMassGrams = 0L,
                routeOperations = new()
            }).ToList();
        if (lines.Count == 0)
            throw new InvalidOperationException("Completed batch has no routable line.");
        List<ProductionPreparedOutputNonPhysicalDispositionSaveData>
            nonPhysicalDispositions = batch.lines
                .Where(line => line != null
                    && line.rollSucceeded
                    && ProductionOutputRoleRules.IsNonPhysical(line.role))
                .OrderBy(line => line.outputLineId, StringComparer.Ordinal)
                .Select(line => new
                    ProductionPreparedOutputNonPhysicalDispositionSaveData
                    {
                        batchCommitId = batch.batchCommitId,
                        lineCommitId = line.lineCommitId,
                        outputLineId = line.outputLineId,
                        role = line.role,
                        canonicalPayload = line.componentPayload,
                        dispositionFingerprint = line.componentFingerprint,
                        exactMassGrams = line.exactMassGrams
                    })
                .ToList();
        ProductionPreparedOutputRoutingBatchSaveData result = new()
        {
            batchCommitId = batch.batchCommitId,
            ownerBillId = batch.billId,
            ownerRecipeId = batch.recipeId,
            ownerFacilityId = ownerFacilityId.Value,
            cycleSequence = batch.cycleSequence,
            outcomeFingerprint = batch.outcomeFingerprint,
            destinationId = batch.destinationId,
            capacitySourceDigest = batch.capacitySourceDigest,
            outputBufferCycleCapacity = batch.outputBufferCycleCapacity,
            projectedPortfolioCapacityGrams =
                batch.projectedPortfolioCapacityGrams,
            requiredMinimumCapacityGrams =
                batch.requiredMinimumCapacityGrams,
            maximumMassProofDigest = batch.maximumMassProofDigest,
            maximumBatchMassGrams = batch.maximumBatchMassGrams,
            capacityClaimDigest = batch.capacityClaimDigest,
            totalDeclaredLossMassGrams = batch.totalDeclaredLossMassGrams,
            totalDeclaredExternalInputMassGrams =
                batch.totalDeclaredExternalInputMassGrams,
            nonPhysicalDispositions = nonPhysicalDispositions,
            lines = lines
        };
        result.routingFingerprint = RoutingFingerprint(result);
        return result;
    }

    private static void ValidateSave(ProductionPreparedOutputRoutingSaveData source)
    {
        if (source == null
            || source.version != ProductionPreparedOutputRoutingSaveData.CurrentVersion
            || source.lastConfirmedCheckpointSequence < 0L
            || source.lastConfirmedCheckpointSequence == 0L
                && !string.Equals(source.lastConfirmedCheckpointDigest,
                    string.Empty,
                    StringComparison.Ordinal)
            || source.lastConfirmedCheckpointSequence > 0L
                && !IsDigest(source.lastConfirmedCheckpointDigest)
            || source.batches == null)
            throw new InvalidOperationException("Routing payload schema is invalid.");
        HashSet<string> batchIds = new(StringComparer.Ordinal);
        HashSet<string> operationIds = new(StringComparer.Ordinal);
        string previous = string.Empty;
        foreach (var batch in source.batches)
        {
            ValidateBatch(batch, operationIds);
            if (!batchIds.Add(batch.batchCommitId)
                || previous.Length > 0
                    && string.CompareOrdinal(previous, batch.batchCommitId) >= 0)
                throw new InvalidOperationException("Routing batches are unordered.");
            previous = batch.batchCommitId;
        }
    }

    private static void ValidateBatch(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ISet<string> operationIds)
    {
        if (batch == null || batch.lines == null || batch.lines.Count == 0
            || batch.nonPhysicalDispositions == null
            || batch.totalDeclaredLossMassGrams < 0L
            || batch.totalDeclaredExternalInputMassGrams < 0L)
            throw new InvalidOperationException("Routing batch is empty.");
        RequireCanonical(batch.batchCommitId, "batch.id");
        RequireCanonical(batch.ownerBillId, "batch.bill");
        RequireCanonical(batch.ownerRecipeId, "batch.recipe");
        RequireCanonical(batch.ownerFacilityId, "batch.facility");
        RequireCanonical(batch.destinationId, "batch.destination");
        RequireDigest(batch.outcomeFingerprint, "batch.outcome");
        RequireDigest(batch.routingFingerprint, "batch.fingerprint");
        ProductionBillId billId = (ProductionBillId)batch.ownerBillId;
        if (!billId.IsValid || !((BuildingInstanceId)batch.ownerFacilityId).IsValid
            || batch.cycleSequence <= 0
            || !string.Equals(batch.batchCommitId,
                ProductionPreparedOutputIdentity.BuildBatchCommitId(billId,
                    batch.cycleSequence, batch.outcomeFingerprint),
                StringComparison.Ordinal)
            || !string.Equals(batch.destinationId,
                ProductionBillRuntime.OutputDestinationPrefix + batch.ownerFacilityId,
                StringComparison.Ordinal)
            || !string.Equals(batch.routingFingerprint, RoutingFingerprint(batch),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Routing batch identity conflicts.");

        string previous = string.Empty;
        foreach (var line in batch.lines)
        {
            ValidateLine(batch, line, operationIds);
            if (previous.Length > 0
                && string.CompareOrdinal(previous, line.outputLineId) >= 0)
                throw new InvalidOperationException("Routing lines are unordered.");
            previous = line.outputLineId;
        }

        long declaredLossMassGrams = 0L;
        long declaredExternalInputMassGrams = 0L;
        previous = string.Empty;
        foreach (ProductionPreparedOutputNonPhysicalDispositionSaveData
                 disposition in batch.nonPhysicalDispositions)
        {
            ValidateNonPhysicalDisposition(batch, disposition);
            if (previous.Length > 0
                && string.CompareOrdinal(
                    previous,
                    disposition.outputLineId) >= 0)
            {
                throw new InvalidOperationException(
                    "Routing non-physical dispositions are unordered.");
            }
            previous = disposition.outputLineId;
            if (disposition.role == ProductionOutputRole.DeclaredLoss)
            {
                declaredLossMassGrams = checked(
                    declaredLossMassGrams + disposition.exactMassGrams);
            }
            else
            {
                declaredExternalInputMassGrams = checked(
                    declaredExternalInputMassGrams
                    + disposition.exactMassGrams);
            }
        }
        if (declaredLossMassGrams != batch.totalDeclaredLossMassGrams
            || declaredExternalInputMassGrams !=
                batch.totalDeclaredExternalInputMassGrams)
        {
            throw new InvalidOperationException(
                "Routing non-physical disposition total conflicts.");
        }

        long originalPhysicalMassGrams = batch.lines.Aggregate(
            0L,
            (total, line) => checked(total + line.originalMassGrams));
        bool hasProof = IsDigest(batch.maximumMassProofDigest)
            && batch.maximumBatchMassGrams > 0L
            && IsDigest(batch.capacityClaimDigest);
        if (!IsDigest(batch.capacitySourceDigest)
            || batch.outputBufferCycleCapacity is < 2 or > 4
            || batch.projectedPortfolioCapacityGrams <= 0L
            || !hasProof
            || originalPhysicalMassGrams > batch.maximumBatchMassGrams
            || batch.requiredMinimumCapacityGrams != Math.Max(
                batch.projectedPortfolioCapacityGrams,
                checked(
                    batch.maximumBatchMassGrams
                    * batch.outputBufferCycleCapacity)))
        {
            throw new InvalidOperationException(
                "Routing batch capacity authority is invalid.");
        }
    }

    private static void ValidateLine(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ISet<string> operationIds)
    {
        if (line == null || !IsPhysicalRole(line.role)
            || line.originalQuantity <= 0 || line.remainingQuantity < 0
            || line.routedQuantity < 0
            || line.originalQuantity != line.remainingQuantity + line.routedQuantity
            || line.originalMassGrams <= 0L || line.remainingMassGrams < 0L
            || line.routedMassGrams < 0L
            || line.originalMassGrams != line.remainingMassGrams + line.routedMassGrams
            || line.routeOperations == null)
            throw new InvalidOperationException("Routing line totals are invalid.");
        RequireCanonical(line.outputLineId, "line.output");
        RequireCanonical(line.itemId, "line.item");
        RequireCanonical(line.destinationId, "line.destination");
        RequireDigest(line.componentFingerprint, "line.component");
        RequireCanonical(line.outputCapabilityId, "line.capability");
        RequireCanonical(line.outputComponentCodecId, "line.componentCodec");
        RequireDigest(
            line.outputCapabilityFingerprint,
            "line.capabilityFingerprint");
        if (!string.Equals(line.batchCommitId, batch.batchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(line.destinationId, batch.destinationId,
                StringComparison.Ordinal)
            || !string.Equals(line.lineCommitId,
                ProductionPreparedOutputIdentity.BuildLineCommitId(
                    batch.batchCommitId, line.outputLineId),
                StringComparison.Ordinal)
            || line.outputCapabilityVersion <= 0
            || line.outputComponentCodecVersion <= 0
            || !string.Equals(
                line.outputCapabilityFingerprint,
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    line.outputLineId,
                    line.itemId,
                    line.outputCapabilityId,
                    line.outputCapabilityVersion,
                    line.outputComponentCodecId,
                    line.outputComponentCodecVersion),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Routing line identity conflicts.");

        int coveredQuantity = 0;
        long coveredMass = 0L;
        bool pendingSeen = false;
        foreach (var operation in line.routeOperations
                     .OrderBy(value => value.sourceOffsetQuantity))
        {
            ValidateOperation(batch, line, operation);
            if (!operationIds.Add(operation.routeOperationId)
                || operation.sourceOffsetQuantity != coveredQuantity
                || operation.sourceOffsetMassGrams != coveredMass || pendingSeen)
                throw new InvalidOperationException("Routing operations overlap.");
            if (operation.phase == ProductionPreparedOutputRoutePhase.PhysicalPending)
            {
                pendingSeen = true;
                continue;
            }
            coveredQuantity = checked(coveredQuantity + operation.routedQuantity);
            coveredMass = checked(coveredMass + operation.routedMassGrams);
        }
        if (coveredQuantity != line.routedQuantity
            || coveredMass != line.routedMassGrams)
            throw new InvalidOperationException("Routing operation coverage conflicts.");
    }

    private static void ValidateNonPhysicalDisposition(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputNonPhysicalDispositionSaveData disposition)
    {
        if (disposition == null
            || !ProductionOutputRoleRules.IsNonPhysical(disposition.role)
            || disposition.exactMassGrams <= 0L
            || disposition.canonicalPayload == null
            || disposition.canonicalPayload.Length == 0
            || !string.Equals(
                disposition.canonicalPayload,
                disposition.canonicalPayload.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Routing non-physical disposition is invalid.");
        }
        RequireCanonical(disposition.outputLineId, "disposition.output");
        RequireDigest(
            disposition.dispositionFingerprint,
            "disposition.fingerprint");
        if (!string.Equals(
                disposition.batchCommitId,
                batch.batchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                disposition.lineCommitId,
                ProductionPreparedOutputIdentity.BuildLineCommitId(
                    batch.batchCommitId,
                    disposition.outputLineId),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Routing non-physical disposition identity conflicts.");
        }
    }

    private static void ValidateOperation(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData operation)
    {
        if (operation == null
            || !Enum.IsDefined(typeof(ProductionPreparedOutputRoutePhase),
                operation.phase)
            || operation.sourceOffsetQuantity < 0
            || operation.sourceOffsetMassGrams < 0L
            || operation.routedQuantity <= 0 || operation.routedMassGrams <= 0L
            || operation.physicalSlices == null)
            throw new InvalidOperationException("Routing operation fields are invalid.");
        RequireCanonical(operation.routeOperationId, "operation.id");
        RequireCanonicalOptional(
            operation.targetDestinationId,
            "operation.target");
        RequireDigest(operation.requestFingerprint, "operation.request");
        if (!string.Equals(operation.routeOperationId,
                BuildOperationId(batch.batchCommitId, line.lineCommitId,
                    operation.sourceOffsetQuantity), StringComparison.Ordinal)
            || !string.Equals(operation.requestFingerprint,
                RequestFingerprint(batch, line, operation),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Routing operation identity conflicts.");
        bool pending = operation.phase ==
            ProductionPreparedOutputRoutePhase.PhysicalPending;
        ValidateDeliveryRevisions(operation);
        if (pending)
        {
            if (!string.IsNullOrEmpty(operation.physicalReceiptFingerprint)
                || operation.physicalSlices.Count != 0)
                throw new InvalidOperationException("Pending route has a receipt.");
            return;
        }
        RequireDigest(operation.physicalReceiptFingerprint, "operation.receipt");
        ValidateSavedSlices(line, operation);
        if (!string.Equals(
                operation.physicalReceiptFingerprint,
                SavedReceiptFingerprint(batch, line, operation),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Saved physical receipt fingerprint conflicts with its exact slices.");
        }
    }

    private static void ValidateSavedSlices(
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData operation)
    {
        if (operation.physicalSlices.Count == 0)
            throw new InvalidOperationException("Applied route has no slices.");
        int sourceOffset = operation.sourceOffsetQuantity;
        int quantity = 0;
        long mass = 0L;
        foreach (var slice in operation.physicalSlices
                     .OrderBy(value => value.sourceOffsetQuantity)
                     .ThenBy(value => value.sourceStackId, StringComparer.Ordinal)
                     .ThenBy(value => value.routedStackId, StringComparer.Ordinal)
                     .ThenBy(value => value.routedOffsetQuantity))
        {
            if (slice == null || slice.sourceOffsetQuantity != sourceOffset
                || slice.routedOffsetQuantity < 0 || slice.routedQuantity <= 0
                || slice.routedMassGrams <= 0L
                || checked(line.originalMassGrams * slice.routedQuantity)
                    != checked(slice.routedMassGrams * line.originalQuantity))
                throw new InvalidOperationException("Saved physical slice is invalid.");
            RequireCanonical(slice.sourceStackId, "slice.source");
            RequireCanonical(slice.routedStackId, "slice.routed");
            sourceOffset = checked(sourceOffset + slice.routedQuantity);
            quantity = checked(quantity + slice.routedQuantity);
            mass = checked(mass + slice.routedMassGrams);
        }
        if (quantity != operation.routedQuantity || mass != operation.routedMassGrams)
            throw new InvalidOperationException("Saved slice totals conflict.");
        foreach (var routed in operation.physicalSlices.GroupBy(
                     value => value.routedStackId, StringComparer.Ordinal))
        {
            int expected = 0;
            foreach (var slice in routed.OrderBy(value => value.routedOffsetQuantity)
                         .ThenBy(value => value.sourceStackId,
                             StringComparer.Ordinal))
            {
                if (slice.routedOffsetQuantity != expected)
                    throw new InvalidOperationException("Saved routed ranges conflict.");
                expected = checked(expected + slice.routedQuantity);
            }
        }
    }

    private static ProductionPreparedOutputDeliveryRevisionSaveData
        BuildInitialDeliveryRevision(
            ProductionPreparedOutputRouteOperationSaveData operation)
    {
        ProductionPreparedOutputDeliveryRevisionSaveData revision = new()
        {
            revision = 0L,
            targetKind = string.IsNullOrEmpty(operation.targetDestinationId)
                ? ProductionPreparedOutputDeliveryTargetKind
                    .WarehouseSelectionPending
                : ProductionPreparedOutputDeliveryTargetKind.InitialExactTarget,
            reason = ProductionPreparedOutputDeliveryRerouteReason.InitialRoute,
            rerouteOperationId = string.Empty,
            previousRevisionFingerprint = string.Empty,
            originalPhysicalReceiptFingerprint = string.Empty,
            targetDestinationId = operation.targetDestinationId,
            targetPositionX = operation.targetPositionX,
            targetPositionY = operation.targetPositionY,
            targetAuthorityFingerprint = string.Empty
        };
        revision.revisionFingerprint = DeliveryRevisionFingerprint(
            operation.routeOperationId,
            operation.requestFingerprint,
            revision);
        return revision;
    }

    private static void FinalizeInitialDeliveryRevision(
        ProductionPreparedOutputRouteOperationSaveData operation)
    {
        if (operation.deliveryRevisions == null
            || operation.deliveryRevisions.Count != 1
            || operation.deliveryRevisions[0] == null
            || operation.deliveryRevisions[0].revision != 0L)
        {
            throw new InvalidOperationException(
                "Physical route has no exact initial delivery revision.");
        }
        ProductionPreparedOutputDeliveryRevisionSaveData initial =
            operation.deliveryRevisions[0];
        initial.originalPhysicalReceiptFingerprint =
            operation.physicalReceiptFingerprint;
        initial.revisionFingerprint = DeliveryRevisionFingerprint(
            operation.routeOperationId,
            operation.requestFingerprint,
            initial);
    }

    private static ProductionPreparedOutputDeliveryRevisionSaveData
        BuildRerouteDeliveryRevision(
            string routeOperationId,
            string requestFingerprint,
            ProductionPreparedOutputDeliveryRevisionSaveData previous,
            string originalPhysicalReceiptFingerprint,
            ProductionPreparedOutputDeliveryRerouteReason reason,
            string targetDestinationId,
            int targetPositionX,
            int targetPositionY,
            string targetAuthorityFingerprint)
    {
        long nextRevision = checked(previous.revision + 1L);
        string rerouteOperationId = BuildDeliveryRerouteOperationId(
            routeOperationId,
            nextRevision,
            previous.revisionFingerprint,
            originalPhysicalReceiptFingerprint,
            reason,
            targetDestinationId,
            targetPositionX,
            targetPositionY,
            targetAuthorityFingerprint);
        ProductionPreparedOutputDeliveryRevisionSaveData next = new()
        {
            revision = nextRevision,
            targetKind = ProductionPreparedOutputDeliveryTargetKind
                .ExactRerouteTarget,
            reason = reason,
            rerouteOperationId = rerouteOperationId,
            previousRevisionFingerprint = previous.revisionFingerprint,
            originalPhysicalReceiptFingerprint =
                originalPhysicalReceiptFingerprint,
            targetDestinationId = targetDestinationId,
            targetPositionX = targetPositionX,
            targetPositionY = targetPositionY,
            targetAuthorityFingerprint = targetAuthorityFingerprint
        };
        next.revisionFingerprint = DeliveryRevisionFingerprint(
            routeOperationId,
            requestFingerprint,
            next);
        return next;
    }

    private static void ValidateDeliveryRevisions(
        ProductionPreparedOutputRouteOperationSaveData operation)
    {
        if (operation.deliveryRevisions == null
            || operation.deliveryRevisions.Count == 0)
        {
            throw new InvalidOperationException(
                "Routing operation has no delivery revision authority.");
        }

        bool pendingPhysical = operation.phase ==
            ProductionPreparedOutputRoutePhase.PhysicalPending;
        ProductionPreparedOutputDeliveryRevisionSaveData previous = null;
        for (int index = 0; index < operation.deliveryRevisions.Count; index++)
        {
            ProductionPreparedOutputDeliveryRevisionSaveData revision =
                operation.deliveryRevisions[index];
            if (revision == null
                || revision.revision != index
                || !Enum.IsDefined(
                    typeof(ProductionPreparedOutputDeliveryTargetKind),
                    revision.targetKind)
                || !Enum.IsDefined(
                    typeof(ProductionPreparedOutputDeliveryRerouteReason),
                    revision.reason))
            {
                throw new InvalidOperationException(
                    "Delivery revision sequence is invalid.");
            }
            RequireCanonicalOptional(
                revision.targetDestinationId,
                "delivery.target");
            RequireCanonicalOptional(
                revision.rerouteOperationId,
                "delivery.rerouteOperation");
            RequireCanonicalOptional(
                revision.previousRevisionFingerprint,
                "delivery.previousFingerprint");
            RequireCanonicalOptional(
                revision.originalPhysicalReceiptFingerprint,
                "delivery.originalReceipt");
            RequireCanonicalOptional(
                revision.targetAuthorityFingerprint,
                "delivery.targetAuthority");
            RequireDigest(revision.revisionFingerprint,
                "delivery.revisionFingerprint");

            if (index == 0)
            {
                ProductionPreparedOutputDeliveryTargetKind initialKind =
                    string.IsNullOrEmpty(operation.targetDestinationId)
                        ? ProductionPreparedOutputDeliveryTargetKind
                            .WarehouseSelectionPending
                        : ProductionPreparedOutputDeliveryTargetKind
                            .InitialExactTarget;
                if (revision.targetKind != initialKind
                    || revision.reason !=
                        ProductionPreparedOutputDeliveryRerouteReason.InitialRoute
                    || !string.IsNullOrEmpty(revision.rerouteOperationId)
                    || !string.IsNullOrEmpty(revision.previousRevisionFingerprint)
                    || !string.Equals(revision.targetDestinationId,
                        operation.targetDestinationId, StringComparison.Ordinal)
                    || revision.targetPositionX != operation.targetPositionX
                    || revision.targetPositionY != operation.targetPositionY
                    || !string.IsNullOrEmpty(revision.targetAuthorityFingerprint))
                {
                    throw new InvalidOperationException(
                        "Initial delivery revision conflicts with original request.");
                }
            }
            else
            {
                RequireCanonical(revision.targetDestinationId,
                    "delivery.rerouteTarget");
                RequireDigest(revision.targetAuthorityFingerprint,
                    "delivery.targetAuthority");
                RequireDigest(revision.previousRevisionFingerprint,
                    "delivery.previousFingerprint");
                RequireCanonical(revision.rerouteOperationId,
                    "delivery.rerouteOperation");
                if (revision.targetKind !=
                        ProductionPreparedOutputDeliveryTargetKind.ExactRerouteTarget
                    || revision.reason ==
                        ProductionPreparedOutputDeliveryRerouteReason.InitialRoute
                    || previous == null
                    || !string.Equals(revision.previousRevisionFingerprint,
                        previous.revisionFingerprint, StringComparison.Ordinal)
                    || !string.Equals(revision.rerouteOperationId,
                        BuildDeliveryRerouteOperationId(
                            operation.routeOperationId,
                            revision.revision,
                            revision.previousRevisionFingerprint,
                            revision.originalPhysicalReceiptFingerprint,
                            revision.reason,
                            revision.targetDestinationId,
                            revision.targetPositionX,
                            revision.targetPositionY,
                            revision.targetAuthorityFingerprint),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Delivery reroute revision conflicts with its predecessor.");
                }
            }

            string expectedReceipt = pendingPhysical
                ? string.Empty
                : operation.physicalReceiptFingerprint;
            if (!string.Equals(revision.originalPhysicalReceiptFingerprint,
                    expectedReceipt, StringComparison.Ordinal)
                || !string.Equals(revision.revisionFingerprint,
                    DeliveryRevisionFingerprint(
                        operation.routeOperationId,
                        operation.requestFingerprint,
                        revision),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Delivery revision changed its original physical receipt or fingerprint.");
            }
            if (pendingPhysical && index > 0)
            {
                throw new InvalidOperationException(
                    "A physically pending route cannot have a reroute revision.");
            }
            previous = revision;
        }
    }

    private static ProductionPreparedOutputDeliveryRevisionSaveData
        CurrentDeliveryRevision(
            ProductionPreparedOutputRouteOperationSaveData operation)
    {
        ValidateDeliveryRevisions(operation);
        return operation.deliveryRevisions[operation.deliveryRevisions.Count - 1];
    }

    private static ProductionPreparedOutputDeliveryRevisionSnapshot DeliverySnapshot(
        string routeOperationId,
        ProductionPreparedOutputDeliveryRevisionSaveData revision) => new(
        routeOperationId,
        revision.revision,
        revision.targetKind,
        revision.reason,
        revision.rerouteOperationId,
        revision.previousRevisionFingerprint,
        revision.originalPhysicalReceiptFingerprint,
        revision.targetDestinationId,
        revision.targetPositionX,
        revision.targetPositionY,
        revision.targetAuthorityFingerprint,
        revision.revisionFingerprint);

    private static string DeliveryRevisionFingerprint(
        string routeOperationId,
        string requestFingerprint,
        ProductionPreparedOutputDeliveryRevisionSaveData revision) => Digest(
        DeliveryRevisionV1,
        routeOperationId,
        requestFingerprint,
        revision.revision.ToString(CultureInfo.InvariantCulture),
        ((int)revision.targetKind).ToString(CultureInfo.InvariantCulture),
        ((int)revision.reason).ToString(CultureInfo.InvariantCulture),
        revision.rerouteOperationId,
        revision.previousRevisionFingerprint,
        revision.originalPhysicalReceiptFingerprint,
        revision.targetDestinationId,
        revision.targetPositionX.ToString(CultureInfo.InvariantCulture),
        revision.targetPositionY.ToString(CultureInfo.InvariantCulture),
        revision.targetAuthorityFingerprint);

    private static string BuildDeliveryRerouteOperationId(
        string routeOperationId,
        long revision,
        string previousRevisionFingerprint,
        string originalPhysicalReceiptFingerprint,
        ProductionPreparedOutputDeliveryRerouteReason reason,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        string targetAuthorityFingerprint) =>
        "production-output-delivery-reroute:" + Digest(
            DeliveryRerouteOperationV1,
            routeOperationId,
            revision.ToString(CultureInfo.InvariantCulture),
            previousRevisionFingerprint,
            originalPhysicalReceiptFingerprint,
            ((int)reason).ToString(CultureInfo.InvariantCulture),
            targetDestinationId,
            targetPositionX.ToString(CultureInfo.InvariantCulture),
            targetPositionY.ToString(CultureInfo.InvariantCulture),
            targetAuthorityFingerprint);

    private static bool SameDeliveryRevision(
        ProductionPreparedOutputDeliveryRevisionSaveData left,
        ProductionPreparedOutputDeliveryRevisionSaveData right) =>
        left != null && right != null
        && left.revision == right.revision
        && left.targetKind == right.targetKind
        && left.reason == right.reason
        && string.Equals(left.rerouteOperationId, right.rerouteOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.previousRevisionFingerprint,
            right.previousRevisionFingerprint, StringComparison.Ordinal)
        && string.Equals(left.originalPhysicalReceiptFingerprint,
            right.originalPhysicalReceiptFingerprint, StringComparison.Ordinal)
        && string.Equals(left.targetDestinationId, right.targetDestinationId,
            StringComparison.Ordinal)
        && left.targetPositionX == right.targetPositionX
        && left.targetPositionY == right.targetPositionY
        && string.Equals(left.targetAuthorityFingerprint,
            right.targetAuthorityFingerprint, StringComparison.Ordinal)
        && string.Equals(left.revisionFingerprint, right.revisionFingerprint,
            StringComparison.Ordinal);

    private static Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
        CloneBatchMap(
            IReadOnlyDictionary<string,
                ProductionPreparedOutputRoutingBatchSaveData> source) =>
        source.Values
            .OrderBy(value => value.batchCommitId, StringComparer.Ordinal)
            .ToDictionary(
                value => value.batchCommitId,
                value => value.Clone(),
                StringComparer.Ordinal);

    private static string BuildOperationId(
        string batchId,
        string lineId,
        int sourceOffset) => "production-output-route:" + Digest(
        "production-output-route-operation-v1", batchId, lineId,
        sourceOffset.ToString(CultureInfo.InvariantCulture));

    private static string RoutingFingerprint(
        ProductionPreparedOutputRoutingBatchSaveData batch)
    {
        List<string> values = new()
        {
            RoutingV9, batch.batchCommitId, batch.ownerBillId,
            batch.ownerRecipeId, batch.ownerFacilityId,
            batch.cycleSequence.ToString(CultureInfo.InvariantCulture),
            batch.outcomeFingerprint, batch.destinationId,
            batch.capacitySourceDigest,
            batch.outputBufferCycleCapacity.ToString(
                CultureInfo.InvariantCulture),
            batch.projectedPortfolioCapacityGrams.ToString(
                CultureInfo.InvariantCulture),
            batch.requiredMinimumCapacityGrams.ToString(
                CultureInfo.InvariantCulture),
            batch.maximumMassProofDigest,
            batch.maximumBatchMassGrams.ToString(
                CultureInfo.InvariantCulture),
            batch.capacityClaimDigest,
            batch.totalDeclaredLossMassGrams.ToString(
                CultureInfo.InvariantCulture),
            batch.totalDeclaredExternalInputMassGrams.ToString(
                CultureInfo.InvariantCulture)
        };
        foreach (ProductionPreparedOutputNonPhysicalDispositionSaveData
                 disposition in batch.nonPhysicalDispositions.OrderBy(
                     value => value.outputLineId,
                     StringComparer.Ordinal))
        {
            values.Add(disposition.lineCommitId);
            values.Add(disposition.outputLineId);
            values.Add(((int)disposition.role).ToString(
                CultureInfo.InvariantCulture));
            values.Add(disposition.canonicalPayload);
            values.Add(disposition.dispositionFingerprint);
            values.Add(disposition.exactMassGrams.ToString(
                CultureInfo.InvariantCulture));
        }
        foreach (var line in batch.lines.OrderBy(value => value.outputLineId,
                     StringComparer.Ordinal))
        {
            values.Add(line.lineCommitId);
            values.Add(line.outputLineId);
            values.Add(((int)line.role).ToString(CultureInfo.InvariantCulture));
            values.Add(line.itemId);
            values.Add(line.componentFingerprint);
            values.Add(line.outputCapabilityId);
            values.Add(line.outputCapabilityVersion.ToString(
                CultureInfo.InvariantCulture));
            values.Add(line.outputComponentCodecId);
            values.Add(line.outputComponentCodecVersion.ToString(
                CultureInfo.InvariantCulture));
            values.Add(line.outputCapabilityFingerprint);
            values.Add(line.originalQuantity.ToString(CultureInfo.InvariantCulture));
            values.Add(line.originalMassGrams.ToString(CultureInfo.InvariantCulture));
        }
        return Digest(values.ToArray());
    }

    private static string RequestFingerprint(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData operation) => Digest(
        RequestV1, operation.routeOperationId, batch.batchCommitId,
        line.destinationId, operation.targetDestinationId,
        operation.targetPositionX.ToString(CultureInfo.InvariantCulture),
        operation.targetPositionY.ToString(CultureInfo.InvariantCulture),
        line.outputLineId, line.lineCommitId,
        operation.sourceOffsetQuantity.ToString(CultureInfo.InvariantCulture),
        operation.routedQuantity.ToString(CultureInfo.InvariantCulture),
        line.itemId, line.componentFingerprint,
        operation.routedMassGrams.ToString(CultureInfo.InvariantCulture));

    public static string ComputePhysicalReceiptFingerprint(
        ProductionPreparedOutputPhysicalRouteReceipt receipt)
    {
        List<string> values = new()
        {
            ReceiptV1, receipt.RequestFingerprint, receipt.RouteOperationId,
            receipt.BatchCommitId, receipt.SourceDestinationId,
            receipt.TargetDestinationId,
            receipt.TargetPositionX.ToString(CultureInfo.InvariantCulture),
            receipt.TargetPositionY.ToString(CultureInfo.InvariantCulture),
            receipt.TotalQuantity.ToString(CultureInfo.InvariantCulture),
            receipt.TotalMassGrams.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var slice in (receipt.Slices ?? Array.Empty<
                     ProductionPreparedOutputPhysicalRouteSliceReceipt>())
                 .OrderBy(value => value.SourceStackId, StringComparer.Ordinal)
                 .ThenBy(value => value.RoutedStackId, StringComparer.Ordinal))
        {
            values.Add(slice.SourceStackId);
            values.Add(slice.RoutedStackId);
            values.Add(slice.OutputLineId);
            values.Add(slice.LineCommitId);
            values.Add(slice.SourceOffsetQuantity.ToString(CultureInfo.InvariantCulture));
            values.Add(slice.RoutedOffsetQuantity.ToString(CultureInfo.InvariantCulture));
            values.Add(slice.RoutedQuantity.ToString(CultureInfo.InvariantCulture));
            values.Add(slice.ItemId);
            values.Add(slice.ComponentFingerprint);
            values.Add(slice.RoutedMassGrams.ToString(CultureInfo.InvariantCulture));
        }
        return Digest(values.ToArray());
    }

    private static string SavedReceiptFingerprint(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData operation)
    {
        ProductionPreparedOutputPhysicalRouteSliceReceipt[] slices =
            operation.physicalSlices.Select(value => new
                ProductionPreparedOutputPhysicalRouteSliceReceipt(
                    value.sourceStackId,
                    value.routedStackId,
                    line.outputLineId,
                    line.lineCommitId,
                    line.itemId,
                    value.sourceOffsetQuantity,
                    value.routedOffsetQuantity,
                    value.routedQuantity,
                    value.routedMassGrams,
                    line.componentFingerprint))
                .ToArray();
        return ComputePhysicalReceiptFingerprint(new
            ProductionPreparedOutputPhysicalRouteReceipt(
                operation.routeOperationId,
                operation.requestFingerprint,
                operation.physicalReceiptFingerprint,
                batch.batchCommitId,
                line.destinationId,
                operation.targetDestinationId,
                operation.targetPositionX,
                operation.targetPositionY,
                operation.routedQuantity,
                operation.routedMassGrams,
                slices));
    }

    private static string Digest(params string[] values)
    {
        StringBuilder text = new();
        foreach (string value in values)
        {
            string exact = value ?? string.Empty;
            text.Append(Encoding.UTF8.GetByteCount(exact).ToString(
                CultureInfo.InvariantCulture));
            text.Append(':').Append(exact).Append('|');
        }
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static bool IsPhysicalRole(ProductionOutputRole role) =>
        ProductionOutputRoleRules.IsPhysical(role);

    private static void RequireBill(ProductionBillId billId)
    {
        if (!billId.IsValid)
            throw new ArgumentException("Production bill id is invalid.", nameof(billId));
    }

    private static void RequireCanonical(string value, string label)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException($"Routing {label} is not canonical.");
    }

    private static void RequireCanonicalOptional(string value, string label)
    {
        if (value == null
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Routing {label} is not canonical optional text.");
        }
    }

    private static void RequireDigest(string value, string label)
    {
        if (!IsDigest(value))
            throw new InvalidOperationException($"Routing {label} is not SHA-256.");
    }

    private static bool IsDigest(string value) =>
        value != null && value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');

    private PreparedOutputCheckpointGcResult GcResult(
        PreparedOutputCheckpointGcStatus status,
        PreparedOutputCheckpointGcReason reason,
        PreparedOutputCheckpointGcContext context,
        string message,
        int collectedBatchCount = 0) => new(
        status,
        reason,
        context.CheckpointSequence,
        message,
        collectedBatchCount);

    private CheckpointGcCandidate RequireGcCandidate(
        IPreparedOutputCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointGcCandidate exact
            || !string.Equals(exact.ParticipantId,
                CheckpointGcParticipantId,
                StringComparison.Ordinal)
            || exact.ParticipantKind != CheckpointGcParticipantKind)
        {
            throw new InvalidOperationException(
                "Prepared-output checkpoint candidate owner conflicts.");
        }
        return exact;
    }

    private static DeliveryRerouteCandidate RequireDeliveryRerouteCandidate(
        IProductionPreparedOutputDeliveryRerouteCandidate candidate)
    {
        if (candidate is not DeliveryRerouteCandidate exact)
        {
            throw new InvalidOperationException(
                "Delivery reroute candidate owner conflicts.");
        }
        return exact;
    }

    private sealed class CheckpointGcCandidate :
        IPreparedOutputCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            PreparedOutputCheckpointGcContext context,
            long sourceRevision,
            Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
                previousBatches,
            long previousSequence,
            string previousDigest,
            Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
                nextBatches,
            IReadOnlyList<string> batchCommitIds,
            IReadOnlyList<string> routeOperationIds)
        {
            Context = context;
            SourceRevision = sourceRevision;
            PreviousBatches = previousBatches
                ?? throw new ArgumentNullException(nameof(previousBatches));
            PreviousSequence = previousSequence;
            PreviousDigest = previousDigest ?? string.Empty;
            NextBatches = nextBatches
                ?? throw new ArgumentNullException(nameof(nextBatches));
            BatchCommitIds = batchCommitIds ?? Array.Empty<string>();
            RouteOperationIds = routeOperationIds ?? Array.Empty<string>();
        }

        public string ParticipantId =>
            "540.economy.prepared-output-routing";
        public PreparedOutputCheckpointGcParticipantKind ParticipantKind =>
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority;
        public long CheckpointSequence => Context.CheckpointSequence;
        public string SerializedByteDigest => Context.SerializedByteDigest;
        public IReadOnlyList<string> BatchCommitIds { get; }
        public IReadOnlyList<string> RouteOperationIds { get; }
        internal PreparedOutputCheckpointGcContext Context { get; }
        internal long SourceRevision { get; }
        internal Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
            PreviousBatches { get; }
        internal long PreviousSequence { get; }
        internal string PreviousDigest { get; }
        internal Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
            NextBatches { get; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }

    private sealed class DeliveryRerouteCandidate :
        IProductionPreparedOutputDeliveryRerouteCandidate
    {
        internal DeliveryRerouteCandidate(
            string routeOperationId,
            ProductionPreparedOutputDeliveryRevisionSaveData expected,
            ProductionPreparedOutputDeliveryRevisionSaveData next,
            long sourceRoutingRevision,
            Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
                previousBatches,
            Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
                nextBatches,
            long nextRoutingRevision,
            bool alreadyApplied)
        {
            RouteOperationId = routeOperationId;
            Expected = expected?.Clone()
                ?? throw new ArgumentNullException(nameof(expected));
            Next = next?.Clone() ?? throw new ArgumentNullException(nameof(next));
            SourceRoutingRevision = sourceRoutingRevision;
            PreviousBatches = previousBatches
                ?? throw new ArgumentNullException(nameof(previousBatches));
            NextBatches = nextBatches
                ?? throw new ArgumentNullException(nameof(nextBatches));
            NextRoutingRevision = nextRoutingRevision;
            AlreadyApplied = alreadyApplied;
        }

        public string RouteOperationId { get; }
        public string RerouteOperationId => Next.rerouteOperationId;
        public long ExpectedCurrentRevision => Expected.revision;
        public string ExpectedCurrentRevisionFingerprint =>
            Expected.revisionFingerprint;
        public string PreviousRevisionFingerprint =>
            Next.previousRevisionFingerprint;
        public long NextRevision => Next.revision;
        public string NextRevisionFingerprint => Next.revisionFingerprint;
        public string OriginalPhysicalReceiptFingerprint =>
            Next.originalPhysicalReceiptFingerprint;
        public ProductionPreparedOutputDeliveryRerouteReason Reason => Next.reason;
        public string TargetDestinationId => Next.targetDestinationId;
        public int TargetPositionX => Next.targetPositionX;
        public int TargetPositionY => Next.targetPositionY;
        public string TargetAuthorityFingerprint =>
            Next.targetAuthorityFingerprint;
        internal ProductionPreparedOutputDeliveryRevisionSaveData Expected { get; }
        internal ProductionPreparedOutputDeliveryRevisionSaveData Next { get; }
        internal long SourceRoutingRevision { get; }
        internal Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
            PreviousBatches { get; }
        internal Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
            NextBatches { get; }
        internal long NextRoutingRevision { get; }
        internal bool AlreadyApplied { get; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }

    private sealed class Context
    {
        internal Context(
            ProductionPreparedOutputRoutingBatchSaveData batch,
            ProductionPreparedOutputRoutingLineSaveData line,
            ProductionPreparedOutputRouteOperationSaveData operation)
        {
            Batch = batch;
            Line = line;
            Operation = operation;
        }
        internal ProductionPreparedOutputRoutingBatchSaveData Batch { get; }
        internal ProductionPreparedOutputRoutingLineSaveData Line { get; }
        internal ProductionPreparedOutputRouteOperationSaveData Operation { get; }
    }
}
