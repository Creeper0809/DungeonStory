using System;
using System.Globalization;
using DungeonStory.Narrative.Korean;

public static class WastePolicyFactObservation
{
    public static TradeInventoryOutcomeReceipt Capture(
        WastePolicyChangedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        string origin = ((int)fact.Origin).ToString(
            CultureInfo.InvariantCulture);
        string definitionId = "waste-policy-definition:" + origin;
        string instanceId = "waste-policy:" + origin;
        KoreanNameSnapshot displayName = TradeInventoryOutcomeNames.Snapshot(
            FormatWasteOrigin(fact.Origin) + " 정책",
            "waste-policy:" + origin + ":v1");
        GameplayEntityId definition = new(
            TradeInventoryOutcomeIds.WastePolicyDefinitionKind,
            definitionId);
        GameplayEntityId instance = new(
            TradeInventoryOutcomeIds.WastePolicyInstanceKind,
            instanceId);
        GameplayEntityId operation = new(
            TradeInventoryOutcomeIds.OperationKind,
            fact.OperationId);
        string slug = TradeInventoryOutcomeIds.Slug(
            TradeInventoryOutcomeKind.WastePolicyCommand);
        GameplayOutcomeParticipant[] participants =
        {
            new(
                instance,
                TradeInventoryOutcomeIds.PolicyRole,
                GameplayParticipationKind.Direct,
                true,
                displayName),
            new(
                definition,
                TradeInventoryOutcomeIds.PolicyRole,
                GameplayParticipationKind.Direct,
                false,
                displayName)
        };
        GameplayOutcomeMetric[] metrics =
        {
            new(
                TradeInventoryOutcomeIds.KindMetric,
                (int)TradeInventoryOutcomeKind.WastePolicyCommand,
                TradeInventoryOutcomeIds.EnumUnit,
                new GameplayEntityId(
                    TradeInventoryOutcomeIds.OutcomeKind,
                    slug)),
            new(
                TradeInventoryOutcomeIds.ResultRevisionMetric,
                fact.OwnerRevision,
                TradeInventoryOutcomeIds.RevisionUnit,
                operation),
            new(
                TradeInventoryOutcomeIds.BeforeDispositionMetric,
                (int)fact.BeforeDisposition,
                TradeInventoryOutcomeIds.EnumUnit,
                instance),
            new(
                TradeInventoryOutcomeIds.AfterDispositionMetric,
                (int)fact.AfterDisposition,
                TradeInventoryOutcomeIds.EnumUnit,
                instance),
            new(
                TradeInventoryOutcomeIds.BeforeEnabledMetric,
                fact.BeforeEnabled ? 1d : 0d,
                TradeInventoryOutcomeIds.EnumUnit,
                instance),
            new(
                TradeInventoryOutcomeIds.AfterEnabledMetric,
                fact.AfterEnabled ? 1d : 0d,
                TradeInventoryOutcomeIds.EnumUnit,
                instance),
            new(
                TradeInventoryOutcomeIds.BeforePercentMetric,
                fact.BeforeMaximumFeedContamination,
                TradeInventoryOutcomeIds.PercentUnit,
                instance),
            new(
                TradeInventoryOutcomeIds.AfterPercentMetric,
                fact.AfterMaximumFeedContamination,
                TradeInventoryOutcomeIds.PercentUnit,
                instance)
        };
        GameplayOutcomeSubjectLink[] subjects =
        {
            new(
                instance,
                0.32f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0),
            new(
                definition,
                0.24f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0)
        };
        GameplayOutcomeTagId[] tags =
        {
            TradeInventoryOutcomeIds.TradeInventoryTag,
            TradeInventoryOutcomeIds.EconomyTag
        };
        GameplayOutcomeProvenanceReference[] provenance =
        {
            TradeInventoryOutcomeIds.RuntimeReceiptProvenance,
            new("source-receipt", slug),
            new("domain-commit", fact.OperationId)
        };
        return new TradeInventoryOutcomeReceipt(
            TradeInventoryOutcomeKind.WastePolicyCommand,
            fact.OperationId,
            fact.OwnerRevision,
            fact.OwnerRevision,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            participants,
            metrics,
            subjects,
            tags,
            provenance,
            Array.Empty<GameplayOutcomeFact>());
    }

    private static string FormatWasteOrigin(
        WasteOriginKind origin) => origin switch
    {
        WasteOriginKind.Plant => "식물성 부패물",
        WasteOriginKind.Animal => "동물성 부패물",
        WasteOriginKind.Mixed => "혼합 부패물",
        WasteOriginKind.Forbidden => "금기 부패물",
        _ => throw new ArgumentOutOfRangeException(
            nameof(origin),
            origin,
            null)
    };
}
