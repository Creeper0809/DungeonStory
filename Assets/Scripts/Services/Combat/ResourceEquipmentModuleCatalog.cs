using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ResourceEquipmentModuleCatalog : IEquipmentModuleCatalog
{
    private readonly Dictionary<string, EquipmentModuleDefinitionSO> byId;
    private readonly IReadOnlyList<EquipmentModuleDefinitionSO> all;

    public ResourceEquipmentModuleCatalog(IGameContentCatalog content)
        : this((content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<EquipmentModuleDefinitionSO>())
    {
    }

    public ResourceEquipmentModuleCatalog(
        IEnumerable<EquipmentModuleDefinitionSO> source)
    {
        EquipmentModuleDefinitionSO[] definitions = (source
                ?? Array.Empty<EquipmentModuleDefinitionSO>())
            .Where(definition => definition != null)
            .ToArray();
        EquipmentModuleDefinitionSO invalid = definitions.FirstOrDefault(
            definition => string.IsNullOrWhiteSpace(definition.ModuleId));
        if (invalid != null)
        {
            throw new InvalidOperationException(
                $"Equipment module '{invalid.name}' has no stable module ID.");
        }

        IGrouping<string, EquipmentModuleDefinitionSO> duplicate = definitions
            .GroupBy(definition => definition.ModuleId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException(
                $"Duplicate equipment module ID '{duplicate.Key}'.");
        }

        Dictionary<string, EquipmentModuleDefinitionSO> index = definitions
            .ToDictionary(
                definition => definition.ModuleId,
                definition => definition,
                StringComparer.Ordinal);
        EquipmentModuleDefinitionSO[] ordered = definitions
            .OrderBy(definition => definition.LineageKind)
            .ThenBy(definition => definition.ModuleId, StringComparer.Ordinal)
            .ToArray();
        byId = index;
        all = ordered;
    }

    public IReadOnlyList<EquipmentModuleDefinitionSO> All => all;
    public bool TryGet(string moduleId, out EquipmentModuleDefinitionSO definition) =>
        byId.TryGetValue(moduleId?.Trim() ?? string.Empty, out definition);
}
