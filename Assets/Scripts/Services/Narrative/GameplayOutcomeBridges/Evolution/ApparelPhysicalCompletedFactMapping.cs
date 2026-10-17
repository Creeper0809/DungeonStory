using System;

public static class ApparelPhysicalCompletedFactMapping
{
    public const string ObserverId = "log:equipment.apparel-physical-completed";

    public static GameplayResultKey ResultKey(
        ApparelPhysicalCompletedFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            EvolutionOutcomeIds.ApparelPhysicalProducerId,
            new GameplayOperationId(data.operationId),
            data.attemptIndex,
            0);
    }

    public static OutcomeWriteRequirements Reserve(
        ApparelPhysicalCompletedFact fact,
        long worldEpoch)
    {
        ApparelPhysicalCompletedFactData data = fact.Snapshot();
        data.Validate();
        if (data.completed)
        {
            throw new InvalidOperationException(
                "apparel-physical-reservation-requires-proposal");
        }
        return ApparelPhysicalOutcomeAdapter.CreateRequirements(
            ResultKey(data),
            data.attemptIndex,
            data.absoluteDay,
            worldEpoch,
            data.hasProductQuality);
    }

    public static ApparelPhysicalOutcomeReceipt Capture(
        ApparelPhysicalCompletedFact fact)
    {
        ApparelPhysicalCompletedFactData data = fact.Snapshot();
        data.Validate(requireCompleted: true);
        var result = new ApparelPhysicalTransactionResult(
            ApparelPhysicalTransactionStatus.Completed,
            data.outputStackId,
            data.outputInstanceId,
            data.inputMassGrams,
            data.outputMassGrams,
            string.Empty);
        return new ApparelPhysicalOutcomeReceipt(
            data.operationId,
            data.attemptIndex,
            data.facilityId,
            data.facilityName,
            data.orderId,
            data.outputItemId,
            result,
            data.absoluteDay,
            data.hasProductQuality ? 1 : 0,
            data.makerId,
            data.makerName,
            (CraftsmanshipQualityTier)data.quality,
            data.attemptIndex,
            data.rejectedBelowMinimum);
    }

    public static GameplayDomainFactObserverBinding<
        ApparelPhysicalCompletedFact,
        ApparelPhysicalOutcomeReceipt> CreateBinding(
            IGameplayOutcomeRegistry registry,
            GameplayOutcomeLedger ledger,
            IGameplayOutcomeRecorder recorder,
            GameplayDomainFactCommitGroup commits) => new(
                ObserverId,
                Capture,
                registry,
                ledger,
                recorder,
                commits,
                Reserve);
}
