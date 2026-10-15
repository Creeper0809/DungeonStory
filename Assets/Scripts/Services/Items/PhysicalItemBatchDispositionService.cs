using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using VContainer;
using VContainer.Unity;

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
        SourceStackIds = Array.AsReadOnly((sourceStackIds ?? Array.Empty<string>()).ToArray());
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
        OwnerRevision = ownerRevision;
        SourceFacts = Array.AsReadOnly((sourceFacts
            ?? Array.Empty<PhysicalItemDispositionSourceFact>()).ToArray());
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
    IJointPhysicalItemBatchDispositionService,
    IOutcomeAwarePhysicalItemDispositionAcknowledgementService,
    IReservedPhysicalItemBatchDispositionService,
    IJointReservedPhysicalItemBatchDispositionService,
    ICarriedPhysicalItemBatchDispositionService,
    IReversibleCarriedPhysicalItemBatchDispositionService, ITickable, IDungeonSaveRestoreCompletedHook
{
    private readonly WorldItemRepository repository;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IItemMarkerPresenter markers;
    private readonly ItemQuantityReservationService quantityReservations;
    private readonly IDungeonItemCatalogProvider itemCatalog;
    private readonly IPhysicalItemDispositionEffect defaultEffect;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IDomainFactDeliveryQuery deliveries;
    private readonly Queue<string> pendingPublications = new();
    private readonly HashSet<string> queuedPublications = new(StringComparer.Ordinal);
    private float nextRecoveryTime;
    public string LastRecoveryFailure { get; private set; } = string.Empty;

    public void OnRestoreCompleted()
    {
        pendingPublications.Clear(); queuedPublications.Clear();
        nextRecoveryTime = 0f; LastRecoveryFailure = string.Empty;
        foreach (var operation in repository.PendingBatchDispositionPublicationIds()) QueueRecovery(operation);
    }

    public void Tick()
    {
        if (pendingPublications.Count == 0 || UnityEngine.Time.unscaledTime < nextRecoveryTime) return;
        nextRecoveryTime = UnityEngine.Time.unscaledTime + 1f;
        int count = Math.Min(8, pendingPublications.Count);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            string operation = pendingPublications.Dequeue(); queuedPublications.Remove(operation);
            if (!repository.TryGetPendingBatchDisposition(operation, out var pending)
                || !pending.publicationExpected || pending.publicationDelivered) continue;
            string detail;
            try { TryReconcilePublication(pending, out detail); }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            { detail = "physical-publication-recovery-failed:" + exception.GetType().Name; }
            if (repository.TryGetPendingBatchDisposition(operation, out pending) && !pending.publicationDelivered)
            { QueueRecovery(operation); failure = detail; }
        }
        if (!string.IsNullOrEmpty(failure) && failure != LastRecoveryFailure)
            UnityEngine.Debug.LogWarning("Physical disposition publication recovery pending: " + failure);
        LastRecoveryFailure = failure;
        // Only the consuming owner acknowledges/removes the pending debit journal.
    }

    private void QueueRecovery(string operation)
    { if (queuedPublications.Add(operation)) pendingPublications.Enqueue(operation); }
#if UNITY_EDITOR
    internal Func<string, bool> DebugRejectAcknowledgement { get; set; }
#endif

    public PhysicalItemBatchDispositionService(WorldItemRepository repository, IPhysicalItemMassQuery massQuery,
        IItemMarkerPresenter markers) : this(repository, massQuery, markers, null, null, null, null, null, null) { }

    public PhysicalItemBatchDispositionService(WorldItemRepository repository, IPhysicalItemMassQuery massQuery,
        IItemMarkerPresenter markers, ItemQuantityReservationService quantityReservations)
        : this(repository, massQuery, markers, quantityReservations, null, null, null, null, null) { }

    [Inject]
    public PhysicalItemBatchDispositionService(WorldItemRepository repository, IPhysicalItemMassQuery massQuery,
        IItemMarkerPresenter markers, ItemQuantityReservationService quantityReservations,
        IDungeonItemCatalogProvider itemCatalog, IDomainFactStagingPublisher facts,
        IDomainFactRecoveryPublisher recovery, IDomainFactDeliveryQuery deliveries, IGameCalendar calendar)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
        this.quantityReservations = quantityReservations;
        this.itemCatalog = itemCatalog;
        this.recovery = recovery;
        this.deliveries = deliveries;
        if (facts != null) defaultEffect = new DefaultEffect(facts, calendar ?? throw new ArgumentNullException(nameof(calendar)));
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
        defaultEffect,
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
        IPhysicalItemDispositionEffect effect,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => TryCommitReservedPending(
        leaseId,
        quantity,
        PhysicalItemDispositionKind.Sink,
        operationId,
        reasonCode,
        effect,
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
        defaultEffect,
        out receipt,
        out failureReason);

    private bool TryCommitReservedPending(
        string leaseId,
        int quantity,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        IPhysicalItemDispositionEffect effect,
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
                    effect,
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
                    effect != null,
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
        if (!TryStageEffect(effect, receipt, ownerRevision, out IDomainFactStage stage, out failureReason))
        { receipt = default; return false; }
        try { repository.AddPendingBatchDisposition(CreatePending(receipt, stage)); }
        catch { stage?.Cancel(); throw; }
        if (stage != null) QueueRecovery(operation);

        ItemQuantityLease leaseSnapshot = lease.Clone();
        if (!quantityReservations.TryConsumeSlices(
                leaseKey,
                quantity,
                out _,
                out DomainFailure consumeFailure))
        {
            stage?.Cancel();
            repository.AcknowledgePendingBatchDisposition(receipt.CommitId);
            receipt = default;
            failureReason =
                "physical-reserved-disposition-lease-commit-failed:"
                + consumeFailure.Code;
            return false;
        }

        bool publicationMayBeCommitted = false;
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
            if (stage != null)
            {
                // Set before invoking Commit: an exception is ambiguity, not a rejection.
                publicationMayBeCommitted = true;
                DomainFactDelivery delivery = stage.Commit();
                publicationMayBeCommitted = delivery.MayBeCommitted;
                failureReason = delivery.Detail;
                if (!delivery.MayBeCommitted)
                    throw new InvalidOperationException("physical-publication-rejected:" + delivery.Detail);
                if (!TryRecordDelivery(receipt.CommitId, delivery, out string proofFailure))
                    failureReason = proofFailure;
            }
        }
        catch (Exception exception)
        {
            if (publicationMayBeCommitted)
            {
                failureReason =
                    "physical-reserved-outcome-reconciliation-pending:"
                    + exception.Message;
                return true;
            }
            stage?.Cancel();
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
            defaultEffect,
            out receipt,
            out failureReason);
    }

    public bool TryCommitPending(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        IPhysicalItemDispositionEffect effect,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => TryCommitPendingCore(
        inputs,
        kind,
        operationId,
        reasonCode,
        requiredSourceState: null,
        effect,
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
        defaultEffect,
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
        IPhysicalItemDispositionEffect effect,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        return TryCommitPendingCore(
            inputs,
            kind,
            operationId,
            reasonCode,
            requiredSourceState,
            effect,
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
        IPhysicalItemDispositionEffect effect,
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
                    effect,
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
                    effect != null,
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
        if (!TryStageEffect(effect, receipt, ownerRevision, out IDomainFactStage stage, out failureReason))
        { receipt = default; return false; }
        try { repository.AddPendingBatchDisposition(CreatePending(receipt, stage)); }
        catch { stage?.Cancel(); throw; }
        if (stage != null) QueueRecovery(operation);

        bool publicationMayBeCommitted = false;
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
            if (stage != null)
            {
                // Set before invoking Commit: an exception is ambiguity, not a rejection.
                publicationMayBeCommitted = true;
                DomainFactDelivery delivery = stage.Commit();
                publicationMayBeCommitted = delivery.MayBeCommitted;
                failureReason = delivery.Detail;
                if (!delivery.MayBeCommitted)
                    throw new InvalidOperationException("physical-publication-rejected:" + delivery.Detail);
                if (!TryRecordDelivery(receipt.CommitId, delivery, out string proofFailure))
                    failureReason = proofFailure;
            }
        }
        catch (Exception exception)
        {
            if (publicationMayBeCommitted)
            {
                committedMutations = mutations;
                failureReason =
                    "physical-gameplay-outcome-reconciliation-pending:"
                    + exception.Message;
                return true;
            }
            stage?.Cancel();
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
#if UNITY_EDITOR
        if (hasPending && DebugRejectAcknowledgement?.Invoke(canonical) == true)
        {
            failureReason = "qa-physical-acknowledgement-rejected";
            return false;
        }
#endif
        if (participant != null && !hasPending)
        { failureReason = "physical-joint-ack-exact-receipt-missing"; return false; }
        if (hasPending && pending.publicationExpected
            && (!TryReconcilePublication(pending, out failureReason)
                || !repository.TryGetPendingBatchDispositionByCommitId(canonical, out pending)
                || !pending.publicationDelivered))
        {
            if (string.IsNullOrEmpty(failureReason)) failureReason = "physical-publication-not-delivered";
            return false;
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
        pending.ownerRevision,
        (pending.sourceFacts
                ?? new List<PhysicalItemDispositionSourceFactSaveData>())
            .Select(PhysicalItemSourceFactSaveCodec.FromSave)
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
            definition.DisplayName);
        if (fact.IsValid)
            return true;
        failureReason = "physical-disposition-source-fact-invalid:"
            + source.stackId;
        return false;
    }

    private bool TryPrepareCanonicalReplay(in PhysicalItemBatchDispositionReceipt receipt,
        PhysicalItemBatchDispositionSaveData pending, IPhysicalItemDispositionEffect effect, out string failureReason)
    {
        failureReason = string.Empty;
        if (!pending.publicationExpected)
        {
            if (effect == null) return true;
            failureReason = "physical-unobserved-replay-effect-conflict";
            return false;
        }
        if (pending.defaultObservationDay > 0) return TryReconcilePublication(pending, out failureReason);
        if (effect == null)
        { failureReason = "physical-joint-replay-effect-missing"; return false; }
        if (!effect.TryStage(receipt, pending.ownerRevision, out var stage, out failureReason)) return false;
        bool exact = stage != null && stage.IsReplay && stage.OperationKey == pending.publicationKey
            && stage.OwnerRevision == pending.ownerRevision;
        stage?.Cancel();
        if (!exact) { failureReason = "physical-joint-replay-payload-conflict"; return false; }
        return TryReconcilePublication(pending, out failureReason);
    }

    private static bool TryStageEffect(IPhysicalItemDispositionEffect effect, in PhysicalItemBatchDispositionReceipt receipt,
        long revision, out IDomainFactStage stage, out string failure)
    {
        stage = null; failure = string.Empty;
        if (effect == null) return true; // Explicit reversible intermediate or isolated fixture, not a live default route.
        try
        {
            if (!effect.TryStage(receipt, revision, out stage, out failure))
            { stage?.Cancel(); stage = null; return false; }
            if (stage != null && stage.IsActive && !stage.IsReplay && stage.OwnerRevision == revision
                && DomainFactValueSyntax.IsText(stage.OperationKey, 2048, false)) return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        {
            stage?.Cancel(); stage = null;
            failure = "physical-disposition-prepare-failed:" + exception.GetType().Name;
            return false;
        }
        stage?.Cancel(); stage = null;
        failure = "physical-disposition-stage-identity-or-owner-invalid";
        return false;
    }

    private static PhysicalItemBatchDispositionSaveData CreatePending(in PhysicalItemBatchDispositionReceipt receipt,
        IDomainFactStage stage) => new()
    {
        kind = (int)receipt.Kind, operationId = receipt.OperationId, reasonCode = receipt.ReasonCode,
        requestFingerprint = receipt.RequestFingerprint, sourceStackIds = receipt.SourceStackIds.ToList(),
        quantity = receipt.Quantity, inputMassGrams = receipt.InputMassGrams, commitId = receipt.CommitId,
        ownerRevision = stage == null ? 0 : receipt.OwnerRevision, publicationExpected = stage != null,
        publicationKey = stage?.OperationKey ?? string.Empty,
        defaultObservationDay = stage is DefaultStage standard ? standard.AbsoluteDay : 0,
        sourceFacts = receipt.SourceFacts.Select(value => PhysicalItemSourceFactSaveCodec.ToSave(value)).ToList()
    };

    private bool TryReconcilePublication(PhysicalItemBatchDispositionSaveData pending, out string failure)
    {
        failure = string.Empty;
        DomainFactDelivery delivery;
        if (pending.defaultObservationDay > 0)
        {
            if (recovery == null) { failure = "physical-disposition-recovery-unavailable"; return false; }
            var fact = new PhysicalItemDisposedFact(RestoreReceipt(pending), pending.defaultObservationDay);
            var results = new DomainFactDelivery[1];
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey, fact) }, results, out failure)) return false;
            delivery = results[0];
        }
        else
        {
            if (deliveries == null) { failure = "physical-disposition-delivery-query-unavailable"; return false; }
            delivery = deliveries.Reconcile(pending.publicationKey);
        }
        return TryRecordDelivery(pending.commitId, delivery, out failure);
    }

    private bool TryRecordDelivery(string commitId, in DomainFactDelivery delivery, out string failure)
    {
        failure = delivery.Detail;
        if (!repository.TryGetPendingBatchDispositionByCommitId(commitId, out var pending))
        { failure = "physical-disposition-publication-owner-missing"; return false; }
        if (!string.IsNullOrEmpty(delivery.OperationKey) && delivery.OperationKey != pending.publicationKey)
        { failure = "physical-disposition-publication-key-conflict"; return false; }
        if (delivery.Phase == DomainFactDeliveryPhase.InDoubt)
        { if (string.IsNullOrEmpty(failure)) failure = "physical-disposition-publication-in-doubt"; return true; }
        if (!delivery.MayBeCommitted || delivery.OperationKey != pending.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 256, false)
            || !IsCanonicalRequired(delivery.PublicationId)
            || delivery.Fingerprint.Length != 64
            || !delivery.Fingerprint.All(value => value is >= '0' and <= '9' or >= 'a' and <= 'f')
            || !repository.TryRecordPendingBatchDispositionPublication(commitId, delivery.PublicationId,
                delivery.Fingerprint, delivery.Completed))
        { failure = "physical-disposition-publication-proof-conflict:" + failure; return false; }
        return true;
    }

    private sealed class DefaultEffect : IPhysicalItemDispositionEffect
    {
        private readonly IDomainFactStagingPublisher facts;
        private readonly IGameCalendar calendar;
        public DefaultEffect(IDomainFactStagingPublisher facts, IGameCalendar calendar)
        { this.facts = facts; this.calendar = calendar; }
        public bool TryStage(in PhysicalItemBatchDispositionReceipt exact, long expectedRevision,
            out IDomainFactStage stage, out string failure)
        {
            var fact = new PhysicalItemDisposedFact(exact, Math.Max(1, calendar.Day));
            if (!facts.TryStage(fact, out var prepared, out failure)) { stage = null; return false; }
            stage = new DefaultStage(prepared, fact.AbsoluteDay);
            return true;
        }
    }

    private sealed class DefaultStage : IDomainFactStage
    {
        private readonly IDomainFactStage inner;
        public DefaultStage(IDomainFactStage inner, int day) { this.inner = inner; AbsoluteDay = day; }
        public int AbsoluteDay { get; }
        public string OperationKey => inner.OperationKey;
        public long OwnerRevision => inner.OwnerRevision;
        public bool IsReplay => inner.IsReplay;
        public bool IsActive => inner.IsActive;
        public DomainFactDelivery Commit() => inner.Commit();
        public void Cancel() => inner.Cancel();
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
