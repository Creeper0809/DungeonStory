using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Read-only material, evolution, and installed-module stat projection.
/// </summary>
public sealed class CombatEquipmentStatProjector
{
    private readonly IItemInstanceRepository itemInstances;
    private readonly IEvolutionModuleRegistry evolutionModules;
    private readonly IEquipmentModuleCatalog moduleCatalog;

    public CombatEquipmentStatProjector(
        IItemInstanceRepository itemInstances,
        IEvolutionModuleRegistry evolutionModules,
        IEquipmentModuleCatalog moduleCatalog)
    {
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.evolutionModules = evolutionModules
            ?? throw new ArgumentNullException(nameof(evolutionModules));
        this.moduleCatalog = moduleCatalog
            ?? throw new ArgumentNullException(nameof(moduleCatalog));
    }

    public CombatEquipmentDerivedStats Build(
        CombatEquipmentDefinitionSO definition,
        CraftMaterialDefinitionSO material,
        CombatEquipmentInstance instance = null)
    {
        float weightMultiplier = material?.WeightMultiplier ?? 1f;
        float durabilityMultiplier = material?.DurabilityMultiplier ?? 1f;
        string displayName = material == null
            ? definition?.DisplayName ?? string.Empty
            : $"{material.DisplayName} {definition.DisplayName}";
        return new CombatEquipmentDerivedStats(
            definition?.EquipmentId,
            material?.MaterialId,
            displayName,
            (definition?.Weight ?? 0f) * weightMultiplier * GetEvolutionMultiplier(instance, "combat.weight"),
            (definition?.MaxDurability ?? 1f) * durabilityMultiplier * GetEvolutionMultiplier(instance, "combat.durability"),
            (material?.DamageMultiplier ?? 1f) * (definition?.BaseStatMultiplier ?? 1f) * GetEvolutionMultiplier(instance, "combat.damage"),
            (material?.PenetrationDefenseMultiplier ?? 1f) * (definition?.BaseStatMultiplier ?? 1f) * GetEvolutionMultiplier(instance, "combat.defense"),
            (material?.ValueMultiplier ?? 1f) * GetEvolutionMultiplier(instance, "combat.value"),
            material?.Tint ?? Color.white);
    }

    public float GetEvolutionMultiplier(CombatEquipmentInstance instance, string statId)
    {
        if (instance?.evolution == null || string.IsNullOrWhiteSpace(statId))
        {
            return 1f;
        }

        HashSet<string> activeHistory = new HashSet<string>(
            instance.evolution.activeHistoricalNodeIds ?? new List<string>(),
            StringComparer.Ordinal);
        float additive = 0f;
        float multiplier = 1f;
        foreach (EvolutionNode node in instance.evolution.evolutionNodes
                     ?? new List<EvolutionNode>())
        {
            if (node == null
                || !node.active
                || !node.mechanicallyUnlocked
                || node.historical && !activeHistory.Contains(node.nodeId))
            {
                continue;
            }

            float potency = Mathf.Max(0.01f, node.potencyMultiplier);
            if (evolutionModules.TryGet(node.effectId, out EvolutionModuleDefinition module))
            {
                ApplyEvolutionModifiers(module.Benefits, statId, potency, ref additive, ref multiplier);
                ApplyEvolutionModifiers(module.Burdens, statId, potency, ref additive, ref multiplier);
            }

            if (!string.IsNullOrWhiteSpace(node.burdenEffectId)
                && !string.Equals(node.burdenEffectId, node.effectId, StringComparison.Ordinal)
                && evolutionModules.TryGet(node.burdenEffectId, out EvolutionModuleDefinition burdenModule))
            {
                ApplyEvolutionModifiers(
                    burdenModule.Burdens,
                    statId,
                    potency,
                    ref additive,
                    ref multiplier);
            }
        }

        return Mathf.Max(0.05f, multiplier + additive);
    }

    public float GetInstalledModuleMultiplier(CombatEquipmentInstance equipmentInstance, bool power)
    {
        if (equipmentInstance?.moduleSlots == null)
        {
            return 1f;
        }

        IDictionary<string, EquipmentModuleInstance> modules = itemInstances.EquipmentModules;
        float additive = 0f;
        foreach (EquipmentModuleSlotState slot in equipmentInstance.moduleSlots)
        {
            if (slot == null
                || !modules.TryGetValue(slot.moduleInstanceId, out EquipmentModuleInstance module)
                || module.state != EquipmentModuleProcessState.Installed
                || !moduleCatalog.TryGet(module.definitionId, out EquipmentModuleDefinitionSO definition))
            {
                continue;
            }
            additive += (power ? definition.PowerPerGrade : definition.UtilityPerGrade)
                * Mathf.Clamp(module.grade, 1, 4)
                * Mathf.Clamp01(module.condition);
        }
        return Mathf.Max(0.1f, 1f + additive);
    }

    public static void NormalizeEvolutionPresentationState(EquipmentEvolutionState evolution)
    {
        if (evolution == null)
        {
            return;
        }

        evolution.evolutionNodes ??= new List<EvolutionNode>();
        evolution.narrativeRequests ??= new List<EvolutionNarrativeRequestSnapshot>();
        foreach (EvolutionNode node in evolution.evolutionNodes
                     .Where(node => node != null))
        {
            if (!string.IsNullOrWhiteSpace(node.effectId))
            {
                node.mechanicallyUnlocked = true;
            }
            if (!node.historical)
            {
                node.narrativeReady = true;
                node.uiVisible = true;
            }
            else if (node.playerVisible)
            {
                node.uiVisible = true;
            }
            node.playerVisible = node.uiVisible;
        }
    }

    private static void ApplyEvolutionModifiers(
        IReadOnlyList<EvolutionEffectModifier> modifiers,
        string statId,
        float potency,
        ref float additive,
        ref float multiplier)
    {
        foreach (EvolutionEffectModifier modifier in modifiers
                     ?? Array.Empty<EvolutionEffectModifier>())
        {
            if (modifier != null
                && string.Equals(modifier.statId, statId, StringComparison.Ordinal))
            {
                additive += modifier.additive * potency;
                multiplier *= Mathf.Max(0f, 1f + (modifier.multiplier - 1f) * potency);
            }
        }
    }
}
