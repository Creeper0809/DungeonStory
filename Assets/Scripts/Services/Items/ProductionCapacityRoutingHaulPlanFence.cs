using System;
using System.Collections.Generic;
using System.Linq;

public interface IProductionCapacityRoutingHaulPlanFence
{
    [GameplayInternalOnly(
        "Stops only unpicked haul plans for a pending capacity drain while preserving already-carried exact custody for actor quiescence.",
        "Production capacity-routing durable prepare only")]
    bool TryReleaseUnpickedPlans(
        string batchCommitId,
        IReadOnlyList<ProductionCapacityRoutingDrainActorCarrySaveData>
            frozenCarries,
        out string failureReason);
}

/// <summary>
/// Closes the race between durable drain preparation and the haul planner.
/// The pending outbox blocks new plans; this service then releases only plans
/// that have not physically picked up batch custody. Picked plans remain exact
/// and are handled by the synchronous actor quiescence transaction.
/// </summary>
public sealed class ProductionCapacityRoutingHaulPlanFence :
    IProductionCapacityRoutingHaulPlanFence
{
    private const string InterruptionReason =
        "production-capacity-routing-drain-unpicked-plan";

    private readonly IProductionCapacityRoutingDrainQuery drains;
    private readonly ICharacterCarryInventoryRegistry inventories;
    private readonly ItemQuantityReservationService reservations;
    private readonly WorldItemRepository repository;
    private readonly IItemReservationMutationGate mutationGate;

    public ProductionCapacityRoutingHaulPlanFence(
        IProductionCapacityRoutingDrainQuery drains,
        ICharacterCarryInventoryRegistry inventories,
        ItemQuantityReservationService reservations,
        WorldItemRepository repository,
        IItemReservationMutationGate mutationGate)
    {
        this.drains = drains ?? throw new ArgumentNullException(nameof(drains));
        this.inventories = inventories
            ?? throw new ArgumentNullException(nameof(inventories));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.mutationGate = mutationGate
            ?? throw new ArgumentNullException(nameof(mutationGate));
    }

    [GameplayInternalOnly(
        "Stops only unpicked haul plans for a pending capacity drain while preserving already-carried exact custody for actor quiescence.",
        "Production capacity-routing durable prepare only")]
    public bool TryReleaseUnpickedPlans(
        string batchCommitId,
        IReadOnlyList<ProductionCapacityRoutingDrainActorCarrySaveData>
            frozenCarries,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!drains.IsBatchPending(batchCommitId))
        {
            failureReason =
                "production-capacity-routing-haul-fence-drain-missing";
            return false;
        }

        ProductionCapacityRoutingDrainActorCarrySaveData[] frozen =
            (frozenCarries
                ?? Array.Empty<ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Where(value => value != null)
            .OrderBy(
                ProductionCapacityRoutingDrainFingerprint.ActorCarryKey,
                StringComparer.Ordinal)
            .ToArray();
        HashSet<string> picked = frozen
            .Select(value => value.haulIntentOperationId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (CharacterCarryInventory inventory in inventories.All
                     .Where(value => value != null)
                     .OrderBy(value => value.CharacterId.Value,
                         StringComparer.Ordinal))
        {
            CharacterActor actor = inventory.GetComponent<CharacterActor>();
            AbilityHaul haul = actor != null
                ? actor.GetComponent<AbilityHaul>()
                : null;
            if (haul == null)
                continue;

            string[] active = haul.CaptureActiveHaulOperationIds()
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] relevant = active
                .Where(operationId => IsBatchOperation(
                    operationId,
                    batchCommitId))
                .ToArray();
            if (relevant.Length == 0)
                continue;

            string[] carried = inventory.Items
                .Where(value => value != null && value.quantity > 0)
                .Select(value => value.ownerOperationId ?? string.Empty)
                .Where(value => relevant.Contains(value, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (carried.Any(value => !picked.Contains(value)))
            {
                failureReason =
                    "production-capacity-routing-unfrozen-picked-operation:"
                    + carried.First(value => !picked.Contains(value));
                return false;
            }

            if (carried.Length > 0)
            {
                if (!active.SequenceEqual(relevant, StringComparer.Ordinal)
                    || relevant.Any(value => !picked.Contains(value)))
                {
                    failureReason =
                        "production-capacity-routing-mixed-picked-plan:"
                        + inventory.CharacterId.Value;
                    return false;
                }
                ProductionCapacityRoutingDrainActorCarrySaveData[] expected =
                    frozen.Where(value => string.Equals(
                            value.actorPersistentId,
                            inventory.CharacterId.Value,
                            StringComparison.Ordinal))
                        .ToArray();
                if (!haul.TryFreezeForCapacityRoutingQuiescence(
                        inventory.CharacterId.Value,
                        expected,
                        out _,
                        out string freezeFailure))
                {
                    failureReason =
                        "production-capacity-routing-picked-plan-freeze-deferred:"
                        + inventory.CharacterId.Value + ":" + freezeFailure;
                    return false;
                }
                continue;
            }

            using IDisposable barrier = mutationGate.EnterCaptureBarrier();
            if (!haul.TryStopHaulingIfActiveOperationsSubsetOf(
                    relevant,
                    InterruptionReason,
                    HaulInterruptionDisposition
                        .ReleaseUnpickedAndRetainCarriedForReplan,
                    out string stopFailure))
            {
                failureReason =
                    "production-capacity-routing-unpicked-plan-release-deferred:"
                    + inventory.CharacterId.Value + ":" + stopFailure;
                return false;
            }
        }

        return true;
    }

    private bool IsBatchOperation(
        string operationId,
        string batchCommitId)
    {
        if (!reservations.TryGetLeasesByOwner(
                operationId,
                out IReadOnlyList<ItemQuantityLease> leases))
        {
            return false;
        }

        foreach (ItemQuantityLease lease in leases ?? Array.Empty<ItemQuantityLease>())
        {
            foreach (ItemLeaseSlice slice in lease?.slices
                         ?? new List<ItemLeaseSlice>())
            {
                if (!repository.RecordsById.TryGetValue(
                        slice.stackId,
                        out WorldItemStackRecord stack)
                    || !FacilityOutputExactRouteCustodyCodec.TryRead(
                        stack.components,
                        out FacilityOutputExactRouteCustodyMetadata custody))
                {
                    continue;
                }
                if (string.Equals(
                        custody.BatchCommitId,
                        batchCommitId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
