using System;
using UnityEngine;

// Ally projection remains a pure adapter. Enemy definitions and compositions
// are owned exclusively by the V20 SO catalogs and EnemyEncounterFactory.
public static class OffenseEncounterCatalog
{
    public static string GetEnemySummary(int campaignOrder) =>
        $"SO 전술 조우군 {Mathf.Clamp(campaignOrder, 1, 6)}";

    public static OffenseBattleCombatant CreateAlly(
        CharacterActor actor,
        string persistentId,
        OffenseFormationSlot formation = OffenseFormationSlot.Front,
        float stress = 0f)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        actor.EnsureRuntimeState();
        CharacterIdentity identity = actor.Identity;
        float stressMultiplier = Mathf.Lerp(
            1f,
            0.65f,
            Mathf.Clamp01(stress / 100f));
        float maxHealth = Mathf.Max(1f, actor.MaxHealth);
        return new OffenseBattleCombatant(
            persistentId,
            identity != null ? identity.DisplayName : actor.name,
            identity != null ? identity.SpeciesTag : string.Empty,
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(
                maxHealth,
                actor.GetCharacterStat(CharacterStatType.Attack)
                    * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Strength)
                    * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Toughness)
                    * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Dexterity)
                    * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.MoveSpeed)
                    * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Shooting)
                    * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Evasion)
                    * stressMultiplier),
            Mathf.Clamp(actor.CurrentHealth, 0f, maxHealth),
            CharacterCombatAbilityCatalog.GetAbilities(actor),
            identity?.Data != null ? identity.Data.id : -1,
            formation);
    }
}
