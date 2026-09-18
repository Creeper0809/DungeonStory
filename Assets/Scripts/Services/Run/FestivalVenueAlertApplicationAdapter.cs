using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using VContainer.Unity;

public sealed class FestivalVenueAlertApplicationAdapter :
    IStartable,
    ITickable,
    IDisposable
{
    private readonly IFestivalDefinitionCatalog festivals;
    private readonly IFestivalExecutionQuery executions;
    private readonly IFestivalExecutionDriver driver;
    private readonly IGameCalendar calendar;
    private readonly IGameEventBus events;
    private IDisposable dayStartedSubscription;

    public FestivalVenueAlertApplicationAdapter(
        IFestivalDefinitionCatalog festivals,
        IFestivalExecutionQuery executions,
        IFestivalExecutionDriver driver,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        this.festivals = festivals
            ?? throw new ArgumentNullException(nameof(festivals));
        this.executions = executions
            ?? throw new ArgumentNullException(nameof(executions));
        this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        dayStartedSubscription ??=
            events.Subscribe<OperatingDayStartedEvent>(OnDayStarted);
        PublishForDay(calendar.Day);
    }

    public void Tick() => driver.Advance();

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        dayStartedSubscription = null;
    }

    private void OnDayStarted(OperatingDayStartedEvent started) =>
        PublishForDay(started.day);

    private void PublishForDay(int absoluteDay)
    {
        int occurrenceYear = Math.Max(1, calendar.Year);
        foreach (FestivalDefinitionSO festival in festivals.All
                     .Where(value => value != null)
                     .OrderBy(value => value.StableId, StringComparer.Ordinal))
        {
            FestivalExecutionPreview preview = executions.BuildPreview(
                festival.StableId,
                occurrenceYear);
            if (preview.AnnouncementAbsoluteDay != absoluteDay
                || executions.HasOccurrence(preview.OccurrenceId))
                continue;

            string materials = preview.Materials.Count == 0
                ? "없음"
                : string.Join(", ", preview.Materials.Select(value =>
                    $"{value.ItemId} {value.Available}/{value.Required}"));
            string venue = preview.Venue == null
                ? "장소 배정 불가: " + preview.FailureReason
                : $"장소: {preview.Venue.DisplayName} "
                    + $"({preview.Venue.AnchorCenter.x}, {preview.Venue.AnchorCenter.y})\n"
                    + $"정원/배정: {preview.Venue.EventCells.Count}/{preview.ParticipantIds.Count}명 · "
                    + $"{preview.Venue.CapacitySummary}";
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                festival.displayName,
                $"{festival.description}\n준비물(보유/필요): {materials}\n"
                + $"준비 작업: {preview.RequiredPreparationWork:0.#} WU · "
                + $"준비 기간 {preview.PreparationLeadDays}일\n"
                + $"개최/마감: {preview.FestivalAbsoluteDay}일 "
                + $"{preview.StartHour}:00 · 행사 {preview.PlannedDurationHours}시간\n"
                + $"문화·신분·필수 업무 제외 후 배정: {preview.ParticipantIds.Count}명\n"
                + venue,
                EventAlertImportance.High,
                "V21 축제",
                new[]
                {
                    new EventAlertChoice(
                        "축제 개최",
                        preview.CanHost
                            ? "물자 운반과 실제 장소 준비를 시작합니다. 마감 미충족 시 이번 회차만 취소됩니다."
                            : "현재 실제 접근·수용 조건을 충족하지 못해 개최할 수 없습니다.",
                        V21ContentAlertActionIds.Festival(
                            festival.StableId,
                            occurrenceYear)),
                    new EventAlertChoice(
                        "이번 회차 미개최",
                        "비용 없이 이번 회차를 넘기며 자동 재시작하지 않습니다.",
                        V21ContentAlertActionIds.FestivalSkip(
                            festival.StableId,
                            occurrenceYear))
                },
                preview.OccurrenceId)));
        }
    }
}
