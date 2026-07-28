using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CharacterInfectionBurdenRequestedEvent
{
    public CharacterInfectionBurdenRequestedEvent(CharacterActor actor, float amount)
    {
        Actor = actor;
        Amount = Mathf.Max(0f, amount);
    }

    public CharacterActor Actor { get; }
    public float Amount { get; }
}

public readonly struct CharacterInfectionBurdenReductionRequestedEvent
{
    public CharacterInfectionBurdenReductionRequestedEvent(
        CharacterActor actor,
        float amount)
    {
        Actor = actor;
        Amount = Mathf.Max(0f, amount);
    }

    public CharacterActor Actor { get; }
    public float Amount { get; }
}

public readonly struct CharacterMentalInstabilityBurdenRequestedEvent
{
    public CharacterMentalInstabilityBurdenRequestedEvent(
        CharacterActor actor,
        float amount)
    {
        Actor = actor;
        Amount = Mathf.Max(0f, amount);
    }

    public CharacterActor Actor { get; }
    public float Amount { get; }
}

public readonly struct CharacterDeprivationDiagnosticsSnapshot
{
    public CharacterDeprivationDiagnosticsSnapshot(
        int safeReliefRequests,
        int safeReliefPlanFailures,
        int safeReliefActionsStarted,
        int safeReliefStoredStackPlans,
        int safeReliefMoveFailures,
        int safeReliefBreakdownMoveFailures,
        int safeReliefBlockedMoveFailures,
        int safeReliefOtherMoveFailures,
        int safeReliefStaleStartFailures,
        int safeReliefWallBlockedFailures,
        int safeReliefDoorDeniedFailures,
        int safeReliefDefenseReservationFailures,
        int safeReliefTraversalChangedFailures,
        int safeReliefArrivals,
        int safeReliefInteractionAttempts,
        int safeReliefSuccesses,
        int safeReliefRunningActions,
        int safeReliefActionsFinished,
        long safeReliefPlannedPathSteps,
        int safeReliefMaximumPlannedPathSteps,
        float safeReliefCompletedDurationSeconds,
        float safeReliefMaximumDurationSeconds,
        int safeReliefCancelledMoveFailures,
        int safeReliefMissingPathFailures,
        int safeReliefMissingMovementHandlerFailures,
        int safeReliefGridUnavailableFailures,
        int safeReliefInvalidSpeedFailures,
        int safeReliefNoFailureReasonFailures,
        int safeReliefActorDeadMoveFailures,
        int safeReliefActorMissingMoveFailures,
        int safeReliefCrossFloorTargetPlans,
        int safeReliefPathsWithVerticalTraversal,
        long safeReliefVerticalTraversalSteps,
        int desperateDrinkAttempts,
        int desperateDrinkStackMoveFailures,
        int desperateDrinkStackArrivals,
        int desperateDrinkStackConsumptions)
    {
        SafeReliefRequests = safeReliefRequests;
        SafeReliefPlanFailures = safeReliefPlanFailures;
        SafeReliefActionsStarted = safeReliefActionsStarted;
        SafeReliefStoredStackPlans = safeReliefStoredStackPlans;
        SafeReliefMoveFailures = safeReliefMoveFailures;
        SafeReliefBreakdownMoveFailures = safeReliefBreakdownMoveFailures;
        SafeReliefBlockedMoveFailures = safeReliefBlockedMoveFailures;
        SafeReliefOtherMoveFailures = safeReliefOtherMoveFailures;
        SafeReliefStaleStartFailures = safeReliefStaleStartFailures;
        SafeReliefWallBlockedFailures = safeReliefWallBlockedFailures;
        SafeReliefDoorDeniedFailures = safeReliefDoorDeniedFailures;
        SafeReliefDefenseReservationFailures =
            safeReliefDefenseReservationFailures;
        SafeReliefTraversalChangedFailures =
            safeReliefTraversalChangedFailures;
        SafeReliefArrivals = safeReliefArrivals;
        SafeReliefInteractionAttempts = safeReliefInteractionAttempts;
        SafeReliefSuccesses = safeReliefSuccesses;
        SafeReliefRunningActions = safeReliefRunningActions;
        SafeReliefActionsFinished = safeReliefActionsFinished;
        SafeReliefPlannedPathSteps = safeReliefPlannedPathSteps;
        SafeReliefMaximumPlannedPathSteps =
            safeReliefMaximumPlannedPathSteps;
        SafeReliefCompletedDurationSeconds =
            safeReliefCompletedDurationSeconds;
        SafeReliefMaximumDurationSeconds =
            safeReliefMaximumDurationSeconds;
        SafeReliefCancelledMoveFailures =
            safeReliefCancelledMoveFailures;
        SafeReliefMissingPathFailures =
            safeReliefMissingPathFailures;
        SafeReliefMissingMovementHandlerFailures =
            safeReliefMissingMovementHandlerFailures;
        SafeReliefGridUnavailableFailures =
            safeReliefGridUnavailableFailures;
        SafeReliefInvalidSpeedFailures =
            safeReliefInvalidSpeedFailures;
        SafeReliefNoFailureReasonFailures =
            safeReliefNoFailureReasonFailures;
        SafeReliefActorDeadMoveFailures =
            safeReliefActorDeadMoveFailures;
        SafeReliefActorMissingMoveFailures =
            safeReliefActorMissingMoveFailures;
        SafeReliefCrossFloorTargetPlans =
            safeReliefCrossFloorTargetPlans;
        SafeReliefPathsWithVerticalTraversal =
            safeReliefPathsWithVerticalTraversal;
        SafeReliefVerticalTraversalSteps =
            safeReliefVerticalTraversalSteps;
        DesperateDrinkAttempts = desperateDrinkAttempts;
        DesperateDrinkStackMoveFailures = desperateDrinkStackMoveFailures;
        DesperateDrinkStackArrivals = desperateDrinkStackArrivals;
        DesperateDrinkStackConsumptions = desperateDrinkStackConsumptions;
    }

    public int SafeReliefRequests { get; }
    public int SafeReliefPlanFailures { get; }
    public int SafeReliefActionsStarted { get; }
    public int SafeReliefStoredStackPlans { get; }
    public int SafeReliefMoveFailures { get; }
    public int SafeReliefBreakdownMoveFailures { get; }
    public int SafeReliefBlockedMoveFailures { get; }
    public int SafeReliefOtherMoveFailures { get; }
    public int SafeReliefStaleStartFailures { get; }
    public int SafeReliefWallBlockedFailures { get; }
    public int SafeReliefDoorDeniedFailures { get; }
    public int SafeReliefDefenseReservationFailures { get; }
    public int SafeReliefTraversalChangedFailures { get; }
    public int SafeReliefArrivals { get; }
    public int SafeReliefInteractionAttempts { get; }
    public int SafeReliefSuccesses { get; }
    public int SafeReliefRunningActions { get; }
    public int SafeReliefActionsFinished { get; }
    public long SafeReliefPlannedPathSteps { get; }
    public int SafeReliefMaximumPlannedPathSteps { get; }
    public float SafeReliefCompletedDurationSeconds { get; }
    public float SafeReliefMaximumDurationSeconds { get; }
    public int SafeReliefCancelledMoveFailures { get; }
    public int SafeReliefMissingPathFailures { get; }
    public int SafeReliefMissingMovementHandlerFailures { get; }
    public int SafeReliefGridUnavailableFailures { get; }
    public int SafeReliefInvalidSpeedFailures { get; }
    public int SafeReliefNoFailureReasonFailures { get; }
    public int SafeReliefActorDeadMoveFailures { get; }
    public int SafeReliefActorMissingMoveFailures { get; }
    public int SafeReliefCrossFloorTargetPlans { get; }
    public int SafeReliefPathsWithVerticalTraversal { get; }
    public long SafeReliefVerticalTraversalSteps { get; }
    public int DesperateDrinkAttempts { get; }
    public int DesperateDrinkStackMoveFailures { get; }
    public int DesperateDrinkStackArrivals { get; }
    public int DesperateDrinkStackConsumptions { get; }
}

public interface ICharacterDeprivationRuntime
{
    bool HasActiveBreakdown(CharacterActor actor);
    bool HasBreakdownKind(CharacterActor actor, CharacterBreakdownKind kind);
    bool TryGetDisplayState(
        CharacterActor actor,
        out CharacterDeprivationDisplayState displayState);
    bool TryGetSnapshot(CharacterActor actor, out CharacterDeprivationSnapshot snapshot);
    bool TryRunActiveBreakdown(CharacterActor actor, out string status);
    bool NeedsSafeEmergencyRelief(CharacterActor actor, out string reason);
    bool TryRunSafeEmergencyRelief(CharacterActor actor, out string status);
    CharacterDeprivationDiagnosticsSnapshot GetDiagnostics();
    void ResetDiagnostics();
    void BeginBreakdownAction(CharacterActor actor, CharacterBreakdownKind kind);
    bool IsSuppressible(CharacterActor actor);
    bool ApplySuppression(CharacterActor actor, float amount, out bool ended);
    float GetMoveSpeedMultiplier(CharacterActor actor);
    float GetWorkSpeedMultiplier(CharacterActor actor);
    void AddInfectionBurden(CharacterActor actor, float amount);
    void ReduceInfectionBurden(CharacterActor actor, float amount);
    void RecordTaboo(CharacterActor actor, string memory);
    void RecordTabooWitnesses(
        CharacterActor source,
        Vector2Int position,
        string memory,
        float moodPenalty);
    DungeonDarkSurvivalSaveData Capture();
    void Restore(DungeonDarkSurvivalSaveData saveData);
    bool DebugForceBreakdown(CharacterActor actor, CharacterBreakdownKind kind);
    bool DebugClearBreakdown(CharacterActor actor);
}
