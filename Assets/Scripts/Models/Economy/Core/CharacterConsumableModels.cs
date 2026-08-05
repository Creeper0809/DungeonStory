using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct ConsumeSubstanceCommand
{
    public ConsumeSubstanceCommand(
        ConsumableOperationId operationId,
        CharacterId characterId,
        ItemDefinitionId itemDefinitionId,
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
    public ItemDefinitionId ItemDefinitionId { get; }
    public ItemStackId ItemStackId { get; }
    public bool MedicalContext { get; }
    public bool CombatContext { get; }
    public bool IsValid => OperationId.IsValid
        && CharacterId.IsValid
        && ItemDefinitionId.IsValid
        && ItemStackId.IsValid;
}

public readonly struct CharacterSubstanceUseRequest
{
    public CharacterSubstanceUseRequest(
        string substanceId,
        ItemDefinitionId itemDefinitionId,
        string displayName,
        float urgency,
        bool medicalContext,
        bool combatContext,
        string reason)
    {
        SubstanceId = substanceId?.Trim() ?? string.Empty;
        ItemDefinitionId = itemDefinitionId;
        DisplayName = displayName ?? string.Empty;
        Urgency = Mathf.Clamp01(urgency);
        MedicalContext = medicalContext;
        CombatContext = combatContext;
        Reason = reason ?? string.Empty;
    }

    public string SubstanceId { get; }
    public ItemDefinitionId ItemDefinitionId { get; }
    public string ItemId => ItemDefinitionId.Value;
    public string DisplayName { get; }
    public float Urgency { get; }
    public bool MedicalContext { get; }
    public bool CombatContext { get; }
    public string Reason { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(SubstanceId)
        && ItemDefinitionId.IsValid
        && Urgency > 0f;
}

public readonly struct MealConsumptionResult
{
    private readonly IReadOnlyList<string> parameters;

    private MealConsumptionResult(
        bool success,
        CharacterConsumablesFailureCode failureCode,
        ItemDefinitionId itemDefinitionId,
        ItemStackId itemStackId,
        string displayName,
        MealDietClass dietClass,
        MealQualityTier quality,
        float nutrition,
        float mood,
        int unitPrice,
        bool policyViolation,
        bool contaminated,
        params string[] parameters)
    {
        Success = success;
        FailureCode = failureCode;
        this.parameters = parameters == null || parameters.Length == 0
            ? Array.Empty<string>()
            : Array.AsReadOnly((string[])parameters.Clone());
        ItemDefinitionId = itemDefinitionId;
        ItemStackId = itemStackId;
        DisplayName = displayName ?? string.Empty;
        DietClass = dietClass;
        Quality = quality;
        Nutrition = Mathf.Max(0f, nutrition);
        Mood = mood;
        UnitPrice = Mathf.Max(0, unitPrice);
        PolicyViolation = policyViolation;
        Contaminated = contaminated;
    }

    public bool Success { get; }
    public CharacterConsumablesFailureCode FailureCode { get; }
    public IReadOnlyList<string> Parameters => parameters ?? Array.Empty<string>();
    public ItemDefinitionId ItemDefinitionId { get; }
    public ItemStackId ItemStackId { get; }
    public string ItemId => ItemDefinitionId.Value;
    public string DisplayName { get; }
    public MealDietClass DietClass { get; }
    public MealQualityTier Quality { get; }
    public float Nutrition { get; }
    public float Mood { get; }
    public int UnitPrice { get; }
    public bool PolicyViolation { get; }
    public bool Contaminated { get; }

    public static MealConsumptionResult Failed(
        CharacterConsumablesFailureCode code,
        params string[] parameters) =>
        new(false, code, default, default, string.Empty,
            MealDietClass.Vegan, MealQualityTier.Simple, 0f, 0f, 0, false, false,
            parameters);

    public static MealConsumptionResult Consumed(
        ItemDefinitionSO item,
        ItemStackId itemStackId,
        bool policyViolation,
        bool contaminated)
    {
        FoodItemFeature food = item?.GetFeatureOrDefault<FoodItemFeature>();
        ResourceItemDefinitionSO resource = item as ResourceItemDefinitionSO;
        return new MealConsumptionResult(
            true,
            CharacterConsumablesFailureCode.None,
            item?.StableId ?? default,
            itemStackId,
            item?.DisplayName ?? string.Empty,
            resource != null ? resource.MealDietClass : MealDietClass.Vegan,
            food?.quality ?? MealQualityTier.Simple,
            food?.nutrition ?? 0f,
            food?.mood ?? 0f,
            item?.UnitPrice ?? 0,
            policyViolation,
            contaminated);
    }

    internal static MealConsumptionResult FromCore(
        CharacterConsumablesMealResult result) =>
        new(
            result.Success,
            result.FailureCode,
            (ItemDefinitionId)result.Meal.Id.Value,
            result.ItemStackId,
            result.Meal.DisplayName,
            result.Meal.DietClass,
            result.Meal.Quality,
            result.Meal.Nutrition,
            result.Meal.Mood,
            result.Meal.UnitPrice,
            result.PolicyViolation,
            result.Contaminated,
            result.Parameters.ToArray());
}

public readonly struct SubstanceUseResult
{
    private readonly IReadOnlyList<string> parameters;

    public SubstanceUseResult(
        bool success,
        CharacterConsumablesFailureCode failureCode,
        string substanceId,
        ItemDefinitionId itemDefinitionId,
        ItemStackId itemStackId,
        string displayName,
        float tolerance,
        float addiction,
        bool becameAddicted,
        bool overdosed,
        params string[] parameters)
    {
        Success = success;
        FailureCode = failureCode;
        this.parameters = parameters == null || parameters.Length == 0
            ? Array.Empty<string>()
            : Array.AsReadOnly((string[])parameters.Clone());
        SubstanceId = substanceId?.Trim() ?? string.Empty;
        ItemDefinitionId = itemDefinitionId;
        ItemStackId = itemStackId;
        DisplayName = displayName ?? string.Empty;
        Tolerance = Mathf.Clamp(tolerance, 0f, 100f);
        Addiction = Mathf.Clamp(addiction, 0f, 100f);
        BecameAddicted = becameAddicted;
        Overdosed = overdosed;
    }

    public bool Success { get; }
    public CharacterConsumablesFailureCode FailureCode { get; }
    public IReadOnlyList<string> Parameters => parameters ?? Array.Empty<string>();
    public string SubstanceId { get; }
    public ItemDefinitionId ItemDefinitionId { get; }
    public ItemStackId ItemStackId { get; }
    public string DisplayName { get; }
    public float Tolerance { get; }
    public float Addiction { get; }
    public bool BecameAddicted { get; }
    public bool Overdosed { get; }

    public static SubstanceUseResult Failed(
        CharacterConsumablesFailureCode code,
        params string[] parameters) =>
        new(false, code, string.Empty, default, default, string.Empty,
            0f, 0f, false, false, parameters);

    public static SubstanceUseResult Failed(
        CharacterConsumablesFailureCode code,
        ItemDefinitionSO item,
        SubstanceItemFeature feature,
        ItemStackId stackId,
        params string[] parameters) =>
        new(false, code, feature?.substanceId, item?.StableId ?? default,
            stackId, item?.DisplayName, 0f, 0f, false, false, parameters);

    internal static SubstanceUseResult FromCore(
        CharacterConsumablesSubstanceResult result) =>
        new(
            result.Success,
            result.FailureCode,
            result.Substance.Definition?.SubstanceId,
            (ItemDefinitionId)result.Substance.Id.Value,
            result.ItemStackId,
            result.Substance.Definition?.DisplayName,
            result.Tolerance,
            result.Addiction,
            result.BecameAddicted,
            result.Overdosed,
            result.Parameters.ToArray());
}

public readonly struct PhysicalMealConsumedEvent
{
    public PhysicalMealConsumedEvent(
        ConsumableOperationId operationId,
        CharacterActor actor,
        BuildableObject facility,
        MealConsumptionResult result)
    {
        OperationId = operationId;
        Actor = actor;
        Facility = facility;
        Result = result;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterActor Actor { get; }
    public BuildableObject Facility { get; }
    public MealConsumptionResult Result { get; }
}

public interface ICharacterDietPolicyQuery
{
    CharacterDietPolicyKind GetPolicy(CharacterActor actor);
    bool IsAllowed(CharacterActor actor, ResourceItemDefinitionSO meal);
}

public interface IMealConsumptionQuery
{
    bool HasMealAvailable(
        CharacterActor actor,
        BuildableObject facility,
        out CharacterConsumablesFailure failure);
}

public interface ICharacterSubstanceQuery
{
    CharacterSubstancePolicyState GetPolicy(CharacterActor actor, string substanceId);
    CharacterSubstanceState GetState(CharacterActor actor, string substanceId);
    bool TryGetAutomaticUseRequest(CharacterActor actor, out CharacterSubstanceUseRequest request);
    float GetWorkSpeedMultiplier(CharacterActor actor);
    float GetCombatMultiplier(CharacterActor actor);
}

public interface ICharacterDietPolicyCommand
{
    void SetPolicy(CharacterActor actor, CharacterDietPolicyKind policy);
}

public interface IMealConsumptionCommand
{
    bool TryConsumeMeal(CharacterActor actor, BuildableObject facility, out MealConsumptionResult result);
    bool TryConsumeMeal(ConsumeMealCommand command, out MealConsumptionResult result);
}

public interface ICharacterSubstanceCommand
{
    void SetPolicy(
        CharacterActor actor,
        string substanceId,
        SubstancePolicyMode mode,
        float moodThreshold = 30f,
        int scheduledHour = 20);
    bool TryConsume(
        CharacterActor actor,
        string substanceId,
        bool medicalContext,
        bool combatContext,
        out SubstanceUseResult result);
    bool TryConsume(ConsumeSubstanceCommand command, out SubstanceUseResult result);
}

public interface ICharacterConsumablesQuery :
    ICharacterDietPolicyQuery,
    IMealConsumptionQuery,
    ICharacterSubstanceQuery
{
}

public interface ICharacterConsumablesCommand :
    ICharacterDietPolicyCommand,
    IMealConsumptionCommand,
    ICharacterSubstanceCommand
{
}

public interface ICharacterDietPolicyRuntime :
    ICharacterDietPolicyQuery,
    ICharacterDietPolicyCommand
{
}

public interface IMealConsumptionRuntime :
    IMealConsumptionQuery,
    IMealConsumptionCommand
{
}

public interface ICharacterSubstanceRuntime :
    ICharacterSubstanceQuery,
    ICharacterSubstanceCommand
{
}
