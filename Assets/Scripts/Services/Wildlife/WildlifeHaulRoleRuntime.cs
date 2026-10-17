using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

public sealed class WildlifeHaulRoleRuntime :
    ITickable,
    IWildlifeHaulLifecycleSink
{
    private const float CareThreshold = 0.45f;

    private readonly IWildlifeHaulRoleStateCommand roles;
    private readonly IWildlifeHaulItemRuntime items;
    private readonly WildlifeWorldServices world;
    private readonly WildlifeExecutionServices execution;
    private readonly List<WildlifeHaulAssignmentSnapshot> assignments = new();

    public WildlifeHaulRoleRuntime(
        IWildlifeHaulRoleStateCommand roles,
        IWildlifeHaulItemRuntime items,
        WildlifeWorldServices world,
        WildlifeExecutionServices execution)
    {
        this.roles = roles ?? throw new ArgumentNullException(nameof(roles));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.execution = execution
            ?? throw new ArgumentNullException(nameof(execution));
    }

    public void Tick()
    {
        if (execution.Clock.IsPaused || execution.Clock.DeltaTime <= 0f)
        {
            return;
        }

        roles.CopyHaulAssignments(assignments);
        foreach (WildlifeHaulAssignmentSnapshot assignment in assignments)
        {
            TickAnimal(assignment, execution.Clock.Time);
        }
    }

    public void OnWildlifeUnavailable(
        WildlifeActor actor,
        WorldItemCarryInterruptionKind interruptionKind)
    {
        if (actor == null
            || !roles.TryGetHaul(
                actor.WildlifeId,
                out WildlifeHaulAssignmentSnapshot assignment))
        {
            return;
        }
        if (assignment.Phase == CapturedWildlifeHaulPhase.Reserved)
        {
            items.ReleaseUnpicked(assignment);
            ClearOrRetire(
                assignment.WildlifeId,
                interruptionKind,
                "독립 운반 중단 · 미인수 예약 해제");
            actor.SetManagedCargoLoad(1f);
            return;
        }
        if (assignment.Phase is CapturedWildlifeHaulPhase.CargoOwned
                or CapturedWildlifeHaulPhase.ReleasePending)
        {
            if (!items.TryCommitRecoveryPending(
                    assignment,
                    actor.GridPosition,
                    interruptionKind,
                    out string recoveryFailure))
            {
                throw new InvalidOperationException(
                    $"Wildlife haul cargo recovery handoff failed for '{assignment.WildlifeId}': {recoveryFailure}");
            }
            ClearOrRetire(
                assignment.WildlifeId,
                interruptionKind,
                "독립 운반 중단 · 화물 회수 대기");
            actor.SetManagedCargoLoad(1f);
            return;
        }
        ClearOrRetire(
            assignment.WildlifeId,
            interruptionKind,
            "독립 운반 중단");
    }

    private void ClearOrRetire(
        string wildlifeId,
        WorldItemCarryInterruptionKind interruptionKind,
        string status)
    {
        if (interruptionKind == WorldItemCarryInterruptionKind.Dead)
        {
            roles.RetireUnavailableHaul(wildlifeId);
            return;
        }
        roles.ClearRecoveredHaul(wildlifeId, status);
    }

    private void TickAnimal(WildlifeHaulAssignmentSnapshot assignment, float now)
    {
        WildlifeActor actor = world.WorldRegistry.Wildlife.FirstOrDefault(value =>
            value != null
            && string.Equals(value.WildlifeId, assignment.WildlifeId,
                StringComparison.Ordinal));
        if (actor == null || !actor.IsAlive)
        {
            return;
        }
        if (actor.State != WildlifeState.Captured || !actor.gameObject.activeInHierarchy)
        {
            OnWildlifeUnavailable(actor, WorldItemCarryInterruptionKind.Disabled);
            return;
        }
        if (!world.Species.TryGetSpecies(
                actor.SpeciesId,
                out WildlifeSpeciesDefinition species)
            || species.HaulRoleProfile == null)
        {
            throw new InvalidOperationException(
                $"Assigned haul species '{actor.SpeciesId}' has no haul profile.");
        }

        WildlifeHaulRoleProfile profile = species.HaulRoleProfile;
        bool carrying = assignment.Phase is CapturedWildlifeHaulPhase.CargoOwned
            or CapturedWildlifeHaulPhase.ReleasePending;
        actor.SetManagedCargoLoad(
            carrying ? profile.LoadedMoveSpeedMultiplier : 1f);
        if (carrying
            && !items.TryMoveCargo(assignment, actor.GridPosition, out string cargoError))
        {
            actor.SetIntent(WildlifeIntent.Rest, "독립 운반 화물 차단 · " + cargoError);
            return;
        }

        if (!carrying
            && (actor.Hunger >= CareThreshold || actor.Thirst >= CareThreshold))
        {
            if (assignment.Phase == CapturedWildlifeHaulPhase.Reserved)
            {
                items.ReleaseUnpicked(assignment);
                ReplaceIdle(assignment, now, profile, "돌봄 우선 · 예약 해제");
            }
            RecordMovement(
                actor,
                actor.RequestManagedCaptivePath(assignment.PenPosition, now),
                "돌봄을 위해 우리로 복귀");
            return;
        }

        switch (assignment.Phase)
        {
            case CapturedWildlifeHaulPhase.Idle:
                TickIdle(actor, assignment, profile, now);
                break;
            case CapturedWildlifeHaulPhase.Reserved:
                TickReserved(actor, assignment, profile, now);
                break;
            case CapturedWildlifeHaulPhase.CargoOwned:
            case CapturedWildlifeHaulPhase.ReleasePending:
                TickCargo(actor, assignment, profile, now);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown wildlife haul phase '{assignment.Phase}'.");
        }
    }

    private void TickIdle(
        WildlifeActor actor,
        WildlifeHaulAssignmentSnapshot assignment,
        WildlifeHaulRoleProfile profile,
        float now)
    {
        if (now + 0.0001f < assignment.NextDecisionAt)
        {
            return;
        }
        WildlifeHaulPlanStatus status = items.TryReserveNext(
            assignment.WildlifeId,
            actor.GridPosition,
            profile.MaxCargoMassGrams,
            out WildlifeHaulAssignmentSnapshot reserved,
            out string reason);
        if (status == WildlifeHaulPlanStatus.Reserved)
        {
            reserved = WithPen(reserved, assignment.PenPosition);
            if (!roles.TryReplaceHaul(
                    assignment.WildlifeId,
                    CapturedWildlifeHaulPhase.Idle,
                    string.Empty,
                    reserved,
                    "독립 운반 픽업 예약"))
            {
                items.ReleaseUnpicked(reserved);
            }
            return;
        }
        if (status == WildlifeHaulPlanStatus.Pending)
        {
            actor.SetIntent(WildlifeIntent.Rest, reason);
            ReplaceIdle(assignment, now, profile, reason);
            return;
        }
        ReplaceIdle(assignment, now, profile, reason);
    }

    private void TickReserved(
        WildlifeActor actor,
        WildlifeHaulAssignmentSnapshot assignment,
        WildlifeHaulRoleProfile profile,
        float now)
    {
        if (!items.RenewUnpicked(assignment))
        {
            items.ReleaseUnpicked(assignment);
            ReplaceIdle(assignment, now, profile, "픽업 예약 소실 · 재탐색");
            return;
        }
        if (actor.GridPosition != assignment.PickupStandPosition)
        {
            GridPathRequestStatus move = actor.RequestManagedCaptivePath(
                assignment.PickupStandPosition,
                now);
            if (move == GridPathRequestStatus.Unreachable)
            {
                items.ReleaseUnpicked(assignment);
                ReplaceIdle(assignment, now, profile, "픽업 경로 없음 · 재탐색");
            }
            else
            {
                RecordMovement(actor, move, "독립 운반 픽업 이동");
            }
            return;
        }

        if (!items.TryPickup(
                actor,
                assignment,
                out WildlifeHaulAssignmentSnapshot cargo,
                out string failureReason))
        {
            items.ReleaseUnpicked(assignment);
            ReplaceIdle(assignment, now, profile, failureReason);
            return;
        }
        cargo = WithPen(cargo, assignment.PenPosition);
        if (!roles.TryReplaceHaul(
                assignment.WildlifeId,
                CapturedWildlifeHaulPhase.Reserved,
                assignment.OperationId,
                cargo,
                "독립 운반 화물 인수"))
        {
            if (items.TryCommitRecoveryPending(
                    cargo,
                    actor.GridPosition,
                    WorldItemCarryInterruptionKind.Disabled,
                    out _))
            {
                roles.ClearRecoveredHaul(
                    assignment.WildlifeId,
                    "역할 변경 충돌 · 화물 회수 대기");
            }
        }
    }

    private void TickCargo(
        WildlifeActor actor,
        WildlifeHaulAssignmentSnapshot assignment,
        WildlifeHaulRoleProfile profile,
        float now)
    {
        if (actor.GridPosition != assignment.DeliveryPosition)
        {
            RecordMovement(
                actor,
                actor.RequestManagedCaptivePath(assignment.DeliveryPosition, now),
                "독립 운반 배송 이동");
            return;
        }
        if (!items.TryDeliver(assignment, out string failureReason))
        {
            actor.SetIntent(WildlifeIntent.Rest, "배송 차단 · " + failureReason);
            return;
        }
        actor.SetManagedCargoLoad(1f);
        if (assignment.Phase == CapturedWildlifeHaulPhase.ReleasePending)
        {
            roles.ClearRecoveredHaul(assignment.WildlifeId, "독립 운반 역할 해제 완료");
            return;
        }
        ReplaceIdle(assignment, now, profile, "독립 운반 배송 완료");
    }

    private void ReplaceIdle(
        WildlifeHaulAssignmentSnapshot current,
        float now,
        WildlifeHaulRoleProfile profile,
        string status)
    {
        WildlifeHaulAssignmentSnapshot idle = new(
            current.WildlifeId,
            CapturedWildlifeHaulPhase.Idle,
            now + profile.DecisionIntervalSeconds,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            default,
            WorldItemHaulDestinationKind.Warehouse,
            string.Empty,
            default,
            default,
            current.PenPosition);
        roles.TryReplaceHaul(
            current.WildlifeId,
            current.Phase,
            current.OperationId,
            idle,
            status);
    }

    private static WildlifeHaulAssignmentSnapshot WithPen(
        WildlifeHaulAssignmentSnapshot value,
        Vector2Int pen) => new(
            value.WildlifeId,
            value.Phase,
            value.NextDecisionAt,
            value.OperationId,
            value.LeaseId,
            value.SourceStackId,
            value.CargoStackId,
            value.ItemId,
            value.ExpectedStackSignature,
            value.Quantity,
            value.PickupStandPosition,
            value.DestinationKind,
            value.DestinationId,
            value.DeliveryPosition,
            value.DropPosition,
            pen);

    private static void RecordMovement(
        WildlifeActor actor,
        GridPathRequestStatus status,
        string purpose)
    {
        actor.SetIntent(
            status == GridPathRequestStatus.Unreachable
                ? WildlifeIntent.Rest
                : WildlifeIntent.Wander,
            status switch
            {
                GridPathRequestStatus.Pending => purpose + " · 경로 계산 대기",
                GridPathRequestStatus.Reachable => purpose,
                _ => purpose + " · 경로 없음"
            });
    }
}
