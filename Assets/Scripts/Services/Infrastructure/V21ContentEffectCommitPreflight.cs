using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

public static class V21ContentEffectCommitPreflight
{
    public static bool TryPlanItemCosts(
        IEnumerable<V20ContentEffect> effects,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        out IReadOnlyList<ReservedItemConsumption> costs,
        out string missingItemId)
    {
        List<ReservedItemConsumption> planned = new();
        missingItemId = string.Empty;
        WorldItemStackSnapshot[] available = (stacks
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(value => value != null
                && value.Quantity > 0
                && !value.Forbidden
                && value.AvailableQuantity > 0)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        foreach (IGrouping<string, V20ContentEffect> group in (effects
                     ?? Array.Empty<V20ContentEffect>())
                 .Where(value => value != null
                     && value.IsValid
                     && value.kind == V20ContentEffectKind.ItemConsume)
                 .GroupBy(
                     value => value.targetId?.Trim() ?? string.Empty,
                     StringComparer.Ordinal)
                 .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            int needed = group.Sum(value =>
                Math.Max(0, Mathf.RoundToInt(value.amount)));
            foreach (WorldItemStackSnapshot stack in available.Where(value =>
                         string.Equals(
                             value.ItemId,
                             group.Key,
                             StringComparison.Ordinal)))
            {
                int quantity = Math.Min(needed, stack.Quantity);
                if (quantity <= 0)
                {
                    continue;
                }
                planned.Add(new ReservedItemConsumption(stack.StackId, quantity));
                needed -= quantity;
                if (needed == 0)
                {
                    break;
                }
            }
            if (needed != 0)
            {
                missingItemId = group.Key;
                costs = Array.Empty<ReservedItemConsumption>();
                return false;
            }
        }

        costs = planned.AsReadOnly();
        return true;
    }
}

