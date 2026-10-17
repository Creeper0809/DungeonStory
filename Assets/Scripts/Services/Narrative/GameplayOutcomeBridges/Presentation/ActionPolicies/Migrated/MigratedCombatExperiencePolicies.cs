using System.Collections.Generic;

public sealed class CombatCharacterDownedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CombatCharacterDownedExperiencePolicy() : base(
        "combat.character-downed",
        "성공한 전투 불능의 target 캐릭터만 강렬한 경험(.80)으로 판정한다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Intense,
            "confirmed-downed", "실제 전투 불능이 확정된 당사자 경험.", "target");
}

public sealed class CombatCharacterRecoveredExperiencePolicy : GameplayActionExperiencePolicy
{
    public CombatCharacterRecoveredExperiencePolicy() : base(
        "combat.character-recovered",
        "성공한 전투 복귀의 target 캐릭터만 의미 있는 경험(.55)으로 판정한다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Meaningful,
            "confirmed-recovery", "전투 불능에서 실제 복귀한 당사자 경험.", "target");
}

public sealed class CharacterDeathExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterDeathExperiencePolicy() : base(
        "character.death",
        "성공한 death의 target 캐릭터는 결정적 경험(.95); 다른 개체는 기술 사실이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedCombatExperienceFacts.Death(facts, includeKiller: false);
}

public sealed class CombatCharacterKilledExperiencePolicy : GameplayActionExperiencePolicy
{
    public CombatCharacterKilledExperiencePolicy() : base(
        "combat.character-killed",
        "성공한 killed의 target 피해자는 .95, actor 살해자는 .80으로 역할별 판정한다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedCombatExperienceFacts.Death(facts, includeKiller: true);
}

public sealed class CombatAttackResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CombatAttackResolvedExperiencePolicy() : base(
        "combat.attack-resolved",
        "고정 detail 형식의 실행/피해만 읽는다. 성공한 actor/target은 피해 중증도 근거가 없어 Mild, 실패는 기술 결과다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Success)
            return facts.Technical("attack-not-completed");
        if (!facts.Character)
            return facts.Technical("non-character-subject");
        bool actor = facts.HasRole("actor");
        bool target = !actor && facts.HasRole("target");
        if (!actor && !target)
            GameplayExperienceFacts.Fail("attack-participant-role");

        Dictionary<string, string> detail = MigratedExperiencePolicyFacts.StrictDetail(
            facts, "executed", "hit", "coverBlocked", "bodyPart", "damage");
        bool executed = MigratedExperiencePolicyFacts.StrictBoolean(detail, "executed");
        _ = MigratedExperiencePolicyFacts.StrictBoolean(detail, "hit");
        _ = MigratedExperiencePolicyFacts.StrictBoolean(detail, "coverBlocked");
        double damage = MigratedExperiencePolicyFacts.StrictNumber(detail, "damage", minimum: 0d);
        if (!executed)
            return facts.Technical("attack-not-executed");
        return facts.Judge(GameplayExperienceCategory.Mild,
            damage > 0d ? "attack-damage-without-severity" : "attack-without-damage-severity",
            target && damage > 0d
                ? "실제 피해량은 확인되나 직전 체력 비율이 없어 가벼운 피해 경험으로만 판정."
                : "실행된 공격 판정은 확인되나 중상·생명 위기의 구조화 근거가 없어 Mild를 넘지 않음.");
    }
}

public sealed class CharacterHealthThresholdCrossedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterHealthThresholdCrossedExperiencePolicy() : base(
        "character.health-threshold-crossed",
        "고정 ratio/downed detail만 읽어 0 health=.95, downed=.80, ratio<=.25=.55, 그 외 Mild로 판정한다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Success)
            return facts.Technical("health-transition-not-confirmed");
        if (!facts.Character || !facts.HasRole("target"))
        {
            if (!facts.Character)
                return facts.Technical("non-character-subject");
            GameplayExperienceFacts.Fail("health-threshold-role");
        }

        string detailText = facts.Fact("detail");
        Dictionary<string, string> detail =
            MigratedExperiencePolicyFacts.ParseCanonicalPairs(detailText);
        if (detail.ContainsKey("beforeRatio") || detail.ContainsKey("afterRatio"))
        {
            MigratedExperiencePolicyFacts.RequireKeys(detail, "beforeRatio", "afterRatio");
            double before = MigratedExperiencePolicyFacts.StrictNumber(
                detail, "beforeRatio", 0d, 1d);
            double after = MigratedExperiencePolicyFacts.StrictNumber(
                detail, "afterRatio", 0d, 1d);
            if (before.Equals(after))
                GameplayExperienceFacts.Fail("unchanged-health-ratio");
            if (after == 0d)
                return facts.Judge(GameplayExperienceCategory.Transformative,
                    "health-exhausted", "구조화된 afterRatio가 0이므로 결정적 건강 붕괴로 판정.");
            if (after <= .25d)
                return facts.Judge(GameplayExperienceCategory.Meaningful,
                    "critical-health-threshold", "구조화된 afterRatio가 25% 이하인 실제 건강 임계 전이.");
            return facts.Judge(GameplayExperienceCategory.Mild,
                "health-threshold-crossed", "건강 임계 전이는 확인되나 중대 상태 근거가 없어 Mild로 판정.");
        }

        MigratedExperiencePolicyFacts.RequireKeys(detail, "beforeDowned", "afterDowned");
        bool beforeDowned = MigratedExperiencePolicyFacts.StrictBoolean(detail, "beforeDowned");
        bool afterDowned = MigratedExperiencePolicyFacts.StrictBoolean(detail, "afterDowned");
        if (beforeDowned == afterDowned)
            GameplayExperienceFacts.Fail("unchanged-downed-state");
        return facts.Judge(afterDowned
                ? GameplayExperienceCategory.Intense
                : GameplayExperienceCategory.Mild,
            afterDowned ? "downed-threshold" : "downed-threshold-recovered",
            afterDowned
                ? "구조화된 downed 전이가 실제 전투 불능을 확인하므로 강렬한 경험."
                : "downed 해제는 확인되나 별도 회복 중증도 근거가 없어 Mild로 판정.");
    }
}

internal static class MigratedCombatExperienceFacts
{
    internal static GameplayExperienceJudgment Death(
        GameplayExperienceFacts facts,
        bool includeKiller)
    {
        if (!facts.Success)
            return facts.Technical("death-not-confirmed");
        if (!facts.Character)
            return facts.Technical("non-character-subject");
        if (facts.HasRole("target"))
        {
            return facts.Judge(GameplayExperienceCategory.Transformative,
                "confirmed-death-victim",
                "실제 사망이 확정된 피해자 당사자이므로 결정적 경험으로 판정.");
        }
        if (includeKiller && facts.HasRole("actor"))
        {
            return facts.Judge(GameplayExperienceCategory.Intense,
                "confirmed-killer",
                "실제 살해 귀속이 확정된 행위자이므로 강렬한 경험으로 판정.");
        }
        GameplayExperienceFacts.Fail("death-participant-role");
        return default;
    }
}
