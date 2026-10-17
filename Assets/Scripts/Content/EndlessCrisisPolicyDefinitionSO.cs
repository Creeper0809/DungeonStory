using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EndlessCrisisAxis
{
    None = 0,
    Climate = 1,
    Faction = 2,
    Disease = 3,
    Logistics = 4,
    Combat = 5
}

[Serializable]
public sealed class EndlessCrisisAxisPolicy
{
    public EndlessCrisisAxis axis;
    public string displayName = string.Empty;
    public float singleAxisMultiplier = 1f;
    public float compoundAxisMultiplier = 1f;

    public bool IsValid
    {
        get
        {
            if (!Enum.IsDefined(typeof(EndlessCrisisAxis), axis)
                || axis == EndlessCrisisAxis.None
                || string.IsNullOrWhiteSpace(displayName)
                || !string.Equals(
                    displayName,
                    displayName.Trim(),
                    StringComparison.Ordinal)
                || !FinitePositive(singleAxisMultiplier)
                || !FinitePositive(compoundAxisMultiplier))
            {
                return false;
            }

            return IsEfficiencyAxis(axis)
                ? singleAxisMultiplier < compoundAxisMultiplier
                    && compoundAxisMultiplier < 1f
                : singleAxisMultiplier > compoundAxisMultiplier
                    && compoundAxisMultiplier > 1f;
        }
    }

    public static bool IsEfficiencyAxis(EndlessCrisisAxis value) => value is
        EndlessCrisisAxis.Climate
        or EndlessCrisisAxis.Faction
        or EndlessCrisisAxis.Logistics;

    private static bool FinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}

[CreateAssetMenu(
    fileName = "EndlessCrisisPolicy",
    menuName = "DungeonStory/V20/Endless Crisis Policy")]
public sealed class EndlessCrisisPolicyDefinitionSO : ScriptableObject
{
    public const string RequiredStableId =
        "policy:endless-crisis:owned-pressure-recovery";

    public string stableId = RequiredStableId;
    [Range(0, 100)] public int singleAxisProbabilityPercent = 70;
    [Min(1)] public int pressureDurationDays = 3;
    [Min(5)] public int singleAxisRecoveryDays = 7;
    [Min(5)] public int compoundAxisRecoveryDays = 8;
    public List<EndlessCrisisAxisPolicy> axes = new();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (!string.Equals(stableId, RequiredStableId, StringComparison.Ordinal))
            errors.Add("Endless crisis policy has an invalid stable ID.");
        if (singleAxisProbabilityPercent != 70)
            errors.Add("Endless crisis policy must author a 70 percent single-axis probability.");
        if (pressureDurationDays < 1
            || singleAxisRecoveryDays < 5
            || compoundAxisRecoveryDays < singleAxisRecoveryDays)
            errors.Add("Endless crisis policy requires positive pressure, at least five recovery days, and compound recovery no shorter than single-axis recovery.");

        EndlessCrisisAxis[] expected = Enum
            .GetValues(typeof(EndlessCrisisAxis))
            .Cast<EndlessCrisisAxis>()
            .Where(value => value != EndlessCrisisAxis.None)
            .ToArray();
        if (axes == null
            || axes.Count != expected.Length
            || axes.Any(value => value == null || !value.IsValid)
            || axes.Select(value => value.axis).Distinct().Count()
                != expected.Length
            || expected.Any(axis => axes.All(value => value.axis != axis)))
        {
            errors.Add("Endless crisis policy requires one valid entry for each of its five axes.");
        }
        return errors;
    }

    public EndlessCrisisAxisPolicy Require(EndlessCrisisAxis axis)
    {
        if (ValidateDefinition().Count > 0)
            throw new InvalidOperationException(
                "Endless crisis policy is invalid: "
                + string.Join(" | ", ValidateDefinition()));
        return axes.Single(value => value.axis == axis);
    }
}
