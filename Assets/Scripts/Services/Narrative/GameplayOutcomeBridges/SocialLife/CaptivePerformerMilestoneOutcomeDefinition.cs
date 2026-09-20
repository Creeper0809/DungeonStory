using System;
using DungeonStory.Narrative.Korean;

public static class CaptivePerformerMilestoneOutcomeIds
{
    public const string ProducerId = "captivity.performer-milestone";

    public static readonly GameplayOutcomeTypeId Unlocked =
        new("captivity.performer-milestone-unlocked");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayRoleId CaptiveRole = new("captive-performer");
    public static readonly GameplayMetricId ThresholdMetric =
        new("captivity.performer-milestone.threshold");
    public static readonly GameplayMetricId FameMetric =
        new("captivity.performer-milestone.current-fame");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("captivity.performer-milestone.owner-revision");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId PerformerTag =
        new("captive-performer");
    public static readonly GameplayOutcomeFactId UnlockKindFact =
        new("captivity.performer-milestone.unlock-kind");
    public static readonly GameplayOutcomeProvenanceReference Source =
        new("runtime-receipt", "captive-performer-milestone-v1");

    public static GameplayResultKey ResultKey(
        string captiveId,
        int threshold,
        long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId(
            $"captive-performer-milestone:{captiveId}:{threshold}"),
        ownerRevision,
        0);

    public static string UnlockKind(int threshold) => threshold switch
    {
        50 => "care-priority",
        75 => "staff-contract",
        100 => "final-contract-choice",
        _ => throw new ArgumentOutOfRangeException(nameof(threshold))
    };
}

public readonly struct CaptivePerformerMilestoneOutcomeReceipt
{
    public CaptivePerformerMilestoneOutcomeReceipt(
        string captiveId,
        KoreanNameSnapshot captiveName,
        int threshold,
        float currentFame,
        long ownerRevision,
        int absoluteDay)
    {
        CharacterId characterId = new(captiveId);
        if (!characterId.IsValid)
            throw new ArgumentException("A canonical captive character ID is required.", nameof(captiveId));
        if (!GameplayOutcomeLedger.IsValidDisplayNameSnapshot(captiveName))
            throw new ArgumentException("A frozen captive display name is required.", nameof(captiveName));
        _ = CaptivePerformerMilestoneOutcomeIds.UnlockKind(threshold);
        if (float.IsNaN(currentFame)
            || float.IsInfinity(currentFame)
            || currentFame < threshold
            || currentFame > 100f)
        {
            throw new ArgumentOutOfRangeException(nameof(currentFame));
        }
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        CaptiveId = characterId;
        CaptiveName = captiveName;
        Threshold = threshold;
        CurrentFame = currentFame;
        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
    }

    public CharacterId CaptiveId { get; }
    public KoreanNameSnapshot CaptiveName { get; }
    public int Threshold { get; }
    public float CurrentFame { get; }
    public long OwnerRevision { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey =>
        CaptivePerformerMilestoneOutcomeIds.ResultKey(
            CaptiveId.Value,
            Threshold,
            OwnerRevision);
}

public sealed class CaptivePerformerMilestoneOutcomeAdapter :
    GameplayOutcomeAdapter<CaptivePerformerMilestoneOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        CaptivePerformerMilestoneOutcomeIds.Unlocked;

    public override OutcomePrepareResult TryGetRequirements(
        in CaptivePerformerMilestoneOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        bool valid = receipt.CaptiveId.IsValid
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(receipt.CaptiveName)
            && receipt.OwnerRevision > 0L
            && receipt.AbsoluteDay >= 0
            && IsThreshold(receipt.Threshold)
            && !float.IsNaN(receipt.CurrentFame)
            && !float.IsInfinity(receipt.CurrentFame)
            && receipt.CurrentFame >= receipt.Threshold
            && receipt.CurrentFame <= 100f
            && receipt.ResultKey.IsValid;
        if (!valid)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "captive-performer-milestone-receipt-invalid");
        }

        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 1,
            subjectCount: 1,
            metricCount: 3,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 1,
            factCount: 1);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in CaptivePerformerMilestoneOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId captive = Character(receipt.CaptiveId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                captive,
                CaptivePerformerMilestoneOutcomeIds.CaptiveRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.CaptiveName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                captive,
                Salience(receipt.Threshold),
                Tier(receipt.Threshold),
                receipt.Threshold == 100,
                false,
                0))
            && AddMetric(
                ref builder,
                CaptivePerformerMilestoneOutcomeIds.ThresholdMetric,
                receipt.Threshold,
                CaptivePerformerMilestoneOutcomeIds.PointUnit,
                captive)
            && AddMetric(
                ref builder,
                CaptivePerformerMilestoneOutcomeIds.FameMetric,
                receipt.CurrentFame,
                CaptivePerformerMilestoneOutcomeIds.PointUnit,
                captive)
            && AddMetric(
                ref builder,
                CaptivePerformerMilestoneOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                CaptivePerformerMilestoneOutcomeIds.RevisionUnit,
                captive)
            && builder.AddTag(CaptivePerformerMilestoneOutcomeIds.PerformerTag)
            && builder.AddProvenance(CaptivePerformerMilestoneOutcomeIds.Source)
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivePerformerMilestoneOutcomeIds.UnlockKindFact,
                CaptivePerformerMilestoneOutcomeIds.UnlockKind(receipt.Threshold)));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "captive-performer-milestone-write-failed");
    }

    internal static bool IsThreshold(int threshold) =>
        threshold is 50 or 75 or 100;

    internal static float Salience(int threshold) => threshold switch
    {
        50 => 0.85f,
        75 => 0.9f,
        100 => 0.98f,
        _ => 0f
    };

    internal static NarrativeMemoryTier Tier(int threshold) =>
        threshold == 100
            ? NarrativeMemoryTier.Core
            : NarrativeMemoryTier.Episodic;

    private static GameplayEntityId Character(CharacterId id) => new(
        CaptivePerformerMilestoneOutcomeIds.CharacterKind,
        id.Value);

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId subject) => builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, subject));
}

public readonly struct PreparedCaptivePerformerMilestoneOutcome
{
    public PreparedCaptivePerformerMilestoneOutcome(
        PreparedOwnerOutcome prepared,
        GameplayResultKey resultKey,
        long ownerRevision,
        bool isReplay)
    {
        Prepared = prepared;
        ResultKey = resultKey;
        OwnerRevision = ownerRevision;
        IsReplay = isReplay;
    }

    internal PreparedOwnerOutcome Prepared { get; }
    public GameplayResultKey ResultKey { get; }
    public long OwnerRevision { get; }
    public bool IsReplay { get; }
}

public interface ICaptivePerformerMilestoneOutcomeCommitter
{
    bool TryPrepare(
        in CaptivePerformerMilestoneOutcomeReceipt receipt,
        out PreparedCaptivePerformerMilestoneOutcome prepared,
        out string failureReason);
    OwnerOutcomeCommitResult Commit(
        in PreparedCaptivePerformerMilestoneOutcome prepared);
    void Cancel(in PreparedCaptivePerformerMilestoneOutcome prepared);
}

public sealed class CaptivePerformerMilestoneGameplayOutcomeBridge :
    ICaptivePerformerMilestoneOutcomeCommitter
{
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public CaptivePerformerMilestoneGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
    }

    public bool TryPrepare(
        in CaptivePerformerMilestoneOutcomeReceipt receipt,
        out PreparedCaptivePerformerMilestoneOutcome prepared,
        out string failureReason)
    {
        prepared = default;
        if (!transactions.TryPrepare(
                receipt,
                out PreparedOwnerOutcome token,
                out OwnerOutcomeCommitResult replay,
                out _,
                out failureReason))
        {
            return false;
        }
        prepared = new PreparedCaptivePerformerMilestoneOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            replay.DurablyCommitted);
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedCaptivePerformerMilestoneOutcome prepared) =>
        prepared.IsReplay
            ? transactions.Reconcile(prepared.ResultKey)
            : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public void Cancel(in PreparedCaptivePerformerMilestoneOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }
}

internal sealed class CaptivePerformerMilestoneMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":" + Threshold(outcome));

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        int threshold = Threshold(outcome);
        return new OutcomeMemoryEvaluation(
            CaptivePerformerMilestoneOutcomeAdapter.Salience(threshold),
            CaptivePerformerMilestoneOutcomeAdapter.Tier(threshold),
            threshold == 100
                ? int.MaxValue
                : Math.Max(evaluationDay + 30, outcome.AbsoluteDay + 30));
    }

    private static int Threshold(in GameplayOutcomeReadView outcome)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(
                    CaptivePerformerMilestoneOutcomeIds.ThresholdMetric))
            {
                return (int)metric.Value;
            }
        }
        return 0;
    }
}

internal sealed class CaptivePerformerMilestonePerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class CaptivePerformerMilestoneConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class CaptivePerformerMilestonePerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "captive-performer-milestone-v1";
    private readonly IKoreanJosaFormatter josa;

    public CaptivePerformerMilestonePerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant captive = FindCaptive(outcome);
        int threshold = (int)Metric(
            outcome,
            CaptivePerformerMilestoneOutcomeIds.ThresholdMetric);
        string unlocked = threshold switch
        {
            50 => "우선 식량·치료 특혜를 얻었다",
            75 => "직원 계약 제안 자격을 얻었다",
            100 => "석방 협상과 전속 투사 계약의 최종 선택권을 얻었다",
            _ => "공연자 이정표에 도달했다"
        };
        KoreanJosaFormatResult subject = josa.Format(new KoreanJosaRequest(
            captive.DisplayName,
            KoreanJosaKind.Subject));
        bool neutral = subject.RequiresNeutralFrame;
        string text = neutral
            ? $"공연자: {captive.DisplayName.DisplayText} · 명성 {threshold} · {unlocked}."
            : $"{subject.Text} 공연 명성 {threshold}에 도달해 {unlocked}.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }

    private static GameplayOutcomeParticipant FindCaptive(
        in GameplayOutcomeReadView outcome)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant participant = outcome.GetParticipant(index);
            if (participant.RoleId.Equals(
                    CaptivePerformerMilestoneOutcomeIds.CaptiveRole))
            {
                return participant;
            }
        }
        return default;
    }

    private static double Metric(
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

public sealed class CaptivePerformerMilestoneOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new CaptivePerformerMilestoneMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new CaptivePerformerMilestonePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new CaptivePerformerMilestoneConsolidator();

    public CaptivePerformerMilestoneOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new CaptivePerformerMilestonePerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        CaptivePerformerMilestoneOutcomeIds.Unlocked;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(CaptivePerformerMilestoneOutcomeIds.CaptiveRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        (metricId.Equals(CaptivePerformerMilestoneOutcomeIds.ThresholdMetric)
            || metricId.Equals(CaptivePerformerMilestoneOutcomeIds.FameMetric))
        && unitId.Equals(CaptivePerformerMilestoneOutcomeIds.PointUnit)
        || metricId.Equals(CaptivePerformerMilestoneOutcomeIds.OwnerRevisionMetric)
        && unitId.Equals(CaptivePerformerMilestoneOutcomeIds.RevisionUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        int captiveCount = 0;
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            if (outcome.GetParticipant(index).RoleId.Equals(
                    CaptivePerformerMilestoneOutcomeIds.CaptiveRole))
            {
                captiveCount++;
            }
        }
        bool hasThreshold = TryMetric(
            outcome,
            CaptivePerformerMilestoneOutcomeIds.ThresholdMetric,
            out double thresholdValue);
        bool hasFame = TryMetric(
            outcome,
            CaptivePerformerMilestoneOutcomeIds.FameMetric,
            out double fame);
        bool hasRevision = TryMetric(
            outcome,
            CaptivePerformerMilestoneOutcomeIds.OwnerRevisionMetric,
            out double revisionValue);
        int threshold = (int)thresholdValue;
        long revision = (long)revisionValue;
        GameplayOutcomeParticipant participant = outcome.ParticipantCount == 1
            ? outcome.GetParticipant(0)
            : default;
        GameplayOutcomeSubjectLink subject = outcome.SubjectCount == 1
            ? outcome.GetSubject(0)
            : default;
        GameplayOutcomeFact fact = outcome.FactCount == 1
            ? outcome.GetFact(0)
            : default;
        GameplayOutcomeProvenanceReference provenance =
            outcome.ProvenanceCount == 1
                ? outcome.GetProvenance(0)
                : default;
        GameplayResultKey expectedKey = participant.EntityId.IsValid
            && CaptivePerformerMilestoneOutcomeAdapter.IsThreshold(threshold)
            && revision > 0L
                ? CaptivePerformerMilestoneOutcomeIds.ResultKey(
                    participant.EntityId.Value,
                    threshold,
                    revision)
                : default;
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && captiveCount == 1
            && outcome.ParticipantCount == 1
            && outcome.SubjectCount == 1
            && outcome.MetricCount == 3
            && outcome.TagCount == 1
            && outcome.ProvenanceCount == 1
            && outcome.FactCount == 1
            && outcome.Status == GameplayOutcomeStatus.Succeeded
            && hasThreshold
            && hasFame
            && hasRevision
            && CaptivePerformerMilestoneOutcomeAdapter.IsThreshold(threshold)
            && fame >= threshold
            && fame <= 100d
            && revisionValue == revision
            && revision > 0L
            && outcome.OwnerRevision == revision
            && outcome.ResultKey == expectedKey
            && participant.EntityId.Kind.Equals(
                CaptivePerformerMilestoneOutcomeIds.CharacterKind)
            && participant.ParticipationKind == GameplayParticipationKind.Direct
            && participant.HasPerceptionEvidence
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(
                participant.DisplayName)
            && subject.SubjectId.Equals(participant.EntityId)
            && Math.Abs(
                subject.Salience
                - CaptivePerformerMilestoneOutcomeAdapter.Salience(threshold))
                < 0.0001f
            && subject.Tier == (threshold == 100
                ? NarrativeMemoryTier.Core
                : NarrativeMemoryTier.Recent)
            && subject.IsPinned == (threshold == 100)
            && !subject.IsOptionalWitness
            && outcome.GetTag(0).Equals(
                CaptivePerformerMilestoneOutcomeIds.PerformerTag)
            && provenance.Equals(CaptivePerformerMilestoneOutcomeIds.Source)
            && fact.FactId.Equals(
                CaptivePerformerMilestoneOutcomeIds.UnlockKindFact)
            && string.Equals(
                fact.Value,
                CaptivePerformerMilestoneOutcomeIds.UnlockKind(threshold),
                StringComparison.Ordinal);
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "captive-performer-milestone-shape-invalid");
    }

    private static double Metric(
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

    private static bool TryMetric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId metricId,
        out double value)
    {
        value = 0d;
        int count = 0;
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (!metric.MetricId.Equals(metricId))
                continue;
            count++;
            value = metric.Value;
        }
        return count == 1;
    }
}
