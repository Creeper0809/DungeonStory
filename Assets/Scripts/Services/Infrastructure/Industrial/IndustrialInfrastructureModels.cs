using System;
using System.Collections.Generic;
using UnityEngine;

public enum PowerPriority
{
    Critical = 1,
    Defense = 2,
    Essential = 3,
    Production = 4,
    Optional = 5
}

public enum ConveyorPortMode
{
    Input = 0,
    Output = 1,
    Both = 2
}

public enum ConveyorStallReason
{
    None = 0,
    PowerUnavailable = 1,
    IntentionallyStopped = 2,
    InputPortFull = 3,
    FilterMismatch = 4,
    DestinationFull = 5,
    NextSegmentOccupied = 6,
    NoRoute = 7,
    CyclicDeadlock = 8,
    OverflowBlocked = 9
}

public enum ConveyorNetworkState
{
    Running = 0,
    Stalled = 1,
    Deadlocked = 2,
    Unpowered = 3,
    Stopped = 4
}

public enum ConveyorOverflowPolicy
{
    ReserveWarehouseThenLoose = 0,
    AnyCompatibleWarehouseThenLoose = 1,
    LooseOnly = 2,
    ManualApproval = 3
}

public static class ConveyorNetworkStateEvaluator
{
    public static ConveyorNetworkState Evaluate(
        bool cyclic,
        int payloadCount,
        int totalCapacity,
        bool networkHasNoProgress,
        bool allUnpowered,
        bool allStopped,
        float longestStallSeconds,
        float stallThresholdSeconds = 30f)
    {
        if (payloadCount > 0 && allUnpowered)
        {
            return ConveyorNetworkState.Unpowered;
        }

        if (payloadCount > 0 && allStopped)
        {
            return ConveyorNetworkState.Stopped;
        }

        if (cyclic
            && payloadCount > 0
            && totalCapacity > 0
            && payloadCount >= totalCapacity
            && networkHasNoProgress)
        {
            return ConveyorNetworkState.Deadlocked;
        }

        return longestStallSeconds >= Mathf.Max(0f, stallThresholdSeconds)
            ? ConveyorNetworkState.Stalled
            : ConveyorNetworkState.Running;
    }
}

[Serializable]
public sealed class PowerNodeSaveData
{
    public string buildingInstanceId = string.Empty;
    public int priority = (int)PowerPriority.Production;
    public float storedPower;
    public float fuelSeconds;
    public float heat;
    public float fault;
    public bool breakerTripped;
}

[Serializable]
public sealed class DungeonPowerInfrastructureSaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public List<PowerNodeSaveData> nodes = new List<PowerNodeSaveData>();
}

[Serializable]
public sealed class FluidNodeSaveData
{
    public string buildingInstanceId = string.Empty;
    public float cleanWater;
    public float unsafeWater;
    public float foulWater;
    public float wastewater;
    public float blockage;
    public float leak;
    public float processorWork;
    public float manualWaterReserve;
    public WaterContainerTransferMode transferMode;
    public float transferWork;
}

[Serializable]
public sealed class DungeonFluidInfrastructureSaveData
{
    public const int CurrentVersion = 4;
    public int version = CurrentVersion;
    public List<FluidNodeSaveData> nodes = new List<FluidNodeSaveData>();
}

[Serializable]
public sealed class ConveyorFilterCriteria
{
    public List<string> itemIds = new List<string>();
    public List<StockCategory> stockCategories =
        new List<StockCategory>();
    public List<string> materialIds = new List<string>();
    public bool allowForbidden;
    public bool filterQuality;
    public CombatEquipmentQuality minimumQuality =
        CombatEquipmentQuality.Awful;
    public CombatEquipmentQuality maximumQuality =
        CombatEquipmentQuality.Mythic;
    public bool filterFreshness;
    [Range(0f, 1f)] public float minimumFreshness01;
    [Range(0f, 1f)] public float maximumFreshness01 = 1f;
    public bool allowContaminated = true;
}

[Serializable]
public sealed class ConveyorFilterSaveData
{
    public List<string> itemIds = new List<string>();
    public List<int> stockCategories = new List<int>();
    public List<string> materialIds = new List<string>();
    public bool allowForbidden;
    public bool filterQuality;
    public int minimumQuality = (int)CombatEquipmentQuality.Awful;
    public int maximumQuality = (int)CombatEquipmentQuality.Mythic;
    public bool filterFreshness;
    public float minimumFreshness01;
    public float maximumFreshness01 = 1f;
    public bool allowContaminated = true;
}

[Serializable]
public sealed class ConveyorPayloadSaveData
{
    public string payloadId = string.Empty;
    public string itemStackId = string.Empty;
    public string segmentBuildingInstanceId = string.Empty;
    public string previousBuildingInstanceId = string.Empty;
    public string destinationId = string.Empty;
    public float progress;
    public float lastMovedAt;
    public float stalledSince;
    public int routeVersion;
    public ConveyorStallReason stallReason;
}

[Serializable]
public sealed class ConveyorNodeSaveData
{
    public string buildingInstanceId = string.Empty;
    public bool enabled = true;
    public string destinationId = string.Empty;
    public ConveyorOverflowPolicy overflowPolicy =
        ConveyorOverflowPolicy.ReserveWarehouseThenLoose;
    public string reserveWarehouseId = string.Empty;
    public ConveyorFilterSaveData filter = new ConveyorFilterSaveData();
}

[Serializable]
public sealed class DungeonConveyorInfrastructureSaveData
{
    public const int CurrentVersion = 3;
    public int version = CurrentVersion;
    public int nextPayloadSequence = 1;
    public List<ConveyorNodeSaveData> nodes =
        new List<ConveyorNodeSaveData>();
    public List<ConveyorPayloadSaveData> payloads =
        new List<ConveyorPayloadSaveData>();
}

public sealed class PowerNodeSnapshot
{
    public BuildingInstanceId BuildingId { get; set; }
    public string NetworkId { get; set; } = string.Empty;
    public PowerPriority Priority { get; set; }
    public bool Powered { get; set; }
    public bool BreakerTripped { get; set; }
    public float ProductionPerSecond { get; set; }
    public float DemandPerSecond { get; set; }
    public float SuppliedFraction { get; set; }
    public float StoredPower { get; set; }
    public float StorageCapacity { get; set; }
    public float Heat { get; set; }
    public float Fault { get; set; }
}

public sealed class PowerNetworkSnapshot
{
    public string NetworkId { get; set; } = string.Empty;
    public float ProductionPerSecond { get; set; }
    public float DemandPerSecond { get; set; }
    public float SuppliedPerSecond { get; set; }
    public float StoredPower { get; set; }
    public float StorageCapacity { get; set; }
    public bool Tripped { get; set; }
    public IReadOnlyList<PowerNodeSnapshot> Nodes { get; set; } =
        Array.Empty<PowerNodeSnapshot>();
}

public sealed class WaterTransferFacilitySnapshot
{
    public BuildingInstanceId BuildingId { get; set; }
    public WaterContainerTransferMode Mode { get; set; }
    public bool Powered { get; set; }
    public float Progress01 { get; set; }
    public InfrastructureStatus Status { get; set; }
}

public sealed class ConveyorPayloadSnapshot
{
    public string PayloadId { get; set; } = string.Empty;
    public ItemStackId StackId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public BuildingInstanceId SegmentBuildingId { get; set; }
    public string DestinationId { get; set; } = string.Empty;
    public float Progress { get; set; }
    public float StalledSeconds { get; set; }
    public ConveyorStallReason StallReason { get; set; }
}

public sealed class ConveyorNodeSnapshot
{
    public BuildingInstanceId BuildingId { get; set; }
    public int Capacity { get; set; }
    public bool Enabled { get; set; }
    public string DestinationId { get; set; } = string.Empty;
    public ConveyorOverflowPolicy OverflowPolicy { get; set; }
    public string ReserveWarehouseId { get; set; } = string.Empty;
    public ConveyorFilterCriteria Filter { get; set; } =
        new ConveyorFilterCriteria();
}

public sealed class ConveyorNetworkSnapshot
{
    public string NetworkId { get; set; } = string.Empty;
    public ConveyorNetworkState State { get; set; }
    public int PayloadCount { get; set; }
    public int Capacity { get; set; }
    public bool IsCyclic { get; set; }
    public float LongestStallSeconds { get; set; }
    public ConveyorStallReason PrimaryReason { get; set; }
    public BuildingInstanceId PlannedOverflowBuildingId { get; set; }
    public IReadOnlyList<ConveyorPayloadSnapshot> Payloads { get; set; } =
        Array.Empty<ConveyorPayloadSnapshot>();
    public IReadOnlyList<ConveyorNodeSnapshot> Nodes { get; set; } =
        Array.Empty<ConveyorNodeSnapshot>();
}

public readonly struct InfrastructureCommandResult
{
    public InfrastructureCommandResult(bool succeeded, DomainFailure failure)
    {
        Succeeded = succeeded;
        Failure = failure;
    }

    public bool Succeeded { get; }
    public DomainFailure Failure { get; }

    public static InfrastructureCommandResult Success() =>
        new InfrastructureCommandResult(true, DomainFailure.None);

    public static InfrastructureCommandResult Failed(
        FailureCode code,
        params string[] parameters) =>
        new InfrastructureCommandResult(
            false,
            new DomainFailure(code, parameters));
}

public interface IPowerInfrastructureQuery
{
    int Version { get; }
    IReadOnlyList<PowerNetworkSnapshot> Networks { get; }
    bool IsPowered(BuildableObject building);
    bool TryGetNode(BuildableObject building, out PowerNodeSnapshot snapshot);
}

public interface IPowerInfrastructureCommand
{
    InfrastructureCommandResult SetPriority(
        BuildableObject building,
        PowerPriority priority);
    InfrastructureCommandResult ResetBreaker(BuildableObject building);
}

public interface IPowerInfrastructurePersistence
{
    DungeonPowerInfrastructureSaveData Capture();
    ElectricalNetworkRestoreCandidate PrepareRestore(
        DungeonPowerInfrastructureSaveData snapshot);
    void Restore(ElectricalNetworkRestoreCandidate candidate);
}

public interface IFluidInfrastructureQuery
{
    int Version { get; }
    IReadOnlyList<FluidNetworkSnapshot> Networks { get; }
    bool TryGetNetwork(
        BuildableObject building,
        out FluidNetworkSnapshot snapshot);
    IReadOnlyList<WaterTransferFacilitySnapshot> WaterTransfers { get; }
    bool TryGetMaintenance(
        BuildableObject building,
        out float blockage,
        out float leak);
}

public interface IFluidInfrastructureTransaction
{
    bool TryConsume(
        BuildableObject consumer,
        WorldWaterQuality minimumQuality,
        float amount,
        out WorldWaterQuality consumedQuality,
        out DomainFailure failure);
    bool CanConsume(
        BuildableObject consumer,
        WorldWaterQuality minimumQuality,
        float amount,
        out DomainFailure failure);
    bool TryAdd(
        BuildableObject producer,
        WorldWaterQuality quality,
        float amount,
        out float accepted);
    bool TryConsumeManualContainer(
        BuildableObject consumer,
        string destinationId,
        float amount,
        out DomainFailure failure);
}

public interface IFluidWastewaterTransaction
{
    bool TryAddWastewater(
        BuildableObject fixture,
        float amount,
        out float accepted,
        out DomainFailure failure);
    bool TryConsumeWastewater(
        BuildableObject processor,
        float amount,
        out float consumed);
    bool CanAcceptWastewater(
        BuildableObject fixture,
        float amount,
        out DomainFailure failure);
}

public interface IFluidInfrastructureCommand
{
    InfrastructureCommandResult SetWaterTransferMode(
        BuildableObject building,
        WaterContainerTransferMode mode);
    InfrastructureCommandResult ClearBlockage(BuildableObject building);
    InfrastructureCommandResult RepairLeak(BuildableObject building);
}

public interface IFluidInfrastructurePersistence
{
    DungeonFluidInfrastructureSaveData Capture();
    FluidNetworkRestoreCandidate PrepareRestore(
        DungeonFluidInfrastructureSaveData snapshot);
    void Restore(FluidNetworkRestoreCandidate candidate);
}

public enum WaterFixtureSupplyKind
{
    None = 0,
    Piped = 1,
    ManualContainer = 2,
    DryFallback = 3
}

public readonly struct WaterFixtureUseTicket
{
    public WaterFixtureUseTicket(
        BuildingInstanceId fixtureId,
        WaterFixtureSupplyKind supplyKind,
        float wastewaterAmount)
    {
        FixtureId = fixtureId;
        SupplyKind = supplyKind;
        WastewaterAmount = Mathf.Max(0f, wastewaterAmount);
    }

    public BuildingInstanceId FixtureId { get; }
    public WaterFixtureSupplyKind SupplyKind { get; }
    public float WastewaterAmount { get; }
    public bool IsValid =>
        FixtureId.IsValid
        && SupplyKind != WaterFixtureSupplyKind.None;
}

public interface IWaterFixtureUseRuntime
{
    bool TryBeginUse(
        BuildableObject fixture,
        CharacterId protectedCharacterId,
        out WaterFixtureUseTicket ticket,
        out DomainFailure failure);
    void CompleteUse(
        BuildableObject fixture,
        WaterFixtureUseTicket ticket);
}

public interface IProcessFluidUseRuntime
{
    bool EnsureCycleSupply(
        BuildableObject facility,
        WorkTypeId workTypeId,
        out DomainFailure failure);
    bool TryConsumeCycle(
        BuildableObject facility,
        WorkTypeId workTypeId,
        out DomainFailure failure);
    bool TryConsumeCycle(
        BuildableObject facility,
        WorkTypeId workTypeId,
        float cleanWater,
        float wastewater,
        bool allowsManualWaterFallback,
        out DomainFailure failure);
}

public interface IConveyorInfrastructureQuery
{
    int Version { get; }
    IReadOnlyList<ConveyorNetworkSnapshot> Networks { get; }
}

public interface IConveyorPayloadTransaction
{
    bool TryLoadStack(
        ItemStackId stackId,
        BuildableObject inputPort,
        string destinationId,
        out string payloadId,
        out DomainFailure failure);
}

public interface IConveyorInfrastructureCommand
{
    InfrastructureCommandResult SetNodeEnabled(
        BuildableObject segment,
        bool enabled);
    InfrastructureCommandResult SetPortDestination(
        BuildableObject port,
        string destinationId);
    InfrastructureCommandResult SetOverflowPolicy(
        BuildableObject segment,
        ConveyorOverflowPolicy policy,
        string reserveWarehouseId);
    InfrastructureCommandResult SetFilter(
        BuildableObject segment,
        IReadOnlyList<string> itemIds,
        IReadOnlyList<StockCategory> stockCategories,
        bool allowForbidden);
    InfrastructureCommandResult SetAdvancedFilter(
        BuildableObject segment,
        ConveyorFilterCriteria criteria);
    InfrastructureCommandResult ApproveOverflow(string payloadId);
    void MarkTopologyDirty();
}

public interface IConveyorRoutingService
{
    bool TryFindRoute(
        BuildingInstanceId fromBuildingId,
        string destinationId,
        ItemStackId stackId,
        out IReadOnlyList<BuildingInstanceId> buildingIds,
        out ConveyorStallReason failureReason);
}

public interface IConveyorInfrastructurePersistence
{
    DungeonConveyorInfrastructureSaveData Capture();
    ConveyorRestoreState PrepareRestore(
        DungeonConveyorInfrastructureSaveData snapshot);
    void Restore(ConveyorRestoreState candidate);
}

public interface IAutomationInfrastructureQuery
{
    int Version { get; }
    IReadOnlyList<AutomationFacilitySnapshot> Facilities { get; }
    bool TryGetFacility(
        BuildableObject facility,
        out AutomationFacilitySnapshot snapshot);
    float GetWorkSpeedMultiplier(BuildableObject facility);
}

public interface IAutomationInfrastructureCommand
{
    InfrastructureCommandResult SetMode(
        BuildableObject facility,
        AutomationMode mode);
    InfrastructureCommandResult Maintain(BuildableObject facility, float amount);
}

public interface IAutomationInfrastructurePersistence
{
    DungeonAutomationSaveData Capture();
    AutomationRestoreCandidate PrepareRestore(
        DungeonAutomationSaveData snapshot);
    void Restore(AutomationRestoreCandidate candidate);
}
