using System;

public enum CharacterAiRetryKind
{
    None = 0,
    CandidateEvaluation,
    PathSearch,
    FailureCooldown,
    DecisionRetry,
    ExecutorDeferred
}

public readonly struct CharacterAiDecisionScheduleObservation
{
    public CharacterAiDecisionScheduleObservation(
        bool isScheduled,
        float dueTime,
        float observedAt,
        CharacterAiRetryKind retryKind)
    {
        IsScheduled = isScheduled;
        DueTime = dueTime;
        RemainingSeconds = isScheduled
            ? Math.Max(0f, dueTime - observedAt)
            : 0f;
        RetryKind = isScheduled ? retryKind : CharacterAiRetryKind.None;
    }

    public bool IsScheduled { get; }
    public float DueTime { get; }
    public float RemainingSeconds { get; }
    public CharacterAiRetryKind RetryKind { get; }
    public bool IsRetry => IsScheduled && RetryKind != CharacterAiRetryKind.None;
    public static CharacterAiDecisionScheduleObservation Unscheduled => default;
}

