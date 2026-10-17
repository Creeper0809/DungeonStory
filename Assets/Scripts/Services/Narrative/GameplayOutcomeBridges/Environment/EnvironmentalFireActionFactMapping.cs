using System;
using DungeonStory.Environment;

public static class EnvironmentalFireActionFactMapping
{
    public static EnvironmentalFireIgnitionOutcomeReceipt Capture(
        EnvironmentalFireIgnitedFact fact)
    {
        EnvironmentalFireCauseSaveRecord snapshot = fact.Snapshot();
        var result = new EnvironmentalFireIgnitionResult(
            (EnvironmentalFireIgnitionDisposition)snapshot.disposition,
            snapshot.fireId,
            snapshot.reason);
        return EnvironmentalFireOutcomeReceiptFactory.CreateFireIgnition(
            fact.ToRequest(),
            result,
            snapshot.targetDisplayName,
            new CoreGridCell(snapshot.positionX, snapshot.positionY),
            snapshot.absoluteDay,
            snapshot.outcomeRevision,
            snapshot.appliedIntensity,
            snapshot.hasLocation);
    }

    public static EnvironmentalFireSuppressionOutcomeReceipt Capture(
        EnvironmentalFireSuppressedFact fact)
    {
        EnvironmentalFireSuppressionSaveRecord snapshot = fact.Snapshot();
        var result = new EnvironmentalFireSuppressionResult(
            (EnvironmentalFireSuppressionDisposition)snapshot.resultDisposition,
            snapshot.intensityBefore,
            snapshot.intensityAfter,
            snapshot.waterCommittedQuantity,
            snapshot.waterCommitId,
            snapshot.resultReason);
        return EnvironmentalFireOutcomeReceiptFactory.CreateFireSuppression(
            fact.ToCommand(),
            result,
            snapshot.workerDisplayName,
            new CoreGridCell(snapshot.positionX, snapshot.positionY),
            snapshot.absoluteDay,
            snapshot.outcomeRevision,
            snapshot.targetId);
    }

    public static WorkCompletionIdentityDeliveryRequest CreateRequest(
        EnvironmentalFireSuppressionCompletedFact fact)
    {
        EnvironmentalFireSuppressionSaveRecord snapshot = fact.Snapshot();
        if (!string.Equals(
                snapshot.publicationKey,
                EnvironmentalFireCompletionIdentity.ChildKey(snapshot).ToString(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Fire completion requires its exact suppression publication.",
                nameof(fact));
        }
        return EnvironmentalFireCompletionIdentity.CreateRequest(snapshot);
    }
}
