using System;
using static LifeExperiencePolicyFacts;

public sealed class CaptivityPerformerMilestoneUnlockedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public CaptivityPerformerMilestoneUnlockedExperiencePolicy()
        : base(CaptivePerformerMilestoneOutcomeIds.Unlocked.Value,
            "포로 공연자의 실제 명성 이정표 해금") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, CaptivePerformerMilestoneOutcomeIds.CaptiveRole.Value);
        int threshold = WholePositive(facts.Number(
            CaptivePerformerMilestoneOutcomeIds.ThresholdMetric.Value,
            CaptivePerformerMilestoneOutcomeIds.PointUnit.Value),
            "performer milestone threshold");
        string unlock = facts.Fact(CaptivePerformerMilestoneOutcomeIds.UnlockKindFact.Value);
        string expected = threshold switch
        {
            50 => "care-priority",
            75 => "staff-contract",
            100 => "final-contract-choice",
            _ => throw new GameplayExperienceEvidenceException("performer milestone threshold")
        };
        if (unlock != expected)
            GameplayExperienceFacts.Fail("performer milestone unlock kind");
        GameplayExperienceCategory category = threshold switch
        {
            50 => GameplayExperienceCategory.Mild,
            75 => GameplayExperienceCategory.Meaningful,
            100 => GameplayExperienceCategory.Intense,
            _ => throw new GameplayExperienceEvidenceException("performer milestone threshold")
        };
        return facts.Action(category,
            threshold == 50 ? "공연자 명성 50 이정표를 실제 해금해 가벼운 경험으로 평가함."
            : threshold == 75 ? "공연자 명성 75 이정표를 실제 해금해 의미 있는 경험으로 평가함."
            : "공연자 명성 100 최종 선택을 실제 해금해 강렬한 경험으로 평가함.");
    }
}

public sealed class CaptivityCaptiveEscapedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CaptivityCaptiveEscapedExperiencePolicy()
        : base(CaptiveEscapeOutcomeIds.Escaped.Value,
            "포로 신분에서 실제 이탈") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, CaptiveEscapeOutcomeIds.CaptiveRole.Value);
        string kind = facts.Fact(CaptiveEscapeOutcomeIds.KindFact.Value);
        CaptivityStatus previous = facts.EnumFact<CaptivityStatus>(
            CaptiveEscapeOutcomeIds.PreviousStatusFact.Value);
        if (!EscapeKindAndStatusAgree(kind, previous))
            GameplayExperienceFacts.Fail("captive escape kind/status");
        facts.Number(CaptiveEscapeOutcomeIds.OwnerRevisionMetric.Value,
            CaptiveEscapeOutcomeIds.RevisionUnit.Value);
        return facts.Action(GameplayExperienceCategory.Transformative,
            "포로 신분에서 실제 이탈하여 결정적 신분 전환 경험으로 평가함.");
    }

    private static bool EscapeKindAndStatusAgree(string kind, CaptivityStatus status) =>
        kind switch
        {
            "physical-escape" => status == CaptivityStatus.EscapeAttempt,
            "arrival-custody-loss" => status == CaptivityStatus.AwaitingCapture,
            "false-compliance-betrayal" => status is CaptivityStatus.Confined
                or CaptivityStatus.Labor or CaptivityStatus.Performer,
            "minion-control-break" => status == CaptivityStatus.Minion,
            _ => false
        };
}

public sealed class CaptivityCaptiveRansomedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CaptivityCaptiveRansomedExperiencePolicy()
        : base(CaptiveRansomOutcomeIds.Ransomed.Value,
            "몸값 지불로 실제 포로 신분 종료") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, CaptiveRansomOutcomeIds.CaptiveRole.Value);
        CaptivityStatus previous = facts.EnumFact<CaptivityStatus>(
            CaptiveRansomOutcomeIds.PreviousStatusFact.Value);
        if (!CaptiveRansomOutcomeIds.IsPreviousStatusValid(previous))
            GameplayExperienceFacts.Fail("captive ransom previous status");
        WholePositive(facts.Number(CaptiveRansomOutcomeIds.AmountMetric.Value,
            CaptiveRansomOutcomeIds.GoldUnit.Value), "captive ransom amount");
        return facts.Action(GameplayExperienceCategory.Transformative,
            "몸값 지불로 포로 신분이 실제 종료되어 결정적 신분 전환 경험으로 평가함.");
    }
}

public sealed class CaptivityInteractionResolvedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public CaptivityInteractionResolvedExperiencePolicy()
        : base(CaptivityInteractionOutcomeIds.Resolved.Value,
            "포로 상호작용의 실제 신체·심리 결과") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        bool captive = facts.HasRole(CaptivityInteractionOutcomeIds.CaptiveRole.Value);
        bool warden = facts.HasRole(CaptivityInteractionOutcomeIds.WardenRole.Value);
        bool facility = facts.HasRole(CaptivityInteractionOutcomeIds.FacilityRole.Value);
        if (!captive && !warden && !facility)
            GameplayExperienceFacts.Fail("captivity interaction subject role");
        double[] deltas =
        {
            facts.Number(CaptivityInteractionOutcomeIds.WillDeltaMetric.Value,
                CaptivityInteractionOutcomeIds.PointUnit.Value,
                CaptivityInteractionOutcomeIds.CaptiveRole.Value),
            facts.Number(CaptivityInteractionOutcomeIds.FearDeltaMetric.Value,
                CaptivityInteractionOutcomeIds.PointUnit.Value,
                CaptivityInteractionOutcomeIds.CaptiveRole.Value),
            facts.Number(CaptivityInteractionOutcomeIds.TrustDeltaMetric.Value,
                CaptivityInteractionOutcomeIds.PointUnit.Value,
                CaptivityInteractionOutcomeIds.CaptiveRole.Value),
            facts.Number(CaptivityInteractionOutcomeIds.GrudgeDeltaMetric.Value,
                CaptivityInteractionOutcomeIds.PointUnit.Value,
                CaptivityInteractionOutcomeIds.CaptiveRole.Value),
            facts.Number(CaptivityInteractionOutcomeIds.CorruptionDeltaMetric.Value,
                CaptivityInteractionOutcomeIds.PointUnit.Value,
                CaptivityInteractionOutcomeIds.CaptiveRole.Value)
        };
        double damage = facts.Number(CaptivityInteractionOutcomeIds.BodyDamageMetric.Value,
            CaptivityInteractionOutcomeIds.HealthUnit.Value,
            CaptivityInteractionOutcomeIds.CaptiveRole.Value);
        double healthBefore = facts.Number(
            CaptivityInteractionOutcomeIds.BodyHealthBeforeMetric.Value,
            CaptivityInteractionOutcomeIds.HealthUnit.Value,
            CaptivityInteractionOutcomeIds.CaptiveRole.Value);
        double healthAfter = facts.Number(
            CaptivityInteractionOutcomeIds.BodyHealthAfterMetric.Value,
            CaptivityInteractionOutcomeIds.HealthUnit.Value,
            CaptivityInteractionOutcomeIds.CaptiveRole.Value);
        bool applied = FactBoolean(facts.Fact(
            CaptivityInteractionOutcomeIds.SuccessFact.Value),
            "captivity interaction success");
        if (applied != facts.Success)
            GameplayExperienceFacts.Fail("captivity interaction status");
        if (!applied)
        {
            foreach (double delta in deltas)
            {
                if (delta != 0d)
                    GameplayExperienceFacts.Fail("failed captivity interaction delta");
            }
            if (damage != 0d || Math.Abs(healthBefore - healthAfter) > .001d)
                GameplayExperienceFacts.Fail("failed captivity interaction damage");
            return facts.Technical("captivity-interaction-no-applied-effect");
        }
        if (facility)
        {
            return facts.Judge(GameplayExperienceCategory.Routine,
                "captivity-interaction-facility",
                "시설은 상호작용 장소로만 참여해 일상 운영 사건으로 평가함.");
        }
        GameplayExperienceJudgment result;
        if (damage > 0d)
        {
            result = facts.Damage(healthBefore, healthAfter, damage,
                "포로 상호작용이 실제 신체 피해를 적용함.");
        }
        else
        {
            if (Math.Abs(healthBefore - healthAfter) > .001d)
                GameplayExperienceFacts.Fail("captivity interaction health delta");
            double magnitude = 0d;
            foreach (double delta in deltas)
                magnitude = Math.Max(magnitude, Math.Abs(delta));
            result = facts.Judge(
                magnitude >= 20d ? GameplayExperienceCategory.Meaningful
                    : magnitude > 0d ? GameplayExperienceCategory.Mild
                    : GameplayExperienceCategory.Routine,
                magnitude >= 20d ? "substantial-captivity-psychological-change"
                    : magnitude > 0d ? "captivity-psychological-change"
                    : "captivity-interaction-without-change",
                magnitude >= 20d
                    ? "심리 수치가 실제 20점 이상 변해 의미 있는 경험으로 평가함."
                    : magnitude > 0d
                        ? "심리 수치가 실제 변해 가벼운 경험으로 평가함."
                        : "실제 신체·심리 변화가 없어 일상 상호작용으로 평가함.");
        }
        if (!warden || result.Category <= GameplayExperienceCategory.Meaningful)
            return result;
        return facts.Judge(GameplayExperienceCategory.Meaningful,
            "warden-involved-in-consequential-interaction",
            "교도 담당자는 중대한 상호작용에 관여했지만 대상의 피해를 자신의 피해로 간주하지 않아 의미 있는 수준으로 제한함.");
    }
}

public sealed class CaptivityRehabilitationProgressedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public CaptivityRehabilitationProgressedExperiencePolicy()
        : base(CaptivityTransitionOutcomeIds.Rehabilitation.Value,
            "재활 작업의 실제 진척 또는 하루 완료") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        bool captive = facts.HasRole(CaptivityTransitionOutcomeIds.CaptiveRole.Value);
        bool warden = facts.HasRole(CaptivityTransitionOutcomeIds.WardenRole.Value);
        bool facility = facts.HasRole(CaptivityTransitionOutcomeIds.FacilityRole.Value);
        bool food = facts.HasRole(CaptivityTransitionOutcomeIds.FoodRole.Value);
        if (!captive && !warden && !facility && !food)
            GameplayExperienceFacts.Fail("rehabilitation subject role");
        bool completed = Boolean(facts.Number(CaptivityTransitionOutcomeIds.CompletedDay.Value,
            CaptivityTransitionOutcomeIds.BooleanUnit.Value,
            CaptivityTransitionOutcomeIds.CaptiveRole.Value), "rehabilitation completed day");
        Nonnegative(facts.Number(CaptivityTransitionOutcomeIds.ApprovedWork.Value,
            CaptivityTransitionOutcomeIds.WorkUnit.Value,
            CaptivityTransitionOutcomeIds.WardenRole.Value), "rehabilitation approved work");
        if (!facts.Success)
            return facts.Technical("rehabilitation-not-progressed");
        if (facility || food)
        {
            return facts.Judge(GameplayExperienceCategory.Routine,
                "rehabilitation-support-resource",
                "시설 또는 음식은 재활의 지원 자원으로 참여해 일상 운영 사건으로 평가함.");
        }
        return facts.Judge(completed ? GameplayExperienceCategory.Mild
                : GameplayExperienceCategory.Routine,
            completed ? "rehabilitation-day-completed" : "rehabilitation-work-progressed",
            completed ? "재활 하루가 실제 완료되어 가벼운 경험으로 평가함."
                : "재활 작업이 진척되었지만 하루 완료 전이므로 일상 경험으로 평가함.");
    }
}

public sealed class CaptivityReleaseResolvedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public CaptivityReleaseResolvedExperiencePolicy()
        : base(CaptivityTransitionOutcomeIds.Release.Value,
            "결정에 따른 실제 포로 석방") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        bool captive = facts.HasRole(CaptivityTransitionOutcomeIds.CaptiveRole.Value);
        bool decider = facts.HasRole(CaptivityTransitionOutcomeIds.DeciderRole.Value);
        bool facility = facts.HasRole(CaptivityTransitionOutcomeIds.FacilityRole.Value);
        if (!captive && !decider && !facility)
            GameplayExperienceFacts.Fail("captivity release subject role");
        CaptivityStatus before = facts.EnumFact<CaptivityStatus>(
            CaptivityTransitionOutcomeIds.StatusBefore.Value);
        CaptivityStatus after = facts.EnumFact<CaptivityStatus>(
            CaptivityTransitionOutcomeIds.StatusAfter.Value);
        bool custodyBefore = Boolean(facts.Number(CaptivityTransitionOutcomeIds.CustodyBefore.Value,
            CaptivityTransitionOutcomeIds.BooleanUnit.Value,
            CaptivityTransitionOutcomeIds.CaptiveRole.Value), "release custody before");
        bool custodyAfter = Boolean(facts.Number(CaptivityTransitionOutcomeIds.CustodyAfter.Value,
            CaptivityTransitionOutcomeIds.BooleanUnit.Value,
            CaptivityTransitionOutcomeIds.CaptiveRole.Value), "release custody after");
        if (before == CaptivityStatus.Released || after != CaptivityStatus.Released
            || !custodyBefore || custodyAfter)
        {
            GameplayExperienceFacts.Fail("captivity release transition");
        }
        if (!facts.Success)
            return facts.Technical("captivity-release-not-resolved");
        if (facility)
        {
            return facts.Judge(GameplayExperienceCategory.Routine,
                "release-facility-support",
                "시설은 석방의 지원 장소로 참여해 일상 운영 사건으로 평가함.");
        }
        if (decider)
        {
            return facts.Judge(GameplayExperienceCategory.Meaningful,
                "release-decision-made",
                "다른 인물의 실제 석방을 결정해 의미 있는 경험으로 평가함.");
        }
        return facts.Judge(GameplayExperienceCategory.Transformative,
            "captivity-ended-by-release",
            "포로 신분이 실제 석방으로 종료되어 결정적 신분 전환 경험으로 평가함.");
    }
}
