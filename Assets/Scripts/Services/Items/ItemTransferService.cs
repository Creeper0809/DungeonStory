using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class ItemTransferService :
    IItemTransferService,
    IReservedItemTransferService,
    IReservedRetailStockTransferService,
    ICarriedItemDropService,
    IMemoryErasureSealStoredCargoReturnService,
    IProductionCapacityRoutingActorQuiescence
{
    private readonly ProductionCapacityRoutingActorQuiescenceService actorQuiescence;
    private readonly ReservedRetailStockTransferService retailTransfers;
    private readonly WorldItemTransitService transit;
    private readonly WorldItemQuantityTransferService quantityTransfers;
    private readonly StoredCargoReturnService storedCargoReturns;
    private readonly ExactOwnedItemReleaseService ownedItemReleases;
    private readonly WorldItemTransferPhysicalProjection physicalProjection;
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
    private readonly WorldItemWarehouseService warehouseService;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly IItemQuantityLeaseMutation quantityLeaseMutations;
    private readonly IBufferStackAggregationService bufferAggregation;
#if UNITY_EDITOR
    public Func<int, bool> DebugFailBeforeCapacityActorQuiescenceMutation
    {
        get => actorQuiescence.DebugFailBeforeCapacityActorQuiescenceMutation;
        set => actorQuiescence.DebugFailBeforeCapacityActorQuiescenceMutation = value;
    }
    public Func<bool> DebugFailBeforeFacilityTransitAdmissionCommit
    {
        get => transit.DebugFailBeforeFacilityTransitAdmissionCommit;
        set => transit.DebugFailBeforeFacilityTransitAdmissionCommit = value;
    }
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
        IPhysicalItemRelocationService physicalItemRelocations,
        IWarehouseMassAdmissionService warehouseMassAdmission = null,
        IRetailStockPhysicalRuntime retailStockPhysical = null,
        IFacilityBufferMassAdmissionService
            facilityBufferMassAdmission = null)
    {
        WorldItemReadServices reads = readServices
            ?? throw new ArgumentNullException(nameof(readServices));
        catalogProvider = reads.Catalog;
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
        IDungeonDebugRuleQuery debugRules = reads.DebugRules;
        WorldItemQueryService itemQueries = reads.Queries;
        this.warehouseService = warehouseService
            ?? throw new ArgumentNullException(nameof(warehouseService));
        this.quantityReservations = quantityReservations
            ?? throw new ArgumentNullException(nameof(quantityReservations));
        this.quantityLeaseMutations = quantityLeaseMutations
            ?? throw new ArgumentNullException(nameof(quantityLeaseMutations));
        this.bufferAggregation = bufferAggregation
            ?? throw new ArgumentNullException(nameof(bufferAggregation));
        _ = physicalItemRelocations
            ?? throw new ArgumentNullException(nameof(physicalItemRelocations));
        physicalProjection = new WorldItemTransferPhysicalProjection(reads.Mass);
        actorQuiescence = new ProductionCapacityRoutingActorQuiescenceService(
            destinationClaims, gridSystemProvider, markerPresenter,
            quantityReservations, repository, worldRegistry, physicalProjection);
        retailTransfers = new ReservedRetailStockTransferService(
            catalogProvider, haulingSettingsProvider, markerPresenter, reads.Mass,
            quantityLeaseMutations, quantityReservations, repository, retailStockPhysical);
        transit = new WorldItemTransitService(
            catalogProvider, markerPresenter, repository,
            warehouseMassAdmission, facilityBufferMassAdmission);
        quantityTransfers = new WorldItemQuantityTransferService(
            catalogProvider,
            debugRules,
            itemQueries,
            markerPresenter,
            physicalProjection,
            quantityLeaseMutations,
            quantityReservations,
            repository);
        storedCargoReturns = new StoredCargoReturnService(
            catalogProvider,
            characterIdRegistry,
            markerPresenter,
            quantityReservations,
            repository,
            warehouseMassAdmission,
            worldRegistry);
        ownedItemReleases = new ExactOwnedItemReleaseService(
            destinationClaims,
            gridSystemProvider,
            physicalItemRelocations,
            repository,
            worldRegistry);
    }

    [GameplayInternalOnly(
        "Releases one exact owner-claimed whole stack to an existing legal outbound cell without changing its identity.",
        "SurgicalPartRuntime freshness expiry only")]
    public bool TryReleaseExactOwnedWholeStack(
        ExactOwnedItemReleaseRequest request,
        out PhysicalItemRelocationReceipt receipt,
        out string failureReason)
    =>
        ownedItemReleases.TryReleaseExactOwnedWholeStack(
            request,
            out receipt,
            out failureReason);

    [GameplayInternalOnly(
        "Atomically publishes one exact carried descendant vector and its durable actor receipt.",
        "Production capacity-routing destructive-drain participant only")]
    public ProductionCapacityRoutingActorQuiescenceResult
        TryQuiesceAtCurrentCell(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            ProductionCapacityRoutingActorQuiescenceRequest request)
    {
        return actorQuiescence.TryQuiesceAtCurrentCell(actor, inventory, request);
    }

    public bool TryVerifyDurableReceipt(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        ProductionCapacityRoutingDrainSaveData drain,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        out string failureReason)
    {
        return actorQuiescence.TryVerifyDurableReceipt(
            actor,
            inventory,
            drain,
            receipt,
            out failureReason);
    }

    public int ReleaseQuantityReservationsByOwner(
        string ownerOperationId,
        ItemReservationReleaseReason reason) =>
        quantityTransfers.ReleaseQuantityReservationsByOwner(
            ownerOperationId,
            reason);

    public bool RenewQuantityReservation(
        string leaseId,
        double requestedUntilGameSeconds,
        out DomainFailure failure) =>
        quantityTransfers.RenewQuantityReservation(
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
    =>
        quantityTransfers.TryReserveAvailableStackForDirectPickup(
            ownerCharacterId,
            ownerOperationId,
            purpose,
            stackId,
            quantity,
            out lease,
            out failure);

    public bool ReleaseQuantityReservation(
        string leaseId,
        ItemReservationReleaseReason reason) =>
        quantityTransfers.ReleaseQuantityReservation(
            leaseId,
            reason);

    public bool TryExtractReservedQuantity(
        string leaseId,
        int quantity,
        ItemTransitDestination destination,
        out ItemExtractionReceipt receipt,
        out DomainFailure failure)
    =>
        quantityTransfers.TryExtractReservedQuantity(
            leaseId,
            quantity,
            destination,
            out receipt,
            out failure);

    public bool TryConsumeReservedQuantity(
        string leaseId,
        int quantity,
        out DomainFailure failure)
    =>
        quantityTransfers.TryConsumeReservedQuantity(
            leaseId,
            quantity,
            out failure);

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
    =>
        quantityTransfers.TryConsumeStackQuantity(
            stackId,
            quantity,
            out consumed,
            out failure);

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

        // A component-bearing or unique output is one physical lot. Splitting
        // it through the definition-only spawner would either duplicate its
        // component identity or allocate a different item-instance ID. Reject
        // such a route before mutating any earlier candidate; full-lot moves
        // below preserve the original record in place.
        int preflightRemaining = amount;
        foreach (WorldItemStackRecord candidate in candidates)
        {
            if (preflightRemaining <= 0)
                break;
            int available = Mathf.Max(
                0,
                quantityReservations.GetAvailableQuantity(
                    new ItemStackId(candidate.stackId)));
            int planned = Mathf.Min(preflightRemaining, available);
            if (planned > 0
                && planned < candidate.quantity
                && (!string.IsNullOrEmpty(candidate.itemInstanceId)
                    || candidate.components?.Count > 0))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    normalizedItemId,
                    "stateful-facility-output-partial-route-forbidden");
                return false;
            }
            preflightRemaining -= planned;
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

            // A full-lot route is a relocation of the same physical record,
            // not a consume-and-respawn transform. Keeping the record preserves
            // unique item identity, stateful components and exact gram mass.
            if (moved == source.quantity)
            {
                source.state = WorldItemStackState.Loose;
                source.destinationId = targetId;
                source.sourceStorageDestinationId = string.Empty;
                source.hasDestinationPosition = targetId.Length > 0;
                source.destinationPosition = destinationPosition;
                repository.MarkChanged();
                routed += moved;
                remaining -= moved;
                markerPresenter.RefreshAt(sourcePosition);
                continue;
            }

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
                contamination: source.contamination,
                components: source.components);
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
        return transit.TryBeginTransit(
            stackId,
            expectedPosition,
            transitOwnerId,
            out stack,
            out failure);
    }

    public bool TryInspectStackForTransit(
        ItemStackId stackId,
        out ItemTransitStackSnapshot stack)
    {
        return transit.TryInspectStackForTransit(stackId, out stack);
    }

    public void CopyLoadableTransitStackIds(
        Vector2Int position,
        List<ItemStackId> destination)
    {
        transit.CopyLoadableTransitStackIds(position, destination);
    }

    public bool TryGetTransitStack(
        ItemStackId stackId,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack)
    {
        return transit.TryGetTransitStack(stackId, transitOwnerId, out stack);
    }

    public bool TryCompleteTransit(
        ItemStackId stackId,
        string transitOwnerId,
        WorldItemStackState destinationState,
        Vector2Int destinationPosition,
        string destinationId,
        out DomainFailure failure)
    {
        return transit.TryCompleteTransit(
            stackId,
            transitOwnerId,
            destinationState,
            destinationPosition,
            destinationId,
            out failure);
    }

    public bool TryCompleteTransitToFacilityBuffer(
        ItemStackId stackId,
        string transitOwnerId,
        Vector2Int destinationPosition,
        string destinationId,
        out FacilityBufferMassAdmissionReceipt receipt,
        out DomainFailure failure)
    {
        return transit.TryCompleteTransitToFacilityBuffer(
            stackId,
            transitOwnerId,
            destinationPosition,
            destinationId,
            out receipt,
            out failure);
    }

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
                exactMassGrams = physicalProjection.GetPhysicalQuantityMass(
                    record.itemId,
                    record.itemInstanceId,
                    WorldItemTransferMetadataRules.CapturePhysicalBusinessComponents(record.components),
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
                    WorldItemTransferLocationRules.ResolveActorGridPosition(actor),
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

    [GameplayInternalOnly(
        "An active memory-erasure order returns its one exact carried unit to the preserved source stack and warehouse through mass admission.",
        "WorldItemStackRuntime")]
    public bool TryReturnCarriedMemoryErasureSealToStoredSource(
        CharacterActor actor,
        CharacterCarryInventory carry,
        string ownerOperationId,
        out string failureReason)
    =>
        storedCargoReturns.TryReturnCarriedMemoryErasureSealToStoredSource(
            actor,
            carry,
            ownerOperationId,
            out failureReason);

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

        if (!warehouse.Inventory.HasMassCapacityAuthority)
        {
            failureReason = "warehouse has no positive gram-capacity authority";
            return false;
        }

        return TryDepositMassAdmittedCarriedItem(
            actor,
            inventory,
            warehouse,
            ownerOperationIds,
            out failureReason);
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
            WorldItemTransferLocationRules.ResolveWarehouseStoragePosition(warehouse),
            WorldItemStackState.Stored,
            WorldItemTransferLocationRules.GetWarehouseStorageDestinationId(warehouse),
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
                WorldItemTransferLocationRules.GetWarehouseStorageDestinationId(warehouse),
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
        return retailTransfers.TryTakeReservedRetailLots(
            leaseId,
            quantity,
            saleItemId,
            expectedItemDefinitionId,
            operationId,
            carryInventory,
            out receipt,
            out failure);
    }

    public bool TryRollbackRetailTransfer(
        ReservedRetailStockTransferReceipt receipt,
        out DomainFailure failure)
    {
        return retailTransfers.TryRollbackRetailTransfer(receipt, out failure);
    }

    public bool TryCompleteTransitToWarehouse(
        ItemStackId stackId,
        string transitOwnerId,
        IWarehouseFacility warehouse,
        out WarehouseMassAdmissionReceipt receipt,
        out DomainFailure failure)
    {
        return transit.TryCompleteTransitToWarehouse(
            stackId,
            transitOwnerId,
            warehouse,
            out receipt,
            out failure);
    }

    private void RestoreMassDepositCarry(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        CharacterCarriedItemSaveData item)
    {
        string restoreFailure = "carried item missing";
        int restored = 0;
        string actorId = characterIdRegistry.GetOrAssignPersistentId(actor);
        Vector2Int actorPosition = WorldItemTransferLocationRules.ResolveActorGridPosition(actor);
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

        Vector2Int dropPosition = WorldItemTransferLocationRules.ResolveActorGridPosition(actor);
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
        quantityTransfers.TryConsumeFacilityBuffer(
            destinationId,
            costs,
            out failureReason);

    public bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason) =>
        quantityTransfers.TryConsumeFacilityItemBuffer(
            destinationId,
            costs,
            out failureReason);

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
        warehouse = WorldItemTransferLocationRules.GetWarehouses(worldRegistry).FirstOrDefault(candidate =>
            candidate != null
            && candidate.Inventory != null
            && string.Equals(
                WorldItemTransferLocationRules.GetWarehouseStorageDestinationId(candidate),
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
                WorldItemTransferLocationRules.ResolveWarehouseStoragePosition(warehouse),
                WorldItemStackState.Stored,
                WorldItemTransferLocationRules.GetWarehouseStorageDestinationId(warehouse),
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
                WorldItemTransferMetadataRules.ClearTransientRecoveryMetadata(carriedRecord);
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
            || !quantityTransfers.TryPrepareCustodyExtraction(
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
            WorldItemTransferMetadataRules.CloneComponents(carriedRecord.components);
        string publishedId = repository.AllocateStackId();
        WorldItemStackRecord published = WorldItemTransferPhysicalProjection.CloneRecord(carriedRecord);
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
        WorldItemTransferMetadataRules.ClearTransientRecoveryMetadata(published);

        try
        {
            carriedRecord.quantity = checked(originalQuantity - quantity);
            carriedRecord.components = remainderComponents;
            repository.Add(published);
            item.components = WorldItemTransferMetadataRules.CloneComponents(remainderComponents);
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
