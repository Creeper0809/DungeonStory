using System;
using DungeonStory.Operation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class CustomerCheckoutPatiencePlayModeVerifier
{
    public static string LastReport { get; private set; } = "NOT_RUN";

    public static string Start()
    {
        if (!Application.isPlaying)
        {
            LastReport = "FAIL: PlayMode required.";
            return LastReport;
        }

        if (UnityEngine.Object.FindFirstObjectByType<CustomerCheckoutPatiencePlayModeRunner>() != null)
        {
            return "RUNNING";
        }

        LastReport = "RUNNING";
        new GameObject("Customer Checkout Patience PlayMode Runner")
            .AddComponent<CustomerCheckoutPatiencePlayModeRunner>();
        return LastReport;
    }

    internal static void Complete(string report)
    {
        LastReport = report;
    }
}

public sealed class CustomerCheckoutPatiencePlayModeRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        float originalTimeScale = Time.timeScale;
        CustomerAiDebugScenarios.CustomerAiScenarioWorld world = null;
        CheckoutAlertListener alertListener = null;
        string report;

        try
        {
            Time.timeScale = 8f;
            world = new CustomerAiDebugScenarios.CustomerAiScenarioWorld();
            alertListener =
                new CheckoutAlertListener(CharacterAiEditorTestDependencies.GameEvents);
            Shop shop = world.Place(
                "P1_GeneralStore",
                new Vector2Int(4, 0),
                requireStaffedService: true) as Shop;
            CharacterActor customer = world.CreateCustomer(
                "Slime",
                Vector2Int.zero,
                90f,
                90f,
                10f,
                70f);
            AbilityShopping shopping = customer.GetAbility<AbilityShopping>();
            customer.Identity.Data.aiPersonality.patience = 0.25f;
            yield return null;
            shopping.BeginVisitInteraction(shop);
            float moodBefore = customer.Mood.Value;

            Type sessionType = typeof(Shop).GetNestedType(
                "CheckoutWaitSession",
                BindingFlags.NonPublic);
            MethodInfo waitMethod = typeof(Shop).GetMethod(
                "WaitForServingWorkerWithPatience",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object session = sessionType != null ? Activator.CreateInstance(sessionType) : null;
            IEnumerator waitRoutine = session != null && waitMethod != null
                ? waitMethod.Invoke(shop, new[] { customer, session }) as IEnumerator
                : null;

            if (waitRoutine != null)
            {
                yield return waitRoutine;
            }

            bool abandoned = sessionType != null
                && session != null
                && (bool)(sessionType.GetProperty("Abandoned")?.GetValue(session) ?? false);
            bool serviceAlert = alertListener.Requests.Any(request => request != null
                && request.Title == "계산 지원 필요");
            bool abandonAlert = alertListener.Requests.Any(request => request != null
                && request.Title == "손님이 구매를 포기함");
            bool visiblePhase = customer.Brain != null
                && customer.Brain.CurrentActionPhase == "구매 포기"
                && customer.Brain.CurrentActionPhaseDetail.Contains("다른 곳");
            bool passed = waitRoutine != null
                && abandoned
                && shopping.LastVisitOutcome == ShoppingVisitOutcome.Abandoned
                && shop.WaitingCheckoutCount == 0
                && customer.Mood.Value < moodBefore
                && customer.SocialMemory.GetFacilitySentiment(shop) < 0f
                && serviceAlert
                && abandonAlert
                && visiblePhase;
            report = passed
                ? $"PASS: outcome={shopping.LastVisitOutcome}; waiting={shop.WaitingCheckoutCount}; mood={moodBefore:0.#}->{customer.Mood.Value:0.#}; sentiment={customer.SocialMemory.GetFacilitySentiment(shop):0.###}; alerts={alertListener.Requests.Count}; phase={customer.Brain.CurrentActionPhase}"
                : $"FAIL: routine={waitRoutine != null}; abandoned={abandoned}; outcome={shopping.LastVisitOutcome}; waiting={shop.WaitingCheckoutCount}; mood={moodBefore:0.#}->{customer.Mood.Value:0.#}; sentiment={customer.SocialMemory.GetFacilitySentiment(shop):0.###}; serviceAlert={serviceAlert}; abandonAlert={abandonAlert}; phase={customer.Brain?.CurrentActionPhase}/{customer.Brain?.CurrentActionPhaseDetail}";
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            alertListener?.Dispose();
            world?.Dispose();
        }

        CustomerCheckoutPatiencePlayModeVerifier.Complete(report);
        if (report.StartsWith("PASS", StringComparison.Ordinal))
        {
            Debug.Log($"[CustomerCheckoutPatiencePlayMode] {report}");
        }
        else
        {
            Debug.LogError($"[CustomerCheckoutPatiencePlayMode] {report}");
        }

        Destroy(gameObject);
    }

    private sealed class CheckoutAlertListener : IDisposable
    {
        private readonly List<EventAlertRequest> requests = new List<EventAlertRequest>();
        private readonly IDisposable subscription;

        public IReadOnlyList<EventAlertRequest> Requests => requests;

        public CheckoutAlertListener(DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription =
                gameEventBus.Subscribe<EventAlertRequestedEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(EventAlertRequestedEvent eventType)
        {
            if (eventType.request != null)
            {
                requests.Add(eventType.request);
            }
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }
}
