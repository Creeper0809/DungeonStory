using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class CombatEquipmentInstanceFactory
{
    private readonly ICombatEquipmentCatalog catalog;
    private readonly CombatEquipmentCraftDefinitionRules definitionRules;
    private readonly IItemInstanceRepository itemInstances;

    public CombatEquipmentInstanceFactory(
        ICombatEquipmentCatalog catalog,
        CombatEquipmentCraftDefinitionRules definitionRules,
        IItemInstanceRepository itemInstances)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.definitionRules = definitionRules
            ?? throw new ArgumentNullException(nameof(definitionRules));
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
    }

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState,
        string materialId)
    {
        return CreateInstance(
            definitionId,
            quality,
            worldState,
            materialId,
            null);
    }

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState,
        string materialId,
        MythicProvenanceSaveData mythicProvenance)
    {
        CombatEquipmentInstance instance = BuildPreparedInstance(
            definitionId,
            quality,
            worldState,
            materialId,
            mythicProvenance,
            itemInstances.AllocateItemInstanceId().Value);
        itemInstances.EquipmentInstances.Add(instance.instanceId, instance);
        return instance.Clone();
    }

    public CombatEquipmentInstance BuildPreparedInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState,
        string materialId,
        MythicProvenanceSaveData mythicProvenance,
        string instanceId)
    {
        if (!catalog.TryGet(definitionId, out CombatEquipmentDefinitionSO definition))
        {
            throw new KeyNotFoundException(
                $"Unknown combat equipment definition '{definitionId}'.");
        }
        if (!definitionRules.IsDefinitionUnlocked(definitionId, out string lockedReason))
        {
            throw new InvalidOperationException(lockedReason);
        }
        if (!definitionRules.TryResolveMaterial(
                definition,
                materialId,
                out CraftMaterialDefinitionSO material,
                out string failureReason))
        {
            throw new ArgumentException(failureReason, nameof(materialId));
        }

        CombatEquipmentInstance instance = new CombatEquipmentInstance
        {
            instanceId = instanceId,
            definitionId = definition.EquipmentId,
            materialId = material?.MaterialId
                ?? CombatEquipmentCraftDefinitionRules.ResolveRequestedMaterialId(
                    definition,
                    materialId),
            quality = quality,
            mythicProvenance = mythicProvenance?.Clone(),
            durabilityRatio = 1f,
            powerCharge = 100f,
            loadedAmmunition = new LoadedAmmunitionBatch(),
            worldState = worldState,
            moduleSlots = Enumerable.Range(0, definition.ModuleSlotCount)
                .Select(index => new EquipmentModuleSlotState { slotIndex = index })
                .ToList()
        };
        if (quality == CombatEquipmentQuality.Mythic
            && (instance.mythicProvenance == null
                || instance.mythicProvenance.sourceTraitId
                    != MythicCraftInspirationRules.SourceTraitId
                || string.IsNullOrWhiteSpace(
                    instance.mythicProvenance.makerCharacterId)))
            throw new InvalidOperationException(
                "Mythic equipment requires trait-300 provenance.");
        if (quality != CombatEquipmentQuality.Mythic
            && instance.mythicProvenance != null)
            throw new InvalidOperationException(
                "Non-Mythic equipment cannot carry Mythic provenance.");
        return instance;
    }

    public CombatEquipmentInstance CreateExternalInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        string materialId)
    {
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition))
        {
            throw new KeyNotFoundException(
                $"Unknown external combat equipment definition '{definitionId}'.");
        }
        if (!definitionRules.TryResolveMaterial(
                definition,
                materialId,
                out CraftMaterialDefinitionSO material,
                out string failureReason))
        {
            throw new ArgumentException(failureReason, nameof(materialId));
        }

        CombatEquipmentInstance instance = new CombatEquipmentInstance
        {
            instanceId = itemInstances.AllocateItemInstanceId().Value,
            definitionId = definition.EquipmentId,
            materialId = material?.MaterialId
                ?? CombatEquipmentCraftDefinitionRules.ResolveRequestedMaterialId(
                    definition,
                    materialId),
            quality = quality,
            durabilityRatio = 1f,
            powerCharge = 100f,
            loadedAmmunition = new LoadedAmmunitionBatch(),
            worldState = CombatEquipmentWorldState.Equipped,
            moduleSlots = Enumerable.Range(0, definition.ModuleSlotCount)
                .Select(index => new EquipmentModuleSlotState { slotIndex = index })
                .ToList()
        };
        itemInstances.EquipmentInstances.Add(instance.instanceId, instance);
        return instance.Clone();
    }
}
