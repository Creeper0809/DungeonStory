/// <summary>
/// Assembly-low simulation clock authority shared by model and service layers.
/// Calendar projection may depend on this contract; production models must not
/// depend upward on the CoreSession assembly merely to convert game hours.
/// </summary>
public static class GameSimulationTimeRules
{
    public const int HoursPerDay = 24;
    public const float SecondsPerDay = 180f;
    public const float SecondsPerGameHour = SecondsPerDay / HoursPerDay;
}
