#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Pure state coverage only: coroutine execution and BuildingStateModule restore hooks
// are intentionally outside this scenario's authority.
public static class FacilityUseExecutionStateDebugScenarios
{
    public static bool RunAll(bool log)
    {
        try
        {
            VerifyActiveUseJsonRoundTrip();
            VerifyCloneIsIndependent();
            VerifyPhaseAndTimerProgression();
            VerifyInvalidStateRejections();
            VerifyAcknowledgedUseRanges();
            if (log)
            {
                Debug.Log(
                    "FACILITY_USE_EXECUTION_STATE=PASS; "
                    + "scope=state-json-clone-phase-acknowledgement-invariants; "
                    + "coroutine-save-hook-resume=not-covered");
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"{nameof(FacilityUseExecutionStateDebugScenarios)} failed: {exception}");
            return false;
        }
    }

    private static void VerifyActiveUseJsonRoundTrip()
    {
        FacilityRuntimeState source = CreateValidRuntimeState();
        Require(source.IsValid(out string sourceError),
            "Valid active facility-use state was rejected: " + sourceError);

        FacilityUseExecutionState expected = source.activeUses[0];
        FacilityRuntimeState restored = JsonUtility.FromJson<FacilityRuntimeState>(
            JsonUtility.ToJson(source));
        string restoreError = string.Empty;
        Require(restored != null && restored.IsValid(out restoreError),
            "Active facility-use JSON roundtrip was invalid: " + restoreError);
        Require(restored.nextUseOperationSequence == source.nextUseOperationSequence
                && restored.activeUses != null
                && restored.activeUses.Count == 1,
            "Active facility-use JSON roundtrip lost its owning sequence or active entry.");
        RequireSameExecutionState(
            expected,
            restored.activeUses[0],
            "Active facility-use JSON roundtrip");
    }

    private static void VerifyCloneIsIndependent()
    {
        FacilityRuntimeState source = CreateValidRuntimeState();
        FacilityRuntimeState clone = source.Clone();
        FacilityUseExecutionState sourceUse = source.activeUses[0];
        FacilityUseExecutionState clonedUse = clone.activeUses[0];
        Require(!ReferenceEquals(source, clone)
                && !ReferenceEquals(source.activeUses, clone.activeUses)
                && !ReferenceEquals(sourceUse, clonedUse)
                && !ReferenceEquals(sourceUse.before, clonedUse.before)
                && !ReferenceEquals(sourceUse.before.conditions, clonedUse.before.conditions)
                && !ReferenceEquals(sourceUse.childResultKeys, clonedUse.childResultKeys),
            "Facility runtime clone retained mutable active-use references.");

        clone.nextUseOperationSequence = 99L;
        clonedUse.remainingSeconds = 0.25f;
        clonedUse.before.displayName = "Changed Visitor";
        clonedUse.before.conditions[0] = "condition:changed";
        clonedUse.childResultKeys[0] = "outcome:changed";

        Require(source.nextUseOperationSequence == 8L
                && Mathf.Approximately(sourceUse.remainingSeconds, 1.75f)
                && sourceUse.before.displayName == "Rest Visitor"
                && sourceUse.before.conditions[0] == "condition:rested"
                && sourceUse.childResultKeys[0] == "outcome:room-experience",
            "Facility runtime clone mutation changed the original active use.");
    }

    private static void VerifyPhaseAndTimerProgression()
    {
        FacilityUseExecutionState state = CreateValidUse();
        state.phase = FacilityUseExecutionPhase.Service;
        state.effectsCommitted = false;
        state.waterPrepared = false;
        state.waterFixtureId = string.Empty;
        state.waterSupplyKind = WaterFixtureSupplyKind.None;
        state.wastewaterAmount = 0f;
        state.mealOperationId = string.Empty;
        state.mealWaitElapsedSeconds = 0f;
        state.mealSucceeded = false;
        state.mealDisplayName = string.Empty;
        state.timerInitialized = true;
        state.remainingSeconds = 2.5f;
        Require(state.IsValid(8L, out string serviceError),
            "Service-phase timer fixture was invalid: " + serviceError);

        state.Advance(FacilityUseExecutionPhase.Service);
        Require(state.phase == FacilityUseExecutionPhase.Effects
                && !state.timerInitialized
                && Mathf.Approximately(state.remainingSeconds, 0f),
            "Advance did not enter the next phase with a reset timer.");

        state.timerInitialized = true;
        state.remainingSeconds = 1.25f;
        Require(!state.Enter(FacilityUseExecutionPhase.Service)
                && state.phase == FacilityUseExecutionPhase.Effects
                && state.timerInitialized
                && Mathf.Approximately(state.remainingSeconds, 1.25f),
            "Enter allowed a completed facility-use phase to move backwards.");

        state.effectsCommitted = true;
        state.Advance(FacilityUseExecutionPhase.Effects);
        string paymentError = string.Empty;
        Require(state.phase == FacilityUseExecutionPhase.Payment
                && !state.timerInitialized
                && Mathf.Approximately(state.remainingSeconds, 0f)
                && state.IsValid(8L, out paymentError),
            "Effects-to-payment progression broke the phase/timer invariant: " + paymentError);
    }

    private static void VerifyInvalidStateRejections()
    {
        FacilityRuntimeState nanRemaining = CreateValidRuntimeState();
        nanRemaining.activeUses[0].remainingSeconds = float.NaN;
        RequireInvalid(nanRemaining, "NaN remaining time");

        FacilityRuntimeState negativeRemaining = CreateValidRuntimeState();
        negativeRemaining.activeUses[0].remainingSeconds = -0.25f;
        RequireInvalid(negativeRemaining, "negative remaining time");

        FacilityRuntimeState uninitializedTimer = CreateValidRuntimeState();
        uninitializedTimer.activeUses[0].timerInitialized = false;
        RequireInvalid(uninitializedTimer, "uninitialized timer with remaining time");

        FacilityRuntimeState undefinedPhase = CreateValidRuntimeState();
        undefinedPhase.activeUses[0].phase = (FacilityUseExecutionPhase)999;
        RequireInvalid(undefinedPhase, "undefined execution phase");

        FacilityRuntimeState duplicateActor = CreateValidRuntimeState();
        duplicateActor.activeUses.Insert(
            0,
            CreateValidUse(6L, "character:rest-visitor"));
        RequireInvalid(duplicateActor, "duplicate active-use actor");

        FacilityRuntimeState duplicateOperation = CreateValidRuntimeState();
        FacilityUseExecutionState sameSequence = CreateValidUse(
            7L,
            "character:second-visitor");
        sameSequence.operationId = "facility-visit:facility=building:fixture:use=7:duplicate";
        duplicateOperation.activeUses.Add(sameSequence);
        RequireInvalid(duplicateOperation, "duplicate active-use operation sequence");

        FacilityRuntimeState activePendingCollision = CreateValidRuntimeState();
        activePendingCollision.pendingUses.Add(CreatePendingUse(7L));
        RequireInvalid(activePendingCollision, "active and pending operation collision");

        FacilityRuntimeState paymentWithoutEffects = CreateValidRuntimeState();
        paymentWithoutEffects.activeUses[0].effectsCommitted = false;
        RequireInvalid(paymentWithoutEffects, "payment phase before effects commit");
    }

    private static void VerifyAcknowledgedUseRanges()
    {
        FacilityRuntimeState source = CreateAcknowledgedRangeState();
        Require(source.IsValid(out string sourceError),
            "Valid facility-use acknowledgement ranges were rejected: "
            + sourceError);
        Require(source.IsUseAcknowledged(1L)
                && source.IsUseAcknowledged(2L)
                && !source.IsUseAcknowledged(3L)
                && source.IsUseAcknowledged(4L)
                && source.IsUseAcknowledged(5L)
                && !source.IsUseAcknowledged(6L),
            "Facility-use acknowledgement range membership changed.");

        FacilityRuntimeState clone = source.Clone();
        Require(!ReferenceEquals(source.acknowledgedUseRanges,
                    clone.acknowledgedUseRanges)
                && !ReferenceEquals(source.acknowledgedUseRanges[0],
                    clone.acknowledgedUseRanges[0]),
            "Facility runtime clone retained mutable acknowledgement ranges.");
        clone.acknowledgedUseRanges[0].last = 1L;
        Require(source.acknowledgedUseRanges[0].last == 2L,
            "Acknowledgement range clone mutation changed the original state.");

        FacilityRuntimeState restored = JsonUtility.FromJson<FacilityRuntimeState>(
            JsonUtility.ToJson(source));
        string restoreError = string.Empty;
        Require(restored != null
                && restored.IsValid(out restoreError)
                && restored.nextUseOperationSequence == 7L
                && restored.activeUses != null
                && restored.activeUses.Count == 0
                && restored.pendingUses != null
                && restored.pendingUses.Count == 0
                && restored.acknowledgedUseRanges != null
                && restored.acknowledgedUseRanges.Count == 2
                && restored.acknowledgedUseRanges[0].first == 1L
                && restored.acknowledgedUseRanges[0].last == 2L
                && restored.acknowledgedUseRanges[1].first == 4L
                && restored.acknowledgedUseRanges[1].last == 5L,
            "Facility-use acknowledgement ranges did not round-trip exactly: "
            + restoreError);

        FacilityRuntimeState overlap = CreateAcknowledgedRangeState();
        overlap.acknowledgedUseRanges[1].first = 2L;
        RequireInvalid(overlap, "overlapping acknowledgement ranges");

        FacilityRuntimeState adjacent = CreateAcknowledgedRangeState();
        adjacent.acknowledgedUseRanges[1].first = 3L;
        RequireInvalid(adjacent, "adjacent acknowledgement ranges");

        FacilityRuntimeState unsorted = CreateAcknowledgedRangeState();
        unsorted.acknowledgedUseRanges.Reverse();
        RequireInvalid(unsorted, "unsorted acknowledgement ranges");

        FacilityRuntimeState beyondCursor = CreateAcknowledgedRangeState();
        beyondCursor.acknowledgedUseRanges[1].last = 7L;
        RequireInvalid(beyondCursor, "acknowledgement range at the next-use cursor");

        FacilityRuntimeState nullRanges = CreateAcknowledgedRangeState();
        nullRanges.acknowledgedUseRanges = null;
        RequireInvalid(nullRanges, "null acknowledgement range list");

        FacilityRuntimeState pendingCollision = CreateAcknowledgedRangeState();
        pendingCollision.pendingUses.Add(CreatePendingUse(2L));
        RequireInvalid(pendingCollision,
            "pending use colliding with an acknowledged sequence");
    }

    private static FacilityRuntimeState CreateAcknowledgedRangeState() => new()
    {
        nextUseOperationSequence = 7L,
        acknowledgedUseRanges = new List<FacilityUseAcknowledgementRange>
        {
            new() { first = 1L, last = 2L },
            new() { first = 4L, last = 5L }
        },
        activeUses = new List<FacilityUseExecutionState>(),
        pendingUses = new List<FacilityUseCommitState>()
    };

    private static FacilityRuntimeState CreateValidRuntimeState() => new()
    {
        nextUseOperationSequence = 8L,
        activeUses = new List<FacilityUseExecutionState>
        {
            CreateValidUse()
        }
    };

    private static FacilityUseExecutionState CreateValidUse(
        long sequence = 7L,
        string actorId = "character:rest-visitor") => new()
    {
        operationSequence = sequence,
        operationId = "facility-visit:facility=building:fixture:use=" + sequence,
        actorId = actorId,
        actorDisplayName = "Rest Visitor",
        facilityRole = FacilityRole.Rest.ToString(),
        serviceSessionId = "session:rest-visitor",
        actionType = nameof(AIRest),
        actionBranch = (int)CharacterAiBranch.Rest,
        phase = FacilityUseExecutionPhase.Payment,
        timerInitialized = true,
        remainingSeconds = 1.75f,
        serviceSeconds = 3.5f,
        before = new FacilityUseVisitorState
        {
            actorId = actorId,
            displayName = "Rest Visitor",
            position = new Vector3(4.5f, 2.25f, 0f),
            runtimeActive = true,
            internalStaff = false,
            canMove = true,
            stayMultiplier = 1.25f,
            revenueMultiplier = 1.1f,
            personalityPatience = 0.75f,
            modelPatience = 0.8f,
            crimeRisk = 0.2f,
            productionMultiplier = 1.05f,
            productionBonus = 2,
            stress = 3f,
            mood = 4f,
            hunger = 5f,
            fun = 6f,
            sleep = 7f,
            excretion = 8f,
            hygiene = 9f,
            health = 10f,
            maximumHealth = 12f,
            injury = 0.5f,
            conditions = new[] { "condition:rested", "condition:well-fed" }
        },
        effectsCommitted = true,
        childResultKeys = new List<string>
        {
            "outcome:room-experience",
            "outcome:need-recovery"
        },
        waterPrepared = true,
        waterFixtureId = "building:water-fixture",
        waterSupplyKind = WaterFixtureSupplyKind.Piped,
        wastewaterAmount = 0.5f,
        mealOperationId = "meal:rest-visitor",
        mealWaitElapsedSeconds = 2.5f,
        mealSucceeded = true,
        mealDisplayName = "Fixture Meal"
    };

    private static FacilityUseCommitState CreatePendingUse(long sequence) => new()
    {
        phase = (int)FacilityUseCommitPhase.EffectsCommitted,
        operationSequence = sequence,
        operationId = "facility-use:pending=" + sequence,
        actorId = "character:pending-visitor",
        actorDisplayName = "Pending Visitor",
        outcomeStatus = (int)CharacterFacilityUseOutcomeStatus.Completed
    };

    private static void RequireSameExecutionState(
        FacilityUseExecutionState expected,
        FacilityUseExecutionState actual,
        string context)
    {
        Require(actual != null
                && actual.operationSequence == expected.operationSequence
                && actual.operationId == expected.operationId
                && actual.actorId == expected.actorId
                && actual.actorDisplayName == expected.actorDisplayName
                && actual.facilityRole == expected.facilityRole
                && actual.serviceSessionId == expected.serviceSessionId
                && actual.actionType == expected.actionType
                && actual.actionBranch == expected.actionBranch
                && actual.phase == expected.phase
                && actual.timerInitialized == expected.timerInitialized
                && Mathf.Approximately(actual.remainingSeconds, expected.remainingSeconds)
                && Mathf.Approximately(actual.serviceSeconds, expected.serviceSeconds)
                && actual.effectsCommitted == expected.effectsCommitted
                && actual.waterPrepared == expected.waterPrepared
                && actual.waterFixtureId == expected.waterFixtureId
                && actual.waterSupplyKind == expected.waterSupplyKind
                && Mathf.Approximately(actual.wastewaterAmount, expected.wastewaterAmount)
                && actual.mealOperationId == expected.mealOperationId
                && Mathf.Approximately(
                    actual.mealWaitElapsedSeconds,
                    expected.mealWaitElapsedSeconds)
                && actual.mealSucceeded == expected.mealSucceeded
                && actual.mealDisplayName == expected.mealDisplayName
                && actual.childResultKeys != null
                && actual.childResultKeys.SequenceEqual(expected.childResultKeys),
            context + " lost active-use operation, timer, effects, or child state.");
        RequireSameVisitorState(expected.before, actual.before, context);
    }

    private static void RequireSameVisitorState(
        FacilityUseVisitorState expected,
        FacilityUseVisitorState actual,
        string context)
    {
        Require(actual != null
                && actual.actorId == expected.actorId
                && actual.displayName == expected.displayName
                && actual.position == expected.position
                && actual.runtimeActive == expected.runtimeActive
                && actual.internalStaff == expected.internalStaff
                && actual.canMove == expected.canMove
                && Mathf.Approximately(actual.stayMultiplier, expected.stayMultiplier)
                && Mathf.Approximately(actual.revenueMultiplier, expected.revenueMultiplier)
                && Mathf.Approximately(
                    actual.personalityPatience,
                    expected.personalityPatience)
                && Mathf.Approximately(actual.modelPatience, expected.modelPatience)
                && Mathf.Approximately(actual.crimeRisk, expected.crimeRisk)
                && Mathf.Approximately(
                    actual.productionMultiplier,
                    expected.productionMultiplier)
                && actual.productionBonus == expected.productionBonus
                && Mathf.Approximately(actual.stress, expected.stress)
                && Mathf.Approximately(actual.mood, expected.mood)
                && Mathf.Approximately(actual.hunger, expected.hunger)
                && Mathf.Approximately(actual.fun, expected.fun)
                && Mathf.Approximately(actual.sleep, expected.sleep)
                && Mathf.Approximately(actual.excretion, expected.excretion)
                && Mathf.Approximately(actual.hygiene, expected.hygiene)
                && Mathf.Approximately(actual.health, expected.health)
                && Mathf.Approximately(actual.maximumHealth, expected.maximumHealth)
                && Mathf.Approximately(actual.injury, expected.injury)
                && actual.conditions != null
                && actual.conditions.SequenceEqual(expected.conditions),
            context + " lost the captured visitor-before state.");
    }

    private static void RequireInvalid(FacilityRuntimeState state, string scenario)
    {
        Require(!state.IsValid(out string error)
                && !string.IsNullOrWhiteSpace(error),
            scenario + " was accepted as valid facility-use runtime state.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
