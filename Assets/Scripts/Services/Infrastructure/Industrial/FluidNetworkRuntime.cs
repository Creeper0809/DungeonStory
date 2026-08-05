using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class FluidNetworkRuntime :
    IFluidInfrastructureQuery,
    IFluidInfrastructureTransaction,
    IFluidWastewaterTransaction,
    IFluidInfrastructureCommand,
    IFluidInfrastructurePersistence,
    ITickable
{
    private const float TickInterval = 0.5f;
    private const float BackflowWarningInterval = 5f;
    private const string BottledCleanWaterItemId = "resource:clean-water";

    private readonly IIndustrialInfrastructureTopologyRuntime topologyRuntime;
    private readonly IPowerInfrastructureQuery power;
    private readonly IWorldItemStackRuntime items;
    private readonly IWorldFilthQuery filth;
    private readonly IGameClock clock;
    private readonly FluidNetworkStateStore stateStore;
    private readonly FluidNetworkProjectionAdapter projectionAdapter;
    private readonly Dictionary<string, float> nextBackflowAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private IReadOnlyList<WaterTransferFacilitySnapshot> waterTransfers =
        Array.Empty<WaterTransferFacilitySnapshot>();
    private float accumulated;

    public FluidNetworkRuntime(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IPowerInfrastructureQuery power,
        IWorldItemStackRuntime items,
        IWorldFilthQuery filth,
        IGameClock clock,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.topologyRuntime = topologyRuntime
            ?? throw new ArgumentNullException(nameof(topologyRuntime));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        stateStore = new FluidNetworkStateStore(
            aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore)));
        projectionAdapter = new FluidNetworkProjectionAdapter(
            this.topologyRuntime,
            stateStore);
    }

    public int Version => stateStore.Version;

    public IReadOnlyList<FluidNetworkSnapshot> Networks
    {
        get
        {
            EnsureTopology();
            return projectionAdapter.GetSnapshots(Version);
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
        stateStore.Touch();
    }

    public bool TryConsume(
        BuildableObject consumer,
        WorldWaterQuality minimumQuality,
        float amount,
        out WorldWaterQuality consumedQuality,
        out DomainFailure failure)
    {
        consumedQuality = WorldWaterQuality.Foul;
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            consumedQuality = WorldWaterQuality.Clean;
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                consumer,
                UtilityChannel.CleanWater,
                out string networkId,
                out _))
        {
            failure = new DomainFailure(FailureCode.FluidNetworkUnavailable);
            return false;
        }

        WorldWaterQuality[] order =
            FluidNodeWaterRules.GetConsumptionOrder(minimumQuality);
        foreach (WorldWaterQuality quality in order)
        {
            float available = GetNetworkWater(networkId, quality);
            if (available + 0.0001f < amount)
            {
                continue;
            }

            RemoveNetworkWater(networkId, quality, amount);
            consumedQuality = quality;
            stateStore.Touch();
            return true;
        }

        failure = new DomainFailure(
            FailureCode.FluidInsufficientWater,
            minimumQuality.ToString(),
            amount.ToString("0.###"));
        return false;
    }

    public bool CanConsume(
        BuildableObject consumer,
        WorldWaterQuality minimumQuality,
        float amount,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                consumer,
                UtilityChannel.CleanWater,
                out string networkId,
                out _))
        {
            failure = new DomainFailure(FailureCode.FluidNetworkUnavailable);
            return false;
        }

        foreach (WorldWaterQuality quality in
                 FluidNodeWaterRules.GetConsumptionOrder(minimumQuality))
        {
            if (GetNetworkWater(networkId, quality) + 0.0001f >= amount)
            {
                return true;
            }
        }

        failure = new DomainFailure(
            FailureCode.FluidInsufficientWater,
            minimumQuality.ToString(),
            amount.ToString("0.###"));
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

        if (!projectionAdapter.TryResolveNetwork(
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
            stateStore.Touch();
        }

        return accepted + 0.0001f >= amount;
    }

    public bool TryConsumeManualContainer(
        BuildableObject consumer,
        string destinationId,
        float amount,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            return true;
        }

        EnsureTopology();
        if (!projectionAdapter.TryResolveState(
                consumer,
                out FluidNodeState state)
            || string.IsNullOrWhiteSpace(destinationId))
        {
            failure = new DomainFailure(
                FailureCode.FluidManualWaterUnavailable);
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
                failure = new DomainFailure(
                    FailureCode.FluidManualWaterUnavailable,
                    destinationId.Trim());
                return false;
            }

            state.ManualWaterReserve += requiredContainers;
        }

        state.ManualWaterReserve = Mathf.Max(
            0f,
            state.ManualWaterReserve - amount);
        stateStore.Touch();
        return true;
    }

    public bool TryGetNetwork(
        BuildableObject building,
        out FluidNetworkSnapshot snapshot)
    {
        EnsureTopology();
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

        string cleanId = projectionAdapter.ResolveNetworkId(
            topology,
            UtilityChannel.CleanWater,
            nodeId);
        string wasteId = projectionAdapter.ResolveNetworkId(
            topology,
            UtilityChannel.Wastewater,
            nodeId);
        snapshot = projectionAdapter.GetSnapshots(Version).FirstOrDefault(candidate =>
            string.Equals(candidate.NetworkId, cleanId, StringComparison.Ordinal)
            || string.Equals(candidate.NetworkId, wasteId, StringComparison.Ordinal));
        return snapshot != null;
    }

    public bool TryAddWastewater(
        BuildableObject fixture,
        float amount,
        out float accepted,
        out DomainFailure failure)
    {
        accepted = 0f;
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                fixture,
                UtilityChannel.Wastewater,
                out string networkId,
                out _))
        {
            CreateBackflow(fixture, amount);
            failure = new DomainFailure(
                FailureCode.FluidWastewaterUnavailable);
            return false;
        }

        float capacity = GetWastewaterCapacity(networkId);
        float current = GetNetworkWastewater(networkId);
        accepted = Mathf.Min(amount, Mathf.Max(0f, capacity - current));
        AddNetworkWastewater(networkId, accepted);
        if (accepted + 0.0001f < amount)
        {
            CreateBackflow(fixture, amount - accepted);
            failure = new DomainFailure(
                FailureCode.FluidWastewaterUnavailable,
                amount.ToString("0.###"),
                accepted.ToString("0.###"));
        }

        stateStore.Touch();
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

        if (!projectionAdapter.TryResolveNetwork(
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
            stateStore.Touch();
        }

        return consumed + 0.0001f >= amount;
    }

    public bool CanAcceptWastewater(
        BuildableObject fixture,
        float amount,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (amount <= 0f)
        {
            return true;
        }

        if (!projectionAdapter.TryResolveNetwork(
                fixture,
                UtilityChannel.Wastewater,
                out string networkId,
                out _))
        {
            failure = new DomainFailure(
                FailureCode.FluidWastewaterUnavailable);
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

        failure = new DomainFailure(
            FailureCode.FluidWastewaterUnavailable,
            amount.ToString("0.###"));
        return false;
    }

    public InfrastructureCommandResult ClearBlockage(
        BuildableObject building)
    {
        if (!projectionAdapter.TryResolveState(
                building,
                out FluidNodeState state))
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.FluidMaintenanceUnavailable);
        }

        state.Blockage = 0f;
        stateStore.Touch();
        return InfrastructureCommandResult.Success();
    }

    public InfrastructureCommandResult SetWaterTransferMode(
        BuildableObject building,
        WaterContainerTransferMode mode)
    {
        if (!Enum.IsDefined(typeof(WaterContainerTransferMode), mode)
            || building?.BuildingData?.GetAbility<
                BuildingWaterContainerTransferAbility>() == null
            || !projectionAdapter.TryResolveState(
                building,
                out FluidNodeState state))
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.IndustrialCommandInvalid);
        }

        state.TransferMode = mode;
        state.TransferWork = 0f;
        state.TransferStatus = InfrastructureStatus.None;
        stateStore.Touch();
        return InfrastructureCommandResult.Success();
    }

    public bool TryGetMaintenance(
        BuildableObject building,
        out float blockage,
        out float leak)
    {
        EnsureTopology();
        if (projectionAdapter.TryResolveState(
                building,
                out FluidNodeState state))
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
        if (!projectionAdapter.TryResolveState(
                building,
                out FluidNodeState state))
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.FluidMaintenanceUnavailable);
        }

        state.Leak = 0f;
        stateStore.Touch();
        return InfrastructureCommandResult.Success();
    }

    public DungeonFluidInfrastructureSaveData Capture()
    {
        EnsureTopology();
        return new DungeonFluidInfrastructureSaveData
        {
            nodes = stateStore.Nodes
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new FluidNodeSaveData
                {
                    buildingInstanceId = pair.Key,
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

    public FluidNetworkRestoreCandidate PrepareRestore(
        DungeonFluidInfrastructureSaveData snapshot)
    {
        IndustrialInfrastructureSaveValidation.RequireValid(snapshot);
        FluidNetworkAggregateState restored = new FluidNetworkAggregateState
        {
            Version = 1
        };
        foreach (FluidNodeSaveData saved in snapshot?.nodes
                 ?? new List<FluidNodeSaveData>())
        {
            if (saved == null
                || !new BuildingInstanceId(
                    saved.buildingInstanceId).IsValid)
            {
                continue;
            }

            restored.Nodes[saved.buildingInstanceId.Trim()] =
                new FluidNodeState
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

        return new FluidNetworkRestoreCandidate(restored);
    }

    public void Restore(FluidNetworkRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        stateStore.Replace(candidate);
        if (!stateStore.IsRestoreStaging)
        {
            ResetProjectionAfterRestore();
            EnsureTopology();
        }
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

            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            if (state.TransferMode == WaterContainerTransferMode.Disabled)
            {
                state.TransferWork = 0f;
                state.TransferStatus = InfrastructureStatus.None;
                continue;
            }

            if (transfer.requiresPower && !power.IsPowered(node.Building))
            {
                state.TransferStatus = new InfrastructureStatus(
                    InfrastructureStatusCode.PowerUnavailable);
                continue;
            }

            state.TransferWork += deltaTime * ResolveFlowMultiplier(node);
            float required = Mathf.Max(0.1f, transfer.secondsPerBatch);
            if (state.TransferWork + 0.0001f < required)
            {
                state.TransferStatus = InfrastructureStatus.None;
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
        if (!items.CatalogProvider.TryGetDefinition(
                BottledCleanWaterItemId,
                out DungeonItemDefinition bottledWater)
            || bottledWater.StockCategory != StockCategory.Water)
        {
            throw new InvalidOperationException(
                $"Water-container transfer requires authored clean-water item '{BottledCleanWaterItemId}'.");
        }
        int currentStock = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    BottledCleanWaterItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        if (currentStock >= Mathf.Max(1, transfer.bottleTargetStock))
        {
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.OutputTargetReached,
                transfer.bottleTargetStock.ToString());
            return false;
        }

        if (!TryConsume(
                node.Building,
                WorldWaterQuality.Clean,
                transfer.waterPerBatch,
                out _,
                out DomainFailure failure))
        {
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.StorageCapacityUnavailable,
                failure.Code.ToString());
            return false;
        }

        if (!items.SpawnItemAt(
                BottledCleanWaterItemId,
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
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.OutputSpaceUnavailable);
            return false;
        }

        state.TransferStatus = InfrastructureStatus.None;
        return true;
    }

    private bool TryFeedWaterNetwork(
        IndustrialNodeDescriptor node,
        FluidNodeState state,
        BuildingWaterContainerTransferAbility transfer)
    {
        if (!projectionAdapter.TryResolveNetwork(
                node.Building,
                UtilityChannel.CleanWater,
                out string networkId,
                out _)
            || GetNetworkWaterFreeCapacity(networkId)
                + 0.0001f < transfer.waterPerBatch)
        {
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.StorageCapacityUnavailable);
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
            state.TransferStatus = new InfrastructureStatus(
                InfrastructureStatusCode.InputDeliveryPending,
                destinationId);
            return false;
        }

        float accepted = AddNetworkWater(
            networkId,
            WorldWaterQuality.Clean,
            transfer.waterPerBatch);
        state.TransferStatus = accepted + 0.0001f
            >= transfer.waterPerBatch
                ? InfrastructureStatus.None
                : new InfrastructureStatus(
                    InfrastructureStatusCode.StorageCapacityUnavailable);
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
                FluidNodeState state = stateStore.EnsureState(node.NodeId);
                return new WaterTransferFacilitySnapshot
                {
                    BuildingId = new BuildingInstanceId(node.NodeId),
                    Mode = state.TransferMode,
                    Powered = !ability.requiresPower
                        || power.IsPowered(node.Building),
                    Progress01 = state.TransferMode
                        == WaterContainerTransferMode.Disabled
                            ? 0f
                            : Mathf.Clamp01(
                                state.TransferWork
                                / Mathf.Max(0.1f, ability.secondsPerBatch)),
                    Status = state.TransferStatus
                };
            })
            .ToArray();
    }

    private static string CreateWaterTransferDestinationId(string nodeId) =>
        $"plumbing:water-transfer:{nodeId}";

    private void EnsureTopology()
    {
        if (projectionAdapter.EnsurePublishedRestoreRevision())
        {
            ResetTransientProjection();
        }

        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!projectionAdapter.TryUpdateTopologyVersion(
                topology.SourceVersion))
        {
            return;
        }

        foreach (IndustrialNodeDescriptor node in topology.Nodes.Values.Where(
                     node => (node.Channels
                             & (UtilityChannel.CleanWater
                                | UtilityChannel.Wastewater))
                         != 0))
        {
            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            BuildingWaterStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingWaterStorageAbility>();
            if (storage != null)
            {
                float waterCapacity = Mathf.Max(
                    0f,
                    storage.cleanWaterCapacity);
                FluidNodeWaterRules.ClampToCapacity(state, waterCapacity);
                state.Wastewater = Mathf.Min(
                    state.Wastewater,
                    Mathf.Max(0f, storage.wastewaterCapacity));
            }
        }

        stateStore.Touch();
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

            if (!projectionAdapter.TryResolveNetwork(
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

            if (!projectionAdapter.TryResolveNetwork(
                    node.Building,
                    UtilityChannel.Wastewater,
                    out string wasteNetworkId,
                    out _)
                || !projectionAdapter.TryResolveNetwork(
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

            FluidNodeState state = stateStore.EnsureState(node.NodeId);
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
            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            if (state.Leak > 0f)
            {
                float leaked = Mathf.Min(
                    state.CleanWater + state.UnsafeWater + state.FoulWater,
                    state.Leak * 0.001f * deltaTime);
                FluidNodeWaterRules.Remove(
                    state,
                    WorldWaterQuality.Foul,
                    leaked);
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

        if (projectionAdapter.TryResolveState(
                building,
                out FluidNodeState state))
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
        FluidNodeState state = stateStore.EnsureState(node.NodeId);
        return Mathf.Clamp01(
            1f - state.Blockage / 100f - state.FaultEquivalent());
    }

    private float AddNetworkWater(
        string networkId,
        WorldWaterQuality quality,
        float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (IndustrialNodeDescriptor node in projectionAdapter.GetNetworkNodes(
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

            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            float free = Mathf.Max(
                0f,
                storage.cleanWaterCapacity
                - state.CleanWater
                - state.UnsafeWater
                - state.FoulWater);
            float accepted = Mathf.Min(free, remaining);
            FluidNodeWaterRules.Add(state, quality, accepted);
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
        return projectionAdapter.GetNetworkNodes(
                UtilityChannel.CleanWater,
                networkId)
            .Sum(node => FluidNodeWaterRules.GetWater(
                stateStore.EnsureState(node.NodeId),
                quality));
    }

    private float GetNetworkWaterFreeCapacity(string networkId)
    {
        return projectionAdapter.GetNetworkNodes(
                UtilityChannel.CleanWater,
                networkId)
            .Sum(node =>
            {
                BuildingWaterStorageAbility storage =
                    node.Building.BuildingData
                        .GetAbility<BuildingWaterStorageAbility>();
                FluidNodeState state = stateStore.EnsureState(node.NodeId);
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
        foreach (IndustrialNodeDescriptor node in projectionAdapter.GetNetworkNodes(
                     UtilityChannel.CleanWater,
                     networkId))
        {
            FluidNodeState state = stateStore.EnsureState(node.NodeId);
            float removed = Mathf.Min(
                FluidNodeWaterRules.GetWater(state, quality),
                remaining);
            FluidNodeWaterRules.SetWater(
                state,
                quality,
                FluidNodeWaterRules.GetWater(state, quality) - removed);
            remaining -= removed;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }
    }

    private float GetNetworkWastewater(string networkId)
    {
        return projectionAdapter.GetNetworkNodes(
                UtilityChannel.Wastewater,
                networkId)
            .Sum(node => stateStore.EnsureState(node.NodeId).Wastewater);
    }

    private float GetWastewaterCapacity(string networkId)
    {
        return projectionAdapter.GetNetworkNodes(
                UtilityChannel.Wastewater,
                networkId)
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
        foreach (IndustrialNodeDescriptor node in projectionAdapter.GetNetworkNodes(
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

            FluidNodeState state = stateStore.EnsureState(node.NodeId);
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
        foreach (IndustrialNodeDescriptor node in projectionAdapter.GetNetworkNodes(
                     UtilityChannel.Wastewater,
                     networkId))
        {
            FluidNodeState state = stateStore.EnsureState(node.NodeId);
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

    private void ResetProjectionAfterRestore()
    {
        projectionAdapter.Reset(stateStore.PublishedRestoreRevision);
        ResetTransientProjection();
    }

    private void ResetTransientProjection()
    {
        accumulated = 0f;
        nextBackflowAt.Clear();
        waterTransfers = Array.Empty<WaterTransferFacilitySnapshot>();
    }
}
