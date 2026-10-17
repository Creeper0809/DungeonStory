using System;

public static class ResidentEvacuationFactObserver
{
    public const string ObserverId = "log:invasion.resident-evacuation";

    public static GameplayResultKey Key(
        ResidentEvacuationTerminalFact fact) =>
        Capture(fact).ResultKey;

    public static ResidentEvacuationOutcomeReceipt Capture(
        ResidentEvacuationTerminalFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        ResidentEvacuationTerminalSaveData data = fact.Snapshot();
        return new ResidentEvacuationOutcomeReceipt(
            data.operationId,
            data.requestGroupId,
            data.ownerRevision,
            data.characterId,
            data.characterDisplayName,
            data.initiationKind,
            data.threatKind,
            data.terminalKind,
            data.intruderRuntimeIds,
            data.fireCells,
            data.requestedAbsoluteDay,
            data.terminalAbsoluteDay,
            data.originX,
            data.originY,
            data.hasTarget,
            data.targetX,
            data.targetY,
            data.finalX,
            data.finalY);
    }

    public static GameplayDomainFactObserverBinding<
        ResidentEvacuationTerminalFact,
        ResidentEvacuationOutcomeReceipt> CreateBinding(
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
