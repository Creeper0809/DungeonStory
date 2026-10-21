using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class ShopCustomerInteractionService
{
    private const float SelfServiceCheckoutSeconds = 0.8f;
    private const float StaffedCheckoutSeconds = 0.35f;

    private readonly IRealtimeClock realtimeClock;
    private readonly Shop owner;
    private readonly Func<Vector3> worldPositionProvider;
    private readonly string ownerDisplayName;

    internal ShopCustomerInteractionService(
        IRealtimeClock realtimeClock,
        Shop owner,
        Func<Vector3> worldPositionProvider)
    {
        this.realtimeClock = realtimeClock
            ?? throw new ArgumentNullException(nameof(realtimeClock));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.worldPositionProvider = worldPositionProvider
            ?? throw new ArgumentNullException(nameof(worldPositionProvider));
        ownerDisplayName = owner.CustomerDisplayName;
    }

    internal int WaitingCheckoutCount => owner.InteractionState.activeUses.Count(value =>
        value.waitingForWorker || value.phase == ShopUseExecutionPhase.Checkout && value.selfServiceCheckout);
    internal bool HasWaitingCheckout => WaitingCheckoutCount > 0;

    internal IEnumerator Interact(IBuildingVisitorPort actor)
    {
        owner.EnsureCustomerStockInitialized();
        object currentAction = actor?.CurrentActionToken;
        ShopUseExecutionState restored = actor == null ? null
            : owner.FindActiveShopUse(actor.BuildingCharacterId.Value);
        if (restored == null && actor != null && !owner.CanVisit(actor, out _))
        {
            yield return owner.WaitForVisitAdmission(
                actor,
                currentAction,
                ownerDisplayName);
            if (!actor.IsCurrentAction(currentAction)
                || actor.IsCurrentActionEnded)
            {
                AbortInteraction(
                    actor,
                    serviceSession: null,
                    BuildingInteractionFailureKind.ActionReplaced,
                    "action-replaced-while-waiting-for-shop");
                yield break;
            }
        }
        if (restored == null && !owner.TryBeginUse(actor, out string failureReason))
        {
            actor?.Shopping?.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
            PublishObservation(ShopInteractionTransition.AdmissionRejected, actor, reason: failureReason);
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
        if (restored != null && !string.IsNullOrEmpty(restored.serviceSessionId)
            && (!owner.CustomerServiceSessionRuntime.TryGetSessionSnapshot(restored.serviceSessionId, out serviceSession)
                || !serviceSession.IsActive && !restored.MatchesCancelledSession(serviceSession, owner.RequirePersistentInstanceId().Value)
                || serviceSession.ActorId != restored.actorId
                || serviceSession.HubId != owner.RequirePersistentInstanceId().Value))
            throw new InvalidOperationException("Restored shop service session mismatch.");
        BuildingServiceHubAbility serviceHub = owner.GetServiceHubAbility();
        if (restored == null && serviceHub != null
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
            PublishObservation(ShopInteractionTransition.SessionRejected, actor, reason: serviceFailureCode);
            owner.EndUse(actor);
            yield break;
        }

        currentAction = actor.CurrentActionToken;
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
            if (!TryContinueInteraction(
                    actor,
                    currentAction,
                    out BuildingInteractionFailureKind mealFailure,
                    out string mealFailureDetail))
            {
                AbortInteraction(
                    actor,
                    serviceSession,
                    mealFailure,
                    mealFailureDetail);
                yield break;
            }
            owner.CustomerServiceCompletion.Finish(
                actor,
                serviceSession?.SessionId,
                shopable.LastVisitOutcome == BuildingVisitOutcome.Completed,
                "식사 서비스가 완료되지 않았습니다.",
                owner.CustomerServiceSessionCompletion);
            yield break;
        }

        yield return RunRetailVisit(actor, currentAction, serviceSession,
            restored ?? owner.BeginShopExecution(actor, serviceSession));
    }

    private IEnumerator RunRetailVisit(IBuildingVisitorPort actor, object action,
        ServiceSessionSnapshot session, ShopUseExecutionState state)
    {
        IBuildingShoppingVisitorPort shopping = actor.Shopping;
        while (true)
        {
            if (!ContinueRetail(actor, action, state)) yield break;
            switch (state.phase)
            {
                case ShopUseExecutionPhase.Selection:
                    if (state.selectionIndex >= state.selectionCount)
                    {
                        if (state.cart.Count == 0)
                            FailRetail(actor, state, "no-purchasable-item", "구매 가능한 상품이 없습니다.");
                        else state.Go(ShopUseExecutionPhase.CheckoutApproach);
                        break;
                    }
                    var selected = state.cart.GroupBy(value => value.saleItemId)
                        .ToDictionary(group => group.Key, group => group.Count());
                    state.selectedItemId = shopping.SelectOffer(
                        CreateOfferSnapshots(owner.GetCustomerPurchasableStock(selected)));
                    state.Go(ShopUseExecutionPhase.SelectionApproach);
                    break;
                case ShopUseExecutionPhase.SelectionApproach:
                    actor.SetActionPhase("상품 둘러보기", owner, $"{state.selectionIndex + 1}/{state.selectionCount}");
                    yield return actor.MoveTo(owner.GetFacilityAnchorWorldPosition(
                        FacilityAnchorPurposeIds.Use, actor.VisitorSnapshot.Position), 0.7f, action);
                    if (!ContinueRetail(actor, action, state)) yield break;
                    state.Go(ShopUseExecutionPhase.SelectionSettle);
                    break;
                case ShopUseExecutionPhase.SelectionSettle:
                    yield return WaitSaved(actor, action, state, 0.1f);
                    if (!ContinueRetail(actor, action, state)) yield break;
                    RemainStock candidate = owner.CustomerInventory.Stocks.FirstOrDefault(
                        value => value.id == state.selectedItemId);
                    int selectedCount = state.cart.Count(value => value.saleItemId == state.selectedItemId);
                    if (candidate != null && candidate.stock > selectedCount)
                        state.cart.Add(new ShopUseCartLine { saleItemId = candidate.id });
                    state.selectionIndex++;
                    state.selectedItemId = -2;
                    state.Go(ShopUseExecutionPhase.Selection);
                    break;
                case ShopUseExecutionPhase.CheckoutApproach:
                    actor.SetActionPhase("계산대 이동", owner);
                    yield return actor.MoveTo(owner.GetFacilityAnchorWorldPosition(
                        FacilityAnchorPurposeIds.Checkout, actor.VisitorSnapshot.Position), 1f, action);
                    if (!ContinueRetail(actor, action, state)) yield break;
                    state.Go(ShopUseExecutionPhase.Waiting);
                    break;
                case ShopUseExecutionPhase.Waiting:
                    bool managed = session == null ? owner.RequiresCustomerServingWorker()
                        : session.Contract.mode == ServiceOperationMode.Managed && owner.RequiresCustomerServingWorker();
                    if (managed)
                    {
                        SetServiceStage(session, ServiceSessionStage.Waiting);
                        yield return WaitForSavedAttendant(actor, action, state);
                        if (!ContinueRetail(actor, action, state)) yield break;
                    }
                    if (state.checkoutAbandoned)
                    {
                        state.serviceFailureReason = "대기 시간이 길어 손님이 떠났습니다.";
                        state.Go(ShopUseExecutionPhase.ServiceCompletion);
                    }
                    else if (managed && !owner.CanServeCustomer(actor, out string cannotServe))
                        FailRetail(actor, state, cannotServe, cannotServe);
                    else
                    {
                        SetServiceStage(session, ServiceSessionStage.Service);
                        state.selfServiceCheckout = state.createsRevenue && !owner.HasServingWorker;
                        state.Go(ShopUseExecutionPhase.Checkout);
                    }
                    break;
                case ShopUseExecutionPhase.Checkout:
                    if (state.createsRevenue)
                    {
                        if (!state.timerInitialized && state.selfServiceCheckout)
                        {
                            owner.RequireCustomerWorkforceReplanService().RequestIdleWorkersToReplan();
                            PublishObservation(ShopInteractionTransition.SelfCheckoutStarted, actor);
                        }
                        yield return WaitSaved(actor, action, state,
                            state.selfServiceCheckout ? SelfServiceCheckoutSeconds : StaffedCheckoutSeconds);
                        if (!ContinueRetail(actor, action, state)) yield break;
                    }
                    state.Go(ShopUseExecutionPhase.Crime);
                    break;
                case ShopUseExecutionPhase.Crime:
                    bool crimeResolved;
                    try
                    {
                        var cart = state.cart.Select(line => owner.CustomerInventory.Stocks
                            .FirstOrDefault(stock => stock.id == line.saleItemId)).Where(stock => stock != null).ToList();
                        crimeResolved = owner.TryResolveCheckoutCrime(actor, cart);
                    }
                    catch (Exception failure)
                    {
                        state.serviceFailureReason = "shoplifting-checkout-failed: " + failure.Message;
                        state.Go(ShopUseExecutionPhase.ServiceCompletion);
                        shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
                        try
                        {
                            FinishRetail(actor, state);
                        }
                        catch (Exception cleanupFailure)
                        {
                            // Keep the original transaction failure visible as well
                            // as the cleanup failure; the execution remains pending.
                            throw new AggregateException(
                                "Shop checkout failed and terminal cleanup remains pending.",
                                failure, cleanupFailure);
                        }
                        throw;
                    }
                    if (crimeResolved)
                    {
                        state.serviceSucceeded = true;
                        state.Go(ShopUseExecutionPhase.ServiceCompletion);
                        shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Completed);
                    }
                    else state.Go(ShopUseExecutionPhase.Purchase);
                    break;
                case ShopUseExecutionPhase.Purchase:
                    if (state.purchaseIndex >= state.cart.Count)
                    {
                        state.Go(ShopUseExecutionPhase.Finish);
                        break;
                    }
                    ShopUseCartLine line = state.cart[state.purchaseIndex];
                    if (!line.Committed)
                    {
                        RemainStock stock = owner.CustomerInventory.Stocks.FirstOrDefault(value => value.id == line.saleItemId);
                        if (!line.pricingCaptured)
                        {
                            if (stock == null || stock.stock <= 0) { state.purchaseIndex++; break; }
                            RetailRevenueModifierSnapshot pricing = owner.CaptureRevenueModifierSnapshot();
                            line.price = owner.CustomerInventory.CreatePricedStock(stock, pricing.PriceMultiplier).cost;
                            line.workerId = pricing.WorkerId;
                            line.priceMultiplier = pricing.PriceMultiplier;
                            line.modifierProvenance = pricing.SkillModifierProvenance;
                            line.pricingCaptured = true;
                        }
                        var result = new BuildingRetailPurchaseCommitResult();
                        result.BindPricingContext(line.Pricing);
                        result.BindShopUse(state.operationId, state.purchaseIndex);
                        // The purchase port must recover an already-taken lot before
                        // checking today's stock or wallet balance.
                        yield return shopping.Purchase(stock, line.price, action, owner, result);
                        if (!ContinueRetail(actor, action, state)) yield break;
                        if (!result.IsResolved || !result.Committed)
                        {
                            state.lastPurchaseFailure = result.IsResolved
                                ? result.FailureCode : "purchase-port-returned-without-result";
                            state.purchaseIndex++;
                            break;
                        }
                        if (!line.Committed)
                            throw new InvalidOperationException("Purchase port committed without binding its shop execution.");
                    }
                    if (!line.postCommitApplied)
                    {
                        line.postCommitApplied = true;
                        state.purchaseCount++;
                        if (state.createsRevenue) state.usedMoney = checked(state.usedMoney + line.price);
                        owner.PublishCustomerGameEvent(new FacilityStockConsumedEvent(actor, owner,
                            owner.CustomerInventory.GetStockCategory(line.saleItemId), 1));
                    }
                    state.purchaseIndex++;
                    owner.MarkCustomerFacilityStateDirty();
                    break;
                case ShopUseExecutionPhase.Finish:
                    if (state.purchaseCount == 0)
                    {
                        FailRetail(actor, state, state.lastPurchaseFailure, "결제 가능한 상품이 없습니다.");
                        break;
                    }
                    actor.ApplyMoodFactor($"shopping:{owner.RequirePersistentInstanceId().Value}",
                        "마음에 드는 물건을 삼", 5f, 120f, 2);
                    if (state.createsRevenue && state.usedMoney > 0)
                    {
                        Vector2 end = owner.GetFacilityAnchorWorldPosition(FacilityAnchorPurposeIds.Checkout,
                            actor.VisitorSnapshot.Position);
                        owner.RequireCustomerFloatingNumberFeedbackService().TryShow(
                            NumberCondition.ONEARNMONEY, end + Vector2.up, state.usedMoney);
                        owner.PublishCustomerGameEvent(new FacilityRevenueEvent(actor, owner, state.usedMoney));
                    }
                    if (!state.createsRevenue)
                        PublishObservation(ShopInteractionTransition.StaffUseCompleted, actor);
                    actor.ApplyRoomExperience(owner.CustomerRoomEnvironmentExperienceService, owner, "shopping");
                    shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Completed);
                    state.serviceSucceeded = true;
                    state.Go(ShopUseExecutionPhase.ServiceCompletion);
                    break;
                case ShopUseExecutionPhase.ServiceCompletion:
                    FinishRetail(actor, state);
                    if (state.serviceSucceeded) yield return new WaitForSeconds(0.5f);
                    yield break;
                default:
                    throw new InvalidOperationException("Unexpected retail execution phase: " + state.phase);
            }
        }
    }

    private void SetServiceStage(ServiceSessionSnapshot session, ServiceSessionStage stage)
    {
        if (session == null) return;
        if (!owner.CustomerServiceSessionRuntime.TryGetSessionSnapshot(session.SessionId, out var current))
            throw new InvalidOperationException("Shop service session disappeared.");
        if (current.Stage == stage) return;
        if (!owner.CustomerServiceSessionRuntime.TrySetStage(session.SessionId, stage, out var failure))
            throw new InvalidOperationException("Shop service stage refused: " + failure.Code);
    }

    private IEnumerator WaitSaved(IBuildingVisitorPort actor, object action,
        ShopUseExecutionState state, float seconds)
    {
        if (!state.timerInitialized)
        {
            state.timerInitialized = true;
            state.remainingSeconds = seconds;
        }
        while (state.remainingSeconds > 0f)
        {
            if (!TryContinueInteraction(actor, action, out _, out _)) yield break;
            state.remainingSeconds = Mathf.Max(0f, state.remainingSeconds - owner.CustomerGameDeltaTime);
            yield return null;
        }
    }

    private IEnumerator WaitForSavedAttendant(IBuildingVisitorPort actor, object action, ShopUseExecutionState state)
    {
        if (state.patience == null && ShouldWaitForServingWorker(actor))
        {
            state.waitingForWorker = true;
            BuildingVisitorSnapshot visitor = actor.VisitorSnapshot;
            state.patience = ShopCheckoutPatienceState.Capture(BuildingCheckoutPatienceRules.Create(
                visitor.PersonalityPatience, visitor.ModelPatience, WaitingCheckoutCount));
            state.waitStage = BuildingCheckoutWaitStage.Waiting;
            owner.RequireCustomerWorkforceReplanService().RequestIdleWorkersToReplan();
            PublishObservation(ShopInteractionTransition.AttendantWaitingStarted, actor);
        }
        while (ShouldWaitForServingWorker(actor) && !state.checkoutAbandoned)
        {
            if (!TryContinueInteraction(actor, action, out _, out _)) yield break;
            BuildingCheckoutPatienceProfile profile = state.patience.ToProfile();
            BuildingCheckoutWaitStage next = BuildingCheckoutPatienceRules.GetStage(profile, state.patienceElapsedSeconds);
            if (next != state.waitStage)
            {
                state.waitStage = next;
                HandleCheckoutWaitStage(actor, profile, next, state.patienceElapsedSeconds);
                if (next == BuildingCheckoutWaitStage.Abandoned)
                {
                    state.checkoutAbandoned = true;
                    break;
                }
            }
            actor.SetActionPhase("계산 대기", owner,
                BuildingCheckoutPatienceRules.GetQueueDetail(profile, state.patienceElapsedSeconds, state.waitStage));
            state.patienceElapsedSeconds += owner.CustomerGameDeltaTime;
            yield return null;
        }
        state.waitingForWorker = false;
        PublishObservation(ShopInteractionTransition.CheckoutQueueSampled, actor,
            waitingCount: WaitingCheckoutCount);
        owner.MarkCustomerFacilityStateDirty();
        if (!state.checkoutAbandoned && state.patience != null && owner.HasServingWorker)
            PublishObservation(ShopInteractionTransition.StaffCheckoutStarted, actor);
    }

    private bool ContinueRetail(IBuildingVisitorPort actor, object action, ShopUseExecutionState state)
    {
        if (TryContinueInteraction(actor, action, out var kind, out string detail)) return true;
        state.waitingForWorker = false;
        state.serviceSucceeded = false;
        state.serviceFailureReason = detail;
        state.Go(ShopUseExecutionPhase.ServiceCompletion);
        actor.Shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Abandoned);
        FinishRetail(actor, state);
        if (kind == BuildingInteractionFailureKind.ActionReplaced)
            actor.ReportInteractionCancellation(kind, detail, owner);
        else actor.ReportInteractionFailure(kind, detail, owner);
        return false;
    }

    private void FailRetail(IBuildingVisitorPort actor, ShopUseExecutionState state, string code, string reason)
    {
        actor.Shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
        PublishObservation(ShopInteractionTransition.RetailFailed, actor, reason: code, detail: reason);
        state.serviceSucceeded = false;
        state.serviceFailureReason = reason;
        state.Go(ShopUseExecutionPhase.ServiceCompletion);
    }

    internal void ResumeFailedServiceCleanup(IBuildingVisitorPort actor, ShopUseExecutionState state)
    {
        if (actor == null || state == null || state.actorId != actor.BuildingCharacterId.Value
            || !ReferenceEquals(owner.FindActiveShopUse(state.actorId), state)
            || !owner.CustomerServiceSessionRuntime.TryGetSessionSnapshot(state.serviceSessionId, out var session)
            || !state.MatchesCancelledSession(session, owner.RequirePersistentInstanceId().Value))
            throw new InvalidOperationException("Shop failed-cleanup owner/session mismatch.");
        FinishRetail(actor, state);
    }

    private void FinishRetail(IBuildingVisitorPort actor, ShopUseExecutionState state)
    {
        if (!string.IsNullOrEmpty(state.serviceSessionId))
        {
            IServiceSessionRuntime runtime = owner.CustomerServiceSessionRuntime;
            if (!runtime.TryGetSessionSnapshot(state.serviceSessionId, out var session))
                throw new InvalidOperationException("Shop terminal lost its active service session.");
            if (!session.IsActive)
            {
                if (!state.MatchesCancelledSession(session, owner.RequirePersistentInstanceId().Value))
                    throw new InvalidOperationException("Shop terminal does not match its cancelled cleanup session.");
            }
            else if (state.serviceSucceeded)
            {
                if (!runtime.TryCompleteSession(state.serviceSessionId, out _, out var failure))
                    throw new InvalidOperationException("Shop completion remains pending: " + failure.Code);
            }
            else if (!runtime.CancelSession(state.serviceSessionId, state.serviceFailureReason))
                throw new InvalidOperationException("Shop cancellation remains pending.");
        }
        owner.EndShopUse(actor, state.serviceSucceeded);
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
        if (!TryContinueInteraction(
                actor,
                currentAction,
                out BuildingInteractionFailureKind interactionFailure,
                out string interactionFailureDetail))
        {
            shopping.SetVisitOutcome(null, BuildingVisitOutcome.Abandoned);
            if (interactionFailure != BuildingInteractionFailureKind.ActionReplaced)
            {
                actor?.ReportInteractionFailure(
                    interactionFailure,
                    $"{ownerDisplayName}: {interactionFailureDetail}",
                    owner != null && !owner.isDestroy ? owner : null);
            }
            yield break;
        }
        yield return Linger(actor, 0.1f, currentAction);
        if (!TryContinueInteraction(
                actor,
                currentAction,
                out interactionFailure,
                out interactionFailureDetail))
        {
            shopping.SetVisitOutcome(null, BuildingVisitOutcome.Abandoned);
            if (interactionFailure != BuildingInteractionFailureKind.ActionReplaced)
            {
                actor?.ReportInteractionFailure(
                    interactionFailure,
                    $"{ownerDisplayName}: {interactionFailureDetail}",
                    owner != null && !owner.isDestroy ? owner : null);
            }
            yield break;
        }

        // The consumables runtime owns the authored four-second physical meal action.
        // A second facility linger here would double-count meal time.
        float duration = 0f;

        actor?.SetActionPhase("식사 중", owner, $"{duration:0.#}초");
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        BuildingMealUseSnapshot meal = default;
        bool consumed = actor != null
            && actor.TryConsumeMeal(
                owner.CustomerMealConsumptionRuntime,
                owner,
                out meal);
        if (!consumed && meal.AcceptedPending)
        {
            actor.SetActionPhase("식사 중", owner, "4s");
            float deadline = realtimeClock.Time + 15f;
            while (realtimeClock.Time < deadline)
            {
                yield return null;
                if (!TryContinueInteraction(
                        actor,
                        currentAction,
                        out interactionFailure,
                        out interactionFailureDetail))
                {
                    shopping.SetVisitOutcome(null, BuildingVisitOutcome.Abandoned);
                    if (interactionFailure != BuildingInteractionFailureKind.ActionReplaced)
                    {
                        actor?.ReportInteractionFailure(
                            interactionFailure,
                            $"{ownerDisplayName}: {interactionFailureDetail}",
                            owner != null && !owner.isDestroy ? owner : null);
                    }
                    yield break;
                }
                if (!actor.TryGetMealConsumptionResult(
                        owner.CustomerMealConsumptionRuntime,
                        meal.OperationId,
                        out BuildingMealUseSnapshot currentMeal))
                {
                    meal = currentMeal;
                    break;
                }
                meal = currentMeal;
                if (meal.Success)
                {
                    consumed = true;
                    break;
                }
                if (!meal.AcceptedPending)
                {
                    break;
                }
            }
            if (!consumed && meal.AcceptedPending)
            {
                meal = new BuildingMealUseSnapshot(
                    false,
                    CharacterConsumablesFailureCode.PhysicalConsumptionFailed.ToString(),
                    string.Empty,
                    0,
                    failureDetail: "meal-action-timeout");
            }
        }
        if (!consumed)
        {
            if (meal.IsRetryableUnavailable)
            {
                shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Abandoned);
                PublishObservation(ShopInteractionTransition.MealSupplyChanged, actor);
                yield break;
            }

            if (meal.IsNoLongerNeeded)
            {
                shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Abandoned);
                PublishObservation(ShopInteractionTransition.MealNoLongerNeeded, actor);
                yield break;
            }

            string failureCode = string.IsNullOrWhiteSpace(meal.FailureCode)
                ? CharacterConsumablesFailureCode.PhysicalConsumptionFailed.ToString()
                : meal.FailureCode;
            shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Failed);
            PublishObservation(ShopInteractionTransition.MealFailed, actor,
                reason: failureCode, detail: meal.FailureDetail);
            yield break;
        }

        int revenue = 0;
        if (Shop.CreatesRevenueFor(actor))
        {
            if (shopping.CanPay(meal.UnitPrice))
            {
                yield return shopping.PayForService(
                    meal.UnitPrice,
                    currentAction,
                    owner);
                if (!TryContinueInteraction(
                        actor,
                        currentAction,
                        out BuildingInteractionFailureKind paymentFailure,
                        out string paymentFailureDetail))
                {
                    shopping.SetVisitOutcome(null, BuildingVisitOutcome.Abandoned);
                    if (paymentFailure != BuildingInteractionFailureKind.ActionReplaced)
                    {
                        actor.ReportInteractionFailure(
                            paymentFailure,
                            $"{owner.CustomerDisplayName}: {paymentFailureDetail}",
                            owner);
                    }
                    yield break;
                }
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
                PublishObservation(ShopInteractionTransition.MealUnpaid, actor, itemName: meal.DisplayName);
            }
        }

        actor?.ApplyRoomExperience(owner.CustomerRoomEnvironmentExperienceService, owner, "shopping");
        shopping.SetVisitOutcome(owner, BuildingVisitOutcome.Completed);
        PublishObservation(ShopInteractionTransition.MealCompleted, actor, itemName: meal.DisplayName, revenue: revenue);
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

        switch (stage)
        {
            case BuildingCheckoutWaitStage.Restless:
                actor.ApplyMoodFactor(
                    $"checkout-wait:{owner.RequirePersistentInstanceId().Value}:restless",
                    "계산대가 오래 걸림",
                    -1.5f,
                    90f,
                    1);
                PublishObservation(ShopInteractionTransition.CheckoutRestless, actor, elapsedSeconds: elapsedSeconds);
                break;

            case BuildingCheckoutWaitStage.RequestingService:
                owner.RequireCustomerWorkforceReplanService().RequestIdleWorkersToReplan();
                PublishObservation(ShopInteractionTransition.CheckoutServiceRequested, actor,
                    elapsedSeconds: elapsedSeconds, patienceMultiplier: patience.PatienceMultiplier);
                break;

            case BuildingCheckoutWaitStage.Abandoned:
                actor.ApplyMoodFactor(
                    $"checkout-wait:{owner.RequirePersistentInstanceId().Value}:abandoned",
                    "기다리다 구매를 포기함",
                    patience.AbandonMoodPenalty,
                    180f,
                    1);
                actor.ApplyMoodFactor(
                    "wait:exceeded",
                    "대기 한계 초과",
                    0f,
                    GameCalendarRules.SecondsPerDay,
                    1);
                actor.Shopping?.SetVisitOutcome(owner, BuildingVisitOutcome.Abandoned);
                actor.SetActionPhase(
                    "구매 포기",
                    owner,
                    $"{Mathf.CeilToInt(elapsedSeconds)}초 기다린 뒤 다른 곳을 찾음");
                PublishObservation(ShopInteractionTransition.CheckoutAbandoned, actor,
                    elapsedSeconds: elapsedSeconds, abandonSentiment: patience.AbandonSentiment,
                    gameTime: owner.CustomerGameTime);
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

    private bool TryContinueInteraction(
        IBuildingVisitorPort actor,
        object expectedAction,
        out BuildingInteractionFailureKind failureKind,
        out string failureDetail)
    {
        if (owner == null || owner.isDestroy)
        {
            failureKind = BuildingInteractionFailureKind.FacilityDestroyed;
            failureDetail = "shop-destroyed-during-interaction";
            return false;
        }
        if (actor == null)
        {
            failureKind = BuildingInteractionFailureKind.ActorUnavailable;
            failureDetail = "actor-missing-during-shop-interaction";
            return false;
        }
        try
        {
            if (!actor.VisitorSnapshot.IsRuntimeActive)
            {
                failureKind = BuildingInteractionFailureKind.ActorUnavailable;
                failureDetail = "actor-inactive-during-shop-interaction";
                return false;
            }
        }
        catch (MissingReferenceException)
        {
            failureKind = BuildingInteractionFailureKind.ActorUnavailable;
            failureDetail = "actor-destroyed-during-shop-interaction";
            return false;
        }
        if (expectedAction != null
            && (!actor.IsCurrentAction(expectedAction)
                || actor.IsCurrentActionEnded))
        {
            failureKind = BuildingInteractionFailureKind.ActionReplaced;
            failureDetail = "action-replaced-during-shop-interaction";
            return false;
        }

        failureKind = BuildingInteractionFailureKind.None;
        failureDetail = string.Empty;
        return true;
    }

    private void AbortInteraction(
        IBuildingVisitorPort actor,
        ServiceSessionSnapshot serviceSession,
        BuildingInteractionFailureKind failureKind,
        string failureDetail)
    {
        string detail = string.IsNullOrWhiteSpace(failureDetail)
            ? failureKind.ToString()
            : failureDetail;
        bool ownerAlive = owner != null && !owner.isDestroy;
        if (ownerAlive)
        {
            actor?.Shopping?.SetVisitOutcome(owner, BuildingVisitOutcome.Abandoned);
            owner.CustomerServiceCompletion.Finish(
                actor,
                serviceSession?.SessionId,
                false,
                detail,
                owner.CustomerServiceSessionCompletion);
            PublishObservation(ShopInteractionTransition.InteractionAborted, actor,
                detail: detail, facilityName: ownerDisplayName);
        }
        else
        {
            actor?.Shopping?.SetVisitOutcome(null, BuildingVisitOutcome.Abandoned);
        }

        if (failureKind == BuildingInteractionFailureKind.ActionReplaced)
        {
            actor?.ReportInteractionCancellation(
                failureKind,
                $"{ownerDisplayName}: {detail}",
                ownerAlive ? owner : null);
        }
        else
        {
            actor?.ReportInteractionFailure(
                failureKind,
                $"{ownerDisplayName}: {detail}",
                ownerAlive ? owner : null);
        }
    }

    private void PublishObservation(ShopInteractionTransition transition, IBuildingVisitorPort actor,
        string reason = "", string detail = "", string itemName = "", float elapsedSeconds = 0f,
        float patienceMultiplier = 1f, float abandonSentiment = 0f, int revenue = 0,
        int waitingCount = 0, float gameTime = 0f, string facilityName = null)
    {
        var observation = new ShopInteractionObservedEvent(transition, owner, actor,
            reason, detail, itemName, elapsedSeconds, patienceMultiplier, abandonSentiment,
            revenue, waitingCount, gameTime, facilityName);
        try { owner.PublishCustomerGameEvent(observation); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            Debug.LogWarning("shop-interaction-observation:" + exception.GetType().Name + ":" + exception.Message);
        }
    }
}
