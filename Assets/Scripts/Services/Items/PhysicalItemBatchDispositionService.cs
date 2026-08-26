using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using VContainer;

public readonly struct PhysicalItemBatchDispositionReceipt
{
    internal PhysicalItemBatchDispositionReceipt(
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        string requestFingerprint,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams)
    {
        Kind = kind;
        OperationId = operationId;
        ReasonCode = reasonCode;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        SourceStackIds = (sourceStackIds ?? Array.Empty<string>()).ToArray();
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
        CommitId = $"physical-batch-disposition:{(int)kind}:{operationId}:{quantity}:{inputMassGrams}";
    }

    public PhysicalItemDispositionKind Kind { get; }
    public string OperationId { get; }
    public string ReasonCode { get; }
    public string RequestFingerprint { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
    public string CommitId { get; }
    public bool IsCommitted => Kind is PhysicalItemDispositionKind.Transfer
            or PhysicalItemDispositionKind.Sink
        && OperationId?.Length > 0
        && ReasonCode?.Length > 0
        && SourceStackIds?.Count > 0
        && Quantity > 0
        && InputMassGrams > 0L;
}

public interface IPhysicalItemBatchDispositionService
{
    bool TryCommit(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);

    bool TryCommitPending(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);

    bool Acknowledge(string commitId, out string failureReason);

    bool TryGetPending(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt);
}

/// <summary>
/// Item-layer boundary for converting an already reserved physical quantity
/// into a durable pending terminal receipt. The reservation debit and world
/// stack debit are committed as one in-process transaction; the owning domain
/// acknowledges the receipt only after publishing its own terminal state.
/// </summary>
public interface IReservedPhysicalItemBatchDispositionService
{
    bool TryCommitReservedSinkPending(
        string leaseId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);

    bool TryCommitReservedTransferPending(
        string leaseId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);
}

/// <summary>
/// Dedicated terminal boundary for an exact stack already held by a character.
/// Generic batch disposition continues to reject Carried sources; the owning
/// character adapter must update its carry inventory in the same rollback scope.
/// </summary>
public interface ICarriedPhysicalItemBatchDispositionService
{
    bool TryCommitCarriedSinkPending(
        string stackId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);
}

/// <summary>
/// Synchronous all-or-nothing custody transfer or terminal Sink across several
/// world stacks. Mass-changing work is deliberately excluded and must use the
/// transform/WIP boundary.
/// </summary>
public sealed class PhysicalItemBatchDispositionService :
    IPhysicalItemBatchDispositionService,
    IReservedPhysicalItemBatchDispositionService,
    ICarriedPhysicalItemBatchDispositionService
{
    private readonly WorldItemRepository repository;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IItemMarkerPresenter markers;
    private readonly ItemQuantityReservationService quantityReservations;

    public PhysicalItemBatchDispositionService(
        WorldItemRepository repository,
        IPhysicalItemMassQuery massQuery,
        IItemMarkerPresenter markers)
        : this(repository, massQuery, markers, null)
    {
    }

    [Inject]
    public PhysicalItemBatchDispositionService(
        WorldItemRepository repository,
        IPhysicalItemMassQuery massQuery,
        IItemMarkerPresenter markers,
        ItemQuantityReservationService quantityReservations)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
        this.quantityReservations = quantityReservations;
    }

    [GameplayInternalOnly(
        "A domain-owned exact lease may terminally consume its reserved physical quantity.",
        "Character consumables and other registered reserved-Sink owners only")]
    public bool TryCommitReservedSinkPending(
        string leaseId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => TryCommitReservedPending(
        leaseId,
        quantity,
        PhysicalItemDispositionKind.Sink,
        operationId,
        reasonCode,
        out receipt,
        out failureReason);

    [GameplayInternalOnly(
        "An exact owner lease may move a reserved physical output into durable WIP without a haul race.",
        "Apparel physical transaction facade and registered reserved-Transfer owners only")]
    public bool TryCommitReservedTransferPending(
        string leaseId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => TryCommitReservedPending(
        leaseId,
        quantity,
        PhysicalItemDispositionKind.Transfer,
        operationId,
        reasonCode,
        out receipt,
        out failureReason);

    private bool TryCommitReservedPending(
        string leaseId,
        int quantity,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        string leaseKey = leaseId ?? string.Empty;
        string operation = operationId ?? string.Empty;
        string reason = reasonCode ?? string.Empty;
        if (quantityReservations == null)
        {
            failureReason =
                "physical-reserved-disposition-capability-unavailable";
            return false;
        }
        if (!IsCanonicalRequired(leaseKey)
            || quantity <= 0
            || kind is not (PhysicalItemDispositionKind.Transfer
                or PhysicalItemDispositionKind.Sink)
            || !IsCanonicalRequired(operation)
            || !IsCanonicalRequired(reason))
        {
            failureReason = "physical-reserved-disposition-invalid-request";
            return false;
        }

        // A domain retry after item commit must not depend on the lease, which
        // was intentionally consumed by the first attempt.
        if (repository.TryGetPendingBatchDisposition(
                operation,
                out PhysicalItemBatchDispositionSaveData existing))
        {
            receipt = RestoreReceipt(existing);
            if (receipt.Kind != kind
                || receipt.Quantity != quantity
                || !string.Equals(
                    receipt.ReasonCode,
                    reason,
                    StringComparison.Ordinal))
            {
                receipt = default;
                failureReason =
                    "physical-reserved-disposition-operation-conflict:"
                    + operation;
                return false;
            }
            return receipt.IsCommitted;
        }

        if (!quantityReservations.Revalidate(
                leaseKey,
                out ItemQuantityLease lease,
                out DomainFailure leaseFailure)
            || lease.remainingQuantity < quantity
            || !string.Equals(
                lease.ownerOperationId,
                operation,
                StringComparison.Ordinal))
        {
            failureReason =
                "physical-reserved-disposition-lease-invalid:"
                + (leaseFailure.IsFailure
                    ? leaseFailure.Code.ToString()
                    : leaseKey);
            return false;
        }

        List<SourceMutation> mutations = new();
        int remaining = quantity;
        foreach (ItemLeaseSlice slice in lease.slices
                     .Where(value => value != null && value.quantity > 0)
                     .OrderBy(value => value.stackId, StringComparer.Ordinal))
        {
            if (remaining <= 0)
                break;
            if (!repository.RecordsById.TryGetValue(
                    slice.stackId,
                    out WorldItemStackRecord source)
                || source == null
                || source.state is WorldItemStackState.Carried
                    or WorldItemStackState.InTransit)
            {
                failureReason =
                    "physical-reserved-disposition-source-unavailable:"
                    + slice.stackId;
                return false;
            }
            int take = Math.Min(remaining, slice.quantity);
            mutations.Add(new SourceMutation(source, take));
            remaining -= take;
        }
        if (remaining > 0)
        {
            failureReason =
                "physical-reserved-disposition-quantity-unavailable";
            return false;
        }
        if (mutations.Any(mutation =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    mutation.Record.components)))
        {
            failureReason =
                "physical-reserved-disposition-prepared-output-route-protected";
            return false;
        }

        long inputMassGrams = 0L;
        foreach (SourceMutation mutation in mutations)
        {
            WorldItemStackRecord source = mutation.Record;
            PhysicalItemMassSubject subject =
                PhysicalItemMassSubjectAdapter.Create(
                    massQuery,
                    (ItemDefinitionId)source.itemId,
                    source.itemInstanceId,
                    source.components);
            inputMassGrams = checked(inputMassGrams
                + massQuery.GetQuantityMass(
                    (ItemDefinitionId)source.itemId,
                    subject,
                    mutation.Quantity).Value);
        }

        string requestFingerprint = CreateRequestFingerprint(
            kind,
            reason,
            mutations.Select(value => new PhysicalItemTransformInput(
                    value.Record.stackId,
                    value.Quantity))
                .ToArray());
        receipt = new PhysicalItemBatchDispositionReceipt(
            kind,
            operation,
            reason,
            requestFingerprint,
            mutations.Select(value => value.Record.stackId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            quantity,
            inputMassGrams);
        repository.AddPendingBatchDisposition(
            new PhysicalItemBatchDispositionSaveData
            {
                kind = (int)kind,
                operationId = operation,
                reasonCode = reason,
                requestFingerprint = requestFingerprint,
                sourceStackIds = receipt.SourceStackIds.ToList(),
                quantity = quantity,
                inputMassGrams = inputMassGrams,
                commitId = receipt.CommitId
            });

        ItemQuantityLease leaseSnapshot = lease.Clone();
        if (!quantityReservations.TryConsumeSlices(
                leaseKey,
                quantity,
                out _,
                out DomainFailure consumeFailure))
        {
            repository.AcknowledgePendingBatchDisposition(receipt.CommitId);
            receipt = default;
            failureReason =
                "physical-reserved-disposition-lease-commit-failed:"
                + consumeFailure.Code;
            return false;
        }

        try
        {
            foreach (SourceMutation mutation in mutations)
            {
                mutation.Record.quantity -= mutation.Quantity;
                if (mutation.Record.quantity <= 0)
                {
                    repository.Remove(mutation.Record);
                    mutation.Removed = true;
                }
                else
                {
                    repository.MarkChanged();
                }
            }
            foreach (UnityEngine.Vector2Int position in mutations
                         .Select(value => value.Record.position)
                         .Distinct())
            {
                markers.RefreshAt(position);
            }
            return true;
        }
        catch (Exception exception)
        {
            Rollback(mutations);
            quantityReservations
                .RestoreLeaseSnapshotForFailedPhysicalCommit(leaseSnapshot);
            if (!repository.AcknowledgePendingBatchDisposition(
                    receipt.CommitId))
            {
                throw new InvalidOperationException(
                    $"Reserved physical disposition '{operation}' failed and its pending receipt could not be rolled back.",
                    exception);
            }
            receipt = default;
            failureReason =
                "physical-reserved-disposition-rollback:"
                + exception.Message;
            return false;
        }
    }

    public bool TryCommit(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        if (!TryCommitPending(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason))
        {
            return false;
        }
        if (!Acknowledge(receipt.CommitId, out failureReason))
        {
            throw new InvalidOperationException(
                $"Physical disposition '{receipt.CommitId}' committed but could not be acknowledged: {failureReason}");
        }
        return true;
    }

    public bool TryCommitPending(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        return TryCommitPendingCore(
            inputs,
            kind,
            operationId,
            reasonCode,
            requiredSourceState: null,
            out receipt,
            out failureReason);
    }

    public bool TryCommitCarriedSinkPending(
        string stackId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => TryCommitPendingCore(
        new[] { new PhysicalItemTransformInput(stackId, quantity) },
        PhysicalItemDispositionKind.Sink,
        operationId,
        reasonCode,
        WorldItemStackState.Carried,
        out receipt,
        out failureReason);

    private bool TryCommitPendingCore(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        WorldItemStackState? requiredSourceState,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        string operation = operationId ?? string.Empty;
        string reason = reasonCode ?? string.Empty;
        PhysicalItemTransformInput[] requested = (inputs
                ?? Array.Empty<PhysicalItemTransformInput>())
            .OrderBy(input => input.StackId, StringComparer.Ordinal)
            .ToArray();
        if (kind is not (PhysicalItemDispositionKind.Transfer
                or PhysicalItemDispositionKind.Sink)
            || !IsCanonicalRequired(operation)
            || !IsCanonicalRequired(reason)
            || requested.Length == 0
            || requested.Any(input => !input.IsValid)
            || requested.Select(input => input.StackId)
                .Distinct(StringComparer.Ordinal).Count() != requested.Length)
        {
            failureReason = "physical-batch-disposition-invalid-request";
            return false;
        }


        string requestFingerprint = CreateRequestFingerprint(
            kind,
            reason,
            requested);
        if (repository.TryGetPendingBatchDisposition(
                operation,
                out PhysicalItemBatchDispositionSaveData pending))
        {
            if (!string.Equals(
                    pending.requestFingerprint,
                    requestFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = "physical-batch-disposition-operation-conflict:"
                    + operation;
                return false;
            }
            receipt = RestoreReceipt(pending);
            return receipt.IsCommitted;
        }

        List<SourceMutation> mutations = new(requested.Length);
        long inputMassGrams = 0L;
        int inputQuantity = 0;
        foreach (PhysicalItemTransformInput input in requested)
        {
            if (!repository.RecordsById.TryGetValue(
                    input.StackId,
                    out WorldItemStackRecord source)
                || source == null
                || source.quantity < input.Quantity
                || source.quantity - source.reservedQuantity < input.Quantity
                || source.reservedQuantity > 0
                || !string.IsNullOrEmpty(source.reservedByPersistentId)
                || (requiredSourceState.HasValue
                    ? source.state != requiredSourceState.Value
                    : source.state is WorldItemStackState.Carried
                        or WorldItemStackState.InTransit))
            {
                failureReason = "physical-batch-disposition-source-unavailable:"
                    + input.StackId;
                return false;
            }
            if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    source.components))
            {
                failureReason =
                    "physical-batch-disposition-prepared-output-route-protected:"
                    + input.StackId;
                return false;
            }
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                (ItemDefinitionId)source.itemId,
                source.itemInstanceId,
                source.components);
            inputMassGrams = checked(inputMassGrams + massQuery.GetQuantityMass(
                (ItemDefinitionId)source.itemId,
                subject,
                input.Quantity).Value);
            inputQuantity = checked(inputQuantity + input.Quantity);
            mutations.Add(new SourceMutation(source, input.Quantity));
        }

        receipt = new PhysicalItemBatchDispositionReceipt(
            kind,
            operation,
            reason,
            requestFingerprint,
            mutations.Select(mutation => mutation.Record.stackId).ToArray(),
            inputQuantity,
            inputMassGrams);
        repository.AddPendingBatchDisposition(new PhysicalItemBatchDispositionSaveData
        {
            kind = (int)kind,
            operationId = operation,
            reasonCode = reason,
            requestFingerprint = requestFingerprint,
            sourceStackIds = receipt.SourceStackIds.ToList(),
            quantity = receipt.Quantity,
            inputMassGrams = receipt.InputMassGrams,
            commitId = receipt.CommitId
        });

        try
        {
            foreach (SourceMutation mutation in mutations)
            {
                if (mutation.Record.quantity == mutation.Quantity)
                {
                    repository.Remove(mutation.Record);
                    mutation.Removed = true;
                }
                else
                {
                    mutation.Record.quantity = checked(
                        mutation.Record.quantity - mutation.Quantity);
                    repository.MarkChanged();
                }
            }
            foreach (UnityEngine.Vector2Int position in mutations
                         .Select(mutation => mutation.Record.position)
                         .Distinct())
            {
                markers.RefreshAt(position);
            }
        }
        catch (Exception exception)
        {
            Rollback(mutations);
            if (!repository.AcknowledgePendingBatchDisposition(receipt.CommitId))
            {
                throw new InvalidOperationException(
                    $"Physical disposition '{operation}' failed and its pending receipt could not be rolled back.",
                    exception);
            }
            receipt = default;
            failureReason = "physical-batch-disposition-rollback:"
                + exception.Message;
            return false;
        }
        return true;
    }

    public bool Acknowledge(string commitId, out string failureReason)
    {
        failureReason = string.Empty;
        string canonical = commitId ?? string.Empty;
        if (!IsCanonicalRequired(canonical))
        {
            failureReason = "physical-batch-disposition-ack-invalid";
            return false;
        }
        // Acknowledgement is deliberately idempotent. The durable consumer may
        // replay it after restore when the previous acknowledgement already
        // completed immediately before the save boundary.
        repository.AcknowledgePendingBatchDisposition(canonical);
        return true;
    }

    public bool TryGetPending(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt)
    {
        receipt = default;
        string operation = operationId ?? string.Empty;
        if (!IsCanonicalRequired(operation)
            || !repository.TryGetPendingBatchDisposition(
                operation,
                out PhysicalItemBatchDispositionSaveData pending))
        {
            return false;
        }

        receipt = RestoreReceipt(pending);
        return receipt.IsCommitted;
    }

    private static string CreateRequestFingerprint(
        PhysicalItemDispositionKind kind,
        string reason,
        IReadOnlyList<PhysicalItemTransformInput> inputs) =>
        $"{(int)kind}:{reason}:"
        + string.Join(",", inputs.Select(input =>
            $"{input.StackId}={input.Quantity}"));

    private static PhysicalItemBatchDispositionReceipt RestoreReceipt(
        PhysicalItemBatchDispositionSaveData pending) => new(
        (PhysicalItemDispositionKind)pending.kind,
        pending.operationId,
        pending.reasonCode,
        pending.requestFingerprint,
        pending.sourceStackIds,
        pending.quantity,
        pending.inputMassGrams);

    private void Rollback(IReadOnlyList<SourceMutation> mutations)
    {
        foreach (SourceMutation mutation in mutations)
        {
            if (mutation.Removed)
            {
                repository.Add(mutation.Record);
            }
            else
            {
                mutation.Record.quantity = mutation.OriginalQuantity;
            }
        }
        repository.MarkChanged();
    }

    private static bool IsCanonicalRequired(string value) =>
        value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private sealed class SourceMutation
    {
        internal SourceMutation(WorldItemStackRecord record, int quantity)
        {
            Record = record;
            Quantity = quantity;
            OriginalQuantity = record.quantity;
        }

        internal WorldItemStackRecord Record { get; }
        internal int Quantity { get; }
        internal int OriginalQuantity { get; }
        internal bool Removed { get; set; }
    }
}
