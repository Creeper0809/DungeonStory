using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterDietPolicyState
{
    public string characterId = string.Empty;
    public CharacterDietPolicyKind policy = CharacterDietPolicyKind.Free;
}

[Serializable]
public sealed class CharacterSubstancePolicyState
{
    public string characterId = string.Empty;
    public string substanceId = string.Empty;
    public SubstancePolicyMode mode = SubstancePolicyMode.Forbidden;
    [Range(0f, 100f)] public float moodThreshold = 30f;
    public int scheduledHour = 20;
}

[Serializable]
public sealed class CharacterSubstanceState
{
    public string characterId = string.Empty;
    public string substanceId = string.Empty;
    [Range(0f, 100f)] public float tolerance;
    [Range(0f, 100f)] public float addiction;
    [Range(0f, 100f)] public float withdrawal;
    public float activeSeconds;
    public float secondsSinceLastDose;
    public float scheduledCooldownSeconds;
    public bool addicted;
    public bool overdosed;
}

[Serializable]
public sealed class DungeonCharacterConsumablesSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public List<CharacterDietPolicyState> dietPolicies =
        new List<CharacterDietPolicyState>();
    public List<CharacterSubstancePolicyState> substancePolicies =
        new List<CharacterSubstancePolicyState>();
    public List<CharacterSubstanceState> substanceStates =
        new List<CharacterSubstanceState>();
}

public readonly struct CharacterSubstanceUseRequest
{
    public CharacterSubstanceUseRequest(
        string substanceId,
        string itemId,
        string displayName,
        float urgency,
        bool medicalContext,
        bool combatContext,
        string reason)
    {
        SubstanceId = substanceId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Urgency = Mathf.Clamp01(urgency);
        MedicalContext = medicalContext;
        CombatContext = combatContext;
        Reason = reason ?? string.Empty;
    }

    public string SubstanceId { get; }
    public string ItemId { get; }
    public string DisplayName { get; }
    public float Urgency { get; }
    public bool MedicalContext { get; }
    public bool CombatContext { get; }
    public string Reason { get; }
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(SubstanceId)
        && !string.IsNullOrWhiteSpace(ItemId)
        && Urgency > 0f;
}

public readonly struct MealConsumptionResult
{
    private MealConsumptionResult(
        bool success,
        string failureReason,
        string itemId,
        string displayName,
        MealDietClass dietClass,
        MealQualityTier quality,
        float nutrition,
        float mood,
        int unitPrice,
        bool policyViolation,
        bool contaminated)
    {
        Success = success;
        FailureReason = failureReason ?? string.Empty;
        ItemId = itemId ?? string.Empty;
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
    public string FailureReason { get; }
    public string ItemId { get; }
    public string DisplayName { get; }
    public MealDietClass DietClass { get; }
    public MealQualityTier Quality { get; }
    public float Nutrition { get; }
    public float Mood { get; }
    public int UnitPrice { get; }
    public bool PolicyViolation { get; }
    public bool Contaminated { get; }

    public static MealConsumptionResult Failed(string reason) =>
        new MealConsumptionResult(
            false,
            reason,
            string.Empty,
            string.Empty,
            MealDietClass.Vegan,
            MealQualityTier.Simple,
            0f,
            0f,
            0,
            false,
            false);

    public static MealConsumptionResult Consumed(
        ResourceItemDefinitionSO item,
        bool policyViolation,
        bool contaminated) =>
        new MealConsumptionResult(
            true,
            string.Empty,
            item != null ? item.ItemId : string.Empty,
            item != null ? item.DisplayName : string.Empty,
            item != null ? item.MealDietClass : MealDietClass.Vegan,
            item != null ? item.MealQuality : MealQualityTier.Simple,
            item != null ? item.Nutrition : 0f,
            item != null ? item.MealMood : 0f,
            item != null ? item.UnitPrice : 0,
            policyViolation,
            contaminated);
}

public readonly struct SubstanceUseResult
{
    public SubstanceUseResult(
        bool success,
        string failureReason,
        string substanceId,
        string displayName,
        float tolerance,
        float addiction,
        bool becameAddicted,
        bool overdosed)
    {
        Success = success;
        FailureReason = failureReason ?? string.Empty;
        SubstanceId = substanceId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Tolerance = Mathf.Clamp(tolerance, 0f, 100f);
        Addiction = Mathf.Clamp(addiction, 0f, 100f);
        BecameAddicted = becameAddicted;
        Overdosed = overdosed;
    }

    public bool Success { get; }
    public string FailureReason { get; }
    public string SubstanceId { get; }
    public string DisplayName { get; }
    public float Tolerance { get; }
    public float Addiction { get; }
    public bool BecameAddicted { get; }
    public bool Overdosed { get; }
}

public readonly struct PhysicalMealConsumedEvent
{
    public PhysicalMealConsumedEvent(
        CharacterActor actor,
        BuildableObject facility,
        MealConsumptionResult result)
    {
        Actor = actor;
        Facility = facility;
        Result = result;
    }

    public CharacterActor Actor { get; }
    public BuildableObject Facility { get; }
    public MealConsumptionResult Result { get; }
}

public interface ICharacterDietPolicyRuntime
{
    CharacterDietPolicyKind GetPolicy(CharacterActor actor);
    void SetPolicy(CharacterActor actor, CharacterDietPolicyKind policy);
    bool IsAllowed(CharacterActor actor, ResourceItemDefinitionSO meal);
}

public interface IMealConsumptionRuntime
{
    bool TryConsumeMeal(
        CharacterActor actor,
        BuildableObject facility,
        out MealConsumptionResult result);
    bool HasMealAvailable(
        CharacterActor actor,
        BuildableObject facility,
        out string reason);
}

public interface ICharacterSubstanceRuntime
{
    CharacterSubstancePolicyState GetPolicy(
        CharacterActor actor,
        string substanceId);
    void SetPolicy(
        CharacterActor actor,
        string substanceId,
        SubstancePolicyMode mode,
        float moodThreshold = 30f,
        int scheduledHour = 20);
    CharacterSubstanceState GetState(
        CharacterActor actor,
        string substanceId);
    bool TryConsume(
        CharacterActor actor,
        string substanceId,
        bool medicalContext,
        bool combatContext,
        out SubstanceUseResult result);
    bool TryGetAutomaticUseRequest(
        CharacterActor actor,
        out CharacterSubstanceUseRequest request);
    float GetWorkSpeedMultiplier(CharacterActor actor);
    float GetCombatMultiplier(CharacterActor actor);
}

public interface ICharacterConsumablesRuntime :
    ICharacterDietPolicyRuntime,
    IMealConsumptionRuntime,
    ICharacterSubstanceRuntime
{
    DungeonCharacterConsumablesSaveData Capture();
    void Restore(DungeonCharacterConsumablesSaveData saveData);
}
