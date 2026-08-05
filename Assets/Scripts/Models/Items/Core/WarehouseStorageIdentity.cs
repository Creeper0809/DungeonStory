using System;

public static class WarehouseStorageIdentity
{
    public const string DestinationPrefix = "warehouse:";

    public static string RequireDestinationId(IWarehouseFacility warehouse)
    {
        if (warehouse == null)
        {
            throw new ArgumentNullException(nameof(warehouse));
        }

        BuildingInstanceId buildingId = warehouse.PersistentInstanceId;
        if (!buildingId.IsValid)
        {
            throw new InvalidOperationException(
                "Warehouse facilities require a persistent BuildingInstanceId before storage access.");
        }

        return DestinationPrefix + buildingId.Value;
    }
}
