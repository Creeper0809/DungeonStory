using System;
using System.Linq;

public static class MemoryErasureTerminalFactMapping
{
    public const string ObserverId = "log:character.memory-erasure";

    public static GameplayResultKey ResultKey(MemoryErasureTerminalFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            EvolutionOutcomeIds.MemoryErasureProducerId,
            new GameplayOperationId(data.operationId),
            data.terminalRevision,
            0);
    }

    public static MemoryErasureOutcomeReceipt Capture(
        MemoryErasureTerminalFact fact)
    {
        MemoryErasureTerminalFactData data = fact.Snapshot();
        data.Validate();
        var result = new MemoryErasureSealUseResult(
            data.status,
            data.operationId,
            data.auditId,
            data.characterId,
            data.traitInstanceId,
            data.physicalCommitId,
            data.previousRevision,
            data.committedRevision,
            data.absoluteHour,
            data.detail);
        var selected = new CharacterAcquiredTraitInstanceState
        {
            instanceId = data.traitInstanceId,
            combinationId = data.combinationId,
            moduleIds = data.moduleIds.ToList(),
            displayName = data.traitDisplayName
        };
        return new MemoryErasureOutcomeReceipt(
            result,
            selected,
            data.characterDisplayName,
            data.absoluteDay,
            data.terminalRevision);
    }

    public static GameplayDomainFactObserverBinding<
        MemoryErasureTerminalFact,
        MemoryErasureOutcomeReceipt> CreateBinding(
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
