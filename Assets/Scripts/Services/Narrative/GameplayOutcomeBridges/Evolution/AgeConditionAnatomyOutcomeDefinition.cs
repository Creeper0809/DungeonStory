using System;
using System.Globalization;
using DungeonStory.Narrative.Korean;

public static class AgeConditionAnatomyOutcomeIds
{
    public const string ProducerId = "character.age-condition-anatomy";
    public static readonly GameplayOutcomeTypeId Applied =
        new("character.age-condition-anatomy-applied");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayRoleId PatientRole = new("patient");
    public static readonly GameplayOutcomeTagId AgingTag =
        new("age-condition-anatomy");
    public static readonly GameplayMetricUnitId HealthUnit = new("health");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricId HealthBefore =
        new("age-condition.health-before");
    public static readonly GameplayMetricId HealthAfter =
        new("age-condition.health-after");
    public static readonly GameplayMetricId DamageApplied =
        new("age-condition.damage-applied");
    public static readonly GameplayMetricId NodeCount =
        new("age-condition.node-count");
    public static readonly GameplayOutcomeFactId ConditionId =
        new("age-condition.id");
    public static readonly GameplayOutcomeFactId SeverityBefore =
        new("age-condition.severity-before");
    public static readonly GameplayOutcomeFactId SeverityAfter =
        new("age-condition.severity-after");
    public static readonly GameplayOutcomeFactId NodeDeltas =
        new("age-condition.node-deltas");
    public static readonly GameplayOutcomeFactId ParentResultKey =
        new("age-condition.parent-result-key");

}

public readonly struct AgeConditionAnatomyOutcomeReceipt
{
    public AgeConditionAnatomyOutcomeReceipt(
        string operationId,
        string characterId,
        string characterDisplayName,
        string conditionId,
        AgeConditionSeverity severityBefore,
        AgeConditionSeverity severityAfter,
        int absoluteDay,
        double healthBefore,
        double healthAfter,
        double damageApplied,
        int nodeCount,
        string nodeDeltas,
        GameplayOutcomeId parentOutcomeId,
        GameplayResultKey parentResultKey)
    {
        OperationId = new GameplayOperationId(operationId);
        CharacterId = GameplayOutcomeStableIdSyntax.Require(
            characterId, nameof(characterId));
        CharacterDisplayName = EvolutionOutcomeNames.Snapshot(
            CharacterId, characterDisplayName);
        ConditionId = GameplayOutcomeStableIdSyntax.Require(
            conditionId, nameof(conditionId));
        if (!Enum.IsDefined(typeof(AgeConditionSeverity), severityBefore)
            || !Enum.IsDefined(typeof(AgeConditionSeverity), severityAfter)
            || severityAfter < severityBefore
            || absoluteDay < 0
            || healthBefore < 0d
            || healthAfter < 0d
            || healthAfter > healthBefore
            || damageApplied < 0d
            || Math.Abs((healthBefore - healthAfter) - damageApplied) > 0.001d
            || nodeCount <= 0
            || !GameplayOutcomeLedger.IsValidBoundedUtf16(
                nodeDeltas,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false)
            || !parentOutcomeId.IsValid
            || !parentResultKey.IsValid)
        {
            throw new ArgumentException(
                "A valid age-condition anatomy outcome is required.");
        }
        SeverityBefore = severityBefore;
        SeverityAfter = severityAfter;
        AbsoluteDay = absoluteDay;
        HealthBefore = healthBefore;
        HealthAfter = healthAfter;
        DamageApplied = damageApplied;
        NodeCount = nodeCount;
        NodeDeltas = nodeDeltas.Trim();
        ParentOutcomeId = parentOutcomeId;
        ParentResultKey = parentResultKey;
    }

    public GameplayOperationId OperationId { get; }
    public string CharacterId { get; }
    public KoreanNameSnapshot CharacterDisplayName { get; }
    public string ConditionId { get; }
    public AgeConditionSeverity SeverityBefore { get; }
    public AgeConditionSeverity SeverityAfter { get; }
    public int AbsoluteDay { get; }
    public double HealthBefore { get; }
    public double HealthAfter { get; }
    public double DamageApplied { get; }
    public int NodeCount { get; }
    public string NodeDeltas { get; }
    public GameplayOutcomeId ParentOutcomeId { get; }
    public GameplayResultKey ParentResultKey { get; }
    public GameplayResultKey ResultKey => new(
        AgeConditionAnatomyOutcomeIds.ProducerId,
        OperationId,
        1L,
        0);
}

public sealed class AgeConditionAnatomyOutcomeAdapter :
    GameplayOutcomeAdapter<AgeConditionAnatomyOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        AgeConditionAnatomyOutcomeIds.Applied;

    public override OutcomePrepareResult TryGetRequirements(
        in AgeConditionAnatomyOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            ownerRevision: 1L,
            participantCount: 1,
            metricCount: 4,
            subjectCount: 1,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: 5);
        return receipt.OperationId.IsValid
            && receipt.ParentOutcomeId.IsValid
            && receipt.ParentResultKey.IsValid
            && receipt.NodeCount > 0
                ? OutcomePrepareResult.Prepared()
                : new OutcomePrepareResult(
                    OutcomePrepareCode.InvalidReceipt,
                    "age-condition-anatomy-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in AgeConditionAnatomyOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            AgeConditionAnatomyOutcomeIds.CharacterKind,
            receipt.CharacterId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                character,
                AgeConditionAnatomyOutcomeIds.PatientRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.CharacterDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character,
                0.76f,
                NarrativeMemoryTier.Core,
                true,
                false,
                0))
            && AddMetric(ref builder,
                AgeConditionAnatomyOutcomeIds.HealthBefore,
                receipt.HealthBefore,
                AgeConditionAnatomyOutcomeIds.HealthUnit,
                character)
            && AddMetric(ref builder,
                AgeConditionAnatomyOutcomeIds.HealthAfter,
                receipt.HealthAfter,
                AgeConditionAnatomyOutcomeIds.HealthUnit,
                character)
            && AddMetric(ref builder,
                AgeConditionAnatomyOutcomeIds.DamageApplied,
                receipt.DamageApplied,
                AgeConditionAnatomyOutcomeIds.HealthUnit,
                character)
            && AddMetric(ref builder,
                AgeConditionAnatomyOutcomeIds.NodeCount,
                receipt.NodeCount,
                AgeConditionAnatomyOutcomeIds.CountUnit,
                character)
            && builder.AddTag(AgeConditionAnatomyOutcomeIds.AgingTag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "age-condition-anatomy-v1"))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.OperationId.Value))
            && builder.AddFact(new GameplayOutcomeFact(
                AgeConditionAnatomyOutcomeIds.ConditionId,
                receipt.ConditionId))
            && builder.AddFact(new GameplayOutcomeFact(
                AgeConditionAnatomyOutcomeIds.SeverityBefore,
                receipt.SeverityBefore.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                AgeConditionAnatomyOutcomeIds.SeverityAfter,
                receipt.SeverityAfter.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                AgeConditionAnatomyOutcomeIds.NodeDeltas,
                receipt.NodeDeltas))
            && builder.AddFact(new GameplayOutcomeFact(
                AgeConditionAnatomyOutcomeIds.ParentResultKey,
                receipt.ParentResultKey.ToString()))
            && builder.SetCausation(new GameplayOutcomeCausation(
                receipt.ParentOutcomeId,
                receipt.ParentResultKey.OperationId,
                "age-condition-effect"));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "age-condition-anatomy-write-failed");
    }

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId character) => builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, character));
}

public sealed class AgeConditionAnatomyOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new AgeConditionAnatomyPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new EvolutionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EvolutionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EvolutionOutcomeConsolidator(false);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        AgeConditionAnatomyOutcomeIds.Applied;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(AgeConditionAnatomyOutcomeIds.PatientRole);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        unitId.Equals(AgeConditionAnatomyOutcomeIds.HealthUnit)
            && (metricId.Equals(AgeConditionAnatomyOutcomeIds.HealthBefore)
                || metricId.Equals(AgeConditionAnatomyOutcomeIds.HealthAfter)
                || metricId.Equals(AgeConditionAnatomyOutcomeIds.DamageApplied))
        || unitId.Equals(AgeConditionAnatomyOutcomeIds.CountUnit)
            && metricId.Equals(AgeConditionAnatomyOutcomeIds.NodeCount);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != OutcomeTypeId
            || outcome.ParticipantCount != 1
            || outcome.SubjectCount != 1
            || outcome.MetricCount != 4
            || outcome.TagCount != 1
            || outcome.ProvenanceCount != 2
            || outcome.FactCount != 5
            || outcome.OwnerRevision != 1L
            || !outcome.Causation.HasParent
            || outcome.Causation.RelationId != "age-condition-effect")
        {
            return OutcomeValidationResult.Reject(
                "age-condition-anatomy-shape-invalid");
        }
        GameplayOutcomeParticipant participant = outcome.GetParticipant(0);
        if (!participant.RoleId.Equals(AgeConditionAnatomyOutcomeIds.PatientRole)
            || !participant.EntityId.Kind.Equals(
                AgeConditionAnatomyOutcomeIds.CharacterKind)
            || outcome.GetSubject(0).SubjectId != participant.EntityId
            || !outcome.GetTag(0).Equals(AgeConditionAnatomyOutcomeIds.AgingTag))
        {
            return OutcomeValidationResult.Reject(
                "age-condition-anatomy-owner-invalid");
        }
        GameplayOutcomeMetric before = outcome.GetMetric(0);
        GameplayOutcomeMetric after = outcome.GetMetric(1);
        GameplayOutcomeMetric damage = outcome.GetMetric(2);
        GameplayOutcomeMetric count = outcome.GetMetric(3);
        if (!before.MetricId.Equals(AgeConditionAnatomyOutcomeIds.HealthBefore)
            || !after.MetricId.Equals(AgeConditionAnatomyOutcomeIds.HealthAfter)
            || !damage.MetricId.Equals(AgeConditionAnatomyOutcomeIds.DamageApplied)
            || !count.MetricId.Equals(AgeConditionAnatomyOutcomeIds.NodeCount)
            || before.Value < after.Value
            || Math.Abs((before.Value - after.Value) - damage.Value) > 0.001d
            || count.Value <= 0d
            || count.Value != Math.Truncate(count.Value)
            || !GameplayOutcomeStableIdSyntax.IsValid(outcome.GetFact(0).Value)
            || !Enum.TryParse(outcome.GetFact(1).Value,
                out AgeConditionSeverity severityBefore)
            || !Enum.TryParse(outcome.GetFact(2).Value,
                out AgeConditionSeverity severityAfter)
            || severityAfter < severityBefore
            || string.IsNullOrWhiteSpace(outcome.GetFact(3).Value)
            || string.IsNullOrWhiteSpace(outcome.GetFact(4).Value))
        {
            return OutcomeValidationResult.Reject(
                "age-condition-anatomy-semantics-invalid");
        }
        return OutcomeValidationResult.Accepted;
    }
}

internal sealed class AgeConditionAnatomyPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        string text = outcome.GetParticipant(0).DisplayName.DisplayText
            + "의 「" + outcome.GetFact(0).Value + "」 상태가 "
            + outcome.GetFact(2).Value + " 단계로 진행되어 신체에 영향을 남겼다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "age-condition-anatomy-v1",
            true);
    }
}
