using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CircusRuntimeQueries
{
    public static CircusShowOrder FindOrder(
        IReadOnlyList<CircusShowOrder> orders,
        string orderId) => CircusQueryRules.FindOrder(orders, orderId);

    public static string WildlifePassKey(string wildlifeId)
    {
        return CircusQueryRules.WildlifePassKey(wildlifeId);
    }

    public static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return CircusQueryRules.Manhattan(left, right);
    }

    public static void CancelActiveShows(
        IEnumerable<CircusShowOrder> orders,
        Func<string, string, bool> cancel,
        string reason)
    {
        CircusQueryRules.CancelActiveShows(orders, cancel, reason);
    }

    public static void ReleasePerformers(
        CircusShowOrder order,
        Action<string> release)
    {
        if (release == null)
        {
            throw new ArgumentNullException(nameof(release));
        }
        foreach (string captiveId in order?.performerIds
                     ?? new List<string>())
        {
            release(captiveId);
        }
    }
}
