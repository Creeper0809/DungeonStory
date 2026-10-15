using System;

public static class HeritableTraitsAnalyzedFactMapping
{
    public const string ObserverId = "log:character.heritable-traits-analyzed";

    public static GameplayResultKey ResultKey(
        HeritableTraitsAnalyzedFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            HeritableTraitAnalysisOutcomeIds.ProducerId,
            new GameplayOperationId(data.operationId),
            OwnerRevision(data),
            0);
    }

    public static HeritableTraitAnalysisOutcomeReceipt Capture(
        HeritableTraitsAnalyzedFact fact)
    {
        HeritableTraitsAnalyzedFactData data = fact.Snapshot();
        data.Validate();
        return new HeritableTraitAnalysisOutcomeReceipt(
            data.operationId,
            OwnerRevision(data),
            data.characterId,
            data.characterDisplayName,
            data.facilityInstanceId,
            data.facilityDefinitionId,
            data.kitItemId,
            data.physicalCommitId,
            data.sourceStackIds,
            data.inputMassGrams,
            data.latentTraitIds,
            data.absoluteDay);
    }

    public static GameplayDomainFactObserverBinding<
        HeritableTraitsAnalyzedFact,
        HeritableTraitAnalysisOutcomeReceipt> CreateBinding(
            IGameplayOutcomeRegistry registry,
            GameplayOutcomeLedger ledger,
            IGameplayOutcomeRecorder recorder,
            GameplayDomainFactCommitGroup commits) => new(
                ObserverId,
                Capture,
                registry,
                ledger,
                recorder,
                commits);

    private static long OwnerRevision(HeritableTraitsAnalyzedFactData data) =>
        checked((long)data.operationSequence + 1L);
}
