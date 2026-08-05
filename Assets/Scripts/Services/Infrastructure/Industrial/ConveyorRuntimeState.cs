using System;
using System.Collections.Generic;

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
    public CombatEquipmentQuality MinimumQuality = CombatEquipmentQuality.Awful;
    public CombatEquipmentQuality MaximumQuality = CombatEquipmentQuality.Legendary;
    public bool FilterFreshness;
    public float MinimumFreshness01;
    public float MaximumFreshness01 = 1f;
    public bool AllowContaminated = true;
    public string DestinationId = string.Empty;

    public ConveyorNodeRuntimeState DeepClone()
    {
        ConveyorNodeRuntimeState clone = new ConveyorNodeRuntimeState
        {
            Enabled = Enabled,
            OverflowPolicy = OverflowPolicy,
            ReserveWarehouseId = ReserveWarehouseId,
            AllowForbidden = AllowForbidden,
            FilterQuality = FilterQuality,
            MinimumQuality = MinimumQuality,
            MaximumQuality = MaximumQuality,
            FilterFreshness = FilterFreshness,
            MinimumFreshness01 = MinimumFreshness01,
            MaximumFreshness01 = MaximumFreshness01,
            AllowContaminated = AllowContaminated,
            DestinationId = DestinationId
        };
        clone.ItemIds.UnionWith(ItemIds);
        clone.StockCategories.UnionWith(StockCategories);
        clone.MaterialIds.UnionWith(MaterialIds);
        return clone;
    }
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
    public ItemStackId StackId;
    public IReadOnlyList<string> Route = Array.Empty<string>();
    public int RouteIndex;
    public float NextRouteRetryAt;

    public ConveyorPayloadRuntimeState DeepClone()
    {
        return new ConveyorPayloadRuntimeState
        {
            PayloadId = PayloadId,
            SegmentNodeId = SegmentNodeId,
            PreviousNodeId = PreviousNodeId,
            DestinationId = DestinationId,
            Progress = Progress,
            LastMovedAt = LastMovedAt,
            StalledSince = StalledSince,
            RouteVersion = RouteVersion,
            StallReason = StallReason,
            StackId = StackId,
            Route = Route == null ? Array.Empty<string>() : new List<string>(Route),
            RouteIndex = RouteIndex,
            NextRouteRetryAt = NextRouteRetryAt
        };
    }
}
