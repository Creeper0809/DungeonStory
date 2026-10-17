using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using VContainer.Unity;

/// <summary>
/// Projects applied shop transitions into the legacy activity, experience, and alert feeds.
/// Shop execution and customer state remain owned by the producer.
/// </summary>
public sealed class ShopInteractionObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;
    private ConditionalWeakTable<Shop, ShopAlertState> alertStates = new();

    public ShopInteractionObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize() => subscription ??=
        events.Subscribe<ShopInteractionObservedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
        alertStates = new ConditionalWeakTable<Shop, ShopAlertState>();
    }

    public static BuildingActivitySnapshot Project(
        in ShopInteractionObservedEvent value) =>
        value.Transition switch
        {
            ShopInteractionTransition.AdmissionRejected =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Failed,
                    $"{value.FacilityName} 이용 실패: {value.Reason}",
                    reasonCode: value.Reason,
                    bubbleEligible: true),
            ShopInteractionTransition.SessionRejected =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Failed,
                    $"{value.FacilityName} 이용 실패",
                    reasonCode: value.Reason,
                    bubbleEligible: true),
            ShopInteractionTransition.SelfCheckoutStarted =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Progress,
                    $"{value.FacilityName} 셀프 계산 중",
                    actionId: "checkout:self-service"),
            ShopInteractionTransition.StaffUseCompleted =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Completed,
                    $"{value.FacilityName} 직원 이용: 매출 제외",
                    reasonCode: "staff-no-revenue"),
            ShopInteractionTransition.AttendantWaitingStarted =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Blocked,
                    $"{value.FacilityName}에서 계산할 직원을 기다리기 시작했다.",
                    actionId: "checkout:staffed",
                    reasonCode: "no-serving-worker",
                    bubbleEligible: true),
            ShopInteractionTransition.StaffCheckoutStarted =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Started,
                    $"{value.FacilityName}에서 계산을 시작했다.",
                    actionId: "checkout:staffed"),
            ShopInteractionTransition.RetailFailed =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Failed,
                    $"{value.FacilityName} 이용 실패: {value.Detail}",
                    reasonCode: value.Reason,
                    bubbleEligible: true),
            ShopInteractionTransition.MealSupplyChanged =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Cancelled,
                    "Meal supply changed before commit; retrying selection.",
                    reasonCode: "meal-supply-retry"),
            ShopInteractionTransition.MealNoLongerNeeded =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Cancelled,
                    "Meal service retired because the need was already satisfied.",
                    reasonCode: "meal-no-longer-needed"),
            ShopInteractionTransition.MealFailed =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Failed,
                    string.IsNullOrWhiteSpace(value.Detail)
                        ? value.Reason
                        : $"{value.Reason}:{value.Detail}",
                    reasonCode: value.Reason,
                    bubbleEligible: true),
            ShopInteractionTransition.MealUnpaid =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Social,
                    BuildingActivityOutcomes.Damaged,
                    $"{value.ItemName} 값을 치르지 못하고 쫓겨남",
                    reasonCode: "meal-unaffordable",
                    bubbleEligible: true),
            ShopInteractionTransition.MealCompleted =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Completed,
                    $"{value.ItemName} 식사 완료",
                    value: value.Revenue),
            ShopInteractionTransition.CheckoutRestless =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Progress,
                    $"{GetActorName(value)}은 계산 줄이 좀처럼 줄지 않아 초조해졌다.",
                    actionId: "checkout:waiting",
                    reasonCode: "queue-restless",
                    value: value.ElapsedSeconds,
                    bubbleEligible: true),
            ShopInteractionTransition.CheckoutServiceRequested =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Responded,
                    value.PatienceMultiplier >= 1.1f
                        ? $"{GetActorName(value)}은 계산을 도와줄 직원을 불렀다."
                        : $"{GetActorName(value)}은 더 기다리기 어렵다며 직원을 찾았다.",
                    actionId: "checkout:service-request",
                    reasonCode: "customer-called-worker",
                    value: value.ElapsedSeconds,
                    bubbleEligible: true),
            ShopInteractionTransition.CheckoutAbandoned =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Cancelled,
                    $"{GetActorName(value)}은 계산을 기다리다 구매를 포기했다.",
                    actionId: "checkout:abandoned",
                    reasonCode: "patience-exhausted",
                    value: value.ElapsedSeconds,
                    bubbleEligible: true),
            ShopInteractionTransition.InteractionAborted =>
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Cancelled,
                    $"{value.FacilityName} interaction aborted: {value.Detail}",
                    actionId: "shop:interaction",
                    reasonCode: value.Detail),
            ShopInteractionTransition.CheckoutQueueSampled =>
                throw new InvalidOperationException(
                    "Checkout queue samples do not project an activity."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value.Transition,
                "Unknown shop interaction transition.")
        };

    private void Observe(ShopInteractionObservedEvent value)
    {
        if (value.Transition == ShopInteractionTransition.CheckoutQueueSampled)
        {
            if (value.WaitingCount == 0)
            {
                alertStates.GetOrCreateValue(value.Facility)
                    .CheckoutServiceAlertRaised = false;
            }

            return;
        }

        List<Exception> failures = null;
        if (value.Transition == ShopInteractionTransition.CheckoutAbandoned)
        {
            TryObserve(
                () => value.Actor?.RememberFacilityExperience(
                    value.Facility,
                    value.AbandonSentiment,
                    "계산이 너무 늦어 구매를 포기했다."),
                ref failures);
        }

        TryObserve(
            () => value.Actor?.RecordActivity(value.Facility, Project(value)),
            ref failures);
        TryObserve(() => RaiseAlert(value), ref failures);

        if (failures != null)
        {
            throw new AggregateException(
                "One or more shop interaction projections failed.",
                failures);
        }
    }

    private void RaiseAlert(in ShopInteractionObservedEvent value)
    {
        switch (value.Transition)
        {
            case ShopInteractionTransition.MealUnpaid:
                events.RaiseAlert(
                    "무전취식",
                    $"{GetActorName(value)}이(가) {value.ItemName} 값을 치르지 못했습니다.",
                    EventAlertImportance.Low,
                    "범죄");
                break;
            case ShopInteractionTransition.CheckoutServiceRequested:
            {
                ShopAlertState state = alertStates.GetOrCreateValue(value.Facility);
                if (state.CheckoutServiceAlertRaised)
                {
                    break;
                }

                state.CheckoutServiceAlertRaised = true;
                events.RaiseAlert(
                    "계산 지원 필요",
                    $"{GetActorName(value)}이(가) {value.FacilityName}에서 계산 직원을 찾고 있습니다.",
                    EventAlertImportance.Low,
                    "손님");
                break;
            }
            case ShopInteractionTransition.CheckoutAbandoned:
            {
                ShopAlertState state = alertStates.GetOrCreateValue(value.Facility);
                if (value.GameTime < state.NextCheckoutAbandonAlertTime)
                {
                    break;
                }

                state.NextCheckoutAbandonAlertTime = value.GameTime + 5f;
                events.RaiseAlert(
                    "손님이 구매를 포기함",
                    $"{GetActorName(value)}이(가) {value.FacilityName}의 긴 계산 대기 때문에 다른 곳을 찾습니다.",
                    EventAlertImportance.Medium,
                    "손님");
                break;
            }
        }
    }

    private static string GetActorName(in ShopInteractionObservedEvent value) =>
        value.Actor?.VisitorSnapshot.DisplayName ?? "손님";

    private static void TryObserve(Action action, ref List<Exception> failures)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            failures ??= new List<Exception>();
            failures.Add(exception);
        }
    }

    private sealed class ShopAlertState
    {
        public bool CheckoutServiceAlertRaised;
        public float NextCheckoutAbandonAlertTime;
    }
}
