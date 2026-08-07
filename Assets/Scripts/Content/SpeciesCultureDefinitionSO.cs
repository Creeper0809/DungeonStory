using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SpeciesCulture", menuName = "DungeonStory/V20/Species Culture")]
public sealed class SpeciesCultureDefinitionSO : V20AuthoredContentSO
{
    public string defaultSpeciesId = string.Empty;
    public List<string> preferredItemIds = new();
    public List<string> forbiddenItemIds = new();
    public List<string> preferredFacilityIds = new();
    public List<string> environmentalPreferences = new();
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
        return errors;
    }
}
