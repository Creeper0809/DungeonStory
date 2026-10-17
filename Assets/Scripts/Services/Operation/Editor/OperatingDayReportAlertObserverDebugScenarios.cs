using System;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using Object = UnityEngine.Object;

public static class OperatingDayReportAlertObserverDebugScenarios
{
    public static void RunOrThrow(OperatingDayReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (report.unpaidOperatingCost <= 0)
        {
            throw new ArgumentException(
                "The alert observer scenario requires an unpaid operating cost.",
                nameof(report));
        }

        GameEventBus events = new GameEventBus();
        int wageAlertCount = 0;
        int reportAlertCount = 0;
        EventAlertRequest wageAlert = null;
        EventAlertRequest reportAlert = null;
        using IDisposable alertSubscription =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                EventAlertRequest request = value.request;
                if (request == null)
                {
                    return;
                }

                if (string.Equals(
                    request.Title,
                    "임금 체불",
                    StringComparison.Ordinal))
                {
                    wageAlertCount++;
                    wageAlert = request;
                }

                if (string.Equals(
                    request.Title,
                    $"Day {report.day} 정산",
                    StringComparison.Ordinal))
                {
                    reportAlertCount++;
                    reportAlert = request;
                }
            });

        GameObject host = null;
        try
        {
            host = new GameObject("OperatingDayReportAlertObserver_Debug");
            OperatingDayReportAlertBridge bridge =
                host.AddComponent<OperatingDayReportAlertBridge>();
            bridge.Construct(events);
            bridge.Construct(events);

            events.Publish(new OperatingDayReportEvent(report));
            RequireCounts(
                wageAlertCount,
                reportAlertCount,
                1,
                "Repeated construction duplicated an operating-day alert.");
            Require(
                wageAlert != null
                && wageAlert.Detail ==
                    $"미지급 임금 {report.unpaidOperatingCost}이 발생했습니다. 직원 불만이 증가합니다."
                && wageAlert.Importance == EventAlertImportance.High
                && wageAlert.Category == "경제",
                "The wage-shortfall alert payload changed.");
            Require(
                reportAlert != null
                && reportAlert.Detail == report.ToDetailText()
                && reportAlert.Importance == EventAlertImportance.Medium
                && reportAlert.Category == "정산",
                "The operating-day report alert payload changed.");

            bridge.enabled = false;
            // This focused batch runs in EditMode. Non-ExecuteAlways components do
            // not receive automatic lifecycle callbacks there; exercise the actual
            // hooks explicitly without claiming a PlayMode lifecycle test.
            InvokeLifecycleHook(bridge, "OnDisable");
            InvokeLifecycleHook(bridge, "OnDisable");
            events.Publish(new OperatingDayReportEvent(report));
            RequireCounts(
                wageAlertCount,
                reportAlertCount,
                1,
                "A disabled operating-day alert bridge remained subscribed.");

            bridge.enabled = true;
            InvokeLifecycleHook(bridge, "OnEnable");
            InvokeLifecycleHook(bridge, "OnEnable");
            events.Publish(new OperatingDayReportEvent(report));
            RequireCounts(
                wageAlertCount,
                reportAlertCount,
                2,
                "A re-enabled operating-day alert bridge did not resubscribe once.");

            InvokeLifecycleHook(bridge, "OnDisable");
            Object.DestroyImmediate(host);
            host = null;
            events.Publish(new OperatingDayReportEvent(report));
            RequireCounts(
                wageAlertCount,
                reportAlertCount,
                2,
                "A disposed operating-day alert bridge remained subscribed.");
        }
        finally
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }
    }

    private static void InvokeLifecycleHook(OperatingDayReportAlertBridge bridge, string name)
    {
        MethodInfo hook = typeof(OperatingDayReportAlertBridge).GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing lifecycle hook: " + name);
        hook.Invoke(bridge, null);
    }

    private static void RequireCounts(
        int wageAlertCount,
        int reportAlertCount,
        int expected,
        string failure)
    {
        Require(
            wageAlertCount == expected && reportAlertCount == expected,
            $"{failure} wage={wageAlertCount}; report={reportAlertCount}; "
            + $"expected={expected}.");
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }
}
