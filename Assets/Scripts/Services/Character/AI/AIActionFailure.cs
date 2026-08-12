using UnityEngine;

public readonly struct AIActionFailure
{
    public AIActionFailure(AIActionFailureKind kind, string reason, BuildableObject target = null)
    {
        Kind = kind;
        Reason = reason ?? string.Empty;
        Target = target;
    }

    public AIActionFailureKind Kind { get; }
    public string Reason { get; }
    public BuildableObject Target { get; }
    public bool HasFailure => Kind != AIActionFailureKind.None;
    public bool IsDeferred => IsDeferredKind(Kind);

    public static AIActionFailure None => new AIActionFailure(AIActionFailureKind.None, string.Empty);

    public static AIActionFailure Create(
        AIActionFailureKind kind,
        string reason = null,
        BuildableObject target = null)
    {
        return new AIActionFailure(kind, reason ?? GetDefaultReason(kind), target);
    }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Reason) ? GetDefaultReason(Kind) : Reason;
    }

    public static bool IsDeferredKind(AIActionFailureKind kind)
    {
        return kind == AIActionFailureKind.CandidateEvaluationDeferred
            || kind == AIActionFailureKind.FacilityCandidateDeferred
            || kind == AIActionFailureKind.PathSearchDeferred;
    }

    private static string GetDefaultReason(AIActionFailureKind kind)
    {
        return kind switch
        {
            AIActionFailureKind.None => string.Empty,
            AIActionFailureKind.NoAction => "행동 없음",
            AIActionFailureKind.Cooldown => "쿨다운",
            AIActionFailureKind.CandidateEvaluationDeferred => "행동 후보 평가 지연",
            AIActionFailureKind.FacilityCandidateDeferred => "시설 후보 인덱스 지연",
            AIActionFailureKind.PathSearchDeferred => "경로 탐색 지연",
            AIActionFailureKind.CannotStart => "시작 조건 불만족",
            AIActionFailureKind.NoScore => "점수 없음",
            AIActionFailureKind.NoDestination => "목적지 없음",
            AIActionFailureKind.DestinationSelectionFailed => "목적지 선택 실패",
            AIActionFailureKind.DestinationOccupied => "목적지 선점됨",
            AIActionFailureKind.NoPath => "경로 없음",
            AIActionFailureKind.NoGrid => "AI 또는 그리드 없음",
            AIActionFailureKind.NoWork => "작업 없음",
            AIActionFailureKind.OffDuty => "비번",
            AIActionFailureKind.Unsupported => "지원하지 않음",
            AIActionFailureKind.Destroyed => "시설 파괴됨",
            _ => "알 수 없음"
        };
    }
}

public static class FacilityAssignmentFailureExtensions
{
    public static AIActionFailureKind ToAiActionFailureKind(
        this FacilityAssignmentFailureKind failureKind)
    {
        return failureKind switch
        {
            FacilityAssignmentFailureKind.None => AIActionFailureKind.None,
            FacilityAssignmentFailureKind.MissingWorker => AIActionFailureKind.CannotStart,
            FacilityAssignmentFailureKind.Destroyed => AIActionFailureKind.Destroyed,
            FacilityAssignmentFailureKind.UnsupportedWork => AIActionFailureKind.Unsupported,
            FacilityAssignmentFailureKind.WorkNotNeeded => AIActionFailureKind.NoWork,
            FacilityAssignmentFailureKind.Damaged => AIActionFailureKind.NoWork,
            FacilityAssignmentFailureKind.Occupied => AIActionFailureKind.DestinationOccupied,
            FacilityAssignmentFailureKind.Reserved => AIActionFailureKind.DestinationOccupied,
            _ => AIActionFailureKind.Unknown
        };
    }
}

public readonly struct AIActionDebugCandidate
{
    public AIActionDebugCandidate(
        string actionLabel,
        float score,
        AIActionFailure failure,
        BuildableObject destination)
    {
        ActionLabel = actionLabel ?? string.Empty;
        Score = Mathf.Clamp01(score);
        Failure = failure;
        Destination = destination;
    }

    public string ActionLabel { get; }
    public float Score { get; }
    public AIActionFailure Failure { get; }
    public BuildableObject Destination { get; }
}
