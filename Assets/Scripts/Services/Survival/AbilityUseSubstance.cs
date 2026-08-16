using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AbilityUseSubstance : MonoBehaviour
{
    private const int MaximumPathResolveFrames = 240;

    private CharacterActor actor;
    private AbilityMove move;
    private Coroutine routine;
    private bool useExecutionActive;
    private WorldItemReservedStackQuantity reservation;
    private CharacterSubstanceUseRequest request;
#if UNITY_EDITOR
    public System.Action<WorldItemReservedStackQuantity, Vector2Int>
        DebugAfterQuantityLeaseReserved;
#endif

    public bool IsUsingSubstance => useExecutionActive;
    public CharacterSubstanceUseRequest ActiveRequest => request;

    private ICharacterSubstanceRuntime Substances =>
        actor?.SubstanceRuntime;
    private IWorldItemStackRuntime Items =>
        actor?.WorldItemStackRuntime;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            StopUse("비활성화");
        }
    }

    public static AbilityUseSubstance Ensure(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        AbilityUseSubstance ability =
            actor.GetComponent<AbilityUseSubstance>();
        if (ability == null && Application.isPlaying)
        {
            ability = actor.gameObject.AddComponent<AbilityUseSubstance>();
        }

        ability?.CacheReferences();
        return ability;
    }

    public bool CanStart(out CharacterSubstanceUseRequest next)
    {
        CacheReferences();
        next = default;
        return actor != null
            && move != null
            && Items != null
            && Substances?.TryGetAutomaticUseRequest(actor, out next) == true;
    }

    public void StartUse()
    {
        // Behavior-tree leaves may be polled again while their coroutine is
        // already running. Re-entry is an idempotent acknowledgement, not a
        // failed second attempt; failing here terminates the live epoch while
        // the first coroutine still owns its reservation and causes a hot
        // retry loop.
        if (IsUsingSubstance)
        {
            return;
        }

        if (!CanStart(out CharacterSubstanceUseRequest next))
        {
            FailAiAction(
                AIActionFailureKind.CannotStart,
                "No automatic substance-use request is currently valid.");
            return;
        }

        request = next;
        useExecutionActive = true;
        Coroutine started = StartCoroutine(UseRoutine());
        // StartCoroutine advances to the first yield synchronously. Do not
        // resurrect a handle after an immediate terminal path cleared it.
        routine = useExecutionActive ? started : null;
    }

    public void StopUse(string reason)
    {
        useExecutionActive = false;
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        ReleaseReservation();
        request = default;
    }

    private IEnumerator UseRoutine()
    {
        if (request.UseClass == SubstanceUseClass.Recreational
            && TryFindRecreationalVenue(out Facility venue))
        {
            actor.Brain?.SetActionPhase(
                $"{venue.BuildingData?.objectName ?? venue.name}에서 음료 즐기기",
                venue,
                request.Reason);
            yield return venue.Interact(actor.BuildingVisitor);
            if (venue == null || venue.isDestroy)
            {
                FinishFailed(
                    AIActionFailureKind.Destroyed,
                    "The recreational venue was destroyed during substance use.");
            }
            else
            {
                FinishCompleted();
            }
            yield break;
        }

        CharacterCarryInventory inventory =
            CharacterCarryInventory.Ensure(actor);
        if (inventory == null || Items == null || Substances == null)
        {
            FinishFailed(
                AIActionFailureKind.Unsupported,
                "Substance inventory or runtime collaborators are unavailable.");
            yield break;
        }

        if (inventory.CountItem(request.ItemId) <= 0)
        {
            string actorId = CharacterPersistentIdentity.Require(actor).Value;
            string operationId =
                $"substance-use:{actorId}:{(actor.Brain?.RuntimeActionEpoch ?? 0L):D16}";
            string reserveFailure = string.Empty;
            if (Items is not IWorldItemQuantityLeaseRuntime leaseRuntime
                || !leaseRuntime.TryReserveAvailableItemForDirectPickup(
                    actor,
                    request.ItemId,
                    1,
                    ItemReservationPurpose.PersonalConsumption,
                    operationId,
                    out reservation,
                    out Vector2Int pickupStand,
                    out reserveFailure))
            {
                actor.Brain?.SetActionPhase(
                    "복용품 확보 실패",
                    null,
                    reserveFailure);
                FinishFailed(AIActionFailureKind.ResourceUnavailable, reserveFailure);
                yield break;
            }
#if UNITY_EDITOR
            DebugAfterQuantityLeaseReserved?.Invoke(reservation, pickupStand);
#endif

            actor.Brain?.SetActionPhase(
                $"{request.DisplayName} 가지러 이동",
                null,
                request.Reason);
            bool reached = false;
            yield return MoveToPickup(pickupStand, value => reached = value);
            if (!reached)
            {
                FinishFailed(
                    AIActionFailureKind.NoPath,
                    "The reserved substance pickup position is unreachable.");
                yield break;
            }

            if (!Items.TryPickupReservedStackQuantity(
                    actor,
                    inventory,
                    reservation,
                    out int pickedUp,
                    out string pickupFailure)
                || pickedUp <= 0)
            {
                actor.Brain?.SetActionPhase(
                    "복용품 수거 실패",
                    null,
                    pickupFailure);
                FinishFailed(AIActionFailureKind.ConsumptionFailed, pickupFailure);
                yield break;
            }

            leaseRuntime.ReleaseQuantityLease(
                reservation.LeaseId,
                ItemReservationReleaseReason.Completed);
            reservation = default;
        }

        actor.Brain?.SetActionPhase(
            $"{request.DisplayName} 복용",
            null,
            request.Reason);
        CharacterCarriedItemSaveData carried = inventory.Items.FirstOrDefault(item =>
            item != null
            && item.quantity > 0
            && string.Equals(item.itemId, request.ItemId, System.StringComparison.Ordinal));
        if (carried == null)
        {
            FinishFailed(
                AIActionFailureKind.ConsumptionFailed,
                "The picked substance is not present in the actor inventory.");
            yield break;
        }
        ItemStackId physicalStackId = new(carried.carriedStackId);
        ConsumableOperationId consumeOperation = new(
            $"consumable-operation:ai-substance:{CharacterPersistentIdentity.Require(actor).Value}:"
            + $"{(actor.Brain?.RuntimeActionEpoch ?? 0L):D16}");
        if (!Substances.TryConsume(
                new ConsumeSubstanceCommand(
                    consumeOperation,
                    CharacterPersistentIdentity.Require(actor),
                    request.ItemDefinitionId,
                    physicalStackId,
                    request.MedicalContext,
                    request.CombatContext),
                out SubstanceUseResult result))
        {
            string failureCode = result.FailureCode.ToString();
            actor.Brain?.SetActionPhase(
                failureCode,
                null,
                failureCode);
            yield return null;
            FinishFailed(AIActionFailureKind.ConsumptionFailed, failureCode);
            yield break;
        }
        else
        {
            actor.AddLog($"{request.DisplayName}을 복용했다.");
        }

        yield return null;
        FinishCompleted();
    }

    private bool TryFindRecreationalVenue(out Facility venue)
    {
        venue = actor?.WorldRegistry?.Buildings
            ?.OfType<Facility>()
            .Where(candidate => candidate != null
                && !candidate.isDestroy
                && candidate.BuildingData?
                    .GetAbility<BuildingRecreationalSubstanceServiceAbility>()?
                    .IsValid == true
                && candidate.CanVisit(actor.BuildingVisitor, out _))
            .OrderBy(candidate => Vector2Int.Distance(
                actor.GetNowXY(),
                candidate.centerPos))
            .ThenBy(candidate => candidate.RequirePersistentInstanceId().Value,
                System.StringComparer.Ordinal)
            .FirstOrDefault();
        return venue != null;
    }

    private IEnumerator MoveToPickup(
        Vector2Int target,
        System.Action<bool> onResolved)
    {
        if (actor?.WorldRegistry == null
            || !actor.WorldRegistry.TryGetGrid(out Grid grid)
            || actor.PathSearchBroker == null)
        {
            onResolved?.Invoke(false);
            yield break;
        }

        if (actor.GetNowXY() == target)
        {
            onResolved?.Invoke(true);
            yield break;
        }

        Queue<GridMoveStep> path = null;
        GridTraversalContext traversal =
            GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(actor),
                movementIntent: GridMovementIntent.SafeChore);
        for (int frame = 0; frame < MaximumPathResolveFrames; frame++)
        {
            if (!IsCurrentAction())
            {
                onResolved?.Invoke(false);
                yield break;
            }

            GridPathRequestStatus status =
                actor.PathSearchBroker.RequestMovePathTo(
                    grid,
                    actor.GetNowXY(),
                    target,
                    out path,
                    GridPathSearchPriority.Urgent,
                    traversal);
            if (status == GridPathRequestStatus.Reachable)
            {
                break;
            }

            if (status == GridPathRequestStatus.Unreachable)
            {
                onResolved?.Invoke(false);
                yield break;
            }

            yield return null;
        }

        if (path == null)
        {
            onResolved?.Invoke(false);
            yield break;
        }

        move.CancelActiveMovement();
        yield return move.MoveByPath(path, actor.Brain?.bestAction);
        onResolved?.Invoke(
            IsCurrentAction() && actor.GetNowXY() == target);
    }

    private bool IsCurrentAction()
    {
        return actor != null
            && actor.Brain != null
            && actor.Brain.bestAction?.actionset is AISubstanceUse;
    }

    private void ReleaseReservation()
    {
        if (!reservation.IsValid || Items == null || actor == null)
        {
            reservation = default;
            return;
        }

        if (Items is IWorldItemQuantityLeaseRuntime leaseRuntime
            && !string.IsNullOrWhiteSpace(reservation.LeaseId))
        {
            leaseRuntime.ReleaseQuantityLease(
                reservation.LeaseId,
                ItemReservationReleaseReason.Cancelled);
        }
        else
        {
            Items.ReleaseReservation(
                reservation.StackId,
                actor.Identity?.PersistentId ?? string.Empty);
        }
        reservation = default;
    }

    private void FinishCompleted()
    {
        ReleaseReservation();
        useExecutionActive = false;
        routine = null;
        request = default;
        EndAiAction(CharacterAiActionTerminalKind.Completed, clearFailures: true);
    }

    private void FinishFailed(AIActionFailureKind kind, string reason)
    {
        ReleaseReservation();
        useExecutionActive = false;
        routine = null;
        request = default;
        FailAiAction(kind, reason);
    }

    private void FailAiAction(AIActionFailureKind kind, string reason)
    {
        if (actor?.Brain != null)
        {
            actor.Brain.ReportRuntimeActionFailure(
                AIActionFailure.Create(kind, reason),
                requestImmediateReplan: true);
        }

        EndAiAction(CharacterAiActionTerminalKind.Failed, clearFailures: false);
    }

    private void EndAiAction(
        CharacterAiActionTerminalKind terminalKind,
        bool clearFailures)
    {
        if (actor?.Brain == null)
        {
            return;
        }

        actor.Brain.EndExpectedAction(
            actor.Brain.bestAction,
            terminalKind,
            clearFailures);
    }

    private void CacheReferences()
    {
        actor = actor != null ? actor : GetComponent<CharacterActor>();
        move = move != null ? move : GetComponent<AbilityMove>();
    }
}
