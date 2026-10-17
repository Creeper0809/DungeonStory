using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCombatCommandAggregateState
{
    public Dictionary<string, CharacterCombatCommand> Commands { get; } =
        new(StringComparer.Ordinal);
    public HashSet<string> CombatStance { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, int> CommandRevisions { get; } =
        new(StringComparer.Ordinal);
    public int CommandSequence { get; set; }
    public List<CombatCellShotPendingData> PendingCellShots { get; } = new();

    public CharacterCombatCommandAggregateState Clone()
    {
        CharacterCombatCommandAggregateState clone = new()
        {
            CommandSequence = CommandSequence
        };
        foreach (KeyValuePair<string, CharacterCombatCommand> pair in Commands)
        {
            clone.Commands.Add(pair.Key, pair.Value.Clone());
        }
        clone.CombatStance.UnionWith(CombatStance);
        foreach (KeyValuePair<string, int> pair in CommandRevisions)
        {
            clone.CommandRevisions.Add(pair.Key, pair.Value);
        }
        foreach (var pending in PendingCellShots) clone.PendingCellShots.Add(pending.Clone());

        return clone;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCombatCommandRestoreCandidate
{
    public CharacterCombatCommandRestoreCandidate(
        CharacterCombatCommandAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public CharacterCombatCommandAggregateState State { get; }
}
