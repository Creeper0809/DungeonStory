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
        int maximumCount = 3)
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
            .OrderBy(value => value.id)
            .ToArray();

        // Roll only after canonical ID ordering. Catalog enumeration and asset
        // import order can therefore never change a run's selected traits.
        CharacterTraitSO[] ranked = orderedCandidates
            .Select(value => new
            {
                Trait = value,
                Rank = random.NextInt(0, int.MaxValue)
            })
            .OrderBy(value => value.Rank)
            .ThenBy(value => value.Trait.id)
            .Select(value => value.Trait)
            .ToArray();

        List<int> selected = new(maximumCount);
        foreach (CharacterTraitSO candidate in ranked)
        {
            bool conflictsWithSelection = conflicts.Any(rule =>
                (rule.firstTraitId == candidate.id
                    && selected.Contains(rule.secondTraitId))
                || (rule.secondTraitId == candidate.id
                    && selected.Contains(rule.firstTraitId)));
            if (conflictsWithSelection) continue;

            selected.Add(candidate.id);
            if (selected.Count >= maximumCount) break;
        }
        return selected;
    }
}
