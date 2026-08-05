using System;
using System.Collections.Generic;

internal sealed class DefenseTacticalAggregateState
{
    internal Dictionary<string, CombatPositionReservation> ByActor { get; } =
        new(StringComparer.Ordinal);
    internal int Sequence { get; set; }

    internal DefenseTacticalAggregateState Clone()
    {
        DefenseTacticalAggregateState clone = new() { Sequence = Sequence };
        foreach (KeyValuePair<string, CombatPositionReservation> pair in ByActor)
        {
            clone.ByActor.Add(pair.Key, pair.Value.Clone());
        }

        return clone;
    }
}

public sealed class DefenseTacticalRestoreCandidate
{
    internal DefenseTacticalRestoreCandidate(DefenseTacticalAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal DefenseTacticalAggregateState State { get; }
}
