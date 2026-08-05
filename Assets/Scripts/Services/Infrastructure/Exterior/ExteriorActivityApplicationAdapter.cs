using System;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class ExteriorActivityApplicationAdapter
{
    private readonly IGameClock gameClock;
    private readonly IGameCalendar gameCalendar;
    private readonly IRandomStream incidentRandom;
    private readonly ISurvivalEnvironmentQuery survivalEnvironment;

    public ExteriorActivityApplicationAdapter(
        IGameClock gameClock,
        IGameCalendar gameCalendar,
        IRandomStream incidentRandom,
        ISurvivalEnvironmentQuery survivalEnvironment)
    {
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.gameCalendar = gameCalendar
            ?? throw new ArgumentNullException(nameof(gameCalendar));
        this.incidentRandom = incidentRandom
            ?? throw new ArgumentNullException(nameof(incidentRandom));
        this.survivalEnvironment = survivalEnvironment
            ?? throw new ArgumentNullException(nameof(survivalEnvironment));
    }

    public float Time => gameClock.Time;
    public float DeltaTime => gameClock.DeltaTime;
    public bool IsNight => gameCalendar.TimeOfDay == TimeOfDay.Night;
    public SurvivalEnvironmentSnapshot Environment =>
        survivalEnvironment.GetEnvironmentSnapshot();

    public bool Chance(float probability) =>
        incidentRandom.Chance(probability);

    public float NextFloat() => incidentRandom.NextFloat();

    public static float CalculateIncidentChance(
        SurvivalEnvironmentSnapshot environment,
        float patrolReadiness,
        bool isNight)
    {
        return DungeonStory.Exterior.ExteriorActivityRules
            .CalculateIncidentChance(
                CreateHazardSnapshot(
                    environment,
                    isNight ? 20f : 70f),
                patrolReadiness);
    }

    public static float GetIncidentSelectionWeight(
        ExteriorIncidentKind kind,
        SurvivalEnvironmentSnapshot environment,
        float patrolReadiness)
    {
        return DungeonStory.Exterior.ExteriorActivityRules
            .GetIncidentSelectionWeight(
                (DungeonStory.Exterior.ExteriorIncidentKind)kind,
                CreateHazardSnapshot(
                    environment,
                    Mathf.Lerp(
                        70f,
                        20f,
                        Mathf.Clamp01(
                            environment.ExteriorNightDanger / 100f))),
                patrolReadiness);
    }

    private static DungeonStory.Exterior.ExteriorHazardSnapshot
        CreateHazardSnapshot(
            SurvivalEnvironmentSnapshot environment,
            float exteriorLightLevel)
    {
        return new DungeonStory.Exterior.ExteriorHazardSnapshot(
            new DungeonStory.Environment.EnvironmentalCellSnapshot(
                new DungeonStory.Environment.EnvironmentalCellAddress(0, 0),
                environment.OutdoorTemperature,
                100f,
                exteriorLightLevel),
            environment.ExteriorNightDanger,
            environment.WeatherPressure01);
    }
}
