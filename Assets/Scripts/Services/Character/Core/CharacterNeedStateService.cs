using System;
using UnityEngine;

public readonly struct CharacterNeedDecayBatch
{
    public CharacterNeedDecayBatch(
        float hunger,
        float thirst,
        float fun,
        float excretion,
        float hygiene)
    {
        Hunger = hunger;
        Thirst = thirst;
        Fun = fun;
        Excretion = excretion;
        Hygiene = hygiene;
    }

    public float Hunger { get; }
    public float Thirst { get; }
    public float Fun { get; }
    public float Excretion { get; }
    public float Hygiene { get; }
}

/// <summary>
/// Calculates need decay and recovery deltas. CharacterStats remains the sole
/// owner and publisher of condition values.
/// </summary>
public sealed class CharacterNeedStateService
{
    private readonly ICharacterNeedBalanceRuntime balance;
    private readonly IDungeonDebugRuleQuery debugRules;

    public CharacterNeedStateService(
        ICharacterNeedBalanceRuntime balance,
        IDungeonDebugRuleQuery debugRules)
    {
        this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
        this.debugRules = debugRules
            ?? throw new ArgumentNullException(nameof(debugRules));
    }

    public bool ShouldFreeze(CharacterCondition condition, float delta) =>
        debugRules.ShouldFreezeNeed(condition, delta);

    public float ApplyRecoveryMultiplier(
        CharacterCondition condition,
        float amount,
        CharacterNeedRecoverySource source) =>
        balance.ApplyRecoveryMultiplier(condition, amount, source);

    public CharacterNeedResponseProfile GetResponse(
        CharacterCondition condition) => balance.GetResponse(condition);

    public float GetWorkDepletion(
        CharacterCondition condition,
        float elapsedSeconds) =>
        balance.GetWorkDepletion(condition, elapsedSeconds);

    public CharacterNeedDecayBatch CalculateTimedDecay(
        CharacterActor actor,
        float elapsedSeconds)
    {
        SpeciesNeedProfile speciesNeeds =
            actor?.profile?.GetNeedProfile() ?? new SpeciesNeedProfile();
        float consumptionMultiplier = actor != null
            ? Mathf.Max(0f, actor.GetConsumptionMultiplier())
            : 1f;
        float hungerMultiplier = GetPersonaMultiplier(
            actor,
            CharacterCondition.HUNGER)
            * Mathf.Max(0f, speciesNeeds.hungerRateMultiplier)
            * consumptionMultiplier;
        float thirstMultiplier = GetPersonaMultiplier(
            actor,
            CharacterCondition.THIRST)
            * Mathf.Max(0f, speciesNeeds.thirstRateMultiplier);
        float funMultiplier = GetPersonaMultiplier(
            actor,
            CharacterCondition.FUN);
        float excretionMultiplier = GetPersonaMultiplier(
            actor,
            CharacterCondition.EXCRETION)
            * Mathf.Max(
                0f,
                Mathf.Max(
                    speciesNeeds.hungerRateMultiplier,
                    speciesNeeds.thirstRateMultiplier))
            * consumptionMultiplier;
        float hygieneMultiplier = GetPersonaMultiplier(
            actor,
            CharacterCondition.HYGIENE)
            * Mathf.Max(0f, speciesNeeds.hygieneRateMultiplier);

        return new CharacterNeedDecayBatch(
            balance.GetTimedDepletion(
                CharacterCondition.HUNGER,
                elapsedSeconds,
                speciesMultiplier: 1f,
                personaMultiplier: hungerMultiplier),
            balance.GetTimedDepletion(
                CharacterCondition.THIRST,
                elapsedSeconds,
                speciesMultiplier: 1f,
                personaMultiplier: thirstMultiplier),
            balance.GetTimedDepletion(
                CharacterCondition.FUN,
                elapsedSeconds,
                speciesMultiplier: 1f,
                personaMultiplier: funMultiplier),
            balance.GetTimedDepletion(
                CharacterCondition.EXCRETION,
                elapsedSeconds,
                speciesMultiplier: 1f,
                personaMultiplier: excretionMultiplier),
            balance.GetTimedDepletion(
                CharacterCondition.HYGIENE,
                elapsedSeconds,
                speciesMultiplier: 1f,
                personaMultiplier: hygieneMultiplier));
    }

    private static float GetPersonaMultiplier(
        CharacterActor actor,
        CharacterCondition condition)
    {
        return actor?.PersonaRuntime != null
            ? actor.PersonaRuntime.GetConditionCurveMultiplier(condition)
            : 1f;
    }
}

public sealed class DefaultCharacterNeedBalanceRuntime :
    ICharacterNeedBalanceRuntime
{
    public static readonly DefaultCharacterNeedBalanceRuntime Instance = new();
    private DefaultCharacterNeedBalanceRuntime() { }

    public DungeonSurvivalPressure Pressure => DungeonSurvivalPressure.Standard;
    public float DayLengthSeconds =>
        SurvivalBalanceSettingsSO.DefaultDayLengthSeconds;
    public float GetDailyDepletion(CharacterCondition condition) =>
        SurvivalBalanceSettingsSO.TryGetDefaultNeed(condition, out var entry)
            ? entry.dailyDepletion
            : 0f;
    public float GetTimedDepletion(
        CharacterCondition condition,
        float elapsedSeconds,
        float speciesMultiplier = 1f,
        float personaMultiplier = 1f) =>
        GetDailyDepletion(condition)
        * Mathf.Max(0f, speciesMultiplier)
        * Mathf.Max(0f, personaMultiplier)
        * Mathf.Max(0f, elapsedSeconds)
        / DayLengthSeconds;
    public float GetWorkDepletion(
        CharacterCondition condition,
        float elapsedSeconds = 1f) =>
        SurvivalBalanceSettingsSO.TryGetDefaultNeed(condition, out var entry)
            ? entry.workDepletionPerSecond * Mathf.Max(0f, elapsedSeconds)
            : 0f;
    public CharacterNeedResponseProfile GetResponse(
        CharacterCondition condition) =>
        SurvivalBalanceSettingsSO.TryGetDefaultNeed(condition, out var entry)
            ? entry.response
            : new CharacterNeedResponseProfile(0f, 0f, 100f);
    public float ApplyRecoveryMultiplier(
        CharacterCondition condition,
        float amount,
        CharacterNeedRecoverySource source) => amount;
    public float ApplyPersonalContinuousWaterMultiplier(float amount) =>
        Mathf.Max(0f, amount);
    public float GetDeprivationBurdenMultiplier(bool recovering) => 1f;
    public float ForcedBreakdownDelaySeconds => 60f;
    public float HighBurdenDamageIntervalSeconds => 5f;
}
