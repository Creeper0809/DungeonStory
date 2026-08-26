using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Read-only bridge from the live combat craft/repair aggregates to the frozen
/// terminal-drain producer subjects. It adds no save or mutation authority.
/// </summary>
public interface ICombatEquipmentTerminalFacilitySourceQuery
{
    IReadOnlyList<CombatEquipmentTerminalPreparedSource> CaptureFacilitySources(
        BuildingInstanceId facilityId);
}

public interface ICombatEquipmentTerminalFacilityQuery
{
    ProductionFacilityHandle Capture(BuildingInstanceId facilityId);
}

public sealed class CombatEquipmentTerminalFacilityAdapter :
    ICombatEquipmentTerminalFacilityQuery
{
    private readonly IProductionAssemblyBridge bridge;

    public CombatEquipmentTerminalFacilityAdapter(IProductionAssemblyBridge bridge) =>
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

    public ProductionFacilityHandle Capture(BuildingInstanceId facilityId)
    {
        ProductionFacilityHandle[] matches = (bridge.Facilities
                ?? Array.Empty<ProductionFacilityHandle>())
            .Where(value => value != null && !value.IsDestroyed
                && value.InstanceId.Equals(facilityId)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "combat-equipment-terminal-facility-resolution-conflict");
        return matches[0];
    }
}

public sealed class CombatEquipmentTerminalDrainSourceAdapter :
    ICombatEquipmentTerminalFacilitySourceQuery
{
    private readonly ICombatEquipmentCraftQueueQuery craft;
    private readonly ICombatEquipmentMaintenanceOrderQuery repair;
    private readonly ICombatEquipmentTerminalDrainQuery producer;

    public CombatEquipmentTerminalDrainSourceAdapter(
        ICombatEquipmentCraftQueueQuery craft,
        ICombatEquipmentMaintenanceOrderQuery repair,
        ICombatEquipmentTerminalDrainQuery producer)
    {
        this.craft = craft ?? throw new ArgumentNullException(nameof(craft));
        this.repair = repair ?? throw new ArgumentNullException(nameof(repair));
        this.producer = producer
            ?? throw new ArgumentNullException(nameof(producer));
    }

    public IReadOnlyList<CombatEquipmentTerminalPreparedSource>
        CaptureFacilitySources(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("A valid facility is required.", nameof(facilityId));

        string[] owners = (craft.CraftQueue
                .Where(value => value != null
                    && string.Equals(value.facilityPersistentId,
                        facilityId.Value, StringComparison.Ordinal))
                .Select(value => ProductionFacilityDestructiveDrainOwnerStableIds
                    .CombatCraftOrder(value.orderId)))
            .Concat(repair.Orders
                .Where(value => value != null
                    && value.state is not CombatEquipmentRepairOrderState.Completed
                        and not CombatEquipmentRepairOrderState.Cancelled
                    && string.Equals(value.facilityBuildingId,
                        facilityId.Value, StringComparison.Ordinal))
                .Select(value => ProductionFacilityDestructiveDrainOwnerStableIds
                    .EquipmentRepairOrder(value.orderId)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        if (owners.Distinct(StringComparer.Ordinal).Count() != owners.Length)
            throw new InvalidOperationException(
                "combat-equipment-terminal-live-owner-duplicate");

        CombatEquipmentTerminalPreparedSource[] result = new
            CombatEquipmentTerminalPreparedSource[owners.Length];
        for (int index = 0; index < owners.Length; index++)
        {
            if (!producer.TryCaptureLiveSourceForPreparation(
                    owners[index],
                    out CombatEquipmentTerminalPreparedSource prepared,
                    out string failureReason)
                || prepared == null
                || prepared.Source == null
                || !string.Equals(prepared.Source.OwnerStableId, owners[index],
                    StringComparison.Ordinal)
                || !string.Equals(prepared.Source.FacilityId, facilityId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(failureReason)
                        ? "combat-equipment-terminal-live-source-missing"
                        : failureReason);
            }
            CombatEquipmentTerminalFrozenSubject source = prepared.Source;
            if (!string.Equals(source.OwnerStableId, owners[index],
                    StringComparison.Ordinal)
                || !string.Equals(source.FacilityId, facilityId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "combat-equipment-terminal-live-source-drift");
            }
            result[index] = prepared;
        }
        return Array.AsReadOnly(result);
    }
}
