using System;
using DungeonStory.Narrative.Korean;

internal sealed class EnvironmentOutcomeMemoryPolicy : IOutcomeMemoryPolicy
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
        float baseSalience = 0f;
        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink subject = outcome.GetSubject(index);
            if (subject.SubjectId == subjectId)
            {
                baseSalience = subject.Salience;
                break;
            }
        }
        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float salience = Math.Clamp(
            baseSalience
                + (priorMatchingCount == 0 ? 0.08f : 0f)
                + (outcome.Causation.HasParent ? 0.04f : 0f)
                - Math.Min(0.28f, priorMatchingCount * 0.035f)
                - Math.Min(0.35f, age * 0.012f),
            0f,
            1f);
        NarrativeMemoryTier tier = salience >= 0.88f
            ? NarrativeMemoryTier.Core
            : salience >= 0.4f
                ? NarrativeMemoryTier.Episodic
                : NarrativeMemoryTier.Recent;
        return new OutcomeMemoryEvaluation(
            salience,
            tier,
            Math.Max(evaluationDay + 2, outcome.AbsoluteDay + 2));
    }
}

internal sealed class EnvironmentOutcomePerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class ExactEnvironmentOutcomeConsolidator :
    IOutcomeMemoryConsolidator,
    IOutcomeCompactionContract
{
    public bool SupportsCompaction => false;
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
    public bool CanMerge(
        in CompactedNarrativeMemoryReadView existing,
        in GameplayOutcomeReadView incoming,
        GameplayEntityId subjectId) => false;
}

internal sealed class EnvironmentPerspectiveProjector : INarrativePerspectiveProjector
{
    private readonly IKoreanJosaFormatter josa;
    private readonly string eventLabel;
    private readonly GameplayRoleId narrativeSubjectRole;
    private readonly KoreanJosaKind narrativeSubjectJosa;
    private readonly string globalSuffix;
    private readonly string viewerFrame;

    public EnvironmentPerspectiveProjector(
        IKoreanJosaFormatter josa,
        string eventLabel,
        GameplayRoleId narrativeSubjectRole,
        KoreanJosaKind narrativeSubjectJosa,
        string globalSuffix,
        string viewerFrame)
    {
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));
        this.eventLabel = eventLabel ?? string.Empty;
        this.narrativeSubjectRole = narrativeSubjectRole;
        this.narrativeSubjectJosa = narrativeSubjectJosa;
        this.globalSuffix = globalSuffix ?? string.Empty;
        this.viewerFrame = viewerFrame ?? string.Empty;
    }

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant actor = FindParticipant(
            outcome,
            narrativeSubjectRole);
        KoreanJosaFormatResult formatted = josa.Format(new KoreanJosaRequest(
            actor.DisplayName,
            narrativeSubjectJosa));
        bool neutral = formatted.RequiresNeutralFrame;
        string summary = FindFact(outcome, EnvironmentOutcomeIds.SummaryFact);
        bool isSubjectViewer = perspective.ViewerId == actor.EntityId;
        string text = isSubjectViewer
            ? viewerFrame
            : neutral
                ? eventLabel + " · " + actor.DisplayName.DisplayText
                : formatted.Text + globalSuffix;
        if (!string.IsNullOrWhiteSpace(summary))
            text += " " + summary;
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "environment-v1+" + josa.FormatterVersion,
            neutral);
    }

    private static GameplayOutcomeParticipant FindParticipant(
        in GameplayOutcomeReadView outcome,
        GameplayRoleId role)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant participant = outcome.GetParticipant(index);
            if (participant.RoleId.Equals(role))
                return participant;
        }
        return default;
    }

    private static string FindFact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId id)
    {
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(id))
                return fact.Value;
        }
        return string.Empty;
    }
}

public readonly struct EnvironmentMetricRule
{
    public EnvironmentMetricRule(
        GameplayMetricId id,
        GameplayMetricUnitId unit,
        double minimum,
        double maximum,
        bool integral)
    {
        Id = id;
        Unit = unit;
        Minimum = minimum;
        Maximum = maximum;
        Integral = integral;
    }
    public GameplayMetricId Id { get; }
    public GameplayMetricUnitId Unit { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public bool Integral { get; }

    public bool IsValid(double value) =>
        double.IsFinite(value)
        && value >= Minimum
        && value <= Maximum
        && (!Integral || value == Math.Truncate(value));
}

public abstract class EnvironmentOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private static readonly IOutcomeMemoryPolicy Memory = new EnvironmentOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception = new EnvironmentOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator = new ExactEnvironmentOutcomeConsolidator();
    private readonly GameplayRoleId[] roles;
    private readonly EnvironmentMetricRule[] metrics;
    private readonly GameplayOutcomeFactId[] facts;
    private readonly GameplayOutcomeTagId requiredTag;
    private readonly GameplayOutcomeStatus allowedStatus;
    private readonly GameplayOutcomeStatus alternateStatus;
    private readonly INarrativePerspectiveProjector projector;

    protected EnvironmentOutcomeDescriptor(
        IKoreanJosaFormatter josa,
        string eventLabel,
        GameplayRoleId narrativeSubjectRole,
        KoreanJosaKind narrativeSubjectJosa,
        string globalSuffix,
        string viewerFrame,
        GameplayOutcomeStatus allowedStatus,
        GameplayOutcomeStatus alternateStatus,
        GameplayOutcomeTagId requiredTag,
        GameplayRoleId[] roles,
        EnvironmentMetricRule[] metrics,
        GameplayOutcomeFactId[] facts)
    {
        this.requiredTag = requiredTag;
        this.allowedStatus = allowedStatus;
        this.alternateStatus = alternateStatus;
        this.roles = roles ?? Array.Empty<GameplayRoleId>();
        this.metrics = metrics ?? Array.Empty<EnvironmentMetricRule>();
        this.facts = facts ?? Array.Empty<GameplayOutcomeFactId>();
        projector = new EnvironmentPerspectiveProjector(
            josa,
            eventLabel,
            narrativeSubjectRole,
            narrativeSubjectJosa,
            globalSuffix,
            viewerFrame);
    }

    public abstract GameplayOutcomeTypeId OutcomeTypeId { get; }
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId)
    {
        for (int index = 0; index < roles.Length; index++)
        {
            if (roles[index].Equals(roleId))
                return true;
        }
        return false;
    }

    public bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId)
    {
        for (int index = 0; index < metrics.Length; index++)
        {
            if (metrics[index].Id.Equals(metricId)
                && metrics[index].Unit.Equals(unitId))
                return true;
        }
        return false;
    }

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != OutcomeTypeId
            || outcome.Status != allowedStatus
                && outcome.Status != alternateStatus
            || outcome.ParticipantCount == 0
            || outcome.ParticipantCount != roles.Length
            || outcome.MetricCount != metrics.Length
            || outcome.SubjectCount == 0
            || outcome.TagCount != 1
            || !outcome.GetTag(0).Equals(requiredTag)
            || outcome.AnchorCount != 0
            || outcome.ProvenanceCount == 0
            || outcome.FactCount != facts.Length)
            return OutcomeValidationResult.Reject(
                OutcomeTypeId.Value + "-semantic-shape-invalid");
        for (int roleIndex = 0; roleIndex < roles.Length; roleIndex++)
        {
            int count = 0;
            for (int participantIndex = 0;
                 participantIndex < outcome.ParticipantCount;
                 participantIndex++)
            {
                if (outcome.GetParticipant(participantIndex).RoleId.Equals(roles[roleIndex]))
                    count++;
            }
            if (count != 1)
                return OutcomeValidationResult.Reject(
                    OutcomeTypeId.Value + "-required-role-shape-invalid");
        }
        for (int ruleIndex = 0; ruleIndex < metrics.Length; ruleIndex++)
        {
            int count = 0;
            for (int metricIndex = 0; metricIndex < outcome.MetricCount; metricIndex++)
            {
                GameplayOutcomeMetric metric = outcome.GetMetric(metricIndex);
                if (metric.MetricId.Equals(metrics[ruleIndex].Id)
                    && metric.UnitId.Equals(metrics[ruleIndex].Unit)
                    && metrics[ruleIndex].IsValid(metric.Value))
                    count++;
            }
            if (count != 1)
                return OutcomeValidationResult.Reject(
                    OutcomeTypeId.Value + "-required-metric-shape-invalid");
        }
        for (int ruleIndex = 0; ruleIndex < facts.Length; ruleIndex++)
        {
            int count = 0;
            for (int factIndex = 0; factIndex < outcome.FactCount; factIndex++)
            {
                if (outcome.GetFact(factIndex).FactId.Equals(facts[ruleIndex]))
                    count++;
            }
            if (count != 1)
                return OutcomeValidationResult.Reject(
                    OutcomeTypeId.Value + "-required-fact-shape-invalid");
        }
        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink subject = outcome.GetSubject(index);
            if (subject.IsOptionalWitness || !HasParticipant(outcome, subject.SubjectId))
                return OutcomeValidationResult.Reject(
                    OutcomeTypeId.Value + "-subject-not-direct-participant");
        }
        return ValidateAdditional(outcome);
    }

    protected virtual OutcomeValidationResult ValidateAdditional(
        in GameplayOutcomeReadView outcome) => OutcomeValidationResult.Accepted;

    private static bool HasParticipant(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId id)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            if (outcome.GetParticipant(index).EntityId == id)
                return true;
        }
        return false;
    }

    protected static EnvironmentMetricRule Metric(
        GameplayMetricId id,
        GameplayMetricUnitId unit,
        double minimum = double.MinValue,
        double maximum = double.MaxValue,
        bool integral = false) => new(
        id,
        unit,
        minimum,
        maximum,
        integral);

    protected static double FindMetric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId id)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(id))
                return metric.Value;
        }
        return double.NaN;
    }
}

public sealed class CertifiedSeedCompletionOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public CertifiedSeedCompletionOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "인증 종자 생산", EnvironmentOutcomeIds.FacilityRole,
        KoreanJosaKind.Topic, " 인증 종자 생산을 마쳤다.", "인증 종자 생산을 마쳤다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.AgricultureTag,
        new[] { EnvironmentOutcomeIds.FacilityRole, EnvironmentOutcomeIds.CropRole, EnvironmentOutcomeIds.ItemRole },
        new[] { Metric(EnvironmentOutcomeIds.QuantityMetric, EnvironmentOutcomeIds.CountUnit, 1d, int.MaxValue, true), Metric(EnvironmentOutcomeIds.MassMetric, EnvironmentOutcomeIds.GramUnit, 1d, long.MaxValue, true), Metric(EnvironmentOutcomeIds.SequenceMetric, EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true) },
        new[] { EnvironmentOutcomeIds.ReceiptKindFact, EnvironmentOutcomeIds.SourceDigestFact, EnvironmentOutcomeIds.CommitFact, EnvironmentOutcomeIds.FingerprintFact, EnvironmentOutcomeIds.CorrelationFact, EnvironmentOutcomeIds.SummaryFact }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.CertifiedSeedCompleted;
}

public sealed class CropPlanOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public CropPlanOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "작물 재배", EnvironmentOutcomeIds.FacilityRole,
        KoreanJosaKind.Topic, " 작물 재배 주기를 마쳤다.", "작물 재배 주기를 마쳤다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Failed,
        EnvironmentOutcomeIds.AgricultureTag,
        new[] { EnvironmentOutcomeIds.FacilityRole, EnvironmentOutcomeIds.CropRole },
        new[] { Metric(EnvironmentOutcomeIds.InputQuantityMetric, EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true), Metric(EnvironmentOutcomeIds.InputMassMetric, EnvironmentOutcomeIds.GramUnit, 0d, long.MaxValue, true), Metric(EnvironmentOutcomeIds.OutputQuantityMetric, EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true), Metric(EnvironmentOutcomeIds.OutputMassMetric, EnvironmentOutcomeIds.GramUnit, 0d, long.MaxValue, true), Metric(EnvironmentOutcomeIds.StatusMetric, EnvironmentOutcomeIds.EnumUnit, 2d, 4d, true) },
        new[] { EnvironmentOutcomeIds.ReceiptKindFact, EnvironmentOutcomeIds.SourceDigestFact, EnvironmentOutcomeIds.InputDigestFact, EnvironmentOutcomeIds.OutputDigestFact, EnvironmentOutcomeIds.CommitFact, EnvironmentOutcomeIds.FingerprintFact, EnvironmentOutcomeIds.IndoorFact, EnvironmentOutcomeIds.CorrelationFact, EnvironmentOutcomeIds.SummaryFact }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.CropPlanTerminal;

    protected override OutcomeValidationResult ValidateAdditional(
        in GameplayOutcomeReadView outcome)
    {
        double status = FindMetric(outcome, EnvironmentOutcomeIds.StatusMetric);
        bool valid = status == 2d
            ? outcome.Status == GameplayOutcomeStatus.Succeeded
            : (status == 3d || status == 4d)
                && outcome.Status == GameplayOutcomeStatus.Failed;
        return valid
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                OutcomeTypeId.Value + "-terminal-status-mismatch");
    }
}

public sealed class CropIrrigationSupplyOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public CropIrrigationSupplyOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "농지 관개", EnvironmentOutcomeIds.FacilityRole,
        KoreanJosaKind.Topic, " 깨끗한 물을 공급받았다.", "깨끗한 물을 공급받았다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.AgricultureTag,
        new[] { EnvironmentOutcomeIds.FacilityRole, EnvironmentOutcomeIds.ActorRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.WaterUnitsMetric, EnvironmentOutcomeIds.PointUnit, double.Epsilon),
            Metric(EnvironmentOutcomeIds.WaterQualityMetric, EnvironmentOutcomeIds.EnumUnit, 0d, int.MaxValue, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.CropIrrigationSupplied;
}

public sealed class FireIgnitionOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public FireIgnitionOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "화재 발화 판정", EnvironmentOutcomeIds.FacilityRole,
        KoreanJosaKind.Topic, " 화재 발화 판정을 받았다.", "화재 발화 판정을 받았다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Failed,
        EnvironmentOutcomeIds.DisasterTag,
        new[] { EnvironmentOutcomeIds.FacilityRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.StatusMetric, EnvironmentOutcomeIds.EnumUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.IntensityAfterMetric, EnvironmentOutcomeIds.RatioUnit, 0d, 1d)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.ReasonFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.FireIgnition;
}

public sealed class FireSuppressionOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public FireSuppressionOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "화재 진압 결과", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Subject, " 화재를 진압했다.", "화재를 진압했다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.DisasterTag,
        new[] { EnvironmentOutcomeIds.ActorRole, EnvironmentOutcomeIds.FireRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.IntensityBeforeMetric, EnvironmentOutcomeIds.RatioUnit, double.Epsilon, 1d),
            Metric(EnvironmentOutcomeIds.IntensityAfterMetric, EnvironmentOutcomeIds.RatioUnit, 0d, 1d),
            Metric(EnvironmentOutcomeIds.QuantityMetric, EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.StatusMetric, EnvironmentOutcomeIds.EnumUnit, 0d, int.MaxValue, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.ReasonFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.FireSuppression;
}

public sealed class FireDamageOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public FireDamageOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "화재 피해", EnvironmentOutcomeIds.FireRole,
        KoreanJosaKind.Subject, " 피해를 입혔다.", "화재 피해가 발생했다.",
        GameplayOutcomeStatus.Failed, GameplayOutcomeStatus.Failed,
        EnvironmentOutcomeIds.DisasterTag,
        new[] { EnvironmentOutcomeIds.FireRole, EnvironmentOutcomeIds.TargetRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.DamageMetric,
                EnvironmentOutcomeIds.PointUnit, double.Epsilon),
            Metric(EnvironmentOutcomeIds.IntensityBeforeMetric,
                EnvironmentOutcomeIds.RatioUnit, double.Epsilon, 1d),
            Metric(EnvironmentOutcomeIds.StatusMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, 1d, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.FireDamage;
}

public sealed class FireFuelOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public FireFuelOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "화재 연료 소실", EnvironmentOutcomeIds.ItemRole,
        KoreanJosaKind.Subject, " 화재로 소실됐다.", "화재로 소실됐다.",
        GameplayOutcomeStatus.Failed, GameplayOutcomeStatus.Failed,
        EnvironmentOutcomeIds.DisasterTag,
        new[] { EnvironmentOutcomeIds.ItemRole },
        new[] { Metric(EnvironmentOutcomeIds.QuantityMetric, EnvironmentOutcomeIds.CountUnit, 1d, int.MaxValue, true), Metric(EnvironmentOutcomeIds.MassMetric, EnvironmentOutcomeIds.GramUnit, 1d, long.MaxValue, true) },
        new[] { EnvironmentOutcomeIds.ReceiptKindFact, EnvironmentOutcomeIds.SourceDigestFact, EnvironmentOutcomeIds.CommitFact, EnvironmentOutcomeIds.CorrelationFact, EnvironmentOutcomeIds.SummaryFact }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.FireFuelLoss;
}

public sealed class FireWaterOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public FireWaterOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "화재 진압", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Subject, " 화재를 진압했다.", "화재를 진압했다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.DisasterTag,
        new[] { EnvironmentOutcomeIds.ActorRole, EnvironmentOutcomeIds.ItemRole },
        new[] { Metric(EnvironmentOutcomeIds.QuantityMetric, EnvironmentOutcomeIds.CountUnit, 1d, int.MaxValue, true), Metric(EnvironmentOutcomeIds.MassMetric, EnvironmentOutcomeIds.GramUnit, 1d, long.MaxValue, true) },
        new[] { EnvironmentOutcomeIds.ReceiptKindFact, EnvironmentOutcomeIds.SourceDigestFact, EnvironmentOutcomeIds.CommitFact, EnvironmentOutcomeIds.CorrelationFact, EnvironmentOutcomeIds.SummaryFact }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.FireWaterConsumed;
}

public sealed class DiseaseExposureOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public DiseaseExposureOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "질병 경로 노출", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Subject, " 질병 경로에 노출됐다.", "질병 경로에 노출됐다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.DiseaseTag,
        new[] { EnvironmentOutcomeIds.ActorRole, EnvironmentOutcomeIds.DiseaseRole },
        new[] { Metric(EnvironmentOutcomeIds.ExposureHoursMetric, EnvironmentOutcomeIds.HourUnit, double.Epsilon), Metric(EnvironmentOutcomeIds.EnvironmentCoefficientMetric, EnvironmentOutcomeIds.RatioUnit, double.Epsilon), Metric(EnvironmentOutcomeIds.SequenceMetric, EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true) },
        new[] { EnvironmentOutcomeIds.ReceiptKindFact, EnvironmentOutcomeIds.RouteFact, EnvironmentOutcomeIds.DetailFact, EnvironmentOutcomeIds.SummaryFact }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.DiseaseRouteExposure;
}

public sealed class ProcessAccidentOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public ProcessAccidentOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "작업 사고", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Subject, " 작업 사고로 다쳤다.", "작업 사고로 다쳤다.",
        GameplayOutcomeStatus.Failed, GameplayOutcomeStatus.Failed,
        EnvironmentOutcomeIds.DisasterTag,
        new[] { EnvironmentOutcomeIds.ActorRole, EnvironmentOutcomeIds.FacilityRole },
        new[] { Metric(EnvironmentOutcomeIds.DamageMetric, EnvironmentOutcomeIds.PointUnit, double.Epsilon) },
        new[] { EnvironmentOutcomeIds.ReceiptKindFact, EnvironmentOutcomeIds.WorkTypeFact, EnvironmentOutcomeIds.AnatomyNodeFact, EnvironmentOutcomeIds.ReasonFact, EnvironmentOutcomeIds.SummaryFact }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.ProcessAccident;
}

public sealed class RoomConditionOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public RoomConditionOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "방 상태 변화", EnvironmentOutcomeIds.RoomRole,
        KoreanJosaKind.Topic, " 상태가 변했다.", "상태가 변했다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.EnvironmentTag,
        new[] { EnvironmentOutcomeIds.ActorRole, EnvironmentOutcomeIds.RoomRole, EnvironmentOutcomeIds.FacilityRole },
        new[] { Metric(EnvironmentOutcomeIds.PreviousCleanlinessMetric, EnvironmentOutcomeIds.PointUnit, 0d, 1d), Metric(EnvironmentOutcomeIds.CurrentCleanlinessMetric, EnvironmentOutcomeIds.PointUnit, 0d, 1d), Metric(EnvironmentOutcomeIds.SequenceMetric, EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true) },
        new[] { EnvironmentOutcomeIds.ReceiptKindFact, EnvironmentOutcomeIds.SummaryFact }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.RoomConditionChanged;

    protected override OutcomeValidationResult ValidateAdditional(
        in GameplayOutcomeReadView outcome) =>
        FindMetric(outcome, EnvironmentOutcomeIds.PreviousCleanlinessMetric)
            != FindMetric(outcome, EnvironmentOutcomeIds.CurrentCleanlinessMetric)
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                OutcomeTypeId.Value + "-unchanged-condition-invalid");
}

public sealed class RoomEnvironmentExperienceOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public RoomEnvironmentExperienceOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "방 환경 경험", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Subject, " 방 환경의 영향을 받았다.", "방 환경의 영향을 받았다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.EnvironmentTag,
        new[] { EnvironmentOutcomeIds.ActorRole, EnvironmentOutcomeIds.RoomRole, EnvironmentOutcomeIds.FacilityRole },
        new[] { Metric(EnvironmentOutcomeIds.ImpressionMoodMetric, EnvironmentOutcomeIds.PointUnit, -100d, 100d), Metric(EnvironmentOutcomeIds.CleanlinessMoodMetric, EnvironmentOutcomeIds.PointUnit, -100d, 100d), Metric(EnvironmentOutcomeIds.DurationMetric, EnvironmentOutcomeIds.SecondUnit, double.Epsilon), Metric(EnvironmentOutcomeIds.SequenceMetric, EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true) },
        new[] { EnvironmentOutcomeIds.ReceiptKindFact, EnvironmentOutcomeIds.SummaryFact, EnvironmentOutcomeIds.ActivityFact, EnvironmentOutcomeIds.WorkTypeFact }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.RoomExperienceApplied;

    protected override OutcomeValidationResult ValidateAdditional(
        in GameplayOutcomeReadView outcome) =>
        FindMetric(outcome, EnvironmentOutcomeIds.ImpressionMoodMetric) != 0d
            || FindMetric(outcome, EnvironmentOutcomeIds.CleanlinessMoodMetric) != 0d
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                OutcomeTypeId.Value + "-zero-effect-invalid");
}

public sealed class SpeciesIncidentOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public SpeciesIncidentOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "종족 사건", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Topic, " 종족 사건을 겪었다.", "종족 사건을 겪었다.",
        GameplayOutcomeStatus.Failed, GameplayOutcomeStatus.Failed,
        EnvironmentOutcomeIds.WildlifeTag,
        new[] { EnvironmentOutcomeIds.ActorRole, EnvironmentOutcomeIds.SpeciesRole },
        new[] { Metric(EnvironmentOutcomeIds.SequenceMetric, EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true) },
        new[] { EnvironmentOutcomeIds.ReceiptKindFact, EnvironmentOutcomeIds.IncidentFact, EnvironmentOutcomeIds.SummaryFact }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.SpeciesIncidentTriggered;
}

public sealed class HarpyGaleRelocationOutcomeDescriptor :
    EnvironmentOutcomeDescriptor
{
    public HarpyGaleRelocationOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "하피 돌풍", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Subject, " 돌풍으로 물건을 흩뜨렸다.",
        "돌풍으로 물건을 흩뜨렸다.",
        GameplayOutcomeStatus.Failed, GameplayOutcomeStatus.Failed,
        EnvironmentOutcomeIds.WildlifeTag,
        new[]
        {
            EnvironmentOutcomeIds.ActorRole,
            EnvironmentOutcomeIds.SpeciesRole,
            EnvironmentOutcomeIds.ItemRole
        },
        new[]
        {
            Metric(EnvironmentOutcomeIds.SequenceMetric,
                EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true),
            Metric(EnvironmentOutcomeIds.QuantityMetric,
                EnvironmentOutcomeIds.CountUnit, 1d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.MassMetric,
                EnvironmentOutcomeIds.GramUnit, 1d, long.MaxValue, true),
            Metric(EnvironmentOutcomeIds.SourceXMetric,
                EnvironmentOutcomeIds.CellUnit, int.MinValue, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.SourceYMetric,
                EnvironmentOutcomeIds.CellUnit, int.MinValue, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.DestinationXMetric,
                EnvironmentOutcomeIds.CellUnit, int.MinValue, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.DestinationYMetric,
                EnvironmentOutcomeIds.CellUnit, int.MinValue, int.MaxValue, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.IncidentFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.SourceStackFact,
            EnvironmentOutcomeIds.DestinationStackFact,
            EnvironmentOutcomeIds.ItemDefinitionFact,
            EnvironmentOutcomeIds.ItemInstanceFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.HarpyGaleRelocation;
}
