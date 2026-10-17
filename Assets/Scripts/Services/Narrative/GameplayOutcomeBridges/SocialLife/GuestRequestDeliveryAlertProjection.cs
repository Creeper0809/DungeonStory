using System;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Operation;

public static class GuestRequestDeliveryAlertProjection
{
    private const string AlertCategory = "V21 사회 사건";

    public static EventAlertRequest Build(
        GuestRequestDeliveryProgressedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        GuestRequestDeliverySnapshot snapshot = fact.Snapshot();
        RequireEligible(snapshot.Phase);

        EventAlertChoice[] choices = snapshot.Phase is
                GuestRequestDeliveryPhase.AwaitingVenue
                or GuestRequestDeliveryPhase.Delivering
            ? new[]
            {
                new EventAlertChoice(
                    "요청 취소",
                    "아직 인도되지 않은 물품을 기존 물류 소유권으로 회수한 뒤 요청을 거절합니다.",
                    V21ContentAlertActionIds.Society(
                        snapshot.InstanceId,
                        "decline"))
            }
            : Array.Empty<EventAlertChoice>();
        string venue = string.IsNullOrEmpty(
                snapshot.VenueFacilityInstanceId)
            ? "배정 대기"
            : $"{snapshot.VenueDisplayName} ({snapshot.Destination.x}, {snapshot.Destination.y})"
                + (string.IsNullOrWhiteSpace(
                        snapshot.VenueCapacitySummary)
                    ? string.Empty
                    : $"\n장소 여유: {snapshot.VenueCapacitySummary}");
        string progress = string.Join("\n", snapshot.Items.Select(value =>
            $"- {value.DisplayName}: 배정 {value.Assigned}, 운반 {value.Hauling}, "
            + $"도착 {value.Arrived}, 인도 {value.Delivered}/{value.Required}"));
        string reason = string.IsNullOrWhiteSpace(snapshot.StatusReason)
            ? string.Empty
            : "\n상태 이유: " + snapshot.StatusReason;
        string risk = V20SocietyEventAlertProjection.FormatRisk(
            fact.RiskTier,
            fact.RiskReason);
        return new EventAlertRequest(
            fact.RequestName,
            $"{fact.RequestDescription}\n납품 장소: {venue}\n"
                + $"진행: {DescribePhase(snapshot.Phase)}\n{progress}{reason}\n{risk}\n"
                + $"기한: {fact.DeadlineDay}일",
            V20SocietyEventAlertProjection.ToAlertImportance(
                fact.RiskTier),
            AlertCategory,
            choices,
            snapshot.InstanceId);
    }

    public static string Fingerprint(
        GuestRequestDeliveryProgressedFact fact,
        EventAlertRequest request = null)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        request ??= Build(fact);
        GuestRequestDeliverySnapshot snapshot = fact.Snapshot();
        RequireEligible(snapshot.Phase);

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

        Append(result, ((int)snapshot.Phase).ToString(
            CultureInfo.InvariantCulture));
        Append(result, snapshot.VenueFacilityInstanceId);
        Append(result, snapshot.VenueAccessCell.x.ToString(
            CultureInfo.InvariantCulture));
        Append(result, snapshot.VenueAccessCell.y.ToString(
            CultureInfo.InvariantCulture));
        Append(result, snapshot.VenueCapacitySummary);
        Append(result, snapshot.Destination.x.ToString(
            CultureInfo.InvariantCulture));
        Append(result, snapshot.Destination.y.ToString(
            CultureInfo.InvariantCulture));
        Append(result, snapshot.OperationId);
        Append(result, snapshot.CommitId);
        Append(result, snapshot.TerminalResolutionId);
        Append(result, snapshot.StatusReason);
        Append(result, snapshot.Items.Count.ToString(
            CultureInfo.InvariantCulture));
        foreach (GuestRequestDeliveryItemProgressSnapshot item in
                 snapshot.Items)
        {
            Append(result, item.ItemId);
            Append(result, item.Assigned.ToString(
                CultureInfo.InvariantCulture));
            Append(result, item.Hauling.ToString(
                CultureInfo.InvariantCulture));
            Append(result, item.Arrived.ToString(
                CultureInfo.InvariantCulture));
            Append(result, item.Delivered.ToString(
                CultureInfo.InvariantCulture));
            Append(result, item.Required.ToString(
                CultureInfo.InvariantCulture));
        }
        return result.ToString();
    }

    private static string DescribePhase(GuestRequestDeliveryPhase phase) =>
        phase switch
        {
            GuestRequestDeliveryPhase.AwaitingVenue => "시설 배정 대기",
            GuestRequestDeliveryPhase.Delivering => "물품 운반 중",
            GuestRequestDeliveryPhase.ReleasePending => "물품 회수 중",
            GuestRequestDeliveryPhase.PhysicalCommitted =>
                "물품 인도 완료 · 보상 정산 대기",
            GuestRequestDeliveryPhase.RewardPublished =>
                "보상 반영 완료 · 인도 확인 중",
            _ => "대기"
        };

    private static void RequireEligible(GuestRequestDeliveryPhase phase)
    {
        if (!Enum.IsDefined(typeof(GuestRequestDeliveryPhase), phase)
            || phase == GuestRequestDeliveryPhase.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phase),
                phase,
                "A guest-request delivery alert requires an active phase.");
        }
    }

    private static void Append(StringBuilder target, string value)
    {
        value ??= string.Empty;
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }
}
