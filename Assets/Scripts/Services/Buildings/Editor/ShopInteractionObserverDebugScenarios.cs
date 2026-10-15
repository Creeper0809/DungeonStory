#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ShopInteractionObserverDebugScenarios
{
    private const string FacilityName = "관찰자 상점";
    private const string ActorName = "관찰자 손님";

    public static void RunOrThrow()
    {
        GameObject firstObject = null;
        GameObject secondObject = null;
        try
        {
            firstObject = new GameObject("Shop Interaction Observer A");
            secondObject = new GameObject("Shop Interaction Observer B");
            Shop first = firstObject.AddComponent<Shop>();
            Shop second = secondObject.AddComponent<Shop>();
            IBuildingVisitorPort actor = VisitorProbe.Create(
                ActorName,
                out VisitorProbe actorProbe);

            VerifyProjectionTable(first, actor);
            VerifyObserverLifecycleAndWeakState(
                first,
                second,
                actor,
                actorProbe);
        }
        finally
        {
            if (secondObject != null)
            {
                Object.DestroyImmediate(secondObject);
            }

            if (firstObject != null)
            {
                Object.DestroyImmediate(firstObject);
            }
        }
    }

    private static void VerifyProjectionTable(
        Shop facility,
        IBuildingVisitorPort actor)
    {
        ProjectionCase[] cases =
        {
            new(
                "admission-rejected",
                Observe(
                    ShopInteractionTransition.AdmissionRejected,
                    facility,
                    actor,
                    reason: "admission-closed"),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Failed,
                    $"{FacilityName} 이용 실패: admission-closed",
                    reason: "admission-closed",
                    bubble: true)),
            new(
                "session-rejected",
                Observe(
                    ShopInteractionTransition.SessionRejected,
                    facility,
                    actor,
                    reason: "CapacityExceeded"),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Failed,
                    $"{FacilityName} 이용 실패",
                    reason: "CapacityExceeded",
                    bubble: true)),
            new(
                "self-checkout-started",
                Observe(
                    ShopInteractionTransition.SelfCheckoutStarted,
                    facility,
                    actor),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Progress,
                    $"{FacilityName} 셀프 계산 중",
                    action: "checkout:self-service")),
            new(
                "staff-use-completed",
                Observe(
                    ShopInteractionTransition.StaffUseCompleted,
                    facility,
                    actor),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Completed,
                    $"{FacilityName} 직원 이용: 매출 제외",
                    reason: "staff-no-revenue")),
            new(
                "attendant-waiting-started",
                Observe(
                    ShopInteractionTransition.AttendantWaitingStarted,
                    facility,
                    actor),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Blocked,
                    $"{FacilityName}에서 계산할 직원을 기다리기 시작했다.",
                    action: "checkout:staffed",
                    reason: "no-serving-worker",
                    bubble: true)),
            new(
                "staff-checkout-started",
                Observe(
                    ShopInteractionTransition.StaffCheckoutStarted,
                    facility,
                    actor),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Started,
                    $"{FacilityName}에서 계산을 시작했다.",
                    action: "checkout:staffed")),
            new(
                "retail-failed",
                Observe(
                    ShopInteractionTransition.RetailFailed,
                    facility,
                    actor,
                    reason: "no-purchasable-item",
                    detail: "결제 가능한 상품이 없습니다."),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Failed,
                    $"{FacilityName} 이용 실패: 결제 가능한 상품이 없습니다.",
                    reason: "no-purchasable-item",
                    bubble: true)),
            new(
                "meal-supply-changed",
                Observe(
                    ShopInteractionTransition.MealSupplyChanged,
                    facility,
                    actor),
                Activity(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Cancelled,
                    "Meal supply changed before commit; retrying selection.",
                    reason: "meal-supply-retry")),
            new(
                "meal-no-longer-needed",
                Observe(
                    ShopInteractionTransition.MealNoLongerNeeded,
                    facility,
                    actor),
                Activity(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Cancelled,
                    "Meal service retired because the need was already satisfied.",
                    reason: "meal-no-longer-needed")),
            new(
                "meal-failed",
                Observe(
                    ShopInteractionTransition.MealFailed,
                    facility,
                    actor,
                    reason: "PhysicalConsumptionFailed",
                    detail: "meal-action-timeout"),
                Activity(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Failed,
                    "PhysicalConsumptionFailed:meal-action-timeout",
                    reason: "PhysicalConsumptionFailed",
                    bubble: true)),
            new(
                "meal-unpaid",
                Observe(
                    ShopInteractionTransition.MealUnpaid,
                    facility,
                    actor,
                    itemName: "수프"),
                Activity(
                    BuildingActivityKinds.Social,
                    BuildingActivityOutcomes.Damaged,
                    "수프 값을 치르지 못하고 쫓겨남",
                    reason: "meal-unaffordable",
                    bubble: true)),
            new(
                "meal-completed",
                Observe(
                    ShopInteractionTransition.MealCompleted,
                    facility,
                    actor,
                    itemName: "수프",
                    revenue: 7),
                Activity(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Completed,
                    "수프 식사 완료",
                    value: 7f)),
            new(
                "checkout-restless",
                Observe(
                    ShopInteractionTransition.CheckoutRestless,
                    facility,
                    actor,
                    elapsedSeconds: 12f),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Progress,
                    $"{ActorName}은 계산 줄이 좀처럼 줄지 않아 초조해졌다.",
                    action: "checkout:waiting",
                    reason: "queue-restless",
                    value: 12f,
                    bubble: true)),
            new(
                "checkout-service-requested",
                Observe(
                    ShopInteractionTransition.CheckoutServiceRequested,
                    facility,
                    actor,
                    elapsedSeconds: 21f,
                    patienceMultiplier: 1.2f),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Responded,
                    $"{ActorName}은 계산을 도와줄 직원을 불렀다.",
                    action: "checkout:service-request",
                    reason: "customer-called-worker",
                    value: 21f,
                    bubble: true)),
            new(
                "checkout-abandoned",
                Observe(
                    ShopInteractionTransition.CheckoutAbandoned,
                    facility,
                    actor,
                    elapsedSeconds: 33f,
                    abandonSentiment: -0.6f),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Cancelled,
                    $"{ActorName}은 계산을 기다리다 구매를 포기했다.",
                    action: "checkout:abandoned",
                    reason: "patience-exhausted",
                    value: 33f,
                    bubble: true)),
            new(
                "interaction-aborted",
                Observe(
                    ShopInteractionTransition.InteractionAborted,
                    facility,
                    actor,
                    detail: "action-replaced-during-shop-interaction"),
                Activity(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Cancelled,
                    $"{FacilityName} interaction aborted: action-replaced-during-shop-interaction",
                    action: "shop:interaction",
                    reason: "action-replaced-during-shop-interaction"))
        };

        Require(cases.Length == 16, "The shop projection table no longer covers 16 activities.");
        for (int index = 0; index < cases.Length; index++)
        {
            ProjectionCase current = cases[index];
            RequireActivity(
                ShopInteractionObserver.Project(current.Observation),
                current.Expected,
                current.Name);
        }

        BuildingActivitySnapshot impatient = ShopInteractionObserver.Project(
            Observe(
                ShopInteractionTransition.CheckoutServiceRequested,
                facility,
                actor,
                elapsedSeconds: 22f,
                patienceMultiplier: 1f));
        Require(
            impatient.FactText == $"{ActorName}은 더 기다리기 어렵다며 직원을 찾았다.",
            "The low-patience checkout request text changed.");

        bool queueRejected = false;
        try
        {
            ShopInteractionObserver.Project(
                Observe(
                    ShopInteractionTransition.CheckoutQueueSampled,
                    facility,
                    actor,
                    waitingCount: 0));
        }
        catch (InvalidOperationException)
        {
            queueRejected = true;
        }

        Require(queueRejected, "A checkout queue sample projected a fake activity.");
    }

    private static void VerifyObserverLifecycleAndWeakState(
        Shop first,
        Shop second,
        IBuildingVisitorPort actor,
        VisitorProbe actorProbe)
    {
        GameEventBus events = new();
        ShopInteractionObserver observer = new(events);
        List<EventAlertRequest> alerts = new();
        using IDisposable alertSubscription =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                if (value.request != null)
                {
                    alerts.Add(value.request);
                }
            });

        try
        {
            observer.Initialize();
            observer.Initialize();

            PublishServiceRequest(events, first, actor, 10f);
            PublishServiceRequest(events, first, actor, 11f);
            RequireAlertCount(alerts, "계산 지원 필요", 1,
                "Repeated service requests duplicated the per-shop alert.");

            events.Publish(Observe(
                ShopInteractionTransition.CheckoutQueueSampled,
                first,
                actor,
                waitingCount: 1));
            PublishServiceRequest(events, first, actor, 12f);
            RequireAlertCount(alerts, "계산 지원 필요", 1,
                "A non-empty queue reset the service-request alert.");

            events.Publish(Observe(
                ShopInteractionTransition.CheckoutQueueSampled,
                first,
                actor,
                waitingCount: 0));
            PublishServiceRequest(events, first, actor, 13f);
            RequireAlertCount(alerts, "계산 지원 필요", 2,
                "An empty queue did not reset the service-request alert.");

            PublishServiceRequest(events, second, actor, 14f);
            RequireAlertCount(alerts, "계산 지원 필요", 3,
                "A second shop did not receive independent service-alert state.");
            EventAlertRequest serviceAlert = alerts.Last(value =>
                value.Title == "계산 지원 필요");
            Require(
                serviceAlert.Detail ==
                    $"{ActorName}이(가) {FacilityName}에서 계산 직원을 찾고 있습니다."
                && serviceAlert.Importance == EventAlertImportance.Low
                && serviceAlert.Category == "손님",
                "The service-request alert payload changed.");

            PublishAbandon(events, first, actor, 0f);
            PublishAbandon(events, first, actor, 4.999f);
            RequireAlertCount(alerts, "손님이 구매를 포기함", 1,
                "The first shop ignored the five-second abandon alert boundary.");
            PublishAbandon(events, first, actor, 5f);
            RequireAlertCount(alerts, "손님이 구매를 포기함", 2,
                "The abandon alert did not reopen at exactly five seconds.");
            PublishAbandon(events, second, actor, 0f);
            RequireAlertCount(alerts, "손님이 구매를 포기함", 3,
                "A second shop shared the first shop's abandon throttle.");
            Require(
                actorProbe.MemoryCount == 4
                && ReferenceEquals(actorProbe.LastMemoryFacility, second)
                && actorProbe.LastMemorySentiment == -0.5f
                && actorProbe.LastMemoryDetail == "계산이 너무 늦어 구매를 포기했다.",
                "Checkout abandonment did not remember exactly one facility experience per event.");

            EventAlertRequest abandonAlert = alerts.Last(value =>
                value.Title == "손님이 구매를 포기함");
            Require(
                abandonAlert.Detail ==
                    $"{ActorName}이(가) {FacilityName}의 긴 계산 대기 때문에 다른 곳을 찾습니다."
                && abandonAlert.Importance == EventAlertImportance.Medium
                && abandonAlert.Category == "손님",
                "The checkout-abandon alert payload changed.");

            int unpaidBefore = CountAlerts(alerts, "무전취식");
            events.Publish(Observe(
                ShopInteractionTransition.MealUnpaid,
                first,
                actor,
                itemName: "수프"));
            events.Publish(Observe(
                ShopInteractionTransition.MealUnpaid,
                first,
                actor,
                itemName: "수프"));
            RequireAlertCount(alerts, "무전취식", unpaidBefore + 2,
                "Meal-unpaid alerts were incorrectly deduplicated.");
            EventAlertRequest unpaidAlert = alerts.Last(value => value.Title == "무전취식");
            Require(
                unpaidAlert.Detail == $"{ActorName}이(가) 수프 값을 치르지 못했습니다."
                && unpaidAlert.Importance == EventAlertImportance.Low
                && unpaidAlert.Category == "범죄",
                "The meal-unpaid alert payload changed.");

            IBuildingVisitorPort throwingActor = VisitorProbe.Create(
                "기록 실패 손님",
                out VisitorProbe throwingProbe);
            throwingProbe.ThrowOnRecordActivity = true;
            int abandonAlertsBeforeFailure = CountAlerts(alerts, "손님이 구매를 포기함");
            AggregateException aggregate = null;
            try
            {
                PublishAbandon(events, first, throwingActor, 10f);
            }
            catch (AggregateException failure)
            {
                aggregate = failure;
            }

            Require(
                aggregate != null
                && aggregate.InnerExceptions.Count == 1
                && aggregate.InnerExceptions[0] is InvalidOperationException,
                "A RecordActivity failure did not surface as an explicit aggregate.");
            Require(
                throwingProbe.MemoryCount == 1
                && CountAlerts(alerts, "손님이 구매를 포기함") ==
                    abandonAlertsBeforeFailure + 1,
                "A RecordActivity failure suppressed memory or alert projection.");

            int recordsBeforeDispose = actorProbe.RecordCount;
            int alertsBeforeDispose = alerts.Count;
            observer.Dispose();
            observer.Dispose();
            events.Publish(Observe(
                ShopInteractionTransition.MealUnpaid,
                first,
                actor,
                itemName: "수프"));
            Require(
                actorProbe.RecordCount == recordsBeforeDispose
                && alerts.Count == alertsBeforeDispose,
                "A disposed shop interaction observer remained subscribed.");

            observer.Initialize();
            observer.Initialize();
            int serviceAlertsBeforeReinit = CountAlerts(alerts, "계산 지원 필요");
            PublishServiceRequest(events, first, actor, 15f);
            Require(
                actorProbe.RecordCount == recordsBeforeDispose + 1
                && CountAlerts(alerts, "계산 지원 필요") ==
                    serviceAlertsBeforeReinit + 1,
                "Reinitialization duplicated subscription or retained disposed alert state.");
        }
        finally
        {
            observer.Dispose();
        }
    }

    private static void PublishServiceRequest(
        IGameEventBus events,
        Shop facility,
        IBuildingVisitorPort actor,
        float elapsedSeconds) =>
        events.Publish(Observe(
            ShopInteractionTransition.CheckoutServiceRequested,
            facility,
            actor,
            elapsedSeconds: elapsedSeconds,
            patienceMultiplier: 1.2f));

    private static void PublishAbandon(
        IGameEventBus events,
        Shop facility,
        IBuildingVisitorPort actor,
        float gameTime) =>
        events.Publish(Observe(
            ShopInteractionTransition.CheckoutAbandoned,
            facility,
            actor,
            elapsedSeconds: 30f,
            abandonSentiment: -0.5f,
            gameTime: gameTime));

    private static ShopInteractionObservedEvent Observe(
        ShopInteractionTransition transition,
        Shop facility,
        IBuildingVisitorPort actor,
        string reason = "",
        string detail = "",
        string itemName = "",
        float elapsedSeconds = 0f,
        float patienceMultiplier = 1f,
        float abandonSentiment = 0f,
        int revenue = 0,
        int waitingCount = 0,
        float gameTime = 0f) =>
        new(
            transition,
            facility,
            actor,
            reason,
            detail,
            itemName,
            elapsedSeconds,
            patienceMultiplier,
            abandonSentiment,
            revenue,
            waitingCount,
            gameTime,
            FacilityName);

    private static BuildingActivitySnapshot Activity(
        string kind,
        string outcome,
        string text,
        string action = "",
        string reason = "",
        float value = 0f,
        bool bubble = false) =>
        new(
            kind,
            outcome,
            text,
            action,
            reason,
            value,
            quantity: 0,
            sentiment: 0f,
            bubbleEligible: bubble,
            absoluteDay: 0);

    private static void RequireActivity(
        BuildingActivitySnapshot actual,
        BuildingActivitySnapshot expected,
        string name)
    {
        Require(
            actual.KindId == expected.KindId
            && actual.OutcomeId == expected.OutcomeId
            && actual.FactText == expected.FactText
            && actual.WorkTypeId.Length == 0
            && actual.ActionId == expected.ActionId
            && actual.ReasonCode == expected.ReasonCode
            && actual.Value == expected.Value
            && actual.Quantity == expected.Quantity
            && actual.Sentiment == expected.Sentiment
            && actual.BubbleEligible == expected.BubbleEligible
            && actual.AbsoluteDay == expected.AbsoluteDay,
            "Shop activity projection changed: " + name);
    }

    private static void RequireAlertCount(
        IReadOnlyList<EventAlertRequest> alerts,
        string title,
        int expected,
        string failure) =>
        Require(
            CountAlerts(alerts, title) == expected,
            failure + $" expected={expected}; actual={CountAlerts(alerts, title)}.");

    private static int CountAlerts(
        IEnumerable<EventAlertRequest> alerts,
        string title) =>
        alerts.Count(value => value.Title == title);

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }

    private readonly struct ProjectionCase
    {
        public ProjectionCase(
            string name,
            ShopInteractionObservedEvent observation,
            BuildingActivitySnapshot expected)
        {
            Name = name;
            Observation = observation;
            Expected = expected;
        }

        public string Name { get; }
        public ShopInteractionObservedEvent Observation { get; }
        public BuildingActivitySnapshot Expected { get; }
    }

    public class VisitorProbe : DispatchProxy
    {
        public VisitorProbe()
        {
        }

        internal string DisplayName { get; private set; } = string.Empty;
        internal int RecordCount { get; private set; }
        internal int MemoryCount { get; private set; }
        internal IBuildingWorldEntryPort LastMemoryFacility { get; private set; }
        internal float LastMemorySentiment { get; private set; }
        internal string LastMemoryDetail { get; private set; } = string.Empty;
        internal bool ThrowOnRecordActivity { get; set; }

        internal static IBuildingVisitorPort Create(
            string displayName,
            out VisitorProbe probe)
        {
            IBuildingVisitorPort value =
                DispatchProxy.Create<IBuildingVisitorPort, VisitorProbe>();
            probe = (VisitorProbe)(object)value;
            probe.DisplayName = displayName;
            return value;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            switch (targetMethod?.Name)
            {
                case "get_VisitorSnapshot":
                    return new BuildingVisitorSnapshot(
                        persistentId: "character:shop-interaction-observer",
                        displayName: DisplayName,
                        position: Vector3.zero,
                        isRuntimeActive: true,
                        isInternalStaff: false,
                        canMove: true,
                        stayDurationMultiplier: 1f,
                        revenueMultiplier: 1f,
                        personalityPatience: 1f,
                        modelPatience: 1f,
                        crimeRiskMultiplier: 1f,
                        productionOutputMultiplier: 1f,
                        stockProductionBonus: 0,
                        expeditionStress: 0f,
                        mood: 0f,
                        hunger: 0f,
                        fun: 0f,
                        sleep: 0f,
                        excretion: 0f,
                        hygiene: 0f);
                case nameof(IBuildingVisitorPort.RecordActivity):
                    RequirePortArguments(targetMethod, args, 2);
                    if (ThrowOnRecordActivity)
                    {
                        throw new InvalidOperationException(
                            "injected-shop-record-activity-failure");
                    }

                    if (args[0] is not IBuildingWorldEntryPort
                        || args[1] is not BuildingActivitySnapshot)
                    {
                        throw new InvalidOperationException(
                            "Shop activity projection supplied invalid arguments.");
                    }

                    RecordCount++;
                    return null;
                case nameof(IBuildingVisitorPort.RememberFacilityExperience):
                    RequirePortArguments(targetMethod, args, 3);
                    if (args[0] is not IBuildingWorldEntryPort facility
                        || args[1] is not float sentiment
                        || args[2] is not string detail)
                    {
                        throw new InvalidOperationException(
                            "Shop facility memory supplied invalid arguments.");
                    }

                    MemoryCount++;
                    LastMemoryFacility = facility;
                    LastMemorySentiment = sentiment;
                    LastMemoryDetail = detail;
                    return null;
                default:
                    throw new InvalidOperationException(
                        "Unexpected shop interaction visitor call: "
                        + (targetMethod?.Name ?? "<null>"));
            }
        }

        private static void RequirePortArguments(
            MethodInfo targetMethod,
            object[] args,
            int expected)
        {
            if (args == null || args.Length != expected)
            {
                throw new InvalidOperationException(
                    "Unexpected shop visitor argument count: "
                    + targetMethod.Name);
            }
        }
    }
}
#endif
