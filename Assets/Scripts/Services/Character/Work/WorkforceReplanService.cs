using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IWorkforceReplanService
{
    void RequestIdleWorkersToReplan(bool clearFailures = true);
    void RequestOneWorkerToReplanFor(
        WorkTypeId workTypeId,
        bool clearFailures = true,
        bool forceInterrupt = false);
    void RequestOneHaulerToReplan(
        bool clearFailures = true,
        bool forceInterrupt = false);
}

public sealed class DungeonWorkforceReplanService : IWorkforceReplanService
{
    private readonly ICharacterWorldQuery characterWorld;

    public DungeonWorkforceReplanService(ICharacterWorldQuery characterWorld)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
    }

    public void RequestIdleWorkersToReplan(bool clearFailures = true)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        foreach (CharacterActor actor in characterWorld.Characters)
        {
            AbilityWork work = actor != null ? actor.GetAbility<AbilityWork>() : null;
            if (work == null)
            {
                continue;
            }

            AIBrain brain = work.WorkerActor?.Brain;
            if (work.isWorking)
            {
                brain?.InvalidateQueuedActionForNextDecision();
                continue;
            }

            brain?.RequestImmediateReplan(clearFailures);
        }
    }

    public void RequestOneWorkerToReplanFor(
        WorkTypeId workTypeId,
        bool clearFailures = true,
        bool forceInterrupt = false)
    {
        if (!Application.isPlaying
            || !WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition requestedDefinition))
        {
            return;
        }

        WorkTypeId requestedWorkTypeId = requestedDefinition.WorkTypeId;
        List<(AbilityWork work, WorkTargetCandidate candidate)> eligible =
            new List<(AbilityWork work, WorkTargetCandidate candidate)>();
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            AbilityWork work = actor != null ? actor.GetAbility<AbilityWork>() : null;
            AIBrain brain = actor != null ? actor.Brain : null;
            WorkTypeId assignedWorkTypeId = work != null
                ? work.AssignedWorkTypeId
                : default;
            if (work == null
                || actor == null
                || brain == null
                || work.IsOffDuty
                || assignedWorkTypeId == requestedWorkTypeId
                || !work.WorkPriorities.IsEnabled(requestedWorkTypeId))
            {
                continue;
            }

            GridPathSearchResult search = brain.GetPathSearch(actor);
            if (!work.CanStartWorkAction(requestedWorkTypeId, search)
                || !work.TryGetBestWorkCandidate(
                    requestedWorkTypeId,
                    search,
                    out WorkTargetCandidate candidate))
            {
                continue;
            }

            WorkPriorityLevel requestedPriority =
                work.WorkPriorities.GetPriority(requestedWorkTypeId);
            WorkPriorityLevel currentPriority = assignedWorkTypeId.IsValid
                ? work.WorkPriorities.GetPriority(assignedWorkTypeId)
                : WorkPriorityLevel.Off;
            bool canReplaceCurrent = forceInterrupt
                || !work.isWorking
                || currentPriority == WorkPriorityLevel.Off
                || requestedPriority <= currentPriority;
            if (canReplaceCurrent)
            {
                eligible.Add((work, candidate));
            }
        }

        (AbilityWork work, WorkTargetCandidate candidate) selected = eligible
            .OrderBy(entry => entry.work.isWorking)
            .ThenByDescending(entry => entry.candidate.Score)
            .FirstOrDefault();
        if (selected.work == null)
        {
            return;
        }

        AIBrain selectedBrain = selected.work.WorkerActor.Brain;
        selectedBrain.PreferWorkActionOnNextDecision(
            requestedWorkTypeId,
            persistenceSeconds: 600f);
        if (selected.work.isWorking)
        {
            selectedBrain.StopCurrentActionForReplan(
                $"{requestedDefinition.DisplayName} 작업 시작");
        }

        selectedBrain.RequestImmediateReplan(clearFailures);
    }

    public void RequestOneHaulerToReplan(
        bool clearFailures = true,
        bool forceInterrupt = false)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        List<(AbilityWork work, bool canInterrupt)> eligible =
            new List<(AbilityWork work, bool canInterrupt)>();
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            AbilityWork work = actor != null ? actor.GetAbility<AbilityWork>() : null;
            AIBrain brain = actor != null ? actor.Brain : null;
            if (work == null
                || actor == null
                || brain == null
                || !actor.CanRunAi
                || work.IsOffDuty
                || !work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Haul))
            {
                continue;
            }

            WorkPriorityLevel requestedPriority =
                work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul);
            WorkTypeId assignedWorkTypeId = work.AssignedWorkTypeId;
            WorkPriorityLevel currentPriority = assignedWorkTypeId.IsValid
                ? work.WorkPriorities.GetPriority(assignedWorkTypeId)
                : WorkPriorityLevel.Off;
            bool canInterrupt = forceInterrupt
                || !work.isWorking
                || currentPriority == WorkPriorityLevel.Off
                || requestedPriority <= currentPriority;
            eligible.Add((work, canInterrupt));
        }

        (AbilityWork work, bool canInterrupt) selected = eligible
            .OrderByDescending(entry => entry.canInterrupt)
            .ThenBy(entry => entry.work.isWorking)
            .ThenBy(entry => entry.work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul))
            .FirstOrDefault();
        if (selected.work == null)
        {
            return;
        }

        AIBrain selectedBrain = selected.work.WorkerActor.Brain;
        if (!selected.canInterrupt)
        {
            selectedBrain.PreferActionOnNextDecision<AIHaul>(persistenceSeconds: 600f);
            return;
        }

        if (selected.work.isWorking)
        {
            selectedBrain.StopCurrentActionForReplan("공사 자재 운반 시작");
        }

        selectedBrain.RequestImmediateReplanForAction<AIHaul>(clearFailures);
    }
}
