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
}
