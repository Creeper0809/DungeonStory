using System;
using static LifeExperiencePolicyFacts;

public sealed class CharacterAmbitionTransitionedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public CharacterAmbitionTransitionedExperiencePolicy()
        : base(CharacterAmbitionTransitionOutcomeIds.Transitioned.Value,
            "야망의 실제 시작 또는 실패 전환") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, CharacterAmbitionTransitionOutcomeIds.OwnerRole.Value);
        CharacterAmbitionStatus before = facts.EnumFact<CharacterAmbitionStatus>(
            CharacterAmbitionTransitionOutcomeIds.BeforeStatus.Value);
        CharacterAmbitionStatus after = facts.EnumFact<CharacterAmbitionStatus>(
            CharacterAmbitionTransitionOutcomeIds.AfterStatus.Value);
        string reason = facts.Fact(CharacterAmbitionTransitionOutcomeIds.Reason.Value);
        facts.Number(CharacterAmbitionTransitionOutcomeIds.ProgressBefore.Value,
            CharacterAmbitionTransitionOutcomeIds.ProgressUnit.Value);
        facts.Number(CharacterAmbitionTransitionOutcomeIds.ProgressAfter.Value,
            CharacterAmbitionTransitionOutcomeIds.ProgressUnit.Value);
        if (!facts.Success)
            return facts.Technical("ambition-transition-not-completed");
        if (before != CharacterAmbitionStatus.Active && after == CharacterAmbitionStatus.Active
            && reason == CharacterAmbitionTransitionOutcomeIds.ActivationReason)
        {
            return facts.Judge(GameplayExperienceCategory.Mild, "ambition-started",
                "야망이 실제 시작되어 가벼운 목표 전환 경험으로 평가함.");
        }
        if (before == CharacterAmbitionStatus.Active && after == CharacterAmbitionStatus.Failed
            && (reason == CharacterAmbitionTransitionOutcomeIds.FailureReason
                || reason == CharacterAmbitionTransitionOutcomeIds.DeadlineReason))
        {
            return facts.Judge(GameplayExperienceCategory.Meaningful, "ambition-failed",
                "야망의 실제 실패를 의미 있는 좌절로 평가하되 삶 전체의 파탄은 추정하지 않음.");
        }
        GameplayExperienceFacts.Fail("unsupported ambition transition");
        return default;
    }
}

public sealed class CharacterAgeConditionAnatomyAppliedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public CharacterAgeConditionAnatomyAppliedExperiencePolicy()
        : base(AgeConditionAnatomyOutcomeIds.Applied.Value,
            "노화 상태가 신체에 적용한 실제 체력 손실") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, AgeConditionAnatomyOutcomeIds.PatientRole.Value);
        facts.Fact(AgeConditionAnatomyOutcomeIds.ConditionId.Value);
        double before = facts.Number(AgeConditionAnatomyOutcomeIds.HealthBefore.Value,
            AgeConditionAnatomyOutcomeIds.HealthUnit.Value);
        double after = facts.Number(AgeConditionAnatomyOutcomeIds.HealthAfter.Value,
            AgeConditionAnatomyOutcomeIds.HealthUnit.Value);
        double damage = facts.Number(AgeConditionAnatomyOutcomeIds.DamageApplied.Value,
            AgeConditionAnatomyOutcomeIds.HealthUnit.Value);
        WholePositive(facts.Number(AgeConditionAnatomyOutcomeIds.NodeCount.Value,
            AgeConditionAnatomyOutcomeIds.CountUnit.Value), "age-condition node count");
        if (!facts.Success)
            return facts.Technical("age-condition-not-applied");
        return facts.Damage(before, after, damage,
            damage == 0d ? "노화 상태 적용은 확인되지만 체력 손실은 없음."
                : "노화 상태가 실제 신체 피해를 적용함.");
    }
}

public sealed class CharacterReproductionProcessTransitionedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public CharacterReproductionProcessTransitionedExperiencePolicy()
        : base(ReproductionProcessTransitionOutcomeIds.Transitioned.Value,
            "생식 과정의 실제 상태 전환을 참여 역할별로 평가") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        bool facility = facts.HasRole(ReproductionProcessTransitionOutcomeIds.FacilityRole.Value);
        bool responder = facts.HasRole(
            ReproductionProcessTransitionOutcomeIds.MedicalResponderRole.Value);
        bool carrier = facts.HasRole(ReproductionProcessTransitionOutcomeIds.CarrierRole.Value);
        bool offspring = facts.HasRole(ReproductionProcessTransitionOutcomeIds.OffspringRole.Value);
        bool parent = facts.HasRole(ReproductionProcessTransitionOutcomeIds.GeneticParentRole.Value);
        if (!facility && !responder && !carrier && !offspring && !parent)
            GameplayExperienceFacts.Fail("reproduction subject role");
        string transition = facts.Fact(ReproductionProcessTransitionOutcomeIds.TransitionKind.Value);
        string before = ReproductionStatus(facts.Fact(
            ReproductionProcessTransitionOutcomeIds.StatusBefore.Value));
        string after = ReproductionStatus(facts.Fact(
            ReproductionProcessTransitionOutcomeIds.StatusAfter.Value));
        string failureBefore = ReproductionFailure(facts.Fact(
            ReproductionProcessTransitionOutcomeIds.FailureBefore.Value));
        string failure = ReproductionFailure(facts.Fact(
            ReproductionProcessTransitionOutcomeIds.FailureAfter.Value));
        facts.Fact(ReproductionProcessTransitionOutcomeIds.Mode.Value);
        ConsecutiveRevisions(
            facts.Number(ReproductionProcessTransitionOutcomeIds.RevisionBefore.Value,
                ReproductionProcessTransitionOutcomeIds.RevisionUnit.Value, anyReference: true),
            facts.Number(ReproductionProcessTransitionOutcomeIds.RevisionAfter.Value,
                ReproductionProcessTransitionOutcomeIds.RevisionUnit.Value, anyReference: true),
            "reproduction revision");
        GameplayOutcomeStatus expectedStatus = after == "failed"
            ? GameplayOutcomeStatus.Failed
            : after.StartsWith("waiting-", StringComparison.Ordinal)
                ? GameplayOutcomeStatus.PartiallySucceeded
                : GameplayOutcomeStatus.Succeeded;
        if (facts.Binding.status != (int)expectedStatus
            || !ValidReproductionTransition(
                transition, before, after, failureBefore, failure))
        {
            GameplayExperienceFacts.Fail("reproduction transition/status");
        }
        if (facility)
        {
            return facts.Judge(GameplayExperienceCategory.Routine,
                "reproduction-facility-support",
                "시설은 생식 과정의 지원 장소로 참여했으므로 일상 운영 사건으로 평가함.");
        }
        if (transition == "planned" || transition == "started")
        {
            return facts.Judge(GameplayExperienceCategory.Mild, "reproduction-process-began",
                "생식 과정의 계획 또는 시작이 실제 확정되어 가벼운 삶의 전환으로 평가함.");
        }
        if (transition == "birth-published" && after == "completed")
        {
            return ReproductionMajor(facts, responder,
                "출생 또는 활성화가 실제 공표되어 결정적 삶의 변화로 평가함.");
        }
        if (transition == "carrier-death")
        {
            bool terminalDeath = after == "failed" && failure == "carrier-died";
            bool extractionCrisis = after == "waiting-emergency-extraction" && failure == "none";
            if (!terminalDeath && !extractionCrisis)
                GameplayExperienceFacts.Fail("carrier death transition");
            if (responder)
            {
                return facts.Judge(GameplayExperienceCategory.Meaningful,
                    "medical-response-required",
                    "의료 대응이 필요한 실제 위기에 참여하여 의미 있는 경험으로 평가함.");
            }
            return facts.Judge(GameplayExperienceCategory.Transformative,
                "carrier-death-confirmed",
                "운반자의 실제 사망이 확인되어 부모·운반자·자녀에게 결정적 경험으로 평가함.");
        }
        if (transition == "emergency-extraction" && before == "waiting-emergency-extraction")
        {
            return responder
                ? facts.Judge(GameplayExperienceCategory.Meaningful,
                    "medical-response-performed",
                    "실제 응급 의료 대응에 참여해 의미 있는 경험으로 평가함.")
                : facts.Judge(GameplayExperienceCategory.Intense,
                    "emergency-extraction-performed",
                    "응급 추출이 실제 수행되어 강렬한 위기 경험으로 평가함.");
        }
        if (transition == "daily-transition")
        {
            if (after == "failed" && failure == "miscarriage")
            {
                return ReproductionMajor(facts, responder,
                    "실제 유산이 확인되어 결정적 경험으로 평가함.");
            }
            if (after == "failed" && failure == "emergency-extraction-expired")
            {
                return facts.Judge(GameplayExperienceCategory.Intense,
                    "emergency-extraction-expired",
                    "응급 추출 기한이 실제 만료되어 강렬한 위기 실패로 평가하되 추가 사망은 추정하지 않음.");
            }
            if (after == "failed")
            {
                return facts.Judge(GameplayExperienceCategory.Meaningful,
                    "reproduction-process-failed",
                    "수태 또는 환경 조건으로 생식 과정이 실제 실패해 의미 있는 좌절로 평가하되 영구 충격은 추정하지 않음.");
            }
            if (after == before)
            {
                return facts.Judge(GameplayExperienceCategory.Routine,
                    "reproduction-daily-continuation",
                    "생식 과정의 일일 상태 유지로 확인되어 일상 경험으로 평가함.");
            }
            if (after == "waiting-environment")
            {
                return facts.Judge(GameplayExperienceCategory.Mild,
                    "reproduction-environment-wait",
                    "환경 조건을 기다리는 실제 상태 전환을 가벼운 경험으로 평가함.");
            }
            return facts.Judge(GameplayExperienceCategory.Mild,
                "reproduction-daily-state-changed",
                "생식 과정이 환경 대기에서 복귀하거나 완료 상태로 전환되어 가벼운 경험으로 평가함. 실제 출생은 별도 사건으로 평가함.");
        }
        GameplayExperienceFacts.Fail("unsupported reproduction transition");
        return default;
    }

    private static GameplayExperienceJudgment ReproductionMajor(GameplayExperienceFacts facts,
        bool responder, string explanation)
    {
        return responder
            ? facts.Judge(GameplayExperienceCategory.Meaningful,
                "medical-response-to-major-transition",
                "중대한 생식 전환에 의료 대응자로 참여해 의미 있는 경험으로 평가함.")
            : facts.Judge(GameplayExperienceCategory.Transformative,
                "major-reproduction-transition", explanation);
    }

    private static string ReproductionStatus(string value) => value switch
    {
        "none" or "planned" or "active" or "waiting-environment"
            or "waiting-emergency-extraction" or "completed" or "failed" => value,
        _ => throw new GameplayExperienceEvidenceException("reproduction status")
    };

    private static bool ValidReproductionTransition(string transition,
        string before, string after, string failureBefore, string failureAfter)
    {
        if (failureBefore != "none")
            return false;
        if (transition == "planned")
            return before == "none" && after == "planned" && failureAfter == "none";
        if (transition == "started")
            return before == "planned" && after == "active" && failureAfter == "none";
        if (transition == "carrier-death")
        {
            return before == "active"
                && (after == "waiting-emergency-extraction" && failureAfter == "none"
                    || after == "failed" && failureAfter == "carrier-died");
        }
        if (transition == "emergency-extraction")
        {
            return before == "waiting-emergency-extraction"
                && after == "active" && failureAfter == "none";
        }
        if (transition == "birth-published")
            return before == "completed" && after == "completed" && failureAfter == "none";
        if (transition != "daily-transition")
            return false;
        bool beforeDaily = before is "active" or "waiting-environment"
            or "waiting-emergency-extraction";
        if (!beforeDaily)
            return false;
        if (after == "failed")
        {
            return failureAfter switch
            {
                "miscarriage" => before == "active",
                "conception-failed" => before is "active" or "waiting-environment",
                "incubation-environment-failed" => before == "waiting-environment",
                "emergency-extraction-expired" => before == "waiting-emergency-extraction",
                _ => false
            };
        }
        if (failureAfter != "none")
            return false;
        return (before is "active" or "waiting-environment")
            && (after is "active" or "waiting-environment" or "completed");
    }

    private static string ReproductionFailure(string value) => value switch
    {
        "none" or "kinship-restricted" or "incompatible-role"
            or "cross-lineage-incubator-required" or "golem-genetic-hybrid-forbidden"
            or "conception-failed" or "miscarriage" or "incubation-environment-failed"
            or "carrier-died" or "emergency-extraction-expired" => value,
        _ => throw new GameplayExperienceEvidenceException("reproduction failure")
    };
}

public sealed class CharacterRitualFastingTransitionedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public CharacterRitualFastingTransitionedExperiencePolicy()
        : base(CharacterRitualFastingOutcomeIds.Transitioned.Value,
            "의례 단식의 시작·완료·중단 전환") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, CharacterRitualFastingOutcomeIds.Faster.Value);
        string transition = facts.Fact(CharacterRitualFastingOutcomeIds.Facts[0].Value);
        CharacterRitualFastPhase before = facts.EnumFact<CharacterRitualFastPhase>(
            CharacterRitualFastingOutcomeIds.Facts[3].Value);
        CharacterRitualFastPhase after = facts.EnumFact<CharacterRitualFastPhase>(
            CharacterRitualFastingOutcomeIds.Facts[4].Value);
        facts.Number(CharacterRitualFastingOutcomeIds.Metrics[0].Value,
            CharacterRitualFastingOutcomeIds.Day.Value);
        facts.Number(CharacterRitualFastingOutcomeIds.Metrics[1].Value,
            CharacterRitualFastingOutcomeIds.Day.Value);
        facts.Number(CharacterRitualFastingOutcomeIds.Metrics[2].Value,
            CharacterRitualFastingOutcomeIds.Mood.Value);
        facts.Number(CharacterRitualFastingOutcomeIds.Metrics[3].Value,
            CharacterRitualFastingOutcomeIds.Mood.Value);
        if (!facts.Success)
            return facts.Technical("ritual-fasting-transition-not-committed");
        if (transition == CharacterRitualFastingOutcomeIds.Started
            && before == CharacterRitualFastPhase.Inactive
            && after == CharacterRitualFastPhase.Fasting)
        {
            return facts.Judge(GameplayExperienceCategory.Mild, "ritual-fasting-started",
                "의례 단식이 실제 시작되어 가벼운 경험으로 평가함.");
        }
        if (transition == CharacterRitualFastingOutcomeIds.Completed
            && before == CharacterRitualFastPhase.Fasting
            && after == CharacterRitualFastPhase.AwaitingPostFastMeal)
        {
            return facts.Judge(GameplayExperienceCategory.Meaningful,
                "ritual-fasting-completed",
                "의례 단식을 실제 완수해 의미 있는 경험으로 평가함.");
        }
        if (transition == CharacterRitualFastingOutcomeIds.Interrupted
            && before == CharacterRitualFastPhase.Fasting
            && after == CharacterRitualFastPhase.Inactive)
        {
            return facts.Judge(GameplayExperienceCategory.Mild,
                "ritual-fasting-interrupted",
                "의례 단식이 실제 중단되었음을 가벼운 경험으로 평가하며 실패나 트라우마는 추가 추정하지 않음.");
        }
        GameplayExperienceFacts.Fail("unsupported ritual fasting transition");
        return default;
    }
}
