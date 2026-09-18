using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WildlifeMigrationCapability
{
    Unspecified = 0,
    GeneralHabitat = 1,
    LoreOnly = 2,
    FoulWater = 3,
    TemperatureBand = 4,
    Carcass = 5
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeMigrationProfile
{
    public const int MinimumDetectionRadiusCells = 1;
    public const int MaximumDetectionRadiusCells = 32;

    [SerializeField]
    private WildlifeMigrationCapability capability;
    [SerializeField]
    private int detectionRadiusCells;
    [SerializeField]
    private float signalScore;
    [SerializeField]
    private float minimumWaterRemainingRatio;
    [SerializeField]
    private int minimumCarcassQuantity;
    [SerializeField]
    private float minimumTemperatureC;
    [SerializeField]
    private float maximumTemperatureC;

    public WildlifeMigrationProfile(
        WildlifeMigrationCapability capability,
        int detectionRadiusCells,
        float signalScore = 0f,
        float minimumWaterRemainingRatio = 0f,
        int minimumCarcassQuantity = 0,
        float minimumTemperatureC = 0f,
        float maximumTemperatureC = 0f)
    {
        this.capability = capability;
        this.detectionRadiusCells = detectionRadiusCells;
        this.signalScore = signalScore;
        this.minimumWaterRemainingRatio = minimumWaterRemainingRatio;
        this.minimumCarcassQuantity = minimumCarcassQuantity;
        this.minimumTemperatureC = minimumTemperatureC;
        this.maximumTemperatureC = maximumTemperatureC;
        RequireValid();
    }

    public WildlifeMigrationCapability Capability => capability;
    public int DetectionRadiusCells => detectionRadiusCells;
    public float SignalScore => signalScore;
    public float MinimumWaterRemainingRatio => minimumWaterRemainingRatio;
    public int MinimumCarcassQuantity => minimumCarcassQuantity;
    public float MinimumTemperatureC => minimumTemperatureC;
    public float MaximumTemperatureC => maximumTemperatureC;

    public static WildlifeMigrationProfile GeneralHabitat(
        int detectionRadiusCells = 12) =>
        new(
            WildlifeMigrationCapability.GeneralHabitat,
            detectionRadiusCells);

    public static WildlifeMigrationProfile LoreOnly(
        int detectionRadiusCells = 12) =>
        new(
            WildlifeMigrationCapability.LoreOnly,
            detectionRadiusCells);

    public static WildlifeMigrationProfile FoulWater(
        int detectionRadiusCells = 10,
        float signalScore = 8f,
        float minimumRemainingRatio = 0.25f) =>
        new(
            WildlifeMigrationCapability.FoulWater,
            detectionRadiusCells,
            signalScore,
            minimumWaterRemainingRatio: minimumRemainingRatio);

    public static WildlifeMigrationProfile TemperatureBand(
        float minimumTemperatureC,
        float maximumTemperatureC,
        int detectionRadiusCells = 8,
        float signalScore = 7f) =>
        new(
            WildlifeMigrationCapability.TemperatureBand,
            detectionRadiusCells,
            signalScore,
            minimumTemperatureC: minimumTemperatureC,
            maximumTemperatureC: maximumTemperatureC);

    public static WildlifeMigrationProfile Carcass(
        int detectionRadiusCells = 12,
        float signalScore = 9f,
        int minimumQuantity = 1) =>
        new(
            WildlifeMigrationCapability.Carcass,
            detectionRadiusCells,
            signalScore,
            minimumCarcassQuantity: minimumQuantity);

    public WildlifeMigrationProfile Snapshot()
    {
        RequireValid();
        return new WildlifeMigrationProfile(
            capability,
            detectionRadiusCells,
            signalScore,
            minimumWaterRemainingRatio,
            minimumCarcassQuantity,
            minimumTemperatureC,
            maximumTemperatureC);
    }

    public IReadOnlyList<string> Validate(string speciesId = "")
    {
        List<string> errors = new();
        string owner = string.IsNullOrWhiteSpace(speciesId)
            ? "Wildlife migration profile"
            : $"Wildlife species '{speciesId.Trim()}' migration profile";
        if (!Enum.IsDefined(typeof(WildlifeMigrationCapability), capability)
            || capability == WildlifeMigrationCapability.Unspecified)
        {
            errors.Add($"{owner} has unknown capability '{(int)capability}'.");
        }
        if (detectionRadiusCells < MinimumDetectionRadiusCells
            || detectionRadiusCells > MaximumDetectionRadiusCells)
        {
            errors.Add(
                $"{owner} detection radius must be "
                + $"{MinimumDetectionRadiusCells}..{MaximumDetectionRadiusCells} cells.");
        }
        if (!IsFinite(signalScore)
            || !IsFinite(minimumWaterRemainingRatio)
            || !IsFinite(minimumTemperatureC)
            || !IsFinite(maximumTemperatureC))
        {
            errors.Add($"{owner} numeric values must be finite.");
        }

        switch (capability)
        {
            case WildlifeMigrationCapability.GeneralHabitat:
            case WildlifeMigrationCapability.LoreOnly:
                RequireNeutralSignalFields(errors, owner);
                break;
            case WildlifeMigrationCapability.FoulWater:
                if (signalScore <= 0f)
                {
                    errors.Add($"{owner} requires a positive signal score.");
                }
                if (minimumWaterRemainingRatio <= 0f
                    || minimumWaterRemainingRatio > 1f)
                {
                    errors.Add(
                        $"{owner} water remaining ratio must be greater than 0 and at most 1.");
                }
                if (minimumCarcassQuantity != 0
                    || minimumTemperatureC != 0f
                    || maximumTemperatureC != 0f)
                {
                    errors.Add($"{owner} contains fields for a different capability.");
                }
                break;
            case WildlifeMigrationCapability.TemperatureBand:
                if (signalScore <= 0f)
                {
                    errors.Add($"{owner} requires a positive signal score.");
                }
                if (minimumTemperatureC > maximumTemperatureC)
                {
                    errors.Add($"{owner} temperature range must be ordered.");
                }
                if (minimumWaterRemainingRatio != 0f
                    || minimumCarcassQuantity != 0)
                {
                    errors.Add($"{owner} contains fields for a different capability.");
                }
                break;
            case WildlifeMigrationCapability.Carcass:
                if (signalScore <= 0f)
                {
                    errors.Add($"{owner} requires a positive signal score.");
                }
                if (minimumCarcassQuantity < 1)
                {
                    errors.Add($"{owner} requires at least one physical carcass.");
                }
                if (minimumWaterRemainingRatio != 0f
                    || minimumTemperatureC != 0f
                    || maximumTemperatureC != 0f)
                {
                    errors.Add($"{owner} contains fields for a different capability.");
                }
                break;
        }

        return errors;
    }

    public void RequireValid(string speciesId = "")
    {
        IReadOnlyList<string> errors = Validate(speciesId);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" | ", errors));
        }
    }

    private void RequireNeutralSignalFields(
        ICollection<string> errors,
        string owner)
    {
        if (signalScore != 0f
            || minimumWaterRemainingRatio != 0f
            || minimumCarcassQuantity != 0
            || minimumTemperatureC != 0f
            || maximumTemperatureC != 0f)
        {
            errors.Add($"{owner} general/lore capability must not author signal fields.");
        }
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
