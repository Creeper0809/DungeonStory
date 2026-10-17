using System;
using DungeonStory.Narrative.Korean;
using VContainer;

// G03's shared boolean policy/status transition contract. Producers own their
// domain state and durable revision; this adapter records an applied transition.
public static class PolicyStatusChangeOutcomeIds
{
    public const string ProducerId = "policy.status-change";
    public static readonly GameplayOutcomeTypeId Changed =
        new("policy.status-changed");
    public static readonly GameplayRoleId SubjectRole = new("changed-subject");
    public static readonly GameplayMetricId Before = new("policy-status.before");
    public static readonly GameplayMetricId After = new("policy-status.after");
    public static readonly GameplayMetricId Revision = new("policy-status.revision");
    public static readonly GameplayMetricUnitId FlagUnit = new("flag");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId ChangeTag =
        new("policy-status-change");
    public static readonly GameplayOutcomeFactId KeyFact =
        new("policy-status.key");
    public static readonly GameplayOutcomeFactId SourceFact =
        new("policy-status.actor-or-command-source");
    public static readonly GameplayOutcomeFactId OperationFact =
        new("policy-status.operation-id");

    public static GameplayResultKey ResultKey(string operationId, long revision) =>
        new(ProducerId, new GameplayOperationId(operationId), revision, 0);
}

public readonly struct PolicyStatusChangeReceipt
{
    public PolicyStatusChangeReceipt(
        GameplayEntityId subjectId,
        KoreanNameSnapshot subjectName,
        string policyStatusKey,
        string actorOrCommandSource,
        string operationId,
        bool before,
        bool after,
        long revision,
        int absoluteDay,
        GameplayLocationReference location)
    {
        if (!subjectId.IsValid
            || string.IsNullOrWhiteSpace(subjectName.DisplayText)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                subjectName.DisplaySnapshotRevision)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                subjectName.PronunciationHint.Revision)
            || !GameplayOutcomeStableIdSyntax.IsValid(policyStatusKey)
            || !GameplayOutcomeStableIdSyntax.IsValid(actorOrCommandSource)
            || !GameplayOutcomeStableIdSyntax.IsValid(operationId)
            || before == after
            || revision <= 0L
            || absoluteDay < 0)
        {
            throw new ArgumentException(
                "An applied policy/status transition receipt is required.");
        }
        SubjectId = subjectId;
        SubjectName = subjectName;
        PolicyStatusKey = policyStatusKey;
        ActorOrCommandSource = actorOrCommandSource;
        OperationId = operationId;
        Before = before;
        After = after;
        Revision = revision;
        AbsoluteDay = absoluteDay;
        Location = location;
    }

    public GameplayEntityId SubjectId { get; }
    public KoreanNameSnapshot SubjectName { get; }
    public string PolicyStatusKey { get; }
    public string ActorOrCommandSource { get; }
    public string OperationId { get; }
    public bool Before { get; }
    public bool After { get; }
    public long Revision { get; }
    public int AbsoluteDay { get; }
    public GameplayLocationReference Location { get; }
    public GameplayResultKey ResultKey =>
        PolicyStatusChangeOutcomeIds.ResultKey(OperationId, Revision);
}

public sealed class PolicyStatusChangeOutcomeAdapter :
    GameplayOutcomeAdapter<PolicyStatusChangeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        PolicyStatusChangeOutcomeIds.Changed;

    public override OutcomePrepareResult TryGetRequirements(
        in PolicyStatusChangeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid || !receipt.SubjectId.IsValid
            || receipt.Before == receipt.After || receipt.Revision <= 0L)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "policy-status-receipt-invalid");
        }
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.Revision,
            participantCount: 1,
            metricCount: 3,
            subjectCount: 1,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: 3);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in PolicyStatusChangeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId subject = receipt.SubjectId;
        bool written = builder.AddParticipant(
                new GameplayOutcomeParticipant(
                    subject,
                    PolicyStatusChangeOutcomeIds.SubjectRole,
                    GameplayParticipationKind.Direct,
                    true,
                    receipt.SubjectName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                subject,
                0.18f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                PolicyStatusChangeOutcomeIds.Before,
                receipt.Before ? 1d : 0d,
                PolicyStatusChangeOutcomeIds.FlagUnit,
                subject))
            && builder.AddMetric(new GameplayOutcomeMetric(
                PolicyStatusChangeOutcomeIds.After,
                receipt.After ? 1d : 0d,
                PolicyStatusChangeOutcomeIds.FlagUnit,
                subject))
            && builder.AddMetric(new GameplayOutcomeMetric(
                PolicyStatusChangeOutcomeIds.Revision,
                receipt.Revision,
                PolicyStatusChangeOutcomeIds.RevisionUnit,
                subject))
            && builder.AddTag(PolicyStatusChangeOutcomeIds.ChangeTag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "policy-status-change-v1"))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.OperationId))
            && builder.AddFact(new GameplayOutcomeFact(
                PolicyStatusChangeOutcomeIds.KeyFact,
                receipt.PolicyStatusKey))
            && builder.AddFact(new GameplayOutcomeFact(
                PolicyStatusChangeOutcomeIds.SourceFact,
                receipt.ActorOrCommandSource))
            && builder.AddFact(new GameplayOutcomeFact(
                PolicyStatusChangeOutcomeIds.OperationFact,
                receipt.OperationId))
            && builder.SetLocation(receipt.Location);
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "policy-status-write-failed");
    }
}

internal sealed class PolicyStatusChangeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;
    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) =>
        new(outcome.OutcomeTypeId.Value);
    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay) => new(
        0.18f,
        NarrativeMemoryTier.Recent,
        Math.Max(evaluationDay + 1, outcome.AbsoluteDay + 1));
}

internal sealed class PolicyStatusChangePerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class PolicyStatusChangeConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class PolicyStatusChangePerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective) => new(
        outcome.OutcomeId,
        perspective.Kind,
        "정책 또는 상태 설정이 변경되었다.",
        "policy-status-change-v1",
        false);
}

public sealed class PolicyStatusChangeOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new PolicyStatusChangePerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new PolicyStatusChangeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new PolicyStatusChangePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new PolicyStatusChangeConsolidator();

    public GameplayOutcomeTypeId OutcomeTypeId =>
        PolicyStatusChangeOutcomeIds.Changed;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(PolicyStatusChangeOutcomeIds.SubjectRole);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        unitId.Equals(PolicyStatusChangeOutcomeIds.FlagUnit)
            && (metricId.Equals(PolicyStatusChangeOutcomeIds.Before)
                || metricId.Equals(PolicyStatusChangeOutcomeIds.After))
        || unitId.Equals(PolicyStatusChangeOutcomeIds.RevisionUnit)
            && metricId.Equals(PolicyStatusChangeOutcomeIds.Revision);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && outcome.ParticipantCount == 1
            && outcome.SubjectCount == 1
            && outcome.MetricCount == 3
            && outcome.TagCount == 1
            && outcome.ProvenanceCount == 2
            && outcome.FactCount == 3
            && outcome.GetParticipant(0).RoleId.Equals(
                PolicyStatusChangeOutcomeIds.SubjectRole)
            && outcome.GetParticipant(0).EntityId.Equals(
                outcome.GetSubject(0).SubjectId)
            && outcome.GetMetric(0).MetricId.Equals(
                PolicyStatusChangeOutcomeIds.Before)
            && outcome.GetMetric(0).UnitId.Equals(
                PolicyStatusChangeOutcomeIds.FlagUnit)
            && outcome.GetMetric(1).MetricId.Equals(
                PolicyStatusChangeOutcomeIds.After)
            && outcome.GetMetric(1).UnitId.Equals(
                PolicyStatusChangeOutcomeIds.FlagUnit)
            && outcome.GetMetric(2).MetricId.Equals(
                PolicyStatusChangeOutcomeIds.Revision)
            && outcome.GetMetric(2).UnitId.Equals(
                PolicyStatusChangeOutcomeIds.RevisionUnit)
            && outcome.GetFact(0).FactId.Equals(
                PolicyStatusChangeOutcomeIds.KeyFact)
            && outcome.GetFact(1).FactId.Equals(
                PolicyStatusChangeOutcomeIds.SourceFact)
            && outcome.GetFact(2).FactId.Equals(
                PolicyStatusChangeOutcomeIds.OperationFact)
            && GameplayOutcomeStableIdSyntax.IsValid(outcome.GetFact(0).Value)
            && GameplayOutcomeStableIdSyntax.IsValid(outcome.GetFact(1).Value)
            && string.Equals(
                outcome.GetFact(2).Value,
                outcome.OperationId.Value,
                StringComparison.Ordinal)
            && (outcome.GetMetric(0).Value == 0d
                || outcome.GetMetric(0).Value == 1d)
            && (outcome.GetMetric(1).Value == 0d
                || outcome.GetMetric(1).Value == 1d)
            && outcome.GetMetric(0).Value != outcome.GetMetric(1).Value
            && outcome.OwnerRevision > 0L
            && outcome.GetMetric(2).Value == outcome.OwnerRevision;
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "policy-status-shape-invalid");
    }
}

public static class PolicyStatusChangeOutcomeRegistration
{
    public static void RegisterPolicyStatusChangeGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.RegisterGameplayActionLogs(nameof(PolicyStatusChangeOutcomeAdapter));
    }
}
