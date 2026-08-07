using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WeatherFront",
    menuName = "DungeonStory/World/Weather Front")]
public sealed class WeatherFrontDefinitionSO : ScriptableObject
{
    public string stableId = string.Empty;
    public string displayName = string.Empty;
    public WeatherFrontKind kind;
    [Min(1)] public int minimumDurationDays = 1;
    [Min(1)] public int maximumDurationDays = 1;
    public float temperatureModifierC;
    [Min(0f)] public float springWeight;
    [Min(0f)] public float summerWeight;
    [Min(0f)] public float autumnWeight;
    [Min(0f)] public float winterWeight;

    public WeatherFrontDefinition CreateRuntimeDefinition() => new(
        stableId,
        kind,
        minimumDurationDays,
        maximumDurationDays,
        temperatureModifierC,
        new[] { springWeight, summerWeight, autumnWeight, winterWeight });

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (!CreateRuntimeDefinition().IsValid) errors.Add("Weather-front content is invalid.");
        if (string.IsNullOrWhiteSpace(displayName)) errors.Add("Weather-front display name is required.");
        if (maximumDurationDays < minimumDurationDays) errors.Add("Maximum duration precedes minimum duration.");
        return errors;
    }
}
