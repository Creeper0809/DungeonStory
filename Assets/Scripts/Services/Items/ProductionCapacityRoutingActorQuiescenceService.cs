using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>Owns atomic capacity-routing carry quiescence and durable physical-receipt verification.</summary>
internal sealed class ProductionCapacityRoutingActorQuiescenceService
{
    private readonly IFacilityBufferDestinationClaimQuery destinationClaims;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly WorldItemRepository repository;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly WorldItemTransferPhysicalProjection physicalProjection;
#if UNITY_EDITOR
    internal Func<int, bool> DebugFailBeforeCapacityActorQuiescenceMutation;
#endif

    internal ProductionCapacityRoutingActorQuiescenceService(
        IFacilityBufferDestinationClaimQuery destinationClaims,
        IGridSystemProvider gridSystemProvider,
        IItemMarkerPresenter markerPresenter,
        IItemQuantityReservationService quantityReservations,
        WorldItemRepository repository,
        ICharacterAiWorldRegistry worldRegistry,
        WorldItemTransferPhysicalProjection physicalProjection)
    {
        this.destinationClaims = destinationClaims;
        this.gridSystemProvider = gridSystemProvider;
        this.markerPresenter = markerPresenter;
        this.quantityReservations = quantityReservations;
        this.repository = repository;
        this.worldRegistry = worldRegistry;
        this.physicalProjection = physicalProjection;
    }

    [GameplayInternalOnly(
        "Atomically publishes one exact carried descendant vector and its durable actor receipt.",
        "Production capacity-routing destructive-drain participant only")]
    public ProductionCapacityRoutingActorQuiescenceResult
        TryQuiesceAtCurrentCell(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            ProductionCapacityRoutingActorQuiescenceRequest request)
    {
        static ProductionCapacityRoutingActorQuiescenceResult Fail(
            ProductionCapacityRoutingDrainStatus status,
            string reason) => new(
            status,
            default,
            0,
            0L,
            null,
            reason);

        if (actor == null || inventory == null || request == null)
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                "capacity-routing-actor-quiescence-runtime-unavailable");
        if (!CharacterPersistentIdentity.TryGet(actor, out CharacterId actorId)
            || !actorId.Equals(inventory.CharacterId)
            || !string.Equals(
                actorId.Value,
                request.ActorPersistentId,
                StringComparison.Ordinal)
            || !IsCanonicalQuiescenceToken(request.StepOperationId)
            || !IsCanonicalQuiescenceToken(request.BatchCommitId)
            || !IsLowerSha256Token(request.DrainRequestFingerprint)
            || request.Plan == null
            || !string.Equals(
                request.Plan.ActorPersistentId,
                request.ActorPersistentId,
                StringComparison.Ordinal)
            || !IsLowerSha256Token(request.Plan.Fingerprint)
            || request.Plan.QuantityLeaseIds.Count == 0
            || request.Plan.PickedLeaseIds.Count == 0
            || request.Plan.PickedLeaseIds.Any(value =>
                !request.Plan.QuantityLeaseIds.Contains(
                    value,
                    StringComparer.Ordinal))
            || request.ExpectedCarries == null
            || request.ExpectedCarries.Count == 0)
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Conflict,
                "capacity-routing-actor-quiescence-request-invalid");
        }
        if (!repository.TryGetPendingCapacityRoutingDrain(
                request.StepOperationId,
                out ProductionCapacityRoutingDrainSaveData pending)
            || pending == null
            || !string.Equals(
                pending.batchCommitId,
                request.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                pending.requestFingerprint,
                request.DrainRequestFingerprint,
                StringComparison.Ordinal))
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Conflict,
                "capacity-routing-actor-quiescence-drain-authority-conflict");
        }

        string[] expectedRowKeys = request.ExpectedCarries
            .Select(ProductionCapacityRoutingDrainFingerprint.ActorCarryKey)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] plannedRowKeys = pending.sourceActorCarries
            .Where(value => string.Equals(
                value.actorPersistentId,
                request.ActorPersistentId,
                StringComparison.Ordinal))
            .Select(ProductionCapacityRoutingDrainFingerprint.ActorCarryKey)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expectedOperationIds = request.ExpectedCarries
            .Select(value => value.haulIntentOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (expectedRowKeys.Length == 0
            || expectedRowKeys.Distinct(StringComparer.Ordinal).Count()
                != expectedRowKeys.Length
            || !expectedRowKeys.SequenceEqual(
                plannedRowKeys,
                StringComparer.Ordinal)
            || !request.Plan.OperationIds.SequenceEqual(
                expectedOperationIds,
                StringComparer.Ordinal))
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Conflict,
                "capacity-routing-actor-quiescence-row-vector-conflict");
        }

        ProductionCapacityRoutingActorQuiesceReceiptSaveData durableReceipt =
            pending.actorQuiesceReceipts.FirstOrDefault(value => value != null
                && string.Equals(
                    value.actorPersistentId,
                    request.ActorPersistentId,
                    StringComparison.Ordinal));
        if (durableReceipt != null)
        {
            if (!durableReceipt.carriedRowKeys.SequenceEqual(
                    expectedRowKeys,
                    StringComparer.Ordinal)
                || !string.Equals(
                    durableReceipt.receiptFingerprint,
                    ProductionCapacityRoutingDrainFingerprint
                        .CreateActorQuiesceReceiptFingerprint(
                            pending.stepOperationId,
                            pending.requestFingerprint,
                            durableReceipt),
                    StringComparison.Ordinal))
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Conflict,
                    "capacity-routing-actor-quiescence-durable-receipt-conflict");
            }
            if (!TryVerifyDurableReceipt(
                    actor,
                    inventory,
                    pending,
                    durableReceipt,
                    out string replayFailure))
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Conflict,
                    replayFailure);
            }
            int replayQuantity = request.ExpectedCarries.Sum(value => value.quantity);
            long replayMass = request.ExpectedCarries.Sum(value => value.massGrams);
            return new ProductionCapacityRoutingActorQuiescenceResult(
                ProductionCapacityRoutingDrainStatus.Replay,
                new Vector2Int(
                    durableReceipt.physicalCellX,
                    durableReceipt.physicalCellY),
                replayQuantity,
                replayMass,
                durableReceipt,
                string.Empty);
        }
        if (pending.phase != ProductionCapacityRoutingDrainPhase.QuiescingActors)
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                "capacity-routing-actor-quiescence-phase-mismatch");
        }
        string[] plannedActors = pending.sourceActorCarries
            .Select(value => value.actorPersistentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (pending.actorQuiesceReceipts.Count >= plannedActors.Length
            || !string.Equals(
                plannedActors[pending.actorQuiesceReceipts.Count],
                request.ActorPersistentId,
                StringComparison.Ordinal))
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                "capacity-routing-actor-quiescence-out-of-order");
        }

        if (!inventory.TryPrepareCapacityRoutingExactPhysicalTransfer(
                request.ExpectedCarries,
                out CharacterCarryInventory.ExactPhysicalTransferCandidate
                    carryCandidate,
                out string carryFailure))
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                carryFailure);
        }

        Vector2Int physicalCell = WorldItemTransferLocationRules.ResolveActorGridPosition(actor);
        List<WorldItemStackRecord> records = new(request.ExpectedCarries.Count);
        Dictionary<string, HaulDeliveryIntentSaveData> intents =
            new(StringComparer.Ordinal);
        string targetDestinationId = string.Empty;
        Vector2Int targetPosition = default;
        int totalQuantity = 0;
        long totalMass = 0L;
        foreach (ProductionCapacityRoutingDrainActorCarrySaveData expected in
                 request.ExpectedCarries)
        {
            if (!TryValidateCapacityActorCarry(
                    actorId.Value,
                    request.BatchCommitId,
                    physicalCell,
                    expected,
                    out WorldItemStackRecord record,
                    out HaulDeliveryIntentSaveData intent,
                    out Vector2Int authorityTargetPosition,
                    out string rowFailure))
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Deferred,
                    rowFailure);
            }
            if (targetDestinationId.Length == 0)
            {
                targetDestinationId = intent.destinationId;
                targetPosition = authorityTargetPosition;
            }
            else if (!string.Equals(
                         targetDestinationId,
                         intent.destinationId,
                         StringComparison.Ordinal)
                     || targetPosition != authorityTargetPosition)
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Deferred,
                    "capacity-routing-mixed-carried-destination");
            }
            records.Add(record);
            intents[intent.operationId] = intent;
            try
            {
                totalQuantity = checked(totalQuantity + expected.quantity);
                totalMass = checked(totalMass + expected.massGrams);
            }
            catch (OverflowException)
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Conflict,
                    "capacity-routing-actor-quiescence-total-overflow");
            }
        }
        foreach (KeyValuePair<string, HaulDeliveryIntentSaveData> pair in intents)
        {
            string[] expectedCommitments = request.ExpectedCarries
                .Where(value => string.Equals(
                    value.haulIntentOperationId,
                    pair.Key,
                    StringComparison.Ordinal))
                .Select(value => value.carriedStackId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] actualCommitments = (pair.Value.commitments
                    ?? new List<HaulDeliveryItemCommitmentSaveData>())
                .Where(value => value != null && value.quantity > 0)
                .Select(value => value.carriedStackId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!actualCommitments.SequenceEqual(
                    expectedCommitments,
                    StringComparer.Ordinal))
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Deferred,
                    "capacity-routing-mixed-or-extra-intent-commitment:"
                    + pair.Key);
            }
        }

        string prePhysicalFingerprint =
            CreateCapacityActorPhysicalFingerprint(records);
        if (!carryCandidate.TryPublish(out string publishFailure))
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                publishFailure);
        }

        bool committed = repository.TryQuiesceCarriedBatchAtomically(
            records,
            physicalCell,
            targetDestinationId,
            targetPosition,
            pending,
            () => CreateCapacityActorQuiescenceReceipt(
                pending,
                request,
                physicalCell,
                expectedRowKeys,
                prePhysicalFingerprint,
                CreateCapacityActorPhysicalFingerprint(records)),
#if UNITY_EDITOR
            DebugFailBeforeCapacityActorQuiescenceMutation,
#else
            null,
#endif
            out ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
            out string repositoryFailure);
        if (!committed)
        {
            carryCandidate.Rollback();
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                repositoryFailure);
        }

        carryCandidate.Complete();
        markerPresenter.RefreshAt(physicalCell);
        return new ProductionCapacityRoutingActorQuiescenceResult(
            ProductionCapacityRoutingDrainStatus.Applied,
            physicalCell,
            totalQuantity,
            totalMass,
            receipt,
            string.Empty);
    }

    public bool TryVerifyDurableReceipt(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        ProductionCapacityRoutingDrainSaveData drain,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null
            || inventory == null
            || drain == null
            || receipt == null
            || !CharacterPersistentIdentity.TryGet(
                actor,
                out CharacterId actorId)
            || !actorId.Equals(inventory.CharacterId)
            || !string.Equals(
                actorId.Value,
                receipt.actorPersistentId,
                StringComparison.Ordinal)
            || !string.Equals(
                drain.batchCommitId,
                receipt.batchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.receiptFingerprint,
                ProductionCapacityRoutingDrainFingerprint
                    .CreateActorQuiesceReceiptFingerprint(
                        drain.stepOperationId,
                        drain.requestFingerprint,
                        receipt),
                StringComparison.Ordinal))
        {
            failureReason =
                "capacity-routing-durable-actor-receipt-identity-conflict";
            return false;
        }

        ProductionCapacityRoutingDrainActorCarrySaveData[] carries =
            (drain.sourceActorCarries
                ?? new List<ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.actorPersistentId,
                    receipt.actorPersistentId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ThenBy(value => value.haulIntentOperationId, StringComparer.Ordinal)
            .ToArray();
        string[] rowKeys = carries
            .Select(ProductionCapacityRoutingDrainFingerprint.ActorCarryKey)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] operationIds = carries
            .Select(value => value.haulIntentOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (carries.Length == 0
            || !rowKeys.SequenceEqual(
                receipt.carriedRowKeys,
                StringComparer.Ordinal)
            || inventory.Items.Any(item => item != null
                && item.quantity > 0
                && operationIds.Contains(
                    item.ownerOperationId,
                    StringComparer.Ordinal)))
        {
            failureReason =
                "capacity-routing-durable-actor-receipt-cargo-conflict";
            return false;
        }

        Vector2Int cell = new(receipt.physicalCellX, receipt.physicalCellY);
        List<WorldItemStackRecord> records = new(carries.Length);
        foreach (ProductionCapacityRoutingDrainActorCarrySaveData carry in carries)
        {
            if (!repository.RecordsById.TryGetValue(
                    carry.carriedStackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.state != WorldItemStackState.Loose
                || record.position != cell
                || record.quantity != carry.quantity
                || string.IsNullOrEmpty(record.destinationId)
                || !record.hasDestinationPosition
                || record.dropDisposition != WorldItemDropDisposition.None
                || !string.IsNullOrEmpty(record.recoveryOwnerOperationId)
                || !string.IsNullOrEmpty(record.recoverySourceStackId)
                || !string.IsNullOrEmpty(record.recoveryCarrierPersistentId)
                || record.recoveryInterruptionKind !=
                    WorldItemCarryInterruptionKind.None)
            {
                failureReason =
                    "capacity-routing-durable-actor-receipt-physical-conflict:"
                    + carry.carriedStackId;
                return false;
            }
            records.Add(record);
        }
        if (!string.Equals(
                CreateCapacityActorPhysicalFingerprint(records),
                receipt.postPhysicalFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "capacity-routing-durable-actor-receipt-fingerprint-conflict";
            return false;
        }

        string[] liveLeaseIds = operationIds
            .SelectMany(operationId => quantityReservations.TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                ? leases.Select(lease => lease.leaseId)
                : Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        ProductionCapacityRoutingActorAuthorityReleaseSaveData release =
            drain.actorAuthorityReleases?.FirstOrDefault(value => value != null
                && string.Equals(
                    value.actorPersistentId,
                    receipt.actorPersistentId,
                    StringComparison.Ordinal));
        bool leasesValid = release == null
            ? liveLeaseIds.SequenceEqual(
                receipt.quantityLeaseIds,
                StringComparer.Ordinal)
            : release.effectsCommitted
                ? liveLeaseIds.Length == 0
                : release.operations.All(row =>
                {
                    string[] current = quantityReservations
                        .TryGetLeasesByOwner(
                            row.operationId,
                            out IReadOnlyList<ItemQuantityLease> rowLeases)
                        ? rowLeases.Select(value => value.leaseId)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray()
                        : Array.Empty<string>();
                    return current.Length == 0
                        || current.SequenceEqual(
                            row.quantityLeaseIds,
                            StringComparer.Ordinal);
                });
        if (!leasesValid)
        {
            failureReason =
                "capacity-routing-durable-actor-receipt-lease-conflict";
            return false;
        }
        return true;
    }

    private bool TryValidateCapacityActorCarry(
        string actorPersistentId,
        string batchCommitId,
        Vector2Int physicalCell,
        ProductionCapacityRoutingDrainActorCarrySaveData expected,
        out WorldItemStackRecord record,
        out HaulDeliveryIntentSaveData intent,
        out Vector2Int authorityTargetPosition,
        out string failureReason)
    {
        record = null;
        intent = null;
        authorityTargetPosition = default;
        failureReason = string.Empty;
        if (expected == null
            || !string.Equals(
                expected.actorPersistentId,
                actorPersistentId,
                StringComparison.Ordinal)
            || !IsCanonicalQuiescenceToken(expected.haulIntentOperationId)
            || !IsCanonicalQuiescenceToken(expected.routeOperationId)
            || !IsCanonicalQuiescenceToken(expected.carriedStackId)
            || !IsCanonicalQuiescenceToken(expected.sourceStackId)
            || expected.quantity <= 0
            || expected.massGrams <= 0L
            || !IsLowerSha256Token(expected.stackSignature)
            || !repository.RecordsById.TryGetValue(
                expected.carriedStackId,
                out record)
            || record == null
            || record.state is not (WorldItemStackState.Carried
                or WorldItemStackState.InTransit)
            || !string.Equals(
                record.destinationId,
                actorPersistentId,
                StringComparison.Ordinal)
            || !record.hasDestinationPosition
            || record.quantity != expected.quantity
            || record.dropDisposition != WorldItemDropDisposition.None
            || !string.IsNullOrEmpty(record.recoveryOwnerOperationId)
            || !string.IsNullOrEmpty(record.recoverySourceStackId)
            || !string.IsNullOrEmpty(record.recoveryCarrierPersistentId)
            || record.recoveryInterruptionKind !=
                WorldItemCarryInterruptionKind.None
            || record.droppedAtGameTime != 0d
            || record.recoveryDeadlineGameTime != 0d
            || !string.Equals(
                ProductionCapacityRoutingDrainFingerprint
                    .CreateActorCarryStackSignature(
                        record.itemId,
                        record.itemInstanceId,
                        record.components),
                expected.stackSignature,
                StringComparison.Ordinal)
            || !FacilityOutputExactRouteCustodyCodec.TryRead(
                record.components,
                out FacilityOutputExactRouteCustodyMetadata custody)
            || custody.Phase != FacilityOutputExactRouteCustodyPhase.Routable
            || !string.Equals(
                custody.BatchCommitId,
                batchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                custody.RouteOperationId,
                expected.routeOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                custody.CurrentSourceStackId,
                expected.sourceStackId,
                StringComparison.Ordinal)
            || custody.Quantity != expected.quantity
            || custody.MassGrams != expected.massGrams)
        {
            failureReason = "capacity-routing-carried-physical-row-conflict:"
                + (expected?.carriedStackId ?? string.Empty);
            return false;
        }

        // A carried repository row is a durable custody record, not a
        // per-movement position tracker. The actor/inventory pair is the live
        // physical location authority while the exact carried identity is
        // still joined above. TryQuiesceCarriedBatchAtomically relocates that
        // row to physicalCell in the same candidate/commit transaction.

        long actualMass;
        try
        {
            actualMass = physicalProjection.GetPhysicalQuantityMass(
                record.itemId,
                record.itemInstanceId,
                WorldItemTransferMetadataRules.CapturePhysicalBusinessComponents(record.components),
                record.quantity);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            failureReason = "capacity-routing-carried-mass-invalid:"
                + record.stackId + ":" + exception.Message;
            return false;
        }
        if (actualMass != expected.massGrams
            || !repository.HaulDeliveryIntents.TryCapture(
                expected.haulIntentOperationId,
                out intent)
            || intent == null
            || !string.Equals(
                intent.operationId,
                expected.haulIntentOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                intent.ownerCharacterId,
                actorPersistentId,
                StringComparison.Ordinal))
        {
            failureReason = "capacity-routing-carried-intent-or-mass-conflict:"
                + record.stackId;
            return false;
        }
        HaulDeliveryItemCommitmentSaveData commitment = intent.commitments?
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.carriedStackId,
                    expected.carriedStackId,
                    StringComparison.Ordinal));
        string readableSignature = ItemStackSignature.Create(
            record.itemId,
            record.components);
        if (commitment == null
            || !string.Equals(
                commitment.sourceStackId,
                expected.sourceStackId,
                StringComparison.Ordinal)
            || commitment.quantity != expected.quantity
            || !string.Equals(
                commitment.expectedStackSignature,
                readableSignature,
                StringComparison.Ordinal))
        {
            failureReason = "capacity-routing-carried-commitment-conflict:"
                + record.stackId;
            return false;
        }

        string custodyTarget = !string.IsNullOrEmpty(
                custody.CurrentTargetDestinationId)
            ? custody.CurrentTargetDestinationId
            : custody.TargetDestinationId;
        authorityTargetPosition = custody.CurrentTargetPosition;
        Vector2Int savedDelivery = new(
            intent.deliveryGridX,
            intent.deliveryGridY);
        Vector2Int savedDrop = new(intent.dropGridX, intent.dropGridY);
        if (!string.Equals(
                intent.destinationId,
                custodyTarget,
                StringComparison.Ordinal)
            || !gridSystemProvider.TryGetGrid(out Grid grid)
            || !WorldItemHaulDestinationAuthority.TryResolve(
                grid,
                worldRegistry,
                destinationClaims,
                intent.destinationKind,
                custodyTarget,
                custody.CurrentTargetPosition,
                out WorldItemHaulDestinationAuthority.Resolution resolution,
                out _)
            || resolution.Kind != intent.destinationKind
            || !string.Equals(
                resolution.DestinationId,
                custodyTarget,
                StringComparison.Ordinal)
            || resolution.DeliveryPosition != savedDelivery
            || resolution.DropPosition != savedDrop)
        {
            failureReason = "capacity-routing-carried-target-conflict:"
                + record.stackId;
            return false;
        }
        return true;
    }

    private static ProductionCapacityRoutingActorQuiesceReceiptSaveData
        CreateCapacityActorQuiescenceReceipt(
            ProductionCapacityRoutingDrainSaveData pending,
            ProductionCapacityRoutingActorQuiescenceRequest request,
            Vector2Int physicalCell,
            IEnumerable<string> expectedRowKeys,
            string prePhysicalFingerprint,
            string postPhysicalFingerprint)
    {
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt = new()
        {
            actorPersistentId = request.ActorPersistentId,
            batchCommitId = request.BatchCommitId,
            physicalCellX = physicalCell.x,
            physicalCellY = physicalCell.y,
            carriedRowKeys = expectedRowKeys
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            quantityLeaseIds = request.Plan.QuantityLeaseIds.ToList(),
            warehouseAdmissionTokenIds = request.Plan
                .WarehouseAdmissionTokenIds.ToList(),
            activePlanFingerprint = request.Plan.Fingerprint,
            prePhysicalFingerprint = prePhysicalFingerprint,
            postPhysicalFingerprint = postPhysicalFingerprint
        };
        receipt.receiptFingerprint = ProductionCapacityRoutingDrainFingerprint
            .CreateActorQuiesceReceiptFingerprint(
                pending.stepOperationId,
                pending.requestFingerprint,
                receipt);
        return receipt;
    }

    private static string CreateCapacityActorPhysicalFingerprint(
        IEnumerable<WorldItemStackRecord> source) =>
        ProductionCapacityRoutingActorPhysicalFingerprint.Create(source);

    private static bool IsCanonicalQuiescenceToken(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsLowerSha256Token(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}
