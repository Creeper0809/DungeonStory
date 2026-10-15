#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// Pure DTO validation only. Shop coroutine execution and state-module restore
// wiring are deliberately outside this scenario's authority.
public static class ShopUseExecutionStateDebugScenarios
{
    private const string FacilityId = "building:shop:fixture";

    public static bool RunAll(bool log = true)
    {
        try
        {
            VerifyEmptyStateIsValid();
            VerifyCloneIsIndependent();
            VerifyPurchaseCommitBindingValidation();
            VerifyOperationSequenceAndActorRejections();
            VerifyPhaseAndTimeRejections();
            VerifyCartLotAndActionRejections();
            VerifyCartFeedbackRejections();
            VerifyDuplicateActorAndSequenceRejections();
            VerifyCancelledServiceCompletionMatching();
            if (log)
            {
                Debug.Log(
                    "SHOP_USE_EXECUTION_STATE=PASS; "
                    + "scope=dto-clone-and-validation; "
                    + "runtime-execution-and-restore-wiring=not-covered");
            }
            return true;
        }
        catch (Exception exception)
        {
            if (log)
            {
                Debug.LogError(
                    $"{nameof(ShopUseExecutionStateDebugScenarios)} failed: {exception}");
            }
            return false;
        }
    }

    private static void VerifyEmptyStateIsValid()
    {
        ShopInteractionState empty = new();
        Require(empty.IsValid(FacilityId, out string error),
            "An empty shop interaction state was rejected: " + error);
    }

    private static void VerifyCloneIsIndependent()
    {
        ShopInteractionState source = CreateValidState();
        ShopInteractionState clone = source.Clone();
        ShopUseExecutionState sourceUse = source.activeUses[0];
        ShopUseExecutionState clonedUse = clone.activeUses[0];
        Require(!ReferenceEquals(source, clone)
                && !ReferenceEquals(source.activeUses, clone.activeUses)
                && !ReferenceEquals(sourceUse, clonedUse)
                && !ReferenceEquals(sourceUse.cart, clonedUse.cart)
                && !ReferenceEquals(sourceUse.cart[0], clonedUse.cart[0])
                && !ReferenceEquals(
                    sourceUse.cart[0].committedLot,
                    clonedUse.cart[0].committedLot),
            "Shop interaction clone retained mutable execution or cart references.");

        clone.nextSequence = 8L;
        clonedUse.actorId = "character:shop:changed";
        clonedUse.cart[0].price = 99;
        clonedUse.cart[0].committedLot.itemDefinitionId = "item:shop:changed";
        clonedUse.cart[0].feedbackStarted = false;
        clonedUse.cart[0].feedbackRemainingSeconds = 0f;

        Require(source.nextSequence == 2L
                && sourceUse.actorId == "character:shop:visitor"
                && sourceUse.cart[0].price == 7
                && sourceUse.cart[0].committedLot.itemDefinitionId
                    == "item:shop:fixture"
                && sourceUse.cart[0].feedbackStarted
                && sourceUse.cart[0].feedbackRemainingSeconds == 0.5f,
            "Shop interaction clone mutation changed the original DTO state.");
    }

    private static void VerifyPurchaseCommitBindingValidation()
    {
        string operationId = ShopUseExecutionState.OperationFor(FacilityId, 1L);
        BuildingRetailPurchaseCommitResult bound = new();
        bound.BindShopUse(operationId, 0);
        Require(bound.ShopUseOperationId == operationId && bound.ShopUseCartIndex == 0,
            "Retail purchase result did not retain its valid shop-use binding.");

        BuildingRetailPurchaseCommitResult emptyOperation = new();
        RequireInvalidShopUseBinding(emptyOperation, string.Empty, 0,
            "empty shop-use operation identity");

        BuildingRetailPurchaseCommitResult negativeIndex = new();
        RequireInvalidShopUseBinding(negativeIndex, operationId, -1,
            "negative shop-use cart index");

        RequireInvalidShopUseBinding(bound, operationId, 1,
            "shop-use binding rebinding");
        Require(bound.ShopUseOperationId == operationId && bound.ShopUseCartIndex == 0,
            "Rejected shop-use rebinding changed the original binding.");

        BuildingRetailPurchaseCommitResult resolved = new();
        resolved.Reject("fixture-rejected");
        RequireInvalidShopUseBinding(resolved, operationId, 0,
            "shop-use binding after purchase resolution");
        Require(resolved.IsResolved && string.IsNullOrEmpty(resolved.ShopUseOperationId)
                && resolved.ShopUseCartIndex == -1,
            "Rejected resolved shop-use binding changed the purchase result.");
    }

    private static void VerifyOperationSequenceAndActorRejections()
    {
        ShopInteractionState nonPositiveSequence = CreateValidState();
        nonPositiveSequence.activeUses[0].sequence = 0L;
        RequireInvalid(nonPositiveSequence, "non-positive operation sequence");

        ShopInteractionState uncommittedSequence = CreateValidState();
        uncommittedSequence.activeUses[0].sequence = uncommittedSequence.nextSequence;
        RequireInvalid(uncommittedSequence, "next operation sequence accepted as active");

        ShopInteractionState mismatchedOperation = CreateValidState();
        mismatchedOperation.activeUses[0].operationId =
            ShopUseExecutionState.OperationFor(FacilityId, 2L);
        RequireInvalid(mismatchedOperation, "operation identity mismatched its sequence");

        ShopInteractionState missingActor = CreateValidState();
        missingActor.activeUses[0].actorId = string.Empty;
        RequireInvalid(missingActor, "missing actor identity");
    }

    private static void VerifyPhaseAndTimeRejections()
    {
        ShopInteractionState undefinedPhase = CreateValidState();
        undefinedPhase.activeUses[0].phase = (ShopUseExecutionPhase)999;
        RequireInvalid(undefinedPhase, "undefined shop-use phase");

        ShopInteractionState nanRemaining = CreateValidState();
        nanRemaining.activeUses[0].remainingSeconds = float.NaN;
        RequireInvalid(nanRemaining, "NaN remaining time");

        ShopInteractionState negativeRemaining = CreateValidState();
        negativeRemaining.activeUses[0].remainingSeconds = -0.25f;
        RequireInvalid(negativeRemaining, "negative remaining time");
    }

    private static void VerifyCartLotAndActionRejections()
    {
        ShopInteractionState invalidLot = CreateValidState();
        invalidLot.activeUses[0].cart[0].committedLot.unitMassGrams = 0L;
        RequireInvalid(invalidLot, "committed cart lot without positive unit mass");

        ShopInteractionState missingActionType = CreateValidState();
        missingActionType.activeUses[0].actionType = string.Empty;
        RequireInvalid(missingActionType, "missing action type");

        ShopInteractionState missingActionBranch = CreateValidState();
        missingActionBranch.activeUses[0].actionBranch =
            (int)CharacterAiBranch.None;
        RequireInvalid(missingActionBranch, "missing action branch");
    }

    private static void VerifyCartFeedbackRejections()
    {
        ShopInteractionState nanFeedback = CreateValidState();
        nanFeedback.activeUses[0].cart[0].feedbackRemainingSeconds = float.NaN;
        RequireInvalid(nanFeedback, "NaN cart feedback remaining time");

        ShopInteractionState negativeFeedback = CreateValidState();
        negativeFeedback.activeUses[0].cart[0].feedbackRemainingSeconds = -0.25f;
        RequireInvalid(negativeFeedback, "negative cart feedback remaining time");

        ShopInteractionState unstartedFeedback = CreateValidState();
        unstartedFeedback.activeUses[0].cart[0].feedbackStarted = false;
        RequireInvalid(unstartedFeedback,
            "positive cart feedback remaining time before feedback starts");
    }

    private static void VerifyDuplicateActorAndSequenceRejections()
    {
        ShopInteractionState duplicateActor = CreateValidState();
        duplicateActor.nextSequence = 3L;
        duplicateActor.activeUses.Add(CreateValidUse(
            sequence: 2L,
            actorId: "character:shop:visitor"));
        RequireInvalid(duplicateActor, "duplicate active-use actor");

        ShopInteractionState duplicateSequence = CreateValidState();
        duplicateSequence.activeUses.Add(CreateValidUse(
            sequence: 1L,
            actorId: "character:shop:second-visitor"));
        RequireInvalid(duplicateSequence, "duplicate active-use sequence");
    }

    private static void VerifyCancelledServiceCompletionMatching()
    {
        ShopUseExecutionState failed = CreateFailedServiceCompletionUse();
        Require(failed.IsFailedServiceCompletion
                && failed.MatchesCancelledSession(
                    CreateCancelledSession(ServiceSessionTerminalPhase.Prepared),
                    FacilityId)
                && failed.MatchesCancelledSession(
                    CreateCancelledSession(ServiceSessionTerminalPhase.Acknowledged),
                    FacilityId),
            "Failed service completion did not accept exact cancelled terminal endpoints.");

        ServiceSessionSnapshot wrongActor = CreateCancelledSession(
            ServiceSessionTerminalPhase.Prepared);
        wrongActor.ActorId = "character:shop:other";
        Require(!failed.MatchesCancelledSession(wrongActor, FacilityId),
            "Cancelled service completion accepted a different actor.");

        ServiceSessionSnapshot wrongSession = CreateCancelledSession(
            ServiceSessionTerminalPhase.Prepared);
        wrongSession.SessionId = "service:shop:other";
        Require(!failed.MatchesCancelledSession(wrongSession, FacilityId),
            "Cancelled service completion accepted a different session.");

        ServiceSessionSnapshot wrongReason = CreateCancelledSession(
            ServiceSessionTerminalPhase.Prepared);
        wrongReason.CancellationReason = "fixture-other-reason";
        Require(!failed.MatchesCancelledSession(wrongReason, FacilityId),
            "Cancelled service completion accepted a different cancellation reason.");

        ServiceSessionSnapshot wrongFacility = CreateCancelledSession(
            ServiceSessionTerminalPhase.Prepared);
        wrongFacility.HubId = "building:shop:other";
        Require(!failed.MatchesCancelledSession(wrongFacility, FacilityId),
            "Cancelled service completion accepted a different facility.");

        ServiceSessionSnapshot completed = CreateCancelledSession(
            ServiceSessionTerminalPhase.Prepared);
        completed.Stage = ServiceSessionStage.Completed;
        Require(!failed.MatchesCancelledSession(completed, FacilityId),
            "Cancelled service completion accepted a completed session.");

        ServiceSessionSnapshot active = CreateCancelledSession(
            ServiceSessionTerminalPhase.Prepared);
        active.Stage = ServiceSessionStage.Service;
        Require(!failed.MatchesCancelledSession(active, FacilityId),
            "Cancelled service completion accepted an active session.");

        ServiceSessionSnapshot noTerminal = CreateCancelledSession(
            ServiceSessionTerminalPhase.None);
        Require(!failed.MatchesCancelledSession(noTerminal, FacilityId),
            "Cancelled service completion accepted no terminal phase.");

        ShopUseExecutionState wrongPhase = CreateFailedServiceCompletionUse();
        wrongPhase.phase = ShopUseExecutionPhase.Finish;
        Require(!wrongPhase.IsFailedServiceCompletion
                && !wrongPhase.MatchesCancelledSession(
                    CreateCancelledSession(ServiceSessionTerminalPhase.Prepared),
                    FacilityId),
            "Non-service-completion state matched a cancelled session.");

        ShopUseExecutionState succeeded = CreateFailedServiceCompletionUse();
        succeeded.serviceSucceeded = true;
        Require(!succeeded.IsFailedServiceCompletion
                && !succeeded.MatchesCancelledSession(
                    CreateCancelledSession(ServiceSessionTerminalPhase.Prepared),
                    FacilityId),
            "Succeeded service completion matched a cancelled session.");

        ShopUseExecutionState emptyReason = CreateFailedServiceCompletionUse();
        emptyReason.serviceFailureReason = string.Empty;
        Require(!emptyReason.IsFailedServiceCompletion
                && !emptyReason.MatchesCancelledSession(
                    CreateCancelledSession(ServiceSessionTerminalPhase.Prepared),
                    FacilityId),
            "Service completion without a failure reason matched a cancelled session.");

        ShopUseExecutionState emptySession = CreateFailedServiceCompletionUse();
        emptySession.serviceSessionId = string.Empty;
        Require(!emptySession.IsFailedServiceCompletion
                && !emptySession.MatchesCancelledSession(
                    CreateCancelledSession(ServiceSessionTerminalPhase.Prepared),
                    FacilityId),
            "Service completion without a session identity matched a cancelled session.");

        ShopUseExecutionState meal = CreateFailedServiceCompletionUse();
        meal.meal = true;
        Require(!meal.IsFailedServiceCompletion
                && !meal.MatchesCancelledSession(
                    CreateCancelledSession(ServiceSessionTerminalPhase.Prepared),
                    FacilityId),
            "Meal service completion matched a cancelled retail session.");
    }

    private static ShopInteractionState CreateValidState() => new()
    {
        nextSequence = 2L,
        activeUses = new List<ShopUseExecutionState>
        {
            CreateValidUse()
        }
    };

    private static ShopUseExecutionState CreateValidUse(
        long sequence = 1L,
        string actorId = "character:shop:visitor") => new()
    {
        sequence = sequence,
        operationId = ShopUseExecutionState.OperationFor(FacilityId, sequence),
        actorId = actorId,
        actionType = "AIShop",
        actionBranch = (int)CharacterAiBranch.Shopping,
        phase = ShopUseExecutionPhase.Finish,
        createsRevenue = true,
        selectionCount = 1,
        selectionIndex = 1,
        selectedItemId = 101,
        cart = new List<ShopUseCartLine>
        {
            new()
            {
                saleItemId = 101,
                pricingCaptured = true,
                price = 7,
                workerId = string.Empty,
                priceMultiplier = 1f,
                modifierProvenance = "none",
                purchaseOperationId = "shop-purchase:fixture:1",
                committedLot = new RetailStockLotSnapshot
                {
                    saleItemId = 101,
                    itemDefinitionId = "item:shop:fixture",
                    quantity = 1,
                    unitMassGrams = 10L
                },
                postCommitApplied = true,
                feedbackStarted = true,
                feedbackRemainingSeconds = 0.5f
            }
        },
        purchaseIndex = 1,
        purchaseCount = 1,
        timerInitialized = true,
        remainingSeconds = 1.5f,
        usedMoney = 7,
        lastPurchaseFailure = "no-purchasable-item",
        mealDisplayName = string.Empty
    };

    private static ShopUseExecutionState CreateFailedServiceCompletionUse()
    {
        ShopUseExecutionState state = CreateValidUse();
        state.phase = ShopUseExecutionPhase.ServiceCompletion;
        state.serviceSessionId = "service:shop:cancelled";
        state.serviceFailureReason = "fixture-shop-service-cancelled";
        state.serviceSucceeded = false;
        state.meal = false;
        return state;
    }

    private static ServiceSessionSnapshot CreateCancelledSession(
        ServiceSessionTerminalPhase terminalPhase) => new()
    {
        SessionId = "service:shop:cancelled",
        HubId = FacilityId,
        ActorId = "character:shop:visitor",
        ProcessId = "service:retail:sale",
        Stage = ServiceSessionStage.Cancelled,
        TerminalPhase = terminalPhase,
        CancellationReason = "fixture-shop-service-cancelled"
    };

    private static void RequireInvalid(
        ShopInteractionState state,
        string scenario)
    {
        Require(!state.IsValid(FacilityId, out string error)
                && !string.IsNullOrWhiteSpace(error),
            scenario + " was accepted as valid shop interaction state.");
    }

    private static void RequireInvalidShopUseBinding(
        BuildingRetailPurchaseCommitResult result,
        string operationId,
        int cartIndex,
        string scenario)
    {
        try
        {
            result.BindShopUse(operationId, cartIndex);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(
            scenario + " was accepted as a retail purchase shop-use binding.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
