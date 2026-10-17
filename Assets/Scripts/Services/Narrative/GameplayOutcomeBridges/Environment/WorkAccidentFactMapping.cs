using System;
using System.Globalization;

public static class WorkAccidentFactMapping
{
    public static ProcessAccidentOutcomeReceipt Capture(
        WorkAccidentFact fact)
    {
        WorkAccidentFactSaveData source = (fact
            ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        source.Validate();
        ProcessAccidentOutcomeReceipt receipt =
            EnvironmentConditionOutcomeReceiptFactory.CreateProcessAccident(
                source.operationId,
                new CharacterId(source.workerId),
                source.workerDisplayName,
                new BuildingInstanceId(source.facilityId),
                source.facilityDisplayName,
                new WorkTypeId(source.workTypeId),
                source.anatomyNodeId,
                source.appliedDamage,
                source.observedCause,
                new CoreGridCell(source.x, source.y),
                source.absoluteDay);
        EnvironmentOutcomePayload payload = receipt.Payload;
        EnvironmentFixedBuffer24<GameplayOutcomeFact> facts = payload.Facts;
        AddFact(
            ref facts,
            EnvironmentOutcomeIds.ProcessRiskMultiplierFact,
            source.accidentMultiplier.ToString(
                "R",
                CultureInfo.InvariantCulture));
        AddFact(
            ref facts,
            EnvironmentOutcomeIds.ProcessCommandOriginFact,
            source.commandOrigin.ToString(CultureInfo.InvariantCulture));
        AddFact(
            ref facts,
            EnvironmentOutcomeIds.ProcessFireEligibleFact,
            source.fireEligible ? "true" : "false");
        AddFact(
            ref facts,
            EnvironmentOutcomeIds.ProcessFireIntensityFact,
            source.fireIntensity.ToString(
                "R",
                CultureInfo.InvariantCulture));
        AddFact(
            ref facts,
            EnvironmentOutcomeIds.ProcessFireTargetNameFact,
            source.fireTargetDisplayName.Length == 0
                ? "none"
                : source.fireTargetDisplayName);
        return new ProcessAccidentOutcomeReceipt(
            new EnvironmentOutcomePayload(
                payload.ResultKey,
                payload.OutcomeTypeId,
                payload.AbsoluteDay,
                payload.Status,
                payload.OwnerRevision,
                payload.Participants,
                payload.Metrics,
                payload.Subjects,
                payload.Tags,
                payload.Provenance,
                facts,
                payload.Location,
                payload.Causation));
    }

    public static GameplayDomainFactObserverBinding<
        WorkAccidentFact,
        ProcessAccidentOutcomeReceipt> CreateBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits) => new(
            "log:work.process-accident",
            Capture,
            registry,
            ledger,
            recorder,
            commits);

    private static void AddFact(
        ref EnvironmentFixedBuffer24<GameplayOutcomeFact> facts,
        GameplayOutcomeFactId id,
        string value)
    {
        if (!facts.TryAdd(new GameplayOutcomeFact(id, value)))
            throw new InvalidOperationException(
                "work-accident-canonical-fact-capacity-exhausted");
    }
}
