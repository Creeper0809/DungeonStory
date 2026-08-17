/// <summary>
/// Canonical V27 per-adult daily labor authority.
/// Historical values exist only to reproduce older balance evidence; current
/// gameplay projections must choose Actual or EffectiveOutput explicitly.
/// </summary>
public static class SettlementLaborAuthority
{
    public const float HistoricalV26WuPerAdultDay = 20f;
    public const float HistoricalTheoreticalCapacityWuPerAdultDay = 99f;
    public const float ActualWuPerAdultDay = 50f;
    public const float EffectiveOutputWuPerAdultDay = 45f;
    public const float EffectiveToActualRatio =
        EffectiveOutputWuPerAdultDay / ActualWuPerAdultDay;
}
