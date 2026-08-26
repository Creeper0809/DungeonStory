using System;
using System.Collections.Generic;
using UnityEngine;

public enum EquipmentEra { Starting, Medieval, EarlyIndustrial, MatureIndustrial, RuneAbyssal }
public enum EquipmentSlotProfile { None = 0, StandardOne = 1, GrowthThree = 3, GrowthFour = 4 }
public enum EquipmentLineageKind { Weapon, Armor, Shield }
public enum EquipmentExpeditionRewardKind
{
    RegionBoss = 0,
    EliteCombat = 1,
    FacilityRaid = 2
}

public static class EquipmentExpeditionRewardSourceIds
{
    public const string RegionBossModule =
        "expedition-reward:region-boss:equipment-module";
    public const string EliteCombatModule =
        "expedition-reward:elite-combat:equipment-module";
    public const string FacilityRaidModule =
        "expedition-reward:facility-raid:equipment-module";

    public static string ForModule(EquipmentExpeditionRewardKind kind) =>
        kind switch
        {
            EquipmentExpeditionRewardKind.RegionBoss => RegionBossModule,
            EquipmentExpeditionRewardKind.EliteCombat => EliteCombatModule,
            EquipmentExpeditionRewardKind.FacilityRaid => FacilityRaidModule,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}

public enum EquipmentModuleProcessState
{
    Unidentified,
    IdentifiedDamaged,
    Restored,
    Tuned,
    Installed,
    Lost
}

public enum EquipmentModuleAppraisalCommitPhase
{
    None = 0,
    IntentRecorded = 1,
    OutcomePublished = 2
}

[Serializable]
public sealed class EquipmentModuleAppraisalCommitSaveData
{
    public int phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string moduleInstanceId = string.Empty;
    public string destinationId = string.Empty;
    public string couponStackId = string.Empty;
    public string couponItemId = string.Empty;
    public int quantity;
    public bool moduleIdentifiedBefore;
    public bool moduleIdentifiedAfter;
    public EquipmentModuleProcessState moduleStateBefore;
    public EquipmentModuleProcessState moduleStateAfter;
    public string gaugeStackId = string.Empty;
    public string gaugeItemId = string.Empty;
    public float gaugeDurabilityBefore;
    public float gaugeDurabilityAfter;
    public string lensStackId = string.Empty;
    public string lensItemId = string.Empty;
    public float lensDurabilityBefore;
    public float lensDurabilityAfter;
    public List<string> sourceStackIds = new();
    public long inputMassGrams;
    public string commitId = string.Empty;

    public EquipmentModuleAppraisalCommitSaveData Clone() => new()
    {
        phase = phase,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        reasonCode = reasonCode ?? string.Empty,
        moduleInstanceId = moduleInstanceId ?? string.Empty,
        destinationId = destinationId ?? string.Empty,
        couponStackId = couponStackId ?? string.Empty,
        couponItemId = couponItemId ?? string.Empty,
        quantity = quantity,
        moduleIdentifiedBefore = moduleIdentifiedBefore,
        moduleIdentifiedAfter = moduleIdentifiedAfter,
        moduleStateBefore = moduleStateBefore,
        moduleStateAfter = moduleStateAfter,
        gaugeStackId = gaugeStackId ?? string.Empty,
        gaugeItemId = gaugeItemId ?? string.Empty,
        gaugeDurabilityBefore = gaugeDurabilityBefore,
        gaugeDurabilityAfter = gaugeDurabilityAfter,
        lensStackId = lensStackId ?? string.Empty,
        lensItemId = lensItemId ?? string.Empty,
        lensDurabilityBefore = lensDurabilityBefore,
        lensDurabilityAfter = lensDurabilityAfter,
        sourceStackIds = new List<string>(sourceStackIds ?? new List<string>()),
        inputMassGrams = inputMassGrams,
        commitId = commitId ?? string.Empty
    };
}

public static class EquipmentProgressionItemIds
{
    public const string LineageSeal = "item:lineage-seal";
}

public static class EquipmentProgressionWorkstationTags
{
    public const string Appraisal = "workstation:v3:appraisal";
    public const string Restoration = "workstation:v3:restoration";
    public const string PrecisionFitting = "workstation:v3:precision-fitting";
    public const string RuneTuning = "workstation:v3:rune-tuning";
    public const string LineageArchive = "workstation:v3:lineage-archive";

    public static bool IsModuleProcess(string workstationTag)
    {
        string normalized = workstationTag?.Trim() ?? string.Empty;
        return normalized == Appraisal
            || normalized == Restoration
            || normalized == PrecisionFitting
            || normalized == RuneTuning;
    }
}

[Serializable]
public sealed class EquipmentModuleSlotState
{
    [Min(0)] public int slotIndex;
    public string moduleInstanceId = string.Empty;
    public EquipmentModuleSlotState Clone() => new EquipmentModuleSlotState
    {
        slotIndex = Mathf.Max(0, slotIndex),
        moduleInstanceId = moduleInstanceId?.Trim() ?? string.Empty
    };
}

[Serializable]
public sealed class EquipmentModuleInstance
{
    public string instanceId = string.Empty;
    public string definitionId = string.Empty;
    [Range(1, 4)] public int grade = 1;
    [Range(0f, 1f)] public float condition = 1f;
    public bool identified;
    public bool runeTuned;
    public EquipmentModuleProcessState state;
    public string sourceStackId = string.Empty;
    public string attachedEquipmentInstanceId = string.Empty;
    public int nextAppraisalOperationSequence = 1;
    public EquipmentModuleAppraisalCommitSaveData pendingAppraisal = new();
    public EquipmentModuleInstance Clone() => new EquipmentModuleInstance
    {
        instanceId = instanceId?.Trim() ?? string.Empty,
        definitionId = definitionId?.Trim() ?? string.Empty,
        grade = Mathf.Clamp(grade, 1, 4),
        condition = Mathf.Clamp01(condition),
        identified = identified,
        runeTuned = runeTuned,
        state = state,
        sourceStackId = sourceStackId?.Trim() ?? string.Empty,
        attachedEquipmentInstanceId = attachedEquipmentInstanceId?.Trim() ?? string.Empty,
        nextAppraisalOperationSequence = nextAppraisalOperationSequence,
        pendingAppraisal = pendingAppraisal?.Clone()
            ?? new EquipmentModuleAppraisalCommitSaveData()
    };
}

[Serializable]
public sealed class EquipmentHistoryTransferOrder
{
    public string orderId = string.Empty;
    public string sourceEquipmentInstanceId = string.Empty;
    public string targetEquipmentInstanceId = string.Empty;
    public string lineageSealStackId = string.Empty;
    public string facilityPersistentId = string.Empty;
    public string destinationId = string.Empty;
    [Min(0f)] public float requiredWork = 120f;
    [Min(0f)] public float completedWork;
    public bool completed;
    public EquipmentHistoryTransferOrder Clone() => new EquipmentHistoryTransferOrder
    {
        orderId = orderId?.Trim() ?? string.Empty,
        sourceEquipmentInstanceId = sourceEquipmentInstanceId?.Trim() ?? string.Empty,
        targetEquipmentInstanceId = targetEquipmentInstanceId?.Trim() ?? string.Empty,
        lineageSealStackId = lineageSealStackId?.Trim() ?? string.Empty,
        facilityPersistentId = facilityPersistentId?.Trim() ?? string.Empty,
        destinationId = destinationId?.Trim() ?? string.Empty,
        requiredWork = Mathf.Max(1f, requiredWork),
        completedWork = Mathf.Clamp(completedWork, 0f, Mathf.Max(1f, requiredWork)),
        completed = completed
    };
}
