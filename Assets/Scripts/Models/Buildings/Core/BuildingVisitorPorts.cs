using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BuildingVisitOutcome
{
    None = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Abandoned = 4
}

public static class BuildingActivityKinds
{
    public const string FacilityUse = "activity:facility-use";
    public const string Shopping = "activity:shopping";
    public const string Social = "activity:social";
    public const string Stock = "activity:stock";
    public const string Work = "activity:work";
}

public static class BuildingActivityOutcomes
{
    public const string Progress = "outcome:progress";
    public const string Completed = "outcome:completed";
    public const string Failed = "outcome:failed";
    public const string Cancelled = "outcome:cancelled";
    public const string Responded = "outcome:responded";
    public const string Damaged = "outcome:damaged";
    public const string Blocked = "outcome:blocked";
    public const string Started = "outcome:started";
}

public readonly struct BuildingVisitorSnapshot
{
    public BuildingVisitorSnapshot(
        string persistentId,
        string displayName,
        Vector3 position,
        bool isRuntimeActive,
        bool isInternalStaff,
        bool canMove,
        float stayDurationMultiplier,
        float revenueMultiplier,
        float personalityPatience,
        float modelPatience,
        float crimeRiskMultiplier,
        float productionOutputMultiplier,
        int stockProductionBonus,
        float expeditionStress,
        float mood,
        float hunger,
        float fun,
        float sleep,
        float excretion,
        float hygiene)
    {
        PersistentId = persistentId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Position = position;
        IsRuntimeActive = isRuntimeActive;
        IsInternalStaff = isInternalStaff;
        CanMove = canMove;
        StayDurationMultiplier = stayDurationMultiplier;
        RevenueMultiplier = revenueMultiplier;
        PersonalityPatience = personalityPatience;
        ModelPatience = modelPatience;
        CrimeRiskMultiplier = crimeRiskMultiplier;
        ProductionOutputMultiplier = productionOutputMultiplier;
        StockProductionBonus = stockProductionBonus;
        ExpeditionStress = expeditionStress;
        Mood = mood;
        Hunger = hunger;
        Fun = fun;
        Sleep = sleep;
        Excretion = excretion;
        Hygiene = hygiene;
    }

    public string PersistentId { get; }
    public string DisplayName { get; }
    public Vector3 Position { get; }
    public bool IsRuntimeActive { get; }
    public bool IsInternalStaff { get; }
    public bool CanMove { get; }
    public float StayDurationMultiplier { get; }
    public float RevenueMultiplier { get; }
    public float PersonalityPatience { get; }
    public float ModelPatience { get; }
    public float CrimeRiskMultiplier { get; }
    public float ProductionOutputMultiplier { get; }
    public int StockProductionBonus { get; }
    public float ExpeditionStress { get; }
    public float Mood { get; }
    public float Hunger { get; }
    public float Fun { get; }
    public float Sleep { get; }
    public float Excretion { get; }
    public float Hygiene { get; }
}

public readonly struct BuildingActivitySnapshot
{
    public BuildingActivitySnapshot(
        string kindId,
        string outcomeId,
        string factText,
        string actionId = "",
        string reasonCode = "",
        float value = 0f,
        int quantity = 0,
        float sentiment = 0f,
        bool bubbleEligible = false)
    {
        KindId = kindId ?? string.Empty;
        OutcomeId = outcomeId ?? string.Empty;
        FactText = factText ?? string.Empty;
        ActionId = actionId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        Value = value;
        Quantity = quantity;
        Sentiment = sentiment;
        BubbleEligible = bubbleEligible;
        WorkTypeId = string.Empty;
    }

    public BuildingActivitySnapshot(
        string kindId,
        string outcomeId,
        string factText,
        string workTypeId,
        string actionId,
        string reasonCode,
        float value,
        int quantity,
        bool bubbleEligible)
        : this(
            kindId,
            outcomeId,
            factText,
            actionId,
            reasonCode,
            value,
            quantity,
            0f,
            bubbleEligible)
    {
        WorkTypeId = workTypeId ?? string.Empty;
    }

    public string KindId { get; }
    public string OutcomeId { get; }
    public string FactText { get; }
    public string ActionId { get; }
    public string ReasonCode { get; }
    public float Value { get; }
    public int Quantity { get; }
    public float Sentiment { get; }
    public bool BubbleEligible { get; }
    public string WorkTypeId { get; }
}

public readonly struct BuildingRetailOfferSnapshot
{
    public BuildingRetailOfferSnapshot(int itemId, int cost)
    {
        ItemId = itemId;
        Cost = cost;
    }

    public int ItemId { get; }
    public int Cost { get; }
}

public readonly struct BuildingNeedRecoverySnapshot
{
    public BuildingNeedRecoverySnapshot(
        float sleep,
        float mood,
        float fun,
        float hunger,
        float excretion,
        float hygiene,
        string sourceId,
        IReadOnlyList<string> activeConditionIds,
        string sourceName)
        : this(
            sleep,
            mood,
            fun,
            hunger,
            excretion,
            hygiene,
            sourceId,
            sourceName,
            activeConditionIds)
    {
    }

    public BuildingNeedRecoverySnapshot(
        float sleep,
        float mood,
        float fun,
        float hunger,
        float excretion,
        float hygiene,
        string sourceId,
        string sourceName,
        IReadOnlyList<string> activeConditionIds = null)
    {
        Sleep = sleep;
        Mood = mood;
        Fun = fun;
        Hunger = hunger;
        Excretion = excretion;
        Hygiene = hygiene;
        SourceId = sourceId ?? string.Empty;
        SourceName = sourceName ?? string.Empty;
        ActiveConditionIds = activeConditionIds ?? System.Array.Empty<string>();
    }

    public float Sleep { get; }
    public float Mood { get; }
    public float Fun { get; }
    public float Hunger { get; }
    public float Excretion { get; }
    public float Hygiene { get; }
    public string SourceId { get; }
    public string SourceName { get; }
    public IReadOnlyList<string> ActiveConditionIds { get; }
}

public readonly struct BuildingMealUseSnapshot
{
    public BuildingMealUseSnapshot(
        bool success,
        string failureCode,
        string displayName,
        int unitPrice,
        bool acceptedPending = false,
        string operationId = "",
        string failureDetail = "")
    {
        Success = success;
        FailureCode = failureCode ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        UnitPrice = unitPrice;
        AcceptedPending = acceptedPending;
        OperationId = operationId ?? string.Empty;
        FailureDetail = failureDetail ?? string.Empty;
    }

    public bool Success { get; }
    public string FailureCode { get; }
    public string DisplayName { get; }
    public int UnitPrice { get; }
    public bool AcceptedPending { get; }
    public string OperationId { get; }
    public string FailureDetail { get; }
}

public readonly struct BuildingRecreationalSubstanceUseSnapshot
{
    public BuildingRecreationalSubstanceUseSnapshot(
        bool success,
        string failureCode,
        string displayName,
        bool becameAddicted,
        bool overdosed)
    {
        Success = success;
        FailureCode = failureCode ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        BecameAddicted = becameAddicted;
        Overdosed = overdosed;
    }

    public bool Success { get; }
    public string FailureCode { get; }
    public string DisplayName { get; }
    public bool BecameAddicted { get; }
    public bool Overdosed { get; }
}

public interface IBuildingShoppingVisitorPort
{
    BuildingVisitOutcome LastVisitOutcome { get; }
    int GetShoppingCount();
    int SelectOffer(IReadOnlyList<BuildingRetailOfferSnapshot> offers);
    bool CanPay(int amount);
    IEnumerator Purchase(object stockToken, int cost);
    IEnumerator PayForService(int amount);
    void SetVisitOutcome(
        IBuildingWorldEntryPort building,
        BuildingVisitOutcome outcome);
}

public interface IBuildingWorkforceReplanPort
{
    void RequestIdleWorkersToReplan(bool clearFailures = true);
}

public interface IBuildingVisitorPort : IBuildingCharacterPort
{
    BuildingVisitorSnapshot VisitorSnapshot { get; }
    IBuildingShoppingVisitorPort Shopping { get; }
    object CurrentActionToken { get; }
    bool IsCurrentAction(object expectedAction);
    bool IsCurrentActionEnded { get; }
    void SetActionPhase(
        string phase,
        IBuildingWorldEntryPort destination,
        string detail = null);
    IEnumerator MoveTo(Vector3 position, float speed, object expectedAction);
    IEnumerator MoveToGrid(Vector2Int position);
    void SetWorldPosition(Vector3 position);
    void HideForTraversal(float failSafeDelay);
    void RestoreTraversalVisibility();
    void ChangeLayer(string layerName);
    void FaceRight();
    void ApplyMoodFactor(
        string sourceId,
        string description,
        float amount,
        float duration,
        int stackLimit);
    void RecordActivity(
        IBuildingWorldEntryPort facility,
        BuildingActivitySnapshot activity);
    void RememberFacilityExperience(
        IBuildingWorldEntryPort facility,
        float sentiment,
        string detail);
    void ApplyNeedRecovery(BuildingNeedRecoverySnapshot recovery);
    bool TryConsumeMeal(
        object mealRuntime,
        IBuildingWorldEntryPort facility,
        out BuildingMealUseSnapshot result);
    bool TryGetMealConsumptionResult(
        object mealRuntime,
        string operationId,
        out BuildingMealUseSnapshot result);
    bool TryConsumeRecreationalSubstance(
        IBuildingWorldEntryPort facility,
        out BuildingRecreationalSubstanceUseSnapshot result);
    void ApplyRoomExperience(
        object roomExperienceRuntime,
        IBuildingWorldEntryPort facility,
        string activityId);
    void ApplyFacilityUseCompleted(IBuildingWorldEntryPort facility);
    void ApplyExpeditionRecovery(
        float healthHealRatio,
        float injuryReduction,
        float stressRecovery);
    void AddExperience(int amount);
    void ApplyNeedDelta(string needId, float amount);
    void AddCarriedItem(string sourceId, string itemDefinitionId, int quantity);
}

public interface IBuildingCharacterDisplayQuery
{
    bool TryGetDisplayName(string persistentId, out string displayName);
}
