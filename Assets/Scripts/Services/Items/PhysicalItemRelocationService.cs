using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonStory.Foundation.Facts;
using VContainer.Unity;

public readonly struct PhysicalItemRelocationReceipt
{
    internal PhysicalItemRelocationReceipt(
        string operationId,
        string reasonCode,
        string sourceStackId,
        string destinationStackId,
        string itemId,
        int quantity,
        long massGrams,
        Vector2Int sourcePosition,
        Vector2Int destinationPosition)
    {
        OperationId = operationId;
        ReasonCode = reasonCode;
        SourceStackId = sourceStackId;
        DestinationStackId = destinationStackId;
        ItemId = itemId;
        Quantity = quantity;
        MassGrams = massGrams;
        SourcePosition = sourcePosition;
        DestinationPosition = destinationPosition;
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public string SourceStackId { get; }
    public string DestinationStackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public Vector2Int SourcePosition { get; }
    public Vector2Int DestinationPosition { get; }
    public bool IsCommitted => OperationId?.Length > 0
        && ReasonCode?.Length > 0
        && SourceStackId?.Length > 0
        && DestinationStackId?.Length > 0
        && ItemId?.Length > 0
        && Quantity > 0
        && MassGrams > 0L;
}

public interface IPhysicalItemRelocationService
{
    bool TryRelocateQuantity(
        string sourceStackId,
        int quantity,
        Vector2Int destinationPosition,
        WorldItemStackState destinationState,
        string destinationId,
        string operationId,
        string reasonCode,
        out PhysicalItemRelocationReceipt receipt,
        out string failureReason);
}

/// <summary>
/// Exact identity-preserving movement inside the physical world. This is not
/// a Transform: input and output item identity, quantity, components, and gram
/// mass must remain equal.
/// </summary>
public sealed class PhysicalItemRelocationService : IPhysicalItemRelocationService, ITickable, IDungeonSaveRestoreCompletedHook
{
    private readonly WorldItemRepository repository;
    private readonly IPreparedPhysicalItemRelocationService relocations;
    private readonly IDomainFactStagingPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IGameCalendar calendar;
    private readonly Queue<string> pendingPublications = new();
    private readonly HashSet<string> queuedPublications = new(StringComparer.Ordinal);
    private float nextRecoveryTime;
    public string LastRecoveryFailure { get; private set; } = string.Empty;

    public PhysicalItemRelocationService(WorldItemRepository repository,
        IPreparedPhysicalItemRelocationService relocations, IDomainFactStagingPublisher facts,
        IDomainFactRecoveryPublisher recovery, IGameCalendar calendar)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.relocations = relocations ?? throw new ArgumentNullException(nameof(relocations));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public void OnRestoreCompleted()
    {
        pendingPublications.Clear();
        queuedPublications.Clear();
        LastRecoveryFailure = string.Empty;
        nextRecoveryTime = 0f;
        // Rebuild transient scheduling once from owner state, not from ledger rows.
        foreach (string operation in repository.PendingPhysicalRelocationPublicationIds()) QueueRecovery(operation);
    }

    public void Tick()
    {
        if (pendingPublications.Count == 0 || Time.unscaledTime < nextRecoveryTime) return;
        nextRecoveryTime = Time.unscaledTime + 1f;
        int count = Math.Min(8, pendingPublications.Count);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            string operation = pendingPublications.Dequeue();
            queuedPublications.Remove(operation);
            if (!repository.TryGetPhysicalItemRelocation(operation, out var journal)
                || journal.phase == (int)PhysicalItemRelocationJournalPhase.Delivered) continue;
            TryReplay(journal.Clone(), journal.requestFingerprint, out _, out string detail);
            if (repository.TryGetPhysicalItemRelocation(operation, out journal)
                && journal.phase != (int)PhysicalItemRelocationJournalPhase.Delivered)
            {
                QueueRecovery(operation);
                failure = detail;
            }
        }
        if (!string.IsNullOrEmpty(failure) && failure != LastRecoveryFailure)
            Debug.LogWarning("Physical relocation publication recovery pending: " + failure);
        LastRecoveryFailure = failure;
    }

    private void QueueRecovery(string operation)
    { if (queuedPublications.Add(operation)) pendingPublications.Enqueue(operation); }

    public bool TryRelocateQuantity(string sourceStackId, int quantity,
        Vector2Int destinationPosition, WorldItemStackState destinationState,
        string destinationId, string operationId, string reasonCode,
        out PhysicalItemRelocationReceipt receipt, out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        string reason = reasonCode?.Trim() ?? string.Empty;
        string source = sourceStackId?.Trim() ?? string.Empty;
        string destination = destinationId?.Trim() ?? string.Empty;
        string fingerprint = PhysicalItemRelocatedFact.CreateRequestFingerprint(source, quantity, destinationPosition,
            destinationState, destination, operation, reason);
        if (repository.TryGetPhysicalItemRelocation(operation, out var existing))
            return TryReplay(existing.Clone(), fingerprint, out receipt, out failureReason);
        if (!relocations.TryPrepare(source, quantity, destinationPosition, destinationState,
                destination, operation, reason, out var prepared, out failureReason))
            return false;
        var preview = prepared.Preview;
        IDomainFactStage stage = null;
        PhysicalItemRelocatedFact fact;
        try
        {
            fact = new PhysicalItemRelocatedFact(preview, Math.Max(1, calendar.Day), destinationState, destination);
            if (!facts.TryStage(fact, out stage, out failureReason))
            { prepared.Cancel(); return false; }
            if (stage == null || !stage.IsActive || stage.OwnerRevision != preview.OwnerRevision
                || string.IsNullOrEmpty(stage.OperationKey) || stage.IsReplay)
            {
                stage?.Cancel();
                prepared.Cancel();
                failureReason = "physical-relocation-publication-without-owner-journal";
                return false;
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            stage?.Cancel();
            prepared.Cancel();
            failureReason = "physical-relocation-observation-prepare:" + exception.Message;
            return false;
        }
        if (!repository.CanAddPhysicalItemRelocation(operation))
        {
            stage.Cancel();
            prepared.Cancel();
            failureReason = "physical-relocation-journal-capacity-exhausted";
            return false;
        }
        var journal = CreateJournal(fact, destinationState, destination, fingerprint, stage.OperationKey);
        if (!prepared.TryApply(out var transaction, out failureReason))
        { stage.Cancel(); return false; }
        try { repository.AddPhysicalItemRelocation(journal); }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            if (!transaction.TryRollback(out string rollbackFailure))
                throw new InvalidOperationException("physical-relocation-journal-rollback:" + rollbackFailure, exception);
            stage.Cancel();
            failureReason = "physical-relocation-journal-write-failed:" + exception.Message;
            return false;
        }

        QueueRecovery(operation);
        DomainFactDelivery delivery;
        try { delivery = stage.Commit(); }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            // A lost response is not proof of rejection. Preserve the applied
            // movement and its exact saved fact for observer recovery.
            delivery = DomainFactDelivery.Uncertain("physical-relocation-publication-in-doubt:" + exception.Message);
        }
        if (!delivery.MayBeCommitted)
        {
            if (!transaction.TryRollback(out string rollbackFailure))
                throw new InvalidOperationException("physical-relocation-publication-rollback:" + rollbackFailure);
            stage.Cancel();
            repository.RemovePhysicalItemRelocation(operation);
            failureReason = delivery.Detail;
            return false;
        }
        receipt = transaction.Receipt;
        RecordDelivery(journal, delivery, out failureReason);
        if (!transaction.TryAcknowledge(out string acknowledgementFailure) && failureReason.Length == 0)
            failureReason = acknowledgementFailure;
        return true;
    }

    private bool TryReplay(PhysicalItemRelocationSaveData journal, string requestFingerprint,
        out PhysicalItemRelocationReceipt receipt, out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (journal.requestFingerprint != requestFingerprint)
        { failureReason = "physical-relocation-operation-conflict:" + journal.operationId; return false; }
        PhysicalItemRelocatedFact fact;
        try { ValidateJournal(journal); fact = PhysicalItemRelocatedFact.FromSave(journal); }
        catch (Exception exception) when (IsRecoverable(exception))
        { failureReason = "physical-relocation-journal-invalid:" + exception.Message; return false; }
        var deliveries = new DomainFactDelivery[1];
        try
        {
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(journal.publicationKey, fact) },
                    deliveries, out failureReason) || !deliveries[0].MayBeCommitted)
                return false;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        { failureReason = "physical-relocation-recovery-failed:" + exception.Message; return false; }
        if (!RecordDelivery(journal, deliveries[0], out failureReason)) return false;
        // No relocation prepare/apply call on this path, including cold restore.
        receipt = fact.Preview.Receipt;
        return true;
    }

    private bool RecordDelivery(PhysicalItemRelocationSaveData journal,
        in DomainFactDelivery delivery, out string failure)
    {
        failure = delivery.Detail;
        if ((!string.IsNullOrEmpty(delivery.OperationKey) && delivery.OperationKey != journal.publicationKey)
            || (!string.IsNullOrEmpty(journal.publicationId) && !string.IsNullOrEmpty(delivery.PublicationId)
                && journal.publicationId != delivery.PublicationId)
            || (!string.IsNullOrEmpty(journal.publicationFingerprint) && !string.IsNullOrEmpty(delivery.Fingerprint)
                && journal.publicationFingerprint != delivery.Fingerprint))
        { failure = "physical-relocation-publication-identity-conflict"; return false; }
        if (delivery.Phase == DomainFactDeliveryPhase.InDoubt)
        {
            if (string.IsNullOrEmpty(failure)) failure = "physical-relocation-publication-in-doubt";
            return true;
        }
        if (delivery.OperationKey != journal.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 256, false)
            || !DomainFactValueSyntax.IsText(delivery.Fingerprint, 256, false))
        { failure = "physical-relocation-publication-proof-missing"; return false; }
        journal.publicationId = delivery.PublicationId;
        journal.publicationFingerprint = delivery.Fingerprint;
        // A transient later delivery failure cannot erase a previously acknowledged proof.
        journal.phase = Math.Max(journal.phase, (int)(delivery.Completed
            ? PhysicalItemRelocationJournalPhase.Delivered : PhysicalItemRelocationJournalPhase.PublicationAccepted));
        if (!repository.TryUpdatePhysicalItemRelocation(journal))
            throw new InvalidOperationException("physical-relocation-journal-update-missing:" + journal.operationId);
        if (!delivery.Completed && string.IsNullOrEmpty(failure))
            failure = "physical-relocation-publication-delivery-pending";
        return true;
    }

    internal static void ValidateJournal(PhysicalItemRelocationSaveData journal)
    {
        var fact = PhysicalItemRelocatedFact.FromSave(journal);
        var moved = fact.Preview.Receipt;
        if (!Enum.IsDefined(typeof(WorldItemStackState), journal.destinationState)
            || (WorldItemStackState)journal.destinationState is WorldItemStackState.Carried or WorldItemStackState.InTransit
            || !DomainFactValueSyntax.IsText(journal.destinationId, 256, true)
            || journal.destinationId != journal.destinationId.Trim()
            || !DomainFactValueSyntax.IsText(journal.publicationKey, 512, false)
            || journal.phase < (int)PhysicalItemRelocationJournalPhase.DomainCommitted
            || journal.phase > (int)PhysicalItemRelocationJournalPhase.Delivered
            || journal.requestFingerprint != PhysicalItemRelocatedFact.CreateRequestFingerprint(moved.SourceStackId, moved.Quantity,
                moved.DestinationPosition, (WorldItemStackState)journal.destinationState, journal.destinationId,
                moved.OperationId, moved.ReasonCode))
            throw new InvalidOperationException("physical-relocation-journal-shape-invalid");
        bool hasProof = DomainFactValueSyntax.IsText(journal.publicationId, 256, false)
            && DomainFactValueSyntax.IsText(journal.publicationFingerprint, 256, false);
        if (journal.phase == (int)PhysicalItemRelocationJournalPhase.DomainCommitted
            ? !string.IsNullOrEmpty(journal.publicationId) || !string.IsNullOrEmpty(journal.publicationFingerprint)
            : !hasProof)
            throw new InvalidOperationException("physical-relocation-journal-delivery-proof-invalid");
    }

    private static PhysicalItemRelocationSaveData CreateJournal(PhysicalItemRelocatedFact fact,
        WorldItemStackState destinationState, string destinationId, string requestFingerprint, string publicationKey)
    {
        var preview = fact.Preview;
        var exact = preview.Receipt;
        return new PhysicalItemRelocationSaveData
        {
            operationId = exact.OperationId, reasonCode = exact.ReasonCode, requestFingerprint = requestFingerprint,
            phase = (int)PhysicalItemRelocationJournalPhase.DomainCommitted,
            sourceStackId = exact.SourceStackId, destinationStackId = exact.DestinationStackId,
            itemDefinitionId = exact.ItemId, itemInstanceId = preview.ItemInstanceId,
            quantity = exact.Quantity, massGrams = exact.MassGrams,
            sourceX = exact.SourcePosition.x, sourceY = exact.SourcePosition.y,
            destinationX = exact.DestinationPosition.x, destinationY = exact.DestinationPosition.y,
            destinationState = (int)destinationState, destinationId = destinationId,
            ownerRevision = preview.OwnerRevision, absoluteDay = fact.AbsoluteDay,
            displayText = preview.DisplayName, publicationKey = publicationKey
        };
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;
}
