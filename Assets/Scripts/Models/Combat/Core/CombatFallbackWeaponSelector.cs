using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatFallbackWeaponSelector
{
    private readonly ICombatFallbackWeaponRuntimePort equipment;

    public CombatFallbackWeaponSelector(
        ICombatFallbackWeaponRuntimePort equipment)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
    }

    public bool TrySelect(
        string actorId,
        bool preferLoadedRanged,
        out CombatWeaponSnapshot selected)
    {
        selected = null;
        CharacterCombatLoadoutProfile profile =
            equipment.GetActiveProfileSnapshot(actorId);
        if (profile == null)
        {
            return false;
        }

        string original = profile.activeWeaponInstanceId;
        List<(string id, CombatWeaponSnapshot weapon)> options = new();
        foreach (string instanceId in profile.weaponInstanceIds)
        {
            if (string.Equals(instanceId, original, StringComparison.Ordinal)
                || !equipment.TrySetActiveWeapon(actorId, instanceId, out _)
                || !equipment.TryGetActiveWeapon(
                    actorId,
                    out CombatWeaponSnapshot weapon)
                || weapon == null)
            {
                continue;
            }
            options.Add((instanceId, weapon));
        }

        (string id, CombatWeaponSnapshot weapon) choice = options
            .OrderBy(option => preferLoadedRanged
                && option.weapon.IsRanged
                && (!option.weapon.RequiresAmmo || option.weapon.LoadedAmmo > 0)
                    ? 0
                    : !option.weapon.IsRanged ? 1 : 2)
            .FirstOrDefault();
        if (choice.weapon != null
            && equipment.TrySetActiveWeapon(actorId, choice.id, out _))
        {
            selected = choice.weapon;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(original))
        {
            equipment.TrySetActiveWeapon(actorId, original, out _);
        }
        return false;
    }
}

public interface ICombatFallbackWeaponRuntimePort
{
    CharacterCombatLoadoutProfile GetActiveProfileSnapshot(string characterId);
    bool TrySetActiveWeapon(
        string characterId,
        string instanceId,
        out string failureReason);
    bool TryGetActiveWeapon(
        string characterId,
        out CombatWeaponSnapshot weapon);
}
