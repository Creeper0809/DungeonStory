using System;
using System.Collections.Generic;
using UnityEngine;

public interface IItemReservationService
{
    bool TryReserve(IEnumerable<string> stackIds, string persistentId);
    void Release(string stackId, string persistentId);
    bool TryClear(string stackId);
    bool SetForbidden(string stackId, bool forbidden);
    bool PrioritizeHaul(string stackId);
}

public sealed class ItemReservationService : IItemReservationService
{
    private readonly WorldItemRepository repository;
    private readonly IItemMarkerPresenter markerPresenter;

    public ItemReservationService(
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.markerPresenter = markerPresenter
            ?? throw new ArgumentNullException(nameof(markerPresenter));
    }

    public bool TryReserve(IEnumerable<string> stackIds, string persistentId)
    {
        if (stackIds == null || string.IsNullOrWhiteSpace(persistentId))
        {
            return false;
        }

        List<WorldItemStackRecord> selected = new List<WorldItemStackRecord>();
        HashSet<Vector2Int> positions = new HashSet<Vector2Int>();
        foreach (string stackId in stackIds)
        {
            if (string.IsNullOrWhiteSpace(stackId)
                || !repository.RecordsById.TryGetValue(
                    stackId,
                    out WorldItemStackRecord record)
                || record == null
                || (!string.IsNullOrWhiteSpace(record.reservedByPersistentId)
                    && !string.Equals(
                        record.reservedByPersistentId,
                        persistentId,
                        StringComparison.Ordinal)))
            {
                return false;
            }

            selected.Add(record);
            positions.Add(record.position);
        }

        if (selected.Count == 0)
        {
            return false;
        }

        foreach (WorldItemStackRecord record in selected)
        {
            record.reservedByPersistentId = persistentId;
        }

        repository.MarkChanged();
        foreach (Vector2Int position in positions)
        {
            markerPresenter.RefreshAt(position);
        }

        return true;
    }

    public void Release(string stackId, string persistentId)
    {
        if (!TryGetRecord(stackId, out WorldItemStackRecord record))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(persistentId)
            && !string.Equals(
                record.reservedByPersistentId,
                persistentId,
                StringComparison.Ordinal))
        {
            return;
        }

        ClearReservation(record);
    }

    public bool TryClear(string stackId)
    {
        if (!TryGetRecord(stackId, out WorldItemStackRecord record))
        {
            return false;
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
        record.reservedByPersistentId = string.Empty;
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
        record.reservedByPersistentId = string.Empty;
        RestoreDirectPickupStack(record);
        repository.MarkChanged();
        markerPresenter.RefreshAt(record.position);
    }

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
