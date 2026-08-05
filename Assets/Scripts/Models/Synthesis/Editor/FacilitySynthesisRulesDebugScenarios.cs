using System;

public static class FacilitySynthesisRulesDebugScenarios
{
    public static void Validate()
    {
        if (!FacilitySynthesisRules.MatchesMaterialIds(
                new[] { 3, 1, 2 },
                new[] { 2, 3, 1 })
            || FacilitySynthesisRules.CalculateInheritedLevel(
                new[] { 2, 4 },
                0.75f) != 2
            || !FacilitySynthesisRules.IsRecipeVisible(
                true, false, "recipe", "research", false, false,
                new[] { "research" }))
        {
            throw new InvalidOperationException(
                "Facility synthesis pure rules regression.");
        }
    }
}
