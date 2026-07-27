using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IWorldItemHaulPlanningService
{
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
    private readonly IItemReservationService reservationService;

    public WorldItemHaulPlanningService(
        IGridSystemProvider gridSystemProvider,
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider haulingSettingsProvider,
        ICharacterIdRegistry characterIdRegistry,
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        WorldItemRepository repository,
        IItemReservationService reservationService)
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
    }

    public bool HasAvailablePlan(CharacterActor actor)
    {
        return TryBuildBestPlan(actor, reserve: false, out _, out _);
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
            useDropPosition: true);
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
        HaulCandidate seed = FindSeedCandidate(
            grid,
            null,
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
            null,
            actor,
            inventory,
            actorId,
            seed,
            out float plannedWeight,
            out int expectedDetour);
        List<WorldItemHaulPlanLeg> pickupLegs =
            new List<WorldItemHaulPlanLeg>(selected.Count);
        List<WorldItemReservedStackQuantity> reservations =
            new List<WorldItemReservedStackQuantity>(selected.Count);
        foreach (HaulCandidate candidate in selected)
        {
            WorldItemReservedStackQuantity itemReservation =
                new WorldItemReservedStackQuantity(
                    candidate.Stack.stackId,
                    candidate.Stack.itemId,
                    candidate.Quantity,
                    candidate.Stack.position,
                    candidate.DestinationKind,
                    candidate.DestinationId);
            reservations.Add(itemReservation);
            pickupLegs.Add(new WorldItemHaulPlanLeg(
                itemReservation,
                candidate.PickupStandPosition,
                candidate.Warehouse,
                candidate.DeliveryPosition,
                candidate.DropPosition));
        }

        if (reserve
            && !reservationService.TryReserve(
                selected.Select(candidate => candidate.Stack.stackId),
                actorId))
        {
            failureReason = "reservation changed";
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
            seed.DestinationId);
        return true;
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
            stack.quantity,
            plannedWeight);
        if (acceptable <= 0)
        {
            failureReason = "carry capacity exhausted";
            return false;
        }

        if (!TryResolvePickupStandCell(
                grid,
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
            if (!TryResolveFacilityDeliveryCell(
                    grid,
                    stack.destinationPosition,
                    out deliveryCell))
            {
                failureReason =
                    $"no delivery stand near {stack.destinationPosition}";
                return false;
            }

            dropCell = stack.destinationPosition;
            destinationKind = WorldItemHaulDestinationKind.FacilityBuffer;
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
            destinationId = string.Empty;
        }
        else
        {
            failureReason = "no reachable destination";
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
        bool isEquipmentItem = DungeonItemCatalogSO.TryGetEquipmentIdFromItemId(
            stack.itemId,
            out _);
        if (!isStockItem && !isEquipmentItem)
        {
            return false;
        }

        int bestDistance = int.MaxValue;
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
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            warehouse = candidate;
            deliveryCell = candidateDelivery;
        }

        return warehouse != null;
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

    private static bool CanUseStack(WorldItemStackRecord stack, string actorId)
    {
        return CanHaul(stack)
            && (string.IsNullOrWhiteSpace(stack.reservedByPersistentId)
                || string.Equals(
                    stack.reservedByPersistentId,
                    actorId,
                    StringComparison.Ordinal));
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
            && !string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
            && (IsFacilityInputDestination(stack.destinationId)
                || IsCombatLoadoutDestination(stack.destinationId));
    }

    private static bool IsFacilityInputBuffer(WorldItemStackRecord stack)
    {
        return stack != null
            && stack.state == WorldItemStackState.FacilityBuffer
            && IsFacilityInputDestination(stack.destinationId);
    }

    private static bool IsFacilityInputDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && (destinationId.StartsWith(
                    WorldItemStackRuntime.FacilityInputDestinationPrefix,
                    StringComparison.Ordinal)
                || destinationId.StartsWith(
                    WorkOrderRuntime.ConstructionDestinationPrefix,
                    StringComparison.Ordinal));
    }

    private static bool IsCombatLoadoutDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && destinationId.StartsWith(
                WorldItemStackRuntime.CombatLoadoutDestinationPrefix,
                StringComparison.Ordinal);
    }

    private static bool TryGetWarehouseStockCategory(
        string itemId,
        out StockCategory category)
    {
        if (DungeonItemCatalogSO.TryGetStockCategoryFromItemId(
                itemId,
                out category))
        {
            return true;
        }

        if (CombatItemDefinitions.TryGetDefinition(
                itemId,
                out DungeonItemDefinition ammunition))
        {
            category = ammunition.StockCategory;
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

    private static bool TryResolvePickupStandCell(
        Grid grid,
        Vector2Int itemPosition,
        out Vector2Int standCell)
    {
        if (grid.IsValidGridPos(itemPosition) && grid.IsWalkable(itemPosition))
        {
            standCell = itemPosition;
            return true;
        }

        return grid.TryFindNearbyWalkablePositionOnSameFloor(
            itemPosition,
            out standCell,
            maxDistance: 1);
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
