using System;
using System.Globalization;
using DungeonStory.Foundation.Facts;

public enum CombatFactParticipantKind { Character, Wildlife }

[Serializable]
public sealed class CombatFactParticipantData
{
    public CombatFactParticipantKind kind;
    public string id = string.Empty;
    public string name = string.Empty;
    public CombatFactParticipantData Clone() => (CombatFactParticipantData)MemberwiseClone();
    public void Validate(bool requireName = true)
    {
        if (!Enum.IsDefined(typeof(CombatFactParticipantKind), kind) || !DomainFactValueSyntax.IsIdentity(id)
            || kind == CombatFactParticipantKind.Character && !new CharacterId(id).IsValid
            || requireName && !DomainFactValueSyntax.IsText(name, 256, false))
            throw new ArgumentException("combat-fact-participant-invalid");
    }
}

/// <summary>The social owner supplies a frozen conflict, not a prepared log token.</summary>
[Serializable]
public sealed class CombatConflictFactData
{
    public string operationId = string.Empty;
    public CombatFactParticipantData instigator = new();
    public CombatFactParticipantData target = new();
    public string conflictId = string.Empty;
    public float severity;
    public float committedMoodDelta;
    public CharacterCommandOrigin origin;
    public int absoluteDay;
    public bool alreadyPublished;
    public bool IsEmpty => operationId == string.Empty && conflictId == string.Empty && severity == 0
        && committedMoodDelta == 0 && origin == default && absoluteDay == 0 && !alreadyPublished
        && EmptyParticipant(instigator) && EmptyParticipant(target);
    private static bool EmptyParticipant(CombatFactParticipantData value) => value != null
        && value.kind == default && value.id == string.Empty && value.name == string.Empty;
    public CombatConflictFactData Clone()
    {
        var copy = (CombatConflictFactData)MemberwiseClone();
        copy.instigator = instigator?.Clone(); copy.target = target?.Clone();
        return copy;
    }
    public void Validate()
    {
        instigator?.Validate(); target?.Validate();
        if (!DomainFactValueSyntax.IsIdentity(operationId) || !DomainFactValueSyntax.IsIdentity(conflictId)
            || instigator == null || target == null || instigator.kind != CombatFactParticipantKind.Character
            || target.kind != CombatFactParticipantKind.Character || instigator.id == target.id
            || !float.IsFinite(severity) || severity < 0 || !float.IsFinite(committedMoodDelta)
            || !Enum.IsDefined(typeof(CharacterCommandOrigin), origin) || absoluteDay < 1)
            throw new ArgumentException("combat-conflict-fact-invalid");
    }
}

[Serializable]
public sealed class CombatActionFactData
{
    public string operationId = string.Empty;
    public long attackRevision;
    public int absoluteDay;
    public CombatFactParticipantData attacker;
    public CombatFactParticipantData target;
    public int targetX;
    public int targetY;
    public CombatActionEvidenceData evidence;
    public float healthBefore;
    public float maxHealthBefore;
    public float healthAfter;
    public float maxHealthAfter;
    public bool downedBefore;
    public bool downedAfter;
    public string damageSource = string.Empty;
    public CharacterCommandOrigin origin;
    public bool hasConflict;
    public CombatConflictFactData conflict = new();
    public CombatActionFactData Clone()
    {
        var copy = (CombatActionFactData)MemberwiseClone();
        copy.attacker = attacker?.Clone(); copy.target = target?.Clone();
        copy.evidence = evidence?.Clone(); copy.conflict = conflict?.Clone();
        return copy;
    }
}

/// <summary>Actual state deltas only. Observer policy chooses records and importance.</summary>
public sealed class CombatActionFact
{
    private readonly CombatActionFactData value;
    public bool IsProposal { get; }
    public string OperationId => value.operationId;
    public long AttackRevision => value.attackRevision;
    public string Identity => OperationId + ":" + AttackRevision.ToString(CultureInfo.InvariantCulture);
    public CombatActionFact(CombatActionFactData value) : this(value, false) { }
    private CombatActionFact(CombatActionFactData value, bool proposal)
    {
        this.value = value?.Clone() ?? throw new ArgumentNullException(nameof(value));
        IsProposal = proposal;
        Validate(this.value, proposal);
    }
    public static CombatActionFact Proposal(string operationId, long revision, int day,
        CombatFactParticipantData target, CombatConflictFactData conflict = null) => new(
        new CombatActionFactData { operationId = operationId, attackRevision = revision,
            absoluteDay = day, target = target, hasConflict = conflict != null, conflict = conflict ?? new() }, true);
    public CombatActionFactData Snapshot() => value.Clone();
    public bool MatchesProposal(CombatActionFact proposed)
    {
        if (proposed == null || !proposed.IsProposal) return false;
        var before = proposed.value;
        return Identity == proposed.Identity && value.absoluteDay == before.absoluteDay
            && value.target.kind == before.target.kind && value.target.id == before.target.id
            && value.hasConflict == before.hasConflict && (!value.hasConflict || SameConflict(value.conflict, before.conflict));
    }
    private static bool SameConflict(CombatConflictFactData a, CombatConflictFactData b) =>
        a == null ? b == null : b != null && a.operationId == b.operationId && a.conflictId == b.conflictId
        && a.instigator.id == b.instigator.id && a.instigator.name == b.instigator.name
        && a.target.id == b.target.id && a.target.name == b.target.name && a.severity == b.severity
        && a.committedMoodDelta == b.committedMoodDelta && a.origin == b.origin
        && a.absoluteDay == b.absoluteDay && a.alreadyPublished == b.alreadyPublished;
    private static void Validate(CombatActionFactData data, bool proposal)
    {
        if (!DomainFactValueSyntax.IsIdentity(data.operationId) || data.attackRevision < 0
            || data.absoluteDay < 1 || data.target == null)
            throw new ArgumentException("combat-fact-identity-invalid");
        data.target.Validate(!proposal);
        // Unity serializes inline classes by value; absence is an explicit flag
        // and empty value, never a reliance on null surviving JsonUtility.
        if (data.conflict == null || !data.hasConflict && !data.conflict.IsEmpty)
            throw new ArgumentException("combat-fact-conflict-presence-invalid");
        if (data.hasConflict) data.conflict.Validate();
        if (proposal) return;
        data.attacker?.Validate();
        if (data.attacker == null || data.evidence == null || !data.evidence.IsValid || !data.evidence.executed
            || data.attacker.kind == data.target.kind && data.attacker.id == data.target.id
            || !DomainFactValueSyntax.IsText(data.damageSource, 1024, false)
            || !Enum.IsDefined(typeof(CharacterCommandOrigin), data.origin)
            || !ValidHealth(data.healthBefore, data.maxHealthBefore)
            || !ValidHealth(data.healthAfter, data.maxHealthAfter) || data.healthAfter > data.healthBefore
            || (!data.evidence.hit || data.evidence.coverBlocked) && data.healthAfter != data.healthBefore)
            throw new ArgumentException("combat-fact-applied-state-invalid");
        if (data.hasConflict && (data.conflict.operationId != data.operationId
            || data.attacker.kind != CombatFactParticipantKind.Character || data.target.kind != CombatFactParticipantKind.Character
            || !((data.attacker.id == data.conflict.instigator.id && data.target.id == data.conflict.target.id)
                || (data.attacker.id == data.conflict.target.id && data.target.id == data.conflict.instigator.id))))
            throw new ArgumentException("combat-fact-conflict-participant-mismatch");
    }
    private static bool ValidHealth(float health, float maximum) => float.IsFinite(health)
        && float.IsFinite(maximum) && maximum > 0 && health >= 0 && health <= maximum;
}

public readonly struct ReservedCombatAction
{
    public ReservedCombatAction(DomainFactLease lease, CombatActionFact proposal)
    { Lease = lease; Proposal = proposal; }
    public DomainFactLease Lease { get; }
    public CombatActionFact Proposal { get; }
    public string OperationId => Proposal?.OperationId ?? string.Empty;
    public long AttackRevision => Proposal?.AttackRevision ?? -1;
    public bool IsValid => Lease.IsValid && Proposal != null;
}

public readonly struct CombatOutcomeParentContext
{
    private readonly CombatConflictFactData conflict;
    public CombatOutcomeParentContext(CombatConflictFactData conflict)
    { this.conflict = conflict?.Clone() ?? throw new ArgumentNullException(nameof(conflict)); this.conflict.Validate(); }
    public bool HasParent => conflict != null;
    public CombatConflictFactData Snapshot() => conflict?.Clone();
    public CombatOutcomeParentContext AsCommitted()
    {
        if (conflict == null) return default;
        var copy = conflict.Clone(); copy.alreadyPublished = true;
        return new CombatOutcomeParentContext(copy);
    }
}
