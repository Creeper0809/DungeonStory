#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class D13FaultInjectingEquipmentRuntime : ICombatEquipmentRuntime
{
    private readonly ICombatEquipmentRuntime inner;

    internal D13FaultInjectingEquipmentRuntime(ICombatEquipmentRuntime inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    internal bool RejectAmmunitionConsumption;
    internal int AmmunitionConsumeAttempts;
    internal bool RejectArcaneOverchargeDurabilityDamage;
    internal int ArcaneOverchargeDurabilityAttempts;
    internal Func<float> ObserveMana;
    internal float ManaObservedAtRejectedConsumption;

    public IReadOnlyList<CombatEquipmentCraftOrderSaveData> CraftQueue =>
        inner.CraftQueue;
    public IReadOnlyList<CombatEquipmentDefinitionSO> Definitions =>
        inner.Definitions;
    public IReadOnlyCollection<CombatEquipmentInstance> Instances =>
        inner.Instances;
    public IReadOnlyCollection<EquipmentModuleInstance> ModuleInstances =>
        inner.ModuleInstances;
    public IReadOnlyList<EquipmentHistoryTransferOrder> HistoryTransferOrders =>
        inner.HistoryTransferOrders;

    public CraftQualityAttemptEstimate CaptureCraftQualityEstimate(string orderId) =>
        inner.CaptureCraftQualityEstimate(orderId);

    public bool TryGetDefinition(
        string definitionId,
        out CombatEquipmentDefinitionSO definition) =>
        inner.TryGetDefinition(definitionId, out definition);

    public bool IsDefinitionUnlocked(string definitionId, out string failureReason) =>
        inner.IsDefinitionUnlocked(definitionId, out failureReason);

    public int GetAvailableCount(string definitionId) =>
        inner.GetAvailableCount(definitionId);

    public bool TryQueueCraft(
        string definitionId,
        BuildableObject craftingFacility,
        out string failureReason) =>
        inner.TryQueueCraft(definitionId, craftingFacility, out failureReason);

    public bool TryQueueCraft(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        out string failureReason) =>
        inner.TryQueueCraft(
            definitionId,
            materialId,
            craftingFacility,
            out failureReason);

    public bool TryGetNextCraftMaterialContext(
        IEnumerable<string> craftableDefinitionIds,
        CharacterActor worker,
        out string definitionId,
        out string materialId,
        out bool usesSubstituteMaterial) =>
        inner.TryGetNextCraftMaterialContext(
            craftableDefinitionIds,
            worker,
            out definitionId,
            out materialId,
            out usesSubstituteMaterial);

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId) =>
        inner.ApplyCraftWork(
            craftableDefinitionIds,
            workUnits,
            out completedDefinitionId);

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId,
        out string completedMaterialId) =>
        inner.ApplyCraftWork(
            craftableDefinitionIds,
            workUnits,
            out completedDefinitionId,
            out completedMaterialId);

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        CharacterActor worker,
        float relevantSkill,
        out string completedDefinitionId,
        out string completedMaterialId,
        out CombatEquipmentQuality completedQuality) =>
        inner.ApplyCraftWork(
            craftableDefinitionIds,
            workUnits,
            worker,
            relevantSkill,
            out completedDefinitionId,
            out completedMaterialId,
            out completedQuality);

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        CharacterActor worker,
        float relevantSkill,
        out string completedDefinitionId,
        out string completedMaterialId,
        out CombatEquipmentQuality completedQuality,
        out MythicProvenanceSaveData completedMythicProvenance) =>
        inner.ApplyCraftWork(
            craftableDefinitionIds,
            workUnits,
            worker,
            relevantSkill,
            out completedDefinitionId,
            out completedMaterialId,
            out completedQuality,
            out completedMythicProvenance);

    public WorkerSelectionPolicySaveData GetCraftWorkerPolicy(string orderId) =>
        inner.GetCraftWorkerPolicy(orderId);

    public bool SetCraftWorkerPolicy(
        string orderId,
        WorkerSelectionPolicySaveData policy,
        out string failureReason) =>
        inner.SetCraftWorkerPolicy(orderId, policy, out failureReason);

    public bool SetCraftQualityTarget(
        string orderId,
        CraftsmanshipQualityTier minimumQuality,
        RejectedOutputDisposition rejectedDisposition,
        QualityRepeatLimitMode repeatLimitMode,
        int maximumAttempts,
        float workBudget,
        int requiredAcceptedCount,
        out string failureReason) =>
        inner.SetCraftQualityTarget(
            orderId,
            minimumQuality,
            rejectedDisposition,
            repeatLimitMode,
            maximumAttempts,
            workBudget,
            requiredAcceptedCount,
            out failureReason);

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState = CombatEquipmentWorldState.Stored,
        string materialId = "") =>
        inner.CreateInstance(definitionId, quality, worldState, materialId);

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState,
        string materialId,
        MythicProvenanceSaveData mythicProvenance) =>
        inner.CreateInstance(
            definitionId,
            quality,
            worldState,
            materialId,
            mythicProvenance);

    public CombatEquipmentInstance CreateExternalInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        string materialId = "") =>
        inner.CreateExternalInstance(definitionId, quality, materialId);

    public IReadOnlyList<CraftMaterialDefinitionSO> GetAllowedMaterials(
        string definitionId) =>
        inner.GetAllowedMaterials(definitionId);

    public CombatEquipmentCraftMaterialPolicySaveData GetCraftMaterialPolicy(
        string definitionId,
        BuildableObject craftingFacility) =>
        inner.GetCraftMaterialPolicy(definitionId, craftingFacility);

    public bool SetCraftMaterialAllowed(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        bool allowed,
        out string failureReason) =>
        inner.SetCraftMaterialAllowed(
            definitionId,
            materialId,
            craftingFacility,
            allowed,
            out failureReason);

    public bool MoveCraftMaterialPriority(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        int offset,
        out string failureReason) =>
        inner.MoveCraftMaterialPriority(
            definitionId,
            materialId,
            craftingFacility,
            offset,
            out failureReason);

    public bool TryGetPreviewStats(
        string definitionId,
        string materialId,
        out CombatEquipmentDerivedStats stats) =>
        inner.TryGetPreviewStats(definitionId, materialId, out stats);

    public bool TryGetDerivedStats(
        string instanceId,
        out CombatEquipmentDerivedStats stats) =>
        inner.TryGetDerivedStats(instanceId, out stats);

    public bool TrySalvage(
        string instanceId,
        Vector2Int outputPosition,
        out string recoveredItemId,
        out int recoveredAmount,
        out string failureReason) =>
        inner.TrySalvage(
            instanceId,
            outputPosition,
            out recoveredItemId,
            out recoveredAmount,
            out failureReason);

    public bool TrySalvage(
        string instanceId,
        CharacterActor worker,
        Vector2Int outputPosition,
        out string recoveredItemId,
        out int recoveredAmount,
        out string failureReason) =>
        inner.TrySalvage(
            instanceId,
            worker,
            outputPosition,
            out recoveredItemId,
            out recoveredAmount,
            out failureReason);

    public bool TryDiscardBySourceStack(
        string sourceStackId,
        out bool wasSalvageable,
        out string failureReason) =>
        inner.TryDiscardBySourceStack(
            sourceStackId,
            out wasSalvageable,
            out failureReason);

    public bool TryBeginMarketSale(
        string sourceStackId,
        string operationId,
        out CombatEquipmentInstance pendingInstance,
        out string failureReason) =>
        inner.TryBeginMarketSale(
            sourceStackId,
            operationId,
            out pendingInstance,
            out failureReason);

    public bool TryFinalizeMarketSale(
        string itemInstanceId,
        string operationId,
        out CombatEquipmentInstance soldInstance,
        out string failureReason) =>
        inner.TryFinalizeMarketSale(
            itemInstanceId,
            operationId,
            out soldInstance,
            out failureReason);

    public bool TryRestoreMarketSalePendingToPhysical(
        string itemInstanceId,
        string operationId,
        string sourceStackId,
        CombatEquipmentWorldState restoredWorldState,
        out string failureReason) =>
        inner.TryRestoreMarketSalePendingToPhysical(
            itemInstanceId,
            operationId,
            sourceStackId,
            restoredWorldState,
            out failureReason);

    public bool TryGetInstance(
        string instanceId,
        out CombatEquipmentInstance instance) =>
        inner.TryGetInstance(instanceId, out instance);

    public bool TryUpdateEvolutionState(
        string instanceId,
        EquipmentEvolutionState evolutionState) =>
        inner.TryUpdateEvolutionState(instanceId, evolutionState);

    public bool TryGetInstanceBySourceStack(
        string sourceStackId,
        out CombatEquipmentInstance instance) =>
        inner.TryGetInstanceBySourceStack(sourceStackId, out instance);

    public bool TryLinkToWorldStack(
        string instanceId,
        string sourceStackId,
        CombatEquipmentWorldState worldState) =>
        inner.TryLinkToWorldStack(instanceId, sourceStackId, worldState);

    public bool TryDropExistingEquipmentToWorld(
        string instanceId,
        Vector2Int position,
        out string stackId,
        out string failureReason) =>
        inner.TryDropExistingEquipmentToWorld(
            instanceId,
            position,
            out stackId,
            out failureReason);

    public bool TryAssignPickedEquipmentToCharacter(
        string characterId,
        string instanceId,
        CharacterCarryInventory inventory,
        string sourceStackId,
        string itemId,
        out string failureReason) =>
        inner.TryAssignPickedEquipmentToCharacter(
            characterId,
            instanceId,
            inventory,
            sourceStackId,
            itemId,
            out failureReason);

    public bool TryUnequipSlotToWorld(
        string characterId,
        CombatEquipmentLoadoutSlot slot,
        Vector2Int position,
        out IReadOnlyList<string> stackIds,
        out string failureReason) =>
        inner.TryUnequipSlotToWorld(
            characterId,
            slot,
            position,
            out stackIds,
            out failureReason);

    public bool TrySetWorldStateBySourceStack(
        string sourceStackId,
        CombatEquipmentWorldState worldState) =>
        inner.TrySetWorldStateBySourceStack(sourceStackId, worldState);

    public bool TryMarkLost(string instanceId) => inner.TryMarkLost(instanceId);

    public bool TryBindRetailStock(
        string instanceId,
        string retailSourceOperationId,
        out string failureReason) =>
        inner.TryBindRetailStock(
            instanceId,
            retailSourceOperationId,
            out failureReason);

    public bool TryBindPhysicalToRetailStock(
        string instanceId,
        string expectedSourceStackId,
        string retailSourceOperationId,
        out string failureReason) =>
        inner.TryBindPhysicalToRetailStock(
            instanceId,
            expectedSourceStackId,
            retailSourceOperationId,
            out failureReason);

    public bool TryConsumeRetailStock(
        string instanceId,
        string retailSourceOperationId,
        out CombatEquipmentInstance consumedInstance,
        out string failureReason) =>
        inner.TryConsumeRetailStock(
            instanceId,
            retailSourceOperationId,
            out consumedInstance,
            out failureReason);

    public bool TryRestoreRetailStockToPhysical(
        string instanceId,
        string retailSourceOperationId,
        string sourceStackId,
        CombatEquipmentWorldState restoredWorldState,
        out string failureReason) =>
        inner.TryRestoreRetailStockToPhysical(
            instanceId,
            retailSourceOperationId,
            sourceStackId,
            restoredWorldState,
            out failureReason);

    public bool TryAssignToCharacter(
        string characterId,
        string instanceId,
        out string failureReason) =>
        inner.TryAssignToCharacter(characterId, instanceId, out failureReason);

    public bool TryUnassignSlot(
        string characterId,
        CombatEquipmentLoadoutSlot slot,
        out string failureReason) =>
        inner.TryUnassignSlot(characterId, slot, out failureReason);

    public bool TrySetActiveWeapon(
        string characterId,
        string instanceId,
        out string failureReason) =>
        inner.TrySetActiveWeapon(characterId, instanceId, out failureReason);

    public bool TrySetActiveProfile(string characterId, string profileId) =>
        inner.TrySetActiveProfile(characterId, profileId);

    public bool TrySetFireMode(
        string characterId,
        CombatFireMode fireMode,
        out string failureReason) =>
        inner.TrySetFireMode(characterId, fireMode, out failureReason);

    public bool TrySetHoldFire(string characterId, bool holdFire) =>
        inner.TrySetHoldFire(characterId, holdFire);

    public CharacterCombatLoadoutState GetOrCreateLoadout(string characterId) =>
        inner.GetOrCreateLoadout(characterId);

    public CharacterCombatLoadoutProfile GetActiveProfileSnapshot(
        string characterId) =>
        inner.GetActiveProfileSnapshot(characterId);

    public bool TryGetActiveProfileSnapshot(
        string characterId,
        out CharacterCombatLoadoutProfile profile) =>
        inner.TryGetActiveProfileSnapshot(characterId, out profile);

    public bool TryGetActiveWeapon(
        string characterId,
        out CombatWeaponSnapshot weapon) =>
        inner.TryGetActiveWeapon(characterId, out weapon);

    public IReadOnlyList<CombatArmorSnapshot> GetArmor(string characterId) =>
        inner.GetArmor(characterId);

    public CombatShieldSnapshot GetShield(
        string characterId,
        float incomingAngleDegrees = 0f) =>
        inner.GetShield(characterId, incomingAngleDegrees);

    public bool HasCompatibleAmmunition(string characterId, string instanceId) =>
        inner.HasCompatibleAmmunition(characterId, instanceId);

    public bool TryGetPreferredAmmunitionItemId(
        string definitionId,
        out ItemDefinitionId itemId) =>
        inner.TryGetPreferredAmmunitionItemId(definitionId, out itemId);

    public bool TryReloadFromInventory(
        string instanceId,
        CharacterCarryInventory inventory,
        out int consumedAmmo) =>
        inner.TryReloadFromInventory(instanceId, inventory, out consumedAmmo);

    public bool TryReloadFromInventory(
        string instanceId,
        CharacterCarryInventory inventory,
        out ItemDefinitionId consumedAmmoItemId,
        out int consumedAmmo) =>
        inner.TryReloadFromInventory(
            instanceId,
            inventory,
            out consumedAmmoItemId,
            out consumedAmmo);

    public bool TryReloadFromCharacterInventory(
        string characterId,
        string instanceId,
        out int consumedAmmo) =>
        inner.TryReloadFromCharacterInventory(
            characterId,
            instanceId,
            out consumedAmmo);

    public bool TryReloadFromCharacterInventory(
        string characterId,
        string instanceId,
        out ItemDefinitionId consumedAmmoItemId,
        out int consumedAmmo) =>
        inner.TryReloadFromCharacterInventory(
            characterId,
            instanceId,
            out consumedAmmoItemId,
            out consumedAmmo);

    public bool TryConsumeLoadedAmmo(string instanceId)
    {
        return RejectAmmunitionConsumption
            ? RejectAmmunitionConsumptionAttempt()
            : inner.TryConsumeLoadedAmmo(instanceId);
    }

    public bool TryConsumeLoadedAmmo(string instanceId, int amount)
    {
        return RejectAmmunitionConsumption
            ? RejectAmmunitionConsumptionAttempt()
            : inner.TryConsumeLoadedAmmo(instanceId, amount);
    }

    public bool TryConsumePower(string instanceId, float amount) =>
        inner.TryConsumePower(instanceId, amount);

    public bool TryRestorePower(string instanceId, float amount) =>
        inner.TryRestorePower(instanceId, amount);

    public bool TryLoadExternalAmmunition(
        string instanceId,
        string ammunitionItemId,
        int amount) =>
        inner.TryLoadExternalAmmunition(instanceId, ammunitionItemId, amount);

    public bool TryApplyDurabilityDamage(string instanceId, float damage) =>
        inner.TryApplyDurabilityDamage(instanceId, damage);

    public bool TryApplyArcaneOverchargeDurabilityDamage(
        string instanceId,
        float damage)
    {
        ArcaneOverchargeDurabilityAttempts++;
        return !RejectArcaneOverchargeDurabilityDamage
            && inner.TryApplyArcaneOverchargeDurabilityDamage(
                instanceId,
                damage);
    }

    public bool TryRollbackArcaneOverchargeDurabilityDamage(
        string instanceId,
        float durabilityRatio) =>
        inner.TryRollbackArcaneOverchargeDurabilityDamage(
            instanceId,
            durabilityRatio);

    public bool TryDetachForMaintenance(
        string instanceId,
        out CombatEquipmentInstance detached) =>
        inner.TryDetachForMaintenance(instanceId, out detached);

    public IReadOnlyList<CombatEquipmentInstance> ConfiscateAllFromCharacter(
        string characterId) =>
        inner.ConfiscateAllFromCharacter(characterId);

    public IReadOnlyList<CombatEquipmentInstance> ConfiscateAllForExpedition(
        string characterId) =>
        inner.ConfiscateAllForExpedition(characterId);

    public bool TryMaterializeRecoveredEquipment(
        string instanceId,
        Vector2Int position,
        out string failureReason) =>
        inner.TryMaterializeRecoveredEquipment(
            instanceId,
            position,
            out failureReason);

    public void HandleCharacterDeath(string characterId) =>
        inner.HandleCharacterDeath(characterId);

    public void RetireCharacterEquipment(string characterId) =>
        inner.RetireCharacterEquipment(characterId);

    public bool TryRestoreDurability(string instanceId, float durabilityRatio) =>
        inner.TryRestoreDurability(instanceId, durabilityRatio);

    public bool TryGetModuleDefinition(
        string definitionId,
        out EquipmentModuleDefinitionSO definition) =>
        inner.TryGetModuleDefinition(definitionId, out definition);

    public EquipmentModuleInstance CreateExpeditionModule(
        string definitionId,
        int grade,
        Vector2Int deliveryPosition,
        WorldItemStackState worldState = WorldItemStackState.Loose,
        string destinationId = "",
        bool identified = false) =>
        inner.CreateExpeditionModule(
            definitionId,
            grade,
            deliveryPosition,
            worldState,
            destinationId,
            identified);

    public bool TryAppraiseModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure) =>
        inner.TryAppraiseModule(moduleInstanceId, facility, out failure);

    public bool TryRestoreModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure) =>
        inner.TryRestoreModule(moduleInstanceId, facility, out failure);

    public bool TryTuneModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure) =>
        inner.TryTuneModule(moduleInstanceId, facility, out failure);

    public bool TryInstallModule(
        string equipmentInstanceId,
        string moduleInstanceId,
        int slotIndex,
        BuildableObject facility,
        out DomainFailure failure) =>
        inner.TryInstallModule(
            equipmentInstanceId,
            moduleInstanceId,
            slotIndex,
            facility,
            out failure);

    public bool TryRemoveModule(
        string equipmentInstanceId,
        int slotIndex,
        BuildableObject facility,
        out EquipmentModuleInstance removed,
        out DomainFailure failure) =>
        inner.TryRemoveModule(
            equipmentInstanceId,
            slotIndex,
            facility,
            out removed,
            out failure);

    public bool TryQueueHistoryTransfer(
        string sourceEquipmentInstanceId,
        string targetEquipmentInstanceId,
        string lineageSealStackId,
        BuildableObject facility,
        out EquipmentHistoryTransferOrder order,
        out DomainFailure failure) =>
        inner.TryQueueHistoryTransfer(
            sourceEquipmentInstanceId,
            targetEquipmentInstanceId,
            lineageSealStackId,
            facility,
            out order,
            out failure);

    public bool ApplyHistoryTransferWork(
        string orderId,
        float work,
        BuildableObject facility,
        out bool completed,
        out DomainFailure failure) =>
        inner.ApplyHistoryTransferWork(
            orderId,
            work,
            facility,
            out completed,
            out failure);

    public bool TryClaimRegionLineageSeal(string regionId) =>
        inner.TryClaimRegionLineageSeal(regionId);

    public float GetCarriedWeight(string characterId) =>
        inner.GetCarriedWeight(characterId);

    public DungeonCombatEquipmentSaveData Capture() => inner.Capture();

    public CombatEquipmentRestoreCandidate BuildRestoreCandidate(
        DungeonCombatEquipmentSaveData saveData) =>
        inner.BuildRestoreCandidate(saveData);

    public void PublishRestoreCandidate(CombatEquipmentRestoreCandidate candidate) =>
        inner.PublishRestoreCandidate(candidate);

    public bool HasPendingCraftWork(IEnumerable<string> craftableDefinitionIds) =>
        inner.HasPendingCraftWork(craftableDefinitionIds);

    private bool RejectAmmunitionConsumptionAttempt()
    {
        AmmunitionConsumeAttempts++;
        ManaObservedAtRejectedConsumption = ObserveMana == null
            ? 0f
            : ObserveMana();
        return false;
    }
}
#endif
