using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CombatEquipmentRuntime : ICombatEquipmentRuntime, ICombatLoadoutRuntime
{
    private readonly ICombatEquipmentCatalog catalog;
    private readonly IItemInstanceRepository itemInstances;
    private readonly ICharacterCarryInventoryRegistry carryInventories;
    private readonly CombatEquipmentStatProjector statProjector;
    private readonly IEquipmentModuleCatalog moduleCatalog;
    private readonly CombatEquipmentCraftingRuntime crafting;
    private readonly EquipmentModuleRuntime moduleRuntime;
    private readonly EquipmentHistoryTransferRuntime historyRuntime;
    private readonly CombatEquipmentPhysicalStateWriter physicalState;
    private readonly CombatEquipmentLoadoutRuntime loadoutRuntime;
    private readonly CombatEquipmentRuntimeStateStore stateStore;
    private readonly IEquipmentPhysicalItemGateway itemStackRuntime;
    private IDictionary<string, CombatEquipmentInstance> instances =>
        itemInstances.EquipmentInstances;
    private IDictionary<string, EquipmentModuleInstance> moduleInstances =>
        itemInstances.EquipmentModules;

    public CombatEquipmentRuntime(
        ICombatEquipmentCatalog catalog,
        IItemInstanceRepository itemInstances,
        ICharacterCarryInventoryRegistry carryInventories,
        IEquipmentModuleCatalog moduleCatalog,
        IEquipmentPhysicalItemGateway itemStackRuntime,
        CombatEquipmentRuntimeCollaborators collaborators,
        CombatEquipmentCraftingRuntime crafting,
        CombatEquipmentLoadoutRuntime loadoutRuntime)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.carryInventories = carryInventories
            ?? throw new ArgumentNullException(nameof(carryInventories));
        this.moduleCatalog = moduleCatalog
            ?? throw new ArgumentNullException(nameof(moduleCatalog));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        CombatEquipmentRuntimeCollaborators requiredCollaborators = collaborators
            ?? throw new ArgumentNullException(nameof(collaborators));
        this.statProjector = requiredCollaborators.StatProjector;
        this.physicalState = requiredCollaborators.PhysicalState;
        this.moduleRuntime = requiredCollaborators.ModuleRuntime;
        this.historyRuntime = requiredCollaborators.HistoryRuntime;
        this.stateStore = requiredCollaborators.StateStore;
        this.crafting = crafting
            ?? throw new ArgumentNullException(nameof(crafting));
        this.loadoutRuntime = loadoutRuntime
            ?? throw new ArgumentNullException(nameof(loadoutRuntime));
    }

    public IReadOnlyList<CombatEquipmentDefinitionSO> Definitions => catalog.All;
    public IReadOnlyCollection<CombatEquipmentInstance> Instances =>
        instances.Values.Select(instance => instance.Clone()).ToArray();
    public IReadOnlyList<CombatEquipmentCraftOrderSaveData> CraftQueue => crafting.Queue;
    public IReadOnlyCollection<EquipmentModuleInstance> ModuleInstances =>
        moduleRuntime.Snapshots;
    public IReadOnlyList<EquipmentHistoryTransferOrder> HistoryTransferOrders =>
        historyRuntime.Snapshots;

    public bool TryGetModuleDefinition(
        string definitionId,
        out EquipmentModuleDefinitionSO definition)
    {
        return moduleCatalog.TryGet(
            definitionId?.Trim() ?? string.Empty,
            out definition);
    }

    public bool TryGetDefinition(
        string definitionId,
        out CombatEquipmentDefinitionSO definition)
    {
        return catalog.TryGet(definitionId, out definition);
    }

    public bool IsDefinitionUnlocked(string definitionId, out string failureReason)
    {
        return crafting.IsDefinitionUnlocked(definitionId, out failureReason);
    }

    public int GetAvailableCount(string definitionId)
    {
        string normalizedId = definitionId?.Trim() ?? string.Empty;
        return instances.Values.Count(instance =>
            instance != null
            && string.Equals(instance.definitionId, normalizedId, StringComparison.Ordinal)
            && instance.worldState == CombatEquipmentWorldState.Stored);
    }

    public IReadOnlyList<CraftMaterialDefinitionSO> GetAllowedMaterials(
        string definitionId)
    {
        return crafting.GetAllowedMaterials(definitionId);
    }

    public CombatEquipmentCraftMaterialPolicySaveData GetCraftMaterialPolicy(
        string definitionId,
        BuildableObject craftingFacility)
    {
        return crafting.GetMaterialPolicy(definitionId, craftingFacility);
    }

    public bool SetCraftMaterialAllowed(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        bool allowed,
        out string failureReason)
    {
        return crafting.SetMaterialAllowed(
            definitionId,
            materialId,
            craftingFacility,
            allowed,
            out failureReason);
    }

    public bool MoveCraftMaterialPriority(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        int offset,
        out string failureReason)
    {
        return crafting.MoveMaterialPriority(
            definitionId,
            materialId,
            craftingFacility,
            offset,
            out failureReason);
    }

    public bool TryGetDerivedStats(
        string instanceId,
        out CombatEquipmentDerivedStats stats)
    {
        stats = default;
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            return false;
        }

        CraftMaterialDefinitionSO material = crafting.ResolveInstanceMaterial(
            instance,
            definition);
        stats = statProjector.Build(definition, material, instance);
        return true;
    }

    public bool TryGetPreviewStats(
        string definitionId,
        string materialId,
        out CombatEquipmentDerivedStats stats)
    {
        return crafting.TryGetPreviewStats(definitionId, materialId, out stats);
    }

    public bool TrySalvage(
        string instanceId,
        Vector2Int outputPosition,
        out string recoveredItemId,
        out int recoveredAmount,
        out string failureReason)
    {
        recoveredItemId = string.Empty;
        recoveredAmount = 0;
        failureReason = string.Empty;
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "해체할 장비를 찾을 수 없습니다.";
            return false;
        }

        if (instance.worldState is CombatEquipmentWorldState.Equipped
            or CombatEquipmentWorldState.ExpeditionPacked
            or CombatEquipmentWorldState.MaintenanceBuffer)
        {
            failureReason = "장착·출정·수리 중인 장비는 해체할 수 없습니다.";
            return false;
        }

        CraftMaterialDefinitionSO material = crafting.ResolveInstanceMaterial(
            instance,
            definition);
        if (material == null || string.IsNullOrWhiteSpace(material.ItemId))
        {
            failureReason = "원래 재질을 확인할 수 없습니다.";
            return false;
        }

        recoveredAmount = Mathf.FloorToInt(
            definition.PrimaryMaterialAmount
            * 0.5f
            * Mathf.Clamp01(instance.durabilityRatio));
        if (recoveredAmount <= 0)
        {
            failureReason = "회수할 수 있는 재료가 남아 있지 않습니다.";
            return false;
        }

        if (!itemStackRuntime.SpawnItemAt(
                material.ItemId,
                recoveredAmount,
                outputPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned != recoveredAmount)
        {
            recoveredAmount = 0;
            failureReason = "해체 재료를 월드에 생성하지 못했습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(instance.sourceStackId))
        {
            itemStackRuntime.DeleteStack(instance.sourceStackId);
        }

        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instances.Remove(instance.instanceId);
        recoveredItemId = material.ItemId;
        return true;
    }

    public bool TryQueueCraft(
        string definitionId,
        BuildableObject craftingFacility,
        out string failureReason)
    {
        return crafting.TryQueue(definitionId, craftingFacility, out failureReason);
    }

    public bool TryQueueCraft(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        out string failureReason)
    {
        return crafting.TryQueue(
            definitionId,
            materialId,
            craftingFacility,
            out failureReason);
    }

    public bool HasPendingCraftWork(IEnumerable<string> craftableDefinitionIds)
    {
        return crafting.HasPendingWork(craftableDefinitionIds);
    }

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId)
    {
        return crafting.ApplyWork(
            craftableDefinitionIds,
            workUnits,
            out completedDefinitionId,
            out _);
    }

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId,
        out string completedMaterialId)
    {
        return crafting.ApplyWork(
            craftableDefinitionIds,
            workUnits,
            out completedDefinitionId,
            out completedMaterialId);
    }

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState = CombatEquipmentWorldState.Stored,
        string materialId = "")
    {
        return crafting.CreateInstance(
            definitionId,
            quality,
            worldState,
            materialId);
    }

    public bool TryGetInstance(string instanceId, out CombatEquipmentInstance instance)
    {
        if (instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance stored))
        {
            instance = stored.Clone();
            return true;
        }

        instance = null;
        return false;
    }

    public bool TryUpdateEvolutionState(
        string instanceId,
        EquipmentEvolutionState evolutionState)
    {
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance))
        {
            return false;
        }

        instance.evolution = evolutionState?.Clone()
            ?? new EquipmentEvolutionState();
        CombatEquipmentStatProjector.NormalizeEvolutionPresentationState(instance.evolution);
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryGetInstanceBySourceStack(
        string sourceStackId,
        out CombatEquipmentInstance instance)
    {
        CombatEquipmentInstance stored = instances.Values.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sourceStackId,
                sourceStackId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));
        if (stored != null)
        {
            instance = stored.Clone();
            return true;
        }

        instance = null;
        return false;
    }

    public bool TryLinkToWorldStack(
        string instanceId,
        string sourceStackId,
        CombatEquipmentWorldState worldState)
    {
        if (!instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || string.IsNullOrWhiteSpace(sourceStackId))
        {
            return false;
        }

        WorldItemStackSnapshot physicalStack = itemStackRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && string.Equals(
                    stack.StackId,
                    sourceStackId.Trim(),
                    StringComparison.Ordinal));
        if (physicalStack == null
            || !string.Equals(
                physicalStack.ItemInstanceId,
                instance.instanceId,
                StringComparison.Ordinal))
        {
            return false;
        }

        instance.sourceStackId = sourceStackId.Trim();
        instance.worldState = worldState;
        PersistPhysicalState(instance);
        if (worldState is CombatEquipmentWorldState.Stored
            or CombatEquipmentWorldState.Loose
            or CombatEquipmentWorldState.Carried
            or CombatEquipmentWorldState.MaintenanceBuffer)
        {
            loadoutRuntime.RemoveEquipment(instance.instanceId);
            instance.ownerCharacterId = string.Empty;
        }

        return true;
    }

    private void PersistPhysicalState(CombatEquipmentInstance instance)
    {
        physicalState.Persist(instance);
    }

    public bool TrySetWorldStateBySourceStack(
        string sourceStackId,
        CombatEquipmentWorldState worldState)
    {
        CombatEquipmentInstance instance = instances.Values.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sourceStackId,
                sourceStackId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));
        if (instance == null)
        {
            return false;
        }

        instance.worldState = worldState;
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryMarkLost(string instanceId)
    {
        if (!instances.TryGetValue(
            instanceId?.Trim() ?? string.Empty,
            out CombatEquipmentInstance instance))
        {
            return false;
        }

        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = string.Empty;
        instance.worldState = CombatEquipmentWorldState.Lost;
        foreach (EquipmentModuleSlotState slot in instance.moduleSlots
                     ?? new List<EquipmentModuleSlotState>())
        {
            if (slot != null
                && moduleInstances.TryGetValue(slot.moduleInstanceId,
                    out EquipmentModuleInstance module))
            {
                module.attachedEquipmentInstanceId = string.Empty;
                module.state = EquipmentModuleProcessState.Lost;
                module.condition = 0f;
            }
        }
        return true;
    }

    public bool TryAssignToCharacter(
        string characterId,
        string instanceId,
        out string failureReason)
    {
        return loadoutRuntime.TryAssign(
            characterId,
            instanceId,
            out failureReason);
    }

    public bool TryUnassignSlot(
        string characterId,
        CombatEquipmentLoadoutSlot slot,
        out string failureReason)
    {
        return loadoutRuntime.TryUnassign(characterId, slot, out failureReason);
    }

    public bool TrySetActiveWeapon(
        string characterId,
        string instanceId,
        out string failureReason)
    {
        return loadoutRuntime.TrySetActiveWeapon(
            characterId,
            instanceId,
            out failureReason);
    }

    public bool TrySetActiveProfile(string characterId, string profileId)
    {
        return loadoutRuntime.TrySetActiveProfile(characterId, profileId);
    }

    public bool TrySetFireMode(
        string characterId,
        CombatFireMode fireMode,
        out string failureReason)
    {
        return loadoutRuntime.TrySetFireMode(
            characterId,
            fireMode,
            out failureReason);
    }

    public bool TrySetHoldFire(string characterId, bool holdFire)
    {
        return loadoutRuntime.TrySetHoldFire(characterId, holdFire);
    }

    public CharacterCombatLoadoutState GetOrCreateLoadout(string characterId)
    {
        return loadoutRuntime.GetOrCreate(characterId);
    }

    public CharacterCombatLoadoutProfile GetActiveProfileSnapshot(string characterId)
    {
        return loadoutRuntime.GetActiveProfileSnapshot(characterId);
    }

    public bool TryGetActiveWeapon(
        string characterId,
        out CombatWeaponSnapshot weapon)
    {
        return loadoutRuntime.TryGetActiveWeapon(characterId, out weapon);
    }

    public IReadOnlyList<CombatArmorSnapshot> GetArmor(string characterId)
    {
        return loadoutRuntime.GetArmor(characterId);
    }

    public CombatShieldSnapshot GetShield(
        string characterId,
        float incomingAngleDegrees = 0f)
    {
        return loadoutRuntime.GetShield(characterId, incomingAngleDegrees);
    }


    public bool HasCompatibleAmmunition(
        string characterId,
        string instanceId)
    {
        CharacterCarryInventory inventory = carryInventories.Find((CharacterId)characterId);
        if (inventory == null)
        {
            return false;
        }

        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance))
        {
            return false;
        }

        if (!catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon)
        {
            return false;
        }

        return CombatAmmunitionPolicy.CountAvailable(weapon, inventory) > 0;
    }

    public bool TryGetPreferredAmmunitionItemId(
        string definitionId,
        out ItemDefinitionId itemId)
    {
        itemId = default;
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon)
        {
            return false;
        }

        itemId = CombatAmmunitionPolicy.GetPreferred(
            weapon.CompatibleAmmunitionItemIds);
        return itemId.IsValid;
    }

    public bool TryReloadFromInventory(
        string instanceId,
        CharacterCarryInventory inventory,
        out int consumedAmmo)
    {
        return TryReloadFromInventory(
            instanceId,
            inventory,
            out _,
            out consumedAmmo);
    }

    public bool TryReloadFromInventory(
        string instanceId,
        CharacterCarryInventory inventory,
        out ItemDefinitionId consumedAmmoItemId,
        out int consumedAmmo)
    {
        consumedAmmoItemId = default;
        consumedAmmo = 0;
        if (inventory == null
            || !instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon
            || weapon.MagazineCapacity <= 0
            || !CombatAmmunitionPolicy.TrySelectAvailable(
                weapon,
                inventory,
                out ItemDefinitionId selectedItemId))
        {
            return false;
        }

        int needed = Mathf.Max(0, weapon.MagazineCapacity - instance.loadedAmmo);
        int available = inventory.CountItem(selectedItemId.Value);
        consumedAmmo = Mathf.Min(needed, available);
        if (consumedAmmo <= 0
            || !CombatAmmunitionPolicy.TryConsumeSelected(
                inventory,
                selectedItemId,
                consumedAmmo))
        {
            consumedAmmo = 0;
            return false;
        }

        consumedAmmoItemId = selectedItemId;
        instance.loadedAmmo += consumedAmmo;
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryReloadFromCharacterInventory(
        string characterId,
        string instanceId,
        out int consumedAmmo)
    {
        return TryReloadFromCharacterInventory(
            characterId,
            instanceId,
            out _,
            out consumedAmmo);
    }

    public bool TryReloadFromCharacterInventory(
        string characterId,
        string instanceId,
        out ItemDefinitionId consumedAmmoItemId,
        out int consumedAmmo)
    {
        return TryReloadFromInventory(
            instanceId,
            carryInventories.Find((CharacterId)characterId),
            out consumedAmmoItemId,
            out consumedAmmo);
    }

    public bool TryConsumeLoadedAmmo(string instanceId)
    {
        if (!instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || instance.loadedAmmo <= 0)
        {
            return false;
        }

        instance.loadedAmmo--;
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryApplyDurabilityDamage(string instanceId, float damage)
    {
        if (damage <= 0f
            || !instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is CombatEquipmentKind.MeleeWeapon
                or CombatEquipmentKind.RangedWeapon
                or CombatEquipmentKind.RecoverableThrowingWeapon)
        {
            return false;
        }

        float maxDurability = statProjector.Build(
            definition,
            crafting.ResolveInstanceMaterial(instance, definition),
            instance).MaxDurability;
        instance.durabilityRatio = Mathf.Clamp01(
            instance.durabilityRatio - damage / maxDurability);
        PersistPhysicalState(instance);
        return true;
    }

    public bool TryDetachForMaintenance(
        string instanceId,
        out CombatEquipmentInstance detached)
    {
        detached = null;
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is not CombatEquipmentKind.Armor
                and not CombatEquipmentKind.Shield)
        {
            return false;
        }

        loadoutRuntime.RemoveEquipment(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = string.Empty;
        instance.worldState = CombatEquipmentWorldState.Loose;
        detached = instance.Clone();
        return true;
    }

    public IReadOnlyList<CombatEquipmentInstance> ConfiscateAllFromCharacter(
        string characterId)
    {
        return loadoutRuntime.ConfiscateAll(characterId);
    }

    public void HandleCharacterDeath(string characterId)
    {
        loadoutRuntime.HandleCharacterDeath(characterId);
    }

    public bool TryRestoreDurability(string instanceId, float durabilityRatio)
    {
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is not CombatEquipmentKind.Armor
                and not CombatEquipmentKind.Shield)
        {
            return false;
        }

        instance.durabilityRatio = Mathf.Clamp01(
            Mathf.Max(instance.durabilityRatio, durabilityRatio));
        PersistPhysicalState(instance);
        return true;
    }

    public EquipmentModuleInstance CreateExpeditionModule(
        string definitionId,
        int grade,
        Vector2Int deliveryPosition,
        WorldItemStackState worldState = WorldItemStackState.Loose,
        string destinationId = "",
        bool identified = false)
    {
        return moduleRuntime.CreateExpeditionModule(
            definitionId,
            grade,
            deliveryPosition,
            worldState,
            destinationId,
            identified);
    }

    public bool TryAppraiseModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        return moduleRuntime.TryAppraise(moduleInstanceId, facility, out failure);
    }

    public bool TryRestoreModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        return moduleRuntime.TryRestore(moduleInstanceId, facility, out failure);
    }

    public bool TryTuneModule(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        return moduleRuntime.TryTune(moduleInstanceId, facility, out failure);
    }

    public bool TryInstallModule(
        string equipmentInstanceId,
        string moduleInstanceId,
        int slotIndex,
        BuildableObject facility,
        out DomainFailure failure)
    {
        return moduleRuntime.TryInstall(
            equipmentInstanceId,
            moduleInstanceId,
            slotIndex,
            facility,
            out failure);
    }

    public bool TryRemoveModule(
        string equipmentInstanceId,
        int slotIndex,
        BuildableObject facility,
        out EquipmentModuleInstance removed,
        out DomainFailure failure)
    {
        return moduleRuntime.TryRemove(
            equipmentInstanceId,
            slotIndex,
            facility,
            out removed,
            out failure);
    }

    public bool TryQueueHistoryTransfer(
        string sourceEquipmentInstanceId,
        string targetEquipmentInstanceId,
        string lineageSealStackId,
        BuildableObject facility,
        out EquipmentHistoryTransferOrder order,
        out DomainFailure failure)
    {
        return historyRuntime.TryQueue(
            sourceEquipmentInstanceId,
            targetEquipmentInstanceId,
            lineageSealStackId,
            facility,
            out order,
            out failure);
    }

    public bool ApplyHistoryTransferWork(
        string orderId,
        float work,
        BuildableObject facility,
        out bool completed,
        out DomainFailure failure)
    {
        return historyRuntime.ApplyWork(
            orderId,
            work,
            facility,
            out completed,
            out failure);
    }

    public bool TryClaimRegionLineageSeal(string regionId)
    {
        return historyRuntime.TryClaimRegionSeal(regionId);
    }

    public float GetCarriedWeight(string characterId)
    {
        return loadoutRuntime.GetCarriedWeight(characterId);
    }

    public DungeonCombatEquipmentSaveData Capture()
    {
        return new DungeonCombatEquipmentSaveData
        {
            loadouts = loadoutRuntime.Capture().ToList(),
            craftOrders = crafting.CaptureOrders().ToList(),
            craftMaterialPolicies = crafting.CapturePolicies().ToList(),
            historyTransferOrders = historyRuntime.CaptureOrders().ToList(),
            claimedLineageSealRegionIds =
                historyRuntime.CaptureClaimedRegionIds().ToList()
        };
    }

    public CombatEquipmentRestoreCandidate BuildRestoreCandidate(
        DungeonCombatEquipmentSaveData saveData)
    {
        return CombatEquipmentRestoreBuilder.Build(
            saveData,
            catalog,
            crafting);
    }

    public void PublishRestoreCandidate(
        CombatEquipmentRestoreCandidate candidate)
    {
        stateStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
    }

}
