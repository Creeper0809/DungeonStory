using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static class CharacterGrowthRules
{
    public static CharacterPotentialGrade RollPotential(
        CharacterSkillSystemSettingsSO settings,
        System.Random random)
    {
        float[] weights = settings?.potentialPopulationWeights;
        if (weights == null || weights.Length != 5)
        {
            weights = new[] { 45f, 30f, 15f, 8f, 2f };
        }

        int index = RollWeighted(weights.Select(value => Mathf.Max(0f, value)).ToArray(), random);
        return (CharacterPotentialGrade)Mathf.Clamp(index, 0, 4);
    }

    public static CharacterPotentialGrade RollPotential(
        CharacterSkillSystemSettingsSO settings,
        IRandomStream random)
    {
        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        float[] weights = settings?.potentialPopulationWeights;
        if (weights == null || weights.Length != 5)
        {
            weights = new[] { 45f, 30f, 15f, 8f, 2f };
        }

        int index = RollWeighted(
            weights.Select(value => Mathf.Max(0f, value)).ToArray(),
            random);
        return (CharacterPotentialGrade)Mathf.Clamp(index, 0, 4);
    }

    public static CharacterSkillRarity RollRarity(
        CharacterSkillSystemSettingsSO settings,
        CharacterPotentialGrade potential,
        bool applyPity,
        System.Random random)
    {
        IReadOnlyList<CharacterWeightedRarity> entries = settings.GetRarityWeights(potential);
        float[] weights = Enum.GetValues(typeof(CharacterSkillRarity))
            .Cast<CharacterSkillRarity>()
            .Select(rarity =>
            {
                float weight = entries.FirstOrDefault(item => item != null && item.rarity == rarity)?.weight ?? 1f;
                return applyPity && rarity >= CharacterSkillRarity.Rare
                    ? weight * settings.missedUpperRarityMultiplier
                    : weight;
            })
            .ToArray();
        return (CharacterSkillRarity)RollWeighted(weights, random);
    }

    public static CharacterSkillRarity RollRarity(
        CharacterSkillSystemSettingsSO settings,
        CharacterPotentialGrade potential,
        bool applyPity,
        IRandomStream random)
    {
        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        IReadOnlyList<CharacterWeightedRarity> entries = settings.GetRarityWeights(potential);
        float[] weights = Enum.GetValues(typeof(CharacterSkillRarity))
            .Cast<CharacterSkillRarity>()
            .Select(rarity =>
            {
                float weight = entries.FirstOrDefault(item => item != null && item.rarity == rarity)?.weight ?? 1f;
                return applyPity && rarity >= CharacterSkillRarity.Rare
                    ? weight * settings.missedUpperRarityMultiplier
                    : weight;
            })
            .ToArray();
        return (CharacterSkillRarity)RollWeighted(weights, random);
    }

    public static CharacterSkillTrigger ChoosePassiveTrigger(
        CharacterNarrativeLedger ledger,
        System.Random random)
    {
        CharacterNarrativeDomain? strongest = ledger?.Facts
            .Where(item => item != null)
            .GroupBy(item => item.domain)
            .OrderByDescending(group => group.Sum(item => item.milestoneCount))
            .ThenBy(group => group.Key)
            .Select(group => (CharacterNarrativeDomain?)group.Key)
            .FirstOrDefault();
        if (!strongest.HasValue)
        {
            CharacterSkillTrigger[] identityTriggers =
            {
                CharacterSkillTrigger.WorkStarted,
                CharacterSkillTrigger.WorkCompleted,
                CharacterSkillTrigger.DamageTaken,
                CharacterSkillTrigger.MoodChanged,
                CharacterSkillTrigger.NeedChanged
            };
            return identityTriggers[random.Next(identityTriggers.Length)];
        }

        return strongest.Value switch
        {
            CharacterNarrativeDomain.Work or CharacterNarrativeDomain.FacilityUse =>
                random.Next(2) == 0 ? CharacterSkillTrigger.WorkStarted : CharacterSkillTrigger.WorkCompleted,
            CharacterNarrativeDomain.Need => CharacterSkillTrigger.NeedChanged,
            CharacterNarrativeDomain.Mood => CharacterSkillTrigger.MoodChanged,
            CharacterNarrativeDomain.Relationship => CharacterSkillTrigger.RelationshipChanged,
            CharacterNarrativeDomain.Invasion => CharacterSkillTrigger.InvasionStarted,
            CharacterNarrativeDomain.Injury => CharacterSkillTrigger.DamageTaken,
            _ => CharacterSkillTrigger.BattleCompleted
        };
    }

    public static CharacterSkillTrigger ChoosePassiveTrigger(
        CharacterNarrativeLedger ledger,
        IRandomStream random)
    {
        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        CharacterNarrativeDomain? strongest = ledger?.Facts
            .Where(item => item != null)
            .GroupBy(item => item.domain)
            .OrderByDescending(group => group.Sum(item => item.milestoneCount))
            .ThenBy(group => group.Key)
            .Select(group => (CharacterNarrativeDomain?)group.Key)
            .FirstOrDefault();
        if (!strongest.HasValue)
        {
            CharacterSkillTrigger[] identityTriggers =
            {
                CharacterSkillTrigger.WorkStarted,
                CharacterSkillTrigger.WorkCompleted,
                CharacterSkillTrigger.DamageTaken,
                CharacterSkillTrigger.MoodChanged,
                CharacterSkillTrigger.NeedChanged
            };
            return identityTriggers[random.NextInt(0, identityTriggers.Length)];
        }

        return strongest.Value switch
        {
            CharacterNarrativeDomain.Work or CharacterNarrativeDomain.FacilityUse =>
                random.NextInt(0, 2) == 0
                    ? CharacterSkillTrigger.WorkStarted
                    : CharacterSkillTrigger.WorkCompleted,
            CharacterNarrativeDomain.Need => CharacterSkillTrigger.NeedChanged,
            CharacterNarrativeDomain.Mood => CharacterSkillTrigger.MoodChanged,
            CharacterNarrativeDomain.Relationship => CharacterSkillTrigger.RelationshipChanged,
            CharacterNarrativeDomain.Invasion => CharacterSkillTrigger.InvasionStarted,
            CharacterNarrativeDomain.Injury => CharacterSkillTrigger.DamageTaken,
            _ => CharacterSkillTrigger.BattleCompleted
        };
    }

    public static int StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return (int)hash;
        }
    }

    private static int RollWeighted(float[] weights, System.Random random)
    {
        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        float total = weights?.Sum(value => Mathf.Max(0f, value)) ?? 0f;
        if (total <= 0f)
        {
            return 0;
        }

        double roll = random.NextDouble() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= Mathf.Max(0f, weights[i]);
            if (roll <= 0d)
            {
                return i;
            }
        }

        return weights.Length - 1;
    }

    private static int RollWeighted(float[] weights, IRandomStream random)
    {
        float total = weights?.Sum(value => Mathf.Max(0f, value)) ?? 0f;
        if (total <= 0f)
        {
            return 0;
        }

        float roll = random.NextFloat() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= Mathf.Max(0f, weights[i]);
            if (roll <= 0f)
            {
                return i;
            }
        }

        return weights.Length - 1;
    }

}
