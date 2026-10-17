using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeHaulRoleProfile
{
    public const float MaximumDecisionIntervalSeconds = 0.5f;

    [SerializeField, Min(1)] private long maxCargoMassGrams;
    [SerializeField, Range(0.01f, 1f)] private float loadedMoveSpeedMultiplier;
    [SerializeField, Min(0.01f)] private float decisionIntervalSeconds;

    public WildlifeHaulRoleProfile(
        long maxCargoMassGrams,
        float loadedMoveSpeedMultiplier,
        float decisionIntervalSeconds)
    {
        this.maxCargoMassGrams = maxCargoMassGrams;
        this.loadedMoveSpeedMultiplier = loadedMoveSpeedMultiplier;
        this.decisionIntervalSeconds = decisionIntervalSeconds;
        RequireValid();
    }

    public long MaxCargoMassGrams => maxCargoMassGrams;
    public float LoadedMoveSpeedMultiplier => loadedMoveSpeedMultiplier;
    public float DecisionIntervalSeconds => decisionIntervalSeconds;

    public WildlifeHaulRoleProfile Snapshot()
    {
        RequireValid();
        return new WildlifeHaulRoleProfile(
            maxCargoMassGrams,
            loadedMoveSpeedMultiplier,
            decisionIntervalSeconds);
    }

    public IReadOnlyList<string> Validate(string speciesId = "")
    {
        List<string> errors = new();
        string owner = string.IsNullOrWhiteSpace(speciesId)
            ? "Wildlife haul role profile"
            : $"Wildlife species '{speciesId.Trim()}' haul role profile";
        if (maxCargoMassGrams < 1)
        {
            errors.Add($"{owner} maximum cargo mass must be positive grams.");
        }
        if (!IsFinite(loadedMoveSpeedMultiplier)
            || loadedMoveSpeedMultiplier <= 0f
            || loadedMoveSpeedMultiplier > 1f)
        {
            errors.Add($"{owner} loaded move-speed multiplier must be finite and in (0, 1].");
        }
        if (!IsFinite(decisionIntervalSeconds)
            || decisionIntervalSeconds <= 0f
            || decisionIntervalSeconds > MaximumDecisionIntervalSeconds)
        {
            errors.Add(
                $"{owner} decision interval must be finite, positive, and at most "
                + $"{MaximumDecisionIntervalSeconds:0.###} game seconds.");
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

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
