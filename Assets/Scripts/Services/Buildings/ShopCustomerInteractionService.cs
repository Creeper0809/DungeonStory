using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class ShopCustomerInteractionService
{
    private const float CheckoutWaitPollSeconds = 0.2f;
    private const float SelfServiceCheckoutSeconds = 0.8f;
    private const float StaffedCheckoutSeconds = 0.35f;

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

    private readonly Shop owner;
    private readonly Func<Vector3> worldPositionProvider;
    private int waitingCheckoutCount;
    private bool checkoutServiceAlertRaised;
    private float nextCheckoutAbandonAlertTime;

    internal ShopCustomerInteractionService(
        Shop owner,
        Func<Vector3> worldPositionProvider)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.worldPositionProvider = worldPositionProvider
            ?? throw new ArgumentNullException(nameof(worldPositionProvider));
    }

    internal int WaitingCheckoutCount => waitingCheckoutCount;
    internal bool HasWaitingCheckout => waitingCheckoutCount > 0;

    internal IEnumerator Interact(IBuildingVisitorPort actor)
    {
        owner.EnsureCustomerStockInitialized();
        if (!owner.TryBeginUse(actor, out string failureReason))
        {
            actor?.Shopping?.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
            actor?.RecordActivity(owner, new BuildingActivitySnapshot(
                BuildingActivityKinds.Shopping,
                BuildingActivityOutcomes.Failed,
                $"{owner.CustomerDisplayName} 이용 실패: {failureReason}",
                reasonCode: failureReason,
                bubbleEligible: true));
            yield break;
        }

        IBuildingShoppingVisitorPort shopable = actor?.Shopping;
        if (shopable == null || actor == null || !actor.VisitorSnapshot.CanMove)
        {
            shopable?.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
            owner.EndUse(actor);
            yield break;
        }

        ServiceSessionSnapshot serviceSession = null;
        BuildingServiceHubAbility serviceHub = owner.GetServiceHubAbility();
        if (serviceHub != null
            && owner.CustomerServiceSessionRuntime != null
            && !owner.CustomerServiceSessionRuntime.TryBeginSession(
                new ServiceSessionRequest
                {
                    Hub = owner,
                    Actor = actor,
                    ProcessId = serviceHub.supportedProcessIds?
                        .FirstOrDefault() ?? string.Empty,
                    IsInternalActor = Shop.IsInternalStaffUse(actor),
                    AdvertisedDemand = !Shop.IsInternalStaffUse(actor)
                },
                out serviceSession,
                out DomainFailure serviceFailure))
        {
            string serviceFailureCode = serviceFailure.Code.ToString();
            shopable.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
            actor?.RecordActivity(owner, new BuildingActivitySnapshot(
                BuildingActivityKinds.Shopping,
                BuildingActivityOutcomes.Failed,
                $"{owner.CustomerDisplayName} 이용 실패",
                reasonCode: serviceFailureCode,
                bubbleEligible: true));
            owner.EndUse(actor);
            yield break;
        }

        object currentAction = actor.CurrentActionToken;
        if (owner.Facility != null && owner.Facility.SupportsRole(FacilityRole.Meal))
        {
            if (serviceSession != null)
            {
                owner.CustomerServiceSessionRuntime.TrySetStage(
                    serviceSession.SessionId,
                    ServiceSessionStage.Service,
                    out _);
            }
            yield return RunPhysicalMealService(
                actor,
                shopable,
                currentAction);
            owner.CustomerServiceCompletion.Finish(
                actor,
                serviceSession?.SessionId,
                shopable.LastVisitOutcome == BuildingVisitOutcome.Completed,
                "식사 서비스가 완료되지 않았습니다.",
                owner.CustomerServiceSessionCompletion);
            yield break;
        }

        int howmany = shopable.GetShoppingCount();
        if (Shop.IsInternalStaffUse(actor)
            && owner.Facility != null
            && owner.Facility.SupportsRole(FacilityRole.Meal))
        {
            howmany = 1;
        }
        ShoppingSelectionBuffer selection = AcquireShoppingSelectionBuffer();
        Dictionary<int, int> selectedCounts = selection.SelectedCounts;
        List<RemainStock> cart = selection.Cart;
        bool createsRevenue = Shop.CreatesRevenueFor(actor);
        for (int i = 0; i < howmany; i++)
        {
            int selectedItemId = shopable.SelectOffer(
                CreateOfferSnapshots(owner.GetCustomerPurchasableStock(selectedCounts)));
            actor.SetActionPhase("\uC0C1\uD488 \uB458\uB7EC\uBCF4\uAE30", owner, $"{i + 1}/{howmany}");
            yield return actor.MoveTo(
                owner.GetFacilityAnchorWorldPosition(
                    FacilityAnchorPurposeIds.Use,
                    actor.VisitorSnapshot.Position),
                0.7f,
                currentAction);
            yield return Linger(actor, 0.1f, currentAction);
            if (selectedItemId == -1) continue;

            RemainStock remainStock = owner.CustomerInventory.Stocks.FirstOrDefault(
                stock => stock.id == selectedItemId);
            if (remainStock == null
                || ShopInventoryRuntime.GetRemainingStockAfterSelection(
                    remainStock,
                    selectedCounts) <= 0)
            {
                continue;
            }

            cart.Add(remainStock);
            selectedCounts.TryGetValue(remainStock.id, out int selectedCount);
            selectedCounts[remainStock.id] = selectedCount + 1;
        }

        if (cart.Count == 0)
        {
            ReleaseShoppingSelectionBuffer(selection);
            shopable.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
            actor.RecordActivity(owner, new BuildingActivitySnapshot(
                BuildingActivityKinds.Shopping,
                BuildingActivityOutcomes.Failed,
                $"{owner.CustomerDisplayName} 이용 실패: 구매 가능한 상품 없음",
                reasonCode: "no-purchasable-item",
                bubbleEligible: true));
            owner.CustomerServiceCompletion.Finish(
                actor,
                serviceSession?.SessionId,
                false,
                "구매 가능한 상품이 없습니다.",
                owner.CustomerServiceSessionCompletion);
            yield break;
        }

        Vector2 endPos = owner.GetFacilityAnchorWorldPosition(
            FacilityAnchorPurposeIds.Checkout,
            actor.VisitorSnapshot.Position);
        actor.SetActionPhase("\uACC4\uC0B0\uB300 \uC774\uB3D9", owner);
        yield return actor.MoveTo(endPos, 1f, currentAction);
        CheckoutWaitSession checkoutWaitSession = new CheckoutWaitSession();
        bool requiresManagedAttendant = serviceSession == null
            ? owner.RequiresCustomerServingWorker()
            : serviceSession.Contract.mode == ServiceOperationMode.Managed
                && owner.RequiresCustomerServingWorker();
        if (requiresManagedAttendant)
        {
            owner.CustomerServiceSessionRuntime?.TrySetStage(
                serviceSession?.SessionId,
                ServiceSessionStage.Waiting,
                out _);
            yield return WaitForServingWorkerWithPatience(
                actor,
                checkoutWaitSession);
        }
        if (checkoutWaitSession.Abandoned
            || shopable.LastVisitOutcome == BuildingVisitOutcome.Abandoned)
        {
            ReleaseShoppingSelectionBuffer(selection);
            owner.CustomerServiceCompletion.Finish(
                actor,
                serviceSession?.SessionId,
                false,
                "대기 시간이 길어 손님이 떠났습니다.",
                owner.CustomerServiceSessionCompletion);
            yield break;
        }
        if (requiresManagedAttendant
            && !owner.CanServeCustomer(actor, out string serviceFailureReason))
        {
            ReleaseShoppingSelectionBuffer(selection);
            shopable.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
            actor.RecordActivity(owner, new BuildingActivitySnapshot(
                BuildingActivityKinds.Shopping,
                BuildingActivityOutcomes.Cancelled,
                $"{owner.CustomerDisplayName} 계산 대기 중단: {serviceFailureReason}",
                reasonCode: serviceFailureReason,
                bubbleEligible: true));
            owner.CustomerServiceCompletion.Finish(
                actor,
                serviceSession?.SessionId,
                false,
                serviceFailureReason,
                owner.CustomerServiceSessionCompletion);
            yield break;
        }

        owner.CustomerServiceSessionRuntime?.TrySetStage(
            serviceSession?.SessionId,
            ServiceSessionStage.Service,
            out _);
        yield return RunCheckoutService(actor);
        if (owner.TryResolveCheckoutCrime(actor, cart))
        {
            ReleaseShoppingSelectionBuffer(selection);
            shopable.SetVisitOutcome(owner, BuildingVisitOutcome.Completed);
            owner.CustomerServiceCompletion.Finish(
                actor,
                serviceSession?.SessionId,
                true,
                string.Empty,
                owner.CustomerServiceSessionCompletion);
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

            Stock pricedStock = owner.CustomerInventory.CreatePricedStock(
                remainStock,
                owner.CurrentPriceMultiplier);
            if (!shopable.CanPay(pricedStock.cost))
            {
                continue;
            }

            yield return shopable.Purchase(remainStock, pricedStock.cost);
            purchaseCount++;
            owner.PublishCustomerGameEvent(new FacilityStockConsumedEvent(
                actor,
                owner,
                owner.CustomerInventory.GetStockCategory(remainStock.id),
                1));
            if (createsRevenue)
            {
                usedMoney += pricedStock.cost;
            }
            remainStock.stock--;
            owner.MarkCustomerFacilityStateDirty();
        }

        if (purchaseCount == 0)
        {
            ReleaseShoppingSelectionBuffer(selection);
            shopable.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
            actor.RecordActivity(owner, new BuildingActivitySnapshot(
                BuildingActivityKinds.Shopping,
                BuildingActivityOutcomes.Failed,
                $"{owner.CustomerDisplayName} 이용 실패: 구매 가능한 상품 없음",
                reasonCode: "no-purchasable-item",
                bubbleEligible: true));
            owner.CustomerServiceCompletion.Finish(
                actor,
                serviceSession?.SessionId,
                false,
                "결제 가능한 상품이 없습니다.",
                owner.CustomerServiceSessionCompletion);
            yield break;
        }

        actor?.ApplyMoodFactor(
            $"shopping:{owner.RequirePersistentInstanceId().Value}",
            "마음에 드는 물건을 삼",
            5f,
            120f,
            2);

        if (createsRevenue && usedMoney > 0)
        {
            usedMoney = Mathf.Max(1, Mathf.RoundToInt(
                usedMoney * (owner.ServingWorker?.VisitorSnapshot.RevenueMultiplier ?? 1f)));
            owner.RequireCustomerFloatingNumberFeedbackService().TryShow(NumberCondition.ONEARNMONEY, endPos + Vector2.up, usedMoney);
        }

        if (createsRevenue && usedMoney > 0)
        {
            owner.CustomerMoneyAccount.Add(
                usedMoney,
                new EconomyTransactionContext(
                    EconomyTransactionKind.GuestServiceIncome,
                    owner.RequirePersistentInstanceId().Value,
                    actor.VisitorSnapshot.PersistentId,
                    "손님 물품 판매"));
        }

        if (createsRevenue && usedMoney > 0)
        {
            owner.PublishCustomerGameEvent(new FacilityRevenueEvent(actor, owner, usedMoney));
        }

        if (!createsRevenue)
        {
            actor.RecordActivity(owner, new BuildingActivitySnapshot(
                BuildingActivityKinds.Shopping,
                BuildingActivityOutcomes.Completed,
                $"{owner.CustomerDisplayName} 직원 이용: 매출 제외",
                reasonCode: "staff-no-revenue"));
        }

        actor.ApplyRoomExperience(owner.CustomerRoomEnvironmentExperienceService, owner, "shopping");
        shopable.SetVisitOutcome(owner, BuildingVisitOutcome.Completed);
        ReleaseShoppingSelectionBuffer(selection);
        yield return PurchaseCompleteDelay;
        owner.CustomerServiceCompletion.Finish(
            actor,
            serviceSession?.SessionId,
            true,
            string.Empty,
            owner.CustomerServiceSessionCompletion);
    }

    private IEnumerator RunPhysicalMealService(
        IBuildingVisitorPort actor,
        IBuildingShoppingVisitorPort shopping,
        object currentAction)
    {
        actor?.SetActionPhase("식사 자리로 이동", owner);
        yield return actor.MoveTo(
            owner.GetFacilityAnchorWorldPosition(
                FacilityAnchorPurposeIds.Use,
                actor.VisitorSnapshot.Position),
            0.7f,
            currentAction);
        yield return Linger(actor, 0.1f, currentAction);

        float duration = owner.Facility != null ? owner.Facility.useDuration : 1f;
        duration *= actor?.VisitorSnapshot.StayDurationMultiplier ?? 1f;

        actor?.SetActionPhase("식사 중", owner, $"{duration:0.#}초");
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        BuildingMealUseSnapshot meal = default;
        if (actor == null
            || !actor.TryConsumeMeal(
                owner.CustomerMealConsumptionRuntime,
                owner,
                out meal))
        {
            string failureCode = meal.FailureCode;
            shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
            actor?.RecordActivity(owner, new BuildingActivitySnapshot(
                BuildingActivityKinds.FacilityUse,
                BuildingActivityOutcomes.Failed,
                failureCode,
                reasonCode: failureCode,
                bubbleEligible: true));
            yield break;
        }

        int revenue = 0;
        if (Shop.CreatesRevenueFor(actor))
        {
            if (shopping.CanPay(meal.UnitPrice))
            {
                yield return shopping.PayForService(meal.UnitPrice);
                revenue = Mathf.Max(0, meal.UnitPrice);
                if (revenue > 0)
                {
                    owner.CustomerMoneyAccount.Add(
                        revenue,
                        new EconomyTransactionContext(
                            EconomyTransactionKind.GuestServiceIncome,
                            owner.RequirePersistentInstanceId().Value,
                            actor.VisitorSnapshot.PersistentId,
                            "손님 식사 매출"));

                    owner.PublishCustomerGameEvent(
                        new FacilityRevenueEvent(actor, owner, revenue));
                    owner.RequireCustomerFloatingNumberFeedbackService().TryShow(
                        NumberCondition.ONEARNMONEY,
                        worldPositionProvider() + Vector3.up,
                        revenue);
                }
            }
            else
            {
                actor?.ApplyMoodFactor(
                    $"meal-debt:{owner.RequirePersistentInstanceId().Value}",
                    "식사 값을 치르지 못함",
                    -5f,
                    180f,
                    1);
                actor?.RecordActivity(owner, new BuildingActivitySnapshot(
                    BuildingActivityKinds.Social,
                    BuildingActivityOutcomes.Damaged,
                    $"{meal.DisplayName} 값을 치르지 못하고 쫓겨남",
                    reasonCode: "meal-unaffordable",
                    bubbleEligible: true));
                owner.CustomerGameEventBus.RaiseAlert(
                    "무전취식",
                    $"{actor?.VisitorSnapshot.DisplayName ?? "손님"}이(가) "
                    + $"{meal.DisplayName} 값을 치르지 못했습니다.",
                    EventAlertImportance.Low,
                    "범죄");
            }
        }

        actor?.ApplyRoomExperience(owner.CustomerRoomEnvironmentExperienceService, owner, "shopping");
        shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Completed);
        actor?.RecordActivity(owner, new BuildingActivitySnapshot(
            BuildingActivityKinds.FacilityUse,
            BuildingActivityOutcomes.Completed,
            $"{meal.DisplayName} 식사 완료",
            value: revenue));
    }

    private IEnumerator RunCheckoutService(IBuildingVisitorPort actor)
    {
        if (!Shop.CreatesRevenueFor(actor))
        {
            yield break;
        }

        bool selfService = !owner.HasServingWorker;
        if (selfService)
        {
            waitingCheckoutCount++;
            owner.MarkCustomerFacilityStateDirty();
            owner.RequireCustomerWorkforceReplanService().RequestIdleWorkersToReplan();
            actor?.RecordActivity(owner, new BuildingActivitySnapshot(
                BuildingActivityKinds.Shopping,
                BuildingActivityOutcomes.Progress,
                $"{owner.CustomerDisplayName} 셀프 계산 중",
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
                owner.MarkCustomerFacilityStateDirty();
            }
        }
    }

    private IEnumerator WaitForServingWorkerWithPatience(
        IBuildingVisitorPort actor,
        CheckoutWaitSession session)
    {
        IBuildingShoppingVisitorPort shopping = actor?.Shopping;
        if (!ShouldWaitForServingWorker(actor))
        {
            yield break;
        }

        waitingCheckoutCount++;
        int queuePosition = waitingCheckoutCount;
        BuildingVisitorSnapshot visitor = actor.VisitorSnapshot;
        BuildingCheckoutPatienceProfile patience = BuildingCheckoutPatienceRules.Create(
            visitor.PersonalityPatience,
            visitor.ModelPatience,
            queuePosition);
        BuildingCheckoutWaitStage stage = BuildingCheckoutWaitStage.Waiting;
        float elapsedSeconds = 0f;
        owner.MarkCustomerFacilityStateDirty();
        owner.RequireCustomerWorkforceReplanService().RequestIdleWorkersToReplan();
        actor?.SetActionPhase(
            "계산 대기",
            owner,
            BuildingCheckoutPatienceRules.GetQueueDetail(patience, elapsedSeconds, stage));
        actor?.RecordActivity(owner, new BuildingActivitySnapshot(
            BuildingActivityKinds.Shopping,
            BuildingActivityOutcomes.Blocked,
            $"{owner.CustomerDisplayName}에서 계산할 직원을 기다리기 시작했다.",
            actionId: "checkout:staffed",
            reasonCode: "no-serving-worker",
            bubbleEligible: true));

        try
        {
            while (ShouldWaitForServingWorker(actor))
            {
                BuildingCheckoutWaitStage nextStage = BuildingCheckoutPatienceRules.GetStage(
                    patience,
                    elapsedSeconds);
                if (nextStage != stage)
                {
                    stage = nextStage;
                    HandleCheckoutWaitStage(actor, patience, stage, elapsedSeconds);
                    if (stage == BuildingCheckoutWaitStage.Abandoned)
                    {
                        session.Abandoned = true;
                        break;
                    }
                }

                actor?.SetActionPhase(
                    "계산 대기",
                    owner,
                    BuildingCheckoutPatienceRules.GetQueueDetail(patience, elapsedSeconds, stage));
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
            owner.MarkCustomerFacilityStateDirty();
        }

        if (shopping?.LastVisitOutcome != BuildingVisitOutcome.Abandoned
            && owner.HasServingWorker)
        {
            actor?.SetActionPhase("계산 중", owner, $"{Mathf.CeilToInt(elapsedSeconds)}초 기다림");
            actor?.RecordActivity(owner, new BuildingActivitySnapshot(
                BuildingActivityKinds.Shopping,
                BuildingActivityOutcomes.Started,
                $"{owner.CustomerDisplayName}에서 계산을 시작했다.",
                actionId: "checkout:staffed"));
        }
    }

    private void HandleCheckoutWaitStage(
        IBuildingVisitorPort actor,
        BuildingCheckoutPatienceProfile patience,
        BuildingCheckoutWaitStage stage,
        float elapsedSeconds)
    {
        if (actor == null)
        {
            return;
        }

        string actorName = actor.VisitorSnapshot.DisplayName;
        switch (stage)
        {
            case BuildingCheckoutWaitStage.Restless:
                owner.CustomerGameEventBus.Publish(
                    new CharacterTraitReactionEvent(
                        new[] { actor.BuildingCharacterId },
                        "queue:delay"));
                actor.ApplyMoodFactor(
                    $"checkout-wait:{owner.RequirePersistentInstanceId().Value}:restless",
                    "계산대가 오래 걸림",
                    -1.5f,
                    90f,
                    1);
                actor.RecordActivity(owner, new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Progress,
                    $"{actorName}은 계산 줄이 좀처럼 줄지 않아 초조해졌다.",
                    actionId: "checkout:waiting",
                    reasonCode: "queue-restless",
                    value: elapsedSeconds,
                    bubbleEligible: true));
                break;

            case BuildingCheckoutWaitStage.RequestingService:
                owner.RequireCustomerWorkforceReplanService().RequestIdleWorkersToReplan();
                actor.RecordActivity(owner, new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Responded,
                    patience.PatienceMultiplier >= 1.1f
                        ? $"{actorName}은 계산을 도와줄 직원을 불렀다."
                        : $"{actorName}은 더 기다리기 어렵다며 직원을 찾았다.",
                    actionId: "checkout:service-request",
                    reasonCode: "customer-called-worker",
                    value: elapsedSeconds,
                    bubbleEligible: true));
                if (!checkoutServiceAlertRaised)
                {
                    checkoutServiceAlertRaised = true;
                    owner.CustomerGameEventBus.RaiseAlert(
                        "계산 지원 필요",
                        $"{actorName}이(가) {owner.CustomerDisplayName}에서 계산 직원을 찾고 있습니다.",
                        EventAlertImportance.Low,
                        "손님");
                }
                break;

            case BuildingCheckoutWaitStage.Abandoned:
                actor.ApplyMoodFactor(
                    $"checkout-wait:{owner.RequirePersistentInstanceId().Value}:abandoned",
                    "기다리다 구매를 포기함",
                    patience.AbandonMoodPenalty,
                    180f,
                    1);
                actor.RememberFacilityExperience(
                    owner,
                    patience.AbandonSentiment,
                    "계산이 너무 늦어 구매를 포기했다.");
                actor.Shopping?.SetVisitOutcome(owner, BuildingVisitOutcome.Abandoned);
                actor.SetActionPhase(
                    "구매 포기",
                    owner,
                    $"{Mathf.CeilToInt(elapsedSeconds)}초 기다린 뒤 다른 곳을 찾음");
                actor.RecordActivity(owner, new BuildingActivitySnapshot(
                    BuildingActivityKinds.Shopping,
                    BuildingActivityOutcomes.Cancelled,
                    $"{actorName}은 계산을 기다리다 구매를 포기했다.",
                    actionId: "checkout:abandoned",
                    reasonCode: "patience-exhausted",
                    value: elapsedSeconds,
                    bubbleEligible: true));
                if (owner.CustomerGameTime >= nextCheckoutAbandonAlertTime)
                {
                    nextCheckoutAbandonAlertTime = owner.CustomerGameTime + 5f;
                    owner.CustomerGameEventBus.RaiseAlert(
                        "손님이 구매를 포기함",
                        $"{actorName}이(가) {owner.CustomerDisplayName}의 긴 계산 대기 때문에 다른 곳을 찾습니다.",
                        EventAlertImportance.Medium,
                        "손님");
                }
                break;
        }
    }

    private bool ShouldWaitForServingWorker(IBuildingVisitorPort actor)
    {
        return actor != null
            && !owner.isDestroy
            && Shop.CreatesRevenueFor(actor)
            && owner.RequiresCustomerServingWorker()
            && !owner.HasServingWorker;
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

    private static IReadOnlyList<BuildingRetailOfferSnapshot> CreateOfferSnapshots(
        IReadOnlyList<Stock> stocks)
    {
        if (stocks == null || stocks.Count == 0)
        {
            return Array.Empty<BuildingRetailOfferSnapshot>();
        }

        BuildingRetailOfferSnapshot[] offers =
            new BuildingRetailOfferSnapshot[stocks.Count];
        for (int index = 0; index < stocks.Count; index++)
        {
            offers[index] = new BuildingRetailOfferSnapshot(
                stocks[index].id,
                stocks[index].cost);
        }

        return offers;
    }

    private IEnumerator Linger(IBuildingVisitorPort actor, float seconds, object expectedAction)
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
                    || !actor.IsCurrentAction(expectedAction)
                    || actor.IsCurrentActionEnded))
            {
                yield break;
            }

            timer += owner.CustomerGameDeltaTime;
            yield return null;
        }
    }
}

internal sealed class CheckoutWaitSession
{
    public bool Abandoned { get; set; }
}

internal sealed class ShoppingSelectionBuffer
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
