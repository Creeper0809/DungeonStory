/// <summary>
/// Read-only projection of an execution result already observed by the action
/// owner. It is deliberately not a command/check API: UI consumers may render
/// it without evaluating an action, reserving anything, or requesting a path.
/// </summary>
public readonly struct CharacterOperationBlockSnapshot
{
    public CharacterOperationBlockSnapshot(
        string actionStableId,
        string actionLabel,
        string targetStableId,
        string targetLabel,
        long actionEpoch,
        AIActionFailure failure,
        DomainFailure domainFailure,
        CharacterOperationBlockAxis axis,
        CharacterAiRetryKind retryKind,
        bool isCurrent,
        bool hasKnownExpiry,
        float expiresAt,
        float observedAt = 0f,
        bool isCurrentnessConfirmed = true)
    {
        ActionStableId = actionStableId ?? string.Empty;
        ActionLabel = actionLabel ?? string.Empty;
        TargetStableId = targetStableId ?? string.Empty;
        TargetLabel = targetLabel ?? string.Empty;
        ActionEpoch = actionEpoch;
        Failure = failure;
        DomainFailure = domainFailure;
        Axis = axis;
        RetryKind = retryKind;
        IsCurrent = isCurrent;
        HasKnownExpiry = hasKnownExpiry;
        ExpiresAt = expiresAt;
        ObservedAt = observedAt;
        IsCurrentnessConfirmed = isCurrentnessConfirmed;
    }

    public string ActionStableId { get; }
    public string ActionLabel { get; }
    public string TargetStableId { get; }
    public string TargetLabel { get; }
    public long ActionEpoch { get; }
    public AIActionFailure Failure { get; }
    public DomainFailure DomainFailure { get; }
    public CharacterOperationBlockAxis Axis { get; }
    public CharacterAiRetryKind RetryKind { get; }
    public bool IsCurrent { get; }
    public bool HasKnownExpiry { get; }
    public float ExpiresAt { get; }
    public float ObservedAt { get; }
    public bool IsCurrentnessConfirmed { get; }
    public bool HasTarget => !string.IsNullOrWhiteSpace(TargetStableId);
    public bool HasObservation =>
        ActionEpoch > 0L && Failure.HasFailure && HasTarget;
    public bool HasCurrentBlock =>
        IsCurrent && IsCurrentnessConfirmed && HasObservation;

    internal CharacterOperationBlockSnapshot WithCooldownExpiry(float expiresAt) =>
        new CharacterOperationBlockSnapshot(
            ActionStableId,
            ActionLabel,
            TargetStableId,
            TargetLabel,
            ActionEpoch,
            Failure,
            DomainFailure,
            Axis,
            RetryKind,
            IsCurrent,
            hasKnownExpiry: true,
            expiresAt: expiresAt,
            observedAt: ObservedAt,
            isCurrentnessConfirmed: IsCurrentnessConfirmed);

    internal CharacterOperationBlockSnapshot WithCurrentnessUnconfirmed() =>
        new CharacterOperationBlockSnapshot(
            ActionStableId,
            ActionLabel,
            TargetStableId,
            TargetLabel,
            ActionEpoch,
            Failure,
            DomainFailure,
            Axis,
            RetryKind,
            IsCurrent,
            HasKnownExpiry,
            ExpiresAt,
            ObservedAt,
            isCurrentnessConfirmed: false);

    internal CharacterOperationBlockSnapshot AsHistory() =>
        new CharacterOperationBlockSnapshot(
            ActionStableId,
            ActionLabel,
            TargetStableId,
            TargetLabel,
            ActionEpoch,
            Failure,
            DomainFailure,
            Axis,
            RetryKind,
            isCurrent: false,
            hasKnownExpiry: false,
            expiresAt: 0f,
            observedAt: ObservedAt,
            isCurrentnessConfirmed: false);
}
