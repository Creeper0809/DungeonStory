using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IItemTransferService
{
    bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure);

    bool TryRequestStackDelivery(
        ItemStackId stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure);

    bool TryConsumeStackQuantity(
        ItemStackId stackId,
        int quantity,
        out WorldItemStackSnapshot consumed,
        out DomainFailure failure);

    bool TrySpawnItem(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned);

    bool TrySpawnItemWithComponents(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int spawned);

    bool TryRouteFacilityOutput(
        string sourceDestinationId,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int routed,
        out DomainFailure failure);

    void PrioritizeDestination(string destinationId);
    int ReleaseDestination(string destinationId, Vector2Int releasePosition);
    int RemoveDestination(
        string destinationId,
        params WorldItemStackState[] states);

    bool TryInspectStackForTransit(
        ItemStackId stackId,
        out ItemTransitStackSnapshot stack);

    void CopyLoadableTransitStackIds(
        Vector2Int position,
        List<ItemStackId> destination);

    bool TryBeginTransit(
        ItemStackId stackId,
        Vector2Int expectedPosition,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack,
        out DomainFailure failure);

    bool TryGetTransitStack(
        ItemStackId stackId,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack);

    bool TryCompleteTransit(
        ItemStackId stackId,
        string transitOwnerId,
        WorldItemStackState destinationState,
        Vector2Int destinationPosition,
        string destinationId,
        out DomainFailure failure);

    bool TryCompleteTransitToWarehouse(
        ItemStackId stackId,
        string transitOwnerId,
        IWarehouseFacility warehouse,
        out WarehouseMassAdmissionReceipt receipt,
        out DomainFailure failure);

    bool TryPickupReservedStackQuantity(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemReservedStackQuantity reservation,
        out int pickedUp,
        out string failureReason);

    bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        out string failureReason);

    bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason);

    bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        out string failureReason);

    bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason);

    bool TryConsumeFacilityBuffer(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs,
        out string failureReason);

    bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason);

    int ReleaseQuantityReservationsByOwner(
        string ownerOperationId,
        ItemReservationReleaseReason reason);

    bool RenewQuantityReservation(
        string leaseId,
        double requestedUntilGameSeconds,
        out DomainFailure failure);

    bool TryReserveAvailableStackForDirectPickup(
        string ownerCharacterId,
        string ownerOperationId,
        ItemReservationPurpose purpose,
        string stackId,
        int quantity,
        out ItemQuantityLease lease,
        out DomainFailure failure);

    bool ReleaseQuantityReservation(
        string leaseId,
        ItemReservationReleaseReason reason);
}

public readonly struct ItemTransitDestination
{
    public ItemTransitDestination(
        WorldItemStackState state,
        Vector2Int position,
        string destinationId)
    {
        State = state;
        Position = position;
        DestinationId = destinationId?.Trim() ?? string.Empty;
    }

    public WorldItemStackState State { get; }
    public Vector2Int Position { get; }
    public string DestinationId { get; }
}

public readonly struct ItemExtractionReceipt
{
    public ItemExtractionReceipt(
        string leaseId,
        string sourceStackId,
        string extractedStackId,
        string itemId,
        int extractedQuantity,
        int sourceRemainingQuantity,
        bool sourceIdentityTransferred)
    {
        LeaseId = leaseId?.Trim() ?? string.Empty;
        SourceStackId = sourceStackId?.Trim() ?? string.Empty;
        ExtractedStackId = extractedStackId?.Trim() ?? string.Empty;
        ItemId = itemId?.Trim() ?? string.Empty;
        ExtractedQuantity = Mathf.Max(0, extractedQuantity);
        SourceRemainingQuantity = Mathf.Max(0, sourceRemainingQuantity);
        SourceIdentityTransferred = sourceIdentityTransferred;
    }

    public string LeaseId { get; }
    public string SourceStackId { get; }
    public string ExtractedStackId { get; }
    public string ItemId { get; }
    public int ExtractedQuantity { get; }
    public int SourceRemainingQuantity { get; }
    public bool SourceIdentityTransferred { get; }
}

public interface IReservedItemTransferService
{
    bool TryExtractReservedQuantity(
        string leaseId,
        int quantity,
        ItemTransitDestination destination,
        out ItemExtractionReceipt receipt,
        out DomainFailure failure);

    bool TryConsumeReservedQuantity(
        string leaseId,
        int quantity,
        out DomainFailure failure);
}

public sealed class ReservedRetailStockTransferReceipt
{
    internal ReservedRetailStockTransferReceipt(
        string operationId,
        IReadOnlyList<RetailStockLotSnapshot> lots,
        IReadOnlyList<RetailStockSourceUndo> undo,
        CharacterCarryInventory carryInventory,
        IReadOnlyList<CharacterCarriedItemSaveData> carriedUndo)
    {
        OperationId = operationId;
        Lots = lots;
        Undo = undo;
        CarryInventory = carryInventory;
        CarriedUndo = carriedUndo;
    }

    public string OperationId { get; }
    public IReadOnlyList<RetailStockLotSnapshot> Lots { get; }
    internal IReadOnlyList<RetailStockSourceUndo> Undo { get; }
    internal CharacterCarryInventory CarryInventory { get; }
    internal IReadOnlyList<CharacterCarriedItemSaveData> CarriedUndo { get; }
    internal bool RolledBack { get; set; }
}

internal readonly struct RetailStockSourceUndo
{
    internal RetailStockSourceUndo(
        WorldItemStackRecord snapshot,
        int removedQuantity,
        bool recordWasRemoved)
    {
        Snapshot = snapshot;
        RemovedQuantity = removedQuantity;
        RecordWasRemoved = recordWasRemoved;
    }

    internal WorldItemStackRecord Snapshot { get; }
    internal int RemovedQuantity { get; }
    internal bool RecordWasRemoved { get; }
}

public interface IReservedRetailStockTransferService
{
    bool TryTakeReservedRetailLots(
        string leaseId,
        int quantity,
        int saleItemId,
        string expectedItemDefinitionId,
        string operationId,
        CharacterCarryInventory carryInventory,
        out ReservedRetailStockTransferReceipt receipt,
        out DomainFailure failure);

    bool TryRollbackRetailTransfer(
        ReservedRetailStockTransferReceipt receipt,
        out DomainFailure failure);
}

public interface ICarriedItemDropService
{
    bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        out string failureReason);
    bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason);
    bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IReadOnlyCollection<string> ownerOperationIds,
        HaulCarryDropContext context,
        out string failureReason);
}

public readonly struct ReservedItemConsumption
{
    public ReservedItemConsumption(string stackId, int quantity)
    {
        StackId = stackId?.Trim() ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
    }

    public string StackId { get; }
    public int Quantity { get; }
    public bool IsValid => StackId.Length > 0 && Quantity > 0;
}

/// <summary>
/// Commits an already-reserved set of physical item costs as one repository
/// mutation. Every stack and quantity is validated before the first item is
/// removed, so a stale or stolen final stack cannot leave a partial payment.
/// </summary>
public interface IAtomicItemConsumptionService
{
    bool TryConsumeReserved(
        IReadOnlyList<ReservedItemConsumption> consumptions,
        string reservationOwnerId,
        out DomainFailure failure);
}

public sealed class AtomicItemConsumptionService :
    IAtomicItemConsumptionService
{
    private readonly WorldItemRepository repository;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly IItemQuantityReservationService reservations;
    private readonly IItemQuantityLeaseMutation leaseMutations;

    public AtomicItemConsumptionService(
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter,
        IItemQuantityReservationService reservations,
        IItemQuantityLeaseMutation leaseMutations)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.markerPresenter = markerPresenter
            ?? throw new ArgumentNullException(nameof(markerPresenter));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.leaseMutations = leaseMutations
            ?? throw new ArgumentNullException(nameof(leaseMutations));
    }

    public bool TryConsumeReserved(
        IReadOnlyList<ReservedItemConsumption> consumptions,
        string reservationOwnerId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string owner = reservationOwnerId?.Trim() ?? string.Empty;
        ReservedItemConsumption[] costs = (consumptions
                ?? Array.Empty<ReservedItemConsumption>())
            .Where(value => value.IsValid)
            .GroupBy(value => value.StackId, StringComparer.Ordinal)
            .Select(group => new ReservedItemConsumption(
                group.Key,
                group.Sum(value => value.Quantity)))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (costs.Length == 0)
        {
            return true;
        }
        if (owner.Length == 0)
        {
            failure = new DomainFailure(FailureCode.ItemTransferConsumptionFailed);
            return false;
        }

        if (!reservations.TryGetLeasesByOwner(owner, out IReadOnlyList<ItemQuantityLease> leases))
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationLeaseMissing,
                owner);
            return false;
        }
        Dictionary<string, Queue<(string leaseId, int quantity)>> leasedByStack =
            new(StringComparer.Ordinal);
        foreach (ItemQuantityLease lease in leases)
        {
            foreach (ItemLeaseSlice slice in lease.slices ?? new List<ItemLeaseSlice>())
            {
                if (slice == null || slice.quantity <= 0) continue;
                if (!leasedByStack.TryGetValue(
                        slice.stackId,
                        out Queue<(string leaseId, int quantity)> queue))
                {
                    queue = new Queue<(string leaseId, int quantity)>();
                    leasedByStack.Add(slice.stackId, queue);
                }
                queue.Enqueue((lease.leaseId, slice.quantity));
            }
        }

        List<(WorldItemStackRecord record, int quantity)> resolved = new();
        List<(string leaseId, int quantity)> leaseConsumptions = new();
        foreach (ReservedItemConsumption cost in costs)
        {
            if (!repository.RecordsById.TryGetValue(
                    cost.StackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.quantity < cost.Quantity
                || !leasedByStack.TryGetValue(
                    cost.StackId,
                    out Queue<(string leaseId, int quantity)> queue))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed);
                return false;
            }
            int remainingCost = cost.Quantity;
            while (remainingCost > 0 && queue.Count > 0)
            {
                (string leaseId, int leasedQuantity) = queue.Dequeue();
                int consume = Math.Min(remainingCost, leasedQuantity);
                leaseConsumptions.Add((leaseId, consume));
                remainingCost -= consume;
                if (leasedQuantity > consume)
                    queue.Enqueue((leaseId, leasedQuantity - consume));
            }
            if (remainingCost > 0)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationQuantityUnavailable,
                    cost.StackId);
                return false;
            }
            resolved.Add((record, cost.Quantity));
        }

        if (resolved.Any(value =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    value.record.components)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferConsumptionFailed,
                owner,
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass
                    .ToString());
            return false;
        }

        foreach ((string leaseId, int quantity) in leaseConsumptions)
        {
            if (!leaseMutations.TryConsumeSlices(
                    leaseId,
                    quantity,
                    out _,
                    out failure))
            {
                return false;
            }
        }

        HashSet<Vector2Int> changedPositions = new();
        foreach ((WorldItemStackRecord record, int quantity) in resolved)
        {
            changedPositions.Add(record.position);
            record.quantity -= quantity;
            if (record.quantity > 0) continue;
            if (!string.IsNullOrWhiteSpace(record.itemInstanceId))
            {
                repository.TryMarkEquipmentLostBySourceStack(record.stackId);
                repository.TryMarkModuleLostBySourceStack(record.stackId);
            }
            repository.Remove(record);
        }
        repository.MarkChanged();
        foreach (Vector2Int position in changedPositions)
        {
            markerPresenter.RefreshAt(position);
        }
        return true;
    }
}

public readonly struct ItemTransitStackSnapshot
{
    public ItemTransitStackSnapshot(
        ItemStackId stackId,
        string itemId,
        int quantity,
        bool forbidden,
        float contamination)
    {
        StackId = stackId;
        ItemId = itemId ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
        Forbidden = forbidden;
        Contamination = Mathf.Clamp(contamination, 0f, 100f);
    }

    public ItemStackId StackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public bool Forbidden { get; }
    public float Contamination { get; }
    public bool IsValid => StackId.IsValid && Quantity > 0;
}

public sealed class ItemTransferService :
    IItemTransferService,
    IReservedItemTransferService,
    IReservedRetailStockTransferService,
    ICarriedItemDropService,
    IProductionCapacityRoutingActorQuiescence
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemHaulingSettingsProvider haulingSettingsProvider;
    private readonly ICharacterIdRegistry characterIdRegistry;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IFacilityBufferDestinationClaimQuery destinationClaims;
    private readonly ICombatEquipmentCatalog combatEquipmentCatalog;
    private readonly IGameEventBus gameEventBus;
    private readonly WorldItemRepository repository;
    private readonly IWorldItemSpawner itemSpawner;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly WorldItemQueryService itemQueries;
    private readonly WorldItemWarehouseService warehouseService;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly IItemQuantityLeaseMutation quantityLeaseMutations;
    private readonly IBufferStackAggregationService bufferAggregation;
    private readonly IWarehouseMassAdmissionService warehouseMassAdmission;
    private readonly IPhysicalItemMassQuery physicalMass;
    private readonly IRetailStockPhysicalRuntime retailStockPhysical;
    private long facilityConsumptionSequence;
    private long directConsumptionSequence;
#if UNITY_EDITOR
    public Func<int, bool> DebugFailBeforeCapacityActorQuiescenceMutation;
#endif

    public ItemTransferService(
        WorldItemReadServices readServices,
        ICharacterIdRegistry characterIdRegistry,
        IGridSystemProvider gridSystemProvider,
        ICharacterAiWorldRegistry worldRegistry,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        ICombatEquipmentCatalog combatEquipmentCatalog,
        IGameEventBus gameEventBus,
        WorldItemRepository repository,
        IWorldItemSpawner itemSpawner,
        WorldItemWarehouseService warehouseService,
        IItemQuantityReservationService quantityReservations,
        IItemQuantityLeaseMutation quantityLeaseMutations,
        IBufferStackAggregationService bufferAggregation,
        IWarehouseMassAdmissionService warehouseMassAdmission = null,
        IRetailStockPhysicalRuntime retailStockPhysical = null)
    {
        WorldItemReadServices reads = readServices
            ?? throw new ArgumentNullException(nameof(readServices));
        catalogProvider = reads.Catalog;
        physicalMass = reads.Mass;
        haulingSettingsProvider = reads.HaulingSettings;
        this.characterIdRegistry = characterIdRegistry
            ?? throw new ArgumentNullException(nameof(characterIdRegistry));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.destinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
        this.combatEquipmentCatalog = combatEquipmentCatalog
            ?? throw new ArgumentNullException(nameof(combatEquipmentCatalog));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.itemSpawner = itemSpawner
            ?? throw new ArgumentNullException(nameof(itemSpawner));
        markerPresenter = reads.Markers;
        debugRules = reads.DebugRules;
        itemQueries = reads.Queries;
        this.warehouseService = warehouseService
            ?? throw new ArgumentNullException(nameof(warehouseService));
        this.quantityReservations = quantityReservations
            ?? throw new ArgumentNullException(nameof(quantityReservations));
        this.quantityLeaseMutations = quantityLeaseMutations
            ?? throw new ArgumentNullException(nameof(quantityLeaseMutations));
        this.bufferAggregation = bufferAggregation
            ?? throw new ArgumentNullException(nameof(bufferAggregation));
        this.warehouseMassAdmission = warehouseMassAdmission;
        this.retailStockPhysical = retailStockPhysical;
    }

    [GameplayInternalOnly(
        "Atomically publishes one exact carried descendant vector and its durable actor receipt.",
        "Production capacity-routing destructive-drain participant only")]
    public ProductionCapacityRoutingActorQuiescenceResult
        TryQuiesceAtCurrentCell(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            ProductionCapacityRoutingActorQuiescenceRequest request)
    {
        static ProductionCapacityRoutingActorQuiescenceResult Fail(
            ProductionCapacityRoutingDrainStatus status,
            string reason) => new(
            status,
            default,
            0,
            0L,
            null,
            reason);

        if (actor == null || inventory == null || request == null)
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                "capacity-routing-actor-quiescence-runtime-unavailable");
        if (!CharacterPersistentIdentity.TryGet(actor, out CharacterId actorId)
            || !actorId.Equals(inventory.CharacterId)
            || !string.Equals(
                actorId.Value,
                request.ActorPersistentId,
                StringComparison.Ordinal)
            || !IsCanonicalQuiescenceToken(request.StepOperationId)
            || !IsCanonicalQuiescenceToken(request.BatchCommitId)
            || !IsLowerSha256Token(request.DrainRequestFingerprint)
            || request.Plan == null
            || !string.Equals(
                request.Plan.ActorPersistentId,
                request.ActorPersistentId,
                StringComparison.Ordinal)
            || !IsLowerSha256Token(request.Plan.Fingerprint)
            || request.Plan.QuantityLeaseIds.Count == 0
            || request.Plan.PickedLeaseIds.Count == 0
            || request.Plan.PickedLeaseIds.Any(value =>
                !request.Plan.QuantityLeaseIds.Contains(
                    value,
                    StringComparer.Ordinal))
            || request.ExpectedCarries == null
            || request.ExpectedCarries.Count == 0)
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Conflict,
                "capacity-routing-actor-quiescence-request-invalid");
        }
        if (!repository.TryGetPendingCapacityRoutingDrain(
                request.StepOperationId,
                out ProductionCapacityRoutingDrainSaveData pending)
            || pending == null
            || !string.Equals(
                pending.batchCommitId,
                request.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                pending.requestFingerprint,
                request.DrainRequestFingerprint,
                StringComparison.Ordinal))
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Conflict,
                "capacity-routing-actor-quiescence-drain-authority-conflict");
        }

        string[] expectedRowKeys = request.ExpectedCarries
            .Select(ProductionCapacityRoutingDrainFingerprint.ActorCarryKey)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] plannedRowKeys = pending.sourceActorCarries
            .Where(value => string.Equals(
                value.actorPersistentId,
                request.ActorPersistentId,
                StringComparison.Ordinal))
            .Select(ProductionCapacityRoutingDrainFingerprint.ActorCarryKey)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expectedOperationIds = request.ExpectedCarries
            .Select(value => value.haulIntentOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (expectedRowKeys.Length == 0
            || expectedRowKeys.Distinct(StringComparer.Ordinal).Count()
                != expectedRowKeys.Length
            || !expectedRowKeys.SequenceEqual(
                plannedRowKeys,
                StringComparer.Ordinal)
            || !request.Plan.OperationIds.SequenceEqual(
                expectedOperationIds,
                StringComparer.Ordinal))
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Conflict,
                "capacity-routing-actor-quiescence-row-vector-conflict");
        }

        ProductionCapacityRoutingActorQuiesceReceiptSaveData durableReceipt =
            pending.actorQuiesceReceipts.FirstOrDefault(value => value != null
                && string.Equals(
                    value.actorPersistentId,
                    request.ActorPersistentId,
                    StringComparison.Ordinal));
        if (durableReceipt != null)
        {
            if (!durableReceipt.carriedRowKeys.SequenceEqual(
                    expectedRowKeys,
                    StringComparer.Ordinal)
                || !string.Equals(
                    durableReceipt.receiptFingerprint,
                    ProductionCapacityRoutingDrainFingerprint
                        .CreateActorQuiesceReceiptFingerprint(
                            pending.stepOperationId,
                            pending.requestFingerprint,
                            durableReceipt),
                    StringComparison.Ordinal))
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Conflict,
                    "capacity-routing-actor-quiescence-durable-receipt-conflict");
            }
            if (!TryVerifyDurableReceipt(
                    actor,
                    inventory,
                    pending,
                    durableReceipt,
                    out string replayFailure))
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Conflict,
                    replayFailure);
            }
            int replayQuantity = request.ExpectedCarries.Sum(value => value.quantity);
            long replayMass = request.ExpectedCarries.Sum(value => value.massGrams);
            return new ProductionCapacityRoutingActorQuiescenceResult(
                ProductionCapacityRoutingDrainStatus.Replay,
                new Vector2Int(
                    durableReceipt.physicalCellX,
                    durableReceipt.physicalCellY),
                replayQuantity,
                replayMass,
                durableReceipt,
                string.Empty);
        }
        if (pending.phase != ProductionCapacityRoutingDrainPhase.QuiescingActors)
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                "capacity-routing-actor-quiescence-phase-mismatch");
        }
        string[] plannedActors = pending.sourceActorCarries
            .Select(value => value.actorPersistentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (pending.actorQuiesceReceipts.Count >= plannedActors.Length
            || !string.Equals(
                plannedActors[pending.actorQuiesceReceipts.Count],
                request.ActorPersistentId,
                StringComparison.Ordinal))
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                "capacity-routing-actor-quiescence-out-of-order");
        }

        if (!inventory.TryPrepareCapacityRoutingExactPhysicalTransfer(
                request.ExpectedCarries,
                out CharacterCarryInventory.ExactPhysicalTransferCandidate
                    carryCandidate,
                out string carryFailure))
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                carryFailure);
        }

        Vector2Int physicalCell = ResolveActorGridPosition(actor);
        List<WorldItemStackRecord> records = new(request.ExpectedCarries.Count);
        Dictionary<string, HaulDeliveryIntentSaveData> intents =
            new(StringComparer.Ordinal);
        string targetDestinationId = string.Empty;
        Vector2Int targetPosition = default;
        int totalQuantity = 0;
        long totalMass = 0L;
        foreach (ProductionCapacityRoutingDrainActorCarrySaveData expected in
                 request.ExpectedCarries)
        {
            if (!TryValidateCapacityActorCarry(
                    actorId.Value,
                    request.BatchCommitId,
                    physicalCell,
                    expected,
                    out WorldItemStackRecord record,
                    out HaulDeliveryIntentSaveData intent,
                    out string rowFailure))
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Deferred,
                    rowFailure);
            }
            Vector2Int rowTarget = new(intent.dropGridX, intent.dropGridY);
            if (targetDestinationId.Length == 0)
            {
                targetDestinationId = intent.destinationId;
                targetPosition = rowTarget;
            }
            else if (!string.Equals(
                         targetDestinationId,
                         intent.destinationId,
                         StringComparison.Ordinal)
                     || targetPosition != rowTarget)
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Deferred,
                    "capacity-routing-mixed-carried-destination");
            }
            records.Add(record);
            intents[intent.operationId] = intent;
            try
            {
                totalQuantity = checked(totalQuantity + expected.quantity);
                totalMass = checked(totalMass + expected.massGrams);
            }
            catch (OverflowException)
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Conflict,
                    "capacity-routing-actor-quiescence-total-overflow");
            }
        }
        foreach (KeyValuePair<string, HaulDeliveryIntentSaveData> pair in intents)
        {
            string[] expectedCommitments = request.ExpectedCarries
                .Where(value => string.Equals(
                    value.haulIntentOperationId,
                    pair.Key,
                    StringComparison.Ordinal))
                .Select(value => value.carriedStackId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] actualCommitments = (pair.Value.commitments
                    ?? new List<HaulDeliveryItemCommitmentSaveData>())
                .Where(value => value != null && value.quantity > 0)
                .Select(value => value.carriedStackId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!actualCommitments.SequenceEqual(
                    expectedCommitments,
                    StringComparer.Ordinal))
            {
                return Fail(
                    ProductionCapacityRoutingDrainStatus.Deferred,
                    "capacity-routing-mixed-or-extra-intent-commitment:"
                    + pair.Key);
            }
        }

        string prePhysicalFingerprint =
            CreateCapacityActorPhysicalFingerprint(records);
        if (!carryCandidate.TryPublish(out string publishFailure))
        {
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                publishFailure);
        }

        bool committed = repository.TryQuiesceCarriedBatchAtomically(
            records,
            physicalCell,
            targetDestinationId,
            targetPosition,
            pending,
            () => CreateCapacityActorQuiescenceReceipt(
                pending,
                request,
                physicalCell,
                expectedRowKeys,
                prePhysicalFingerprint,
                CreateCapacityActorPhysicalFingerprint(records)),
#if UNITY_EDITOR
            DebugFailBeforeCapacityActorQuiescenceMutation,
#else
            null,
#endif
            out ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
            out string repositoryFailure);
        if (!committed)
        {
            carryCandidate.Rollback();
            return Fail(
                ProductionCapacityRoutingDrainStatus.Deferred,
                repositoryFailure);
        }

        carryCandidate.Complete();
        markerPresenter.RefreshAt(physicalCell);
        return new ProductionCapacityRoutingActorQuiescenceResult(
            ProductionCapacityRoutingDrainStatus.Applied,
            physicalCell,
            totalQuantity,
            totalMass,
            receipt,
            string.Empty);
    }

    public bool TryVerifyDurableReceipt(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        ProductionCapacityRoutingDrainSaveData drain,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null
            || inventory == null
            || drain == null
            || receipt == null
            || !CharacterPersistentIdentity.TryGet(
                actor,
                out CharacterId actorId)
            || !actorId.Equals(inventory.CharacterId)
            || !string.Equals(
                actorId.Value,
                receipt.actorPersistentId,
                StringComparison.Ordinal)
            || !string.Equals(
                drain.batchCommitId,
                receipt.batchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.receiptFingerprint,
                ProductionCapacityRoutingDrainFingerprint
                    .CreateActorQuiesceReceiptFingerprint(
                        drain.stepOperationId,
                        drain.requestFingerprint,
                        receipt),
                StringComparison.Ordinal))
        {
            failureReason =
                "capacity-routing-durable-actor-receipt-identity-conflict";
            return false;
        }

        ProductionCapacityRoutingDrainActorCarrySaveData[] carries =
            (drain.sourceActorCarries
                ?? new List<ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.actorPersistentId,
                    receipt.actorPersistentId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ThenBy(value => value.haulIntentOperationId, StringComparer.Ordinal)
            .ToArray();
        string[] rowKeys = carries
            .Select(ProductionCapacityRoutingDrainFingerprint.ActorCarryKey)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] operationIds = carries
            .Select(value => value.haulIntentOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (carries.Length == 0
            || !rowKeys.SequenceEqual(
                receipt.carriedRowKeys,
                StringComparer.Ordinal)
            || inventory.Items.Any(item => item != null
                && item.quantity > 0
                && operationIds.Contains(
                    item.ownerOperationId,
                    StringComparer.Ordinal)))
        {
            failureReason =
                "capacity-routing-durable-actor-receipt-cargo-conflict";
            return false;
        }

        Vector2Int cell = new(receipt.physicalCellX, receipt.physicalCellY);
        List<WorldItemStackRecord> records = new(carries.Length);
        foreach (ProductionCapacityRoutingDrainActorCarrySaveData carry in carries)
        {
            if (!repository.RecordsById.TryGetValue(
                    carry.carriedStackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.state != WorldItemStackState.Loose
                || record.position != cell
                || record.quantity != carry.quantity
                || string.IsNullOrEmpty(record.destinationId)
                || !record.hasDestinationPosition
                || record.dropDisposition != WorldItemDropDisposition.None
                || !string.IsNullOrEmpty(record.recoveryOwnerOperationId)
                || !string.IsNullOrEmpty(record.recoverySourceStackId)
                || !string.IsNullOrEmpty(record.recoveryCarrierPersistentId)
                || record.recoveryInterruptionKind !=
                    WorldItemCarryInterruptionKind.None)
            {
                failureReason =
                    "capacity-routing-durable-actor-receipt-physical-conflict:"
                    + carry.carriedStackId;
                return false;
            }
            records.Add(record);
        }
        if (!string.Equals(
                CreateCapacityActorPhysicalFingerprint(records),
                receipt.postPhysicalFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "capacity-routing-durable-actor-receipt-fingerprint-conflict";
            return false;
        }

        string[] liveLeaseIds = operationIds
            .SelectMany(operationId => quantityReservations.TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                ? leases.Select(lease => lease.leaseId)
                : Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        ProductionCapacityRoutingActorAuthorityReleaseSaveData release =
            drain.actorAuthorityReleases?.FirstOrDefault(value => value != null
                && string.Equals(
                    value.actorPersistentId,
                    receipt.actorPersistentId,
                    StringComparison.Ordinal));
        bool leasesValid = release == null
            ? liveLeaseIds.SequenceEqual(
                receipt.quantityLeaseIds,
                StringComparer.Ordinal)
            : release.effectsCommitted
                ? liveLeaseIds.Length == 0
                : release.operations.All(row =>
                {
                    string[] current = quantityReservations
                        .TryGetLeasesByOwner(
                            row.operationId,
                            out IReadOnlyList<ItemQuantityLease> rowLeases)
                        ? rowLeases.Select(value => value.leaseId)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray()
                        : Array.Empty<string>();
                    return current.Length == 0
                        || current.SequenceEqual(
                            row.quantityLeaseIds,
                            StringComparer.Ordinal);
                });
        if (!leasesValid)
        {
            failureReason =
                "capacity-routing-durable-actor-receipt-lease-conflict";
            return false;
        }
        return true;
    }

    public int ReleaseQuantityReservationsByOwner(
        string ownerOperationId,
        ItemReservationReleaseReason reason) =>
        quantityReservations.ReleaseByOwner(ownerOperationId, reason);

    public bool RenewQuantityReservation(
        string leaseId,
        double requestedUntilGameSeconds,
        out DomainFailure failure) =>
        quantityReservations.Renew(
            leaseId,
            requestedUntilGameSeconds,
            out failure);

    public bool TryReserveAvailableStackForDirectPickup(
        string ownerCharacterId,
        string ownerOperationId,
        ItemReservationPurpose purpose,
        string stackId,
        int quantity,
        out ItemQuantityLease lease,
        out DomainFailure failure)
    {
        lease = null;
        failure = DomainFailure.None;
        string characterId = ownerCharacterId?.Trim() ?? string.Empty;
        string operationId = ownerOperationId?.Trim() ?? string.Empty;
        string normalizedStackId = stackId?.Trim() ?? string.Empty;
        if (characterId.Length == 0
            || operationId.Length == 0
            || quantity <= 0
            || !repository.RecordsById.TryGetValue(
                normalizedStackId,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.state is not (WorldItemStackState.Loose
                or WorldItemStackState.Stored)
            || record.forbidden
            || FacilityOutputExactRouteCustodyCodec.IsRouteBlocked(
                record.components))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferStackUnavailable,
                normalizedStackId);
            return false;
        }

        return quantityReservations.TryReserve(
            operationId,
            characterId,
            purpose,
            $"direct-pickup:{purpose}:{characterId}",
            new ItemQuantityReservationRequest(
                new ItemStackId(record.stackId),
                quantity,
                ItemReservationSignature.Create(record.itemId, record.components)),
            out lease,
            out failure);
    }

    public bool ReleaseQuantityReservation(
        string leaseId,
        ItemReservationReleaseReason reason) =>
        quantityReservations.Release(leaseId, reason);

    public bool TryExtractReservedQuantity(
        string leaseId,
        int quantity,
        ItemTransitDestination destination,
        out ItemExtractionReceipt receipt,
        out DomainFailure failure)
    {
        receipt = default;
        failure = DomainFailure.None;
        if (quantity <= 0
            || !quantityReservations.Revalidate(
                leaseId,
                out ItemQuantityLease lease,
                out failure)
            || lease.remainingQuantity < quantity)
        {
            if (!failure.IsFailure)
                failure = new DomainFailure(
                    FailureCode.ItemReservationQuantityUnavailable,
                    leaseId ?? string.Empty);
            return false;
        }

        ItemLeaseSlice sourceSlice = lease.slices.FirstOrDefault(slice =>
            slice != null
            && slice.quantity >= quantity
            && repository.RecordsById.ContainsKey(slice.stackId));
        if (sourceSlice == null
            || !repository.RecordsById.TryGetValue(
                sourceSlice.stackId,
                out WorldItemStackRecord source)
            || source == null
            || source.quantity < quantity)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                leaseId ?? string.Empty,
                sourceSlice?.stackId ?? string.Empty);
            return false;
        }

        string sourceId = source.stackId;
        bool transferredIdentity = source.quantity == quantity
            && source.reservedQuantity == quantity;
        string extractedId = transferredIdentity
            ? source.stackId
            : repository.AllocateStackId();
        Vector2Int sourcePosition = source.position;
        string itemId = source.itemId;
        WorldItemStackState transitState = destination.State is
            WorldItemStackState.Carried or WorldItemStackState.InTransit
                ? destination.State
                : WorldItemStackState.InTransit;
        List<ItemInstanceComponentSaveData> originalComponents =
            CloneComponents(source.components);
        if (!TryPrepareCustodyExtraction(
                source,
                quantity,
                out List<ItemInstanceComponentSaveData> sourceComponents,
                out List<ItemInstanceComponentSaveData> extractedComponents,
                out failure))
        {
            return false;
        }
        if (transferredIdentity)
        {
            repository.Relocate(source, destination.Position);
            source.state = transitState;
            source.destinationId = destination.DestinationId;
            source.aggregationCohortId = lease.aggregationCohortId;
            source.hasDestinationPosition = destination.DestinationId.Length > 0;
            source.destinationPosition = destination.Position;
            source.sourceStorageDestinationId = string.Empty;
            ClearTransientRecoveryMetadata(source);
            repository.MarkChanged();
        }
        else
        {
            WorldItemStackRecord child = CloneForTransit(
                source,
                extractedId,
                quantity,
                transitState,
                destination,
                lease.aggregationCohortId,
                extractedComponents);
            source.quantity -= quantity;
            source.components = sourceComponents;
            repository.Add(child);

            string sourceSignature = ItemReservationSignature.Create(
                source.itemId,
                source.components);
            string extractedSignature = ItemReservationSignature.Create(
                child.itemId,
                child.components);

            List<ItemLeaseSlice> replacements = BuildExtractionReplacements(
                lease,
                sourceId,
                extractedId,
                sourceSignature,
                extractedSignature,
                quantity);
            if (replacements == null
                || !quantityLeaseMutations.TryRetargetSlices(
                    new Dictionary<string, IReadOnlyList<ItemLeaseSlice>>
                    {
                        [lease.leaseId] = replacements
                    },
                    out failure))
            {
                repository.Remove(child);
                source.quantity += quantity;
                source.components = originalComponents;
                repository.MarkChanged();
                if (!failure.IsFailure)
                {
                    failure = new DomainFailure(
                        FailureCode.ItemReservationSliceInvalid,
                        lease.leaseId,
                        sourceId);
                }
                return false;
            }
        }

        markerPresenter.RefreshAt(sourcePosition);
        markerPresenter.RefreshAt(destination.Position);
        receipt = new ItemExtractionReceipt(
            leaseId,
            sourceId,
            extractedId,
            itemId,
            quantity,
            transferredIdentity ? 0 : source.quantity,
            transferredIdentity);
        return true;
    }

    public bool TryConsumeReservedQuantity(
        string leaseId,
        int quantity,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (quantity <= 0
            || !quantityReservations.Revalidate(
                leaseId,
                out ItemQuantityLease lease,
                out failure)
            || lease.remainingQuantity < quantity)
        {
            if (!failure.IsFailure)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationQuantityUnavailable,
                    leaseId ?? string.Empty);
            }
            return false;
        }

        List<(WorldItemStackRecord record, int quantity)> removals = new();
        int remaining = quantity;
        foreach (ItemLeaseSlice slice in lease.slices)
        {
            if (slice == null || slice.quantity <= 0 || remaining <= 0)
                continue;
            if (!repository.RecordsById.TryGetValue(
                    slice.stackId,
                    out WorldItemStackRecord record)
                || record == null)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationSliceInvalid,
                    lease.leaseId,
                    slice.stackId);
                return false;
            }
            int take = Mathf.Min(remaining, slice.quantity);
            removals.Add((record, take));
            remaining -= take;
        }
        if (remaining > 0)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationQuantityUnavailable,
                lease.leaseId);
            return false;
        }
        if (removals.Any(candidate =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    candidate.record.components)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferConsumptionFailed,
                lease.leaseId,
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass
                    .ToString());
            return false;
        }
        if (!quantityLeaseMutations.TryConsumeSlices(
                lease.leaseId,
                quantity,
                out _,
                out failure))
        {
            return false;
        }
        HashSet<Vector2Int> touched = new();
        foreach ((WorldItemStackRecord record, int remove) in removals)
        {
            touched.Add(record.position);
            record.quantity -= remove;
            if (record.quantity <= 0)
            {
                if (!string.IsNullOrWhiteSpace(record.itemInstanceId))
                {
                    repository.TryMarkEquipmentLostBySourceStack(record.stackId);
                    repository.TryMarkModuleLostBySourceStack(record.stackId);
                }
                repository.Remove(record);
            }
        }
        repository.MarkChanged();
        foreach (Vector2Int position in touched)
            markerPresenter.RefreshAt(position);
        return true;
    }

    private static WorldItemStackRecord CloneForTransit(
        WorldItemStackRecord source,
        string stackId,
        int quantity,
        WorldItemStackState state,
        ItemTransitDestination destination,
        string aggregationCohortId,
        IReadOnlyList<ItemInstanceComponentSaveData> components) => new()
    {
        stackId = stackId,
        itemInstanceId = source.itemInstanceId,
        itemId = source.itemId,
        quantity = quantity,
        state = state,
        position = destination.Position,
        destinationId = destination.DestinationId,
        aggregationCohortId = aggregationCohortId?.Trim() ?? string.Empty,
        hasDestinationPosition = destination.DestinationId.Length > 0,
        destinationPosition = destination.Position,
        forbidden = source.forbidden,
        sourceCharacterId = source.sourceCharacterId,
        sourceDisplayName = source.sourceDisplayName,
        sourceSpeciesTag = source.sourceSpeciesTag,
        sourceDeathReason = source.sourceDeathReason,
        emergencyButcheryAllowed = source.emergencyButcheryAllowed,
        wasteOrigin = source.wasteOrigin,
        contamination = source.contamination,
        components = (components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .Select(component => component.Clone())
            .ToList()
    };

    private bool TryPrepareCustodyExtraction(
        WorldItemStackRecord source,
        int extractedQuantity,
        out List<ItemInstanceComponentSaveData> sourceComponents,
        out List<ItemInstanceComponentSaveData> extractedComponents,
        out DomainFailure failure)
    {
        sourceComponents = CloneComponents(source?.components);
        extractedComponents = CloneComponents(source?.components);
        failure = DomainFailure.None;
        if (source == null)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                string.Empty,
                "physical-source-missing");
            return false;
        }
        if (!FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                source.components))
        {
            return true;
        }

        if (!FacilityOutputExactRouteCustodyCodec.TryRead(
                source.components,
                out FacilityOutputExactRouteCustodyMetadata custody)
            || custody.Phase != FacilityOutputExactRouteCustodyPhase.Routable
            || custody.Quantity != source.quantity
            || !string.Equals(custody.ItemId, source.itemId,
                StringComparison.Ordinal)
            || (source.state is not (WorldItemStackState.Carried
                    or WorldItemStackState.InTransit)
                && !string.Equals(
                    custody.CurrentTargetDestinationId,
                    source.destinationId,
                    StringComparison.Ordinal))
            || extractedQuantity <= 0
            || extractedQuantity > source.quantity)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                source?.stackId ?? string.Empty,
                "malformed-or-blocked-exact-route-custody");
            return false;
        }

        List<ItemInstanceComponentSaveData> business =
            CapturePhysicalBusinessComponents(source.components);
        string signature = FacilityBufferPlannedOutputPublicationService
            .CreateRuntimeComponentSignature(business);
        long totalMass = GetPhysicalQuantityMass(
            source.itemId,
            source.itemInstanceId,
            business,
            source.quantity);
        if (!string.Equals(
                signature,
                custody.ComponentSignature,
                StringComparison.Ordinal)
            || totalMass != custody.MassGrams)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                source.stackId,
                "exact-route-custody-payload-changed");
            return false;
        }

        if (extractedQuantity == source.quantity)
            return true;

        int remainderQuantity = checked(source.quantity - extractedQuantity);
        long extractedMass = GetPhysicalQuantityMass(
            source.itemId,
            source.itemInstanceId,
            business,
            extractedQuantity);
        long remainderMass = GetPhysicalQuantityMass(
            source.itemId,
            source.itemInstanceId,
            business,
            remainderQuantity);
        if (!custody.TryPartitionRoutablePrefix(
                source.stackId,
                extractedQuantity,
                extractedMass,
                remainderMass,
                out FacilityOutputExactRouteCustodyMetadata extracted,
                out FacilityOutputExactRouteCustodyMetadata remainder))
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                source.stackId,
                "exact-route-custody-partition-invalid");
            return false;
        }

        sourceComponents = FacilityOutputExactRouteCustodyCodec
            .ReplaceAuthority(source.components, remainder);
        extractedComponents = FacilityOutputExactRouteCustodyCodec
            .ReplaceAuthority(source.components, extracted);
        return true;
    }

    private long GetPhysicalQuantityMass(
        string itemId,
        string itemInstanceId,
        IReadOnlyList<ItemInstanceComponentSaveData> businessComponents,
        int quantity)
    {
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            physicalMass,
            (ItemDefinitionId)itemId,
            itemInstanceId,
            businessComponents);
        return physicalMass.GetQuantityMass(
            (ItemDefinitionId)itemId,
            subject,
            quantity).Value;
    }

    private static List<ItemInstanceComponentSaveData>
        CapturePhysicalBusinessComponents(
            IEnumerable<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>())
        .Where(component => component != null
            && !PlannedOutputPublicationComponentCodec.IsAnyMarker(component)
            && !FacilityOutputExactRouteCustodyCodec.IsCustody(component))
        .Select(component => component.Clone())
        .ToList();

    private static List<ItemInstanceComponentSaveData> CloneComponents(
        IEnumerable<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>())
        .Where(component => component != null)
        .Select(component => component.Clone())
        .ToList();

    private static List<ItemLeaseSlice> BuildExtractionReplacements(
        ItemQuantityLease lease,
        string sourceStackId,
        string extractedStackId,
        string sourceExpectedSignature,
        string extractedExpectedSignature,
        int quantity)
    {
        int remaining = quantity;
        List<ItemLeaseSlice> replacements = new();
        foreach (ItemLeaseSlice slice in lease.slices)
        {
            if (slice == null || slice.quantity <= 0)
                continue;
            if (!string.Equals(
                    slice.stackId,
                    sourceStackId,
                    StringComparison.Ordinal)
                || remaining <= 0)
            {
                replacements.Add(slice.Clone());
                continue;
            }
            int moved = Mathf.Min(remaining, slice.quantity);
            int left = slice.quantity - moved;
            if (left > 0)
            {
                replacements.Add(new ItemLeaseSlice
                {
                    stackId = slice.stackId,
                    originStackId = string.IsNullOrWhiteSpace(slice.originStackId)
                        ? slice.stackId
                        : slice.originStackId,
                    expectedStackSignature = sourceExpectedSignature,
                    quantity = left
                });
            }
            if (moved > 0)
            {
                replacements.Add(new ItemLeaseSlice
                {
                    stackId = extractedStackId,
                    originStackId = string.IsNullOrWhiteSpace(slice.originStackId)
                        ? slice.stackId
                        : slice.originStackId,
                    expectedStackSignature = extractedExpectedSignature,
                    quantity = moved
                });
            }
            remaining -= moved;
        }
        if (remaining > 0)
            return null;
        return replacements;
    }

    public bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure)
    {
        bool succeeded = warehouseService.TryRequestDelivery(
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out string rawFailure);
        failure = succeeded
            ? DomainFailure.None
            : new DomainFailure(
                string.IsNullOrWhiteSpace(destinationId)
                    ? FailureCode.ItemTransferDestinationMissing
                    : FailureCode.ItemTransferRequestFailed,
                itemId?.Trim() ?? string.Empty);
        return succeeded;
    }

    public bool TryRequestStackDelivery(
        ItemStackId stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure)
    {
        if (!stackId.IsValid)
        {
            requested = 0;
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }
        bool succeeded = warehouseService.TryRequestStackDelivery(
            stackId.Value,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out string rawFailure);
        failure = succeeded
            ? DomainFailure.None
            : new DomainFailure(
                string.IsNullOrWhiteSpace(destinationId)
                    ? FailureCode.ItemTransferDestinationMissing
                    : FailureCode.ItemTransferRequestFailed,
                stackId.Value);
        return succeeded;
    }

    public bool TryConsumeStackQuantity(
        ItemStackId stackId,
        int quantity,
        out WorldItemStackSnapshot consumed,
        out DomainFailure failure)
    {
        consumed = null;
        failure = DomainFailure.None;
        if (!stackId.IsValid
            || quantity <= 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.quantity - record.reservedQuantity < quantity)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferStackUnavailable,
                stackId.Value);
            return false;
        }

        if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                record.components))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferConsumptionFailed,
                stackId.Value,
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass
                    .ToString());
            return false;
        }

        WorldItemStackSnapshot snapshot = itemQueries.CreateSnapshot(record);
        snapshot.Quantity = quantity;
        if (debugRules.ShouldSkipCosts())
        {
            consumed = snapshot;
            return consumed.Quantity > 0;
        }

        string operationId =
            $"direct-item-consume:{++directConsumptionSequence:D16}:{stackId.Value}";
        string signature = ItemReservationSignature.Create(
            record.itemId,
            record.components);
        if (!quantityReservations.TryReserve(
                operationId,
                string.Empty,
                ItemReservationPurpose.DirectPlayerOrder,
                $"direct-consume:{record.itemId}",
                new ItemQuantityReservationRequest(
                    stackId,
                    quantity,
                    signature),
                out ItemQuantityLease lease,
                out failure))
        {
            return false;
        }
        if (!TryConsumeReservedQuantity(
                lease.leaseId,
                quantity,
                out failure))
        {
            quantityReservations.ReleaseByOwner(
                operationId,
                ItemReservationReleaseReason.Cancelled);
            return false;
        }
        consumed = snapshot;
        return true;
    }

    public bool TrySpawnItem(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned)
    {
        spawned = itemSpawner.Spawn(
            itemId,
            amount,
            position,
            state,
            destinationId?.Trim() ?? string.Empty);
        return spawned == Mathf.Max(0, amount);
    }

    public bool TrySpawnItemWithComponents(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int spawned)
    {
        spawned = itemSpawner.Spawn(
            itemId,
            amount,
            position,
            state,
            destinationId?.Trim() ?? string.Empty,
            components: components);
        return spawned == Mathf.Max(0, amount);
    }

    public bool TryRouteFacilityOutput(
        string sourceDestinationId,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int routed,
        out DomainFailure failure)
    {
        routed = 0;
        failure = DomainFailure.None;
        string sourceId = sourceDestinationId?.Trim() ?? string.Empty;
        string normalizedItemId = itemId?.Trim() ?? string.Empty;
        string targetId = destinationId?.Trim() ?? string.Empty;
        if (sourceId.Length == 0
            || normalizedItemId.Length == 0
            || amount <= 0)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                normalizedItemId);
            return false;
        }

        WorldItemStackRecord[] candidates = repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && record.state == WorldItemStackState.FacilityOutputBuffer
                && !record.forbidden
                && record.quantity - record.reservedQuantity > 0
                && string.Equals(
                    record.destinationId,
                    sourceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    record.itemId,
                    normalizedItemId,
                    StringComparison.Ordinal))
            .OrderBy(record => record.stackId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Any(record =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    record.components)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                normalizedItemId,
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass
                    .ToString());
            return false;
        }
        if (candidates.Sum(record => Mathf.Max(
                0,
                quantityReservations.GetAvailableQuantity(
                    new ItemStackId(record.stackId)))) < amount)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferStackUnavailable,
                normalizedItemId);
            return false;
        }

        int remaining = amount;
        foreach (WorldItemStackRecord source in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            int moved = Mathf.Min(
                remaining,
                Mathf.Max(
                    0,
                    quantityReservations.GetAvailableQuantity(
                        new ItemStackId(source.stackId))));
            if (moved <= 0)
                continue;
            Vector2Int sourcePosition = source.position;
            source.quantity -= moved;
            repository.MarkChanged();
            if (source.quantity <= 0)
            {
                repository.Remove(source);
            }

            int spawned = itemSpawner.Spawn(
                normalizedItemId,
                moved,
                sourcePosition,
                WorldItemStackState.Loose,
                targetId,
                hasDestinationPosition: targetId.Length > 0,
                destinationPosition: destinationPosition,
                wasteOrigin: source.wasteOrigin,
                contamination: source.contamination);
            if (spawned != moved)
            {
                itemSpawner.Spawn(
                    normalizedItemId,
                    moved - Mathf.Max(0, spawned),
                    sourcePosition,
                    WorldItemStackState.FacilityOutputBuffer,
                    sourceId,
                    wasteOrigin: source.wasteOrigin,
                    contamination: source.contamination);
                routed += Mathf.Max(0, spawned);
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    normalizedItemId);
                return false;
            }

            routed += moved;
            remaining -= moved;
            markerPresenter.RefreshAt(sourcePosition);
        }

        if (targetId.Length > 0)
        {
            warehouseService.PrioritizeDestination(targetId);
        }
        return routed == amount;
    }

    public void PrioritizeDestination(string destinationId)
    {
        warehouseService.PrioritizeDestination(destinationId);
    }

    public int ReleaseDestination(
        string destinationId,
        Vector2Int releasePosition)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            return 0;
        }
        WorldItemStackRecord[] targets = repository.Records
            .Where(record => record != null
                && string.Equals(
                    record.destinationId,
                    destination,
                    StringComparison.Ordinal))
            .ToArray();
        if (targets.Any(target =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    target.components)))
        {
            throw new FacilityOutputExactRouteBypassException(
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass,
                nameof(ReleaseDestination));
        }
        int released = 0;
        foreach (WorldItemStackRecord target in targets)
        {
            int quantity = Mathf.Max(0, target.quantity);
            if (quantity <= 0)
            {
                continue;
            }
            released += quantity;
            Vector2Int oldPosition = target.position;
            string sourceDestination = IsOutboundStoredStack(target)
                ? target.sourceStorageDestinationId
                : string.Empty;
            WorldItemStackState state = sourceDestination.Length > 0
                ? WorldItemStackState.Stored
                : WorldItemStackState.Loose;
            Vector2Int position = state == WorldItemStackState.Stored
                ? oldPosition
                : target.state is WorldItemStackState.FacilityBuffer
                    or WorldItemStackState.FacilityOutputBuffer
                        ? releasePosition
                        : oldPosition;
            quantityLeaseMutations.InvalidateStack(
                target.stackId,
                ItemReservationReleaseReason.Cancelled);
            repository.Relocate(target, position);
            target.state = state;
            target.destinationId = sourceDestination;
            target.sourceStorageDestinationId = string.Empty;
            target.hasDestinationPosition = false;
            target.destinationPosition = default;
            target.aggregationCohortId = string.Empty;
            target.reservedByPersistentId = string.Empty;
            target.reservedQuantity = 0;
            target.reservationRevision++;
            repository.TrySetEquipmentWorldStateBySourceStack(
                target.stackId,
                state == WorldItemStackState.Stored
                    ? CombatEquipmentWorldState.Stored
                    : CombatEquipmentWorldState.Loose);
            repository.MarkChanged();
            markerPresenter.RefreshAt(oldPosition);
            markerPresenter.RefreshAt(position);
        }
        return released;
    }

    public int RemoveDestination(
        string destinationId,
        params WorldItemStackState[] states)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        HashSet<WorldItemStackState> allowed = new(
            states ?? Array.Empty<WorldItemStackState>());
        if (destination.Length == 0 || allowed.Count == 0)
        {
            return 0;
        }
        WorldItemStackRecord[] targets = repository.Records
            .Where(record => record != null
                && allowed.Contains(record.state)
                && string.Equals(
                    record.destinationId,
                    destination,
                    StringComparison.Ordinal))
            .ToArray();
        if (targets.Any(target =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    target.components)))
        {
            throw new FacilityOutputExactRouteBypassException(
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass,
                nameof(RemoveDestination));
        }
        int removed = 0;
        foreach (WorldItemStackRecord target in targets)
        {
            removed += Mathf.Max(0, target.quantity);
            Vector2Int position = target.position;
            repository.Remove(target);
            markerPresenter.RefreshAt(position);
        }
        return removed;
    }

    public bool TryBeginTransit(
        ItemStackId stackId,
        Vector2Int expectedPosition,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack,
        out DomainFailure failure)
    {
        stack = default;
        failure = DomainFailure.None;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (!stackId.IsValid
            || ownerId.Length == 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackUnavailable,
                stackId.Value);
            return false;
        }

        if (record.state is not (WorldItemStackState.Loose
                or WorldItemStackState.FacilityOutputBuffer))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackUnavailable,
                stackId.Value);
            return false;
        }

        if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                record.components))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackUnavailable,
                stackId.Value,
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass
                    .ToString());
            return false;
        }

        if (record.reservedQuantity > 0)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackReserved,
                stackId.Value);
            return false;
        }

        if (Mathf.Abs(record.position.x - expectedPosition.x)
                + Mathf.Abs(record.position.y - expectedPosition.y)
            > 1)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackOutOfRange,
                stackId.Value);
            return false;
        }

        Vector2Int previousPosition = record.position;
        record.state = WorldItemStackState.InTransit;
        record.destinationId = ownerId;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.MarkChanged();
        markerPresenter.RefreshAt(previousPosition);
        stack = CreateTransitSnapshot(record);
        return true;
    }

    public bool TryInspectStackForTransit(
        ItemStackId stackId,
        out ItemTransitStackSnapshot stack)
    {
        stack = default;
        if (!stackId.IsValid
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0)
        {
            return false;
        }

        stack = CreateTransitSnapshot(record);
        return true;
    }

    public void CopyLoadableTransitStackIds(
        Vector2Int position,
        List<ItemStackId> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (!repository.RecordsByPosition.TryGetValue(
                position,
                out List<WorldItemStackRecord> records))
        {
            return;
        }

        foreach (WorldItemStackRecord record in records
                     .Where(record => record != null
                          && record.quantity > 0
                          && record.state is WorldItemStackState.Loose
                              or WorldItemStackState.FacilityOutputBuffer
                          && !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                              record.components)
                          && record.quantity - record.reservedQuantity > 0)
                     .OrderBy(record => record.stackId, StringComparer.Ordinal))
        {
            destination.Add(new ItemStackId(record.stackId));
        }
    }

    public bool TryGetTransitStack(
        ItemStackId stackId,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack)
    {
        stack = default;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (!stackId.IsValid
            || ownerId.Length == 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.state != WorldItemStackState.InTransit
            || !string.Equals(
                record.destinationId,
                ownerId,
                StringComparison.Ordinal))
        {
            return false;
        }

        stack = CreateTransitSnapshot(record);
        return true;
    }

    public bool TryCompleteTransit(
        ItemStackId stackId,
        string transitOwnerId,
        WorldItemStackState destinationState,
        Vector2Int destinationPosition,
        string destinationId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (!stackId.IsValid
            || ownerId.Length == 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.state != WorldItemStackState.InTransit
            || !string.Equals(
                record.destinationId,
                ownerId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorTransitOwnershipMismatch,
                stackId.Value,
                ownerId);
            return false;
        }

        if (destinationState is not (WorldItemStackState.Loose
                or WorldItemStackState.Stored
                or WorldItemStackState.FacilityBuffer
                or WorldItemStackState.FacilityOutputBuffer))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorDestinationUnavailable,
                destinationState.ToString());
            return false;
        }

        Vector2Int previousPosition = record.position;
        repository.Relocate(record, destinationPosition);
        record.state = destinationState;
        record.destinationId = destinationState == WorldItemStackState.Loose
            ? string.Empty
            : destinationId?.Trim() ?? string.Empty;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.MarkChanged();
        markerPresenter.RefreshAt(previousPosition);
        markerPresenter.RefreshAt(destinationPosition);
        return true;
    }

    private static ItemTransitStackSnapshot CreateTransitSnapshot(
        WorldItemStackRecord record) =>
        new ItemTransitStackSnapshot(
            new ItemStackId(record.stackId),
            record.itemId,
            record.quantity,
            record.forbidden,
            record.contamination);

    public bool TryPickupReservedStackQuantity(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemReservedStackQuantity reservation,
        out int pickedUp,
        out string failureReason)
    {
        pickedUp = 0;
        failureReason = string.Empty;
        if (actor == null || inventory == null || !reservation.IsValid)
        {
            failureReason = "invalid haul reservation";
            return false;
        }

        string actorId = characterIdRegistry.GetOrAssignPersistentId(actor);
        if (!repository.RecordsById.TryGetValue(
                reservation.StackId,
                out WorldItemStackRecord record)
            || record.quantity <= 0)
        {
            failureReason = "stack disappeared";
            return false;
        }

        ItemQuantityLease lease = null;
        DomainFailure leaseFailure = DomainFailure.None;
        if (string.IsNullOrWhiteSpace(reservation.LeaseId)
            || !quantityReservations.Revalidate(
                reservation.LeaseId,
                out lease,
                out leaseFailure)
            || !string.Equals(
                lease.ownerCharacterId,
                actorId,
                StringComparison.Ordinal)
            || !string.Equals(
                lease.ownerOperationId,
                reservation.OwnerOperationId,
                StringComparison.Ordinal))
        {
            failureReason = leaseFailure.IsFailure
                ? leaseFailure.ToString()
                : "stack quantity leased by another operation";
            return false;
        }

        int leasedOnStack = lease.slices
            .Where(slice => slice != null
                && string.Equals(
                    slice.stackId,
                    reservation.StackId,
                    StringComparison.Ordinal))
            .Sum(slice => Mathf.Max(0, slice.quantity));
        int requested = Mathf.Min(
            record.quantity,
            Mathf.Min(leasedOnStack, Mathf.Max(1, reservation.Quantity)));
        int accepted = inventory.GetMaxAcceptableQuantity(
            record.itemId,
            record.itemInstanceId,
            record.components,
            requested,
            catalogProvider,
            haulingSettingsProvider);
        if (accepted <= 0)
        {
            failureReason = "carry limit";
            return false;
        }

        // This is the last read-only boundary before pickup mutates either the
        // source warehouse, the physical repository, the quantity lease or the
        // actor inventory. Renewal in AbilityHaul is only a heartbeat: exact
        // prepared output must rejoin every source and destination authority
        // here so an invalidation between renewal and pickup cannot extract it.
        if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                record.components))
        {
            long exactMassGrams;
            try
            {
                exactMassGrams = GetPhysicalQuantityMass(
                    record.itemId,
                    record.itemInstanceId,
                    CapturePhysicalBusinessComponents(record.components),
                    record.quantity);
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException
                                               or KeyNotFoundException
                                               or OverflowException)
            {
                failureReason =
                    "items.haul.prepared_output_pickup_boundary:SourceLotStale:"
                    + exception.Message;
                return false;
            }

            PreparedOutputPickupBoundaryResult boundary =
                warehouseService.ValidatePreparedOutputPickupBoundary(
                    reservation,
                    lease,
                    record,
                    accepted,
                    exactMassGrams);
            if (boundary.IsFailure)
            {
                failureReason = boundary.ToString();
                return false;
            }
        }
        if (IsOutboundStoredStack(record)
            && !TryWithdrawOutboundStoredStock(
                record,
                accepted,
                out _,
                out _,
                out accepted,
                out failureReason))
        {
            return false;
        }

        Vector2Int position = record.position;
        string sourceDestinationId = record.destinationId;
        bool sourceHadDestinationPosition = record.hasDestinationPosition;
        Vector2Int sourceDestinationPosition = record.destinationPosition;
        if (!TryExtractReservedQuantity(
                reservation.LeaseId,
                accepted,
                new ItemTransitDestination(
                    WorldItemStackState.Carried,
                    ResolveActorGridPosition(actor),
                    actorId),
                out ItemExtractionReceipt extraction,
                out DomainFailure extractionFailure))
        {
            failureReason = extractionFailure.Code + ":"
                + string.Join(",", extractionFailure.Parameters.ToArray());
            return false;
        }
        if (!repository.RecordsById.TryGetValue(
                extraction.ExtractedStackId,
                out WorldItemStackRecord carriedRecord)
            || carriedRecord == null)
        {
            quantityReservations.Release(
                reservation.LeaseId,
                ItemReservationReleaseReason.StackInvalidated);
            failureReason = "transport stack missing after extraction";
            return false;
        }

        if (!inventory.TryAddLeasedPartialStack(
                carriedRecord.stackId,
                extraction.SourceStackId,
                reservation.OwnerOperationId,
                carriedRecord.itemInstanceId,
                carriedRecord.itemId,
                accepted,
                catalogProvider,
                haulingSettingsProvider,
                carriedRecord.wasteOrigin,
                carriedRecord.contamination,
                carriedRecord.components,
                out pickedUp,
                out string carryFailure)
            || pickedUp != accepted)
        {
            quantityReservations.Release(
                reservation.LeaseId,
                ItemReservationReleaseReason.Cancelled);
            repository.Relocate(carriedRecord, position);
            carriedRecord.state = WorldItemStackState.Loose;
            carriedRecord.aggregationCohortId = string.Empty;
            if (FacilityOutputExactRouteCustodyCodec.TryRead(
                    carriedRecord.components,
                    out FacilityOutputExactRouteCustodyMetadata custody)
                && custody.Phase ==
                    FacilityOutputExactRouteCustodyPhase.Routable)
            {
                carriedRecord.destinationId = sourceDestinationId;
                carriedRecord.hasDestinationPosition =
                    sourceHadDestinationPosition;
                carriedRecord.destinationPosition = sourceDestinationPosition;
            }
            else
            {
                carriedRecord.destinationId = string.Empty;
                carriedRecord.hasDestinationPosition = false;
                carriedRecord.destinationPosition = default;
            }
            repository.MarkChanged();
            failureReason = string.IsNullOrWhiteSpace(carryFailure)
                ? "carry commit failed after lease extraction"
                : carryFailure;
            markerPresenter.RefreshAt(position);
            return false;
        }

        repository.TrySetEquipmentWorldStateBySourceStack(
            carriedRecord.stackId,
            CombatEquipmentWorldState.Carried);
        markerPresenter.RefreshAt(position);
        return true;
    }

    public bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        out string failureReason) =>
        TryDepositCarriedItemsCore(
            actor,
            inventory,
            warehouse,
            null,
            out failureReason);

    public bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason) =>
        TryDepositCarriedItemsCore(
            actor,
            inventory,
            warehouse,
            ownerOperationIds,
            out failureReason);

    private bool TryDepositCarriedItemsCore(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || inventory == null || warehouse == null
            || !warehouse.HasWarehouseInventory
            || warehouse.Inventory == null)
        {
            failureReason = "warehouse unavailable";
            return false;
        }

        if (warehouse.Inventory.HasMassCapacityAuthority)
        {
            return TryDepositMassAdmittedCarriedItem(
                actor,
                inventory,
                warehouse,
                ownerOperationIds,
                out failureReason);
        }

        List<CharacterCarriedItemSaveData> carried = ownerOperationIds == null
            ? inventory.RemoveAllItemsForPhysicalTransfer()
            : inventory.RemoveItemsOwnedByOperationsForPhysicalTransfer(
                ownerOperationIds);
        if (carried.Count == 0)
        {
            failureReason = "nothing carried";
            return false;
        }

        HashSet<string> completedOperations = carried
            .Where(item => item != null
                && !string.IsNullOrWhiteSpace(item.ownerOperationId))
            .Select(item => item.ownerOperationId.Trim())
            .ToHashSet(StringComparer.Ordinal);
        foreach (string operationId in completedOperations)
        {
            quantityReservations.ReleaseByOwner(
                operationId,
                ItemReservationReleaseReason.Completed);
        }

        Vector2Int dropPosition = ResolveActorGridPosition(actor);
        bool depositedAny = false;
        foreach (CharacterCarriedItemSaveData item in carried)
        {
            if (item == null || item.quantity <= 0)
            {
                continue;
            }

            int remaining = item.quantity;
            if (TryGetWarehouseStockCategory(
                    item.itemId,
                    out StockCategory category)
                && warehouse.Inventory.Accepts(category)
                && string.IsNullOrWhiteSpace(item.itemInstanceId))
            {
                int deposited = warehouse.Inventory.GetAcceptableQuantity(
                    item.itemId,
                    remaining);
                if (deposited > 0)
                {
                    deposited = TrySpawnCarriedItem(
                        item,
                        deposited,
                        ResolveWarehouseStoragePosition(warehouse),
                        WorldItemStackState.Stored,
                        GetWarehouseStorageDestinationId(warehouse),
                        false,
                        default,
                        out _);
                }

                remaining -= deposited;
                depositedAny |= deposited > 0;
            }
            else if (PhysicalItemIds.TryGetEquipmentDefinitionId(
                         item.itemId,
                         out string equipmentId))
            {
                bool isCombatEquipment =
                    combatEquipmentCatalog.TryGet(equipmentId, out _);
                DungeonItemDefinition equipmentItem =
                    catalogProvider.GetDefinition(item.itemId);
                if (isCombatEquipment
                    && warehouse.Inventory.Accepts(
                        equipmentItem.StockCategory)
                    && warehouse.Inventory.GetAcceptableQuantity(
                        item.itemId,
                        remaining) == remaining
                    && remaining == 1
                    && TrySpawnCarriedItem(
                        item,
                        remaining,
                        ResolveWarehouseStoragePosition(warehouse),
                        WorldItemStackState.Stored,
                        GetWarehouseStorageDestinationId(warehouse),
                        false,
                        default,
                        out string storedStackId) == 1)
                {
                    if (repository.EquipmentInstances.TryGetValue(
                            item.itemInstanceId,
                            out CombatEquipmentInstance linked))
                    {
                        repository.TryLinkEquipmentToStack(
                            linked.instanceId,
                            storedStackId,
                            CombatEquipmentWorldState.Stored);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Carried equipment '{item.itemInstanceId}' has no item repository state.");
                    }

                    gameEventBus.Publish(
                        new EquipmentStoredEvent(equipmentId, 1));
                    depositedAny = true;
                    remaining = 0;
                }
                else if (!isCombatEquipment)
                {
                    gameEventBus.Publish(
                        new EquipmentStoredEvent(equipmentId, remaining));
                    depositedAny |= remaining > 0;
                    remaining = 0;
                }
            }

            if (remaining > 0)
            {
                bool exactRouteRemainder =
                    FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                        item.components);
                TrySpawnCarriedItem(
                    item,
                    remaining,
                    dropPosition,
                    WorldItemStackState.Loose,
                    exactRouteRemainder
                        ? GetWarehouseStorageDestinationId(warehouse)
                        : string.Empty,
                    exactRouteRemainder,
                    exactRouteRemainder
                        ? ResolveWarehouseStoragePosition(warehouse)
                        : default,
                    out _);
            }
        }

        if (!depositedAny)
        {
            failureReason = "warehouse rejected carried items";
        }

        return depositedAny;
    }

    private bool TryDepositMassAdmittedCarriedItem(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason)
    {
        failureReason = string.Empty;
        string[] owners = (ownerOperationIds ?? Array.Empty<string>())
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        CharacterCarriedItemSaveData[] preview = inventory.Items
            .Where(item => item != null
                && item.quantity > 0
                && owners.Contains(
                    item.ownerOperationId?.Trim() ?? string.Empty,
                    StringComparer.Ordinal))
            .ToArray();
        if (owners.Length != 1
            || preview.Length != 1
            || !repository.HaulDeliveryIntents.TryCapture(
                owners[0],
                out HaulDeliveryIntentSaveData intent)
            || !warehouseService.TryValidateHaulAdmission(
                intent,
                preview[0],
                warehouse,
                out WarehouseHaulAdmissionSaveData admission,
                out failureReason))
        {
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                failureReason = "mass warehouse requires one exact admitted haul lot";
            }
            return false;
        }

        List<CharacterCarriedItemSaveData> removed =
            inventory.RemoveItemsOwnedByOperationsForPhysicalTransfer(owners);
        if (removed.Count != 1)
        {
            failureReason = "admitted carried lot changed before deposit";
            return false;
        }

        CharacterCarriedItemSaveData carried = removed[0];
        int deposited = TrySpawnCarriedItem(
            carried,
            carried.quantity,
            ResolveWarehouseStoragePosition(warehouse),
            WorldItemStackState.Stored,
            GetWarehouseStorageDestinationId(warehouse),
            false,
            default,
            out string storedStackId);
        string publishedStackId = !string.IsNullOrWhiteSpace(storedStackId)
            ? storedStackId.Trim()
            : carried.carriedStackId?.Trim() ?? string.Empty;
        WorldItemStackRecord storedRecord = null;
        bool publishedExactPhysicalLot = deposited == carried.quantity
            && publishedStackId.Length > 0
            && repository.RecordsById.TryGetValue(
                publishedStackId,
                out storedRecord)
            && storedRecord != null
            && storedRecord.state == WorldItemStackState.Stored
            && string.Equals(
                storedRecord.destinationId,
                GetWarehouseStorageDestinationId(warehouse),
                StringComparison.Ordinal);
        if (publishedExactPhysicalLot)
        {
            // An outbound Stored stack retains its source warehouse while it
            // is routed and carried. Final publication must sever that route;
            // otherwise the gram index continues charging the source owner.
            storedRecord.sourceStorageDestinationId = string.Empty;
            repository.MarkChanged();
        }
        if (!publishedExactPhysicalLot
            || !warehouseService.TryCommitHaulAdmission(
                admission,
                owners[0],
                out failureReason))
        {
            RestoreMassDepositCarry(actor, inventory, carried);
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                failureReason = "admitted physical warehouse publication failed";
            }
            return false;
        }

        if (PhysicalItemIds.TryGetEquipmentDefinitionId(
                carried.itemId,
                out string equipmentDefinitionId)
            && !string.IsNullOrWhiteSpace(carried.itemInstanceId)
            && combatEquipmentCatalog.TryGet(equipmentDefinitionId, out _))
        {
            if (!repository.TrySetEquipmentWorldStateBySourceStack(
                    publishedStackId,
                    CombatEquipmentWorldState.Stored))
            {
                throw new InvalidOperationException(
                    $"Mass-admitted equipment '{carried.itemInstanceId}' lost its stored world-state authority.");
            }
            gameEventBus.Publish(new EquipmentStoredEvent(equipmentDefinitionId, 1));
        }

        quantityReservations.ReleaseByOwner(
            owners[0],
            ItemReservationReleaseReason.Completed);
        return true;
    }

    public bool TryTakeReservedRetailLots(
        string leaseId,
        int quantity,
        int saleItemId,
        string expectedItemDefinitionId,
        string operationId,
        CharacterCarryInventory carryInventory,
        out ReservedRetailStockTransferReceipt receipt,
        out DomainFailure failure)
    {
        receipt = null;
        failure = DomainFailure.None;
        string expectedItemId = expectedItemDefinitionId ?? string.Empty;
        string normalizedOperation = operationId ?? string.Empty;
        if (saleItemId < 0
            || quantity <= 0
            || expectedItemId.Length == 0
            || normalizedOperation.Length == 0
            || carryInventory == null
            || !string.Equals(expectedItemId, expectedItemId.Trim(), StringComparison.Ordinal)
            || !string.Equals(normalizedOperation, normalizedOperation.Trim(), StringComparison.Ordinal)
            || !quantityReservations.Revalidate(
                leaseId,
                out ItemQuantityLease lease,
                out failure)
            || lease.remainingQuantity < quantity)
        {
            if (!failure.IsFailure)
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-invalid-request");
            }
            return false;
        }

        List<(WorldItemStackRecord record, int quantity)> removals = new();
        int remaining = quantity;
        foreach (ItemLeaseSlice slice in lease.slices)
        {
            if (slice == null || slice.quantity <= 0 || remaining <= 0)
            {
                continue;
            }
            if (!repository.RecordsById.TryGetValue(
                    slice.stackId,
                    out WorldItemStackRecord record)
                || record == null
                || !string.Equals(record.itemId, expectedItemId, StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-source-exact-lot-mismatch",
                    slice.stackId ?? string.Empty);
                return false;
            }

            int take = Mathf.Min(remaining, slice.quantity);
            if (!string.IsNullOrEmpty(record.itemInstanceId)
                && (record.quantity != 1 || take != 1))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-unique-quantity-invalid",
                    record.stackId);
                return false;
            }
            removals.Add((record, take));
            remaining -= take;
        }
        if (remaining > 0)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationQuantityUnavailable,
                lease.leaseId);
            return false;
        }

        List<RetailStockLotSnapshot> lots = new();
        List<RetailStockSourceUndo> undo = new();
        CharacterCarriedItemSaveData[] carriedPreview = carryInventory.Items
            .Where(item => item != null
                && item.quantity > 0
                && string.Equals(
                    item.ownerOperationId,
                    normalizedOperation,
                    StringComparison.Ordinal))
            .ToArray();
        if (carriedPreview.Sum(item => item.quantity) != quantity
            || carriedPreview.Any(item => !string.Equals(
                item.itemId,
                expectedItemId,
                StringComparison.Ordinal)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-carry-quantity-mismatch",
                normalizedOperation);
            return false;
        }
        if (removals.Any(entry =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    entry.record.components))
            || carriedPreview.Any(item =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    item.components)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-prepared-output-route-protected",
                normalizedOperation);
            return false;
        }
        foreach ((WorldItemStackRecord record, int take) in removals)
        {
            int exactCarriedQuantity = carriedPreview
                .Where(item => string.Equals(
                        item.sourceStackId,
                        record.stackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.itemInstanceId,
                        record.itemInstanceId,
                        StringComparison.Ordinal))
                .Sum(item => item.quantity);
            if (exactCarriedQuantity != take)
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-carry-lot-mismatch",
                    record.stackId);
                return false;
            }
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                physicalMass,
                (ItemDefinitionId)record.itemId,
                record.itemInstanceId,
                record.components);
            long unitMassGrams = physicalMass.GetStackUnitMass(
                (ItemDefinitionId)record.itemId,
                subject).Value;
            lots.Add(new RetailStockLotSnapshot
            {
                saleItemId = saleItemId,
                itemDefinitionId = record.itemId,
                itemInstanceId = record.itemInstanceId,
                sourceStackId = record.stackId,
                quantity = take,
                unitMassGrams = unitMassGrams,
                sourceOperationId = $"{normalizedOperation}:stack:{record.stackId}",
                componentFingerprint = ItemStackSignature.Create(
                    record.itemId,
                    record.components),
                components = CaptureRetailComponents(record.components)
            });
            undo.Add(new RetailStockSourceUndo(
                CloneRecord(record),
                take,
                record.quantity == take));
        }

        foreach (RetailStockLotSnapshot uniqueLot in lots.Where(
            lot => !string.IsNullOrEmpty(lot.itemInstanceId)))
        {
            string uniqueFailure = string.Empty;
            if (retailStockPhysical == null
                || !retailStockPhysical.TryPrepareExistingUniqueLot(
                    uniqueLot,
                    out uniqueFailure))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-unique-prepare-failed",
                    uniqueFailure ?? string.Empty);
                return false;
            }
        }

        if (removals.Any(entry =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    entry.record.components))
            || carryInventory.Items.Any(item => item != null
                && item.quantity > 0
                && string.Equals(
                    item.ownerOperationId,
                    normalizedOperation,
                    StringComparison.Ordinal)
                && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    item.components)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-prepared-output-route-protected-precommit",
                normalizedOperation);
            return false;
        }

        List<CharacterCarriedItemSaveData> carriedUndo =
            carryInventory.RemoveItemsOwnedByOperations(new[] { normalizedOperation });
        if (carriedUndo.Sum(item => item?.quantity ?? 0) != quantity)
        {
            RestoreRetailCarryEntries(carryInventory, carriedUndo, out _);
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-carry-commit-mismatch",
                normalizedOperation);
            return false;
        }

        if (!quantityLeaseMutations.TryConsumeSlices(
                lease.leaseId,
                quantity,
                out _,
                out failure))
        {
            if (!RestoreRetailCarryEntries(
                    carryInventory,
                    carriedUndo,
                    out string carryRestoreFailure))
            {
                throw new InvalidOperationException(
                    "Retail lease commit failed and exact carried cargo could not be restored: "
                    + carryRestoreFailure);
            }
            return false;
        }

        HashSet<Vector2Int> touched = new();
        foreach ((WorldItemStackRecord record, int remove) in removals)
        {
            touched.Add(record.position);
            record.quantity -= remove;
            if (record.quantity <= 0)
            {
                repository.Remove(record);
            }
        }
        repository.MarkChanged();
        foreach (Vector2Int position in touched)
        {
            markerPresenter.RefreshAt(position);
        }
        List<RetailStockLotSnapshot> boundUniqueLots = new();
        foreach (RetailStockLotSnapshot uniqueLot in lots.Where(
            lot => !string.IsNullOrEmpty(lot.itemInstanceId)))
        {
            if (retailStockPhysical.TryBindExistingUniqueLot(
                    uniqueLot,
                    out string bindFailure))
            {
                boundUniqueLots.Add(uniqueLot);
                continue;
            }

            RestoreRetailSourceRecords(undo);
            foreach (RetailStockLotSnapshot bound in boundUniqueLots)
            {
                if (!retailStockPhysical.TryRestoreBoundUniqueLot(
                        bound,
                        CombatEquipmentWorldState.Carried,
                        out string uniqueRestoreFailure))
                {
                    throw new InvalidOperationException(
                        "Retail unique bind failed and a prior unique lot could not be restored: "
                        + uniqueRestoreFailure);
                }
            }
            if (!RestoreRetailCarryEntries(
                    carryInventory,
                    carriedUndo,
                    out string carryRestoreFailure))
            {
                throw new InvalidOperationException(
                    "Retail unique bind failed and exact carried cargo could not be restored: "
                    + carryRestoreFailure);
            }
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-unique-bind-failed",
                bindFailure ?? string.Empty);
            return false;
        }
        receipt = new ReservedRetailStockTransferReceipt(
            normalizedOperation,
            lots.AsReadOnly(),
            undo.AsReadOnly(),
            carryInventory,
            carriedUndo.AsReadOnly());
        return true;
    }

    public bool TryRollbackRetailTransfer(
        ReservedRetailStockTransferReceipt receipt,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (receipt == null || receipt.RolledBack || receipt.Undo == null)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-rollback-invalid");
            return false;
        }

        HashSet<Vector2Int> touched = new();
        foreach (RetailStockSourceUndo entry in receipt.Undo)
        {
            WorldItemStackRecord snapshot = entry.Snapshot;
            if (snapshot == null)
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-rollback-snapshot-missing");
                return false;
            }
            touched.Add(snapshot.position);
            if (entry.RecordWasRemoved)
            {
                if (repository.RecordsById.ContainsKey(snapshot.stackId))
                {
                    failure = new DomainFailure(
                        FailureCode.ItemTransferRequestFailed,
                        "retail-transfer-rollback-stack-conflict",
                        snapshot.stackId);
                    return false;
                }
                repository.Add(CloneRecord(snapshot));
            }
            else if (repository.RecordsById.TryGetValue(
                snapshot.stackId,
                out WorldItemStackRecord remaining))
            {
                remaining.quantity = checked(remaining.quantity + entry.RemovedQuantity);
            }
            else
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-rollback-source-missing",
                    snapshot.stackId);
                return false;
            }
        }
        foreach (RetailStockLotSnapshot uniqueLot in receipt.Lots.Where(
            lot => lot != null && !string.IsNullOrEmpty(lot.itemInstanceId)))
        {
            string uniqueRestoreFailure = string.Empty;
            if (retailStockPhysical == null
                || !retailStockPhysical.TryRestoreBoundUniqueLot(
                    uniqueLot,
                    CombatEquipmentWorldState.Carried,
                    out uniqueRestoreFailure))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    "retail-transfer-unique-rollback-failed",
                    uniqueRestoreFailure ?? string.Empty);
                return false;
            }
        }
        if (!RestoreRetailCarryEntries(
                receipt.CarryInventory,
                receipt.CarriedUndo,
                out string restoreFailure))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                "retail-transfer-carry-rollback-failed",
                restoreFailure ?? string.Empty);
            return false;
        }
        receipt.RolledBack = true;
        repository.MarkChanged();
        foreach (Vector2Int position in touched)
        {
            markerPresenter.RefreshAt(position);
        }
        return true;
    }

    private bool RestoreRetailCarryEntries(
        CharacterCarryInventory carryInventory,
        IReadOnlyList<CharacterCarriedItemSaveData> carriedEntries,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (carryInventory == null)
        {
            failureReason = "retail-transfer-carry-authority-missing";
            return false;
        }
        foreach (CharacterCarriedItemSaveData carried in
            carriedEntries ?? Array.Empty<CharacterCarriedItemSaveData>())
        {
            if (!carryInventory.TryAddLeasedPartialStack(
                    carried.carriedStackId,
                    carried.sourceStackId,
                    carried.ownerOperationId,
                    carried.itemInstanceId,
                    carried.itemId,
                    carried.quantity,
                    catalogProvider,
                    haulingSettingsProvider,
                    carried.wasteOrigin,
                    carried.contamination,
                    carried.components,
                    out int restored,
                    out string restoreFailure)
                || restored != carried.quantity)
            {
                failureReason = restoreFailure ?? string.Empty;
                return false;
            }
        }
        return true;
    }

    private void RestoreRetailSourceRecords(
        IReadOnlyList<RetailStockSourceUndo> undo)
    {
        foreach (RetailStockSourceUndo entry in undo ?? Array.Empty<RetailStockSourceUndo>())
        {
            WorldItemStackRecord snapshot = entry.Snapshot;
            if (snapshot == null)
            {
                continue;
            }
            if (entry.RecordWasRemoved)
            {
                if (!repository.RecordsById.ContainsKey(snapshot.stackId))
                {
                    repository.Add(CloneRecord(snapshot));
                }
            }
            else if (repository.RecordsById.TryGetValue(
                snapshot.stackId,
                out WorldItemStackRecord remaining))
            {
                remaining.quantity = checked(remaining.quantity + entry.RemovedQuantity);
            }
        }
        repository.MarkChanged();
    }

    private static List<RetailStockComponentSnapshot> CaptureRetailComponents(
        IEnumerable<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .OrderBy(component => component.componentTypeId, StringComparer.Ordinal)
            .Select(component => new RetailStockComponentSnapshot
            {
                componentTypeId = component.componentTypeId,
                schemaVersion = component.schemaVersion,
                affectsStacking = component.affectsStacking,
                values = (component.values ?? new List<ItemStateValueSaveData>())
                    .Where(value => value != null)
                    .OrderBy(value => value.key, StringComparer.Ordinal)
                    .Select(value => new RetailStockComponentValueSnapshot
                    {
                        key = value.key,
                        kind = (int)value.kind,
                        stringValue = value.stringValue,
                        integerValue = value.integerValue,
                        decimalValue = value.decimalValue,
                        booleanValue = value.booleanValue
                    })
                    .ToList()
            })
            .ToList();

    private static WorldItemStackRecord CloneRecord(WorldItemStackRecord source) => new()
    {
        stackId = source.stackId,
        itemInstanceId = source.itemInstanceId,
        itemId = source.itemId,
        quantity = source.quantity,
        state = source.state,
        position = source.position,
        reservedByPersistentId = source.reservedByPersistentId,
        reservedQuantity = source.reservedQuantity,
        reservationRevision = source.reservationRevision,
        destinationId = source.destinationId,
        aggregationCohortId = source.aggregationCohortId,
        sourceStorageDestinationId = source.sourceStorageDestinationId,
        hasDestinationPosition = source.hasDestinationPosition,
        destinationPosition = source.destinationPosition,
        forbidden = source.forbidden,
        sourceCharacterId = source.sourceCharacterId,
        sourceDisplayName = source.sourceDisplayName,
        sourceSpeciesTag = source.sourceSpeciesTag,
        sourceDeathReason = source.sourceDeathReason,
        emergencyButcheryAllowed = source.emergencyButcheryAllowed,
        wasteOrigin = source.wasteOrigin,
        contamination = source.contamination,
        components = (source.components ?? new List<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .Select(component => component.Clone())
            .ToList(),
        dropDisposition = source.dropDisposition,
        recoveryOwnerOperationId = source.recoveryOwnerOperationId,
        recoverySourceStackId = source.recoverySourceStackId,
        recoveryCarrierPersistentId = source.recoveryCarrierPersistentId,
        recoveryInterruptionKind = source.recoveryInterruptionKind,
        droppedAtGameTime = source.droppedAtGameTime,
        recoveryDeadlineGameTime = source.recoveryDeadlineGameTime
    };

    public bool TryCompleteTransitToWarehouse(
        ItemStackId stackId,
        string transitOwnerId,
        IWarehouseFacility warehouse,
        out WarehouseMassAdmissionReceipt receipt,
        out DomainFailure failure)
    {
        receipt = default;
        failure = DomainFailure.None;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (warehouseMassAdmission == null)
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionRequestInvalid,
                ownerId,
                "mass-admission-service-missing");
            return false;
        }

        if (!stackId.IsValid
            || ownerId.Length == 0
            || warehouse == null
            || warehouse.Inventory == null
            || !warehouse.HasWarehouseInventory
            || !warehouse.PersistentInstanceId.IsValid
            || !warehouse.Inventory.HasMassCapacityAuthority
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.state != WorldItemStackState.InTransit
            || !string.Equals(
                record.destinationId,
                ownerId,
                StringComparison.Ordinal)
            || !catalogProvider.TryGetDefinition(
                record.itemId,
                out DungeonItemDefinition definition)
            || !warehouse.Inventory.Accepts(definition.StockCategory))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorDestinationUnavailable,
                stackId.Value,
                warehouse?.PersistentInstanceId.Value ?? string.Empty);
            return false;
        }

        BuildingInstanceId warehouseId = warehouse.PersistentInstanceId;
        long expectedCapacityRevision = warehouseMassAdmission
            .GetWarehouseCapacityRevision(warehouseId);
        string operationId = ownerId
            + ":warehouse:"
            + warehouseId.Value
            + ":revision:"
            + expectedCapacityRevision.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        ItemDefinitionId itemId = new(record.itemId);
        PhysicalItemMassSubject massSubject = warehouseMassAdmission
            .PrepareMassSubject(
                itemId,
                record.itemInstanceId,
                record.components);
        WarehouseMassAdmissionRequest request = new(
            warehouseId,
            operationId,
            itemId,
            record.itemInstanceId,
            ItemReservationSignature.Create(record.itemId, record.components),
            record.quantity,
            expectedCapacityRevision,
            warehouseMassAdmission.CatalogRevision,
            repository.ItemStackVersion,
            massSubject);
        if (!warehouseMassAdmission.TryReserve(
                request,
                out WarehouseMassAdmissionToken token,
                out failure))
        {
            return false;
        }

        if (token.AcceptedQuantity != record.quantity)
        {
            if (!warehouseMassAdmission.TryRelease(
                    token.TokenId,
                    WarehouseMassAdmissionReleaseReason.TransactionRollback,
                    out DomainFailure releaseFailure))
            {
                failure = releaseFailure;
                return false;
            }

            failure = new DomainFailure(
                FailureCode.WarehouseMassCapacityUnavailable,
                warehouseId.Value,
                record.itemId,
                token.AcceptedQuantity.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                record.quantity.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            return false;
        }

        Vector2Int previousPosition = record.position;
        WorldItemStackState previousState = record.state;
        string previousDestinationId = record.destinationId;
        string previousSourceStorageDestinationId =
            record.sourceStorageDestinationId;
        bool previousHasDestinationPosition = record.hasDestinationPosition;
        Vector2Int previousDestinationPosition = record.destinationPosition;
        string previousReservedByPersistentId = record.reservedByPersistentId;

        Vector2Int warehousePosition = warehouse is BuildableObject building
            ? building.centerPos
            : Vector2Int.zero;
        repository.Relocate(record, warehousePosition);
        record.state = WorldItemStackState.Stored;
        record.destinationId = WarehouseStorageIdentity
            .RequireDestinationId(warehouse);
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.MarkChanged();
        markerPresenter.RefreshAt(previousPosition);
        markerPresenter.RefreshAt(warehousePosition);

        string commitId = operationId + ":commit";
        if (warehouseMassAdmission.TryCommit(
                token.TokenId,
                commitId,
                out receipt,
                out failure))
        {
            return true;
        }

        repository.Relocate(record, previousPosition);
        record.state = previousState;
        record.destinationId = previousDestinationId;
        record.sourceStorageDestinationId = previousSourceStorageDestinationId;
        record.hasDestinationPosition = previousHasDestinationPosition;
        record.destinationPosition = previousDestinationPosition;
        record.reservedByPersistentId = previousReservedByPersistentId;
        repository.MarkChanged();
        markerPresenter.RefreshAt(warehousePosition);
        markerPresenter.RefreshAt(previousPosition);
        if (warehouseMassAdmission.TryGetStatus(
                token.TokenId,
                out WarehouseMassAdmissionStatusSnapshot status)
            && status.Status == WarehouseMassAdmissionTokenStatus.Reserved)
        {
            warehouseMassAdmission.TryRelease(
                token.TokenId,
                WarehouseMassAdmissionReleaseReason.TransactionRollback,
                out _);
        }
        return false;
    }

    private void RestoreMassDepositCarry(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        CharacterCarriedItemSaveData item)
    {
        string restoreFailure = "carried item missing";
        int restored = 0;
        string actorId = characterIdRegistry.GetOrAssignPersistentId(actor);
        Vector2Int actorPosition = ResolveActorGridPosition(actor);
        if (!string.IsNullOrWhiteSpace(item?.carriedStackId)
            && repository.RecordsById.TryGetValue(
                item.carriedStackId,
                out WorldItemStackRecord record)
            && record != null)
        {
            repository.Relocate(record, actorPosition);
            record.state = WorldItemStackState.Carried;
            record.destinationId = actorId;
            record.hasDestinationPosition = false;
            record.destinationPosition = default;
            repository.MarkChanged();
        }

        if (item == null
            || !inventory.TryAddLeasedPartialStack(
                item.carriedStackId,
                item.sourceStackId,
                item.ownerOperationId,
                item.itemInstanceId,
                item.itemId,
                item.quantity,
                catalogProvider,
                haulingSettingsProvider,
                item.wasteOrigin,
                item.contamination,
                item.components,
                out restored,
                out restoreFailure)
            || restored != item.quantity)
        {
            throw new InvalidOperationException(
                $"Mass-admitted warehouse deposit rollback failed: {restoreFailure}");
        }
    }

    public bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || inventory == null)
        {
            failureReason = "carrier unavailable";
            return false;
        }
        if (inventory.Items.Any(item => item != null
                && item.quantity > 0
                && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    item.components)))
        {
            failureReason = "prepared-output-route-protected-contextless-drop";
            return false;
        }

        return TryDropRemovedCarriedItems(
            actor,
            inventory,
            inventory.RemoveAllItems(),
            default,
            out failureReason);
    }

    public bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || inventory == null)
        {
            failureReason = "carrier unavailable";
            return false;
        }
        HashSet<string> owners = new(
            (ownerOperationIds ?? Array.Empty<string>())
                .Select(value => value?.Trim() ?? string.Empty)
                .Where(value => value.Length > 0),
            StringComparer.Ordinal);
        if (inventory.Items.Any(item => item != null
                && item.quantity > 0
                && owners.Contains(item.ownerOperationId?.Trim() ?? string.Empty)
                && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    item.components)))
        {
            failureReason = "prepared-output-route-protected-contextless-drop";
            return false;
        }

        return TryDropRemovedCarriedItems(
            actor,
            inventory,
            inventory.RemoveItemsOwnedByOperations(owners),
            default,
            out failureReason);
    }

    public bool TryDropCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IReadOnlyCollection<string> ownerOperationIds,
        HaulCarryDropContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || inventory == null || !context.IsValid)
        {
            failureReason = "carrier or typed recovery context unavailable";
            return false;
        }

        return TryDropRemovedCarriedItems(
            actor,
            inventory,
            inventory.RemoveItemsOwnedByOperationsForPhysicalTransfer(
                ownerOperationIds),
            context,
            out failureReason);
    }

    private bool TryDropRemovedCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        List<CharacterCarriedItemSaveData> carried,
        HaulCarryDropContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (carried.Count == 0)
        {
            return true;
        }

        Vector2Int dropPosition = ResolveActorGridPosition(actor);
        List<CharacterCarriedItemSaveData> failed = new();
        foreach (CharacterCarriedItemSaveData item in carried)
        {
            if (item == null || item.quantity <= 0)
                continue;

            int dropped = context.IsValid
                ? TryRelocateExactCarriedRecoveryDrop(
                    item,
                    dropPosition,
                    context,
                    out _)
                : TrySpawnCarriedItem(
                    item,
                    item.quantity,
                    dropPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    false,
                    default,
                    out _);
            if (dropped != item.quantity)
            {
                failed.Add(item);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.ownerOperationId))
            {
                quantityReservations.ReleaseByOwner(
                    item.ownerOperationId,
                    ItemReservationReleaseReason.Cancelled);
            }
        }

        if (failed.Count == 0)
        {
            markerPresenter.RefreshAt(dropPosition);
            return true;
        }

        CharacterCarryInventorySaveData restore = inventory.Capture();
        restore.items.AddRange(failed);
        inventory.Restore(restore);
        failureReason = $"failed to return {failed.Count} carried stack(s) to the world";
        return false;
    }

    private int TryRelocateExactCarriedRecoveryDrop(
        CharacterCarriedItemSaveData item,
        Vector2Int dropPosition,
        HaulCarryDropContext context,
        out string stackId)
    {
        stackId = string.Empty;
        string carriedId = item?.carriedStackId?.Trim() ?? string.Empty;
        if (item == null
            || item.quantity <= 0
            || carriedId.Length == 0
            || !context.IsValid
            || !repository.RecordsById.TryGetValue(
                carriedId,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity != item.quantity
            || record.state is not (WorldItemStackState.Carried
                or WorldItemStackState.InTransit)
            || !string.Equals(record.itemId, item.itemId, StringComparison.Ordinal)
            || !string.Equals(
                ItemStackSignature.Create(record.itemId, record.components),
                ItemStackSignature.Create(item.itemId, item.components),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(item.ownerOperationId)
            || string.IsNullOrWhiteSpace(item.sourceStackId))
        {
            return 0;
        }

        bool hasExactRouteCustody =
            FacilityOutputExactRouteCustodyCodec.TryRead(
                record.components,
                out FacilityOutputExactRouteCustodyMetadata custody)
            && custody.Phase ==
                FacilityOutputExactRouteCustodyPhase.Routable;
        HaulDeliveryIntentSaveData recoveryIntent = null;
        if (hasExactRouteCustody
            && (!repository.HaulDeliveryIntents.TryCapture(
                    item.ownerOperationId,
                    out recoveryIntent)
                || recoveryIntent == null
                || !ExactRouteRecoveryIntentMatches(
                    custody,
                    recoveryIntent)))
        {
            return 0;
        }

        repository.Relocate(record, dropPosition);
        record.state = WorldItemStackState.Loose;
        record.aggregationCohortId = string.Empty;
        record.sourceStorageDestinationId = string.Empty;
        if (hasExactRouteCustody)
        {
            record.destinationId = recoveryIntent.destinationId;
            record.hasDestinationPosition = true;
            record.destinationPosition = new Vector2Int(
                recoveryIntent.dropGridX,
                recoveryIntent.dropGridY);
        }
        else
        {
            record.destinationId = string.Empty;
            record.hasDestinationPosition = false;
            record.destinationPosition = default;
        }
        record.reservedByPersistentId = string.Empty;
        record.dropDisposition =
            WorldItemDropDisposition.TransientCarryRecoveryDrop;
        record.recoveryOwnerOperationId = item.ownerOperationId.Trim();
        record.recoverySourceStackId = item.sourceStackId.Trim();
        record.recoveryCarrierPersistentId = context.CarrierPersistentId;
        record.recoveryInterruptionKind = context.InterruptionKind;
        record.droppedAtGameTime = context.DroppedAtGameTime;
        record.recoveryDeadlineGameTime = context.RecoveryDeadlineGameTime;
        repository.MarkChanged();
        stackId = record.stackId;
        return item.quantity;
    }

    private bool ExactRouteRecoveryIntentMatches(
        FacilityOutputExactRouteCustodyMetadata custody,
        HaulDeliveryIntentSaveData intent)
    {
        if (intent == null
            || string.IsNullOrWhiteSpace(intent.destinationId))
        {
            return false;
        }
        if (!string.IsNullOrEmpty(custody.TargetDestinationId))
        {
            return string.Equals(
                intent.destinationId,
                custody.TargetDestinationId,
                StringComparison.Ordinal);
        }
        if (intent.destinationKind != WorldItemHaulDestinationKind.Warehouse
            || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return false;
        }

        Vector2Int savedDelivery = new(
            intent.deliveryGridX,
            intent.deliveryGridY);
        Vector2Int savedDrop = new(intent.dropGridX, intent.dropGridY);
        return WorldItemHaulDestinationAuthority.TryResolve(
                grid,
                worldRegistry,
                destinationClaims,
                WorldItemHaulDestinationKind.Warehouse,
                intent.destinationId,
                savedDrop,
                out WorldItemHaulDestinationAuthority.Resolution resolution,
                out _)
            && string.Equals(
                resolution.DestinationId,
                intent.destinationId,
                StringComparison.Ordinal)
            && resolution.DeliveryPosition == savedDelivery
            && resolution.DropPosition == savedDrop;
    }

    public bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        out string failureReason) =>
        TryDepositCarriedItemsToFacilityCore(
            actor,
            inventory,
            destinationPosition,
            destinationId,
            null,
            out failureReason);

    public bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason) =>
        TryDepositCarriedItemsToFacilityCore(
            actor,
            inventory,
            destinationPosition,
            destinationId,
            ownerOperationIds,
            out failureReason);

    private bool TryDepositCarriedItemsToFacilityCore(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        IReadOnlyCollection<string> ownerOperationIds,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || inventory == null)
        {
            failureReason = "carrier unavailable";
            return false;
        }

        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDestination))
        {
            failureReason = "destination missing";
            return false;
        }

        if (!gridSystemProvider.TryGetGrid(out Grid grid)
            || !WorldItemHaulDestinationAuthority.TryResolve(
                grid,
                worldRegistry,
                destinationClaims,
                WorldItemHaulDestinationKind.FacilityBuffer,
                normalizedDestination,
                destinationPosition,
                out _,
                out failureReason))
        {
            if (string.IsNullOrWhiteSpace(failureReason))
                failureReason = "facility destination authority unavailable";
            return false;
        }

        List<CharacterCarriedItemSaveData> carried = ownerOperationIds == null
            ? inventory.RemoveAllItemsForPhysicalTransfer()
            : inventory.RemoveItemsOwnedByOperationsForPhysicalTransfer(
                ownerOperationIds);
        if (carried.Count == 0)
        {
            failureReason = "nothing carried";
            return false;
        }

        bool depositedAny = false;
        foreach (CharacterCarriedItemSaveData item in carried)
        {
            if (item == null
                || item.quantity <= 0
                || string.IsNullOrWhiteSpace(item.itemId))
            {
                continue;
            }

            int spawned;
            if (item.quantity == 1
                && repository.EquipmentInstances.TryGetValue(
                    item.itemInstanceId,
                    out CombatEquipmentInstance linked)
                && TrySpawnCarriedItem(
                    item,
                    item.quantity,
                    destinationPosition,
                    WorldItemStackState.FacilityBuffer,
                    normalizedDestination,
                    true,
                    destinationPosition,
                    out string bufferStackId) == 1)
            {
                repository.TryLinkEquipmentToStack(
                    linked.instanceId,
                    bufferStackId,
                    CombatEquipmentWorldState.MaintenanceBuffer);
                spawned = 1;
            }
            else
            {
                ItemReservationPurpose purpose = ItemReservationPurpose.ProductionInput;
                string cohortId = string.Empty;
                if (quantityReservations.TryGetLeasesByOwner(
                        item.ownerOperationId,
                        out IReadOnlyList<ItemQuantityLease> activeLeases))
                {
                    ItemQuantityLease carriedLease = activeLeases.FirstOrDefault(lease =>
                        lease?.slices?.Any(slice => slice != null
                            && string.Equals(
                                slice.stackId,
                                item.carriedStackId,
                                StringComparison.Ordinal)) == true);
                    if (carriedLease != null)
                    {
                        purpose = carriedLease.purpose;
                        cohortId = carriedLease.aggregationCohortId;
                    }
                }
                if (string.IsNullOrWhiteSpace(cohortId))
                {
                    cohortId = purpose == ItemReservationPurpose.Meal
                        ? $"meal:{normalizedDestination}:{item.itemId}:unassigned"
                        : $"production:{normalizedDestination}:unassigned:{item.itemId}";
                }
                spawned = bufferAggregation.TryDepositAndAggregate(
                    item,
                    purpose,
                    cohortId,
                    normalizedDestination,
                    destinationPosition,
                    out _,
                    out _)
                    ? item.quantity
                    : TrySpawnCarriedItem(
                        item,
                        item.quantity,
                        destinationPosition,
                        WorldItemStackState.FacilityBuffer,
                        normalizedDestination,
                        true,
                        destinationPosition,
                        out _);
            }

            depositedAny |= spawned > 0;
        }

        if (!depositedAny)
        {
            failureReason = "facility rejected carried items";
        }

        return depositedAny;
    }

    public bool TryConsumeFacilityBuffer(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs,
        out string failureReason) =>
        ItemFacilityBufferTransaction.TryConsumeByCategory(
            destinationId,
            costs,
            debugRules,
            repository,
            catalogProvider,
            markerPresenter,
            quantityReservations,
            this,
            AllocateFacilityConsumptionOperationId(destinationId),
            out failureReason);

    public bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason) =>
        ItemFacilityBufferTransaction.TryConsumeByItem(
            destinationId,
            costs,
            debugRules,
            repository,
            markerPresenter,
            quantityReservations,
            this,
            AllocateFacilityConsumptionOperationId(destinationId),
            out failureReason);

    private string AllocateFacilityConsumptionOperationId(string destinationId) =>
        $"facility-consume:{destinationId?.Trim() ?? string.Empty}:{++facilityConsumptionSequence}";

    private bool TryWithdrawOutboundStoredStock(
        WorldItemStackRecord stack,
        int requested,
        out IWarehouseFacility warehouse,
        out StockCategory category,
        out int withdrawn,
        out string failureReason)
    {
        warehouse = null;
        category = default;
        withdrawn = 0;
        failureReason = string.Empty;
        if (!IsOutboundStoredStack(stack)
            || requested <= 0
            || !TryGetWarehouseStockCategory(stack.itemId, out category))
        {
            failureReason = "stored source unavailable";
            return false;
        }

        string sourceStorageDestinationId =
            stack.sourceStorageDestinationId.Trim();
        warehouse = GetWarehouses().FirstOrDefault(candidate =>
            candidate != null
            && candidate.Inventory != null
            && string.Equals(
                GetWarehouseStorageDestinationId(candidate),
                sourceStorageDestinationId,
                StringComparison.Ordinal));
        if (warehouse == null)
        {
            failureReason = "source warehouse unavailable";
            return false;
        }

        withdrawn = Mathf.Min(requested, stack.quantity);
        if (withdrawn <= 0)
        {
            failureReason = "warehouse stock unavailable";
            return false;
        }

        return true;
    }

    private IEnumerable<IWarehouseFacility> GetWarehouses()
    {
        foreach (IWarehouseFacility warehouse in worldRegistry.Warehouses)
        {
            if (warehouse != null
                && warehouse.HasWarehouseInventory
                && warehouse.Inventory != null)
            {
                yield return warehouse;
            }
        }
    }

    private int AddStoredWarehouseItems(
        IWarehouseFacility warehouse,
        string itemId,
        int amount,
        WasteOriginKind wasteOrigin = WasteOriginKind.Unknown,
        float contamination = 0f,
        IReadOnlyList<ItemInstanceComponentSaveData> components = null)
    {
        return warehouse == null
            || string.IsNullOrWhiteSpace(itemId)
            || amount <= 0
            ? 0
            : itemSpawner.Spawn(
                itemId,
                amount,
                ResolveWarehouseStoragePosition(warehouse),
                WorldItemStackState.Stored,
                GetWarehouseStorageDestinationId(warehouse),
                wasteOrigin: wasteOrigin,
                contamination: contamination,
                components: components);
    }

    private int TrySpawnCarriedItem(
        CharacterCarriedItemSaveData item,
        int quantity,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition,
        Vector2Int destinationPosition,
        out string stackId)
    {
        stackId = string.Empty;
        if (item == null || quantity <= 0 || string.IsNullOrWhiteSpace(item.itemId))
        {
            return 0;
        }

        string carriedId = item.carriedStackId?.Trim() ?? string.Empty;
        bool hasExactRouteCustody =
            FacilityOutputExactRouteCustodyCodec.HasAnyCustody(item.components);
        if (hasExactRouteCustody
            && (carriedId.Length == 0
                || !FacilityOutputExactRouteCustodyCodec.TryRead(
                    item.components,
                    out FacilityOutputExactRouteCustodyMetadata itemCustody)
                || itemCustody.Phase !=
                    FacilityOutputExactRouteCustodyPhase.Routable))
        {
            return 0;
        }
        if (carriedId.Length > 0
            && repository.RecordsById.TryGetValue(
                carriedId,
                out WorldItemStackRecord carriedRecord)
            && carriedRecord != null
            && carriedRecord.quantity >= quantity
            && carriedRecord.state is WorldItemStackState.Carried
                or WorldItemStackState.InTransit
            && string.Equals(
                carriedRecord.itemId,
                item.itemId,
                StringComparison.Ordinal))
        {
            if (hasExactRouteCustody
                && !string.Equals(
                    ItemStackSignature.Create(
                        carriedRecord.itemId,
                        carriedRecord.components),
                    ItemStackSignature.Create(item.itemId, item.components),
                    StringComparison.Ordinal))
            {
                return 0;
            }
            carriedRecord.wasteOrigin = item.wasteOrigin;
            carriedRecord.contamination = Mathf.Clamp(item.contamination, 0f, 100f);
            carriedRecord.components = (item.components
                    ?? new List<ItemInstanceComponentSaveData>())
                .Where(component => component != null)
                .Select(component => component.Clone())
                .ToList();
            if (carriedRecord.quantity > quantity)
            {
                if (hasExactRouteCustody)
                {
                    return TryPublishPartialExactRouteCustody(
                        item,
                        carriedRecord,
                        quantity,
                        position,
                        state,
                        destinationId,
                        hasDestinationPosition,
                        destinationPosition,
                        out stackId);
                }
                carriedRecord.quantity -= quantity;
                repository.MarkChanged();
                return itemSpawner.Spawn(
                    item.itemId,
                    quantity,
                    position,
                    state,
                    destinationId,
                    hasDestinationPosition,
                    destinationPosition,
                    wasteOrigin: item.wasteOrigin,
                    contamination: item.contamination,
                    components: item.components);
            }

            repository.Relocate(carriedRecord, position);
            carriedRecord.state = state;
            carriedRecord.destinationId = destinationId?.Trim() ?? string.Empty;
            if (state == WorldItemStackState.Stored)
            {
                carriedRecord.sourceStorageDestinationId = string.Empty;
                carriedRecord.aggregationCohortId = string.Empty;
            }
            carriedRecord.hasDestinationPosition = hasDestinationPosition;
            carriedRecord.destinationPosition = destinationPosition;
            if (state != WorldItemStackState.Loose)
                ClearTransientRecoveryMetadata(carriedRecord);
            repository.MarkChanged();
            markerPresenter.RefreshAt(position);
            stackId = carriedRecord.stackId;
            return quantity;
        }

        if (hasExactRouteCustody)
            return 0;

        ItemInstanceId instanceId = (ItemInstanceId)item.itemInstanceId;
        if (instanceId.IsValid)
        {
            return quantity == 1
                && itemSpawner.SpawnExistingUnique(
                    item.itemId,
                    instanceId,
                    position,
                    state,
                    destinationId,
                    hasDestinationPosition,
                    destinationPosition,
                    item.components,
                    out stackId)
                    ? 1
                    : 0;
        }

        return itemSpawner.Spawn(
            item.itemId,
            quantity,
            position,
            state,
            destinationId,
            hasDestinationPosition,
            destinationPosition,
            wasteOrigin: item.wasteOrigin,
            contamination: item.contamination,
            components: item.components);
    }

    private int TryPublishPartialExactRouteCustody(
        CharacterCarriedItemSaveData item,
        WorldItemStackRecord carriedRecord,
        int quantity,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition,
        Vector2Int destinationPosition,
        out string stackId)
    {
        stackId = string.Empty;
        if (item == null
            || carriedRecord == null
            || quantity <= 0
            || quantity >= carriedRecord.quantity
            || !TryPrepareCustodyExtraction(
                carriedRecord,
                quantity,
                out List<ItemInstanceComponentSaveData> remainderComponents,
                out List<ItemInstanceComponentSaveData> publishedComponents,
                out _))
        {
            return 0;
        }

        int originalQuantity = carriedRecord.quantity;
        List<ItemInstanceComponentSaveData> originalComponents =
            CloneComponents(carriedRecord.components);
        string publishedId = repository.AllocateStackId();
        WorldItemStackRecord published = CloneRecord(carriedRecord);
        published.stackId = publishedId;
        published.itemInstanceId = string.Empty;
        published.quantity = quantity;
        published.state = state;
        published.position = position;
        published.reservedByPersistentId = string.Empty;
        published.reservedQuantity = 0;
        published.reservationRevision = checked(
            published.reservationRevision + 1L);
        published.destinationId = destinationId?.Trim() ?? string.Empty;
        published.aggregationCohortId = string.Empty;
        published.sourceStorageDestinationId = string.Empty;
        published.hasDestinationPosition = hasDestinationPosition;
        published.destinationPosition = destinationPosition;
        published.components = publishedComponents;
        ClearTransientRecoveryMetadata(published);

        try
        {
            carriedRecord.quantity = checked(originalQuantity - quantity);
            carriedRecord.components = remainderComponents;
            repository.Add(published);
            item.components = CloneComponents(remainderComponents);
            repository.MarkChanged();
        }
        catch
        {
            if (repository.RecordsById.ContainsKey(publishedId))
                repository.Remove(published);
            carriedRecord.quantity = originalQuantity;
            carriedRecord.components = originalComponents;
            repository.MarkChanged();
            throw;
        }

        markerPresenter.RefreshAt(position);
        stackId = publishedId;
        return quantity;
    }

    private static void ClearTransientRecoveryMetadata(WorldItemStackRecord record)
    {
        if (record == null)
            return;
        record.dropDisposition = WorldItemDropDisposition.None;
        record.recoveryOwnerOperationId = string.Empty;
        record.recoverySourceStackId = string.Empty;
        record.recoveryCarrierPersistentId = string.Empty;
        record.recoveryInterruptionKind = WorldItemCarryInterruptionKind.None;
        record.droppedAtGameTime = 0d;
        record.recoveryDeadlineGameTime = 0d;
    }

    private bool TryValidateCapacityActorCarry(
        string actorPersistentId,
        string batchCommitId,
        Vector2Int physicalCell,
        ProductionCapacityRoutingDrainActorCarrySaveData expected,
        out WorldItemStackRecord record,
        out HaulDeliveryIntentSaveData intent,
        out string failureReason)
    {
        record = null;
        intent = null;
        failureReason = string.Empty;
        if (expected == null
            || !string.Equals(
                expected.actorPersistentId,
                actorPersistentId,
                StringComparison.Ordinal)
            || !IsCanonicalQuiescenceToken(expected.haulIntentOperationId)
            || !IsCanonicalQuiescenceToken(expected.routeOperationId)
            || !IsCanonicalQuiescenceToken(expected.carriedStackId)
            || !IsCanonicalQuiescenceToken(expected.sourceStackId)
            || expected.quantity <= 0
            || expected.massGrams <= 0L
            || !IsLowerSha256Token(expected.stackSignature)
            || !repository.RecordsById.TryGetValue(
                expected.carriedStackId,
                out record)
            || record == null
            || record.state is not (WorldItemStackState.Carried
                or WorldItemStackState.InTransit)
            || record.position != physicalCell
            || !string.Equals(
                record.destinationId,
                actorPersistentId,
                StringComparison.Ordinal)
            || !record.hasDestinationPosition
            || record.destinationPosition != physicalCell
            || record.quantity != expected.quantity
            || record.dropDisposition != WorldItemDropDisposition.None
            || !string.IsNullOrEmpty(record.recoveryOwnerOperationId)
            || !string.IsNullOrEmpty(record.recoverySourceStackId)
            || !string.IsNullOrEmpty(record.recoveryCarrierPersistentId)
            || record.recoveryInterruptionKind !=
                WorldItemCarryInterruptionKind.None
            || record.droppedAtGameTime != 0d
            || record.recoveryDeadlineGameTime != 0d
            || !string.Equals(
                ProductionCapacityRoutingDrainFingerprint
                    .CreateActorCarryStackSignature(
                        record.itemId,
                        record.itemInstanceId,
                        record.components),
                expected.stackSignature,
                StringComparison.Ordinal)
            || !FacilityOutputExactRouteCustodyCodec.TryRead(
                record.components,
                out FacilityOutputExactRouteCustodyMetadata custody)
            || custody.Phase != FacilityOutputExactRouteCustodyPhase.Routable
            || !string.Equals(
                custody.BatchCommitId,
                batchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                custody.RouteOperationId,
                expected.routeOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                custody.CurrentSourceStackId,
                expected.sourceStackId,
                StringComparison.Ordinal)
            || custody.Quantity != expected.quantity
            || custody.MassGrams != expected.massGrams)
        {
            failureReason = "capacity-routing-carried-physical-row-conflict:"
                + (expected?.carriedStackId ?? string.Empty);
            return false;
        }

        long actualMass;
        try
        {
            actualMass = GetPhysicalQuantityMass(
                record.itemId,
                record.itemInstanceId,
                CapturePhysicalBusinessComponents(record.components),
                record.quantity);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            failureReason = "capacity-routing-carried-mass-invalid:"
                + record.stackId + ":" + exception.Message;
            return false;
        }
        if (actualMass != expected.massGrams
            || !repository.HaulDeliveryIntents.TryCapture(
                expected.haulIntentOperationId,
                out intent)
            || intent == null
            || !string.Equals(
                intent.operationId,
                expected.haulIntentOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                intent.ownerCharacterId,
                actorPersistentId,
                StringComparison.Ordinal))
        {
            failureReason = "capacity-routing-carried-intent-or-mass-conflict:"
                + record.stackId;
            return false;
        }
        HaulDeliveryItemCommitmentSaveData commitment = intent.commitments?
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.carriedStackId,
                    expected.carriedStackId,
                    StringComparison.Ordinal));
        string readableSignature = ItemStackSignature.Create(
            record.itemId,
            record.components);
        if (commitment == null
            || !string.Equals(
                commitment.sourceStackId,
                expected.sourceStackId,
                StringComparison.Ordinal)
            || commitment.quantity != expected.quantity
            || !string.Equals(
                commitment.expectedStackSignature,
                readableSignature,
                StringComparison.Ordinal))
        {
            failureReason = "capacity-routing-carried-commitment-conflict:"
                + record.stackId;
            return false;
        }

        string custodyTarget = !string.IsNullOrEmpty(
                custody.CurrentTargetDestinationId)
            ? custody.CurrentTargetDestinationId
            : custody.TargetDestinationId;
        if (!string.Equals(
                intent.destinationId,
                custodyTarget,
                StringComparison.Ordinal)
            || !string.IsNullOrEmpty(custody.CurrentTargetDestinationId)
            && new Vector2Int(intent.dropGridX, intent.dropGridY)
                != custody.CurrentTargetPosition)
        {
            failureReason = "capacity-routing-carried-target-conflict:"
                + record.stackId;
            return false;
        }
        return true;
    }

    private static ProductionCapacityRoutingActorQuiesceReceiptSaveData
        CreateCapacityActorQuiescenceReceipt(
            ProductionCapacityRoutingDrainSaveData pending,
            ProductionCapacityRoutingActorQuiescenceRequest request,
            Vector2Int physicalCell,
            IEnumerable<string> expectedRowKeys,
            string prePhysicalFingerprint,
            string postPhysicalFingerprint)
    {
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt = new()
        {
            actorPersistentId = request.ActorPersistentId,
            batchCommitId = request.BatchCommitId,
            physicalCellX = physicalCell.x,
            physicalCellY = physicalCell.y,
            carriedRowKeys = expectedRowKeys
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            quantityLeaseIds = request.Plan.QuantityLeaseIds.ToList(),
            warehouseAdmissionTokenIds = request.Plan
                .WarehouseAdmissionTokenIds.ToList(),
            activePlanFingerprint = request.Plan.Fingerprint,
            prePhysicalFingerprint = prePhysicalFingerprint,
            postPhysicalFingerprint = postPhysicalFingerprint
        };
        receipt.receiptFingerprint = ProductionCapacityRoutingDrainFingerprint
            .CreateActorQuiesceReceiptFingerprint(
                pending.stepOperationId,
                pending.requestFingerprint,
                receipt);
        return receipt;
    }

    private static string CreateCapacityActorPhysicalFingerprint(
        IEnumerable<WorldItemStackRecord> source) =>
        ProductionCapacityRoutingActorPhysicalFingerprint.Create(source);

    private static bool IsCanonicalQuiescenceToken(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsLowerSha256Token(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static Vector2Int ResolveActorGridPosition(CharacterActor actor)
    {
        return actor != null ? actor.GetNowXY() : Vector2Int.zero;
    }

    private static string GetWarehouseStorageDestinationId(
        IWarehouseFacility warehouse)
    {
        return WarehouseStorageIdentity.RequireDestinationId(warehouse);
    }

    private static Vector2Int ResolveWarehouseStoragePosition(
        IWarehouseFacility warehouse)
    {
        return warehouse is BuildableObject building
            ? building.centerPos
            : Vector2Int.zero;
    }

    private static bool IsOutboundStoredStack(WorldItemStackRecord stack)
    {
        return stack != null
            && stack.state == WorldItemStackState.Stored
            && stack.hasDestinationPosition
            && !string.IsNullOrWhiteSpace(stack.destinationId)
            && !string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId);
    }

    private static bool IsCombatLoadoutDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && destinationId.StartsWith(
                WorldItemStackRuntime.CombatLoadoutDestinationPrefix,
                StringComparison.Ordinal);
    }

    private bool TryGetWarehouseStockCategory(
        string itemId,
        out StockCategory category)
    {
        if (catalogProvider.TryGetDefinition(
                itemId,
                out DungeonItemDefinition definition))
        {
            category = definition.StockCategory;
            return true;
        }

        category = default;
        return false;
    }

    private static void RestoreDirectPickupStack(
        WorldItemStackRecord stack)
    {
        if (stack == null
            || !IsCombatLoadoutDestination(stack.destinationId))
        {
            return;
        }

        stack.destinationId =
            stack.sourceStorageDestinationId ?? string.Empty;
        stack.sourceStorageDestinationId = string.Empty;
        stack.hasDestinationPosition = false;
        stack.destinationPosition = default;
    }
}
