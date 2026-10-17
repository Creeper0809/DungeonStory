using System;

public static class CharacterArcaneWeaponRules
{
    public static bool IsArcane(CombatWeaponSnapshot weapon) => weapon?.ArcaneManaCost > 0f;
    public static bool IsArcane(CombatEquipmentDerivedStats equipment) => equipment.ArcaneManaCost > 0f;
    public static float GetManaCost(CombatWeaponSnapshot weapon) => weapon?.ArcaneManaCost ?? 0f;

    public static bool IsArcane(string definitionId) =>
        definitionId?.Trim() is "weapon:rune-blade"
            or "weapon:mana-lance"
            or "weapon:rune-bow";

    public static float GetManaCost(string definitionId) =>
        definitionId?.Trim() switch
        {
            "weapon:rune-blade" => 8f,
            "weapon:rune-bow" => 10f,
            "weapon:mana-lance" => 12f,
            _ => 0f
        };
}
