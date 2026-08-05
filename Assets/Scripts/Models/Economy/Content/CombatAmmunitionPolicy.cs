using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICombatAmmunitionInventory
{
    int CountItem(string itemId);
    bool TryConsumeItem(string itemId, int quantity);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CombatAmmunitionPolicy
{
    public static IReadOnlyList<ItemDefinitionId> Normalize(
        IEnumerable<string> authoredItemIds)
    {
        if (authoredItemIds == null)
        {
            return Array.Empty<ItemDefinitionId>();
        }

        List<ItemDefinitionId> result = new List<ItemDefinitionId>();
        HashSet<ItemDefinitionId> seen = new HashSet<ItemDefinitionId>();
        foreach (string authoredItemId in authoredItemIds)
        {
            ItemDefinitionId itemId = (ItemDefinitionId)authoredItemId;
            if (itemId.IsValid && seen.Add(itemId))
            {
                result.Add(itemId);
            }
        }

        return result.Count == 0
            ? Array.Empty<ItemDefinitionId>()
            : result.AsReadOnly();
    }

    public static ItemDefinitionId GetPreferred(
        IReadOnlyList<ItemDefinitionId> compatibleItemIds)
    {
        return compatibleItemIds != null && compatibleItemIds.Count > 0
            ? compatibleItemIds[0]
            : default;
    }

    public static int CountAvailable(
        CombatWeaponSO weapon,
        ICombatAmmunitionInventory inventory)
    {
        if (weapon == null || inventory == null)
        {
            return 0;
        }

        int available = 0;
        foreach (ItemDefinitionId itemId in weapon.CompatibleAmmunitionItemIds)
        {
            available += inventory.CountItem(itemId.Value);
        }

        return available;
    }

    public static bool TrySelectAvailable(
        CombatWeaponSO weapon,
        ICombatAmmunitionInventory inventory,
        out ItemDefinitionId selectedItemId)
    {
        selectedItemId = default;
        if (weapon == null || inventory == null)
        {
            return false;
        }

        foreach (ItemDefinitionId itemId in weapon.CompatibleAmmunitionItemIds)
        {
            if (inventory.CountItem(itemId.Value) > 0)
            {
                selectedItemId = itemId;
                return true;
            }
        }

        return false;
    }

    public static bool TryConsumeSelected(
        ICombatAmmunitionInventory inventory,
        ItemDefinitionId selectedItemId,
        int quantity)
    {
        return inventory != null
            && selectedItemId.IsValid
            && quantity > 0
            && inventory.TryConsumeItem(selectedItemId.Value, quantity);
    }
}
