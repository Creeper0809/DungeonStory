using System;
using System.Globalization;

public sealed class CropSelectionChangedFact
{
    private const string OperationPrefix = "crop-selection:";
    private readonly CropSelectionCommandSaveData snapshot;

    public CropSelectionChangedFact(
        BuildingInstanceId plotId,
        CropSelectionCommandSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (!plotId.IsValid
            || source.operationSequence < 0
            || source.operationSequence == int.MaxValue
            || source.completedAbsoluteDay < 0
            || !string.Equals(
                source.operationId,
                FormatOperationId(plotId, source.operationSequence),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(source.plotDisplayName)
            || string.IsNullOrWhiteSpace(source.cropDisplayName)
            || !IsCanonicalRequired(source.cropAfter)
            || !IsEmptyOrCanonical(source.cropBefore)
            || !float.IsFinite(source.waterBefore)
            || source.waterBefore < 0f
            || source.waterAfter != 0f)
        {
            throw new ArgumentException(
                "An exact mutation-applied crop selection is required.",
                nameof(source));
        }

        PlotId = plotId;
        snapshot = Copy(source);
    }

    public BuildingInstanceId PlotId { get; }

    public CropSelectionCommandSaveData Snapshot() => Copy(snapshot);

    private static CropSelectionCommandSaveData Copy(
        CropSelectionCommandSaveData source) => new()
    {
        phase = CropCommandOutcomePhase.MutationApplied,
        operationSequence = source.operationSequence,
        operationId = source.operationId,
        publicationKey = string.Empty,
        plotDisplayName = source.plotDisplayName,
        cropDisplayName = source.cropDisplayName,
        locationX = source.locationX,
        locationY = source.locationY,
        cropBefore = source.cropBefore ?? string.Empty,
        cropAfter = source.cropAfter,
        waterBefore = source.waterBefore,
        waterAfter = source.waterAfter,
        completedAbsoluteDay = source.completedAbsoluteDay,
        gameplayOutcomeId = string.Empty,
        gameplayOutcomeDigest = string.Empty,
        failureReason = string.Empty
    };

    private static string FormatOperationId(
        BuildingInstanceId plotId,
        int sequence) => OperationPrefix + plotId.Value + ":"
        + sequence.ToString("D6", CultureInfo.InvariantCulture);

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsEmptyOrCanonical(string value) =>
        string.IsNullOrEmpty(value) || IsCanonicalRequired(value);
}

public sealed class GoldenHarvestScheduledFact
{
    private const string OperationPrefix = "golden-harvest-schedule:";
    private readonly CropGoldenHarvestScheduleSaveData snapshot;

    public GoldenHarvestScheduledFact(
        BuildingInstanceId plotId,
        CropGoldenHarvestScheduleSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (!plotId.IsValid
            || source.operationSequence < 0
            || source.operationSequence == int.MaxValue
            || source.completedAbsoluteDay < 0
            || !string.Equals(
                source.operationId,
                FormatOperationId(plotId, source.operationSequence),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(source.plotDisplayName)
            || string.IsNullOrWhiteSpace(source.cropDisplayName)
            || !IsCanonicalRequired(source.cropId)
            || !IsCanonicalRequired(source.harvesterId)
            || string.IsNullOrWhiteSpace(source.harvesterDisplayName)
            || !string.IsNullOrEmpty(source.harvesterBefore)
            || !string.Equals(
                source.harvesterAfter,
                source.harvesterId,
                StringComparison.Ordinal)
            || source.attemptBefore != source.operationSequence
            || source.attemptAfter != source.attemptBefore + 1)
        {
            throw new ArgumentException(
                "An exact mutation-applied golden-harvest schedule is required.",
                nameof(source));
        }

        PlotId = plotId;
        snapshot = Copy(source);
    }

    public BuildingInstanceId PlotId { get; }

    public CropGoldenHarvestScheduleSaveData Snapshot() => Copy(snapshot);

    private static CropGoldenHarvestScheduleSaveData Copy(
        CropGoldenHarvestScheduleSaveData source) => new()
    {
        phase = CropCommandOutcomePhase.MutationApplied,
        operationSequence = source.operationSequence,
        operationId = source.operationId,
        publicationKey = string.Empty,
        plotDisplayName = source.plotDisplayName,
        cropDisplayName = source.cropDisplayName,
        locationX = source.locationX,
        locationY = source.locationY,
        cropId = source.cropId,
        harvesterId = source.harvesterId,
        harvesterDisplayName = source.harvesterDisplayName,
        harvesterBefore = string.Empty,
        harvesterAfter = source.harvesterAfter,
        attemptBefore = source.attemptBefore,
        attemptAfter = source.attemptAfter,
        completedAbsoluteDay = source.completedAbsoluteDay,
        gameplayOutcomeId = string.Empty,
        gameplayOutcomeDigest = string.Empty,
        failureReason = string.Empty
    };

    private static string FormatOperationId(
        BuildingInstanceId plotId,
        int sequence) => OperationPrefix + plotId.Value + ":"
        + sequence.ToString("D6", CultureInfo.InvariantCulture);

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
