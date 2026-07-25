using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Defense/Effects/Corrosion", order = 1)]
public sealed class DefenseCorrosionEffectSO : DefenseEffectSO
{
    public override string EffectId => DefenseEffectIds.Corrosion;
    public override string DisplayName => "방어력 감소";

    public override void Apply(DefenseEffectContext context)
    {
        context.ApplyStatus(DefenseStatusKind.Corrosion, Amount, Duration, Stacks);
        context.AddEffectTag(EffectiveLogTag);
    }
}
