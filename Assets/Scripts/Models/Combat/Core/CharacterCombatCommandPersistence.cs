using System;
using System.Collections.Generic;
using System.Linq;

public static class CharacterCombatCommandPersistence
{
    public static CharacterCombatCommandSaveData Capture(
        CharacterCombatCommandAggregateState state)
    {
        return new CharacterCombatCommandSaveData
        {
            commandSequence = state.CommandSequence,
            stanceCharacterIds = state.CombatStance
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            commands = state.Commands.Values
                .Where(command => command != null
                    && command.state is not CharacterCombatCommandState.Completed
                    and not CharacterCombatCommandState.Cancelled)
                .Select(command => command.Clone())
                .ToList(),
            revisions = state.CommandRevisions
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new CharacterCombatCommandRevisionSaveData
                {
                    actorId = pair.Key,
                    revision = pair.Value
                })
                .ToList()
        };
    }
}
