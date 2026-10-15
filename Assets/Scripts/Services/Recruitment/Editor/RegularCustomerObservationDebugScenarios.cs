#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

public static class RegularCustomerObservationDebugScenarios
{
    internal static void RunOrThrow()
    {
        GameEventBus events = new();
        RegularCustomerObservationObserver observer = new(events);
        List<EventAlertRequest> alerts = new();
        using IDisposable capture =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                if (value.request != null)
                {
                    alerts.Add(value.request);
                }
            });

        RegularCustomerSnapshot customer = new(
            "character:b108:customer",
            "검증 손님",
            "human",
            4,
            72.5f,
            RegularCustomerStatus.RecruitCandidate,
            RecruitCapability.Staff
                | RecruitCapability.Defense
                | RecruitCapability.Expedition);
        string sourceSummary = customer.ToSummaryText();
        RegularCustomerObservation regular = new(
            RegularCustomerObservationKind.BecameRegular,
            customer);
        Require(
            !ReferenceEquals(regular.Customer, customer)
            && customer.ToSummaryText() == sourceSummary,
            "The observation did not detach its frozen customer snapshot.");

        try
        {
            observer.Initialize();
            observer.Initialize();

            events.Publish(regular);
            events.Publish(new RegularCustomerObservation(
                RegularCustomerObservationKind.BecameRecruitCandidate,
                customer));
            events.Publish(new RegularCustomerObservation(
                RegularCustomerObservationKind.ExpeditionCandidate,
                customer));
            PublishRecruitmentBranches(events, customer);

            Require(alerts.Count == 7,
                "Regular-customer presentation duplicated or lost an alert.");
            RequireAlert(
                alerts[0],
                "단골 등장",
                "검증 손님이 단골이 되었습니다.\n" + sourceSummary,
                EventAlertImportance.Low,
                "단골");
            RequireAlert(
                alerts[1],
                "영입 후보",
                "검증 손님을 영입할 수 있습니다.\n가능 역할: 직원/방어/원정",
                EventAlertImportance.Medium,
                "영입");
            RequireAlert(
                alerts[2],
                "원정 영입 후보",
                "검증 손님이 원정 보상으로 영입 후보가 되었습니다.\n가능 역할: 직원/방어/원정",
                EventAlertImportance.Medium,
                "영입");
            RequireRecruitmentAlert(
                alerts[3], "손님 영입", complete: true, "영입");
            RequireRecruitmentAlert(
                alerts[4], "손님 영입", complete: false, "영입");
            RequireRecruitmentAlert(
                alerts[5], "용병 계약", complete: true, "고용");
            RequireRecruitmentAlert(
                alerts[6], "용병 계약", complete: false, "고용");
            Require(
                customer.ToSummaryText() == sourceSummary
                && customer.customerId == "character:b108:customer"
                && customer.displayName == "검증 손님"
                && customer.visitCount == 4
                && customer.averageSatisfaction == 72.5f,
                "Presentation mutated the source customer snapshot.");

            observer.Dispose();
            observer.Dispose();
            events.Publish(regular);
            Require(alerts.Count == 7,
                "A disposed regular-customer observer remained subscribed.");

            observer.Initialize();
            observer.Initialize();
            events.Publish(regular);
            Require(alerts.Count == 8,
                "A reinitialized regular-customer observer did not publish exactly once.");
            RequireAlert(
                alerts[7],
                "단골 등장",
                "검증 손님이 단골이 되었습니다.\n" + sourceSummary,
                EventAlertImportance.Low,
                "단골");

            Debug.Log("B108_RECRUITMENT_PRESENTATION=PASS");
        }
        finally
        {
            observer.Dispose();
        }
    }

    private static void PublishRecruitmentBranches(
        IGameEventBus events,
        RegularCustomerSnapshot customer)
    {
        events.Publish(new RegularCustomerObservation(
            RegularCustomerObservationKind.Recruited,
            customer,
            RegularCustomerRecruitDeliveryKind.Staff,
            deliveredImmediately: true));
        events.Publish(new RegularCustomerObservation(
            RegularCustomerObservationKind.Recruited,
            customer,
            RegularCustomerRecruitDeliveryKind.Staff));
        events.Publish(new RegularCustomerObservation(
            RegularCustomerObservationKind.Recruited,
            customer,
            RegularCustomerRecruitDeliveryKind.Mercenary,
            deliveredImmediately: true));
        events.Publish(new RegularCustomerObservation(
            RegularCustomerObservationKind.Recruited,
            customer,
            RegularCustomerRecruitDeliveryKind.Mercenary));
    }

    private static void RequireRecruitmentAlert(
        EventAlertRequest value,
        string title,
        bool complete,
        string category) =>
        RequireAlert(
            value,
            title,
            complete
                ? "검증 손님 영입 완료\n가능 역할: 직원/방어/원정"
                : "검증 손님 영입 확정; 캐릭터 배치 재시도 대기",
            EventAlertImportance.Medium,
            category);

    private static void RequireAlert(
        EventAlertRequest value,
        string title,
        string detail,
        EventAlertImportance importance,
        string category)
    {
        Require(
            value != null
            && value.Title == title
            && value.Detail == detail
            && value.Importance == importance
            && value.Category == category,
            "Regular-customer alert payload changed: " + title);
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }
}
#endif
