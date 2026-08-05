using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[Flags]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum UtilityChannel
{
    None = 0,
    Power = 1 << 0,
    CleanWater = 1 << 1,
    Wastewater = 1 << 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WaterContainerTransferMode
{
    Disabled = 0,
    BottleFromNetwork = 1,
    FeedNetwork = 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

public readonly struct FluidTopologyNodeProjection
{
    public FluidTopologyNodeProjection(
        string nodeId,
        float cleanWaterCapacity,
        float wastewaterCapacity)
    {
        NodeId = nodeId ?? string.Empty;
        CleanWaterCapacity = Math.Max(0f, cleanWaterCapacity);
        WastewaterCapacity = Math.Max(0f, wastewaterCapacity);
    }

    public string NodeId { get; }
    public float CleanWaterCapacity { get; }
    public float WastewaterCapacity { get; }
}

public sealed class FluidTopologyProjection
{
    private readonly Dictionary<string, FluidTopologyNodeProjection> nodes =
        new Dictionary<string, FluidTopologyNodeProjection>(StringComparer.Ordinal);
    private readonly Dictionary<UtilityChannel, Dictionary<string, string[]>>
        nodeIdsByNetwork =
            new Dictionary<UtilityChannel, Dictionary<string, string[]>>();
    private readonly Dictionary<UtilityChannel, Dictionary<string, string>>
        networkByNode =
            new Dictionary<UtilityChannel, Dictionary<string, string>>();

    public void AddNode(FluidTopologyNodeProjection node)
    {
        if (string.IsNullOrWhiteSpace(node.NodeId))
        {
            throw new ArgumentException("Fluid topology node id is required.");
        }

        nodes.Add(node.NodeId, node);
    }

    public void AddNetwork(
        UtilityChannel channel,
        string networkId,
        IReadOnlyList<string> nodeIds)
    {
        if (channel != UtilityChannel.CleanWater
            && channel != UtilityChannel.Wastewater)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        if (string.IsNullOrWhiteSpace(networkId))
        {
            throw new ArgumentException("Fluid network id is required.");
        }

        string[] stableNodeIds = (nodeIds ?? Array.Empty<string>())
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();
        if (!nodeIdsByNetwork.TryGetValue(
                channel,
                out Dictionary<string, string[]> byNetwork))
        {
            byNetwork = new Dictionary<string, string[]>(StringComparer.Ordinal);
            nodeIdsByNetwork.Add(channel, byNetwork);
        }

        byNetwork.Add(networkId, stableNodeIds);
        if (!networkByNode.TryGetValue(
                channel,
                out Dictionary<string, string> byNode))
        {
            byNode = new Dictionary<string, string>(StringComparer.Ordinal);
            networkByNode.Add(channel, byNode);
        }

        foreach (string nodeId in stableNodeIds)
        {
            byNode[nodeId] = networkId;
        }
    }

    public IReadOnlyList<string> GetNetworkIds(UtilityChannel channel) =>
        nodeIdsByNetwork.TryGetValue(
            channel,
            out Dictionary<string, string[]> byNetwork)
            ? byNetwork.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

    public IReadOnlyList<string> GetNodeIds(
        UtilityChannel channel,
        string networkId) =>
        nodeIdsByNetwork.TryGetValue(
            channel,
            out Dictionary<string, string[]> byNetwork)
        && byNetwork.TryGetValue(networkId, out string[] nodeIds)
            ? nodeIds
            : Array.Empty<string>();

    public bool TryGetNode(
        string nodeId,
        out FluidTopologyNodeProjection node) =>
        nodes.TryGetValue(nodeId ?? string.Empty, out node);

    public string ResolveNetworkId(
        UtilityChannel channel,
        string nodeId) =>
        networkByNode.TryGetValue(
            channel,
            out Dictionary<string, string> byNode)
        && byNode.TryGetValue(nodeId ?? string.Empty, out string networkId)
            ? networkId
            : string.Empty;
}

public static class FluidNetworkProjector
{
    public static IReadOnlyList<FluidNetworkSnapshot> Build(
        FluidTopologyProjection topology,
        Func<string, FluidNodeState> getState)
    {
        if (topology == null)
        {
            throw new ArgumentNullException(nameof(topology));
        }

        if (getState == null)
        {
            throw new ArgumentNullException(nameof(getState));
        }

        List<FluidNetworkSnapshot> result = new List<FluidNetworkSnapshot>();
        foreach (UtilityChannel channel in new[]
                 {
                     UtilityChannel.CleanWater,
                     UtilityChannel.Wastewater
                 })
        {
            foreach (string networkId in topology.GetNetworkIds(channel))
            {
                result.Add(BuildNetwork(topology, getState, channel, networkId));
            }
        }

        return result;
    }

    private static FluidNetworkSnapshot BuildNetwork(
        FluidTopologyProjection topology,
        Func<string, FluidNodeState> getState,
        UtilityChannel channel,
        string networkId)
    {
        float cleanWater = 0f;
        float unsafeWater = 0f;
        float foulWater = 0f;
        float wastewater = 0f;
        float capacity = 0f;
        float blockage = 0f;
        float leak = 0f;
        int count = 0;
        foreach (string nodeId in topology.GetNodeIds(channel, networkId))
        {
            if (!topology.TryGetNode(nodeId, out FluidTopologyNodeProjection node))
            {
                continue;
            }

            FluidNodeState state = getState(nodeId);
            cleanWater += state.CleanWater;
            unsafeWater += state.UnsafeWater;
            foulWater += state.FoulWater;
            wastewater += state.Wastewater;
            capacity += channel == UtilityChannel.CleanWater
                ? node.CleanWaterCapacity
                : node.WastewaterCapacity;
            blockage += state.Blockage;
            leak += state.Leak;
            count++;
        }

        return new FluidNetworkSnapshot
        {
            NetworkId = networkId,
            Channel = channel,
            CleanWater = cleanWater,
            UnsafeWater = unsafeWater,
            FoulWater = foulWater,
            Wastewater = wastewater,
            Capacity = capacity,
            Blockage = count == 0 ? 0f : blockage / count,
            Leak = count == 0 ? 0f : leak / count,
            HasOverflowRisk = channel == UtilityChannel.Wastewater
                && capacity > 0f
                && wastewater >= capacity - 0.001f
        };
    }
}
