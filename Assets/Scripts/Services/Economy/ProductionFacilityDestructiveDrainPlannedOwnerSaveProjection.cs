using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Detached, save-only projection of the active source owners that must be
/// represented exactly once by a Prepared destructive-drain journal entry.
/// Terminal receipts are not source owners and are intentionally excluded.
/// </summary>
public static class ProductionFacilityDestructiveDrainPlannedOwnerSaveProjection
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Project(
        BuildingInstanceId facilityId,
        DungeonProductionBillSaveData production,
        DungeonCombatEquipmentSaveData combat,
        CombatEquipmentMaintenanceSaveData maintenance,
        DungeonCharacterEnvironmentSaveData environment,
        DungeonPhysicalItemSaveData items,
        DungeonCharacterWorldSaveData characters,
        ProductionPreparedOutputRoutingSaveData routing)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "A valid facility is required.",
                nameof(facilityId));
        if (production?.bills == null
            || combat?.craftOrders == null
            || maintenance?.orders == null
            || environment?.apparelWorkOrders == null
            || items?.stacks == null
            || items.pendingProductionInputDestinationDrains == null
            || characters?.actors == null
            || routing?.batches == null)
        {
            throw new InvalidOperationException(
                "Planned destructive-drain owner projection requires all current-format source collections.");
        }

        string destination = ProductionOutputDestinationId
            .FromFacility(facilityId).Value;
        Dictionary<string, IReadOnlyList<string>> result =
            new(StringComparer.Ordinal)
            {
                [ProductionFacilityDestructiveDrainParticipantIds
                    .GenericProductionBills] = ProjectUnique(
                        production.bills
                            .Where(value => value != null
                                && string.Equals(
                                    value.buildingInstanceId,
                                    facilityId.Value,
                                    StringComparison.Ordinal))
                            .Select(value =>
                                ProductionFacilityDestructiveDrainOwnerStableIds
                                    .GenericBill(value.billId)),
                        "generic production bill"),
                [ProductionFacilityDestructiveDrainParticipantIds
                    .CombatEquipmentCrafting] = ProjectUnique(
                        combat.craftOrders
                            .Where(value => value != null
                                && string.Equals(
                                    value.facilityPersistentId,
                                    facilityId.Value,
                                    StringComparison.Ordinal))
                            .Select(value =>
                                ProductionFacilityDestructiveDrainOwnerStableIds
                                    .CombatCraftOrder(value.orderId))
                            .Concat(maintenance.orders
                                .Where(value => value != null
                                    && value.state is not
                                        CombatEquipmentRepairOrderState.Completed
                                        and not CombatEquipmentRepairOrderState.Cancelled
                                    && string.Equals(
                                        value.facilityBuildingId,
                                        facilityId.Value,
                                        StringComparison.Ordinal))
                                .Select(value =>
                                    ProductionFacilityDestructiveDrainOwnerStableIds
                                        .EquipmentRepairOrder(value.orderId))),
                        "combat equipment craft or repair order"),
                [ProductionFacilityDestructiveDrainParticipantIds
                    .ApparelWorkOrders] = ProjectUnique(
                        environment.apparelWorkOrders
                            .Where(value => value != null
                                && value.state != ApparelWorkOrderState.Completed
                                && string.Equals(
                                    value.facilityInstanceId,
                                    facilityId.Value,
                                    StringComparison.Ordinal))
                            .Select(value =>
                                ProductionFacilityDestructiveDrainOwnerStableIds
                                    .ApparelWorkOrder(value.orderId)),
                        "apparel work order"),
                [ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox] = ProjectUnique(
                        routing.batches
                            .Where(value => value != null
                                && string.Equals(
                                    value.ownerFacilityId,
                                    facilityId.Value,
                                    StringComparison.Ordinal))
                            .Select(value =>
                                ProductionFacilityDestructiveDrainOwnerStableIds
                                    .RoutingBatch(value.batchCommitId)),
                        "prepared-output routing batch"),
                [ProductionFacilityDestructiveDrainParticipantIds
                    .PhysicalCustodyCarryRecovery] = ProjectUnique(
                        items.stacks.Any(value => value != null
                                && value.quantity > 0
                                && IsOwnedPhysicalStack(value, destination))
                            ? new[]
                            {
                                ProductionFacilityDestructiveDrainOwnerStableIds
                                    .PhysicalDestination(destination)
                            }
                            : Array.Empty<string>(),
                        "physical custody destination"),
                [ProductionFacilityDestructiveDrainParticipantIds
                    .StockSensorEmbeddedSalvage] = ProjectUnique(
                        HasActiveStockSensorSource(production, facilityId.Value)
                        || ProductionOutputDestinationDurableSaveProjector
                            .CaptureExactDestinationCustody(
                                ProductionStockSensorRuntime.BuildDestinationId(
                                    facilityId.Value),
                                items,
                                characters).HasAuthority
                        || HasDurableStockSensorChild(items, facilityId.Value)
                            ? new[]
                            {
                                ProductionFacilityDestructiveDrainOwnerStableIds
                                    .StockSensor(facilityId.Value)
                            }
                            : Array.Empty<string>(),
                        "stock sensor")
            };
        return result;
    }

    private static bool HasActiveStockSensorSource(
        DungeonProductionBillSaveData production,
        string facilityId)
    {
        if (production.installedStockSensorFacilityIds == null
            || production.acknowledgedStockSensorFacilityIds == null
            || production.pendingStockSensorInstalls == null
            || production.installedStockSensors == null
            || production.pendingStockSensorRemovals == null)
        {
            throw new InvalidOperationException(
                "Stock-sensor owner projection requires current-format source collections.");
        }

        bool pendingInstall = production.pendingStockSensorInstalls.Any(value =>
            value != null && string.Equals(
                value.facilityId,
                facilityId,
                StringComparison.Ordinal));
        bool installed = production.installedStockSensors.Any(value =>
            value != null && string.Equals(
                value.facilityId,
                facilityId,
                StringComparison.Ordinal));
        bool activeRemoval = production.pendingStockSensorRemovals.Any(value =>
            value != null
            && value.phase != ProductionStockSensorRemovalPhase
                .OwnerAcknowledgedAwaitingCheckpointGc
            && string.Equals(
                value.facilityId,
                facilityId,
                StringComparison.Ordinal));
        return pendingInstall || installed || activeRemoval;
    }

    private static bool HasDurableStockSensorChild(
        DungeonPhysicalItemSaveData items,
        string facilityId)
    {
        string owner = ProductionFacilityDestructiveDrainOwnerStableIds
            .StockSensor(facilityId);
        string destination = ProductionStockSensorRuntime.BuildDestinationId(
            facilityId);
        return items.pendingProductionInputDestinationDrains.Any(value =>
            value != null
            && string.Equals(value.ownerStableId,
                owner, StringComparison.Ordinal)
            && string.Equals(value.facilityId,
                facilityId, StringComparison.Ordinal)
            && string.Equals(value.billId,
                destination, StringComparison.Ordinal)
            && string.Equals(value.sourceDestinationId,
                destination, StringComparison.Ordinal));
    }

    private static bool IsOwnedPhysicalStack(
        WorldItemStackSaveData stack,
        string originDestinationId)
    {
        if (stack.state == WorldItemStackState.FacilityOutputBuffer
            && string.Equals(
                stack.destinationId,
                originDestinationId,
                StringComparison.Ordinal))
        {
            return true;
        }
        return FacilityOutputExactRouteCustodyCodec.TryRead(
                stack.components,
                out FacilityOutputExactRouteCustodyMetadata custody)
            && string.Equals(
                custody.OriginDestinationId,
                originDestinationId,
                StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ProjectUnique(
        IEnumerable<string> source,
        string sourceKind)
    {
        string[] ordered = (source ?? throw new ArgumentNullException(
                nameof(source)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            if (string.Equals(
                    ordered[index - 1],
                    ordered[index],
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Duplicate " + sourceKind
                    + " identity in destructive-drain source projection: "
                    + ordered[index]);
            }
        }
        return Array.AsReadOnly(ordered);
    }
}
