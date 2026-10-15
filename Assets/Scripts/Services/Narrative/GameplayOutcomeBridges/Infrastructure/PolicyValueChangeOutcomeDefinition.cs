using System;
using DungeonStory.Narrative.Korean;
using VContainer;

/// <summary>
/// G03's shared value-policy transition contract. The owner supplies stable
/// before/after values and a consecutive owner revision; this adapter records
/// those values without reducing enums or identifiers to boolean flags.
/// </summary>
public static class PolicyValueChangeOutcomeIds
{
    public const string ProducerId = "policy.value-change";
    public static readonly GameplayOutcomeTypeId Changed =
        new("policy.value-changed");
    public static readonly GameplayRoleId SubjectRole = new("changed-subject");
    public static readonly GameplayMetricId BeforeRevision =
        new("policy-value-change.before-revision");
    public static readonly GameplayMetricId AfterRevision =
        new("policy-value-change.after-revision");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId ChangeTag =
        new("policy-value-change");
    public static readonly GameplayOutcomeFactId PolicyIdFact =
        new("policy-value-change.policy-id");
    public static readonly GameplayOutcomeFactId ScopeIdFact =
        new("policy-value-change.scope-id");
    public static readonly GameplayOutcomeFactId BeforeValueFact =
        new("policy-value-change.before-value");
    public static readonly GameplayOutcomeFactId AfterValueFact =
        new("policy-value-change.after-value");
    public static readonly GameplayOutcomeFactId SourceFact =
        new("policy-value-change.source");
    public static readonly GameplayOutcomeFactId OperationFact =
        new("policy-value-change.operation-id");

    public static GameplayResultKey ResultKey(string operationId, long revision) =>
        new(ProducerId, new GameplayOperationId(operationId), revision, 0);
}

/// <summary>
/// Immutable receipt for one applied policy value transition. Values and
/// source are stable IDs so the outcome ledger preserves the exact authored
/// enum/string representation that the owner committed.
/// </summary>
public readonly struct PolicyValueChangeReceipt
{
    public PolicyValueChangeReceipt(
        GameplayEntityId subjectId,
        KoreanNameSnapshot subjectName,
        string policyId,
        string scopeId,
        string beforeValue,
        string afterValue,
        string source,
        string operationId,
        long beforeRevision,
        long afterRevision,
        int absoluteDay,
        GameplayLocationReference location)
    {
        if (!subjectId.IsValid
            || string.IsNullOrWhiteSpace(subjectName.DisplayText)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                subjectName.DisplaySnapshotRevision)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                subjectName.PronunciationHint.Revision)
            || !GameplayOutcomeStableIdSyntax.IsValid(policyId)
            || !GameplayOutcomeStableIdSyntax.IsValid(scopeId)
            || !GameplayOutcomeStableIdSyntax.IsValid(beforeValue)
            || !GameplayOutcomeStableIdSyntax.IsValid(afterValue)
            || string.Equals(beforeValue, afterValue, StringComparison.Ordinal)
            || !GameplayOutcomeStableIdSyntax.IsValid(source)
            || !GameplayOutcomeStableIdSyntax.IsValid(operationId)
            || beforeRevision < 0L
            || beforeRevision == long.MaxValue
            || afterRevision != beforeRevision + 1L
            || absoluteDay < 0)
        {
            throw new ArgumentException(
                "A valid, applied policy value transition receipt is required.");
        }

        SubjectId = subjectId;
        SubjectName = subjectName;
        PolicyId = policyId;
        ScopeId = scopeId;
        BeforeValue = beforeValue;
        AfterValue = afterValue;
        Source = source;
        OperationId = operationId;
        BeforeRevision = beforeRevision;
        AfterRevision = afterRevision;
        AbsoluteDay = absoluteDay;
        Location = location;
    }

    public GameplayEntityId SubjectId { get; }
    public KoreanNameSnapshot SubjectName { get; }
    public string PolicyId { get; }
    public string ScopeId { get; }
    public string BeforeValue { get; }
    public string AfterValue { get; }
    public string Source { get; }
    public string OperationId { get; }
    public long BeforeRevision { get; }
    public long AfterRevision { get; }
    public int AbsoluteDay { get; }
    public GameplayLocationReference Location { get; }

    public GameplayResultKey ResultKey =>
        PolicyValueChangeOutcomeIds.ResultKey(OperationId, AfterRevision);

    public bool IsValid => SubjectId.IsValid
        && !string.IsNullOrWhiteSpace(SubjectName.DisplayText)
        && GameplayOutcomeStableIdSyntax.IsValid(
            SubjectName.DisplaySnapshotRevision)
        && GameplayOutcomeStableIdSyntax.IsValid(
            SubjectName.PronunciationHint.Revision)
        && GameplayOutcomeStableIdSyntax.IsValid(PolicyId)
        && GameplayOutcomeStableIdSyntax.IsValid(ScopeId)
        && GameplayOutcomeStableIdSyntax.IsValid(BeforeValue)
        && GameplayOutcomeStableIdSyntax.IsValid(AfterValue)
        && !string.Equals(BeforeValue, AfterValue, StringComparison.Ordinal)
        && GameplayOutcomeStableIdSyntax.IsValid(Source)
        && GameplayOutcomeStableIdSyntax.IsValid(OperationId)
        && BeforeRevision >= 0L
        && BeforeRevision < long.MaxValue
        && AfterRevision == BeforeRevision + 1L
        && AbsoluteDay >= 0;
}

public sealed class PolicyValueChangeOutcomeAdapter :
    GameplayOutcomeAdapter<PolicyValueChangeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        PolicyValueChangeOutcomeIds.Changed;

    public override OutcomePrepareResult TryGetRequirements(
        in PolicyValueChangeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.IsValid || !receipt.ResultKey.IsValid)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "policy-value-change-receipt-invalid");
        }

        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.AfterRevision,
            participantCount: 1,
            metricCount: 2,
            subjectCount: 1,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: 6);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in PolicyValueChangeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        if (!receipt.IsValid)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "policy-value-change-receipt-invalid");
        }

        GameplayEntityId subject = receipt.SubjectId;
        bool written = builder.AddParticipant(
                new GameplayOutcomeParticipant(
                    subject,
                    PolicyValueChangeOutcomeIds.SubjectRole,
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
                PolicyValueChangeOutcomeIds.BeforeRevision,
                receipt.BeforeRevision,
                PolicyValueChangeOutcomeIds.RevisionUnit,
                subject))
            && builder.AddMetric(new GameplayOutcomeMetric(
                PolicyValueChangeOutcomeIds.AfterRevision,
                receipt.AfterRevision,
                PolicyValueChangeOutcomeIds.RevisionUnit,
                subject))
            && builder.AddTag(PolicyValueChangeOutcomeIds.ChangeTag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "policy-value-change-v1"))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.OperationId))
            && builder.AddFact(new GameplayOutcomeFact(
                PolicyValueChangeOutcomeIds.PolicyIdFact,
                receipt.PolicyId))
            && builder.AddFact(new GameplayOutcomeFact(
                PolicyValueChangeOutcomeIds.ScopeIdFact,
                receipt.ScopeId))
            && builder.AddFact(new GameplayOutcomeFact(
                PolicyValueChangeOutcomeIds.BeforeValueFact,
                receipt.BeforeValue))
            && builder.AddFact(new GameplayOutcomeFact(
                PolicyValueChangeOutcomeIds.AfterValueFact,
                receipt.AfterValue))
            && builder.AddFact(new GameplayOutcomeFact(
                PolicyValueChangeOutcomeIds.SourceFact,
                receipt.Source))
            && builder.AddFact(new GameplayOutcomeFact(
                PolicyValueChangeOutcomeIds.OperationFact,
                receipt.OperationId))
            && builder.SetLocation(receipt.Location);
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "policy-value-change-write-failed");
    }
}

internal sealed class PolicyValueChangeMemoryPolicy : IOutcomeMemoryPolicy
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

internal sealed class PolicyValueChangePerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;

    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class PolicyValueChangeConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;

    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class PolicyValueChangePerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective) => new(
        outcome.OutcomeId,
        perspective.Kind,
        "정책 값이 변경되었다.",
        "policy-value-change-v1",
        false);
}

public sealed class PolicyValueChangeOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new PolicyValueChangePerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new PolicyValueChangeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new PolicyValueChangePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new PolicyValueChangeConsolidator();

    public GameplayOutcomeTypeId OutcomeTypeId =>
        PolicyValueChangeOutcomeIds.Changed;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(PolicyValueChangeOutcomeIds.SubjectRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        unitId.Equals(PolicyValueChangeOutcomeIds.RevisionUnit)
            && (metricId.Equals(PolicyValueChangeOutcomeIds.BeforeRevision)
                || metricId.Equals(PolicyValueChangeOutcomeIds.AfterRevision));

    public OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome)
    {
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && outcome.ParticipantCount == 1
            && outcome.SubjectCount == 1
            && outcome.MetricCount == 2
            && outcome.TagCount == 1
            && outcome.ProvenanceCount == 2
            && outcome.FactCount == 6
            && outcome.GetParticipant(0).RoleId.Equals(
                PolicyValueChangeOutcomeIds.SubjectRole)
            && outcome.GetParticipant(0).EntityId.Equals(
                outcome.GetSubject(0).SubjectId)
            && outcome.GetMetric(0).MetricId.Equals(
                PolicyValueChangeOutcomeIds.BeforeRevision)
            && outcome.GetMetric(0).UnitId.Equals(
                PolicyValueChangeOutcomeIds.RevisionUnit)
            && outcome.GetMetric(1).MetricId.Equals(
                PolicyValueChangeOutcomeIds.AfterRevision)
            && outcome.GetMetric(1).UnitId.Equals(
                PolicyValueChangeOutcomeIds.RevisionUnit)
            && outcome.GetTag(0).Equals(
                PolicyValueChangeOutcomeIds.ChangeTag)
            && outcome.GetFact(0).FactId.Equals(
                PolicyValueChangeOutcomeIds.PolicyIdFact)
            && outcome.GetFact(1).FactId.Equals(
                PolicyValueChangeOutcomeIds.ScopeIdFact)
            && outcome.GetFact(2).FactId.Equals(
                PolicyValueChangeOutcomeIds.BeforeValueFact)
            && outcome.GetFact(3).FactId.Equals(
                PolicyValueChangeOutcomeIds.AfterValueFact)
            && outcome.GetFact(4).FactId.Equals(
                PolicyValueChangeOutcomeIds.SourceFact)
            && outcome.GetFact(5).FactId.Equals(
                PolicyValueChangeOutcomeIds.OperationFact)
            && GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(0).Value)
            && GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(1).Value)
            && GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(2).Value)
            && GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(3).Value)
            && !string.Equals(
                outcome.GetFact(2).Value,
                outcome.GetFact(3).Value,
                StringComparison.Ordinal)
            && GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(4).Value)
            && GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(5).Value)
            && string.Equals(
                outcome.GetFact(5).Value,
                outcome.OperationId.Value,
                StringComparison.Ordinal)
            && IsConsecutiveRevisionPair(
                outcome.GetMetric(0).Value,
                outcome.GetMetric(1).Value)
            && outcome.OwnerRevision > 0L
            && outcome.GetMetric(1).Value == outcome.OwnerRevision;
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "policy-value-change-shape-invalid");
    }

    private static bool IsConsecutiveRevisionPair(
        double beforeRevision,
        double afterRevision) =>
        !double.IsNaN(beforeRevision)
        && !double.IsInfinity(beforeRevision)
        && !double.IsNaN(afterRevision)
        && !double.IsInfinity(afterRevision)
        && beforeRevision >= 0d
        && afterRevision > beforeRevision
        && Math.Truncate(beforeRevision) == beforeRevision
        && Math.Truncate(afterRevision) == afterRevision
        && afterRevision == beforeRevision + 1d;
}

public static class PolicyValueChangeOutcomeRegistration
{
    public static void RegisterPolicyValueChangeGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.RegisterGameplayActionLogs(nameof(PolicyValueChangeOutcomeAdapter));
    }
}
