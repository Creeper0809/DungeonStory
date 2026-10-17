using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class OperatingDayReportAlertBridge : MonoBehaviour
{
    private IGameEventBus gameEventBus;
    private IDisposable operatingDayReportSubscription;

    [Inject]
    public void Construct(IGameEventBus gameEventBus)
    {
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvent();
    }

    public void OnTriggerEvent(OperatingDayReportEvent eventType)
    {
        OperatingDayReport report = eventType.report;
        if (report == null)
        {
            return;
        }

        if (report.unpaidOperatingCost > 0)
        {
            PublishNonFatal(
                () => gameEventBus.RaiseAlert(
                    "임금 체불",
                    $"미지급 임금 {report.unpaidOperatingCost}이 발생했습니다. 직원 불만이 증가합니다.",
                    EventAlertImportance.High,
                    "경제"),
                "operating-day-wage-alert");
        }

        gameEventBus.RaiseAlert(
            $"Day {report.day} 정산",
            report.ToDetailText(),
            EventAlertImportance.Medium,
            "정산");

        if (report.staffComplaintEvents.Count > 0)
        {
            gameEventBus.RaiseStaffComplaint(
                string.Join("\n", report.staffComplaintEvents),
                report.staffComplaintEvents.Count >= 3 ? EventAlertImportance.High : EventAlertImportance.Medium);
        }
    }

    private void OnEnable()
    {
        SubscribeToScopedEvent();
    }

    private void OnDisable()
    {
        operatingDayReportSubscription?.Dispose();
        operatingDayReportSubscription = null;
    }

    private void SubscribeToScopedEvent()
    {
        if (isActiveAndEnabled && gameEventBus != null)
        {
            operatingDayReportSubscription ??=
                gameEventBus.Subscribe<OperatingDayReportEvent>(OnTriggerEvent);
        }
    }

    private static void PublishNonFatal(Action observer, string label)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(label + ":" + exception.GetType().Name);
        }
    }
}
