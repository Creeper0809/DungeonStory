using System;

public static class FacilityEvolutionCompletedFactMapping
{
    public const string ObserverId = "log:facility.evolution-completed";

    public static GameplayResultKey ResultKey(
        FacilityEvolutionCompletedFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            EvolutionOutcomeIds.FacilityProducerId,
            new GameplayOperationId(data.operationId),
            data.historySequence,
            0);
    }

    public static FacilityEvolutionOutcomeReceipt Capture(
        FacilityEvolutionCompletedFact fact)
    {
        FacilityEvolutionCompletedFactData data = fact.Snapshot();
        return new FacilityEvolutionOutcomeReceipt(
            data.operationId,
            data.historySequence,
            data.facilityPersistentId,
            data.facilityDisplayName,
            data.recipeId,
            data.resultFacilityDefinitionId,
            data.resultStarGrade,
            data.materialCommitId,
            data.sourceStackIds,
            data.materialQuantity,
            data.materialInputMassGrams,
            data.absoluteDay);
    }

    public static OutcomeWriteRequirements Reserve(
        FacilityEvolutionCompletedFact fact,
        long worldEpoch)
    {
        FacilityEvolutionCompletedFactData data = fact.Snapshot();
        return FacilityEvolutionOutcomeAdapter.CreateRequirements(
            ResultKey(data),
            data.historySequence,
            data.absoluteDay,
            data.HasMaterialCommit,
            worldEpoch);
    }

    public static GameplayDomainFactObserverBinding<
        FacilityEvolutionCompletedFact,
        FacilityEvolutionOutcomeReceipt> CreateBinding(
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
