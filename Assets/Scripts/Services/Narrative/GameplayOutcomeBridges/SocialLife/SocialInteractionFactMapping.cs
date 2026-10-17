using System;
using DungeonStory.Narrative.Korean;

public static class SocialInteractionFactMapping
{
    public const string ConflictObserverId = "log:social.conflict-resolution";
    public const string ApologyObserverId = "log:social.apology-resolution";

    public static GameplayResultKey ResultKey(SocialInteractionFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            data.kind == SocialInteractionKind.Apology
                ? SocialLifeOutcomeIds.ApologyProducerId
                : SocialLifeOutcomeIds.ConflictProducerId,
            new GameplayOperationId(data.operationId),
            0L,
            0);
    }

    public static SocialConflictOutcomeReceipt Capture(SocialConflictFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        if (fact.IsProposal)
            throw new InvalidOperationException(
                "social-conflict-capture-requires-applied-fact");
        SocialInteractionFactData data = fact.Snapshot();
        data.Validate();
        bool minionArgument = data.kind == SocialInteractionKind.MinionArgument;
        bool hasVenue = data.facilityId.Length != 0;
        return new SocialConflictOutcomeReceipt(
            data.operationId,
            new CharacterId(data.actorId),
            minionArgument
                ? ProductionCombatOutcomeNames.Snapshot(
                    data.actorName,
                    data.actorId)
                : Name(
                    "social-life-character-v1",
                    data.actorId,
                    data.actorName),
            new CharacterId(data.targetId),
            minionArgument
                ? ProductionCombatOutcomeNames.Snapshot(
                    data.targetName,
                    data.targetId)
                : Name(
                    "social-life-character-v1",
                    data.targetId,
                    data.targetName),
            data.conflictId,
            data.severity,
            data.origin,
            data.committedMoodDelta,
            data.absoluteDay,
            hasVenue ? new CharacterId(data.customerId) : default,
            hasVenue ? new SpeciesCultureId(data.instigatorCultureId) : default,
            hasVenue ? new SpeciesCultureId(data.targetCultureId) : default,
            hasVenue ? new BuildingInstanceId(data.facilityId) : default,
            hasVenue
                ? Name(
                    "social-life-facility-v1",
                    data.facilityId,
                    data.facilityName)
                : default,
            hasVenue
                ? new CoreGridCell(data.positionX, data.positionY)
                : default);
    }

    public static ApologyOutcomeReceipt Capture(SocialApologyFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        if (fact.IsProposal)
            throw new InvalidOperationException(
                "social-apology-capture-requires-applied-fact");
        SocialInteractionFactData data = fact.Snapshot();
        data.Validate();
        return new ApologyOutcomeReceipt(
            data.operationId,
            new CharacterId(data.actorId),
            Name(
                "social-life-character-v1",
                data.actorId,
                data.actorName),
            new CharacterId(data.targetId),
            Name(
                "social-life-character-v1",
                data.targetId,
                data.targetName),
            data.offenseId,
            data.restitutionProvided,
            data.committedMoodDelta,
            data.absoluteDay,
            SocialOffenseHistory.ParsePublicationId(
                data.offendingPublicationId),
            new GameplayOperationId(data.rootOperationId),
            data.causationRelationId);
    }

    public static OutcomeWriteRequirements ReserveShape(
        SocialConflictFact fact,
        long worldEpoch)
    {
        SocialInteractionFactData data = RequireProposal(fact);
        return SocialConflictOutcomeAdapter.CreateRequirements(
            ResultKey(data),
            data.absoluteDay,
            data.facilityId.Length != 0,
            worldEpoch);
    }

    public static OutcomeWriteRequirements ReserveShape(
        SocialApologyFact fact,
        long worldEpoch)
    {
        SocialInteractionFactData data = RequireProposal(fact);
        return ApologyOutcomeAdapter.CreateRequirements(
            ResultKey(data),
            data.absoluteDay,
            worldEpoch);
    }

    public static GameplayDomainFactObserverBinding<
        SocialConflictFact,
        SocialConflictOutcomeReceipt> CreateConflictBinding(
            IGameplayOutcomeRegistry registry,
            GameplayOutcomeLedger ledger,
            IGameplayOutcomeRecorder recorder,
            GameplayDomainFactCommitGroup group) => new(
                ConflictObserverId,
                Capture,
                registry,
                ledger,
                recorder,
                group,
                ReserveShape, (actual, proposed) => actual.MatchesProposal(proposed));

    public static GameplayDomainFactObserverBinding<
        SocialApologyFact,
        ApologyOutcomeReceipt> CreateApologyBinding(
            IGameplayOutcomeRegistry registry,
            GameplayOutcomeLedger ledger,
            IGameplayOutcomeRecorder recorder,
            GameplayDomainFactCommitGroup group) => new(
                ApologyObserverId,
                Capture,
                registry,
                ledger,
                recorder,
                group,
                ReserveShape, (actual, proposed) => actual.MatchesProposal(proposed));

    private static SocialInteractionFactData RequireProposal(
        SocialInteractionFact fact)
    {
        if (fact?.IsProposal != true)
            throw new InvalidOperationException(
                "social-interaction-reservation-requires-proposal");
        SocialInteractionFactData data = fact.Snapshot();
        data.Validate();
        if (data.committedMoodDelta != 0f)
            throw new InvalidOperationException(
                "social-interaction-proposal-has-effect");
        return data;
    }

    private static KoreanNameSnapshot Name(
        string schema,
        string stableId,
        string displayName)
    {
        string display = displayName?.Trim() ?? string.Empty;
        if (!GameplayOutcomeStableIdSyntax.IsValid(stableId)
            || display.Length == 0)
        {
            throw new ArgumentException(
                "A stable entity id and historical display name are required.");
        }
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append(schema);
        digest.Append(stableId);
        digest.Append(display);
        string revision = schema + ":" + digest.ComputeSha256();
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }
}
