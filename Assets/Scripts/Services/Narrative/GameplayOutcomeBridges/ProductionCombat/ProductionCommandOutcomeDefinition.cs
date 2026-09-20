using System;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class ProductionCommandOutcomeIds
{
    public const string ProducerId = "production.command";

    public static readonly GameplayOutcomeTypeId Applied =
        new("production.command-applied");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayRoleId TargetFacilityRole =
        new("target-facility");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("production-command.owner-revision");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId ProductionCommandTag =
        new("production-command");
    public static readonly GameplayOutcomeFactId CommandKindFact =
        new("production-command.kind");
    public static readonly GameplayOutcomeFactId BillIdFact =
        new("production-command.bill-id");
    public static readonly GameplayOutcomeFactId RecipeIdFact =
        new("production-command.recipe-id");
    public static readonly GameplayOutcomeFactId RecipeNameFact =
        new("production-command.recipe-name");
    public static readonly GameplayOutcomeFactId BeforeValueFact =
        new("production-command.before");
    public static readonly GameplayOutcomeFactId AfterValueFact =
        new("production-command.after");

    public static GameplayResultKey ResultKey(long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId($"production-command:{ownerRevision}"),
        ownerRevision,
        0);

    public static string StableKind(ProductionCommandOutcomeKind kind) => kind switch
    {
        ProductionCommandOutcomeKind.BillAdded => "bill-added",
        ProductionCommandOutcomeKind.BillRemoved => "bill-removed",
        ProductionCommandOutcomeKind.BillPriorityChanged => "bill-priority-changed",
        ProductionCommandOutcomeKind.MinimumCraftQualityChanged =>
            "minimum-craft-quality-changed",
        ProductionCommandOutcomeKind.SuspensionChanged => "suspension-changed",
        ProductionCommandOutcomeKind.StockPolicyChanged => "stock-policy-changed",
        ProductionCommandOutcomeKind.OrderModeChanged => "order-mode-changed",
        ProductionCommandOutcomeKind.DistributionPolicyChanged =>
            "distribution-policy-changed",
        ProductionCommandOutcomeKind.WorkerPolicyChanged => "worker-policy-changed",
        ProductionCommandOutcomeKind.EmergencyWorkerChanged =>
            "emergency-worker-changed",
        ProductionCommandOutcomeKind.StockSensorInstalled => "stock-sensor-installed",
        ProductionCommandOutcomeKind.StockSensorRemoved => "stock-sensor-removed",
        ProductionCommandOutcomeKind.StockSensorUnlockAcknowledged =>
            "stock-sensor-unlock-acknowledged",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

public readonly struct ProductionCommandOutcomeReceipt
{
    public ProductionCommandOutcomeReceipt(
        in ProductionCommandOutcomeSource source,
        KoreanNameSnapshot facilityDisplayName,
        long ownerRevision,
        int absoluteDay)
    {
        if (!Enum.IsDefined(typeof(ProductionCommandOutcomeKind), source.Kind))
            throw new ArgumentOutOfRangeException(nameof(source));
        if (!GameplayOutcomeStableIdSyntax.IsValid(source.FacilityId))
            throw new ArgumentException(
                "A canonical production facility ID is required.",
                nameof(source));
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

        Kind = source.Kind;
        BillId = source.BillId;
        RecipeId = source.RecipeId;
        RecipeDisplayName = source.RecipeDisplayName;
        FacilityId = source.FacilityId;
        FacilityDisplayName = facilityDisplayName;
        FacilityX = source.FacilityX;
        FacilityY = source.FacilityY;
        BeforeValue = source.BeforeValue;
        AfterValue = source.AfterValue;
        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
    }

    public ProductionCommandOutcomeKind Kind { get; }
    public string BillId { get; }
    public string RecipeId { get; }
    public string RecipeDisplayName { get; }
    public string FacilityId { get; }
    public KoreanNameSnapshot FacilityDisplayName { get; }
    public int FacilityX { get; }
    public int FacilityY { get; }
    public string BeforeValue { get; }
    public string AfterValue { get; }
    public long OwnerRevision { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey =>
        ProductionCommandOutcomeIds.ResultKey(OwnerRevision);
}

public sealed class ProductionCommandOutcomeAdapter :
    GameplayOutcomeAdapter<ProductionCommandOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        ProductionCommandOutcomeIds.Applied;

    public override OutcomePrepareResult TryGetRequirements(
        in ProductionCommandOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid
            || !Enum.IsDefined(
                typeof(ProductionCommandOutcomeKind),
                receipt.Kind)
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.FacilityId)
            || receipt.OwnerRevision <= 0L
            || receipt.AbsoluteDay <= 0
            || string.IsNullOrWhiteSpace(receipt.BeforeValue)
            || string.IsNullOrWhiteSpace(receipt.AfterValue))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "production-command-receipt-invalid");
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
            factCount: 6);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in ProductionCommandOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId facility = new(
            ProductionCommandOutcomeIds.FacilityKind,
            receipt.FacilityId);
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                facility,
                ProductionCommandOutcomeIds.TargetFacilityRole,
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
                ProductionCommandOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                ProductionCommandOutcomeIds.RevisionUnit,
                facility))
            || !builder.AddTag(
                ProductionCommandOutcomeIds.ProductionCommandTag)
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "production-command-v1"))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.ResultKey.OperationId.Value))
            || !builder.AddFact(new GameplayOutcomeFact(
                ProductionCommandOutcomeIds.CommandKindFact,
                ProductionCommandOutcomeIds.StableKind(receipt.Kind)))
            || !builder.AddFact(new GameplayOutcomeFact(
                ProductionCommandOutcomeIds.BillIdFact,
                OptionalFact(receipt.BillId)))
            || !builder.AddFact(new GameplayOutcomeFact(
                ProductionCommandOutcomeIds.RecipeIdFact,
                OptionalFact(receipt.RecipeId)))
            || !builder.AddFact(new GameplayOutcomeFact(
                ProductionCommandOutcomeIds.RecipeNameFact,
                OptionalFact(receipt.RecipeDisplayName)))
            || !builder.AddFact(new GameplayOutcomeFact(
                ProductionCommandOutcomeIds.BeforeValueFact,
                receipt.BeforeValue))
            || !builder.AddFact(new GameplayOutcomeFact(
                ProductionCommandOutcomeIds.AfterValueFact,
                receipt.AfterValue))
            || !builder.SetLocation(new GameplayLocationReference(
                "dungeon",
                string.Empty,
                receipt.FacilityX,
                receipt.FacilityY)))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "production-command-write-failed");
        }
        return OutcomePrepareResult.Prepared();
    }

    private static string OptionalFact(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value;
}

internal sealed class PreparedProductionCommandOutcome :
    IPreparedProductionCommandOutcome
{
    internal PreparedProductionCommandOutcome(
        PreparedOwnerOutcome prepared,
        GameplayResultKey resultKey,
        long ownerRevision,
        in ProductionCommandOutcomeFrozenContext frozenContext,
        bool replay)
    {
        Prepared = prepared;
        ResultKey = resultKey;
        OwnerRevision = ownerRevision;
        FrozenContext = frozenContext;
        IsReplay = replay;
    }

    internal PreparedOwnerOutcome Prepared { get; }
    public GameplayResultKey ResultKey { get; }
    public long OwnerRevision { get; }
    public ProductionCommandOutcomeFrozenContext FrozenContext { get; }
    public bool IsReplay { get; }
    internal bool IsValid => IsReplay ? ResultKey.IsValid : Prepared.IsValid;
}

public sealed class ProductionCommandGameplayOutcomeBridge :
    IProductionCommandOutcomeCommitter
{
    private readonly IGameCalendar calendar;
    private readonly IGameplayOutcomeDisplayNameQuery displayNames;
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public ProductionCommandGameplayOutcomeBridge(
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
        in ProductionCommandOutcomeSource source,
        long ownerRevision,
        out IPreparedProductionCommandOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        GameplayEntityId facility = new(
            ProductionCommandOutcomeIds.FacilityKind,
            source.FacilityId);
        if (!displayNames.TryGetCurrentName(
                facility,
                out KoreanNameSnapshot facilityName))
        {
            failureReason = "production-command-facility-name-missing";
            return false;
        }

        ProductionCommandOutcomeFrozenContext context;
        try
        {
            context = new ProductionCommandOutcomeFrozenContext(
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
            failureReason = "production-command-context-invalid:"
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
        ProductionCommandOutcomeOutboxSaveData pending,
        out IPreparedProductionCommandOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        if (pending == null)
        {
            failureReason = "production-command-pending-null";
            return false;
        }
        try
        {
            ProductionCommandOutcomeSource source = pending.ToSource();
            ProductionCommandOutcomeFrozenContext context =
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
            failureReason = "production-command-pending-invalid:"
                + exception.Message;
            return false;
        }
    }

    private bool TryPrepareFrozen(
        in ProductionCommandOutcomeSource source,
        long ownerRevision,
        in ProductionCommandOutcomeFrozenContext context,
        out IPreparedProductionCommandOutcome prepared,
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
            failureReason = "production-command-pronunciation-invalid";
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

        ProductionCommandOutcomeReceipt receipt;
        try
        {
            receipt = new ProductionCommandOutcomeReceipt(
                source,
                facilityName,
                ownerRevision,
                context.AbsoluteDay);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "production-command-receipt-invalid:"
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
        prepared = new PreparedProductionCommandOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            context,
            replay.DurablyCommitted);
        return true;
    }

    public ProductionCommandOutcomeCommitResult Commit(
        IPreparedProductionCommandOutcome prepared)
    {
        if (prepared is not PreparedProductionCommandOutcome exact
            || !exact.IsValid)
        {
            return new ProductionCommandOutcomeCommitResult(
                false,
                "production-command-outcome-token-invalid");
        }
        OwnerOutcomeCommitResult result = exact.IsReplay
            ? transactions.Reconcile(exact.ResultKey)
            : transactions.Commit(exact.Prepared, exact.OwnerRevision);
        return new ProductionCommandOutcomeCommitResult(
            result.DurablyCommitted,
            result.DetailCode);
    }

    public void Cancel(IPreparedProductionCommandOutcome prepared)
    {
        if (prepared is PreparedProductionCommandOutcome exact
            && !exact.IsReplay)
        {
            transactions.Cancel(exact.Prepared);
        }
    }
}

internal sealed class ProductionCommandMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":"
        + Fact(outcome, ProductionCommandOutcomeIds.CommandKindFact));

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

internal sealed class ProductionCommandPerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class ProductionCommandConsolidator : IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class ProductionCommandPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "production-command-v1";
    private readonly IKoreanJosaFormatter josa;

    public ProductionCommandPerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant facility = outcome.GetParticipant(0);
        string action = ActionLabel(Fact(
            outcome,
            ProductionCommandOutcomeIds.CommandKindFact));
        string recipe = Fact(outcome, ProductionCommandOutcomeIds.RecipeNameFact);
        string suffix = string.Equals(recipe, "none", StringComparison.Ordinal)
            ? $"{action} 명령을 반영했다."
            : $"{recipe} 주문의 {action} 명령을 반영했다.";
        KoreanJosaFormatResult subject = josa.Format(
            new KoreanJosaRequest(
                facility.DisplayName,
                KoreanJosaKind.Topic));
        bool neutral = subject.RequiresNeutralFrame;
        string text = neutral
            ? $"생산 시설 {facility.DisplayName.DisplayText}: {suffix}"
            : $"{subject.Text} {suffix}";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }

    private static string ActionLabel(string kind) => kind switch
    {
        "bill-added" => "생산 주문 추가",
        "bill-removed" => "생산 주문 제거",
        "bill-priority-changed" => "우선순위 변경",
        "minimum-craft-quality-changed" => "최저 품질 변경",
        "suspension-changed" => "가동 상태 변경",
        "stock-policy-changed" => "재고 정책 변경",
        "order-mode-changed" => "주문 방식 변경",
        "distribution-policy-changed" => "분배 정책 변경",
        "worker-policy-changed" => "작업자 정책 변경",
        "emergency-worker-changed" => "긴급 작업자 변경",
        "stock-sensor-installed" => "재고 감지기 설치",
        "stock-sensor-removed" => "재고 감지기 제거",
        "stock-sensor-unlock-acknowledged" => "재고 감지기 해금 확인",
        _ => "생산 설정 변경"
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

public sealed class ProductionCommandOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new ProductionCommandMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new ProductionCommandPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new ProductionCommandConsolidator();

    public ProductionCommandOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new ProductionCommandPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        ProductionCommandOutcomeIds.Applied;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(ProductionCommandOutcomeIds.TargetFacilityRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(ProductionCommandOutcomeIds.OwnerRevisionMetric)
        && unitId.Equals(ProductionCommandOutcomeIds.RevisionUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && outcome.ParticipantCount == 1
            && outcome.SubjectCount == 1
            && outcome.MetricCount == 1
            && outcome.TagCount == 1
            && outcome.ProvenanceCount == 2
            && outcome.FactCount == 6
            && outcome.GetParticipant(0).RoleId.Equals(
                ProductionCommandOutcomeIds.TargetFacilityRole);
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "production-command-shape-invalid");
    }
}

public static class ProductionCommandOutcomeRegistration
{
    public static void RegisterProductionCommandGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register<ProductionCommandGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<IProductionCommandOutcomeCommitter>();
        builder.Register<ProductionCommandOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<ProductionCommandOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
