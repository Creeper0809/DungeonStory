using System;
using DungeonStory.Narrative.Korean;

public static class EmergencyWorkSuspensionOutcomeIds
{
    public const string ProducerId = "work.emergency-suspension";

    public static readonly GameplayOutcomeTypeId SuspensionRecorded =
        new("work.emergency-suspension-recorded");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayRoleId WorkerRole = new("worker");
    public static readonly GameplayRoleId TargetFacilityRole = new("target-facility");
    public static readonly GameplayMetricId AlertEpochMetric = new("alert.epoch");
    public static readonly GameplayMetricId InlineCompletedWorkMetric =
        new("work.inline-completed");
    public static readonly GameplayMetricId InlineRequiredWorkMetric =
        new("work.inline-required");
    public static readonly GameplayMetricId ExternalProgressMetric =
        new("work.progress-externally-persisted");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId WorkUnit = new("work-unit");
    public static readonly GameplayOutcomeTagId EmergencySuspensionTag =
        new("emergency-work-suspension");
    public static readonly GameplayOutcomeFactId WorkDisplayNameFact =
        new("work.display-name");

    public static GameplayResultKey ResultKey(
        string characterId,
        long alertEpochId,
        long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId(
            $"emergency-work-suspension:{characterId}:{alertEpochId}"),
        ownerRevision,
        0);
}

public readonly struct EmergencyWorkSuspensionOutcomeReceipt
{
    public EmergencyWorkSuspensionOutcomeReceipt(
        string characterId,
        KoreanNameSnapshot workerDisplayName,
        WorkTypeId workTypeId,
        string workDisplayName,
        string targetBuildingId,
        KoreanNameSnapshot targetDisplayName,
        long alertEpochId,
        long suspendedAtAbsoluteHour,
        bool progressExternallyPersisted,
        float inlineCompletedWork,
        float inlineRequiredWork,
        long ownerRevision,
        int absoluteDay)
    {
        CharacterId = GameplayOutcomeStableIdSyntax.Require(
            characterId,
            nameof(characterId));
        if (string.IsNullOrWhiteSpace(workerDisplayName.DisplayText)
            || string.IsNullOrWhiteSpace(workerDisplayName.DisplaySnapshotRevision))
        {
            throw new ArgumentException(
                "A frozen worker display snapshot is required.",
                nameof(workerDisplayName));
        }
        if (!workTypeId.IsValid)
            throw new ArgumentException("A work type is required.", nameof(workTypeId));
        if (string.IsNullOrWhiteSpace(workDisplayName))
            throw new ArgumentException("A work display name is required.", nameof(workDisplayName));
        TargetBuildingId = GameplayOutcomeStableIdSyntax.Require(
            targetBuildingId,
            nameof(targetBuildingId));
        if (string.IsNullOrWhiteSpace(targetDisplayName.DisplayText)
            || string.IsNullOrWhiteSpace(targetDisplayName.DisplaySnapshotRevision))
        {
            throw new ArgumentException(
                "A frozen target display snapshot is required.",
                nameof(targetDisplayName));
        }
        if (alertEpochId <= 0L)
            throw new ArgumentOutOfRangeException(nameof(alertEpochId));
        if (suspendedAtAbsoluteHour < 0L)
            throw new ArgumentOutOfRangeException(nameof(suspendedAtAbsoluteHour));
        bool hasInlineProgress = inlineRequiredWork > 0f
            && inlineCompletedWork >= 0f
            && inlineCompletedWork < inlineRequiredWork
            && float.IsFinite(inlineCompletedWork)
            && float.IsFinite(inlineRequiredWork);
        if (!progressExternallyPersisted && !hasInlineProgress)
        {
            throw new ArgumentException(
                "Suspension progress must be externally persisted or exact inline progress.");
        }
        if (progressExternallyPersisted
            && (inlineCompletedWork != 0f || inlineRequiredWork != 0f))
        {
            throw new ArgumentException(
                "Externally persisted progress cannot carry inline progress.");
        }
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (absoluteDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        WorkerDisplayName = workerDisplayName;
        WorkTypeId = workTypeId;
        WorkDisplayName = workDisplayName.Trim();
        TargetDisplayName = targetDisplayName;
        AlertEpochId = alertEpochId;
        SuspendedAtAbsoluteHour = suspendedAtAbsoluteHour;
        ProgressExternallyPersisted = progressExternallyPersisted;
        InlineCompletedWork = inlineCompletedWork;
        InlineRequiredWork = inlineRequiredWork;
        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
    }

    public string CharacterId { get; }
    public KoreanNameSnapshot WorkerDisplayName { get; }
    public WorkTypeId WorkTypeId { get; }
    public string WorkDisplayName { get; }
    public string TargetBuildingId { get; }
    public KoreanNameSnapshot TargetDisplayName { get; }
    public long AlertEpochId { get; }
    public long SuspendedAtAbsoluteHour { get; }
    public bool ProgressExternallyPersisted { get; }
    public float InlineCompletedWork { get; }
    public float InlineRequiredWork { get; }
    public long OwnerRevision { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey =>
        EmergencyWorkSuspensionOutcomeIds.ResultKey(
            CharacterId,
            AlertEpochId,
            OwnerRevision);
}

public sealed class EmergencyWorkSuspensionOutcomeAdapter :
    GameplayOutcomeAdapter<EmergencyWorkSuspensionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EmergencyWorkSuspensionOutcomeIds.SuspensionRecorded;

    public override OutcomePrepareResult TryGetRequirements(
        in EmergencyWorkSuspensionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!GameplayOutcomeStableIdSyntax.IsValid(receipt.CharacterId)
            || !receipt.WorkTypeId.IsValid
            || string.IsNullOrWhiteSpace(receipt.WorkDisplayName)
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.TargetBuildingId)
            || receipt.AlertEpochId <= 0L
            || receipt.SuspendedAtAbsoluteHour < 0L
            || receipt.OwnerRevision <= 0L
            || receipt.AbsoluteDay <= 0)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "emergency-work-suspension-receipt-invalid");
        }

        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 2,
            metricCount: 4,
            subjectCount: 2,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: 1);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in EmergencyWorkSuspensionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId worker = new(
            EmergencyWorkSuspensionOutcomeIds.CharacterKind,
            receipt.CharacterId);
        GameplayEntityId facility = new(
            EmergencyWorkSuspensionOutcomeIds.FacilityKind,
            receipt.TargetBuildingId);
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                worker,
                EmergencyWorkSuspensionOutcomeIds.WorkerRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.WorkerDisplayName))
            || !builder.AddParticipant(new GameplayOutcomeParticipant(
                facility,
                EmergencyWorkSuspensionOutcomeIds.TargetFacilityRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.TargetDisplayName))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                worker,
                0.82f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility,
                0.58f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                EmergencyWorkSuspensionOutcomeIds.AlertEpochMetric,
                receipt.AlertEpochId,
                EmergencyWorkSuspensionOutcomeIds.CountUnit,
                worker))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                EmergencyWorkSuspensionOutcomeIds.InlineCompletedWorkMetric,
                receipt.InlineCompletedWork,
                EmergencyWorkSuspensionOutcomeIds.WorkUnit,
                facility))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                EmergencyWorkSuspensionOutcomeIds.InlineRequiredWorkMetric,
                receipt.InlineRequiredWork,
                EmergencyWorkSuspensionOutcomeIds.WorkUnit,
                facility))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                EmergencyWorkSuspensionOutcomeIds.ExternalProgressMetric,
                receipt.ProgressExternallyPersisted ? 1d : 0d,
                EmergencyWorkSuspensionOutcomeIds.CountUnit,
                facility))
            || !builder.AddTag(
                EmergencyWorkSuspensionOutcomeIds.EmergencySuspensionTag)
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "work-type",
                receipt.WorkTypeId.Value))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "alert-epoch",
                $"alert-epoch:{receipt.AlertEpochId}"))
            || !builder.AddFact(new GameplayOutcomeFact(
                EmergencyWorkSuspensionOutcomeIds.WorkDisplayNameFact,
                receipt.WorkDisplayName)))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "emergency-work-suspension-write-failed");
        }

        return OutcomePrepareResult.Prepared();
    }
}

public readonly struct PreparedEmergencyWorkSuspensionOutcome
{
    internal PreparedEmergencyWorkSuspensionOutcome(
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
    public bool IsValid => IsReplay ? ResultKey.IsValid : Prepared.IsValid;
}

public interface IEmergencyWorkSuspensionOutcomeCommitter
{
    bool TryPrepare(
        in SettlementSuspendedWorkSnapshot snapshot,
        int absoluteDay,
        out PreparedEmergencyWorkSuspensionOutcome prepared,
        out string failureReason);
    OwnerOutcomeCommitResult Commit(
        in PreparedEmergencyWorkSuspensionOutcome prepared);
    void Cancel(in PreparedEmergencyWorkSuspensionOutcome prepared);
}

public sealed class EmergencyWorkSuspensionGameplayOutcomeBridge :
    IEmergencyWorkSuspensionOutcomeCommitter
{
    private readonly IGameplayOutcomeDisplayNameQuery displayNames;
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public EmergencyWorkSuspensionGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameplayOutcomeDisplayNameQuery displayNames)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
        this.displayNames = displayNames
            ?? throw new ArgumentNullException(nameof(displayNames));
    }

    public bool TryPrepare(
        in SettlementSuspendedWorkSnapshot snapshot,
        int absoluteDay,
        out PreparedEmergencyWorkSuspensionOutcome prepared,
        out string failureReason)
    {
        prepared = default;
        GameplayEntityId worker = new(
            EmergencyWorkSuspensionOutcomeIds.CharacterKind,
            snapshot.CharacterId);
        GameplayEntityId facility = new(
            EmergencyWorkSuspensionOutcomeIds.FacilityKind,
            snapshot.TargetBuildingId);
        if (!displayNames.TryGetCurrentName(worker, out KoreanNameSnapshot workerName))
        {
            failureReason = "emergency-work-suspension-worker-name-missing";
            return false;
        }
        if (!displayNames.TryGetCurrentName(
                facility,
                out KoreanNameSnapshot facilityName))
        {
            failureReason = "emergency-work-suspension-target-name-missing";
            return false;
        }
        if (!WorkTypeCatalog.TryGet(
                snapshot.WorkTypeId,
                out WorkTypeDefinition workType))
        {
            failureReason = "emergency-work-suspension-work-type-missing";
            return false;
        }

        EmergencyWorkSuspensionOutcomeReceipt receipt;
        try
        {
            receipt = new EmergencyWorkSuspensionOutcomeReceipt(
                snapshot.CharacterId,
                workerName,
                snapshot.WorkTypeId,
                workType.DisplayName,
                snapshot.TargetBuildingId,
                facilityName,
                snapshot.AlertEpochId,
                snapshot.SuspendedAtAbsoluteHour,
                snapshot.ProgressExternallyPersisted,
                snapshot.InlineCompletedWork,
                snapshot.InlineRequiredWork,
                snapshot.OutcomeOwnerRevision,
                absoluteDay);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "emergency-work-suspension-receipt-invalid:"
                + exception.Message;
            return false;
        }

        if (!transactions.TryPrepare(
                receipt,
                out PreparedOwnerOutcome token,
                out OwnerOutcomeCommitResult replay,
                out _,
                out failureReason))
        {
            return false;
        }
        prepared = new PreparedEmergencyWorkSuspensionOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            replay.DurablyCommitted);
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedEmergencyWorkSuspensionOutcome prepared) =>
        prepared.IsReplay
            ? transactions.Reconcile(prepared.ResultKey)
            : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public void Cancel(in PreparedEmergencyWorkSuspensionOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }
}

internal sealed class EmergencyWorkSuspensionMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(outcome.OutcomeTypeId.Value);

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
            baseSalience - Math.Min(0.3f, priorMatchingCount * 0.04f)
                - Math.Min(0.25f, age * 0.01f),
            0f,
            1f);
        return new OutcomeMemoryEvaluation(
            salience,
            salience >= 0.68f
                ? NarrativeMemoryTier.Episodic
                : NarrativeMemoryTier.Recent,
            Math.Max(evaluationDay + 2, outcome.AbsoluteDay + 2));
    }
}

internal sealed class EmergencyWorkSuspensionPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class EmergencyWorkSuspensionConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => true;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class EmergencyWorkSuspensionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "emergency-work-suspension-v1";
    private readonly IKoreanJosaFormatter josa;

    public EmergencyWorkSuspensionPerspectiveProjector(IKoreanJosaFormatter josa)
    {
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));
    }

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        TryParticipant(
            outcome,
            EmergencyWorkSuspensionOutcomeIds.WorkerRole,
            out GameplayOutcomeParticipant worker);
        TryParticipant(
            outcome,
            EmergencyWorkSuspensionOutcomeIds.TargetFacilityRole,
            out GameplayOutcomeParticipant facility);
        string workName = Fact(
            outcome,
            EmergencyWorkSuspensionOutcomeIds.WorkDisplayNameFact);
        bool workerView = perspective.Kind == NarrativePerspectiveKind.Character
            && perspective.ViewerId == worker.EntityId;
        bool facilityObjectApplied = TryWithJosa(
            facility.DisplayName,
            KoreanJosaKind.Object,
            out string facilityObject);
        bool workerSubjectApplied = TryWithJosa(
            worker.DisplayName,
            KoreanJosaKind.Subject,
            out string workerSubject);
        bool neutral;
        string text;
        if (workerView && facilityObjectApplied)
        {
            neutral = false;
            text = $"{facilityObject} 대상으로 하던 {workName} 작업을 비상 경보에 맞춰 안전하게 일시중단했다.";
        }
        else if (!workerView && facilityObjectApplied && workerSubjectApplied)
        {
            neutral = false;
            text = $"{workerSubject} {facilityObject} 대상으로 하던 {workName} 작업을 비상 경보에 맞춰 일시중단했다.";
        }
        else
        {
            neutral = true;
            text = $"작업자: {worker.DisplayName.DisplayText} · 대상: {facility.DisplayName.DisplayText} · 작업: {workName} · 비상 중단 완료.";
        }
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }

    private bool TryWithJosa(
        KoreanNameSnapshot name,
        KoreanJosaKind kind,
        out string text)
    {
        KoreanJosaFormatResult result = josa.Format(new KoreanJosaRequest(name, kind));
        text = result.Text;
        return !result.RequiresNeutralFrame;
    }

    private static bool TryParticipant(
        in GameplayOutcomeReadView outcome,
        GameplayRoleId role,
        out GameplayOutcomeParticipant participant)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant candidate = outcome.GetParticipant(index);
            if (candidate.RoleId.Equals(role))
            {
                participant = candidate;
                return true;
            }
        }
        participant = default;
        return false;
    }

    private static string Fact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId id)
    {
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(id))
                return fact.Value;
        }
        return "작업";
    }
}

public sealed class EmergencyWorkSuspensionOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new EmergencyWorkSuspensionMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EmergencyWorkSuspensionPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EmergencyWorkSuspensionConsolidator();

    public EmergencyWorkSuspensionOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new EmergencyWorkSuspensionPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        EmergencyWorkSuspensionOutcomeIds.SuspensionRecorded;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(EmergencyWorkSuspensionOutcomeIds.WorkerRole)
        || roleId.Equals(EmergencyWorkSuspensionOutcomeIds.TargetFacilityRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        (metricId.Equals(EmergencyWorkSuspensionOutcomeIds.AlertEpochMetric)
            || metricId.Equals(EmergencyWorkSuspensionOutcomeIds.ExternalProgressMetric))
            && unitId.Equals(EmergencyWorkSuspensionOutcomeIds.CountUnit)
        || (metricId.Equals(
                EmergencyWorkSuspensionOutcomeIds.InlineCompletedWorkMetric)
            || metricId.Equals(
                EmergencyWorkSuspensionOutcomeIds.InlineRequiredWorkMetric))
            && unitId.Equals(EmergencyWorkSuspensionOutcomeIds.WorkUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 2
        && outcome.SubjectCount == 2
        && outcome.MetricCount == 4
        && outcome.TagCount == 1
        && outcome.ProvenanceCount == 2
        && outcome.FactCount == 1
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "emergency-work-suspension-shape-invalid");
}
