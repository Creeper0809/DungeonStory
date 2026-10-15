using System;

public static class FloorItemStolenFactMapping
{
    public const string ObserverId = "log:item.floor-stolen";

    public static GameplayResultKey ResultKey(FloorItemStolenFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            VisitorExitOutcomeIds.TheftProducerId,
            new GameplayOperationId(data.operationId),
            data.ownerRevision,
            0);
    }

    public static FloorTheftOutcomeReceipt Capture(FloorItemStolenFact fact)
    {
        FloorItemStolenFactData data = fact.Snapshot();
        data.Validate();
        return new FloorTheftOutcomeReceipt(
            data.operationId,
            data.ownerRevision,
            data.characterId,
            data.characterDisplayName,
            data.absoluteDay,
            data.sourceStackId,
            data.carriedStackId,
            data.itemInstanceId,
            data.itemDefinitionId,
            data.componentFingerprint,
            data.sourceQuantityBefore,
            data.sourceQuantityAfter,
            data.carryQuantityBefore,
            data.carryQuantityAfter,
            data.unitMassGrams,
            data.positionX,
            data.positionY);
    }

    public static GameplayDomainFactObserverBinding<
        FloorItemStolenFact,
        FloorTheftOutcomeReceipt> CreateBinding(
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

public static class VisitorDepartureFactMapping
{
    public const string ObserverId = "log:character.visitor-departure";

    public static GameplayResultKey ResultKey(VisitorDepartureFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            VisitorExitOutcomeIds.DepartureProducerId,
            new GameplayOperationId(data.operationId),
            data.ownerRevision,
            0);
    }

    public static VisitorDepartureOutcomeReceipt Capture(
        VisitorDepartureFact fact)
    {
        VisitorDepartureFactData data = fact.Snapshot();
        data.Validate();
        return new VisitorDepartureOutcomeReceipt(
            data.operationId,
            data.ownerRevision,
            data.characterId,
            data.characterDisplayName,
            data.absoluteDay,
            data.sourceMacro,
            data.terminalKind,
            data.failureReason,
            data.lifecycleBefore,
            data.lifecycleAfter,
            data.visitingBefore,
            data.visitingAfter,
            data.visitCountBefore,
            data.visitCountAfter,
            data.equipmentCountBefore,
            data.equipmentCountAfter,
            data.equipmentRetired,
            data.handoffKind,
            data.theftPublicationKey);
    }

    public static GameplayDomainFactObserverBinding<
        VisitorDepartureFact,
        VisitorDepartureOutcomeReceipt> CreateBinding(
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
