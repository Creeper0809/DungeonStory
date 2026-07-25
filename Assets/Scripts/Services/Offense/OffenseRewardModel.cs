using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public sealed class OffenseRewardGrantResult
{
    public OffenseRewardGrantResult(
        OffenseRewardCategory category,
        string label,
        int requestedAmount,
        int grantedAmount,
        bool success,
        string detail)
    {
        this.category = category;
        this.label = label ?? string.Empty;
        this.requestedAmount = Mathf.Max(0, requestedAmount);
        this.grantedAmount = Mathf.Max(0, grantedAmount);
        this.success = success;
        this.detail = detail ?? string.Empty;
    }

    public OffenseRewardCategory category { get; }
    public string label { get; }
    public int requestedAmount { get; }
    public int grantedAmount { get; }
    public bool success { get; }
    public string detail { get; }

    public string ToSummaryText()
    {
        string rewardName = string.IsNullOrWhiteSpace(label) ? category.ToString() : label;
        if (success)
        {
            string amountText = grantedAmount > 0 ? $" x{grantedAmount}" : string.Empty;
            string detailText = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" - {detail}";
            return $"{rewardName}{amountText}{detailText}";
        }

        string reason = string.IsNullOrWhiteSpace(detail) ? "지급 실패" : detail;
        return $"{rewardName} 지급 실패 - {reason}";
    }
}

public interface IOffenseRewardStateView
{
    int MoneyEarned { get; }
    int HumanFactionWeakening { get; }
    int RivalFactionWeakening { get; }
    int RecruitCandidateCount { get; }
    int PrisonerCount { get; }
    int SpecialMonsterCount { get; }
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
    public int HumanFactionWeakening { get; private set; }
    public int RivalFactionWeakening { get; private set; }
    public int RecruitCandidateCount { get; private set; }
    public int PrisonerCount { get; private set; }
    public int SpecialMonsterCount { get; private set; }
    public IReadOnlyDictionary<StockCategory, int> StockGrantedByCategory => stockGrantedView;
    public IReadOnlyCollection<int> RareFacilityBuildingIds => rareFacilityBuildingIds.ToArray();
    public IReadOnlyCollection<int> AcquiredBlueprintIds => acquiredBlueprintIds.ToArray();

    internal void Reset()
    {
        MoneyEarned = 0;
        HumanFactionWeakening = 0;
        RivalFactionWeakening = 0;
        RecruitCandidateCount = 0;
        PrisonerCount = 0;
        SpecialMonsterCount = 0;
        stockGrantedByCategory.Clear();
        rareFacilityBuildingIds.Clear();
        acquiredBlueprintIds.Clear();
    }

    internal void Restore(
        int moneyEarned,
        int humanFactionWeakening,
        int rivalFactionWeakening,
        int recruitCandidateCount,
        int prisonerCount,
        int specialMonsterCount,
        IReadOnlyDictionary<StockCategory, int> restoredStock,
        IEnumerable<int> restoredRareFacilityIds,
        IEnumerable<int> restoredBlueprintIds)
    {
        Reset();
        MoneyEarned = Mathf.Max(0, moneyEarned);
        HumanFactionWeakening = Mathf.Max(0, humanFactionWeakening);
        RivalFactionWeakening = Mathf.Max(0, rivalFactionWeakening);
        RecruitCandidateCount = Mathf.Max(0, recruitCandidateCount);
        PrisonerCount = Mathf.Max(0, prisonerCount);
        SpecialMonsterCount = Mathf.Max(0, specialMonsterCount);
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

    internal void RecordFactionWeakening(bool humanFaction, int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (humanFaction)
        {
            HumanFactionWeakening += safeAmount;
        }
        else
        {
            RivalFactionWeakening += safeAmount;
        }
    }

    internal void RecordRecruitCandidates(int amount)
    {
        RecruitCandidateCount += Mathf.Max(0, amount);
    }

    internal void RecordPrisoners(int amount)
    {
        PrisonerCount += Mathf.Max(0, amount);
    }

    internal void RecordSpecialMonsters(int amount)
    {
        SpecialMonsterCount += Mathf.Max(0, amount);
    }
}

public sealed class OffenseRewardContext
{
    public GameData gameData;
    public IEnumerable<IWarehouseFacility> warehouses = Enumerable.Empty<IWarehouseFacility>();
    public FacilityShopUnlockState shopUnlockState;
    public BlueprintResearchState researchState;
    public BlueprintResearchRuntime researchRuntime;
    public OffenseRewardState rewardState;
    public OffenseTargetDefinition target;
}
