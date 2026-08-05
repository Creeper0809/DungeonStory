using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class ConveyorRuntime :
    IConveyorInfrastructureQuery,
    IConveyorInfrastructureCommand,
    IConveyorPayloadTransaction,
    IConveyorRestoreProjection,
    IConveyorRoutingService,
    ITickable
{
    private const float TickInterval = 0.1f;
    private const float DefaultStallSeconds = 30f;
    private const float FailedRouteRetrySeconds = 1f;
    private const int MaxOverflowResolutionsPerTick = 8;

    private readonly IIndustrialInfrastructureTopologyRuntime topologyRuntime;
    private readonly IPowerInfrastructureQuery power;
    private readonly ConveyorItemGateway items;
    private readonly IGameClock clock;
    private readonly ConveyorPayloadAdmissionPolicy admissionPolicy;
    private readonly ConveyorSnapshotProjector snapshotProjector = new();
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly HashSet<string> approvedOverflowPayloads =
        new HashSet<string>(StringComparer.Ordinal);
    private IReadOnlyList<ConveyorNetworkSnapshot> networks =
        Array.Empty<ConveyorNetworkSnapshot>();
    private int topologyVersion = int.MinValue;
    private int routeVersion = 1;
    private int projectedRestoreRevision;
    private float accumulated;
    private readonly List<ItemStackId> autoLoadStackIds =
        new List<ItemStackId>(8);
    private readonly List<string> payloadIterationIds =
        new List<string>(2048);
    private readonly List<string> overflowCandidateIds =
        new List<string>(128);
    private readonly Dictionary<string, int> payloadCountsByNode =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> overflowThresholdByNetwork =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>>
        overflowGatesByStartNode =
            new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.Ordinal);

    private ConveyorAggregateState State =>
        aggregateRootStore.GetOrCreateWritable(
            () => new ConveyorAggregateState(),
            state => state.DeepClone());

    private Dictionary<string, ConveyorNodeRuntimeState> nodeStates =>
        State.Nodes;

    private Dictionary<string, ConveyorPayloadRuntimeState> payloads =>
        State.Payloads;

    private int nextPayloadSequence
    {
        get => State.NextPayloadSequence;
        set => State.NextPayloadSequence = value;
    }

    public ConveyorRuntime(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IPowerInfrastructureQuery power,
        IDungeonItemCatalogProvider catalog,
        ConveyorItemGateway items,
        IGameClock clock,
        ICombatEquipmentRuntime equipment,
        ISurvivalFoodQuery food,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.topologyRuntime = topologyRuntime
            ?? throw new ArgumentNullException(nameof(topologyRuntime));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        projectedRestoreRevision =
            this.aggregateRootStore.PublishedRestoreRevision;
        admissionPolicy = new ConveyorPayloadAdmissionPolicy(
            this.topologyRuntime,
            catalog,
            equipment,
            food,
            GetNodeState);
    }

    public int Version => State.Version;

    public IReadOnlyList<ConveyorNetworkSnapshot> Networks
    {
        get
        {
            EnsureTopology();
            RefreshSnapshots();
            return networks;
        }
    }

    void IConveyorRestoreProjection.EnsureTopology()
    {
        EnsureTopology();
    }

    void IConveyorRestoreProjection.ResetAfterRestore()
    {
        ResetProjectionAfterRestore();
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
        AutoLoadInputPorts();
        CopySortedPayloadIds(payloadIterationIds);
        for (int index = 0; index < payloadIterationIds.Count; index++)
        {
            if (payloads.TryGetValue(
                    payloadIterationIds[index],
                    out ConveyorPayloadRuntimeState payload))
            {
                AdvancePayload(payload, deltaTime);
            }
        }

        ResolveOverflow();
        Touch();
    }

    public bool TryLoadStack(
        ItemStackId stackId,
        BuildableObject inputPort,
        string destinationId,
        out string payloadId,
        out DomainFailure failure)
    {
        payloadId = string.Empty;
        failure = DomainFailure.None;
        EnsureTopology();
        if (!TryResolveNode(
                inputPort,
                out string nodeId,
                out IndustrialNodeDescriptor node)
            || node.ConveyorPort == null
            || node.ConveyorPort.mode == ConveyorPortMode.Output
            || !GetNodeState(node).Enabled)
        {
            failure = new DomainFailure(FailureCode.ConveyorPortUnavailable);
            return false;
        }

        if (CountPayloadsAt(nodeId) >= ResolveCapacity(node))
        {
            failure = new DomainFailure(FailureCode.ConveyorPortFull, nodeId);
            return false;
        }

        if (!items.TryInspect(stackId, out ItemTransitStackSnapshot source))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackUnavailable,
                stackId.Value);
            return false;
        }

        if (!admissionPolicy.CanEnter(nodeId, source))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorFilterMismatch,
                nodeId,
                stackId.Value);
            return false;
        }

        if (!TryFindRouteInternal(
                nodeId,
                destinationId,
                source,
                out IReadOnlyList<string> route,
                out ConveyorStallReason routeFailure))
        {
            failure = new DomainFailure(
                routeFailure == ConveyorStallReason.FilterMismatch
                    ? FailureCode.ConveyorFilterMismatch
                    : FailureCode.ConveyorRouteUnavailable,
                nodeId,
                destinationId ?? string.Empty);
            return false;
        }

        payloadId = "conveyor-payload:"
            + nextPayloadSequence.ToString("D8");
        if (!items.TryBeginTransit(
                stackId,
                items.ResolveNodeDropPosition(node),
                payloadId,
                out _,
                out failure))
        {
            payloadId = string.Empty;
            return false;
        }

        nextPayloadSequence++;
        payloads[payloadId] = new ConveyorPayloadRuntimeState
        {
            PayloadId = payloadId,
            StackId = stackId,
            SegmentNodeId = nodeId,
            DestinationId = destinationId?.Trim() ?? string.Empty,
            Progress = 0f,
            LastMovedAt = clock.Time,
            RouteVersion = routeVersion,
            Route = route,
            RouteIndex = 0
        };
        IncrementPayloadCount(nodeId);
        Touch();
        return true;
    }

    public InfrastructureCommandResult SetNodeEnabled(
        BuildableObject segment,
        bool enabled)
    {
        if (!TryResolveNode(segment, out _, out IndustrialNodeDescriptor node))
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.IndustrialBuildingUnavailable);
        }

        GetNodeState(node).Enabled = enabled;
        InvalidateRoutes();
        return InfrastructureCommandResult.Success();
    }

    public InfrastructureCommandResult SetPortDestination(
        BuildableObject port,
        string destinationId)
    {
        if (!TryResolveNode(
                port,
                out _,
                out IndustrialNodeDescriptor node)
            || node.ConveyorPort == null)
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.ConveyorPortUnavailable);
        }

        GetNodeState(node).DestinationId =
            destinationId?.Trim() ?? string.Empty;
        InvalidateRoutes();
        return InfrastructureCommandResult.Success();
    }

    public InfrastructureCommandResult SetOverflowPolicy(
        BuildableObject segment,
        ConveyorOverflowPolicy policy,
        string reserveWarehouseId)
    {
        if (!Enum.IsDefined(typeof(ConveyorOverflowPolicy), policy)
            || !TryResolveNode(
                segment,
                out _,
                out IndustrialNodeDescriptor node)
            || node.Overflow == null)
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.IndustrialCommandInvalid);
        }

        ConveyorNodeRuntimeState state = GetNodeState(node);
        state.OverflowPolicy = policy;
        state.ReserveWarehouseId = reserveWarehouseId?.Trim()
            ?? string.Empty;
        Touch();
        return InfrastructureCommandResult.Success();
    }

    public InfrastructureCommandResult SetFilter(
        BuildableObject segment,
        IReadOnlyList<string> itemIds,
        IReadOnlyList<StockCategory> stockCategories,
        bool allowForbidden)
    {
        return SetAdvancedFilter(
            segment,
            new ConveyorFilterCriteria
            {
                itemIds = (itemIds ?? Array.Empty<string>()).ToList(),
                stockCategories =
                    (stockCategories ?? Array.Empty<StockCategory>()).ToList(),
                allowForbidden = allowForbidden
            });
    }

    public InfrastructureCommandResult SetAdvancedFilter(
        BuildableObject segment,
        ConveyorFilterCriteria criteria)
    {
        if (!TryResolveNode(segment, out _, out IndustrialNodeDescriptor node))
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.IndustrialBuildingUnavailable);
        }

        ConveyorNodeRuntimeState state = GetNodeState(node);
        ConveyorFilterCriteria source = criteria
            ?? new ConveyorFilterCriteria();
        state.ItemIds.Clear();
        foreach (string itemId in source.itemIds ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                state.ItemIds.Add(itemId.Trim());
            }
        }

        state.StockCategories.Clear();
        foreach (StockCategory category in source.stockCategories
                     ?? new List<StockCategory>())
        {
            state.StockCategories.Add(category);
        }

        state.MaterialIds.Clear();
        foreach (string materialId in source.materialIds
                     ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(materialId))
            {
                state.MaterialIds.Add(materialId.Trim());
            }
        }

        state.AllowForbidden = source.allowForbidden;
        state.FilterQuality = source.filterQuality;
        state.MinimumQuality = source.minimumQuality;
        state.MaximumQuality = source.maximumQuality;
        if ((int)state.MinimumQuality > (int)state.MaximumQuality)
        {
            (state.MinimumQuality, state.MaximumQuality) =
                (state.MaximumQuality, state.MinimumQuality);
        }

        state.FilterFreshness = source.filterFreshness;
        state.MinimumFreshness01 = Mathf.Clamp01(
            source.minimumFreshness01);
        state.MaximumFreshness01 = Mathf.Clamp01(
            source.maximumFreshness01);
        if (state.MinimumFreshness01 > state.MaximumFreshness01)
        {
            (state.MinimumFreshness01, state.MaximumFreshness01) =
                (state.MaximumFreshness01, state.MinimumFreshness01);
        }

        state.AllowContaminated = source.allowContaminated;
        InvalidateRoutes();
        return InfrastructureCommandResult.Success();
    }

    public InfrastructureCommandResult ApproveOverflow(string payloadId)
    {
        string normalized = payloadId?.Trim() ?? string.Empty;
        if (!payloads.ContainsKey(normalized))
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.ConveyorPayloadMissing,
                normalized);
        }

        approvedOverflowPayloads.Add(normalized);
        ResolveOverflow();
        Touch();
        return InfrastructureCommandResult.Success();
    }

    public void MarkTopologyDirty()
    {
        topologyRuntime.MarkDirty();
        topologyVersion = int.MinValue;
        InvalidateRoutes();
    }

    public bool TryFindRoute(
        BuildingInstanceId fromBuildingId,
        string destinationId,
        ItemStackId stackId,
        out IReadOnlyList<BuildingInstanceId> buildingIds,
        out ConveyorStallReason failureReason)
    {
        buildingIds = Array.Empty<BuildingInstanceId>();
        failureReason = ConveyorStallReason.NoRoute;
        if (!fromBuildingId.IsValid
            || !items.TryInspect(stackId, out ItemTransitStackSnapshot stack)
            || !TryFindRouteInternal(
                fromBuildingId.Value,
                destinationId,
                stack,
                out IReadOnlyList<string> nodeIds,
                out failureReason))
        {
            return false;
        }

        buildingIds = nodeIds
            .Select(nodeId => new BuildingInstanceId(nodeId))
            .ToArray();
        return true;
    }

    private bool TryFindRouteInternal(
        string fromNodeId,
        string destinationId,
        ItemTransitStackSnapshot stack,
        out IReadOnlyList<string> nodeIds,
        out ConveyorStallReason failureReason)
    {
        EnsureTopology();
        return ConveyorRoutePlanner.TryFindRoute(
            topologyRuntime.Current,
            fromNodeId,
            destinationId,
            stack,
            admissionPolicy.CanEnter,
            ResolvePortDestination,
            out nodeIds,
            out failureReason);
    }

    private void EnsureTopology()
    {
        EnsureRestoreProjectionCurrent();
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (topology.SourceVersion == topologyVersion)
        {
            return;
        }

        topologyVersion = topology.SourceVersion;
        overflowThresholdByNetwork.Clear();
        overflowGatesByStartNode.Clear();
        foreach (IndustrialNodeDescriptor node in topology.Nodes.Values.Where(
                     node => node.Conveyor != null
                         || node.ConveyorPort != null
                         || node.Overflow != null))
        {
            GetNodeState(node);
        }

        CacheOverflowThresholds(topology);
        InvalidateRoutes();
    }

    private void AdvancePayload(
        ConveyorPayloadRuntimeState payload,
        float deltaTime)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!topology.Nodes.TryGetValue(
                payload.SegmentNodeId,
                out IndustrialNodeDescriptor current))
        {
            SetStall(payload, ConveyorStallReason.NoRoute, countTime: true);
            return;
        }

        ConveyorNodeRuntimeState nodeState = GetNodeState(current);
        if (!nodeState.Enabled)
        {
            SetStall(
                payload,
                ConveyorStallReason.IntentionallyStopped,
                countTime: false);
            return;
        }

        if (current.Conveyor != null
            && current.Conveyor.requiresPower
            && !power.IsPowered(current.Building))
        {
            SetStall(
                payload,
                ConveyorStallReason.PowerUnavailable,
                countTime: false);
            return;
        }

        if (payload.RouteVersion != routeVersion
            || payload.Route == null
            || payload.Route.Count == 0
            || payload.RouteIndex >= payload.Route.Count
            || !string.Equals(
                payload.Route[payload.RouteIndex],
                payload.SegmentNodeId,
                StringComparison.Ordinal))
        {
            if (clock.Time + 0.0001f < payload.NextRouteRetryAt)
            {
                return;
            }

            if (!TryRebuildRoute(payload))
            {
                return;
            }
        }

        if (payload.RouteIndex >= payload.Route.Count - 1)
        {
            TryUnloadAtCurrentNode(payload, current);
            return;
        }

        string nextNodeId = payload.Route[payload.RouteIndex + 1];
        if (!topology.Nodes.TryGetValue(
                nextNodeId,
                out IndustrialNodeDescriptor next))
        {
            SetStall(payload, ConveyorStallReason.NoRoute, countTime: true);
            return;
        }

        ItemTransitStackSnapshot transitStack = ResolveTransitStack(payload);
        if (!transitStack.IsValid
            || !admissionPolicy.CanEnter(nextNodeId, transitStack))
        {
            SetStall(
                payload,
                ConveyorStallReason.FilterMismatch,
                countTime: true);
            return;
        }

        if (CountPayloadsAt(nextNodeId) >= ResolveCapacity(next))
        {
            SetStall(
                payload,
                next.ConveyorPort != null
                    ? ConveyorStallReason.InputPortFull
                    : ConveyorStallReason.NextSegmentOccupied,
                countTime: true);
            return;
        }

        float speed = Mathf.Max(
            0.1f,
            current.Conveyor?.speed ?? 1f);
        payload.Progress += speed * deltaTime;
        if (payload.Progress < 1f)
        {
            ClearStall(payload);
            return;
        }

        payload.Progress -= 1f;
        DecrementPayloadCount(payload.SegmentNodeId);
        payload.PreviousNodeId = payload.SegmentNodeId;
        payload.SegmentNodeId = nextNodeId;
        IncrementPayloadCount(payload.SegmentNodeId);
        payload.RouteIndex++;
        payload.LastMovedAt = clock.Time;
        ClearStall(payload);
    }

    private bool TryRebuildRoute(ConveyorPayloadRuntimeState payload)
    {
        ItemTransitStackSnapshot transitStack = ResolveTransitStack(payload);
        ConveyorStallReason failure = ConveyorStallReason.NoRoute;
        if (!transitStack.IsValid
            || !TryFindRouteInternal(
                payload.SegmentNodeId,
                payload.DestinationId,
                transitStack,
                out IReadOnlyList<string> route,
                out failure))
        {
            payload.NextRouteRetryAt =
                clock.Time + FailedRouteRetrySeconds;
            SetStall(payload, failure, countTime: true);
            return false;
        }

        payload.Route = route;
        payload.RouteIndex = 0;
        payload.RouteVersion = routeVersion;
        payload.NextRouteRetryAt = 0f;
        return true;
    }

    private bool TryUnloadAtCurrentNode(
        ConveyorPayloadRuntimeState payload,
        IndustrialNodeDescriptor node)
    {
        bool restored;
        Vector2Int position = items.ResolveNodeDropPosition(node);
        if (!string.IsNullOrWhiteSpace(payload.DestinationId)
            && node.ConveyorPort != null
            && string.Equals(
                ResolvePortDestination(node.NodeId),
                payload.DestinationId,
                StringComparison.Ordinal))
        {
            restored = items.TryCompleteToFacility(
                payload.StackId,
                payload.PayloadId,
                position,
                payload.DestinationId,
                out _);
        }
        else
        {
            restored = items.TryCompleteLoose(
                payload.StackId,
                payload.PayloadId,
                position,
                out _,
                out _);
        }

        if (!restored)
        {
            SetStall(
                payload,
                ConveyorStallReason.DestinationFull,
                countTime: true);
            return false;
        }

        RemovePayload(payload);
        approvedOverflowPayloads.Remove(payload.PayloadId);
        return true;
    }

    private void AutoLoadInputPorts()
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        for (int index = 0;
             index < topology.ConveyorInputNodes.Count;
             index++)
        {
            IndustrialNodeDescriptor node =
                topology.ConveyorInputNodes[index];
            if (!GetNodeState(node).Enabled
                || CountPayloadsAt(node.NodeId) >= ResolveCapacity(node))
            {
                continue;
            }

            string destinationId = ResolvePortDestination(node.NodeId);
            if (string.IsNullOrWhiteSpace(destinationId))
            {
                continue;
            }

            items.CopyLoadableStackIds(
                items.ResolveNodeDropPosition(node),
                autoLoadStackIds);
            foreach (ItemStackId stackId in autoLoadStackIds)
            {
                if (TryLoadStack(
                        stackId,
                        node.Building,
                        destinationId,
                        out _,
                        out _))
                {
                    break;
                }
            }
        }
    }

    private string ResolvePortDestination(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)
            || !topologyRuntime.Current.Nodes.TryGetValue(
                nodeId,
                out IndustrialNodeDescriptor node)
            || node.ConveyorPort == null)
        {
            return string.Empty;
        }

        ConveyorNodeRuntimeState state = GetNodeState(node);
        return !string.IsNullOrWhiteSpace(state.DestinationId)
            ? state.DestinationId
            : node.ConveyorPort.destinationId?.Trim() ?? string.Empty;
    }

    private void ResolveOverflow()
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        overflowCandidateIds.Clear();
        foreach (KeyValuePair<string, ConveyorPayloadRuntimeState> pair
                 in payloads)
        {
            if (IsOverflowEligible(pair.Value))
            {
                AddOverflowCandidate(pair.Key);
            }
        }

        for (int index = 0; index < overflowCandidateIds.Count; index++)
        {
            if (!payloads.TryGetValue(
                    overflowCandidateIds[index],
                    out ConveyorPayloadRuntimeState payload))
            {
                continue;
            }

            IReadOnlyList<string> gates =
                GetReachableOverflowNodes(topology, payload.SegmentNodeId);
            foreach (string gateId in gates)
            {
                IndustrialNodeDescriptor gate = topology.Nodes[gateId];
                ConveyorNodeRuntimeState state = GetNodeState(gate);
                if (state.OverflowPolicy
                        == ConveyorOverflowPolicy.ManualApproval
                    && !approvedOverflowPayloads.Contains(payload.PayloadId))
                {
                    continue;
                }

                if (TryDischargeOverflow(payload, gate, state))
                {
                    break;
                }
            }

            if (payloads.ContainsKey(payload.PayloadId)
                && gates.Count > 0)
            {
                payload.StallReason = ConveyorStallReason.OverflowBlocked;
            }
        }
    }

    private bool TryDischargeOverflow(
        ConveyorPayloadRuntimeState payload,
        IndustrialNodeDescriptor gate,
        ConveyorNodeRuntimeState state)
    {
        bool tryWarehouse = state.OverflowPolicy
            != ConveyorOverflowPolicy.LooseOnly;
        bool allowAnyWarehouse = state.OverflowPolicy
            == ConveyorOverflowPolicy.AnyCompatibleWarehouseThenLoose;
        if (tryWarehouse
            && items.TryCompleteToWarehouse(
                payload.StackId,
                payload.PayloadId,
                state.ReserveWarehouseId,
                allowAnyWarehouse,
                out _,
                out _))
        {
            CompleteOverflow(payload);
            return true;
        }

        Vector2Int dropPosition = items.ResolveNodeDropPosition(gate);
        if (items.TryCompleteLoose(
                payload.StackId,
                payload.PayloadId,
                dropPosition,
                out _,
                out _))
        {
            CompleteOverflow(payload);
            return true;
        }

        return false;
    }

    private void CompleteOverflow(ConveyorPayloadRuntimeState payload)
    {
        RemovePayload(payload);
        approvedOverflowPayloads.Remove(payload.PayloadId);
    }

    private bool IsOverflowEligible(ConveyorPayloadRuntimeState payload)
    {
        if (payload == null
            || payload.StalledSince <= 0f
            || payload.StallReason is ConveyorStallReason.PowerUnavailable
                or ConveyorStallReason.IntentionallyStopped)
        {
            return false;
        }

        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        string networkId = topology.ConveyorNetworkByNode
            .GetValueOrDefault(payload.SegmentNodeId, string.Empty);
        float threshold = overflowThresholdByNetwork.GetValueOrDefault(
            networkId,
            DefaultStallSeconds);
        return clock.Time - payload.StalledSince >= threshold
            && !string.IsNullOrWhiteSpace(networkId);
    }

    private void CacheOverflowThresholds(
        IndustrialTopologySnapshot topology)
    {
        foreach (KeyValuePair<string, List<string>> network
                 in topology.ConveyorNodesByNetwork)
        {
            float threshold = DefaultStallSeconds;
            bool found = false;
            for (int index = 0; index < network.Value.Count; index++)
            {
                IndustrialNodeDescriptor node =
                    topology.Nodes[network.Value[index]];
                if (node.Overflow == null)
                {
                    continue;
                }

                float candidate = Mathf.Max(
                    1f,
                    node.Overflow.stallSeconds);
                threshold = found
                    ? Mathf.Min(threshold, candidate)
                    : candidate;
                found = true;
            }

            overflowThresholdByNetwork[network.Key] = threshold;
        }
    }

    private IReadOnlyList<string> GetReachableOverflowNodes(
        IndustrialTopologySnapshot topology,
        string startNodeId)
    {
        if (!overflowGatesByStartNode.TryGetValue(
                startNodeId,
                out IReadOnlyList<string> gates))
        {
            gates = ConveyorRoutePlanner.FindReachableOverflowNodes(
                topology,
                startNodeId);
            overflowGatesByStartNode[startNodeId] = gates;
        }

        return gates;
    }

    private void AddOverflowCandidate(string payloadId)
    {
        int insertAt = overflowCandidateIds.Count;
        for (int index = 0;
             index < overflowCandidateIds.Count;
             index++)
        {
            if (CompareOverflowCandidates(
                    payloadId,
                    overflowCandidateIds[index]) < 0)
            {
                insertAt = index;
                break;
            }
        }

        overflowCandidateIds.Insert(insertAt, payloadId);
        if (overflowCandidateIds.Count
            > MaxOverflowResolutionsPerTick)
        {
            overflowCandidateIds.RemoveAt(
                overflowCandidateIds.Count - 1);
        }
    }

    private void RefreshSnapshots()
    {
        networks = snapshotProjector.Build(
            topologyRuntime.Current,
            payloads,
            GetNodeState,
            ResolveTransitStack,
            clock.Time);
    }

    private void SetStall(
        ConveyorPayloadRuntimeState payload,
        ConveyorStallReason reason,
        bool countTime)
    {
        payload.StallReason = reason;
        if (!countTime)
        {
            payload.StalledSince = 0f;
            return;
        }

        if (payload.StalledSince <= 0f)
        {
            payload.StalledSince = clock.Time;
        }
    }

    private static void ClearStall(ConveyorPayloadRuntimeState payload)
    {
        payload.StallReason = ConveyorStallReason.None;
        payload.StalledSince = 0f;
    }

    private int CountPayloadsAt(string nodeId)
    {
        return string.IsNullOrWhiteSpace(nodeId)
            ? 0
            : payloadCountsByNode.GetValueOrDefault(nodeId, 0);
    }

    private void CopySortedPayloadIds(List<string> destination)
    {
        destination.Clear();
        foreach (string payloadId in payloads.Keys)
        {
            destination.Add(payloadId);
        }

        destination.Sort(StringComparer.Ordinal);
    }

    private int CompareOverflowCandidates(string leftId, string rightId)
    {
        int approvalComparison =
            approvedOverflowPayloads.Contains(rightId).CompareTo(
                approvedOverflowPayloads.Contains(leftId));
        if (approvalComparison != 0)
        {
            return approvalComparison;
        }

        if (!payloads.TryGetValue(
                leftId,
                out ConveyorPayloadRuntimeState left))
        {
            return payloads.ContainsKey(rightId) ? 1 : 0;
        }

        if (!payloads.TryGetValue(
                rightId,
                out ConveyorPayloadRuntimeState right))
        {
            return -1;
        }

        int stalledComparison =
            left.StalledSince.CompareTo(right.StalledSince);
        return stalledComparison != 0
            ? stalledComparison
            : string.CompareOrdinal(leftId, rightId);
    }

    private void IncrementPayloadCount(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        payloadCountsByNode[nodeId] =
            payloadCountsByNode.GetValueOrDefault(nodeId, 0) + 1;
    }

    private void DecrementPayloadCount(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)
            || !payloadCountsByNode.TryGetValue(nodeId, out int count))
        {
            return;
        }

        if (count <= 1)
        {
            payloadCountsByNode.Remove(nodeId);
        }
        else
        {
            payloadCountsByNode[nodeId] = count - 1;
        }
    }

    private bool RemovePayload(ConveyorPayloadRuntimeState payload)
    {
        if (payload == null || !payloads.Remove(payload.PayloadId))
        {
            return false;
        }

        DecrementPayloadCount(payload.SegmentNodeId);
        return true;
    }

    private static int ResolveCapacity(IndustrialNodeDescriptor node)
    {
        return Mathf.Max(
            1,
            node.Conveyor?.capacity
            ?? node.ConveyorPort?.capacity
            ?? 1);
    }

    private ConveyorNodeRuntimeState GetNodeState(
        IndustrialNodeDescriptor node)
    {
        if (!nodeStates.TryGetValue(
                node.NodeId,
                out ConveyorNodeRuntimeState state))
        {
            state = new ConveyorNodeRuntimeState
            {
                OverflowPolicy = node.Overflow?.defaultPolicy
                    ?? ConveyorOverflowPolicy.ReserveWarehouseThenLoose,
                AllowForbidden = node.Conveyor?.allowForbidden ?? false,
                FilterQuality = node.Conveyor?.filterQuality ?? false,
                MinimumQuality = node.Conveyor?.minimumQuality
                    ?? CombatEquipmentQuality.Awful,
                MaximumQuality = node.Conveyor?.maximumQuality
                    ?? CombatEquipmentQuality.Legendary,
                FilterFreshness = node.Conveyor?.filterFreshness ?? false,
                MinimumFreshness01 = Mathf.Clamp01(
                    node.Conveyor?.minimumFreshness01 ?? 0f),
                MaximumFreshness01 = Mathf.Clamp01(
                    node.Conveyor?.maximumFreshness01 ?? 1f),
                AllowContaminated =
                    node.Conveyor?.allowContaminated ?? true
            };
            if (node.Conveyor?.allowedItemIds != null)
            {
                foreach (string itemId in node.Conveyor.allowedItemIds)
                {
                    if (!string.IsNullOrWhiteSpace(itemId))
                    {
                        state.ItemIds.Add(itemId.Trim());
                    }
                }
            }

            if (node.Conveyor?.allowedStockCategories != null)
            {
                foreach (StockCategory category in
                         node.Conveyor.allowedStockCategories)
                {
                    state.StockCategories.Add(category);
                }
            }

            if (node.Conveyor?.allowedMaterialIds != null)
            {
                foreach (string materialId in
                         node.Conveyor.allowedMaterialIds)
                {
                    if (!string.IsNullOrWhiteSpace(materialId))
                    {
                        state.MaterialIds.Add(materialId.Trim());
                    }
                }
            }

            nodeStates[node.NodeId] = state;
        }

        return state;
    }

    private bool TryResolveNode(
        BuildableObject building,
        out string nodeId,
        out IndustrialNodeDescriptor node)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (building != null
            && topology.NodeIdsByBuilding.TryGetValue(building, out nodeId)
            && topology.Nodes.TryGetValue(nodeId, out node)
            && (node.Conveyor != null
                || node.ConveyorPort != null
                || node.Overflow != null))
        {
            return true;
        }

        nodeId = string.Empty;
        node = null;
        return false;
    }

    private ItemTransitStackSnapshot ResolveTransitStack(
        ConveyorPayloadRuntimeState payload) =>
        payload != null
        && items.TryGetTransit(
            payload.StackId,
            payload.PayloadId,
            out ItemTransitStackSnapshot stack)
                ? stack
                : default;

    private void InvalidateRoutes()
    {
        unchecked
        {
            routeVersion++;
        }

        foreach (ConveyorPayloadRuntimeState payload in payloads.Values)
        {
            payload.RouteVersion = 0;
            payload.NextRouteRetryAt = 0f;
            payload.StalledSince = 0f;
            payload.StallReason = ConveyorStallReason.None;
        }

        Touch();
    }

    private void Touch()
    {
        unchecked
        {
            State.Version++;
        }
    }

    private void EnsureRestoreProjectionCurrent()
    {
        int revision = aggregateRootStore.PublishedRestoreRevision;
        if (projectedRestoreRevision == revision)
        {
            return;
        }

        projectedRestoreRevision = revision;
        ResetProjectionAfterRestore();
    }

    private void ResetProjectionAfterRestore()
    {
        topologyVersion = int.MinValue;
        accumulated = 0f;
        approvedOverflowPayloads.Clear();
        payloadCountsByNode.Clear();
        foreach (ConveyorPayloadRuntimeState payload in payloads.Values)
        {
            IncrementPayloadCount(payload.SegmentNodeId);
        }

        autoLoadStackIds.Clear();
        payloadIterationIds.Clear();
        overflowCandidateIds.Clear();
        overflowThresholdByNetwork.Clear();
        overflowGatesByStartNode.Clear();
        networks = Array.Empty<ConveyorNetworkSnapshot>();
        unchecked
        {
            routeVersion++;
        }
    }

}
