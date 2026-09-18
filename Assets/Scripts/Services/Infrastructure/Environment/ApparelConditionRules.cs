using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct ApparelConditionCoverage
{
    public ApparelConditionCoverage(
        ApparelLayer layer,
        AnatomyAttachmentPoint occupiedPoints)
    {
        Layer = layer;
        OccupiedPoints = occupiedPoints;
    }

    public ApparelLayer Layer { get; }
    public AnatomyAttachmentPoint OccupiedPoints { get; }
}

public readonly struct ApparelConditionStepInput
{
    public ApparelConditionStepInput(
        float deltaSeconds,
        ApparelLayer layer,
        float hygiene,
        bool working,
        bool exterior,
        bool rainIngress,
        float exposedFraction,
        float nominalMaterialDurability,
        float currentWaterResistance,
        float dryingRate,
        float durability,
        float moisture,
        float contamination)
    {
        DeltaSeconds = deltaSeconds;
        Layer = layer;
        Hygiene = hygiene;
        Working = working;
        Exterior = exterior;
        RainIngress = rainIngress;
        ExposedFraction = exposedFraction;
        NominalMaterialDurability = nominalMaterialDurability;
        CurrentWaterResistance = currentWaterResistance;
        DryingRate = dryingRate;
        Durability = durability;
        Moisture = moisture;
        Contamination = contamination;
    }

    public float DeltaSeconds { get; }
    public ApparelLayer Layer { get; }
    public float Hygiene { get; }
    public bool Working { get; }
    public bool Exterior { get; }
    public bool RainIngress { get; }
    public float ExposedFraction { get; }
    public float NominalMaterialDurability { get; }
    public float CurrentWaterResistance { get; }
    public float DryingRate { get; }
    public float Durability { get; }
    public float Moisture { get; }
    public float Contamination { get; }
}

public readonly struct ApparelConditionStepResult
{
    public ApparelConditionStepResult(
        float durability,
        float moisture,
        float contamination)
    {
        Durability = durability;
        Moisture = moisture;
        Contamination = contamination;
    }

    public float Durability { get; }
    public float Moisture { get; }
    public float Contamination { get; }
}

public static class ApparelConditionRules
{
    public const float DailyBodyContactContamination = 8f;
    public const float DailyWorkContamination = 8f;
    public const float DailyRainContamination = 4f;
    public const float DailyBaseWear = 0.5f;
    public const float DailyWorkWear = 2f;
    public const float ReferenceMaterialDurability = 60f;
    public const float MinimumWearResistanceMultiplier = 0.25f;
    public const float MaximumWearResistanceMultiplier = 4f;
    public const float DailyRainIngress = 400f;
    public const float DailyNaturalDrying = 80f;
    public const float AutomaticContaminationTrigger = 60f;
    public const float AutomaticMoistureTrigger = 60f;
    public const float AutomaticDurabilityTrigger = 40f;
    public const float ReplacementMaximumContamination = 20f;
    public const float ReplacementMaximumMoisture = 20f;
    public const float ReplacementMinimumDurability = 70f;

    public static ApparelConditionStepResult Step(
        ApparelConditionStepInput input)
    {
        RequireFinite(input.DeltaSeconds, nameof(input.DeltaSeconds));
        RequireFinite(input.Hygiene, nameof(input.Hygiene));
        RequireFinite(input.ExposedFraction, nameof(input.ExposedFraction));
        RequireFinite(
            input.NominalMaterialDurability,
            nameof(input.NominalMaterialDurability));
        RequireFinite(
            input.CurrentWaterResistance,
            nameof(input.CurrentWaterResistance));
        RequireFinite(input.DryingRate, nameof(input.DryingRate));
        RequireFinite(input.Durability, nameof(input.Durability));
        RequireFinite(input.Moisture, nameof(input.Moisture));
        RequireFinite(input.Contamination, nameof(input.Contamination));

        float elapsedDays = Mathf.Max(0f, input.DeltaSeconds)
            / GameCalendarRules.SecondsPerDay;
        float exposed = Mathf.Clamp01(input.ExposedFraction);
        float hygiene = Mathf.Clamp(input.Hygiene, 0f, 100f) / 100f;
        float bodyContact = DailyBodyContactContamination
            * (1f + (1f - hygiene))
            * ResolveLayerContact(input.Layer);
        float direct = (input.Working ? DailyWorkContamination : 0f) * exposed
            + (input.RainIngress && input.Exterior
                ? DailyRainContamination * exposed
                : 0f);
        float wearResistance = Mathf.Clamp(
            ReferenceMaterialDurability
                / Mathf.Max(float.Epsilon, input.NominalMaterialDurability),
            MinimumWearResistanceMultiplier,
            MaximumWearResistanceMultiplier);
        float wear = (DailyBaseWear + (input.Working ? DailyWorkWear : 0f))
            * wearResistance;
        float rainIngress = input.RainIngress && input.Exterior
            ? DailyRainIngress
                * exposed
                * (1f - Mathf.Clamp01(input.CurrentWaterResistance))
            : 0f;
        float moisture = rainIngress > 0f
            ? rainIngress
            : -DailyNaturalDrying
                * Mathf.Max(0f, input.DryingRate)
                * (0.25f + 0.75f * exposed);

        return new ApparelConditionStepResult(
            Mathf.Clamp(input.Durability - wear * elapsedDays, 0f, 100f),
            Mathf.Clamp(input.Moisture + moisture * elapsedDays, 0f, 100f),
            Mathf.Clamp(
                input.Contamination + (bodyContact + direct) * elapsedDays,
                0f,
                100f));
    }

    public static float ResolveLayerContact(ApparelLayer layer) => layer switch
    {
        ApparelLayer.Underwear => 1f,
        ApparelLayer.Inner => 0.75f,
        ApparelLayer.Outer => 0.25f,
        ApparelLayer.Armor => 0.1f,
        ApparelLayer.Accessory => 0.1f,
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null)
    };

    public static float ResolveExposedFraction(
        ApparelLayer layer,
        AnatomyAttachmentPoint occupiedPoints,
        IReadOnlyList<ApparelConditionCoverage> outfit)
    {
        uint occupied = (uint)occupiedPoints;
        int occupiedCount = CountBits(occupied);
        if (occupiedCount == 0)
        {
            return 0f;
        }

        uint covered = 0u;
        IReadOnlyList<ApparelConditionCoverage> source =
            outfit ?? Array.Empty<ApparelConditionCoverage>();
        for (int index = 0; index < source.Count; index++)
        {
            ApparelConditionCoverage candidate = source[index];
            if (candidate.Layer == ApparelLayer.Accessory
                || candidate.Layer <= layer)
            {
                continue;
            }

            covered |= (uint)candidate.OccupiedPoints;
        }

        int exposedCount = CountBits(occupied & ~covered);
        return Mathf.Clamp01(exposedCount / (float)occupiedCount);
    }

    public static bool ShouldRetainCurrent(
        float durability,
        float moisture,
        float contamination) =>
        IsFinite(durability)
        && IsFinite(moisture)
        && IsFinite(contamination)
        && contamination < AutomaticContaminationTrigger
        && moisture < AutomaticMoistureTrigger
        && durability >= AutomaticDurabilityTrigger;

    public static bool IsReplacementEligible(
        float durability,
        float moisture,
        float contamination) =>
        IsFinite(durability)
        && IsFinite(moisture)
        && IsFinite(contamination)
        && contamination <= ReplacementMaximumContamination
        && moisture < ReplacementMaximumMoisture
        && durability >= ReplacementMinimumDurability;

    private static int CountBits(uint value)
    {
        int count = 0;
        while (value != 0u)
        {
            value &= value - 1u;
            count++;
        }
        return count;
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static void RequireFinite(float value, string parameterName)
    {
        if (!IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
