using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Rebinds saved shop/quantity ownership to inert, published workers before AI
/// activation. Only transient handles are changed; durable claims stay with
/// their original restore participants through publication and rollback.
/// </summary>
public sealed class RestockCargoRestoreCoordinator : IDungeonRestoreTransactionParticipant
{
    private readonly ICharacterWorldSaveService characters;
    private readonly IRetailWorldQuery retail;
    private readonly IItemQuantityReservationService reservations;
    private readonly List<(AbilityWork Work, string Operation)> rebound = new();
    private bool active;
    private bool published;

    public RestockCargoRestoreCoordinator(ICharacterWorldSaveService characters,
        IRetailWorldQuery retail, IItemQuantityReservationService reservations)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.retail = retail ?? throw new ArgumentNullException(nameof(retail));
        this.reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
    }

    public string ParticipantId => "226.world.restock-cargo";

    public void BeginRestoreCandidate()
    {
        if (active) throw new InvalidOperationException("Restock cargo restore is already active.");
        rebound.Clear();
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
            throw new InvalidOperationException("Restock cargo restore is not ready to publish.");

        var bindings = new List<(AbilityWork Work, ItemQuantityLease Lease, IRetailRestockOperationOwner Owner)>();
        var workers = new HashSet<string>(StringComparer.Ordinal);
        foreach (IRetailStockStateOwner stock in retail.RetailFacilities.OfType<IRetailStockStateOwner>())
        {
            ShopStockStateSnapshot snapshot = stock.CreateStockSnapshot();
            if (snapshot?.activeRestockOperationIds == null)
                throw new InvalidOperationException("Restored retail stock has no restock ownership collection.");
            foreach (string operation in snapshot.activeRestockOperationIds)
            {
                if (stock is not IRetailRestockOperationOwner owner
                    || !reservations.TryGetLeasesByOwner(operation, out var leases)
                    || leases.Count != 1 || leases[0] == null
                    || leases[0].purpose != ItemReservationPurpose.FacilityBuffer
                    || leases[0].remainingQuantity <= 0
                    || !string.Equals(leases[0].ownerOperationId, operation, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(leases[0].ownerCharacterId)
                    || !workers.Add(leases[0].ownerCharacterId)
                    || !characters.TryGetRestoredActor(leases[0].ownerCharacterId, out var actor)
                    || actor == null || !actor.TryGetAbility(out AbilityWork work))
                    throw new InvalidOperationException($"Restock '{operation}' has no exact restored worker/lease/shop binding.");
                bindings.Add((work, leases[0], owner));
            }
        }

        // Validate the entire join before binding any worker. If a later bind
        // or participant fails, reverse only these candidate-local handles.
        foreach (var binding in bindings)
        {
            binding.Work.RestoreRestockOwnership(binding.Lease, binding.Owner);
            rebound.Add((binding.Work, binding.Lease.ownerOperationId));
        }
        published = true;
    }

    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();

    public void CompleteRestoreCandidate()
    {
        rebound.Clear();
        active = false;
        published = false;
    }

    public void DiscardRestoreCandidate()
    {
        List<Exception> failures = null;
        for (int index = rebound.Count - 1; index >= 0; index--)
        {
            try { rebound[index].Work.RelinquishRestockForWorldRestore(rebound[index].Operation); }
            catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
        }
        CompleteRestoreCandidate();
        if (failures != null)
            throw new AggregateException("Restock cargo candidate handles could not all be relinquished.", failures);
    }
}
