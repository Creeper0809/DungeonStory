using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class ConveyorNodeRuntimeState
{
    public bool Enabled = true;
    public ConveyorOverflowPolicy OverflowPolicy =
        ConveyorOverflowPolicy.ReserveWarehouseThenLoose;
    public string ReserveWarehouseId = string.Empty;
    public readonly HashSet<string> ItemIds =
        new HashSet<string>(StringComparer.Ordinal);
    public readonly HashSet<StockCategory> StockCategories =
        new HashSet<StockCategory>();
    public readonly HashSet<string> MaterialIds =
        new HashSet<string>(StringComparer.Ordinal);
    public bool AllowForbidden;
    public bool FilterQuality;
    public CombatEquipmentQuality MinimumQuality =
        CombatEquipmentQuality.Awful;
    public CombatEquipmentQuality MaximumQuality =
        CombatEquipmentQuality.Legendary;
    public bool FilterFreshness;
    public float MinimumFreshness01;
    public float MaximumFreshness01 = 1f;
    public bool AllowContaminated = true;
    public string DestinationId = string.Empty;
}

internal sealed class ConveyorPayloadRuntimeState
{
    public string PayloadId = string.Empty;
    public string SegmentNodeId = string.Empty;
    public string PreviousNodeId = string.Empty;
    public string DestinationId = string.Empty;
    public float Progress;
    public float LastMovedAt;
    public float StalledSince;
    public int RouteVersion;
    public ConveyorStallReason StallReason;
    public WorldItemStackSaveData Stack = new WorldItemStackSaveData();
    public IReadOnlyList<string> Route = Array.Empty<string>();
    public int RouteIndex;
    public float NextRouteRetryAt;
}

internal sealed class ConveyorRuntime :
    IConveyorCommandService,
    IConveyorRoutingService,
    ITickable
{
    private const float TickInterval = 0.1f;
    private const float DefaultStallSeconds = 30f;
    private const float FailedRouteRetrySeconds = 1f;
    private const int MaxOverflowResolutionsPerTick = 8;

    private readonly IIndustrialInfrastructureTopologyRuntime topologyRuntime;
    private readonly IElectricalNetworkRuntime power;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly ISurvivalFoodRuntime food;
    private readonly ConveyorItemGateway items;
    private readonly IGameClock clock;
    private readonly Dictionary<string, ConveyorNodeRuntimeState> nodeStates =
        new Dictionary<string, ConveyorNodeRuntimeState>(
            StringComparer.Ordinal);
    private readonly Dictionary<string, ConveyorPayloadRuntimeState> payloads =
        new Dictionary<string, ConveyorPayloadRuntimeState>(
            StringComparer.Ordinal);
    private readonly HashSet<string> approvedOverflowPayloads =
        new HashSet<string>(StringComparer.Ordinal);
    private IReadOnlyList<ConveyorNetworkSnapshot> networks =
        Array.Empty<ConveyorNetworkSnapshot>();
    private int topologyVersion = int.MinValue;
    private int routeVersion = 1;
    private int nextPayloadSequence = 1;
    private float accumulated;
    private readonly List<string> autoLoadStackIds = new List<string>(8);
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

    public ConveyorRuntime(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IElectricalNetworkRuntime power,
        IDungeonItemCatalogProvider catalog,
        ConveyorItemGateway items,
        IGameClock clock,
        ICombatEquipmentRuntime equipment = null,
        ISurvivalFoodRuntime food = null)
    {
        this.topologyRuntime = topologyRuntime
            ?? throw new ArgumentNullException(nameof(topologyRuntime));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.equipment = equipment;
        this.food = food;
    }

    public int Version { get; private set; }

    public IReadOnlyList<ConveyorNetworkSnapshot> Networks
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
        string stackId,
        BuildableObject inputPort,
        string destinationId,
        out string payloadId,
        out string failureReason)
    {
        payloadId = string.Empty;
        failureReason = string.Empty;
        EnsureTopology();
        if (!TryResolveNode(
                inputPort,
                out string nodeId,
                out IndustrialNodeDescriptor node)
            || node.ConveyorPort == null
            || node.ConveyorPort.mode == ConveyorPortMode.Output)
        {
            failureReason = "컨베이어 입력 포트를 선택해야 합니다.";
            return false;
        }

        if (!GetNodeState(node).Enabled)
        {
            failureReason = "입력 포트가 정지되어 있습니다.";
            return false;
        }

        int capacity = ResolveCapacity(node);
        if (CountPayloadsAt(nodeId) >= capacity)
        {
            failureReason = "입력 포트 버퍼가 가득 찼습니다.";
            return false;
        }

        WorldItemStackSaveData source = TryPeekStack(stackId);
        if (source == null)
        {
            failureReason = "컨베이어에 올릴 아이템을 찾을 수 없습니다.";
            return false;
        }

        if (!TryFindRoute(
                nodeId,
                destinationId,
                source,
                out IReadOnlyList<string> route,
                out ConveyorStallReason routeFailure))
        {
            failureReason = FormatStallReason(routeFailure);
            return false;
        }

        Vector2Int inputPosition = items.ResolveNodeDropPosition(node);
        if (!items.TryExtract(
                stackId,
                inputPosition,
                out WorldItemStackSaveData extracted,
                out failureReason))
        {
            return false;
        }

        payloadId = "conveyor-payload:"
            + nextPayloadSequence++.ToString("D8");
        payloads[payloadId] = new ConveyorPayloadRuntimeState
        {
            PayloadId = payloadId,
            SegmentNodeId = nodeId,
            DestinationId = destinationId?.Trim() ?? string.Empty,
            Progress = 0f,
            LastMovedAt = clock.Time,
            RouteVersion = routeVersion,
            Stack = extracted,
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
            return InfrastructureCommandResult.Failure(
                "컨베이어 시설을 선택해야 합니다.");
        }

        GetNodeState(node).Enabled = enabled;
        InvalidateRoutes();
        return InfrastructureCommandResult.Success(
            enabled ? "컨베이어를 가동했습니다." : "컨베이어를 정지했습니다.");
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
            return InfrastructureCommandResult.Failure(
                "컨베이어 포트를 선택해야 합니다.");
        }

        GetNodeState(node).DestinationId =
            destinationId?.Trim() ?? string.Empty;
        InvalidateRoutes();
        return InfrastructureCommandResult.Success(
            string.IsNullOrWhiteSpace(destinationId)
                ? "포트 목적지를 해제했습니다."
                : $"포트 목적지를 {destinationId.Trim()}(으)로 설정했습니다.");
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
            return InfrastructureCommandResult.Failure(
                "오버플로 배출구를 선택해야 합니다.");
        }

        ConveyorNodeRuntimeState state = GetNodeState(node);
        state.OverflowPolicy = policy;
        state.ReserveWarehouseId = reserveWarehouseId?.Trim()
            ?? string.Empty;
        Touch();
        return InfrastructureCommandResult.Success(
            "오버플로 배출 정책을 변경했습니다.");
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
            return InfrastructureCommandResult.Failure(
                "컨베이어 시설을 선택해야 합니다.");
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
        return InfrastructureCommandResult.Success(
            "컨베이어 필터를 변경했습니다.");
    }

    public InfrastructureCommandResult ApproveOverflow(string payloadId)
    {
        string normalized = payloadId?.Trim() ?? string.Empty;
        if (!payloads.ContainsKey(normalized))
        {
            return InfrastructureCommandResult.Failure(
                "승인할 정체 화물을 찾을 수 없습니다.");
        }

        approvedOverflowPayloads.Add(normalized);
        ResolveOverflow();
        Touch();
        return InfrastructureCommandResult.Success("오버플로 배출을 승인했습니다.");
    }

    public void MarkTopologyDirty()
    {
        topologyRuntime.MarkDirty();
        topologyVersion = int.MinValue;
        InvalidateRoutes();
    }

    public bool TryFindRoute(
        string fromNodeId,
        string destinationId,
        WorldItemStackSaveData stack,
        out IReadOnlyList<string> nodeIds,
        out ConveyorStallReason failureReason)
    {
        EnsureTopology();
        return ConveyorRoutePlanner.TryFindRoute(
            topologyRuntime.Current,
            fromNodeId,
            destinationId,
            stack,
            CanPayloadEnter,
            ResolvePortDestination,
            out nodeIds,
            out failureReason);
    }

    public DungeonConveyorInfrastructureSaveData Capture()
    {
        EnsureTopology();
        return new DungeonConveyorInfrastructureSaveData
        {
            nextPayloadSequence = nextPayloadSequence,
            nodes = nodeStates
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ConveyorNodeSaveData
                {
                    nodeId = pair.Key,
                    enabled = pair.Value.Enabled,
                    destinationId = pair.Value.DestinationId,
                    overflowPolicy = pair.Value.OverflowPolicy,
                    reserveWarehouseId =
                        pair.Value.ReserveWarehouseId ?? string.Empty,
                    filter = new ConveyorFilterSaveData
                    {
                        itemIds = pair.Value.ItemIds
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToList(),
                        stockCategories = pair.Value.StockCategories
                            .Select(category => (int)category)
                            .OrderBy(value => value)
                            .ToList(),
                        materialIds = pair.Value.MaterialIds
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToList(),
                        allowForbidden = pair.Value.AllowForbidden,
                        filterQuality = pair.Value.FilterQuality,
                        minimumQuality = (int)pair.Value.MinimumQuality,
                        maximumQuality = (int)pair.Value.MaximumQuality,
                        filterFreshness = pair.Value.FilterFreshness,
                        minimumFreshness01 =
                            pair.Value.MinimumFreshness01,
                        maximumFreshness01 =
                            pair.Value.MaximumFreshness01,
                        allowContaminated =
                            pair.Value.AllowContaminated
                    }
                })
                .ToList(),
            payloads = payloads.Values
                .OrderBy(payload => payload.PayloadId, StringComparer.Ordinal)
                .Select(payload => new ConveyorPayloadSaveData
                {
                    payloadId = payload.PayloadId,
                    segmentNodeId = payload.SegmentNodeId,
                    previousNodeId = payload.PreviousNodeId,
                    destinationId = payload.DestinationId,
                    progress = payload.Progress,
                    lastMovedAt = Mathf.Max(
                        0f,
                        clock.Time - payload.LastMovedAt),
                    stalledSince = payload.StalledSince > 0f
                        ? Mathf.Max(0f, clock.Time - payload.StalledSince)
                        : 0f,
                    routeVersion = payload.RouteVersion,
                    stallReason = payload.StallReason,
                    stack = CloneStack(payload.Stack)
                })
                .ToList()
        };
    }

    public void Restore(DungeonConveyorInfrastructureSaveData snapshot)
    {
        nodeStates.Clear();
        payloads.Clear();
        payloadCountsByNode.Clear();
        approvedOverflowPayloads.Clear();
        nextPayloadSequence = Mathf.Max(
            1,
            snapshot?.nextPayloadSequence ?? 1);
        foreach (ConveyorNodeSaveData saved in snapshot?.nodes
                 ?? new List<ConveyorNodeSaveData>())
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.nodeId))
            {
                continue;
            }

            ConveyorNodeRuntimeState state = new ConveyorNodeRuntimeState
            {
                Enabled = saved.enabled,
                DestinationId =
                    saved.destinationId?.Trim() ?? string.Empty,
                OverflowPolicy = Enum.IsDefined(
                    typeof(ConveyorOverflowPolicy),
                    saved.overflowPolicy)
                        ? saved.overflowPolicy
                        : ConveyorOverflowPolicy.ReserveWarehouseThenLoose,
                ReserveWarehouseId =
                    saved.reserveWarehouseId?.Trim() ?? string.Empty,
                AllowForbidden = saved.filter?.allowForbidden ?? false,
                FilterQuality = saved.filter?.filterQuality ?? false,
                MinimumQuality = ParseQuality(
                    saved.filter?.minimumQuality
                    ?? (int)CombatEquipmentQuality.Awful,
                    CombatEquipmentQuality.Awful),
                MaximumQuality = ParseQuality(
                    saved.filter?.maximumQuality
                    ?? (int)CombatEquipmentQuality.Legendary,
                    CombatEquipmentQuality.Legendary),
                FilterFreshness =
                    saved.filter?.filterFreshness ?? false,
                MinimumFreshness01 = Mathf.Clamp01(
                    saved.filter?.minimumFreshness01 ?? 0f),
                MaximumFreshness01 = Mathf.Clamp01(
                    saved.filter?.maximumFreshness01 ?? 1f),
                AllowContaminated =
                    saved.filter?.allowContaminated ?? true
            };
            foreach (string itemId in saved.filter?.itemIds
                     ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(itemId))
                {
                    state.ItemIds.Add(itemId.Trim());
                }
            }

            foreach (int category in saved.filter?.stockCategories
                     ?? new List<int>())
            {
                if (Enum.IsDefined(typeof(StockCategory), category))
                {
                    state.StockCategories.Add((StockCategory)category);
                }
            }

            foreach (string materialId in saved.filter?.materialIds
                     ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(materialId))
                {
                    state.MaterialIds.Add(materialId.Trim());
                }
            }

            if ((int)state.MinimumQuality > (int)state.MaximumQuality)
            {
                (state.MinimumQuality, state.MaximumQuality) =
                    (state.MaximumQuality, state.MinimumQuality);
            }

            if (state.MinimumFreshness01 > state.MaximumFreshness01)
            {
                (state.MinimumFreshness01, state.MaximumFreshness01) =
                    (state.MaximumFreshness01, state.MinimumFreshness01);
            }

            nodeStates[saved.nodeId.Trim()] = state;
        }

        foreach (ConveyorPayloadSaveData saved in snapshot?.payloads
                 ?? new List<ConveyorPayloadSaveData>())
        {
            if (saved == null
                || string.IsNullOrWhiteSpace(saved.payloadId)
                || saved.stack == null
                || saved.stack.quantity <= 0)
            {
                continue;
            }

            string payloadId = saved.payloadId.Trim();
            string segmentNodeId =
                saved.segmentNodeId?.Trim() ?? string.Empty;
            payloads[payloadId] =
                new ConveyorPayloadRuntimeState
                {
                    PayloadId = payloadId,
                    SegmentNodeId = segmentNodeId,
                    PreviousNodeId =
                        saved.previousNodeId?.Trim() ?? string.Empty,
                    DestinationId =
                        saved.destinationId?.Trim() ?? string.Empty,
                    Progress = Mathf.Clamp01(saved.progress),
                    LastMovedAt = clock.Time
                        - Mathf.Max(0f, saved.lastMovedAt),
                    StalledSince = saved.stalledSince > 0f
                        ? clock.Time - saved.stalledSince
                        : 0f,
                    RouteVersion = 0,
                    StallReason = saved.stallReason,
                    Stack = CloneStack(saved.stack)
                };
            IncrementPayloadCount(segmentNodeId);
        }

        topologyVersion = int.MinValue;
        InvalidateRoutes();
        EnsureTopology();
        Touch();
    }

    private void EnsureTopology()
    {
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

        if (!CanPayloadEnter(nextNodeId, payload.Stack))
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
        if (!TryFindRoute(
                payload.SegmentNodeId,
                payload.DestinationId,
                payload.Stack,
                out IReadOnlyList<string> route,
                out ConveyorStallReason failure))
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
        string failureReason;
        Vector2Int position = items.ResolveNodeDropPosition(node);
        if (!string.IsNullOrWhiteSpace(payload.DestinationId)
            && node.ConveyorPort != null
            && string.Equals(
                ResolvePortDestination(node.NodeId),
                payload.DestinationId,
                StringComparison.Ordinal))
        {
            restored = items.TryRestoreToFacility(
                payload.Stack,
                position,
                payload.DestinationId,
                out failureReason);
        }
        else
        {
            restored = items.TryRestoreLoose(
                payload.Stack,
                position,
                out _,
                out failureReason);
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
            foreach (string stackId in autoLoadStackIds)
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
            && items.TryRestoreToWarehouse(
                payload.Stack,
                state.ReserveWarehouseId,
                allowAnyWarehouse,
                out _,
                out _))
        {
            CompleteOverflow(payload);
            return true;
        }

        Vector2Int dropPosition = items.ResolveNodeDropPosition(gate);
        if (items.TryRestoreLoose(
                payload.Stack,
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

    private bool CanPayloadEnter(
        string nodeId,
        WorldItemStackSaveData stack)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!topology.Nodes.TryGetValue(
                nodeId,
                out IndustrialNodeDescriptor node)
            || !GetNodeState(node).Enabled)
        {
            return false;
        }

        ConveyorNodeRuntimeState runtimeFilter = GetNodeState(node);
        BuildingConveyorSegmentAbility staticFilter = node.Conveyor;
        if (stack.forbidden
            && !(runtimeFilter.AllowForbidden
                || staticFilter?.allowForbidden == true))
        {
            return false;
        }

        if (!catalog.TryGetDefinition(
                stack.itemId,
                out DungeonItemDefinition definition))
        {
            return runtimeFilter.ItemIds.Count == 0
                && runtimeFilter.StockCategories.Count == 0
                && (staticFilter?.allowedItemIds == null
                    || staticFilter.allowedItemIds.Length == 0)
                && (staticFilter?.allowedStockCategories == null
                    || staticFilter.allowedStockCategories.Length == 0);
        }

        bool runtimeAllows = runtimeFilter.ItemIds.Count == 0
            && runtimeFilter.StockCategories.Count == 0
            || runtimeFilter.ItemIds.Contains(stack.itemId)
            || runtimeFilter.StockCategories.Contains(
                definition.StockCategory);
        bool staticAllows = staticFilter == null
            || (staticFilter.allowedItemIds == null
                || staticFilter.allowedItemIds.Length == 0)
            && (staticFilter.allowedStockCategories == null
                || staticFilter.allowedStockCategories.Length == 0)
            || staticFilter.allowedItemIds?.Contains(
                stack.itemId,
                StringComparer.Ordinal) == true
            || staticFilter.allowedStockCategories?.Contains(
                definition.StockCategory) == true;
        return runtimeAllows
            && staticAllows
            && MatchesRuntimeMetadata(runtimeFilter, stack)
            && MatchesStaticMetadata(staticFilter, stack);
    }

    private void RefreshSnapshots()
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        List<ConveyorNetworkSnapshot> result =
            new List<ConveyorNetworkSnapshot>();
        foreach (KeyValuePair<string, List<string>> pair in
                 topology.ConveyorNodesByNetwork.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            HashSet<string> nodeIds = new HashSet<string>(
                pair.Value,
                StringComparer.Ordinal);
            ConveyorPayloadRuntimeState[] networkPayloads = payloads.Values
                .Where(payload => nodeIds.Contains(payload.SegmentNodeId))
                .OrderBy(payload => payload.PayloadId, StringComparer.Ordinal)
                .ToArray();
            float longest = networkPayloads
                .Where(payload => payload.StalledSince > 0f)
                .Select(payload => clock.Time - payload.StalledSince)
                .DefaultIfEmpty(0f)
                .Max();
            bool allUnpowered = networkPayloads.Length > 0
                && networkPayloads.All(payload =>
                    payload.StallReason
                    == ConveyorStallReason.PowerUnavailable);
            bool allStopped = networkPayloads.Length > 0
                && networkPayloads.All(payload =>
                    payload.StallReason
                    == ConveyorStallReason.IntentionallyStopped);
            int totalCapacity = pair.Value.Sum(nodeId =>
                topology.Nodes.TryGetValue(
                    nodeId,
                    out IndustrialNodeDescriptor node)
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
                    clock.Time - networkPayloads.Max(payload =>
                        payload.LastMovedAt));
            bool networkHasNoProgress = allPayloadsBlocked
                && noProgressSeconds >= DefaultStallSeconds;
            bool cyclic = topology.CyclicConveyorNetworks
                .Contains(pair.Key);
            bool deadlocked = cyclic
                && networkPayloads.Length > 0
                && networkPayloads.Length >= totalCapacity
                && networkHasNoProgress;
            ConveyorNetworkState networkState =
                ConveyorNetworkStateEvaluator.Evaluate(
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
                LongestStallSeconds = Mathf.Max(
                    longest,
                    noProgressSeconds),
                PrimaryReason = deadlocked
                    ? ConveyorStallReason.CyclicDeadlock
                    : primary?.StallReason ?? ConveyorStallReason.None,
                PlannedOverflowNodeId = overflowNode,
                Payloads = networkPayloads.Select(ToSnapshot).ToArray(),
                Nodes = pair.Value
                    .Where(topology.Nodes.ContainsKey)
                    .Select(nodeId => ToNodeSnapshot(
                        topology.Nodes[nodeId],
                        GetNodeState(topology.Nodes[nodeId])))
                    .ToArray()
            });
        }

        networks = result;
    }

    private ConveyorPayloadSnapshot ToSnapshot(
        ConveyorPayloadRuntimeState payload)
    {
        return new ConveyorPayloadSnapshot
        {
            PayloadId = payload.PayloadId,
            StackId = payload.Stack.stackId,
            ItemId = payload.Stack.itemId,
            Quantity = payload.Stack.quantity,
            SegmentNodeId = payload.SegmentNodeId,
            DestinationId = payload.DestinationId,
            Progress = payload.Progress,
            StalledSeconds = payload.StalledSince > 0f
                ? clock.Time - payload.StalledSince
                : 0f,
            StallReason = payload.StallReason
        };
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

    private bool MatchesRuntimeMetadata(
        ConveyorNodeRuntimeState filter,
        WorldItemStackSaveData stack)
    {
        if (!TryMatchEquipment(
            stack,
            filter.MaterialIds,
            filter.FilterQuality,
            filter.MinimumQuality,
            filter.MaximumQuality))
        {
            return false;
        }

        return MatchesFreshness(
            stack,
            filter.FilterFreshness,
            filter.MinimumFreshness01,
            filter.MaximumFreshness01,
            filter.AllowContaminated);
    }

    private bool MatchesStaticMetadata(
        BuildingConveyorSegmentAbility filter,
        WorldItemStackSaveData stack)
    {
        if (filter == null)
        {
            return true;
        }

        if (!TryMatchEquipment(
                stack,
                filter.allowedMaterialIds,
                filter.filterQuality,
                filter.minimumQuality,
                filter.maximumQuality))
        {
            return false;
        }

        return MatchesFreshness(
            stack,
            filter.filterFreshness,
            filter.minimumFreshness01,
            filter.maximumFreshness01,
            filter.allowContaminated);
    }

    private bool TryMatchEquipment(
        WorldItemStackSaveData stack,
        ICollection<string> materialIds,
        bool filterQuality,
        CombatEquipmentQuality minimumQuality,
        CombatEquipmentQuality maximumQuality)
    {
        bool hasMaterialFilter = materialIds != null
            && materialIds.Count > 0;
        if (!hasMaterialFilter && !filterQuality)
        {
            return true;
        }

        if (equipment == null
            || !equipment.TryGetInstanceBySourceStack(
                stack.stackId,
                out CombatEquipmentInstance instance))
        {
            return false;
        }

        if (hasMaterialFilter
            && !materialIds.Contains(instance.materialId))
        {
            return false;
        }

        return !filterQuality
            || (int)instance.quality >= (int)minimumQuality
            && (int)instance.quality <= (int)maximumQuality;
    }

    private bool MatchesFreshness(
        WorldItemStackSaveData stack,
        bool filterFreshness,
        float minimumFreshness01,
        float maximumFreshness01,
        bool allowContaminated)
    {
        if (!filterFreshness && allowContaminated)
        {
            return true;
        }

        bool contaminated = stack.contamination > 0.001f;
        if (food != null
            && food.TryGetItemStatus(
                stack.stackId,
                stack.itemId,
                out SurvivalItemStatus status))
        {
            contaminated |= status.Contaminated;
            if (filterFreshness
                && (status.Freshness01
                        + 0.0001f < Mathf.Clamp01(minimumFreshness01)
                    || status.Freshness01
                        - 0.0001f > Mathf.Clamp01(maximumFreshness01)))
            {
                return false;
            }
        }
        else if (filterFreshness)
        {
            return false;
        }

        return allowContaminated || !contaminated;
    }

    private static ConveyorNodeSnapshot ToNodeSnapshot(
        IndustrialNodeDescriptor node,
        ConveyorNodeRuntimeState state)
    {
        return new ConveyorNodeSnapshot
        {
            NodeId = node.NodeId,
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

    private static CombatEquipmentQuality ParseQuality(
        int value,
        CombatEquipmentQuality fallback)
    {
        return Enum.IsDefined(typeof(CombatEquipmentQuality), value)
            ? (CombatEquipmentQuality)value
            : fallback;
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

    private WorldItemStackSaveData TryPeekStack(string stackId)
    {
        return items.TryPeek(stackId, out WorldItemStackSaveData stack)
            ? stack
            : null;
    }

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
            Version++;
        }
    }

    private static string FormatStallReason(ConveyorStallReason reason)
    {
        return reason switch
        {
            ConveyorStallReason.FilterMismatch =>
                "필터 조건에 맞는 경로가 없습니다.",
            ConveyorStallReason.InputPortFull =>
                "입력 포트가 가득 찼습니다.",
            ConveyorStallReason.DestinationFull =>
                "목적지 용량이 부족합니다.",
            ConveyorStallReason.CyclicDeadlock =>
                "순환 컨베이어가 교착되었습니다.",
            _ => "목적지까지 이어진 컨베이어 경로가 없습니다."
        };
    }

    private static WorldItemStackSaveData CloneStack(
        WorldItemStackSaveData source)
    {
        if (source == null)
        {
            return new WorldItemStackSaveData();
        }

        return new WorldItemStackSaveData
        {
            stackId = source.stackId,
            itemId = source.itemId,
            quantity = source.quantity,
            state = source.state,
            gridX = source.gridX,
            gridY = source.gridY,
            reservedByPersistentId = source.reservedByPersistentId,
            destinationId = source.destinationId,
            sourceStorageDestinationId =
                source.sourceStorageDestinationId,
            hasDestinationPosition = source.hasDestinationPosition,
            destinationGridX = source.destinationGridX,
            destinationGridY = source.destinationGridY,
            forbidden = source.forbidden,
            sourceCharacterId = source.sourceCharacterId,
            sourceDisplayName = source.sourceDisplayName,
            sourceSpeciesTag = source.sourceSpeciesTag,
            sourceDeathReason = source.sourceDeathReason,
            emergencyButcheryAllowed = source.emergencyButcheryAllowed,
            wasteOrigin = source.wasteOrigin,
            contamination = source.contamination
        };
    }
}
