using System;
using System.Collections.Generic;

// 저장 검사마다 원 장부와 독립된 DTO를 제공한다. 사실과 검사는 생략하지 않는다.
internal static class GameplayOutcomeSaveReadCopy
{
    private static readonly Func<GameplayOutcomeSnapshot, GameplayOutcomeSnapshot> OutcomeCopy = Copy;
    private static readonly Func<CompactedNarrativeMemorySnapshot, CompactedNarrativeMemorySnapshot> MemoryCopy = Copy;
    private static readonly Func<GameplayOutcomeTombstoneSnapshot, GameplayOutcomeTombstoneSnapshot> TombstoneCopy = Copy;
    private static readonly Func<GameplayOutcomeConsolidationJobSnapshot, GameplayOutcomeConsolidationJobSnapshot> JobCopy = Copy;
    private static readonly Func<GameplayOutcomePerspectiveCacheSnapshot, GameplayOutcomePerspectiveCacheSnapshot> PerspectiveCopy = Copy;
    private static readonly Func<GameplayOutcomeParticipantSnapshot, GameplayOutcomeParticipantSnapshot> ParticipantCopy = Copy;
    private static readonly Func<GameplayOutcomeMetricSnapshot, GameplayOutcomeMetricSnapshot> MetricCopy = Copy;
    private static readonly Func<GameplayOutcomeSubjectSnapshot, GameplayOutcomeSubjectSnapshot> SubjectCopy = Copy;
    private static readonly Func<NarrativeEvidenceReferenceSnapshot, NarrativeEvidenceReferenceSnapshot> AnchorCopy = Copy;
    private static readonly Func<GameplayOutcomeProvenanceReferenceSnapshot, GameplayOutcomeProvenanceReferenceSnapshot> ProvenanceCopy = Copy;
    private static readonly Func<GameplayOutcomeFactSnapshot, GameplayOutcomeFactSnapshot> FactCopy = Copy;
    private static readonly Func<GameplayOutcomeMetricAggregateSnapshot, GameplayOutcomeMetricAggregateSnapshot> AggregateMetricCopy = Copy;

    internal static GameplayOutcomeLedgerSaveData Copy(GameplayOutcomeLedgerSaveData source) =>
        source == null ? null : new GameplayOutcomeLedgerSaveData
        {
            version = source.version,
            runId = source.runId,
            worldEpoch = source.worldEpoch,
            revision = source.revision,
            nextSequence = source.nextSequence,
            notificationFaultCount = source.notificationFaultCount,
            knownResultKeyHighWater = source.knownResultKeyHighWater,
            capacityDeferredCount = source.capacityDeferredCount,
            consecutiveCapacityDeferredCount = source.consecutiveCapacityDeferredCount,
            capacityDeferredStreakHighWater = source.capacityDeferredStreakHighWater,
            exactPageUseHighWater = source.exactPageUseHighWater,
            retentionPromotionFaultCount = source.retentionPromotionFaultCount,
            lastScheduledEvaluationDay = source.lastScheduledEvaluationDay,
            lastScheduledCutoffSequence = source.lastScheduledCutoffSequence,
            outbox = CopyList(source.outbox, OutcomeCopy),
            exactOutcomes = CopyList(source.exactOutcomes, OutcomeCopy),
            compactedMemories = CopyList(source.compactedMemories, MemoryCopy),
            tombstones = CopyList(source.tombstones, TombstoneCopy),
            consolidationJobs = CopyList(source.consolidationJobs, JobCopy),
            perspectiveCache = CopyList(source.perspectiveCache, PerspectiveCopy),
        };

    internal static GameplayOutcomeSnapshot Copy(GameplayOutcomeSnapshot source)
    {
        if (source == null) return null;
        var result = source.ShallowCopyForSaveRead();
        result.participants = CopyList(source.participants, ParticipantCopy);
        result.metrics = CopyList(source.metrics, MetricCopy);
        result.subjects = CopyList(source.subjects, SubjectCopy);
        result.initialSubjects = CopyList(source.initialSubjects, SubjectCopy);
        result.tags = source.tags == null ? null : new List<string>(source.tags);
        result.anchors = CopyList(source.anchors, AnchorCopy);
        result.initialAnchors = CopyList(source.initialAnchors, AnchorCopy);
        result.provenance = CopyList(source.provenance, ProvenanceCopy);
        result.facts = CopyList(source.facts, FactCopy);
        return result;
    }

    internal static GameplayOutcomeParticipantSnapshot Copy(GameplayOutcomeParticipantSnapshot source) =>
        source == null ? null : new GameplayOutcomeParticipantSnapshot
        {
            entityKindId = source.entityKindId,
            entityId = source.entityId,
            roleId = source.roleId,
            participationKind = source.participationKind,
            hasPerceptionEvidence = source.hasPerceptionEvidence,
            displayText = source.displayText,
            displayRevision = source.displayRevision,
            locale = source.locale,
            pronunciationMode = source.pronunciationMode,
            pronunciationValue = source.pronunciationValue,
            finalConsonant = source.finalConsonant,
            pronunciationRevision = source.pronunciationRevision,
        };

    internal static GameplayOutcomeMetricSnapshot Copy(GameplayOutcomeMetricSnapshot source) =>
        source == null ? null : new GameplayOutcomeMetricSnapshot
        {
            metricId = source.metricId,
            value = source.value,
            unitId = source.unitId,
            referenceKindId = source.referenceKindId,
            referenceId = source.referenceId,
        };

    internal static GameplayOutcomeSubjectSnapshot Copy(GameplayOutcomeSubjectSnapshot source) =>
        source == null ? null : new GameplayOutcomeSubjectSnapshot
        {
            entityKindId = source.entityKindId,
            entityId = source.entityId,
            salience = source.salience,
            tier = source.tier,
            isPinned = source.isPinned,
            isOptionalWitness = source.isOptionalWitness,
            anchorRevision = source.anchorRevision,
            nextEvaluationDay = source.nextEvaluationDay,
            influenceUseCount = source.influenceUseCount,
            influenceRevision = source.influenceRevision,
        };

    internal static NarrativeEvidenceReferenceSnapshot Copy(NarrativeEvidenceReferenceSnapshot source) =>
        source == null ? null : new NarrativeEvidenceReferenceSnapshot
        {
            subjectKindId = source.subjectKindId,
            subjectId = source.subjectId,
            anchorTypeId = source.anchorTypeId,
            anchorId = source.anchorId,
        };

    internal static GameplayOutcomeProvenanceReferenceSnapshot Copy(GameplayOutcomeProvenanceReferenceSnapshot source) =>
        source == null ? null : new GameplayOutcomeProvenanceReferenceSnapshot
        {
            kindId = source.kindId,
            value = source.value,
        };

    internal static GameplayOutcomeFactSnapshot Copy(GameplayOutcomeFactSnapshot source) =>
        source == null ? null : new GameplayOutcomeFactSnapshot
        {
            factId = source.factId,
            value = source.value,
        };

    internal static CompactedNarrativeMemorySnapshot Copy(CompactedNarrativeMemorySnapshot source) =>
        source == null ? null : new CompactedNarrativeMemorySnapshot
        {
            memoryId = source.memoryId,
            sharedAggregateId = source.sharedAggregateId,
            signature = source.signature,
            outcomeTypeId = source.outcomeTypeId,
            status = source.status,
            subjectKindId = source.subjectKindId,
            subjectId = source.subjectId,
            locationId = source.locationId,
            roomId = source.roomId,
            locationX = source.locationX,
            locationY = source.locationY,
            firstSequence = source.firstSequence,
            lastSequence = source.lastSequence,
            firstDay = source.firstDay,
            lastDay = source.lastDay,
            occurrenceCount = source.occurrenceCount,
            salience = source.salience,
            influenceUseCount = source.influenceUseCount,
            influenceRevision = source.influenceRevision,
            sourceSegmentHash = source.sourceSegmentHash,
            compactedPayloadHash = source.compactedPayloadHash,
            tags = source.tags == null ? null : new List<string>(source.tags),
            metrics = CopyList(source.metrics, AggregateMetricCopy),
            participants = CopyList(source.participants, ParticipantCopy),
            provenance = CopyList(source.provenance, ProvenanceCopy),
            facts = CopyList(source.facts, FactCopy),
        };

    internal static GameplayOutcomeMetricAggregateSnapshot Copy(GameplayOutcomeMetricAggregateSnapshot source) =>
        source == null ? null : new GameplayOutcomeMetricAggregateSnapshot
        {
            metricId = source.metricId,
            unitId = source.unitId,
            referenceKindId = source.referenceKindId,
            referenceId = source.referenceId,
            sum = source.sum,
            minimum = source.minimum,
            maximum = source.maximum,
            sampleCount = source.sampleCount,
        };

    internal static GameplayOutcomeTombstoneSnapshot Copy(GameplayOutcomeTombstoneSnapshot source) =>
        source == null ? null : new GameplayOutcomeTombstoneSnapshot
        {
            runId = source.runId,
            sequence = source.sequence,
            producerId = source.producerId,
            operationId = source.operationId,
            commitRevision = source.commitRevision,
            localResultIndex = source.localResultIndex,
            ownerRevision = source.ownerRevision,
            temporalMetadataVersion = source.temporalMetadataVersion,
            absoluteDay = source.absoluteDay,
            parentRunId = source.parentRunId,
            parentSequence = source.parentSequence,
            deliveryMetadataVersion = source.deliveryMetadataVersion,
            deliveryClockId = source.deliveryClockId,
            recordedAbsoluteHour = source.recordedAbsoluteHour,
            publishedRevision = source.publishedRevision,
            deliveryFaultCount = source.deliveryFaultCount,
            deliveryDelayReason = source.deliveryDelayReason,
            terminalTier = source.terminalTier,
            canonicalHash = source.canonicalHash,
            influenceUseCount = source.influenceUseCount,
            influenceRevision = source.influenceRevision,
        };

    internal static GameplayOutcomeConsolidationJobSnapshot Copy(GameplayOutcomeConsolidationJobSnapshot source) =>
        source == null ? null : new GameplayOutcomeConsolidationJobSnapshot
        {
            evaluationDay = source.evaluationDay,
            cutoffSequence = source.cutoffSequence,
            policyVersion = source.policyVersion,
            worldEpoch = source.worldEpoch,
            nextPublishedSlot = source.nextPublishedSlot,
            nextSequence = source.nextSequence,
        };

    internal static GameplayOutcomePerspectiveCacheSnapshot Copy(GameplayOutcomePerspectiveCacheSnapshot source) =>
        source == null ? null : new GameplayOutcomePerspectiveCacheSnapshot
        {
            runId = source.runId,
            sequence = source.sequence,
            viewerKindId = source.viewerKindId,
            viewerId = source.viewerId,
            perspectiveKind = source.perspectiveKind,
            locale = source.locale,
            rendererVersion = source.rendererVersion,
            text = source.text,
            neutralFrameUsed = source.neutralFrameUsed,
        };

    private static List<T> CopyList<T>(List<T> source, Func<T, T> copy)
    {
        if (source == null) return null;
        var result = new List<T>(source.Count);
        foreach (T item in source) result.Add(copy(item));
        return result;
    }
}
