using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

// Domain-owned delivery memory. Observers alone choose native identities and formatting.
// Recovery replays frozen facts; it never reapplies grief, resources, campaign or lineage effects.
public sealed class SocialRitualFactJournal : ISocialRitualFactPublisher,
    IDungeonRestoreTransactionParticipant, IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly Dictionary<DomainFactLease, SocialRitualPendingFact> active = new();
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retry = System.Diagnostics.Stopwatch.StartNew();
    private SocialRitualFactSaveData state = new();
    private bool delivering, preparing, restoring;
    public string LastFailure { get; private set; } = string.Empty;
    public SocialRitualFactJournal(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure)
    {
        lease = default; failure = string.Empty;
        if (delivering || preparing || restoring) { failure = "social-ritual-publication-busy"; return false; }
        if (state.pending.Count > 0 && !RecoverPending(out failure)) return false;
        var row = SocialRitualFactCodec.Encode(fact);
        SocialRitualFactCodec.Decode(row);
        preparing = true;
        try
        {
            if (!publisher.TryReserve(fact, out lease, out failure)) return false;
            row.publicationKey = lease.PublicationKey;
            if (!Key(row.publicationKey) || active.Values.Any(v => v.publicationKey == row.publicationKey))
            { publisher.Cancel(lease); lease = default; failure = "social-ritual-reservation-conflict"; return false; }
            active.Add(lease, row); return true;
        }
        finally { preparing = false; }
    }
    public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
    {
        var actual = SocialRitualFactCodec.Encode(fact);
        if (!active.TryGetValue(lease, out var row) || row.kind != actual.kind || row.payload != actual.payload)
            return DomainFactDelivery.Reject("social-ritual-reserved-fact-conflict");
        var results = new DomainFactDelivery[1];
        PublishBatch(new[] { DomainFactEmission.Create(lease, fact) }, results, out _);
        return results[0];
    }
    public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure)
    {
        failure = string.Empty;
        if (delivering || preparing || restoring || facts == null || results == null || facts.Count != results.Length
            || facts.Count == 0 || facts.Any(v => v == null || !active.ContainsKey(v.Lease))
            || facts.Select(v => v.Lease).Distinct().Count() != facts.Count)
        { failure = "social-ritual-publication-batch-invalid"; return false; }
        var rows = facts.Select(v => active[v.Lease]).ToArray();
        delivering = true;
        state.pending.AddRange(rows);
        try
        {
            bool accepted;
            try { accepted = publisher.PublishBatch(facts, results, out failure); }
            catch (Exception error) when (Recoverable(error))
            {
                accepted = true; failure = "social-ritual-response-unknown:" + error.Message;
                for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Uncertain(failure);
            }
            for (int i = 0; i < rows.Length; i++)
            {
                if (!accepted || !results[i].MayBeCommitted || Acknowledged(rows[i], results[i])) state.pending.Remove(rows[i]);
                if (!accepted) results[i] = DomainFactDelivery.Reject(failure);
            }
            LastFailure = state.pending.Count == 0 ? string.Empty : "social-ritual-observation-pending:" + failure;
            return accepted;
        }
        finally
        {
            try
            {
                foreach (var fact in facts) active.Remove(fact.Lease);
                foreach (var fact in facts) publisher.Cancel(fact.Lease);
            }
            finally { delivering = false; }
        }
    }
    public void Cancel(in DomainFactLease lease)
    { active.Remove(lease); publisher.Cancel(lease); }
    public DomainFactDelivery Replay<T>(in T fact) =>
        throw new InvalidOperationException("Social ritual recovery requires the retained publication key.");

    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (delivering || preparing || restoring || active.Count != 0)
        { failure = "social-ritual-recovery-busy"; return false; }
        if (state.pending.Count == 0) return true;
        delivering = true;
        try
        {
            var rows = state.pending.ToArray();
            var results = new DomainFactDelivery[rows.Length];
            if (!recovery.ReplayBatch(rows.Select(SocialRitualFactCodec.Replay).ToArray(), results, out failure))
            { LastFailure = failure; return false; }
            for (int i = 0; i < rows.Length; i++)
                if (Acknowledged(rows[i], results[i])) state.pending.Remove(rows[i]);
            LastFailure = failure = state.pending.Count == 0 ? string.Empty : "social-ritual-acknowledgement-pending";
            return state.pending.Count == 0;
        }
        catch (Exception error) when (Recoverable(error))
        { LastFailure = failure = "social-ritual-recovery:" + error.Message; return false; }
        finally { delivering = false; }
    }
    private static bool Acknowledged(SocialRitualPendingFact row, DomainFactDelivery value) =>
        value.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery
        && value.OperationKey == row.publicationKey && Key(value.PublicationId) && !string.IsNullOrWhiteSpace(value.Fingerprint);
    private static bool Key(string value) => DomainFactValueSyntax.IsText(value, 2048, false);
    private static bool Recoverable(Exception error) => error is not OutOfMemoryException
        && error is not StackOverflowException && error is not AccessViolationException;
    public void AssertIdle()
    { if (delivering || preparing || active.Count > 0) throw new InvalidOperationException("social-ritual-publication-active"); }
    public void Tick()
    { if (state.pending.Count == 0 || delivering || preparing || restoring || active.Count > 0 || retry.ElapsedMilliseconds < 1000) return;
        retry.Restart(); RecoverPending(out _); }
    public void OnRestoreCompleted() => RecoverPending(out _);
    public SocialRitualFactSaveData Capture() { AssertIdle(); return ValidateAndClone(state); }
    public static SocialRitualFactSaveData ValidateAndClone(SocialRitualFactSaveData source)
    {
        if (source?.version != SocialRitualFactSaveData.CurrentVersion || source.pending == null)
            throw new InvalidOperationException("social-ritual-save-invalid");
        var result = new SocialRitualFactSaveData();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.pending)
        {
            if (row == null || !Key(row.publicationKey) || !keys.Add(row.publicationKey))
                throw new InvalidOperationException("social-ritual-pending-key-invalid");
            SocialRitualFactCodec.Decode(row); result.pending.Add(row.Clone());
        }
        return result;
    }
    public void Restore(SocialRitualFactSaveData source)
    { AssertIdle(); var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var before = state; return () => state = before; })) state = candidate; }
    public string ParticipantId => SocialRitualFactSaveSection.Id;
    public void BeginRestoreCandidate()
    { AssertIdle(); if (restoring) throw new InvalidOperationException("social-ritual-already-restoring"); restore.Begin(); restoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { restoring = false; } }
    public void CompleteRestoreCandidate() { try { restore.Complete(); } finally { restoring = false; } }
}
