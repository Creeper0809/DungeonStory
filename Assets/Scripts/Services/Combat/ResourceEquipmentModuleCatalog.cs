using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ResourceEquipmentModuleCatalog : IEquipmentModuleCatalog
{
    private readonly Dictionary<string, EquipmentModuleDefinitionSO> byId;
    private readonly IReadOnlyList<EquipmentModuleDefinitionSO> all;

    public ResourceEquipmentModuleCatalog(IGameContentCatalog content)
    {
        byId = (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<EquipmentModuleDefinitionSO>()
            .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.ModuleId))
            .GroupBy(definition => definition.ModuleId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        all = byId.Values.OrderBy(definition => definition.LineageKind)
            .ThenBy(definition => definition.ModuleId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<EquipmentModuleDefinitionSO> All => all;
    public bool TryGet(string moduleId, out EquipmentModuleDefinitionSO definition) =>
        byId.TryGetValue(moduleId?.Trim() ?? string.Empty, out definition);
}
