using System;
using System.Linq;

public static class WarehousePhysicalStockOperations
{
    public static int Consume(
        this IWorldItemStackRuntime items,
        IWarehouseFacility warehouse,
        StockCategory category,
        int requested)
    {
        if (items == null || warehouse == null || requested <= 0)
        {
            return 0;
        }

        string destinationId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        int remaining = requested;
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                     .Where(stack => stack != null
                         && stack.Quantity > 0
                         && stack.State == WorldItemStackState.Stored
                         && items.CatalogProvider.GetDefinition(stack.ItemId).StockCategory == category
                         && string.Equals(
                             string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId)
                                 ? stack.DestinationId
                                 : stack.SourceStorageDestinationId,
                             destinationId,
                             StringComparison.Ordinal))
                     .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                     .ToArray())
        {
            int amount = Math.Min(remaining, stack.Quantity);
            if (!items.TryConsumeStackQuantity(stack.StackId, amount, out _))
            {
                throw new InvalidOperationException(
                    $"Failed to consume authoritative warehouse stack '{stack.StackId}'.");
            }

            remaining -= amount;
            if (remaining == 0)
            {
                break;
            }
        }

        return requested - remaining;
    }

    public static int ConsumeAcross(
        this IWorldItemStackRuntime items,
        System.Collections.Generic.IEnumerable<IWarehouseFacility> warehouses,
        StockCategory category,
        int requested)
    {
        int remaining = Math.Max(0, requested);
        foreach (IWarehouseFacility warehouse in warehouses
                     ?? Array.Empty<IWarehouseFacility>())
        {
            remaining -= items.Consume(warehouse, category, remaining);
            if (remaining == 0)
            {
                break;
            }
        }

        return requested - remaining;
    }
}
