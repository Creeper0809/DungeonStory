using System;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WorkOrderExecutionServices
{
    public WorkOrderExecutionServices(
        IWorkforceReplanService workforce,
        IGameClock gameClock,
        IUiClock uiClock,
        IDungeonDebugRuleQuery debugRules)
    {
        Workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        UiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        DebugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
    }

    public IWorkforceReplanService Workforce { get; }
    public IGameClock GameClock { get; }
    public IUiClock UiClock { get; }
    public IDungeonDebugRuleQuery DebugRules { get; }
}

public interface IWorkforceReplanService : IBuildingWorkforceReplanPort
{
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
    private readonly IFacilityCandidateCache facilityCandidateCache;

    public DungeonWorkforceReplanService(
        ICharacterWorldQuery characterWorld,
        IFacilityCandidateCache facilityCandidateCache)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.facilityCandidateCache = facilityCandidateCache;
    }

    public void RequestIdleWorkersToReplan(bool clearFailures = true)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (actor == null
                || actor.Brain == null
                || !actor.CanRunAi
                || !CharacterWorkRoleUtility.TryGetWork(
                    actor,
                    out AbilityWork work))
            {
                continue;
            }

            AIBrain brain = work.WorkerActor?.Brain;
            if (brain?.IsExternallyDrivenActionActive == true)
            {
                continue;
            }

            if (work.isWorking)
            {
                brain?.InvalidateQueuedActionForNextDecision();
                continue;
            }

            if (ShouldPreserveRunningNonWorkAction(work, brain, forceInterrupt: false))
            {
                brain.InvalidateQueuedActionForNextDecision();
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
        facilityCandidateCache?.MarkDynamicStateDirty();
        AbilityWork selectedWork = null;
        WorkTargetCandidate selectedCandidate = default;
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (!CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
                || actor == null
                || actor.Brain == null
                || !actor.CanRunAi)
            {
                continue;
            }

            if (actor.Brain.IsExternallyDrivenActionActive)
            {
                continue;
            }

            if (ShouldPreserveRunningNonWorkAction(work, actor.Brain, forceInterrupt))
            {
                continue;
            }

            WorkTypeId assignedWorkTypeId = work.AssignedWorkTypeId;
            if (work.IsOffDuty
                || !work.WorkPriorities.IsEnabled(requestedWorkTypeId))
            {
                continue;
            }

            if (assignedWorkTypeId == requestedWorkTypeId && work.isWorking)
            {
                continue;
            }

            if (!work.CanStartWorkAction(requestedWorkTypeId, null)
                || !work.TryGetBestWorkCandidate(
                    requestedWorkTypeId,
                    null,
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
            if (!canReplaceCurrent)
            {
                continue;
            }

            if (selectedWork == null
                || (selectedWork.isWorking && !work.isWorking)
                || (selectedWork.isWorking == work.isWorking
                    && candidate.Score > selectedCandidate.Score))
            {
                selectedWork = work;
                selectedCandidate = candidate;
            }
        }

        if (selectedWork == null)
        {
            return;
        }

        AIBrain selectedBrain = selectedWork.WorkerActor.Brain;
        selectedBrain.PreferWorkActionOnNextDecision(
            requestedWorkTypeId,
            persistenceSeconds: 600f);
        if (selectedBrain.HasRunningAction)
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

        AbilityWork selectedWork = null;
        bool selectedCanInterrupt = false;
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (!CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
                || actor == null
                || actor.Brain == null
                || !actor.CanRunAi
                || actor.Brain.IsExternallyDrivenActionActive
                || ShouldPreserveRunningNonWorkAction(work, actor.Brain, forceInterrupt)
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
            if (selectedWork == null
                || (canInterrupt && !selectedCanInterrupt)
                || (canInterrupt == selectedCanInterrupt
                    && selectedWork.isWorking && !work.isWorking)
                || (canInterrupt == selectedCanInterrupt
                    && selectedWork.isWorking == work.isWorking
                    && requestedPriority
                        < selectedWork.WorkPriorities.GetPriority(
                            BuiltInWorkTypeIds.Haul)))
            {
                selectedWork = work;
                selectedCanInterrupt = canInterrupt;
            }
        }

        if (selectedWork == null)
        {
            return;
        }

        AIBrain selectedBrain = selectedWork.WorkerActor.Brain;
        if (!selectedBrain.PreferActionOnNextDecision<AIHaul>(
                persistenceSeconds: 600f))
        {
            selectedBrain.UseStaffWorkActions();
            if (!selectedBrain.PreferActionOnNextDecision<AIHaul>(
                    persistenceSeconds: 600f))
            {
                return;
            }
        }

        if (!selectedCanInterrupt)
        {
            return;
        }

        if (selectedBrain.HasRunningAction)
        {
            selectedBrain.StopCurrentActionForReplan("공사 자재 운반 시작");
        }

        selectedBrain.RequestImmediateReplan(clearFailures);
    }

    public static bool ShouldPreserveRunningNonWorkAction(
        AbilityWork work,
        AIBrain brain,
        bool forceInterrupt)
    {
        // A non-forced workforce notification is a wake-up hint, never an
        // interruption authority. This also protects the one-frame work
        // finalization boundary where AbilityWork.isWorking has cleared but
        // the current AI action has not yet completed. Only callers that
        // explicitly opt into forceInterrupt may replace a running action.
        return !forceInterrupt
            && work != null
            && brain?.HasRunningAction == true;
    }
}
