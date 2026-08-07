using System;
using System.Collections.Generic;
using System.Linq;

public enum WeatherFrontKind
{
    Clear = 0,
    Rain = 1,
    Fog = 2,
    Heatwave = 3,
    ColdSnap = 4,
    Storm = 5
}

public readonly struct ClimateZoneDefinition
{
    public ClimateZoneDefinition(
        string id,
        float meanTemperatureC,
        float annualAmplitudeC,
        int localHourOffset)
    {
        Id = id?.Trim() ?? string.Empty;
        MeanTemperatureC = meanTemperatureC;
        AnnualAmplitudeC = Math.Max(0f, annualAmplitudeC);
        LocalHourOffset = Math.Clamp(localHourOffset, -6, 6);
    }

    public string Id { get; }
    public float MeanTemperatureC { get; }
    public float AnnualAmplitudeC { get; }
    public int LocalHourOffset { get; }
    public bool IsValid => Id.Length > 0 && AnnualAmplitudeC >= 0f;
}

public readonly struct WeatherFrontDefinition
{
    private readonly float[] seasonalWeights;

    public WeatherFrontDefinition(
        string id,
        WeatherFrontKind kind,
        int minimumDurationDays,
        int maximumDurationDays,
        float temperatureModifierC,
        IReadOnlyList<float> seasonalWeights)
    {
        Id = id?.Trim() ?? string.Empty;
        Kind = kind;
        MinimumDurationDays = Math.Max(1, minimumDurationDays);
        MaximumDurationDays = Math.Max(MinimumDurationDays, maximumDurationDays);
        TemperatureModifierC = temperatureModifierC;
        this.seasonalWeights = Enumerable.Range(0, 4)
            .Select(index => Math.Max(
                0f,
                seasonalWeights != null && index < seasonalWeights.Count
                    ? seasonalWeights[index]
                    : 0f))
            .ToArray();
    }

    public string Id { get; }
    public WeatherFrontKind Kind { get; }
    public int MinimumDurationDays { get; }
    public int MaximumDurationDays { get; }
    public float TemperatureModifierC { get; }
    public bool IsValid => Id.Length > 0
        && seasonalWeights != null
        && seasonalWeights.Length == 4
        && seasonalWeights.Any(value => value > 0f);
    public float GetWeight(Season season) => seasonalWeights[(int)season];
}

public interface IClimateDefinitionCatalog
{
    ClimateZoneDefinition RequireZone(string id);
    WeatherFrontDefinition RequireFront(string id);
    IReadOnlyList<WeatherFrontDefinition> Fronts { get; }
}

[Serializable]
public sealed class ClimateWorldSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public int absoluteDay = 1;
    public string climateZoneId = string.Empty;
    public string weatherFrontId = string.Empty;
    public int frontRemainingDays;
    public float dailyNoiseC;
}

public sealed class ClimateAggregateState
{
    private ClimateAggregateState()
    {
    }

    public int AbsoluteDay { get; private set; }
    public string ClimateZoneId { get; private set; }
    public string WeatherFrontId { get; private set; }
    public int FrontRemainingDays { get; private set; }
    public float DailyNoiseC { get; private set; }

    public static ClimateAggregateState Create(
        int absoluteDay,
        string climateZoneId,
        IClimateDefinitionCatalog definitions,
        Func<double> nextUnitRandom)
    {
        ClimateZoneDefinition zone = definitions.RequireZone(climateZoneId);
        ClimateAggregateState state = new()
        {
            AbsoluteDay = Math.Max(1, absoluteDay),
            ClimateZoneId = zone.Id
        };
        state.SelectNextFront(definitions, nextUnitRandom);
        state.DailyNoiseC = NextNoise(nextUnitRandom);
        return state;
    }

    public void AdvanceToDay(
        int absoluteDay,
        IClimateDefinitionCatalog definitions,
        Func<double> nextUnitRandom)
    {
        if (absoluteDay < AbsoluteDay)
        {
            throw new InvalidOperationException(
                "Climate cannot advance backward outside staged restore.");
        }

        while (AbsoluteDay < absoluteDay)
        {
            AbsoluteDay++;
            FrontRemainingDays--;
            if (FrontRemainingDays <= 0)
            {
                SelectNextFront(definitions, nextUnitRandom);
            }
            DailyNoiseC = NextNoise(nextUnitRandom);
        }
    }

    public float GetOutdoorTemperature(IClimateDefinitionCatalog definitions)
    {
        ClimateZoneDefinition zone = definitions.RequireZone(ClimateZoneId);
        WeatherFrontDefinition front = definitions.RequireFront(WeatherFrontId);
        int dayOfYear = GameCalendarRules.Project(AbsoluteDay, 0).DayOfYear;
        double seasonal = zone.AnnualAmplitudeC * Math.Sin(
            2d * Math.PI * (dayOfYear - 30d) / GameCalendarRules.DaysPerYear);
        return zone.MeanTemperatureC
            + (float)seasonal
            + front.TemperatureModifierC
            + DailyNoiseC;
    }

    public ClimateWorldSaveData Capture() => new()
    {
        absoluteDay = AbsoluteDay,
        climateZoneId = ClimateZoneId,
        weatherFrontId = WeatherFrontId,
        frontRemainingDays = FrontRemainingDays,
        dailyNoiseC = DailyNoiseC
    };

    public static ClimateAggregateState Restore(
        ClimateWorldSaveData data,
        IClimateDefinitionCatalog definitions)
    {
        if (data == null
            || data.version != ClimateWorldSaveData.CurrentVersion
            || data.absoluteDay < 1
            || data.frontRemainingDays < 1
            || data.dailyNoiseC < -2.0001f
            || data.dailyNoiseC > 2.0001f)
        {
            throw new InvalidOperationException(
                "Climate payload is missing or invalid.");
        }
        ClimateZoneDefinition zone = definitions.RequireZone(data.climateZoneId);
        WeatherFrontDefinition front = definitions.RequireFront(data.weatherFrontId);
        return new ClimateAggregateState
        {
            AbsoluteDay = data.absoluteDay,
            ClimateZoneId = zone.Id,
            WeatherFrontId = front.Id,
            FrontRemainingDays = data.frontRemainingDays,
            DailyNoiseC = data.dailyNoiseC
        };
    }

    private void SelectNextFront(
        IClimateDefinitionCatalog definitions,
        Func<double> nextUnitRandom)
    {
        if (nextUnitRandom == null)
            throw new ArgumentNullException(nameof(nextUnitRandom));
        Season season = GameCalendarRules.Project(AbsoluteDay, 0).Season;
        WeatherFrontDefinition[] available = definitions.Fronts
            .Where(value => value.IsValid && value.GetWeight(season) > 0f)
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();
        float total = available.Sum(value => value.GetWeight(season));
        if (available.Length == 0 || total <= 0f)
            throw new InvalidOperationException($"No weather front is authored for {season}.");
        double cursor = ClampUnit(nextUnitRandom()) * total;
        WeatherFrontDefinition selected = available[^1];
        foreach (WeatherFrontDefinition candidate in available)
        {
            cursor -= candidate.GetWeight(season);
            if (cursor < 0d)
            {
                selected = candidate;
                break;
            }
        }
        int span = selected.MaximumDurationDays - selected.MinimumDurationDays + 1;
        WeatherFrontId = selected.Id;
        FrontRemainingDays = selected.MinimumDurationDays
            + Math.Min(span - 1, (int)Math.Floor(ClampUnit(nextUnitRandom()) * span));
    }

    private static float NextNoise(Func<double> nextUnitRandom) =>
        -2f + 4f * (float)ClampUnit((nextUnitRandom
            ?? throw new ArgumentNullException(nameof(nextUnitRandom)))());

    private static double ClampUnit(double value) =>
        Math.Max(0d, Math.Min(0.999999999999d, value));
}

public interface IClimateQuery
{
    int Version { get; }
    int AbsoluteDay { get; }
    string ClimateZoneId { get; }
    string WeatherFrontId { get; }
    int FrontRemainingDays { get; }
    float OutdoorTemperatureC { get; }
}

public interface IClimatePersistence
{
    ClimateWorldSaveData Capture();
    ClimateAggregateState PrepareRestore(ClimateWorldSaveData data);
    void PublishRestore(ClimateAggregateState candidate);
}
