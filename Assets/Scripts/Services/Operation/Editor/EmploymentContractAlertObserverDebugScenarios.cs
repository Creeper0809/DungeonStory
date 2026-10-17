using System;
using DungeonStory.Foundation;
using DungeonStory.Operation;

public static class EmploymentContractAlertObserverDebugScenarios
{
    public static void RunOrThrow()
    {
        const string displayName = "Observer Mercenary";
        MercenaryContractEndedEvent terminal =
            new MercenaryContractEndedEvent(
                "character:employment-alert-debug",
                displayName,
                12,
                "finance:employment-alert-debug");
        GameEventBus events = new GameEventBus();
        EmploymentContractAlertObserver observer =
            new EmploymentContractAlertObserver(events);
        int alertCount = 0;
        EventAlertRequest lastAlert = null;
        using IDisposable alertSubscription =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                EventAlertRequest request = value.request;
                if (request != null
                    && string.Equals(
                        request.Title,
                        "용병 계약 종료",
                        StringComparison.Ordinal))
                {
                    alertCount++;
                    lastAlert = request;
                }
            });

        try
        {
            observer.Initialize();
            observer.Initialize();
            events.Publish(terminal);
            Require(
                alertCount == 1,
                "Repeated initialization duplicated the employment alert.");
            Require(
                lastAlert != null
                && lastAlert.Detail ==
                    $"{displayName}에게 일급을 지급하지 못해 용병 계약이 종료되었습니다."
                && lastAlert.Importance == EventAlertImportance.Medium
                && lastAlert.Category == "경영",
                "The employment-contract alert payload changed.");

            observer.Dispose();
            events.Publish(terminal);
            Require(
                alertCount == 1,
                "A disposed employment alert observer remained subscribed.");

            observer.Initialize();
            events.Publish(terminal);
            Require(
                alertCount == 2,
                "A reinitialized employment alert observer did not subscribe once.");

            observer.Dispose();
            events.Publish(terminal);
            Require(
                alertCount == 2,
                "The employment alert observer remained subscribed after final disposal.");
        }
        finally
        {
            observer.Dispose();
        }
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }
}
