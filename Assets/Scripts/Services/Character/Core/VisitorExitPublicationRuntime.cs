using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class VisitorExitPublicationData
{
    public FloorItemStolenFactData theft;
    public VisitorDepartureFactData departure;
    public string publicationKey = string.Empty, publicationId = string.Empty, fingerprint = string.Empty;
    public bool pending;
    public string OperationId => theft?.operationId ?? departure?.operationId;
    public VisitorExitPublicationData Clone() => new()
    { theft = theft?.Clone(), departure = departure?.Clone(), publicationKey = publicationKey,
        publicationId = publicationId, fingerprint = fingerprint, pending = pending };
}
[Serializable]
public sealed class VisitorExitPublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<VisitorExitPublicationData> observations = new();
}

/// <summary>Durable observed facts outlive visiting actors. Recovery does not move items or release visitors.</summary>
public sealed class VisitorExitPublicationRuntime : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactStagingPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private VisitorExitPublicationSaveData state = new();
    private Preparation active;
    private Func<bool> rollback;
    private bool preparing, recovering;
    public bool IsRestoring { get; private set; }
    public bool IsBusy => preparing || recovering || active != null || rollback != null;
    public string LastFailure { get; private set; } = string.Empty;
    public VisitorExitPublicationRuntime(IDomainFactStagingPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool HasOperation(string operation) => state.observations.Any(row => row.OperationId == operation);
    public bool TryPrepare(FloorItemStolenFact fact, out Preparation prepared, out string failure) =>
        TryPrepareCore(fact, new VisitorExitPublicationData { theft = fact.Snapshot(), pending = true }, out prepared, out failure);
    public bool TryPrepare(VisitorDepartureFact fact, out Preparation prepared, out string failure) =>
        TryPrepareCore(fact, new VisitorExitPublicationData { departure = fact.Snapshot(), pending = true }, out prepared, out failure);
    private bool TryPrepareCore<T>(T fact, VisitorExitPublicationData row, out Preparation prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        if (IsBusy || IsRestoring) { failure = "visitor-exit-publication-busy"; return false; }
        preparing = true;
        IDomainFactStage stage = null;
        try
        {
            if (HasOperation(row.OperationId)) { failure = "visitor-exit-fact-already-applied"; return false; }
            RequireChild(row, state.observations);
            if (!publisher.TryStage(fact, out stage, out failure)) return false;
            if (stage == null || !stage.IsActive || stage.IsReplay
                || !DomainFactValueSyntax.IsText(stage.OperationKey, 1024, false))
            { stage?.Cancel(); failure = "visitor-exit-stage-invalid-or-unowned-replay"; return false; }
            row.publicationKey = stage.OperationKey;
            prepared = new Preparation(this, row, stage);
            active = prepared; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            stage?.Cancel();
            LastFailure = failure = "visitor-exit-stage:" + exception.Message; return false;
        }
        finally { preparing = false; }
    }
    public sealed class Preparation : IDisposable
    {
        private readonly VisitorExitPublicationRuntime owner;
        internal readonly VisitorExitPublicationData Row;
        internal readonly IDomainFactStage Stage;
        internal Func<bool> Rollback;
        internal bool Applied, Disposed, Attempted;
        internal Preparation(VisitorExitPublicationRuntime owner, VisitorExitPublicationData row, IDomainFactStage stage)
        { this.owner = owner; Row = row; Stage = stage; }
        public string PublicationKey => Row.publicationKey;
        public void SetRollback(Func<bool> action)
        {
            if (action == null || Rollback != null || Disposed || Attempted)
                throw new InvalidOperationException("visitor-exit-rollback-contract-invalid");
            Rollback = action;
        }
        public DomainFactDelivery PublishApplied() => owner.PublishApplied(this);
        public void Dispose() => owner.End(this);
    }
    private DomainFactDelivery PublishApplied(Preparation prepared)
    {
        if (!ReferenceEquals(active, prepared) || prepared.Disposed || prepared.Attempted)
            throw new InvalidOperationException("visitor-exit-publication-contract-invalid");
        prepared.Attempted = true;
        // Exact after-image is durable before observer commit; a save cannot interleave this active scope.
        state.observations.Add(prepared.Row);
        DomainFactDelivery delivery;
        try
        {
            bool childPending = ChildPending(prepared.Row);
            delivery = childPending ? DomainFactDelivery.Uncertain("visitor-exit-awaiting-theft-observation")
                : prepared.Stage.Commit();
        }
        catch (Exception exception) when (Recoverable(exception))
        { delivery = DomainFactDelivery.Uncertain("visitor-exit-commit-response-unknown:" + exception.Message); }
        if (!delivery.MayBeCommitted)
        {
            state.observations.Remove(prepared.Row);
            LastFailure = "visitor-exit-observation-rejected:" + delivery.Detail;
            return delivery; // Dispose rolls the gameplay owner back, including its operation marker.
        }
        prepared.Applied = true;
        if (Accept(prepared.Row, delivery)) { LastFailure = string.Empty; return delivery; }
        LastFailure = "visitor-exit-observation-pending:" + delivery.Detail;
        return DomainFactDelivery.Uncertain(LastFailure);
    }
    private bool ChildPending(VisitorExitPublicationData row) => row.departure != null
        && row.departure.theftPublicationKey.Length != 0
        && state.observations.Single(value => value.publicationKey == row.departure.theftPublicationKey).pending;
    private void End(Preparation prepared)
    {
        if (prepared.Disposed) return;
        if (!ReferenceEquals(active, prepared)) throw new InvalidOperationException("visitor-exit-source-not-active");
        prepared.Disposed = true;
        try
        {
            if (!prepared.Applied && prepared.Rollback != null)
            { rollback = prepared.Rollback; RetryRollback(); }
        }
        finally
        {
            try { prepared.Stage.Cancel(); }
            catch (Exception exception) when (Recoverable(exception))
            { LastFailure = "visitor-exit-stage-release-failed:" + exception.Message; }
            finally { active = null; }
        }
    }
    private bool RetryRollback()
    {
        if (rollback == null) return true;
        try { if (rollback()) { rollback = null; return true; } }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = "visitor-exit-rollback-pending:" + exception.Message; return false; }
        LastFailure = "visitor-exit-rollback-pending"; return false;
    }
    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (IsRestoring || preparing || active != null || recovering)
        { failure = "visitor-exit-recovery-busy"; return false; }
        recovering = true;
        try
        {
            if (!RetryRollback()) { failure = LastFailure; return false; }
            foreach (var row in state.observations.Where(row => row.pending))
            {
                if (ChildPending(row)) { failure = "visitor-exit-child-observation-pending"; return false; }
                var results = new DomainFactDelivery[1];
                DomainFactReplay replay = row.theft != null
                    ? DomainFactReplay.Create(row.publicationKey, new FloorItemStolenFact(row.theft))
                    : DomainFactReplay.Create(row.publicationKey, new VisitorDepartureFact(row.departure));
                if (!recovery.ReplayBatch(new[] { replay }, results, out failure) || !Accept(row, results[0]))
                { LastFailure = failure = "visitor-exit-replay-pending:" + failure; return false; }
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = failure = "visitor-exit-recovery:" + exception.Message; return false; }
        finally { recovering = false; }
    }
    public void Tick()
    {
        if (IsRestoring || preparing || active != null || recovering || retryClock.ElapsedMilliseconds < 1000
            || rollback == null && !state.observations.Any(row => row.pending)) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);
    private static bool Accept(VisitorExitPublicationData row, DomainFactDelivery delivery)
    {
        if (!delivery.Completed || delivery.OperationKey != row.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 1024, false) || !Hash(delivery.Fingerprint)) return false;
        row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint; row.pending = false; return true;
    }
    public VisitorExitPublicationSaveData Capture()
    { RequireIdleMutation(); return ValidateAndClone(state); }
    public static VisitorExitPublicationSaveData ValidateAndClone(VisitorExitPublicationSaveData source)
    {
        if (source == null || source.version != VisitorExitPublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("visitor-exit-publication-save-invalid");
        var result = new VisitorExitPublicationSaveData();
        var operations = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.observations)
        {
            if (row == null || (row.theft == null) == (row.departure == null))
                throw new ArgumentException("visitor-exit-fact-union-invalid");
            row.theft?.Validate(); row.departure?.Validate();
            if (!operations.Add(row.OperationId) || !keys.Add(row.publicationKey)
                || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false)
                || (row.pending ? row.publicationId != string.Empty || row.fingerprint != string.Empty
                    : !DomainFactValueSyntax.IsText(row.publicationId, 1024, false) || !Hash(row.fingerprint)))
                throw new ArgumentException("visitor-exit-publication-proof-invalid");
            RequireChild(row, result.observations);
            result.observations.Add(row.Clone());
        }
        return result;
    }
    private static void RequireChild(VisitorExitPublicationData row, IEnumerable<VisitorExitPublicationData> earlier)
    {
        var parent = row.departure;
        if (parent == null || parent.theftPublicationKey.Length == 0) return;
        var child = earlier.SingleOrDefault(value => value.publicationKey == parent.theftPublicationKey);
        if (child?.theft == null || child.theft.parentOperationId != parent.operationId
            || child.theft.ownerRevision != parent.ownerRevision || child.theft.characterId != parent.characterId
            || child.theft.absoluteDay != parent.absoluteDay || !row.pending && child.pending)
            throw new ArgumentException("visitor-exit-child-fact-conflict");
    }
    public void Restore(VisitorExitPublicationSaveData source)
    {
        RequireIdlePersistence();
        var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var before = state; return () => state = before; })) state = candidate;
    }
    public void RequireIdlePersistence() { if (IsBusy) throw new InvalidOperationException("visitor-exit-publication-active"); }
    public void RequireIdleMutation()
    { RequireIdlePersistence(); if (IsRestoring) throw new InvalidOperationException("visitor-exit-publication-restoring"); }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    public string ParticipantId => VisitorExitPublicationSaveSection.Id;
    public void BeginRestoreCandidate() { RequireIdleMutation(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
