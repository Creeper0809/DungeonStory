using System;
using System.Collections.Generic;

public static class CharacterCombatCommandSaveValidation
{
    public const int MaximumCommands = 256;
    public const int MaximumRevisionRecords = 1024;

    public static void Validate(
        CharacterCombatCommandSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (payload == null)
        {
            report.AddError("Combat-command payload is null.");
            return;
        }
        if (payload.commandSequence < 0
            || payload.stanceCharacterIds == null
            || payload.commands == null
            || payload.revisions == null)
        {
            report.AddError("Combat-command payload has missing collections or a negative sequence.");
            return;
        }
        if (payload.stanceCharacterIds.Count > MaximumCommands
            || payload.commands.Count > MaximumCommands
            || payload.revisions.Count > MaximumRevisionRecords)
        {
            report.AddError("Combat-command payload exceeds its bounded collection limits.");
        }

        HashSet<string> stance = new(StringComparer.Ordinal);
        foreach (string actorId in payload.stanceCharacterIds)
        {
            if (!IsCharacterId(actorId) || !stance.Add(actorId))
            {
                report.AddError($"Combat stance contains invalid or duplicate actor '{actorId}'.");
            }
        }

        Dictionary<string, int> revisions = new(StringComparer.Ordinal);
        foreach (CharacterCombatCommandRevisionSaveData revision in payload.revisions)
        {
            string actorId = revision?.actorId ?? string.Empty;
            if (revision == null
                || !IsCharacterId(actorId)
                || revision.revision < 0
                || !revisions.TryAdd(actorId, revision.revision))
            {
                report.AddError($"Combat-command revision record for '{actorId}' is invalid or duplicated.");
            }
        }

        HashSet<string> commandIds = new(StringComparer.Ordinal);
        HashSet<string> commandActors = new(StringComparer.Ordinal);
        int highestSequence = 0;
        foreach (CharacterCombatCommand command in payload.commands)
        {
            string commandId = command?.commandId ?? string.Empty;
            if (command == null
                || !TryParseCommandId(commandId, out int sequence)
                || !commandIds.Add(commandId)
                || !IsCharacterId(command.actorId)
                || !commandActors.Add(command.actorId)
                || !stance.Contains(command.actorId)
                || command.targetId == null
                || command.weaponInstanceId == null
                || command.status == null
                || !Enum.IsDefined(typeof(CombatCommandType), command.type)
                || command.type == CombatCommandType.None
                || !Enum.IsDefined(
                    typeof(CharacterCombatCommandState),
                    command.state)
                || command.state is CharacterCombatCommandState.Completed
                    or CharacterCombatCommandState.Cancelled
                || !Enum.IsDefined(typeof(CombatFireMode), command.fireMode)
                || !IsFiniteAtLeast(command.attackCooldownRemaining, 0f)
                || !IsFiniteAtLeast(command.reloadRemaining, 0f)
                || command.revision <= 0)
            {
                report.AddError($"Combat command '{commandId}' is structurally invalid.");
                continue;
            }

            highestSequence = Math.Max(highestSequence, sequence);
            if (!revisions.TryGetValue(command.actorId, out int revision)
                || revision < command.revision)
            {
                report.AddError($"Combat command '{commandId}' exceeds its actor revision watermark.");
            }
            if (RequiresTargetCell(command.type) && !command.hasTargetCell)
            {
                report.AddError($"Combat command '{commandId}' is missing its target cell.");
            }
            if (RequiresTargetId(command.type)
                && string.IsNullOrWhiteSpace(command.targetId))
            {
                report.AddError($"Combat command '{commandId}' is missing its target entity.");
            }
            if (command.weaponInstanceId.Length > 0
                && !((ItemInstanceId)command.weaponInstanceId).IsValid)
            {
                report.AddError($"Combat command '{commandId}' has an invalid weapon instance ID.");
            }
        }

        if (payload.commandSequence < highestSequence)
        {
            report.AddError(
                $"Combat-command sequence {payload.commandSequence} is below saved command {highestSequence}.");
        }
    }

    public static CharacterCombatCommandAggregateState CreateState(
        CharacterCombatCommandSaveData payload)
    {
        CharacterCombatCommandAggregateState state = new()
        {
            CommandSequence = payload.commandSequence
        };
        state.CombatStance.UnionWith(payload.stanceCharacterIds);
        foreach (CharacterCombatCommandRevisionSaveData revision in payload.revisions)
        {
            state.CommandRevisions.Add(revision.actorId, revision.revision);
        }
        foreach (CharacterCombatCommand source in payload.commands)
        {
            CharacterCombatCommand restored = source.Clone();
            restored.state = CharacterCombatCommandState.Queued;
            state.Commands.Add(restored.actorId, restored);
        }

        return state;
    }

    public static bool RequiresTargetCell(CombatCommandType type) =>
        type is CombatCommandType.Move
            or CombatCommandType.MoveToCover
            or CombatCommandType.ForceFire;

    public static bool RequiresTargetId(CombatCommandType type) =>
        type is CombatCommandType.Attack or CombatCommandType.Rescue;

    private static bool IsCharacterId(string value) =>
        ((CharacterId)(value ?? string.Empty)).IsValid;

    private static bool TryParseCommandId(string value, out int sequence)
    {
        const string prefix = "combat-command:";
        sequence = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(value.Substring(prefix.Length), out sequence)
            && sequence > 0;
    }

    private static bool IsFiniteAtLeast(float value, float minimum) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum;
}
