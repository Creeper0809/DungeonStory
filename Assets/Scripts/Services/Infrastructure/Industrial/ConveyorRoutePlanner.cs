using System;
using System.Collections.Generic;
using System.Linq;

internal static class ConveyorRoutePlanner
{
    public static bool TryFindRoute(
        IndustrialTopologySnapshot topology,
        string fromNodeId,
        string destinationId,
        ItemTransitStackSnapshot stack,
        Func<string, ItemTransitStackSnapshot, bool> canEnter,
        Func<string, string> resolvePortDestination,
        out IReadOnlyList<string> nodeIds,
        out ConveyorStallReason failureReason)
    {
        nodeIds = Array.Empty<string>();
        failureReason = ConveyorStallReason.NoRoute;
        if (topology == null
            || string.IsNullOrWhiteSpace(fromNodeId)
            || !topology.Nodes.ContainsKey(fromNodeId)
            || !stack.IsValid
            || string.IsNullOrWhiteSpace(stack.ItemId))
        {
            return false;
        }

        string normalizedDestination = destinationId?.Trim()
            ?? string.Empty;
        Queue<string> queue = new Queue<string>();
        Dictionary<string, string> parent =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [fromNodeId] = string.Empty
            };
        queue.Enqueue(fromNodeId);
        string target = string.Empty;
        bool filterRejected = false;
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (IsDestination(
                    topology,
                    current,
                    normalizedDestination,
                    fromNodeId,
                    resolvePortDestination))
            {
                target = current;
                break;
            }

            foreach (string next in topology.ConveyorOutgoing
                         .GetValueOrDefault(current, new List<string>())
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                if (parent.ContainsKey(next))
                {
                    continue;
                }

                if (canEnter != null && !canEnter(next, stack))
                {
                    filterRejected = true;
                    continue;
                }

                parent[next] = current;
                queue.Enqueue(next);
            }
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            failureReason = filterRejected
                ? ConveyorStallReason.FilterMismatch
                : ConveyorStallReason.NoRoute;
            return false;
        }

        List<string> route = new List<string>();
        for (string current = target;
             !string.IsNullOrWhiteSpace(current);
             current = parent[current])
        {
            route.Add(current);
        }

        route.Reverse();
        nodeIds = route;
        failureReason = ConveyorStallReason.None;
        return true;
    }

    public static IReadOnlyList<string> FindReachableOverflowNodes(
        IndustrialTopologySnapshot topology,
        string fromNodeId)
    {
        if (topology == null
            || string.IsNullOrWhiteSpace(fromNodeId)
            || !topology.Nodes.ContainsKey(fromNodeId))
        {
            return Array.Empty<string>();
        }

        Queue<(string NodeId, int Distance)> queue =
            new Queue<(string NodeId, int Distance)>();
        HashSet<string> visited =
            new HashSet<string>(StringComparer.Ordinal) { fromNodeId };
        List<(string NodeId, int Distance)> candidates =
            new List<(string NodeId, int Distance)>();
        queue.Enqueue((fromNodeId, 0));
        while (queue.Count > 0)
        {
            (string current, int distance) = queue.Dequeue();
            if (topology.Nodes[current].Overflow != null)
            {
                candidates.Add((current, distance));
            }

            IEnumerable<string> adjacent =
                topology.ConveyorOutgoing
                    .GetValueOrDefault(current, new List<string>())
                    .Concat(topology.ConveyorIncoming
                        .GetValueOrDefault(current, new List<string>()));
            foreach (string next in adjacent
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                if (visited.Add(next))
                {
                    queue.Enqueue((next, distance + 1));
                }
            }
        }

        return candidates
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.NodeId, StringComparer.Ordinal)
            .Select(candidate => candidate.NodeId)
            .ToArray();
    }

    private static bool IsDestination(
        IndustrialTopologySnapshot topology,
        string nodeId,
        string destinationId,
        string startNodeId,
        Func<string, string> resolvePortDestination)
    {
        IndustrialNodeDescriptor node = topology.Nodes[nodeId];
        BuildingConveyorPortAbility port = node.ConveyorPort;
        if (port == null || port.mode == ConveyorPortMode.Input)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(destinationId))
        {
            return string.Equals(
                resolvePortDestination?.Invoke(nodeId)
                    ?? port.destinationId?.Trim()
                    ?? string.Empty,
                destinationId,
                StringComparison.Ordinal);
        }

        return !string.Equals(nodeId, startNodeId, StringComparison.Ordinal)
            && (!topology.ConveyorOutgoing.TryGetValue(
                    nodeId,
                    out List<string> outgoing)
                || outgoing.Count == 0);
    }
}
