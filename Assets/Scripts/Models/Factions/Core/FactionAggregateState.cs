using System;
using System.Collections.Generic;

namespace DungeonStory.Factions
{

/// <summary>
/// The single mutable aggregate owned by the faction domain. Unity adapters may
/// project it, but must not maintain a parallel faction or route collection.
/// </summary>
public sealed class FactionAggregateState
{
    public Dictionary<string, DungeonFactionState> Factions { get; } =
        new(StringComparer.Ordinal);
    public List<FactionRouteState> Routes { get; } = new();
    public int CurrentDay { get; set; } = 1;
    public int RouteSequence { get; set; }
}

public sealed class FactionRestoreCandidate
{
    public FactionRestoreCandidate(
        FactionAggregateState state,
        DungeonFactionSaveData payload = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Payload = payload;
    }

    public FactionAggregateState State { get; }
    public DungeonFactionSaveData Payload { get; }
}
}
