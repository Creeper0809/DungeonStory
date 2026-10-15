using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class CharacterSkillActivationPublicationData
{
    public CharacterSkillActivationFactData fact = new();
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string fingerprint = string.Empty;
    public bool pending;
    public bool ownerFinalized;
    public CharacterSkillActivationPublicationData Clone() => new()
    { fact = fact.Clone(), publicationKey = publicationKey, publicationId = publicationId,
        fingerprint = fingerprint, pending = pending, ownerFinalized = ownerFinalized };
}

[Serializable]
public sealed class CharacterSkillActivationPublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<CharacterSkillActivationPublicationData> observations = new();
}

/// <summary>Applied standalone skill facts survive uncertain observation; replay never executes a skill.</summary>
public sealed class CharacterSkillActivationPublicationRuntime : IDungeonRestoreTransactionParticipant
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private Dictionary<(string, long), CharacterSkillActivationPublicationData> rows = new();
    private readonly HashSet<string> activeSources = new(StringComparer.Ordinal);
    private bool recovering;
    public bool IsRestoring { get; private set; }
    public bool IsBusy => activeSources.Count > 0 || recovering;
    public string LastFailure { get; private set; } = string.Empty;

    public CharacterSkillActivationPublicationRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool TryBegin(string sourceId, out string failure)
    {
        failure = string.Empty;
        if (!DomainFactValueSyntax.IsIdentity(sourceId) || IsRestoring || recovering
            || activeSources.Contains(sourceId) || PendingForSource(sourceId) != null)
        { failure = "skill-publication-source-busy"; return false; }
        activeSources.Add(sourceId); return true;
    }
    public void End(string sourceId)
    { if (!activeSources.Remove(sourceId)) throw new InvalidOperationException("skill-publication-source-not-active"); }

    public CharacterSkillActivationPublicationData Find(string sourceId, long revision) =>
        rows.TryGetValue((sourceId, revision), out var row) ? row.Clone() : null;
    public CharacterSkillActivationPublicationData PendingForSource(string sourceId) =>
        rows.Values.SingleOrDefault(row => row.fact.sourceCharacterId == sourceId && (!row.ownerFinalized || row.pending))?.Clone();
    public IReadOnlyList<CharacterSkillActivationPublicationData> PendingOwners() => rows.Values
        .Where(row => !row.ownerFinalized).OrderBy(row => row.fact.sourceCharacterId, StringComparer.Ordinal)
        .ThenBy(row => row.fact.ownerRevision).Select(row => row.Clone()).ToArray();

    public DomainFactDelivery PublishApplied(CharacterSkillActivationFact fact, DomainFactLease lease)
    {
        if (fact == null || !activeSources.Contains(fact.SourceCharacterId) || !lease.IsValid
            || fact.ActivationKind == CharacterSkillActivationKind.ManualCombatUltimate || IsRestoring || recovering
            || rows.ContainsKey((fact.SourceCharacterId, fact.OwnerRevision)))
            throw new InvalidOperationException("skill-publication-applied-contract-invalid");
        var row = new CharacterSkillActivationPublicationData
        { fact = fact.Snapshot(), publicationKey = lease.PublicationKey, pending = true };
        var key = (fact.SourceCharacterId, fact.OwnerRevision);
        rows.Add(key, row);
        DomainFactDelivery delivery;
        try { delivery = publisher.Publish(lease, fact); }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = "skill-publication-uncertain:" + exception.GetType().Name + ":" + exception.Message;
            return DomainFactDelivery.Uncertain(LastFailure); }
        if (!delivery.MayBeCommitted)
        { rows.Remove(key); LastFailure = delivery.Detail; return delivery; }
        if (Accept(row, delivery)) { LastFailure = string.Empty; return delivery; }
        LastFailure = "skill-publication-pending:" + delivery.Detail;
        return DomainFactDelivery.Uncertain(LastFailure);
    }

    public bool RecoverPending(out string failure)
    {
        RequireIdle(); recovering = true; failure = string.Empty;
        try
        {
            foreach (var row in rows.Values.Where(row => row.pending)
                .OrderBy(row => row.fact.sourceCharacterId, StringComparer.Ordinal).ThenBy(row => row.fact.ownerRevision))
            {
                var replies = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey,
                    new CharacterSkillActivationFact(row.fact)) }, replies, out failure) || !Accept(row, replies[0]))
                { failure = "skill-publication-recovery-pending:" + failure; LastFailure = failure; return false; }
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "skill-publication-recovery:" + exception.GetType().Name + ":" + exception.Message;
            LastFailure = failure; return false; }
        finally { recovering = false; }
    }

    public void CompleteOwner(string sourceId, long revision)
    {
        if (IsRestoring || recovering || !rows.TryGetValue((sourceId, revision), out var row) || row.pending)
            throw new InvalidOperationException("skill-publication-owner-finalize-invalid");
        row.ownerFinalized = true;
    }
    public static DomainFactDelivery Delivery(CharacterSkillActivationPublicationData row) => row.pending
        ? DomainFactDelivery.Uncertain("skill-publication-pending")
        : new(DomainFactDeliveryPhase.Delivered, row.publicationKey, row.publicationId, row.fingerprint, string.Empty);
    private static bool Accept(CharacterSkillActivationPublicationData row, DomainFactDelivery delivery)
    {
        if (!delivery.Completed || delivery.OperationKey != row.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 1024, false) || !Hash(delivery.Fingerprint)) return false;
        row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint; row.pending = false; return true;
    }
    public CharacterSkillActivationPublicationSaveData Capture()
    {
        RequireNotBusy();
        return new() { observations = rows.Values.OrderBy(row => row.fact.sourceCharacterId, StringComparer.Ordinal)
            .ThenBy(row => row.fact.ownerRevision).Select(row => row.Clone()).ToList() };
    }
    public static CharacterSkillActivationPublicationSaveData ValidateAndClone(CharacterSkillActivationPublicationSaveData source)
    {
        if (source == null || source.version != CharacterSkillActivationPublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("skill-publication-save-invalid");
        var result = new CharacterSkillActivationPublicationSaveData();
        var identities = new HashSet<(string, long)>(); var keys = new HashSet<string>(StringComparer.Ordinal);
        var pendingSources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.observations.OrderBy(row => row?.fact?.sourceCharacterId, StringComparer.Ordinal)
            .ThenBy(row => row?.fact?.ownerRevision))
        {
            if (row?.fact == null || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false)
                || !keys.Add(row.publicationKey) || row.pending && row.ownerFinalized
                || (row.pending ? row.publicationId != string.Empty || row.fingerprint != string.Empty
                    : !DomainFactValueSyntax.IsText(row.publicationId, 1024, false) || !Hash(row.fingerprint)))
                throw new ArgumentException("skill-publication-proof-invalid");
            var fact = new CharacterSkillActivationFact(row.fact);
            if (fact.ActivationKind == CharacterSkillActivationKind.ManualCombatUltimate
                || !identities.Add((fact.SourceCharacterId, fact.OwnerRevision)) || pendingSources.Contains(fact.SourceCharacterId))
                throw new ArgumentException("skill-publication-history-invalid");
            if (!row.ownerFinalized) pendingSources.Add(fact.SourceCharacterId);
            result.observations.Add(row.Clone());
        }
        return result;
    }
    public void Restore(CharacterSkillActivationPublicationSaveData source)
    {
        RequireNotBusy();
        var candidate = ValidateAndClone(source).observations.ToDictionary(row => (row.fact.sourceCharacterId, row.fact.ownerRevision));
        if (!restore.TryStage(() => rows = candidate, () => { var previous = rows; return () => rows = previous; })) rows = candidate;
    }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    private void RequireNotBusy() { if (IsBusy) throw new InvalidOperationException("skill-publication-active"); }
    private void RequireIdle() { RequireNotBusy(); if (IsRestoring) throw new InvalidOperationException("skill-publication-restore-active"); }
    public string ParticipantId => "character.skill-publications";
    public void BeginRestoreCandidate() { RequireIdle(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
