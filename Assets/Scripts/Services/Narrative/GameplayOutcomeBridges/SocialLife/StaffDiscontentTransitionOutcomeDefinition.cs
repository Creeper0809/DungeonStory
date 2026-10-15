using System;
using DungeonStory.Narrative.Korean;

public static class StaffDiscontentTransitionOutcomeIds
{
    public const string ProducerId = "social.staff-discontent-transition";

    public static readonly GameplayOutcomeTypeId Transitioned =
        new("social.staff-discontent-transitioned");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId StageKind =
        new("staff-discontent-stage");
    public static readonly GameplayEntityKindId OutcomeKind =
        new("staff-discontent-outcome");
    public static readonly GameplayRoleId StaffRole = new("staff");
    public static readonly GameplayMetricId OutcomeMetric =
        new("staff-discontent.outcome");
    public static readonly GameplayMetricId BeforeStageMetric =
        new("staff-discontent.before-stage");
    public static readonly GameplayMetricId AfterStageMetric =
        new("staff-discontent.after-stage");
    public static readonly GameplayMetricId BeforeMoodMetric =
        new("staff-discontent.before-mood");
    public static readonly GameplayMetricId AfterMoodMetric =
        new("staff-discontent.after-mood");
    public static readonly GameplayMetricId BeforeLowMoodDaysMetric =
        new("staff-discontent.before-low-mood-days");
    public static readonly GameplayMetricId AfterLowMoodDaysMetric =
        new("staff-discontent.after-low-mood-days");
    public static readonly GameplayMetricId PermanentLossMetric =
        new("staff-discontent.permanent-loss");
    public static readonly GameplayMetricId DepartedMetric =
        new("staff-discontent.departed");
    public static readonly GameplayMetricId LocalRebellionMetric =
        new("staff-discontent.local-rebellion");
    public static readonly GameplayMetricId OwnerThreatMetric =
        new("staff-discontent.owner-threat");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId EnumUnit = new("enum");
    public static readonly GameplayMetricUnitId BooleanUnit = new("boolean");
    public static readonly GameplayMetricUnitId DayUnit = new("day");
    public static readonly GameplayOutcomeTagId RelationshipTag =
        new("relationship");
    public static readonly GameplayOutcomeTagId StaffTag = new("staff");
    public static readonly GameplayOutcomeTagId DiscontentTag =
        new("staff-discontent");
    public static readonly GameplayOutcomeProvenanceReference Source =
        new("source-receipt", "staff-discontent-daily-transition");

    public static GameplayResultKey ResultKey(long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId("staff-discontent:" + ownerRevision),
        ownerRevision,
        0);

    public static string OutcomeSlug(StaffDiscontentOutcome value) => value switch
    {
        StaffDiscontentOutcome.Warning => "warning",
        StaffDiscontentOutcome.EfficiencyPenalty => "efficiency-penalty",
        StaffDiscontentOutcome.WorkDisruption => "work-disruption",
        StaffDiscontentOutcome.PermanentDeparture => "permanent-departure",
        StaffDiscontentOutcome.LocalRebellion => "local-rebellion",
        StaffDiscontentOutcome.OwnerThreat => "owner-threat",
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            "Only meaningful daily staff transitions are outcomes.")
    };
}

public readonly struct StaffDiscontentTransitionOutcomeReceipt
{
    public StaffDiscontentTransitionOutcomeReceipt(
        long ownerRevision,
        int absoluteDay,
        StaffDiscontentOutcome transition,
        StaffDiscontentSnapshot before,
        StaffDiscontentSnapshot after)
    {
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (before == null || after == null)
            throw new ArgumentNullException(
                before == null ? nameof(before) : nameof(after));
        if (!string.Equals(before.staffId, after.staffId, StringComparison.Ordinal))
            throw new ArgumentException(
                "Staff transition snapshots must have the same staff ID.");
        Staff = new CharacterId(after.staffId);
        if (!Staff.IsValid)
            throw new ArgumentException("A canonical staff ID is required.");
        _ = StaffDiscontentTransitionOutcomeIds.OutcomeSlug(transition);

        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
        Transition = transition;
        Before = before;
        After = after;
        StaffName = CaptureName(Staff.Value, after.displayName);
        FactDigest = new StaffDiscontentTransitionedFact(new StaffDiscontentFactData
        {
            kind = StaffDiscontentFactKind.DailyTransition, revision = ownerRevision, absoluteDay = absoluteDay,
            transition = transition, before = StaffDiscontentFactValues.Capture(before), after = StaffDiscontentFactValues.Capture(after)
        }).ComputeSemanticDigest();
    }

    public long OwnerRevision { get; }
    public string FactDigest { get; }
    public int AbsoluteDay { get; }
    public StaffDiscontentOutcome Transition { get; }
    public CharacterId Staff { get; }
    public KoreanNameSnapshot StaffName { get; }
    public StaffDiscontentSnapshot Before { get; }
    public StaffDiscontentSnapshot After { get; }
    public GameplayResultKey ResultKey =>
        StaffDiscontentTransitionOutcomeIds.ResultKey(OwnerRevision);

    private static KoreanNameSnapshot CaptureName(
        string stableIdentity,
        string displayText)
    {
        string identity = GameplayOutcomeStableIdSyntax.Require(
            stableIdentity,
            nameof(stableIdentity));
        string display = displayText?.Trim() ?? string.Empty;
        if (display.Length == 0)
        {
            throw new ArgumentException(
                "An immutable staff display name is required.",
                nameof(displayText));
        }
        string revision = "staff-discontent-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(identity + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }
}

public sealed class StaffDiscontentTransitionOutcomeAdapter :
    GameplayOutcomeAdapter<StaffDiscontentTransitionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        StaffDiscontentTransitionOutcomeIds.Transitioned;

    public static OutcomeWriteRequirements CreateRequirements(
        GameplayResultKey resultKey,
        long ownerRevision,
        int absoluteDay,
        long worldEpoch) => new(
        resultKey,
        StaffDiscontentTransitionOutcomeIds.Transitioned,
        absoluteDay,
        GameplayOutcomeStatus.Succeeded,
        worldEpoch,
        ownerRevision,
        participantCount: 1,
        metricCount: 11,
        subjectCount: 1,
        tagCount: 3,
        anchorCount: 0,
        provenanceCount: 2);

    public override OutcomePrepareResult TryGetRequirements(
        in StaffDiscontentTransitionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = CreateRequirements(
            receipt.ResultKey,
            receipt.OwnerRevision,
            receipt.AbsoluteDay,
            currentWorldEpoch);
        bool valid = receipt.OwnerRevision > 0L
            && receipt.AbsoluteDay >= 0
            && receipt.Staff.IsValid
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(receipt.StaffName)
            && receipt.Before != null
            && receipt.After != null
            && receipt.After.lastProcessedAbsoluteDay == receipt.AbsoluteDay
            && string.Equals(
                receipt.Before.staffId,
                receipt.After.staffId,
                StringComparison.Ordinal);
        try
        {
            _ = StaffDiscontentTransitionOutcomeIds.OutcomeSlug(
                receipt.Transition);
        }
        catch (ArgumentOutOfRangeException)
        {
            valid = false;
        }
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "staff-discontent-transition-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in StaffDiscontentTransitionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId staff = Character(receipt.Staff);
        GameplayEntityId outcome = new(
            StaffDiscontentTransitionOutcomeIds.OutcomeKind,
            StaffDiscontentTransitionOutcomeIds.OutcomeSlug(
                receipt.Transition));
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                staff,
                StaffDiscontentTransitionOutcomeIds.StaffRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.StaffName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                staff,
                Salience(receipt.Transition),
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && AddMetric(ref builder,
                StaffDiscontentTransitionOutcomeIds.OutcomeMetric,
                (int)receipt.Transition,
                StaffDiscontentTransitionOutcomeIds.EnumUnit,
                outcome)
            && AddMetric(ref builder,
                StaffDiscontentTransitionOutcomeIds.BeforeStageMetric,
                (int)receipt.Before.stage,
                StaffDiscontentTransitionOutcomeIds.EnumUnit,
                Stage(receipt.Before.stage))
            && AddMetric(ref builder,
                StaffDiscontentTransitionOutcomeIds.AfterStageMetric,
                (int)receipt.After.stage,
                StaffDiscontentTransitionOutcomeIds.EnumUnit,
                Stage(receipt.After.stage))
            && AddMetric(ref builder,
                StaffDiscontentTransitionOutcomeIds.BeforeMoodMetric,
                receipt.Before.mood,
                StaffDiscontentTransitionOutcomeIds.PointUnit,
                staff)
            && AddMetric(ref builder,
                StaffDiscontentTransitionOutcomeIds.AfterMoodMetric,
                receipt.After.mood,
                StaffDiscontentTransitionOutcomeIds.PointUnit,
                staff)
            && AddMetric(ref builder,
                StaffDiscontentTransitionOutcomeIds.BeforeLowMoodDaysMetric,
                receipt.Before.lowMoodDays,
                StaffDiscontentTransitionOutcomeIds.DayUnit,
                staff)
            && AddMetric(ref builder,
                StaffDiscontentTransitionOutcomeIds.AfterLowMoodDaysMetric,
                receipt.After.lowMoodDays,
                StaffDiscontentTransitionOutcomeIds.DayUnit,
                staff)
            && AddBoolean(ref builder,
                StaffDiscontentTransitionOutcomeIds.PermanentLossMetric,
                receipt.After.permanentLoss,
                staff)
            && AddBoolean(ref builder,
                StaffDiscontentTransitionOutcomeIds.DepartedMetric,
                receipt.After.departed,
                staff)
            && AddBoolean(ref builder,
                StaffDiscontentTransitionOutcomeIds.LocalRebellionMetric,
                receipt.After.localRebellion,
                staff)
            && AddBoolean(ref builder,
                StaffDiscontentTransitionOutcomeIds.OwnerThreatMetric,
                receipt.After.ownerThreat,
                staff)
            && builder.AddTag(
                StaffDiscontentTransitionOutcomeIds.RelationshipTag)
            && builder.AddTag(StaffDiscontentTransitionOutcomeIds.StaffTag)
            && builder.AddTag(
                StaffDiscontentTransitionOutcomeIds.DiscontentTag)
            && builder.AddProvenance(
                StaffDiscontentTransitionOutcomeIds.Source)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference("domain-fact-sha256", receipt.FactDigest));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "staff-discontent-transition-write-failed");
    }

    private static bool AddBoolean(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        bool value,
        GameplayEntityId entity) => AddMetric(
        ref builder,
        id,
        value ? 1d : 0d,
        StaffDiscontentTransitionOutcomeIds.BooleanUnit,
        entity);

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId entity) => builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, entity));

    private static GameplayEntityId Character(CharacterId id) => new(
        StaffDiscontentTransitionOutcomeIds.CharacterKind,
        id.Value);

    private static GameplayEntityId Stage(StaffDiscontentStage stage) => new(
        StaffDiscontentTransitionOutcomeIds.StageKind,
        StaffDiscontentOutcomeIds.StageSlug(stage));

    private static float Salience(StaffDiscontentOutcome outcome) =>
        outcome switch
        {
            StaffDiscontentOutcome.PermanentDeparture => 1f,
            StaffDiscontentOutcome.LocalRebellion => 1f,
            StaffDiscontentOutcome.OwnerThreat => 0.98f,
            StaffDiscontentOutcome.WorkDisruption => 0.82f,
            StaffDiscontentOutcome.EfficiencyPenalty => 0.68f,
            _ => 0.55f
        };
}


internal sealed class StaffDiscontentTransitionMemoryPolicy :
    IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":" + (int)Metric(
            outcome,
            StaffDiscontentTransitionOutcomeIds.OutcomeMetric));

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        float baseSalience = outcome.GetSubject(0).Salience;
        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float salience = Math.Clamp(
            baseSalience
            - Math.Min(0.2f, priorMatchingCount * 0.03f)
            - Math.Min(0.2f, age * 0.006f),
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

    private static double Metric(
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
}

internal sealed class StaffDiscontentTransitionPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class StaffDiscontentTransitionConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class StaffDiscontentTransitionPerspectiveProjector :
    SocialLifePerspectiveProjectorBase
{
    public StaffDiscontentTransitionPerspectiveProjector(
        IKoreanJosaFormatter josa)
        : base(josa)
    {
    }

    public override NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        TryParticipant(
            outcome,
            StaffDiscontentTransitionOutcomeIds.StaffRole,
            out GameplayOutcomeParticipant staff);
        StaffDiscontentStage before = (StaffDiscontentStage)(int)Metric(
            outcome,
            StaffDiscontentTransitionOutcomeIds.BeforeStageMetric);
        StaffDiscontentStage after = (StaffDiscontentStage)(int)Metric(
            outcome,
            StaffDiscontentTransitionOutcomeIds.AfterStageMetric);
        bool neutral = !TryWithJosa(
            staff.DisplayName,
            KoreanJosaKind.Subject,
            out string subject);
        string detail = $"불만 상태가 {StageLabel(before)}에서 {StageLabel(after)}로 바뀌었다.";
        string text = neutral
            ? $"직원: {staff.DisplayName.DisplayText} · {detail}"
            : $"{subject} {detail}";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }

    private static string StageLabel(StaffDiscontentStage stage) => stage switch
    {
        StaffDiscontentStage.Stable => "안정",
        StaffDiscontentStage.LowSatisfaction => "낮은 만족도",
        StaffDiscontentStage.EfficiencyDrop => "효율 저하",
        StaffDiscontentStage.WorkDisruption => "작업 방해",
        StaffDiscontentStage.Departure => "이탈",
        StaffDiscontentStage.LocalRebellion => "국지 반란",
        _ => "알 수 없음"
    };
}

public sealed class StaffDiscontentTransitionOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new StaffDiscontentTransitionMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new StaffDiscontentTransitionPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new StaffDiscontentTransitionConsolidator();

    public StaffDiscontentTransitionOutcomeDescriptor(
        IKoreanJosaFormatter josa) =>
        projector = new StaffDiscontentTransitionPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        StaffDiscontentTransitionOutcomeIds.Transitioned;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(StaffDiscontentTransitionOutcomeIds.StaffRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        (metricId.Equals(StaffDiscontentTransitionOutcomeIds.OutcomeMetric)
            || metricId.Equals(StaffDiscontentTransitionOutcomeIds.BeforeStageMetric)
            || metricId.Equals(StaffDiscontentTransitionOutcomeIds.AfterStageMetric))
        && unitId.Equals(StaffDiscontentTransitionOutcomeIds.EnumUnit)
        || (metricId.Equals(StaffDiscontentTransitionOutcomeIds.BeforeMoodMetric)
            || metricId.Equals(StaffDiscontentTransitionOutcomeIds.AfterMoodMetric))
        && unitId.Equals(StaffDiscontentTransitionOutcomeIds.PointUnit)
        || (metricId.Equals(StaffDiscontentTransitionOutcomeIds.BeforeLowMoodDaysMetric)
            || metricId.Equals(StaffDiscontentTransitionOutcomeIds.AfterLowMoodDaysMetric))
        && unitId.Equals(StaffDiscontentTransitionOutcomeIds.DayUnit)
        || (metricId.Equals(StaffDiscontentTransitionOutcomeIds.PermanentLossMetric)
            || metricId.Equals(StaffDiscontentTransitionOutcomeIds.DepartedMetric)
            || metricId.Equals(StaffDiscontentTransitionOutcomeIds.LocalRebellionMetric)
            || metricId.Equals(StaffDiscontentTransitionOutcomeIds.OwnerThreatMetric))
        && unitId.Equals(StaffDiscontentTransitionOutcomeIds.BooleanUnit);

    public OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 1
        && outcome.MetricCount == 11
        && outcome.SubjectCount == 1
        && outcome.TagCount == 3
        && outcome.AnchorCount == 0
        && outcome.ProvenanceCount == 2
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "staff-discontent-transition-shape-invalid");
}
