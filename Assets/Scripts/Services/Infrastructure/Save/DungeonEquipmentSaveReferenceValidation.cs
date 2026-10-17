using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DungeonSaveReferenceRules;

/// <summary>Validates detached equipment references and evolution joins.</summary>
internal sealed class DungeonEquipmentSaveReferenceValidation
{
    private readonly ICombatEquipmentCatalog combatEquipmentDefinitions;

    internal DungeonEquipmentSaveReferenceValidation(ICombatEquipmentCatalog combatEquipmentDefinitions)
    {
        this.combatEquipmentDefinitions = combatEquipmentDefinitions ?? throw new ArgumentNullException(nameof(combatEquipmentDefinitions));
    }

    internal void ValidateCombat(
        DungeonCombatEquipmentSaveData source,
        ISet<string> characterIds,
        DungeonSavePhysicalReferenceIndex physical,
        ISet<string> buildingIds,
        DungeonResearchSaveData research,
        DungeonGameRestoreReport report)
    {
        foreach (CharacterCombatLoadoutState loadout in source?.loadouts
                     ?? new List<CharacterCombatLoadoutState>())
        {
            string characterId = loadout?.characterId ?? string.Empty;
            if (loadout == null || !characterIds.Contains(characterId))
            {
                report.AddError(
                    $"Combat loadout references missing character '{characterId}'.");
                continue;
            }

            foreach (CharacterCombatLoadoutProfile profile in loadout.profiles
                         ?? new List<CharacterCombatLoadoutProfile>())
            {
                if (profile == null)
                {
                    continue;
                }
                foreach (string equipmentId in profile.weaponInstanceIds
                             ?? new List<string>())
                {
                    RequireEquipmentReference<CombatWeaponSO>(
                        equipmentId,
                        physical,
                        $"Combat loadout '{characterId}' weapon",
                        report);
                }
                foreach (string equipmentId in profile.armorInstanceIds
                             ?? new List<string>())
                {
                    RequireEquipmentReference<CombatArmorSO>(
                        equipmentId,
                        physical,
                        $"Combat loadout '{characterId}' armor",
                        report);
                }
                if (!string.IsNullOrEmpty(profile.shieldInstanceId))
                {
                    RequireEquipmentReference<CombatShieldSO>(
                        profile.shieldInstanceId,
                        physical,
                        $"Combat loadout '{characterId}' shield",
                        report);
                }
            }
        }

        foreach (CombatEquipmentCraftMaterialPolicySaveData policy in
                 source?.craftMaterialPolicies
                 ?? new List<CombatEquipmentCraftMaterialPolicySaveData>())
        {
            if (policy != null)
            {
                RequireReference(
                    policy.facilityKey,
                    buildingIds,
                    "Combat material-policy facility",
                    report);
            }
        }

        HashSet<string> completedResearch = new HashSet<string>(
            research?.completedProjectIds ?? new List<string>(),
            StringComparer.Ordinal);
        foreach (CombatEquipmentCraftOrderSaveData order in source?.craftOrders
                     ?? new List<CombatEquipmentCraftOrderSaveData>())
        {
            if (order == null
                || CombatEquipmentCraftingRuntime.IsAmmunitionRecipe(
                    order.definitionId))
            {
                continue;
            }
            if (!combatEquipmentDefinitions.TryGet(
                    order.definitionId,
                    out CombatEquipmentDefinitionSO definition))
            {
                report.AddError(
                    $"Combat craft order '{order.orderId}' references unknown equipment definition '{order.definitionId}'.");
                continue;
            }
            if (!string.IsNullOrEmpty(definition.RequiredResearchId)
                && !completedResearch.Contains(definition.RequiredResearchId))
            {
                report.AddError(
                    $"Combat craft order '{order.orderId}' bypasses required research '{definition.RequiredResearchId}'.");
            }
        }

        foreach (EquipmentHistoryTransferOrder order in
                 source?.historyTransferOrders
                 ?? new List<EquipmentHistoryTransferOrder>())
        {
            if (order == null)
            {
                continue;
            }
            RequireReference(
                order.sourceEquipmentInstanceId,
                physical.EquipmentInstanceIds,
                $"History transfer '{order.orderId}' source equipment",
                report);
            RequireReference(
                order.targetEquipmentInstanceId,
                physical.EquipmentInstanceIds,
                $"History transfer '{order.orderId}' target equipment",
                report);
            RequireReference(
                order.lineageSealStackId,
                physical.StackIds,
                $"History transfer '{order.orderId}' lineage-seal stack",
                report);
            if (TryGetEquipmentDefinition(
                    order.sourceEquipmentInstanceId,
                    physical,
                    out CombatEquipmentDefinitionSO sourceDefinition)
                && TryGetEquipmentDefinition(
                    order.targetEquipmentInstanceId,
                    physical,
                    out CombatEquipmentDefinitionSO targetDefinition)
                && sourceDefinition.LineageKind != targetDefinition.LineageKind)
            {
                report.AddError(
                    $"History transfer '{order.orderId}' crosses equipment lineage kinds.");
            }
        }
    }

    internal void RequireEquipmentReference<TDefinition>(
        string equipmentId,
        DungeonSavePhysicalReferenceIndex physical,
        string label,
        DungeonGameRestoreReport report)
        where TDefinition : CombatEquipmentDefinitionSO
    {
        if (!TryGetEquipmentDefinition(
                equipmentId,
                physical,
                out CombatEquipmentDefinitionSO definition)
            || definition is not TDefinition)
        {
            report.AddError(
                $"{label} references missing equipment '{equipmentId ?? string.Empty}' or the wrong equipment kind.");
        }
    }

    internal bool TryGetEquipmentDefinition(
        string equipmentId,
        DungeonSavePhysicalReferenceIndex physical,
        out CombatEquipmentDefinitionSO definition)
    {
        definition = null;
        return !string.IsNullOrEmpty(equipmentId)
            && physical.EquipmentDefinitionIds.TryGetValue(
                equipmentId,
                out string definitionId)
            && combatEquipmentDefinitions.TryGet(definitionId, out definition);
    }

    internal static void ValidateEvolution(
        EquipmentEvolutionSaveData source,
        ISet<string> equipmentIds,
        ISet<string> buildingIds,
        DungeonGameRestoreReport report)
    {
        foreach ((string orderId, string equipmentId, string buildingId) in
                 (source?.reforgeOrders ?? new List<EvolutionReforgeOrder>())
                 .Where(order => order != null)
                 .Select(order => (
                     order.orderId,
                     order.equipmentInstanceId,
                     order.facilityPersistentId))
                 .Concat((source?.reattunementOrders
                         ?? new List<EquipmentReattunementOrder>())
                     .Where(order => order != null)
                     .Select(order => (
                         order.orderId,
                         order.equipmentInstanceId,
                         order.facilityPersistentId))))
        {
            RequireReference(
                equipmentId,
                equipmentIds,
                $"Equipment evolution '{orderId}' equipment",
                report);
            RequireReference(
                buildingId,
                buildingIds,
                $"Equipment evolution '{orderId}' facility",
                report);
        }
    }
}
