using System;
using System.Collections.Generic;
using DungeonStory.Narrative.Korean;

public sealed partial class GameplayOutcomeLedger :
    IGameplayOutcomeQuery,
    IGameplayOutcomeMemoryCommands,
    IGameplayOutcomeConsolidationService,
    IGameplayOutcomeDiagnosticsQuery,
    IGameplayOutcomePersistence,
    IDungeonRestoreTransactionParticipant
{
    private readonly object gate = new object();
    private readonly IGameplayOutcomeRegistry registry;
    private readonly GameplayOutcomeBufferLimits configuredLimits;
    private readonly GameplayOutcomeCanonicalHash.ExactDigestWriter exactDigestWriter;
    private GameplayOutcomeLedgerRuntimeState state;
    private GameplayOutcomeLedgerRuntimeState stagedRestoreState;
    private GameplayOutcomeLedgerRuntimeState previousRestoreState;
    private bool restoreTransactionBegun;
    private bool restoreCandidatePublished;

    public GameplayOutcomeLedger(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeBufferLimits limits)
        : this(
            registry,
            limits,
            new GameplayOutcomeRunId("run:" + Guid.NewGuid().ToString("N")),
            1L)
    {
    }

    public GameplayOutcomeLedger(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeBufferLimits limits,
        GameplayOutcomeRunId runId,
        long worldEpoch)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        configuredLimits = limits ?? throw new ArgumentNullException(nameof(limits));
        exactDigestWriter = new GameplayOutcomeCanonicalHash.ExactDigestWriter();
        state = new GameplayOutcomeLedgerRuntimeState(runId, worldEpoch, limits);
        GameplayOutcomeVisibilityRules.ValidateExactSubjectTierContract();
    }

    public string ParticipantId => "gameplay-outcome-ledger";

    public long CurrentWorldEpoch
    {
        get
        {
            lock (gate)
                return state.WorldEpoch;
        }
    }

    internal OutcomePrepareResult TryReserveCore(
        in OutcomeWriteRequirements requirements,
        out PreparedOutcomeReservation reservation)
    {
        reservation = default;
        if (!ValidateRequirements(requirements, out string validationFailure))
            return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, validationFailure);

        lock (gate)
        {
            if (requirements.WorldEpoch != state.WorldEpoch)
                return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, "world-epoch-mismatch");
            if (!registry.TryGetDescriptor(requirements.OutcomeTypeId, out _))
                return new OutcomePrepareResult(OutcomePrepareCode.DescriptorMissing, "descriptor-missing");
            if (state.KnownResultKeys.Contains(requirements.ResultKey))
            {
                TryGetResultIdentityNoLock(requirements.ResultKey, out GameplayOutcomeReplayIdentity existing);
                return new OutcomePrepareResult(
                    OutcomePrepareCode.DuplicateResult,
                    "result-key-already-known",
                    existing);
            }
            if (state.KnownResultKeys.Count >= state.Limits.KnownResultKeyCapacity)
            {
                RecordCapacityDeferredNoLock();
                return new OutcomePrepareResult(
                    OutcomePrepareCode.CapacityDeferred,
                    "known-result-key-capacity-deferred");
            }
            if (!state.Limits.CanFit(requirements)
                || !state.Pool.TryRent(requirements, out int pageIndex, out GameplayOutcomeValuePage page))
            {
                RecordCapacityDeferredNoLock();
                return new OutcomePrepareResult(OutcomePrepareCode.CapacityDeferred, "outcome-page-capacity-deferred");
            }

            state.ConsecutiveCapacityDeferredCount = 0;
            int pagesInUse = state.Pool.PooledInUseCount;
            state.ExactPageUseHighWater = Math.Max(state.ExactPageUseHighWater, pagesInUse);
            state.KnownResultKeys.Add(requirements.ResultKey);
            state.KnownResultKeyHighWater = Math.Max(
                state.KnownResultKeyHighWater,
                state.KnownResultKeys.Count);
            state.ActiveByResultKey.Add(requirements.ResultKey, pageIndex);
            state.ReservedCount++;
            reservation = new PreparedOutcomeReservation(
                pageIndex,
                page.Generation,
                state.WorldEpoch,
                requirements.ResultKey);
            return OutcomePrepareResult.Prepared();
        }
    }

    internal bool TryGetReservedPage(
        in PreparedOutcomeReservation reservation,
        out GameplayOutcomeValuePage page)
    {
        lock (gate)
        {
            page = state.Pool.Get(reservation.PageIndex);
            return page != null
                && reservation.WorldEpoch == state.WorldEpoch
                && page.Generation == reservation.PageGeneration
                && page.State == GameplayOutcomePageState.Reserved
                && page.ResultKey == reservation.ResultKey;
        }
    }

    internal OutcomePrepareResult CompletePreparation(
        in PreparedOutcomeReservation reservation,
        in OutcomeWriteRequirements requirements,
        out PreparedOutcomeToken prepared)
    {
        prepared = default;
        lock (gate)
        {
            GameplayOutcomeValuePage page = state.Pool.Get(reservation.PageIndex);
            if (!MatchesReservation(page, reservation))
                return new OutcomePrepareResult(OutcomePrepareCode.ReservationInvalid, "reservation-not-active");
            if (requirements.ResultKey != page.ResultKey
                || requirements.OutcomeTypeId != page.OutcomeTypeId
                || requirements.OwnerRevision != page.OwnerRevision
                || requirements.WorldEpoch != page.WorldEpoch)
            {
                return new OutcomePrepareResult(OutcomePrepareCode.ReservationInvalid, "reservation-requirements-mismatch");
            }
            if (!registry.TryGetDescriptor(page.OutcomeTypeId, out IGameplayOutcomeDescriptor descriptor))
                return new OutcomePrepareResult(OutcomePrepareCode.DescriptorMissing, "descriptor-missing");

            FilterOptionalWitnessLinks(page, descriptor.PerceptionPolicy);
            NormalizeInitialSubjectTiers(page);
            if (!ValidateDirectSubjectCoverage(page, out string coverageFailure))
                return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, coverageFailure);

            page.Sequence = state.NextSequence;
            page.OutcomeId = new GameplayOutcomeId(state.RunId, page.Sequence);
            GameplayOutcomeReadView view = new GameplayOutcomeReadView(page);
            OutcomeValidationResult validation = ValidateDescriptorVocabulary(page, descriptor);
            if (validation.Valid)
                validation = descriptor.Validate(view);
            if (!validation.Valid)
            {
                page.Sequence = 0L;
                page.OutcomeId = default;
                return new OutcomePrepareResult(
                    OutcomePrepareCode.InvalidReceipt,
                    string.IsNullOrEmpty(validation.DetailCode) ? "descriptor-rejected" : validation.DetailCode);
            }

            page.CaptureImmutableRows();
            exactDigestWriter.Write(
                new GameplayOutcomeReadView(page),
                page.ImmutablePayloadDigest);
            page.HasImmutablePayloadDigest = true;
            page.ImmutablePayloadHash = string.Empty;
            page.State = GameplayOutcomePageState.Prepared;
            state.NextSequence++;
            state.ActiveByOutcomeId.Add(page.OutcomeId, reservation.PageIndex);
            prepared = new PreparedOutcomeToken(
                reservation.PageIndex,
                reservation.PageGeneration,
                state.WorldEpoch,
                page.OwnerRevision,
                page.ResultKey,
                page.OutcomeId);
            return OutcomePrepareResult.Prepared();
        }
    }

    internal void CancelReservationCore(in PreparedOutcomeReservation reservation)
    {
        lock (gate)
        {
            GameplayOutcomeValuePage page = state.Pool.Get(reservation.PageIndex);
            if (!MatchesReservation(page, reservation))
                return;
            state.ActiveByResultKey.Remove(page.ResultKey);
            state.KnownResultKeys.Remove(page.ResultKey);
            state.ReservedCount--;
            state.Pool.Return(reservation.PageIndex, reservation.PageGeneration);
        }
    }

    internal void CancelPreparedCore(in PreparedOutcomeToken prepared)
    {
        lock (gate)
        {
            GameplayOutcomeValuePage page = state.Pool.Get(prepared.PageIndex);
            if (!MatchesPrepared(page, prepared) || page.State != GameplayOutcomePageState.Prepared)
                return;
            state.ActiveByResultKey.Remove(page.ResultKey);
            state.ActiveByOutcomeId.Remove(page.OutcomeId);
            state.KnownResultKeys.Remove(page.ResultKey);
            state.ReservedCount--;
            state.Pool.Return(prepared.PageIndex, prepared.PageGeneration);
        }
    }

    internal OutcomeCommitResult CommitPreparedCore(
        in PreparedOutcomeToken prepared,
        long expectedOwnerRevision,
        out CommittedOutcomeToken committed)
    {
        committed = default;
        lock (gate)
        {
            GameplayOutcomeValuePage page = state.Pool.Get(prepared.PageIndex);
            if (!MatchesPrepared(page, prepared))
                return new OutcomeCommitResult(OutcomeCommitCode.InvalidPreparedToken, "prepared-token-invalid");
            if (prepared.WorldEpoch != state.WorldEpoch)
                return new OutcomeCommitResult(OutcomeCommitCode.WorldEpochMismatch, "world-epoch-mismatch");
            if (page.OwnerRevision != expectedOwnerRevision)
                return new OutcomeCommitResult(OutcomeCommitCode.OwnerRevisionMismatch, "owner-revision-mismatch");
            if (page.State == GameplayOutcomePageState.Committed
                || page.State == GameplayOutcomePageState.DeliveryFaultPending
                || page.State == GameplayOutcomePageState.PublishedAwaitingAcknowledgement
                || page.State == GameplayOutcomePageState.PublishedAcknowledged)
            {
                committed = CreateCommittedToken(prepared.PageIndex, page);
                return new OutcomeCommitResult(OutcomeCommitCode.AlreadyCommitted, string.Empty);
            }
            if (page.State != GameplayOutcomePageState.Prepared)
                return new OutcomeCommitResult(OutcomeCommitCode.InvalidPreparedToken, "prepared-token-not-prepared");

            page.CommitGeneration = page.ResultKey.CommitRevision;
            page.State = GameplayOutcomePageState.Committed;
            state.ReservedCount--;
            state.PendingDeliveryCount++;
            state.Revision++;
            committed = CreateCommittedToken(prepared.PageIndex, page);
            return new OutcomeCommitResult(OutcomeCommitCode.Committed, string.Empty);
        }
    }

    internal OutcomeDeliveryResult TryPublishCore(in CommittedOutcomeToken committed)
    {
        lock (gate)
        {
            GameplayOutcomeValuePage page = state.Pool.Get(committed.PageIndex);
            if (!MatchesCommitted(page, committed))
                return new OutcomeDeliveryResult(OutcomeDeliveryCode.InvalidCommittedToken, default, "committed-token-invalid");
            if (committed.WorldEpoch != state.WorldEpoch)
                return new OutcomeDeliveryResult(OutcomeDeliveryCode.WorldEpochMismatch, committed.OutcomeId, "world-epoch-mismatch");
            if (page.State == GameplayOutcomePageState.PublishedAwaitingAcknowledgement
                || page.State == GameplayOutcomePageState.PublishedAcknowledged)
            {
                return new OutcomeDeliveryResult(OutcomeDeliveryCode.AlreadyPublished, page.OutcomeId, string.Empty);
            }
            if (page.State != GameplayOutcomePageState.Committed
                && page.State != GameplayOutcomePageState.DeliveryFaultPending)
            {
                return new OutcomeDeliveryResult(OutcomeDeliveryCode.InvalidCommittedToken, page.OutcomeId, "outbox-entry-not-committed");
            }

            try
            {
                if (!registry.TryGetDescriptor(page.OutcomeTypeId, out IGameplayOutcomeDescriptor descriptor))
                    return SetDeliveryFault(page, OutcomeDeliveryCode.DescriptorRejected, "descriptor-missing");
                OutcomeValidationResult validation = ValidateDescriptorVocabulary(page, descriptor);
                if (validation.Valid)
                    validation = descriptor.Validate(new GameplayOutcomeReadView(page));
                if (!validation.Valid)
                {
                    return SetDeliveryFault(
                        page,
                        OutcomeDeliveryCode.DescriptorRejected,
                        string.IsNullOrEmpty(validation.DetailCode) ? "descriptor-rejected" : validation.DetailCode);
                }

                page.State = GameplayOutcomePageState.PublishedAwaitingAcknowledgement;
                page.PublishedRevision = ++state.Revision;
                page.PublishedListSlot = state.PublishedPageIndices.Count;
                state.PublishedPageIndices.Add(committed.PageIndex);
                MarkDuePageDirty(committed.PageIndex, page);
                state.PendingDeliveryCount--;
                state.PublishedCount++;
                return new OutcomeDeliveryResult(OutcomeDeliveryCode.Published, page.OutcomeId, string.Empty);
            }
            catch (Exception exception) when (IsRecoverableDeliveryException(exception))
            {
                return SetDeliveryFault(page, OutcomeDeliveryCode.PendingDeliveryFault, "delivery-subsystem-fault");
            }
        }
    }

    internal OutcomeAcknowledgeResult AcknowledgeCore(in CommittedOutcomeToken committed)
    {
        lock (gate)
        {
            GameplayOutcomeValuePage page = state.Pool.Get(committed.PageIndex);
            if (!MatchesCommitted(page, committed))
                return new OutcomeAcknowledgeResult(OutcomeAcknowledgeCode.InvalidCommittedToken);
            if (page.State == GameplayOutcomePageState.PublishedAcknowledged)
            {
                if (!TryPromoteAcknowledgedPage(committed.PageIndex, page))
                {
                    RecordRetentionPromotionFaultNoLock();
                    return new OutcomeAcknowledgeResult(
                        OutcomeAcknowledgeCode.RetentionPromotionDeferred);
                }
                return new OutcomeAcknowledgeResult(OutcomeAcknowledgeCode.AlreadyAcknowledged);
            }
            if (page.State != GameplayOutcomePageState.PublishedAwaitingAcknowledgement)
                return new OutcomeAcknowledgeResult(OutcomeAcknowledgeCode.NotPublished);
            page.State = GameplayOutcomePageState.PublishedAcknowledged;
            if (!TryPromoteAcknowledgedPage(committed.PageIndex, page))
            {
                // Promotion owns the pre-reserved retained slab and is the
                // durable acknowledgement boundary. Keep the outbox row
                // retryable when an invariant fault prevents that transfer.
                page.State = GameplayOutcomePageState.PublishedAwaitingAcknowledgement;
                RecordRetentionPromotionFaultNoLock();
                return new OutcomeAcknowledgeResult(
                    OutcomeAcknowledgeCode.RetentionPromotionDeferred);
            }
            state.Revision++;
            return new OutcomeAcknowledgeResult(OutcomeAcknowledgeCode.Acknowledged);
        }
    }

    private void RecordCapacityDeferredNoLock()
    {
        if (state.CapacityDeferredCount < long.MaxValue)
            state.CapacityDeferredCount++;
        if (state.ConsecutiveCapacityDeferredCount < int.MaxValue)
            state.ConsecutiveCapacityDeferredCount++;
        state.CapacityDeferredStreakHighWater = Math.Max(
            state.CapacityDeferredStreakHighWater,
            state.ConsecutiveCapacityDeferredCount);
    }

    private void RecordRetentionPromotionFaultNoLock()
    {
        if (state.RetentionPromotionFaultCount < int.MaxValue)
            state.RetentionPromotionFaultCount++;
    }

    private bool TryPromoteAcknowledgedPage(int pageIndex, GameplayOutcomeValuePage page)
    {
        if (page == null || page.IsDetached)
            return true;
        // Acknowledgement is the cold promotion boundary. Flush mutable due
        // metadata and reserve all long-run collections here so later hot
        // reserve/publish and anchor commits stay within existing capacity.
        FlushDirtyDuePages();
        int sourceGeneration = page.Generation;
        if (!state.Pool.TryPromotePublishedToDetached(
            pageIndex,
            sourceGeneration,
            out int detachedPageIndex,
            out GameplayOutcomeValuePage detached))
            return false;
        EnsureLongRunCapacityAfterPromotion();
        state.ActiveByResultKey[detached.ResultKey] = detachedPageIndex;
        state.ActiveByOutcomeId[detached.OutcomeId] = detachedPageIndex;
        if (detached.PublishedListSlot >= 0
            && detached.PublishedListSlot < state.PublishedPageIndices.Count)
            state.PublishedPageIndices[detached.PublishedListSlot] = detachedPageIndex;
        RemoveDuePage(pageIndex);
        RegisterDuePage(detachedPageIndex, detached);
        state.Pool.Return(pageIndex, sourceGeneration);
        return true;
    }

    private void EnsureLongRunCapacityAfterPromotion()
        => EnsureLongRunCapacity(state);

    private static void EnsureLongRunCapacity(GameplayOutcomeLedgerRuntimeState target)
    {
        int nextPublished = target.Pool.AddressableCount;
        if (target.PublishedPageIndices.Capacity < nextPublished)
            target.PublishedPageIndices.Capacity = nextPublished;
        int nextAddressable = target.Pool.AddressableCount;
        if (target.DirtyDuePageIndices.Capacity < nextAddressable)
            target.DirtyDuePageIndices.Capacity = nextAddressable;
        target.ActiveByResultKey.EnsureCapacity(nextAddressable);
        target.ActiveByOutcomeId.EnsureCapacity(nextAddressable);
        target.DueByPageIndex.EnsureCapacity(nextAddressable);
        target.KnownResultKeys.EnsureCapacity(Math.Min(
            target.Limits.KnownResultKeyCapacity,
            checked(target.KnownResultKeys.Count + 1)));
    }

    internal bool TryGetPendingCommittedToken(int startPageIndex, out int pageIndex, out CommittedOutcomeToken token)
    {
        lock (gate)
        {
            for (int index = Math.Max(0, startPageIndex); index < state.Pool.AddressableCount; index++)
            {
                GameplayOutcomeValuePage page = state.Pool.Get(index);
                if (page == null
                    || page.State != GameplayOutcomePageState.Committed
                    && page.State != GameplayOutcomePageState.DeliveryFaultPending
                    && page.State != GameplayOutcomePageState.PublishedAwaitingAcknowledgement)
                    continue;
                pageIndex = index;
                token = CreateCommittedToken(index, page);
                return true;
            }
            pageIndex = -1;
            token = default;
            return false;
        }
    }

    internal void RecordNotificationFault()
    {
        lock (gate)
            state.NotificationFaultCount++;
    }

    public GameplayOutcomeLedgerDiagnostics GetDiagnostics()
    {
        lock (gate)
        {
            return new GameplayOutcomeLedgerDiagnostics(
                state.WorldEpoch,
                state.Revision,
                state.NextSequence,
                state.Pool.FreeSmallCount,
                state.Pool.FreeLargeCount,
                state.ReservedCount,
                state.PendingDeliveryCount,
                state.PublishedCount,
                state.CompactedMemories.Count,
                state.ForgottenCount,
                state.NotificationFaultCount,
                state.KnownResultKeys.Count,
                state.Limits.KnownResultKeyCapacity,
                state.KnownResultKeyHighWater,
                state.ActiveAnchorReservationCount,
                state.CapacityDeferredCount,
                state.ConsecutiveCapacityDeferredCount,
                state.CapacityDeferredStreakHighWater,
                state.ExactPageUseHighWater,
                state.Pool.DetachedInUseCount,
                state.Tombstones.Count,
                state.ActiveInfluenceReservationCount,
                state.Pool.FreeRetainedSmallCount,
                state.Pool.FreeRetainedLargeCount,
                state.RetentionPromotionFaultCount);
        }
    }

    public IReadOnlyList<GameplayOutcomeOutboxSnapshot> GetOutboxSnapshot()
    {
        lock (gate)
        {
            List<GameplayOutcomeOutboxSnapshot> result = new List<GameplayOutcomeOutboxSnapshot>();
            for (int index = 0; index < state.Pool.AddressableCount; index++)
            {
                GameplayOutcomeValuePage page = state.Pool.Get(index);
                if (page == null
                    || page.State != GameplayOutcomePageState.Committed
                    && page.State != GameplayOutcomePageState.DeliveryFaultPending
                    && page.State != GameplayOutcomePageState.PublishedAwaitingAcknowledgement)
                    continue;
                result.Add(new GameplayOutcomeOutboxSnapshot(
                    page.ResultKey,
                    page.OutcomeId,
                    ToPublicLifecycle(page.State),
                    page.DeliveryFaultCount,
                    page.LastDeliveryFaultCode));
            }
            return result;
        }
    }

    public bool TryGetResultIdentity(
        GameplayResultKey resultKey,
        out GameplayOutcomeReplayIdentity identity)
    {
        lock (gate)
            return TryGetResultIdentityNoLock(resultKey, out identity);
    }

    internal OutcomePrepareResult TryCreateReplayComparisonCore(
        in OutcomeWriteRequirements requirements,
        out GameplayOutcomeReplayIdentity baseline,
        out GameplayOutcomeValuePage comparison)
    {
        baseline = default;
        comparison = null;
        if (!ValidateRequirements(requirements, out string validationFailure))
            return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, validationFailure);
        lock (gate)
        {
            if (requirements.WorldEpoch != state.WorldEpoch)
                return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, "world-epoch-mismatch");
            if (!TryGetResultIdentityNoLock(requirements.ResultKey, out baseline))
                return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, "duplicate-result-no-longer-known");
            if (!baseline.HasCanonicalPayloadHash || !baseline.OutcomeId.IsValid)
            {
                return new OutcomePrepareResult(
                    OutcomePrepareCode.DuplicateResult,
                    "duplicate-result-not-reconcilable",
                    baseline);
            }
            if (!configuredLimits.CanFit(requirements))
            {
                return new OutcomePrepareResult(
                    OutcomePrepareCode.CapacityDeferred,
                    "replay-comparison-capacity-deferred",
                    baseline);
            }

            // Duplicate reconciliation is a cold recovery path. Its detached
            // exact-size arrays never consume or overwrite live pool pages.
            comparison = new GameplayOutcomeValuePage(
                requirements.ParticipantCount,
                requirements.MetricCount,
                requirements.SubjectCount,
                requirements.TagCount,
                requirements.AnchorCount,
                requirements.ProvenanceCount,
                requirements.FactCount,
                large: true);
            comparison.Reserve(requirements);
            comparison.OutcomeId = baseline.OutcomeId;
            comparison.Sequence = baseline.OutcomeId.Sequence;
            return OutcomePrepareResult.Prepared();
        }
    }

    internal OutcomePrepareResult CompleteReplayComparisonCore(
        GameplayOutcomeValuePage comparison,
        in GameplayOutcomeReplayIdentity baseline)
    {
        if (comparison == null || !baseline.HasCanonicalPayloadHash)
            return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, "replay-comparison-invalid", baseline);
        lock (gate)
        {
            GameplayOutcomeReplayIdentity current = default;
            if (comparison.WorldEpoch != state.WorldEpoch
                || !TryGetResultIdentityNoLock(comparison.ResultKey, out current)
                || current.OutcomeId != baseline.OutcomeId)
            {
                return new OutcomePrepareResult(
                    OutcomePrepareCode.DuplicateResult,
                    "duplicate-result-changed-during-reconciliation",
                    current);
            }
            if (!registry.TryGetDescriptor(comparison.OutcomeTypeId, out IGameplayOutcomeDescriptor descriptor))
                return new OutcomePrepareResult(OutcomePrepareCode.DescriptorMissing, "descriptor-missing", current);

            FilterOptionalWitnessLinks(comparison, descriptor.PerceptionPolicy);
            NormalizeInitialSubjectTiers(comparison);
            if (!ValidateDirectSubjectCoverage(comparison, out string coverageFailure))
                return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, coverageFailure, current);
            OutcomeValidationResult validation = ValidateDescriptorVocabulary(comparison, descriptor);
            if (validation.Valid)
                validation = descriptor.Validate(new GameplayOutcomeReadView(comparison));
            if (!validation.Valid)
            {
                return new OutcomePrepareResult(
                    OutcomePrepareCode.InvalidReceipt,
                    string.IsNullOrEmpty(validation.DetailCode) ? "descriptor-rejected" : validation.DetailCode,
                    current);
            }

            comparison.CaptureImmutableRows();
            string candidateHash = GameplayOutcomeCanonicalHash.HashExact(
                new GameplayOutcomeReadView(comparison));
            if (!string.Equals(candidateHash, current.CanonicalPayloadHash, StringComparison.Ordinal))
            {
                return new OutcomePrepareResult(
                    OutcomePrepareCode.ConflictingResult,
                    "result-key-payload-conflict",
                    current);
            }
            return new OutcomePrepareResult(
                ToReplayPrepareCode(current.State),
                "result-key-identical-replay",
                current);
        }
    }

    private bool TryGetResultIdentityNoLock(
        GameplayResultKey resultKey,
        out GameplayOutcomeReplayIdentity identity)
    {
        if (resultKey.IsValid
            && state.ActiveByResultKey.TryGetValue(resultKey, out int pageIndex))
        {
            GameplayOutcomeValuePage page = state.Pool.Get(pageIndex);
            GameplayOutcomeReplayState replayState = ToReplayState(page.State);
            string hash = page.State >= GameplayOutcomePageState.Prepared
                ? page.MaterializeImmutablePayloadHash()
                : string.Empty;
            identity = new GameplayOutcomeReplayIdentity(
                resultKey,
                page.OutcomeId,
                replayState,
                hash);
            return true;
        }
        if (state.TombstoneByResultKey.TryGetValue(resultKey, out int tombstoneIndex))
        {
            GameplayOutcomeTombstoneSnapshot tombstone = state.Tombstones[tombstoneIndex];
            identity = new GameplayOutcomeReplayIdentity(
                resultKey,
                new GameplayOutcomeId(new GameplayOutcomeRunId(tombstone.runId), tombstone.sequence),
                tombstone.terminalTier == NarrativeMemoryTier.Compacted
                    ? GameplayOutcomeReplayState.Compacted
                    : GameplayOutcomeReplayState.Forgotten,
                tombstone.canonicalHash);
            return true;
        }
        identity = default;
        return false;
    }

    private static GameplayOutcomeReplayState ToReplayState(GameplayOutcomePageState value) =>
        (GameplayOutcomeReplayState)(int)value;

    private static OutcomePrepareCode ToReplayPrepareCode(GameplayOutcomeReplayState value)
    {
        if (value == GameplayOutcomeReplayState.Committed
            || value == GameplayOutcomeReplayState.DeliveryFaultPending)
            return OutcomePrepareCode.AlreadyCommitted;
        if (value == GameplayOutcomeReplayState.PublishedAwaitingAcknowledgement
            || value == GameplayOutcomeReplayState.PublishedAcknowledged)
            return OutcomePrepareCode.AlreadyPublished;
        if (value == GameplayOutcomeReplayState.Compacted
            || value == GameplayOutcomeReplayState.Forgotten)
            return OutcomePrepareCode.AlreadyTerminal;
        return OutcomePrepareCode.DuplicateResult;
    }

    private OutcomeDeliveryResult SetDeliveryFault(
        GameplayOutcomeValuePage page,
        OutcomeDeliveryCode code,
        string detailCode)
    {
        page.State = GameplayOutcomePageState.DeliveryFaultPending;
        page.DeliveryFaultCount++;
        page.LastDeliveryFaultCode = detailCode;
        state.Revision++;
        return new OutcomeDeliveryResult(code, page.OutcomeId, detailCode);
    }

    private static bool IsRecoverableDeliveryException(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;

    private static bool ValidateRequirements(in OutcomeWriteRequirements requirements, out string failure)
    {
        if (!requirements.ResultKey.IsValid)
        {
            failure = "result-key-invalid";
            return false;
        }
        if (!requirements.OutcomeTypeId.IsValid)
        {
            failure = "outcome-type-invalid";
            return false;
        }
        if (requirements.AbsoluteDay < 0)
        {
            failure = "absolute-day-invalid";
            return false;
        }
        if ((int)requirements.Status < (int)GameplayOutcomeStatus.Succeeded
            || (int)requirements.Status > (int)GameplayOutcomeStatus.Cancelled)
        {
            failure = "outcome-status-invalid";
            return false;
        }
        if (requirements.WorldEpoch <= 0L || requirements.OwnerRevision < 0L)
        {
            failure = "revision-invalid";
            return false;
        }
        if (!requirements.HasNonNegativeCounts)
        {
            failure = "collection-count-invalid";
            return false;
        }
        if (requirements.SubjectCount == 0)
        {
            failure = "subject-link-required";
            return false;
        }
        failure = string.Empty;
        return true;
    }

    private static bool MatchesReservation(GameplayOutcomeValuePage page, in PreparedOutcomeReservation reservation) =>
        page != null
        && page.Generation == reservation.PageGeneration
        && page.State == GameplayOutcomePageState.Reserved
        && page.WorldEpoch == reservation.WorldEpoch
        && page.ResultKey == reservation.ResultKey;

    private static bool MatchesPrepared(GameplayOutcomeValuePage page, in PreparedOutcomeToken prepared) =>
        page != null
        && page.Generation == prepared.PageGeneration
        && page.WorldEpoch == prepared.WorldEpoch
        && page.ResultKey == prepared.ResultKey
        && page.OutcomeId == prepared.OutcomeId;

    private static bool MatchesCommitted(GameplayOutcomeValuePage page, in CommittedOutcomeToken committed) =>
        page != null
        && page.Generation == committed.PageGeneration
        && page.WorldEpoch == committed.WorldEpoch
        && page.ResultKey == committed.ResultKey
        && page.OutcomeId == committed.OutcomeId;

    private static CommittedOutcomeToken CreateCommittedToken(int pageIndex, GameplayOutcomeValuePage page) =>
        new CommittedOutcomeToken(pageIndex, page.Generation, page.WorldEpoch, page.ResultKey, page.OutcomeId);

    private static GameplayOutcomeRecordLifecycle ToPublicLifecycle(GameplayOutcomePageState value) =>
        (GameplayOutcomeRecordLifecycle)(int)value;

    private static bool ValidateDirectSubjectCoverage(GameplayOutcomeValuePage page, out string failure)
    {
        for (int participantIndex = 0; participantIndex < page.ParticipantCount; participantIndex++)
        {
            GameplayOutcomeParticipant participant = page.Participants[participantIndex];
            if (participant.ParticipationKind != GameplayParticipationKind.Direct)
                continue;
            bool found = false;
            for (int subjectIndex = 0; subjectIndex < page.SubjectCount; subjectIndex++)
            {
                if (page.Subjects[subjectIndex].SubjectId == participant.EntityId)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                failure = "direct-participant-subject-link-missing";
                return false;
            }
        }
        for (int anchorIndex = 0; anchorIndex < page.AnchorCount; anchorIndex++)
        {
            bool found = false;
            GameplayEntityId anchorSubject = page.Anchors[anchorIndex].SubjectId;
            for (int subjectIndex = 0; subjectIndex < page.SubjectCount; subjectIndex++)
            {
                if (page.Subjects[subjectIndex].SubjectId == anchorSubject)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                failure = "anchor-subject-link-missing";
                return false;
            }
        }
        failure = string.Empty;
        return true;
    }

    private static OutcomeValidationResult ValidateDescriptorVocabulary(
        GameplayOutcomeValuePage page,
        IGameplayOutcomeDescriptor descriptor)
    {
        for (int index = 0; index < page.ParticipantCount; index++)
        {
            if (!descriptor.IsKnownRole(page.Participants[index].RoleId))
                return OutcomeValidationResult.Reject("descriptor-role-unknown");
        }
        for (int index = 0; index < page.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = page.Metrics[index];
            if (!descriptor.IsKnownMetric(metric.MetricId, metric.UnitId))
                return OutcomeValidationResult.Reject("descriptor-metric-or-unit-unknown");
        }
        return OutcomeValidationResult.Accepted;
    }

    internal static bool IsValidDisplayNameSnapshot(in KoreanNameSnapshot value)
    {
        if (!IsValidBoundedUtf16(
                value.DisplayText,
                GameplayOutcomeBufferLimits.MaximumDisplayTextUtf16Length,
                allowEmpty: false)
            || !GameplayOutcomeStableIdSyntax.IsValid(value.DisplaySnapshotRevision)
            || value.Locale == null
            || value.Locale.Length > GameplayOutcomeBufferLimits.MaximumLocaleLength
            || !GameplayOutcomeStableIdSyntax.IsValid(value.Locale)
            || !GameplayOutcomeStableIdSyntax.IsValid(value.PronunciationHint.Revision)
            || !IsValidBoundedUtf16(
                value.PronunciationHint.Value ?? string.Empty,
                GameplayOutcomeBufferLimits.MaximumPronunciationValueUtf16Length,
                allowEmpty: true)
            || (int)value.PronunciationHint.Mode < (int)KoreanPronunciationMode.Unknown
            || (int)value.PronunciationHint.Mode > (int)KoreanPronunciationMode.ExplicitFinalConsonant
            || (int)value.PronunciationHint.ExplicitFinalConsonant < (int)KoreanFinalConsonantKind.Unknown
            || (int)value.PronunciationHint.ExplicitFinalConsonant > (int)KoreanFinalConsonantKind.Rieul)
            return false;

        KoreanPronunciationHint hint = value.PronunciationHint;
        if (hint.Mode == KoreanPronunciationMode.Unknown
            || hint.Mode == KoreanPronunciationMode.AutoHangulDisplay)
        {
            return string.IsNullOrEmpty(hint.Value)
                && hint.ExplicitFinalConsonant == KoreanFinalConsonantKind.Unknown;
        }
        if (hint.Mode == KoreanPronunciationMode.ExplicitFinalConsonant)
        {
            return string.IsNullOrEmpty(hint.Value)
                && hint.ExplicitFinalConsonant != KoreanFinalConsonantKind.Unknown;
        }
        return !string.IsNullOrWhiteSpace(hint.Value)
            && hint.ExplicitFinalConsonant == KoreanFinalConsonantKind.Unknown;
    }

    internal static bool IsValidBoundedUtf16(string value, int maximumLength, bool allowEmpty)
    {
        if (value == null || value.Length > maximumLength
            || !allowEmpty && string.IsNullOrWhiteSpace(value))
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsHighSurrogate(current))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                    return false;
            }
            else if (char.IsLowSurrogate(current))
            {
                return false;
            }
        }
        return true;
    }

    private static bool RequiresNeutralFrame(GameplayOutcomeValuePage page)
    {
        for (int index = 0; index < page.ParticipantCount; index++)
        {
            if (page.Participants[index].DisplayName.PronunciationHint.Mode
                == KoreanPronunciationMode.Unknown)
                return true;
        }
        return false;
    }

    private static void FilterOptionalWitnessLinks(
        GameplayOutcomeValuePage page,
        IOutcomePerceptionPolicy policy)
    {
        GameplayOutcomeReadView view = new GameplayOutcomeReadView(page);
        int originalSubjectCount = page.SubjectCount;
        int write = 0;
        int optionalCount = 0;
        for (int index = 0; index < page.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink subject = page.Subjects[index];
            if (!subject.IsOptionalWitness)
            {
                page.Subjects[write++] = subject;
                continue;
            }
            if (subject.IsPinned
                || HasAnchorFor(page, subject.SubjectId)
                || policy.ShouldCreateOptionalWitnessLink(view, subject))
                page.Subjects[write++] = subject;
        }
        Array.Clear(page.Subjects, write, originalSubjectCount - write);
        page.SubjectCount = write;

        SortOptionalWitnesses(page);
        write = 0;
        for (int index = 0; index < page.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink subject = page.Subjects[index];
            bool protectedWitness = subject.IsPinned
                || HasAnchorFor(page, subject.SubjectId);
            if (!subject.IsOptionalWitness
                || protectedWitness
                || optionalCount < policy.MaximumOptionalWitnessLinks)
            {
                page.Subjects[write++] = subject;
                if (subject.IsOptionalWitness && !protectedWitness)
                    optionalCount++;
            }
        }
        Array.Clear(page.Subjects, write, page.SubjectCount - write);
        page.SubjectCount = write;
    }

    private static void SortOptionalWitnesses(GameplayOutcomeValuePage page)
    {
        for (int index = 1; index < page.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink candidate = page.Subjects[index];
            int insert = index;
            while (insert > 0 && CompareSubjectLinks(candidate, page.Subjects[insert - 1]) < 0)
            {
                page.Subjects[insert] = page.Subjects[insert - 1];
                insert--;
            }
            page.Subjects[insert] = candidate;
        }
    }

    private static void NormalizeInitialSubjectTiers(GameplayOutcomeValuePage page)
    {
        for (int index = 0; index < page.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink current = page.Subjects[index];
            bool anchored = current.IsPinned || HasAnchorFor(page, current.SubjectId);
            page.Subjects[index] = new GameplayOutcomeSubjectLink(
                current.SubjectId,
                current.Salience,
                anchored ? NarrativeMemoryTier.Core : NarrativeMemoryTier.Recent,
                current.IsPinned,
                current.IsOptionalWitness,
                current.AnchorRevision,
                anchored ? int.MaxValue : current.NextEvaluationDay,
                current.InfluenceUseCount,
                current.InfluenceRevision);
        }
    }

    private static int CompareSubjectLinks(GameplayOutcomeSubjectLink left, GameplayOutcomeSubjectLink right)
    {
        if (left.IsOptionalWitness != right.IsOptionalWitness)
            return left.IsOptionalWitness ? 1 : -1;
        if (left.IsOptionalWitness)
        {
            int salience = right.Salience.CompareTo(left.Salience);
            if (salience != 0)
                return salience;
        }
        return left.SubjectId.CompareTo(right.SubjectId);
    }

    private void RegisterDuePage(int pageIndex, GameplayOutcomeValuePage page)
        => RegisterDuePage(state, pageIndex, page);

    private void MarkDuePageDirty(int pageIndex, GameplayOutcomeValuePage page)
    {
        if (page == null || page.DueIndexDirty)
            return;
        // Capacity is preallocated for the hot pool and extended only at the
        // detached cold-promotion boundary.
        System.Diagnostics.Debug.Assert(
            state.DirtyDuePageIndices.Count < state.DirtyDuePageIndices.Capacity,
            "Due-index dirty queue capacity was not reserved.");
        page.DueIndexDirty = true;
        state.DirtyDuePageIndices.Add(pageIndex);
    }

    private void FlushDirtyDuePages()
    {
        for (int index = 0; index < state.DirtyDuePageIndices.Count; index++)
        {
            int pageIndex = state.DirtyDuePageIndices[index];
            GameplayOutcomeValuePage page = state.Pool.Get(pageIndex);
            if (page == null || !page.DueIndexDirty)
                continue;
            page.DueIndexDirty = false;
            RegisterDuePage(pageIndex, page);
        }
        state.DirtyDuePageIndices.Clear();
    }

    private static void RegisterDuePage(
        GameplayOutcomeLedgerRuntimeState target,
        int pageIndex,
        GameplayOutcomeValuePage page)
    {
        RemoveDuePage(target, pageIndex);
        if (!IsPublished(page)) return;
        int dueDay = int.MaxValue;
        for (int index = 0; index < page.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink subject = page.Subjects[index];
            if (!GameplayOutcomeVisibilityRules.IsExactSubjectTierVisible(subject.Tier))
                continue;
            dueDay = Math.Min(dueDay, subject.NextEvaluationDay);
        }
        if (dueDay == int.MaxValue) return;
        GameplayOutcomeDueEntry entry = new GameplayOutcomeDueEntry(
            dueDay, page.Sequence, pageIndex, page.Generation);
        int insert = target.DueEntries.BinarySearch(entry);
        if (insert >= 0)
            throw new InvalidOperationException(
                "Duplicate gameplay outcome due-index entry.");
        insert = ~insert;
        target.DueEntries.Insert(insert, entry);
        target.DueByPageIndex.Add(pageIndex, entry);
    }

    private void RemoveDuePage(int pageIndex) => RemoveDuePage(state, pageIndex);

    private static void RemoveDuePage(GameplayOutcomeLedgerRuntimeState target, int pageIndex)
    {
        if (!target.DueByPageIndex.TryGetValue(pageIndex, out GameplayOutcomeDueEntry existing))
            return;
        target.DueByPageIndex.Remove(pageIndex);
        int index = target.DueEntries.BinarySearch(existing);
        if (index >= 0)
            target.DueEntries.RemoveAt(index);
    }

    private List<int> BuildDuePageList(int evaluationDay, long cutoffSequence, long nextSequence)
        => BuildDuePageList(state, evaluationDay, cutoffSequence, nextSequence);

    private static List<int> BuildDuePageList(
        GameplayOutcomeLedgerRuntimeState target,
        int evaluationDay,
        long cutoffSequence,
        long nextSequence)
    {
        List<GameplayOutcomeDueEntry> selected = new List<GameplayOutcomeDueEntry>();
        foreach (GameplayOutcomeDueEntry entry in target.DueEntries)
        {
            if (entry.DueDay > evaluationDay) break;
            if (entry.Sequence >= nextSequence && entry.Sequence <= cutoffSequence)
                selected.Add(entry);
        }
        selected.Sort(CompareDueEntriesBySequence);
        List<int> pages = new List<int>(selected.Count);
        for (int index = 0; index < selected.Count; index++) pages.Add(selected[index].PageIndex);
        return pages;
    }

    private static int CompareDueEntriesBySequence(GameplayOutcomeDueEntry left, GameplayOutcomeDueEntry right)
    {
        int value = left.Sequence.CompareTo(right.Sequence);
        return value != 0 ? value : left.PageIndex.CompareTo(right.PageIndex);
    }
}
