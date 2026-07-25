using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct FacilityCrimeRiskContext
{
    public readonly BuildableObject building;
    public readonly CharacterActor actor;
    public readonly bool hasServingWorker;
    public readonly bool hasWaitingCheckout;
    public readonly int currentUserCount;
    public readonly int cartItemCount;
    public readonly int cartValue;
    public readonly int currentStock;
    public readonly bool isDamaged;

    public FacilityCrimeRiskContext(
        BuildableObject building,
        CharacterActor actor,
        bool hasServingWorker,
        bool hasWaitingCheckout,
        int currentUserCount,
        int cartItemCount,
        int cartValue,
        int currentStock,
        bool isDamaged)
    {
        this.building = building;
        this.actor = actor;
        this.hasServingWorker = hasServingWorker;
        this.hasWaitingCheckout = hasWaitingCheckout;
        this.currentUserCount = Mathf.Max(0, currentUserCount);
        this.cartItemCount = Mathf.Max(0, cartItemCount);
        this.cartValue = Mathf.Max(0, cartValue);
        this.currentStock = Mathf.Max(0, currentStock);
        this.isDamaged = isDamaged;
    }

    public FacilityData Facility => building != null ? building.Facility : null;
}

public interface IFacilityCrimeRiskEvaluator
{
    float CalculateShopliftingChance(FacilityCrimeRiskContext context);
    float CalculateOperationalRisk(FacilityCrimeRiskContext context);
    bool ShouldTriggerCrime(float chance, float roll);
}

public sealed class FacilityCrimeRiskEvaluator : IFacilityCrimeRiskEvaluator
{
    private readonly IFacilityCrimeSettingsProvider settingsProvider;

    public FacilityCrimeRiskEvaluator(IFacilityCrimeSettingsProvider settingsProvider)
    {
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
    }

    public float CalculateShopliftingChance(FacilityCrimeRiskContext context)
    {
        FacilityData facility = context.Facility;
        if (facility == null || context.cartItemCount <= 0 || context.currentStock <= 0)
        {
            return 0f;
        }

        FacilityCrimeSettingsSO settings = GetSettings();
        float pressure = settings.BaseCrimePressure;
        pressure += GetSupervisionPressure(context, settings);
        pressure += GetNeedPressure(context, settings);
        pressure += GetCrowdPressure(context, settings);
        pressure += GetCartValuePressure(context, settings);
        pressure += GetFacilityStatePressure(context, settings);
        pressure = ApplyBuildingModifiers(context, pressure);
        pressure *= context.actor != null ? context.actor.GetCrimeRiskMultiplier() : 1f;
        return Mathf.Clamp01(pressure);
    }

    public float CalculateOperationalRisk(FacilityCrimeRiskContext context)
    {
        FacilityData facility = context.Facility;
        if (facility == null || context.currentStock <= 0)
        {
            return 0f;
        }

        FacilityCrimeRiskContext baselineContext = new FacilityCrimeRiskContext(
            context.building,
            context.actor,
            context.hasServingWorker,
            context.hasWaitingCheckout,
            context.currentUserCount,
            cartItemCount: 1,
            cartValue: context.cartValue,
            context.currentStock,
            context.isDamaged);

        FacilityCrimeSettingsSO settings = GetSettings();
        float risk = CalculateShopliftingChance(baselineContext) * settings.OperationalRiskScale;
        if (context.hasWaitingCheckout && !context.hasServingWorker)
        {
            risk += settings.CrowdCrimeRiskWeight;
        }

        return Mathf.Max(0f, risk);
    }

    public bool ShouldTriggerCrime(float chance, float roll)
    {
        return chance > 0f && Mathf.Clamp01(roll) < Mathf.Clamp01(chance);
    }

    private FacilityCrimeSettingsSO GetSettings()
    {
        return settingsProvider.Settings
            ?? throw new InvalidOperationException("Facility crime settings provider returned no settings.");
    }

    private static float GetSupervisionPressure(
        FacilityCrimeRiskContext context,
        FacilityCrimeSettingsSO settings)
    {
        return context.hasServingWorker
            ? -settings.StaffedSupervisionReduction
            : settings.UnstaffedSupervisionRisk;
    }

    private static float GetNeedPressure(
        FacilityCrimeRiskContext context,
        FacilityCrimeSettingsSO settings)
    {
        CharacterStats stats = context.actor != null ? context.actor.Stats : null;
        if (stats == null)
        {
            return 0f;
        }

        float mood = GetStat(stats, CharacterCondition.MOOD, 50f);
        float hunger = GetStat(stats, CharacterCondition.HUNGER, 50f);
        float fun = GetStat(stats, CharacterCondition.FUN, 50f);
        float sleep = GetStat(stats, CharacterCondition.SLEEP, 50f);
        float excretion = GetStat(stats, CharacterCondition.EXCRETION, 50f);
        float hygiene = GetStat(stats, CharacterCondition.HYGIENE, 50f);

        float lowMoodPressure = settings.LowMoodCrimeRiskWeight
            * LowStatPressure(mood, comfortable: 65f, critical: 15f);
        float unmetNeedPressure = settings.UnmetNeedCrimeRiskWeight
            * Mathf.Max(
                LowStatPressure(hunger, comfortable: 45f, critical: 5f),
                LowStatPressure(fun, comfortable: 40f, critical: 0f) * 0.6f,
                LowStatPressure(sleep, comfortable: 35f, critical: 0f) * 0.4f,
                LowStatPressure(excretion, comfortable: 55f, critical: 10f) * 0.9f,
                LowStatPressure(hygiene, comfortable: 45f, critical: 5f) * 0.55f);
        return lowMoodPressure + unmetNeedPressure;
    }

    private static float GetCrowdPressure(
        FacilityCrimeRiskContext context,
        FacilityCrimeSettingsSO settings)
    {
        FacilityData facility = context.Facility;
        if (facility == null || facility.capacity <= 0)
        {
            return 0f;
        }

        float crowdRatio = Mathf.Clamp01((float)context.currentUserCount / Mathf.Max(1, facility.capacity));
        float waitPressure = context.hasWaitingCheckout && !context.hasServingWorker ? 0.5f : 0f;
        return settings.CrowdCrimeRiskWeight * Mathf.Clamp01(crowdRatio + waitPressure);
    }

    private static float GetCartValuePressure(
        FacilityCrimeRiskContext context,
        FacilityCrimeSettingsSO settings)
    {
        if (context.cartValue <= 0)
        {
            return 0f;
        }

        float valuePressure = Mathf.Clamp01(context.cartValue / 500f);
        float countPressure = Mathf.Clamp01((context.cartItemCount - 1f) / 3f) * 0.5f;
        return settings.HighValueCrimeRiskWeight * Mathf.Clamp01(valuePressure + countPressure);
    }

    private static float GetFacilityStatePressure(
        FacilityCrimeRiskContext context,
        FacilityCrimeSettingsSO settings)
    {
        FacilityData facility = context.Facility;
        if (facility == null)
        {
            return 0f;
        }

        float pressure = context.isDamaged ? settings.DamagedFacilityCrimeRiskWeight : 0f;
        if (context.currentStock <= context.building.GetRestockRequestThreshold())
        {
            pressure += settings.HighValueCrimeRiskWeight * 0.25f;
        }

        return pressure;
    }

    private static float ApplyBuildingModifiers(FacilityCrimeRiskContext context, float pressure)
    {
        IReadOnlyList<BuildingAbility> abilities = context.building != null
            && context.building.BuildingData != null
                ? context.building.BuildingData.Abilities
                : null;
        if (abilities == null)
        {
            return Mathf.Max(0f, pressure);
        }

        float result = pressure;
        foreach (BuildingAbility ability in abilities)
        {
            if (ability is IBuildingCrimeRiskModifier modifier)
            {
                result = modifier.ModifyCrimePressure(result, context);
            }
        }

        return Mathf.Max(0f, result);
    }

    private static float GetStat(CharacterStats stats, CharacterCondition condition, float defaultValue)
    {
        return stats.Stats.TryGetValue(condition, out float value) ? Mathf.Clamp(value, 0f, 100f) : defaultValue;
    }

    private static float LowStatPressure(float value, float comfortable, float critical)
    {
        float range = Mathf.Max(1f, comfortable - critical);
        return Mathf.Clamp01((comfortable - value) / range);
    }
}
