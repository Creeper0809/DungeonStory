using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Defense/Effects/Guard Attack", order = 5)]
public sealed class DefenseGuardAttackEffectSO : DefenseEffectSO
{
    [SerializeField] private string damageReason = "경비실 교전";

    public override string EffectId => DefenseEffectIds.GuardAttack;
    public override string DisplayName => "근접 교전";

    public string DamageReason
    {
        get => damageReason;
        set => damageReason = value;
    }

    public override void Apply(DefenseEffectContext context)
    {
        string reason = string.IsNullOrWhiteSpace(damageReason) ? DisplayName : damageReason;
        context.ApplyDamage(Amount, reason);
        context.AddEffectTag(EffectiveLogTag);
    }
}
