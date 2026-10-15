using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class OffenseBattleCommandFactData
{
    public string battleId = string.Empty;
    public string expeditionId = string.Empty;
    public string actorId = string.Empty;
    public string actorDisplayName = string.Empty;
    public string targetId = string.Empty;
    public string targetDisplayName = string.Empty;
    public string abilityId = string.Empty;
    // Equipment observed after an applied command (switch/throw may change it).
    public string weaponInstanceId = string.Empty;
    public string weaponDefinitionId = string.Empty;
    public long commandId;
    public int absoluteDay;
    public long absoluteHour;
    public OffenseBattleActionType actionType;
    public bool resolved;
    public bool targetWasDead;
    public bool targetIsDead;
    public bool hit;
    public bool shieldBlocked;
    public bool coverBlocked;
    public float targetHealthBefore;
    public float targetHealthAfter;
    public float amount;

    public OffenseBattleCommandFactData Clone() =>
        (OffenseBattleCommandFactData)MemberwiseClone();
}

/// <summary>
/// Frozen proposal or applied battle command evidence without a live session,
/// combatant, equipment snapshot, or narrative transaction.
/// </summary>
public sealed class OffenseBattleCommandFact
{
    private readonly OffenseBattleCommandFactData data;

    public OffenseBattleCommandFact(OffenseBattleCommandFactData source)
    {
        data = (source ?? throw new ArgumentNullException(nameof(source))).Clone();
        Validate(data);
    }

    public OffenseBattleCommandFactData Snapshot() => data.Clone();

    public bool MatchesProposal(OffenseBattleCommandFact proposed)
    {
        if (!data.resolved || proposed == null || proposed.data.resolved)
            return false;
        OffenseBattleCommandFactData before = proposed.data;
        return data.battleId == before.battleId
            && data.expeditionId == before.expeditionId
            && data.actorId == before.actorId
            && data.actorDisplayName == before.actorDisplayName
            && data.targetId == before.targetId
            && data.targetDisplayName == before.targetDisplayName
            && data.abilityId == before.abilityId
            && data.commandId == before.commandId
            && data.absoluteDay == before.absoluteDay
            && data.absoluteHour == before.absoluteHour
            && data.actionType == before.actionType
            && data.targetWasDead == before.targetWasDead
            && data.targetHealthBefore == before.targetHealthBefore;
    }

    private static void Validate(OffenseBattleCommandFactData value)
    {
        if (!DomainFactValueSyntax.IsIdentity(value.battleId)
            || !DomainFactValueSyntax.IsIdentity(value.expeditionId)
            || !DomainFactValueSyntax.IsIdentity(value.actorId)
            || !OptionalIdentity(value.targetId)
            || !OptionalIdentity(value.abilityId)
            || !OptionalIdentity(value.weaponInstanceId)
            || !OptionalIdentity(value.weaponDefinitionId)
            || !RawName(value.actorDisplayName)
            || !RawName(value.targetDisplayName)
            || value.commandId < 1L
            || value.absoluteDay < 1
            || value.absoluteHour < 0
            || !Enum.IsDefined(typeof(OffenseBattleActionType), value.actionType)
            || !NonNegativeFinite(value.targetHealthBefore)
            || !NonNegativeFinite(value.targetHealthAfter)
            || !NonNegativeFinite(value.amount)
            || !value.resolved && (value.targetHealthAfter != value.targetHealthBefore
                || value.amount != 0f || value.hit || value.shieldBlocked
                || value.coverBlocked || value.targetIsDead != value.targetWasDead))
        {
            throw new ArgumentException("offense-battle-command-fact-invalid");
        }
    }

    private static bool OptionalIdentity(string value) =>
        value == string.Empty || DomainFactValueSyntax.IsIdentity(value);

    private static bool RawName(string value) =>
        DomainFactValueSyntax.IsText(
            value,
            DomainFactValueSyntax.MaximumNameCharacters,
            allowEmpty: true);

    private static bool NonNegativeFinite(float value) =>
        float.IsFinite(value) && value >= 0f;
}
