using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct FacilityNeedRecoveryData
{
    public float sleep;
    public float mood;
    public float fun;
    public float hunger;
    public float excretion;
    public float hygiene;

    public bool HasEffect => !Mathf.Approximately(sleep, 0f)
        || !Mathf.Approximately(mood, 0f)
        || !Mathf.Approximately(fun, 0f)
        || !Mathf.Approximately(hunger, 0f)
        || !Mathf.Approximately(excretion, 0f)
        || !Mathf.Approximately(hygiene, 0f);
}

[Serializable]
public sealed class FacilityFuelCommitState
{
    public int phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string destinationId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public float fuelSecondsBefore;
    public float fuelSecondsAfter;
    public string physicalCommitId = string.Empty;
    public List<string> physicalSourceStackIds = new List<string>();
    public long physicalInputMassGrams;

    public FacilityFuelCommitState Clone()
    {
        return new FacilityFuelCommitState
        {
            phase = phase,
            operationSequence = operationSequence,
            operationId = operationId ?? string.Empty,
            destinationId = destinationId ?? string.Empty,
            itemId = itemId ?? string.Empty,
            quantity = quantity,
            fuelSecondsBefore = fuelSecondsBefore,
            fuelSecondsAfter = fuelSecondsAfter,
            physicalCommitId = physicalCommitId ?? string.Empty,
            physicalSourceStackIds = new List<string>(
                physicalSourceStackIds ?? new List<string>()),
            physicalInputMassGrams = physicalInputMassGrams
        };
    }
}

public enum FacilityFuelCommitPhase
{
    None = 0,
    IntentRecorded = 1,
    OutcomePublished = 2
}

[Serializable]
public sealed class FacilityRuntimeState
{
    [Min(0)] public int completedUses;
    [Min(0)] public int completedWorkCycles;
    [Range(0f, 100f)] public float cleanliness = 100f;
    [Min(0f)] public float remainingFuelGameSeconds;
    [Min(1)] public int nextFuelOperationSequence = 1;
    public FacilityFuelCommitState pendingFuel = new FacilityFuelCommitState();

    public FacilityRuntimeState Clone()
    {
        return new FacilityRuntimeState
        {
            completedUses = completedUses,
            completedWorkCycles = completedWorkCycles,
            cleanliness = cleanliness,
            remainingFuelGameSeconds = remainingFuelGameSeconds,
            nextFuelOperationSequence = nextFuelOperationSequence,
            pendingFuel = pendingFuel?.Clone() ?? new FacilityFuelCommitState()
        };
    }

    public void CopyFrom(FacilityRuntimeState source)
    {
        if (source == null)
        {
            completedUses = 0;
            completedWorkCycles = 0;
            cleanliness = 100f;
            remainingFuelGameSeconds = 0f;
            nextFuelOperationSequence = 1;
            pendingFuel = new FacilityFuelCommitState();
            return;
        }

        completedUses = Mathf.Max(0, source.completedUses);
        completedWorkCycles = Mathf.Max(0, source.completedWorkCycles);
        cleanliness = Mathf.Clamp(source.cleanliness, 0f, 100f);
        remainingFuelGameSeconds = source.remainingFuelGameSeconds;
        nextFuelOperationSequence = source.nextFuelOperationSequence;
        pendingFuel = source.pendingFuel?.Clone() ?? new FacilityFuelCommitState();
    }

    public bool IsValid(out string error)
    {
        if (completedUses < 0 || completedWorkCycles < 0
            || !IsFinite(cleanliness) || cleanliness < 0f || cleanliness > 100f
            || !IsFinite(remainingFuelGameSeconds) || remainingFuelGameSeconds < 0f
            || nextFuelOperationSequence <= 0)
        {
            error = "facility runtime scalar state is invalid";
            return false;
        }

        FacilityFuelCommitState fuel = pendingFuel ?? new FacilityFuelCommitState();
        FacilityFuelCommitPhase phase = (FacilityFuelCommitPhase)fuel.phase;
        if (phase == FacilityFuelCommitPhase.None)
        {
            bool empty = fuel.operationSequence == 0
                && string.IsNullOrEmpty(fuel.operationId)
                && string.IsNullOrEmpty(fuel.destinationId)
                && string.IsNullOrEmpty(fuel.itemId)
                && fuel.quantity == 0
                && Mathf.Approximately(fuel.fuelSecondsBefore, 0f)
                && Mathf.Approximately(fuel.fuelSecondsAfter, 0f)
                && string.IsNullOrEmpty(fuel.physicalCommitId)
                && (fuel.physicalSourceStackIds == null
                    || fuel.physicalSourceStackIds.Count == 0)
                && fuel.physicalInputMassGrams == 0L;
            error = empty
                ? string.Empty
                : "empty facility fuel commit contains stale state";
            return empty;
        }

        if ((phase != FacilityFuelCommitPhase.IntentRecorded
                && phase != FacilityFuelCommitPhase.OutcomePublished)
            || fuel.operationSequence != nextFuelOperationSequence
            || string.IsNullOrWhiteSpace(fuel.operationId)
            || !string.Equals(fuel.operationId, fuel.operationId.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(fuel.destinationId)
            || !string.Equals(fuel.destinationId, fuel.destinationId.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(fuel.itemId)
            || !string.Equals(fuel.itemId, fuel.itemId.Trim(), StringComparison.Ordinal)
            || fuel.quantity <= 0
            || !IsFinite(fuel.fuelSecondsBefore)
            || !IsFinite(fuel.fuelSecondsAfter)
            || fuel.fuelSecondsBefore < 0f
            || fuel.fuelSecondsAfter <= fuel.fuelSecondsBefore)
        {
            error = "facility fuel commit contract is invalid";
            return false;
        }

        if (phase == FacilityFuelCommitPhase.OutcomePublished
            && (string.IsNullOrWhiteSpace(fuel.physicalCommitId)
                || !string.Equals(
                    fuel.physicalCommitId,
                    fuel.physicalCommitId.Trim(),
                    StringComparison.Ordinal)
                || fuel.physicalInputMassGrams <= 0L
                || fuel.physicalSourceStackIds == null
                || !HasCanonicalOrderedUniqueIds(
                    fuel.physicalSourceStackIds)))
        {
            error = "published facility fuel commit receipt is invalid";
            return false;
        }
        if (phase == FacilityFuelCommitPhase.IntentRecorded
            && (!string.IsNullOrEmpty(fuel.physicalCommitId)
                || fuel.physicalInputMassGrams != 0L
                || fuel.physicalSourceStackIds?.Count > 0))
        {
            error = "facility fuel intent already contains terminal receipt state";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool HasCanonicalOrderedUniqueIds(
        IReadOnlyList<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return false;
        }
        string previous = null;
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index];
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
                || previous != null
                    && string.CompareOrdinal(previous, value) >= 0)
            {
                return false;
            }
            previous = value;
        }
        return true;
    }
}

public interface IBuildingUnlockStateView
{
    bool IsBuildingUnlocked(int buildingId);
}

public static class FacilityProgression
{
    public static int GetCurrentPhase(GameSessionState gameData)
    {
        int day = gameData != null && gameData.day != null
            ? Mathf.Max(1, gameData.day.Value)
            : 1;
        return Mathf.Clamp(1 + ((day - 1) / 3), 1, 3);
    }

    public static bool IsUnlocked(
        BuildingSO building,
        GameSessionState gameData,
        IBuildingUnlockStateView unlockState,
        IDungeonDebugRuleQuery debugRules,
        IRunMilestoneQuery milestoneQuery = null)
    {
        if (building == null)
        {
            return false;
        }

        if ((debugRules ?? throw new ArgumentNullException(nameof(debugRules)))
            .IsEnabled(DungeonDebugCheat.IgnoreUnlocks))
        {
            return true;
        }

        string definitionId = building.ContentDefinitionId;
        if (milestoneQuery != null
            && milestoneQuery.IsLandmarkBuilding(definitionId))
        {
            return milestoneQuery.IsLandmarkUnlocked(definitionId);
        }

        if (unlockState != null && unlockState.IsBuildingUnlocked(building.id))
        {
            return true;
        }

        if (!building.unlocked)
        {
            return false;
        }

        return !building.IsModularFacility() || GetCurrentPhase(gameData) >= building.GetUnlockPhase();
    }

    public static int GetRefund(BuildingSO building)
    {
        return 0;
    }
}
