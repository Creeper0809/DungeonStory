using System;

internal sealed class EvolutionOutcomeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
            outcome.OutcomeTypeId.Value + ":" + subjectId.Kind.Value);

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

        bool permanent = outcome.OutcomeTypeId ==
                EvolutionOutcomeIds.FacilityEvolutionCompleted
            || outcome.OutcomeTypeId == EvolutionOutcomeIds.MemoryErasureTerminal
                && outcome.Status == GameplayOutcomeStatus.Succeeded;
        NarrativeMemoryTier tier = permanent
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

internal sealed class EvolutionOutcomePerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;

    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class EvolutionOutcomeConsolidator : IOutcomeMemoryConsolidator
{
    private readonly bool compactable;

    public EvolutionOutcomeConsolidator(bool compactable) =>
        this.compactable = compactable;

    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => compactable;

    public bool IsAdditiveMetric(GameplayMetricId metricId) =>
        metricId.Equals(EvolutionOutcomeIds.TraitExperienceMetric)
        || metricId.Equals(EvolutionOutcomeIds.TraitMoodMetric);
}

internal sealed class ApparelChangePerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeMetric state = FindMetric(
            outcome,
            EvolutionOutcomeIds.ApparelEquippedMetric);
        string verb = state.Value >= 0.5d ? "착용했다" : "벗었다";
        string text = $"의복을 {verb}.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "apparel-change-v1",
            true);
    }

    private static GameplayOutcomeMetric FindMetric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId id)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(id))
                return metric;
        }
        return default;
    }
}

internal sealed class TraitReactionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeMetric metric = outcome.GetMetric(0);
        string effect = metric.MetricId.Equals(EvolutionOutcomeIds.TraitExperienceMetric)
            ? $"경험 {metric.Value:0.##}"
            : $"기분 변화 {metric.Value:0.##}";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            $"후천 특성 반응으로 {effect} 효과가 확정됐다.",
            "trait-reaction-v1",
            true);
    }
}

internal sealed class FacilityEvolutionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeMetric grade = FindMetric(
            outcome,
            EvolutionOutcomeIds.FacilityStarGradeMetric);
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            $"시설 진화가 {grade.Value:0}성으로 확정됐다.",
            "facility-evolution-v1",
            true);
    }

    private static GameplayOutcomeMetric FindMetric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId id)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(id))
                return metric;
        }
        return default;
    }
}

internal sealed class MemoryErasurePerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeMetric status = FindMetric(
            outcome,
            EvolutionOutcomeIds.MemoryErasureStatusMetric);
        MemoryErasureSealUseStatus value =
            (MemoryErasureSealUseStatus)(int)status.Value;
        string text = value == MemoryErasureSealUseStatus.Succeeded
            ? "망각의 인장을 사용해 선택한 후천 특성이 소거됐다."
            : value == MemoryErasureSealUseStatus.AlreadyCompleted
                ? "같은 망각의 인장 사용 결과가 이미 확정돼 있었다."
                : $"망각의 인장 사용이 {value} 상태로 끝났다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "memory-erasure-v1",
            true);
    }

    private static GameplayOutcomeMetric FindMetric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId id)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(id))
                return metric;
        }
        return default;
    }
}

public sealed class ApparelChangeOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new ApparelChangePerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new EvolutionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EvolutionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EvolutionOutcomeConsolidator(true);

    public GameplayOutcomeTypeId OutcomeTypeId => EvolutionOutcomeIds.ApparelChanged;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(EvolutionOutcomeIds.WearerRole);
    public bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId) =>
        metricId.Equals(EvolutionOutcomeIds.ApparelEquippedMetric)
            && unitId.Equals(EvolutionOutcomeIds.BooleanUnit)
        || metricId.Equals(EvolutionOutcomeIds.ApparelOriginMetric)
            && unitId.Equals(EvolutionOutcomeIds.EnumUnit);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 1
        && outcome.MetricCount == 2
        && outcome.SubjectCount == 1
        && outcome.TagCount == 1
        && outcome.AnchorCount == 0
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("apparel-change-shape-invalid");
}

public sealed class AcquiredTraitReactionOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new TraitReactionPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new EvolutionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EvolutionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EvolutionOutcomeConsolidator(true);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.TraitReactionExecuted;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(EvolutionOutcomeIds.TraitOwnerRole);
    public bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId) =>
        (metricId.Equals(EvolutionOutcomeIds.TraitExperienceMetric)
            || metricId.Equals(EvolutionOutcomeIds.TraitMoodMetric))
        && unitId.Equals(EvolutionOutcomeIds.EffectPointUnit);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 1
        && outcome.MetricCount == 1
        && outcome.SubjectCount == 1
        && outcome.TagCount == 1
        && outcome.AnchorCount == 2
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("trait-reaction-shape-invalid");
}

public sealed class FacilityEvolutionOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new FacilityEvolutionPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new EvolutionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EvolutionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EvolutionOutcomeConsolidator(false);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.FacilityEvolutionCompleted;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(EvolutionOutcomeIds.EvolvedFacilityRole);
    public bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId) =>
        metricId.Equals(EvolutionOutcomeIds.FacilityStarGradeMetric)
            && unitId.Equals(EvolutionOutcomeIds.GradeUnit)
        || (metricId.Equals(EvolutionOutcomeIds.MaterialQuantityMetric)
                || metricId.Equals(EvolutionOutcomeIds.MaterialSourceCountMetric))
            && unitId.Equals(EvolutionOutcomeIds.CountUnit)
        || metricId.Equals(EvolutionOutcomeIds.MaterialMassMetric)
            && unitId.Equals(EvolutionOutcomeIds.GramUnit);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 1
        && outcome.MetricCount == 4
        && outcome.SubjectCount == 1
        && outcome.TagCount == 1
        && outcome.AnchorCount is 0 or 2
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("facility-evolution-shape-invalid");
}

public sealed class MemoryErasureOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new MemoryErasurePerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new EvolutionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EvolutionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EvolutionOutcomeConsolidator(false);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.MemoryErasureTerminal;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(EvolutionOutcomeIds.ErasureTargetRole);
    public bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId) =>
        metricId.Equals(EvolutionOutcomeIds.MemoryErasureStatusMetric)
            && unitId.Equals(EvolutionOutcomeIds.EnumUnit)
        || (metricId.Equals(EvolutionOutcomeIds.PreviousRevisionMetric)
                || metricId.Equals(EvolutionOutcomeIds.CommittedRevisionMetric))
            && unitId.Equals(EvolutionOutcomeIds.RevisionUnit)
        || metricId.Equals(EvolutionOutcomeIds.ConsumedSealMetric)
            && unitId.Equals(EvolutionOutcomeIds.CountUnit);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 1
        && outcome.MetricCount == 4
        && outcome.SubjectCount == 1
        && outcome.TagCount == 1
        && outcome.AnchorCount is 1 or 2
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("memory-erasure-shape-invalid");
}
