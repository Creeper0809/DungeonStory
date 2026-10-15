using System;

/// <summary>Native log identity, display snapshots and bounded reservations belong to observation.</summary>
public static class IdleSocialInteractionFactMapping
{
    public static IdleSocialInteractionOutcomeReceipt Capture(IdleSocialInteractionResolvedFact fact)
    {
        if (fact == null || fact.IsProposal) throw new ArgumentException("idle-social-observation-requires-applied-fact");
        var value = fact.Snapshot();
        return new IdleSocialInteractionOutcomeReceipt(value.operationId,
            new CharacterId(value.actorId), MigratedProducerOutcomeSnapshots.Name(value.actorId, value.actorName),
            new CharacterId(value.targetId), MigratedProducerOutcomeSnapshots.Name(value.targetId, value.targetName),
            value.behaviorId, value.reasonId, value.actorRelationshipBefore, value.actorRelationshipAfter,
            value.targetRelationshipBefore, value.targetRelationshipAfter, value.moodBefore, value.moodAfter, value.absoluteDay);
    }
    public static OutcomeWriteRequirements ReserveShape(IdleSocialInteractionResolvedFact fact, long epoch)
    {
        if (fact?.IsProposal != true) throw new ArgumentException("idle-social-observation-proposal-required");
        return IdleSocialInteractionOutcomeAdapter.CreateRequirements(new GameplayResultKey(
            IdleSocialInteractionOutcomeIds.ProducerId, new GameplayOperationId(fact.OperationId), 0L, 0), fact.AbsoluteDay, epoch);
    }
}
