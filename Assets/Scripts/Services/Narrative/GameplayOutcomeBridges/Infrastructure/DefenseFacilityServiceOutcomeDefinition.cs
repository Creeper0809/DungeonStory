using System;
using System.Globalization;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class DefenseFacilityServiceOutcomeIds
{
    public const string ProducerId = "defense.facility-service";
    public static readonly GameplayOutcomeTypeId Type =
        new("defense.facility-service-result");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayRoleId FacilityRole = new("serviced-facility");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayMetricId BeforeSupply =
        new("defense-service.supply-before");
    public static readonly GameplayMetricId AfterSupply =
        new("defense-service.supply-after");
    public static readonly GameplayMetricId Revision =
        new("defense-service.revision");
    public static readonly GameplayOutcomeTagId Tag =
        new("defense-facility-service");
    public static readonly GameplayOutcomeFactId Kind =
        new("defense-service.kind");
    public static readonly GameplayOutcomeFactId Phase =
        new("defense-service.phase");
    public static readonly GameplayOutcomeFactId BeforeState =
        new("defense-service.state-before");
    public static readonly GameplayOutcomeFactId AfterState =
        new("defense-service.state-after");
    public static readonly GameplayOutcomeFactId PhysicalProducer =
        new("defense-service.physical-producer");
    public static readonly GameplayOutcomeFactId PhysicalOperation =
        new("defense-service.physical-operation");
    public static readonly GameplayOutcomeFactId PhysicalRevision =
        new("defense-service.physical-revision");
    public static readonly GameplayOutcomeFactId PhysicalIndex =
        new("defense-service.physical-index");
    public static readonly GameplayOutcomeFactId Source =
        new("defense-service.command-source");
    public static readonly GameplayOutcomeFactId Item =
        new("defense-service.item-id");
    public static readonly GameplayOutcomeFactId Quantity =
        new("defense-service.input-quantity");
    public static readonly GameplayOutcomeFactId Mass =
        new("defense-service.input-mass-grams");
    public static GameplayResultKey ResultKey(string operationId, long revision) =>
        new(ProducerId, new GameplayOperationId(operationId), revision, 0);
}

public readonly struct DefenseFacilityServiceOutcomeReceipt
{
    public DefenseFacilityServiceOutcomeReceipt(
        string facilityId,
        string operationId,
        long revision,
        int absoluteDay,
        DefenseFacilityPhysicalCommitKind kind,
        DefenseFacilityServiceOutcomePhase phase,
        DefenseFacilityOperationalState beforeState,
        DefenseFacilityOperationalState afterState,
        int beforeSupply,
        int afterSupply,
        GameplayResultKey physicalChild,
        string itemId,
        int inputQuantity,
        long inputMassGrams,
        string commandSource = "player-ui",
        GameplayOutcomeId physicalOutcomeId = default)
    {
        if (!GameplayOutcomeStableIdSyntax.IsValid(facilityId)
            || !GameplayOutcomeStableIdSyntax.IsValid(operationId)
            || !GameplayOutcomeStableIdSyntax.IsValid(commandSource)
            || revision <= 0 || absoluteDay < 0
            || kind is not (DefenseFacilityPhysicalCommitKind.SupplyTransfer
                or DefenseFacilityPhysicalCommitKind.MaintenanceSink)
            || !Enum.IsDefined(typeof(DefenseFacilityServiceOutcomePhase), phase)
            || !Enum.IsDefined(typeof(DefenseFacilityOperationalState), beforeState)
            || !Enum.IsDefined(typeof(DefenseFacilityOperationalState), afterState)
            || beforeSupply < 0 || afterSupply < 0
            || phase == DefenseFacilityServiceOutcomePhase.Completed
                && (!physicalChild.IsValid || inputQuantity <= 0
                    || inputMassGrams <= 0 || !GameplayOutcomeStableIdSyntax.IsValid(itemId))
            || phase != DefenseFacilityServiceOutcomePhase.Completed
                && (physicalChild.IsValid || inputQuantity != 0
                    || inputMassGrams != 0 || !string.IsNullOrEmpty(itemId)))
        {
            throw new ArgumentException("Defense service receipt is inconsistent with its applied phase.");
        }
        FacilityId = facilityId;
        OperationId = operationId;
        Revision = revision;
        AbsoluteDay = absoluteDay;
        Kind = kind;
        Phase = phase;
        BeforeState = beforeState;
        AfterState = afterState;
        BeforeSupply = beforeSupply;
        AfterSupply = afterSupply;
        PhysicalChild = physicalChild;
        PhysicalOutcomeId = physicalOutcomeId;
        ItemId = itemId ?? string.Empty;
        InputQuantity = inputQuantity;
        InputMassGrams = inputMassGrams;
        CommandSource = commandSource;
    }

    public string FacilityId { get; }
    public string OperationId { get; }
    public long Revision { get; }
    public int AbsoluteDay { get; }
    public DefenseFacilityPhysicalCommitKind Kind { get; }
    public DefenseFacilityServiceOutcomePhase Phase { get; }
    public DefenseFacilityOperationalState BeforeState { get; }
    public DefenseFacilityOperationalState AfterState { get; }
    public int BeforeSupply { get; }
    public int AfterSupply { get; }
    public GameplayResultKey PhysicalChild { get; }
    public GameplayOutcomeId PhysicalOutcomeId { get; }
    public string ItemId { get; }
    public int InputQuantity { get; }
    public long InputMassGrams { get; }
    public string CommandSource { get; }
    public GameplayResultKey ResultKey =>
        DefenseFacilityServiceOutcomeIds.ResultKey(OperationId, Revision);

    internal DefenseFacilityServiceOutcomeReceipt WithPhysicalOutcomeId(
        GameplayOutcomeId id) => new(
        FacilityId, OperationId, Revision, AbsoluteDay, Kind, Phase,
        BeforeState, AfterState, BeforeSupply, AfterSupply, PhysicalChild,
        ItemId, InputQuantity, InputMassGrams, CommandSource, id);
}

public sealed class DefenseFacilityServiceOutcomeAdapter :
    GameplayOutcomeAdapter<DefenseFacilityServiceOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        DefenseFacilityServiceOutcomeIds.Type;

    public override OutcomePrepareResult TryGetRequirements(
        in DefenseFacilityServiceOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey, OutcomeTypeId, receipt.AbsoluteDay,
            receipt.Phase == DefenseFacilityServiceOutcomePhase.FailedWithImpact
                ? GameplayOutcomeStatus.Failed
                : GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch, receipt.Revision,
            participantCount: 1, metricCount: 3, subjectCount: 1,
            tagCount: 1, anchorCount: 0, provenanceCount: 2,
            factCount: 12);
        return receipt.ResultKey.IsValid
            && (receipt.Phase != DefenseFacilityServiceOutcomePhase.Completed
                || receipt.PhysicalOutcomeId.IsValid)
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "defense-service-parent-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in DefenseFacilityServiceOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId facility = new(
            DefenseFacilityServiceOutcomeIds.FacilityKind,
            receipt.FacilityId);
        const string NameRevision = "defense-service-name-v1";
        KoreanNameSnapshot name = new(
            "방어 시설", NameRevision,
            KoreanPronunciationHint.AutoHangulDisplay(NameRevision), "ko-KR");
        // Requested/failed transitions have no physical child by contract.
        // Canonical facts forbid empty text; this is absence, not a fabricated child identity.
        bool hasPhysicalChild = receipt.Phase == DefenseFacilityServiceOutcomePhase.Completed;
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                facility, DefenseFacilityServiceOutcomeIds.FacilityRole,
                GameplayParticipationKind.Direct, true, name))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility, 0.55f, NarrativeMemoryTier.Recent,
                false, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                DefenseFacilityServiceOutcomeIds.BeforeSupply,
                receipt.BeforeSupply, DefenseFacilityServiceOutcomeIds.CountUnit,
                facility))
            && builder.AddMetric(new GameplayOutcomeMetric(
                DefenseFacilityServiceOutcomeIds.AfterSupply,
                receipt.AfterSupply, DefenseFacilityServiceOutcomeIds.CountUnit,
                facility))
            && builder.AddMetric(new GameplayOutcomeMetric(
                DefenseFacilityServiceOutcomeIds.Revision,
                receipt.Revision,
                DefenseFacilityServiceOutcomeIds.RevisionUnit, facility))
            && builder.AddTag(DefenseFacilityServiceOutcomeIds.Tag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt", "defense-facility-service-v1"))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit", receipt.OperationId))
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.Kind,
                receipt.Kind.ToString())
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.Phase,
                receipt.Phase.ToString())
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.BeforeState,
                receipt.BeforeState.ToString())
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.AfterState,
                receipt.AfterState.ToString())
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.PhysicalProducer,
                hasPhysicalChild ? receipt.PhysicalChild.ProducerId : "none")
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.PhysicalOperation,
                hasPhysicalChild ? receipt.PhysicalChild.OperationId.Value : "none")
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.PhysicalRevision,
                receipt.PhysicalChild.CommitRevision.ToString(CultureInfo.InvariantCulture))
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.PhysicalIndex,
                receipt.PhysicalChild.LocalResultIndex.ToString(CultureInfo.InvariantCulture))
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.Source,
                receipt.CommandSource)
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.Item,
                hasPhysicalChild ? receipt.ItemId : "none")
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.Quantity,
                receipt.InputQuantity.ToString(CultureInfo.InvariantCulture))
            && Fact(ref builder, DefenseFacilityServiceOutcomeIds.Mass,
                receipt.InputMassGrams.ToString(CultureInfo.InvariantCulture))
            && (receipt.Phase != DefenseFacilityServiceOutcomePhase.Completed
                || builder.SetCausation(new GameplayOutcomeCausation(
                    receipt.PhysicalOutcomeId,
                    receipt.PhysicalChild.OperationId,
                    "physical-service-consumption")));
        return written ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "defense-service-write-failed");
    }

    private static bool Fact(
        ref OutcomeWriteBuilder builder, GameplayOutcomeFactId id,
        string value) => builder.AddFact(new GameplayOutcomeFact(id, value));
}

internal sealed class DefenseFacilityServiceMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;
    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome, GameplayEntityId subjectId) =>
        new(outcome.OutcomeTypeId.Value);
    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome, GameplayEntityId subjectId,
        int priorMatchingCount, int evaluationDay) => new(
        0.55f, NarrativeMemoryTier.Recent,
        Math.Max(evaluationDay + 1, outcome.AbsoluteDay + 1));
}

internal sealed class DefenseFacilityServicePerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class DefenseFacilityServiceConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class DefenseFacilityServiceProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective) => new(
        outcome.OutcomeId, perspective.Kind,
        "방어 시설 서비스 상태가 변경되었다.",
        "defense-facility-service-v1", false);
}

public sealed class DefenseFacilityServiceOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new DefenseFacilityServiceProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new DefenseFacilityServiceMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new DefenseFacilityServicePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new DefenseFacilityServiceConsolidator();
    public GameplayOutcomeTypeId OutcomeTypeId => DefenseFacilityServiceOutcomeIds.Type;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(DefenseFacilityServiceOutcomeIds.FacilityRole);
    public bool IsKnownMetric(GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        unitId.Equals(DefenseFacilityServiceOutcomeIds.CountUnit)
            && (metricId.Equals(DefenseFacilityServiceOutcomeIds.BeforeSupply)
                || metricId.Equals(DefenseFacilityServiceOutcomeIds.AfterSupply))
        || unitId.Equals(DefenseFacilityServiceOutcomeIds.RevisionUnit)
            && metricId.Equals(DefenseFacilityServiceOutcomeIds.Revision);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && outcome.ParticipantCount == 1
            && outcome.SubjectCount == 1
            && outcome.MetricCount == 3
            && outcome.TagCount == 1
            && outcome.ProvenanceCount == 2
            && outcome.FactCount == 12
            && outcome.GetParticipant(0).RoleId.Equals(
                DefenseFacilityServiceOutcomeIds.FacilityRole)
            && outcome.GetMetric(2).Value == outcome.OwnerRevision
            && outcome.GetFact(0).FactId.Equals(
                DefenseFacilityServiceOutcomeIds.Kind)
            && outcome.GetFact(1).FactId.Equals(
                DefenseFacilityServiceOutcomeIds.Phase);
        return shape ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "defense-facility-service-shape-invalid");
    }
}

public static class DefenseFacilityServiceOutcomeRegistration
{
    public static void RegisterDefenseFacilityServiceGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register(resolver =>
                DefenseServiceFactMapping.CreateBinding(
                    resolver.Resolve<IGameplayOutcomeRegistry>(),
                    resolver.Resolve<GameplayOutcomeLedger>(),
                    resolver.Resolve<IGameplayOutcomeRecorder>(),
                    resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.RegisterGameplayActionLogs(nameof(DefenseFacilityServiceOutcomeAdapter));
        builder.Register<DefenseServiceFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
    }
}
