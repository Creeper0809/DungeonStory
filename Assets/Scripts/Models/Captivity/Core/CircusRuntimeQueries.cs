using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class CircusQueryRules
{
    internal static CircusShowOrder FindOrder(
        IReadOnlyList<CircusShowOrder> orders,
        string orderId) =>
        orders.FirstOrDefault(item => string.Equals(
            item.orderId,
            orderId?.Trim(),
            StringComparison.Ordinal));

    internal static string WildlifePassKey(string wildlifeId) =>
        $"wildlife:{wildlifeId?.Trim() ?? string.Empty}";

    internal static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);

    internal static void CancelActiveShows(
        IEnumerable<CircusShowOrder> orders,
        Func<string, string, bool> cancel,
        string reason)
    {
        foreach (CircusShowOrder order in orders
                     .Where(item => !item.IsTerminal)
                     .ToArray())
        {
            cancel(order.orderId, reason);
        }
    }
}
