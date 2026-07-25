using System;
using System.Collections.Generic;
using DungeonStory.Foundation;

public static class GameEventBusAlertExtensions
{
    public static void RaiseAlert(
        this IGameEventBus gameEventBus,
        string title,
        string detail,
        EventAlertImportance importance,
        string category = "",
        IEnumerable<EventAlertChoice> choices = null)
    {
        if (gameEventBus == null)
        {
            throw new ArgumentNullException(nameof(gameEventBus));
        }

        gameEventBus.Publish(
            new EventAlertRequestedEvent(
                new EventAlertRequest(title, detail, importance, category, choices)));
    }

    public static void RaiseInvasionResult(
        this IGameEventBus gameEventBus,
        string detail,
        EventAlertImportance importance = EventAlertImportance.High)
    {
        gameEventBus.RaiseAlert("침입 결과", detail, importance, "침입");
    }

    public static void RaiseStaffComplaint(
        this IGameEventBus gameEventBus,
        string detail,
        EventAlertImportance importance = EventAlertImportance.Medium)
    {
        gameEventBus.RaiseAlert("직원 불만", detail, importance, "직원");
    }

    public static void RaiseBlueprintAcquired(
        this IGameEventBus gameEventBus,
        string detail,
        EventAlertImportance importance = EventAlertImportance.Medium)
    {
        gameEventBus.RaiseAlert("설계도 획득", detail, importance, "설계도");
    }
}
