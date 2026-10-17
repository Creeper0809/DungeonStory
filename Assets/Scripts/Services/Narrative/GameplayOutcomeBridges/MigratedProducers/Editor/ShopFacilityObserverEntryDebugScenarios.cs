#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class MigratedProducerEconomyEntryExtendedE2EDebugScenarios
{
    public static void RunShopFacilityObserversForBatch()
    {
        ShopInteractionObserverDebugScenarios.RunOrThrow();
        Debug.Log("B106_SHOP_PROJECTION_LIFETIME=PASS;activities=16");
        FacilityAnnouncementObserverDebugScenarios.RunOrThrow();
        Debug.Log("B106_FACILITY_PROJECTION_LIFETIME=PASS");
        RetailScenario.VerifyInteractionObservationEntry();
        VerifyFacilityRefreshObservationEntry();
        VerifyEvolutionCompletionObservationBoundary();
        Debug.Log("B106_SHOP_FACILITY_OBSERVERS=PASS;natural-play=NOT-RUN");
    }

    private sealed partial class RetailScenario
    {
        internal static void VerifyInteractionObservationEntry()
        {
            const MigratedProducerOutcomeKind Kind = MigratedProducerOutcomeKind.FacilityCrimeEvent;
            using (var source = new RetailScenario(Kind, crime: true))
            {
                using var observer = new ShopInteractionObserver(source.shopEvents);
                observer.Initialize();
                var observations = new List<ShopInteractionObservedEvent>();
                using var capture = source.shopEvents.Subscribe<ShopInteractionObservedEvent>(observations.Add);
                source.RunLiveCheckout(waitForAttendant: true);
                source.RequireLiveCheckoutExecutionClosed();
                Require(source.LastVisitOutcome == ShoppingVisitOutcome.Completed
                    && source.ActiveShopUsers == 0 && source.StockCount == source.InitialStockCount - 1
                    && source.HoldingMoney == source.InitialMoney && source.Probe.CommitCount(Kind) == 1,
                    "Observed checkout changed its physical effect, ledger, or terminal cleanup.");
                Require(observations.Count(value => value.Transition == ShopInteractionTransition.AttendantWaitingStarted) == 1
                    && observations.Count(value => value.Transition == ShopInteractionTransition.StaffCheckoutStarted) == 1
                    && source.customer.LogComponent.ActivityEntries.Count(value => value.ActionId == "checkout:staffed"
                        && value.OutcomeId == CharacterActivityOutcomes.Started) == 1,
                    "The actual staffed checkout did not reach its single activity observer.");
                string saved = JsonConvert.SerializeObject(source.shop.CaptureInteractionState());
                JObject owners = source.CaptureOwnerCheckpoint();
                int observationCount = observations.Count;
                source.shop.RestoreInteractionState(JsonConvert.DeserializeObject<ShopInteractionState>(saved));
                Require(JsonConvert.SerializeObject(source.shop.CaptureInteractionState()) == saved
                    && JToken.DeepEquals(owners, source.CaptureOwnerCheckpoint()) && observations.Count == observationCount,
                    "Restoring the completed checkout repeated an effect or observation.");
            }
            Debug.Log("B106_SHOP_ACTUAL_CHECKOUT_RESTORE=PASS;path=Shop.Interact;fixture=controlled-clock-attendant-arrival");

            using (var source = new RetailScenario(Kind, crime: true))
            {
                var transitions = new List<ShopInteractionObservedEvent>();
                using var capture = source.shopEvents.Subscribe<ShopInteractionObservedEvent>(transitions.Add);
                using var observer = new ShopInteractionObserver(source.shopEvents);
                observer.Initialize();
                int alerts = 0;
                using var display = source.shopEvents.Subscribe<EventAlertRequestedEvent>(_ =>
                {
                    alerts++;
                    throw new InvalidOperationException("qa-b106-shop-display-failed");
                });
                var service = new ShopCustomerInteractionService(source.shop, () => source.shop.transform.position);
                var profile = BuildingCheckoutPatienceRules.Create(1f, 1f, 1);
                source.shopping.BeginVisitInteraction(source.shop);
                JObject owners = source.CaptureOwnerCheckpoint();
                InvokeB106(service, "HandleCheckoutWaitStage", source.customer.BuildingVisitor,
                    profile, BuildingCheckoutWaitStage.Abandoned, profile.AbandonSeconds);
                Require(source.LastVisitOutcome == ShoppingVisitOutcome.Abandoned && alerts == 1
                    && transitions.Count == 1 && transitions[0].Transition == ShopInteractionTransition.CheckoutAbandoned
                    && source.customer.SocialMemory.GetFacilitySentiment(source.shop) < 0f
                    && source.customer.SocialMemory.RecentRumors.Count(value => value.summary == "계산이 너무 늦어 구매를 포기했다.") == 1
                    && source.customer.LogComponent.ActivityEntries.Count(value => value.ActionId == "checkout:abandoned") == 1
                    && JToken.DeepEquals(owners, source.CaptureOwnerCheckpoint()),
                    "A failed display blocked the actual abandonment, memory, activity, or changed stock/money.");
                string memory = JsonConvert.SerializeObject(source.customer.SocialMemory.CaptureSnapshot());
                float sentiment = source.customer.SocialMemory.GetFacilitySentiment(source.shop);
                source.customer.SocialMemory.RestoreSnapshot(JsonConvert.DeserializeObject<CharacterSocialMemorySnapshot>(memory));
                Require(source.customer.SocialMemory.GetFacilitySentiment(source.shop) == sentiment
                    && source.customer.SocialMemory.RecentRumors.Count(value => value.summary == "계산이 너무 늦어 구매를 포기했다.") == 1
                    && transitions.Count == 1 && alerts == 1,
                    "Current memory restore lost or repeated the observed experience.");
            }
            Debug.Log("B106_SHOP_ABANDON_OWNER_BOUNDARY=PASS;wait-loop=NOT-RUN;memory-roundtrip=PASS");
        }
    }

    private static void VerifyFacilityRefreshObservationEntry()
    {
        using var source = new FacilityShopScenario(MigratedProducerOutcomeKind.FacilityShopPurchaseResult);
        DailyFacilityShopRuntime runtime = source.ShopOwner;
        var events = (IGameEventBus)typeof(DailyFacilityShopRuntime).GetField(
            "gameEventBus", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
        var root = (DungeonRuntimeAggregateRootStore)typeof(DailyFacilityShopRuntime).GetField(
            "aggregateRootStore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
        var host = new GameObject("B106 Facility Announcements");
        try
        {
            var observer = host.AddComponent<FacilityAnnouncementObserver>();
            var facts = new List<FacilityShopOffersRebuiltEvent>();
            using var capture = events.Subscribe<FacilityShopOffersRebuiltEvent>(facts.Add);
            observer.Construct(events);
            int alerts = 0;
            using var display = events.Subscribe<EventAlertRequestedEvent>(_ =>
            {
                alerts++;
                throw new InvalidOperationException("qa-b106-facility-display-failed");
            });
            int money = source.Money;
            int purchaseSequence = source.PurchaseSequence;
            events.Publish(new OperatingDayEndedEvent(5));
            Require(runtime.CurrentOfferDay == 6 && facts.Count == 1 && facts[0].Day == 6
                && facts[0].Cause == FacilityShopRefreshCause.OperatingDay && alerts == 1
                && facts[0].DailyOffers.Count == runtime.CurrentDailyOffers.Count,
                "The actual operating-day refresh did not publish its applied offers after display failure.");
            string saved = JsonConvert.SerializeObject(runtime.CaptureState());
            RequireThrows<ArgumentOutOfRangeException>(() => runtime.Refresh(9, (FacilityShopRefreshCause)99));
            Require(JsonConvert.SerializeObject(runtime.CaptureState()) == saved,
                "An invalid refresh cause changed the owner before failing.");
            var persistence = (IFacilityShopPersistence)runtime;
            var candidate = persistence.BuildRestoreCandidate(JsonConvert.DeserializeObject<FacilityShopStateSnapshot>(saved));
            InvokeB106(root, "BeginRestoreStaging");
            try
            {
                persistence.PublishRestoreCandidate(candidate);
                InvokeB106(root, "PublishRestoreStaging");
            }
            finally
            {
                if (root.IsRestoreStaging) InvokeB106(root, "DiscardRestoreStaging");
            }
            InvokeB106(runtime, "Update");
            InvokeB106(runtime, "Update");
            Require(facts.Count == 2 && facts[1].Cause == FacilityShopRefreshCause.Restore
                && alerts == 1 && JsonConvert.SerializeObject(runtime.CaptureState()) == saved
                && source.Money == money && source.PurchaseSequence == purchaseSequence,
                "The current restore repeated an alert/purchase or failed to rebuild offers exactly once.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
        Debug.Log("B106_FACILITY_REFRESH_ACTUAL_OWNER_RESTORE=PASS");
    }

    // Only the changed post-commit publication boundary is invoked here. This
    // does not claim an entire facility replacement or evolution Play session.
    private static void VerifyEvolutionCompletionObservationBoundary()
    {
        var host = new GameObject("B106 Evolution Completion Boundary");
        try
        {
            var runtime = host.AddComponent<FacilityEvolutionRuntime>();
            var events = new GameEventBus();
            typeof(FacilityEvolutionRuntime).GetField("gameEventBus",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runtime, events);
            int callbacks = 0, notifications = 0;
            runtime.Completed += _ =>
            {
                callbacks++;
                throw new InvalidOperationException("qa-b106-completion-callback-failed");
            };
            using var capture = events.Subscribe<FacilityEvolutionCompletedEvent>(_ =>
            {
                notifications++;
                throw new InvalidOperationException("qa-b106-completion-observer-failed");
            });
            var result = new FacilityEvolutionResult(false, null, null, 0, "fixture", default, "rejected");
            InvokeB106(runtime, "PublishCompletion", result);
            Require(callbacks == 1 && notifications == 1,
                "Completion callback failure suppressed the independent domain event attempt.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
        Debug.Log("B106_EVOLUTION_PUBLICATION_BOUNDARY=PASS;whole-evolution=NOT-RUN");
    }

    private static void InvokeB106(object target, string name, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, name);
        method.Invoke(target, arguments);
    }
}
#endif
