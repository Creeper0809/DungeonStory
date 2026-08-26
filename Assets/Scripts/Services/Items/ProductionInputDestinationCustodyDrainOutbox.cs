using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Items-owned durable progress authority for one generic-production input
/// destination drain. Physical effects are executed by a separate port; this
/// object records one monotonic actor, operation-authority, or destination step
/// at a time so faults roll forward without replaying an earlier physical effect.
/// </summary>
public sealed class ProductionInputDestinationCustodyDrainOutbox :
    IProductionInputDestinationCustodyDrainOutbox
{
    private readonly WorldItemRepository repository;

    public ProductionInputDestinationCustodyDrainOutbox(
        WorldItemRepository repository)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    [GameplayInternalOnly(
        "Persists the immutable Items-owned input-destination drain request.",
        "Production input-destination drain service and generic terminal producer only")]
    public ProductionInputDestinationCustodyDrainResult TryPrepare(
        ProductionInputDestinationCustodyDrainRequest request)
    {
        if (!IsValid(request, out string failure))
            return Conflict(failure);
        if (repository.TryGetPendingProductionInputDestinationDrain(
                request.StepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData existing))
        {
            return Matches(existing, request)
                ? Current(existing,
                    ProductionInputDestinationCustodyDrainStatus.Replay)
                : Conflict(
                    "production-input-destination-drain-request-conflict");
        }

        ProductionInputDestinationCustodyDrainSaveData prepared = new()
        {
            parentOperationId = request.ParentOperationId,
            stepOperationId = request.StepOperationId,
            ownerStableId = request.OwnerStableId,
            billId = request.BillId,
            facilityId = request.FacilityId,
            sourceDestinationId = request.SourceDestinationId,
            ownerGridX = request.OwnerGridX,
            ownerGridY = request.OwnerGridY,
            sourceClaimFingerprint = request.SourceClaimFingerprint,
            sourceOwnershipFingerprint = request.SourceOwnershipFingerprint,
            requestFingerprint = request.RequestFingerprint,
            phase = ProductionInputDestinationCustodyDrainPhase.Prepared,
            sourceStacks = request.SourceStacks.Select(value => value.Clone())
                .ToList(),
            sourceOperations = request.SourceOperations.Select(value => value.Clone())
                .ToList(),
            sourceActors = request.SourceActors.Select(value => value.Clone()).ToList(),
            inputQuantity = request.InputQuantity,
            inputMassGrams = request.InputMassGrams
        };
        repository.SetPendingProductionInputDestinationDrain(prepared);
        return Current(prepared,
            ProductionInputDestinationCustodyDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Advances the durable input-destination drain into actor release.",
        "Production input-destination drain service only")]
    public ProductionInputDestinationCustodyDrainResult TryBeginDraining(
        string stepOperationId,
        string requestFingerprint)
    {
        if (!TryGet(stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData value))
            return Conflict("production-input-destination-drain-missing");
        if (!string.Equals(
                value.requestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
            return Conflict("production-input-destination-drain-request-conflict");
        if (value.phase != ProductionInputDestinationCustodyDrainPhase.Prepared)
            return Current(value,
                ProductionInputDestinationCustodyDrainStatus.Replay);
        value.phase = ProductionInputDestinationCustodyDrainPhase.ReleasingActors;
        repository.SetPendingProductionInputDestinationDrain(value);
        return Current(value,
            ProductionInputDestinationCustodyDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Records one exact actor custody release after the physical effect succeeds.",
        "Production input-destination drain service only")]
    public ProductionInputDestinationCustodyDrainResult TryRecordActorCompleted(
        string stepOperationId,
        string actorId) => RecordProgress(
        stepOperationId,
        actorId,
        value => value.completedActorIds,
        value => value.sourceActors.Select(actor => actor.actorId).ToList(),
        ProductionInputDestinationCustodyDrainPhase.ReleasingActors,
        "actor");

    [GameplayInternalOnly(
        "Advances the durable drain after every frozen actor is released.",
        "Production input-destination drain service only")]
    public ProductionInputDestinationCustodyDrainResult
        TryBeginReleasingOperationAuthority(string stepOperationId) =>
        AdvancePhase(
            stepOperationId,
            ProductionInputDestinationCustodyDrainPhase.ReleasingActors,
            ProductionInputDestinationCustodyDrainPhase
                .ReleasingOperationAuthority,
            value => value.completedActorIds.SequenceEqual(
                value.sourceActors.Select(actor => actor.actorId),
                StringComparer.Ordinal),
            "production-input-destination-drain-actors-incomplete");

    [GameplayInternalOnly(
        "Records one exact operation authority release after its lease and intent are closed.",
        "Production input-destination drain service only")]
    public ProductionInputDestinationCustodyDrainResult TryRecordOperationReleased(
        string stepOperationId,
        string operationId) => RecordProgress(
        stepOperationId,
        operationId,
        value => value.releasedOperationIds,
        value => value.sourceOperations.Select(operation => operation.operationId)
            .ToList(),
        ProductionInputDestinationCustodyDrainPhase.ReleasingOperationAuthority,
        "operation");

    [GameplayInternalOnly(
        "Advances the durable drain after every frozen operation authority is released.",
        "Production input-destination drain service only")]
    public ProductionInputDestinationCustodyDrainResult TryBeginReleasingDestination(
        string stepOperationId) => AdvancePhase(
        stepOperationId,
        ProductionInputDestinationCustodyDrainPhase.ReleasingOperationAuthority,
        ProductionInputDestinationCustodyDrainPhase.ReleasingDestination,
        value => value.releasedOperationIds.SequenceEqual(
            value.sourceOperations.Select(operation => operation.operationId),
            StringComparer.Ordinal),
        "production-input-destination-drain-operations-incomplete");

    [GameplayInternalOnly(
        "Commits the exact released stack, quantity, gram and physical result receipt.",
        "Production input-destination drain service only")]
    public ProductionInputDestinationCustodyDrainResult TryCommitEffect(
        string stepOperationId,
        IEnumerable<string> releasedStackIds,
        int releasedQuantity,
        long releasedMassGrams,
        string resultFingerprint)
    {
        if (!TryGet(stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData value))
            return Conflict("production-input-destination-drain-missing");
        if (value.phase is ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck
            or ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc)
        {
            return value.releasedQuantity == releasedQuantity
                    && value.releasedMassGrams == releasedMassGrams
                    && string.Equals(value.resultFingerprint, resultFingerprint,
                        StringComparison.Ordinal)
                ? Current(value,
                    ProductionInputDestinationCustodyDrainStatus.Replay)
                : Conflict("production-input-destination-drain-result-conflict");
        }
        if (value.phase !=
            ProductionInputDestinationCustodyDrainPhase.ReleasingDestination)
        {
            return Deferred(
                "production-input-destination-drain-destination-not-releasing");
        }

        string[] released = (releasedStackIds ?? Array.Empty<string>())
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
        string[] expected = value.sourceStacks
            .Select(stack => stack.stackId)
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
        if (releasedQuantity != value.inputQuantity
            || releasedMassGrams != value.inputMassGrams
            || !IsDigest(resultFingerprint)
            || !released.SequenceEqual(expected, StringComparer.Ordinal)
            || !IsCanonicalUnique(released, requireNonEmpty: false))
        {
            return Deferred(
                "production-input-destination-drain-effect-incomplete");
        }

        value.releasedStackIds = released.ToList();
        value.releasedQuantity = releasedQuantity;
        value.releasedMassGrams = releasedMassGrams;
        value.resultFingerprint = resultFingerprint;
        value.commitId = ProductionInputDestinationCustodyDrainFingerprint
            .CreateCommit(value.stepOperationId, value.requestFingerprint);
        value.receiptFingerprint = ProductionInputDestinationCustodyDrainFingerprint
            .CreateReceipt(
                value.requestFingerprint,
                value.resultFingerprint,
                value.releasedQuantity,
                value.releasedMassGrams,
                value.releasedStackIds,
                value.releasedOperationIds);
        value.phase = ProductionInputDestinationCustodyDrainPhase
            .EffectCommittedAwaitingBillAck;
        repository.SetPendingProductionInputDestinationDrain(value);
        return Current(value,
            ProductionInputDestinationCustodyDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Acknowledges the exact child receipt after its Production owner records it.",
        "Production generic terminal producer only")]
    public ProductionInputDestinationCustodyDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (!TryGet(stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData value))
            return Conflict("production-input-destination-drain-missing");
        if (!string.Equals(value.receiptFingerprint, receiptFingerprint,
                StringComparison.Ordinal))
            return Conflict("production-input-destination-drain-receipt-conflict");
        if (value.phase == ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc)
            return Current(value,
                ProductionInputDestinationCustodyDrainStatus.Replay);
        if (value.phase != ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck)
            return Deferred("production-input-destination-drain-effect-not-committed");
        value.phase = ProductionInputDestinationCustodyDrainPhase
            .BillAcknowledgedAwaitingCheckpointGc;
        repository.SetPendingProductionInputDestinationDrain(value);
        return Current(value,
            ProductionInputDestinationCustodyDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Removes an acknowledged child tombstone only at the durable checkpoint boundary.",
        "Production destructive-drain checkpoint recovery only")]
    public ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (!TryGet(stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData value))
        {
            return new ProductionInputDestinationCustodyDrainResult(
                ProductionInputDestinationCustodyDrainStatus.Replay,
                string.Empty,
                receiptFingerprint,
                string.Empty);
        }
        if (value.phase != ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc)
            return Deferred("production-input-destination-drain-not-acknowledged");
        if (!string.Equals(value.receiptFingerprint, receiptFingerprint,
                StringComparison.Ordinal))
            return Conflict("production-input-destination-drain-receipt-conflict");
        repository.RemovePendingProductionInputDestinationDrain(stepOperationId);
        return Current(value,
            ProductionInputDestinationCustodyDrainStatus.Applied);
    }

    public bool TryCapture(
        string stepOperationId,
        out ProductionInputDestinationCustodyDrainSaveData record)
    {
        record = null;
        if (!TryGet(stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData value))
            return false;
        record = value.Clone();
        return true;
    }

    private ProductionInputDestinationCustodyDrainResult RecordProgress(
        string stepOperationId,
        string identity,
        Func<ProductionInputDestinationCustodyDrainSaveData, List<string>> target,
        Func<ProductionInputDestinationCustodyDrainSaveData, List<string>> allowed,
        ProductionInputDestinationCustodyDrainPhase requiredPhase,
        string kind)
    {
        if (!IsToken(identity))
            return Conflict("production-input-destination-drain-" + kind + "-invalid");
        if (!TryGet(stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData value))
            return Conflict("production-input-destination-drain-missing");
        if (value.phase != requiredPhase)
            return Deferred("production-input-destination-drain-phase-mismatch");
        List<string> completed = target(value);
        int index = completed.BinarySearch(identity, StringComparer.Ordinal);
        if (index >= 0)
            return Current(value,
                ProductionInputDestinationCustodyDrainStatus.Replay);
        List<string> planned = allowed(value);
        if (completed.Count >= planned.Count
            || !string.Equals(planned[completed.Count], identity,
                StringComparison.Ordinal))
        {
            return Conflict("production-input-destination-drain-" + kind
                + "-out-of-order-or-not-planned");
        }
        completed.Add(identity);
        repository.SetPendingProductionInputDestinationDrain(value);
        return Current(value,
            ProductionInputDestinationCustodyDrainStatus.Applied);
    }

    private ProductionInputDestinationCustodyDrainResult AdvancePhase(
        string stepOperationId,
        ProductionInputDestinationCustodyDrainPhase expected,
        ProductionInputDestinationCustodyDrainPhase next,
        Func<ProductionInputDestinationCustodyDrainSaveData, bool> canAdvance,
        string incompleteReason)
    {
        if (!TryGet(stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData value))
            return Conflict("production-input-destination-drain-missing");
        if (value.phase > expected)
            return Current(value,
                ProductionInputDestinationCustodyDrainStatus.Replay);
        if (value.phase != expected || !canAdvance(value))
            return Deferred(incompleteReason);
        value.phase = next;
        repository.SetPendingProductionInputDestinationDrain(value);
        return Current(value,
            ProductionInputDestinationCustodyDrainStatus.Applied);
    }

    private bool TryGet(
        string stepOperationId,
        out ProductionInputDestinationCustodyDrainSaveData value)
    {
        value = null;
        if (!IsToken(stepOperationId)
            || !repository.TryGetPendingProductionInputDestinationDrain(
                stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData stored))
            return false;
        value = stored.Clone();
        return true;
    }

    private static bool IsValid(
        ProductionInputDestinationCustodyDrainRequest request,
        out string failure)
    {
        failure = string.Empty;
        if (request == null
            || !IsToken(request.ParentOperationId)
            || !IsToken(request.StepOperationId)
            || !IsToken(request.OwnerStableId)
            || !IsToken(request.BillId)
            || !IsToken(request.FacilityId)
            || !IsToken(request.SourceDestinationId)
            || !IsDigest(request.SourceClaimFingerprint)
            || !IsDigest(request.SourceOwnershipFingerprint)
            || request.InputQuantity < 0
            || request.InputMassGrams < 0L
            || request.SourceStacks.Any(value => !IsValid(value))
            || request.SourceOperations.Any(value => !IsValid(value))
            || request.SourceActors.Any(value => !IsValid(value))
            || !IsUnique(request.SourceStacks.Select(value => value.stackId))
            || !IsUnique(request.SourceOperations.Select(value => value.operationId))
            || !IsUnique(request.SourceActors.Select(value => value.actorId))
            || !TrySumStacks(
                request.SourceStacks,
                out int sourceQuantity,
                out long sourceMassGrams)
            || sourceQuantity != request.InputQuantity
            || sourceMassGrams != request.InputMassGrams)
        {
            failure = "production-input-destination-drain-request-invalid";
            return false;
        }

        HashSet<string> actorIds = request.SourceActors
            .Select(value => value.actorId)
            .ToHashSet(StringComparer.Ordinal);
        if (request.SourceOperations.Any(value => value.hadCommittedPickup
                && !actorIds.Contains(value.actorId))
            || request.SourceActors.Any(actor => actor.allowedOperationIds.Any(
                operationId => !request.SourceOperations.Any(operation =>
                    string.Equals(operation.operationId, operationId,
                        StringComparison.Ordinal)
                    && string.Equals(operation.actorId, actor.actorId,
                        StringComparison.Ordinal)))))
        {
            failure = "production-input-destination-drain-authority-join-invalid";
            return false;
        }

        string expected = ProductionInputDestinationCustodyDrainFingerprint
            .CreateRequest(
                request.ParentOperationId,
                request.StepOperationId,
                request.OwnerStableId,
                request.BillId,
                request.FacilityId,
                request.SourceDestinationId,
                request.OwnerGridX,
                request.OwnerGridY,
                request.SourceClaimFingerprint,
                request.SourceOwnershipFingerprint,
                request.SourceStacks,
                request.SourceOperations,
                request.SourceActors,
                request.InputQuantity,
                request.InputMassGrams);
        if (!string.Equals(expected, request.RequestFingerprint,
                StringComparison.Ordinal))
        {
            failure = "production-input-destination-drain-fingerprint-invalid";
            return false;
        }
        return true;
    }

    private static bool IsValid(ProductionInputDestinationDrainStackSaveData value) =>
        value != null
        && IsToken(value.stackId)
        && IsToken(value.itemId)
        && (string.IsNullOrEmpty(value.itemInstanceId)
            || IsToken(value.itemInstanceId))
        && IsDigest(value.componentFingerprint)
        && value.quantity > 0
        && value.massGrams > 0L
        && Enum.IsDefined(typeof(WorldItemStackState), value.state)
        && value.reservationRevision >= 0L;

    private static bool TrySumStacks(
        IEnumerable<ProductionInputDestinationDrainStackSaveData> source,
        out int quantity,
        out long massGrams)
    {
        quantity = 0;
        massGrams = 0L;
        try
        {
            foreach (ProductionInputDestinationDrainStackSaveData row in
                     source ?? Array.Empty<
                         ProductionInputDestinationDrainStackSaveData>())
            {
                if (row == null)
                    return false;
                quantity = checked(quantity + row.quantity);
                massGrams = checked(massGrams + row.massGrams);
            }
            return true;
        }
        catch (OverflowException)
        {
            quantity = 0;
            massGrams = 0L;
            return false;
        }
    }

    private static bool IsValid(
        ProductionInputDestinationDrainOperationSaveData value) =>
        value != null
        && IsToken(value.operationId)
        && (!value.hadCommittedPickup || IsToken(value.actorId))
        && IsDigest(value.operationFingerprint)
        && IsCanonicalDigestSet(
            value.leaseAuthorityFingerprints,
            requireNonEmpty: false)
        && IsCanonicalUnique(value.carriedStackIds, requireNonEmpty: false)
        && (value.hadCommittedPickup
            ? value.carriedStackIds.Count > 0
            : value.carriedStackIds.Count == 0
                && value.leaseAuthorityFingerprints.Count > 0);

    private static bool IsValid(ProductionInputDestinationDrainActorSaveData value) =>
        value != null
        && IsToken(value.actorId)
        && IsDigest(value.sourcePhysicalFingerprint)
        && IsCanonicalUnique(value.allowedOperationIds, requireNonEmpty: true);

    private static bool Matches(
        ProductionInputDestinationCustodyDrainSaveData value,
        ProductionInputDestinationCustodyDrainRequest request) =>
        value != null
        && value.schemaVersion ==
            ProductionInputDestinationCustodyDrainSaveData.CurrentSchemaVersion
        && string.Equals(value.requestFingerprint, request.RequestFingerprint,
            StringComparison.Ordinal)
        && string.Equals(value.parentOperationId, request.ParentOperationId,
            StringComparison.Ordinal)
        && string.Equals(value.stepOperationId, request.StepOperationId,
            StringComparison.Ordinal);

    private static bool IsUnique(IEnumerable<string> values) =>
        IsCanonicalUnique(values, requireNonEmpty: false);

    private static bool IsCanonicalUnique(
        IEnumerable<string> values,
        bool requireNonEmpty)
    {
        string[] captured = (values ?? Array.Empty<string>()).ToArray();
        return (!requireNonEmpty || captured.Length > 0)
            && captured.All(IsToken)
            && captured.Distinct(StringComparer.Ordinal).Count() == captured.Length
            && captured.SequenceEqual(
                captured.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private static bool IsCanonicalDigestSet(
        IEnumerable<string> values,
        bool requireNonEmpty)
    {
        string[] captured = (values ?? Array.Empty<string>()).ToArray();
        return (!requireNonEmpty || captured.Length > 0)
            && captured.All(IsDigest)
            && captured.Distinct(StringComparer.Ordinal).Count() == captured.Length
            && captured.SequenceEqual(
                captured.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private static bool IsToken(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsDigest(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static ProductionInputDestinationCustodyDrainResult Current(
        ProductionInputDestinationCustodyDrainSaveData value,
        ProductionInputDestinationCustodyDrainStatus status) => new(
        status,
        value?.commitId,
        value?.receiptFingerprint,
        string.Empty);

    private static ProductionInputDestinationCustodyDrainResult Conflict(
        string failure) => new(
        ProductionInputDestinationCustodyDrainStatus.Conflict,
        string.Empty,
        string.Empty,
        failure);

    private static ProductionInputDestinationCustodyDrainResult Deferred(
        string failure) => new(
        ProductionInputDestinationCustodyDrainStatus.Deferred,
        string.Empty,
        string.Empty,
        failure);
}
