public readonly struct WorkTargetCandidate
{
    public WorkTargetCandidate(
        BuildableObject building,
        WorkTypeDefinition definition,
        WorkPriorityLevel priority,
        float score,
        float urgencyScore,
        string failureReason,
        AIActionFailureKind failureKind = AIActionFailureKind.None,
        string breakdownSummary = "")
    {
        Building = building;
        WorkType = definition != null ? definition.Type : FacilityWorkType.None;
        WorkTypeId = definition != null ? definition.WorkTypeId : default;
        DisplayName = definition != null ? definition.DisplayName : string.Empty;
        Priority = priority;
        Score = score;
        UrgencyScore = urgencyScore;
        FailureReason = failureReason;
        FailureKind = failureKind;
        BreakdownSummary = breakdownSummary ?? string.Empty;
        IsValid = building != null && WorkTypeId.IsValid && priority != WorkPriorityLevel.Off;
    }

    public BuildableObject Building { get; }
    internal FacilityWorkType WorkType { get; }
    public WorkTypeId WorkTypeId { get; }
    public string DisplayName { get; }
    public WorkPriorityLevel Priority { get; }
    public float Score { get; }
    public float UrgencyScore { get; }
    public string FailureReason { get; }
    public AIActionFailureKind FailureKind { get; }
    public string BreakdownSummary { get; }
    public bool IsValid { get; }

    public static WorkTargetCandidate Invalid(
        BuildableObject building,
        string failureReason,
        AIActionFailureKind failureKind = AIActionFailureKind.Unknown)
    {
        return new WorkTargetCandidate(
            building,
            null,
            WorkPriorityLevel.Off,
            float.NegativeInfinity,
            0f,
            failureReason,
            failureKind);
    }
}
