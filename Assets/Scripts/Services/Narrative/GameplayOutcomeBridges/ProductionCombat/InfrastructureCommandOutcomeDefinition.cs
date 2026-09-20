using System;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class InfrastructureCommandOutcomeIds
{
    public const string ProducerId = "infrastructure.command";

    public static readonly GameplayOutcomeTypeId Applied =
        new("infrastructure.command-applied");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayRoleId TargetFacilityRole =
        new("target-facility");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("infrastructure-command.owner-revision");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId InfrastructureCommandTag =
        new("infrastructure-command");
    public static readonly GameplayOutcomeFactId CommandKindFact =
        new("infrastructure-command.kind");
    public static readonly GameplayOutcomeFactId TargetIdFact =
        new("infrastructure-command.target-id");
    public static readonly GameplayOutcomeFactId BeforeValueFact =
        new("infrastructure-command.before");
    public static readonly GameplayOutcomeFactId AfterValueFact =
        new("infrastructure-command.after");

    public static GameplayResultKey ResultKey(long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId($"infrastructure-command:{ownerRevision}"),
        ownerRevision,
        0);

    public static string StableKind(InfrastructureCommandOutcomeKind kind) =>
        kind switch
        {
            InfrastructureCommandOutcomeKind.PowerConnectionChanged =>
                "power-connection-changed",
            InfrastructureCommandOutcomeKind.PowerPriorityChanged =>
                "power-priority-changed",
            InfrastructureCommandOutcomeKind.PowerBreakerReset =>
                "power-breaker-reset",
            InfrastructureCommandOutcomeKind.FluidBlockageCleared =>
                "fluid-blockage-cleared",
            InfrastructureCommandOutcomeKind.WaterTransferModeChanged =>
                "water-transfer-mode-changed",
            InfrastructureCommandOutcomeKind.FluidLeakRepaired =>
                "fluid-leak-repaired",
            InfrastructureCommandOutcomeKind.ConveyorNodeEnabledChanged =>
                "conveyor-node-enabled-changed",
            InfrastructureCommandOutcomeKind.ConveyorDestinationChanged =>
                "conveyor-destination-changed",
            InfrastructureCommandOutcomeKind.ConveyorOverflowPolicyChanged =>
                "conveyor-overflow-policy-changed",
            InfrastructureCommandOutcomeKind.ConveyorFilterChanged =>
                "conveyor-filter-changed",
            InfrastructureCommandOutcomeKind.ConveyorOverflowApproved =>
                "conveyor-overflow-approved",
            InfrastructureCommandOutcomeKind.AutomationModeChanged =>
                "automation-mode-changed",
            InfrastructureCommandOutcomeKind.AutomationMaintained =>
                "automation-maintained",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}

public readonly struct InfrastructureCommandOutcomeReceipt
{
    public InfrastructureCommandOutcomeReceipt(
        in InfrastructureCommandOutcomeSource source,
        KoreanNameSnapshot facilityDisplayName,
        long ownerRevision,
        int absoluteDay)
    {
        if (!Enum.IsDefined(typeof(InfrastructureCommandOutcomeKind), source.Kind))
            throw new ArgumentOutOfRangeException(nameof(source));
        if (!GameplayOutcomeStableIdSyntax.IsValid(source.TargetId)
            || !GameplayOutcomeStableIdSyntax.IsValid(source.FacilityId))
        {
            throw new ArgumentException(
                "Canonical infrastructure command IDs are required.",
                nameof(source));
        }
        if (string.IsNullOrWhiteSpace(facilityDisplayName.DisplayText)
            || string.IsNullOrWhiteSpace(
                facilityDisplayName.DisplaySnapshotRevision))
        {
            throw new ArgumentException(
                "A frozen facility display snapshot is required.",
                nameof(facilityDisplayName));
        }
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (absoluteDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        Source = source;
        FacilityDisplayName = facilityDisplayName;
        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
    }

    public InfrastructureCommandOutcomeSource Source { get; }
    public KoreanNameSnapshot FacilityDisplayName { get; }
    public long OwnerRevision { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey =>
        InfrastructureCommandOutcomeIds.ResultKey(OwnerRevision);
}

public sealed class InfrastructureCommandOutcomeAdapter :
    GameplayOutcomeAdapter<InfrastructureCommandOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        InfrastructureCommandOutcomeIds.Applied;

    public override OutcomePrepareResult TryGetRequirements(
        in InfrastructureCommandOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid
            || !Enum.IsDefined(
                typeof(InfrastructureCommandOutcomeKind),
                receipt.Source.Kind)
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.Source.TargetId)
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.Source.FacilityId)
            || receipt.OwnerRevision <= 0L
            || receipt.AbsoluteDay <= 0
            || string.IsNullOrWhiteSpace(receipt.Source.BeforeValue)
            || string.IsNullOrWhiteSpace(receipt.Source.AfterValue))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "infrastructure-command-receipt-invalid");
        }

        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 1,
            metricCount: 1,
            subjectCount: 1,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: 4);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in InfrastructureCommandOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId facility = new(
            InfrastructureCommandOutcomeIds.FacilityKind,
            receipt.Source.FacilityId);
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                facility,
                InfrastructureCommandOutcomeIds.TargetFacilityRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.FacilityDisplayName))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility,
                0.42f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                InfrastructureCommandOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                InfrastructureCommandOutcomeIds.RevisionUnit,
                facility))
            || !builder.AddTag(
                InfrastructureCommandOutcomeIds.InfrastructureCommandTag)
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "infrastructure-command-v1"))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.ResultKey.OperationId.Value))
            || !builder.AddFact(new GameplayOutcomeFact(
                InfrastructureCommandOutcomeIds.CommandKindFact,
                InfrastructureCommandOutcomeIds.StableKind(
                    receipt.Source.Kind)))
            || !builder.AddFact(new GameplayOutcomeFact(
                InfrastructureCommandOutcomeIds.TargetIdFact,
                receipt.Source.TargetId))
            || !builder.AddFact(new GameplayOutcomeFact(
                InfrastructureCommandOutcomeIds.BeforeValueFact,
                receipt.Source.BeforeValue))
            || !builder.AddFact(new GameplayOutcomeFact(
                InfrastructureCommandOutcomeIds.AfterValueFact,
                receipt.Source.AfterValue))
            || !builder.SetLocation(new GameplayLocationReference(
                "dungeon",
                string.Empty,
                receipt.Source.FacilityX,
                receipt.Source.FacilityY)))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "infrastructure-command-write-failed");
        }
        return OutcomePrepareResult.Prepared();
    }
}

internal sealed class PreparedInfrastructureCommandOutcome :
    IPreparedInfrastructureCommandOutcome
{
    internal PreparedInfrastructureCommandOutcome(
        PreparedOwnerOutcome prepared,
        GameplayResultKey resultKey,
        in InfrastructureCommandOutcomeSource source,
        long ownerRevision,
        in InfrastructureCommandOutcomeFrozenContext frozenContext,
        bool replay)
    {
        Prepared = prepared;
        ResultKey = resultKey;
        Source = source;
        OwnerRevision = ownerRevision;
        FrozenContext = frozenContext;
        IsReplay = replay;
    }

    internal PreparedOwnerOutcome Prepared { get; }
    public GameplayResultKey ResultKey { get; }
    public InfrastructureCommandOutcomeSource Source { get; }
    public long OwnerRevision { get; }
    public InfrastructureCommandOutcomeFrozenContext FrozenContext { get; }
    public bool IsReplay { get; }
    internal bool IsValid => IsReplay ? ResultKey.IsValid : Prepared.IsValid;
}

public sealed class InfrastructureCommandGameplayOutcomeBridge :
    IInfrastructureCommandOutcomeCommitter
{
    private readonly IGameCalendar calendar;
    private readonly IGameplayOutcomeDisplayNameQuery displayNames;
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public InfrastructureCommandGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameplayOutcomeDisplayNameQuery displayNames,
        IGameCalendar calendar)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
        this.displayNames = displayNames
            ?? throw new ArgumentNullException(nameof(displayNames));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public bool TryPrepare(
        in InfrastructureCommandOutcomeSource source,
        long ownerRevision,
        out IPreparedInfrastructureCommandOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        GameplayEntityId facility = new(
            InfrastructureCommandOutcomeIds.FacilityKind,
            source.FacilityId);
        if (!displayNames.TryGetCurrentName(
                facility,
                out KoreanNameSnapshot facilityName))
        {
            failureReason = "infrastructure-command-facility-name-missing";
            return false;
        }

        InfrastructureCommandOutcomeFrozenContext context;
        try
        {
            context = new InfrastructureCommandOutcomeFrozenContext(
                facilityName.DisplayText,
                facilityName.DisplaySnapshotRevision,
                (int)facilityName.PronunciationHint.Mode,
                facilityName.PronunciationHint.Value,
                (int)facilityName.PronunciationHint.ExplicitFinalConsonant,
                facilityName.PronunciationHint.Revision,
                string.IsNullOrWhiteSpace(facilityName.Locale)
                    ? "ko-KR"
                    : facilityName.Locale,
                Math.Max(1, calendar.Day));
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "infrastructure-command-context-invalid:"
                + exception.Message;
            return false;
        }
        return TryPrepareFrozen(
            source,
            ownerRevision,
            context,
            out prepared,
            out failureReason);
    }

    public bool TryPreparePending(
        InfrastructureCommandOutcomeOutboxSaveData pending,
        out IPreparedInfrastructureCommandOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        if (pending == null)
        {
            failureReason = "infrastructure-command-pending-null";
            return false;
        }
        try
        {
            InfrastructureCommandOutcomeSource source = pending.ToSource();
            InfrastructureCommandOutcomeFrozenContext context =
                pending.ToFrozenContext();
            return TryPrepareFrozen(
                source,
                pending.ownerRevision,
                context,
                out prepared,
                out failureReason);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "infrastructure-command-pending-invalid:"
                + exception.Message;
            return false;
        }
    }

    private bool TryPrepareFrozen(
        in InfrastructureCommandOutcomeSource source,
        long ownerRevision,
        in InfrastructureCommandOutcomeFrozenContext context,
        out IPreparedInfrastructureCommandOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        if (!Enum.IsDefined(
                typeof(KoreanPronunciationMode),
                context.PronunciationMode)
            || !Enum.IsDefined(
                typeof(KoreanFinalConsonantKind),
                context.PronunciationFinalConsonant))
        {
            failureReason = "infrastructure-command-pronunciation-invalid";
            return false;
        }
        KoreanNameSnapshot facilityName = new(
            context.FacilityDisplayText,
            context.FacilityDisplayRevision,
            new KoreanPronunciationHint(
                (KoreanPronunciationMode)context.PronunciationMode,
                context.PronunciationValue,
                (KoreanFinalConsonantKind)
                    context.PronunciationFinalConsonant,
                context.PronunciationRevision),
            context.Locale);

        InfrastructureCommandOutcomeReceipt receipt;
        try
        {
            receipt = new InfrastructureCommandOutcomeReceipt(
                source,
                facilityName,
                ownerRevision,
                context.AbsoluteDay);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "infrastructure-command-receipt-invalid:"
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
        prepared = new PreparedInfrastructureCommandOutcome(
            token,
            receipt.ResultKey,
            source,
            receipt.OwnerRevision,
            context,
            replay.DurablyCommitted);
        return true;
    }

    public InfrastructureCommandOutcomeCommitResult Commit(
        IPreparedInfrastructureCommandOutcome prepared)
    {
        if (prepared is not PreparedInfrastructureCommandOutcome exact
            || !exact.IsValid)
        {
            return new InfrastructureCommandOutcomeCommitResult(
                false,
                "infrastructure-command-outcome-token-invalid");
        }
        OwnerOutcomeCommitResult result = exact.IsReplay
            ? transactions.Reconcile(exact.ResultKey)
            : transactions.Commit(exact.Prepared, exact.OwnerRevision);
        return new InfrastructureCommandOutcomeCommitResult(
            result.DurablyCommitted,
            result.DetailCode);
    }

    public void Cancel(IPreparedInfrastructureCommandOutcome prepared)
    {
        if (prepared is PreparedInfrastructureCommandOutcome exact
            && !exact.IsReplay)
        {
            transactions.Cancel(exact.Prepared);
        }
    }
}

internal sealed class InfrastructureCommandMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":"
        + Fact(outcome, InfrastructureCommandOutcomeIds.CommandKindFact));

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
            baseSalience - Math.Min(0.25f, priorMatchingCount * 0.04f)
                - Math.Min(0.25f, age * 0.015f),
            0f,
            1f);
        return new OutcomeMemoryEvaluation(
            salience,
            NarrativeMemoryTier.Recent,
            Math.Max(evaluationDay + 2, outcome.AbsoluteDay + 2));
    }

    private static string Fact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId id)
    {
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(id)) return fact.Value;
        }
        return string.Empty;
    }
}

internal sealed class InfrastructureCommandPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class InfrastructureCommandConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class InfrastructureCommandPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "infrastructure-command-v1";
    private readonly IKoreanJosaFormatter josa;

    public InfrastructureCommandPerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant facility = outcome.GetParticipant(0);
        string action = ActionLabel(Fact(
            outcome,
            InfrastructureCommandOutcomeIds.CommandKindFact));
        KoreanJosaFormatResult subject = josa.Format(
            new KoreanJosaRequest(
                facility.DisplayName,
                KoreanJosaKind.Topic));
        bool neutral = subject.RequiresNeutralFrame;
        string text = neutral
            ? $"산업 시설 {facility.DisplayName.DisplayText}: {action} 명령을 반영했다."
            : $"{subject.Text} {action} 명령을 반영했다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }

    private static string ActionLabel(string kind) => kind switch
    {
        "power-connection-changed" => "전력 연결 변경",
        "power-priority-changed" => "전력 우선순위 변경",
        "power-breaker-reset" => "차단기 복구",
        "fluid-blockage-cleared" => "배관 막힘 제거",
        "water-transfer-mode-changed" => "물 이송 방식 변경",
        "fluid-leak-repaired" => "배관 누수 수리",
        "conveyor-node-enabled-changed" => "운송 장치 가동 변경",
        "conveyor-destination-changed" => "운송 목적지 변경",
        "conveyor-overflow-policy-changed" => "넘침 처리 정책 변경",
        "conveyor-filter-changed" => "운송 필터 변경",
        "conveyor-overflow-approved" => "넘침 배출 승인",
        "automation-mode-changed" => "자동화 방식 변경",
        "automation-maintained" => "자동화 설비 정비",
        _ => "산업 설비 설정 변경"
    };

    private static string Fact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId id)
    {
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(id)) return fact.Value;
        }
        return "none";
    }
}

public sealed class InfrastructureCommandOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new InfrastructureCommandMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new InfrastructureCommandPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new InfrastructureCommandConsolidator();

    public InfrastructureCommandOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new InfrastructureCommandPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        InfrastructureCommandOutcomeIds.Applied;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(InfrastructureCommandOutcomeIds.TargetFacilityRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(InfrastructureCommandOutcomeIds.OwnerRevisionMetric)
        && unitId.Equals(InfrastructureCommandOutcomeIds.RevisionUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && outcome.ParticipantCount == 1
            && outcome.SubjectCount == 1
            && outcome.MetricCount == 1
            && outcome.TagCount == 1
            && outcome.ProvenanceCount == 2
            && outcome.FactCount == 4
            && outcome.GetParticipant(0).RoleId.Equals(
                InfrastructureCommandOutcomeIds.TargetFacilityRole);
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "infrastructure-command-shape-invalid");
    }
}

public static class InfrastructureCommandOutcomeRegistration
{
    public static void RegisterInfrastructureCommandGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register<InfrastructureCommandGameplayOutcomeBridge>(
                Lifetime.Singleton)
            .As<IInfrastructureCommandOutcomeCommitter>();
        builder.Register<InfrastructureCommandOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<InfrastructureCommandOutcomeDescriptor>(
                Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
