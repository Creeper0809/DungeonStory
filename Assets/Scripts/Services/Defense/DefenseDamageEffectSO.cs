using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Defense/Effects/Damage", order = 0)]
public sealed class DefenseDamageEffectSO : DefenseEffectSO
{
    public override string EffectId => DefenseEffectIds.Damage;
    public override string DisplayName => "피해";

    public override void Apply(IDefenseEffectCommands context)
    {
        context.ApplyDamage(Amount, context.CombatLogText);
        context.AddEffectTag(EffectiveLogTag);
    }
}
