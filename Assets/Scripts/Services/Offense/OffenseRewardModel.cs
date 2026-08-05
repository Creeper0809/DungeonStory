using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public interface IOffenseRewardStateView
{
    int MoneyEarned { get; }
    int RecoveredLootValue { get; }
    IReadOnlyDictionary<StockCategory, int> StockGrantedByCategory { get; }
    IReadOnlyCollection<int> RareFacilityBuildingIds { get; }
    IReadOnlyCollection<int> AcquiredBlueprintIds { get; }
}

public sealed class OffenseRewardState : IOffenseRewardStateView
{
    private readonly Dictionary<StockCategory, int> stockGrantedByCategory = new Dictionary<StockCategory, int>();
    private readonly HashSet<int> rareFacilityBuildingIds = new HashSet<int>();
    private readonly HashSet<int> acquiredBlueprintIds = new HashSet<int>();
    private readonly IReadOnlyDictionary<StockCategory, int> stockGrantedView;

    public OffenseRewardState()
    {
        stockGrantedView = new ReadOnlyDictionary<StockCategory, int>(stockGrantedByCategory);
    }

    public int MoneyEarned { get; private set; }
    public int RecoveredLootValue => MoneyEarned;
    public IReadOnlyDictionary<StockCategory, int> StockGrantedByCategory => stockGrantedView;
    public IReadOnlyCollection<int> RareFacilityBuildingIds => rareFacilityBuildingIds.ToArray();
    public IReadOnlyCollection<int> AcquiredBlueprintIds => acquiredBlueprintIds.ToArray();

    internal void Reset()
    {
        MoneyEarned = 0;
        stockGrantedByCategory.Clear();
        rareFacilityBuildingIds.Clear();
        acquiredBlueprintIds.Clear();
    }

    internal void Restore(
        int moneyEarned,
        IReadOnlyDictionary<StockCategory, int> restoredStock,
        IEnumerable<int> restoredRareFacilityIds,
        IEnumerable<int> restoredBlueprintIds)
    {
        Reset();
        MoneyEarned = Mathf.Max(0, moneyEarned);
        if (restoredStock != null)
        {
            foreach (KeyValuePair<StockCategory, int> pair in restoredStock)
            {
                if (pair.Value > 0)
                {
                    stockGrantedByCategory[pair.Key] = pair.Value;
                }
            }
        }

        rareFacilityBuildingIds.UnionWith(restoredRareFacilityIds ?? Array.Empty<int>());
        acquiredBlueprintIds.UnionWith(restoredBlueprintIds ?? Array.Empty<int>());
    }

    internal void RecordMoney(int amount)
    {
        MoneyEarned += Mathf.Max(0, amount);
    }

    internal void RecordStock(StockCategory category, int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0) return;

        stockGrantedByCategory[category] = stockGrantedByCategory.TryGetValue(category, out int current)
            ? current + safeAmount
            : safeAmount;
    }

    internal bool RecordRareFacility(BuildingSO building)
    {
        return building != null && rareFacilityBuildingIds.Add(building.id);
    }

    internal bool RecordBlueprint(FacilityBlueprintSO blueprint)
    {
        return blueprint != null && acquiredBlueprintIds.Add(blueprint.id);
    }

}

public sealed class OffenseRewardContext
{
    public GameSessionState gameData;
    public IEnumerable<IWarehouseFacility> warehouses = Enumerable.Empty<IWarehouseFacility>();
    public FacilityShopUnlockState shopUnlockState;
    public BlueprintResearchState researchState;
    public BlueprintResearchRuntime researchRuntime;
    public OffenseRewardState rewardState;
    public IOffenseRegionRuntime regionRuntime;
    public IOffenseReturnArrivalRuntime returnArrivalRuntime;
    public string expeditionId;
    public OffenseTargetDefinition target;
}
