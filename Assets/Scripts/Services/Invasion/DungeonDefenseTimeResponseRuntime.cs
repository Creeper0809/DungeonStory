using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class DungeonDefenseTimeResponseRuntime :
    IInitializable,
    ITickable,
    IDisposable
{
    private readonly IGameEventBus events;
    private readonly IGameTimeScaleController timeScale;
    private readonly IDungeonUserSettingsService settings;
    private IDisposable breachedSubscription;
    private IDisposable frontCollapsedSubscription;
    private IDisposable finalDefenseSubscription;
    private IDisposable resolvedSubscription;
    private bool entered;
    private bool frontPaused;
    private bool finalPaused;
    private bool changedAutomatically;
    private bool playerOverrode;
    private float scaleBeforeDefense = 1f;
    private float automaticallyAppliedScale = 1f;

    public DungeonDefenseTimeResponseRuntime(
        IGameEventBus events,
        IGameTimeScaleController timeScale,
        IDungeonUserSettingsService settings)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.timeScale = timeScale
            ?? throw new ArgumentNullException(nameof(timeScale));
        this.settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
    }

    public void Initialize()
    {
        breachedSubscription = events.Subscribe<InvasionDungeonBreachedEvent>(
            OnDungeonBreached);
        frontCollapsedSubscription = events.Subscribe<DefenseFrontCollapsedEvent>(
            _ => PauseForCriticalEvent(ref frontPaused));
        finalDefenseSubscription = events.Subscribe<InvasionFinalCombatStartedEvent>(
            _ => PauseForCriticalEvent(ref finalPaused));
        resolvedSubscription = events.Subscribe<InvasionResolvedEvent>(
            _ => OnInvasionResolved());
    }

    public void Tick()
    {
        if (!changedAutomatically || playerOverrode)
        {
            return;
        }

        if (!Mathf.Approximately(timeScale.Scale, automaticallyAppliedScale))
        {
            playerOverrode = true;
        }
    }

    public void Dispose()
    {
        breachedSubscription?.Dispose();
        frontCollapsedSubscription?.Dispose();
        finalDefenseSubscription?.Dispose();
        resolvedSubscription?.Dispose();
    }

    private void OnDungeonBreached(InvasionDungeonBreachedEvent _)
    {
        if (entered)
        {
            return;
        }

        entered = true;
        DungeonDefenseTimeResponse response =
            settings.Current.defenseTimeResponse;
        if (response == DungeonDefenseTimeResponse.SlowToX1
            && timeScale.Scale > 1f)
        {
            ApplyAutomaticScale(1f);
        }
        else if (response == DungeonDefenseTimeResponse.PauseOnCritical)
        {
            ApplyAutomaticScale(0f);
        }
    }

    private void PauseForCriticalEvent(ref bool alreadyPaused)
    {
        if (alreadyPaused
            || settings.Current.defenseTimeResponse
                != DungeonDefenseTimeResponse.PauseOnCritical)
        {
            return;
        }

        alreadyPaused = true;
        ApplyAutomaticScale(0f);
    }

    private void ApplyAutomaticScale(float targetScale)
    {
        if (!changedAutomatically)
        {
            scaleBeforeDefense = timeScale.Scale;
            changedAutomatically = true;
        }

        automaticallyAppliedScale = Mathf.Max(0f, targetScale);
        timeScale.Scale = automaticallyAppliedScale;
        playerOverrode = false;
    }

    private void OnInvasionResolved()
    {
        if (changedAutomatically
            && !playerOverrode
            && Mathf.Approximately(
                timeScale.Scale,
                automaticallyAppliedScale))
        {
            timeScale.Scale = scaleBeforeDefense;
        }

        entered = false;
        frontPaused = false;
        finalPaused = false;
        changedAutomatically = false;
        playerOverrode = false;
        scaleBeforeDefense = 1f;
        automaticallyAppliedScale = 1f;
    }
}
