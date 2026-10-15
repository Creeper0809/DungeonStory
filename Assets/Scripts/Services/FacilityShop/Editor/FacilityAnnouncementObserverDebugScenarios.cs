#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FacilityAnnouncementObserverDebugScenarios
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    public static void RunOrThrow()
    {
        VerifyRefreshSnapshotIsDetached();
        VerifyPresentationAndLifecycle();
    }

    private static void VerifyRefreshSnapshotIsDetached()
    {
        FacilityShopOfferSnapshot original = DailyOffer();
        FacilityShopOfferSnapshot replacement = BasicOffer();
        FacilityShopOfferSnapshot[] daily = { original };
        FacilityShopOfferSnapshot[] basic = { replacement };
        var observed = new FacilityShopOffersRebuiltEvent(
            7,
            FacilityShopRefreshCause.Manual,
            daily,
            basic);

        daily[0] = replacement;
        basic[0] = original;

        Require(
            observed.Day == 7
            && observed.Cause == FacilityShopRefreshCause.Manual
            && observed.DailyOffers.Count == 1
            && !ReferenceEquals(observed.DailyOffers[0], original)
            && observed.DailyOffers[0].displayName == original.displayName
            && observed.DailyOffers[0].cost == original.cost
            && observed.BasicPurchaseOffers.Count == 1
            && !ReferenceEquals(
                observed.BasicPurchaseOffers[0],
                replacement)
            && observed.BasicPurchaseOffers[0].displayName
                == replacement.displayName
            && observed.BasicPurchaseOffers[0].cost == replacement.cost,
            "A facility-shop refresh event retained its mutable input lists.");
    }

    private static void VerifyPresentationAndLifecycle()
    {
        var oldEvents = new GameEventBus();
        var newEvents = new GameEventBus();
        var oldAlerts = new List<EventAlertRequest>();
        var newAlerts = new List<EventAlertRequest>();
        int oldRefreshEvents = 0;
        int oldEvolutionEvents = 0;
        int oldPurchaseEvents = 0;
        using IDisposable oldAlertSubscription =
            CaptureAlerts(oldEvents, oldAlerts);
        using IDisposable newAlertSubscription =
            CaptureAlerts(newEvents, newAlerts);
        using IDisposable refreshSubscription =
            oldEvents.Subscribe<FacilityShopOffersRebuiltEvent>(
                _ => oldRefreshEvents++);
        using IDisposable evolutionSubscription =
            oldEvents.Subscribe<FacilityEvolutionCompletedEvent>(
                _ => oldEvolutionEvents++);
        using IDisposable purchaseSubscription =
            oldEvents.Subscribe<FacilityShopPurchasedEvent>(
                _ => oldPurchaseEvents++);

        var observerObject = new GameObject(
            "Facility Announcement Observer Debug");
        var resultObject = new GameObject(
            "Facility Announcement Result Debug");
        BuildingSO resultDefinition = null;
        FacilityBlueprintSO blueprintDefinition = null;
        FacilityAnnouncementObserver observer = null;
        try
        {
            observer = observerObject.AddComponent<
                FacilityAnnouncementObserver>();
            Require(
                ReadToggle(observer, "raiseAlertOnRefresh")
                && ReadToggle(observer, "raiseAlertOnEvolution"),
                "Facility announcement display toggles no longer default true.");
            observer.Construct(oldEvents);
            InvokeLifecycle(observer, "OnEnable");
            observer.Construct(oldEvents);
            InvokeLifecycle(observer, "OnEnable");

            FacilityShopOffersRebuiltEvent daily = Refresh(
                FacilityShopRefreshCause.OperatingDay,
                new[] { DailyOffer() },
                new[] { BasicOffer() });
            oldEvents.Publish(daily);
            Require(
                oldAlerts.Count == 1,
                "Repeated construction or enable duplicated the shop refresh alert.");
            RequireAlert(
                oldAlerts[0],
                "시설 상점 갱신",
                "일일 상품:\n"
                + "- 시설: 별빛 작업장 / 2성 / 비용 120 / Rare\n"
                + "\n기본 구매:\n"
                + "- 시설: 기본 숙소 / 1성 / 비용 60 / 기본 구매",
                "상점");

            oldEvents.Publish(Refresh(
                FacilityShopRefreshCause.Initialization));
            oldEvents.Publish(Refresh(
                FacilityShopRefreshCause.RunSetup));
            oldEvents.Publish(Refresh(
                FacilityShopRefreshCause.Restore));
            Require(
                oldAlerts.Count == 1,
                "Initialization, run setup, or restore emitted a shop alert.");

            oldEvents.Publish(Refresh(FacilityShopRefreshCause.Manual));
            Require(
                oldAlerts.Count == 2,
                "A manual shop refresh did not emit one alert.");
            RequireAlert(
                oldAlerts[1],
                "시설 상점 갱신",
                "일일 상품:\n- 없음\n\n기본 구매:\n- 없음",
                "상점");

            SetToggle(observer, "raiseAlertOnRefresh", false);
            oldEvents.Publish(daily);
            Require(
                oldAlerts.Count == 2
                && oldRefreshEvents == 6,
                "The refresh toggle suppressed the source event or emitted display.");
            SetToggle(observer, "raiseAlertOnRefresh", true);

            resultDefinition = ScriptableObject.CreateInstance<BuildingSO>();
            resultDefinition.id = 10601;
            resultDefinition.objectName = "별빛 요새";
            BuildableObject resultBuilding =
                resultObject.AddComponent<BuildableObject>();
            SetBuildingData(resultBuilding, resultDefinition);
            FacilityEvolutionCompletedEvent evolution =
                new FacilityEvolutionCompletedEvent(
                    new FacilityEvolutionResult(
                        true,
                        null,
                        resultBuilding,
                        3,
                        "낡은 시설",
                        default,
                        "completed"));
            oldEvents.Publish(evolution);
            Require(
                oldAlerts.Count == 3,
                "A facility evolution did not emit one alert.");
            RequireAlert(
                oldAlerts[2],
                "시설 진화 완료",
                "낡은 시설 -> 별빛 요새 3성",
                "시설 진화");

            SetToggle(observer, "raiseAlertOnEvolution", false);
            oldEvents.Publish(evolution);
            Require(
                oldAlerts.Count == 3
                && oldEvolutionEvents == 2,
                "The evolution toggle suppressed the source event or emitted display.");

            blueprintDefinition =
                ScriptableObject.CreateInstance<FacilityBlueprintSO>();
            blueprintDefinition.id = 10602;
            blueprintDefinition.blueprintName = "심층 공학";
            var blueprintOffer = new FacilityBlueprintOffer(
                blueprintDefinition,
                90,
                FacilityShopRarity.Rare,
                true);
            FacilityShopPurchasedEvent blueprintPurchase = Purchase(
                true,
                blueprintOffer,
                "purchased");
            FacilityShopPurchasedEvent failedBlueprintPurchase = Purchase(
                false,
                blueprintOffer,
                "rejected");
            FacilityShopPurchasedEvent buildingPurchase = Purchase(
                true,
                new FacilityBuildingOffer(
                    resultDefinition,
                    40,
                    FacilityShopRarity.Common,
                    false,
                    true),
                "purchased");
            SetToggle(observer, "raiseAlertOnRefresh", false);
            oldEvents.Publish(blueprintPurchase);
            oldEvents.Publish(failedBlueprintPurchase);
            oldEvents.Publish(buildingPurchase);
            Require(
                oldAlerts.Count == 4
                && oldPurchaseEvents == 3,
                "Blueprint purchase projection was not success/type filtered or was suppressed by display toggles.");
            RequireAlert(
                oldAlerts[3],
                "설계도 획득",
                "심층 공학 획득",
                "설계도");
            SetToggle(observer, "raiseAlertOnRefresh", true);
            SetToggle(observer, "raiseAlertOnEvolution", true);

            InvokeLifecycle(observer, "OnDisable");
            oldEvents.Publish(daily);
            oldEvents.Publish(evolution);
            oldEvents.Publish(blueprintPurchase);
            Require(
                oldAlerts.Count == 4,
                "A disabled facility announcement observer remained subscribed.");

            observer.Construct(newEvents);
            InvokeLifecycle(observer, "OnDisable");
            oldEvents.Publish(daily);
            newEvents.Publish(daily);
            newEvents.Publish(blueprintPurchase);
            Require(
                oldAlerts.Count == 4
                && newAlerts.Count == 0,
                "A disabled reconstructed observer subscribed to an event bus.");

            InvokeLifecycle(observer, "OnEnable");
            oldEvents.Publish(daily);
            Require(
                oldAlerts.Count == 4,
                "A reconstructed observer remained connected to its old bus.");
            newEvents.Publish(daily);
            newEvents.Publish(blueprintPurchase);
            Require(
                newAlerts.Count == 2,
                "A reconstructed observer did not connect once to its new bus.");
            RequireAlert(
                newAlerts[1],
                "설계도 획득",
                "심층 공학 획득",
                "설계도");

            InvokeLifecycle(observer, "OnDestroy");
            newEvents.Publish(daily);
            newEvents.Publish(blueprintPurchase);
            Require(
                newAlerts.Count == 2,
                "A destroyed facility announcement observer remained subscribed.");
        }
        finally
        {
            if (observer != null)
            {
                InvokeLifecycle(observer, "OnDestroy");
            }
            if (resultDefinition != null)
            {
                Object.DestroyImmediate(resultDefinition);
            }
            if (blueprintDefinition != null)
            {
                Object.DestroyImmediate(blueprintDefinition);
            }
            Object.DestroyImmediate(resultObject);
            Object.DestroyImmediate(observerObject);
        }
    }

    private static FacilityShopOffersRebuiltEvent Refresh(
        FacilityShopRefreshCause cause,
        IReadOnlyList<FacilityShopOfferSnapshot> daily = null,
        IReadOnlyList<FacilityShopOfferSnapshot> basic = null) =>
        new FacilityShopOffersRebuiltEvent(
            7,
            cause,
            daily ?? Array.Empty<FacilityShopOfferSnapshot>(),
            basic ?? Array.Empty<FacilityShopOfferSnapshot>());

    private static FacilityShopPurchasedEvent Purchase(
        bool success,
        FacilityShopOffer offer,
        string message) =>
        new FacilityShopPurchasedEvent(
            new FacilityShopPurchaseResult(
                success,
                offer,
                offer?.Cost ?? 0,
                message));

    private static FacilityShopOfferSnapshot DailyOffer() =>
        new FacilityShopOfferSnapshot(
            FacilityShopOfferTypeIds.Building,
            "시설",
            FacilityShopRarity.Rare,
            "별빛 작업장",
            120,
            2,
            false);

    private static FacilityShopOfferSnapshot BasicOffer() =>
        new FacilityShopOfferSnapshot(
            FacilityShopOfferTypeIds.Building,
            "시설",
            FacilityShopRarity.Common,
            "기본 숙소",
            60,
            1,
            true);

    private static IDisposable CaptureAlerts(
        IGameEventBus events,
        ICollection<EventAlertRequest> alerts) =>
        events.Subscribe<EventAlertRequestedEvent>(value =>
        {
            if (value.request != null)
            {
                alerts.Add(value.request);
            }
        });

    private static void RequireAlert(
        EventAlertRequest request,
        string title,
        string detail,
        string category)
    {
        Require(
            request != null
            && request.Title == title
            && request.Detail == detail
            && request.Importance == EventAlertImportance.Medium
            && request.Category == category,
            "A facility announcement payload changed: " + title);
    }

    private static void SetBuildingData(
        BuildableObject building,
        BuildingSO definition)
    {
        PropertyInfo property = typeof(BuildableObject).GetProperty(
            nameof(BuildableObject.BuildingData),
            BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic);
        MethodInfo setter = property?.GetSetMethod(nonPublic: true);
        if (setter == null)
        {
            throw new MissingMemberException(
                typeof(BuildableObject).FullName,
                nameof(BuildableObject.BuildingData));
        }
        setter.Invoke(building, new object[] { definition });
    }

    private static void SetToggle(
        FacilityAnnouncementObserver observer,
        string fieldName,
        bool value)
    {
        RequireToggleField(fieldName).SetValue(observer, value);
    }

    private static bool ReadToggle(
        FacilityAnnouncementObserver observer,
        string fieldName) =>
        (bool)RequireToggleField(fieldName).GetValue(observer);

    private static FieldInfo RequireToggleField(string fieldName)
    {
        FieldInfo field = typeof(FacilityAnnouncementObserver).GetField(
            fieldName,
            PrivateInstance);
        return field != null && field.FieldType == typeof(bool)
            ? field
            : throw new MissingFieldException(
                typeof(FacilityAnnouncementObserver).FullName,
                fieldName);
    }

    private static void InvokeLifecycle(
        FacilityAnnouncementObserver observer,
        string methodName)
    {
        MethodInfo method = typeof(FacilityAnnouncementObserver).GetMethod(
            methodName,
            PrivateInstance);
        if (method == null)
        {
            throw new MissingMethodException(
                typeof(FacilityAnnouncementObserver).FullName,
                methodName);
        }
        method.Invoke(observer, null);
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
