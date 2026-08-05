using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public interface IRoomEnvironmentAuthoredContentPort
{
    RoomEnvironmentSettingsSO RoomEnvironmentSettings { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class FacilityRoleColorOverride
{
    public string roleId;
    public Color color = Color.white;
}

[CreateAssetMenu(
    fileName = "RoomEnvironmentSettings",
    menuName = "DungeonStory/Rooms/Environment Settings",
    order = 0)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RoomEnvironmentSettingsSO : ScriptableObject
{
    [Header("Environment Formula")]
    [SerializeField, Min(1f)] private float spaciousAreaMinimum = 2f;
    [SerializeField, Min(2f)] private float spaciousAreaMaximum = 16f;
    [SerializeField, Range(0f, 1f)] private float spaciousAreaWeight = 0.45f;
    [SerializeField, Range(0f, 1f)] private float spaciousFreeCellWeight = 0.55f;
    [SerializeField] private float beautyBaseline = 50f;
    [SerializeField] private float luxuryMultiplier = 1.25f;
    [SerializeField] private float beautyDamagePenalty = 30f;
    [SerializeField, Range(0f, 1f)] private float beautyCrowdingThreshold = 0.6f;
    [SerializeField] private float beautyCrowdingPenalty = 50f;
    [SerializeField] private float cleanlinessBaseline = 60f;
    [SerializeField] private float hygieneFacilityContribution = 10f;
    [SerializeField] private float hygieneContributionMaximum = 25f;
    [SerializeField] private float cleanStreakContribution = 2f;
    [SerializeField] private float cleanStreakContributionMaximum = 10f;
    [SerializeField] private float cleanlinessDamagePenalty = 35f;
    [SerializeField, Range(0f, 1f)] private float cleanlinessCrowdingThreshold = 0.5f;
    [SerializeField] private float cleanlinessCrowdingPenalty = 40f;
    [SerializeField, Range(0f, 1f)] private float impressivenessBeautyWeight = 0.35f;
    [SerializeField, Range(0f, 1f)] private float impressivenessSpaciousnessWeight = 0.3f;
    [SerializeField, Range(0f, 1f)] private float impressivenessCleanlinessWeight = 0.2f;
    [SerializeField, Range(0f, 1f)] private float impressivenessQualityWeight = 0.15f;

    [Header("Mood")]
    [SerializeField, Min(0.25f)] private float moodDurationSeconds = 180f;
    [SerializeField] private float awfulRoomMood = -6f;
    [SerializeField] private float poorRoomMood = -3f;
    [SerializeField] private float goodRoomMood = 3f;
    [SerializeField] private float excellentRoomMood = 6f;
    [SerializeField] private float filthyRoomMood = -4f;
    [SerializeField] private float dirtyRoomMood = -2f;
    [SerializeField] private float cleanRoomMood = 2f;

    [Header("Room Role Colors")]
    [SerializeField] private List<FacilityRoleColorOverride> roleColorOverrides =
        new List<FacilityRoleColorOverride>();
    [SerializeField] private Color mixedColor =
        new Color(0.77f, 0.80f, 0.83f, 1f);
    [SerializeField] private Color undefinedColor =
        new Color(0.55f, 0.58f, 0.61f, 1f);

    public float SpaciousAreaMinimum => spaciousAreaMinimum;
    public float SpaciousAreaMaximum =>
        Mathf.Max(spaciousAreaMinimum + 1f, spaciousAreaMaximum);
    public float SpaciousAreaWeight => spaciousAreaWeight;
    public float SpaciousFreeCellWeight => spaciousFreeCellWeight;
    public float BeautyBaseline => beautyBaseline;
    public float LuxuryMultiplier => luxuryMultiplier;
    public float BeautyDamagePenalty => beautyDamagePenalty;
    public float BeautyCrowdingThreshold => beautyCrowdingThreshold;
    public float BeautyCrowdingPenalty => beautyCrowdingPenalty;
    public float CleanlinessBaseline => cleanlinessBaseline;
    public float HygieneFacilityContribution => hygieneFacilityContribution;
    public float HygieneContributionMaximum => hygieneContributionMaximum;
    public float CleanStreakContribution => cleanStreakContribution;
    public float CleanStreakContributionMaximum => cleanStreakContributionMaximum;
    public float CleanlinessDamagePenalty => cleanlinessDamagePenalty;
    public float CleanlinessCrowdingThreshold => cleanlinessCrowdingThreshold;
    public float CleanlinessCrowdingPenalty => cleanlinessCrowdingPenalty;
    public float ImpressivenessBeautyWeight => impressivenessBeautyWeight;
    public float ImpressivenessSpaciousnessWeight => impressivenessSpaciousnessWeight;
    public float ImpressivenessCleanlinessWeight => impressivenessCleanlinessWeight;
    public float ImpressivenessQualityWeight => impressivenessQualityWeight;
    public float MoodDurationSeconds => moodDurationSeconds;

    public DungeonStory.Rooms.RoomEnvironmentFormulaSettings
        CreateFormulaSettings()
    {
        return new DungeonStory.Rooms.RoomEnvironmentFormulaSettings(
            spaciousAreaMinimum,
            SpaciousAreaMaximum,
            spaciousAreaWeight,
            spaciousFreeCellWeight,
            beautyBaseline,
            luxuryMultiplier,
            beautyDamagePenalty,
            beautyCrowdingThreshold,
            beautyCrowdingPenalty,
            cleanlinessBaseline,
            hygieneContributionMaximum,
            cleanStreakContribution,
            cleanStreakContributionMaximum,
            cleanlinessDamagePenalty,
            cleanlinessCrowdingThreshold,
            cleanlinessCrowdingPenalty,
            impressivenessBeautyWeight,
            impressivenessSpaciousnessWeight,
            impressivenessCleanlinessWeight,
            impressivenessQualityWeight);
    }

    public DungeonStory.Rooms.RoomMoodDecision EvaluateMood(
        float impressiveness,
        float cleanliness)
    {
        return DungeonStory.Rooms.RoomExperienceRules.Evaluate(
            impressiveness,
            cleanliness,
            awfulRoomMood,
            poorRoomMood,
            goodRoomMood,
            excellentRoomMood,
            filthyRoomMood,
            dirtyRoomMood,
            cleanRoomMood,
            moodDurationSeconds);
    }

    public float GetImpressivenessMood(float value)
    {
        return EvaluateMood(value, 50f).ImpressionMood;
    }

    public float GetCleanlinessMood(float value)
    {
        return EvaluateMood(50f, value).CleanlinessMood;
    }

    public Color GetRoleColor(FacilityRole role, bool mixed)
    {
        if (mixed) return mixedColor;

        if (!FacilityRoleCatalog.TryGet(
                role,
                out FacilityRoleDefinition definition))
        {
            return undefinedColor;
        }

        FacilityRoleColorOverride colorOverride = roleColorOverrides?
            .FirstOrDefault(entry => entry != null
                && string.Equals(
                    entry.roleId,
                    definition.Id,
                    StringComparison.Ordinal));
        return colorOverride != null ? colorOverride.color : definition.Color;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IRoomEnvironmentSettingsProvider
{
    RoomEnvironmentSettingsSO Settings { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResourceRoomEnvironmentSettingsProvider :
    IRoomEnvironmentSettingsProvider
{
    public const string ResourcePath = "Config/RoomEnvironmentSettings";

    private readonly RoomEnvironmentSettingsSO settings;

    public ResourceRoomEnvironmentSettingsProvider(
        IRoomEnvironmentAuthoredContentPort content)
    {
        settings = (content ?? throw new ArgumentNullException(nameof(content)))
            .RoomEnvironmentSettings
            ?? throw new InvalidOperationException(
                "The authored room-environment settings asset is missing.");
    }

    public RoomEnvironmentSettingsSO Settings => settings;
}
