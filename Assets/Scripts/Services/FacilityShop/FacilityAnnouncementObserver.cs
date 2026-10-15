using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer;

public sealed class FacilityAnnouncementObserver : MonoBehaviour
{
    [SerializeField] private bool raiseAlertOnRefresh = true;
    [SerializeField] private bool raiseAlertOnEvolution = true;

    private IGameEventBus events;
    private IDisposable refreshSubscription;
    private IDisposable evolutionSubscription;
    private IDisposable purchaseSubscription;

    [Inject]
    public void Construct(IGameEventBus gameEventBus)
    {
        if (gameEventBus == null)
        {
            throw new ArgumentNullException(nameof(gameEventBus));
        }
        if (ReferenceEquals(events, gameEventBus))
        {
            Subscribe();
            return;
        }

        Unsubscribe();
        events = gameEventBus;
        Subscribe();
    }

    private void OnEnable() => Subscribe();

    private void OnDisable() => Unsubscribe();

    private void OnDestroy() => Unsubscribe();

    private void Subscribe()
    {
        if (!isActiveAndEnabled || events == null)
        {
            return;
        }

        refreshSubscription ??=
            events.Subscribe<FacilityShopOffersRebuiltEvent>(ObserveRefresh);
        evolutionSubscription ??=
            events.Subscribe<FacilityEvolutionCompletedEvent>(ObserveEvolution);
        purchaseSubscription ??=
            events.Subscribe<FacilityShopPurchasedEvent>(ObservePurchase);
    }

    private void Unsubscribe()
    {
        refreshSubscription?.Dispose();
        refreshSubscription = null;
        evolutionSubscription?.Dispose();
        evolutionSubscription = null;
        purchaseSubscription?.Dispose();
        purchaseSubscription = null;
    }

    private void ObserveRefresh(FacilityShopOffersRebuiltEvent value)
    {
        if (!raiseAlertOnRefresh
            || value.Cause is not (
                FacilityShopRefreshCause.OperatingDay
                or FacilityShopRefreshCause.Manual))
        {
            return;
        }

        events.RaiseAlert(
            "시설 상점 갱신",
            FormatOfferList(value.DailyOffers, value.BasicPurchaseOffers),
            EventAlertImportance.Medium,
            "상점");
    }

    private void ObserveEvolution(FacilityEvolutionCompletedEvent value)
    {
        if (!raiseAlertOnEvolution)
        {
            return;
        }

        FacilityEvolutionResult result = value.result;
        if (!result.Success || result.ResultBuilding == null)
        {
            return;
        }

        events.RaiseAlert(
            "시설 진화 완료",
            $"{result.SourceFacilityName} -> {FacilityShopService.GetBuildingName(result.ResultBuilding.BuildingData)} {result.ResultStarGrade}성",
            EventAlertImportance.Medium,
            "시설 진화");
    }

    private void ObservePurchase(FacilityShopPurchasedEvent value)
    {
        FacilityShopPurchaseResult result = value.result;
        if (!result.success
            || !string.Equals(
                result.offerTypeId,
                FacilityShopOfferTypeIds.Blueprint,
                StringComparison.Ordinal))
        {
            return;
        }

        events.RaiseBlueprintAcquired(
            $"{result.offer?.displayName ?? "설계도"} 획득");
    }

    private static string FormatOfferList(
        IReadOnlyList<FacilityShopOfferSnapshot> dailyOffers,
        IReadOnlyList<FacilityShopOfferSnapshot> basicPurchaseOffers)
    {
        List<string> lines = new() { "일일 상품:" };
        AppendOffers(lines, dailyOffers);
        lines.Add(string.Empty);
        lines.Add("기본 구매:");
        AppendOffers(lines, basicPurchaseOffers);
        return string.Join("\n", lines);
    }

    private static void AppendOffers(
        ICollection<string> lines,
        IReadOnlyList<FacilityShopOfferSnapshot> offers)
    {
        if (offers == null || offers.Count == 0)
        {
            lines.Add("- 없음");
            return;
        }

        for (int index = 0; index < offers.Count; index++)
        {
            FacilityShopOfferSnapshot offer = offers[index];
            if (offer != null)
            {
                lines.Add("- " + offer.ToSummaryText());
            }
        }
    }
}
