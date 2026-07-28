using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AbilityHaul : MonoBehaviour
{
    private const int MaximumPathResolveFrames = 240;
    private const int MaximumMovementAttempts = 5;
    private static readonly WaitForSeconds MovementRetryDelay =
        new WaitForSeconds(0.15f);

    private CharacterActor actor;
    private AbilityMove move;
    private Coroutine haulingRoutine;
    private WorldItemHaulPlan activePlan;
    private WorldItemHaulPlanUnloadReason unloadReason;
    private bool lastMoveSucceeded;
    private IWorldItemStackRuntime ItemRuntime => actor?.WorldItemStackRuntime;

    public bool IsHauling => haulingRoutine != null;
    public string CurrentPlanSummary => activePlan != null && activePlan.IsValid
        ? activePlan.Summary
        : "운반 계획 없음";
    public string CurrentUnloadReason => ToDisplayText(unloadReason);

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            StopHauling("disabled");
        }
    }

    public static AbilityHaul Ensure(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        AbilityHaul ability = actor.GetComponent<AbilityHaul>();
        if (ability == null && Application.isPlaying)
        {
            ability = actor.gameObject.AddComponent<AbilityHaul>();
        }

        ability?.CacheReferences();
        return ability;
    }

    public bool CanStartHauling(out string failureReason)
    {
        failureReason = string.Empty;
        CacheReferences();
        return actor != null
            && move != null
            && ItemRuntime != null
            && ItemRuntime.HasAvailableHaulJob(actor);
    }

    public void StartHauling()
    {
        CacheReferences();
        IWorldItemStackRuntime itemRuntime = ItemRuntime;
        if (actor == null || move == null || itemRuntime == null)
        {
            EndAiAction();
            return;
        }

        if (IsHauling)
        {
            return;
        }

        if (!itemRuntime.TryReserveBestHaulPlan(
                actor,
                out WorldItemHaulPlan reservedPlan,
                out string reason))
        {
            actor.Brain?.SetActionPhase("운반 대기", null, reason);
            EndAiAction();
            return;
        }

        activePlan = reservedPlan;
        unloadReason = WorldItemHaulPlanUnloadReason.None;
        haulingRoutine = StartCoroutine(HaulRoutine(activePlan));
    }

    public void StopHauling(string reason)
    {
        if (haulingRoutine != null)
        {
            StopCoroutine(haulingRoutine);
            haulingRoutine = null;
        }

        ReleaseActivePlanReservations();
        activePlan = null;
        unloadReason = WorldItemHaulPlanUnloadReason.Interrupted;
    }

    private IEnumerator HaulRoutine(WorldItemHaulPlan plan)
    {
        IWorldItemStackRuntime itemRuntime = ItemRuntime;
        CharacterCarryInventory carry = actor != null
            ? actor.CarryInventory
            : null;
        if (carry == null || itemRuntime == null || plan == null || !plan.IsValid)
        {
            EndAiAction();
            yield break;
        }

        move.CancelActiveMovement();
        if (!TryGetGrid(out Grid grid))
        {
            actor.Brain?.SetActionPhase("운반 실패", null, "그리드 없음");
            EndAiAction();
            yield break;
        }

        AIAction expectedAction = GetExpectedHaulAction();
        int pickedStackCount = 0;
        IReadOnlyList<WorldItemHaulPlanLeg> pickupLegs = plan.PickupLegs;
        for (int pickupIndex = 0; pickupIndex < pickupLegs.Count; pickupIndex++)
        {
            WorldItemHaulPlanLeg pickup = pickupLegs[pickupIndex];
            if (!pickup.IsValid)
            {
                continue;
            }

            actor.Brain?.SetActionPhase(
                "물건 가지러 이동",
                null,
                $"{pickup.ItemPosition} · {pickedStackCount + 1}/{plan.PickupLegs.Count}");
            bool pickupReached = IsActorAt(pickup.PickupStandPosition);
            if (!pickupReached)
            {
                yield return MoveTo(
                    grid,
                    pickup.PickupStandPosition,
                    expectedAction);
                pickupReached = lastMoveSucceeded;
            }

            if (!pickupReached)
            {
                unloadReason = WorldItemHaulPlanUnloadReason.Interrupted;
                break;
            }

            actor.Brain?.SetActionPhase(
                "물건 줍는 중",
                null,
                $"{pickup.Reservation.Quantity}개");
            if (!itemRuntime.TryPickupReservedStackQuantity(
                    actor,
                    carry,
                    pickup.Reservation,
                    out int pickedUp,
                    out string pickupReason))
            {
                actor.Brain?.SetActionPhase("운반 건너뜀", null, pickupReason);
                itemRuntime.ReleaseReservation(
                    pickup.Reservation.StackId,
                    actor.Identity != null ? actor.Identity.PersistentId : string.Empty);
                continue;
            }

            pickedStackCount++;
            yield return null;
            if (pickedUp < pickup.Reservation.Quantity
                || carry.GetLoadRatio(
                    itemRuntime.CatalogProvider,
                    itemRuntime.HaulingSettingsProvider) >= 0.98f)
            {
                unloadReason = WorldItemHaulPlanUnloadReason.LoadLimitReached;
                break;
            }
        }

        ReleaseActivePlanReservations();
        if (!carry.HasItems || pickedStackCount == 0)
        {
            unloadReason = WorldItemHaulPlanUnloadReason.NoPickupCandidate;
            actor.Brain?.SetActionPhase("운반 실패", null, "집을 물건 없음");
            FinishHauling();
            yield break;
        }

        IReadOnlyList<WorldItemHaulPlanLeg> deliveryLegs = plan.DeliveryLegs;
        for (int deliveryIndex = 0; deliveryIndex < deliveryLegs.Count; deliveryIndex++)
        {
            WorldItemHaulPlanLeg delivery = deliveryLegs[deliveryIndex];
            if (!delivery.IsValid)
            {
                continue;
            }

            actor.Brain?.SetActionPhase(
                delivery.DestinationKind == WorldItemHaulDestinationKind.FacilityBuffer
                    ? "목적지로 이동"
                    : "창고로 이동",
                null,
                delivery.DeliveryPosition.ToString());
            bool deliveryReached = IsActorAt(delivery.DeliveryPosition);
            if (!deliveryReached)
            {
                yield return MoveTo(
                    grid,
                    delivery.DeliveryPosition,
                    expectedAction);
                deliveryReached = lastMoveSucceeded;
            }

            if (!deliveryReached)
            {
                unloadReason = WorldItemHaulPlanUnloadReason.JobChanged;
                actor.Brain?.SetActionPhase(
                    "운반 중단",
                    null,
                    "목적지까지 이동할 수 없음");
                break;
            }

            actor.Brain?.SetActionPhase("물품 내려놓는 중", null, delivery.DeliveryPosition.ToString());
            string depositReason;
            bool deposited;
            if (delivery.DestinationKind == WorldItemHaulDestinationKind.FacilityBuffer)
            {
                deposited = itemRuntime.TryDepositCarriedItemsToFacility(
                    actor,
                    carry,
                    delivery.DropPosition,
                    delivery.DestinationId,
                    out depositReason);
            }
            else
            {
                deposited = itemRuntime.TryDepositCarriedItems(
                    actor,
                    carry,
                    delivery.Warehouse,
                    out depositReason);
            }

            if (!deposited)
            {
                actor.AddLog("운반 정리 실패: " + (string.IsNullOrWhiteSpace(depositReason) ? "입고 실패" : depositReason));
            }
            else
            {
                actor.AddLog($"바닥 물건 {pickedStackCount}묶음을 정리했다.");
                unloadReason = WorldItemHaulPlanUnloadReason.Completed;
            }

            break;
        }

        FinishHauling();
    }

    private IEnumerator MoveTo(
        Grid grid,
        Vector2Int target,
        AIAction expectedAction)
    {
        lastMoveSucceeded = false;
        if (grid == null || actor == null)
        {
            yield break;
        }

        if (IsActorAt(target))
        {
            lastMoveSucceeded = true;
            yield break;
        }

        IGridPathSearchBroker broker = actor.PathSearchBroker;
        if (broker == null)
        {
            yield break;
        }

        GridTraversalContext traversalContext =
            GridTraversalContext.ForCharacter(actor, DoorAccessOverrideKind.None);
        for (int movementAttempt = 0;
             movementAttempt < MaximumMovementAttempts;
             movementAttempt++)
        {
            if (IsActorAt(target))
            {
                lastMoveSucceeded = true;
                yield break;
            }

            Queue<GridMoveStep> path = null;
            for (int frame = 0; frame < MaximumPathResolveFrames; frame++)
            {
                if (IsActionCancelled(expectedAction))
                {
                    yield break;
                }

                GridPathRequestStatus status = broker.RequestMovePathTo(
                    grid,
                    actor.GetNowXY(),
                    target,
                    out path,
                    GridPathSearchPriority.Urgent,
                    traversalContext);
                if (status == GridPathRequestStatus.Reachable)
                {
                    break;
                }

                if (status == GridPathRequestStatus.Unreachable)
                {
                    actor.Brain?.SetActionPhase(
                        "운반 경로 없음",
                        null,
                        target.ToString());
                    yield break;
                }

                if (frame == 0)
                {
                    actor.Brain?.SetActionPhase(
                        "운반 경로 계산 중",
                        null,
                        target.ToString());
                }

                yield return null;
            }

            if (path == null)
            {
                actor.Brain?.SetActionPhase(
                    "운반 경로 지연",
                    null,
                    target.ToString());
                yield break;
            }

            yield return move.MoveByPath(path, expectedAction);
            if (IsActorAt(target)
                && !IsActionCancelled(expectedAction))
            {
                lastMoveSucceeded = true;
                yield break;
            }

            if (IsActionCancelled(expectedAction))
            {
                yield break;
            }

            actor.Brain?.SetActionPhase(
                "운반 경로 다시 계산",
                null,
                $"{movementAttempt + 1}/{MaximumMovementAttempts}");
            yield return MovementRetryDelay;
        }
    }

    private AIAction GetExpectedHaulAction()
    {
        AIAction current = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;
        return current?.actionset is AIHaul ? current : null;
    }

    private bool TryGetGrid(out Grid grid)
    {
        grid = null;
        return actor?.WorldRegistry != null
            && actor.WorldRegistry.TryGetGrid(out grid);
    }

    private bool IsActionCancelled(AIAction expectedAction)
    {
        return expectedAction != null
            && (actor == null || actor.Brain == null || actor.Brain.bestAction != expectedAction);
    }

    private bool IsActorAt(Vector2Int gridPosition)
    {
        return actor != null && actor.GetNowXY() == gridPosition;
    }

    private void FinishHauling()
    {
        activePlan = null;
        haulingRoutine = null;
        EndAiAction();
    }

    private void ReleaseActivePlanReservations()
    {
        IWorldItemStackRuntime itemRuntime = ItemRuntime;
        if (activePlan == null || actor == null || itemRuntime == null)
        {
            return;
        }

        string actorId = actor.Identity != null ? actor.Identity.PersistentId : string.Empty;
        IReadOnlyList<WorldItemReservedStackQuantity> reservations =
            activePlan.ReservedStackQuantities;
        for (int index = 0; index < reservations.Count; index++)
        {
            WorldItemReservedStackQuantity reservation = reservations[index];
            itemRuntime.ReleaseReservation(reservation.StackId, actorId);
        }
    }

    private void EndAiAction()
    {
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
            actor.Brain.RequestImmediateReplan(clearFailures: true);
        }
    }

    private void CacheReferences()
    {
        actor = actor != null ? actor : GetComponent<CharacterActor>();
        move = move != null ? move : GetComponent<AbilityMove>();
    }

    private static string ToDisplayText(WorldItemHaulPlanUnloadReason reason)
    {
        return reason switch
        {
            WorldItemHaulPlanUnloadReason.LoadLimitReached => "적재 한도 도달",
            WorldItemHaulPlanUnloadReason.NoPickupCandidate => "집을 후보 없음",
            WorldItemHaulPlanUnloadReason.JobChanged => "경로 또는 목적지 변경",
            WorldItemHaulPlanUnloadReason.Idle => "대기 전 적재물 정리",
            WorldItemHaulPlanUnloadReason.Interrupted => "운반 중단",
            WorldItemHaulPlanUnloadReason.Completed => "배송 완료",
            _ => "진행 중"
        };
    }
}
