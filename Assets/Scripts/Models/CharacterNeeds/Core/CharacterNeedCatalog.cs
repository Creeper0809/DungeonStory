using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[Flags]
public enum CharacterNeedTag
{
    None = 0,
    Survival = 1 << 0,
    Leisure = 1 << 1,
    DirectorRoutine = 1 << 2,
    MoodInteraction = 1 << 3
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterNeedMoodProfile
{
    public CharacterNeedMoodProfile(
        CharacterNeedMoodBand critical,
        CharacterNeedMoodBand low,
        CharacterNeedMoodBand high)
    {
        CriticalMaximum = critical.Threshold;
        CriticalLabel = critical.Label;
        CriticalMood = critical.Mood;
        LowMaximum = low.Threshold;
        LowLabel = low.Label;
        LowMood = low.Mood;
        HighMinimum = high.Threshold;
        HighLabel = high.Label;
        HighMood = high.Mood;
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

public readonly struct CharacterNeedMoodBand
{
    public CharacterNeedMoodBand(float threshold, string label, float mood)
    {
        Threshold = threshold;
        Label = label ?? string.Empty;
        Mood = mood;
    }

    public float Threshold { get; }
    public string Label { get; }
    public float Mood { get; }
}

public readonly struct CharacterNeedIdentity
{
    public CharacterNeedIdentity(
        string id,
        CharacterCondition condition,
        string displayName,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Character need id is required.", nameof(id));
        }

        Id = id.Trim();
        Condition = condition;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? Id
            : displayName.Trim();
        SortOrder = sortOrder;
    }

    public string Id { get; }
    public CharacterCondition Condition { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }
}

public readonly struct CharacterNeedDefaults
{
    public CharacterNeedDefaults(float defaultValue, float workerInitialValue)
    {
        DefaultValue = Mathf.Clamp(defaultValue, 0f, 100f);
        WorkerInitialValue = Mathf.Clamp(workerInitialValue, 0f, 100f);
    }

    public float DefaultValue { get; }
    public float WorkerInitialValue { get; }
}

public readonly struct CharacterNeedBehavior
{
    public CharacterNeedBehavior(
        FacilityRole relatedFacilityRole,
        CharacterNeedTag tags,
        float survivalWeight)
    {
        RelatedFacilityRole = relatedFacilityRole;
        Tags = tags;
        SurvivalWeight = Mathf.Max(0f, survivalWeight);
    }

    public FacilityRole RelatedFacilityRole { get; }
    public CharacterNeedTag Tags { get; }
    public float SurvivalWeight { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterNeedDefinition
{
    public CharacterNeedDefinition(
        CharacterNeedIdentity identity,
        CharacterNeedDefaults defaults,
        CharacterNeedBehavior behavior,
        CharacterNeedMoodProfile moodProfile)
    {
        Id = identity.Id;
        Condition = identity.Condition;
        DisplayName = identity.DisplayName;
        SortOrder = identity.SortOrder;
        DefaultValue = defaults.DefaultValue;
        WorkerInitialValue = defaults.WorkerInitialValue;
        RelatedFacilityRole = behavior.RelatedFacilityRole;
        Tags = behavior.Tags;
        SurvivalWeight = behavior.SurvivalWeight;
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

    public bool HasTag(CharacterNeedTag tag) => (Tags & tag) != 0;
}

public interface ICharacterNeedDefinitionQuery
{
    IReadOnlyList<CharacterNeedDefinition> All { get; }
    bool TryGet(CharacterCondition condition, out CharacterNeedDefinition definition);
    bool TryGet(string id, out CharacterNeedDefinition definition);
    CharacterNeedDefinition Require(CharacterCondition condition);
}
