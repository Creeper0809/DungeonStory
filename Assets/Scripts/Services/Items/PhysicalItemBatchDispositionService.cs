using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
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
        : this(
            kind,
            operationId,
            reasonCode,
            requestFingerprint,
            sourceStackIds,
            quantity,
            inputMassGrams,
            0L,
            Array.Empty<PhysicalItemDispositionSourceFact>())
    {
    }

    internal PhysicalItemBatchDispositionReceipt(
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        string requestFingerprint,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams,
        long ownerRevision,
        IReadOnlyList<PhysicalItemDispositionSourceFact> sourceFacts)
    {
        Kind = kind;
        OperationId = operationId;
        ReasonCode = reasonCode;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        SourceStackIds = (sourceStackIds ?? Array.Empty<string>()).ToArray();
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
        OwnerRevision = ownerRevision;
        SourceFacts = (sourceFacts
            ?? Array.Empty<PhysicalItemDispositionSourceFact>()).ToArray();
        CommitId = $"physical-batch-disposition:{(int)kind}:{operationId}:{quantity}:{inputMassGrams}";
    }

    public PhysicalItemDispositionKind Kind { get; }
    public string OperationId { get; }
    public string ReasonCode { get; }
    public string RequestFingerprint { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
    public long OwnerRevision { get; }
    public IReadOnlyList<PhysicalItemDispositionSourceFact> SourceFacts { get; }
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

public interface IOutcomeAwareReservedPhysicalItemBatchDispositionService
{
    bool TryCommitReservedSinkPending(
        string leaseId,
        int quantity,
        string operationId,
        string reasonCode,
        IPhysicalItemBatchDispositionOutcomeParticipant outcomeParticipant,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);
}

/// <summary>
/// Short-lived in-process rollback handle for a carried terminal Sink. The
/// handle is deliberately not a second inventory authority: it only retains
/// the exact repository mutation until the owning domain publishes or rejects
/// its state transition in the same synchronous call.
/// </summary>
public interface IReversiblePhysicalItemDisposition
{
    PhysicalItemBatchDispositionReceipt Receipt { get; }
    bool TryRollback(out string failureReason);
    bool TryAcknowledge(out string failureReason);
}

public interface IReversibleCarriedPhysicalItemBatchDispositionService
{
    bool TryCommitCarriedSinkReversible(
        string stackId,
        int quantity,
        string operationId,
        string reasonCode,
        out IReversiblePhysicalItemDisposition transaction,
        out string failureReason);
}

/// <summary>
/// Synchronous all-or-nothing custody transfer or terminal Sink across several
/// world stacks. Mass-changing work is deliberately excluded and must use the
/// transform/WIP boundary.
/// </summary>
public sealed class PhysicalItemBatchDispositionService :
    IPhysicalItemBatchDispositionService,
    IOutcomeAwarePhysicalItemBatchDispositionService,
    IOutcomeAwarePhysicalItemDispositionAcknowledgementService,
    IReservedPhysicalItemBatchDispositionService,
    IOutcomeAwareReservedPhysicalItemBatchDispositionService,
    ICarriedPhysicalItemBatchDispositionService,
    IReversibleCarriedPhysicalItemBatchDispositionService
{
    private readonly WorldItemRepository repository;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IItemMarkerPresenter markers;
    private readonly ItemQuantityReservationService quantityReservations;
    private readonly IDungeonItemCatalogProvider itemCatalog;
    private readonly IDefaultPhysicalItemBatchDispositionOutcomeParticipant
        defaultOutcomeParticipant;
    private readonly IGameplayOutcomeDiagnosticsQuery outcomeDiagnostics;
    private readonly IGameplayOutcomeRecorder outcomeRecorder;

    public PhysicalItemBatchDispositionService(
        WorldItemRepository repository,
        IPhysicalItemMassQuery massQuery,
        IItemMarkerPresenter markers)
        : this(repository, massQuery, markers, null, null, null, null, null)
    {
    }

    public PhysicalItemBatchDispositionService(
        WorldItemRepository repository,
        IPhysicalItemMassQuery massQuery,
        IItemMarkerPresenter markers,
        ItemQuantityReservationService quantityReservations)
        : this(
            repository,
            massQuery,
            markers,
            quantityReservations,
            null,
            null,
            null,
            null)
    {
    }

    [Inject]
    public PhysicalItemBatchDispositionService(
        WorldItemRepository repository,
        IPhysicalItemMassQuery massQuery,
        IItemMarkerPresenter markers,
        ItemQuantityReservationService quantityReservations,
        IDungeonItemCatalogProvider itemCatalog,
        IDefaultPhysicalItemBatchDispositionOutcomeParticipant
            defaultOutcomeParticipant,
        IGameplayOutcomeDiagnosticsQuery outcomeDiagnostics,
        IGameplayOutcomeRecorder outcomeRecorder)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
        this.quantityReservations = quantityReservations;
        this.itemCatalog = itemCatalog;
        this.defaultOutcomeParticipant = defaultOutcomeParticipant;
        this.outcomeDiagnostics = outcomeDiagnostics;
        this.outcomeRecorder = outcomeRecorder;
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
        defaultOutcomeParticipant,
        out receipt,
        out failureReason);

    [GameplayInternalOnly(
        "A domain-owned exact lease may terminally consume its reserved physical quantity with a domain-specific outcome participant.",
        "Registered reserved-Sink owners that require an exact typed gameplay outcome only")]
    public bool TryCommitReservedSinkPending(
        string leaseId,
        int quantity,
        string operationId,
        string reasonCode,
        IPhysicalItemBatchDispositionOutcomeParticipant outcomeParticipant,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => TryCommitReservedPending(
        leaseId,
        quantity,
        PhysicalItemDispositionKind.Sink,
        operationId,
        reasonCode,
        outcomeParticipant,
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
        defaultOutcomeParticipant,
        out receipt,
        out failureReason);

    private bool TryCommitReservedPending(
        string leaseId,
        int quantity,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        IPhysicalItemBatchDispositionOutcomeParticipant outcomeParticipant,
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
            if (!TryPrepareCanonicalReplay(
                    receipt,
                    existing,
                    outcomeParticipant,
                    out failureReason))
            {
                receipt = default;
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
        List<PhysicalItemDispositionSourceFact> sourceFacts =
            new(mutations.Count);
        foreach (SourceMutation mutation in mutations)
        {
            WorldItemStackRecord source = mutation.Record;
            PhysicalItemMassSubject subject =
                PhysicalItemMassSubjectAdapter.Create(
                    massQuery,
                    (ItemDefinitionId)source.itemId,
                    source.itemInstanceId,
                    source.components);
            long sourceMassGrams = massQuery.GetQuantityMass(
                (ItemDefinitionId)source.itemId,
                subject,
                mutation.Quantity).Value;
            inputMassGrams = checked(inputMassGrams + sourceMassGrams);
            if (!TryCreateSourceFact(
                    source,
                    mutation.Quantity,
                    sourceMassGrams,
                    outcomeParticipant != null,
                    out PhysicalItemDispositionSourceFact sourceFact,
                    out failureReason))
                return false;
            if (sourceFact.IsValid)
                sourceFacts.Add(sourceFact);
        }

        string requestFingerprint = CreateRequestFingerprint(
            kind,
            reason,
            mutations.Select(value => new PhysicalItemTransformInput(
                    value.Record.stackId,
                    value.Quantity))
                .ToArray());
        long ownerRevision = checked(
            (long)repository.ItemStackVersion + 2L + mutations.Count);
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
            inputMassGrams,
            ownerRevision,
            sourceFacts);
        IPreparedPhysicalItemGameplayOutcome preparedOutcome = null;
        if (outcomeParticipant != null
            && !outcomeParticipant.TryPrepare(
                receipt,
                ownerRevision,
                out preparedOutcome,
                out failureReason))
        {
            receipt = default;
            return false;
        }
        GameplayResultKey expectedOutcomeKey = preparedOutcome?.ResultKey
            ?? default;
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
                commitId = receipt.CommitId,
                outcomeOwnerRevision = preparedOutcome != null
                    ? ownerRevision
                    : 0L,
                gameplayOutcomeExpected = preparedOutcome != null,
                expectedOutcomeProducerId = expectedOutcomeKey.ProducerId,
                expectedOutcomeOperationId =
                    expectedOutcomeKey.OperationId.Value,
                expectedOutcomeCommitRevision =
                    expectedOutcomeKey.CommitRevision,
                expectedOutcomeLocalResultIndex =
                    expectedOutcomeKey.LocalResultIndex,
                sourceFacts = receipt.SourceFacts
                    .Select((PhysicalItemDispositionSourceFact value) =>
                        PhysicalGameplayOutcomeSaveCodec.ToSave(value))
                    .ToList()
            });

        ItemQuantityLease leaseSnapshot = lease.Clone();
        if (!quantityReservations.TryConsumeSlices(
                leaseKey,
                quantity,
                out _,
                out DomainFailure consumeFailure))
        {
            preparedOutcome?.Cancel();
            repository.AcknowledgePendingBatchDisposition(receipt.CommitId);
            receipt = default;
            failureReason =
                "physical-reserved-disposition-lease-commit-failed:"
                + consumeFailure.Code;
            return false;
        }

        bool gameplayOutcomeCommitted = false;
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
            if (repository.ItemStackVersion != ownerRevision)
            {
                throw new InvalidOperationException(
                    "physical-reserved-owner-revision-mismatch");
            }
            if (preparedOutcome != null)
            {
                bool outcomeCallSucceeded = preparedOutcome.TryCommit(
                    ownerRevision,
                    out PhysicalGameplayOutcomeAttachment attachment,
                    out bool canonicalCommitted,
                    out failureReason);
                gameplayOutcomeCommitted = canonicalCommitted;
                if (!outcomeCallSucceeded && !canonicalCommitted)
                {
                    throw new InvalidOperationException(
                        failureReason.Length > 0
                            ? failureReason
                            : "physical-reserved-outcome-commit-failed");
                }
                if (attachment.IsValid)
                {
                    if (!repository.TryAttachPendingBatchDispositionOutcome(
                            receipt.CommitId,
                            PhysicalGameplayOutcomeSaveCodec.ToSave(attachment)))
                    {
                        failureReason =
                            "physical-reserved-outcome-attachment-pending";
                    }
                }
                else if (canonicalCommitted)
                {
                    // The canonical ledger row is already durable. Keep both
                    // the physical mutation and its pending join row so the
                    // exact expected result key can be reconciled later.
                    failureReason = failureReason.Length > 0
                        ? failureReason
                        : "physical-reserved-outcome-identity-pending";
                }
            }
        }
        catch (Exception exception)
        {
            gameplayOutcomeCommitted |= HasCanonicalCommittedOutcome(
                preparedOutcome);
            if (gameplayOutcomeCommitted)
            {
                failureReason =
                    "physical-reserved-outcome-reconciliation-pending:"
                    + exception.Message;
                return true;
            }
            preparedOutcome?.Cancel();
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
        try
        {
            foreach (UnityEngine.Vector2Int position in mutations
                         .Select(value => value.Record.position)
                         .Distinct())
                markers.RefreshAt(position);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError(
                "Reserved physical disposition marker refresh failed after its authoritative transaction committed: "
                + exception);
        }
        return true;
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
            // TryCommitPending crossing its commit point is authoritative.
            // Any later delivery, identity attachment, or acknowledgement
            // failure must retain the joint pending row and report durable
            // success; returning false would invite an owner to replay domain
            // effects that have already committed.
            return true;
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
            defaultOutcomeParticipant,
            out receipt,
            out failureReason);
    }

    public bool TryCommitPending(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        IPhysicalItemBatchDispositionOutcomeParticipant outcomeParticipant,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => TryCommitPendingCore(
        inputs,
        kind,
        operationId,
        reasonCode,
        requiredSourceState: null,
        outcomeParticipant,
        out receipt,
        out failureReason);

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
        defaultOutcomeParticipant,
        out receipt,
        out failureReason);

    [GameplayInternalOnly(
        "A cross-aggregate owner may retain one exact carried Sink mutation until its synchronous domain commit finishes.",
        "MemoryErasureSealTransactionService")]
    public bool TryCommitCarriedSinkReversible(
        string stackId,
        int quantity,
        string operationId,
        string reasonCode,
        out IReversiblePhysicalItemDisposition transaction,
        out string failureReason)
    {
        transaction = null;
        if (!TryCommitPendingCore(
                new[] { new PhysicalItemTransformInput(stackId, quantity) },
                PhysicalItemDispositionKind.Sink,
                operationId,
                reasonCode,
                WorldItemStackState.Carried,
                // This receipt is an intermediate half of a larger owner
                // transaction. The owner publishes its terminal outcome only
                // after it accepts the rollback handle; committing the default
                // item outcome here would make TryRollback split domain and
                // ledger state.
                null,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason,
                out List<SourceMutation> mutations,
                out bool replayed))
        {
            return false;
        }
        if (replayed)
        {
            failureReason =
                "physical-reversible-disposition-pending-replay-requires-owner-recovery:"
                + operationId;
            return false;
        }

        transaction = new ReversibleDisposition(this, receipt, mutations);
        return true;
    }

    private bool TryCommitPendingCore(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        WorldItemStackState? requiredSourceState,
        IPhysicalItemBatchDispositionOutcomeParticipant outcomeParticipant,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        return TryCommitPendingCore(
            inputs,
            kind,
            operationId,
            reasonCode,
            requiredSourceState,
            outcomeParticipant,
            out receipt,
            out failureReason,
            out _,
            out _);
    }

    private bool TryCommitPendingCore(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        WorldItemStackState? requiredSourceState,
        IPhysicalItemBatchDispositionOutcomeParticipant outcomeParticipant,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason,
        out List<SourceMutation> committedMutations,
        out bool replayed)
    {
        receipt = default;
        failureReason = string.Empty;
        committedMutations = null;
        replayed = false;
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
            if (!TryPrepareCanonicalReplay(
                    receipt,
                    pending,
                    outcomeParticipant,
                    out failureReason))
            {
                receipt = default;
                return false;
            }
            replayed = true;
            return receipt.IsCommitted;
        }

        List<SourceMutation> mutations = new(requested.Length);
        List<PhysicalItemDispositionSourceFact> sourceFacts =
            new(requested.Length);
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
            long sourceMassGrams = massQuery.GetQuantityMass(
                (ItemDefinitionId)source.itemId,
                subject,
                input.Quantity).Value;
            inputMassGrams = checked(inputMassGrams + sourceMassGrams);
            inputQuantity = checked(inputQuantity + input.Quantity);
            mutations.Add(new SourceMutation(source, input.Quantity));
            if (!TryCreateSourceFact(
                    source,
                    input.Quantity,
                    sourceMassGrams,
                    outcomeParticipant != null,
                    out PhysicalItemDispositionSourceFact sourceFact,
                    out failureReason))
            {
                return false;
            }
            if (sourceFact.IsValid)
                sourceFacts.Add(sourceFact);
        }

        long ownerRevision = checked(
            (long)repository.ItemStackVersion + 1L + mutations.Count);
        receipt = new PhysicalItemBatchDispositionReceipt(
            kind,
            operation,
            reason,
            requestFingerprint,
            mutations.Select(mutation => mutation.Record.stackId).ToArray(),
            inputQuantity,
            inputMassGrams,
            ownerRevision,
            sourceFacts);
        IPreparedPhysicalItemGameplayOutcome preparedOutcome = null;
        if (outcomeParticipant != null
            && !outcomeParticipant.TryPrepare(
                receipt,
                ownerRevision,
                out preparedOutcome,
                out failureReason))
        {
            receipt = default;
            return false;
        }
        GameplayResultKey expectedOutcomeKey = preparedOutcome?.ResultKey
            ?? default;
        repository.AddPendingBatchDisposition(new PhysicalItemBatchDispositionSaveData
        {
            kind = (int)kind,
            operationId = operation,
            reasonCode = reason,
            requestFingerprint = requestFingerprint,
            sourceStackIds = receipt.SourceStackIds.ToList(),
            quantity = receipt.Quantity,
            inputMassGrams = receipt.InputMassGrams,
            commitId = receipt.CommitId,
            outcomeOwnerRevision = preparedOutcome != null
                ? ownerRevision
                : 0L,
            gameplayOutcomeExpected = preparedOutcome != null,
            expectedOutcomeProducerId = expectedOutcomeKey.ProducerId,
            expectedOutcomeOperationId = expectedOutcomeKey.OperationId.Value,
            expectedOutcomeCommitRevision = expectedOutcomeKey.CommitRevision,
            expectedOutcomeLocalResultIndex = expectedOutcomeKey.LocalResultIndex,
            sourceFacts = receipt.SourceFacts
                .Select((PhysicalItemDispositionSourceFact value) =>
                    PhysicalGameplayOutcomeSaveCodec.ToSave(value))
                .ToList()
        });

        bool gameplayOutcomeCommitted = false;
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
            if (repository.ItemStackVersion != ownerRevision)
            {
                throw new InvalidOperationException(
                    "physical-batch-owner-revision-mismatch");
            }
            if (preparedOutcome != null)
            {
                bool outcomeCallSucceeded = preparedOutcome.TryCommit(
                    ownerRevision,
                    out PhysicalGameplayOutcomeAttachment attachment,
                    out bool canonicalCommitted,
                    out failureReason);
                gameplayOutcomeCommitted = canonicalCommitted;
                if (!outcomeCallSucceeded && !canonicalCommitted)
                {
                    throw new InvalidOperationException(
                        failureReason.Length > 0
                            ? failureReason
                            : "physical-gameplay-outcome-commit-failed");
                }
                if (attachment.IsValid)
                {
                    if (!repository.TryAttachPendingBatchDispositionOutcome(
                            receipt.CommitId,
                            PhysicalGameplayOutcomeSaveCodec.ToSave(attachment)))
                    {
                        failureReason =
                            "physical-gameplay-outcome-attachment-pending";
                    }
                }
                else if (canonicalCommitted)
                {
                    // Never roll the physical domain back after the ledger
                    // commit point. The pending row contains the exact result
                    // key and remains the durable reconciliation marker.
                    failureReason = failureReason.Length > 0
                        ? failureReason
                        : "physical-gameplay-outcome-identity-pending";
                }
            }
        }
        catch (Exception exception)
        {
            gameplayOutcomeCommitted |= HasCanonicalCommittedOutcome(
                preparedOutcome);
            if (gameplayOutcomeCommitted)
            {
                committedMutations = mutations;
                failureReason =
                    "physical-gameplay-outcome-reconciliation-pending:"
                    + exception.Message;
                return true;
            }
            preparedOutcome?.Cancel();
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
        try
        {
            foreach (UnityEngine.Vector2Int position in mutations
                         .Select(mutation => mutation.Record.position)
                         .Distinct())
                markers.RefreshAt(position);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError(
                "Physical disposition marker refresh failed after its authoritative transaction committed: "
                + exception);
        }
        committedMutations = mutations;
        return true;
    }

    private bool HasCanonicalCommittedOutcome(
        IPreparedPhysicalItemGameplayOutcome prepared)
    {
        if (prepared == null || outcomeDiagnostics == null)
            return false;
        try
        {
            return outcomeDiagnostics.TryGetResultIdentity(
                    prepared.ResultKey,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.ResultKey.Equals(prepared.ResultKey)
                && identity.State is >= GameplayOutcomeReplayState.Committed
                    and <= GameplayOutcomeReplayState.Forgotten
                && identity.HasCanonicalPayloadHash;
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            UnityEngine.Debug.LogError(
                "Physical outcome commit-phase reconciliation failed: "
                + exception);
            return false;
        }
    }

    public bool Acknowledge(string commitId, out string failureReason)
        => AcknowledgeCore(commitId, null, out failureReason);

    public bool Acknowledge(string commitId, IPhysicalItemDispositionAcknowledgementParticipant participant,
        out string failureReason)
    {
        if (participant == null)
        { failureReason = "physical-joint-ack-participant-missing"; return false; }
        return AcknowledgeCore(commitId, participant, out failureReason);
    }

    private bool AcknowledgeCore(string commitId, IPhysicalItemDispositionAcknowledgementParticipant participant,
        out string failureReason)
    {
        failureReason = string.Empty;
        string canonical = commitId ?? string.Empty;
        if (!IsCanonicalRequired(canonical))
        {
            failureReason = "physical-batch-disposition-ack-invalid";
            return false;
        }
        bool hasPending = repository.TryGetPendingBatchDispositionByCommitId(canonical, out var pending);
        if (participant != null && !hasPending)
        { failureReason = "physical-joint-ack-exact-receipt-missing"; return false; }
        if (hasPending && (pending.gameplayOutcomeExpected
                || pending.gameplayOutcome != null))
        {
            if (outcomeDiagnostics == null)
            {
                failureReason =
                    "physical-batch-disposition-outcome-diagnostics-unavailable";
                return false;
            }
            outcomeRecorder?.RetryPendingDeliveries(1);
            if (pending.gameplayOutcome == null
                && !TryReconcilePendingOutcomeAttachment(
                    pending,
                    out failureReason))
            {
                return false;
            }
            PhysicalGameplayOutcomeAttachment attachment;
            try
            {
                attachment = PhysicalGameplayOutcomeSaveCodec.FromSave(
                    pending.gameplayOutcome);
            }
            catch (Exception)
            {
                failureReason =
                    "physical-batch-disposition-outcome-attachment-invalid";
                return false;
            }
            if (!attachment.IsValid
                || !outcomeDiagnostics.TryGetResultIdentity(
                    attachment.ResultKey,
                    out GameplayOutcomeReplayIdentity identity)
                || !identity.ResultKey.Equals(attachment.ResultKey)
                || !identity.OutcomeId.Equals(attachment.OutcomeId)
                || identity.State is < GameplayOutcomeReplayState
                        .PublishedAcknowledged
                    or > GameplayOutcomeReplayState.Forgotten
                || !string.Equals(
                    identity.CanonicalPayloadHash,
                    attachment.CanonicalPayloadHash,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "physical-batch-disposition-outcome-not-acknowledged";
                return false;
            }
        }
        // Acknowledgement is deliberately idempotent. The durable consumer may
        // replay it after restore when the previous acknowledgement already
        // completed immediately before the save boundary.
        if (participant == null)
        {
            repository.AcknowledgePendingBatchDisposition(canonical);
            return true;
        }
        try
        {
            if (!participant.TryCommit(out failureReason))
            {
                participant.Rollback();
                return false;
            }
            if (!repository.AcknowledgePendingBatchDisposition(canonical))
                throw new InvalidOperationException("physical-joint-ack-receipt-changed");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        {
            // Both publications are synchronous and contain no observers.
            // Restore the exact pending row as well as the domain cleanup.
            if (!repository.TryGetPendingBatchDispositionByCommitId(canonical, out _))
                repository.AddPendingBatchDisposition(pending);
            participant.Rollback();
            failureReason = "physical-joint-ack-exception:" + exception.GetType().Name;
            return false;
        }
        return true;
    }

    private bool TryReconcilePendingOutcomeAttachment(
        PhysicalItemBatchDispositionSaveData pending,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (pending == null
            || !pending.gameplayOutcomeExpected
            || !GameplayOutcomeStableIdSyntax.IsValid(
                pending.expectedOutcomeProducerId)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                pending.expectedOutcomeOperationId)
            || pending.expectedOutcomeCommitRevision < 0L
            || pending.expectedOutcomeLocalResultIndex < 0)
        {
            failureReason =
                "physical-batch-disposition-expected-outcome-key-invalid";
            return false;
        }
        GameplayResultKey expected = new(
            pending.expectedOutcomeProducerId,
            new GameplayOperationId(pending.expectedOutcomeOperationId),
            pending.expectedOutcomeCommitRevision,
            pending.expectedOutcomeLocalResultIndex);
        if (!outcomeDiagnostics.TryGetResultIdentity(
                expected,
                out GameplayOutcomeReplayIdentity identity)
            || !identity.ResultKey.Equals(expected)
            || identity.State is < GameplayOutcomeReplayState.Committed
                or > GameplayOutcomeReplayState.Forgotten)
        {
            failureReason =
                "physical-batch-disposition-outcome-identity-pending";
            return false;
        }
        PhysicalGameplayOutcomeAttachment attachment = new(
            identity.ResultKey,
            identity.OutcomeId,
            identity.State,
            identity.CanonicalPayloadHash);
        if (!attachment.IsValid
            || !repository.TryAttachPendingBatchDispositionOutcome(
                pending.commitId,
                PhysicalGameplayOutcomeSaveCodec.ToSave(attachment)))
        {
            failureReason =
                "physical-batch-disposition-outcome-attachment-pending";
            return false;
        }
        pending.gameplayOutcome = PhysicalGameplayOutcomeSaveCodec.ToSave(
            attachment);
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
        pending.inputMassGrams,
        pending.outcomeOwnerRevision,
        (pending.sourceFacts
                ?? new List<PhysicalItemDispositionSourceFactSaveData>())
            .Select(PhysicalGameplayOutcomeSaveCodec.FromSave)
            .ToArray());

    private bool TryCreateSourceFact(
        WorldItemStackRecord source,
        int quantity,
        long massGrams,
        bool required,
        out PhysicalItemDispositionSourceFact fact,
        out string failureReason)
    {
        fact = default;
        failureReason = string.Empty;
        if (!required && itemCatalog == null)
            return true;
        if (source == null
            || itemCatalog == null
            || !itemCatalog.TryGetDefinition(
                source.itemId,
                out DungeonItemDefinition definition)
            || definition == null)
        {
            failureReason =
                "physical-disposition-item-definition-snapshot-unavailable:"
                + (source?.itemId ?? string.Empty);
            return false;
        }
        fact = new PhysicalItemDispositionSourceFact(
            source.stackId,
            source.itemId,
            source.itemInstanceId,
            quantity,
            massGrams,
            source.position,
            new KoreanNameSnapshot(
                definition.DisplayName,
                "item-definition:" + source.itemId + ":v1",
                KoreanPronunciationHint.AutoHangulDisplay(
                    "item-definition-pronunciation-v1"),
                "ko-KR"));
        if (fact.IsValid)
            return true;
        failureReason = "physical-disposition-source-fact-invalid:"
            + source.stackId;
        return false;
    }

    private static bool TryPrepareCanonicalReplay(
        in PhysicalItemBatchDispositionReceipt receipt,
        PhysicalItemBatchDispositionSaveData pending,
        IPhysicalItemBatchDispositionOutcomeParticipant participant,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (participant == null)
            return true;
        if (pending == null || !pending.gameplayOutcomeExpected)
        {
            failureReason =
                "physical-disposition-replay-outcome-expectation-missing";
            return false;
        }
        GameplayResultKey expected;
        try
        {
            expected = new GameplayResultKey(
                pending.expectedOutcomeProducerId,
                new GameplayOperationId(pending.expectedOutcomeOperationId),
                pending.expectedOutcomeCommitRevision,
                pending.expectedOutcomeLocalResultIndex);
        }
        catch (Exception)
        {
            failureReason =
                "physical-disposition-replay-expected-key-invalid";
            return false;
        }
        if (!participant.TryPrepare(
                receipt,
                pending.outcomeOwnerRevision,
                out IPreparedPhysicalItemGameplayOutcome prepared,
                out failureReason))
            return false;
        bool exact = prepared != null
            && prepared.IsCanonicalReplay
            && prepared.ResultKey.Equals(expected);
        if (exact && pending.gameplayOutcome != null)
        {
            try
            {
                PhysicalGameplayOutcomeAttachment attachment =
                    PhysicalGameplayOutcomeSaveCodec.FromSave(
                        pending.gameplayOutcome);
                exact = attachment.IsValid
                    && attachment.ResultKey.Equals(expected);
            }
            catch (Exception)
            {
                exact = false;
            }
        }
        prepared?.Cancel();
        if (exact)
            return true;
        failureReason = "physical-disposition-replay-payload-conflict";
        return false;
    }

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

    private sealed class ReversibleDisposition :
        IReversiblePhysicalItemDisposition
    {
        private readonly PhysicalItemBatchDispositionService owner;
        private readonly IReadOnlyList<SourceMutation> mutations;
        private bool terminal;

        internal ReversibleDisposition(
            PhysicalItemBatchDispositionService owner,
            PhysicalItemBatchDispositionReceipt receipt,
            IReadOnlyList<SourceMutation> mutations)
        {
            this.owner = owner
                ?? throw new ArgumentNullException(nameof(owner));
            Receipt = receipt;
            this.mutations = mutations
                ?? throw new ArgumentNullException(nameof(mutations));
        }

        public PhysicalItemBatchDispositionReceipt Receipt { get; }

        [GameplayInternalOnly(
            "Rejecting the paired domain state restores the exact carried world-stack mutation.",
            "MemoryErasureSealTransactionService")]
        public bool TryRollback(out string failureReason)
        {
            failureReason = string.Empty;
            if (terminal)
            {
                failureReason = "physical-reversible-disposition-already-terminal";
                return false;
            }

            bool receiptRemoved;
            try
            {
                owner.Rollback(mutations);
                receiptRemoved = owner.repository.AcknowledgePendingBatchDisposition(
                    Receipt.CommitId);
                terminal = true;
            }
            catch (Exception exception)
            {
                failureReason =
                    "physical-reversible-disposition-rollback-failed:"
                    + exception.Message;
                return false;
            }

            try
            {
                foreach (UnityEngine.Vector2Int position in mutations
                             .Select(value => value.Record.position)
                             .Distinct())
                {
                    owner.markers.RefreshAt(position);
                }
            }
            catch (Exception exception)
            {
                // Marker presentation is not item authority. Once the exact
                // repository mutation and pending receipt are restored, a UI
                // refresh exception must not suppress the carry rollback.
                UnityEngine.Debug.LogError(
                    "Physical reversible disposition marker refresh failed after exact rollback: "
                    + exception);
            }
            if (!receiptRemoved)
            {
                UnityEngine.Debug.LogWarning(
                    "Physical reversible disposition restored its exact source while the pending receipt was already absent: "
                    + Receipt.CommitId);
            }
            return true;
        }

        [GameplayInternalOnly(
            "The paired domain state committed, so the durable pending Sink receipt may retire.",
            "MemoryErasureSealTransactionService")]
        public bool TryAcknowledge(out string failureReason)
        {
            if (terminal)
            {
                failureReason =
                    "physical-reversible-disposition-already-terminal";
                return false;
            }
            bool acknowledged = owner.Acknowledge(
                Receipt.CommitId,
                out failureReason);
            if (acknowledged)
            {
                terminal = true;
            }
            return acknowledged;
        }
    }
}
