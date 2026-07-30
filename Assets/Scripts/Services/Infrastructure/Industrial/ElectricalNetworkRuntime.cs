using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class ElectricalNodeState
{
    public PowerPriority Priority = PowerPriority.Production;
    public float StoredPower;
    public float FuelSeconds;
    public float Heat;
    public float Fault;
    public bool BreakerTripped;
    public bool Powered;
    public float SuppliedFraction;
}

internal sealed class ElectricalNetworkSummaryState
{
    public float ProductionPerSecond;
    public float DemandPerSecond;
    public float SuppliedPerSecond;
    public bool Tripped;
}

internal readonly struct ElectricalConsumerEntry
{
    public ElectricalConsumerEntry(
        IndustrialNodeDescriptor node,
        BuildingPowerConsumerAbility ability)
    {
        Node = node;
        Ability = ability;
    }

    public IndustrialNodeDescriptor Node { get; }
    public BuildingPowerConsumerAbility Ability { get; }
}

internal sealed class ElectricalNetworkRuntime :
    IElectricalNetworkRuntime,
    IPowerPriorityCommandService,
    ITickable
{
    private const float TickInterval = 0.25f;
    private const float FuelRequestInterval = 10f;

    private readonly IIndustrialInfrastructureTopologyRuntime topologyRuntime;
    private readonly IGameClock clock;
    private readonly IWorldItemStackRuntime items;
    private readonly AutomationPowerDemandRegistry automationPowerDemand;
    private readonly Dictionary<string, ElectricalNodeState> states =
        new Dictionary<string, ElectricalNodeState>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> nextFuelRequestAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, ElectricalNetworkSummaryState>
        networkSummaries =
            new Dictionary<string, ElectricalNetworkSummaryState>(
                StringComparer.Ordinal);
    private readonly List<ElectricalConsumerEntry> consumerScratch =
        new List<ElectricalConsumerEntry>(64);
    private readonly Dictionary<string, int> fuelRequirementScratch =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private IReadOnlyList<PowerNetworkSnapshot> networks =
        Array.Empty<PowerNetworkSnapshot>();
    private float accumulated;
    private int topologyVersion = int.MinValue;
    private int automationPowerVersion = int.MinValue;

    public ElectricalNetworkRuntime(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IGameClock clock,
        IWorldItemStackRuntime items,
        AutomationPowerDemandRegistry automationPowerDemand)
    {
        this.topologyRuntime = topologyRuntime
            ?? throw new ArgumentNullException(nameof(topologyRuntime));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.automationPowerDemand = automationPowerDemand
            ?? throw new ArgumentNullException(nameof(automationPowerDemand));
    }

    public int Version { get; private set; }
    public IReadOnlyList<PowerNetworkSnapshot> Networks
    {
        get
        {
            EnsureTopology();
            RefreshSnapshots();
            return networks;
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
        EvaluateNetworks(deltaTime);
    }

    public bool IsPowered(BuildableObject building)
    {
        if (!TryResolve(building, out string nodeId, out _))
        {
            return false;
        }

        return states.TryGetValue(nodeId, out ElectricalNodeState state)
            && state.Powered
            && !state.BreakerTripped;
    }

    public bool TryGetNode(
        BuildableObject building,
        out PowerNodeSnapshot snapshot)
    {
        snapshot = null;
        if (!TryResolve(building, out string nodeId, out IndustrialNodeDescriptor node)
            || !states.TryGetValue(nodeId, out ElectricalNodeState state))
        {
            return false;
        }

        snapshot = CreateNodeSnapshot(
            node,
            state,
            ResolvePowerNetworkId(nodeId));
        return true;
    }

    public InfrastructureCommandResult SetPriority(
        BuildableObject building,
        PowerPriority priority)
    {
        if (!Enum.IsDefined(typeof(PowerPriority), priority)
            || !TryResolve(building, out string nodeId, out IndustrialNodeDescriptor node)
            || node.Building.BuildingData
                .GetAbility<BuildingPowerConsumerAbility>() == null)
        {
            return InfrastructureCommandResult.Failure(
                "전력 소비 시설을 선택해야 합니다.");
        }

        EnsureState(node).Priority = priority;
        Touch();
        EvaluateNetworks(0f);
        return InfrastructureCommandResult.Success(
            $"전력 우선순위를 {priority}로 변경했습니다.");
    }

    public InfrastructureCommandResult ResetBreaker(
        BuildableObject building)
    {
        if (!TryResolve(building, out string nodeId, out IndustrialNodeDescriptor node)
            || node.Building.BuildingData
                .GetAbility<BuildingCircuitBreakerAbility>() == null)
        {
            return InfrastructureCommandResult.Failure(
                "차단기 시설을 선택해야 합니다.");
        }

        ElectricalNodeState state = EnsureState(node);
        if (state.Heat >= 60f)
        {
            return InfrastructureCommandResult.Failure(
                "회로가 아직 뜨거워 차단기를 복구할 수 없습니다.");
        }

        state.BreakerTripped = false;
        state.Fault = Mathf.Max(0f, state.Fault - 10f);
        Touch();
        EvaluateNetworks(0f);
        return InfrastructureCommandResult.Success("차단기를 복구했습니다.");
    }

    public DungeonPowerInfrastructureSaveData Capture()
    {
        EnsureTopology();
        return new DungeonPowerInfrastructureSaveData
        {
            nodes = states
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new PowerNodeSaveData
                {
                    nodeId = pair.Key,
                    priority = (int)pair.Value.Priority,
                    storedPower = pair.Value.StoredPower,
                    fuelSeconds = pair.Value.FuelSeconds,
                    heat = pair.Value.Heat,
                    fault = pair.Value.Fault,
                    breakerTripped = pair.Value.BreakerTripped
                })
                .ToList()
        };
    }

    public void Restore(DungeonPowerInfrastructureSaveData snapshot)
    {
        states.Clear();
        foreach (PowerNodeSaveData saved in snapshot?.nodes
                 ?? new List<PowerNodeSaveData>())
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.nodeId))
            {
                continue;
            }

            PowerPriority priority =
                Enum.IsDefined(typeof(PowerPriority), saved.priority)
                    ? (PowerPriority)saved.priority
                    : PowerPriority.Production;
            states[saved.nodeId.Trim()] = new ElectricalNodeState
            {
                Priority = priority,
                StoredPower = Mathf.Max(0f, saved.storedPower),
                FuelSeconds = Mathf.Max(0f, saved.fuelSeconds),
                Heat = Mathf.Max(0f, saved.heat),
                Fault = Mathf.Clamp(saved.fault, 0f, 100f),
                BreakerTripped = saved.breakerTripped
            };
        }

        topologyVersion = int.MinValue;
        EnsureTopology();
        EvaluateNetworks(0f);
        Touch();
    }

    private void EnsureTopology()
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        bool topologyChanged = topology.SourceVersion != topologyVersion;
        bool automationChanged =
            automationPowerDemand.Version != automationPowerVersion;
        if (!topologyChanged && !automationChanged)
        {
            return;
        }

        topologyVersion = topology.SourceVersion;
        automationPowerVersion = automationPowerDemand.Version;
        if (!topologyChanged)
        {
            EvaluateNetworks(0f);
            return;
        }

        networkSummaries.Clear();
        foreach (IndustrialNodeDescriptor node in topology.Nodes.Values
                     .Where(node => (node.Channels & UtilityChannel.Power) != 0))
        {
            ElectricalNodeState state = EnsureState(node);
            BuildingPowerStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerStorageAbility>();
            if (storage != null)
            {
                state.StoredPower = Mathf.Clamp(
                    state.StoredPower,
                    0f,
                    storage.capacity);
            }
        }

        EvaluateNetworks(0f);
        Touch();
    }

    private void EvaluateNetworks(float deltaTime)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!topology.NodeDescriptorsByNetwork.TryGetValue(
                UtilityChannel.Power,
                out Dictionary<
                    string,
                    IReadOnlyList<IndustrialNodeDescriptor>> grouped))
        {
            networkSummaries.Clear();
            networks = Array.Empty<PowerNetworkSnapshot>();
            return;
        }

        foreach (KeyValuePair<
                     string,
                     IReadOnlyList<IndustrialNodeDescriptor>> network
                 in grouped)
        {
            EvaluateNetwork(
                network.Key,
                network.Value,
                deltaTime);
        }

        Touch();
    }

    private void EvaluateNetwork(
        string networkId,
        IReadOnlyList<IndustrialNodeDescriptor> nodes,
        float deltaTime)
    {
        bool tripped = false;
        for (int index = 0; index < nodes.Count; index++)
        {
            IndustrialNodeDescriptor node = nodes[index];
            if (node.Building.BuildingData
                    .GetAbility<BuildingCircuitBreakerAbility>() != null
                && EnsureState(node).BreakerTripped)
            {
                tripped = true;
                break;
            }
        }

        float production = 0f;
        for (int index = 0; index < nodes.Count; index++)
        {
            IndustrialNodeDescriptor node = nodes[index];
            BuildingPowerProducerAbility producer =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerProducerAbility>();
            if (producer == null
                || tripped
                || !CanProduce(node, producer, deltaTime))
            {
                continue;
            }

            ElectricalNodeState state = EnsureState(node);
            production += Mathf.Max(0f, producer.productionPerSecond)
                * Mathf.Clamp01(1f - state.Fault / 125f);
        }

        consumerScratch.Clear();
        for (int index = 0; index < nodes.Count; index++)
        {
            IndustrialNodeDescriptor node = nodes[index];
            BuildingPowerConsumerAbility ability =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerConsumerAbility>();
            if (ability != null)
            {
                consumerScratch.Add(
                    new ElectricalConsumerEntry(node, ability));
            }
        }

        consumerScratch.Sort(CompareConsumers);
        float demand = 0f;
        for (int index = 0; index < consumerScratch.Count; index++)
        {
            ElectricalConsumerEntry consumer = consumerScratch[index];
            demand += ResolveDemand(consumer.Node, consumer.Ability);
        }

        float dischargeRate = tripped
            ? 0f
            : ResolveDischargeRate(nodes, deltaTime, demand - production);
        float available = tripped ? 0f : production + dischargeRate;
        float supplied = 0f;
        for (int index = 0; index < consumerScratch.Count; index++)
        {
            ElectricalConsumerEntry consumer = consumerScratch[index];
            IndustrialNodeDescriptor node = consumer.Node;
            BuildingPowerConsumerAbility ability = consumer.Ability;
            ElectricalNodeState state = EnsureState(node);
            float requested = ResolveDemand(node, ability);
            float granted = Mathf.Min(requested, Mathf.Max(0f, available));
            float fraction = requested <= 0.001f ? 1f : granted / requested;
            state.SuppliedFraction = fraction;
            state.Powered = fraction + 0.001f
                >= Mathf.Clamp01(ability.minimumSupplyFraction);
            if (state.Powered)
            {
                available -= granted;
                supplied += granted;
            }
        }

        for (int index = 0; index < nodes.Count; index++)
        {
            IndustrialNodeDescriptor node = nodes[index];
            if (node.Building.BuildingData
                    .GetAbility<BuildingPowerConsumerAbility>() != null)
            {
                continue;
            }

            ElectricalNodeState state = EnsureState(node);
            state.Powered = !tripped && production + dischargeRate > 0.001f;
            state.SuppliedFraction = state.Powered ? 1f : 0f;
        }

        float excess = Mathf.Max(0f, production - supplied);
        ChargeStorage(nodes, excess, deltaTime);
        UpdateOverload(nodes, production, demand, deltaTime);

        if (!networkSummaries.TryGetValue(
                networkId,
                out ElectricalNetworkSummaryState summary))
        {
            summary = new ElectricalNetworkSummaryState();
            networkSummaries[networkId] = summary;
        }

        summary.ProductionPerSecond = production;
        summary.DemandPerSecond = demand;
        summary.SuppliedPerSecond = supplied;
        summary.Tripped = tripped;
    }

    private int CompareConsumers(
        ElectricalConsumerEntry left,
        ElectricalConsumerEntry right)
    {
        int priorityComparison = ((int)EnsureState(left.Node).Priority)
            .CompareTo((int)EnsureState(right.Node).Priority);
        return priorityComparison != 0
            ? priorityComparison
            : string.CompareOrdinal(left.Node.NodeId, right.Node.NodeId);
    }

    private void RefreshSnapshots()
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!topology.NodeDescriptorsByNetwork.TryGetValue(
                UtilityChannel.Power,
                out Dictionary<
                    string,
                    IReadOnlyList<IndustrialNodeDescriptor>> grouped))
        {
            networks = Array.Empty<PowerNetworkSnapshot>();
            return;
        }

        List<PowerNetworkSnapshot> snapshots =
            new List<PowerNetworkSnapshot>(grouped.Count);
        foreach (KeyValuePair<
                     string,
                     IReadOnlyList<IndustrialNodeDescriptor>> network
                 in grouped.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            List<PowerNodeSnapshot> nodeSnapshots =
                new List<PowerNodeSnapshot>(network.Value.Count);
            float storedPower = 0f;
            float storageCapacity = 0f;
            for (int index = 0; index < network.Value.Count; index++)
            {
                IndustrialNodeDescriptor node = network.Value[index];
                PowerNodeSnapshot nodeSnapshot = CreateNodeSnapshot(
                    node,
                    EnsureState(node),
                    network.Key);
                nodeSnapshots.Add(nodeSnapshot);
                storedPower += nodeSnapshot.StoredPower;
                storageCapacity += nodeSnapshot.StorageCapacity;
            }

            networkSummaries.TryGetValue(
                network.Key,
                out ElectricalNetworkSummaryState summary);
            snapshots.Add(new PowerNetworkSnapshot
            {
                NetworkId = network.Key,
                ProductionPerSecond =
                    summary?.ProductionPerSecond ?? 0f,
                DemandPerSecond = summary?.DemandPerSecond ?? 0f,
                SuppliedPerSecond = summary?.SuppliedPerSecond ?? 0f,
                StoredPower = storedPower,
                StorageCapacity = storageCapacity,
                Tripped = summary?.Tripped ?? false,
                Nodes = nodeSnapshots
            });
        }

        networks = snapshots;
    }

    private bool CanProduce(
        IndustrialNodeDescriptor node,
        BuildingPowerProducerAbility producer,
        float deltaTime)
    {
        if (!producer.requiresFuel)
        {
            return true;
        }

        ElectricalNodeState state = EnsureState(node);
        state.FuelSeconds = Mathf.Max(0f, state.FuelSeconds - deltaTime);
        if (state.FuelSeconds > 0f)
        {
            return true;
        }

        string fuelItemId = producer.fuelItemId?.Trim() ?? string.Empty;
        string destinationId = "power:" + node.NodeId;
        fuelRequirementScratch.Clear();
        if (!string.IsNullOrWhiteSpace(fuelItemId))
        {
            fuelRequirementScratch[fuelItemId] = 1;
        }

        if (!string.IsNullOrWhiteSpace(fuelItemId)
            && items.TryConsumeFacilityItemBuffer(
                destinationId,
                fuelRequirementScratch,
                out _))
        {
            state.FuelSeconds = Mathf.Max(1f, producer.secondsPerFuel);
            return true;
        }

        if (clock.Time >= nextFuelRequestAt.GetValueOrDefault(node.NodeId, 0f)
            && !string.IsNullOrWhiteSpace(fuelItemId))
        {
            items.TryRequestItemDelivery(
                fuelItemId,
                1,
                node.Building.centerPos,
                destinationId,
                out _,
                out _);
            nextFuelRequestAt[node.NodeId] =
                clock.Time + FuelRequestInterval;
        }

        return false;
    }

    private float ResolveDischargeRate(
        IReadOnlyList<IndustrialNodeDescriptor> nodes,
        float deltaTime,
        float requestedRate)
    {
        if (requestedRate <= 0f || deltaTime <= 0f)
        {
            return 0f;
        }

        float remainingEnergy = requestedRate * deltaTime;
        float suppliedEnergy = 0f;
        foreach (IndustrialNodeDescriptor node in nodes)
        {
            BuildingPowerStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerStorageAbility>();
            if (storage == null)
            {
                continue;
            }

            ElectricalNodeState state = EnsureState(node);
            float available = Mathf.Min(
                state.StoredPower,
                storage.transferPerSecond * deltaTime);
            float removed = Mathf.Min(available, remainingEnergy);
            state.StoredPower -= removed;
            remainingEnergy -= removed;
            suppliedEnergy += removed * Mathf.Clamp01(storage.efficiency);
            if (remainingEnergy <= 0.001f)
            {
                break;
            }
        }

        return suppliedEnergy / deltaTime;
    }

    private void ChargeStorage(
        IReadOnlyList<IndustrialNodeDescriptor> nodes,
        float excessRate,
        float deltaTime)
    {
        if (excessRate <= 0f || deltaTime <= 0f)
        {
            return;
        }

        float energy = excessRate * deltaTime;
        foreach (IndustrialNodeDescriptor node in nodes)
        {
            BuildingPowerStorageAbility storage =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerStorageAbility>();
            if (storage == null)
            {
                continue;
            }

            ElectricalNodeState state = EnsureState(node);
            float room = Mathf.Max(0f, storage.capacity - state.StoredPower);
            float input = Mathf.Min(
                energy,
                storage.transferPerSecond * deltaTime);
            float stored = Mathf.Min(
                room,
                input * Mathf.Clamp01(storage.efficiency));
            state.StoredPower += stored;
            energy -= input;
            if (energy <= 0.001f)
            {
                break;
            }
        }
    }

    private void UpdateOverload(
        IReadOnlyList<IndustrialNodeDescriptor> nodes,
        float production,
        float demand,
        float deltaTime)
    {
        float capacity = Mathf.Max(0.01f, production);
        float ratio = demand / capacity;
        foreach (IndustrialNodeDescriptor node in nodes)
        {
            ElectricalNodeState state = EnsureState(node);
            state.Heat = ratio > 1f
                ? state.Heat + (ratio - 1f) * 18f * deltaTime
                : Mathf.Max(0f, state.Heat - 8f * deltaTime);
            if (state.Heat > 75f)
            {
                state.Fault = Mathf.Clamp(
                    state.Fault + (state.Heat - 75f) * 0.02f * deltaTime,
                    0f,
                    100f);
            }

            BuildingCircuitBreakerAbility breaker =
                node.Building.BuildingData
                    .GetAbility<BuildingCircuitBreakerAbility>();
            if (breaker != null
                && ratio > Mathf.Max(1f, breaker.overloadTolerance)
                && state.Heat >= Mathf.Max(1f, breaker.tripHeat))
            {
                state.BreakerTripped = true;
                state.Powered = false;
            }
        }
    }

    private ElectricalNodeState EnsureState(IndustrialNodeDescriptor node)
    {
        if (!states.TryGetValue(node.NodeId, out ElectricalNodeState state))
        {
            BuildingPowerConsumerAbility consumer =
                node.Building.BuildingData
                    .GetAbility<BuildingPowerConsumerAbility>();
            state = new ElectricalNodeState
            {
                Priority = consumer?.priority ?? PowerPriority.Production
            };
            states[node.NodeId] = state;
        }

        return state;
    }

    private bool TryResolve(
        BuildableObject building,
        out string nodeId,
        out IndustrialNodeDescriptor node)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (building != null
            && topology.NodeIdsByBuilding.TryGetValue(building, out nodeId)
            && topology.Nodes.TryGetValue(nodeId, out node)
            && (node.Channels & UtilityChannel.Power) != 0)
        {
            return true;
        }

        nodeId = string.Empty;
        node = null;
        return false;
    }

    private string ResolvePowerNetworkId(string nodeId)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        return topology.NetworkByNode.TryGetValue(
                UtilityChannel.Power,
                out Dictionary<string, string> networkByNode)
            && networkByNode.TryGetValue(nodeId, out string networkId)
                ? networkId
                : string.Empty;
    }

    private PowerNodeSnapshot CreateNodeSnapshot(
        IndustrialNodeDescriptor node,
        ElectricalNodeState state,
        string networkId)
    {
        BuildingSO data = node.Building.BuildingData;
        BuildingPowerProducerAbility producer =
            data.GetAbility<BuildingPowerProducerAbility>();
        BuildingPowerConsumerAbility consumer =
            data.GetAbility<BuildingPowerConsumerAbility>();
        BuildingPowerStorageAbility storage =
            data.GetAbility<BuildingPowerStorageAbility>();
        return new PowerNodeSnapshot
        {
            NodeId = node.NodeId,
            NetworkId = networkId,
            Priority = state.Priority,
            Powered = state.Powered,
            BreakerTripped = state.BreakerTripped,
            ProductionPerSecond = producer?.productionPerSecond ?? 0f,
            DemandPerSecond = consumer == null
                ? 0f
                : ResolveDemand(node, consumer),
            SuppliedFraction = state.SuppliedFraction,
            StoredPower = state.StoredPower,
            StorageCapacity = storage?.capacity ?? 0f,
            Heat = state.Heat,
            Fault = state.Fault
        };
    }

    private float ResolveDemand(
        IndustrialNodeDescriptor node,
        BuildingPowerConsumerAbility consumer)
    {
        BuildingAutomationAbility automation =
            node.Building.BuildingData
                .GetAbility<BuildingAutomationAbility>();
        if (automation == null)
        {
            return Mathf.Max(0f, consumer.demandPerSecond);
        }

        return automationPowerDemand.ResolveDemand(
            node.NodeId,
            automation);
    }

    private void Touch()
    {
        unchecked
        {
            Version++;
        }
    }
}
