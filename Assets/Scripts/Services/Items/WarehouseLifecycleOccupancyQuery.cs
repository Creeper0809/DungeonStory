using System;
using System.Linq;

/// <summary>
/// Read-only cross-boundary guard for warehouse demolition and relocation.
/// It joins the physical repository, destination admission ledger and active
/// haul intents without turning a save DTO or UI snapshot into gameplay
/// authority.
/// </summary>
public sealed class WarehouseLifecycleOccupancyQuery :
    IWarehouseLifecycleOccupancyQuery
{
    private readonly WorldItemRepository repository;
    private readonly IWarehouseMassAdmissionLedgerQuery admissionLedger;

    public WarehouseLifecycleOccupancyQuery(
        WorldItemRepository repository,
        IWarehouseMassAdmissionLedgerQuery admissionLedger)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.admissionLedger = admissionLedger
            ?? throw new ArgumentNullException(nameof(admissionLedger));
    }

    public bool TryRequireEmpty(
        IWarehouseFacility warehouse,
        out WarehouseLifecycleOccupancySnapshot occupancy,
        out string failureReason)
    {
        occupancy = default;
        failureReason = string.Empty;
        if (warehouse?.Inventory == null
            || !warehouse.PersistentInstanceId.IsValid)
        {
            failureReason = "warehouse-lifecycle-owner-invalid";
            return false;
        }

        string warehouseId = warehouse.PersistentInstanceId.Value;
        string destinationId =
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + warehouseId;
        int referencedStacks = repository.Records.Count(record =>
            record != null
            && record.quantity > 0
            && (string.Equals(
                    record.destinationId,
                    destinationId,
                    StringComparison.Ordinal)
                || string.Equals(
                    record.sourceStorageDestinationId,
                    destinationId,
                    StringComparison.Ordinal)));
        int activeIntents = repository.HaulDeliveryIntents
            .CaptureRuntimeState()
            .Count(intent => intent != null
                && (string.Equals(
                        intent.destinationId,
                        destinationId,
                        StringComparison.Ordinal)
                    || (intent.warehouseAdmissions
                            ?? new System.Collections.Generic.List<
                                WarehouseHaulAdmissionSaveData>())
                        .Any(admission => admission != null
                            && (string.Equals(
                                    admission.warehouseId,
                                    warehouseId,
                                    StringComparison.Ordinal)
                                || string.Equals(
                                    admission.sourceWarehouseId,
                                    warehouseId,
                                    StringComparison.Ordinal)))));

        occupancy = new WarehouseLifecycleOccupancySnapshot(
            warehouse.Inventory.StoredMassGrams,
            admissionLedger.GetReservedInboundMassGrams(
                warehouse.PersistentInstanceId),
            referencedStacks,
            activeIntents);
        if (occupancy.IsEmpty)
        {
            return true;
        }

        failureReason =
            $"warehouse-lifecycle-not-empty:{warehouseId}:"
            + $"storedMassGrams={occupancy.StoredMassGrams}:"
            + $"reservedInboundMassGrams={occupancy.ReservedInboundMassGrams}:"
            + $"referencedStacks={occupancy.ReferencedPhysicalStackCount}:"
            + $"activeHaulIntents={occupancy.ActiveHaulIntentCount}";
        return false;
    }
}
