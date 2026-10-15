using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class SocialInteractionPublicationData
{
    public SocialInteractionFactData fact;
    public string publicationKey = string.Empty, publicationId = string.Empty, fingerprint = string.Empty;
    public bool pending;
    public string OperationId => fact?.operationId;
    public SocialInteractionPublicationData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, publicationId = publicationId,
        fingerprint = fingerprint, pending = pending };
}
[Serializable]
public sealed class SocialInteractionPublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<SocialInteractionPublicationData> observations = new();
}

/// <summary>Durable social facts survive actor lifetime. Recovery never changes relationships or mood.</summary>
public sealed class SocialInteractionPublicationRuntime : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private SocialInteractionPublicationSaveData state = new();
    private Preparation active;
    private Func<bool> rollback;
    private bool preparing, recovering;
    public bool IsRestoring { get; private set; }
    public bool IsBusy => preparing || recovering || active != null || rollback != null;
    public string LastFailure { get; private set; } = string.Empty;
    public SocialInteractionPublicationRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool HasOperation(string operation) => state.observations.Any(row => row.OperationId == operation);
    public bool TryPrepare(SocialInteractionFact proposed, out Preparation prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        if (IsBusy || IsRestoring) { failure = "social-interaction-publication-busy"; return false; }
        preparing = true;
        DomainFactLease lease = default;
        try
        {
            if (proposed?.IsProposal != true) throw new ArgumentException("social-interaction-requires-proposal");
            var data = proposed.Snapshot();
            if (HasOperation(data.operationId)) { failure = "social-interaction-fact-already-applied"; return false; }
            bool reserved = data.kind == SocialInteractionKind.Apology
                ? publisher.TryReserve(new SocialApologyFact(data, true), out lease, out failure)
                : publisher.TryReserve(new SocialConflictFact(data, true), out lease, out failure);
            if (!reserved) return false;
            if (!lease.IsValid || !DomainFactValueSyntax.IsText(lease.PublicationKey, 1024, false))
                throw new InvalidOperationException("social-interaction-reserved-key-invalid");
            var row = new SocialInteractionPublicationData { fact = data, publicationKey = lease.PublicationKey, pending = true };
            prepared = new Preparation(this, proposed, row, lease);
            active = prepared; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            if (lease.IsValid) publisher.Cancel(lease);
            LastFailure = failure = "social-interaction-reserve:" + exception.Message; return false;
        }
        finally { preparing = false; }
    }
    public sealed class Preparation : IDisposable
    {
        private readonly SocialInteractionPublicationRuntime owner;
        internal readonly SocialInteractionPublicationData Row;
        internal readonly DomainFactLease Lease;
        internal readonly SocialInteractionFact Proposed;
        internal Func<bool> Rollback;
        internal bool Applied, Disposed, Attempted;
        internal Preparation(SocialInteractionPublicationRuntime owner, SocialInteractionFact proposed, SocialInteractionPublicationData row, DomainFactLease lease)
        { this.owner = owner; Proposed = proposed; Row = row; Lease = lease; }
        public string PublicationKey => Row.publicationKey;
        public void SetRollback(Func<bool> action)
        {
            if (action == null || Rollback != null || Disposed || Attempted)
                throw new InvalidOperationException("social-interaction-rollback-contract-invalid");
            Rollback = action;
        }
        public DomainFactDelivery PublishApplied(float moodDelta = 0f) => owner.PublishApplied(this, moodDelta);
        public void Dispose() => owner.End(this);
    }
    private DomainFactDelivery PublishApplied(Preparation prepared, float moodDelta)
    {
        if (!ReferenceEquals(active, prepared) || prepared.Disposed || prepared.Attempted)
            throw new InvalidOperationException("social-interaction-publication-contract-invalid");
        prepared.Row.fact = prepared.Proposed.WithMoodDelta(moodDelta).Snapshot();
        prepared.Attempted = true;
        // Exact after-image is durable before observer commit; a save cannot interleave this active scope.
        state.observations.Add(prepared.Row);
        DomainFactDelivery delivery;
        try
        {
            delivery = prepared.Row.fact.kind == SocialInteractionKind.Apology
                ? publisher.Publish(prepared.Lease, new SocialApologyFact(prepared.Row.fact))
                : publisher.Publish(prepared.Lease, new SocialConflictFact(prepared.Row.fact));
        }
        catch (Exception exception) when (Recoverable(exception))
        { delivery = DomainFactDelivery.Uncertain("social-interaction-commit-response-unknown:" + exception.Message); }
        if (!delivery.MayBeCommitted)
        {
            state.observations.Remove(prepared.Row);
            LastFailure = "social-interaction-observation-rejected:" + delivery.Detail;
            return delivery; // Dispose rolls the gameplay owner back, including its operation marker.
        }
        prepared.Applied = true;
        if (Accept(prepared.Row, delivery)) { LastFailure = string.Empty; return delivery; }
        LastFailure = "social-interaction-observation-pending:" + delivery.Detail;
        return DomainFactDelivery.Uncertain(LastFailure);
    }
    private void End(Preparation prepared)
    {
        if (prepared.Disposed) return;
        if (!ReferenceEquals(active, prepared)) throw new InvalidOperationException("social-interaction-source-not-active");
        prepared.Disposed = true;
        try
        {
            if (!prepared.Applied && prepared.Rollback != null)
            { rollback = prepared.Rollback; RetryRollback(); }
        }
        finally
        {
            try { if (prepared.Lease.IsValid) publisher.Cancel(prepared.Lease); }
            catch (Exception exception) when (Recoverable(exception))
            { LastFailure = "social-interaction-stage-release-failed:" + exception.Message; }
            finally { active = null; }
        }
    }
    private bool RetryRollback()
    {
        if (rollback == null) return true;
        try { if (rollback()) { rollback = null; return true; } }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = "social-interaction-rollback-pending:" + exception.Message; return false; }
        LastFailure = "social-interaction-rollback-pending"; return false;
    }
    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (IsRestoring || preparing || active != null || recovering)
        { failure = "social-interaction-recovery-busy"; return false; }
        recovering = true;
        try
        {
            if (!RetryRollback()) { failure = LastFailure; return false; }
            foreach (var row in state.observations.Where(row => row.pending))
            {
                var results = new DomainFactDelivery[1];
                DomainFactReplay replay = row.fact.kind == SocialInteractionKind.Apology
                    ? DomainFactReplay.Create(row.publicationKey, new SocialApologyFact(row.fact))
                    : DomainFactReplay.Create(row.publicationKey, new SocialConflictFact(row.fact));
                if (!recovery.ReplayBatch(new[] { replay }, results, out failure) || !Accept(row, results[0]))
                { LastFailure = failure = "social-interaction-replay-pending:" + failure; return false; }
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = failure = "social-interaction-recovery:" + exception.Message; return false; }
        finally { recovering = false; }
    }
    public void Tick()
    {
        if (IsRestoring || preparing || active != null || recovering || retryClock.ElapsedMilliseconds < 1000
            || rollback == null && !state.observations.Any(row => row.pending)) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);
    private static bool Accept(SocialInteractionPublicationData row, DomainFactDelivery delivery)
    {
        if (!delivery.Completed || delivery.OperationKey != row.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 1024, false) || !Hash(delivery.Fingerprint)) return false;
        row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint; row.pending = false; return true;
    }
    public SocialInteractionPublicationSaveData Capture()
    { RequireIdleMutation(); return ValidateAndClone(state); }
    public static SocialInteractionPublicationSaveData ValidateAndClone(SocialInteractionPublicationSaveData source)
    {
        if (source == null || source.version != SocialInteractionPublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("social-interaction-publication-save-invalid");
        var result = new SocialInteractionPublicationSaveData();
        var operations = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.observations)
        {
            if (row?.fact == null)
                throw new ArgumentException("social-interaction-fact-union-invalid");
            row.fact.Validate();
            if (!operations.Add(row.OperationId) || !keys.Add(row.publicationKey)
                || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false)
                || (row.pending ? row.publicationId != string.Empty || row.fingerprint != string.Empty
                    : !DomainFactValueSyntax.IsText(row.publicationId, 1024, false) || !Hash(row.fingerprint)))
                throw new ArgumentException("social-interaction-publication-proof-invalid");
            result.observations.Add(row.Clone());
        }
        return result;
    }
    public void Restore(SocialInteractionPublicationSaveData source)
    {
        RequireIdlePersistence();
        var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var before = state; return () => state = before; })) state = candidate;
    }
    public void RequireIdlePersistence() { if (IsBusy) throw new InvalidOperationException("social-interaction-publication-active"); }
    public void RequireIdleMutation()
    { RequireIdlePersistence(); if (IsRestoring) throw new InvalidOperationException("social-interaction-publication-restoring"); }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    public string ParticipantId => SocialInteractionPublicationSaveSection.Id;
    public void BeginRestoreCandidate() { RequireIdleMutation(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
