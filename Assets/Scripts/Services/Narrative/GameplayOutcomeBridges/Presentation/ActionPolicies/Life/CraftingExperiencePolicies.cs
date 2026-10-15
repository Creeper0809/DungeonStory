using static LifeExperiencePolicyFacts;

public sealed class EquipmentApparelChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EquipmentApparelChangedExperiencePolicy()
        : base(EvolutionOutcomeIds.ApparelChanged.Value,
            "의류 착용 상태가 실제로 바뀐 일상 사건") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, EvolutionOutcomeIds.WearerRole.Value);
        Boolean(facts.Number(EvolutionOutcomeIds.ApparelEquippedMetric.Value,
            EvolutionOutcomeIds.BooleanUnit.Value, anyReference: true), "apparel equipped state");
        EnumNumber<CharacterCommandOrigin>(facts.Number(
            EvolutionOutcomeIds.ApparelOriginMetric.Value,
            EvolutionOutcomeIds.EnumUnit.Value, anyReference: true), "apparel origin");
        return facts.Action(GameplayExperienceCategory.Routine,
            "의류 착용 상태의 실제 변경을 일상 활동으로 평가함.");
    }
}

public sealed class EquipmentApparelPhysicalCompletedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public EquipmentApparelPhysicalCompletedExperiencePolicy()
        : base(EvolutionOutcomeIds.ApparelPhysicalCompleted.Value,
            "의류 제작의 실제 물리 완료와 선택적 품질 결과") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        double input = facts.Number(EvolutionOutcomeIds.PhysicalInputMassMetric.Value,
            EvolutionOutcomeIds.GramUnit.Value, anyReference: true);
        double output = facts.Number(EvolutionOutcomeIds.PhysicalOutputMassMetric.Value,
            EvolutionOutcomeIds.GramUnit.Value, anyReference: true);
        Nonnegative(input, "physical input mass");
        Nonnegative(output, "physical output mass");
        bool hasQuality = facts.HasMetric(EvolutionOutcomeIds.ProductQualityTierMetric.Value);
        bool hasAttempt = facts.HasMetric(EvolutionOutcomeIds.ProductQualityAttemptMetric.Value);
        bool hasRejected = facts.HasMetric(EvolutionOutcomeIds.ProductRejectedMetric.Value);
        if (hasQuality != hasAttempt || hasQuality != hasRejected)
            GameplayExperienceFacts.Fail("incomplete optional product quality");
        if (!hasQuality)
        {
            return facts.Action(GameplayExperienceCategory.Routine,
                "실제 제작 완료만 확인되어 일상 제작으로 평가함.");
        }
        CraftsmanshipQualityTier quality = EnumNumber<CraftsmanshipQualityTier>(facts.Number(
            EvolutionOutcomeIds.ProductQualityTierMetric.Value,
            EvolutionOutcomeIds.EnumUnit.Value, anyReference: true), "product quality");
        WholeNonnegative(facts.Number(EvolutionOutcomeIds.ProductQualityAttemptMetric.Value,
            EvolutionOutcomeIds.CountUnit.Value, anyReference: true), "quality attempt");
        bool rejected = Boolean(facts.Number(EvolutionOutcomeIds.ProductRejectedMetric.Value,
            EvolutionOutcomeIds.BooleanUnit.Value, anyReference: true), "quality rejection");
        return facts.Success ? CraftingExperienceJudgment.Quality(facts, quality, rejected)
            : facts.Technical("apparel-physical-not-completed");
    }
}

public sealed class EquipmentProductQualityResolvedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public EquipmentProductQualityResolvedExperiencePolicy()
        : base(EvolutionOutcomeIds.ProductQualityResolved.Value,
            "제작자의 실제 제품 품질 확정") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, EvolutionOutcomeIds.ProductMakerRole.Value);
        CraftsmanshipQualityTier quality = EnumNumber<CraftsmanshipQualityTier>(facts.Number(
            EvolutionOutcomeIds.ProductQualityTierMetric.Value,
            EvolutionOutcomeIds.EnumUnit.Value, anyReference: true), "product quality");
        WholeNonnegative(facts.Number(EvolutionOutcomeIds.ProductQualityAttemptMetric.Value,
            EvolutionOutcomeIds.CountUnit.Value, anyReference: true), "quality attempt");
        bool rejected = Boolean(facts.Number(EvolutionOutcomeIds.ProductRejectedMetric.Value,
            EvolutionOutcomeIds.BooleanUnit.Value, anyReference: true), "quality rejection");
        return facts.Success ? CraftingExperienceJudgment.Quality(facts, quality, rejected)
            : facts.Technical("quality-resolution-failed");
    }
}

internal static class CraftingExperienceJudgment
{
    internal static GameplayExperienceJudgment Quality(GameplayExperienceFacts facts,
        CraftsmanshipQualityTier quality, bool rejected)
    {
        if (rejected)
            return facts.Technical("product-rejected-below-minimum");
        GameplayExperienceCategory category = quality is CraftsmanshipQualityTier.Legendary
            or CraftsmanshipQualityTier.Mythic
                ? GameplayExperienceCategory.Meaningful
                : GameplayExperienceCategory.Mild;
        return facts.Judge(category,
            category == GameplayExperienceCategory.Meaningful
                ? "highest-product-quality-confirmed" : "product-quality-confirmed",
            category == GameplayExperienceCategory.Meaningful
                ? "전설급 이상 품질이 실제 확정되어 의미 있는 제작 성취로 평가함."
                : "제품 품질이 실제 확정되었으나 최고 품질은 아니어서 가벼운 제작 성취로 평가함.");
    }
}
