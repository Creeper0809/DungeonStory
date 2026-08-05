using System;
using System.Collections.Generic;
using System.Linq;
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
        out string failureReason)
    {
        RequireDependencies(debugRules, repository, markerPresenter);
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

        Dictionary<StockCategory, int> available = new();
        foreach (WorldItemStackRecord stack in repository.Records)
        {
            if (!MatchesCategoryBuffer(
                    stack,
                    destination,
                    catalogProvider,
                    out StockCategory category))
            {
                continue;
            }

            available.TryGetValue(category, out int current);
            available[category] = current + stack.quantity;
        }

        foreach (KeyValuePair<StockCategory, int> pair in required)
        {
            if (!available.TryGetValue(pair.Key, out int quantity)
                || quantity < pair.Value)
            {
                failureReason = "facility materials missing";
                return false;
            }
        }

        foreach (KeyValuePair<StockCategory, int> pair in required)
        {
            ConsumeMatching(
                repository,
                markerPresenter,
                pair.Value,
                stack => MatchesCategoryBuffer(
                        stack,
                        destination,
                        catalogProvider,
                        out StockCategory category)
                    && category == pair.Key);
        }

        return true;
    }

    internal static bool TryConsumeByItem(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        IDungeonDebugRuleQuery debugRules,
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter,
        out string failureReason)
    {
        RequireDependencies(debugRules, repository, markerPresenter);
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

        Dictionary<string, int> available =
            new(StringComparer.Ordinal);
        foreach (WorldItemStackRecord stack in repository.Records)
        {
            if (!MatchesItemBuffer(stack, destination))
            {
                continue;
            }

            available.TryGetValue(stack.itemId, out int current);
            available[stack.itemId] = current + stack.quantity;
        }

        foreach (KeyValuePair<string, int> pair in required)
        {
            if (!available.TryGetValue(pair.Key, out int quantity)
                || quantity < pair.Value)
            {
                failureReason = $"facility item missing: {pair.Key}";
                return false;
            }
        }

        foreach (KeyValuePair<string, int> pair in required)
        {
            ConsumeMatching(
                repository,
                markerPresenter,
                pair.Value,
                stack => MatchesItemBuffer(stack, destination)
                    && string.Equals(
                        stack.itemId,
                        pair.Key,
                        StringComparison.Ordinal));
        }

        return true;
    }

    private static void ConsumeMatching(
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter,
        int requested,
        Func<WorldItemStackRecord, bool> predicate)
    {
        int remaining = requested;
        foreach (WorldItemStackRecord stack in repository.Records.ToArray())
        {
            if (remaining <= 0)
            {
                break;
            }
            if (!predicate(stack))
            {
                continue;
            }

            int consumed = Mathf.Min(remaining, stack.quantity);
            Vector2Int position = stack.position;
            stack.quantity -= consumed;
            remaining -= consumed;
            repository.MarkChanged();
            if (stack.quantity <= 0)
            {
                repository.Remove(stack);
            }
            markerPresenter.RefreshAt(position);
        }
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
        IItemMarkerPresenter markerPresenter)
    {
        _ = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        _ = repository ?? throw new ArgumentNullException(nameof(repository));
        _ = markerPresenter ?? throw new ArgumentNullException(nameof(markerPresenter));
    }
}
