using System;

[Serializable]
public sealed class CertifiedSeedCompletionFactData
{
    public string actionId = string.Empty;
    public string orderId = string.Empty;
    public int orderSequence;
    public string destinationId = string.Empty;
    public string facilityInstanceId = string.Empty;
    public string cropId = string.Empty;
    public string facilityDisplayName = string.Empty;
    public string cropDisplayName = string.Empty;
    public string outputItemDefinitionId = string.Empty;
    public string outputItemDisplayName = string.Empty;
    public int outputQuantity;
    public long outputMassGrams;
    public string outputOutcomeFingerprint = string.Empty;
    public int absoluteDay;
    public long ownerRevision;

    public bool IsEmpty => actionId == string.Empty
        && orderId == string.Empty
        && orderSequence == 0
        && destinationId == string.Empty
        && facilityInstanceId == string.Empty
        && cropId == string.Empty
        && facilityDisplayName == string.Empty
        && cropDisplayName == string.Empty
        && outputItemDefinitionId == string.Empty
        && outputItemDisplayName == string.Empty
        && outputQuantity == 0
        && outputMassGrams == 0L
        && outputOutcomeFingerprint == string.Empty
        && absoluteDay == 0
        && ownerRevision == 0L;

    public CertifiedSeedCompletionFactData Clone() =>
        (CertifiedSeedCompletionFactData)MemberwiseClone();
}
