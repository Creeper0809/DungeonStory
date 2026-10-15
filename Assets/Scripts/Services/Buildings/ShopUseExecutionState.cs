using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// Durable domain progress. Stock, money, consumption and ledger receipts remain
// owned by their existing authorities; this state only binds their execution.
public enum ShopUseExecutionPhase
{
    Selection, SelectionApproach, SelectionSettle, CheckoutApproach,
    Waiting, Checkout, Crime, Purchase, Finish, ServiceCompletion, Complete,
    MealApproach, MealSettle, MealRequest, MealWaiting, MealPayment, MealFinish
}

[Serializable]
public sealed class ShopUseCartLine
{
    public int saleItemId;
    public bool pricingCaptured;
    public int price;
    public string workerId = string.Empty;
    public float priceMultiplier = 1f;
    public string modifierProvenance = "none";
    public string purchaseOperationId = string.Empty;
    public string pendingPurchaseOperationId = string.Empty;
    public RetailStockLotSnapshot committedLot;
    public bool postCommitApplied;
    public bool feedbackStarted;
    public float feedbackRemainingSeconds;

    [Newtonsoft.Json.JsonIgnore] public RetailRevenueModifierSnapshot Pricing => new(workerId, priceMultiplier, modifierProvenance);
    [Newtonsoft.Json.JsonIgnore] public bool Committed => !string.IsNullOrEmpty(purchaseOperationId);
    public ShopUseCartLine Clone()
    {
        var clone = (ShopUseCartLine)MemberwiseClone();
        clone.committedLot = committedLot?.Clone();
        return clone;
    }

    internal bool IsValid() => saleItemId >= 0 && price >= 0
        && FacilityUseExecutionState.Optional(workerId)
        && FacilityUseExecutionState.Canonical(modifierProvenance)
        && FacilityUseExecutionState.Finite(priceMultiplier) && priceMultiplier >= 1f
        && priceMultiplier <= GoldEconomyBalanceRules.MaximumWorkerRevenuePremium
        && FacilityUseExecutionState.Optional(purchaseOperationId)
        && FacilityUseExecutionState.Optional(pendingPurchaseOperationId)
        && (!Committed || pendingPurchaseOperationId.Length == 0)
        && (pendingPurchaseOperationId.Length == 0 || pricingCaptured && feedbackStarted && feedbackRemainingSeconds == 0f)
        && FacilityUseExecutionState.Finite(feedbackRemainingSeconds) && feedbackRemainingSeconds >= 0f
        && (feedbackStarted || feedbackRemainingSeconds == 0f)
        && (!Committed ? committedLot == null && !postCommitApplied
            : pricingCaptured && committedLot != null && committedLot.saleItemId == saleItemId
                && committedLot.quantity == 1 && committedLot.unitMassGrams > 0
                && FacilityUseExecutionState.Canonical(committedLot.itemDefinitionId));
}

[Serializable]
public sealed class ShopCheckoutPatienceState
{
    public float multiplier, restlessSeconds, requestSeconds, abandonSeconds, moodPenalty, sentiment;
    public int queuePosition;
    public BuildingCheckoutPatienceProfile ToProfile() => new(multiplier, queuePosition,
        restlessSeconds, requestSeconds, abandonSeconds, moodPenalty, sentiment);
    public static ShopCheckoutPatienceState Capture(BuildingCheckoutPatienceProfile profile) => new()
    {
        multiplier = profile.PatienceMultiplier, queuePosition = profile.QueuePosition,
        restlessSeconds = profile.RestlessSeconds, requestSeconds = profile.RequestServiceSeconds,
        abandonSeconds = profile.AbandonSeconds, moodPenalty = profile.AbandonMoodPenalty,
        sentiment = profile.AbandonSentiment
    };
    public ShopCheckoutPatienceState Clone() => (ShopCheckoutPatienceState)MemberwiseClone();
    internal bool IsValid() => queuePosition > 0 && multiplier > 0f && restlessSeconds >= 0f
        && requestSeconds >= restlessSeconds && abandonSeconds >= requestSeconds
        && new[] { multiplier, restlessSeconds, requestSeconds, abandonSeconds, moodPenalty, sentiment }
            .All(FacilityUseExecutionState.Finite);
}

[Serializable]
public sealed class ShopUseExecutionState
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public long sequence;
    public string operationId = string.Empty, actorId = string.Empty, actionType = string.Empty;
    public int actionBranch;
    public string serviceSessionId = string.Empty;
    public ShopUseExecutionPhase phase;
    public bool meal, createsRevenue;
    public int selectionCount, selectionIndex, selectedItemId = -2, purchaseIndex;
    public List<ShopUseCartLine> cart = new();
    public bool timerInitialized;
    public float remainingSeconds;
    public bool selfServiceCheckout, waitingForWorker;
    public ShopCheckoutPatienceState patience;
    public float patienceElapsedSeconds;
    public BuildingCheckoutWaitStage waitStage;
    public bool checkoutAbandoned;
    public int usedMoney, purchaseCount;
    public string lastPurchaseFailure = "no-purchasable-item";
    public bool serviceSucceeded;
    public string serviceFailureReason = string.Empty;
    public string mealOperationId = string.Empty, mealDisplayName = string.Empty;
    public int mealUnitPrice;
    public float mealWaitElapsedSeconds;
    public bool mealSucceeded, mealPaymentCompleted, mealRevenueCommitted;

    [Newtonsoft.Json.JsonIgnore]
    public bool IsFailedServiceCompletion => !meal && phase == ShopUseExecutionPhase.ServiceCompletion
        && !serviceSucceeded && !string.IsNullOrWhiteSpace(serviceFailureReason)
        && !string.IsNullOrEmpty(serviceSessionId);

    // Cleanup is still owned by this execution, but the cancelled session no
    // longer owns occupancy. Do not infer this from phase or session ID alone.
    public bool MatchesCancelledSession(ServiceSessionSnapshot session, string facilityId) =>
        IsFailedServiceCompletion && session != null && session.Stage == ServiceSessionStage.Cancelled
        && session.TerminalPhase is >= ServiceSessionTerminalPhase.Prepared and <= ServiceSessionTerminalPhase.Acknowledged
        && session.SessionId == serviceSessionId && session.ActorId == actorId && session.HubId == facilityId
        && string.Equals(session.CancellationReason, serviceFailureReason, StringComparison.Ordinal);

    public static string OperationFor(string facilityId, long sequence)
    {
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        return new GameplayOperationId("shop-use:" + new GameplayOperationId(facilityId).Value
            + ":" + sequence.ToString(CultureInfo.InvariantCulture)).Value;
    }

    public ShopUseExecutionState Clone()
    {
        var clone = (ShopUseExecutionState)MemberwiseClone();
        clone.cart = cart?.Select(value => value?.Clone()).ToList();
        clone.patience = patience?.Clone();
        return clone;
    }

    public void Go(ShopUseExecutionPhase next)
    {
        phase = next;
        timerInitialized = false;
        remainingSeconds = 0f;
    }

    // One binding contract for live recovery, detached capture and restore.
    public void ValidatePurchaseBinding(string facilityId, RetailPurchaseRecoverySaveData receipt)
    {
        if (receipt == null || receipt.facilityId != facilityId || receipt.buyerId != actorId
            || receipt.shopUseOperationId != operationId || phase != ShopUseExecutionPhase.Purchase
            || receipt.shopUseCartIndex != purchaseIndex || purchaseIndex < 0 || cart == null
            || purchaseIndex >= cart.Count)
            throw new InvalidOperationException("Retail purchase execution/actor/slot mismatch.");
        ShopUseCartLine line = cart[purchaseIndex];
        if (line == null || !line.pricingCaptured || receipt.lot == null
            || receipt.lot.saleItemId != line.saleItemId
            || receipt.purchaseCost != (createsRevenue ? line.price : 0)
            || receipt.revenueWorkerId != line.workerId
            || receipt.revenuePriceMultiplier != line.priceMultiplier
            || receipt.revenueSkillModifierProvenance != line.modifierProvenance
            || (!line.Committed && line.pendingPurchaseOperationId != receipt.operationId)
            || (line.Committed && (line.purchaseOperationId != receipt.operationId
                || JsonUtility.ToJson(line.committedLot) != JsonUtility.ToJson(receipt.lot))))
            throw new InvalidOperationException("Retail purchase receipt does not match its saved cart line.");
    }

    public bool IsValid(string facilityId, long nextSequence, out string error)
    {
        bool valid = version == CurrentVersion && sequence > 0 && sequence < nextSequence
            && operationId == OperationFor(facilityId, sequence)
            && FacilityUseExecutionState.Canonical(actorId) && FacilityUseExecutionState.Canonical(actionType)
            && Enum.IsDefined(typeof(CharacterAiBranch), actionBranch) && actionBranch != (int)CharacterAiBranch.None
            && FacilityUseExecutionState.Optional(serviceSessionId) && Enum.IsDefined(typeof(ShopUseExecutionPhase), phase)
            && selectionCount >= 0 && selectionIndex >= 0 && selectionIndex <= selectionCount && selectedItemId >= -2
            && cart != null && cart.All(value => value != null && value.IsValid()) && cart.Count <= selectionCount
            && purchaseIndex >= 0 && purchaseIndex <= cart.Count && purchaseCount >= 0 && purchaseCount <= cart.Count
            && purchaseCount == cart.Count(value => value.postCommitApplied)
            && usedMoney == (createsRevenue ? cart.Where(value => value.postCommitApplied).Sum(value => (long)value.price) : 0L)
            && (phase >= ShopUseExecutionPhase.Purchase || cart.All(value => !value.pricingCaptured && !value.Committed))
            && (!waitingForWorker || phase == ShopUseExecutionPhase.Waiting)
            && cart.Select((value, index) => string.IsNullOrEmpty(value.pendingPurchaseOperationId)
                || phase == ShopUseExecutionPhase.Purchase && index == purchaseIndex).All(value => value)
            && usedMoney >= 0 && FacilityUseExecutionState.Canonical(lastPurchaseFailure)
            && FacilityUseExecutionState.Finite(remainingSeconds) && remainingSeconds >= 0f
            && (timerInitialized || remainingSeconds == 0f)
            && FacilityUseExecutionState.Finite(patienceElapsedSeconds) && patienceElapsedSeconds >= 0f
            && (patience == null ? patienceElapsedSeconds == 0f && !waitingForWorker : patience.IsValid())
            && Enum.IsDefined(typeof(BuildingCheckoutWaitStage), waitStage)
            && FacilityUseExecutionState.Optional(serviceFailureReason)
            && FacilityUseExecutionState.Optional(mealOperationId) && mealDisplayName != null && mealUnitPrice >= 0
            && FacilityUseExecutionState.Finite(mealWaitElapsedSeconds) && mealWaitElapsedSeconds >= 0f
            && mealWaitElapsedSeconds <= 15f
            && (!mealSucceeded || meal && !string.IsNullOrWhiteSpace(mealDisplayName))
            && (!mealPaymentCompleted || mealSucceeded) && (!mealRevenueCommitted || mealPaymentCompleted)
            && !meal && phase < ShopUseExecutionPhase.MealApproach && phase != ShopUseExecutionPhase.Complete;
        error = valid ? string.Empty : "Invalid shop-use execution state: " + operationId;
        return valid;
    }
}

[Serializable]
public sealed class ShopInteractionState
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public long nextSequence = 1;
    public List<ShopUseExecutionState> activeUses = new();

    public ShopInteractionState Clone() => new()
    {
        version = version, nextSequence = nextSequence,
        activeUses = activeUses?.Select(value => value?.Clone()).ToList()
    };

    public bool IsValid(string facilityId, out string error)
    {
        error = "Invalid shop interaction owner collection.";
        if (version != CurrentVersion || nextSequence <= 0 || activeUses == null
            || activeUses.Any(value => value == null)
            || activeUses.Select(value => value.actorId).Distinct(StringComparer.Ordinal).Count() != activeUses.Count
            || activeUses.Select(value => value.sequence).Distinct().Count() != activeUses.Count) return false;
        foreach (ShopUseExecutionState use in activeUses)
            if (!use.IsValid(facilityId, nextSequence, out error)) return false;
        error = string.Empty;
        return true;
    }
}
