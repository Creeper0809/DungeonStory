using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// This is domain progress, not a serialized coroutine, path, or AI score.
public enum FacilityUseExecutionPhase
{
    Approach, Settle, Reception, Waiting, SeatApproach, Service,
    Effects, Payment, Cleanup, Exit, Complete
}

[Serializable]
public sealed class FacilityUseVisitorState
{
    public string actorId = string.Empty;
    public string displayName = string.Empty;
    public Vector3 position;
    public bool runtimeActive, internalStaff, canMove;
    public float stayMultiplier, revenueMultiplier, personalityPatience,
        modelPatience, crimeRisk, productionMultiplier;
    public int productionBonus;
    public float stress, mood, hunger, fun, sleep, excretion, hygiene,
        health, maximumHealth, injury;
    public string[] conditions = Array.Empty<string>();

    public static FacilityUseVisitorState Capture(in BuildingVisitorSnapshot value) => new()
    {
        actorId = value.PersistentId, displayName = value.DisplayName,
        position = value.Position, runtimeActive = value.IsRuntimeActive,
        internalStaff = value.IsInternalStaff, canMove = value.CanMove,
        stayMultiplier = value.StayDurationMultiplier,
        revenueMultiplier = value.RevenueMultiplier,
        personalityPatience = value.PersonalityPatience,
        modelPatience = value.ModelPatience, crimeRisk = value.CrimeRiskMultiplier,
        productionMultiplier = value.ProductionOutputMultiplier,
        productionBonus = value.StockProductionBonus,
        stress = value.ExpeditionStress, mood = value.Mood, hunger = value.Hunger,
        fun = value.Fun, sleep = value.Sleep, excretion = value.Excretion,
        hygiene = value.Hygiene, health = value.CurrentHealth,
        maximumHealth = value.MaximumHealth, injury = value.InjurySeverity,
        conditions = (value.ActiveConditionIds ?? Array.Empty<string>()).ToArray()
    };

    public BuildingVisitorSnapshot ToSnapshot() => new(
        actorId, displayName, position, runtimeActive, internalStaff, canMove,
        stayMultiplier, revenueMultiplier, personalityPatience, modelPatience,
        crimeRisk, productionMultiplier, productionBonus, stress, mood, hunger,
        fun, sleep, excretion, hygiene, health, maximumHealth, injury,
        conditions == null ? null : (string[])conditions.Clone());

    public FacilityUseVisitorState Clone()
    {
        var result = (FacilityUseVisitorState)MemberwiseClone();
        result.conditions = conditions == null ? null : (string[])conditions.Clone();
        return result;
    }

    public bool IsValid() => FacilityUseExecutionState.Canonical(actorId)
        && !string.IsNullOrWhiteSpace(displayName)
        && conditions != null && conditions.All(FacilityUseExecutionState.Canonical)
        && conditions.Distinct(StringComparer.Ordinal).Count() == conditions.Length
        && new[] { position.x, position.y, position.z, stayMultiplier,
            revenueMultiplier, personalityPatience, modelPatience, crimeRisk,
            productionMultiplier, stress, mood, hunger, fun, sleep, excretion,
            hygiene, health, maximumHealth, injury }
            .All(FacilityUseExecutionState.Finite)
        && stayMultiplier >= 0f && health >= 0f && maximumHealth >= 0f && injury >= 0f;
}

[Serializable]
public sealed class FacilityUseExecutionState
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public long operationSequence;
    public string operationId = string.Empty;
    public string actorId = string.Empty;
    public string actorDisplayName = string.Empty;
    public string facilityRole = string.Empty;
    public string serviceSessionId = string.Empty;
    public string actionType = string.Empty;
    public int actionBranch;
    public FacilityUseExecutionPhase phase;
    public bool timerInitialized;
    public float remainingSeconds;
    public float serviceSeconds;
    public FacilityUseVisitorState before;
    public bool effectsCommitted;
    public List<string> childResultKeys = new();
    public bool waterPrepared;
    public string waterFixtureId = string.Empty;
    public WaterFixtureSupplyKind waterSupplyKind;
    public float wastewaterAmount;
    public string mealOperationId = string.Empty;
    public float mealWaitElapsedSeconds;
    public bool mealSucceeded;
    public string mealDisplayName = string.Empty;

    public FacilityUseExecutionState Clone()
    {
        var result = (FacilityUseExecutionState)MemberwiseClone();
        result.before = before?.Clone();
        result.childResultKeys = childResultKeys == null ? null : new(childResultKeys);
        return result;
    }

    public bool IsValid(long nextSequence, out string error)
    {
        bool valid = version == CurrentVersion && operationSequence > 0L
            && operationSequence < nextSequence && Canonical(operationId)
            && Canonical(actorId) && !string.IsNullOrWhiteSpace(actorDisplayName)
            && Canonical(actionType) && actionBranch != (int)CharacterAiBranch.None
            && Enum.IsDefined(typeof(CharacterAiBranch), actionBranch)
            && Enum.IsDefined(typeof(FacilityUseExecutionPhase), phase)
            && Optional(serviceSessionId) && before != null && before.IsValid()
            && before.actorId == actorId && Finite(remainingSeconds)
            && remainingSeconds >= 0f && (timerInitialized || remainingSeconds == 0f)
            && Finite(serviceSeconds) && serviceSeconds >= 0f
            && childResultKeys != null && childResultKeys.All(Canonical)
            && childResultKeys.Distinct(StringComparer.Ordinal).Count() == childResultKeys.Count
            && Optional(mealOperationId) && Finite(mealWaitElapsedSeconds)
            && mealWaitElapsedSeconds >= 0f && mealWaitElapsedSeconds <= 15f
            && (!mealSucceeded || !string.IsNullOrWhiteSpace(mealDisplayName))
            && Enum.IsDefined(typeof(WaterFixtureSupplyKind), waterSupplyKind)
            && Optional(waterFixtureId) && Finite(wastewaterAmount) && wastewaterAmount >= 0f
            && (waterSupplyKind == WaterFixtureSupplyKind.None
                ? waterFixtureId.Length == 0 && wastewaterAmount == 0f
                : waterPrepared && Canonical(waterFixtureId))
            && (phase >= FacilityUseExecutionPhase.Payment ? effectsCommitted : true)
            && (phase != FacilityUseExecutionPhase.Effects || !effectsCommitted)
            && (phase >= FacilityUseExecutionPhase.Effects
                || !effectsCommitted && !waterPrepared && mealOperationId.Length == 0);
        error = valid ? string.Empty : "invalid facility-use execution progress";
        return valid;
    }

    public bool Enter(FacilityUseExecutionPhase target)
    {
        if (phase > target) return false;
        if (phase < target)
        {
            phase = target;
            timerInitialized = false;
            remainingSeconds = 0f;
        }
        return true;
    }

    public void Advance(FacilityUseExecutionPhase completed)
    {
        if (phase > completed) return;
        phase = completed + 1;
        timerInitialized = false;
        remainingSeconds = 0f;
    }

    internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    internal static bool Canonical(string value) => !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    internal static bool Optional(string value) => value != null && (value.Length == 0 || Canonical(value));
}
