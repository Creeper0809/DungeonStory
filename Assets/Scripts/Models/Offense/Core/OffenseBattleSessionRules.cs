using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class OffenseBattleSessionRules
{
    public static CombatStatSnapshot CreateCombatStats(
        OffenseBattleCombatant combatant)
    {
        float manipulation = combatant.Manipulation * combatant.Consciousness;
        float mobility = combatant.Mobility * combatant.Consciousness;
        return new CombatStatSnapshot(
            combatant.Stats.Attack * manipulation,
            combatant.Stats.Shooting * manipulation,
            combatant.Stats.Evasion * mobility,
            combatant.Stats.MoveSpeed * mobility,
            combatant.Stats.Strength * manipulation,
            combatant.Stats.Toughness,
            combatant.Stats.Dexterity * manipulation,
            Mathf.Min(combatant.HealthRatio, combatant.Consciousness));
    }

    public static int GetFormationDistance(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        return 1 + ((int)source.Formation + (int)target.Formation) * 4;
    }

    public static string GetBodyPartName(CombatBodyPart bodyPart)
    {
        return bodyPart switch
        {
            CombatBodyPart.Head => "머리",
            CombatBodyPart.Torso => "몸통",
            CombatBodyPart.LeftArm => "왼팔",
            CombatBodyPart.RightArm => "오른팔",
            CombatBodyPart.LeftLeg => "왼다리",
            CombatBodyPart.RightLeg => "오른다리",
            _ => "몸"
        };
    }

    public static float EstimateAbilityDamageMultiplier(
        CharacterCombatAbilityDefinition ability)
    {
        float estimate = 0f;
        foreach (OffenseCombatEffectModule effect in
                 ability?.Effects ?? Array.Empty<OffenseCombatEffectModule>())
        {
            if (effect is OffenseDamageEffect)
            {
                estimate += 1.1f;
            }
            else if (effect is OffenseVulnerabilityEffect
                     || effect is OffenseDamageOverTimeEffect)
            {
                estimate += 0.3f;
            }
        }

        return estimate;
    }
}
