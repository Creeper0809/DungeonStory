using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class IndustrialNodeDescriptor
{
    public string NodeId;
    public BuildableObject Building;
    public UtilityChannel Channels;
    public IReadOnlyList<Vector2Int> Cells = Array.Empty<Vector2Int>();
    public BuildingConveyorSegmentAbility Conveyor;
    public BuildingConveyorPortAbility ConveyorPort;
    public BuildingConveyorOverflowAbility Overflow;
}

internal sealed class IndustrialTopologySnapshot
{
    public int SourceVersion;
    public readonly Dictionary<string, IndustrialNodeDescriptor> Nodes =
        new Dictionary<string, IndustrialNodeDescriptor>(StringComparer.Ordinal);
    public readonly Dictionary<BuildableObject, string> NodeIdsByBuilding =
        new Dictionary<BuildableObject, string>();
    public readonly Dictionary<UtilityChannel, Dictionary<string, string>>
        NetworkByNode =
            new Dictionary<UtilityChannel, Dictionary<string, string>>();
    public readonly Dictionary<UtilityChannel, Dictionary<string, List<string>>>
        NodesByNetwork =
            new Dictionary<UtilityChannel, Dictionary<string, List<string>>>();
    public readonly Dictionary<
        UtilityChannel,
        Dictionary<string, IReadOnlyList<IndustrialNodeDescriptor>>>
        NodeDescriptorsByNetwork =
            new Dictionary<
                UtilityChannel,
                Dictionary<string, IReadOnlyList<IndustrialNodeDescriptor>>>();
    public readonly Dictionary<string, List<string>> ConveyorOutgoing =
        new Dictionary<string, List<string>>(StringComparer.Ordinal);
    public readonly Dictionary<string, List<string>> ConveyorIncoming =
        new Dictionary<string, List<string>>(StringComparer.Ordinal);
    public readonly Dictionary<string, string> ConveyorNetworkByNode =
        new Dictionary<string, string>(StringComparer.Ordinal);
    public readonly Dictionary<string, List<string>> ConveyorNodesByNetwork =
        new Dictionary<string, List<string>>(StringComparer.Ordinal);
    public readonly HashSet<string> CyclicConveyorNetworks =
        new HashSet<string>(StringComparer.Ordinal);
    public readonly List<IndustrialNodeDescriptor> ConveyorInputNodes =
        new List<IndustrialNodeDescriptor>();
}

internal static class IndustrialInfrastructureIdentity
{
    public static string GetNodeId(BuildableObject building)
    {
        if (building == null)
        {
            return string.Empty;
        }
        return building.RequirePersistentInstanceId().Value;
    }
}

internal static class IndustrialInfrastructureTopologyBuilder
{
    private static readonly UtilityChannel[] Channels =
    {
        UtilityChannel.Power,
        UtilityChannel.CleanWater,
        UtilityChannel.Wastewater
    };

    private static readonly Vector2Int[] Cardinal =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static IndustrialTopologySnapshot Build(
        int sourceVersion,
        IReadOnlyList<BuildableObject> buildings)
    {
        List<IndustrialNodeDescriptor> nodes =
            new List<IndustrialNodeDescriptor>();
        foreach (BuildableObject building in buildings
                     ?? Array.Empty<BuildableObject>())
        {
            IndustrialNodeDescriptor node = CreateNode(building);
            if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                continue;
            }

            nodes.Add(node);
        }

        return BuildFromDescriptors(sourceVersion, nodes);
    }

    internal static IndustrialTopologySnapshot BuildFromDescriptors(
        int sourceVersion,
        IReadOnlyList<IndustrialNodeDescriptor> nodes)
    {
        IndustrialTopologySnapshot result =
            new IndustrialTopologySnapshot { SourceVersion = sourceVersion };
        foreach (IndustrialNodeDescriptor node in nodes
                     ?? Array.Empty<IndustrialNodeDescriptor>())
        {
            if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                continue;
            }

            result.Nodes[node.NodeId] = node;
            if (node.Building != null)
            {
                result.NodeIdsByBuilding[node.Building] = node.NodeId;
            }
        }

        foreach (UtilityChannel channel in Channels)
        {
            BuildUtilityComponents(result, channel);
        }

        BuildConveyorGraph(result);
        return result;
    }

    private static IndustrialNodeDescriptor CreateNode(
        BuildableObject building)
    {
        if (building == null
            || building.IsGridDestroyed
            || building.BuildingData == null)
        {
            return null;
        }

        BuildingSO data = building.BuildingData;
        BuildingUtilityConnectionAbility connection =
            data.GetAbility<BuildingUtilityConnectionAbility>();
        UtilityChannel channels = connection?.channels ?? UtilityChannel.None;
        if (data.GetAbility<BuildingPowerProducerAbility>() != null
            || data.GetAbility<BuildingPowerConsumerAbility>() != null
            || data.GetAbility<BuildingPowerStorageAbility>() != null
            || data.GetAbility<BuildingCircuitBreakerAbility>() != null
            || data.GetAbility<BuildingAutomationAbility>() != null)
        {
            channels |= UtilityChannel.Power;
        }

        if (data.GetAbility<BuildingWaterProducerAbility>() != null
            || data.GetAbility<BuildingWaterFixtureAbility>() != null)
        {
            channels |= UtilityChannel.CleanWater;
        }

        BuildingWaterStorageAbility storage =
            data.GetAbility<BuildingWaterStorageAbility>();
        if (storage != null)
        {
            channels |= storage.channels;
        }

        if (data.GetAbility<BuildingWaterFixtureAbility>() != null
            || data.GetAbility<BuildingWastewaterProcessorAbility>() != null)
        {
            channels |= UtilityChannel.Wastewater;
        }

        BuildingConveyorSegmentAbility conveyor =
            data.GetAbility<BuildingConveyorSegmentAbility>();
        BuildingConveyorPortAbility port =
            data.GetAbility<BuildingConveyorPortAbility>();
        BuildingConveyorOverflowAbility overflow =
            data.GetAbility<BuildingConveyorOverflowAbility>();
        if ((conveyor?.requiresPower ?? false)
            || data.GetAbility<BuildingAutomationAbility>() != null)
        {
            channels |= UtilityChannel.Power;
        }
        if (channels == UtilityChannel.None
            && conveyor == null
            && port == null
            && overflow == null)
        {
            return null;
        }

        Vector2Int[] cells = building.buildPoses?
            .Distinct()
            .OrderBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .ToArray()
            ?? Array.Empty<Vector2Int>();
        if (cells.Length == 0)
        {
            cells = new[] { building.centerPos };
        }

        return new IndustrialNodeDescriptor
        {
            NodeId = IndustrialInfrastructureIdentity.GetNodeId(building),
            Building = building,
            Channels = channels,
            Cells = cells,
            Conveyor = conveyor,
            ConveyorPort = port,
            Overflow = overflow
        };
    }

    private static void BuildUtilityComponents(
        IndustrialTopologySnapshot topology,
        UtilityChannel channel)
    {
        IndustrialNodeDescriptor[] nodes = topology.Nodes.Values
            .Where(node => (node.Channels & channel) != 0)
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<Vector2Int, List<string>> byCell = IndexCells(nodes);
        Dictionary<string, HashSet<string>> neighbors =
            CreateUndirectedNeighbors(nodes, byCell);
        Dictionary<string, string> networkByNode =
            new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, List<string>> nodesByNetwork =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (IndustrialNodeDescriptor node in nodes)
        {
            if (!visited.Add(node.NodeId))
            {
                continue;
            }

            List<string> component = new List<string>();
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(node.NodeId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                component.Add(current);
                foreach (string neighbor in neighbors[current]
                             .OrderBy(value => value, StringComparer.Ordinal))
                {
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            component.Sort(StringComparer.Ordinal);
            string networkId = CreateNetworkId(channel.ToString(), component);
            nodesByNetwork[networkId] = component;
            foreach (string nodeId in component)
            {
                networkByNode[nodeId] = networkId;
            }
        }

        topology.NetworkByNode[channel] = networkByNode;
        topology.NodesByNetwork[channel] = nodesByNetwork;
        topology.NodeDescriptorsByNetwork[channel] =
            nodesByNetwork.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<IndustrialNodeDescriptor>)pair.Value
                    .Select(nodeId => topology.Nodes[nodeId])
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private static void BuildConveyorGraph(
        IndustrialTopologySnapshot topology)
    {
        IndustrialNodeDescriptor[] nodes = topology.Nodes.Values
            .Where(IsConveyorNode)
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<Vector2Int, List<string>> byCell = IndexCells(nodes);
        foreach (IndustrialNodeDescriptor node in nodes)
        {
            if (node.ConveyorPort != null
                && node.ConveyorPort.mode != ConveyorPortMode.Output)
            {
                topology.ConveyorInputNodes.Add(node);
            }

            List<string> outgoing = new List<string>();
            IEnumerable<Vector2Int> directions = ResolveOutputDirections(node);
            foreach (Vector2Int cell in node.Cells)
            {
                foreach (Vector2Int direction in directions)
                {
                    if (!byCell.TryGetValue(
                            cell + direction,
                            out List<string> candidates))
                    {
                        continue;
                    }

                    foreach (string candidateId in candidates)
                    {
                        if (string.Equals(
                                candidateId,
                                node.NodeId,
                                StringComparison.Ordinal)
                            || !CanReceive(topology.Nodes[candidateId]))
                        {
                            continue;
                        }

                        if (!outgoing.Contains(candidateId))
                        {
                            outgoing.Add(candidateId);
                        }
                    }
                }
            }

            outgoing.Sort(StringComparer.Ordinal);
            topology.ConveyorOutgoing[node.NodeId] = outgoing;
            if (!topology.ConveyorIncoming.ContainsKey(node.NodeId))
            {
                topology.ConveyorIncoming[node.NodeId] = new List<string>();
            }
        }

        foreach (KeyValuePair<string, List<string>> edge
                 in topology.ConveyorOutgoing)
        {
            foreach (string target in edge.Value)
            {
                if (!topology.ConveyorIncoming.TryGetValue(
                        target,
                        out List<string> incoming))
                {
                    incoming = new List<string>();
                    topology.ConveyorIncoming[target] = incoming;
                }

                if (!incoming.Contains(edge.Key))
                {
                    incoming.Add(edge.Key);
                }
            }
        }

        BuildConveyorComponentsAndCycles(topology, nodes);
    }

    private static void BuildConveyorComponentsAndCycles(
        IndustrialTopologySnapshot topology,
        IReadOnlyList<IndustrialNodeDescriptor> nodes)
    {
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (IndustrialNodeDescriptor node in nodes)
        {
            if (!visited.Add(node.NodeId))
            {
                continue;
            }

            List<string> component = new List<string>();
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(node.NodeId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                component.Add(current);
                IEnumerable<string> adjacent =
                    topology.ConveyorOutgoing.GetValueOrDefault(
                        current,
                        new List<string>())
                    .Concat(topology.ConveyorIncoming.GetValueOrDefault(
                        current,
                        new List<string>()));
                foreach (string neighbor in adjacent)
                {
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            component.Sort(StringComparer.Ordinal);
            string networkId = CreateNetworkId("Conveyor", component);
            topology.ConveyorNodesByNetwork[networkId] = component;
            foreach (string nodeId in component)
            {
                topology.ConveyorNetworkByNode[nodeId] = networkId;
            }

            if (ContainsDirectedCycle(topology, component))
            {
                topology.CyclicConveyorNetworks.Add(networkId);
            }
        }
    }

    private static bool ContainsDirectedCycle(
        IndustrialTopologySnapshot topology,
        IReadOnlyList<string> component)
    {
        HashSet<string> allowed = new HashSet<string>(
            component,
            StringComparer.Ordinal);
        Dictionary<string, int> indexByNode =
            new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<string, int> lowLink =
            new Dictionary<string, int>(StringComparer.Ordinal);
        Stack<string> stack = new Stack<string>();
        HashSet<string> onStack = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        bool cycleFound = false;

        void Visit(string nodeId)
        {
            indexByNode[nodeId] = index;
            lowLink[nodeId] = index;
            index++;
            stack.Push(nodeId);
            onStack.Add(nodeId);
            foreach (string target in topology.ConveyorOutgoing.GetValueOrDefault(
                         nodeId,
                         new List<string>()))
            {
                if (!allowed.Contains(target))
                {
                    continue;
                }

                if (!indexByNode.ContainsKey(target))
                {
                    Visit(target);
                    lowLink[nodeId] = Mathf.Min(
                        lowLink[nodeId],
                        lowLink[target]);
                }
                else if (onStack.Contains(target))
                {
                    lowLink[nodeId] = Mathf.Min(
                        lowLink[nodeId],
                        indexByNode[target]);
                }
            }

            if (lowLink[nodeId] != indexByNode[nodeId])
            {
                return;
            }

            int count = 0;
            string member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                count++;
            }
            while (!string.Equals(member, nodeId, StringComparison.Ordinal));

            cycleFound |= count > 1
                || topology.ConveyorOutgoing.GetValueOrDefault(
                        nodeId,
                        new List<string>())
                    .Contains(nodeId);
        }

        foreach (string nodeId in component)
        {
            if (!indexByNode.ContainsKey(nodeId))
            {
                Visit(nodeId);
            }
        }

        return cycleFound;
    }

    private static Dictionary<Vector2Int, List<string>> IndexCells(
        IEnumerable<IndustrialNodeDescriptor> nodes)
    {
        Dictionary<Vector2Int, List<string>> result =
            new Dictionary<Vector2Int, List<string>>();
        foreach (IndustrialNodeDescriptor node in nodes)
        {
            foreach (Vector2Int cell in node.Cells)
            {
                if (!result.TryGetValue(cell, out List<string> ids))
                {
                    ids = new List<string>();
                    result[cell] = ids;
                }

                ids.Add(node.NodeId);
            }
        }

        return result;
    }

    private static Dictionary<string, HashSet<string>>
        CreateUndirectedNeighbors(
            IEnumerable<IndustrialNodeDescriptor> nodes,
            IReadOnlyDictionary<Vector2Int, List<string>> byCell)
    {
        Dictionary<string, HashSet<string>> result =
            nodes.ToDictionary(
                node => node.NodeId,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);
        foreach (IndustrialNodeDescriptor node in nodes)
        {
            foreach (Vector2Int cell in node.Cells)
            {
                foreach (Vector2Int offset in Cardinal.Prepend(Vector2Int.zero))
                {
                    if (!byCell.TryGetValue(
                            cell + offset,
                            out List<string> ids))
                    {
                        continue;
                    }

                    foreach (string other in ids)
                    {
                        if (!string.Equals(
                                node.NodeId,
                                other,
                                StringComparison.Ordinal))
                        {
                            result[node.NodeId].Add(other);
                            result[other].Add(node.NodeId);
                        }
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<Vector2Int> ResolveOutputDirections(
        IndustrialNodeDescriptor node)
    {
        if (node.Conveyor != null
            && node.Conveyor.outputDirections != null
            && node.Conveyor.outputDirections.Length > 0)
        {
            return node.Conveyor.outputDirections
                .Where(direction => Mathf.Abs(direction.x)
                    + Mathf.Abs(direction.y) == 1)
                .Distinct()
                .ToArray();
        }

        return node.ConveyorPort != null
            && node.ConveyorPort.mode != ConveyorPortMode.Output
                ? Cardinal
                : Array.Empty<Vector2Int>();
    }

    private static bool CanReceive(IndustrialNodeDescriptor node)
    {
        return node.Conveyor != null
            || node.ConveyorPort != null
            || node.Overflow != null;
    }

    private static bool IsConveyorNode(IndustrialNodeDescriptor node)
    {
        return node.Conveyor != null
            || node.ConveyorPort != null
            || node.Overflow != null;
    }

    private static string CreateNetworkId(
        string prefix,
        IReadOnlyList<string> sortedNodeIds)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (string nodeId in sortedNodeIds)
            {
                foreach (char character in nodeId)
                {
                    hash ^= character;
                    hash *= 16777619;
                }
            }

            return $"{prefix.ToLowerInvariant()}:{hash:x8}";
        }
    }
}

internal interface IIndustrialInfrastructureTopologyRuntime
{
    IndustrialTopologySnapshot Current { get; }
    void MarkDirty();
}

internal sealed class IndustrialInfrastructureTopologyRuntime :
    IIndustrialInfrastructureTopologyRuntime
{
    private readonly IBuildingWorldQuery buildings;
    private IndustrialTopologySnapshot current;
    private bool dirty = true;

    public IndustrialInfrastructureTopologyRuntime(
        IBuildingWorldQuery buildings)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
    }

    public IndustrialTopologySnapshot Current
    {
        get
        {
            if (dirty
                || current == null
                || current.SourceVersion != buildings.BuildingVersion)
            {
                current = IndustrialInfrastructureTopologyBuilder.Build(
                    buildings.BuildingVersion,
                    buildings.Buildings);
                dirty = false;
            }

            return current;
        }
    }

    public void MarkDirty()
    {
        dirty = true;
    }
}
