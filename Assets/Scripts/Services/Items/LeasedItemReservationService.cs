using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public readonly struct LeasedItemReservation
{
    public LeasedItemReservation(
        string ownerId,
        string stackId,
        string expectedSignature,
        int expectedMinimumQuantity,
        float createdGameHour,
        float expiresGameHour,
        float maximumGameHour)
    {
        OwnerId = ownerId ?? string.Empty;
        StackId = stackId ?? string.Empty;
        ExpectedSignature = expectedSignature ?? string.Empty;
        ExpectedMinimumQuantity = Math.Max(1, expectedMinimumQuantity);
        CreatedGameHour = createdGameHour;
        ExpiresGameHour = expiresGameHour;
        MaximumGameHour = maximumGameHour;
    }

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
    private readonly IItemReservationService reservations;
    private readonly IGameClock clock;
    private readonly Dictionary<string, List<LeasedItemReservation>> byOwner =
        new(StringComparer.Ordinal);

    public LeasedItemReservationService(
        IWorldItemStackRuntime items,
        IItemReservationService reservations,
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
        Release(owner);
        Dictionary<string, WorldItemStackSnapshot> available = items.GetAllStacks()
            .Where(value => value != null)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        List<string> ids = new(stacks.Count);
        List<LeasedItemReservation> created = new(stacks.Count);
        float now = GameHour;
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemReservedStackQuantity requested = stacks[index];
            if (!requested.IsValid
                || !available.TryGetValue(requested.StackId, out WorldItemStackSnapshot stack)
                || stack.Forbidden
                || stack.Quantity < requested.Quantity
                || stack.IsReserved && !string.Equals(
                    stack.ReservedByPersistentId,
                    owner,
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.ApparelMaterialUnavailable,
                    requested.StackId);
                return false;
            }
            ids.Add(requested.StackId);
            created.Add(new LeasedItemReservation(
                owner,
                requested.StackId,
                stack.StackSignature,
                requested.Quantity,
                now,
                now + ItemLeaseHours,
                now + MaximumLeaseHours));
        }
        if (!reservations.TryReserve(ids, owner))
        {
            failure = new DomainFailure(FailureCode.ApparelItemReserved, owner);
            return false;
        }
        byOwner.Add(owner, created);
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
        Dictionary<string, WorldItemStackSnapshot> current = items.GetAllStacks()
            .Where(value => value != null)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        float now = GameHour;
        for (int index = 0; index < leases.Count; index++)
        {
            LeasedItemReservation lease = leases[index];
            if (now > lease.ExpiresGameHour
                || !current.TryGetValue(lease.StackId, out WorldItemStackSnapshot stack)
                || stack.Quantity < lease.ExpectedMinimumQuantity
                || !string.Equals(stack.StackSignature, lease.ExpectedSignature, StringComparison.Ordinal)
                || !string.Equals(stack.ReservedByPersistentId, owner, StringComparison.Ordinal))
            {
                Release(owner);
                failure = new DomainFailure(
                    FailureCode.ApparelReservationExpired,
                    lease.StackId);
                return false;
            }
            if (renewForTransportProgress && now < lease.MaximumGameHour)
            {
                leases[index] = new LeasedItemReservation(
                    lease.OwnerId,
                    lease.StackId,
                    lease.ExpectedSignature,
                    lease.ExpectedMinimumQuantity,
                    lease.CreatedGameHour,
                    Math.Min(lease.MaximumGameHour, now + ItemLeaseHours),
                    lease.MaximumGameHour);
            }
        }
        return true;
    }

    public void Release(string ownerId)
    {
        string owner = ownerId?.Trim() ?? string.Empty;
        if (!byOwner.Remove(owner, out List<LeasedItemReservation> leases))
        {
            return;
        }
        foreach (LeasedItemReservation lease in leases)
        {
            reservations.Release(lease.StackId, owner);
        }
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
}
