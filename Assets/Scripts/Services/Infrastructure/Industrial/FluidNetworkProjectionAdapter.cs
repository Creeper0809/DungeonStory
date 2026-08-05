using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class FluidNetworkProjectionAdapter
{
    private readonly IIndustrialInfrastructureTopologyRuntime topologyRuntime;
    private readonly FluidNetworkStateStore stateStore;
    private IReadOnlyList<FluidNetworkSnapshot> snapshots =
        Array.Empty<FluidNetworkSnapshot>();
    private int topologyVersion = int.MinValue;
    private int snapshotVersion = int.MinValue;
    private int publishedRestoreRevision;

    public FluidNetworkProjectionAdapter(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        FluidNetworkStateStore stateStore)
    {
        this.topologyRuntime = topologyRuntime
            ?? throw new ArgumentNullException(nameof(topologyRuntime));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        publishedRestoreRevision = stateStore.PublishedRestoreRevision;
    }

    public IReadOnlyList<FluidNetworkSnapshot> GetSnapshots(int stateVersion)
    {
        if (snapshotVersion == stateVersion)
        {
            return snapshots;
        }

        snapshots = FluidNetworkProjector.Build(
            Capture(topologyRuntime.Current),
            stateStore.EnsureState);
        snapshotVersion = stateVersion;
        return snapshots;
    }

    public IReadOnlyList<IndustrialNodeDescriptor> GetNetworkNodes(
        UtilityChannel channel,
        string networkId)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        return topology.NodeDescriptorsByNetwork.TryGetValue(
                channel,
                out Dictionary<string, IReadOnlyList<IndustrialNodeDescriptor>>
                    byNetwork)
            && byNetwork.TryGetValue(
                networkId,
                out IReadOnlyList<IndustrialNodeDescriptor> nodes)
                ? nodes
                : Array.Empty<IndustrialNodeDescriptor>();
    }

    public bool TryResolveNetwork(
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

    public bool TryResolveState(
        BuildableObject building,
        out FluidNodeState state)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (building != null
            && topology.NodeIdsByBuilding.TryGetValue(
                building,
                out string nodeId)
            && topology.Nodes.TryGetValue(
                nodeId,
                out IndustrialNodeDescriptor node)
            && (node.Channels
                    & (UtilityChannel.CleanWater
                       | UtilityChannel.Wastewater))
                != 0)
        {
            state = stateStore.EnsureState(nodeId);
            return true;
        }

        state = null;
        return false;
    }

    public string ResolveNetworkId(
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

    public bool EnsurePublishedRestoreRevision()
    {
        int revision = stateStore.PublishedRestoreRevision;
        if (publishedRestoreRevision == revision)
        {
            return false;
        }

        Reset(revision);
        return true;
    }

    public bool TryUpdateTopologyVersion(int sourceVersion)
    {
        if (topologyVersion == sourceVersion)
        {
            return false;
        }

        topologyVersion = sourceVersion;
        snapshotVersion = int.MinValue;
        return true;
    }

    public void Reset(int restoreRevision)
    {
        publishedRestoreRevision = restoreRevision;
        topologyVersion = int.MinValue;
        snapshotVersion = int.MinValue;
        snapshots = Array.Empty<FluidNetworkSnapshot>();
    }

    private static FluidTopologyProjection Capture(
        IndustrialTopologySnapshot topology)
    {
        FluidTopologyProjection projection = new FluidTopologyProjection();
        foreach (IndustrialNodeDescriptor node in topology.Nodes.Values
                     .OrderBy(node => node.NodeId, StringComparer.Ordinal))
        {
            BuildingWaterStorageAbility storage = node.Building?.BuildingData
                ?.GetAbility<BuildingWaterStorageAbility>();
            projection.AddNode(new FluidTopologyNodeProjection(
                node.NodeId,
                storage?.cleanWaterCapacity ?? 0f,
                storage?.wastewaterCapacity ?? 0f));
        }

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
                projection.AddNetwork(channel, pair.Key, pair.Value);
            }
        }

        return projection;
    }
}

internal sealed class FluidNetworkStateStore
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    public FluidNetworkStateStore(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    private FluidNetworkAggregateState State =>
        aggregateRootStore.GetOrCreateWritable(
            () => new FluidNetworkAggregateState(),
            state => state.DeepClone());

    public Dictionary<string, FluidNodeState> Nodes => State.Nodes;
    public int Version => State.Version;
    public int PublishedRestoreRevision =>
        aggregateRootStore.PublishedRestoreRevision;
    public bool IsRestoreStaging => aggregateRootStore.IsRestoreStaging;

    public FluidNodeState EnsureState(string nodeId)
    {
        if (!Nodes.TryGetValue(nodeId, out FluidNodeState state))
        {
            state = new FluidNodeState();
            Nodes[nodeId] = state;
        }

        return state;
    }

    public void Touch()
    {
        unchecked
        {
            State.Version++;
        }
    }

    public void Replace(FluidNetworkRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(candidate.State);
    }
}
