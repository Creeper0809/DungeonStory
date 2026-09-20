using System;
using DungeonStory.Narrative.Korean;

public static class CaptivityInteractionOutcomeIds
{
    public const string ProducerId = "captivity.interaction-result";

    public static readonly GameplayOutcomeTypeId Resolved =
        new("captivity.interaction-resolved");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayRoleId CaptiveRole = new("interaction-captive");
    public static readonly GameplayRoleId WardenRole = new("interaction-warden");
    public static readonly GameplayRoleId FacilityRole = new("interaction-facility");
    public static readonly GameplayMetricId WillDeltaMetric =
        new("captivity.interaction.will-delta");
    public static readonly GameplayMetricId FearDeltaMetric =
        new("captivity.interaction.fear-delta");
    public static readonly GameplayMetricId TrustDeltaMetric =
        new("captivity.interaction.trust-delta");
    public static readonly GameplayMetricId GrudgeDeltaMetric =
        new("captivity.interaction.grudge-delta");
    public static readonly GameplayMetricId CorruptionDeltaMetric =
        new("captivity.interaction.corruption-delta");
    public static readonly GameplayMetricId BodyDamageMetric =
        new("captivity.interaction.body-damage");
    public static readonly GameplayMetricId BodyHealthBeforeMetric =
        new("captivity.interaction.body-health-before");
    public static readonly GameplayMetricId BodyHealthAfterMetric =
        new("captivity.interaction.body-health-after");
    public static readonly GameplayMetricId OutputAmountMetric =
        new("captivity.interaction.output-amount");
    public static readonly GameplayMetricId AttemptMetric =
        new("captivity.interaction.attempt");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("captivity.interaction.owner-revision");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId HealthUnit = new("health-point");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId CaptivityTag =
        new("domain:captivity");
    public static readonly GameplayOutcomeTagId InteractionTag =
        new("captivity-interaction");
    public static readonly GameplayOutcomeFactId InteractionIdFact =
        new("captivity.interaction.id");
    public static readonly GameplayOutcomeFactId InteractionKindFact =
        new("captivity.interaction.kind");
    public static readonly GameplayOutcomeFactId InteractionDisplayFact =
        new("captivity.interaction.display-name");
    public static readonly GameplayOutcomeFactId MessageFact =
        new("captivity.interaction.message");
    public static readonly GameplayOutcomeFactId SuccessFact =
        new("captivity.interaction.success");
    public static readonly GameplayOutcomeFactId OutputItemFact =
        new("captivity.interaction.output-item-id");
    public static readonly GameplayOutcomeProvenanceReference Source =
        new("runtime-receipt", "captivity-interaction-result-v1");

    public static GameplayResultKey ResultKey(
        string captiveId,
        int attemptId,
        long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId(
            $"captivity-interaction:{captiveId}:{attemptId:D8}"),
        ownerRevision,
        0);
}

public static class CaptivityInteractionOutcomeNames
{
    public static KoreanNameSnapshot Snapshot(
        string role,
        string identity,
        string displayText)
    {
        string id = GameplayOutcomeStableIdSyntax.Require(
            identity,
            nameof(identity));
        string display = displayText?.Trim() ?? string.Empty;
        if (display.Length == 0)
            throw new ArgumentException(
                "A captivity-interaction display snapshot is required.",
                nameof(displayText));
        string revision = "captivity-interaction-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(
                (role ?? string.Empty) + "|" + id + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }
}

public readonly struct CaptivityInteractionOutcomeReceipt
{
    public CaptivityInteractionOutcomeReceipt(
        string captiveId,
        KoreanNameSnapshot captiveName,
        string wardenId,
        KoreanNameSnapshot wardenName,
        string facilityId,
        KoreanNameSnapshot facilityName,
        int positionX,
        int positionY,
        string interactionId,
        CaptiveInteractionKind interactionKind,
        string interactionDisplayName,
        bool success,
        string message,
        float willDelta,
        float fearDelta,
        float trustDelta,
        float grudgeDelta,
        float corruptionDelta,
        float bodyDamage,
        float bodyHealthBefore,
        float bodyHealthAfter,
        string outputItemId,
        int outputAmount,
        int attemptId,
        long ownerRevision,
        int absoluteDay)
    {
        CharacterId captive = new(captiveId);
        CharacterId warden = new(wardenId);
        BuildingInstanceId facility = new(facilityId);
        string interaction = interactionId?.Trim() ?? string.Empty;
        string display = interactionDisplayName?.Trim() ?? string.Empty;
        string resultMessage = message?.Trim() ?? string.Empty;
        string output = outputItemId?.Trim() ?? string.Empty;
        bool effectsValid = IsFiniteDelta(willDelta)
            && IsFiniteDelta(fearDelta)
            && IsFiniteDelta(trustDelta)
            && IsFiniteDelta(grudgeDelta)
            && IsFiniteDelta(corruptionDelta)
            && IsFiniteNonNegative(bodyDamage)
            && IsFiniteNonNegative(bodyHealthBefore)
            && IsFiniteNonNegative(bodyHealthAfter)
            && bodyHealthAfter <= bodyHealthBefore + 0.001f;
        bool outputValid = outputAmount == 0
            ? output.Length == 0
            : outputAmount > 0
                && output.Length > 0
                && string.Equals(output, outputItemId, StringComparison.Ordinal);
        bool failedShapeValid = success
            || (Math.Abs(willDelta) < 0.0001f
                && Math.Abs(fearDelta) < 0.0001f
                && Math.Abs(trustDelta) < 0.0001f
                && Math.Abs(grudgeDelta) < 0.0001f
                && Math.Abs(corruptionDelta) < 0.0001f
                && bodyDamage <= 0f
                && outputAmount == 0);
        if (!captive.IsValid
            || !warden.IsValid
            || !facility.IsValid
            || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(captiveName)
            || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(wardenName)
            || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(facilityName)
            || interaction.Length == 0
            || !string.Equals(interaction, interactionId, StringComparison.Ordinal)
            || !Enum.IsDefined(typeof(CaptiveInteractionKind), interactionKind)
            || display.Length == 0
            || resultMessage.Length == 0
            || !effectsValid
            || !outputValid
            || !failedShapeValid
            || (bodyDamage > 0f && bodyHealthBefore <= 0f)
            || attemptId <= 0
            || ownerRevision <= 0L
            || absoluteDay < 0)
        {
            throw new ArgumentException(
                "Captivity interaction outcome receipt is invalid.");
        }

        CaptiveId = captive;
        CaptiveName = captiveName;
        WardenId = warden;
        WardenName = wardenName;
        FacilityId = facility;
        FacilityName = facilityName;
        PositionX = positionX;
        PositionY = positionY;
        InteractionId = interaction;
        InteractionKind = interactionKind;
        InteractionDisplayName = display;
        Success = success;
        Message = resultMessage;
        WillDelta = willDelta;
        FearDelta = fearDelta;
        TrustDelta = trustDelta;
        GrudgeDelta = grudgeDelta;
        CorruptionDelta = corruptionDelta;
        BodyDamage = bodyDamage;
        BodyHealthBefore = bodyHealthBefore;
        BodyHealthAfter = bodyHealthAfter;
        OutputItemId = output;
        OutputAmount = outputAmount;
        AttemptId = attemptId;
        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
    }

    public CharacterId CaptiveId { get; }
    public KoreanNameSnapshot CaptiveName { get; }
    public CharacterId WardenId { get; }
    public KoreanNameSnapshot WardenName { get; }
    public BuildingInstanceId FacilityId { get; }
    public KoreanNameSnapshot FacilityName { get; }
    public int PositionX { get; }
    public int PositionY { get; }
    public string InteractionId { get; }
    public CaptiveInteractionKind InteractionKind { get; }
    public string InteractionDisplayName { get; }
    public bool Success { get; }
    public string Message { get; }
    public float WillDelta { get; }
    public float FearDelta { get; }
    public float TrustDelta { get; }
    public float GrudgeDelta { get; }
    public float CorruptionDelta { get; }
    public float BodyDamage { get; }
    public float BodyHealthBefore { get; }
    public float BodyHealthAfter { get; }
    public string OutputItemId { get; }
    public int OutputAmount { get; }
    public int AttemptId { get; }
    public long OwnerRevision { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey => CaptivityInteractionOutcomeIds.ResultKey(
        CaptiveId.Value,
        AttemptId,
        OwnerRevision);

    private static bool IsFiniteDelta(float value) =>
        float.IsFinite(value) && value >= -100f && value <= 100f;

    private static bool IsFiniteNonNegative(float value) =>
        float.IsFinite(value) && value >= 0f;
}

public sealed class CaptivityInteractionOutcomeAdapter :
    GameplayOutcomeAdapter<CaptivityInteractionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        CaptivityInteractionOutcomeIds.Resolved;

    public override OutcomePrepareResult TryGetRequirements(
        in CaptivityInteractionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid
            || !receipt.CaptiveId.IsValid
            || !receipt.WardenId.IsValid
            || !receipt.FacilityId.IsValid
            || receipt.AttemptId <= 0
            || receipt.OwnerRevision <= 0L
            || receipt.AbsoluteDay < 0)
        {
            return Invalid("captivity-interaction-receipt-invalid");
        }
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            receipt.Success
                ? GameplayOutcomeStatus.Succeeded
                : GameplayOutcomeStatus.Failed,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 3,
            subjectCount: 3,
            metricCount: 11,
            tagCount: 2,
            anchorCount: 0,
            provenanceCount: 1,
            factCount: receipt.OutputAmount > 0 ? 6 : 5);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in CaptivityInteractionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId captive = Character(receipt.CaptiveId);
        GameplayEntityId warden = Character(receipt.WardenId);
        GameplayEntityId facility = Facility(receipt.FacilityId);
        bool written = AddParticipant(
                ref builder,
                captive,
                CaptivityInteractionOutcomeIds.CaptiveRole,
                receipt.CaptiveName,
                0.82f,
                NarrativeMemoryTier.Episodic)
            && AddParticipant(
                ref builder,
                warden,
                CaptivityInteractionOutcomeIds.WardenRole,
                receipt.WardenName,
                0.62f,
                NarrativeMemoryTier.Episodic)
            && AddParticipant(
                ref builder,
                facility,
                CaptivityInteractionOutcomeIds.FacilityRole,
                receipt.FacilityName,
                0.42f,
                NarrativeMemoryTier.Recent)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.WillDeltaMetric,
                receipt.WillDelta, CaptivityInteractionOutcomeIds.PointUnit, captive)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.FearDeltaMetric,
                receipt.FearDelta, CaptivityInteractionOutcomeIds.PointUnit, captive)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.TrustDeltaMetric,
                receipt.TrustDelta, CaptivityInteractionOutcomeIds.PointUnit, captive)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.GrudgeDeltaMetric,
                receipt.GrudgeDelta, CaptivityInteractionOutcomeIds.PointUnit, captive)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.CorruptionDeltaMetric,
                receipt.CorruptionDelta, CaptivityInteractionOutcomeIds.PointUnit, captive)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.BodyDamageMetric,
                receipt.BodyDamage, CaptivityInteractionOutcomeIds.HealthUnit, captive)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.BodyHealthBeforeMetric,
                receipt.BodyHealthBefore, CaptivityInteractionOutcomeIds.HealthUnit, captive)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.BodyHealthAfterMetric,
                receipt.BodyHealthAfter, CaptivityInteractionOutcomeIds.HealthUnit, captive)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.OutputAmountMetric,
                receipt.OutputAmount, CaptivityInteractionOutcomeIds.CountUnit, captive)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.AttemptMetric,
                receipt.AttemptId, CaptivityInteractionOutcomeIds.CountUnit, captive)
            && AddMetric(ref builder, CaptivityInteractionOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision, CaptivityInteractionOutcomeIds.RevisionUnit, captive)
            && builder.AddTag(CaptivityInteractionOutcomeIds.CaptivityTag)
            && builder.AddTag(CaptivityInteractionOutcomeIds.InteractionTag)
            && builder.AddProvenance(CaptivityInteractionOutcomeIds.Source)
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityInteractionOutcomeIds.InteractionIdFact,
                receipt.InteractionId))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityInteractionOutcomeIds.InteractionKindFact,
                receipt.InteractionKind.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityInteractionOutcomeIds.InteractionDisplayFact,
                receipt.InteractionDisplayName))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityInteractionOutcomeIds.MessageFact,
                receipt.Message))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityInteractionOutcomeIds.SuccessFact,
                receipt.Success ? "1" : "0"))
            && (receipt.OutputAmount <= 0
                || builder.AddFact(new GameplayOutcomeFact(
                    CaptivityInteractionOutcomeIds.OutputItemFact,
                    receipt.OutputItemId)))
            && builder.SetLocation(new GameplayLocationReference(
                receipt.FacilityId.Value,
                string.Empty,
                receipt.PositionX,
                receipt.PositionY));
        return written
            ? OutcomePrepareResult.Prepared()
            : Invalid("captivity-interaction-write-failed",
                OutcomePrepareCode.AdapterWriteFailed);
    }

    private static bool AddParticipant(
        ref OutcomeWriteBuilder builder,
        GameplayEntityId entity,
        GameplayRoleId role,
        KoreanNameSnapshot name,
        float salience,
        NarrativeMemoryTier tier) =>
        builder.AddParticipant(new GameplayOutcomeParticipant(
            entity,
            role,
            GameplayParticipationKind.Direct,
            true,
            name))
        && builder.AddSubject(new GameplayOutcomeSubjectLink(
            entity,
            salience,
            tier,
            false,
            false,
            0));

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId metric,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId subject) => builder.AddMetric(
        new GameplayOutcomeMetric(metric, value, unit, subject));

    private static GameplayEntityId Character(CharacterId id) => new(
        CaptivityInteractionOutcomeIds.CharacterKind,
        id.Value);

    private static GameplayEntityId Facility(BuildingInstanceId id) => new(
        CaptivityInteractionOutcomeIds.FacilityKind,
        id.Value);

    private static OutcomePrepareResult Invalid(
        string detail,
        OutcomePrepareCode code = OutcomePrepareCode.InvalidReceipt) =>
        new(code, detail);
}

public readonly struct PreparedCaptivityInteractionOutcome
{
    public PreparedCaptivityInteractionOutcome(
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

public interface ICaptivityInteractionOutcomeCommitter
{
    bool TryPrepare(
        in CaptivityInteractionOutcomeReceipt receipt,
        out PreparedCaptivityInteractionOutcome prepared,
        out string failureReason);
    OwnerOutcomeCommitResult Commit(
        in PreparedCaptivityInteractionOutcome prepared);
    void Cancel(in PreparedCaptivityInteractionOutcome prepared);
}

public sealed class CaptivityInteractionGameplayOutcomeBridge :
    ICaptivityInteractionOutcomeCommitter
{
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public CaptivityInteractionGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics) =>
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));

    public bool TryPrepare(
        in CaptivityInteractionOutcomeReceipt receipt,
        out PreparedCaptivityInteractionOutcome prepared,
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
        prepared = new PreparedCaptivityInteractionOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            replay.DurablyCommitted);
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedCaptivityInteractionOutcome prepared) => prepared.IsReplay
        ? transactions.Reconcile(prepared.ResultKey)
        : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public void Cancel(in PreparedCaptivityInteractionOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }
}

internal sealed class CaptivityInteractionMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":" + Fact(
            outcome,
            CaptivityInteractionOutcomeIds.InteractionKindFact));

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        Metric(outcome, CaptivityInteractionOutcomeIds.BodyDamageMetric,
            out double damage);
        Metric(outcome, CaptivityInteractionOutcomeIds.OutputAmountMetric,
            out double output);
        bool major = damage > 0d || output > 0d;
        float salience = outcome.Status == GameplayOutcomeStatus.Failed
            ? 0.45f
            : major ? 0.86f : 0.64f;
        return new OutcomeMemoryEvaluation(
            salience,
            major ? NarrativeMemoryTier.Episodic : NarrativeMemoryTier.Recent,
            Math.Max(evaluationDay + (major ? 90 : 30), outcome.AbsoluteDay + 30));
    }

    internal static bool Metric(
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

    internal static string Fact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId factId)
    {
        string value = string.Empty;
        int count = 0;
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (!fact.FactId.Equals(factId))
                continue;
            count++;
            value = fact.Value;
        }
        return count == 1 ? value : string.Empty;
    }
}

internal sealed class CaptivityInteractionPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class CaptivityInteractionConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class CaptivityInteractionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "captivity-interaction-v1";
    private readonly IKoreanJosaFormatter josa;

    public CaptivityInteractionPerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant captive = Find(
            outcome,
            CaptivityInteractionOutcomeIds.CaptiveRole);
        GameplayOutcomeParticipant warden = Find(
            outcome,
            CaptivityInteractionOutcomeIds.WardenRole);
        string display = CaptivityInteractionMemoryPolicy.Fact(
            outcome,
            CaptivityInteractionOutcomeIds.InteractionDisplayFact);
        string interactionId = CaptivityInteractionMemoryPolicy.Fact(
            outcome,
            CaptivityInteractionOutcomeIds.InteractionIdFact);
        string message = CaptivityInteractionMemoryPolicy.Fact(
            outcome,
            CaptivityInteractionOutcomeIds.MessageFact);
        KoreanJosaFormatResult wardenSubject = josa.Format(new KoreanJosaRequest(
            warden.DisplayName,
            KoreanJosaKind.Subject));
        KoreanJosaFormatResult captiveObject = josa.Format(new KoreanJosaRequest(
            captive.DisplayName,
            KoreanJosaKind.Object));
        KoreanNameSnapshot interactionName =
            CaptivityInteractionOutcomeNames.Snapshot(
                "interaction",
                interactionId,
                display);
        KoreanJosaFormatResult interactionObject = josa.Format(
            new KoreanJosaRequest(
                interactionName,
                KoreanJosaKind.Object));
        bool neutral = wardenSubject.RequiresNeutralFrame
            || captiveObject.RequiresNeutralFrame
            || interactionObject.RequiresNeutralFrame;
        string text = neutral
            ? $"관리자 {warden.DisplayName.DisplayText} · 포로 {captive.DisplayName.DisplayText}: ‘{display}’ 결과 — {message}"
            : $"{wardenSubject.Text} {captiveObject.Text} 상대로 ‘{display}’{interactionObject.SelectedParticle} 마쳤다. {message}";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }

    private static GameplayOutcomeParticipant Find(
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
}

public sealed class CaptivityInteractionOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new CaptivityInteractionMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new CaptivityInteractionPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new CaptivityInteractionConsolidator();

    public CaptivityInteractionOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new CaptivityInteractionPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        CaptivityInteractionOutcomeIds.Resolved;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(CaptivityInteractionOutcomeIds.CaptiveRole)
        || roleId.Equals(CaptivityInteractionOutcomeIds.WardenRole)
        || roleId.Equals(CaptivityInteractionOutcomeIds.FacilityRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        IsPoint(metricId) && unitId.Equals(CaptivityInteractionOutcomeIds.PointUnit)
        || IsHealth(metricId) && unitId.Equals(CaptivityInteractionOutcomeIds.HealthUnit)
        || (metricId.Equals(CaptivityInteractionOutcomeIds.OutputAmountMetric)
            || metricId.Equals(CaptivityInteractionOutcomeIds.AttemptMetric))
            && unitId.Equals(CaptivityInteractionOutcomeIds.CountUnit)
        || metricId.Equals(CaptivityInteractionOutcomeIds.OwnerRevisionMetric)
            && unitId.Equals(CaptivityInteractionOutcomeIds.RevisionUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        GameplayOutcomeParticipant captive = Find(
            outcome,
            CaptivityInteractionOutcomeIds.CaptiveRole);
        GameplayOutcomeParticipant warden = Find(
            outcome,
            CaptivityInteractionOutcomeIds.WardenRole);
        GameplayOutcomeParticipant facility = Find(
            outcome,
            CaptivityInteractionOutcomeIds.FacilityRole);
        string success = CaptivityInteractionMemoryPolicy.Fact(
            outcome,
            CaptivityInteractionOutcomeIds.SuccessFact);
        string interactionId = CaptivityInteractionMemoryPolicy.Fact(
            outcome,
            CaptivityInteractionOutcomeIds.InteractionIdFact);
        string interactionKind = CaptivityInteractionMemoryPolicy.Fact(
            outcome,
            CaptivityInteractionOutcomeIds.InteractionKindFact);
        string display = CaptivityInteractionMemoryPolicy.Fact(
            outcome,
            CaptivityInteractionOutcomeIds.InteractionDisplayFact);
        string message = CaptivityInteractionMemoryPolicy.Fact(
            outcome,
            CaptivityInteractionOutcomeIds.MessageFact);
        string outputItem = CaptivityInteractionMemoryPolicy.Fact(
            outcome,
            CaptivityInteractionOutcomeIds.OutputItemFact);
        double willDelta = 0d;
        double fearDelta = 0d;
        double trustDelta = 0d;
        double grudgeDelta = 0d;
        double corruptionDelta = 0d;
        double bodyDamage = 0d;
        double bodyBefore = 0d;
        double bodyAfter = 0d;
        double outputAmount = 0d;
        double attemptValue = 0d;
        double revisionValue = 0d;
        bool metrics = CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.WillDeltaMetric,
                out willDelta)
            && CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.FearDeltaMetric,
                out fearDelta)
            && CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.TrustDeltaMetric,
                out trustDelta)
            && CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.GrudgeDeltaMetric,
                out grudgeDelta)
            && CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.CorruptionDeltaMetric,
                out corruptionDelta)
            && CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.BodyDamageMetric,
                out bodyDamage)
            && CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.BodyHealthBeforeMetric,
                out bodyBefore)
            && CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.BodyHealthAfterMetric,
                out bodyAfter)
            && CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.OutputAmountMetric,
                out outputAmount)
            && CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.AttemptMetric,
                out attemptValue)
            && CaptivityInteractionMemoryPolicy.Metric(
                outcome,
                CaptivityInteractionOutcomeIds.OwnerRevisionMetric,
                out revisionValue);
        int attempt = IsWholeNumber(attemptValue, 1d, int.MaxValue)
            ? (int)attemptValue
            : 0;
        long revision = IsWholeNumber(revisionValue, 1d, long.MaxValue)
            ? (long)revisionValue
            : 0L;
        bool finiteShape = IsDelta(willDelta)
            && IsDelta(fearDelta)
            && IsDelta(trustDelta)
            && IsDelta(grudgeDelta)
            && IsDelta(corruptionDelta)
            && IsNonNegative(bodyDamage)
            && IsNonNegative(bodyBefore)
            && IsNonNegative(bodyAfter)
            && IsWholeNumber(outputAmount, 0d, int.MaxValue)
            && attempt > 0
            && revision > 0L;
        bool bodyShape = bodyDamage <= 0d
            ? Approximately(bodyBefore, 0d) && Approximately(bodyAfter, 0d)
            : bodyBefore > 0d
                && bodyAfter <= bodyBefore + 0.001d
                && Approximately(
                    bodyAfter,
                    Math.Max(0d, bodyBefore - bodyDamage));
        bool outputShape = outputAmount > 0d
            ? outputItem.Length > 0
            : outputItem.Length == 0;
        bool failedShape = success != "0"
            || (Approximately(willDelta, 0d)
                && Approximately(fearDelta, 0d)
                && Approximately(trustDelta, 0d)
                && Approximately(grudgeDelta, 0d)
                && Approximately(corruptionDelta, 0d)
                && Approximately(bodyDamage, 0d)
                && Approximately(outputAmount, 0d));
        GameplayResultKey expected = captive.EntityId.IsValid
            && attempt > 0
            && revision > 0L
                ? CaptivityInteractionOutcomeIds.ResultKey(
                    captive.EntityId.Value,
                    attempt,
                    revision)
                : default;
        bool shape = outcome.OutcomeTypeId.Equals(OutcomeTypeId)
            && outcome.ParticipantCount == 3
            && outcome.SubjectCount == 3
            && outcome.MetricCount == 11
            && outcome.TagCount == 2
            && outcome.ProvenanceCount == 1
            && outcome.AnchorCount == 0
            && outcome.FactCount == (outputAmount > 0d ? 6 : 5)
            && captive.EntityId.Kind.Equals(
                CaptivityInteractionOutcomeIds.CharacterKind)
            && warden.EntityId.Kind.Equals(
                CaptivityInteractionOutcomeIds.CharacterKind)
            && facility.EntityId.Kind.Equals(
                CaptivityInteractionOutcomeIds.FacilityKind)
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(captive.DisplayName)
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(warden.DisplayName)
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(facility.DisplayName)
            && (success == "1"
                ? outcome.Status == GameplayOutcomeStatus.Succeeded
                : success == "0" && outcome.Status == GameplayOutcomeStatus.Failed)
            && interactionId.Length > 0
            && Enum.TryParse(interactionKind, out CaptiveInteractionKind _)
            && display.Length > 0
            && message.Length > 0
            && metrics
            && finiteShape
            && bodyShape
            && outputShape
            && failedShape
            && outcome.ResultKey.Equals(expected)
            && outcome.Location.HasLocation
            && string.Equals(
                outcome.Location.LocationId,
                facility.EntityId.Value,
                StringComparison.Ordinal);
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "captivity-interaction-outcome-invalid");
    }

    private static bool IsPoint(GameplayMetricId id) =>
        id.Equals(CaptivityInteractionOutcomeIds.WillDeltaMetric)
        || id.Equals(CaptivityInteractionOutcomeIds.FearDeltaMetric)
        || id.Equals(CaptivityInteractionOutcomeIds.TrustDeltaMetric)
        || id.Equals(CaptivityInteractionOutcomeIds.GrudgeDeltaMetric)
        || id.Equals(CaptivityInteractionOutcomeIds.CorruptionDeltaMetric);

    private static bool IsHealth(GameplayMetricId id) =>
        id.Equals(CaptivityInteractionOutcomeIds.BodyDamageMetric)
        || id.Equals(CaptivityInteractionOutcomeIds.BodyHealthBeforeMetric)
        || id.Equals(CaptivityInteractionOutcomeIds.BodyHealthAfterMetric);

    private static bool IsDelta(double value) =>
        IsFinite(value) && value >= -100d && value <= 100d;

    private static bool IsNonNegative(double value) =>
        IsFinite(value) && value >= 0d;

    private static bool IsWholeNumber(
        double value,
        double minimum,
        double maximum) => IsFinite(value)
        && value >= minimum
        && value <= maximum
        && Math.Truncate(value) == value;

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool Approximately(double left, double right) =>
        Math.Abs(left - right) <= 0.001d;

    private static GameplayOutcomeParticipant Find(
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
}
