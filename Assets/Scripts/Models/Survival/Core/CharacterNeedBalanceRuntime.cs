using System;
using UnityEngine;

public interface ICharacterNeedBalanceRuntime
{
    DungeonSurvivalPressure Pressure { get; }
    float DayLengthSeconds { get; }
    float GetDailyDepletion(CharacterCondition condition);
    float GetTimedDepletion(
        CharacterCondition condition,
        float elapsedSeconds,
        float speciesMultiplier = 1f,
        float personaMultiplier = 1f);
    float GetWorkDepletion(
        CharacterCondition condition,
        float elapsedSeconds = 1f);
    CharacterNeedResponseProfile GetResponse(CharacterCondition condition);
    float ApplyRecoveryMultiplier(
        CharacterCondition condition,
        float amount,
        CharacterNeedRecoverySource source);
    float ApplyPersonalContinuousWaterMultiplier(float amount);
    float GetDeprivationBurdenMultiplier(bool recovering);
    float ForcedBreakdownDelaySeconds { get; }
    float HighBurdenDamageIntervalSeconds { get; }
}

public interface ISurvivalPressureProvider
{
    DungeonSurvivalPressure GetSurvivalPressure();
}

public sealed class CharacterNeedBalanceRuntime : ICharacterNeedBalanceRuntime
{
    private readonly SurvivalBalanceSettingsSO settings;
    private readonly ISurvivalPressureProvider pressureProvider;

    public CharacterNeedBalanceRuntime(
        IGameContentDefinitionSource content,
        ISurvivalPressureProvider pressureProvider)
    {
        this.pressureProvider = pressureProvider
            ?? throw new ArgumentNullException(nameof(pressureProvider));
        settings = (content ?? throw new ArgumentNullException(nameof(content)))
            .RequireSingle<SurvivalBalanceSettingsSO>();
    }

    public DungeonSurvivalPressure Pressure =>
        pressureProvider.GetSurvivalPressure();

    public float DayLengthSeconds =>
        settings.DayLengthSeconds;

    public float GetDailyDepletion(CharacterCondition condition)
    {
        return TryGetNeed(condition, out CharacterNeedBalanceEntry entry)
            ? Mathf.Max(0f, entry.dailyDepletion)
                * GetPressure().depletionMultiplier
            : 0f;
    }

    public float GetTimedDepletion(
        CharacterCondition condition,
        float elapsedSeconds,
        float speciesMultiplier = 1f,
        float personaMultiplier = 1f)
    {
        return GetDailyDepletion(condition)
            * Mathf.Max(0f, speciesMultiplier)
            * Mathf.Max(0f, personaMultiplier)
            * Mathf.Max(0f, elapsedSeconds)
            / DayLengthSeconds;
    }

    public float GetWorkDepletion(
        CharacterCondition condition,
        float elapsedSeconds = 1f)
    {
        return TryGetNeed(condition, out CharacterNeedBalanceEntry entry)
            ? Mathf.Max(0f, entry.workDepletionPerSecond)
                * GetPressure().depletionMultiplier
                * Mathf.Max(0f, elapsedSeconds)
            : 0f;
    }

    public CharacterNeedResponseProfile GetResponse(
        CharacterCondition condition)
    {
        return TryGetNeed(condition, out CharacterNeedBalanceEntry entry)
            ? entry.response
            : new CharacterNeedResponseProfile(0f, 0f, 100f);
    }

    public float ApplyRecoveryMultiplier(
        CharacterCondition condition,
        float amount,
        CharacterNeedRecoverySource source)
    {
        if (amount <= 0f || source == CharacterNeedRecoverySource.Debug)
        {
            return amount;
        }

        return IsBalancedNeed(condition)
            ? amount * GetPressure().recoveryMultiplier
            : amount;
    }

    public float ApplyPersonalContinuousWaterMultiplier(float amount)
    {
        return Mathf.Max(0f, amount)
            * GetPressure().personalContinuousWaterMultiplier;
    }

    public float GetDeprivationBurdenMultiplier(bool recovering)
    {
        SurvivalPressureBalanceProfile profile = GetPressure();
        return recovering
            ? profile.deprivationBurdenRecoveryMultiplier
            : profile.deprivationBurdenGainMultiplier;
    }

    public float ForcedBreakdownDelaySeconds =>
        GetPressure().forcedBreakdownDelaySeconds;

    public float HighBurdenDamageIntervalSeconds =>
        GetPressure().highBurdenDamageIntervalSeconds;

    private bool TryGetNeed(
        CharacterCondition condition,
        out CharacterNeedBalanceEntry entry)
    {
        return settings != null
            ? settings.TryGetNeed(condition, out entry)
            : SurvivalBalanceSettingsSO.TryGetDefaultNeed(condition, out entry);
    }

    private SurvivalPressureBalanceProfile GetPressure()
    {
        return settings != null
            ? settings.GetPressure(Pressure)
            : SurvivalBalanceSettingsSO.GetDefaultPressure(Pressure);
    }

    private static bool IsBalancedNeed(CharacterCondition condition)
    {
        return condition == CharacterCondition.HUNGER
            || condition == CharacterCondition.THIRST
            || condition == CharacterCondition.SLEEP
            || condition == CharacterCondition.EXCRETION
            || condition == CharacterCondition.HYGIENE;
    }
}
