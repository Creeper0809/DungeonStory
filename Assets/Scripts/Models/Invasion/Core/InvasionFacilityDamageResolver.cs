using System;
using System.Collections.Generic;
using System.Linq;

public sealed class InvasionFacilityTargetSelectionSnapshot
{
    public InvasionFacilityTargetSelectionSnapshot(
        IEnumerable<InvasionIntruderFacilityTargetSnapshot> candidates,
        InvasionIntruderTargetPreference preference,
        BuildingInstanceId preferredTargetId = default,
        IEnumerable<BuildingInstanceId> excludedTargetIds = null)
    {
        Candidates = Array.AsReadOnly(
            (candidates
                ?? Array.Empty<InvasionIntruderFacilityTargetSnapshot>())
            .ToArray());
        Preference = preference;
        PreferredTargetId = preferredTargetId;
        ExcludedTargetIds = Array.AsReadOnly(
            (excludedTargetIds ?? Array.Empty<BuildingInstanceId>())
            .Distinct()
            .ToArray());
    }

    public IReadOnlyList<InvasionIntruderFacilityTargetSnapshot> Candidates { get; }
    public InvasionIntruderTargetPreference Preference { get; }
    public BuildingInstanceId PreferredTargetId { get; }
    public IReadOnlyList<BuildingInstanceId> ExcludedTargetIds { get; }
}

public static class InvasionFacilityDamageSelectionRules
{
    public static bool TrySelectDamageTarget(
        InvasionFacilityTargetSelectionSnapshot selection,
        out InvasionIntruderFacilityTargetSnapshot target)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        InvasionIntruderFacilityTargetSnapshot[] candidates =
            GetEligibleCandidates(selection).ToArray();
        if (selection.PreferredTargetId.IsValid)
        {
            foreach (InvasionIntruderFacilityTargetSnapshot candidate in candidates)
            {
                if (candidate.TargetId.Equals(selection.PreferredTargetId))
                {
                    target = candidate;
                    return true;
                }
            }
        }

        foreach (InvasionIntruderFacilityTargetSnapshot candidate in candidates)
        {
            if (MatchesPreference(candidate, selection.Preference))
            {
                target = candidate;
                return true;
            }
        }

        if (selection.Preference == InvasionIntruderTargetPreference.Owner
            && candidates.Length > 0)
        {
            target = candidates[0];
            return true;
        }

        target = default;
        return false;
    }

    public static bool TrySelectPriorityTarget(
        InvasionFacilityTargetSelectionSnapshot selection,
        out InvasionIntruderFacilityTargetSnapshot target)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }
        if (selection.Preference == InvasionIntruderTargetPreference.Owner)
        {
            target = default;
            return false;
        }

        IEnumerable<InvasionIntruderFacilityTargetSnapshot> candidates =
            GetEligibleCandidates(selection)
            .Where(candidate => MatchesPreference(
                candidate,
                selection.Preference));
        InvasionIntruderFacilityTargetSnapshot[] ordered = selection.Preference
            == InvasionIntruderTargetPreference.ValuableFacility
            ? candidates
                .OrderByDescending(candidate => candidate.ConstructionValue)
                .ThenBy(candidate => candidate.MoveCost)
                .ToArray()
            : candidates
                .OrderBy(candidate => candidate.MoveCost)
                .ThenByDescending(candidate => candidate.ConstructionValue)
                .ToArray();
        if (ordered.Length == 0)
        {
            target = default;
            return false;
        }

        target = ordered[0];
        return true;
    }

    public static bool MatchesPreference(
        InvasionIntruderFacilityTargetSnapshot target,
        InvasionIntruderTargetPreference preference)
    {
        return preference switch
        {
            InvasionIntruderTargetPreference.DefenseFacility =>
                target.DefenseFacility,
            InvasionIntruderTargetPreference.ValuableFacility =>
                !target.DefenseFacility,
            _ => true
        };
    }

    private static IEnumerable<InvasionIntruderFacilityTargetSnapshot>
        GetEligibleCandidates(InvasionFacilityTargetSelectionSnapshot selection)
    {
        HashSet<BuildingInstanceId> excluded = new(selection.ExcludedTargetIds);
        return selection.Candidates
            .Where(candidate => candidate.Damageable)
            .Where(candidate => !excluded.Contains(candidate.TargetId))
            .GroupBy(candidate => candidate.TargetId)
            .Select(group => group.First());
    }
}
