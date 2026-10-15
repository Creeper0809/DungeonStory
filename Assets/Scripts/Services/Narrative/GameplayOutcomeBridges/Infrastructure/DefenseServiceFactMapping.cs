using System;

public static class DefenseServiceFactMapping
{
    public const string ObserverId = "log:defense.facility-service";

    public static GameplayResultKey ResultKey(DefenseServiceFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return DefenseFacilityServiceOutcomeIds.ResultKey(
            data.operationId,
            data.revision);
    }

    public static DefenseFacilityServiceOutcomeReceipt Capture(
        DefenseServiceChangedFact fact,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        if (diagnostics == null)
            throw new ArgumentNullException(nameof(diagnostics));

        DefenseServiceFactData data = fact.Snapshot();
        data.Validate();
        if (data.phase != DefenseFacilityServiceOutcomePhase.Completed)
            return CaptureWithPhysicalIdentity(fact, default);

        GameplayResultKey childKey = PhysicalChildKey(data);
        if (!diagnostics.TryGetResultIdentity(
                childKey,
                out GameplayOutcomeReplayIdentity child)
            || child.State < GameplayOutcomeReplayState.Committed
            || !child.OutcomeId.IsValid
            || !child.HasCanonicalPayloadHash)
        {
            throw new InvalidOperationException(
                "defense-service-physical-child-not-committed");
        }

        return CaptureWithPhysicalIdentity(fact, child.OutcomeId);
    }

    public static DefenseFacilityServiceOutcomeReceipt CaptureWithPhysicalIdentity(
        DefenseServiceChangedFact fact,
        GameplayOutcomeId physicalChildOutcomeId)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        DefenseServiceFactData data = fact.Snapshot();
        data.Validate();
        bool completed = data.phase
            == DefenseFacilityServiceOutcomePhase.Completed;
        if (completed != physicalChildOutcomeId.IsValid)
        {
            throw new ArgumentException(
                completed
                    ? "A completed defense service requires its physical outcome identity."
                    : "A non-completed defense service cannot reference a physical outcome identity.",
                nameof(physicalChildOutcomeId));
        }

        GameplayResultKey physicalChild = completed
            ? PhysicalChildKey(data)
            : default;
        return new DefenseFacilityServiceOutcomeReceipt(
            data.facilityId,
            data.operationId,
            data.revision,
            data.absoluteDay,
            data.kind,
            data.phase,
            data.beforeState,
            data.afterState,
            data.beforeSupply,
            data.afterSupply,
            physicalChild,
            data.itemId,
            data.inputQuantity,
            data.inputMassGrams,
            "player-ui",
            physicalChildOutcomeId);
    }

    public static GameplayDomainFactObserverBinding<
        DefenseServiceChangedFact,
        DefenseFacilityServiceOutcomeReceipt> CreateBinding(
            IGameplayOutcomeRegistry registry,
            GameplayOutcomeLedger ledger,
            IGameplayOutcomeRecorder recorder,
            GameplayDomainFactCommitGroup group)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        return new GameplayDomainFactObserverBinding<
            DefenseServiceChangedFact,
            DefenseFacilityServiceOutcomeReceipt>(
                ObserverId,
                fact => Capture(fact, ledger),
                registry,
                ledger,
                recorder,
                group);
    }

    public static GameplayResultKey PhysicalChildKey(
        DefenseServiceFactData data) => new(
        TradeInventoryOutcomeDefinitions.Get(
            TradeInventoryOutcomeKind.PhysicalItemBatchDisposition)
            .ProducerId,
        new GameplayOperationId(data.physicalOperationId),
        data.physicalOwnerRevision,
        0);
}
