#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class EditorWarehouseStockRuntime : IWorldItemStackRuntime
{
    public bool SpawnItemAtWithComponents(string itemId, int amount, Vector2Int position, WorldItemStackState state, string destinationId, IReadOnlyList<ItemInstanceComponentSaveData> components, out int spawned) { spawned = 0; return false; }
    public bool TryRemoveInstanceComponent(string stackId, string componentTypeId) => false;
    public IDungeonItemCatalogProvider CatalogProvider => null;
    public IPhysicalItemMassQuery MassQuery => null;
    public IItemHaulingSettingsProvider HaulingSettingsProvider => null;
    public bool StoredItemMarkersVisible => false;
    public int ItemStackVersion => 0;
    public int HaulJobVersion => 0;
    public int GetCommittedHaulDeliveryQuantity(
        string destinationId,
        string itemId) => 0;
    public long GetCommittedHaulDeliveryMassGrams(string destinationId) => 0L;
    public bool TryCommitHaulPickup(
        string ownerOperationId,
        CharacterCarryInventory inventory,
        out string failureReason)
    {
        failureReason = "editor warehouse haul delivery authority unavailable";
        return false;
    }
    public bool TryCaptureHaulDeliveryIntent(
        string ownerOperationId,
        out HaulDeliveryIntentSaveData intent)
    {
        intent = null;
        return false;
    }
    public bool ReleaseHaulDeliveryIntent(string ownerOperationId) => false;

    public bool TryCommitBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public IReadOnlyList<HaulDeliveryIntentSaveData>
        CaptureHaulDeliveryIntentsByDestination(string destinationId) =>
        Array.Empty<HaulDeliveryIntentSaveData>();

    public bool AcknowledgeBatchPhysicalDisposition(
        string commitId,
        out string failureReason)
    {
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryCommitPendingBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryGetPendingBatchPhysicalDisposition(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt)
    {
        receipt = default;
        return false;
    }

    public DungeonPhysicalItemSaveData Capture() => new DungeonPhysicalItemSaveData();
    public void Restore(DungeonPhysicalItemSaveData snapshot) { }
    public void SetStoredItemMarkersVisible(bool visible) { }

    public bool SpawnItemAtDropoff(
        string itemId,
        int amount,
        string sourceLabel,
        out int spawned)
    {
        spawned = 0;
        return false;
    }

    public bool SpawnStockInWarehouse(
        IWarehouseFacility warehouse,
        StockCategory category,
        int amount,
        out int spawned)
    {
        spawned = warehouse?.Inventory?.SeedPhysicalStockForTest(category, amount) ?? 0;
        return spawned > 0;
    }

    public bool SpawnStockAtDropoff(
        StockCategory category,
        int amount,
        string sourceLabel,
        out int spawned)
    {
        spawned = 0;
        return false;
    }

    public bool SpawnStockAtDropoff(
        StockCategory category,
        int amount,
        string sourceLabel,
        WorldItemStackState state,
        string destinationId,
        out int spawned)
    {
        spawned = 0;
        return false;
    }

    public bool SpawnItemAt(string itemId, int amount, Vector2Int position,
        WorldItemStackState state, string destinationId, out int spawned)
    {
        spawned = 0;
        return false;
    }

    public bool SpawnWasteAt(string itemId, int amount, Vector2Int position,
        WasteOriginKind origin, float contamination, out int spawned)
    {
        spawned = 0;
        return false;
    }

    public bool SpawnUniqueItemAt(string itemId, Vector2Int position,
        WorldItemStackState state, string destinationId, out string stackId)
    {
        stackId = string.Empty;
        return false;
    }

    public bool SpawnUniqueItemAt(string itemId, Vector2Int position,
        WorldItemStackState state, string destinationId,
        Vector2Int destinationPosition, out string stackId)
    {
        stackId = string.Empty;
        return false;
    }

    public bool SpawnExistingUniqueItemAt(string itemId,
        ItemInstanceId itemInstanceId, Vector2Int position,
        WorldItemStackState state, string destinationId, out string stackId)
    {
        stackId = string.Empty;
        return false;
    }

    public bool TryAbsorbUniqueItemStack(
        string stackId,
        ItemInstanceId expectedInstanceId) => false;

    public bool SpawnHumanoidCorpse(CharacterActor source, Vector2Int position,
        string deathReason, out string stackId)
    {
        stackId = string.Empty;
        return false;
    }

    public bool TryRequestFacilityDelivery(StockCategory category, int amount,
        Vector2Int destinationPosition, string destinationId,
        out int requested, out string failureReason)
    {
        requested = 0;
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryRequestItemDelivery(string itemId, int amount,
        Vector2Int destinationPosition, string destinationId,
        out int requested, out string failureReason)
    {
        requested = 0;
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryRequestStackDelivery(string stackId, int amount,
        Vector2Int destinationPosition, string destinationId,
        out int requested, out string failureReason)
    {
        requested = 0;
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryGetPileAt(Vector2Int position, out WorldItemPileSnapshot pile)
    {
        pile = null;
        return false;
    }

    public bool TryGetPileTargetAt(Vector2Int position,
        out ItemPileInfoTarget target, out UnityEngine.Object markerObject)
    {
        target = null;
        markerObject = null;
        return false;
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(
        Vector2Int position, bool includeStored = false) =>
        Array.Empty<WorldItemStackSnapshot>();
    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
        Array.Empty<WorldItemStackSnapshot>();

    public bool TryFindNearestAvailableStock(Vector2Int origin,
        StockCategory category, bool preferStored, out WorldItemStackSnapshot stack)
    {
        stack = null;
        return false;
    }

    public void CopyAvailableStockCandidates(StockCategory category,
        List<WorldItemStockCandidate> destination) => destination?.Clear();

    public bool TryFindBestAvailableStack(Vector2Int origin,
        Func<string, int> rankSelector, out WorldItemStackSnapshot stack)
    {
        stack = null;
        return false;
    }

    public bool HasAvailableHaulJob(CharacterActor actor) => false;

    public bool TryReserveBestHaulPlan(CharacterActor actor,
        out WorldItemHaulPlan plan, out string failureReason)
    {
        plan = null;
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryReserveStoredItemForDirectPickup(CharacterActor actor,
        string itemId, int quantity,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupStandPosition, out string failureReason)
    {
        reservation = default;
        pickupStandPosition = default;
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryReserveBestHaulJob(CharacterActor actor,
        out WorldItemHaulJob job, out string failureReason)
    {
        job = default;
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryPickupReservedStackQuantity(CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemReservedStackQuantity reservation,
        out int pickedUp, out string failureReason)
    {
        pickedUp = 0;
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryPickupReservedStack(CharacterActor actor,
        CharacterCarryInventory inventory, WorldItemHaulJob job,
        out string failureReason)
    {
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryDepositCarriedItems(CharacterActor actor,
        CharacterCarryInventory inventory, IWarehouseFacility warehouse,
        out string failureReason)
    {
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason) =>
        TryDepositCarriedItems(actor, inventory, warehouse, out failureReason);

    public bool TryDepositCarriedItemsToFacility(CharacterActor actor,
        CharacterCarryInventory inventory, Vector2Int destinationPosition,
        string destinationId, out string failureReason)
    {
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason) =>
        TryDepositCarriedItemsToFacility(
            actor,
            inventory,
            destinationPosition,
            destinationId,
            out failureReason);

    public bool TryConsumeFacilityBuffer(string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs, out string failureReason)
    {
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryConsumeFacilityItemBuffer(string destinationId,
        IReadOnlyDictionary<string, int> costs, out string failureReason)
    {
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool TryStealLooseItem(CharacterActor actor, int searchRadius,
        out WorldItemStackSnapshot stolenItem, out string failureReason)
    {
        stolenItem = null;
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public void ReleaseReservation(string stackId, string persistentId) { }
    public bool TryClearReservation(string stackId) => false;
    public bool SetForbidden(string stackId, bool forbidden) => false;
    public bool PrioritizeHaul(string stackId) => false;

    public bool TryRouteStackToDestination(string stackId,
        WorldItemStackState state, string destinationId,
        Vector2Int destinationPosition, out string failureReason)
    {
        failureReason = "not supported by warehouse fixture";
        return false;
    }

    public bool DeleteStack(string stackId) => false;

    public bool TryConsumeStackQuantity(string stackId, int quantity,
        out WorldItemStackSnapshot consumed)
    {
        consumed = null;
        return false;
    }

    public bool TrySetInstanceComponent(string stackId,
        ItemInstanceComponentSaveData component) => false;
    public bool SetEmergencyButcheryAllowed(string stackId, bool allowed) => false;
    public int RemoveStacksByStateAndDestination(
        WorldItemStackState state, string destinationId) => 0;
    public int ReleaseStacksByDestination(
        string destinationId, Vector2Int releasePosition) => 0;
}
#endif
