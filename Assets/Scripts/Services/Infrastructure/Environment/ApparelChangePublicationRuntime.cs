using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class ApparelChangePublicationData
{
    public ApparelLoadoutChangedFactData fact;
    public string publicationKey = string.Empty, publicationId = string.Empty, fingerprint = string.Empty;
    public bool pending;
    public ApparelChangePublicationData Clone() => new()
    { fact = fact.Clone(), publicationKey = publicationKey, publicationId = publicationId,
        fingerprint = fingerprint, pending = pending };
}

[Serializable]
public sealed class ApparelChangePublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public long transitionRevision;
    public List<ApparelChangePublicationData> observations = new();
}

/// <summary>Durable actual-change sequence and observation cohorts. Replay never moves apparel.</summary>
public sealed class ApparelChangePublicationRuntime : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private ApparelChangePublicationSaveData state = new();
    private Preparation active;
    private Func<bool> rollback;
    private bool preparing, recovering;
    public bool IsRestoring { get; private set; }
    public bool IsBusy => preparing || active != null || rollback != null || recovering;
    public string LastFailure { get; private set; } = string.Empty;
    public long NextTransitionRevision => checked(state.transitionRevision + 1);

    public ApparelChangePublicationRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool TryPrepare(IReadOnlyList<ApparelLoadoutChangedFact> facts, out Preparation prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        if (IsBusy || IsRestoring || state.observations.Any(row => row.pending))
        { failure = "apparel-change-publication-busy"; return false; }
        var leases = new List<DomainFactLease>();
        preparing = true;
        try
        {
            if (facts == null || facts.Count == 0) throw new ArgumentException("apparel-change-cohort-empty");
            var rows = facts.Select(fact => new ApparelChangePublicationData { fact = fact.Snapshot(), pending = true }).ToArray();
            ValidateCohort(rows, NextTransitionRevision);
            foreach (var fact in facts)
            {
                if (!publisher.TryReserve(fact, out var lease, out failure))
                { foreach (var reserved in leases) publisher.Cancel(reserved); return false; }
                leases.Add(lease);
                rows[leases.Count - 1].publicationKey = lease.PublicationKey;
            }
            prepared = new Preparation(this, facts.ToArray(), leases.ToArray(), rows);
            active = prepared;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            foreach (var lease in leases) publisher.Cancel(lease);
            failure = "apparel-change-prepare:" + exception.Message;
            LastFailure = failure; return false;
        }
        finally { preparing = false; }
    }

    public sealed class Preparation : IDisposable
    {
        private readonly ApparelChangePublicationRuntime owner;
        internal readonly ApparelLoadoutChangedFact[] Facts;
        internal readonly DomainFactLease[] Leases;
        internal readonly ApparelChangePublicationData[] Rows;
        internal Func<bool> Rollback;
        internal bool Applied, Disposed;
        internal Preparation(ApparelChangePublicationRuntime owner, ApparelLoadoutChangedFact[] facts,
            DomainFactLease[] leases, ApparelChangePublicationData[] rows)
        { this.owner = owner; Facts = facts; Leases = leases; Rows = rows; }
        public void SetRollback(Func<bool> rollback)
        {
            if (Disposed || Applied || Rollback != null || rollback == null)
                throw new InvalidOperationException("apparel-change-rollback-contract-invalid");
            Rollback = rollback;
        }
        // Physical owner has already committed. A publication fault is retained, never a false gameplay rollback.
        public void PublishApplied() => owner.PublishApplied(this);
        public void Dispose() => owner.End(this);
    }

    private void PublishApplied(Preparation prepared)
    {
        if (!ReferenceEquals(active, prepared) || prepared.Applied || prepared.Disposed
            || prepared.Rows[0].fact.transitionRevision != NextTransitionRevision)
            throw new InvalidOperationException("apparel-change-publication-contract-invalid");
        state.observations.AddRange(prepared.Rows);
        state.transitionRevision = prepared.Rows[0].fact.transitionRevision;
        prepared.Applied = true;
        var results = new DomainFactDelivery[prepared.Rows.Length];
        try
        {
            var emissions = prepared.Facts.Select((fact, index) =>
                DomainFactEmission.Create(prepared.Leases[index], fact)).ToArray();
            bool accepted = publisher.PublishBatch(emissions, results, out string failure);
            LastFailure = accepted && AcceptCohort(prepared.Rows, results)
                ? string.Empty : "apparel-change-observation-pending:" + failure;
        }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = "apparel-change-observation-pending:" + exception.Message; }
        finally
        {
            foreach (var lease in prepared.Leases) if (lease.IsValid) publisher.Cancel(lease);
        }
    }

    private void End(Preparation prepared)
    {
        if (prepared.Disposed) return;
        if (!ReferenceEquals(active, prepared))
            throw new InvalidOperationException("apparel-change-source-not-active");
        prepared.Disposed = true;
        try
        {
            if (!prepared.Applied && prepared.Rollback != null)
            {
                rollback = prepared.Rollback;
                RetryRollback();
            }
        }
        finally
        {
            foreach (var lease in prepared.Leases) if (lease.IsValid) publisher.Cancel(lease);
            active = null;
        }
    }

    private bool RetryRollback()
    {
        if (rollback == null) return true;
        try { if (rollback()) { rollback = null; return true; } }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = "apparel-change-rollback-pending:" + exception.Message; return false; }
        LastFailure = "apparel-change-rollback-pending"; return false;
    }

    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (IsRestoring || preparing || active != null || recovering)
        { failure = "apparel-change-recovery-busy"; return false; }
        recovering = true;
        try
        {
            if (!RetryRollback()) { failure = LastFailure; return false; }
            foreach (var cohort in state.observations.Where(row => row.pending)
                .GroupBy(row => row.fact.transitionRevision).OrderBy(group => group.Key))
            {
                var rows = cohort.OrderBy(row => row.fact.changeIndex).ToArray();
                var results = new DomainFactDelivery[rows.Length];
                if (!recovery.ReplayBatch(rows.Select(row => DomainFactReplay.Create(row.publicationKey,
                    new ApparelLoadoutChangedFact(row.fact))).ToArray(), results, out failure)
                    || !AcceptCohort(rows, results))
                { LastFailure = failure = "apparel-change-recovery-pending:" + failure; return false; }
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = failure = "apparel-change-recovery:" + exception.Message; return false; }
        finally { recovering = false; }
    }
    public void Tick()
    {
        if (IsRestoring || preparing || active != null || recovering || retryClock.ElapsedMilliseconds < 1000
            || rollback == null && !state.observations.Any(row => row.pending)) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);
    private static bool AcceptCohort(ApparelChangePublicationData[] rows, DomainFactDelivery[] results)
    {
        if (results.Length != rows.Length) return false;
        for (int index = 0; index < rows.Length; index++)
            if (!results[index].Completed || results[index].OperationKey != rows[index].publicationKey
                || !DomainFactValueSyntax.IsText(results[index].PublicationId, 1024, false)
                || !Hash(results[index].Fingerprint)) return false;
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index].publicationId = results[index].PublicationId;
            rows[index].fingerprint = results[index].Fingerprint;
            rows[index].pending = false;
        }
        return true;
    }

    public ApparelChangePublicationSaveData Capture()
    { RequireIdleMutation(); return ValidateAndClone(state); }
    public static ApparelChangePublicationSaveData ValidateAndClone(ApparelChangePublicationSaveData source)
    {
        if (source == null || source.version != ApparelChangePublicationSaveData.CurrentVersion
            || source.transitionRevision < 0 || source.observations == null || source.observations.Any(row => row?.fact == null))
            throw new ArgumentException("apparel-change-publication-save-invalid");
        var result = new ApparelChangePublicationSaveData { transitionRevision = source.transitionRevision };
        var keys = new HashSet<string>(StringComparer.Ordinal);
        long revision = 0; bool hasPending = false;
        foreach (var cohort in source.observations.GroupBy(row => row.fact.transitionRevision).OrderBy(group => group.Key))
        {
            var rows = cohort.OrderBy(row => row.fact.changeIndex).ToArray();
            if (hasPending) throw new ArgumentException("apparel-change-history-after-pending");
            ValidateCohort(rows, checked(++revision));
            foreach (var row in rows)
            {
                if (!DomainFactValueSyntax.IsText(row.publicationKey, 1024, false) || !keys.Add(row.publicationKey)
                    || (row.pending ? row.publicationId != string.Empty || row.fingerprint != string.Empty
                        : !DomainFactValueSyntax.IsText(row.publicationId, 1024, false) || !Hash(row.fingerprint)))
                    throw new ArgumentException("apparel-change-publication-proof-invalid");
                result.observations.Add(row.Clone());
            }
            hasPending = rows[0].pending;
        }
        if (revision != source.transitionRevision) throw new ArgumentException("apparel-change-transition-sequence-invalid");
        return result;
    }
    private static void ValidateCohort(ApparelChangePublicationData[] rows, long revision)
    {
        if (rows.Length == 0) throw new ArgumentException("apparel-change-cohort-empty");
        var first = rows[0].fact; first.Validate();
        for (int index = 0; index < rows.Length; index++)
        {
            var fact = rows[index].fact; fact.Validate();
            if (fact.transitionRevision != revision || fact.changeIndex != index || fact.changeCount != rows.Length
                || fact.characterId != first.characterId || fact.characterDisplayName != first.characterDisplayName
                || fact.origin != first.origin || fact.absoluteDay != first.absoluteDay || rows[index].pending != rows[0].pending)
                throw new ArgumentException("apparel-change-cohort-invalid");
        }
    }
    public void Restore(ApparelChangePublicationSaveData source)
    {
        RequireIdlePersistence();
        var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var previous = state; return () => state = previous; })) state = candidate;
    }
    public void RequireIdlePersistence()
    { if (IsBusy) throw new InvalidOperationException("apparel-change-publication-active"); }
    public void RequireIdleMutation()
    { RequireIdlePersistence(); if (IsRestoring) throw new InvalidOperationException("apparel-change-publication-restoring"); }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    public string ParticipantId => ApparelChangePublicationSaveSection.Id;
    public void BeginRestoreCandidate() { RequireIdleMutation(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
