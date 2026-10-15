using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

// Frozen domain effect receipt. Identity/format/importance in the native log belongs to its observer.
[Serializable]
public sealed class ContentResolutionFactData
{
    public string operationId = string.Empty;
    public int absoluteDay, resolutionIndex;
    public V20ResolvedEventRecoverySaveData resolution;
    public string moneyTransactionId = string.Empty;
    public string itemConsumptionOperationId = string.Empty;
    public string itemGrantBatchCommitId = string.Empty;
    public string durableEquipmentStackId = string.Empty;
    public string durableEquipmentItemId = string.Empty;
    public string durableEquipmentContext = string.Empty;
    public string stateBeforeFingerprint = string.Empty;
    public string stateAfterFingerprint = string.Empty;

    public ContentResolutionFactData Clone() => DungeonSaveJson.Deserialize<ContentResolutionFactData>(
        DungeonCharacterWorldSaveCodec.SerializeFields(this));
}

public sealed class ContentResolutionFact
{
    private readonly ContentResolutionFactData data;
    public ContentResolutionFact(ContentResolutionFactData source)
    {
        if (source == null || !DomainFactValueSyntax.IsText(source.operationId, 4096, false)
            || source.operationId != source.operationId.Trim() || source.absoluteDay < 1 || source.resolutionIndex < 0
            || source.resolution == null || string.IsNullOrWhiteSpace(source.resolution.definitionId)
            || string.IsNullOrWhiteSpace(source.resolution.resolutionId)
            || source.resolution.effects == null || source.resolution.participantCharacterIds == null
            || source.resolution.terminalEffectKinds == null || source.resolution.applications == null
            || source.resolution.effects.Any(value => value == null || !value.IsValid || !float.IsFinite(value.amount))
            || source.resolution.participantCharacterIds.Any(string.IsNullOrWhiteSpace)
            || source.resolution.applications.Any(value => value == null || value.effectIndex < 0
                || value.effectIndex >= source.resolution.effects.Count
                || !Enum.IsDefined(typeof(V20EffectApplicationStatus), value.status)
                || !float.IsFinite(value.requestedAmount) || !float.IsFinite(value.appliedAmount)))
            throw new InvalidOperationException("content-resolution-fact-invalid");
        foreach (string value in new[] { source.moneyTransactionId, source.itemConsumptionOperationId,
                     source.itemGrantBatchCommitId, source.durableEquipmentStackId, source.durableEquipmentItemId,
                     source.durableEquipmentContext, source.stateBeforeFingerprint, source.stateAfterFingerprint })
            if (!DomainFactValueSyntax.IsText(value, 16384, true))
                throw new InvalidOperationException("content-resolution-fact-evidence-invalid");
        data = source.Clone();
        _ = ToResult(data.resolution);
    }
    public ContentResolutionFactData Snapshot() => data.Clone();

    public static V20ResolvedEventResult ToResult(V20ResolvedEventRecoverySaveData value) => new(
        value.definitionId, value.resolutionId,
        value.effects.Select(effect => new V20ContentEffect
        { kind = effect.kind, targetId = effect.targetId, amount = effect.amount, durationDays = effect.durationDays }).ToArray(),
        value.participantCharacterIds.ToArray(), value.contextFactionId, value.instanceId, value.displayName,
        value.riskTier, value.riskReason, value.terminalActionLabel, value.terminalOutcomeNarrative,
        value.terminalEffectKinds.ToArray(), value.observedCause, value.occurrenceInstanceId,
        value.applications.Select(application => application.Clone()).ToArray());
}

// A definite delivery rejection is distinct from an unknown acknowledgement or a domain fault.
internal sealed class ContentResolutionFactRejectedException : InvalidOperationException
{
    internal ContentResolutionFactRejectedException(string detail) : base(detail) { }
}
