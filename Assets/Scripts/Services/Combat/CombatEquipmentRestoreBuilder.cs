using System;
using System.Collections.Generic;
using System.Linq;

internal static class CombatEquipmentRestoreBuilder
{
    internal static CombatEquipmentRestoreCandidate Build(
        DungeonCombatEquipmentSaveData source,
        ICombatEquipmentCatalog catalog,
        CombatEquipmentCraftingRuntime crafting)
    {
        if (source == null
            || source.loadouts == null
            || source.craftOrders == null
            || source.craftMaterialPolicies == null
            || source.historyTransferOrders == null
            || source.claimedLineageSealRegionIds == null)
        {
            throw new InvalidOperationException(
                "Combat equipment V6 payload is missing a required collection.");
        }

        CombatEquipmentRuntimeState restored = new();
        RestoreLoadouts(source.loadouts, restored, catalog);
        RestoreCraftOrders(source.craftOrders, restored, catalog, crafting);
        RestoreMaterialPolicies(
            source.craftMaterialPolicies,
            restored,
            catalog,
            crafting);
        RestoreHistoryOrders(source.historyTransferOrders, restored);
        RestoreClaimedRegions(source.claimedLineageSealRegionIds, restored);
        return new CombatEquipmentRestoreCandidate(restored);
    }

    private static void RestoreLoadouts(
        IEnumerable<CharacterCombatLoadoutState> source,
        CombatEquipmentRuntimeState restored,
        ICombatEquipmentCatalog catalog)
    {
        foreach (CharacterCombatLoadoutState loadout in source)
        {
            if (loadout == null)
            {
                throw new InvalidOperationException(
                    "Combat equipment loadout collection contains null.");
            }
            RequireCanonicalId(loadout.characterId, "loadout character");
            RequireCanonicalId(loadout.activeProfileId, "active loadout profile");
            if (loadout.profiles == null
                || !restored.Loadouts.TryAdd(
                    loadout.characterId,
                    CloneLoadout(loadout, catalog)))
            {
                throw new InvalidOperationException(
                    $"Loadout for character '{loadout.characterId}' is duplicate or incomplete.");
            }
        }
    }

    private static CharacterCombatLoadoutState CloneLoadout(
        CharacterCombatLoadoutState source,
        ICombatEquipmentCatalog catalog)
    {
        CharacterCombatLoadoutState clone = new()
        {
            characterId = source.characterId,
            activeProfileId = source.activeProfileId,
            profiles = new List<CharacterCombatLoadoutProfile>()
        };
        HashSet<string> profileIds = new(StringComparer.Ordinal);
        foreach (CharacterCombatLoadoutProfile profile in source.profiles)
        {
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Loadout '{source.characterId}' contains a null profile.");
            }
            RequireCanonicalId(profile.profileId, "loadout profile");
            RequireCanonicalTextOrEmpty(profile.displayName, "loadout display name");
            RequireCanonicalTextOrEmpty(
                profile.desiredShieldDefinitionId,
                "desired shield definition");
            RequireCanonicalTextOrEmpty(
                profile.shieldInstanceId,
                "shield instance");
            RequireCanonicalTextOrEmpty(
                profile.activeWeaponInstanceId,
                "active weapon instance");
            if (!profileIds.Add(profile.profileId)
                || profile.weaponInstanceIds == null
                || profile.armorInstanceIds == null
                || profile.desiredWeaponDefinitionIds == null
                || profile.desiredArmorDefinitionIds == null
                || profile.desiredAmmo < 0
                || !Enum.IsDefined(typeof(CombatFireMode), profile.fireMode))
            {
                throw new InvalidOperationException(
                    $"Loadout profile '{profile.profileId}' has duplicate IDs or invalid fields.");
            }

            ValidateUniqueIds(profile.weaponInstanceIds, "weapon instance");
            ValidateUniqueIds(profile.armorInstanceIds, "armor instance");
            ValidateDefinitions<CombatWeaponSO>(
                profile.desiredWeaponDefinitionIds,
                "desired weapon",
                catalog);
            ValidateDefinitions<CombatArmorSO>(
                profile.desiredArmorDefinitionIds,
                "desired armor",
                catalog);
            if (!string.IsNullOrEmpty(profile.desiredShieldDefinitionId)
                && (!catalog.TryGet(
                        profile.desiredShieldDefinitionId,
                        out CombatEquipmentDefinitionSO shield)
                    || shield is not CombatShieldSO))
            {
                throw new InvalidOperationException(
                    $"Loadout profile '{profile.profileId}' references unknown shield definition '{profile.desiredShieldDefinitionId}'.");
            }
            if (!string.IsNullOrEmpty(profile.activeWeaponInstanceId)
                && !profile.weaponInstanceIds.Contains(
                    profile.activeWeaponInstanceId,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Active weapon '{profile.activeWeaponInstanceId}' is not in profile '{profile.profileId}'.");
            }
            clone.profiles.Add(profile.Clone());
        }
        if (clone.profiles.Count == 0
            || !profileIds.Contains(clone.activeProfileId))
        {
            throw new InvalidOperationException(
                $"Loadout '{source.characterId}' has no active profile definition.");
        }
        return clone;
    }

    private static void RestoreCraftOrders(
        IEnumerable<CombatEquipmentCraftOrderSaveData> source,
        CombatEquipmentRuntimeState restored,
        ICombatEquipmentCatalog catalog,
        CombatEquipmentCraftingRuntime crafting)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (CombatEquipmentCraftOrderSaveData order in source)
        {
            if (order == null)
            {
                throw new InvalidOperationException(
                    "Combat craft order collection contains null.");
            }
            RequireCanonicalId(order.orderId, "combat craft order");
            RequireCanonicalId(order.definitionId, "combat craft definition");
            RequireCanonicalTextOrEmpty(order.materialId, "combat craft material");
            RequireCanonicalId(
                order.materialDestinationId,
                "combat craft material destination");
            bool ammunition = CombatEquipmentCraftingRuntime.IsAmmunitionRecipe(
                order.definitionId);
            if (!ids.Add(order.orderId)
                || !IsFinitePositive(order.requiredWork)
                || !IsFiniteInRange(
                    order.completedWork,
                    0f,
                    order.requiredWork,
                    includeMaximum: true)
                || !string.Equals(
                    order.materialDestinationId,
                    WorldItemStackRuntime.FacilityInputDestinationPrefix
                        + order.orderId,
                    StringComparison.Ordinal)
                || !Enum.IsDefined(
                    typeof(CraftsmanshipQualityTier),
                    order.minimumQuality)
                || !Enum.IsDefined(
                    typeof(RejectedOutputDisposition),
                    order.rejectedDisposition)
                || !Enum.IsDefined(
                    typeof(QualityRepeatLimitMode),
                    order.repeatLimitMode)
                || !Enum.IsDefined(
                    typeof(QualityTargetPipelineStage),
                    order.qualityStage)
                || order.maximumAttempts <= 0
                || order.requiredAcceptedCount <= 0
                || order.acceptedCount < 0
                || order.acceptedCount > order.requiredAcceptedCount
                || order.consumedWork < 0f
                || (order.rejectedOutputConsumed
                    && !order.dismantlingRejectedOutput)
                || (order.dismantlingRejectedOutput
                    && (string.IsNullOrWhiteSpace(order.rejectedInstanceId)
                        || string.IsNullOrWhiteSpace(order.rejectedStackId)))
                || (!ammunition
                    && (order.qualityRoll == null
                        || order.qualityRoll.attemptIndex
                            != order.qualityAttemptIndex)))
            {
                throw new InvalidOperationException(
                    $"Combat craft order '{order.orderId}' has duplicate ID or invalid work.");
            }
            if (!ammunition
                && (!catalog.TryGet(order.definitionId, out CombatEquipmentDefinitionSO definition)
                    || !ValidateMaterial(definition, order.materialId, crafting)))
            {
                throw new InvalidOperationException(
                    $"Combat craft order '{order.orderId}' references invalid authored content.");
            }
            if (!ammunition
                && !crafting.IsDefinitionUnlocked(order.definitionId, out _))
            {
                throw new InvalidOperationException(
                    $"Combat craft order '{order.orderId}' bypasses its current research lock.");
            }
            if (ammunition && !string.IsNullOrEmpty(order.materialId))
            {
                throw new InvalidOperationException(
                    $"Ammunition craft order '{order.orderId}' cannot carry an equipment material ID.");
            }
            restored.CraftOrders.Add(order.Clone());
        }
    }

    private static void RestoreMaterialPolicies(
        IEnumerable<CombatEquipmentCraftMaterialPolicySaveData> source,
        CombatEquipmentRuntimeState restored,
        ICombatEquipmentCatalog catalog,
        CombatEquipmentCraftingRuntime crafting)
    {
        foreach (CombatEquipmentCraftMaterialPolicySaveData policy in source)
        {
            if (policy == null)
            {
                throw new InvalidOperationException(
                    "Combat craft material policy collection contains null.");
            }
            RequireCanonicalId(policy.facilityKey, "material policy facility");
            RequireCanonicalId(policy.definitionId, "material policy definition");
            if (policy.priorityMaterialIds == null
                || policy.allowedMaterialIds == null
                || !catalog.TryGet(
                    policy.definitionId,
                    out CombatEquipmentDefinitionSO definition))
            {
                throw new InvalidOperationException(
                    $"Material policy '{policy.facilityKey}/{policy.definitionId}' is incomplete or references an unknown definition.");
            }

            string[] authored = crafting.GetAllowedMaterials(policy.definitionId)
                .Select(material => material.MaterialId)
                .ToArray();
            ValidateUniqueIds(policy.priorityMaterialIds, "priority material");
            ValidateUniqueIds(policy.allowedMaterialIds, "allowed material");
            if (policy.priorityMaterialIds.Count != authored.Length
                || policy.priorityMaterialIds.Any(id =>
                    !authored.Contains(id, StringComparer.Ordinal))
                || policy.allowedMaterialIds.Count == 0
                || policy.allowedMaterialIds.Any(id =>
                    !authored.Contains(id, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Material policy '{policy.facilityKey}/{policy.definitionId}' does not exactly match authored materials.");
            }

            string key = policy.facilityKey + "|" + definition.EquipmentId;
            if (!restored.CraftMaterialPolicies.TryAdd(key, policy.Clone()))
            {
                throw new InvalidOperationException(
                    $"Duplicate combat material policy '{key}'.");
            }
        }
    }

    private static void RestoreHistoryOrders(
        IEnumerable<EquipmentHistoryTransferOrder> source,
        CombatEquipmentRuntimeState restored)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> equipmentReservations = new(StringComparer.Ordinal);
        foreach (EquipmentHistoryTransferOrder order in source)
        {
            if (order == null)
            {
                throw new InvalidOperationException(
                    "Equipment history transfer order collection contains null.");
            }
            RequireCanonicalId(order.orderId, "history transfer order");
            RequireCanonicalId(order.sourceEquipmentInstanceId, "history source equipment");
            RequireCanonicalId(order.targetEquipmentInstanceId, "history target equipment");
            RequireCanonicalId(order.lineageSealStackId, "history lineage seal stack");
            RequireCanonicalId(order.facilityPersistentId, "history transfer facility");
            RequireCanonicalId(order.destinationId, "history transfer destination");
            if (!((BuildingInstanceId)order.facilityPersistentId).IsValid
                || !string.Equals(
                    order.destinationId,
                    order.facilityPersistentId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"History transfer order '{order.orderId}' has an invalid facility buffer identity.");
            }
            if (!ids.Add(order.orderId)
                || order.completed
                || string.Equals(
                    order.sourceEquipmentInstanceId,
                    order.targetEquipmentInstanceId,
                    StringComparison.Ordinal)
                || !equipmentReservations.Add(order.sourceEquipmentInstanceId)
                || !equipmentReservations.Add(order.targetEquipmentInstanceId)
                || !IsFinitePositive(order.requiredWork)
                || !IsFiniteInRange(
                    order.completedWork,
                    0f,
                    order.requiredWork,
                    includeMaximum: false))
            {
                throw new InvalidOperationException(
                    $"History transfer order '{order.orderId}' is duplicate, completed, conflicting, or out of range.");
            }
            restored.HistoryTransferOrders.Add(order.Clone());
        }
    }

    private static void RestoreClaimedRegions(
        IEnumerable<string> source,
        CombatEquipmentRuntimeState restored)
    {
        foreach (string id in source)
        {
            RequireCanonicalId(id, "claimed lineage region");
            if (!restored.ClaimedLineageSealRegionIds.Add(id))
            {
                throw new InvalidOperationException(
                    $"Duplicate claimed lineage region '{id}'.");
            }
        }
    }

    private static bool ValidateMaterial(
        CombatEquipmentDefinitionSO definition,
        string materialId,
        CombatEquipmentCraftingRuntime crafting)
    {
        IReadOnlyList<CraftMaterialDefinitionSO> allowed =
            crafting.GetAllowedMaterials(definition.EquipmentId);
        return allowed.Count == 0
            ? string.Equals(
                materialId,
                definition.DefaultMaterialId,
                StringComparison.Ordinal)
                || string.IsNullOrEmpty(materialId)
            : allowed.Any(material => string.Equals(
                material.MaterialId,
                materialId,
                StringComparison.Ordinal));
    }

    private static void ValidateDefinitions<TDefinition>(
        IEnumerable<string> source,
        string label,
        ICombatEquipmentCatalog catalog)
        where TDefinition : CombatEquipmentDefinitionSO
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string id in source)
        {
            RequireCanonicalId(id, label);
            if (!ids.Add(id)
                || !catalog.TryGet(id, out CombatEquipmentDefinitionSO definition)
                || definition is not TDefinition)
            {
                throw new InvalidOperationException(
                    $"{label} definition '{id}' is duplicate, unknown, or has the wrong kind.");
            }
        }
    }

    private static void ValidateUniqueIds(
        IEnumerable<string> source,
        string label)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string id in source)
        {
            RequireCanonicalId(id, label);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException(
                    $"Duplicate {label} id '{id}'.");
            }
        }
    }

    private static bool IsFinitePositive(float value) =>
        float.IsFinite(value) && value > 0f;

    private static bool IsFiniteInRange(
        float value,
        float minimum,
        float maximum,
        bool includeMaximum)
    {
        return float.IsFinite(value)
            && value >= minimum
            && (includeMaximum ? value <= maximum : value < maximum);
    }

    private static void RequireCanonicalId(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} id must be non-empty and canonical.");
        }
    }

    private static void RequireCanonicalTextOrEmpty(string value, string label)
    {
        if (value == null
            || (!string.IsNullOrEmpty(value)
                && !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{label} must be non-null and canonical.");
        }
    }
}
