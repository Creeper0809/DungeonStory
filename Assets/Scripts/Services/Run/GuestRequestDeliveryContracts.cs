using System;
using System.Collections.Generic;
using UnityEngine;


public sealed class GuestRequestDeliveryItemProgressSnapshot
{
    public GuestRequestDeliveryItemProgressSnapshot(
        string itemId,
        string displayName,
        int required,
        int assigned,
        int hauling,
        int arrived,
        int delivered)
    {
        ItemId = itemId ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? ItemId
            : displayName;
        Required = Math.Max(0, required);
        Assigned = Math.Max(0, assigned);
        Hauling = Math.Max(0, hauling);
        Arrived = Math.Max(0, arrived);
        Delivered = Math.Max(0, delivered);
    }

    public string ItemId { get; }
    public string DisplayName { get; }
    public int Required { get; }
    public int Assigned { get; }
    public int Hauling { get; }
    public int Arrived { get; }
    public int Delivered { get; }
}

public sealed class GuestRequestDeliverySnapshot
{
    public string InstanceId { get; internal set; } = string.Empty;
    public string DefinitionId { get; internal set; } = string.Empty;
    public GuestRequestDeliveryPhase Phase { get; internal set; }
    public string VenueFacilityInstanceId { get; internal set; } = string.Empty;
    public string VenueDisplayName { get; internal set; } = string.Empty;
    public Vector2Int VenueAccessCell { get; internal set; }
    public string VenueCapacitySummary { get; internal set; } = string.Empty;
    public string DestinationId { get; internal set; } = string.Empty;
    public Vector2Int Destination { get; internal set; }
    public string OperationId { get; internal set; } = string.Empty;
    public string CommitId { get; internal set; } = string.Empty;
    public string RequestFingerprint { get; internal set; } = string.Empty;
    public string TerminalResolutionId { get; internal set; } = string.Empty;
    public string StatusReason { get; internal set; } = string.Empty;
    public IReadOnlyList<GuestRequestDeliveryItemProgressSnapshot> Items
        { get; internal set; } =
        Array.Empty<GuestRequestDeliveryItemProgressSnapshot>();
}

public interface IGuestRequestDeliveryQuery
{
    IReadOnlyList<GuestRequestDeliverySnapshot> ActiveDeliveries { get; }
    bool TryGetDelivery(
        string instanceId,
        out GuestRequestDeliverySnapshot snapshot);
}
