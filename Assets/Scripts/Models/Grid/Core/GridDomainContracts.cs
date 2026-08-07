using System;
using UnityEngine;

public enum DoorAccessOverrideKind
{
    None = 0,
    DirectCommand = 1,
    EscortPass = 2,
    CaptiveEscape = 3,
    IntruderBreach = 4
}

public enum GridTraversalSubjectKind
{
    None = 0,
    Character = 1,
    Wildlife = 2
}

public enum GridMovementIntent
{
    General = 0,
    SafeChore = 1,
    Apprenticeship = 2,
    CombatSupply = 3,
    Combat = 4,
    EscapeHazard = 5,
    Escort = 6
}

public readonly struct ChildSafetyAuthorizationToken :
    IEquatable<ChildSafetyAuthorizationToken>
{
    public ChildSafetyAuthorizationToken(
        CharacterId characterId,
        string workOrderId,
        int policyVersion)
    {
        CharacterId = characterId;
        WorkOrderId = workOrderId?.Trim() ?? string.Empty;
        PolicyVersion = policyVersion;
    }

    public CharacterId CharacterId { get; }
    public string WorkOrderId { get; }
    public int PolicyVersion { get; }
    public bool IsValid => CharacterId.IsValid
        && WorkOrderId.Length > 0
        && PolicyVersion > 0;
    public bool Equals(ChildSafetyAuthorizationToken other) =>
        CharacterId.Equals(other.CharacterId)
        && string.Equals(WorkOrderId, other.WorkOrderId, StringComparison.Ordinal)
        && PolicyVersion == other.PolicyVersion;
    public override bool Equals(object obj) =>
        obj is ChildSafetyAuthorizationToken other && Equals(other);
    public override int GetHashCode() =>
        HashCode.Combine(CharacterId, WorkOrderId, PolicyVersion);
}

public readonly struct GridTraversalContext : IEquatable<GridTraversalContext>
{
    private GridTraversalContext(
        GridTraversalSubjectKind subjectKind,
        CharacterId characterId,
        string wildlifeId,
        GridMovementIntent movementIntent,
        ChildSafetyAuthorizationToken safetyAuthorization,
        DoorAccessOverrideKind overrideKind,
        int environmentVersion,
        int combatRiskVersion,
        int lifeStageVersion,
        int safetyPolicyVersion)
    {
        SubjectKind = subjectKind;
        CharacterId = characterId;
        WildlifeId = wildlifeId?.Trim() ?? string.Empty;
        MovementIntent = movementIntent;
        SafetyAuthorization = safetyAuthorization;
        OverrideKind = overrideKind;
        EnvironmentVersion = environmentVersion;
        CombatRiskVersion = combatRiskVersion;
        LifeStageVersion = lifeStageVersion;
        SafetyPolicyVersion = safetyPolicyVersion;
    }

    public GridTraversalSubjectKind SubjectKind { get; }
    public CharacterId CharacterId { get; }
    public string WildlifeId { get; }
    public GridMovementIntent MovementIntent { get; }
    public ChildSafetyAuthorizationToken SafetyAuthorization { get; }
    public DoorAccessOverrideKind OverrideKind { get; }
    public int EnvironmentVersion { get; }
    public int CombatRiskVersion { get; }
    public int LifeStageVersion { get; }
    public int SafetyPolicyVersion { get; }
    public bool HasSubject => SubjectKind != GridTraversalSubjectKind.None;

    public static GridTraversalContext ForCharacter(
        CharacterId characterId,
        DoorAccessOverrideKind overrideKind = DoorAccessOverrideKind.None,
        GridMovementIntent movementIntent = GridMovementIntent.General,
        ChildSafetyAuthorizationToken safetyAuthorization = default,
        int environmentVersion = 0,
        int combatRiskVersion = 0,
        int lifeStageVersion = 0,
        int safetyPolicyVersion = 0)
    {
        if (!characterId.IsValid)
            throw new ArgumentException("A valid CharacterId is required.", nameof(characterId));
        return new GridTraversalContext(
            GridTraversalSubjectKind.Character,
            characterId,
            string.Empty,
            movementIntent,
            safetyAuthorization,
            overrideKind,
            environmentVersion,
            combatRiskVersion,
            lifeStageVersion,
            safetyPolicyVersion);
    }

    public static GridTraversalContext ForWildlife(
        string wildlifeId,
        DoorAccessOverrideKind overrideKind = DoorAccessOverrideKind.None)
    {
        if (string.IsNullOrWhiteSpace(wildlifeId))
            throw new ArgumentException("A stable wildlife id is required.", nameof(wildlifeId));
        return new GridTraversalContext(
            GridTraversalSubjectKind.Wildlife,
            default,
            wildlifeId,
            GridMovementIntent.General,
            default,
            overrideKind,
            0,
            0,
            0,
            0);
    }

    public bool Equals(GridTraversalContext other)
    {
        return SubjectKind == other.SubjectKind
            && CharacterId.Equals(other.CharacterId)
            && string.Equals(WildlifeId, other.WildlifeId, StringComparison.Ordinal)
            && MovementIntent == other.MovementIntent
            && SafetyAuthorization.Equals(other.SafetyAuthorization)
            && OverrideKind == other.OverrideKind
            && EnvironmentVersion == other.EnvironmentVersion
            && CombatRiskVersion == other.CombatRiskVersion
            && LifeStageVersion == other.LifeStageVersion
            && SafetyPolicyVersion == other.SafetyPolicyVersion;
    }

    public override bool Equals(object obj)
    {
        return obj is GridTraversalContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)SubjectKind;
            hash = (hash * 397) ^ CharacterId.GetHashCode();
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(WildlifeId ?? string.Empty);
            hash = (hash * 397) ^ (int)MovementIntent;
            hash = (hash * 397) ^ SafetyAuthorization.GetHashCode();
            hash = (hash * 397) ^ (int)OverrideKind;
            hash = (hash * 397) ^ EnvironmentVersion;
            hash = (hash * 397) ^ CombatRiskVersion;
            hash = (hash * 397) ^ LifeStageVersion;
            return (hash * 397) ^ SafetyPolicyVersion;
        }
    }
}

public interface IGridTraversalAccessQuery
{
    int DoorAccessVersion { get; }
    bool CanTraverse(
        Grid grid,
        Vector2Int position,
        GridTraversalContext context,
        out string denialReason);
}

public interface IGridPathPerformanceRecorder
{
    bool DetailedCollectionEnabled { get; }
    void RecordGridPathSearch(double elapsedMilliseconds);
}
