using System;

public sealed class ExternalVisitorReceptionAppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public ExternalVisitorReceptionAppliedExperiencePolicy() : base(
        "external.visitor-reception-applied",
        "성공한 방문객 actor의 응대 적용은 Routine; summary에서 기분 크기를 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "visitor-reception-applied", "응대가 실제 적용된 방문객의 일상 경험.", "actor");
}

public sealed class FactionRouteArrivedExperiencePolicy : GameplayActionExperiencePolicy
{
    public FactionRouteArrivedExperiencePolicy() : base(
        "faction.route-arrived",
        "성공한 route/faction 당사자에게만 Mild; source/destination은 기술 참조다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.RoleAction(facts, GameplayExperienceCategory.Mild,
            "route-arrival-experience", "교역로가 실제 도착한 당사자의 가벼운 사건 경험.",
            "route", "faction");
}

public sealed class FactionRouteCargoDeliveredExperiencePolicy : GameplayActionExperiencePolicy
{
    public FactionRouteCargoDeliveredExperiencePolicy() : base(
        "faction.route-cargo-delivered",
        "성공한 route/faction 당사자에게만 Mild; 화물량으로 개인 서사를 확대하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.RoleAction(facts, GameplayExperienceCategory.Mild,
            "route-cargo-experience", "교역 화물이 실제 인도된 당사자의 가벼운 사건 경험.",
            "route", "faction");
}

public sealed class FactionRouteSettledExperiencePolicy : GameplayActionExperiencePolicy
{
    public FactionRouteSettledExperiencePolicy() : base(
        "faction.route-settled",
        "성공한 route/faction 정산 당사자에게만 Mild; 금액으로 파산·부흥을 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.RoleAction(facts, GameplayExperienceCategory.Mild,
            "route-settlement-experience", "교역로 정산이 확정된 당사자의 가벼운 사건 경험.",
            "route", "faction");
}

public sealed class FactionTrustChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public FactionTrustChangedExperiencePolicy() : base(
        "faction.trust-changed",
        "실제 trust before/after가 있는 actor/faction/target만 |delta|>=20 Meaningful, 0 Routine, 그 외 Mild다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.HasRole("actor", "faction", "target"))
            return facts.Technical("trust-observer-or-system-role");
        if (!facts.Success)
            return facts.Technical("trust-change-not-applied");
        double before = facts.Number("trust-before", MigratedExperiencePolicyFacts.Point);
        double after = facts.Number("trust-after", MigratedExperiencePolicyFacts.Point);
        double delta = Math.Abs(after - before);
        GameplayExperienceCategory category = delta == 0d
            ? GameplayExperienceCategory.Routine
            : delta >= 20d
                ? GameplayExperienceCategory.Meaningful
                : GameplayExperienceCategory.Mild;
        return facts.Judge(category,
            delta == 0d ? "trust-unchanged" : delta >= 20d
                ? "major-trust-change" : "minor-trust-change",
            delta == 0d
                ? "구조화된 신뢰 전후치가 같아 Routine."
                : delta >= 20d
                    ? "구조화된 신뢰 절대 변화가 20 이상인 의미 있는 관계 변화."
                    : "구조화된 신뢰 변화가 있으나 20 미만이어서 Mild.");
    }
}
