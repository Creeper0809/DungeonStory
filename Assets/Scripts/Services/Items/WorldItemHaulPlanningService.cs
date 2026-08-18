using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IWorldItemHaulPlanningService
{
    bool HasPendingPriorityWork { get; }
    bool HasAvailablePlan(CharacterActor actor);
    bool TryPreviewBestPlan(
        CharacterActor actor,
        out WorldItemHaulPlan plan,
        out string failureReason);
    bool TryReserveBestPlan(
        CharacterActor actor,
        out WorldItemHaulPlan plan,
        out string failureReason);
    bool TryReserveBestJob(
        CharacterActor actor,
        out WorldItemHaulJob job,
        out string failureReason);
}

public sealed class WorldItemHaulPlanningService : IWorldItemHaulPlanningService
{
    private const int MaximumPickupLegs = 6;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemHaulingSettingsProvider haulingSettingsProvider;
    private readonly ICharacterIdRegistry characterIdRegistry;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly WorldItemRepository repository;
    private readonly IItemQuantityReservationService reservationService;
    private readonly IFacilityBufferDestinationClaimQuery destinationClaims;
    private CharacterActor cachedAvailabilityActor;
    private Vector2Int cachedAvailabilityActorPosition;
    private int cachedAvailabilityHaulVersion = -1;
    private int cachedAvailabilityWarehouseVersion = -1;
    private int cachedAvailabilityTraversalVersion = -1;
    private long cachedAvailabilityDestinationClaimRevision = -1;
    private bool cachedAvailability;

    public bool HasPendingPriorityWork => repository.PrioritizedHaulStackIds
        .Any(stackId => repository.RecordsById.TryGetValue(
                stackId,
                out WorldItemStackRecord record)
            && record != null
            && record.quantity > 0);

    public WorldItemHaulPlanningService(
        IGridSystemProvider gridSystemProvider,
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider haulingSettingsProvider,
        ICharacterIdRegistry characterIdRegistry,
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        WorldItemRepository repository,
        IItemQuantityReservationService reservationService,
        IFacilityBufferDestinationClaimQuery destinationClaims)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.catalogProvider = catalogProvider
            ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.haulingSettingsProvider = haulingSettingsProvider
            ?? throw new ArgumentNullException(nameof(haulingSettingsProvider));
        this.characterIdRegistry = characterIdRegistry
            ?? throw new ArgumentNullException(nameof(characterIdRegistry));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.reservationService = reservationService
            ?? throw new ArgumentNullException(nameof(reservationService));
        this.destinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
    }

    public bool HasAvailablePlan(CharacterActor actor)
    {
        if (actor == null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return false;
        }

        if (!TryGetPlanningSearch(actor, grid, out GridPathSearchResult reachable))
        {
            // A broker deferral is not authoritative NoWork.  Do not cache it;
            // the Brain will ask again after the shared path budget advances.
            return false;
        }

        int haulVersion = repository.HaulJobVersion;
        int warehouseVersion = worldRegistry.WarehouseVersion;
        int traversalVersion = grid.TraversalVersion;
        long destinationClaimRevision = destinationClaims.Revision;
        Vector2Int actorPosition = actor.GetNowXY();
        if (ReferenceEquals(cachedAvailabilityActor, actor)
            && cachedAvailabilityActorPosition == actorPosition
            && cachedAvailabilityHaulVersion == haulVersion
            && cachedAvailabilityWarehouseVersion == warehouseVersion
            && cachedAvailabilityTraversalVersion == traversalVersion
            && cachedAvailabilityDestinationClaimRevision
                == destinationClaimRevision)
        {
            return cachedAvailability;
        }

        cachedAvailabilityActor = actor;
        cachedAvailabilityActorPosition = actorPosition;
        cachedAvailabilityHaulVersion = haulVersion;
        cachedAvailabilityWarehouseVersion = warehouseVersion;
        cachedAvailabilityTraversalVersion = traversalVersion;
        cachedAvailabilityDestinationClaimRevision = destinationClaimRevision;
        cachedAvailability = HasAvailablePlanCore(actor, grid, reachable);
        return cachedAvailability;
    }

    public bool TryPreviewBestPlan(
        CharacterActor actor,
        out WorldItemHaulPlan plan,
        out string failureReason)
    {
        return TryBuildBestPlan(actor, reserve: false, out plan, out failureReason);
    }

    public bool TryReserveBestPlan(
        CharacterActor actor,
        out WorldItemHaulPlan plan,
        out string failureReason)
    {
        return TryBuildBestPlan(actor, reserve: true, out plan, out failureReason);
    }

    public bool TryReserveBestJob(
        CharacterActor actor,
        out WorldItemHaulJob job,
        out string failureReason)
    {
        job = default;
        if (!TryBuildBestPlan(
                actor,
                reserve: true,
                out WorldItemHaulPlan plan,
                out failureReason))
        {
            return false;
        }

        WorldItemHaulPlanLeg leg = plan.PickupLegs[0];
        job = new WorldItemHaulJob(
            leg.Reservation.StackId,
            leg.ItemPosition,
            leg.PickupStandPosition,
            leg.Warehouse,
            leg.DeliveryPosition,
            leg.DestinationKind,
            leg.DestinationId,
            leg.DropPosition,
            useDropPosition: true,
            quantity: leg.Reservation.Quantity,
            leaseId: leg.Reservation.LeaseId,
            ownerOperationId: leg.Reservation.OwnerOperationId);
        return true;
    }

    private bool TryBuildBestPlan(
        CharacterActor actor,
        bool reserve,
        out WorldItemHaulPlan plan,
        out string failureReason)
    {
        plan = null;
        failureReason = string.Empty;
        if (actor == null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "no grid";
            return false;
        }

        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        if (inventory == null)
        {
            failureReason = "no carry inventory";
            return false;
        }

        string actorId = characterIdRegistry.GetOrAssignPersistentId(actor);
        if (!TryGetPlanningSearch(actor, grid, out GridPathSearchResult reachable))
        {
            failureReason = "path search deferred";
            return false;
        }
        HaulCandidate seed = FindSeedCandidate(
            grid,
            reachable,
            actor,
            inventory,
            actorId,
            out string priorityFailureReason);
        if (seed == null)
        {
            failureReason = string.IsNullOrWhiteSpace(priorityFailureReason)
                ? "no haulable stack"
                : priorityFailureReason;
            return false;
        }

        List<HaulCandidate> selected = SelectOpportunisticCandidates(
            grid,
            reachable,
            actor,
            inventory,
            actorId,
            seed,
            out float plannedWeight,
            out int expectedDetour);
        // A haul operation is one plan, never one actor-wide reusable owner.
        // Its durable identity is retained by the delivery intent across save.
        string ownerOperationId = reserve
            ? repository.AllocateHaulDeliveryOperationId(actorId)
            : string.Empty;
        Dictionary<string, ItemQuantityLease> leasesByStack =
            new(StringComparer.Ordinal);
        if (reserve)
        {
            ItemQuantityReservationRequest[] requests = selected
                .Select(candidate => new ItemQuantityReservationRequest(
                    new ItemStackId(candidate.Stack.stackId),
                    candidate.Quantity,
                    ItemReservationSignature.Create(
                        candidate.Stack.itemId,
                        candidate.Stack.components)))
                .ToArray();
            if (!reservationService.TryReserveBatch(
                    ownerOperationId,
                    actorId,
                    ItemReservationPurpose.Hauling,
                    $"haul:{seed.DestinationKind}:{seed.DestinationId}",
                    requests,
                    out IReadOnlyList<ItemQuantityLease> leases,
                    out _))
            {
                failureReason = "reservation changed";
                return false;
            }
            foreach (ItemQuantityLease lease in leases)
            {
                if (lease?.slices == null || lease.slices.Count != 1)
                    continue;
                leasesByStack[lease.slices[0].stackId] = lease;
            }
        }

        List<WorldItemHaulPlanLeg> pickupLegs =
            new List<WorldItemHaulPlanLeg>(selected.Count);
        List<WorldItemReservedStackQuantity> reservations =
            new List<WorldItemReservedStackQuantity>(selected.Count);
        foreach (HaulCandidate candidate in selected)
        {
            leasesByStack.TryGetValue(candidate.Stack.stackId, out ItemQuantityLease lease);
            WorldItemReservedStackQuantity itemReservation =
                new WorldItemReservedStackQuantity(
                    candidate.Stack.stackId,
                    candidate.Stack.itemId,
                    candidate.Quantity,
                    candidate.Stack.position,
                    candidate.DestinationKind,
                    candidate.DestinationId,
                    lease?.leaseId,
                    reserve ? ownerOperationId : string.Empty);
            reservations.Add(itemReservation);
            pickupLegs.Add(new WorldItemHaulPlanLeg(
                itemReservation,
                candidate.PickupStandPosition,
                candidate.Warehouse,
                candidate.DeliveryPosition,
                candidate.DropPosition));
        }

        if (reserve && !repository.HaulDeliveryIntents.TryRegisterPlan(
                ownerOperationId,
                actorId,
                seed.DestinationKind,
                seed.DestinationId,
                seed.DeliveryPosition,
                seed.DropPosition,
                out string intentFailure))
        {
            reservationService.ReleaseByOwner(
                ownerOperationId,
                ItemReservationReleaseReason.Cancelled);
            failureReason = intentFailure;
            return false;
        }

        plan = new WorldItemHaulPlan(
            pickupLegs,
            new[]
            {
                new WorldItemHaulPlanLeg(
                    reservations[0],
                    seed.PickupStandPosition,
                    seed.Warehouse,
                    seed.DeliveryPosition,
                    seed.DropPosition)
            },
            reservations,
            plannedWeight,
            expectedDetour,
            seed.DestinationKind,
            seed.DestinationId,
            isPriority: seed.IsPriority);
        return true;
    }

    private bool HasAvailablePlanCore(
        CharacterActor actor,
        Grid grid,
        GridPathSearchResult reachable)
    {
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        if (inventory == null)
        {
            return false;
        }

        string actorId = characterIdRegistry.GetOrAssignPersistentId(actor);
        IReadOnlyList<WorldItemStackRecord> stacks = GetHaulableStacks();
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackRecord stack = stacks[index];
            int acceptable = GetAcceptableQuantity(
                    inventory,
                    stack.itemId,
                    reservationService.GetAvailableQuantity(
                        new ItemStackId(stack.stackId)),
                    plannedWeight: 0f);
            if (!CanUseStack(stack, actorId)
                || acceptable <= 0
                || !TryResolvePickupStandCell(
                    grid,
                    reachable,
                    stack.position,
                    out _))
            {
                continue;
            }

            if (stack.hasDestinationPosition
                && !string.IsNullOrWhiteSpace(stack.destinationId))
            {
                if (WorldItemHaulDestinationAuthority.TryResolve(
                        grid,
                        worldRegistry,
                        destinationClaims,
                        WorldItemHaulDestinationKind.FacilityBuffer,
                        stack.destinationId,
                        stack.destinationPosition,
                        out _,
                        out _))
                {
                    return true;
                }

                continue;
            }

            if (HasAvailableWarehouseDestination(grid, stack)
                && ApplySurvivalTransitReserve(
                    stack,
                    WorldItemHaulDestinationKind.Warehouse,
                    acceptable) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAvailableWarehouseDestination(
        Grid grid,
        WorldItemStackRecord stack)
    {
        bool isStockItem = TryGetWarehouseStockCategory(
            stack.itemId,
            out StockCategory category);
        bool isEquipmentItem = PhysicalItemIds.TryGetEquipmentDefinitionId(
            stack.itemId,
            out _);
        if (!isStockItem && !isEquipmentItem)
        {
            return false;
        }

        IReadOnlyList<IWarehouseFacility> warehouses = worldRegistry.Warehouses;
        for (int index = 0; index < warehouses.Count; index++)
        {
            IWarehouseFacility candidate = warehouses[index];
            if (!IsUsableWarehouse(candidate)
                || (!isEquipmentItem
                    && !candidate.Inventory.CanStore(category, 1))
                || candidate is not BuildableObject building
                || building.isDestroy)
            {
                continue;
            }

            if (TryResolveDeliveryCell(grid, building, out _))
            {
                return true;
            }
        }

        return false;
    }

    private HaulCandidate FindSeedCandidate(
        Grid grid,
        GridPathSearchResult reachable,
        CharacterActor actor,
        CharacterCarryInventory inventory,
        string actorId,
        out string priorityFailureReason)
    {
        priorityFailureReason = string.Empty;
        HaulCandidate best = null;
        foreach (WorldItemStackRecord stack in GetHaulableStacks())
        {
            if (!CanUseStack(stack, actorId))
            {
                continue;
            }

            bool isPriority = repository.PrioritizedHaulStackIds.Contains(stack.stackId);
            if (!TryBuildCandidate(
                    grid,
                    reachable,
                    actor,
                    inventory,
                    stack,
                    plannedWeight: 0f,
                    out HaulCandidate candidate,
                    out string candidateFailureReason))
            {
                if (isPriority && string.IsNullOrWhiteSpace(priorityFailureReason))
                {
                    priorityFailureReason =
                        $"priority stack {stack.stackId}: {candidateFailureReason}";
                }

                continue;
            }

            if (best == null
                || (candidate.IsPriority && !best.IsPriority)
                || candidate.IsPriority == best.IsPriority
                    && candidate.Score > best.Score)
            {
                best = candidate;
            }
        }

        return best;
    }

    private List<HaulCandidate> SelectOpportunisticCandidates(
        Grid grid,
        GridPathSearchResult reachable,
        CharacterActor actor,
        CharacterCarryInventory inventory,
        string actorId,
        HaulCandidate seed,
        out float plannedWeight,
        out int expectedDetour)
    {
        List<HaulCandidate> selected = new List<HaulCandidate> { seed };
        HashSet<string> selectedIds =
            new HashSet<string>(StringComparer.Ordinal) { seed.Stack.stackId };
        plannedWeight = seed.TotalWeight;
        expectedDetour = 0;
        int directDistance = Manhattan(actor.GetNowXY(), seed.PickupStandPosition)
            + Manhattan(seed.PickupStandPosition, seed.DeliveryPosition);
        int detourLimit = Mathf.Min(
            4,
            Mathf.Max(1, Mathf.RoundToInt(directDistance * 0.15f)));

        List<HaulCandidate> opportunistic = new List<HaulCandidate>();
        foreach (WorldItemStackRecord stack in GetHaulableStacks())
        {
            if (selectedIds.Contains(stack.stackId)
                || !CanUseStack(stack, actorId)
                || !TryBuildCandidate(
                    grid,
                    reachable,
                    actor,
                    inventory,
                    stack,
                    plannedWeight: 0f,
                    out HaulCandidate candidate,
                    out _)
                || !HasSameDestination(seed, candidate))
            {
                continue;
            }

            int detour = GetDetour(seed, candidate);
            if (detour <= detourLimit)
            {
                candidate.DetourCost = Mathf.Max(0, detour);
                opportunistic.Add(candidate);
            }
        }

        foreach (HaulCandidate candidate in opportunistic
            .OrderBy(candidate => candidate.DetourCost)
            .ThenBy(candidate => Manhattan(
                seed.PickupStandPosition,
                candidate.PickupStandPosition))
            .ThenByDescending(candidate => candidate.Score))
        {
            if (selected.Count >= MaximumPickupLegs)
            {
                break;
            }

            if (!TryBuildCandidate(
                    grid,
                    reachable,
                    actor,
                    inventory,
                    candidate.Stack,
                    plannedWeight,
                    out HaulCandidate refreshed,
                    out _)
                || refreshed.Quantity <= 0)
            {
                continue;
            }

            refreshed.DetourCost = candidate.DetourCost;
            selected.Add(refreshed);
            selectedIds.Add(refreshed.Stack.stackId);
            plannedWeight += refreshed.TotalWeight;
            expectedDetour += refreshed.DetourCost;

            float maxAllowed = Mathf.Max(
                0.01f,
                inventory.GetMaxAllowedWeight(haulingSettingsProvider));
            float actualWeight = inventory.GetCurrentWeight(catalogProvider);
            if ((actualWeight + plannedWeight) / maxAllowed >= 0.98f)
            {
                break;
            }
        }

        return selected;
    }

    private bool TryBuildCandidate(
        Grid grid,
        GridPathSearchResult reachable,
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemStackRecord stack,
        float plannedWeight,
        out HaulCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (grid == null
            || actor == null
            || inventory == null)
        {
            failureReason = "invalid planning context";
            return false;
        }

        if (!CanHaul(stack))
        {
            failureReason = "stack is not haulable";
            return false;
        }

        int acceptable = GetAcceptableQuantity(
            inventory,
            stack.itemId,
            reservationService.GetAvailableQuantity(
                new ItemStackId(stack.stackId)),
            plannedWeight);
        if (acceptable <= 0)
        {
            failureReason = "carry capacity exhausted";
            return false;
        }

        if (!TryResolvePickupStandCell(
                grid,
                reachable,
                stack.position,
                out Vector2Int pickupStand))
        {
            failureReason = $"no pickup stand near {stack.position}";
            return false;
        }

        IWarehouseFacility warehouse = null;
        Vector2Int deliveryCell;
        Vector2Int dropCell;
        WorldItemHaulDestinationKind destinationKind;
        string destinationId = stack.destinationId ?? string.Empty;
        if (stack.hasDestinationPosition && !string.IsNullOrWhiteSpace(destinationId))
        {
            if (!WorldItemHaulDestinationAuthority.TryResolve(
                    grid,
                    worldRegistry,
                    destinationClaims,
                    WorldItemHaulDestinationKind.FacilityBuffer,
                    destinationId,
                    stack.destinationPosition,
                    out WorldItemHaulDestinationAuthority.Resolution destination,
                    out failureReason))
            {
                return false;
            }

            warehouse = destination.Warehouse;
            deliveryCell = destination.DeliveryPosition;
            dropCell = destination.DropPosition;
            destinationKind = destination.Kind;
            destinationId = destination.DestinationId;
        }
        else if (TryFindWarehouse(
                     grid,
                     reachable,
                     stack,
                     out warehouse,
                     out deliveryCell))
        {
            dropCell = deliveryCell;
            destinationKind = WorldItemHaulDestinationKind.Warehouse;
            destinationId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
            if (!WorldItemHaulDestinationAuthority.TryResolve(
                    grid,
                    worldRegistry,
                    destinationClaims,
                    destinationKind,
                    destinationId,
                    dropCell,
                    out WorldItemHaulDestinationAuthority.Resolution destination,
                    out failureReason))
            {
                return false;
            }
            warehouse = destination.Warehouse;
            deliveryCell = destination.DeliveryPosition;
            dropCell = destination.DropPosition;
        }
        else
        {
            failureReason = "no reachable destination";
            return false;
        }

        acceptable = ApplySurvivalTransitReserve(
            stack,
            destinationKind,
            acceptable);
        if (acceptable <= 0)
        {
            failureReason = "survival transit reserve protected";
            return false;
        }

        DungeonItemDefinition definition = catalogProvider.GetDefinition(stack.itemId);
        int distance = Manhattan(actor.GetNowXY(), pickupStand)
            + Manhattan(pickupStand, deliveryCell);
        float priorityBonus = repository.PrioritizedHaulStackIds.Contains(stack.stackId)
            ? 12f
            : 0f;
        candidate = new HaulCandidate
        {
            Stack = stack,
            Quantity = acceptable,
            PickupStandPosition = pickupStand,
            Warehouse = warehouse,
            DeliveryPosition = deliveryCell,
            DropPosition = dropCell,
            DestinationKind = destinationKind,
            DestinationId = destinationId,
            IsPriority = priorityBonus > 0f,
            Score = priorityBonus
                + definition.UnitPrice * acceptable * 0.02f
                + Mathf.Min(acceptable, definition.MaxStack) * 0.01f
                - distance * 0.01f,
            TotalWeight = definition.UnitWeight * acceptable
        };
        return true;
    }

    private int GetAcceptableQuantity(
        CharacterCarryInventory inventory,
        string itemId,
        int requestedQuantity,
        float plannedWeight)
    {
        if (inventory == null || requestedQuantity <= 0)
        {
            return 0;
        }

        DungeonItemDefinition definition = catalogProvider.GetDefinition(itemId);
        float maxAllowed = inventory.GetMaxAllowedWeight(haulingSettingsProvider);
        float current = inventory.GetCurrentWeight(catalogProvider);
        float remainingWeight = Mathf.Max(
            0f,
            maxAllowed - current - Mathf.Max(0f, plannedWeight));
        int byWeight = Mathf.FloorToInt(
            remainingWeight / Mathf.Max(0.01f, definition.UnitWeight));
        return Mathf.Clamp(byWeight, 0, Mathf.Max(0, requestedQuantity));
    }

    /// <summary>
    /// Ordinary warehouse hauling must not make the settlement's last immediately
    /// consumable food or water disappear into transit. Quantity leases let us leave
    /// one emergency serving per active character at the source while the warehouse
    /// has not yet received an equivalent reserve. Once the first delivery lands,
    /// the remaining source quantity becomes haulable normally.
    /// </summary>
    private int ApplySurvivalTransitReserve(
        WorldItemStackRecord stack,
        WorldItemHaulDestinationKind destinationKind,
        int proposedQuantity)
    {
        if (stack == null
            || proposedQuantity <= 0
            || stack.state != WorldItemStackState.Loose
            || destinationKind != WorldItemHaulDestinationKind.Warehouse
            || !TryGetWarehouseStockCategory(stack.itemId, out StockCategory category)
            || !RequiresImmediateSurvivalReserve(stack.itemId, category))
        {
            return Mathf.Max(0, proposedQuantity);
        }

        int activeConsumers = 0;
        IReadOnlyList<CharacterActor> characters = worldRegistry.Characters;
        for (int index = 0; index < characters.Count; index++)
        {
            CharacterActor character = characters[index];
            if (character != null
                && character.CurrentLifecycleState == CharacterLifecycleState.Active)
            {
                activeConsumers++;
            }
        }

        // A planning actor may be evaluated before its runtime registry publication.
        // Protect at least the actor's own next serving in that short bootstrap window.
        activeConsumers = Mathf.Max(1, activeConsumers);

        int availableStored = CountAvailableStoredStock(category);
        int sourceReserve = Mathf.Max(0, activeConsumers - availableStored);
        int sourceAvailable = reservationService.GetAvailableQuantity(
            new ItemStackId(stack.stackId));
        int movable = Mathf.Max(0, sourceAvailable - sourceReserve);
        return Mathf.Min(proposedQuantity, movable);
    }

    private bool RequiresImmediateSurvivalReserve(
        string itemId,
        StockCategory category)
    {
        if (category == StockCategory.Water)
            return true;
        return category == StockCategory.Food
            && catalogProvider.TryGetDefinition(itemId, out DungeonItemDefinition definition)
            && definition != null
            && definition.ResourceKind == ResourceItemKind.Food;
    }

    private int CountAvailableStoredStock(StockCategory category)
    {
        int total = 0;
        foreach (WorldItemStackRecord candidate in repository.Records)
        {
            if (candidate == null
                || candidate.forbidden
                || candidate.state != WorldItemStackState.Stored
                || !TryGetWarehouseStockCategory(candidate.itemId, out StockCategory storedCategory)
                || storedCategory != category)
            {
                continue;
            }

            total += reservationService.GetAvailableQuantity(
                new ItemStackId(candidate.stackId));
        }

        return Mathf.Max(0, total);
    }

    private bool TryFindWarehouse(
        Grid grid,
        GridPathSearchResult reachable,
        WorldItemStackRecord stack,
        out IWarehouseFacility warehouse,
        out Vector2Int deliveryCell)
    {
        warehouse = null;
        deliveryCell = default;
        bool isStockItem = TryGetWarehouseStockCategory(
            stack.itemId,
            out StockCategory category);
        bool isEquipmentItem = PhysicalItemIds.TryGetEquipmentDefinitionId(
            stack.itemId,
            out _);
        if (!isStockItem && !isEquipmentItem)
        {
            return false;
        }

        int bestFaultSaturation = int.MaxValue;
        int bestDistance = int.MaxValue;
        int bestUtilization = int.MaxValue;
        string bestId = string.Empty;
        foreach (IWarehouseFacility candidate in GetWarehouses()
            .Where(candidate =>
                candidate.HasWarehouseInventory
                && candidate.Inventory != null
                && (isEquipmentItem || candidate.Inventory.CanStore(category, 1))))
        {
            if (candidate is not BuildableObject building
                || building.isDestroy
                || !TryResolveDeliveryCell(
                    grid,
                    building,
                    out Vector2Int candidateDelivery)
                )
            {
                continue;
            }

            int distance = Manhattan(stack.position, candidateDelivery);
            int utilization = GetWarehouseUtilizationPermille(candidate.Inventory);
            int faultSaturation = utilization >= 900 ? 1 : 0;
            string candidateId = candidate.PersistentInstanceId.Value ?? string.Empty;
            bool improves = faultSaturation < bestFaultSaturation
                || faultSaturation == bestFaultSaturation && distance < bestDistance
                || faultSaturation == bestFaultSaturation && distance == bestDistance
                    && utilization < bestUtilization
                || faultSaturation == bestFaultSaturation && distance == bestDistance
                    && utilization == bestUtilization
                    && string.CompareOrdinal(candidateId, bestId) < 0;
            if (!improves)
            {
                continue;
            }

            bestFaultSaturation = faultSaturation;
            bestDistance = distance;
            bestUtilization = utilization;
            bestId = candidateId;
            warehouse = candidate;
            deliveryCell = candidateDelivery;
        }

        return warehouse != null;
    }

    private static int GetWarehouseUtilizationPermille(
        IWarehouseInventoryPort inventory)
    {
        if (inventory == null || !inventory.HasCapacityLimit)
            return 0;
        int capacity = Mathf.Max(1, inventory.MaxCapacity);
        long scaledStock = checked((long)Mathf.Max(0, inventory.TotalStock) * 1000L);
        long roundedUp = checked((scaledStock + capacity - 1L) / capacity);
        return checked((int)Math.Min(1000L, roundedUp));
    }

    private IEnumerable<IWarehouseFacility> GetWarehouses()
    {
        return worldRegistry.Warehouses.Where(IsUsableWarehouse);
    }

    private IReadOnlyList<WorldItemStackRecord> GetHaulableStacks()
    {
        if (!repository.HaulableCacheDirty)
        {
            return repository.HaulableCache;
        }

        repository.HaulableCache.Clear();
        foreach (WorldItemStackRecord stack in repository.Records)
        {
            if (CanHaul(stack))
            {
                repository.HaulableCache.Add(stack);
            }
        }

        repository.HaulableCacheDirty = false;
        return repository.HaulableCache;
    }

    private static bool IsUsableWarehouse(IWarehouseFacility warehouse)
    {
        return warehouse != null
            && warehouse.HasWarehouseInventory
            && warehouse.Inventory != null;
    }

    private bool CanUseStack(WorldItemStackRecord stack, string actorId)
    {
        _ = actorId;
        return CanHaul(stack)
            && reservationService.GetAvailableQuantity(
                new ItemStackId(stack.stackId)) > 0;
    }

    private static bool CanHaul(WorldItemStackRecord stack)
    {
        return stack != null
            && stack.quantity > 0
            && !stack.forbidden
            && !IsFacilityInputBuffer(stack)
            && (stack.state == WorldItemStackState.Loose
                || stack.state == WorldItemStackState.FacilityBuffer
                || IsOutboundStoredStack(stack));
    }

    private static bool IsOutboundStoredStack(WorldItemStackRecord stack)
    {
        return stack != null
            && stack.state == WorldItemStackState.Stored
            && stack.hasDestinationPosition
            && !string.IsNullOrWhiteSpace(stack.destinationId)
            && !string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId);
    }

    private static bool IsFacilityInputBuffer(WorldItemStackRecord stack)
    {
        return stack != null
            && stack.state == WorldItemStackState.FacilityBuffer
            && stack.hasDestinationPosition
            && !string.IsNullOrWhiteSpace(stack.destinationId);
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

    private static bool HasSameDestination(HaulCandidate a, HaulCandidate b)
    {
        if (a == null || b == null || a.DestinationKind != b.DestinationKind)
        {
            return false;
        }

        return a.DestinationKind == WorldItemHaulDestinationKind.FacilityBuffer
            ? string.Equals(a.DestinationId, b.DestinationId, StringComparison.Ordinal)
            : ReferenceEquals(a.Warehouse, b.Warehouse);
    }

    private static int GetDetour(HaulCandidate seed, HaulCandidate candidate)
    {
        int direct = Manhattan(
            seed.PickupStandPosition,
            seed.DeliveryPosition);
        int withPickup = Manhattan(
                seed.PickupStandPosition,
                candidate.PickupStandPosition)
            + Manhattan(
                candidate.PickupStandPosition,
                seed.DeliveryPosition);
        return Mathf.Max(0, withPickup - direct);
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private bool TryGetPlanningSearch(
        CharacterActor actor,
        Grid grid,
        out GridPathSearchResult reachable)
    {
        reachable = null;
        return actor != null
            && grid != null
            && pathSearchBroker.TryGetSearch(
                grid,
                actor.GetNowXY(),
                out reachable,
                GridPathSearchPriority.Normal,
                GridTraversalContext.ForCharacter(
                    CharacterPersistentIdentity.Require(actor)));
    }

    private static bool TryResolvePickupStandCell(
        Grid grid,
        GridPathSearchResult reachable,
        Vector2Int itemPosition,
        out Vector2Int standCell)
    {
        standCell = default;
        if (grid == null || reachable == null)
        {
            return false;
        }

        // Items may occupy a cell that is statically walkable but cannot be
        // entered under the actor's traversal authority (resource nodes are a
        // common example).  Planning and execution must agree on the exact
        // reachable stand instead of retrying an impossible exact-cell plan.
        Vector2Int[] candidates =
        {
            itemPosition,
            itemPosition + Vector2Int.left,
            itemPosition + Vector2Int.right
        };
        int bestCost = int.MaxValue;
        for (int index = 0; index < candidates.Length; index++)
        {
            Vector2Int candidate = candidates[index];
            if (!grid.IsValidGridPos(candidate) || !grid.IsWalkable(candidate))
            {
                continue;
            }

            int moveCost = reachable.GetMoveCostTo(candidate);
            if (moveCost == int.MaxValue || moveCost >= bestCost)
            {
                continue;
            }

            bestCost = moveCost;
            standCell = candidate;
        }

        return bestCost != int.MaxValue;
    }

    private static bool TryResolveDeliveryCell(
        Grid grid,
        BuildableObject warehouse,
        out Vector2Int deliveryCell)
    {
        deliveryCell = default;
        if (grid == null || warehouse == null)
        {
            return false;
        }

        foreach (Vector2Int position in
            warehouse.buildPoses ?? Array.Empty<Vector2Int>())
        {
            if (grid.IsValidGridPos(position) && grid.IsWalkable(position))
            {
                deliveryCell = position;
                return true;
            }
        }

        return grid.TryFindNearbyWalkablePositionOnSameFloor(
            warehouse.centerPos,
            out deliveryCell,
            maxDistance: 2);
    }

    private static bool TryResolveFacilityDeliveryCell(
        Grid grid,
        Vector2Int destinationPosition,
        out Vector2Int deliveryCell)
    {
        deliveryCell = default;
        if (grid == null)
        {
            return false;
        }

        if (grid.IsValidGridPos(destinationPosition)
            && grid.IsWalkable(destinationPosition))
        {
            deliveryCell = destinationPosition;
            return true;
        }

        return grid.TryFindNearbyWalkablePositionOnSameFloor(
            destinationPosition,
            out deliveryCell,
            maxDistance: 2);
    }

    private sealed class HaulCandidate
    {
        public WorldItemStackRecord Stack;
        public int Quantity;
        public Vector2Int PickupStandPosition;
        public IWarehouseFacility Warehouse;
        public Vector2Int DeliveryPosition;
        public Vector2Int DropPosition;
        public WorldItemHaulDestinationKind DestinationKind;
        public string DestinationId = string.Empty;
        public bool IsPriority;
        public float Score;
        public float TotalWeight;
        public int DetourCost;
    }
}
