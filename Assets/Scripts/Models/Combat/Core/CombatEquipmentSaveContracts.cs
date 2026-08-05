using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonCombatEquipmentSaveData
{
    public List<CharacterCombatLoadoutState> loadouts = new List<CharacterCombatLoadoutState>();
    public List<CombatEquipmentCraftOrderSaveData> craftOrders =
        new List<CombatEquipmentCraftOrderSaveData>();
    public List<CombatEquipmentCraftMaterialPolicySaveData> craftMaterialPolicies =
        new List<CombatEquipmentCraftMaterialPolicySaveData>();
    public List<EquipmentHistoryTransferOrder> historyTransferOrders =
        new List<EquipmentHistoryTransferOrder>();
    public List<string> claimedLineageSealRegionIds = new List<string>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatEquipmentCraftMaterialPolicySaveData
{
    public string facilityKey = string.Empty;
    public string definitionId = string.Empty;
    public List<string> priorityMaterialIds = new List<string>();
    public List<string> allowedMaterialIds = new List<string>();

    public CombatEquipmentCraftMaterialPolicySaveData Clone()
    {
        return new CombatEquipmentCraftMaterialPolicySaveData
        {
            facilityKey = facilityKey ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            priorityMaterialIds = priorityMaterialIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>(),
            allowedMaterialIds = allowedMaterialIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>()
        };
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatEquipmentCraftOrderSaveData
{
    public string orderId = string.Empty;
    public string definitionId = string.Empty;
    public string materialId = string.Empty;
    public float requiredWork;
    public float completedWork;
    public bool materialsReady;
    public string materialDestinationId = string.Empty;
    public int destinationX;
    public int destinationY;

    public float RemainingWork => Mathf.Max(0f, requiredWork - completedWork);

    public CombatEquipmentCraftOrderSaveData Clone()
    {
        return new CombatEquipmentCraftOrderSaveData
        {
            orderId = orderId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            materialId = materialId ?? string.Empty,
            requiredWork = Mathf.Max(0.1f, requiredWork),
            completedWork = Mathf.Clamp(completedWork, 0f, Mathf.Max(0.1f, requiredWork)),
            materialsReady = materialsReady,
            materialDestinationId = materialDestinationId ?? string.Empty,
            destinationX = destinationX,
            destinationY = destinationY
        };
    }
}
