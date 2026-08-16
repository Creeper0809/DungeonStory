using UnityEngine;

/// <summary>
/// Run-scoped, non-persistent diagnostics for emergency deprivation relief.
/// Gameplay state remains in <see cref="CharacterDeprivationRuntime"/>.
/// </summary>
public sealed class CharacterDeprivationDiagnostics
{
    public int SafeReliefRequests { get; set; }
    public int SafeReliefPlanFailures { get; set; }
    public int SafeReliefPlanSearchPending { get; set; }
    public int SafeReliefPlanReservationRejected { get; set; }
    public int SafeReliefPlanNoSource { get; set; }
    public string LastSafeReliefPlanFailureDetail { get; set; } = string.Empty;
    public int SafeReliefActionsStarted { get; set; }
    public int SafeReliefStoredStackPlans { get; set; }
    public int SafeReliefMoveFailures { get; set; }
    public int SafeReliefBreakdownMoveFailures { get; set; }
    public int SafeReliefBlockedMoveFailures { get; set; }
    public int SafeReliefOtherMoveFailures { get; set; }
    public int SafeReliefStaleStartFailures { get; set; }
    public int SafeReliefWallBlockedFailures { get; set; }
    public int SafeReliefDoorDeniedFailures { get; set; }
    public int SafeReliefDefenseReservationFailures { get; set; }
    public int SafeReliefTraversalChangedFailures { get; set; }
    public int SafeReliefArrivals { get; set; }
    public int SafeReliefInteractionAttempts { get; set; }
    public int SafeReliefSuccesses { get; set; }
    public int SafeReliefActionsFinished { get; set; }
    public long SafeReliefPlannedPathSteps { get; set; }
    public int SafeReliefMaximumPlannedPathSteps { get; set; }
    public float SafeReliefCompletedDurationSeconds { get; set; }
    public float SafeReliefMaximumDurationSeconds { get; set; }
    public int SafeReliefCancelledMoveFailures { get; set; }
    public int SafeReliefMissingPathFailures { get; set; }
    public int SafeReliefMissingMovementHandlerFailures { get; set; }
    public int SafeReliefGridUnavailableFailures { get; set; }
    public int SafeReliefInvalidSpeedFailures { get; set; }
    public int SafeReliefNoFailureReasonFailures { get; set; }
    public int SafeReliefActorDeadMoveFailures { get; set; }
    public int SafeReliefActorMissingMoveFailures { get; set; }
    public int SafeReliefCrossFloorTargetPlans { get; set; }
    public int SafeReliefPathsWithVerticalTraversal { get; set; }
    public long SafeReliefVerticalTraversalSteps { get; set; }
    public int DesperateDrinkAttempts { get; set; }
    public int DesperateDrinkStackMoveFailures { get; set; }
    public int DesperateDrinkStackArrivals { get; set; }
    public int DesperateDrinkStackConsumptions { get; set; }

    public CharacterDeprivationDiagnosticsSnapshot Capture(int activeActions)
    {
        return new CharacterDeprivationDiagnosticsSnapshot(
            SafeReliefRequests,
            SafeReliefPlanFailures,
            SafeReliefActionsStarted,
            SafeReliefStoredStackPlans,
            SafeReliefMoveFailures,
            SafeReliefBreakdownMoveFailures,
            SafeReliefBlockedMoveFailures,
            SafeReliefOtherMoveFailures,
            SafeReliefStaleStartFailures,
            SafeReliefWallBlockedFailures,
            SafeReliefDoorDeniedFailures,
            SafeReliefDefenseReservationFailures,
            SafeReliefTraversalChangedFailures,
            SafeReliefArrivals,
            SafeReliefInteractionAttempts,
            SafeReliefSuccesses,
            Mathf.Max(0, activeActions),
            SafeReliefActionsFinished,
            SafeReliefPlannedPathSteps,
            SafeReliefMaximumPlannedPathSteps,
            SafeReliefCompletedDurationSeconds,
            SafeReliefMaximumDurationSeconds,
            SafeReliefCancelledMoveFailures,
            SafeReliefMissingPathFailures,
            SafeReliefMissingMovementHandlerFailures,
            SafeReliefGridUnavailableFailures,
            SafeReliefInvalidSpeedFailures,
            SafeReliefNoFailureReasonFailures,
            SafeReliefActorDeadMoveFailures,
            SafeReliefActorMissingMoveFailures,
            SafeReliefCrossFloorTargetPlans,
            SafeReliefPathsWithVerticalTraversal,
            SafeReliefVerticalTraversalSteps,
            DesperateDrinkAttempts,
            DesperateDrinkStackMoveFailures,
            DesperateDrinkStackArrivals,
            DesperateDrinkStackConsumptions,
            SafeReliefPlanSearchPending,
            SafeReliefPlanReservationRejected,
            SafeReliefPlanNoSource,
            LastSafeReliefPlanFailureDetail);
    }

    public void Reset()
    {
        SafeReliefRequests = 0;
        SafeReliefPlanFailures = 0;
        SafeReliefPlanSearchPending = 0;
        SafeReliefPlanReservationRejected = 0;
        SafeReliefPlanNoSource = 0;
        LastSafeReliefPlanFailureDetail = string.Empty;
        SafeReliefActionsStarted = 0;
        SafeReliefStoredStackPlans = 0;
        SafeReliefMoveFailures = 0;
        SafeReliefBreakdownMoveFailures = 0;
        SafeReliefBlockedMoveFailures = 0;
        SafeReliefOtherMoveFailures = 0;
        SafeReliefStaleStartFailures = 0;
        SafeReliefWallBlockedFailures = 0;
        SafeReliefDoorDeniedFailures = 0;
        SafeReliefDefenseReservationFailures = 0;
        SafeReliefTraversalChangedFailures = 0;
        SafeReliefArrivals = 0;
        SafeReliefInteractionAttempts = 0;
        SafeReliefSuccesses = 0;
        SafeReliefActionsFinished = 0;
        SafeReliefPlannedPathSteps = 0L;
        SafeReliefMaximumPlannedPathSteps = 0;
        SafeReliefCompletedDurationSeconds = 0f;
        SafeReliefMaximumDurationSeconds = 0f;
        SafeReliefCancelledMoveFailures = 0;
        SafeReliefMissingPathFailures = 0;
        SafeReliefMissingMovementHandlerFailures = 0;
        SafeReliefGridUnavailableFailures = 0;
        SafeReliefInvalidSpeedFailures = 0;
        SafeReliefNoFailureReasonFailures = 0;
        SafeReliefActorDeadMoveFailures = 0;
        SafeReliefActorMissingMoveFailures = 0;
        SafeReliefCrossFloorTargetPlans = 0;
        SafeReliefPathsWithVerticalTraversal = 0;
        SafeReliefVerticalTraversalSteps = 0L;
        DesperateDrinkAttempts = 0;
        DesperateDrinkStackMoveFailures = 0;
        DesperateDrinkStackArrivals = 0;
        DesperateDrinkStackConsumptions = 0;
    }
}
