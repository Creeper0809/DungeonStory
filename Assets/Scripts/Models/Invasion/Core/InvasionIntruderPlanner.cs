using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public readonly struct InvasionIntruderFacilityTargetSnapshot
{
    public InvasionIntruderFacilityTargetSnapshot(
        DefenseBreachTargetSnapshot breachTarget,
        bool damaged,
        bool movement,
        bool facility,
        bool defenseFacility,
        int constructionValue,
        int moveCost)
    {
        BreachTarget = breachTarget;
        Damaged = damaged;
        Movement = movement;
        Facility = facility;
        DefenseFacility = defenseFacility;
        ConstructionValue = constructionValue;
        MoveCost = moveCost;
    }

    public DefenseBreachTargetSnapshot BreachTarget { get; }
    public BuildingInstanceId TargetId => BreachTarget.TargetId;
    public bool Damaged { get; }
    public bool Movement { get; }
    public bool Facility { get; }
    public bool DefenseFacility { get; }
    public int ConstructionValue { get; }
    public int MoveCost { get; }

    public bool Damageable =>
        TargetId.IsValid
        && BreachTarget.Breachable
        && !BreachTarget.Destroyed
        && !Damaged
        && !Movement
        && Facility;
}

public sealed class InvasionIntruderPathSearchSnapshot
{
    public InvasionIntruderPathSearchSnapshot(
        Vector2Int start,
        IEnumerable<Vector2Int> reachableWalkablePositions,
        IEnumerable<InvasionIntruderFacilityTargetSnapshot> reachableFacilities)
    {
        Start = start;
        ReachableWalkablePositions = Array.AsReadOnly(
            (reachableWalkablePositions ?? Array.Empty<Vector2Int>()).ToArray());
        ReachableFacilities = Array.AsReadOnly(
            (reachableFacilities
                ?? Array.Empty<InvasionIntruderFacilityTargetSnapshot>()).ToArray());
    }

    public Vector2Int Start { get; }
    public IReadOnlyList<Vector2Int> ReachableWalkablePositions { get; }
    public IReadOnlyList<InvasionIntruderFacilityTargetSnapshot> ReachableFacilities { get; }
}

public enum InvasionIntruderRouteTargetKind
{
    None = 0,
    Owner = 1,
    Facility = 2,
    Explore = 3
}

public readonly struct InvasionIntruderRoutePlan
{
    public InvasionIntruderRoutePlan(
        InvasionIntruderRouteTargetKind targetKind,
        Vector2Int destination,
        BuildingInstanceId priorityTargetId,
        bool directPath)
    {
        TargetKind = targetKind;
        Destination = destination;
        PriorityTargetId = priorityTargetId;
        DirectPath = directPath;
    }

    public InvasionIntruderRouteTargetKind TargetKind { get; }
    public Vector2Int Destination { get; }
    public BuildingInstanceId PriorityTargetId { get; }
    public bool DirectPath { get; }
}

public static class InvasionIntruderPlanningRules
{
    public static float CalculateFocus(
        float elapsedSeconds,
        float secondsToFullFocus)
    {
        return Mathf.Clamp01(
            elapsedSeconds / Mathf.Max(0.1f, secondsToFullFocus));
    }

    public static InvasionIntruderRoutePlan Plan(
        InvasionIntruderPathSearchSnapshot search,
        Vector2Int ownerPosition,
        float focus,
        IRandomStream randomStream,
        InvasionIntruderPatternDefinition pattern,
        ISet<BuildingInstanceId> excludedFacilityIds = null,
        int damagedFacilityCount = 0)
    {
        if (search == null)
        {
            throw new ArgumentNullException(nameof(search));
        }
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        if (damagedFacilityCount < pattern.maxFacilityDamageCount
            && focus < pattern.facilityDiversionFocus
            && TrySelectPriorityTarget(
                search.ReachableFacilities,
                pattern.targetPreference,
                excludedFacilityIds,
                out InvasionIntruderFacilityTargetSnapshot target))
        {
            return new InvasionIntruderRoutePlan(
                InvasionIntruderRouteTargetKind.Facility,
                default,
                target.TargetId,
                false);
        }

        if (focus >= pattern.directOwnerFocus)
        {
            return new InvasionIntruderRoutePlan(
                InvasionIntruderRouteTargetKind.Owner,
                ownerPosition,
                default,
                true);
        }

        Vector2Int exploreTarget = SelectExploreTarget(
            search.ReachableWalkablePositions,
            search.Start,
            ownerPosition,
            focus,
            randomStream);
        if (exploreTarget == search.Start)
        {
            return new InvasionIntruderRoutePlan(
                InvasionIntruderRouteTargetKind.Owner,
                ownerPosition,
                default,
                true);
        }

        return new InvasionIntruderRoutePlan(
            InvasionIntruderRouteTargetKind.Explore,
            exploreTarget,
            default,
            false);
    }

    public static Vector2Int SelectExploreTarget(
        IEnumerable<Vector2Int> reachableWalkablePositions,
        Vector2Int start,
        Vector2Int ownerPosition,
        float focus,
        IRandomStream randomStream)
    {
        if (randomStream == null)
        {
            throw new ArgumentNullException(nameof(randomStream));
        }

        List<Vector2Int> candidates = (reachableWalkablePositions
                ?? Array.Empty<Vector2Int>())
            .Where(position => position != start)
            .ToList();
        if (candidates.Count == 0)
        {
            return start;
        }

        if (candidates.Count > 1)
        {
            candidates.Remove(ownerPosition);
        }
        if (candidates.Count == 0)
        {
            return start;
        }

        int maxDistance = Mathf.Max(
            1,
            candidates.Max(position => Manhattan(position, ownerPosition)));
        float clampedFocus = Mathf.Clamp01(focus);
        return candidates
            .OrderByDescending(position =>
            {
                float closeness = 1f
                    - (float)Manhattan(position, ownerPosition) / maxDistance;
                float explorationNoise = randomStream.NextFloat();
                return Mathf.Lerp(
                    explorationNoise,
                    closeness,
                    clampedFocus);
            })
            .First();
    }

    public static bool TrySelectPriorityTarget(
        IEnumerable<InvasionIntruderFacilityTargetSnapshot> reachableFacilities,
        InvasionIntruderTargetPreference preference,
        ISet<BuildingInstanceId> excludedFacilityIds,
        out InvasionIntruderFacilityTargetSnapshot target)
    {
        InvasionFacilityTargetSelectionSnapshot selection = new(
            reachableFacilities,
            preference,
            default,
            excludedFacilityIds);
        return InvasionFacilityDamageSelectionRules.TrySelectPriorityTarget(
            selection,
            out target);
    }

    public static bool IsAtOwner(
        Vector2Int intruderPosition,
        Vector2Int ownerPosition)
    {
        return intruderPosition == ownerPosition;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
