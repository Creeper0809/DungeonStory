using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public sealed class CropWaterRefillFact
{
    private const string OperationPrefix = "crop-water-refill:";
    private const float WorkEpsilon = 0.001f;
    private const float WaterEpsilon = 0.0001f;
    private readonly CropWaterRefillSaveData snapshot;

    public CropWaterRefillFact(
        BuildingInstanceId plotId,
        CropWaterRefillSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        bool inputCommitted = source.phase is CropWaterRefillPhase.InputCommitted
            or CropWaterRefillPhase.OutcomePublished
            or CropWaterRefillPhase.LedgerCommitted;
        bool waterApplied = source.phase is CropWaterRefillPhase.OutcomePublished
            or CropWaterRefillPhase.LedgerCommitted;
        if (!plotId.IsValid
            || source.phase is not (CropWaterRefillPhase.Working
                or CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted)
            || source.outcomeSchemaVersion
                != CropWaterRefillSaveData.CurrentOutcomeSchemaVersion
            || source.operationSequence < 0
            || source.operationSequence == int.MaxValue
            || !string.Equals(
                source.operationId,
                FormatOperationId(plotId, source.operationSequence),
                StringComparison.Ordinal)
            || !IsCanonicalRequired(source.itemId)
            || !IsCanonicalRequired(source.responsibleWorkerId)
            || string.IsNullOrWhiteSpace(source.plotDisplayName)
            || string.IsNullOrWhiteSpace(source.itemDisplayName)
            || string.IsNullOrWhiteSpace(source.responsibleWorkerDisplayName)
            || source.quantity <= 0
            || !float.IsFinite(source.requiredWork)
            || source.requiredWork <= 0f
            || !float.IsFinite(source.completedWork)
            || source.completedWork < 0f
            || source.completedWork > source.requiredWork
            || source.completedWork + WorkEpsilon < source.requiredWork
            || source.completedAbsoluteDay < 0
            || inputCommitted && !HasExactPhysicalInput(source)
            || waterApplied && (!float.IsFinite(source.waterBefore)
                || source.waterBefore < 0f
                || !float.IsFinite(source.waterAfter)
                || Math.Abs(
                    source.waterBefore + source.quantity - source.waterAfter)
                    > WaterEpsilon))
        {
            throw new ArgumentException(
                "An exact completed crop water-refill fact is required.",
                nameof(source));
        }

        PlotId = plotId;
        snapshot = source.DeepClone();
        if (snapshot.phase == CropWaterRefillPhase.LedgerCommitted)
            snapshot.phase = CropWaterRefillPhase.OutcomePublished;
        snapshot.publicationKey = string.Empty;
        snapshot.gameplayOutcomeId = string.Empty;
        snapshot.gameplayOutcomeDigest = string.Empty;
        snapshot.failureReason = string.Empty;
    }

    public BuildingInstanceId PlotId { get; }

    public CropWaterRefillSaveData Snapshot() => snapshot.DeepClone();

    private static string FormatOperationId(
        BuildingInstanceId plotId,
        int sequence) => OperationPrefix + plotId.Value + ":"
        + sequence.ToString("D6", CultureInfo.InvariantCulture);

    private static bool HasExactPhysicalInput(CropWaterRefillSaveData source)
    {
        IReadOnlyList<string> stackIds = source.sourceStackIds;
        return IsCanonicalRequired(source.commitId)
            && stackIds != null
            && stackIds.Count > 0
            && stackIds.All(IsCanonicalRequired)
            && stackIds.Distinct(StringComparer.Ordinal).Count() == stackIds.Count
            && stackIds.SequenceEqual(
                stackIds.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal)
            && source.inputQuantity == source.quantity
            && source.inputMassGrams > 0L
            && IsCanonicalRequired(source.physicalRequestFingerprint)
            && source.physicalOutcomeOwnerRevision > 0L;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
