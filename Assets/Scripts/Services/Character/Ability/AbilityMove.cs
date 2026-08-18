using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using static GridMovePathRules;
public class AbilityMove : CharacterAbility
{
    private const int DefaultPathSearchDeferralLimit = 64;
    private float moveSpeed;
    private CharacterSpawner spawner;
    private ICharacterSpawnerProvider spawnerProvider;
    private ICharacterAiSchedulingService aiSchedulingService;
    private IGridPathSearchBroker pathSearchBroker;
    private IDefenseEngagementRuntime defenseEngagementRuntime;
    private IGameClock gameClock;
    private IRandomStream movementRandom;
    private IRandomStreamProvider randomStreamProvider;
    private CharacterId movementRandomCharacterId;
    private CharacterIdleWanderPlanner idleWanderPlanner;
    private AbilityMoveTraversalGuard traversalGuard;
    private Coroutine enterDungeonRoutine;
    private Coroutine activeActionMovementRoutine;
    private Vector2Int? activeManualMoveDestination;
    private Vector2Int? activeSystemMoveDestination;
    private DoorAccessOverrideKind activeSystemMoveOverride;
    private int movementOperationVersion;
    private bool protectedSystemMovementOperation;
    private bool retainProtectedSystemMovementAfterCompletion;
    private string activeMovementOperationOwner = string.Empty;
    private long runtimeActionPathReplanCount;
    private long runtimeActionPathFailureCount;
    private int pathSearchDeferralLimit = DefaultPathSearchDeferralLimit;

    public bool LastGridMoveWasBlocked { get; private set; }
    public GridMoveFailureReason LastGridMoveFailureReason { get; private set; }
    public string LastMovementCancellationSourceForDiagnostics { get; private set; }
        = string.Empty;
    public string LastMovementOperationPreemptionForDiagnostics { get; private set; }
    public string LastActionMovementCancellationReasonForDiagnostics
    {
        get;
        private set;
    }
        = string.Empty;
    public string LastRejectedMovementOperationOwnerForDiagnostics { get; private set; }
        = string.Empty;
    public long RuntimeActionPathReplanCount => runtimeActionPathReplanCount;
    public long RuntimeActionPathFailureCount => runtimeActionPathFailureCount;
    public bool IsSystemMoveInProgress => activeActionMovementRoutine != null
        && activeSystemMoveDestination.HasValue;
    public bool HasProtectedSystemMovementOwnership =>
        retainProtectedSystemMovementAfterCompletion
        && protectedSystemMovementOperation;
    public bool HasActiveMovementRoutineForDiagnostics =>
        activeActionMovementRoutine != null || enterDungeonRoutine != null;
    public Vector2Int? ActiveSystemMoveDestinationForDiagnostics =>
        activeSystemMoveDestination;
    public string ActiveMovementOperationOwnerForDiagnostics =>
        activeMovementOperationOwner;
    public int MovementOperationVersionForDiagnostics =>
        movementOperationVersion;
    public int PathSearchDeferralLimitForDiagnostics =>
        pathSearchDeferralLimit;
    public float GameClockTimeForDiagnostics => gameClock?.Time ?? -1f;
    public float GameClockDeltaTimeForDiagnostics => gameClock?.DeltaTime ?? -1f;

#if UNITY_EDITOR
    public int DebugReplacePathSearchDeferralLimit(int replacement)
    {
        int previous = pathSearchDeferralLimit;
        pathSearchDeferralLimit = Mathf.Clamp(
            replacement,
            1,
            DefaultPathSearchDeferralLimit);
        return previous;
    }

    public IGridPathSearchBroker DebugReplacePathSearchBroker(
        IGridPathSearchBroker replacement)
    {
        IGridPathSearchBroker previous = pathSearchBroker;
        pathSearchBroker = replacement
            ?? throw new ArgumentNullException(nameof(replacement));
        idleWanderPlanner = null;
        return previous;
    }
#endif

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        CancelActiveMovement();
        if (enterDungeonRoutine != null)
        {
            StopCoroutine(enterDungeonRoutine);
            enterDungeonRoutine = null;
        }
    }

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
        this.randomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        movementRandom = null;
        movementRandomCharacterId = default;
        idleWanderPlanner = null;
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
        // Initialization runs before life/narrative publication has registered
        // the authoritative nine proficiencies. The live actor performance is
        // queried by CharacterMovementKinematics for every movement segment;
        // this value is only the actor-less fallback.
        moveSpeed = data != null ? data.moveSpeed : 1f;
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
    }

    public IEnumerator MoveByPath(
        Queue<GridMoveStep> path,
        AIAction expectedAction = null,
        Action movementProgressCallback = null) =>
        MoveByPathOwned(
            path,
            expectedAction,
            movementProgressCallback,
            "raw-path");

    private IEnumerator MoveByPathOwned(
        Queue<GridMoveStep> path,
        AIAction expectedAction,
        Action movementProgressCallback,
        string operationOwner)
    {
        if (!TryBeginMovementOperation(
                operationOwner,
                out int operationVersion))
        {
            yield break;
        }
        yield return MoveByPathInternal(
            path,
            expectedAction,
            operationVersion,
            movementProgressCallback);
        CompleteMovementOperation(operationVersion, operationOwner);
    }

    private IEnumerator MoveByPathInternal(
        Queue<GridMoveStep> path,
        AIAction expectedAction,
        int operationVersion,
        Action movementProgressCallback)
    {
        LastGridMoveWasBlocked = false;
        LastGridMoveFailureReason = GridMoveFailureReason.None;
        if (path == null)
        {
            LastGridMoveFailureReason = GridMoveFailureReason.MissingPath;
            actor?.Brain?.NotifyMovementTerminal(LastGridMoveFailureReason);
            yield break;
        }

        int totalPathSteps = path.Count;
        int completedPathSteps = 0;
        actor?.Brain?.NotifyMovementStarted(totalPathSteps);

        bool hasExpectedDestination =
            TryGetPathDestination(path, out Vector2Int expectedDestination);
        Vector3 pathStartPosition = transform.position;
        float completedPathDistance = 0f;
        int staleReplanAttempts = 0;
        int pathSearchDeferrals = 0;
        int totalPathSearchDeferrals = 0;
        int pathRecoveryBackoffFrames = 0;
        bool pathRecoveryDeferred = false;
        AIActionFailure deferredRecoveryFailure = AIActionFailure.None;
        while (path.Count > 0 || pathRecoveryDeferred)
        {
            if (IsMovementOperationCancelled(
                    expectedAction,
                    operationVersion))
            {
                LastGridMoveFailureReason = GridMoveFailureReason.Cancelled;
                yield break;
            }

            // A deferred broker result is a per-frame budget signal, not proof
            // that the destination is unreachable. Preserve the current action
            // and its reservation while retrying on later frames. The bound keeps
            // a broken broker from creating an immortal movement coroutine.
            if (pathRecoveryDeferred)
            {
                int framesToWait = Mathf.Max(1, pathRecoveryBackoffFrames);
                for (int frame = 0; frame < framesToWait; frame++)
                {
                    if (IsMovementOperationCancelled(
                            expectedAction,
                            operationVersion))
                    {
                        LastGridMoveFailureReason =
                            GridMoveFailureReason.Cancelled;
                        yield break;
                    }

                    RefreshCurrentActionReservation();
                    yield return null;
                }
                if (TryRecoverBlockedActionPath(
                        expectedAction,
                        ref staleReplanAttempts,
                        out Queue<GridMoveStep> deferredPath,
                        out deferredRecoveryFailure))
                {
                    path = deferredPath;
                    pathRecoveryDeferred = false;
                    pathSearchDeferrals = 0;
                    pathRecoveryBackoffFrames = 0;
                    continue;
                }

                if (TryScheduleDeferredPathRecovery(
                        ref deferredRecoveryFailure,
                        ref pathSearchDeferrals,
                        ref totalPathSearchDeferrals,
                        out pathRecoveryBackoffFrames,
                        out pathRecoveryDeferred))
                {
                    continue;
                }

                CompleteBlockedActionPath(
                    expectedAction,
                    deferredRecoveryFailure);
                yield break;
            }

            GridMoveStep step = path.Dequeue();
            if (!step.IsValid) continue;

            if (!AbilityMoveTraversalGuard.IsAtStepStart(
                    grid,
                    transform.position,
                    step))
            {
                AIActionFailure staleFailure = AIActionFailure.None;
                if (staleReplanAttempts < 1
                    && TryReplanCurrentActionPath(
                        expectedAction,
                        out Queue<GridMoveStep> rebuiltPath,
                        out staleFailure))
                {
                    staleReplanAttempts++;
                    runtimeActionPathReplanCount++;
                    path = rebuiltPath;
                    continue;
                }

                if (TryScheduleDeferredPathRecovery(
                        ref staleFailure,
                        ref pathSearchDeferrals,
                        ref totalPathSearchDeferrals,
                        out pathRecoveryBackoffFrames,
                        out pathRecoveryDeferred))
                {
                    deferredRecoveryFailure = staleFailure;
                    continue;
                }

                if (expectedAction != null && expectedAction.planKind == AIActionPlanKind.DestinationOnly)
                {
                    LastGridMoveFailureReason =
                        GridMoveFailureReason.StaleStepStart;
                    yield break;
                }

                SetGridMoveBlocked(
                    GridMoveFailureReason.StaleStepStart,
                    reportToBrain: false);
                CompleteBlockedActionPath(
                    expectedAction,
                    AIActionFailure.Create(
                        AIActionFailureKind.NoPath,
                        "Committed action path no longer starts at the actor position.",
                        expectedAction?.destination));
                yield break;
            }

            if (traversalGuard.TryGetWalkStepBlockReason(
                    actor,
                    grid,
                    step,
                    out GridMoveFailureReason initialBlockReason))
            {
                SetGridMoveBlocked(initialBlockReason, reportToBrain: false);
                if (TryRecoverBlockedActionPath(
                    expectedAction,
                    ref staleReplanAttempts,
                    out Queue<GridMoveStep> rebuiltPath,
                    out AIActionFailure failure))
                {
                    path = rebuiltPath;
                    continue;
                }

                if (TryScheduleDeferredPathRecovery(
                        ref failure,
                        ref pathSearchDeferrals,
                        ref totalPathSearchDeferrals,
                        out pathRecoveryBackoffFrames,
                        out pathRecoveryDeferred))
                {
                    deferredRecoveryFailure = failure;
                    continue;
                }
                CompleteBlockedActionPath(expectedAction, failure);
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
                            destination),
                        reportToBrain: false);
                    if (TryRecoverBlockedActionPath(
                        expectedAction,
                        ref staleReplanAttempts,
                        out Queue<GridMoveStep> rebuiltPath,
                        out AIActionFailure failure))
                    {
                        path = rebuiltPath;
                        continue;
                    }

                    if (TryScheduleDeferredPathRecovery(
                            ref failure,
                            ref pathSearchDeferrals,
                            ref totalPathSearchDeferrals,
                            out pathRecoveryBackoffFrames,
                            out pathRecoveryDeferred))
                    {
                        deferredRecoveryFailure = failure;
                        continue;
                    }
                    CompleteBlockedActionPath(expectedAction, failure);
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
                bool currentStepBlocked = false;
                while (timer < duration)
                {
                    if (TryRollbackForChangedGridBlock(
                            destination,
                            ref observedGridVersion,
                            startPosition,
                            reportToBrain: false)
                        || IsMovementOperationCancelled(
                            expectedAction,
                            operationVersion))
                    {
                        if (LastGridMoveWasBlocked)
                        {
                            currentStepBlocked = true;
                            break;
                        }
                        else
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
                                startPosition,
                                reportToBrain: false))
                        {
                            if (LastGridMoveWasBlocked)
                            {
                                currentStepBlocked = true;
                                break;
                            }
                            else
                            {
                                LastGridMoveFailureReason =
                                    GridMoveFailureReason.Cancelled;
                            }
                            yield break;
                        }

                        timer += gameClock.DeltaTime;
                    }

                    if (currentStepBlocked)
                    {
                        break;
                    }

                    yield return null;
                }

                if (currentStepBlocked)
                {
                    if (TryRecoverBlockedActionPath(
                        expectedAction,
                        ref staleReplanAttempts,
                        out Queue<GridMoveStep> rebuiltPath,
                        out AIActionFailure failure))
                    {
                        path = rebuiltPath;
                        continue;
                    }

                    if (TryScheduleDeferredPathRecovery(
                            ref failure,
                            ref pathSearchDeferrals,
                            ref totalPathSearchDeferrals,
                            out pathRecoveryBackoffFrames,
                            out pathRecoveryDeferred))
                    {
                        deferredRecoveryFailure = failure;
                        continue;
                    }
                    CompleteBlockedActionPath(expectedAction, failure);
                    yield break;
                }

                if (TryRollbackForChangedGridBlock(
                    destination,
                    ref observedGridVersion,
                    startPosition,
                    reportToBrain: false))
                {
                    if (TryRecoverBlockedActionPath(
                        expectedAction,
                        ref staleReplanAttempts,
                        out Queue<GridMoveStep> rebuiltPath,
                        out AIActionFailure failure))
                    {
                        path = rebuiltPath;
                        continue;
                    }

                    if (TryScheduleDeferredPathRecovery(
                            ref failure,
                            ref pathSearchDeferrals,
                            ref totalPathSearchDeferrals,
                            out pathRecoveryBackoffFrames,
                            out pathRecoveryDeferred))
                    {
                        deferredRecoveryFailure = failure;
                        continue;
                    }
                    CompleteBlockedActionPath(expectedAction, failure);
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
                if (TryRecoverBlockedActionPath(
                    expectedAction,
                    ref staleReplanAttempts,
                    out Queue<GridMoveStep> rebuiltPath,
                    out AIActionFailure failure))
                {
                    path = rebuiltPath;
                    continue;
                }

                if (TryScheduleDeferredPathRecovery(
                        ref failure,
                        ref pathSearchDeferrals,
                        ref totalPathSearchDeferrals,
                        out pathRecoveryBackoffFrames,
                        out pathRecoveryDeferred))
                {
                    deferredRecoveryFailure = failure;
                    continue;
                }
                CompleteBlockedActionPath(expectedAction, failure);
                yield break;
            }

            if (traversalGuard.TryGetWalkStepBlockReason(
                    actor,
                    grid,
                    step,
                    out GridMoveFailureReason completedBlockReason))
            {
                SetGridMoveBlocked(completedBlockReason, reportToBrain: false);
                if (TryRecoverBlockedActionPath(
                    expectedAction,
                    ref staleReplanAttempts,
                    out Queue<GridMoveStep> rebuiltPath,
                    out AIActionFailure failure))
                {
                    path = rebuiltPath;
                    continue;
                }

                if (TryScheduleDeferredPathRecovery(
                        ref failure,
                        ref pathSearchDeferrals,
                        ref totalPathSearchDeferrals,
                        out pathRecoveryBackoffFrames,
                        out pathRecoveryDeferred))
                {
                    deferredRecoveryFailure = failure;
                    continue;
                }
                CompleteBlockedActionPath(expectedAction, failure);
                yield break;
            }
            completedPathSteps++;
            actor?.Brain?.NotifyMovementProgress(
                completedPathSteps,
                totalPathSteps);
            movementProgressCallback?.Invoke();
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
        actor?.Brain?.NotifyMovementTerminal(GridMoveFailureReason.None);
    }

    public IEnumerator MoveByStep(GridMoveStep step, AIAction expectedAction = null)
    {
        if (!TryBeginMovementOperation(
                "raw-step",
                out int operationVersion))
        {
            yield break;
        }
        yield return MoveByStepInternal(
            step,
            expectedAction,
            operationVersion);
        CompleteMovementOperation(operationVersion, "raw-step");
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

        if (!allowWorker
            && CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
            && !work.IsOffDuty)
        {
            actor.Brain?.ClearPathSearchCache();
            return;
        }

        if (enterDungeonRoutine != null)
        {
            StopCoroutine(enterDungeonRoutine);
            enterDungeonRoutine = null;
        }

        AIAction expectedAction = overrideKind == DoorAccessOverrideKind.None
            ? GetCurrentAction()
            : null;
        if (overrideKind != DoorAccessOverrideKind.None)
        {
            actor.SetLifecycleState(CharacterLifecycleState.ExitingDungeon);
        }
        if (overrideKind == DoorAccessOverrideKind.None)
        {
            StartTrackedActionMovement(ExitDungeon(overrideKind, expectedAction));
            return;
        }

        CancelActiveMovement();
        activeSystemMoveOverride = overrideKind;
        activeActionMovementRoutine = StartCoroutine(
            TrackActionMovement(ExitDungeon(overrideKind, expectedAction)));
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
        if (!TryFindIdleWanderPath(
                minDistance,
                maxDistance,
                out Queue<GridMoveStep> path,
                out _)
            || path == null
            || path.Count == 0)
        {
            return false;
        }

        AIAction expectedAction = GetCurrentAction();
        StartTrackedActionMovement(MoveByPathThenWait(path, waitDuration, expectedAction));
        return true;
    }

    public bool StartIdleWanderWithDeferredRecovery(
        float waitDuration,
        int minDistance = 2,
        int maxDistance = 8)
    {
        if (TryFindIdleWanderPath(
                minDistance,
                maxDistance,
                out Queue<GridMoveStep> path,
                out CharacterIdleWanderFailure failure)
            && path != null
            && path.Count > 0)
        {
            AIAction expectedAction = GetCurrentAction();
            StartTrackedActionMovement(
                MoveByPathThenWait(path, waitDuration, expectedAction));
            return true;
        }

        if (failure != CharacterIdleWanderFailure.Deferred)
        {
            return false;
        }

        AIAction deferredAction = GetCurrentAction();
        StartTrackedActionMovement(RetryIdleWanderAfterDeferral(
            deferredAction,
            waitDuration,
            minDistance,
            maxDistance));
        return true;
    }

    public void CancelActiveMovement(
        [System.Runtime.CompilerServices.CallerMemberName]
        string cancellationSource = "")
    {
        bool hadActiveMovement = activeActionMovementRoutine != null
            || activeManualMoveDestination.HasValue
            || activeSystemMoveDestination.HasValue
            || !string.IsNullOrWhiteSpace(activeMovementOperationOwner);
        if (hadActiveMovement)
        {
            LastMovementCancellationSourceForDiagnostics =
                cancellationSource ?? string.Empty;
            LastMovementOperationPreemptionForDiagnostics =
                $"{activeMovementOperationOwner}->cancel:{cancellationSource}";
        }
        if (activeActionMovementRoutine != null)
        {
            LastGridMoveWasBlocked = false;
            LastGridMoveFailureReason = GridMoveFailureReason.Cancelled;
            actor?.Brain?.NotifyMovementTerminal(
                GridMoveFailureReason.Cancelled);
        }
        InvalidateMovementOperation();
        protectedSystemMovementOperation = false;
        retainProtectedSystemMovementAfterCompletion = false;
        activeMovementOperationOwner = string.Empty;
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

    public bool TryCancelForImmediateAiReplan(
        [System.Runtime.CompilerServices.CallerMemberName]
        string cancellationSource = "")
    {
        if (HasProtectedSystemMovementOwnership)
        {
            return false;
        }

        CancelActiveMovement(cancellationSource);
        return true;
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
        protectedSystemMovementOperation = true;
        activeSystemMoveDestination = destination;
        activeSystemMoveOverride = overrideKind;
        activeActionMovementRoutine = StartCoroutine(
            TrackActionMovement(ExecuteSystemMove(path)));
        message = $"({destination.x}, {destination.y}) 칸으로 이동";
        return true;
    }

    [GameplayInternalOnly(
        "Protected domain actions own system movement until their terminal cleanup.",
        "WildlifeCaptureTransportAbilityUnityPort")]
    public bool TryStartProtectedSystemMove(
        Vector2Int destination,
        DoorAccessOverrideKind overrideKind,
        out string message)
    {
        bool started = TryStartSystemMove(
            destination,
            overrideKind,
            out message);
        if (started && actor != null && actor.GetNowXY() != destination)
        {
            retainProtectedSystemMovementAfterCompletion = true;
            protectedSystemMovementOperation = true;
        }
        return started;
    }

    [GameplayInternalOnly(
        "A protected domain resolver already owns an exact live route and passes it atomically to movement.",
        "WildlifeCaptureTransportAbilityUnityPort")]
    internal bool TryStartProtectedSystemMoveWithResolvedPath(
        Vector2Int destination,
        DoorAccessOverrideKind overrideKind,
        Queue<GridMoveStep> path,
        out string message)
    {
        CacheCommonReferences();
        if (actor == null || grid == null)
        {
            message = "Movement requires a live actor and grid.";
            return false;
        }

        if (!grid.IsValidGridPos(destination) || !grid.IsWalkable(destination))
        {
            message = "The resolved movement destination is not walkable.";
            return false;
        }

        Vector2Int start = grid.GetXY(transform.position);
        if (start == destination)
        {
            message = "The actor is already at the resolved destination.";
            return true;
        }

        Vector2Int expectedFrom = start;
        bool valid = path != null && path.Count > 0;
        if (valid)
        {
            foreach (GridMoveStep step in path)
            {
                if (!step.IsValid || step.From != expectedFrom)
                {
                    valid = false;
                    break;
                }
                expectedFrom = step.To;
            }
        }
        if (!valid || expectedFrom != destination)
        {
            message = "The resolved movement path is missing, stale, or targets a different destination.";
            return false;
        }

        CancelActiveMovement();
        protectedSystemMovementOperation = true;
        retainProtectedSystemMovementAfterCompletion = true;
        activeSystemMoveDestination = destination;
        activeSystemMoveOverride = overrideKind;
        activeActionMovementRoutine = StartCoroutine(
            TrackActionMovement(ExecuteSystemMove(path)));
        message = $"({destination.x}, {destination.y}) resolved protected movement";
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
        yield return MoveByPathOwned(
            path,
            expectedAction: null,
            movementProgressCallback: null,
            operationOwner: "protected-system-command");
        if (!retainProtectedSystemMovementAfterCompletion)
        {
            protectedSystemMovementOperation = false;
        }
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
            actor.Brain.EndExpectedAction(
                expectedAction,
                CharacterAiActionTerminalKind.Completed,
                clearFailures: true);
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
            actor.Brain.EndExpectedAction(
                expectedAction,
                CharacterAiActionTerminalKind.Completed,
                clearFailures: true);
        }
    }

    public bool TryFindIdleWanderPath(
        int minDistance,
        int maxDistance,
        out Queue<GridMoveStep> path)
    {
        return TryFindIdleWanderPath(
            minDistance,
            maxDistance,
            out path,
            out _);
    }

    public bool TryFindIdleWanderPath(
        int minDistance,
        int maxDistance,
        out Queue<GridMoveStep> path,
        out CharacterIdleWanderFailure failure)
    {
        CacheCommonReferences();
        if (actor == null)
        {
            path = null;
            failure = CharacterIdleWanderFailure.NoGrid;
            return false;
        }

        return RequireIdleWanderPlanner().TryFind(
            grid,
            actor.transform.position,
            GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor)),
            minDistance,
            maxDistance,
            out path,
            out failure);
    }

    private CharacterIdleWanderPlanner RequireIdleWanderPlanner()
    {
        IRandomStreamProvider provider = randomStreamProvider
            ?? throw new InvalidOperationException(
                $"{nameof(AbilityMove)} requires "
                + $"{nameof(IRandomStreamProvider)} injection.");
        CacheLocalReferences();
        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        if (idleWanderPlanner == null
            || movementRandom == null
            || !movementRandomCharacterId.Equals(characterId))
        {
            movementRandom = provider.Get(
                CharacterRandomStreamScopeIds.Movement(characterId));
            movementRandomCharacterId = characterId;
            idleWanderPlanner = new CharacterIdleWanderPlanner(
                pathSearchBroker,
                movementRandom);
        }

        return idleWanderPlanner;
    }

    private IEnumerator RetryIdleWanderAfterDeferral(
        AIAction expectedAction,
        float waitDuration,
        int minDistance,
        int maxDistance)
    {
        int maximumDeferrals = pathSearchDeferralLimit;
        for (int attempt = 1; attempt <= maximumDeferrals; attempt++)
        {
            int backoffFrames = 1 << Mathf.Min(attempt - 1, 4);
            float frameSeconds = gameClock != null
                ? Mathf.Max(0.001f, gameClock.DeltaTime)
                : 1f / 60f;
            actor?.Brain?.SetActionPhase(
                "Path search deferred",
                expectedAction?.destination,
                $"idle-wander attempt={attempt}; backoffFrames={backoffFrames}");
            actor?.Brain?.NotifyRetryScheduled(backoffFrames * frameSeconds);
            for (int frame = 0; frame < backoffFrames; frame++)
            {
                if (IsActionMovementCancelled(expectedAction))
                {
                    yield break;
                }
                RefreshCurrentActionReservation();
                yield return null;
            }

            actor?.Brain?.NotifyRetryAttempted();
            if (TryFindIdleWanderPath(
                    minDistance,
                    maxDistance,
                    out Queue<GridMoveStep> path,
                    out CharacterIdleWanderFailure failure))
            {
                yield return MoveByPathThenWait(path, waitDuration, expectedAction);
                yield break;
            }
            if (failure != CharacterIdleWanderFailure.Deferred)
            {
                yield return WaitForAiAction(waitDuration, expectedAction);
                yield break;
            }
        }

        if (expectedAction == null || actor?.Brain == null)
        {
            yield break;
        }
        AIActionFailure starved = AIActionFailure.Create(
            AIActionFailureKind.PathSearchStarved,
            $"Idle wander path search remained deferred for {maximumDeferrals} attempts.",
            expectedAction.destination);
        actor.Brain.ReportRuntimeActionFailure(starved, requestImmediateReplan: false);
        actor.Brain.EndExpectedAction(
            expectedAction,
            CharacterAiActionTerminalKind.Failed,
            clearFailures: false);
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
        out Queue<GridMoveStep> rebuiltPath,
        out AIActionFailure failure)
    {
        rebuiltPath = null;
        failure = AIActionFailure.None;
        if (action == null
            || actor == null
            || actor.Brain == null)
        {
            return false;
        }

        actor.Brain.ClearPathSearchCache();
        if (!action.TryRebuildPathFromCurrentPosition(actor, out failure))
        {
            actor.Brain.SetActionPhase("\uACBD\uB85C \uC7AC\uD0D0\uC0C9 \uC2E4\uD328", action.destination, failure.ToString());
            return false;
        }

        if (action.pathSteps.Count == 0)
        {
            actor.Brain.SetActionPhase("\uB3C4\uCC29", action.destination, action.planKind.ToString());
            rebuiltPath = new Queue<GridMoveStep>();
            return action.planKind == AIActionPlanKind.DestinationOnly;
        }

        rebuiltPath = new Queue<GridMoveStep>(action.pathSteps);
        actor.Brain.SetActionPhase(
            "\uACBD\uB85C \uC7AC\uD0D0\uC0C9",
            action.destination,
            $"{action.planKind} / {action.pathSteps.Count}\uCE78");
        return rebuiltPath.Count > 0;
    }

    private bool TryRecoverBlockedActionPath(
        AIAction expectedAction,
        ref int replanAttempts,
        out Queue<GridMoveStep> rebuiltPath,
        out AIActionFailure failure)
    {
        rebuiltPath = null;
        failure = AIActionFailure.Create(
            AIActionFailureKind.NoPath,
            $"Committed action movement was blocked ({LastGridMoveFailureReason}).",
            expectedAction?.destination);
        if (expectedAction == null || replanAttempts >= 1)
        {
            return false;
        }

        if (!TryReplanCurrentActionPath(
                expectedAction,
                out rebuiltPath,
                out AIActionFailure rebuildFailure))
        {
            if (rebuildFailure.HasFailure)
            {
                failure = rebuildFailure;
            }
            return false;
        }

        replanAttempts++;
        runtimeActionPathReplanCount++;
        LastGridMoveWasBlocked = false;
        LastGridMoveFailureReason = GridMoveFailureReason.None;
        if (actor?.Brain != null
            && ReferenceEquals(actor.Brain.bestAction, expectedAction))
        {
            actor.Brain.isBestActionEnd = false;
        }
        return true;
    }

    private bool TryScheduleDeferredPathRecovery(
        ref AIActionFailure failure,
        ref int pathSearchDeferrals,
        ref int totalPathSearchDeferrals,
        out int backoffFrames,
        out bool recoveryDeferred)
    {
        backoffFrames = 0;
        recoveryDeferred = failure.Kind ==
            AIActionFailureKind.PathSearchDeferred;
        if (!recoveryDeferred)
        {
            return false;
        }

        pathSearchDeferrals++;
        totalPathSearchDeferrals++;
        if (totalPathSearchDeferrals >= pathSearchDeferralLimit)
        {
            recoveryDeferred = false;
            failure = AIActionFailure.Create(
                AIActionFailureKind.PathSearchStarved,
                $"Path search remained deferred for {totalPathSearchDeferrals} attempts.",
                failure.Target);
            return false;
        }

        backoffFrames = 1 << Mathf.Min(pathSearchDeferrals - 1, 4);
        float frameSeconds = gameClock != null
            ? Mathf.Max(0.001f, gameClock.DeltaTime)
            : 1f / 60f;
        actor?.Brain?.SetActionPhase(
            "Path search deferred",
            failure.Target,
            $"attempt={totalPathSearchDeferrals}; backoffFrames={backoffFrames}");
        actor?.Brain?.NotifyRetryScheduled(backoffFrames * frameSeconds);
        return true;
    }

    private void CompleteBlockedActionPath(
        AIAction expectedAction,
        AIActionFailure failure)
    {
        actor?.Brain?.NotifyMovementTerminal(LastGridMoveFailureReason);
        GridMoveBlockedResponder.Respond(actor, grid, transform.position);
        if (expectedAction == null || actor?.Brain == null)
        {
            return;
        }

        runtimeActionPathFailureCount++;
        AIActionFailure terminalFailure = failure.HasFailure
            ? failure
            : AIActionFailure.Create(
                AIActionFailureKind.NoPath,
                $"Committed action movement was blocked ({LastGridMoveFailureReason}).",
                expectedAction.destination);
        expectedAction.ReleaseReservation(actor);
        actor.Brain.ReportRuntimeActionFailure(
            terminalFailure,
            requestImmediateReplan: false);
        actor.Brain.EndExpectedAction(
            expectedAction,
            CharacterAiActionTerminalKind.Failed,
            clearFailures: false);
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
        if (expectedAction == null)
            return false;
        string reason = actor == null
            ? "actor-null"
            : actor.Brain == null
                ? "brain-null"
                : actor.Brain.bestAction != expectedAction
                    ? "best-action-replaced"
                    : actor.Brain.isBestActionEnd
                        ? "decision-pending"
                        : string.Empty;
        if (reason.Length == 0)
            return false;
        LastActionMovementCancellationReasonForDiagnostics = reason;
        return true;
    }

    private bool TryBeginMovementOperation(
        string operationOwner,
        out int operationVersion)
    {
        string normalizedOwner = string.IsNullOrWhiteSpace(operationOwner)
            ? "unknown"
            : operationOwner;
        if (protectedSystemMovementOperation
            && !string.Equals(
                normalizedOwner,
                "protected-system-command",
                StringComparison.Ordinal))
        {
            LastRejectedMovementOperationOwnerForDiagnostics =
                normalizedOwner;
            operationVersion = movementOperationVersion;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(activeMovementOperationOwner))
        {
            LastMovementOperationPreemptionForDiagnostics =
                $"{activeMovementOperationOwner}->{normalizedOwner}";
        }
        LastMovementCancellationSourceForDiagnostics = string.Empty;
        LastRejectedMovementOperationOwnerForDiagnostics = string.Empty;
        InvalidateMovementOperation();
        activeMovementOperationOwner = normalizedOwner;
        operationVersion = movementOperationVersion;
        return true;
    }

    private void CompleteMovementOperation(
        int operationVersion,
        string operationOwner)
    {
        if (operationVersion == movementOperationVersion
            && string.Equals(
                activeMovementOperationOwner,
                operationOwner,
                StringComparison.Ordinal))
        {
            activeMovementOperationOwner = string.Empty;
        }
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
            actor.Brain.EndExpectedAction(
                expectedAction,
                CharacterAiActionTerminalKind.Completed,
                clearFailures: true);
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

    private IEnumerator ExitDungeon(
        DoorAccessOverrideKind overrideKind,
        AIAction expectedAction)
    {
        if (grid == null)
        {
            CacheCommonReferences();
        }

        if (grid == null)
        {
            if (actor != null
                && actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon)
            {
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }
            activeSystemMoveDestination = null;
            activeSystemMoveOverride = DoorAccessOverrideKind.None;
            FailExitAction(expectedAction, AIActionFailureKind.NoGrid,
                "exit-dungeon-grid-unavailable");
            yield break;
        }

        TryResolveSpawner();
        if (spawner == null
            || !spawner.TryGetEntryGridPosition(out Vector2Int exitGridPosition))
        {
            if (actor != null
                && actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon)
            {
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }

            activeSystemMoveDestination = null;
            activeSystemMoveOverride = DoorAccessOverrideKind.None;
            FailExitAction(expectedAction, AIActionFailureKind.NoDestination,
                "exit-dungeon-spawner-unavailable");
            yield break;
        }

        bool reachedExit = actor != null
            && grid.GetXY(actor.transform.position) == exitGridPosition;
        int counter = 0;
        int pathSearchDeferrals = 0;
        int maximumPathSearchDeferrals = pathSearchDeferralLimit;
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
                pathSearchDeferrals++;
                if (pathSearchDeferrals >= maximumPathSearchDeferrals)
                {
                    FailExitAction(
                        expectedAction,
                        AIActionFailureKind.PathSearchStarved,
                        $"exit path search remained deferred for {pathSearchDeferrals} attempts");
                    activeSystemMoveDestination = null;
                    activeSystemMoveOverride = DoorAccessOverrideKind.None;
                    yield break;
                }
                int backoffFrames = 1 << Mathf.Min(pathSearchDeferrals - 1, 4);
                actor?.Brain?.SetActionPhase(
                    "Path search deferred",
                    detail: $"exit attempt={pathSearchDeferrals}; backoffFrames={backoffFrames}");
                actor?.Brain?.NotifyRetryScheduled(backoffFrames * Mathf.Max(
                    0.001f,
                    gameClock?.DeltaTime ?? 1f / 60f));
                for (int frame = 0; frame < backoffFrames; frame++)
                {
                    if (IsActionMovementCancelled(expectedAction))
                    {
                        yield break;
                    }
                    yield return null;
                }
                actor?.Brain?.NotifyRetryAttempted();
                continue;
            }

            pathSearchDeferrals = 0;

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
            if (actor != null
                && actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon)
            {
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }

            activeSystemMoveDestination = null;
            activeSystemMoveOverride = DoorAccessOverrideKind.None;
            FailExitAction(expectedAction, AIActionFailureKind.NoPath,
                "exit-dungeon-path-unreachable");
            yield break;
        }

        if (spawner != null)
        {
            yield return Move2PosBySpeed(spawner.GetEntryDoorWorldPosition());
            yield return Move2PosBySpeed(spawner.GetOutsideSpawnWorldPosition());

            // The authored exit action owns traversal through the outside spawn
            // point. Closing the action or entering a non-Active lifecycle any
            // earlier releases this very movement coroutine and strands the
            // visitor at the dungeon threshold. Transfer the movement handle
            // before lifecycle cleanup, then perform the spawner handoff eagerly
            // in the same terminal frame.
            if (expectedAction != null && actor?.Brain != null)
            {
                actor.Brain.EndExpectedAction(
                    expectedAction,
                    CharacterAiActionTerminalKind.Completed,
                    clearFailures: true);
            }
            if (actor != null)
            {
                activeActionMovementRoutine = null;
                actor.SetLifecycleState(CharacterLifecycleState.ExitingDungeon);
            }

            IEnumerator interaction = spawner.Interact(actor);
            if (interaction != null)
            {
                yield return interaction;
            }
        }
        else if (actor != null)
        {
            actor.SetLifecycleState(CharacterLifecycleState.Active);
        }

        activeSystemMoveDestination = null;
        activeSystemMoveOverride = DoorAccessOverrideKind.None;
    }

    private void FailExitAction(
        AIAction expectedAction,
        AIActionFailureKind kind,
        string detail)
    {
        if (expectedAction == null || actor?.Brain == null)
        {
            return;
        }
        actor.Brain.ReportRuntimeActionFailure(
            AIActionFailure.Create(kind, detail, expectedAction.destination),
            requestImmediateReplan: false);
        actor.Brain.EndExpectedAction(
            expectedAction,
            CharacterAiActionTerminalKind.Failed,
            clearFailures: false);
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
                // Frame stride throttles presentation updates; it must not
                // slow authoritative movement. Account for every skipped
                // frame's game time so population size cannot stretch travel
                // duration in proportion to the scheduler stride.
                timer += gameClock.DeltaTime;
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
        Vector3 blockedFallbackPosition,
        bool reportToBrain = true)
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
            SetGridMoveBlocked(reason, reportToBrain);
        }
        return blocked;
    }

    private void SetGridMoveBlocked(
        GridMoveFailureReason reason = GridMoveFailureReason.WallBlocked,
        bool reportToBrain = true)
    {
        LastGridMoveWasBlocked = true;
        if (LastGridMoveFailureReason == GridMoveFailureReason.None)
        {
            LastGridMoveFailureReason = reason;
        }
        if (reportToBrain)
        {
            actor?.Brain?.NotifyMovementTerminal(LastGridMoveFailureReason);
            GridMoveBlockedResponder.Respond(actor, grid, transform.position);
        }
    }
}
