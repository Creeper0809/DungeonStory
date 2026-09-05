using System;
using System.Collections.Generic;

public enum CertifiedSeedOrderPhase
{
    Planned = 0,
    InputCommitted = 1,
    OutputPublished = 2,
    OutputRestoredAwaitingInputAcknowledgement = 3,
    FacilityDestroyedLossPending = 4,
    InputCommittedAwaitingDestinationRetirement = 5
}

[Serializable]
public sealed class CertifiedSeedOrderSaveData
{
    public string orderId = string.Empty;
    public int orderSequence;
    public string actionId = string.Empty;
    public string facilityInstanceId = string.Empty;
    public string cropId = string.Empty;
    public string destinationId = string.Empty;
    public int destinationX;
    public int destinationY;
    public CertifiedSeedOrderPhase phase;
    public CropPhysicalCommitSaveData pendingInput = new();
    public SeedLotState certifiedSeedLot;
    public ProductionOutputCapabilitySaveData outputCapability = new();
    public ProductionDomainOutputPublicationSaveData outputPublication = new();

    public CertifiedSeedOrderSaveData DeepClone() => new()
    {
        orderId = orderId ?? string.Empty,
        orderSequence = orderSequence,
        actionId = actionId ?? string.Empty,
        facilityInstanceId = facilityInstanceId ?? string.Empty,
        cropId = cropId ?? string.Empty,
        destinationId = destinationId ?? string.Empty,
        destinationX = destinationX,
        destinationY = destinationY,
        phase = phase,
        pendingInput = pendingInput?.DeepClone() ?? new CropPhysicalCommitSaveData(),
        certifiedSeedLot = certifiedSeedLot?.Clone(),
        outputCapability = outputCapability?.Clone()
            ?? new ProductionOutputCapabilitySaveData(),
        outputPublication = outputPublication?.Clone()
            ?? new ProductionDomainOutputPublicationSaveData()
    };
}

[Serializable]
public sealed class CertifiedSeedWorldSaveData
{
    public const int CurrentVersion = 6;

    public int version = CurrentVersion;
    public int nextOrderSequence;
    public int lastProcessedOperatingDay;
    public List<CertifiedSeedOrderSaveData> orders = new();
}

public sealed class CertifiedSeedRestoreCandidate
{
    internal CertifiedSeedRestoreCandidate(
        int nextOrderSequence,
        IReadOnlyList<CertifiedSeedOrderSaveData> orders,
        IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement>
            outputAcknowledgements = null,
        int lastProcessedOperatingDay = 0)
    {
        if (nextOrderSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(nextOrderSequence));
        if (lastProcessedOperatingDay < 0)
            throw new ArgumentOutOfRangeException(
                nameof(lastProcessedOperatingDay));
        NextOrderSequence = nextOrderSequence;
        LastProcessedOperatingDay = lastProcessedOperatingDay;
        Orders = orders ?? throw new ArgumentNullException(nameof(orders));
        OutputAcknowledgements = outputAcknowledgements
            ?? Array.Empty<ProductionDomainOutputRestoreAcknowledgement>();
    }

    internal int NextOrderSequence { get; }
    internal int LastProcessedOperatingDay { get; }
    internal IReadOnlyList<CertifiedSeedOrderSaveData> Orders { get; }
    internal IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement>
        OutputAcknowledgements { get; }
}

public interface ICertifiedSeedPersistence
{
    CertifiedSeedWorldSaveData Capture();
    CertifiedSeedRestoreCandidate BuildRestore(CertifiedSeedWorldSaveData snapshot);
    void Restore(CertifiedSeedRestoreCandidate candidate);
}
