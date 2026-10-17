using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class DefenseActivationPublicationData
{
    public DefenseActivationFactData activation;
    public string publicationKey = string.Empty, publicationId = string.Empty, fingerprint = string.Empty;
    public bool pending;
    public DefenseEffectFactData effect;
    public string effectPublicationKey = string.Empty, effectPublicationId = string.Empty, effectFingerprint = string.Empty;
    public bool effectPending;
    public string effectFailure = string.Empty;
    public DefenseActivationPublicationData Clone() => new()
    {
        activation = activation?.Clone(), publicationKey = publicationKey, publicationId = publicationId,
        fingerprint = fingerprint, pending = pending, effect = effect?.Clone(), effectPublicationKey = effectPublicationKey,
        effectPublicationId = effectPublicationId, effectFingerprint = effectFingerprint, effectPending = effectPending,
        effectFailure = effectFailure
    };
}
[Serializable]
public sealed class DefenseActivationPublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<DefenseActivationPublicationData> observations = new();
}

/// <summary>Frozen activation/effect delivery. Recovery cannot activate traps or apply damage.</summary>
public sealed class DefenseActivationPublicationRuntime : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactStagingPublisher staging;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private DefenseActivationPublicationSaveData state = new();
    private Preparation active;
    private Func<bool> cleanup;
    private bool preparing, recovering;
    public bool IsRestoring { get; private set; }
    public bool IsBusy => active != null || preparing || recovering || cleanup != null;
    public string LastFailure { get; private set; } = string.Empty;
    public DefenseActivationPublicationRuntime(IDomainFactPublisher publisher, IDomainFactStagingPublisher staging,
        IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.staging = staging ?? throw new ArgumentNullException(nameof(staging));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool TryBegin(DefenseActivationFact proposal, out Preparation prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        if (IsBusy || IsRestoring) { failure = "defense-activation-publication-busy"; return false; }
        preparing = true; DomainFactLease lease = default;
        try
        {
            if (proposal?.IsProposal != true) throw new ArgumentException("defense-activation-proposal-required");
            var data = proposal.Snapshot();
            int prior = state.observations.Count(row => row.activation.facilityId == data.facilityId);
            if (data.activationCount != checked(prior + 1))
            { failure = "defense-activation-owner-revision-conflict"; return false; }
            if (!publisher.TryReserve(proposal, out lease, out failure)) return false;
            if (!lease.IsValid || !Text(lease.PublicationKey))
            { publisher.Cancel(lease); failure = "defense-activation-reservation-invalid"; return false; }
            active = prepared = new Preparation(this, proposal, lease); return true;
        }
        catch (Exception error) when (Recoverable(error))
        { publisher.Cancel(lease); failure = "defense-activation-reserve:" + error.Message; return false; }
        finally { preparing = false; if (prepared == null) LastFailure = failure; }
    }
    public sealed class Preparation : IDisposable
    {
        private readonly DefenseActivationPublicationRuntime owner;
        internal readonly DefenseActivationFact Proposal;
        internal readonly DomainFactLease Lease;
        internal DefenseActivationPublicationData Row;
        internal IDomainFactStage EffectStage;
        internal Action RollbackActivation, RollbackEffect, FinalizeEffect;
        internal bool ActivationAttempted, ActivationApplied, EffectAttempted, EffectApplied, Disposed;
        internal Preparation(DefenseActivationPublicationRuntime owner, DefenseActivationFact proposal, DomainFactLease lease)
        { this.owner = owner; Proposal = proposal; Lease = lease; }
        public void SetActivationRollback(Action action)
        {
            if (Disposed || ActivationAttempted || RollbackActivation != null || action == null)
                throw new InvalidOperationException("defense-activation-rollback-contract");
            RollbackActivation = action;
        }
        public void SetEffectActions(Action rollback, Action complete)
        {
            if (Disposed || !ActivationApplied || EffectAttempted || RollbackEffect != null || rollback == null || complete == null)
                throw new InvalidOperationException("defense-effect-cleanup-contract");
            RollbackEffect = rollback; FinalizeEffect = complete;
        }
        public DomainFactDelivery PublishActivation(DefenseActivationFact fact) => owner.PublishActivation(this, fact);
        public DomainFactDelivery PublishEffect(DefenseEffectFact fact) => owner.PublishEffect(this, fact);
        public void RecordEffectFailure(string failure)
        {
            if (Disposed || !ActivationApplied || EffectApplied || !Text(failure))
                throw new InvalidOperationException("defense-effect-failure-contract");
            Row.effectFailure = failure; owner.LastFailure = failure;
        }
        public void Dispose() => owner.End(this);
    }
    private DomainFactDelivery PublishActivation(Preparation prepared, DefenseActivationFact fact)
    {
        RequireActive(prepared);
        if (prepared.ActivationAttempted || fact?.IsProposal != false || !fact.MatchesProposal(prepared.Proposal))
            throw new InvalidOperationException("defense-activation-applied-fact-conflict");
        prepared.ActivationAttempted = true;
        prepared.Row = new DefenseActivationPublicationData
        { activation = fact.Snapshot(), publicationKey = prepared.Lease.PublicationKey, pending = true,
            effectFailure = "effect-not-applied" };
        state.observations.Add(prepared.Row);
        DomainFactDelivery delivered;
        try { delivered = publisher.Publish(prepared.Lease, fact); }
        catch (Exception error) when (Recoverable(error))
        { delivered = DomainFactDelivery.Uncertain("defense-activation-response-unknown:" + error.Message); }
        if (!delivered.MayBeCommitted)
        { state.observations.Remove(prepared.Row); LastFailure = delivered.Detail; return delivered; }
        prepared.ActivationApplied = true;
        if (Accept(prepared.Row, delivered, false)) { LastFailure = string.Empty; return delivered; }
        LastFailure = "defense-activation-delivery-pending:" + delivered.Detail;
        return DomainFactDelivery.Uncertain(LastFailure);
    }
    private DomainFactDelivery PublishEffect(Preparation prepared, DefenseEffectFact fact)
    {
        RequireActive(prepared);
        if (!prepared.ActivationApplied || prepared.EffectAttempted || prepared.RollbackEffect == null
            || fact == null || !fact.Snapshot().activation.SameAs(prepared.Row.activation))
            throw new InvalidOperationException("defense-effect-applied-fact-conflict");
        prepared.EffectAttempted = true;
        var row = prepared.Row;
        try
        {
            if (!staging.TryStage(fact, out prepared.EffectStage, out string failure))
            { prepared.RecordEffectFailure("defense-effect-stage:" + failure); return DomainFactDelivery.Reject(LastFailure); }
            if (prepared.EffectStage == null || !prepared.EffectStage.IsActive || prepared.EffectStage.IsReplay
                || !Text(prepared.EffectStage.OperationKey))
            { prepared.RecordEffectFailure("defense-effect-stage-invalid-or-unowned-replay"); return DomainFactDelivery.Reject(LastFailure); }
        }
        catch (Exception error) when (Recoverable(error))
        { prepared.RecordEffectFailure("defense-effect-stage:" + error.Message); return DomainFactDelivery.Reject(LastFailure); }
        row.effect = fact.Snapshot(); row.effectPublicationKey = prepared.EffectStage.OperationKey;
        row.effectPending = true; row.effectFailure = string.Empty;
        DomainFactDelivery delivered;
        try { delivered = prepared.EffectStage.Commit(); }
        catch (Exception error) when (Recoverable(error))
        { delivered = DomainFactDelivery.Uncertain("defense-effect-response-unknown:" + error.Message); }
        if (!delivered.MayBeCommitted)
        {
            row.effect = null; row.effectPending = false; row.effectPublicationKey = string.Empty;
            prepared.RecordEffectFailure("defense-effect-observation-rejected:" + delivered.Detail);
            return delivered;
        }
        prepared.EffectApplied = true;
        if (Accept(row, delivered, true)) { LastFailure = string.Empty; return delivered; }
        LastFailure = "defense-effect-delivery-pending:" + delivered.Detail;
        return DomainFactDelivery.Uncertain(LastFailure);
    }
    private void RequireActive(Preparation prepared)
    {
        if (!ReferenceEquals(active, prepared) || prepared.Disposed)
            throw new InvalidOperationException("defense-activation-publication-not-active");
    }
    private void End(Preparation prepared)
    {
        if (prepared.Disposed) return;
        RequireActive(prepared); prepared.Disposed = true;
        cleanup = () =>
        {
            if (!prepared.EffectApplied && prepared.RollbackEffect != null)
            { prepared.RollbackEffect(); prepared.RollbackEffect = null; }
            if (!prepared.ActivationApplied && prepared.RollbackActivation != null)
            { prepared.RollbackActivation(); prepared.RollbackActivation = null; }
            if (prepared.EffectApplied && prepared.FinalizeEffect != null)
            { prepared.FinalizeEffect(); prepared.FinalizeEffect = null; }
            return true;
        };
        try { RetryCleanup(); }
        finally
        {
            try { prepared.EffectStage?.Cancel(); publisher.Cancel(prepared.Lease); }
            catch (Exception error) when (Recoverable(error)) { LastFailure = "defense-activation-release:" + error.Message; }
            finally { active = null; }
        }
    }
    private bool RetryCleanup()
    {
        if (cleanup == null) return true;
        try { if (cleanup()) { cleanup = null; return true; } }
        catch (Exception error) when (Recoverable(error)) { LastFailure = "defense-activation-cleanup-pending:" + error.Message; }
        return false;
    }
    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (IsRestoring || preparing || active != null || recovering)
        { failure = "defense-activation-recovery-busy"; return false; }
        recovering = true;
        try
        {
            if (!RetryCleanup()) { failure = LastFailure; return false; }
            foreach (var row in state.observations)
            {
                if (row.pending && !Replay(row, false, out failure)) return false;
                if (row.effectPending && !Replay(row, true, out failure)) return false;
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception error) when (Recoverable(error))
        { LastFailure = failure = "defense-activation-recovery:" + error.Message; return false; }
        finally { recovering = false; }
    }
    private bool Replay(DefenseActivationPublicationData row, bool effect, out string failure)
    {
        var request = effect ? DomainFactReplay.Create(row.effectPublicationKey, new DefenseEffectFact(row.effect))
            : DomainFactReplay.Create(row.publicationKey, new DefenseActivationFact(row.activation));
        var results = new DomainFactDelivery[1];
        if (!recovery.ReplayBatch(new[] { request }, results, out failure) || !Accept(row, results[0], effect))
        { LastFailure = failure = "defense-activation-replay-pending:" + failure; return false; }
        return true;
    }
    private static bool Accept(DefenseActivationPublicationData row, DomainFactDelivery delivery, bool effect)
    {
        if (!delivery.Completed || delivery.OperationKey != (effect ? row.effectPublicationKey : row.publicationKey)
            || !Text(delivery.PublicationId) || !Hash(delivery.Fingerprint)) return false;
        if (effect) { row.effectPublicationId = delivery.PublicationId; row.effectFingerprint = delivery.Fingerprint; row.effectPending = false; }
        else { row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint; row.pending = false; }
        return true;
    }
    public void Tick()
    {
        if (IsRestoring || preparing || active != null || recovering || retryClock.ElapsedMilliseconds < 1000
            || cleanup == null && !state.observations.Any(row => row.pending || row.effectPending)) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);
    public DefenseActivationPublicationSaveData Capture() { RequireIdleMutation(); return ValidateAndClone(state); }
    public static DefenseActivationPublicationSaveData ValidateAndClone(DefenseActivationPublicationSaveData source)
    {
        if (source?.version != DefenseActivationPublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("defense-activation-save-invalid");
        var result = new DefenseActivationPublicationSaveData(); var revisions = new Dictionary<string, int>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.observations)
        {
            if (row?.activation == null) throw new ArgumentException("defense-activation-history-missing");
            row.activation.Validate(); revisions.TryGetValue(row.activation.facilityId, out int before);
            if (row.activation.activationCount != checked(before + 1) || !keys.Add(row.publicationKey)
                || !Proof(row.publicationKey, row.publicationId, row.fingerprint, row.pending))
                throw new ArgumentException("defense-activation-history-or-proof-invalid");
            revisions[row.activation.facilityId] = row.activation.activationCount;
            if (row.effect == null)
            {
                if (row.effectPending || row.effectPublicationKey != string.Empty || row.effectPublicationId != string.Empty
                    || row.effectFingerprint != string.Empty || !Text(row.effectFailure))
                    throw new ArgumentException("defense-effect-absent-state-invalid");
            }
            else
            {
                row.effect.Validate();
                if (!row.effect.activation.SameAs(row.activation) || row.effectFailure != string.Empty
                    || !keys.Add(row.effectPublicationKey)
                    || !Proof(row.effectPublicationKey, row.effectPublicationId, row.effectFingerprint, row.effectPending))
                    throw new ArgumentException("defense-effect-history-or-proof-invalid");
            }
            result.observations.Add(row.Clone());
        }
        return result;
    }
    private static bool Proof(string key, string id, string hash, bool pending) => Text(key)
        && (pending ? id == string.Empty && hash == string.Empty : Text(id) && Hash(hash));
    private static bool Text(string value) => DomainFactValueSyntax.IsText(value, 2048, false);
    private static bool Hash(string value) => value?.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception error) => error is not OutOfMemoryException && error is not StackOverflowException && error is not AccessViolationException;
    public void RequireIdlePersistence() { if (IsBusy) throw new InvalidOperationException("defense-activation-publication-active"); }
    public void RequireIdleMutation() { RequireIdlePersistence(); if (IsRestoring) throw new InvalidOperationException("defense-activation-publication-restoring"); }
    public void Restore(DefenseActivationPublicationSaveData source)
    {
        RequireIdlePersistence(); var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var before = state; return () => state = before; })) state = candidate;
    }
    public string ParticipantId => DefenseActivationPublicationSaveSection.Id;
    public void BeginRestoreCandidate() { RequireIdleMutation(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
