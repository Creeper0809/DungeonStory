using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

public sealed class WorldItemStackSnapshot
{
    public string StackId { get; set; }
    public string ItemId { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public StockCategory StockCategory { get; set; }
    public int Quantity { get; set; }
    public int UnitPrice { get; set; }
    public float UnitWeight { get; set; }
    public Sprite Sprite { get; set; }
    public WorldItemStackState State { get; set; }
    public Vector2Int Position { get; set; }
    public string ReservedByPersistentId { get; set; }
    public string DestinationId { get; set; }
    public string SourceStorageDestinationId { get; set; }
    public bool HasDestinationPosition { get; set; }
    public Vector2Int DestinationPosition { get; set; }
    public bool Forbidden { get; set; }
    public string SourceCharacterId { get; set; }
    public string SourceDisplayName { get; set; }
    public string SourceSpeciesTag { get; set; }
    public string SourceDeathReason { get; set; }
    public bool EmergencyButcheryAllowed { get; set; }
    public bool HasUniqueMetadata => !string.IsNullOrWhiteSpace(SourceCharacterId);
    public float TotalWeight => UnitWeight * Quantity;
    public int TotalValue => UnitPrice * Quantity;
    public bool IsReserved => !string.IsNullOrWhiteSpace(ReservedByPersistentId);
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
    public bool HasReservedItems => Stacks.Any(stack => stack.IsReserved);
}

public sealed class ItemPileInfoTarget : IInfoable
{
    public ItemPileInfoTarget(Vector2Int position)
    {
        Position = position;
    }

    public Vector2Int Position { get; }
}

public readonly struct WorldItemReservedStackQuantity
{
    public WorldItemReservedStackQuantity(
        string stackId,
        string itemId,
        int quantity,
        Vector2Int position,
        WorldItemHaulDestinationKind destinationKind,
        string destinationId)
    {
        StackId = stackId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
        Position = position;
        DestinationKind = destinationKind;
        DestinationId = destinationId ?? string.Empty;
    }

    public string StackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public Vector2Int Position { get; }
    public WorldItemHaulDestinationKind DestinationKind { get; }
    public string DestinationId { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(StackId) && Quantity > 0;
}

public readonly struct WorldItemHaulPlanLeg
{
    public WorldItemHaulPlanLeg(
        WorldItemReservedStackQuantity reservation,
        Vector2Int pickupStandPosition,
        IWarehouseFacility warehouse,
        Vector2Int deliveryPosition,
        Vector2Int dropPosition)
    {
        Reservation = reservation;
        PickupStandPosition = pickupStandPosition;
        Warehouse = warehouse;
        DeliveryPosition = deliveryPosition;
        DropPosition = dropPosition;
    }

    public WorldItemReservedStackQuantity Reservation { get; }
    public Vector2Int ItemPosition => Reservation.Position;
    public Vector2Int PickupStandPosition { get; }
    public IWarehouseFacility Warehouse { get; }
    public Vector2Int DeliveryPosition { get; }
    public Vector2Int DropPosition { get; }
    public WorldItemHaulDestinationKind DestinationKind => Reservation.DestinationKind;
    public string DestinationId => Reservation.DestinationId;
    public bool IsValid => Reservation.IsValid
        && (DestinationKind == WorldItemHaulDestinationKind.FacilityBuffer || Warehouse != null);
}

public sealed class WorldItemHaulPlan
{
    public WorldItemHaulPlan(
        IReadOnlyList<WorldItemHaulPlanLeg> pickupLegs,
        IReadOnlyList<WorldItemHaulPlanLeg> deliveryLegs,
        IReadOnlyList<WorldItemReservedStackQuantity> reservedStackQuantities,
        float totalWeight,
        int expectedDetourCost,
        WorldItemHaulDestinationKind primaryDestination,
        string primaryDestinationId)
    {
        PickupLegs = pickupLegs ?? Array.Empty<WorldItemHaulPlanLeg>();
        DeliveryLegs = deliveryLegs ?? Array.Empty<WorldItemHaulPlanLeg>();
        ReservedStackQuantities = reservedStackQuantities ?? Array.Empty<WorldItemReservedStackQuantity>();
        TotalWeight = Mathf.Max(0f, totalWeight);
        ExpectedDetourCost = Mathf.Max(0, expectedDetourCost);
        PrimaryDestination = primaryDestination;
        PrimaryDestinationId = primaryDestinationId ?? string.Empty;
    }

    public IReadOnlyList<WorldItemHaulPlanLeg> PickupLegs { get; }
    public IReadOnlyList<WorldItemHaulPlanLeg> DeliveryLegs { get; }
    public IReadOnlyList<WorldItemReservedStackQuantity> ReservedStackQuantities { get; }
    public float TotalWeight { get; }
    public int ExpectedDetourCost { get; }
    public WorldItemHaulDestinationKind PrimaryDestination { get; }
    public string PrimaryDestinationId { get; }
    public bool IsValid => PickupLegs.Count > 0 && DeliveryLegs.Count > 0 && ReservedStackQuantities.Count > 0;
    public string Summary => $"{ReservedStackQuantities.Count}스택 · {TotalWeight:0.#}kg";
}

public interface IHaulPlanBuilder
{
    bool TryReserveBestHaulPlan(CharacterActor actor, out WorldItemHaulPlan plan, out string failureReason);
    bool TryReserveStoredItemForDirectPickup(
        CharacterActor actor,
        string itemId,
        int quantity,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupStandPosition,
        out string failureReason);
}

public readonly struct WorldItemHaulJob
{
    public WorldItemHaulJob(
        string stackId,
        Vector2Int itemPosition,
        Vector2Int pickupStandPosition,
        IWarehouseFacility warehouse,
        Vector2Int deliveryPosition,
        WorldItemHaulDestinationKind destinationKind = WorldItemHaulDestinationKind.Warehouse,
        string destinationId = "",
        Vector2Int dropPosition = default,
        bool useDropPosition = false)
    {
        StackId = stackId ?? string.Empty;
        ItemPosition = itemPosition;
        PickupStandPosition = pickupStandPosition;
        Warehouse = warehouse;
        DeliveryPosition = deliveryPosition;
        DestinationKind = destinationKind;
        DestinationId = destinationId ?? string.Empty;
        DropPosition = useDropPosition ? dropPosition : deliveryPosition;
    }

    public string StackId { get; }
    public Vector2Int ItemPosition { get; }
    public Vector2Int PickupStandPosition { get; }
    public IWarehouseFacility Warehouse { get; }
    public Vector2Int DeliveryPosition { get; }
    public Vector2Int DropPosition { get; }
    public WorldItemHaulDestinationKind DestinationKind { get; }
    public string DestinationId { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(StackId)
        && (DestinationKind == WorldItemHaulDestinationKind.FacilityBuffer || Warehouse != null);
}

public interface IWorldItemStackRuntime
{
    IDungeonItemCatalogProvider CatalogProvider { get; }
    IItemHaulingSettingsProvider HaulingSettingsProvider { get; }
    bool StoredItemMarkersVisible { get; }
    int ItemStackVersion { get; }
    int HaulJobVersion { get; }
    DungeonPhysicalItemSaveData Capture();
    void Restore(DungeonPhysicalItemSaveData snapshot);
    void SetStoredItemMarkersVisible(bool visible);
    bool SpawnStockAtDropoff(StockCategory category, int amount, string sourceLabel, out int spawned);
    bool SpawnStockAtDropoff(
        StockCategory category,
        int amount,
        string sourceLabel,
        WorldItemStackState state,
        string destinationId,
        out int spawned);
    bool SpawnItemAt(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned);
    bool SpawnUniqueItemAt(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId);
    bool SpawnHumanoidCorpse(
        CharacterActor source,
        Vector2Int position,
        string deathReason,
        out string stackId);
    bool TryRequestFacilityDelivery(
        StockCategory category,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
    bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
    bool TryGetPileAt(Vector2Int position, out WorldItemPileSnapshot pile);
    bool TryGetPileTargetAt(
        Vector2Int position,
        out ItemPileInfoTarget target,
        out UnityEngine.Object markerObject);
    IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(Vector2Int position, bool includeStored = false);
    IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();
    bool HasAvailableHaulJob(CharacterActor actor);
    bool TryReserveBestHaulPlan(CharacterActor actor, out WorldItemHaulPlan plan, out string failureReason);
    bool TryReserveStoredItemForDirectPickup(
        CharacterActor actor,
        string itemId,
        int quantity,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupStandPosition,
        out string failureReason);
    bool TryReserveBestHaulJob(CharacterActor actor, out WorldItemHaulJob job, out string failureReason);
    bool TryPickupReservedStackQuantity(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemReservedStackQuantity reservation,
        out int pickedUp,
        out string failureReason);
    bool TryPickupReservedStack(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemHaulJob job,
        out string failureReason);
    bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        out string failureReason);
    bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        out string failureReason);
    bool TryConsumeFacilityBuffer(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs,
        out string failureReason);
    bool TryStealLooseItem(
        CharacterActor actor,
        int searchRadius,
        out WorldItemStackSnapshot stolenItem,
        out string failureReason);
    void ReleaseReservation(string stackId, string persistentId);
    bool TryClearReservation(string stackId);
    bool SetForbidden(string stackId, bool forbidden);
    bool PrioritizeHaul(string stackId);
    bool DeleteStack(string stackId);
    bool TryConsumeStackQuantity(string stackId, int quantity, out WorldItemStackSnapshot consumed);
    bool SetEmergencyButcheryAllowed(string stackId, bool allowed);
    int RemoveStacksByStateAndDestination(WorldItemStackState state, string destinationId);
    int ReleaseStacksByDestination(string destinationId, Vector2Int releasePosition);
}

internal sealed class WorldItemStackRecord
{
    public string stackId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public WorldItemStackState state;
    public Vector2Int position;
    public string reservedByPersistentId = string.Empty;
    public string destinationId = string.Empty;
    public string sourceStorageDestinationId = string.Empty;
    public bool hasDestinationPosition;
    public Vector2Int destinationPosition;
    public bool forbidden;
    public string sourceCharacterId = string.Empty;
    public string sourceDisplayName = string.Empty;
    public string sourceSpeciesTag = string.Empty;
    public string sourceDeathReason = string.Empty;
    public bool emergencyButcheryAllowed;
}
