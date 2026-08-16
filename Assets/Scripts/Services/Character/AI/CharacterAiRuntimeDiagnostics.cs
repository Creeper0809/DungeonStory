using System;
using System.Text;

public readonly struct CharacterAiBranchRuntimeGateSnapshot
{
    public CharacterAiBranchRuntimeGateSnapshot(
        long actionStarts,
        long actionTerminals,
        int liveActions,
        long gameplayProgress,
        long pathRequests,
        long pathResults,
        int livePathRequests,
        long reservationAcquires,
        long reservationReleases,
        int liveReservations)
    {
        ActionStarts = actionStarts;
        ActionTerminals = actionTerminals;
        LiveActions = liveActions;
        GameplayProgress = gameplayProgress;
        PathRequests = pathRequests;
        PathResults = pathResults;
        LivePathRequests = livePathRequests;
        ReservationAcquires = reservationAcquires;
        ReservationReleases = reservationReleases;
        LiveReservations = liveReservations;
    }

    public long ActionStarts { get; }
    public long ActionTerminals { get; }
    public int LiveActions { get; }
    public long GameplayProgress { get; }
    public long PathRequests { get; }
    public long PathResults { get; }
    public int LivePathRequests { get; }
    public long ReservationAcquires { get; }
    public long ReservationReleases { get; }
    public int LiveReservations { get; }

    public bool WasObservedFrom(in CharacterAiBranchRuntimeGateSnapshot start) =>
        ActionStarts > start.ActionStarts
        || ActionTerminals > start.ActionTerminals
        || GameplayProgress > start.GameplayProgress
        || PathRequests > start.PathRequests
        || ReservationAcquires > start.ReservationAcquires
        || LiveActions > 0
        || LivePathRequests > 0
        || LiveReservations > 0;

    public bool ConservesFrom(in CharacterAiBranchRuntimeGateSnapshot start) =>
        ActionStarts - start.ActionStarts + start.LiveActions
            == ActionTerminals - start.ActionTerminals + LiveActions
        && PathRequests - start.PathRequests + start.LivePathRequests
            == PathResults - start.PathResults + LivePathRequests
        && ReservationAcquires - start.ReservationAcquires
            + start.LiveReservations
            == ReservationReleases - start.ReservationReleases
                + LiveReservations;
}

/// <summary>
/// Allocation-free counters used by long-running and high-population gates.
/// A measured interval conserves ownership when starts plus the initially-live
/// count equal terminals plus the finally-live count.
/// </summary>
public readonly struct CharacterAiRuntimeGateSnapshot
{
    public CharacterAiRuntimeGateSnapshot(
        long actionStarts,
        long actionTerminals,
        long actionCompleted,
        long actionFailed,
        long actionCancelled,
        int liveActions,
        long progressRevision,
        long gameplayProgressRevision,
        long facilityQueueHeartbeats,
        long facilityServiceHeartbeats,
        long pathRequests,
        long pathResults,
        int livePathRequests,
        long reservationAcquires,
        long reservationReleases,
        int liveReservations,
        long retrySchedules,
        long retryAttempts,
        long schedulerProcesses,
        long schedulerOverdue,
        int maximumSchedulerDelayMilliseconds,
        long invariantAnomalies,
        long failureLoops,
        CharacterAiBranchRuntimeGateSnapshot[] branches = null)
    {
        ActionStarts = actionStarts;
        ActionTerminals = actionTerminals;
        ActionCompleted = actionCompleted;
        ActionFailed = actionFailed;
        ActionCancelled = actionCancelled;
        LiveActions = liveActions;
        ProgressRevision = progressRevision;
        GameplayProgressRevision = gameplayProgressRevision;
        FacilityQueueHeartbeats = facilityQueueHeartbeats;
        FacilityServiceHeartbeats = facilityServiceHeartbeats;
        PathRequests = pathRequests;
        PathResults = pathResults;
        LivePathRequests = livePathRequests;
        ReservationAcquires = reservationAcquires;
        ReservationReleases = reservationReleases;
        LiveReservations = liveReservations;
        RetrySchedules = retrySchedules;
        RetryAttempts = retryAttempts;
        SchedulerProcesses = schedulerProcesses;
        SchedulerOverdue = schedulerOverdue;
        MaximumSchedulerDelayMilliseconds = maximumSchedulerDelayMilliseconds;
        InvariantAnomalies = invariantAnomalies;
        FailureLoops = failureLoops;
        this.branches = branches;
    }

    public long ActionStarts { get; }
    public long ActionTerminals { get; }
    public long ActionCompleted { get; }
    public long ActionFailed { get; }
    public long ActionCancelled { get; }
    public int LiveActions { get; }
    public long ProgressRevision { get; }
    public long GameplayProgressRevision { get; }
    public long FacilityQueueHeartbeats { get; }
    public long FacilityServiceHeartbeats { get; }
    public long PathRequests { get; }
    public long PathResults { get; }
    public int LivePathRequests { get; }
    public long ReservationAcquires { get; }
    public long ReservationReleases { get; }
    public int LiveReservations { get; }
    public long RetrySchedules { get; }
    public long RetryAttempts { get; }
    public long SchedulerProcesses { get; }
    public long SchedulerOverdue { get; }
    public int MaximumSchedulerDelayMilliseconds { get; }
    public long InvariantAnomalies { get; }
    public long FailureLoops { get; }
    private readonly CharacterAiBranchRuntimeGateSnapshot[] branches;

    public CharacterAiBranchRuntimeGateSnapshot GetBranch(
        CharacterAiBranch branch)
    {
        int index = (int)branch;
        return branches != null && index >= 0 && index < branches.Length
            ? branches[index]
            : default;
    }

    public bool ConservesObservedBranchesFrom(
        in CharacterAiRuntimeGateSnapshot start)
    {
        int count = Math.Max(
            branches?.Length ?? 0,
            start.branches?.Length ?? 0);
        for (int index = 1; index < count; index++)
        {
            CharacterAiBranchRuntimeGateSnapshot endBranch =
                GetBranch((CharacterAiBranch)index);
            CharacterAiBranchRuntimeGateSnapshot startBranch =
                start.GetBranch((CharacterAiBranch)index);
            if (endBranch.WasObservedFrom(in startBranch)
                && !endBranch.ConservesFrom(in startBranch))
            {
                return false;
            }
        }
        return true;
    }

    public string FormatObservedBranchesFrom(
        in CharacterAiRuntimeGateSnapshot start)
    {
        StringBuilder builder = new StringBuilder(192);
        int count = Math.Max(
            branches?.Length ?? 0,
            start.branches?.Length ?? 0);
        for (int index = 1; index < count; index++)
        {
            CharacterAiBranch branch = (CharacterAiBranch)index;
            CharacterAiBranchRuntimeGateSnapshot endBranch = GetBranch(branch);
            CharacterAiBranchRuntimeGateSnapshot startBranch =
                start.GetBranch(branch);
            if (!endBranch.WasObservedFrom(in startBranch)) continue;
            if (builder.Length > 0) builder.Append(" | ");
            builder.Append(branch)
                .Append(":a=")
                .Append(endBranch.ActionStarts - startBranch.ActionStarts)
                .Append('/')
                .Append(endBranch.ActionTerminals - startBranch.ActionTerminals)
                .Append('/')
                .Append(endBranch.LiveActions)
                .Append(",g=")
                .Append(endBranch.GameplayProgress - startBranch.GameplayProgress)
                .Append(",p=")
                .Append(endBranch.PathRequests - startBranch.PathRequests)
                .Append('/')
                .Append(endBranch.PathResults - startBranch.PathResults)
                .Append('/')
                .Append(endBranch.LivePathRequests)
                .Append(",r=")
                .Append(endBranch.ReservationAcquires - startBranch.ReservationAcquires)
                .Append('/')
                .Append(endBranch.ReservationReleases - startBranch.ReservationReleases)
                .Append('/')
                .Append(endBranch.LiveReservations);
        }
        return builder.Length > 0 ? builder.ToString() : "none";
    }

    public bool HasProgressFrom(in CharacterAiRuntimeGateSnapshot start) =>
        ProgressRevision > start.ProgressRevision;

    public bool HasGameplayProgressFrom(
        in CharacterAiRuntimeGateSnapshot start) =>
        GameplayProgressRevision > start.GameplayProgressRevision;

    public bool HasHealthyActivityFrom(
        in CharacterAiRuntimeGateSnapshot start) =>
        HasGameplayProgressFrom(in start)
        || FacilityQueueHeartbeats > start.FacilityQueueHeartbeats
        || FacilityServiceHeartbeats > start.FacilityServiceHeartbeats;

    public bool ConservesLifecycleFrom(in CharacterAiRuntimeGateSnapshot start) =>
        ActionStarts - start.ActionStarts + start.LiveActions
            == ActionTerminals - start.ActionTerminals + LiveActions;

    public bool ConservesPathsFrom(in CharacterAiRuntimeGateSnapshot start) =>
        PathRequests - start.PathRequests + start.LivePathRequests
            == PathResults - start.PathResults + LivePathRequests;

    public bool ConservesReservationsFrom(in CharacterAiRuntimeGateSnapshot start) =>
        ReservationAcquires - start.ReservationAcquires + start.LiveReservations
            == ReservationReleases - start.ReservationReleases + LiveReservations;
}

/// <summary>
/// Bounded counters for one character's AI lifecycle. Normal decision ticks only
/// mutate fixed arrays; snapshots copy them only when an audit requests evidence.
/// </summary>
public readonly struct CharacterAiRuntimeDiagnosticsSnapshot
{
    private readonly long[] executionFailuresByKind;
    private readonly long[] candidateRejectionsByKind;
    private readonly long[] jobGiverEvaluationRejectionsByBranchAndKind;
    private readonly CharacterAiRuntimeTraceEvent[] recentTrace;
    private readonly CharacterAiRuntimeGateSnapshot gate;

    public CharacterAiRuntimeDiagnosticsSnapshot(
        long actionStarts, long actionSwitches, long sameActionRestarts,
        long phaseTransitions, long immediateReplans, long interruptedReplans,
        string lastInterruptedReplanDetail,
        long executionFailures, long noActionFailures, long candidateRejections,
        long duplicateExecutionSuppressions,
        long interactionActionReplacements,
        string lastInteractionActionReplacementDetail,
        long protectedRunningActionReplans,
        string lastProtectedRunningActionReplanDetail,
        long orphanWorkActionRecoveries,
        string lastOrphanWorkActionRecoveryDetail,
        long currentRepeatedFailureCount, long peakRepeatedFailureCount,
        AIActionFailureKind repeatedFailureKind,
        string lastExecutionFailureDetail,
        long[] executionFailuresByKind, long[] candidateRejectionsByKind,
        long jobGiverEvaluationRejections,
        long[] jobGiverEvaluationRejectionsByBranchAndKind,
        CharacterAiRuntimeTraceEvent[] recentTrace,
        CharacterAiRuntimeGateSnapshot gate = default)
    {
        ActionStarts = actionStarts;
        ActionSwitches = actionSwitches;
        SameActionRestarts = sameActionRestarts;
        PhaseTransitions = phaseTransitions;
        ImmediateReplans = immediateReplans;
        InterruptedReplans = interruptedReplans;
        LastInterruptedReplanDetail = lastInterruptedReplanDetail ?? string.Empty;
        ExecutionFailures = executionFailures;
        NoActionFailures = noActionFailures;
        CandidateRejections = candidateRejections;
        DuplicateExecutionSuppressions = duplicateExecutionSuppressions;
        InteractionActionReplacements = interactionActionReplacements;
        LastInteractionActionReplacementDetail =
            lastInteractionActionReplacementDetail ?? string.Empty;
        ProtectedRunningActionReplans = protectedRunningActionReplans;
        LastProtectedRunningActionReplanDetail =
            lastProtectedRunningActionReplanDetail ?? string.Empty;
        OrphanWorkActionRecoveries = orphanWorkActionRecoveries;
        LastOrphanWorkActionRecoveryDetail =
            lastOrphanWorkActionRecoveryDetail ?? string.Empty;
        CurrentRepeatedFailureCount = currentRepeatedFailureCount;
        PeakRepeatedFailureCount = peakRepeatedFailureCount;
        RepeatedFailureKind = repeatedFailureKind;
        LastExecutionFailureDetail = lastExecutionFailureDetail ?? string.Empty;
        this.executionFailuresByKind = executionFailuresByKind;
        this.candidateRejectionsByKind = candidateRejectionsByKind;
        JobGiverEvaluationRejections = jobGiverEvaluationRejections;
        this.jobGiverEvaluationRejectionsByBranchAndKind =
            jobGiverEvaluationRejectionsByBranchAndKind;
        this.recentTrace = recentTrace;
        this.gate = gate;
    }

    public long ActionStarts { get; }
    public long ActionSwitches { get; }
    public long SameActionRestarts { get; }
    public long PhaseTransitions { get; }
    public long ImmediateReplans { get; }
    public long InterruptedReplans { get; }
    public string LastInterruptedReplanDetail { get; }
    public long ExecutionFailures { get; }
    public long NoActionFailures { get; }
    public long CandidateRejections { get; }
    public long DuplicateExecutionSuppressions { get; }
    public long InteractionActionReplacements { get; }
    public string LastInteractionActionReplacementDetail { get; }
    public long ProtectedRunningActionReplans { get; }
    public string LastProtectedRunningActionReplanDetail { get; }
    public long OrphanWorkActionRecoveries { get; }
    public string LastOrphanWorkActionRecoveryDetail { get; }
    public long JobGiverEvaluationRejections { get; }
    public long CurrentRepeatedFailureCount { get; }
    public long PeakRepeatedFailureCount { get; }
    public AIActionFailureKind RepeatedFailureKind { get; }
    public string LastExecutionFailureDetail { get; }
    public CharacterAiRuntimeGateSnapshot Gate => gate;

    /// <summary>
    /// Resolves the typed terminal that closed a specific action epoch. The
    /// trace is already copied for this diagnostic snapshot, so querying it
    /// does not touch the live hot path or confuse a replacement action's
    /// terminal with the epoch under test.
    /// </summary>
    public bool TryGetActionTerminal(
        long actionEpoch,
        out CharacterAiActionTerminalKind terminalKind)
    {
        if (recentTrace != null)
        {
            for (int index = recentTrace.Length - 1; index >= 0; index--)
            {
                CharacterAiRuntimeTraceEvent traceEvent = recentTrace[index];
                if (traceEvent.ActionEpoch != actionEpoch
                    || traceEvent.Kind != CharacterAiRuntimeTraceKind.ActionTerminal)
                {
                    continue;
                }

                terminalKind = traceEvent.TerminalKind;
                return terminalKind != CharacterAiActionTerminalKind.None;
            }
        }

        terminalKind = CharacterAiActionTerminalKind.None;
        return false;
    }

    public string FormatRecentTrace()
    {
        if (recentTrace == null || recentTrace.Length == 0)
        {
            return "none";
        }

        StringBuilder builder = new StringBuilder(recentTrace.Length * 96);
        for (int index = 0; index < recentTrace.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(" | ");
            }

            recentTrace[index].AppendTo(builder);
        }

        return builder.ToString();
    }

    public string FormatDeltaFrom(in CharacterAiRuntimeDiagnosticsSnapshot start)
    {
        StringBuilder builder = new StringBuilder(256);
        builder.Append("starts=").Append(ActionStarts - start.ActionStarts)
            .Append("; switches=").Append(ActionSwitches - start.ActionSwitches)
            .Append("; restarts=").Append(SameActionRestarts - start.SameActionRestarts)
            .Append("; phases=").Append(PhaseTransitions - start.PhaseTransitions)
            .Append("; replans=").Append(ImmediateReplans - start.ImmediateReplans)
            .Append("; interruptedReplans=").Append(InterruptedReplans - start.InterruptedReplans)
            .Append("; executionFailures=").Append(ExecutionFailures - start.ExecutionFailures)
            .Append("; noAction=").Append(NoActionFailures - start.NoActionFailures)
            .Append("; candidateRejections=").Append(CandidateRejections - start.CandidateRejections)
            .Append("; duplicateExecutionSuppressions=")
            .Append(DuplicateExecutionSuppressions - start.DuplicateExecutionSuppressions)
            .Append("; interactionActionReplacements=")
            .Append(InteractionActionReplacements - start.InteractionActionReplacements)
            .Append("; protectedRunningActionReplans=")
            .Append(ProtectedRunningActionReplans - start.ProtectedRunningActionReplans)
            .Append("; orphanWorkActionRecoveries=")
            .Append(OrphanWorkActionRecoveries - start.OrphanWorkActionRecoveries)
            .Append("; jobGiverEvaluationRejections=")
            .Append(JobGiverEvaluationRejections - start.JobGiverEvaluationRejections)
            .Append("; repeatedPeak=").Append(PeakRepeatedFailureCount)
            .Append('(').Append(RepeatedFailureKind).Append(')')
            .Append("; terminals=").Append(gate.ActionTerminals - start.gate.ActionTerminals)
            .Append("; liveActions=").Append(gate.LiveActions)
            .Append("; progress=").Append(gate.ProgressRevision - start.gate.ProgressRevision)
            .Append("; gameplayProgress=")
            .Append(gate.GameplayProgressRevision - start.gate.GameplayProgressRevision)
            .Append("; queueHeartbeats=")
            .Append(gate.FacilityQueueHeartbeats - start.gate.FacilityQueueHeartbeats)
            .Append("; serviceHeartbeats=")
            .Append(gate.FacilityServiceHeartbeats - start.gate.FacilityServiceHeartbeats)
            .Append("; paths=").Append(gate.PathRequests - start.gate.PathRequests)
            .Append('/').Append(gate.PathResults - start.gate.PathResults)
            .Append('/').Append(gate.LivePathRequests)
            .Append("; reservations=")
            .Append(gate.ReservationAcquires - start.gate.ReservationAcquires)
            .Append('/').Append(gate.ReservationReleases - start.gate.ReservationReleases)
            .Append('/').Append(gate.LiveReservations)
            .Append("; retries=").Append(gate.RetrySchedules - start.gate.RetrySchedules)
            .Append('/').Append(gate.RetryAttempts - start.gate.RetryAttempts)
            .Append("; schedulerProcesses=")
            .Append(gate.SchedulerProcesses - start.gate.SchedulerProcesses)
            .Append("; schedulerOverdue=")
            .Append(gate.SchedulerOverdue - start.gate.SchedulerOverdue)
            .Append("; invariantAnomalies=")
            .Append(gate.InvariantAnomalies - start.gate.InvariantAnomalies)
            .Append("; failureLoops=")
            .Append(gate.FailureLoops - start.gate.FailureLoops);
        if (InterruptedReplans > start.InterruptedReplans)
        {
            builder.Append("; lastInterruptedReplan=")
                .Append(string.IsNullOrWhiteSpace(LastInterruptedReplanDetail)
                    ? "unknown"
                    : LastInterruptedReplanDetail);
        }
        if (ExecutionFailures > start.ExecutionFailures)
        {
            builder.Append("; lastExecutionFailure=")
                .Append(string.IsNullOrWhiteSpace(LastExecutionFailureDetail)
                    ? "unknown"
                    : LastExecutionFailureDetail);
        }
        if (InteractionActionReplacements > start.InteractionActionReplacements)
        {
            builder.Append("; lastInteractionActionReplacement=")
                .Append(string.IsNullOrWhiteSpace(LastInteractionActionReplacementDetail)
                    ? "unknown"
                    : LastInteractionActionReplacementDetail);
        }
        if (ProtectedRunningActionReplans > start.ProtectedRunningActionReplans)
        {
            builder.Append("; lastProtectedRunningActionReplan=")
                .Append(string.IsNullOrWhiteSpace(LastProtectedRunningActionReplanDetail)
                    ? "unknown"
                    : LastProtectedRunningActionReplanDetail);
        }
        if (OrphanWorkActionRecoveries > start.OrphanWorkActionRecoveries)
        {
            builder.Append("; lastOrphanWorkActionRecovery=")
                .Append(string.IsNullOrWhiteSpace(LastOrphanWorkActionRecoveryDetail)
                    ? "unknown"
                    : LastOrphanWorkActionRecoveryDetail);
        }
        AppendKinds(builder, "executionByKind", executionFailuresByKind, start.executionFailuresByKind);
        AppendKinds(builder, "candidateByKind", candidateRejectionsByKind, start.candidateRejectionsByKind);
        AppendJobGiverRejections(
            builder,
            jobGiverEvaluationRejectionsByBranchAndKind,
            start.jobGiverEvaluationRejectionsByBranchAndKind);
        return builder.ToString();
    }

    private static void AppendJobGiverRejections(
        StringBuilder builder,
        long[] end,
        long[] start)
    {
        bool wrote = false;
        int branchCount = Enum.GetValues(typeof(CharacterAiBranch)).Length;
        int failureKindCount = Enum.GetValues(typeof(AIActionFailureKind)).Length;
        for (int branchIndex = 1; branchIndex < branchCount; branchIndex++)
        {
            for (int failureIndex = 1;
                 failureIndex < failureKindCount;
                 failureIndex++)
            {
                int index = branchIndex * failureKindCount + failureIndex;
                long delta = At(end, index) - At(start, index);
                if (delta <= 0L)
                {
                    continue;
                }

                builder.Append(wrote ? ',' : ';');
                if (!wrote)
                {
                    builder.Append(" jobGiverByBranchKind=");
                }
                builder.Append((CharacterAiBranch)branchIndex)
                    .Append('/')
                    .Append((AIActionFailureKind)failureIndex)
                    .Append(':')
                    .Append(delta);
                wrote = true;
            }
        }

        if (!wrote)
        {
            builder.Append("; jobGiverByBranchKind=none");
        }
    }

    private static void AppendKinds(StringBuilder builder, string label, long[] end, long[] start)
    {
        bool wrote = false;
        int count = Enum.GetValues(typeof(AIActionFailureKind)).Length;
        for (int index = 1; index < count; index++)
        {
            long delta = At(end, index) - At(start, index);
            if (delta <= 0L) continue;
            builder.Append(wrote ? ',' : ';').Append(wrote ? string.Empty : " ").Append(wrote ? string.Empty : label + "=");
            builder.Append((AIActionFailureKind)index).Append(':').Append(delta);
            wrote = true;
        }
        if (!wrote) builder.Append("; ").Append(label).Append("=none");
    }

    private static long At(long[] values, int index) =>
        values != null && index >= 0 && index < values.Length ? values[index] : 0L;
}
