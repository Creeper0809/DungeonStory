using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Flags]
public enum CharacterNeedTag
{
    None = 0,
    Survival = 1 << 0,
    Leisure = 1 << 1,
    DirectorRoutine = 1 << 2,
    MoodInteraction = 1 << 3
}

public sealed class CharacterNeedMoodProfile
{
    public CharacterNeedMoodProfile(
        float criticalMaximum,
        string criticalLabel,
        float criticalMood,
        float lowMaximum,
        string lowLabel,
        float lowMood,
        float highMinimum,
        string highLabel,
        float highMood)
    {
        CriticalMaximum = criticalMaximum;
        CriticalLabel = criticalLabel ?? string.Empty;
        CriticalMood = criticalMood;
        LowMaximum = lowMaximum;
        LowLabel = lowLabel ?? string.Empty;
        LowMood = lowMood;
        HighMinimum = highMinimum;
        HighLabel = highLabel ?? string.Empty;
        HighMood = highMood;
    }

    public float CriticalMaximum { get; }
    public string CriticalLabel { get; }
    public float CriticalMood { get; }
    public float LowMaximum { get; }
    public string LowLabel { get; }
    public float LowMood { get; }
    public float HighMinimum { get; }
    public string HighLabel { get; }
    public float HighMood { get; }

    public bool TryEvaluate(float value, out string label, out float mood)
    {
        if (value <= CriticalMaximum)
        {
            label = CriticalLabel;
            mood = CriticalMood;
            return true;
        }

        if (value <= LowMaximum)
        {
            label = LowLabel;
            mood = LowMood;
            return true;
        }

        if (value >= HighMinimum)
        {
            label = HighLabel;
            mood = HighMood;
            return true;
        }

        label = string.Empty;
        mood = 0f;
        return false;
    }
}

public sealed class CharacterNeedDefinition
{
    public CharacterNeedDefinition(
        string id,
        CharacterCondition condition,
        string displayName,
        int sortOrder,
        float defaultValue,
        float workerInitialValue,
        FacilityRole relatedFacilityRole,
        CharacterNeedTag tags,
        float survivalWeight,
        CharacterNeedMoodProfile moodProfile)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Character need id is required.", nameof(id));
        }

        Id = id.Trim();
        Condition = condition;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        SortOrder = sortOrder;
        DefaultValue = Mathf.Clamp(defaultValue, 0f, 100f);
        WorkerInitialValue = Mathf.Clamp(workerInitialValue, 0f, 100f);
        RelatedFacilityRole = relatedFacilityRole;
        Tags = tags;
        SurvivalWeight = Mathf.Max(0f, survivalWeight);
        MoodProfile = moodProfile;
    }

    public string Id { get; }
    public CharacterCondition Condition { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }
    public float DefaultValue { get; }
    public float WorkerInitialValue { get; }
    public FacilityRole RelatedFacilityRole { get; }
    public CharacterNeedTag Tags { get; }
    public float SurvivalWeight { get; }
    public CharacterNeedMoodProfile MoodProfile { get; }

    public bool HasTag(CharacterNeedTag tag)
    {
        return (Tags & tag) != 0;
    }

    public float GetUrgency(CharacterActor actor)
    {
        CharacterStats stats = actor != null ? actor.Stats : null;
        if (stats == null
            || stats.Stats == null
            || !stats.Stats.TryGetValue(Condition, out float value))
        {
            return 0.5f;
        }

        return Mathf.Clamp01(1f - value / 100f);
    }

    public bool TryCreateMoodFactor(
        IReadOnlyDictionary<CharacterCondition, float> stats,
        out CharacterMoodFactorSnapshot factor)
    {
        float value = stats != null && stats.TryGetValue(Condition, out float current)
            ? Mathf.Clamp(current, 0f, 100f)
            : DefaultValue;
        if (MoodProfile == null
            || !MoodProfile.TryEvaluate(value, out string label, out float mood))
        {
            factor = null;
            return false;
        }

        factor = new CharacterMoodFactorSnapshot(
            Id,
            label,
            mood,
            CharacterMoodFactorKind.Need,
            0f);
        return true;
    }
}

public static class CharacterNeedCatalog
{
    private static readonly Dictionary<string, CharacterNeedDefinition> ById =
        new Dictionary<string, CharacterNeedDefinition>(StringComparer.Ordinal);
    private static readonly Dictionary<CharacterCondition, CharacterNeedDefinition> ByCondition =
        new Dictionary<CharacterCondition, CharacterNeedDefinition>();
    private static bool initialized;

    public static IReadOnlyList<CharacterNeedDefinition> All
    {
        get
        {
            EnsureInitialized();
            return ById.Values
                .OrderBy((definition) => definition.SortOrder)
                .ThenBy((definition) => definition.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static void Register(CharacterNeedDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        EnsureInitialized();
        if (ById.TryGetValue(definition.Id, out CharacterNeedDefinition existingById)
            && existingById.Condition != definition.Condition)
        {
            throw new InvalidOperationException(
                $"Character need id '{definition.Id}' is already assigned to {existingById.Condition}.");
        }

        if (ByCondition.TryGetValue(definition.Condition, out CharacterNeedDefinition existingByCondition)
            && !string.Equals(existingByCondition.Id, definition.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Character condition '{definition.Condition}' is already assigned to '{existingByCondition.Id}'.");
        }

        ById[definition.Id] = definition;
        ByCondition[definition.Condition] = definition;
    }

    public static bool TryGet(CharacterCondition condition, out CharacterNeedDefinition definition)
    {
        EnsureInitialized();
        return ByCondition.TryGetValue(condition, out definition);
    }

    public static bool TryGet(string id, out CharacterNeedDefinition definition)
    {
        EnsureInitialized();
        return ById.TryGetValue(id?.Trim() ?? string.Empty, out definition);
    }

    public static CharacterNeedDefinition GetRequired(CharacterCondition condition)
    {
        if (TryGet(condition, out CharacterNeedDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            $"No character need definition is registered for '{condition}' ({(int)condition}).");
    }

    public static float GetUrgency(CharacterActor actor, CharacterCondition condition)
    {
        return TryGet(condition, out CharacterNeedDefinition definition)
            ? definition.GetUrgency(actor)
            : 0.5f;
    }

    public static float GetStrongestUrgency(
        CharacterActor actor,
        CharacterNeedTag requiredTag,
        bool applySurvivalWeight = true)
    {
        return All
            .Where((definition) => definition.HasTag(requiredTag))
            .Select((definition) => definition.GetUrgency(actor)
                * (applySurvivalWeight ? definition.SurvivalWeight : 1f))
            .DefaultIfEmpty(0f)
            .Max();
    }

    public static bool TryGetStrongestUrgency(
        CharacterActor actor,
        CharacterNeedTag requiredTag,
        out CharacterNeedDefinition strongest,
        out float urgency,
        bool applySurvivalWeight = true)
    {
        strongest = null;
        urgency = 0f;
        foreach (CharacterNeedDefinition definition in All)
        {
            if (!definition.HasTag(requiredTag))
            {
                continue;
            }

            float candidate = definition.GetUrgency(actor)
                * (applySurvivalWeight ? definition.SurvivalWeight : 1f);
            if (strongest != null && candidate <= urgency)
            {
                continue;
            }

            strongest = definition;
            urgency = candidate;
        }

        urgency = Mathf.Clamp01(urgency);
        return strongest != null;
    }

    public static float GetWeightedUrgency(CharacterActor actor, CharacterCondition condition)
    {
        if (!TryGet(condition, out CharacterNeedDefinition definition))
        {
            return 0f;
        }

        return Mathf.Clamp01(definition.GetUrgency(actor) * definition.SurvivalWeight);
    }

    public static void ResetToBuiltIns()
    {
        ById.Clear();
        ByCondition.Clear();
        initialized = true;
        RegisterBuiltIns();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ById.Clear();
        ByCondition.Clear();
        initialized = false;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        RegisterBuiltIns();
    }

    private static void RegisterBuiltIns()
    {
        RegisterBuiltIn(
            "need:hunger", CharacterCondition.HUNGER, "포만감", 10, 100f, 80f,
            FacilityRole.Meal,
            CharacterNeedTag.Survival | CharacterNeedTag.DirectorRoutine | CharacterNeedTag.MoodInteraction,
            1f,
            new CharacterNeedMoodProfile(15f, "굶주림", -18f, 35f, "허기짐", -9f, 85f, "배가 든든함", 4f));
        RegisterBuiltIn(
            "need:thirst", CharacterCondition.THIRST, "갈증", 15, 100f, 85f,
            FacilityRole.None,
            CharacterNeedTag.Survival | CharacterNeedTag.DirectorRoutine | CharacterNeedTag.MoodInteraction,
            1.15f,
            new CharacterNeedMoodProfile(15f, "심한 갈증", -18f, 35f, "목이 마름", -9f, 85f, "갈증이 없음", 3f));
        RegisterBuiltIn(
            "need:sleep", CharacterCondition.SLEEP, "휴식", 20, 100f, 85f,
            FacilityRole.Rest,
            CharacterNeedTag.Survival | CharacterNeedTag.DirectorRoutine | CharacterNeedTag.MoodInteraction,
            1f,
            new CharacterNeedMoodProfile(15f, "완전히 지침", -15f, 35f, "피곤함", -8f, 85f, "푹 쉼", 4f));
        RegisterBuiltIn(
            "need:fun", CharacterCondition.FUN, "재미", 30, 100f, 70f,
            FacilityRole.Purchase,
            CharacterNeedTag.Leisure | CharacterNeedTag.DirectorRoutine | CharacterNeedTag.MoodInteraction,
            0f,
            new CharacterNeedMoodProfile(15f, "몹시 지루함", -12f, 35f, "재미 부족", -6f, 85f, "즐거움 충족", 4f));
        RegisterBuiltIn(
            "need:excretion", CharacterCondition.EXCRETION, "배변", 40, 100f, 85f,
            FacilityRole.Toilet,
            CharacterNeedTag.Survival | CharacterNeedTag.DirectorRoutine | CharacterNeedTag.MoodInteraction,
            1f,
            new CharacterNeedMoodProfile(15f, "용변이 매우 급함", -12f, 35f, "화장실이 필요함", -6f, 85f, "개운함", 2f));
        RegisterBuiltIn(
            "need:hygiene", CharacterCondition.HYGIENE, "위생", 50, 100f, 80f,
            FacilityRole.Hygiene,
            CharacterNeedTag.Survival | CharacterNeedTag.DirectorRoutine | CharacterNeedTag.MoodInteraction,
            0.7f,
            new CharacterNeedMoodProfile(15f, "매우 지저분함", -10f, 35f, "씻고 싶음", -5f, 85f, "깨끗함", 3f));
    }

    private static void RegisterBuiltIn(
        string id,
        CharacterCondition condition,
        string displayName,
        int sortOrder,
        float defaultValue,
        float workerInitialValue,
        FacilityRole relatedFacilityRole,
        CharacterNeedTag tags,
        float survivalWeight,
        CharacterNeedMoodProfile moodProfile)
    {
        CharacterNeedDefinition definition = new CharacterNeedDefinition(
            id,
            condition,
            displayName,
            sortOrder,
            defaultValue,
            workerInitialValue,
            relatedFacilityRole,
            tags,
            survivalWeight,
            moodProfile);
        ById.Add(definition.Id, definition);
        ByCondition.Add(definition.Condition, definition);
    }
}
