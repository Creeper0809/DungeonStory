using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class FacilitySynthesisPendingFactData
{
    public FacilitySynthesisCompletedFactData fact;
    public string publicationKey = string.Empty;
    public FacilitySynthesisPendingFactData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey };
}

[Serializable]
public sealed class FacilitySynthesisFactSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<FacilitySynthesisPendingFactData> pending = new();
}

// Retains only domain facts awaiting acknowledgement. Recovery never mutates facilities or production.
public sealed class FacilitySynthesisFactOutbox : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactStagingPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private FacilitySynthesisFactSaveData state = new();
    private Publication active;
    private bool preparing, recovering, restoring;
    public string LastFailure { get; private set; } = string.Empty;

    public FacilitySynthesisFactOutbox(IDomainFactStagingPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool TryBegin(FacilitySynthesisCompletedFact fact, out IDomainFactStage stage, out string failure)
    {
        stage = null; failure = string.Empty;
        if (restoring || preparing || recovering || active != null)
        { failure = "facility-synthesis-publication-busy"; return false; }
        if (!RecoverPending(out failure)) return false;
        preparing = true;
        IDomainFactStage reserved = null;
        try
        {
            if (!publisher.TryStage(fact, out reserved, out failure)) return false;
            // An observed result with unapplied source materials is a conflict, never permission to consume again.
            if (reserved == null || !reserved.IsActive || reserved.IsReplay
                || !Key(reserved.OperationKey) || reserved.OwnerRevision != fact.Snapshot().revision)
            { failure = "facility-synthesis-publication-reservation-conflict"; return false; }
            active = new Publication(this, reserved, fact);
            stage = active; return true;
        }
        catch (Exception error) when (Recoverable(error))
        { failure = "facility-synthesis-publication-prepare:" + error.Message; return false; }
        finally
        {
            preparing = false;
            if (stage == null) { reserved?.Cancel(); LastFailure = failure; }
        }
    }

    private sealed class Publication : IDomainFactStage
    {
        private readonly FacilitySynthesisFactOutbox owner;
        private readonly IDomainFactStage inner;
        private readonly FacilitySynthesisCompletedFact fact;
        private bool attempted, ended;
        public Publication(FacilitySynthesisFactOutbox owner, IDomainFactStage inner, FacilitySynthesisCompletedFact fact)
        { this.owner = owner; this.inner = inner; this.fact = fact; }
        public string OperationKey => inner.OperationKey;
        public long OwnerRevision => inner.OwnerRevision;
        public bool IsReplay => false;
        public bool IsActive => !ended && !attempted && inner.IsActive;
        public DomainFactDelivery Commit()
        {
            if (ended || attempted || !ReferenceEquals(owner.active, this))
                throw new InvalidOperationException("facility-synthesis-publication-not-active");
            attempted = true;
            if (!inner.IsActive) return DomainFactDelivery.Reject("facility-synthesis-observer-disconnected-before-commit");
            var row = new FacilitySynthesisPendingFactData { fact = fact.Snapshot(), publicationKey = OperationKey };
            owner.state.pending.Add(row);
            DomainFactDelivery delivery;
            try { delivery = inner.Commit(); }
            catch (Exception error) when (Recoverable(error))
            { delivery = DomainFactDelivery.Uncertain("facility-synthesis-publication-response-unknown:" + error.Message); }
            if (!delivery.MayBeCommitted || Accepted(row, delivery)) owner.state.pending.Remove(row);
            owner.LastFailure = owner.state.pending.Contains(row)
                ? "facility-synthesis-publication-pending:" + delivery.Detail : delivery.Detail;
            return delivery.MayBeCommitted && !Accepted(row, delivery)
                ? DomainFactDelivery.Uncertain(owner.LastFailure) : delivery;
        }
        public void Cancel()
        {
            if (ended) return;
            ended = true;
            try { inner.Cancel(); }
            finally { if (ReferenceEquals(owner.active, this)) owner.active = null; }
        }
    }

    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (restoring || preparing || recovering || active != null)
        { failure = "facility-synthesis-recovery-busy"; return false; }
        recovering = true;
        try
        {
            foreach (var row in state.pending.ToArray())
            {
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey,
                        new FacilitySynthesisCompletedFact(row.fact)) }, results, out failure)
                    || !Accepted(row, results[0]))
                { LastFailure = failure = "facility-synthesis-recovery-pending:" + failure + ":" + results[0].Detail; return false; }
                state.pending.Remove(row);
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception error) when (Recoverable(error))
        { LastFailure = failure = "facility-synthesis-recovery:" + error.Message; return false; }
        finally { recovering = false; }
    }
    private static bool Accepted(FacilitySynthesisPendingFactData row, DomainFactDelivery value) =>
        value.Completed && value.OperationKey == row.publicationKey && Key(value.PublicationId)
        && value.Fingerprint?.Length == 64 && value.Fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Key(string value) => DomainFactValueSyntax.IsText(value, 2048, false);
    private static bool Recoverable(Exception error) => error is not OutOfMemoryException
        && error is not StackOverflowException && error is not AccessViolationException;
    public void Tick()
    {
        if (state.pending.Count == 0 || restoring || preparing || recovering || active != null || retryClock.ElapsedMilliseconds < 1000) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);
    public FacilitySynthesisFactSaveData Capture() { RequireIdle(); return ValidateAndClone(state); }
    public static FacilitySynthesisFactSaveData ValidateAndClone(FacilitySynthesisFactSaveData source)
    {
        if (source?.version != FacilitySynthesisFactSaveData.CurrentVersion || source.pending == null)
            throw new ArgumentException("facility-synthesis-facts-save-invalid");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var owners = new HashSet<string>(StringComparer.Ordinal);
        var result = new FacilitySynthesisFactSaveData();
        foreach (var row in source.pending)
        {
            if (row?.fact == null || !Key(row.publicationKey) || !keys.Add(row.publicationKey)
                || !owners.Add(row.fact.survivorFacilityId))
                throw new ArgumentException("facility-synthesis-pending-identity-invalid");
            row.fact.Validate(); result.pending.Add(row.Clone());
        }
        return result;
    }
    private void RequireIdle()
    { if (active != null || preparing || recovering) throw new InvalidOperationException("facility-synthesis-publication-active"); }
    public void Restore(FacilitySynthesisFactSaveData source)
    {
        RequireIdle(); var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var before = state; return () => state = before; })) state = candidate;
    }
    public string ParticipantId => FacilitySynthesisFactSaveSection.Id;
    public void BeginRestoreCandidate()
    { RequireIdle(); if (restoring) throw new InvalidOperationException("facility-synthesis-already-restoring"); restore.Begin(); restoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { restoring = false; } }
    public void CompleteRestoreCandidate() { try { restore.Complete(); } finally { restoring = false; } }
}
