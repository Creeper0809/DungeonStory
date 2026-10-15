using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class CharacterTraitReactionPublicationData
{
    public CharacterTraitReactionFactData fact;
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string fingerprint = string.Empty;
    public bool pending;
    public CharacterTraitReactionPublicationData Clone() => new()
    { fact = fact.Clone(), publicationKey = publicationKey, publicationId = publicationId,
        fingerprint = fingerprint, pending = pending };
}

[Serializable]
public sealed class CharacterTraitReactionPublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<CharacterTraitReactionPublicationData> observations = new();
}

/// <summary>Lifetime-independent applied facts; recovery never applies experience, mood or reaction counters.</summary>
public sealed class CharacterTraitReactionPublicationRuntime : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private Dictionary<(string, long), CharacterTraitReactionPublicationData> rows = new();
    private readonly HashSet<string> active = new(StringComparer.Ordinal);
    // Failed reversible transactions are not saveable. Keep their exact rollback,
    // including staged effect snapshots, until it succeeds; actor lifetime cannot discard it.
    private readonly Dictionary<string, Func<bool>> rollbacks = new(StringComparer.Ordinal);
    private bool recovering;
    public bool IsRestoring { get; private set; }
    public bool IsBusy => active.Count != 0 || rollbacks.Count != 0 || recovering;
    public string LastFailure { get; private set; } = string.Empty;

    public CharacterTraitReactionPublicationRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public CharacterTraitReactionPublicationData PendingForCharacter(string characterId) => rows.Values
        .SingleOrDefault(row => row.pending && row.fact.characterId == characterId)?.Clone();

    public bool TryBegin(string characterId, out string failure)
    {
        failure = string.Empty;
        if (!DomainFactValueSyntax.IsIdentity(characterId) || IsRestoring || recovering || rollbacks.Count != 0
            || active.Contains(characterId) || PendingForCharacter(characterId) != null)
        { failure = "trait-reaction-publication-busy"; return false; }
        active.Add(characterId); return true;
    }
    public void End(string characterId)
    { if (!active.Remove(characterId)) throw new InvalidOperationException("trait-reaction-source-not-active"); }

    public void RetainRollback(string characterId, Func<bool> retry)
    {
        if (!active.Contains(characterId) || retry == null || rollbacks.ContainsKey(characterId))
            throw new InvalidOperationException("trait-reaction-rollback-contract-invalid");
        rollbacks.Add(characterId, retry);
        LastFailure = "trait-reaction-effect-rollback-pending";
    }

    public DomainFactDelivery PublishApplied(CharacterTraitReactionFact fact, DomainFactLease lease)
    {
        var data = fact.Snapshot();
        var key = (data.characterId, data.afterRevision);
        if (!active.Contains(data.characterId) || IsRestoring || recovering || !lease.IsValid || rows.ContainsKey(key))
            throw new InvalidOperationException("trait-reaction-publication-contract-invalid");
        var row = new CharacterTraitReactionPublicationData
        { fact = data, publicationKey = lease.PublicationKey, pending = true };
        rows.Add(key, row);
        DomainFactDelivery delivery;
        try { delivery = publisher.Publish(lease, fact); }
        catch
        {
            // The neutral port contract defines an escaped exception as pre-commit.
            rows.Remove(key); throw;
        }
        if (!delivery.MayBeCommitted)
        { rows.Remove(key); LastFailure = delivery.Detail; return delivery; }
        if (Accept(row, delivery)) { LastFailure = string.Empty; return delivery; }
        LastFailure = "trait-reaction-observation-pending:" + delivery.Detail;
        return DomainFactDelivery.Uncertain(LastFailure);
    }

    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (IsRestoring || recovering || active.Count != 0)
        { failure = "trait-reaction-recovery-busy"; return false; }
        recovering = true;
        try
        {
            foreach (var rollback in rollbacks.ToArray())
            {
                if (!rollback.Value())
                { failure = "trait-reaction-effect-rollback-pending"; LastFailure = failure; return false; }
                rollbacks.Remove(rollback.Key);
            }
            foreach (var row in rows.Values.Where(row => row.pending)
                .OrderBy(row => row.fact.characterId, StringComparer.Ordinal).ThenBy(row => row.fact.afterRevision))
            {
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey,
                    new CharacterTraitReactionFact(row.fact)) }, results, out failure) || !Accept(row, results[0]))
                { failure = "trait-reaction-recovery-pending:" + failure; LastFailure = failure; return false; }
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "trait-reaction-recovery:" + exception.Message; LastFailure = failure; return false; }
        finally { recovering = false; }
    }
    public void Tick()
    {
        if (IsRestoring || active.Count != 0 || recovering || retryClock.ElapsedMilliseconds < 1000
            || rollbacks.Count == 0 && !rows.Values.Any(row => row.pending)) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);

    private static bool Accept(CharacterTraitReactionPublicationData row, DomainFactDelivery delivery)
    {
        if (!delivery.Completed || delivery.OperationKey != row.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 1024, false) || !Hash(delivery.Fingerprint)) return false;
        row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint; row.pending = false; return true;
    }
    public CharacterTraitReactionPublicationSaveData Capture()
    {
        RequireIdle();
        return new() { observations = rows.Values.OrderBy(row => row.fact.characterId, StringComparer.Ordinal)
            .ThenBy(row => row.fact.afterRevision).Select(row => row.Clone()).ToList() };
    }
    public static CharacterTraitReactionPublicationSaveData ValidateAndClone(CharacterTraitReactionPublicationSaveData source)
    {
        if (source == null || source.version != CharacterTraitReactionPublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("trait-reaction-publication-save-invalid");
        var result = new CharacterTraitReactionPublicationSaveData();
        var identities = new HashSet<(string, long)>(); var keys = new HashSet<string>(StringComparer.Ordinal);
        var pendingCharacters = new HashSet<string>(StringComparer.Ordinal);
        var lastRevisions = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var row in source.observations.OrderBy(row => row?.fact?.characterId, StringComparer.Ordinal)
            .ThenBy(row => row?.fact?.afterRevision))
        {
            if (row?.fact == null || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false)
                || !keys.Add(row.publicationKey)
                || (row.pending ? row.publicationId != string.Empty || row.fingerprint != string.Empty
                    : !DomainFactValueSyntax.IsText(row.publicationId, 1024, false) || !Hash(row.fingerprint)))
                throw new ArgumentException("trait-reaction-publication-proof-invalid");
            row.fact.Validate();
            if (!identities.Add((row.fact.characterId, row.fact.afterRevision)) || pendingCharacters.Contains(row.fact.characterId)
                || lastRevisions.TryGetValue(row.fact.characterId, out long previous) && row.fact.beforeRevision < previous)
                throw new ArgumentException("trait-reaction-publication-history-invalid");
            lastRevisions[row.fact.characterId] = row.fact.afterRevision;
            if (row.pending) pendingCharacters.Add(row.fact.characterId);
            result.observations.Add(row.Clone());
        }
        return result;
    }
    public void Restore(CharacterTraitReactionPublicationSaveData source)
    {
        if (IsBusy) throw new InvalidOperationException("trait-reaction-publication-active");
        var candidate = ValidateAndClone(source).observations.ToDictionary(row => (row.fact.characterId, (long)row.fact.afterRevision));
        if (!restore.TryStage(() => rows = candidate, () => { var previous = rows; return () => rows = previous; })) rows = candidate;
    }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    private void RequireIdle()
    { if (IsBusy || IsRestoring) throw new InvalidOperationException("trait-reaction-publication-active"); }
    public string ParticipantId => "character.trait-reactions";
    public void BeginRestoreCandidate() { RequireIdle(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
