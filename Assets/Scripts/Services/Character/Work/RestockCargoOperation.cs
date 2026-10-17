using System;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>
/// Owns one worker's restock cargo, quantity lease and shop admission until
/// delivery or cancellation has returned the physical cargo to a safe owner.
/// </summary>
internal sealed class RestockCargoOperation
{
    private const double ActiveLeaseSeconds = 45d;
    private readonly IGameClock clock;
    private CharacterActor actor;
    private IWorldItemQuantityLeaseRuntime leases;
    private IRetailRestockOperationOwner operationOwner;

    internal RestockCargoOperation(IGameClock clock)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    internal string OperationId { get; private set; } = string.Empty;
    internal string LeaseId { get; private set; } = string.Empty;

    internal static string CreateOperationId(string actorId, string targetId, int saleItemId, int runId)
    {
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(targetId)
            || saleItemId < 0 || runId <= 0)
            throw new ArgumentException("Restock operation identity requires actor, target and sale item IDs.");
        return $"restock:{actorId.Trim()}:{targetId.Trim()}:{saleItemId}:{runId:D8}";
    }

    internal bool TryActivate(CharacterActor worker, IWorldItemQuantityLeaseRuntime leaseRuntime,
        string operationId, string leaseId, IRetailRestockOperationOwner owner)
    {
        if (OperationId.Length != 0 || LeaseId.Length != 0)
            throw new InvalidOperationException("A restock cargo operation is already active.");
        if (worker == null) throw new ArgumentNullException(nameof(worker));
        if (leaseRuntime == null) throw new ArgumentNullException(nameof(leaseRuntime));
        if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(leaseId))
            throw new ArgumentException("Restock cargo requires operation and lease identities.");
        actor = worker;
        leases = leaseRuntime;
        OperationId = operationId;
        LeaseId = leaseId;
        operationOwner = owner;
        // Keep the acquired quantity lease owned even when shop admission fails.
        // The caller then uses the same cancellation path to release it.
        return operationOwner != null && operationOwner.TryBeginRestockOperation(OperationId);
    }

    internal bool TryRenew(out string failureReason)
    {
        failureReason = string.Empty;
        if (leases == null || string.IsNullOrWhiteSpace(LeaseId))
        {
            failureReason = "restock-quantity-lease-runtime-unavailable";
            return false;
        }
        if (!leases.TryRevalidateQuantityLease(LeaseId, out failureReason))
        {
            failureReason = "revalidate:" + failureReason;
            return false;
        }
        if (!leases.TryRenewQuantityLease(LeaseId, clock.Time + ActiveLeaseSeconds, out failureReason))
        {
            failureReason = "renew:" + failureReason;
            return false;
        }
        return true;
    }

    internal void Restore(CharacterActor worker, ItemQuantityLease lease,
        IRetailRestockOperationOwner owner)
    {
        if (OperationId.Length != 0 || LeaseId.Length != 0)
            throw new InvalidOperationException("A restock cargo operation is already active.");
        if (worker == null || worker.WorldItemStackRuntime is not IWorldItemQuantityLeaseRuntime runtime
            || lease == null || string.IsNullOrWhiteSpace(lease.leaseId)
            || string.IsNullOrWhiteSpace(lease.ownerOperationId)
            || !lease.ownerOperationId.StartsWith("restock:", StringComparison.Ordinal)
            || lease.purpose != ItemReservationPurpose.FacilityBuffer || lease.remainingQuantity <= 0
            || string.IsNullOrWhiteSpace(worker.Identity?.PersistentId)
            || !string.Equals(worker.Identity.PersistentId, lease.ownerCharacterId, StringComparison.Ordinal)
            || owner == null)
            throw new InvalidOperationException("Restock restore requires an exact worker, quantity lease and shop owner.");

        // Shop admission and the physical lease were restored by their owners.
        // Rebind only the cancellation handle; never acquire a second claim.
        actor = worker;
        leases = runtime;
        OperationId = lease.ownerOperationId;
        LeaseId = lease.leaseId;
        operationOwner = owner;
    }

    internal void RelinquishForWorldRestore(string expectedOperationId = null)
    {
        if (expectedOperationId != null
            && !string.Equals(OperationId, expectedOperationId, StringComparison.Ordinal))
            throw new InvalidOperationException("Restock restore relinquish does not own the expected operation.");
        // Candidate rollback and retiring actors must not issue commands against
        // the physical root now owned by the restored world.
        ClearHandle();
    }

    internal void Release(ItemReservationReleaseReason reason)
    {
        if (OperationId.Length == 0 && LeaseId.Length == 0) return;
        if (OperationId.Length != 0 && actor != null)
        {
            CharacterCarryInventory carry = actor.GetComponent<CharacterCarryInventory>();
            bool ownsCargo = carry?.Items.Any(item => item != null && item.quantity > 0
                && string.Equals(item.ownerOperationId, OperationId, StringComparison.Ordinal)) == true;
            string dropFailure = "restock-drop-runtime-unavailable";
            if (ownsCargo && (actor.WorldItemStackRuntime is not IWorldItemCarryRecoveryRuntime recovery
                || !recovery.TryDropCarriedItems(actor, carry, new[] { OperationId }, out dropFailure)))
                throw new InvalidOperationException(
                    $"Restock cargo '{OperationId}' could not be physically dropped before lease release: {dropFailure}");
        }

        // Shop admission and quantity release are idempotent. Retain this scope
        // if either command throws, so cancellation can retry the same identities.
        operationOwner?.EndRestockOperation(OperationId);
        if (LeaseId.Length != 0) leases.ReleaseQuantityLease(LeaseId, reason);
        ClearHandle();
    }

    private void ClearHandle()
    {
        OperationId = string.Empty;
        LeaseId = string.Empty;
        operationOwner = null;
        leases = null;
        actor = null;
    }
}
