using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Narrative.Korean;

public static class EquipmentModuleSelectionOutcomeIds
{
    public const string ProducerId = "equipment.evolution-module-selection";
    public static readonly GameplayOutcomeTypeId Selected =
        new("equipment.evolution-module-selected");
    public static readonly GameplayEntityKindId NodeKind =
        new("equipment-evolution-node");
    public static readonly GameplayEntityKindId EquipmentKind =
        new("item-instance");
    public static readonly GameplayRoleId SelectedNodeRole =
        new("selected-evolution-node");
    public static readonly GameplayRoleId EquipmentRole =
        new("evolved-equipment");

    public static readonly GameplayMetricId RevisionBefore =
        new("equipment-evolution.module-selection-revision-before");
    public static readonly GameplayMetricId RevisionAfter =
        new("equipment-evolution.module-selection-revision-after");
    public static readonly GameplayMetricId GenerationBefore =
        new("equipment-evolution.generation-before");
    public static readonly GameplayMetricId GenerationAfter =
        new("equipment-evolution.generation-after");
    public static readonly GameplayMetricId MasteryBefore =
        new("equipment-evolution.mastery-before");
    public static readonly GameplayMetricId MasteryAfter =
        new("equipment-evolution.mastery-after");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayMetricUnitId GenerationUnit = new("generation");
    public static readonly GameplayMetricUnitId MasteryUnit = new("mastery-point");
    public static readonly GameplayOutcomeTagId EvolutionSelectionTag =
        new("equipment-evolution-module-selection");

    public static readonly GameplayOutcomeFactId OperationIdFact =
        new("equipment-evolution.operation-id");
    public static readonly GameplayOutcomeFactId SelectionIdFact =
        new("equipment-evolution.selection-id");
    public static readonly GameplayOutcomeFactId NodeIdFact =
        new("equipment-evolution.node-id");
    public static readonly GameplayOutcomeFactId EquipmentInstanceIdFact =
        new("equipment-evolution.equipment-instance-id");
    public static readonly GameplayOutcomeFactId EquipmentDefinitionIdFact =
        new("equipment-evolution.equipment-definition-id");
    public static readonly GameplayOutcomeFactId OwnershipFact =
        new("equipment-evolution.ownership-canonical");
    public static readonly GameplayOutcomeFactId ContextFact =
        new("equipment-evolution.context-canonical");
    public static readonly GameplayOutcomeFactId ModulesFact =
        new("equipment-evolution.modules-canonical");
    public static readonly GameplayOutcomeFactId MechanicsFact =
        new("equipment-evolution.mechanics-canonical");
}

/// <summary>
/// Frozen terminal truth from one successful equipment module-selection commit.
/// The source owner supplies the exact selected IDs and before/after values;
/// this receipt never derives mechanics from narrative text.
/// </summary>
public readonly struct EquipmentModuleSelectionOutcomeReceipt
{
    private const int MaximumFactLength =
        GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length;

    public EquipmentModuleSelectionOutcomeReceipt(
        string operationId,
        string selectionId,
        long revisionBefore,
        long revisionAfter,
        string nodeId,
        string nodeDisplayName,
        string equipmentInstanceId,
        string equipmentDefinitionId,
        string currentOwnerId,
        string attunementOwnerId,
        string reforgeOrderId,
        bool historical,
        int generationBefore,
        int generationAfter,
        float masteryBefore,
        float masteryAfter,
        IReadOnlyList<string> positiveModuleIds,
        IReadOnlyList<string> drawbackModuleIds,
        IReadOnlyList<string> selectedModuleIds,
        IReadOnlyList<string> capabilityIds,
        IReadOnlyList<string> effectIds,
        IReadOnlyList<string> evidenceFactIds,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(operationId);
        SelectionId = GameplayOutcomeStableIdSyntax.Require(
            selectionId, nameof(selectionId));
        if (revisionBefore < 0L || revisionBefore == long.MaxValue
            || revisionAfter != revisionBefore + 1L)
            throw new ArgumentException(
                "The module-selection revision must advance exactly once.",
                nameof(revisionAfter));
        NodeId = GameplayOutcomeStableIdSyntax.Require(nodeId, nameof(nodeId));
        NodeDisplayName = EvolutionOutcomeNames.Snapshot(NodeId, nodeDisplayName);
        EquipmentInstanceId = GameplayOutcomeStableIdSyntax.Require(
            equipmentInstanceId, nameof(equipmentInstanceId));
        EquipmentDefinitionId = GameplayOutcomeStableIdSyntax.Require(
            equipmentDefinitionId, nameof(equipmentDefinitionId));
        CurrentOwnerId = OptionalId(currentOwnerId, nameof(currentOwnerId));
        AttunementOwnerId = OptionalId(
            attunementOwnerId, nameof(attunementOwnerId));
        ReforgeOrderId = OptionalId(reforgeOrderId, nameof(reforgeOrderId));
        if (generationBefore < 0 || generationAfter < 0)
            throw new ArgumentOutOfRangeException(nameof(generationAfter));
        if (!FiniteNonnegative(masteryBefore) || !FiniteNonnegative(masteryAfter))
            throw new ArgumentOutOfRangeException(nameof(masteryAfter));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        IReadOnlyList<string> positive = FreezeIds(
            positiveModuleIds, nameof(positiveModuleIds), required: true);
        IReadOnlyList<string> drawbacks = FreezeIds(
            drawbackModuleIds, nameof(drawbackModuleIds), required: false);
        IReadOnlyList<string> selected = FreezeIds(
            selectedModuleIds, nameof(selectedModuleIds), required: true);
        IReadOnlyList<string> capabilities = FreezeIds(
            capabilityIds, nameof(capabilityIds), required: true);
        IReadOnlyList<string> effects = FreezeIds(
            effectIds, nameof(effectIds), required: false);
        IReadOnlyList<string> evidence = FreezeIds(
            evidenceFactIds, nameof(evidenceFactIds), required: true);
        string[] expectedModules = positive.Concat(drawbacks)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (expectedModules.Length != selected.Count
            || !expectedModules.SequenceEqual(selected, StringComparer.Ordinal))
            throw new ArgumentException(
                "Selected modules must exactly match positive and drawback modules.",
                nameof(selectedModuleIds));

        RevisionBefore = revisionBefore;
        RevisionAfter = revisionAfter;
        Historical = historical;
        GenerationBefore = generationBefore;
        GenerationAfter = generationAfter;
        MasteryBefore = masteryBefore;
        MasteryAfter = masteryAfter;
        PositiveModuleIds = positive;
        DrawbackModuleIds = drawbacks;
        SelectedModuleIds = selected;
        CapabilityIds = capabilities;
        EffectIds = effects;
        EvidenceFactIds = evidence;
        OwnershipCanonical = RequireBoundedFact(Fingerprint(
            CurrentOwnerId, AttunementOwnerId));
        ContextCanonical = RequireBoundedFact(Fingerprint(
            ReforgeOrderId, Historical ? "true" : "false"));
        ModulesCanonical = RequireBoundedFact(Fingerprint(
            Join(positive), Join(drawbacks), Join(selected)));
        MechanicsCanonical = RequireBoundedFact(Fingerprint(
            Join(capabilities), Join(effects), Join(evidence)));
        AbsoluteDay = absoluteDay;
        EquipmentInstanceSha256 = NarrativeInferenceHash.ComputeSha256Utf8(
            EquipmentInstanceId);
        SelectionSha256 = NarrativeInferenceHash.ComputeSha256Utf8(
            Fingerprint(OperationId.Value, SelectionId, NodeId));
        MechanicsSha256 = NarrativeInferenceHash.ComputeSha256Utf8(
            Fingerprint(
                revisionBefore.ToString(CultureInfo.InvariantCulture),
                revisionAfter.ToString(CultureInfo.InvariantCulture),
                generationBefore.ToString(CultureInfo.InvariantCulture),
                generationAfter.ToString(CultureInfo.InvariantCulture),
                masteryBefore.ToString("R", CultureInfo.InvariantCulture),
                masteryAfter.ToString("R", CultureInfo.InvariantCulture),
                Join(positive), Join(drawbacks), Join(selected),
                Join(capabilities), Join(effects)));
        EvidenceSha256 = NarrativeInferenceHash.ComputeSha256Utf8(
            Fingerprint(Join(evidence)));
    }

    public GameplayOperationId OperationId { get; }
    public string SelectionId { get; }
    public long RevisionBefore { get; }
    public long RevisionAfter { get; }
    public string NodeId { get; }
    public KoreanNameSnapshot NodeDisplayName { get; }
    public string EquipmentInstanceId { get; }
    public string EquipmentDefinitionId { get; }
    public string CurrentOwnerId { get; }
    public string AttunementOwnerId { get; }
    public string ReforgeOrderId { get; }
    public bool Historical { get; }
    public int GenerationBefore { get; }
    public int GenerationAfter { get; }
    public float MasteryBefore { get; }
    public float MasteryAfter { get; }
    public IReadOnlyList<string> PositiveModuleIds { get; }
    public IReadOnlyList<string> DrawbackModuleIds { get; }
    public IReadOnlyList<string> SelectedModuleIds { get; }
    public IReadOnlyList<string> CapabilityIds { get; }
    public IReadOnlyList<string> EffectIds { get; }
    public IReadOnlyList<string> EvidenceFactIds { get; }
    public string OwnershipCanonical { get; }
    public string ContextCanonical { get; }
    public string ModulesCanonical { get; }
    public string MechanicsCanonical { get; }
    public int AbsoluteDay { get; }
    public string EquipmentInstanceSha256 { get; }
    public string SelectionSha256 { get; }
    public string MechanicsSha256 { get; }
    public string EvidenceSha256 { get; }
    public GameplayResultKey ResultKey => new(
        EquipmentModuleSelectionOutcomeIds.ProducerId,
        OperationId,
        RevisionAfter,
        0);

    internal bool IsValid => OperationId.IsValid
        && GameplayOutcomeStableIdSyntax.IsValid(SelectionId)
        && GameplayOutcomeStableIdSyntax.IsValid(NodeId)
        && GameplayOutcomeStableIdSyntax.IsValid(EquipmentInstanceId)
        && GameplayOutcomeStableIdSyntax.IsValid(EquipmentDefinitionId)
        && !string.IsNullOrWhiteSpace(NodeDisplayName.DisplayText)
        && RevisionBefore >= 0L && RevisionBefore < long.MaxValue
        && RevisionAfter == RevisionBefore + 1L
        && GenerationBefore >= 0 && GenerationAfter >= 0
        && FiniteNonnegative(MasteryBefore) && FiniteNonnegative(MasteryAfter)
        && PositiveModuleIds is { Count: > 0 }
        && DrawbackModuleIds != null
        && SelectedModuleIds is { Count: > 0 }
        && CapabilityIds is { Count: > 0 }
        && EffectIds != null
        && EvidenceFactIds is { Count: > 0 }
        && !string.IsNullOrWhiteSpace(OwnershipCanonical)
        && !string.IsNullOrWhiteSpace(ContextCanonical)
        && !string.IsNullOrWhiteSpace(ModulesCanonical)
        && !string.IsNullOrWhiteSpace(MechanicsCanonical)
        && AbsoluteDay >= 0;

    internal static string FactIds(IReadOnlyList<string> values) =>
        values == null || values.Count == 0 ? "none" : string.Join(",", values);

    internal static string OptionalFact(string value) =>
        string.IsNullOrEmpty(value) ? "none" : value;

    private static string OptionalId(string value, string name) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : GameplayOutcomeStableIdSyntax.Require(value, name);

    private static IReadOnlyList<string> FreezeIds(
        IReadOnlyList<string> source,
        string name,
        bool required)
    {
        if (source == null)
            throw new ArgumentNullException(name);
        string[] values = source
            .Select(value => GameplayOutcomeStableIdSyntax.Require(value, name))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (required && values.Length == 0)
            throw new ArgumentException("At least one exact ID is required.", name);
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Exact IDs must be unique.", name);
        if (FactIds(values).Length > MaximumFactLength)
            throw new ArgumentException("Exact IDs exceed the outcome fact limit.", name);
        return Array.AsReadOnly(values);
    }

    private static bool FiniteNonnegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static string Join(IReadOnlyList<string> values) =>
        string.Join(",", values);

    private static string Fingerprint(params string[] values)
    {
        StringBuilder builder = new();
        foreach (string value in values)
        {
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(value);
        }
        return builder.ToString();
    }

    private static string RequireBoundedFact(string value)
    {
        if (!GameplayOutcomeLedger.IsValidBoundedUtf16(
                value,
                MaximumFactLength,
                allowEmpty: false))
            throw new ArgumentException(
                "Canonical module-selection fact exceeds the outcome limit.");
        return value;
    }
}

public sealed class EquipmentModuleSelectionOutcomeAdapter :
    GameplayOutcomeAdapter<EquipmentModuleSelectionOutcomeReceipt>
{
    public const int ParticipantCount = 2;
    public const int MetricCount = 6;
    public const int SubjectCount = 2;
    public const int TagCount = 1;
    public const int AnchorCount = 4;
    public const int FactCount = 8;

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EquipmentModuleSelectionOutcomeIds.Selected;

    public override OutcomePrepareResult TryGetRequirements(
        in EquipmentModuleSelectionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.IsValid)
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "equipment-module-selection-receipt-invalid");
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.RevisionAfter,
            ParticipantCount,
            MetricCount,
            SubjectCount,
            TagCount,
            AnchorCount,
            factCount: FactCount);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in EquipmentModuleSelectionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        if (!receipt.IsValid)
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "equipment-module-selection-receipt-invalid");
        GameplayEntityId node = new(
            EquipmentModuleSelectionOutcomeIds.NodeKind,
            receipt.NodeId);
        GameplayEntityId equipment = new(
            EquipmentModuleSelectionOutcomeIds.EquipmentKind,
            receipt.EquipmentInstanceId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                node,
                EquipmentModuleSelectionOutcomeIds.SelectedNodeRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.NodeDisplayName))
            && builder.AddParticipant(new GameplayOutcomeParticipant(
                equipment,
                EquipmentModuleSelectionOutcomeIds.EquipmentRole,
                GameplayParticipationKind.Direct,
                true,
                EvolutionOutcomeNames.Snapshot(
                    receipt.EquipmentInstanceId,
                    receipt.EquipmentDefinitionId)))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                node, 0.9f, NarrativeMemoryTier.Recent, true, false, 0))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                equipment, 0.85f, NarrativeMemoryTier.Recent, true, false, 0))
            && AddMetric(ref builder, node,
                EquipmentModuleSelectionOutcomeIds.RevisionBefore,
                receipt.RevisionBefore,
                EquipmentModuleSelectionOutcomeIds.RevisionUnit)
            && AddMetric(ref builder, node,
                EquipmentModuleSelectionOutcomeIds.RevisionAfter,
                receipt.RevisionAfter,
                EquipmentModuleSelectionOutcomeIds.RevisionUnit)
            && AddMetric(ref builder, node,
                EquipmentModuleSelectionOutcomeIds.GenerationBefore,
                receipt.GenerationBefore,
                EquipmentModuleSelectionOutcomeIds.GenerationUnit)
            && AddMetric(ref builder, node,
                EquipmentModuleSelectionOutcomeIds.GenerationAfter,
                receipt.GenerationAfter,
                EquipmentModuleSelectionOutcomeIds.GenerationUnit)
            && AddMetric(ref builder, node,
                EquipmentModuleSelectionOutcomeIds.MasteryBefore,
                receipt.MasteryBefore,
                EquipmentModuleSelectionOutcomeIds.MasteryUnit)
            && AddMetric(ref builder, node,
                EquipmentModuleSelectionOutcomeIds.MasteryAfter,
                receipt.MasteryAfter,
                EquipmentModuleSelectionOutcomeIds.MasteryUnit)
            && builder.AddTag(
                EquipmentModuleSelectionOutcomeIds.EvolutionSelectionTag)
            && builder.AddAnchor(node, new NarrativeEvidenceReference(
                "equipment-instance-sha256", receipt.EquipmentInstanceSha256))
            && builder.AddAnchor(node, new NarrativeEvidenceReference(
                "equipment-selection-sha256", receipt.SelectionSha256))
            && builder.AddAnchor(node, new NarrativeEvidenceReference(
                "equipment-mechanics-sha256", receipt.MechanicsSha256))
            && builder.AddAnchor(node, new NarrativeEvidenceReference(
                "equipment-evidence-sha256", receipt.EvidenceSha256))
            && AddFact(ref builder,
                EquipmentModuleSelectionOutcomeIds.SelectionIdFact,
                receipt.SelectionId)
            && AddFact(ref builder,
                EquipmentModuleSelectionOutcomeIds.NodeIdFact,
                receipt.NodeId)
            && AddFact(ref builder,
                EquipmentModuleSelectionOutcomeIds.EquipmentInstanceIdFact,
                receipt.EquipmentInstanceId)
            && AddFact(ref builder,
                EquipmentModuleSelectionOutcomeIds.EquipmentDefinitionIdFact,
                receipt.EquipmentDefinitionId)
            && AddFact(ref builder,
                EquipmentModuleSelectionOutcomeIds.OwnershipFact,
                receipt.OwnershipCanonical)
            && AddFact(ref builder,
                EquipmentModuleSelectionOutcomeIds.ContextFact,
                receipt.ContextCanonical)
            && AddFact(ref builder,
                EquipmentModuleSelectionOutcomeIds.ModulesFact,
                receipt.ModulesCanonical)
            && AddFact(ref builder,
                EquipmentModuleSelectionOutcomeIds.MechanicsFact,
                receipt.MechanicsCanonical);
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "equipment-module-selection-write-failed");
    }

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayEntityId node,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit) => builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, node));

    private static bool AddFact(
        ref OutcomeWriteBuilder builder,
        GameplayOutcomeFactId id,
        string value) => builder.AddFact(new GameplayOutcomeFact(id, value));
}

public sealed class EquipmentModuleSelectionOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new EquipmentModuleSelectionPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new EvolutionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EvolutionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EvolutionOutcomeConsolidator(false);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        EquipmentModuleSelectionOutcomeIds.Selected;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(EquipmentModuleSelectionOutcomeIds.SelectedNodeRole)
        || roleId.Equals(EquipmentModuleSelectionOutcomeIds.EquipmentRole);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        (metricId.Equals(EquipmentModuleSelectionOutcomeIds.RevisionBefore)
            || metricId.Equals(EquipmentModuleSelectionOutcomeIds.RevisionAfter))
            && unitId.Equals(EquipmentModuleSelectionOutcomeIds.RevisionUnit)
        || (metricId.Equals(EquipmentModuleSelectionOutcomeIds.GenerationBefore)
            || metricId.Equals(EquipmentModuleSelectionOutcomeIds.GenerationAfter))
            && unitId.Equals(EquipmentModuleSelectionOutcomeIds.GenerationUnit)
        || (metricId.Equals(EquipmentModuleSelectionOutcomeIds.MasteryBefore)
            || metricId.Equals(EquipmentModuleSelectionOutcomeIds.MasteryAfter))
            && unitId.Equals(EquipmentModuleSelectionOutcomeIds.MasteryUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && outcome.ParticipantCount
                == EquipmentModuleSelectionOutcomeAdapter.ParticipantCount
            && outcome.MetricCount
                == EquipmentModuleSelectionOutcomeAdapter.MetricCount
            && outcome.SubjectCount
                == EquipmentModuleSelectionOutcomeAdapter.SubjectCount
            && outcome.TagCount
                == EquipmentModuleSelectionOutcomeAdapter.TagCount
            // Initial anchors are frozen only after first validation. Evidence
            // use can append mutable anchors later, so restore validates the
            // frozen authored count rather than the expanded current count.
            && (outcome.InitialAnchorCount
                    == EquipmentModuleSelectionOutcomeAdapter.AnchorCount
                || outcome.InitialAnchorCount == 0
                && outcome.AnchorCount
                    == EquipmentModuleSelectionOutcomeAdapter.AnchorCount)
            && outcome.FactCount
                == EquipmentModuleSelectionOutcomeAdapter.FactCount
            && outcome.GetParticipant(0).RoleId.Equals(
                EquipmentModuleSelectionOutcomeIds.SelectedNodeRole)
            && outcome.GetParticipant(1).RoleId.Equals(
                EquipmentModuleSelectionOutcomeIds.EquipmentRole);
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "equipment-module-selection-shape-invalid");
    }
}

internal sealed class EquipmentModuleSelectionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant node = outcome.GetParticipant(0);
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            $"장비 진화 「{node.DisplayName.DisplayText}」의 모듈 선택이 확정됐다.",
            "equipment-evolution-module-selection-v1",
            true);
    }
}
