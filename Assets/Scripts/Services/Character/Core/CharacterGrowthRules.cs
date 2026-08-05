using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static class CharacterGrowthRules
{
    private static readonly CharacterStatType[] Stats =
        Enum.GetValues(typeof(CharacterStatType)).Cast<CharacterStatType>().ToArray();

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

    public static CharacterStatBlock RollInitialStats(
        CharacterSkillSystemSettingsSO settings,
        System.Random random)
    {
        int minimum = Mathf.Max(1, settings.initialStatMin);
        int maximum = Mathf.Max(minimum, settings.initialStatMax);
        int target = Mathf.Clamp(settings.initialStatTotal, minimum * Stats.Length, maximum * Stats.Length);
        int[] values = Enumerable.Repeat(minimum, Stats.Length).ToArray();
        int remaining = target - minimum * Stats.Length;
        while (remaining > 0)
        {
            int[] available = Enumerable.Range(0, values.Length)
                .Where(index => values[index] < maximum)
                .ToArray();
            if (available.Length == 0)
            {
                break;
            }

            values[available[random.Next(available.Length)]]++;
            remaining--;
        }

        CharacterStatBlock result = new CharacterStatBlock();
        for (int i = 0; i < Stats.Length; i++)
        {
            result.Set(Stats[i], values[i]);
        }

        return result;
    }

    public static CharacterStatBlock RollInitialStats(
        CharacterSkillSystemSettingsSO settings,
        IRandomStream random)
    {
        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        int minimum = Mathf.Max(1, settings.initialStatMin);
        int maximum = Mathf.Max(minimum, settings.initialStatMax);
        int target = Mathf.Clamp(settings.initialStatTotal, minimum * Stats.Length, maximum * Stats.Length);
        int[] values = Enumerable.Repeat(minimum, Stats.Length).ToArray();
        int remaining = target - minimum * Stats.Length;
        while (remaining > 0)
        {
            int[] available = Enumerable.Range(0, values.Length)
                .Where(index => values[index] < maximum)
                .ToArray();
            if (available.Length == 0)
            {
                break;
            }

            values[available[random.NextInt(0, available.Length)]]++;
            remaining--;
        }

        CharacterStatBlock result = new CharacterStatBlock();
        for (int i = 0; i < Stats.Length; i++)
        {
            result.Set(Stats[i], values[i]);
        }

        return result;
    }

    public static int GetGrowthPointsForLevel(int reachedLevel)
    {
        return reachedLevel <= 1 ? 0 : 1 + (reachedLevel % 5 == 0 ? 1 : 0);
    }

    public static void AllocateGrowthPoints(
        CharacterGrowthState growth,
        CharacterNarrativeLedger ledger,
        int reachedLevel,
        int pointCount,
        int cap,
        float identityWeight,
        System.Random random)
    {
        if (growth == null || pointCount <= 0)
        {
            return;
        }

        growth.EnsureCollections();
        for (int point = 0; point < pointCount; point++)
        {
            float[] weights = Stats.Select(stat =>
            {
                int baseValue = growth.initialBaseStats.Get(stat);
                int grown = growth.levelGrowthStats.Get(stat);
                if (grown >= cap)
                {
                    return 0f;
                }

                float identity = Mathf.Max(0.1f, baseValue);
                float activity = GetActivityWeight(stat, ledger);
                return identity * Mathf.Clamp01(identityWeight)
                    + activity * (1f - Mathf.Clamp01(identityWeight));
            }).ToArray();
            int selected = RollWeighted(weights, random);
            CharacterStatType selectedStat = Stats[selected];
            growth.levelGrowthStats.Add(CharacterStatCatalog.GetRequired(selectedStat).Id, 1);
            growth.allocatedGrowthPoints++;
            growth.allocationRecords.Add(new CharacterGrowthAllocationRecord
            {
                level = Mathf.Clamp(reachedLevel, 1, CharacterProgression.MaxLevel),
                statType = selectedStat,
                reason = ResolveGrowthReason(selectedStat, growth, ledger, identityWeight)
            });
        }
    }

    public static void AllocateGrowthPoints(
        CharacterGrowthState growth,
        CharacterNarrativeLedger ledger,
        int reachedLevel,
        int pointCount,
        int cap,
        float identityWeight,
        IRandomStream random)
    {
        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        if (growth == null || pointCount <= 0)
        {
            return;
        }

        growth.EnsureCollections();
        for (int point = 0; point < pointCount; point++)
        {
            float[] weights = Stats.Select(stat =>
            {
                int baseValue = growth.initialBaseStats.Get(stat);
                int grown = growth.levelGrowthStats.Get(stat);
                if (grown >= cap)
                {
                    return 0f;
                }

                float identity = Mathf.Max(0.1f, baseValue);
                float activity = GetActivityWeight(stat, ledger);
                return identity * Mathf.Clamp01(identityWeight)
                    + activity * (1f - Mathf.Clamp01(identityWeight));
            }).ToArray();
            int selected = RollWeighted(weights, random);
            CharacterStatType selectedStat = Stats[selected];
            growth.levelGrowthStats.Add(CharacterStatCatalog.GetRequired(selectedStat).Id, 1);
            growth.allocatedGrowthPoints++;
            growth.allocationRecords.Add(new CharacterGrowthAllocationRecord
            {
                level = Mathf.Clamp(reachedLevel, 1, CharacterProgression.MaxLevel),
                statType = selectedStat,
                reason = ResolveGrowthReason(selectedStat, growth, ledger, identityWeight)
            });
        }
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

    private static float GetActivityWeight(CharacterStatType stat, CharacterNarrativeLedger ledger)
    {
        if (ledger == null)
        {
            return 1f;
        }

        CharacterNarrativeDomain[] domains = stat switch
        {
            CharacterStatType.Attack or CharacterStatType.Strength or CharacterStatType.Toughness =>
                new[] { CharacterNarrativeDomain.Combat, CharacterNarrativeDomain.Invasion, CharacterNarrativeDomain.Survival },
            CharacterStatType.Dexterity =>
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Combat },
            CharacterStatType.Research =>
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse },
            CharacterStatType.Sales =>
                new[] { CharacterNarrativeDomain.Relationship, CharacterNarrativeDomain.Work },
            CharacterStatType.Cleaning =>
                new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Need },
            CharacterStatType.Endurance =>
                new[] { CharacterNarrativeDomain.Expedition, CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Injury },
            _ => new[] { CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Expedition }
        };
        return 1f + ledger.Facts
            .Where(item => item != null && domains.Contains(item.domain))
            .Sum(item => item.milestoneCount + Mathf.Abs(item.totalValue) * 0.02f);
    }

    private static string ResolveGrowthReason(
        CharacterStatType stat,
        CharacterGrowthState growth,
        CharacterNarrativeLedger ledger,
        float identityWeight)
    {
        float clampedIdentityWeight = Mathf.Clamp01(identityWeight);
        float identityScore = Mathf.Max(0.1f, growth?.initialBaseStats?.Get(stat) ?? 1) * clampedIdentityWeight;
        float activityScore = GetActivityWeight(stat, ledger) * (1f - clampedIdentityWeight);
        return activityScore > identityScore
            ? "실제 활동 기록"
            : "정체성 성향";
    }
}
