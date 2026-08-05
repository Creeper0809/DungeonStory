using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterNeedRecoverySource
{
    Meal,
    Drink,
    Rest,
    Toilet,
    Hygiene,
    Emergency,
    Debug
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public struct CharacterNeedResponseProfile
{
    [Range(0f, 100f)] public float routineStart;
    [Range(0f, 100f)] public float emergencyStart;
    [Range(0f, 100f)] public float resumeTarget;

    public CharacterNeedResponseProfile(
        float routineStart,
        float emergencyStart,
        float resumeTarget)
    {
        this.routineStart = Mathf.Clamp(routineStart, 0f, 100f);
        this.emergencyStart = Mathf.Clamp(emergencyStart, 0f, this.routineStart);
        this.resumeTarget = Mathf.Clamp(resumeTarget, this.routineStart, 100f);
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public struct CharacterNeedBalanceEntry
{
    public CharacterCondition condition;
    [Min(0f)] public float dailyDepletion;
    [Min(0f)] public float workDepletionPerSecond;
    public CharacterNeedResponseProfile response;

    public CharacterNeedBalanceEntry(
        CharacterCondition condition,
        float dailyDepletion,
        float workDepletionPerSecond,
        CharacterNeedResponseProfile response)
    {
        this.condition = condition;
        this.dailyDepletion = Mathf.Max(0f, dailyDepletion);
        this.workDepletionPerSecond = Mathf.Max(0f, workDepletionPerSecond);
        this.response = response;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public struct SurvivalPressureBalanceProfile
{
    public DungeonSurvivalPressure pressure;
    [Min(0f)] public float depletionMultiplier;
    [Min(0f)] public float recoveryMultiplier;
    [Min(0f)] public float personalContinuousWaterMultiplier;
    [Min(0f)] public float deprivationBurdenGainMultiplier;
    [Min(0f)] public float deprivationBurdenRecoveryMultiplier;
    [Min(0.1f)] public float forcedBreakdownDelaySeconds;
    [Min(0.1f)] public float highBurdenDamageIntervalSeconds;

    public SurvivalPressureBalanceProfile(
        DungeonSurvivalPressure pressure,
        float depletionMultiplier,
        float recoveryMultiplier,
        float personalContinuousWaterMultiplier,
        float deprivationBurdenGainMultiplier,
        float deprivationBurdenRecoveryMultiplier,
        float forcedBreakdownDelaySeconds,
        float highBurdenDamageIntervalSeconds)
    {
        this.pressure = pressure;
        this.depletionMultiplier = Mathf.Max(0f, depletionMultiplier);
        this.recoveryMultiplier = Mathf.Max(0f, recoveryMultiplier);
        this.personalContinuousWaterMultiplier =
            Mathf.Max(0f, personalContinuousWaterMultiplier);
        this.deprivationBurdenGainMultiplier =
            Mathf.Max(0f, deprivationBurdenGainMultiplier);
        this.deprivationBurdenRecoveryMultiplier =
            Mathf.Max(0f, deprivationBurdenRecoveryMultiplier);
        this.forcedBreakdownDelaySeconds =
            Mathf.Max(0.1f, forcedBreakdownDelaySeconds);
        this.highBurdenDamageIntervalSeconds =
            Mathf.Max(0.1f, highBurdenDamageIntervalSeconds);
    }
}

[CreateAssetMenu(
    fileName = "SurvivalBalanceSettings",
    menuName = "DungeonStory/Survival/Balance Settings")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SurvivalBalanceSettingsSO : ScriptableObject
{
    public const string ResourcePath = "SO/Survival/SurvivalBalanceSettings";
    public const float DefaultDayLengthSeconds = 180f;

    [SerializeField, Min(1f)] private float dayLengthSeconds =
        DefaultDayLengthSeconds;
    [SerializeField] private List<CharacterNeedBalanceEntry> needs =
        CreateDefaultNeeds();
    [SerializeField] private List<SurvivalPressureBalanceProfile> pressures =
        CreateDefaultPressures();

    public float DayLengthSeconds => Mathf.Max(1f, dayLengthSeconds);
    public IReadOnlyList<CharacterNeedBalanceEntry> Needs => needs;
    public IReadOnlyList<SurvivalPressureBalanceProfile> Pressures => pressures;

    public bool TryGetNeed(
        CharacterCondition condition,
        out CharacterNeedBalanceEntry entry)
    {
        if (needs != null)
        {
            for (int index = 0; index < needs.Count; index++)
            {
                if (needs[index].condition == condition)
                {
                    entry = needs[index];
                    return true;
                }
            }
        }

        return TryGetDefaultNeed(condition, out entry);
    }

    public SurvivalPressureBalanceProfile GetPressure(
        DungeonSurvivalPressure pressure)
    {
        DungeonSurvivalPressure normalized =
            DungeonSurvivalPressureRules.Normalize((int)pressure);
        if (pressures != null)
        {
            for (int index = 0; index < pressures.Count; index++)
            {
                if (pressures[index].pressure == normalized)
                {
                    return pressures[index];
                }
            }
        }

        return GetDefaultPressure(normalized);
    }

    public static bool TryGetDefaultNeed(
        CharacterCondition condition,
        out CharacterNeedBalanceEntry entry)
    {
        CharacterNeedBalanceEntry[] defaults = DefaultNeeds;
        for (int index = 0; index < defaults.Length; index++)
        {
            if (defaults[index].condition == condition)
            {
                entry = defaults[index];
                return true;
            }
        }

        entry = default;
        return false;
    }

    public static SurvivalPressureBalanceProfile GetDefaultPressure(
        DungeonSurvivalPressure pressure)
    {
        DungeonSurvivalPressure normalized =
            DungeonSurvivalPressureRules.Normalize((int)pressure);
        SurvivalPressureBalanceProfile[] defaults = DefaultPressures;
        for (int index = 0; index < defaults.Length; index++)
        {
            if (defaults[index].pressure == normalized)
            {
                return defaults[index];
            }
        }

        return defaults[0];
    }

    public void ResetToDefaults()
    {
        dayLengthSeconds = DefaultDayLengthSeconds;
        needs = CreateDefaultNeeds();
        pressures = CreateDefaultPressures();
    }

    private static List<CharacterNeedBalanceEntry> CreateDefaultNeeds()
    {
        return new List<CharacterNeedBalanceEntry>(DefaultNeeds);
    }

    private static List<SurvivalPressureBalanceProfile> CreateDefaultPressures()
    {
        return new List<SurvivalPressureBalanceProfile>(DefaultPressures);
    }

    private static readonly CharacterNeedBalanceEntry[] DefaultNeeds =
    {
        new CharacterNeedBalanceEntry(
            CharacterCondition.HUNGER,
            50f,
            0f,
            new CharacterNeedResponseProfile(65f, 35f, 75f)),
        new CharacterNeedBalanceEntry(
            CharacterCondition.THIRST,
            60f,
            0f,
            new CharacterNeedResponseProfile(60f, 35f, 75f)),
        new CharacterNeedBalanceEntry(
            CharacterCondition.SLEEP,
            0f,
            0.35f,
            new CharacterNeedResponseProfile(60f, 30f, 70f)),
        new CharacterNeedBalanceEntry(
            CharacterCondition.EXCRETION,
            24f,
            0.05f,
            new CharacterNeedResponseProfile(45f, 25f, 70f)),
        new CharacterNeedBalanceEntry(
            CharacterCondition.HYGIENE,
            18f,
            0.06f,
            new CharacterNeedResponseProfile(40f, 20f, 65f))
    };

    private static readonly SurvivalPressureBalanceProfile[] DefaultPressures =
    {
        new SurvivalPressureBalanceProfile(
            DungeonSurvivalPressure.Standard,
            1f,
            1f,
            1f,
            1f,
            1f,
            30f,
            10f),
        new SurvivalPressureBalanceProfile(
            DungeonSurvivalPressure.Relaxed,
            0.75f,
            1.10f,
            0.85f,
            0.75f,
            1.15f,
            45f,
            12f),
        new SurvivalPressureBalanceProfile(
            DungeonSurvivalPressure.Harsh,
            1.25f,
            0.90f,
            1.15f,
            1.25f,
            0.85f,
            24f,
            8f)
    };
}
