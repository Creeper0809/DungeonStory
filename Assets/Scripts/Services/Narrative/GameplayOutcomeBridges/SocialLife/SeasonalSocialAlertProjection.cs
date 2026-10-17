using System;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Operation;

public static class SeasonalSocialAlertProjection
{
    private const string WildlifeCategory = "V20 계절 야생동물";
    private const string CargoCategory = "V20 계절 표류 화물";
    private const string FestivalCategory = "V21 축제";

    public static EventAlertRequest Build(
        SeasonalWildlifeProgressedFact fact)
    {
        int remaining = Math.Max(0, fact.ExactCount - fact.SpawnedCount);
        string locations = fact.Positions.Count == 0
            ? "미확정"
            : string.Join(", ", fact.Positions.Select(value =>
                value.HasValue
                    ? $"({value.Value.x},{value.Value.y})"
                    : "?"));
        string failure = string.IsNullOrEmpty(fact.FailureReason)
            ? string.Empty
            : $"\n현재 지연: {fact.FailureReason}";
        return new EventAlertRequest(
            fact.EventName,
            $"대상: {fact.SpeciesName} {fact.ExactCount}마리\n"
                + $"도착 확정: {fact.SpawnedCount}마리 / 남은 생성: {remaining}마리\n"
                + $"외부 위치: {locations}\n"
                + $"기한: {fact.DeadlineDay}일 "
                + $"(남은 {Math.Max(0, fact.DeadlineDay - fact.AbsoluteDay)}일)"
                + failure,
            fact.Qualification
                == SeasonalWildlifeArrivalQualification.Predatory
                    ? EventAlertImportance.High
                    : EventAlertImportance.Medium,
            WildlifeCategory,
            Array.Empty<EventAlertChoice>(),
            fact.OccurrenceId);
    }

    public static EventAlertRequest Build(SeasonalWildlifeExpiredFact fact) =>
        new(
            fact.EventName,
            $"계절 방문 기한이 {fact.AbsoluteDay}일에 종료되었습니다. 이미 도착한 개체는 유지되고 미생성 잔여만 취소됩니다.",
            fact.Qualification
                == SeasonalWildlifeArrivalQualification.Predatory
                    ? EventAlertImportance.High
                    : EventAlertImportance.Medium,
            WildlifeCategory,
            Array.Empty<EventAlertChoice>(),
            fact.OccurrenceId,
            isResolved: true,
            resultSummary: "기한 종료: 미생성 잔여 취소, 기존 개체 유지");

    public static EventAlertRequest Build(SeasonalCargoProgressedFact fact)
    {
        string location = fact.Position.HasValue
            ? $"({fact.Position.Value.x},{fact.Position.Value.y})"
            : "미확정";
        string failure = string.IsNullOrEmpty(fact.FailureReason)
            ? string.Empty
            : $"\n현재 지연: {fact.FailureReason}";
        return new EventAlertRequest(
            fact.EventName,
            $"대상: {fact.ItemName} {fact.ExactQuantity}개\n"
                + $"외곽 위치: {location}\n"
                + $"미회수: {fact.RemainingQuantity}개 / "
                + $"{fact.RemainingQuantity * fact.UnitMassGrams / 1000f:0.###}kg\n"
                + $"실제 픽업 확보: {fact.SecuredQuantity}개 / "
                + $"현재 픽업 가능: {fact.AvailableQuantity}개\n"
                + $"기한: {fact.DeadlineDay}일 "
                + $"(남은 {Math.Max(0, fact.DeadlineDay - fact.AbsoluteDay)}일)"
                + failure,
            EventAlertImportance.Medium,
            CargoCategory,
            Array.Empty<EventAlertChoice>(),
            fact.OccurrenceId);
    }

    public static EventAlertRequest Build(SeasonalCargoExpiredFact fact) =>
        new(
            fact.EventName,
            $"회수 기한이 {fact.AbsoluteDay}일에 끝났습니다. "
                + $"{fact.ItemName} 확보 {fact.SecuredQuantity}개, "
                + $"미픽업 원본 유실 {fact.ExpiredQuantity}개 "
                + $"({fact.ExpiredMassGrams / 1000f:0.###}kg). "
                + "운반 중·회수 드롭·일반 재고는 유지됩니다.",
            EventAlertImportance.Medium,
            CargoCategory,
            Array.Empty<EventAlertChoice>(),
            fact.OccurrenceId,
            isResolved: true,
            resultSummary:
                $"확보 {fact.SecuredQuantity} / 원본 유실 {fact.ExpiredQuantity}");

    public static EventAlertRequest Build(FestivalExecutionProgressedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        bool terminal = fact.Phase is FestivalExecutionPhase.Cancelled
            or FestivalExecutionPhase.Stopped
            or FestivalExecutionPhase.Resolved
            or FestivalExecutionPhase.Declined;
        string attendance = fact.Attendance.Count == 0
            ? "배정 참가자 없음"
            : string.Join(
                ", ",
                fact.Attendance.Select(value =>
                    $"{value.CharacterId} {FestivalExecutionRules.AttendanceRatio(value.AttendedSeconds, fact.PlannedDurationSeconds) * 100f:0}%"));
        string detail = $"장소: {fact.VenueFacilityInstanceId} · 정원 {fact.VenueCapacity}명 · 배정 {fact.Attendance.Count}명\n"
            + $"준비: {fact.CompletedPreparationWork:0.0}/{fact.RequiredPreparationWork:0.0} WU\n"
            + $"개최/마감: {fact.DeadlineDay}일 {fact.DeadlineHour}:00 · 예정 {fact.PlannedDurationSeconds / GameCalendarRules.SecondsPerGameHour:0}시간\n"
            + $"실제 참석: {attendance}\n상태: {fact.LastStatus}";
        return new EventAlertRequest(
            fact.FestivalName,
            detail,
            fact.Phase == FestivalExecutionPhase.Running
                ? EventAlertImportance.High
                : EventAlertImportance.Medium,
            FestivalCategory,
            Array.Empty<EventAlertChoice>(),
            fact.OccurrenceId,
            isResolved: terminal,
            resultSummary: terminal
                ? string.IsNullOrWhiteSpace(fact.ResultSummary)
                    ? fact.LastStatus
                    : fact.ResultSummary
                : string.Empty);
    }

    public static string FestivalFingerprint(
        FestivalExecutionProgressedFact fact,
        EventAlertRequest request = null)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        request ??= Build(fact);
        var result = new StringBuilder();
        Append(result, request.Title);
        Append(result, request.Detail);
        Append(result, ((int)request.Importance).ToString(
            CultureInfo.InvariantCulture));
        Append(result, request.Category);
        Append(result, request.SourceId);
        Append(result, request.IsResolved ? "1" : "0");
        Append(result, request.ResultSummary);
        Append(result, request.Choices.Count.ToString(
            CultureInfo.InvariantCulture));
        foreach (EventAlertChoice choice in request.Choices)
        {
            Append(result, choice.Label);
            Append(result, choice.Description);
            Append(result, choice.ActionId);
        }
        Append(result, fact.ElapsedFestivalSeconds.ToString(
            "0.0",
            CultureInfo.InvariantCulture));
        Append(result, fact.InputOwnerActive ? "1" : "0");
        Append(result, fact.MaterialReceiptPending ? "1" : "0");
        Append(result, fact.StateGeneration.ToString(
            CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static void Append(StringBuilder target, string value)
    {
        value ??= string.Empty;
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }
}
