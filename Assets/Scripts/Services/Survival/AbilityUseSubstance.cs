using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AbilityUseSubstance : MonoBehaviour
{
    private const int MaximumPathResolveFrames = 240;

    private CharacterActor actor;
    private AbilityMove move;
    private Coroutine routine;
    private WorldItemReservedStackQuantity reservation;
    private CharacterSubstanceUseRequest request;

    public bool IsUsingSubstance => routine != null;
    public CharacterSubstanceUseRequest ActiveRequest => request;

    private ICharacterConsumablesRuntime Consumables =>
        actor?.ConsumablesRuntime;
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
            && Consumables?.TryGetAutomaticUseRequest(actor, out next) == true;
    }

    public void StartUse()
    {
        if (IsUsingSubstance
            || !CanStart(out CharacterSubstanceUseRequest next))
        {
            EndAiAction();
            return;
        }

        request = next;
        routine = StartCoroutine(UseRoutine());
    }

    public void StopUse(string reason)
    {
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
        CharacterCarryInventory inventory =
            CharacterCarryInventory.Ensure(actor);
        if (inventory == null || Items == null || Consumables == null)
        {
            Finish();
            yield break;
        }

        if (inventory.CountItem(request.ItemId) <= 0)
        {
            if (!Items.TryReserveStoredItemForDirectPickup(
                    actor,
                    request.ItemId,
                    1,
                    out reservation,
                    out Vector2Int pickupStand,
                    out string reserveFailure))
            {
                actor.Brain?.SetActionPhase(
                    "복용품 확보 실패",
                    null,
                    reserveFailure);
                Finish();
                yield break;
            }

            actor.Brain?.SetActionPhase(
                $"{request.DisplayName} 가지러 이동",
                null,
                request.Reason);
            bool reached = false;
            yield return MoveToPickup(pickupStand, value => reached = value);
            if (!reached)
            {
                Finish();
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
                Finish();
                yield break;
            }

            reservation = default;
        }

        actor.Brain?.SetActionPhase(
            $"{request.DisplayName} 복용",
            null,
            request.Reason);
        if (!Consumables.TryConsume(
                actor,
                request.SubstanceId,
                request.MedicalContext,
                request.CombatContext,
                out SubstanceUseResult result))
        {
            actor.Brain?.SetActionPhase(
                "복용 중단",
                null,
                result.FailureReason);
        }
        else
        {
            actor.AddLog($"{request.DisplayName}을 복용했다.");
        }

        yield return null;
        Finish();
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
            GridTraversalContext.ForCharacter(actor);
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

        Items.ReleaseReservation(
            reservation.StackId,
            actor.Identity?.PersistentId ?? string.Empty);
        reservation = default;
    }

    private void Finish()
    {
        ReleaseReservation();
        routine = null;
        request = default;
        EndAiAction();
    }

    private void EndAiAction()
    {
        if (actor?.Brain == null)
        {
            return;
        }

        actor.Brain.isBestActionEnd = true;
        actor.Brain.RequestImmediateReplan(clearFailures: true);
    }

    private void CacheReferences()
    {
        actor = actor != null ? actor : GetComponent<CharacterActor>();
        move = move != null ? move : GetComponent<AbilityMove>();
    }
}
