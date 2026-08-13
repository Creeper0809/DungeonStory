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
    IReservedItemTransferService
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemHaulingSettingsProvider haulingSettingsProvider;
    private readonly ICharacterIdRegistry characterIdRegistry;
    private readonly ICharacterAiWorldRegistry worldRegistry;
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
    private long facilityConsumptionSequence;
    private long directConsumptionSequence;

    public ItemTransferService(
        WorldItemReadServices readServices,
        ICharacterIdRegistry characterIdRegistry,
        ICharacterAiWorldRegistry worldRegistry,
        ICombatEquipmentCatalog combatEquipmentCatalog,
        IGameEventBus gameEventBus,
        WorldItemRepository repository,
        IWorldItemSpawner itemSpawner,
        WorldItemWarehouseService warehouseService,
        IItemQuantityReservationService quantityReservations,
        IItemQuantityLeaseMutation quantityLeaseMutations,
        IBufferStackAggregationService bufferAggregation)
    {
        WorldItemReadServices reads = readServices
            ?? throw new ArgumentNullException(nameof(readServices));
        catalogProvider = reads.Catalog;
        haulingSettingsProvider = reads.HaulingSettings;
        this.characterIdRegistry = characterIdRegistry
            ?? throw new ArgumentNullException(nameof(characterIdRegistry));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
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

        if (transferredIdentity)
        {
            repository.Relocate(source, destination.Position);
            source.state = transitState;
            source.destinationId = destination.DestinationId;
            source.aggregationCohortId = lease.aggregationCohortId;
            source.hasDestinationPosition = destination.DestinationId.Length > 0;
            source.destinationPosition = destination.Position;
            source.sourceStorageDestinationId = string.Empty;
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
                lease.aggregationCohortId);
            source.quantity -= quantity;
            repository.Add(child);

            List<ItemLeaseSlice> replacements = BuildExtractionReplacements(
                lease,
                sourceId,
                extractedId,
                sourceSlice.expectedStackSignature,
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
        string aggregationCohortId) => new()
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
        components = (source.components ?? new List<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .Select(component => component.Clone())
            .ToList()
    };

    private static List<ItemLeaseSlice> BuildExtractionReplacements(
        ItemQuantityLease lease,
        string sourceStackId,
        string extractedStackId,
        string expectedSignature,
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
                    expectedStackSignature = slice.expectedStackSignature,
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
                    expectedStackSignature = expectedSignature,
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
            repository.Remove(target);
            itemSpawner.Spawn(
                target.itemId,
                quantity,
                position,
                state,
                sourceDestination);
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
            requested,
            catalogProvider,
            haulingSettingsProvider);
        if (accepted <= 0)
        {
            failureReason = "carry limit";
            return false;
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
            failureReason = extractionFailure.ToString();
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
            carriedRecord.destinationId = string.Empty;
            carriedRecord.aggregationCohortId = string.Empty;
            carriedRecord.hasDestinationPosition = false;
            carriedRecord.destinationPosition = default;
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

        List<CharacterCarriedItemSaveData> carried = inventory.RemoveAllItems();
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
                && string.IsNullOrWhiteSpace(item.itemInstanceId))
            {
                int deposited = Mathf.Min(
                    remaining,
                    warehouse.Inventory.RemainingCapacity);
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
                if (isCombatEquipment
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
                TrySpawnCarriedItem(
                    item,
                    remaining,
                    dropPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    false,
                    default,
                    out _);
            }
        }

        if (!depositedAny)
        {
            failureReason = "warehouse rejected carried items";
        }

        return depositedAny;
    }

    public bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
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

        List<CharacterCarriedItemSaveData> carried = inventory.RemoveAllItems();
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
            carriedRecord.wasteOrigin = item.wasteOrigin;
            carriedRecord.contamination = Mathf.Clamp(item.contamination, 0f, 100f);
            carriedRecord.components = (item.components
                    ?? new List<ItemInstanceComponentSaveData>())
                .Where(component => component != null)
                .Select(component => component.Clone())
                .ToList();
            if (carriedRecord.quantity > quantity)
            {
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
            carriedRecord.hasDestinationPosition = hasDestinationPosition;
            carriedRecord.destinationPosition = destinationPosition;
            repository.MarkChanged();
            markerPresenter.RefreshAt(position);
            stackId = carriedRecord.stackId;
            return quantity;
        }

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
