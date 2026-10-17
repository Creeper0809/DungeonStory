using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class DefenseServicePublicationData
{
    public DefenseServiceFactData fact;
    public string publicationKey = string.Empty, publicationId = string.Empty, fingerprint = string.Empty;
    public bool pending;
    public string OperationId => fact?.operationId;
    public DefenseServicePublicationData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey,
        publicationId = publicationId, fingerprint = fingerprint, pending = pending };
}
[Serializable]
public sealed class DefenseServicePublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<DefenseServicePublicationData> observations = new();
}

/// <summary>Durable service transitions. Recovery only republishes facts; it never consumes parts or refills a facility.</summary>
public sealed class DefenseServicePublicationRuntime : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactStagingPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private DefenseServicePublicationSaveData state = new();
    private Preparation active;
    private Func<bool> rollback;
    private bool preparing, recovering;
    public bool IsRestoring { get; private set; }
    public bool IsBusy => preparing || recovering || active != null || rollback != null;
    public string LastFailure { get; private set; } = string.Empty;
    public DefenseServicePublicationRuntime(IDomainFactStagingPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool HasOperation(string operation) => state.observations.Any(row => row.OperationId == operation);
    public bool TryPrepare(DefenseServiceChangedFact fact, out Preparation prepared, out string failure) =>
        TryPrepareCore(fact, new DefenseServicePublicationData { fact = fact.Snapshot(), pending = true }, out prepared, out failure);

    public bool TryEnsurePublished(DefenseServiceChangedFact fact, out string failure)
    {
        RequireIdleMutation();
        var data = fact.Snapshot();
        var row = state.observations.SingleOrDefault(value => value.OperationId == data.operationId);
        if (row == null || !row.fact.SameAs(data))
        { failure = LastFailure = "defense-service-applied-fact-missing-or-conflicting"; return false; }
        if (!row.pending) { failure = string.Empty; return true; }
        return RecoverPending(out failure) && !row.pending;
    }
    private bool TryPrepareCore<T>(T fact, DefenseServicePublicationData row, out Preparation prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        if (IsBusy || IsRestoring) { failure = "defense-service-publication-busy"; return false; }
        preparing = true;
        IDomainFactStage stage = null;
        try
        {
            if (HasOperation(row.OperationId)) { failure = "defense-service-fact-already-applied"; return false; }
            RequireRequest(row, state.observations);
            if (!publisher.TryStage(fact, out stage, out failure)) return false;
            if (stage == null || !stage.IsActive || stage.IsReplay
                || !DomainFactValueSyntax.IsText(stage.OperationKey, 1024, false))
            { stage?.Cancel(); failure = "defense-service-stage-invalid-or-unowned-replay"; return false; }
            row.publicationKey = stage.OperationKey;
            prepared = new Preparation(this, row, stage);
            active = prepared; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            stage?.Cancel();
            LastFailure = failure = "defense-service-stage:" + exception.Message; return false;
        }
        finally { preparing = false; if (prepared == null) LastFailure = failure; }
    }
    public sealed class Preparation : IDisposable
    {
        private readonly DefenseServicePublicationRuntime owner;
        internal readonly DefenseServicePublicationData Row;
        internal readonly IDomainFactStage Stage;
        internal Func<bool> Rollback;
        internal bool Applied, Disposed, Attempted;
        internal Preparation(DefenseServicePublicationRuntime owner, DefenseServicePublicationData row, IDomainFactStage stage)
        { this.owner = owner; Row = row; Stage = stage; }
        public string PublicationKey => Row.publicationKey;
        public void SetRollback(Func<bool> action)
        {
            if (action == null || Rollback != null || Disposed || Attempted)
                throw new InvalidOperationException("defense-service-rollback-contract-invalid");
            Rollback = action;
        }
        public DomainFactDelivery PublishApplied() => owner.PublishApplied(this);
        public void Dispose() => owner.End(this);
    }
    private DomainFactDelivery PublishApplied(Preparation prepared)
    {
        if (!ReferenceEquals(active, prepared) || prepared.Disposed || prepared.Attempted)
            throw new InvalidOperationException("defense-service-publication-contract-invalid");
        prepared.Attempted = true;
        // Exact after-image is durable before observer commit; a save cannot interleave this active scope.
        state.observations.Add(prepared.Row);
        DomainFactDelivery delivery;
        try
        {
            delivery = prepared.Stage.Commit();
        }
        catch (Exception exception) when (Recoverable(exception))
        { delivery = DomainFactDelivery.Uncertain("defense-service-commit-response-unknown:" + exception.Message); }
        if (!delivery.MayBeCommitted)
        {
            state.observations.Remove(prepared.Row);
            LastFailure = "defense-service-observation-rejected:" + delivery.Detail;
            return delivery; // Dispose rolls the gameplay owner back, including its operation marker.
        }
        prepared.Applied = true;
        if (Accept(prepared.Row, delivery)) { LastFailure = string.Empty; return delivery; }
        LastFailure = "defense-service-observation-pending:" + delivery.Detail;
        return DomainFactDelivery.Uncertain(LastFailure);
    }
    private void End(Preparation prepared)
    {
        if (prepared.Disposed) return;
        if (!ReferenceEquals(active, prepared)) throw new InvalidOperationException("defense-service-source-not-active");
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
            { LastFailure = "defense-service-stage-release-failed:" + exception.Message; }
            finally { active = null; }
        }
    }
    private bool RetryRollback()
    {
        if (rollback == null) return true;
        try { if (rollback()) { rollback = null; return true; } }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = "defense-service-rollback-pending:" + exception.Message; return false; }
        LastFailure = "defense-service-rollback-pending"; return false;
    }
    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (IsRestoring || preparing || active != null || recovering)
        { failure = "defense-service-recovery-busy"; return false; }
        recovering = true;
        try
        {
            if (!RetryRollback()) { failure = LastFailure; return false; }
            foreach (var row in state.observations.Where(row => row.pending))
            {
                var results = new DomainFactDelivery[1];
                DomainFactReplay replay = DomainFactReplay.Create(row.publicationKey, new DefenseServiceChangedFact(row.fact));
                if (!recovery.ReplayBatch(new[] { replay }, results, out failure) || !Accept(row, results[0]))
                { LastFailure = failure = "defense-service-replay-pending:" + failure; return false; }
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = failure = "defense-service-recovery:" + exception.Message; return false; }
        finally { recovering = false; }
    }
    public void Tick()
    {
        if (IsRestoring || preparing || active != null || recovering || retryClock.ElapsedMilliseconds < 1000
            || rollback == null && !state.observations.Any(row => row.pending)) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);
    private static bool Accept(DefenseServicePublicationData row, DomainFactDelivery delivery)
    {
        if (!delivery.Completed || delivery.OperationKey != row.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 1024, false) || !Hash(delivery.Fingerprint)) return false;
        row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint; row.pending = false; return true;
    }
    public DefenseServicePublicationSaveData Capture()
    { RequireIdleMutation(); return ValidateAndClone(state); }
    public static DefenseServicePublicationSaveData ValidateAndClone(DefenseServicePublicationSaveData source)
    {
        if (source == null || source.version != DefenseServicePublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("defense-service-publication-save-invalid");
        var result = new DefenseServicePublicationSaveData();
        var operations = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.observations)
        {
            if (row?.fact == null) throw new ArgumentException("defense-service-fact-missing");
            row.fact.Validate();
            if (!operations.Add(row.OperationId) || !keys.Add(row.publicationKey)
                || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false)
                || (row.pending ? row.publicationId != string.Empty || row.fingerprint != string.Empty
                    : !DomainFactValueSyntax.IsText(row.publicationId, 1024, false) || !Hash(row.fingerprint)))
                throw new ArgumentException("defense-service-publication-proof-invalid");
            RequireRequest(row, result.observations);
            result.observations.Add(row.Clone());
        }
        return result;
    }
    private static void RequireRequest(DefenseServicePublicationData row, IEnumerable<DefenseServicePublicationData> earlier)
    {
        var data = row.fact;
        if (data.phase == DefenseFacilityServiceOutcomePhase.Requested) return;
        var same = earlier.Where(value => value.fact.facilityId == data.facilityId
            && value.fact.operationSequence == data.operationSequence).ToArray();
        var request = same.SingleOrDefault(value => value.fact.phase == DefenseFacilityServiceOutcomePhase.Requested);
        if (request == null || same.Length != 1 || request.fact.revision + 1 != data.revision
            || request.fact.kind != data.kind || request.fact.absoluteDay != data.absoluteDay
            || request.fact.beforeState != data.beforeState || request.fact.beforeSupply != data.beforeSupply
            || request.pending)
            throw new ArgumentException("defense-service-request-fact-conflict");
    }
    public void Restore(DefenseServicePublicationSaveData source)
    {
        RequireIdlePersistence();
        var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var before = state; return () => state = before; })) state = candidate;
    }
    public void RequireIdlePersistence() { if (IsBusy) throw new InvalidOperationException("defense-service-publication-active"); }
    public void RequireIdleMutation()
    { RequireIdlePersistence(); if (IsRestoring) throw new InvalidOperationException("defense-service-publication-restoring"); }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    public string ParticipantId => DefenseServicePublicationSaveSection.Id;
    public void BeginRestoreCandidate() { RequireIdleMutation(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
