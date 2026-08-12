using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CharacterSafeReliefRunner
{
    private const string IntentOwnerId = "survival:safe-relief";
    private const int MaximumStartsPerFrame = 2;

    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldWaterQuery waterQuery;
    private readonly IGameClock gameClock;
    private readonly ICharacterNeedBalanceRuntime needBalanceRuntime;
    private readonly IGameEventBus events;
    private readonly CharacterDeprivationStateStore stateStore;
    private readonly CharacterSafeDrinkPlanner planner;
    private readonly CharacterEmergencyMovement movement;
    private readonly CharacterDeprivationDiagnostics diagnostics;
    private readonly HashSet<CharacterId> runningActorIds =
        new HashSet<CharacterId>();
    private int startFrame = -1;
    private int startsThisFrame;

    public CharacterSafeReliefRunner(
        IWorldItemStackRuntime itemStackRuntime,
        IWorldWaterQuery waterQuery,
        IGameClock gameClock,
        ICharacterNeedBalanceRuntime needBalanceRuntime,
        IGameEventBus events,
        CharacterDeprivationStateStore stateStore,
        CharacterSafeDrinkPlanner planner,
        CharacterEmergencyMovement movement,
        CharacterDeprivationDiagnostics diagnostics)
    {
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.waterQuery = waterQuery
            ?? throw new ArgumentNullException(nameof(waterQuery));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.needBalanceRuntime = needBalanceRuntime
            ?? throw new ArgumentNullException(nameof(needBalanceRuntime));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.planner = planner
            ?? throw new ArgumentNullException(nameof(planner));
        this.movement = movement
            ?? throw new ArgumentNullException(nameof(movement));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public int ActiveCount => runningActorIds.Count;

    public bool TryStart(CharacterActor actor, bool emergency, out string status)
    {
        status = string.Empty;
        diagnostics.SafeReliefRequests++;
        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        CharacterDeprivationState deprivation = stateStore.Ensure(actorId);
        if (runningActorIds.Contains(actorId))
        {
            status = "식수를 찾는 중";
            return true;
        }

        float now = gameClock.Time;
        if (now < deprivation.nextSafeReliefAttemptAt)
        {
            StartDeferredRetry(
                actor,
                actorId,
                deprivation.nextSafeReliefAttemptAt);
            status = "물을 마실 자리를 기다리는 중";
            return true;
        }

        if (!CanStartThisFrame())
        {
            StartDeferredRetry(
                actor,
                actorId,
                now + CharacterSafeDrinkPlanner.GetRetryDelay(actorId.Value));
            status = "급수 순서를 기다리는 중";
            return true;
        }

        if (!planner.TryCreatePlan(
                actor,
                actorId.Value,
                out CharacterSafeDrinkPlan plan))
        {
            diagnostics.SafeReliefPlanFailures++;
            deprivation.nextSafeReliefAttemptAt = now
                + CharacterSafeDrinkPlanner.GetRetryDelay(actorId.Value);
            StartDeferredRetry(
                actor,
                actorId,
                deprivation.nextSafeReliefAttemptAt);
            status = "물을 마실 자리를 기다리는 중";
            return true;
        }

        RecordStart();
        RecordPlanDiagnostics(actor, plan);
        CharacterActionIntentKind intentKind = emergency
            ? CharacterActionIntentKind.EmergencyNeed
            : CharacterActionIntentKind.RoutineNeed;
        if (actor.Brain == null
            || !actor.Brain.TryBeginExternallyDrivenAction(
                IntentOwnerId,
                intentKind,
                emergency ? "긴급 식수 확보" : "식수 이용",
                "이동 중",
                $"목표 ({plan.TargetPosition.x}, {plan.TargetPosition.y})",
                out CharacterActionIntentLease intentLease))
        {
            planner.Release(actorId.Value, plan.ApproachPosition);
            deprivation.nextSafeReliefAttemptAt = now
                + CharacterSafeDrinkPlanner.GetRetryDelay(actorId.Value);
            status = "더 높은 우선순위 행동이 끝나기를 기다리는 중";
            return false;
        }

        runningActorIds.Add(actorId);
        actor.StartCoroutine(Run(actor, actorId, plan, intentLease));
        status = emergency
            ? "심한 갈증 때문에 식수를 찾음"
            : "일상적으로 식수를 이용함";
        return true;
    }

    private void StartDeferredRetry(
        CharacterActor actor,
        CharacterId actorId,
        float retryAt)
    {
        if (actor == null || actor.IsDead || runningActorIds.Contains(actorId))
        {
            return;
        }

        runningActorIds.Add(actorId);
        actor.StartCoroutine(WaitForRetry(actor, actorId, retryAt));
    }

    private IEnumerator WaitForRetry(
        CharacterActor actor,
        CharacterId actorId,
        float retryAt)
    {
        try
        {
            while (actor != null
                && !actor.IsDead
                && gameClock.Time < retryAt)
            {
                yield return null;
            }
        }
        finally
        {
            runningActorIds.Remove(actorId);
            if (actor != null && !actor.IsDead)
            {
                actor.Brain?.RequestImmediateReplan(clearFailures: false);
            }
        }
    }

    public bool IsRunning(CharacterId actorId)
    {
        return actorId.IsValid && runningActorIds.Contains(actorId);
    }

    public void ReleaseActor(CharacterId actorId)
    {
        if (!actorId.IsValid)
        {
            return;
        }

        runningActorIds.Remove(actorId);
        planner.ReleaseForActor(actorId.Value);
    }

    public void Reset()
    {
        runningActorIds.Clear();
        startFrame = -1;
        startsThisFrame = 0;
    }

    private IEnumerator Run(
        CharacterActor actor,
        CharacterId actorId,
        CharacterSafeDrinkPlan plan,
        CharacterActionIntentLease intentLease)
    {
        float startedAt = gameClock.Time;
        try
        {
            yield return movement.MoveNear(actor, plan.ApproachPosition, 0, plan.Path);
            if (actor == null
                || actor.IsDead
                || actor.Brain == null
                || !actor.Brain.IsExternalIntentCurrent(intentLease)
                || actor.GetNowXY() != plan.ApproachPosition)
            {
                RecordMoveFailure(actor);
                yield break;
            }

            diagnostics.SafeReliefArrivals++;
            diagnostics.SafeReliefInteractionAttempts++;
            if (actor.Brain.IsExternalIntentCurrent(intentLease)
                && TryConsumePlan(actor, plan))
            {
                diagnostics.SafeReliefSuccesses++;
            }
        }
        finally
        {
            float duration = Mathf.Max(0f, gameClock.Time - startedAt);
            diagnostics.SafeReliefActionsFinished++;
            diagnostics.SafeReliefCompletedDurationSeconds += duration;
            diagnostics.SafeReliefMaximumDurationSeconds = Mathf.Max(
                diagnostics.SafeReliefMaximumDurationSeconds,
                duration);
            runningActorIds.Remove(actorId);
            planner.Release(actorId.Value, plan.ApproachPosition);
            stateStore.Ensure(actorId).nextSafeReliefAttemptAt =
                gameClock.Time
                + CharacterSafeDrinkPlanner.GetRetryDelay(actorId.Value);
            actor?.Brain?.EndExternallyDrivenAction(
                intentLease,
                clearFailures: true);
        }
    }

    private bool TryConsumePlan(CharacterActor actor, CharacterSafeDrinkPlan plan)
    {
        switch (plan.Kind)
        {
            case CharacterSafeDrinkTargetKind.ItemStack:
                if (Manhattan(actor.GetNowXY(), plan.TargetPosition) <= 1
                    && itemStackRuntime.TryConsumeStackQuantity(plan.TargetId, 1, out _))
                {
                    RecoverThirst(actor, 65f);
                    actor.ApplyMoodFactor("survival:clean-water", "깨끗한 물을 마심", 2f, 90f, 1);
                    PublishWaterConsumed(
                        actor,
                        plan.TargetId,
                        WorldWaterQuality.Clean,
                        1f,
                        string.Empty);
                    return true;
                }
                break;

            case CharacterSafeDrinkTargetKind.Facility:
                BuildableObject facility = plan.Facility;
                if (facility != null
                    && !facility.IsGridDestroyed
                    && Manhattan(actor.GetNowXY(), facility.centerPos) <= 1)
                {
                    RecoverThirst(actor, 65f);
                    actor.ApplyMoodFactor("survival:well-water", "우물에서 물을 마심", 1f, 90f, 1);
                    PublishWaterConsumed(
                        actor,
                        facility.RequirePersistentInstanceId().Value,
                        WorldWaterQuality.Clean,
                        1f,
                        string.Empty);
                    return true;
                }
                break;

            case CharacterSafeDrinkTargetKind.WorldSource:
                if (Manhattan(actor.GetNowXY(), plan.TargetPosition) <= 1
                    && waterQuery.TryGetSource(
                        plan.TargetId,
                        out WorldWaterSourceSnapshot source)
                    && waterQuery.TryDrink(
                        plan.TargetId,
                        needBalanceRuntime.ApplyPersonalContinuousWaterMultiplier(1f),
                        out WorldWaterQuality quality,
                        out float consumed)
                    && consumed > 0f
                    && quality == WorldWaterQuality.Clean)
                {
                    RecoverThirst(actor, 65f);
                    PublishWaterConsumed(actor, source, quality, consumed);
                    return true;
                }
                break;
        }

        return false;
    }

    private void PublishWaterConsumed(
        CharacterActor actor,
        WorldWaterSourceSnapshot source,
        WorldWaterQuality quality,
        float consumed)
    {
        PublishWaterConsumed(
            actor,
            source.SourceId,
            quality,
            consumed,
            source.PathogenDiseaseId);
    }

    private void PublishWaterConsumed(
        CharacterActor actor,
        string sourceId,
        WorldWaterQuality quality,
        float consumed,
        string pathogenDiseaseId)
    {
        events.Publish(new CharacterWaterConsumedEvent(
            CharacterPersistentIdentity.Require(actor),
            sourceId,
            quality,
            consumed,
            pathogenDiseaseId));
    }

    private void RecordPlanDiagnostics(CharacterActor actor, CharacterSafeDrinkPlan plan)
    {
        diagnostics.SafeReliefActionsStarted++;
        int plannedSteps = plan.Path?.Count ?? 0;
        diagnostics.SafeReliefPlannedPathSteps += plannedSteps;
        diagnostics.SafeReliefMaximumPlannedPathSteps = Mathf.Max(
            diagnostics.SafeReliefMaximumPlannedPathSteps,
            plannedSteps);
        if (actor.GetNowXY().y != plan.TargetPosition.y)
        {
            diagnostics.SafeReliefCrossFloorTargetPlans++;
        }

        if (plan.Path == null)
        {
            return;
        }

        int verticalSteps = 0;
        foreach (GridMoveStep step in plan.Path)
        {
            if (step.From.y != step.To.y)
            {
                verticalSteps++;
            }
        }

        if (verticalSteps > 0)
        {
            diagnostics.SafeReliefPathsWithVerticalTraversal++;
            diagnostics.SafeReliefVerticalTraversalSteps += verticalSteps;
        }
    }

    private void RecordMoveFailure(CharacterActor actor)
    {
        diagnostics.SafeReliefMoveFailures++;
        if (actor == null)
        {
            diagnostics.SafeReliefActorMissingMoveFailures++;
            return;
        }

        if (actor.IsDead)
        {
            diagnostics.SafeReliefActorDeadMoveFailures++;
            return;
        }

        if (stateStore.TryGet(actor, out CharacterDeprivationState state)
            && state.breakdown != null
            && state.breakdown.active)
        {
            diagnostics.SafeReliefBreakdownMoveFailures++;
            return;
        }

        if (actor.TryGetAbility(out AbilityMove move) && move.LastGridMoveWasBlocked)
        {
            diagnostics.SafeReliefBlockedMoveFailures++;
            RecordBlockedReason(move.LastGridMoveFailureReason);
            return;
        }

        diagnostics.SafeReliefOtherMoveFailures++;
        RecordOtherFailureReason(
            actor.TryGetAbility(out AbilityMove currentMove)
                ? currentMove.LastGridMoveFailureReason
                : GridMoveFailureReason.None);
    }

    private void RecordBlockedReason(GridMoveFailureReason reason)
    {
        switch (reason)
        {
            case GridMoveFailureReason.StaleStepStart:
                diagnostics.SafeReliefStaleStartFailures++;
                break;
            case GridMoveFailureReason.WallBlocked:
                diagnostics.SafeReliefWallBlockedFailures++;
                break;
            case GridMoveFailureReason.DoorDenied:
                diagnostics.SafeReliefDoorDeniedFailures++;
                break;
            case GridMoveFailureReason.DefenseReservation:
                diagnostics.SafeReliefDefenseReservationFailures++;
                break;
            case GridMoveFailureReason.TraversalChanged:
                diagnostics.SafeReliefTraversalChangedFailures++;
                break;
        }
    }

    private void RecordOtherFailureReason(GridMoveFailureReason reason)
    {
        switch (reason)
        {
            case GridMoveFailureReason.Cancelled:
                diagnostics.SafeReliefCancelledMoveFailures++;
                break;
            case GridMoveFailureReason.MissingPath:
                diagnostics.SafeReliefMissingPathFailures++;
                break;
            case GridMoveFailureReason.MissingMovementHandler:
                diagnostics.SafeReliefMissingMovementHandlerFailures++;
                break;
            case GridMoveFailureReason.GridUnavailable:
                diagnostics.SafeReliefGridUnavailableFailures++;
                break;
            case GridMoveFailureReason.InvalidSpeed:
                diagnostics.SafeReliefInvalidSpeedFailures++;
                break;
            default:
                diagnostics.SafeReliefNoFailureReasonFailures++;
                break;
        }
    }

    private bool CanStartThisFrame()
    {
        int frame = gameClock.FrameCount;
        if (startFrame != frame)
        {
            startFrame = frame;
            startsThisFrame = 0;
        }

        return startsThisFrame < MaximumStartsPerFrame;
    }

    private void RecordStart()
    {
        CanStartThisFrame();
        startsThisFrame++;
    }

    private static void RecoverThirst(CharacterActor actor, float amount)
    {
        actor?.Stats?.RecoverNeed(
            CharacterCondition.THIRST,
            amount,
            CharacterNeedRecoverySource.Emergency);
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
