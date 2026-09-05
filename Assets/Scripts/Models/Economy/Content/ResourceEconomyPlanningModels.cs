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
    public bool isEmergencyReserve;
    [Min(0)] public int minimumStock;
    [Min(0)] public int targetStock = 20;
    [Min(0)] public int maximumStock = 40;
    public StockSurplusDisposition surplusDisposition;
    public string lastStatus = string.Empty;
    public string inputDestinationId = string.Empty;
    public int inputDestinationX;
    public int inputDestinationY;
    public long inputCapacityGrams;
    public long inputMassAuthorityRevision;
    public string inputCapacityFingerprint = string.Empty;

    public ResourceStockPolicyData Clone()
    {
        return (ResourceStockPolicyData)MemberwiseClone();
    }

    public void Normalize()
    {
        itemId = itemId?.Trim() ?? string.Empty;
        if (!enabled)
            isEmergencyReserve = false;
        minimumStock = Mathf.Max(0, minimumStock);
        targetStock = Mathf.Max(minimumStock, targetStock);
        maximumStock = Mathf.Max(targetStock, maximumStock);
        lastStatus ??= string.Empty;
        inputDestinationId ??= string.Empty;
        inputCapacityFingerprint ??= string.Empty;
    }
}

public interface IResourceStockPolicyQuery
{
    IReadOnlyList<ResourceStockPolicyData> Policies { get; }
    int CountOwned(string itemId);
    EmergencyStockReadiness GetEmergencyReadiness();
}

public readonly struct EmergencyStockReadiness
{
    public EmergencyStockReadiness(
        bool configured,
        bool ready,
        int reserveCount,
        int shortageCount)
    {
        Configured = configured;
        Ready = ready;
        ReserveCount = Math.Max(0, reserveCount);
        ShortageCount = Math.Max(0, shortageCount);
    }

    public bool Configured { get; }
    public bool Ready { get; }
    public int ReserveCount { get; }
    public int ShortageCount { get; }
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

public enum EconomyProjectInputOwnerAnchorKind
{
    ReservedTarget = 0,
    LiveFacility = 1
}

public readonly struct EconomyProjectInputOwnerProjection
{
    public EconomyProjectInputOwnerProjection(
        long capacityGrams,
        long massAuthorityRevision,
        string fingerprint)
    {
        CapacityGrams = capacityGrams;
        MassAuthorityRevision = massAuthorityRevision;
        Fingerprint = fingerprint ?? string.Empty;
    }

    public long CapacityGrams { get; }
    public long MassAuthorityRevision { get; }
    public string Fingerprint { get; }
}

public interface IEconomyProjectInputOwnerPort
{
    bool TryEnsure(
        string ownerDomain,
        string ownerOperationId,
        string destinationId,
        Vector2Int position,
        EconomyProjectInputOwnerAnchorKind anchorKind,
        string ownerFacilityId,
        IReadOnlyDictionary<string, int> requirements,
        long storedCapacityGrams,
        long storedMassAuthorityRevision,
        string storedCapacityFingerprint,
        out EconomyProjectInputOwnerProjection projection,
        out string failureReason);

    bool TryValidate(
        string ownerDomain,
        string ownerOperationId,
        string destinationId,
        Vector2Int position,
        EconomyProjectInputOwnerAnchorKind anchorKind,
        string ownerFacilityId,
        IReadOnlyDictionary<string, int> requirements,
        long storedCapacityGrams,
        long storedMassAuthorityRevision,
        string storedCapacityFingerprint,
        out string failureReason);

    bool TryRetireDestination(
        string ownerDomain,
        string destinationId,
        string reasonCode,
        out string failureReason);
}

public sealed class ResourceStockPolicyAggregateState
{
    public Dictionary<string, ResourceStockPolicyData> ByItemId { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, ResourceStockPolicyPendingSale> PendingSalesByItemId
        { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, QualityRejectedSalePending> PendingRejectedSalesByOperationId
        { get; } = new(StringComparer.Ordinal);
    public IReadOnlyList<ResourceStockPolicyData> PolicyView { get; set; } =
        Array.Empty<ResourceStockPolicyData>();
    public int NextSaleSequence { get; set; } = 1;
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
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public int nextSaleSequence = 1;
    public List<ResourceStockPolicyData> policies =
        new List<ResourceStockPolicyData>();
    public List<ResourceStockPolicyPendingSale> pendingSales =
        new List<ResourceStockPolicyPendingSale>();
    public List<QualityRejectedSalePending> pendingRejectedSales =
        new List<QualityRejectedSalePending>();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum QualityRejectedSaleCommitPhase
{
    Prepared = 0,
    PhysicalCommitted = 1,
    IncomePublished = 2,
    UniqueAuthorityReleased = 3
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class QualityRejectedSalePending
{
    public int sequence;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string sourceStackId = string.Empty;
    public string itemId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string componentFingerprint = string.Empty;
    public string destinationId = string.Empty;
    public int destinationX;
    public int destinationY;
    public int quantity = 1;
    public int proceeds;
    public bool requiresCombatAuthority;
    public QualityRejectedSaleCommitPhase phase;
    public string commitId = string.Empty;
    public long inputMassGrams;

    public QualityRejectedSalePending Clone() =>
        (QualityRejectedSalePending)MemberwiseClone();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class QualityRejectedSaleContract
{
    public const string OperationPrefix = "quality-rejected-sale:";
    public const string TransferReason = "quality-rejected-market-export";
    private const int TransferDispositionKind = 1;

    public static string FormatOperationId(int sequence, string sourceStackId) =>
        $"{OperationPrefix}{sequence:D8}:{sourceStackId}";

    public static bool HasCanonicalPending(QualityRejectedSalePending pending)
    {
        if (!HasCanonicalPrepared(pending))
            return false;
        if (pending.phase == QualityRejectedSaleCommitPhase.Prepared)
        {
            return string.IsNullOrEmpty(pending.commitId)
                && pending.inputMassGrams == 0L;
        }
        return pending.phase is QualityRejectedSaleCommitPhase.PhysicalCommitted
                or QualityRejectedSaleCommitPhase.IncomePublished
                or QualityRejectedSaleCommitPhase.UniqueAuthorityReleased
            && IsCanonicalRequired(pending.commitId)
            && pending.inputMassGrams > 0L
            && string.Equals(
                pending.commitId,
                $"physical-batch-disposition:{TransferDispositionKind}:"
                    + $"{pending.operationId}:1:{pending.inputMassGrams}",
                StringComparison.Ordinal);
    }

    public static bool HasCanonicalPrepared(QualityRejectedSalePending pending) =>
        pending != null
        && pending.sequence > 0
        && IsCanonicalRequired(pending.sourceStackId)
        && string.Equals(
            pending.operationId,
            FormatOperationId(pending.sequence, pending.sourceStackId),
            StringComparison.Ordinal)
        && string.Equals(pending.reasonCode, TransferReason, StringComparison.Ordinal)
        && IsCanonicalRequired(pending.itemId)
        && IsCanonicalRequired(pending.itemInstanceId)
        && IsLowerSha256(pending.componentFingerprint)
        && string.Equals(
            pending.destinationId,
            QualityRejectedOutputRules.MarketDestinationId,
            StringComparison.Ordinal)
        && pending.quantity == 1
        && pending.proceeds > 0
        && Enum.IsDefined(typeof(QualityRejectedSaleCommitPhase), pending.phase);

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsLowerSha256(string value)
    {
        if (value?.Length != 64)
            return false;
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9')
                && character is not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ResourceStockPolicySaleCommitPhase
{
    PhysicalCommitted = 1,
    IncomePublished = 2
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResourceStockPolicySaleTransferReceipt
{
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string commitId = string.Empty;
    public List<string> sourceStackIds = new List<string>();
    public int quantity;
    public long inputMassGrams;

    public ResourceStockPolicySaleTransferReceipt Clone()
    {
        ResourceStockPolicySaleTransferReceipt clone =
            (ResourceStockPolicySaleTransferReceipt)MemberwiseClone();
        clone.sourceStackIds = new List<string>(
            sourceStackIds ?? new List<string>());
        return clone;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResourceStockPolicyPendingSale
{
    public int sequence;
    public string itemId = string.Empty;
    public string destinationId = string.Empty;
    public int quantity;
    public int proceeds;
    public ResourceStockPolicySaleCommitPhase phase;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string commitId = string.Empty;
    public List<string> sourceStackIds = new List<string>();
    public long inputMassGrams;

    public ResourceStockPolicyPendingSale Clone()
    {
        ResourceStockPolicyPendingSale clone =
            (ResourceStockPolicyPendingSale)MemberwiseClone();
        clone.sourceStackIds = new List<string>(
            sourceStackIds ?? new List<string>());
        return clone;
    }
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum RegionalSupplyDeliveryCommitPhase
{
    None = 0,
    PhysicalCommitted = 1,
    RewardPublished = 2
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RegionalSupplyDeliveryTransferReceipt
{
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string commitId = string.Empty;
    public List<string> sourceStackIds = new List<string>();
    public int quantity;
    public long inputMassGrams;

    public RegionalSupplyDeliveryTransferReceipt Clone()
    {
        RegionalSupplyDeliveryTransferReceipt clone =
            (RegionalSupplyDeliveryTransferReceipt)MemberwiseClone();
        clone.sourceStackIds = new List<string>(
            sourceStackIds ?? new List<string>());
        return clone;
    }
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
    public RegionalSupplyDeliveryCommitPhase deliveryCommitPhase;
    public string deliveryOperationId = string.Empty;
    public string deliveryCommitId = string.Empty;
    public List<string> deliverySourceStackIds = new List<string>();
    public int deliveryQuantity;
    public long deliveryMassGrams;
    public bool inputOwnerActive;
    public int inputDestinationX;
    public int inputDestinationY;
    public long inputCapacityGrams;
    public long inputMassAuthorityRevision;
    public string inputCapacityFingerprint = string.Empty;
    public List<RegionalSupplyContractRequirement> requirements =
        new List<RegionalSupplyContractRequirement>();

    public RegionalSupplyContractState Clone()
    {
        RegionalSupplyContractState clone =
            (RegionalSupplyContractState)MemberwiseClone();
        clone.requirements = (requirements
            ?? new List<RegionalSupplyContractRequirement>())
            .ConvertAll(requirement => requirement?.Clone());
        clone.deliverySourceStackIds = new List<string>(
            deliverySourceStackIds ?? new List<string>());
        return clone;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonRegionalSupplyContractSaveData
{
    public const int CurrentVersion = 3;

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
    public static int MinimumViableUnitPrice(ResourceItemKind kind) => kind switch
    {
        ResourceItemKind.Raw => 1,
        ResourceItemKind.Intermediate => 1,
        _ => 5
    };

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
public enum GrandProjectPhysicalCommitPhase
{
    None = 0,
    InputCommitted = 1,
    OutcomePublished = 2
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class GrandProjectPhysicalCommitSaveData
{
    public GrandProjectPhysicalCommitPhase phase;
    public string projectId = string.Empty;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string requestFingerprint = string.Empty;
    public string commitId = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public List<string> sourceStackIds = new List<string>();
    public string stateBeforeFingerprint = string.Empty;
    public string stateAfterFingerprint = string.Empty;

    public GrandProjectPhysicalCommitSaveData Clone() => new()
    {
        phase = phase,
        projectId = projectId,
        operationId = operationId,
        reasonCode = reasonCode,
        requestFingerprint = requestFingerprint,
        commitId = commitId,
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        sourceStackIds = new List<string>(sourceStackIds ?? new List<string>()),
        stateBeforeFingerprint = stateBeforeFingerprint,
        stateAfterFingerprint = stateAfterFingerprint
    };
}

public readonly struct GrandProjectPhysicalInputReceipt
{
    public GrandProjectPhysicalInputReceipt(
        string operationId,
        string reasonCode,
        string requestFingerprint,
        string commitId,
        int inputQuantity,
        long inputMassGrams,
        IReadOnlyList<string> sourceStackIds)
    {
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        CommitId = commitId ?? string.Empty;
        InputQuantity = inputQuantity;
        InputMassGrams = inputMassGrams;
        SourceStackIds = sourceStackIds ?? Array.Empty<string>();
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public string RequestFingerprint { get; }
    public string CommitId { get; }
    public int InputQuantity { get; }
    public long InputMassGrams { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public bool IsCommitted => !string.IsNullOrEmpty(OperationId)
        && !string.IsNullOrEmpty(ReasonCode)
        && !string.IsNullOrEmpty(RequestFingerprint)
        && !string.IsNullOrEmpty(CommitId)
        && InputQuantity > 0
        && InputMassGrams > 0L
        && (SourceStackIds?.Count ?? 0) > 0;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class GrandProjectRuntimeState
{
    public string activeProjectId = string.Empty;
    public string destinationId = string.Empty;
    public float completedWork;
    public string lastStatus = string.Empty;
    public string inputOwnerFacilityId = string.Empty;
    public int inputDestinationX;
    public int inputDestinationY;
    public long inputCapacityGrams;
    public long inputMassAuthorityRevision;
    public string inputCapacityFingerprint = string.Empty;
    public List<string> completedProjectIds = new List<string>();
    public GrandProjectPhysicalCommitSaveData pendingPhysicalCommit = new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonGrandProjectSaveData
{
    public const int CurrentVersion = 3;

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
