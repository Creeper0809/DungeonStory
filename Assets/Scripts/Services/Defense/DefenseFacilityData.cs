using System;
using UnityEngine;

[Serializable]
public sealed class DefenseFacilityGrowthData
{
    [Min(0)] public int capacityLevel;
    [Min(0)] public int resetSpeedLevel;
    [Min(0)] public int effectStrengthLevel;
    [Min(0)] public int detectionRangeLevel;
    [Min(0)] public int identificationLevel;
    [Min(0)] public int outageResistanceLevel;
}

[Serializable]
public class DefenseFacilityData
{
    public bool enabled;
    public DefenseAttackConcept concept;
    public DefenseTriggerTiming triggerTimings;
    public DefenseTargetRule targetRule;
    [Min(0f)] public float cooldownSeconds;
    [Min(0f)] public float periodicIntervalSeconds;
    [Min(0)] public int range;
    [Min(1)] public int star = 1;
    public string combatLogText;
    public DefenseEffectSO[] effectAssets = Array.Empty<DefenseEffectSO>();
    [Header("Physical operation")]
    public DefenseSupplyKind supplyKind;
    public string supplyItemId = string.Empty;
    public StockCategory supplyCategory = StockCategory.General;
    [Min(0)] public int supplyCapacity;
    [Min(0)] public int supplyPerActivation = 1;
    [Min(0)] public int initialSupply = 1;
    public bool requiresPower;
    [Min(0f)] public float powerDemand;
    [Min(0f)] public float conditionLossPerActivation = 1f;
    [Range(0f, 1f)] public float baseJamChance;
    [Range(0f, 1f)] public float baseMisfireChance;
    public string facilityFamilyId = "defense:general";
    public string[] affinityTags = Array.Empty<string>();
    public DefenseFacilityGrowthData growth = new DefenseFacilityGrowthData();

    public bool IsDefenseFacility => enabled && concept != DefenseAttackConcept.None;
    public bool UsesPhysicalSupply =>
        supplyKind != DefenseSupplyKind.None
        && supplyKind != DefenseSupplyKind.ElectricalCharge
        && supplyKind != DefenseSupplyKind.Treasury
        && supplyCapacity > 0;

    public bool SupportsTrigger(DefenseTriggerTiming timing)
    {
        return timing != DefenseTriggerTiming.None && (triggerTimings & timing) != 0;
    }
}
