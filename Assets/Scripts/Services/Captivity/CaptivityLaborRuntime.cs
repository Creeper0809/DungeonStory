using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CaptivityLaborRuntime
{
    private readonly CaptivityActorAccess actors;
    private readonly CaptivityActorRuntimeLookup actorRuntime;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly ICaptivityCareLaborInputOwnerRuntime careLaborInputOwner;
    private readonly IGameClock gameClock;
    private readonly CaptivityDurableToolRuntime durableTools;

    public CaptivityLaborRuntime(
        CaptivityActorAccess actors,
        CaptivityActorRuntimeLookup actorRuntime,
        ICharacterAiWorldRegistry worldRegistry,
        ICharacterBodyHealthQuery bodyHealthQuery,
        IWorldItemStackRuntime itemRuntime,
        IPhysicalItemBatchDispositionService batchDispositions,
        ICaptivityCareLaborInputOwnerRuntime careLaborInputOwner,
        IGameClock gameClock,
        CaptivityDurableToolRuntime durableTools)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.actorRuntime = actorRuntime ?? throw new ArgumentNullException(nameof(actorRuntime));
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.bodyHealthQuery = bodyHealthQuery ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        this.itemRuntime = itemRuntime ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.batchDispositions = batchDispositions ?? throw new ArgumentNullException(nameof(batchDispositions));
        this.careLaborInputOwner = careLaborInputOwner ?? throw new ArgumentNullException(nameof(careLaborInputOwner));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.durableTools = durableTools ?? throw new ArgumentNullException(nameof(durableTools));
    }

    public void Tick(CaptiveState state, CharacterActor actor)
    {
        if (!string.IsNullOrWhiteSpace(state?.assignedLaborToolItemId)
            && state.assignedLaborToolDurability <= 0f)
        {
            if (!TryReturnLaborTool(state))
            {
                state.lastResult = "파손된 포로 작업 도구 반납 대기";
                return;
            }
            state.lastResult = "포로 작업 도구 파손으로 노역 중단";
        }
        TickLaborToolPreparation(state, actor);
        TickLaborToolWear(state, actor);
    }

    public void RetryDeadCaptiveTools()
    {
        foreach (CaptiveState state in actors.States)
        {
            if (state?.status == CaptivityStatus.Dead
                && (!string.IsNullOrWhiteSpace(state.assignedLaborToolItemId)
                    || !string.IsNullOrWhiteSpace(state.assignedRestraintItemId)
                    || state.pendingLaborPermissions != CaptiveLaborPermission.None
                    || !string.IsNullOrWhiteSpace(state.laborToolDestinationId)))
            {
                if (!ReturnAssignedCaptivityTools(state))
                    state.lastResult = "수용 중 사망: 포로 도구 반납 대기";
                else if (state.lastResult == "수용 중 사망: 포로 도구 반납 대기")
                    state.lastResult = "수용 중 사망";
            }
        }
    }

    private void TickLaborToolPreparation(
        CaptiveState state,
        CharacterActor actor)
    {
        if (state == null
            || actor == null
            || state.pendingLaborPermissions == CaptiveLaborPermission.None
            || string.IsNullOrWhiteSpace(state.laborToolDestinationId))
        {
            return;
        }

        if (!TryGetHousing(state.captiveId, out BuildableObject housing)
            || housing.BuildingData?.GetCaptiveHousingAbility()
                is not { IsValid: true })
        {
            if (!CancelLaborToolPreparation(state))
            {
                state.lastResult =
                    "소실된 감방의 포로 작업 도구 소유권을 정리할 수 없습니다.";
            }
            return;
        }

        if (CaptivityLaborToolAssignmentOutbox.RequiresFinalization(state))
        {
            if (CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                    state,
                    batchDispositions,
                    out _))
            {
                TryCompleteLaborToolAssignment(state, out _);
            }
            return;
        }

        WorldItemStackSnapshot delivered = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.ItemId,
                    CaptivityItemDefinitions.PrisonerWorkKitItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    state.laborToolDestinationId,
                    StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        CaptiveState assignmentProbe = state.Clone();
        if (delivered == null
            || !((ItemInstanceId)delivered.ItemInstanceId).IsValid
            || DurableToolItemRules.ReadCurrentDurability(
                delivered.ItemId,
                delivered.Components) <= 0f
            || !CaptivityDurableToolRuntime.TryAssignLaborTool(
                assignmentProbe,
                delivered))
        {
            return;
        }

        string operationId = CaptivityLaborToolAssignmentOutbox.FormatOperationId(
            state.captiveId,
            delivered.ItemInstanceId);
        state.assignedLaborToolItemId = assignmentProbe.assignedLaborToolItemId;
        state.assignedLaborToolInstanceId = assignmentProbe.assignedLaborToolInstanceId;
        state.assignedLaborToolDurability = assignmentProbe.assignedLaborToolDurability;
        state.assignedLaborToolMaximumDurability =
            assignmentProbe.assignedLaborToolMaximumDurability;
        state.laborToolAssignmentOperationId = operationId;
        state.laborToolAssignmentSourceStackId = delivered.StackId;
        state.laborToolAssignmentCommitId = string.Empty;
        state.laborToolAssignmentCompleted = false;
        if (!batchDispositions.TryCommitPending(
                new[]
                {
                    new PhysicalItemTransformInput(delivered.StackId, 1)
                },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                CaptivityLaborToolAssignmentOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt disposition,
                out _))
        {
            CaptivityLaborToolAssignmentOutbox.ClearAssignment(state);
            return;
        }

        state.laborToolAssignmentCommitId = disposition.CommitId;
        if (!CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                state,
                batchDispositions,
                out string finalizeFailure))
        {
            throw new InvalidOperationException(
                $"Validated captive labor tool '{delivered.ItemInstanceId}' could not be finalized after physical transfer: {finalizeFailure}");
        }

        if (!TryCompleteLaborToolAssignment(
                state,
                out string closeFailure))
        {
            state.lastResult =
                "포로 작업 도구 배송 종결 대기: " + closeFailure;
        }
    }

    private bool TryCompleteLaborToolAssignment(
        CaptiveState state,
        out string failureReason)
    {
        CaptiveLaborPermission permissions = state.pendingLaborPermissions;
        string destinationId = state.laborToolDestinationId;
        state.pendingLaborPermissions = CaptiveLaborPermission.None;
        state.laborToolDestinationId = string.Empty;
        if (!careLaborInputOwner.TryReconcileLive(
                actors.States,
                out failureReason))
        {
            state.pendingLaborPermissions = permissions;
            state.laborToolDestinationId = destinationId;
            return false;
        }
        state.nextLaborToolWearAt = gameClock.Time + 60f;
        ApplyLaborPermissions(state, permissions);
        return true;
    }

    private void TickLaborToolWear(
        CaptiveState state,
        CharacterActor actor)
    {
        if (state?.status != CaptivityStatus.Labor
            || string.IsNullOrWhiteSpace(state.assignedLaborToolItemId)
            || !state.laborToolAssignmentCompleted
            || gameClock.Time + 0.001f < state.nextLaborToolWearAt
            || actor?.GetAbility<AbilityWork>()?.isWorking != true)
        {
            return;
        }

        state.nextLaborToolWearAt = gameClock.Time + 60f;
        state.assignedLaborToolDurability = Mathf.Max(
            0f,
            state.assignedLaborToolDurability - 1f);
        if (state.assignedLaborToolDurability > 0f)
        {
            return;
        }

        ApplyLaborPermissions(state, CaptiveLaborPermission.None);
        bool returned = TryReturnLaborTool(state);
        state.lastResult = returned
            ? "포로 작업 도구 파손으로 노역 중단"
            : "포로 작업 도구 파손으로 노역 중단: 도구 반납 대기";
    }

    private void ApplyLaborPermissions(
        CaptiveState state,
        CaptiveLaborPermission permissions)
    {
        CaptiveLaborPermission normalized =
            permissions & CaptiveLaborPermission.All;
        state.laborPermissions = normalized;
        state.status = normalized == CaptiveLaborPermission.None
            ? CaptivityStatus.Confined
            : CaptivityStatus.Labor;
        CharacterActor laborer = actorRuntime.Find(state.captiveId);
        if (laborer != null)
        {
            laborer.characterType = normalized == CaptiveLaborPermission.None
                ? CharacterType.Intruder
                : CharacterType.NPC;
        }
        laborer?.SetAiPaused(normalized == CaptiveLaborPermission.None);
        laborer?.SetLifecycleState(
            normalized == CaptiveLaborPermission.None
                ? CharacterLifecycleState.Downed
                : CharacterLifecycleState.Active);
        state.lastResult = normalized == CaptiveLaborPermission.None
            ? "노역 해제"
            : "노역 허용";
    }

    private bool CancelLaborToolPreparation(CaptiveState state)
    {
        if (state == null)
        {
            return true;
        }

        if (CaptivityLaborToolAssignmentOutbox.RequiresFinalization(state)
            && !CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                state,
                batchDispositions,
                out _))
        {
            return false;
        }

        CaptiveLaborPermission previousPermissions =
            state.pendingLaborPermissions;
        string previousDestination = state.laborToolDestinationId;
        state.pendingLaborPermissions = CaptiveLaborPermission.None;
        state.laborToolDestinationId = string.Empty;
        if (!careLaborInputOwner.TryReconcileLive(
                actors.States,
                out _))
        {
            state.pendingLaborPermissions = previousPermissions;
            state.laborToolDestinationId = previousDestination;
            return false;
        }
        return true;
    }

    public bool ReturnAssignedCaptivityTools(CaptiveState state)
    {
        if (state == null)
        {
            return true;
        }

        if (!TryReturnLaborTool(state))
        {
            return false;
        }
        if (!durableTools.TryReturnRestraint(
                itemRuntime, state, state.housingPosition))
        {
            return false;
        }
        return CancelLaborToolPreparation(state);
    }

    private bool TryReturnLaborTool(CaptiveState state)
    {
        if (CaptivityLaborToolAssignmentOutbox.RequiresFinalization(state)
            && !CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                state, batchDispositions, out _))
            return false;
        return durableTools.TryReturnLaborTool(
            itemRuntime, state, state.housingPosition);
    }

    public bool IsWorkAllowed(
        CharacterActor actor,
        WorkTypeId workTypeId,
        out string reason)
    {
        reason = string.Empty;
        CaptiveState state = actors.FindState(actor != null ? CharacterPersistentIdentity.Require(actor).Value : string.Empty);
        if (state == null || !state.IsInCustody)
        {
            return true;
        }

        if (state.status != CaptivityStatus.Labor
            || !CanLabor(state, actor)
            || string.IsNullOrWhiteSpace(state.assignedLaborToolItemId)
            || state.assignedLaborToolDurability <= 0f)
        {
            reason = "이 포로는 현재 노역에 투입할 수 없습니다.";
            return false;
        }

        CaptiveLaborPermission required = GetLaborPermission(workTypeId);
        if (required != CaptiveLaborPermission.None
            && (state.laborPermissions & required) != 0)
        {
            return true;
        }

        reason = $"{WorkTaskCatalog.GetDisplayName(workTypeId)} 노역은 허용되지 않았습니다.";
        return false;
    }

    public bool TrySetLaborPermissions(
        string captiveId,
        CaptiveLaborPermission permissions,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = actors.FindState(captiveId);
        if (state == null || !state.IsInCustody)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        CaptiveLaborPermission requestedPermissions =
            permissions & CaptiveLaborPermission.All;
        if (requestedPermissions != CaptiveLaborPermission.None
            && !CanLabor(state, actorRuntime.Find(state.captiveId)))
        {
            failureReason = "순응도 50 이상, 건강 40% 이상부터 노역을 허용할 수 있습니다.";
            return false;
        }

        CaptiveLaborPermission requested = requestedPermissions;
        if (requested == CaptiveLaborPermission.None)
        {
            if (!TryReturnLaborTool(state))
            {
                failureReason = "포로 작업 도구를 반납할 수 없습니다.";
                return false;
            }
            if (!CancelLaborToolPreparation(state))
            {
                failureReason = "포로 작업 도구 소유권을 정리할 수 없습니다.";
                return false;
            }
            ApplyLaborPermissions(state, CaptiveLaborPermission.None);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(state.assignedLaborToolItemId)
            && state.assignedLaborToolDurability > 0f
            && state.laborToolAssignmentCompleted)
        {
            ApplyLaborPermissions(state, requested);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(state.assignedLaborToolItemId)
            && state.assignedLaborToolDurability <= 0f
            && !TryReturnLaborTool(state))
        {
            failureReason = "파손된 포로 작업 도구를 먼저 반납해야 합니다.";
            return false;
        }

        string destinationId = CaptivityCareLaborInputOwnerAuthority
            .FormatLaborToolDestinationId(state.captiveId);
        CaptivityStatus previousStatus = state.status;
        string previousResult = state.lastResult;
        CaptiveLaborPermission previousPendingPermissions = state.pendingLaborPermissions;
        string previousDestinationId = state.laborToolDestinationId;
        state.pendingLaborPermissions = requested;
        state.laborToolDestinationId = destinationId;
        state.status = CaptivityStatus.Confined;
        state.lastResult = "포로 작업 도구 운반 대기";
        if (!careLaborInputOwner.TryReconcileLive(
                actors.States,
                out failureReason))
        {
            state.pendingLaborPermissions = previousPendingPermissions;
            state.laborToolDestinationId = previousDestinationId;
            state.status = previousStatus;
            state.lastResult = previousResult;
            failureReason = "포로 작업 도구의 물리 소유권을 열 수 없습니다: "
                + failureReason;
            return false;
        }
        if (!itemRuntime.TryRequestItemDelivery(
                CaptivityItemDefinitions.PrisonerWorkKitItemId,
                1,
                state.housingPosition,
                destinationId,
                out int requestedAmount,
                out failureReason)
            || requestedAmount < 1)
        {
            state.pendingLaborPermissions = previousPendingPermissions;
            state.laborToolDestinationId = previousDestinationId;
            state.status = previousStatus;
            state.lastResult = previousResult;
            if (!careLaborInputOwner.TryReconcileLive(
                    actors.States,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Captive labor-tool delivery failed and exact owner rollback failed: "
                    + rollbackFailure);
            }
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "포로 작업 도구를 감방으로 운반할 수 없습니다."
                : failureReason;
            return false;
        }
        return true;
    }

    private bool CanLabor(CaptiveState state, CharacterActor actor)
    {
        if (state == null || actor == null)
        {
            return false;
        }

        CharacterVitalsSnapshot vitals = bodyHealthQuery.GetVitals(actor);
        CharacterBodyHealthSnapshot body = bodyHealthQuery.GetSnapshot(actor);
        return state.CanLaborWithBody(
            CaptivityBodyHealthRules.GetHealthPercent(
                vitals.CurrentHealth,
                vitals.MaximumHealth),
            CaptivityBodyHealthRules.IsBodyAvailable(
                actor.IsDead,
                vitals.IsDead,
                body.Downed));
    }

    private static CaptiveLaborPermission GetLaborPermission(WorkTypeId workTypeId)
    {
        if (workTypeId == BuiltInWorkTypeIds.Clean)
        {
            return CaptiveLaborPermission.Clean;
        }

        if (workTypeId == BuiltInWorkTypeIds.Haul
            || workTypeId == BuiltInWorkTypeIds.Restock)
        {
            return CaptiveLaborPermission.Haul;
        }

        if (workTypeId == BuiltInWorkTypeIds.DrawWater)
        {
            return CaptiveLaborPermission.DrawWater;
        }

        if (workTypeId == BuiltInWorkTypeIds.Refuel)
        {
            return CaptiveLaborPermission.Refuel;
        }

        if (workTypeId == BuiltInWorkTypeIds.Construct)
        {
            return CaptiveLaborPermission.Construct;
        }

        if (workTypeId == BuiltInWorkTypeIds.Repair)
        {
            return CaptiveLaborPermission.Repair;
        }

        if (workTypeId == BuiltInWorkTypeIds.Butcher)
        {
            return CaptiveLaborPermission.Butcher;
        }

        if (workTypeId == BuiltInWorkTypeIds.Craft)
        {
            return CaptiveLaborPermission.CraftAssist;
        }

        return CaptiveLaborPermission.None;
    }

    private bool TryGetHousing(string captiveId, out BuildableObject housing)
    {
        CaptiveState state = actors.FindState(captiveId);
        string housingId = state?.housingBuildingId ?? string.Empty;
        housing = worldRegistry.Buildings.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.isDestroy
            && string.Equals(
                candidate.RequirePersistentInstanceId().Value,
                housingId,
                StringComparison.Ordinal));
        return housing != null;
    }
}
