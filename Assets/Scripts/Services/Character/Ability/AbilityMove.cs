using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using static GridMovePathRules;
public class AbilityMove : CharacterAbility
{
    private float moveSpeed;
    private CharacterSpawner spawner;
    private ICharacterSpawnerProvider spawnerProvider;
    private ICharacterAiSchedulingService aiSchedulingService;
    private IGridPathSearchBroker pathSearchBroker;
    private IDefenseEngagementRuntime defenseEngagementRuntime;
    private IGameClock gameClock;
    private IRandomStream movementRandom;
    private CharacterIdleWanderPlanner idleWanderPlanner;
    private AbilityMoveTraversalGuard traversalGuard;
    private Coroutine enterDungeonRoutine;
    private Coroutine activeActionMovementRoutine;
    private Vector2Int? activeManualMoveDestination;
    private Vector2Int? activeSystemMoveDestination;
    private DoorAccessOverrideKind activeSystemMoveOverride;
    private int movementOperationVersion;

    public bool LastGridMoveWasBlocked { get; private set; }
    public GridMoveFailureReason LastGridMoveFailureReason { get; private set; }
    public bool IsSystemMoveInProgress => activeActionMovementRoutine != null
        && activeSystemMoveDestination.HasValue;

    public bool IsSystemMoveInProgressTo(Vector2Int destination)
    {
        return IsSystemMoveInProgress
            && activeSystemMoveDestination.Value == destination;
    }
    public void MarkGridMoveFailure(GridMoveFailureReason reason)
    {
        if (reason == GridMoveFailureReason.None) return;
        LastGridMoveWasBlocked = false;
        LastGridMoveFailureReason = reason;
    }

    [Inject]
    public void ConstructAbilityMove(
        ICharacterSpawnerProvider spawnerProvider,
        ICharacterAiSchedulingService aiSchedulingService,
        IGridPathSearchBroker pathSearchBroker,
        IRandomStreamProvider randomStreamProvider,
        IGameClock gameClock,
        IDefenseEngagementRuntime defenseEngagementRuntime)
    {
        this.spawnerProvider = spawnerProvider
            ?? throw new ArgumentNullException(nameof(spawnerProvider));
        this.aiSchedulingService = aiSchedulingService
            ?? throw new ArgumentNullException(nameof(aiSchedulingService));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        movementRandom = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("character-movement");
        idleWanderPlanner = new CharacterIdleWanderPlanner(
            pathSearchBroker,
            movementRandom);
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.defenseEngagementRuntime = defenseEngagementRuntime;
        TryResolveSpawner();
    }

    [Inject]
    public void ConstructDoorAccessQuery(IDoorAccessQuery doorAccessQuery)
    {
        traversalGuard = new AbilityMoveTraversalGuard(
            doorAccessQuery,
            defenseEngagementRuntime,
            () => activeManualMoveDestination.HasValue
                ? DoorAccessOverrideKind.DirectCommand
                : activeSystemMoveDestination.HasValue
                    ? activeSystemMoveOverride
                    : DoorAccessOverrideKind.None);
    }

    public override void Initializtion(CharacterSO data)
    {
        base.Initializtion(data);
        moveSpeed = actor != null
            ? actor.GetMoveSpeed()
            : data != null
                ? data.moveSpeed
                : 1f;
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
    }

    public IEnumerator MoveByPath(Queue<GridMoveStep> path, AIAction expectedAction = null)
    {
        int operationVersion = BeginMovementOperation();
        yield return MoveByPathInternal(path, expectedAction, operationVersion);
    }

    private IEnumerator MoveByPathInternal(
        Queue<GridMoveStep> path,
        AIAction expectedAction,
        int operationVersion)
    {
        LastGridMoveWasBlocked = false;
        LastGridMoveFailureReason = GridMoveFailureReason.None;
        if (path == null)
        {
            LastGridMoveFailureReason = GridMoveFailureReason.MissingPath;
            yield break;
        }

        bool hasExpectedDestination =
            TryGetPathDestination(path, out Vector2Int expectedDestination);
        Vector3 pathStartPosition = transform.position;
        float completedPathDistance = 0f;
        int staleReplanAttempts = 0;
        while (path.Count > 0)
        {
            if (IsMovementOperationCancelled(
                    expectedAction,
                    operationVersion))
            {
                LastGridMoveFailureReason = GridMoveFailureReason.Cancelled;
                yield break;
            }

            GridMoveStep step = path.Dequeue();
            if (!step.IsValid) continue;

            if (!AbilityMoveTraversalGuard.IsAtStepStart(
                    grid,
                    transform.position,
                    step))
            {
                if (staleReplanAttempts < 1
                    && TryReplanCurrentActionPath(expectedAction, out Queue<GridMoveStep> rebuiltPath))
                {
                    staleReplanAttempts++;
                    path = rebuiltPath;
                    continue;
                }

                if (expectedAction != null && expectedAction.planKind == AIActionPlanKind.DestinationOnly)
                {
                    LastGridMoveFailureReason =
                        GridMoveFailureReason.StaleStepStart;
                    yield break;
                }

                SetGridMoveBlocked(GridMoveFailureReason.StaleStepStart);
                yield break;
            }

            if (traversalGuard.TryGetWalkStepBlockReason(
                    actor,
                    grid,
                    step,
                    out GridMoveFailureReason initialBlockReason))
            {
                SetGridMoveBlocked(initialBlockReason);
                yield break;
            }

            RefreshCurrentActionReservation();
            if (step.MoveType != GridMoveType.Walk)
            {
                yield return MoveByStepInternal(
                    step,
                    expectedAction,
                    operationVersion);
            }
            else
            {
                LastGridMoveWasBlocked = false;
                if (grid == null)
                {
                    LastGridMoveFailureReason =
                        GridMoveFailureReason.GridUnavailable;
                    yield break;
                }

                Vector2Int destination = step.To;
                Vector3 startPosition = transform.position;
                if (grid.IsMovementBlockedByWall(destination)
                    || !traversalGuard.CanTraverseDoor(
                        actor,
                        grid,
                        destination,
                        out _)
                    || (defenseEngagementRuntime?.IsCellReservedForOther(
                        actor,
                        destination) ?? false))
                {
                    SetGridMoveBlocked(
                        traversalGuard.GetCellBlockReason(
                            actor,
                            grid,
                            destination));
                    yield break;
                }

                int observedGridVersion = grid.TraversalVersion;
                Vector3 endPosition = grid.GetWorldPos(destination);
                float terrainSpeedMultiplier = Mathf.Max(
                    0.01f,
                    grid.GetGridCell(destination)
                        ?.TerrainMoveSpeedMultiplier ?? 1f);
                float distance = Vector3.Distance(startPosition, endPosition);
                float totalSpeed = CharacterMovementKinematics.GetMoveSpeed(
                    actor,
                    moveSpeed) * terrainSpeedMultiplier;
                if (totalSpeed <= 0f)
                {
                    LastGridMoveFailureReason =
                        GridMoveFailureReason.InvalidSpeed;
                    yield break;
                }

                CharacterMovementKinematics.UpdateFacing(
                    actor,
                    endPosition.x - startPosition.x);
                float duration = distance / totalSpeed;
                float timer = 0f;
                while (timer < duration)
                {
                    if (TryRollbackForChangedGridBlock(
                            destination,
                            ref observedGridVersion,
                            startPosition)
                        || IsMovementOperationCancelled(
                            expectedAction,
                            operationVersion))
                    {
                        if (!LastGridMoveWasBlocked)
                        {
                            LastGridMoveFailureReason =
                                GridMoveFailureReason.Cancelled;
                        }
                        yield break;
                    }

                    Vector3 nextPosition = Vector3.Lerp(
                        startPosition,
                        endPosition,
                        timer / duration);
                    CharacterMovementKinematics.UpdateFacing(
                        actor,
                        nextPosition.x - transform.position.x);
                    transform.position = nextPosition;
                    timer += gameClock.DeltaTime;

                    int frameStride = RequireAiSchedulingService()
                        .GetMovementFrameStride(actor);
                    for (int frame = 1;
                         frame < frameStride && timer < duration;
                         frame++)
                    {
                        yield return null;
                        if (IsMovementOperationCancelled(
                                expectedAction,
                                operationVersion)
                            || TryRollbackForChangedGridBlock(
                                destination,
                                ref observedGridVersion,
                                startPosition))
                        {
                            if (!LastGridMoveWasBlocked)
                            {
                                LastGridMoveFailureReason =
                                    GridMoveFailureReason.Cancelled;
                            }
                            yield break;
                        }

                        timer += gameClock.DeltaTime;
                    }

                    yield return null;
                }

                if (TryRollbackForChangedGridBlock(
                    destination,
                    ref observedGridVersion,
                    startPosition))
                {
                    yield break;
                }

                CharacterMovementKinematics.UpdateFacing(
                    actor,
                    endPosition.x - transform.position.x);
                transform.position = endPosition;
                completedPathDistance += distance;
            }

            if (LastGridMoveWasBlocked)
            {
                yield break;
            }

            if (traversalGuard.TryGetWalkStepBlockReason(
                    actor,
                    grid,
                    step,
                    out GridMoveFailureReason completedBlockReason))
            {
                SetGridMoveBlocked(completedBlockReason);
                yield break;
            }
        }

        if (IsMovementOperationCancelled(
                expectedAction,
                operationVersion))
        {
            LastGridMoveFailureReason = GridMoveFailureReason.Cancelled;
            yield break;
        }

        if (hasExpectedDestination
            && !LastGridMoveWasBlocked
            && (grid == null
                || grid.GetXY(transform.position) != expectedDestination))
        {
            SetGridMoveBlocked(GridMoveFailureReason.DestinationMismatch);
            yield break;
        }

        Vector2Int completedDestination = hasExpectedDestination
            ? expectedDestination
            : grid != null
                ? grid.GetXY(transform.position)
                : Vector2Int.zero;
        actor?.AiMemory?.RecordMovement(
            completedDestination,
            completedPathDistance > 0f
                ? completedPathDistance
                : Vector3.Distance(pathStartPosition, transform.position),
            true);
    }

    public IEnumerator MoveByStep(GridMoveStep step, AIAction expectedAction = null)
    {
        int operationVersion = BeginMovementOperation();
        yield return MoveByStepInternal(
            step,
            expectedAction,
            operationVersion);
    }

    private IEnumerator MoveByStepInternal(
        GridMoveStep step,
        AIAction expectedAction,
        int operationVersion)
    {
        LastGridMoveWasBlocked = false;
        LastGridMoveFailureReason = GridMoveFailureReason.None;
        if (!AbilityMoveTraversalGuard.IsAtStepStart(
                grid,
                transform.position,
                step))
        {
            SetGridMoveBlocked(GridMoveFailureReason.StaleStepStart);
            yield break;
        }

        if (step.MoveType == GridMoveType.Walk)
        {
            yield return Move2GridPositionInternal(
                step.To,
                expectedAction,
                operationVersion);
            yield break;
        }

        yield return Move2GridPositionInternal(
            step.From,
            expectedAction,
            operationVersion);
        if (IsMovementOperationCancelled(
                expectedAction,
                operationVersion))
        {
            LastGridMoveFailureReason = GridMoveFailureReason.Cancelled;
            yield break;
        }

        if (step.MovementOccupant is IGridMovementHandler movementHandler
            && (step.MovementOccupant is not BuildableObject building
                || !building.isDestroy))
        {
            yield return movementHandler.Traverse(actor?.BuildingVisitor, step);
            if (IsMovementOperationCancelled(
                    expectedAction,
                    operationVersion))
            {
                LastGridMoveFailureReason = GridMoveFailureReason.Cancelled;
            }
            yield break;
        }

        if (RequiresMovementHandler(step))
        {
            SetGridMoveBlocked(
                GridMoveFailureReason.MissingMovementHandler);
            yield break;
        }

        yield return Move2GridPositionInternal(
            step.To,
            expectedAction,
            operationVersion);
    }

    public IEnumerator Move2GridPosition(Vector2Int gridPosition, AIAction expectedAction = null)
    {
        yield return Move2GridPositionInternal(
            gridPosition,
            expectedAction,
            movementOperationVersion);
    }

    private IEnumerator Move2GridPositionInternal(
        Vector2Int gridPosition,
        AIAction expectedAction,
        int operationVersion)
    {
        if (grid == null)
        {
            LastGridMoveFailureReason =
                GridMoveFailureReason.GridUnavailable;
            yield break;
        }

        RefreshCurrentActionReservation();
        Vector3 startPos = transform.position;
        if (grid.IsMovementBlockedByWall(gridPosition)
            || !traversalGuard.CanTraverseDoor(
                actor,
                grid,
                gridPosition,
                out _))
        {
            SetGridMoveBlocked(traversalGuard.GetCellBlockReason(
                actor,
                grid,
                gridPosition));
            yield break;
        }

        int observedGridVersion = grid.TraversalVersion;
        Vector3 endPos = grid.GetWorldPos(gridPosition);
        float terrainSpeedMultiplier = grid.GetGridCell(gridPosition)
            ?.TerrainMoveSpeedMultiplier ?? 1f;
        yield return Move2PosBySpeedInternal(
            endPos,
            Mathf.Max(0.01f, terrainSpeedMultiplier),
            expectedAction,
            gridPosition,
            observedGridVersion,
            startPos,
            operationVersion);
    }

    public void StartExitDungeon()
    {
        StartExitDungeonInternal(
            allowWorker: false,
            DoorAccessOverrideKind.None);
    }

    public void StartSystemExitDungeon()
    {
        StartExitDungeonInternal(
            allowWorker: true,
            DoorAccessOverrideKind.DirectCommand);
    }

    private void StartExitDungeonInternal(
        bool allowWorker,
        DoorAccessOverrideKind overrideKind)
    {
        if (actor == null
            || actor.Lifecycle == null
            || actor.Lifecycle.CurrentState == CharacterLifecycleState.ExitingDungeon)
        {
            return;
        }

        if (!allowWorker && CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            actor.Brain?.ClearPathSearchCache();
            return;
        }

        if (enterDungeonRoutine != null)
        {
            StopCoroutine(enterDungeonRoutine);
            enterDungeonRoutine = null;
        }

        actor.SetLifecycleState(CharacterLifecycleState.ExitingDungeon);
        if (overrideKind == DoorAccessOverrideKind.None)
        {
            StartTrackedActionMovement(ExitDungeon(overrideKind));
            return;
        }

        CancelActiveMovement();
        activeSystemMoveOverride = overrideKind;
        activeActionMovementRoutine = StartCoroutine(
            TrackActionMovement(ExitDungeon(overrideKind)));
    }

    public void StartEnterDungeon(Vector3 entryDoorWorldPosition, Vector2Int entryGridPosition)
    {
        if (enterDungeonRoutine != null)
        {
            StopCoroutine(enterDungeonRoutine);
        }

        StartTrackedActionMovement(EnterDungeon(entryDoorWorldPosition, entryGridPosition));
    }

    public void StartMoveByCurrentActionPath(float waitDuration = 0f)
    {
        AIAction expectedAction = GetCurrentAction();
        StartTrackedActionMovement(MoveByCurrentActionPath(waitDuration, expectedAction));
    }

    public void StartWait(float duration)
    {
        AIAction expectedAction = GetCurrentAction();
        StartTrackedActionMovement(WaitForAiAction(duration, expectedAction));
    }

    public bool StartIdleWander(float waitDuration, int minDistance = 2, int maxDistance = 8)
    {
        if (!TryFindIdleWanderPath(minDistance, maxDistance, out Queue<GridMoveStep> path)
            || path == null
            || path.Count == 0)
        {
            return false;
        }

        AIAction expectedAction = GetCurrentAction();
        StartTrackedActionMovement(MoveByPathThenWait(path, waitDuration, expectedAction));
        return true;
    }

    public void CancelActiveMovement()
    {
        InvalidateMovementOperation();
        if (activeActionMovementRoutine != null)
        {
            StopCoroutine(activeActionMovementRoutine);
            activeActionMovementRoutine = null;
        }

        if (activeManualMoveDestination.HasValue)
        {
            Vector2Int destination = activeManualMoveDestination.Value;
            activeManualMoveDestination = null;
            actor?.Brain?.CompleteManualMoveCommand(destination, succeeded: false);
        }

        activeSystemMoveDestination = null;
        activeSystemMoveOverride = DoorAccessOverrideKind.None;
    }

    public bool TryStartPlayerMove(Vector2Int destination, out string message)
    {
        CacheCommonReferences();
        if (actor == null || grid == null)
        {
            message = "이동할 캐릭터나 그리드를 찾을 수 없습니다.";
            return false;
        }

        if (!grid.IsValidGridPos(destination) || !grid.IsWalkable(destination))
        {
            message = "해당 칸으로 이동할 수 없습니다.";
            return false;
        }

        Vector2Int start = grid.GetXY(transform.position);
        if (start == destination)
        {
            actor.Brain?.CompleteManualMoveCommand(destination, succeeded: true);
            message = "이미 해당 칸에 있습니다.";
            return true;
        }

        GridTraversalContext directContext = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(actor),
            DoorAccessOverrideKind.DirectCommand);
        Queue<GridMoveStep> path = pathSearchBroker?.GetMovePathTo(
            grid,
            start,
            destination,
            GridPathSearchPriority.Urgent,
            directContext);
        if (path == null || path.Count == 0)
        {
            message = "해당 칸까지 이어지는 경로가 없습니다.";
            return false;
        }

        CancelActiveMovement();
        actor.Brain?.BeginManualMoveCommand(destination);
        activeManualMoveDestination = destination;
        activeActionMovementRoutine = StartCoroutine(
            TrackActionMovement(ExecutePlayerMove(path, destination)));
        message = $"({destination.x}, {destination.y}) 칸으로 이동";
        return true;
    }

    public bool TryStartSystemMove(
        Vector2Int destination,
        DoorAccessOverrideKind overrideKind,
        out string message)
    {
        CacheCommonReferences();
        if (actor == null || grid == null)
        {
            message = "이동할 캐릭터나 그리드를 찾을 수 없습니다.";
            return false;
        }

        if (!grid.IsValidGridPos(destination) || !grid.IsWalkable(destination))
        {
            message = "해당 칸으로 이동할 수 없습니다.";
            return false;
        }

        Vector2Int start = grid.GetXY(transform.position);
        if (start == destination)
        {
            message = "이미 해당 칸에 있습니다.";
            return true;
        }

        GridTraversalContext context = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(actor),
            overrideKind);
        Queue<GridMoveStep> path = pathSearchBroker?.GetMovePathTo(
            grid,
            start,
            destination,
            GridPathSearchPriority.Urgent,
            context);
        if (path == null || path.Count == 0)
        {
            message = "해당 칸까지 이어지는 경로가 없습니다.";
            return false;
        }

        CancelActiveMovement();
        activeSystemMoveDestination = destination;
        activeSystemMoveOverride = overrideKind;
        activeActionMovementRoutine = StartCoroutine(
            TrackActionMovement(ExecuteSystemMove(path)));
        message = $"({destination.x}, {destination.y}) 칸으로 이동";
        return true;
    }

    private void StartTrackedActionMovement(IEnumerator routine)
    {
        CancelActiveMovement();
        activeActionMovementRoutine = StartCoroutine(TrackActionMovement(routine));
    }

    private IEnumerator TrackActionMovement(IEnumerator routine)
    {
        yield return routine;
        activeActionMovementRoutine = null;
    }

    private IEnumerator ExecutePlayerMove(
        Queue<GridMoveStep> path,
        Vector2Int destination)
    {
        LastGridMoveWasBlocked = false;
        yield return MoveByPath(path);
        bool succeeded = !LastGridMoveWasBlocked
            && grid != null
            && grid.GetXY(transform.position) == destination;
        activeManualMoveDestination = null;
        actor?.Brain?.CompleteManualMoveCommand(destination, succeeded);
    }

    private IEnumerator ExecuteSystemMove(Queue<GridMoveStep> path)
    {
        LastGridMoveWasBlocked = false;
        yield return MoveByPath(path);
        activeSystemMoveDestination = null;
        activeSystemMoveOverride = DoorAccessOverrideKind.None;
    }

    private AIAction GetCurrentAction()
    {
        return actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;
    }

    private IEnumerator MoveByCurrentActionPath(float waitDuration, AIAction expectedAction)
    {
        yield return MoveByActionPath(expectedAction);

        if (waitDuration > 0f)
        {
            yield return WaitForAiActionDelay(waitDuration, expectedAction);
        }

        if (IsActionMovementCancelled(expectedAction))
        {
            yield break;
        }

        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }
    }

    private IEnumerator MoveByPathThenWait(Queue<GridMoveStep> path, float waitDuration, AIAction expectedAction)
    {
        yield return MoveByPath(path, expectedAction);

        if (waitDuration > 0f)
        {
            yield return WaitForAiActionDelay(waitDuration, expectedAction);
        }

        if (IsActionMovementCancelled(expectedAction))
        {
            yield break;
        }

        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }
    }

    public bool TryFindIdleWanderPath(
        int minDistance,
        int maxDistance,
        out Queue<GridMoveStep> path)
    {
        CacheCommonReferences();
        if (actor == null)
        {
            path = null;
            return false;
        }

        return idleWanderPlanner.TryFind(
            grid,
            actor.transform.position,
            GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor)),
            minDistance,
            maxDistance,
            out path);
    }

    private void SnapToGridRowIfWalkable(Vector2Int gridPosition)
    {
        if (grid == null
            || !grid.IsValidGridPos(gridPosition)
            || !grid.IsWalkable(gridPosition))
        {
            return;
        }

        Vector3 position = transform.position;
        position.y = grid.GetWorldPos(gridPosition).y;
        transform.position = position;
    }

    public IEnumerator MoveByCurrentBestActionPath()
    {
        yield return MoveByActionPath(GetCurrentAction());
    }

    private IEnumerator MoveByActionPath(AIAction action)
    {
        if (action == null)
        {
            yield break;
        }

        if (action.pathSteps.Count > 0)
        {
            actor?.Brain?.SetActionPhase("\uC774\uB3D9", action.destination, $"{action.planKind} / {action.pathSteps.Count}\uCE78");
            yield return MoveByPath(new Queue<GridMoveStep>(action.pathSteps), action);
        }
    }

    private bool TryReplanCurrentActionPath(
        AIAction action,
        out Queue<GridMoveStep> rebuiltPath)
    {
        rebuiltPath = null;
        if (action == null
            || actor == null
            || actor.Brain == null)
        {
            return false;
        }

        actor.Brain.ClearPathSearchCache();
        if (!action.TryRebuildPathFromCurrentPosition(actor, out AIActionFailure failure))
        {
            actor.Brain.SetActionPhase("\uACBD\uB85C \uC7AC\uD0D0\uC0C9 \uC2E4\uD328", action.destination, failure.ToString());
            return false;
        }

        if (action.pathSteps.Count == 0)
        {
            actor.Brain.SetActionPhase("\uB3C4\uCC29", action.destination, action.planKind.ToString());
            return false;
        }

        rebuiltPath = new Queue<GridMoveStep>(action.pathSteps);
        actor.Brain.SetActionPhase(
            "\uACBD\uB85C \uC7AC\uD0D0\uC0C9",
            action.destination,
            $"{action.planKind} / {action.pathSteps.Count}\uCE78");
        return rebuiltPath.Count > 0;
    }

    private void RefreshCurrentActionReservation()
    {
        if (actor == null || actor.Brain == null || actor.Brain.bestAction == null)
        {
            return;
        }

        actor.Brain.bestAction.RefreshReservation(actor);
    }

    private bool IsActionMovementCancelled(AIAction expectedAction)
    {
        return expectedAction != null
            && (actor == null
                || actor.Brain == null
                || actor.Brain.bestAction != expectedAction
                || actor.Brain.isBestActionEnd);
    }

    private int BeginMovementOperation()
    {
        InvalidateMovementOperation();
        return movementOperationVersion;
    }

    private void InvalidateMovementOperation()
    {
        unchecked
        {
            movementOperationVersion++;
        }
    }

    private bool IsMovementOperationCancelled(
        AIAction expectedAction,
        int operationVersion)
    {
        return operationVersion != movementOperationVersion
            || IsActionMovementCancelled(expectedAction);
    }

    private IEnumerator WaitForAiAction(float duration, AIAction expectedAction)
    {
        yield return WaitForAiActionDelay(duration, expectedAction);

        if (IsActionMovementCancelled(expectedAction))
        {
            yield break;
        }

        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }
    }

    private IEnumerator WaitForAiActionDelay(float duration, AIAction expectedAction)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (IsActionMovementCancelled(expectedAction))
            {
                yield break;
            }

            timer += gameClock.DeltaTime;
            yield return null;
        }
    }

    private IEnumerator EnterDungeon(Vector3 entryDoorWorldPosition, Vector2Int entryGridPosition)
    {
        if (actor != null)
        {
            actor.SetLifecycleState(CharacterLifecycleState.EnteringDungeon);
        }

        CacheCommonReferences();

        yield return Move2PosBySpeed(entryDoorWorldPosition);

        if (grid != null && grid.IsValidGridPos(entryGridPosition))
        {
            yield return Move2PosBySpeed(grid.GetWorldPos(entryGridPosition));
        }

        if (actor != null)
        {
            actor.ChangeLayer("Default");
            actor.SetLifecycleState(CharacterLifecycleState.Active);
        }

        enterDungeonRoutine = null;
    }

    private IEnumerator ExitDungeon(DoorAccessOverrideKind overrideKind)
    {
        if (grid == null)
        {
            CacheCommonReferences();
        }

        if (grid == null)
        {
            if (actor != null)
            {
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }
            activeSystemMoveDestination = null;
            activeSystemMoveOverride = DoorAccessOverrideKind.None;
            yield break;
        }

        TryResolveSpawner();
        if (spawner == null
            || !spawner.TryGetEntryGridPosition(out Vector2Int exitGridPosition))
        {
            if (actor != null)
            {
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }

            activeSystemMoveDestination = null;
            activeSystemMoveOverride = DoorAccessOverrideKind.None;
            yield break;
        }

        bool reachedExit = actor != null
            && grid.GetXY(actor.transform.position) == exitGridPosition;
        int counter = 0;
        while (!reachedExit && counter < 5)
        {
            Vector2Int startPos = grid.GetXY(transform.position);
            startPos = grid.IsValidGridPos(startPos) ? startPos : Vector2Int.zero;
            GridTraversalContext traversalContext = actor != null
                ? GridTraversalContext.ForCharacter(
                    CharacterPersistentIdentity.Require(actor),
                    overrideKind)
                : default;
            Queue<GridMoveStep> path = pathSearchBroker != null
                ? pathSearchBroker.GetMovePathTo(
                    grid,
                    startPos,
                    exitGridPosition,
                    GridPathSearchPriority.Urgent,
                    traversalContext)
                : grid.GetMovePathTo(startPos, exitGridPosition);
            if (path == null)
            {
                yield return null;
                counter++;
                continue;
            }

            if (path != null && path.Count > 0)
            {
                yield return MoveByPath(path);
            }

            if (actor != null
                && grid.GetXY(actor.transform.position) == exitGridPosition)
            {
                reachedExit = true;
                break;
            }

            yield return new WaitForSeconds(1f);
            counter++;
        }

        if (!reachedExit)
        {
            if (actor != null)
            {
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }

            activeSystemMoveDestination = null;
            activeSystemMoveOverride = DoorAccessOverrideKind.None;
            yield break;
        }

        if (spawner != null)
        {
            yield return Move2PosBySpeed(spawner.GetEntryDoorWorldPosition());
            yield return Move2PosBySpeed(spawner.GetOutsideSpawnWorldPosition());
            if (actor != null)
            {
                actor.SetLifecycleState(CharacterLifecycleState.Despawned);
            }
            yield return spawner.Interact(actor);
        }
        else if (actor != null)
        {
            actor.SetLifecycleState(CharacterLifecycleState.Active);
        }

        activeSystemMoveDestination = null;
        activeSystemMoveOverride = DoorAccessOverrideKind.None;
    }

    private bool TryResolveSpawner()
    {
        if (spawner != null)
        {
            return true;
        }

        return spawnerProvider != null && spawnerProvider.TryGetSpawner(out spawner);
    }

    private ICharacterAiSchedulingService RequireAiSchedulingService()
    {
        return aiSchedulingService
            ?? throw new InvalidOperationException($"{nameof(AbilityMove)} requires {nameof(ICharacterAiSchedulingService)} injection.");
    }

    public IEnumerator Move2PosByTime(Vector3 endPos, float duration)
    {
        float timer = 0f;
        Vector3 startPos = transform.position;
        while (timer < duration)
        {
            transform.position = Vector3.Lerp(startPos, endPos, (timer / duration));
            timer += gameClock.DeltaTime;
            yield return null;
        }
        transform.position = endPos;
    }
    public IEnumerator Move2PosBySpeed(Vector3 endPos, float multifly = 1.0f, AIAction expectedAction = null)
    {
        yield return Move2PosBySpeedInternal(
            endPos,
            multifly,
            expectedAction,
            null,
            0,
            transform.position,
            movementOperationVersion);
    }

    private IEnumerator Move2PosBySpeedInternal(
        Vector3 endPos,
        float multifly,
        AIAction expectedAction,
        Vector2Int? blockedGridPosition,
        int observedGridVersion,
        Vector3 blockedFallbackPosition,
        int operationVersion)
    {
        Vector3 startPos = transform.position;
        float deltaX = endPos.x - startPos.x;
        if (Mathf.Abs(deltaX) > 0.01f && deltaX > 0f)
        {
            actor?.Flip(CharacterFacing.RIGHT);
        }
        else if (Mathf.Abs(deltaX) > 0.01f)
        {
            actor?.Flip(CharacterFacing.LEFT);
        }
        float distance = Vector3.Distance(startPos, endPos);
        float totalSpeed = CharacterMovementKinematics.GetMoveSpeed(
            actor,
            moveSpeed) * multifly;
        if (totalSpeed <= 0f)
        {
            LastGridMoveFailureReason =
                GridMoveFailureReason.InvalidSpeed;
            yield break;
        }

        float duration = distance / totalSpeed;
        float timer = 0f;

        while (timer < duration)
        {
            if (TryRollbackForChangedGridBlock(
                blockedGridPosition,
                ref observedGridVersion,
                blockedFallbackPosition))
            {
                yield break;
            }

            if (IsMovementOperationCancelled(
                    expectedAction,
                    operationVersion))
            {
                LastGridMoveFailureReason =
                    GridMoveFailureReason.Cancelled;
                yield break;
            }

            Vector3 nextPosition = Vector3.Lerp(startPos, endPos, (timer / duration));
            CharacterMovementKinematics.UpdateFacing(
                actor,
                nextPosition.x - transform.position.x);
            transform.position = nextPosition;
            timer += gameClock.DeltaTime;
            int frameStride = RequireAiSchedulingService().GetMovementFrameStride(actor);
            for (int i = 1; i < frameStride && timer < duration; i++)
            {
                yield return null;
                if (IsMovementOperationCancelled(
                        expectedAction,
                        operationVersion))
                {
                    LastGridMoveFailureReason =
                        GridMoveFailureReason.Cancelled;
                    yield break;
                }

                if (TryRollbackForChangedGridBlock(
                    blockedGridPosition,
                    ref observedGridVersion,
                    blockedFallbackPosition))
                {
                    yield break;
                }

                timer += gameClock.DeltaTime;
            }
            yield return null;
        }

        if (TryRollbackForChangedGridBlock(
            blockedGridPosition,
            ref observedGridVersion,
            blockedFallbackPosition))
        {
            yield break;
        }

        CharacterMovementKinematics.UpdateFacing(
            actor,
            endPos.x - transform.position.x);
        transform.position = endPos;
    }

    private bool TryRollbackForChangedGridBlock(
        Vector2Int? blockedGridPosition,
        ref int observedGridVersion,
        Vector3 blockedFallbackPosition)
    {
        bool blocked = traversalGuard.TryRollbackForChangedBlock(
                actor,
                grid,
                transform,
                blockedGridPosition,
                ref observedGridVersion,
                blockedFallbackPosition,
                out GridMoveFailureReason reason);
        if (blocked)
        {
            SetGridMoveBlocked(reason);
        }
        return blocked;
    }

    private void SetGridMoveBlocked(
        GridMoveFailureReason reason =
            GridMoveFailureReason.WallBlocked)
    {
        LastGridMoveWasBlocked = true;
        if (LastGridMoveFailureReason == GridMoveFailureReason.None)
        {
            LastGridMoveFailureReason = reason;
        }
        GridMoveBlockedResponder.Respond(actor, grid, transform.position);
    }
}
