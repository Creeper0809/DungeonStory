using UnityEngine;

public class InvasionCombatReportRuntime : MonoBehaviour
{
    private const int MaxReportHistory = 20;

    [SerializeField] private bool showActivationNotice = true;
    [SerializeField] private int maxActivationNoticeLength = 64;

    private InvasionCombatReport currentReport;
    private InvasionCombatReportSnapshot currentReportView;
    private bool isRecording;
    private DungeonStory.Foundation.IGameEventBus gameEventBus;
    private DungeonStory.Foundation.IGameClock gameClock;
    private System.IDisposable defenseFacilityTriggeredSubscription;
    private System.IDisposable invasionStartedSubscription;
    private System.IDisposable invasionSpawnedSubscription;
    private System.IDisposable invasionFacilityDamagedSubscription;
    private System.IDisposable invasionFinalCombatStartedSubscription;
    private System.IDisposable invasionResolvedSubscription;
    private readonly System.Collections.Generic.List<InvasionCombatReportSnapshot> reportHistory = new System.Collections.Generic.List<InvasionCombatReportSnapshot>();
    private System.Collections.Generic.IReadOnlyList<InvasionCombatReportSnapshot> reportHistoryView;

    public event System.Action<string, DefenseActivationSnapshot> Feedback;

    [VContainer.Inject]
    public void ConstructInvasionCombatReportRuntime(
        DungeonStory.Foundation.IGameEventBus gameEventBus,
        DungeonStory.Foundation.IGameClock gameClock)
    {
        this.gameEventBus = gameEventBus
            ?? throw new System.ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
        SubscribeToScopedEvents();
    }

    public InvasionCombatReportSnapshot CurrentReport => currentReportView;
    public System.Collections.Generic.IReadOnlyList<InvasionCombatReportSnapshot> ReportHistory
    {
        get
        {
            if (reportHistoryView == null)
            {
                reportHistoryView = reportHistory.AsReadOnly();
            }

            return reportHistoryView;
        }
    }

    public void OnTriggerEvent(InvasionStartedEvent eventType)
    {
        currentReport = new InvasionCombatReport(eventType.snapshot, RequireGameClock().Time);
        isRecording = true;
        RefreshCurrentReportView();
    }

    public void OnTriggerEvent(InvasionSpawnedEvent eventType)
    {
        EnsureReport(eventType.threatSnapshot).SetIntruder(eventType.intruderActor);
        isRecording = true;
        RefreshCurrentReportView();
    }

    public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)
    {
        if (!isRecording || currentReport == null || eventType.report == null)
        {
            return;
        }

        currentReport.RecordDefenseActivation(eventType.report);
        RefreshCurrentReportView();
        string message = InvasionCombatReportFormatter.FormatActivation(eventType.report);
        Feedback?.Invoke(message, eventType.report);

        if (showActivationNotice && !string.IsNullOrWhiteSpace(message))
        {
            GameEventBusNoticeFeedExtensions.ShowNotice(
                gameEventBus,
                ClampLine(message),
                NoticeFeedEvent.Grade.NONE);
        }
    }

    public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)
    {
        if (!isRecording || currentReport == null)
        {
            return;
        }

        currentReport.RecordFacilityDamage(eventType.facility);
        RefreshCurrentReportView();
    }

    public void OnTriggerEvent(InvasionFinalCombatStartedEvent eventType)
    {
        if (!isRecording || currentReport == null)
        {
            return;
        }

        currentReport.RecordFinalCombat(eventType.ownerActor);
        RefreshCurrentReportView();
    }

    public void OnTriggerEvent(InvasionResolvedEvent eventType)
    {
        if (currentReport == null)
        {
            return;
        }

        currentReport.Resolve(eventType.defended, eventType.residualRisk);
        isRecording = false;
        InvasionCombatReportSnapshot completedReport = currentReport.CreateSnapshot();
        currentReportView = completedReport;
        reportHistory.Insert(0, completedReport);
        if (reportHistory.Count > MaxReportHistory)
        {
            reportHistory.RemoveRange(MaxReportHistory, reportHistory.Count - MaxReportHistory);
        }

        (gameEventBus
            ?? throw new System.InvalidOperationException(
                $"{nameof(InvasionCombatReportRuntime)} requires "
                + $"{nameof(DungeonStory.Foundation.IGameEventBus)} injection."))
            .Publish(new InvasionCombatReportReadyEvent(completedReport));
        gameEventBus.RaiseInvasionResult(
            completedReport.ToDetailText(),
            eventType.defended ? EventAlertImportance.Medium : EventAlertImportance.High);
        currentReport = null;
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        defenseFacilityTriggeredSubscription?.Dispose();
        defenseFacilityTriggeredSubscription = null;
        invasionStartedSubscription?.Dispose();
        invasionStartedSubscription = null;
        invasionSpawnedSubscription?.Dispose();
        invasionSpawnedSubscription = null;
        invasionFacilityDamagedSubscription?.Dispose();
        invasionFacilityDamagedSubscription = null;
        invasionFinalCombatStartedSubscription?.Dispose();
        invasionFinalCombatStartedSubscription = null;
        invasionResolvedSubscription?.Dispose();
        invasionResolvedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        defenseFacilityTriggeredSubscription ??=
            gameEventBus.Subscribe<DefenseFacilityTriggeredEvent>(OnTriggerEvent);
        invasionStartedSubscription ??=
            gameEventBus.Subscribe<InvasionStartedEvent>(OnTriggerEvent);
        invasionSpawnedSubscription ??=
            gameEventBus.Subscribe<InvasionSpawnedEvent>(OnTriggerEvent);
        invasionFacilityDamagedSubscription ??=
            gameEventBus.Subscribe<InvasionFacilityDamagedEvent>(OnTriggerEvent);
        invasionFinalCombatStartedSubscription ??=
            gameEventBus.Subscribe<InvasionFinalCombatStartedEvent>(OnTriggerEvent);
        invasionResolvedSubscription ??=
            gameEventBus.Subscribe<InvasionResolvedEvent>(OnTriggerEvent);
    }

    private InvasionCombatReport EnsureReport(InvasionThreatSnapshot snapshot)
    {
        currentReport ??= new InvasionCombatReport(snapshot, RequireGameClock().Time);
        return currentReport;
    }

    private DungeonStory.Foundation.IGameClock RequireGameClock()
    {
        return gameClock
            ?? throw new System.InvalidOperationException(
                $"{nameof(InvasionCombatReportRuntime)} requires "
                + $"{nameof(DungeonStory.Foundation.IGameClock)} injection.");
    }

    private void RefreshCurrentReportView()
    {
        currentReportView = currentReport != null ? currentReport.CreateSnapshot() : currentReportView;
    }

    private string ClampLine(string message)
    {
        int maxLength = Mathf.Max(12, maxActivationNoticeLength);
        if (string.IsNullOrWhiteSpace(message) || message.Length <= maxLength)
        {
            return message;
        }

        return message.Substring(0, maxLength - 1) + "...";
    }
}
