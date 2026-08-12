using System;
using System.Collections.Generic;
using System.Linq;

public enum ProficiencyWorkOutcome
{
    Success,
    PartialSuccess,
    SafeFailure,
    AccidentOrForcedStop,
    NoApprovedWork
}

public readonly struct CharacterProficiencySnapshot
{
    public CharacterProficiencySnapshot(
        CharacterProficiencyId proficiencyId,
        long currentMilliExperience,
        long lifetimeMilliExperience,
        long lastPracticeAbsoluteHour,
        long lastDecaySettlementAbsoluteHour,
        long practiceMilliExperienceToday = 0L,
        float learningMultiplier = 1f)
    {
        ProficiencyId = proficiencyId;
        CurrentMilliExperience = Math.Max(0L, currentMilliExperience);
        LifetimeMilliExperience = Math.Max(
            CurrentMilliExperience,
            lifetimeMilliExperience);
        LastPracticeAbsoluteHour = Math.Max(0L, lastPracticeAbsoluteHour);
        LastDecaySettlementAbsoluteHour = Math.Max(
            LastPracticeAbsoluteHour,
            lastDecaySettlementAbsoluteHour);
        PracticeMilliExperienceToday = Math.Max(
            0L,
            practiceMilliExperienceToday);
        LearningMultiplier = CharacterProficiencySpecializationRules
            .NormalizeSerializedMultiplier(learningMultiplier);
    }

    public CharacterProficiencyId ProficiencyId { get; }
    public long CurrentMilliExperience { get; }
    public long LifetimeMilliExperience { get; }
    public long LastPracticeAbsoluteHour { get; }
    public long LastDecaySettlementAbsoluteHour { get; }
    public long PracticeMilliExperienceToday { get; }
    public float LearningMultiplier { get; }
    public float PracticeExperienceToday =>
        PracticeMilliExperienceToday / (float)ProficiencyProgressionRules.MilliPerExperience;
    public int CurrentExperience => checked((int)Math.Min(
        int.MaxValue,
        CurrentMilliExperience / ProficiencyProgressionRules.MilliPerExperience));
    public CharacterProficiencyRank Rank =>
        ProficiencyProgressionRules.ResolveRank(CurrentMilliExperience);
    public CharacterProficiencyBandSnapshot Band =>
        ProficiencyProgressionRules.ResolveBand(CurrentMilliExperience);
}

public readonly struct CharacterProficiencyEffectSnapshot
{
    public CharacterProficiencyEffectSnapshot(
        CharacterProficiencyRank rank,
        float workSpeedMultiplier,
        float qualityScore,
        float accidentMultiplier)
    {
        Rank = rank;
        WorkSpeedMultiplier = workSpeedMultiplier;
        QualityScore = qualityScore;
        AccidentMultiplier = accidentMultiplier;
    }

    public CharacterProficiencyRank Rank { get; }
    public float WorkSpeedMultiplier { get; }
    public float QualityScore { get; }
    public float AccidentMultiplier { get; }
}

public static class ProficiencyProgressionRules
{
    public const long MilliPerExperience = 1000L;
    public const long SkilledThreshold = 100L * MilliPerExperience;
    public const long TechnicianThreshold = 400L * MilliPerExperience;
    public const long ExpertThreshold = 1200L * MilliPerExperience;
    public const long MasterThreshold = 3000L * MilliPerExperience;
    public const long MasterCurrentCap = 3060L * MilliPerExperience;
    public const float ExperiencePerApprovedWork = 0.08f;

    public static CharacterProficiencyRank ResolveRank(long currentMilliExperience)
    {
        long value = Math.Max(0L, currentMilliExperience);
        if (value >= MasterThreshold) return CharacterProficiencyRank.Master;
        if (value >= ExpertThreshold) return CharacterProficiencyRank.Expert;
        if (value >= TechnicianThreshold) return CharacterProficiencyRank.Technician;
        if (value >= SkilledThreshold) return CharacterProficiencyRank.Skilled;
        return CharacterProficiencyRank.Apprentice;
    }

    public static CharacterProficiencyBandSnapshot ResolveBand(
        long currentMilliExperience)
    {
        long value = Math.Clamp(currentMilliExperience, 0L, MasterCurrentCap);
        CharacterProficiencyRank rank = ResolveRank(value);
        long minimum;
        long nextRank;
        switch (rank)
        {
            case CharacterProficiencyRank.Apprentice:
                minimum = 0L;
                nextRank = SkilledThreshold;
                break;
            case CharacterProficiencyRank.Skilled:
                minimum = SkilledThreshold;
                nextRank = TechnicianThreshold;
                break;
            case CharacterProficiencyRank.Technician:
                minimum = TechnicianThreshold;
                nextRank = ExpertThreshold;
                break;
            case CharacterProficiencyRank.Expert:
                minimum = ExpertThreshold;
                nextRank = MasterThreshold;
                break;
            case CharacterProficiencyRank.Master:
                minimum = MasterThreshold;
                nextRank = MasterCurrentCap;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        long span = Math.Max(1L, nextRank - minimum);
        int index = Math.Clamp(
            (int)(((value - minimum) * 4L) / span),
            0,
            3);
        long bandMinimum = minimum + (span * index + 3L) / 4L;
        long bandNext = index == 3
            ? nextRank
            : minimum + (span * (index + 1) + 3L) / 4L;
        return new CharacterProficiencyBandSnapshot(
            rank,
            (CharacterProficiencySubgrade)(4 - index),
            bandMinimum,
            Math.Min(MasterCurrentCap, bandNext));
    }

    public static float ResolveSpeedMultiplier(CharacterProficiencyRank rank) =>
        rank switch
        {
            CharacterProficiencyRank.Apprentice => 0.85f,
            CharacterProficiencyRank.Skilled => 0.95f,
            CharacterProficiencyRank.Technician => 1.05f,
            CharacterProficiencyRank.Expert => 1.15f,
            CharacterProficiencyRank.Master => 1.25f,
            _ => 1f
        };

    public static int ResolveQualityScore(CharacterProficiencyRank rank) =>
        rank switch
        {
            CharacterProficiencyRank.Apprentice => 25,
            CharacterProficiencyRank.Skilled => 40,
            CharacterProficiencyRank.Technician => 58,
            CharacterProficiencyRank.Expert => 78,
            CharacterProficiencyRank.Master => 95,
            _ => 25
        };

    public static float ResolveContinuousPerformanceScore(
        long currentMilliExperience)
    {
        long experience = Math.Clamp(
            currentMilliExperience,
            0L,
            MasterCurrentCap);
        if (experience <= SkilledThreshold)
        {
            return Lerp(25f, 40f, experience, 0L, SkilledThreshold);
        }
        if (experience <= TechnicianThreshold)
        {
            return Lerp(
                40f,
                58f,
                experience,
                SkilledThreshold,
                TechnicianThreshold);
        }
        if (experience <= ExpertThreshold)
        {
            return Lerp(
                58f,
                78f,
                experience,
                TechnicianThreshold,
                ExpertThreshold);
        }
        if (experience <= MasterThreshold)
        {
            return Lerp(
                78f,
                95f,
                experience,
                ExpertThreshold,
                MasterThreshold);
        }
        return Lerp(
            95f,
            100f,
            experience,
            MasterThreshold,
            MasterCurrentCap);
    }

    public static CharacterProficiencyEffectSnapshot ResolveEffects(
        long currentMilliExperience)
    {
        CharacterProficiencyRank rank = ResolveRank(currentMilliExperience);
        return new CharacterProficiencyEffectSnapshot(
            rank,
            ResolveContinuousSpeedMultiplier(currentMilliExperience),
            ResolveContinuousPerformanceScore(currentMilliExperience),
            ResolveContinuousAccidentMultiplier(currentMilliExperience));
    }

    public static float ResolveContinuousSpeedMultiplier(
        long currentMilliExperience) => ResolveContinuousMetric(
        currentMilliExperience,
        0.85f,
        0.95f,
        1.05f,
        1.15f,
        1.25f,
        1.30f);

    public static float ResolveContinuousAccidentMultiplier(
        long currentMilliExperience) => ResolveContinuousMetric(
        currentMilliExperience,
        1.25f,
        1.10f,
        1.00f,
        0.80f,
        0.65f,
        0.60f);

    public static float ResolveAccidentMultiplier(CharacterProficiencyRank rank) =>
        rank switch
        {
            CharacterProficiencyRank.Apprentice => 1.25f,
            CharacterProficiencyRank.Skilled => 1.10f,
            CharacterProficiencyRank.Technician => 1f,
            CharacterProficiencyRank.Expert => 0.80f,
            CharacterProficiencyRank.Master => 0.65f,
            _ => 1f
        };

    public static long CalculateWorkAwardMilli(
        float approvedWork,
        float difficultyMultiplier,
        ProficiencyWorkOutcome outcome,
        float learningMultiplier,
        float repetitionMultiplier)
    {
        if (approvedWork <= 0f || outcome == ProficiencyWorkOutcome.NoApprovedWork)
        {
            return 0L;
        }

        double resultMultiplier = outcome switch
        {
            ProficiencyWorkOutcome.Success => 1d,
            ProficiencyWorkOutcome.PartialSuccess => 0.6d,
            ProficiencyWorkOutcome.SafeFailure => 0.3d,
            ProficiencyWorkOutcome.AccidentOrForcedStop => 0.1d,
            _ => 0d
        };
        double award = approvedWork
            * ExperiencePerApprovedWork
            * Math.Clamp(difficultyMultiplier, 0.20f, 1.25f)
            * resultMultiplier
            * Math.Clamp(
                learningMultiplier,
                0.70f,
                CharacterProficiencySpecializationRules
                    .MaximumCombinedLearningMultiplier)
            * Math.Clamp(repetitionMultiplier, 0.15f, 1f)
            * MilliPerExperience;
        return Math.Max(0L, checked((long)Math.Round(
            award,
            MidpointRounding.AwayFromZero)));
    }

    public static long SettleDecay(
        long currentMilliExperience,
        long lastPracticeAbsoluteHour,
        long lastSettlementAbsoluteHour,
        long absoluteHour)
    {
        long current = Math.Clamp(
            currentMilliExperience,
            0L,
            MasterCurrentCap);
        long settlement = Math.Max(lastSettlementAbsoluteHour, 0L);
        long now = Math.Max(settlement, absoluteHour);
        while (settlement < now && current >= ExpertThreshold)
        {
            CharacterProficiencyRank rank = ResolveRank(current);
            long graceHours = rank == CharacterProficiencyRank.Master
                ? 5L * GameCalendarRules.HoursPerDay
                : 15L * GameCalendarRules.HoursPerDay;
            long decayStart = Math.Max(
                settlement,
                Math.Max(0L, lastPracticeAbsoluteHour) + graceHours);
            if (decayStart >= now)
            {
                break;
            }

            long ratePerHour = rank == CharacterProficiencyRank.Master
                ? 100L
                : 250L;
            long rankFloor = rank == CharacterProficiencyRank.Master
                ? MasterThreshold - 1L
                : ExpertThreshold - 1L;
            long hoursUntilDemotion = Math.Max(
                1L,
                (current - rankFloor + ratePerHour - 1L) / ratePerHour);
            long elapsed = Math.Min(now - decayStart, hoursUntilDemotion);
            current = Math.Max(rankFloor, current - elapsed * ratePerHour);
            settlement = decayStart + elapsed;
            if (current == rankFloor)
            {
                lastPracticeAbsoluteHour = settlement;
            }
        }

        return current;
    }

    private static float Lerp(
        float from,
        float to,
        long value,
        long minimum,
        long maximum)
    {
        if (maximum <= minimum) return to;
        double t = Math.Clamp(
            (value - minimum) / (double)(maximum - minimum),
            0d,
            1d);
        return (float)(from + (to - from) * t);
    }

    private static float ResolveContinuousMetric(
        long currentMilliExperience,
        float apprentice,
        float skilled,
        float technician,
        float expert,
        float master,
        float cap)
    {
        long experience = Math.Clamp(
            currentMilliExperience,
            0L,
            MasterCurrentCap);
        if (experience <= SkilledThreshold)
            return Lerp(apprentice, skilled, experience, 0L, SkilledThreshold);
        if (experience <= TechnicianThreshold)
            return Lerp(skilled, technician, experience, SkilledThreshold, TechnicianThreshold);
        if (experience <= ExpertThreshold)
            return Lerp(technician, expert, experience, TechnicianThreshold, ExpertThreshold);
        if (experience <= MasterThreshold)
            return Lerp(expert, master, experience, ExpertThreshold, MasterThreshold);
        return Lerp(master, cap, experience, MasterThreshold, MasterCurrentCap);
    }
}

[Serializable]
public sealed class CharacterStartingProficiencyExperience
{
    public string proficiencyId = string.Empty;
    public int experience;
    public float learningMultiplier =
        CharacterProficiencySpecializationRules.NeutralLearningMultiplier;

    public CharacterStartingProficiencyExperience Clone() => new()
    {
        proficiencyId = proficiencyId ?? string.Empty,
        experience = Math.Max(0, experience),
        learningMultiplier = CharacterProficiencySpecializationRules
            .NormalizeSerializedMultiplier(learningMultiplier)
    };
}

public static class CharacterProficiencySpecializationRules
{
    public const float PrimaryLearningMultiplier = 1.50f;
    public const float SecondaryLearningMultiplier = 1.20f;
    public const float NeutralLearningMultiplier = 1.00f;
    public const float MaximumCombinedLearningMultiplier = 2.10f;

    public static float Resolve(
        CharacterStartingProfileState profile,
        CharacterProficiencyId proficiencyId) => profile?.prepared == true
        ? Resolve(
            profile.primaryProficiencyId,
            profile.secondaryProficiencyId,
            proficiencyId)
        : NeutralLearningMultiplier;

    public static float Resolve(
        string primaryProficiencyId,
        string secondaryProficiencyId,
        CharacterProficiencyId proficiencyId)
    {
        if (!proficiencyId.IsValid)
            return NeutralLearningMultiplier;
        if (string.Equals(
                primaryProficiencyId,
                proficiencyId.Value,
                StringComparison.Ordinal))
        {
            return PrimaryLearningMultiplier;
        }
        if (string.Equals(
                secondaryProficiencyId,
                proficiencyId.Value,
                StringComparison.Ordinal))
        {
            return SecondaryLearningMultiplier;
        }
        return NeutralLearningMultiplier;
    }

    public static float NormalizeSerializedMultiplier(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            return NeutralLearningMultiplier;
        return value;
    }

    public static bool IsCanonical(float value) =>
        Approximately(value, NeutralLearningMultiplier)
        || Approximately(value, SecondaryLearningMultiplier)
        || Approximately(value, PrimaryLearningMultiplier);

    private static bool Approximately(float left, float right) =>
        Math.Abs(left - right) <= 0.0001f;
}

/// <summary>
/// Creates the immutable starting experience packet used before a character is
/// published into the narrative aggregate. Current proficiency after
/// publication is owned only by CharacterNarrativeRuntime.
/// </summary>
public static class CharacterStartingProficiencyRules
{
    public const int MinimumStartingExperience = 15;
    public const int MaximumStartingExperience = 45;

    public static IReadOnlyList<CharacterStartingProficiencyExperience> Create(
        int seed)
    {
        List<CharacterStartingProficiencyExperience> values = new(
            BuiltInCharacterProficiencyIds.All.Count);
        for (int index = 0; index < BuiltInCharacterProficiencyIds.All.Count; index++)
        {
            CharacterProficiencyId id = BuiltInCharacterProficiencyIds.All[index];
            uint hash = StableHash($"{seed}:{id.Value}:{index}");
            int experience = MinimumStartingExperience
                + (int)(hash % (uint)(MaximumStartingExperience
                    - MinimumStartingExperience + 1));
            values.Add(new CharacterStartingProficiencyExperience
            {
                proficiencyId = id.Value,
                experience = experience,
                learningMultiplier = CharacterProficiencySpecializationRules
                    .NeutralLearningMultiplier
            });
        }
        return values;
    }

    public static void Validate(
        IReadOnlyList<CharacterStartingProficiencyExperience> values)
    {
        if (values == null || values.Count != BuiltInCharacterProficiencyIds.All.Count)
        {
            throw new InvalidOperationException(
                "A starting proficiency packet must contain exactly nine entries.");
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            CharacterStartingProficiencyExperience value = values[index]
                ?? throw new InvalidOperationException(
                    "A starting proficiency packet contains a null entry.");
            CharacterProficiencyId id = new(value.proficiencyId);
            value.learningMultiplier = CharacterProficiencySpecializationRules
                .NormalizeSerializedMultiplier(value.learningMultiplier);
            if (!id.IsValid
                || !BuiltInCharacterProficiencyIds.All.Contains(id)
                || !seen.Add(id.Value)
                || value.experience < 0
                || !CharacterProficiencySpecializationRules.IsCanonical(
                    value.learningMultiplier)
                || value.experience >= ProficiencyProgressionRules.TechnicianThreshold
                    / ProficiencyProgressionRules.MilliPerExperience)
            {
                throw new InvalidOperationException(
                    $"Invalid starting proficiency entry '{value.proficiencyId}'.");
            }
        }
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                hash ^= source[index];
                hash *= 16777619u;
            }
            return hash;
        }
    }
}

public readonly struct ProficiencyWorkProfile
{
    public ProficiencyWorkProfile(
        CharacterProficiencyId primary,
        CharacterProficiencyId secondary = default,
        float primaryWeight = 1f)
    {
        Primary = primary;
        Secondary = secondary;
        PrimaryWeight = Math.Clamp(primaryWeight, 0f, 1f);
    }

    public CharacterProficiencyId Primary { get; }
    public CharacterProficiencyId Secondary { get; }
    public float PrimaryWeight { get; }
    public float SecondaryWeight => Secondary.IsValid ? 1f - PrimaryWeight : 0f;
    public bool IsValid => Primary.IsValid;
}

public static class WorkTypeProficiencyRules
{
    public static bool TryResolve(
        WorkTypeId workTypeId,
        out ProficiencyWorkProfile profile)
    {
        profile = default;
        if (!workTypeId.IsValid) return false;

        if (workTypeId == BuiltInWorkTypeIds.Restock
            || workTypeId == BuiltInWorkTypeIds.Haul
            || workTypeId == BuiltInWorkTypeIds.DrawWater
            || workTypeId == BuiltInWorkTypeIds.Refuel
            || workTypeId == BuiltInWorkTypeIds.Gather
            || workTypeId == BuiltInWorkTypeIds.Logging
            || workTypeId == BuiltInWorkTypeIds.Clean)
            profile = new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.Fieldwork);
        else if (workTypeId == BuiltInWorkTypeIds.Construct)
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.ConstructionEngineering,
                BuiltInCharacterProficiencyIds.Fieldwork,
                .80f);
        else if (workTypeId == BuiltInWorkTypeIds.Repair
            || workTypeId == BuiltInWorkTypeIds.Dismantle)
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.ConstructionEngineering,
                BuiltInCharacterProficiencyIds.Crafting,
                .80f);
        else if (workTypeId == BuiltInWorkTypeIds.Plumbing
            || workTypeId == BuiltInWorkTypeIds.GrandProject)
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.ConstructionEngineering,
                BuiltInCharacterProficiencyIds.Scholarship,
                .80f);
        else if (workTypeId == BuiltInWorkTypeIds.Quarry)
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Fieldwork,
                BuiltInCharacterProficiencyIds.ConstructionEngineering,
                .80f);
        else if (workTypeId == BuiltInWorkTypeIds.Craft)
            profile = new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.Crafting);
        else if (workTypeId == BuiltInWorkTypeIds.Hunt
            || workTypeId == BuiltInWorkTypeIds.Cook)
            profile = new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.FoodProduction);
        else if (workTypeId == BuiltInWorkTypeIds.Butcher
            || workTypeId == BuiltInWorkTypeIds.AnimalCare)
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.FoodProduction,
                BuiltInCharacterProficiencyIds.Medicine,
                .80f);
        else if (workTypeId == BuiltInWorkTypeIds.Sow
            || workTypeId == BuiltInWorkTypeIds.Harvest)
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.FoodProduction,
                BuiltInCharacterProficiencyIds.Fieldwork,
                .80f);
        else if (workTypeId == BuiltInWorkTypeIds.Research)
            profile = new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.Scholarship);
        else if (workTypeId == BuiltInWorkTypeIds.Rescue)
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Medicine,
                BuiltInCharacterProficiencyIds.Fieldwork,
                .80f);
        else if (workTypeId == BuiltInWorkTypeIds.Treat)
            profile = new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.Medicine);
        else if (workTypeId == BuiltInWorkTypeIds.Surgery)
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Medicine,
                BuiltInCharacterProficiencyIds.Scholarship,
                .80f);
        else if (workTypeId == BuiltInWorkTypeIds.ThreatMitigation)
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Scholarship,
                BuiltInCharacterProficiencyIds.Fieldwork,
                .80f);
        else if (workTypeId == BuiltInWorkTypeIds.Reception
            || workTypeId == BuiltInWorkTypeIds.Warden
            || workTypeId == BuiltInWorkTypeIds.Perform)
            profile = new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.Social);

        // Operate is deliberately not guessed. Each facility execution role must
        // provide an authored primary proficiency before it can award experience.
        // Rest and Guard award no routine-work XP. Guard uses the active weapon
        // proficiency from the combat execution context.
        return profile.IsValid;
    }

    public static float ResolveDefenseExperience(
        int meleeExperience,
        int rangedExperience) => Math.Max(meleeExperience, rangedExperience);

    public static float ResolvePrisonerManagementExperience(
        int socialExperience,
        int meleeExperience,
        int rangedExperience) =>
        socialExperience * 0.80f
        + Math.Max(meleeExperience, rangedExperience) * 0.20f;

    public static float ResolveHuntingExperience(
        int foodExperience,
        int weaponExperience) =>
        foodExperience * 0.80f + weaponExperience * 0.20f;

    public static float ResolveRuneCraftExperience(
        int craftingExperience,
        int scholarshipExperience) =>
        craftingExperience * 0.80f + scholarshipExperience * 0.20f;
}

public interface ICharacterProficiencyQuery
{
    bool TryGetProficiency(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        long absoluteHour,
        out CharacterProficiencySnapshot snapshot);
    IReadOnlyList<CharacterProficiencySnapshot> GetAllProficiencies(
        CharacterId characterId,
        long absoluteHour);
}

public interface ICharacterProficiencyCommand
{
    long AddApprovedWork(
        CharacterId characterId,
        ProficiencyWorkProfile profile,
        float approvedWork,
        float difficultyMultiplier,
        ProficiencyWorkOutcome outcome,
        float learningMultiplier,
        float repetitionMultiplier,
        long absoluteHour);
    long AddDirectExperience(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        float experience,
        long absoluteHour,
        bool applyLearningMultiplier = true);
    long AddCombatExperience(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        float experience,
        bool training,
        string stableAwardKey,
        long absoluteHour);
    void RecordPractice(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        long absoluteHour);
}
