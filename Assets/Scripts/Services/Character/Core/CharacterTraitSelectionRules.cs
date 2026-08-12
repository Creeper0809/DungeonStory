using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public static class CharacterTraitSelectionRules
{
    public static IReadOnlyList<int> Select(
        IEnumerable<CharacterTraitSO> candidates,
        IEnumerable<CharacterTraitConflictRule> conflictRules,
        IRandomStream random,
        string speciesTag = null,
        int maximumCount = 4)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        if (maximumCount <= 0) return Array.Empty<int>();

        CharacterTraitConflictRule[] conflicts = (conflictRules
                ?? Array.Empty<CharacterTraitConflictRule>())
            .Where(value => value != null)
            .ToArray();
        CharacterTraitSO[] orderedCandidates = (candidates
                ?? Array.Empty<CharacterTraitSO>())
            .Where(value => value != null)
            .GroupBy(value => value.id)
            .Select(group => group.First())
            .Where(value => value.SelectionWeight > 0
                && value.IsEligibleForSpecies(speciesTag))
            .OrderBy(value => value.id)
            .ToArray();

        int targetCount = Math.Min(maximumCount, RollTraitCount(random));
        List<CharacterTraitSO> available = orderedCandidates.ToList();
        List<CharacterTraitSO> selectedTraits = new(targetCount);
        while (selectedTraits.Count < targetCount)
        {
            CharacterTraitSO[] eligible = available
                .Where(candidate => !ConflictsWithSelection(
                    candidate,
                    selectedTraits,
                    conflicts))
                .ToArray();
            if (eligible.Length == 0) break;

            bool alreadyHasExtreme = selectedTraits.Any(value => value.IsExtreme);
            CharacterTraitPolarity[] eligiblePolarities = eligible
                .Select(value => value.polarity)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            int polarityTotalWeight = eligiblePolarities.Sum(value =>
                PolaritySelectionWeight(value, alreadyHasExtreme));
            int polarityRoll = random.NextInt(0, polarityTotalWeight);
            CharacterTraitPolarity selectedPolarity = eligiblePolarities[^1];
            foreach (CharacterTraitPolarity polarity in eligiblePolarities)
            {
                polarityRoll -= PolaritySelectionWeight(polarity, alreadyHasExtreme);
                if (polarityRoll < 0)
                {
                    selectedPolarity = polarity;
                    break;
                }
            }

            CharacterTraitSO[] slotCandidates = eligible
                .Where(value => value.polarity == selectedPolarity)
                .ToArray();
            int totalWeight = slotCandidates.Sum(value => value.SelectionWeight);
            int roll = random.NextInt(0, totalWeight);
            CharacterTraitSO selected = slotCandidates[slotCandidates.Length - 1];
            foreach (CharacterTraitSO candidate in slotCandidates)
            {
                roll -= candidate.SelectionWeight;
                if (roll < 0)
                {
                    selected = candidate;
                    break;
                }
            }

            selectedTraits.Add(selected);
            available.Remove(selected);
        }

        if (selectedTraits.Count != targetCount)
        {
            throw new InvalidOperationException(
                $"Trait pool could provide only {selectedTraits.Count} of {targetCount} "
                + $"requested traits for species '{speciesTag ?? string.Empty}'.");
        }
        return selectedTraits.Select(value => value.id).ToArray();
    }

    private static int PolaritySelectionWeight(
        CharacterTraitPolarity polarity,
        bool alreadyHasExtreme) => polarity switch
        {
            CharacterTraitPolarity.Advantage => 3200,
            CharacterTraitPolarity.Tradeoff => 3300,
            CharacterTraitPolarity.Negative => 2800,
            CharacterTraitPolarity.Quirk => 600,
            CharacterTraitPolarity.Extreme => alreadyHasExtreme ? 5 : 100,
            _ => 0
        };

    public static int RollTraitCount(IRandomStream random)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        int roll = random.NextInt(0, 100);
        if (roll < 15) return 1;
        if (roll < 55) return 2;
        if (roll < 90) return 3;
        return 4;
    }

    private static bool ConflictsWithSelection(
        CharacterTraitSO candidate,
        IReadOnlyCollection<CharacterTraitSO> selected,
        IReadOnlyCollection<CharacterTraitConflictRule> conflicts)
    {
        string family = candidate.selectionFamilyId?.Trim() ?? string.Empty;
        if (family.Length > 0 && selected.Any(value => string.Equals(
                value.selectionFamilyId?.Trim(),
                family,
                StringComparison.Ordinal)))
        {
            return true;
        }

        HashSet<string> candidateGroups = new(
            (candidate.incompatibilityGroups ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()),
            StringComparer.Ordinal);
        if (candidateGroups.Count > 0 && selected.Any(value =>
                (value.incompatibilityGroups ?? new List<string>())
                .Where(group => !string.IsNullOrWhiteSpace(group))
                .Select(group => group.Trim())
                .Any(candidateGroups.Contains)))
        {
            return true;
        }

        return conflicts.Any(rule => selected.Any(value =>
            (rule.firstTraitId == candidate.id
                && value.id == rule.secondTraitId)
            || (rule.secondTraitId == candidate.id
                && value.id == rule.firstTraitId)));
    }
}
