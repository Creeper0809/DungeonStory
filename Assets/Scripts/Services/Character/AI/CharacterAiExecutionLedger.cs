using System;
using UnityEngine;

// A value captured by the Unity host at a notification boundary. No live actor,
// action, clock callback or scene reference is retained by the execution ledger.
internal readonly struct CharacterAiTraceContext
{
    internal readonly float Time;
    internal readonly int? DefinitionId;
    internal readonly int? DestinationId;
    internal readonly CharacterAiBranch? Branch;
    internal readonly int PhaseCode;

    internal CharacterAiTraceContext(float time, int? definitionId, int? destinationId,
        CharacterAiBranch? branch, int phaseCode)
    {
        Time = time;
        DefinitionId = definitionId;
        DestinationId = destinationId;
        Branch = branch;
        PhaseCode = phaseCode;
    }
}

// Owns execution conservation and its bounded journal. The host still owns
// selecting/executing actions, Unity objects and presentation/domain effects.
internal sealed class CharacterAiExecutionLedger
{
    private const int RuntimeTraceCapacity = 32;
    private static readonly int BranchCount = Enum.GetValues(typeof(CharacterAiBranch)).Length;
    private long actionStartCount;
    private long actionSwitchCount;
    private long sameActionRestartCount;
    private UnityEngine.Object lastStartedActionSet;
    private readonly CharacterAiRuntimeTraceEvent[] runtimeTrace =
        new CharacterAiRuntimeTraceEvent[RuntimeTraceCapacity];
    private int runtimeTraceWriteIndex;
    private int runtimeTraceCount;
    private long runtimeTraceSequence;
    private long currentActionEpoch;
    private bool actionEpochLive;
    private int currentActionDefinitionId;
    private int currentActionDestinationId;
    private CharacterAiBranch currentActionBranch;
    private CharacterAiRuntimePhase currentRuntimePhase;
    private long runtimeProgressRevision;
    private long gameplayProgressRevision;
    private long facilityQueueHeartbeatCount;
    private int facilityQueuePosition = -1;
    private long facilityQueuePositionRevision;
    private long facilityServiceHeartbeatCount;
    private long actionTerminalCount;
    private long actionCompletedCount;
    private long actionFailedCount;
    private long actionCancelledCount;
    private long pathRequestCount;
    private long pathResultCount;
    private int livePathRequestCount;
    private int currentPathRequestId;
    private int nextPathRequestId;
    private long reservationAcquireCount;
    private long reservationReleaseCount;
    private int liveReservationCount;
    private long retryScheduleCount;
    private long retryAttemptCount;
    private int currentRetryAttempt;
    private long schedulerProcessCount;
    private long schedulerOverdueCount;
    private int maximumSchedulerDelayMilliseconds;
    private long invariantAnomalyCount;
    private CharacterAiRuntimeInvariant lastInvariantAnomaly;
    private CharacterAiRuntimeInvariant activeInvariantMask;
    // Per-branch accounting is deliberately stored in fixed arrays. The hot
    // path only mutates primitive counters; audit snapshots are the only place
    // that allocates a formatted/copy representation.
    private readonly long[] branchActionStarts = new long[BranchCount];
    private readonly long[] branchActionTerminals = new long[BranchCount];
    private readonly int[] branchLiveActions = new int[BranchCount];
    private readonly long[] branchGameplayProgress = new long[BranchCount];
    private readonly long[] branchPathRequests = new long[BranchCount];
    private readonly long[] branchPathResults = new long[BranchCount];
    private readonly int[] branchLivePathRequests = new int[BranchCount];
    private readonly long[] branchReservationAcquires = new long[BranchCount];
    private readonly long[] branchReservationReleases = new long[BranchCount];
    private readonly int[] branchLiveReservations = new int[BranchCount];
    private CharacterAiBranch currentPathRequestBranch;
    private bool committedPathSearchDeferred;
    private long committedPathSearchDeferralCount;

    internal void AdvanceRuntimeProgress(in CharacterAiTraceContext context, long progressMilli = 0L)
    {
        runtimeProgressRevision = checked(runtimeProgressRevision + 1L);
        if (progressMilli != 0L)
        {
            RecordRuntimeTrace(context,
                CharacterAiRuntimeTraceKind.Progress,
                progressMilli: progressMilli);
        }
    }

    internal void AdvanceGameplayProgress(in CharacterAiTraceContext context, long progressMilli = 0L)
    {
        gameplayProgressRevision = checked(gameplayProgressRevision + 1L);
        branchGameplayProgress[GetBranchIndex(ResolveRuntimeBranch(context))]++;
        AdvanceRuntimeProgress(context, progressMilli);
    }

    internal void NotifyMovementStarted(in CharacterAiTraceContext context, int stepCount)
    {
        currentRuntimePhase = CharacterAiRuntimePhase.Moving;
        AdvanceRuntimeProgress(context);
        RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.MovementStarted,
            pathStepCount: Mathf.Max(0, stepCount));
    }

    internal void NotifyMovementProgress(in CharacterAiTraceContext context, int stepIndex, int stepCount)
    {
        currentRuntimePhase = CharacterAiRuntimePhase.Moving;
        AdvanceGameplayProgress(context);
        RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.MovementProgress,
            pathStepIndex: Mathf.Max(0, stepIndex),
            pathStepCount: Mathf.Max(0, stepCount));
    }

    /// <summary>
    /// Records real domain work without treating scheduler bookkeeping as
    /// progress. Long-running non-AbilityWork actions such as rescue use this
    /// so the watchdog observes WU advancement instead of phase text changes.
    /// </summary>
    public void NotifyGameplayWorkProgress(in CharacterAiTraceContext context, float completedWu)
    {
        if (float.IsNaN(completedWu) || float.IsInfinity(completedWu)
            || completedWu < 0f)
            throw new ArgumentOutOfRangeException(nameof(completedWu));
        if (completedWu <= 0f) return;
        long milliWu = Math.Max(
            1L,
            (long)Math.Round(
                completedWu * 1000d,
                MidpointRounding.AwayFromZero));
        AdvanceGameplayProgress(context, milliWu);
    }

    internal void NotifyFacilityQueueHeartbeat(in CharacterAiTraceContext context, int queuePosition)
    {
        if (queuePosition < 0)
            throw new ArgumentOutOfRangeException(nameof(queuePosition));
        facilityQueueHeartbeatCount = checked(facilityQueueHeartbeatCount + 1L);
        if (facilityQueuePosition != queuePosition)
        {
            facilityQueuePosition = queuePosition;
            facilityQueuePositionRevision = checked(
                facilityQueuePositionRevision + 1L);
        }
        AdvanceRuntimeProgress(context);
    }

    internal void NotifyFacilityServiceHeartbeat(in CharacterAiTraceContext context)
    {
        facilityServiceHeartbeatCount = checked(
            facilityServiceHeartbeatCount + 1L);
        AdvanceGameplayProgress(context);
    }

    internal void NotifyMovementTerminal(in CharacterAiTraceContext context, GridMoveFailureReason failureReason)
    {
        AdvanceRuntimeProgress(context);
        RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.MovementTerminal,
            failureKind: failureReason == GridMoveFailureReason.None
                ? AIActionFailureKind.None
                : AIActionFailureKind.NoPath,
            pathState: failureReason == GridMoveFailureReason.None
                ? CharacterAiPathTraceState.Found
                : failureReason == GridMoveFailureReason.Cancelled
                    ? CharacterAiPathTraceState.Cancelled
                    : CharacterAiPathTraceState.NoPath);
    }

    internal int NotifyPathRequested(in CharacterAiTraceContext context,
        bool repath,
        CharacterAiBranch branch = CharacterAiBranch.None)
    {
        // A new attempt now owns the prior deferred request. Its terminal
        // result below decides whether the scheduler must retain retry
        // ownership for another bounded slice.
        committedPathSearchDeferred = false;
        int requestId = nextPathRequestId == int.MaxValue
            ? 1
            : nextPathRequestId + 1;
        nextPathRequestId = requestId;
        currentPathRequestId = requestId;
        currentPathRequestBranch = branch != CharacterAiBranch.None
            ? branch
            : ResolveRuntimeBranch(context);
        int branchIndex = GetBranchIndex(currentPathRequestBranch);
        pathRequestCount++;
        livePathRequestCount++;
        branchPathRequests[branchIndex]++;
        branchLivePathRequests[branchIndex]++;
        AdvanceRuntimeProgress(context);
        RecordRuntimeTrace(context,
            repath
                ? CharacterAiRuntimeTraceKind.PathRepath
                : CharacterAiRuntimeTraceKind.PathRequested,
            pathState: CharacterAiPathTraceState.Requested,
            pathRequestId: requestId);
        return requestId;
    }

    internal void NotifyPathResult(in CharacterAiTraceContext context,
        int requestId,
        CharacterAiPathTraceState state,
        int stepCount)
    {
        bool matchesLiveRequest = currentPathRequestId == requestId
            && livePathRequestCount > 0;
        if (matchesLiveRequest)
        {
            committedPathSearchDeferred =
                state == CharacterAiPathTraceState.Deferred;
            if (committedPathSearchDeferred)
            {
                committedPathSearchDeferralCount = checked(
                    committedPathSearchDeferralCount + 1L);
            }
        }
        int branchIndex = GetBranchIndex(
            matchesLiveRequest
                ? currentPathRequestBranch
                : CharacterAiBranch.None);
        pathResultCount++;
        branchPathResults[branchIndex]++;
        if (matchesLiveRequest)
        {
            livePathRequestCount--;
            if (branchLivePathRequests[branchIndex] > 0)
            {
                branchLivePathRequests[branchIndex]--;
            }
            else
            {
                RecordInvariantAnomaly(context,
                    CharacterAiRuntimeInvariant.PathCounterMismatch);
            }
        }
        else
        {
            RecordInvariantAnomaly(context, CharacterAiRuntimeInvariant.PathCounterMismatch);
        }
        if (matchesLiveRequest)
        {
            currentPathRequestId = 0;
            currentPathRequestBranch = CharacterAiBranch.None;
        }
        AdvanceRuntimeProgress(context);
        RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.PathResult,
            pathState: state,
            pathRequestId: requestId,
            pathStepCount: Mathf.Max(0, stepCount));
    }

    internal void NotifyRetryAttempted(in CharacterAiTraceContext context)
    {
        if (currentRetryAttempt <= 0)
        {
            return;
        }
        retryAttemptCount = checked(retryAttemptCount + 1L);
        RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.RetryAttempted,
            retryAttempt: currentRetryAttempt);
    }

    internal void RecordInvariantAnomaly(in CharacterAiTraceContext context, CharacterAiRuntimeInvariant invariant)
    {
        if (invariant == CharacterAiRuntimeInvariant.None) return;
        invariantAnomalyCount++;
        lastInvariantAnomaly = invariant;
        RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.InvariantAnomaly,
            invariant: invariant);
    }

    internal void RecordRuntimeTrace(in CharacterAiTraceContext context,
        CharacterAiRuntimeTraceKind kind,
        AIActionFailureKind failureKind = AIActionFailureKind.None,
        CharacterAiActionTerminalKind terminalKind = CharacterAiActionTerminalKind.None,
        CharacterAiPathTraceState pathState = CharacterAiPathTraceState.None,
        CharacterAiReservationTraceState reservationState = CharacterAiReservationTraceState.None,
        CharacterAiRuntimeInvariant invariant = CharacterAiRuntimeInvariant.None,
        int pathRequestId = 0,
        int pathStepIndex = 0,
        int pathStepCount = 0,
        int reservationId = 0,
        int retryAttempt = 0,
        int delayMilliseconds = 0,
        long progressMilli = 0L)
    {
        int actionDefinitionId = context.DefinitionId ?? currentActionDefinitionId;
        int destinationId = context.DestinationId ?? currentActionDestinationId;
        int phaseCode = context.PhaseCode;
        long sequence = checked(runtimeTraceSequence + 1L);
        runtimeTraceSequence = sequence;
        runtimeTrace[runtimeTraceWriteIndex] = new CharacterAiRuntimeTraceEvent(
            sequence,
            currentActionEpoch,
            context.Time,
            kind,
            context.Branch ?? currentActionBranch,
            failureKind,
            actionDefinitionId,
            destinationId,
            phaseCode,
            runtimeProgressRevision,
            currentRuntimePhase,
            terminalKind,
            pathState,
            reservationState,
            invariant,
            pathRequestId,
            pathStepIndex,
            pathStepCount,
            reservationId,
            retryAttempt,
            delayMilliseconds,
            progressMilli);
        runtimeTraceWriteIndex = (runtimeTraceWriteIndex + 1)
            % RuntimeTraceCapacity;
        runtimeTraceCount = Mathf.Min(
            RuntimeTraceCapacity,
            runtimeTraceCount + 1);
    }

    internal CharacterAiRuntimeTraceEvent[] CopyRuntimeTrace()
    {
        if (runtimeTraceCount == 0)
        {
            return Array.Empty<CharacterAiRuntimeTraceEvent>();
        }

        CharacterAiRuntimeTraceEvent[] copy =
            new CharacterAiRuntimeTraceEvent[runtimeTraceCount];
        int start = runtimeTraceCount == RuntimeTraceCapacity
            ? runtimeTraceWriteIndex
            : 0;
        for (int index = 0; index < runtimeTraceCount; index++)
        {
            copy[index] = runtimeTrace[
                (start + index) % RuntimeTraceCapacity];
        }

        return copy;
    }

    internal CharacterAiRuntimeGateSnapshot CaptureRuntimeGateSnapshot(long failureLoopCount)
    {
        CharacterAiBranchRuntimeGateSnapshot[] branches =
            new CharacterAiBranchRuntimeGateSnapshot[BranchCount];
        for (int index = 0; index < BranchCount; index++)
        {
            branches[index] = new CharacterAiBranchRuntimeGateSnapshot(
                branchActionStarts[index],
                branchActionTerminals[index],
                branchLiveActions[index],
                branchGameplayProgress[index],
                branchPathRequests[index],
                branchPathResults[index],
                branchLivePathRequests[index],
                branchReservationAcquires[index],
                branchReservationReleases[index],
                branchLiveReservations[index]);
        }

        return new CharacterAiRuntimeGateSnapshot(
            actionStartCount,
            actionTerminalCount,
            actionCompletedCount,
            actionFailedCount,
            actionCancelledCount,
            actionEpochLive ? 1 : 0,
            runtimeProgressRevision,
            gameplayProgressRevision,
            facilityQueueHeartbeatCount,
            facilityServiceHeartbeatCount,
            pathRequestCount,
            pathResultCount,
            livePathRequestCount,
            reservationAcquireCount,
            reservationReleaseCount,
            liveReservationCount,
            retryScheduleCount,
            retryAttemptCount,
            schedulerProcessCount,
            schedulerOverdueCount,
            maximumSchedulerDelayMilliseconds,
            invariantAnomalyCount,
            failureLoopCount,
            branches);
    }

    private static int GetBranchIndex(CharacterAiBranch branch)
    {
        int index = (int)branch;
        return index >= 0 && index < BranchCount ? index : 0;
    }

    private CharacterAiBranch ResolveRuntimeBranch(in CharacterAiTraceContext context)
    {
        CharacterAiBranch branch = context.Branch ?? currentActionBranch;
        return GetBranchIndex(branch) == 0 ? CharacterAiBranch.None : branch;
    }

    internal void ClearCommittedPathSearchDeferral() => committedPathSearchDeferred = false;
    internal void SetRuntimePhase(CharacterAiRuntimePhase phase) => currentRuntimePhase = phase;
    internal void SetRuntimePhaseForPresentation(string phase) => currentRuntimePhase = ClassifyRuntimePhase(phase);

    private static CharacterAiRuntimePhase ClassifyRuntimePhase(string phase)
    {
        if (string.IsNullOrEmpty(phase)) return CharacterAiRuntimePhase.None;
        if (phase == "선택") return CharacterAiRuntimePhase.Selected;
        if (phase == "시작") return CharacterAiRuntimePhase.Starting;
        if (phase == "이동" || phase.Contains("이동", StringComparison.Ordinal)
            || phase.Contains("접근", StringComparison.Ordinal)
            || phase.Contains("추적", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.Moving;
        if (phase.Contains("재탐색", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.Repathing;
        if (phase == "도착") return CharacterAiRuntimePhase.Arrived;
        if (phase.Contains("접수", StringComparison.Ordinal)
            || phase.Contains("자리 잡기", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.FacilityAdmission;
        if (phase.Contains("대기", StringComparison.Ordinal)
            || phase.Contains("예약", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.FacilityQueue;
        if (phase.Contains("이용", StringComparison.Ordinal)
            || phase.Contains("식사 중", StringComparison.Ordinal)
            || phase.Contains("계산 중", StringComparison.Ordinal)
            || phase.Contains("서비스", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.FacilityService;
        if (phase.Contains("작업", StringComparison.Ordinal)
            || phase.Contains("공사", StringComparison.Ordinal)
            || phase.Contains("수리", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.Working;
        if (phase.Contains("기다", StringComparison.Ordinal)
            || phase.Contains("둘러보기", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.Waiting;
        return CharacterAiRuntimePhase.Unknown;
    }

    internal void ResetSchedulerDelayTelemetryForDiagnostics() => maximumSchedulerDelayMilliseconds = 0;

    internal void SetSelectedDefinition(int definitionId, int destinationId, CharacterAiBranch branch)
    {
        currentActionDefinitionId = definitionId;
        currentActionDestinationId = destinationId;
        currentActionBranch = branch;
    }

    internal void NoteActionStarted(UnityEngine.Object definition)
    {
        actionStartCount++;
        if (lastStartedActionSet == definition)
        {
            sameActionRestartCount++;
        }
        else if (lastStartedActionSet != null)
        {
            actionSwitchCount++;
        }
        lastStartedActionSet = definition;
    }

    internal void BeginEpoch(int definitionId, int destinationId, CharacterAiBranch branch,
        in CharacterAiTraceContext context)
    {
        currentActionEpoch = checked(currentActionEpoch + 1L);
        actionEpochLive = true;
        currentActionDefinitionId = definitionId;
        currentActionDestinationId = destinationId;
        currentActionBranch = branch;
        int branchIndex = GetBranchIndex(currentActionBranch);
        branchActionStarts[branchIndex]++;
        branchLiveActions[branchIndex]++;
        currentRuntimePhase = CharacterAiRuntimePhase.Starting;
        AdvanceRuntimeProgress(context);
        RecordRuntimeTrace(context, CharacterAiRuntimeTraceKind.ActionStarted);
    }

    internal bool TryCloseEpoch(ref CharacterAiActionTerminalKind terminalKind,
        in CharacterAiTraceContext context)
    {
        if (!actionEpochLive) return false;
        actionEpochLive = false;
        actionTerminalCount++;
        int branchIndex = GetBranchIndex(currentActionBranch);
        branchActionTerminals[branchIndex]++;
        if (branchLiveActions[branchIndex] > 0)
        {
            branchLiveActions[branchIndex]--;
        }
        else
        {
            RecordInvariantAnomaly(context,
                CharacterAiRuntimeInvariant.LiveEpochWithoutAction);
        }
        switch (terminalKind)
        {
            case CharacterAiActionTerminalKind.Failed:
                actionFailedCount++;
                break;
            case CharacterAiActionTerminalKind.Cancelled:
            case CharacterAiActionTerminalKind.Recovered:
                actionCancelledCount++;
                break;
            default:
                actionCompletedCount++;
                terminalKind = CharacterAiActionTerminalKind.Completed;
                break;
        }

        return true;
    }

    internal void RecordTerminal(CharacterAiActionTerminalKind terminalKind, in CharacterAiTraceContext context)
    {
        currentRuntimePhase = CharacterAiRuntimePhase.Terminal;
        AdvanceRuntimeProgress(context);
        RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.ActionTerminal,
            terminalKind: terminalKind);
    }

    internal void NotifyReservationAcquired(CharacterAiBranch branch, int reservationId,
        in CharacterAiTraceContext context)
    {
        int branchIndex = GetBranchIndex(branch);
        reservationAcquireCount++;
        liveReservationCount++;
        branchReservationAcquires[branchIndex]++;
        branchLiveReservations[branchIndex]++;
        AdvanceRuntimeProgress(context);
        RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.ReservationAcquired,
            reservationState: CharacterAiReservationTraceState.Acquired,
            reservationId: reservationId);
    }

    internal void NotifyReservationReleased(CharacterAiBranch branch, int reservationId,
        in CharacterAiTraceContext context)
    {
        int branchIndex = GetBranchIndex(branch);
        reservationReleaseCount++;
        branchReservationReleases[branchIndex]++;
        if (liveReservationCount > 0)
        {
            liveReservationCount--;
            if (branchLiveReservations[branchIndex] > 0)
            {
                branchLiveReservations[branchIndex]--;
            }
            else
            {
                RecordInvariantAnomaly(context,
                    CharacterAiRuntimeInvariant.ReservationCounterMismatch);
            }
        }
        else
        {
            RecordInvariantAnomaly(context, CharacterAiRuntimeInvariant.ReservationCounterMismatch);
        }
        AdvanceRuntimeProgress(context);
        RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.ReservationReleased,
            reservationState: CharacterAiReservationTraceState.Released,
            reservationId: reservationId);
    }

    internal void NotifyRetryScheduled(in CharacterAiTraceContext context,
        float delaySeconds,
        CharacterAiRetryKind retryKind)
    {
        if (retryKind == CharacterAiRetryKind.None)
        {
            throw new ArgumentException(
                "A retry schedule requires a classified retry kind.",
                nameof(retryKind));
        }
        retryScheduleCount++;
        currentRetryAttempt++;
        AdvanceRuntimeProgress(context);
        RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.RetryScheduled,
            retryAttempt: currentRetryAttempt,
            delayMilliseconds: Mathf.Max(0, Mathf.RoundToInt(delaySeconds * 1000f)));
    }

    internal void NotifySchedulerDecisionProcessed(in CharacterAiTraceContext context,
        float dueTime,
        float processedAt,
        bool decided,
        bool retryScheduled)
    {
        schedulerProcessCount = checked(schedulerProcessCount + 1L);
        if (!retryScheduled)
        {
            currentRetryAttempt = 0;
        }
        AdvanceRuntimeProgress(context);
        if (processedAt > dueTime + 0.0001f)
        {
            int lagMilliseconds = Mathf.Max(
                0,
                Mathf.RoundToInt((processedAt - dueTime) * 1000f));
            maximumSchedulerDelayMilliseconds = Mathf.Max(
                maximumSchedulerDelayMilliseconds,
                lagMilliseconds);
            if (lagMilliseconds >= 2000)
            {
                schedulerOverdueCount++;
                RecordRuntimeTrace(context,
                    CharacterAiRuntimeTraceKind.SchedulerOverdue,
                    delayMilliseconds: lagMilliseconds);
            }
        }
    }

    internal void AuditRuntimeInvariants(CharacterAiRuntimeInvariant mask, int actualReservations,
        in CharacterAiTraceContext context)
    {
        if (actualReservations != liveReservationCount)
            mask |= CharacterAiRuntimeInvariant.ReservationCounterMismatch;
        if (livePathRequestCount < 0 || livePathRequestCount > 1)
            mask |= CharacterAiRuntimeInvariant.PathCounterMismatch;
        int branchActionLiveTotal = 0;
        int branchPathLiveTotal = 0;
        int branchReservationLiveTotal = 0;
        for (int index = 0; index < BranchCount; index++)
        {
            branchActionLiveTotal += branchLiveActions[index];
            branchPathLiveTotal += branchLivePathRequests[index];
            branchReservationLiveTotal += branchLiveReservations[index];
        }
        if (branchActionLiveTotal != (actionEpochLive ? 1 : 0)
            || branchPathLiveTotal != livePathRequestCount
            || branchReservationLiveTotal != liveReservationCount)
        {
            mask |= CharacterAiRuntimeInvariant.BranchCounterMismatch;
        }

        CharacterAiRuntimeInvariant newlyActive = mask & ~activeInvariantMask;
        activeInvariantMask = mask;
        if (newlyActive != CharacterAiRuntimeInvariant.None)
        {
            RecordInvariantAnomaly(context, newlyActive);
        }
    }

    internal long ActionStartCount => actionStartCount;
    internal long ActionSwitchCount => actionSwitchCount;
    internal long SameActionRestartCount => sameActionRestartCount;
    internal long CurrentActionEpoch => currentActionEpoch;
    internal bool ActionEpochLive => actionEpochLive;
    internal int CurrentActionDefinitionId => currentActionDefinitionId;
    internal int CurrentActionDestinationId => currentActionDestinationId;
    internal CharacterAiBranch CurrentActionBranch => currentActionBranch;
    internal CharacterAiRuntimePhase CurrentRuntimePhase => currentRuntimePhase;
    internal long RuntimeProgressRevision => runtimeProgressRevision;
    internal long GameplayProgressRevision => gameplayProgressRevision;
    internal long FacilityQueueHeartbeatCount => facilityQueueHeartbeatCount;
    internal long FacilityQueuePositionRevision => facilityQueuePositionRevision;
    internal long FacilityServiceHeartbeatCount => facilityServiceHeartbeatCount;
    internal int LivePathRequestCount => livePathRequestCount;
    internal int LiveReservationCount => liveReservationCount;
    internal long SchedulerProcessCount => schedulerProcessCount;
    internal int MaximumSchedulerDelayMilliseconds => maximumSchedulerDelayMilliseconds;
    internal long InvariantAnomalyCount => invariantAnomalyCount;
    internal CharacterAiRuntimeInvariant LastInvariantAnomaly => lastInvariantAnomaly;
    internal bool CommittedPathSearchDeferred => committedPathSearchDeferred;
    internal long CommittedPathSearchDeferralCount => committedPathSearchDeferralCount;
}
