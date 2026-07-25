using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class FacilityRoleColorOverride
{
    public string roleId;
    public Color color = Color.white;
}

[CreateAssetMenu(fileName = "RoomEnvironmentSettings", menuName = "DungeonStory/Rooms/Environment Settings", order = 0)]
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
    [SerializeField] private Color mixedColor = new Color(0.77f, 0.80f, 0.83f, 1f);
    [SerializeField] private Color undefinedColor = new Color(0.55f, 0.58f, 0.61f, 1f);

    public float SpaciousAreaMinimum => spaciousAreaMinimum;
    public float SpaciousAreaMaximum => Mathf.Max(spaciousAreaMinimum + 1f, spaciousAreaMaximum);
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

    public float GetImpressivenessMood(float value)
    {
        if (value < 20f) return awfulRoomMood;
        if (value < 40f) return poorRoomMood;
        if (value < 60f) return 0f;
        if (value < 80f) return goodRoomMood;
        return excellentRoomMood;
    }

    public float GetCleanlinessMood(float value)
    {
        if (value < 20f) return filthyRoomMood;
        if (value < 40f) return dirtyRoomMood;
        if (value < 80f) return 0f;
        return cleanRoomMood;
    }

    public Color GetRoleColor(FacilityRole role, bool mixed)
    {
        if (mixed) return mixedColor;

        if (!FacilityRoleCatalog.TryGet(role, out FacilityRoleDefinition definition))
        {
            return undefinedColor;
        }

        FacilityRoleColorOverride colorOverride = roleColorOverrides?
            .FirstOrDefault((entry) => entry != null
                && string.Equals(entry.roleId, definition.Id, StringComparison.Ordinal));
        return colorOverride != null ? colorOverride.color : definition.Color;
    }
}

public interface IRoomEnvironmentSettingsProvider
{
    RoomEnvironmentSettingsSO Settings { get; }
}

public sealed class ResourceRoomEnvironmentSettingsProvider : IRoomEnvironmentSettingsProvider
{
    public const string ResourcePath = "Config/RoomEnvironmentSettings";

    private readonly IResourcesAssetLoader assetLoader;
    private RoomEnvironmentSettingsSO settings;

    public ResourceRoomEnvironmentSettingsProvider(IResourcesAssetLoader assetLoader)
    {
        this.assetLoader = assetLoader
            ?? throw new ArgumentNullException(nameof(assetLoader));
    }

    public RoomEnvironmentSettingsSO Settings
    {
        get
        {
            if (settings == null)
            {
                settings = assetLoader.LoadRequired<RoomEnvironmentSettingsSO>(ResourcePath);
            }

            return settings;
        }
    }
}
