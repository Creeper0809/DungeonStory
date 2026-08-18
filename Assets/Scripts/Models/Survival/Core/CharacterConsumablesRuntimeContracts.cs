using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterConsumablesFailureCode
{
    None,
    InvalidCommand,
    CharacterMissing,
    FacilityMissing,
    ItemDefinitionMissing,
    ItemStackMissing,
    ItemNotConsumable,
    PolicyForbidden,
    DeliveryPending,
    AlreadyProcessed,
    PhysicalConsumptionFailed
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CharacterConsumablesFailure
{
    private readonly IReadOnlyList<string> parameters;

    public CharacterConsumablesFailure(
        CharacterConsumablesFailureCode code,
        params string[] parameters)
    {
        Code = code;
        this.parameters = parameters == null || parameters.Length == 0
            ? Array.Empty<string>()
            : Array.AsReadOnly((string[])parameters.Clone());
    }

    public CharacterConsumablesFailureCode Code { get; }
    public IReadOnlyList<string> Parameters => parameters ?? Array.Empty<string>();
    public bool IsFailure => Code != CharacterConsumablesFailureCode.None;
    public static CharacterConsumablesFailure None =>
        new(CharacterConsumablesFailureCode.None);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CharacterConsumablesPolicyRules
{
    public static SubstancePolicyMode GetDefaultSubstancePolicy(
        SubstanceUseClass useClass) =>
        useClass switch
        {
            SubstanceUseClass.Medicine => SubstancePolicyMode.MedicalOnly,
            SubstanceUseClass.NonAddictive => SubstancePolicyMode.MoodThreshold,
            SubstanceUseClass.Recreational => SubstancePolicyMode.MoodThreshold,
            _ => SubstancePolicyMode.Forbidden
        };

    public static bool AllowsSubstance(
        CharacterSubstancePolicyState policy,
        bool medicalContext,
        bool combatContext,
        float mood) =>
        policy.mode switch
        {
            SubstancePolicyMode.MedicalOnly => medicalContext,
            SubstancePolicyMode.CombatOnly => combatContext,
            SubstancePolicyMode.MoodThreshold => mood <= policy.moodThreshold,
            SubstancePolicyMode.Scheduled => true,
            _ => false
        };

    public static bool AllowsMeal(
        CharacterDietPolicyKind policy,
        MealDietClass dietClass,
        bool containsForbiddenIngredient) =>
        policy switch
        {
            CharacterDietPolicyKind.Vegan => dietClass == MealDietClass.Vegan,
            CharacterDietPolicyKind.Vegetarian =>
                dietClass is MealDietClass.Vegan or MealDietClass.Vegetarian,
            CharacterDietPolicyKind.CarnivorePreferred =>
                dietClass is MealDietClass.Carnivore or MealDietClass.Mixed,
            CharacterDietPolicyKind.StrictTaboo => !containsForbiddenIngredient,
            _ => true
        };
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ConsumeMealCommand
{
    public ConsumeMealCommand(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId,
        ItemStackId itemStackId)
    {
        OperationId = operationId;
        CharacterId = characterId;
        FacilityId = facilityId;
        ItemStackId = itemStackId;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterId CharacterId { get; }
    public BuildingInstanceId FacilityId { get; }
    public ItemStackId ItemStackId { get; }
    public bool IsValid => OperationId.IsValid
        && CharacterId.IsValid
        && FacilityId.IsValid
        && ItemStackId.IsValid;
}

public readonly struct ConsumeSubstanceByIdCommand
{
    public ConsumeSubstanceByIdCommand(
        ConsumableOperationId operationId,
        CharacterId characterId,
        ConsumableItemDefinitionId itemDefinitionId,
        ItemStackId itemStackId,
        bool medicalContext,
        bool combatContext)
    {
        OperationId = operationId;
        CharacterId = characterId;
        ItemDefinitionId = itemDefinitionId;
        ItemStackId = itemStackId;
        MedicalContext = medicalContext;
        CombatContext = combatContext;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterId CharacterId { get; }
    public ConsumableItemDefinitionId ItemDefinitionId { get; }
    public ItemStackId ItemStackId { get; }
    public bool MedicalContext { get; }
    public bool CombatContext { get; }
    public bool IsValid => OperationId.IsValid
        && CharacterId.IsValid
        && ItemDefinitionId.IsValid
        && ItemStackId.IsValid;
}

public enum CharacterConsumablesStackState
{
    Other = 0,
    Loose = 1,
    Stored = 2,
    Carried = 3,
    FacilityBuffer = 4
}

public readonly struct CharacterConsumablesActorSnapshot
{
    public CharacterConsumablesActorSnapshot(
        CharacterId id,
        bool active,
        float health,
        float maxHealth,
        float mood,
        float hunger,
        bool combatStance,
        Vector2Int position = default)
    {
        Id = id;
        Active = active;
        Health = Math.Max(0f, health);
        MaxHealth = Math.Max(1f, maxHealth);
        Mood = mood;
        Hunger = hunger;
        CombatStance = combatStance;
        Position = position;
    }

    public CharacterId Id { get; }
    public bool Active { get; }
    public float Health { get; }
    public float MaxHealth { get; }
    public float Mood { get; }
    public float Hunger { get; }
    public bool CombatStance { get; }
    public Vector2Int Position { get; }
}

public readonly struct CharacterConsumablesFacilitySnapshot
{
    public CharacterConsumablesFacilitySnapshot(
        BuildingInstanceId id,
        bool mealFacility,
        bool recreationalSubstanceFacility,
        Vector2Int position)
    {
        Id = id;
        MealFacility = mealFacility;
        RecreationalSubstanceFacility = recreationalSubstanceFacility;
        Position = position;
    }

    public BuildingInstanceId Id { get; }
    public bool MealFacility { get; }
    public bool RecreationalSubstanceFacility { get; }
    public Vector2Int Position { get; }
}

public readonly struct CharacterConsumablesStackSnapshot
{
    public CharacterConsumablesStackSnapshot(
        ItemStackId stackId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        CharacterConsumablesStackState state,
        string destinationId,
        bool forbidden,
        int reservedQuantity,
        float contamination,
        float freshness01,
        float remainingFreshnessSeconds,
        bool preserved,
        Vector2Int position = default)
    {
        StackId = stackId;
        ItemId = itemId;
        Quantity = Math.Max(0, quantity);
        ReservedQuantity = Math.Max(0, Math.Min(Quantity, reservedQuantity));
        State = state;
        DestinationId = destinationId?.Trim() ?? string.Empty;
        Forbidden = forbidden;
        Contamination = Math.Max(0f, contamination);
        Freshness01 = Clamp01(freshness01);
        RemainingFreshnessSeconds = Math.Max(0f, remainingFreshnessSeconds);
        Preserved = preserved;
        Position = position;
    }

    public CharacterConsumablesStackSnapshot(
        ItemStackId stackId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        CharacterConsumablesStackState state,
        string destinationId,
        bool forbidden,
        bool reserved,
        float contamination,
        float freshness01,
        float remainingFreshnessSeconds,
        bool preserved,
        Vector2Int position = default)
        : this(
            stackId,
            itemId,
            quantity,
            state,
            destinationId,
            forbidden,
            reserved ? quantity : 0,
            contamination,
            freshness01,
            remainingFreshnessSeconds,
            preserved,
            position)
    {
    }

    public ItemStackId StackId { get; }
    public ConsumableItemDefinitionId ItemId { get; }
    public int Quantity { get; }
    public int ReservedQuantity { get; }
    public int AvailableQuantity => Math.Max(0, Quantity - ReservedQuantity);
    public CharacterConsumablesStackState State { get; }
    public string DestinationId { get; }
    public bool Forbidden { get; }
    public bool Reserved => ReservedQuantity > 0;
    public float Contamination { get; }
    public float Freshness01 { get; }
    public float RemainingFreshnessSeconds { get; }
    public bool Preserved { get; }
    public Vector2Int Position { get; }

    private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
}

public readonly struct CharacterConsumablesMealDefinitionSnapshot
{
    public CharacterConsumablesMealDefinitionSnapshot(
        ConsumableItemDefinitionId id,
        string displayName,
        MealDietClass dietClass,
        MealQualityTier quality,
        float nutrition,
        float mood,
        int unitPrice,
        bool forbiddenIngredient,
        bool sweet,
        bool salted,
        MealQualityBand qualityBand = MealQualityBand.Simple,
        MealServingRole servingRole = MealServingRole.FullMeal)
    {
        Id = id;
        DisplayName = displayName?.Trim() ?? string.Empty;
        DietClass = dietClass;
        Quality = quality;
        Nutrition = Math.Max(0f, nutrition);
        Mood = mood;
        UnitPrice = Math.Max(0, unitPrice);
        ForbiddenIngredient = forbiddenIngredient;
        Sweet = sweet;
        Salted = salted;
        QualityBand = qualityBand;
        ServingRole = servingRole;
    }

    public ConsumableItemDefinitionId Id { get; }
    public string DisplayName { get; }
    public MealDietClass DietClass { get; }
    public MealQualityTier Quality { get; }
    public float Nutrition { get; }
    public float Mood { get; }
    public int UnitPrice { get; }
    public bool ForbiddenIngredient { get; }
    public bool Sweet { get; }
    public bool Salted { get; }
    public MealQualityBand QualityBand { get; }
    public MealServingRole ServingRole { get; }
}

public readonly struct CharacterConsumablesSubstanceDefinitionSnapshot
{
    public CharacterConsumablesSubstanceDefinitionSnapshot(
        ConsumableItemDefinitionId id,
        SubstanceDefinitionView definition)
    {
        Id = id;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public ConsumableItemDefinitionId Id { get; }
    public SubstanceDefinitionView Definition { get; }
}

public readonly struct CharacterConsumablesMealResult
{
    private readonly IReadOnlyList<string> parameters;

    private CharacterConsumablesMealResult(
        bool success,
        CharacterConsumablesFailureCode failureCode,
        ConsumableOperationId operationId,
        CharacterConsumablesMealDefinitionSnapshot meal,
        ItemStackId itemStackId,
        bool policyViolation,
        bool contaminated,
        params string[] parameters)
    {
        Success = success;
        FailureCode = failureCode;
        OperationId = operationId;
        Meal = meal;
        ItemStackId = itemStackId;
        PolicyViolation = policyViolation;
        Contaminated = contaminated;
        this.parameters = parameters == null || parameters.Length == 0
            ? Array.Empty<string>()
            : Array.AsReadOnly((string[])parameters.Clone());
    }

    public bool Success { get; }
    public CharacterConsumablesFailureCode FailureCode { get; }
    public ConsumableOperationId OperationId { get; }
    public bool IsAcceptedPending =>
        !Success
        && FailureCode == CharacterConsumablesFailureCode.DeliveryPending
        && OperationId.IsValid;
    public CharacterConsumablesMealDefinitionSnapshot Meal { get; }
    public ItemStackId ItemStackId { get; }
    public bool PolicyViolation { get; }
    public bool Contaminated { get; }
    public IReadOnlyList<string> Parameters => parameters ?? Array.Empty<string>();
    public static CharacterConsumablesMealResult Failed(
        CharacterConsumablesFailureCode code,
        params string[] parameters) =>
        new(false, code, default, default, default, false, false, parameters);
    public static CharacterConsumablesMealResult Pending(
        ConsumableOperationId operationId,
        CharacterConsumablesMealDefinitionSnapshot meal,
        ItemStackId stackId,
        params string[] parameters) =>
        new(false, CharacterConsumablesFailureCode.DeliveryPending, operationId,
            meal, stackId, false, false, parameters);
    public static CharacterConsumablesMealResult Consumed(
        ConsumableOperationId operationId,
        CharacterConsumablesMealDefinitionSnapshot meal,
        ItemStackId stackId,
        bool policyViolation,
        bool contaminated) =>
        new(true, CharacterConsumablesFailureCode.None, operationId, meal, stackId,
            policyViolation, contaminated);
}

public readonly struct CharacterConsumablesSubstanceResult
{
    private readonly IReadOnlyList<string> parameters;

    public CharacterConsumablesSubstanceResult(
        bool success,
        CharacterConsumablesFailureCode failureCode,
        CharacterConsumablesSubstanceDefinitionSnapshot substance,
        ItemStackId itemStackId,
        float tolerance,
        float addiction,
        bool becameAddicted,
        bool overdosed,
        params string[] parameters)
    {
        Success = success;
        FailureCode = failureCode;
        Substance = substance;
        ItemStackId = itemStackId;
        Tolerance = Math.Max(0f, Math.Min(100f, tolerance));
        Addiction = Math.Max(0f, Math.Min(100f, addiction));
        BecameAddicted = becameAddicted;
        Overdosed = overdosed;
        this.parameters = parameters == null || parameters.Length == 0
            ? Array.Empty<string>()
            : Array.AsReadOnly((string[])parameters.Clone());
    }

    public bool Success { get; }
    public CharacterConsumablesFailureCode FailureCode { get; }
    public CharacterConsumablesSubstanceDefinitionSnapshot Substance { get; }
    public ItemStackId ItemStackId { get; }
    public float Tolerance { get; }
    public float Addiction { get; }
    public bool BecameAddicted { get; }
    public bool Overdosed { get; }
    public IReadOnlyList<string> Parameters => parameters ?? Array.Empty<string>();
    public static CharacterConsumablesSubstanceResult Failed(
        CharacterConsumablesFailureCode code,
        params string[] parameters) =>
        new(false, code, default, default, 0f, 0f, false, false, parameters);
}

public readonly struct CharacterConsumablesUseRequest
{
    public CharacterConsumablesUseRequest(
        CharacterConsumablesSubstanceDefinitionSnapshot substance,
        float urgency,
        bool medicalContext,
        bool combatContext,
        string reason)
    {
        Substance = substance;
        Urgency = Math.Max(0f, Math.Min(1f, urgency));
        MedicalContext = medicalContext;
        CombatContext = combatContext;
        Reason = reason?.Trim() ?? string.Empty;
    }

    public CharacterConsumablesSubstanceDefinitionSnapshot Substance { get; }
    public float Urgency { get; }
    public bool MedicalContext { get; }
    public bool CombatContext { get; }
    public string Reason { get; }
    public bool IsValid => Substance.Id.IsValid && Urgency > 0f;
}

public readonly struct CharacterConsumablesMealConsumedEvent
{
    public CharacterConsumablesMealConsumedEvent(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId,
        CharacterConsumablesMealResult result)
    {
        OperationId = operationId;
        CharacterId = characterId;
        FacilityId = facilityId;
        Result = result;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterId CharacterId { get; }
    public BuildingInstanceId FacilityId { get; }
    public CharacterConsumablesMealResult Result { get; }
}

public enum CharacterMealRouteStatus
{
    Pending,
    Reachable,
    Unreachable
}

public interface ICharacterConsumablesWorldPort
{
    IReadOnlyList<CharacterId> CharacterIds { get; }
    IReadOnlyList<BuildingInstanceId> FacilityIds { get; }
    bool TryGetActor(CharacterId id, out CharacterConsumablesActorSnapshot actor);
    bool TryGetFacility(BuildingInstanceId id, out CharacterConsumablesFacilitySnapshot facility);
    CharacterCultureMealPreference GetCultureMealPreference(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId);
    float ProjectGameplayEffect(
        CharacterId characterId,
        string targetId,
        float baseValue);
    float GetBehaviorUtilityMultiplier(
        CharacterId characterId,
        IReadOnlyCollection<string> semanticTags);
    float GetBaseMoodForMealChoice(CharacterId characterId) => 50f;
    CharacterMealRouteStatus GetMealRouteStatus(
        CharacterId characterId,
        Vector2Int from,
        Vector2Int to,
        out float travelSeconds)
    {
        travelSeconds = Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        return CharacterMealRouteStatus.Reachable;
    }
    bool TryReserveMealFacilitySlot(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId) => true;
    void ReleaseMealFacilitySlot(
        ConsumableOperationId operationId,
        BuildingInstanceId facilityId)
    {
    }
    void ApplyBestMealMood(
        CharacterId characterId,
        string label,
        float value,
        float durationSeconds) => ApplyMood(
            characterId,
            "meal:best-active",
            label,
            value,
            durationSeconds);
    void RecoverHunger(CharacterId id, float amount);
    void ApplyMood(CharacterId id, string sourceId, string label, float value, float durationSeconds);
    void ApplyDamage(CharacterId id, float amount, string reason);
    void RecordNeedNarrative(
        CharacterId id,
        string factId,
        string subjectId,
        string outcome,
        float value);
}

/// <summary>
/// Optional identity-policy port. Keeping it separate preserves the engine-core
/// consumables boundary and allows test worlds without identity content.
/// </summary>
public interface ICharacterRitualFastingMealPort
{
    bool IsRitualFasting(CharacterId characterId);
    void RecordMealConsumed(CharacterId characterId, bool directPlayerOrder);
}

public enum CharacterCultureMealPreference
{
    Neutral,
    Preferred,
    Forbidden
}

public enum CharacterMealQualityLimit
{
    Inherit = -1,
    Poor = (int)MealQualityBand.Poor,
    Simple = (int)MealQualityBand.Simple,
    Decent = (int)MealQualityBand.Decent,
    Fine = (int)MealQualityBand.Fine,
    Lavish = (int)MealQualityBand.Lavish
}

public interface ICharacterConsumablesInventoryPort
{
    IReadOnlyList<CharacterConsumablesStackSnapshot> GetAllStacks();
    IReadOnlyList<CharacterConsumablesSubstanceDefinitionSnapshot> GetSubstances();
    bool TryGetMeal(
        ConsumableItemDefinitionId id,
        out CharacterConsumablesMealDefinitionSnapshot meal);
    bool TryResolveSubstance(
        string substanceOrItemId,
        out CharacterConsumablesSubstanceDefinitionSnapshot substance);
    bool TryResolveSubstance(
        ConsumableItemDefinitionId id,
        out CharacterConsumablesSubstanceDefinitionSnapshot substance);
    bool TryConsume(ItemStackId stackId, int quantity);
    bool TryConsumeForCharacter(
        CharacterId characterId,
        ItemStackId stackId,
        int quantity) => TryConsume(stackId, quantity);
    bool TryReserveMealQuantity(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId,
        ItemStackId stackId,
        out string leaseId)
    {
        leaseId = string.Empty;
        return true;
    }
    bool RevalidateMealQuantity(string leaseId, ItemStackId stackId) =>
        string.IsNullOrWhiteSpace(leaseId);
    bool TryResolveMealQuantityStack(
        string leaseId,
        out ItemStackId stackId)
    {
        stackId = default;
        return false;
    }
    bool TryRebindMealQuantityLease(
        ConsumableOperationId operationId,
        out string leaseId,
        out ItemStackId stackId)
    {
        leaseId = string.Empty;
        stackId = default;
        return false;
    }
    bool TryConsumeReservedMealQuantity(
        string leaseId,
        ItemStackId stackId,
        int quantity) => string.IsNullOrWhiteSpace(leaseId)
            && TryConsume(stackId, quantity);
    void ReleaseMealQuantity(string leaseId)
    {
    }
    bool TryRequestDelivery(
        ConsumableItemDefinitionId itemId,
        int quantity,
        Vector2Int position,
        string destinationId,
        out int requested,
        out string failureReason);

}

public enum CharacterMealPlanPhase
{
    Reserved,
    Eating,
    Completed,
    Aborted
}

[Serializable]
public sealed class CharacterMealPlan
{
    public string planId = string.Empty;
    public string characterId = string.Empty;
    public string facilityInstanceId = string.Empty;
    public string sourceStackId = string.Empty;
    public string transportStackId = string.Empty;
    public string itemDefinitionId = string.Empty;
    public string mealQuantityLeaseId = string.Empty;
    public CharacterMealPlanPhase phase;
    public double createdAt;
    public double leaseExpiresAt;
    public float expectedCompletionEta;
    public bool physicalConsumptionCommitted;
    public bool automaticOperation;
    public float beginContamination;
    public bool facilitySlotReserved;
}

public interface ICharacterConsumablesEventPort
{
    void Publish(CharacterConsumablesMealConsumedEvent consumedEvent);
}

public interface ICharacterConsumablesWorkforcePort
{
    void RequestOneHaulerToReplan(CharacterId requestingCharacterId);
}

public interface ICharacterConsumablesApplication
{
    CharacterDietPolicyKind GetDietPolicy(CharacterId characterId);
    void SetDietPolicy(CharacterId characterId, CharacterDietPolicyKind policy);
    CharacterMealQualityLimit GetMealQualityLimit(CharacterId characterId);
    void SetMealQualityLimit(
        CharacterId characterId,
        CharacterMealQualityLimit qualityLimit);
    bool IsMealAllowed(
        CharacterId characterId,
        CharacterConsumablesMealDefinitionSnapshot meal);
    bool HasMealAvailable(
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterConsumablesFailure failure);
    bool TryConsumeMeal(
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterConsumablesMealResult result);
    bool TryConsumeMeal(
        ConsumeMealCommand command,
        out CharacterConsumablesMealResult result);
    bool TryGetMealOperationResult(
        ConsumableOperationId operationId,
        out CharacterConsumablesMealResult result);
    CharacterSubstancePolicyState GetSubstancePolicy(
        CharacterId characterId,
        string substanceId);
    void SetSubstancePolicy(
        CharacterId characterId,
        string substanceId,
        SubstancePolicyMode mode,
        float moodThreshold,
        int scheduledHour);
    CharacterSubstanceState GetSubstanceState(
        CharacterId characterId,
        string substanceId);
    bool TryConsumeSubstance(
        CharacterId characterId,
        string substanceId,
        bool medicalContext,
        bool combatContext,
        out CharacterConsumablesSubstanceResult result);
    bool TryConsumeRecreationalSubstance(
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterConsumablesSubstanceResult result);
    bool TryConsumeSubstance(
        ConsumeSubstanceByIdCommand command,
        out CharacterConsumablesSubstanceResult result);
    bool TryGetAutomaticUseRequest(
        CharacterId characterId,
        out CharacterConsumablesUseRequest request);
    float GetWorkSpeedMultiplier(CharacterId characterId);
    float GetCombatMultiplier(CharacterId characterId);
    void Tick();
}
