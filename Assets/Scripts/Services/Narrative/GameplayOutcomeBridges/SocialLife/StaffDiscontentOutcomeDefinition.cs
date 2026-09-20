using System;
using DungeonStory.Narrative.Korean;

public static class StaffDiscontentOutcomeIds
{
    public const string ProducerId = "social.staff-rebellion-response";

    public static readonly GameplayOutcomeTypeId ResponseResolved =
        new("social.staff-rebellion-response-resolved");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId ResponseKind =
        new("staff-response-kind");
    public static readonly GameplayEntityKindId StageKind =
        new("staff-discontent-stage");
    public static readonly GameplayRoleId StaffRole = new("staff");
    public static readonly GameplayRoleId ResponderRole = new("responder");
    public static readonly GameplayMetricId ResponseTypeMetric =
        new("staff-response.type");
    public static readonly GameplayMetricId BeforeStageMetric =
        new("staff-response.before-stage");
    public static readonly GameplayMetricId AfterStageMetric =
        new("staff-response.after-stage");
    public static readonly GameplayMetricId BeforeMoodMetric =
        new("staff-response.before-mood");
    public static readonly GameplayMetricId AfterMoodMetric =
        new("staff-response.after-mood");
    public static readonly GameplayMetricId BeforeLowMoodDaysMetric =
        new("staff-response.before-low-mood-days");
    public static readonly GameplayMetricId AfterLowMoodDaysMetric =
        new("staff-response.after-low-mood-days");
    public static readonly GameplayMetricId PermanentLossMetric =
        new("staff-response.permanent-loss");
    public static readonly GameplayMetricId DepartedMetric =
        new("staff-response.departed");
    public static readonly GameplayMetricId LocalRebellionMetric =
        new("staff-response.local-rebellion");
    public static readonly GameplayMetricId OwnerThreatMetric =
        new("staff-response.owner-threat");
    public static readonly GameplayMetricId IsolatedMetric =
        new("staff-response.isolated");
    public static readonly GameplayMetricId SuppressedMetric =
        new("staff-response.suppressed");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId EnumUnit = new("enum");
    public static readonly GameplayMetricUnitId BooleanUnit = new("boolean");
    public static readonly GameplayMetricUnitId DayUnit = new("day");
    public static readonly GameplayOutcomeTagId RelationshipTag =
        new("relationship");
    public static readonly GameplayOutcomeTagId StaffTag = new("staff");
    public static readonly GameplayOutcomeTagId RebellionResponseTag =
        new("rebellion-response");
    public static readonly GameplayOutcomeProvenanceReference Source =
        new("source-receipt", "staff-rebellion-response-result");

    public static GameplayResultKey ResultKey(long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId("staff-response:" + ownerRevision),
        ownerRevision,
        0);

    public static string ResponseSlug(StaffRebellionResponseType value) =>
        value switch
        {
            StaffRebellionResponseType.SuppressCommand => "suppress-command",
            StaffRebellionResponseType.Isolate => "isolate",
            StaffRebellionResponseType.Calm => "calm",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Only committed staff responses have outcome identities.")
        };

    public static string StageSlug(StaffDiscontentStage value) => value switch
    {
        StaffDiscontentStage.Stable => "stable",
        StaffDiscontentStage.LowSatisfaction => "low-satisfaction",
        StaffDiscontentStage.EfficiencyDrop => "efficiency-drop",
        StaffDiscontentStage.WorkDisruption => "work-disruption",
        StaffDiscontentStage.Departure => "departure",
        StaffDiscontentStage.LocalRebellion => "local-rebellion",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}

public readonly struct StaffDiscontentOutcomeReceipt
{
    public StaffDiscontentOutcomeReceipt(
        long ownerRevision,
        int absoluteDay,
        StaffRebellionResponseType responseType,
        StaffDiscontentSnapshot before,
        StaffDiscontentSnapshot after,
        CharacterId responder = default,
        KoreanNameSnapshot responderName = default)
    {
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (before == null || after == null)
            throw new ArgumentNullException(
                before == null ? nameof(before) : nameof(after));
        if (!string.Equals(before.staffId, after.staffId, StringComparison.Ordinal))
            throw new ArgumentException("Staff response snapshots must have the same staff ID.");
        CharacterId staff = new(before.staffId);
        if (!staff.IsValid)
            throw new ArgumentException("A canonical staff ID is required.");
        _ = StaffDiscontentOutcomeIds.ResponseSlug(responseType);

        StaffName = CaptureName(staff.Value, before.displayName);
        if (responder.IsValid
            != GameplayOutcomeLedger.IsValidDisplayNameSnapshot(responderName))
        {
            throw new ArgumentException(
                "Responder identity and historical name must be supplied together.");
        }
        if (responder.IsValid && responder.Equals(staff))
            throw new ArgumentException("Staff and responder must be distinct.");

        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
        ResponseType = responseType;
        Staff = staff;
        Before = before;
        After = after;
        Responder = responder;
        ResponderName = responderName;
    }

    public long OwnerRevision { get; }
    public int AbsoluteDay { get; }
    public StaffRebellionResponseType ResponseType { get; }
    public CharacterId Staff { get; }
    public KoreanNameSnapshot StaffName { get; }
    public StaffDiscontentSnapshot Before { get; }
    public StaffDiscontentSnapshot After { get; }
    public CharacterId Responder { get; }
    public KoreanNameSnapshot ResponderName { get; }
    public bool HasResponder => Responder.IsValid;
    public GameplayResultKey ResultKey =>
        StaffDiscontentOutcomeIds.ResultKey(OwnerRevision);

    public static KoreanNameSnapshot CaptureName(
        string stableIdentity,
        string displayText)
    {
        string identity = GameplayOutcomeStableIdSyntax.Require(
            stableIdentity,
            nameof(stableIdentity));
        string display = displayText?.Trim() ?? string.Empty;
        if (display.Length == 0)
            throw new ArgumentException(
                "An immutable staff display name is required.",
                nameof(displayText));
        string revision = "staff-response-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(identity + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }
}

public sealed class StaffDiscontentOutcomeAdapter :
    GameplayOutcomeAdapter<StaffDiscontentOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        StaffDiscontentOutcomeIds.ResponseResolved;

    public static OutcomeWriteRequirements CreateRequirements(
        GameplayResultKey resultKey,
        long ownerRevision,
        int absoluteDay,
        bool hasResponder,
        long worldEpoch) => new(
        resultKey,
        StaffDiscontentOutcomeIds.ResponseResolved,
        absoluteDay,
        GameplayOutcomeStatus.Succeeded,
        worldEpoch,
        ownerRevision,
        participantCount: hasResponder ? 2 : 1,
        metricCount: 13,
        subjectCount: hasResponder ? 2 : 1,
        tagCount: 3,
        anchorCount: 0,
        provenanceCount: 1);

    public override OutcomePrepareResult TryGetRequirements(
        in StaffDiscontentOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = CreateRequirements(
            receipt.ResultKey,
            receipt.OwnerRevision,
            receipt.AbsoluteDay,
            receipt.HasResponder,
            currentWorldEpoch);
        bool valid = receipt.OwnerRevision > 0L
            && receipt.AbsoluteDay >= 0
            && receipt.Staff.IsValid
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(receipt.StaffName)
            && receipt.Before != null
            && receipt.After != null
            && string.Equals(
                receipt.Before.staffId,
                receipt.After.staffId,
                StringComparison.Ordinal)
            && (!receipt.HasResponder
                || GameplayOutcomeLedger.IsValidDisplayNameSnapshot(
                    receipt.ResponderName));
        try
        {
            _ = StaffDiscontentOutcomeIds.ResponseSlug(receipt.ResponseType);
        }
        catch (ArgumentOutOfRangeException)
        {
            valid = false;
        }
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "staff-response-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in StaffDiscontentOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId staff = Character(receipt.Staff);
        GameplayEntityId response = new(
            StaffDiscontentOutcomeIds.ResponseKind,
            StaffDiscontentOutcomeIds.ResponseSlug(receipt.ResponseType));
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                staff,
                StaffDiscontentOutcomeIds.StaffRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.StaffName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                staff,
                Salience(receipt.ResponseType),
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && AddMetric(
                ref builder,
                StaffDiscontentOutcomeIds.ResponseTypeMetric,
                (int)receipt.ResponseType,
                StaffDiscontentOutcomeIds.EnumUnit,
                response)
            && AddMetric(
                ref builder,
                StaffDiscontentOutcomeIds.BeforeStageMetric,
                (int)receipt.Before.stage,
                StaffDiscontentOutcomeIds.EnumUnit,
                Stage(receipt.Before.stage))
            && AddMetric(
                ref builder,
                StaffDiscontentOutcomeIds.AfterStageMetric,
                (int)receipt.After.stage,
                StaffDiscontentOutcomeIds.EnumUnit,
                Stage(receipt.After.stage))
            && AddMetric(
                ref builder,
                StaffDiscontentOutcomeIds.BeforeMoodMetric,
                receipt.Before.mood,
                StaffDiscontentOutcomeIds.PointUnit,
                staff)
            && AddMetric(
                ref builder,
                StaffDiscontentOutcomeIds.AfterMoodMetric,
                receipt.After.mood,
                StaffDiscontentOutcomeIds.PointUnit,
                staff)
            && AddMetric(
                ref builder,
                StaffDiscontentOutcomeIds.BeforeLowMoodDaysMetric,
                receipt.Before.lowMoodDays,
                StaffDiscontentOutcomeIds.DayUnit,
                staff)
            && AddMetric(
                ref builder,
                StaffDiscontentOutcomeIds.AfterLowMoodDaysMetric,
                receipt.After.lowMoodDays,
                StaffDiscontentOutcomeIds.DayUnit,
                staff)
            && AddBoolean(
                ref builder,
                StaffDiscontentOutcomeIds.PermanentLossMetric,
                receipt.After.permanentLoss,
                staff)
            && AddBoolean(
                ref builder,
                StaffDiscontentOutcomeIds.DepartedMetric,
                receipt.After.departed,
                staff)
            && AddBoolean(
                ref builder,
                StaffDiscontentOutcomeIds.LocalRebellionMetric,
                receipt.After.localRebellion,
                staff)
            && AddBoolean(
                ref builder,
                StaffDiscontentOutcomeIds.OwnerThreatMetric,
                receipt.After.ownerThreat,
                staff)
            && AddBoolean(
                ref builder,
                StaffDiscontentOutcomeIds.IsolatedMetric,
                receipt.After.isolated,
                staff)
            && AddBoolean(
                ref builder,
                StaffDiscontentOutcomeIds.SuppressedMetric,
                receipt.After.suppressed,
                staff)
            && builder.AddTag(StaffDiscontentOutcomeIds.RelationshipTag)
            && builder.AddTag(StaffDiscontentOutcomeIds.StaffTag)
            && builder.AddTag(StaffDiscontentOutcomeIds.RebellionResponseTag)
            && builder.AddProvenance(StaffDiscontentOutcomeIds.Source);

        if (written && receipt.HasResponder)
        {
            GameplayEntityId responder = Character(receipt.Responder);
            written = builder.AddParticipant(new GameplayOutcomeParticipant(
                    responder,
                    StaffDiscontentOutcomeIds.ResponderRole,
                    GameplayParticipationKind.Direct,
                    true,
                    receipt.ResponderName))
                && builder.AddSubject(new GameplayOutcomeSubjectLink(
                    responder,
                    0.55f,
                    NarrativeMemoryTier.Recent,
                    false,
                    false,
                    0));
        }

        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "staff-response-write-failed");
    }

    private static bool AddBoolean(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        bool value,
        GameplayEntityId entity) => AddMetric(
        ref builder,
        id,
        value ? 1d : 0d,
        StaffDiscontentOutcomeIds.BooleanUnit,
        entity);

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId entity) => builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, entity));

    private static GameplayEntityId Character(CharacterId id) => new(
        StaffDiscontentOutcomeIds.CharacterKind,
        id.Value);

    private static GameplayEntityId Stage(StaffDiscontentStage stage) => new(
        StaffDiscontentOutcomeIds.StageKind,
        StaffDiscontentOutcomeIds.StageSlug(stage));

    private static float Salience(StaffRebellionResponseType response) =>
        response switch
        {
            StaffRebellionResponseType.SuppressCommand => 0.95f,
            StaffRebellionResponseType.Isolate => 0.9f,
            _ => 0.72f
        };
}

public readonly struct ReservedStaffDiscontentOutcome
{
    internal ReservedStaffDiscontentOutcome(
        PreparedOutcomeReservation reservation,
        GameplayResultKey resultKey)
    {
        Reservation = reservation;
        ResultKey = resultKey;
    }

    internal PreparedOutcomeReservation Reservation { get; }
    public GameplayResultKey ResultKey { get; }
    public bool IsValid => Reservation.IsValid && ResultKey.IsValid;
}

public interface IStaffDiscontentOutcomeCommitter
{
    bool TryReserve(
        long ownerRevision,
        int absoluteDay,
        bool hasResponder,
        out ReservedStaffDiscontentOutcome reserved,
        out string failureReason);
    bool TryWrite(
        in StaffDiscontentOutcomeReceipt receipt,
        in ReservedStaffDiscontentOutcome reserved,
        out PreparedOwnerOutcome prepared,
        out string failureReason);
    OwnerOutcomeCommitResult Commit(
        in PreparedOwnerOutcome prepared,
        long expectedOwnerRevision);
    OwnerOutcomeCommitResult Reconcile(GameplayResultKey resultKey);
    void Cancel(in ReservedStaffDiscontentOutcome reserved);
    void Cancel(in PreparedOwnerOutcome prepared);
}

public sealed class StaffDiscontentGameplayOutcomeBridge :
    IStaffDiscontentOutcomeCommitter
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly GameplayOutcomeLedger ledger;
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public StaffDiscontentGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder,
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
    }

    public bool TryReserve(
        long ownerRevision,
        int absoluteDay,
        bool hasResponder,
        out ReservedStaffDiscontentOutcome reserved,
        out string failureReason)
    {
        reserved = default;
        GameplayResultKey key;
        try
        {
            key = StaffDiscontentOutcomeIds.ResultKey(ownerRevision);
        }
        catch (Exception exception) when (IsCaptureException(exception))
        {
            failureReason = "staff-response-result-key-invalid:"
                + exception.Message;
            return false;
        }
        OutcomePrepareResult result = recorder.TryReserve(
            StaffDiscontentOutcomeAdapter.CreateRequirements(
                key,
                ownerRevision,
                absoluteDay,
                hasResponder,
                ledger.CurrentWorldEpoch),
            out PreparedOutcomeReservation reservation);
        if (!result.Success)
        {
            failureReason = "staff-response-reserve-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }
        reserved = new ReservedStaffDiscontentOutcome(reservation, key);
        failureReason = string.Empty;
        return true;
    }

    public bool TryWrite(
        in StaffDiscontentOutcomeReceipt receipt,
        in ReservedStaffDiscontentOutcome reserved,
        out PreparedOwnerOutcome prepared,
        out string failureReason)
    {
        prepared = default;
        if (!reserved.IsValid || reserved.ResultKey != receipt.ResultKey)
        {
            failureReason = "staff-response-reservation-mismatch";
            return false;
        }
        OutcomePrepareResult result = recorder.TryWriteReserved(
            receipt,
            reserved.Reservation,
            out PreparedOutcomeToken token);
        if (!result.Success)
        {
            recorder.CancelReservation(reserved.Reservation);
            failureReason = "staff-response-write-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }
        prepared = new PreparedOwnerOutcome(token);
        failureReason = string.Empty;
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedOwnerOutcome prepared,
        long expectedOwnerRevision) =>
        transactions.Commit(prepared, expectedOwnerRevision);

    public OwnerOutcomeCommitResult Reconcile(GameplayResultKey resultKey) =>
        transactions.Reconcile(resultKey);

    public void Cancel(in ReservedStaffDiscontentOutcome reserved)
    {
        if (reserved.Reservation.IsValid)
            recorder.CancelReservation(reserved.Reservation);
    }

    public void Cancel(in PreparedOwnerOutcome prepared) =>
        transactions.Cancel(prepared);

    private static bool IsCaptureException(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or OverflowException;
}

internal sealed class StaffDiscontentOutcomeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":" + (int)Metric(
            outcome,
            StaffDiscontentOutcomeIds.ResponseTypeMetric));

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
        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float salience = Math.Clamp(
            baseSalience
            + (priorMatchingCount == 0 ? 0.06f : 0f)
            - Math.Min(0.25f, priorMatchingCount * 0.035f)
            - Math.Min(0.25f, age * 0.008f),
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

internal sealed class StaffDiscontentOutcomePerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class StaffDiscontentOutcomeConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class StaffDiscontentPerspectiveProjector :
    SocialLifePerspectiveProjectorBase
{
    public StaffDiscontentPerspectiveProjector(IKoreanJosaFormatter josa)
        : base(josa)
    {
    }

    public override NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        TryParticipant(
            outcome,
            StaffDiscontentOutcomeIds.StaffRole,
            out GameplayOutcomeParticipant staff);
        bool hasResponder = TryParticipant(
            outcome,
            StaffDiscontentOutcomeIds.ResponderRole,
            out GameplayOutcomeParticipant responder);
        StaffRebellionResponseType response =
            (StaffRebellionResponseType)(int)Metric(
                outcome,
                StaffDiscontentOutcomeIds.ResponseTypeMetric);
        StaffDiscontentStage before = (StaffDiscontentStage)(int)Metric(
            outcome,
            StaffDiscontentOutcomeIds.BeforeStageMetric);
        StaffDiscontentStage after = (StaffDiscontentStage)(int)Metric(
            outcome,
            StaffDiscontentOutcomeIds.AfterStageMetric);
        string verb = ResponseVerb(response);
        string detail = $" 상태는 {StageLabel(before)}에서 {StageLabel(after)}로 바뀌었다.";
        bool neutral;
        string text;
        if (hasResponder
            && perspective.Kind == NarrativePerspectiveKind.Character
            && perspective.ViewerId == responder.EntityId)
        {
            neutral = !TryWithJosa(
                staff.DisplayName,
                KoreanJosaKind.Object,
                out string staffObject);
            text = neutral
                ? $"대상: {staff.DisplayName.DisplayText} · 대응: {verb}." + detail
                : $"{staffObject} {verb}." + detail;
        }
        else if (perspective.Kind == NarrativePerspectiveKind.Character
                 && perspective.ViewerId == staff.EntityId)
        {
            string responderSubject = string.Empty;
            neutral = hasResponder && !TryWithJosa(
                responder.DisplayName,
                KoreanJosaKind.Subject,
                out responderSubject);
            text = hasResponder
                ? neutral
                    ? $"대응자: {responder.DisplayName.DisplayText} · 결과: {verb}." + detail
                    : $"{responderSubject} 나를 {verb}." + detail
                : $"관리 대응으로 {verb}." + detail;
        }
        else if (hasResponder)
        {
            bool left = TryWithJosa(
                responder.DisplayName,
                KoreanJosaKind.Subject,
                out string responderSubject);
            bool right = TryWithJosa(
                staff.DisplayName,
                KoreanJosaKind.Object,
                out string staffObject);
            neutral = !left || !right;
            text = neutral
                ? $"대응자: {responder.DisplayName.DisplayText} · 대상: {staff.DisplayName.DisplayText} · 결과: {verb}." + detail
                : $"{responderSubject} {staffObject} {verb}." + detail;
        }
        else
        {
            neutral = !TryWithJosa(
                staff.DisplayName,
                KoreanJosaKind.Subject,
                out string staffSubject);
            text = neutral
                ? $"대상: {staff.DisplayName.DisplayText} · 결과: {verb}." + detail
                : $"{staffSubject} 관리 대응으로 {verb}." + detail;
        }
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }

    private static string ResponseVerb(StaffRebellionResponseType response) =>
        response switch
        {
            StaffRebellionResponseType.Calm => "진정시켰다",
            StaffRebellionResponseType.Isolate => "격리했다",
            StaffRebellionResponseType.SuppressCommand => "제압했다",
            _ => "대응했다"
        };

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

public sealed class StaffDiscontentOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new StaffDiscontentOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new StaffDiscontentOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new StaffDiscontentOutcomeConsolidator();

    public StaffDiscontentOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new StaffDiscontentPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        StaffDiscontentOutcomeIds.ResponseResolved;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(StaffDiscontentOutcomeIds.StaffRole)
        || roleId.Equals(StaffDiscontentOutcomeIds.ResponderRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        (metricId.Equals(StaffDiscontentOutcomeIds.ResponseTypeMetric)
            || metricId.Equals(StaffDiscontentOutcomeIds.BeforeStageMetric)
            || metricId.Equals(StaffDiscontentOutcomeIds.AfterStageMetric))
        && unitId.Equals(StaffDiscontentOutcomeIds.EnumUnit)
        || (metricId.Equals(StaffDiscontentOutcomeIds.BeforeMoodMetric)
            || metricId.Equals(StaffDiscontentOutcomeIds.AfterMoodMetric))
        && unitId.Equals(StaffDiscontentOutcomeIds.PointUnit)
        || (metricId.Equals(StaffDiscontentOutcomeIds.BeforeLowMoodDaysMetric)
            || metricId.Equals(StaffDiscontentOutcomeIds.AfterLowMoodDaysMetric))
        && unitId.Equals(StaffDiscontentOutcomeIds.DayUnit)
        || (metricId.Equals(StaffDiscontentOutcomeIds.PermanentLossMetric)
            || metricId.Equals(StaffDiscontentOutcomeIds.DepartedMetric)
            || metricId.Equals(StaffDiscontentOutcomeIds.LocalRebellionMetric)
            || metricId.Equals(StaffDiscontentOutcomeIds.OwnerThreatMetric)
            || metricId.Equals(StaffDiscontentOutcomeIds.IsolatedMetric)
            || metricId.Equals(StaffDiscontentOutcomeIds.SuppressedMetric))
        && unitId.Equals(StaffDiscontentOutcomeIds.BooleanUnit);

    public OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount is 1 or 2
        && outcome.MetricCount == 13
        && outcome.SubjectCount == outcome.ParticipantCount
        && outcome.TagCount == 3
        && outcome.AnchorCount == 0
        && outcome.ProvenanceCount == 1
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "staff-response-shape-invalid");
}
