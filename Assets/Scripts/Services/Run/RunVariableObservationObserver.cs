using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
public sealed class RunVariableObservationObserver : MonoBehaviour
{
    [SerializeField] private bool raiseAlerts = true;

    private IGameEventBus events;
    private IDisposable startSubscription;
    private IDisposable operationSubscription;
    private IDisposable invasionSubscription;

    [Inject]
    public void Construct(IGameEventBus gameEventBus)
    {
        if (gameEventBus == null)
            throw new ArgumentNullException(nameof(gameEventBus));
        if (ReferenceEquals(events, gameEventBus))
        {
            Subscribe();
            return;
        }

        Unsubscribe();
        events = gameEventBus;
        Subscribe();
    }

    private void OnEnable() => Subscribe();

    private void OnDisable() => Unsubscribe();

    private void OnDestroy() => Unsubscribe();

    private void Subscribe()
    {
        if (!isActiveAndEnabled || events == null)
            return;

        startSubscription ??=
            events.Subscribe<RunStartVariablesSelectedEvent>(ObserveStart);
        operationSubscription ??=
            events.Subscribe<RunVariableActivatedEvent>(ObserveOperation);
        invasionSubscription ??=
            events.Subscribe<InvasionVariableSelectedEvent>(ObserveInvasion);
    }

    private void Unsubscribe()
    {
        startSubscription?.Dispose();
        startSubscription = null;
        operationSubscription?.Dispose();
        operationSubscription = null;
        invasionSubscription?.Dispose();
        invasionSubscription = null;
    }

    private void ObserveStart(RunStartVariablesSelectedEvent value)
    {
        if (!raiseAlerts || value.snapshot == null)
            return;

        events.RaiseAlert(
            "런 시작 변수",
            value.snapshot.ToSummaryText(),
            EventAlertImportance.Low,
            "런 변수");
    }

    private void ObserveOperation(RunVariableActivatedEvent value)
    {
        RunVariableDefinition definition = value.activeVariable?.Definition;
        if (!raiseAlerts || !value.Announce || definition == null)
            return;

        events.RaiseAlert(
            definition.title,
            definition.ToDetailText(),
            definition.importance,
            "운영 변수");
    }

    private void ObserveInvasion(InvasionVariableSelectedEvent value)
    {
        RunVariableDefinition definition = value.definition;
        if (!raiseAlerts || !value.Announce || definition == null)
            return;

        events.RaiseAlert(
            definition.title,
            definition.ToDetailText(),
            definition.importance,
            "침입 변수");
    }
}
