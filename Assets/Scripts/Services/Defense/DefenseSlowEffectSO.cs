using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Defense/Effects/Slow", order = 4)]
public sealed class DefenseSlowEffectSO : DefenseEffectSO
{
    public override string EffectId => DefenseEffectIds.Slow;
    public override string DisplayName => "감속";

    public override void Apply(DefenseEffectContext context)
    {
        context.ApplyStatus(DefenseStatusKind.Slow, Amount, Duration, Stacks);
        context.AddMovementDelay(Amount);
        context.AddEffectTag(EffectiveLogTag);
    }
}
