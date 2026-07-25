using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class RepairWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Repair };
    private readonly ICombatEquipmentMaintenanceRuntime maintenanceRuntime;
    private readonly IWorkAmountCalculator workAmountCalculator;
    private readonly IGameClock gameClock;

    public RepairWorkExecutionHandler(
        ICombatEquipmentMaintenanceRuntime maintenanceRuntime,
        IWorkAmountCalculator workAmountCalculator,
        IGameClock gameClock)
    {
        this.maintenanceRuntime = maintenanceRuntime
            ?? throw new ArgumentNullException(nameof(maintenanceRuntime));
        this.workAmountCalculator = workAmountCalculator
            ?? throw new ArgumentNullException(nameof(workAmountCalculator));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        return target != null
            && (target.IsDamaged || maintenanceRuntime.HasRepairWorkFor(target));
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return target != null && maintenanceRuntime.HasRepairWorkFor(target)
            ? maintenanceRuntime.GetRepairUrgency(target)
            : 0f;
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        if (context.Target == null)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        if (maintenanceRuntime.HasRepairWorkFor(context.Target))
        {
            float lastReportAt = -10f;
            while (context.CanContinue && maintenanceRuntime.HasRepairWorkFor(context.Target))
            {
                float deltaWork = workAmountCalculator.CalculateWorkPerSecond(
                        context.Actor,
                        context.Target,
                        context.WorkTypeId,
                        context.Work.GetWorkEnvironmentDurationMultiplier(context.WorkTypeId))
                    * gameClock.DeltaTime;
                if (!maintenanceRuntime.TryApplyRepairWork(
                        context.Actor,
                        context.Target,
                        deltaWork,
                        out bool completed,
                        out string message))
                {
                    result.CompletedSuccessfully = false;
                    context.Actor?.AddActivity(CharacterActivityEvent.Work(
                        FacilityWorkType.Repair,
                        CharacterActivityOutcomes.Blocked,
                        $"장비 수리 중단: {message}",
                        context.Target,
                        reasonCode: "equipment-repair-blocked",
                        bubbleEligible: true));
                    yield break;
                }

                if (gameClock.Time - lastReportAt >= 0.75f)
                {
                    lastReportAt = gameClock.Time;
                    context.Actor?.Brain?.SetActionPhase(message, context.Target);
                }

                if (completed)
                {
                    context.Actor?.AddActivity(CharacterActivityEvent.Work(
                        FacilityWorkType.Repair,
                        CharacterActivityOutcomes.Completed,
                        message,
                        context.Target,
                        reasonCode: "equipment-repair-completed",
                        bubbleEligible: true));
                    yield break;
                }

                yield return null;
            }

            result.CompletedSuccessfully = false;
            yield break;
        }

        float requiredWork = Mathf.Max(
            0.1f,
            context.Target.BuildingData.GetRequiredWork(BuiltInWorkTypeIds.Repair));
        float repairMultiplier =
            CharacterSkillRuntimeEffects.GetRepairSpeedMultiplier(context.Actor);
        yield return context.ExecuteWorkAmount(requiredWork, "수리", repairMultiplier);
        if (!context.CanContinue)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        context.Target.SetDamaged(false);
        context.Actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Repair,
            CharacterActivityOutcomes.Completed,
            $"수리 완료: {context.Target.name}",
            context.Target));
    }
}
