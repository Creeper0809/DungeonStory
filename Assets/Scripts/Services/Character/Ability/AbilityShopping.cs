using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public enum ShoppingVisitOutcome
{
    None = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Abandoned = 4
}

public class AbilityShopping : CharacterAbility
{
    private const int DefaultMaxLookAroundCount = 1;

    private int holdingMoney;
    private readonly List<BuildableObject> mutableVisitedBuildings = new List<BuildableObject>();
    private IReadOnlyList<BuildableObject> visitedBuildingsView;
    private bool attemptedLooseItemTheftBeforeExit;
    private BuildableObject currentVisitTarget;
    [SerializeField, Min(0.05f)]
    private float purchaseFeedbackIconMaxWorldSize = FloatingIconFeedbackDefaults.DefaultMaxWorldSize;
    private IShopStockCatalog shopStockCatalog;
    private IFloatingIconFeedbackService floatingIconFeedbackService;
    private IRandomStreamProvider randomStreamProvider;
    private IGameClock gameClock;
    private IGameEventBus gameEventBus;

    public int visitCount { get; private set; }
    public int lookAroundCount { get; private set; }
    public IReadOnlyList<BuildableObject> visitedBuilding =>
        visitedBuildingsView ??= ReadOnlyView.List(mutableVisitedBuildings);
    public int HoldingMoney => holdingMoney;
    public ShoppingVisitOutcome LastVisitOutcome { get; private set; }

    [Inject]
    public void ConstructAbilityShopping(
        IShopStockCatalog shopStockCatalog,
        IFloatingIconFeedbackService floatingIconFeedbackService,
        IRandomStreamProvider randomStreamProvider,
        IGameClock gameClock,
        IGameEventBus gameEventBus)
    {
        this.shopStockCatalog = shopStockCatalog
            ?? throw new ArgumentNullException(nameof(shopStockCatalog));
        this.floatingIconFeedbackService = floatingIconFeedbackService
            ?? throw new ArgumentNullException(nameof(floatingIconFeedbackService));
        this.randomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public override void Initializtion(CharacterSO data)
    {
        base.Initializtion(data);
        mutableVisitedBuildings.Clear();
        IRandomStream randomStream = GetRandomStream();
        visitCount = data != null ? data.GetFrequencyVisit(randomStream) : 1;
        lookAroundCount = 0;
        attemptedLooseItemTheftBeforeExit = false;
        currentVisitTarget = null;
        LastVisitOutcome = ShoppingVisitOutcome.None;
        holdingMoney = data != null
            ? Mathf.Max(0, Mathf.RoundToInt(
                data.GetHoldingMoney(randomStream)
                    * (actor?.Stats?.GetSpendingMultiplier() ?? 1f)))
            : 0;
    }
    public Stock DetermineBuyingItem(IReadOnlyList<Stock> stocks)
    {
        if (stocks == null || stocks.Count == 0)
        {
            return new Stock(-1,0);
        }

        if (IsInternalStaffUse())
        {
            return stocks[GetRandomStream().NextInt(0, stocks.Count)];
        }

        int affordableCount = 0;
        Stock selected = default;
        foreach (Stock stock in stocks)
        {
            if (stock.cost > holdingMoney)
            {
                continue;
            }

            affordableCount++;
            if (GetRandomStream().NextInt(0, affordableCount) == 0)
            {
                selected = stock;
            }
        }

        return affordableCount > 0 ? selected : new Stock(-1, 0);
    }

    public bool CanPay(Stock stock)
    {
        return actor != null
            && (IsInternalStaffUse() || stock.cost <= holdingMoney);
    }

    public bool CanBuyFrom(IRetailFacility shop, out string failureReason)
    {
        failureReason = string.Empty;
        if (shop == null)
        {
            failureReason = "상점 없음";
            return false;
        }

        if (IsInternalStaffUse())
        {
            failureReason = "직원은 구매 상점을 이용하지 않음";
            return false;
        }

        IReadOnlyList<Stock> stocks = shop.GetPurchasableStock();
        if (stocks == null || stocks.Count == 0)
        {
            failureReason = "재고 없음";
            return false;
        }

        bool canPayAny = false;
        foreach (Stock stock in stocks)
        {
            if (CanPay(stock))
            {
                canPayAny = true;
                break;
            }
        }

        if (!canPayAny)
        {
            failureReason = "소지금 부족";
            return false;
        }

        return true;
    }

    public float GetAffordabilityScore(IRetailFacility shop)
    {
        if (shop == null) return 1f;

        IReadOnlyList<Stock> stocks = shop.GetPurchasableStock();
        if (stocks == null || stocks.Count == 0)
        {
            return 0f;
        }

        if (IsInternalStaffUse())
        {
            return 1f;
        }

        int affordableCount = 0;
        foreach (Stock stock in stocks)
        {
            if (CanPay(stock))
            {
                affordableCount++;
            }
        }

        return Mathf.Clamp01((float)affordableCount / stocks.Count);
    }

    public void StartSopping()
    {
        move?.CancelActiveMovement();
        StartCoroutine(Shopping());
    }
    private IEnumerator Shopping()
    {
        AIAction action = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;
        if (action == null || action.destination == null)
        {
            actor?.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Shopping,
                CharacterActivityOutcomes.Failed,
                "쇼핑 실패: 목적지 없음",
                actionId: "shopping:visit",
                reasonCode: "missing-destination",
                sentiment: -0.7f,
                bubbleEligible: true));
            if (actor != null && actor.Brain != null)
            {
                actor.Brain.isBestActionEnd = true;
            }
            yield break;
        }

        if (move == null || grid == null)
        {
            CacheCommonReferences();
        }
        if (move == null || grid == null)
        {
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Shopping,
                CharacterActivityOutcomes.Failed,
                "쇼핑 실패: 이동 정보 없음",
                action.destination,
                actionId: "shopping:visit",
                reasonCode: "missing-movement-context",
                bubbleEligible: true));
            action.ReleaseReservation(actor);
            if (actor != null && actor.Brain != null)
            {
                actor.Brain.isBestActionEnd = true;
            }
            yield break;
        }

        yield return move.MoveByCurrentBestActionPath();
        GridCell destinationCell = grid.GetGridCell(grid.GetXY(transform.position));
        if (destinationCell != null
            && destinationCell.ContainsOccupant(action.destination)
            && action.destination is IInteractable shop)
        {
            BeginVisitInteraction(action.destination);
            yield return shop.Interact(actor);
            action.ReleaseReservation(actor);
            if (LastVisitOutcome == ShoppingVisitOutcome.Abandoned)
            {
                RegisterAvoidedVisit(action.destination);
            }
            else
            {
                RegisterVisit(action.destination);
            }
        }
        else
        {
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Shopping,
                CharacterActivityOutcomes.Failed,
                "쇼핑 실패: 목적지 도달 실패",
                action.destination,
                actionId: "shopping:visit",
                reasonCode: "destination-unreachable",
                bubbleEligible: true));
            action.ReleaseReservation(actor);
        }
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }
    }

    public void RegisterVisit(BuildableObject building)
    {
        if (building != null && !mutableVisitedBuildings.Contains(building))
        {
            mutableVisitedBuildings.Add(building);
        }

        if (visitCount > 0)
        {
            visitCount--;
        }
    }

    public bool HasVisited(BuildableObject building)
    {
        return building != null && mutableVisitedBuildings.Contains(building);
    }

    public void RegisterAvoidedVisit(BuildableObject building)
    {
        if (building != null && !mutableVisitedBuildings.Contains(building))
        {
            mutableVisitedBuildings.Add(building);
        }
    }

    public void BeginVisitInteraction(BuildableObject building)
    {
        currentVisitTarget = building;
        LastVisitOutcome = ShoppingVisitOutcome.InProgress;
    }

    public void SetVisitOutcome(BuildableObject building, ShoppingVisitOutcome outcome)
    {
        if (building == null || currentVisitTarget == building)
        {
            currentVisitTarget = building;
            LastVisitOutcome = outcome;
        }
    }

    public void RegisterLookAround()
    {
        lookAroundCount++;
    }

    public void BeginOffDutyVisitCycle()
    {
        visitCount = Mathf.Max(visitCount, 1);
        lookAroundCount = DefaultMaxLookAroundCount;
        mutableVisitedBuildings.Clear();
    }

    public void RestorePersistentState(int savedVisitCount, int savedLookAroundCount, int savedHoldingMoney)
    {
        visitCount = Mathf.Max(0, savedVisitCount);
        lookAroundCount = Mathf.Max(0, savedLookAroundCount);
        holdingMoney = Mathf.Max(0, savedHoldingMoney);
        mutableVisitedBuildings.Clear();
    }

    public bool IsOffDutyStaffVisitor()
    {
        return actor != null
            && IsInternalStaffUse()
            && abilityCache != null
            && abilityCache.TryGetAbility(out AbilityWork work)
            && work.IsOffDuty;
    }

    public FacilityRole GetInterestRoles()
    {
        return CharacterVisitPolicy.GetInterestRoles(actor);
    }

    public bool CanLookAround()
    {
        return actor != null
            && visitCount > 0
            && ShouldEndVisitCycle()
            && lookAroundCount < DefaultMaxLookAroundCount;
    }

    public bool ShouldExitDungeon()
    {
        return actor != null
            && ShouldEndVisitCycle()
            && (visitCount <= 0
                || lookAroundCount >= DefaultMaxLookAroundCount);
    }

    public bool ShouldEndVisitCycle()
    {
        return visitCount <= 0 || !IsThereVisitableBuilding();
    }

    public bool IsThereVisitableBuilding()
    {
        if (grid == null)
        {
            CacheCommonReferences();
        }

        if (grid == null)
        {
            return false;
        }

        if (visitCount <= 0)
        {
            return false;
        }

        if (actor == null)
        {
            return false;
        }

        foreach (BuildableObject building in actor.GetReachableBuilding())
        {
            if (CanVisitBuilding(building))
            {
                return true;
            }
        }

        return false;
    }
    public BuildableObject FindShop()
    {
        if (actor == null)
        {
            return null;
        }

        BuildableObject randomCandidate = null;
        BuildableObject favorite = null;
        int candidateCount = 0;
        int favoriteCount = 0;
        CharacterSO characterData = identity != null ? identity.Data : null;
        int wantId = characterData != null
            && characterData.favoriteStore != null
            && characterData.favoriteStore.Length > 0
                ? characterData.favoriteStore[
                    GetRandomStream().NextInt(0, characterData.favoriteStore.Length)].id
                : -1;

        foreach (BuildableObject building in actor.GetReachableBuilding())
        {
            if (!CanVisitBuilding(building))
            {
                continue;
            }

            candidateCount++;
            if (GetRandomStream().NextInt(0, candidateCount) == 0)
            {
                randomCandidate = building;
            }

            if (building.id == wantId)
            {
                favoriteCount++;
                if (GetRandomStream().NextInt(0, favoriteCount) == 0)
                {
                    favorite = building;
                }
            }
        }

        return favorite != null ? favorite : randomCandidate;
    }

    private bool CanVisitBuilding(BuildableObject building)
    {
        if (!CharacterVisitPolicy.CanVisitBuilding(
            actor,
            building,
            HasVisited(building),
            out _))
        {
            return false;
        }

        return building is not IRetailFacility shop || CanBuyFrom(shop, out _);
    }

    public int GetShoppingCount()
    {
        int baseCount = GetRandomStream().NextInt(1, 4);
        float multiplier = actor != null && actor.Stats != null ? actor.Stats.GetConsumptionMultiplier() : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(baseCount * multiplier));
    }
    public IEnumerator BuyItem(RemainStock item, int purchaseCost)
    {
        IShopStockCatalog catalog = shopStockCatalog
            ?? throw new InvalidOperationException($"{nameof(AbilityShopping)} requires {nameof(IShopStockCatalog)} injection.");
        if (catalog.TryGetSaleItem(item.id, out SaleItem iteminfo))
        {
            RequireFloatingIconFeedbackService().Show(this, iteminfo.itemSprite, purchaseFeedbackIconMaxWorldSize);
        }

        yield return new WaitForSeconds(0.5f);
        if (!IsInternalStaffUse())
        {
            holdingMoney -= Mathf.Max(0, purchaseCost);
        }

        if (!IsInternalStaffUse() && iteminfo != null)
        {
            AddPurchasedItemToCarry(iteminfo);
        }

        foreach(var events in item.onbuy)
        {
            events.Onbuy(actor);
        }
    }

    private void AddPurchasedItemToCarry(SaleItem itemInfo)
    {
        if (actor == null || itemInfo == null)
        {
            return;
        }

        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        IWorldItemStackRuntime itemRuntime = actor.WorldItemStackRuntime;
        if (inventory == null || itemRuntime == null)
        {
            return;
        }

        string itemId = DungeonItemCatalogSO.StockItemId(itemInfo.category);
        inventory.TryAdd(
            $"purchase:{itemInfo.id}:{RequireGameClock().FrameCount}",
            itemId,
            1,
            itemRuntime.CatalogProvider,
            itemRuntime.HaulingSettingsProvider,
            out _);
    }

    public bool TryStealLooseItemBeforeExit()
    {
        if (attemptedLooseItemTheftBeforeExit
            || actor == null
            || IsInternalStaffUse()
            || actor.characterType != CharacterType.Customer
            || actor.WorldItemStackRuntime == null)
        {
            return false;
        }

        attemptedLooseItemTheftBeforeExit = true;
        if (!actor.WorldItemStackRuntime.TryStealLooseItem(
                actor,
                4,
                out WorldItemStackSnapshot stolen,
                out _))
        {
            return false;
        }

        string itemName = !string.IsNullOrWhiteSpace(stolen?.DisplayName)
            ? stolen.DisplayName
            : "item";
        string detail = $"{actor.name} pocketed {itemName} from the floor.";
        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Shopping,
            CharacterActivityOutcomes.Damaged,
            detail,
            actionId: "crime:floor-theft",
            targetId: stolen?.StackId ?? string.Empty,
            targetName: itemName,
            reasonCode: "floor-theft",
            value: stolen?.TotalValue ?? 0,
            quantity: 1,
            sentiment: -0.5f,
            bubbleEligible: true));
        gameEventBus.RaiseAlert("바닥 물품 도난", detail, EventAlertImportance.Medium, "범죄");
        return true;
    }

    private bool IsInternalStaffUse()
    {
        return CharacterWorkRoleUtility.TryGetWork(actor, out _);
    }

    private IFloatingIconFeedbackService RequireFloatingIconFeedbackService()
    {
        return floatingIconFeedbackService
            ?? throw new InvalidOperationException($"{nameof(AbilityShopping)} requires {nameof(IFloatingIconFeedbackService)} injection.");
    }

    private IRandomStream GetRandomStream()
    {
        IRandomStreamProvider provider = randomStreamProvider
            ?? throw new InvalidOperationException(
                $"{nameof(AbilityShopping)} requires {nameof(IRandomStreamProvider)} injection.");
        string actorId = identity != null
            && !string.IsNullOrWhiteSpace(identity.PersistentId)
                ? identity.PersistentId
                : $"instance:{GetInstanceID()}";
        return provider.Get($"shopping:{actorId}");
    }

    private IGameClock RequireGameClock()
    {
        return gameClock
            ?? throw new InvalidOperationException(
                $"{nameof(AbilityShopping)} requires {nameof(IGameClock)} injection.");
    }
}
