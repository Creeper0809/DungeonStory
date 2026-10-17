#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

internal static class CharacterConsumptionFactFixture
{
    internal static DomainFactDispatcher Create(
        MigratedProducerOutcomeRuntime runtime,
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        var dispatcher = new DomainFactDispatcher();
        try
        {
            Attach(dispatcher, runtime, recorder, diagnostics);
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    internal static void Attach(
        DomainFactDispatcher dispatcher,
        MigratedProducerOutcomeRuntime runtime,
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
        if (runtime == null) throw new ArgumentNullException(nameof(runtime));
        var commits = new GameplayDomainFactCommitGroup(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
        var subscriptions = new List<IDisposable>(4);
        try
        {
            subscriptions.Add(dispatcher.Subscribe(
                "fixture:character-consumables-meal",
                new MigratedDomainFactObserver<CharacterMealConsumedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    runtime,
                    commits)));
            subscriptions.Add(dispatcher.Subscribe(
                "fixture:character-consumables-substance",
                new MigratedDomainFactObserver<CharacterSubstanceConsumedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    runtime,
                    commits)));
            subscriptions.Add(dispatcher.Subscribe(
                "fixture:character-consumables-detox",
                new MigratedDomainFactObserver<CharacterDetoxAppliedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    runtime,
                    commits)));
            subscriptions.Add(dispatcher.Subscribe(
                "fixture:character-consumables-primitive-meal",
                new MigratedDomainFactObserver<CharacterPrimitiveMealCompletedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    runtime,
                    commits)));
        }
        catch
        {
            for (int index = subscriptions.Count - 1; index >= 0; index--)
                subscriptions[index].Dispose();
            throw;
        }
    }
}
#endif
