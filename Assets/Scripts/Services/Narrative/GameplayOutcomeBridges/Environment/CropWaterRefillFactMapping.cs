using System;

public sealed class CropWaterRefillFactMapping
{
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public CropWaterRefillFactMapping(
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public static OutcomeWriteRequirements Reserve(
        CropWaterRefillFact fact,
        long worldEpoch)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        CropWaterRefillSaveData source = fact.Snapshot();
        long ownerRevision = checked((long)source.operationSequence + 1L);
        return new OutcomeWriteRequirements(
            new GameplayResultKey(
                EnvironmentOutcomeIds.CropManualWaterProducer,
                new GameplayOperationId(source.operationId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.CropManualWaterRefilled,
            source.completedAbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            worldEpoch,
            ownerRevision,
            participantCount: 3,
            metricCount: 5,
            subjectCount: 3,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 1,
            factCount: 7);
    }

    public CropManualWaterRefillOutcomeReceipt Capture(
        CropWaterRefillFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        CropWaterRefillSaveData source = fact.Snapshot();
        if (source.phase != CropWaterRefillPhase.OutcomePublished)
            throw new InvalidOperationException(
                "Only an applied crop water-refill fact can be captured.");

        GameplayResultKey physicalChildKey = PhysicalChildKey(
            source.operationId,
            source.physicalOutcomeOwnerRevision);
        if (!diagnostics.TryGetResultIdentity(
                physicalChildKey,
                out GameplayOutcomeReplayIdentity identity)
            || identity.ResultKey != physicalChildKey
            || !identity.OutcomeId.IsValid)
        {
            throw new InvalidOperationException(
                "The exact physical water-refill child outcome is unavailable.");
        }

        return CropProductionOutcomeReceiptFactory.CreateManualWaterRefill(
            source,
            fact.PlotId,
            source.plotDisplayName,
            source.itemDisplayName,
            physicalChildKey.ToString(),
            identity.OutcomeId,
            new CoreGridCell(source.locationX, source.locationY));
    }

    public static GameplayResultKey PhysicalChildKey(
        string operationId,
        long ownerRevision)
    {
        if (string.IsNullOrWhiteSpace(operationId)
            || !string.Equals(
                operationId,
                operationId.Trim(),
                StringComparison.Ordinal)
            || ownerRevision <= 0L)
        {
            throw new ArgumentException(
                "A canonical physical child operation and positive revision are required.");
        }

        return new GameplayResultKey(
            TradeInventoryOutcomeDefinitions.Get(
                TradeInventoryOutcomeKind.PhysicalItemBatchDisposition).ProducerId,
            new GameplayOperationId(operationId),
            ownerRevision,
            0);
    }
}
