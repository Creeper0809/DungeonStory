using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
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
    private static readonly WaitForSeconds PurchaseFeedbackDelay =
        new WaitForSeconds(0.5f);

    private int holdingMoney;
    private bool spendingProjectionApplied;
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
    private IDomainFactPublisher purchaseFacts;
    private IDomainFactRecoveryPublisher purchaseRecovery;
    private bool retailTransactionActive;
    private IGameSessionStateProvider gameDataProvider;
    private ICharacterVisitorExitOutcomeRuntime visitorExitOutcomes;
    private Predicate<BuildableObject> canVisitBuildingPredicate;
    private float decisionStateCapturedAt = float.NegativeInfinity;
    private int decisionStateVisitCount = int.MinValue;
    private int decisionStateLookAroundCount = int.MinValue;
    private int decisionStateFacilityVersion = int.MinValue;
    private bool cachedCanLookAround;
    private bool cachedShouldExitDungeon;
    private Coroutine shoppingRoutine;
    private bool shoppingExecutionActive;
    private AIAction activeShoppingAction;
    private float nextFailedCleanupRetryAt;
    private BuildableObject activeShoppingTarget;
    private Action activeShoppingTargetDestroyedHandler;
    private int committedPurchaseCount;
    private long committedPurchaseMassGrams;
    private RetailStockLotSnapshot lastCommittedPurchaseLot;
    private readonly List<RetailPurchaseRecoverySaveData>
        pendingRetailPurchases = new();
#if UNITY_EDITOR
    public Action<AIAction> DebugBeforeShoppingRoutineStart;
#endif

    public int visitCount { get; private set; }
    public int lookAroundCount { get; private set; }
    public bool HasNoRemainingVisits => visitCount <= 0;
    public bool HasUsedLookAroundAllowance =>
        lookAroundCount >= DefaultMaxLookAroundCount;
    public IReadOnlyList<BuildableObject> visitedBuilding =>
        visitedBuildingsView ??= ReadOnlyView.List(mutableVisitedBuildings);
    public int HoldingMoney
    {
        get
        {
            EnsureSpendingProjection();
            return holdingMoney;
        }
    }
    public ShoppingVisitOutcome LastVisitOutcome { get; private set; }
    public bool HasActiveShoppingRoutineForDiagnostics => shoppingExecutionActive;
    public BuildableObject CurrentVisitTargetForDiagnostics => currentVisitTarget;
    public int CommittedPurchaseCountForDiagnostics => committedPurchaseCount;
    public long CommittedPurchaseMassGramsForDiagnostics =>
        committedPurchaseMassGrams;
    public RetailStockLotSnapshot LastCommittedPurchaseLotForDiagnostics =>
        lastCommittedPurchaseLot?.Clone();

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

    [Inject]
    public void ConstructRetailPurchaseFacts(
        IDomainFactPublisher purchaseFacts,
        IDomainFactRecoveryPublisher purchaseRecovery,
        IGameSessionStateProvider gameDataProvider)
    {
        RequireRetailIdle();
        this.purchaseFacts = purchaseFacts ?? throw new ArgumentNullException(nameof(purchaseFacts));
        this.purchaseRecovery = purchaseRecovery ?? throw new ArgumentNullException(nameof(purchaseRecovery));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
    }

    [Inject]
    public void ConstructVisitorExitOutcome(
        ICharacterVisitorExitOutcomeRuntime visitorExitOutcomes)
    {
        this.visitorExitOutcomes = visitorExitOutcomes
            ?? throw new ArgumentNullException(nameof(visitorExitOutcomes));
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
        committedPurchaseCount = 0;
        committedPurchaseMassGrams = 0L;
        lastCommittedPurchaseLot = null;
        pendingRetailPurchases.Clear();
        holdingMoney = data != null
            ? Mathf.Max(0, data.GetHoldingMoney(randomStream))
            : 0;
        spendingProjectionApplied = false;
        nextFailedCleanupRetryAt = 0f;
    }
    public Stock DetermineBuyingItem(IReadOnlyList<Stock> stocks)
    {
        EnsureSpendingProjection();
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
        EnsureSpendingProjection();
        return actor != null
            && (IsInternalStaffUse() || stock.cost <= holdingMoney);
    }

    public bool CanPayAmount(int amount)
    {
        EnsureSpendingProjection();
        return actor != null
            && (IsInternalStaffUse()
                || Mathf.Max(0, amount) <= holdingMoney);
    }

    public IEnumerator PayForService(
        int amount,
        AIAction expectedAction,
        BuildableObject expectedFacility)
    {
        EnsureSpendingProjection();
        yield return PurchaseFeedbackDelay;
        if (!CanCommitShoppingTransaction(expectedAction, expectedFacility))
        {
            yield break;
        }
        if (!IsInternalStaffUse())
        {
            holdingMoney -= Mathf.Max(0, amount);
        }
    }

    public bool CanBuyFrom(IRetailFacility shop, out string failureReason)
    {
        failureReason = string.Empty;
        if (shop == null)
        {
            failureReason = "상점 없음";
            return false;
        }

        if (shop is Shop concreteShop
            && !TryRecoverPendingRetailPurchases(
                concreteShop,
                out failureReason))
        {
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
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            CompleteCurrentShoppingAction(null, clearFailures: false);

            return;
        }

        StopShopping("shopping-restarted");
        move?.CancelActiveMovement();
        activeShoppingAction = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;
        SubscribeToActiveTargetDestruction(activeShoppingAction);
        shoppingExecutionActive = true;
#if UNITY_EDITOR
        DebugBeforeShoppingRoutineStart?.Invoke(activeShoppingAction);
#endif
        Coroutine started = StartCoroutine(Shopping(activeShoppingAction));
        shoppingRoutine = shoppingExecutionActive ? started : null;
    }
    internal void ResumeFacilityUse(Facility facility, FacilityUseExecutionState state)
    {
        if (shoppingExecutionActive || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            throw new InvalidOperationException("Facility resume actor is inactive or already executing.");
        activeShoppingAction = actor.Brain.RestoreFacilityUseAction(facility, state);
        BeginVisitInteraction(facility);
        SubscribeToActiveTargetDestruction(activeShoppingAction);
        shoppingExecutionActive = true;
        Coroutine started = StartCoroutine(ResumeShoppingAfterRestore(activeShoppingAction));
        shoppingRoutine = shoppingExecutionActive ? started : null;
    }

    internal void ResumeShopUse(Shop shop, ShopUseExecutionState state)
    {
        if (shoppingExecutionActive || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            throw new InvalidOperationException("Shop resume actor is inactive or already executing.");
        activeShoppingAction = actor.Brain.RestoreShopUseAction(shop, state);
        BeginVisitInteraction(shop);
        SubscribeToActiveTargetDestruction(activeShoppingAction);
        shoppingExecutionActive = true;
        Coroutine started = StartCoroutine(ResumeShoppingAfterRestore(activeShoppingAction));
        shoppingRoutine = shoppingExecutionActive ? started : null;
    }

    private IEnumerator ResumeShoppingAfterRestore(AIAction action)
    {
        // StartCoroutine advances synchronously. The current frame's delta was
        // already consumed by the saved execution, and may remain nonzero even
        // after the load UI pauses the game clock. Bind ownership immediately,
        // but do not advance any saved timer/effect until a new frame.
        yield return null;
        yield return Shopping(action, restored: true);
    }

    private IEnumerator Shopping(AIAction action, bool restored = false)
    {
        if (action == null || action.destination == null)
        {
            bool destinationWasDestroyed = action != null
                && !ReferenceEquals(action.destination, null);
            if (actor != null) gameEventBus.Publish(new ShoppingActionObservedEvent(
                actor, ShoppingActionTransition.MissingDestination));
            if (destinationWasDestroyed)
            {
                FailCurrentShoppingAction(
                    action,
                    AIActionFailure.Create(
                        AIActionFailureKind.Destroyed,
                        "Facility destination was destroyed before approach.",
                        action.destination));
                FinishShoppingRoutine(action);
                yield break;
            }
            FailCurrentShoppingAction(
                action,
                AIActionFailure.Create(
                    AIActionFailureKind.NoDestination,
                    "Shopping action has no destination."));
            FinishShoppingRoutine(action);
            yield break;
        }

        if (move == null || grid == null)
        {
            CacheCommonReferences();
        }
        if (move == null || grid == null)
        {
            if (actor != null) gameEventBus.Publish(new ShoppingActionObservedEvent(
                actor, ShoppingActionTransition.MissingMovementContext, action.destination));
            FailCurrentShoppingAction(
                action,
                AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    "Shopping action has no movement context.",
                    action.destination));
            FinishShoppingRoutine(action);
            yield break;
        }

        if (!restored) yield return move.MoveByCurrentBestActionPath();
        if (actor == null
            || actor.Brain == null
            || actor.Brain.bestAction != action)
        {
            action.ReleaseReservation(actor);
            FinishShoppingRoutine(action);
            yield break;
        }
        if (action.destination == null || action.destination.isDestroy)
        {
            FailCurrentShoppingAction(
                action,
                AIActionFailure.Create(
                    AIActionFailureKind.Destroyed,
                    "Facility destination was destroyed during approach.",
                    action.destination));
            FinishShoppingRoutine(action);
            yield break;
        }
        Vector2Int actorGridPosition = grid.GetXY(transform.position);
        if ((restored || action.destination.ContainsGridPosition(actorGridPosition))
            && action.destination is IInteractable shop)
        {
            BeginVisitInteraction(action.destination);
            yield return shop.Interact(actor?.BuildingVisitor);
            if (action.destination == null || action.destination.isDestroy)
            {
                action.ReleaseReservation(actor);
                if (actor?.Brain != null
                    && ReferenceEquals(actor.Brain.bestAction, action))
                {
                    if (actor.Brain.LastActionFailure.Kind
                        != AIActionFailureKind.Destroyed)
                    {
                        FailCurrentShoppingAction(
                            action,
                            AIActionFailure.Create(
                                AIActionFailureKind.Destroyed,
                                "Facility destination was destroyed during interaction.",
                                action.destination));
                    }
                    else
                    {
                        actor.Brain.EndExpectedAction(
                            action,
                            CharacterAiActionTerminalKind.Failed,
                            clearFailures: false);
                    }
                }
                FinishShoppingRoutine(action);
                yield break;
            }
            if (LastVisitOutcome == ShoppingVisitOutcome.InProgress)
            {
                SetVisitOutcome(
                    action.destination,
                    ShoppingVisitOutcome.Failed);
            }
            action.ReleaseReservation(actor);
            if (LastVisitOutcome == ShoppingVisitOutcome.Abandoned)
            {
                RegisterAvoidedVisit(action.destination);
            }
            else if (LastVisitOutcome == ShoppingVisitOutcome.Completed)
            {
                RegisterVisit(action.destination);
            }
        }
        else
        {
            if (actor != null) gameEventBus.Publish(new ShoppingActionObservedEvent(
                actor, ShoppingActionTransition.DestinationUnreachable, action.destination));
            action.ReleaseReservation(actor);
            if (LastVisitOutcome == ShoppingVisitOutcome.InProgress)
            {
                SetVisitOutcome(
                    action.destination,
                    ShoppingVisitOutcome.Failed);
            }
            if (actor?.Brain != null
                && ReferenceEquals(actor.Brain.bestAction, action)
                && !actor.Brain.LastActionFailure.HasFailure)
            {
                actor.Brain.ReportRuntimeActionFailure(
                    AIActionFailure.Create(
                        AIActionFailureKind.NoPath,
                        "Shopping destination was not reached.",
                        action.destination),
                    requestImmediateReplan: false);
            }
        }
        CharacterAiActionTerminalKind terminalKind = LastVisitOutcome switch
        {
            ShoppingVisitOutcome.Completed => CharacterAiActionTerminalKind.Completed,
            ShoppingVisitOutcome.Abandoned => CharacterAiActionTerminalKind.Cancelled,
            _ => CharacterAiActionTerminalKind.Failed
        };
        CompleteCurrentShoppingAction(
            action,
            clearFailures: terminalKind == CharacterAiActionTerminalKind.Completed,
            terminalKind);
        FinishShoppingRoutine(action);
    }

    public void StopShopping(string reason)
    {
        if (currentVisitTarget is Shop pendingShop
            && pendingShop.CustomerServiceSessionRuntime?.IsReplacedFacility(pendingShop) != true
            && pendingRetailPurchases.Any(value => value.facilityId == pendingShop.RequirePersistentInstanceId().Value)
            && !TryRecoverPendingRetailPurchases(pendingShop, out string pendingFailure))
            throw new InvalidOperationException("Shopping cancellation awaits purchase recovery: " + pendingFailure);
        shoppingExecutionActive = false;
        Coroutine routine = shoppingRoutine;
        nextFailedCleanupRetryAt = 0f;
        shoppingRoutine = null;
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        move?.CancelActiveMovement();
        AIAction action = activeShoppingAction;
        activeShoppingAction = null;
        UnsubscribeFromActiveTargetDestruction();
        action?.ReleaseReservation(actor);
        BuildableObject target = currentVisitTarget;
        currentVisitTarget = null;
        if (target != null && actor != null)
        {
            target.ReleaseTransientCharacterOwnership(
                actor.BuildingVisitor,
                reason);
        }
        if (LastVisitOutcome == ShoppingVisitOutcome.InProgress)
        {
            LastVisitOutcome = ShoppingVisitOutcome.Abandoned;
        }
    }

    private void Update()
    {
        // A nested shop iterator can terminate with an exception while this
        // ability still owns the action. Retry only the exact cancelled cleanup
        // owner, never the rejected checkout or the entire shopping interaction.
        AIAction action = activeShoppingAction;
        if (!shoppingExecutionActive || gameClock == null || gameClock.IsPaused
            || gameClock.Time < nextFailedCleanupRetryAt || actor?.Brain == null
            || !ReferenceEquals(actor.Brain.bestAction, action) || action?.destination is not Shop shop)
            return;
        ShopUseExecutionState pending = shop.FindActiveShopUse(actor.Identity.PersistentId);
        if (pending?.IsFailedServiceCompletion != true
            || !shop.CustomerServiceSessionRuntime.TryGetSessionSnapshot(pending.serviceSessionId, out var session)
            || !pending.MatchesCancelledSession(session, shop.RequirePersistentInstanceId().Value))
            return;
        if (shoppingRoutine != null) StopCoroutine(shoppingRoutine);
        shoppingRoutine = null;
        nextFailedCleanupRetryAt = gameClock.Time + 1f;
        try { shop.ResumeFailedServiceCleanup(actor.BuildingVisitor, pending); }
        catch (Exception failure)
        {
            Debug.LogException(new InvalidOperationException(
                "Shop failed cleanup remains pending: " + pending.operationId, failure), this);
            return;
        }
        SetVisitOutcome(shop, ShoppingVisitOutcome.Failed);
        action.ReleaseReservation(actor);
        CompleteCurrentShoppingAction(action, clearFailures: false,
            terminalKind: CharacterAiActionTerminalKind.Failed);
        FinishShoppingRoutine(action);
    }

    private void FinishShoppingRoutine(AIAction expectedAction)
    {
        if (!ReferenceEquals(activeShoppingAction, expectedAction))
        {
            return;
        }

        shoppingRoutine = null;
        shoppingExecutionActive = false;
        activeShoppingAction = null;
        nextFailedCleanupRetryAt = 0f;
        UnsubscribeFromActiveTargetDestruction();
        currentVisitTarget = null;
    }

    private void SubscribeToActiveTargetDestruction(AIAction action)
    {
        UnsubscribeFromActiveTargetDestruction();
        BuildableObject target = action?.destination;
        if (target == null)
        {
            return;
        }

        activeShoppingTarget = target;
        activeShoppingTargetDestroyedHandler = () =>
        {
            AIBrain brain = actor != null ? actor.Brain : null;
            AIAction expectedAction = activeShoppingAction;
            BuildableObject destroyedTarget = activeShoppingTarget;
            if (brain == null
                || expectedAction == null
                || !ReferenceEquals(brain.bestAction, expectedAction))
            {
                return;
            }

            // The target is still alive during its destruction notification.
            // Stop and release the visit now, before facility state is removed;
            // EndExpectedAction only notifies the executor and is not an interrupt.
            StopShopping("shopping-target-destroyed");
            brain.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.Destroyed,
                    "Facility destination was destroyed during the active visit action.",
                    destroyedTarget),
                requestImmediateReplan: false);
            brain.EndExpectedAction(
                expectedAction,
                CharacterAiActionTerminalKind.Failed,
                clearFailures: false);
        };
        target.OnBuildingDestroyed += activeShoppingTargetDestroyedHandler;
    }

    private void UnsubscribeFromActiveTargetDestruction()
    {
        if (!ReferenceEquals(activeShoppingTarget, null)
            && activeShoppingTargetDestroyedHandler != null)
        {
            activeShoppingTarget.OnBuildingDestroyed -=
                activeShoppingTargetDestroyedHandler;
        }
        activeShoppingTarget = null;
        activeShoppingTargetDestroyedHandler = null;
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            StopShopping("shopping-ability-disabled");
        }
    }

    private void FailCurrentShoppingAction(
        AIAction expectedAction,
        AIActionFailure failure)
    {
        AIBrain brain = actor != null ? actor.Brain : null;
        if (brain == null
            || expectedAction == null
            || !ReferenceEquals(brain.bestAction, expectedAction))
        {
            return;
        }

        expectedAction.ReleaseReservation(actor);
        brain.ReportRuntimeActionFailure(
            failure,
            requestImmediateReplan: false);
        brain.EndExpectedAction(
            expectedAction,
            CharacterAiActionTerminalKind.Failed,
            clearFailures: false);
    }

    private void CompleteCurrentShoppingAction(
        AIAction expectedAction,
        bool clearFailures,
        CharacterAiActionTerminalKind terminalKind = CharacterAiActionTerminalKind.Cancelled)
    {
        AIBrain brain = actor != null ? actor.Brain : null;
        if (brain == null
            || (expectedAction != null && brain.bestAction != expectedAction))
        {
            return;
        }

        brain.EndExpectedAction(expectedAction, terminalKind, clearFailures);
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

    public void RestorePersistentState(
        int savedVisitCount, int savedLookAroundCount, int savedHoldingMoney,
        IReadOnlyList<RetailPurchaseRecoverySaveData> savedPendingPurchases = null)
    {
        RequireRetailIdle();
        var restoredPurchases = new List<RetailPurchaseRecoverySaveData>();
        foreach (var source in savedPendingPurchases ?? Array.Empty<RetailPurchaseRecoverySaveData>())
        {
            source?.Validate();
            if (source == null || source.buyerId != actor?.Identity?.PersistentId
                || restoredPurchases.Any(value => value.operationId == source.operationId))
                throw new InvalidOperationException("Character retail purchase recovery payload is invalid or duplicated.");
            restoredPurchases.Add(source.Clone());
        }
        // Validate the complete candidate before replacing any live owner state.
        visitCount = Mathf.Max(0, savedVisitCount);
        lookAroundCount = Mathf.Max(0, savedLookAroundCount);
        holdingMoney = Mathf.Max(0, savedHoldingMoney);
        spendingProjectionApplied = true;
        mutableVisitedBuildings.Clear();
        pendingRetailPurchases.Clear();
        pendingRetailPurchases.AddRange(restoredPurchases);
    }

    public IReadOnlyList<RetailPurchaseRecoverySaveData> CapturePendingRetailPurchases()
    {
        RequireRetailIdle();
        foreach (var row in pendingRetailPurchases) row.Validate();
        return pendingRetailPurchases.OrderBy(value => value.operationId, StringComparer.Ordinal)
            .Select(value => value.Clone()).ToArray();
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
        GetDecisionState(out bool canLookAround, out _);
        return canLookAround;
    }

    public bool ShouldExitDungeon()
    {
        GetDecisionState(out _, out bool shouldExitDungeon);
        return shouldExitDungeon;
    }

    public void GetDecisionState(
        out bool canLookAround,
        out bool shouldExitDungeon)
    {
        float now = RequireGameClock().Time;
        float cacheSeconds = actor?.NaturalnessSettings?.SignalCacheSeconds ?? 0f;
        int facilityVersion = actor?.Brain?.RequireFacilityCandidateCache()
            .DynamicStateVersion ?? int.MinValue;
        if (now - decisionStateCapturedAt <= cacheSeconds
            && decisionStateVisitCount == visitCount
            && decisionStateLookAroundCount == lookAroundCount
            && decisionStateFacilityVersion == facilityVersion)
        {
            canLookAround = cachedCanLookAround;
            shouldExitDungeon = cachedShouldExitDungeon;
            return;
        }

        bool hasVisitableBuilding = actor != null
            && visitCount > 0
            && IsThereVisitableBuilding();
        CacheDecisionState(now, facilityVersion, hasVisitableBuilding);
        canLookAround = cachedCanLookAround;
        shouldExitDungeon = cachedShouldExitDungeon;
    }

    public void RecordVisitableFacilitySearchResult(bool hasCandidate)
    {
        float now = RequireGameClock().Time;
        int facilityVersion = actor?.Brain?.RequireFacilityCandidateCache()
            .DynamicStateVersion ?? int.MinValue;
        CacheDecisionState(now, facilityVersion, hasCandidate);
    }

    private void CacheDecisionState(
        float now,
        int facilityVersion,
        bool hasVisitableBuilding)
    {
        bool shouldEndVisitCycle = visitCount <= 0 || !hasVisitableBuilding;
        cachedCanLookAround = shouldEndVisitCycle
            && visitCount > 0
            && lookAroundCount < DefaultMaxLookAroundCount;
        cachedShouldExitDungeon = shouldEndVisitCycle
            && (visitCount <= 0
                || lookAroundCount >= DefaultMaxLookAroundCount);
        decisionStateCapturedAt = now;
        decisionStateVisitCount = visitCount;
        decisionStateLookAroundCount = lookAroundCount;
        decisionStateFacilityVersion = facilityVersion;
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

        AIBrain brain = actor.Brain;
        if (brain == null || !brain.TryGetRuntimeGrid(out _))
        {
            return false;
        }

        canVisitBuildingPredicate ??= CanVisitCandidate;
        return FacilityCandidateScorer.HasCandidate(
            actor,
            null,
            GetInterestRoles(),
            canVisitBuildingPredicate);
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

        foreach (BuildableObject building in FacilityCandidateScorer.GetCandidates(
                     actor,
                     null,
                     GetInterestRoles()))
        {
            if (!CanVisitCandidate(building))
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

    public bool CanVisitCandidate(BuildableObject building)
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
    public IEnumerator BuyItem(
        RemainStock item,
        int purchaseCost,
        AIAction expectedAction,
        BuildableObject expectedFacility,
        BuildingRetailPurchaseCommitResult commitResult)
    {
        if (commitResult == null)
        {
            throw new ArgumentNullException(nameof(commitResult));
        }

        if (expectedFacility is Shop recoveryShop
            && !TryRecoverPendingRetailPurchases(
                recoveryShop,
                out string recoveryFailure))
        {
            throw new InvalidOperationException(
                "Retail purchase recovery is still pending: "
                + recoveryFailure);
        }

        ShopUseCartLine executionLine = null;
        if (!string.IsNullOrEmpty(commitResult.ShopUseOperationId))
        {
            if (expectedFacility is not Shop executionShop)
                throw new InvalidOperationException("Bound retail purchase requires its shop.");
            executionLine = executionShop.RequirePurchaseLine(commitResult.ShopUseOperationId,
                commitResult.ShopUseCartIndex, actor.Identity.PersistentId);
            if (!executionLine.pricingCaptured || executionLine.price != purchaseCost
                || executionLine.workerId != commitResult.PricingContext.WorkerId
                || executionLine.priceMultiplier != commitResult.PricingContext.PriceMultiplier
                || executionLine.modifierProvenance != commitResult.PricingContext.SkillModifierProvenance)
                throw new InvalidOperationException("Retail purchase price differs from its saved cart line.");
            if (executionLine.Committed)
            {
                commitResult.Commit(executionLine.committedLot);
                yield break;
            }
        }
        EnsureSpendingProjection();
        IShopStockCatalog catalog = shopStockCatalog
            ?? throw new InvalidOperationException($"{nameof(AbilityShopping)} requires {nameof(IShopStockCatalog)} injection.");
        if ((executionLine == null || !executionLine.feedbackStarted)
            && item != null && catalog.TryGetSaleItem(item.id, out SaleItem iteminfo))
        {
            RequireFloatingIconFeedbackService().Show(this, iteminfo.itemSprite, purchaseFeedbackIconMaxWorldSize);
        }

        if (executionLine == null)
            yield return PurchaseFeedbackDelay;
        else
        {
            if (!executionLine.feedbackStarted)
            {
                executionLine.feedbackStarted = true;
                executionLine.feedbackRemainingSeconds = 0.5f;
            }
            while (executionLine.feedbackRemainingSeconds > 0f)
            {
                if (!CanCommitShoppingTransaction(expectedAction, expectedFacility)) break;
                executionLine.feedbackRemainingSeconds = Mathf.Max(0f,
                    executionLine.feedbackRemainingSeconds - ((Shop)expectedFacility).CustomerGameDeltaTime);
                yield return null;
            }
        }
        if (!CanCommitShoppingTransaction(expectedAction, expectedFacility))
        {
            commitResult.Reject("shopping-action-no-longer-authoritative");
            yield break;
        }

        if (item == null || item.stock <= 0)
        {
            commitResult.Reject("shop-stock-depleted-before-commit");
            yield break;
        }

        int normalizedCost = Mathf.Max(0, purchaseCost);
        if (!IsInternalStaffUse() && holdingMoney < normalizedCost)
        {
            commitResult.Reject("shopping-funds-changed-before-commit");
            yield break;
        }

        using IDisposable mutation = EnterRetailMutation();
        if (purchaseFacts == null || purchaseRecovery == null
            || gameDataProvider == null
            || !gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            || gameData?.day == null)
        {
            throw new InvalidOperationException(
                "Retail purchase fact publication or current day is unavailable.");
        }
        if (expectedFacility is not Shop shop
            || !shop.TryPreviewExactRetailLotOperationId(
                item.id,
                out string previewOperationId))
        {
            commitResult.Reject("retail-lot-unavailable-before-outcome-reservation");
            yield break;
        }

        string actorId = actor?.Identity?.PersistentId ?? string.Empty;
        int absoluteDay = Mathf.Max(0, gameData.day.Value);
        if (actorId.Length == 0)
        {
            throw new InvalidOperationException(
                "Retail purchase requires a persistent buyer identity.");
        }

        string lotFailure = "retail-lot-unavailable-before-commit";
        RetailStockLotSnapshot purchasedLot;
        string unitOperationId;
        bool lotTaken;
        lotTaken = shop.TryTakeExactRetailLot(
            item.id,
            out purchasedLot,
            out unitOperationId,
            out lotFailure);
        if (!lotTaken)
        {
            commitResult.Reject(string.IsNullOrWhiteSpace(lotFailure)
                ? "retail-lot-unavailable-before-commit"
                : lotFailure);
            yield break;
        }

        if (!string.Equals(
                previewOperationId,
                unitOperationId,
                StringComparison.Ordinal))
        {
            RestoreRetailLotOrThrow(shop, purchasedLot, "retail-operation-drift");
            throw new InvalidOperationException(
                "Retail purchase operation identity drifted before mutation.");
        }

        int holdingMoneyBefore = holdingMoney;
        int purchaseCountBefore = committedPurchaseCount;
        long purchaseMassBefore = committedPurchaseMassGrams;
        RetailStockLotSnapshot lastLotBefore = lastCommittedPurchaseLot?.Clone();
        RetailPurchaseRecoverySaveData recovery = null;
        try
        {
            int nextPurchaseCount = checked(committedPurchaseCount + 1);
            long nextPurchaseMass = checked(
                committedPurchaseMassGrams
                + purchasedLot.unitMassGrams * purchasedLot.quantity);
            if (!IsInternalStaffUse())
            {
                holdingMoney -= normalizedCost;
            }
            committedPurchaseCount = nextPurchaseCount;
            committedPurchaseMassGrams = nextPurchaseMass;
            lastCommittedPurchaseLot = purchasedLot.Clone();

            recovery = new RetailPurchaseRecoverySaveData
            {
                shopUseOperationId = commitResult.ShopUseOperationId,
                shopUseCartIndex = commitResult.ShopUseCartIndex,
                operationId = unitOperationId,
                buyerId = actorId,
                facilityId = shop.RequirePersistentInstanceId().Value,
                absoluteDay = absoluteDay,
                purchaseCost = IsInternalStaffUse() ? 0 : normalizedCost,
                buyerMoneyBefore = holdingMoneyBefore,
                buyerMoneyAfter = holdingMoney,
                revenueWorkerId = commitResult.PricingContext.WorkerId,
                revenuePriceMultiplier =
                    commitResult.PricingContext.PriceMultiplier,
                revenueSkillModifierProvenance =
                    commitResult.PricingContext.SkillModifierProvenance,
                phase = RetailPurchaseRecoveryPhase.LotTaken,
                lot = purchasedLot.Clone()
            };
            pendingRetailPurchases.Add(recovery);
            if (executionLine != null) executionLine.pendingPurchaseOperationId = unitOperationId;
        }
        catch
        {
            if (recovery == null)
            {
                RestoreRetailPurchaseOwnerState(
                    shop,
                    purchasedLot,
                    holdingMoneyBefore,
                    purchaseCountBefore,
                    purchaseMassBefore,
                    lastLotBefore);
            }
            throw;
        }

        if (!TryAdvanceRetailPurchase(
                shop,
                recovery,
                out string advanceFailure))
        {
            throw new InvalidOperationException(
                "Retail purchase is durably pending recovery at phase '"
                + recovery.phase + "': " + advanceFailure);
        }

        CompleteRetailPurchase(shop, recovery, item.onbuy);
        commitResult.Commit(purchasedLot);
    }

    private void CompleteRetailPurchase(Shop shop, RetailPurchaseRecoverySaveData recovery, OnBuyItemSO[] onBuy)
    {
        shop.ValidatePurchaseBinding(recovery);
        foreach(var events in onBuy ?? Array.Empty<OnBuyItemSO>())
        {
            try
            {
                events?.Onbuy(actor?.BuildingVisitor);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                && exception is not StackOverflowException
                && exception is not AccessViolationException)
            {
                Debug.LogError(
                    "retail-purchase-post-commit-onbuy:"
                    + exception.GetType().Name);
            }
        }
        shop.RecordPurchaseBinding(recovery);
        pendingRetailPurchases.Remove(recovery);
    }

    public bool TryRecoverPendingRetailPurchases(
        Shop shop,
        out string failureReason)
    {
        if (retailTransactionActive)
        { failureReason = "retail-local-transaction-active"; return false; }
        using IDisposable mutation = EnterRetailMutation();
        return RecoverPendingRetailPurchasesCore(shop, out failureReason);
    }

    private bool RecoverPendingRetailPurchasesCore(Shop shop, out string failureReason)
    {
        failureReason = string.Empty;
        if (shop == null)
        {
            failureReason = "retail-recovery-shop-missing";
            return false;
        }
        string facilityId = shop.RequirePersistentInstanceId().Value;
        RetailPurchaseRecoverySaveData[] pending = pendingRetailPurchases
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .ToArray();
        foreach (RetailPurchaseRecoverySaveData recovery in pending)
        {
            if (!string.Equals(
                    recovery.facilityId,
                    facilityId,
                    StringComparison.Ordinal))
            {
                failureReason = "retail-recovery-awaiting-facility:"
                    + recovery.facilityId;
                return false;
            }
            if (!TryAdvanceRetailPurchase(
                    shop,
                    recovery,
                    out failureReason))
            {
                return false;
            }
            if (!shop.TryGetSaleItem(recovery.lot.saleItemId, out SaleItem definition) || definition == null)
                throw new InvalidOperationException("Recovered retail purchase has no authored product callbacks.");
            CompleteRetailPurchase(shop, recovery, definition.buyevent);
        }
        return true;
    }

    private bool TryAdvanceRetailPurchase(
        Shop shop,
        RetailPurchaseRecoverySaveData recovery,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsValidPendingRetailPurchase(recovery)
            || shop == null
            || !string.Equals(
                recovery.facilityId,
                shop.RequirePersistentInstanceId().Value,
                StringComparison.Ordinal))
        {
            failureReason = "retail-recovery-contract-invalid";
            return false;
        }

        shop.ValidatePurchaseBinding(recovery);
        if (recovery.phase == RetailPurchaseRecoveryPhase.LotTaken)
        {
            if (!shop.TryCommitExactRetailExternalSink(
                    recovery.lot,
                    out failureReason))
            {
                return false;
            }
            recovery.phase = RetailPurchaseRecoveryPhase.PhysicalCommitted;
        }

        if (recovery.phase == RetailPurchaseRecoveryPhase.PhysicalCommitted)
        {
            if (!TryObserveRetailPurchase(recovery, out failureReason)) return false;
            recovery.phase = RetailPurchaseRecoveryPhase.OutcomeCommitted;
        }

        if (recovery.phase == RetailPurchaseRecoveryPhase.OutcomeCommitted)
        {
            if (!shop.TryCommitExactRetailRevenue(
                    recovery.operationId,
                    recovery.buyerId,
                    recovery.purchaseCost,
                    out failureReason))
            {
                return false;
            }
            recovery.phase = RetailPurchaseRecoveryPhase.RevenueCommitted;
        }

        return recovery.phase == RetailPurchaseRecoveryPhase.RevenueCommitted;
    }

    private bool TryObserveRetailPurchase(RetailPurchaseRecoverySaveData row, out string failure)
    {
        failure = string.Empty;
        if (purchaseFacts == null || purchaseRecovery == null)
            throw new InvalidOperationException("Retail purchase fact publication is not injected.");
        var fact = new RetailPurchaseFact(row);
        DomainFactDelivery delivery;
        if (row.publicationKey.Length > 0)
        {
            var results = new DomainFactDelivery[1];
            try
            {
                if (!purchaseRecovery.ReplayBatch(
                    new[] { DomainFactReplay.Create(row.publicationKey, fact) }, results, out failure))
                    return false;
                delivery = results[0];
            }
            catch (Exception error) when (RecoverableRetailObservation(error))
            { failure = "retail-observation-recovery-unknown:" + error.Message; return false; }
        }
        else
        {
            if (!purchaseFacts.TryReserve(fact, out DomainFactLease lease, out failure)) return false;
            try
            {
                if (!lease.IsValid || !RetailPurchaseRecoverySaveData.IsPublicationKey(lease.PublicationKey))
                { failure = "retail-observation-reservation-invalid"; return false; }
                row.publicationKey = lease.PublicationKey;
                try { delivery = purchaseFacts.Publish(lease, fact); }
                catch (Exception error) when (RecoverableRetailObservation(error))
                { delivery = DomainFactDelivery.Uncertain("retail-observation-response-unknown:" + error.Message); }
                // A known rejection did not commit. Unknown ACKs retain their
                // original key and frozen owner facts for exact saved replay.
                if (!delivery.MayBeCommitted) row.publicationKey = string.Empty;
            }
            finally { purchaseFacts.Cancel(lease); }
        }
        if (delivery.Phase is not (DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
            || delivery.OperationKey != row.publicationKey
            || !RetailPurchaseRecoverySaveData.IsPublicationKey(delivery.PublicationId)
            || !RetailPurchaseRecoverySaveData.IsFingerprint(delivery.Fingerprint))
        {
            failure = "retail-observation-pending:" + delivery.Detail;
            return false;
        }
        row.publicationId = delivery.PublicationId;
        row.publicationFingerprint = delivery.Fingerprint;
        return true;
    }

    private static bool RecoverableRetailObservation(Exception error) => error is not OutOfMemoryException
        && error is not StackOverflowException && error is not AccessViolationException;

    private static bool IsValidPendingRetailPurchase(RetailPurchaseRecoverySaveData row)
    {
        if (row == null) return false;
        try { row.Validate(); return true; }
        catch (ArgumentException) { return false; }
    }

    private void RequireRetailIdle()
    {
        if (retailTransactionActive) throw new InvalidOperationException("retail-local-transaction-active");
    }

    private IDisposable EnterRetailMutation()
    {
        RequireRetailIdle();
        retailTransactionActive = true;
        return new RetailMutation(this);
    }

    private sealed class RetailMutation : IDisposable
    {
        private AbilityShopping owner;
        public RetailMutation(AbilityShopping owner) => this.owner = owner;
        public void Dispose()
        {
            if (owner == null) return;
            owner.retailTransactionActive = false;
            owner = null;
        }
    }

    private void RestoreRetailPurchaseOwnerState(
        Shop shop,
        RetailStockLotSnapshot purchasedLot,
        int holdingMoneyBefore,
        int purchaseCountBefore,
        long purchaseMassBefore,
        RetailStockLotSnapshot lastLotBefore)
    {
        holdingMoney = holdingMoneyBefore;
        committedPurchaseCount = purchaseCountBefore;
        committedPurchaseMassGrams = purchaseMassBefore;
        lastCommittedPurchaseLot = lastLotBefore?.Clone();
        RestoreRetailLotOrThrow(
            shop,
            purchasedLot,
            "retail-outcome-rollback");
    }

    private static void RestoreRetailLotOrThrow(
        Shop shop,
        RetailStockLotSnapshot purchasedLot,
        string reason)
    {
        if (!shop.TryRestoreTakenExactRetailLot(
                purchasedLot,
                out string restoreFailure))
        {
            throw new InvalidOperationException(
                "Retail purchase rollback '" + reason
                + "' could not restore exact lot '"
                + purchasedLot?.sourceOperationId + "': " + restoreFailure);
        }
    }

    private bool CanCommitShoppingTransaction(
        AIAction expectedAction,
        BuildableObject expectedFacility)
    {
        return actor != null
            && actor.CanRunAi
            && actor.Brain != null
            && ReferenceEquals(actor.Brain.bestAction, expectedAction)
            && !actor.Brain.isBestActionEnd
            && expectedFacility != null
            && !expectedFacility.isDestroy;
    }

    private void EnsureSpendingProjection()
    {
        if (spendingProjectionApplied)
        {
            return;
        }

        float multiplier = actor?.Stats?.GetSpendingMultiplier()
            ?? throw new InvalidOperationException(
                "Shopping spending projection requires a live character stats authority.");
        holdingMoney = Mathf.Max(
            0,
            Mathf.RoundToInt(holdingMoney * multiplier));
        spendingProjectionApplied = true;
    }

    public bool TryStealLooseItemBeforeExit()
    {
        if (attemptedLooseItemTheftBeforeExit
            || actor == null
            || IsInternalStaffUse()
            || actor.characterType != CharacterType.Customer)
        {
            return false;
        }

        attemptedLooseItemTheftBeforeExit = true;
        ICharacterVisitorExitOutcomeRuntime exitOutcomes = visitorExitOutcomes
            ?? throw new InvalidOperationException(
                $"{nameof(AbilityShopping)} requires "
                + $"{nameof(ICharacterVisitorExitOutcomeRuntime)} injection.");
        if (!exitOutcomes.BeginExit(
                actor,
                "ai.exit-dungeon",
                out _)
            || !exitOutcomes.TryStealLooseItemBeforeExit(
                actor,
                4,
                out WorldItemStackSnapshot stolen,
                out _))
        {
            return false;
        }

        gameEventBus.Publish(new ShoppingActionObservedEvent(actor, ShoppingActionTransition.LooseItemStolen,
            itemStackId: stolen?.StackId ?? string.Empty, itemDisplayName: stolen?.DisplayName ?? string.Empty,
            itemValue: stolen?.TotalValue ?? 0));
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
        CharacterActor actor = GetComponent<CharacterActor>();
        string actorId = CharacterPersistentIdentity.Require(actor).Value;
        return provider.Get($"shopping:{actorId}");
    }

    private IGameClock RequireGameClock()
    {
        return gameClock
            ?? throw new InvalidOperationException(
                $"{nameof(AbilityShopping)} requires {nameof(IGameClock)} injection.");
    }
}
