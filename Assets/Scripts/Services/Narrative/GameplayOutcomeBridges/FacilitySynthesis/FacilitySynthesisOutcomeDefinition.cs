using System;
using System.Collections.Generic;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class FacilitySynthesisOutcomeIds
{
    public const string ProducerId = "facility.synthesis";

    public static readonly GameplayOutcomeTypeId Completed =
        new("facility.synthesis-completed");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayEntityKindId BuildingDefinitionKind =
        new("building-definition");
    public static readonly GameplayRoleId MaterialRole = new("material-facility");
    public static readonly GameplayRoleId ResultRole = new("result-facility");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("synthesis.owner-revision");
    public static readonly GameplayMetricId InheritedLevelMetric =
        new("synthesis.inherited-level");
    public static readonly GameplayMetricId MaterialCountMetric =
        new("synthesis.material-count");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayMetricUnitId LevelUnit = new("level");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayOutcomeTagId SynthesisTag =
        new("facility-synthesis");
    public static readonly GameplayOutcomeFactId RecipeIdFact =
        new("synthesis.recipe-id");
    public static readonly GameplayOutcomeFactId RecipeNameFact =
        new("synthesis.recipe-name");
    public static readonly GameplayOutcomeFactId ResultDefinitionFact =
        new("synthesis.result-definition-id");
    public static readonly GameplayOutcomeFactId MaterialDefinitionsFact =
        new("synthesis.material-definition-ids");

    public static GameplayResultKey ResultKey(
        string survivorFacilityId,
        long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId(
            $"facility-synthesis:{survivorFacilityId}:{ownerRevision}"),
        ownerRevision,
        0);
}

public static class FacilitySynthesisOutcomeNames
{
    public static KoreanNameSnapshot Snapshot(string stableId, string displayText)
    {
        string identity = GameplayOutcomeStableIdSyntax.Require(
            stableId,
            nameof(stableId));
        string display = displayText?.Trim() ?? string.Empty;
        if (display.Length == 0)
            throw new ArgumentException(
                "A facility synthesis display snapshot is required.",
                nameof(displayText));
        string revision = "facility-synthesis-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(identity + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }
}

public readonly struct FacilitySynthesisOutcomeReceipt
{
    private readonly string[] materialPersistentIds;
    private readonly string[] materialDefinitionIds;
    private readonly KoreanNameSnapshot[] materialDisplayNames;

    public FacilitySynthesisOutcomeReceipt(
        string survivorFacilityId,
        long ownerRevision,
        string recipeId,
        string recipeDisplayName,
        string resultDefinitionId,
        KoreanNameSnapshot resultDisplayName,
        IReadOnlyList<string> materialFacilityIds,
        IReadOnlyList<string> materialBuildingDefinitionIds,
        IReadOnlyList<KoreanNameSnapshot> materialNames,
        int inheritedLevel,
        int absoluteDay,
        int x,
        int y)
    {
        SurvivorFacilityId = GameplayOutcomeStableIdSyntax.Require(
            survivorFacilityId,
            nameof(survivorFacilityId));
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        RecipeId = RequireText(recipeId, nameof(recipeId));
        RecipeDisplayName = RequireText(
            recipeDisplayName,
            nameof(recipeDisplayName));
        ResultDefinitionId = GameplayOutcomeStableIdSyntax.Require(
            resultDefinitionId,
            nameof(resultDefinitionId));
        RequireName(resultDisplayName, nameof(resultDisplayName));
        if (materialFacilityIds == null
            || materialBuildingDefinitionIds == null
            || materialNames == null
            || materialFacilityIds.Count < 2
            || materialFacilityIds.Count != materialBuildingDefinitionIds.Count
            || materialFacilityIds.Count != materialNames.Count)
        {
            throw new ArgumentException(
                "Facility synthesis requires matching material snapshots.");
        }

        materialPersistentIds = new string[materialFacilityIds.Count];
        materialDefinitionIds = new string[materialFacilityIds.Count];
        materialDisplayNames = new KoreanNameSnapshot[materialFacilityIds.Count];
        HashSet<string> uniqueFacilityIds = new(StringComparer.Ordinal);
        bool survivorPresent = false;
        for (int index = 0; index < materialFacilityIds.Count; index++)
        {
            string materialId = GameplayOutcomeStableIdSyntax.Require(
                materialFacilityIds[index],
                nameof(materialFacilityIds));
            if (!uniqueFacilityIds.Add(materialId))
                throw new ArgumentException(
                    "Facility synthesis material IDs must be unique.",
                    nameof(materialFacilityIds));
            string definitionId = GameplayOutcomeStableIdSyntax.Require(
                materialBuildingDefinitionIds[index],
                nameof(materialBuildingDefinitionIds));
            KoreanNameSnapshot name = materialNames[index];
            RequireName(name, nameof(materialNames));
            materialPersistentIds[index] = materialId;
            materialDefinitionIds[index] = definitionId;
            materialDisplayNames[index] = name;
            survivorPresent |= string.Equals(
                materialId,
                SurvivorFacilityId,
                StringComparison.Ordinal);
        }
        if (!survivorPresent)
            throw new ArgumentException(
                "The surviving facility must be one of the synthesis materials.");
        if (inheritedLevel <= 0)
            throw new ArgumentOutOfRangeException(nameof(inheritedLevel));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        OwnerRevision = ownerRevision;
        ResultDisplayName = resultDisplayName;
        InheritedLevel = inheritedLevel;
        AbsoluteDay = absoluteDay;
        X = x;
        Y = y;
    }

    public string SurvivorFacilityId { get; }
    public long OwnerRevision { get; }
    public string RecipeId { get; }
    public string RecipeDisplayName { get; }
    public string ResultDefinitionId { get; }
    public KoreanNameSnapshot ResultDisplayName { get; }
    public IReadOnlyList<string> MaterialPersistentIds => materialPersistentIds;
    public IReadOnlyList<string> MaterialDefinitionIds => materialDefinitionIds;
    public IReadOnlyList<KoreanNameSnapshot> MaterialDisplayNames =>
        materialDisplayNames;
    public int MaterialCount => materialPersistentIds?.Length ?? 0;
    public int InheritedLevel { get; }
    public int AbsoluteDay { get; }
    public int X { get; }
    public int Y { get; }
    public GameplayResultKey ResultKey => FacilitySynthesisOutcomeIds.ResultKey(
        SurvivorFacilityId,
        OwnerRevision);
    public string MaterialDefinitionList => string.Join(",", materialDefinitionIds);

    private static string RequireText(string value, string parameterName)
    {
        string result = value?.Trim() ?? string.Empty;
        if (result.Length == 0)
            throw new ArgumentException("A non-empty snapshot is required.", parameterName);
        return result;
    }

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

public sealed class FacilitySynthesisOutcomeAdapter :
    GameplayOutcomeAdapter<FacilitySynthesisOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        FacilitySynthesisOutcomeIds.Completed;

    public override OutcomePrepareResult TryGetRequirements(
        in FacilitySynthesisOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid
            || receipt.OwnerRevision <= 0L
            || receipt.MaterialCount < 2
            || receipt.InheritedLevel <= 0
            || receipt.AbsoluteDay < 0)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "facility-synthesis-receipt-invalid");
        }
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: receipt.MaterialCount + 1,
            metricCount: 3,
            subjectCount: receipt.MaterialCount,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: 4);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in FacilitySynthesisOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId resultFacility = new(
            FacilitySynthesisOutcomeIds.FacilityKind,
            receipt.SurvivorFacilityId);
        for (int index = 0; index < receipt.MaterialCount; index++)
        {
            GameplayEntityId material = new(
                FacilitySynthesisOutcomeIds.FacilityKind,
                receipt.MaterialPersistentIds[index]);
            if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                    material,
                    FacilitySynthesisOutcomeIds.MaterialRole,
                    GameplayParticipationKind.Direct,
                    true,
                    receipt.MaterialDisplayNames[index]))
                || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                    material,
                    material.Equals(resultFacility) ? 0.82f : 0.62f,
                    NarrativeMemoryTier.Recent,
                    false,
                    false,
                    0)))
            {
                return FailedWrite();
            }
        }
        GameplayEntityId resultDefinition = new(
            FacilitySynthesisOutcomeIds.BuildingDefinitionKind,
            receipt.ResultDefinitionId);
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                resultFacility,
                FacilitySynthesisOutcomeIds.ResultRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.ResultDisplayName))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                FacilitySynthesisOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                FacilitySynthesisOutcomeIds.RevisionUnit,
                resultFacility))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                FacilitySynthesisOutcomeIds.InheritedLevelMetric,
                receipt.InheritedLevel,
                FacilitySynthesisOutcomeIds.LevelUnit,
                resultDefinition))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                FacilitySynthesisOutcomeIds.MaterialCountMetric,
                receipt.MaterialCount,
                FacilitySynthesisOutcomeIds.CountUnit,
                resultFacility))
            || !builder.AddTag(FacilitySynthesisOutcomeIds.SynthesisTag)
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "facility-synthesis-v1"))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.ResultKey.OperationId.Value))
            || !builder.AddFact(new GameplayOutcomeFact(
                FacilitySynthesisOutcomeIds.RecipeIdFact,
                receipt.RecipeId))
            || !builder.AddFact(new GameplayOutcomeFact(
                FacilitySynthesisOutcomeIds.RecipeNameFact,
                receipt.RecipeDisplayName))
            || !builder.AddFact(new GameplayOutcomeFact(
                FacilitySynthesisOutcomeIds.ResultDefinitionFact,
                receipt.ResultDefinitionId))
            || !builder.AddFact(new GameplayOutcomeFact(
                FacilitySynthesisOutcomeIds.MaterialDefinitionsFact,
                receipt.MaterialDefinitionList))
            || !builder.SetLocation(new GameplayLocationReference(
                "dungeon",
                string.Empty,
                receipt.X,
                receipt.Y)))
        {
            return FailedWrite();
        }
        return OutcomePrepareResult.Prepared();
    }

    private static OutcomePrepareResult FailedWrite() => new(
        OutcomePrepareCode.AdapterWriteFailed,
        "facility-synthesis-write-failed");
}

public readonly struct PreparedFacilitySynthesisOutcome
{
    internal PreparedFacilitySynthesisOutcome(
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

public interface IFacilitySynthesisOutcomeCommitter
{
    bool TryPrepare(
        in FacilitySynthesisOutcomeReceipt receipt,
        out PreparedFacilitySynthesisOutcome prepared,
        out string failureReason);
    OwnerOutcomeCommitResult Commit(
        in PreparedFacilitySynthesisOutcome prepared);
    void Cancel(in PreparedFacilitySynthesisOutcome prepared);
}

public sealed class FacilitySynthesisGameplayOutcomeBridge :
    IFacilitySynthesisOutcomeCommitter
{
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public FacilitySynthesisGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
    }

    public bool TryPrepare(
        in FacilitySynthesisOutcomeReceipt receipt,
        out PreparedFacilitySynthesisOutcome prepared,
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
        prepared = new PreparedFacilitySynthesisOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            replay.DurablyCommitted);
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedFacilitySynthesisOutcome prepared) =>
        prepared.IsReplay
            ? transactions.Reconcile(prepared.ResultKey)
            : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public void Cancel(in PreparedFacilitySynthesisOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }
}

internal sealed class FacilitySynthesisMemoryPolicy : IOutcomeMemoryPolicy
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
            baseSalience - Math.Min(0.22f, priorMatchingCount * 0.03f)
                - Math.Min(0.2f, age * 0.008f),
            0f,
            1f);
        return new OutcomeMemoryEvaluation(
            salience,
            salience >= 0.7f
                ? NarrativeMemoryTier.Episodic
                : NarrativeMemoryTier.Recent,
            Math.Max(evaluationDay + 3, outcome.AbsoluteDay + 3));
    }
}

internal sealed class FacilitySynthesisPerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class FacilitySynthesisConsolidator : IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class FacilitySynthesisPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "facility-synthesis-v1";
    private readonly IKoreanJosaFormatter josa;

    public FacilitySynthesisPerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        TryParticipant(
            outcome,
            FacilitySynthesisOutcomeIds.ResultRole,
            out GameplayOutcomeParticipant result);
        string recipe = Fact(outcome, FacilitySynthesisOutcomeIds.RecipeNameFact);
        int materialCount = (int)Metric(
            outcome,
            FacilitySynthesisOutcomeIds.MaterialCountMetric);
        bool resultView = perspective.Kind == NarrativePerspectiveKind.Facility
            && perspective.ViewerId == result.EntityId;
        GameplayOutcomeParticipant material = default;
        bool materialView = !resultView
            && perspective.Kind == NarrativePerspectiveKind.Facility
            && TryParticipant(
                outcome,
                FacilitySynthesisOutcomeIds.MaterialRole,
                perspective.ViewerId,
                out material);
        string text;
        bool neutral;
        if (resultView && TryWithJosa(
                result.DisplayName,
                KoreanJosaKind.Topic,
                out string resultTopic))
        {
            text = $"{resultTopic} {recipe} 조합으로 {materialCount}개 시설의 기능을 계승해 완성되었다.";
            neutral = false;
        }
        else if (materialView && TryWithJosa(
                     material.DisplayName,
                     KoreanJosaKind.Subject,
                     out string materialSubject))
        {
            text = $"{materialSubject} {recipe} 조합의 재료가 되어 {result.DisplayName.DisplayText} 완성에 참여했다.";
            neutral = false;
        }
        else if (TryWithJosa(
                     result.DisplayName,
                     KoreanJosaKind.Subject,
                     out string resultSubject))
        {
            text = $"{resultSubject} {recipe} 조합으로 {materialCount}개 시설을 계승해 완성되었다.";
            neutral = false;
        }
        else
        {
            text = $"합성 결과: {result.DisplayName.DisplayText} · 조합식: {recipe} · 재료 시설: {materialCount}개.";
            neutral = true;
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

    private static bool TryParticipant(
        in GameplayOutcomeReadView outcome,
        GameplayRoleId role,
        GameplayEntityId entity,
        out GameplayOutcomeParticipant participant)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant candidate = outcome.GetParticipant(index);
            if (candidate.RoleId.Equals(role) && candidate.EntityId == entity)
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
            if (fact.FactId.Equals(id)) return fact.Value;
        }
        return "시설 합성";
    }

    private static double Metric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId id)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(id)) return metric.Value;
        }
        return 0d;
    }
}

public sealed class FacilitySynthesisOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new FacilitySynthesisMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new FacilitySynthesisPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new FacilitySynthesisConsolidator();

    public FacilitySynthesisOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new FacilitySynthesisPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        FacilitySynthesisOutcomeIds.Completed;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(FacilitySynthesisOutcomeIds.MaterialRole)
        || roleId.Equals(FacilitySynthesisOutcomeIds.ResultRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(FacilitySynthesisOutcomeIds.OwnerRevisionMetric)
            && unitId.Equals(FacilitySynthesisOutcomeIds.RevisionUnit)
        || metricId.Equals(FacilitySynthesisOutcomeIds.InheritedLevelMetric)
            && unitId.Equals(FacilitySynthesisOutcomeIds.LevelUnit)
        || metricId.Equals(FacilitySynthesisOutcomeIds.MaterialCountMetric)
            && unitId.Equals(FacilitySynthesisOutcomeIds.CountUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        int materials = 0;
        int results = 0;
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayRoleId role = outcome.GetParticipant(index).RoleId;
            if (role.Equals(FacilitySynthesisOutcomeIds.MaterialRole)) materials++;
            if (role.Equals(FacilitySynthesisOutcomeIds.ResultRole)) results++;
        }
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && materials >= 2
            && results == 1
            && outcome.ParticipantCount == materials + 1
            && outcome.SubjectCount == materials
            && outcome.MetricCount == 3
            && outcome.TagCount == 1
            && outcome.ProvenanceCount == 2
            && outcome.FactCount == 4;
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("facility-synthesis-shape-invalid");
    }
}

public static class FacilitySynthesisOutcomeRegistration
{
    public static void RegisterFacilitySynthesisGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register<FacilitySynthesisGameplayOutcomeBridge>(
                Lifetime.Singleton)
            .As<IFacilitySynthesisOutcomeCommitter>();
        builder.Register<FacilitySynthesisOutcomeAdapter>(
                Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<FacilitySynthesisOutcomeDescriptor>(
                Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
