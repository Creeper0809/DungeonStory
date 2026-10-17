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
            // Evidence retention anchors can be added after this receipt commits.
            || outcome.InitialAnchorCount != 0
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
        new[] { Metric(EnvironmentOutcomeIds.InputQuantityMetric, EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true), Metric(EnvironmentOutcomeIds.InputMassMetric, EnvironmentOutcomeIds.GramUnit, 0d, long.MaxValue, true), Metric(EnvironmentOutcomeIds.OutputQuantityMetric, EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true), Metric(EnvironmentOutcomeIds.OutputMassMetric, EnvironmentOutcomeIds.GramUnit, 0d, long.MaxValue, true), Metric(EnvironmentOutcomeIds.StatusMetric, EnvironmentOutcomeIds.EnumUnit, 2d, 4d, true), Metric(EnvironmentOutcomeIds.WaterBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d), Metric(EnvironmentOutcomeIds.WaterAfterMetric, EnvironmentOutcomeIds.PointUnit, 0d) },
        new[] { EnvironmentOutcomeIds.ReceiptKindFact, EnvironmentOutcomeIds.SourceDigestFact, EnvironmentOutcomeIds.InputDigestFact, EnvironmentOutcomeIds.OutputDigestFact, EnvironmentOutcomeIds.CommitFact, EnvironmentOutcomeIds.FingerprintFact, EnvironmentOutcomeIds.IndoorFact, EnvironmentOutcomeIds.CorrelationFact, EnvironmentOutcomeIds.SowOperationFact, EnvironmentOutcomeIds.HarvestOperationFact, EnvironmentOutcomeIds.SowContributorsFact, EnvironmentOutcomeIds.HarvestContributorsFact, EnvironmentOutcomeIds.HarvesterFact, EnvironmentOutcomeIds.EcologyBeforeFact, EnvironmentOutcomeIds.EcologyAfterFact, EnvironmentOutcomeIds.GoldenTraitFact, EnvironmentOutcomeIds.GoldenOutcomeFact, EnvironmentOutcomeIds.GoldenMultipliersFact, EnvironmentOutcomeIds.GoldenRollHashFact, EnvironmentOutcomeIds.IrrigationChildrenFact, EnvironmentOutcomeIds.SummaryFact }) { }
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
            Metric(EnvironmentOutcomeIds.WaterQualityMetric, EnvironmentOutcomeIds.EnumUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.WaterBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d),
            Metric(EnvironmentOutcomeIds.WaterAfterMetric, EnvironmentOutcomeIds.PointUnit, 0d),
            Metric(EnvironmentOutcomeIds.NetworkWaterBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d),
            Metric(EnvironmentOutcomeIds.NetworkWaterAfterMetric, EnvironmentOutcomeIds.PointUnit, 0d)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.NetworkIdFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.CropIrrigationSupplied;
}

public sealed class CropSelectionCommandOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public CropSelectionCommandOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "재배 작물 지정", EnvironmentOutcomeIds.FacilityRole,
        KoreanJosaKind.Topic, " 재배 작물을 바꿨다.", "재배 작물을 바꿨다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.AgricultureTag,
        new[] { EnvironmentOutcomeIds.FacilityRole, EnvironmentOutcomeIds.CropRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.SequenceMetric, EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true),
            Metric(EnvironmentOutcomeIds.WaterBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d),
            Metric(EnvironmentOutcomeIds.WaterAfterMetric, EnvironmentOutcomeIds.PointUnit, 0d)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.PreviousCropFact,
            EnvironmentOutcomeIds.SelectedCropFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.CropSelectionChanged;
}

public sealed class GoldenHarvestScheduleOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public GoldenHarvestScheduleOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "황금 수확 예약", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Topic, " 황금 수확을 예약했다.", "황금 수확을 예약했다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.AgricultureTag,
        new[] { EnvironmentOutcomeIds.ActorRole, EnvironmentOutcomeIds.FacilityRole, EnvironmentOutcomeIds.CropRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.SequenceMetric, EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true),
            Metric(EnvironmentOutcomeIds.AttemptBeforeMetric, EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.AttemptAfterMetric, EnvironmentOutcomeIds.CountUnit, 1d, int.MaxValue, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.PreviousHarvesterFact,
            EnvironmentOutcomeIds.HarvesterFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.GoldenHarvestScheduled;
}

public sealed class CropManualWaterRefillOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public CropManualWaterRefillOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "수동 급수", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Topic, " 농지에 물을 보충했다.", "농지에 물을 보충했다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.AgricultureTag,
        new[] { EnvironmentOutcomeIds.ActorRole, EnvironmentOutcomeIds.FacilityRole, EnvironmentOutcomeIds.ItemRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.InputQuantityMetric, EnvironmentOutcomeIds.CountUnit, 1d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.InputMassMetric, EnvironmentOutcomeIds.GramUnit, 1d, long.MaxValue, true),
            Metric(EnvironmentOutcomeIds.WaterBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d),
            Metric(EnvironmentOutcomeIds.WaterAfterMetric, EnvironmentOutcomeIds.PointUnit, double.Epsilon),
            Metric(EnvironmentOutcomeIds.SequenceMetric, EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.CommitFact,
            EnvironmentOutcomeIds.SourceStackFact,
            EnvironmentOutcomeIds.ChildResultKeyFact,
            EnvironmentOutcomeIds.WorkContributorsFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.CropManualWaterRefilled;
}

public sealed class EnvironmentalControlCommandOutcomeDescriptor :
    EnvironmentOutcomeDescriptor
{
    public EnvironmentalControlCommandOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "환경 제어", EnvironmentOutcomeIds.FacilityRole,
        KoreanJosaKind.Topic, " 목표 온도를 조정했다.", "목표 온도를 조정했다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.EnvironmentTag,
        new[] { EnvironmentOutcomeIds.FacilityRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.RequestedTargetMetric, EnvironmentOutcomeIds.PointUnit, -1000d, 1000d),
            Metric(EnvironmentOutcomeIds.AppliedTargetMetric, EnvironmentOutcomeIds.PointUnit, -50d, 80d),
            Metric(EnvironmentOutcomeIds.MinimumTargetMetric, EnvironmentOutcomeIds.PointUnit, -50d, 80d),
            Metric(EnvironmentOutcomeIds.MaximumTargetMetric, EnvironmentOutcomeIds.PointUnit, -50d, 80d),
            Metric(EnvironmentOutcomeIds.TargetBeforeMetric, EnvironmentOutcomeIds.PointUnit, -50d, 80d),
            Metric(EnvironmentOutcomeIds.TargetAfterMetric, EnvironmentOutcomeIds.PointUnit, -50d, 80d)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.CommandSourceFact,
            EnvironmentOutcomeIds.ReasonFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.EnvironmentalControlChanged;

    protected override OutcomeValidationResult ValidateAdditional(
        in GameplayOutcomeReadView outcome)
    {
        double minimum = FindMetric(outcome, EnvironmentOutcomeIds.MinimumTargetMetric);
        double maximum = FindMetric(outcome, EnvironmentOutcomeIds.MaximumTargetMetric);
        double applied = FindMetric(outcome, EnvironmentOutcomeIds.AppliedTargetMetric);
        double before = FindMetric(outcome, EnvironmentOutcomeIds.TargetBeforeMetric);
        double after = FindMetric(outcome, EnvironmentOutcomeIds.TargetAfterMetric);
        return minimum <= maximum
            && applied >= minimum
            && applied <= maximum
            && applied == after
            && before != after
                ? OutcomeValidationResult.Accepted
                : OutcomeValidationResult.Reject(
                    OutcomeTypeId.Value + "-control-transition-invalid");
    }
}

public sealed class EnvironmentalExposureThresholdOutcomeDescriptor :
    EnvironmentOutcomeDescriptor
{
    public EnvironmentalExposureThresholdOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "환경 노출", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Topic, " 환경 노출 임계 상태가 변했다.", "환경 노출 임계 상태가 변했다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Failed,
        EnvironmentOutcomeIds.EnvironmentTag,
        new[] { EnvironmentOutcomeIds.ActorRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.TemperatureMetric, EnvironmentOutcomeIds.PointUnit, -50d, 80d),
            Metric(EnvironmentOutcomeIds.AirQualityMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.LightLevelMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.ColdBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.ColdAfterMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.HeatBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.HeatAfterMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.AirborneBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.AirborneAfterMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.BandBeforeMetric, EnvironmentOutcomeIds.EnumUnit, 0d, 4d, true),
            Metric(EnvironmentOutcomeIds.BandAfterMetric, EnvironmentOutcomeIds.EnumUnit, 0d, 4d, true),
            Metric(EnvironmentOutcomeIds.DamageMetric, EnvironmentOutcomeIds.PointUnit, 0d),
            Metric(EnvironmentOutcomeIds.HealthBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d),
            Metric(EnvironmentOutcomeIds.HealthAfterMetric, EnvironmentOutcomeIds.PointUnit, 0d)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.EventKindFact,
            EnvironmentOutcomeIds.BodyPartFact,
            EnvironmentOutcomeIds.EnvironmentSourceFact,
            EnvironmentOutcomeIds.ReasonFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.EnvironmentalExposureThreshold;

    protected override OutcomeValidationResult ValidateAdditional(
        in GameplayOutcomeReadView outcome)
    {
        double damage = FindMetric(outcome, EnvironmentOutcomeIds.DamageMetric);
        double healthBefore = FindMetric(outcome, EnvironmentOutcomeIds.HealthBeforeMetric);
        double healthAfter = FindMetric(outcome, EnvironmentOutcomeIds.HealthAfterMetric);
        double bandBefore = FindMetric(outcome, EnvironmentOutcomeIds.BandBeforeMetric);
        double bandAfter = FindMetric(outcome, EnvironmentOutcomeIds.BandAfterMetric);
        bool damageEvent = damage > 0d;
        bool valid = damageEvent
            ? outcome.Status == GameplayOutcomeStatus.Failed
                && healthAfter <= healthBefore
            : outcome.Status == GameplayOutcomeStatus.Succeeded
                && bandBefore != bandAfter
                && healthBefore == healthAfter;
        return valid
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                OutcomeTypeId.Value + "-exposure-transition-invalid");
    }
}

public sealed class CropTreatmentOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public CropTreatmentOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "작물 처리", EnvironmentOutcomeIds.FacilityRole,
        KoreanJosaKind.Topic, " 작물 처리를 마쳤다.", "작물 처리를 마쳤다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.AgricultureTag,
        new[]
        {
            EnvironmentOutcomeIds.ActorRole,
            EnvironmentOutcomeIds.FacilityRole,
            EnvironmentOutcomeIds.ItemRole
        },
        new[]
        {
            Metric(EnvironmentOutcomeIds.InputQuantityMetric, EnvironmentOutcomeIds.CountUnit, 1d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.InputMassMetric, EnvironmentOutcomeIds.GramUnit, 1d, long.MaxValue, true),
            Metric(EnvironmentOutcomeIds.EffectAmountMetric, EnvironmentOutcomeIds.PointUnit, double.Epsilon, 100d),
            Metric(EnvironmentOutcomeIds.PestPressureBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.PestPressureAfterMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.DiseasePressureBeforeMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.DiseasePressureAfterMetric, EnvironmentOutcomeIds.PointUnit, 0d, 100d),
            Metric(EnvironmentOutcomeIds.CooldownBeforeDayMetric, EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.CooldownAfterDayMetric, EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.OutputQuantityMetric, EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.OutputMassMetric, EnvironmentOutcomeIds.GramUnit, 0d, long.MaxValue, true),
            Metric(EnvironmentOutcomeIds.SequenceMetric, EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.TreatmentKindFact,
            EnvironmentOutcomeIds.CommitFact,
            EnvironmentOutcomeIds.SourceStackFact,
            EnvironmentOutcomeIds.EcologyBeforeFact,
            EnvironmentOutcomeIds.EcologyAfterFact,
            EnvironmentOutcomeIds.TareCommitFact,
            EnvironmentOutcomeIds.TreatmentContributorsFact,
            EnvironmentOutcomeIds.ItemDefinitionFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.CropTreatmentApplied;

    protected override OutcomeValidationResult ValidateAdditional(
        in GameplayOutcomeReadView outcome)
    {
        double pestBefore = FindMetric(
            outcome,
            EnvironmentOutcomeIds.PestPressureBeforeMetric);
        double pestAfter = FindMetric(
            outcome,
            EnvironmentOutcomeIds.PestPressureAfterMetric);
        double diseaseBefore = FindMetric(
            outcome,
            EnvironmentOutcomeIds.DiseasePressureBeforeMetric);
        double diseaseAfter = FindMetric(
            outcome,
            EnvironmentOutcomeIds.DiseasePressureAfterMetric);
        double cooldownBefore = FindMetric(
            outcome,
            EnvironmentOutcomeIds.CooldownBeforeDayMetric);
        double cooldownAfter = FindMetric(
            outcome,
            EnvironmentOutcomeIds.CooldownAfterDayMetric);
        bool pressureReduced = pestAfter < pestBefore
            && diseaseAfter == diseaseBefore
            || diseaseAfter < diseaseBefore
            && pestAfter == pestBefore;
        bool valid = pressureReduced && cooldownAfter >= cooldownBefore;
        return valid
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                OutcomeTypeId.Value + "-state-transition-invalid");
    }
}

public sealed class WildlifeFoodRaidOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public WildlifeFoodRaidOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa,
        "야생동물 식량 습격",
        EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Subject,
        " 식량을 훔쳤다.",
        "식량을 훔쳤다.",
        GameplayOutcomeStatus.Succeeded,
        GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.WildlifeTag,
        new[]
        {
            EnvironmentOutcomeIds.ActorRole,
            EnvironmentOutcomeIds.SpeciesRole,
            EnvironmentOutcomeIds.TargetRole,
            EnvironmentOutcomeIds.ItemRole
        },
        new[]
        {
            Metric(EnvironmentOutcomeIds.RequestedQuantityMetric,
                EnvironmentOutcomeIds.CountUnit, 1d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.StolenQuantityMetric,
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
                EnvironmentOutcomeIds.CellUnit, int.MinValue, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.StartedTimeMetric,
                EnvironmentOutcomeIds.SecondUnit, 0d, float.MaxValue),
            Metric(EnvironmentOutcomeIds.CompletedTimeMetric,
                EnvironmentOutcomeIds.SecondUnit, 0d, float.MaxValue),
            Metric(EnvironmentOutcomeIds.StartedDayMetric,
                EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.CompletedDayMetric,
                EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.SequenceMetric,
                EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.RaidIdFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.CommitFact,
            EnvironmentOutcomeIds.SourceStackFact,
            EnvironmentOutcomeIds.ItemDefinitionFact,
            EnvironmentOutcomeIds.TerminalStateFact,
            EnvironmentOutcomeIds.ReasonFact,
            EnvironmentOutcomeIds.ChildResultKeyFact,
            EnvironmentOutcomeIds.SummaryFact,
            new GameplayOutcomeFactId(
                "wildlife-food-raid-context-sha256")
        }) { }

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.WildlifeFoodRaidCompleted;

    protected override OutcomeValidationResult ValidateAdditional(
        in GameplayOutcomeReadView outcome)
    {
        double requested = FindMetric(
            outcome,
            EnvironmentOutcomeIds.RequestedQuantityMetric);
        double stolen = FindMetric(
            outcome,
            EnvironmentOutcomeIds.StolenQuantityMetric);
        double started = FindMetric(
            outcome,
            EnvironmentOutcomeIds.StartedTimeMetric);
        double completed = FindMetric(
            outcome,
            EnvironmentOutcomeIds.CompletedTimeMetric);
        double startedDay = FindMetric(
            outcome,
            EnvironmentOutcomeIds.StartedDayMetric);
        double completedDay = FindMetric(
            outcome,
            EnvironmentOutcomeIds.CompletedDayMetric);
        bool valid = stolen <= requested
            && completed >= started
            && completedDay >= startedDay
            && outcome.Causation.HasParent
            && outcome.Causation.RootOperationId.IsValid
            && string.Equals(
                outcome.Causation.RelationId,
                "physical-theft-consumption",
                StringComparison.Ordinal);
        return valid
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                OutcomeTypeId.Value + "-state-transition-invalid");
    }
}

public sealed class WildlifeCommandOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public WildlifeCommandOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa,
        "야생동물 명령",
        EnvironmentOutcomeIds.WildlifeRole,
        KoreanJosaKind.Topic,
        "에게 새 명령이 내려졌다.",
        "새 명령을 받았다.",
        GameplayOutcomeStatus.Succeeded,
        GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.WildlifeTag,
        new[] { EnvironmentOutcomeIds.WildlifeRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.StateBeforeMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.StateAfterMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.FlagBeforeMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, 1d, true),
            Metric(EnvironmentOutcomeIds.FlagAfterMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, 1d, true),
            Metric(EnvironmentOutcomeIds.PriorityBeforeMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, 1d, true),
            Metric(EnvironmentOutcomeIds.PriorityAfterMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, 1d, true),
            Metric(EnvironmentOutcomeIds.SequenceMetric,
                EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.EventKindFact,
            EnvironmentOutcomeIds.FacilityFact,
            EnvironmentOutcomeIds.RelatedActorFact,
            EnvironmentOutcomeIds.ReasonFact,
            EnvironmentOutcomeIds.SummaryFact,
            new GameplayOutcomeFactId("wildlife-context-sha256")
        }) { }

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.WildlifeCommand;
}

public sealed class WildlifeActionOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public WildlifeActionOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa,
        "야생동물 행동 결과",
        EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Topic,
        " 야생동물 행동을 수행했다.",
        "야생동물 행동을 수행했다.",
        GameplayOutcomeStatus.Succeeded,
        GameplayOutcomeStatus.Failed,
        EnvironmentOutcomeIds.WildlifeTag,
        new[]
        {
            EnvironmentOutcomeIds.ActorRole,
            EnvironmentOutcomeIds.WildlifeRole
        },
        new[]
        {
            Metric(EnvironmentOutcomeIds.StateBeforeMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.StateAfterMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.HealthBeforeMetric,
                EnvironmentOutcomeIds.PointUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.HealthAfterMetric,
                EnvironmentOutcomeIds.PointUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.DamageMetric,
                EnvironmentOutcomeIds.PointUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.AmmunitionBeforeMetric,
                EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.AmmunitionConsumedMetric,
                EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.AmmunitionAfterMetric,
                EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.HitMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, 1d, true),
            Metric(EnvironmentOutcomeIds.KilledMetric,
                EnvironmentOutcomeIds.EnumUnit, 0d, 1d, true),
            Metric(EnvironmentOutcomeIds.OutputQuantityMetric,
                EnvironmentOutcomeIds.CountUnit, 0d, int.MaxValue, true),
            Metric(EnvironmentOutcomeIds.OutputMassMetric,
                EnvironmentOutcomeIds.GramUnit, 0d, long.MaxValue, true),
            Metric(EnvironmentOutcomeIds.SequenceMetric,
                EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.CorrelationFact,
            EnvironmentOutcomeIds.EventKindFact,
            EnvironmentOutcomeIds.WeaponDefinitionFact,
            EnvironmentOutcomeIds.WeaponInstanceFact,
            EnvironmentOutcomeIds.AmmunitionItemFact,
            EnvironmentOutcomeIds.BodyPartFact,
            EnvironmentOutcomeIds.CoverSourceFact,
            EnvironmentOutcomeIds.CarcassStackFact,
            EnvironmentOutcomeIds.FacilityFact,
            EnvironmentOutcomeIds.ChildResultKeyFact,
            EnvironmentOutcomeIds.ReasonFact,
            EnvironmentOutcomeIds.SummaryFact,
            new GameplayOutcomeFactId("wildlife-context-sha256")
        }) { }

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.WildlifeAction;

    protected override OutcomeValidationResult ValidateAdditional(
        in GameplayOutcomeReadView outcome)
    {
        double before = FindMetric(outcome, EnvironmentOutcomeIds.HealthBeforeMetric);
        double after = FindMetric(outcome, EnvironmentOutcomeIds.HealthAfterMetric);
        double damage = FindMetric(outcome, EnvironmentOutcomeIds.DamageMetric);
        double ammunitionBefore = FindMetric(
            outcome,
            EnvironmentOutcomeIds.AmmunitionBeforeMetric);
        double ammunitionConsumed = FindMetric(
            outcome,
            EnvironmentOutcomeIds.AmmunitionConsumedMetric);
        double ammunitionAfter = FindMetric(
            outcome,
            EnvironmentOutcomeIds.AmmunitionAfterMetric);
        bool valid = after <= before
            && damage == before - after
            && ammunitionAfter <= ammunitionBefore
            && ammunitionConsumed == ammunitionBefore - ammunitionAfter;
        return valid
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                OutcomeTypeId.Value + "-state-transition-invalid");
    }
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
            EnvironmentOutcomeIds.SummaryFact,
            new GameplayOutcomeFactId("fire-completion-target"),
            new GameplayOutcomeFactId("fire-completion-origin"),
            new GameplayOutcomeFactId("fire-completion-work"),
            new GameplayOutcomeFactId("fire-completion-required")
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
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.WorkTypeFact,
            EnvironmentOutcomeIds.AnatomyNodeFact,
            EnvironmentOutcomeIds.ReasonFact,
            EnvironmentOutcomeIds.SummaryFact,
            EnvironmentOutcomeIds.ProcessRiskMultiplierFact,
            EnvironmentOutcomeIds.ProcessCommandOriginFact,
            EnvironmentOutcomeIds.ProcessFireEligibleFact,
            EnvironmentOutcomeIds.ProcessFireIntensityFact,
            EnvironmentOutcomeIds.ProcessFireTargetNameFact
        }) { }
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

public sealed class GolemUpkeepOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public GolemUpkeepOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa, "골렘 상태 변화", EnvironmentOutcomeIds.ActorRole,
        KoreanJosaKind.Topic, " 골렘 상태가 변했다.", "골렘 상태가 변했다.",
        GameplayOutcomeStatus.Succeeded, GameplayOutcomeStatus.Succeeded,
        EnvironmentOutcomeIds.SpeciesTag,
        new[] { EnvironmentOutcomeIds.ActorRole, EnvironmentOutcomeIds.SpeciesRole },
        new[]
        {
            Metric(EnvironmentOutcomeIds.SequenceMetric,
                EnvironmentOutcomeIds.RevisionUnit, 1d, long.MaxValue, true)
        },
        new[]
        {
            EnvironmentOutcomeIds.ReceiptKindFact,
            EnvironmentOutcomeIds.EpisodeKindFact,
            EnvironmentOutcomeIds.StateBeforeFact,
            EnvironmentOutcomeIds.StateAfterFact,
            EnvironmentOutcomeIds.PhysicalChildFact,
            EnvironmentOutcomeIds.WorkChildFact,
            EnvironmentOutcomeIds.BodyChildFact,
            EnvironmentOutcomeIds.SummaryFact
        }) { }

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.GolemUpkeepEpisode;
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
