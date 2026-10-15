using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Narrative.Korean;

public static class HeritableTraitAnalysisOutcomeIds
{
    public const string ProducerId = "character.trait-analysis";
    public static readonly GameplayOutcomeTypeId Completed =
        new("character.heritable-traits-analyzed");
    public static readonly GameplayEntityKindId CharacterKind =
        new("character");
    public static readonly GameplayRoleId SubjectRole =
        new("analysis-subject");
    public static readonly GameplayMetricId BeforeState =
        new("trait-analysis.visible-before");
    public static readonly GameplayMetricId AfterState =
        new("trait-analysis.visible-after");
    public static readonly GameplayMetricId ConsumedKitCount =
        new("trait-analysis.consumed-kit-count");
    public static readonly GameplayMetricId PhysicalInputMass =
        new("trait-analysis.physical-input-mass");
    public static readonly GameplayMetricUnitId BooleanUnit = new("boolean");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId GramUnit = new("gram");
    public static readonly GameplayOutcomeTagId AnalysisTag =
        new("heritable-trait-analysis");
    public static readonly GameplayOutcomeFactId FacilityInstanceId =
        new("analysis.facility-instance-id");
    public static readonly GameplayOutcomeFactId FacilityDefinitionId =
        new("analysis.facility-definition-id");
    public static readonly GameplayOutcomeFactId KitItemId =
        new("analysis.kit-item-id");
    public static readonly GameplayOutcomeFactId PhysicalCommitId =
        new("analysis.physical-commit-id");
    public static readonly GameplayOutcomeFactId SourceStackIds =
        new("analysis.source-stack-ids");
    public static readonly GameplayOutcomeFactId LatentTraitIds =
        new("analysis.latent-trait-ids");
    public const string PhysicalEvidenceAnchor =
        "trait-analysis-physical-sha256";
    public static readonly GameplayOutcomeFactId[] OrderedFacts =
    {
        FacilityInstanceId,
        FacilityDefinitionId,
        KitItemId,
        PhysicalCommitId,
        SourceStackIds,
        LatentTraitIds
    };
}

public readonly struct HeritableTraitAnalysisOutcomeReceipt
{
    public HeritableTraitAnalysisOutcomeReceipt(
        string operationId,
        long ownerRevision,
        string characterId,
        string characterDisplayName,
        string facilityInstanceId,
        string facilityDefinitionId,
        string kitItemId,
        string physicalCommitId,
        IReadOnlyList<string> sourceStackIds,
        long physicalInputMassGrams,
        IReadOnlyList<string> latentTraitIds,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(operationId);
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        OwnerRevision = ownerRevision;
        CharacterId = GameplayOutcomeStableIdSyntax.Require(
            characterId,
            nameof(characterId));
        CharacterDisplayName = EvolutionOutcomeNames.Snapshot(
            CharacterId,
            characterDisplayName);
        FacilityInstanceId = GameplayOutcomeStableIdSyntax.Require(
            facilityInstanceId,
            nameof(facilityInstanceId));
        FacilityDefinitionId = GameplayOutcomeStableIdSyntax.Require(
            facilityDefinitionId,
            nameof(facilityDefinitionId));
        KitItemId = GameplayOutcomeStableIdSyntax.Require(
            kitItemId,
            nameof(kitItemId));
        PhysicalCommitId = GameplayOutcomeStableIdSyntax.Require(
            physicalCommitId,
            nameof(physicalCommitId));
        SourceStackIds = CanonicalIds(sourceStackIds, nameof(sourceStackIds));
        LatentTraitIds = CanonicalIds(latentTraitIds, nameof(latentTraitIds));
        if (physicalInputMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(physicalInputMassGrams));
        PhysicalInputMassGrams = physicalInputMassGrams;
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        AbsoluteDay = absoluteDay;
        SourceStackCanonical = string.Join(",", SourceStackIds);
        LatentTraitCanonical = string.Join(",", LatentTraitIds);
        PhysicalEvidenceCanonical = string.Join(
            ";",
            "physical-v1",
            PhysicalCommitId,
            KitItemId,
            SourceStackCanonical,
            PhysicalInputMassGrams.ToString(CultureInfo.InvariantCulture));
        RequireFact(SourceStackCanonical, nameof(sourceStackIds));
        RequireFact(LatentTraitCanonical, nameof(latentTraitIds));
        RequireFact(PhysicalEvidenceCanonical, "physicalEvidence");
    }

    public GameplayOperationId OperationId { get; }
    public long OwnerRevision { get; }
    public string CharacterId { get; }
    public KoreanNameSnapshot CharacterDisplayName { get; }
    public string FacilityInstanceId { get; }
    public string FacilityDefinitionId { get; }
    public string KitItemId { get; }
    public string PhysicalCommitId { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public long PhysicalInputMassGrams { get; }
    public IReadOnlyList<string> LatentTraitIds { get; }
    public int AbsoluteDay { get; }
    public string SourceStackCanonical { get; }
    public string LatentTraitCanonical { get; }
    public string PhysicalEvidenceCanonical { get; }
    public GameplayResultKey ResultKey => new(
        HeritableTraitAnalysisOutcomeIds.ProducerId,
        OperationId,
        OwnerRevision,
        0);

    private static string[] CanonicalIds(
        IReadOnlyList<string> values,
        string parameterName)
    {
        string[] result = (values ?? Array.Empty<string>())
            .Select(value => GameplayOutcomeStableIdSyntax.Require(
                value,
                parameterName))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (result.Length == 0
            || result.Distinct(StringComparer.Ordinal).Count()
                != result.Length)
            throw new ArgumentException(
                "A non-empty distinct stable-id set is required.",
                parameterName);
        return result;
    }

    private static void RequireFact(string value, string parameterName)
    {
        if (!GameplayOutcomeLedger.IsValidBoundedUtf16(
                value,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false))
        {
            throw new ArgumentException(
                "A bounded immutable fact is required.",
                parameterName);
        }
    }
}

public sealed class HeritableTraitAnalysisOutcomeAdapter :
    GameplayOutcomeAdapter<HeritableTraitAnalysisOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        HeritableTraitAnalysisOutcomeIds.Completed;

    public override OutcomePrepareResult TryGetRequirements(
        in HeritableTraitAnalysisOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            1,
            4,
            1,
            1,
            1,
            factCount: 6);
        bool valid = receipt.OperationId.IsValid
            && receipt.OwnerRevision > 0L
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.CharacterId)
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(
                receipt.CharacterDisplayName)
            && GameplayOutcomeStableIdSyntax.IsValid(
                receipt.FacilityInstanceId)
            && GameplayOutcomeStableIdSyntax.IsValid(
                receipt.FacilityDefinitionId)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.KitItemId)
            && GameplayOutcomeStableIdSyntax.IsValid(
                receipt.PhysicalCommitId)
            && receipt.SourceStackIds.Count > 0
            && receipt.LatentTraitIds.Count > 0
            && receipt.PhysicalInputMassGrams > 0L
            && receipt.AbsoluteDay >= 1;
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "heritable-trait-analysis-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in HeritableTraitAnalysisOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            HeritableTraitAnalysisOutcomeIds.CharacterKind,
            receipt.CharacterId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                character,
                HeritableTraitAnalysisOutcomeIds.SubjectRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.CharacterDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character,
                0.82f,
                NarrativeMemoryTier.Core,
                true,
                false,
                0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                HeritableTraitAnalysisOutcomeIds.BeforeState,
                0d,
                HeritableTraitAnalysisOutcomeIds.BooleanUnit,
                character))
            && builder.AddMetric(new GameplayOutcomeMetric(
                HeritableTraitAnalysisOutcomeIds.AfterState,
                1d,
                HeritableTraitAnalysisOutcomeIds.BooleanUnit,
                character))
            && builder.AddMetric(new GameplayOutcomeMetric(
                HeritableTraitAnalysisOutcomeIds.ConsumedKitCount,
                1d,
                HeritableTraitAnalysisOutcomeIds.CountUnit,
                character))
            && builder.AddMetric(new GameplayOutcomeMetric(
                HeritableTraitAnalysisOutcomeIds.PhysicalInputMass,
                receipt.PhysicalInputMassGrams,
                HeritableTraitAnalysisOutcomeIds.GramUnit,
                character))
            && builder.AddTag(HeritableTraitAnalysisOutcomeIds.AnalysisTag)
            && builder.AddFact(new GameplayOutcomeFact(
                HeritableTraitAnalysisOutcomeIds.FacilityInstanceId,
                receipt.FacilityInstanceId))
            && builder.AddFact(new GameplayOutcomeFact(
                HeritableTraitAnalysisOutcomeIds.FacilityDefinitionId,
                receipt.FacilityDefinitionId))
            && builder.AddFact(new GameplayOutcomeFact(
                HeritableTraitAnalysisOutcomeIds.KitItemId,
                receipt.KitItemId))
            && builder.AddFact(new GameplayOutcomeFact(
                HeritableTraitAnalysisOutcomeIds.PhysicalCommitId,
                receipt.PhysicalCommitId))
            && builder.AddFact(new GameplayOutcomeFact(
                HeritableTraitAnalysisOutcomeIds.SourceStackIds,
                receipt.SourceStackCanonical))
            && builder.AddFact(new GameplayOutcomeFact(
                HeritableTraitAnalysisOutcomeIds.LatentTraitIds,
                receipt.LatentTraitCanonical))
            && builder.AddAnchor(
                character,
                new NarrativeEvidenceReference(
                    HeritableTraitAnalysisOutcomeIds.PhysicalEvidenceAnchor,
                    NarrativeInferenceHash.ComputeSha256Utf8(
                        receipt.PhysicalEvidenceCanonical)));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "heritable-trait-analysis-write-failed");
    }
}

public sealed class HeritableTraitAnalysisOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new HeritableTraitAnalysisPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new EvolutionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EvolutionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EvolutionOutcomeConsolidator(false);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        HeritableTraitAnalysisOutcomeIds.Completed;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(HeritableTraitAnalysisOutcomeIds.SubjectRole);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        (metricId.Equals(HeritableTraitAnalysisOutcomeIds.BeforeState)
            && unitId.Equals(HeritableTraitAnalysisOutcomeIds.BooleanUnit))
        || (metricId.Equals(HeritableTraitAnalysisOutcomeIds.AfterState)
            && unitId.Equals(HeritableTraitAnalysisOutcomeIds.BooleanUnit))
        || (metricId.Equals(HeritableTraitAnalysisOutcomeIds.ConsumedKitCount)
            && unitId.Equals(HeritableTraitAnalysisOutcomeIds.CountUnit))
        || (metricId.Equals(HeritableTraitAnalysisOutcomeIds.PhysicalInputMass)
            && unitId.Equals(HeritableTraitAnalysisOutcomeIds.GramUnit));

    public OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != OutcomeTypeId
            || outcome.ParticipantCount != 1
            || outcome.SubjectCount != 1
            || outcome.MetricCount != 4
            || outcome.TagCount != 1
            || outcome.AnchorCount != 1
            || outcome.FactCount != 6)
            return OutcomeValidationResult.Reject(
                "heritable-trait-analysis-shape-invalid");
        GameplayOutcomeParticipant participant = outcome.GetParticipant(0);
        if (!participant.RoleId.Equals(
                HeritableTraitAnalysisOutcomeIds.SubjectRole)
            || !participant.EntityId.Kind.Equals(
                HeritableTraitAnalysisOutcomeIds.CharacterKind)
            || outcome.GetSubject(0).SubjectId != participant.EntityId
            || !outcome.GetTag(0).Equals(
                HeritableTraitAnalysisOutcomeIds.AnalysisTag)
            || !MetricMatches(
                outcome.GetMetric(0),
                HeritableTraitAnalysisOutcomeIds.BeforeState,
                HeritableTraitAnalysisOutcomeIds.BooleanUnit,
                participant.EntityId,
                0d)
            || !MetricMatches(
                outcome.GetMetric(1),
                HeritableTraitAnalysisOutcomeIds.AfterState,
                HeritableTraitAnalysisOutcomeIds.BooleanUnit,
                participant.EntityId,
                1d)
            || !MetricMatches(
                outcome.GetMetric(2),
                HeritableTraitAnalysisOutcomeIds.ConsumedKitCount,
                HeritableTraitAnalysisOutcomeIds.CountUnit,
                participant.EntityId,
                1d)
            || !MetricMatches(
                outcome.GetMetric(3),
                HeritableTraitAnalysisOutcomeIds.PhysicalInputMass,
                HeritableTraitAnalysisOutcomeIds.GramUnit,
                participant.EntityId,
                outcome.GetMetric(3).Value)
            || outcome.GetMetric(3).Value <= 0d
            || outcome.GetMetric(3).Value % 1d != 0d)
            return OutcomeValidationResult.Reject(
                "heritable-trait-analysis-subject-invalid");
        for (int index = 0;
             index < HeritableTraitAnalysisOutcomeIds.OrderedFacts.Length;
             index++)
        {
            if (!outcome.GetFact(index).FactId.Equals(
                    HeritableTraitAnalysisOutcomeIds.OrderedFacts[index]))
                return OutcomeValidationResult.Reject(
                    "heritable-trait-analysis-facts-invalid");
        }
        GameplayOutcomeFact physicalCommit = outcome.GetFact(3);
        GameplayOutcomeFact kitItem = outcome.GetFact(2);
        GameplayOutcomeFact sourceStacks = outcome.GetFact(4);
        NarrativeEvidenceReference anchor = outcome.GetAnchor(0);
        string physicalCanonical = string.Join(
            ";",
            "physical-v1",
            physicalCommit.Value,
            kitItem.Value,
            sourceStacks.Value,
            outcome.GetMetric(3).Value.ToString(
                "0",
                CultureInfo.InvariantCulture));
        if (outcome.GetAnchorSubject(0) != participant.EntityId
            || anchor.AnchorTypeId
                != HeritableTraitAnalysisOutcomeIds.PhysicalEvidenceAnchor
            || !string.Equals(
                anchor.AnchorId,
                NarrativeInferenceHash.ComputeSha256Utf8(physicalCanonical),
                StringComparison.Ordinal))
            return OutcomeValidationResult.Reject(
                "heritable-trait-analysis-anchor-invalid");
        return OutcomeValidationResult.Accepted;
    }

    private static bool MetricMatches(
        GameplayOutcomeMetric metric,
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId,
        GameplayEntityId reference,
        double value) =>
        metric.MetricId.Equals(metricId)
        && metric.UnitId.Equals(unitId)
        && metric.DefinitionOrInstanceId == reference
        && metric.Value == value;
}

internal sealed class HeritableTraitAnalysisPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective) => new(
        outcome.OutcomeId,
        perspective.Kind,
        outcome.GetParticipant(0).DisplayName.DisplayText
            + "의 잠재 유전 특성을 분석해 공개했다.",
        "heritable-trait-analysis-v1",
        true);
}
