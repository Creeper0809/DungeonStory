using System;
using System.Linq;
using C = GameplayExperienceCategory;
using static WorldExperiencePolicyFacts;

public sealed class EnvironmentFireDamageExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentFireDamageExperiencePolicy() : base("environment.fire-damage", "실제 피해 대상만 평가. 가연성 종료는 사망 확정이 아님.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        if (!f.HasRole("target")) return f.Technical("fire-context-not-target");
        double damage = Nonnegative(f.Number("damage", "point"));
        double combustible = f.Number("status", "enum");
        if (combustible != 0 && combustible != 1) GameplayExperienceFacts.Fail("combustible flag");
        if (damage == 0) return f.Judge(C.Routine, "no-fire-damage", "실제 화재 피해 0.");
        if (combustible == 0) return f.Judge(f.Character ? C.Intense : C.Meaningful, "fire-target-disabled", "피해로 대상이 더 이상 가연 대상이 아님. 쓰러짐/파괴 가능하나 사망으로 단정하지 않음.");
        return f.AppliedDamage(damage, "화재 피해.");
    }
}

public sealed class EnvironmentFireFuelLossExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentFireFuelLossExperiencePolicy() : base("environment.fire-fuel-loss", "Failed 상태라도 실제 양수 연료 소실은 경미한 손실. 개인 생계 파탄 추정 금지.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
            double quantity = Nonnegative(f.Number("quantity", "count", "item"));
            return quantity > 0 ? f.Judge(C.Mild, "physical-fire-fuel-loss", "실제 연료 소실. 처리 실패와 달리 물적 손실은 있으나 소유자 생계 파탄은 근거 없음.") : f.Technical("no-fuel-loss");
        }
}

public sealed class EnvironmentFireIgnitionExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentFireIgnitionExperiencePolicy() : base("environment.fire-ignition", "화재 발생 사실. 개인 피해/목격 여부는 별도 결과가 필요.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Mild, "화재 발생 사실. 개인 피해/목격 여부는 별도 결과가 필요.");
}

public sealed class EnvironmentFireSuppressionExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentFireSuppressionExperiencePolicy() : base("environment.fire-suppression", "진압자: 감소 .30, 완전 진압 .55, 변화 없음 0.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        double before = Nonnegative(f.Number("intensity-before", "ratio", "fire"));
        double after = Nonnegative(f.Number("intensity-after", "ratio", "fire"));
        if (after > before) GameplayExperienceFacts.Fail("suppression increased intensity");
        if (!f.HasRole("actor") || after == before) return f.Technical("no-personal-suppression-result");
        return f.Judge(after <= .0001 ? C.Meaningful : C.Mild, "confirmed-fire-suppression", "실제 화재 강도 감소/진압 성취. 구조한 생명을 임의로 만들지 않음.");
    }
}

public sealed class EnvironmentFireWaterConsumedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentFireWaterConsumedExperiencePolicy() : base("environment.fire-water-consumed", "진압용 물 소비. 진압 성공은 별도 결과에서 판단.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Routine, "진압용 물 소비. 진압 성공은 별도 결과에서 판단.");
}

