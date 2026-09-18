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
    private readonly IPhysicalItemMassQuery physicalMass;

    public CombatEquipmentStatProjector(
        IItemInstanceRepository itemInstances,
        IEvolutionModuleRegistry evolutionModules,
        IEquipmentModuleCatalog moduleCatalog,
        IPhysicalItemMassQuery physicalMass)
    {
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.evolutionModules = evolutionModules
            ?? throw new ArgumentNullException(nameof(evolutionModules));
        this.moduleCatalog = moduleCatalog
            ?? throw new ArgumentNullException(nameof(moduleCatalog));
        this.physicalMass = physicalMass
            ?? throw new ArgumentNullException(nameof(physicalMass));
    }

    public CombatEquipmentDerivedStats Build(
        CombatEquipmentDefinitionSO definition,
        CraftMaterialDefinitionSO material,
        CombatEquipmentInstance instance = null)
    {
        float durabilityMultiplier = material?.DurabilityMultiplier ?? 1f;
        string displayName = material == null
            ? definition?.DisplayName ?? string.Empty
            : $"{material.DisplayName} {definition.DisplayName}";
        return new CombatEquipmentDerivedStats(
            definition?.EquipmentId,
            material?.MaterialId,
            displayName,
            GetPhysicalMass(definition, instance).Value / 1000f,
            (definition?.MaxDurability ?? 1f) * durabilityMultiplier * GetEvolutionMultiplier(instance, "combat.durability"),
            (material?.DamageMultiplier ?? 1f) * (definition?.BaseStatMultiplier ?? 1f) * GetEvolutionMultiplier(instance, "combat.damage"),
            (material?.PenetrationDefenseMultiplier ?? 1f) * (definition?.BaseStatMultiplier ?? 1f) * GetEvolutionMultiplier(instance, "combat.defense"),
            (material?.ValueMultiplier ?? 1f) * GetEvolutionMultiplier(instance, "combat.value"),
            material?.Tint ?? Color.white);
    }

    public PhysicalMassGrams GetPhysicalMass(
        CombatEquipmentDefinitionSO definition,
        CombatEquipmentInstance instance = null)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (instance != null && !string.Equals(
                instance.definitionId, definition.EquipmentId, StringComparison.Ordinal))
            throw new InvalidOperationException("Equipment mass definition does not match its instance.");

        int count = 0;
        if (instance?.moduleSlots != null)
        {
            for (int i = 0; i < instance.moduleSlots.Count; i++)
            {
                EquipmentModuleSlotState slot = instance.moduleSlots[i];
                if (slot == null || string.IsNullOrEmpty(slot.moduleInstanceId)) continue;
                if (!itemInstances.EquipmentModules.TryGetValue(slot.moduleInstanceId, out EquipmentModuleInstance module)
                    || module.state != EquipmentModuleProcessState.Installed
                    || !string.Equals(module.attachedEquipmentInstanceId, instance.instanceId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Equipment mass has an invalid installed module: " + slot.moduleInstanceId);
                for (int j = 0; j < i; j++)
                    if (string.Equals(instance.moduleSlots[j]?.moduleInstanceId, slot.moduleInstanceId, StringComparison.Ordinal))
                        throw new InvalidOperationException("Equipment mass has a duplicate installed module: " + slot.moduleInstanceId);
                count++;
            }
        }
        return CombatEquipmentPhysicalItemMassProjector.ResolveUnitMass(
            physicalMass, (ItemDefinitionId)PhysicalItemIds.ForEquipment(definition.EquipmentId),
            count, instance?.loadedAmmunition);
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

            if (node.formulaVersion > 0)
            {
                if (node.presentationState != EquipmentEvolutionPresentationState.Ready
                    || !node.narrativeReady)
                    continue;
                EquipmentEvolutionFormulaCatalogSO catalog =
                    EquipmentEvolutionFormulaCatalogSO.LoadForPersistedNode(node.formulaVersion);
                EquipmentEvolutionRules.ValidateFormulaNode(node, catalog);
                foreach (EquipmentEvolutionFormulaCapabilityEnvelope envelope in
                         node.formulaCapabilities)
                {
                    ApplyFormulaCapability(
                        envelope,
                        catalog,
                        statId,
                        ref multiplier);
                    ApplyFormulaBurden(node, envelope, catalog, statId,
                        ref additive, ref multiplier);
                }
                if (!string.IsNullOrWhiteSpace(node.burdenEffectId)
                    && evolutionModules.TryGet(node.burdenEffectId,
                        out EvolutionModuleDefinition selectedDrawback)
                    && selectedDrawback.BurdenKind ==
                        EvolutionModuleBurdenKind.OptionalDrawback)
                {
                    ApplyEvolutionModifiers(
                        selectedDrawback.Burdens,
                        statId,
                        Mathf.Max(1f, node.burdenPotencyMultiplier),
                        ref additive,
                        ref multiplier);
                }
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
        evolution.formulaEvidence ??= new List<EquipmentEvolutionFormulaEvidenceRecord>();
        evolution.presentationRequests ??= new List<EquipmentEvolutionPresentationRequest>();
        foreach (EvolutionNode node in evolution.evolutionNodes
                     .Where(node => node != null))
        {
            if (node.formulaVersion > 0)
            {
                continue;
            }
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

    private static void ApplyFormulaCapability(
        EquipmentEvolutionFormulaCapabilityEnvelope envelope,
        EquipmentEvolutionFormulaCatalogSO catalog,
        string requestedStatId,
        ref float multiplier)
    {
        EquipmentEvolutionFormulaCapabilityDefinition definition =
            catalog.RequireCapability(envelope?.capabilityId);
        if (!string.Equals(definition.statId, requestedStatId, StringComparison.Ordinal))
            return;
        EquipmentEvolutionFormulaParameterEnvelope magnitude = envelope.parameters
            .Single(value => string.Equals(
                value.parameterId,
                NarrativeFormulaParameterIds.Magnitude,
                StringComparison.Ordinal));
        decimal value = definition.ToRuntime()
            .RequireRange(NarrativeFormulaParameterIds.Magnitude)
            .ToDecimal(magnitude.units);
        float scalar = (float)value;
        switch (envelope.applicatorId)
        {
            case "equipment:combat-damage:increase":
            case "equipment:combat-accuracy:increase":
            case "equipment:combat-durability:increase":
                if (definition.modifierKind != EquipmentEvolutionFormulaModifierKind.MultiplierIncrease)
                    throw new InvalidOperationException("Equipment increase applicator has the wrong modifier kind.");
                multiplier *= 1f + scalar;
                return;
            case "equipment:combat-reload:reduction":
                if (definition.modifierKind != EquipmentEvolutionFormulaModifierKind.MultiplierReduction)
                    throw new InvalidOperationException("Equipment reduction applicator has the wrong modifier kind.");
                multiplier *= Mathf.Max(0.05f, 1f - scalar);
                return;
            default:
                throw new InvalidOperationException(
                    $"Unknown equipment formula applicator '{envelope.applicatorId ?? string.Empty}'.");
        }
    }

    private void ApplyFormulaBurden(
        EvolutionNode node,
        EquipmentEvolutionFormulaCapabilityEnvelope envelope,
        EquipmentEvolutionFormulaCatalogSO catalog,
        string requestedStatId,
        ref float additive,
        ref float multiplier)
    {
        if (!evolutionModules.TryGet(envelope?.capabilityId,
                out EvolutionModuleDefinition module))
            throw new InvalidOperationException(
                $"Equipment formula capability '{envelope?.capabilityId ?? string.Empty}' is not registered.");
        if (node.formulaVersion >= EquipmentEvolutionRules.DrawbackModuleSelectionFormulaVersion)
        {
            if (module.BurdenKind == EvolutionModuleBurdenKind.None)
                return;
            if (module.BurdenKind == EvolutionModuleBurdenKind.OptionalDrawback)
                throw new InvalidOperationException(
                    "An optional equipment drawback cannot be applied as a positive capability.");
        }
        if (module.Burdens.Count == 0)
            throw new InvalidOperationException(
                $"Equipment formula capability '{envelope?.capabilityId ?? string.Empty}' has no registered burden.");
        EquipmentEvolutionFormulaCapabilityDefinition definition =
            catalog.RequireCapability(envelope.capabilityId);
        EquipmentEvolutionFormulaParameterEnvelope magnitude = envelope.parameters
            .Single(value => string.Equals(value.parameterId,
                NarrativeFormulaParameterIds.Magnitude, StringComparison.Ordinal));
        float magnitudeValue = (float)definition.ToRuntime()
            .RequireRange(NarrativeFormulaParameterIds.Magnitude)
            .ToDecimal(magnitude.units);
        float authoredBenefitMagnitude = module.Benefits.Sum(value =>
            Mathf.Abs(value.additive) + Mathf.Abs(value.multiplier - 1f));
        if (authoredBenefitMagnitude <= 0f)
            throw new InvalidOperationException(
                $"Equipment formula capability '{envelope.capabilityId}' has no measurable paired benefit.");
        float potency = magnitudeValue / authoredBenefitMagnitude;
        ApplyEvolutionModifiers(module.Burdens, requestedStatId, potency,
            ref additive, ref multiplier);
    }
}
