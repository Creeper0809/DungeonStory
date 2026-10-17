using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class ApparelWorkOrderAuthorityState
{
    internal List<ApparelWorkOrderSaveData> Orders { get; } = new();
    internal Dictionary<string, ApparelWorkOrderTerminalStateSaveData>
        TerminalStates { get; } = new(StringComparer.Ordinal);
    internal int Version;

    internal ApparelWorkOrderAuthorityState Clone()
    {
        ApparelWorkOrderAuthorityState clone = new()
        {
            Version = Version
        };
        clone.Orders.AddRange(Orders.Select(ApparelWorkOrderSnapshots.Clone));
        foreach (KeyValuePair<string,
                     ApparelWorkOrderTerminalStateSaveData> pair in
                 TerminalStates)
        {
            clone.TerminalStates.Add(pair.Key, pair.Value?.Clone());
        }
        return clone;
    }
}

/// <summary>One replaceable pointer for live orders, terminal rows and their revision.</summary>
internal sealed class ApparelWorkOrderAuthorityStore
{
    internal ApparelWorkOrderAuthorityState Current { get; private set; } = new();

    internal void Replace(ApparelWorkOrderAuthorityState state) =>
        Current = state ?? throw new ArgumentNullException(nameof(state));
}
