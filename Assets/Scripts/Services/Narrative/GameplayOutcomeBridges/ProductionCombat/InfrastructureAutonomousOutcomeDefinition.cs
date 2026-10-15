using System;
using DungeonStory.Narrative.Korean;

public static class InfrastructureAutonomousOutcomeIds
{
    public const string ProducerId = "infrastructure.autonomous";

    public static readonly GameplayOutcomeTypeId Transitioned =
        new("infrastructure.autonomous-transition");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayRoleId AffectedFacilityRole =
        new("affected-facility");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("infrastructure-autonomous.owner-revision");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId AutonomousTransitionTag =
        new("infrastructure-autonomous-transition");
    public static readonly GameplayOutcomeFactId TransitionKindFact =
        new("infrastructure-autonomous.kind");
    public static readonly GameplayOutcomeFactId TargetIdFact =
        new("infrastructure-autonomous.target-id");
    public static readonly GameplayOutcomeFactId BeforeValueFact =
        new("infrastructure-autonomous.before");
    public static readonly GameplayOutcomeFactId AfterValueFact =
        new("infrastructure-autonomous.after");

    public static GameplayResultKey ResultKey(long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId($"infrastructure-autonomous:{ownerRevision}"),
        ownerRevision,
        0);

    public static string StableKind(InfrastructureCommandOutcomeKind kind) =>
        kind switch
        {
            InfrastructureCommandOutcomeKind.PowerFuelConsumed =>
                "power-fuel-consumed",
            InfrastructureCommandOutcomeKind.PowerBreakerTripped =>
                "power-breaker-tripped",
            InfrastructureCommandOutcomeKind.WastewaterBatchCompleted =>
                "wastewater-batch-completed",
            InfrastructureCommandOutcomeKind.FluidLeakEpisode =>
                "fluid-leak-episode",
            InfrastructureCommandOutcomeKind.WastewaterBackflow =>
                "wastewater-backflow",
            InfrastructureCommandOutcomeKind.PipeFreezeLatched =>
                "pipe-freeze-latched",
            InfrastructureCommandOutcomeKind.PipeFreezeRecovered =>
                "pipe-freeze-recovered",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}

public readonly struct InfrastructureAutonomousOutcomeReceipt
{
    public InfrastructureAutonomousOutcomeReceipt(
        in InfrastructureCommandOutcomeSource source,
        KoreanNameSnapshot facilityDisplayName,
        long ownerRevision,
        int absoluteDay)
    {
        if (!InfrastructureOutcomeKindRules.IsAutonomous(source.Kind))
            throw new ArgumentOutOfRangeException(nameof(source));
        if (!GameplayOutcomeStableIdSyntax.IsValid(source.TargetId)
            || !GameplayOutcomeStableIdSyntax.IsValid(source.FacilityId))
        {
            throw new ArgumentException(
                "Canonical autonomous infrastructure IDs are required.",
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
        InfrastructureAutonomousOutcomeIds.ResultKey(OwnerRevision);
}

public sealed class InfrastructureAutonomousOutcomeAdapter :
    GameplayOutcomeAdapter<InfrastructureAutonomousOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        InfrastructureAutonomousOutcomeIds.Transitioned;

    public override OutcomePrepareResult TryGetRequirements(
        in InfrastructureAutonomousOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid
            || !InfrastructureOutcomeKindRules.IsAutonomous(
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
                "infrastructure-autonomous-receipt-invalid");
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
        in InfrastructureAutonomousOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId facility = new(
            InfrastructureAutonomousOutcomeIds.FacilityKind,
            receipt.Source.FacilityId);
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                facility,
                InfrastructureAutonomousOutcomeIds.AffectedFacilityRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.FacilityDisplayName))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility,
                0.55f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                InfrastructureAutonomousOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                InfrastructureAutonomousOutcomeIds.RevisionUnit,
                facility))
            || !builder.AddTag(
                InfrastructureAutonomousOutcomeIds.AutonomousTransitionTag)
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "infrastructure-autonomous-v1"))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.ResultKey.OperationId.Value))
            || !builder.AddFact(new GameplayOutcomeFact(
                InfrastructureAutonomousOutcomeIds.TransitionKindFact,
                InfrastructureAutonomousOutcomeIds.StableKind(
                    receipt.Source.Kind)))
            || !builder.AddFact(new GameplayOutcomeFact(
                InfrastructureAutonomousOutcomeIds.TargetIdFact,
                receipt.Source.TargetId))
            || !builder.AddFact(new GameplayOutcomeFact(
                InfrastructureAutonomousOutcomeIds.BeforeValueFact,
                receipt.Source.BeforeValue))
            || !builder.AddFact(new GameplayOutcomeFact(
                InfrastructureAutonomousOutcomeIds.AfterValueFact,
                receipt.Source.AfterValue))
            || !builder.SetLocation(new GameplayLocationReference(
                "dungeon",
                string.Empty,
                receipt.Source.FacilityX,
                receipt.Source.FacilityY)))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "infrastructure-autonomous-write-failed");
        }
        return OutcomePrepareResult.Prepared();
    }
}

internal sealed class InfrastructureAutonomousMemoryPolicy :
    IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":" + Fact(
            outcome,
            InfrastructureAutonomousOutcomeIds.TransitionKindFact));

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

internal sealed class InfrastructureAutonomousPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class InfrastructureAutonomousConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class InfrastructureAutonomousPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "infrastructure-autonomous-v1";
    private readonly IKoreanJosaFormatter josa;

    public InfrastructureAutonomousPerspectiveProjector(
        IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant facility = outcome.GetParticipant(0);
        string action = ActionLabel(Fact(
            outcome,
            InfrastructureAutonomousOutcomeIds.TransitionKindFact));
        KoreanJosaFormatResult subject = josa.Format(
            new KoreanJosaRequest(
                facility.DisplayName,
                KoreanJosaKind.Topic));
        bool neutral = subject.RequiresNeutralFrame;
        string text = neutral
            ? $"산업 시설 {facility.DisplayName.DisplayText}: {action}."
            : $"{subject.Text} {action}.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }

    private static string ActionLabel(string kind) => kind switch
    {
        "power-fuel-consumed" => "연료를 소비해 전력을 공급했다",
        "power-breaker-tripped" => "과부하로 차단기가 작동했다",
        "wastewater-batch-completed" => "폐수 처리 한 회분을 완료했다",
        "fluid-leak-episode" => "누수로 물이 손실되고 오염이 생겼다",
        "wastewater-backflow" => "폐수가 역류해 배관과 주변을 오염시켰다",
        "pipe-freeze-latched" => "한파로 배관이 얼었다",
        "pipe-freeze-recovered" => "기온 회복으로 배관이 녹았다",
        _ => "설비 상태가 임계 전이했다"
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

public sealed class InfrastructureAutonomousOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new InfrastructureAutonomousMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new InfrastructureAutonomousPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new InfrastructureAutonomousConsolidator();

    public InfrastructureAutonomousOutcomeDescriptor(
        IKoreanJosaFormatter josa) =>
        projector = new InfrastructureAutonomousPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        InfrastructureAutonomousOutcomeIds.Transitioned;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(
            InfrastructureAutonomousOutcomeIds.AffectedFacilityRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(
            InfrastructureAutonomousOutcomeIds.OwnerRevisionMetric)
        && unitId.Equals(InfrastructureAutonomousOutcomeIds.RevisionUnit);

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
                InfrastructureAutonomousOutcomeIds.AffectedFacilityRole);
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "infrastructure-autonomous-shape-invalid");
    }
}
