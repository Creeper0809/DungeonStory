using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public sealed class CultureRoomPreferenceDefinition
{
    public FacilityRole preferredRoles = FacilityRole.None;
    public float idealTemperatureC = 20f;
    [Min(1f)] public float temperatureToleranceC = 8f;
    [Range(0f, 100f)] public float minimumVentilation = 35f;
    [Range(0f, 100f)] public float minimumLighting = 15f;
    [Range(0f, 100f)] public float maximumLighting = 90f;
    [Range(0f, 100f)] public float minimumCleanliness = 35f;
    public bool prefersSharedSpace;
    public bool prefersPrivateSpace;

    public IEnumerable<string> Validate(string ownerId)
    {
        if (temperatureToleranceC < 1f)
            yield return $"'{ownerId}' culture temperature tolerance must be positive.";
        if (minimumLighting > maximumLighting)
            yield return $"'{ownerId}' culture lighting range is inverted.";
        if (prefersSharedSpace && prefersPrivateSpace)
            yield return $"'{ownerId}' cannot prefer shared and private rooms together.";
    }
}

[CreateAssetMenu(fileName = "SpeciesCulture", menuName = "DungeonStory/V20/Species Culture")]
public sealed class SpeciesCultureDefinitionSO : V20AuthoredContentSO
{
    public string defaultSpeciesId = string.Empty;
    public List<string> preferredItemIds = new();
    public List<string> forbiddenItemIds = new();
    public List<string> preferredFacilityIds = new();
    public List<string> environmentalPreferences = new();
    public CultureRoomPreferenceDefinition roomPreference = new();
    public List<string> etiquetteRules = new();
    public List<string> ceremonyIds = new();
    public List<V20WeightedId> otherCultureAttitudes = new();
    [Min(1)] public int assimilationDays = 120;

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (string.IsNullOrWhiteSpace(defaultSpeciesId)) errors.Add($"'{StableId}' requires a default species id.");
        if (assimilationDays != 120) errors.Add($"'{StableId}' assimilation must take exactly 120 days.");
        if (etiquetteRules == null || etiquetteRules.Count == 0) errors.Add($"'{StableId}' requires authored etiquette.");
        errors.AddRange((roomPreference ?? new CultureRoomPreferenceDefinition()).Validate(StableId));
        return errors;
    }
}
