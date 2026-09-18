using System;
using System.Collections.Generic;
using DungeonStory.Infrastructure;
using DungeonStory.Narrative.Korean;

[Serializable]
public sealed class GameplayOutcomePerspectiveCacheSnapshot
{
    public string runId = string.Empty;
    public long sequence;
    public string viewerKindId = string.Empty;
    public string viewerId = string.Empty;
    public NarrativePerspectiveKind perspectiveKind;
    public string locale = string.Empty;
    public string rendererVersion = string.Empty;
    public string text = string.Empty;
    public bool neutralFrameUsed;
}

[Serializable]
public sealed class GameplayOutcomeLedgerSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public string runId = string.Empty;
    public long worldEpoch;
    public long revision;
    public long nextSequence = 1L;
    public int notificationFaultCount;
    public int knownResultKeyHighWater;
    public long capacityDeferredCount;
    public int consecutiveCapacityDeferredCount;
    public int capacityDeferredStreakHighWater;
    public int exactPageUseHighWater;
    public int retentionPromotionFaultCount;
    public int lastScheduledEvaluationDay = -1;
    public long lastScheduledCutoffSequence;
    public List<GameplayOutcomeSnapshot> outbox = new List<GameplayOutcomeSnapshot>();
    public List<GameplayOutcomeSnapshot> exactOutcomes = new List<GameplayOutcomeSnapshot>();
    public List<CompactedNarrativeMemorySnapshot> compactedMemories = new List<CompactedNarrativeMemorySnapshot>();
    public List<GameplayOutcomeTombstoneSnapshot> tombstones = new List<GameplayOutcomeTombstoneSnapshot>();
    public List<GameplayOutcomeConsolidationJobSnapshot> consolidationJobs = new List<GameplayOutcomeConsolidationJobSnapshot>();
    public List<GameplayOutcomePerspectiveCacheSnapshot> perspectiveCache = new List<GameplayOutcomePerspectiveCacheSnapshot>();
}

public sealed class GameplayOutcomeLedgerRestoreCandidate : IDungeonDiscardableRestoreCandidate
{
    internal GameplayOutcomeLedgerRestoreCandidate(GameplayOutcomeLedgerRuntimeState state) =>
        State = state ?? throw new ArgumentNullException(nameof(state));
    internal GameplayOutcomeLedgerRuntimeState State { get; private set; }
    public void Discard() => State = null;
    internal GameplayOutcomeLedgerRuntimeState Take()
    {
        GameplayOutcomeLedgerRuntimeState value = State
            ?? throw new InvalidOperationException("Gameplay outcome restore candidate was already consumed or discarded.");
        State = null;
        return value;
    }
}

public interface IGameplayOutcomePersistence
{
    GameplayOutcomeLedgerSaveData CaptureGameplayOutcomes();
    GameplayOutcomeLedgerRestoreCandidate PrepareGameplayOutcomeRestore(GameplayOutcomeLedgerSaveData payload);
    void PublishGameplayOutcomeRestore(GameplayOutcomeLedgerRestoreCandidate candidate);
}

public sealed partial class GameplayOutcomeLedger
{
    public GameplayOutcomeLedgerSaveData CaptureGameplayOutcomes()
    {
        // Save capture is a cold checkpoint path. It owns detached DTO graphs so
        // serialization never observes mutable pooled pages after this lock.
        lock (gate)
        {
            if (state.ReservedCount != 0 || state.ActiveAnchorReservationCount != 0
                || state.ActiveInfluenceReservationCount != 0)
            {
                throw new InvalidOperationException(
                    "Gameplay outcome ledger cannot be captured while an outcome or evidence-anchor reservation is active.");
            }
            GameplayOutcomeLedgerSaveData payload = new GameplayOutcomeLedgerSaveData
            {
                version = GameplayOutcomeLedgerSaveData.CurrentVersion,
                runId = state.RunId.Value,
                worldEpoch = state.WorldEpoch,
                revision = state.Revision,
                nextSequence = state.NextSequence,
                notificationFaultCount = state.NotificationFaultCount,
                knownResultKeyHighWater = state.KnownResultKeyHighWater,
                capacityDeferredCount = state.CapacityDeferredCount,
                consecutiveCapacityDeferredCount = state.ConsecutiveCapacityDeferredCount,
                capacityDeferredStreakHighWater = state.CapacityDeferredStreakHighWater,
                exactPageUseHighWater = state.ExactPageUseHighWater,
                retentionPromotionFaultCount = state.RetentionPromotionFaultCount,
                lastScheduledEvaluationDay = state.LastScheduledEvaluationDay,
                lastScheduledCutoffSequence = state.LastScheduledCutoffSequence,
                outbox = new List<GameplayOutcomeSnapshot>(state.PendingDeliveryCount + 4),
                exactOutcomes = new List<GameplayOutcomeSnapshot>(state.PublishedCount + 4),
                compactedMemories = new List<CompactedNarrativeMemorySnapshot>(state.CompactedMemories.Count),
                tombstones = new List<GameplayOutcomeTombstoneSnapshot>(state.Tombstones.Count),
                consolidationJobs = new List<GameplayOutcomeConsolidationJobSnapshot>(state.ConsolidationJobs.Count),
                perspectiveCache = new List<GameplayOutcomePerspectiveCacheSnapshot>()
            };

            for (int index = 0; index < state.Pool.AddressableCount; index++)
            {
                GameplayOutcomeValuePage page = state.Pool.Get(index);
                if (page == null)
                    continue;
                if (page.State == GameplayOutcomePageState.Committed
                    || page.State == GameplayOutcomePageState.DeliveryFaultPending)
                {
                    payload.outbox.Add(GameplayOutcomeSnapshotCodec.Capture(page));
                }
                else if (page.State == GameplayOutcomePageState.PublishedAwaitingAcknowledgement
                    || page.State == GameplayOutcomePageState.PublishedAcknowledged)
                {
                    payload.exactOutcomes.Add(GameplayOutcomeSnapshotCodec.Capture(page));
                }
            }
            payload.outbox.Sort(CompareSnapshotsBySequence);
            payload.exactOutcomes.Sort(CompareSnapshotsBySequence);
            for (int index = 0; index < state.CompactedMemories.Count; index++)
                payload.compactedMemories.Add(CloneCompacted(state.CompactedMemories[index]));
            for (int index = 0; index < state.Tombstones.Count; index++)
                payload.tombstones.Add(CloneTombstone(state.Tombstones[index]));
            for (int index = 0; index < state.ConsolidationJobs.Count; index++)
            {
                GameplayOutcomeConsolidationJob job = state.ConsolidationJobs[index];
                payload.consolidationJobs.Add(new GameplayOutcomeConsolidationJobSnapshot
                {
                    evaluationDay = job.EvaluationDay,
                    cutoffSequence = job.CutoffSequence,
                    policyVersion = job.PolicyVersion,
                    worldEpoch = job.WorldEpoch,
                    // This slot is transient worker scratch tied to the current
                    // page array. Only sequence progress is durable.
                    nextPublishedSlot = 0,
                    nextSequence = job.NextSequence
                });
            }
            return payload;
        }
    }

    public GameplayOutcomeLedgerRestoreCandidate PrepareGameplayOutcomeRestore(
        GameplayOutcomeLedgerSaveData payload)
    {
        // Restore deliberately allocates a complete detached root. Publication
        // later performs only an atomic reference swap through the coordinator.
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        ValidateTopLevelPayload(payload);
        GameplayOutcomeRunId runId = new GameplayOutcomeRunId(payload.runId);
        long restoredEpoch = checked(payload.worldEpoch + 1L);
        GameplayOutcomeLedgerRuntimeState candidate = new GameplayOutcomeLedgerRuntimeState(
            runId,
            restoredEpoch,
            configuredLimits);
        if (payload.knownResultKeyHighWater > candidate.Limits.KnownResultKeyCapacity)
        {
            throw new InvalidOperationException(
                "Gameplay outcome restore known-result-key high-water exceeds configured capacity.");
        }
        candidate.Revision = payload.revision;
        candidate.NextSequence = payload.nextSequence;
        candidate.NotificationFaultCount = payload.notificationFaultCount;
        candidate.KnownResultKeyHighWater = payload.knownResultKeyHighWater;
        candidate.CapacityDeferredCount = payload.capacityDeferredCount;
        candidate.ConsecutiveCapacityDeferredCount = payload.consecutiveCapacityDeferredCount;
        candidate.CapacityDeferredStreakHighWater = payload.capacityDeferredStreakHighWater;
        candidate.ExactPageUseHighWater = payload.exactPageUseHighWater;
        candidate.RetentionPromotionFaultCount = payload.retentionPromotionFaultCount;
        candidate.LastScheduledEvaluationDay = payload.lastScheduledEvaluationDay;
        candidate.LastScheduledCutoffSequence = payload.lastScheduledCutoffSequence;

        for (int index = 0; index < payload.outbox.Count; index++)
            RestoreExactRecord(candidate, payload.outbox[index], published: false, restoredEpoch);
        for (int index = 0; index < payload.exactOutcomes.Count; index++)
            RestoreExactRecord(candidate, payload.exactOutcomes[index], published: true, restoredEpoch);
        RestoreTombstones(candidate, payload.tombstones);
        if (candidate.KnownResultKeyHighWater < candidate.KnownResultKeys.Count)
            throw new InvalidOperationException("Known-result-key high-water mark is below the restored live count.");
        int restoredPagesInUse = candidate.Pool.PooledInUseCount;
        if (candidate.ExactPageUseHighWater < restoredPagesInUse
            || candidate.ExactPageUseHighWater > candidate.Pool.TotalCount)
            throw new InvalidOperationException("Exact-page high-water mark is inconsistent with restored capacity.");
        RestoreCompacted(candidate, payload.compactedMemories);
        RestoreConsolidationJobs(candidate, payload.consolidationJobs, payload.worldEpoch, restoredEpoch);
        ValidateRestoreReferences(candidate);
        ValidateNextSequence(candidate);
        EnsureLongRunCapacity(candidate);
        return new GameplayOutcomeLedgerRestoreCandidate(candidate);
    }

    [GameplayInternalOnly(
        "Stages a fully validated detached ledger restore candidate for transaction publication.",
        "GameplayOutcomeLedgerSaveSection staged restore commit")]
    public void PublishGameplayOutcomeRestore(GameplayOutcomeLedgerRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        GameplayOutcomeLedgerRuntimeState restored = candidate.Take();
        lock (gate)
        {
            if (!restoreTransactionBegun)
                throw new InvalidOperationException("Gameplay outcome restore transaction was not begun.");
            if (stagedRestoreState != null || restoreCandidatePublished)
                throw new InvalidOperationException("Gameplay outcome restore candidate was already staged.");
            stagedRestoreState = restored;
        }
    }

    public void BeginRestoreCandidate()
    {
        lock (gate)
        {
            if (state.ReservedCount != 0 || state.ActiveAnchorReservationCount != 0
                || state.ActiveInfluenceReservationCount != 0)
                throw new InvalidOperationException("Gameplay outcome restore cannot begin while a reservation is active.");
            if (restoreTransactionBegun || stagedRestoreState != null
                || previousRestoreState != null || restoreCandidatePublished)
            {
                throw new InvalidOperationException(
                    "A gameplay outcome restore transaction is already active.");
            }
            restoreTransactionBegun = true;
        }
    }

    public void PublishRestoreCandidate()
    {
        lock (gate)
        {
            if (!restoreTransactionBegun || stagedRestoreState == null
                || restoreCandidatePublished || previousRestoreState != null)
            {
                throw new InvalidOperationException(
                    "The gameplay outcome restore candidate is not ready to publish.");
            }

            // This is the only live mutation in restore publication. Both roots
            // were fully allocated and validated before this atomic pointer swap.
            previousRestoreState = state;
            state = stagedRestoreState;
            stagedRestoreState = null;
            restoreCandidatePublished = true;
        }
    }

    public void RollbackPublishedRestoreCandidate()
    {
        lock (gate)
        {
            if (restoreCandidatePublished && previousRestoreState != null)
            {
                GameplayOutcomeLedgerRuntimeState abandoned = state;
                state = previousRestoreState;
                DisposeConsolidationScratch(abandoned);
            }
            previousRestoreState = null;
            stagedRestoreState = null;
            restoreCandidatePublished = false;
            restoreTransactionBegun = false;
        }
    }

    public void CompleteRestoreCandidate()
    {
        lock (gate)
        {
            // Retirement is a reference release only and is deliberately
            // non-throwing after the save coordinator has published all roots.
            DisposeConsolidationScratch(previousRestoreState);
            previousRestoreState = null;
            stagedRestoreState = null;
            restoreCandidatePublished = false;
            restoreTransactionBegun = false;
        }
    }

    public void DiscardRestoreCandidate()
    {
        lock (gate)
        {
            if (restoreCandidatePublished && previousRestoreState != null)
            {
                GameplayOutcomeLedgerRuntimeState abandoned = state;
                state = previousRestoreState;
                DisposeConsolidationScratch(abandoned);
            }
            previousRestoreState = null;
            stagedRestoreState = null;
            restoreCandidatePublished = false;
            restoreTransactionBegun = false;
        }
    }

    private void RestoreExactRecord(
        GameplayOutcomeLedgerRuntimeState candidate,
        GameplayOutcomeSnapshot snapshot,
        bool published,
        long restoredEpoch)
    {
        RequireSnapshotCollections(snapshot);
        GameplayOperationId operationId = new GameplayOperationId(snapshot.operationId);
        GameplayResultKey resultKey = new GameplayResultKey(
            snapshot.producerId,
            operationId,
            snapshot.commitRevision,
            snapshot.localResultIndex);
        GameplayOutcomeTypeId outcomeTypeId = new GameplayOutcomeTypeId(snapshot.outcomeTypeId);
        OutcomeWriteRequirements requirements = new OutcomeWriteRequirements(
            resultKey,
            outcomeTypeId,
            snapshot.absoluteDay,
            snapshot.status,
            restoredEpoch,
            snapshot.ownerRevision,
            snapshot.participants.Count,
            snapshot.metrics.Count,
            Math.Max(snapshot.subjects.Count, snapshot.initialSubjects.Count),
            snapshot.tags.Count,
            Math.Max(snapshot.anchors.Count, snapshot.initialAnchors.Count),
            snapshot.provenance.Count,
            snapshot.facts.Count);
        if (!ValidateRequirements(requirements, out string validationFailure))
            throw new InvalidOperationException($"Gameplay outcome restore rejected '{resultKey}': {validationFailure}.");
        if (candidate.KnownResultKeys.Count >= candidate.Limits.KnownResultKeyCapacity)
        {
            throw new InvalidOperationException(
                "Gameplay outcome restore exceeds the configured known-result-key capacity.");
        }
        if (!candidate.KnownResultKeys.Add(resultKey))
            throw new InvalidOperationException($"Duplicate gameplay result key '{resultKey}' in restore payload.");
        if (!candidate.Pool.TryRent(requirements, out int pageIndex, out GameplayOutcomeValuePage page))
            throw new InvalidOperationException("Gameplay outcome restore exceeds the configured bounded page capacity.");

        GameplayOutcomeId outcomeId = new GameplayOutcomeId(candidate.RunId, snapshot.sequence);
        if (!string.Equals(snapshot.runId, candidate.RunId.Value, StringComparison.Ordinal))
            throw new InvalidOperationException("Gameplay outcome restore contains a foreign run ID.");
        if (snapshot.commitGeneration != snapshot.commitRevision
            || snapshot.anchorRevision < 0
            || snapshot.deliveryFaultCount < 0)
        {
            throw new InvalidOperationException(
                $"Gameplay outcome '{outcomeId}' has invalid revision metadata.");
        }
        page.OutcomeId = outcomeId;
        page.Sequence = snapshot.sequence;
        page.CommitGeneration = snapshot.commitGeneration;
        page.OwnerRevision = snapshot.ownerRevision;
        page.WorldEpoch = restoredEpoch;
        page.StorageTier = snapshot.storageTier;
        page.AnchorRevision = snapshot.anchorRevision;
        page.DeliveryFaultCount = snapshot.deliveryFaultCount;
        page.LastDeliveryFaultCode = snapshot.lastDeliveryFaultCode ?? string.Empty;
        page.Location = new GameplayLocationReference(
            snapshot.locationId,
            snapshot.roomId,
            snapshot.locationX,
            snapshot.locationY);
        GameplayOutcomeId parentId = string.IsNullOrEmpty(snapshot.parentRunId)
            ? default
            : new GameplayOutcomeId(new GameplayOutcomeRunId(snapshot.parentRunId), snapshot.parentSequence);
        GameplayOperationId rootOperation = string.IsNullOrEmpty(snapshot.rootOperationId)
            ? default
            : new GameplayOperationId(snapshot.rootOperationId);
        page.Causation = new GameplayOutcomeCausation(
            parentId,
            rootOperation,
            snapshot.causationRelationId);
        RestoreRows(page, snapshot);
        page.ImmutablePayloadHash = snapshot.immutablePayloadHash ?? string.Empty;
        if (!IsSha256(page.ImmutablePayloadHash)
            || !string.Equals(
                page.ImmutablePayloadHash,
                GameplayOutcomeCanonicalHash.HashExact(new GameplayOutcomeReadView(page)),
                StringComparison.Ordinal)
            || !GameplayOutcomeCanonicalHash.TryDecodeHex(
                page.ImmutablePayloadHash,
                page.ImmutablePayloadDigest))
        {
            throw new InvalidOperationException(
                $"Gameplay outcome '{outcomeId}' immutable receipt hash failed integrity validation.");
        }
        page.HasImmutablePayloadDigest = true;

        GameplayOutcomePageState lifecycle = (GameplayOutcomePageState)(int)snapshot.lifecycle;
        if (published)
        {
            if (lifecycle != GameplayOutcomePageState.PublishedAwaitingAcknowledgement
                && lifecycle != GameplayOutcomePageState.PublishedAcknowledged)
                throw new InvalidOperationException($"Published gameplay outcome '{outcomeId}' has invalid lifecycle '{snapshot.lifecycle}'.");
            if (!GameplayOutcomeVisibilityRules.IsExactSubjectTierVisible(snapshot.storageTier))
                throw new InvalidOperationException($"Published gameplay outcome '{outcomeId}' has a non-exact storage tier.");
            page.State = lifecycle;
            page.PublishedListSlot = candidate.PublishedPageIndices.Count;
            candidate.PublishedPageIndices.Add(pageIndex);
            candidate.PublishedCount++;
            RegisterDuePage(candidate, pageIndex, page);
        }
        else
        {
            if (lifecycle != GameplayOutcomePageState.Committed
                && lifecycle != GameplayOutcomePageState.DeliveryFaultPending)
                throw new InvalidOperationException($"Outbox gameplay outcome '{outcomeId}' has invalid lifecycle '{snapshot.lifecycle}'.");
            if (snapshot.storageTier != NarrativeMemoryTier.Recent)
                throw new InvalidOperationException($"Outbox gameplay outcome '{outcomeId}' has an invalid storage tier.");
            page.State = lifecycle;
            candidate.PendingDeliveryCount++;
        }
        candidate.ActiveByResultKey.Add(resultKey, pageIndex);
        if (!candidate.ActiveByOutcomeId.TryAdd(outcomeId, pageIndex))
            throw new InvalidOperationException($"Duplicate gameplay outcome ID '{outcomeId}' in restore payload.");
        if (!registry.TryGetDescriptor(outcomeTypeId, out IGameplayOutcomeDescriptor descriptor))
            throw new InvalidOperationException($"Unknown gameplay outcome type '{outcomeTypeId}' in restore payload.");
        OutcomeValidationResult validation = ValidateDescriptorVocabulary(page, descriptor);
        if (validation.Valid)
            validation = descriptor.Validate(new GameplayOutcomeReadView(page));
        if (!validation.Valid)
            throw new InvalidOperationException($"Gameplay outcome '{outcomeId}' failed descriptor restore validation: {validation.DetailCode}.");
        if (published && lifecycle == GameplayOutcomePageState.PublishedAcknowledged)
            PromoteRestoredAcknowledgedPage(candidate, pageIndex, page);
    }

    private static void PromoteRestoredAcknowledgedPage(
        GameplayOutcomeLedgerRuntimeState candidate,
        int pageIndex,
        GameplayOutcomeValuePage page)
    {
        int sourceGeneration = page.Generation;
        if (!candidate.Pool.TryPromotePublishedToDetached(
            pageIndex,
            sourceGeneration,
            out int detachedPageIndex,
            out GameplayOutcomeValuePage detached))
        {
            throw new InvalidOperationException(
                $"Acknowledged gameplay outcome '{page.OutcomeId}' could not enter the detached exact store.");
        }
        candidate.ActiveByResultKey[detached.ResultKey] = detachedPageIndex;
        candidate.ActiveByOutcomeId[detached.OutcomeId] = detachedPageIndex;
        candidate.PublishedPageIndices[detached.PublishedListSlot] = detachedPageIndex;
        RemoveDuePage(candidate, pageIndex);
        RegisterDuePage(candidate, detachedPageIndex, detached);
        if (!candidate.Pool.Return(pageIndex, sourceGeneration))
            throw new InvalidOperationException("Restored hot outcome page could not be released after promotion.");
    }

    private static void RestoreRows(GameplayOutcomeValuePage page, GameplayOutcomeSnapshot snapshot)
    {
        for (int index = 0; index < snapshot.participants.Count; index++)
        {
            GameplayOutcomeParticipantSnapshot source = snapshot.participants[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains a null participant.");
            GameplayOutcomeParticipant value = new GameplayOutcomeParticipant(
                ParseEntity(source.entityKindId, source.entityId, required: true),
                new GameplayRoleId(source.roleId),
                source.participationKind,
                source.hasPerceptionEvidence,
                new KoreanNameSnapshot(
                    source.displayText,
                    source.displayRevision,
                    new KoreanPronunciationHint(
                        source.pronunciationMode,
                        source.pronunciationValue,
                        source.finalConsonant,
                        source.pronunciationRevision),
                    source.locale));
            if ((int)source.participationKind < (int)GameplayParticipationKind.Direct
                || (int)source.participationKind > (int)GameplayParticipationKind.GroupObservation
                || source.participationKind == GameplayParticipationKind.OptionalWitness
                && !source.hasPerceptionEvidence)
                throw new InvalidOperationException("Optional witness restore row has no perception evidence.");
            if (!IsValidDisplayNameSnapshot(value.DisplayName))
                throw new InvalidOperationException("Gameplay outcome restore contains invalid participant display metadata.");
            page.Participants[page.ParticipantCount++] = value;
        }
        for (int index = 0; index < snapshot.metrics.Count; index++)
        {
            GameplayOutcomeMetricSnapshot source = snapshot.metrics[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains a null metric.");
            if (double.IsNaN(source.value) || double.IsInfinity(source.value))
                throw new InvalidOperationException("Gameplay outcome restore contains a non-finite metric.");
            page.Metrics[page.MetricCount++] = new GameplayOutcomeMetric(
                new GameplayMetricId(source.metricId),
                source.value,
                new GameplayMetricUnitId(source.unitId),
                ParseEntity(source.referenceKindId, source.referenceId, required: false));
        }
        HashSet<GameplayEntityId> subjects = new HashSet<GameplayEntityId>();
        for (int index = 0; index < snapshot.subjects.Count; index++)
        {
            GameplayOutcomeSubjectSnapshot source = snapshot.subjects[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains a null subject link.");
            GameplayEntityId subjectId = ParseEntity(source.entityKindId, source.entityId, required: true);
            if (!subjects.Add(subjectId)
                || float.IsNaN(source.salience)
                || float.IsInfinity(source.salience)
                || source.salience < 0f
                || source.salience > 1f
                || (int)source.tier < (int)NarrativeMemoryTier.Recent
                || (int)source.tier > (int)NarrativeMemoryTier.Forgotten
                || source.anchorRevision < 0
                || source.anchorRevision > snapshot.anchorRevision
                || source.influenceUseCount < 0
                || source.influenceRevision < 0
                || source.influenceRevision != source.influenceUseCount
                || source.nextEvaluationDay < 0
                || source.isPinned
                    && (source.tier != NarrativeMemoryTier.Core
                        || source.nextEvaluationDay != int.MaxValue))
                throw new InvalidOperationException("Gameplay outcome restore contains an invalid or duplicate subject link.");
            page.Subjects[page.SubjectCount++] = new GameplayOutcomeSubjectLink(
                subjectId,
                source.salience,
                source.tier,
                source.isPinned,
                source.isOptionalWitness,
                source.anchorRevision,
                source.nextEvaluationDay,
                source.influenceUseCount,
                source.influenceRevision);
        }
        HashSet<GameplayOutcomeTagId> tags = new HashSet<GameplayOutcomeTagId>();
        for (int index = 0; index < snapshot.tags.Count; index++)
        {
            GameplayOutcomeTagId tag = new GameplayOutcomeTagId(snapshot.tags[index]);
            if (!tags.Add(tag))
                throw new InvalidOperationException("Gameplay outcome restore contains a duplicate tag.");
            page.Tags[page.TagCount++] = tag;
        }
        HashSet<GameplayOutcomeProvenanceReference> provenance =
            new HashSet<GameplayOutcomeProvenanceReference>();
        for (int index = 0; index < snapshot.provenance.Count; index++)
        {
            GameplayOutcomeProvenanceReferenceSnapshot source = snapshot.provenance[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains null receipt provenance.");
            GameplayOutcomeProvenanceReference value =
                new GameplayOutcomeProvenanceReference(source.kindId, source.value);
            if (!provenance.Add(value))
                throw new InvalidOperationException("Gameplay outcome restore contains duplicate receipt provenance.");
            page.Provenance[page.ProvenanceCount++] = value;
        }
        HashSet<GameplayOutcomeFactId> facts = new HashSet<GameplayOutcomeFactId>();
        for (int index = 0; index < snapshot.facts.Count; index++)
        {
            GameplayOutcomeFactSnapshot source = snapshot.facts[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains a null immutable fact.");
            GameplayOutcomeFactId factId = new GameplayOutcomeFactId(source.factId);
            if (!facts.Add(factId)
                || !IsValidBoundedUtf16(
                    source.value,
                    GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                    allowEmpty: false))
                throw new InvalidOperationException("Gameplay outcome restore contains an invalid or duplicate immutable fact.");
            page.Facts[page.FactCount++] = new GameplayOutcomeFact(factId, source.value);
        }
        HashSet<NarrativeAnchorKey> anchors = new HashSet<NarrativeAnchorKey>();
        for (int index = 0; index < snapshot.anchors.Count; index++)
        {
            NarrativeEvidenceReferenceSnapshot source = snapshot.anchors[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains a null anchor.");
            GameplayEntityId subjectId = ParseEntity(source.subjectKindId, source.subjectId, required: true);
            NarrativeEvidenceReference reference = new NarrativeEvidenceReference(source.anchorTypeId, source.anchorId);
            NarrativeAnchorKey key = new NarrativeAnchorKey(subjectId, reference);
            if (!subjects.Contains(subjectId) || !anchors.Add(key))
                throw new InvalidOperationException("Gameplay outcome restore contains a broken or duplicate anchor.");
            for (int subjectIndex = 0; subjectIndex < page.SubjectCount; subjectIndex++)
            {
                if (page.Subjects[subjectIndex].SubjectId == subjectId
                    && (page.Subjects[subjectIndex].Tier != NarrativeMemoryTier.Core
                        || page.Subjects[subjectIndex].NextEvaluationDay != int.MaxValue))
                {
                    throw new InvalidOperationException(
                        "Gameplay outcome restore contains an anchor whose subject is not core memory.");
                }
            }
            page.Anchors[page.AnchorCount++] = new GameplayOutcomeAnchorRow(subjectId, reference);
        }
        RestoreInitialRows(page, snapshot);
        if (!ValidateDirectSubjectCoverage(page, out string coverageFailure))
            throw new InvalidOperationException($"Gameplay outcome restore direct subject validation failed: {coverageFailure}.");
        if (page.SubjectCount > 0)
        {
            bool hasExactSubject = false;
            for (int index = 0; index < page.SubjectCount; index++)
                hasExactSubject |= GameplayOutcomeVisibilityRules.IsExactSubjectTierVisible(page.Subjects[index].Tier);
            if (GameplayOutcomeVisibilityRules.IsExactSubjectTierVisible(snapshot.storageTier)
                && !hasExactSubject)
            {
                throw new InvalidOperationException(
                    "Gameplay outcome restore retained an exact page without an exact subject link.");
            }
        }
    }

    private static void RestoreInitialRows(
        GameplayOutcomeValuePage page,
        GameplayOutcomeSnapshot snapshot)
    {
        HashSet<GameplayEntityId> subjects = new HashSet<GameplayEntityId>();
        for (int index = 0; index < snapshot.initialSubjects.Count; index++)
        {
            GameplayOutcomeSubjectSnapshot source = snapshot.initialSubjects[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains a null initial subject link.");
            GameplayEntityId subjectId = ParseEntity(source.entityKindId, source.entityId, required: true);
            if (!subjects.Add(subjectId)
                || float.IsNaN(source.salience)
                || float.IsInfinity(source.salience)
                || source.salience < 0f
                || source.salience > 1f
                || source.tier != NarrativeMemoryTier.Recent
                    && source.tier != NarrativeMemoryTier.Core
                || source.anchorRevision != 0
                || source.influenceUseCount != 0
                || source.influenceRevision != 0
                || source.nextEvaluationDay != 0
                    && source.nextEvaluationDay != int.MaxValue
                || source.isPinned
                    && (source.tier != NarrativeMemoryTier.Core
                        || source.nextEvaluationDay != int.MaxValue))
            {
                throw new InvalidOperationException(
                    "Gameplay outcome restore contains an invalid immutable initial subject link.");
            }
            page.InitialSubjects[page.InitialSubjectCount++] = new GameplayOutcomeSubjectLink(
                subjectId,
                source.salience,
                source.tier,
                source.isPinned,
                source.isOptionalWitness,
                source.anchorRevision,
                source.nextEvaluationDay,
                source.influenceUseCount,
                source.influenceRevision);
        }
        HashSet<NarrativeAnchorKey> anchors = new HashSet<NarrativeAnchorKey>();
        for (int index = 0; index < snapshot.initialAnchors.Count; index++)
        {
            NarrativeEvidenceReferenceSnapshot source = snapshot.initialAnchors[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains a null immutable initial anchor.");
            GameplayEntityId subjectId = ParseEntity(source.subjectKindId, source.subjectId, required: true);
            NarrativeEvidenceReference reference = new NarrativeEvidenceReference(source.anchorTypeId, source.anchorId);
            NarrativeAnchorKey key = new NarrativeAnchorKey(subjectId, reference);
            if (!subjects.Contains(subjectId) || !anchors.Add(key))
            {
                throw new InvalidOperationException(
                    "Gameplay outcome restore contains a broken or duplicate immutable initial anchor.");
            }
            bool core = false;
            for (int subjectIndex = 0; subjectIndex < page.InitialSubjectCount; subjectIndex++)
            {
                if (page.InitialSubjects[subjectIndex].SubjectId == subjectId)
                {
                    core = page.InitialSubjects[subjectIndex].Tier == NarrativeMemoryTier.Core;
                    break;
                }
            }
            if (!core)
                throw new InvalidOperationException("Immutable initial anchor subject is not core memory.");
            page.InitialAnchors[page.InitialAnchorCount++] = new GameplayOutcomeAnchorRow(subjectId, reference);
        }
        for (int subjectIndex = 0; subjectIndex < page.InitialSubjectCount; subjectIndex++)
        {
            GameplayOutcomeSubjectLink subject = page.InitialSubjects[subjectIndex];
            bool anchored = subject.IsPinned;
            for (int anchorIndex = 0; anchorIndex < page.InitialAnchorCount; anchorIndex++)
                anchored |= page.InitialAnchors[anchorIndex].SubjectId == subject.SubjectId;
            if (anchored
                    && (subject.Tier != NarrativeMemoryTier.Core
                        || subject.NextEvaluationDay != int.MaxValue)
                || !anchored
                    && (subject.Tier != NarrativeMemoryTier.Recent
                        || subject.NextEvaluationDay != 0))
            {
                throw new InvalidOperationException(
                    "Immutable initial subject retention metadata is inconsistent with its anchors.");
            }
        }
        if (page.InitialSubjectCount != page.SubjectCount)
            throw new InvalidOperationException("Immutable initial subjects do not match retained subject identities.");
        for (int index = 0; index < page.SubjectCount; index++)
        {
            bool found = false;
            for (int initialIndex = 0; initialIndex < page.InitialSubjectCount; initialIndex++)
                found |= page.Subjects[index].SubjectId == page.InitialSubjects[initialIndex].SubjectId;
            if (!found)
                throw new InvalidOperationException("Retained subject identity was not present in the immutable receipt.");
        }
    }

    private static GameplayEntityId ParseEntity(string kindId, string value, bool required)
    {
        bool empty = string.IsNullOrEmpty(kindId) && string.IsNullOrEmpty(value);
        if (!required && empty)
            return default;
        if (string.IsNullOrEmpty(kindId) || string.IsNullOrEmpty(value))
            throw new InvalidOperationException("Gameplay outcome restore contains a partial entity reference.");
        return new GameplayEntityId(new GameplayEntityKindId(kindId), value);
    }

    private static void RestoreTombstones(
        GameplayOutcomeLedgerRuntimeState candidate,
        List<GameplayOutcomeTombstoneSnapshot> source)
    {
        for (int index = 0; index < source.Count; index++)
        {
            GameplayOutcomeTombstoneSnapshot tombstone = source[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains a null tombstone.");
            GameplayOperationId operationId = new GameplayOperationId(tombstone.operationId);
            GameplayResultKey resultKey = new GameplayResultKey(
                tombstone.producerId,
                operationId,
                tombstone.commitRevision,
                tombstone.localResultIndex);
            if (candidate.KnownResultKeys.Count >= candidate.Limits.KnownResultKeyCapacity)
            {
                throw new InvalidOperationException(
                    "Gameplay outcome restore exceeds the configured known-result-key capacity.");
            }
            if (!string.Equals(tombstone.runId, candidate.RunId.Value, StringComparison.Ordinal)
                || tombstone.sequence <= 0L
                || tombstone.ownerRevision < 0L
                || tombstone.terminalTier != NarrativeMemoryTier.Compacted
                    && tombstone.terminalTier != NarrativeMemoryTier.Forgotten
                || tombstone.influenceUseCount < 0
                || tombstone.influenceRevision < 0
                || tombstone.influenceRevision != tombstone.influenceUseCount
                || !IsSha256(tombstone.canonicalHash)
                || !candidate.KnownResultKeys.Add(resultKey))
                throw new InvalidOperationException("Gameplay outcome restore contains an invalid or duplicate tombstone.");
            candidate.Tombstones.Add(CloneTombstone(tombstone));
            candidate.TombstoneByResultKey.Add(resultKey, candidate.Tombstones.Count - 1);
            if (tombstone.terminalTier == NarrativeMemoryTier.Forgotten)
                candidate.ForgottenCount++;
        }
    }

    private void RestoreCompacted(
        GameplayOutcomeLedgerRuntimeState candidate,
        List<CompactedNarrativeMemorySnapshot> source)
    {
        for (int index = 0; index < source.Count; index++)
        {
            CompactedNarrativeMemorySnapshot memory = CloneCompacted(source[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains a null compacted memory."));
            GameplayEntityId subjectId = ParseEntity(memory.subjectKindId, memory.subjectId, required: true);
            GameplayMemorySignature signature = new GameplayMemorySignature(memory.signature);
            GameplayOutcomeTypeId outcomeTypeId = new GameplayOutcomeTypeId(memory.outcomeTypeId);
            _ = new GameplayLocationReference(
                memory.locationId, memory.roomId, memory.locationX, memory.locationY);
            _ = new NarrativeMemoryId(memory.memoryId);
            string expectedMemoryId = GameplayOutcomeCanonicalHash.CreateMemoryId(
                candidate.RunId,
                subjectId,
                signature);
            string expectedSharedId = GameplayOutcomeCanonicalHash.CreateSharedAggregateId(
                candidate.RunId,
                outcomeTypeId,
                memory.status,
                memory.firstSequence,
                memory.lastSequence,
                memory.occurrenceCount,
                memory.sourceSegmentHash);
            if (!string.Equals(memory.memoryId, expectedMemoryId, StringComparison.Ordinal)
                || !string.Equals(memory.sharedAggregateId, expectedSharedId, StringComparison.Ordinal)
                || memory.firstSequence <= 0L
                || memory.lastSequence < memory.firstSequence
                || memory.lastSequence >= candidate.NextSequence
                || memory.firstDay < 0
                || memory.lastDay < memory.firstDay
                || memory.occurrenceCount <= 0
                || memory.influenceUseCount < 0
                || memory.influenceRevision < 0
                || memory.influenceRevision != memory.influenceUseCount
                || float.IsNaN(memory.salience)
                || float.IsInfinity(memory.salience)
                || memory.salience < 0f
                || memory.salience > 1f
                || (int)memory.status < (int)GameplayOutcomeStatus.Succeeded
                || (int)memory.status > (int)GameplayOutcomeStatus.Cancelled
                || memory.tags == null
                || memory.metrics == null
                || memory.participants == null
                || memory.provenance == null
                || memory.facts == null
                || memory.participants.Count > GameplayOutcomeBufferLimits.AbsoluteMaximumParticipantsPerOutcome
                || memory.provenance.Count > GameplayOutcomeBufferLimits.AbsoluteMaximumProvenancePerOutcome
                || memory.metrics.Count > GameplayOutcomeBufferLimits.AbsoluteMaximumCompactedMetricKeys
                || memory.facts.Count > GameplayOutcomeBufferLimits.AbsoluteMaximumFactsPerOutcome
                || !IsSha256(memory.sourceSegmentHash)
                || !string.Equals(
                    GameplayOutcomeCanonicalHash.HashCompacted(memory),
                    memory.compactedPayloadHash,
                    StringComparison.Ordinal))
                throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' failed integrity validation.");
            if (!registry.TryGetDescriptor(outcomeTypeId, out _))
                throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' has unknown outcome type '{outcomeTypeId}'.");
            string previousTag = null;
            for (int tagIndex = 0; tagIndex < memory.tags.Count; tagIndex++)
            {
                string tag = memory.tags[tagIndex];
                _ = new GameplayOutcomeTagId(tag);
                if (previousTag != null && string.CompareOrdinal(previousTag, tag) >= 0)
                    throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' has duplicate or non-canonical tags.");
                previousTag = tag;
            }
            string previousMetricKey = null;
            for (int metricIndex = 0; metricIndex < memory.metrics.Count; metricIndex++)
            {
                GameplayOutcomeMetricAggregateSnapshot metric = memory.metrics[metricIndex]
                    ?? throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' has a null metric aggregate.");
                _ = new GameplayMetricId(metric.metricId);
                _ = new GameplayMetricUnitId(metric.unitId);
                if (double.IsNaN(metric.sum)
                    || double.IsInfinity(metric.sum)
                    || double.IsNaN(metric.minimum)
                    || double.IsInfinity(metric.minimum)
                    || double.IsNaN(metric.maximum)
                    || double.IsInfinity(metric.maximum)
                    || metric.minimum > metric.maximum
                    || metric.sampleCount <= 0)
                {
                    throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' has an invalid metric aggregate.");
                }
                ParseEntity(metric.referenceKindId, metric.referenceId, required: false);
                string metricKey = metric.metricId + "|" + metric.unitId + "|"
                    + metric.referenceKindId + "|" + metric.referenceId;
                if (previousMetricKey != null
                    && string.CompareOrdinal(previousMetricKey, metricKey) >= 0)
                {
                    throw new InvalidOperationException(
                        $"Compacted gameplay memory '{memory.memoryId}' has duplicate or non-canonical metric aggregates.");
                }
                previousMetricKey = metricKey;
            }
            GameplayOutcomeParticipantSnapshot previousParticipant = null;
            for (int participantIndex = 0; participantIndex < memory.participants.Count; participantIndex++)
            {
                GameplayOutcomeParticipantSnapshot participant = memory.participants[participantIndex]
                    ?? throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' has null participant provenance.");
                GameplayOutcomeParticipant value = new GameplayOutcomeParticipant(
                    ParseEntity(participant.entityKindId, participant.entityId, required: true),
                    new GameplayRoleId(participant.roleId),
                    participant.participationKind,
                    participant.hasPerceptionEvidence,
                    new KoreanNameSnapshot(
                        participant.displayText,
                        participant.displayRevision,
                        new KoreanPronunciationHint(
                            participant.pronunciationMode,
                            participant.pronunciationValue,
                            participant.finalConsonant,
                            participant.pronunciationRevision),
                        participant.locale));
                if (!IsValidDisplayNameSnapshot(value.DisplayName)
                    || (int)value.ParticipationKind < (int)GameplayParticipationKind.Direct
                    || (int)value.ParticipationKind > (int)GameplayParticipationKind.GroupObservation
                    || value.ParticipationKind == GameplayParticipationKind.OptionalWitness
                        && !value.HasPerceptionEvidence
                    || previousParticipant != null
                        && GameplayOutcomeSnapshotCodec.CompareParticipants(previousParticipant, participant) >= 0)
                {
                    throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' has invalid participant provenance.");
                }
                previousParticipant = participant;
            }
            string previousProvenance = null;
            for (int provenanceIndex = 0; provenanceIndex < memory.provenance.Count; provenanceIndex++)
            {
                GameplayOutcomeProvenanceReferenceSnapshot provenanceRow = memory.provenance[provenanceIndex]
                    ?? throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' has null receipt provenance.");
                _ = new GameplayOutcomeProvenanceReference(provenanceRow.kindId, provenanceRow.value);
                string keyText = provenanceRow.kindId + "|" + provenanceRow.value;
                if (previousProvenance != null && string.CompareOrdinal(previousProvenance, keyText) >= 0)
                    throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' has non-canonical receipt provenance.");
                previousProvenance = keyText;
            }
            HashSet<GameplayOutcomeFactId> compactedFacts = new HashSet<GameplayOutcomeFactId>();
            for (int factIndex = 0; factIndex < memory.facts.Count; factIndex++)
            {
                GameplayOutcomeFactSnapshot fact = memory.facts[factIndex]
                    ?? throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' has null immutable fact.");
                GameplayOutcomeFactId factId = new GameplayOutcomeFactId(fact.factId);
                if (!compactedFacts.Add(factId)
                    || !IsValidBoundedUtf16(
                        fact.value,
                        GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                        allowEmpty: false))
                    throw new InvalidOperationException($"Compacted gameplay memory '{memory.memoryId}' has invalid immutable facts.");
            }
            CompactedMemoryKey key = new CompactedMemoryKey(subjectId, signature);
            if (!candidate.CompactedByKey.TryAdd(key, candidate.CompactedMemories.Count))
                throw new InvalidOperationException($"Duplicate compacted gameplay memory key '{memory.signature}'.");
            candidate.CompactedMemories.Add(memory);
        }
    }

    private void RestoreConsolidationJobs(
        GameplayOutcomeLedgerRuntimeState candidate,
        List<GameplayOutcomeConsolidationJobSnapshot> source,
        long savedWorldEpoch,
        long restoredWorldEpoch)
    {
        long previousCutoff = 0L;
        int previousEvaluationDay = -1;
        int previousPolicyVersion = -1;
        for (int index = 0; index < source.Count; index++)
        {
            GameplayOutcomeConsolidationJobSnapshot job = source[index]
                ?? throw new InvalidOperationException("Gameplay outcome restore contains a null consolidation job.");
            if (job.worldEpoch != savedWorldEpoch
                || job.evaluationDay < 0
                || job.cutoffSequence <= 0L
                || job.cutoffSequence >= candidate.NextSequence
                || job.policyVersion <= 0
                || job.policyVersion != registry.ConsolidationPolicyVersion
                || job.nextPublishedSlot != 0
                || job.nextSequence <= 0L
                || job.nextSequence > job.cutoffSequence + 1L)
                throw new InvalidOperationException("Gameplay outcome restore contains an invalid consolidation job.");
            if (index > 0
                && (job.cutoffSequence < previousCutoff
                    || job.cutoffSequence == previousCutoff
                        && (job.evaluationDay < previousEvaluationDay
                            || job.evaluationDay == previousEvaluationDay
                                && job.policyVersion <= previousPolicyVersion)))
            {
                throw new InvalidOperationException(
                    "Gameplay outcome restore contains duplicate or non-canonically ordered consolidation jobs.");
            }
            previousCutoff = job.cutoffSequence;
            previousEvaluationDay = job.evaluationDay;
            previousPolicyVersion = job.policyVersion;
            candidate.ConsolidationJobs.Add(new GameplayOutcomeConsolidationJob
            {
                EvaluationDay = job.evaluationDay,
                CutoffSequence = job.cutoffSequence,
                PolicyVersion = job.policyVersion,
                WorldEpoch = restoredWorldEpoch,
                NextDueIndex = 0,
                NextSequence = job.nextSequence,
                // Scratch and due snapshots are deliberately omitted from a
                // save. The queue head rebuilds from committed due metadata.
                DuePageIndices = null
            });
        }
    }

    private static void ValidateRestoreReferences(GameplayOutcomeLedgerRuntimeState candidate)
    {
        HashSet<string> knownOutcomeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GameplayOutcomeId id in candidate.ActiveByOutcomeId.Keys)
        {
            if (!knownOutcomeIds.Add(id.ToString()))
                throw new InvalidOperationException("Gameplay outcome restore contains a duplicate exact outcome ID.");
        }
        for (int index = 0; index < candidate.Tombstones.Count; index++)
        {
            GameplayOutcomeTombstoneSnapshot tombstone = candidate.Tombstones[index];
            if (!knownOutcomeIds.Add(tombstone.runId + "/" + tombstone.sequence))
                throw new InvalidOperationException("Gameplay outcome restore contains a duplicate tombstone outcome ID.");
        }
        for (int pageIndex = 0; pageIndex < candidate.Pool.AddressableCount; pageIndex++)
        {
            GameplayOutcomeValuePage page = candidate.Pool.Get(pageIndex);
            if (page == null || page.State == GameplayOutcomePageState.Free)
                continue;
            if (page.Causation.HasParent && !knownOutcomeIds.Contains(page.Causation.ParentOutcomeId.ToString()))
                throw new InvalidOperationException($"Gameplay outcome '{page.OutcomeId}' has a broken parent outcome reference.");
            // Anchor referent semantics are owned by typed integration ports;
            // the ledger validates only the generic stable-ID contract here.
        }
    }

    private static void ValidateNextSequence(GameplayOutcomeLedgerRuntimeState candidate)
    {
        long maximum = 0L;
        foreach (GameplayOutcomeId id in candidate.ActiveByOutcomeId.Keys)
            maximum = Math.Max(maximum, id.Sequence);
        for (int index = 0; index < candidate.Tombstones.Count; index++)
            maximum = Math.Max(maximum, candidate.Tombstones[index].sequence);
        if (candidate.NextSequence <= maximum)
            throw new InvalidOperationException("Gameplay outcome restore next sequence does not exceed all stored outcomes.");
    }

    private static void ValidateTopLevelPayload(GameplayOutcomeLedgerSaveData payload)
    {
        if (payload.version != GameplayOutcomeLedgerSaveData.CurrentVersion)
            throw new InvalidOperationException($"Unsupported gameplay outcome payload version {payload.version}.");
        _ = new GameplayOutcomeRunId(payload.runId);
        if (payload.worldEpoch <= 0L
            || payload.revision < 0L
            || payload.nextSequence <= 0L
            || payload.notificationFaultCount < 0
            || payload.knownResultKeyHighWater < 0
            || payload.knownResultKeyHighWater > GameplayOutcomeBufferLimits.AbsoluteMaximumKnownResultKeys
            || payload.capacityDeferredCount < 0L
            || payload.consecutiveCapacityDeferredCount < 0
            || payload.capacityDeferredStreakHighWater < payload.consecutiveCapacityDeferredCount
            || payload.exactPageUseHighWater < 0
            || payload.retentionPromotionFaultCount < 0
            || payload.lastScheduledEvaluationDay < -1
            || payload.lastScheduledCutoffSequence < 0L
            || payload.lastScheduledEvaluationDay < 0 && payload.lastScheduledCutoffSequence != 0L
            || payload.lastScheduledCutoffSequence >= payload.nextSequence
            || payload.outbox == null
            || payload.exactOutcomes == null
            || payload.compactedMemories == null
            || payload.tombstones == null
            || payload.consolidationJobs == null
            || payload.perspectiveCache == null
            || payload.perspectiveCache.Count != 0)
            throw new InvalidOperationException("Gameplay outcome restore payload is incomplete or invalid.");
    }

    private static void RequireSnapshotCollections(GameplayOutcomeSnapshot snapshot)
    {
        if (snapshot == null
            || snapshot.participants == null
            || snapshot.metrics == null
            || snapshot.subjects == null
            || snapshot.initialSubjects == null
            || snapshot.tags == null
            || snapshot.anchors == null
            || snapshot.initialAnchors == null
            || snapshot.provenance == null
            || snapshot.facts == null)
            throw new InvalidOperationException("Gameplay outcome restore contains a null record or required collection.");
    }

    private static bool IsSha256(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (!(current >= '0' && current <= '9')
                && !(current >= 'a' && current <= 'f'))
                return false;
        }
        return true;
    }

    private static int CompareSnapshotsBySequence(GameplayOutcomeSnapshot left, GameplayOutcomeSnapshot right) =>
        left.sequence.CompareTo(right.sequence);

    private static GameplayOutcomeTombstoneSnapshot CloneTombstone(GameplayOutcomeTombstoneSnapshot source) =>
        new GameplayOutcomeTombstoneSnapshot
        {
            runId = source.runId,
            sequence = source.sequence,
            producerId = source.producerId,
            operationId = source.operationId,
            commitRevision = source.commitRevision,
            localResultIndex = source.localResultIndex,
            ownerRevision = source.ownerRevision,
            terminalTier = source.terminalTier,
            canonicalHash = source.canonicalHash,
            influenceUseCount = source.influenceUseCount,
            influenceRevision = source.influenceRevision
        };

    private readonly struct NarrativeAnchorKey : IEquatable<NarrativeAnchorKey>
    {
        public NarrativeAnchorKey(GameplayEntityId subjectId, NarrativeEvidenceReference reference)
        {
            SubjectId = subjectId;
            Reference = reference;
        }
        public GameplayEntityId SubjectId { get; }
        public NarrativeEvidenceReference Reference { get; }
        public bool Equals(NarrativeAnchorKey other) => SubjectId.Equals(other.SubjectId) && Reference.Equals(other.Reference);
        public override bool Equals(object obj) => obj is NarrativeAnchorKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SubjectId, Reference);
    }
}

public sealed class GameplayOutcomeLedgerSaveSection :
    DungeonStrictJsonSaveSection<GameplayOutcomeLedgerSaveData, GameplayOutcomeLedgerRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "world.gameplay-outcome-ledger";
    private readonly IGameplayOutcomePersistence persistence;

    public GameplayOutcomeLedgerSaveSection(IGameplayOutcomePersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));

    public override string SectionId => Id;
    public override int SectionVersion => GameplayOutcomeLedgerSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;

    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(
            payloadJson,
            "outbox",
            "exactOutcomes",
            "compactedMemories",
            "tombstones",
            "consolidationJobs",
            "perspectiveCache");

    protected override GameplayOutcomeLedgerSaveData CapturePayload() =>
        persistence.CaptureGameplayOutcomes();

    protected override GameplayOutcomeLedgerRestoreCandidate BuildRestoreCandidate(
        GameplayOutcomeLedgerSaveData payload) =>
        persistence.PrepareGameplayOutcomeRestore(payload);

    protected override void PublishRestoreCandidate(
        GameplayOutcomeLedgerRestoreCandidate candidate) =>
        persistence.PublishGameplayOutcomeRestore(candidate);
}
