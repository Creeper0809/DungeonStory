using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ClimateZone",
    menuName = "DungeonStory/World/Climate Zone")]
public sealed class ClimateZoneDefinitionSO : ScriptableObject
{
    public string stableId = string.Empty;
    public string displayName = string.Empty;
    public float meanTemperatureC = 14f;
    [Min(0f)] public float annualAmplitudeC = 14f;
    [Range(-6, 6)] public int localHourOffset;

    public ClimateZoneDefinition CreateRuntimeDefinition() => new(
        stableId,
        meanTemperatureC,
        annualAmplitudeC,
        localHourOffset);

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (!CreateRuntimeDefinition().IsValid) errors.Add("Climate zone id or amplitude is invalid.");
        if (string.IsNullOrWhiteSpace(displayName)) errors.Add("Climate zone display name is required.");
        return errors;
    }
}
