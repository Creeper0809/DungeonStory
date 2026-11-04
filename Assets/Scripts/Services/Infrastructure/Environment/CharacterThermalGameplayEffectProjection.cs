using UnityEngine;

/// <summary>
/// Bridges canonical character detailed stats into the environment domain's
/// thermal-protection input. This is intentionally the only environment-side
/// trait/equipment effect adapter; environment simulation never inspects traits.
/// </summary>
public static class CharacterThermalGameplayEffectProjection
{
    private static readonly string[] ExposureFormulas =
        { "performance:survival:cold-exposure", "performance:survival:heat-exposure" };
    public const string ComfortMinimumOffsetTarget =
        "environment:comfort-minimum-offset";
    public const string SafeMinimumOffsetTarget =
        "environment:safe-minimum-offset";

    public static void Apply(
        CharacterActor actor,
        ThermalProtectionProfile profile,
        ICharacterPerformanceQuery performance)
    {
        if (actor == null || profile == null)
            return;
        if (performance == null)
            throw new System.ArgumentNullException(nameof(performance));

        profile.comfortMinimumOffset += actor.ProjectDetailedStat(
            ComfortMinimumOffsetTarget,
            0f).Value;
        profile.safeMinimumOffset += actor.ProjectDetailedStat(
            SafeMinimumOffsetTarget,
            0f).Value;
        var exposure = performance.EvaluateMany(actor, ExposureFormulas);
        profile.coldExposureMultiplier *= Mathf.Max(0.05f, exposure[0].Value);
        profile.heatExposureMultiplier *= Mathf.Max(0.05f, exposure[1].Value);
    }
}
