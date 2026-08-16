using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class WorkCommandHandler
{
    private readonly AbilityWork work;
    private readonly WorkTargetSelector targetSelector;
    private readonly IDefenseEngagementRuntime defenseEngagementRuntime;
    private BuildableObject priorityWorkTarget;
    private CharacterActor prioritySuppressTarget;
    private Grid prioritySuppressGrid;
    private WorkTypeId priorityWorkTypeId;
    private BuildableObject pendingEnvironmentRiskTarget;
    private WorkTypeId pendingEnvironmentRiskWorkTypeId;

    public WorkCommandHandler(
        AbilityWork work,
        WorkTargetSelector targetSelector,
        IDefenseEngagementRuntime defenseEngagementRuntime)
    {
        this.work = work;
        this.targetSelector = targetSelector;
        this.defenseEngagementRuntime = defenseEngagementRuntime;
    }

    public BuildableObject PriorityWorkTarget => priorityWorkTarget != null ? priorityWorkTarget : null;
    public CharacterActor PrioritySuppressActor => prioritySuppressTarget != null ? prioritySuppressTarget : null;
    public bool HasPrioritySuppressTarget => prioritySuppressTarget != null && !prioritySuppressTarget.IsDead;
    internal FacilityWorkType PriorityWorkType => priorityWorkTypeId.IsValid
        ? FacilityWorkTypeMap.GetRequired(priorityWorkTypeId)
        : FacilityWorkType.None;
    public WorkTypeId PriorityWorkTypeId => priorityWorkTypeId;

    public bool TrySetPriorityWorkTarget(BuildableObject building, out string errorMessage)
    {
        return TrySetPriorityWorkTargetById(
            building,
            default,
            null,
            out errorMessage);
    }

    public bool TrySetPriorityWorkTarget(
        BuildableObject building,
        WorkTypeId preferredWorkTypeId,
        GridPathSearchResult searchResult,
        out string errorMessage)
    {
        return TrySetPriorityWorkTargetById(
            building,
            preferredWorkTypeId,
            searchResult,
            out errorMessage);
    }

    private bool TrySetPriorityWorkTargetById(
        BuildableObject building,
        WorkTypeId preferredWorkTypeId,
        GridPathSearchResult searchResult,
        out string errorMessage)
    {
        if (building == null)
        {
            errorMessage = "우선 지정할 시설이 없습니다";
            return false;
        }

        if (preferredWorkTypeId.IsValid
            && !WorkTypeCatalog.TryGet(
                preferredWorkTypeId,
                out WorkTypeDefinition _))
        {
            errorMessage =
                $"No work type definition is registered for '{preferredWorkTypeId}'.";
            return false;
        }

        bool forced = preferredWorkTypeId.IsValid;
        FacilityWorkType preferredWorkType = forced
            ? FacilityWorkTypeMap.GetRequired(preferredWorkTypeId)
            : FacilityWorkType.None;
        if (forced
            && targetSelector.RequiresForcedEnvironmentConfirmation(
                building,
                preferredWorkTypeId,
                searchResult,
                out string environmentWarning))
        {
            bool confirmed = pendingEnvironmentRiskTarget == building
                && pendingEnvironmentRiskWorkTypeId == preferredWorkTypeId;
            if (!confirmed)
            {
                pendingEnvironmentRiskTarget = building;
                pendingEnvironmentRiskWorkTypeId = preferredWorkTypeId;
                errorMessage = environmentWarning;
                return false;
            }
        }

        pendingEnvironmentRiskTarget = null;
        pendingEnvironmentRiskWorkTypeId = default;
        if (!targetSelector.TryEvaluateWorkTarget(building, searchResult, preferredWorkType, forced, out WorkTargetCandidate candidate))
        {
            errorMessage = candidate.FailureReason;
            return false;
        }

        priorityWorkTarget = building;
        prioritySuppressTarget = null;
        prioritySuppressGrid = null;
        priorityWorkTypeId = candidate.WorkTypeId;
        // An accepted priority-work target is a direct player command, just
        // like a priority suppress target. Leaving a worker OffDuty makes a
        // non-emergency command permanently ineligible: AIWork fails its duty
        // gate and an unrelated wait action wins every subsequent decision.
        // The command boundary owns this transition so callers do not need a
        // second hidden SetDutyState call after a successful command.
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.AssignWork(building, candidate.WorkTypeId);
        work.WorkerActor?.Brain?.RequestImmediateReplan(clearFailures: true);
        work.WorkerActor?.AddActivity(CharacterActivityEvent.Work(
            candidate.WorkTypeId,
            CharacterActivityOutcomes.Changed,
            $"우선 작업 지정: {building.name} - {candidate.DisplayName}",
            building,
            reasonCode: "priority-command"));
        errorMessage = string.Empty;
        return true;
    }

    public bool TrySetPrioritySuppressTarget(
        CharacterActor target,
        GridPathSearchResult searchResult,
        out string errorMessage)
    {
        if (!WorkCommandResolver.TryResolveSuppressCommand(
                work.WorkerActor,
                target,
                work.StaffDiscontentRuntimeService.IsRebellionTarget,
                out errorMessage))
        {
            return false;
        }

        if (!CanReachSuppressTarget(target, searchResult, out errorMessage))
        {
            return false;
        }

        prioritySuppressTarget = target;
        prioritySuppressGrid = work.WorkGridResolver.ResolveActiveGrid(work, searchResult);
        priorityWorkTarget = null;
        priorityWorkTypeId = BuiltInWorkTypeIds.Guard;
        work.AssignWork(null, BuiltInWorkTypeIds.Guard);
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.WorkerActor?.Brain?.RequestImmediateReplan(clearFailures: true);
        work.WorkerActor?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Command,
            CharacterActivityOutcomes.Changed,
            $"우선 제압 지정: {target.name}",
            actionId: "command:suppress",
            targetId: CharacterPersistentIdentity.Require(target).Value,
            targetName: target.name));
        errorMessage = string.Empty;
        return true;
    }

    public bool TryGetPrioritySuppressDestination(GridPathSearchResult searchResult, out BuildableObject destination)
    {
        destination = null;
        if (!HasPrioritySuppressTarget)
        {
            return false;
        }

        Grid activeGrid = work.WorkGridResolver.ResolveActiveGrid(work, searchResult, prioritySuppressGrid);
        if (activeGrid == null)
        {
            return false;
        }

        GridCell cell = activeGrid.GetGridCell(work.WorkGridResolver.GetGridPosition(activeGrid, prioritySuppressTarget));
        if (cell == null)
        {
            return false;
        }

        destination = cell.GetAllBuilding()
            .Where((building) => building != null && !building.isDestroy)
            .OrderByDescending((building) => building.IsGridMovement)
            .FirstOrDefault();
        return destination != null;
    }

    public void ClearPriorityWorkTarget()
    {
        priorityWorkTarget = null;
        prioritySuppressTarget = null;
        prioritySuppressGrid = null;
        priorityWorkTypeId = default;
        pendingEnvironmentRiskTarget = null;
        pendingEnvironmentRiskWorkTypeId = default;
    }

    public bool HasUrgentPriorityTarget()
    {
        if (priorityWorkTarget == null)
        {
            return false;
        }

        bool forced = priorityWorkTypeId.IsValid;
        FacilityWorkType priorityWorkType = forced
            ? FacilityWorkTypeMap.GetRequired(priorityWorkTypeId)
            : FacilityWorkType.None;
        return targetSelector.TryEvaluateWorkTarget(priorityWorkTarget, null, priorityWorkType, forced, out WorkTargetCandidate candidate)
            && candidate.UrgencyScore >= 60f;
    }

    public IEnumerator SuppressPriorityTarget(Func<bool> ownsAction = null)
    {
        CharacterActor actor = work.WorkerActor;
        if (actor == null || actor.Brain == null)
        {
            yield break;
        }

        work.EnsureWorkReferences();
        AbilityMove move = work.WorkerMove;
        CharacterActor target = prioritySuppressTarget;
        AIAction currentAction = actor.Brain.bestAction;
        bool OwnsAction() => ownsAction?.Invoke() ?? ReferenceEquals(
            actor?.Brain?.bestAction,
            currentAction);
        if (!WorkCommandResolver.TryResolveSuppressCommand(
                actor,
                target,
                work.StaffDiscontentRuntimeService.IsRebellionTarget,
                out string errorMessage))
        {
            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Failed,
                $"제압 실패: {errorMessage}",
                actionId: "combat:suppress",
                targetId: target != null
                    ? CharacterPersistentIdentity.Require(target).Value
                    : string.Empty,
                targetName: target != null ? target.name : string.Empty,
                reasonCode: errorMessage,
                sentiment: -0.7f,
                bubbleEligible: true));
            if (OwnsAction())
            {
                ClearPriorityWorkTarget();
                actor.Brain.isBestActionEnd = true;
            }
            yield break;
        }

        Grid activeGrid = work.WorkGridResolver.ResolveActiveGrid(work, null, prioritySuppressGrid);
        if (move == null || activeGrid == null)
        {
            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Failed,
                "제압 실패: 이동 정보 없음",
                actionId: "combat:suppress",
                reasonCode: "missing-movement",
                sentiment: -0.7f,
                bubbleEligible: true));
            if (OwnsAction())
            {
                ClearPriorityWorkTarget();
                actor.Brain.isBestActionEnd = true;
            }
            yield break;
        }

        if (target.TryGetComponent(out InvasionIntruderRuntime invasionIntruder))
        {
            IDefenseEngagementRuntime defenseRuntime = defenseEngagementRuntime;
            string failureReason = "방어 지휘 체계 없음";
            if (defenseRuntime == null
                || !defenseRuntime.TryAssignManual(actor, invasionIntruder, out failureReason))
            {
                actor.AddActivity(CharacterActivityEvent.Create(
                    CharacterActivityKinds.Combat,
                    CharacterActivityOutcomes.Failed,
                    $"저지 실패: {failureReason ?? "방어 지휘 체계 없음"}",
                    actionId: "combat:suppress",
                    targetId: target.Identity?.PersistentId ?? string.Empty,
                    targetName: target.Identity?.DisplayName ?? target.name,
                    reasonCode: failureReason,
                    sentiment: -0.7f,
                    bubbleEligible: true));
                if (OwnsAction())
                {
                    ClearPriorityWorkTarget();
                    actor.Brain.isBestActionEnd = true;
                }
                yield break;
            }

            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Started,
                $"{target.Identity?.DisplayName ?? target.name} 저지하러 이동",
                actionId: "defense:manual-intercept",
                targetId: target.Identity?.PersistentId ?? string.Empty,
                targetName: target.Identity?.DisplayName ?? target.name));
            while (target != null
                && !target.IsDead
                && actor != null
                && !actor.IsDead
                && OwnsAction()
                && defenseRuntime.TryGetEngagement(invasionIntruder, out _))
            {
                yield return null;
            }

            if (OwnsAction())
            {
                ClearPriorityWorkTarget();
                actor.Brain.isBestActionEnd = true;
            }
            yield break;
        }

        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Started,
            $"제압 시작: {target.name}",
            actionId: "combat:suppress",
            targetId: CharacterPersistentIdentity.Require(target).Value,
            targetName: target.name));
        while (target != null
            && !target.IsDead
            && actor != null
            && !actor.IsDead
            && OwnsAction())
        {
            Vector2Int targetPos = work.WorkGridResolver.GetGridPosition(activeGrid, target);
            Vector2Int actorPos = work.WorkGridResolver.GetGridPosition(activeGrid, actor);
            if (actorPos != targetPos)
            {
                Queue<GridMoveStep> path = activeGrid.GetMovePathTo(actorPos, targetPos);
                if (path == null || path.Count == 0)
                {
                    actor.AddActivity(CharacterActivityEvent.Create(
                        CharacterActivityKinds.Combat,
                        CharacterActivityOutcomes.Blocked,
                        "제압 실패: 도달할 수 없는 대상",
                        actionId: "combat:suppress",
                        targetId: CharacterPersistentIdentity.Require(target).Value,
                        targetName: target.name,
                        reasonCode: "target-unreachable",
                        sentiment: -0.7f,
                        bubbleEligible: true));
                    break;
                }

                yield return move.MoveByPath(path, currentAction);
                if (!OwnsAction())
                {
                    yield break;
                }

                continue;
            }

            float damage = Mathf.Max(1f, work.SuppressBaseDamage * actor.GetCombatPowerMultiplier());
            ICharacterDeprivationQuery deprivationQuery = target.DeprivationQuery;
            ICharacterDeprivationCommand deprivationCommands =
                target.DeprivationCommands;
            bool breakdownTarget =
                deprivationQuery?.IsSuppressible(target) ?? false;
            bool suppressionEnded = false;
            if (breakdownTarget)
            {
                deprivationCommands.ApplySuppression(
                    target,
                    damage,
                    out suppressionEnded);
            }
            else
            {
                target.ApplyDamage(damage, $"제압: {actor.name}");
            }
            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                target.IsDead || suppressionEnded ? CharacterActivityOutcomes.Completed : CharacterActivityOutcomes.Progress,
                target.IsDead || suppressionEnded
                    ? $"제압 완료: {target.name}"
                    : $"저항을 누르는 중: {target.name}",
                actionId: "combat:suppress",
                targetId: CharacterPersistentIdentity.Require(target).Value,
                targetName: target.name,
                value: damage,
                sentiment: target.IsDead || suppressionEnded ? 0.2f : -0.3f));

            if (suppressionEnded)
            {
                break;
            }

            if (target.IsDead)
            {
                if (target.TryGetComponent(out InvasionIntruderRuntime intruderRuntime))
                {
                    intruderRuntime.ResolveSuppressedBy(actor);
                }
                else
                {
                    work.StaffDiscontentRuntimeService.ResolveSuppressedRebel(target, actor);
                }
                break;
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, work.SuppressAttackInterval));
            if (!OwnsAction())
            {
                yield break;
            }
        }

        if (OwnsAction())
        {
            ClearPriorityWorkTarget();
            actor.Brain.isBestActionEnd = true;
        }
    }

    private bool CanReachSuppressTarget(
        CharacterActor target,
        GridPathSearchResult searchResult,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (target == null)
        {
            errorMessage = "제압할 대상이 없습니다";
            return false;
        }

        Grid activeGrid = work.WorkGridResolver.ResolveActiveGrid(work, searchResult);
        if (activeGrid == null)
        {
            errorMessage = "그리드가 초기화되지 않았습니다";
            return false;
        }

        Vector2Int targetPos = work.WorkGridResolver.GetGridPosition(activeGrid, target);
        if (!activeGrid.IsValidGridPos(targetPos))
        {
            errorMessage = "제압 대상 위치가 유효하지 않습니다";
            return false;
        }

        CharacterActor actor = work.WorkerActor;
        if (actor != null && work.WorkGridResolver.GetGridPosition(activeGrid, actor) == targetPos)
        {
            return true;
        }

        Vector2Int actorPos = actor != null ? work.WorkGridResolver.GetGridPosition(activeGrid, actor) : Vector2Int.zero;
        Queue<GridMoveStep> path = searchResult != null
            ? searchResult.GetMovePathTo(targetPos)
            : activeGrid.GetMovePathTo(actorPos, targetPos);
        if (path == null || path.Count == 0)
        {
            errorMessage = "도달할 수 없는 대상입니다";
            return false;
        }

        return true;
    }
}
