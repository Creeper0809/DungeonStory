using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Defense/Effects/Burn", order = 2)]
public sealed class DefenseBurnEffectSO : DefenseEffectSO
{
    public override string EffectId => DefenseEffectIds.Burn;
    public override string DisplayName => "지속 피해";

    public override void Apply(DefenseEffectContext context)
    {
        context.ApplyStatus(DefenseStatusKind.Burn, Amount, Duration, Stacks);
        context.AddEffectTag(EffectiveLogTag);
    }
}
