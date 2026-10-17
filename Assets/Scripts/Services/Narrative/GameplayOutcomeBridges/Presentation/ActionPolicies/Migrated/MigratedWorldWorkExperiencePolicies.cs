public sealed class WorldSeasonalWildlifeArrivedExperiencePolicy : GameplayActionExperiencePolicy
{
    public WorldSeasonalWildlifeArrivedExperiencePolicy() : base(
        "world.seasonal-wildlife-arrived",
        "실제 생성된 wildlife target은 Mild; count로 생존 위기나 번영을 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.HasRole("target") || facts.Binding.subjectKindId != "wildlife")
            return facts.Technical("seasonal-wildlife-reference-subject");
        if (!facts.Success)
            return facts.Technical("seasonal-wildlife-not-arrived");
        double count = facts.Number("count", MigratedExperiencePolicyFacts.Count, anyReference: true);
        if (count <= 0d)
            GameplayExperienceFacts.Fail("seasonal-wildlife-count");
        return facts.Judge(GameplayExperienceCategory.Mild,
            "seasonal-wildlife-arrival", "실제 생성·도착한 야생동물의 가벼운 환경 변화 경험.");
    }
}

public sealed class WorldSeasonalDriftCargoResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public WorldSeasonalDriftCargoResolvedExperiencePolicy() : base(
        "world.seasonal-drift-cargo-resolved",
        "item subject의 확보/유실 정산이며 개인 경험이 아니다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class EquipmentApparelWorkOrderCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EquipmentApparelWorkOrderCompletedExperiencePolicy() : base(
        "equipment.apparel-work-order-completed",
        "facility/output만 기록되어 개인 작업 경험을 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class EquipmentCombatCraftCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EquipmentCombatCraftCompletedExperiencePolicy() : base(
        "equipment.combat-craft-completed",
        "성공한 contributor actor는 Routine, facility/output은 기술 사실; 실패는 Technical이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "combat-craft-work", "전투 장비 제작에 실제 기여한 일반 작업 경험.", "actor");
}

public sealed class WorkRepairResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public WorkRepairResolvedExperiencePolicy() : base(
        "work.repair-resolved",
        "성공한 actor 수리 작업은 Routine; facility/target은 기술 참조다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "repair-work", "실제 완료한 일반 수리 작업 경험.", "actor");
}

public sealed class WorldResourceCycleResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public WorldResourceCycleResolvedExperiencePolicy() : base(
        "world.resource-cycle-resolved",
        "성공한 actor 자원 작업은 Routine; target 자원은 기술 참조다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "resource-work", "실제 완료한 일반 자원 작업 경험.", "actor");
}

public sealed class EconomyAnimalHusbandryCommandResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EconomyAnimalHusbandryCommandResolvedExperiencePolicy() : base(
        "economy.animal-husbandry-command-resolved",
        "facility/target 명령 결과만 기록되어 개인 경험을 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class EconomyAnimalHusbandryWorkCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EconomyAnimalHusbandryWorkCompletedExperiencePolicy() : base(
        "economy.animal-husbandry-work-completed",
        "성공한 actor 축산/도축 작업은 Routine; operation/facility/item은 기술 사실이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "husbandry-work", "실제 완료한 일반 축산 작업 경험.", "actor");
}

public sealed class EconomyStockPolicySaleCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EconomyStockPolicySaleCompletedExperiencePolicy() : base(
        "economy.stock-policy-sale-completed",
        "operation/item/policy/destination의 판매 정산이며 금액으로 개인 경험을 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class EconomyQualityRejectedSaleCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EconomyQualityRejectedSaleCompletedExperiencePolicy() : base(
        "economy.quality-rejected-sale-completed",
        "operation/item/destination의 품질 거절 판매 정산이며 개인 경험이 아니다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class SurvivalFoulWaterConsequenceExperiencePolicy : GameplayActionExperiencePolicy
{
    public SurvivalFoulWaterConsequenceExperiencePolicy() : base(
        "survival.foul-water-consequence",
        "typed health/infection 변화로 실제 오염수 결과를 판정한다. health 손실은 피해 비율, 감염 악화는 크기로 평가한다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Success)
            return facts.Technical("foul-water-consequence-not-applied");
        if (!facts.Character || !facts.HasRole("actor"))
        {
            if (!facts.Character)
                return facts.Technical("foul-water-reference-subject");
            GameplayExperienceFacts.Fail("foul-water-actor-role");
        }
        double healthBefore = facts.Number("health-before", MigratedExperiencePolicyFacts.Point);
        double healthAfter = facts.Number("health-after", MigratedExperiencePolicyFacts.Point);
        double infectionBefore = facts.Number("infection-before", MigratedExperiencePolicyFacts.Point);
        double infectionAfter = facts.Number("infection-after", MigratedExperiencePolicyFacts.Point);
        if (healthBefore < 0d || healthAfter < 0d
            || infectionBefore < 0d || infectionAfter < 0d)
            GameplayExperienceFacts.Fail("foul-water-vitals");
        if (healthAfter < healthBefore)
            return facts.Damage(healthBefore, healthAfter, healthBefore - healthAfter,
                "오염수 결과로 인한");
        double infection = infectionAfter - infectionBefore;
        return facts.Judge(infection >= 20d
                ? GameplayExperienceCategory.Meaningful
                : infection > 0d
                    ? GameplayExperienceCategory.Mild
                    : GameplayExperienceCategory.Routine,
            infection >= 20d ? "major-infection-increase" : infection > 0d
                ? "minor-infection-increase" : "foul-water-without-measured-harm",
            infection > 0d
                ? "typed 감염 수치의 실제 악화 크기로 판정."
                : "오염수 결과는 기록됐지만 typed 건강·감염 악화가 없어 Routine.");
    }
}
