using System;
using C = GameplayExperienceCategory;

internal static class WorldExperiencePolicyFacts
{
    internal static double Nonnegative(double value)
    {
        if (value < 0) GameplayExperienceFacts.Fail("negative quantity");
        return value;
    }
    internal static GameplayExperienceJudgment Mood(GameplayExperienceFacts f, double delta) => f.Judge(
        delta == 0 ? C.Routine : Math.Abs(delta) >= 10 ? C.Meaningful : C.Mild,
        "applied-mood-change", "실제 기분 변화량으로 평가. 영구적 트라우마나 관계 단절을 추정하지 않음.");
}

