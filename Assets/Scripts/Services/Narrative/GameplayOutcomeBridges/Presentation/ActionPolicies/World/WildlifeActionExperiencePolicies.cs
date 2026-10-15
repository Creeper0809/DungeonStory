using System;
using System.Linq;
using C = GameplayExperienceCategory;
using static WorldExperiencePolicyFacts;

public sealed class EnvironmentWildlifeActionExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentWildlifeActionExperiencePolicy() : base("environment.wildlife-action", "사냥꾼은 사냥/처리 성취, 동물은 자신의 피해. 피해자 점수를 사냥꾼에 전이하지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        string action = f.Fact("event-kind");
        double before = Nonnegative(f.Number("health-before", "point", "wildlife"));
        double after = Nonnegative(f.Number("health-after", "point", "wildlife"));
        double damage = Nonnegative(f.Number("damage", "point", "wildlife"));
        if (f.HasRole("wildlife")) return f.Damage(before, after, damage, "야생동물 자신의 피해.");
        if (!f.HasRole("actor")) return f.Technical();
        if (action == "hunt-attack") return f.Judge(damage > 0 ? C.Mild : C.Routine, "hunting-action", "사냥 행동. 동물의 사망/피해를 사냥꾼 자신의 생명 위기로 평가하지 않음.");
        return f.Action(C.Mild, "야생동물 대상 실제 행동 완료: " + action);
    }
}

public sealed class EnvironmentWildlifeCommandExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentWildlifeCommandExperiencePolicy() : base("environment.wildlife-command", "야생동물 명령 설정. 아직 사냥·길들이기 성공이 아님.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Routine, "야생동물 명령 설정. 아직 사냥·길들이기 성공이 아님.");
}

public sealed class EnvironmentWildlifeFoodRaidCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentWildlifeFoodRaidCompletedExperiencePolicy() : base("environment.wildlife-food-raid-completed", "야생동물 먹이 습격 종료. 주민의 기아·생계 파탄을 추정하지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Mild, "야생동물 먹이 습격 종료. 주민의 기아·생계 파탄을 추정하지 않음.");
}

