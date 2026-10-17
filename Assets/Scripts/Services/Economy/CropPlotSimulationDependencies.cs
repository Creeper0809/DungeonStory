using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

public sealed class CropPlotSimulationDependencies
{
    public CropPlotSimulationDependencies(
        IGameClock gameClock,
        IGameCalendar calendar,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IGameSessionStateProvider gameDataProvider,
        ISurvivalEnvironmentQuery environmentQuery,
        IEnvironmentalFieldQuery environmentalField,
        IGrandProjectBenefitQuery grandProjectBenefits,
        IGameEventBus events,
        ISeasonalEventQuery seasonalEvents,
        V20StoryContentCatalog storyCatalog,
        ExtremeTraitRuntime extremeTraits = null,
        IRunSeedProvider runSeedProvider = null)
    {
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        ProgressionRuntimes = progressionRuntimes
            ?? throw new ArgumentNullException(nameof(progressionRuntimes));
        GameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        EnvironmentQuery = environmentQuery
            ?? throw new ArgumentNullException(nameof(environmentQuery));
        EnvironmentalField = environmentalField
            ?? throw new ArgumentNullException(nameof(environmentalField));
        GrandProjectBenefits = grandProjectBenefits
            ?? throw new ArgumentNullException(nameof(grandProjectBenefits));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        SeasonalEvents = seasonalEvents
            ?? throw new ArgumentNullException(nameof(seasonalEvents));
        StoryCatalog = storyCatalog
            ?? throw new ArgumentNullException(nameof(storyCatalog));
        ExtremeTraits = extremeTraits;
        RunSeedProvider = runSeedProvider;
    }

    public IGameClock GameClock { get; }
    public IGameCalendar Calendar { get; }
    public ProgressionSceneRuntimeReferences ProgressionRuntimes { get; }
    public IGameSessionStateProvider GameDataProvider { get; }
    public ISurvivalEnvironmentQuery EnvironmentQuery { get; }
    public IEnvironmentalFieldQuery EnvironmentalField { get; }
    public IGrandProjectBenefitQuery GrandProjectBenefits { get; }
    public IGameEventBus Events { get; }
    public ISeasonalEventQuery SeasonalEvents { get; }
    public V20StoryContentCatalog StoryCatalog { get; }
    public ExtremeTraitRuntime ExtremeTraits { get; }
    public IRunSeedProvider RunSeedProvider { get; }
}
