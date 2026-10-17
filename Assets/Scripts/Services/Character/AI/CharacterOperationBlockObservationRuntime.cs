using System;
using DungeonStory.Foundation;

internal sealed class CharacterOperationBlockObservationRuntime
{
    private CharacterOperationBlockSnapshot currentOperationBlock;
    private CharacterOperationBlockSnapshot recentOperationFailure;
    private AIActionSet currentOperationBlockActionSet;
    private BuildableObject currentOperationBlockTarget;
    private bool currentOperationBlockUsesCooldown;
    private Func<int> currentOperationBlockSourceRevisionReader;
    private int currentOperationBlockSourceRevision;

    internal CharacterOperationBlockSnapshot CaptureCurrentOperationBlock(
        AIBrainActionEvaluator actionEvaluator,
        bool actionEpochLive,
        long currentActionEpoch,
        AIAction bestAction)
    {
        CharacterOperationBlockSnapshot snapshot = currentOperationBlock;
        if (!snapshot.HasCurrentBlock
            || ReferenceEquals(currentOperationBlockTarget, null)
            || currentOperationBlockTarget == null
            || currentOperationBlockTarget.isDestroy
            || !currentOperationBlockTarget.PersistentInstanceId.IsValid
            || !string.Equals(
                currentOperationBlockTarget.PersistentInstanceId.Value,
                snapshot.TargetStableId,
                StringComparison.Ordinal))
        {
            return default;
        }

        if (currentOperationBlockUsesCooldown)
        {
            if (actionEvaluator == null
                || !actionEvaluator.TryGetCooldownExpiry(
                    currentOperationBlockActionSet,
                    currentOperationBlockTarget,
                    out float expiresAt))
            {
                return default;
            }

            snapshot = snapshot.WithCooldownExpiry(expiresAt);
        }

        bool sourceCurrent = currentOperationBlockSourceRevisionReader != null
            ? currentOperationBlockSourceRevisionReader()
                == currentOperationBlockSourceRevision
            : actionEpochLive
                && currentActionEpoch == snapshot.ActionEpoch
                && bestAction?.actionset == currentOperationBlockActionSet
                && ReferenceEquals(
                    bestAction.destination,
                    currentOperationBlockTarget);
        return sourceCurrent
            ? snapshot
            : snapshot.WithCurrentnessUnconfirmed();
    }

    internal CharacterOperationBlockSnapshot RecentOperationFailure => recentOperationFailure;

    internal bool Record(
        AIActionSet actionSet,
        AIActionFailure failure,
        DomainFailure domainFailure,
        CharacterOperationBlockAxis axis,
        CharacterAiRetryKind retryKind,
        bool usesCooldown,
        bool recordHistory,
        CharacterActor actor,
        AIAction bestAction,
        long currentActionEpoch,
        AIBrainActionEvaluator actionEvaluator,
        IGameClock gameClock)
    {
        BuildableObject target = !ReferenceEquals(failure.Target, null)
            ? failure.Target
            : bestAction?.destination;
        if (actionSet == null
            || !failure.HasFailure
            || ReferenceEquals(target, null)
            || target == null
            || target.isDestroy
            || !target.PersistentInstanceId.IsValid
            || currentActionEpoch <= 0L)
        {
            return false;
        }

        WorkTypeId workTypeId = ResolveObservedWorkTypeId(actor, actionSet);
        CharacterOperationBlockAxis resolvedAxis = axis !=
            CharacterOperationBlockAxis.Unknown
                ? axis
                : CharacterOperationBlockClassification.Classify(
                    domainFailure,
                    failure.Kind);
        float expiresAt = 0f;
        bool hasKnownExpiry = usesCooldown
            && actionEvaluator != null
            && actionEvaluator.TryGetCooldownExpiry(
                actionSet,
                target,
                out expiresAt);
        currentOperationBlock = new CharacterOperationBlockSnapshot(
            ResolveActionStableId(actionSet, workTypeId),
            actionSet.GetDisplayLabel(),
            target.PersistentInstanceId.Value,
            AIBrainDebugFormatter.GetDestinationLabel(target),
            currentActionEpoch,
            failure,
            domainFailure,
            resolvedAxis,
            retryKind,
            isCurrent: true,
            hasKnownExpiry: hasKnownExpiry,
            expiresAt: hasKnownExpiry ? expiresAt : 0f,
            observedAt: gameClock != null ? gameClock.Time : 0f,
            isCurrentnessConfirmed: true);
        currentOperationBlockActionSet = actionSet;
        currentOperationBlockTarget = target;
        currentOperationBlockUsesCooldown = usesCooldown;
        currentOperationBlockSourceRevisionReader = null;
        currentOperationBlockSourceRevision = 0;
        if (recordHistory)
        {
            recentOperationFailure = currentOperationBlock.AsHistory();
        }

        return true;
    }

    internal void ClearCurrentOperationBlock()
    {
        currentOperationBlock = default;
        currentOperationBlockActionSet = null;
        currentOperationBlockTarget = null;
        currentOperationBlockUsesCooldown = false;
        currentOperationBlockSourceRevisionReader = null;
        currentOperationBlockSourceRevision = 0;
    }

    internal void ClearAllOperationDiagnostics()
    {
        ClearCurrentOperationBlock();
        recentOperationFailure = default;
    }

    internal void ClearCurrentOperationBlockFor(AIAction action)
    {
        if (!currentOperationBlock.HasCurrentBlock || action == null)
        {
            return;
        }

        if (currentOperationBlockActionSet == action.actionset)
        {
            ClearCurrentOperationBlock();
        }
    }

    private static WorkTypeId ResolveObservedWorkTypeId(CharacterActor actor, AIActionSet actionSet)
    {
        if (actor != null
            && actor.TryGetAbility(out AbilityWork work)
            && work.AssignedWorkTypeId.IsValid)
        {
            return work.AssignedWorkTypeId;
        }

        return actionSet is AIWork aiWork && aiWork.WorkTypeId.IsValid
            ? aiWork.WorkTypeId
            : default;
    }

    private static string ResolveActionStableId(
        AIActionSet actionSet,
        WorkTypeId workTypeId)
    {
        return workTypeId.IsValid
            ? workTypeId.Value
            : "ai-action:" + (actionSet?.GetType().FullName ?? "unknown");
    }

    internal void BindSourceRevision(Func<int> reader, int revision)
    {
        currentOperationBlockSourceRevisionReader = reader;
        currentOperationBlockSourceRevision = revision;
    }

    internal void RetireForDifferentDestination(BuildableObject destination)
    {
        if (destination != null
            && destination.PersistentInstanceId.IsValid
            && currentOperationBlock.HasCurrentBlock
            && !string.Equals(
                currentOperationBlock.TargetStableId,
                destination.PersistentInstanceId.Value,
                StringComparison.Ordinal))
        {
            ClearCurrentOperationBlock();
        }
    }
}
