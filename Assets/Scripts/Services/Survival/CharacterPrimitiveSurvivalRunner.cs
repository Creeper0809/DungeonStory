using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

internal enum CharacterPrimitiveSurvivalActionKind
{
    FieldMeal,
    FloorRest,
    Latrine,
    BucketWash
}

public static class PrimitiveSurvivalBalanceAuthority
{
    public const float FieldMealSeconds = 4f;
    public const float FloorRestSeconds = 60f;
    public const float LatrineSeconds = 6f;
    public const float BucketWashSeconds = 6f;
    public const float FloorRestRecovery = 55f;
    public const float FloorRestHygieneDelta = -4f;
    public const float FloorRestMoodDelta = -3f;
    public const float LatrineRecovery = 85f;
    public const float LatrineHygieneDelta = -8f;
    public const float LatrineMoodDelta = -2f;
    public const float LatrineWaste = 8f;
    public const float LatrineStain = 2f;
    public const float BucketWashRecovery = 50f;
    public const string CleanWaterItemId = "resource:clean-water";
}

/// <summary>
/// Explicit, inferior survival bridge used before proper service facilities exist.
/// It owns transient movement/action state only; needs and physical items remain in
/// their existing authoritative runtimes.
/// </summary>
internal sealed class CharacterPrimitiveSurvivalRunner
{
    private const string IntentOwnerPrefix = "survival:primitive:";
    private const int PrimitiveLatrineSearchRadius = 8;
    private const float AuthoredFacilityRecheckSeconds = 1f;
    private const int MaximumFieldMealSourceChanges = 4;

    private readonly CharacterBreakdownWorld world;
    private readonly CharacterEmergencyMovement movement;
    private readonly CharacterDeprivationStateStore stateStore;
    private readonly IGameClock clock;
    private readonly IFieldMealConsumptionCommand fieldMeals;
    private readonly IGameEventBus events;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly IReservedItemTransferService reservedTransfers;
    private readonly ICharacterPrimitiveSurvivalCoroutineScheduler coroutineScheduler;
    private readonly CharacterPrimitiveSurvivalCompletion completion;
    private readonly Dictionary<CharacterId, RunningPrimitiveAction> runningActions = new();

    private sealed class RunningPrimitiveAction
    {
        internal RunningPrimitiveAction(CharacterActor actor, CharacterPrimitiveSurvivalActionKind kind,
            CharacterActionIntentKind intentKind, CharacterActionIntentLease intentLease)
        { Actor = actor; Kind = kind; IntentKind = intentKind; IntentLease = intentLease; }
        internal CharacterActor Actor { get; }
        internal CharacterPrimitiveSurvivalActionKind Kind { get; }
        internal CharacterActionIntentKind IntentKind { get; }
        internal CharacterActionIntentLease IntentLease { get; }
        internal bool SharesIntent(CharacterActor actor, in CharacterActionIntentLease lease) =>
            ReferenceEquals(Actor, actor) && IntentLease.Epoch == lease.Epoch
            && IntentLease.Kind == lease.Kind
            && string.Equals(IntentLease.OwnerId, lease.OwnerId, StringComparison.Ordinal);
    }

    internal CharacterPrimitiveSurvivalRunner(
        CharacterBreakdownWorld world,
        CharacterEmergencyMovement movement,
        CharacterDeprivationStateStore stateStore,
        IGameClock clock,
        IGameEventBus events,
        CharacterPrimitiveSurvivalDependencies dependencies)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        dependencies = dependencies
            ?? throw new ArgumentNullException(nameof(dependencies));
        fieldMeals = dependencies.FieldMeals;
        quantityReservations = dependencies.QuantityReservations;
        reservedTransfers = dependencies.ReservedTransfers;
        coroutineScheduler = dependencies.CoroutineScheduler;
        completion = new CharacterPrimitiveSurvivalCompletion(world, stateStore, clock, events,
            dependencies.Calendar, dependencies.PrimitiveFacts);
    }

    internal bool IsRunning(CharacterId actorId) =>
        actorId.IsValid && runningActions.ContainsKey(actorId);

    internal bool HasFieldMeal(CharacterActor actor, out string reason)
    {
        bool found = fieldMeals.TryFindFieldMeal(
            actor,
            out _,
            out _,
            out CharacterConsumablesFailure failure);
        reason = found
            ? "시설 없이 먹을 수 있는 배급식이 있습니다."
            : failure.Code.ToString();
        return found;
    }

    internal bool HasWashWater(CharacterActor actor, out string reason)
    {
        bool found = actor != null
            && world.TryFindBestAvailableStack(
                actor.GetNowXY(),
                itemId => string.Equals(
                    itemId,
                    PrimitiveSurvivalBalanceAuthority.CleanWaterItemId,
                    StringComparison.Ordinal)
                    ? 0
                    : int.MaxValue,
                out WorldItemStackSnapshot stack)
            && stack != null
            && stack.AvailableQuantity > 0;
        reason = found
            ? "깨끗한 물 1개로 간이 세척할 수 있습니다."
            : "간이 세척에 필요한 깨끗한 물이 없습니다.";
        return found;
    }

    internal void RecoverRetiredPublications() => completion.RecoverRetiredPublications();

    internal bool TryResumePending(CharacterActor actor, out string status)
    {
        status = string.Empty;
        if (actor == null || actor.IsDead)
        {
            return false;
        }

        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        if (IsRunning(actorId))
        {
            status = "현재 생존 행동 종료 대기";
            return false;
        }
        return completion.TryResumePending(actor, out status);
    }

    internal bool TryStart(
        CharacterActor actor,
        CharacterPrimitiveSurvivalActionKind kind,
        out string status)
    {
        status = string.Empty;
        if (actor == null || actor.IsDead)
        {
            return false;
        }

        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        if (stateStore.TryGetPrimitiveSurvivalPending(actorId, out _))
        {
            status = "이전 생존 행동 결과 복구 대기";
            return false;
        }
        CharacterActionIntentKind intentKind = ResolveIntentKind(actor, kind);
        if (runningActions.TryGetValue(
                actorId,
                out RunningPrimitiveAction running)
            && ReferenceEquals(running.Actor, actor)
            && actor.Brain?.IsExternalIntentCurrent(running.IntentLease) == true
            && intentKind <= running.IntentKind)
        {
            status = GetLabel(running.Kind) + " 진행 중";
            return true;
        }

        if (actor.Brain == null
            || !actor.Brain.TryBeginExternallyDrivenAction(
                IntentOwnerPrefix + kind,
                intentKind,
                GetLabel(kind),
                "시설 없는 초기 생존 행동을 수행하는 중",
                GetReason(kind),
                out CharacterActionIntentLease intentLease))
        {
            status = "더 높은 우선순위 행동이 진행 중";
            return false;
        }

        var run = new RunningPrimitiveAction(actor, kind, intentKind, intentLease);
        runningActions[actorId] = run;
        try
        {
            CharacterCondition condition = GetCondition(kind);
            float needValue = actor.Stats != null
                && actor.Stats.TryGetConditionValue(condition, out float currentNeed)
                    ? currentNeed : 0f;
            events.Publish(new CharacterPrimitiveSurvivalStartedEvent(
                actorId, GetActionId(kind), intentKind >= CharacterActionIntentKind.EmergencyNeed, needValue));
            events.Publish(new PrimitiveSurvivalChangedEvent(PrimitiveSurvivalTransition.Started, actor,
                kind: kind.ToString(), intentKind: intentKind, epoch: intentLease.Epoch, position: actor.GetNowXY()));
            coroutineScheduler.Start(actor, Run(actor, actorId, kind, intentLease, run));
            status = GetLabel(kind);
            return true;
        }
        catch
        {
            CompleteRunningAction(actorId, run);
            if (!ReplacementSharesIntent(actorId, run, actor, intentLease)
                && actor.Brain?.IsExternalIntentCurrent(intentLease) == true)
                actor.Brain.EndExternallyDrivenAction(intentLease, clearFailures: true);
            throw;
        }
    }

    internal void ReleaseActor(CharacterId actorId)
    {
        if (actorId.IsValid)
        {
            runningActions.Remove(actorId);
        }
    }

    internal void Reset()
    {
        runningActions.Clear();
        completion.Reset();
    }

    private IEnumerator Run(CharacterActor actor, CharacterId actorId,
        CharacterPrimitiveSurvivalActionKind kind, CharacterActionIntentLease intentLease,
        RunningPrimitiveAction run)
    {
        try
        {
            if (!IsCurrentRun(actorId, run)) yield break;
            IEnumerator action = kind switch
            {
                CharacterPrimitiveSurvivalActionKind.FieldMeal => RunFieldMeal(actor, intentLease),
                CharacterPrimitiveSurvivalActionKind.FloorRest => RunFloorRest(actor, intentLease),
                CharacterPrimitiveSurvivalActionKind.Latrine => RunLatrine(actor, intentLease),
                CharacterPrimitiveSurvivalActionKind.BucketWash => RunBucketWash(actor, actorId, intentLease),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown primitive survival action.")
            };
            IEnumerator guarded = RunWhileCurrent(action, () => IsCurrentRun(actorId, run) && CanCommit(actor, intentLease));
            try { yield return guarded; }
            finally { (guarded as IDisposable)?.Dispose(); }
        }
        finally
        {
            CompleteRunningAction(actorId, run);
            if (!ReplacementSharesIntent(actorId, run, actor, intentLease)
                && actor?.Brain?.IsExternalIntentCurrent(intentLease) == true)
                actor.Brain.EndExternallyDrivenAction(intentLease, clearFailures: true);
        }
    }

    private bool IsCurrentRun(CharacterId actorId, RunningPrimitiveAction run) =>
        runningActions.TryGetValue(actorId, out RunningPrimitiveAction current) && ReferenceEquals(current, run);

    private bool CompleteRunningAction(CharacterId actorId, RunningPrimitiveAction run) =>
        IsCurrentRun(actorId, run) && runningActions.Remove(actorId);

    private bool ReplacementSharesIntent(CharacterId actorId, RunningPrimitiveAction run,
        CharacterActor actor, CharacterActionIntentLease lease) =>
        runningActions.TryGetValue(actorId, out RunningPrimitiveAction current)
        && !ReferenceEquals(current, run) && current.SharesIntent(actor, lease);

    private static IEnumerator RunWhileCurrent(IEnumerator action, Func<bool> isCurrent)
    {
        try
        {
            while (isCurrent() && action.MoveNext())
            {
                object current = action.Current;
                if (current is IEnumerator nested)
                {
                    IEnumerator guarded = RunWhileCurrent(nested, isCurrent);
                    try { yield return guarded; }
                    finally { (guarded as IDisposable)?.Dispose(); }
                }
                else yield return current;
            }
        }
        finally { (action as IDisposable)?.Dispose(); }
    }

    private static bool IsEmergency(
        CharacterActor actor,
        CharacterPrimitiveSurvivalActionKind kind)
    {
        CharacterCondition condition = GetCondition(kind);
        return kind == CharacterPrimitiveSurvivalActionKind.FieldMeal
            ? CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(
                actor,
                condition)
            : CharacterNeedAiThresholds.IsEmergency(actor, condition);
    }

    private static CharacterActionIntentKind ResolveIntentKind(
        CharacterActor actor,
        CharacterPrimitiveSurvivalActionKind kind)
    {
        if (!IsEmergency(actor, kind))
        {
            return CharacterActionIntentKind.RoutineNeed;
        }

        return CharacterNeedAiThresholds.GetEmergencyIntentKind(
            actor,
            GetCondition(kind));
    }

    private static CharacterCondition GetCondition(
        CharacterPrimitiveSurvivalActionKind kind) =>
        kind switch
        {
            CharacterPrimitiveSurvivalActionKind.FieldMeal => CharacterCondition.HUNGER,
            CharacterPrimitiveSurvivalActionKind.FloorRest => CharacterCondition.SLEEP,
            CharacterPrimitiveSurvivalActionKind.Latrine => CharacterCondition.EXCRETION,
            CharacterPrimitiveSurvivalActionKind.BucketWash => CharacterCondition.HYGIENE,
            _ => default
        };

    private static string GetActionId(
        CharacterPrimitiveSurvivalActionKind kind) =>
        kind switch
        {
            CharacterPrimitiveSurvivalActionKind.FieldMeal => "survival:field-meal",
            CharacterPrimitiveSurvivalActionKind.FloorRest => "survival:floor-rest",
            CharacterPrimitiveSurvivalActionKind.Latrine => "survival:primitive-latrine",
            CharacterPrimitiveSurvivalActionKind.BucketWash => "survival:bucket-wash",
            _ => "survival:primitive"
        };

    private IEnumerator RunFieldMeal(
        CharacterActor actor,
        CharacterActionIntentLease intentLease,
        int sourceRevision = 0)
    {
        if (!ContinuePrimitiveFallback(
                actor,
                CharacterPrimitiveSurvivalActionKind.FieldMeal,
                intentLease,
                "before-source-selection"))
        {
            yield break;
        }

        if (!fieldMeals.TryFindFieldMeal(
                actor,
                out ItemStackId stackId,
                out Vector2Int position,
                out _))
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.FieldMeal,
                "field-meal-missing", "No physical field meal was available at execution time.");
            yield break;
        }

        yield return movement.MoveNear(actor, position, 1);
        if (!ContinuePrimitiveFallback(
                actor,
                CharacterPrimitiveSurvivalActionKind.FieldMeal,
                intentLease,
                "after-approach"))
        {
            yield break;
        }
        if (!IsAliveAndNear(actor, position, 1))
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.FieldMeal,
                "field-meal-approach-failed",
                $"leaseCurrent={CanCommit(actor, intentLease)}; actor={actor?.GetNowXY()}; target={position}");
            yield break;
        }
        if (!IsCurrentFieldMealSource(
                actor,
                stackId,
                position,
                sourceRevision,
                "approach"))
        {
            if (sourceRevision >= MaximumFieldMealSourceChanges)
            {
                RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.FieldMeal,
                    "field-meal-source-churn",
                    $"Physical meal source changed more than {MaximumFieldMealSourceChanges} times during one action.");
                yield break;
            }

            yield return RunFieldMeal(actor, intentLease, sourceRevision + 1);
            yield break;
        }
        yield return WaitGameSeconds(
            actor,
            CharacterPrimitiveSurvivalActionKind.FieldMeal,
            intentLease,
            PrimitiveSurvivalBalanceAuthority.FieldMealSeconds);
        if (CanCommit(actor, intentLease)
            && !IsCurrentFieldMealSource(
                actor,
                stackId,
                position,
                sourceRevision,
                "commit"))
        {
            if (sourceRevision >= MaximumFieldMealSourceChanges)
            {
                RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.FieldMeal,
                    "field-meal-source-churn",
                    $"Physical meal source changed more than {MaximumFieldMealSourceChanges} times during one action.");
                yield break;
            }

            yield return RunFieldMeal(actor, intentLease, sourceRevision + 1);
            yield break;
        }
        if (CanCommit(actor, intentLease)
            && IsAliveAndNear(actor, position, 1)
            && fieldMeals is IPrimitiveFieldMealConsumptionCommand primitiveMeals
            && primitiveMeals.TryConsumePrimitiveFieldMeal(
                actor,
                stackId,
                out MealConsumptionResult result)
            && result.Success)
        {
            events.Publish(new PrimitiveSurvivalChangedEvent(PrimitiveSurvivalTransition.FieldMealCompleted, actor,
                displayName: result.DisplayName, sourceStackId: stackId.Value, recovery: result.Nutrition));
            completion.PublishCompletedEvent(
                actor,
                "survival:field-meal",
                result.Nutrition,
                1);
        }
        else if (CanCommit(actor, intentLease))
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.FieldMeal,
                "field-meal-commit-failed",
                $"leaseCurrent={CanCommit(actor, intentLease)}; actor={actor?.GetNowXY()}; target={position}; stack={stackId.Value}");
        }
    }

    private bool IsCurrentFieldMealSource(
        CharacterActor actor,
        ItemStackId expectedStackId,
        Vector2Int expectedPosition,
        int sourceRevision,
        string phase)
    {
        WorldItemStackSnapshot current = world
            .GetStacksAt(expectedPosition, includeStored: true)
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.StackId,
                    expectedStackId.Value,
                    StringComparison.Ordinal)
                && candidate.AvailableQuantity > 0
                && candidate.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored);
        if (current != null)
        {
            return true;
        }

        if (actor != null)
            events.Publish(new PrimitiveSurvivalChangedEvent(PrimitiveSurvivalTransition.MealSourceChanged, actor,
                phase: phase, sourceRevision: sourceRevision, sourceStackId: expectedStackId.Value, sourcePosition: expectedPosition));
        return false;
    }

    private IEnumerator RunFloorRest(
        CharacterActor actor,
        CharacterActionIntentLease intentLease)
    {
        Vector2Int position = actor.GetNowXY();
        yield return WaitGameSeconds(
            actor,
            CharacterPrimitiveSurvivalActionKind.FloorRest,
            intentLease,
            PrimitiveSurvivalBalanceAuthority.FloorRestSeconds);
        if (!CanCommit(actor, intentLease) || !IsAliveAndNear(actor, position, 0))
        {
            yield break;
        }

        CharacterStats stats = RequireStats(actor);
        CharacterMoodDeliveryTransactionSnapshot statsBefore =
            stats.CaptureMoodDeliveryTransactionState();
        CharacterPrimitiveSurvivalCompletion.PreparedPrimitiveCompletion prepared = completion.ReserveCompletion(actor, "survival:floor-rest");
        CharacterEventContext eventContext = CharacterPrimitiveSurvivalCompletion.BuildCompletedContext(prepared.Data, prepared.Data.operationId);
        string restResultBefore = stats.LastRestOutcomeResultKey;
        if (stats.HasPendingRestObservation)
        {
            completion.Cancel(prepared);
            throw new InvalidOperationException("floor-rest-prior-recovery-unresolved");
        }
        float hygieneBefore = stats.GetConditionValue(CharacterCondition.HYGIENE);
        try
        {
            // Stage the other reversible effect before the independently durable rest.
            stats.ChangeConditionBeforeOutcomeCommit(CharacterCondition.HYGIENE,
                PrimitiveSurvivalBalanceAuthority.FloorRestHygieneDelta);
            stats.RecoverNeed(
                CharacterCondition.SLEEP,
                PrimitiveSurvivalBalanceAuthority.FloorRestRecovery,
                CharacterNeedRecoverySource.Rest,
                eventContext: eventContext);
            if (stats.IsRestRollbackBlocked || !stats.HasPendingRestObservation
                && (string.IsNullOrWhiteSpace(stats.LastRestOutcomeResultKey)
                    || string.Equals(stats.LastRestOutcomeResultKey, restResultBefore, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Primitive floor-rest recovery did not commit a new rest outcome.");
            }
            stateStore.RecordPrimitiveSurvivalPending(
                CharacterPersistentIdentity.Require(actor),
                completion.CreatePending(
                    actor,
                    prepared,
                    "survival:floor-rest",
                    PrimitiveSurvivalBalanceAuthority.FloorRestRecovery,
                    0,
                    default,
                    default));
        }
        catch
        {
            completion.Cancel(prepared);
            if (stats.HasPendingRestObservation || stats.LastRestOutcomeResultKey != restResultBefore)
            {
                // Retain the actual child and its unfinished parent; never undo a
                // confirmed/uncertain recovery because a later owner step failed.
                CharacterId id = CharacterPersistentIdentity.Require(actor);
                if (!stateStore.TryGetPrimitiveSurvivalPending(id, out _))
                    stateStore.RecordPrimitiveSurvivalPending(id, completion.CreatePending(actor, prepared,
                        "survival:floor-rest", PrimitiveSurvivalBalanceAuthority.FloorRestRecovery,
                        0, default, default));
            }
            else stats.RestoreMoodDeliveryTransactionState(statsBefore, publish: false);
            throw;
        }

        try { stats.PublishCommittedConditionChange(CharacterCondition.HYGIENE, hygieneBefore,
            stats.GetConditionValue(CharacterCondition.HYGIENE), prepared.Data.absoluteDay); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        { Debug.LogError("floor-rest-hygiene-observer-failed:" + exception.Message); }

        if (stats.HasUnconfirmedRestObservation)
        {
            completion.Cancel(prepared);
            yield break;
        }
        completion.CompletePendingParent(actor, prepared.Lease);
    }

    private IEnumerator RunLatrine(
        CharacterActor actor,
        CharacterActionIntentLease intentLease)
    {
        if (!ContinuePrimitiveFallback(
                actor,
                CharacterPrimitiveSurvivalActionKind.Latrine,
                intentLease,
                "before-position-selection"))
        {
            yield break;
        }

        if (!TryGetDesignatedLatrinePosition(actor, out Vector2Int target))
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.Latrine,
                "latrine-position-missing", "No nearby safe primitive latrine cell was available.");
            yield break;
        }

        yield return movement.MoveNear(actor, target, 0);
        if (!ContinuePrimitiveFallback(
                actor,
                CharacterPrimitiveSurvivalActionKind.Latrine,
                intentLease,
                "after-approach"))
        {
            yield break;
        }
        if (!IsAliveAndNear(actor, target, 0)
            || !IsCurrentPrimitiveLatrineTarget(target))
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.Latrine,
                "latrine-approach-failed",
                $"leaseCurrent={CanCommit(actor, intentLease)}; actor={actor?.GetNowXY()}; target={target}; validTarget={IsCurrentPrimitiveLatrineTarget(target)}");
            yield break;
        }
        yield return WaitGameSeconds(
            actor,
            CharacterPrimitiveSurvivalActionKind.Latrine,
            intentLease,
            PrimitiveSurvivalBalanceAuthority.LatrineSeconds);
        if (!CanCommit(actor, intentLease))
        {
            yield break;
        }
        if (!IsAliveAndNear(actor, target, 0)
            || !IsCurrentPrimitiveLatrineTarget(target))
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.Latrine,
                "latrine-target-invalidated",
                $"leaseCurrent={CanCommit(actor, intentLease)}; actor={actor?.GetNowXY()}; target={target}; validTarget={IsCurrentPrimitiveLatrineTarget(target)}");
            yield break;
        }

        CharacterStats stats = RequireStats(actor);
        CharacterMoodDeliveryTransactionSnapshot statsBefore =
            stats.CaptureMoodDeliveryTransactionState();
        CharacterBreakdownWorld.FilthTransactionSnapshot filthBefore =
            world.CaptureFilth();
        CharacterPrimitiveSurvivalCompletion.PreparedPrimitiveCompletion prepared = completion.ReserveCompletion(actor, "survival:primitive-latrine");
        CharacterEventContext eventContext = CharacterPrimitiveSurvivalCompletion.BuildCompletedContext(prepared.Data, prepared.Data.operationId);
        WorldFilthSnapshot waste = default;
        WorldFilthSnapshot stain = default;
        try
        {
            string actorId = CharacterPersistentIdentity.Require(actor).Value;
            waste = world.AddFilth(
                WorldFilthType.Waste,
                target,
                PrimitiveSurvivalBalanceAuthority.LatrineWaste,
                actorId,
                0.35f);
            stain = world.AddFilth(
                WorldFilthType.Stain,
                target,
                PrimitiveSurvivalBalanceAuthority.LatrineStain,
                actorId,
                0.2f);
            stats.RecoverNeed(
                CharacterCondition.EXCRETION,
                PrimitiveSurvivalBalanceAuthority.LatrineRecovery,
                CharacterNeedRecoverySource.Toilet,
                eventContext: eventContext);
            actor.ChangesStat(
                CharacterCondition.HYGIENE,
                PrimitiveSurvivalBalanceAuthority.LatrineHygieneDelta);
            stateStore.RecordPrimitiveSurvivalPending(
                CharacterPersistentIdentity.Require(actor),
                completion.CreatePending(
                    actor,
                    prepared,
                    "survival:primitive-latrine",
                    PrimitiveSurvivalBalanceAuthority.LatrineRecovery,
                    0,
                    waste,
                    stain));
        }
        catch
        {
            completion.Cancel(prepared);
            Exception rollbackFailure = null;
            try
            {
                world.RestoreFilth(filthBefore);
            }
            catch (Exception exception)
            {
                rollbackFailure = exception;
            }
            try
            {
                stats.RestoreMoodDeliveryTransactionState(statsBefore);
            }
            catch (Exception exception)
            {
                rollbackFailure = rollbackFailure == null
                    ? exception
                    : new AggregateException(rollbackFailure, exception);
            }
            if (rollbackFailure != null)
            {
                throw new InvalidOperationException(
                    "Primitive latrine rollback failed after the outcome transaction was rejected.",
                    rollbackFailure);
            }
            throw;
        }

        completion.CompletePendingParent(actor, prepared.Lease);
    }

    private IEnumerator RunBucketWash(
        CharacterActor actor,
        CharacterId actorId,
        CharacterActionIntentLease intentLease)
    {
        if (!ContinuePrimitiveFallback(
                actor,
                CharacterPrimitiveSurvivalActionKind.BucketWash,
                intentLease,
                "before-water-selection"))
        {
            yield break;
        }

        if (!world.TryFindBestAvailableStack(
                actor.GetNowXY(),
                itemId => string.Equals(
                    itemId,
                    PrimitiveSurvivalBalanceAuthority.CleanWaterItemId,
                    StringComparison.Ordinal)
                    ? 0
                    : int.MaxValue,
                out WorldItemStackSnapshot water)
            || water == null
            || water.AvailableQuantity <= 0)
        {
            yield break;
        }

        // Do not hold an unserialized item lease while walking or washing. The
        // water is reserved and committed atomically at the final action frame.
        yield return movement.MoveNear(actor, water.Position, 1);
        if (!ContinuePrimitiveFallback(
                actor,
                CharacterPrimitiveSurvivalActionKind.BucketWash,
                intentLease,
                "after-approach")
            || !IsAliveAndNear(actor, water.Position, 1))
        {
            yield break;
        }
        yield return WaitGameSeconds(
            actor,
            CharacterPrimitiveSurvivalActionKind.BucketWash,
            intentLease,
            PrimitiveSurvivalBalanceAuthority.BucketWashSeconds);
        if (!CanCommit(actor, intentLease)
            || !IsAliveAndNear(actor, water.Position, 1))
        {
            yield break;
        }

        DungeonPhysicalItemSaveData physicalBefore = world.CaptureItems();
        CharacterStats stats = RequireStats(actor);
        CharacterMoodDeliveryTransactionSnapshot statsBefore = stats.CaptureMoodDeliveryTransactionState();
        CharacterPrimitiveSurvivalCompletion.PreparedPrimitiveCompletion prepared = completion.ReserveCompletion(actor, "survival:bucket-wash", water.StackId);
        string operationId = $"primitive:wash:{actorId.Value}:{clock.FrameCount}";
        if (!quantityReservations.TryReserve(operationId, actorId.Value, ItemReservationPurpose.Hygiene,
                $"hygiene:{actorId.Value}",
                new ItemQuantityReservationRequest((ItemStackId)water.StackId, 1, water.ReservationSignature),
                out ItemQuantityLease lease, out _))
        {
            completion.Cancel(prepared);
            yield break;
        }

        bool consumed;
        PrimitiveSurvivalPendingSaveData pending = null;
        try
        {
            consumed = reservedTransfers.TryConsumeReservedQuantity(lease.leaseId, 1, out _);
            if (consumed)
            {
                stats.RecoverNeed(CharacterCondition.HYGIENE, PrimitiveSurvivalBalanceAuthority.BucketWashRecovery,
                    CharacterNeedRecoverySource.Hygiene);
                pending = completion.CreatePending(actor, prepared, "survival:bucket-wash",
                    PrimitiveSurvivalBalanceAuthority.BucketWashRecovery, 1, default, default);
                stateStore.RecordPrimitiveSurvivalPending(actorId, pending);
            }
        }
        catch
        {
            completion.Cancel(prepared);
            world.RestoreItems(physicalBefore);
            stats.RestoreMoodDeliveryTransactionState(statsBefore);
            throw;
        }
        if (!consumed)
        {
            quantityReservations.Release(lease.leaseId, ItemReservationReleaseReason.Cancelled);
            completion.Cancel(prepared);
            yield break;
        }

        DomainFactDelivery delivered = completion.PublishPending(pending, prepared.Lease);
        if (delivered.Phase == DomainFactDeliveryPhase.Rejected)
        {
            // Only a known rejection permits undoing these reversible effects.
            // An unknown ACK retains the frozen result and never consumes water again.
            try
            {
                world.RestoreItems(physicalBefore);
                stats.RestoreMoodDeliveryTransactionState(statsBefore);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            {
                stateStore.BlockPrimitiveSurvivalRecovery(actorId, pending.operationId, exception.Message);
                throw;
            }
            stateStore.ClearPrimitiveSurvivalPending(actorId, pending.operationId);
            throw new InvalidOperationException("Primitive wash observation rejected: " + delivered.Detail);
        }
        if (!CharacterPrimitiveSurvivalCompletion.PublicationConfirmed(pending, delivered))
            throw new InvalidOperationException("Primitive wash completion remains pending: " + delivered.Detail);
        stateStore.ClearPrimitiveSurvivalPending(actorId, pending.operationId);
        completion.CompleteCommittedPending(actor, pending, delivered);
    }

    private IEnumerator WaitGameSeconds(
        CharacterActor actor,
        CharacterPrimitiveSurvivalActionKind kind,
        CharacterActionIntentLease intentLease,
        float seconds)
    {
        float until = clock.Time + Mathf.Max(0f, seconds);
        float nextFacilityRecheckAt = clock.Time;
        while (clock.Time < until && CanCommit(actor, intentLease))
        {
            if (clock.Time >= nextFacilityRecheckAt)
            {
                nextFacilityRecheckAt = clock.Time
                    + AuthoredFacilityRecheckSeconds;
                if (!ContinuePrimitiveFallback(
                        actor,
                        kind,
                        intentLease,
                        "timed-service-recheck"))
                {
                    yield break;
                }
            }

            yield return null;
        }
    }

    private bool ContinuePrimitiveFallback(
        CharacterActor actor,
        CharacterPrimitiveSurvivalActionKind kind,
        in CharacterActionIntentLease intentLease,
        string phase)
    {
        if (!CanCommit(actor, intentLease))
        {
            return false;
        }

        FacilityRole facilityRole = GetFacilityRole(kind);
        CharacterCondition condition = GetCondition(kind);
        if (facilityRole == FacilityRole.None)
        {
            return true;
        }

        bool fallbackAllowed = AIPrimitiveSurvivalAction.CanUsePrimitiveFallback(
                actor,
                facilityRole,
                condition,
                out bool pathSearchPending);
        if (fallbackAllowed || pathSearchPending)
        {
            // Pending blocks admission of a NEW primitive action, but is not
            // proof of an authored replacement for this already-owned lease.
            // Movement changes the path origin; retain ownership until the
            // existing periodic recheck receives a published snapshot. Real
            // source, proximity and transaction checks still guard commitment.
            if (pathSearchPending && phase != "timed-service-recheck")
            {
                events.Publish(new PrimitiveSurvivalChangedEvent(PrimitiveSurvivalTransition.PathSearchPending, actor,
                    kind: kind.ToString(), phase: phase, facilityRole: facilityRole));
            }
            return true;
        }

        events.Publish(new PrimitiveSurvivalChangedEvent(PrimitiveSurvivalTransition.YieldedToFacility, actor,
            kind: kind.ToString(), phase: phase, facilityRole: facilityRole));
        actor.Brain.EndExternallyDrivenAction(
            intentLease,
            clearFailures: false);
        return false;
    }

    private static FacilityRole GetFacilityRole(
        CharacterPrimitiveSurvivalActionKind kind) =>
        kind switch
        {
            CharacterPrimitiveSurvivalActionKind.FieldMeal => FacilityRole.Meal,
            CharacterPrimitiveSurvivalActionKind.FloorRest => FacilityRole.Rest,
            CharacterPrimitiveSurvivalActionKind.Latrine => FacilityRole.Toilet,
            CharacterPrimitiveSurvivalActionKind.BucketWash => FacilityRole.Hygiene,
            _ => FacilityRole.None
        };

    private static bool CanCommit(
        CharacterActor actor,
        in CharacterActionIntentLease intentLease)
    {
        return actor != null
            && !actor.IsDead
            && actor.Brain != null
            && actor.Brain.IsExternalIntentCurrent(intentLease);
    }

    private bool TryGetDesignatedLatrinePosition(
        CharacterActor actor,
        out Vector2Int position)
    {
        position = actor != null ? actor.GetNowXY() : default;
        if (actor == null || !world.TryGetGrid(out Grid grid))
        {
            return false;
        }

        Vector2Int origin = actor.GetNowXY();
        GridCell best = null;
        int bestPriority = int.MaxValue;
        int bestDistance = int.MaxValue;
        int minX = Mathf.Max(0, origin.x - PrimitiveLatrineSearchRadius);
        int maxX = Mathf.Min(grid.width - 1, origin.x + PrimitiveLatrineSearchRadius);
        for (int x = minX; x <= maxX; x++)
        {
            GridCell cell = grid.GetGridCell(new Vector2Int(x, origin.y));
            if (cell == null
                || !grid.IsWalkable(cell.Position))
            {
                continue;
            }
            int priority = world.GetAccidentLocationPriority(grid, cell);
            int distance = Mathf.Abs(cell.Position.x - origin.x);
            if (best != null
                && (priority > bestPriority
                    || (priority == bestPriority && distance > bestDistance)
                    || (priority == bestPriority && distance == bestDistance
                        && cell.Position.x >= best.Position.x)))
            {
                continue;
            }
            best = cell;
            bestPriority = priority;
            bestDistance = distance;
        }

        if (best == null)
        {
            return false;
        }
        position = best.Position;
        return true;
    }

    private bool IsCurrentPrimitiveLatrineTarget(Vector2Int target)
    {
        return world.TryGetGrid(out Grid grid)
            && grid.IsValidGridPos(target)
            && grid.IsWalkable(target);
    }

    private void RecordFailure(
        CharacterActor actor,
        CharacterPrimitiveSurvivalActionKind kind,
        string reasonCode,
        string detail)
    {
        if (actor != null)
            events.Publish(new PrimitiveSurvivalChangedEvent(PrimitiveSurvivalTransition.Failed, actor,
                kind: kind.ToString(), reasonCode: reasonCode, detail: detail));
    }

    private static bool IsAliveAndNear(
        CharacterActor actor,
        Vector2Int target,
        int distance) =>
        actor != null
        && !actor.IsDead
        && Mathf.Abs(actor.GetNowXY().x - target.x)
            + Mathf.Abs(actor.GetNowXY().y - target.y) <= distance;

    private static CharacterStats RequireStats(CharacterActor actor)
    {
        if (actor?.Stats != null)
            return actor.Stats;
        throw new InvalidOperationException(
            "Primitive survival requires an initialized character stats owner.");
    }

    private static string GetLabel(CharacterPrimitiveSurvivalActionKind kind) =>
        kind switch
        {
            CharacterPrimitiveSurvivalActionKind.FieldMeal => "야전식 섭취",
            CharacterPrimitiveSurvivalActionKind.FloorRest => "바닥 취침",
            CharacterPrimitiveSurvivalActionKind.Latrine => "임시 변소 사용",
            CharacterPrimitiveSurvivalActionKind.BucketWash => "물로 간이 세척",
            _ => "초기 생존 행동"
        };

    private static string GetReason(CharacterPrimitiveSurvivalActionKind kind) =>
        kind switch
        {
            CharacterPrimitiveSurvivalActionKind.FieldMeal => "식당 시설 없음",
            CharacterPrimitiveSurvivalActionKind.FloorRest => "침대 시설 없음",
            CharacterPrimitiveSurvivalActionKind.Latrine => "변기 시설 없음",
            CharacterPrimitiveSurvivalActionKind.BucketWash => "세면 시설 없음",
            _ => string.Empty
        };
}
