using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

internal static class CaptivityAbilityAdapterFactory
{
    public static AbilityCaptiveEscape EnsureEscape(
        CharacterActor actor,
        ICaptivityEscapeRuntime runtime,
        IGameClock clock)
    {
        AbilityCaptiveEscape ability = Ensure<AbilityCaptiveEscape>(actor);
        ability?.Configure(
            new CaptiveEscapeAbilityUnityPort(actor, runtime),
            clock);
        return ability;
    }

    public static AbilityCaptiveEscort EnsureEscort(
        CharacterActor actor,
        ICaptivityEscortRuntime runtime,
        IGameClock clock)
    {
        AbilityCaptiveEscort ability = Ensure<AbilityCaptiveEscort>(actor);
        ability?.Configure(
            new CaptiveEscortAbilityUnityPort(actor, runtime),
            clock);
        return ability;
    }

    public static AbilityWildlifeCaptureTransport EnsureWildlifeTransport(
        CharacterActor actor,
        IWildlifeCaptureTransportRuntime runtime)
    {
        AbilityWildlifeCaptureTransport ability =
            Ensure<AbilityWildlifeCaptureTransport>(actor);
        ability?.Configure(
            new WildlifeCaptureTransportAbilityUnityPort(actor, runtime));
        return ability;
    }

    private static T Ensure<T>(CharacterActor actor) where T : Component
    {
        if (actor == null)
        {
            return null;
        }

        T ability = actor.GetComponent<T>();
        if (ability == null && Application.isPlaying)
        {
            ability = actor.gameObject.AddComponent<T>();
        }

        return ability;
    }
}

internal abstract class CaptivityAbilityUnityPort
{
    protected CaptivityAbilityUnityPort(CharacterActor actor)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Move = actor.GetComponent<AbilityMove>()
            ?? throw new InvalidOperationException($"{nameof(AbilityMove)} is required.");
    }

    protected CharacterActor Actor { get; }
    protected AbilityMove Move { get; }

    protected bool TryCreateMovement(
        Vector2Int destination,
        CaptivityAbilityAccessKind accessKind,
        out IEnumerator movement)
    {
        movement = null;
        if (Actor.WorldRegistry == null
            || !Actor.WorldRegistry.TryGetGrid(out Grid grid))
        {
            return false;
        }

        if (Actor.GetNowXY() == destination)
        {
            movement = EmptyMovement();
            return true;
        }

        Queue<GridMoveStep> path = Actor.PathSearchBroker?.GetMovePathTo(
            grid,
            Actor.GetNowXY(),
            destination,
            GridPathSearchPriority.Urgent,
            GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(Actor),
                ToDoorAccessOverride(accessKind)));
        if (path == null || path.Count == 0)
        {
            return false;
        }

        movement = Move.MoveByPath(path);
        return true;
    }

    protected void SetActionPhase(string phase, string detail) =>
        Actor.Brain?.SetActionPhase(phase, null, detail);

    protected void RequestImmediateReplan(bool clearFailures) =>
        Actor.Brain?.RequestImmediateReplan(clearFailures);

    protected bool TryCreateAdjacentMovement(
        Vector2Int target,
        CaptivityAbilityAccessKind accessKind,
        out IEnumerator movement)
    {
        movement = null;
        if (Actor.WorldRegistry == null
            || !Actor.WorldRegistry.TryGetGrid(out Grid grid))
        {
            return false;
        }

        Vector2Int start = Actor.GetNowXY();
        if (Mathf.Abs(start.x - target.x) + Mathf.Abs(start.y - target.y) <= 1)
        {
            movement = EmptyMovement();
            return true;
        }

        GridTraversalContext context = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(Actor),
            ToDoorAccessOverride(accessKind));
        Queue<GridMoveStep> bestPath = null;
        Vector2Int[] candidates =
        {
            target + Vector2Int.left,
            target + Vector2Int.right,
            target + Vector2Int.up,
            target + Vector2Int.down,
            target
        };
        foreach (Vector2Int candidate in candidates)
        {
            if (!grid.IsValidGridPos(candidate) || !grid.IsWalkable(candidate))
            {
                continue;
            }
            Queue<GridMoveStep> path = Actor.PathSearchBroker?.GetMovePathTo(
                grid,
                start,
                candidate,
                GridPathSearchPriority.Urgent,
                context);
            if (path != null
                && path.Count > 0
                && (bestPath == null || path.Count < bestPath.Count))
            {
                bestPath = path;
            }
        }

        if (bestPath == null)
        {
            return false;
        }
        movement = Move.MoveByPath(bestPath);
        return true;
    }

    private static DoorAccessOverrideKind ToDoorAccessOverride(
        CaptivityAbilityAccessKind accessKind) =>
        accessKind switch
        {
            CaptivityAbilityAccessKind.EscortPass => DoorAccessOverrideKind.EscortPass,
            CaptivityAbilityAccessKind.CaptiveEscape => DoorAccessOverrideKind.CaptiveEscape,
            _ => DoorAccessOverrideKind.None
        };

    protected static IEnumerator EmptyMovement()
    {
        yield break;
    }
}

internal sealed class CaptiveEscapeAbilityUnityPort :
    CaptivityAbilityUnityPort,
    ICaptiveEscapeAbilityPort
{
    private readonly ICaptivityEscapeRuntime runtime;

    public CaptiveEscapeAbilityUnityPort(
        CharacterActor actor,
        ICaptivityEscapeRuntime runtime)
        : base(actor)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public bool IsAlive => Actor != null
        && !Actor.IsDead
        && Actor.isActiveAndEnabled
        && Actor.CurrentLifecycleState == CharacterLifecycleState.Active;
    public Vector2Int Position => Actor.GetNowXY();

    public bool TryGetEscapeState(
        string captiveId,
        out Vector2Int destination,
        out string failureReason) =>
        runtime.TryGetEscapeState(captiveId, Actor, out destination, out failureReason);

    public IDisposable BeginEscapePass(string captiveId) =>
        runtime.BeginEscapePass(Actor, captiveId);

    public bool TryStartSystemMove(
        Vector2Int destination,
        out string failureReason)
    {
        if (Move.IsSystemMoveInProgressTo(destination))
        {
            failureReason = string.Empty;
            return true;
        }

        return Move.TryStartSystemMove(
            destination,
            DoorAccessOverrideKind.CaptiveEscape,
            out failureReason);
    }

    public void CompleteEscape(string captiveId) =>
        runtime.CompleteEscape(captiveId, Actor);

    public void FailEscape(string captiveId, string reason) =>
        runtime.FailEscape(captiveId, Actor, reason);

    public void SetActionPhase(string phase, Vector2Int destination) =>
        SetActionPhase(phase, destination.ToString());
}

internal sealed class CaptiveEscortAbilityUnityPort :
    CaptivityAbilityUnityPort,
    ICaptiveEscortAbilityPort
{
    private readonly ICaptivityEscortRuntime runtime;
    private CharacterActionIntentLease actionIntentLease;

    public CaptiveEscortAbilityUnityPort(
        CharacterActor actor,
        ICaptivityEscortRuntime runtime)
        : base(actor)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public bool TryBeginActionOwnership(
        string captiveId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedId = captiveId?.Trim() ?? string.Empty;
        if (normalizedId.Length == 0 || Actor?.Brain == null)
        {
            failureReason = "Captive escort requires an actor brain and captive id.";
            return false;
        }

        string ownerId = $"captivity:escort:{normalizedId}";
        if (!Actor.Brain.TryBeginExternallyDrivenAction(
                ownerId,
                CharacterActionIntentKind.ProtectedAction,
                "Captive escort",
                "Preparing escort",
                normalizedId,
                out CharacterActionIntentLease lease))
        {
            failureReason = "Captive escort could not acquire AI action ownership.";
            return false;
        }

        actionIntentLease = lease;
        return true;
    }

    public bool HasActionOwnership() =>
        actionIntentLease.IsValid
        && Actor?.Brain?.IsExternalIntentCurrent(actionIntentLease) == true;

    public void EndActionOwnership(bool clearFailures)
    {
        CharacterActionIntentLease lease = actionIntentLease;
        actionIntentLease = default;
        if (lease.IsValid
            && Actor?.Brain?.IsExternalIntentCurrent(lease) == true)
        {
            Actor.Brain.EndExternallyDrivenAction(lease, clearFailures);
        }
    }

    public bool TryGetState(
        string captiveId,
        out CaptiveState state,
        out Vector2Int subjectPosition,
        out string subjectDisplayName,
        out string failureReason)
    {
        bool found = runtime.TryGetEscortState(
            captiveId,
            Actor,
            out state,
            out CharacterActor subject,
            out failureReason);
        subjectPosition = subject != null ? subject.GetNowXY() : default;
        subjectDisplayName = subject?.Identity?.DisplayName ?? string.Empty;
        return found;
    }

    public new bool TryCreateMovement(
        Vector2Int destination,
        CaptivityAbilityAccessKind accessKind,
        out IEnumerator movement) =>
        base.TryCreateMovement(destination, accessKind, out movement);

    public bool TryPickupReservedRestraint(
        CaptiveState state,
        out string failureReason) =>
        runtime.TryPickupReservedRestraint(state, Actor, out failureReason);

    public float AdvanceStabilization(string captiveId, float deltaSeconds) =>
        runtime.AdvanceStabilization(
            captiveId,
            Actor,
            Actor.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Warden)
                * Mathf.Max(0f, deltaSeconds));

    public bool TryBeginEscort(string captiveId, out string failureReason) =>
        runtime.TryBeginEscort(captiveId, Actor, out failureReason);

    public IDisposable BeginEscortPass(string captiveId) =>
        runtime.BeginEscortPass(Actor, captiveId);

    public bool TryCompleteEscort(string captiveId, out string failureReason) =>
        runtime.TryCompleteEscort(captiveId, Actor, out failureReason);

    public void FailEscort(string captiveId, string reason) =>
        runtime.FailEscort(captiveId, Actor, reason);

    public new void SetActionPhase(string phase, string detail) =>
        base.SetActionPhase(phase, detail);

    public new void RequestImmediateReplan(bool clearFailures) =>
        base.RequestImmediateReplan(clearFailures);
}

internal sealed class WildlifeCaptureTransportAbilityUnityPort :
    CaptivityAbilityUnityPort,
    IWildlifeCaptureTransportAbilityPort
{
    private readonly IWildlifeCaptureTransportRuntime runtime;
    private CharacterActionIntentLease actionIntentLease;
    private Queue<GridMoveStep> resolvedDeliveryPath;

    public WildlifeCaptureTransportAbilityUnityPort(
        CharacterActor actor,
        IWildlifeCaptureTransportRuntime runtime)
        : base(actor)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public bool TryBeginActionOwnership(
        string wildlifeId,
        out string failureReason)
    {
        resolvedDeliveryPath = null;
        failureReason = string.Empty;
        string normalizedId = wildlifeId?.Trim() ?? string.Empty;
        if (normalizedId.Length == 0 || Actor?.Brain == null)
        {
            failureReason =
                "Wildlife transport requires an actor brain and wildlife id.";
            return false;
        }

        string ownerId = $"captivity:wildlife-transport:{normalizedId}";
        if (!Actor.Brain.TryBeginExternallyDrivenAction(
                ownerId,
                CharacterActionIntentKind.ProtectedAction,
                "Wildlife transport",
                "Preparing captured wildlife transport",
                normalizedId,
                out CharacterActionIntentLease lease))
        {
            failureReason =
                "Wildlife transport could not acquire AI action ownership.";
            return false;
        }

        actionIntentLease = lease;
        // The external intent and movement ownership must cross the boundary
        // atomically. A raw path coroutine selected immediately before this
        // lease can otherwise advance during the first yielded pickup frame and
        // invalidate the already-validated adjacent animal distance.
        Move.CancelActiveMovement(
            "wildlife-transport-action-ownership-acquired");
        return true;
    }

    public bool HasActionOwnership() =>
        actionIntentLease.IsValid
        && Actor?.Brain?.IsExternalIntentCurrent(actionIntentLease) == true;

    public void EndActionOwnership(bool clearFailures)
    {
        resolvedDeliveryPath = null;
        CharacterActionIntentLease lease = actionIntentLease;
        actionIntentLease = default;
        if (lease.IsValid
            && Actor?.Brain?.IsExternalIntentCurrent(lease) == true)
        {
            if (clearFailures)
            {
                Actor.Brain.EndExternallyDrivenAction(lease, true);
            }
            else
            {
                Actor.Brain.FailExternallyDrivenAction(lease);
            }
        }
    }

    public void CancelActionOwnership()
    {
        resolvedDeliveryPath = null;
        CharacterActionIntentLease lease = actionIntentLease;
        actionIntentLease = default;
        if (lease.IsValid
            && Actor?.Brain?.IsExternalIntentCurrent(lease) == true)
        {
            Actor.Brain.CancelExternallyDrivenAction(lease);
        }
    }

    public bool TryGetTransportState(
        string wildlifeId,
        out CapturedWildlifeState state,
        out Vector2Int wildlifePosition,
        out string wildlifeDisplayName,
        out string failureReason)
    {
        bool found = runtime.TryGetTransportState(
            wildlifeId,
            Actor,
            out state,
            out WildlifeActor wildlife,
            out failureReason);
        wildlifePosition = wildlife != null ? wildlife.GridPosition : default;
        wildlifeDisplayName = wildlife?.DisplayName ?? string.Empty;
        return found;
    }

    public new bool TryCreateMovement(
        Vector2Int destination,
        CaptivityAbilityAccessKind accessKind,
        out IEnumerator movement) =>
        base.TryCreateMovement(destination, accessKind, out movement);

    public bool TryCreatePickupMovement(
        Vector2Int wildlifePosition,
        out IEnumerator movement) =>
        base.TryCreateAdjacentMovement(
            wildlifePosition,
            CaptivityAbilityAccessKind.None,
            out movement);

    public bool TryCreateDeliveryMovement(
        Vector2Int destination,
        out IEnumerator movement)
    {
        if (Actor.GetNowXY() == destination)
        {
            resolvedDeliveryPath = null;
            movement = EmptyMovement();
            return true;
        }

        Queue<GridMoveStep> path = resolvedDeliveryPath;
        resolvedDeliveryPath = null;
        if (!Move.TryStartProtectedSystemMoveWithResolvedPath(
                destination,
                DoorAccessOverrideKind.EscortPass,
                path,
                out _))
        {
            movement = null;
            return false;
        }
        movement = WaitForSystemMovement();
        return true;
    }

    public bool TryValidateMovementArrival(
        Vector2Int destination,
        out string failureReason)
    {
        Vector2Int actual = Actor.GetNowXY();
        GridMoveFailureReason movementFailure =
            Move.LastGridMoveFailureReason;
        bool arrived = actual == destination
            && movementFailure == GridMoveFailureReason.None;
        failureReason = arrived
            ? string.Empty
            : $"planned={destination};pathEnd={destination};carrier={actual};"
                + $"movementFailure={movementFailure};"
                + "cancellationSource="
                + Move.LastMovementCancellationSourceForDiagnostics
                + ";operationPreemption="
                + Move.LastMovementOperationPreemptionForDiagnostics
                + ";rejectedOperation="
                + Move.LastRejectedMovementOperationOwnerForDiagnostics;
        return arrived;
    }

    private IEnumerator WaitForSystemMovement()
    {
        while (Move.IsSystemMoveInProgress)
        {
            yield return null;
        }
    }

    public bool TryBeginCarry(string wildlifeId, out string failureReason) =>
        runtime.TryBeginCarry(wildlifeId, Actor, out failureReason);

    public WildlifeDeliveryStandResolution ResolveDeliveryStand(
        string wildlifeId,
        out CapturedWildlifeState state,
        out string failureReason)
    {
        WildlifeDeliveryStandResolution resolution =
            runtime.ResolveDeliveryStand(
            wildlifeId,
            Actor,
            out state,
            out Queue<GridMoveStep> path,
            out failureReason);
        resolvedDeliveryPath = resolution == WildlifeDeliveryStandResolution.Ready
            ? path
            : null;
        return resolution;
    }

    public IDisposable BeginTransportPass(string wildlifeId) =>
        runtime.BeginTransportPass(Actor, wildlifeId);

    public bool TryCompleteCarry(string wildlifeId, out string failureReason) =>
        runtime.TryCompleteCarry(wildlifeId, Actor, out failureReason);

    public void FailCarry(string wildlifeId, string reason) =>
        runtime.FailCarry(wildlifeId, Actor, reason);

    public new void SetActionPhase(string phase, string detail) =>
        base.SetActionPhase(phase, detail);

    public new void RequestImmediateReplan(bool clearFailures) =>
        base.RequestImmediateReplan(clearFailures);
}
