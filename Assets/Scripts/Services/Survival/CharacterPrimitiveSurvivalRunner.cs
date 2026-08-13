using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

internal enum CharacterPrimitiveSurvivalActionKind
{
    FieldMeal,
    FloorRest,
    Latrine,
    BucketWash
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
    private const float FieldMealSeconds = 4f;
    private const float FloorRestSeconds = 60f;
    private const float LatrineSeconds = 6f;
    private const float BucketWashSeconds = 6f;
    private const string CleanWaterItemId = "resource:clean-water";

    private readonly CharacterBreakdownWorld world;
    private readonly CharacterEmergencyMovement movement;
    private readonly IGameClock clock;
    private readonly IFieldMealConsumptionCommand fieldMeals;
    private readonly IGameEventBus events;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly IReservedItemTransferService reservedTransfers;
    private readonly Dictionary<CharacterId, RunningPrimitiveAction> runningActions = new();

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
        IGameClock clock,
        IGameEventBus events,
        CharacterPrimitiveSurvivalDependencies dependencies)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        dependencies = dependencies
            ?? throw new ArgumentNullException(nameof(dependencies));
        fieldMeals = dependencies.FieldMeals;
        quantityReservations = dependencies.QuantityReservations;
        reservedTransfers = dependencies.ReservedTransfers;
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
                itemId => string.Equals(itemId, CleanWaterItemId, StringComparison.Ordinal)
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
        if (runningActions.TryGetValue(
                actorId,
                out RunningPrimitiveAction running)
            && (IsEmergency(actor, kind)
                    ? CharacterActionIntentKind.EmergencyNeed
                    : CharacterActionIntentKind.RoutineNeed)
                <= running.IntentKind)
        {
            status = GetLabel(running.Kind) + " 진행 중";
            return true;
        }

        CharacterActionIntentKind intentKind =
            IsEmergency(actor, kind)
                ? CharacterActionIntentKind.EmergencyNeed
                : CharacterActionIntentKind.RoutineNeed;
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
            intentKind == CharacterActionIntentKind.EmergencyNeed,
            needValue));
        actor.AddActivity(CharacterActivityEvent.InternalAi(
            CharacterActivityOutcomes.Started,
            "primitive-survival-start",
            $"Primitive survival started: kind={kind}; intent={intentKind}; epoch={intentLease.Epoch}; position={actor.GetNowXY()}"));
        actor.StartCoroutine(Run(actor, actorId, kind, intentLease));
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

    internal void Reset() => runningActions.Clear();

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
            actor?.Brain?.EndExternallyDrivenAction(
                intentLease,
                clearFailures: true);
        }
    }

    private static bool IsEmergency(
        CharacterActor actor,
        CharacterPrimitiveSurvivalActionKind kind)
    {
        return CharacterNeedAiThresholds.IsEmergency(actor, GetCondition(kind));
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
        CharacterActionIntentLease intentLease)
    {
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
        if (!CanCommit(actor, intentLease) || !IsAliveAndNear(actor, position, 1))
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.FieldMeal,
                "field-meal-approach-failed",
                $"leaseCurrent={CanCommit(actor, intentLease)}; actor={actor?.GetNowXY()}; target={position}");
            yield break;
        }
        yield return WaitGameSeconds(actor, intentLease, FieldMealSeconds);
        if (CanCommit(actor, intentLease)
            && IsAliveAndNear(actor, position, 1)
            && fieldMeals.TryConsumeFieldMeal(actor, stackId, out MealConsumptionResult result)
            && result.Success)
        {
            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Health,
                CharacterActivityOutcomes.Completed,
                $"{result.DisplayName}을(를) 야전식으로 먹음",
                actionId: "survival:field-meal",
                targetId: stackId.Value,
                reasonCode: "no-meal-facility",
                value: result.Nutrition,
                bubbleEligible: true));
            PublishCompleted(actor, "survival:field-meal", result.Nutrition, 1);
        }
        else
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.FieldMeal,
                "field-meal-commit-failed",
                $"leaseCurrent={CanCommit(actor, intentLease)}; actor={actor?.GetNowXY()}; target={position}; stack={stackId.Value}");
        }
    }

    private IEnumerator RunFloorRest(
        CharacterActor actor,
        CharacterActionIntentLease intentLease)
    {
        Vector2Int position = actor.GetNowXY();
        yield return WaitGameSeconds(actor, intentLease, FloorRestSeconds);
        if (!CanCommit(actor, intentLease) || !IsAliveAndNear(actor, position, 0))
        {
            yield break;
        }

        actor.Stats?.RecoverNeed(
            CharacterCondition.SLEEP,
            55f,
            CharacterNeedRecoverySource.Rest);
        actor.ChangesStat(CharacterCondition.HYGIENE, -4f);
        actor.ApplyMoodFactor(
            "survival:floor-rest",
            "맨바닥에서 잠",
            -3f,
            180f,
            1);
        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Completed,
            "맨바닥에서 잠을 청함",
            actionId: "survival:floor-rest",
            reasonCode: "no-rest-facility",
            value: 55f,
            bubbleEligible: true));
        PublishCompleted(actor, "survival:floor-rest", 55f, 0);
    }

    private IEnumerator RunLatrine(
        CharacterActor actor,
        CharacterActionIntentLease intentLease)
    {
        if (!TryGetDesignatedLatrinePosition(actor, out Vector2Int target))
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.Latrine,
                "latrine-position-missing", "No nearby safe primitive latrine cell was available.");
            yield break;
        }

        yield return movement.MoveNear(actor, target, 0);
        if (!CanCommit(actor, intentLease) || !IsAliveAndNear(actor, target, 0))
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.Latrine,
                "latrine-approach-failed",
                $"leaseCurrent={CanCommit(actor, intentLease)}; actor={actor?.GetNowXY()}; target={target}");
            yield break;
        }
        yield return WaitGameSeconds(actor, intentLease, LatrineSeconds);
        if (!CanCommit(actor, intentLease) || !IsAliveAndNear(actor, target, 0))
        {
            RecordFailure(actor, CharacterPrimitiveSurvivalActionKind.Latrine,
                "latrine-commit-failed",
                $"leaseCurrent={CanCommit(actor, intentLease)}; actor={actor?.GetNowXY()}; target={target}");
            yield break;
        }

        world.AddFilth(
            WorldFilthType.Waste,
            target,
            8f,
            CharacterPersistentIdentity.Require(actor).Value,
            0.35f);
        world.AddFilth(
            WorldFilthType.Stain,
            target,
            2f,
            CharacterPersistentIdentity.Require(actor).Value,
            0.2f);
        actor.Stats?.RecoverNeed(
            CharacterCondition.EXCRETION,
            85f,
            CharacterNeedRecoverySource.Toilet);
        actor.ChangesStat(CharacterCondition.HYGIENE, -8f);
        actor.ApplyMoodFactor(
            "survival:primitive-latrine",
            "임시 변소를 사용함",
            -2f,
            180f,
            1);
        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Completed,
            "지정된 임시 변소를 사용함",
            actionId: "survival:primitive-latrine",
            reasonCode: "no-toilet-facility",
            value: 85f,
            bubbleEligible: true));
        PublishCompleted(actor, "survival:primitive-latrine", 85f, 0);
    }

    private IEnumerator RunBucketWash(
        CharacterActor actor,
        CharacterId actorId,
        CharacterActionIntentLease intentLease)
    {
        if (!world.TryFindBestAvailableStack(
                actor.GetNowXY(),
                itemId => string.Equals(itemId, CleanWaterItemId, StringComparison.Ordinal)
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
        if (!CanCommit(actor, intentLease)
            || !IsAliveAndNear(actor, water.Position, 1))
        {
            yield break;
        }
        yield return WaitGameSeconds(actor, intentLease, BucketWashSeconds);
        if (!CanCommit(actor, intentLease)
            || !IsAliveAndNear(actor, water.Position, 1))
        {
            yield break;
        }

        string operationId = $"primitive:wash:{actorId.Value}:{clock.FrameCount}";
        if (!quantityReservations.TryReserve(
                operationId,
                actorId.Value,
                ItemReservationPurpose.Hygiene,
                $"hygiene:{actorId.Value}",
                new ItemQuantityReservationRequest(
                    (ItemStackId)water.StackId,
                    1,
                    water.StackSignature),
                out ItemQuantityLease lease,
                out _))
        {
            yield break;
        }

        bool consumed = reservedTransfers.TryConsumeReservedQuantity(
            lease.leaseId,
            1,
            out _);
        if (!consumed)
        {
            quantityReservations.Release(
                lease.leaseId,
                ItemReservationReleaseReason.Cancelled);
            yield break;
        }

        actor.Stats?.RecoverNeed(
            CharacterCondition.HYGIENE,
            50f,
            CharacterNeedRecoverySource.Hygiene);
        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Completed,
            "깨끗한 물 한 단위로 몸을 씻음",
            actionId: "survival:bucket-wash",
            targetId: water.StackId,
            reasonCode: "no-hygiene-facility",
            value: 50f,
            bubbleEligible: true));
        PublishCompleted(actor, "survival:bucket-wash", 50f, 1);
    }

    private IEnumerator WaitGameSeconds(
        CharacterActor actor,
        CharacterActionIntentLease intentLease,
        float seconds)
    {
        float until = clock.Time + Mathf.Max(0f, seconds);
        while (clock.Time < until && CanCommit(actor, intentLease))
        {
            yield return null;
        }
    }

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

    private static void RecordFailure(
        CharacterActor actor,
        CharacterPrimitiveSurvivalActionKind kind,
        string reasonCode,
        string detail)
    {
        actor?.AddActivity(CharacterActivityEvent.InternalAi(
            CharacterActivityOutcomes.Failed,
            reasonCode,
            $"Primitive survival failed: kind={kind}; {detail}"));
    }

    private static bool IsAliveAndNear(
        CharacterActor actor,
        Vector2Int target,
        int distance) =>
        actor != null
        && !actor.IsDead
        && Mathf.Abs(actor.GetNowXY().x - target.x)
            + Mathf.Abs(actor.GetNowXY().y - target.y) <= distance;

    private void PublishCompleted(
        CharacterActor actor,
        string actionId,
        float recovery,
        int physicalItemCount)
    {
        events.Publish(new CharacterPrimitiveSurvivalCompletedEvent(
            CharacterPersistentIdentity.Require(actor),
            actionId,
            recovery,
            physicalItemCount));
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
