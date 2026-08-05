using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CharacterCombatCommandLifecyclePolicy
{
    public static IReadOnlyList<CharacterCombatCommand> CreateActiveCommandView(
        IEnumerable<CharacterCombatCommand> commands)
    {
        return (commands ?? Array.Empty<CharacterCombatCommand>())
            .Where(command => command != null
                && command.state is not CharacterCombatCommandState.Completed
                and not CharacterCombatCommandState.Cancelled)
            .Select(command => command.Clone())
            .ToArray();
    }

    public static IReadOnlyList<CharacterCombatCommand> FindCompletedRescues(
        IEnumerable<CharacterCombatCommand> commands,
        string recoveredCharacterId)
    {
        string targetId = recoveredCharacterId?.Trim() ?? string.Empty;
        if (targetId.Length == 0)
        {
            return Array.Empty<CharacterCombatCommand>();
        }

        return (commands ?? Array.Empty<CharacterCombatCommand>())
            .Where(command => command != null
                && command.type == CombatCommandType.Rescue
                && string.Equals(
                    command.targetId,
                    targetId,
                    StringComparison.Ordinal))
            .ToArray();
    }

    public static CombatFireMode ResolveSupportedFireMode(
        CombatWeaponSnapshot weapon,
        CombatFireMode requested) =>
        weapon == null || !weapon.IsRanged
            ? CombatFireMode.Aimed
            : requested switch
            {
                CombatFireMode.Rapid when weapon.SupportsRapid => CombatFireMode.Rapid,
                CombatFireMode.Suppressive when weapon.SupportsSuppressive =>
                    CombatFireMode.Suppressive,
                _ => CombatFireMode.Aimed
            };

    public static int Manhattan(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
}
