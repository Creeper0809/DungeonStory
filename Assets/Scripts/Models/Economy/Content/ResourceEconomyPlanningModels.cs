using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum StockSurplusDisposition
{
    Hold = 0,
    Sell = 1,
    Process = 2,
    Compost = 3,
    Dismantle = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ResourceStockThreshold
{
    Minimum = 0,
    Target = 1,
    Maximum = 2
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResourceStockPolicyData
{
    public string itemId = string.Empty;
    public bool enabled;
    [Min(0)] public int minimumStock;
    [Min(0)] public int targetStock = 20;
    [Min(0)] public int maximumStock = 40;
    public StockSurplusDisposition surplusDisposition;
    public string lastStatus = string.Empty;

    public ResourceStockPolicyData Clone()
    {
        return (ResourceStockPolicyData)MemberwiseClone();
    }

    public void Normalize()
    {
        itemId = itemId?.Trim() ?? string.Empty;
        minimumStock = Mathf.Max(0, minimumStock);
        targetStock = Mathf.Max(minimumStock, targetStock);
        maximumStock = Mathf.Max(targetStock, maximumStock);
        lastStatus ??= string.Empty;
    }
}

public interface IResourceStockPolicyRuntime
{
    int Version { get; }
    IReadOnlyList<ResourceStockPolicyData> Policies { get; }
    ResourceStockPolicyData GetOrCreate(string itemId);
    bool SetPolicy(ResourceStockPolicyData policy, out string failureReason);
    int CountOwned(string itemId);
    DungeonResourceStockPolicySaveData Capture();
    ResourceStockPolicyRestoreCandidate PrepareRestoreCandidate(
        DungeonResourceStockPolicySaveData saveData);
    void PublishRestoreCandidate(ResourceStockPolicyRestoreCandidate candidate);
}

public sealed class ResourceStockPolicyAggregateState
{
    public Dictionary<string, ResourceStockPolicyData> ByItemId { get; } =
        new(StringComparer.Ordinal);
    public IReadOnlyList<ResourceStockPolicyData> PolicyView { get; set; } =
        Array.Empty<ResourceStockPolicyData>();
    public int Version { get; set; }
    public float NextEvaluationTime { get; set; }
}

public sealed class ResourceStockPolicyRestoreCandidate
{
    public ResourceStockPolicyRestoreCandidate(
        ResourceStockPolicyAggregateState state,
        DungeonResourceStockPolicySaveData payload = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Payload = payload;
    }

    public ResourceStockPolicyAggregateState State { get; }
    public DungeonResourceStockPolicySaveData Payload { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonResourceStockPolicySaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public List<ResourceStockPolicyData> policies =
        new List<ResourceStockPolicyData>();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum RegionalSupplyContractStatus
{
    Offered = 0,
    Accepted = 1,
    Delivering = 2,
    Completed = 3,
    Failed = 4,
    Declined = 5
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RegionalSupplyContractRequirement
{
    public string itemId = string.Empty;
    [Min(1)] public int amount = 1;

    public RegionalSupplyContractRequirement Clone()
    {
        return (RegionalSupplyContractRequirement)MemberwiseClone();
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RegionalSupplyContractState
{
    public string contractId = string.Empty;
    public string title = string.Empty;
    public string regionName = string.Empty;
    public int offeredDay;
    public int deadlineDay;
    public int rewardGold;
    public RegionalSupplyContractStatus status;
    public string destinationId = string.Empty;
    public string lastStatus = string.Empty;
    public List<RegionalSupplyContractRequirement> requirements =
        new List<RegionalSupplyContractRequirement>();

    public RegionalSupplyContractState Clone()
    {
        RegionalSupplyContractState clone =
            (RegionalSupplyContractState)MemberwiseClone();
        clone.requirements = (requirements
            ?? new List<RegionalSupplyContractRequirement>())
            .ConvertAll(requirement => requirement?.Clone());
        return clone;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonRegionalSupplyContractSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int currentDay = 1;
    public int nextOfferDay = 1;
    public int nextSequence = 1;
    public List<RegionalSupplyContractState> contracts =
        new List<RegionalSupplyContractState>();
}

public interface IRegionalSupplyContractRuntime
{
    int Version { get; }
    bool IsUnlocked { get; }
    IReadOnlyList<RegionalSupplyContractState> Contracts { get; }
    bool Accept(string contractId, out string message);
    bool Decline(string contractId, out string message);
    DungeonRegionalSupplyContractSaveData Capture();
    RegionalSupplyContractRestoreCandidate PrepareRestoreCandidate(
        DungeonRegionalSupplyContractSaveData saveData);
    void PublishRestoreCandidate(
        RegionalSupplyContractRestoreCandidate candidate);
}

public static class RegionalSupplyContractSizing
{
    public static int ResolveAmount(
        ResourceItemKind kind,
        int population,
        int completedResearchCount,
        int offerIndex)
    {
        int stageBonus = Mathf.Clamp(completedResearchCount / 12, 0, 5);
        int populationBonus = Mathf.Clamp(population / 3, 0, 10);
        return kind switch
        {
            ResourceItemKind.Raw => Mathf.Clamp(
                20 + populationBonus * 3 + stageBonus * 5 + offerIndex * 4,
                20,
                80),
            ResourceItemKind.Intermediate => Mathf.Clamp(
                10 + populationBonus * 2 + stageBonus * 3 + offerIndex * 2,
                10,
                40),
            _ => Mathf.Clamp(
                2 + populationBonus / 2 + stageBonus + offerIndex,
                2,
                12)
        };
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum GrandProjectStatus
{
    Locked = 0,
    Available = 1,
    WaitingForMaterials = 2,
    InProgress = 3,
    Completed = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class GrandProjectDefinition
{
    public GrandProjectDefinition(
        string projectId,
        string displayName,
        string description,
        string requiredResearchId,
        float requiredWork,
        params ItemAmountDefinition[] requirements)
    {
        ProjectId = projectId?.Trim() ?? string.Empty;
        DisplayName = displayName?.Trim() ?? string.Empty;
        Description = description?.Trim() ?? string.Empty;
        RequiredResearchId = requiredResearchId?.Trim() ?? string.Empty;
        RequiredWork = Mathf.Max(1f, requiredWork);
        Requirements = requirements ?? Array.Empty<ItemAmountDefinition>();
    }

    public string ProjectId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string RequiredResearchId { get; }
    public float RequiredWork { get; }
    public IReadOnlyList<ItemAmountDefinition> Requirements { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class GrandProjectRuntimeState
{
    public string activeProjectId = string.Empty;
    public string destinationId = string.Empty;
    public float completedWork;
    public string lastStatus = string.Empty;
    public List<string> completedProjectIds = new List<string>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonGrandProjectSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public GrandProjectRuntimeState state = new GrandProjectRuntimeState();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct GrandProjectWorkSnapshot
{
    public GrandProjectWorkSnapshot(
        bool available,
        string projectId,
        string displayName,
        float requiredWork,
        float completedWork,
        string unavailableReason)
    {
        Available = available;
        ProjectId = projectId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        RequiredWork = Mathf.Max(1f, requiredWork);
        CompletedWork = Mathf.Clamp(completedWork, 0f, RequiredWork);
        UnavailableReason = unavailableReason ?? string.Empty;
    }

    public bool Available { get; }
    public string ProjectId { get; }
    public string DisplayName { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public string UnavailableReason { get; }
}

public interface IGrandProjectRuntime
{
    int Version { get; }
    IReadOnlyList<GrandProjectDefinition> Definitions { get; }
    GrandProjectRuntimeState State { get; }
    GrandProjectStatus GetStatus(
        string projectId,
        out string reason);
    bool Start(string projectId, out string message);
    bool CancelActive(out string message);
    bool TryGetWork(
        BuildingInstanceId facilityId,
        out GrandProjectWorkSnapshot work);
    bool ApplyWork(
        BuildingInstanceId facilityId,
        float amount,
        out bool completed);
    DungeonGrandProjectSaveData Capture();
    GrandProjectRestoreCandidate BuildRestore(
        DungeonGrandProjectSaveData saveData);
    void PublishRestoreCandidate(GrandProjectRestoreCandidate candidate);
}

public interface IGrandProjectBenefitQuery
{
    bool IsCompleted(string projectId);
    float GetProductionOutputMultiplier(string facilityTag);
    float ContractRewardMultiplier { get; }
    float DefensePreparationMultiplier { get; }
    int ExpeditionSupplyCapacityBonus { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResourceEconomyForecastRow
{
    public string ItemId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Available { get; set; }
    public int Reserved { get; set; }
    public int ExpectedProduction { get; set; }
    public int ExpectedDemand { get; set; }
    public int ProjectedBalance =>
        Available + ExpectedProduction - ExpectedDemand;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResourceEconomyForecast
{
    public int HorizonDays { get; set; } = 3;
    public IReadOnlyList<ResourceEconomyForecastRow> Rows { get; set; } =
        Array.Empty<ResourceEconomyForecastRow>();
    public IReadOnlyList<ResourceEconomyForecastRow> Shortages { get; set; } =
        Array.Empty<ResourceEconomyForecastRow>();
    public IReadOnlyList<ResourceEconomyForecastRow> Surpluses { get; set; } =
        Array.Empty<ResourceEconomyForecastRow>();
}

public interface IResourceEconomyForecastService
{
    ResourceEconomyForecast Capture(int horizonDays = 3);
}
