using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    fileName = "ItemDefinitionCatalog",
    menuName = "DungeonStory/Content/Item Definition Catalog",
    order = 0)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ItemDefinitionCatalogSO : ScriptableObject
{
    [SerializeField] private List<ItemDefinitionSO> definitions = new();

    public IReadOnlyList<ItemDefinitionSO> Definitions => definitions;

    public IReadOnlyList<string> ValidateCatalog()
    {
        List<string> errors = new();
        foreach (ItemDefinitionSO definition in definitions ?? new List<ItemDefinitionSO>())
        {
            if (definition == null)
            {
                errors.Add("Item definition catalog contains a null entry.");
                continue;
            }

            errors.AddRange(definition.ValidateDefinition()
                .Select(error => $"{definition.name}: {error}"));
        }

        foreach (IGrouping<string, ItemDefinitionSO> duplicate in
                 (definitions ?? new List<ItemDefinitionSO>())
                 .Where(definition => definition != null)
                 .GroupBy(definition => definition.ItemId, StringComparer.Ordinal)
                 .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
        {
            errors.Add(string.IsNullOrWhiteSpace(duplicate.Key)
                ? "Item definition catalog contains an empty stable ID."
                : $"Duplicate item definition ID '{duplicate.Key}'.");
        }

        ItemDefinitionSO[] itemDefinitions =
            (definitions ?? new List<ItemDefinitionSO>())
            .Where(definition => definition != null)
            .ToArray();
        ResourceItemDefinitionSO[] resourceItems = itemDefinitions
            .OfType<ResourceItemDefinitionSO>()
            .ToArray();
        foreach (ResourceItemDefinitionSO item in resourceItems)
        {
            bool hasSubstance = item.TryGetFeature(out SubstanceItemFeature _);
            if (item.Kind == ResourceItemKind.Substance && !hasSubstance)
            {
                errors.Add(
                    $"{item.name}: substance item '{item.ItemId}' has no substance feature.");
            }
        }

        foreach (IGrouping<string, ItemDefinitionSO> duplicate in itemDefinitions
                     .Where(item => item.TryGetFeature(out SubstanceItemFeature _))
                     .GroupBy(
                         item => item.GetFeatureOrDefault<SubstanceItemFeature>()
                             ?.substanceId?.Trim() ?? string.Empty,
                         StringComparer.Ordinal)
                     .Where(group => string.IsNullOrWhiteSpace(group.Key)
                         || group.Count() > 1))
        {
            errors.Add(string.IsNullOrWhiteSpace(duplicate.Key)
                ? "Item definition catalog contains an empty substance ID."
                : $"Duplicate substance ID '{duplicate.Key}' in item definitions.");
        }

        return errors;
    }

#if UNITY_EDITOR
    public void SetDefinitions(IEnumerable<ItemDefinitionSO> source)
    {
        definitions = (source ?? Array.Empty<ItemDefinitionSO>())
            .Where(definition => definition != null)
            .Distinct()
            .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
            .ToList();
    }
#endif
}
