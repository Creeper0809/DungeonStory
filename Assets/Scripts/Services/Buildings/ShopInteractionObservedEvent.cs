using System;

public enum ShopInteractionTransition
{
    AdmissionRejected, SessionRejected, SelfCheckoutStarted, StaffUseCompleted,
    AttendantWaitingStarted, StaffCheckoutStarted, RetailFailed,
    MealSupplyChanged, MealNoLongerNeeded, MealFailed, MealUnpaid, MealCompleted,
    CheckoutRestless, CheckoutServiceRequested, CheckoutAbandoned, InteractionAborted,
    CheckoutQueueSampled
}

// An immediate observation of the shop owner's applied transition. No activity,
// alert, text template, or display-deduplication state is supplied by the owner.
public readonly struct ShopInteractionObservedEvent
{
    public ShopInteractionObservedEvent(ShopInteractionTransition transition, Shop facility,
        IBuildingVisitorPort actor, string reason = "", string detail = "", string itemName = "",
        float elapsedSeconds = 0f, float patienceMultiplier = 1f, float abandonSentiment = 0f,
        int revenue = 0, int waitingCount = 0, float gameTime = 0f, string facilityName = null)
    {
        if (!Enum.IsDefined(typeof(ShopInteractionTransition), transition) || facility == null
            || !float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f
            || !float.IsFinite(patienceMultiplier) || patienceMultiplier < 0f
            || !float.IsFinite(abandonSentiment) || abandonSentiment < -1f || abandonSentiment > 1f
            || !float.IsFinite(gameTime) || revenue < 0 || waitingCount < 0)
            throw new ArgumentException("Invalid shop interaction observation.");
        Transition = transition; Facility = facility; Actor = actor;
        Reason = reason ?? string.Empty; Detail = detail ?? string.Empty; ItemName = itemName ?? string.Empty;
        ElapsedSeconds = elapsedSeconds; PatienceMultiplier = patienceMultiplier;
        AbandonSentiment = abandonSentiment; Revenue = revenue; WaitingCount = waitingCount;
        GameTime = gameTime; FacilityName = facilityName ?? facility.CustomerDisplayName;
    }
    public ShopInteractionTransition Transition { get; }
    public Shop Facility { get; }
    public IBuildingVisitorPort Actor { get; }
    public string Reason { get; }
    public string Detail { get; }
    public string ItemName { get; }
    public float ElapsedSeconds { get; }
    public float PatienceMultiplier { get; }
    public float AbandonSentiment { get; }
    public int Revenue { get; }
    public int WaitingCount { get; }
    public float GameTime { get; }
    public string FacilityName { get; }
}
