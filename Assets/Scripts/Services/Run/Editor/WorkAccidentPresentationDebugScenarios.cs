#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;

public static class WorkAccidentPresentationDebugScenarios
{
    private const int Day = 17;
    private const string WorkerId = "character:work-accident-presentation";
    private const string WorkerName = "Fixture Worker";
    private const string FacilityId = "building:work-accident-presentation";
    private const string FacilityName = "Fixture Forge";
    private const string NodeId = "left-arm";
    private const string Cause = "fixture-overheat";
    private const float Damage = 2.5f;

    public static void RunForBatch()
    {
        var events = new GameEventBus();
        var calendar = new Calendar(Day);
        var alerts = new List<EventAlertRequest>();
        using IDisposable capture = events.Subscribe<EventAlertRequestedEvent>(value =>
            alerts.Add(value.request));
        var observer = new WorkAccidentPresentationObserver(events, calendar);
        try
        {
            observer.Initialize();
            observer.Initialize();
            events.Publish(Accident("operation:work-accident:presentation:first", Day));
            Require(alerts.Count == 1,
                "Repeated work-accident presentation initialization duplicated the alert.");
            RequireAlert(
                alerts[0],
                "operation:work-accident:presentation:first");
            Require(string.Equals(
                    observer.LastObservedWorkAccidentOperationId,
                    "operation:work-accident:presentation:first",
                    StringComparison.Ordinal)
                && observer.LastObservedWorkAccidentAlertPublished
                && !observer.LastObservedWorkAccidentCallbackFailure.IsFailure,
                "Valid work-accident presentation reported a callback failure.");

            observer.Dispose();
            observer.Dispose();
            events.Publish(Accident("operation:work-accident:presentation:disposed", Day));
            Require(alerts.Count == 1
                && string.Equals(
                    observer.LastObservedWorkAccidentOperationId,
                    "operation:work-accident:presentation:first",
                    StringComparison.Ordinal),
                "Disposed work-accident presentation observed a later injury.");

            observer.Initialize();
            events.Publish(Accident("operation:work-accident:presentation:reinitialized", Day));
            Require(alerts.Count == 2
                && string.Equals(
                    observer.LastObservedWorkAccidentOperationId,
                    "operation:work-accident:presentation:reinitialized",
                    StringComparison.Ordinal),
                "Reinitialized work-accident presentation did not resume observation.");
            RequireAlert(
                alerts[1],
                "operation:work-accident:presentation:reinitialized");

            events.Publish(new CharacterInjuredIdentityEvent(
                new CharacterId(WorkerId),
                default,
                CombatDamageType.Blunt,
                1f,
                Day));
            Require(alerts.Count == 2
                && string.Equals(
                    observer.LastObservedWorkAccidentOperationId,
                    "operation:work-accident:presentation:reinitialized",
                    StringComparison.Ordinal),
                "A non-work five-argument injury reached work-accident presentation.");

            calendar.SetDateTime(Day + 1, 0);
            events.Publish(Accident("operation:work-accident:presentation:day-mismatch", Day));
            Require(alerts.Count == 2
                && string.Equals(
                    observer.LastObservedWorkAccidentOperationId,
                    "operation:work-accident:presentation:day-mismatch",
                    StringComparison.Ordinal)
                && !observer.LastObservedWorkAccidentAlertPublished
                && FailureIs(
                    observer.LastObservedWorkAccidentCallbackFailure,
                    "work-accident-alert-source-day-mismatch"),
                "Work-accident presentation accepted a mismatched source day.");

            events.Publish(Accident(
                "operation:work-accident:presentation:after-mismatch",
                Day + 1));
            Require(alerts.Count == 3
                && string.Equals(
                    observer.LastObservedWorkAccidentOperationId,
                    "operation:work-accident:presentation:after-mismatch",
                    StringComparison.Ordinal)
                && observer.LastObservedWorkAccidentAlertPublished
                && !observer.LastObservedWorkAccidentCallbackFailure.IsFailure,
                "The next valid work accident did not clear the prior day mismatch.");
            RequireAlert(
                alerts[2],
                "operation:work-accident:presentation:after-mismatch");

            using IDisposable throwing = events.Subscribe<EventAlertRequestedEvent>(
                _ => throw new InvalidOperationException("fixture-alert-subscriber"));
            events.Publish(Accident(
                "operation:work-accident:presentation:subscriber-failure",
                Day + 1));
            Require(alerts.Count == 4
                && string.Equals(
                    observer.LastObservedWorkAccidentOperationId,
                    "operation:work-accident:presentation:subscriber-failure",
                    StringComparison.Ordinal)
                && !observer.LastObservedWorkAccidentAlertPublished
                && FailureIs(
                    observer.LastObservedWorkAccidentCallbackFailure,
                    "work-accident-alert-callback"),
                "A throwing alert subscriber escaped or left successful diagnostics.");
        }
        finally
        {
            observer.Dispose();
        }
    }

    private static CharacterInjuredIdentityEvent Accident(
        string operationId,
        int day) => new(
        new CharacterId(WorkerId),
        default,
        CombatDamageType.Blunt,
        Damage,
        day,
        operationId,
        WorkerName,
        BuiltInWorkTypeIds.Operate,
        new BuildingInstanceId(FacilityId),
        FacilityName,
        new CoreGridCell(4, 7),
        NodeId,
        Cause,
        CharacterCommandOrigin.ScriptedForced,
        workAccidentRiskMultiplier: 1.25f);

    private static void RequireAlert(EventAlertRequest alert, string operationId)
    {
        Require(alert != null
            && string.Equals(
                alert.SourceId,
                "work-accident:" + operationId,
                StringComparison.Ordinal)
            && string.Equals(alert.Title, "작업 사고", StringComparison.Ordinal)
            && alert.Detail.Contains(WorkerName, StringComparison.Ordinal)
            && alert.Detail.Contains(
                WorkTaskCatalog.GetDisplayName(BuiltInWorkTypeIds.Operate),
                StringComparison.Ordinal)
            && alert.Detail.Contains(FacilityName, StringComparison.Ordinal)
            && alert.Detail.Contains(FacilityId, StringComparison.Ordinal)
            && alert.Detail.Contains("4,7", StringComparison.Ordinal)
            && alert.Detail.Contains(NodeId, StringComparison.Ordinal)
            && alert.Detail.Contains($"-{Damage:0.###}", StringComparison.Ordinal)
            && alert.Detail.Contains(Cause, StringComparison.Ordinal),
            "Work-accident alert lost its source, worker, facility, damage or cause detail.");
    }

    private static bool FailureIs(DomainFailure failure, string detailCode) =>
        failure.IsFailure
        && failure.Parameters.Length > 1
        && string.Equals(
            failure.Parameters[0],
            detailCode,
            StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(failure.Parameters[1]);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Calendar : IGameCalendar
    {
        internal Calendar(int day) => SetDateTime(day, 0);
        public int Day { get; private set; }
        public int Hour { get; private set; }
        private CalendarDateTime Projection => GameCalendarRules.Project(Day, Hour);
        public int Year => Projection.Year;
        public int DayOfYear => Projection.DayOfYear;
        public Season Season => Projection.Season;
        public int DayOfSeason => Projection.DayOfSeason;
        public long AbsoluteHour => Projection.AbsoluteHour;
        public float ElapsedSeconds => (Day - 1) * GameCalendarRules.SecondsPerDay
            + Hour * GameCalendarRules.SecondsPerGameHour;
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => Projection;
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int day, int hour)
        {
            Day = Math.Max(1, day);
            Hour = Math.Clamp(hour, 0, GameCalendarRules.HoursPerDay - 1);
        }
    }
}
#endif
