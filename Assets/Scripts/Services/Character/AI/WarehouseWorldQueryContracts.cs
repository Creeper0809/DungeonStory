using System.Collections.Generic;
using DungeonStory.Foundation;

public interface IWarehouseWorldQuery
{
    int WarehouseVersion { get; }
    IReadOnlyList<IWarehouseFacility> Warehouses { get; }
}
