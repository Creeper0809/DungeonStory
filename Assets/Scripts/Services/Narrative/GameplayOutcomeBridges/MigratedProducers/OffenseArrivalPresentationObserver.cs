using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// Presents already-applied arrival activity; it never changes arrival state,
/// containment, history, or terminal publication ownership.
/// </summary>
public sealed class OffenseArrivalPresentationObserver :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public OffenseArrivalPresentationObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize()
    {
        if (subscription != null)
            return;
        subscription = events.Subscribe<OffenseArrivalActivityFact>(Observe);
    }

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Observe(OffenseArrivalActivityFact fact)
    {
        TryNotify(() => events.RaiseAlert(
            Title(fact),
            Body(fact),
            EventAlertImportance.High,
            "오펜스"));
    }

    private static string Title(OffenseArrivalActivityFact fact) =>
        fact.Activity switch
        {
            OffenseArrivalActivity.Materialized => "원정 귀환 대상 도착",
            OffenseArrivalActivity.Escaped =>
                Subject(fact.Kind) + " 이탈",
            _ => throw new ArgumentOutOfRangeException(
                nameof(fact),
                fact.Activity,
                "Unknown offense arrival activity.")
        };

    private static string Body(OffenseArrivalActivityFact fact) =>
        fact.Activity switch
        {
            OffenseArrivalActivity.Materialized => fact.Message,
            OffenseArrivalActivity.Escaped =>
                "수용 준비가 늦어 " + Subject(fact.Kind) + " "
                + CountedSubject(fact.Kind, fact.Count)
                + " 하차장에서 달아났습니다.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(fact),
                fact.Activity,
                "Unknown offense arrival activity.")
        };

    private static string Subject(OffenseReturnArrivalKind kind) => kind switch
    {
        OffenseReturnArrivalKind.Prisoner => "귀환 포로",
        OffenseReturnArrivalKind.SpecialWildlife => "특수 동물",
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "Unknown offense arrival kind.")
    };

    private static string CountedSubject(
        OffenseReturnArrivalKind kind,
        int count) => kind switch
    {
        OffenseReturnArrivalKind.Prisoner => $"{count}명이",
        OffenseReturnArrivalKind.SpecialWildlife => $"{count}마리가",
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "Unknown offense arrival kind.")
    };

    private static void TryNotify(Action notify)
    {
        try
        {
            notify();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogException(exception);
        }
    }
}
