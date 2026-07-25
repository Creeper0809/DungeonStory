using BehaviorDesigner.Runtime.Tasks.Unity.UnityAnimation;
using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class AbilityMove : CharacterAbility
{
    private float moveSpeed;
    private CharacterSpawner spawner;
    private ICharacterSpawnerProvider spawnerProvider;
    private ICharacterAiSchedulingService aiSchedulingService;
    private IGridPathSearchBroker pathSearchBroker;
    private IDefenseEngagementRuntime defenseEngagementRuntime;
    private IDoorAccessQuery doorAccessQuery;
    private IGameClock gameClock;
    private IRandomStream movementRandom;
    private Coroutine enterDungeonRoutine;
    private Coroutine activeActionMovementRoutine;
    private Vector2Int? activeManualMoveDestination;
    private Vector2Int? activeSystemMoveDestination;
    private DoorAccessOverrideKind activeSystemMoveOverride;

    public bool LastGridMoveWasBlocked { get; private set; }

    [Inject]
    public void ConstructAbilityMove(
        ICharacterSpawnerProvider spawnerProvider,
        ICharacterAiSchedulingService aiSchedulingService,
        IGridPathSearchBroker pathSearchBroker,
        IRandomStreamProvider randomStreamProvider,
        IGameClock gameClock,
        IDefenseEngagementRuntime defenseEngagementRuntime = null)
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
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.defenseEngagementRuntime = defenseEngagementRuntime;
        TryResolveSpawner();
    }

    [Inject]
    public void ConstructDoorAccessQuery(IDoorAccessQuery doorAccessQuery)
    {
        this.doorAccessQuery = doorAccessQuery
            ?? throw new ArgumentNullException(nameof(doorAccessQuery));
    }

    protected override void Awake()
    {
        base.Awake();
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
        if (path == null) yield break;

        int staleReplanAttempts = 0;
        while (path.Count > 0)
        {
            if (IsActionMovementCancelled(expectedAction))
            {
                yield break;
            }

            GridMoveStep step = path.Dequeue();
            if (step == null) continue;

            if (!IsAtStepStart(step))
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
                    yield break;
                }

                SetGridMoveBlocked();
                yield break;
            }

            if (IsWalkStepBlocked(step))
            {
                SetGridMoveBlocked();
                yield break;
            }

            RefreshCurrentActionReservation();
            yield return MoveByStep(step, expectedAction);

            if (LastGridMoveWasBlocked || IsWalkStepBlocked(step))
            {
                SetGridMoveBlocked();
                yield break;
            }
        }
    }

    public IEnumerator MoveByStep(GridMoveStep step, AIAction expectedAction = null)
    {
        LastGridMoveWasBlocked = false;
        if (!IsAtStepStart(step))
        {
            SetGridMoveBlocked();
            yield break;
        }

        if (step.MoveType == GridMoveType.Walk)
        {
            yield return Move2GridPosition(step.To, expectedAction);
            yield break;
        }

        yield return Move2GridPosition(step.From, expectedAction);
        if (IsActionMovementCancelled(expectedAction))
        {
            yield break;
        }

        if (step.MovementOccupant is BuildableObject building
            && !building.isDestroy
            && building is IGridMovementHandler movementHandler)
        {
            yield return movementHandler.Traverse(actor, step);
            yield break;
        }

        if (RequiresMovementHandler(step))
        {
            SetGridMoveBlocked();
            yield break;
        }

        yield return Move2GridPosition(step.To, expectedAction);
    }

    public IEnumerator Move2GridPosition(Vector2Int gridPosition, AIAction expectedAction = null)
    {
        if (grid == null) yield break;

        RefreshCurrentActionReservation();
        Vector3 startPos = transform.position;
        if (grid.IsMovementBlockedByWall(gridPosition)
            || !CanTraverseDoor(gridPosition, out _))
        {
            SetGridMoveBlocked();
            yield break;
        }

        int observedGridVersion = grid.version;
        Vector3 endPos = grid.GetWorldPos(gridPosition);
        yield return Move2PosBySpeedInternal(
            endPos,
            1.0f,
            expectedAction,
            gridPosition,
            observedGridVersion,
            startPos);
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
            actor,
            DoorAccessOverrideKind.DirectCommand);
        GridPathSearchResult search = pathSearchBroker != null
            && pathSearchBroker.TryGetSearch(
                grid,
                start,
                out GridPathSearchResult directSearch,
                GridPathSearchPriority.Urgent,
                directContext)
                ? directSearch
                : null;
        if (search == null || !search.ContainsPosition(destination))
        {
            message = "해당 칸까지 이어지는 경로가 없습니다.";
            return false;
        }

        Queue<GridMoveStep> path = search.GetMovePath(position => position == destination);
        if (path == null || path.Count == 0)
        {
            message = "이동 경로를 만들 수 없습니다.";
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
            actor,
            overrideKind);
        GridPathSearchResult search = pathSearchBroker != null
            && pathSearchBroker.TryGetSearch(
                grid,
                start,
                out GridPathSearchResult systemSearch,
                GridPathSearchPriority.Urgent,
                context)
                ? systemSearch
                : null;
        if (search == null || !search.ContainsPosition(destination))
        {
            message = "해당 칸까지 이어지는 경로가 없습니다.";
            return false;
        }

        Queue<GridMoveStep> path = search.GetMovePath(position => position == destination);
        if (path == null || path.Count == 0)
        {
            message = "이동 경로를 만들 수 없습니다.";
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
        path = null;
        CacheCommonReferences();
        if (actor == null || grid == null)
        {
            return false;
        }

        Vector2Int originalPos = grid.GetXY(actor.transform.position);
        GridPathSearchResult searchResult = GetIdleSearchResult(originalPos);
        if (searchResult == null)
        {
            return false;
        }

        int min = Mathf.Max(1, minDistance);
        return searchResult.TryGetMovePathToRandomReachablePosition(
                IsPlainIdleWalkable,
                IsSupportedIdleWanderPath,
                min,
                maxDistance,
                movementRandom,
                out path)
            || searchResult.TryGetMovePathToRandomReachablePosition(
                IsPlainIdleWalkable,
                IsSupportedIdleWanderPath,
                1,
                0,
                movementRandom,
                out path);
    }

    private bool IsPlainIdleWalkable(Vector2Int position)
    {
        if (grid == null || !grid.IsWalkable(position))
        {
            return false;
        }

        IGridOccupant buildingOccupant = grid.GetGridCell(position)?.GetOccupant(GridLayer.Building);
        return buildingOccupant == null || !buildingOccupant.IsGridMovement;
    }

    private static bool IsSupportedIdleWanderPath(Queue<GridMoveStep> path)
    {
        if (path == null || path.Count == 0)
        {
            return false;
        }

        foreach (GridMoveStep step in path)
        {
            if (step == null)
            {
                return false;
            }

            if (step.MoveType == GridMoveType.Walk)
            {
                continue;
            }

            if (!IsSupportedVerticalMovementStep(step))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSupportedVerticalMovementStep(GridMoveStep step)
    {
        return step != null
            && (step.MoveType == GridMoveType.Stair || step.MoveType == GridMoveType.Elevator)
            && step.MovementOccupant is IGridMovementHandler;
    }

    private static bool RequiresMovementHandler(GridMoveStep step)
    {
        return step != null
            && (step.MoveType == GridMoveType.Stair || step.MoveType == GridMoveType.Elevator);
    }

    private GridPathSearchResult GetIdleSearchResult(Vector2Int originalPos)
    {
        if (grid == null)
        {
            return null;
        }

        if (!grid.IsValidGridPos(originalPos) || !grid.IsWalkable(originalPos))
        {
            return null;
        }

        if (actor != null && actor.Brain != null)
        {
            return actor.Brain.GetPathSearch(actor);
        }

        IGridPathSearchBroker broker = pathSearchBroker ?? actor?.PathSearchBroker;
        return broker != null
            && broker.TryGetSearch(
                grid,
                originalPos,
                out GridPathSearchResult search,
                GridPathSearchPriority.Urgent,
                GridTraversalContext.ForCharacter(actor))
            ? search
            : null;
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

        Func<Vector2Int, bool> condition = (pos) => grid.GetGridCell(pos)?.GetBuildingInlayer()?.id == 1;
        int counter = 0;
        while (counter < 5)
        {
            Vector2Int startPos = grid.GetXY(transform.position);
            startPos = grid.IsValidGridPos(startPos) ? startPos : Vector2Int.zero;
            GridTraversalContext traversalContext = actor != null
                ? GridTraversalContext.ForCharacter(actor, overrideKind)
                : default;
            Queue<GridMoveStep> path = pathSearchBroker != null
                ? pathSearchBroker.GetMovePath(
                    grid,
                    startPos,
                    condition,
                    GridPathSearchPriority.Urgent,
                    traversalContext)
                : grid.GetMovePath(startPos, condition);
            if (path != null && path.Count > 0)
            {
                yield return MoveByPath(path);
            }
            if(actor != null && grid.GetXY(actor.transform.position).y == 0)
            {
                break;
            }
            yield return new WaitForSeconds(1f);
            counter++;
        }

        TryResolveSpawner();

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
            transform.position);
    }

    private IEnumerator Move2PosBySpeedInternal(
        Vector3 endPos,
        float multifly,
        AIAction expectedAction,
        Vector2Int? blockedGridPosition,
        int observedGridVersion,
        Vector3 blockedFallbackPosition)
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
        float totalSpeed = moveSpeed * multifly;
        if (totalSpeed <= 0f)
        {
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

            if (IsActionMovementCancelled(expectedAction))
            {
                yield break;
            }

            Vector3 nextPosition = Vector3.Lerp(startPos, endPos, (timer / duration));
            UpdateFacingForMovement(nextPosition.x - transform.position.x);
            transform.position = nextPosition;
            timer += gameClock.DeltaTime;
            int frameStride = RequireAiSchedulingService().GetMovementFrameStride(actor);
            for (int i = 1; i < frameStride && timer < duration; i++)
            {
                yield return null;
                if (IsActionMovementCancelled(expectedAction))
                {
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

        UpdateFacingForMovement(endPos.x - transform.position.x);
        transform.position = endPos;
        if (blockedGridPosition.HasValue)
        {
            actor?.AiMemory?.RecordMovement(blockedGridPosition.Value, distance, true);
        }
    }

    private bool TryRollbackForChangedGridBlock(
        Vector2Int? blockedGridPosition,
        ref int observedGridVersion,
        Vector3 blockedFallbackPosition)
    {
        if (!blockedGridPosition.HasValue || grid == null)
        {
            return false;
        }

        if (defenseEngagementRuntime?.IsCellReservedForOther(actor, blockedGridPosition.Value) ?? false)
        {
            transform.position = blockedFallbackPosition;
            SetGridMoveBlocked();
            return true;
        }

        if (!CanTraverseDoor(blockedGridPosition.Value, out _))
        {
            transform.position = blockedFallbackPosition;
            SetGridMoveBlocked();
            return true;
        }

        int currentGridVersion = grid.version;
        if (currentGridVersion == observedGridVersion)
        {
            return false;
        }

        observedGridVersion = currentGridVersion;
        if (!grid.IsMovementBlockedByWall(blockedGridPosition.Value))
        {
            return false;
        }

        transform.position = blockedFallbackPosition;
        SetGridMoveBlocked();
        return true;
    }

    private bool IsWalkStepBlocked(GridMoveStep step)
    {
        return step != null
            && step.MoveType == GridMoveType.Walk
            && grid != null
            && (grid.IsMovementBlockedByWall(step.To)
                || !CanTraverseDoor(step.To, out _)
                || (defenseEngagementRuntime?.IsCellReservedForOther(actor, step.To) ?? false));
    }

    private bool CanTraverseDoor(
        Vector2Int position,
        out string denialReason)
    {
        denialReason = string.Empty;
        if (grid == null || doorAccessQuery == null || actor == null)
        {
            return true;
        }

        DoorAccessOverrideKind overrideKind = activeManualMoveDestination.HasValue
            ? DoorAccessOverrideKind.DirectCommand
            : activeSystemMoveDestination.HasValue
                ? activeSystemMoveOverride
                : DoorAccessOverrideKind.None;
        return doorAccessQuery.CanTraverse(
            grid,
            position,
            GridTraversalContext.ForCharacter(actor, overrideKind),
            out denialReason);
    }

    private bool IsAtStepStart(GridMoveStep step)
    {
        return step != null
            && grid != null
            && grid.GetXY(transform.position) == step.From;
    }

    private void SetGridMoveBlocked()
    {
        LastGridMoveWasBlocked = true;
        actor?.AiMemory?.RecordMovement(
            grid != null ? grid.GetXY(transform.position) : Vector2Int.zero,
            0f,
            false,
            "길 막힘");
        if (actor == null || actor.Brain == null)
        {
            return;
        }

        actor.Brain.ClearPathSearchCache();
        if (actor.Brain.bestAction != null)
        {
            actor.Brain.SetActionPhase("\uC774\uB3D9 \uB9C9\uD798", actor.Brain.bestAction.destination);
            actor.Brain.isBestActionEnd = true;
        }
    }

    private void UpdateFacingForMovement(float deltaX)
    {
        if (actor == null || Mathf.Abs(deltaX) <= 0.001f)
        {
            return;
        }

        actor.Flip(deltaX > 0f ? CharacterFacing.RIGHT : CharacterFacing.LEFT);
    }
}
