using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

/// <summary>Observes delivered truth facts; never completes a campaign target or changes its state.</summary>
public sealed class OffenseTruthPresentationObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;
    public OffenseTruthPresentationObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    public void Initialize() => subscription ??= events.Subscribe<OffenseTruthRevealedFact>(Observe);
    public void Dispose() { subscription?.Dispose(); subscription = null; }
    private void Observe(OffenseTruthRevealedFact fact)
    {
        var value = fact.Snapshot();
        Notify(() => events.RaiseAlert(value.truthTitle, value.truthText, EventAlertImportance.High, "원정"));
        Notify(() => events.Publish(new OffenseTruthRevealedEvent(value.targetId, value.truthTitle, value.truthText)));
    }
    private static void Notify(Action callback)
    {
        try { callback(); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        { Debug.LogException(exception); }
    }
}
