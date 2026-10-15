#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using Newtonsoft.Json;
using UnityEngine;

public static class SurvivalStateProducerObserverDebugScenarios
{
    public static void RunForBatch()
    {
        SurvivalStateAlertObserverDebugScenarios.RunOrThrow();
        Debug.Log("B105_SURVIVAL_OBSERVER_THRESHOLDS_LIFETIME=PASS");
        SurvivalStateActivityObserverDebugScenarios.RunOrThrow();
        Debug.Log("B105_SURVIVAL_ACTIVITY_PROJECTION_LIFETIME=PASS");
        VerifyDailyWeatherProducer();
        Debug.Log("B105_DAILY_WEATHER_OWNER_FAILURE_RESTORE=PASS");
        VerifyDeprivationSamplingBoundary();
        Debug.Log("B105_DEPRIVATION_SAMPLING_BOUNDARY=PASS;full-tick-play=NOT-RUN");
        EvolutionCatalystDropObserverDebugScenarios.RunOrThrow();
        Debug.Log("B105_CATALYST_PHYSICAL_DROP_OBSERVER=PASS");
        Debug.Log("B105_SURVIVAL_CATALYST_OBSERVERS=PASS");
    }

    private static void VerifyDailyWeatherProducer()
    {
        var events = new GameEventBus();
        using var facts = new DomainFactDispatcher();
        using var runtime = SurvivalDebugScenarios.CreateMealMissedOutcomeEntryFixture(
            events, new DungeonRuntimeAggregateRootStore(), facts);
        using var observer = new SurvivalStateAlertObserver(events);
        var samples = new List<SurvivalWeatherSampledEvent>();
        int alerts = 0;
        using var capture = events.Subscribe<SurvivalWeatherSampledEvent>(samples.Add);
        using var display = events.Subscribe<EventAlertRequestedEvent>(value =>
        {
            Require(value.request.Title == "날씨가 위험해집니다", "Unexpected non-weather alert in empty world.");
            alerts++;
            throw new InvalidOperationException("qa-b105-weather-display-failed");
        });
        observer.Initialize();
        runtime.Initialize();
        runtime.DebugSetWeather(SurvivalWeatherType.ColdSnap);
        events.Publish(new OperatingDayStartedEvent(1));
        Require(runtime.Capture().lastProcessedDay == 1 && samples.Count == 1 && alerts == 1,
            "Display failure interrupted the actual daily survival owner.");
        Require(samples[0].Weather == SurvivalWeatherType.ColdSnap && samples[0].FrontId == "debug:ColdSnap",
            "Actual climate sample lost its front identity or weather.");
        events.Publish(new OperatingDayStartedEvent(1));
        Require(samples.Count == 1 && alerts == 1, "The same day ran twice.");
        events.Publish(new OperatingDayStartedEvent(2));
        Require(runtime.Capture().lastProcessedDay == 2 && samples.Count == 2 && alerts == 1,
            "Daily owner skipped a valid day or the observer repeated a weather front.");

        string saved = JsonConvert.SerializeObject(runtime.Capture());
        runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(
            JsonConvert.DeserializeObject<DungeonSurvivalSaveData>(saved)));
        events.Publish(new OperatingDayStartedEvent(2));
        Require(samples.Count == 2 && alerts == 1, "Current-save roundtrip lost the processed-day guard.");
        runtime.DebugSetWeather(SurvivalWeatherType.HeatWave);
        events.Publish(new OperatingDayStartedEvent(3));
        Require(runtime.Capture().lastProcessedDay == 3 && samples.Count == 3 && alerts == 2,
            "A changed dangerous front did not reach the observer exactly once.");
        runtime.Dispose();
        events.Publish(new OperatingDayStartedEvent(4));
        Require(runtime.Capture().lastProcessedDay == 3 && samples.Count == 3,
            "Disposed daily runtime retained its subscription.");
    }

    // Exercise the changed collection/publication boundary on a normally constructed
    // owner. Unused gameplay ports reject access. This is not a full simulated Tick.
    private static void VerifyDeprivationSamplingBoundary()
    {
        var events = new GameEventBus();
        using var facts = new DomainFactDispatcher();
        var stateStore = new CharacterDeprivationStateStore(new DungeonRuntimeAggregateRootStore());
        var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        var primitive = new CharacterPrimitiveSurvivalDependencies(
            Guard<IFieldMealConsumptionCommand>(), Guard<IItemQuantityReservationService>(),
            Guard<IReservedItemTransferService>(), new EditorFixedGameCalendar(1), facts);
        using var runtime = new CharacterDeprivationRuntime(
            new CharacterDeprivationWorldDependencies(
                Guard<IGridSystemProvider>(), Guard<IWorldItemStackRuntime>(), Guard<IWorldFilthQuery>(),
                Guard<IWorldWaterQuery>(), Guard<IRoomLayoutCache>(), Guard<ICharacterAiWorldRegistry>(),
                Guard<ICharacterLifetimeQuery>(), Guard<IFacilityCandidateCache>(), Guard<ISurvivalFoodQuery>(),
                Guard<IEnvironmentWorkPolicy>()),
            new CharacterDeprivationSystemDependencies(events, Guard<IGameClock>(), Guard<IDynamicFrameWorkBudget>(),
                new RandomStreamProvider(105), Guard<IUiClock>(), Guard<IDoorAccessQuery>(),
                DefaultCharacterNeedBalanceRuntime.Instance, DisabledDungeonDebugRuleQuery.Instance,
                Guard<IHeritableTraitEffectQuery>(), Guard<IReproductionService>()),
            new CharacterDeprivationAuthorityDependencies(new ResourceItemDefinitionCatalog(content), stateStore,
                Guard<ICharacterBodyHealthCommand>(), Guard<ICharacterPerformanceQuery>(), primitive,
                facts: facts, recovery: facts, notifications: facts));
        using var observer = new SurvivalStateAlertObserver(events);
        var snapshots = new List<DeprivationBurdenSnapshotEvent>();
        using var capture = events.Subscribe<DeprivationBurdenSnapshotEvent>(snapshots.Add);
        observer.Initialize();
        int alerts = 0;
        using var display = events.Subscribe<EventAlertRequestedEvent>(_ =>
        {
            alerts++;
            throw new InvalidOperationException("qa-b105-deprivation-display-failed");
        });
        var sampleState = new CharacterDeprivationState
        {
            characterId = "character:b105:sample",
            burdens = new List<DeprivationBurdenSaveData>
            {
                new() { kind = DeprivationKind.Hunger, burden = 39f },
                new() { kind = DeprivationKind.Thirst, burden = 70f }
            }
        };
        Invoke(runtime, "CollectBurdenSample", sampleState);
        Invoke(runtime, "PublishBurdenSnapshot");
        Require(snapshots.Count == 1 && snapshots[0].Samples.Count == 1
            && snapshots[0].Samples[0].HighestBurden == 70f
            && snapshots[0].Samples[0].CharacterId.Value == sampleState.characterId && alerts == 1,
            "Actual deprivation collection did not publish the raw highest burden.");
        Invoke(runtime, "PublishBurdenSnapshot");
        Require(snapshots.Count == 2 && snapshots[1].Samples.Count == 0 && alerts == 1,
            "A display failure retained stale producer samples.");
        Require(sampleState.burdens[0].burden == 39f && sampleState.burdens[1].burden == 70f,
            "Observation changed the domain burden state.");
    }

    private static T Guard<T>() where T : class => TabooButcheryCausalTestPorts.Guard<T>();
    private static void Invoke(object owner, string method, params object[] args) =>
        (owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing production boundary: " + method)).Invoke(owner, args);
    private static void Require(bool condition, string detail)
    { if (!condition) throw new InvalidOperationException(detail); }
}
#endif
