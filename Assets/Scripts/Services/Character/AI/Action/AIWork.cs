using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Work", order = 0)]
public class AIWork : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.Work,
        "작업",
        CharacterAiActionTags.Work);

    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    [SerializeField] private FacilityWorkType workType = FacilityWorkType.None;
    [SerializeField] private float minimumDuration = 1f;
    [SerializeField] private int workInterruptPriority = 50;

    public WorkTypeId WorkTypeId => TryGetConfiguredWorkTypeId(out WorkTypeId workTypeId)
        ? workTypeId
        : default;
    public override bool IsContinuous => true;
    public override float MinimumDuration => minimumDuration;
    public override int InterruptPriority => workInterruptPriority;

    public override void PrepareScoreContext(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        if (actor != null && actor.TryGetAbility(out AbilityWork work))
        {
            work.SeedDecisionContext(in context);
        }
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        if (actor == null || !actor.TryGetAbility(out AbilityWork work))
        {
            return 0f;
        }

        float utilityScore = TryResolveWorkTypeId(actor, out WorkTypeId workTypeId)
            ? work.GetWorkUtilityScore(workTypeId, null)
            : work.GetAnyWorkUtilityScore(null);
        if (utilityScore <= 0f)
        {
            return 0f;
        }

        float availableWorkWeight = Mathf.Lerp(0.6f, 1f, utilityScore);
        return Mathf.Clamp01(baseScore * availableWorkWeight);
    }

    public override float AdjustScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        float baseScore)
    {
        if (actor != null && actor.TryGetAbility(out AbilityWork work))
        {
            work.SeedDecisionContext(in context);
        }

        return AdjustScore(actor, baseScore);
    }

    public override bool CanStart(CharacterActor actor)
    {
        if (actor == null || !actor.TryGetAbility(out AbilityWork work))
        {
            return false;
        }

        return TryResolveWorkTypeId(actor, out WorkTypeId workTypeId)
            ? work.CanStartWorkAction(workTypeId, null)
            : work.CanStartAnyWorkAction(null);
    }

    public override bool CanStart(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        if (actor != null && actor.TryGetAbility(out AbilityWork work))
        {
            work.SeedDecisionContext(in context);
        }

        return CanStart(actor);
    }

    public override bool CanContinue(CharacterActor actor, AIAction runningAction, out string stopReason)
    {
        stopReason = string.Empty;
        return actor != null
            && actor.TryGetAbility(out AbilityWork work)
            && work.CanContinueCurrentWork(out stopReason);
    }

    public override bool CanInterrupt(CharacterActor actor, AIAction runningAction, out string interruptReason)
    {
        interruptReason = string.Empty;
        return actor != null
            && actor.TryGetAbility(out AbilityWork work)
            && work.ShouldInterruptCurrentWork(out interruptReason);
    }

    public override void Execute(CharacterActor actor)
    {
        if (actor != null && actor.TryGetAbility(out AbilityWork work))
        {
            BuildableObject selectedDestination = actor.Brain != null
                ? actor.Brain.bestAction?.destination
                : null;
            if (TryResolveWorkTypeId(actor, out WorkTypeId workTypeId))
            {
                work.StartWorking(workTypeId, selectedDestination);
                actor.Brain?.ConsumePreferredWorkType(workTypeId);
            }
            else
            {
                work.StartAnyWork(selectedDestination);
            }
            return;
        }

        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }
    }

    public override void OnStop(CharacterActor actor, AIAction runningAction, string reason)
    {
        if (actor != null && actor.TryGetAbility(out AbilityWork work))
        {
            work.StopAssignedWorkFromAi(reason);
        }
    }

    public override bool TryReserveDestination(
        CharacterActor actor,
        BuildableObject destination,
        out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        if (destination == null)
        {
            return true;
        }

        if (destination.TryReserveWorker(actor, out FacilityAssignmentStatus status))
        {
            return true;
        }

        failure = AIActionFailure.Create(
            status.FailureKind.ToAiActionFailureKind(),
            status.Reason,
            destination);
        return false;
    }

    public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)
    {
        destination?.RefreshWorkerReservation(actor);
    }

    public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)
    {
        destination?.ReleaseWorkerReservation(actor);
    }

    public override IReadOnlyList<BuildableObject> GetDestinationCandidates(
        CharacterActor actor,
        GridPathSearchResult searchResult)
    {
        if (actor == null || !actor.TryGetAbility(out AbilityWork work))
        {
            return new List<BuildableObject>();
        }

        if (CanHandleSuppressCommand()
            && work.TryGetPrioritySuppressDestination(searchResult, out BuildableObject suppressDestination))
        {
            return new List<BuildableObject> { suppressDestination };
        }

        WorkTargetCandidate candidate;
        bool found = TryResolveWorkTypeId(actor, out WorkTypeId workTypeId)
            ? work.TryGetBestWorkCandidate(workTypeId, searchResult, out candidate)
            : work.TryGetBestAnyWorkCandidate(searchResult, out candidate);
        return found
            ? new List<BuildableObject> { candidate.Building }
            : new List<BuildableObject>();
    }

    public override bool TryResolveDestinationWithFailure(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out AIActionFailure failure)
    {
        destination = null;
        failure = AIActionFailure.None;
        if (actor == null || !actor.TryGetAbility(out AbilityWork work))
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.NoDestination);
            return false;
        }

        if (CanHandleSuppressCommand()
            && work.TryGetPrioritySuppressDestination(
                searchResult,
                out destination))
        {
            return destination != null && !destination.isDestroy;
        }

        WorkTargetCandidate candidate;
        bool found = TryResolveWorkTypeId(actor, out WorkTypeId workTypeId)
            ? work.TryGetBestWorkCandidate(
                workTypeId,
                searchResult,
                out candidate)
            : work.TryGetBestAnyWorkCandidate(
                searchResult,
                out candidate);
        if (!found || candidate.Building == null || candidate.Building.isDestroy)
        {
            AIActionFailureKind failureKind =
                candidate.FailureKind != AIActionFailureKind.None
                    ? candidate.FailureKind
                    : AIActionFailureKind.NoDestination;
            failure = AIActionFailure.Create(
                failureKind,
                candidate.FailureReason,
                candidate.Building);
            return false;
        }

        destination = candidate.Building;
        return true;
    }

    private bool CanHandleSuppressCommand()
    {
        return workType == FacilityWorkType.None || workType == FacilityWorkType.Guard;
    }

    private bool TryGetConfiguredWorkTypeId(out WorkTypeId workTypeId)
    {
        workTypeId = default;
        if (workType == FacilityWorkType.None)
        {
            return false;
        }

        if (!WorkTypeCatalog.TryGet(workType, out WorkTypeDefinition definition))
        {
            return false;
        }

        workTypeId = definition.WorkTypeId;
        return true;
    }

    private bool TryResolveWorkTypeId(
        CharacterActor actor,
        out WorkTypeId workTypeId)
    {
        if (TryGetConfiguredWorkTypeId(out workTypeId))
        {
            return true;
        }

        return actor != null
            && actor.Brain != null
            && actor.Brain.TryGetPreferredWorkType(out workTypeId);
    }
}
