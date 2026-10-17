using System;
using System.Globalization;
using System.Text;

public sealed class WildlifeFoodRaidFactMapping
{
    private static readonly GameplayOutcomeFactId ContextSha256Fact =
        new("wildlife-food-raid-context-sha256");

    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public WildlifeFoodRaidFactMapping(
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public WildlifeFoodRaidOutcomeReceipt Capture(
        WildlifeFoodRaidFact fact)
    {
        WildlifeFoodRaidFactData source = fact.Snapshot();
        GameplayResultKey childKey = PhysicalChildKey(source);
        if (!diagnostics.TryGetResultIdentity(
                childKey,
                out GameplayOutcomeReplayIdentity identity)
            || identity.ResultKey != childKey
            || !identity.OutcomeId.IsValid
            || !identity.HasCanonicalPayloadHash
            || identity.State is not (GameplayOutcomeReplayState.Committed
                or GameplayOutcomeReplayState.DeliveryFaultPending
                or GameplayOutcomeReplayState.PublishedAwaitingAcknowledgement
                or GameplayOutcomeReplayState.PublishedAcknowledged
                or GameplayOutcomeReplayState.Compacted
                or GameplayOutcomeReplayState.Forgotten))
        {
            throw new InvalidOperationException(
                "The committed physical wildlife food-raid child is unavailable.");
        }

        return CaptureWithChild(fact, identity.OutcomeId);
    }

    public static WildlifeFoodRaidOutcomeReceipt CaptureWithChild(
        WildlifeFoodRaidFact fact,
        GameplayOutcomeId physicalChildOutcomeId)
    {
        WildlifeFoodRaidFactData source = fact.Snapshot();
        source.Validate();
        if (!physicalChildOutcomeId.IsValid)
        {
            throw new ArgumentException(
                "A valid physical wildlife food-raid child outcome is required.",
                nameof(physicalChildOutcomeId));
        }

        GameplayResultKey childKey = PhysicalChildKey(source);
        WildlifeFoodRaidOutcomeReceipt receipt =
            WildlifeOutcomeReceiptFactory.CreateWildlifeFoodRaid(
                source,
                childKey.ToString(),
                physicalChildOutcomeId);
        return new WildlifeFoodRaidOutcomeReceipt(
            WithContext(receipt.Payload, source));
    }

    public static GameplayResultKey PhysicalChildKey(
        WildlifeFoodRaidFactData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate();
        return new GameplayResultKey(
            TradeInventoryOutcomeDefinitions.Get(
                TradeInventoryOutcomeKind.PhysicalItemBatchDisposition)
                .ProducerId,
            new GameplayOperationId(source.dispositionOperationId),
            source.dispositionOwnerRevision,
            0);
    }

    public static GameplayDomainFactObserverBinding<
        WildlifeFoodRaidFact,
        WildlifeFoodRaidOutcomeReceipt> CreateBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        var mapping = new WildlifeFoodRaidFactMapping(diagnostics);
        return new GameplayDomainFactObserverBinding<
            WildlifeFoodRaidFact,
            WildlifeFoodRaidOutcomeReceipt>(
                "log:environment.wildlife-food-raid",
                mapping.Capture,
                registry,
                ledger,
                recorder,
                commits);
    }

    public static string ComputeContextSha256(
        WildlifeFoodRaidFactData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate();
        WildlifeFoodRaidFactData data = source.Clone();

        var canonical = new StringBuilder();
        Append(canonical, "wildlife-food-raid-context-v1");
        Append(canonical, data.raidId);
        Append(canonical, data.wildlifeId);
        Append(canonical, data.wildlifeSpeciesId);
        Append(canonical, data.wildlifeDisplayName);
        Append(canonical, data.targetStackId);
        Append(canonical, data.targetItemId);
        Append(canonical, data.targetItemDisplayName);
        Append(canonical, data.requestedQuantity);
        Append(canonical, data.startX);
        Append(canonical, data.startY);
        Append(canonical, data.startedGameTime);
        Append(canonical, data.startedAbsoluteDay);
        Append(canonical, data.endX);
        Append(canonical, data.endY);
        Append(canonical, data.completedGameTime);
        Append(canonical, data.completedAbsoluteDay);
        Append(canonical, data.stolenQuantity);
        Append(canonical, data.dispositionOperationId);
        Append(canonical, data.dispositionReasonCode);
        Append(canonical, data.dispositionCommitId);
        Append(canonical, data.dispositionSourceStackIds.Count);
        foreach (string stackId in data.dispositionSourceStackIds)
            Append(canonical, stackId);
        Append(canonical, data.dispositionQuantity);
        Append(canonical, data.dispositionInputMassGrams);
        Append(canonical, data.dispositionOwnerRevision);
        Append(canonical, data.dispositionItemId);
        Append(canonical, data.outcomeReason);
        return NarrativeInferenceHash.ComputeSha256Utf8(
            canonical.ToString());
    }

    private static EnvironmentOutcomePayload WithContext(
        EnvironmentOutcomePayload payload,
        WildlifeFoodRaidFactData source)
    {
        EnvironmentFixedBuffer24<GameplayOutcomeFact> facts = payload.Facts;
        if (!facts.TryAdd(new GameplayOutcomeFact(
                ContextSha256Fact,
                ComputeContextSha256(source))))
        {
            throw new InvalidOperationException(
                "wildlife-food-raid-context-fact-capacity-exhausted");
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

    private static void Append(StringBuilder target, float value) =>
        Append(target, value.ToString("R", CultureInfo.InvariantCulture));
}
