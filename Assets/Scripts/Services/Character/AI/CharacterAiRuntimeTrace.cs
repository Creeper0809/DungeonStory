using System;
using System.Text;

public enum CharacterAiRuntimeTraceKind : byte
{
    None = 0,
    ActionStarted = 1,
    ActionTerminal = 2,
    PhaseChanged = 3,
    ExecutionFailure = 4,
    DeferredRetry = 5,
    LifecycleCleanup = 6,
    OrphanRecovery = 7,
    Progress = 8,
    MovementStarted = 9,
    MovementProgress = 10,
    MovementTerminal = 11,
    PathRequested = 12,
    PathResult = 13,
    PathRepath = 14,
    ReservationAcquired = 15,
    ReservationReleased = 16,
    ReservationFailed = 17,
    RetryScheduled = 18,
    RetryAttempted = 19,
    SchedulerOverdue = 20,
    InvariantAnomaly = 21
}

public enum CharacterAiRuntimePhase : byte
{
    None = 0,
    Selected = 1,
    Starting = 2,
    Moving = 3,
    Repathing = 4,
    Arrived = 5,
    FacilityAdmission = 6,
    FacilityQueue = 7,
    FacilityService = 8,
    Working = 9,
    Waiting = 10,
    Terminal = 11,
    External = 12,
    Unknown = 255
}

public enum CharacterAiActionTerminalKind : byte
{
    None = 0,
    Completed = 1,
    Failed = 2,
    Cancelled = 3,
    Recovered = 4
}

public enum CharacterAiPathTraceState : byte
{
    None = 0,
    Requested = 1,
    Found = 2,
    Deferred = 3,
    NoPath = 4,
    Invalidated = 5,
    Cancelled = 6
}

public enum CharacterAiReservationTraceState : byte
{
    None = 0,
    Acquired = 1,
    Refreshed = 2,
    Released = 3,
    Failed = 4
}

[Flags]
public enum CharacterAiRuntimeInvariant : ushort
{
    None = 0,
    LiveEpochWithoutAction = 1 << 0,
    RunningWithoutEpoch = 1 << 1,
    ExecutedWithoutActionOwner = 1 << 2,
    ReservationCounterMismatch = 1 << 3,
    PathCounterMismatch = 1 << 4,
    TerminalWithRuntimeOwner = 1 << 5,
    DecisionPendingWithoutSchedulerProgress = 1 << 6,
    BranchCounterMismatch = 1 << 7
}
/// <summary>
/// Allocation-free, fixed-width evidence captured on AI lifecycle transitions.
/// Human-readable strings are produced only when a diagnostic snapshot is requested.
/// </summary>
public readonly struct CharacterAiRuntimeTraceEvent
{
    public CharacterAiRuntimeTraceEvent(
        long sequence,
        long actionEpoch,
        float gameTime,
        CharacterAiRuntimeTraceKind kind,
        CharacterAiBranch branch,
        AIActionFailureKind failureKind,
        int actionDefinitionInstanceId,
        int destinationInstanceId,
        int phaseCode,
        long progressRevision = 0L,
        CharacterAiRuntimePhase phase = CharacterAiRuntimePhase.None,
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
        Sequence = sequence;
        ActionEpoch = actionEpoch;
        GameTime = gameTime;
        Kind = kind;
        Branch = branch;
        FailureKind = failureKind;
        ActionDefinitionInstanceId = actionDefinitionInstanceId;
        DestinationInstanceId = destinationInstanceId;
        PhaseCode = phaseCode;
        ProgressRevision = progressRevision;
        Phase = phase;
        TerminalKind = terminalKind;
        PathState = pathState;
        ReservationState = reservationState;
        Invariant = invariant;
        PathRequestId = pathRequestId;
        PathStepIndex = pathStepIndex;
        PathStepCount = pathStepCount;
        ReservationId = reservationId;
        RetryAttempt = retryAttempt;
        DelayMilliseconds = delayMilliseconds;
        ProgressMilli = progressMilli;
    }

    public long Sequence { get; }
    public long ActionEpoch { get; }
    public float GameTime { get; }
    public CharacterAiRuntimeTraceKind Kind { get; }
    public CharacterAiBranch Branch { get; }
    public AIActionFailureKind FailureKind { get; }
    public int ActionDefinitionInstanceId { get; }
    public int DestinationInstanceId { get; }
    public int PhaseCode { get; }
    public long ProgressRevision { get; }
    public CharacterAiRuntimePhase Phase { get; }
    public CharacterAiActionTerminalKind TerminalKind { get; }
    public CharacterAiPathTraceState PathState { get; }
    public CharacterAiReservationTraceState ReservationState { get; }
    public CharacterAiRuntimeInvariant Invariant { get; }
    public int PathRequestId { get; }
    public int PathStepIndex { get; }
    public int PathStepCount { get; }
    public int ReservationId { get; }
    public int RetryAttempt { get; }
    public int DelayMilliseconds { get; }
    public long ProgressMilli { get; }

    public void AppendTo(StringBuilder builder)
    {
        builder.Append('#').Append(Sequence)
            .Append("@t=").Append(GameTime.ToString("0.###"))
            .Append(" epoch=").Append(ActionEpoch)
            .Append(" kind=").Append(Kind)
            .Append(" branch=").Append(Branch)
            .Append(" action=").Append(ActionDefinitionInstanceId)
            .Append(" destination=").Append(DestinationInstanceId)
            .Append(" phase=").Append(Phase)
            .Append('/').Append(PhaseCode)
            .Append(" progress=").Append(ProgressRevision);
        if (FailureKind != AIActionFailureKind.None)
        {
            builder.Append(" failure=").Append(FailureKind);
        }
        if (TerminalKind != CharacterAiActionTerminalKind.None)
            builder.Append(" terminal=").Append(TerminalKind);
        if (PathState != CharacterAiPathTraceState.None)
            builder.Append(" path=").Append(PathRequestId).Append('/')
                .Append(PathState).Append('[').Append(PathStepIndex)
                .Append('/').Append(PathStepCount).Append(']');
        if (ReservationState != CharacterAiReservationTraceState.None)
            builder.Append(" reservation=").Append(ReservationId).Append('/')
                .Append(ReservationState);
        if (RetryAttempt > 0)
            builder.Append(" retry=").Append(RetryAttempt).Append('@')
                .Append(DelayMilliseconds).Append("ms");
        if (Invariant != CharacterAiRuntimeInvariant.None)
            builder.Append(" invariant=").Append(Invariant);
        if (ProgressMilli != 0L)
            builder.Append(" progressMilli=").Append(ProgressMilli);
    }
}
