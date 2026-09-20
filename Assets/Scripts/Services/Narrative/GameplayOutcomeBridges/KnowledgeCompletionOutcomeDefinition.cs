using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Narrative.Korean;
using VContainer;

/// <summary>
/// Immutable completion receipt for one memory-residue research task. The
/// physical sink receipt and its owner revision remain the transaction
/// authority; this value only carries their exact committed evidence into the
/// gameplay-outcome ledger.
/// </summary>
public readonly struct KnowledgeCompletionOutcomeReceipt
{
    public KnowledgeCompletionOutcomeReceipt(
        GameplayResultKey resultKey,
        int absoluteDay,
        string taskId,
        string taskLabel,
        string facilityId,
        string facilityName,
        KnowledgeResidueUse use,
        string regionId,
        string regionName,
        string codexClue,
        float rewardBefore,
        float rewardAfter,
        in PhysicalItemBatchDispositionReceipt input)
    {
        ResultKey = resultKey;
        AbsoluteDay = absoluteDay;
        TaskId = taskId ?? string.Empty;
        TaskLabel = taskLabel ?? string.Empty;
        FacilityId = facilityId ?? string.Empty;
        FacilityName = facilityName ?? string.Empty;
        Use = use;
        RegionId = regionId ?? string.Empty;
        RegionName = regionName ?? string.Empty;
        CodexClue = codexClue ?? string.Empty;
        RewardBefore = rewardBefore;
        RewardAfter = rewardAfter;
        Input = input;

        if (!KnowledgeCompletionOutcomeValidation.TryValidate(this, out string detail))
            throw new ArgumentException(detail, nameof(resultKey));
    }

    public GameplayResultKey ResultKey { get; }
    public int AbsoluteDay { get; }
    public string TaskId { get; }
    public string TaskLabel { get; }
    public string FacilityId { get; }
    public string FacilityName { get; }
    public KnowledgeResidueUse Use { get; }
    public string RegionId { get; }
    public string RegionName { get; }
    public string CodexClue { get; }
    public float RewardBefore { get; }
    public float RewardAfter { get; }
    public PhysicalItemBatchDispositionReceipt Input { get; }
}

public static class KnowledgeCompletionOutcomeIds
{
    public const string ProducerId = "research.knowledge-completed";
    public const string CodexMemoryResidueEntryId = "memory-residue";
    public const string CodexMemoryResidueName = "기억 잔재";

    public static readonly GameplayOutcomeTypeId Completed =
        new("research.knowledge-completed");
    public static readonly GameplayEntityKindId TaskKind = new("knowledge-task");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayEntityKindId RegionKind = new("region");
    public static readonly GameplayEntityKindId CodexEntryKind = new("codex-entry");
    public static readonly GameplayEntityKindId ItemInstanceKind = new("item-instance");
    public static readonly GameplayEntityKindId ItemStackKind = new("item-stack");

    public static readonly GameplayRoleId TaskRole = new("knowledge-task");
    public static readonly GameplayRoleId FacilityRole = new("research-facility");
    public static readonly GameplayRoleId RewardTargetRole =
        new("knowledge-reward-target");
    public static readonly GameplayRoleId ConsumedMaterialRole =
        new("consumed-material");

    public static readonly GameplayMetricId RewardBeforeMetric =
        new("knowledge.reward-before");
    public static readonly GameplayMetricId RewardAfterMetric =
        new("knowledge.reward-after");
    public static readonly GameplayMetricId RewardDeltaMetric =
        new("knowledge.reward-delta");
    public static readonly GameplayMetricId InputQuantityMetric =
        new("physical.input-quantity");
    public static readonly GameplayMetricId InputMassMetric =
        new("physical.input-mass-grams");
    public static readonly GameplayMetricId SourceXMetric =
        new("physical.source-x");
    public static readonly GameplayMetricId SourceYMetric =
        new("physical.source-y");

    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId PercentUnit = new("percent");
    public static readonly GameplayMetricUnitId GramUnit = new("gram");
    public static readonly GameplayMetricUnitId CellUnit = new("cell");

    public static readonly GameplayOutcomeTagId ResearchTag = new("research");
    public static readonly GameplayOutcomeTagId KnowledgeResidueTag =
        new("knowledge-residue");

    public static readonly GameplayOutcomeFactId UseFact = new("knowledge.use");
    public static readonly GameplayOutcomeFactId CodexClueFact =
        new("knowledge.codex-clue");
    public static readonly GameplayOutcomeFactId InputOperationFact =
        new("physical.input-operation");
    public static readonly GameplayOutcomeFactId InputCommitFact =
        new("physical.input-commit");
    public static readonly GameplayOutcomeFactId InputFingerprintFact =
        new("physical.input-request-fingerprint");
    public static readonly GameplayOutcomeFactId InputReasonFact =
        new("physical.input-reason-code");
    public static readonly GameplayOutcomeFactId SourceStackFact =
        new("physical.source-stack-id");
    public static readonly GameplayOutcomeFactId SourceDefinitionFact =
        new("physical.source-definition-id");
    public static readonly GameplayOutcomeFactId SourceInstanceFact =
        new("physical.source-instance-id");
}

public static class KnowledgeCompletionOutcomeRegistration
{
    public static void RegisterKnowledgeCompletionGameplayOutcomes(
        this IContainerBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        builder.Register<KnowledgeCompletionOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<KnowledgeCompletionOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}

public sealed class KnowledgeCompletionOutcomeAdapter :
    GameplayOutcomeAdapter<KnowledgeCompletionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        KnowledgeCompletionOutcomeIds.Completed;

    public override OutcomePrepareResult TryGetRequirements(
        in KnowledgeCompletionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!KnowledgeCompletionOutcomeValidation.TryValidate(receipt, out string detail))
            return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, detail);

        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.Input.OwnerRevision,
            participantCount: 4,
            metricCount: 7,
            subjectCount: 4,
            tagCount: 2,
            anchorCount: 0,
            provenanceCount: 0,
            factCount: 7
                + (receipt.Input.SourceFacts[0].ItemInstanceId.Length > 0 ? 1 : 0)
                + (receipt.Use == KnowledgeResidueUse.CodexAnalysis ? 1 : 0));
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in KnowledgeCompletionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        if (!KnowledgeCompletionOutcomeValidation.TryValidate(receipt, out string detail))
            return Failed(detail);

        PhysicalItemDispositionSourceFact source = receipt.Input.SourceFacts[0];
        GameplayEntityId task = new(KnowledgeCompletionOutcomeIds.TaskKind, receipt.TaskId);
        GameplayEntityId facility = new(
            KnowledgeCompletionOutcomeIds.FacilityKind,
            receipt.FacilityId);
        GameplayEntityId rewardTarget = receipt.Use == KnowledgeResidueUse.CodexAnalysis
            ? new GameplayEntityId(
                KnowledgeCompletionOutcomeIds.CodexEntryKind,
                KnowledgeCompletionOutcomeIds.CodexMemoryResidueEntryId)
            : new GameplayEntityId(KnowledgeCompletionOutcomeIds.RegionKind, receipt.RegionId);
        GameplayEntityId material = source.ItemInstanceId.Length > 0
            ? new GameplayEntityId(
                KnowledgeCompletionOutcomeIds.ItemInstanceKind,
                source.ItemInstanceId)
            : new GameplayEntityId(
                KnowledgeCompletionOutcomeIds.ItemStackKind,
                source.StackId);
        KoreanNameSnapshot rewardTargetName = receipt.Use == KnowledgeResidueUse.CodexAnalysis
            ? ResearchWorkOutcomeNames.Snapshot(
                KnowledgeCompletionOutcomeIds.CodexMemoryResidueEntryId,
                KnowledgeCompletionOutcomeIds.CodexMemoryResidueName)
            : ResearchWorkOutcomeNames.Snapshot(receipt.RegionId, receipt.RegionName);
        GameplayMetricUnitId rewardUnit = receipt.Use == KnowledgeResidueUse.CodexAnalysis
            ? KnowledgeCompletionOutcomeIds.CountUnit
            : KnowledgeCompletionOutcomeIds.PercentUnit;

        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                task,
                KnowledgeCompletionOutcomeIds.TaskRole,
                GameplayParticipationKind.Direct,
                true,
                ResearchWorkOutcomeNames.Snapshot(receipt.TaskId, receipt.TaskLabel)))
            || !builder.AddParticipant(new GameplayOutcomeParticipant(
                facility,
                KnowledgeCompletionOutcomeIds.FacilityRole,
                GameplayParticipationKind.Direct,
                true,
                ResearchWorkOutcomeNames.Snapshot(
                    receipt.FacilityId,
                    receipt.FacilityName)))
            || !builder.AddParticipant(new GameplayOutcomeParticipant(
                rewardTarget,
                KnowledgeCompletionOutcomeIds.RewardTargetRole,
                GameplayParticipationKind.Direct,
                true,
                rewardTargetName))
            || !builder.AddParticipant(new GameplayOutcomeParticipant(
                material,
                KnowledgeCompletionOutcomeIds.ConsumedMaterialRole,
                GameplayParticipationKind.Direct,
                true,
                source.DisplayName))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                KnowledgeCompletionOutcomeIds.RewardBeforeMetric,
                receipt.RewardBefore,
                rewardUnit,
                rewardTarget))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                KnowledgeCompletionOutcomeIds.RewardAfterMetric,
                receipt.RewardAfter,
                rewardUnit,
                rewardTarget))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                KnowledgeCompletionOutcomeIds.RewardDeltaMetric,
                receipt.RewardAfter - receipt.RewardBefore,
                rewardUnit,
                rewardTarget))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                KnowledgeCompletionOutcomeIds.InputQuantityMetric,
                receipt.Input.Quantity,
                KnowledgeCompletionOutcomeIds.CountUnit,
                material))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                KnowledgeCompletionOutcomeIds.InputMassMetric,
                receipt.Input.InputMassGrams,
                KnowledgeCompletionOutcomeIds.GramUnit,
                material))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                KnowledgeCompletionOutcomeIds.SourceXMetric,
                source.SourcePosition.x,
                KnowledgeCompletionOutcomeIds.CellUnit,
                material))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                KnowledgeCompletionOutcomeIds.SourceYMetric,
                source.SourcePosition.y,
                KnowledgeCompletionOutcomeIds.CellUnit,
                material))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                task, 1f, NarrativeMemoryTier.Core, true, false, 0))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility, .8f, NarrativeMemoryTier.Core, true, false, 0))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                rewardTarget, 1f, NarrativeMemoryTier.Core, true, false, 0))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                material, .5f, NarrativeMemoryTier.Core, true, false, 0))
            || !builder.AddTag(KnowledgeCompletionOutcomeIds.ResearchTag)
            || !builder.AddTag(KnowledgeCompletionOutcomeIds.KnowledgeResidueTag)
            || !builder.AddFact(new GameplayOutcomeFact(
                KnowledgeCompletionOutcomeIds.UseFact,
                KnowledgeCompletionOutcomeFacts.UseValue(receipt.Use)))
            || !builder.AddFact(new GameplayOutcomeFact(
                KnowledgeCompletionOutcomeIds.InputOperationFact,
                receipt.Input.OperationId))
            || !builder.AddFact(new GameplayOutcomeFact(
                KnowledgeCompletionOutcomeIds.InputCommitFact,
                receipt.Input.CommitId))
            || !builder.AddFact(new GameplayOutcomeFact(
                KnowledgeCompletionOutcomeIds.InputFingerprintFact,
                receipt.Input.RequestFingerprint))
            || !builder.AddFact(new GameplayOutcomeFact(
                KnowledgeCompletionOutcomeIds.InputReasonFact,
                receipt.Input.ReasonCode))
            || !builder.AddFact(new GameplayOutcomeFact(
                KnowledgeCompletionOutcomeIds.SourceStackFact,
                source.StackId))
            || !builder.AddFact(new GameplayOutcomeFact(
                KnowledgeCompletionOutcomeIds.SourceDefinitionFact,
                source.ItemDefinitionId)))
        {
            return Failed("knowledge-completion-write-failed");
        }

        if (source.ItemInstanceId.Length > 0
            && !builder.AddFact(new GameplayOutcomeFact(
                KnowledgeCompletionOutcomeIds.SourceInstanceFact,
                source.ItemInstanceId)))
        {
            return Failed("knowledge-completion-write-failed");
        }
        if (receipt.Use == KnowledgeResidueUse.CodexAnalysis
            && !builder.AddFact(new GameplayOutcomeFact(
                KnowledgeCompletionOutcomeIds.CodexClueFact,
                receipt.CodexClue)))
        {
            return Failed("knowledge-completion-write-failed");
        }

        return OutcomePrepareResult.Prepared();
    }

    private static OutcomePrepareResult Failed(string detail) =>
        new(OutcomePrepareCode.AdapterWriteFailed, detail);
}

public sealed class KnowledgeCompletionOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private static readonly IOutcomeMemoryPolicy Memory =
        new KnowledgeCompletionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new KnowledgeCompletionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new KnowledgeCompletionOutcomeConsolidator();
    private readonly KnowledgeCompletionOutcomePerspectiveProjector projector;

    public KnowledgeCompletionOutcomeDescriptor()
        : this(new KoreanJosaFormatter())
    {
    }

    [Inject]
    public KnowledgeCompletionOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new KnowledgeCompletionOutcomePerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId => KnowledgeCompletionOutcomeIds.Completed;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(KnowledgeCompletionOutcomeIds.TaskRole)
        || roleId.Equals(KnowledgeCompletionOutcomeIds.FacilityRole)
        || roleId.Equals(KnowledgeCompletionOutcomeIds.RewardTargetRole)
        || roleId.Equals(KnowledgeCompletionOutcomeIds.ConsumedMaterialRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(KnowledgeCompletionOutcomeIds.RewardBeforeMetric)
        || metricId.Equals(KnowledgeCompletionOutcomeIds.RewardAfterMetric)
        || metricId.Equals(KnowledgeCompletionOutcomeIds.RewardDeltaMetric)
            ? unitId.Equals(KnowledgeCompletionOutcomeIds.CountUnit)
                || unitId.Equals(KnowledgeCompletionOutcomeIds.PercentUnit)
            : metricId.Equals(KnowledgeCompletionOutcomeIds.InputQuantityMetric)
                ? unitId.Equals(KnowledgeCompletionOutcomeIds.CountUnit)
                : metricId.Equals(KnowledgeCompletionOutcomeIds.InputMassMetric)
                    ? unitId.Equals(KnowledgeCompletionOutcomeIds.GramUnit)
                    : (metricId.Equals(KnowledgeCompletionOutcomeIds.SourceXMetric)
                        || metricId.Equals(KnowledgeCompletionOutcomeIds.SourceYMetric))
                        && unitId.Equals(KnowledgeCompletionOutcomeIds.CellUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        KnowledgeCompletionOutcomeValidation.TryValidateReadView(outcome, out string detail)
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(detail);
}

public static class KnowledgeCompletionOutcomeValidation
{
    private const double Epsilon = .00001d;

    internal static bool TryValidate(
        in KnowledgeCompletionOutcomeReceipt receipt,
        out string detail)
    {
        detail = "knowledge-completion-receipt-invalid";
        PhysicalItemBatchDispositionReceipt input = receipt.Input;
        if (!IsCanonicalResultKey(receipt.ResultKey, input)
            || receipt.AbsoluteDay < 0
            || !IsIdentity(receipt.TaskId, receipt.TaskLabel)
            || !IsIdentity(receipt.FacilityId, receipt.FacilityName)
            || !Enum.IsDefined(typeof(KnowledgeResidueUse), receipt.Use)
            || !TryValidateReward(receipt)
            || !TryValidateInput(input))
        {
            return false;
        }

        detail = string.Empty;
        return true;
    }

    public static bool TryValidateReadView(
        in GameplayOutcomeReadView outcome,
        out string detail)
    {
        detail = "knowledge-completion-shape-invalid";
        if (outcome.OutcomeTypeId != KnowledgeCompletionOutcomeIds.Completed
            || outcome.Status != GameplayOutcomeStatus.Succeeded
            || outcome.AbsoluteDay < 0
            || !IsCanonicalResultKey(outcome.ResultKey)
            || outcome.OwnerRevision != outcome.ResultKey.CommitRevision
            || outcome.ParticipantCount != 4
            || outcome.MetricCount != 7
            || outcome.SubjectCount != 4
            || outcome.TagCount != 2
            || outcome.AnchorCount != 0
            || outcome.ProvenanceCount != 0
            || !outcome.GetTag(0).Equals(KnowledgeCompletionOutcomeIds.ResearchTag)
            || !outcome.GetTag(1).Equals(KnowledgeCompletionOutcomeIds.KnowledgeResidueTag)
            || !KnowledgeCompletionOutcomeFacts.TryReadUse(outcome, out KnowledgeResidueUse use)
            || !TryValidateParticipants(outcome, use, out GameplayOutcomeParticipant task,
                out GameplayOutcomeParticipant facility,
                out GameplayOutcomeParticipant target,
                out GameplayOutcomeParticipant material)
            || outcome.FactCount != 7
                + (material.EntityId.Kind.Equals(
                    KnowledgeCompletionOutcomeIds.ItemInstanceKind) ? 1 : 0)
                + (use == KnowledgeResidueUse.CodexAnalysis ? 1 : 0)
            || !TryValidateSubjects(outcome, task.EntityId, facility.EntityId, target.EntityId, material.EntityId)
            || !TryValidateMetrics(outcome, use, target.EntityId, material.EntityId,
                out double before, out double after, out double mass)
            || !TryValidateFacts(outcome, use, target.EntityId, material.EntityId, mass)
            || !TryValidateRewardValues(use, before, after))
        {
            return false;
        }

        detail = string.Empty;
        return true;
    }

    internal static bool IsCanonicalResultKey(
        GameplayResultKey resultKey) => resultKey.IsValid
        && string.Equals(resultKey.ProducerId, KnowledgeCompletionOutcomeIds.ProducerId,
            StringComparison.Ordinal)
        && resultKey.CommitRevision > 0L
        && resultKey.LocalResultIndex == 0;

    private static bool IsCanonicalResultKey(
        GameplayResultKey resultKey,
        in PhysicalItemBatchDispositionReceipt input) =>
        IsCanonicalResultKey(resultKey)
        && input.OwnerRevision > 0L
        && resultKey.CommitRevision == input.OwnerRevision
        && string.Equals(resultKey.OperationId.Value, input.OperationId,
            StringComparison.Ordinal);

    private static bool TryValidateReward(in KnowledgeCompletionOutcomeReceipt receipt)
    {
        if (!float.IsFinite(receipt.RewardBefore)
            || !float.IsFinite(receipt.RewardAfter))
        {
            return false;
        }

        if (receipt.Use == KnowledgeResidueUse.CodexAnalysis)
        {
            return receipt.RegionId.Length == 0
                && receipt.RegionName.Length == 0
                && IsBoundedText(receipt.CodexClue, allowEmpty: false)
                && IsExactNonNegativeInteger(receipt.RewardBefore)
                && IsExactNonNegativeInteger(receipt.RewardAfter)
                && Math.Abs(receipt.RewardAfter - receipt.RewardBefore - 1f) < Epsilon;
        }

        if (receipt.Use != KnowledgeResidueUse.RegionReconnaissance
            || !IsIdentity(receipt.RegionId, receipt.RegionName)
            || receipt.CodexClue.Length != 0
            || receipt.RewardBefore < 0f
            || receipt.RewardBefore >= 100f
            || receipt.RewardAfter <= receipt.RewardBefore
            || receipt.RewardAfter > 100f)
        {
            return false;
        }

        float expectedAfter = Math.Min(100f, receipt.RewardBefore + 10f);
        return Math.Abs(receipt.RewardAfter - expectedAfter) < Epsilon;
    }

    private static bool TryValidateInput(in PhysicalItemBatchDispositionReceipt input)
    {
        if (!input.IsCommitted
            || input.Kind != PhysicalItemDispositionKind.Sink
            || input.OwnerRevision <= 0L
            || input.Quantity != 1
            || input.InputMassGrams <= 0L
            || input.SourceFacts == null
            || input.SourceFacts.Count != 1
            || input.SourceStackIds == null
            || input.SourceStackIds.Count != 1
            || !GameplayOutcomeStableIdSyntax.IsValid(input.OperationId)
            || !GameplayOutcomeStableIdSyntax.IsValid(input.ReasonCode)
            || !IsBoundedText(input.RequestFingerprint, allowEmpty: false)
            || !IsBoundedText(input.CommitId, allowEmpty: false))
        {
            return false;
        }

        PhysicalItemDispositionSourceFact source = input.SourceFacts[0];
        return source.IsValid
            && source.Quantity == 1
            && source.MassGrams == input.InputMassGrams
            && string.Equals(
                source.ItemDefinitionId,
                KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
                StringComparison.Ordinal)
            && string.Equals(input.SourceStackIds[0], source.StackId,
                StringComparison.Ordinal);
    }

    private static bool TryValidateParticipants(
        in GameplayOutcomeReadView outcome,
        KnowledgeResidueUse use,
        out GameplayOutcomeParticipant task,
        out GameplayOutcomeParticipant facility,
        out GameplayOutcomeParticipant target,
        out GameplayOutcomeParticipant material)
    {
        task = facility = target = material = default;
        bool found = KnowledgeCompletionOutcomeFacts.TryGetParticipant(
            outcome, KnowledgeCompletionOutcomeIds.TaskRole, out task)
            && KnowledgeCompletionOutcomeFacts.TryGetParticipant(
                outcome, KnowledgeCompletionOutcomeIds.FacilityRole, out facility)
            && KnowledgeCompletionOutcomeFacts.TryGetParticipant(
                outcome, KnowledgeCompletionOutcomeIds.RewardTargetRole, out target)
            && KnowledgeCompletionOutcomeFacts.TryGetParticipant(
                outcome, KnowledgeCompletionOutcomeIds.ConsumedMaterialRole, out material);
        if (!found
            || !IsDirect(task, KnowledgeCompletionOutcomeIds.TaskKind)
            || !IsDirect(facility, KnowledgeCompletionOutcomeIds.FacilityKind)
            || !(IsDirect(material, KnowledgeCompletionOutcomeIds.ItemInstanceKind)
                || IsDirect(material, KnowledgeCompletionOutcomeIds.ItemStackKind))
            || !IsDirect(target, use == KnowledgeResidueUse.CodexAnalysis
                ? KnowledgeCompletionOutcomeIds.CodexEntryKind
                : KnowledgeCompletionOutcomeIds.RegionKind))
        {
            return false;
        }

        return use != KnowledgeResidueUse.CodexAnalysis
            || string.Equals(target.EntityId.Value,
                KnowledgeCompletionOutcomeIds.CodexMemoryResidueEntryId,
                StringComparison.Ordinal)
                && string.Equals(target.DisplayName.DisplayText,
                    KnowledgeCompletionOutcomeIds.CodexMemoryResidueName,
                    StringComparison.Ordinal);
    }

    private static bool TryValidateSubjects(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId task,
        GameplayEntityId facility,
        GameplayEntityId target, GameplayEntityId material)
    {
        HashSet<GameplayEntityId> expected = new() { task, facility, target, material };
        if (expected.Count != 4)
            return false;
        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink subject = outcome.GetSubject(index);
            if (!expected.Remove(subject.SubjectId)
                || subject.Tier != NarrativeMemoryTier.Core
                || !subject.IsPinned
                || subject.IsOptionalWitness)
            {
                return false;
            }
        }
        return expected.Count == 0;
    }

    private static bool TryValidateMetrics(
        in GameplayOutcomeReadView outcome,
        KnowledgeResidueUse use,
        GameplayEntityId target,
        GameplayEntityId material,
        out double before,
        out double after,
        out double mass)
    {
        before = after = mass = 0;
        GameplayMetricUnitId rewardUnit = use == KnowledgeResidueUse.CodexAnalysis
            ? KnowledgeCompletionOutcomeIds.CountUnit
            : KnowledgeCompletionOutcomeIds.PercentUnit;
        return KnowledgeCompletionOutcomeFacts.TryGetMetric(
                outcome, KnowledgeCompletionOutcomeIds.RewardBeforeMetric,
                rewardUnit, target, out before)
            && KnowledgeCompletionOutcomeFacts.TryGetMetric(
                outcome, KnowledgeCompletionOutcomeIds.RewardAfterMetric,
                rewardUnit, target, out after)
            && KnowledgeCompletionOutcomeFacts.TryGetMetric(
                outcome, KnowledgeCompletionOutcomeIds.RewardDeltaMetric,
                rewardUnit, target, out double delta)
            && Math.Abs(delta - (after - before)) < Epsilon
            && KnowledgeCompletionOutcomeFacts.TryGetMetric(
                outcome, KnowledgeCompletionOutcomeIds.InputQuantityMetric,
                KnowledgeCompletionOutcomeIds.CountUnit, material, out double quantity)
            && quantity == 1d
            && KnowledgeCompletionOutcomeFacts.TryGetMetric(
                outcome, KnowledgeCompletionOutcomeIds.InputMassMetric,
                KnowledgeCompletionOutcomeIds.GramUnit, material, out mass)
            && mass > 0d
            && IsExactNonNegativeInteger(mass)
            && KnowledgeCompletionOutcomeFacts.TryGetMetric(
                outcome, KnowledgeCompletionOutcomeIds.SourceXMetric,
                KnowledgeCompletionOutcomeIds.CellUnit, material, out _)
            && KnowledgeCompletionOutcomeFacts.TryGetMetric(
                outcome, KnowledgeCompletionOutcomeIds.SourceYMetric,
                KnowledgeCompletionOutcomeIds.CellUnit, material, out _);
    }

    private static bool TryValidateFacts(
        in GameplayOutcomeReadView outcome,
        KnowledgeResidueUse use,
        GameplayEntityId target,
        GameplayEntityId material,
        double mass)
    {
        if (!KnowledgeCompletionOutcomeFacts.TryGetFact(outcome,
                KnowledgeCompletionOutcomeIds.InputOperationFact, out string operation)
            || !KnowledgeCompletionOutcomeFacts.TryGetFact(outcome,
                KnowledgeCompletionOutcomeIds.InputCommitFact, out string commit)
            || !KnowledgeCompletionOutcomeFacts.TryGetFact(outcome,
                KnowledgeCompletionOutcomeIds.InputFingerprintFact, out string fingerprint)
            || !KnowledgeCompletionOutcomeFacts.TryGetFact(outcome,
                KnowledgeCompletionOutcomeIds.InputReasonFact, out string reason)
            || !KnowledgeCompletionOutcomeFacts.TryGetFact(outcome,
                KnowledgeCompletionOutcomeIds.SourceStackFact, out string stack)
            || !KnowledgeCompletionOutcomeFacts.TryGetFact(outcome,
                KnowledgeCompletionOutcomeIds.SourceDefinitionFact, out string definition)
            || !GameplayOutcomeStableIdSyntax.IsValid(reason)
            || !GameplayOutcomeStableIdSyntax.IsValid(stack)
            || !GameplayOutcomeStableIdSyntax.IsValid(definition)
            || definition != KnowledgeResidueDestinationAuthority.MemoryResidueItemId
            || !string.Equals(operation, outcome.ResultKey.OperationId.Value,
                StringComparison.Ordinal)
            || !string.Equals(commit, ExpectedCommit(operation, mass),
                StringComparison.Ordinal)
            || !string.Equals(fingerprint, ExpectedFingerprint(reason, stack),
                StringComparison.Ordinal))
        {
            return false;
        }

        if (material.Kind.Equals(KnowledgeCompletionOutcomeIds.ItemInstanceKind))
        {
            if (!KnowledgeCompletionOutcomeFacts.TryGetFact(outcome,
                    KnowledgeCompletionOutcomeIds.SourceInstanceFact,
                    out string instance)
                || !GameplayOutcomeStableIdSyntax.IsValid(instance)
                || !string.Equals(instance, material.Value,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        else if (!material.Kind.Equals(KnowledgeCompletionOutcomeIds.ItemStackKind)
            || !string.Equals(stack, material.Value, StringComparison.Ordinal)
            || KnowledgeCompletionOutcomeFacts.ContainsFact(
                outcome,
                KnowledgeCompletionOutcomeIds.SourceInstanceFact))
        {
            return false;
        }

        if (use == KnowledgeResidueUse.CodexAnalysis)
        {
            return target.Kind.Equals(KnowledgeCompletionOutcomeIds.CodexEntryKind)
                && KnowledgeCompletionOutcomeFacts.TryGetFact(outcome,
                    KnowledgeCompletionOutcomeIds.CodexClueFact, out string clue)
                && IsBoundedText(clue, allowEmpty: false);
        }

        return !KnowledgeCompletionOutcomeFacts.ContainsFact(
            outcome, KnowledgeCompletionOutcomeIds.CodexClueFact);
    }

    private static bool TryValidateRewardValues(
        KnowledgeResidueUse use,
        double before,
        double after)
    {
        if (double.IsNaN(before) || double.IsInfinity(before)
            || double.IsNaN(after) || double.IsInfinity(after))
        {
            return false;
        }
        if (use == KnowledgeResidueUse.CodexAnalysis)
        {
            return IsExactNonNegativeInteger(before)
                && IsExactNonNegativeInteger(after)
                && Math.Abs(after - before - 1d) < Epsilon;
        }
        return before >= 0d
            && before < 100d
            && after > before
            && after <= 100d
            && Math.Abs(after - Math.Min(100d, before + 10d)) < Epsilon;
    }

    private static bool IsIdentity(string id, string display) =>
        GameplayOutcomeStableIdSyntax.IsValid(id)
        && IsBoundedText(display, allowEmpty: false);

    private static bool IsBoundedText(string value, bool allowEmpty) =>
        GameplayOutcomeLedger.IsValidBoundedUtf16(
            value,
            GameplayOutcomeBufferLimits.MaximumDisplayTextUtf16Length,
            allowEmpty);

    private static bool IsExactNonNegativeInteger(float value) =>
        value >= 0f && value <= int.MaxValue
        && Math.Abs(value - (float)Math.Round(value)) < Epsilon;

    private static bool IsExactNonNegativeInteger(double value) =>
        value >= 0d && value <= int.MaxValue
        && Math.Abs(value - Math.Round(value)) < Epsilon;

    private static bool IsDirect(
        in GameplayOutcomeParticipant participant,
        GameplayEntityKindId kind) =>
        participant.EntityId.Kind.Equals(kind)
        && participant.EntityId.IsValid
        && participant.ParticipationKind == GameplayParticipationKind.Direct
        && participant.HasPerceptionEvidence
        && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(participant.DisplayName);

    private static string ExpectedCommit(string operation, double mass) =>
        "physical-batch-disposition:"
        + ((int)PhysicalItemDispositionKind.Sink).ToString(CultureInfo.InvariantCulture)
        + ":" + operation + ":1:"
        + mass.ToString("0", CultureInfo.InvariantCulture);

    private static string ExpectedFingerprint(string reason, string stack) =>
        ((int)PhysicalItemDispositionKind.Sink).ToString(CultureInfo.InvariantCulture)
        + ":" + reason + ":" + stack + "=1";
}

internal sealed class KnowledgeCompletionOutcomeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId)
    {
        return KnowledgeCompletionOutcomeFacts.TryGetParticipant(
                outcome, KnowledgeCompletionOutcomeIds.TaskRole, out GameplayOutcomeParticipant task)
            ? new GameplayMemorySignature("knowledge-completion:"
                + NarrativeInferenceHash.ComputeSha256Utf8(task.EntityId.ToString()))
            : new GameplayMemorySignature("knowledge-completion-invalid");
    }

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay) =>
        new(1f, NarrativeMemoryTier.Core, int.MaxValue);
}

internal sealed class KnowledgeCompletionOutcomePerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;

    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class KnowledgeCompletionOutcomeConsolidator :
    IOutcomeMemoryConsolidator,
    IOutcomeCompactionContract
{
    public bool SupportsCompaction => false;

    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;

    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;

    public bool CanMerge(
        in CompactedNarrativeMemoryReadView existing,
        in GameplayOutcomeReadView incoming,
        GameplayEntityId subjectId) => false;
}

internal sealed class KnowledgeCompletionOutcomePerspectiveProjector :
    INarrativePerspectiveProjector
{
    private readonly IKoreanJosaFormatter josa;

    public KnowledgeCompletionOutcomePerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        if (!KnowledgeCompletionOutcomeFacts.TryReadUse(outcome, out KnowledgeResidueUse use)
            || !KnowledgeCompletionOutcomeFacts.TryGetParticipant(outcome,
                KnowledgeCompletionOutcomeIds.TaskRole, out GameplayOutcomeParticipant task)
            || !KnowledgeCompletionOutcomeFacts.TryGetParticipant(outcome,
                KnowledgeCompletionOutcomeIds.FacilityRole, out GameplayOutcomeParticipant facility)
            || !KnowledgeCompletionOutcomeFacts.TryGetParticipant(outcome,
                KnowledgeCompletionOutcomeIds.RewardTargetRole, out GameplayOutcomeParticipant target)
            || !KnowledgeCompletionOutcomeFacts.TryGetMetric(outcome,
                KnowledgeCompletionOutcomeIds.RewardBeforeMetric,
                use == KnowledgeResidueUse.CodexAnalysis
                    ? KnowledgeCompletionOutcomeIds.CountUnit
                    : KnowledgeCompletionOutcomeIds.PercentUnit,
                target.EntityId, out double before)
            || !KnowledgeCompletionOutcomeFacts.TryGetMetric(outcome,
                KnowledgeCompletionOutcomeIds.RewardAfterMetric,
                use == KnowledgeResidueUse.CodexAnalysis
                    ? KnowledgeCompletionOutcomeIds.CountUnit
                    : KnowledgeCompletionOutcomeIds.PercentUnit,
                target.EntityId, out double after))
        {
            return new NarrativeView(
                outcome.OutcomeId,
                perspective.Kind,
                "기억 잔재 완료 기록",
                "knowledge-completion-v1+" + josa.FormatterVersion,
                true);
        }

        bool neutral = !TryWithJosa(task.DisplayName, KoreanJosaKind.Object,
            out string taskObject);
        string prefix = perspective.Kind == NarrativePerspectiveKind.Facility
            && perspective.ViewerId == facility.EntityId
            ? facility.DisplayName.DisplayText + "에서 "
            : string.Empty;
        string text;
        if (use == KnowledgeResidueUse.CodexAnalysis)
        {
            text = neutral
                ? $"기억 잔재 분석 완료 · {task.DisplayName.DisplayText} · 도감 단서 {before:0} → {after:0}"
                : $"{prefix}{taskObject} 마쳐 도감 단서를 {before:0}개에서 {after:0}개로 늘렸다.";
        }
        else
        {
            text = neutral
                ? $"기억 정찰 완료 · {task.DisplayName.DisplayText} · {target.DisplayName.DisplayText} 정보망 약화 {before:0.#} → {after:0.#}"
                : $"{prefix}{taskObject} 마쳐 {target.DisplayName.DisplayText}의 정보망 약화 수치가 {before:0.#}에서 {after:0.#}로 올랐다.";
        }

        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "knowledge-completion-v1+" + josa.FormatterVersion,
            neutral);
    }

    private bool TryWithJosa(
        in KoreanNameSnapshot name,
        KoreanJosaKind kind,
        out string text)
    {
        KoreanJosaFormatResult result = josa.Format(
            new KoreanJosaRequest(name, kind));
        text = result.Text;
        return !result.RequiresNeutralFrame;
    }
}

internal static class KnowledgeCompletionOutcomeFacts
{
    internal static string UseValue(KnowledgeResidueUse use) =>
        use == KnowledgeResidueUse.CodexAnalysis
            ? "codex-analysis"
            : "region-reconnaissance";

    internal static bool TryReadUse(
        in GameplayOutcomeReadView outcome,
        out KnowledgeResidueUse use)
    {
        use = default;
        return TryGetFact(outcome, KnowledgeCompletionOutcomeIds.UseFact, out string value)
            && (value == "codex-analysis"
                ? SetUse(KnowledgeResidueUse.CodexAnalysis, out use)
                : value == "region-reconnaissance"
                    && SetUse(KnowledgeResidueUse.RegionReconnaissance, out use));
    }

    internal static bool TryGetParticipant(
        in GameplayOutcomeReadView outcome,
        GameplayRoleId role,
        out GameplayOutcomeParticipant participant)
    {
        participant = default;
        bool found = false;
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant candidate = outcome.GetParticipant(index);
            if (!candidate.RoleId.Equals(role))
                continue;
            if (found)
                return false;
            participant = candidate;
            found = true;
        }
        return found;
    }

    internal static bool TryGetMetric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId,
        GameplayEntityId target,
        out double value)
    {
        value = 0d;
        bool found = false;
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (!metric.MetricId.Equals(metricId))
                continue;
            if (found || !metric.UnitId.Equals(unitId)
                || !metric.DefinitionOrInstanceId.Equals(target))
            {
                return false;
            }
            value = metric.Value;
            found = true;
        }
        return found;
    }

    internal static bool TryGetFact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId factId,
        out string value)
    {
        value = string.Empty;
        bool found = false;
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (!fact.FactId.Equals(factId))
                continue;
            if (found)
                return false;
            value = fact.Value;
            found = true;
        }
        return found;
    }

    internal static bool ContainsFact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId factId)
    {
        for (int index = 0; index < outcome.FactCount; index++)
        {
            if (outcome.GetFact(index).FactId.Equals(factId))
                return true;
        }
        return false;
    }

    private static bool SetUse(KnowledgeResidueUse value, out KnowledgeResidueUse use)
    {
        use = value;
        return true;
    }
}
