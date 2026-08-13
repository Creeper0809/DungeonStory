using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Executes validated, all-or-nothing facility-buffer consumption against the
/// authoritative physical item repository. Availability is proven before any
/// stack is mutated, so a rejected request cannot partially consume stock.
/// </summary>
internal static class ItemFacilityBufferTransaction
{
    internal static bool TryConsumeByCategory(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs,
        IDungeonDebugRuleQuery debugRules,
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalogProvider,
        IItemMarkerPresenter markerPresenter,
        IItemQuantityReservationService reservations,
        IReservedItemTransferService reservedTransfers,
        string ownerOperationId,
        out string failureReason)
    {
        RequireDependencies(
            debugRules,
            repository,
            markerPresenter,
            reservations,
            reservedTransfers);
        if (catalogProvider == null)
        {
            throw new ArgumentNullException(nameof(catalogProvider));
        }

        failureReason = string.Empty;
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            failureReason = "destination missing";
            return false;
        }

        Dictionary<StockCategory, int> required = costs?
            .Where(pair => pair.Value > 0)
            .GroupBy(pair => pair.Key)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => Mathf.Max(0, pair.Value)))
            ?? new Dictionary<StockCategory, int>();
        if (required.Count == 0 || debugRules.ShouldSkipCosts())
        {
            return true;
        }

        List<ReservedItemConsumption> selected = new();
        foreach (KeyValuePair<StockCategory, int> pair in required
                     .OrderBy(value => value.Key))
        {
            if (!TrySelectAvailable(
                    repository,
                    reservations,
                    pair.Value,
                    stack => MatchesCategoryBuffer(
                            stack,
                            destination,
                            catalogProvider,
                            out StockCategory category)
                        && category == pair.Key,
                    selected))
            {
                failureReason = "facility materials missing";
                return false;
            }
        }
        return TryReserveAndConsume(
            destination,
            ownerOperationId,
            selected,
            repository,
            reservations,
            reservedTransfers,
            out failureReason);
    }

    internal static bool TryConsumeByItem(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        IDungeonDebugRuleQuery debugRules,
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter,
        IItemQuantityReservationService reservations,
        IReservedItemTransferService reservedTransfers,
        string ownerOperationId,
        out string failureReason)
    {
        RequireDependencies(
            debugRules,
            repository,
            markerPresenter,
            reservations,
            reservedTransfers);
        failureReason = string.Empty;
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            failureReason = "destination missing";
            return false;
        }

        Dictionary<string, int> required = costs?
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            .GroupBy(pair => pair.Key.Trim(), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => Mathf.Max(0, pair.Value)),
                StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        if (required.Count == 0 || debugRules.ShouldSkipCosts())
        {
            return true;
        }

        List<ReservedItemConsumption> selected = new();
        foreach (KeyValuePair<string, int> pair in required
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (!TrySelectAvailable(
                    repository,
                    reservations,
                    pair.Value,
                    stack => MatchesItemBuffer(stack, destination)
                        && string.Equals(
                            stack.itemId,
                            pair.Key,
                            StringComparison.Ordinal),
                    selected))
            {
                failureReason = $"facility item missing: {pair.Key}";
                return false;
            }
        }
        return TryReserveAndConsume(
            destination,
            ownerOperationId,
            selected,
            repository,
            reservations,
            reservedTransfers,
            out failureReason);
    }

    private static bool TrySelectAvailable(
        WorldItemRepository repository,
        IItemQuantityReservationService reservations,
        int requested,
        Func<WorldItemStackRecord, bool> predicate,
        ICollection<ReservedItemConsumption> selected)
    {
        int remaining = requested;
        foreach (WorldItemStackRecord stack in repository.Records
                     .Where(value => value != null)
                     .OrderBy(value => value.stackId, StringComparer.Ordinal))
        {
            if (remaining <= 0) break;
            if (!predicate(stack))
                continue;
            int available = reservations.GetAvailableQuantity(
                new ItemStackId(stack.stackId));
            int take = Mathf.Min(remaining, available);
            if (take <= 0) continue;
            selected.Add(new ReservedItemConsumption(stack.stackId, take));
            remaining -= take;
        }
        return remaining == 0;
    }

    private static bool TryReserveAndConsume(
        string destinationId,
        string ownerOperationId,
        IReadOnlyList<ReservedItemConsumption> selected,
        WorldItemRepository repository,
        IItemQuantityReservationService reservations,
        IReservedItemTransferService reservedTransfers,
        out string failureReason)
    {
        failureReason = string.Empty;
        string owner = ownerOperationId?.Trim() ?? string.Empty;
        if (owner.Length == 0)
        {
            failureReason = "facility operation missing";
            return false;
        }
        ItemQuantityReservationRequest[] requests = selected
            .Where(value => value.IsValid)
            .Select(value => new ItemQuantityReservationRequest(
                new ItemStackId(value.StackId),
                value.Quantity,
                ItemReservationSignature.Create(
                    repository.RecordsById[value.StackId].itemId,
                    repository.RecordsById[value.StackId].components)))
            .ToArray();
        if (!reservations.TryReserveBatch(
                owner,
                string.Empty,
                ItemReservationPurpose.FacilityBuffer,
                $"facility-buffer:{destinationId}",
                requests,
                out IReadOnlyList<ItemQuantityLease> leases,
                out DomainFailure reserveFailure))
        {
            failureReason = reserveFailure.ToString();
            return false;
        }
        foreach (ItemQuantityLease lease in leases)
        {
            if (reservedTransfers.TryConsumeReservedQuantity(
                    lease.leaseId,
                    lease.remainingQuantity,
                    out DomainFailure consumeFailure))
            {
                continue;
            }
            reservations.ReleaseByOwner(
                owner,
                ItemReservationReleaseReason.Cancelled);
            failureReason = consumeFailure.ToString();
            return false;
        }
        return true;
    }

    private static bool MatchesCategoryBuffer(
        WorldItemStackRecord stack,
        string destinationId,
        IDungeonItemCatalogProvider catalogProvider,
        out StockCategory category)
    {
        category = default;
        if (!MatchesItemBuffer(stack, destinationId)
            || !catalogProvider.TryGetDefinition(
                stack.itemId,
                out DungeonItemDefinition definition))
        {
            return false;
        }

        category = definition.StockCategory;
        return true;
    }

    private static bool MatchesItemBuffer(
        WorldItemStackRecord stack,
        string destinationId) =>
        stack != null
        && stack.quantity > 0
        && stack.state == WorldItemStackState.FacilityBuffer
        && string.Equals(
            stack.destinationId ?? string.Empty,
            destinationId,
            StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(stack.itemId);

    private static void RequireDependencies(
        IDungeonDebugRuleQuery debugRules,
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter,
        IItemQuantityReservationService reservations,
        IReservedItemTransferService reservedTransfers)
    {
        _ = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        _ = repository ?? throw new ArgumentNullException(nameof(repository));
        _ = markerPresenter ?? throw new ArgumentNullException(nameof(markerPresenter));
        _ = reservations ?? throw new ArgumentNullException(nameof(reservations));
        _ = reservedTransfers ?? throw new ArgumentNullException(nameof(reservedTransfers));
    }
}
