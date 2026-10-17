#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

public static class GuestDeliveryPresentationDebugScenarios
{
    public const int ExpectedDeliberateErrorCount = 1;
    private const string InstanceId = "guest:b128";
    private const string RequestName = "목재 지원 요청";
    private const string RequestDescription = "방문객이 목재 지원을 요청했습니다.";
    private const string RiskReason = "known-risk";

    public static void RunForBatch()
    {
        VerifyFrozenProjection();
        VerifyPhaseProjection();
        VerifyObserverLifecycleAndRetry();
        VerifyInputGuards();
    }

    private static void VerifyFrozenProjection()
    {
        var items = new List<GuestRequestDeliveryItemProgressSnapshot>
        {
            Wood(assigned: 1, hauling: 1, arrived: 0, delivered: 0)
        };
        GuestRequestDeliverySnapshot source = Snapshot(
            GuestRequestDeliveryPhase.AwaitingVenue,
            items);
        GuestRequestDeliveryProgressedFact fact = Fact(source);
        string expected = GuestRequestDeliveryAlertProjection.Fingerprint(fact);

        source.Phase = GuestRequestDeliveryPhase.RewardPublished;
        source.VenueDisplayName = "변경된 장소";
        source.Destination = new Vector2Int(99, 99);
        source.StatusReason = "변경됨";
        items.Clear();
        items.Add(Wood(2, 0, 0, 2));
        Require(string.Equals(
                expected,
                GuestRequestDeliveryAlertProjection.Fingerprint(fact),
                StringComparison.Ordinal),
            "Source snapshot/list mutation changed the frozen fact projection.");

        GuestRequestDeliverySnapshot accessor = fact.Snapshot();
        accessor.Phase = GuestRequestDeliveryPhase.ReleasePending;
        accessor.VenueDisplayName = "접근자 변경";
        accessor.Items = Array.Empty<GuestRequestDeliveryItemProgressSnapshot>();
        Require(string.Equals(
                expected,
                GuestRequestDeliveryAlertProjection.Fingerprint(fact),
                StringComparison.Ordinal),
            "Snapshot accessor mutation changed the frozen fact projection.");
    }

    private static void VerifyPhaseProjection()
    {
        var expected = new Dictionary<GuestRequestDeliveryPhase, string>
        {
            [GuestRequestDeliveryPhase.AwaitingVenue] = "시설 배정 대기",
            [GuestRequestDeliveryPhase.Delivering] = "물품 운반 중",
            [GuestRequestDeliveryPhase.ReleasePending] = "물품 회수 중",
            [GuestRequestDeliveryPhase.PhysicalCommitted] =
                "물품 인도 완료 · 보상 정산 대기",
            [GuestRequestDeliveryPhase.RewardPublished] =
                "보상 반영 완료 · 인도 확인 중"
        };
        foreach (KeyValuePair<GuestRequestDeliveryPhase, string> entry in
                 expected)
        {
            GuestRequestDeliveryProgressedFact fact = Fact(Snapshot(entry.Key));
            EventAlertRequest request =
                GuestRequestDeliveryAlertProjection.Build(fact);
            string detail = RequestDescription
                + "\n납품 장소: 검증 회관 (4, 5)\n장소 여유: 2/4"
                + $"\n진행: {entry.Value}\n"
                + "- 목재: 배정 1, 운반 1, 도착 0, 인도 0/2"
                + "\n상태 이유: 운반 경로 대기"
                + "\n예상 결과 위험: 회복 가능한 손실"
                + $"\n위험 근거: {RiskReason}\n기한: 9일";
            Require(request.Title == RequestName
                    && request.Category == "V21 사회 사건"
                    && request.Importance == EventAlertImportance.Medium
                    && request.SourceId == InstanceId
                    && request.Detail == detail
                    && !request.IsResolved
                    && request.ResultSummary.Length == 0,
                "Guest delivery alert visible projection differs for "
                + entry.Key + ".");

            bool cancellable = entry.Key is
                GuestRequestDeliveryPhase.AwaitingVenue
                or GuestRequestDeliveryPhase.Delivering;
            Require(request.Choices.Count == (cancellable ? 1 : 0),
                "Guest delivery cancellation eligibility differs for "
                + entry.Key + ".");
            if (!cancellable) continue;
            EventAlertChoice choice = request.Choices[0];
            Require(choice.Label == "요청 취소"
                    && choice.Description
                        == "아직 인도되지 않은 물품을 기존 물류 소유권으로 회수한 뒤 요청을 거절합니다."
                    && choice.ActionId == V21ContentAlertActionIds.Society(
                        InstanceId,
                        "decline"),
                "Guest delivery cancellation action differs.");
        }
    }

    private static void VerifyObserverLifecycleAndRetry()
    {
        var events = new GameEventBus();
        var observed = new List<EventAlertRequest>();
        using IDisposable capture = events.Subscribe<EventAlertRequestedEvent>(
            value => observed.Add(value.request));
        var observer = new GuestRequestDeliveryPresentationObserver(events);
        GuestRequestDeliveryProgressedFact baseline = Fact(Snapshot(
            GuestRequestDeliveryPhase.AwaitingVenue));

        observer.Initialize();
        observer.Initialize();
        events.Publish(baseline);
        events.Publish(baseline);
        Require(observed.Count == 1,
            "Double initialization or exact replay bypassed deduplication.");

        observer.Dispose();
        observer.Dispose();
        events.Publish(baseline);
        Require(observed.Count == 1,
            "Disposed guest delivery observer still emitted an alert.");

        observer.Initialize();
        events.Publish(baseline);
        Require(observed.Count == 2,
            "Reinitialized guest delivery observer did not emit.");
        observer.OnRestoreCompleted();
        events.Publish(baseline);
        Require(observed.Count == 3,
            "Restore completion did not clear guest delivery deduplication.");
        events.Publish(new GuestRequestDeliveryFinalizedFact(InstanceId));
        events.Publish(baseline);
        Require(observed.Count == 4,
            "Finalized fact did not clear its guest delivery cache entry.");

        events.Publish(Fact(
            Snapshot(GuestRequestDeliveryPhase.AwaitingVenue),
            requestName: "수정된 목재 지원 요청"));
        events.Publish(Fact(
            Snapshot(GuestRequestDeliveryPhase.AwaitingVenue),
            requestName: "수정된 목재 지원 요청",
            deadlineDay: 10));
        GuestRequestDeliverySnapshot changed = Snapshot(
            GuestRequestDeliveryPhase.Delivering);
        changed.StatusReason = "가시 상태 변경";
        events.Publish(Fact(
            changed,
            requestName: "수정된 목재 지원 요청",
            deadlineDay: 10));
        Require(observed.Count == 7,
            "Authored name, deadline, or visible state was wrongly suppressed.");

        events.Publish(new GuestRequestDeliveryFinalizedFact(InstanceId));
        int deliberateErrors = 0;
        int faultSubscriberInvocations = 0;
        using IDisposable fault = events.Subscribe<EventAlertRequestedEvent>(_ =>
        {
            faultSubscriberInvocations++;
            if (deliberateErrors == 0)
            {
                deliberateErrors++;
                throw new InvalidOperationException(
                    "b128-deliberate-alert-subscriber-failure");
            }
        });
        int beforeRetry = observed.Count;
        events.Publish(baseline);
        events.Publish(baseline);
        events.Publish(baseline);
        Require(deliberateErrors == ExpectedDeliberateErrorCount
                && faultSubscriberInvocations == 2,
            "Alert subscriber did not receive the retry and committed emission.");
        Require(observed.Count == beforeRetry + 2,
            "Failed alert emission was cached or exact replay was not deduplicated.");
        observer.Dispose();
    }

    private static void VerifyInputGuards()
    {
        RequireThrows<ArgumentOutOfRangeException>(() =>
            GuestRequestDeliveryAlertProjection.Build(
                Fact(Snapshot(GuestRequestDeliveryPhase.None))));
        RequireThrows<ArgumentOutOfRangeException>(() =>
            GuestRequestDeliveryAlertProjection.Build(
                Fact(Snapshot((GuestRequestDeliveryPhase)999))));
    }

    private static GuestRequestDeliveryProgressedFact Fact(
        GuestRequestDeliverySnapshot snapshot,
        string requestName = RequestName,
        int deadlineDay = 9) => new(
        snapshot,
        requestName,
        RequestDescription,
        ExperienceEventRiskTier.Recoverable,
        RiskReason,
        deadlineDay);

    private static GuestRequestDeliverySnapshot Snapshot(
        GuestRequestDeliveryPhase phase,
        IReadOnlyList<GuestRequestDeliveryItemProgressSnapshot> items = null) =>
        new()
        {
            InstanceId = InstanceId,
            DefinitionId = "guest-request:b128",
            Phase = phase,
            VenueFacilityInstanceId = "facility:b128",
            VenueDisplayName = "검증 회관",
            VenueAccessCell = new Vector2Int(4, 5),
            VenueCapacitySummary = "2/4",
            DestinationId = "guest-delivery:b128",
            Destination = new Vector2Int(4, 5),
            OperationId = "guest-delivery-operation:b128",
            CommitId = "guest-delivery-commit:b128",
            RequestFingerprint = "guest-delivery-request:b128",
            TerminalResolutionId = string.Empty,
            StatusReason = "운반 경로 대기",
            Items = items ?? new[]
            {
                Wood(assigned: 1, hauling: 1, arrived: 0, delivered: 0)
            }
        };

    private static GuestRequestDeliveryItemProgressSnapshot Wood(
        int assigned,
        int hauling,
        int arrived,
        int delivered) => new(
        "resource:wood",
        "목재",
        required: 2,
        assigned: assigned,
        hauling: hauling,
        arrived: arrived,
        delivered: delivered);

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }

    private static void RequireThrows<T>(Action action)
        where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException(
            "Expected " + typeof(T).Name + ".");
    }
}
#endif
