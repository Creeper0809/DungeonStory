using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public readonly struct WildlifeCompanionAssignmentSnapshot
{
    public WildlifeCompanionAssignmentSnapshot(
        string wildlifeId,
        CharacterId ownerId,
        float nextAttackAt,
        Vector2Int penPosition)
    {
        WildlifeId = wildlifeId ?? string.Empty;
        OwnerId = ownerId;
        NextAttackAt = nextAttackAt;
        PenPosition = penPosition;
    }

    public string WildlifeId { get; }
    public CharacterId OwnerId { get; }
    public float NextAttackAt { get; }
    public Vector2Int PenPosition { get; }
}

public interface IWildlifeCompanionRoleQuery
{
    bool TryGetCompanion(
        string wildlifeId,
        out WildlifeCompanionAssignmentSnapshot assignment);
    void CopyCompanionAssignments(
        List<WildlifeCompanionAssignmentSnapshot> destination);
}

public interface IWildlifeCompanionRoleCommand
{
    bool TryAssignCompanion(
        string wildlifeId,
        CharacterId ownerId,
        out string failureReason);
    bool TryClearCompanion(string wildlifeId, out string failureReason);
}

public interface IWildlifeCompanionAffiliationQuery
{
    bool TryGetCompanionOwner(
        WildlifeActor wildlife,
        out CharacterActor owner);
}

public interface IWildlifeCompanionRoleStateCommand :
    IWildlifeCompanionRoleQuery
{
    bool TryArmAttackCooldown(
        string wildlifeId,
        CharacterId expectedOwnerId,
        float expectedReadyAt,
        float nextAttackAt,
        out WildlifeCompanionAssignmentSnapshot assignment);
    void ClearInvalidCompanion(string wildlifeId, string reason);
}
