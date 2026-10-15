using System;

public static class MemoryErasureBossAwardFactMapping
{
    public const string ObserverId = "log:offense.memory-erasure-boss-award";

    public static GameplayResultKey ResultKey(
        MemoryErasureBossAwardFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            EvolutionOutcomeIds.MemoryErasureBossAwardProducerId,
            new GameplayOperationId(data.operationId),
            0L,
            0);
    }

    public static MemoryErasureBossAwardOutcomeReceipt Capture(
        MemoryErasureBossAwardFact fact)
    {
        MemoryErasureBossAwardFactData data = fact.Snapshot();
        data.Validate();
        var result = new MemoryErasureSealBossAwardResult(
            MemoryErasureSealBossAwardStatus.Awarded,
            data.regionId,
            data.operationId,
            data.physicalCommitId,
            "exactly one physical memory-erasure seal was published");
        return new MemoryErasureBossAwardOutcomeReceipt(
            result,
            data.regionDisplayName,
            data.absoluteDay);
    }

    public static GameplayDomainFactObserverBinding<
        MemoryErasureBossAwardFact,
        MemoryErasureBossAwardOutcomeReceipt> CreateBinding(
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
