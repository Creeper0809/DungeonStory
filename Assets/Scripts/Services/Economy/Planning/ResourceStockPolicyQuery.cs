using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Read-only projection used by production routing. Keeping this projection
/// separate from ResourceStockPolicyRuntime prevents the production
/// distribution graph from depending on the stock-policy command runtime,
/// which itself creates production orders.
/// </summary>
public sealed class ResourceStockPolicyQuery : IResourceStockPolicyQuery
{
    private const string SellDestinationPrefix = "stock-policy:sell:";

    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IStockQuery stock;

    public ResourceStockPolicyQuery(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IStockQuery stock)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
    }

    public IReadOnlyList<ResourceStockPolicyData> Policies => State.PolicyView;

    public int CountOwned(string itemId)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return 0;
        }

        return SaturatingSum(stock.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && string.Equals(
                    stack.ItemId,
                    normalized,
                    StringComparison.Ordinal)
                && !(stack.DestinationId?.StartsWith(
                    SellDestinationPrefix,
                    StringComparison.Ordinal) ?? false))
            .Select(stack => stack.Quantity));
    }

    public EmergencyStockReadiness GetEmergencyReadiness()
    {
        ResourceStockPolicyData[] reserves = Policies
            .Where(policy => policy != null
                && policy.enabled
                && policy.isEmergencyReserve)
            .OrderBy(policy => policy.itemId, StringComparer.Ordinal)
            .ToArray();
        int shortages = reserves.Count(policy =>
            CountOwned(policy.itemId) < policy.minimumStock);
        return new EmergencyStockReadiness(
            reserves.Length > 0,
            reserves.Length > 0 && shortages == 0,
            reserves.Length,
            shortages);
    }

    private ResourceStockPolicyAggregateState State =>
        aggregateRootStore.GetOrCreate(
            () => new ResourceStockPolicyAggregateState());

    private static int SaturatingSum(IEnumerable<int> values)
    {
        long total = 0L;
        foreach (int value in values)
        {
            total += Math.Max(0, value);
            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return (int)total;
    }
}
