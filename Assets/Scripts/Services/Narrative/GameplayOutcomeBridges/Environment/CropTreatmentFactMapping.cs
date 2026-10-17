using System;

public static class CropTreatmentFactMapping
{
    public static OutcomeWriteRequirements Reserve(
        CropTreatmentFact fact,
        long worldEpoch)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        CropTreatmentOrderSaveData source = fact.Snapshot();
        long ownerRevision = checked((long)source.operationSequence + 1L);
        return new OutcomeWriteRequirements(
            new GameplayResultKey(
                EnvironmentOutcomeIds.CropTreatmentProducer,
                new GameplayOperationId(source.operationId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.CropTreatmentApplied,
            source.completedAbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            worldEpoch,
            ownerRevision,
            participantCount: 3,
            metricCount: 12,
            subjectCount: 3,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 1,
            factCount: 11);
    }

    public static CropTreatmentOutcomeReceipt Capture(CropTreatmentFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        CropTreatmentOrderSaveData source = fact.Snapshot();
        if (source.phase != CropTreatmentOrderPhase.OutcomePublished)
            throw new InvalidOperationException(
                "Only an applied crop-treatment fact can be captured.");

        return CropProductionOutcomeReceiptFactory.CreateCropTreatment(
            source,
            fact.PlotId.Value,
            source.plotDisplayName,
            source.itemDisplayName,
            new CoreGridCell(source.locationX, source.locationY));
    }
}
