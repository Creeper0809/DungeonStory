using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

internal sealed class InfrastructureObservationRuntime : IInfrastructureFactJournal, IInfrastructureObservationPersistence, ITickable
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly IDomainFactStagingPublisher observations;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IGameCalendar calendar;
    private Stage active;
    private bool replaying;

    public InfrastructureObservationRuntime(DungeonRuntimeAggregateRootStore rootStore, IDomainFactStagingPublisher observations,
        IDomainFactRecoveryPublisher recovery, IGameCalendar calendar)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.observations = observations ?? throw new ArgumentNullException(nameof(observations));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        _ = Current;
    }
    private InfrastructureObservationAggregateState Current => rootStore.GetOrCreate(() => new InfrastructureObservationAggregateState());
    private InfrastructureObservationAggregateState Writable => rootStore.GetOrCreateWritable(
        () => new InfrastructureObservationAggregateState(), state => state.DeepClone());

    public void AssertIdle()
    { if (active != null || replaying) throw new InvalidOperationException("industrial-fact-publication-in-progress"); }
    public IndustrialTransitionFactData CaptureDraft(IndustrialTransitionKind kind, string targetId, string facilityId,
        string facilityDisplayName, int x, int y, IndustrialStateData before, IndustrialStateData after, IndustrialTransitionDetails details = null)
    {
        AssertIdle();
        var draft = new IndustrialTransitionFactData
        { kind = kind, targetId = targetId, facilityId = facilityId, facilityDisplayName = facilityDisplayName,
            facilityX = x, facilityY = y, absoluteDay = calendar.Day, before = before?.Clone(), after = after?.Clone(),
            details = details?.Clone() ?? new IndustrialTransitionDetails() };
        IndustrialTransitionFact.ValidateDraft(draft);
        return draft;
    }
    public bool TryPrepare(IndustrialTransitionFactData draft, out IDomainFactStage prepared, out string failure)
    {
        AssertIdle(); prepared = null;
        FlushPending();
        if (Current.Pending.Count > 0) { failure = "industrial-fact-prior-publication-pending"; return false; }
        long revision = Current.NextOwnerRevision;
        if (revision < 1 || revision == long.MaxValue) { failure = "industrial-fact-revision-exhausted"; return false; }
        IDomainFactStage inner = null;
        try
        {
            var data = draft?.Clone();
            IndustrialTransitionFact.ValidateDraft(data);
            if (data.ownerRevision != 0) { failure = "industrial-fact-draft-already-sequenced"; return false; }
            data.ownerRevision = revision;
            var fact = new IndustrialTransitionFact(data);
            active = new Stage(this, fact);
            if (!observations.TryStage(fact, out inner, out failure) || inner == null)
            { inner?.Cancel(); active = null; return false; }
            if (inner.OwnerRevision != revision || inner.IsReplay
                || !DomainFactValueSyntax.IsText(inner.OperationKey, 4096, false) || inner.OperationKey != inner.OperationKey.Trim())
            { inner.Cancel(); active = null; failure = "industrial-fact-stage-identity-conflict"; return false; }
            active.Inner = inner; prepared = active; failure = ""; return true;
        }
        catch (Exception error) when (Nonfatal(error))
        { try { inner?.Cancel(); } finally { active = null; } failure = "industrial-fact-prepare:" + error.Message; return false; }
    }
    public DomainFactDelivery CommitReversible(IDomainFactStage prepared)
    {
        if (prepared is not Stage stage || stage.Owner != this || stage != active || stage.Queued)
            return DomainFactDelivery.Reject("industrial-fact-stage-stale");
        try
        {
            try { QueueCommitted(stage); }
            catch (Exception error) when (Nonfatal(error))
            { return DomainFactDelivery.Reject("industrial-fact-queue:" + error.Message); }
            DomainFactDelivery delivery;
            try { delivery = stage.Inner.Commit(); }
            catch (Exception error) when (Nonfatal(error)) { delivery = DomainFactDelivery.Uncertain("industrial-fact-commit:" + error.Message); }
            if (delivery.Phase == DomainFactDeliveryPhase.Rejected)
            {
                Writable.Pending.Remove(stage.OwnerRevision);
                Writable.NextOwnerRevision = stage.OwnerRevision;
                return delivery;
            }
            return Accept(stage.OwnerRevision, delivery);
        }
        finally { Cancel(stage); }
    }
    public void QueueCommitted(IDomainFactStage prepared)
    {
        if (prepared is not Stage stage || stage.Owner != this || stage != active || stage.Queued
            || stage.OwnerRevision != Current.NextOwnerRevision)
            throw new InvalidOperationException("industrial-fact-queue-stage-stale");
        var pending = new InfrastructureObservationSaveData { fact = stage.Fact.Snapshot(), publicationKey = stage.OperationKey };
        pending.Validate(checked(stage.OwnerRevision + 1));
        var state = Writable;
        state.Pending.Add(stage.OwnerRevision, pending);
        state.NextOwnerRevision = checked(stage.OwnerRevision + 1);
        stage.Queued = true;
        // The producing owner applies its effects before DeliverQueued releases this fence.
    }
    public void DeliverQueued(long ownerRevision, IDomainFactStage prepared = null)
    {
        if (prepared != null && (prepared is not Stage supplied || supplied.Owner != this || supplied != active || !supplied.Queued))
            throw new InvalidOperationException("industrial-fact-delivery-stage-stale");
        if (prepared == null) AssertIdle();
        if (!Current.Pending.TryGetValue(ownerRevision, out var pending)) { if (prepared != null) Cancel(prepared); return; }
        replaying = true;
        try
        {
            DomainFactDelivery delivery;
            if (prepared is Stage stage) delivery = stage.Inner.Commit();
            else
            {
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey, new IndustrialTransitionFact(pending.fact)) }, results, out string failure))
                { Debug.LogError("Industrial fact remains pending: " + ownerRevision + ":" + failure); return; }
                delivery = results[0];
            }
            delivery = Accept(ownerRevision, delivery);
            if (!delivery.Completed) Debug.LogError("Industrial fact remains pending: " + ownerRevision + ":" + delivery.Detail);
        }
        catch (Exception error) when (Nonfatal(error))
        { Debug.LogError("Industrial fact remains pending after uncertain delivery: " + ownerRevision + ":" + error.Message); }
        finally { replaying = false; if (prepared != null) Cancel(prepared); }
    }
    private DomainFactDelivery Accept(long revision, DomainFactDelivery delivery)
    {
        if (delivery.Phase is DomainFactDeliveryPhase.Rejected or DomainFactDeliveryPhase.InDoubt) return delivery;
        if (!Current.Pending.TryGetValue(revision, out var saved) || delivery.OperationKey != saved.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 4096, false) || delivery.PublicationId != delivery.PublicationId.Trim()
            || !InfrastructureObservationSaveData.IsDigest(delivery.Fingerprint)
            || saved.publicationId.Length > 0 && (saved.publicationId != delivery.PublicationId || saved.publicationFingerprint != delivery.Fingerprint))
            return DomainFactDelivery.Uncertain("industrial-fact-delivery-proof-conflict");
        if (delivery.Completed) Writable.Pending.Remove(revision);
        else
        {
            var copy = saved.Clone(); copy.publicationId = delivery.PublicationId; copy.publicationFingerprint = delivery.Fingerprint;
            Writable.Pending[revision] = copy;
        }
        return delivery;
    }
    public bool IsPending(long ownerRevision) => Current.Pending.ContainsKey(ownerRevision);
    public void Cancel(IDomainFactStage prepared)
    {
        if (prepared is not Stage stage || stage.Owner != this) return;
        try { stage.Inner?.Cancel(); }
        finally { if (active == stage) active = null; stage.Closed = true; }
    }
    public void Tick() { AssertIdle(); FlushPending(); }
    private void FlushPending()
    {
        foreach (long revision in Current.Pending.Keys.ToArray())
        { DeliverQueued(revision); if (Current.Pending.ContainsKey(revision)) break; }
    }
    public DungeonInfrastructureObservationSaveData Capture()
    { AssertIdle(); return new() { nextOwnerRevision = Current.NextOwnerRevision, pending = Current.Pending.Values.Select(value => value.Clone()).ToList() }; }
    public InfrastructureObservationRestoreCandidate PrepareRestore(DungeonInfrastructureObservationSaveData snapshot)
    {
        AssertIdle(); InfrastructureObservationSaveValidation.RequireValid(snapshot);
        return new(snapshot.nextOwnerRevision, snapshot.pending.Select(value => value.Clone()).ToArray());
    }
    public void Restore(InfrastructureObservationRestoreCandidate candidate)
    {
        AssertIdle(); if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        var replacement = new InfrastructureObservationAggregateState { NextOwnerRevision = candidate.NextOwnerRevision };
        foreach (var row in candidate.Pending) replacement.Pending.Add(row.fact.ownerRevision, row.Clone());
        rootStore.Replace(replacement);
    }
    private static bool Nonfatal(Exception error) => error is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
    private sealed class Stage : IDomainFactStage
    {
        internal readonly InfrastructureObservationRuntime Owner; internal readonly IndustrialTransitionFact Fact;
        internal IDomainFactStage Inner; internal bool Queued, Closed;
        internal Stage(InfrastructureObservationRuntime owner, IndustrialTransitionFact fact) { Owner = owner; Fact = fact; }
        public string OperationKey => Inner?.OperationKey ?? "";
        public long OwnerRevision => Fact.OwnerRevision;
        public bool IsReplay => Inner?.IsReplay ?? false;
        public bool IsActive => !Closed && Owner.active == this;
        public DomainFactDelivery Commit() => Owner.CommitReversible(this);
        public void Cancel() => Owner.Cancel(this);
    }
}

internal static class InfrastructureObservationSaveValidation
{
    public static void RequireValid(DungeonInfrastructureObservationSaveData data)
    {
        if (data == null || data.version != DungeonInfrastructureObservationSaveData.CurrentVersion || data.nextOwnerRevision < 1 || data.pending == null)
            throw new InvalidOperationException("industrial-observation-save-header-invalid");
        long previous = 0;
        foreach (var row in data.pending)
        {
            if (row?.fact == null || row.fact.ownerRevision <= previous) throw new InvalidOperationException("industrial-observation-save-order-invalid");
            row.Validate(data.nextOwnerRevision); previous = row.fact.ownerRevision;
        }
    }
}

internal static class InfrastructureFactExecution
{
    public static IndustrialTransitionFactData CaptureDraft(IInfrastructureFactJournal journal, IndustrialTransitionKind kind,
        string targetId, BuildableObject facility, IndustrialStateData before, IndustrialStateData after, IndustrialTransitionDetails details = null)
    {
        if (journal == null || facility?.BuildingData == null) throw new InvalidOperationException("industrial-fact-owner-context-missing");
        return journal.CaptureDraft(kind, targetId, IndustrialInfrastructureIdentity.GetNodeId(facility),
            facility.BuildingData.objectName, facility.centerPos.x, facility.centerPos.y, before, after, details);
    }
    public static bool TryPrepare(IInfrastructureFactJournal journal, IndustrialTransitionKind kind, string targetId,
        BuildableObject facility, IndustrialStateData before, IndustrialStateData after, out IDomainFactStage prepared,
        out InfrastructureCommandResult failure, IndustrialTransitionDetails details = null)
    {
        prepared = null; failure = default;
        try
        {
            var draft = CaptureDraft(journal, kind, targetId, facility, before, after, details);
            if (journal.TryPrepare(draft, out prepared, out string reason)) return true;
            failure = InfrastructureCommandResult.Failed(FailureCode.IndustrialCommandInvalid, reason); return false;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { failure = InfrastructureCommandResult.Failed(FailureCode.IndustrialCommandInvalid, "industrial-fact-prepare:" + error.Message); return false; }
    }
    public static InfrastructureCommandResult CommitFailure(in DomainFactDelivery result) =>
        InfrastructureCommandResult.Failed(FailureCode.IndustrialCommandInvalid, "industrial-fact-not-confirmed", result.Detail);
}
