using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IItemTransferService
{
    bool TryReleaseExactOwnedWholeStack(
        ExactOwnedItemReleaseRequest request,
        out PhysicalItemRelocationReceipt receipt,
        out string failureReason);

    bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure);

    bool TryRequestStackDelivery(
        ItemStackId stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure);

    bool TryConsumeStackQuantity(
        ItemStackId stackId,
        int quantity,
        out WorldItemStackSnapshot consumed,
        out DomainFailure failure);

    bool TrySpawnItem(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned);

    bool TrySpawnItemWithComponents(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int spawned);

    bool TryRouteFacilityOutput(
        string sourceDestinationId,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int routed,
        out DomainFailure failure);

    void PrioritizeDestination(string destinationId);
    int ReleaseDestination(string destinationId, Vector2Int releasePosition);
    int RemoveDestination(
        string destinationId,
        params WorldItemStackState[] states);

    bool TryInspectStackForTransit(
        ItemStackId stackId,
        out ItemTransitStackSnapshot stack);

    void CopyLoadableTransitStackIds(
        Vector2Int position,
        List<ItemStackId> destination);

    bool TryBeginTransit(
        ItemStackId stackId,
        Vector2Int expectedPosition,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack,
        out DomainFailure failure);

    bool TryGetTransitStack(
        ItemStackId stackId,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack);

    bool TryCompleteTransit(
        ItemStackId stackId,
        string transitOwnerId,
        WorldItemStackState destinationState,
        Vector2Int destinationPosition,
        string destinationId,
        out DomainFailure failure);

    bool TryCompleteTransitToFacilityBuffer(
        ItemStackId stackId,
        string transitOwnerId,
        Vector2Int destinationPosition,
        string destinationId,
        out FacilityBufferMassAdmissionReceipt receipt,
        out DomainFailure failure);

    bool TryCompleteTransitToWarehouse(
        ItemStackId stackId,
        string transitOwnerId,
        IWarehouseFacility warehouse,
        out WarehouseMassAdmissionReceipt receipt,
        out DomainFailure failure);

    bool TryPickupReservedStackQuantity(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemReservedStackQuantity reservation,
        out int pickedUp,
        out string failureReason);

    bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        out string failureReason);

    bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason);

    bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        out string failureReason);

    bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason);

    bool TryConsumeFacilityBuffer(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs,
        out string failureReason);

    bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason);

    int ReleaseQuantityReservationsByOwner(
        string ownerOperationId,
        ItemReservationReleaseReason reason);

    bool RenewQuantityReservation(
        string leaseId,
        double requestedUntilGameSeconds,
        out DomainFailure failure);

    bool TryReserveAvailableStackForDirectPickup(
        string ownerCharacterId,
        string ownerOperationId,
        ItemReservationPurpose purpose,
        string stackId,
        int quantity,
        out ItemQuantityLease lease,
        out DomainFailure failure);

    bool ReleaseQuantityReservation(
        string leaseId,
        ItemReservationReleaseReason reason);
}

internal interface IMemoryErasureSealStoredCargoReturnService
{
    bool TryReturnCarriedMemoryErasureSealToStoredSource(
        CharacterActor actor,
        CharacterCarryInventory carry,
        string ownerOperationId,
        out string failureReason);
}

public interface IReservedRetailStockTransferService
{
    bool TryTakeReservedRetailLots(
        string leaseId,
        int quantity,
        int saleItemId,
        string expectedItemDefinitionId,
        string operationId,
        CharacterCarryInventory carryInventory,
        out ReservedRetailStockTransferReceipt receipt,
        out DomainFailure failure);

    bool TryRollbackRetailTransfer(
        ReservedRetailStockTransferReceipt receipt,
        out DomainFailure failure);
}

public interface ICarriedItemDropService
{
    bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        out string failureReason);
    bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason);
    bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IReadOnlyCollection<string> ownerOperationIds,
        HaulCarryDropContext context,
        out string failureReason);
}

