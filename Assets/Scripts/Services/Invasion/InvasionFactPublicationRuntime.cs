using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class InvasionFactPublicationData
{
    public InvasionLifecycleFactData fact;
    public string publicationKey = string.Empty, publicationId = string.Empty, fingerprint = string.Empty;
    public bool pending;
    public InvasionFactPublicationData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, publicationId = publicationId, fingerprint = fingerprint, pending = pending };
}
[Serializable]
public sealed class InvasionFactPublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<InvasionFactPublicationData> observations = new();
}

/// <summary>Owns frozen invasion facts. Recovery never spawns actors, wears equipment or applies damage.</summary>
public sealed class InvasionFactPublicationRuntime : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private InvasionFactPublicationSaveData state = new();
    private Preparation active;
    private Action cleanup;
    private bool preparing, recovering;
    public bool IsRestoring { get; private set; }
    public bool IsBusy => active != null || preparing || recovering || cleanup != null;
    public string LastFailure { get; private set; } = string.Empty;

    public InvasionFactPublicationRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool TryBegin(InvasionLifecycleFactData source, out Preparation prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        if (IsBusy || IsRestoring) { failure = "invasion-publication-busy"; return false; }
        // A new stage needs its actual predecessor observed before the observer can resolve causation.
        if (!RecoverPending(out failure)) return false;
        preparing = true; DomainFactLease lease = default;
        try
        {
            var data = source?.Clone() ?? throw new ArgumentNullException(nameof(source));
            var previous = state.observations.LastOrDefault(row => row.fact.runtimeId == data.runtimeId);
            data.sequence = previous == null ? 1 : checked(previous.fact.sequence + 1);
            data.parentPublicationKey = previous?.publicationKey ?? string.Empty;
            ValidateSuccessor(previous?.fact, data);
            var proposal = new InvasionLifecycleFact(data, true);
            if (!publisher.TryReserve(proposal, out lease, out failure)) return false;
            if (!lease.IsValid || !Text(lease.PublicationKey))
            { failure = "invasion-publication-reservation-invalid"; publisher.Cancel(lease); return false; }
            active = prepared = new Preparation(this, proposal, lease);
            return true;
        }
        catch (Exception error) when (Recoverable(error))
        { publisher.Cancel(lease); failure = "invasion-publication-reserve:" + error.Message; return false; }
        finally { preparing = false; if (prepared == null) LastFailure = failure; }
    }

    public sealed class Preparation : IDisposable
    {
        private readonly InvasionFactPublicationRuntime owner;
        internal readonly InvasionLifecycleFact Proposal;
        internal readonly DomainFactLease Lease;
        internal Action Rollback;
        internal bool Attempted, Applied, Disposed;
        internal Preparation(InvasionFactPublicationRuntime owner, InvasionLifecycleFact proposal, DomainFactLease lease)
        { this.owner = owner; Proposal = proposal; Lease = lease; PublicationKey = lease.PublicationKey; }
        public string PublicationKey { get; }
        public bool MayBeApplied => Applied;
        public InvasionLifecycleFactData Snapshot() => Proposal.Snapshot();
        public void SetRollback(Action action)
        {
            if (Disposed || Attempted || Rollback != null || action == null)
                throw new InvalidOperationException("invasion-publication-rollback-contract");
            Rollback = action;
        }
        public DomainFactDelivery PublishApplied(InvasionLifecycleFactData fact) => owner.Publish(this, new InvasionLifecycleFact(fact));
        public void Dispose() => owner.End(this);
    }

    private DomainFactDelivery Publish(Preparation prepared, InvasionLifecycleFact fact)
    {
        RequireActive(prepared);
        if (prepared.Attempted || prepared.Rollback == null || !fact.MatchesProposal(prepared.Proposal))
            throw new InvalidOperationException("invasion-applied-fact-conflict");
        prepared.Attempted = true;
        var row = new InvasionFactPublicationData { fact = fact.Snapshot(), publicationKey = prepared.PublicationKey, pending = true };
        state.observations.Add(row);
        DomainFactDelivery delivered;
        try { delivered = publisher.Publish(prepared.Lease, fact); }
        catch (Exception error) when (Recoverable(error))
        { delivered = DomainFactDelivery.Uncertain("invasion-publication-response-unknown:" + error.Message); }
        if (!delivered.MayBeCommitted)
        { state.observations.Remove(row); LastFailure = delivered.Detail; return delivered; }
        prepared.Applied = true;
        if (Accept(row, delivered)) { LastFailure = string.Empty; return delivered; }
        LastFailure = "invasion-publication-pending:" + delivered.Detail;
        return DomainFactDelivery.Uncertain(LastFailure);
    }
    private void RequireActive(Preparation prepared)
    {
        if (!ReferenceEquals(active, prepared) || prepared.Disposed)
            throw new InvalidOperationException("invasion-publication-not-active");
    }
    private void End(Preparation prepared)
    {
        if (prepared.Disposed) return;
        RequireActive(prepared); prepared.Disposed = true;
        if (!prepared.Applied) cleanup = prepared.Rollback;
        try { RetryCleanup(); }
        finally
        {
            try { publisher.Cancel(prepared.Lease); }
            catch (Exception error) when (Recoverable(error)) { LastFailure = "invasion-publication-release:" + error.Message; }
            finally { active = null; }
        }
    }
    private bool RetryCleanup()
    {
        if (cleanup == null) return true;
        try { cleanup(); cleanup = null; return true; }
        catch (Exception error) when (Recoverable(error))
        { LastFailure = "invasion-publication-cleanup-pending:" + error.Message; return false; }
    }
    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (IsRestoring || preparing || active != null || recovering)
        { failure = "invasion-publication-recovery-busy"; return false; }
        recovering = true;
        try
        {
            if (!RetryCleanup()) { failure = LastFailure; return false; }
            foreach (var row in state.observations)
            {
                if (!row.pending) continue;
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey, new InvasionLifecycleFact(row.fact)) }, results, out failure)
                    || !Accept(row, results[0]))
                { LastFailure = failure = "invasion-publication-replay-pending:" + failure; return false; }
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception error) when (Recoverable(error))
        { LastFailure = failure = "invasion-publication-recovery:" + error.Message; return false; }
        finally { recovering = false; }
    }
    private static bool Accept(InvasionFactPublicationData row, DomainFactDelivery delivery)
    {
        if (!delivery.Completed || delivery.OperationKey != row.publicationKey || !Text(delivery.PublicationId) || !Hash(delivery.Fingerprint)) return false;
        row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint; row.pending = false; return true;
    }
    public void Tick()
    {
        if (IsRestoring || preparing || active != null || recovering || retryClock.ElapsedMilliseconds < 1000
            || cleanup == null && !state.observations.Any(row => row.pending)) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);
    public InvasionFactPublicationSaveData Capture() { RequireIdleMutation(); return ValidateAndClone(state); }
    public static InvasionFactPublicationSaveData ValidateAndClone(InvasionFactPublicationSaveData source)
    {
        if (source?.version != InvasionFactPublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("invasion-publication-save-invalid");
        var result = new InvasionFactPublicationSaveData();
        var last = new Dictionary<string, InvasionFactPublicationData>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var breaches = new HashSet<string>(StringComparer.Ordinal);
        var facilities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.observations)
        {
            if (row?.fact == null) throw new ArgumentException("invasion-publication-history-missing");
            row.fact.Validate(false); last.TryGetValue(row.fact.runtimeId, out var previous);
            ValidateSuccessor(previous?.fact, row.fact);
            if (row.fact.sequence != (previous == null ? 1 : checked(previous.fact.sequence + 1))
                || row.fact.parentPublicationKey != (previous?.publicationKey ?? string.Empty)
                || !keys.Add(row.publicationKey) || !Text(row.publicationKey)
                || !(row.pending ? row.publicationId == string.Empty && row.fingerprint == string.Empty : Text(row.publicationId) && Hash(row.fingerprint))
                || previous?.pending == true
                || row.fact.stage == InvasionLifecycleStage.Breached && !breaches.Add(row.fact.runtimeId)
                || row.fact.stage == InvasionLifecycleStage.FacilityDamaged && !facilities.Add(row.fact.runtimeId + "\n" + row.fact.targetId))
                throw new ArgumentException("invasion-publication-history-or-proof-invalid");
            last[row.fact.runtimeId] = row; result.observations.Add(row.Clone());
        }
        return result;
    }
    private static void ValidateSuccessor(InvasionLifecycleFactData previous, InvasionLifecycleFactData next)
    {
        if (previous == null ? next.stage != InvasionLifecycleStage.Started
            : next.stage == InvasionLifecycleStage.Started || previous.stage == InvasionLifecycleStage.Resolved
                || previous.intruderId != next.intruderId || previous.damageIndexAfter != next.damageIndexBefore
                || next.absoluteDay < previous.absoluteDay)
            throw new ArgumentException("invasion-publication-stage-order-invalid");
    }
    private static bool Text(string value) => DomainFactValueSyntax.IsText(value, 2048, false);
    private static bool Hash(string value) => value?.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception error) => error is not OutOfMemoryException && error is not StackOverflowException && error is not AccessViolationException;
    public void RequireIdlePersistence() { if (IsBusy) throw new InvalidOperationException("invasion-publication-active"); }
    public void RequireIdleMutation() { RequireIdlePersistence(); if (IsRestoring) throw new InvalidOperationException("invasion-publication-restoring"); }
    public void Restore(InvasionFactPublicationSaveData source)
    {
        RequireIdlePersistence(); var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var before = state; return () => state = before; })) state = candidate;
    }
    public string ParticipantId => InvasionFactPublicationSaveSection.Id;
    public void BeginRestoreCandidate() { RequireIdleMutation(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
