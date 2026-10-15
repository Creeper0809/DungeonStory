using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

// Presentation is an optional consumer. It never owns effect completion, a save cursor, or a gameplay command.
public sealed class SeasonalSocialPresentationObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly List<IDisposable> subscriptions = new();
    private readonly Dictionary<string, string> festivalFingerprints = new(StringComparer.Ordinal);
    private readonly HashSet<(string occurrenceId, string fingerprint)> festivalPublishing = new();
    private int lifetime;

    public SeasonalSocialPresentationObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize()
    {
        if (subscriptions.Count != 0) return;
        ++lifetime;
        try
        {
            subscriptions.Add(events.Subscribe<SeasonalWildlifeProgressedFact>(value =>
                TryNotify(() => Publish(SeasonalSocialAlertProjection.Build(value)))));
            subscriptions.Add(events.Subscribe<SeasonalWildlifeExpiredFact>(value =>
                TryNotify(() => Publish(SeasonalSocialAlertProjection.Build(value)))));
            subscriptions.Add(events.Subscribe<SeasonalCargoProgressedFact>(value =>
                TryNotify(() => Publish(SeasonalSocialAlertProjection.Build(value)))));
            subscriptions.Add(events.Subscribe<SeasonalCargoExpiredFact>(value =>
                TryNotify(() => Publish(SeasonalSocialAlertProjection.Build(value)))));
            subscriptions.Add(events.Subscribe<FestivalExecutionProgressedFact>(value =>
                TryNotify(() => ObserveFestival(value))));
            subscriptions.Add(events.Subscribe<V20ContentEffectsResolvedEvent>(value =>
                TryNotify(() =>
                {
                    if (value.TryGetResolution(out var result) && result.IsSocietyTerminal)
                        Publish(V20SocietyEventAlertProjection.BuildResolved(result));
                })));
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        ++lifetime;
        foreach (IDisposable subscription in subscriptions) subscription.Dispose();
        subscriptions.Clear();
        festivalFingerprints.Clear();
        festivalPublishing.Clear();
    }

    private void ObserveFestival(FestivalExecutionProgressedFact fact)
    {
        EventAlertRequest request = SeasonalSocialAlertProjection.Build(fact);
        string fingerprint = SeasonalSocialAlertProjection.FestivalFingerprint(fact, request);
        if (fact.Change == FestivalExecutionChange.Advanced
            && festivalFingerprints.TryGetValue(fact.OccurrenceId, out string previous)
            && string.Equals(previous, fingerprint, StringComparison.Ordinal))
            return;
        var pending = (fact.OccurrenceId, fingerprint);
        if (!festivalPublishing.Add(pending)) return;
        int observedLifetime = lifetime;
        try
        {
            Publish(request);
            if (observedLifetime == lifetime)
                festivalFingerprints[fact.OccurrenceId] = fingerprint;
        }
        finally { festivalPublishing.Remove(pending); }
    }

    private void Publish(EventAlertRequest request) =>
        events.Publish(new EventAlertRequestedEvent(request));

    private static void TryNotify(Action action)
    {
        try { action(); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        { Debug.LogError("seasonal-social-presentation-observer:" + exception.GetType().Name); }
    }
}
