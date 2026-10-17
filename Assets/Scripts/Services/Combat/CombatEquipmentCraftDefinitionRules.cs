using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class CombatEquipmentCraftDefinitionRules
{
    private readonly ICombatEquipmentCatalog catalog;
    private readonly IResourceEconomyContentCatalog materialCatalog;
    private readonly BlueprintResearchRuntime research;

    public CombatEquipmentCraftDefinitionRules(
        ICombatEquipmentCatalog catalog,
        IResourceEconomyContentCatalog materialCatalog,
        BlueprintResearchRuntime research)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.materialCatalog = materialCatalog
            ?? throw new ArgumentNullException(nameof(materialCatalog));
        this.research = research ?? throw new ArgumentNullException(nameof(research));
    }

    public bool IsDefinitionUnlocked(
        string definitionId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        if (string.IsNullOrWhiteSpace(definition.RequiredResearchId))
        {
            return true;
        }
        if (research.State.Projects.IsCompleted(
                new ResearchProjectId(definition.RequiredResearchId)))
        {
            return true;
        }
        failureReason = $"equipment.research.required:{definition.RequiredResearchId}";
        return false;
    }

    public IReadOnlyList<CraftMaterialDefinitionSO> GetAllowedMaterials(
        string definitionId)
    {
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition))
        {
            return Array.Empty<CraftMaterialDefinitionSO>();
        }

        return materialCatalog.Materials
            .Where(definition.AllowsMaterial)
            .OrderBy(material => material.RareMaterial ? 1 : 0)
            .ThenBy(material => material.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public CraftMaterialDefinitionSO ResolveInstanceMaterial(
        CombatEquipmentInstance instance,
        CombatEquipmentDefinitionSO definition)
    {
        if (materialCatalog.Materials.Count == 0 || definition == null)
        {
            return null;
        }
        string materialId = ResolveRequestedMaterialId(definition, instance?.materialId);
        return materialCatalog.TryGetMaterial(materialId, out CraftMaterialDefinitionSO material)
            && definition.AllowsMaterial(material)
                ? material
                : null;
    }

    public string NormalizeRestoredMaterialId(
        CombatEquipmentDefinitionSO definition,
        string materialId)
    {
        string normalized = ResolveRequestedMaterialId(definition, materialId);
        if (materialCatalog.Materials.Count == 0)
        {
            return normalized;
        }
        return materialCatalog.TryGetMaterial(normalized, out CraftMaterialDefinitionSO material)
            && definition.AllowsMaterial(material)
                ? material.MaterialId
                : definition.DefaultMaterialId;
    }

    public bool TryResolveMaterial(
        CombatEquipmentDefinitionSO definition,
        string requestedMaterialId,
        out CraftMaterialDefinitionSO material,
        out string failureReason)
    {
        material = null;
        failureReason = string.Empty;
        if (definition == null)
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        string normalizedId = ResolveRequestedMaterialId(
            definition,
            requestedMaterialId);
        if (materialCatalog.Materials.Count == 0)
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(normalizedId)
            || !materialCatalog.TryGetMaterial(normalizedId, out material))
        {
            failureReason = "equipment.material.unknown";
            return false;
        }
        if (!definition.AllowsMaterial(material))
        {
            failureReason = "equipment.material.not_allowed";
            material = null;
            return false;
        }
        return true;
    }

    public CombatEquipmentCraftMaterialPolicySaveData NormalizeMaterialPolicy(
        CombatEquipmentCraftMaterialPolicySaveData source)
    {
        CombatEquipmentCraftMaterialPolicySaveData clone =
            source?.Clone() ?? new CombatEquipmentCraftMaterialPolicySaveData();
        if (!catalog.TryGet(clone.definitionId, out CombatEquipmentDefinitionSO definition))
        {
            return new CombatEquipmentCraftMaterialPolicySaveData();
        }
        Dictionary<string, CraftMaterialDefinitionSO> allowedById =
            GetAllowedMaterials(definition.EquipmentId)
                .ToDictionary(
                    material => material.MaterialId,
                    material => material,
                    StringComparer.Ordinal);
        List<string> priority = clone.priorityMaterialIds
            .Where(allowedById.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (CraftMaterialDefinitionSO material in allowedById.Values
                     .OrderBy(candidate => string.Equals(
                             candidate.MaterialId,
                             definition.DefaultMaterialId,
                             StringComparison.Ordinal)
                         ? 0
                         : 1)
                     .ThenBy(candidate => candidate.RareMaterial ? 1 : 0)
                     .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal))
        {
            if (!priority.Contains(material.MaterialId, StringComparer.Ordinal))
            {
                priority.Add(material.MaterialId);
            }
        }
        return new CombatEquipmentCraftMaterialPolicySaveData
        {
            facilityKey = clone.facilityKey,
            definitionId = definition.EquipmentId,
            priorityMaterialIds = priority,
            allowedMaterialIds = clone.allowedMaterialIds
                .Where(allowedById.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            commandRevision = clone.commandRevision,
            commandReceipts = clone.commandReceipts
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList()
        };
    }

    public static string ResolveRequestedMaterialId(
        CombatEquipmentDefinitionSO definition,
        string requestedMaterialId)
    {
        return string.IsNullOrWhiteSpace(requestedMaterialId)
            ? definition?.DefaultMaterialId ?? string.Empty
            : requestedMaterialId.Trim();
    }
}
