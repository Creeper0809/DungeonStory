using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CharacterGameplayEffectProjector
{
    public static GameplayEffectProjectionResult Resolve(
        string targetId,
        float baseValue,
        IEnumerable<IGameplayEffectSource> sources,
        GameplayEffectContext context = null)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("Gameplay effect target id is required.", nameof(targetId));
        if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
            throw new ArgumentOutOfRangeException(nameof(baseValue));

        GameplayEffectContext requiredContext = context ?? new GameplayEffectContext();
        List<GameplayEffectContribution> contributions = new();
        HashSet<string> seenBindings = new(StringComparer.Ordinal);
        foreach (IGameplayEffectSource source in sources ?? Array.Empty<IGameplayEffectSource>())
        {
            if (source == null) continue;
            GameplayEffectSourceRef sourceRef = source.SourceRef;
            foreach (GameplayEffectBinding binding in source.Effects
                         ?? Array.Empty<GameplayEffectBinding>())
            {
                if (binding?.definition == null
                    || !string.Equals(
                        binding.definition.TargetId,
                        targetId.Trim(),
                        StringComparison.Ordinal))
                    continue;

                GameplayEffectContribution contribution = new()
                {
                    EffectId = binding.definition.EffectId,
                    Source = sourceRef,
                    BindingId = binding.bindingId?.Trim() ?? string.Empty,
                    AuthoredValue = binding.value,
                    Definition = binding.definition
                };
                if (!binding.IsValidFor(sourceRef, out string reason))
                {
                    contribution.Suppressed = true;
                    contribution.SuppressionReason = reason;
                }
                else if (!requiredContext.IsActive(binding.condition))
                {
                    contribution.Suppressed = true;
                    contribution.SuppressionReason = "condition is inactive";
                }
                else if (!seenBindings.Add(
                             $"{sourceRef.Kind}:{sourceRef.SourceId}:{contribution.BindingId}"))
                {
                    contribution.Suppressed = true;
                    contribution.SuppressionReason = "duplicate source binding";
                }
                contributions.Add(contribution);
            }
        }

        ApplyStackingPolicies(contributions);
        float value = ApplyOrdered(baseValue, contributions);
        return new GameplayEffectProjectionResult(value, contributions);
    }

    public static int ResolveStartingProficiencyDelta(
        string proficiencyId,
        IEnumerable<CharacterTraitSO> traits)
    {
        float value = Resolve(
            GameplayEffectTargetIds.StartingProficiencyExperience(proficiencyId),
            0f,
            (traits ?? Array.Empty<CharacterTraitSO>()).Cast<IGameplayEffectSource>()).Value;
        return Mathf.RoundToInt(value);
    }

    private static void ApplyStackingPolicies(
        List<GameplayEffectContribution> contributions)
    {
        foreach (IGrouping<string, GameplayEffectContribution> group in contributions
                     .Where(value => !value.Suppressed)
                     .GroupBy(value => value.EffectId, StringComparer.Ordinal))
        {
            GameplayEffectContribution[] values = group
                .OrderBy(value => value.Source.Kind)
                .ThenBy(value => value.Source.SourceId, StringComparer.Ordinal)
                .ThenBy(value => value.BindingId, StringComparer.Ordinal)
                .ToArray();
            GameplayEffectStackingPolicy policy = values[0].Definition.StackingPolicy;
            if (policy == GameplayEffectStackingPolicy.StackAll) continue;

            HashSet<GameplayEffectContribution> retained = policy switch
            {
                GameplayEffectStackingPolicy.HighestMagnitude =>
                    new HashSet<GameplayEffectContribution>
                    {
                        values.OrderByDescending(ResolveMagnitude)
                            .ThenBy(value => value.Source.SourceId, StringComparer.Ordinal)
                            .First()
                    },
                GameplayEffectStackingPolicy.LowestMagnitude =>
                    new HashSet<GameplayEffectContribution>
                    {
                        values.OrderBy(ResolveMagnitude)
                            .ThenBy(value => value.Source.SourceId, StringComparer.Ordinal)
                            .First()
                    },
                GameplayEffectStackingPolicy.UniquePerSource =>
                    values.GroupBy(value => value.Source)
                        .Select(value => value.First())
                        .ToHashSet(),
                _ => new HashSet<GameplayEffectContribution> { values[0] }
            };
            foreach (GameplayEffectContribution value in values.Where(value => !retained.Contains(value)))
            {
                value.Suppressed = true;
                value.SuppressionReason = $"suppressed by {policy}";
            }
        }
    }

    private static float ResolveMagnitude(GameplayEffectContribution value) =>
        value.Definition.Operation == GameplayEffectOperation.Multiply
            ? Math.Abs(value.AuthoredValue - 1f)
            : Math.Abs(value.AuthoredValue);

    private static float ApplyOrdered(
        float baseValue,
        List<GameplayEffectContribution> contributions)
    {
        GameplayEffectContribution[] active = contributions
            .Where(value => !value.Suppressed)
            .OrderBy(value => value.Definition.ProjectionPhase)
            .ThenBy(value => value.EffectId, StringComparer.Ordinal)
            .ThenBy(value => value.Source.Kind)
            .ThenBy(value => value.Source.SourceId, StringComparer.Ordinal)
            .ThenBy(value => value.BindingId, StringComparer.Ordinal)
            .ToArray();

        float value = baseValue;
        foreach (GameplayEffectContribution contribution in active
                     .Where(item => item.Definition.Operation == GameplayEffectOperation.AddFlat))
        {
            value += contribution.AuthoredValue;
            contribution.AppliedValue = contribution.AuthoredValue;
        }

        float additivePercent = 0f;
        foreach (GameplayEffectContribution contribution in active
                     .Where(item => item.Definition.Operation == GameplayEffectOperation.AddPercent))
        {
            additivePercent += contribution.AuthoredValue;
            contribution.AppliedValue = contribution.AuthoredValue;
        }
        value *= 1f + additivePercent;

        foreach (GameplayEffectContribution contribution in active
                     .Where(item => item.Definition.Operation == GameplayEffectOperation.Multiply))
        {
            value *= contribution.AuthoredValue;
            contribution.AppliedValue = contribution.AuthoredValue;
        }

        foreach (GameplayEffectContribution contribution in active
                     .Where(item => item.Definition.Operation == GameplayEffectOperation.Override))
        {
            value = contribution.AuthoredValue;
            contribution.AppliedValue = contribution.AuthoredValue;
        }

        foreach (GameplayEffectContribution contribution in active
                     .Where(item => item.Definition.Operation == GameplayEffectOperation.ClampMinimum))
        {
            value = Mathf.Max(value, contribution.AuthoredValue);
            contribution.AppliedValue = contribution.AuthoredValue;
        }
        foreach (GameplayEffectContribution contribution in active
                     .Where(item => item.Definition.Operation == GameplayEffectOperation.ClampMaximum))
        {
            value = Mathf.Min(value, contribution.AuthoredValue);
            contribution.AppliedValue = contribution.AuthoredValue;
        }

        float authoredMinimum = active.Length == 0
            ? float.MinValue
            : active.Max(item => item.Definition.MinimumResult);
        float authoredMaximum = active.Length == 0
            ? float.MaxValue
            : active.Min(item => item.Definition.MaximumResult);
        return Mathf.Clamp(value, authoredMinimum, authoredMaximum);
    }
}

public static class CharacterTraitStartingProficiencyRules
{
    public static void Apply(
        IList<CharacterStartingProficiencyExperience> proficiencies,
        IEnumerable<CharacterTraitSO> selectedTraits,
        int ageCap)
    {
        if (proficiencies == null)
            throw new ArgumentNullException(nameof(proficiencies));
        if (ageCap < 0)
            throw new ArgumentOutOfRangeException(nameof(ageCap));

        CharacterTraitSO[] traits = (selectedTraits ?? Array.Empty<CharacterTraitSO>())
            .Where(value => value != null)
            .OrderBy(value => value.id)
            .ToArray();
        foreach (CharacterStartingProficiencyExperience proficiency in proficiencies)
        {
            if (proficiency == null || string.IsNullOrWhiteSpace(proficiency.proficiencyId))
                throw new InvalidOperationException("Starting proficiency entry is invalid.");
            int delta = CharacterGameplayEffectProjector.ResolveStartingProficiencyDelta(
                proficiency.proficiencyId,
                traits);
            proficiency.experience = Math.Clamp(
                proficiency.experience + delta,
                0,
                ageCap);
        }
        CharacterStartingProficiencyRules.Validate(proficiencies.ToArray());
    }
}
