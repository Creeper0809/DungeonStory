using System;

public static class CropHarvestCompletionObservation
{
    public const string DeliveryPrefix = "identity-event:";
    public const string StreamPrefix = "crop-harvest:";

    public static WorkCompletionIdentityDeliveryRequest CreateRequest(CropHarvestCompletedFact fact)
    {
        if (fact == null || !fact.ParentConfirmed || string.IsNullOrEmpty(fact.ParentPublicationKey))
            throw new InvalidOperationException("Crop completion requires its confirmed terminal publication.");
        return Project(fact);
    }

    // Restore preflight also inspects harvests before their terminal is published.
    // It only projects/validates; it never emits a fact or applies identity effects.
    public static WorkCompletionIdentityDeliveryRequest CreateSavedRequest(BuildingInstanceId plotId,
        CropHarvestOutputSaveData pending, CropCycleExecutionReceiptSaveData cycle)
    {
        var request = Project(new CropHarvestCompletedFact(plotId, pending, cycle));
        if (!string.IsNullOrEmpty(pending.completionDeliveryId) && pending.completionDeliveryId != request.DeliveryId
            || !string.IsNullOrEmpty(pending.completionDeliveryFingerprint)
                && pending.completionDeliveryFingerprint != request.PayloadFingerprint
            || !string.IsNullOrEmpty(pending.completionPublicationKey)
                && pending.completionPublicationKey != WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString())
            throw new InvalidOperationException("Saved crop completion publication provenance drifted.");
        return request;
    }

    private static WorkCompletionIdentityDeliveryRequest Project(CropHarvestCompletedFact fact)
    {
        GameplayResultKey child = new(EnvironmentOutcomeIds.CropPlanProducer,
            new GameplayOperationId(fact.CycleId), checked((long)fact.OperationSequence + 1L), 0);
        if (!string.IsNullOrEmpty(fact.ParentPublicationKey) && fact.ParentPublicationKey != child.ToString())
            throw new InvalidOperationException("Crop completion references a different terminal publication.");
        return new WorkCompletionIdentityDeliveryRequest(
            DeliveryPrefix + fact.OperationId, StreamPrefix + fact.PlotId.Value,
            fact.OperationSequence, fact.WorkerId, BuiltInWorkTypeIds.Harvest.Value,
            fact.PlotId.Value + ":" + fact.OutcomeId, CharacterCommandOrigin.Autonomous,
            fact.AbsoluteDay, child.OperationId.Value, child.ProducerId, child.ToString(),
            fact.PlotId.Value, fact.ContributionWork);
    }
}
