using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Defense/Effects/Burn", order = 2)]
public sealed class DefenseBurnEffectSO : DefenseEffectSO
{
    [SerializeField, Range(0f, 1f)]
    private float environmentalIgnitionIntensity;

    public override string EffectId => DefenseEffectIds.Burn;
    public override string DisplayName => "지속 피해";
    public float EnvironmentalIgnitionIntensity =>
        Mathf.Clamp01(environmentalIgnitionIntensity);

    public override void Apply(DefenseEffectContext context)
    {
        context.ApplyStatus(DefenseStatusKind.Burn, Amount, Duration, Stacks);
        if (EnvironmentalIgnitionIntensity > 0f)
        {
            context.RecordEnvironmentalIgnition(
                EnvironmentalIgnitionIntensity);
        }
        context.AddEffectTag(EffectiveLogTag);
    }

    public void ConfigureEnvironmentalIgnition(float ignitionIntensity)
    {
        environmentalIgnitionIntensity = Mathf.Clamp01(ignitionIntensity);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        environmentalIgnitionIntensity =
            Mathf.Clamp01(environmentalIgnitionIntensity);
    }
}
