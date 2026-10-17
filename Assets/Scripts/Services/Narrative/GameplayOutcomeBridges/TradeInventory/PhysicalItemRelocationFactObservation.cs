using System;
using System.Collections.Generic;

/// <summary>Only the observer maps a frozen movement to the durable narrative receipt.</summary>
public static class PhysicalItemRelocationFactObservation
{
    public static TradeInventoryOutcomeReceipt Capture(PhysicalItemRelocatedFact fact)
    {
        // Reject default(struct) as well as invalid reconstructed facts before reserving.
        fact = new PhysicalItemRelocatedFact(fact.Preview, fact.AbsoluteDay, fact.DestinationState, fact.DestinationId);
        var preview = fact.Preview;
        PhysicalItemRelocationReceipt exact = preview.Receipt;
        var displayName = PhysicalItemFactDisplayName.Snapshot(
            exact.ItemId,
            preview.DisplayName);
        string slug = TradeInventoryOutcomeIds.Slug(
            TradeInventoryOutcomeKind.PhysicalItemRelocation);
        GameplayEntityId sourceStack = new(
            TradeInventoryOutcomeIds.ItemStackKind,
            exact.SourceStackId);
        GameplayEntityId destinationStack = new(
            TradeInventoryOutcomeIds.ItemStackKind,
            exact.DestinationStackId);
        GameplayEntityId itemDefinition = new(
            TradeInventoryOutcomeIds.ItemDefinitionKind,
            exact.ItemId);
        GameplayEntityId operationEntity = new(
            TradeInventoryOutcomeIds.OperationKind,
            exact.OperationId);
        bool hasInstance = preview.ItemInstanceId.Length > 0;
        GameplayEntityId itemInstance = hasInstance
            ? new GameplayEntityId(
                TradeInventoryOutcomeIds.ItemInstanceKind,
                preview.ItemInstanceId)
            : default;
        GameplayOutcomeParticipant[] participants = hasInstance
            ? new[]
            {
                new GameplayOutcomeParticipant(
                    sourceStack,
                    TradeInventoryOutcomeIds.SourceRole,
                    GameplayParticipationKind.Direct,
                    true,
                    displayName),
                new GameplayOutcomeParticipant(
                    destinationStack,
                    TradeInventoryOutcomeIds.DestinationRole,
                    GameplayParticipationKind.Direct,
                    true,
                    displayName),
                new GameplayOutcomeParticipant(
                    itemDefinition,
                    TradeInventoryOutcomeIds.ItemRole,
                    GameplayParticipationKind.Direct,
                    true,
                    displayName),
                new GameplayOutcomeParticipant(
                    itemInstance,
                    TradeInventoryOutcomeIds.ItemRole,
                    GameplayParticipationKind.Direct,
                    true,
                    displayName)
            }
            : new[]
            {
                new GameplayOutcomeParticipant(
                    sourceStack,
                    TradeInventoryOutcomeIds.SourceRole,
                    GameplayParticipationKind.Direct,
                    true,
                    displayName),
                new GameplayOutcomeParticipant(
                    destinationStack,
                    TradeInventoryOutcomeIds.DestinationRole,
                    GameplayParticipationKind.Direct,
                    true,
                    displayName),
                new GameplayOutcomeParticipant(
                    itemDefinition,
                    TradeInventoryOutcomeIds.ItemRole,
                    GameplayParticipationKind.Direct,
                    true,
                    displayName)
            };
        GameplayOutcomeMetric[] metrics =
        {
            new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.KindMetric,
                (int)TradeInventoryOutcomeKind.PhysicalItemRelocation,
                TradeInventoryOutcomeIds.EnumUnit,
                new GameplayEntityId(
                    TradeInventoryOutcomeIds.OutcomeKind,
                    slug)),
            new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.ResultRevisionMetric,
                preview.OwnerRevision,
                TradeInventoryOutcomeIds.RevisionUnit,
                operationEntity),
            new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.QuantityMetric,
                exact.Quantity,
                TradeInventoryOutcomeIds.CountUnit,
                itemDefinition),
            new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.MassMetric,
                exact.MassGrams,
                TradeInventoryOutcomeIds.GramUnit,
                itemDefinition),
            new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.DestinationXMetric,
                exact.DestinationPosition.x,
                TradeInventoryOutcomeIds.CellUnit,
                destinationStack),
            new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.DestinationYMetric,
                exact.DestinationPosition.y,
                TradeInventoryOutcomeIds.CellUnit,
                destinationStack)
        };
        List<GameplayOutcomeSubjectLink> subjectValues = new()
        {
            new GameplayOutcomeSubjectLink(
                sourceStack,
                sourceStack.Equals(destinationStack) ? 0.45f : 0.35f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0)
        };
        if (!sourceStack.Equals(destinationStack))
        {
            subjectValues.Add(new GameplayOutcomeSubjectLink(
                destinationStack,
                0.45f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0));
        }
        subjectValues.Add(new GameplayOutcomeSubjectLink(
            itemDefinition,
            0.5f,
            NarrativeMemoryTier.Recent,
            false,
            false,
            0));
        if (hasInstance)
        {
            subjectValues.Add(new GameplayOutcomeSubjectLink(
                itemInstance,
                0.65f,
                NarrativeMemoryTier.Episodic,
                false,
                false,
                0));
        }
        GameplayOutcomeSubjectLink[] subjects = subjectValues.ToArray();
        GameplayOutcomeTagId[] tags =
        {
            TradeInventoryOutcomeIds.TradeInventoryTag,
            TradeInventoryOutcomeIds.PhysicalTag
        };
        GameplayOutcomeProvenanceReference[] provenance =
        {
            TradeInventoryOutcomeIds.RuntimeReceiptProvenance,
            new GameplayOutcomeProvenanceReference("source-receipt", slug),
            new GameplayOutcomeProvenanceReference(
                "domain-commit",
                "physical-relocation:" + exact.OperationId + ":"
                + preview.OwnerRevision)
        };
        GameplayOutcomeFact[] facts =
        {
            new GameplayOutcomeFact(
                TradeInventoryOutcomeIds.ReasonCodeFact,
                exact.ReasonCode),
            new GameplayOutcomeFact(TradeInventoryOutcomeIds.RequestFingerprintFact, fact.RequestFingerprint)
        };
        return new TradeInventoryOutcomeReceipt(
            TradeInventoryOutcomeKind.PhysicalItemRelocation,
            exact.OperationId,
            0L,
            preview.OwnerRevision,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            participants,
            metrics,
            subjects,
            tags,
            provenance,
            facts,
            new GameplayLocationReference(
                "dungeon",
                string.Empty,
                exact.SourcePosition.x,
                exact.SourcePosition.y));
    }
}
