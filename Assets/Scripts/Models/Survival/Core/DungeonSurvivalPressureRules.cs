using System;

public static class DungeonSurvivalPressureRules
{
    public static DungeonSurvivalPressure Normalize(int value)
    {
        return Enum.IsDefined(typeof(DungeonSurvivalPressure), value)
            ? (DungeonSurvivalPressure)value
            : DungeonSurvivalPressure.Standard;
    }

    public static string GetDisplayName(DungeonSurvivalPressure pressure)
    {
        return Normalize((int)pressure) switch
        {
            DungeonSurvivalPressure.Relaxed => "느긋함",
            DungeonSurvivalPressure.Harsh => "가혹함",
            _ => "표준"
        };
    }
}
