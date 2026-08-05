using System;

public sealed class CombatFallbackWeaponRuntimeAdapter :
    ICombatFallbackWeaponRuntimePort
{
    private readonly ICombatEquipmentRuntime equipment;

    public CombatFallbackWeaponRuntimeAdapter(
        ICombatEquipmentRuntime equipment)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
    }

    public CharacterCombatLoadoutProfile GetActiveProfileSnapshot(
        string characterId) =>
        equipment.GetActiveProfileSnapshot(characterId);

    public bool TrySetActiveWeapon(
        string characterId,
        string instanceId,
        out string failureReason) =>
        equipment.TrySetActiveWeapon(
            characterId,
            instanceId,
            out failureReason);

    public bool TryGetActiveWeapon(
        string characterId,
        out CombatWeaponSnapshot weapon) =>
        equipment.TryGetActiveWeapon(characterId, out weapon);
}
