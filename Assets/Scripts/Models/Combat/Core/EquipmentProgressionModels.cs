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
        attachedEquipmentInstanceId = attachedEquipmentInstanceId?.Trim() ?? string.Empty
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
