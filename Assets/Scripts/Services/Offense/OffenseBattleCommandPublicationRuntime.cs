using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class OffenseBattleCommandObservationData
{
    public OffenseBattleCommandFactData fact = new();
    public string publicationKey = string.Empty;
    public bool pending, hasSkill;
    public CharacterSkillActivationFactData skill = new();
    public string skillPublicationKey = string.Empty, skillPublicationId = string.Empty, skillFingerprint = string.Empty;
    public int followupStage;
    public OffenseBattleCommandObservationData Clone()
    {
        var copy = (OffenseBattleCommandObservationData)MemberwiseClone();
        copy.fact = fact.Clone(); copy.skill = skill.Clone(); return copy;
    }
}

[Serializable]
public sealed class OffenseBattleCommandPublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<OffenseBattleCommandObservationData> observations = new();
}

/// <summary>Applied command evidence and delivery only; never executes combat or skill effects.</summary>
public sealed class OffenseBattleCommandPublicationRuntime : IDungeonRestoreTransactionParticipant
{
    public const int FollowupsComplete = 5;
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private Dictionary<(string, long), OffenseBattleCommandObservationData> rows = new();
    private OffenseBattleCommandFact proposal;
    private DomainFactLease lease, skillLease;
    private bool recovering, restoring;
    public string LastFailure { get; private set; } = string.Empty;
    public OffenseBattleCommandPublicationRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }
    public string ReservedPublicationKey => lease.PublicationKey;
    public bool HasPending(string battleId) => rows.Values.Any(row => row.fact.battleId == battleId
        && (row.pending || row.followupStage != FollowupsComplete));
    public bool HasAnyPending => rows.Values.Any(row => row.pending || row.followupStage != FollowupsComplete);
    public OffenseBattleCommandObservationData PendingForBattle(string battleId) => rows.Values
        .SingleOrDefault(row => row.fact.battleId == battleId && (row.pending || row.followupStage != FollowupsComplete))?.Clone();

    public bool TryBegin(OffenseBattleCommandFact fact, out string failure)
    {
        RequireIdle();
        var data = (fact ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        if (data.resolved || HasAnyPending || rows.Keys.Any(key => key.Item1 == data.battleId && key.Item2 >= data.commandId))
        { failure = "battle-command-observation-busy-or-duplicate"; return false; }
        proposal = fact;
        try
        {
            if (publisher.TryReserve(fact, out lease, out failure)
                && lease.IsValid && !string.IsNullOrWhiteSpace(lease.PublicationKey)) return true;
            LastFailure = "battle-command-reserve:" + failure;
        }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = "battle-command-reserve:" + exception.GetType().Name + ":" + exception.Message; }
        failure = LastFailure; End(); return false;
    }

    public DomainFactDelivery PublishApplied(OffenseBattleCommandFact actual,
        CharacterSkillActivationFact skill, DomainFactLease skillReservation)
    {
        if (proposal == null || actual == null || !actual.MatchesProposal(proposal) || !actual.Snapshot().resolved)
            throw new InvalidOperationException("battle-command-applied-context-invalid");
        var data = actual.Snapshot();
        var row = new OffenseBattleCommandObservationData
        { fact = data, publicationKey = lease.PublicationKey, pending = true, hasSkill = skill != null,
            skill = skill?.Snapshot() ?? new(), skillPublicationKey = skill != null ? skillReservation.PublicationKey : string.Empty };
        if (skill != null && (!skillReservation.IsValid || !SkillMatches(row)))
            throw new InvalidOperationException("battle-command-skill-cohort-invalid");
        skillLease = skillReservation;
        var key = (data.battleId, data.commandId);
        rows.Add(key, row); // Save the actual effects before crossing the durable boundary.
        var replies = new DomainFactDelivery[skill != null ? 2 : 1];
        bool accepted;
        try
        {
            var emissions = new List<DomainFactEmission> { DomainFactEmission.Create(lease, actual) };
            if (skill != null) emissions.Add(DomainFactEmission.Create(skillReservation, skill));
            accepted = publisher.PublishBatch(emissions, replies, out string failure);
            LastFailure = failure;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            accepted = true;
            LastFailure = "battle-command-publish:" + exception.GetType().Name + ":" + exception.Message;
            for (int i = 0; i < replies.Length; i++) replies[i] = DomainFactDelivery.Uncertain(LastFailure);
        }
        if (!accepted && replies.All(value => !value.MayBeCommitted))
        { rows.Remove(key); return DomainFactDelivery.Reject(LastFailure); }
        if (AcceptDelivery(row, replies)) return replies[0];
        LastFailure = "battle-command-delivery-pending:" + LastFailure;
        return DomainFactDelivery.Uncertain(LastFailure);
    }

    public void End()
    {
        if (proposal == null) throw new InvalidOperationException("battle-command-no-active-publication");
        try { publisher.Cancel(lease); publisher.Cancel(skillLease); }
        catch (Exception exception) when (Recoverable(exception)) { LastFailure = "battle-command-cancel:" + exception.GetType().Name; }
        finally { lease = default; skillLease = default; proposal = null; }
    }

    public bool RecoverPending(out string failure)
    {
        RequireIdle(); recovering = true; failure = string.Empty;
        try
        {
            foreach (var row in rows.Values.Where(value => value.pending).OrderBy(value => value.fact.commandId))
            {
                var requests = new List<DomainFactReplay>
                { DomainFactReplay.Create(row.publicationKey, new OffenseBattleCommandFact(row.fact)) };
                if (row.hasSkill) requests.Add(DomainFactReplay.Create(row.skillPublicationKey, new CharacterSkillActivationFact(row.skill)));
                var replies = new DomainFactDelivery[requests.Count];
                if (!recovery.ReplayBatch(requests, replies, out failure) || !AcceptDelivery(row, replies))
                { failure = "battle-command-recovery-pending:" + failure; LastFailure = failure; return false; }
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "battle-command-recovery:" + exception.GetType().Name + ":" + exception.Message; LastFailure = failure; return false; }
        finally { recovering = false; }
    }

    public void AdvanceFollowup(string battleId, long commandId, int completedStage)
    {
        RequireIdle();
        if (!rows.TryGetValue((battleId, commandId), out var row) || row.pending
            || row.followupStage != completedStage || completedStage >= FollowupsComplete)
            throw new InvalidOperationException("battle-command-followup-order-invalid");
        row.followupStage++;
    }

    public void RecordFollowupFailure(Exception exception) => LastFailure =
        "battle-command-followup:" + exception.GetType().Name + ":" + exception.Message;

    public OffenseBattleCommandPublicationSaveData Capture()
    {
        RequireNotBusy();
        return new() { observations = rows.Values.OrderBy(row => row.fact.battleId, StringComparer.Ordinal)
            .ThenBy(row => row.fact.commandId).Select(row => row.Clone()).ToList() };
    }

    public static OffenseBattleCommandPublicationSaveData ValidateAndClone(OffenseBattleCommandPublicationSaveData source)
    {
        if (source == null || source.version != OffenseBattleCommandPublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("battle-command-save-invalid");
        var result = new OffenseBattleCommandPublicationSaveData();
        var identities = new HashSet<(string, long)>(); var unfinished = new HashSet<string>(StringComparer.Ordinal);
        var publications = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.observations.OrderBy(value => value?.fact?.battleId, StringComparer.Ordinal).ThenBy(value => value?.fact?.commandId))
        {
            if (row?.fact == null || row.skill == null || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false)
                || !publications.Add(row.publicationKey) || row.followupStage < 0 || row.followupStage > FollowupsComplete
                || row.pending && row.followupStage != 0)
                throw new ArgumentException("battle-command-observation-invalid");
            var fact = new OffenseBattleCommandFact(row.fact).Snapshot();
            if (!fact.resolved || !identities.Add((fact.battleId, fact.commandId)) || unfinished.Contains(fact.battleId))
                throw new ArgumentException("battle-command-observation-order-invalid");
            if (row.pending || row.followupStage != FollowupsComplete) unfinished.Add(fact.battleId);
            if (row.hasSkill)
            {
                _ = new CharacterSkillActivationFact(row.skill);
                if (!SkillMatches(row) || !DomainFactValueSyntax.IsText(row.skillPublicationKey, 1024, false)
                    || !publications.Add(row.skillPublicationKey)
                    || (row.pending ? row.skillPublicationId != string.Empty || row.skillFingerprint != string.Empty
                        : !DomainFactValueSyntax.IsText(row.skillPublicationId, 1024, false) || !Hash(row.skillFingerprint)))
                    throw new ArgumentException("battle-command-skill-observation-invalid");
            }
            else if (row.skillPublicationKey != string.Empty || row.skillPublicationId != string.Empty || row.skillFingerprint != string.Empty
                || !EmptySkill(row.skill))
                throw new ArgumentException("battle-command-unexpected-skill-observation");
            result.observations.Add(row.Clone());
        }
        if (unfinished.Count > 1) throw new ArgumentException("battle-command-multiple-pending-battles");
        return result;
    }

    private static bool SkillMatches(OffenseBattleCommandObservationData row) =>
        row.fact.actionType == OffenseBattleActionType.Ability && row.skill.sourceCharacterId == row.fact.actorId
        && row.skill.skillId == row.fact.abilityId && row.skill.activationKind == CharacterSkillActivationKind.ManualCombatUltimate
        && row.skill.ultimateDomain == CharacterUltimateDomain.Offense
        && row.skill.trigger == CharacterSkillTrigger.ManualCombat
        && row.skill.triggerSerial == CharacterGrowthRules.StableNonNegativeHash(row.fact.battleId)
        && row.skill.absoluteHour == row.fact.absoluteHour
        && row.skill.triggerEventId == "battle:" + row.fact.battleId + ":command:" + row.fact.commandId + ":offense-ultimate"
        && row.skill.childPublicationKeys.Count == 1 && row.skill.childPublicationKeys[0] == row.publicationKey;

    private static bool EmptySkill(CharacterSkillActivationFactData value) =>
        value.sourceCharacterId == string.Empty && value.sourceDisplayName == string.Empty && value.ownerRevision == 0
        && value.activationKind == 0 && value.skillId == string.Empty && value.skillDisplayName == string.Empty
        && value.trigger == 0 && value.target == 0 && value.targetingMode == 0 && value.effectArea == 0 && value.areaSize == 1
        && value.durationHours == 0 && value.cooldownDays == 0 && value.absoluteDay == 0 && value.absoluteHour == 0
        && value.expiresAbsoluteHour == 0 && value.useCountBefore == 0 && value.useCountAfter == 0
        && value.readyAbsoluteHourBefore == 0 && value.readyAbsoluteHourAfter == 0 && value.mechanicsSha256 == string.Empty
        && value.recipients?.Count == 0 && value.childPublicationKeys?.Count == 0 && value.triggerEventId == string.Empty
        && value.triggerEventKeySha256 == string.Empty && value.ultimateDomain == 0 && value.triggerSerial == -1;

    private static bool AcceptDelivery(OffenseBattleCommandObservationData row, DomainFactDelivery[] replies)
    {
        if (replies.Length != (row.hasSkill ? 2 : 1) || !Delivered(row.publicationKey, replies[0])
            || row.hasSkill && !Delivered(row.skillPublicationKey, replies[1])) return false;
        if (row.hasSkill) { row.skillPublicationId = replies[1].PublicationId; row.skillFingerprint = replies[1].Fingerprint; }
        row.pending = false; return true;
    }
    public static DomainFactDelivery SkillDelivery(OffenseBattleCommandObservationData row) => new(
        DomainFactDeliveryPhase.Delivered, row.skillPublicationKey, row.skillPublicationId, row.skillFingerprint, string.Empty);
    private static bool Delivered(string key, DomainFactDelivery reply) => reply.Completed && reply.OperationKey == key
        && !string.IsNullOrWhiteSpace(reply.PublicationId) && Hash(reply.Fingerprint);
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    private void RequireNotBusy() { if (proposal != null || recovering) throw new InvalidOperationException("battle-command-publication-active"); }
    private void RequireIdle() { RequireNotBusy(); if (restoring) throw new InvalidOperationException("battle-command-restore-active"); }
    public void Restore(OffenseBattleCommandPublicationSaveData source)
    {
        RequireNotBusy();
        var candidate = ValidateAndClone(source).observations.ToDictionary(row => (row.fact.battleId, row.fact.commandId));
        if (!restore.TryStage(() => rows = candidate, () => { var previous = rows; return () => rows = previous; })) rows = candidate;
    }
    public string ParticipantId => "offense.battle-command-publications";
    public void BeginRestoreCandidate() { RequireIdle(); restore.Begin(); restoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { restoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); restoring = false; }
}
