using System;
using System.Globalization;
using System.Text;

public static class WildlifeFactMapping
{
    private static readonly GameplayOutcomeFactId ContextSha256Fact =
        new("wildlife-context-sha256");

    public static WildlifeCommandOutcomeReceipt Capture(
        WildlifeCommandFact fact)
    {
        WildlifeGameplayOutcomeSaveData source = fact.Snapshot();
        source.Validate();
        return WithContext(
            EnvironmentOutcomeReceiptFactory.CreateWildlifeCommand(source),
            source);
    }

    public static WildlifeActionOutcomeReceipt Capture(
        WildlifeActionFact fact)
    {
        WildlifeGameplayOutcomeSaveData source = fact.Snapshot();
        source.Validate();
        return WithContext(
            EnvironmentOutcomeReceiptFactory.CreateWildlifeAction(source),
            source);
    }

    public static OutcomeWriteRequirements Reserve(
        WildlifeCommandFact fact,
        long worldEpoch)
    {
        WildlifeGameplayOutcomeSaveData source = fact.Snapshot();
        source.Validate(proposed: true);
        return Requirements(
            source,
            EnvironmentOutcomeIds.WildlifeCommandProducer,
            EnvironmentOutcomeIds.WildlifeCommand,
            GameplayOutcomeStatus.Succeeded,
            worldEpoch,
            participantCount: 1,
            metricCount: 7,
            subjectCount: 1,
            factCount: 8);
    }

    public static OutcomeWriteRequirements Reserve(
        WildlifeActionFact fact,
        long worldEpoch)
    {
        WildlifeGameplayOutcomeSaveData source = fact.Snapshot();
        source.Validate(proposed: true);
        return Requirements(
            source,
            EnvironmentOutcomeIds.WildlifeActionProducer,
            EnvironmentOutcomeIds.WildlifeAction,
            ResolveActionStatus(source),
            worldEpoch,
            participantCount: 2,
            metricCount: 13,
            subjectCount: 2,
            factCount: 14);
    }

    public static GameplayDomainFactObserverBinding<
        WildlifeCommandFact,
        WildlifeCommandOutcomeReceipt> CreateCommandBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits) => new(
            "log:environment.wildlife-command",
            Capture,
            registry,
            ledger,
            recorder,
            commits,
            Reserve);

    public static GameplayDomainFactObserverBinding<
        WildlifeActionFact,
        WildlifeActionOutcomeReceipt> CreateActionBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits) => new(
            "log:environment.wildlife-action",
            Capture,
            registry,
            ledger,
            recorder,
            commits,
            Reserve);

    public static string ComputeContextSha256(
        WildlifeGameplayOutcomeSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate();
        WildlifeGameplayOutcomeSaveData data = source.Clone();

        var canonical = new StringBuilder();
        Append(canonical, "wildlife-gameplay-context-v1");
        Append(canonical, data.schemaVersion);
        Append(canonical, (int)data.profile);
        Append(canonical, data.operationId);
        Append(canonical, data.eventKind);
        Append(canonical, data.wildlifeId);
        Append(canonical, data.wildlifeSpeciesId);
        Append(canonical, data.wildlifeDisplayName);
        Append(canonical, data.actorId);
        Append(canonical, data.actorDisplayName);
        Append(canonical, data.facilityId);
        Append(canonical, data.facilityDisplayName);
        Append(canonical, data.x);
        Append(canonical, data.y);
        Append(canonical, data.stateBefore);
        Append(canonical, data.stateAfter);
        Append(canonical, data.flagBefore);
        Append(canonical, data.flagAfter);
        Append(canonical, data.priorityBefore);
        Append(canonical, data.priorityAfter);
        Append(canonical, data.healthBefore);
        Append(canonical, data.healthAfter);
        Append(canonical, data.damage);
        Append(canonical, data.hit);
        Append(canonical, data.killed);
        Append(canonical, data.weaponDefinitionId);
        Append(canonical, data.weaponInstanceId);
        Append(canonical, data.ammunitionItemId);
        Append(canonical, data.ammunitionBefore);
        Append(canonical, data.ammunitionConsumed);
        Append(canonical, data.ammunitionAfter);
        Append(canonical, data.bodyPart);
        Append(canonical, data.coverSourceId);
        Append(canonical, data.coverDamage);
        Append(canonical, data.carcassStackId);
        Append(canonical, data.physicalChildResultKey);
        Append(canonical, data.outputQuantity);
        Append(canonical, data.outputMassGrams);
        Append(canonical, data.reasonCode);
        Append(canonical, data.absoluteDay);
        Append(canonical, data.ownerRevision);
        return NarrativeInferenceHash.ComputeSha256Utf8(
            canonical.ToString());
    }

    private static WildlifeCommandOutcomeReceipt WithContext(
        WildlifeCommandOutcomeReceipt receipt,
        WildlifeGameplayOutcomeSaveData source) => new(
            WithContext(receipt.Payload, source));

    private static WildlifeActionOutcomeReceipt WithContext(
        WildlifeActionOutcomeReceipt receipt,
        WildlifeGameplayOutcomeSaveData source) => new(
            WithContext(receipt.Payload, source));

    private static EnvironmentOutcomePayload WithContext(
        EnvironmentOutcomePayload payload,
        WildlifeGameplayOutcomeSaveData source)
    {
        EnvironmentFixedBuffer24<GameplayOutcomeFact> facts = payload.Facts;
        if (!facts.TryAdd(new GameplayOutcomeFact(
                ContextSha256Fact,
                ComputeContextSha256(source))))
        {
            throw new InvalidOperationException(
                "wildlife-context-fact-capacity-exhausted");
        }

        return new EnvironmentOutcomePayload(
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
            payload.Causation);
    }

    private static OutcomeWriteRequirements Requirements(
        WildlifeGameplayOutcomeSaveData source,
        string producer,
        GameplayOutcomeTypeId outcomeType,
        GameplayOutcomeStatus status,
        long worldEpoch,
        int participantCount,
        int metricCount,
        int subjectCount,
        int factCount) => new(
            new GameplayResultKey(
                producer,
                new GameplayOperationId(source.operationId),
                source.ownerRevision,
                0),
            outcomeType,
            source.absoluteDay,
            status,
            worldEpoch,
            source.ownerRevision,
            participantCount,
            metricCount,
            subjectCount,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 1,
            factCount: factCount);

    private static GameplayOutcomeStatus ResolveActionStatus(
        WildlifeGameplayOutcomeSaveData source) =>
        string.Equals(
            source.eventKind,
            "hunt-attack",
            StringComparison.Ordinal)
        && !source.hit
            ? GameplayOutcomeStatus.Failed
            : GameplayOutcomeStatus.Succeeded;

    private static void Append(StringBuilder target, string value)
    {
        string text = value ?? string.Empty;
        target.Append(Encoding.UTF8.GetByteCount(text).ToString(
                CultureInfo.InvariantCulture))
            .Append(':')
            .Append(text)
            .Append('|');
    }

    private static void Append(StringBuilder target, int value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, long value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, bool value) =>
        Append(target, value ? "1" : "0");
}
