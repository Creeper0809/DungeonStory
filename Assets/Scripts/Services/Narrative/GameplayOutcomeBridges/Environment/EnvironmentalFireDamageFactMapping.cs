using System;
using DungeonStory.Environment;

public static class EnvironmentalFireDamageFactMapping
{
    public static GameplayResultKey Key(EnvironmentalFireDamageRecord value) => new(
        EnvironmentOutcomeIds.FireDamageProducer, new GameplayOperationId(value.operationId), value.ownerRevision, 0);

    public static OutcomeWriteRequirements Reserve(EnvironmentalFireDamageFact fact, long worldEpoch)
    {
        var value = fact.Snapshot();
        return new OutcomeWriteRequirements(Key(value), EnvironmentOutcomeIds.FireDamage, value.absoluteDay,
            GameplayOutcomeStatus.Failed, worldEpoch, value.ownerRevision,
            participantCount: 2, metricCount: 3, subjectCount: 2, tagCount: 1, anchorCount: 0, provenanceCount: 1, factCount: 3);
    }
    public static EnvironmentalFireDamageOutcomeReceipt Capture(EnvironmentalFireDamageFact fact)
    {
        var value = fact.Snapshot();
        if (value.phase != EnvironmentalFireDamagePhase.Applied)
            throw new InvalidOperationException("Only confirmed applied fire damage can be observed.");
        var command = new EnvironmentalFireDamageCommand(value.operationId, value.fireId,
            new EnvironmentalFireTargetRef((EnvironmentalFireTargetKind)value.targetKind, value.targetId),
            new UnityEngine.Vector2Int(value.positionX, value.positionY), value.intensity, value.requestedDamage);
        return EnvironmentalFireOutcomeReceiptFactory.CreateFireDamage(command,
            new EnvironmentalFireDamageResult(true, value.appliedDamage, value.targetRemainsCombustible),
            value.targetDisplayName, value.absoluteDay, value.ownerRevision);
    }
}
