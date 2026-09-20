using System;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class DungeonSpaceExpansionOutcomeIds
{
    public const string ProducerId = "infrastructure.dungeon-space-expansion";
    public const string MainDungeonSpaceId = "dungeon:main-interior";

    public static readonly GameplayOutcomeTypeId Expanded =
        new("infrastructure.dungeon-space-expanded");
    public static readonly GameplayEntityKindId DungeonSpaceKind =
        new("dungeon-space");
    public static readonly GameplayEntityKindId ResearchProjectKind =
        new("research-project");
    public static readonly GameplayRoleId ExpandedSpaceRole =
        new("expanded-space");
    public static readonly GameplayRoleId UnlockingResearchRole =
        new("unlocking-research");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("dungeon-space.owner-revision");
    public static readonly GameplayMetricId TierMetric =
        new("dungeon-space.tier");
    public static readonly GameplayMetricId PreviousInteriorColumnsMetric =
        new("dungeon-space.previous-interior-columns");
    public static readonly GameplayMetricId CurrentInteriorColumnsMetric =
        new("dungeon-space.current-interior-columns");
    public static readonly GameplayMetricId AddedInteriorColumnsMetric =
        new("dungeon-space.added-interior-columns");
    public static readonly GameplayMetricId PreviousGridWidthMetric =
        new("dungeon-space.previous-grid-width");
    public static readonly GameplayMetricId CurrentGridWidthMetric =
        new("dungeon-space.current-grid-width");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayMetricUnitId TierUnit = new("tier");
    public static readonly GameplayMetricUnitId ColumnUnit = new("column");
    public static readonly GameplayOutcomeTagId ExpansionTag =
        new("dungeon-space-expansion");
    public static readonly GameplayOutcomeFactId ResearchProjectIdFact =
        new("dungeon-space.research-project-id");

    public static GameplayEntityId MainDungeonSpace => new(
        DungeonSpaceKind,
        MainDungeonSpaceId);

    public static GameplayResultKey ResultKey(
        string researchProjectId,
        long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId("dungeon-space-expansion:" + researchProjectId),
        ownerRevision,
        0);
}

public static class DungeonSpaceExpansionOutcomeNames
{
    public static KoreanNameSnapshot Snapshot(string stableId, string displayText)
    {
        string identity = GameplayOutcomeStableIdSyntax.Require(
            stableId,
            nameof(stableId));
        string display = displayText?.Trim() ?? string.Empty;
        if (display.Length == 0)
        {
            throw new ArgumentException(
                "A dungeon-space expansion display snapshot is required.",
                nameof(displayText));
        }
        string revision = "dungeon-space-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(identity + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }

    public static KoreanNameSnapshot DungeonSpace => Snapshot(
        DungeonSpaceExpansionOutcomeIds.MainDungeonSpaceId,
        "던전 구역");
}

public readonly struct DungeonSpaceExpansionOutcomeReceipt
{
    public DungeonSpaceExpansionOutcomeReceipt(
        string researchProjectId,
        KoreanNameSnapshot researchDisplayName,
        long ownerRevision,
        int tier,
        int previousInteriorColumns,
        int currentInteriorColumns,
        int previousGridWidth,
        int currentGridWidth,
        int absoluteDay,
        int entranceX,
        int entranceY)
    {
        ResearchProjectId = GameplayOutcomeStableIdSyntax.Require(
            researchProjectId,
            nameof(researchProjectId));
        RequireName(researchDisplayName, nameof(researchDisplayName));
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (tier < 0)
            throw new ArgumentOutOfRangeException(nameof(tier));
        if (previousInteriorColumns < 1
            || currentInteriorColumns <= previousInteriorColumns)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentInteriorColumns),
                "A dungeon-space outcome must describe a positive expansion.");
        }
        if (ownerRevision != currentInteriorColumns)
        {
            throw new ArgumentException(
                "Dungeon-space owner revision must equal the committed interior-column count.",
                nameof(ownerRevision));
        }
        if (previousGridWidth < 1 || currentGridWidth < previousGridWidth)
            throw new ArgumentOutOfRangeException(nameof(currentGridWidth));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        ResearchDisplayName = researchDisplayName;
        OwnerRevision = ownerRevision;
        Tier = tier;
        PreviousInteriorColumns = previousInteriorColumns;
        CurrentInteriorColumns = currentInteriorColumns;
        PreviousGridWidth = previousGridWidth;
        CurrentGridWidth = currentGridWidth;
        AbsoluteDay = absoluteDay;
        EntranceX = entranceX;
        EntranceY = entranceY;
    }

    public string ResearchProjectId { get; }
    public KoreanNameSnapshot ResearchDisplayName { get; }
    public long OwnerRevision { get; }
    public int Tier { get; }
    public int PreviousInteriorColumns { get; }
    public int CurrentInteriorColumns { get; }
    public int AddedInteriorColumns =>
        CurrentInteriorColumns - PreviousInteriorColumns;
    public int PreviousGridWidth { get; }
    public int CurrentGridWidth { get; }
    public int AbsoluteDay { get; }
    public int EntranceX { get; }
    public int EntranceY { get; }
    public GameplayResultKey ResultKey =>
        DungeonSpaceExpansionOutcomeIds.ResultKey(
            ResearchProjectId,
            OwnerRevision);

    private static void RequireName(
        KoreanNameSnapshot value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value.DisplayText)
            || string.IsNullOrWhiteSpace(value.DisplaySnapshotRevision)
            || string.IsNullOrWhiteSpace(value.PronunciationHint.Revision))
        {
            throw new ArgumentException(
                "A frozen Korean display snapshot is required.",
                parameterName);
        }
    }
}

public sealed class DungeonSpaceExpansionOutcomeAdapter :
    GameplayOutcomeAdapter<DungeonSpaceExpansionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        DungeonSpaceExpansionOutcomeIds.Expanded;

    public override OutcomePrepareResult TryGetRequirements(
        in DungeonSpaceExpansionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid
            || receipt.OwnerRevision != receipt.CurrentInteriorColumns
            || receipt.AddedInteriorColumns <= 0)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "dungeon-space-expansion-receipt-invalid");
        }
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 2,
            subjectCount: 2,
            metricCount: 7,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: 1);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in DungeonSpaceExpansionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId dungeon = DungeonSpaceExpansionOutcomeIds.MainDungeonSpace;
        GameplayEntityId research = new(
            DungeonSpaceExpansionOutcomeIds.ResearchProjectKind,
            receipt.ResearchProjectId);
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                dungeon,
                DungeonSpaceExpansionOutcomeIds.ExpandedSpaceRole,
                GameplayParticipationKind.Direct,
                true,
                DungeonSpaceExpansionOutcomeNames.DungeonSpace))
            || !builder.AddParticipant(new GameplayOutcomeParticipant(
                research,
                DungeonSpaceExpansionOutcomeIds.UnlockingResearchRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.ResearchDisplayName))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                dungeon,
                0.78f,
                NarrativeMemoryTier.Episodic,
                false,
                false,
                0))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                research,
                0.64f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            || !Metric(
                ref builder,
                DungeonSpaceExpansionOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                DungeonSpaceExpansionOutcomeIds.RevisionUnit,
                dungeon)
            || !Metric(
                ref builder,
                DungeonSpaceExpansionOutcomeIds.TierMetric,
                receipt.Tier,
                DungeonSpaceExpansionOutcomeIds.TierUnit,
                research)
            || !Metric(
                ref builder,
                DungeonSpaceExpansionOutcomeIds.PreviousInteriorColumnsMetric,
                receipt.PreviousInteriorColumns,
                DungeonSpaceExpansionOutcomeIds.ColumnUnit,
                dungeon)
            || !Metric(
                ref builder,
                DungeonSpaceExpansionOutcomeIds.CurrentInteriorColumnsMetric,
                receipt.CurrentInteriorColumns,
                DungeonSpaceExpansionOutcomeIds.ColumnUnit,
                dungeon)
            || !Metric(
                ref builder,
                DungeonSpaceExpansionOutcomeIds.AddedInteriorColumnsMetric,
                receipt.AddedInteriorColumns,
                DungeonSpaceExpansionOutcomeIds.ColumnUnit,
                dungeon)
            || !Metric(
                ref builder,
                DungeonSpaceExpansionOutcomeIds.PreviousGridWidthMetric,
                receipt.PreviousGridWidth,
                DungeonSpaceExpansionOutcomeIds.ColumnUnit,
                dungeon)
            || !Metric(
                ref builder,
                DungeonSpaceExpansionOutcomeIds.CurrentGridWidthMetric,
                receipt.CurrentGridWidth,
                DungeonSpaceExpansionOutcomeIds.ColumnUnit,
                dungeon)
            || !builder.AddTag(DungeonSpaceExpansionOutcomeIds.ExpansionTag)
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "dungeon-space-expansion-v1"))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.ResultKey.OperationId.Value))
            || !builder.AddFact(new GameplayOutcomeFact(
                DungeonSpaceExpansionOutcomeIds.ResearchProjectIdFact,
                receipt.ResearchProjectId))
            || !builder.SetLocation(new GameplayLocationReference(
                "dungeon",
                string.Empty,
                receipt.EntranceX,
                receipt.EntranceY)))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "dungeon-space-expansion-write-failed");
        }
        return OutcomePrepareResult.Prepared();
    }

    private static bool Metric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId subject) =>
        builder.AddMetric(new GameplayOutcomeMetric(id, value, unit, subject));
}

public readonly struct PreparedDungeonSpaceExpansionOutcome
{
    internal PreparedDungeonSpaceExpansionOutcome(
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

public interface IDungeonSpaceExpansionOutcomeCommitter
{
    bool TryPrepare(
        in DungeonSpaceExpansionOutcomeReceipt receipt,
        out PreparedDungeonSpaceExpansionOutcome prepared,
        out string failureReason);
    OwnerOutcomeCommitResult Commit(
        in PreparedDungeonSpaceExpansionOutcome prepared);
    void Cancel(in PreparedDungeonSpaceExpansionOutcome prepared);
}

public sealed class DungeonSpaceExpansionGameplayOutcomeBridge :
    IDungeonSpaceExpansionOutcomeCommitter
{
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public DungeonSpaceExpansionGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
    }

    public bool TryPrepare(
        in DungeonSpaceExpansionOutcomeReceipt receipt,
        out PreparedDungeonSpaceExpansionOutcome prepared,
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
        prepared = new PreparedDungeonSpaceExpansionOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            replay.DurablyCommitted);
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedDungeonSpaceExpansionOutcome prepared) =>
        prepared.IsReplay
            ? transactions.Reconcile(prepared.ResultKey)
            : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public void Cancel(in PreparedDungeonSpaceExpansionOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }
}

internal sealed class DungeonSpaceExpansionMemoryPolicy : IOutcomeMemoryPolicy
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
        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float salience = Math.Clamp(0.78f - Math.Min(0.18f, age * 0.004f), 0f, 1f);
        return new OutcomeMemoryEvaluation(
            salience,
            NarrativeMemoryTier.Episodic,
            Math.Max(evaluationDay + 7, outcome.AbsoluteDay + 7));
    }
}

internal sealed class DungeonSpaceExpansionPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class DungeonSpaceExpansionConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class DungeonSpaceExpansionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "dungeon-space-expansion-v1";
    private readonly IKoreanJosaFormatter josa;

    public DungeonSpaceExpansionPerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant dungeon = FindParticipant(
            outcome,
            DungeonSpaceExpansionOutcomeIds.ExpandedSpaceRole);
        GameplayOutcomeParticipant research = FindParticipant(
            outcome,
            DungeonSpaceExpansionOutcomeIds.UnlockingResearchRole);
        int before = (int)Metric(
            outcome,
            DungeonSpaceExpansionOutcomeIds.PreviousInteriorColumnsMetric);
        int after = (int)Metric(
            outcome,
            DungeonSpaceExpansionOutcomeIds.CurrentInteriorColumnsMetric);
        int added = (int)Metric(
            outcome,
            DungeonSpaceExpansionOutcomeIds.AddedInteriorColumnsMetric);
        KoreanJosaFormatResult topic = josa.Format(new KoreanJosaRequest(
            dungeon.DisplayName,
            KoreanJosaKind.Topic));
        string text = topic.RequiresNeutralFrame
            ? $"던전 구역 확장: {research.DisplayName.DisplayText} 연구 · 내부 폭 {before}칸 → {after}칸 (+{added}칸)."
            : $"{topic.Text} {research.DisplayName.DisplayText} 연구에 따라 내부 폭이 {before}칸에서 {after}칸으로 {added}칸 넓어졌다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            topic.RequiresNeutralFrame);
    }

    private static GameplayOutcomeParticipant FindParticipant(
        in GameplayOutcomeReadView outcome,
        GameplayRoleId role)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant value = outcome.GetParticipant(index);
            if (value.RoleId.Equals(role)) return value;
        }
        return default;
    }

    private static double Metric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId id)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric value = outcome.GetMetric(index);
            if (value.MetricId.Equals(id)) return value.Value;
        }
        return 0d;
    }
}

public sealed class DungeonSpaceExpansionOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new DungeonSpaceExpansionMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new DungeonSpaceExpansionPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new DungeonSpaceExpansionConsolidator();

    public DungeonSpaceExpansionOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new DungeonSpaceExpansionPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        DungeonSpaceExpansionOutcomeIds.Expanded;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(DungeonSpaceExpansionOutcomeIds.ExpandedSpaceRole)
        || roleId.Equals(DungeonSpaceExpansionOutcomeIds.UnlockingResearchRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(DungeonSpaceExpansionOutcomeIds.OwnerRevisionMetric)
            && unitId.Equals(DungeonSpaceExpansionOutcomeIds.RevisionUnit)
        || metricId.Equals(DungeonSpaceExpansionOutcomeIds.TierMetric)
            && unitId.Equals(DungeonSpaceExpansionOutcomeIds.TierUnit)
        || (metricId.Equals(DungeonSpaceExpansionOutcomeIds.PreviousInteriorColumnsMetric)
            || metricId.Equals(DungeonSpaceExpansionOutcomeIds.CurrentInteriorColumnsMetric)
            || metricId.Equals(DungeonSpaceExpansionOutcomeIds.AddedInteriorColumnsMetric)
            || metricId.Equals(DungeonSpaceExpansionOutcomeIds.PreviousGridWidthMetric)
            || metricId.Equals(DungeonSpaceExpansionOutcomeIds.CurrentGridWidthMetric))
            && unitId.Equals(DungeonSpaceExpansionOutcomeIds.ColumnUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        int expandedSpaces = 0;
        int researchProjects = 0;
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayRoleId role = outcome.GetParticipant(index).RoleId;
            if (role.Equals(DungeonSpaceExpansionOutcomeIds.ExpandedSpaceRole))
                expandedSpaces++;
            if (role.Equals(DungeonSpaceExpansionOutcomeIds.UnlockingResearchRole))
                researchProjects++;
        }
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && expandedSpaces == 1
            && researchProjects == 1
            && outcome.ParticipantCount == 2
            && outcome.SubjectCount == 2
            && outcome.MetricCount == 7
            && outcome.TagCount == 1
            && outcome.ProvenanceCount == 2
            && outcome.FactCount == 1;
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "dungeon-space-expansion-shape-invalid");
    }
}

public static class DungeonSpaceExpansionOutcomeRegistration
{
    public static void RegisterDungeonSpaceExpansionGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register<DungeonSpaceExpansionGameplayOutcomeBridge>(
                Lifetime.Singleton)
            .As<IDungeonSpaceExpansionOutcomeCommitter>();
        builder.Register<DungeonSpaceExpansionOutcomeAdapter>(
                Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<DungeonSpaceExpansionOutcomeDescriptor>(
                Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
