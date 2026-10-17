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
    public const CharacterDietPolicyKind DefaultDietPolicy =
        CharacterDietPolicyKind.Free;

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
        Vector2Int position = default,
        string displayName = "",
        bool requiresHungerRecovery = false)
    {
        Id = id;
        Active = active;
        Health = Math.Max(0f, health);
        MaxHealth = Math.Max(1f, maxHealth);
        Mood = mood;
        Hunger = hunger;
        CombatStance = combatStance;
        RequiresHungerRecovery = requiresHungerRecovery;
        Position = position;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? id.Value
            : displayName.Trim();
    }

    public CharacterId Id { get; }
    public bool Active { get; }
    public float Health { get; }
    public float MaxHealth { get; }
    public float Mood { get; }
    public float Hunger { get; }
    public bool CombatStance { get; }
    public bool RequiresHungerRecovery { get; }
    public Vector2Int Position { get; }
    public string DisplayName { get; }
}

public enum CharacterConsumablesPolicyChangeCode
{
    Changed = 0,
    NoChange = 1,
    InvalidRequest = 2,
    OutcomeRejected = 3
}

public readonly struct CharacterConsumablesPolicyChangeResult
{
    public CharacterConsumablesPolicyChangeResult(
        CharacterConsumablesPolicyChangeCode code,
        string policyId,
        string scopeId,
        string beforeValue,
        string afterValue,
        long revision,
        string detailCode)
    {
        Code = code;
        PolicyId = policyId ?? string.Empty;
        ScopeId = scopeId ?? string.Empty;
        BeforeValue = beforeValue ?? string.Empty;
        AfterValue = afterValue ?? string.Empty;
        Revision = Math.Max(0L, revision);
        DetailCode = detailCode ?? string.Empty;
    }

    public CharacterConsumablesPolicyChangeCode Code { get; }
    public string PolicyId { get; }
    public string ScopeId { get; }
    public string BeforeValue { get; }
    public string AfterValue { get; }
    public long Revision { get; }
    public string DetailCode { get; }
    public bool Succeeded => Code is CharacterConsumablesPolicyChangeCode.Changed
        or CharacterConsumablesPolicyChangeCode.NoChange;
    public bool Changed => Code == CharacterConsumablesPolicyChangeCode.Changed;
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

public readonly struct CharacterDetoxMedicineDefinitionSnapshot
{
    public CharacterDetoxMedicineDefinitionSnapshot(
        ConsumableItemDefinitionId id,
        string displayName,
        float detoxReduction)
    {
        if (!id.IsValid)
            throw new ArgumentException("Detox medicine ID is invalid.", nameof(id));
        if (float.IsNaN(detoxReduction)
            || float.IsInfinity(detoxReduction)
            || detoxReduction <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(detoxReduction));
        }
        Id = id;
        DisplayName = displayName?.Trim() ?? string.Empty;
        DetoxReduction = detoxReduction;
    }

    public ConsumableItemDefinitionId Id { get; }
    public string DisplayName { get; }
    public float DetoxReduction { get; }
}

public static class CharacterToxicityPolicy
{
    public const float OverdoseGain = 30f;
    public const float NaturalRecoveryPerDay = 30f;
    public const float MaximumToxicity = 100f;
    public const float MaximumPerformancePenalty = 0.20f;

    public static float Clamp(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        return Math.Max(0f, Math.Min(MaximumToxicity, value));
    }

    public static float GetPerformancePenalty(float toxicity) =>
        MaximumPerformancePenalty * Clamp(toxicity) / MaximumToxicity;
}

public readonly struct CharacterToxicityStatus
{
    public CharacterToxicityStatus(
        float toxicity,
        bool treatmentAvailable,
        bool treatmentPending,
        string treatmentUnavailableReason)
    {
        Toxicity = CharacterToxicityPolicy.Clamp(toxicity);
        PerformancePenalty =
            CharacterToxicityPolicy.GetPerformancePenalty(Toxicity);
        TreatmentAvailable = treatmentAvailable;
        TreatmentPending = treatmentPending;
        TreatmentUnavailableReason = treatmentUnavailableReason?.Trim()
            ?? string.Empty;
    }

    public float Toxicity { get; }
    public float PerformancePenalty { get; }
    public bool TreatmentAvailable { get; }
    public bool TreatmentPending { get; }
    public string TreatmentUnavailableReason { get; }
}

public readonly struct CharacterDetoxTreatmentResult
{
    public CharacterDetoxTreatmentResult(
        bool success,
        CharacterConsumablesFailureCode failureCode,
        ConsumableOperationId operationId,
        ConsumableItemDefinitionId medicineId,
        ItemStackId sourceStackId,
        float appliedReduction,
        float remainingToxicity,
        string detail)
    {
        Success = success;
        FailureCode = failureCode;
        OperationId = operationId;
        MedicineId = medicineId;
        SourceStackId = sourceStackId;
        AppliedReduction = appliedReduction;
        RemainingToxicity = remainingToxicity;
        Detail = detail?.Trim() ?? string.Empty;
    }

    public bool Success { get; }
    public CharacterConsumablesFailureCode FailureCode { get; }
    public ConsumableOperationId OperationId { get; }
    public ConsumableItemDefinitionId MedicineId { get; }
    public ItemStackId SourceStackId { get; }
    public float AppliedReduction { get; }
    public float RemainingToxicity { get; }
    public string Detail { get; }
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
        string outcomeResultKey,
        params string[] parameters)
    {
        Success = success;
        FailureCode = failureCode;
        OperationId = operationId;
        Meal = meal;
        ItemStackId = itemStackId;
        PolicyViolation = policyViolation;
        Contaminated = contaminated;
        OutcomeResultKey = outcomeResultKey?.Trim() ?? string.Empty;
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
    public string OutcomeResultKey { get; }
    public IReadOnlyList<string> Parameters => parameters ?? Array.Empty<string>();
    public static CharacterConsumablesMealResult Failed(
        CharacterConsumablesFailureCode code,
        params string[] parameters) =>
        new(false, code, default, default, default, false, false,
            string.Empty, parameters);
    public static CharacterConsumablesMealResult Pending(
        ConsumableOperationId operationId,
        CharacterConsumablesMealDefinitionSnapshot meal,
        ItemStackId stackId,
        params string[] parameters) =>
        new(false, CharacterConsumablesFailureCode.DeliveryPending, operationId,
            meal, stackId, false, false, string.Empty, parameters);
    public static CharacterConsumablesMealResult Aborted(
        ConsumableOperationId operationId,
        CharacterConsumablesFailureCode code,
        string detail,
        string outcomeResultKey = "") =>
        new(false, code, operationId, default, default, false, false,
            outcomeResultKey, operationId.Value, detail);
    public static CharacterConsumablesMealResult Consumed(
        ConsumableOperationId operationId,
        CharacterConsumablesMealDefinitionSnapshot meal,
        ItemStackId stackId,
        bool policyViolation,
        bool contaminated,
        string outcomeResultKey = "") =>
        new(true, CharacterConsumablesFailureCode.None, operationId, meal, stackId,
            policyViolation, contaminated, outcomeResultKey);
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
        string outcomeResultKey,
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
        OutcomeResultKey = outcomeResultKey?.Trim() ?? string.Empty;
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
    public string OutcomeResultKey { get; }
    public IReadOnlyList<string> Parameters => parameters ?? Array.Empty<string>();
    public static CharacterConsumablesSubstanceResult Failed(
        CharacterConsumablesFailureCode code,
        params string[] parameters) =>
        new(false, code, default, default, 0f, 0f, false, false,
            string.Empty, parameters);
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
    bool CanReceiveMealPostCommitObservers(CharacterId id) =>
        TryGetActor(id, out CharacterConsumablesActorSnapshot actor) && actor.Health > 0f;
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
    void ApplyContaminatedMealDamage(
        CharacterId id,
        float amount,
        string reason) => ApplyDamage(id, amount, reason);
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
    bool RecordMealConsumed(CharacterId characterId, bool directPlayerOrder,
        CharacterMealConsumedFact meal, string mealResultKey, out string failureReason);
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
    IReadOnlyList<CharacterDetoxMedicineDefinitionSnapshot> GetDetoxMedicines() =>
        Array.Empty<CharacterDetoxMedicineDefinitionSnapshot>();
    bool TryGetMeal(
        ConsumableItemDefinitionId id,
        out CharacterConsumablesMealDefinitionSnapshot meal);
    bool TryResolveSubstance(
        string substanceOrItemId,
        out CharacterConsumablesSubstanceDefinitionSnapshot substance);
    bool TryResolveSubstance(
        ConsumableItemDefinitionId id,
        out CharacterConsumablesSubstanceDefinitionSnapshot substance);
    bool TryResolveDetoxMedicine(
        ConsumableItemDefinitionId id,
        out CharacterDetoxMedicineDefinitionSnapshot medicine)
    {
        medicine = default;
        return false;
    }
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
    bool TryCommitReservedMealQuantityPending(
        ConsumableOperationId operationId,
        string leaseId,
        int quantity,
        out CharacterMealPhysicalCommitSnapshot commit,
        out string failureReason)
    {
        commit = default;
        failureReason = "meal-pending-disposition-unavailable";
        return false;
    }
    bool TryGetPendingMealConsumption(
        ConsumableOperationId operationId,
        out CharacterMealPhysicalCommitSnapshot commit)
    {
        commit = default;
        return false;
    }
    bool TryAcknowledgeMealConsumption(
        string commitId,
        out string failureReason)
    {
        failureReason = "meal-pending-disposition-unavailable";
        return false;
    }
    bool TryAcknowledgeMealConsumption(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        string commitId,
        out string failureReason) =>
        TryAcknowledgeMealConsumption(commitId, out failureReason);
    bool TryAcknowledgeMealConsumption(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        string commitId,
        Vector2Int consumptionPosition,
        out string failureReason) =>
        TryAcknowledgeMealConsumption(characterId, itemId, quantity, commitId, out failureReason);
    bool TryCommitSubstanceConsumptionPending(
        ConsumableOperationId operationId,
        CharacterId characterId,
        ItemStackId stackId,
        out CharacterSubstancePhysicalCommitSnapshot commit,
        out string failureReason)
    {
        commit = default;
        failureReason = "substance-pending-disposition-unavailable";
        return false;
    }
    bool TryGetPendingSubstanceConsumption(
        ConsumableOperationId operationId,
        out CharacterSubstancePhysicalCommitSnapshot commit)
    {
        commit = default;
        return false;
    }
    bool TryAcknowledgeSubstanceConsumption(
        string commitId,
        out string failureReason)
    {
        failureReason = "substance-pending-disposition-unavailable";
        return false;
    }
    bool TryAcknowledgeSubstanceConsumption(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        string commitId,
        out string failureReason) =>
        TryAcknowledgeSubstanceConsumption(commitId, out failureReason);
    bool TryCommitDetoxConsumptionPending(
        ConsumableOperationId operationId,
        ItemStackId stackId,
        out CharacterDetoxPhysicalCommitSnapshot commit,
        out string failureReason)
    {
        commit = default;
        failureReason = "detox-pending-disposition-unavailable";
        return false;
    }
    bool TryGetPendingDetoxConsumption(
        ConsumableOperationId operationId,
        out CharacterDetoxPhysicalCommitSnapshot commit)
    {
        commit = default;
        return false;
    }
    bool TryAcknowledgeDetoxConsumption(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        string commitId,
        out string failureReason)
    {
        failureReason = "detox-pending-disposition-unavailable";
        return false;
    }
    void ReleaseMealQuantity(ConsumableOperationId operationId)
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
    ItemCommitted,
    EffectsPublished,
    Completed,
    Aborted,
    EffectsApplied
}

public readonly struct CharacterMealPhysicalCommitSnapshot
{
    public CharacterMealPhysicalCommitSnapshot(
        string operationId,
        string reasonCode,
        string commitId,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams)
    {
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        CommitId = commitId ?? string.Empty;
        SourceStackIds = sourceStackIds ?? Array.Empty<string>();
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public string CommitId { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
}

public readonly struct CharacterSubstancePhysicalCommitSnapshot
{
    public CharacterSubstancePhysicalCommitSnapshot(
        string operationId,
        string reasonCode,
        string commitId,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams)
    {
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        CommitId = commitId ?? string.Empty;
        SourceStackIds = sourceStackIds ?? Array.Empty<string>();
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public string CommitId { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
}

public readonly struct CharacterConsumablesEffectEvidence
{
    public CharacterConsumablesEffectEvidence(
        float hungerBefore,
        float hungerAfter,
        float healthBefore,
        float healthAfter,
        float moodBefore,
        float moodAfter)
    {
        HungerBefore = hungerBefore;
        HungerAfter = hungerAfter;
        HealthBefore = healthBefore;
        HealthAfter = healthAfter;
        MoodBefore = moodBefore;
        MoodAfter = moodAfter;
    }

    public float HungerBefore { get; }
    public float HungerAfter { get; }
    public float HealthBefore { get; }
    public float HealthAfter { get; }
    public float MoodBefore { get; }
    public float MoodAfter { get; }
}

public readonly struct CharacterConsumablesOutcomeEvidence
{
    public CharacterConsumablesOutcomeEvidence(
        BuildingInstanceId facilityId,
        ConsumableItemDefinitionId itemDefinitionId,
        IReadOnlyList<string> sourceStackIds,
        string physicalCommitId,
        int consumedQuantity,
        long consumedMassGrams,
        in CharacterConsumablesEffectEvidence effects,
        bool policyViolation,
        bool contaminated,
        bool contaminationDamageApplied,
        string effectsSkippedReason = "")
    {
        FacilityId = facilityId;
        ItemDefinitionId = itemDefinitionId;
        SourceStackIds = sourceStackIds ?? Array.Empty<string>();
        PhysicalCommitId = physicalCommitId?.Trim() ?? string.Empty;
        ConsumedQuantity = consumedQuantity;
        ConsumedMassGrams = consumedMassGrams;
        Effects = effects;
        PolicyViolation = policyViolation;
        Contaminated = contaminated;
        ContaminationDamageApplied = contaminationDamageApplied;
        EffectsSkippedReason = effectsSkippedReason ?? string.Empty;
    }

    public BuildingInstanceId FacilityId { get; }
    public ConsumableItemDefinitionId ItemDefinitionId { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public string PhysicalCommitId { get; }
    public int ConsumedQuantity { get; }
    public long ConsumedMassGrams { get; }
    public CharacterConsumablesEffectEvidence Effects { get; }
    public bool PolicyViolation { get; }
    public bool Contaminated { get; }
    public bool ContaminationDamageApplied { get; }
    public string EffectsSkippedReason { get; }
}

public readonly struct CharacterDetoxPhysicalCommitSnapshot
{
    public CharacterDetoxPhysicalCommitSnapshot(
        string operationId,
        string reasonCode,
        string commitId,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams)
    {
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        CommitId = commitId ?? string.Empty;
        SourceStackIds = sourceStackIds ?? Array.Empty<string>();
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public string CommitId { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
}

[Serializable]
public sealed class CharacterMealPlan
{
    public bool ritualFastingConsequenceDelivered;
    // Frozen at the physical sink, before effect callbacks can end the actor's lifetime.
    public bool physicalContextFrozen;
    public Vector2Int consumptionPosition;
    public string effectsSkippedReason = string.Empty;
    public string planId = string.Empty;
    public string characterId = string.Empty;
    public string facilityInstanceId = string.Empty;
    public string sourceStackId = string.Empty;
    public string transportStackId = string.Empty;
    public string itemDefinitionId = string.Empty;
    public string mealQuantityLeaseId = string.Empty;
    public CharacterMealPlanPhase phase;
    public int factAbsoluteDay;
    public string publicationKey = string.Empty;
    public bool publicationPending;
    public double createdAt;
    public double leaseExpiresAt;
    public float expectedCompletionEta;
    public bool physicalConsumptionCommitted;
    public bool automaticOperation;
    public float beginContamination;
    public bool facilitySlotReserved;
    public string physicalCommitOperationId = string.Empty;
    public string physicalCommitReasonCode = string.Empty;
    public string physicalCommitId = string.Empty;
    public List<string> physicalCommitSourceStackIds = new();
    public int physicalCommitQuantity;
    public long physicalCommitInputMassGrams;
    public bool committedPolicyViolation;
    public bool committedContaminated;
    public bool contaminationRollFrozen;
    public bool resolvedContaminationDamage;
    public bool primitiveSurvivalOutcomeRequired;
    public string primitivePublicationKey = string.Empty;
    public float primitiveNutrition;
    public bool recipientActiveAtCompletion;
    public bool postCommitEventDelivered;
    public bool effectEvidenceFrozen;
    public float hungerBefore;
    public float hungerAfter;
    public float healthBefore;
    public float healthAfter;
    public float moodBefore;
    public float moodAfter;
}

public enum CharacterSubstanceUsePlanPhase
{
    ItemCommitted = 0,
    EffectsPublished = 1,
    EffectsApplied = 2
}

public enum CharacterDetoxTreatmentPlanPhase
{
    ItemCommitted = 0,
    EffectsPublished = 1,
    PhysicalAcknowledged = 2
}

[Serializable]
public sealed class CharacterDetoxTreatmentPlan
{
    public string operationId = string.Empty;
    public string characterId = string.Empty;
    public string facilityInstanceId = string.Empty;
    public string itemDefinitionId = string.Empty;
    public string sourceStackId = string.Empty;
    public CharacterDetoxTreatmentPlanPhase phase;
    public int factAbsoluteDay;
    public string publicationKey = string.Empty;
    public bool publicationPending;
    public float detoxReduction;
    public float appliedReduction;
    public string physicalCommitOperationId = string.Empty;
    public string physicalCommitReasonCode = string.Empty;
    public string physicalCommitId = string.Empty;
    public List<string> physicalCommitSourceStackIds = new();
    public int physicalCommitQuantity;
    public long physicalCommitInputMassGrams;
}

[Serializable]
public sealed class CharacterSubstanceUsePlan
{
    public string operationId = string.Empty;
    public string characterId = string.Empty;
    public string itemDefinitionId = string.Empty;
    public string sourceStackId = string.Empty;
    public CharacterSubstanceUsePlanPhase phase;
    public int factAbsoluteDay;
    public string publicationKey = string.Empty;
    public bool publicationPending;
    public bool automaticOperation;
    public string physicalCommitOperationId = string.Empty;
    public string physicalCommitReasonCode = string.Empty;
    public string physicalCommitId = string.Empty;
    public List<string> physicalCommitSourceStackIds = new();
    public int physicalCommitQuantity;
    public long physicalCommitInputMassGrams;
    public float resolvedTolerance;
    public float resolvedAddiction;
    public float resolvedWithdrawal;
    public float resolvedActiveSeconds;
    public float resolvedSecondsSinceLastDose;
    public float resolvedScheduledCooldownSeconds;
    public float effectToleranceRatio;
    public bool resolvedAddicted;
    public bool resolvedOverdosed;
    public bool becameAddicted;
    public float resolvedEffectMood;
    public float resolvedOverdoseDamage;
}

public readonly struct CharacterMealEffectCommand
{
    public CharacterMealEffectCommand(
        ConsumableOperationId operationId,
        CharacterId characterId,
        float nutrition,
        float mood,
        bool policyViolation,
        bool applyContaminationDamage,
        string itemId,
        string displayName)
    {
        OperationId = operationId;
        CharacterId = characterId;
        Nutrition = nutrition;
        Mood = mood;
        PolicyViolation = policyViolation;
        ApplyContaminationDamage = applyContaminationDamage;
        ItemId = itemId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterId CharacterId { get; }
    public float Nutrition { get; }
    public float Mood { get; }
    public bool PolicyViolation { get; }
    public bool ApplyContaminationDamage { get; }
    public string ItemId { get; }
    public string DisplayName { get; }
}

public readonly struct CharacterSubstanceEffectCommand
{
    public CharacterSubstanceEffectCommand(
        ConsumableOperationId operationId,
        CharacterId characterId,
        string itemId,
        string displayName,
        float mood,
        float durationSeconds,
        float tolerance,
        bool overdosed,
        float overdoseDamage)
    {
        OperationId = operationId;
        CharacterId = characterId;
        ItemId = itemId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Mood = mood;
        DurationSeconds = durationSeconds;
        Tolerance = tolerance;
        Overdosed = overdosed;
        OverdoseDamage = overdoseDamage;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterId CharacterId { get; }
    public string ItemId { get; }
    public string DisplayName { get; }
    public float Mood { get; }
    public float DurationSeconds { get; }
    public float Tolerance { get; }
    public bool Overdosed { get; }
    public float OverdoseDamage { get; }
}

public interface ICharacterConsumablesEffectTransactionPort
{
    bool TryApplyMealEffects(
        in CharacterMealEffectCommand command,
        out CharacterConsumablesEffectEvidence evidence,
        out string failureReason);
    bool TryApplySubstanceEffects(
        in CharacterSubstanceEffectCommand command,
        out string failureReason);
}

public interface ICharacterConsumablesEventPort
{
    void Publish(CharacterConsumablesMealConsumedEvent consumedEvent);
}

public interface ICharacterConsumablesWorkforcePort
{
    void RequestOneHaulerToReplan(CharacterId requestingCharacterId);
}

public static class CharacterSubstanceEffectMultiplierAuthority
{
    public const string Schema =
        "character-substance-effect-multiplier-authority@1";
    public const double NeutralMultiplier = 1d;
    public const double WithdrawalPenaltyPerPoint = 0.0025d;
    public const double MinimumMultiplier = 0.45d;
    public const double MaximumMultiplier = 1.75d;

    public static float Resolve(
        double activeEffectTotal,
        double withdrawalPenaltyTotal)
    {
        if (double.IsNaN(activeEffectTotal)
            || double.IsInfinity(activeEffectTotal)
            || double.IsNaN(withdrawalPenaltyTotal)
            || double.IsInfinity(withdrawalPenaltyTotal)
            || withdrawalPenaltyTotal < 0d)
        {
            throw new InvalidOperationException(
                "Character substance effect aggregate must be finite.");
        }
        double value = NeutralMultiplier
            + activeEffectTotal
            - withdrawalPenaltyTotal;
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new InvalidOperationException(
                "Character substance effect result must be finite.");
        return (float)Math.Clamp(value, MinimumMultiplier, MaximumMultiplier);
    }
}

public interface ICharacterConsumablesApplication
{
    CharacterDietPolicyKind GetDietPolicy(CharacterId characterId);
    CharacterConsumablesPolicyChangeResult SetDietPolicy(
        CharacterId characterId,
        CharacterDietPolicyKind policy,
        string source = "runtime-command");
    CharacterMealQualityLimit GetMealQualityLimit(CharacterId characterId);
    CharacterConsumablesPolicyChangeResult SetMealQualityLimit(
        CharacterId characterId,
        CharacterMealQualityLimit qualityLimit,
        string source = "runtime-command");
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
    bool TryConsumePermittedMeal(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterConsumablesMealResult result);
    bool TryGetMealOperationResult(
        ConsumableOperationId operationId,
        out CharacterConsumablesMealResult result);
    CharacterSubstancePolicyState GetSubstancePolicy(
        CharacterId characterId,
        string substanceId);
    CharacterConsumablesPolicyChangeResult SetSubstancePolicy(
        CharacterId characterId,
        string substanceId,
        SubstancePolicyMode mode,
        float moodThreshold,
        int scheduledHour,
        string source = "runtime-command");
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
    CharacterToxicityStatus GetToxicityStatus(CharacterId characterId);
    bool TryApplyDetoxTreatment(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterDetoxTreatmentResult result);
    bool TryGetPendingDetoxTreatment(
        CharacterId characterId,
        out BuildingInstanceId facilityId,
        out ConsumableOperationId operationId);
    bool TryGetPendingDetoxTreatment(
        CharacterId characterId,
        out BuildingInstanceId facilityId,
        out ConsumableOperationId operationId,
        out ConsumableItemDefinitionId itemId);
    bool TryGetDetoxTreatmentOwner(
        ConsumableOperationId operationId,
        out CharacterId characterId,
        out BuildingInstanceId facilityId,
        out bool completed);
    bool TryAcknowledgeDetoxTreatment(
        ConsumableOperationId operationId);
    void ProcessOperatingDay(int day);
    void Tick();
}
