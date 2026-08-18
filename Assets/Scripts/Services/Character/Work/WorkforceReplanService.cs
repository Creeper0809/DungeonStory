using System;
using System.Collections.Generic;
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
        bool forceInterrupt = false,
        CharacterId protectedCharacterId = default,
        bool forcePriorityWakeFanout = false);
}

public sealed class DungeonWorkforceReplanService : IWorkforceReplanService
{
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly IWorldItemHaulPlanningService haulPlanningService;

    public string LastHaulReplanDetail { get; private set; } = "not-requested";

    public DungeonWorkforceReplanService(
        ICharacterWorldQuery characterWorld,
        IFacilityCandidateCache facilityCandidateCache,
        IWorldItemHaulPlanningService haulPlanningService)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.facilityCandidateCache = facilityCandidateCache;
        this.haulPlanningService = haulPlanningService;
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
        bool forceInterrupt = false,
        CharacterId protectedCharacterId = default,
        bool forcePriorityWakeFanout = false)
    {
        LastHaulReplanDetail =
            $"requested:playing={Application.isPlaying}:forceInterrupt={forceInterrupt}:"
            + $"clearFailures={clearFailures}:protected={protectedCharacterId.Value}:"
            + $"priorityWakeFanout={forcePriorityWakeFanout}";
        if (!Application.isPlaying)
        {
            LastHaulReplanDetail += ":result=not-playing";
            return;
        }

        AbilityWork selectedWork = null;
        bool selectedCanInterrupt = false;
        bool selectedIsProtected = true;
        bool selectedHasUrgentSurvivalNeed = true;
        bool selectedHasPriorityPlan = false;
        WorldItemHaulPlan selectedPreviewPlan = null;
        List<(AbilityWork Work, bool CanInterrupt)> priorityWakeCandidates = new();
        int observedActors = 0;
        int eligibleActors = 0;
        int missingWorkOrBrain = 0;
        int cannotRunAi = 0;
        int externalOwners = 0;
        int preservedActions = 0;
        int offDuty = 0;
        int haulDisabled = 0;
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            observedActors++;
            if (actor == null
                || !CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
                || actor.Brain == null)
            {
                missingWorkOrBrain++;
                continue;
            }
            if (!actor.CanRunAi)
            {
                cannotRunAi++;
                continue;
            }
            if (actor.Brain.IsExternallyDrivenActionActive)
            {
                externalOwners++;
                continue;
            }
            if (ShouldPreserveRunningNonWorkAction(work, actor.Brain, forceInterrupt))
            {
                preservedActions++;
                continue;
            }
            if (work.IsOffDuty)
            {
                offDuty++;
                continue;
            }
            if (!work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Haul))
            {
                haulDisabled++;
                continue;
            }

            eligibleActors++;

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
            bool hasUrgentSurvivalNeed = HasUrgentSurvivalNeed(actor);
            bool isProtected = protectedCharacterId.IsValid
                && CharacterPersistentIdentity.Require(actor).Equals(
                    protectedCharacterId);
            WorldItemHaulPlan previewPlan = null;
            bool hasPriorityPlan = haulPlanningService != null
                && haulPlanningService.TryPreviewBestPlan(
                    actor,
                    out previewPlan,
                    out _)
                && previewPlan?.IsPriority == true;
            if (!hasUrgentSurvivalNeed && !isProtected)
            {
                priorityWakeCandidates.Add((work, canInterrupt));
            }
            if (selectedWork == null
                || (hasPriorityPlan && !selectedHasPriorityPlan)
                || (hasPriorityPlan == selectedHasPriorityPlan
                    && ((!isProtected && selectedIsProtected)
                        || (isProtected == selectedIsProtected
                            && !hasUrgentSurvivalNeed
                            && selectedHasUrgentSurvivalNeed)
                        || (isProtected == selectedIsProtected
                            && hasUrgentSurvivalNeed == selectedHasUrgentSurvivalNeed
                            && canInterrupt && !selectedCanInterrupt)
                        || (isProtected == selectedIsProtected
                            && hasUrgentSurvivalNeed == selectedHasUrgentSurvivalNeed
                            && canInterrupt == selectedCanInterrupt
                            && selectedWork.isWorking && !work.isWorking)
                        || (isProtected == selectedIsProtected
                            && hasUrgentSurvivalNeed == selectedHasUrgentSurvivalNeed
                            && canInterrupt == selectedCanInterrupt
                            && selectedWork.isWorking == work.isWorking
                            && requestedPriority
                                < selectedWork.WorkPriorities.GetPriority(
                                    BuiltInWorkTypeIds.Haul)))))
            {
                selectedWork = work;
                selectedCanInterrupt = canInterrupt;
                selectedIsProtected = isProtected;
                selectedHasUrgentSurvivalNeed = hasUrgentSurvivalNeed;
                selectedHasPriorityPlan = hasPriorityPlan;
                selectedPreviewPlan = hasPriorityPlan ? previewPlan : null;
            }
        }

        // The path broker may defer every read-only preview in the exact frame
        // that a newly routed priority stack wakes the workforce. It can also
        // publish the priority preview for only the hungry/thirsty consumer
        // while the non-urgent haulers are still deferred. Selecting that actor
        // makes survival arbitration correctly beat hauling, while unrelated
        // workers remain asleep. Wake the bounded non-urgent cohort in either
        // boundary; the first actor whose path snapshot completes reserves the
        // exact stack and the others naturally observe it as unavailable.
        if (haulPlanningService?.HasPendingPriorityWork == true
            && (forcePriorityWakeFanout
                || !selectedHasPriorityPlan
                || selectedHasUrgentSurvivalNeed
                || selectedIsProtected))
        {
            int dispatched = 0;
            List<string> dispatchedIds = new();
            foreach ((AbilityWork work, bool canInterrupt) in priorityWakeCandidates)
            {
                // Liveness-critical physical deliveries may wake a bounded
                // cohort. Quantity reservation remains the single pickup
                // authority, while independent path searches prevent one
                // deferred broker continuation from stalling the service.
                if (forcePriorityWakeFanout && dispatched >= 3)
                {
                    break;
                }
                if (!canInterrupt || work?.WorkerActor?.Brain == null)
                {
                    continue;
                }

                AIBrain brain = work.WorkerActor.Brain;
                if (!brain.PreferActionOnNextDecision<AIHaul>(
                        persistenceSeconds: 600f))
                {
                    brain.UseStaffWorkActions();
                    if (!brain.PreferActionOnNextDecision<AIHaul>(
                            persistenceSeconds: 600f))
                    {
                        continue;
                    }
                }
                if (brain.HasRunningAction)
                {
                    brain.StopCurrentActionForReplan("우선 운반 경로 준비");
                }
                brain.RequestImmediateReplan(clearFailures);
                dispatched++;
                dispatchedIds.Add(
                    CharacterPersistentIdentity.Require(work.WorkerActor).Value);
            }

            if (dispatched > 0)
            {
                LastHaulReplanDetail +=
                    $":observed={observedActors}:eligible={eligibleActors}:"
                    + $"priorityPathFanout={dispatched}:"
                    + $"actors={string.Join(",", dispatchedIds)}:"
                    + "result=priority-path-deferred-dispatched";
                return;
            }
        }

        if (selectedWork == null)
        {
            LastHaulReplanDetail +=
                $":observed={observedActors}:eligible={eligibleActors}:"
                + $"missingWorkOrBrain={missingWorkOrBrain}:cannotRunAi={cannotRunAi}:"
                + $"external={externalOwners}:preserved={preservedActions}:"
                + $"offDuty={offDuty}:haulDisabled={haulDisabled}:result=no-candidate";
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
                LastHaulReplanDetail +=
                    $":observed={observedActors}:eligible={eligibleActors}:"
                    + $"selected={CharacterPersistentIdentity.Require(selectedWork.WorkerActor).Value}:"
                    + $"protected={selectedIsProtected}:"
                    + $"urgentSurvival={selectedHasUrgentSurvivalNeed}:"
                    + "result=aihaul-action-missing";
                return;
            }
        }

        if (!selectedCanInterrupt)
        {
            LastHaulReplanDetail +=
                $":observed={observedActors}:eligible={eligibleActors}:"
                + $"selected={CharacterPersistentIdentity.Require(selectedWork.WorkerActor).Value}:"
                + $"protected={selectedIsProtected}:"
                + $"urgentSurvival={selectedHasUrgentSurvivalNeed}:"
                + $"running={selectedBrain.HasRunningAction}:result=not-interruptible";
            return;
        }

        if (selectedBrain.HasRunningAction)
        {
            selectedBrain.StopCurrentActionForReplan("공사 자재 운반 시작");
        }

        selectedBrain.RequestImmediateReplan(clearFailures);
        LastHaulReplanDetail +=
            $":observed={observedActors}:eligible={eligibleActors}:"
            + $"selected={CharacterPersistentIdentity.Require(selectedWork.WorkerActor).Value}:"
            + $"protected={selectedIsProtected}:"
            + $"urgentSurvival={selectedHasUrgentSurvivalNeed}:"
            + $"priorityPlan={selectedHasPriorityPlan}:"
            + $"destination={selectedPreviewPlan?.PrimaryDestinationId ?? string.Empty}:"
            + $"running={selectedBrain.HasRunningAction}:"
            + $"preferred={selectedBrain.IsActionPreferredForNextDecision<AIHaul>()}:"
            + "result=dispatched";
    }

    private static bool HasUrgentSurvivalNeed(CharacterActor actor)
    {
        return CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(
                actor,
                CharacterCondition.HUNGER)
            || CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(
                actor,
                CharacterCondition.THIRST)
            || CharacterNeedAiThresholds.IsEmergency(
                actor,
                CharacterCondition.SLEEP)
            || CharacterNeedAiThresholds.IsEmergency(
                actor,
                CharacterCondition.EXCRETION)
            || CharacterNeedAiThresholds.IsEmergency(
                actor,
                CharacterCondition.HYGIENE);
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
