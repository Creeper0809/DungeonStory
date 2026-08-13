using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum ItemReservationReleaseReason
{
    Completed,
    Cancelled,
    Expired,
    StackInvalidated,
    OwnerRemoved,
    WorldRestore,
    Replanned
}

[Serializable]
public sealed class ItemLeaseSlice
{
    public string stackId = string.Empty;
    public string originStackId = string.Empty;
    public string expectedStackSignature = string.Empty;
    public int quantity;

    public ItemLeaseSlice Clone() => new()
    {
        stackId = stackId,
        originStackId = originStackId,
        expectedStackSignature = expectedStackSignature,
        quantity = quantity
    };
}

[Serializable]
public sealed class ItemQuantityLease
{
    public string leaseId = string.Empty;
    public string ownerOperationId = string.Empty;
    public string ownerCharacterId = string.Empty;
    public ItemReservationPurpose purpose;
    public string aggregationCohortId = string.Empty;
    public int originalQuantity;
    public int remainingQuantity;
    public List<ItemLeaseSlice> slices = new();
    public double createdAtGameSeconds;
    public double expiresAtGameSeconds;
    public double maximumExpiresAtGameSeconds;

    public ItemQuantityLease Clone() => new()
    {
        leaseId = leaseId,
        ownerOperationId = ownerOperationId,
        ownerCharacterId = ownerCharacterId,
        purpose = purpose,
        aggregationCohortId = aggregationCohortId,
        originalQuantity = originalQuantity,
        remainingQuantity = remainingQuantity,
        slices = (slices ?? new List<ItemLeaseSlice>())
            .Where(slice => slice != null)
            .Select(slice => slice.Clone())
            .ToList(),
        createdAtGameSeconds = createdAtGameSeconds,
        expiresAtGameSeconds = expiresAtGameSeconds,
        maximumExpiresAtGameSeconds = maximumExpiresAtGameSeconds
    };
}

public readonly struct ItemQuantityReservationRequest
{
    public ItemQuantityReservationRequest(
        ItemStackId stackId,
        int quantity,
        string expectedStackSignature)
    {
        StackId = stackId;
        Quantity = quantity;
        ExpectedStackSignature = expectedStackSignature?.Trim() ?? string.Empty;
    }

    public ItemStackId StackId { get; }
    public int Quantity { get; }
    public string ExpectedStackSignature { get; }
    public bool IsValid => StackId.IsValid
        && Quantity > 0
        && ExpectedStackSignature.Length > 0;
}

public sealed class ItemReservationRestoreDiagnostics
{
    public int GrandfatherOperationCount { get; internal set; }
    public int RestoredLeaseCount { get; internal set; }
    public int ClaimedStackCount { get; internal set; }
    public int RestoredQuantity { get; internal set; }
    public int HintlessOperationCount { get; internal set; }
    public int PriorityReplanCount { get; internal set; }
    public int BlockedReservationAttempts { get; internal set; }
}

public interface IItemQuantityReservationService
{
    ItemReservationRestoreDiagnostics LastRestoreDiagnostics { get; }
    bool TryReserve(
        string ownerOperationId,
        string ownerCharacterId,
        ItemReservationPurpose purpose,
        string aggregationCohortId,
        ItemQuantityReservationRequest request,
        out ItemQuantityLease lease,
        out DomainFailure failure);

    bool TryReserveBatch(
        string ownerOperationId,
        string ownerCharacterId,
        ItemReservationPurpose purpose,
        string aggregationCohortId,
        IReadOnlyList<ItemQuantityReservationRequest> requests,
        out IReadOnlyList<ItemQuantityLease> leases,
        out DomainFailure failure);

    bool Revalidate(
        string leaseId,
        out ItemQuantityLease lease,
        out DomainFailure failure);

    bool Renew(
        string leaseId,
        double requestedUntilGameSeconds,
        out DomainFailure failure);

    bool Release(string leaseId, ItemReservationReleaseReason reason);
    int ReleaseByOwner(string ownerOperationId, ItemReservationReleaseReason reason);
    bool TryGetLeasesByOwner(
        string ownerOperationId,
        out IReadOnlyList<ItemQuantityLease> leases);
    IReadOnlyList<ItemQuantityLease> GetLeasesForStack(ItemStackId stackId);
    int GetReservedQuantity(ItemStackId stackId);
    int GetAvailableQuantity(ItemStackId stackId);
}

public interface IItemQuantityLeaseMutation
{
    bool TryConsumeSlices(
        string leaseId,
        int quantity,
        out IReadOnlyList<ItemLeaseSlice> consumedSlices,
        out DomainFailure failure);

    bool TryRetargetSlices(
        IReadOnlyDictionary<string, IReadOnlyList<ItemLeaseSlice>> replacementsByLeaseId,
        out DomainFailure failure);

    int InvalidateStack(string stackId, ItemReservationReleaseReason reason);
    void ResetTransientLedger();
}

public interface IItemQuantityReservationPersistence
{
    IReadOnlyList<ItemReservationIntentSaveData> CaptureReservationIntents();
    bool TryRestoreGrandfathered(
        IReadOnlyList<ItemReservationIntentSaveData> intents,
        out DomainFailure failure);
}

public interface IItemReservationMutationGate
{
    bool IsCaptureBarrierActive { get; }
    bool IsRestoreBarrierActive { get; }
    bool BlocksNewReservations { get; }
    IDisposable EnterCaptureBarrier();
    IDisposable EnterRestoreBarrier();
}

/// <summary>
/// Transient, indexed ownership ledger for exact quantities in physical item stacks.
/// Physical records remain the quantity authority; this service owns reservation state only.
/// </summary>
public sealed class ItemQuantityReservationService :
    IItemQuantityReservationService,
    IItemQuantityLeaseMutation,
    IItemQuantityReservationPersistence,
    IItemReservationMutationGate
{
    private const double DefaultLeaseSeconds = 15d;
    private const double MaximumLeaseSeconds = 45d;

    private readonly WorldItemRepository repository;
    private readonly IItemMarkerPresenter markers;
    private readonly IGameClock clock;
    private readonly Dictionary<string, ItemQuantityLease> leasesById =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> leasesByOwnerOperationId =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> leaseIdsByStackId =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> reservedQuantityByStackId =
        new(StringComparer.Ordinal);
    private long leaseSequence;
    private int captureBarrierDepth;
    private int restoreBarrierDepth;
    private int blockedReservationAttemptsDuringRestore;

    public ItemReservationRestoreDiagnostics LastRestoreDiagnostics { get; private set; } =
        new();

    public ItemQuantityReservationService(
        WorldItemRepository repository,
        IItemMarkerPresenter markers,
        IGameClock clock)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public bool IsCaptureBarrierActive => captureBarrierDepth > 0;
    public bool IsRestoreBarrierActive => restoreBarrierDepth > 0;
    public bool BlocksNewReservations =>
        IsCaptureBarrierActive || IsRestoreBarrierActive;

    public IDisposable EnterCaptureBarrier()
    {
        captureBarrierDepth++;
        return new MutationGateScope(() => captureBarrierDepth--);
    }

    public IDisposable EnterRestoreBarrier()
    {
        restoreBarrierDepth++;
        return new MutationGateScope(() => restoreBarrierDepth--);
    }

    public bool TryReserve(
        string ownerOperationId,
        string ownerCharacterId,
        ItemReservationPurpose purpose,
        string aggregationCohortId,
        ItemQuantityReservationRequest request,
        out ItemQuantityLease lease,
        out DomainFailure failure)
    {
        lease = null;
        if (!TryReserveBatch(
                ownerOperationId,
                ownerCharacterId,
                purpose,
                aggregationCohortId,
                new[] { request },
                out IReadOnlyList<ItemQuantityLease> leases,
                out failure))
        {
            return false;
        }
        lease = leases[0];
        return true;
    }

    public bool TryReserveBatch(
        string ownerOperationId,
        string ownerCharacterId,
        ItemReservationPurpose purpose,
        string aggregationCohortId,
        IReadOnlyList<ItemQuantityReservationRequest> requests,
        out IReadOnlyList<ItemQuantityLease> leases,
        out DomainFailure failure)
    {
        leases = Array.Empty<ItemQuantityLease>();
        failure = DomainFailure.None;
        string operationId = ownerOperationId?.Trim() ?? string.Empty;
        string characterId = ownerCharacterId?.Trim() ?? string.Empty;
        string cohortId = aggregationCohortId?.Trim() ?? string.Empty;
        if (BlocksNewReservations)
        {
            if (IsRestoreBarrierActive)
                blockedReservationAttemptsDuringRestore++;
            failure = new DomainFailure(
                FailureCode.ItemReservationRestoreConflict,
                operationId,
                IsRestoreBarrierActive
                    ? "restore-in-progress"
                    : "save-capture-in-progress");
            return false;
        }
        if (operationId.Length == 0 || requests == null || requests.Count == 0)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationRequestInvalid,
                operationId);
            return false;
        }

        List<NormalizedRequest> normalized = NormalizeRequests(requests, out failure);
        if (normalized == null)
            return false;

        if (leasesByOwnerOperationId.TryGetValue(
                operationId,
                out List<string> existingIds))
        {
            if (!MatchesExisting(
                    existingIds,
                    purpose,
                    cohortId,
                    normalized,
                    out leases))
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationOperationConflict,
                    operationId);
                return false;
            }
            return true;
        }

        List<(WorldItemStackRecord record, NormalizedRequest request)> resolved =
            new(normalized.Count);
        foreach (NormalizedRequest request in normalized)
        {
            if (!repository.RecordsById.TryGetValue(
                    request.StackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.quantity <= 0)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationStackMissing,
                    request.StackId);
                return false;
            }
            if (record.forbidden)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationStackForbidden,
                    request.StackId);
                return false;
            }
            string signature = ItemReservationSignature.Create(
                record.itemId,
                record.components);
            if (!string.Equals(
                    signature,
                    request.ExpectedSignature,
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationSignatureMismatch,
                    request.StackId);
                return false;
            }
            int available = record.quantity - GetCachedReserved(request.StackId);
            if (available < request.Quantity)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationQuantityUnavailable,
                    request.StackId);
                return false;
            }
            resolved.Add((record, request));
        }

        double now = Math.Max(0d, clock.Time);
        List<ItemQuantityLease> created = new(resolved.Count);
        foreach ((WorldItemStackRecord record, NormalizedRequest request) in resolved)
        {
            ItemQuantityLease createdLease = new()
            {
                leaseId = AllocateLeaseId(operationId),
                ownerOperationId = operationId,
                ownerCharacterId = characterId,
                purpose = purpose,
                aggregationCohortId = cohortId,
                originalQuantity = request.Quantity,
                remainingQuantity = request.Quantity,
                slices = new List<ItemLeaseSlice>
                {
                    new()
                    {
                        stackId = request.StackId,
                        originStackId = request.StackId,
                        expectedStackSignature = request.ExpectedSignature,
                        quantity = request.Quantity
                    }
                },
                createdAtGameSeconds = now,
                expiresAtGameSeconds = now + DefaultLeaseSeconds,
                maximumExpiresAtGameSeconds = now + MaximumLeaseSeconds
            };
            RegisterLease(createdLease);
            TouchReservation(record);
            created.Add(createdLease.Clone());
        }
        repository.MarkChanged();
        RefreshPositions(resolved.Select(value => value.record.position));
        leases = created;
        return true;
    }

    public bool Revalidate(
        string leaseId,
        out ItemQuantityLease lease,
        out DomainFailure failure)
    {
        lease = null;
        failure = DomainFailure.None;
        string id = leaseId?.Trim() ?? string.Empty;
        if (!leasesById.TryGetValue(id, out ItemQuantityLease current))
        {
            failure = new DomainFailure(FailureCode.ItemReservationLeaseMissing, id);
            return false;
        }
        if (clock.Time > current.expiresAtGameSeconds)
        {
            Release(id, ItemReservationReleaseReason.Expired);
            failure = new DomainFailure(FailureCode.ItemReservationLeaseExpired, id);
            return false;
        }
        foreach (ItemLeaseSlice slice in current.slices)
        {
            if (slice == null
                || slice.quantity <= 0
                || !repository.RecordsById.TryGetValue(
                    slice.stackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.quantity < GetCachedReserved(slice.stackId)
                || !string.Equals(
                    ItemReservationSignature.Create(record.itemId, record.components),
                    slice.expectedStackSignature,
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationSliceInvalid,
                    id,
                    slice?.stackId ?? string.Empty);
                return false;
            }
        }
        lease = current.Clone();
        return true;
    }

    public bool Renew(
        string leaseId,
        double requestedUntilGameSeconds,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string id = leaseId?.Trim() ?? string.Empty;
        if (!leasesById.TryGetValue(id, out ItemQuantityLease lease))
        {
            failure = new DomainFailure(FailureCode.ItemReservationLeaseMissing, id);
            return false;
        }
        double now = Math.Max(0d, clock.Time);
        if (now > lease.expiresAtGameSeconds)
        {
            Release(id, ItemReservationReleaseReason.Expired);
            failure = new DomainFailure(FailureCode.ItemReservationLeaseExpired, id);
            return false;
        }
        lease.expiresAtGameSeconds = Math.Min(
            lease.maximumExpiresAtGameSeconds,
            Math.Max(lease.expiresAtGameSeconds, requestedUntilGameSeconds));
        return true;
    }

    public bool Release(string leaseId, ItemReservationReleaseReason reason)
    {
        string id = leaseId?.Trim() ?? string.Empty;
        if (!leasesById.Remove(id, out ItemQuantityLease lease))
            return false;

        HashSet<Vector2Int> positions = new();
        foreach (ItemLeaseSlice slice in lease.slices ?? new List<ItemLeaseSlice>())
        {
            if (slice == null || slice.quantity <= 0)
                continue;
            RemoveSliceIndex(id, slice.stackId, slice.quantity);
            if (repository.RecordsById.TryGetValue(
                    slice.stackId,
                    out WorldItemStackRecord record)
                && record != null)
            {
                TouchReservation(record);
                positions.Add(record.position);
            }
        }
        if (leasesByOwnerOperationId.TryGetValue(
                lease.ownerOperationId,
                out List<string> ownerLeases))
        {
            ownerLeases.Remove(id);
            if (ownerLeases.Count == 0)
                leasesByOwnerOperationId.Remove(lease.ownerOperationId);
        }
        repository.MarkChanged();
        RefreshPositions(positions);
        return true;
    }

    public int ReleaseByOwner(
        string ownerOperationId,
        ItemReservationReleaseReason reason)
    {
        string owner = ownerOperationId?.Trim() ?? string.Empty;
        if (!leasesByOwnerOperationId.TryGetValue(owner, out List<string> ids))
            return 0;
        string[] copy = ids.ToArray();
        int released = 0;
        foreach (string id in copy)
            if (Release(id, reason)) released++;
        return released;
    }

    public bool TryGetLeasesByOwner(
        string ownerOperationId,
        out IReadOnlyList<ItemQuantityLease> leases)
    {
        string owner = ownerOperationId?.Trim() ?? string.Empty;
        if (!leasesByOwnerOperationId.TryGetValue(owner, out List<string> ids))
        {
            leases = Array.Empty<ItemQuantityLease>();
            return false;
        }
        leases = ids
            .Where(leasesById.ContainsKey)
            .Select(id => leasesById[id].Clone())
            .ToArray();
        return leases.Count > 0;
    }

    public IReadOnlyList<ItemQuantityLease> GetLeasesForStack(ItemStackId stackId)
    {
        if (!stackId.IsValid
            || !leaseIdsByStackId.TryGetValue(
                stackId.Value,
                out HashSet<string> ids))
        {
            return Array.Empty<ItemQuantityLease>();
        }
        return ids
            .Where(leasesById.ContainsKey)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => leasesById[id].Clone())
            .ToArray();
    }

    public int GetReservedQuantity(ItemStackId stackId) =>
        GetCachedReserved(stackId.Value);

    public int GetAvailableQuantity(ItemStackId stackId)
    {
        if (!repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null)
        {
            return 0;
        }
        return Math.Max(0, record.quantity - GetCachedReserved(stackId.Value));
    }

    public bool TryConsumeSlices(
        string leaseId,
        int quantity,
        out IReadOnlyList<ItemLeaseSlice> consumedSlices,
        out DomainFailure failure)
    {
        consumedSlices = Array.Empty<ItemLeaseSlice>();
        failure = DomainFailure.None;
        if (quantity <= 0
            || !Revalidate(leaseId, out ItemQuantityLease snapshot, out failure)
            || snapshot.remainingQuantity < quantity
            || !leasesById.TryGetValue(snapshot.leaseId, out ItemQuantityLease lease))
        {
            if (!failure.IsFailure)
                failure = new DomainFailure(
                    FailureCode.ItemReservationQuantityUnavailable,
                    leaseId ?? string.Empty);
            return false;
        }

        int remaining = quantity;
        List<ItemLeaseSlice> consumed = new();
        HashSet<Vector2Int> positions = new();
        for (int index = 0; index < lease.slices.Count && remaining > 0; index++)
        {
            ItemLeaseSlice slice = lease.slices[index];
            int take = Math.Min(remaining, slice.quantity);
            if (take <= 0) continue;
            consumed.Add(new ItemLeaseSlice
            {
                stackId = slice.stackId,
                originStackId = string.IsNullOrWhiteSpace(slice.originStackId)
                    ? slice.stackId
                    : slice.originStackId,
                expectedStackSignature = slice.expectedStackSignature,
                quantity = take
            });
            slice.quantity -= take;
            remaining -= take;
            RemoveSliceIndex(
                lease.leaseId,
                slice.stackId,
                take,
                keepLeaseMembership: slice.quantity > 0);
            if (repository.RecordsById.TryGetValue(
                    slice.stackId,
                    out WorldItemStackRecord record))
            {
                TouchReservation(record);
                positions.Add(record.position);
            }
        }
        lease.slices.RemoveAll(slice => slice == null || slice.quantity <= 0);
        lease.remainingQuantity -= quantity;
        if (lease.remainingQuantity == 0)
            RemoveEmptyLease(lease);
        repository.MarkChanged();
        RefreshPositions(positions);
        consumedSlices = consumed;
        return true;
    }

    public bool TryRetargetSlices(
        IReadOnlyDictionary<string, IReadOnlyList<ItemLeaseSlice>> replacementsByLeaseId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (replacementsByLeaseId == null || replacementsByLeaseId.Count == 0)
            return true;

        Dictionary<string, List<ItemLeaseSlice>> normalized = new(StringComparer.Ordinal);
        Dictionary<string, int> newTotalsByStack = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, IReadOnlyList<ItemLeaseSlice>> pair in replacementsByLeaseId)
        {
            if (!leasesById.TryGetValue(pair.Key, out ItemQuantityLease lease))
            {
                failure = new DomainFailure(FailureCode.ItemReservationLeaseMissing, pair.Key);
                return false;
            }
            List<ItemLeaseSlice> replacements = (pair.Value ?? Array.Empty<ItemLeaseSlice>())
                .Where(slice => slice != null && slice.quantity > 0)
                .Select(slice => slice.Clone())
                .ToList();
            if (replacements.Sum(slice => slice.quantity) != lease.remainingQuantity)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationSliceInvalid,
                    pair.Key,
                    string.Empty);
                return false;
            }
            foreach (ItemLeaseSlice slice in replacements)
            {
                if (!repository.RecordsById.TryGetValue(
                        slice.stackId,
                        out WorldItemStackRecord record)
                    || record == null
                    || !string.Equals(
                        ItemReservationSignature.Create(record.itemId, record.components),
                        slice.expectedStackSignature,
                        StringComparison.Ordinal))
                {
                    failure = new DomainFailure(
                        FailureCode.ItemReservationSignatureMismatch,
                        slice.stackId);
                    return false;
                }
                newTotalsByStack[slice.stackId] =
                    newTotalsByStack.TryGetValue(slice.stackId, out int total)
                        ? checked(total + slice.quantity)
                        : slice.quantity;
            }
            normalized.Add(pair.Key, replacements);
        }

        Dictionary<string, int> releasedByStack = new(StringComparer.Ordinal);
        foreach (string leaseId in normalized.Keys)
        {
            foreach (ItemLeaseSlice oldSlice in leasesById[leaseId].slices)
            {
                releasedByStack[oldSlice.stackId] =
                    releasedByStack.TryGetValue(oldSlice.stackId, out int total)
                        ? checked(total + oldSlice.quantity)
                        : oldSlice.quantity;
            }
        }
        foreach (KeyValuePair<string, int> pair in newTotalsByStack)
        {
            WorldItemStackRecord record = repository.RecordsById[pair.Key];
            int unchangedReservations = GetCachedReserved(pair.Key)
                - (releasedByStack.TryGetValue(pair.Key, out int released) ? released : 0);
            if (record.quantity - Math.Max(0, unchangedReservations) < pair.Value)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationQuantityUnavailable,
                    pair.Key);
                return false;
            }
        }

        HashSet<string> touchedStacks = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<ItemLeaseSlice>> pair in normalized)
        {
            ItemQuantityLease lease = leasesById[pair.Key];
            foreach (ItemLeaseSlice oldSlice in lease.slices)
            {
                RemoveSliceIndex(pair.Key, oldSlice.stackId, oldSlice.quantity);
                touchedStacks.Add(oldSlice.stackId);
            }
            lease.slices = pair.Value;
            foreach (ItemLeaseSlice newSlice in lease.slices)
            {
                AddSliceIndex(pair.Key, newSlice.stackId, newSlice.quantity);
                touchedStacks.Add(newSlice.stackId);
            }
        }
        TouchStacks(touchedStacks);
        return true;
    }

    public int InvalidateStack(
        string stackId,
        ItemReservationReleaseReason reason)
    {
        string id = stackId?.Trim() ?? string.Empty;
        if (!leaseIdsByStackId.TryGetValue(id, out HashSet<string> leases))
            return 0;
        string[] copy = leases.ToArray();
        int released = 0;
        foreach (string leaseId in copy)
            if (Release(leaseId, reason)) released++;
        return released;
    }

    public void ResetTransientLedger()
    {
        foreach (WorldItemStackRecord record in repository.Records)
        {
            if (record == null) continue;
            record.reservedQuantity = 0;
            record.reservationRevision++;
        }
        leasesById.Clear();
        leasesByOwnerOperationId.Clear();
        leaseIdsByStackId.Clear();
        reservedQuantityByStackId.Clear();
        repository.MarkChanged();
    }

    public IReadOnlyList<ItemReservationIntentSaveData> CaptureReservationIntents()
    {
        List<ItemReservationIntentSaveData> result = new();
        foreach (KeyValuePair<string, List<string>> owner in
                 leasesByOwnerOperationId.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            List<ItemQuantityLease> active = owner.Value
                .Where(leasesById.ContainsKey)
                .Select(id => leasesById[id])
                .Where(lease => clock.Time <= lease.expiresAtGameSeconds)
                .OrderBy(lease => lease.slices[0].stackId, StringComparer.Ordinal)
                .ThenBy(lease => lease.leaseId, StringComparer.Ordinal)
                .ToList();
            if (active.Count == 0)
                continue;
            ItemReservationIntentSaveData intent = new()
            {
                ownerOperationId = owner.Key,
                ownerCharacterId = active[0].ownerCharacterId,
                hadActiveItemReservation = true
            };
            int ordinal = 0;
            foreach (ItemQuantityLease lease in active)
            {
                foreach (ItemLeaseSlice slice in lease.slices
                             .Where(value => value != null && value.quantity > 0)
                             .OrderBy(value => value.stackId, StringComparer.Ordinal))
                {
                    repository.RecordsById.TryGetValue(
                        slice.stackId,
                        out WorldItemStackRecord record);
                    intent.reservationHints.Add(new ItemReservationClaimHintSaveData
                    {
                        claimHintId = $"claim:{owner.Key}:{ordinal}",
                        originStackId = string.IsNullOrWhiteSpace(slice.originStackId)
                            ? slice.stackId
                            : slice.originStackId,
                        preferredPhysicalStackId = slice.stackId,
                        itemId = record?.itemId ?? string.Empty,
                        expectedStackSignature = slice.expectedStackSignature,
                        quantity = slice.quantity,
                        purpose = lease.purpose,
                        aggregationCohortId = lease.aggregationCohortId,
                        claimOrdinal = ordinal++
                    });
                }
            }
            result.Add(intent);
        }
        return result;
    }

    public bool TryRestoreGrandfathered(
        IReadOnlyList<ItemReservationIntentSaveData> intents,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        int hintlessOperationCount = (intents
                ?? Array.Empty<ItemReservationIntentSaveData>())
            .Count(intent => intent != null && !intent.hadActiveItemReservation);
        ItemReservationIntentSaveData[] ordered = (intents
                ?? Array.Empty<ItemReservationIntentSaveData>())
            .Where(intent => intent != null && intent.hadActiveItemReservation)
            .OrderBy(intent => intent.ownerOperationId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, int> totalsByStack = new(StringComparer.Ordinal);
        HashSet<string> owners = new(StringComparer.Ordinal);
        HashSet<string> claimIds = new(StringComparer.Ordinal);
        foreach (ItemReservationIntentSaveData intent in ordered)
        {
            if (string.IsNullOrWhiteSpace(intent.ownerOperationId)
                || !owners.Add(intent.ownerOperationId)
                || intent.reservationHints == null
                || intent.reservationHints.Count == 0)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationRestoreConflict,
                    intent?.ownerOperationId ?? string.Empty,
                    string.Empty);
                return false;
            }
            foreach (ItemReservationClaimHintSaveData hint in intent.reservationHints
                         .OrderBy(value => value.preferredPhysicalStackId, StringComparer.Ordinal)
                         .ThenBy(value => value.claimOrdinal))
            {
                if (hint == null
                    || hint.quantity <= 0
                    || string.IsNullOrWhiteSpace(hint.originStackId)
                    || !claimIds.Add(hint.claimHintId ?? string.Empty)
                    || !repository.RecordsById.TryGetValue(
                        hint.preferredPhysicalStackId,
                        out WorldItemStackRecord record)
                    || !string.Equals(record.itemId, hint.itemId, StringComparison.Ordinal)
                    || !string.Equals(
                        ItemReservationSignature.Create(record.itemId, record.components),
                        hint.expectedStackSignature,
                        StringComparison.Ordinal))
                {
                    failure = new DomainFailure(
                        FailureCode.ItemReservationRestoreConflict,
                        intent.ownerOperationId,
                        hint?.preferredPhysicalStackId ?? string.Empty);
                    return false;
                }
                totalsByStack[record.stackId] =
                    totalsByStack.TryGetValue(record.stackId, out int total)
                        ? checked(total + hint.quantity)
                        : hint.quantity;
            }
        }
        foreach (KeyValuePair<string, int> total in totalsByStack)
        {
            if (repository.RecordsById[total.Key].quantity < total.Value)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationRestoreConflict,
                    total.Key,
                    total.Value.ToString());
                return false;
            }
        }

        ResetTransientLedger();
        double now = Math.Max(0d, clock.Time);
        var orderedClaims = ordered
            .SelectMany(intent => intent.reservationHints.Select(hint => new
            {
                Intent = intent,
                Hint = hint
            }))
            .OrderBy(value => value.Hint.preferredPhysicalStackId, StringComparer.Ordinal)
            .ThenBy(value => value.Intent.ownerOperationId, StringComparer.Ordinal)
            .ThenBy(value => value.Hint.claimOrdinal)
            .ToArray();
        foreach (var claim in orderedClaims)
        {
            ItemReservationIntentSaveData intent = claim.Intent;
            ItemReservationClaimHintSaveData hint = claim.Hint;
                ItemQuantityLease lease = new()
                {
                    leaseId = AllocateLeaseId(intent.ownerOperationId),
                    ownerOperationId = intent.ownerOperationId,
                    ownerCharacterId = intent.ownerCharacterId?.Trim() ?? string.Empty,
                    purpose = hint.purpose,
                    aggregationCohortId = hint.aggregationCohortId?.Trim() ?? string.Empty,
                    originalQuantity = hint.quantity,
                    remainingQuantity = hint.quantity,
                    slices = new List<ItemLeaseSlice>
                    {
                        new()
                        {
                            stackId = hint.preferredPhysicalStackId,
                            originStackId = string.IsNullOrWhiteSpace(hint.originStackId)
                                ? hint.preferredPhysicalStackId
                                : hint.originStackId,
                            expectedStackSignature = hint.expectedStackSignature,
                            quantity = hint.quantity
                        }
                    },
                    createdAtGameSeconds = now,
                    expiresAtGameSeconds = now + DefaultLeaseSeconds,
                    maximumExpiresAtGameSeconds = now + MaximumLeaseSeconds
                };
                RegisterLease(lease);
                TouchReservation(repository.RecordsById[hint.preferredPhysicalStackId]);
        }
        repository.MarkChanged();
        RefreshPositions(totalsByStack.Keys.Select(id => repository.RecordsById[id].position));
        LastRestoreDiagnostics = new ItemReservationRestoreDiagnostics
        {
            GrandfatherOperationCount = ordered.Length,
            RestoredLeaseCount = orderedClaims.Length,
            ClaimedStackCount = totalsByStack.Count,
            RestoredQuantity = totalsByStack.Values.Sum(),
            HintlessOperationCount = hintlessOperationCount,
            PriorityReplanCount = hintlessOperationCount,
            BlockedReservationAttempts = blockedReservationAttemptsDuringRestore
        };
        blockedReservationAttemptsDuringRestore = 0;
        return true;
    }

    private List<NormalizedRequest> NormalizeRequests(
        IReadOnlyList<ItemQuantityReservationRequest> requests,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        Dictionary<string, NormalizedRequest> byStack = new(StringComparer.Ordinal);
        foreach (ItemQuantityReservationRequest request in requests)
        {
            if (!request.IsValid)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationRequestInvalid,
                    string.Empty);
                return null;
            }
            string stackId = request.StackId.Value;
            if (byStack.TryGetValue(stackId, out NormalizedRequest existing))
            {
                if (!string.Equals(
                        existing.ExpectedSignature,
                        request.ExpectedStackSignature,
                        StringComparison.Ordinal))
                {
                    failure = new DomainFailure(
                        FailureCode.ItemReservationSignatureMismatch,
                        stackId);
                    return null;
                }
                byStack[stackId] = new NormalizedRequest(
                    stackId,
                    checked(existing.Quantity + request.Quantity),
                    existing.ExpectedSignature);
            }
            else
            {
                byStack.Add(stackId, new NormalizedRequest(
                    stackId,
                    request.Quantity,
                    request.ExpectedStackSignature));
            }
        }
        return byStack.Values
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToList();
    }

    private bool MatchesExisting(
        IReadOnlyList<string> existingIds,
        ItemReservationPurpose purpose,
        string cohortId,
        IReadOnlyList<NormalizedRequest> requests,
        out IReadOnlyList<ItemQuantityLease> leases)
    {
        leases = Array.Empty<ItemQuantityLease>();
        List<ItemQuantityLease> existing = existingIds
            .Where(leasesById.ContainsKey)
            .Select(id => leasesById[id])
            .OrderBy(value => value.slices[0].stackId, StringComparer.Ordinal)
            .ToList();
        if (existing.Count != requests.Count)
            return false;
        for (int index = 0; index < existing.Count; index++)
        {
            ItemQuantityLease lease = existing[index];
            NormalizedRequest request = requests[index];
            if (lease.purpose != purpose
                || !string.Equals(lease.aggregationCohortId, cohortId, StringComparison.Ordinal)
                || lease.remainingQuantity != request.Quantity
                || lease.slices.Count != 1
                || !string.Equals(lease.slices[0].stackId, request.StackId, StringComparison.Ordinal)
                || !string.Equals(
                    lease.slices[0].expectedStackSignature,
                    request.ExpectedSignature,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        leases = existing.Select(value => value.Clone()).ToArray();
        return true;
    }

    private void RegisterLease(ItemQuantityLease lease)
    {
        leasesById.Add(lease.leaseId, lease);
        if (!leasesByOwnerOperationId.TryGetValue(
                lease.ownerOperationId,
                out List<string> ownerLeases))
        {
            ownerLeases = new List<string>();
            leasesByOwnerOperationId.Add(lease.ownerOperationId, ownerLeases);
        }
        ownerLeases.Add(lease.leaseId);
        foreach (ItemLeaseSlice slice in lease.slices)
            AddSliceIndex(lease.leaseId, slice.stackId, slice.quantity);
    }

    private void AddSliceIndex(string leaseId, string stackId, int quantity)
    {
        if (!leaseIdsByStackId.TryGetValue(stackId, out HashSet<string> ids))
        {
            ids = new HashSet<string>(StringComparer.Ordinal);
            leaseIdsByStackId.Add(stackId, ids);
        }
        ids.Add(leaseId);
        int total = checked(GetCachedReserved(stackId) + quantity);
        reservedQuantityByStackId[stackId] = total;
        if (repository.RecordsById.TryGetValue(stackId, out WorldItemStackRecord record))
            record.reservedQuantity = total;
    }

    private void RemoveSliceIndex(
        string leaseId,
        string stackId,
        int quantity,
        bool keepLeaseMembership = false)
    {
        int total = Math.Max(0, GetCachedReserved(stackId) - quantity);
        if (total == 0) reservedQuantityByStackId.Remove(stackId);
        else reservedQuantityByStackId[stackId] = total;
        if (repository.RecordsById.TryGetValue(stackId, out WorldItemStackRecord record))
            record.reservedQuantity = total;
        if (!keepLeaseMembership
            && leaseIdsByStackId.TryGetValue(stackId, out HashSet<string> ids))
        {
            ids.Remove(leaseId);
            if (ids.Count == 0) leaseIdsByStackId.Remove(stackId);
        }
    }

    private void RemoveEmptyLease(ItemQuantityLease lease)
    {
        leasesById.Remove(lease.leaseId);
        if (leasesByOwnerOperationId.TryGetValue(
                lease.ownerOperationId,
                out List<string> ids))
        {
            ids.Remove(lease.leaseId);
            if (ids.Count == 0)
                leasesByOwnerOperationId.Remove(lease.ownerOperationId);
        }
        foreach (HashSet<string> stackLeases in leaseIdsByStackId.Values)
            stackLeases.Remove(lease.leaseId);
    }

    private sealed class MutationGateScope : IDisposable
    {
        private Action release;

        public MutationGateScope(Action release)
        {
            this.release = release;
        }

        public void Dispose()
        {
            Action callback = release;
            release = null;
            callback?.Invoke();
        }
    }

    private int GetCachedReserved(string stackId) =>
        reservedQuantityByStackId.TryGetValue(stackId, out int quantity)
            ? quantity
            : 0;

    private string AllocateLeaseId(string operationId) =>
        $"lease:{operationId}:{++leaseSequence}";

    private static void TouchReservation(WorldItemStackRecord record)
    {
        if (record == null) return;
        record.reservationRevision++;
    }

    private void TouchStacks(IEnumerable<string> stackIds)
    {
        HashSet<Vector2Int> positions = new();
        foreach (string stackId in stackIds)
        {
            if (!repository.RecordsById.TryGetValue(
                    stackId,
                    out WorldItemStackRecord record))
                continue;
            TouchReservation(record);
            positions.Add(record.position);
        }
        repository.MarkChanged();
        RefreshPositions(positions);
    }

    private void RefreshPositions(IEnumerable<Vector2Int> positions)
    {
        foreach (Vector2Int position in positions.Distinct())
            markers.RefreshAt(position);
    }

    private readonly struct NormalizedRequest
    {
        public NormalizedRequest(
            string stackId,
            int quantity,
            string expectedSignature)
        {
            StackId = stackId;
            Quantity = quantity;
            ExpectedSignature = expectedSignature;
        }

        public string StackId { get; }
        public int Quantity { get; }
        public string ExpectedSignature { get; }
    }
}
