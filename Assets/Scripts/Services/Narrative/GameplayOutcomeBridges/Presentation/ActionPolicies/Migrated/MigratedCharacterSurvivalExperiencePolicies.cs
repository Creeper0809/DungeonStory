using System;
using System.Collections.Generic;

public sealed class CharacterMentorshipAwardedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterMentorshipAwardedExperiencePolicy() : base(
        "character.mentorship-awarded",
        "actor 단일 subject의 보상 실행 확인만 있어 Routine; 관계 깊이는 summary에서 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "mentorship-award-confirmed", "멘토링 보상 적용이 확인된 일상 경험.", "actor");
}

public sealed class CharacterAgeConditionChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterAgeConditionChangedExperiencePolicy() : base(
        "character.age-condition-changed",
        "actor 단일 subject의 연령 조건 전이 확인만 있어 Routine; 전이 방향은 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "age-condition-confirmed", "연령 조건 전이가 적용되었다는 실행 확인.", "actor");
}

public sealed class SurvivalMealConsumedExperiencePolicy : GameplayActionExperiencePolicy
{
    public SurvivalMealConsumedExperiencePolicy() : base(
        "survival.meal-consumed",
        "typed health/hunger 전후치로 피해 또는 큰 회복만 승격하고 기본 식사는 Routine이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedSurvivalExperienceFacts.Nourishment(
            facts, "hunger-before", "hunger-after", "meal-consumption");
}

public sealed class SurvivalSubstanceConsumedExperiencePolicy : GameplayActionExperiencePolicy
{
    public SurvivalSubstanceConsumedExperiencePolicy() : base(
        "survival.substance-consumed",
        "actor 단일 subject의 섭취 실행 확인만 있어 성공 Routine, 비성공 Technical이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "substance-consumption-confirmed", "기호품 섭취 적용이 확인된 일상 경험.", "actor");
}

public sealed class SurvivalDetoxTreatedExperiencePolicy : GameplayActionExperiencePolicy
{
    public SurvivalDetoxTreatedExperiencePolicy() : base(
        "survival.detox-treated",
        "actor 단일 subject의 해독 실행 확인만 있어 성공 Routine; 치료량·회복을 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "detox-treatment-confirmed", "해독 치료 적용이 확인된 일상 경험.", "actor");
}

public sealed class CharacterLifeStageChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterLifeStageChangedExperiencePolicy() : base(
        "character.life-stage-changed",
        "actor 단일 subject의 생애 단계 전이 확인만 있어 Routine; 방향·원인은 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "life-stage-confirmed", "생애 단계 전이가 적용되었다는 실행 확인.", "actor");
}

public sealed class SurvivalPrimitiveCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public SurvivalPrimitiveCompletedExperiencePolicy() : base(
        "survival.primitive-completed",
        "actor 단일 subject의 생존 행동 완료 확인만 있어 Routine이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "primitive-survival-confirmed", "원시 생존 행동 완료가 확인된 일상 경험.", "actor");
}

public sealed class CharacterProficiencyAwardedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterProficiencyAwardedExperiencePolicy() : base(
        "character.proficiency-awarded",
        "typed rank 상승은 Meaningful, 경험치만 증가한 일반 숙련 작업은 Routine이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedSurvivalExperienceFacts.Proficiency(facts);
}

public sealed class SurvivalTabooIncidentExperiencePolicy : GameplayActionExperiencePolicy
{
    public SurvivalTabooIncidentExperiencePolicy() : base(
        "survival.taboo-incident",
        "실제 extreme-hunger corpse-consumption(quantity>0+physical commit)의 eater는 .95; 최소 humanoid butchery actor는 Mild다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Success)
            return facts.Technical("taboo-action-not-completed");
        if (!facts.Character)
            return facts.Technical("taboo-reference-subject");
        if (!facts.HasRole("actor"))
        {
            if (facts.HasRole("target"))
                return facts.Technical("corpse-is-not-experiencing-subject");
            GameplayExperienceFacts.Fail("taboo-character-role");
        }

        if (!facts.HasMetric("quantity"))
        {
            if (facts.HasMetric("input-mass")
                || MigratedExperiencePolicyFacts.HasFact(facts, "reason-code")
                || MigratedExperiencePolicyFacts.HasFact(facts, "physical-commit-id")
                || MigratedExperiencePolicyFacts.HasFact(facts, "detail"))
            {
                GameplayExperienceFacts.Fail("corpse-consumption-quantity");
            }
            return facts.Judge(GameplayExperienceCategory.Mild,
                "humanoid-butchery-only",
                "최소 도축 receipt는 행위 완료만 확인한다. 실제 식인·섭취를 추정하지 않고 Mild로 제한.");
        }

        double quantity = facts.Number("quantity", MigratedExperiencePolicyFacts.Count);
        _ = facts.Number("input-mass", MigratedExperiencePolicyFacts.Gram);
        string reason = facts.Fact("reason-code");
        string physicalCommit = facts.Fact("physical-commit-id");
        Dictionary<string, string> detail = MigratedExperiencePolicyFacts.StrictDetail(
            facts, "action", "sameSpecies", "deathReason");
        _ = MigratedExperiencePolicyFacts.StrictBoolean(detail, "sameSpecies");
        if (quantity <= 0d || reason != "extreme-hunger"
            || detail["action"] != "corpse-consumption"
            || string.IsNullOrWhiteSpace(physicalCommit))
            GameplayExperienceFacts.Fail("corpse-consumption-evidence");
        return facts.Judge(GameplayExperienceCategory.Transformative,
            "confirmed-extreme-hunger-corpse-consumption",
            "극심한 굶주림, 실제 수량, 물리 commit이 모두 확인된 시체 섭취 당사자이므로 .95.");
    }
}

public sealed class SurvivalWaterConsumedExperiencePolicy : GameplayActionExperiencePolicy
{
    public SurvivalWaterConsumedExperiencePolicy() : base(
        "survival.water-consumed",
        "typed health/thirst 전후치로 피해 또는 큰 회복만 승격하고 기본 식수는 Routine이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedSurvivalExperienceFacts.Nourishment(
            facts, "thirst-before", "thirst-after", "water-consumption");
}

public sealed class SurvivalMealMissedExperiencePolicy : GameplayActionExperiencePolicy
{
    public SurvivalMealMissedExperiencePolicy() : base(
        "survival.meal-missed",
        "actor 단일 subject의 누락 확인만 있어 Routine; 결식 피해는 별도 typed 결과 없이는 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "meal-miss-confirmed", "식사 누락 사실의 일상 수준 기록.", "actor");
}

public sealed class CharacterFuneralObservedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterFuneralObservedExperiencePolicy() : base(
        "character.funeral-observed",
        "witness 캐릭터는 Intense(.80); deceased target 자체와 facility는 관찰 경험 주체가 아니다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Success)
            return facts.Technical("funeral-not-observed");
        if (facts.Character && facts.HasRole("witness"))
            return facts.Judge(GameplayExperienceCategory.Intense,
                "funeral-witness", "장례를 실제 목격한 캐릭터의 강렬한 상실 경험.");
        if (facts.HasRole("target"))
            return facts.Technical("deceased-not-observer");
        return facts.Technical("funeral-reference-subject");
    }
}

public sealed class CharacterLastLessonObservedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterLastLessonObservedExperiencePolicy() : base(
        "character.last-lesson-observed",
        "mentor actor와 student target은 Meaningful; facility/item/destination은 기술 참조다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Success)
            return facts.Technical("last-lesson-not-observed");
        if (facts.Character && facts.HasRole("actor", "target"))
            return facts.Judge(GameplayExperienceCategory.Meaningful,
                "last-lesson-participant", "마지막 가르침의 mentor/student 직접 당사자 경험.");
        return facts.Technical("last-lesson-reference-subject");
    }
}

public sealed class CharacterLineageCompressionObservedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterLineageCompressionObservedExperiencePolicy() : base(
        "character.lineage-compression-observed",
        "계보 압축은 저장·요약 처리이며 target 캐릭터도 개인 경험으로 판정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class CharacterProductionLossObservedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterProductionLossObservedExperiencePolicy() : base(
        "character.production-loss-observed",
        "실제 mass 손실을 본 actor/witness는 Mild(0이면 Routine); 시설·source는 기술 참조다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Success)
            return facts.Technical("production-loss-not-observed");
        if (!facts.Character)
            return facts.Technical("production-loss-reference-subject");
        if (!facts.HasRole("actor", "witness"))
            GameplayExperienceFacts.Fail("production-loss-character-role");
        double mass = facts.Number("mass", MigratedExperiencePolicyFacts.Gram, anyReference: true);
        if (mass < 0d)
            GameplayExperienceFacts.Fail("production-loss-mass");
        return facts.Judge(mass > 0d
                ? GameplayExperienceCategory.Mild
                : GameplayExperienceCategory.Routine,
            mass > 0d ? "production-loss-observed" : "zero-production-loss-observed",
            mass > 0d
                ? "실제 생산 손실량을 관측했으나 장기 생계 피해 근거는 없어 Mild."
                : "손실량이 0인 일반 관측이므로 Routine.");
    }
}

public sealed class SurvivalMealIncidentObservedExperiencePolicy : GameplayActionExperiencePolicy
{
    public SurvivalMealIncidentObservedExperiencePolicy() : base(
        "survival.meal-incident-observed",
        "actor 단일 subject의 관측 확인만 있어 Routine; incident 내용은 summary에서 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "meal-incident-observed", "식사 사건을 관측했다는 일상 수준 기록.", "actor");
}

public sealed class CharacterQuietPromotionObservedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterQuietPromotionObservedExperiencePolicy() : base(
        "character.quiet-promotion-observed",
        "typed rank 상승은 Meaningful, 경험치만 증가한 일반 작업 관측은 Routine이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedSurvivalExperienceFacts.Proficiency(facts);
}

public sealed class CharacterRestIdentityAppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterRestIdentityAppliedExperiencePolicy() : base(
        "character.rest-identity-applied",
        "typed sleep 전후치로 큰 욕구 해소만 Meaningful, 그 외 실제 휴식은 Routine이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Character || !facts.HasRole("actor"))
        {
            if (!facts.Character)
                return facts.Technical("rest-reference-subject");
            GameplayExperienceFacts.Fail("rest-actor-role");
        }
        if (!facts.Success)
            return facts.Technical("rest-not-applied");
        double before = facts.Number("sleep-before", MigratedExperiencePolicyFacts.Point);
        double after = facts.Number("sleep-after", MigratedExperiencePolicyFacts.Point);
        if (before < 0d || after < 0d)
            GameplayExperienceFacts.Fail("sleep-values");
        if (after < before)
            GameplayExperienceFacts.Fail("sleep-recovery-direction");
        bool rescuedCriticalNeed = before <= MigratedExperiencePolicyFacts.CriticalNeedValue
            && after >= MigratedExperiencePolicyFacts.SatisfiedNeedValue;
        return facts.Judge(rescuedCriticalNeed
                ? GameplayExperienceCategory.Meaningful
                : GameplayExperienceCategory.Routine,
            rescuedCriticalNeed ? "critical-sleep-need-resolved" : "routine-rest",
            rescuedCriticalNeed
                ? "sleep 값은 높을수록 충족이다. 직전 위기값(20 이하)에서 충분한 휴식 기준(80 이상)까지 회복되어 Meaningful."
                : "40점 보충만으로 승격하지 않는다. 위기값에서 충분한 휴식까지의 실제 전이가 없는 일반 휴식은 Routine.");
    }
}

internal static class MigratedSurvivalExperienceFacts
{
    internal static GameplayExperienceJudgment Proficiency(GameplayExperienceFacts facts)
    {
        if (!facts.Character)
            return facts.Technical("proficiency-source-subject");
        if (!facts.HasRole("actor"))
            GameplayExperienceFacts.Fail("proficiency-actor-role");
        if (!facts.Success)
            return facts.Technical("proficiency-award-not-applied");

        // Award receipts reference the proficiency; career observations reference the actor.
        string metricRole = facts.Binding.outcomeTypeId == "character.proficiency-awarded"
            ? "source" : "actor";
        double currentBefore = facts.Number(
            "current-experience-before", MigratedExperiencePolicyFacts.MilliExperience, metricRole);
        double currentAfter = facts.Number(
            "current-experience-after", MigratedExperiencePolicyFacts.MilliExperience, metricRole);
        double lifetimeBefore = facts.Number(
            "lifetime-experience-before", MigratedExperiencePolicyFacts.MilliExperience, metricRole);
        double lifetimeAfter = facts.Number(
            "lifetime-experience-after", MigratedExperiencePolicyFacts.MilliExperience, metricRole);
        double rankBefore = facts.Number("rank-before", "enum", metricRole);
        double rankAfter = facts.Number("rank-after", "enum", metricRole);
        if (rankBefore != Math.Truncate(rankBefore) || rankAfter != Math.Truncate(rankAfter)
            || rankBefore > int.MaxValue || rankBefore < int.MinValue
            || rankAfter > int.MaxValue || rankAfter < int.MinValue
            || !Enum.IsDefined(typeof(CareerRank), (int)rankBefore)
            || !Enum.IsDefined(typeof(CareerRank), (int)rankAfter))
            GameplayExperienceFacts.Fail("proficiency-rank-enum");
        if (currentBefore < 0d || currentAfter < 0d || lifetimeBefore < 0d
            || lifetimeAfter < lifetimeBefore || rankBefore < 0d || rankAfter < rankBefore)
            GameplayExperienceFacts.Fail("proficiency-progression");
        if (rankAfter > rankBefore)
            return facts.Judge(GameplayExperienceCategory.Meaningful,
                "proficiency-rank-increased", "구조화된 rank가 실제 상승한 승급이므로 Meaningful.");
        return facts.Judge(GameplayExperienceCategory.Routine,
            lifetimeAfter > lifetimeBefore || currentAfter != currentBefore
                ? "proficiency-work-progress" : "proficiency-no-rank-change",
            "rank 상승이 없는 일반 숙련 작업·보상은 Routine으로 판정.");
    }

    internal static GameplayExperienceJudgment Nourishment(
        GameplayExperienceFacts facts,
        string needBeforeId,
        string needAfterId,
        string reasonPrefix)
    {
        if (!facts.Character)
            return facts.Technical("consumption-reference-subject");
        if (!facts.HasRole("actor"))
            GameplayExperienceFacts.Fail("consumption-actor-role");
        if (!facts.Success && !facts.Partial)
            return facts.Technical("consumption-not-applied");

        double healthBefore = facts.Number("health-before", MigratedExperiencePolicyFacts.Point);
        double healthAfter = facts.Number("health-after", MigratedExperiencePolicyFacts.Point);
        double needBefore = facts.Number(needBeforeId, MigratedExperiencePolicyFacts.Point);
        double needAfter = facts.Number(needAfterId, MigratedExperiencePolicyFacts.Point);
        if (healthBefore < 0d || healthAfter < 0d || needBefore < 0d || needAfter < 0d
            || needAfter < needBefore)
            GameplayExperienceFacts.Fail("consumption-vitals");
        if (healthAfter < healthBefore)
            return facts.Damage(healthBefore, healthAfter, healthBefore - healthAfter,
                "섭취 당사자의");
        double healthRecovery = healthAfter - healthBefore;
        bool rescuedCriticalNeed = needBefore <= MigratedExperiencePolicyFacts.CriticalNeedValue
            && needAfter >= MigratedExperiencePolicyFacts.SatisfiedNeedValue;
        if (healthRecovery >= MigratedExperiencePolicyFacts.MajorHealthRecoveryPoints
            || rescuedCriticalNeed)
        {
            return facts.Judge(GameplayExperienceCategory.Meaningful,
                reasonPrefix + "-major-relief",
                healthRecovery >= MigratedExperiencePolicyFacts.MajorHealthRecoveryPoints
                    ? "최대 체력 정규화 근거가 없으므로 명시 정책 상수 20점 이상의 실제 건강 회복만 Meaningful."
                    : "욕구 값은 높을수록 충족이다. 직전 위기값(20 이하)에서 충족값(80 이상)까지 실제 완화되어 Meaningful.");
        }
        return facts.Judge(GameplayExperienceCategory.Routine,
            reasonPrefix + "-routine",
            "실제 섭취는 확인되지만 큰 회복·위기 근거가 없어 Routine.");
    }
}
