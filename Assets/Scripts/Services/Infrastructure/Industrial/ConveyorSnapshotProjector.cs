using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class ConveyorSnapshotProjector
{
    private const float DefaultStallSeconds = 30f;

    public IReadOnlyList<ConveyorNetworkSnapshot> Build(
        IndustrialTopologySnapshot topology,
        IReadOnlyDictionary<string, ConveyorPayloadRuntimeState> payloads,
        Func<IndustrialNodeDescriptor, ConveyorNodeRuntimeState> getNodeState,
        Func<ConveyorPayloadRuntimeState, ItemTransitStackSnapshot> getStack,
        float currentTime)
    {
        if (topology == null)
        {
            return Array.Empty<ConveyorNetworkSnapshot>();
        }

        List<ConveyorNetworkSnapshot> result = new();
        foreach (KeyValuePair<string, List<string>> pair in
                 topology.ConveyorNodesByNetwork.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            HashSet<string> nodeIds = new(pair.Value, StringComparer.Ordinal);
            ConveyorPayloadRuntimeState[] networkPayloads = payloads.Values
                .Where(payload => nodeIds.Contains(payload.SegmentNodeId))
                .OrderBy(payload => payload.PayloadId, StringComparer.Ordinal)
                .ToArray();
            float longest = networkPayloads
                .Where(payload => payload.StalledSince > 0f)
                .Select(payload => currentTime - payload.StalledSince)
                .DefaultIfEmpty(0f)
                .Max();
            bool allUnpowered = networkPayloads.Length > 0
                && networkPayloads.All(payload =>
                    payload.StallReason == ConveyorStallReason.PowerUnavailable);
            bool allStopped = networkPayloads.Length > 0
                && networkPayloads.All(payload =>
                    payload.StallReason == ConveyorStallReason.IntentionallyStopped);
            int totalCapacity = pair.Value.Sum(nodeId =>
                topology.Nodes.TryGetValue(nodeId, out IndustrialNodeDescriptor node)
                    ? ResolveCapacity(node)
                    : 0);
            bool allPayloadsBlocked = networkPayloads.Length > 0
                && networkPayloads.All(payload =>
                    payload.StalledSince > 0f
                    && payload.StallReason != ConveyorStallReason.None);
            float noProgressSeconds = networkPayloads.Length == 0
                ? 0f
                : Mathf.Max(
                    0f,
                    currentTime - networkPayloads.Max(payload => payload.LastMovedAt));
            bool networkHasNoProgress = allPayloadsBlocked
                && noProgressSeconds >= DefaultStallSeconds;
            bool cyclic = topology.CyclicConveyorNetworks.Contains(pair.Key);
            bool deadlocked = cyclic
                && networkPayloads.Length > 0
                && networkPayloads.Length >= totalCapacity
                && networkHasNoProgress;
            ConveyorNetworkState networkState = ConveyorNetworkStateEvaluator.Evaluate(
                cyclic,
                networkPayloads.Length,
                totalCapacity,
                networkHasNoProgress,
                allUnpowered,
                allStopped,
                Mathf.Max(longest, noProgressSeconds),
                DefaultStallSeconds);
            ConveyorPayloadRuntimeState primary = networkPayloads
                .Where(payload => payload.StalledSince > 0f)
                .OrderBy(payload => payload.StalledSince)
                .ThenBy(payload => payload.PayloadId, StringComparer.Ordinal)
                .FirstOrDefault();
            string overflowNode = primary == null
                ? string.Empty
                : ConveyorRoutePlanner.FindReachableOverflowNodes(
                        topology,
                        primary.SegmentNodeId)
                    .FirstOrDefault()
                    ?? string.Empty;
            result.Add(new ConveyorNetworkSnapshot
            {
                NetworkId = pair.Key,
                State = networkState,
                PayloadCount = networkPayloads.Length,
                Capacity = totalCapacity,
                IsCyclic = cyclic,
                LongestStallSeconds = Mathf.Max(longest, noProgressSeconds),
                PrimaryReason = deadlocked
                    ? ConveyorStallReason.CyclicDeadlock
                    : primary?.StallReason ?? ConveyorStallReason.None,
                PlannedOverflowBuildingId =
                    new BuildingInstanceId(overflowNode),
                Payloads = networkPayloads
                    .Select(payload => ToSnapshot(
                        payload,
                        getStack?.Invoke(payload) ?? default,
                        currentTime))
                    .ToArray(),
                Nodes = pair.Value
                    .Where(topology.Nodes.ContainsKey)
                    .Select(nodeId => ToNodeSnapshot(
                        topology.Nodes[nodeId],
                        getNodeState(topology.Nodes[nodeId])))
                    .ToArray()
            });
        }

        return result;
    }

    private static ConveyorPayloadSnapshot ToSnapshot(
        ConveyorPayloadRuntimeState payload,
        ItemTransitStackSnapshot stack,
        float currentTime)
    {
        return new ConveyorPayloadSnapshot
        {
            PayloadId = payload.PayloadId,
            StackId = payload.StackId,
            ItemId = stack.ItemId,
            Quantity = stack.Quantity,
            SegmentBuildingId = new BuildingInstanceId(payload.SegmentNodeId),
            DestinationId = payload.DestinationId,
            Progress = payload.Progress,
            StalledSeconds = payload.StalledSince > 0f
                ? currentTime - payload.StalledSince
                : 0f,
            StallReason = payload.StallReason
        };
    }

    private static ConveyorNodeSnapshot ToNodeSnapshot(
        IndustrialNodeDescriptor node,
        ConveyorNodeRuntimeState state)
    {
        return new ConveyorNodeSnapshot
        {
            BuildingId = new BuildingInstanceId(node.NodeId),
            Capacity = ResolveCapacity(node),
            Enabled = state.Enabled,
            DestinationId = state.DestinationId,
            OverflowPolicy = state.OverflowPolicy,
            ReserveWarehouseId = state.ReserveWarehouseId,
            Filter = new ConveyorFilterCriteria
            {
                itemIds = state.ItemIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList(),
                stockCategories = state.StockCategories
                    .OrderBy(value => value)
                    .ToList(),
                materialIds = state.MaterialIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList(),
                allowForbidden = state.AllowForbidden,
                filterQuality = state.FilterQuality,
                minimumQuality = state.MinimumQuality,
                maximumQuality = state.MaximumQuality,
                filterFreshness = state.FilterFreshness,
                minimumFreshness01 = state.MinimumFreshness01,
                maximumFreshness01 = state.MaximumFreshness01,
                allowContaminated = state.AllowContaminated
            }
        };
    }

    private static int ResolveCapacity(IndustrialNodeDescriptor node)
    {
        return Mathf.Max(
            1,
            node?.Conveyor?.capacity
            ?? node?.ConveyorPort?.capacity
            ?? 1);
    }
}
