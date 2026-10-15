using System;
using System.Globalization;

[Serializable]
public sealed class CropIrrigationPublicationSaveData
{
    public bool pending;
    public int operationSequence;
    public int absoluteDay;
    public int locationX;
    public int locationY;
    public string operationId = string.Empty;
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string fingerprint = string.Empty;
    public string failureReason = string.Empty;
    public string plotId = string.Empty;
    public string irrigatorId = string.Empty;
    public string plotDisplayName = string.Empty;
    public string irrigatorDisplayName = string.Empty;
    public string networkId = string.Empty;
    public float quantity;
    public float waterBefore;
    public float waterAfter;
    public float networkWaterBefore;
    public float networkWaterAfter;
    public WorldWaterQuality consumedQuality;

    public bool IsEmpty => !pending && operationSequence == 0 && absoluteDay == 0
        && locationX == 0 && locationY == 0 && quantity == 0f
        && waterBefore == 0f && waterAfter == 0f && networkWaterBefore == 0f
        && networkWaterAfter == 0f && consumedQuality == default
        && string.IsNullOrEmpty(operationId) && string.IsNullOrEmpty(publicationKey)
        && string.IsNullOrEmpty(publicationId) && string.IsNullOrEmpty(fingerprint)
        && string.IsNullOrEmpty(failureReason) && string.IsNullOrEmpty(plotId)
        && string.IsNullOrEmpty(irrigatorId) && string.IsNullOrEmpty(plotDisplayName)
        && string.IsNullOrEmpty(irrigatorDisplayName) && string.IsNullOrEmpty(networkId);

    public CropIrrigationPublicationSaveData DeepClone() =>
        (CropIrrigationPublicationSaveData)MemberwiseClone();
}

public sealed class CropIrrigationSuppliedFact
{
    private const string OperationPrefix = "crop-irrigation:";
    private const float WaterEpsilon = 0.0001f;
    private readonly CropIrrigationPublicationSaveData snapshot;

    public CropIrrigationSuppliedFact(
        CropIrrigationPublicationSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var plotId = new BuildingInstanceId(source.plotId);
        var irrigatorId = new BuildingInstanceId(source.irrigatorId);
        if (!source.pending
            || source.operationSequence < 0
            || source.operationSequence == int.MaxValue
            || source.absoluteDay < 0
            || !IsCanonical(plotId, source.plotId)
            || !IsCanonical(irrigatorId, source.irrigatorId)
            || !IsCanonicalRequired(source.networkId)
            || !string.Equals(
                source.operationId,
                FormatOperationId(plotId, source.operationSequence),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(source.plotDisplayName)
            || string.IsNullOrWhiteSpace(source.irrigatorDisplayName)
            || !float.IsFinite(source.quantity)
            || source.quantity <= 0f
            || !IsFiniteNonNegative(source.waterBefore)
            || !IsFiniteNonNegative(source.waterAfter)
            || !IsFiniteNonNegative(source.networkWaterBefore)
            || !IsFiniteNonNegative(source.networkWaterAfter)
            || Math.Abs(
                source.waterBefore + source.quantity - source.waterAfter)
                > WaterEpsilon
            || Math.Abs(
                source.networkWaterBefore
                - source.quantity
                - source.networkWaterAfter) > WaterEpsilon
            || source.consumedQuality != WorldWaterQuality.Clean)
        {
            throw new ArgumentException(
                "An exact completed clean-water irrigation fact is required.",
                nameof(source));
        }

        snapshot = source.DeepClone();
        snapshot.publicationKey = string.Empty;
        snapshot.publicationId = string.Empty;
        snapshot.fingerprint = string.Empty;
        snapshot.failureReason = string.Empty;
    }

    public CropIrrigationPublicationSaveData Snapshot() =>
        snapshot.DeepClone();

    private static string FormatOperationId(
        BuildingInstanceId plotId,
        int sequence) => OperationPrefix + plotId.Value + ":"
        + sequence.ToString("D6", CultureInfo.InvariantCulture);

    private static bool IsCanonical(
        BuildingInstanceId id,
        string source) => id.IsValid
        && string.Equals(id.Value, source, StringComparison.Ordinal);

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsFiniteNonNegative(float value) =>
        float.IsFinite(value) && value >= 0f;
}
