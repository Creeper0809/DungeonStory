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
    private readonly IGameCalendar calendar;
    private readonly IDomainFactPublisher primitiveFacts;
    private readonly ICharacterPrimitiveSurvivalCoroutineScheduler coroutineScheduler;
    private readonly Dictionary<CharacterId, RunningPrimitiveAction> runningActions = new();
    private readonly HashSet<string> reportedRetiredPublications = new(StringComparer.Ordinal);
    private float lastRetiredRecoveryTime = float.NegativeInfinity;

    private readonly struct RunningPrimitiveAction
    {
        internal RunningPrimitiveAction(
            CharacterPrimitiveSurvivalActionKind kind,
            CharacterActionIntentKind intentKind,
            long leaseEpoch)
        {
            Kind = kind;
            IntentKind = intentKind;
            LeaseEpoch = leaseEpoch;
        }

        internal CharacterPrimitiveSurvivalActionKind Kind { get; }
        internal CharacterActionIntentKind IntentKind { get; }
        internal long LeaseEpoch { get; }
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
        calendar = dependencies.Calendar;
        primitiveFacts = dependencies.PrimitiveFacts;
        coroutineScheduler = dependencies.CoroutineScheduler;
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

    internal void RecoverRetiredPublications()
    {
        if (stateStore.IsRestoreStaging) return;
        float now = clock.Time;
        if (now >= lastRetiredRecoveryTime && now < lastRetiredRecoveryTime + 1f) return;
        lastRetiredRecoveryTime = now;
        foreach (var pending in stateStore.CaptureRetiredPrimitivePublications())
        {
            try
            {
                var rest = pending.GetRestObservation();
                if (pending.rollbackBlocked || rest?.rollbackBlocked == true)
                    throw new InvalidOperationException("primitive-retired-recovery-rollback-blocked");
                if (rest != null)
                {
                    var results = new DomainFactDelivery[1];
                    ((IDomainFactRecoveryPublisher)primitiveFacts).ReplayBatch(new[]
                    {
                        DomainFactReplay.Create(pending.restPublicationKey,
                            new CharacterRestAppliedFact(rest.fact))
                    }, results, out string failure);
                    var child = results[0];
                    if (child.Phase is not (DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
                        || child.OperationKey != pending.restPublicationKey || string.IsNullOrEmpty(child.PublicationId)
                        || child.Fingerprint.Length != 64)
                        throw new InvalidOperationException("primitive-retired-rest-child-pending:" + failure);
                }
                DomainFactDelivery delivered = PublishPending(pending);
                if (!PublicationConfirmed(pending, delivered))
                    throw new InvalidOperationException("primitive-retired-publication-pending:" + delivered.Detail);
                stateStore.ClearRetiredPrimitivePublication(pending.operationId, pending.publicationKey);
                reportedRetiredPublications.Remove(pending.operationId);
                // The actor lifetime has ended: publish the applied facts only.
                // No physical effects, mood or actor-targeted notifications are repeated.
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            {
                if (reportedRetiredPublications.Add(pending.operationId))
                    Debug.LogError(exception.Message + ":" + pending.operationId);
            }
        }
    }

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
        if (!stateStore.TryGetPrimitiveSurvivalPending(
                actorId,
                out PrimitiveSurvivalPendingSaveData pending))
        {
            return false;
        }

        if (pending.actionId == "survival:floor-rest" && !RequireStats(actor).TryCompleteRestObservation())
        {
            status = "이전 휴식 적용 사실 전달 대기";
            return false;
        }
        ValidatePendingChild(actor, pending);
        CompletePendingParent(actor);
        status = "이전 생존 행동 결과 복구 완료";
        return true;
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

        runningActions[actorId] = new RunningPrimitiveAction(
            kind,
            intentKind,
            intentLease.Epoch);
        CharacterCondition condition = GetCondition(kind);
        float needValue = actor.Stats != null
            && actor.Stats.TryGetConditionValue(condition, out float currentNeed)
                ? currentNeed
                : 0f;
        events.Publish(new CharacterPrimitiveSurvivalStartedEvent(
            actorId,
            GetActionId(kind),
            intentKind >= CharacterActionIntentKind.EmergencyNeed,
            needValue));
        events.Publish(new PrimitiveSurvivalChangedEvent(PrimitiveSurvivalTransition.Started, actor,
            kind: kind.ToString(), intentKind: intentKind, epoch: intentLease.Epoch, position: actor.GetNowXY()));
        coroutineScheduler.Start(actor, Run(actor, actorId, kind, intentLease));
        status = GetLabel(kind);
        return true;
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
        lastRetiredRecoveryTime = float.NegativeInfinity;
        reportedRetiredPublications.Clear();
    }

    private IEnumerator Run(
        CharacterActor actor,
        CharacterId actorId,
        CharacterPrimitiveSurvivalActionKind kind,
        CharacterActionIntentLease intentLease)
    {
        try
        {
            switch (kind)
            {
                case CharacterPrimitiveSurvivalActionKind.FieldMeal:
                    yield return RunFieldMeal(actor, intentLease);
                    break;
                case CharacterPrimitiveSurvivalActionKind.FloorRest:
                    yield return RunFloorRest(actor, intentLease);
                    break;
                case CharacterPrimitiveSurvivalActionKind.Latrine:
                    yield return RunLatrine(actor, intentLease);
                    break;
                case CharacterPrimitiveSurvivalActionKind.BucketWash:
                    yield return RunBucketWash(actor, actorId, intentLease);
                    break;
            }
        }
        finally
        {
            if (runningActions.TryGetValue(
                    actorId,
                    out RunningPrimitiveAction running)
                && running.LeaseEpoch == intentLease.Epoch)
            {
                runningActions.Remove(actorId);
            }
            if (actor?.Brain?.IsExternalIntentCurrent(intentLease) == true)
            {
                actor.Brain.EndExternallyDrivenAction(
                    intentLease,
                    clearFailures: true);
            }
        }
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
            PublishCompletedEvent(
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
        PreparedPrimitiveCompletion prepared = ReserveCompletion(actor, "survival:floor-rest");
        CharacterEventContext eventContext = BuildCompletedContext(prepared.Data, prepared.Data.operationId);
        string restResultBefore = stats.LastRestOutcomeResultKey;
        if (stats.HasPendingRestObservation)
        {
            primitiveFacts.Cancel(prepared.Lease);
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
                CreatePending(
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
            primitiveFacts.Cancel(prepared.Lease);
            if (stats.HasPendingRestObservation || stats.LastRestOutcomeResultKey != restResultBefore)
            {
                // Retain the actual child and its unfinished parent; never undo a
                // confirmed/uncertain recovery because a later owner step failed.
                CharacterId id = CharacterPersistentIdentity.Require(actor);
                if (!stateStore.TryGetPrimitiveSurvivalPending(id, out _))
                    stateStore.RecordPrimitiveSurvivalPending(id, CreatePending(actor, prepared,
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
            primitiveFacts.Cancel(prepared.Lease);
            yield break;
        }
        CompletePendingParent(actor, prepared.Lease);
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
        PreparedPrimitiveCompletion prepared = ReserveCompletion(actor, "survival:primitive-latrine");
        CharacterEventContext eventContext = BuildCompletedContext(prepared.Data, prepared.Data.operationId);
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
                CreatePending(
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
            primitiveFacts.Cancel(prepared.Lease);
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

        CompletePendingParent(actor, prepared.Lease);
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
        PreparedPrimitiveCompletion prepared = ReserveCompletion(actor, "survival:bucket-wash", water.StackId);
        string operationId = $"primitive:wash:{actorId.Value}:{clock.FrameCount}";
        if (!quantityReservations.TryReserve(operationId, actorId.Value, ItemReservationPurpose.Hygiene,
                $"hygiene:{actorId.Value}",
                new ItemQuantityReservationRequest((ItemStackId)water.StackId, 1, water.ReservationSignature),
                out ItemQuantityLease lease, out _))
        {
            primitiveFacts.Cancel(prepared.Lease);
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
                pending = CreatePending(actor, prepared, "survival:bucket-wash",
                    PrimitiveSurvivalBalanceAuthority.BucketWashRecovery, 1, default, default);
                stateStore.RecordPrimitiveSurvivalPending(actorId, pending);
            }
        }
        catch
        {
            primitiveFacts.Cancel(prepared.Lease);
            world.RestoreItems(physicalBefore);
            stats.RestoreMoodDeliveryTransactionState(statsBefore);
            throw;
        }
        if (!consumed)
        {
            quantityReservations.Release(lease.leaseId, ItemReservationReleaseReason.Cancelled);
            primitiveFacts.Cancel(prepared.Lease);
            yield break;
        }

        DomainFactDelivery delivered = PublishPending(pending, prepared.Lease);
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
        if (!PublicationConfirmed(pending, delivered))
            throw new InvalidOperationException("Primitive wash completion remains pending: " + delivered.Detail);
        stateStore.ClearPrimitiveSurvivalPending(actorId, pending.operationId);
        CompleteCommittedPending(actor, pending, delivered);
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

    private sealed class PreparedPrimitiveCompletion
    {
        internal PrimitiveSurvivalPendingSaveData Data;
        internal DomainFactLease Lease;
    }

    private PreparedPrimitiveCompletion ReserveCompletion(CharacterActor actor, string actionId, string waterStackId = "")
    {
        var data = new PrimitiveSurvivalPendingSaveData
        {
            operationId = "primitive:" + Guid.NewGuid().ToString("N"),
            characterId = CharacterPersistentIdentity.Require(actor).Value,
            characterName = actor.BuildingDisplayName,
            actionId = actionId, absoluteDay = Math.Max(1, calendar.Day),
            recovery = actionId == "survival:floor-rest" ? PrimitiveSurvivalBalanceAuthority.FloorRestRecovery
                : actionId == "survival:primitive-latrine" ? PrimitiveSurvivalBalanceAuthority.LatrineRecovery
                : PrimitiveSurvivalBalanceAuthority.BucketWashRecovery,
            physicalItemCount = actionId == "survival:bucket-wash" ? 1 : 0,
            waterStackId = waterStackId
        };
        var proposed = new PrimitiveSurvivalCompletedFact(data, reservationOnly: true);
        if (!primitiveFacts.TryReserve(proposed, out DomainFactLease lease, out string failure))
            throw new InvalidOperationException("Primitive survival observation unavailable: " + failure);
        string key = lease.PublicationKey;
        if (!DomainFactValueSyntax.IsText(key, 4096, false))
        {
            primitiveFacts.Cancel(lease);
            throw new InvalidOperationException("Primitive survival publication has no durable identity.");
        }
        data.publicationKey = key;
        return new PreparedPrimitiveCompletion { Data = data, Lease = lease };
    }

    private void CompletePendingParent(CharacterActor actor, DomainFactLease lease = default)
    {
        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        if (!stateStore.TryGetPrimitiveSurvivalPending(actorId, out PrimitiveSurvivalPendingSaveData pending))
            throw new InvalidOperationException("Primitive survival applied child has no pending completion.");
        DomainFactDelivery delivered;
        try
        {
            ValidatePendingChild(actor, pending);
            delivered = PublishPending(pending, lease);
        }
        finally { if (lease.IsValid) primitiveFacts.Cancel(lease); }
        if (!PublicationConfirmed(pending, delivered))
            throw new InvalidOperationException("Primitive survival completion remains pending: " + delivered.Detail);
        stateStore.ClearPrimitiveSurvivalPending(actorId, pending.operationId);
        CompleteCommittedPending(actor, pending, delivered);
    }

    private DomainFactDelivery PublishPending(PrimitiveSurvivalPendingSaveData pending, DomainFactLease lease = default)
    {
        try
        {
            var fact = new PrimitiveSurvivalCompletedFact(pending);
            if (lease.IsValid) return primitiveFacts.Publish(lease, fact);
            var results = new DomainFactDelivery[1];
            return ((IDomainFactRecoveryPublisher)primitiveFacts).ReplayBatch(
                new[] { DomainFactReplay.Create(pending.publicationKey, fact) }, results, out string failure)
                ? results[0] : DomainFactDelivery.Reject(failure);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        { return DomainFactDelivery.Uncertain("primitive-survival-publication-unconfirmed:" + exception.Message); }
    }

    private static bool PublicationConfirmed(PrimitiveSurvivalPendingSaveData pending, DomainFactDelivery delivery) =>
        delivery.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery
        && delivery.OperationKey == pending.publicationKey && !string.IsNullOrEmpty(delivery.PublicationId)
        && delivery.Fingerprint.Length == 64;

    private PrimitiveSurvivalPendingSaveData CreatePending(CharacterActor actor, PreparedPrimitiveCompletion prepared,
        string actionId, float recovery, int physicalItemCount, in WorldFilthSnapshot waste, in WorldFilthSnapshot stain)
    {
        PrimitiveSurvivalPendingSaveData data = prepared.Data.Clone();
        if (data.actionId != actionId || data.recovery != recovery || data.physicalItemCount != physicalItemCount)
            throw new InvalidOperationException("Primitive survival applied action differs from its reservation.");
        CharacterStats stats = RequireStats(actor);
        data.sleepAfter = stats.GetConditionValue(CharacterCondition.SLEEP, 0f);
        data.hygieneAfter = stats.GetConditionValue(CharacterCondition.HYGIENE, 0f);
        data.excretionAfter = stats.GetConditionValue(CharacterCondition.EXCRETION, 0f);
        if (actionId == "survival:floor-rest")
        {
            var rest = stats.CaptureRestObservation();
            if (rest != null) data.restObservations.Add(rest);
            data.restPublicationKey = rest?.publicationKey ?? stats.LastRestOutcomeResultKey;
        }
        if (actionId == "survival:primitive-latrine")
        {
            data.targetGridX = waste.Position.x; data.targetGridY = waste.Position.y;
            data.wasteFilthId = waste.FilthId; data.wasteAmountAfter = waste.Amount;
            data.stainFilthId = stain.FilthId; data.stainAmountAfter = stain.Amount;
        }
        return data;
    }

    private void ValidatePendingChild(
        CharacterActor actor,
        PrimitiveSurvivalPendingSaveData pending)
    {
        if (pending.rollbackBlocked)
            throw new InvalidOperationException("primitive-survival-rollback-blocked:" + pending.rollbackFailure);
        if (pending.actionId == "survival:bucket-wash")
        {
            PrimitiveSurvivalCompletedFact.ValidateSaved(pending, CharacterPersistentIdentity.Require(actor).Value);
            return;
        }
        bool floorRest = string.Equals(
            pending.actionId,
            "survival:floor-rest",
            StringComparison.Ordinal);
        bool latrine = string.Equals(
            pending.actionId,
            "survival:primitive-latrine",
            StringComparison.Ordinal);
        float expectedRecovery = floorRest
            ? PrimitiveSurvivalBalanceAuthority.FloorRestRecovery
            : latrine
                ? PrimitiveSurvivalBalanceAuthority.LatrineRecovery
                : float.NaN;
        if ((!floorRest && !latrine)
            || !Mathf.Approximately(pending.recovery, expectedRecovery)
            || pending.physicalItemCount != 0)
        {
            throw new InvalidOperationException(
                "Primitive-survival pending receipt contains an unsupported child contract.");
        }
        if (floorRest)
        {
            if (RequireStats(actor).IsRestRollbackBlocked)
                throw new InvalidOperationException("floor-rest-child-rollback-blocked");
            return;
        }

        Vector2Int target = new(pending.targetGridX, pending.targetGridY);
        string actorId = CharacterPersistentIdentity.Require(actor).Value;
        IReadOnlyList<WorldFilthSnapshot> atTarget =
            world.GetFilthAt(target) ?? Array.Empty<WorldFilthSnapshot>();
        RequireFilthReceipt(
            atTarget,
            pending.wasteFilthId,
            WorldFilthType.Waste,
            pending.wasteAmountAfter,
            target,
            actorId);
        RequireFilthReceipt(
            atTarget,
            pending.stainFilthId,
            WorldFilthType.Stain,
            pending.stainAmountAfter,
            target,
            actorId);
    }

    private static void RequireFilthReceipt(
        IReadOnlyList<WorldFilthSnapshot> filth,
        string expectedId,
        WorldFilthType expectedType,
        float expectedAmount,
        Vector2Int expectedPosition,
        string expectedActorId)
    {
        WorldFilthSnapshot receipt = filth.FirstOrDefault(value =>
            string.Equals(value.FilthId, expectedId, StringComparison.Ordinal));
        if (!string.Equals(receipt.FilthId, expectedId, StringComparison.Ordinal)
            || receipt.Type != expectedType
            || receipt.Position != expectedPosition
            || !string.Equals(
                receipt.SourceCharacterId,
                expectedActorId,
                StringComparison.Ordinal)
            || !Mathf.Approximately(receipt.Amount, expectedAmount))
        {
            throw new InvalidOperationException(
                "Primitive-latrine recovery could not verify its durable filth child: "
                + expectedId);
        }
    }

    private void CompleteCommittedPending(CharacterActor actor, PrimitiveSurvivalPendingSaveData pending,
        in DomainFactDelivery delivered)
    {
        CharacterEventContext context = BuildCompletedContext(pending, delivered.PublicationId);
        bool floor = pending.actionId == "survival:floor-rest";
        bool latrine = pending.actionId == "survival:primitive-latrine";
        // Effects are settled and pending cleared. Attempt each remaining reaction
        // once, retaining failures rather than replaying physical effects.
        List<Exception> failures = null;
        void Publish(Action callback)
        {
            try { callback(); }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            { (failures ??= new List<Exception>()).Add(exception); }
        }
        if (floor || latrine)
            Publish(() => actor.ApplyMoodFactorFromEvent(pending.actionId,
                floor ? "맨바닥에서 잠" : "임시 변소를 사용함",
                floor ? PrimitiveSurvivalBalanceAuthority.FloorRestMoodDelta : PrimitiveSurvivalBalanceAuthority.LatrineMoodDelta,
                180f, 1, pending.absoluteDay, context));
        string publicationId = delivered.PublicationId;
        Publish(() => events.Publish(new PrimitiveSurvivalChangedEvent(PrimitiveSurvivalTransition.Completed,
            actor, pending: pending, publicationId: publicationId)));
        Publish(() => PublishCompletedEvent(actor, pending.actionId, pending.recovery, pending.physicalItemCount));
        if (failures != null) throw new AggregateException("Primitive survival completion reactions failed.", failures);
    }

    private void PublishCompletedEvent(CharacterActor actor, string actionId, float recovery, int physicalItemCount) =>
        events.Publish(new CharacterPrimitiveSurvivalCompletedEvent(
            CharacterPersistentIdentity.Require(actor), actionId, recovery, physicalItemCount));

    private static CharacterEventContext BuildCompletedContext(PrimitiveSurvivalPendingSaveData data, string eventId) => new()
    {
        eventInstanceId = eventId, chainId = eventId, actorId = data.characterId,
        actorDisplayName = data.characterName, resultDetail = "생존 행동 완료", occurredDay = data.absoluteDay
    };

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
