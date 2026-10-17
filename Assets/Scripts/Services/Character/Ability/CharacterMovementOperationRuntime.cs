using System;
using UnityEngine;

internal sealed class CharacterMovementOperationRuntime
{
    private Vector2Int? activeManualMoveDestination;
    private Vector2Int? activeSystemMoveDestination;
    private DoorAccessOverrideKind activeSystemMoveOverride;
    private int movementOperationVersion;
    private bool protectedSystemMovementOperation;
    private bool retainProtectedSystemMovementAfterCompletion;
    private bool exitTransitOwnsMovement;
    private string activeMovementOperationOwner = string.Empty;
    public string LastMovementCancellationSourceForDiagnostics { get; private set; }
        = string.Empty;
    public string LastMovementOperationPreemptionForDiagnostics { get; private set; }
    public string LastRejectedMovementOperationOwnerForDiagnostics { get; private set; }
        = string.Empty;

    internal Vector2Int? ManualDestination => activeManualMoveDestination;
    internal Vector2Int? SystemDestination => activeSystemMoveDestination;
    internal DoorAccessOverrideKind SystemOverride => activeSystemMoveOverride;
    internal int Version => movementOperationVersion;
    internal string Owner => activeMovementOperationOwner;
    internal bool HasRetainedProtection => retainProtectedSystemMovementAfterCompletion && protectedSystemMovementOperation;
    internal bool ExitTransitOwnsMovement => exitTransitOwnsMovement;

    internal void BeginManualMove(Vector2Int destination) => activeManualMoveDestination = destination;
    internal void CompleteManualMove() => activeManualMoveDestination = null;
    internal void BeginSystemMove(Vector2Int destination, DoorAccessOverrideKind overrideKind, bool retainOwnership)
    {
        protectedSystemMovementOperation = true;
        retainProtectedSystemMovementAfterCompletion = retainOwnership;
        activeSystemMoveDestination = destination;
        activeSystemMoveOverride = overrideKind;
    }
    internal void RetainSystemOwnership()
    {
        retainProtectedSystemMovementAfterCompletion = true;
        protectedSystemMovementOperation = true;
    }
    internal void CompleteSystemMove()
    {
        if (!retainProtectedSystemMovementAfterCompletion) protectedSystemMovementOperation = false;
        ClearSystemDestination();
    }
    internal void ClearSystemDestination()
    {
        activeSystemMoveDestination = null;
        activeSystemMoveOverride = DoorAccessOverrideKind.None;
    }
    internal void BeginExitOverride(DoorAccessOverrideKind overrideKind) => activeSystemMoveOverride = overrideKind;
    internal void BeginTrackedMovement(bool preserveExitTransit) => exitTransitOwnsMovement = preserveExitTransit;
    internal void CompleteTrackedMovement() => exitTransitOwnsMovement = false;
    internal void AbandonOperation()
    {
        protectedSystemMovementOperation = false;
        retainProtectedSystemMovementAfterCompletion = false;
        exitTransitOwnsMovement = false;
        activeMovementOperationOwner = string.Empty;
    }
    internal void NoteCancellation(bool hasActiveRoutine, string cancellationSource)
    {
        bool hadActiveMovement = hasActiveRoutine
            || activeManualMoveDestination.HasValue
            || activeSystemMoveDestination.HasValue
            || !string.IsNullOrWhiteSpace(activeMovementOperationOwner);
        if (hadActiveMovement)
        {
            LastMovementCancellationSourceForDiagnostics =
                cancellationSource ?? string.Empty;
            LastMovementOperationPreemptionForDiagnostics =
                $"{activeMovementOperationOwner}->cancel:{cancellationSource}";
        }
    }
    internal bool TryBeginMovementOperation(
        string operationOwner,
        out int operationVersion)
    {
        string normalizedOwner = string.IsNullOrWhiteSpace(operationOwner)
            ? "unknown"
            : operationOwner;
        if (protectedSystemMovementOperation
            && !string.Equals(
                normalizedOwner,
                "protected-system-command",
                StringComparison.Ordinal))
        {
            LastRejectedMovementOperationOwnerForDiagnostics =
                normalizedOwner;
            operationVersion = movementOperationVersion;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(activeMovementOperationOwner))
        {
            LastMovementOperationPreemptionForDiagnostics =
                $"{activeMovementOperationOwner}->{normalizedOwner}";
        }
        LastMovementCancellationSourceForDiagnostics = string.Empty;
        LastRejectedMovementOperationOwnerForDiagnostics = string.Empty;
        InvalidateMovementOperation();
        activeMovementOperationOwner = normalizedOwner;
        operationVersion = movementOperationVersion;
        return true;
    }

    internal void CompleteMovementOperation(
        int operationVersion,
        string operationOwner)
    {
        if (operationVersion == movementOperationVersion
            && string.Equals(
                activeMovementOperationOwner,
                operationOwner,
                StringComparison.Ordinal))
        {
            activeMovementOperationOwner = string.Empty;
        }
    }

    internal void InvalidateMovementOperation()
    {
        unchecked
        {
            movementOperationVersion++;
        }
    }

}
