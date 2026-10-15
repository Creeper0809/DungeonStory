using System;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using VContainer.Unity;

public interface IWorkAccidentPresentationDiagnostic
{
    string LastObservedWorkAccidentOperationId { get; }
    bool LastObservedWorkAccidentAlertPublished { get; }
    DomainFailure LastObservedWorkAccidentCallbackFailure { get; }
}

public sealed class WorkAccidentPresentationObserver : IInitializable, IDisposable, IWorkAccidentPresentationDiagnostic
{
    private readonly IGameEventBus events;
    private readonly IGameCalendar calendar;
    private IDisposable subscription;
    public WorkAccidentPresentationObserver(IGameEventBus events, IGameCalendar calendar)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }
    public void Initialize() => subscription ??= events.Subscribe<CharacterInjuredIdentityEvent>(OnWorkAccidentCommitted);
    public void Dispose() { subscription?.Dispose(); subscription = null; }

    public string LastObservedWorkAccidentOperationId { get; private set; } =
        string.Empty;
    public bool LastObservedWorkAccidentAlertPublished { get; private set; }
    public DomainFailure LastObservedWorkAccidentCallbackFailure
        { get; private set; }

    private void OnWorkAccidentCommitted(
        CharacterInjuredIdentityEvent committed)
    {
        if (!committed.HasWorkAccidentEvidence)
        {
            return;
        }

        LastObservedWorkAccidentOperationId =
            committed.WorkAccidentOperationId;
        LastObservedWorkAccidentAlertPublished = false;
        LastObservedWorkAccidentCallbackFailure = DomainFailure.None;
        try
        {
            int currentDay = Math.Max(0, calendar.Day);
            if (committed.AbsoluteDay != currentDay)
            {
                LastObservedWorkAccidentCallbackFailure = new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    "work-accident-alert-source-day-mismatch",
                    $"Committed day {committed.AbsoluteDay} does not match current day {currentDay}.");
                return;
            }

            string sourceId = "work-accident:"
                + committed.WorkAccidentOperationId;
            string detail =
                $"피해 대상: {committed.WorkAccidentWorkerDisplayName} ({committed.Character.Value})\n"
                + $"작업: {WorkTaskCatalog.GetDisplayName(committed.WorkAccidentWorkTypeId)}\n"
                + $"현장: {committed.WorkAccidentFacilityDisplayName} "
                + $"({committed.WorkAccidentFacilityInstanceId.Value}, "
                + $"{committed.WorkAccidentLocation.X},{committed.WorkAccidentLocation.Y})\n"
                + $"실제 피해: {committed.WorkAccidentDamagedAnatomyNodeId} "
                + $"-{committed.AppliedDamage:0.###}\n"
                + $"발생 당시 확인된 원인: {committed.WorkAccidentObservedCause}\n"
                + "필요 대응: 의료·회복 화면에서 손상 부위를 실제로 치료하거나 회복 상태를 확인해야 합니다. "
                + "이 알림을 닫아도 피해는 복구되지 않습니다.";
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "작업 사고",
                detail,
                EventAlertImportance.Medium,
                "V21 사회 사건",
                Array.Empty<EventAlertChoice>(),
                sourceId)));
            LastObservedWorkAccidentAlertPublished = true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            // Anatomy damage is authoritative and has already committed. Alert
            // projection can report failure but must not replay or compensate it.
            LastObservedWorkAccidentCallbackFailure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "work-accident-alert-callback",
                exception.Message);
        }
    }

}
