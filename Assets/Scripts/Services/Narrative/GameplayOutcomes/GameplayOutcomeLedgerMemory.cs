using System;
using System.Collections.Generic;
using System.Diagnostics;

public sealed partial class GameplayOutcomeLedger
{
    [GameplayInternalOnly(
        "Mutates only narrative-memory retention metadata for an existing outcome.",
        "Gameplay outcome application/UI command adapter")]
    public bool TrySetPlayerPin(
        GameplayOutcomeId outcomeId,
        GameplayEntityId subjectId,
        bool pinned,
        out string failureCode)
    {
        lock (gate)
        {
            if (!TryGetMutablePublishedPage(outcomeId, out GameplayOutcomeValuePage page))
            {
                failureCode = "outcome-not-active";
                return false;
            }
            if (page.AnchorReservationActive)
            {
                failureCode = "anchor-reservation-active";
                return false;
            }
            int subjectIndex = FindSubjectIndex(page, subjectId);
            if (subjectIndex < 0)
            {
                failureCode = "subject-link-not-found";
                return false;
            }
            GameplayOutcomeSubjectLink current = page.Subjects[subjectIndex];
            if (current.IsPinned == pinned)
            {
                failureCode = string.Empty;
                return true;
            }
            int revision = checked(page.AnchorRevision + 1);
            page.AnchorRevision = revision;
            page.Subjects[subjectIndex] = new GameplayOutcomeSubjectLink(
                current.SubjectId,
                current.Salience,
                pinned ? NarrativeMemoryTier.Core : current.Tier,
                pinned,
                current.IsOptionalWitness,
                revision,
                pinned || HasAnchorFor(page, current.SubjectId) ? int.MaxValue : 0,
                current.InfluenceUseCount,
                current.InfluenceRevision);
            MarkDuePageDirty(state.ActiveByOutcomeId[outcomeId], page);
            state.Revision++;
            failureCode = string.Empty;
            return true;
        }
    }

    [GameplayInternalOnly(
        "Adds an explicit evidence-retention reference without mutating gameplay state.",
        "Narrative evidence selection transaction adapters")]
    public bool TryAddEvidenceAnchor(
        GameplayOutcomeId outcomeId,
        GameplayEntityId subjectId,
        NarrativeEvidenceReference anchor,
        out string failureCode)
    {
        int revision;
        lock (gate)
        {
            if (!TryGetMutablePublishedPage(outcomeId, out GameplayOutcomeValuePage page))
            {
                failureCode = "outcome-not-active";
                return false;
            }
            revision = page.AnchorRevision;
        }
        EvidenceAnchorPrepareResult preparedResult = TryPrepareEvidenceAnchorMutation(
            outcomeId, subjectId, anchor, EvidenceAnchorMutationKind.Add, revision, out PreparedEvidenceAnchorToken prepared);
        if (preparedResult.Code == EvidenceAnchorPrepareCode.AlreadyApplied)
        {
            failureCode = string.Empty;
            return true;
        }
        if (!preparedResult.Success)
        {
            failureCode = preparedResult.DetailCode;
            return false;
        }
        EvidenceAnchorCommitResult committed = CommitEvidenceAnchorMutation(prepared, out _);
        failureCode = committed.Success ? string.Empty : committed.Code.ToString();
        return committed.Success;
    }

    [GameplayInternalOnly(
        "Removes one evidence-retention reference while preserving other anchors.",
        "Narrative evidence selection transaction adapters")]
    public bool TryRemoveEvidenceAnchor(
        GameplayOutcomeId outcomeId,
        GameplayEntityId subjectId,
        NarrativeEvidenceReference anchor,
        out string failureCode)
    {
        int revision;
        lock (gate)
        {
            if (!TryGetMutablePublishedPage(outcomeId, out GameplayOutcomeValuePage page))
            {
                failureCode = "outcome-not-active";
                return false;
            }
            revision = page.AnchorRevision;
        }
        EvidenceAnchorPrepareResult preparedResult = TryPrepareEvidenceAnchorMutation(
            outcomeId, subjectId, anchor, EvidenceAnchorMutationKind.Remove, revision, out PreparedEvidenceAnchorToken prepared);
        if (preparedResult.Code == EvidenceAnchorPrepareCode.AlreadyApplied)
        {
            failureCode = string.Empty;
            return true;
        }
        if (!preparedResult.Success)
        {
            failureCode = preparedResult.DetailCode;
            return false;
        }
        EvidenceAnchorCommitResult committed = CommitEvidenceAnchorMutation(prepared, out _);
        failureCode = committed.Success ? string.Empty : committed.Code.ToString();
        return committed.Success;
    }

    public EvidenceAnchorPrepareResult TryPrepareEvidenceAnchorMutation(
        GameplayOutcomeId outcomeId,
        GameplayEntityId subjectId,
        NarrativeEvidenceReference anchor,
        EvidenceAnchorMutationKind mutation,
        int expectedAnchorRevision,
        out PreparedEvidenceAnchorToken prepared)
    {
        prepared = default;
        lock (gate)
        {
            if (!anchor.IsValid || !subjectId.IsValid
                || mutation != EvidenceAnchorMutationKind.Add
                    && mutation != EvidenceAnchorMutationKind.Remove)
                return new EvidenceAnchorPrepareResult(EvidenceAnchorPrepareCode.Invalid, "anchor-mutation-invalid");
            if (!TryGetMutablePublishedPage(outcomeId, out GameplayOutcomeValuePage page))
                return new EvidenceAnchorPrepareResult(EvidenceAnchorPrepareCode.Invalid, "outcome-not-active");
            if (page.AnchorReservationActive)
                return new EvidenceAnchorPrepareResult(EvidenceAnchorPrepareCode.ReservationBusy, "anchor-reservation-active");
            if (page.AnchorRevision != expectedAnchorRevision)
                return new EvidenceAnchorPrepareResult(EvidenceAnchorPrepareCode.Stale, "anchor-revision-stale");
            if (page.AnchorRevision == int.MaxValue || page.AnchorReservationNonce == long.MaxValue)
                return new EvidenceAnchorPrepareResult(EvidenceAnchorPrepareCode.Invalid, "anchor-revision-exhausted");
            int subjectIndex = FindSubjectIndex(page, subjectId);
            if (subjectIndex < 0 || !GameplayOutcomeVisibilityRules.IsExactSubjectTierVisible(page.Subjects[subjectIndex].Tier))
                return new EvidenceAnchorPrepareResult(EvidenceAnchorPrepareCode.Invalid, "subject-link-not-visible");
            int anchorIndex = FindAnchorIndex(page, subjectId, anchor);
            if (mutation == EvidenceAnchorMutationKind.Add && anchorIndex >= 0
                || mutation == EvidenceAnchorMutationKind.Remove && anchorIndex < 0)
                return new EvidenceAnchorPrepareResult(EvidenceAnchorPrepareCode.AlreadyApplied, string.Empty);
            if (mutation == EvidenceAnchorMutationKind.Add && page.AnchorCount >= page.Anchors.Length)
            {
                int pageIndex = state.ActiveByOutcomeId[outcomeId];
                if (!page.IsDetached
                    || !state.Pool.TryGrowDetachedAnchorCapacity(
                        pageIndex,
                        page.Generation,
                        out GameplayOutcomeValuePage grown))
                {
                    return new EvidenceAnchorPrepareResult(
                        EvidenceAnchorPrepareCode.CapacityDeferred,
                        "anchor-capacity-deferred");
                }
                page = grown;
            }

            page.AnchorReservationNonce++;
            if (page.AnchorReservationNonce <= 0L)
                page.AnchorReservationNonce = 1L;
            page.AnchorReservationActive = true;
            state.ActiveAnchorReservationCount++;
            prepared = new PreparedEvidenceAnchorToken(
                state.ActiveByOutcomeId[outcomeId], page.Generation, state.WorldEpoch,
                page.AnchorReservationNonce, expectedAnchorRevision, subjectIndex, anchorIndex,
                mutation, subjectId, anchor);
            return new EvidenceAnchorPrepareResult(EvidenceAnchorPrepareCode.Prepared, string.Empty);
        }
    }

    public EvidenceAnchorCommitResult CommitEvidenceAnchorMutation(
        in PreparedEvidenceAnchorToken prepared,
        out EvidenceAnchorRollbackToken rollback)
    {
        rollback = default;
        lock (gate)
        {
            GameplayOutcomeValuePage page = state.Pool.Get(prepared.PageIndex);
            if (prepared.WorldEpoch != state.WorldEpoch)
                return new EvidenceAnchorCommitResult(EvidenceAnchorCommitCode.WorldMismatch);
            if (!MatchesAnchorReservation(page, prepared))
                return new EvidenceAnchorCommitResult(EvidenceAnchorCommitCode.InvalidToken);
            if (page.AnchorRevision != prepared.ExpectedRevision)
            {
                ReleaseAnchorReservation(page);
                return new EvidenceAnchorCommitResult(EvidenceAnchorCommitCode.Stale);
            }
            GameplayOutcomeSubjectLink previous = page.Subjects[prepared.SubjectIndex];
            int anchorIndex = prepared.AnchorIndex;
            if (prepared.Mutation == EvidenceAnchorMutationKind.Add)
            {
                anchorIndex = page.AnchorCount;
                page.Anchors[page.AnchorCount++] = new GameplayOutcomeAnchorRow(prepared.SubjectId, prepared.Anchor);
            }
            else
            {
                for (int index = anchorIndex + 1; index < page.AnchorCount; index++)
                    page.Anchors[index - 1] = page.Anchors[index];
                page.Anchors[--page.AnchorCount] = default;
            }
            int revision = page.AnchorRevision + 1;
            page.AnchorRevision = revision;
            page.Subjects[prepared.SubjectIndex] = new GameplayOutcomeSubjectLink(
                previous.SubjectId, previous.Salience,
                prepared.Mutation == EvidenceAnchorMutationKind.Add ? NarrativeMemoryTier.Core : previous.Tier,
                previous.IsPinned, previous.IsOptionalWitness, revision,
                prepared.Mutation == EvidenceAnchorMutationKind.Add
                    || previous.IsPinned
                    || HasAnchorFor(page, previous.SubjectId)
                    ? int.MaxValue
                    : 0,
                previous.InfluenceUseCount,
                previous.InfluenceRevision);
            ReleaseAnchorReservation(page);
            MarkDuePageDirty(prepared.PageIndex, page);
            state.Revision++;
            rollback = new EvidenceAnchorRollbackToken(
                prepared.PageIndex, prepared.PageGeneration, prepared.WorldEpoch,
                prepared.ExpectedRevision, revision,
                anchorIndex, prepared.Mutation, prepared.SubjectId, prepared.Anchor, previous);
            return new EvidenceAnchorCommitResult(EvidenceAnchorCommitCode.Committed);
        }
    }

    public void CancelEvidenceAnchorMutation(in PreparedEvidenceAnchorToken prepared)
    {
        lock (gate)
        {
            GameplayOutcomeValuePage page = state.Pool.Get(prepared.PageIndex);
            if (MatchesAnchorReservation(page, prepared))
                ReleaseAnchorReservation(page);
        }
    }

    public EvidenceAnchorRollbackResult RollbackEvidenceAnchorMutation(
        in EvidenceAnchorRollbackToken rollback)
    {
        lock (gate)
        {
            if (rollback.WorldEpoch != state.WorldEpoch)
                return new EvidenceAnchorRollbackResult(EvidenceAnchorRollbackCode.WorldMismatch);
            GameplayOutcomeValuePage page = state.Pool.Get(rollback.PageIndex);
            if (page == null || page.Generation != rollback.PageGeneration || !IsPublished(page))
                return new EvidenceAnchorRollbackResult(EvidenceAnchorRollbackCode.InvalidToken);
            if (page.AnchorReservationActive || page.AnchorRevision != rollback.CommittedRevision)
                return new EvidenceAnchorRollbackResult(EvidenceAnchorRollbackCode.Stale);
            int subjectIndex = FindSubjectIndex(page, rollback.SubjectId);
            if (subjectIndex < 0)
                return new EvidenceAnchorRollbackResult(EvidenceAnchorRollbackCode.InvalidToken);
            if (rollback.Mutation == EvidenceAnchorMutationKind.Add)
            {
                int remove = FindAnchorIndex(page, rollback.SubjectId, rollback.Anchor);
                if (remove < 0) return new EvidenceAnchorRollbackResult(EvidenceAnchorRollbackCode.Stale);
                for (int index = remove + 1; index < page.AnchorCount; index++)
                    page.Anchors[index - 1] = page.Anchors[index];
                page.Anchors[--page.AnchorCount] = default;
            }
            else
            {
                if (page.AnchorCount >= page.Anchors.Length || rollback.AnchorIndex > page.AnchorCount)
                    return new EvidenceAnchorRollbackResult(EvidenceAnchorRollbackCode.Stale);
                for (int index = page.AnchorCount; index > rollback.AnchorIndex; index--)
                    page.Anchors[index] = page.Anchors[index - 1];
                page.Anchors[rollback.AnchorIndex] = new GameplayOutcomeAnchorRow(rollback.SubjectId, rollback.Anchor);
                page.AnchorCount++;
            }
            page.AnchorRevision = rollback.PriorRevision;
            page.Subjects[subjectIndex] = rollback.PreviousSubject;
            MarkDuePageDirty(rollback.PageIndex, page);
            state.Revision++;
            return new EvidenceAnchorRollbackResult(EvidenceAnchorRollbackCode.RolledBack);
        }
    }

    private bool MatchesAnchorReservation(
        GameplayOutcomeValuePage page,
        in PreparedEvidenceAnchorToken prepared) =>
        page != null && page.Generation == prepared.PageGeneration && IsPublished(page)
        && page.AnchorReservationActive && page.AnchorReservationNonce == prepared.Nonce
        && prepared.SubjectIndex >= 0 && prepared.SubjectIndex < page.SubjectCount
        && page.Subjects[prepared.SubjectIndex].SubjectId == prepared.SubjectId;

    private void ReleaseAnchorReservation(GameplayOutcomeValuePage page)
    {
        page.AnchorReservationActive = false;
        state.ActiveAnchorReservationCount--;
    }

    private static int FindAnchorIndex(
        GameplayOutcomeValuePage page,
        GameplayEntityId subjectId,
        NarrativeEvidenceReference anchor)
    {
        for (int index = 0; index < page.AnchorCount; index++)
        {
            GameplayOutcomeAnchorRow row = page.Anchors[index];
            if (row.SubjectId == subjectId && row.Reference.Equals(anchor))
                return index;
        }
        return -1;
    }

    public InfluenceUsePrepareResult TryPrepareInfluenceUse(
        GameplayOutcomeId outcomeId,
        GameplayEntityId subjectId,
        int expectedRevision,
        int expectedUseCount,
        out PreparedInfluenceUseToken prepared)
    {
        prepared = default;
        lock (gate)
        {
            if (!subjectId.IsValid || expectedRevision < 0
                || expectedUseCount < 0)
            {
                return new InfluenceUsePrepareResult(
                    InfluenceUsePrepareCode.Invalid,
                    "influence-use-request-invalid");
            }
            if (!TryGetMutablePublishedPage(
                    outcomeId,
                    out GameplayOutcomeValuePage page))
            {
                return new InfluenceUsePrepareResult(
                    InfluenceUsePrepareCode.Invalid,
                    "outcome-not-active");
            }
            if (page.InfluenceReservationActive)
            {
                return new InfluenceUsePrepareResult(
                    InfluenceUsePrepareCode.ReservationBusy,
                    "influence-use-reservation-active");
            }
            int subjectIndex = FindSubjectIndex(page, subjectId);
            if (subjectIndex < 0
                || !GameplayOutcomeVisibilityRules.IsExactSubjectTierVisible(
                    page.Subjects[subjectIndex].Tier))
            {
                return new InfluenceUsePrepareResult(
                    InfluenceUsePrepareCode.Invalid,
                    "subject-link-not-visible");
            }
            GameplayOutcomeSubjectLink subject = page.Subjects[subjectIndex];
            if (subject.InfluenceRevision != expectedRevision
                || subject.InfluenceUseCount != expectedUseCount)
            {
                return new InfluenceUsePrepareResult(
                    InfluenceUsePrepareCode.Stale,
                    "influence-use-revision-stale");
            }
            if (subject.InfluenceRevision == int.MaxValue
                || subject.InfluenceUseCount == int.MaxValue
                || page.InfluenceReservationNonce == long.MaxValue)
            {
                return new InfluenceUsePrepareResult(
                    InfluenceUsePrepareCode.Exhausted,
                    "influence-use-counter-exhausted");
            }
            page.InfluenceReservationNonce++;
            if (page.InfluenceReservationNonce <= 0L)
                page.InfluenceReservationNonce = 1L;
            page.InfluenceReservationActive = true;
            state.ActiveInfluenceReservationCount++;
            prepared = new PreparedInfluenceUseToken(
                state.ActiveByOutcomeId[outcomeId],
                page.Generation,
                state.WorldEpoch,
                page.InfluenceReservationNonce,
                subjectIndex,
                subjectId,
                expectedRevision,
                expectedUseCount);
            return new InfluenceUsePrepareResult(
                InfluenceUsePrepareCode.Prepared,
                string.Empty);
        }
    }

    public InfluenceUseCommitResult CommitInfluenceUse(
        in PreparedInfluenceUseToken prepared,
        out InfluenceUseRollbackToken rollback)
    {
        rollback = default;
        lock (gate)
        {
            if (prepared.WorldEpoch != state.WorldEpoch)
                return new InfluenceUseCommitResult(
                    InfluenceUseCommitCode.WorldMismatch);
            GameplayOutcomeValuePage page = state.Pool.Get(prepared.PageIndex);
            if (!MatchesInfluenceReservation(page, prepared))
                return new InfluenceUseCommitResult(
                    InfluenceUseCommitCode.InvalidToken);
            GameplayOutcomeSubjectLink previous =
                page.Subjects[prepared.SubjectIndex];
            if (previous.InfluenceRevision != prepared.ExpectedRevision
                || previous.InfluenceUseCount != prepared.ExpectedUseCount)
            {
                // The owner transaction must explicitly cancel or reconcile
                // this guard. Releasing it on a stale commit would permit a
                // save between a provisional anchor and its rollback.
                return new InfluenceUseCommitResult(InfluenceUseCommitCode.Stale);
            }
            int nextRevision = checked(previous.InfluenceRevision + 1);
            int nextUseCount = checked(previous.InfluenceUseCount + 1);
            page.Subjects[prepared.SubjectIndex] = new GameplayOutcomeSubjectLink(
                previous.SubjectId,
                previous.Salience,
                previous.Tier,
                previous.IsPinned,
                previous.IsOptionalWitness,
                previous.AnchorRevision,
                previous.NextEvaluationDay,
                nextUseCount,
                nextRevision);
            state.Revision++;
            rollback = new InfluenceUseRollbackToken(
                prepared.PageIndex,
                prepared.PageGeneration,
                prepared.WorldEpoch,
                prepared.SubjectIndex,
                prepared.SubjectId,
                prepared.Nonce,
                nextRevision,
                nextUseCount,
                previous);
            return new InfluenceUseCommitResult(InfluenceUseCommitCode.Committed);
        }
    }

    public void CancelInfluenceUse(in PreparedInfluenceUseToken prepared)
    {
        lock (gate)
        {
            GameplayOutcomeValuePage page = state.Pool.Get(prepared.PageIndex);
            if (MatchesInfluenceReservation(page, prepared))
                ReleaseInfluenceReservation(page);
        }
    }

    public InfluenceUseCommitResult CompleteInfluenceUses(
        IReadOnlyList<InfluenceUseRollbackToken> committed)
    {
        lock (gate)
        {
            int count = committed?.Count ?? 0;
            int validCount = 0;
            if (count == 0
                || count > GameplayOutcomeBufferLimits.AbsoluteMaximumAnchorsPerOutcome)
            {
                return new InfluenceUseCommitResult(
                    InfluenceUseCommitCode.InvalidToken);
            }
            for (int index = 0; index < count; index++)
            {
                InfluenceUseRollbackToken token = committed[index];
                if (!token.IsValid)
                    continue;
                validCount++;
                // A repeated token would otherwise pass both validation
                // phases and attempt to terminally release the same guard
                // twice. Reject the whole bounded batch before any release.
                for (int priorIndex = 0; priorIndex < index; priorIndex++)
                {
                    InfluenceUseRollbackToken prior = committed[priorIndex];
                    if (prior.IsValid
                        && prior.PageIndex == token.PageIndex
                        && prior.PageGeneration == token.PageGeneration
                        && prior.ReservationNonce == token.ReservationNonce)
                    {
                        return new InfluenceUseCommitResult(
                            InfluenceUseCommitCode.InvalidToken);
                    }
                }
                if (token.WorldEpoch != state.WorldEpoch)
                    return new InfluenceUseCommitResult(
                        InfluenceUseCommitCode.WorldMismatch);
                GameplayOutcomeValuePage page = state.Pool.Get(token.PageIndex);
                if (page == null || page.Generation != token.PageGeneration
                    || !IsPublished(page) || !page.InfluenceReservationActive
                    || page.InfluenceReservationNonce != token.ReservationNonce
                    || token.SubjectIndex < 0
                    || token.SubjectIndex >= page.SubjectCount)
                {
                    return new InfluenceUseCommitResult(
                        InfluenceUseCommitCode.InvalidToken);
                }
                GameplayOutcomeSubjectLink current =
                    page.Subjects[token.SubjectIndex];
                if (current.SubjectId != token.SubjectId
                    || current.InfluenceRevision != token.CommittedRevision
                    || current.InfluenceUseCount != token.CommittedUseCount)
                {
                    return new InfluenceUseCommitResult(
                        InfluenceUseCommitCode.Stale);
                }
            }
            if (validCount == 0)
                return new InfluenceUseCommitResult(
                    InfluenceUseCommitCode.InvalidToken);
            for (int index = 0; index < count; index++)
            {
                if (committed[index].IsValid)
                    ReleaseInfluenceReservation(
                        state.Pool.Get(committed[index].PageIndex));
            }
            return new InfluenceUseCommitResult(InfluenceUseCommitCode.Committed);
        }
    }

    public InfluenceUseRollbackResult RollbackInfluenceUse(
        in InfluenceUseRollbackToken rollback)
    {
        lock (gate)
        {
            if (rollback.WorldEpoch != state.WorldEpoch)
                return new InfluenceUseRollbackResult(
                    InfluenceUseRollbackCode.WorldMismatch);
            GameplayOutcomeValuePage page = state.Pool.Get(rollback.PageIndex);
            if (page == null || page.Generation != rollback.PageGeneration
                || !IsPublished(page) || !page.InfluenceReservationActive
                || page.InfluenceReservationNonce != rollback.ReservationNonce
                || rollback.SubjectIndex < 0
                || rollback.SubjectIndex >= page.SubjectCount)
            {
                return new InfluenceUseRollbackResult(
                    InfluenceUseRollbackCode.InvalidToken);
            }
            GameplayOutcomeSubjectLink current =
                page.Subjects[rollback.SubjectIndex];
            if (current.SubjectId != rollback.SubjectId
                || current.InfluenceRevision != rollback.CommittedRevision
                || current.InfluenceUseCount != rollback.CommittedUseCount)
            {
                return new InfluenceUseRollbackResult(
                    InfluenceUseRollbackCode.Stale);
            }
            page.Subjects[rollback.SubjectIndex] = rollback.PreviousSubject;
            ReleaseInfluenceReservation(page);
            state.Revision++;
            return new InfluenceUseRollbackResult(
                InfluenceUseRollbackCode.RolledBack);
        }
    }

    private bool MatchesInfluenceReservation(
        GameplayOutcomeValuePage page,
        in PreparedInfluenceUseToken prepared) => page != null
        && page.Generation == prepared.PageGeneration
        && IsPublished(page)
        && page.InfluenceReservationActive
        && page.InfluenceReservationNonce == prepared.Nonce
        && prepared.SubjectIndex >= 0
        && prepared.SubjectIndex < page.SubjectCount
        && page.Subjects[prepared.SubjectIndex].SubjectId == prepared.SubjectId;

    private void ReleaseInfluenceReservation(GameplayOutcomeValuePage page)
    {
        if (page == null || !page.InfluenceReservationActive)
            throw new InvalidOperationException(
                "Gameplay outcome influence reservation was already released.");
        if (state.ActiveInfluenceReservationCount <= 0)
            throw new InvalidOperationException(
                "Gameplay outcome influence reservation count underflowed.");
        page.InfluenceReservationActive = false;
        state.ActiveInfluenceReservationCount--;
    }

    public int PendingJobCount
    {
        get
        {
            lock (gate)
                return state.ConsolidationJobs.Count;
        }
    }

    [GameplayInternalOnly(
        "Schedules a fixed-cutoff deterministic memory evaluation job.",
        "Gameplay outcome day-boundary scheduler")]
    public bool TrySchedule(
        in GameplayOutcomeConsolidationRequest request,
        out string failureCode)
    {
        lock (gate)
        {
            if (request.WorldEpoch != state.WorldEpoch)
            {
                failureCode = "world-epoch-mismatch";
                return false;
            }
            if (request.EvaluationDay < 0
                || request.CutoffSequence < 0L
                || request.CutoffSequence >= state.NextSequence
                || request.PolicyVersion <= 0
                || request.PolicyVersion != registry.ConsolidationPolicyVersion)
            {
                failureCode = "consolidation-request-invalid";
                return false;
            }
            if (request.EvaluationDay <= state.LastScheduledEvaluationDay)
            {
                failureCode = string.Empty;
                return true;
            }
            state.LastScheduledEvaluationDay = request.EvaluationDay;
            state.LastScheduledCutoffSequence = request.CutoffSequence;
            if (request.CutoffSequence == 0L)
            {
                state.Revision++;
                failureCode = string.Empty;
                return true;
            }
            for (int index = 0; index < state.ConsolidationJobs.Count; index++)
            {
                GameplayOutcomeConsolidationJob existing = state.ConsolidationJobs[index];
                if (existing.WorldEpoch == request.WorldEpoch
                    && existing.EvaluationDay == request.EvaluationDay
                    && existing.CutoffSequence == request.CutoffSequence
                    && existing.PolicyVersion == request.PolicyVersion)
                {
                    failureCode = string.Empty;
                    return true;
                }
            }
            GameplayOutcomeConsolidationJob job = new GameplayOutcomeConsolidationJob
            {
                EvaluationDay = request.EvaluationDay,
                CutoffSequence = request.CutoffSequence,
                PolicyVersion = request.PolicyVersion,
                WorldEpoch = request.WorldEpoch,
                NextDueIndex = 0,
                NextSequence = 1L,
                // A queued job snapshots the due index only when it becomes
                // the queue head. Earlier jobs can move an entry's next-due
                // day; capturing here would make backlog depth alter results.
                DuePageIndices = null
            };
            int insert = state.ConsolidationJobs.Count;
            for (int index = 0; index < state.ConsolidationJobs.Count; index++)
            {
                GameplayOutcomeConsolidationJob candidate = state.ConsolidationJobs[index];
                if (request.CutoffSequence < candidate.CutoffSequence
                    || request.CutoffSequence == candidate.CutoffSequence
                        && (request.EvaluationDay < candidate.EvaluationDay
                            || request.EvaluationDay == candidate.EvaluationDay
                                && request.PolicyVersion < candidate.PolicyVersion))
                {
                    insert = index;
                    break;
                }
            }
            state.ConsolidationJobs.Insert(insert, job);
            state.Revision++;
            failureCode = string.Empty;
            return true;
        }
    }

    [GameplayInternalOnly(
        "Advances bounded narrative-memory consolidation on the single writer.",
        "Gameplay outcome PlayerLoop scheduler")]
    public GameplayOutcomeConsolidationSliceResult ProcessSlice(
        int maximumRecords,
        long maximumElapsedTicks)
    {
        // maximumElapsedTicks is expressed in Stopwatch ticks. Scratch is
        // detached and never published until the complete record validates.
        if (maximumRecords <= 0)
            return new GameplayOutcomeConsolidationSliceResult(0, 0, PendingJobCount > 0, "record-budget-empty");
        long started = Stopwatch.GetTimestamp();
        int examined = 0;
        int published = 0;
        lock (gate)
        {
            while (state.ConsolidationJobs.Count > 0 && examined < maximumRecords)
            {
                GameplayOutcomeConsolidationJob job = state.ConsolidationJobs[0];
                if (job.WorldEpoch != state.WorldEpoch)
                {
                    state.ConsolidationJobs.RemoveAt(0);
                    continue;
                }
                if (job.DuePageIndices == null)
                {
                    FlushDirtyDuePages();
                    job.DuePageIndices = BuildDuePageList(
                        job.EvaluationDay,
                        job.CutoffSequence,
                        job.NextSequence);
                    job.NextDueIndex = 0;
                }
                if (state.ConsolidationScratch == null)
                {
                    while (job.NextDueIndex < job.DuePageIndices.Count)
                    {
                        int candidateIndex = job.DuePageIndices[job.NextDueIndex];
                        if (candidateIndex < 0)
                        {
                            job.NextDueIndex++;
                            continue;
                        }
                        GameplayOutcomeValuePage candidate = state.Pool.Get(candidateIndex);
                        if (!IsPublished(candidate)
                            || candidate.Sequence < job.NextSequence
                            || !IsDueForJob(candidateIndex, candidate, job))
                        {
                            job.NextDueIndex++;
                            continue;
                        }
                        if (candidate.Sequence > job.CutoffSequence)
                        {
                            job.NextDueIndex = job.DuePageIndices.Count;
                            break;
                        }
                        if (candidate.AnchorReservationActive
                            || candidate.InfluenceReservationActive)
                            return new GameplayOutcomeConsolidationSliceResult(
                                examined, published, true, "anchor-reservation-active");
                        state.ConsolidationScratch = new GameplayOutcomeConsolidationScratch
                        {
                            PageIndex = candidateIndex,
                            PageGeneration = candidate.Generation,
                            DueSlot = job.NextDueIndex,
                            WorldEpoch = state.WorldEpoch,
                            PublishedRevision = candidate.PublishedRevision,
                            CompactedRevision = state.CompactedRevision,
                            AnchorRevision = candidate.AnchorRevision,
                            PolicyVersion = job.PolicyVersion,
                            EvaluationDay = job.EvaluationDay,
                            CutoffSequence = job.CutoffSequence,
                            Decisions = new SubjectConsolidationDecision[candidate.SubjectCount],
                            SelectedMetrics = new List<GameplayOutcomeMetric>(candidate.MetricCount)
                        };
                        break;
                    }
                }
                if (state.ConsolidationScratch == null)
                {
                    state.ConsolidationJobs.RemoveAt(0);
                    state.Revision++;
                    continue;
                }
                GameplayOutcomeConsolidationScratch scratch = state.ConsolidationScratch;
                GameplayOutcomeValuePage page = state.Pool.Get(scratch.PageIndex);
                try
                {
                    if (!ValidateScratch(page, job, scratch))
                    {
                        ClearConsolidationScratch();
                        return new GameplayOutcomeConsolidationSliceResult(
                            examined, published, true, "consolidation-stale-restart");
                    }
                    bool ready = AdvanceConsolidationScratch(page, job, scratch);
                    if (ready)
                    {
                        long completedSequence = page.Sequence;
                        if (PublishConsolidationScratch(scratch.PageIndex, page, job, scratch))
                            published++;
                        job.NextDueIndex = scratch.DueSlot + 1;
                        job.NextSequence = completedSequence + 1L;
                        ClearConsolidationScratch();
                        examined++;
                    }
                }
                catch (Exception exception) when (
                    exception is not OutOfMemoryException
                    && exception is not StackOverflowException
                    && exception is not AccessViolationException)
                {
                    ClearConsolidationScratch();
                    return new GameplayOutcomeConsolidationSliceResult(
                        examined,
                        published,
                        true,
                        "consolidation-policy-fault");
                }

                if (maximumElapsedTicks > 0L
                    && Stopwatch.GetTimestamp() - started >= maximumElapsedTicks)
                    break;
            }
            return new GameplayOutcomeConsolidationSliceResult(
                examined,
                published,
                state.ConsolidationJobs.Count > 0,
                string.Empty);
        }
    }

    private bool ValidateScratch(
        GameplayOutcomeValuePage page,
        GameplayOutcomeConsolidationJob job,
        GameplayOutcomeConsolidationScratch scratch)
    {
        if (page == null || page.Generation != scratch.PageGeneration || !IsPublished(page)
            || page.PublishedRevision != scratch.PublishedRevision
            || state.CompactedRevision != scratch.CompactedRevision
            || page.AnchorRevision != scratch.AnchorRevision
            || page.AnchorReservationActive
            || page.InfluenceReservationActive
            || state.WorldEpoch != scratch.WorldEpoch
            || job.WorldEpoch != scratch.WorldEpoch
            || job.PolicyVersion != scratch.PolicyVersion
            || job.EvaluationDay != scratch.EvaluationDay
            || job.CutoffSequence != scratch.CutoffSequence)
            return false;
        return registry.TryGetDescriptor(page.OutcomeTypeId, out IGameplayOutcomeDescriptor descriptor)
            && descriptor.MemoryPolicy.PolicyVersion == scratch.PolicyVersion;
    }

    private bool IsDueForJob(
        int pageIndex,
        GameplayOutcomeValuePage page,
        GameplayOutcomeConsolidationJob job)
    {
        return page != null
            && !page.DueIndexDirty
            && state.DueByPageIndex.TryGetValue(pageIndex, out GameplayOutcomeDueEntry due)
            && due.Generation == page.Generation
            && due.Sequence == page.Sequence
            && due.DueDay <= job.EvaluationDay
            && due.Sequence <= job.CutoffSequence;
    }

    private bool AdvanceConsolidationScratch(
        GameplayOutcomeValuePage page,
        GameplayOutcomeConsolidationJob job,
        GameplayOutcomeConsolidationScratch scratch)
    {
        registry.TryGetDescriptor(page.OutcomeTypeId, out IGameplayOutcomeDescriptor descriptor);
        GameplayOutcomeReadView view = new GameplayOutcomeReadView(page);
        if (scratch.SelectingMetrics)
        {
            if (scratch.MetricIndex < page.MetricCount)
            {
                GameplayOutcomeMetric metric = page.Metrics[scratch.MetricIndex++];
                if (descriptor.MemoryConsolidator.IsAdditiveMetric(metric.MetricId))
                    scratch.SelectedMetrics.Add(metric);
                return false;
            }
            SubjectConsolidationDecision pending = scratch.PendingDecision;
            scratch.Decisions[scratch.SubjectIndex++] = new SubjectConsolidationDecision(
                pending.SubjectId, pending.Salience, pending.Tier, pending.Signature,
                pending.Compact, pending.NextEvaluationDay, scratch.SelectedMetrics.ToArray());
            scratch.SelectedMetrics.Clear();
            scratch.MetricIndex = 0;
            scratch.SelectingMetrics = false;
            return false;
        }
        if (scratch.SubjectIndex < page.SubjectCount)
        {
            GameplayOutcomeSubjectLink subject = page.Subjects[scratch.SubjectIndex];
            if (subject.Tier == NarrativeMemoryTier.Compacted
                || subject.Tier == NarrativeMemoryTier.Forgotten)
            {
                scratch.Decisions[scratch.SubjectIndex++] = new SubjectConsolidationDecision(
                    subject.SubjectId, subject.Salience, subject.Tier, default, false,
                    int.MaxValue, Array.Empty<GameplayOutcomeMetric>());
                return false;
            }
            bool anchored = subject.IsPinned || HasAnchorFor(page, subject.SubjectId);
            GameplayMemorySignature signature = descriptor.MemoryPolicy.GetSignature(view, subject.SubjectId);
            OutcomeMemoryEvaluation evaluation = anchored
                ? new OutcomeMemoryEvaluation(Math.Max(subject.Salience, 1f), NarrativeMemoryTier.Core, int.MaxValue)
                : descriptor.MemoryPolicy.Evaluate(
                    view, subject.SubjectId,
                    TryGetCompactedOccurrenceCount(subject.SubjectId, signature),
                    job.EvaluationDay);
            NarrativeMemoryTier tier = evaluation.TargetTier;
            if (evaluation.NextEvaluationDay <= job.EvaluationDay)
                throw new InvalidOperationException("Outcome memory policy returned a non-future evaluation day.");
            if ((int)tier < (int)NarrativeMemoryTier.Recent
                || (int)tier > (int)NarrativeMemoryTier.Forgotten)
                throw new InvalidOperationException("Outcome memory policy returned an invalid tier.");
            bool compact = tier == NarrativeMemoryTier.Compacted
                && descriptor.MemoryConsolidator is IOutcomeCompactionContract contract
                && contract.SupportsCompaction
                && descriptor.MemoryConsolidator.CanCompact(view, subject.SubjectId);
            if (tier == NarrativeMemoryTier.Compacted && !compact)
                tier = NarrativeMemoryTier.Episodic;
            if (compact)
            {
                if (!signature.IsValid)
                    throw new InvalidOperationException("Outcome memory policy returned an invalid compacted signature.");
                if (!CanMergeCompactedTarget(
                    page, subject.SubjectId, signature, descriptor.MemoryConsolidator))
                {
                    // Incompatible participant/display, provenance, location,
                    // type, or status sets remain exact instead of blocking
                    // the deterministic consolidation cursor forever.
                    compact = false;
                    tier = NarrativeMemoryTier.Episodic;
                }
            }
            if (compact)
            {
                scratch.PendingDecision = new SubjectConsolidationDecision(
                    subject.SubjectId, evaluation.Salience, tier, signature, true,
                    evaluation.NextEvaluationDay, Array.Empty<GameplayOutcomeMetric>());
                scratch.SelectingMetrics = true;
                return false;
            }
            scratch.Decisions[scratch.SubjectIndex++] = new SubjectConsolidationDecision(
                subject.SubjectId, evaluation.Salience, tier, signature, false,
                evaluation.NextEvaluationDay, Array.Empty<GameplayOutcomeMetric>());
            return false;
        }
        if (scratch.ParticipantValidationIndex < page.ParticipantCount)
        {
            if (!IsValidDisplayNameSnapshot(
                page.Participants[scratch.ParticipantValidationIndex++].DisplayName))
                throw new InvalidOperationException("Historical participant display metadata became invalid.");
            return false;
        }
        if (scratch.ProvenanceValidationIndex < page.ProvenanceCount)
        {
            if (!page.Provenance[scratch.ProvenanceValidationIndex++].IsValid)
                throw new InvalidOperationException("Historical receipt provenance became invalid.");
            return false;
        }
        if (scratch.PreparedDeltas == null)
        {
            scratch.PreparedDeltas = new List<GameplayOutcomeCompactedDelta>(scratch.Decisions.Length);
            int potentialNew = 0;
            for (int index = 0; index < scratch.Decisions.Length; index++)
                if (scratch.Decisions[index].Compact) potentialNew++;
            int requiredCapacity = checked(state.CompactedMemories.Count + potentialNew);
            if (state.CompactedMemories.Capacity < requiredCapacity)
                state.CompactedMemories.Capacity = requiredCapacity;
            state.CompactedByKey.EnsureCapacity(requiredCapacity);
            return false;
        }
        if (scratch.CompactionDecisionIndex < scratch.Decisions.Length)
        {
            if (scratch.CurrentMerge != null)
            {
                if (AdvanceCompactedMerge(page, scratch.CurrentMerge))
                {
                    scratch.CurrentMerge = null;
                    scratch.CompactionDecisionIndex++;
                }
                return false;
            }
            SubjectConsolidationDecision decision =
                scratch.Decisions[scratch.CompactionDecisionIndex];
            if (!decision.Compact)
            {
                scratch.CompactionDecisionIndex++;
                return false;
            }
            scratch.CurrentMerge = CreateCompactedMergeScratch(
                page, decision, page.MaterializeImmutablePayloadHash(),
                scratch.PreparedDeltas);
            return false;
        }
        if (scratch.ModifiedHashIndex < scratch.PreparedDeltas.Count)
        {
            CompactedNarrativeMemorySnapshot memory =
                scratch.PreparedDeltas[scratch.ModifiedHashIndex].Memory;
            if (scratch.CurrentHash == null)
                scratch.CurrentHash = new GameplayOutcomeCanonicalHash.CompactedHashAccumulator(memory);
            if (!scratch.CurrentHash.Advance())
                return false;
            memory.compactedPayloadHash = scratch.CurrentHash.Result;
            scratch.CurrentHash.Dispose();
            scratch.CurrentHash = null;
            scratch.ModifiedHashIndex++;
            return false;
        }
        return true;
    }

    private bool PublishConsolidationScratch(
        int pageIndex,
        GameplayOutcomeValuePage page,
        GameplayOutcomeConsolidationJob job,
        GameplayOutcomeConsolidationScratch scratch)
    {
        SubjectConsolidationDecision[] decisions = scratch.Decisions;
        // Zero-subject receipts are rejected at preparation and restore. An
        // exact page remains only while at least one subject is still visible.
        bool exactRequired = false;
        bool hasCompacted = false;
        NarrativeMemoryTier retainedTier = NarrativeMemoryTier.Recent;
        for (int index = 0; index < decisions.Length; index++)
        {
            NarrativeMemoryTier tier = decisions[index].Tier;
            if (tier == NarrativeMemoryTier.Core
                || tier == NarrativeMemoryTier.Episodic
                || tier == NarrativeMemoryTier.Recent)
            {
                exactRequired = true;
                retainedTier = HigherRetention(retainedTier, tier);
            }
            hasCompacted |= decisions[index].Compact || tier == NarrativeMemoryTier.Compacted;
        }

        string exactHash = page.MaterializeImmutablePayloadHash();
        for (int index = 0; index < scratch.PreparedDeltas.Count; index++)
        {
            GameplayOutcomeCompactedDelta delta = scratch.PreparedDeltas[index];
            if (delta.ExistingIndex >= 0)
            {
                state.CompactedMemories[delta.ExistingIndex] = delta.Memory;
            }
            else
            {
                int newIndex = state.CompactedMemories.Count;
                state.CompactedMemories.Add(delta.Memory);
                state.CompactedByKey.Add(delta.Key, newIndex);
            }
        }
        if (scratch.PreparedDeltas.Count > 0)
            state.CompactedRevision++;
        for (int index = 0; index < decisions.Length; index++)
        {
            SubjectConsolidationDecision decision = decisions[index];
            GameplayOutcomeSubjectLink current = page.Subjects[index];
            page.Subjects[index] = new GameplayOutcomeSubjectLink(
                current.SubjectId,
                decision.Salience,
                decision.Tier,
                current.IsPinned,
                current.IsOptionalWitness,
                current.AnchorRevision,
                decision.NextEvaluationDay,
                current.InfluenceUseCount,
                current.InfluenceRevision);
        }

        if (exactRequired)
        {
            page.StorageTier = retainedTier;
            MarkDuePageDirty(pageIndex, page);
            state.Revision++;
            return true;
        }

        NarrativeMemoryTier terminalTier = hasCompacted
            ? NarrativeMemoryTier.Compacted
            : NarrativeMemoryTier.Forgotten;
        GameplayOutcomeTombstoneSnapshot tombstone = new GameplayOutcomeTombstoneSnapshot
        {
            runId = page.OutcomeId.RunId.Value,
            sequence = page.OutcomeId.Sequence,
            producerId = page.ResultKey.ProducerId,
            operationId = page.OperationId.Value,
            commitRevision = page.ResultKey.CommitRevision,
            localResultIndex = page.ResultKey.LocalResultIndex,
            ownerRevision = page.OwnerRevision,
            terminalTier = terminalTier,
            canonicalHash = exactHash,
            influenceUseCount = SumInfluenceUseCount(page),
            influenceRevision = SumInfluenceRevision(page)
        };
        int tombstoneIndex = state.Tombstones.Count;
        state.Tombstones.Add(tombstone);
        state.TombstoneByResultKey.Add(page.ResultKey, tombstoneIndex);
        state.ActiveByResultKey.Remove(page.ResultKey);
        state.ActiveByOutcomeId.Remove(page.OutcomeId);
        RemoveDuePage(pageIndex);
        RemovePublishedPage(pageIndex, page);
        if (terminalTier == NarrativeMemoryTier.Forgotten)
            state.ForgottenCount++;
        state.PublishedCount--;
        state.Revision++;
        state.Pool.Return(pageIndex, page.Generation);
        return true;
    }

    private void RemovePublishedPage(int pageIndex, GameplayOutcomeValuePage page)
    {
        int slot = page.PublishedListSlot;
        int lastSlot = state.PublishedPageIndices.Count - 1;
        if (slot < 0 || slot > lastSlot || state.PublishedPageIndices[slot] != pageIndex)
            throw new InvalidOperationException("Published outcome index is inconsistent.");
        if (slot != lastSlot)
        {
            int movedPageIndex = state.PublishedPageIndices[lastSlot];
            GameplayOutcomeValuePage moved = state.Pool.Get(movedPageIndex);
            if (!IsPublished(moved))
                throw new InvalidOperationException("Published outcome index contains an inactive page.");
            state.PublishedPageIndices[slot] = movedPageIndex;
            moved.PublishedListSlot = slot;
        }
        state.PublishedPageIndices.RemoveAt(lastSlot);
        page.PublishedListSlot = -1;
    }

    private static int SumInfluenceUseCount(GameplayOutcomeValuePage page)
    {
        int value = 0;
        for (int index = 0; index < page.SubjectCount; index++)
            value = checked(value + page.Subjects[index].InfluenceUseCount);
        return value;
    }

    private static int SumInfluenceRevision(GameplayOutcomeValuePage page)
    {
        int value = 0;
        for (int index = 0; index < page.SubjectCount; index++)
            value = checked(value + page.Subjects[index].InfluenceRevision);
        return value;
    }

    private GameplayOutcomeCompactedMergeScratch CreateCompactedMergeScratch(
        GameplayOutcomeValuePage page,
        in SubjectConsolidationDecision decision,
        string exactHash,
        List<GameplayOutcomeCompactedDelta> deltas)
    {
        CompactedMemoryKey key = new CompactedMemoryKey(decision.SubjectId, decision.Signature);
        CompactedNarrativeMemorySnapshot memory;
        CompactedNarrativeMemorySnapshot sourceMemory = null;
        int initialStage = 0;
        int existingIndex = -1;
        if (!state.CompactedByKey.TryGetValue(key, out existingIndex))
        {
            existingIndex = -1;
            memory = new CompactedNarrativeMemorySnapshot
            {
                memoryId = GameplayOutcomeCanonicalHash.CreateMemoryId(state.RunId, decision.SubjectId, decision.Signature),
                sharedAggregateId = string.Empty,
                signature = decision.Signature.Value,
                outcomeTypeId = page.OutcomeTypeId.Value,
                status = page.Status,
                subjectKindId = decision.SubjectId.Kind.Value,
                subjectId = decision.SubjectId.Value,
                locationId = page.Location.LocationId ?? string.Empty,
                roomId = page.Location.RoomId ?? string.Empty,
                locationX = page.Location.X,
                locationY = page.Location.Y,
                firstSequence = page.Sequence,
                lastSequence = page.Sequence,
                firstDay = page.AbsoluteDay,
                lastDay = page.AbsoluteDay,
                occurrenceCount = 0,
                salience = decision.Salience,
                sourceSegmentHash = string.Empty,
                compactedPayloadHash = string.Empty,
                tags = new List<string>(page.TagCount),
                metrics = new List<GameplayOutcomeMetricAggregateSnapshot>(page.MetricCount),
                participants = new List<GameplayOutcomeParticipantSnapshot>(page.ParticipantCount),
                provenance = new List<GameplayOutcomeProvenanceReferenceSnapshot>(page.ProvenanceCount),
                facts = new List<GameplayOutcomeFactSnapshot>(page.FactCount)
            };
        }
        else
        {
            sourceMemory = state.CompactedMemories[existingIndex];
            memory = CloneCompactedHeader(sourceMemory, page, decision);
            initialStage = -5;
            if (!string.Equals(memory.outcomeTypeId, page.OutcomeTypeId.Value, StringComparison.Ordinal)
                || memory.status != page.Status)
            {
                throw new InvalidOperationException(
                    "A memory signature attempted to merge different outcome types or statuses.");
            }
        }

        deltas.Add(new GameplayOutcomeCompactedDelta
        {
            Key = key,
            ExistingIndex = existingIndex,
            Memory = memory
        });

        memory.occurrenceCount = checked(memory.occurrenceCount + 1);
        int subjectIndex = FindSubjectIndex(page, decision.SubjectId);
        if (subjectIndex < 0)
            throw new InvalidOperationException(
                "Compacted memory subject disappeared before merge.");
        GameplayOutcomeSubjectLink sourceSubject = page.Subjects[subjectIndex];
        memory.influenceUseCount = checked(
            memory.influenceUseCount + sourceSubject.InfluenceUseCount);
        memory.influenceRevision = checked(
            memory.influenceRevision + sourceSubject.InfluenceRevision);
        memory.salience = Math.Max(memory.salience, decision.Salience);
        memory.lastSequence = page.Sequence;
        memory.lastDay = page.AbsoluteDay;
        memory.sourceSegmentHash = GameplayOutcomeCanonicalHash.RollSourceHash(memory.sourceSegmentHash, exactHash);
        memory.sharedAggregateId = GameplayOutcomeCanonicalHash.CreateSharedAggregateId(
            state.RunId, page.OutcomeTypeId, page.Status,
            memory.firstSequence, memory.lastSequence, memory.occurrenceCount,
            memory.sourceSegmentHash);
        return new GameplayOutcomeCompactedMergeScratch
        {
            Decision = decision,
            Memory = memory,
            SourceMemory = sourceMemory,
            Stage = initialStage
        };
    }

    private static bool AdvanceCompactedMerge(
        GameplayOutcomeValuePage page,
        GameplayOutcomeCompactedMergeScratch scratch)
    {
        if (scratch.Stage == -5)
        {
            if (scratch.CopyIndex < scratch.SourceMemory.tags.Count)
            {
                scratch.Memory.tags.Add(scratch.SourceMemory.tags[scratch.CopyIndex++]);
                return false;
            }
            scratch.Stage = -4; scratch.CopyIndex = 0; return false;
        }
        if (scratch.Stage == -4)
        {
            if (scratch.CopyIndex < scratch.SourceMemory.metrics.Count)
            {
                GameplayOutcomeMetricAggregateSnapshot source =
                    scratch.SourceMemory.metrics[scratch.CopyIndex++];
                scratch.Memory.metrics.Add(new GameplayOutcomeMetricAggregateSnapshot
                {
                    metricId = source.metricId,
                    unitId = source.unitId,
                    referenceKindId = source.referenceKindId,
                    referenceId = source.referenceId,
                    sum = source.sum,
                    minimum = source.minimum,
                    maximum = source.maximum,
                    sampleCount = source.sampleCount
                });
                return false;
            }
            scratch.Stage = -3; scratch.CopyIndex = 0; return false;
        }
        if (scratch.Stage == -3)
        {
            if (scratch.CopyIndex < scratch.SourceMemory.participants.Count)
            {
                scratch.Memory.participants.Add(GameplayOutcomeSnapshotCodec.CloneParticipant(
                    scratch.SourceMemory.participants[scratch.CopyIndex++]));
                return false;
            }
            scratch.Stage = -2; scratch.CopyIndex = 0; return false;
        }
        if (scratch.Stage == -2)
        {
            if (scratch.CopyIndex < scratch.SourceMemory.provenance.Count)
            {
                GameplayOutcomeProvenanceReferenceSnapshot source =
                    scratch.SourceMemory.provenance[scratch.CopyIndex++];
                scratch.Memory.provenance.Add(new GameplayOutcomeProvenanceReferenceSnapshot
                { kindId = source.kindId, value = source.value });
                return false;
            }
            scratch.Stage = -1; scratch.CopyIndex = 0; return false;
        }
        if (scratch.Stage == -1)
        {
            if (scratch.CopyIndex < scratch.SourceMemory.facts.Count)
            {
                GameplayOutcomeFactSnapshot source = scratch.SourceMemory.facts[scratch.CopyIndex++];
                scratch.Memory.facts.Add(new GameplayOutcomeFactSnapshot
                { factId = source.factId, value = source.value });
                return false;
            }
            scratch.Stage = 0; scratch.CopyIndex = 0; return false;
        }
        if (scratch.Stage == 0)
        {
            if (scratch.TagIndex < page.TagCount)
            {
                string tag = page.Tags[scratch.TagIndex++].Value;
                int position = scratch.Memory.tags.BinarySearch(tag, StringComparer.Ordinal);
                if (position < 0) scratch.Memory.tags.Insert(~position, tag);
                return false;
            }
            scratch.Stage = 1;
            return false;
        }
        if (scratch.Stage == 1)
        {
            if (scratch.MetricIndex < scratch.Decision.AdditiveMetrics.Length)
            {
                if (AdvanceCompactedMetric(
                    scratch,
                    scratch.Decision.AdditiveMetrics[scratch.MetricIndex]))
                    scratch.MetricIndex++;
                return false;
            }
            scratch.Stage = 2;
            return false;
        }
        if (scratch.Stage == 2)
        {
            if (scratch.ParticipantIndex < page.ParticipantCount)
            {
                if (AdvanceCompactedParticipant(
                    scratch,
                    page.Participants[scratch.ParticipantIndex]))
                    scratch.ParticipantIndex++;
                return false;
            }
            scratch.Stage = 3;
            return false;
        }
        if (scratch.Stage == 3)
        {
            if (scratch.ProvenanceIndex < page.ProvenanceCount)
            {
                if (AdvanceCompactedProvenance(
                    scratch,
                    page.Provenance[scratch.ProvenanceIndex]))
                    scratch.ProvenanceIndex++;
                return false;
            }
            scratch.Stage = 4;
            return false;
        }
        if (scratch.Stage == 4)
        {
            // Immutable facts are a compatibility key. Existing aggregates
            // already copied their identical fact set in stage -1.
            if (scratch.SourceMemory == null && scratch.FactIndex < page.FactCount)
            {
                GameplayOutcomeFact source = page.Facts[scratch.FactIndex++];
                scratch.Memory.facts.Add(new GameplayOutcomeFactSnapshot
                { factId = source.FactId.Value, value = source.Value });
                return false;
            }
            scratch.Stage = 5;
            return false;
        }
        return true;
    }

    private static CompactedNarrativeMemorySnapshot CloneCompactedHeader(
        CompactedNarrativeMemorySnapshot source,
        GameplayOutcomeValuePage page,
        in SubjectConsolidationDecision decision) => new CompactedNarrativeMemorySnapshot
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
        tags = new List<string>(Math.Min(
            GameplayOutcomeBufferLimits.AbsoluteMaximumTagsPerOutcome,
            checked(source.tags.Count + page.TagCount))),
        metrics = new List<GameplayOutcomeMetricAggregateSnapshot>(Math.Min(
            GameplayOutcomeBufferLimits.AbsoluteMaximumCompactedMetricKeys,
            checked(source.metrics.Count + decision.AdditiveMetrics.Length))),
        participants = new List<GameplayOutcomeParticipantSnapshot>(Math.Min(
            GameplayOutcomeBufferLimits.AbsoluteMaximumParticipantsPerOutcome,
            checked(source.participants.Count + page.ParticipantCount))),
        provenance = new List<GameplayOutcomeProvenanceReferenceSnapshot>(Math.Min(
            GameplayOutcomeBufferLimits.AbsoluteMaximumProvenancePerOutcome,
            checked(source.provenance.Count + page.ProvenanceCount))),
        facts = new List<GameplayOutcomeFactSnapshot>(source.facts.Count)
    };

    private static bool AdvanceCompactedMetric(
        GameplayOutcomeCompactedMergeScratch scratch,
        in GameplayOutcomeMetric metric)
    {
        List<GameplayOutcomeMetricAggregateSnapshot> rows = scratch.Memory.metrics;
        if (scratch.RowState == 0)
        {
            scratch.SearchLow = 0;
            scratch.SearchHigh = rows.Count;
            scratch.RowState = 1;
        }
        if (scratch.RowState == 1 && scratch.SearchLow < scratch.SearchHigh)
        {
            int middle = scratch.SearchLow + ((scratch.SearchHigh - scratch.SearchLow) >> 1);
            int order = CompareMetricAggregateToMetric(rows[middle], metric);
            if (order == 0)
            {
                GameplayOutcomeMetricAggregateSnapshot aggregate = rows[middle];
                double sum = aggregate.sum + metric.Value;
                if (double.IsNaN(sum) || double.IsInfinity(sum))
                    throw new InvalidOperationException("Compacted metric sum is non-finite.");
                aggregate.sum = sum;
                aggregate.minimum = Math.Min(aggregate.minimum, metric.Value);
                aggregate.maximum = Math.Max(aggregate.maximum, metric.Value);
                aggregate.sampleCount = checked(aggregate.sampleCount + 1);
                ResetRowMerge(scratch);
                return true;
            }
            if (order < 0) scratch.SearchLow = middle + 1;
            else scratch.SearchHigh = middle;
            return false;
        }
        if (scratch.RowState == 1)
        {
            if (rows.Count >= GameplayOutcomeBufferLimits.AbsoluteMaximumCompactedMetricKeys)
                throw new InvalidOperationException("Compacted metric provenance exceeds its declared bound.");
            scratch.PendingMetric = new GameplayOutcomeMetricAggregateSnapshot
            {
                metricId = metric.MetricId.Value,
                unitId = metric.UnitId.Value,
                referenceKindId = metric.DefinitionOrInstanceId.Kind.Value ?? string.Empty,
                referenceId = metric.DefinitionOrInstanceId.Value ?? string.Empty,
                sum = metric.Value,
                minimum = metric.Value,
                maximum = metric.Value,
                sampleCount = 1
            };
            scratch.InsertIndex = scratch.SearchLow;
            rows.Add(scratch.PendingMetric);
            scratch.ShiftIndex = rows.Count - 1;
            scratch.RowState = 2;
            return false;
        }
        if (scratch.ShiftIndex > scratch.InsertIndex)
        {
            rows[scratch.ShiftIndex] = rows[scratch.ShiftIndex - 1];
            scratch.ShiftIndex--;
            return false;
        }
        rows[scratch.InsertIndex] = scratch.PendingMetric;
        ResetRowMerge(scratch);
        return true;
    }

    private static bool AdvanceCompactedParticipant(
        GameplayOutcomeCompactedMergeScratch scratch,
        in GameplayOutcomeParticipant participant)
    {
        List<GameplayOutcomeParticipantSnapshot> rows = scratch.Memory.participants;
        if (scratch.RowState == 0)
        {
            scratch.PendingParticipant = GameplayOutcomeSnapshotCodec.CaptureParticipant(participant);
            scratch.SearchLow = 0;
            scratch.SearchHigh = rows.Count;
            scratch.RowState = 1;
        }
        if (scratch.RowState == 1 && scratch.SearchLow < scratch.SearchHigh)
        {
            int middle = scratch.SearchLow + ((scratch.SearchHigh - scratch.SearchLow) >> 1);
            int order = GameplayOutcomeSnapshotCodec.CompareParticipants(rows[middle], scratch.PendingParticipant);
            if (order == 0)
            {
                ResetRowMerge(scratch);
                return true;
            }
            if (order < 0) scratch.SearchLow = middle + 1;
            else scratch.SearchHigh = middle;
            return false;
        }
        if (scratch.RowState == 1)
        {
            if (rows.Count >= GameplayOutcomeBufferLimits.AbsoluteMaximumParticipantsPerOutcome)
                throw new InvalidOperationException("Compacted participant provenance exceeds its declared bound.");
            scratch.InsertIndex = scratch.SearchLow;
            rows.Add(scratch.PendingParticipant);
            scratch.ShiftIndex = rows.Count - 1;
            scratch.RowState = 2;
            return false;
        }
        if (scratch.ShiftIndex > scratch.InsertIndex)
        {
            rows[scratch.ShiftIndex] = rows[scratch.ShiftIndex - 1];
            scratch.ShiftIndex--;
            return false;
        }
        rows[scratch.InsertIndex] = scratch.PendingParticipant;
        ResetRowMerge(scratch);
        return true;
    }

    private static bool AdvanceCompactedProvenance(
        GameplayOutcomeCompactedMergeScratch scratch,
        in GameplayOutcomeProvenanceReference source)
    {
        List<GameplayOutcomeProvenanceReferenceSnapshot> rows = scratch.Memory.provenance;
        if (scratch.RowState == 0)
        {
            scratch.PendingProvenance = new GameplayOutcomeProvenanceReferenceSnapshot
            { kindId = source.KindId, value = source.Value };
            scratch.SearchLow = 0;
            scratch.SearchHigh = rows.Count;
            scratch.RowState = 1;
        }
        if (scratch.RowState == 1 && scratch.SearchLow < scratch.SearchHigh)
        {
            int middle = scratch.SearchLow + ((scratch.SearchHigh - scratch.SearchLow) >> 1);
            int order = CompareProvenance(rows[middle], scratch.PendingProvenance);
            if (order == 0)
            {
                ResetRowMerge(scratch);
                return true;
            }
            if (order < 0) scratch.SearchLow = middle + 1;
            else scratch.SearchHigh = middle;
            return false;
        }
        if (scratch.RowState == 1)
        {
            if (rows.Count >= GameplayOutcomeBufferLimits.AbsoluteMaximumProvenancePerOutcome)
                throw new InvalidOperationException("Compacted receipt provenance exceeds its declared bound.");
            scratch.InsertIndex = scratch.SearchLow;
            rows.Add(scratch.PendingProvenance);
            scratch.ShiftIndex = rows.Count - 1;
            scratch.RowState = 2;
            return false;
        }
        if (scratch.ShiftIndex > scratch.InsertIndex)
        {
            rows[scratch.ShiftIndex] = rows[scratch.ShiftIndex - 1];
            scratch.ShiftIndex--;
            return false;
        }
        rows[scratch.InsertIndex] = scratch.PendingProvenance;
        ResetRowMerge(scratch);
        return true;
    }

    private static void ResetRowMerge(GameplayOutcomeCompactedMergeScratch scratch)
    {
        scratch.RowState = 0;
        scratch.SearchLow = 0;
        scratch.SearchHigh = 0;
        scratch.InsertIndex = 0;
        scratch.ShiftIndex = 0;
        scratch.PendingMetric = null;
        scratch.PendingParticipant = null;
        scratch.PendingProvenance = null;
    }

    private static int CompareMetricAggregateToMetric(
        GameplayOutcomeMetricAggregateSnapshot left,
        in GameplayOutcomeMetric right)
    {
        int value = string.CompareOrdinal(left.metricId, right.MetricId.Value);
        if (value != 0) return value;
        value = string.CompareOrdinal(left.unitId, right.UnitId.Value);
        if (value != 0) return value;
        value = string.CompareOrdinal(left.referenceKindId, right.DefinitionOrInstanceId.Kind.Value);
        return value != 0 ? value : string.CompareOrdinal(left.referenceId, right.DefinitionOrInstanceId.Value);
    }

    private static int CompareProvenance(
        GameplayOutcomeProvenanceReferenceSnapshot left,
        GameplayOutcomeProvenanceReferenceSnapshot right)
    {
        int value = string.CompareOrdinal(left.kindId, right.kindId);
        return value != 0 ? value : string.CompareOrdinal(left.value, right.value);
    }

    private void ClearConsolidationScratch()
    {
        DisposeConsolidationScratch(state);
    }

    private static void DisposeConsolidationScratch(GameplayOutcomeLedgerRuntimeState target)
    {
        target?.ConsolidationScratch?.CurrentHash?.Dispose();
        if (target != null) target.ConsolidationScratch = null;
    }

    private static int CompareMetricAggregates(
        GameplayOutcomeMetricAggregateSnapshot left,
        GameplayOutcomeMetricAggregateSnapshot right)
    {
        int metric = string.CompareOrdinal(left?.metricId, right?.metricId);
        if (metric != 0) return metric;
        int unit = string.CompareOrdinal(left?.unitId, right?.unitId);
        if (unit != 0) return unit;
        int kind = string.CompareOrdinal(left?.referenceKindId, right?.referenceKindId);
        return kind != 0 ? kind : string.CompareOrdinal(left?.referenceId, right?.referenceId);
    }

    private bool CanMergeCompactedTarget(
        GameplayOutcomeValuePage page,
        GameplayEntityId subjectId,
        GameplayMemorySignature signature,
        IOutcomeMemoryConsolidator consolidator)
    {
        CompactedMemoryKey key = new CompactedMemoryKey(subjectId, signature);
        if (!state.CompactedByKey.TryGetValue(key, out int index))
            return true;
        CompactedNarrativeMemorySnapshot memory = state.CompactedMemories[index];
        if (!string.Equals(memory.outcomeTypeId, page.OutcomeTypeId.Value, StringComparison.Ordinal)
            || memory.status != page.Status
            || !string.Equals(memory.locationId, page.Location.LocationId, StringComparison.Ordinal)
            || !string.Equals(memory.roomId, page.Location.RoomId, StringComparison.Ordinal)
            || memory.locationX != page.Location.X
            || memory.locationY != page.Location.Y
            // Conservative boundedness: descriptor compatibility may allow
            // duplicates, but a merge is rejected before scratch begins when
            // its worst-case unique union cannot fit the declared cap.
            || memory.tags.Count + page.TagCount > GameplayOutcomeBufferLimits.AbsoluteMaximumTagsPerOutcome
            || memory.metrics.Count + page.MetricCount > GameplayOutcomeBufferLimits.AbsoluteMaximumCompactedMetricKeys
            || memory.participants.Count + page.ParticipantCount > GameplayOutcomeBufferLimits.AbsoluteMaximumParticipantsPerOutcome
            || memory.provenance.Count + page.ProvenanceCount > GameplayOutcomeBufferLimits.AbsoluteMaximumProvenancePerOutcome)
            return false;
        if (memory.facts == null || memory.facts.Count != page.FactCount)
            return false;
        for (int factIndex = 0; factIndex < page.FactCount; factIndex++)
        {
            GameplayOutcomeFactSnapshot existingFact = memory.facts[factIndex];
            GameplayOutcomeFact incomingFact = page.Facts[factIndex];
            if (existingFact == null
                || !string.Equals(existingFact.factId, incomingFact.FactId.Value, StringComparison.Ordinal)
                || !string.Equals(existingFact.value, incomingFact.Value, StringComparison.Ordinal))
                return false;
        }
        return consolidator is IOutcomeCompactionContract contract
            && contract.SupportsCompaction
            && contract.CanMerge(
                new CompactedNarrativeMemoryReadView(memory),
                new GameplayOutcomeReadView(page),
                subjectId);
    }

    private int TryGetCompactedOccurrenceCount(GameplayEntityId subjectId, GameplayMemorySignature signature)
    {
        CompactedMemoryKey key = new CompactedMemoryKey(subjectId, signature);
        return state.CompactedByKey.TryGetValue(key, out int index)
            ? state.CompactedMemories[index].occurrenceCount
            : 0;
    }

    private bool TryGetMutablePublishedPage(
        GameplayOutcomeId outcomeId,
        out GameplayOutcomeValuePage page)
    {
        if (outcomeId.IsValid
            && state.ActiveByOutcomeId.TryGetValue(outcomeId, out int pageIndex))
        {
            page = state.Pool.Get(pageIndex);
            if (IsPublished(page))
                return true;
        }
        page = null;
        return false;
    }

    private static int FindSubjectIndex(GameplayOutcomeValuePage page, GameplayEntityId subjectId)
    {
        for (int index = 0; index < page.SubjectCount; index++)
        {
            if (page.Subjects[index].SubjectId == subjectId)
                return index;
        }
        return -1;
    }

    private static NarrativeMemoryTier HigherRetention(
        NarrativeMemoryTier current,
        NarrativeMemoryTier candidate)
    {
        if (current == NarrativeMemoryTier.Core || candidate == NarrativeMemoryTier.Core)
            return NarrativeMemoryTier.Core;
        if (current == NarrativeMemoryTier.Episodic || candidate == NarrativeMemoryTier.Episodic)
            return NarrativeMemoryTier.Episodic;
        return NarrativeMemoryTier.Recent;
    }

    internal readonly struct SubjectConsolidationDecision
    {
        public SubjectConsolidationDecision(
            GameplayEntityId subjectId,
            float salience,
            NarrativeMemoryTier tier,
            GameplayMemorySignature signature,
            bool compact,
            int nextEvaluationDay,
            GameplayOutcomeMetric[] additiveMetrics)
        {
            SubjectId = subjectId;
            Salience = salience;
            Tier = tier;
            Signature = signature;
            Compact = compact;
            NextEvaluationDay = nextEvaluationDay;
            AdditiveMetrics = additiveMetrics ?? Array.Empty<GameplayOutcomeMetric>();
        }
        public GameplayEntityId SubjectId { get; }
        public float Salience { get; }
        public NarrativeMemoryTier Tier { get; }
        public GameplayMemorySignature Signature { get; }
        public bool Compact { get; }
        public int NextEvaluationDay { get; }
        public GameplayOutcomeMetric[] AdditiveMetrics { get; }
    }
}
