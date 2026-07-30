using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class FluidNodeState
{
    public float CleanWater;
    public float UnsafeWater;
    public float FoulWater;
    public float Wastewater;
    public float Blockage;
    public float Leak;
    public float ProcessorWork;
    public float ManualWaterReserve;
    public WaterContainerTransferMode TransferMode;
    public float TransferWork;
    public string TransferBlockedReason = string.Empty;
}

internal sealed class FluidNetworkRuntime :
    IWaterNetworkRuntime,
    IWastewaterNetworkRuntime,
    IPlumbingCommandService,
    ITickable
{
    private const float TickInterval = 0.5f;
    private const float BackflowWarningInterval = 5f;

    private readonly IIndustrialInfrastructureTopologyRuntime topologyRuntime;
    private readonly IElectricalNetworkRuntime power;
    private readonly IWorldItemStackRuntime items;
    private readonly IWorldFilthQuery filth;
    private readonly IGameClock clock;
    private readonly Dictionary<string, FluidNodeState> states =
        new Dictionary<string, FluidNodeState>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> nextBackflowAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private IReadOnlyList<FluidNetworkSnapshot> networks =
        Array.Empty<FluidNetworkSnapshot>();
    private IReadOnlyList<WaterTransferFacilitySnapshot> waterTransfers =
        Array.Empty<WaterTransferFacilitySnapshot>();
    private int topologyVersion = int.MinValue;
    private int snapshotVersion = int.MinValue;
    private float accumulated;

    public FluidNetworkRuntime(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IElectricalNetworkRuntime power,
        IWorldItemStackRuntime items,
        IWorldFilthQuery filth,
        IGameClock clock)
    {
        this.topologyRuntime = topologyRuntime
            ?? throw new ArgumentNullException(nameof(topologyRuntime));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public int Version { get; private set; }

    public IReadOnlyList<FluidNetworkSnapshot> Networks
    {
        get
        {
            EnsureTopology();
            EnsureSnapshots();
            return networks;
        }
    }

    public IReadOnlyList<WaterTransferFacilitySnapshot> WaterTransfers
    {
        get
        {
            EnsureTopology();
            RefreshWaterTransferSnapshots();
            return waterTransfers;
        }
    }

    public void Tick()
    {
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        EnsureTopology();
        accumulated += clock.DeltaTime;
        if (accumulated < TickInterval)
        {
            return;
        }

        float deltaTime = accumulated;
        accumulated = 0f;
        ProduceWater(deltaTime);
        TransferContainerWater(deltaTime);
        ProcessWastewater(deltaTime);
        ApplyLeaksAndBackflow(deltaTime);
        Touch();
    }

    public bool TryConsume(
        BuildableObject consumer,
        WorldWaterQuality minimumQuality,
        float amount,
        out WorldWaterQuality consumedQuality,
        out string failureReason)
    {
        consumedQuality = WorldWaterQuality.Foul;
        failureReason = string.Empty;
        if (amount <= 0f)
        {
            consumedQuality = WorldWaterQuality.Clean;
            return true;
        }

        if (!TryResolveNetwork(
                consumer,
                UtilityChannel.CleanWater,
                out string networkId,
                out _))
        {
            failureReason = "상수도에 연결되어 있지 않습니다.";
            return false;
        }

        WorldWaterQuality[] order = GetConsumptionOrder(minimumQuality);
        foreach (WorldWaterQuality quality in order)
        {
            float available = GetNetworkWater(networkId, quality);
            if (available + 0.0001f < amount)
            {
                continue;
            }

            RemoveNetworkWater(networkId, quality, amount);
            consumedQuality = quality;
            Touch();
            return true;
        }

        failureReason = minimumQuality == WorldWaterQuality.Clean
            ? "깨끗한 물이 부족합니다."
            : "사용 가능한 물이 부족합니다.";
        return false;
    }

    public bool CanConsume(
        BuildableObject consumer,
        WorldWaterQuality minimumQuality,
        float amount,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (amount <= 0f)
        {
            return true;
        }

        if (!TryResolveNetwork(
                consumer,
                UtilityChannel.CleanWater,
                out string networkId,
                out _))
        {
            failureReason = "상수도에 연결되어 있지 않습니다.";
            return false;
        }

        foreach (WorldWaterQuality quality in GetConsumptionOrder(minimumQuality))
        {
            if (GetNetworkWater(networkId, quality) + 0.0001f >= amount)
            {
                return true;
            }
        }

        failureReason = minimumQuality == WorldWaterQuality.Clean
            ? "깨끗한 물이 부족합니다."
            : "사용 가능한 물이 부족합니다.";
        return false;
    }

    public bool TryAdd(
        BuildableObject producer,
        WorldWaterQuality quality,
        float amount,
        out float accepted)
    {
        accepted = 0f;
        if (amount <= 0f)
        {
            return true;
        }

        if (!TryResolveNetwork(
                producer,
                UtilityChannel.CleanWater,
                out string networkId,
                out _))
        {
            return false;
        }

        accepted = AddNetworkWater(networkId, quality, amount);
        if (accepted > 0f)
        {
            Touch();
        }

        return accepted + 0.0001f >= amount;
    }

    public bool TryConsumeManualContainer(
        BuildableObject consumer,
        string destinationId,
        float amount,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (amount <= 0f)
        {
            return true;
        }

        EnsureTopology();
        if (!TryResolveState(consumer, out FluidNodeState state)
            || string.IsNullOrWhiteSpace(destinationId))
        {
            failureReason = "수동 물통 보충 지점을 찾을 수 없습니다.";
            return false;
        }

        int requiredContainers = Mathf.Max(
            0,
            Mathf.CeilToInt(amount - state.ManualWaterReserve - 0.0001f));
        if (requiredContainers > 0)
        {
            IReadOnlyDictionary<StockCategory, int> cost =
                new Dictionary<StockCategory, int>
                {
                    [StockCategory.Water] = requiredContainers
                };
            if (!items.TryConsumeFacilityBuffer(
                    destinationId.Trim(),
                    cost,
                    out _))
            {
                items.TryRequestFacilityDelivery(
                    StockCategory.Water,
                    requiredContainers,
                    consumer.centerPos,
                    destinationId.Trim(),
                    out _,
                    out _);
                failureReason = "물통 보충을 기다리는 중입니다.";
                return false;
            }

            state.ManualWaterReserve += requiredContainers;
        }

        state.ManualWaterReserve = Mathf.Max(
            0f,
            state.ManualWaterReserve - amount);
        Touch();
        return true;
    }

    public bool TryGetNetwork(
        BuildableObject building,
        out FluidNetworkSnapshot snapshot)
    {
        EnsureTopology();
        EnsureSnapshots();
        snapshot = null;
        if (building == null)
        {
            return false;
        }

        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!topology.NodeIdsByBuilding.TryGetValue(
                building,
                out string nodeId))
        {
            return false;
        }

        string cleanId = ResolveNetworkId(
            topology,
            UtilityChannel.CleanWater,
            nodeId);
        string wasteId = ResolveNetworkId(
            topology,
            UtilityChannel.Wastewater,
            nodeId);
        snapshot = networks.FirstOrDefault(candidate =>
            string.Equals(candidate.NetworkId, cleanId, StringComparison.Ordinal)
            || string.Equals(candidate.NetworkId, wasteId, StringComparison.Ordinal));
        return snapshot != null;
    }

    public bool TryAddWastewater(
        BuildableObject fixture,
        float amount,
        out float accepted,
        out string failureReason)
    {
        accepted = 0f;
        failureReason = string.Empty;
        if (amount <= 0f)
        {
            return true;
        }

        if (!TryResolveNetwork(
                fixture,
                UtilityChannel.Wastewater,
                out string networkId,
                out _))
        {
            CreateBackflow(fixture, amount);
            failureReason = "하수도에 연결되어 있지 않습니다.";
            return false;
        }

        float capacity = GetWastewaterCapacity(networkId);
        float current = GetNetworkWastewater(networkId);
        accepted = Mathf.Min(amount, Mathf.Max(0f, capacity - current));
        AddNetworkWastewater(networkId, accepted);
        if (accepted + 0.0001f < amount)
        {
            CreateBackflow(fixture, amount - accepted);
            failureReason = "하수 저장 공간이 가득 차 역류가 발생했습니다.";
        }

        Touch();
        return accepted + 0.0001f >= amount;
    }

    public bool TryConsumeWastewater(
        BuildableObject processor,
        float amount,
        out float consumed)
    {
        consumed = 0f;
        if (amount <= 0f)
        {
            return true;
        }

        if (!TryResolveNetwork(
                processor,
                UtilityChannel.Wastewater,
                out string networkId,
                out _))
        {
            return false;
        }

        consumed = RemoveNetworkWastewater(networkId, amount);
        if (consumed > 0f)
        {
            Touch();
        }

        return consumed + 0.0001f >= amount;
    }

    public bool CanAcceptWastewater(
        BuildableObject fixture,
        float amount,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (amount <= 0f)
        {
            return true;
        }

        if (!TryResolveNetwork(
                fixture,
                UtilityChannel.Wastewater,
                out string networkId,
                out _))
        {
            failureReason = "하수도에 연결되어 있지 않습니다.";
            return false;
        }

        float free = Mathf.Max(
            0f,
            GetWastewaterCapacity(networkId)
            - GetNetworkWastewater(networkId));
        if (free + 0.0001f >= amount)
        {
            return true;
        }

        failureReason = "오수 저장 공간이 가득 찼습니다.";
        return false;
    }

    public InfrastructureCommandResult ClearBlockage(
        BuildableObject building)
    {
        if (!TryResolveState(building, out FluidNodeState state))
        {
            return InfrastructureCommandResult.Failure(
                "배관 시설을 선택해야 합니다.");
        }

        state.Blockage = 0f;
        Touch();
        return InfrastructureCommandResult.Success("배관 막힘을 제거했습니다.");
    }

    public InfrastructureCommandResult SetWaterTransferMode(
        BuildableObject building,
        WaterContainerTransferMode mode)
    {
        if (!Enum.IsDefined(typeof(WaterContainerTransferMode), mode)
            || building?.BuildingData?.GetAbility<
                BuildingWaterContainerTransferAbility>() == null
            || !TryResolveState(building, out FluidNodeState state))
        {
            return InfrastructureCommandResult.Failure(
                "물통 충전소를 선택해야 합니다.");
        }

        state.TransferMode = mode;
        state.TransferWork = 0f;
        state.TransferBlockedReason = string.Empty;
        Touch();
        return InfrastructureCommandResult.Success(
            mode switch
            {
                WaterContainerTransferMode.BottleFromNetwork =>
                    "배관 물을 물통으로 병입합니다.",
                WaterContainerTransferMode.FeedNetwork =>
                    "물통을 운반받아 상수망에 투입합니다.",
                _ => "물통 충전소를 정지했습니다."
            });
    }

    public bool TryGetMaintenance(
        BuildableObject building,
        out float blockage,
        out float leak)
    {
        EnsureTopology();
        if (TryResolveState(building, out FluidNodeState state))
        {
            blockage = Mathf.Clamp(state.Blockage, 0f, 100f);
            leak = Mathf.Clamp(state.Leak, 0f, 100f);
            return true;
        }

        blockage = 0f;
        leak = 0f;
        return false;
    }

    public InfrastructureCommandResult RepairLeak(BuildableObject building)
    {
        if (!TryResolveState(building, out FluidNodeState state))
        {
            return InfrastructureCommandResult.Failure(
                "배관 시설을 선택해야 합니다.");
        }

        state.Leak = 0f;
        Touch();
        return InfrastructureCommandResult.Success("배관 누수를 수리했습니다.");
    }

    public DungeonFluidInfrastructureSaveData Capture()
    {
        EnsureTopology();
        return new DungeonFluidInfrastructureSaveData
        {
            nodes = states
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new FluidNodeSaveData
                {
                    nodeId = pair.Key,
                    cleanWater = pair.Value.CleanWater,
                    unsafeWater = pair.Value.UnsafeWater,
                    foulWater = pair.Value.FoulWater,
                    wastewater = pair.Value.Wastewater,
                    blockage = pair.Value.Blockage,
                    leak = pair.Value.Leak,
                    processorWork = pair.Value.ProcessorWork,
                    manualWaterReserve = pair.Value.ManualWaterReserve,
                    transferMode = pair.Value.TransferMode,
                    transferWork = pair.Value.TransferWork
                })
                .ToList()
        };
    }

    public void Restore(DungeonFluidInfrastructureSaveData snapshot)
    {
        states.Clear();
        foreach (FluidNodeSaveData saved in snapshot?.nodes
                 ?? new List<FluidNodeSaveData>())
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.nodeId))
            {
                continue;
            }

            states[saved.nodeId.Trim()] = new FluidNodeState
            {
                CleanWater = Mathf.Max(0f, saved.cleanWater),
                UnsafeWater = Mathf.Max(0f, saved.unsafeWater),
                FoulWater = Mathf.Max(0f, saved.foulWater),
                Wastewater = Mathf.Max(0f, saved.wastewater),
                Blockage = Mathf.Clamp(saved.blockage, 0f, 100f),
                Leak = Mathf.Clamp(saved.leak, 0f, 100f),
                ProcessorWork = Mathf.Max(0f, saved.processorWork),
                ManualWaterReserve = Mathf.Max(
                    0f,
                    saved.manualWaterReserve),
                TransferMode = Enum.IsDefined(
                    typeof(WaterContainerTransferMode),
                    saved.transferMode)
                        ? saved.transferMode
                        : WaterContainerTransferMode.Disabled,
                TransferWork = Mathf.Max(0f, saved.transferWork)
            };
        }

        topologyVersion = int.MinValue;
        EnsureTopology();
        Touch();
    }

    private void TransferContainerWater(float deltaTime)
    {
        foreach (IndustrialNodeDescriptor node in topologyRuntime.Current.Nodes
                     .Values.OrderBy(node => node.NodeId, StringComparer.Ordinal))
        {
            BuildingWaterContainerTransferAbility transfer =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterContainerTransferAbility>();
            if (transfer == null)
            {
                continue;
            }

            FluidNodeState state = EnsureState(node.NodeId);
            if (state.TransferMode == WaterContainerTransferMode.Disabled)
            {
                state.TransferWork = 0f;
                state.TransferBlockedReason = string.Empty;
                continue;
            }

            if (transfer.requiresPower && !power.IsPowered(node.Building))
            {
                state.TransferBlockedReason = "전력 부족";
                continue;
            }

            state.TransferWork += deltaTime * ResolveFlowMultiplier(node);
            float required = Mathf.Max(0.1f, transfer.secondsPerBatch);
            if (state.TransferWork + 0.0001f < required)
            {
                state.TransferBlockedReason = string.Empty;
                continue;
            }

            bool completed = state.TransferMode
                == WaterContainerTransferMode.BottleFromNetwork
                    ? TryBottleWater(node, state, transfer)
                    : TryFeedWaterNetwork(node, state, transfer);
            if (completed)
            {
                state.TransferWork = Mathf.Max(
                    0f,
                    state.TransferWork - required);
            }
            else
            {
                state.TransferWork = Mathf.Min(state.TransferWork, required);
            }
        }

    }

    private bool TryBottleWater(
        IndustrialNodeDescriptor node,
        FluidNodeState state,
        BuildingWaterContainerTransferAbility transfer)
    {
        string waterItemId =
            DungeonItemCatalogSO.StockItemId(StockCategory.Water);
        int currentStock = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    waterItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        if (currentStock >= Mathf.Max(1, transfer.bottleTargetStock))
        {
            state.TransferBlockedReason = "병입 목표 재고 충족";
            return false;
        }

        if (!TryConsume(
                node.Building,
                WorldWaterQuality.Clean,
                transfer.waterPerBatch,
                out _,
                out string failureReason))
        {
            state.TransferBlockedReason = failureReason;
            return false;
        }

        if (!items.SpawnItemAt(
                waterItemId,
                Mathf.Max(1, Mathf.RoundToInt(transfer.waterPerBatch)),
                node.Building.centerPos,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned <= 0)
        {
            TryAdd(
                node.Building,
                WorldWaterQuality.Clean,
                transfer.waterPerBatch,
                out _);
            state.TransferBlockedReason = "병입 출력 공간 부족";
            return false;
        }

        state.TransferBlockedReason = string.Empty;
        return true;
    }

    private bool TryFeedWaterNetwork(
        IndustrialNodeDescriptor node,
        FluidNodeState state,
        BuildingWaterContainerTransferAbility transfer)
    {
        if (!TryResolveNetwork(
                node.Building,
                UtilityChannel.CleanWater,
                out string networkId,
                out _)
            || GetNetworkWaterFreeCapacity(networkId)
                + 0.0001f < transfer.waterPerBatch)
        {
            state.TransferBlockedReason = "상수 저장 공간 부족";
            return false;
        }

        string destinationId = CreateWaterTransferDestinationId(node.NodeId);
        IReadOnlyDictionary<StockCategory, int> cost =
            new Dictionary<StockCategory, int>
            {
                [StockCategory.Water] =
                    Mathf.Max(1, Mathf.RoundToInt(transfer.waterPerBatch))
            };
        if (!items.TryConsumeFacilityBuffer(
                destinationId,
                cost,
                out _))
        {
            items.TryRequestFacilityDelivery(
                StockCategory.Water,
                cost[StockCategory.Water],
                node.Building.centerPos,
                destinationId,
                out _,
                out _);
            state.TransferBlockedReason = "투입할 물통 운반 중";
            return false;
        }

        float accepted = AddNetworkWater(
            networkId,
            WorldWaterQuality.Clean,
            transfer.waterPerBatch);
        state.TransferBlockedReason = accepted + 0.0001f
            >= transfer.waterPerBatch
                ? string.Empty
                : "상수 저장 공간 부족";
        return accepted + 0.0001f >= transfer.waterPerBatch;
    }

    private void RefreshWaterTransferSnapshots()
    {
        waterTransfers = topologyRuntime.Current.Nodes.Values
            .Where(node => node.Building?.BuildingData?.GetAbility<
                BuildingWaterContainerTransferAbility>() != null)
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .Select(node =>
            {
                BuildingWaterContainerTransferAbility ability =
                    node.Building.BuildingData.GetAbility<
                        BuildingWaterContainerTransferAbility>();
                FluidNodeState state = EnsureState(node.NodeId);
                return new WaterTransferFacilitySnapshot
                {
                    FacilityId = node.NodeId,
                    Mode = state.TransferMode,
                    Powered = !ability.requiresPower
                        || power.IsPowered(node.Building),
                    Progress01 = state.TransferMode
                        == WaterContainerTransferMode.Disabled
                            ? 0f
                            : Mathf.Clamp01(
                                state.TransferWork
                                / Mathf.Max(0.1f, ability.secondsPerBatch)),
                    BlockedReason = state.TransferBlockedReason
                };
            })
            .ToArray();
    }

    private static string CreateWaterTransferDestinationId(string nodeId) =>
        $"plumbing:water-transfer:{nodeId}";

    private void EnsureTopology()
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (topology.SourceVersion == topologyVersion)
        {
            return;
        }

        topologyVersion = topology.SourceVersion;
        foreach (IndustrialNodeDescriptor node in topology.Nodes.Values.Where(
                     node => (node.Channels
                             & (UtilityChannel.CleanWater
                                | UtilityChannel.Wastewater))
                         != 0))
        {
            FluidNodeState state = EnsureState(node.NodeId);
            BuildingWaterStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterStorageAbility>();
            if (storage != null)
            {
                float waterCapacity = Mathf.Max(
                    0f,
                    storage.cleanWaterCapacity);
                ClampWater(state, waterCapacity);
                state.Wastewater = Mathf.Min(
                    state.Wastewater,
                    Mathf.Max(0f, storage.wastewaterCapacity));
            }
        }

        Touch();
    }

    private void ProduceWater(float deltaTime)
    {
        foreach (IndustrialNodeDescriptor node in topologyRuntime.Current.Nodes
                     .Values.OrderBy(node => node.NodeId, StringComparer.Ordinal))
        {
            BuildingWaterProducerAbility producer =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterProducerAbility>();
            if (producer == null
                || producer.productionPerSecond <= 0f
                || producer.requiresPower && !power.IsPowered(node.Building))
            {
                continue;
            }

            if (!TryResolveNetwork(
                    node.Building,
                    UtilityChannel.CleanWater,
                    out string networkId,
                    out _))
            {
                continue;
            }

            float rate = producer.productionPerSecond
                * ResolveFlowMultiplier(node);
            AddNetworkWater(
                networkId,
                producer.quality,
                rate * deltaTime);
        }
    }

    private void ProcessWastewater(float deltaTime)
    {
        foreach (IndustrialNodeDescriptor node in topologyRuntime.Current.Nodes
                     .Values.OrderBy(node => node.NodeId, StringComparer.Ordinal))
        {
            BuildingWastewaterProcessorAbility processor =
                node.Building.BuildingData
                    .GetAbility<BuildingWastewaterProcessorAbility>();
            if (processor == null
                || processor.requiresPower && !power.IsPowered(node.Building))
            {
                continue;
            }

            if (!TryResolveNetwork(
                    node.Building,
                    UtilityChannel.Wastewater,
                    out string wasteNetworkId,
                    out _)
                || !TryResolveNetwork(
                    node.Building,
                    UtilityChannel.CleanWater,
                    out string waterNetworkId,
                    out _)
                || GetNetworkWastewater(wasteNetworkId)
                    + 0.0001f < processor.wastewaterInput
                || GetNetworkWaterFreeCapacity(waterNetworkId)
                    + 0.0001f < processor.waterOutput)
            {
                continue;
            }

            FluidNodeState state = EnsureState(node.NodeId);
            state.ProcessorWork += deltaTime * ResolveFlowMultiplier(node);
            float required = Mathf.Max(0.1f, processor.secondsPerBatch);
            while (state.ProcessorWork + 0.0001f >= required
                   && GetNetworkWastewater(wasteNetworkId)
                       + 0.0001f >= processor.wastewaterInput
                   && GetNetworkWaterFreeCapacity(waterNetworkId)
                       + 0.0001f >= processor.waterOutput)
            {
                RemoveNetworkWastewater(
                    wasteNetworkId,
                    processor.wastewaterInput);
                AddNetworkWater(
                    waterNetworkId,
                    processor.outputQuality,
                    processor.waterOutput);
                state.ProcessorWork -= required;
                if (processor.sludgeAmount > 0
                    && !string.IsNullOrWhiteSpace(processor.sludgeItemId))
                {
                    items.SpawnItemAt(
                        processor.sludgeItemId.Trim(),
                        processor.sludgeAmount,
                        node.Building.centerPos,
                        WorldItemStackState.Loose,
                        string.Empty,
                        out _);
                }
            }
        }
    }

    private void ApplyLeaksAndBackflow(float deltaTime)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        foreach (IndustrialNodeDescriptor node in topology.Nodes.Values)
        {
            FluidNodeState state = EnsureState(node.NodeId);
            if (state.Leak > 0f)
            {
                float leaked = Mathf.Min(
                    state.CleanWater + state.UnsafeWater + state.FoulWater,
                    state.Leak * 0.001f * deltaTime);
                RemoveFromNode(state, WorldWaterQuality.Foul, leaked);
                if (leaked > 0.02f)
                {
                    filth.AddFilth(
                        WorldFilthType.Sewage,
                        node.Building.centerPos,
                        leaked * 2f,
                        string.Empty,
                        0.25f);
                }
            }
        }

        if (!topology.NodesByNetwork.TryGetValue(
                UtilityChannel.Wastewater,
                out Dictionary<string, List<string>> networksByNode))
        {
            return;
        }

        foreach (KeyValuePair<string, List<string>> network in networksByNode)
        {
            float capacity = GetWastewaterCapacity(network.Key);
            if (capacity <= 0f
                || GetNetworkWastewater(network.Key)
                    < capacity - 0.001f
                || clock.Time < nextBackflowAt.GetValueOrDefault(
                    network.Key,
                    0f))
            {
                continue;
            }

            IndustrialNodeDescriptor target = network.Value
                .Where(topology.Nodes.ContainsKey)
                .Select(id => topology.Nodes[id])
                .OrderBy(node => node.NodeId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (target != null)
            {
                CreateBackflow(target.Building, 1f);
            }

            nextBackflowAt[network.Key] =
                clock.Time + BackflowWarningInterval;
        }
    }

    private void CreateBackflow(BuildableObject building, float amount)
    {
        if (building == null || amount <= 0f)
        {
            return;
        }

        if (TryResolveState(building, out FluidNodeState state))
        {
            state.Blockage = Mathf.Clamp(
                state.Blockage + Mathf.Max(1f, amount * 2f),
                0f,
                100f);
        }

        filth.AddFilth(
            WorldFilthType.Sewage,
            building.centerPos,
            Mathf.Max(2f, amount * 8f),
            string.Empty,
            Mathf.Clamp01(0.35f + amount * 0.05f));
    }

    private float ResolveFlowMultiplier(IndustrialNodeDescriptor node)
    {
        FluidNodeState state = EnsureState(node.NodeId);
        return Mathf.Clamp01(
            1f - state.Blockage / 100f - state.FaultEquivalent());
    }

    private float AddNetworkWater(
        string networkId,
        WorldWaterQuality quality,
        float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (IndustrialNodeDescriptor node in GetNetworkNodes(
                     UtilityChannel.CleanWater,
                     networkId))
        {
            BuildingWaterStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterStorageAbility>();
            if (storage == null || storage.cleanWaterCapacity <= 0f)
            {
                continue;
            }

            FluidNodeState state = EnsureState(node.NodeId);
            float free = Mathf.Max(
                0f,
                storage.cleanWaterCapacity
                - state.CleanWater
                - state.UnsafeWater
                - state.FoulWater);
            float accepted = Mathf.Min(free, remaining);
            AddToNode(state, quality, accepted);
            remaining -= accepted;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }

        return amount - remaining;
    }

    private float GetNetworkWater(
        string networkId,
        WorldWaterQuality quality)
    {
        return GetNetworkNodes(UtilityChannel.CleanWater, networkId)
            .Sum(node => GetNodeWater(EnsureState(node.NodeId), quality));
    }

    private float GetNetworkWaterFreeCapacity(string networkId)
    {
        return GetNetworkNodes(UtilityChannel.CleanWater, networkId)
            .Sum(node =>
            {
                BuildingWaterStorageAbility storage =
                    node.Building.BuildingData
                        .GetAbility<BuildingWaterStorageAbility>();
                FluidNodeState state = EnsureState(node.NodeId);
                return storage == null
                    ? 0f
                    : Mathf.Max(
                        0f,
                        storage.cleanWaterCapacity
                        - state.CleanWater
                        - state.UnsafeWater
                        - state.FoulWater);
            });
    }

    private void RemoveNetworkWater(
        string networkId,
        WorldWaterQuality quality,
        float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (IndustrialNodeDescriptor node in GetNetworkNodes(
                     UtilityChannel.CleanWater,
                     networkId))
        {
            FluidNodeState state = EnsureState(node.NodeId);
            float removed = Mathf.Min(
                GetNodeWater(state, quality),
                remaining);
            SetNodeWater(
                state,
                quality,
                GetNodeWater(state, quality) - removed);
            remaining -= removed;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }
    }

    private float GetNetworkWastewater(string networkId)
    {
        return GetNetworkNodes(UtilityChannel.Wastewater, networkId)
            .Sum(node => EnsureState(node.NodeId).Wastewater);
    }

    private float GetWastewaterCapacity(string networkId)
    {
        return GetNetworkNodes(UtilityChannel.Wastewater, networkId)
            .Sum(node => Mathf.Max(
                0f,
                node.Building.BuildingData
                    .GetAbility<BuildingWaterStorageAbility>()
                    ?.wastewaterCapacity
                ?? 0f));
    }

    private void AddNetworkWastewater(string networkId, float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (IndustrialNodeDescriptor node in GetNetworkNodes(
                     UtilityChannel.Wastewater,
                     networkId))
        {
            BuildingWaterStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterStorageAbility>();
            if (storage == null || storage.wastewaterCapacity <= 0f)
            {
                continue;
            }

            FluidNodeState state = EnsureState(node.NodeId);
            float accepted = Mathf.Min(
                Mathf.Max(
                    0f,
                    storage.wastewaterCapacity - state.Wastewater),
                remaining);
            state.Wastewater += accepted;
            remaining -= accepted;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }
    }

    private float RemoveNetworkWastewater(string networkId, float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (IndustrialNodeDescriptor node in GetNetworkNodes(
                     UtilityChannel.Wastewater,
                     networkId))
        {
            FluidNodeState state = EnsureState(node.NodeId);
            float removed = Mathf.Min(state.Wastewater, remaining);
            state.Wastewater -= removed;
            remaining -= removed;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }

        return amount - remaining;
    }

    private IReadOnlyList<IndustrialNodeDescriptor> GetNetworkNodes(
        UtilityChannel channel,
        string networkId)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        return topology.NodeDescriptorsByNetwork.TryGetValue(
            channel,
            out Dictionary<
                string,
                IReadOnlyList<IndustrialNodeDescriptor>> byNetwork)
            && byNetwork.TryGetValue(
                networkId,
                out IReadOnlyList<IndustrialNodeDescriptor> nodes)
                ? nodes
                : Array.Empty<IndustrialNodeDescriptor>();
    }

    private void RefreshSnapshots()
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        List<FluidNetworkSnapshot> result =
            new List<FluidNetworkSnapshot>();
        foreach (UtilityChannel channel in new[]
                 {
                     UtilityChannel.CleanWater,
                     UtilityChannel.Wastewater
                 })
        {
            if (!topology.NodesByNetwork.TryGetValue(
                    channel,
                    out Dictionary<string, List<string>> byNetwork))
            {
                continue;
            }

            foreach (KeyValuePair<string, List<string>> pair in byNetwork
                         .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                IReadOnlyList<IndustrialNodeDescriptor> nodes =
                    GetNetworkNodes(channel, pair.Key);
                float waterCapacity = nodes.Sum(node =>
                    Mathf.Max(
                        0f,
                        node.Building.BuildingData
                            .GetAbility<BuildingWaterStorageAbility>()
                            ?.cleanWaterCapacity
                        ?? 0f));
                float wastewaterCapacity = nodes.Sum(node =>
                    Mathf.Max(
                        0f,
                        node.Building.BuildingData
                            .GetAbility<BuildingWaterStorageAbility>()
                            ?.wastewaterCapacity
                        ?? 0f));
                result.Add(new FluidNetworkSnapshot
                {
                    NetworkId = pair.Key,
                    Channel = channel,
                    CleanWater = nodes.Sum(
                        node => EnsureState(node.NodeId).CleanWater),
                    UnsafeWater = nodes.Sum(
                        node => EnsureState(node.NodeId).UnsafeWater),
                    FoulWater = nodes.Sum(
                        node => EnsureState(node.NodeId).FoulWater),
                    Wastewater = nodes.Sum(
                        node => EnsureState(node.NodeId).Wastewater),
                    Capacity = channel == UtilityChannel.CleanWater
                        ? waterCapacity
                        : wastewaterCapacity,
                    Blockage = nodes.Count == 0
                        ? 0f
                        : nodes.Average(
                            node => EnsureState(node.NodeId).Blockage),
                    Leak = nodes.Count == 0
                        ? 0f
                        : nodes.Average(
                            node => EnsureState(node.NodeId).Leak),
                    HasOverflowRisk =
                        channel == UtilityChannel.Wastewater
                        && wastewaterCapacity > 0f
                        && nodes.Sum(
                                node => EnsureState(node.NodeId).Wastewater)
                            >= wastewaterCapacity - 0.001f
                });
            }
        }

        networks = result;
        snapshotVersion = Version;
    }

    private void EnsureSnapshots()
    {
        if (snapshotVersion != Version)
        {
            RefreshSnapshots();
        }
    }

    private bool TryResolveNetwork(
        BuildableObject building,
        UtilityChannel channel,
        out string networkId,
        out IndustrialNodeDescriptor node)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        networkId = string.Empty;
        node = null;
        if (building == null
            || !topology.NodeIdsByBuilding.TryGetValue(
                building,
                out string nodeId)
            || !topology.Nodes.TryGetValue(nodeId, out node)
            || (node.Channels & channel) == 0)
        {
            return false;
        }

        networkId = ResolveNetworkId(topology, channel, nodeId);
        return !string.IsNullOrWhiteSpace(networkId);
    }

    private bool TryResolveState(
        BuildableObject building,
        out FluidNodeState state)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (building != null
            && topology.NodeIdsByBuilding.TryGetValue(
                building,
                out string nodeId)
            && topology.Nodes.TryGetValue(nodeId, out IndustrialNodeDescriptor node)
            && (node.Channels
                    & (UtilityChannel.CleanWater
                       | UtilityChannel.Wastewater))
                != 0)
        {
            state = EnsureState(nodeId);
            return true;
        }

        state = null;
        return false;
    }

    private static string ResolveNetworkId(
        IndustrialTopologySnapshot topology,
        UtilityChannel channel,
        string nodeId)
    {
        return topology.NetworkByNode.TryGetValue(
                channel,
                out Dictionary<string, string> byNode)
            && byNode.TryGetValue(nodeId, out string networkId)
                ? networkId
                : string.Empty;
    }

    private FluidNodeState EnsureState(string nodeId)
    {
        if (!states.TryGetValue(nodeId, out FluidNodeState state))
        {
            state = new FluidNodeState();
            states[nodeId] = state;
        }

        return state;
    }

    private static WorldWaterQuality[] GetConsumptionOrder(
        WorldWaterQuality minimumQuality)
    {
        return minimumQuality switch
        {
            WorldWaterQuality.Clean => new[] { WorldWaterQuality.Clean },
            WorldWaterQuality.Unsafe => new[]
            {
                WorldWaterQuality.Unsafe,
                WorldWaterQuality.Clean
            },
            _ => new[]
            {
                WorldWaterQuality.Foul,
                WorldWaterQuality.Unsafe,
                WorldWaterQuality.Clean
            }
        };
    }

    private static float GetNodeWater(
        FluidNodeState state,
        WorldWaterQuality quality)
    {
        return quality switch
        {
            WorldWaterQuality.Clean => state.CleanWater,
            WorldWaterQuality.Unsafe => state.UnsafeWater,
            _ => state.FoulWater
        };
    }

    private static void SetNodeWater(
        FluidNodeState state,
        WorldWaterQuality quality,
        float value)
    {
        switch (quality)
        {
            case WorldWaterQuality.Clean:
                state.CleanWater = Mathf.Max(0f, value);
                break;
            case WorldWaterQuality.Unsafe:
                state.UnsafeWater = Mathf.Max(0f, value);
                break;
            default:
                state.FoulWater = Mathf.Max(0f, value);
                break;
        }
    }

    private static void AddToNode(
        FluidNodeState state,
        WorldWaterQuality quality,
        float amount)
    {
        SetNodeWater(
            state,
            quality,
            GetNodeWater(state, quality) + Mathf.Max(0f, amount));
    }

    private static void RemoveFromNode(
        FluidNodeState state,
        WorldWaterQuality preferredQuality,
        float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (WorldWaterQuality quality in GetConsumptionOrder(
                     preferredQuality))
        {
            float removed = Mathf.Min(
                GetNodeWater(state, quality),
                remaining);
            SetNodeWater(
                state,
                quality,
                GetNodeWater(state, quality) - removed);
            remaining -= removed;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }
    }

    private static void ClampWater(FluidNodeState state, float capacity)
    {
        float total =
            state.CleanWater + state.UnsafeWater + state.FoulWater;
        if (total <= capacity || total <= 0f)
        {
            return;
        }

        float multiplier = capacity / total;
        state.CleanWater *= multiplier;
        state.UnsafeWater *= multiplier;
        state.FoulWater *= multiplier;
    }

    private void Touch()
    {
        unchecked
        {
            Version++;
        }
    }
}

internal static class FluidNodeStateExtensions
{
    public static float FaultEquivalent(this FluidNodeState state)
    {
        return state == null ? 0f : Mathf.Clamp01(state.Leak / 200f);
    }
}
