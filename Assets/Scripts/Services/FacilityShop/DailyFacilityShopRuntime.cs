using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;

public class DailyFacilityShopRuntime : MonoBehaviour, IFacilityShopPersistence
{
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
    private FacilityShopPurchaseJournal purchaseFacts;
    private IGameSessionStateStore gameSessionState;
    private TreasuryEconomyAggregateStateStore treasuryState;
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

    [Inject]
    public void ConstructPurchaseFacts(
        FacilityShopPurchaseJournal purchaseFacts,
        IGameSessionStateStore gameSessionState,
        TreasuryEconomyAggregateStateStore treasuryState)
    {
        this.purchaseFacts = purchaseFacts
            ?? throw new ArgumentNullException(nameof(purchaseFacts));
        this.gameSessionState = gameSessionState
            ?? throw new ArgumentNullException(nameof(gameSessionState));
        this.treasuryState = treasuryState
            ?? throw new ArgumentNullException(nameof(treasuryState));
    }

    private void Start()
    {
        if (currentDailyOffers.Count == 0)
        {
            Refresh(1, FacilityShopRefreshCause.Initialization);
        }
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        Refresh(
            Mathf.Max(1, eventType.day + 1),
            FacilityShopRefreshCause.OperatingDay);
    }

    public void OnTriggerEvent(RunStartVariablesSelectedEvent eventType)
    {
        if (CurrentOfferDay <= 1)
        {
            Refresh(1, FacilityShopRefreshCause.RunSetup);
        }
    }

    public void Refresh(
        int day,
        FacilityShopRefreshCause cause = FacilityShopRefreshCause.Manual)
    {
        if (!Enum.IsDefined(typeof(FacilityShopRefreshCause), cause))
            throw new ArgumentOutOfRangeException(nameof(cause));
        StateApplication.RefreshForDay(day);
        RebuildOfferProjection(cause, processAutoProcurement: true);
    }

    private void RebuildOfferProjection(
        FacilityShopRefreshCause cause,
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

        IGameEventBus events = gameEventBus
            ?? throw new InvalidOperationException(
                $"{nameof(DailyFacilityShopRuntime)} requires {nameof(IGameEventBus)} injection.");
        var observation = new FacilityShopOffersRebuiltEvent(
                offerDay,
                cause,
                CaptureOfferSnapshots(currentDailyOffers),
                CaptureOfferSnapshots(basicPurchaseOffers));
        try
        {
            events.Publish(observation);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogWarning(
                "facility-shop-refresh-observer:"
                + exception.GetType().Name + ":" + exception.Message);
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
            int balance = money?.Balance ?? -1;
            result = new FacilityShopPurchaseResult(
                false,
                null,
                0,
                "선택한 상품이 없습니다",
                failureStage: FacilityShopPurchaseFailureStage.OfferSelection,
                outcomePolicy: FacilityShopPurchaseOutcomePolicy
                    .RejectedWithoutPublication,
                balanceBefore: balance,
                balanceAfter: balance);
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
            int balance = money?.Balance ?? -1;
            result = new FacilityShopPurchaseResult(
                false,
                null,
                0,
                "선택한 기본 구매 상품이 없습니다",
                failureStage: FacilityShopPurchaseFailureStage.OfferSelection,
                outcomePolicy: FacilityShopPurchaseOutcomePolicy
                    .RejectedWithoutPublication,
                balanceBefore: balance,
                balanceAfter: balance);
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
        int initialBalance = money?.Balance ?? -1;
        if (offer == null || !offer.IsValid)
        {
            result = RejectPurchase(offer, initialBalance,
                FacilityShopPurchaseFailureStage.OfferValidation, "상품 정보가 올바르지 않습니다");
            PublishPurchase(result);
            return false;
        }
        if (gameData?.day == null || purchaseFacts == null
            || gameSessionState == null || treasuryState == null)
        {
            result = RejectPurchase(offer, initialBalance,
                FacilityShopPurchaseFailureStage.RuntimeUnavailable, "시설 상점 구매 사실 전달이 준비되지 않았습니다.");
            PublishPurchase(result);
            return false;
        }
        int absoluteDay = Math.Max(0, gameData.day.Value);
        int previousSequence = unlockState.PurchaseSequence;
        if (previousSequence == int.MaxValue)
        {
            result = RejectPurchase(offer, initialBalance,
                FacilityShopPurchaseFailureStage.PurchaseSequenceExhausted, "시설 상점 구매 번호가 소진되었습니다.");
            return false;
        }
        string identity = CreatePurchaseIdentity(absoluteDay, transactionContext, offer, previousSequence + 1);
        EconomyTransactionContext paymentContext = new(
            transactionContext.kind, identity, transactionContext.targetId, transactionContext.description);
        var proposed = new FacilityShopPurchaseFactData
        {
            operationId = identity, sequence = previousSequence + 1, absoluteDay = absoluteDay,
            shopSource = transactionContext.sourceId ?? string.Empty,
            offerTypeId = offer.OfferTypeId, offerDataId = offer.DataId, offerName = offer.DisplayName,
            itemDefinitionId = offer switch
            {
                FacilityBuildingOffer building => FacilityInstallationKitItemIds.ForBuilding(building.Building),
                FacilityBlueprintOffer blueprint => blueprint.Blueprint.PhysicalItemId,
                _ => string.Empty
            },
            quotedCost = offer.Cost, balanceBefore = initialBalance, balanceAfter = initialBalance,
            paymentSourceId = identity
        };
        FacilityShopStateSnapshot shopBefore = unlockState.Capture();
        GameSessionSnapshot sessionBefore = gameData.Capture();
        TreasuryEconomyAggregateState treasuryBefore = treasuryState.Current.Copy();
        if (!purchaseFacts.TryBegin(new FacilityShopPurchaseFact(proposed, true),
                out FacilityShopPurchaseJournal.Preparation prepared, out string reserveFailure))
        {
            result = RejectPurchase(offer, initialBalance, FacilityShopPurchaseFailureStage.OutcomeReservation,
                "시설 상점 구매 사실을 예약하지 못했습니다: " + reserveFailure);
            PublishPurchase(result);
            return false;
        }
        try
        {
            if (!FacilityShopService.TryPurchaseOffer(money, offer, unlockState, paymentContext, debugRules, out result))
                return false;
            unlockState.CommitPurchaseSequence(previousSequence);
            int balanceAfter = money.Balance;
            int paid = checked(initialBalance - balanceAfter);
            EconomyTransactionRecord payment = FindPurchasePayment(
                paymentContext, paid, initialBalance, balanceAfter, offer.Cost);
            var actual = proposed.Clone();
            actual.balanceAfter = balanceAfter;
            actual.paidCost = paid;
            actual.paymentTransactionId = payment?.transactionId ?? string.Empty;
            DomainFactDelivery delivered = prepared.PublishApplied(new FacilityShopPurchaseFact(actual));
            if (!delivered.MayBeCommitted)
            {
                RestorePurchaseState(shopBefore, sessionBefore, treasuryBefore);
                result = RejectPurchase(offer, initialBalance, FacilityShopPurchaseFailureStage.OutcomeCommit,
                    "시설 상점 구매 사실 전달 실패: " + delivered.Detail);
                return false;
            }
            // Unknown acknowledgement retains the applied purchase and its frozen pending fact.
            // Recovery republishes the fact; it never repeats payment or unlock.
            result = new FacilityShopPurchaseResult(true, offer, paid, result.message,
                prepared.PublicationKey, actual.paymentTransactionId,
                outcomePolicy: FacilityShopPurchaseOutcomePolicy.DomainApplied,
                balanceBefore: initialBalance, balanceAfter: balanceAfter);
        }
        catch
        {
            if (!prepared.MayBeApplied)
                RestorePurchaseState(shopBefore, sessionBefore, treasuryBefore);
            throw;
        }
        finally { prepared.Dispose(); }
        if (!purchaseFacts.RecoverPending(out string pendingFailure))
            Debug.LogWarning(pendingFailure);
        PublishPurchasePostCommit(result);
        return true;
    }

    private static FacilityShopPurchaseResult RejectPurchase(FacilityShopOffer offer, int balance,
        FacilityShopPurchaseFailureStage stage, string message) =>
        new(false, offer, offer?.Cost ?? 0, message,
            failureStage: stage, outcomePolicy: FacilityShopPurchaseOutcomePolicy.RejectedWithoutPublication,
            balanceBefore: balance, balanceAfter: balance);

    private void RestorePurchaseState(
        FacilityShopStateSnapshot shopBefore,
        GameSessionSnapshot sessionBefore,
        TreasuryEconomyAggregateState treasuryBefore)
    {
        unlockState.Restore(
            shopBefore ?? throw new ArgumentNullException(nameof(shopBefore)));
        gameSessionState.Restore(sessionBefore);
        treasuryState.Replace(
            treasuryBefore
            ?? throw new ArgumentNullException(nameof(treasuryBefore)));
    }

    private static string CreatePurchaseIdentity(
        int absoluteDay,
        EconomyTransactionContext transactionContext,
        FacilityShopOffer offer,
        int purchaseSequence) =>
        "facility-shop-purchase:day:" + absoluteDay
        + ":sequence:" + purchaseSequence
        + ":source:" + (transactionContext.sourceId?.Trim() ?? string.Empty)
        + ":target:" + (transactionContext.targetId?.Trim() ?? string.Empty)
        + ":offer:" + offer.OfferTypeId + ":" + offer.DataId;

    private EconomyTransactionRecord FindPurchasePayment(
        EconomyTransactionContext context,
        int paid,
        int balanceBefore,
        int balanceAfter,
        int quotedCost)
    {
        if (paid < 0 || paid != balanceBefore - balanceAfter)
            throw new InvalidOperationException(
                "Facility-shop payment balance moved in the wrong direction.");
        if (paid == 0)
        {
            if (balanceBefore != balanceAfter
                || quotedCost > 0 && !debugRules.ShouldSkipCosts())
                throw new InvalidOperationException(
                    "Facility-shop purchase has no authoritative payment.");
            return null;
        }
        EconomyTransactionRecord[] matches = treasuryState.Current.Ledger.Records
            .Where(row => row != null
                && row.succeeded
                && row.kind == context.kind
                && string.Equals(row.sourceId, context.sourceId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || matches[0].amount != -paid
            || matches[0].balanceBefore != balanceBefore
            || matches[0].balanceAfter != balanceAfter
            || !string.Equals(matches[0].targetId, context.targetId,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(matches[0].transactionId))
            throw new InvalidOperationException(
                "Facility-shop payment transaction does not match its purchase.");
        return matches[0].Clone();
    }

    private void PublishPurchasePostCommit(FacilityShopPurchaseResult result)
    {
        try
        {
            PublishPurchase(result);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(
                "facility-shop-purchase-post-commit-observer:"
                + exception.GetType().Name);
        }
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
            FacilityShopRefreshCause.Restore,
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

    private static IReadOnlyList<FacilityShopOfferSnapshot> CaptureOfferSnapshots(
        IReadOnlyList<FacilityShopOffer> offers)
    {
        if (offers == null || offers.Count == 0)
        {
            return Array.Empty<FacilityShopOfferSnapshot>();
        }

        List<FacilityShopOfferSnapshot> snapshots = new(offers.Count);
        for (int index = 0; index < offers.Count; index++)
        {
            FacilityShopOffer offer = offers[index];
            if (offer != null && offer.IsValid)
            {
                snapshots.Add(offer.ToSnapshot());
            }
        }
        return snapshots.Count == 0
            ? Array.Empty<FacilityShopOfferSnapshot>()
            : Array.AsReadOnly(snapshots.ToArray());
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
