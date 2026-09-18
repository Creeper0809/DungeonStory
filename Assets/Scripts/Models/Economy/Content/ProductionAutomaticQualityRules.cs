using System;
using System.Collections.Generic;

/// <summary>Derives automatic participation from existing durable work contributions.</summary>
public static class ProductionAutomaticQualityRules
{
    public static float ResolveScoreCeiling(
        float authoredScoreCeiling,
        float completedCycleWork,
        IReadOnlyList<CraftContributionSaveData> manualContributions)
    {
        if (!Finite(completedCycleWork) || completedCycleWork < 0f
            || !Finite(authoredScoreCeiling) || authoredScoreCeiling < 50f || authoredScoreCeiling > 100f)
            throw new InvalidOperationException("Automatic quality has invalid work or authored score ceiling.");
        double manualWork = 0d;
        if (manualContributions != null)
        {
            for (int i = 0; i < manualContributions.Count; i++)
            {
                CraftContributionSaveData contribution = manualContributions[i];
                if (contribution == null || string.IsNullOrWhiteSpace(contribution.characterId)
                    || !Finite(contribution.contributedWork) || contribution.contributedWork < 0f)
                    throw new InvalidOperationException("Automatic quality has invalid manual contribution.");
                manualWork += contribution.contributedWork;
            }
        }
        // Match the production work completion tolerance; do not classify float summation dust as automation.
        double tolerance = Math.Max(0.001d, completedCycleWork * 0.000001d);
        if (manualWork > completedCycleWork + tolerance)
            throw new InvalidOperationException("Manual contribution exceeds completed production work.");
        return completedCycleWork - manualWork > tolerance ? authoredScoreCeiling : 100f;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
