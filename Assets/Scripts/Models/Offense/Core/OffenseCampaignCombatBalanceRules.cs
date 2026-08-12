using UnityEngine;

public static class OffenseCampaignCombatBalanceRules
{
    public const float BaselineReferencePower = 10f;

    public static float GetCampaignReferencePower(int campaignOrder) =>
        Mathf.Clamp(campaignOrder, 1, 6) switch
        {
            1 => 10f,
            2 => 16f,
            3 => 32f,
            4 => 42f,
            5 => 60f,
            _ => 85f
        };

    public static float CalculateStatScale(int campaignOrder)
    {
        float normalized = GetCampaignReferencePower(campaignOrder)
            / BaselineReferencePower;
        return Mathf.Clamp(Mathf.Sqrt(normalized), 0.75f, 3f);
    }

    public static float CalculateInitiativeScale(int campaignOrder)
    {
        return Mathf.Clamp(
            Mathf.Sqrt(CalculateStatScale(campaignOrder)),
            0.90f,
            1.55f);
    }

    public static float CalculateThreatScale(int campaignOrder)
    {
        float statScale = CalculateStatScale(campaignOrder);
        return statScale * statScale;
    }
}
