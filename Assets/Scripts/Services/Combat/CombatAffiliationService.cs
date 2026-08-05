using System;
using System.Linq;
using UnityEngine;

public sealed class CombatAffiliationService : ICombatAffiliationService
{
    private readonly InvasionDirectorRuntime invasionDirector;
    private readonly IStaffDiscontentRuntimeService discontentRuntime;
    private readonly ICharacterDeprivationQuery deprivationRuntime;

    public CombatAffiliationService(
        InvasionSceneRuntimeReferences invasionRuntimes,
        IStaffDiscontentRuntimeService discontentRuntime,
        ICharacterDeprivationQuery deprivationRuntime)
    {
        invasionDirector = (invasionRuntimes
                ?? throw new ArgumentNullException(nameof(invasionRuntimes)))
            .Director
            ?? throw new InvalidOperationException(
                $"{nameof(CombatAffiliationService)} requires a loaded {nameof(InvasionDirectorRuntime)}.");
        this.discontentRuntime = discontentRuntime
            ?? throw new ArgumentNullException(nameof(discontentRuntime));
        this.deprivationRuntime = deprivationRuntime
            ?? throw new ArgumentNullException(nameof(deprivationRuntime));
    }

    public CombatRelationship GetRelationship(
        CombatParticipantRef source,
        CombatParticipantRef target)
    {
        if (!source.IsValid || !target.IsValid)
        {
            return CombatRelationship.Neutral;
        }

        if (string.Equals(source.Id, target.Id, StringComparison.Ordinal))
        {
            return CombatRelationship.Ally;
        }

        if (source.IsWildlife || target.IsWildlife)
        {
            return GetWildlifeRelationship(source, target);
        }

        bool sourceIntruder = IsInvasionIntruder(source.Character);
        bool targetIntruder = IsInvasionIntruder(target.Character);
        if (sourceIntruder || targetIntruder)
        {
            return sourceIntruder == targetIntruder
                ? CombatRelationship.Ally
                : CombatRelationship.Hostile;
        }

        bool sourceViolent = IsViolentHumanoid(source.Character);
        bool targetViolent = IsViolentHumanoid(target.Character);
        if (sourceViolent || targetViolent)
        {
            return sourceViolent == targetViolent
                ? CombatRelationship.Ally
                : CombatRelationship.Hostile;
        }

        bool sourceCustomer = source.Character?.Identity?.Data?.characterType == CharacterType.Customer;
        bool targetCustomer = target.Character?.Identity?.Data?.characterType == CharacterType.Customer;
        if (sourceCustomer || targetCustomer)
        {
            return sourceCustomer == targetCustomer
                ? CombatRelationship.Neutral
                : CombatRelationship.Neutral;
        }

        return CombatRelationship.Ally;
    }

    public bool IsProtectedFromAutomaticFire(
        CombatParticipantRef source,
        CombatParticipantRef target)
    {
        return GetRelationship(source, target) != CombatRelationship.Hostile;
    }

    private CombatRelationship GetWildlifeRelationship(
        CombatParticipantRef source,
        CombatParticipantRef target)
    {
        WildlifeActor sourceWildlife = source.Wildlife;
        WildlifeActor targetWildlife = target.Wildlife;
        if (targetWildlife != null && source.Character != null)
        {
            return targetWildlife.HuntDesignated
                ? CombatRelationship.Hostile
                : CombatRelationship.Neutral;
        }

        if (sourceWildlife != null && target.Character != null)
        {
            return sourceWildlife.State is WildlifeState.Retaliating
                or WildlifeState.PredatorStalking
                ? CombatRelationship.Hostile
                : CombatRelationship.Neutral;
        }

        if (sourceWildlife != null && targetWildlife != null)
        {
            bool predatory = sourceWildlife.State == WildlifeState.PredatorStalking
                || targetWildlife.State == WildlifeState.PredatorStalking;
            return predatory ? CombatRelationship.Hostile : CombatRelationship.Neutral;
        }

        return CombatRelationship.Neutral;
    }

    private bool IsInvasionIntruder(CharacterActor actor)
    {
        return actor != null
            && invasionDirector.ActiveIntruders.Any(intruder =>
                intruder?.IntruderActor == actor);
    }

    private bool IsViolentHumanoid(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        if (discontentRuntime.IsRebellionTarget(actor))
        {
            return true;
        }

        return deprivationRuntime.TryGetSnapshot(
                actor,
                out CharacterDeprivationSnapshot snapshot)
            && snapshot.Breakdown?.active == true
            && snapshot.Breakdown.kind == CharacterBreakdownKind.ViolentImpulse;
    }
}

public sealed class CombatFiringSolutionService : ICombatFiringSolutionService
{
    private readonly ICombatLineOfSightService lineOfSight;
    private readonly ICombatCoverQuery coverQuery;
    private readonly ICombatAffiliationService affiliation;
    private readonly ICombatRandomSource random;

    public CombatFiringSolutionService(
        ICombatLineOfSightService lineOfSight,
        ICombatCoverQuery coverQuery,
        ICombatAffiliationService affiliation,
        ICombatRandomSource random)
    {
        this.lineOfSight = lineOfSight ?? throw new ArgumentNullException(nameof(lineOfSight));
        this.coverQuery = coverQuery ?? throw new ArgumentNullException(nameof(coverQuery));
        this.affiliation = affiliation ?? throw new ArgumentNullException(nameof(affiliation));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public CombatFiringSolution Evaluate(
        Grid grid,
        CombatParticipantRef attacker,
        CombatParticipantRef intendedTarget)
    {
        if (grid == null || !attacker.IsValid || !intendedTarget.IsValid)
        {
            return new CombatFiringSolution(
                attacker,
                intendedTarget,
                CombatRelationship.Neutral,
                0,
                default,
                default,
                false,
                "사격 대상이 유효하지 않습니다.");
        }

        CombatRelationship relationship = affiliation.GetRelationship(attacker, intendedTarget);
        CombatLineOfSightResult sight = lineOfSight.Evaluate(
            grid,
            attacker.GridPosition,
            intendedTarget.GridPosition,
            attacker.Id,
            intendedTarget.Id);
        CombatCoverSnapshot cover = coverQuery.GetCover(
            grid,
            attacker.GridPosition,
            intendedTarget.GridPosition);
        int distance = Mathf.Abs(attacker.GridPosition.x - intendedTarget.GridPosition.x)
            + Mathf.Abs(attacker.GridPosition.y - intendedTarget.GridPosition.y);
        bool allowed = sight.HasLineOfSight
            && relationship == CombatRelationship.Hostile
            && !sight.FriendlyFireRisk;
        string reason = !sight.HasLineOfSight
            ? sight.FailureReason
            : relationship != CombatRelationship.Hostile
                ? "보호 대상"
                : sight.FriendlyFireRisk
                    ? "아군 또는 중립이 사선에 있음"
                    : string.Empty;
        return new CombatFiringSolution(
            attacker,
            intendedTarget,
            relationship,
            distance,
            sight,
            cover,
            allowed,
            reason);
    }

    public CombatParticipantRef ResolveImpactTarget(
        CombatFiringSolution solution,
        bool forceFire,
        out bool intercepted)
    {
        intercepted = false;
        if (!forceFire)
        {
            return solution.AutoFireAllowed
                ? solution.IntendedTarget
                : default;
        }

        foreach (CombatShotTraceOccupant occupant in
            solution.LineOfSight.Trace.IntermediateOccupants)
        {
            if (occupant.Participant.IsValid && random.Next01() < 0.35f)
            {
                intercepted = true;
                return occupant.Participant;
            }
        }

        return solution.IntendedTarget;
    }
}
