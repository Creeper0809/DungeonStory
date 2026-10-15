using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

// Domain-owned pending facts, not receipts or ledger keys. Recovery publishes
// the frozen experience; it never repeats water debit, healing or contamination.
internal sealed class CharacterWaterPublicationOutbox
{
    private readonly CharacterDeprivationStateStore owner;
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IGameEventBus events;
    private readonly Queue<string> queue = new();
    private readonly HashSet<string> queued = new(StringComparer.Ordinal);
    private readonly HashSet<string> delivering = new(StringComparer.Ordinal);
    private int observedRestoreRevision = -1;
    private float nextRecoveryAt;
    private string lastFailure = string.Empty;

    internal CharacterWaterPublicationOutbox(CharacterDeprivationStateStore owner, IDomainFactPublisher publisher,
        IDomainFactRecoveryPublisher recovery, IGameEventBus events)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }
    internal DomainFactLease Reserve(CharacterWaterConsumedFact proposal)
    {
        if (proposal == null || !proposal.IsProposal) throw new ArgumentException("water-proposal-required", nameof(proposal));
        string character = proposal.Data.characterId;
        if (owner.TryGetWaterPublication(character, out _) && !Deliver(character, out string pendingFailure))
            throw new InvalidOperationException("water-previous-publication-pending:" + pendingFailure);
        if (!publisher.TryReserve(proposal, out var lease, out string failure))
            throw new InvalidOperationException("water-observer-reservation-failed:" + failure);
        return lease;
    }
    internal float ReadInfectionBurden(string character) =>
        owner.TryGet(new CharacterId(character), out var state) ? state.infectionBurden : 0f;
    internal void Cancel(in DomainFactLease lease) { if (lease.IsValid) publisher.Cancel(lease); }
    internal DomainFactDelivery Publish(in DomainFactLease lease, CharacterWaterConsumedFact actual)
    {
        string reference = lease.PublicationKey;
        var pending = new CharacterWaterPublicationData { publicationKey = reference, fact = actual.Snapshot() };
        owner.RecordWaterPublication(pending);
        DomainFactDelivery result;
        try { result = publisher.Publish(lease, actual); }
        catch (Exception exception) when (Recoverable(exception))
        {
            string detail = "water-publication-response-unknown:" + exception.GetType().Name;
            // Cancel releases only the transient reservation. It cannot undo a
            // committed fact; the frozen owner journal remains the recovery source.
            try { publisher.Cancel(lease); }
            catch (Exception cancellation) when (Recoverable(cancellation))
            { detail += ":reservation-release-unknown:" + cancellation.GetType().Name; }
            result = new DomainFactDelivery(DomainFactDeliveryPhase.InDoubt, reference, null, null, detail);
        }
        if (!result.MayBeCommitted) owner.ClearWaterPublication(pending.fact.characterId, reference);
        else
        {
            Enqueue(pending.fact.characterId);
            if (result.OperationKey != reference)
                result = new DomainFactDelivery(DomainFactDeliveryPhase.InDoubt, reference, null, null,
                    "water-publication-reference-unconfirmed");
        }
        return result;
    }
    internal bool Deliver(string character, out string failure)
    {
        failure = string.Empty;
        if (!owner.TryGetWaterPublication(character, out var pending)) return true;
        if (!delivering.Add(character))
        { failure = "water-notification-reentrant-delivery"; return false; }
        try
        {
            var fact = new CharacterWaterConsumedFact(pending.fact);
            var results = new DomainFactDelivery[1];
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey, fact) }, results, out failure))
                return false;
            if (!results[0].MayBeCommitted || results[0].Phase == DomainFactDeliveryPhase.InDoubt
                || results[0].OperationKey != pending.publicationKey)
            { failure = "water-publication-delivery-pending:" + results[0].Detail; return false; }
            if (!pending.notificationDispatched)
            {
                // Event subscribers may throw after applying their own effect. Mark
                // the original one-shot dispatch before invoking them, never retry it.
                owner.MarkWaterNotificationDispatched(character, pending.publicationKey);
                events.Publish(new CharacterWaterConsumedEvent(new CharacterId(character), pending.fact.sourceId,
                    pending.fact.quality, pending.fact.consumed, pending.fact.pathogenDiseaseId));
            }
            // Gameplay exposure follows the confirmed drink commit, not the
            // availability of narrative projections. Retain only fact delivery.
            if (!results[0].Completed)
            { failure = "water-publication-delivery-pending:" + results[0].Detail; return false; }
            owner.ClearWaterPublication(character, pending.publicationKey);
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "water-publication-recovery-failed:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { delivering.Remove(character); }
    }
    internal void Tick()
    {
        if (owner.IsRestoreStaging) return;
        if (observedRestoreRevision != owner.PublishedRestoreRevision)
        {
            observedRestoreRevision = owner.PublishedRestoreRevision;
            queue.Clear(); queued.Clear(); nextRecoveryAt = 0f;
            foreach (var pending in owner.CaptureWaterPublications()) Enqueue(pending.fact.characterId);
        }
        if (queue.Count == 0 || Time.unscaledTime < nextRecoveryAt) return;
        nextRecoveryAt = Time.unscaledTime + 1f;
        int count = Math.Min(8, queue.Count);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            string character = queue.Dequeue(); queued.Remove(character);
            if (Deliver(character, out string detail)) continue;
            failure = detail; Enqueue(character);
        }
        if (failure.Length > 0 && failure != lastFailure) Debug.LogWarning("Water fact delivery pending: " + failure);
        lastFailure = failure;
    }
    private void Enqueue(string character) { if (queued.Add(character)) queue.Enqueue(character); }
    private static bool Recoverable(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
}
