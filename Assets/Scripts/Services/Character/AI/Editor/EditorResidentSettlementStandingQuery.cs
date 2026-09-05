#if UNITY_EDITOR
public sealed class EditorResidentSettlementStandingQuery :
    ICharacterSettlementStandingQuery
{
    public CharacterSettlementStanding GetStanding(CharacterActor actor) =>
        actor == null
            ? CharacterSettlementStanding.Unknown
            : StaffDiscontentService.IsTrackableStaff(actor)
                ? CharacterSettlementStanding.Resident
                : actor.Identity?.CharacterType == CharacterType.Customer
                    || actor.characterType == CharacterType.Customer
                    ? CharacterSettlementStanding.Visitor
                    : CharacterSettlementStanding.PreparedCandidate;

    public CharacterSettlementStanding GetStanding(string persistentCharacterId) =>
        CharacterSettlementStanding.Unknown;

    public CharacterSettlementPopulationSnapshot GetSettlementPopulation() =>
        new CharacterSettlementPopulationSnapshot(0, 0);

    public bool IsFormalResident(CharacterActor actor) =>
        GetStanding(actor) == CharacterSettlementStanding.Resident;

    public bool IsMinion(CharacterActor actor) => false;

    public bool CanJoinExpedition(
        CharacterActor actor,
        out string failureReason)
    {
        failureReason = string.Empty;
        return actor != null;
    }

    public bool CanParticipateInMentoring(
        CharacterActor actor,
        out string failureReason)
    {
        failureReason = string.Empty;
        return actor != null;
    }

    public bool IsWorkAllowed(
        CharacterActor actor,
        WorkTypeId workTypeId,
        out string failureReason)
    {
        failureReason = string.Empty;
        return actor != null && workTypeId.IsValid;
    }

    public float GetApprovedWorkExperienceMultiplier(CharacterActor actor) => 1f;
}
#endif
