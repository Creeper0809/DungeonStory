using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatRelationship
{
    Ally = 0,
    Neutral = 1,
    Hostile = 2
}

public readonly struct CombatParticipantRef
{
    public CombatParticipantRef(CharacterActor character)
    {
        Character = character;
        Wildlife = null;
    }

    public CombatParticipantRef(WildlifeActor wildlife)
    {
        Character = null;
        Wildlife = wildlife;
    }

    public CharacterActor Character { get; }
    public WildlifeActor Wildlife { get; }
    public bool IsCharacter => Character != null;
    public bool IsWildlife => Wildlife != null;
    public bool IsValid => Character != null || Wildlife != null;
    public bool IsDead => Character != null ? Character.IsDead : Wildlife == null || !Wildlife.IsAlive;
    public string Id => Character != null
        ? Character.Identity?.PersistentId ?? $"scene-actor:{Character.GetInstanceID()}"
        : Wildlife != null ? Wildlife.WildlifeId : string.Empty;
    public string DisplayName => Character != null
        ? Character.Identity?.DisplayName ?? Character.name
        : Wildlife != null ? Wildlife.DisplayName : string.Empty;
    public Vector2Int GridPosition => Character != null
        ? Character.GetNowXY()
        : Wildlife != null ? Wildlife.GridPosition : default;
}

public readonly struct CombatShotTraceOccupant
{
    public CombatShotTraceOccupant(
        CombatParticipantRef participant,
        Vector2Int cell,
        int distanceFromAttacker,
        CombatRelationship relationship)
    {
        Participant = participant;
        Cell = cell;
        DistanceFromAttacker = Mathf.Max(0, distanceFromAttacker);
        Relationship = relationship;
    }

    public CombatParticipantRef Participant { get; }
    public Vector2Int Cell { get; }
    public int DistanceFromAttacker { get; }
    public CombatRelationship Relationship { get; }
    public bool IsProtected => Relationship != CombatRelationship.Hostile;
}

public readonly struct CombatShotTrace
{
    public CombatShotTrace(
        IReadOnlyList<Vector2Int> cells,
        IReadOnlyList<CombatShotTraceOccupant> intermediateOccupants,
        Vector2Int blockingCell,
        string blockingReason)
    {
        Cells = cells ?? Array.Empty<Vector2Int>();
        IntermediateOccupants = intermediateOccupants ?? Array.Empty<CombatShotTraceOccupant>();
        BlockingCell = blockingCell;
        BlockingReason = blockingReason ?? string.Empty;
    }

    public IReadOnlyList<Vector2Int> Cells { get; }
    public IReadOnlyList<CombatShotTraceOccupant> IntermediateOccupants { get; }
    public Vector2Int BlockingCell { get; }
    public string BlockingReason { get; }
    public bool HasProtectedOccupant
    {
        get
        {
            foreach (CombatShotTraceOccupant occupant in IntermediateOccupants)
            {
                if (occupant.IsProtected)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

public readonly struct CombatFiringSolution
{
    public CombatFiringSolution(
        CombatParticipantRef attacker,
        CombatParticipantRef intendedTarget,
        CombatRelationship targetRelationship,
        int distance,
        CombatLineOfSightResult lineOfSight,
        CombatCoverSnapshot cover,
        bool autoFireAllowed,
        string failureReason)
    {
        Attacker = attacker;
        IntendedTarget = intendedTarget;
        TargetRelationship = targetRelationship;
        Distance = Mathf.Max(0, distance);
        LineOfSight = lineOfSight;
        Cover = cover;
        AutoFireAllowed = autoFireAllowed;
        FailureReason = failureReason ?? string.Empty;
    }

    public CombatParticipantRef Attacker { get; }
    public CombatParticipantRef IntendedTarget { get; }
    public CombatRelationship TargetRelationship { get; }
    public int Distance { get; }
    public CombatLineOfSightResult LineOfSight { get; }
    public CombatCoverSnapshot Cover { get; }
    public bool AutoFireAllowed { get; }
    public string FailureReason { get; }
}

public interface ICombatAffiliationService
{
    CombatRelationship GetRelationship(
        CombatParticipantRef source,
        CombatParticipantRef target);
    bool IsProtectedFromAutomaticFire(
        CombatParticipantRef source,
        CombatParticipantRef target);
}

public interface ICombatFiringSolutionService
{
    CombatFiringSolution Evaluate(
        Grid grid,
        CombatParticipantRef attacker,
        CombatParticipantRef intendedTarget);
    CombatParticipantRef ResolveImpactTarget(
        CombatFiringSolution solution,
        bool forceFire,
        out bool intercepted);
}
