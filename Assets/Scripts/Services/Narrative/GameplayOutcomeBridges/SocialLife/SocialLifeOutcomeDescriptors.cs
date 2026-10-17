using System;
using DungeonStory.Narrative.Korean;

internal sealed class SocialLifeOutcomeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId)
    {
        string semantic = outcome.OutcomeTypeId.Value;
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(SocialLifeOutcomeIds.ConflictKindMetric)
                || metric.MetricId.Equals(SocialLifeOutcomeIds.OffenseKindMetric))
            {
                semantic += ":" + metric.DefinitionOrInstanceId.Value;
                break;
            }
        }
        return new GameplayMemorySignature(semantic);
    }

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        float baseSalience = 0f;
        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink link = outcome.GetSubject(index);
            if (link.SubjectId == subjectId)
            {
                baseSalience = link.Salience;
                break;
            }
        }

        float magnitude = 0f;
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(SocialLifeOutcomeIds.SeverityMetric))
                magnitude = Math.Max(magnitude, (float)Math.Min(1d, Math.Abs(metric.Value) / 10d));
            if (metric.MetricId.Equals(SocialLifeOutcomeIds.RestitutionMetric)
                && metric.Value > 0d)
                magnitude = Math.Max(magnitude, 0.5f);
        }

        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float novelty = priorMatchingCount == 0 ? 0.08f : 0f;
        float relationship = magnitude * 0.12f;
        float causal = outcome.Causation.HasParent ? 0.05f : 0f;
        float repetition = Math.Min(0.3f, priorMatchingCount * 0.04f);
        float ageDecay = Math.Min(0.3f, age * 0.01f);
        float salience = Math.Clamp(
            baseSalience + novelty + relationship + causal - repetition - ageDecay,
            0f,
            1f);
        NarrativeMemoryTier tier = salience >= 0.9f
            ? NarrativeMemoryTier.Core
            : salience >= 0.42f
                ? NarrativeMemoryTier.Episodic
                : NarrativeMemoryTier.Recent;
        return new OutcomeMemoryEvaluation(
            salience,
            tier,
            Math.Max(evaluationDay + 2, outcome.AbsoluteDay + 2));
    }
}

internal sealed class SocialLifeOutcomePerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class SocialConflictOutcomeConsolidator : IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;

    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class ApologyOutcomeConsolidator : IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal abstract class SocialLifePerspectiveProjectorBase :
    INarrativePerspectiveProjector
{
    private readonly IKoreanJosaFormatter josa;

    protected SocialLifePerspectiveProjectorBase(IKoreanJosaFormatter josa)
    {
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));
    }

    protected string RendererVersion => "social-life-v1+" + josa.FormatterVersion;

    protected bool TryWithJosa(
        in KoreanNameSnapshot name,
        KoreanJosaKind kind,
        out string text)
    {
        KoreanJosaFormatResult result = josa.Format(new KoreanJosaRequest(name, kind));
        text = result.Text;
        return !result.RequiresNeutralFrame;
    }

    protected static bool TryParticipant(
        in GameplayOutcomeReadView outcome,
        GameplayRoleId role,
        out GameplayOutcomeParticipant participant)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant current = outcome.GetParticipant(index);
            if (current.RoleId.Equals(role))
            {
                participant = current;
                return true;
            }
        }
        participant = default;
        return false;
    }

    protected static double Metric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId id)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(id))
                return metric.Value;
        }
        return 0d;
    }

    public abstract NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective);
}

internal sealed class SocialConflictPerspectiveProjector :
    SocialLifePerspectiveProjectorBase
{
    public SocialConflictPerspectiveProjector(IKoreanJosaFormatter josa)
        : base(josa) { }

    public override NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        TryParticipant(outcome, SocialLifeOutcomeIds.InstigatorRole, out var instigator);
        TryParticipant(outcome, SocialLifeOutcomeIds.TargetRole, out var target);
        double severity = Metric(outcome, SocialLifeOutcomeIds.SeverityMetric);
        bool neutral;
        string text;
        if (perspective.Kind == NarrativePerspectiveKind.Character
            && perspective.ViewerId == instigator.EntityId)
        {
            neutral = !TryWithJosa(target.DisplayName, KoreanJosaKind.Object, out string targetObject);
            text = neutral
                ? $"대상: {target.DisplayName.DisplayText} · 사회적 충돌 강도: {severity:0.##}"
                : $"{targetObject} 상대로 사회적 충돌을 일으켰다. 강도는 {severity:0.##}였다.";
        }
        else if (perspective.Kind == NarrativePerspectiveKind.Character
            && perspective.ViewerId == target.EntityId)
        {
            neutral = !TryWithJosa(instigator.DisplayName, KoreanJosaKind.Subject, out string instigatorSubject);
            text = neutral
                ? $"상대: {instigator.DisplayName.DisplayText} · 사회적 충돌 강도: {severity:0.##}"
                : $"{instigatorSubject} 일으킨 사회적 충돌을 겪었다. 강도는 {severity:0.##}였다.";
        }
        else
        {
            bool left = TryWithJosa(instigator.DisplayName, KoreanJosaKind.Subject, out string instigatorSubject);
            bool right = TryWithJosa(target.DisplayName, KoreanJosaKind.Comitative, out string targetWith);
            neutral = !left || !right;
            text = neutral
                ? $"당사자: {instigator.DisplayName.DisplayText} / {target.DisplayName.DisplayText} · 사회적 충돌 강도: {severity:0.##}"
                : $"{instigatorSubject} {targetWith} 사회적 충돌을 빚었다. 강도는 {severity:0.##}였다.";
        }
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }
}

internal sealed class ApologyPerspectiveProjector :
    SocialLifePerspectiveProjectorBase
{
    public ApologyPerspectiveProjector(IKoreanJosaFormatter josa)
        : base(josa) { }

    public override NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        TryParticipant(outcome, SocialLifeOutcomeIds.OffenderRole, out var offender);
        TryParticipant(outcome, SocialLifeOutcomeIds.RecipientRole, out var recipient);
        bool restitution = Metric(outcome, SocialLifeOutcomeIds.RestitutionMetric) > 0d;
        bool neutral;
        string text;
        if (perspective.Kind == NarrativePerspectiveKind.Character
            && perspective.ViewerId == offender.EntityId)
        {
            neutral = !TryWithJosa(recipient.DisplayName, KoreanJosaKind.Object, out string recipientObject);
            text = neutral
                ? $"사과 대상: {recipient.DisplayName.DisplayText} · 보상: {(restitution ? "있음" : "없음")}"
                : $"{recipientObject} 찾아 사과했고 관계의 상처가 해소됐다."
                    + (restitution ? " 보상도 함께 건넸다." : string.Empty);
        }
        else if (perspective.Kind == NarrativePerspectiveKind.Character
            && perspective.ViewerId == recipient.EntityId)
        {
            neutral = !TryWithJosa(offender.DisplayName, KoreanJosaKind.Subject, out string offenderSubject);
            text = neutral
                ? $"사과한 인물: {offender.DisplayName.DisplayText} · 보상: {(restitution ? "있음" : "없음")}"
                : $"{offenderSubject} 한 사과를 받아들여 관계의 상처를 해소했다."
                    + (restitution ? " 보상도 받았다." : string.Empty);
        }
        else
        {
            bool left = TryWithJosa(offender.DisplayName, KoreanJosaKind.Subject, out string offenderSubject);
            bool right = TryWithJosa(recipient.DisplayName, KoreanJosaKind.Object, out string recipientObject);
            neutral = !left || !right;
            text = neutral
                ? $"사과: {offender.DisplayName.DisplayText} → {recipient.DisplayName.DisplayText} · 보상: {(restitution ? "있음" : "없음")}"
                : $"{offenderSubject} {recipientObject} 찾아 사과했고, 사과가 받아들여졌다."
                    + (restitution ? " 보상도 함께 전달됐다." : string.Empty);
        }
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }
}

public sealed class SocialConflictOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory = new SocialLifeOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception = new SocialLifeOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator = new SocialConflictOutcomeConsolidator();

    public SocialConflictOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new SocialConflictPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId => SocialLifeOutcomeIds.ConflictResolved;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(SocialLifeOutcomeIds.InstigatorRole)
        || roleId.Equals(SocialLifeOutcomeIds.TargetRole)
        || roleId.Equals(SocialLifeOutcomeIds.CustomerRole)
        || roleId.Equals(SocialLifeOutcomeIds.VenueRole);
    public bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId) =>
        (metricId.Equals(SocialLifeOutcomeIds.SeverityMetric)
            || metricId.Equals(SocialLifeOutcomeIds.MoodDeltaMetric))
            && unitId.Equals(SocialLifeOutcomeIds.PointUnit)
        || metricId.Equals(SocialLifeOutcomeIds.OriginMetric)
            && unitId.Equals(SocialLifeOutcomeIds.EnumUnit)
        || metricId.Equals(SocialLifeOutcomeIds.VisitorReceiptDayMetric)
            && unitId.Equals(SocialLifeOutcomeIds.DayUnit)
        || (metricId.Equals(SocialLifeOutcomeIds.ConflictKindMetric)
                || metricId.Equals(SocialLifeOutcomeIds.InstigatorCultureMetric)
                || metricId.Equals(SocialLifeOutcomeIds.TargetCultureMetric))
            && unitId.Equals(SocialLifeOutcomeIds.BooleanUnit);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount is 2 or 4
        && outcome.MetricCount is 4 or 7
        && outcome.SubjectCount is 2 or 3
        && outcome.TagCount is 2 or 3
        && outcome.AnchorCount == 0
        && outcome.ProvenanceCount == 1
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("social-conflict-shape-invalid");
}

public sealed class ApologyOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory = new SocialLifeOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception = new SocialLifeOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator = new ApologyOutcomeConsolidator();

    public ApologyOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new ApologyPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId => SocialLifeOutcomeIds.ApologyResolved;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(SocialLifeOutcomeIds.OffenderRole)
        || roleId.Equals(SocialLifeOutcomeIds.RecipientRole);
    public bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId) =>
        metricId.Equals(SocialLifeOutcomeIds.MoodDeltaMetric)
            && unitId.Equals(SocialLifeOutcomeIds.PointUnit)
        || (metricId.Equals(SocialLifeOutcomeIds.RestitutionMetric)
                || metricId.Equals(SocialLifeOutcomeIds.OffenseKindMetric))
            && unitId.Equals(SocialLifeOutcomeIds.BooleanUnit);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 2
        && outcome.MetricCount == 3
        && outcome.SubjectCount == 2
        && outcome.TagCount == 2
        && outcome.AnchorCount == 0
        && outcome.ProvenanceCount == 1
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("apology-shape-invalid");
}
