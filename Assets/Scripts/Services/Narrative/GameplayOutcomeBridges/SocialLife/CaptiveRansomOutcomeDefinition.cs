using System;
using System.Globalization;
using DungeonStory.Narrative.Korean;

public static class CaptiveRansomOutcomeIds
{
    public const string ProducerId = "captivity.captive-ransom";

    public static readonly GameplayOutcomeTypeId Ransomed =
        new("captivity.captive-ransomed");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayRoleId CaptiveRole = new("ransomed-captive");
    public static readonly GameplayMetricId AmountMetric =
        new("captivity.captive-ransom.amount");
    public static readonly GameplayMetricId RetaliationPressureMetric =
        new("captivity.captive-ransom.retaliation-pressure");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("captivity.captive-ransom.owner-revision");
    public static readonly GameplayMetricUnitId GoldUnit = new("gold");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId RansomTag = new("captive-ransom");
    public static readonly GameplayOutcomeFactId PreviousStatusFact =
        new("captivity.captive-ransom.previous-status");
    public static readonly GameplayOutcomeProvenanceReference Source =
        new("runtime-receipt", "captive-ransom-v1");

    public static GameplayResultKey ResultKey(string captiveId, long ownerRevision) =>
        new(
            ProducerId,
            new GameplayOperationId($"captive-ransom:{captiveId}"),
            ownerRevision,
            0);

    public static bool IsPreviousStatusValid(CaptivityStatus status) =>
        status is CaptivityStatus.AwaitingCapture
            or CaptivityStatus.Stabilizing
            or CaptivityStatus.AwaitingEscort
            or CaptivityStatus.Confined
            or CaptivityStatus.Labor;
}

public readonly struct CaptiveRansomOutcomeReceipt
{
    public CaptiveRansomOutcomeReceipt(
        string captiveId,
        KoreanNameSnapshot captiveName,
        CaptivityStatus previousStatus,
        int amount,
        float retaliationPressure,
        long ownerRevision,
        int absoluteDay)
    {
        CharacterId characterId = new(captiveId);
        if (!characterId.IsValid)
            throw new ArgumentException(
                "A canonical captive character ID is required.",
                nameof(captiveId));
        if (!GameplayOutcomeLedger.IsValidDisplayNameSnapshot(captiveName))
            throw new ArgumentException(
                "A frozen captive display name is required.",
                nameof(captiveName));
        if (!CaptiveRansomOutcomeIds.IsPreviousStatusValid(previousStatus))
            throw new ArgumentOutOfRangeException(nameof(previousStatus));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (float.IsNaN(retaliationPressure)
            || float.IsInfinity(retaliationPressure)
            || retaliationPressure < 0f
            || retaliationPressure > 100f)
        {
            throw new ArgumentOutOfRangeException(nameof(retaliationPressure));
        }
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        CaptiveId = characterId;
        CaptiveName = captiveName;
        PreviousStatus = previousStatus;
        Amount = amount;
        RetaliationPressure = retaliationPressure;
        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
    }

    public CharacterId CaptiveId { get; }
    public KoreanNameSnapshot CaptiveName { get; }
    public CaptivityStatus PreviousStatus { get; }
    public int Amount { get; }
    public float RetaliationPressure { get; }
    public long OwnerRevision { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey =>
        CaptiveRansomOutcomeIds.ResultKey(CaptiveId.Value, OwnerRevision);
}

public sealed class CaptiveRansomOutcomeAdapter :
    GameplayOutcomeAdapter<CaptiveRansomOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        CaptiveRansomOutcomeIds.Ransomed;

    public override OutcomePrepareResult TryGetRequirements(
        in CaptiveRansomOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        bool valid = receipt.CaptiveId.IsValid
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(receipt.CaptiveName)
            && CaptiveRansomOutcomeIds.IsPreviousStatusValid(receipt.PreviousStatus)
            && receipt.Amount > 0
            && !float.IsNaN(receipt.RetaliationPressure)
            && !float.IsInfinity(receipt.RetaliationPressure)
            && receipt.RetaliationPressure >= 0f
            && receipt.RetaliationPressure <= 100f
            && receipt.OwnerRevision > 0L
            && receipt.AbsoluteDay >= 0
            && receipt.ResultKey.IsValid;
        if (!valid)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "captive-ransom-receipt-invalid");
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
        in CaptiveRansomOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId captive = Character(receipt.CaptiveId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                captive,
                CaptiveRansomOutcomeIds.CaptiveRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.CaptiveName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                captive,
                0.84f,
                NarrativeMemoryTier.Episodic,
                false,
                false,
                0))
            && AddMetric(
                ref builder,
                CaptiveRansomOutcomeIds.AmountMetric,
                receipt.Amount,
                CaptiveRansomOutcomeIds.GoldUnit,
                captive)
            && AddMetric(
                ref builder,
                CaptiveRansomOutcomeIds.RetaliationPressureMetric,
                receipt.RetaliationPressure,
                CaptiveRansomOutcomeIds.PointUnit,
                captive)
            && AddMetric(
                ref builder,
                CaptiveRansomOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                CaptiveRansomOutcomeIds.RevisionUnit,
                captive)
            && builder.AddTag(CaptiveRansomOutcomeIds.RansomTag)
            && builder.AddProvenance(CaptiveRansomOutcomeIds.Source)
            && builder.AddFact(new GameplayOutcomeFact(
                CaptiveRansomOutcomeIds.PreviousStatusFact,
                receipt.PreviousStatus.ToString()));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "captive-ransom-write-failed");
    }

    private static GameplayEntityId Character(CharacterId id) => new(
        CaptiveRansomOutcomeIds.CharacterKind,
        id.Value);

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId subject) => builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, subject));
}

public readonly struct PreparedCaptiveRansomOutcome
{
    public PreparedCaptiveRansomOutcome(
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

public interface ICaptiveRansomOutcomeCommitter
{
    bool TryPrepare(
        in CaptiveRansomOutcomeReceipt receipt,
        out PreparedCaptiveRansomOutcome prepared,
        out string failureReason);
    OwnerOutcomeCommitResult Commit(in PreparedCaptiveRansomOutcome prepared);
    void Cancel(in PreparedCaptiveRansomOutcome prepared);
}

public sealed class CaptiveRansomGameplayOutcomeBridge :
    ICaptiveRansomOutcomeCommitter
{
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public CaptiveRansomGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
    }

    public bool TryPrepare(
        in CaptiveRansomOutcomeReceipt receipt,
        out PreparedCaptiveRansomOutcome prepared,
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
        prepared = new PreparedCaptiveRansomOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            replay.DurablyCommitted);
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedCaptiveRansomOutcome prepared) => prepared.IsReplay
        ? transactions.Reconcile(prepared.ResultKey)
        : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public void Cancel(in PreparedCaptiveRansomOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }
}

internal sealed class CaptiveRansomMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(outcome.OutcomeTypeId.Value);

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay) => new(
        0.84f,
        NarrativeMemoryTier.Episodic,
        Math.Max(evaluationDay + 60, outcome.AbsoluteDay + 60));
}

internal sealed class CaptiveRansomPerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class CaptiveRansomConsolidator : IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class CaptiveRansomPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "captive-ransom-v1";
    private readonly IKoreanJosaFormatter josa;

    public CaptiveRansomPerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant captive = outcome.ParticipantCount > 0
            ? outcome.GetParticipant(0)
            : default;
        TryMetric(outcome, CaptiveRansomOutcomeIds.AmountMetric, out double amount);
        KoreanJosaFormatResult topic = josa.Format(new KoreanJosaRequest(
            captive.DisplayName,
            KoreanJosaKind.Topic));
        string amountText = amount.ToString("N0", CultureInfo.InvariantCulture);
        bool neutral = topic.RequiresNeutralFrame;
        string text = neutral
            ? $"석방자: {captive.DisplayName.DisplayText} · 몸값 {amountText} 골드를 남기고 풀려났다."
            : $"{topic.Text} 몸값 {amountText} 골드를 남기고 풀려났다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }

    internal static bool TryMetric(
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

public sealed class CaptiveRansomOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new CaptiveRansomMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new CaptiveRansomPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new CaptiveRansomConsolidator();

    public CaptiveRansomOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new CaptiveRansomPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId => CaptiveRansomOutcomeIds.Ransomed;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(CaptiveRansomOutcomeIds.CaptiveRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(CaptiveRansomOutcomeIds.AmountMetric)
            && unitId.Equals(CaptiveRansomOutcomeIds.GoldUnit)
        || metricId.Equals(CaptiveRansomOutcomeIds.RetaliationPressureMetric)
            && unitId.Equals(CaptiveRansomOutcomeIds.PointUnit)
        || metricId.Equals(CaptiveRansomOutcomeIds.OwnerRevisionMetric)
            && unitId.Equals(CaptiveRansomOutcomeIds.RevisionUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        GameplayOutcomeParticipant participant = outcome.ParticipantCount == 1
            ? outcome.GetParticipant(0)
            : default;
        GameplayOutcomeSubjectLink subject = outcome.SubjectCount == 1
            ? outcome.GetSubject(0)
            : default;
        string previousStatusValue = Fact(
            outcome,
            CaptiveRansomOutcomeIds.PreviousStatusFact);
        bool statusParsed = Enum.TryParse(
            previousStatusValue,
            out CaptivityStatus previousStatus);
        bool hasAmount = CaptiveRansomPerspectiveProjector.TryMetric(
            outcome,
            CaptiveRansomOutcomeIds.AmountMetric,
            out double amount);
        bool hasPressure = CaptiveRansomPerspectiveProjector.TryMetric(
            outcome,
            CaptiveRansomOutcomeIds.RetaliationPressureMetric,
            out double pressure);
        bool hasRevision = CaptiveRansomPerspectiveProjector.TryMetric(
            outcome,
            CaptiveRansomOutcomeIds.OwnerRevisionMetric,
            out double revisionValue);
        long revision = (long)revisionValue;
        GameplayResultKey expectedKey = participant.EntityId.IsValid
            && revision > 0L
                ? CaptiveRansomOutcomeIds.ResultKey(
                    participant.EntityId.Value,
                    revision)
                : default;
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && outcome.Status == GameplayOutcomeStatus.Succeeded
            && outcome.ParticipantCount == 1
            && outcome.SubjectCount == 1
            && outcome.MetricCount == 3
            && outcome.TagCount == 1
            && outcome.ProvenanceCount == 1
            && outcome.FactCount == 1
            && statusParsed
            && CaptiveRansomOutcomeIds.IsPreviousStatusValid(previousStatus)
            && hasAmount
            && amount > 0d
            && amount == Math.Truncate(amount)
            && hasPressure
            && pressure >= 0d
            && pressure <= 100d
            && hasRevision
            && revisionValue == revision
            && revision > 0L
            && outcome.OwnerRevision == revision
            && outcome.ResultKey == expectedKey
            && participant.RoleId.Equals(CaptiveRansomOutcomeIds.CaptiveRole)
            && participant.EntityId.Kind.Equals(
                CaptiveRansomOutcomeIds.CharacterKind)
            && participant.ParticipationKind == GameplayParticipationKind.Direct
            && participant.HasPerceptionEvidence
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(
                participant.DisplayName)
            && subject.SubjectId.Equals(participant.EntityId)
            && Math.Abs(subject.Salience - 0.84f) < 0.0001f
            && subject.Tier == NarrativeMemoryTier.Recent
            && !subject.IsPinned
            && !subject.IsOptionalWitness
            && outcome.GetTag(0).Equals(CaptiveRansomOutcomeIds.RansomTag)
            && outcome.GetProvenance(0).Equals(CaptiveRansomOutcomeIds.Source)
            && outcome.GetFact(0).FactId.Equals(
                CaptiveRansomOutcomeIds.PreviousStatusFact)
            && string.Equals(
                outcome.GetFact(0).Value,
                previousStatusValue,
                StringComparison.Ordinal);
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("captive-ransom-shape-invalid");
    }

    private static string Fact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId factId)
    {
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(factId))
                return fact.Value;
        }
        return string.Empty;
    }
}
