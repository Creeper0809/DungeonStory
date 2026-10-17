using System;
using System.Linq;

/// <summary>Frozen domain result of a completed resource cycle, without log identities.</summary>
public sealed class WorldResourceCycleCompletedFact
{
    private readonly WorldResourcePendingOutputSaveData snapshot;

    public WorldResourceCycleCompletedFact(BuildingInstanceId nodeId, WorkTypeId workTypeId,
        WorldResourcePendingOutputSaveData value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (!nodeId.IsValid || !workTypeId.IsValid || value.cycleSequence <= 0
            || value.operationId != WorldResourceRuntime.FormatCycleOperationId(nodeId, workTypeId, value.cycleSequence)
            || !IsCanonical(value.recipeId) || !value.domainApplied || value.completedAbsoluteDay < 1
            || !float.IsFinite(value.sourceBefore) || !float.IsFinite(value.sourceAfter)
            || value.sourceBefore <= value.sourceAfter || value.sourceAfter < 0f
            || value.renewableSource != !string.IsNullOrEmpty(value.sourcePatchId)
            || value.renewableSource && !IsCanonical(value.sourcePatchId)
            || value.physicalOutputMassGrams < 0L
            || (value.physicalOutputMassGrams > 0L) != !string.IsNullOrEmpty(value.outputBatchCommitId)
            || value.lines == null || value.lines.Any(line => line == null
                || !IsCanonical(line.outputLineId) || !IsCanonical(line.itemId) || line.resolvedQuantity < 0)
            || value.lines.Select(line => line.outputLineId).Distinct(StringComparer.Ordinal).Count() != value.lines.Count
            || value.contributions == null || value.contributions.Any(contributor => contributor == null
                || !IsCanonical(contributor.characterId) || !float.IsFinite(contributor.contributedWork)
                || contributor.contributedWork <= 0f)
            || value.contributions.Select(contributor => contributor.characterId).Distinct(StringComparer.Ordinal).Count()
                != value.contributions.Count)
            throw new ArgumentException("A resource cycle fact requires its exact completed domain state.", nameof(value));

        NodeId = nodeId;
        WorkTypeId = workTypeId;
        snapshot = value.Clone();
        snapshot.parentPublicationKey = string.Empty;
        snapshot.parentResultKey = string.Empty;
    }

    public BuildingInstanceId NodeId { get; }
    public WorkTypeId WorkTypeId { get; }
    public WorldResourcePendingOutputSaveData CaptureDomainState() => snapshot.Clone();

    private static bool IsCanonical(string value) => !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
