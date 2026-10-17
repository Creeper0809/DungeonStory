using System.Collections.Generic;
using UnityEngine;

public interface IEquipmentPhysicalItemGateway
{
    bool SpawnItemAt(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned);
    bool SpawnItemAtWithComponents(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int spawned);
    bool SpawnExistingUniqueItemAt(
        string itemId,
        ItemInstanceId itemInstanceId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId);
    bool TryAbsorbUniqueItemStack(
        string stackId,
        ItemInstanceId expectedInstanceId);
    bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
    IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();
    bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason);
    bool DeleteStack(string stackId);
    bool TryConsumeStackQuantity(
        string stackId,
        int quantity,
        out WorldItemStackSnapshot consumed);
    bool TryCommitBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);
    bool TryCommitPendingBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);
    bool TryGetPendingBatchPhysicalDisposition(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt);
    bool AcknowledgeBatchPhysicalDisposition(
        string commitId,
        out string failureReason);
    bool TrySetInstanceComponent(
        string stackId,
        ItemInstanceComponentSaveData component);
    bool TryRemoveInstanceComponent(
        string stackId,
        string componentTypeId);
    int ReleaseStacksByDestination(
        string destinationId,
        Vector2Int releasePosition);
}
