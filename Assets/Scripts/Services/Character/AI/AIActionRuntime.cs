using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Serialization;

public enum AIActionPlanKind
{
    None,
    NoDestination,
    DestinationOnly,
    MovePath
}

public sealed class AIActionPlan
{
    private static readonly IReadOnlyList<GridMoveStep> EmptyPath =
        ReadOnlyView.List(Array.Empty<GridMoveStep>());

    private AIActionPlan(
        AIActionPlanKind kind,
        BuildableObject destination,
        IEnumerable<GridMoveStep> pathSteps)
    {
        GridMoveStep[] path = pathSteps?.ToArray() ?? Array.Empty<GridMoveStep>();
        if (path.Any((step) => !step.IsValid))
        {
            throw new ArgumentException("An AI action path cannot contain null steps.", nameof(pathSteps));
        }

        bool valid = kind switch
        {
            AIActionPlanKind.None => destination == null && path.Length == 0,
            AIActionPlanKind.NoDestination => destination == null && path.Length == 0,
            AIActionPlanKind.DestinationOnly => destination != null && path.Length == 0,
            AIActionPlanKind.MovePath => destination != null && path.Length > 0,
            _ => false
        };
        if (!valid)
        {
            throw new ArgumentException(
                $"Invalid AI action plan: kind={kind}, destination={destination != null}, steps={path.Length}.");
        }

        Kind = kind;
        Destination = destination;
        PathSteps = path.Length == 0 ? EmptyPath : ReadOnlyView.List(path);
    }

    public static AIActionPlan None { get; } =
        new AIActionPlan(AIActionPlanKind.None, null, null);

    public static AIActionPlan WithoutDestination { get; } =
        new AIActionPlan(AIActionPlanKind.NoDestination, null, null);

    public AIActionPlanKind Kind { get; }
    public BuildableObject Destination { get; }
    public IReadOnlyList<GridMoveStep> PathSteps { get; }

    public static AIActionPlan AtDestination(BuildableObject destination)
    {
        return new AIActionPlan(AIActionPlanKind.DestinationOnly, destination, null);
    }

    public static AIActionPlan MoveTo(
        BuildableObject destination,
        IEnumerable<GridMoveStep> pathSteps)
    {
        return new AIActionPlan(AIActionPlanKind.MovePath, destination, pathSteps);
    }
}

[Serializable]
public class AIAction
{
    private static readonly Dictionary<Type, ProfilerMarker> ConsiderationTypeMarkers =
        new Dictionary<Type, ProfilerMarker>();
    public AIActionSet actionset;
    private float _score;
    public float score
    {
        get { return _score; }
        set
        {
            _score = Mathf.Clamp01(value);
        }
    }
    private AIActionPlan plan = AIActionPlan.None;
    public float startedAt = -1f;
    private AIActionSet reservedActionSet;
    private BuildableObject reservedDestination;
    [NonSerialized] private IGameClock gameClock;
    public bool HasStarted => startedAt >= 0f;
    public float RunningSeconds => HasStarted && gameClock != null
        ? Mathf.Max(0f, gameClock.Time - startedAt)
        : 0f;
    // Unity's overloaded null operator reports a destroyed destination as null.
    // The reservation still exists until its owner releases it, so using that
    // operator here orphaned both the facility reservation and the diagnostic
    // live-reservation counter when a work target was destroyed mid-action.
    public bool HasReservation => !ReferenceEquals(reservedActionSet, null)
        && !ReferenceEquals(reservedDestination, null);
    public BuildableObject ReservedDestination => reservedDestination;
    public AIActionPlan Plan => plan;
    public BuildableObject destination => plan.Destination;
    public IReadOnlyList<GridMoveStep> pathSteps => plan.PathSteps;
    public AIActionPlanKind planKind => plan.Kind;

    public AIAction()
    {
    }

    public AIAction(AIActionSet actionset, AIActionPlan initialPlan)
    {
        this.actionset = actionset;
        plan = initialPlan ?? throw new ArgumentNullException(nameof(initialPlan));
    }

    public void MarkStarted(float time)
    {
        startedAt = time;
    }

    public void BindClock(IGameClock gameClock)
    {
        this.gameClock = gameClock;
    }

    public float CalculateScore(CharacterActor actor)
    {
        CharacterAiDecisionContext context = default;
        return CalculateScoreCore(actor, false, in context);
    }

    public float CalculateScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        return CalculateScoreCore(actor, true, in context);
    }

    private float CalculateScoreCore(
        CharacterActor actor,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context)
    {
        if (actionset == null)
        {
            this.score = 0f;
            return this.score;
        }

        ICharacterAiPerformanceRecorder recorder = actor?.Brain?.PerformanceRecorder;
        if (hasDecisionContext)
        {
            actionset.PrepareScoreContext(actor, in context);
        }

        if (actionset.considerations == null || actionset.considerations.Length == 0)
        {
            float baseScore = EvaluatePersonalityScore(actor, actionset, recorder);
            this.score = EvaluateAdjustedScore(
                actor,
                actionset,
                recorder,
                hasDecisionContext,
                in context,
                baseScore);
            return this.score;
        }

        int considerationCount = actionset.considerations.Length;
        float totalScore = 0f;
        foreach (var consideration in actionset.considerations)
        {
            if (consideration == null)
            {
                this.score = 0f;
                return this.score;
            }

            long considerationStarted = recorder?.SlowTraceEnabled == true
                ? System.Diagnostics.Stopwatch.GetTimestamp()
                : 0L;
            float considerationScore;
            using (GetConsiderationMarker(consideration.GetType()).Auto())
            {
                considerationScore = Mathf.Clamp01(consideration.ScoreConsideration(actor));
            }
            if (considerationStarted != 0L)
            {
                double elapsedMilliseconds =
                    (System.Diagnostics.Stopwatch.GetTimestamp() - considerationStarted)
                    * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency;
                recorder.RecordSlowOperation(
                    "consideration",
                    actor,
                    actionset,
                    consideration,
                    elapsedMilliseconds);
            }
            if (considerationScore <= 0f)
            {
                this.score = 0;
                return this.score;
            }

            totalScore += considerationScore;
        }

        float actionScore = totalScore / considerationCount;
        actionScore *= EvaluatePersonalityScore(actor, actionset, recorder);
        this.score = EvaluateAdjustedScore(
            actor,
            actionset,
            recorder,
            hasDecisionContext,
            in context,
            actionScore);
        return this.score;
    }

    private static ProfilerMarker GetConsiderationMarker(Type type)
    {
        if (ConsiderationTypeMarkers.TryGetValue(type, out ProfilerMarker marker))
        {
            return marker;
        }

        marker = new ProfilerMarker("CharacterAi.Consideration." + type.Name);
        ConsiderationTypeMarkers[type] = marker;
        return marker;
    }

    private static float EvaluatePersonalityScore(
        CharacterActor actor,
        AIActionSet actionSet,
        ICharacterAiPerformanceRecorder recorder)
    {
        if (recorder?.SlowTraceEnabled != true)
        {
            return CharacterAiPersonalityUtility.GetActionScoreMultiplier(
                actor,
                actionSet);
        }

        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        float value = CharacterAiPersonalityUtility.GetActionScoreMultiplier(
            actor,
            actionSet);
        double elapsedMilliseconds =
            (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
        recorder.RecordSlowOperation(
            "personality",
            actor,
            actionSet,
            null,
            elapsedMilliseconds);
        return value;
    }

    private static float EvaluateAdjustedScore(
        CharacterActor actor,
        AIActionSet actionSet,
        ICharacterAiPerformanceRecorder recorder,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context,
        float baseScore)
    {
        if (recorder?.SlowTraceEnabled != true)
        {
            return hasDecisionContext
                ? actionSet.AdjustScore(actor, in context, baseScore)
                : actionSet.AdjustScore(actor, baseScore);
        }

        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        float value = hasDecisionContext
            ? actionSet.AdjustScore(actor, in context, baseScore)
            : actionSet.AdjustScore(actor, baseScore);
        double elapsedMilliseconds =
            (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
        recorder.RecordSlowOperation(
            "adjust-score",
            actor,
            actionSet,
            null,
            elapsedMilliseconds);
        return value;
    }

    public bool SetDestinationWithFailure(CharacterActor actor, out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        if (actor == null || actor.Brain == null || actionset == null)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.NoGrid,
                "AI \uB610\uB294 \uADF8\uB9AC\uB4DC \uC5C6\uC74C");
            return false;
        }

        if (!actionset.TryResolveDestinationWithFailure(
                actor,
                null,
                out BuildableObject resolvedDestination,
                out failure))
        {
            return false;
        }

        return SetResolvedDestinationWithFailure(
            actor,
            resolvedDestination,
            out failure);
    }

    public bool SetResolvedDestinationWithFailure(
        CharacterActor actor,
        BuildableObject resolvedDestination,
        out AIActionFailure failure)
    {
        ReleaseReservation(actor);
        plan = AIActionPlan.None;
        startedAt = -1f;
        failure = AIActionFailure.None;
        if (actor == null
            || actor.Brain == null
            || actionset == null
            || !actor.Brain.TryGetRuntimeGrid(out Grid grid))
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoGrid, "AI \uB610\uB294 \uADF8\uB9AC\uB4DC \uC5C6\uC74C");
            return false;
        }

        if (resolvedDestination == null)
        {
            plan = AIActionPlan.WithoutDestination;
            return !actionset.RequiresDestination;
        }

        if (resolvedDestination.isDestroy)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.Destroyed,
                "\uBAA9\uD45C \uD30C\uAD34\uB428",
                resolvedDestination);
            return false;
        }

        if (IsCharacterAtDestination(actor, resolvedDestination))
        {
            plan = AIActionPlan.AtDestination(resolvedDestination);
            return TryReserveResolvedDestination(actor, resolvedDestination, out failure);
        }

        Vector2Int pathDestination = ResolveNearestDestinationCell(
            actor,
            grid,
            actor.GetNowXY(),
            resolvedDestination);
        int pathRequestId = actor.Brain.NotifyPathRequested(
            repath: false,
            branch: actionset.Branch);
        Queue<GridMoveStep> resolvedPath = actor.PathSearchBroker?.GetMovePathTo(
            grid,
            actor.GetNowXY(),
            pathDestination,
            GridPathSearchPriority.Normal,
            GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor)));
        if (resolvedPath == null)
        {
            actor.Brain.NotifyPathResult(
                pathRequestId,
                CharacterAiPathTraceState.Deferred,
                0);
            failure = AIActionFailure.Create(
                AIActionFailureKind.PathSearchDeferred,
                "경로 탐색 예산 대기",
                resolvedDestination);
            return false;
        }

        bool pathResolved = ResolvePathPlan(
            actor,
            resolvedDestination,
            resolvedPath,
            out failure);
        actor.Brain.NotifyPathResult(
            pathRequestId,
            pathResolved
                ? CharacterAiPathTraceState.Found
                : CharacterAiPathTraceState.NoPath,
            resolvedPath.Count);
        return pathResolved
            && TryReserveResolvedDestination(actor, resolvedDestination, out failure);
    }

    private Vector2Int ResolveNearestDestinationCell(
        CharacterActor actor,
        Grid grid,
        Vector2Int start,
        BuildableObject destination)
    {
        if (actionset is AIWork && destination != null)
        {
            GridPathSearchResult workSearch = actor?.Brain?.GetPathSearch(actor);
            if (WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                    destination,
                    workSearch,
                    out Vector2Int reachableWorkAccess))
            {
                return reachableWorkAccess;
            }

            if (destination.TryGetNearestWorkAccessGridPosition(
                    grid,
                    start,
                    out Vector2Int workAccess))
            {
                return workAccess;
            }
        }

        IReadOnlyList<Vector2Int> positions = destination?.buildPoses;
        if (positions == null || positions.Count == 0)
        {
            return destination != null ? destination.centerPos : start;
        }

        Vector2Int best = positions[0];
        int bestEstimate = EstimateDestinationCost(start, best);
        for (int index = 1; index < positions.Count; index++)
        {
            Vector2Int candidate = positions[index];
            int estimate = EstimateDestinationCost(start, candidate);
            if (estimate < bestEstimate)
            {
                best = candidate;
                bestEstimate = estimate;
            }
        }

        return best;
    }

    private static int EstimateDestinationCost(Vector2Int start, Vector2Int destination)
    {
        int horizontal = Mathf.Abs(start.x - destination.x)
            * DefaultGridTraversalCostPolicy.DryWalkCost;
        int floorChanges = Mathf.Abs(start.y - destination.y);
        return horizontal
            + floorChanges * DefaultGridTraversalCostPolicy.StairFallbackCost;
    }

    public bool TryRebuildPathFromCurrentPosition(CharacterActor actor, out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        BuildableObject currentDestination = destination;
        plan = AIActionPlan.None;
        if (actor == null
            || actor.Brain == null
            || actionset == null
            || !actor.Brain.TryGetRuntimeGrid(out Grid grid))
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoGrid, "AI \uB610\uB294 \uADF8\uB9AC\uB4DC \uC5C6\uC74C");
            return false;
        }

        if (currentDestination == null)
        {
            plan = AIActionPlan.WithoutDestination;
            return !actionset.RequiresDestination;
        }

        if (currentDestination.isDestroy)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.Destroyed,
                "\uBAA9\uD45C \uD30C\uAD34\uB428",
                currentDestination);
            return false;
        }

        Vector2Int pathDestination = ResolveNearestDestinationCell(
            actor,
            grid,
            actor.GetNowXY(),
            currentDestination);
        int pathRequestId = actor.Brain.NotifyPathRequested(
            repath: true,
            branch: actionset.Branch);
        Queue<GridMoveStep> rebuiltPath = actor.PathSearchBroker?.GetMovePathTo(
                grid,
                actor.GetNowXY(),
                pathDestination,
                GridPathSearchPriority.Urgent,
                GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor)));
        if (rebuiltPath == null)
        {
            actor.Brain.NotifyPathResult(
                pathRequestId,
                CharacterAiPathTraceState.Deferred,
                0);
            // Deferred is scheduler backpressure, not a topology verdict.
            // Preserve the committed target for the next bounded retry.
            plan = AIActionPlan.AtDestination(currentDestination);
            failure = AIActionFailure.Create(
                AIActionFailureKind.PathSearchDeferred,
                "경로 탐색 예산 대기",
                currentDestination);
            return false;
        }

        bool pathResolved = ResolvePathPlan(
            actor,
            currentDestination,
            rebuiltPath,
            out failure);
        actor.Brain.NotifyPathResult(
            pathRequestId,
            pathResolved
                ? CharacterAiPathTraceState.Found
                : CharacterAiPathTraceState.NoPath,
            rebuiltPath.Count);
        return pathResolved;
    }

    public void RefreshReservation(CharacterActor actor)
    {
        if (!HasReservation)
        {
            return;
        }

        reservedActionSet.RefreshDestinationReservation(actor, reservedDestination);
        actor?.Brain?.NotifyReservationRefreshed(this);
    }

    public void ReleaseReservation(CharacterActor actor)
    {
        if (!HasReservation)
        {
            return;
        }

        AIActionSet releasingActionSet = reservedActionSet;
        BuildableObject releasingDestination = reservedDestination;
        releasingActionSet.ReleaseDestinationReservation(actor, releasingDestination);
        reservedActionSet = null;
        reservedDestination = null;
        actor?.Brain?.NotifyReservationReleased(this);
    }

    private bool TryReserveResolvedDestination(
        CharacterActor actor,
        BuildableObject destination,
        out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        if (actionset == null || destination == null)
        {
            return true;
        }

        if (!actionset.TryReserveDestination(actor, destination, out failure))
        {
            actor?.Brain?.NotifyReservationFailed(this);
            if (failure.Target == null)
            {
                failure = new AIActionFailure(failure.Kind, failure.Reason, destination);
            }

            plan = AIActionPlan.None;
            return false;
        }

        reservedActionSet = actionset;
        reservedDestination = destination;
        actor?.Brain?.NotifyReservationAcquired(this);
        return true;
    }

    private bool ResolvePathPlan(
        CharacterActor actor,
        BuildableObject destination,
        Queue<GridMoveStep> pathSteps,
        out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        if (pathSteps != null && pathSteps.Count > 0)
        {
            plan = AIActionPlan.MoveTo(destination, pathSteps);
            return true;
        }

        if (IsCharacterAtDestination(actor, destination))
        {
            plan = AIActionPlan.AtDestination(destination);
            return true;
        }

        failure = AIActionFailure.Create(AIActionFailureKind.NoPath, "\uACBD\uB85C \uC5C6\uC74C", destination);
        plan = AIActionPlan.None;
        return false;
    }

    private bool IsCharacterAtDestination(CharacterActor actor, BuildableObject destination)
    {
        if (actor == null
            || actor.Brain == null
            || destination == null
            || !actor.Brain.TryGetRuntimeGrid(out Grid grid))
        {
            return false;
        }

        Vector2Int actorPosition = actor.GetNowXY();
        return actionset is AIWork
            ? destination.IsWorkAccessGridPosition(grid, actorPosition)
            : destination.ContainsGridPosition(actorPosition);
    }
}
