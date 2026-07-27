using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class Shop : BuildableObject, IRetailFacility, IRestockableFacility, IRetailStockStateOwner, IWorkableFacility
{
    private const float CheckoutWaitPollSeconds = 0.2f;
    private const float SelfServiceCheckoutSeconds = 0.8f;
    private const float StaffedCheckoutSeconds = 0.35f;
    private const float WaitingCheckoutOperateUrgency = 160f;
    private const float WaitingCheckoutOperateUrgencyPerCustomer = 40f;
    private static readonly IFloatingNumberFeedbackService FallbackFloatingNumberFeedbackService =
        new ShopNoopFloatingNumberFeedbackService();
    private static readonly WaitForSeconds PurchaseCompleteDelay =
        new WaitForSeconds(0.5f);
    private static readonly WaitForSeconds CheckoutPollDelay =
        new WaitForSeconds(CheckoutWaitPollSeconds);
    private static readonly WaitForSeconds SelfServiceCheckoutDelay =
        new WaitForSeconds(SelfServiceCheckoutSeconds);
    private static readonly WaitForSeconds StaffedCheckoutDelay =
        new WaitForSeconds(StaffedCheckoutSeconds);
    private static readonly Stack<ShoppingSelectionBuffer> ShoppingSelectionPool =
        new Stack<ShoppingSelectionBuffer>();

    private List<RemainStock> stocks = new List<RemainStock>();
    private readonly List<Stock> purchasableStockBuffer = new List<Stock>();
    private CharacterActor worker;
    private int waitingCheckoutCount;
    private bool checkoutServiceAlertRaised;
    private float nextCheckoutAbandonAlertTime;
    private StockInfo baseStock;
    private GameData gameData;
    private IGameDataProvider gameDataProvider;
    private IShopStockCatalog stockCatalog;
    private IFloatingNumberFeedbackService floatingNumberFeedbackService;
    private IWorkforceReplanService workforceReplanService;
    private IFacilityCrimeRiskEvaluator crimeRiskEvaluator;
    private IRoomEnvironmentExperienceService roomEnvironmentExperienceService;
    private IRandomStream shopRandom;
    private bool stockInitialized;
    public int CurrentStock => GetStockCount();
    public bool HasAvailableStock => CurrentStock > 0;
    public int WaitingCheckoutCount => waitingCheckoutCount;
    public bool HasWaitingCheckout => waitingCheckoutCount > 0;
    public bool HasServingWorker
    {
        get
        {
            PruneInvalidWorker();
            return worker != null;
        }
    }

    public StockCategory ActiveStockCategory => ResolveActiveStockCategory();
    public int MaxInternalStock => GetMaxInternalStock(ActiveStockCategory);
    public int MaxStock => MaxInternalStock;
    public int MissingStock => Mathf.Max(0, MaxInternalStock - CurrentStock);
    public bool NeedsRestock => MissingStock > 0;
    public bool RequiresStaffedCheckout => RequiresServingWorker();
    public bool UsesSelfService => !RequiresStaffedCheckout;
    public float CurrentPriceMultiplier => GetPriceMultiplier();
    public IReadOnlyList<RetailProductSnapshot> ProductSnapshots
    {
        get
        {
            EnsureStockInitialized();
            return stocks
                .Where((stock) => stock != null && IsSaleItemAllowed(stock.id))
                .Select((stock) =>
                {
                    Stock priced = CreatePricedStock(stock);
                    return new RetailProductSnapshot(stock.id, stock.itemName, priced.cost, stock.stock);
                })
                .ToArray();
        }
    }

    public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)
    {
        base.Initialization(buildingSO, buildPos);
        baseStock = null;
        stockInitialized = false;
        TryResolveGameData(requireProvider: false);
        TryInitializeStock(requireCatalog: false);
        RegisterStateModule(new ShopStockStateModule(this));
    }

    [Inject]
    public void ConstructShop(
        IGameDataProvider gameDataProvider,
        IShopStockCatalog stockCatalog,
        IFloatingNumberFeedbackService floatingNumberFeedbackService,
        IWorkforceReplanService workforceReplanService,
        IFacilityCrimeRiskEvaluator crimeRiskEvaluator,
        IRandomStreamProvider randomStreamProvider,
        IRoomEnvironmentExperienceService roomEnvironmentExperienceService = null)
    {
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.stockCatalog = stockCatalog
            ?? throw new ArgumentNullException(nameof(stockCatalog));
        this.floatingNumberFeedbackService = floatingNumberFeedbackService
            ?? throw new ArgumentNullException(nameof(floatingNumberFeedbackService));
        this.workforceReplanService = workforceReplanService
            ?? throw new ArgumentNullException(nameof(workforceReplanService));
        this.crimeRiskEvaluator = crimeRiskEvaluator
            ?? throw new ArgumentNullException(nameof(crimeRiskEvaluator));
        this.roomEnvironmentExperienceService = roomEnvironmentExperienceService;
        shopRandom = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("shop-runtime");

        TryResolveGameData(requireProvider: true);
        TryInitializeStock(requireCatalog: false);
    }

    public IEnumerator Interact(CharacterActor actor)
    {
        EnsureStockInitialized();
        if (!TryBeginUse(actor, out string failureReason))
        {
            actor?.GetAbility<AbilityShopping>()?.SetVisitOutcome(this, ShoppingVisitOutcome.Failed);
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Shopping,
                CharacterActivityOutcomes.Failed,
                $"{objectNameOrDefault()} 이용 실패: {failureReason}",
                this,
                reasonCode: failureReason,
                bubbleEligible: true));
            yield break;
        }

        AbilityShopping shopable = actor != null ? actor.GetAbility<AbilityShopping>() : null;
        AbilityMove moveable = actor != null ? actor.GetAbility<AbilityMove>() : null;
        if (shopable == null || moveable == null)
        {
            shopable?.SetVisitOutcome(this, ShoppingVisitOutcome.Failed);
            EndUse(actor);
            yield break;
        }

        AIAction currentAction = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;
        int howmany = shopable.GetShoppingCount();
        if (IsInternalStaffUse(actor)
            && Facility != null
            && Facility.SupportsRole(FacilityRole.Meal))
        {
            howmany = 1;
        }
        ShoppingSelectionBuffer selection = AcquireShoppingSelectionBuffer();
        Dictionary<int, int> selectedCounts = selection.SelectedCounts;
        List<RemainStock> cart = selection.Cart;
        bool createsRevenue = CreatesRevenueFor(actor);
        for (int i = 0; i < howmany; i++)
        {
            Stock buyItem = shopable.DetermineBuyingItem(
                GetPurchasableStock(selectedCounts));
            actor?.Brain?.SetActionPhase("\uC0C1\uD488 \uB458\uB7EC\uBCF4\uAE30", this, $"{i + 1}/{howmany}");
            yield return moveable.Move2PosBySpeed(
                GetFacilityAnchorWorldPosition(FacilityAnchorPurposeIds.Use, actor.transform.position),
                0.7f,
                currentAction);
            yield return Linger(actor, 0.1f, currentAction);
            if (buyItem.id == -1) continue;

            RemainStock remainStock = stocks.FirstOrDefault((stock) => stock.id == buyItem.id);
            if (remainStock == null || GetRemainingStockAfterSelection(remainStock, selectedCounts) <= 0) continue;

            cart.Add(remainStock);
            selectedCounts.TryGetValue(remainStock.id, out int selectedCount);
            selectedCounts[remainStock.id] = selectedCount + 1;
        }

        if (cart.Count == 0)
        {
            ReleaseShoppingSelectionBuffer(selection);
            shopable.SetVisitOutcome(this, ShoppingVisitOutcome.Failed);
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Shopping,
                CharacterActivityOutcomes.Failed,
                $"{objectNameOrDefault()} 이용 실패: 구매 가능한 상품 없음",
                this,
                reasonCode: "no-purchasable-item",
                bubbleEligible: true));
            EndUse(actor);
            yield break;
        }

        Vector2 endPos = GetFacilityAnchorWorldPosition(FacilityAnchorPurposeIds.Checkout, actor.transform.position);
        actor?.Brain?.SetActionPhase("\uACC4\uC0B0\uB300 \uC774\uB3D9", this);
        yield return moveable.Move2PosBySpeed(endPos, 1f, currentAction);
        CheckoutWaitSession checkoutWaitSession = new CheckoutWaitSession();
        yield return WaitForServingWorkerWithPatience(actor, checkoutWaitSession);
        if (checkoutWaitSession.Abandoned
            || shopable.LastVisitOutcome == ShoppingVisitOutcome.Abandoned)
        {
            ReleaseShoppingSelectionBuffer(selection);
            EndUse(actor);
            yield break;
        }
        if (!CanServeCustomer(actor, out string serviceFailureReason))
        {
            ReleaseShoppingSelectionBuffer(selection);
            shopable.SetVisitOutcome(this, ShoppingVisitOutcome.Failed);
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Shopping,
                CharacterActivityOutcomes.Cancelled,
                $"{objectNameOrDefault()} 계산 대기 중단: {serviceFailureReason}",
                this,
                reasonCode: serviceFailureReason,
                bubbleEligible: true));
            EndUse(actor);
            yield break;
        }

        yield return RunCheckoutService(actor);
        if (TryResolveCheckoutCrime(actor, cart))
        {
            ReleaseShoppingSelectionBuffer(selection);
            shopable.SetVisitOutcome(this, ShoppingVisitOutcome.Completed);
            EndUse(actor);
            yield break;
        }

        int usedMoney = 0;
        int purchaseCount = 0;
        foreach (RemainStock remainStock in cart)
        {
            if (remainStock == null || remainStock.stock <= 0)
            {
                continue;
            }

            Stock pricedStock = CreatePricedStock(remainStock);
            if (!shopable.CanPay(pricedStock))
            {
                continue;
            }

            yield return shopable.BuyItem(remainStock, pricedStock.cost);
            purchaseCount++;
            PublishGameEvent(new FacilityStockConsumedEvent(
                actor,
                this,
                GetStockCategory(remainStock.id),
                1));
            if (createsRevenue)
            {
                usedMoney += pricedStock.cost;
            }
            remainStock.stock--;
            MarkFacilityDynamicStateDirty();
        }

        if (purchaseCount == 0)
        {
            ReleaseShoppingSelectionBuffer(selection);
            shopable.SetVisitOutcome(this, ShoppingVisitOutcome.Failed);
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Shopping,
                CharacterActivityOutcomes.Failed,
                $"{objectNameOrDefault()} 이용 실패: 구매 가능한 상품 없음",
                this,
                reasonCode: "no-purchasable-item",
                bubbleEligible: true));
            EndUse(actor);
            yield break;
        }

        actor?.ApplyMoodFactor(
            $"shopping:{GetInstanceID()}",
            "마음에 드는 물건을 삼",
            5f,
            120f,
            2);

        if (createsRevenue && usedMoney > 0)
        {
            usedMoney = Mathf.Max(1, Mathf.RoundToInt(
                usedMoney * CharacterSkillRuntimeEffects.GetRevenueMultiplier(worker)));
            RequireFloatingNumberFeedbackService().TryShow(NumberCondition.ONEARNMONEY, endPos + Vector2.up, usedMoney);
        }

        TryResolveGameData(requireProvider: true);

        if (createsRevenue && usedMoney > 0 && gameData != null)
        {
            gameData.holdingMoney.Value += usedMoney;
        }

        if (createsRevenue && usedMoney > 0)
        {
            PublishGameEvent(new FacilityRevenueEvent(actor, this, usedMoney));
        }

        if (!createsRevenue)
        {
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Shopping,
                CharacterActivityOutcomes.Completed,
                $"{objectNameOrDefault()} 직원 이용: 매출 제외",
                this,
                reasonCode: "staff-no-revenue"));
        }

        roomEnvironmentExperienceService?.Apply(new RoomEnvironmentExperienceEvent(
            actor,
            this,
            RoomExperienceActivity.Shopping));
        shopable.SetVisitOutcome(this, ShoppingVisitOutcome.Completed);
        ReleaseShoppingSelectionBuffer(selection);
        yield return PurchaseCompleteDelay;
        EndUse(actor);
    }

    public static bool CreatesRevenueFor(CharacterActor actor)
    {
        return actor == null || !IsInternalStaffUse(actor);
    }

    public static bool IsInternalStaffUse(CharacterActor actor)
    {
        return CharacterWorkRoleUtility.TryGetWork(actor, out _);
    }

    public bool CanServeCustomer(CharacterActor actor, out string failureReason)
    {
        failureReason = string.Empty;
        if (!CreatesRevenueFor(actor))
        {
            return true;
        }

        if (!RequiresServingWorker())
        {
            return true;
        }

        if (HasServingWorker)
        {
            return true;
        }

        failureReason = "직원 없음";
        return false;
    }

    public float GetCheckoutCrimeChance(int cartItemCount)
    {
        return GetCheckoutCrimeChance(null, cartItemCount, 0);
    }

    public float GetCheckoutCrimeChance(CharacterActor actor, int cartItemCount, int cartValue)
    {
        return RequireCrimeRiskEvaluator().CalculateShopliftingChance(new FacilityCrimeRiskContext(
            this,
            actor,
            HasServingWorker,
            HasWaitingCheckout,
            CurrentUserCount,
            cartItemCount,
            cartValue,
            CurrentStock,
            IsDamaged));
    }

    private bool TryResolveCheckoutCrime(CharacterActor actor, IReadOnlyList<RemainStock> cart)
    {
        if (!CreatesRevenueFor(actor))
        {
            return false;
        }

        int cartItemCount = cart != null ? cart.Count : 0;
        int cartValue = GetCartValue(cart);
        float chance = GetCheckoutCrimeChance(actor, cartItemCount, cartValue);
        if (!RequireCrimeRiskEvaluator().ShouldTriggerCrime(
                chance,
                RequireShopRandom().NextFloat()))
        {
            return false;
        }

        RemainStock stolenStock = cart?.FirstOrDefault((stock) => stock != null && stock.stock > 0);
        if (stolenStock == null)
        {
            return false;
        }

        stolenStock.stock--;
        MarkFacilityDynamicStateDirty();
        StockCategory category = GetStockCategory(stolenStock.id);
        PublishGameEvent(new FacilityStockConsumedEvent(actor, this, category, 1));
        AddCustomerCarriedStock(actor, category, $"theft:{stolenStock.id}:{GameFrameCount}");

        int lossValue = Mathf.Max(0, stolenStock.cost);
        string detail = BuildCrimeDetail(actor, stolenStock, lossValue, chance);
        PublishGameEvent(new FacilityCrimeEvent(
            actor,
            this,
            FacilityCrimeKind.Shoplifting,
            detail,
            lossValue));
        GameEventBus.RaiseAlert("도난 발생", detail, EventAlertImportance.Medium, "범죄");
        actor?.AddActivity(CharacterActivityEvent.Facility(
            CharacterActivityKinds.Social,
            CharacterActivityOutcomes.Damaged,
            detail,
            this,
            actionId: "crime:shoplifting",
            reasonCode: "shoplifting",
            value: lossValue,
            quantity: 1,
            bubbleEligible: true));
        return true;
    }

    private static void AddCustomerCarriedStock(CharacterActor actor, StockCategory category, string sourceId)
    {
        if (actor == null)
        {
            return;
        }

        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        IWorldItemStackRuntime itemRuntime = actor.WorldItemStackRuntime;
        if (inventory == null || itemRuntime == null)
        {
            return;
        }

        inventory.TryAdd(
            sourceId,
            DungeonItemCatalogSO.StockItemId(category),
            1,
            itemRuntime.CatalogProvider,
            itemRuntime.HaulingSettingsProvider,
            out _);
    }

    private IFacilityCrimeRiskEvaluator RequireCrimeRiskEvaluator()
    {
        return crimeRiskEvaluator
            ?? throw new InvalidOperationException("Shop requires IFacilityCrimeRiskEvaluator injection before use.");
    }

    private IRandomStream RequireShopRandom()
    {
        return shopRandom
            ?? throw new InvalidOperationException(
                $"{nameof(Shop)} requires "
                + $"{nameof(IRandomStreamProvider)} injection.");
    }

    private string BuildCrimeDetail(CharacterActor actor, RemainStock stolenStock, int lossValue, float chance)
    {
        string actorName = actor != null ? actor.name : "Unknown customer";
        string itemName = stolenStock != null && !string.IsNullOrWhiteSpace(stolenStock.itemName)
            ? stolenStock.itemName
            : "item";
        return $"{actorName} stole {itemName} from {objectNameOrDefault()} (loss {lossValue}, chance {chance:0.##}).";
    }

    private static int GetCartValue(IReadOnlyList<RemainStock> cart)
    {
        int total = 0;
        if (cart == null)
        {
            return total;
        }

        foreach (RemainStock stock in cart)
        {
            if (stock != null)
            {
                total += Mathf.Max(0, stock.cost);
            }
        }

        return total;
    }

    private IEnumerator RunCheckoutService(CharacterActor actor)
    {
        if (!CreatesRevenueFor(actor))
        {
            yield break;
        }

        bool selfService = !HasServingWorker;
        if (selfService)
        {
            waitingCheckoutCount++;
            MarkFacilityDynamicStateDirty();
            RequireWorkforceReplanService().RequestIdleWorkersToReplan();
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Shopping,
                CharacterActivityOutcomes.Progress,
                $"{objectNameOrDefault()} 셀프 계산 중",
                this,
                actionId: "checkout:self-service"));
        }

        try
        {
            yield return selfService
                ? SelfServiceCheckoutDelay
                : StaffedCheckoutDelay;
        }
        finally
        {
            if (selfService)
            {
                waitingCheckoutCount = Mathf.Max(0, waitingCheckoutCount - 1);
                MarkFacilityDynamicStateDirty();
            }
        }
    }

    private IEnumerator WaitForServingWorkerWithPatience(
        CharacterActor actor,
        CheckoutWaitSession session)
    {
        AbilityShopping shopping = actor != null
            ? actor.GetAbility<AbilityShopping>()
            : null;
        if (!ShouldWaitForServingWorker(actor))
        {
            yield break;
        }

        waitingCheckoutCount++;
        int queuePosition = waitingCheckoutCount;
        CustomerCheckoutPatienceProfile patience = CustomerCheckoutPatienceRules.Create(actor, queuePosition);
        CustomerCheckoutWaitStage stage = CustomerCheckoutWaitStage.Waiting;
        float elapsedSeconds = 0f;
        MarkFacilityDynamicStateDirty();
        RequireWorkforceReplanService().RequestIdleWorkersToReplan();
        actor?.Brain?.SetActionPhase(
            "계산 대기",
            this,
            CustomerCheckoutPatienceRules.GetQueueDetail(patience, elapsedSeconds, stage));
        actor?.AddActivity(CharacterActivityEvent.Facility(
            CharacterActivityKinds.Shopping,
            CharacterActivityOutcomes.Blocked,
            $"{objectNameOrDefault()}에서 계산할 직원을 기다리기 시작했다.",
            this,
            actionId: "checkout:staffed",
            reasonCode: "no-serving-worker",
            bubbleEligible: true));

        try
        {
            while (ShouldWaitForServingWorker(actor))
            {
                CustomerCheckoutWaitStage nextStage = CustomerCheckoutPatienceRules.GetStage(
                    patience,
                    elapsedSeconds);
                if (nextStage != stage)
                {
                    stage = nextStage;
                    HandleCheckoutWaitStage(actor, patience, stage, elapsedSeconds);
                    if (stage == CustomerCheckoutWaitStage.Abandoned)
                    {
                        session.Abandoned = true;
                        break;
                    }
                }

                actor?.Brain?.SetActionPhase(
                    "계산 대기",
                    this,
                    CustomerCheckoutPatienceRules.GetQueueDetail(patience, elapsedSeconds, stage));
                yield return CheckoutPollDelay;
                elapsedSeconds += CheckoutWaitPollSeconds;
            }
        }
        finally
        {
            waitingCheckoutCount = Mathf.Max(0, waitingCheckoutCount - 1);
            if (waitingCheckoutCount == 0)
            {
                checkoutServiceAlertRaised = false;
            }
            MarkFacilityDynamicStateDirty();
        }

        if (shopping?.LastVisitOutcome != ShoppingVisitOutcome.Abandoned
            && HasServingWorker)
        {
            actor?.Brain?.SetActionPhase("계산 중", this, $"{Mathf.CeilToInt(elapsedSeconds)}초 기다림");
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Shopping,
                CharacterActivityOutcomes.Started,
                $"{objectNameOrDefault()}에서 계산을 시작했다.",
                this,
                actionId: "checkout:staffed"));
        }
    }

    private void HandleCheckoutWaitStage(
        CharacterActor actor,
        CustomerCheckoutPatienceProfile patience,
        CustomerCheckoutWaitStage stage,
        float elapsedSeconds)
    {
        if (actor == null)
        {
            return;
        }

        string actorName = actor.Identity != null ? actor.Identity.DisplayName : actor.name;
        switch (stage)
        {
            case CustomerCheckoutWaitStage.Restless:
                actor.ApplyMoodFactor(
                    $"checkout-wait:{GetInstanceID()}:restless",
                    "계산대가 오래 걸림",
                    -1.5f,
                    90f,
                    1);
                actor.AddActivity(CharacterActivityEvent.Facility(
                    CharacterActivityKinds.Shopping,
                    CharacterActivityOutcomes.Progress,
                    $"{actorName}은 계산 줄이 좀처럼 줄지 않아 초조해졌다.",
                    this,
                    actionId: "checkout:waiting",
                    reasonCode: "queue-restless",
                    value: elapsedSeconds,
                    bubbleEligible: true));
                break;

            case CustomerCheckoutWaitStage.RequestingService:
                RequireWorkforceReplanService().RequestIdleWorkersToReplan();
                actor.AddActivity(CharacterActivityEvent.Facility(
                    CharacterActivityKinds.Shopping,
                    CharacterActivityOutcomes.Responded,
                    patience.PatienceMultiplier >= 1.1f
                        ? $"{actorName}은 계산을 도와줄 직원을 불렀다."
                        : $"{actorName}은 더 기다리기 어렵다며 직원을 찾았다.",
                    this,
                    actionId: "checkout:service-request",
                    reasonCode: "customer-called-worker",
                    value: elapsedSeconds,
                    bubbleEligible: true));
                if (!checkoutServiceAlertRaised)
                {
                    checkoutServiceAlertRaised = true;
                    GameEventBus.RaiseAlert(
                        "계산 지원 필요",
                        $"{actorName}이(가) {objectNameOrDefault()}에서 계산 직원을 찾고 있습니다.",
                        EventAlertImportance.Low,
                        "손님");
                }
                break;

            case CustomerCheckoutWaitStage.Abandoned:
                actor.ApplyMoodFactor(
                    $"checkout-wait:{GetInstanceID()}:abandoned",
                    "기다리다 구매를 포기함",
                    patience.AbandonMoodPenalty,
                    180f,
                    1);
                actor.SocialMemory?.RememberFacilityExperience(
                    this,
                    patience.AbandonSentiment,
                    "계산이 너무 늦어 구매를 포기했다.");
                actor.GetAbility<AbilityShopping>()?.SetVisitOutcome(this, ShoppingVisitOutcome.Abandoned);
                actor.Brain?.SetActionPhase(
                    "구매 포기",
                    this,
                    $"{Mathf.CeilToInt(elapsedSeconds)}초 기다린 뒤 다른 곳을 찾음");
                actor.AddActivity(CharacterActivityEvent.Facility(
                    CharacterActivityKinds.Shopping,
                    CharacterActivityOutcomes.Cancelled,
                    $"{actorName}은 계산을 기다리다 구매를 포기했다.",
                    this,
                    actionId: "checkout:abandoned",
                    reasonCode: "patience-exhausted",
                    value: elapsedSeconds,
                    bubbleEligible: true));
                if (GameTime >= nextCheckoutAbandonAlertTime)
                {
                    nextCheckoutAbandonAlertTime = GameTime + 5f;
                    GameEventBus.RaiseAlert(
                        "손님이 구매를 포기함",
                        $"{actorName}이(가) {objectNameOrDefault()}의 긴 계산 대기 때문에 다른 곳을 찾습니다.",
                        EventAlertImportance.Medium,
                        "손님");
                }
                break;
        }
    }

    private sealed class CheckoutWaitSession
    {
        public bool Abandoned { get; set; }
    }

    private sealed class ShoppingSelectionBuffer
    {
        public readonly Dictionary<int, int> SelectedCounts =
            new Dictionary<int, int>();
        public readonly List<RemainStock> Cart = new List<RemainStock>();

        public void Clear()
        {
            SelectedCounts.Clear();
            Cart.Clear();
        }
    }

    private bool ShouldWaitForServingWorker(CharacterActor actor)
    {
        return actor != null
            && !isDestroy
            && CreatesRevenueFor(actor)
            && RequiresServingWorker()
            && !HasServingWorker;
    }

    public List<Stock> GetStock()
    {
        FillPurchasableStock(null);
        return new List<Stock>(purchasableStockBuffer);
    }

    public IReadOnlyList<Stock> GetPurchasableStock()
    {
        return GetPurchasableStock(null);
    }

    private IReadOnlyList<Stock> GetPurchasableStock(
        IReadOnlyDictionary<int, int> selectedCounts)
    {
        FillPurchasableStock(selectedCounts);
        return purchasableStockBuffer;
    }

    private void FillPurchasableStock(
        IReadOnlyDictionary<int, int> selectedCounts)
    {
        EnsureStockInitialized();
        purchasableStockBuffer.Clear();
        PruneInvalidWorker();
        foreach (RemainStock stock in stocks)
        {
            if (stock == null
                || !IsSaleItemAllowed(stock.id)
                || GetRemainingStockAfterSelection(stock, selectedCounts) <= 0) continue;
            purchasableStockBuffer.Add(CreatePricedStock(stock));
        }
    }

    private static ShoppingSelectionBuffer AcquireShoppingSelectionBuffer()
    {
        ShoppingSelectionBuffer buffer = ShoppingSelectionPool.Count > 0
            ? ShoppingSelectionPool.Pop()
            : new ShoppingSelectionBuffer();
        buffer.Clear();
        return buffer;
    }

    private static void ReleaseShoppingSelectionBuffer(
        ShoppingSelectionBuffer buffer)
    {
        if (buffer == null)
        {
            return;
        }

        buffer.Clear();
        ShoppingSelectionPool.Push(buffer);
    }

    private Stock CreatePricedStock(RemainStock stock)
    {
        if (stock == null)
        {
            return new Stock(-1, 0);
        }

        return new Stock(stock.id, Mathf.FloorToInt(stock.cost * GetPriceMultiplier()));
    }

    private float GetPriceMultiplier()
    {
        PruneInvalidWorker();
        return worker == null ? 1.0f : 1.2f;
    }

    private static int GetRemainingStockAfterSelection(
        RemainStock stock,
        IReadOnlyDictionary<int, int> selectedCounts)
    {
        if (stock == null)
        {
            return 0;
        }

        int selected = 0;
        selectedCounts?.TryGetValue(stock.id, out selected);
        return stock.stock - selected;
    }
    public int GetStockCount()
    {
        int count = 0;
        foreach (RemainStock stock in stocks)
        {
            if (stock != null && IsSaleItemAllowed(stock.id))
            {
                count += stock.stock;
            }
        }

        return count;
    }

    public int RestockFrom(IEnumerable<IWarehouseFacility> warehouses, int maxAmount, out string resultMessage)
    {
        EnsureStockInitialized();
        resultMessage = string.Empty;
        if (baseStock == null || baseStock.stocks == null || baseStock.stocks.Count == 0)
        {
            resultMessage = "보충할 상품 데이터가 없습니다";
            return 0;
        }

        int targetAmount = Mathf.Min(Mathf.Max(0, maxAmount), MissingStock);
        if (targetAmount <= 0)
        {
            resultMessage = "재고가 이미 가득 찼습니다";
            return 0;
        }

        int restocked = 0;
        foreach (var stockTuple in baseStock.stocks)
        {
            if (stockTuple == null || stockTuple.Item1 == null) continue;

            SaleItem saleItem = stockTuple.Item1;
            if (!IsSaleItemAllowed(saleItem)) continue;
            while (restocked < targetAmount)
            {
                IWarehouseFacility warehouse = warehouses?
                    .FirstOrDefault((candidate) => candidate != null
                        && candidate.HasWarehouseInventory
                        && candidate.Inventory.HasStock(saleItem.category));
                if (warehouse == null)
                {
                    break;
                }

                int withdrawn = warehouse.Inventory.Withdraw(saleItem.category, 1);
                if (withdrawn <= 0)
                {
                    break;
                }

                AddRemainStock(saleItem, withdrawn);
                restocked += withdrawn;
            }

            if (restocked >= targetAmount)
            {
                break;
            }
        }

        resultMessage = restocked > 0
            ? $"{restocked}개 보충"
            : "창고 재고 부족";
        PublishGameEvent(new FacilityRestockEvent(this, targetAmount, restocked, resultMessage));
        if (restocked > 0)
        {
            MarkFacilityDynamicStateDirty();
        }

        return restocked;
    }

    public bool TryFindRestockSource(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out IWarehouseFacility warehouse,
        out SaleItem saleItem,
        out int amount,
        out string failureReason)
    {
        warehouse = null;
        saleItem = null;
        amount = 0;
        failureReason = string.Empty;
        EnsureStockInitialized();

        if (baseStock == null || baseStock.stocks == null || baseStock.stocks.Count == 0)
        {
            failureReason = "보충할 상품 데이터가 없습니다";
            return false;
        }

        int targetAmount = Mathf.Min(Mathf.Max(0, maxAmount), MissingStock);
        if (targetAmount <= 0)
        {
            failureReason = "재고가 이미 충분함";
            return false;
        }

        foreach (var stockTuple in baseStock.stocks)
        {
            if (stockTuple == null || stockTuple.Item1 == null)
            {
                continue;
            }

            SaleItem candidateItem = stockTuple.Item1;
            if (!IsSaleItemAllowed(candidateItem))
            {
                continue;
            }
            IWarehouseFacility candidateWarehouse = warehouses?
                .FirstOrDefault((candidate) => candidate != null
                    && candidate.HasWarehouseInventory
                    && candidate.Inventory != null
                    && candidate.Inventory.HasStock(candidateItem.category));
            if (candidateWarehouse == null)
            {
                continue;
            }

            int available = candidateWarehouse.Inventory.GetStock(candidateItem.category);
            int loadAmount = Mathf.Min(targetAmount, available);
            if (loadAmount <= 0)
            {
                continue;
            }

            warehouse = candidateWarehouse;
            saleItem = candidateItem;
            amount = loadAmount;
            return true;
        }

        failureReason = "창고 재고 부족";
        return false;
    }

    public int ReceiveRestock(SaleItem saleItem, int amount, int requestedAmount, out string resultMessage)
    {
        resultMessage = string.Empty;
        if (saleItem == null)
        {
            resultMessage = "보충할 상품 데이터가 없습니다";
            return 0;
        }

        if (!IsSaleItemAllowed(saleItem))
        {
            resultMessage = "현재 방 구성과 맞지 않는 상품입니다.";
            return 0;
        }

        int restocked = Mathf.Min(Mathf.Max(0, amount), MissingStock);
        if (restocked <= 0)
        {
            resultMessage = "재고가 이미 충분함";
            return 0;
        }

        AddRemainStock(saleItem, restocked);
        resultMessage = $"{restocked}개 보충";
        PublishGameEvent(new FacilityRestockEvent(
            this,
            Mathf.Max(0, requestedAmount),
            restocked,
            resultMessage));
        MarkFacilityDynamicStateDirty();
        return restocked;
    }

    public bool HasRestockSupply(IEnumerable<IWarehouseFacility> warehouses, out string failureReason)
    {
        EnsureStockInitialized();
        failureReason = string.Empty;
        if (!NeedsRestock)
        {
            failureReason = "재고가 이미 충분함";
            return false;
        }

        if (baseStock == null || baseStock.stocks == null || baseStock.stocks.Count == 0)
        {
            failureReason = "보충할 상품 데이터가 없습니다";
            return false;
        }

        foreach (var stockTuple in baseStock.stocks)
        {
            if (stockTuple == null || stockTuple.Item1 == null)
            {
                continue;
            }

            StockCategory category = stockTuple.Item1.category;
            if (!IsSaleItemAllowed(stockTuple.Item1))
            {
                continue;
            }
            if (warehouses != null
                && warehouses.Any((warehouse) => warehouse != null
                    && warehouse.HasWarehouseInventory
                    && warehouse.Inventory != null
                    && warehouse.Inventory.HasStock(category)))
            {
                return true;
            }
        }

        failureReason = "창고 재고 부족";
        return false;
    }

#if UNITY_EDITOR
    public void DebugClearStock()
    {
        stocks.Clear();
        MarkFacilityDynamicStateDirty();
    }
#endif

    private Vector2 GetCheckoutWorldPosition()
    {
        int endX = buildPoses.Max((pos) => pos.x) - 1;
        return grid.GetWorldPos(new Vector2Int(endX, centerPos.y));
    }

    internal override float GetLegacyWorkUrgency(FacilityWorkType workType)
    {
        float urgency = base.GetLegacyWorkUrgency(workType);
        if (workType == FacilityWorkType.Operate
            && waitingCheckoutCount > 0
            && !HasServingWorker)
        {
            urgency += WaitingCheckoutOperateUrgency
                + waitingCheckoutCount * WaitingCheckoutOperateUrgencyPerCustomer;
        }

        return urgency;
    }

    public override bool isVisitable()
    {
        return CanVisit((CharacterActor)null, out _);
    }

    public FacilityAssignmentStatus GetWorkerAssignmentStatus(CharacterActor actor)
    {
        PruneInvalidWorker();
        FacilityAssignmentStatus workStatus = FacilityAssignmentStatus.Rejected(
            FacilityAssignmentFailureKind.UnsupportedWork,
            "지원하지 않는 작업");
        foreach (WorkTypeDefinition definition in WorkTypeCatalog.Enumerate(
                     Facility != null ? Facility.supportedWorkTypes : FacilityWorkType.Operate))
        {
            workStatus = GetWorkAssignmentStatus(definition.WorkTypeId);
            if (workStatus.IsAllowed)
            {
                break;
            }
        }

        if (!workStatus.IsAllowed)
        {
            return workStatus;
        }

        if (worker != null && worker != actor)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Occupied,
                "이미 근무자 있음");
        }

        if (HasWorkerReservationForOther(actor))
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Reserved,
                "이미 작업 예약됨");
        }

        return FacilityAssignmentStatus.Allowed();
    }

    public bool CanAssignWorker(CharacterActor actor, out string failureReason)
    {
        FacilityAssignmentStatus status = GetWorkerAssignmentStatus(actor);
        failureReason = status.Reason;
        return status.IsAllowed;
    }

    public IEnumerator AllocateWorker(CharacterActor actor)
    {
        PruneInvalidWorker();
        if (worker != null && worker != actor)
        {
            yield break;
        }
        worker = actor;
        MarkFacilityDynamicStateDirty();
        ReleaseWorkerReservation(actor);
        AbilityMove moveable = actor != null ? actor.GetAbility<AbilityMove>() : null;
        if (moveable == null) yield break;

        Vector2 endPos = GetFacilityAnchorWorldPosition(FacilityAnchorPurposeIds.Work, actor.transform.position);
        AIAction currentAction = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;
        actor?.Brain?.SetActionPhase("\uC791\uC5C5\uB300 \uC811\uADFC", this);
        yield return moveable.Move2PosBySpeed(endPos, 1f, currentAction);
        actor.ChangeLayer("DungeonMiddleObject");
        yield return moveable.Move2PosBySpeed(endPos + new Vector2(0, 0.15f), 3f, currentAction);
        actor?.Brain?.SetActionPhase("\uC791\uC5C5 \uC790\uC138", this);
        actor.Flip(CharacterFacing.RIGHT);
    }

    public void DeallocateWorker(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        PruneInvalidWorker();
        if (worker != actor) return;

        worker = null;
        MarkFacilityDynamicStateDirty();
        actor.Brain?.SetActionPhase("\uC2DC\uC124 \uD1F4\uC7A5", this);
        actor.transform.position = actor.transform.position - new Vector3(0, 0.15f);
        Vector2Int actorGridPosition = grid != null
            ? grid.GetXY(actor.transform.position)
            : centerPos;
        if (!ContainsGridPosition(actorGridPosition)
            && TryGetFacilityOccupiedWorldPosition(actor.transform.position, out Vector3 exitPosition))
        {
            actor.transform.position = exitPosition;
        }
        actor.ChangeLayer("Default");
    }

    private IEnumerator Linger(CharacterActor actor, float seconds, AIAction expectedAction)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        float timer = 0f;
        while (timer < seconds)
        {
            if (expectedAction != null
                && (actor == null
                    || actor.Brain == null
                    || actor.Brain.bestAction != expectedAction
                    || actor.Brain.isBestActionEnd))
            {
                yield break;
            }

            timer += GameDeltaTime;
            yield return null;
        }
    }

    private void PruneInvalidWorker()
    {
        if (worker == null)
        {
            return;
        }

        try
        {
            if (worker.gameObject == null
                || !worker.gameObject.scene.IsValid()
                || !worker.gameObject.activeInHierarchy)
            {
                worker = null;
                MarkFacilityDynamicStateDirty();
            }
        }
        catch (MissingReferenceException)
        {
            worker = null;
            MarkFacilityDynamicStateDirty();
        }
    }

    private bool RequiresServingWorker()
    {
        return Facility != null
            && BuildingData.RequiresStaffedService()
            && Facility.SupportsWork(BuiltInWorkTypeIds.Operate);
    }

    private void FillStock()
    {
        stocks.Clear();
        if (baseStock == null || baseStock.stocks == null) return;

        FacilityRoomOperationalProfile profile = GetRoomOperationalProfile();
        Dictionary<StockCategory, int> remainingByCategory = new Dictionary<StockCategory, int>();
        foreach (var stockTuple in baseStock.stocks)
        {
            if (stockTuple == null || stockTuple.Item1 == null) continue;

            var(stock, count) = stockTuple;
            if (!remainingByCategory.TryGetValue(stock.category, out int remainingCapacity))
            {
                remainingCapacity = GetConfiguredInternalStockCapacity()
                    + profile.GetStorageCapacity(stock.category);
            }

            int initialStock = Mathf.Min(Mathf.Max(0, count), remainingCapacity);
            if (initialStock <= 0) continue;

            stocks.Add(CreateRemainStock(stock, initialStock));
            remainingByCategory[stock.category] = remainingCapacity - initialStock;
        }

        MarkFacilityDynamicStateDirty();
    }

    private void AddRemainStock(SaleItem saleItem, int amount)
    {
        if (saleItem == null || amount <= 0) return;

        RemainStock remainStock = stocks.FirstOrDefault((stock) => stock.id == saleItem.id);
        if (remainStock == null)
        {
            stocks.Add(CreateRemainStock(saleItem, amount));
            MarkFacilityDynamicStateDirty();
            return;
        }

        remainStock.stock += amount;
        MarkFacilityDynamicStateDirty();
    }

    private RemainStock CreateRemainStock(SaleItem saleItem, int count)
    {
        return new RemainStock(
            saleItem.id,
            saleItem.itemName,
            Mathf.FloorToInt(saleItem.cost * (baseStock != null ? baseStock.multifly : 1f)),
            count,
            saleItem.buyevent);
    }

    private StockCategory GetStockCategory(int saleItemId)
    {
        return RequireStockCatalog().GetStockCategory(saleItemId);
    }

    private bool IsSaleItemAllowed(int saleItemId)
    {
        return ShouldUseBaseStockPassThrough()
            || GetStockCategory(saleItemId) == ActiveStockCategory;
    }

    private bool IsSaleItemAllowed(SaleItem saleItem)
    {
        return saleItem != null
            && (ShouldUseBaseStockPassThrough()
                || saleItem.category == ActiveStockCategory);
    }

    private StockCategory ResolveActiveStockCategory()
    {
        FacilityRoomOperationalProfile profile = GetRoomOperationalProfile();
        if (HasRetailSpecializationSignal(profile))
        {
            return profile.RetailCategory;
        }

        return TryGetSingleBaseStockCategory(out StockCategory fallbackCategory)
            ? fallbackCategory
            : profile.RetailCategory;
    }

    private static bool HasRetailSpecializationSignal(FacilityRoomOperationalProfile profile)
    {
        if (profile?.Parts == null)
        {
            return false;
        }

        foreach (BuildableObject part in profile.Parts)
        {
            if (part?.BuildingData != null
                && part.BuildingData.GetStockCategorySignals().Any())
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetSingleBaseStockCategory(out StockCategory category)
    {
        category = StockCategory.General;
        if (baseStock == null || baseStock.stocks == null)
        {
            return false;
        }

        bool found = false;
        foreach (var stockTuple in baseStock.stocks)
        {
            SaleItem saleItem = stockTuple?.Item1;
            if (saleItem == null)
            {
                continue;
            }

            if (!found)
            {
                category = saleItem.category;
                found = true;
                continue;
            }

            if (saleItem.category != category)
            {
                return false;
            }
        }

        return found;
    }

    private bool ShouldUseBaseStockPassThrough()
    {
        FacilityRoomOperationalProfile profile = GetRoomOperationalProfile();
        return !HasRetailSpecializationSignal(profile)
            && !TryGetSingleBaseStockCategory(out _);
    }

    private int GetMaxInternalStock(StockCategory category)
    {
        return GetConfiguredInternalStockCapacity()
            + GetRoomOperationalProfile().GetStorageCapacity(category);
    }

    private int GetConfiguredInternalStockCapacity()
    {
        int configuredCapacity = BuildingData.GetInternalStockCapacity();
        return configuredCapacity > 0
            ? configuredCapacity
            : GetConfiguredStockCapacity();
    }

    public ShopStockStateSnapshot CreateStockSnapshot()
    {
        EnsureStockInitialized();
        return new ShopStockStateSnapshot
        {
            items = stocks
                .Where((stock) => stock != null)
                .Select((stock) => new ShopStockItemSnapshot
                {
                    saleItemId = stock.id,
                    amount = Mathf.Max(0, stock.stock)
                })
                .ToList()
        };
    }

    public void ApplyStockSnapshot(ShopStockStateSnapshot snapshot)
    {
        EnsureStockInitialized();
        stocks.Clear();
        if (snapshot?.items == null || baseStock?.stocks == null)
        {
            MarkFacilityDynamicStateDirty();
            return;
        }

        foreach (ShopStockItemSnapshot item in snapshot.items)
        {
            SaleItem saleItem = baseStock.stocks
                .Where((tuple) => tuple != null)
                .Select((tuple) => tuple.Item1)
                .FirstOrDefault((candidate) => candidate != null && candidate.id == item.saleItemId);
            if (saleItem != null && item.amount > 0)
            {
                stocks.Add(CreateRemainStock(saleItem, item.amount));
            }
        }

        MarkFacilityDynamicStateDirty();
    }

    private int GetConfiguredStockCapacity()
    {
        if (baseStock == null || baseStock.stocks == null)
        {
            return 0;
        }

        int capacity = 0;
        foreach (var stockTuple in baseStock.stocks)
        {
            if (stockTuple != null)
            {
                capacity += Mathf.Max(0, stockTuple.Item2);
            }
        }

        return capacity;
    }

    private string objectNameOrDefault()
    {
        return BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.objectName)
            ? BuildingData.objectName
            : name;
    }

    private bool TryInitializeStock(bool requireCatalog)
    {
        if (stockInitialized)
        {
            return baseStock != null;
        }

        if (BuildingData == null)
        {
            return false;
        }

        if (stockCatalog == null)
        {
            if (!requireCatalog)
            {
                return false;
            }

            RuntimeDependency.Require(stockCatalog, this);
        }

        stockInitialized = true;
        if (!stockCatalog.TryGetStockInfoForShop(id, out baseStock))
        {
            Debug.LogWarning($"{name} 상점 재고 데이터를 찾지 못했습니다. shopId: {id}");
            return false;
        }

        FillStock();
        return true;
    }

    private void EnsureStockInitialized()
    {
        TryInitializeStock(requireCatalog: true);
    }

    private bool TryResolveGameData(bool requireProvider)
    {
        if (gameData != null)
        {
            return true;
        }

        if (gameDataProvider == null)
        {
            if (!requireProvider)
            {
                return false;
            }

            RuntimeDependency.Require(gameDataProvider, this);
        }

        bool resolved = gameDataProvider.TryGetGameData(out gameData);
        if (requireProvider)
        {
            RuntimeDependency.Ensure(
                resolved && gameData != null,
                this,
                "IGameDataProvider to return GameData");
        }

        return resolved;
    }

    private IShopStockCatalog RequireStockCatalog()
    {
        return RuntimeDependency.Require(stockCatalog, this);
    }

    private IFloatingNumberFeedbackService RequireFloatingNumberFeedbackService()
    {
        return floatingNumberFeedbackService ?? FallbackFloatingNumberFeedbackService;
    }

    private IWorkforceReplanService RequireWorkforceReplanService()
    {
        return RuntimeDependency.Require(workforceReplanService, this);
    }

    private sealed class ShopNoopFloatingNumberFeedbackService : IFloatingNumberFeedbackService
    {
        public bool TryShow(NumberCondition condition, Vector3 worldPosition, float value) => false;
    }

}

public readonly struct RetailProductSnapshot
{
    public RetailProductSnapshot(int id, string name, int price, int quantity)
    {
        Id = id;
        Name = name ?? string.Empty;
        Price = Mathf.Max(0, price);
        Quantity = Mathf.Max(0, quantity);
    }

    public int Id { get; }
    public string Name { get; }
    public int Price { get; }
    public int Quantity { get; }
}

public class RemainStock
{
    public int id;
    public string itemName;
    public int cost;
    public int stock;
    public OnBuyItemSO[] onbuy;
    public RemainStock(int id,string itemName, int cost, int stock, OnBuyItemSO[] onbuy)
    {
        this.id = id;
        this.itemName = itemName;
        this.cost = cost;
        this.stock = stock;
        this.onbuy = onbuy;
    }
}
public struct Stock
{
    public int id;
    public int cost;

    public Stock(int id, int cost) : this()
    {
        this.id = id;
        this.cost = cost;
    }
}

[Serializable]
public sealed class ShopStockStateSnapshot
{
    public List<ShopStockItemSnapshot> items = new List<ShopStockItemSnapshot>();
}

[Serializable]
public sealed class ShopStockItemSnapshot
{
    public int saleItemId;
    public int amount;
}
