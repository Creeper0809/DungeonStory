using System;
using System.Collections.Generic;

public enum CertifiedSeedOrderPhase
{
    Planned = 0,
    InputCommitted = 1,
    OutputPublished = 2
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
    public CertifiedSeedOrderPhase phase;
    public CropPhysicalCommitSaveData pendingInput = new();
    public SeedLotState certifiedSeedLot;
    public string outputOperationId = string.Empty;
    public string outputCommitId = string.Empty;

    public CertifiedSeedOrderSaveData DeepClone() => new()
    {
        orderId = orderId ?? string.Empty,
        orderSequence = orderSequence,
        actionId = actionId ?? string.Empty,
        facilityInstanceId = facilityInstanceId ?? string.Empty,
        cropId = cropId ?? string.Empty,
        destinationId = destinationId ?? string.Empty,
        phase = phase,
        pendingInput = pendingInput?.DeepClone() ?? new CropPhysicalCommitSaveData(),
        certifiedSeedLot = certifiedSeedLot?.Clone(),
        outputOperationId = outputOperationId ?? string.Empty,
        outputCommitId = outputCommitId ?? string.Empty
    };
}

[Serializable]
public sealed class CertifiedSeedWorldSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int nextOrderSequence;
    public List<CertifiedSeedOrderSaveData> orders = new();
}

public sealed class CertifiedSeedRestoreCandidate
{
    internal CertifiedSeedRestoreCandidate(
        int nextOrderSequence,
        IReadOnlyList<CertifiedSeedOrderSaveData> orders)
    {
        NextOrderSequence = nextOrderSequence;
        Orders = orders ?? throw new ArgumentNullException(nameof(orders));
    }

    internal int NextOrderSequence { get; }
    internal IReadOnlyList<CertifiedSeedOrderSaveData> Orders { get; }
}

public interface ICertifiedSeedPersistence
{
    CertifiedSeedWorldSaveData Capture();
    CertifiedSeedRestoreCandidate BuildRestore(CertifiedSeedWorldSaveData snapshot);
    void Restore(CertifiedSeedRestoreCandidate candidate);
}
