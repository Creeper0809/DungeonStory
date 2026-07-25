using UnityEngine;

public static class MetaProgressionCalculator
{
    public static int CalculateLegacyCurrency(RunResultSnapshot result)
    {
        if (result == null)
        {
            return 0;
        }

        float survivalReward = (result.survivalSeconds / 60f) * 8f
            + Mathf.Max(0, result.survivedOperatingDays) * 35f
            + Mathf.Max(0, result.settlementCount) * 10f;
        float defenseReward = Mathf.Max(0, result.defendedInvasionCount) * 30f;
        float threatReward = GetThreatStageScore(result.maxThreatStage) * 12f
            + Mathf.Max(0f, result.finalInvasionThreat) * 0.25f;
        float discoveryReward = Mathf.Min(60f, Mathf.Max(0, result.firstDiscoveredFacilityCount) * 4f)
            + Mathf.Min(60f, Mathf.Max(0, result.firstUnlockedRecipeCount) * 10f);
        float offenseReward = Mathf.Max(0, result.offenseSuccessCount) * 25f;
        float raw = survivalReward + defenseReward + threatReward + discoveryReward + offenseReward;
        return Mathf.Max(0, Mathf.RoundToInt(raw * Mathf.Max(0.1f, result.difficultyMultiplier)));
    }

    private static int GetThreatStageScore(InvasionThreatStage stage)
    {
        return stage switch
        {
            InvasionThreatStage.Warning => 1,
            InvasionThreatStage.Candidate => 2,
            InvasionThreatStage.Safety => 2,
            _ => 0
        };
    }
}
