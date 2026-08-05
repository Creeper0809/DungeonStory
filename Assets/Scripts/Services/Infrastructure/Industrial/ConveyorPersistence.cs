using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class ConveyorRestoreState
{
    internal int NextPayloadSequence { get; set; }
    internal IReadOnlyDictionary<string, ConveyorNodeRuntimeState> Nodes { get; set; } =
        new Dictionary<string, ConveyorNodeRuntimeState>();
    internal IReadOnlyDictionary<string, ConveyorPayloadRuntimeState> Payloads { get; set; } =
        new Dictionary<string, ConveyorPayloadRuntimeState>();
}

internal interface IConveyorRestoreProjection
{
    void EnsureTopology();
    void ResetAfterRestore();
}

internal sealed class ConveyorPersistenceAdapter :
    IConveyorInfrastructurePersistence
{
    private readonly IConveyorRestoreProjection projection;
    private readonly ConveyorItemGateway items;
    private readonly IGameClock clock;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    public ConveyorPersistenceAdapter(
        IConveyorRestoreProjection projection,
        ConveyorItemGateway items,
        IGameClock clock,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.projection = projection
            ?? throw new ArgumentNullException(nameof(projection));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public DungeonConveyorInfrastructureSaveData Capture()
    {
        projection.EnsureTopology();
        ConveyorAggregateState state = aggregateRootStore.GetOrCreateWritable(
            () => new ConveyorAggregateState(),
            source => source.DeepClone());
        return ConveyorPersistence.Capture(
            state.Nodes,
            state.Payloads,
            state.NextPayloadSequence,
            clock.Time);
    }

    public ConveyorRestoreState PrepareRestore(
        DungeonConveyorInfrastructureSaveData snapshot)
    {
        IndustrialInfrastructureSaveValidation.RequireValid(snapshot);
        return ConveyorPersistence.Restore(snapshot, clock.Time);
    }

    public void Restore(ConveyorRestoreState restored)
    {
        if (restored == null)
        {
            throw new ArgumentNullException(nameof(restored));
        }

        foreach (ConveyorPayloadRuntimeState payload in restored.Payloads.Values)
        {
            if (!items.TryGetTransit(
                    payload.StackId,
                    payload.PayloadId,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Conveyor payload '{payload.PayloadId}' does not own "
                    + $"physical stack '{payload.StackId.Value}'.");
            }
        }

        ConveyorAggregateState replacement = new ConveyorAggregateState
        {
            NextPayloadSequence = restored.NextPayloadSequence,
            Version = 1
        };
        foreach (KeyValuePair<string, ConveyorNodeRuntimeState> pair in
                 restored.Nodes)
        {
            replacement.Nodes.Add(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, ConveyorPayloadRuntimeState> pair in
                 restored.Payloads)
        {
            replacement.Payloads.Add(pair.Key, pair.Value);
        }

        aggregateRootStore.Replace(replacement);
        if (!aggregateRootStore.IsRestoreStaging)
        {
            projection.ResetAfterRestore();
            projection.EnsureTopology();
        }
    }
}

internal static class ConveyorPersistence
{
    public static DungeonConveyorInfrastructureSaveData Capture(
        IReadOnlyDictionary<string, ConveyorNodeRuntimeState> nodeStates,
        IReadOnlyDictionary<string, ConveyorPayloadRuntimeState> payloads,
        int nextPayloadSequence,
        float currentTime)
    {
        return new DungeonConveyorInfrastructureSaveData
        {
            nextPayloadSequence = nextPayloadSequence,
            nodes = nodeStates
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ConveyorNodeSaveData
                {
                    buildingInstanceId = pair.Key,
                    enabled = pair.Value.Enabled,
                    destinationId = pair.Value.DestinationId,
                    overflowPolicy = pair.Value.OverflowPolicy,
                    reserveWarehouseId = pair.Value.ReserveWarehouseId ?? string.Empty,
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
                        minimumFreshness01 = pair.Value.MinimumFreshness01,
                        maximumFreshness01 = pair.Value.MaximumFreshness01,
                        allowContaminated = pair.Value.AllowContaminated
                    }
                })
                .ToList(),
            payloads = payloads.Values
                .OrderBy(payload => payload.PayloadId, StringComparer.Ordinal)
                .Select(payload => new ConveyorPayloadSaveData
                {
                    payloadId = payload.PayloadId,
                    itemStackId = payload.StackId.Value,
                    segmentBuildingInstanceId = payload.SegmentNodeId,
                    previousBuildingInstanceId = payload.PreviousNodeId,
                    destinationId = payload.DestinationId,
                    progress = payload.Progress,
                    lastMovedAt = Mathf.Max(0f, currentTime - payload.LastMovedAt),
                    stalledSince = payload.StalledSince > 0f
                        ? Mathf.Max(0f, currentTime - payload.StalledSince)
                        : 0f,
                    routeVersion = payload.RouteVersion,
                    stallReason = payload.StallReason
                })
                .ToList()
        };
    }

    public static ConveyorRestoreState Restore(
        DungeonConveyorInfrastructureSaveData snapshot,
        float currentTime)
    {
        Dictionary<string, ConveyorNodeRuntimeState> nodes =
            new(StringComparer.Ordinal);
        foreach (ConveyorNodeSaveData saved in snapshot?.nodes
                 ?? new List<ConveyorNodeSaveData>())
        {
            if (saved == null
                || !new BuildingInstanceId(saved.buildingInstanceId).IsValid)
            {
                continue;
            }

            ConveyorNodeRuntimeState state = new()
            {
                Enabled = saved.enabled,
                DestinationId = saved.destinationId?.Trim() ?? string.Empty,
                OverflowPolicy = Enum.IsDefined(
                    typeof(ConveyorOverflowPolicy),
                    saved.overflowPolicy)
                        ? saved.overflowPolicy
                        : ConveyorOverflowPolicy.ReserveWarehouseThenLoose,
                ReserveWarehouseId = saved.reserveWarehouseId?.Trim() ?? string.Empty,
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
                FilterFreshness = saved.filter?.filterFreshness ?? false,
                MinimumFreshness01 = Mathf.Clamp01(
                    saved.filter?.minimumFreshness01 ?? 0f),
                MaximumFreshness01 = Mathf.Clamp01(
                    saved.filter?.maximumFreshness01 ?? 1f),
                AllowContaminated = saved.filter?.allowContaminated ?? true
            };
            AddStrings(state.ItemIds, saved.filter?.itemIds);
            AddCategories(state.StockCategories, saved.filter?.stockCategories);
            AddStrings(state.MaterialIds, saved.filter?.materialIds);
            NormalizeRanges(state);
            nodes[saved.buildingInstanceId.Trim()] = state;
        }

        Dictionary<string, ConveyorPayloadRuntimeState> restoredPayloads =
            new(StringComparer.Ordinal);
        foreach (ConveyorPayloadSaveData saved in snapshot?.payloads
                 ?? new List<ConveyorPayloadSaveData>())
        {
            if (saved == null
                || string.IsNullOrWhiteSpace(saved.payloadId)
                || !new ItemStackId(saved.itemStackId).IsValid
                || !new BuildingInstanceId(
                    saved.segmentBuildingInstanceId).IsValid)
            {
                continue;
            }

            string payloadId = saved.payloadId.Trim();
            restoredPayloads[payloadId] = new ConveyorPayloadRuntimeState
            {
                PayloadId = payloadId,
                StackId = new ItemStackId(saved.itemStackId),
                SegmentNodeId = saved.segmentBuildingInstanceId.Trim(),
                PreviousNodeId = saved.previousBuildingInstanceId?.Trim()
                    ?? string.Empty,
                DestinationId = saved.destinationId?.Trim() ?? string.Empty,
                Progress = Mathf.Clamp01(saved.progress),
                LastMovedAt = currentTime - Mathf.Max(0f, saved.lastMovedAt),
                StalledSince = saved.stalledSince > 0f
                    ? currentTime - saved.stalledSince
                    : 0f,
                RouteVersion = 0,
                StallReason = saved.stallReason
            };
        }

        return new ConveyorRestoreState
        {
            NextPayloadSequence = Mathf.Max(
                1,
                snapshot?.nextPayloadSequence ?? 1),
            Nodes = nodes,
            Payloads = restoredPayloads
        };
    }

    private static void AddStrings(
        ISet<string> destination,
        IEnumerable<string> values)
    {
        foreach (string value in values ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                destination.Add(value.Trim());
            }
        }
    }

    private static void AddCategories(
        ISet<StockCategory> destination,
        IEnumerable<int> values)
    {
        foreach (int value in values ?? Array.Empty<int>())
        {
            if (Enum.IsDefined(typeof(StockCategory), value))
            {
                destination.Add((StockCategory)value);
            }
        }
    }

    private static void NormalizeRanges(ConveyorNodeRuntimeState state)
    {
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
    }

    private static CombatEquipmentQuality ParseQuality(
        int value,
        CombatEquipmentQuality fallback)
    {
        return Enum.IsDefined(typeof(CombatEquipmentQuality), value)
            ? (CombatEquipmentQuality)value
            : fallback;
    }

}
