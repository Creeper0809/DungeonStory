using System;
using DungeonStory.Narrative.Korean;

public static class FacilitySynthesisFactObserver
{
    public const string ObserverId = "log:facility.synthesis-completed";

    public static GameplayResultKey ResultKey(
        FacilitySynthesisCompletedFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return FacilitySynthesisOutcomeIds.ResultKey(
            data.survivorFacilityId,
            data.revision);
    }

    public static FacilitySynthesisOutcomeReceipt Capture(
        FacilitySynthesisCompletedFact fact)
    {
        FacilitySynthesisCompletedFactData data = fact.Snapshot();
        data.Validate();
        KoreanNameSnapshot[] materialNames =
            new KoreanNameSnapshot[data.materialDisplayNames.Length];
        for (int index = 0; index < materialNames.Length; index++)
        {
            materialNames[index] = FacilitySynthesisOutcomeNames.Snapshot(
                data.materialIds[index],
                data.materialDisplayNames[index]);
        }

        FacilitySynthesisOutcomeReceipt receipt = new(
            data.survivorFacilityId,
            data.revision,
            data.recipeId,
            data.recipeDisplayName,
            data.resultDefinitionId,
            FacilitySynthesisOutcomeNames.Snapshot(
                data.survivorFacilityId,
                data.resultDisplayName),
            data.materialIds,
            data.materialDefinitionIds,
            materialNames,
            data.inheritedLevel,
            data.absoluteDay,
            data.x,
            data.y);
        if (receipt.ResultKey != ResultKey(data))
            throw new InvalidOperationException(
                "facility-synthesis-fact-result-key-mismatch");
        return receipt;
    }

    public static GameplayDomainFactObserverBinding<
        FacilitySynthesisCompletedFact,
        FacilitySynthesisOutcomeReceipt> CreateBinding(
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
}
