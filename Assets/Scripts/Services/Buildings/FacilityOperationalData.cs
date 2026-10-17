using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;



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

public enum FacilityUseCommitPhase
{
    None = 0,
    EffectsCommitted = 1,
    SessionCompleted = 2,
    ParentCommitted = 3,
    Acknowledged = 4
}

[Serializable]
public sealed class FacilityUseCommitState
{
    public int phase;
    public long operationSequence;
    public string operationId = string.Empty;
    public string actorId = string.Empty;
    public string actorDisplayName = string.Empty;
    public string facilityRole = string.Empty;
    public string serviceSessionId = string.Empty;
    public int outcomeStatus;
    public bool effectsCommitted;
    public string failureCode = string.Empty;
    public float healthBefore;
    public float healthAfter;
    public float maximumHealthBefore;
    public float maximumHealthAfter;
    public float injuryBefore;
    public float injuryAfter;
    public float expeditionStressBefore;
    public float expeditionStressAfter;
    public float moodBefore;
    public float moodAfter;
    public float hungerBefore;
    public float hungerAfter;
    public float funBefore;
    public float funAfter;
    public float sleepBefore;
    public float sleepAfter;
    public float excretionBefore;
    public float excretionAfter;
    public float hygieneBefore;
    public float hygieneAfter;
    public List<string> activeConditionIds = new();
    public List<string> childResultKeys = new();
    public string publicationKey = string.Empty;
    public int absoluteDay = -1;
    public string facilityDisplayName = string.Empty;

    public FacilityUseCommitState Clone() => new()
    {
        phase = phase,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        actorId = actorId ?? string.Empty,
        actorDisplayName = actorDisplayName ?? string.Empty,
        facilityRole = facilityRole ?? string.Empty,
        serviceSessionId = serviceSessionId ?? string.Empty,
        outcomeStatus = outcomeStatus,
        effectsCommitted = effectsCommitted,
        failureCode = failureCode ?? string.Empty,
        healthBefore = healthBefore,
        healthAfter = healthAfter,
        maximumHealthBefore = maximumHealthBefore,
        maximumHealthAfter = maximumHealthAfter,
        injuryBefore = injuryBefore,
        injuryAfter = injuryAfter,
        expeditionStressBefore = expeditionStressBefore,
        expeditionStressAfter = expeditionStressAfter,
        moodBefore = moodBefore,
        moodAfter = moodAfter,
        hungerBefore = hungerBefore,
        hungerAfter = hungerAfter,
        funBefore = funBefore,
        funAfter = funAfter,
        sleepBefore = sleepBefore,
        sleepAfter = sleepAfter,
        excretionBefore = excretionBefore,
        excretionAfter = excretionAfter,
        hygieneBefore = hygieneBefore,
        hygieneAfter = hygieneAfter,
        activeConditionIds = new List<string>(activeConditionIds ?? new()),
        childResultKeys = new List<string>(childResultKeys ?? new()),
        publicationKey = publicationKey,
        absoluteDay = absoluteDay,
        facilityDisplayName = facilityDisplayName
    };
}

[Serializable]
public sealed class FacilityUseAcknowledgementRange
{
    public long first, last;
    public FacilityUseAcknowledgementRange Clone() => (FacilityUseAcknowledgementRange)MemberwiseClone();
}

public static class FacilityVandalismTargetRules
{
    public static bool IsPersistentAggregateTarget(BuildableObject facility) =>
        facility != null
        && facility is not ConstructionSite
        && facility is not ExteriorZoneMarker
        && facility.BuildingData != null
        && facility.BuildingData.id >= 0;
}

[Serializable]
public sealed class FacilityRuntimeState
{
    [Min(0)] public int completedUses;
    [Min(0)] public int completedWorkCycles;
    [Range(0f, 100f)] public float cleanliness = 100f;
    [Min(0f)] public float remainingFuelGameSeconds;
    [Min(1)] public int nextFuelOperationSequence = 1;
    [Min(1)] public long nextUseOperationSequence = 1L;
    [Min(1)] public long nextVandalismOperationSequence = 1L;
    public long synthesisOutcomeRevision;
    public FacilityFuelCommitState pendingFuel = new FacilityFuelCommitState();
    public List<FacilityUseCommitState> pendingUses = new();
    public List<FacilityUseAcknowledgementRange> acknowledgedUseRanges = new();
    public List<FacilityUseExecutionState> activeUses = new();
    public FacilityVandalismCommitState lastVandalism = new();

    public FacilityRuntimeState Clone()
    {
        return new FacilityRuntimeState
        {
            completedUses = completedUses,
            completedWorkCycles = completedWorkCycles,
            cleanliness = cleanliness,
            remainingFuelGameSeconds = remainingFuelGameSeconds,
            nextFuelOperationSequence = nextFuelOperationSequence,
            nextUseOperationSequence = nextUseOperationSequence,
            nextVandalismOperationSequence = nextVandalismOperationSequence,
            synthesisOutcomeRevision = synthesisOutcomeRevision,
            pendingFuel = pendingFuel?.Clone() ?? new FacilityFuelCommitState(),
            pendingUses = (pendingUses ?? new List<FacilityUseCommitState>())
                .ConvertAll(value => value?.Clone() ?? new FacilityUseCommitState()),
            activeUses = activeUses?.ConvertAll(value => value?.Clone()),
            acknowledgedUseRanges = acknowledgedUseRanges?.ConvertAll(value => value?.Clone()),
            lastVandalism = lastVandalism?.Clone()
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
            nextUseOperationSequence = 1L;
            nextVandalismOperationSequence = 1L;
            synthesisOutcomeRevision = 0L;
            pendingFuel = new FacilityFuelCommitState();
            pendingUses = new List<FacilityUseCommitState>();
            acknowledgedUseRanges = new();
            activeUses = new List<FacilityUseExecutionState>();
            lastVandalism = new FacilityVandalismCommitState();
            return;
        }

        completedUses = Mathf.Max(0, source.completedUses);
        completedWorkCycles = Mathf.Max(0, source.completedWorkCycles);
        cleanliness = Mathf.Clamp(source.cleanliness, 0f, 100f);
        remainingFuelGameSeconds = source.remainingFuelGameSeconds;
        nextFuelOperationSequence = source.nextFuelOperationSequence;
        nextUseOperationSequence = Math.Max(1L, source.nextUseOperationSequence);
        nextVandalismOperationSequence = source.nextVandalismOperationSequence;
        synthesisOutcomeRevision = source.synthesisOutcomeRevision;
        pendingFuel = source.pendingFuel?.Clone() ?? new FacilityFuelCommitState();
        pendingUses = (source.pendingUses ?? new List<FacilityUseCommitState>())
            .ConvertAll(value => value?.Clone() ?? new FacilityUseCommitState());
        activeUses = source.activeUses?.ConvertAll(value => value?.Clone());
        acknowledgedUseRanges = source.acknowledgedUseRanges?.ConvertAll(value => value?.Clone());
        lastVandalism = source.lastVandalism?.Clone();
    }

    public bool IsValid(out string error)
    {
        if (completedUses < 0 || completedWorkCycles < 0
            || !IsFinite(cleanliness) || cleanliness < 0f || cleanliness > 100f
            || !IsFinite(remainingFuelGameSeconds) || remainingFuelGameSeconds < 0f
            || nextFuelOperationSequence <= 0
            || nextUseOperationSequence <= 0L
            || nextVandalismOperationSequence <= 0L
            || synthesisOutcomeRevision < 0L)
        {
            error = "facility runtime scalar state is invalid";
            return false;
        }

        if (!ValidateAcknowledgedUses(out error) || !ValidateActiveUses(out error) || !ValidatePendingUses(out error))
        {
            return false;
        }

        if (lastVandalism == null)
        {
            error = "facility vandalism state is missing";
            return false;
        }
        if (!lastVandalism.IsValid(nextVandalismOperationSequence, out error))
        {
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

    private bool ValidateActiveUses(out string error)
    {
        error = "invalid or duplicate facility active use";
        if (activeUses == null) return false;
        var actors = new HashSet<string>(StringComparer.Ordinal);
        var operations = new HashSet<long>();
        long previous = 0L;
        foreach (FacilityUseExecutionState use in activeUses)
        {
            if (use == null || !use.IsValid(nextUseOperationSequence, out error)
                || !actors.Add(use.actorId) || !operations.Add(use.operationSequence)
                || use.operationSequence <= previous
                || pendingUses?.Exists(value => value != null
                    && value.operationSequence == use.operationSequence) == true)
            {
                error = "invalid, unordered, duplicate or terminal facility active use";
                return false;
            }
            previous = use.operationSequence;
        }
        error = string.Empty;
        return true;
    }

    private bool ValidatePendingUses(out string error)
    {
        if (pendingUses == null) { error = "facility use pending list is missing"; return false; }
        var sequences = new HashSet<long>();
        foreach (FacilityUseCommitState pending in
                 pendingUses ?? new List<FacilityUseCommitState>())
        {
            FacilityUseCommitPhase phase = pending == null
                ? FacilityUseCommitPhase.None
                : (FacilityUseCommitPhase)pending.phase;
            bool validPhase = phase is >= FacilityUseCommitPhase.EffectsCommitted
                and <= FacilityUseCommitPhase.Acknowledged;
            bool validStatus = pending != null
                && Enum.IsDefined(
                    typeof(CharacterFacilityUseOutcomeStatus),
                    pending.outcomeStatus);
            if (!validPhase
                || !validStatus
                || pending.operationSequence <= 0L
                || pending.operationSequence >= nextUseOperationSequence
                || !sequences.Add(pending.operationSequence)
                || IsUseAcknowledged(pending.operationSequence)
                || string.IsNullOrWhiteSpace(pending.operationId)
                || string.IsNullOrWhiteSpace(pending.actorId)
                || !AllFinite(pending)
                || !CanonicalOrderedUnique(pending.activeConditionIds, false)
                || !CanonicalOrderedUnique(pending.childResultKeys, false))
            {
                error = "facility use pending contract is invalid";
                return false;
            }
            if (pending.publicationKey == null || pending.publicationKey.Length > 4096
                || pending.publicationKey != pending.publicationKey.Trim()
                || pending.facilityDisplayName == null || pending.facilityDisplayName.Length > 4096
                || pending.absoluteDay < -1
                || phase == FacilityUseCommitPhase.EffectsCommitted && pending.publicationKey.Length != 0
                || phase >= FacilityUseCommitPhase.SessionCompleted
                    && (pending.publicationKey.Length == 0 || pending.absoluteDay < 0))
            {
                error = "facility use publication metadata is invalid";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    public bool IsUseAcknowledged(long sequence) => acknowledgedUseRanges != null
        && acknowledgedUseRanges.Exists(value => value != null && value.first <= sequence && sequence <= value.last);

    private bool ValidateAcknowledgedUses(out string error)
    {
        error = "facility use acknowledgement ranges are invalid";
        if (acknowledgedUseRanges == null) return false;
        long previous = -1;
        foreach (var range in acknowledgedUseRanges)
        {
            if (range == null || range.first < 1 || range.last < range.first || range.last >= nextUseOperationSequence
                || previous >= 0 && range.first <= previous + 1) return false;
            previous = range.last;
        }
        if (activeUses != null && activeUses.Exists(value => value != null && IsUseAcknowledged(value.operationSequence))) return false;
        error = string.Empty;
        return true;
    }

    private static bool AllFinite(FacilityUseCommitState value) =>
        IsFinite(value.healthBefore) && IsFinite(value.healthAfter)
        && IsFinite(value.maximumHealthBefore) && IsFinite(value.maximumHealthAfter)
        && IsFinite(value.injuryBefore) && IsFinite(value.injuryAfter)
        && IsFinite(value.expeditionStressBefore) && IsFinite(value.expeditionStressAfter)
        && IsFinite(value.moodBefore) && IsFinite(value.moodAfter)
        && IsFinite(value.hungerBefore) && IsFinite(value.hungerAfter)
        && IsFinite(value.funBefore) && IsFinite(value.funAfter)
        && IsFinite(value.sleepBefore) && IsFinite(value.sleepAfter)
        && IsFinite(value.excretionBefore) && IsFinite(value.excretionAfter)
        && IsFinite(value.hygieneBefore) && IsFinite(value.hygieneAfter);

    private static bool CanonicalOrderedUnique(
        IReadOnlyList<string> values,
        bool requireAny)
    {
        if (values == null || values.Count == 0)
            return !requireAny;
        string previous = null;
        for (int index = 0; index < values.Count; index++)
        {
            string current = values[index];
            if (string.IsNullOrWhiteSpace(current)
                || !string.Equals(current, current.Trim(), StringComparison.Ordinal)
                || previous != null
                    && string.CompareOrdinal(previous, current) >= 0)
                return false;
            previous = current;
        }
        return true;
    }

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
