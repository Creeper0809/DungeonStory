using System;
using DungeonStory.Narrative.Korean;

public enum CaptiveEscapeOutcomeKind
{
    PhysicalEscape = 1,
    ArrivalCustodyLoss = 2,
    FalseComplianceBetrayal = 3,
    MinionControlBreak = 4
}

public static class CaptiveEscapeOutcomeIds
{
    public const string ProducerId = "captivity.captive-escape";

    public static readonly GameplayOutcomeTypeId Escaped =
        new("captivity.captive-escaped");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayRoleId CaptiveRole = new("escaped-captive");
    public static readonly GameplayMetricId RetaliationPressureMetric =
        new("captivity.captive-escape.retaliation-pressure");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("captivity.captive-escape.owner-revision");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId EscapeTag = new("captive-escape");
    public static readonly GameplayOutcomeTagId BetrayalTag = new("betrayal");
    public static readonly GameplayOutcomeFactId KindFact =
        new("captivity.captive-escape.kind");
    public static readonly GameplayOutcomeFactId TriggerFact =
        new("captivity.captive-escape.trigger");
    public static readonly GameplayOutcomeFactId PreviousStatusFact =
        new("captivity.captive-escape.previous-status");
    public static readonly GameplayOutcomeProvenanceReference Source =
        new("runtime-receipt", "captive-escape-v1");

    public static GameplayResultKey ResultKey(string captiveId, long ownerRevision) =>
        new(
            ProducerId,
            new GameplayOperationId($"captive-escape:{captiveId}"),
            ownerRevision,
            0);

    public static string KindId(CaptiveEscapeOutcomeKind kind) => kind switch
    {
        CaptiveEscapeOutcomeKind.PhysicalEscape => "physical-escape",
        CaptiveEscapeOutcomeKind.ArrivalCustodyLoss => "arrival-custody-loss",
        CaptiveEscapeOutcomeKind.FalseComplianceBetrayal =>
            "false-compliance-betrayal",
        CaptiveEscapeOutcomeKind.MinionControlBreak => "minion-control-break",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static bool IsBetrayal(CaptiveEscapeOutcomeKind kind) =>
        kind is CaptiveEscapeOutcomeKind.FalseComplianceBetrayal
            or CaptiveEscapeOutcomeKind.MinionControlBreak;

    public static bool IsPreviousStatusValid(
        CaptiveEscapeOutcomeKind kind,
        CaptivityStatus status) => kind switch
        {
            CaptiveEscapeOutcomeKind.PhysicalEscape =>
                status == CaptivityStatus.EscapeAttempt,
            CaptiveEscapeOutcomeKind.ArrivalCustodyLoss =>
                status == CaptivityStatus.AwaitingCapture,
            CaptiveEscapeOutcomeKind.FalseComplianceBetrayal =>
                status is CaptivityStatus.Confined
                    or CaptivityStatus.Labor
                    or CaptivityStatus.Performer,
            CaptiveEscapeOutcomeKind.MinionControlBreak =>
                status == CaptivityStatus.Minion,
            _ => false
        };
}

public readonly struct CaptiveEscapeOutcomeReceipt
{
    public CaptiveEscapeOutcomeReceipt(
        string captiveId,
        KoreanNameSnapshot captiveName,
        CaptiveEscapeOutcomeKind kind,
        string trigger,
        bool betrayal,
        CaptivityStatus previousStatus,
        float retaliationPressure,
        long ownerRevision,
        int absoluteDay)
    {
        CharacterId characterId = new(captiveId);
        string normalizedTrigger = trigger?.Trim() ?? string.Empty;
        if (!characterId.IsValid)
            throw new ArgumentException(
                "A canonical captive character ID is required.",
                nameof(captiveId));
        if (!GameplayOutcomeLedger.IsValidDisplayNameSnapshot(captiveName))
            throw new ArgumentException(
                "A frozen captive display name is required.",
                nameof(captiveName));
        _ = CaptiveEscapeOutcomeIds.KindId(kind);
        if (normalizedTrigger.Length == 0)
            throw new ArgumentException(
                "A normalized escape trigger is required.",
                nameof(trigger));
        if (betrayal != CaptiveEscapeOutcomeIds.IsBetrayal(kind))
            throw new ArgumentException(
                "Escape kind and betrayal classification disagree.",
                nameof(betrayal));
        if (!CaptiveEscapeOutcomeIds.IsPreviousStatusValid(kind, previousStatus))
            throw new ArgumentException(
                "Escape kind and previous captivity status disagree.",
                nameof(previousStatus));
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
        Kind = kind;
        Trigger = normalizedTrigger;
        Betrayal = betrayal;
        PreviousStatus = previousStatus;
        RetaliationPressure = retaliationPressure;
        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
    }

    public CharacterId CaptiveId { get; }
    public KoreanNameSnapshot CaptiveName { get; }
    public CaptiveEscapeOutcomeKind Kind { get; }
    public string Trigger { get; }
    public bool Betrayal { get; }
    public CaptivityStatus PreviousStatus { get; }
    public float RetaliationPressure { get; }
    public long OwnerRevision { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey =>
        CaptiveEscapeOutcomeIds.ResultKey(CaptiveId.Value, OwnerRevision);
}

public sealed class CaptiveEscapeOutcomeAdapter :
    GameplayOutcomeAdapter<CaptiveEscapeOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        CaptiveEscapeOutcomeIds.Escaped;

    public override OutcomePrepareResult TryGetRequirements(
        in CaptiveEscapeOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        bool valid = receipt.CaptiveId.IsValid
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(receipt.CaptiveName)
            && receipt.Trigger.Length > 0
            && receipt.Betrayal == CaptiveEscapeOutcomeIds.IsBetrayal(receipt.Kind)
            && CaptiveEscapeOutcomeIds.IsPreviousStatusValid(
                receipt.Kind,
                receipt.PreviousStatus)
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
                "captive-escape-receipt-invalid");
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
            metricCount: 2,
            tagCount: receipt.Betrayal ? 2 : 1,
            anchorCount: 0,
            provenanceCount: 1,
            factCount: 3);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in CaptiveEscapeOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId captive = Character(receipt.CaptiveId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                captive,
                CaptiveEscapeOutcomeIds.CaptiveRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.CaptiveName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                captive,
                Salience(receipt.Kind),
                NarrativeMemoryTier.Episodic,
                false,
                false,
                0))
            && AddMetric(
                ref builder,
                CaptiveEscapeOutcomeIds.RetaliationPressureMetric,
                receipt.RetaliationPressure,
                CaptiveEscapeOutcomeIds.PointUnit,
                captive)
            && AddMetric(
                ref builder,
                CaptiveEscapeOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                CaptiveEscapeOutcomeIds.RevisionUnit,
                captive)
            && builder.AddTag(CaptiveEscapeOutcomeIds.EscapeTag);
        if (written && receipt.Betrayal)
            written = builder.AddTag(CaptiveEscapeOutcomeIds.BetrayalTag);
        written = written
            && builder.AddProvenance(CaptiveEscapeOutcomeIds.Source)
            && builder.AddFact(new GameplayOutcomeFact(
                CaptiveEscapeOutcomeIds.KindFact,
                CaptiveEscapeOutcomeIds.KindId(receipt.Kind)))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptiveEscapeOutcomeIds.TriggerFact,
                receipt.Trigger))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptiveEscapeOutcomeIds.PreviousStatusFact,
                receipt.PreviousStatus.ToString()));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "captive-escape-write-failed");
    }

    internal static float Salience(CaptiveEscapeOutcomeKind kind) => kind switch
    {
        CaptiveEscapeOutcomeKind.PhysicalEscape => 0.9f,
        CaptiveEscapeOutcomeKind.ArrivalCustodyLoss => 0.84f,
        CaptiveEscapeOutcomeKind.FalseComplianceBetrayal => 0.94f,
        CaptiveEscapeOutcomeKind.MinionControlBreak => 0.96f,
        _ => 0f
    };

    private static GameplayEntityId Character(CharacterId id) => new(
        CaptiveEscapeOutcomeIds.CharacterKind,
        id.Value);

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId subject) => builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, subject));
}

public readonly struct PreparedCaptiveEscapeOutcome
{
    public PreparedCaptiveEscapeOutcome(
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

public interface ICaptiveEscapeOutcomeCommitter
{
    bool TryPrepare(
        in CaptiveEscapeOutcomeReceipt receipt,
        out PreparedCaptiveEscapeOutcome prepared,
        out string failureReason);
    OwnerOutcomeCommitResult Commit(in PreparedCaptiveEscapeOutcome prepared);
    void Cancel(in PreparedCaptiveEscapeOutcome prepared);
}

public sealed class CaptiveEscapeGameplayOutcomeBridge :
    ICaptiveEscapeOutcomeCommitter
{
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public CaptiveEscapeGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
    }

    public bool TryPrepare(
        in CaptiveEscapeOutcomeReceipt receipt,
        out PreparedCaptiveEscapeOutcome prepared,
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
        prepared = new PreparedCaptiveEscapeOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            replay.DurablyCommitted);
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedCaptiveEscapeOutcome prepared) => prepared.IsReplay
        ? transactions.Reconcile(prepared.ResultKey)
        : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public void Cancel(in PreparedCaptiveEscapeOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }
}

internal sealed class CaptiveEscapeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":" + Fact(
            outcome,
            CaptiveEscapeOutcomeIds.KindFact));

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        CaptiveEscapeOutcomeKind kind = ParseKind(Fact(
            outcome,
            CaptiveEscapeOutcomeIds.KindFact));
        return new OutcomeMemoryEvaluation(
            CaptiveEscapeOutcomeAdapter.Salience(kind),
            NarrativeMemoryTier.Episodic,
            Math.Max(evaluationDay + 60, outcome.AbsoluteDay + 60));
    }

    internal static string Fact(
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

    internal static CaptiveEscapeOutcomeKind ParseKind(string value)
    {
        foreach (CaptiveEscapeOutcomeKind kind in Enum.GetValues(
                     typeof(CaptiveEscapeOutcomeKind)))
        {
            if (string.Equals(
                    CaptiveEscapeOutcomeIds.KindId(kind),
                    value,
                    StringComparison.Ordinal))
            {
                return kind;
            }
        }
        return 0;
    }
}

internal sealed class CaptiveEscapePerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class CaptiveEscapeConsolidator : IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class CaptiveEscapePerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "captive-escape-v1";
    private readonly IKoreanJosaFormatter josa;

    public CaptiveEscapePerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant captive = outcome.ParticipantCount > 0
            ? outcome.GetParticipant(0)
            : default;
        CaptiveEscapeOutcomeKind kind = CaptiveEscapeMemoryPolicy.ParseKind(
            CaptiveEscapeMemoryPolicy.Fact(
                outcome,
                CaptiveEscapeOutcomeIds.KindFact));
        string trigger = CaptiveEscapeMemoryPolicy.Fact(
            outcome,
            CaptiveEscapeOutcomeIds.TriggerFact);
        KoreanJosaFormatResult subject = josa.Format(new KoreanJosaRequest(
            captive.DisplayName,
            KoreanJosaKind.Subject));
        bool neutral = subject.RequiresNeutralFrame;
        string action = kind switch
        {
            CaptiveEscapeOutcomeKind.PhysicalEscape =>
                $"'{trigger}' 끝에 수용지를 탈출했다",
            CaptiveEscapeOutcomeKind.ArrivalCustodyLoss =>
                $"'{trigger}' 과정에서 수용되기 전에 이탈했다",
            CaptiveEscapeOutcomeKind.FalseComplianceBetrayal =>
                $"'{trigger}' 상황에서 거짓 복종을 깨고 배신했다",
            CaptiveEscapeOutcomeKind.MinionControlBreak =>
                $"'{trigger}' 끝에 정착지 통제를 깨고 이탈했다",
            _ => "수용 상태에서 이탈했다"
        };
        string text = neutral
            ? $"이탈자: {captive.DisplayName.DisplayText} · {action}."
            : $"{subject.Text} {action}.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }
}

public sealed class CaptiveEscapeOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new CaptiveEscapeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new CaptiveEscapePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new CaptiveEscapeConsolidator();

    public CaptiveEscapeOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new CaptiveEscapePerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId => CaptiveEscapeOutcomeIds.Escaped;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(CaptiveEscapeOutcomeIds.CaptiveRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(CaptiveEscapeOutcomeIds.RetaliationPressureMetric)
            && unitId.Equals(CaptiveEscapeOutcomeIds.PointUnit)
        || metricId.Equals(CaptiveEscapeOutcomeIds.OwnerRevisionMetric)
            && unitId.Equals(CaptiveEscapeOutcomeIds.RevisionUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        GameplayOutcomeParticipant participant = outcome.ParticipantCount == 1
            ? outcome.GetParticipant(0)
            : default;
        GameplayOutcomeSubjectLink subject = outcome.SubjectCount == 1
            ? outcome.GetSubject(0)
            : default;
        string kindValue = CaptiveEscapeMemoryPolicy.Fact(
            outcome,
            CaptiveEscapeOutcomeIds.KindFact);
        string trigger = CaptiveEscapeMemoryPolicy.Fact(
            outcome,
            CaptiveEscapeOutcomeIds.TriggerFact);
        string previousStatusValue = CaptiveEscapeMemoryPolicy.Fact(
            outcome,
            CaptiveEscapeOutcomeIds.PreviousStatusFact);
        CaptiveEscapeOutcomeKind kind = CaptiveEscapeMemoryPolicy.ParseKind(kindValue);
        bool betrayal = CaptiveEscapeOutcomeIds.IsBetrayal(kind);
        bool previousStatusParsed = Enum.TryParse(
            previousStatusValue,
            out CaptivityStatus previousStatus);
        bool hasPressure = TryMetric(
            outcome,
            CaptiveEscapeOutcomeIds.RetaliationPressureMetric,
            out double pressure);
        bool hasRevision = TryMetric(
            outcome,
            CaptiveEscapeOutcomeIds.OwnerRevisionMetric,
            out double revisionValue);
        long revision = (long)revisionValue;
        GameplayResultKey expectedKey = participant.EntityId.IsValid
            && revision > 0L
                ? CaptiveEscapeOutcomeIds.ResultKey(
                    participant.EntityId.Value,
                    revision)
                : default;
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && outcome.Status == GameplayOutcomeStatus.Succeeded
            && outcome.ParticipantCount == 1
            && outcome.SubjectCount == 1
            && outcome.MetricCount == 2
            && outcome.TagCount == (betrayal ? 2 : 1)
            && outcome.ProvenanceCount == 1
            && outcome.FactCount == 3
            && kind != 0
            && previousStatusParsed
            && CaptiveEscapeOutcomeIds.IsPreviousStatusValid(kind, previousStatus)
            && trigger.Length > 0
            && string.Equals(trigger, trigger.Trim(), StringComparison.Ordinal)
            && hasPressure
            && pressure >= 0d
            && pressure <= 100d
            && hasRevision
            && revisionValue == revision
            && revision > 0L
            && outcome.OwnerRevision == revision
            && outcome.ResultKey == expectedKey
            && participant.RoleId.Equals(CaptiveEscapeOutcomeIds.CaptiveRole)
            && participant.EntityId.Kind.Equals(
                CaptiveEscapeOutcomeIds.CharacterKind)
            && participant.ParticipationKind == GameplayParticipationKind.Direct
            && participant.HasPerceptionEvidence
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(
                participant.DisplayName)
            && subject.SubjectId.Equals(participant.EntityId)
            && Math.Abs(
                subject.Salience - CaptiveEscapeOutcomeAdapter.Salience(kind))
                < 0.0001f
            && subject.Tier == NarrativeMemoryTier.Recent
            && !subject.IsPinned
            && !subject.IsOptionalWitness
            && outcome.GetTag(0).Equals(CaptiveEscapeOutcomeIds.EscapeTag)
            && (!betrayal
                || outcome.GetTag(1).Equals(CaptiveEscapeOutcomeIds.BetrayalTag))
            && outcome.GetProvenance(0).Equals(CaptiveEscapeOutcomeIds.Source)
            && outcome.GetFact(0).FactId.Equals(CaptiveEscapeOutcomeIds.KindFact)
            && string.Equals(outcome.GetFact(0).Value, kindValue, StringComparison.Ordinal)
            && outcome.GetFact(1).FactId.Equals(CaptiveEscapeOutcomeIds.TriggerFact)
            && string.Equals(outcome.GetFact(1).Value, trigger, StringComparison.Ordinal)
            && outcome.GetFact(2).FactId.Equals(
                CaptiveEscapeOutcomeIds.PreviousStatusFact)
            && string.Equals(
                outcome.GetFact(2).Value,
                previousStatusValue,
                StringComparison.Ordinal);
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("captive-escape-shape-invalid");
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
