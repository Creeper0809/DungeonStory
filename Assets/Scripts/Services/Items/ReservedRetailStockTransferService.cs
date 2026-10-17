using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>Owns reserved retail physical transfer and its exact rollback transaction.</summary>
internal sealed class ReservedRetailStockTransferService
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemHaulingSettingsProvider haulingSettingsProvider;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly IPhysicalItemMassQuery physicalMass;
    private readonly IItemQuantityLeaseMutation quantityLeaseMutations;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly WorldItemRepository repository;
    private readonly IRetailStockPhysicalRuntime retailStockPhysical;

    internal ReservedRetailStockTransferService(
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider haulingSettingsProvider,
        IItemMarkerPresenter markerPresenter,
        IPhysicalItemMassQuery physicalMass,
        IItemQuantityLeaseMutation quantityLeaseMutations,
        IItemQuantityReservationService quantityReservations,
        WorldItemRepository repository,
        IRetailStockPhysicalRuntime retailStockPhysical)
    {
        this.catalogProvider = catalogProvider;
        this.haulingSettingsProvider = haulingSettingsProvider;
        this.markerPresenter = markerPresenter;
        this.physicalMass = physicalMass;
        this.quantityLeaseMutations = quantityLeaseMutations;
        this.quantityReservations = quantityReservations;
        this.repository = repository;
        this.retailStockPhysical = retailStockPhysical;
    }

    public bool TryTakeReservedRetailLots(
        string leaseId,
        int quantity,
        int saleItemId,
        string expectedItemDefinitionId,
        string operationId,
        CharacterCarryInventory carryInventory,
        out ReservedRetailStockTransferReceipt receipt,
        out DomainFailure failure)
    {
        receipt = null;
        failure = DomainFailure.None;
        string expectedItemId = expectedItemDefinitionId ?? string.Empty;
        string normalizedOperation = operationId ?? string.Empty;
        if (saleItemId < 0
            || quantity <= 0
            || expectedItemId.Length == 0
            || normalizedOperation.Length == 0
            || carryInventory == null
            || !string.Equals(expectedItemId, expectedItemId.Trim(), StringComparison.Ordinal)
            || !string.Equals(normalizedOperation, normalizedOperation.Trim(), StringComparison.Ordinal)
            || !quantityReservations.Revalidate(
                leaseId,
                out ItemQuantityLease lease,
                out failure)
            || lease.remainingQuantity < quantity)
        {
            if (!failure.IsFailure)
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-invalid-request");
            }
            return false;
        }

        List<(WorldItemStackRecord record, int quantity)> removals = new();
        int remaining = quantity;
        foreach (ItemLeaseSlice slice in lease.slices)
        {
            if (slice == null || slice.quantity <= 0 || remaining <= 0)
            {
                continue;
            }
            if (!repository.RecordsById.TryGetValue(
                    slice.stackId,
                    out WorldItemStackRecord record)
                || record == null
                || !string.Equals(record.itemId, expectedItemId, StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-source-exact-lot-mismatch",
                    slice.stackId ?? string.Empty);
                return false;
            }

            int take = Mathf.Min(remaining, slice.quantity);
            if (!string.IsNullOrEmpty(record.itemInstanceId)
                && (record.quantity != 1 || take != 1))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-unique-quantity-invalid",
                    record.stackId);
                return false;
            }
            removals.Add((record, take));
            remaining -= take;
        }
        if (remaining > 0)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationQuantityUnavailable,
                lease.leaseId);
            return false;
        }

        List<RetailStockLotSnapshot> lots = new();
        List<RetailStockSourceUndo> undo = new();
        CharacterCarriedItemSaveData[] carriedPreview = carryInventory.Items
            .Where(item => item != null
                && item.quantity > 0
                && string.Equals(
                    item.ownerOperationId,
                    normalizedOperation,
                    StringComparison.Ordinal))
            .ToArray();
        if (carriedPreview.Sum(item => item.quantity) != quantity
            || carriedPreview.Any(item => !string.Equals(
                item.itemId,
                expectedItemId,
                StringComparison.Ordinal)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-carry-quantity-mismatch",
                normalizedOperation);
            return false;
        }
        if (removals.Any(entry =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    entry.record.components))
            || carriedPreview.Any(item =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    item.components)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-prepared-output-route-protected",
                normalizedOperation);
            return false;
        }
        foreach ((WorldItemStackRecord record, int take) in removals)
        {
            int exactCarriedQuantity = carriedPreview
                .Where(item => string.Equals(
                        item.sourceStackId,
                        record.stackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.itemInstanceId,
                        record.itemInstanceId,
                        StringComparison.Ordinal))
                .Sum(item => item.quantity);
            if (exactCarriedQuantity != take)
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-carry-lot-mismatch",
                    record.stackId);
                return false;
            }
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                physicalMass,
                (ItemDefinitionId)record.itemId,
                record.itemInstanceId,
                record.components);
            long unitMassGrams = physicalMass.GetStackUnitMass(
                (ItemDefinitionId)record.itemId,
                subject).Value;
            lots.Add(new RetailStockLotSnapshot
            {
                saleItemId = saleItemId,
                itemDefinitionId = record.itemId,
                itemInstanceId = record.itemInstanceId,
                sourceStackId = record.stackId,
                quantity = take,
                unitMassGrams = unitMassGrams,
                sourceOperationId = $"{normalizedOperation}:stack:{record.stackId}",
                componentFingerprint = ItemStackSignature.Create(
                    record.itemId,
                    record.components),
                components = CaptureRetailComponents(record.components)
            });
            undo.Add(new RetailStockSourceUndo(
                WorldItemTransferPhysicalProjection.CloneRecord(record),
                take,
                record.quantity == take));
        }

        foreach (RetailStockLotSnapshot uniqueLot in lots.Where(
            lot => !string.IsNullOrEmpty(lot.itemInstanceId)))
        {
            string uniqueFailure = string.Empty;
            if (retailStockPhysical == null
                || !retailStockPhysical.TryPrepareExistingUniqueLot(
                    uniqueLot,
                    out uniqueFailure))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-unique-prepare-failed",
                    uniqueFailure ?? string.Empty);
                return false;
            }
        }

        if (removals.Any(entry =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    entry.record.components))
            || carryInventory.Items.Any(item => item != null
                && item.quantity > 0
                && string.Equals(
                    item.ownerOperationId,
                    normalizedOperation,
                    StringComparison.Ordinal)
                && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    item.components)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-prepared-output-route-protected-precommit",
                normalizedOperation);
            return false;
        }

        List<CharacterCarriedItemSaveData> carriedUndo =
            carryInventory.RemoveItemsOwnedByOperations(new[] { normalizedOperation });
        if (carriedUndo.Sum(item => item?.quantity ?? 0) != quantity)
        {
            RestoreRetailCarryEntries(carryInventory, carriedUndo, out _);
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-carry-commit-mismatch",
                normalizedOperation);
            return false;
        }

        if (!quantityLeaseMutations.TryConsumeSlices(
                lease.leaseId,
                quantity,
                out _,
                out failure))
        {
            if (!RestoreRetailCarryEntries(
                    carryInventory,
                    carriedUndo,
                    out string carryRestoreFailure))
            {
                throw new InvalidOperationException(
                    "Retail lease commit failed and exact carried cargo could not be restored: "
                    + carryRestoreFailure);
            }
            return false;
        }

        HashSet<Vector2Int> touched = new();
        foreach ((WorldItemStackRecord record, int remove) in removals)
        {
            touched.Add(record.position);
            record.quantity -= remove;
            if (record.quantity <= 0)
            {
                repository.Remove(record);
            }
        }
        repository.MarkChanged();
        foreach (Vector2Int position in touched)
        {
            markerPresenter.RefreshAt(position);
        }
        List<RetailStockLotSnapshot> boundUniqueLots = new();
        foreach (RetailStockLotSnapshot uniqueLot in lots.Where(
            lot => !string.IsNullOrEmpty(lot.itemInstanceId)))
        {
            if (retailStockPhysical.TryBindExistingUniqueLot(
                    uniqueLot,
                    out string bindFailure))
            {
                boundUniqueLots.Add(uniqueLot);
                continue;
            }

            RestoreRetailSourceRecords(undo);
            foreach (RetailStockLotSnapshot bound in boundUniqueLots)
            {
                if (!retailStockPhysical.TryRestoreBoundUniqueLot(
                        bound,
                        CombatEquipmentWorldState.Carried,
                        out string uniqueRestoreFailure))
                {
                    throw new InvalidOperationException(
                        "Retail unique bind failed and a prior unique lot could not be restored: "
                        + uniqueRestoreFailure);
                }
            }
            if (!RestoreRetailCarryEntries(
                    carryInventory,
                    carriedUndo,
                    out string carryRestoreFailure))
            {
                throw new InvalidOperationException(
                    "Retail unique bind failed and exact carried cargo could not be restored: "
                    + carryRestoreFailure);
            }
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-unique-bind-failed",
                bindFailure ?? string.Empty);
            return false;
        }
        receipt = new ReservedRetailStockTransferReceipt(
            normalizedOperation,
            lots.AsReadOnly(),
            undo.AsReadOnly(),
            carryInventory,
            carriedUndo.AsReadOnly());
        return true;
    }

    public bool TryRollbackRetailTransfer(
        ReservedRetailStockTransferReceipt receipt,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (receipt == null || receipt.RolledBack || receipt.Undo == null)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-rollback-invalid");
            return false;
        }

        HashSet<Vector2Int> touched = new();
        foreach (RetailStockSourceUndo entry in receipt.Undo)
        {
            WorldItemStackRecord snapshot = entry.Snapshot;
            if (snapshot == null)
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-rollback-snapshot-missing");
                return false;
            }
            touched.Add(snapshot.position);
            if (entry.RecordWasRemoved)
            {
                if (repository.RecordsById.ContainsKey(snapshot.stackId))
                {
                    failure = new DomainFailure(
                        FailureCode.ItemTransferRequestFailed,
                        "retail-transfer-rollback-stack-conflict",
                        snapshot.stackId);
                    return false;
                }
                repository.Add(WorldItemTransferPhysicalProjection.CloneRecord(snapshot));
            }
            else if (repository.RecordsById.TryGetValue(
                snapshot.stackId,
                out WorldItemStackRecord remaining))
            {
                remaining.quantity = checked(remaining.quantity + entry.RemovedQuantity);
            }
            else
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-rollback-source-missing",
                    snapshot.stackId);
                return false;
            }
        }
        foreach (RetailStockLotSnapshot uniqueLot in receipt.Lots.Where(
            lot => lot != null && !string.IsNullOrEmpty(lot.itemInstanceId)))
        {
            string uniqueRestoreFailure = string.Empty;
            if (retailStockPhysical == null
                || !retailStockPhysical.TryRestoreBoundUniqueLot(
                    uniqueLot,
                    CombatEquipmentWorldState.Carried,
                    out uniqueRestoreFailure))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-unique-rollback-failed",
                    uniqueRestoreFailure ?? string.Empty);
                return false;
            }
        }
        if (!RestoreRetailCarryEntries(
                receipt.CarryInventory,
                receipt.CarriedUndo,
                out string restoreFailure))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-carry-rollback-failed",
                restoreFailure ?? string.Empty);
            return false;
        }
        receipt.RolledBack = true;
        repository.MarkChanged();
        foreach (Vector2Int position in touched)
        {
            markerPresenter.RefreshAt(position);
        }
        return true;
    }

    private bool RestoreRetailCarryEntries(
        CharacterCarryInventory carryInventory,
        IReadOnlyList<CharacterCarriedItemSaveData> carriedEntries,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (carryInventory == null)
        {
            failureReason = "retail-transfer-carry-authority-missing";
            return false;
        }
        foreach (CharacterCarriedItemSaveData carried in
            carriedEntries ?? Array.Empty<CharacterCarriedItemSaveData>())
        {
            if (!carryInventory.TryAddLeasedPartialStack(
                    carried.carriedStackId,
                    carried.sourceStackId,
                    carried.ownerOperationId,
                    carried.itemInstanceId,
                    carried.itemId,
                    carried.quantity,
                    catalogProvider,
                    haulingSettingsProvider,
                    carried.wasteOrigin,
                    carried.contamination,
                    carried.components,
                    out int restored,
                    out string restoreFailure)
                || restored != carried.quantity)
            {
                failureReason = restoreFailure ?? string.Empty;
                return false;
            }
        }
        return true;
    }

    private void RestoreRetailSourceRecords(
        IReadOnlyList<RetailStockSourceUndo> undo)
    {
        foreach (RetailStockSourceUndo entry in undo ?? Array.Empty<RetailStockSourceUndo>())
        {
            WorldItemStackRecord snapshot = entry.Snapshot;
            if (snapshot == null)
            {
                continue;
            }
            if (entry.RecordWasRemoved)
            {
                if (!repository.RecordsById.ContainsKey(snapshot.stackId))
                {
                    repository.Add(WorldItemTransferPhysicalProjection.CloneRecord(snapshot));
                }
            }
            else if (repository.RecordsById.TryGetValue(
                snapshot.stackId,
                out WorldItemStackRecord remaining))
            {
                remaining.quantity = checked(remaining.quantity + entry.RemovedQuantity);
            }
        }
        repository.MarkChanged();
    }

    private static List<RetailStockComponentSnapshot> CaptureRetailComponents(
        IEnumerable<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .OrderBy(component => component.componentTypeId, StringComparer.Ordinal)
            .Select(component => new RetailStockComponentSnapshot
            {
                componentTypeId = component.componentTypeId,
                schemaVersion = component.schemaVersion,
                affectsStacking = component.affectsStacking,
                values = (component.values ?? new List<ItemStateValueSaveData>())
                    .Where(value => value != null)
                    .OrderBy(value => value.key, StringComparer.Ordinal)
                    .Select(value => new RetailStockComponentValueSnapshot
                    {
                        key = value.key,
                        kind = (int)value.kind,
                        stringValue = value.stringValue,
                        integerValue = value.integerValue,
                        decimalValue = value.decimalValue,
                        booleanValue = value.booleanValue
                    })
                    .ToList()
            })
            .ToList();
}
