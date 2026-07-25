using UnityEngine;

public static class CombatRuntimeStatFactory
{
    public static CombatStatSnapshot Create(
        CharacterActor actor,
        CharacterBodyHealthSnapshot body)
    {
        if (actor == null)
        {
            return default;
        }

        float health = Mathf.Clamp01(actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
        float bodyEfficiency = Mathf.Min(
            body.Consciousness,
            Mathf.Lerp(0.5f, 1f, body.Manipulation));
        return new CombatStatSnapshot(
            actor.GetCharacterStat(CharacterStatType.Attack),
            actor.GetCharacterStat(CharacterStatType.Shooting),
            actor.GetCharacterStat(CharacterStatType.Evasion),
            actor.GetCharacterStat(CharacterStatType.MoveSpeed) * body.Mobility,
            actor.GetCharacterStat(CharacterStatType.Strength),
            actor.GetCharacterStat(CharacterStatType.Toughness),
            actor.GetCharacterStat(CharacterStatType.Dexterity) * body.Manipulation,
            health * bodyEfficiency);
    }

    public static CombatStatSnapshot Create(WildlifeActor actor)
    {
        if (actor == null)
        {
            return default;
        }

        float health = Mathf.Clamp01(actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
        float danger = actor.IsDangerous ? 1f : 0.65f;
        float aggression = Mathf.Clamp01(actor.Aggression);
        return new CombatStatSnapshot(
            3f + actor.RetaliationDamage * 0.65f,
            0f,
            3f + actor.FearSensitivity * 2f,
            5f * actor.CombatMobility,
            3f + actor.RetaliationDamage * 0.4f,
            4f + actor.MaxHealth * 0.08f,
            4f + aggression * 5f,
            health * danger);
    }
}
