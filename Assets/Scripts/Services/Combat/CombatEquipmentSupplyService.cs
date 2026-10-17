using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class CombatEquipmentSupplyService
{
    private readonly ICombatEquipmentCatalog catalog;
    private readonly IItemInstanceRepository itemInstances;
    private readonly ICharacterCarryInventoryRegistry carryInventories;
    private readonly CombatEquipmentPhysicalStateWriter physicalState;
    private IDictionary<string, CombatEquipmentInstance> instances =>
        itemInstances.EquipmentInstances;

    internal CombatEquipmentSupplyService(
        ICombatEquipmentCatalog catalog,
        IItemInstanceRepository itemInstances,
        ICharacterCarryInventoryRegistry carryInventories,
        CombatEquipmentPhysicalStateWriter physicalState)
    {
        this.catalog = catalog;
        this.itemInstances = itemInstances;
        this.carryInventories = carryInventories;
        this.physicalState = physicalState;
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
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon
            || weapon.MagazineCapacity <= 0)
        {
            return false;
        }

        instance.loadedAmmunition ??= new LoadedAmmunitionBatch();
        ItemDefinitionId selectedItemId;
        if (instance.loadedAmmunition.remaining > 0)
        {
            selectedItemId = (ItemDefinitionId)
                instance.loadedAmmunition.ammunitionItemId;
            if (!selectedItemId.IsValid
                || !weapon.CompatibleAmmunitionItemIds.Contains(
                    selectedItemId)
                || inventory.CountItem(selectedItemId.Value) <= 0)
            {
                return false;
            }
        }
        else if (!CombatAmmunitionPolicy.TrySelectAvailable(
                     weapon,
                     inventory,
                     out selectedItemId))
        {
            return false;
        }

        int needed = Mathf.Max(
            0,
            weapon.MagazineCapacity - instance.loadedAmmunition.remaining);
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
        instance.loadedAmmunition.ammunitionItemId = selectedItemId.Value;
        instance.loadedAmmunition.remaining += consumedAmmo;
        physicalState.Persist(instance);
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
        return TryConsumeLoadedAmmo(instanceId, 1);
    }

    public bool TryConsumeLoadedAmmo(string instanceId, int amount)
    {
        int requested = Mathf.Max(1, amount);
        if (!instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || instance.loadedAmmunition == null
            || instance.loadedAmmunition.remaining < requested
            || string.IsNullOrWhiteSpace(
                instance.loadedAmmunition.ammunitionItemId))
        {
            return false;
        }

        instance.loadedAmmunition.remaining -= requested;
        if (instance.loadedAmmunition.remaining == 0)
        {
            instance.loadedAmmunition.Clear();
        }
        physicalState.Persist(instance);
        return true;
    }

    public bool TryConsumePower(string instanceId, float amount)
    {
        float requested = Mathf.Max(0f, amount);
        if (requested <= 0f
            || !instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || (definition.RoleFlags & CombatEquipmentRoleFlags.Powered) == 0
            || instance.powerCharge <= 0f)
        {
            return false;
        }

        instance.powerCharge = Mathf.Clamp(
            instance.powerCharge - requested,
            0f,
            100f);
        physicalState.Persist(instance);
        return true;
    }

    public bool TryRestorePower(string instanceId, float amount)
    {
        float restored = Mathf.Max(0f, amount);
        if (restored <= 0f
            || !instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || (definition.RoleFlags & CombatEquipmentRoleFlags.Powered) == 0)
        {
            return false;
        }

        instance.powerCharge = Mathf.Clamp(
            instance.powerCharge + restored,
            0f,
            100f);
        physicalState.Persist(instance);
        return true;
    }

    public bool TryLoadExternalAmmunition(
        string instanceId,
        string ammunitionItemId,
        int amount)
    {
        string normalizedAmmo = ammunitionItemId?.Trim() ?? string.Empty;
        if (amount <= 0
            || !instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || CombatEquipmentWorldStateRules.IsExternalCustody(
                instance.worldState)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon
            || weapon.MagazineCapacity <= 0
            || !weapon.CompatibleAmmunitionItemIds.Contains(
                (ItemDefinitionId)normalizedAmmo))
        {
            return false;
        }

        instance.loadedAmmunition = new LoadedAmmunitionBatch
        {
            ammunitionItemId = normalizedAmmo,
            remaining = Mathf.Min(amount, weapon.MagazineCapacity)
        };
        physicalState.Persist(instance);
        return true;
    }
}
