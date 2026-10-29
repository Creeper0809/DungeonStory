using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class WorldItemStackSnapshot
{
    public string StackId { get; set; }
    public long ContentRevision { get; set; }
    public long ReservationRevision { get; set; }
    public string ItemInstanceId { get; set; }
    public string ItemId { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public StockCategory StockCategory { get; set; }
    public int Quantity { get; set; }
    public int TotalQuantity => Mathf.Max(0, Quantity);
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity => Mathf.Max(0, TotalQuantity - ReservedQuantity);
    public int UnitPrice { get; set; }
    public float UnitWeight { get; set; }
    public Sprite Sprite { get; set; }
    public WorldItemStackState State { get; set; }
    public Vector2Int Position { get; set; }
    public string ReservedByPersistentId { get; set; }
    public string DestinationId { get; set; }
    public string AggregationCohortId { get; set; }
    public string SourceStorageDestinationId { get; set; }
    public bool HasDestinationPosition { get; set; }
    public Vector2Int DestinationPosition { get; set; }
    public bool Forbidden { get; set; }
    public string SourceCharacterId { get; set; }
    public string SourceDisplayName { get; set; }
    public string SourceSpeciesTag { get; set; }
    public string SourceDeathReason { get; set; }
    public bool EmergencyButcheryAllowed { get; set; }
    public WasteOriginKind WasteOrigin { get; set; }
    public float Contamination { get; set; }
    public IReadOnlyList<ItemInstanceComponentSaveData> Components { get; set; } =
        Array.Empty<ItemInstanceComponentSaveData>();
    public WorldItemDropDisposition DropDisposition { get; set; }
    public string RecoveryOwnerOperationId { get; set; }
    public string RecoverySourceStackId { get; set; }
    public string RecoveryCarrierPersistentId { get; set; }
    public WorldItemCarryInterruptionKind RecoveryInterruptionKind { get; set; }
    public double DroppedAtGameTime { get; set; }
    public double RecoveryDeadlineGameTime { get; set; }
    public bool IsTransientCarryRecoveryDrop =>
        DropDisposition == WorldItemDropDisposition.TransientCarryRecoveryDrop;
    public string StackSignature => ItemStackSignature.Create(ItemId, Components);
    public string ReservationSignature =>
        ItemReservationSignature.Create(ItemId, Components);
    public bool IsWaste => WasteOrigin != WasteOriginKind.Unknown;
    public bool HasUniqueMetadata => !string.IsNullOrWhiteSpace(SourceCharacterId);
    public float TotalWeight => UnitWeight * Quantity;
    public int TotalValue => UnitPrice * Quantity;
    public bool HasReservations => ReservedQuantity > 0;
    public bool IsFullyReserved => TotalQuantity > 0 && AvailableQuantity == 0;
    [Obsolete("Use ReservedQuantity or AvailableQuantity.")]
    public bool IsReserved => HasReservations;

    public WorldItemStackSnapshot Clone()
    {
        WorldItemStackSnapshot clone =
            (WorldItemStackSnapshot)MemberwiseClone();
        clone.Components = (Components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .Select(component => component.Clone())
            .ToArray();
        return clone;
    }
}

public sealed class WorldItemPileSnapshot
{
    public Vector2Int Position { get; set; }
    public IReadOnlyList<WorldItemStackSnapshot> Stacks { get; set; } =
        Array.Empty<WorldItemStackSnapshot>();
    public WorldItemStackSnapshot Representative { get; set; }
    public int TotalQuantity => Stacks.Sum(stack => stack.Quantity);
    public int KindCount => Stacks.Select(stack => stack.ItemId).Distinct(StringComparer.Ordinal).Count();
    public float TotalWeight => Stacks.Sum(stack => stack.TotalWeight);
    public bool HasReservedItems => Stacks.Any(stack => stack.HasReservations);
}

public readonly struct WorldItemStockCandidate
{
    public WorldItemStockCandidate(
        string stackId,
        Vector2Int position,
        WorldItemStackState state,
        int quantity)
    {
        StackId = stackId ?? string.Empty;
        Position = position;
        State = state;
        Quantity = Mathf.Max(0, quantity);
    }

    public string StackId { get; }
    public Vector2Int Position { get; }
    public WorldItemStackState State { get; }
    public int Quantity { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(StackId) && Quantity > 0;
}
