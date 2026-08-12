using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonCombatEquipmentSaveData
{
    public int nextCraftSequence;
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
public sealed class CombatCraftRecoveryOutputSaveData
{
    public string itemId = string.Empty;
    public int amount;

    public CombatCraftRecoveryOutputSaveData Clone() => new()
    {
        itemId = itemId ?? string.Empty,
        amount = Mathf.Max(0, amount)
    };
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
    public WorkerSelectionPolicySaveData workerPolicy =
        WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.BestExpectedQuality);
    public List<CraftContributionSaveData> contributions = new();
    public CraftQualityRollSaveData qualityRoll;
    public CraftsmanshipQualityTier minimumQuality =
        CraftsmanshipQualityTier.Awful;
    public float facilityQualityBonus;
    public RejectedOutputDisposition rejectedDisposition =
        RejectedOutputDisposition.AutoDismantle;
    public QualityRepeatLimitMode repeatLimitMode =
        QualityRepeatLimitMode.SafeLimits;
    public int maximumAttempts = 10;
    public float workBudget;
    public float consumedWork;
    public int qualityAttemptIndex;
    public int requiredAcceptedCount = 1;
    public int acceptedCount;
    public QualityTargetPipelineStage qualityStage =
        QualityTargetPipelineStage.WaitingForMaterials;
    public bool dismantlingRejectedOutput;
    public bool rejectedOutputConsumed;
    public string rejectedInstanceId = string.Empty;
    public string rejectedStackId = string.Empty;
    public float craftWorkPerAttempt;
    public List<CombatCraftRecoveryOutputSaveData> recoveryOutputs = new();
    public List<int> spawnedRecoveryAmounts = new();

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
            destinationY = destinationY,
            workerPolicy = workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone(
                    WorkerCandidateSortMode.BestExpectedQuality),
            contributions = contributions?
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList() ?? new List<CraftContributionSaveData>(),
            qualityRoll = qualityRoll?.Clone(),
            minimumQuality = minimumQuality,
            facilityQualityBonus = facilityQualityBonus,
            rejectedDisposition = rejectedDisposition,
            repeatLimitMode = repeatLimitMode,
            maximumAttempts = Mathf.Max(1, maximumAttempts),
            workBudget = Mathf.Max(0f, workBudget),
            consumedWork = Mathf.Max(0f, consumedWork),
            qualityAttemptIndex = Mathf.Max(0, qualityAttemptIndex),
            requiredAcceptedCount = Mathf.Max(1, requiredAcceptedCount),
            acceptedCount = Mathf.Max(0, acceptedCount),
            qualityStage = qualityStage,
            dismantlingRejectedOutput = dismantlingRejectedOutput,
            rejectedOutputConsumed = rejectedOutputConsumed,
            rejectedInstanceId = rejectedInstanceId ?? string.Empty,
            rejectedStackId = rejectedStackId ?? string.Empty,
            craftWorkPerAttempt = Mathf.Max(0f, craftWorkPerAttempt),
            recoveryOutputs = recoveryOutputs?
                .Where(value => value != null && value.amount > 0)
                .Select(value => value.Clone())
                .ToList() ?? new List<CombatCraftRecoveryOutputSaveData>(),
            spawnedRecoveryAmounts = spawnedRecoveryAmounts?.
                Select(value => Mathf.Max(0, value)).ToList()
                ?? new List<int>()
        };
    }
}
