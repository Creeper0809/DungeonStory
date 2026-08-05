using System;
using System.Collections.Generic;
using System.Linq;

public static class FacilitySynthesisRules
{
    public static bool IsRecipeVisible(
        bool hasValidData,
        bool publicByDefault,
        string recipeId,
        string requiredResearchRecipeId,
        bool recipePreserved,
        bool requirementPreserved,
        IReadOnlyCollection<string> unlockedRecipeIds)
    {
        if (!hasValidData)
        {
            return false;
        }
        string requirement = requiredResearchRecipeId?.Trim() ?? string.Empty;
        if (publicByDefault && requirement.Length == 0)
        {
            return true;
        }
        return recipePreserved
            || requirementPreserved
            || requirement.Length > 0
            && (unlockedRecipeIds?.Contains(requirement) ?? false);
    }

    public static bool MatchesMaterialIds(
        IEnumerable<int> requiredIds,
        IEnumerable<int> providedIds) =>
        (requiredIds ?? Array.Empty<int>()).OrderBy(id => id)
        .SequenceEqual((providedIds ?? Array.Empty<int>()).OrderBy(id => id));

    public static int CalculateInheritedLevel(
        IEnumerable<int> materialLevels,
        float inheritanceRatio)
    {
        int[] levels = (materialLevels ?? Array.Empty<int>())
            .Select(level => Math.Max(1, level))
            .ToArray();
        if (levels.Length == 0)
        {
            return 1;
        }
        float ratio = Math.Max(0f, Math.Min(1f, inheritanceRatio));
        return Math.Max(
            1,
            (int)Math.Round(levels.Average() * ratio,
                MidpointRounding.AwayFromZero));
    }
}
