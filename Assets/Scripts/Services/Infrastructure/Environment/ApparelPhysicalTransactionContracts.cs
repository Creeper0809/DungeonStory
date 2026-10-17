using System;
using DungeonStory.Foundation;

public enum ApparelPhysicalTransactionStatus
{
    Completed = 0,
    WaitingForOutputSpace = 1,
    PendingFinalization = 2,
    Conflict = 3
}

public readonly struct ApparelPhysicalTransactionResult
{
    public ApparelPhysicalTransactionResult(
        ApparelPhysicalTransactionStatus status,
        string outputStackId,
        string outputInstanceId,
        long inputMassGrams,
        long outputMassGrams,
        string failureReason)
    {
        Status = status;
        OutputStackId = outputStackId ?? string.Empty;
        OutputInstanceId = outputInstanceId ?? string.Empty;
        InputMassGrams = Math.Max(0L, inputMassGrams);
        OutputMassGrams = Math.Max(0L, outputMassGrams);
        FailureReason = failureReason ?? string.Empty;
    }

    public ApparelPhysicalTransactionStatus Status { get; }
    public string OutputStackId { get; }
    public string OutputInstanceId { get; }
    public long InputMassGrams { get; }
    public long OutputMassGrams { get; }
    public string FailureReason { get; }
    public bool IsCompleted => Status == ApparelPhysicalTransactionStatus.Completed;
}

public interface IApparelPhysicalTransaction
{
    bool TryValidateCraftOutputCapability(
        ApparelWorkOrderSaveData order,
        string expectedOutputItemId,
        out DomainFailure failure);

    ApparelPhysicalTransactionResult ExecuteCraftOrResume(
        ApparelWorkOrderSaveData order,
        BuildableObject facility,
        string outputItemId,
        ItemInstanceComponentSaveData frozenOutputComponent,
        bool markForSale,
        string makerCharacterId,
        string makerDisplayName,
        CraftsmanshipQualityTier quality,
        bool rejectedBelowMinimum);

    ApparelPhysicalTransactionResult ExecuteRejectedDismantleOrResume(
        ApparelWorkOrderSaveData order,
        BuildableObject facility,
        string recoveryItemId) => new(
            ApparelPhysicalTransactionStatus.Conflict,
            string.Empty,
            string.Empty,
            0L,
            0L,
            "apparel-rejected-physical-capability-unavailable");
}

/// <summary>
/// Composes Apparel's owner state with the existing Items-owned pending input,
/// facility gram admission, and atomic full-batch publication authorities. The
/// work-order record is the durable cross-aggregate join; this service never
/// uses direct spawn/delete rollback.
/// </summary>
