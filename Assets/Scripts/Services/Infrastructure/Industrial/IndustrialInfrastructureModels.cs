using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum UtilityChannel
{
    None = 0,
    Power = 1 << 0,
    CleanWater = 1 << 1,
    Wastewater = 1 << 2
}

public enum PowerPriority
{
    Critical = 1,
    Defense = 2,
    Essential = 3,
    Production = 4,
    Optional = 5
}

public enum AutomationMode
{
    Manual = 0,
    PoweredAssist = 1,
    Automatic = 2
}

public enum WaterContainerTransferMode
{
    Disabled = 0,
    BottleFromNetwork = 1,
    FeedNetwork = 2
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
    public string nodeId = string.Empty;
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
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<PowerNodeSaveData> nodes = new List<PowerNodeSaveData>();
}

[Serializable]
public sealed class FluidNodeSaveData
{
    public string nodeId = string.Empty;
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
    public const int CurrentVersion = 3;
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
        CombatEquipmentQuality.Legendary;
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
    public int maximumQuality = (int)CombatEquipmentQuality.Legendary;
    public bool filterFreshness;
    public float minimumFreshness01;
    public float maximumFreshness01 = 1f;
    public bool allowContaminated = true;
}

[Serializable]
public sealed class ConveyorPayloadSaveData
{
    public string payloadId = string.Empty;
    public string segmentNodeId = string.Empty;
    public string previousNodeId = string.Empty;
    public string destinationId = string.Empty;
    public float progress;
    public float lastMovedAt;
    public float stalledSince;
    public int routeVersion;
    public ConveyorStallReason stallReason;
    public WorldItemStackSaveData stack = new WorldItemStackSaveData();
}

[Serializable]
public sealed class ConveyorNodeSaveData
{
    public string nodeId = string.Empty;
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
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public int nextPayloadSequence = 1;
    public List<ConveyorNodeSaveData> nodes =
        new List<ConveyorNodeSaveData>();
    public List<ConveyorPayloadSaveData> payloads =
        new List<ConveyorPayloadSaveData>();
}

[Serializable]
public sealed class AutomationFacilitySaveData
{
    public string facilityId = string.Empty;
    public AutomationMode mode;
    public float maintenance;
    public float fault;
}

[Serializable]
public sealed class DungeonAutomationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<AutomationFacilitySaveData> facilities =
        new List<AutomationFacilitySaveData>();
}

public sealed class PowerNodeSnapshot
{
    public string NodeId { get; set; } = string.Empty;
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

public sealed class FluidNetworkSnapshot
{
    public string NetworkId { get; set; } = string.Empty;
    public UtilityChannel Channel { get; set; }
    public float CleanWater { get; set; }
    public float UnsafeWater { get; set; }
    public float FoulWater { get; set; }
    public float Wastewater { get; set; }
    public float Capacity { get; set; }
    public float Blockage { get; set; }
    public float Leak { get; set; }
    public bool HasOverflowRisk { get; set; }
}

public sealed class WaterTransferFacilitySnapshot
{
    public string FacilityId { get; set; } = string.Empty;
    public WaterContainerTransferMode Mode { get; set; }
    public bool Powered { get; set; }
    public float Progress01 { get; set; }
    public string BlockedReason { get; set; } = string.Empty;
}

public sealed class ConveyorPayloadSnapshot
{
    public string PayloadId { get; set; } = string.Empty;
    public string StackId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string SegmentNodeId { get; set; } = string.Empty;
    public string DestinationId { get; set; } = string.Empty;
    public float Progress { get; set; }
    public float StalledSeconds { get; set; }
    public ConveyorStallReason StallReason { get; set; }
}

public sealed class ConveyorNodeSnapshot
{
    public string NodeId { get; set; } = string.Empty;
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
    public string PlannedOverflowNodeId { get; set; } = string.Empty;
    public IReadOnlyList<ConveyorPayloadSnapshot> Payloads { get; set; } =
        Array.Empty<ConveyorPayloadSnapshot>();
    public IReadOnlyList<ConveyorNodeSnapshot> Nodes { get; set; } =
        Array.Empty<ConveyorNodeSnapshot>();
}

public sealed class AutomationFacilitySnapshot
{
    public string FacilityId { get; set; } = string.Empty;
    public AutomationMode Mode { get; set; }
    public bool Powered { get; set; }
    public bool Operational { get; set; }
    public float WorkRate { get; set; }
    public float Maintenance { get; set; }
    public float Fault { get; set; }
    public string BlockedReason { get; set; } = string.Empty;
}

public readonly struct InfrastructureCommandResult
{
    public InfrastructureCommandResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string Message { get; }

    public static InfrastructureCommandResult Success(string message = "") =>
        new InfrastructureCommandResult(true, message);

    public static InfrastructureCommandResult Failure(string message) =>
        new InfrastructureCommandResult(false, message);
}

public interface IElectricalNetworkRuntime
{
    int Version { get; }
    IReadOnlyList<PowerNetworkSnapshot> Networks { get; }
    bool IsPowered(BuildableObject building);
    bool TryGetNode(BuildableObject building, out PowerNodeSnapshot snapshot);
    DungeonPowerInfrastructureSaveData Capture();
    void Restore(DungeonPowerInfrastructureSaveData snapshot);
}

public interface IPowerPriorityCommandService
{
    InfrastructureCommandResult SetPriority(
        BuildableObject building,
        PowerPriority priority);
    InfrastructureCommandResult ResetBreaker(BuildableObject building);
}

public interface IWaterNetworkRuntime
{
    int Version { get; }
    IReadOnlyList<FluidNetworkSnapshot> Networks { get; }
    bool TryConsume(
        BuildableObject consumer,
        WorldWaterQuality minimumQuality,
        float amount,
        out WorldWaterQuality consumedQuality,
        out string failureReason);
    bool CanConsume(
        BuildableObject consumer,
        WorldWaterQuality minimumQuality,
        float amount,
        out string failureReason);
    bool TryAdd(
        BuildableObject producer,
        WorldWaterQuality quality,
        float amount,
        out float accepted);
    bool TryConsumeManualContainer(
        BuildableObject consumer,
        string destinationId,
        float amount,
        out string failureReason);
    bool TryGetNetwork(
        BuildableObject building,
        out FluidNetworkSnapshot snapshot);
    DungeonFluidInfrastructureSaveData Capture();
    void Restore(DungeonFluidInfrastructureSaveData snapshot);
}

public interface IWastewaterNetworkRuntime
{
    bool TryAddWastewater(
        BuildableObject fixture,
        float amount,
        out float accepted,
        out string failureReason);
    bool TryConsumeWastewater(
        BuildableObject processor,
        float amount,
        out float consumed);
    bool CanAcceptWastewater(
        BuildableObject fixture,
        float amount,
        out string failureReason);
}

public interface IPlumbingCommandService
{
    IReadOnlyList<WaterTransferFacilitySnapshot> WaterTransfers { get; }
    bool TryGetMaintenance(
        BuildableObject building,
        out float blockage,
        out float leak);
    InfrastructureCommandResult SetWaterTransferMode(
        BuildableObject building,
        WaterContainerTransferMode mode);
    InfrastructureCommandResult ClearBlockage(BuildableObject building);
    InfrastructureCommandResult RepairLeak(BuildableObject building);
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
        string fixtureId,
        WaterFixtureSupplyKind supplyKind,
        float wastewaterAmount)
    {
        FixtureId = fixtureId ?? string.Empty;
        SupplyKind = supplyKind;
        WastewaterAmount = Mathf.Max(0f, wastewaterAmount);
    }

    public string FixtureId { get; }
    public WaterFixtureSupplyKind SupplyKind { get; }
    public float WastewaterAmount { get; }
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(FixtureId)
        && SupplyKind != WaterFixtureSupplyKind.None;
}

public interface IWaterFixtureUseRuntime
{
    bool TryBeginUse(
        BuildableObject fixture,
        out WaterFixtureUseTicket ticket,
        out string failureReason);
    void CompleteUse(
        BuildableObject fixture,
        WaterFixtureUseTicket ticket);
}

public interface IProcessFluidUseRuntime
{
    bool TryConsumeCycle(
        BuildableObject facility,
        WorkTypeId workTypeId,
        out string failureReason);
}

public interface IConveyorRuntime
{
    int Version { get; }
    IReadOnlyList<ConveyorNetworkSnapshot> Networks { get; }
    bool TryLoadStack(
        string stackId,
        BuildableObject inputPort,
        string destinationId,
        out string payloadId,
        out string failureReason);
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
    DungeonConveyorInfrastructureSaveData Capture();
    void Restore(DungeonConveyorInfrastructureSaveData snapshot);
}

public interface IConveyorRoutingService
{
    bool TryFindRoute(
        string fromNodeId,
        string destinationId,
        WorldItemStackSaveData stack,
        out IReadOnlyList<string> nodeIds,
        out ConveyorStallReason failureReason);
}

public interface IConveyorCommandService : IConveyorRuntime
{
}

public interface IAutomationRuntime
{
    int Version { get; }
    IReadOnlyList<AutomationFacilitySnapshot> Facilities { get; }
    bool TryGetFacility(
        BuildableObject facility,
        out AutomationFacilitySnapshot snapshot);
    InfrastructureCommandResult SetMode(
        BuildableObject facility,
        AutomationMode mode);
    InfrastructureCommandResult Maintain(BuildableObject facility, float amount);
    float GetWorkSpeedMultiplier(BuildableObject facility);
    DungeonAutomationSaveData Capture();
    void Restore(DungeonAutomationSaveData snapshot);
}
