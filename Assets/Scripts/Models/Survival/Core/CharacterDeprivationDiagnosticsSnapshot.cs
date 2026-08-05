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
        SafeReliefDefenseReservationFailures = safeReliefDefenseReservationFailures;
        SafeReliefTraversalChangedFailures = safeReliefTraversalChangedFailures;
        SafeReliefArrivals = safeReliefArrivals;
        SafeReliefInteractionAttempts = safeReliefInteractionAttempts;
        SafeReliefSuccesses = safeReliefSuccesses;
        SafeReliefRunningActions = safeReliefRunningActions;
        SafeReliefActionsFinished = safeReliefActionsFinished;
        SafeReliefPlannedPathSteps = safeReliefPlannedPathSteps;
        SafeReliefMaximumPlannedPathSteps = safeReliefMaximumPlannedPathSteps;
        SafeReliefCompletedDurationSeconds = safeReliefCompletedDurationSeconds;
        SafeReliefMaximumDurationSeconds = safeReliefMaximumDurationSeconds;
        SafeReliefCancelledMoveFailures = safeReliefCancelledMoveFailures;
        SafeReliefMissingPathFailures = safeReliefMissingPathFailures;
        SafeReliefMissingMovementHandlerFailures = safeReliefMissingMovementHandlerFailures;
        SafeReliefGridUnavailableFailures = safeReliefGridUnavailableFailures;
        SafeReliefInvalidSpeedFailures = safeReliefInvalidSpeedFailures;
        SafeReliefNoFailureReasonFailures = safeReliefNoFailureReasonFailures;
        SafeReliefActorDeadMoveFailures = safeReliefActorDeadMoveFailures;
        SafeReliefActorMissingMoveFailures = safeReliefActorMissingMoveFailures;
        SafeReliefCrossFloorTargetPlans = safeReliefCrossFloorTargetPlans;
        SafeReliefPathsWithVerticalTraversal = safeReliefPathsWithVerticalTraversal;
        SafeReliefVerticalTraversalSteps = safeReliefVerticalTraversalSteps;
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
