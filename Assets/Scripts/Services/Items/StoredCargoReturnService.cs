using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class StoredCargoReturnService
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly ICharacterIdRegistry characterIdRegistry;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly WorldItemRepository repository;
    private readonly IWarehouseMassAdmissionService warehouseMassAdmission;
    private readonly IWarehouseWorldQuery worldRegistry;

    internal StoredCargoReturnService(
        IDungeonItemCatalogProvider catalogProvider,
        ICharacterIdRegistry characterIdRegistry,
        IItemMarkerPresenter markerPresenter,
        IItemQuantityReservationService quantityReservations,
        WorldItemRepository repository,
        IWarehouseMassAdmissionService warehouseMassAdmission,
        IWarehouseWorldQuery worldRegistry)
    {
        this.catalogProvider = catalogProvider;
        this.characterIdRegistry = characterIdRegistry;
        this.markerPresenter = markerPresenter;
        this.quantityReservations = quantityReservations;
        this.repository = repository;
        this.warehouseMassAdmission = warehouseMassAdmission;
        this.worldRegistry = worldRegistry;
    }

    [GameplayInternalOnly(
        "An active memory-erasure order returns its one exact carried unit to the preserved source stack and warehouse through mass admission.",
        "WorldItemStackRuntime")]
    public bool TryReturnCarriedMemoryErasureSealToStoredSource(
        CharacterActor actor,
        CharacterCarryInventory carry,
        string ownerOperationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string operationId = ownerOperationId?.Trim() ?? string.Empty;
        if (actor == null
            || carry == null
            || operationId.Length == 0
            || warehouseMassAdmission == null)
        {
            failureReason =
                "memory-erasure-seal-stored-return-invalid-request";
            return false;
        }

        string actorId = characterIdRegistry.GetOrAssignPersistentId(actor);
        quantityReservations.ReleaseByOwner(
            operationId,
            ItemReservationReleaseReason.Cancelled);
        if (quantityReservations.TryGetLeasesByOwner(
                operationId,
                out IReadOnlyList<ItemQuantityLease> remainingLeases)
            && remainingLeases.Count > 0)
        {
            failureReason =
                "memory-erasure-seal-stored-return-lease-still-active";
            return false;
        }
        CharacterCarriedItemSaveData[] owned = carry.Items
            .Where(value => value != null
                && value.quantity > 0
                && string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.itemId,
                    MemoryErasureSealItemRules.ItemId,
                    StringComparison.Ordinal))
            .ToArray();
        if (owned.Length != 1
            || owned[0].quantity != MemoryErasureSealItemRules.UseQuantity)
        {
            failureReason =
                "memory-erasure-seal-stored-return-cargo-mismatch";
            return false;
        }

        CharacterCarriedItemSaveData carriedMirror = owned[0];
        string carriedStackId = carriedMirror.carriedStackId?.Trim()
            ?? string.Empty;
        string sourceStackId = carriedMirror.sourceStackId?.Trim()
            ?? string.Empty;
        if (carriedStackId.Length == 0
            || sourceStackId.Length == 0
            || !repository.RecordsById.TryGetValue(
                carriedStackId,
                out WorldItemStackRecord carried)
            || carried == null
            || carried.state != WorldItemStackState.Carried
            || carried.quantity != MemoryErasureSealItemRules.UseQuantity
            || !string.Equals(
                carried.destinationId,
                actorId,
                StringComparison.Ordinal)
            || !string.Equals(
                carried.itemId,
                MemoryErasureSealItemRules.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                carried.itemInstanceId,
                carriedMirror.itemInstanceId,
                StringComparison.Ordinal)
            || carried.wasteOrigin != carriedMirror.wasteOrigin
            || !Mathf.Approximately(
                carried.contamination,
                carriedMirror.contamination)
            || !string.Equals(
                ItemStackSignature.Create(
                    carried.itemId,
                    carried.components),
                carriedMirror.GetStackSignature(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(
                carried.sourceStorageDestinationId))
        {
            failureReason =
                "memory-erasure-seal-stored-return-physical-mismatch";
            return false;
        }

        string sourceStorageDestinationId =
            carried.sourceStorageDestinationId.Trim();
        IWarehouseFacility sourceWarehouse = WorldItemTransferLocationRules.GetWarehouses(worldRegistry)
            .FirstOrDefault(candidate =>
            {
                try
                {
                    return string.Equals(
                        WorldItemTransferLocationRules.GetWarehouseStorageDestinationId(candidate),
                        sourceStorageDestinationId,
                        StringComparison.Ordinal);
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            });
        Vector2Int warehousePosition =
            WorldItemTransferLocationRules.ResolveWarehouseStoragePosition(sourceWarehouse);
        Vector2Int actorPosition = WorldItemTransferLocationRules.ResolveActorGridPosition(actor);
        if (!WorldItemTransferLocationRules.IsExactLiveWarehouseOwner(
                sourceWarehouse,
                sourceStorageDestinationId,
                warehousePosition)
            || Mathf.Abs(actorPosition.x - warehousePosition.x)
                + Mathf.Abs(actorPosition.y - warehousePosition.y) > 1
            || !catalogProvider.TryGetDefinition(
                carried.itemId,
                out DungeonItemDefinition definition)
            || !sourceWarehouse.Inventory.Accepts(definition.StockCategory))
        {
            failureReason =
                "memory-erasure-seal-stored-return-source-unavailable";
            return false;
        }

        bool sourceIdentityTransferred = string.Equals(
            carriedStackId,
            sourceStackId,
            StringComparison.Ordinal);
        WorldItemStackRecord source = null;
        if (!sourceIdentityTransferred
            && (!repository.RecordsById.TryGetValue(sourceStackId, out source)
                || source == null
                || source.state != WorldItemStackState.Stored
                || source.position != warehousePosition
                || !string.Equals(
                    source.destinationId,
                    sourceStorageDestinationId,
                    StringComparison.Ordinal)
                || !string.IsNullOrEmpty(
                    source.sourceStorageDestinationId)
                || source.reservedQuantity != 0
                || !string.IsNullOrEmpty(source.reservedByPersistentId)
                || source.quantity <= 0
                || source.quantity >= definition.MaxStack
                || !string.Equals(
                    source.itemId,
                    carried.itemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    source.itemInstanceId,
                    carried.itemInstanceId,
                    StringComparison.Ordinal)
                || source.wasteOrigin != carried.wasteOrigin
                || !Mathf.Approximately(
                    source.contamination,
                    carried.contamination)
                || !string.Equals(
                    ItemStackSignature.Create(
                        source.itemId,
                        source.components),
                    ItemStackSignature.Create(
                        carried.itemId,
                        carried.components),
                    StringComparison.Ordinal)))
        {
            failureReason =
                "memory-erasure-seal-stored-return-source-stack-changed";
            return false;
        }

        BuildingInstanceId warehouseId =
            sourceWarehouse.PersistentInstanceId;
        long capacityRevision = warehouseMassAdmission
            .GetWarehouseCapacityRevision(warehouseId);
        string admissionOperationId = operationId
            + ":stored-return:"
            + sourceStackId
            + ":revision:"
            + capacityRevision.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        ItemDefinitionId itemId = new(carried.itemId);
        WarehouseMassAdmissionRequest request = new(
            warehouseId,
            admissionOperationId,
            itemId,
            carried.itemInstanceId,
            ItemReservationSignature.Create(
                carried.itemId,
                carried.components),
            MemoryErasureSealItemRules.UseQuantity,
            capacityRevision,
            warehouseMassAdmission.CatalogRevision,
            repository.ItemStackVersion,
            warehouseMassAdmission.PrepareMassSubject(
                itemId,
                carried.itemInstanceId,
                carried.components));
        if (!warehouseMassAdmission.TryReserve(
                request,
                out WarehouseMassAdmissionToken token,
                out DomainFailure admissionFailure))
        {
            failureReason = admissionFailure.ToString();
            return false;
        }
        if (token.AcceptedQuantity != MemoryErasureSealItemRules.UseQuantity)
        {
            warehouseMassAdmission.TryRelease(
                token.TokenId,
                WarehouseMassAdmissionReleaseReason.TransactionRollback,
                out _);
            failureReason =
                "memory-erasure-seal-stored-return-admission-partial";
            return false;
        }

        CharacterCarryInventorySaveData carryBefore = carry.Capture();
        Vector2Int carriedPosition = carried.position;
        WorldItemStackState carriedState = carried.state;
        string carriedDestinationId = carried.destinationId;
        string carriedSourceStorageDestinationId =
            carried.sourceStorageDestinationId;
        bool carriedHasDestinationPosition =
            carried.hasDestinationPosition;
        Vector2Int carriedDestinationPosition =
            carried.destinationPosition;
        string carriedAggregationCohortId =
            carried.aggregationCohortId;
        string carriedReservedByPersistentId =
            carried.reservedByPersistentId;
        int carriedReservedQuantity = carried.reservedQuantity;
        bool physicalMutated = false;
        try
        {
            if (!carry.TryConsumeCarriedStack(
                    carriedStackId,
                    MemoryErasureSealItemRules.ItemId,
                    MemoryErasureSealItemRules.UseQuantity))
            {
                warehouseMassAdmission.TryRelease(
                    token.TokenId,
                    WarehouseMassAdmissionReleaseReason.TransactionRollback,
                    out _);
                failureReason =
                    "memory-erasure-seal-stored-return-carry-debit-failed";
                return false;
            }

            if (sourceIdentityTransferred)
            {
                repository.Relocate(carried, warehousePosition);
                carried.state = WorldItemStackState.Stored;
                carried.destinationId = sourceStorageDestinationId;
                carried.sourceStorageDestinationId = string.Empty;
                carried.hasDestinationPosition = false;
                carried.destinationPosition = default;
                carried.aggregationCohortId = string.Empty;
                carried.reservedByPersistentId = string.Empty;
                carried.reservedQuantity = 0;
                repository.MarkChanged();
            }
            else
            {
                source.quantity = checked(
                    source.quantity
                    + MemoryErasureSealItemRules.UseQuantity);
                repository.Remove(carried);
            }
            physicalMutated = true;
            markerPresenter.RefreshAt(carriedPosition);
            markerPresenter.RefreshAt(warehousePosition);

            if (warehouseMassAdmission.TryCommit(
                    token.TokenId,
                    admissionOperationId + ":commit",
                    out WarehouseMassAdmissionReceipt receipt,
                    out admissionFailure)
                && receipt.CommittedQuantity
                    == MemoryErasureSealItemRules.UseQuantity)
            {
                return true;
            }

            failureReason = admissionFailure.IsFailure
                ? admissionFailure.ToString()
                : "memory-erasure-seal-stored-return-commit-invalid";
        }
        catch (Exception exception)
        {
            failureReason =
                "memory-erasure-seal-stored-return-exception:"
                + exception.Message;
        }

        if (physicalMutated)
        {
            if (sourceIdentityTransferred)
            {
                repository.Relocate(carried, carriedPosition);
                carried.state = carriedState;
                carried.destinationId = carriedDestinationId;
                carried.sourceStorageDestinationId =
                    carriedSourceStorageDestinationId;
                carried.hasDestinationPosition =
                    carriedHasDestinationPosition;
                carried.destinationPosition =
                    carriedDestinationPosition;
                carried.aggregationCohortId = carriedAggregationCohortId;
                carried.reservedByPersistentId =
                    carriedReservedByPersistentId;
                carried.reservedQuantity = carriedReservedQuantity;
                repository.MarkChanged();
            }
            else
            {
                source.quantity = checked(
                    source.quantity
                    - MemoryErasureSealItemRules.UseQuantity);
                repository.Add(carried);
            }
            markerPresenter.RefreshAt(warehousePosition);
            markerPresenter.RefreshAt(carriedPosition);
        }
        carry.Restore(carryBefore);
        if (warehouseMassAdmission.TryGetStatus(
                token.TokenId,
                out WarehouseMassAdmissionStatusSnapshot status)
            && status.Status == WarehouseMassAdmissionTokenStatus.Reserved)
        {
            warehouseMassAdmission.TryRelease(
                token.TokenId,
                WarehouseMassAdmissionReleaseReason.TransactionRollback,
                out _);
        }
        return false;
    }
}
