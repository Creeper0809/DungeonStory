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
        string destinationId,
        string leaseId = "",
        string ownerOperationId = "")
    {
        StackId = stackId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
        Position = position;
        DestinationKind = destinationKind;
        DestinationId = destinationId ?? string.Empty;
        LeaseId = leaseId?.Trim() ?? string.Empty;
        OwnerOperationId = ownerOperationId?.Trim() ?? string.Empty;
    }

    public string StackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public Vector2Int Position { get; }
    public WorldItemHaulDestinationKind DestinationKind { get; }
    public string DestinationId { get; }
    public string LeaseId { get; }
    public string OwnerOperationId { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(StackId)
        && Quantity > 0
        && (!string.IsNullOrWhiteSpace(LeaseId)
            || string.IsNullOrWhiteSpace(OwnerOperationId));
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
        bool useDropPosition = false,
        int quantity = 0,
        string leaseId = "",
        string ownerOperationId = "")
    {
        StackId = stackId ?? string.Empty;
        ItemPosition = itemPosition;
        PickupStandPosition = pickupStandPosition;
        Warehouse = warehouse;
        DeliveryPosition = deliveryPosition;
        DestinationKind = destinationKind;
        DestinationId = destinationId ?? string.Empty;
        DropPosition = useDropPosition ? dropPosition : deliveryPosition;
        Quantity = Mathf.Max(0, quantity);
        LeaseId = leaseId?.Trim() ?? string.Empty;
        OwnerOperationId = ownerOperationId?.Trim() ?? string.Empty;
    }

    public string StackId { get; }
    public Vector2Int ItemPosition { get; }
    public Vector2Int PickupStandPosition { get; }
    public IWarehouseFacility Warehouse { get; }
    public Vector2Int DeliveryPosition { get; }
    public Vector2Int DropPosition { get; }
    public WorldItemHaulDestinationKind DestinationKind { get; }
    public string DestinationId { get; }
    public int Quantity { get; }
    public string LeaseId { get; }
    public string OwnerOperationId { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(StackId)
        && (DestinationKind == WorldItemHaulDestinationKind.FacilityBuffer || Warehouse != null);
}

public interface IEquipmentPhysicalItemGateway
{
    bool SpawnItemAt(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned);
    bool SpawnExistingUniqueItemAt(
        string itemId,
        ItemInstanceId itemInstanceId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId);
    bool TryAbsorbUniqueItemStack(
        string stackId,
        ItemInstanceId expectedInstanceId);
    bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
    IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();
    bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason);
    bool DeleteStack(string stackId);
    bool TryConsumeStackQuantity(
        string stackId,
        int quantity,
        out WorldItemStackSnapshot consumed);
    bool TrySetInstanceComponent(
        string stackId,
        ItemInstanceComponentSaveData component);
    int ReleaseStacksByDestination(
        string destinationId,
        Vector2Int releasePosition);
}

public sealed class UnavailableEquipmentPhysicalItemGateway :
    IEquipmentPhysicalItemGateway
{
    public static readonly UnavailableEquipmentPhysicalItemGateway Instance = new();

    private UnavailableEquipmentPhysicalItemGateway()
    {
    }

    public bool SpawnItemAt(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned)
    {
        spawned = 0;
        return false;
    }

    public bool SpawnExistingUniqueItemAt(
        string itemId,
        ItemInstanceId itemInstanceId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId)
    {
        stackId = string.Empty;
        return false;
    }

    public bool TryAbsorbUniqueItemStack(
        string stackId,
        ItemInstanceId expectedInstanceId) => false;

    public bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        requested = 0;
        failureReason = "physical item capability unavailable";
        return false;
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
        Array.Empty<WorldItemStackSnapshot>();

    public bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason)
    {
        failureReason = "physical item capability unavailable";
        return false;
    }

    public bool DeleteStack(string stackId) => false;

    public bool TryConsumeStackQuantity(
        string stackId,
        int quantity,
        out WorldItemStackSnapshot consumed)
    {
        consumed = null;
        return false;
    }

    public bool TrySetInstanceComponent(
        string stackId,
        ItemInstanceComponentSaveData component) => false;

    public int ReleaseStacksByDestination(
        string destinationId,
        Vector2Int releasePosition) => 0;
}

public interface IWorldItemStackRuntime : IEquipmentPhysicalItemGateway
{
    IDungeonItemCatalogProvider CatalogProvider { get; }
    IItemHaulingSettingsProvider HaulingSettingsProvider { get; }
    bool StoredItemMarkersVisible { get; }
    int ItemStackVersion { get; }
    int HaulJobVersion { get; }
    DungeonPhysicalItemSaveData Capture();
    void Restore(DungeonPhysicalItemSaveData snapshot);
    void SetStoredItemMarkersVisible(bool visible);
    bool SpawnItemAtDropoff(string itemId, int amount, string sourceLabel, out int spawned);
    bool SpawnStockAtDropoff(StockCategory category, int amount, string sourceLabel, out int spawned);
    bool SpawnStockAtDropoff(
        StockCategory category,
        int amount,
        string sourceLabel,
        WorldItemStackState state,
        string destinationId,
        out int spawned);
    bool SpawnStockInWarehouse(
        IWarehouseFacility warehouse,
        StockCategory category,
        int amount,
        out int spawned);
    new bool SpawnItemAt(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned);
    bool SpawnWasteAt(
        string itemId,
        int amount,
        Vector2Int position,
        WasteOriginKind origin,
        float contamination,
        out int spawned);
    bool SpawnUniqueItemAt(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId);
    bool SpawnUniqueItemAt(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        Vector2Int destinationPosition,
        out string stackId);
    new bool SpawnExistingUniqueItemAt(
        string itemId,
        ItemInstanceId itemInstanceId,
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
    new bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
    bool TryRequestStackDelivery(
        string stackId,
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
    new IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();
    bool TryFindNearestAvailableStock(
        Vector2Int origin,
        StockCategory category,
        bool preferStored,
        out WorldItemStackSnapshot stack);
    void CopyAvailableStockCandidates(
        StockCategory category,
        List<WorldItemStockCandidate> destination);
    bool TryFindBestAvailableStack(
        Vector2Int origin,
        Func<string, int> rankSelector,
        out WorldItemStackSnapshot stack);
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
    new bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
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
    bool TryRouteStackToDestination(
        string stackId,
        WorldItemStackState state,
        string destinationId,
        Vector2Int destinationPosition,
        out string failureReason);
    new bool DeleteStack(string stackId);
    new bool TrySetInstanceComponent(
        string stackId,
        ItemInstanceComponentSaveData component);
    bool SetEmergencyButcheryAllowed(string stackId, bool allowed);
    int RemoveStacksByStateAndDestination(WorldItemStackState state, string destinationId);
    new int ReleaseStacksByDestination(string destinationId, Vector2Int releasePosition);
}

public interface IWorldItemQuantityLeaseRuntime
{
    bool TryRenewQuantityLease(
        string leaseId,
        double requestedUntilGameSeconds,
        out string failureReason);
}

internal sealed class WorldItemStackRecord
{
    public string stackId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public WorldItemStackState state;
    public Vector2Int position;
    public string reservedByPersistentId = string.Empty;
    public int reservedQuantity;
    public long reservationRevision;
    public string destinationId = string.Empty;
    public string aggregationCohortId = string.Empty;
    public string sourceStorageDestinationId = string.Empty;
    public bool hasDestinationPosition;
    public Vector2Int destinationPosition;
    public bool forbidden;
    public string sourceCharacterId = string.Empty;
    public string sourceDisplayName = string.Empty;
    public string sourceSpeciesTag = string.Empty;
    public string sourceDeathReason = string.Empty;
    public bool emergencyButcheryAllowed;
    public WasteOriginKind wasteOrigin;
    public float contamination;
    public List<ItemInstanceComponentSaveData> components = new();
}
