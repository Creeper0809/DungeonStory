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

public enum WorldItemDeliveryReachabilityStatus
{
    Invalid = 0,
    Deferred = 1,
    Unreachable = 2,
    Reachable = 3
}

/// <summary>
/// Read-only preflight for an exact physical stack and destination pair. Domain
/// selectors use this before they mutate destination custody, so an unreachable
/// preferred lot cannot hide a reachable fallback behind a permanent pending
/// quantity.
/// </summary>
public interface IWorldItemDeliveryReachabilityQuery
{
    // This preflight is advisory until destination custody is committed.
    WorldItemDeliveryReachabilityStatus AssessExactStackDelivery(
        ItemStackId stackId,
        int quantity,
        Vector2Int destinationPosition,
        string destinationId,
        out string failureReason);
}

public sealed class WorldItemHaulPlanningService :
    IWorldItemHaulPlanningService,
    IWorldItemDeliveryReachabilityQuery
{
    private const int MaximumPickupLegs = 6;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IItemHaulingSettingsProvider haulingSettingsProvider;
    private readonly ICharacterIdRegistry characterIdRegistry;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly WorldItemRepository repository;
    private readonly IItemQuantityReservationService reservationService;
    private readonly IFacilityBufferDestinationClaimQuery destinationClaims;
    private readonly IWarehouseMassAdmissionService warehouseMassAdmission;
    private readonly IFacilityOutputExactRouteOutboxQuery exactRouteQuery;
    private readonly IProductionCapacityRoutingDrainQuery capacityDrains;
    private readonly IPreparedOutputExactDestinationAdmissionParticipant
        exactDestinationAdmission;
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
        IPhysicalItemMassQuery massQuery,
        IItemHaulingSettingsProvider haulingSettingsProvider,
        ICharacterIdRegistry characterIdRegistry,
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        WorldItemRepository repository,
        IItemQuantityReservationService reservationService,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        IWarehouseMassAdmissionService warehouseMassAdmission = null,
        IFacilityOutputExactRouteOutboxQuery exactRouteQuery = null,
        IPreparedOutputExactDestinationAdmissionParticipant
            exactDestinationAdmission = null,
        IProductionCapacityRoutingDrainQuery capacityDrains = null)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.catalogProvider = catalogProvider
            ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
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
        this.warehouseMassAdmission = warehouseMassAdmission;
        this.exactRouteQuery = exactRouteQuery;
        this.exactDestinationAdmission = exactDestinationAdmission;
        this.capacityDrains = capacityDrains;
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

    public WorldItemDeliveryReachabilityStatus AssessExactStackDelivery(
        ItemStackId stackId,
        int quantity,
        Vector2Int destinationPosition,
        string destinationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string destination = destinationId ?? string.Empty;
        if (!stackId.IsValid
            || quantity <= 0
            || destination.Length == 0
            || !string.Equals(
                destination,
                destination.Trim(),
                StringComparison.Ordinal))
        {
            failureReason = "delivery-reachability-identity-invalid";
            return WorldItemDeliveryReachabilityStatus.Invalid;
        }
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "delivery-reachability-grid-deferred";
            return WorldItemDeliveryReachabilityStatus.Deferred;
        }
        if (!repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord stack)
            || stack == null
            || stack.quantity < quantity
            || stack.forbidden
            || stack.state is not (
                WorldItemStackState.Loose or WorldItemStackState.Stored))
        {
            failureReason = "delivery-reachability-stack-invalid";
            return WorldItemDeliveryReachabilityStatus.Invalid;
        }

        int available = reservationService.GetAvailableQuantity(stackId);
        if (available < quantity)
        {
            // A pre-pickup haul lease can temporarily own the exact slice. It is
            // not proof that the route is unreachable and must not be revoked by
            // a read-only selector.
            failureReason = "delivery-reachability-stack-leased";
            return WorldItemDeliveryReachabilityStatus.Deferred;
        }

        WorldItemHaulDestinationKind destinationKind =
            TryParseWarehouseId(destination, out _)
                ? WorldItemHaulDestinationKind.Warehouse
                : WorldItemHaulDestinationKind.FacilityBuffer;
        if (!WorldItemHaulDestinationAuthority.TryResolve(
                grid,
                worldRegistry,
                destinationClaims,
                destinationKind,
                destination,
                destinationPosition,
                out WorldItemHaulDestinationAuthority.Resolution resolved,
                out failureReason))
        {
            return WorldItemDeliveryReachabilityStatus.Invalid;
        }

        bool activeActorSeen = false;
        bool searchDeferred = false;
        IReadOnlyList<CharacterActor> characters = worldRegistry.Characters
            ?? Array.Empty<CharacterActor>();
        foreach (CharacterActor actor in characters
                     .Where(value => value != null)
                     .OrderBy(
                         value => value.BuildingCharacterId.Value,
                         StringComparer.Ordinal))
        {
            if (actor.CurrentLifecycleState != CharacterLifecycleState.Active)
                continue;
            activeActorSeen = true;
            CharacterCarryInventory inventory = actor.CarryInventory;
            if (inventory == null
                || GetAcceptableQuantity(
                    inventory,
                    stack,
                    quantity,
                    plannedWeight: 0f,
                    out _) < quantity)
            {
                continue;
            }
            if (!TryGetPlanningSearch(actor, grid, out GridPathSearchResult reachable))
            {
                searchDeferred = true;
                continue;
            }
            if (TryResolvePickupStandCell(
                    grid,
                    reachable,
                    stack.position,
                    out _)
                && reachable.GetMoveCostTo(resolved.DeliveryPosition)
                    != int.MaxValue)
            {
                failureReason = string.Empty;
                return WorldItemDeliveryReachabilityStatus.Reachable;
            }
        }

        if (!activeActorSeen || searchDeferred)
        {
            failureReason = !activeActorSeen
                ? "delivery-reachability-actor-deferred"
                : "delivery-reachability-search-deferred";
            return WorldItemDeliveryReachabilityStatus.Deferred;
        }
        failureReason = "delivery-reachability-no-actor-route";
        return WorldItemDeliveryReachabilityStatus.Unreachable;
    }

    public bool TryPreviewBestPlan(
        CharacterActor actor,
        out WorldItemHaulPlan plan,
        out string failureReason)
    {
        return TryBuildBestPlan(actor, reserve: false, out plan, out failureReason);
    }

#if UNITY_EDITOR
    public bool TryExplainCandidateForEditorTest(
        CharacterActor actor,
        string stackId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null
            || string.IsNullOrWhiteSpace(stackId)
            || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "invalid planning context";
            return false;
        }
        if (!repository.RecordsById.TryGetValue(
                stackId,
                out WorldItemStackRecord stack)
            || stack == null)
        {
            failureReason = "stack missing";
            return false;
        }
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        if (inventory == null)
        {
            failureReason = "no carry inventory";
            return false;
        }
        if (stack.quantity <= 0)
            return FailEditorCandidate("quantity is not positive", out failureReason);
        if (stack.forbidden)
            return FailEditorCandidate("stack is forbidden", out failureReason);
        if (FacilityOutputExactRouteCustodyCodec.IsRouteBlocked(stack.components))
            return FailEditorCandidate("exact route custody is blocked", out failureReason);
        if (IsCapacityDrainBlocked(stack))
            return FailEditorCandidate("capacity drain is pending", out failureReason);
        if (repository.IsProductionInputDestinationDrainOpen(stack.destinationId))
            return FailEditorCandidate("production input drain is open", out failureReason);
        if (!HasConsistentRoutableCustody(stack))
            return FailEditorCandidate("routable custody is inconsistent", out failureReason);
        if (!IsExactRouteDeliveryCandidate(
                stack,
                exactRouteQuery,
                out FacilityOutputExactRouteFailure routeFailure,
                exactDestinationAdmission))
        {
            return FailEditorCandidate(
                routeFailure.IsFailure
                    ? $"exact route:{routeFailure.Code}:{routeFailure.Reason}"
                    : "exact route candidate rejected",
                out failureReason);
        }
        if (IsFacilityInputBuffer(stack))
            return FailEditorCandidate("stack is a facility input buffer", out failureReason);
        if (stack.state != WorldItemStackState.Loose
            && stack.state != WorldItemStackState.FacilityBuffer
            && !IsOutboundStoredStack(stack))
        {
            return FailEditorCandidate(
                $"state is not haulable:{stack.state}",
                out failureReason);
        }
        int available = reservationService.GetAvailableQuantity(
            new ItemStackId(stack.stackId));
        if (available <= 0)
        {
            failureReason = $"no available quantity:reserved={stack.reservedQuantity}:"
                + $"quantity={stack.quantity}";
            return false;
        }
        if (!TryGetPlanningSearch(actor, grid, out GridPathSearchResult reachable))
        {
            failureReason = "path search deferred";
            return false;
        }
        return TryBuildCandidate(
            grid,
            reachable,
            actor,
            inventory,
            stack,
            plannedWeight: 0f,
            out _,
            out failureReason);
    }

    private static bool FailEditorCandidate(
        string reason,
        out string failureReason)
    {
        failureReason = reason;
        return false;
    }
#endif

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
        if (seed.DestinationKind == WorldItemHaulDestinationKind.Warehouse)
        {
            // A gram-authoritative destination is admitted one exact lot per
            // haul operation. This keeps physical publication and the opaque
            // destination token one-to-one until the staged multi-lot
            // transaction coordinator replaces this conservative boundary.
            selected = new List<HaulCandidate> { seed };
            plannedWeight = seed.TotalWeight;
            expectedDetour = 0;
        }
        // A haul operation is one plan, never one actor-wide reusable owner.
        // Its durable identity is retained by the delivery intent across save.
        string ownerOperationId = reserve
            ? AllocateHaulOperationIdWithoutAdmissionHistory(
                actorId,
                selected)
            : string.Empty;
        Dictionary<string, ItemQuantityLease> leasesByStack =
            new(StringComparer.Ordinal);
        List<WarehouseHaulAdmissionSaveData> warehouseAdmissions = new();
        bool reserveExactWarehouseBeforeLease =
            RequiresExactWarehouseAdmissionsBeforeLease(
                selected.Select(candidate => candidate.Stack));
        if (reserve)
        {
            if (reserveExactWarehouseBeforeLease
                && !TryReserveWarehouseAdmissions(
                    ownerOperationId,
                    selected,
                    warehouseAdmissions,
                    out failureReason))
            {
                // No item lease, intent or physical mutation exists yet. A
                // stale capacity preflight is therefore a pure typed no-plan.
                return false;
            }
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
                ReleaseWarehouseAdmissions(
                    warehouseAdmissions,
                    WarehouseMassAdmissionReleaseReason.TransactionRollback);
                failureReason = "reservation changed";
                return false;
            }
            foreach (ItemQuantityLease lease in leases)
            {
                if (lease?.slices == null || lease.slices.Count != 1)
                    continue;
                leasesByStack[lease.slices[0].stackId] = lease;
            }

            if (!reserveExactWarehouseBeforeLease
                && !TryReserveWarehouseAdmissions(
                    ownerOperationId,
                    selected,
                    warehouseAdmissions,
                    out failureReason))
            {
                reservationService.ReleaseByOwner(
                    ownerOperationId,
                    ItemReservationReleaseReason.Cancelled);
                return false;
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
                warehouseAdmissions,
                out string intentFailure))
        {
            ReleaseWarehouseAdmissions(
                warehouseAdmissions,
                WarehouseMassAdmissionReleaseReason.TransactionRollback);
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

    private string AllocateHaulOperationIdWithoutAdmissionHistory(
        string actorId,
        IReadOnlyList<HaulCandidate> selected)
    {
        while (true)
        {
            string operationId =
                repository.AllocateHaulDeliveryOperationId(actorId);
            if (warehouseMassAdmission == null)
            {
                return operationId;
            }

            int admissionIndex = 0;
            bool conflicts = false;
            foreach (HaulCandidate candidate in
                     (selected ?? Array.Empty<HaulCandidate>())
                     .Where(candidate => candidate?.Warehouse?.Inventory != null
                         && candidate.DestinationKind
                            == WorldItemHaulDestinationKind.Warehouse
                         && candidate.Warehouse.Inventory.HasMassCapacityAuthority)
                     .OrderBy(candidate => candidate.Stack.stackId,
                         StringComparer.Ordinal))
            {
                string admissionOperationId =
                    $"{operationId}:warehouse-admission:{admissionIndex:D2}";
                if (warehouseMassAdmission.HasOwnerOperationHistory(
                        admissionOperationId))
                {
                    conflicts = true;
                    break;
                }
                admissionIndex++;
            }

            if (!conflicts)
            {
                return operationId;
            }
        }
    }

    private bool TryReserveWarehouseAdmissions(
        string haulOperationId,
        IReadOnlyList<HaulCandidate> selected,
        ICollection<WarehouseHaulAdmissionSaveData> admissions,
        out string failureReason)
    {
        failureReason = string.Empty;
        HaulCandidate[] warehouseLegs = (selected ?? Array.Empty<HaulCandidate>())
            .Where(candidate => candidate?.Warehouse?.Inventory != null
                && candidate.DestinationKind == WorldItemHaulDestinationKind.Warehouse
                && candidate.Warehouse.Inventory.HasMassCapacityAuthority)
            .OrderBy(candidate => candidate.Stack.stackId, StringComparer.Ordinal)
            .ToArray();
        if (warehouseLegs.Length == 0)
        {
            return true;
        }
        if (warehouseMassAdmission == null)
        {
            failureReason = "warehouse mass admission service unavailable";
            return false;
        }

        for (int index = 0; index < warehouseLegs.Length; index++)
        {
            HaulCandidate candidate = warehouseLegs[index];
            bool exactCustody = FacilityOutputExactRouteCustodyCodec.TryRead(
                candidate.Stack.components,
                out FacilityOutputExactRouteCustodyMetadata custody)
                && custody.Phase ==
                    FacilityOutputExactRouteCustodyPhase.Routable;
            if (exactCustody
                && (candidate.Quantity != candidate.Stack.quantity
                    || !string.Equals(
                        WarehouseStorageIdentity.RequireDestinationId(
                            candidate.Warehouse),
                        custody.CurrentTargetDestinationId,
                        StringComparison.Ordinal)))
            {
                ReleaseWarehouseAdmissions(
                    admissions,
                    WarehouseMassAdmissionReleaseReason.TransactionRollback);
                failureReason =
                    "exact warehouse admission target or quantity changed";
                return false;
            }
            string admissionOperationId =
                $"{haulOperationId}:warehouse-admission:{index:D2}";
            string lotFingerprint = ItemReservationSignature.Create(
                candidate.Stack.itemId,
                candidate.Stack.components);
            PhysicalItemMassSubject massSubject;
            try
            {
                massSubject = PhysicalItemMassSubjectAdapter.Create(
                    massQuery,
                    (ItemDefinitionId)candidate.Stack.itemId,
                    candidate.Stack.itemInstanceId,
                    candidate.Stack.components);
            }
            catch (Exception exception)
            {
                ReleaseWarehouseAdmissions(
                    admissions,
                    WarehouseMassAdmissionReleaseReason.TransactionRollback);
                failureReason =
                    $"warehouse mass subject invalid:{candidate.Stack.stackId}:{exception.Message}";
                return false;
            }
            WarehouseMassAdmissionRequest request = new(
                candidate.Warehouse.PersistentInstanceId,
                admissionOperationId,
                (ItemDefinitionId)candidate.Stack.itemId,
                candidate.Stack.itemInstanceId,
                lotFingerprint,
                candidate.Quantity,
                warehouseMassAdmission.GetWarehouseCapacityRevision(
                    candidate.Warehouse.PersistentInstanceId),
                warehouseMassAdmission.CatalogRevision,
                expectedSourceRevision: repository.ItemStackVersion,
                massSubject: massSubject);
            bool reserved = warehouseMassAdmission.TryReserve(
                request,
                out WarehouseMassAdmissionToken token,
                out DomainFailure failure);
            bool exactTokenMismatch = exactCustody
                && !ExactWarehouseAdmissionMatches(
                    candidate.Stack,
                    candidate.Quantity,
                    token);
            if (!reserved
                || token.AcceptedQuantity != candidate.Quantity
                || exactTokenMismatch)
            {
                // A service may return a partial reservation token with a
                // negative exact-lot result. It is not yet part of admissions,
                // so release it explicitly before rolling back older tokens.
                if (!string.IsNullOrEmpty(token.TokenId))
                {
                    warehouseMassAdmission.TryRelease(
                        token.TokenId,
                        WarehouseMassAdmissionReleaseReason.TransactionRollback,
                        out _);
                }
                ReleaseWarehouseAdmissions(
                    admissions,
                    WarehouseMassAdmissionReleaseReason.TransactionRollback);
                failureReason = !failure.IsFailure
                    ? "warehouse mass admission was partial"
                    : $"warehouse mass admission failed:{failure.Code}:"
                        + string.Join(",", failure.Parameters.ToArray());
                return false;
            }

            admissions.Add(CreateWarehouseAdmissionProjection(
                candidate.Stack,
                admissionOperationId,
                token));
        }
        return true;
    }

    internal static WarehouseHaulAdmissionSaveData
        CreateWarehouseAdmissionProjection(
            WorldItemStackRecord source,
            string admissionOperationId,
            WarehouseMassAdmissionToken token)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return new WarehouseHaulAdmissionSaveData
        {
            tokenId = token.TokenId,
            ownerAdmissionOperationId = admissionOperationId ?? string.Empty,
            warehouseId = token.WarehouseId.Value,
            sourceWarehouseId = TryParseWarehouseId(
                source.sourceStorageDestinationId,
                out string sourceWarehouseId)
                    ? sourceWarehouseId
                    : string.Empty,
            sourceStackId = source.stackId,
            itemId = token.ItemId.Value,
            itemInstanceId = token.ItemInstanceId,
            lotFingerprint = token.LotFingerprint,
            quantity = token.AcceptedQuantity,
            reservedMassGrams = token.ReservedMassGrams,
            catalogRevision = token.CatalogRevision,
            sourceRevision = token.SourceRevision
        };
    }

    internal static bool ExactWarehouseAdmissionMatches(
        WorldItemStackRecord stack,
        int exactQuantity,
        WarehouseMassAdmissionToken token)
    {
        if (stack == null
            || exactQuantity <= 0
            || exactQuantity != stack.quantity
            || !FacilityOutputExactRouteCustodyCodec.TryRead(
                stack.components,
                out FacilityOutputExactRouteCustodyMetadata custody)
            || custody.Phase !=
                FacilityOutputExactRouteCustodyPhase.Routable
            || custody.Quantity != stack.quantity)
        {
            return false;
        }

        string tokenDestinationId = token.WarehouseId.IsValid
            ? WarehouseStorageIdentity.DestinationPrefix + token.WarehouseId.Value
            : string.Empty;
        return string.Equals(
                tokenDestinationId,
                custody.CurrentTargetDestinationId,
                StringComparison.Ordinal)
            && string.Equals(
                token.ItemId.Value,
                stack.itemId,
                StringComparison.Ordinal)
            && string.Equals(
                token.ItemInstanceId,
                stack.itemInstanceId ?? string.Empty,
                StringComparison.Ordinal)
            && string.Equals(
                token.LotFingerprint,
                ItemReservationSignature.Create(
                    stack.itemId,
                    stack.components),
                StringComparison.Ordinal)
            && token.AcceptedQuantity == exactQuantity
            && token.ReservedMassGrams == custody.MassGrams;
    }

    private void ReleaseWarehouseAdmissions(
        IEnumerable<WarehouseHaulAdmissionSaveData> admissions,
        WarehouseMassAdmissionReleaseReason reason)
    {
        if (warehouseMassAdmission == null)
        {
            return;
        }
        foreach (WarehouseHaulAdmissionSaveData admission in admissions
                     ?? Array.Empty<WarehouseHaulAdmissionSaveData>())
        {
            if (admission != null && !string.IsNullOrWhiteSpace(admission.tokenId))
            {
                warehouseMassAdmission.TryRelease(admission.tokenId, reason, out _);
            }
        }
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
                    stack,
                    reservationService.GetAvailableQuantity(
                        new ItemStackId(stack.stackId)),
                    plannedWeight: 0f,
                    out _);
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
                WorldItemHaulDestinationKind explicitKind =
                    TryParseWarehouseId(stack.destinationId, out _)
                        ? WorldItemHaulDestinationKind.Warehouse
                        : WorldItemHaulDestinationKind.FacilityBuffer;
                if (WorldItemHaulDestinationAuthority.TryResolve(
                        grid,
                        worldRegistry,
                        destinationClaims,
                        explicitKind,
                        stack.destinationId,
                        stack.destinationPosition,
                        out WorldItemHaulDestinationAuthority.Resolution
                            destination,
                        out _))
                {
                    if (reachable.GetMoveCostTo(destination.DeliveryPosition)
                        != int.MaxValue)
                    {
                        return true;
                    }
                }

                continue;
            }

            if (HasAvailableWarehouseDestination(grid, reachable, stack)
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
        GridPathSearchResult reachable,
        WorldItemStackRecord stack)
    {
        // Candidate availability and reservation must share exactly the same
        // category, gram-admission, building-lifecycle, delivery-cell and
        // reachability authority. A looser preflight makes AI repeatedly start
        // work that the real plan builder can never commit.
        return TryFindWarehouse(
            grid,
            reachable,
            stack,
            out _,
            out _);
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
                else if (string.IsNullOrWhiteSpace(priorityFailureReason)
                    && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                        stack.components))
                {
                    priorityFailureReason =
                        $"exact route stack {stack.stackId}: {candidateFailureReason}";
                }

                continue;
            }

            if (best == null
                || candidate.PriorityRank > best.PriorityRank
                || candidate.PriorityRank == best.PriorityRank
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

        if (!CanHaul(stack, out FacilityOutputExactRouteFailure routeFailure))
        {
            failureReason = routeFailure.IsFailure
                ? $"exact route is not haulable:{routeFailure.Code}:"
                    + routeFailure.Reason
                : "stack is not haulable";
            return false;
        }

        bool exactWarehouseCustody =
            FacilityOutputExactRouteCustodyCodec.TryRead(
                stack.components,
                out FacilityOutputExactRouteCustodyMetadata exactCustody)
            && exactCustody.Phase ==
                FacilityOutputExactRouteCustodyPhase.Routable
            && TryParseWarehouseId(
                exactCustody.CurrentTargetDestinationId,
                out _);

        int acceptable = GetAcceptableQuantity(
            inventory,
            stack,
            reservationService.GetAvailableQuantity(
                new ItemStackId(stack.stackId)),
            plannedWeight,
            out float unitWeight);
        if (acceptable <= 0)
        {
            failureReason = "carry capacity exhausted";
            return false;
        }
        if (exactWarehouseCustody && acceptable != stack.quantity)
        {
            failureReason =
                "exact warehouse route requires the current physical stack quantity";
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
            WorldItemHaulDestinationKind explicitKind =
                TryParseWarehouseId(destinationId, out _)
                    ? WorldItemHaulDestinationKind.Warehouse
                    : WorldItemHaulDestinationKind.FacilityBuffer;
            if (!WorldItemHaulDestinationAuthority.TryResolve(
                    grid,
                    worldRegistry,
                    destinationClaims,
                    explicitKind,
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
            if (reachable.GetMoveCostTo(deliveryCell) == int.MaxValue)
            {
                failureReason = "explicit destination is unreachable";
                return false;
            }
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
        if (destinationKind == WorldItemHaulDestinationKind.Warehouse
            && warehouse?.Inventory != null)
        {
            acceptable = warehouse.Inventory.GetAcceptableQuantity(
                stack.itemId,
                acceptable);
        }
        if (exactWarehouseCustody && acceptable != stack.quantity)
        {
            failureReason =
                "exact warehouse route capacity changed before admission";
            return false;
        }
        if (acceptable <= 0)
        {
            failureReason = "survival transit reserve protected";
            return false;
        }

        DungeonItemDefinition definition = catalogProvider.GetDefinition(stack.itemId);
        int distance = Manhattan(actor.GetNowXY(), pickupStand)
            + Manhattan(pickupStand, deliveryCell);
        // A stack with a concrete live destination is already committed to a
        // service/output/input route.  It must outrank a generic player-priority
        // tidy-up stack; otherwise two persistent priority flags can let the
        // higher-value loose stack starve the committed delivery forever.
        bool hasCommittedDestination = stack.hasDestinationPosition
            && !string.IsNullOrWhiteSpace(stack.destinationId);
        bool explicitlyPrioritized =
            repository.PrioritizedHaulStackIds.Contains(stack.stackId);
        int priorityRank = hasCommittedDestination
            ? explicitlyPrioritized ? 3 : 2
            : explicitlyPrioritized ? 1 : 0;
        float priorityBonus = priorityRank > 0 ? 12f : 0f;
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
            PriorityRank = priorityRank,
            IsPriority = priorityRank > 0,
            Score = priorityBonus
                + definition.UnitPrice * acceptable * 0.02f
                + Mathf.Min(acceptable, definition.MaxStack) * 0.01f
                - distance * 0.01f,
            TotalWeight = unitWeight * acceptable
        };
        return true;
    }

    private int GetAcceptableQuantity(
        CharacterCarryInventory inventory,
        WorldItemStackRecord stack,
        int requestedQuantity,
        float plannedWeight,
        out float unitWeight)
    {
        unitWeight = 0f;
        if (inventory == null || requestedQuantity <= 0)
        {
            return 0;
        }

        if (stack == null
            || string.IsNullOrWhiteSpace(stack.itemId))
        {
            throw new InvalidOperationException(
                "Haul mass planning requires an exact physical stack.");
        }
        PhysicalItemMassSubject massSubject =
            PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                (ItemDefinitionId)stack.itemId,
                stack.itemInstanceId,
                stack.components);
        unitWeight = massQuery
            .GetStackUnitMass(
                (ItemDefinitionId)stack.itemId,
                massSubject)
            .Value / 1000f;
        if (!(unitWeight > 0f) || float.IsInfinity(unitWeight))
        {
            throw new InvalidOperationException(
                $"Haul stack '{stack.stackId}' has invalid unit mass.");
        }
        float maxAllowed = inventory.GetMaxAllowedWeight(haulingSettingsProvider);
        float current = inventory.GetCurrentWeight(catalogProvider);
        float remainingWeight = Mathf.Max(
            0f,
            maxAllowed - current - Mathf.Max(0f, plannedWeight));
        int byWeight = Mathf.FloorToInt(
            remainingWeight / unitWeight);
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
            // A prepared facility output is an indivisible, exact-custody
            // batch. Holding one serving at the source would split the batch
            // after its output and destination admission were committed.
            // Ordinary loose survival stock still uses the reserve below.
            || IsExactWarehouseCustody(stack)
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
                && candidate.Inventory.Accepts(category)
                && candidate.Inventory.GetAcceptableQuantity(
                    stack.itemId,
                    1) == 1))
        {
            if (candidate is not BuildableObject building
                || building.isDestroy
                || !TryResolveDeliveryCell(
                    grid,
                    building,
                    out Vector2Int candidateDelivery)
                || reachable.GetMoveCostTo(candidateDelivery) == int.MaxValue
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
        if (inventory is not IWarehouseMassCapacityQuery mass
            || mass.MaxMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Warehouse utilization requires positive gram-capacity authority.");
        }
        long occupied = checked(
            mass.StoredMassGrams + mass.ReservedInboundMassGrams);
        long scaled = checked(Math.Max(0L, occupied) * 1000L);
        long massRoundedUp = checked(
            (scaled + mass.MaxMassGrams - 1L) / mass.MaxMassGrams);
        return checked((int)Math.Min(1000L, massRoundedUp));
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
            if (CanHaul(stack, out _))
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
            && warehouse.Inventory != null
            && warehouse.Inventory.HasMassCapacityAuthority;
    }

    private bool CanUseStack(WorldItemStackRecord stack, string actorId)
    {
        _ = actorId;
        return CanHaul(stack, out _)
            && reservationService.GetAvailableQuantity(
                new ItemStackId(stack.stackId)) > 0;
    }

    private bool CanHaul(
        WorldItemStackRecord stack,
        out FacilityOutputExactRouteFailure routeFailure)
    {
        routeFailure = FacilityOutputExactRouteFailure.None;
        return stack != null
            && stack.quantity > 0
            && !stack.forbidden
            && !FacilityOutputExactRouteCustodyCodec.IsRouteBlocked(
                stack.components)
            && !IsCapacityDrainBlocked(stack)
            && !repository.IsProductionInputDestinationDrainOpen(
                stack.destinationId)
            && HasConsistentRoutableCustody(stack)
            && IsExactRouteDeliveryCandidate(
                stack,
                exactRouteQuery,
                out routeFailure,
                exactDestinationAdmission)
            && !IsFacilityInputBuffer(stack)
            && (stack.state == WorldItemStackState.Loose
                || stack.state == WorldItemStackState.FacilityBuffer
                || IsOutboundStoredStack(stack));
    }

    private bool IsCapacityDrainBlocked(WorldItemStackRecord stack) =>
        capacityDrains != null
        && FacilityOutputExactRouteCustodyCodec.TryRead(
            stack?.components,
            out FacilityOutputExactRouteCustodyMetadata custody)
        && capacityDrains.IsBatchPending(custody.BatchCommitId);

    private static bool HasConsistentRoutableCustody(
        WorldItemStackRecord stack)
    {
        if (stack == null
            || !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                stack.components))
        {
            return true;
        }
        return FacilityOutputExactRouteCustodyCodec.TryRead(
                stack.components,
                out FacilityOutputExactRouteCustodyMetadata metadata)
            && metadata.Phase ==
                FacilityOutputExactRouteCustodyPhase.Routable
            && metadata.Quantity == stack.quantity
            && string.Equals(
                metadata.ItemId,
                stack.itemId,
                StringComparison.Ordinal);
    }

    internal static bool IsExactWarehouseCustody(WorldItemStackRecord stack) =>
        stack != null
        && FacilityOutputExactRouteCustodyCodec.TryRead(
            stack.components,
            out FacilityOutputExactRouteCustodyMetadata custody)
        && custody.Phase == FacilityOutputExactRouteCustodyPhase.Routable
        && TryParseWarehouseId(custody.CurrentTargetDestinationId, out _);

    internal static bool RequiresExactWarehouseAdmissionsBeforeLease(
        IEnumerable<WorldItemStackRecord> stacks)
    {
        WorldItemStackRecord[] exact = (stacks
                ?? Array.Empty<WorldItemStackRecord>())
            .ToArray();
        return exact.Length > 0 && exact.All(IsExactWarehouseCustody);
    }

    internal static bool IsExactRouteDeliveryCandidate(
        WorldItemStackRecord stack,
        IFacilityOutputExactRouteOutboxQuery query,
        out FacilityOutputExactRouteFailure failure,
        IPreparedOutputExactDestinationAdmissionParticipant
            destinationAdmission = null)
    {
        failure = FacilityOutputExactRouteFailure.None;
        if (stack == null
            || !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                stack.components))
        {
            // Ordinary hauling deliberately has no dependency on the prepared
            // output lifecycle.
            return true;
        }
        if (!FacilityOutputExactRouteCustodyCodec.TryRead(
                stack.components,
                out FacilityOutputExactRouteCustodyMetadata custody))
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.ComponentMismatch,
                "custody metadata is invalid",
                out failure);
        }
        if (custody.Phase != FacilityOutputExactRouteCustodyPhase.Routable
            || custody.CurrentDeliveryRevision < 0L
            || string.IsNullOrEmpty(
                custody.CurrentDeliveryRevisionFingerprint))
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.PhaseMismatch,
                "current delivery revision is not confirmed",
                out failure);
        }
        if (string.IsNullOrEmpty(custody.CurrentTargetDestinationId))
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.PendingRouteMissing,
                "warehouse selection is still pending",
                out failure);
        }
        if (!stack.hasDestinationPosition
            || !string.Equals(
                stack.destinationId,
                custody.CurrentTargetDestinationId,
                StringComparison.Ordinal)
            || stack.destinationPosition != custody.CurrentTargetPosition)
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.ReceiptMismatch,
                "physical target does not match the custody overlay",
                out failure);
        }
        if (query == null)
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.PendingRouteMissing,
                "current delivery query is unavailable",
                out failure);
        }

        FacilityOutputExactRoutePendingSnapshot[] matches =
            (query.CapturePendingRoutes()
                ?? Array.Empty<FacilityOutputExactRoutePendingSnapshot>())
            .Where(value => value?.Receipt != null
                && string.Equals(
                    value.Receipt.RouteOperationId,
                    custody.RouteOperationId,
                    StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            return FailExactRouteCandidate(
                matches.Length == 0
                    ? FacilityOutputExactRouteFailureCode.PendingRouteMissing
                    : FacilityOutputExactRouteFailureCode.OperationConflict,
                matches.Length == 0
                    ? "current route is absent from the outbox"
                    : "current route is duplicated in the outbox",
                out failure);
        }

        FacilityOutputExactRoutePendingSnapshot route = matches[0];
        FacilityOutputExactRouteDeliveryRevisionSnapshot delivery =
            route.DeliveryRevision;
        if (route.Phase != FacilityOutputExactRoutePhase.Routable)
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.PhaseMismatch,
                "outbox route is not routable",
                out failure);
        }
        if (delivery == null
            || !string.Equals(
                route.Receipt.PhysicalReceiptFingerprint,
                custody.PhysicalReceiptFingerprint,
                StringComparison.Ordinal)
            || delivery.Revision != custody.CurrentDeliveryRevision
            || !string.Equals(
                delivery.RevisionFingerprint,
                custody.CurrentDeliveryRevisionFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                delivery.RerouteOperationId,
                custody.CurrentDeliveryRerouteOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                delivery.TargetDestinationId,
                custody.CurrentTargetDestinationId,
                StringComparison.Ordinal)
            || delivery.TargetPositionX != custody.CurrentTargetPosition.x
            || delivery.TargetPositionY != custody.CurrentTargetPosition.y
            || !string.Equals(
                delivery.TargetAuthorityFingerprint,
                custody.CurrentTargetAuthorityFingerprint,
                StringComparison.Ordinal))
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.ReceiptMismatch,
                "outbox and custody delivery overlays differ",
                out failure);
        }

        if (TryParseWarehouseId(
                custody.CurrentTargetDestinationId,
                out _))
        {
            PreparedOutputExactDestinationAuthoritySnapshot authority = default;
            PreparedOutputExactDestinationAdmissionFailureCode authorityFailure =
                PreparedOutputExactDestinationAdmissionFailureCode.None;
            string authorityReason = string.Empty;
            if (custody.CurrentDeliveryRevision <= 0L
                || string.IsNullOrEmpty(
                    custody.CurrentTargetAuthorityFingerprint)
                || destinationAdmission == null
                || !destinationAdmission.TryCaptureTargetAuthority(
                    PreparedOutputExactDestinationTargetKind.Warehouse,
                    custody.CurrentTargetDestinationId,
                    custody.CurrentTargetPosition,
                    out authority,
                    out authorityFailure,
                    out authorityReason)
                || authority.Kind !=
                    PreparedOutputExactDestinationTargetKind.Warehouse
                || authority.Position != custody.CurrentTargetPosition
                || !string.Equals(
                    authority.DestinationId,
                    custody.CurrentTargetDestinationId,
                    StringComparison.Ordinal)
                || string.IsNullOrEmpty(authority.Fingerprint)
                || !string.Equals(
                    authority.Fingerprint,
                    authority.Fingerprint.Trim(),
                    StringComparison.Ordinal)
                || authority.CapacityRevision <= 0L
                || authority.MassAuthorityRevision <= 0L
                || authority.MaxMassGrams <= 0L
                || authority.ReservedMassGrams < 0L
                || authority.ReservedMassGrams > authority.MaxMassGrams)
            {
                return FailExactRouteCandidate(
                    FacilityOutputExactRouteFailureCode.ReceiptMismatch,
                    "warehouse target authority is stale:"
                        + authorityFailure + ":" + authorityReason,
                    out failure);
            }

            // Warehouse admission reserve/release advances the live capacity
            // authority revision. Consequently, a route-minted target fingerprint
            // is a structural provenance token, not a stable view of the mutable
            // admission ledger. Candidate discovery still requires the exact route,
            // custody, destination id, position, and a canonical positive live
            // authority. Current capacity/revision is fenced atomically when the
            // admission token is reserved and again immediately before pickup.
        }
        return true;
    }

    private static bool FailExactRouteCandidate(
        FacilityOutputExactRouteFailureCode code,
        string reason,
        out FacilityOutputExactRouteFailure failure)
    {
        failure = new FacilityOutputExactRouteFailure(code, reason);
        return false;
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

        if (!CanShareOpportunisticRoute(a.Stack, b.Stack))
        {
            return false;
        }
        return a.DestinationKind == WorldItemHaulDestinationKind.FacilityBuffer
            ? string.Equals(a.DestinationId, b.DestinationId, StringComparison.Ordinal)
            : ReferenceEquals(a.Warehouse, b.Warehouse);
    }

    internal static bool CanShareOpportunisticRoute(
        WorldItemStackRecord first,
        WorldItemStackRecord second)
    {
        bool firstHasCustody = first != null
            && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                first.components);
        bool secondHasCustody = second != null
            && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                second.components);
        if (!firstHasCustody && !secondHasCustody)
        {
            return true;
        }
        if (!firstHasCustody
            || !secondHasCustody
            || !FacilityOutputExactRouteCustodyCodec.TryRead(
                first.components,
                out FacilityOutputExactRouteCustodyMetadata firstCustody)
            || !FacilityOutputExactRouteCustodyCodec.TryRead(
                second.components,
                out FacilityOutputExactRouteCustodyMetadata secondCustody))
        {
            return false;
        }
        return string.Equals(
            firstCustody.RouteOperationId,
            secondCustody.RouteOperationId,
            StringComparison.Ordinal);
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

    private static bool TryParseWarehouseId(
        string destinationId,
        out string warehouseId)
    {
        warehouseId = string.Empty;
        string destination = destinationId?.Trim() ?? string.Empty;
        if (!destination.StartsWith(
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                StringComparison.Ordinal))
        {
            return false;
        }
        warehouseId = destination.Substring(
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix.Length);
        return warehouseId.Length > 0;
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
        public int PriorityRank;
        public bool IsPriority;
        public float Score;
        public float TotalWeight;
        public int DetourCost;
    }
}
