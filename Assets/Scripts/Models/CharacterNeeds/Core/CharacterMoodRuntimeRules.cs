using System.Collections.Generic;
using UnityEngine;

internal static class CharacterMoodRuntimeRules
{
    public static bool PruneExpired(
        List<CharacterMoodMemory> interactionFactors,
        float now)
    {
        return interactionFactors != null
            && interactionFactors.RemoveAll(
                item => item == null || item.IsExpired(now)) > 0;
    }

    public static CharacterMoodSnapshot BuildSnapshot(
        IReadOnlyDictionary<CharacterCondition, float> stats,
        IReadOnlyList<CharacterMoodMemory> interactionFactors,
        float baseMood,
        float now,
        ICharacterNeedDefinitionQuery needCatalog)
    {
        List<CharacterMoodFactorSnapshot> factors =
            CharacterMoodRules.BuildNeedFactors(stats, needCatalog);
        if (interactionFactors != null)
        {
            for (int index = 0; index < interactionFactors.Count; index++)
            {
                CharacterMoodMemory factor = interactionFactors[index];
                if (factor != null && !factor.IsExpired(now))
                {
                    factors.Add(factor.CreateSnapshot(now));
                }
            }
        }

        float mood = Mathf.Clamp(
            baseMood + CalculateFactorTotal(factors),
            0f,
            100f);
        return new CharacterMoodSnapshot(mood, baseMood, factors);
    }

    public static float CalculateValue(
        IReadOnlyDictionary<CharacterCondition, float> stats,
        IReadOnlyList<CharacterMoodMemory> interactionFactors,
        float baseMood,
        float now,
        ICharacterNeedDefinitionQuery needCatalog)
    {
        float total = CharacterMoodRules.CalculateNeedFactorTotal(stats, needCatalog);
        if (interactionFactors != null)
        {
            for (int index = 0; index < interactionFactors.Count; index++)
            {
                CharacterMoodMemory factor = interactionFactors[index];
                if (factor != null && !factor.IsExpired(now))
                {
                    total += factor.TotalValue;
                }
            }
        }

        return Mathf.Clamp(baseMood + total, 0f, 100f);
    }

    public static float CalculateFactorTotal(
        IReadOnlyList<CharacterMoodFactorSnapshot> factors)
    {
        float total = 0f;
        if (factors == null)
        {
            return total;
        }

        for (int index = 0; index < factors.Count; index++)
        {
            if (factors[index] != null)
            {
                total += factors[index].Value;
            }
        }

        return total;
    }
}
