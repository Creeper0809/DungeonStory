using static LifeExperiencePolicyFacts;

public sealed class EquipmentEvolutionModuleSelectedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public EquipmentEvolutionModuleSelectedExperiencePolicy()
        : base(EquipmentModuleSelectionOutcomeIds.Selected.Value,
            "장비 진화 모듈 선택의 실제 확정") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        bool node = facts.HasRole(EquipmentModuleSelectionOutcomeIds.SelectedNodeRole.Value);
        bool equipment = facts.HasRole(EquipmentModuleSelectionOutcomeIds.EquipmentRole.Value);
        if (!node && !equipment)
            GameplayExperienceFacts.Fail("equipment module subject role");
        ConsecutiveRevisions(
            facts.Number(EquipmentModuleSelectionOutcomeIds.RevisionBefore.Value,
                EquipmentModuleSelectionOutcomeIds.RevisionUnit.Value,
                EquipmentModuleSelectionOutcomeIds.SelectedNodeRole.Value),
            facts.Number(EquipmentModuleSelectionOutcomeIds.RevisionAfter.Value,
                EquipmentModuleSelectionOutcomeIds.RevisionUnit.Value,
                EquipmentModuleSelectionOutcomeIds.SelectedNodeRole.Value),
            "equipment module selection revision");
        facts.Fact(EquipmentModuleSelectionOutcomeIds.SelectionIdFact.Value);
        facts.Fact(EquipmentModuleSelectionOutcomeIds.ModulesFact.Value);
        return facts.Action(GameplayExperienceCategory.Meaningful,
            "장비 진화 모듈 선택이 실제 확정되어 의미 있는 변화로 평가함.");
    }
}

public sealed class TraitAcquiredInferenceCompletedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public TraitAcquiredInferenceCompletedExperiencePolicy()
        : base(EvolutionOutcomeIds.TraitInferenceCompleted.Value,
            "획득 특성의 실제 확정") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, EvolutionOutcomeIds.AcquiredTraitOwnerRole.Value);
        double before = facts.Number(EvolutionOutcomeIds.TraitExpectedRevisionMetric.Value,
            EvolutionOutcomeIds.RevisionUnit.Value);
        double after = facts.Number(EvolutionOutcomeIds.TraitResultingRevisionMetric.Value,
            EvolutionOutcomeIds.RevisionUnit.Value);
        if (before < 0d || after <= before)
            GameplayExperienceFacts.Fail("acquired trait revision");
        facts.Fact(EvolutionOutcomeIds.TraitInstanceFact.Value);
        facts.Fact(EvolutionOutcomeIds.TraitCombinationFact.Value);
        return facts.Action(GameplayExperienceCategory.Meaningful,
            "새 획득 특성이 실제 확정되어 의미 있는 캐릭터 변화로 평가함.");
    }
}

public sealed class CharacterHeritableTraitsAnalyzedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public CharacterHeritableTraitsAnalyzedExperiencePolicy()
        : base(HeritableTraitAnalysisOutcomeIds.Completed.Value,
            "유전 특성 분석의 실제 완료") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, HeritableTraitAnalysisOutcomeIds.SubjectRole.Value);
        bool before = Boolean(facts.Number(HeritableTraitAnalysisOutcomeIds.BeforeState.Value,
            HeritableTraitAnalysisOutcomeIds.BooleanUnit.Value), "trait analysis before");
        bool after = Boolean(facts.Number(HeritableTraitAnalysisOutcomeIds.AfterState.Value,
            HeritableTraitAnalysisOutcomeIds.BooleanUnit.Value), "trait analysis after");
        WholePositive(facts.Number(HeritableTraitAnalysisOutcomeIds.ConsumedKitCount.Value,
            HeritableTraitAnalysisOutcomeIds.CountUnit.Value), "trait analysis kit count");
        Nonnegative(facts.Number(HeritableTraitAnalysisOutcomeIds.PhysicalInputMass.Value,
            HeritableTraitAnalysisOutcomeIds.GramUnit.Value), "trait analysis input mass");
        facts.Fact(HeritableTraitAnalysisOutcomeIds.FacilityInstanceId.Value);
        facts.Fact(HeritableTraitAnalysisOutcomeIds.LatentTraitIds.Value);
        if (before || !after)
            GameplayExperienceFacts.Fail("trait analysis visibility transition");
        return facts.Action(GameplayExperienceCategory.Mild,
            "숨은 유전 특성 분석이 실제 완료되어 가벼운 자기 이해 경험으로 평가함.");
    }
}

public sealed class TraitAcquiredReactionExecutedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public TraitAcquiredReactionExecutedExperiencePolicy()
        : base(EvolutionOutcomeIds.TraitReactionExecuted.Value,
            "획득 특성 반응의 실제 적용") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, EvolutionOutcomeIds.TraitOwnerRole.Value);
        bool experience = facts.HasMetric(EvolutionOutcomeIds.TraitExperienceMetric.Value);
        bool mood = facts.HasMetric(EvolutionOutcomeIds.TraitMoodMetric.Value);
        if (experience == mood)
            GameplayExperienceFacts.Fail("trait reaction metric");
        double applied = facts.Number((experience ? EvolutionOutcomeIds.TraitExperienceMetric
            : EvolutionOutcomeIds.TraitMoodMetric).Value,
            EvolutionOutcomeIds.EffectPointUnit.Value, anyReference: true);
        if (double.IsNaN(applied) || double.IsInfinity(applied))
            GameplayExperienceFacts.Fail("trait reaction value");
        return facts.Action(GameplayExperienceCategory.Routine,
            "획득 특성의 경험치 또는 기분 반응 적용을 일상 반응으로 평가함.");
    }
}

public sealed class FacilityEvolutionCompletedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public FacilityEvolutionCompletedExperiencePolicy()
        : base(EvolutionOutcomeIds.FacilityEvolutionCompleted.Value,
            "시설 진화의 실제 완료") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, EvolutionOutcomeIds.EvolvedFacilityRole.Value);
        WholePositive(facts.Number(EvolutionOutcomeIds.FacilityStarGradeMetric.Value,
            EvolutionOutcomeIds.GradeUnit.Value, anyReference: true), "facility star grade");
        Nonnegative(facts.Number(EvolutionOutcomeIds.MaterialQuantityMetric.Value,
            EvolutionOutcomeIds.CountUnit.Value, anyReference: true), "facility material count");
        facts.Fact(EvolutionOutcomeIds.FacilityRecipeFact.Value);
        facts.Fact(EvolutionOutcomeIds.FacilityResultDefinitionFact.Value);
        return facts.Action(GameplayExperienceCategory.Meaningful,
            "시설 진화가 실제 완료되어 의미 있는 시설 변화로 평가함.");
    }
}

public sealed class TraitMemoryErasureTerminalExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public TraitMemoryErasureTerminalExperiencePolicy()
        : base(EvolutionOutcomeIds.MemoryErasureTerminal.Value,
            "기억 삭제의 실제 개정 반영 또는 비적용 종결") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, EvolutionOutcomeIds.ErasureTargetRole.Value);
        MemoryErasureSealUseStatus status = EnumNumber<MemoryErasureSealUseStatus>(facts.Number(
            EvolutionOutcomeIds.MemoryErasureStatusMetric.Value,
            EvolutionOutcomeIds.EnumUnit.Value), "memory erasure status");
        double before = facts.Number(EvolutionOutcomeIds.PreviousRevisionMetric.Value,
            EvolutionOutcomeIds.RevisionUnit.Value);
        double after = facts.Number(EvolutionOutcomeIds.CommittedRevisionMetric.Value,
            EvolutionOutcomeIds.RevisionUnit.Value);
        int consumed = WholeNonnegative(facts.Number(EvolutionOutcomeIds.ConsumedSealMetric.Value,
            EvolutionOutcomeIds.CountUnit.Value), "consumed memory seal count");
        facts.Fact(EvolutionOutcomeIds.TraitInstanceFact.Value);
        if (status == MemoryErasureSealUseStatus.Succeeded)
        {
            if (!facts.Success || after <= before
                || consumed != MemoryErasureSealItemRules.UseQuantity)
            {
                GameplayExperienceFacts.Fail("successful memory erasure transition");
            }
            return facts.Judge(GameplayExperienceCategory.Intense, "memory-erasure-applied",
                "기억 삭제가 실제 개정 증가로 반영되어 강렬한 정체성 변화로 평가함.");
        }
        if (status == MemoryErasureSealUseStatus.AlreadyCompleted)
        {
            if (!facts.Success || after != before || consumed != 0)
                GameplayExperienceFacts.Fail("already-completed memory erasure transition");
            return facts.Technical("memory-erasure-already-completed");
        }
        if (after != before || consumed != 0)
            GameplayExperienceFacts.Fail("non-successful memory erasure changed state");
        return facts.Technical("memory-erasure-not-applied");
    }
}

public sealed class ItemMemoryErasureSealBossAwardedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public ItemMemoryErasureSealBossAwardedExperiencePolicy()
        : base(EvolutionOutcomeIds.MemoryErasureBossAwarded.Value,
            "지역 보상 지급 기록") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, EvolutionOutcomeIds.AwardedRegionRole.Value);
        WholePositive(facts.Number(EvolutionOutcomeIds.AwardedItemCountMetric.Value,
            EvolutionOutcomeIds.CountUnit.Value), "awarded item count");
        return facts.Success
            ? facts.Technical("region-award-not-personal-experience")
            : facts.Technical("region-award-not-completed");
    }
}
