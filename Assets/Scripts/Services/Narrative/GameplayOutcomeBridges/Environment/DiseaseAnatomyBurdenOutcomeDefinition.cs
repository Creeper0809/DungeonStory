using System;
using DungeonStory.Narrative.Korean;

public static class DiseaseAnatomyBurdenOutcomeIds
{
    public const string ProducerId = "character.disease-anatomy-burden";
    public static readonly GameplayOutcomeTypeId Applied =
        new("character.disease-anatomy-burden-applied");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayRoleId PatientRole = new("patient");
    public static readonly GameplayOutcomeTagId DiseaseTag =
        new("disease-anatomy-burden");
    public static readonly GameplayMetricUnitId BurdenUnit = new("burden");
    public static readonly GameplayMetricId Severity = new("disease.severity");
    public static readonly GameplayMetricId Requested =
        new("disease.infection-burden-requested");
    public static readonly GameplayMetricId InfectionBefore =
        new("disease.node-infection-before");
    public static readonly GameplayMetricId InfectionAfter =
        new("disease.node-infection-after");
    public static readonly GameplayOutcomeFactId DiseaseId = new("disease.id");
    public static readonly GameplayOutcomeFactId TargetSystem =
        new("disease.target-system");
    public static readonly GameplayOutcomeFactId NodeId =
        new("disease.anatomy-node-id");
}

public readonly struct DiseaseAnatomyBurdenOutcomeReceipt
{
    public DiseaseAnatomyBurdenOutcomeReceipt(
        string operationId,
        string characterId,
        string characterDisplayName,
        string diseaseId,
        DiseaseTargetSystem targetSystem,
        string nodeId,
        float severity,
        float requestedBurden,
        float infectionBefore,
        float infectionAfter,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(operationId);
        CharacterId = GameplayOutcomeStableIdSyntax.Require(
            characterId, nameof(characterId));
        CharacterDisplayName = EvolutionOutcomeNames.Snapshot(
            CharacterId, characterDisplayName);
        DiseaseId = GameplayOutcomeStableIdSyntax.Require(
            diseaseId, nameof(diseaseId));
        NodeId = GameplayOutcomeStableIdSyntax.Require(nodeId, nameof(nodeId));
        if (!Enum.IsDefined(typeof(DiseaseTargetSystem), targetSystem)
            || severity < 0f || requestedBurden <= 0f
            || infectionBefore < 0f || infectionAfter < infectionBefore
            || absoluteDay <= 0)
        {
            throw new ArgumentException(
                "A valid disease anatomy burden outcome is required.");
        }
        TargetSystem = targetSystem;
        Severity = severity;
        RequestedBurden = requestedBurden;
        InfectionBefore = infectionBefore;
        InfectionAfter = infectionAfter;
        AbsoluteDay = absoluteDay;
    }

    public GameplayOperationId OperationId { get; }
    public string CharacterId { get; }
    public KoreanNameSnapshot CharacterDisplayName { get; }
    public string DiseaseId { get; }
    public DiseaseTargetSystem TargetSystem { get; }
    public string NodeId { get; }
    public float Severity { get; }
    public float RequestedBurden { get; }
    public float InfectionBefore { get; }
    public float InfectionAfter { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey => new(
        DiseaseAnatomyBurdenOutcomeIds.ProducerId,
        OperationId,
        1L,
        0);
}

public sealed class DiseaseAnatomyBurdenOutcomeAdapter :
    GameplayOutcomeAdapter<DiseaseAnatomyBurdenOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        DiseaseAnatomyBurdenOutcomeIds.Applied;

    public override OutcomePrepareResult TryGetRequirements(
        in DiseaseAnatomyBurdenOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            1L,
            1,
            4,
            1,
            1,
            0,
            2,
            3);
        return receipt.OperationId.IsValid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "disease-anatomy-burden-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in DiseaseAnatomyBurdenOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            DiseaseAnatomyBurdenOutcomeIds.CharacterKind,
            receipt.CharacterId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                character,
                DiseaseAnatomyBurdenOutcomeIds.PatientRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.CharacterDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character,
                0.58f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && AddMetric(ref builder,
                DiseaseAnatomyBurdenOutcomeIds.Severity,
                receipt.Severity,
                character)
            && AddMetric(ref builder,
                DiseaseAnatomyBurdenOutcomeIds.Requested,
                receipt.RequestedBurden,
                character)
            && AddMetric(ref builder,
                DiseaseAnatomyBurdenOutcomeIds.InfectionBefore,
                receipt.InfectionBefore,
                character)
            && AddMetric(ref builder,
                DiseaseAnatomyBurdenOutcomeIds.InfectionAfter,
                receipt.InfectionAfter,
                character)
            && builder.AddTag(DiseaseAnatomyBurdenOutcomeIds.DiseaseTag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "disease-anatomy-burden-v1"))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.OperationId.Value))
            && builder.AddFact(new GameplayOutcomeFact(
                DiseaseAnatomyBurdenOutcomeIds.DiseaseId,
                receipt.DiseaseId))
            && builder.AddFact(new GameplayOutcomeFact(
                DiseaseAnatomyBurdenOutcomeIds.TargetSystem,
                receipt.TargetSystem.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                DiseaseAnatomyBurdenOutcomeIds.NodeId,
                receipt.NodeId));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "disease-anatomy-burden-write-failed");
    }

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        float value,
        GameplayEntityId character) => builder.AddMetric(
        new GameplayOutcomeMetric(
            id,
            value,
            DiseaseAnatomyBurdenOutcomeIds.BurdenUnit,
            character));
}

public sealed class DiseaseAnatomyBurdenOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new DiseaseAnatomyBurdenPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new EnvironmentOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EnvironmentOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new ExactEnvironmentOutcomeConsolidator();
    public GameplayOutcomeTypeId OutcomeTypeId =>
        DiseaseAnatomyBurdenOutcomeIds.Applied;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(DiseaseAnatomyBurdenOutcomeIds.PatientRole);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        unitId.Equals(DiseaseAnatomyBurdenOutcomeIds.BurdenUnit)
        && (metricId.Equals(DiseaseAnatomyBurdenOutcomeIds.Severity)
            || metricId.Equals(DiseaseAnatomyBurdenOutcomeIds.Requested)
            || metricId.Equals(DiseaseAnatomyBurdenOutcomeIds.InfectionBefore)
            || metricId.Equals(DiseaseAnatomyBurdenOutcomeIds.InfectionAfter));
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != OutcomeTypeId
            || outcome.ParticipantCount != 1
            || outcome.SubjectCount != 1
            || outcome.MetricCount != 4
            || outcome.TagCount != 1
            || outcome.ProvenanceCount != 2
            || outcome.FactCount != 3
            || outcome.OwnerRevision != 1L
            || outcome.Causation.HasParent
            || outcome.GetMetric(3).Value < outcome.GetMetric(2).Value
            || !GameplayOutcomeStableIdSyntax.IsValid(outcome.GetFact(0).Value)
            || !Enum.TryParse(outcome.GetFact(1).Value,
                out DiseaseTargetSystem _)
            || !GameplayOutcomeStableIdSyntax.IsValid(outcome.GetFact(2).Value))
        {
            return OutcomeValidationResult.Reject(
                "disease-anatomy-burden-invalid");
        }
        return OutcomeValidationResult.Accepted;
    }
}

internal sealed class DiseaseAnatomyBurdenPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective) => new(
        outcome.OutcomeId,
        perspective.Kind,
        outcome.GetParticipant(0).DisplayName.DisplayText
            + "의 「" + outcome.GetFact(0).Value + "」 부담이 「"
            + outcome.GetFact(2).Value + "」에 누적되었다.",
        "disease-anatomy-burden-v1",
        true);
}
