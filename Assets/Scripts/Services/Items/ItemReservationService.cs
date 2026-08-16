using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IItemReservationService
{
    bool TryReserve(IEnumerable<string> stackIds, string persistentId);
    bool TryReserveQuantities(
        IEnumerable<ReservedItemConsumption> quantities,
        string ownerOperationId,
        ItemReservationPurpose purpose,
        string aggregationCohortId);
    void Release(string stackId, string persistentId);
    bool TryClear(string stackId);
    bool SetForbidden(string stackId, bool forbidden);
    bool PrioritizeHaul(string stackId);
}

public sealed class ItemReservationService : IItemReservationService
{
    private readonly WorldItemRepository repository;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly Dictionary<string, string> leaseByOwnerAndStack =
        new(StringComparer.Ordinal);

    public ItemReservationService(
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter,
        IItemQuantityReservationService quantityReservations)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.markerPresenter = markerPresenter
            ?? throw new ArgumentNullException(nameof(markerPresenter));
        this.quantityReservations = quantityReservations
            ?? throw new ArgumentNullException(nameof(quantityReservations));
    }

    internal IItemQuantityReservationService QuantityReservations =>
        quantityReservations;

    public bool TryReserve(IEnumerable<string> stackIds, string persistentId)
    {
        if (stackIds == null || string.IsNullOrWhiteSpace(persistentId))
        {
            return false;
        }

        string owner = persistentId.Trim();
        List<WorldItemStackRecord> selected = new();
        foreach (string stackId in stackIds)
        {
            if (string.IsNullOrWhiteSpace(stackId)
                || !repository.RecordsById.TryGetValue(
                    stackId,
                    out WorldItemStackRecord record)
                || record == null)
            {
                return false;
            }

            selected.Add(record);
        }

        if (selected.Count == 0)
        {
            return false;
        }

        return TryReserveQuantities(
            selected.Select(record => new ReservedItemConsumption(
                record.stackId,
                record.quantity)),
            owner,
            ItemReservationPurpose.DirectPlayerOrder,
            $"legacy:{owner}");
    }

    public bool TryReserveQuantities(
        IEnumerable<ReservedItemConsumption> quantities,
        string ownerOperationId,
        ItemReservationPurpose purpose,
        string aggregationCohortId)
    {
        string owner = ownerOperationId?.Trim() ?? string.Empty;
        ReservedItemConsumption[] normalized = (quantities
                ?? Enumerable.Empty<ReservedItemConsumption>())
            .Where(value => value.IsValid)
            .GroupBy(value => value.StackId, StringComparer.Ordinal)
            .Select(group => new ReservedItemConsumption(
                group.Key,
                group.Sum(value => value.Quantity)))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (owner.Length == 0 || normalized.Length == 0)
            return false;
        if (normalized.All(value => leaseByOwnerAndStack.ContainsKey(
                LegacyKey(owner, value.StackId))))
        {
            if (quantityReservations.TryGetLeasesByOwner(owner, out _))
                return true;
            for (int index = 0; index < normalized.Length; index++)
            {
                leaseByOwnerAndStack.Remove(
                    LegacyKey(owner, normalized[index].StackId));
            }
        }

        List<WorldItemStackRecord> selected = new(normalized.Length);
        for (int index = 0; index < normalized.Length; index++)
        {
            ReservedItemConsumption requested = normalized[index];
            if (!repository.RecordsById.TryGetValue(
                    requested.StackId,
                    out WorldItemStackRecord record)
                || record == null
                || quantityReservations.GetAvailableQuantity(
                    new ItemStackId(record.stackId)) < requested.Quantity)
            {
                return false;
            }
            selected.Add(record);
        }

        ItemQuantityReservationRequest[] requests = normalized
            .Select((requested, index) => new ItemQuantityReservationRequest(
                new ItemStackId(requested.StackId),
                requested.Quantity,
                ItemReservationSignature.Create(
                    selected[index].itemId,
                    selected[index].components)))
            .ToArray();
        if (!quantityReservations.TryReserveBatch(
                owner,
                owner,
                purpose,
                aggregationCohortId,
                requests,
                out IReadOnlyList<ItemQuantityLease> leases,
                out _))
        {
            return false;
        }
        foreach (ItemQuantityLease lease in leases)
        {
            foreach (ItemLeaseSlice slice in lease.slices)
                leaseByOwnerAndStack[LegacyKey(owner, slice.stackId)] = lease.leaseId;
        }
        return true;
    }

    public void Release(string stackId, string persistentId)
    {
        if (!TryGetRecord(stackId, out WorldItemStackRecord record))
        {
            return;
        }

        string owner = persistentId?.Trim() ?? string.Empty;
        string key = LegacyKey(owner, record.stackId);
        if (!leaseByOwnerAndStack.Remove(key, out string leaseId))
        {
            return;
        }
        quantityReservations.Release(leaseId, ItemReservationReleaseReason.Cancelled);
        ClearReservation(record);
    }

    public bool TryClear(string stackId)
    {
        if (!TryGetRecord(stackId, out WorldItemStackRecord record))
        {
            return false;
        }

        string suffix = "\n" + record.stackId;
        string[] keys = leaseByOwnerAndStack.Keys
            .Where(key => key.EndsWith(suffix, StringComparison.Ordinal))
            .ToArray();
        foreach (string key in keys)
        {
            if (leaseByOwnerAndStack.Remove(key, out string leaseId))
                quantityReservations.Release(
                    leaseId,
                    ItemReservationReleaseReason.Cancelled);
        }
        ClearReservation(record);
        return true;
    }

    public bool SetForbidden(string stackId, bool forbidden)
    {
        if (!TryGetRecord(stackId, out WorldItemStackRecord record))
        {
            return false;
        }

        record.forbidden = forbidden;
        repository.MarkChanged();
        markerPresenter.RefreshAt(record.position);
        return true;
    }

    public bool PrioritizeHaul(string stackId)
    {
        if (!TryGetRecord(stackId, out WorldItemStackRecord record))
        {
            return false;
        }

        record.forbidden = false;
        TryClear(record.stackId);
        repository.PrioritizedHaulStackIds.Add(record.stackId);
        repository.MarkChanged();
        markerPresenter.RefreshAt(record.position);
        return true;
    }

    private bool TryGetRecord(
        string stackId,
        out WorldItemStackRecord record)
    {
        record = null;
        return !string.IsNullOrWhiteSpace(stackId)
            && repository.RecordsById.TryGetValue(stackId, out record)
            && record != null;
    }

    private void ClearReservation(WorldItemStackRecord record)
    {
        RestoreDirectPickupStack(record);
        repository.MarkChanged();
        markerPresenter.RefreshAt(record.position);
    }

    private static string LegacyKey(string owner, string stackId) =>
        (owner?.Trim() ?? string.Empty) + "\n" + (stackId?.Trim() ?? string.Empty);

    private static void RestoreDirectPickupStack(WorldItemStackRecord record)
    {
        if (record == null
            || string.IsNullOrWhiteSpace(record.destinationId)
            || !record.destinationId.StartsWith(
                WorldItemStackRuntime.CombatLoadoutDestinationPrefix,
                StringComparison.Ordinal))
        {
            return;
        }

        record.destinationId = record.sourceStorageDestinationId ?? string.Empty;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
    }
}
