using System;

internal sealed class ProductionCombatOutcomeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) =>
        new GameplayMemorySignature(outcome.OutcomeTypeId.Value);

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        float salience = 0f;
        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink link = outcome.GetSubject(index);
            if (link.SubjectId == subjectId)
            {
                salience = link.Salience;
                break;
            }
        }

        NarrativeMemoryTier tier = salience >= 0.9f
            ? NarrativeMemoryTier.Core
            : priorMatchingCount >= 3
                ? NarrativeMemoryTier.Compacted
                : NarrativeMemoryTier.Episodic;
        return new OutcomeMemoryEvaluation(
            salience,
            tier,
            Math.Max(evaluationDay + 1, outcome.AbsoluteDay + 1));
    }
}

internal sealed class ProductionCombatOutcomePerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;

    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class ProductionOutcomeConsolidator : IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => true;

    public bool IsAdditiveMetric(GameplayMetricId metricId) =>
        metricId.Equals(ProductionCombatOutcomeIds.ProducedQuantityMetric)
        || metricId.Equals(ProductionCombatOutcomeIds.ProducedMassMetric)
        || metricId.Equals(ProductionCombatOutcomeIds.DeclaredLossMetric)
        || metricId.Equals(ProductionCombatOutcomeIds.DeclaredExternalInputMetric)
        || metricId.Equals(ProductionCombatOutcomeIds.WipInputQuantityMetric)
        || metricId.Equals(ProductionCombatOutcomeIds.WipInputMassMetric)
        || metricId.Equals(ProductionCombatOutcomeIds.CleanWaterMetric)
        || metricId.Equals(ProductionCombatOutcomeIds.WastewaterMetric);
}

internal sealed class CombatDamageOutcomeConsolidator : IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant participant = outcome.GetParticipant(index);
            if (participant.EntityId == subjectId
                && participant.RoleId.Equals(ProductionCombatOutcomeIds.VictimRole))
            {
                return false;
            }
        }
        return true;
    }

    public bool IsAdditiveMetric(GameplayMetricId metricId) =>
        metricId.Equals(ProductionCombatOutcomeIds.DamageMetric);
}

internal sealed class ProductionCompletedPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        double quantity = FindMetric(
            outcome,
            ProductionCombatOutcomeIds.ProducedQuantityMetric);
        double mass = FindMetric(
            outcome,
            ProductionCombatOutcomeIds.ProducedMassMetric);
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            $"생산 작업이 완료되어 {quantity:0}개, {mass:0}그램의 결과물이 확정됐다.",
            "production-completed-v1",
            true);
    }

    private static double FindMetric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId metricId)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(metricId))
                return metric.Value;
        }
        return 0d;
    }
}

internal sealed class CombatDamagePerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayEntityId attacker = default;
        GameplayEntityId victim = default;
        double damage = 0d;
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant participant = outcome.GetParticipant(index);
            if (participant.RoleId.Equals(ProductionCombatOutcomeIds.AttackerRole))
                attacker = participant.EntityId;
            else if (participant.RoleId.Equals(ProductionCombatOutcomeIds.VictimRole))
                victim = participant.EntityId;
        }
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(ProductionCombatOutcomeIds.DamageMetric))
            {
                damage = metric.Value;
                break;
            }
        }

        string text = perspective.Kind == NarrativePerspectiveKind.Character
            && perspective.ViewerId == attacker
                ? $"공격으로 상대에게 {damage:0.##}의 실제 피해를 입혔다."
                : perspective.Kind == NarrativePerspectiveKind.Character
                    && perspective.ViewerId == victim
                        ? $"상대의 공격으로 {damage:0.##}의 실제 피해를 입었다."
                        : $"공격이 대상에게 {damage:0.##}의 실제 피해를 남겼다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "combat-damage-v1",
            true);
    }
}

public sealed class ProductionCompletedOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new ProductionCompletedPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new ProductionCombatOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new ProductionCombatOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new ProductionOutcomeConsolidator();

    public GameplayOutcomeTypeId OutcomeTypeId =>
        ProductionCombatOutcomeIds.ProductionCompleted;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(ProductionCombatOutcomeIds.ProducerRole)
        || roleId.Equals(ProductionCombatOutcomeIds.WorkerRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(ProductionCombatOutcomeIds.ProducedQuantityMetric)
            && unitId.Equals(ProductionCombatOutcomeIds.CountUnit)
        || metricId.Equals(ProductionCombatOutcomeIds.WipInputQuantityMetric)
            && unitId.Equals(ProductionCombatOutcomeIds.CountUnit)
        || (metricId.Equals(ProductionCombatOutcomeIds.ProducedMassMetric)
                || metricId.Equals(ProductionCombatOutcomeIds.DeclaredLossMetric)
                || metricId.Equals(ProductionCombatOutcomeIds.DeclaredExternalInputMetric)
                || metricId.Equals(ProductionCombatOutcomeIds.WipInputMassMetric)
                || metricId.Equals(ProductionCombatOutcomeIds.CleanWaterMetric)
                || metricId.Equals(ProductionCombatOutcomeIds.WastewaterMetric))
            && unitId.Equals(ProductionCombatOutcomeIds.GramUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount is 1 or 2
        && outcome.SubjectCount == outcome.ParticipantCount
        && outcome.MetricCount >= 10
        && (outcome.MetricCount - 8) % 2 == 0
        && outcome.TagCount == 1
        && outcome.ProvenanceCount >= 9
        && outcome.FactCount == 0
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("production-completed-shape-invalid");
}

public sealed class CombatDamageOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new CombatDamagePerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new ProductionCombatOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new ProductionCombatOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new CombatDamageOutcomeConsolidator();

    public GameplayOutcomeTypeId OutcomeTypeId =>
        ProductionCombatOutcomeIds.CombatDamageResolved;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(ProductionCombatOutcomeIds.AttackerRole)
        || roleId.Equals(ProductionCombatOutcomeIds.VictimRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(ProductionCombatOutcomeIds.DamageMetric)
        && unitId.Equals(ProductionCombatOutcomeIds.HealthPointUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 2
        && outcome.SubjectCount == 2
        && outcome.MetricCount == 1
        && outcome.TagCount == 1
        && outcome.ProvenanceCount == 1
        && outcome.FactCount == 2
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("combat-damage-shape-invalid");
}
