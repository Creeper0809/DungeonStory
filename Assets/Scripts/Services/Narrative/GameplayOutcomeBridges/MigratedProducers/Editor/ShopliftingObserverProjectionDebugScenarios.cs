#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

public static class ShopliftingObserverProjectionDebugScenarios
{
    public static void RunForBatch()
    {
        ShopliftingFact fact = CreateFact();
        VerifyMapping(fact);
        VerifyObserverLifetime(fact);
    }

    private static void VerifyMapping(ShopliftingFact fact)
    {
        ShopliftingFactData data = fact.Snapshot();
        MigratedDomainFactCapture capture =
            ShopliftingFactMapping.Capture(fact);
        MigratedProducerOutcomeDetails details = capture.Details;
        string ownerIdentity = "facility-crime:shoplifting:"
            + data.unitOperationId
            + ":facility=" + data.facilityId
            + ":actor=" + data.actorId;
        string summary = "시설 절도 확정: operation=shoplifting-commit:"
            + data.unitOperationId
            + "; actor=" + data.actorId
            + "; item=" + data.lot.itemDefinitionId
            + "; loss=" + data.lossValue
            + "; day=" + data.absoluteDay;

        Require(
            capture.Kind == MigratedProducerOutcomeKind.FacilityCrimeEvent
            && capture.OwnerIdentity == ownerIdentity
            && capture.AbsoluteDay == data.absoluteDay
            && capture.Status == GameplayOutcomeStatus.Succeeded
            && !capture.ReservationOnly,
            "Shoplifting mapping changed its native result identity.");
        Require(
            details.IsValid
            && details.Subjects.Length == 1
            && details.Subjects[0].EntityId.Kind.Equals(
                MigratedProducerOutcomeIds.FacilityKind)
            && details.Subjects[0].EntityId.Value == data.facilityId
            && details.Subjects[0].DisplayName.DisplayText
                == data.facilityName
            && details.Subjects[0].Role.Equals(
                MigratedProducerOutcomeIds.FacilityRole)
            && details.Metrics.Length == 0
            && details.Facts.Length == 0
            && details.Summary == summary
            && !details.SummaryFirst,
            "Shoplifting mapping changed its frozen native payload.");
    }

    private static void VerifyObserverLifetime(ShopliftingFact fact)
    {
        var events = new GameEventBus();
        int stockCount = 0;
        int crimeCount = 0;
        int alertCount = 0;
        FacilityStockConsumedEvent observedStock = default;
        FacilityCrimeEvent observedCrime = default;
        EventAlertRequestedEvent observedAlert = default;
        using IDisposable stockSubscription =
            events.Subscribe<FacilityStockConsumedEvent>(value =>
            {
                stockCount++;
                observedStock = value;
            });
        using IDisposable crimeSubscription =
            events.Subscribe<FacilityCrimeEvent>(value =>
            {
                crimeCount++;
                observedCrime = value;
            });
        using IDisposable alertSubscription =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                alertCount++;
                observedAlert = value;
            });

        var observer = new ShopliftingActivityObserver(events);
        GameObject host = null;
        try
        {
            host = new GameObject("Shoplifting Observer Projection");
            Shop shop = host.AddComponent<Shop>();
            var notification = new ShopliftingCompletedEvent(
                shop,
                null,
                fact);

            observer.Initialize();
            observer.Initialize();
            events.Publish(notification);
            Require(stockCount == 1 && crimeCount == 1 && alertCount == 1,
                "Idempotent shoplifting initialization duplicated projection.");
            VerifyProjection(fact, shop, observedStock, observedCrime, observedAlert);

            observer.Dispose();
            events.Publish(notification);
            Require(stockCount == 1 && crimeCount == 1 && alertCount == 1,
                "Disposed shoplifting observer still projected notifications.");

            observer.Initialize();
            events.Publish(notification);
            Require(stockCount == 2 && crimeCount == 2 && alertCount == 2,
                "Reinitialized shoplifting observer did not project exactly once.");
            VerifyProjection(fact, shop, observedStock, observedCrime, observedAlert);
        }
        finally
        {
            observer.Dispose();
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void VerifyProjection(
        ShopliftingFact fact,
        Shop shop,
        FacilityStockConsumedEvent stock,
        FacilityCrimeEvent crime,
        EventAlertRequestedEvent alert)
    {
        ShopliftingFactData data = fact.Snapshot();
        string detail = data.actorName + " stole " + data.itemName
            + " from " + data.facilityName
            + " (loss " + data.lossValue
            + ", chance " + data.chance.ToString("0.##") + ").";
        Require(
            stock.consumerActor == null
            && ReferenceEquals(stock.facility, shop)
            && stock.category == (StockCategory)data.stockCategory
            && stock.amount == 1,
            "Shoplifting stock-consumed projection changed.");
        Require(
            crime.actor == null
            && ReferenceEquals(crime.facility, shop)
            && crime.kind == FacilityCrimeKind.Shoplifting
            && crime.detail == detail
            && crime.lossValue == data.lossValue
            && crime.commitOperationId
                == "shoplifting-commit:" + data.unitOperationId
            && crime.committedLot != null
            && crime.committedLot.itemDefinitionId
                == data.lot.itemDefinitionId,
            "Shoplifting crime projection changed.");
        Require(
            alert.request != null
            && alert.request.Title == "도난 발생"
            && alert.request.Detail == detail
            && alert.request.Importance == EventAlertImportance.Medium
            && alert.request.Category == "범죄",
            "Shoplifting alert projection changed.");
    }

    private static ShopliftingFact CreateFact()
    {
        var components = new List<RetailStockComponentSnapshot>();
        const string itemDefinitionId = "item:shoplifting-projection";
        const string sourceOperationId = "stock:shoplifting-projection";
        const int sourceQuantityBefore = 3;
        var lot = new RetailStockLotSnapshot
        {
            saleItemId = 17,
            itemDefinitionId = itemDefinitionId,
            itemInstanceId = string.Empty,
            sourceStackId = "stack:shoplifting-projection",
            quantity = 1,
            unitMassGrams = 250,
            sourceOperationId = sourceOperationId,
            components = components,
            componentFingerprint = ItemStackSignature.Create(
                itemDefinitionId,
                Array.Empty<ItemInstanceComponentSaveData>())
        };
        return new ShopliftingFact(new ShopliftingFactData
        {
            unitOperationId = "retail-unit:" + sourceOperationId
                + ":ordinal:" + sourceQuantityBefore.ToString("D10"),
            facilityId = "building:shoplifting-projection",
            facilityName = "투영 상점",
            actorId = "character:shoplifting-projection",
            actorName = "Projection Visitor",
            itemName = "Projection Item",
            absoluteDay = 12,
            lossValue = 37,
            stockCategory = (int)StockCategory.General,
            sourceQuantityBefore = sourceQuantityBefore,
            chance = 0.25f,
            lot = lot
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
