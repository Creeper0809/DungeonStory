using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Defense/Effects/Charge", order = 3)]
public sealed class DefenseChargeEffectSO : DefenseEffectSO
{
    [SerializeField, Min(1)] private int dischargeThreshold = 3;
    [SerializeField] private string dischargeReason = "축전 방전";

    public override string EffectId => DefenseEffectIds.Charge;
    public override string DisplayName => "축전";

    public int DischargeThreshold
    {
        get => dischargeThreshold;
        set => dischargeThreshold = Mathf.Max(1, value);
    }

    public string DischargeReason
    {
        get => dischargeReason;
        set => dischargeReason = value;
    }

    public override void Apply(DefenseEffectContext context)
    {
        int chargeStacks = context.ApplyStatus(DefenseStatusKind.Charge, Amount, Duration, Stacks);
        context.AddEffectTag($"{EffectiveLogTag} {chargeStacks}");
        if (chargeStacks < dischargeThreshold)
        {
            return;
        }

        string reason = string.IsNullOrWhiteSpace(dischargeReason) ? DisplayName : dischargeReason;
        context.ClearStatus(DefenseStatusKind.Charge);
        context.ApplyDamage(Amount * chargeStacks, reason);
        context.AddEffectTag(reason);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        dischargeThreshold = Mathf.Max(1, dischargeThreshold);
    }
}
