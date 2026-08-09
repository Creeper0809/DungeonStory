using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Player-facing projection for V23 work policies and quality pipelines.
/// Stable identifiers and raw enum names are appended only in Debug Mode.
/// </summary>
public static class GameplayUiPresentationText
{
    public static string Quality(CraftsmanshipQualityTier value) => value switch
    {
        CraftsmanshipQualityTier.Awful => "형편없음",
        CraftsmanshipQualityTier.Poor => "저급",
        CraftsmanshipQualityTier.Normal => "보통",
        CraftsmanshipQualityTier.Good => "양호",
        CraftsmanshipQualityTier.Excellent => "우수",
        CraftsmanshipQualityTier.Masterwork => "명품",
        CraftsmanshipQualityTier.Legendary => "전설",
        _ => "알 수 없음"
    };

    public static string WorkerPolicy(WorkerSelectionPolicySaveData source)
    {
        WorkerSelectionPolicySaveData policy = source?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        string subject = policy.mode switch
        {
            WorkerSelectionMode.SpecificCharacters => "지정 주민만",
            WorkerSelectionMode.SpecificOrRuleSet => "지정 주민 우선",
            WorkerSelectionMode.RuleSet => FormatRequirements(policy),
            _ => "작업 가능한 주민"
        };
        return $"{subject} · {SortMode(policy.sortMode)}";
    }

    public static string SortMode(WorkerCandidateSortMode value) => value switch
    {
        WorkerCandidateSortMode.BestExpectedQuality => "예상 품질 우선",
        WorkerCandidateSortMode.Fastest => "작업 속도 우선",
        WorkerCandidateSortMode.Nearest => "가까운 주민 우선",
        WorkerCandidateSortMode.LeastWorkload => "업무가 적은 주민 우선",
        WorkerCandidateSortMode.SpecificThenBestExpectedQuality =>
            "지정 주민, 이후 예상 품질 순",
        _ => "기본 우선순위"
    };

    public static string RejectedOutput(RejectedOutputDisposition value) => value switch
    {
        RejectedOutputDisposition.KeepInStorage => "불합격품 보관",
        RejectedOutputDisposition.MarkForSale => "불합격품 판매 대기",
        RejectedOutputDisposition.KeepFacilityAndStop => "현재 시설 유지 후 중단",
        RejectedOutputDisposition.DismantleFacilityAndRetry => "해체 후 다시 건설",
        _ => "불합격품 자동 분해"
    };

    public static string RepeatMode(
        QualityRepeatLimitMode value,
        int maximumAttempts = 0) => value switch
    {
        QualityRepeatLimitMode.UnlimitedUntilSuccess => "목표 품질까지 반복",
        _ when maximumAttempts > 0 => $"안전 한도 {maximumAttempts}회",
        _ => "안전 한도 적용"
    };

    public static string QualityStage(QualityTargetPipelineStage value) => value switch
    {
        QualityTargetPipelineStage.WaitingForMaterials => "재료 운반 대기",
        QualityTargetPipelineStage.WaitingForEligibleWorker => "조건에 맞는 작업자 대기",
        QualityTargetPipelineStage.Working => "제작 중",
        QualityTargetPipelineStage.ResolvingQuality => "품질 확인 중",
        QualityTargetPipelineStage.WaitingForOutputSpace => "출력 공간 대기",
        QualityTargetPipelineStage.Dismantling => "불합격품 해체 중",
        QualityTargetPipelineStage.Recovering => "회수품 정리 중",
        QualityTargetPipelineStage.Rebuilding => "다음 시도 준비 중",
        QualityTargetPipelineStage.TargetCurrentlyUnreachable => "현재 조건으로 목표 달성 불가",
        QualityTargetPipelineStage.Paused => "일시정지",
        QualityTargetPipelineStage.Completed => "목표 달성",
        QualityTargetPipelineStage.Cancelled => "취소됨",
        _ => "대기"
    };

    public static string WithDebug(
        string playerText,
        bool debugMode,
        string technicalText)
    {
        if (!debugMode || string.IsNullOrWhiteSpace(technicalText))
        {
            return playerText ?? string.Empty;
        }
        return $"{playerText}\n<color=#8291A8><size=80%>DEBUG · {technicalText}</size></color>";
    }

    public static string OrderCreated(string orderId, bool debugMode) =>
        WithDebug("주문을 대기열에 추가했습니다.", debugMode, $"order={orderId}");

    public static string FailureFallback(DomainFailure failure, bool debugMode)
    {
        string text = failure.Code switch
        {
            FailureCode.ApparelMaterialUnavailable => "사용할 수 있는 원단이 부족합니다.",
            FailureCode.ApparelFacilityUnavailable => "이 작업을 수행할 시설을 사용할 수 없습니다.",
            FailureCode.ApparelWorkOrderInvalid => "조건에 맞는 의복이나 작업 대상이 없습니다.",
            FailureCode.ApparelResearchLocked => "필요한 연구가 아직 완료되지 않았습니다.",
            FailureCode.ApparelItemReserved => "대상 의복이 다른 작업에 예약되어 있습니다.",
            FailureCode.WorkOrderWorkerIneligible => "조건에 맞는 작업자가 없습니다.",
            FailureCode.QualityTargetUnreachable => "현재 작업자와 시설로는 목표 품질에 도달할 수 없습니다.",
            FailureCode.ProductionMaterialsMissing => "필요한 재료가 부족합니다.",
            FailureCode.ProductionOutputSpaceUnavailable => "완성품을 둘 공간이 없습니다.",
            FailureCode.RequiredResearchUnavailable => "필요한 연구가 아직 완료되지 않았습니다.",
            _ => "지금은 이 작업을 시작할 수 없습니다. 요구 조건을 확인해 주세요."
        };
        return WithDebug(text, debugMode, failure.Code.ToString());
    }

    private static string FormatRequirements(WorkerSelectionPolicySaveData policy)
    {
        List<string> requirements = new();
        foreach (WorkerStatRequirementSaveData requirement in
                 policy.statRequirements ?? Enumerable.Empty<WorkerStatRequirementSaveData>())
        {
            if (requirement == null)
            {
                continue;
            }
            string stat = StatName(requirement.statType);
            requirements.Add($"{stat} {requirement.minimumValue}+" );
        }
        if (!string.IsNullOrWhiteSpace(policy.minimumSkillId))
        {
            requirements.Add($"관련 숙련 {policy.minimumSkillExperience}+" );
        }
        if (policy.minimumCareerRank > 0)
        {
            requirements.Add($"경력 등급 {policy.minimumCareerRank}+" );
        }
        if (requirements.Count == 0)
        {
            return "조건을 만족하는 주민";
        }
        string joiner = policy.matchMode == WorkerRequirementMatchMode.Any
            ? " 또는 "
            : " · ";
        return string.Join(joiner, requirements);
    }

    private static string StatName(int value) => value switch
    {
        0 => "전투",
        1 => "접객",
        2 => "연구",
        3 => "이동",
        4 => "힘",
        5 => "강인함",
        6 => "민첩",
        7 => "청소",
        8 => "지구력",
        9 => "사격",
        10 => "회피",
        11 => "의료",
        _ => "능력치"
    };
}
