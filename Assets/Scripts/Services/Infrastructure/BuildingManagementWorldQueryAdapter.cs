using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BuildingManagementWorldQueryAdapter : IBuildingManagementWorldQuery
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IRetailWorldQuery retail;
    private readonly IWarehouseWorldQuery warehouses;

    public BuildingManagementWorldQueryAdapter(IBuildingWorldQuery buildings, IRetailWorldQuery retail, IWarehouseWorldQuery warehouses)
    {
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.retail = retail ?? throw new ArgumentNullException(nameof(retail));
        this.warehouses = warehouses ?? throw new ArgumentNullException(nameof(warehouses));
    }

    public IReadOnlyList<BuildingManagementSnapshot> CaptureBuildings() => buildings.Buildings
        .Where(building => building != null)
        .Select(building => new BuildingManagementSnapshot(
            building.Facility != null && building.Facility.IsVisitorFacility,
            building.Facility != null && building.Facility.HasSupportedWorkTypes,
            building.IsDamaged))
        .ToArray();

    public IReadOnlyList<ShopManagementSnapshot> CaptureShops() => retail.RetailFacilities
        .Where(shop => shop != null)
        .Select(shop => new ShopManagementSnapshot(shop.HasAvailableStock))
        .ToArray();

    public IReadOnlyList<WarehouseManagementSnapshot> CaptureWarehouses() => warehouses.Warehouses
        .Where(warehouse => warehouse != null && warehouse.HasWarehouseInventory && warehouse.Inventory != null)
        .Select(warehouse =>
        {
            WarehouseInventory inventory = warehouse.Inventory;
            bool massAuthoritative = inventory.HasMassCapacityAuthority;
            return new WarehouseManagementSnapshot(
                inventory.TotalStock,
                massAuthoritative ? 0 : inventory.MaxCapacity,
                !massAuthoritative && inventory.HasCapacityLimit,
                inventory.EnumerateStock().ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value),
                massAuthoritative,
                massAuthoritative ? inventory.StoredMassGrams : 0L,
                massAuthoritative ? inventory.MaxMassGrams : 0L);
        })
        .ToArray();
}
