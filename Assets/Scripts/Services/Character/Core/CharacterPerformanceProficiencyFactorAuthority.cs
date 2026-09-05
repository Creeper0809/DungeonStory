using System;

public static class CharacterPerformanceProficiencyFactorAuthority
{
    public const string Schema =
        "character-performance-proficiency-factor@1";

    public static float Resolve(
        CharacterPerformanceResultChannel channel,
        CharacterProficiencyEffectSnapshot effects)
    {
        float value = channel switch
        {
            CharacterPerformanceResultChannel.AccidentRisk =>
                effects.AccidentMultiplier,
            CharacterPerformanceResultChannel.Quality
                or CharacterPerformanceResultChannel.Yield
                or CharacterPerformanceResultChannel.SuccessChance =>
                    Math.Max(0f, effects.QualityScore / 58f),
            _ => effects.WorkSpeedMultiplier
        };
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            throw new InvalidOperationException(
                "Character proficiency factor is not finite and nonnegative.");
        }
        return value;
    }
}
