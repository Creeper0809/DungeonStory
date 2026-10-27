using System;
using System.Collections.Generic;
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
    [ContentReference(ContentReferenceKind.Item)]
    public string supplyItemId = string.Empty;
    public StockCategory supplyCategory = StockCategory.General;
    [Min(0)] public int supplyCapacity;
    [Min(0)] public int supplyPerActivation = 1;
    [Min(0)] public int initialSupply = 1;
    public bool requiresPower;
    public bool requiresAutomaticControl;
    [Min(0f)] public float powerDemand;
    [Min(0f)] public float conditionLossPerActivation = 1f;
    [Range(0f, 1f)] public float baseJamChance;
    [Range(0f, 1f)] public float baseMisfireChance;
    [ContentReference(ContentReferenceKind.DefenseFamily)]
    public string facilityFamilyId = DefenseFacilityFamilyIds.General;
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

    public IReadOnlyList<string> ValidateDefinition()
    {
        if (!enabled || DefenseFacilityFamilyIds.IsKnown(facilityFamilyId))
        {
            return Array.Empty<string>();
        }

        return new[]
        {
            $"Enabled defense facility has unknown facility family '{facilityFamilyId ?? string.Empty}'."
        };
    }
}
