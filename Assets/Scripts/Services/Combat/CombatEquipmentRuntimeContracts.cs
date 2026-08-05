using System.Collections.Generic;
using UnityEngine;

public static class EquipmentProgressionFacilityContract
{
    public static bool Matches(BuildableObject facility, string requiredTag)
    {
        return facility != null
            && !facility.isDestroy
            && facility.PersistentInstanceId.IsValid
            && string.Equals(
                facility.GetProductionWorkstationTag(),
                requiredTag,
                System.StringComparison.Ordinal);
    }

    public static bool IsProgressionFacility(BuildableObject facility)
    {
        string tag = facility?.GetProductionWorkstationTag() ?? string.Empty;
        return tag == EquipmentProgressionWorkstationTags.Appraisal
            || tag == EquipmentProgressionWorkstationTags.Restoration
            || tag == EquipmentProgressionWorkstationTags.PrecisionFitting
            || tag == EquipmentProgressionWorkstationTags.RuneTuning
            || tag == EquipmentProgressionWorkstationTags.LineageArchive;
    }

    public static string GetLocalBufferDestinationId(BuildableObject facility)
    {
        return facility != null && facility.PersistentInstanceId.IsValid
            ? facility.PersistentInstanceId.Value
            : string.Empty;
    }
}

public interface ICombatEquipmentRuntime : IBuildingEquipmentCraftingRuntimePort
{
    IReadOnlyList<CombatEquipmentDefinitionSO> Definitions { get; }
    IReadOnlyCollection<CombatEquipmentInstance> Instances { get; }
    IReadOnlyList<CombatEquipmentCraftOrderSaveData> CraftQueue { get; }
    bool TryGetDefinition(string definitionId, out CombatEquipmentDefinitionSO definition);
    bool IsDefinitionUnlocked(string definitionId, out string failureReason);
    int GetAvailableCount(string definitionId);
    bool TryQueueCraft(
        string definitionId,
        BuildableObject craftingFacility,
        out string failureReason);
    bool TryQueueCraft(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        out string failureReason);
    int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId);
    int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId,
        out string completedMaterialId);
    CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState = CombatEquipmentWorldState.Stored,
        string materialId = "");
    IReadOnlyList<CraftMaterialDefinitionSO> GetAllowedMaterials(
        string definitionId);
    CombatEquipmentCraftMaterialPolicySaveData GetCraftMaterialPolicy(
        string definitionId,
        BuildableObject craftingFacility);
    bool SetCraftMaterialAllowed(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        bool allowed,
        out string failureReason);
    bool MoveCraftMaterialPriority(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        int offset,
        out string failureReason);
    bool TryGetPreviewStats(
        string definitionId,
        string materialId,
        out CombatEquipmentDerivedStats stats);
    bool TryGetDerivedStats(
        string instanceId,
        out CombatEquipmentDerivedStats stats);
    bool TrySalvage(
        string instanceId,
        Vector2Int outputPosition,
        out string recoveredItemId,
        out int recoveredAmount,
        out string failureReason);
    bool TryGetInstance(string instanceId, out CombatEquipmentInstance instance);
    bool TryUpdateEvolutionState(
        string instanceId,
        EquipmentEvolutionState evolutionState);
    bool TryGetInstanceBySourceStack(string sourceStackId, out CombatEquipmentInstance instance);
    bool TryLinkToWorldStack(
        string instanceId,
        string sourceStackId,
        CombatEquipmentWorldState worldState);
    bool TrySetWorldStateBySourceStack(string sourceStackId, CombatEquipmentWorldState worldState);
    bool TryMarkLost(string instanceId);
    bool TryAssignToCharacter(string characterId, string instanceId, out string failureReason);
    bool TryUnassignSlot(
        string characterId,
        CombatEquipmentLoadoutSlot slot,
        out string failureReason);
    bool TrySetActiveWeapon(string characterId, string instanceId, out string failureReason);
    bool TrySetActiveProfile(string characterId, string profileId);
    bool TrySetFireMode(string characterId, CombatFireMode fireMode, out string failureReason);
    bool TrySetHoldFire(string characterId, bool holdFire);
    CharacterCombatLoadoutState GetOrCreateLoadout(string characterId);
    CharacterCombatLoadoutProfile GetActiveProfileSnapshot(string characterId);
    bool TryGetActiveWeapon(string characterId, out CombatWeaponSnapshot weapon);
    IReadOnlyList<CombatArmorSnapshot> GetArmor(string characterId);
    CombatShieldSnapshot GetShield(string characterId, float incomingAngleDegrees = 0f);
    bool HasCompatibleAmmunition(
        string characterId,
        string instanceId);
    bool TryGetPreferredAmmunitionItemId(
        string definitionId,
        out ItemDefinitionId itemId);
    bool TryReloadFromInventory(
        string instanceId,
        CharacterCarryInventory inventory,
        out int consumedAmmo);
    bool TryReloadFromInventory(
        string instanceId,
        CharacterCarryInventory inventory,
        out ItemDefinitionId consumedAmmoItemId,
        out int consumedAmmo);
    bool TryReloadFromCharacterInventory(
        string characterId,
        string instanceId,
        out int consumedAmmo);
    bool TryReloadFromCharacterInventory(
        string characterId,
        string instanceId,
        out ItemDefinitionId consumedAmmoItemId,
        out int consumedAmmo);
    bool TryConsumeLoadedAmmo(string instanceId);
    bool TryApplyDurabilityDamage(string instanceId, float damage);
    bool TryDetachForMaintenance(
        string instanceId,
        out CombatEquipmentInstance detached);
    IReadOnlyList<CombatEquipmentInstance> ConfiscateAllFromCharacter(
        string characterId);
    void HandleCharacterDeath(string characterId);
    bool TryRestoreDurability(string instanceId, float durabilityRatio);
    IReadOnlyCollection<EquipmentModuleInstance> ModuleInstances { get; }
    IReadOnlyList<EquipmentHistoryTransferOrder> HistoryTransferOrders { get; }
    bool TryGetModuleDefinition(
        string definitionId,
        out EquipmentModuleDefinitionSO definition);
    EquipmentModuleInstance CreateExpeditionModule(
        string definitionId,
        int grade,
        Vector2Int deliveryPosition,
        WorldItemStackState worldState = WorldItemStackState.Loose,
        string destinationId = "",
        bool identified = false);
    bool TryAppraiseModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure);
    bool TryRestoreModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure);
    bool TryTuneModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure);
    bool TryInstallModule(
        string equipmentInstanceId,
        string moduleInstanceId,
        int slotIndex,
        BuildableObject facility,
        out DomainFailure failure);
    bool TryRemoveModule(
        string equipmentInstanceId,
        int slotIndex,
        BuildableObject facility,
        out EquipmentModuleInstance removed,
        out DomainFailure failure);
    bool TryQueueHistoryTransfer(
        string sourceEquipmentInstanceId,
        string targetEquipmentInstanceId,
        string lineageSealStackId,
        BuildableObject facility,
        out EquipmentHistoryTransferOrder order,
        out DomainFailure failure);
    bool ApplyHistoryTransferWork(
        string orderId,
        float work,
        BuildableObject facility,
        out bool completed,
        out DomainFailure failure);
    bool TryClaimRegionLineageSeal(string regionId);
    float GetCarriedWeight(string characterId);
    DungeonCombatEquipmentSaveData Capture();
    CombatEquipmentRestoreCandidate BuildRestoreCandidate(
        DungeonCombatEquipmentSaveData saveData);
    void PublishRestoreCandidate(CombatEquipmentRestoreCandidate candidate);
}

public interface ICombatLoadoutRuntime
{
    CharacterCombatLoadoutState GetOrCreateLoadout(string characterId);
    bool TrySetActiveProfile(string characterId, string profileId);
    bool TrySetActiveWeapon(string characterId, string instanceId, out string failureReason);
    bool TrySetFireMode(string characterId, CombatFireMode fireMode, out string failureReason);
    bool TrySetHoldFire(string characterId, bool holdFire);
}
