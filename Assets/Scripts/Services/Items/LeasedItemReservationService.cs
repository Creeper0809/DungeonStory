using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public readonly struct LeasedItemReservation
{
    public LeasedItemReservation(
        string leaseId,
        string ownerId,
        string stackId,
        string expectedSignature,
        int expectedMinimumQuantity,
        float createdGameHour,
        float expiresGameHour,
        float maximumGameHour)
    {
        LeaseId = leaseId ?? string.Empty;
        OwnerId = ownerId ?? string.Empty;
        StackId = stackId ?? string.Empty;
        ExpectedSignature = expectedSignature ?? string.Empty;
        ExpectedMinimumQuantity = Math.Max(1, expectedMinimumQuantity);
        CreatedGameHour = createdGameHour;
        ExpiresGameHour = expiresGameHour;
        MaximumGameHour = maximumGameHour;
    }

    public string LeaseId { get; }
    public string OwnerId { get; }
    public string StackId { get; }
    public string ExpectedSignature { get; }
    public int ExpectedMinimumQuantity { get; }
    public float CreatedGameHour { get; }
    public float ExpiresGameHour { get; }
    public float MaximumGameHour { get; }
}

public interface ILeasedItemReservationService
{
    bool TryReserveBatch(
        string ownerId,
        IReadOnlyList<WorldItemReservedStackQuantity> stacks,
        out IReadOnlyList<LeasedItemReservation> leases,
        out DomainFailure failure);
    bool Revalidate(
        string ownerId,
        bool renewForTransportProgress,
        out DomainFailure failure);
    void Release(string ownerId);
    void ReleaseExpired();
}

public sealed class LeasedItemReservationService : ILeasedItemReservationService
{
    private const float GameSecondsPerHour = 7.5f;
    private const float ItemLeaseHours = 2f;
    private const float MaximumLeaseHours = 6f;

    private readonly IWorldItemStackRuntime items;
    private readonly IItemQuantityReservationService reservations;
    private readonly IGameClock clock;
    private readonly Dictionary<string, List<LeasedItemReservation>> byOwner =
        new(StringComparer.Ordinal);

    public LeasedItemReservationService(
        IWorldItemStackRuntime items,
        IItemQuantityReservationService reservations,
        IGameClock clock)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public bool TryReserveBatch(
        string ownerId,
        IReadOnlyList<WorldItemReservedStackQuantity> stacks,
        out IReadOnlyList<LeasedItemReservation> leases,
        out DomainFailure failure)
    {
        leases = Array.Empty<LeasedItemReservation>();
        failure = DomainFailure.None;
        string owner = ownerId?.Trim() ?? string.Empty;
        if (owner.Length == 0 || stacks == null || stacks.Count == 0)
        {
            failure = new DomainFailure(FailureCode.ApparelWorkOrderInvalid, owner);
            return false;
        }
        Dictionary<string, WorldItemStackSnapshot> available = items.GetAllStacks()
            .Where(value => value != null)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        List<ItemQuantityReservationRequest> requests = new(stacks.Count);
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemReservedStackQuantity requested = stacks[index];
            if (!requested.IsValid
                || !available.TryGetValue(requested.StackId, out WorldItemStackSnapshot stack)
                || stack.Forbidden
                || stack.AvailableQuantity < requested.Quantity)
            {
                failure = new DomainFailure(
                    FailureCode.ApparelMaterialUnavailable,
                    requested.StackId);
                return false;
            }
            requests.Add(new ItemQuantityReservationRequest(
                (ItemStackId)requested.StackId,
                requested.Quantity,
                stack.ReservationSignature));
        }
        if (!reservations.TryReserveBatch(
                owner,
                string.Empty,
                ItemReservationPurpose.ProductionInput,
                $"production:{owner}",
                requests,
                out IReadOnlyList<ItemQuantityLease> quantityLeases,
                out failure))
        {
            return false;
        }
        List<LeasedItemReservation> created = quantityLeases
            .Select(ToLegacyLease)
            .ToList();
        byOwner[owner] = created;
        leases = created;
        return true;
    }

    public bool Revalidate(
        string ownerId,
        bool renewForTransportProgress,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string owner = ownerId?.Trim() ?? string.Empty;
        if (!byOwner.TryGetValue(owner, out List<LeasedItemReservation> leases))
        {
            failure = new DomainFailure(FailureCode.ApparelReservationExpired, owner);
            return false;
        }
        for (int index = 0; index < leases.Count; index++)
        {
            LeasedItemReservation lease = leases[index];
            if (!reservations.Revalidate(
                    lease.LeaseId,
                    out ItemQuantityLease current,
                    out failure))
            {
                Release(owner);
                return false;
            }
            if (renewForTransportProgress
                && !reservations.Renew(
                    lease.LeaseId,
                    clock.Time + ItemLeaseHours * GameSecondsPerHour,
                    out failure))
            {
                Release(owner);
                return false;
            }
            leases[index] = ToLegacyLease(current);
        }
        return true;
    }

    public void Release(string ownerId)
    {
        string owner = ownerId?.Trim() ?? string.Empty;
        if (!byOwner.Remove(owner, out _))
        {
            return;
        }
        reservations.ReleaseByOwner(
            owner,
            ItemReservationReleaseReason.Cancelled);
    }

    public void ReleaseExpired()
    {
        float now = GameHour;
        string[] expired = byOwner
            .Where(pair => pair.Value.Any(value => now > value.ExpiresGameHour))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (string owner in expired)
        {
            Release(owner);
        }
    }

    private float GameHour => Math.Max(0f, clock.Time / GameSecondsPerHour);

    private static LeasedItemReservation ToLegacyLease(ItemQuantityLease lease)
    {
        ItemLeaseSlice slice = lease.slices?.FirstOrDefault();
        return new LeasedItemReservation(
            lease.leaseId,
            lease.ownerOperationId,
            slice?.stackId ?? string.Empty,
            slice?.expectedStackSignature ?? string.Empty,
            lease.remainingQuantity,
            (float)(lease.createdAtGameSeconds / GameSecondsPerHour),
            (float)(lease.expiresAtGameSeconds / GameSecondsPerHour),
            (float)(lease.maximumExpiresAtGameSeconds / GameSecondsPerHour));
    }
}
