using System;
using System.Buffers;

/// <summary>Maps a frozen physical-item disposition to its narrative receipt.</summary>
public static class PhysicalItemDispositionFactObservation
{
    public static TradeInventoryOutcomeReceipt Capture(
        PhysicalItemDisposedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));

        PhysicalItemBatchDispositionReceipt exactReceipt = fact.Receipt;
        int sourceCount = exactReceipt.SourceFacts.Count;
        GameplayOutcomeParticipant[] participants =
            ArrayPool<GameplayOutcomeParticipant>.Shared.Rent(sourceCount * 2);
        GameplayOutcomeMetric[] metrics =
            ArrayPool<GameplayOutcomeMetric>.Shared.Rent(5 + sourceCount * 4);
        GameplayOutcomeSubjectLink[] subjects =
            ArrayPool<GameplayOutcomeSubjectLink>.Shared.Rent(sourceCount * 2);
        GameplayOutcomeTagId[] tags =
            ArrayPool<GameplayOutcomeTagId>.Shared.Rent(2);
        GameplayOutcomeProvenanceReference[] provenance =
            ArrayPool<GameplayOutcomeProvenanceReference>.Shared.Rent(3);
        GameplayOutcomeFact[] facts =
            ArrayPool<GameplayOutcomeFact>.Shared.Rent(2);
        int participantCount = 0;
        int metricCount = 0;
        int subjectCount = 0;
        try
        {
            GameplayEntityId operationEntity = new(
                TradeInventoryOutcomeIds.OperationKind,
                fact.ObservationOperationId);
            string slug = TradeInventoryOutcomeIds.Slug(
                TradeInventoryOutcomeKind.PhysicalItemBatchDisposition);
            metrics[metricCount++] = new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.KindMetric,
                (int)TradeInventoryOutcomeKind.PhysicalItemBatchDisposition,
                TradeInventoryOutcomeIds.EnumUnit,
                new GameplayEntityId(
                    TradeInventoryOutcomeIds.OutcomeKind,
                    slug));
            metrics[metricCount++] = new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.ResultRevisionMetric,
                exactReceipt.OwnerRevision,
                TradeInventoryOutcomeIds.RevisionUnit,
                operationEntity);
            metrics[metricCount++] = new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.InputQuantityMetric,
                exactReceipt.Quantity,
                TradeInventoryOutcomeIds.CountUnit,
                operationEntity);
            metrics[metricCount++] = new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.InputMassMetric,
                exactReceipt.InputMassGrams,
                TradeInventoryOutcomeIds.GramUnit,
                operationEntity);
            metrics[metricCount++] = new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.DispositionMetric,
                (int)exactReceipt.Kind,
                TradeInventoryOutcomeIds.EnumUnit,
                operationEntity);

            bool samePosition = true;
            UnityEngine.Vector2Int firstPosition =
                exactReceipt.SourceFacts[0].SourcePosition;
            for (int index = 0; index < sourceCount; index++)
            {
                PhysicalItemDispositionSourceFact source =
                    exactReceipt.SourceFacts[index];
                var displayName = PhysicalItemFactDisplayName.Snapshot(
                    source.ItemDefinitionId,
                    source.DisplayName);
                GameplayEntityId stackEntity = new(
                    TradeInventoryOutcomeIds.ItemStackKind,
                    source.StackId);
                GameplayEntityId itemEntity = source.ItemInstanceId.Length > 0
                    ? new GameplayEntityId(
                        TradeInventoryOutcomeIds.ItemInstanceKind,
                        source.ItemInstanceId)
                    : new GameplayEntityId(
                        TradeInventoryOutcomeIds.ItemDefinitionKind,
                        source.ItemDefinitionId);
                participants[participantCount++] = new GameplayOutcomeParticipant(
                    stackEntity,
                    TradeInventoryOutcomeIds.SourceRole,
                    GameplayParticipationKind.Direct,
                    true,
                    displayName);
                subjects[subjectCount++] = new GameplayOutcomeSubjectLink(
                    stackEntity,
                    0.35f,
                    NarrativeMemoryTier.Recent,
                    false,
                    false,
                    0);
                if (!HasParticipant(
                        participants,
                        participantCount,
                        itemEntity,
                        TradeInventoryOutcomeIds.ItemRole))
                {
                    participants[participantCount++] =
                        new GameplayOutcomeParticipant(
                            itemEntity,
                            TradeInventoryOutcomeIds.ItemRole,
                            GameplayParticipationKind.Direct,
                            true,
                            displayName);
                    subjects[subjectCount++] = new GameplayOutcomeSubjectLink(
                        itemEntity,
                        0.55f,
                        NarrativeMemoryTier.Recent,
                        false,
                        false,
                        0);
                }
                metrics[metricCount++] = new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.QuantityMetric,
                    source.Quantity,
                    TradeInventoryOutcomeIds.CountUnit,
                    stackEntity);
                metrics[metricCount++] = new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.MassMetric,
                    source.MassGrams,
                    TradeInventoryOutcomeIds.GramUnit,
                    stackEntity);
                metrics[metricCount++] = new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.SourceXMetric,
                    source.SourcePosition.x,
                    TradeInventoryOutcomeIds.CellUnit,
                    stackEntity);
                metrics[metricCount++] = new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.SourceYMetric,
                    source.SourcePosition.y,
                    TradeInventoryOutcomeIds.CellUnit,
                    stackEntity);
                samePosition &= source.SourcePosition == firstPosition;
            }

            tags[0] = TradeInventoryOutcomeIds.TradeInventoryTag;
            tags[1] = TradeInventoryOutcomeIds.PhysicalTag;
            provenance[0] = TradeInventoryOutcomeIds.RuntimeReceiptProvenance;
            provenance[1] = new GameplayOutcomeProvenanceReference(
                "source-receipt",
                slug);
            provenance[2] = new GameplayOutcomeProvenanceReference(
                "domain-commit",
                fact.ObservationCommitId);
            facts[0] = new GameplayOutcomeFact(
                TradeInventoryOutcomeIds.ReasonCodeFact,
                exactReceipt.ReasonCode);
            facts[1] = new GameplayOutcomeFact(
                TradeInventoryOutcomeIds.RequestFingerprintFact,
                exactReceipt.RequestFingerprint);
            GameplayLocationReference location = samePosition
                ? new GameplayLocationReference(
                    "dungeon",
                    string.Empty,
                    firstPosition.x,
                    firstPosition.y)
                : default;

            GameplayOutcomeParticipant[] capturedParticipants =
                Snapshot(participants, participantCount);
            GameplayOutcomeMetric[] capturedMetrics =
                Snapshot(metrics, metricCount);
            GameplayOutcomeSubjectLink[] capturedSubjects =
                Snapshot(subjects, subjectCount);
            GameplayOutcomeTagId[] capturedTags = Snapshot(tags, 2);
            GameplayOutcomeProvenanceReference[] capturedProvenance =
                Snapshot(provenance, 3);
            GameplayOutcomeFact[] capturedFacts = Snapshot(facts, 2);
            return new TradeInventoryOutcomeReceipt(
                TradeInventoryOutcomeKind.PhysicalItemBatchDisposition,
                fact.ObservationOperationId,
                exactReceipt.OwnerRevision,
                exactReceipt.OwnerRevision,
                fact.AbsoluteDay,
                GameplayOutcomeStatus.Succeeded,
                capturedParticipants,
                participantCount,
                capturedMetrics,
                metricCount,
                capturedSubjects,
                subjectCount,
                capturedTags,
                capturedTags.Length,
                capturedProvenance,
                capturedProvenance.Length,
                capturedFacts,
                capturedFacts.Length,
                location);
        }
        finally
        {
            ArrayPool<GameplayOutcomeParticipant>.Shared.Return(
                participants,
                clearArray: true);
            ArrayPool<GameplayOutcomeMetric>.Shared.Return(
                metrics,
                clearArray: true);
            ArrayPool<GameplayOutcomeSubjectLink>.Shared.Return(
                subjects,
                clearArray: true);
            ArrayPool<GameplayOutcomeTagId>.Shared.Return(tags, clearArray: true);
            ArrayPool<GameplayOutcomeProvenanceReference>.Shared.Return(
                provenance,
                clearArray: true);
            ArrayPool<GameplayOutcomeFact>.Shared.Return(facts, clearArray: true);
        }
    }

    private static T[] Snapshot<T>(T[] values, int count)
    {
        T[] captured = new T[count];
        Array.Copy(values, captured, count);
        return captured;
    }

    private static bool HasParticipant(
        GameplayOutcomeParticipant[] participants,
        int count,
        GameplayEntityId entity,
        GameplayRoleId role)
    {
        for (int index = 0; index < count; index++)
            if (participants[index].EntityId.Equals(entity)
                && participants[index].RoleId.Equals(role))
                return true;
        return false;
    }
}
