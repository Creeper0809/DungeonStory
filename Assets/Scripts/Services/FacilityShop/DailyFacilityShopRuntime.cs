using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class DailyFacilityShopRuntime : MonoBehaviour, IFacilityShopPersistence
{
    [SerializeField] private bool raiseAlertOnRefresh = true;

    private readonly List<FacilityShopOffer> currentDailyOffers = new List<FacilityShopOffer>();
    private IReadOnlyList<FacilityShopOffer> currentDailyOffersView;
    private FacilityShopUnlockState unlockState = new FacilityShopUnlockState();
    private FacilityShopApplication stateApplication;
    private DungeonRuntimeAggregateRootStore aggregateRootStore;
    private int projectedRestoreRevision;
    private IFacilityShopCatalog facilityShopCatalog;
    private IRunVariableRuntimeReader runVariableReader;
    private IMetaProgressionRuntimeReader metaProgressionReader;
    private IGameEventBus gameEventBus;
    private IAutoProcurementRuntime autoProcurement;
    private IGameMoneyAccount money;
    private IBuildingCategoryDefinitionCatalog buildingCategoryCatalog;
    private IDungeonDebugRuleQuery debugRules;
    private IDisposable runStartVariablesSubscription;
    private IDisposable operatingDayEndedSubscription;

    public event Action<int, IReadOnlyList<FacilityShopOffer>, IReadOnlyList<FacilityShopOffer>> Refreshed;

    public IReadOnlyList<FacilityShopOffer> CurrentDailyOffers =>
        currentDailyOffersView ??= ReadOnlyView.List(currentDailyOffers);
    public IReadOnlyList<FacilityShopOffer> CurrentBasicPurchaseOffers =>
        FacilityShopService.CreateBasicPurchaseOffers(
            ResolveFacilityShopCatalog(),
            unlockState,
            ResolveMetaProgressionReader(),
            ResolveRunVariableReader(),
            buildingCategoryCatalog);
    public FacilityShopUnlockState UnlockState => unlockState;
    public int CurrentOfferDay => StateApplication.CurrentOfferDay;
    private FacilityShopApplication StateApplication =>
        stateApplication ??= new FacilityShopApplication(unlockState);

    [Inject]
    public void ConstructDailyFacilityShopRuntime(
        IFacilityShopCatalog facilityShopCatalog,
        IRunVariableRuntimeReader runVariableReader,
        IMetaProgressionRuntimeReader metaProgressionReader,
        IGameEventBus gameEventBus,
        IGameMoneyAccount money,
        IAutoProcurementRuntime autoProcurement,
        IBuildingCategoryDefinitionCatalog buildingCategoryCatalog,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IDungeonDebugRuleQuery debugRules)
    {
        this.facilityShopCatalog = facilityShopCatalog
            ?? throw new ArgumentNullException(nameof(facilityShopCatalog));
        this.runVariableReader = runVariableReader
            ?? throw new ArgumentNullException(nameof(runVariableReader));
        this.metaProgressionReader = metaProgressionReader
            ?? throw new ArgumentNullException(nameof(metaProgressionReader));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.autoProcurement = autoProcurement;
        this.buildingCategoryCatalog = buildingCategoryCatalog
            ?? throw new ArgumentNullException(nameof(buildingCategoryCatalog));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.debugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        unlockState = new FacilityShopUnlockState(this.aggregateRootStore);
        stateApplication = new FacilityShopApplication(unlockState);
        projectedRestoreRevision = this.aggregateRootStore.PublishedRestoreRevision;
        SubscribeToScopedEvents();
    }

    private void Start()
    {
        if (currentDailyOffers.Count == 0)
        {
            Refresh(1, false);
        }
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        Refresh(Mathf.Max(1, eventType.day + 1), raiseAlertOnRefresh);
    }

    public void OnTriggerEvent(RunStartVariablesSelectedEvent eventType)
    {
        if (CurrentOfferDay <= 1)
        {
            Refresh(1, false);
        }
    }

    public void Refresh(int day, bool raiseAlert)
    {
        StateApplication.RefreshForDay(day);
        RebuildOfferProjection(raiseAlert, processAutoProcurement: true);
    }

    private void RebuildOfferProjection(
        bool raiseAlert,
        bool processAutoProcurement)
    {
        int offerDay = CurrentOfferDay;
        currentDailyOffers.Clear();
        currentDailyOffers.AddRange(FacilityShopService.CreateDailyOffers(
            offerDay,
            ResolveFacilityShopCatalog(),
            ResolveRunVariableReader(),
            buildingCategoryCatalog));

        IReadOnlyList<FacilityShopOffer> basicPurchaseOffers = CurrentBasicPurchaseOffers;
        Refreshed?.Invoke(
            offerDay,
            EventPayloadSnapshot.Copy(currentDailyOffers),
            EventPayloadSnapshot.Copy(basicPurchaseOffers));
        if (processAutoProcurement)
        {
            autoProcurement?.ProcessShopRefresh(
                offerDay,
                CurrentDailyOffers,
                this);
        }

        if (raiseAlert)
        {
            gameEventBus.RaiseAlert(
                "시설 상점 갱신",
                FormatOfferList(currentDailyOffers, basicPurchaseOffers),
                EventAlertImportance.Medium,
                "상점");
        }
    }

    public FacilityShopStateSnapshot CaptureState() => StateApplication.Capture();

    FacilityShopRestoreCandidate IFacilityShopPersistence.BuildRestoreCandidate(
        FacilityShopStateSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }
        return StateApplication.PrepareRestore(snapshot);
    }

    void IFacilityShopPersistence.PublishRestoreCandidate(
        FacilityShopRestoreCandidate candidate)
    {
        StateApplication.PublishRestore(candidate
            ?? throw new ArgumentNullException(nameof(candidate)));
    }

    public bool TryPurchaseDailyOffer(int index, GameSessionState gameData, out FacilityShopPurchaseResult result)
    {
        return TryPurchaseDailyOffer(
            index,
            gameData,
            CreatePurchaseContext(
                currentDailyOffers,
                index,
                "daily-shop",
                "일일 상점 구매"),
            out result);
    }

    public bool TryPurchaseDailyOffer(
        int index,
        GameSessionState gameData,
        EconomyTransactionContext transactionContext,
        out FacilityShopPurchaseResult result)
    {
        if (index < 0 || index >= currentDailyOffers.Count)
        {
            result = new FacilityShopPurchaseResult(false, null, 0, "선택한 상품이 없습니다");
            PublishPurchase(result);
            return false;
        }

        return ExecutePurchase(
            gameData,
            currentDailyOffers[index],
            transactionContext,
            out result);
    }

    public bool TryPurchaseBasicOffer(int index, GameSessionState gameData, out FacilityShopPurchaseResult result)
    {
        IReadOnlyList<FacilityShopOffer> offers = CurrentBasicPurchaseOffers;
        return TryPurchaseBasicOffer(
            index,
            gameData,
            CreatePurchaseContext(
                offers,
                index,
                "basic-shop",
                "기본 시설 구매"),
            out result);
    }

    public bool TryPurchaseBasicOffer(
        int index,
        GameSessionState gameData,
        EconomyTransactionContext transactionContext,
        out FacilityShopPurchaseResult result)
    {
        IReadOnlyList<FacilityShopOffer> offers = CurrentBasicPurchaseOffers;
        if (index < 0 || index >= offers.Count)
        {
            result = new FacilityShopPurchaseResult(false, null, 0, "선택한 기본 구매 상품이 없습니다");
            PublishPurchase(result);
            return false;
        }

        return ExecutePurchase(
            gameData,
            offers[index],
            transactionContext,
            out result);
    }

    private bool ExecutePurchase(
        GameSessionState gameData,
        FacilityShopOffer offer,
        EconomyTransactionContext transactionContext,
        out FacilityShopPurchaseResult result)
    {
        return FacilityShopService.TryPurchaseOffer(
            money,
            offer,
            unlockState,
            transactionContext,
            debugRules,
            out result,
            PublishPurchase);
    }

    private static EconomyTransactionContext CreatePurchaseContext(
        IReadOnlyList<FacilityShopOffer> offers,
        int index,
        string sourceId,
        string description)
    {
        FacilityShopOffer offer =
            index >= 0 && index < (offers?.Count ?? 0)
                ? offers[index]
                : null;
        string targetId = offer == null
            ? string.Empty
            : $"{offer.OfferTypeId}:{offer.DataId}";
        return new EconomyTransactionContext(
            EconomyTransactionKind.ShopPurchase,
            sourceId,
            targetId,
            description);
    }

    private void PublishPurchase(FacilityShopPurchaseResult result)
    {
        gameEventBus.Publish(new FacilityShopPurchasedEvent(result));
        if (result.success
            && string.Equals(
                result.offerTypeId,
                FacilityShopOfferTypeIds.Blueprint,
                StringComparison.Ordinal))
        {
            gameEventBus.RaiseBlueprintAcquired(
                $"{result.offer?.displayName ?? "설계도"} 획득");
        }
    }

    private void Update()
    {
        int publishedRevision = aggregateRootStore?.PublishedRestoreRevision ?? 0;
        if (projectedRestoreRevision == publishedRevision)
        {
            return;
        }

        projectedRestoreRevision = publishedRevision;
        RebuildOfferProjection(
            raiseAlert: false,
            processAutoProcurement: false);
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        runStartVariablesSubscription?.Dispose();
        runStartVariablesSubscription = null;
        operatingDayEndedSubscription?.Dispose();
        operatingDayEndedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        runStartVariablesSubscription ??=
            gameEventBus.Subscribe<RunStartVariablesSelectedEvent>(OnTriggerEvent);
        operatingDayEndedSubscription ??=
            gameEventBus.Subscribe<OperatingDayEndedEvent>(OnTriggerEvent);
    }

    private static string FormatOfferList(
        IEnumerable<FacilityShopOffer> dailyOffers,
        IEnumerable<FacilityShopOffer> basicPurchaseOffers)
    {
        List<string> lines = new List<string> { "일일 상품:" };
        List<string> dailyRows = dailyOffers?
            .Where((offer) => offer != null && offer.IsValid)
            .Select((offer) => $"- {offer.ToSnapshot().ToSummaryText()}")
            .ToList()
            ?? new List<string>();
        if (dailyRows.Count > 0)
        {
            lines.AddRange(dailyRows);
        }
        else
        {
            lines.Add("- 없음");
        }

        List<string> basicRows = basicPurchaseOffers?
            .Where((offer) => offer != null && offer.IsValid)
            .Select((offer) => $"- {offer.ToSnapshot().ToSummaryText()}")
            .ToList()
            ?? new List<string>();
        lines.Add(string.Empty);
        lines.Add("기본 구매:");
        if (basicRows.Count > 0)
        {
            lines.AddRange(basicRows);
        }
        else
        {
            lines.Add("- 없음");
        }
        return string.Join("\n", lines);
    }

    private IFacilityShopCatalog ResolveFacilityShopCatalog()
    {
        return facilityShopCatalog
            ?? throw new InvalidOperationException($"{nameof(DailyFacilityShopRuntime)} requires {nameof(IFacilityShopCatalog)} injection.");
    }

    private IRunVariableRuntimeReader ResolveRunVariableReader()
    {
        return runVariableReader
            ?? throw new InvalidOperationException($"{nameof(DailyFacilityShopRuntime)} requires {nameof(IRunVariableRuntimeReader)} injection.");
    }

    private IMetaProgressionRuntimeReader ResolveMetaProgressionReader()
    {
        return metaProgressionReader
            ?? throw new InvalidOperationException($"{nameof(DailyFacilityShopRuntime)} requires {nameof(IMetaProgressionRuntimeReader)} injection.");
    }
}
