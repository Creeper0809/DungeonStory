using System;
using System.Collections.Generic;
using UnityEngine;

internal static class ExtremeTraitRuleIds
{
    internal const string LastStandRuleId = "extreme:last-stand";
    internal const string ForbiddenLeapRuleId = "extreme:forbidden-leap";
    internal const string MiracleSurgeryRuleId = "extreme:miracle-surgery";
    internal const string GoldenHarvestRuleId = "extreme:golden-harvest";
    internal const string ProductionLimitBreakRuleId = "extreme:production-limit-break";
    internal const string ArcaneOverchargeRuleId = "extreme:arcane-overcharge";
}

public enum ExtremeRiskOutcome
{
    Normal,
    Breakthrough,
    Setback,
    Miracle,
    Complication,
    Jackpot,
    Loss
}

public readonly struct ExtremeRiskResolution
{
    public ExtremeRiskResolution(
        ExtremeRiskOutcome outcome,
        float primaryMultiplier,
        float secondaryMultiplier,
        float progressDelta,
        ulong fixedRollHash)
    {
        Outcome = outcome;
        PrimaryMultiplier = primaryMultiplier;
        SecondaryMultiplier = secondaryMultiplier;
        ProgressDelta = progressDelta;
        FixedRollHash = fixedRollHash;
    }

    public ExtremeRiskOutcome Outcome { get; }
    public float PrimaryMultiplier { get; }
    public float SecondaryMultiplier { get; }
    public float ProgressDelta { get; }
    public ulong FixedRollHash { get; }
}

public readonly struct ArcaneOverchargeActivation
{
    public ArcaneOverchargeActivation(
        float selfDamageFraction,
        float equipmentDurabilityFraction)
    {
        SelfDamageFraction = selfDamageFraction;
        EquipmentDurabilityFraction = equipmentDurabilityFraction;
    }

    public float SelfDamageFraction { get; }
    public float EquipmentDurabilityFraction { get; }
}

[Serializable]
public sealed class LastStandRuntimeState
{
    public string encounterId = string.Empty;
    public bool used;
    public bool active;
    public float aftermathUntilSeconds;
}

[Serializable]
public sealed class ForbiddenResearchLeapRuntimeState
{
    public List<string> usedProjectIds = new();
    public float aftermathUntilSeconds;
}

[Serializable]
public sealed class MiracleSurgeryRuntimeState
{
    public List<string> usedSurgeryIds = new();
    public float aftermathUntilSeconds;
}

[Serializable]
public sealed class GoldenHarvestRuntimeState
{
    public string fieldId = string.Empty;
    public int attemptIndex;
    public float resolveAfterSeconds;
    public bool pending;
    public string preparedOperationId = string.Empty;
    public string preparedFieldId = string.Empty;
    public ExtremeRiskOutcome preparedOutcome;
    public float preparedPrimaryMultiplier;
    public float preparedSecondaryMultiplier;
    public float preparedProgressDelta;
    public ulong preparedRollHash;
    public string preparedFingerprint = string.Empty;
    public bool preparedCommitted;
    public bool preparedOwnerDead;
}

public readonly struct GoldenHarvestPreparedResolution
{
    public GoldenHarvestPreparedResolution(
        string operationId,
        string characterId,
        string traitDefinitionId,
        string fieldId,
        string fingerprint,
        bool committed,
        ExtremeRiskResolution resolution)
    {
        OperationId = operationId ?? string.Empty;
        CharacterId = characterId ?? string.Empty;
        TraitDefinitionId = traitDefinitionId ?? string.Empty;
        FieldId = fieldId ?? string.Empty;
        Fingerprint = fingerprint ?? string.Empty;
        Committed = committed;
        Resolution = resolution;
    }

    public string OperationId { get; }
    public string CharacterId { get; }
    public string TraitDefinitionId { get; }
    public string FieldId { get; }
    public string Fingerprint { get; }
    public bool Committed { get; }
    public ExtremeRiskResolution Resolution { get; }
}

public interface IGoldenHarvestPreparedResolutionQuery
{
    IReadOnlyList<GoldenHarvestPreparedResolution>
        CapturePreparedGoldenHarvests();
}

[Serializable]
public sealed class ProductionLimitBreakRuntimeState
{
    public string batchId = string.Empty;
    public bool active;
    public float activeLeaseUntilSeconds;
    public float aftermathUntilSeconds;
}

[Serializable]
public sealed class ArcaneOverchargeRuntimeState
{
    public string activationId = string.Empty;
    public float activeUntilSeconds;
    public float aftermathUntilSeconds;
    public string equipmentInstanceId = string.Empty;
    public float selfDamageFraction;
    public float equipmentDurabilityFraction;
    public bool selfDamageApplied;
    public float healthBefore;
    public float healthAfter;
    public bool equipmentDurabilityApplied;
    public float durabilityBefore;
    public float durabilityAfter;
    public bool effectsCompleted;
    public int failureCount;
    public string lastFailureCode = string.Empty;
}

public readonly struct ArcaneOverchargeOperationSnapshot
{
    public ArcaneOverchargeOperationSnapshot(
        string activationId,
        string equipmentInstanceId,
        float selfDamageFraction,
        float equipmentDurabilityFraction,
        bool selfDamageApplied,
        float healthBefore,
        float healthAfter,
        bool equipmentDurabilityApplied,
        float durabilityBefore,
        float durabilityAfter,
        bool effectsCompleted,
        int failureCount,
        string lastFailureCode)
    {
        ActivationId = activationId ?? string.Empty;
        EquipmentInstanceId = equipmentInstanceId ?? string.Empty;
        SelfDamageFraction = selfDamageFraction;
        EquipmentDurabilityFraction = equipmentDurabilityFraction;
        SelfDamageApplied = selfDamageApplied;
        HealthBefore = healthBefore;
        HealthAfter = healthAfter;
        EquipmentDurabilityApplied = equipmentDurabilityApplied;
        DurabilityBefore = durabilityBefore;
        DurabilityAfter = durabilityAfter;
        EffectsCompleted = effectsCompleted;
        FailureCount = Mathf.Max(0, failureCount);
        LastFailureCode = lastFailureCode ?? string.Empty;
    }

    public string ActivationId { get; }
    public string EquipmentInstanceId { get; }
    public float SelfDamageFraction { get; }
    public float EquipmentDurabilityFraction { get; }
    public bool SelfDamageApplied { get; }
    public float HealthBefore { get; }
    public float HealthAfter { get; }
    public bool EquipmentDurabilityApplied { get; }
    public float DurabilityBefore { get; }
    public float DurabilityAfter { get; }
    public bool EffectsCompleted { get; }
    public int FailureCount { get; }
    public string LastFailureCode { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(ActivationId)
        && SelfDamageFraction > 0f
        && EquipmentDurabilityFraction > 0f;
}
