using System;

internal sealed class ExternalFactionOutcomeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) =>
        new(outcome.OutcomeTypeId.Value + ":"
            + NarrativeInferenceHash.ComputeSha256Utf8(
                subjectId.ToString()));

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay) =>
        new(
            1f,
            NarrativeMemoryTier.Core,
            Math.Max(evaluationDay + 30, outcome.AbsoluteDay + 30));
}

internal sealed class ExternalFactionOutcomePerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;

    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class OffenseTruthRevealOutcomeConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;

    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class OffenseTruthRevealPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        string target = outcome.ParticipantCount == 0
            ? "알 수 없는 원정 목표"
            : outcome.GetParticipant(0).DisplayName.DisplayText;
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            $"원정 목표: {target} · 결과: 진실 공개",
            "offense-truth-reveal-v1",
            true);
    }
}

public sealed class OffenseTruthRevealOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new OffenseTruthRevealPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new ExternalFactionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new ExternalFactionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new OffenseTruthRevealOutcomeConsolidator();

    public GameplayOutcomeTypeId OutcomeTypeId =>
        ExternalFactionOutcomeIds.TruthRevealed;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(ExternalFactionOutcomeIds.RevealedTargetRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(ExternalFactionOutcomeIds.TruthRevealedMetric)
        && unitId.Equals(ExternalFactionOutcomeIds.BooleanUnit);

    public OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.Status == GameplayOutcomeStatus.Succeeded
        && outcome.ParticipantCount == 1
        && outcome.SubjectCount == 1
        && outcome.MetricCount == 1
        && outcome.TagCount == 1
        && outcome.ProvenanceCount == 2
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "offense-truth-reveal-shape-invalid");
}
