using System;
using System.Collections.Generic;

public enum V20ContentResolutionRecoveryPhase
{
    Prepared = 0,
    ItemCostsCommitted = 1,
    ItemGrantsCommitted = 2,
    TypedEffectsApplied = 3,
    OutcomesCommitted = 4
}

[Serializable]
public sealed class V20ResolvedEventRecoverySaveData
{
    public string definitionId = string.Empty;
    public string resolutionId = string.Empty;
    public List<V20ContentEffect> effects = new();
    public List<string> participantCharacterIds = new();
    public string contextFactionId = string.Empty;
    public string instanceId = string.Empty;
    public string displayName = string.Empty;
    public ExperienceEventRiskTier riskTier;
    public string riskReason = string.Empty;
    public string terminalActionLabel = string.Empty;
    public string terminalOutcomeNarrative = string.Empty;
    public List<V20ContentEffectKind> terminalEffectKinds = new();
    public string observedCause = string.Empty;
    public string occurrenceInstanceId = string.Empty;
    public List<V20EffectApplicationEvidence> applications = new();
}

public enum V20EffectApplicationStatus
{
    Applied = 0,
    Skipped = 1,
    Clamped = 2,
    NoOp = 3
}

[Serializable]
public sealed class V20EffectApplicationEvidence
{
    public int projectionVersion;
    public string targetKindId = string.Empty;
    public string targetRoleId = string.Empty;
    public string metricUnitId = string.Empty;
    public int effectIndex;
    public string targetId = string.Empty;
    public V20EffectApplicationStatus status;
    public float requestedAmount;
    public float appliedAmount;
    public string beforeValue = string.Empty;
    public string afterValue = string.Empty;
    public string semantic = string.Empty;
    public string childCommitId = string.Empty;

    public V20EffectApplicationEvidence Clone() => new()
    {
        projectionVersion = projectionVersion,
        targetKindId = targetKindId ?? string.Empty,
        targetRoleId = targetRoleId ?? string.Empty,
        metricUnitId = metricUnitId ?? string.Empty,
        effectIndex = effectIndex,
        targetId = targetId ?? string.Empty,
        status = status,
        requestedAmount = requestedAmount,
        appliedAmount = appliedAmount,
        beforeValue = beforeValue ?? string.Empty,
        afterValue = afterValue ?? string.Empty,
        semantic = semantic ?? string.Empty,
        childCommitId = childCommitId ?? string.Empty
    };

    public bool HasValidProjection => projectionVersion == 0
        || projectionVersion == 1
            && GameplayOutcomeStableIdSyntax.IsValid(targetKindId)
            && GameplayOutcomeStableIdSyntax.IsValid(targetRoleId)
            && GameplayOutcomeStableIdSyntax.IsValid(metricUnitId);
}

[Serializable]
public sealed class V20ContentResolutionRecoverySaveData
{
    public string actionId = string.Empty;
    public int absoluteDay;
    public ContentResolutionRequestKind requestKind;
    public V20ContentResolutionRecoveryPhase phase;
    public int grantDropoffX;
    public int grantDropoffY;
    public int childCommitEvidenceVersion;
    public string moneyTransactionId = string.Empty;
    public string itemConsumptionOperationId = string.Empty;
    public string itemGrantBatchCommitId = string.Empty;
    public string itemConsumptionCommitId = string.Empty;
    public string itemGrantReceiptId = string.Empty;
    public int durableEquipmentEvidenceVersion;
    public string durableEquipmentStackId = string.Empty;
    public string durableEquipmentItemId = string.Empty;
    public string durableEquipmentContext = string.Empty;
    public int outcomeEvidenceVersion;
    public string stateBeforeFingerprint = string.Empty;
    public string stateAfterFingerprint = string.Empty;
    public List<V20ResolvedEventRecoverySaveData> resolutions = new();
}

