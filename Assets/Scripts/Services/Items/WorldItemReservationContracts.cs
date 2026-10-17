using System;
using UnityEngine;

public readonly struct WorldItemReservedStackQuantity
{
    public WorldItemReservedStackQuantity(
        string stackId,
        string itemId,
        int quantity,
        Vector2Int position,
        WorldItemHaulDestinationKind destinationKind,
        string destinationId,
        string leaseId = "",
        string ownerOperationId = "")
    {
        StackId = stackId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
        Position = position;
        DestinationKind = destinationKind;
        DestinationId = destinationId ?? string.Empty;
        LeaseId = leaseId?.Trim() ?? string.Empty;
        OwnerOperationId = ownerOperationId?.Trim() ?? string.Empty;
    }

    public string StackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public Vector2Int Position { get; }
    public WorldItemHaulDestinationKind DestinationKind { get; }
    public string DestinationId { get; }
    public string LeaseId { get; }
    public string OwnerOperationId { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(StackId)
        && Quantity > 0
        && (!string.IsNullOrWhiteSpace(LeaseId)
            || string.IsNullOrWhiteSpace(OwnerOperationId));
}

public readonly struct HaulDeliveryPathFailureSnapshot
{
    public HaulDeliveryPathFailureSnapshot(
        string ownerOperationId,
        string sourceStackId,
        string destinationId,
        string itemId,
        AIActionFailureKind failureKind)
    {
        OwnerOperationId = ownerOperationId?.Trim() ?? string.Empty;
        SourceStackId = sourceStackId?.Trim() ?? string.Empty;
        DestinationId = destinationId?.Trim() ?? string.Empty;
        ItemId = itemId?.Trim() ?? string.Empty;
        FailureKind = failureKind;
    }

    public string OwnerOperationId { get; }
    public string SourceStackId { get; }
    public string DestinationId { get; }
    public string ItemId { get; }
    public AIActionFailureKind FailureKind { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(SourceStackId)
        && !string.IsNullOrWhiteSpace(DestinationId)
        && !string.IsNullOrWhiteSpace(ItemId)
        && FailureKind == AIActionFailureKind.NoPath;
}

public interface IHaulDeliveryPathFailureQuery
{
    bool TryGetLastDeliveryPathFailure(
        out HaulDeliveryPathFailureSnapshot snapshot);
}

public enum HaulInterruptionDisposition
{
    ReleaseUnpickedAndRetainCarriedForReplan = 0,
    ReleaseUnpickedAndDropCarriedAtActor = 1
}

public readonly struct HaulCarryDropContext
{
    public HaulCarryDropContext(
        string carrierPersistentId,
        WorldItemCarryInterruptionKind interruptionKind,
        double droppedAtGameTime,
        double recoveryDeadlineGameTime)
    {
        CarrierPersistentId = carrierPersistentId?.Trim() ?? string.Empty;
        InterruptionKind = interruptionKind;
        DroppedAtGameTime = droppedAtGameTime;
        RecoveryDeadlineGameTime = recoveryDeadlineGameTime;
    }

    public string CarrierPersistentId { get; }
    public WorldItemCarryInterruptionKind InterruptionKind { get; }
    public double DroppedAtGameTime { get; }
    public double RecoveryDeadlineGameTime { get; }
    public bool IsValid => !string.IsNullOrEmpty(CarrierPersistentId)
        && InterruptionKind is WorldItemCarryInterruptionKind.Downed
            or WorldItemCarryInterruptionKind.Dead
            or WorldItemCarryInterruptionKind.Disabled
        && !double.IsNaN(DroppedAtGameTime)
        && !double.IsInfinity(DroppedAtGameTime)
        && DroppedAtGameTime >= 0d
        && !double.IsNaN(RecoveryDeadlineGameTime)
        && !double.IsInfinity(RecoveryDeadlineGameTime)
        && RecoveryDeadlineGameTime > DroppedAtGameTime;
}
