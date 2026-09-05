using System.Collections.Generic;

public enum DurableFacilityEquipmentSlotCheckpointGcStatus
{
    Applied = 1,
    AlreadyApplied = 2,
    Deferred = 3,
    Corruption = 4
}

public readonly struct DurableFacilityEquipmentSlotCheckpointGcResult
{
    public DurableFacilityEquipmentSlotCheckpointGcResult(
        DurableFacilityEquipmentSlotCheckpointGcStatus status,
        string message)
    {
        Status = status;
        Message = message ?? string.Empty;
    }

    public DurableFacilityEquipmentSlotCheckpointGcStatus Status { get; }
    public string Message { get; }
}

public interface IDurableFacilityEquipmentSlotCheckpointGcCoordinator
{
    DurableFacilityEquipmentSlotCheckpointGcResult OnDurableSaveCommitted(
        string slotId,
        string serializedByteDigest);
}

public interface IDurableFacilityEquipmentSlotCheckpointGcCandidate
{
    IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> ClosedSlots { get; }
}

public interface IDurableFacilityEquipmentSlotCheckpointGcPort
{
    bool TryPrepareCheckpointGarbageCollection(
        out IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate,
        out string failureReason);

    bool TryPublishCheckpointGarbageCollection(
        IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate,
        out string failureReason);

    void RollbackCheckpointGarbageCollection(
        IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate);

    void CompleteCheckpointGarbageCollection(
        IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate);
}
