using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CharacterProficiencySubgrade
{
    Fourth = 4,
    Third = 3,
    Second = 2,
    First = 1
}

public readonly struct CharacterProficiencyBandSnapshot
{
    public CharacterProficiencyBandSnapshot(
        CharacterProficiencyRank rank,
        CharacterProficiencySubgrade subgrade,
        long minimumMilliExperience,
        long nextMilliExperience)
    {
        Rank = rank;
        Subgrade = subgrade;
        MinimumMilliExperience = minimumMilliExperience;
        NextMilliExperience = nextMilliExperience;
    }

    public CharacterProficiencyRank Rank { get; }
    public CharacterProficiencySubgrade Subgrade { get; }
    public long MinimumMilliExperience { get; }
    public long NextMilliExperience { get; }
}

public enum CharacterStartingAgeBand
{
    YoungAdult = 0,
    EstablishedAdult = 1,
    VeteranAdult = 2,
    Elder = 3
}

[Serializable]
public sealed class CharacterStartingProficiencyBonus
{
    public string proficiencyId = string.Empty;
    [Min(0)] public int experience;
}

[Serializable]
public sealed class CharacterStartingProfileState
{
    public bool prepared;
    public string originId = string.Empty;
    public string originDisplayName = string.Empty;
    public string historyId = string.Empty;
    public string historyDisplayName = string.Empty;
    public string primaryProficiencyId = string.Empty;
    public string secondaryProficiencyId = string.Empty;
    public CharacterStartingAgeBand ageBand;
    public double biologicalAgeYears;
    public int proficiencyCap;
    public List<string> initialAgeConditionIds = new();

    public void EnsureCollections() =>
        initialAgeConditionIds ??= new List<string>();

    public CharacterStartingProfileState Clone()
    {
        EnsureCollections();
        return new CharacterStartingProfileState
        {
            prepared = prepared,
            originId = originId ?? string.Empty,
            originDisplayName = originDisplayName ?? string.Empty,
            historyId = historyId ?? string.Empty,
            historyDisplayName = historyDisplayName ?? string.Empty,
            primaryProficiencyId = primaryProficiencyId ?? string.Empty,
            secondaryProficiencyId = secondaryProficiencyId ?? string.Empty,
            ageBand = ageBand,
            biologicalAgeYears = biologicalAgeYears,
            proficiencyCap = proficiencyCap,
            initialAgeConditionIds = initialAgeConditionIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
    }
}

public readonly struct CharacterStartingProfileRoll
{
    public CharacterStartingProfileRoll(
        CharacterStartingProfileState profile,
        IReadOnlyList<CharacterStartingProficiencyExperience> proficiencies)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Proficiencies = proficiencies
            ?? throw new ArgumentNullException(nameof(proficiencies));
    }

    public CharacterStartingProfileState Profile { get; }
    public IReadOnlyList<CharacterStartingProficiencyExperience> Proficiencies { get; }
}

public readonly struct CharacterStartingLifeHistory
{
    public CharacterStartingLifeHistory(
        int adultAgeYears,
        int elderAgeYears,
        float untreatedExpectedLifeYears,
        bool construct)
    {
        AdultAgeYears = adultAgeYears;
        ElderAgeYears = elderAgeYears;
        UntreatedExpectedLifeYears = untreatedExpectedLifeYears;
        Construct = construct;
    }

    public int AdultAgeYears { get; }
    public int ElderAgeYears { get; }
    public float UntreatedExpectedLifeYears { get; }
    public bool Construct { get; }
}

public readonly struct CharacterStartingAgeCondition
{
    public CharacterStartingAgeCondition(string conditionId, bool construct)
    {
        ConditionId = conditionId?.Trim() ?? string.Empty;
        Construct = construct;
    }

    public string ConditionId { get; }
    public bool Construct { get; }
}

public static class CharacterStartingProfileRules
{
    public const int YoungAdultCap = 99;
    public const int EstablishedAdultCap = 174;
    public const int VeteranAdultCap = 249;
    public const int ElderCap = 399;

    public static CharacterStartingProfileRoll Create(
        int seed,
        CharacterStartingLifeHistory lifeHistory,
        CharacterStartingOriginSO origin,
        CharacterStartingHistorySO history,
        IReadOnlyList<CharacterStartingAgeCondition> ageConditions)
    {
        if (lifeHistory.ElderAgeYears <= lifeHistory.AdultAgeYears)
            throw new InvalidOperationException("A valid starting life history is required.");
        if (origin == null || origin.ValidateDefinition().Count > 0)
            throw new InvalidOperationException("A valid starting origin is required.");
        if (history == null || history.ValidateDefinition().Count > 0)
            throw new InvalidOperationException("A valid starting history is required.");

        CharacterStartingAgeBand band = ResolveAgeBand(Unit(seed, "age-band"));
        double ageYears = ResolveAgeYears(
            lifeHistory,
            band,
            Unit(seed, "age-within"));
        int cap = ResolveAgeCap(band);
        double adultSpan = Math.Max(
            1d,
            lifeHistory.ElderAgeYears - lifeHistory.AdultAgeYears);
        double career = Math.Clamp(
            (ageYears - lifeHistory.AdultAgeYears) / adultSpan,
            0d,
            1.25d);

        Dictionary<string, int> bonuses = BuiltInCharacterProficiencyIds.All
            .ToDictionary(value => value.Value, _ => 0, StringComparer.Ordinal);
        AddBonuses(bonuses, origin.proficiencyBonuses);
        AddBonuses(bonuses, history.proficiencyBonuses);
        bonuses[history.primaryProficiencyId] += history.primaryBaseExperience
            + (int)Math.Round(120d * career, MidpointRounding.AwayFromZero);
        bonuses[history.secondaryProficiencyId] += history.secondaryBaseExperience
            + (int)Math.Round(60d * career, MidpointRounding.AwayFromZero);

        List<CharacterStartingProficiencyExperience> proficiencies = new();
        for (int index = 0; index < BuiltInCharacterProficiencyIds.All.Count; index++)
        {
            CharacterProficiencyId id = BuiltInCharacterProficiencyIds.All[index];
            int baseExperience = CharacterStartingProficiencyRules.MinimumStartingExperience
                + (int)(StableHash($"{seed}:base:{id.Value}:{index}") % 31u);
            proficiencies.Add(new CharacterStartingProficiencyExperience
            {
                proficiencyId = id.Value,
                experience = Math.Min(cap, baseExperience + bonuses[id.Value]),
                learningMultiplier = CharacterProficiencySpecializationRules
                    .Resolve(
                        history.primaryProficiencyId,
                        history.secondaryProficiencyId,
                        id)
            });
        }

        CharacterStartingProfileState profile = new()
        {
            prepared = true,
            originId = origin.originId.Trim(),
            originDisplayName = origin.displayName.Trim(),
            historyId = history.historyId.Trim(),
            historyDisplayName = history.displayName.Trim(),
            primaryProficiencyId = history.primaryProficiencyId.Trim(),
            secondaryProficiencyId = history.secondaryProficiencyId.Trim(),
            ageBand = band,
            biologicalAgeYears = Math.Round(ageYears, 3, MidpointRounding.AwayFromZero),
            proficiencyCap = cap,
            initialAgeConditionIds = RollInitialAgeConditions(
                seed,
                lifeHistory,
                ageYears,
                ageConditions)
        };
        CharacterStartingProficiencyRules.Validate(proficiencies);
        return new CharacterStartingProfileRoll(profile, proficiencies);
    }

    public static CharacterStartingAgeBand ResolveAgeBand(double selector)
    {
        double value = Math.Clamp(selector, 0d, 0.999999999d);
        if (value < 0.40d) return CharacterStartingAgeBand.YoungAdult;
        if (value < 0.75d) return CharacterStartingAgeBand.EstablishedAdult;
        if (value < 0.95d) return CharacterStartingAgeBand.VeteranAdult;
        return CharacterStartingAgeBand.Elder;
    }

    public static int ResolveAgeCap(CharacterStartingAgeBand band) => band switch
    {
        CharacterStartingAgeBand.YoungAdult => YoungAdultCap,
        CharacterStartingAgeBand.EstablishedAdult => EstablishedAdultCap,
        CharacterStartingAgeBand.VeteranAdult => VeteranAdultCap,
        CharacterStartingAgeBand.Elder => ElderCap,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, null)
    };

    private static double ResolveAgeYears(
        CharacterStartingLifeHistory lifeHistory,
        CharacterStartingAgeBand band,
        double within)
    {
        double adult = lifeHistory.AdultAgeYears;
        double elder = lifeHistory.ElderAgeYears;
        double span = Math.Max(0d, elder - adult);
        return band switch
        {
            CharacterStartingAgeBand.YoungAdult => adult + span * 0.25d * within,
            CharacterStartingAgeBand.EstablishedAdult =>
                adult + span * (0.25d + 0.35d * within),
            CharacterStartingAgeBand.VeteranAdult =>
                adult + span * (0.60d + 0.40d * within),
            CharacterStartingAgeBand.Elder =>
                elder + Math.Min(10d, Math.Max(1d,
                    lifeHistory.UntreatedExpectedLifeYears - elder)) * within,
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, null)
        };
    }

    private static List<string> RollInitialAgeConditions(
        int seed,
        CharacterStartingLifeHistory lifeHistory,
        double ageYears,
        IReadOnlyList<CharacterStartingAgeCondition> authored)
    {
        if (ageYears < lifeHistory.ElderAgeYears)
            return new List<string>();

        CharacterStartingAgeCondition[] eligible = (authored
                ?? Array.Empty<CharacterStartingAgeCondition>())
            .Where(value => value.ConditionId.Length > 0
                && value.Construct == lifeHistory.Construct)
            .OrderBy(value => value.ConditionId, StringComparer.Ordinal)
            .ToArray();
        List<string> selected = new();
        if (eligible.Length == 0)
            return selected;

        double elderSpan = Math.Min(
            10d,
            Math.Max(
                1d,
                lifeHistory.UntreatedExpectedLifeYears
                - lifeHistory.ElderAgeYears));
        double elderProgress = Math.Clamp(
            (ageYears - lifeHistory.ElderAgeYears) / elderSpan,
            0d,
            1d);
        double anyChance = 0.65d + 0.15d * elderProgress;
        if (Unit(seed, "age-condition:any") >= anyChance)
            return selected;

        int targetCount = 1;
        double secondChance = 0.40d + 0.20d * elderProgress;
        if (targetCount < eligible.Length
            && Unit(seed, "age-condition:second") < secondChance)
        {
            targetCount++;
        }
        double thirdChance = 0.10d + 0.15d * elderProgress;
        if (targetCount == 2 && targetCount < eligible.Length
            && Unit(seed, "age-condition:third") < thirdChance)
        {
            targetCount++;
        }

        for (int slot = 0; slot < targetCount; slot++)
        {
            CharacterStartingAgeCondition[] remaining = eligible
                .Where(value => !selected.Contains(value.ConditionId))
                .ToArray();
            if (remaining.Length == 0) break;
            int index = Math.Min(
                remaining.Length - 1,
                (int)Math.Floor(Unit(seed, $"age-condition-pick:{slot}")
                    * remaining.Length));
            selected.Add(remaining[index].ConditionId);
        }
        return selected;
    }

    private static void AddBonuses(
        IDictionary<string, int> destination,
        IEnumerable<CharacterStartingProficiencyBonus> source)
    {
        foreach (CharacterStartingProficiencyBonus bonus in source
                     ?? Array.Empty<CharacterStartingProficiencyBonus>())
        {
            if (bonus != null && destination.ContainsKey(bonus.proficiencyId))
                destination[bonus.proficiencyId] += Math.Max(0, bonus.experience);
        }
    }

    private static double Unit(int seed, string key) =>
        StableHash($"{seed}:{key}") / ((double)uint.MaxValue + 1d);

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
