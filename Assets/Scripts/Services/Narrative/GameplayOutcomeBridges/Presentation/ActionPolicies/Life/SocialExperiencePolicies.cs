using System;
using static LifeExperiencePolicyFacts;

public sealed class SocialConflictResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public SocialConflictResolvedExperiencePolicy()
        : base(SocialLifeOutcomeIds.ConflictResolved.Value,
            "갈등이 만든 실제 기분 변화") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        bool venue = facts.HasRole(SocialLifeOutcomeIds.VenueRole.Value);
        bool target = facts.HasRole(SocialLifeOutcomeIds.TargetRole.Value);
        bool instigator = facts.HasRole(SocialLifeOutcomeIds.InstigatorRole.Value);
        bool customer = facts.HasRole(SocialLifeOutcomeIds.CustomerRole.Value);
        if (!venue && !target && !instigator && !customer)
            GameplayExperienceFacts.Fail("conflict subject role");
        double mood = facts.Number(SocialLifeOutcomeIds.MoodDeltaMetric.Value,
            SocialLifeOutcomeIds.PointUnit.Value, SocialLifeOutcomeIds.TargetRole.Value);
        if (!facts.Success)
            return facts.Technical("conflict-not-resolved");
        if (venue || (customer && !target && !instigator))
        {
            return facts.Judge(GameplayExperienceCategory.Routine, "conflict-context-only",
                "시설 또는 고객 맥락으로만 참여해 일상 운영 사건으로 평가함.");
        }
        if (target)
            return SocialExperienceJudgment.Delta(facts, Math.Abs(mood),
                "갈등 대상의 실제 기분 변화");
        return SocialExperienceJudgment.ActionDelta(facts, Math.Abs(mood), "갈등 유발 행동");
    }
}

public sealed class SocialApologyResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public SocialApologyResolvedExperiencePolicy()
        : base(SocialLifeOutcomeIds.ApologyResolved.Value,
            "사과가 만든 실제 기분 변화") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        bool offender = facts.HasRole(SocialLifeOutcomeIds.OffenderRole.Value);
        bool recipient = facts.HasRole(SocialLifeOutcomeIds.RecipientRole.Value);
        if (!offender && !recipient)
            GameplayExperienceFacts.Fail("apology subject role");
        double mood = facts.Number(SocialLifeOutcomeIds.MoodDeltaMetric.Value,
            SocialLifeOutcomeIds.PointUnit.Value, SocialLifeOutcomeIds.RecipientRole.Value);
        if (!facts.Success)
            return facts.Technical("apology-not-resolved");
        return recipient
            ? SocialExperienceJudgment.Delta(facts, Math.Abs(mood),
                "사과 수신자의 실제 기분 변화")
            : SocialExperienceJudgment.ActionDelta(facts, Math.Abs(mood), "사과 행동");
    }
}

public sealed class SocialIdleInteractionResolvedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public SocialIdleInteractionResolvedExperiencePolicy()
        : base(IdleSocialInteractionOutcomeIds.OutcomeType.Value,
            "일상 교류의 실제 관계·기분 변화") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        bool actor = facts.HasRole(IdleSocialInteractionOutcomeIds.ActorRole.Value);
        bool target = facts.HasRole(IdleSocialInteractionOutcomeIds.TargetRole.Value);
        if (!actor && !target)
            GameplayExperienceFacts.Fail("idle interaction subject role");
        double magnitude;
        if (actor)
        {
            double before = facts.Number(
                IdleSocialInteractionOutcomeIds.ActorRelationshipBefore.Value,
                IdleSocialInteractionOutcomeIds.PointUnit.Value);
            double after = facts.Number(
                IdleSocialInteractionOutcomeIds.ActorRelationshipAfter.Value,
                IdleSocialInteractionOutcomeIds.PointUnit.Value);
            magnitude = Math.Abs(after - before);
        }
        else
        {
            double relationBefore = facts.Number(
                IdleSocialInteractionOutcomeIds.TargetRelationshipBefore.Value,
                IdleSocialInteractionOutcomeIds.PointUnit.Value);
            double relationAfter = facts.Number(
                IdleSocialInteractionOutcomeIds.TargetRelationshipAfter.Value,
                IdleSocialInteractionOutcomeIds.PointUnit.Value);
            double moodBefore = facts.Number(IdleSocialInteractionOutcomeIds.MoodBefore.Value,
                IdleSocialInteractionOutcomeIds.PointUnit.Value);
            double moodAfter = facts.Number(IdleSocialInteractionOutcomeIds.MoodAfter.Value,
                IdleSocialInteractionOutcomeIds.PointUnit.Value);
            magnitude = Math.Max(Math.Abs(relationAfter - relationBefore),
                Math.Abs(moodAfter - moodBefore));
        }
        return facts.Success
            ? SocialExperienceJudgment.Delta(facts, magnitude,
                "일상 교류의 실제 관계·기분 변화")
            : facts.Technical("idle-interaction-not-resolved");
    }
}

public sealed class SocialStaffRebellionResponseResolvedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public SocialStaffRebellionResponseResolvedExperiencePolicy()
        : base(StaffDiscontentOutcomeIds.ResponseResolved.Value,
            "직원 반발에 대한 실제 대응") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        bool staff = facts.HasRole(StaffDiscontentOutcomeIds.StaffRole.Value);
        bool responder = facts.HasRole(StaffDiscontentOutcomeIds.ResponderRole.Value);
        if (!staff && !responder)
            GameplayExperienceFacts.Fail("staff response subject role");
        StaffRebellionResponseType response = EnumNumber<StaffRebellionResponseType>(facts.Number(
            StaffDiscontentOutcomeIds.ResponseTypeMetric.Value,
            StaffDiscontentOutcomeIds.EnumUnit.Value, anyReference: true),
            "staff response type");
        if (!facts.Success)
            return facts.Technical("staff-response-not-resolved");
        if (responder)
        {
            return facts.Judge(GameplayExperienceCategory.Mild, "staff-response-performed",
                "직원 반발 대응을 수행한 당사자에게 가벼운 경험으로 평가함.");
        }
        return response switch
        {
            StaffRebellionResponseType.SuppressCommand or StaffRebellionResponseType.Isolate =>
                facts.Judge(GameplayExperienceCategory.Intense, "coercive-staff-response",
                    "강제 억압 또는 격리가 실제 적용되어 직원에게 강렬한 경험으로 평가함."),
            StaffRebellionResponseType.Calm =>
                facts.Judge(GameplayExperienceCategory.Mild, "calming-staff-response",
                    "진정 대응이 실제 적용되어 직원에게 가벼운 경험으로 평가함."),
            _ => throw new GameplayExperienceEvidenceException("unsupported staff response")
        };
    }
}

public sealed class SocialStaffDiscontentTransitionedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public SocialStaffDiscontentTransitionedExperiencePolicy()
        : base(StaffDiscontentTransitionOutcomeIds.Transitioned.Value,
            "직원 불만의 실제 단계 결과") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, StaffDiscontentTransitionOutcomeIds.StaffRole.Value);
        StaffDiscontentOutcome outcome = EnumNumber<StaffDiscontentOutcome>(facts.Number(
            StaffDiscontentTransitionOutcomeIds.OutcomeMetric.Value,
            StaffDiscontentTransitionOutcomeIds.EnumUnit.Value, anyReference: true),
            "staff discontent outcome");
        if (!facts.Success)
            return facts.Technical("staff-discontent-transition-not-committed");
        return outcome switch
        {
            StaffDiscontentOutcome.Warning =>
                facts.Judge(GameplayExperienceCategory.Mild, "staff-discontent-warning",
                    "직원 불만 경고가 실제 발생해 가벼운 경험으로 평가함."),
            StaffDiscontentOutcome.EfficiencyPenalty or StaffDiscontentOutcome.WorkDisruption =>
                facts.Judge(GameplayExperienceCategory.Meaningful, "staff-work-impact",
                    "직원 불만이 실제 효율 저하 또는 작업 방해로 이어져 의미 있는 경험으로 평가함."),
            StaffDiscontentOutcome.PermanentDeparture =>
                facts.Judge(GameplayExperienceCategory.Intense, "staff-permanent-departure",
                    "직원의 영구 이탈이 실제 발생해 강렬한 경험으로 평가함."),
            StaffDiscontentOutcome.LocalRebellion or StaffDiscontentOutcome.OwnerThreat =>
                facts.Judge(GameplayExperienceCategory.Intense, "staff-rebellion-or-threat",
                    "지역 반란 또는 소유자 위협이 실제 발생해 강렬한 경험으로 평가함."),
            _ => throw new GameplayExperienceEvidenceException(
                "unsupported staff discontent outcome")
        };
    }
}

internal static class SocialExperienceJudgment
{
    internal static GameplayExperienceJudgment Delta(GameplayExperienceFacts facts,
        double magnitude, string description)
    {
        GameplayExperienceCategory category = magnitude == 0d
            ? GameplayExperienceCategory.Routine
            : magnitude < 10d ? GameplayExperienceCategory.Mild
            : GameplayExperienceCategory.Meaningful;
        return facts.Judge(category,
            category == GameplayExperienceCategory.Routine ? "no-recorded-social-change"
            : category == GameplayExperienceCategory.Mild ? "minor-recorded-social-change"
            : "substantial-recorded-social-change",
            description + (category == GameplayExperienceCategory.Routine
                ? "가 0점이어서 일상 사건으로 평가함."
                : category == GameplayExperienceCategory.Mild
                    ? "가 10점 미만이어서 가벼운 경험으로 평가함."
                    : "가 10점 이상이어서 의미 있는 경험으로 평가함. 관계 파탄은 추가 추정하지 않음."));
    }

    internal static GameplayExperienceJudgment ActionDelta(GameplayExperienceFacts facts,
        double magnitude, string description) => facts.Judge(
        magnitude == 0d ? GameplayExperienceCategory.Routine : GameplayExperienceCategory.Mild,
        magnitude == 0d ? "social-action-without-change" : "social-action-with-recorded-effect",
        description + (magnitude == 0d
            ? "은 확인되었지만 대상의 수치 변화가 없어 일상 경험으로 평가함."
            : "이 대상에게 실제 변화를 만들었으나 행위자의 감정 피해로 전이하지 않아 가벼운 경험으로 제한함."));
}
