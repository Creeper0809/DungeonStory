using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class MemoryErasurePublicationData
{
    public MemoryErasureTerminalFactData fact;
    public string publicationKey = string.Empty, publicationId = string.Empty, fingerprint = string.Empty;
    public bool pending;
    public MemoryErasurePublicationData Clone() => new()
    { fact = fact.Clone(), publicationKey = publicationKey, publicationId = publicationId,
        fingerprint = fingerprint, pending = pending };
}

[Serializable]
public sealed class MemoryErasurePublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public long nextRevision = 1;
    public List<MemoryErasurePublicationData> observations = new();
}

/// <summary>Terminal history outlives actors. Replays observe facts, never consume an item or erase a trait.</summary>
public sealed class MemoryErasurePublicationRuntime : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactStagingPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private Dictionary<long, MemoryErasurePublicationData> rows = new();
    private readonly Dictionary<string, Func<bool>> rollbacks = new(StringComparer.Ordinal);
    private long nextRevision = 1;
    private string activeOperation;
    private bool recovering;
    public bool IsRestoring { get; private set; }
    public bool IsBusy => activeOperation != null || recovering || rollbacks.Count != 0;
    public string LastFailure { get; private set; } = string.Empty;

    public MemoryErasurePublicationRuntime(IDomainFactStagingPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    // Cancellation and a later retry may both be pending; report the latest without losing earlier history.
    public MemoryErasurePublicationData PendingForOperation(string operation) => rows.Values
        .Where(row => row.pending && row.fact.operationId == operation)
        .OrderByDescending(row => row.fact.terminalRevision).FirstOrDefault()?.Clone();

    public bool TryBegin(string operation, out long revision, out string failure)
    {
        revision = 0; failure = string.Empty;
        if (!DomainFactValueSyntax.IsIdentity(operation) || IsBusy || IsRestoring || nextRevision == long.MaxValue)
        { failure = "memory-erasure-publication-busy-or-revision-exhausted"; return false; }
        activeOperation = operation; revision = nextRevision++; return true;
    }
    public void End(string operation)
    {
        if (activeOperation != operation) throw new InvalidOperationException("memory-erasure-source-not-active");
        activeOperation = null;
    }
    public void RetainRollback(string operation, Func<bool> retry)
    {
        if (activeOperation != operation || retry == null || rollbacks.ContainsKey(operation))
            throw new InvalidOperationException("memory-erasure-rollback-contract-invalid");
        rollbacks.Add(operation, retry); LastFailure = "memory-erasure-effect-rollback-pending";
    }

    // Called BEFORE physical acknowledgement. Capture is blocked while this transaction is active.
    public void RetainPrepared(MemoryErasureTerminalFact fact, IDomainFactStage stage)
    {
        var data = fact.Snapshot();
        if (activeOperation != data.operationId || data.status != MemoryErasureSealUseStatus.Succeeded
            || data.terminalRevision >= nextRevision || stage == null || !stage.IsActive
            || !DomainFactValueSyntax.IsText(stage.OperationKey, 1024, false))
            throw new InvalidOperationException("memory-erasure-prepared-publication-invalid");
        rows.Add(data.terminalRevision, new MemoryErasurePublicationData
        { fact = data, publicationKey = stage.OperationKey, pending = true });
    }
    public void DiscardPrepared(long revision) => rows.Remove(revision);

    // After the irreversible physical ACK, even a known publication rejection is retained for replay.
    public DomainFactDelivery CommitApplied(long revision, IDomainFactStage stage)
    {
        var row = rows[revision];
        try
        {
            var delivery = stage.Commit();
            if (Accept(row, delivery)) { LastFailure = string.Empty; return delivery; }
            LastFailure = "memory-erasure-observation-pending:" + delivery.Detail;
        }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = "memory-erasure-observation-pending:" + exception.Message; }
        return DomainFactDelivery.Uncertain(LastFailure);
    }

    public DomainFactDelivery RecordTerminal(MemoryErasureSealUseResult result,
        CharacterAcquiredTraitInstanceState selected, string name, int day)
    {
        if (result.Succeeded) throw new InvalidOperationException("memory-erasure-success-is-transaction-owned");
        if (IsRestoring || nextRevision == long.MaxValue)
            throw new InvalidOperationException("memory-erasure-terminal-history-unavailable");
        var fact = MemoryErasureTerminalFact.Capture(result, selected, name, day, nextRevision);
        var row = new MemoryErasurePublicationData { fact = fact.Snapshot(), pending = true };
        rows.Add(nextRevision++, row);
        string failure = "memory-erasure-source-active";
        if (!IsBusy && TryDeliver(row, out failure))
        { LastFailure = string.Empty; return Delivered(row); }
        LastFailure = "memory-erasure-terminal-observation-pending:" + failure;
        return DomainFactDelivery.Uncertain(LastFailure);
    }

    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (IsRestoring || recovering || activeOperation != null)
        { failure = "memory-erasure-recovery-busy"; return false; }
        recovering = true;
        try
        {
            foreach (var rollback in rollbacks.ToArray())
            {
                if (!rollback.Value())
                { failure = LastFailure = "memory-erasure-effect-rollback-pending"; return false; }
                rollbacks.Remove(rollback.Key);
            }
            foreach (var row in rows.Values.Where(row => row.pending).OrderBy(row => row.fact.terminalRevision).ToArray())
                if (!TryDeliver(row, out failure)) { LastFailure = failure; return false; }
            LastFailure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = LastFailure = "memory-erasure-recovery:" + exception.Message; return false; }
        finally { recovering = false; }
    }
    private bool TryDeliver(MemoryErasurePublicationData row, out string failure)
    {
        failure = string.Empty;
        IDomainFactStage stage = null;
        try
        {
            var fact = new MemoryErasureTerminalFact(row.fact);
            if (row.publicationKey.Length == 0)
            {
                if (!publisher.TryStage(fact, out stage, out failure)) return false;
                if (stage == null || !stage.IsActive || !DomainFactValueSyntax.IsText(stage.OperationKey, 1024, false))
                { failure = "memory-erasure-terminal-stage-invalid"; return false; }
                row.publicationKey = stage.OperationKey;
                var delivery = stage.Commit();
                if (Accept(row, delivery)) return true;
                failure = "memory-erasure-terminal-delivery-pending:" + delivery.Detail; return false;
            }
            var results = new DomainFactDelivery[1];
            if (recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey, fact) }, results, out failure)
                && Accept(row, results[0])) return true;
            failure = "memory-erasure-replay-pending:" + failure; return false;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "memory-erasure-terminal-publication:" + exception.Message; return false; }
        finally
        {
            try { stage?.Cancel(); }
            catch (Exception exception) when (Recoverable(exception))
            { LastFailure = "memory-erasure-stage-cancel-failed:" + exception.Message; }
        }
    }
    private static bool Accept(MemoryErasurePublicationData row, DomainFactDelivery delivery)
    {
        if (!delivery.Completed || delivery.OperationKey != row.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 1024, false) || !Hash(delivery.Fingerprint)) return false;
        row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint; row.pending = false; return true;
    }
    private static DomainFactDelivery Delivered(MemoryErasurePublicationData row) => new(
        DomainFactDeliveryPhase.Delivered, row.publicationKey, row.publicationId, row.fingerprint, string.Empty);
    public void Tick()
    {
        if (IsRestoring || activeOperation != null || recovering || retryClock.ElapsedMilliseconds < 1000
            || rollbacks.Count == 0 && !rows.Values.Any(row => row.pending)) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);
    public MemoryErasurePublicationSaveData Capture()
    {
        RequireIdle(); return new() { nextRevision = nextRevision,
            observations = rows.Values.OrderBy(row => row.fact.terminalRevision).Select(row => row.Clone()).ToList() };
    }
    public static MemoryErasurePublicationSaveData ValidateAndClone(MemoryErasurePublicationSaveData source)
    {
        if (source == null || source.version != MemoryErasurePublicationSaveData.CurrentVersion
            || source.nextRevision < 1 || source.observations == null)
            throw new ArgumentException("memory-erasure-publication-save-invalid");
        var result = new MemoryErasurePublicationSaveData { nextRevision = source.nextRevision };
        var revisions = new HashSet<long>(); var keys = new HashSet<string>(StringComparer.Ordinal);
        var successes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.observations)
        {
            if (row?.fact == null || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, row.pending)
                || row.publicationKey.Length != 0 && !keys.Add(row.publicationKey)
                || (row.pending ? row.publicationId != string.Empty || row.fingerprint != string.Empty
                    : !DomainFactValueSyntax.IsText(row.publicationId, 1024, false) || !Hash(row.fingerprint)))
                throw new ArgumentException("memory-erasure-publication-proof-invalid");
            row.fact.Validate();
            if (!revisions.Add(row.fact.terminalRevision) || row.fact.terminalRevision >= source.nextRevision
                || row.fact.status == MemoryErasureSealUseStatus.Succeeded
                    && (!successes.Add(row.fact.auditId) || row.publicationKey.Length == 0))
                throw new ArgumentException("memory-erasure-publication-history-invalid");
            result.observations.Add(row.Clone());
        }
        return result;
    }
    public void Restore(MemoryErasurePublicationSaveData source)
    {
        if (IsBusy) throw new InvalidOperationException("memory-erasure-publication-active");
        var candidate = ValidateAndClone(source);
        var candidateRows = candidate.observations.ToDictionary(row => row.fact.terminalRevision);
        void Apply() { rows = candidateRows; nextRevision = candidate.nextRevision; }
        if (!restore.TryStage(Apply, () => { var previous = rows; long previousRevision = nextRevision;
            return () => { rows = previous; nextRevision = previousRevision; }; })) Apply();
    }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    private void RequireIdle()
    { if (IsBusy || IsRestoring) throw new InvalidOperationException("memory-erasure-publication-active"); }
    public string ParticipantId => "character.memory-erasure";
    public void BeginRestoreCandidate() { RequireIdle(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
