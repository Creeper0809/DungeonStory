using System;
using System.Linq;
using DungeonStory.Operation;

public static class V20SocietyEventAlertProjection
{
    public static EventAlertImportance ToAlertImportance(
        ExperienceEventRiskTier riskTier) => riskTier switch
        {
            ExperienceEventRiskTier.None => EventAlertImportance.Low,
            ExperienceEventRiskTier.Recoverable => EventAlertImportance.Medium,
            ExperienceEventRiskTier.Serious => EventAlertImportance.High,
            ExperienceEventRiskTier.Lethal => EventAlertImportance.High,
            _ => throw new ArgumentOutOfRangeException(
                nameof(riskTier),
                riskTier,
                "Unknown society-event risk tier.")
        };

    public static string FormatRisk(
        ExperienceEventRiskTier riskTier,
        string riskReason)
    {
        SocietyEventRiskContract.RequireValid(
            riskTier,
            riskReason,
            "society-event-alert");
        string label = riskTier switch
        {
            ExperienceEventRiskTier.None => "불이익 없음",
            ExperienceEventRiskTier.Recoverable => "회복 가능한 손실",
            ExperienceEventRiskTier.Serious => "부상·질병·장기 손실",
            ExperienceEventRiskTier.Lethal => "실제 사망 가능",
            _ => throw new ArgumentOutOfRangeException(nameof(riskTier))
        };
        return $"예상 결과 위험: {label}\n위험 근거: {riskReason}";
    }

    public static string BuildTerminalResultSummary(
        V20ResolvedEventResult resolved)
    {
        if (!resolved.IsSocietyTerminal
            || string.IsNullOrWhiteSpace(resolved.TerminalActionLabel)
            || string.IsNullOrWhiteSpace(
                resolved.TerminalOutcomeNarrative))
        {
            throw new InvalidOperationException(
                "A society terminal result requires its frozen action and authored narrative.");
        }
        string effectTypes = resolved.TerminalEffectKinds.Count == 0
            ? "없음"
            : string.Join(", ", resolved.TerminalEffectKinds.Select(
                DescribeEffectKind));
        return $"처리: {resolved.TerminalActionLabel}\n"
            + $"해결 상태: {resolved.ResolutionId}\n"
            + $"작성 결과 서술: {resolved.TerminalOutcomeNarrative}\n"
            + $"적용 효과 유형: {effectTypes}";
    }

    public static EventAlertRequest BuildResolved(V20ResolvedEventResult resolved)
    {
        if (!resolved.IsSocietyTerminal)
            throw new ArgumentException("A society alert requires a terminal society result.", nameof(resolved));
        string risk = FormatRisk(resolved.RiskTier, resolved.RiskReason);
        string observedCause = string.IsNullOrWhiteSpace(resolved.ObservedCause)
            ? string.Empty : $"\n확인된 발생 사실: {resolved.ObservedCause}";
        return new EventAlertRequest(
            resolved.DisplayName,
            $"사회 사건 처리가 확정되었습니다.{observedCause}\n{risk}",
            ToAlertImportance(resolved.RiskTier),
            "V21 사회 사건",
            Array.Empty<EventAlertChoice>(),
            resolved.InstanceId,
            isResolved: true,
            resultSummary: BuildTerminalResultSummary(resolved));
    }

    private static string DescribeEffectKind(V20ContentEffectKind kind) =>
        kind switch
        {
            V20ContentEffectKind.Mood => "기분",
            V20ContentEffectKind.Trauma => "트라우마",
            V20ContentEffectKind.SkillExperience => "기술 경험치",
            V20ContentEffectKind.Health => "건강",
            V20ContentEffectKind.Relationship => "관계",
            V20ContentEffectKind.FactionRapport => "세력 우호",
            V20ContentEffectKind.FactionGrievance => "세력 불만",
            V20ContentEffectKind.FactionObligation => "세력 의무",
            V20ContentEffectKind.Money => "자금",
            V20ContentEffectKind.ItemGrant => "물품 획득",
            V20ContentEffectKind.ItemConsume => "물품 소비",
            V20ContentEffectKind.WorldFlag => "세계 상태",
            V20ContentEffectKind.WorkDelayDays => "작업 지연",
            V20ContentEffectKind.Threat => "위협",
            V20ContentEffectKind.DiseaseExposure => "질병 노출",
            V20ContentEffectKind.AmbitionProgress => "야망 진척",
            V20ContentEffectKind.MilestonePressure => "이정표 압박",
            V20ContentEffectKind.RetirementSchedule => "은퇴 일정",
            _ => throw new InvalidOperationException(
                $"Unknown committed society effect type '{kind}'.")
        };
}
